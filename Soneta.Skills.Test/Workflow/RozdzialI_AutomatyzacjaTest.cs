using System;
using System.Linq;
using AwesomeAssertions;
using NUnit.Framework;
using Soneta.Business;
using Soneta.Business.Db;
using Soneta.Business.Db.Notifications;
using Soneta.Core;
using Soneta.Core.Schedule;
using Soneta.Types;
using Soneta.Workflow;
using Soneta.Workflow.Config;

namespace Soneta.Skills.Test.Workflow;

/// <summary>
/// Rozdział I — 'Powiadomienia, triggery i automatyzacja procesu' (receptury WORKFLOW-I1 … WORKFLOW-I5).
/// <para>
/// Testy są <b>wykonywalną dokumentacją</b> publicznego kontraktu platformy Soneta dla domeny
/// Workflow. Każda metoda mapuje się 1:1 do receptury z dokumentu skilla
/// <c>workflow/WORKFLOW09-automatyzacja.md</c>.
/// </para>
/// <para>
/// <b>Model 'automatyzacja = konfiguracja + zapis'.</b> Cała automatyzacja procesu jest opisana
/// <b>danymi konfiguracyjnymi</b> podpiętymi do definicji (węzeł <c>TaskDefinition</c>): powiadomienia
/// (<c>SysNotification</c>), triggery (<c>TaskTrigger</c>), para <c>IsStart</c> + <c>ActionRunAt.Auto</c>
/// na węźle startowym, tranzycje automatyczne (<c>WFTransition.IsUserDecision == false</c>) oraz
/// harmonogramy (<c>ScheduleDefinition</c>). Testujemy <b>zakładanie i utrwalenie</b> tych obiektów
/// konfiguracyjnych (<c>InConfigTransaction</c> + <c>AddConfig</c>/<c>GetConfig</c> +
/// <c>SaveDisposeConfig</c>) oraz klucze tabel.
/// </para>
/// <para>
/// <b>GRANICA TESTOWALNOŚCI:</b> FAKTYCZNE wystrzelenie automatyzacji dzieje się w
/// <c>session.Save()</c> sesji operacyjnej (wewnętrzny Saver przegląda triggery) albo w usłudze
/// Harmonogram Zadań (HZ) na serwerze. W teście jednostkowym nie ma uruchomionego silnika triggerów
/// ani usługi HZ, więc scenariuszy end-to-end (start procesu na zapis, wykonanie cyklu) NIE testujemy —
/// sprawdzamy wyłącznie konfigurację, jej utrwalenie i klucze odczytu.
/// </para>
/// <para>
/// Baza Demo nie musi zawierać definicji procesów — każdy scenariusz buduje własną definicję od zera
/// (WORKFLOW-A1) i węzeł (WORKFLOW-B1); rollback po teście sprząta. Operujemy wyłącznie na publicznym
/// kontrakcie.
/// </para>
/// </summary>
[TestFixture]
public class RozdzialI_AutomatyzacjaTest : WorkflowTestBase
{
    /// <summary>Moduł Workflow bieżącej sesji KONFIGURACYJNEJ (wzorce WFDefItems).</summary>
    private WorkflowModule ConfigWorkflow => ConfigEditSession.GetWorkflow();

    /// <summary>Moduł Business bieżącej sesji KONFIGURACYJNEJ (TaskDefs, SysNotifications, TaskTriggers).</summary>
    private BusinessModule ConfigBusiness => ConfigEditSession.GetBusiness();

    /// <summary>Unikalny symbol definicji na potrzeby pojedynczego testu.</summary>
    private static string NowySymbol(string prefix) =>
        prefix + "_" + Guid.NewGuid().ToString("N").Substring(0, 6);

    /// <summary>Nowa definicja procesu (WORKFLOW-A1) — wołać WEWNĄTRZ transakcji konfiguracyjnej.</summary>
    private WFDefinition NowaDefinicja(string prefix)
    {
        var definicja = AddConfig(new WFDefinition());
        definicja.Symbol = NowySymbol(prefix);
        definicja.Name = "Proces testowy " + definicja.Symbol;
        return definicja;
    }

    /// <summary>
    /// Nowy węzeł wg receptury WORKFLOW-B1: konstruktor z NAZWĄ TABELI obiektu nadrzędnego, potem
    /// NAJPIERW <c>WFDefinition</c>, a dopiero POTEM wzorzec <c>WFDefItem</c>. Wołać WEWNĄTRZ
    /// transakcji konfiguracyjnej.
    /// </summary>
    private TaskDefinition NowyWezel(WFDefinition definicja, string wzorzec, string tabela = "Kontrahenci")
    {
        var wezel = AddConfig(new TaskDefinition(tabela));   // tabela tylko w KONSTRUKTORZE (readonly)
        wezel.WFDefinition = definicja;                      // 1) najpierw definicja procesu
        wezel.WFDefItem = ConfigWorkflow.WFDefItems.ByName[wzorzec];   // 2) potem wzorzec elementu
        return wezel;
    }

    /// <summary>
    /// WORKAROUND środowiska testowego (nie część receptury!): w bieżącej bazie testowej zapis
    /// JAKIEGOKOLWIEK wiersza <c>TaskTrigger</c> dodanego automatycznie w <c>TaskDefinition.OnAdded</c>
    /// kończy się błędem SQL (kolumna <c>TaskTriggerGuid</c> NOT NULL). Przed <c>SaveDisposeConfig()</c>
    /// usuwamy więc świeżo dodane triggery; konfiguracja węzłów zapisuje się bez przeszkód.
    /// Wołać NA KOŃCU transakcji konfiguracyjnej. UWAGA: WORKFLOW-I2 świadomie pomija ten workaround
    /// w transakcji (bada trigger przed zapisem) i nie zapisuje go.
    /// </summary>
    private void UsunAutoTriggeryWorkaround()
    {
        foreach (TaskTrigger trigger in ConfigBusiness.TaskTriggers.Cast<TaskTrigger>()
                     .Where(t => t.State == RowState.Added).ToList())
            trigger.Delete();
    }

    // ============================== WORKFLOW-I1 — Powiadomienie z węzła procesu (★) ==============================

    [Test]
    [Description("WORKFLOW-I1: powiadomienie węzła = new SysNotification(host) gdzie host=węzeł " +
                 "(TaskDefinition implementuje ISysNotificationHost); Host ustawia WYŁĄCZNIE konstruktor; " +
                 "Mode jest WYLICZANE z Template (null ⇒ System); po zapisie odczyt kolekcją " +
                 "TaskDefinition.SysNotifications oraz kluczem SysNotifications.WgHost (po polu Host).")]
    public void WORKFLOW_I1_PowiadomienieSystemowe_KonstruktorZHostem_IUtrwalenie()
    {
        Guid guidWezel = Guid.Empty;
        Guid guidPow = Guid.Empty;

        InConfigTransaction(() =>
        {
            var definicja = NowaDefinicja("I1");
            var wezel = NowyWezel(definicja, "Zadanie");
            wezel.FormatedName = "Akceptacja";

            // Powiadomienie SYSTEMOWE: host = węzeł (ustawiany WYŁĄCZNIE konstruktorem — brak settera Host).
            var powiadomienie = AddConfig(new SysNotification(wezel));
            powiadomienie.Name = "Po akceptacji";
            powiadomienie.Fact = SysNotificationFact.OnRealize;                 // okoliczność: realizacja zadania
            powiadomienie.RecipientType = SysNotificationRecipientType.ProcessOwner;
            powiadomienie.Finalization = SysNotificationFinalization.OnProcessFinalized;
            powiadomienie.ErrorReaction = SysNotificationErrorReactionType.GenerateSystemNotification;
            // NotificationType (Typ powiadomienia) jest WYMAGANY (NotificationTypeRequiredVerifier) —
            // wskazuje definicję zadania-powiadomienia (węzeł wzorca 'Powiadomienie') generowaną dla odbiorcy:
            powiadomienie.NotificationType = NowyWezel(definicja, "Powiadomienie");

            // Mode jest WYLICZANE z Template (null ⇒ System) — nie da się go ustawić wprost:
            powiadomienie.Template.Should().BeNull("powiadomienie systemowe nie ma szablonu (Template == null)");
            powiadomienie.Mode.Should().Be(SysNotification.NotificationMode.System,
                "Mode wynika z Template == null — to powiadomienie systemowe (tworzy zadanie-powiadomienie)");

            // Host wskazuje nasz węzeł (relacja przez ISysNotificationHost):
            powiadomienie.Host.Should().BeSameAs((ISysNotificationHost)wezel,
                "Host = węzeł, ustawiony konstruktorem new SysNotification(host)");

            guidWezel = wezel.Guid;
            guidPow = powiadomienie.Guid;
            UsunAutoTriggeryWorkaround();
        });
        SaveDisposeConfig();

        // Odczyt po zapisie — świeża sesja konfiguracyjna:
        var wezel2 = GetConfig<TaskDefinition>(guidWezel);

        // Kolekcja zwrotna węzła:
        var powiadomienia = wezel2.SysNotifications.Cast<SysNotification>().ToList();
        powiadomienia.Should().ContainSingle("dodaliśmy jedno powiadomienie do węzła");
        powiadomienia[0].Name.Should().Be("Po akceptacji");
        powiadomienia[0].Fact.Should().Be(SysNotificationFact.OnRealize);
        powiadomienia[0].RecipientType.Should().Be(SysNotificationRecipientType.ProcessOwner);
        powiadomienia[0].Mode.Should().Be(SysNotification.NotificationMode.System,
            "tryb (Mode) utrwalony jako System");

        // Klucz tabeli SysNotifications.WgHost — podzbiór powiadomień węzła. UWAGA: powiadomienie węzła
        // wiąże się z hostem przez pole Host (klucz WgHost), a NIE przez obsolete kolumnę TaskDefinition
        // (klucz WgTaskDefinition pozostaje pusty dla hosta typu TaskDefinition — kolumna TaskDefinition
        // jest zerowana dla węzłów; to ten sam klucz, którego używa kolekcja TaskDefinition.SysNotifications).
        var poKluczu = ConfigBusiness.SysNotifications.WgHost[(ISysNotificationHost)wezel2]
            .Cast<SysNotification>().ToList();
        poKluczu.Should().ContainSingle("klucz WgHost odnajduje powiadomienia węzła");
        poKluczu[0].Guid.Should().Be(guidPow);
    }

    [Test]
    [Description("WORKFLOW-I1 (pułapka): DelayUnit/DelayValue są read-only, dopóki Fact != OnDelayedTime — " +
                 "ustawienie opóźnienia przed zmianą Fact rzuca ColReadOnlyException; bezpieczna droga to " +
                 "SetDelay(jednostka, wartość), które samo ustawia Fact=OnDelayedTime i opóźnienie.")]
    public void WORKFLOW_I1_Opoznienie_SetDelay_UstawiaFactIWartosc()
    {
        InConfigTransaction(() =>
        {
            var definicja = NowaDefinicja("I1D");
            var wezel = NowyWezel(definicja, "Zadanie");
            wezel.FormatedName = "Zadanie z powiadomieniem opóźnionym";

            var powiadomienie = AddConfig(new SysNotification(wezel));
            powiadomienie.Name = "Przypomnienie";

            // PUŁAPKA: przy Fact innym niż OnDelayedTime pola opóźnienia są read-only.
            powiadomienie.Fact.Should().NotBe(SysNotificationFact.OnDelayedTime,
                "świeże powiadomienie nie startuje w trybie opóźnionym");
            powiadomienie.IsReadOnlyDelayValue().Should().BeTrue(
                "DelayValue jest read-only dopóki Fact != OnDelayedTime");
            System.Action zaWczesnie = () => powiadomienie.DelayValue = 24;
            zaWczesnie.Should().Throw<ColReadOnlyException>(
                "ustawienie DelayValue przed Fact=OnDelayedTime rzuca ColReadOnlyException");

            // Bezpieczna droga: SetDelay ustawia Fact=OnDelayedTime + jednostkę + wartość jednym wywołaniem.
            powiadomienie.SetDelay(SysNotificationDelayUnit.Hours, 24);
            powiadomienie.Fact.Should().Be(SysNotificationFact.OnDelayedTime,
                "SetDelay przełącza Fact na OnDelayedTime (odblokowuje pola opóźnienia)");
            powiadomienie.DelayUnit.Should().Be(SysNotificationDelayUnit.Hours);
            powiadomienie.DelayValue.Should().Be(24);
        });
        // Bez zapisu — pokazujemy zachowanie setterów; rollback sprząta.
    }

    // ============================== WORKFLOW-I2 — Trigger (wyzwalacz) (★) ==============================

    [Test]
    [Description("WORKFLOW-I2: węzeł na tabeli TableName dostaje DOMYŚLNY TaskTrigger automatycznie w OnAdded; " +
                 "trigger to child węzła (TaskTrigger.TaskDefinition) na tabeli TableName; odczyt kolekcją " +
                 "TaskDefinition.TaskTriggers oraz kluczem TaskTriggers.ByTableName. " +
                 "GRANICA: faktyczne wyzwolenie dzieje się w session.Save() sesji operacyjnej — nie testujemy go.")]
    public void WORKFLOW_I2_DomyslnyTrigger_DodawanyWOnAdded_NaTabeliWezla()
    {
        InConfigTransaction(() =>
        {
            var definicja = NowaDefinicja("I2");

            // Węzeł na tabeli "Kontrahenci" — OnAdded automatycznie dokłada domyślny trigger dla tej tabeli:
            var wezel = NowyWezel(definicja, "Zadanie");
            wezel.FormatedName = "Obsługa kontrahenta";

            // Domyślny trigger jest childem węzła (kolekcja TaskTriggers) — nie tworzymy go ręcznie:
            var triggery = wezel.TaskTriggers.Cast<TaskTrigger>().ToList();
            triggery.Should().NotBeEmpty(
                "OnAdded automatycznie dokłada domyślny TaskTrigger dla tabeli węzła");
            var domyslny = triggery[0];
            domyslny.TableName.Should().Be("Kontrahenci",
                "domyślny trigger obserwuje własną tabelę węzła (TableName)");
            domyslny.TaskDefinition.Should().BeSameAs(wezel,
                "trigger to child węzła — TaskDefinition wskazuje węzeł-właściciel (guided-parent)");

            // Klucz tabeli TaskTriggers.ByTableName — silnik szuka nim triggerów w session.Save():
            var poTabeli = ConfigBusiness.TaskTriggers.ByTableName["Kontrahenci"]
                .Cast<TaskTrigger>().ToList();
            poTabeli.Should().Contain(t => t.ID == domyslny.ID,
                "ByTableName[tabela] odnajduje trigger — tym kluczem Saver wybiera triggery dla zapisu");

            // GRANICA TESTOWALNOŚCI: trigger faktycznie wyzwala przeliczenie/start dopiero w
            // session.Save() sesji OPERACYJNEJ (Saver woła GetGuidedRows()) — w teście brak silnika
            // triggerów, więc end-to-end nie testujemy; potwierdzamy tylko konfigurację + klucz.
        });
        // Bez zapisu (workaround SQL na TaskTrigger) — badamy trigger PRZED zapisem; rollback sprząta.
    }

    // POMINIĘTE: ręczne tworzenie new TaskTrigger() + ustawianie TableName/Code i jego ZAPIS —
    // w bieżącej bazie testowej zapis TaskTrigger rzuca błąd SQL (kolumna TaskTriggerGuid NOT NULL,
    // ten sam workaround co w RozdzialB). Konfigurację triggera (kolejność TaskDefinition→TableName,
    // generowanie szablonu Code) i kompilację GetGuidedRows() weryfikuje się tylko przez UI/silnik,
    // poza zakresem testu jednostkowego publicznego kontraktu.

    // ============================== WORKFLOW-I3 — Automatyczny start procesu (★) ==============================

    [Test]
    [Description("WORKFLOW-I3: automatyczny start = węzeł startowy IsStart=true + ActionRunAt.Auto na " +
                 "definicji WDROŻONEJ (IsDeployed=true); wzorzec 'Start' ustawia tę parę. Po zapisie " +
                 "konfiguracja jest utrwalona. GRANICA: proces startuje dopiero w session.Save() obiektu " +
                 "źródłowego (silnik triggerów) — w teście nie ma silnika, więc startu nie testujemy.")]
    public void WORKFLOW_I3_AutomatycznyStart_KonfiguracjaWezlaStartowego_IUtrwalenie()
    {
        Guid guidDef = Guid.Empty;
        Guid guidStart = Guid.Empty;

        InConfigTransaction(() =>
        {
            var definicja = NowaDefinicja("I3");

            var start = NowyWezel(definicja, "Start");
            start.FormatedName = "Uzupełnij dane";

            // Wzorzec 'Start' ustawia parę odpowiadającą za AUTOMATYCZNY start:
            start.IsStart.Should().BeTrue("wzorzec 'Start' ustawia element startowy");
            start.ActionRunAt.Should().Be(ActionRunAt.Auto,
                "para IsStart + ActionRunAt.Auto = automatyczny start (silnik, na zapis obiektu)");

            // Tylko WDROŻONA definicja startuje automatycznie (WORKFLOW-A7):
            definicja.IsDeployed = true;

            guidDef = definicja.Guid;
            guidStart = start.Guid;
            UsunAutoTriggeryWorkaround();
        });
        SaveDisposeConfig();

        // Konfiguracja startu automatycznego jest utrwalona:
        var def2 = GetConfig<WFDefinition>(guidDef);
        def2.IsDeployed.Should().BeTrue("wdrożona definicja — warunek konieczny startu automatycznego");

        var start2 = GetConfig<TaskDefinition>(guidStart);
        start2.IsStart.Should().BeTrue();
        start2.ActionRunAt.Should().Be(ActionRunAt.Auto,
            "automatyczny start utrwalony jako IsStart + ActionRunAt.Auto");

        // GRANICA TESTOWALNOŚCI: faktyczny start (powstanie WFWorkflow + Task) następuje dopiero
        // w session.Save() sesji OPERACYJNEJ zapisującej obiekt z tabeli triggera. W teście
        // jednostkowym nie ma uruchomionego silnika triggerów — scenariusza end-to-end nie testujemy.
    }

    [Test]
    [Description("WORKFLOW-I3 (pułapka): StartPointType jest zapisywalny WYŁĄCZNIE w definicjach Engine — " +
                 "w definicji Standard pole jest read-only (IsReadOnlyStartPointType()==true), start " +
                 "konfiguruje się parą IsStart + ActionRunAt.")]
    public void WORKFLOW_I3_StartPointType_WStandardJestReadOnly()
    {
        InConfigTransaction(() =>
        {
            var definicja = NowaDefinicja("I3S");
            definicja.DefinitionType.Should().Be(DefinitionTypeEnum.Standard,
                "WFDefinition tworzy definicję Standard");

            var start = NowyWezel(definicja, "Start");
            start.FormatedName = "Start standardowy";

            // PUŁAPKA: w definicji Standard StartPointType jest tylko do odczytu (Engine-only):
            start.IsReadOnlyStartPointType().Should().BeTrue(
                "w definicji Standard StartPointType jest read-only — start ustawia para IsStart + ActionRunAt");

            UsunAutoTriggeryWorkaround();
        });
        // Bez zapisu — scenariusz poglądowy; rollback sprząta.
    }

    // ============================== WORKFLOW-I4 — Automatyczna tranzycja ==============================

    [Test]
    [Description("WORKFLOW-I4: tranzycja automatyczna = WFTransition z IsUserDecision=false + warunek " +
                 "realizacji; kolejność: NAJPIERW Source/Target, POTEM warunki. Po zapisie konfiguracja " +
                 "utrwalona; tranzycję węzła źródłowego odnajdujemy w Source.SourceWFTransitions. " +
                 "GRANICA: wykonanie tranzycji wymaga przeliczenia zadania (silnik) — nie testujemy go.")]
    public void WORKFLOW_I4_TranzycjaAutomatyczna_IsUserDecisionFalse_IUtrwalenie()
    {
        Guid guidTranzycja = Guid.Empty;
        Guid guidStart = Guid.Empty;

        InConfigTransaction(() =>
        {
            var definicja = NowaDefinicja("I4");

            var start = NowyWezel(definicja, "Start");
            start.FormatedName = "Weryfikacja danych";

            var koniec = NowyWezel(definicja, "Koniec");
            koniec.FormatedName = "Zakończenie";

            // Tranzycja automatyczna: kolejność — NAJPIERW Source/Target (z nich wynika definicja i typ
            // obiektu dla warunków), POTEM warunki:
            var tranzycja = AddConfig(new WFTransition());
            tranzycja.Source = start;
            tranzycja.Target = koniec;
            tranzycja.Name = "Dane kompletne";
            tranzycja.IsUserDecision = false;                 // bez decyzji operatora = tranzycja automatyczna

            tranzycja.IsUserDecision.Should().BeFalse(
                "IsUserDecision=false czyni tranzycję automatyczną (nie czeka na decyzję)");
            ((TaskDefinition)tranzycja.Source).Guid.Should().Be(start.Guid);
            ((TaskDefinition)tranzycja.Target).Guid.Should().Be(koniec.Guid);

            guidTranzycja = tranzycja.Guid;
            guidStart = start.Guid;
            UsunAutoTriggeryWorkaround();
        });
        SaveDisposeConfig();

        // Konfiguracja tranzycji automatycznej jest utrwalona:
        var start2 = GetConfig<TaskDefinition>(guidStart);
        var wyjsciowe = start2.SourceWFTransitions.Cast<WFTransition>().ToList();
        wyjsciowe.Should().ContainSingle("węzeł źródłowy ma jedną tranzycję wyjściową");
        var tranzycja2 = wyjsciowe[0];
        tranzycja2.Guid.Should().Be(guidTranzycja);
        tranzycja2.IsUserDecision.Should().BeFalse("automatyczna tranzycja utrwalona (IsUserDecision=false)");

        // GRANICA TESTOWALNOŚCI: tranzycja automatyczna wykona się dopiero przy PRZELICZENIU zadania
        // (zapis obiektu obserwowanego przez trigger / inna decyzja) — nie ma publicznego
        // Task.Recalculate(); w teście jednostkowym brak silnika, więc wykonania nie testujemy.
    }

    // POMINIĘTE: IsRealizedExpressionParent/Task/Transition (warunki RowCondition generujące IsRealized) —
    // getter nie robi round-tripu 1:1 (kod osadzany jest w Statement/blob algorytmu, normalizowany po
    // reloadzie), więc asercja na trwałą wartość jest krucha (analogia do GetParentExpression z RozdzialB).
    // Pewny, trwały fakt automatyzacji tranzycji testujemy przez IsUserDecision powyżej.

    // ============================== WORKFLOW-I5 — Harmonogram (ScheduleDefinition) ==============================

    [Test]
    [Description("WORKFLOW-I5: harmonogram = Soneta.Core.Schedule.ScheduleDefinition (tabela ScheduleDefs, " +
                 "moduł Core, NIE Workflow) wiązany z węzłem przez KONSTRUKTOR new ScheduleDefinition(host) — " +
                 "host=węzeł ustawia pole Host (IScheduleAutoJob) ORAZ kolumnę TaskDefinition; cykl ustawia subrow " +
                 "CycleDefinition (Typ/Czas) — subrow edytujemy w miejscu, nie podmieniamy. Po zapisie odczyt " +
                 "kluczem Core.ScheduleDefs.WgTaskDefinition i flagą węzła HasScheduleAutoJob (czyta po polu Host). " +
                 "GRANICA: wykonanie cyklu wymaga usługi HZ na serwerze — nie testujemy odpalenia.")]
    public void WORKFLOW_I5_Harmonogram_KonfiguracjaCyklu_IUtrwalenie()
    {
        Guid guidStart = Guid.Empty;
        Guid guidHarm = Guid.Empty;

        InConfigTransaction(() =>
        {
            var definicja = NowaDefinicja("I5");
            var start = NowyWezel(definicja, "Start");
            start.FormatedName = "Przegląd kontrahentów";
            definicja.IsDeployed = true;

            // ScheduleDefinition należy do modułu Soneta.Core (ScheduleDefs), nie do WorkflowModule.
            // KLUCZOWE: hosta przekazujemy KONSTRUKTOREM new ScheduleDefinition(host) — to ustawia pole Host
            // (iface-ref IScheduleAutoJob) ORAZ kolumnę TaskDefinition. Sam setter TaskDefinition NIE ustawia
            // Host, a flaga TaskDefinition.HasScheduleAutoJob czyta harmonogramy kluczem WgHost (po polu Host),
            // więc bez konstruktora z hostem byłaby False mimo poprawnie ustawionej kolumny TaskDefinition.
            var harmonogram = AddConfig(new ScheduleDefinition(start));
            harmonogram.Name = "Codzienny przegląd";
            harmonogram.ScheduleType = ScheduleTypeEnum.UseCycleDefinition;
            harmonogram.DefaultAutoAction = true;                     // użyj akcji domyślnej węzła (start procesu)

            // Data pierwszego wywołania NIE może być z przeszłości (weryfikator) — ustawiamy w przyszłości:
            harmonogram.StartDate = Date.Today.AddDays(1);

            // CycleDefinition to SUBROW — ustawiamy jego pola w miejscu, nie podmieniamy całego obiektu:
            harmonogram.CycleDefinition.Typ = DefinicjaCykluTyp.Dzienny;
            harmonogram.CycleDefinition.Czas = new Time(6, 0);

            guidStart = start.Guid;
            guidHarm = harmonogram.Guid;
            UsunAutoTriggeryWorkaround();
        });
        SaveDisposeConfig();

        // Konfiguracja harmonogramu jest utrwalona — odczyt z modułu CORE (nie Workflow):
        var harm2 = ConfigEditSession.GetCore().ScheduleDefs.Cast<ScheduleDefinition>()
            .Single(s => s.Guid == guidHarm);
        harm2.Name.Should().Be("Codzienny przegląd");
        harm2.ScheduleType.Should().Be(ScheduleTypeEnum.UseCycleDefinition);
        harm2.DefaultAutoAction.Should().BeTrue();
        harm2.CycleDefinition.Typ.Should().Be(DefinicjaCykluTyp.Dzienny, "typ cyklu utrwalony jako Dzienny");
        ((TaskDefinition)harm2.TaskDefinition).Guid.Should().Be(guidStart,
            "harmonogram wiąże się z węzłem polem TaskDefinition");

        // Klucz Core.ScheduleDefs.WgTaskDefinition — harmonogramy węzła:
        var start2 = GetConfig<TaskDefinition>(guidStart);
        var poKluczu = ConfigEditSession.GetCore().ScheduleDefs.WgTaskDefinition[start2]
            .Cast<ScheduleDefinition>().ToList();
        poKluczu.Should().ContainSingle("klucz WgTaskDefinition odnajduje harmonogram węzła");
        poKluczu[0].Guid.Should().Be(guidHarm);

        // Property węzła sygnalizuje istnienie harmonogramu:
        start2.HasScheduleAutoJob.Should().BeTrue("HasScheduleAutoJob == true gdy węzeł ma harmonogram");

        // GRANICA TESTOWALNOŚCI: wystąpienie cyklu wykonuje USŁUGA HZ na serwerze (AutoAction) —
        // sama definicja to dane konfiguracyjne (zapis przejdzie), ale bez HZ nic nie wystartuje;
        // odpalenia cyklu w teście jednostkowym NIE testujemy.
    }
}
