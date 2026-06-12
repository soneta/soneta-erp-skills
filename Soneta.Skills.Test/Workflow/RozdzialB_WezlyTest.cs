using System;
using System.Linq;
using AwesomeAssertions;
using NUnit.Framework;
using Soneta.Business;
using Soneta.Business.Db;
using Soneta.Workflow;
using Soneta.Workflow.Config;
using Soneta.Workflow.Workers;

namespace Soneta.Skills.Test.Workflow;

/// <summary>
/// Rozdział B — „Węzły procesu (definicje zadań)" (receptury WORKFLOW-B1 … WORKFLOW-B7).
/// <para>
/// Testy są <b>wykonywalną dokumentacją</b> publicznego kontraktu platformy Soneta dla domeny
/// Workflow. Każda metoda mapuje się 1:1 do receptury z dokumentu skilla
/// <c>workflow/WORKFLOW02-wezly.md</c>. Kluczowy model: węzeł procesu to wiersz
/// <c>Soneta.Business.Db.TaskDefinition</c> (tabela <c>TaskDefs</c>, <b>konfiguracyjna</b>,
/// moduł Business), przypięty do definicji procesu polem <c>WFDefinition</c>, a jego rodzaj
/// (start / zadanie / koniec / powiadomienie…) określa wzorzec elementu <c>WFDefItem</c>
/// (<c>Soneta.Workflow.Config.WFDefItem</c>, tabela <c>WFDefItems</c>).
/// </para>
/// <para>
/// Baza Demo nie musi zawierać żadnych definicji procesów — wszystkie scenariusze budują własną
/// definicję od zera (WORKFLOW-A1) w sesji konfiguracyjnej (<c>InConfigTransaction</c> +
/// <c>AddConfig</c>/<c>GetConfig</c> + <c>SaveDisposeConfig</c>); rollback po teście sprząta.
/// Operujemy wyłącznie na publicznym kontrakcie.
/// </para>
/// </summary>
[TestFixture]
public class RozdzialB_WezlyTest : WorkflowTestBase
{
    /// <summary>Moduł Workflow bieżącej sesji KONFIGURACYJNEJ (wzorce WFDefItems, odbiorcy WFRecipients).</summary>
    private WorkflowModule ConfigWorkflow => ConfigEditSession.GetWorkflow();

    /// <summary>Moduł Business bieżącej sesji KONFIGURACYJNEJ (tabela TaskDefs — węzły/definicje zadań).</summary>
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
    /// Nowy węzeł wg receptury WORKFLOW-B1: konstruktor z NAZWĄ TABELI obiektu nadrzędnego,
    /// potem NAJPIERW <c>WFDefinition</c>, a dopiero POTEM wzorzec <c>WFDefItem</c>
    /// (setter wzorca nadpisuje Name/FormatedName/IsStart). Wołać WEWNĄTRZ transakcji konfiguracyjnej.
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
    /// JAKIEGOKOLWIEK wiersza <c>TaskTrigger</c> (także domyślnego, dokładanego automatycznie
    /// w <c>TaskDefinition.OnAdded</c>) kończy się błędem SQL „Cannot insert the value NULL into
    /// column 'TaskTriggerGuid'" — klasa <c>TaskTrigger</c> nie wypełnia tej kolumny NOT NULL.
    /// Przed <c>SaveDisposeConfig()</c> usuwamy więc świeżo dodane triggery; konfiguracja węzłów
    /// (TaskDefs) zapisuje się bez przeszkód. Wołać NA KOŃCU transakcji konfiguracyjnej.
    /// </summary>
    private void UsunAutoTriggeryWorkaround()
    {
        foreach (TaskTrigger trigger in ConfigBusiness.TaskTriggers.Cast<TaskTrigger>()
                     .Where(t => t.State == RowState.Added).ToList())
            trigger.Delete();
    }

    // ============================== WORKFLOW-B1 — Utworzenie węzła w procesie (★) ==============================

    [Test]
    [Description("WORKFLOW-B1: węzeł = TaskDefinition(tablename) + WFDefinition + wzorzec WFDefItem " +
                 "(Start/Zadanie/Koniec); wzorzec ustawia IsStart i typ końca; po zapisie węzły " +
                 "odnajdujemy kluczami TaskDefs.WgWFDefinition i TaskDefs.ByName[tablename, name].")]
    public void WORKFLOW_B1_WezlyStartZadanieKoniec_TworzoneIWyszukiwaneKluczami()
    {
        Guid guidDef = Guid.Empty;
        string nazwaStartu = null;

        InConfigTransaction(() =>
        {
            var definicja = NowaDefinicja("B1");

            // Węzeł STARTOWY — wzorzec "Start" sam ustawia IsStart=true:
            var start = NowyWezel(definicja, "Start");
            start.FormatedName = "Rejestracja wniosku";      // 3) własna etykieta NA KOŃCU
            start.IsStart.Should().BeTrue("wzorzec 'Start' ustawia IsStart=true");
            start.TableName.Should().Be("Kontrahenci", "tabelę obiektu nadrzędnego ustawił konstruktor");

            // Węzeł POŚREDNI (zadanie operatora) — kończony tranzycjami (EndType=WFTransitions):
            var zadanie = NowyWezel(definicja, "Zadanie");
            zadanie.FormatedName = "Akceptacja przełożonego";
            zadanie.IsStart.Should().BeFalse("wzorzec 'Zadanie' nie jest startem");
            zadanie.IsEndTypeNone.Should().BeFalse("zadanie kończy się tranzycjami, nie 'niczym'");
            zadanie.IsEndTypeWorkflow.Should().BeFalse("zadanie nie wymusza końca procesu");

            // Węzeł KOŃCOWY — wymusza zakończenie całego procesu (EndType=Workflow):
            var koniec = NowyWezel(definicja, "Koniec");
            koniec.FormatedName = "Koniec";
            koniec.IsEndTypeWorkflow.Should().BeTrue("wzorzec 'Koniec' wymusza zakończenie procesu");

            // Setter WFDefItem nadał techniczne Name (unikalne w definicji) — zapamiętujemy do klucza:
            start.Name.Should().NotBeNullOrEmpty("setter WFDefItem sam nadaje unikalne Name");
            nazwaStartu = start.Name;

            // OnAdded dołożył automatycznie domyślny TaskTrigger na tabeli TableName (WORKFLOW09):
            start.TaskTriggers.Cast<TaskTrigger>().Should().NotBeEmpty(
                "OnAdded automatycznie dokłada domyślny TaskTrigger — nie tworzymy go ręcznie");

            guidDef = definicja.Guid;
            UsunAutoTriggeryWorkaround();
        });
        SaveDisposeConfig();

        // Odczyt po zapisie — świeża sesja konfiguracyjna:
        var def2 = GetConfig<WFDefinition>(guidDef);

        // Klucz WgWFDefinition — wszystkie węzły definicji (podzbiór):
        var wezly = ConfigBusiness.TaskDefs.WgWFDefinition[def2].Cast<TaskDefinition>().ToList();
        wezly.Should().HaveCount(3, "definicja ma start, zadanie i koniec");
        wezly.Select(w => w.FormatedName).Should().BeEquivalentTo(
            "Rejestracja wniosku", "Akceptacja przełożonego", "Koniec");

        // Kolekcja zwrotna WFDefinition.TaskDefs widzi te same węzły:
        def2.TaskDefs.Cast<TaskDefinition>().Should().HaveCount(3,
            "TaskDefs to kolekcja zwrotna pola WFDefinition");

        // Klucz ByName[tablename, name] — pojedynczy wiersz po parze (tabela, nazwa techniczna):
        TaskDefinition poNazwie = ConfigBusiness.TaskDefs.ByName["Kontrahenci", nazwaStartu];
        poNazwie.Should().NotBeNull("ByName[tablename, name] zwraca pojedynczy węzeł");
        poNazwie.FormatedName.Should().Be("Rejestracja wniosku");

        // Klucz WgWFDefItem — węzły wg wzorca elementu:
        var wzorzecKoniec = ConfigWorkflow.WFDefItems.ByName["Koniec"];
        ConfigBusiness.TaskDefs.WgWFDefItem[wzorzecKoniec].Cast<TaskDefinition>()
            .Should().Contain(w => w.FormatedName == "Koniec" && w.WFDefinition == (IWFDefinition)def2);
    }

    [Test]
    [Description("WORKFLOW-B1 (pułapki): kolejność ma znaczenie — setter WFDefItem NADPISUJE wcześniejsze " +
                 "FormatedName/IsStart wartościami wzorca; ustawienie WFDefinition przypisuje automatycznie " +
                 "pierwszą rolę procesową; węzeł-zadanie bez tranzycji wyjściowych ma ActiveTask=false.")]
    public void WORKFLOW_B1_Pulapki_KolejnoscSetterow_RolaDomyslna_ActiveTask()
    {
        InConfigTransaction(() =>
        {
            var definicja = NowaDefinicja("B1P");

            // PUŁAPKA 1: własna etykieta ustawiona PRZED wzorcem zostaje nadpisana przez setter WFDefItem.
            var wezel = AddConfig(new TaskDefinition("Kontrahenci"));
            wezel.WFDefinition = definicja;                  // najpierw definicja
            wezel.FormatedName = "Moja etykieta";            // za wcześnie!
            wezel.WFDefItem = ConfigWorkflow.WFDefItems.ByName["Zadanie"];
            wezel.FormatedName.Should().NotBe("Moja etykieta",
                "setter WFDefItem nadpisuje FormatedName wartością opisową wzorca („Definicja – Wzorzec\")");

            // PUŁAPKA 2 (mechanizm): setter WFDefinition przypisał automatycznie PIERWSZĄ rolę
            // procesową definicji (domyślna rola powstała razem z definicją — WORKFLOW-A1):
            var pierwszaRola = definicja.ProcessRoles.Cast<WFProcessRole>().First();
            wezel.ProcessRole.Should().BeSameAs(pierwszaRola,
                "setter WFDefinition przypisuje pierwszą rolę procesową definicji");

            // PUŁAPKA 3: węzeł-zadanie (EndType=WFTransitions) bez tranzycji wyjściowych
            // nie ma jak się zakończyć — proces by „utknął":
            wezel.ActiveTask.Should().BeFalse(
                "zadanie bez tranzycji wyjściowych i bez EndType None/Workflow nie ma jak się zakończyć");

            // Węzeł końcowy kończy proces sam z siebie — ActiveTask=true bez żadnej tranzycji:
            var koniec = NowyWezel(definicja, "Koniec");
            koniec.ActiveTask.Should().BeTrue("EndType=Workflow kończy proces — węzeł jest „aktywny\"");
        });
        // Celowo bez zapisu — pokazujemy zachowanie setterów; rollback sprząta.
    }

    // ============================== WORKFLOW-B2 — Węzeł startowy procesu (★) ==============================

    [Test]
    [Description("WORKFLOW-B2: o starcie decyduje para IsStart + ActionRunAt; wzorce startowe ustawiają " +
                 "IsStart=true i ActionRunAt=Auto (start automatyczny) — start MANUALNY wymaga jawnego " +
                 "ActionRunAt=InMenu; GetStartPoint() zwraca wyłącznie węzeł startu manualnego.")]
    public void WORKFLOW_B2_WezelStartowy_InMenu_GetStartPointZwracaWezel()
    {
        Guid guidDef = Guid.Empty;
        Guid guidStart = Guid.Empty;

        InConfigTransaction(() =>
        {
            var definicja = NowaDefinicja("B2");

            var start = NowyWezel(definicja, "Start");
            start.FormatedName = "Start procesu";

            // Wzorzec 'Start' ustawia element startowy, ale w trybie AUTOMATYCZNYM:
            start.IsStart.Should().BeTrue("wzorzec 'Start' ustawia element startowy");
            start.ActionRunAt.Should().Be(ActionRunAt.Auto,
                "wzorce startowe konfigurują start AUTOMATYCZNY (silnik, na zdarzenie zapisu)");

            // Start MANUALNY (czynność w menu dokumentu / panelu workflow) ustawiamy jawnie
            // (setter ActionRunAt jest zapisywalny w definicji Standard):
            start.ActionRunAt = ActionRunAt.InMenu;
            // PUŁAPKA: widoczność czynności startu (ShowInToolbar/ShowInListMenu) jest zapisywalna
            // WYŁĄCZNIE w definicji Engine + ActionRunAt==InMenu — w definicji Standard pole jest
            // read-only (IsReadOnlyShowInToolbar()==true), a próba zapisu rzuca ColReadOnlyException:
            start.IsReadOnlyShowInToolbar().Should().BeTrue(
                "w definicji Standard ShowInToolbar jest tylko do odczytu (Engine-only)");

            guidDef = definicja.Guid;
            guidStart = start.Guid;
            UsunAutoTriggeryWorkaround();
        });
        SaveDisposeConfig();

        // GetStartPoint() zwraca pierwszy węzeł: !Locked && IsStart && ActionRunAt==InMenu:
        var def2 = GetConfig<WFDefinition>(guidDef);
        TaskDefinition punktStartu = def2.GetStartPoint();
        punktStartu.Should().NotBeNull("definicja ma węzeł startu manualnego");
        punktStartu.Guid.Should().Be(guidStart, "GetStartPoint() wskazuje nasz węzeł startowy");
        (punktStartu.IsStart && punktStartu.ActionRunAt == ActionRunAt.InMenu)
            .Should().BeTrue("punkt startu manualnego spełnia parę warunków IsStart + InMenu");
    }

    [Test]
    [Description("WORKFLOW-B2 (pułapki): GetStartPoint() nie „widzi\" startu automatycznego (Auto) — rzuca " +
                 "NotSupportedException; dopiero przełączenie ActionRunAt na InMenu czyni węzeł punktem " +
                 "startu manualnego. Widoczność czynności (ShowInToolbar/ShowInListMenu) jest Engine-only.")]
    public void WORKFLOW_B2_StartAutomatyczny_NieJestPunktemStartuManualnego()
    {
        InConfigTransaction(() =>
        {
            var definicja = NowaDefinicja("B2A");

            var start = NowyWezel(definicja, "Start");
            start.FormatedName = "Start automatyczny";

            // Wzorzec startowy daje tryb AUTOMATYCZNY (IsStart=true, ActionRunAt=Auto) —
            // taki węzeł uruchamia proces przy zapisie wiersza tabeli TableName (TaskTrigger),
            // ale tylko z definicji WDROŻONEJ (WORKFLOW-A7).
            start.ActionRunAt.Should().Be(ActionRunAt.Auto);

            // PUŁAPKA: GetStartPoint() zwraca wyłącznie węzeł startu MANUALNEGO (InMenu) —
            // dla samego startu automatycznego rzuca NotSupportedException:
            System.Action punkt = () => definicja.GetStartPoint();
            punkt.Should().Throw<NotSupportedException>(
                "węzeł startu automatycznego nie jest „widziany\" przez GetStartPoint()");

            // Przełączamy na start manualny — węzeł staje się punktem startu manualnego:
            start.ActionRunAt = ActionRunAt.InMenu;
            definicja.GetStartPoint().Should().BeSameAs(start,
                "po przełączeniu na InMenu węzeł stał się punktem startu manualnego");

            // PUŁAPKA: w definicji Standard widoczność czynności startu jest tylko do odczytu
            // (ShowInToolbar/ShowInListMenu zapisywalne wyłącznie w definicji Engine + InMenu):
            start.IsReadOnlyShowInToolbar().Should().BeTrue("Standard: ShowInToolbar read-only");
            start.IsReadOnlyShowInListMenu().Should().BeTrue("Standard: ShowInListMenu read-only");
        });
        // Bez zapisu — scenariusz poglądowy; rollback sprząta.
    }

    // ============================== WORKFLOW-B3 — Wykonawca węzła (★) ==============================

    [Test]
    [Description("WORKFLOW-B3: wykonawcę wyznacza rola procesowa węzła (domyślnie pierwsza rola definicji); " +
                 "przy roli typu TaskExecutor działają pola węzła: OperatorType=Manual + Operator wskazuje " +
                 "konkretnego operatora.")]
    public void WORKFLOW_B3_Wykonawca_RolaProcesowa_IOperatorManual()
    {
        Guid guidDef = Guid.Empty;
        Guid guidWezel = Guid.Empty;

        InConfigTransaction(() =>
        {
            var definicja = NowaDefinicja("B3");

            // Dodatkowa rola procesowa (tor) — WORKFLOW-A3:
            var rola = AddConfig(new WFProcessRole(definicja));
            rola.Name = "Przełożony";

            var wezel = NowyWezel(definicja, "Zadanie");
            wezel.FormatedName = "Akceptacja przełożonego";

            // 1) Wykonawca przez ROLĘ PROCESOWĄ — przypinamy węzeł do toru "Przełożony":
            wezel.ProcessRole = definicja.ProcessRoles.Cast<WFProcessRole>()
                .First(r => r.Name == "Przełożony");

            // 2) Wykonawca WSKAZANY NA WĘŹLE (działa przy roli typu TaskExecutor —
            //    „Wskazany na zadaniu"): OperatorType PRZED Operator (pułapka niżej):
            var rejestracja = NowyWezel(definicja, "Zadanie");
            rejestracja.FormatedName = "Rejestracja wniosku";
            rejestracja.OperatorType = TaskOperatorType.Manual;
            // Operator musi pochodzić z TEJ SAMEJ sesji (konfiguracyjnej) — GetConfig przepina wiersz:
            rejestracja.Operator = GetConfig(Login.Operator);

            guidDef = definicja.Guid;
            guidWezel = rejestracja.Guid;
            UsunAutoTriggeryWorkaround();
        });
        SaveDisposeConfig();

        // Weryfikacja po zapisie:
        var def2 = GetConfig<WFDefinition>(guidDef);
        var akceptacja = ConfigBusiness.TaskDefs.WgWFDefinition[def2].Cast<TaskDefinition>()
            .First(td => td.FormatedName == "Akceptacja przełożonego");
        akceptacja.ProcessRole.Should().NotBeNull();
        ((WFProcessRole)akceptacja.ProcessRole).Name.Should().Be("Przełożony",
            "węzeł został przypięty do toru roli 'Przełożony'");

        var rejestracja2 = GetConfig<TaskDefinition>(guidWezel);
        rejestracja2.OperatorType.Should().Be(TaskOperatorType.Manual);
        rejestracja2.Operator.Should().NotBeNull("przy OperatorType=Manual wykonawcę wskazuje pole Operator");
        rejestracja2.Operator.Name.Should().Be(Login.Operator.Name);
    }

    [Test]
    [Description("WORKFLOW-B3 (pułapka): setter OperatorType CZYŚCI pola niepasujące do trybu — zmiana " +
                 "z Manual na Current zeruje Operator; RoleName na węźle rzuca ArgumentOutOfRangeException " +
                 "dla nieznanej nazwy roli (inaczej niż WFProcessRole.RoleName).")]
    public void WORKFLOW_B3_SetterOperatorType_CzysciPolaNiepasujace_RoleNameRzucaDlaNieznanej()
    {
        InConfigTransaction(() =>
        {
            var definicja = NowaDefinicja("B3P");
            var wezel = NowyWezel(definicja, "Zadanie");
            wezel.FormatedName = "Zadanie testowe";

            // Ustawiamy wykonawcę ręcznie wybranego (OperatorType PRZED Operator):
            wezel.OperatorType = TaskOperatorType.Manual;
            wezel.Operator = GetConfig(Login.Operator);
            wezel.Operator.Should().NotBeNull();

            // PUŁAPKA: zmiana trybu na Current („Aktualny") czyści pole Operator:
            wezel.OperatorType = TaskOperatorType.Current;
            wezel.Operator.Should().BeNull(
                "setter OperatorType czyści Operator poza trybem Manual");

            // PUŁAPKA: RoleName na TaskDefinition rzuca dla NIEZNANEJ nazwy roli operatorów
            // (inaczej niż WFProcessRole.RoleName, które po cichu zeruje RoleGuid):
            wezel.OperatorType = TaskOperatorType.Role;
            var nieistniejaca = "Rola_" + Guid.NewGuid().ToString("N");
            System.Action ustaw = () => wezel.RoleName = nieistniejaca;
            ustaw.Should().Throw<ArgumentOutOfRangeException>(
                "TaskDefinition.RoleName waliduje istnienie roli operatorów");
        });
        // Bez zapisu — pokazujemy zachowanie setterów; rollback sprząta.
    }

    [Test]
    [Description("WORKFLOW-B3 (multitask): MultiTaskType=ByGetTaskUsers + lista odbiorców WFRecipient " +
                 "(konstruktor wiąże hosta-węzeł, wiersz trzeba dodać do tabeli); odczyt WFRecipients.WgHost.")]
    public void WORKFLOW_B3_Multitask_OdbiorcyWFRecipient()
    {
        Guid guidWezel = Guid.Empty;

        InConfigTransaction(() =>
        {
            var definicja = NowaDefinicja("B3M");
            var wezel = NowyWezel(definicja, "Zadanie");
            wezel.FormatedName = "Zadanie dla wielu";

            // Węzeł WIELOZADANIOWY: jedno przejście tworzy zadania dla wielu wykonawców.
            wezel.MultiTaskType = MultiTaskType.ByGetTaskUsers;

            // Odbiorca multitask: konstruktor WFRecipient(host) wiąże tylko hosta —
            // wiersz TRZEBA dodać do tabeli (tu: AddConfig = AddRow w sesji konfiguracyjnej).
            // TaskUser to referencja interfejsowa (ITaskUser) — może wskazywać Operatora:
            var odbiorca = AddConfig(new WFRecipient(wezel) { TaskUser = GetConfig(Login.Operator) });
            odbiorca.Should().NotBeNull();

            guidWezel = wezel.Guid;
            UsunAutoTriggeryWorkaround();
        });
        SaveDisposeConfig();

        // Odczyt odbiorców węzła po zapisie — klucz WgHost:
        var wezel2 = GetConfig<TaskDefinition>(guidWezel);
        wezel2.MultiTaskType.Should().Be(MultiTaskType.ByGetTaskUsers);
        var odbiorcy = ConfigWorkflow.WFRecipients.WgHost[wezel2].Cast<WFRecipient>().ToList();
        odbiorcy.Should().ContainSingle("dodaliśmy jednego odbiorcę multitask");
        odbiorcy[0].TaskUser.Should().NotBeNull("odbiorcą jest operator (ITaskUser)");
    }

    // ============================== WORKFLOW-B4 — Źródło danych węzła ==============================

    [Test]
    [Description("WORKFLOW-B4: tabelę obiektu nadrzędnego ustawia konstruktor (TableName readonly); " +
                 "wybór wiersza zadania można nadpisać wyrażeniem GetParentExpression (osadza je w " +
                 "GetParentCode); diagnostyka źródła przez ObjTable / ParentType.")]
    public void WORKFLOW_B4_ZrodloDanychWezla_GetParentExpression_IObjTable()
    {
        Guid guidWezel = Guid.Empty;

        InConfigTransaction(() =>
        {
            var definicja = NowaDefinicja("B4");
            var wezel = NowyWezel(definicja, "Zadanie");
            wezel.FormatedName = "Rejestracja wniosku";

            // Wiersz zadania można nadpisać wyrażeniem od obiektu źródłowego. Setter
            // GetParentExpression osadza wyrażenie w pełnej metodzie GetParent(...) (GetParentCode).
            // PUŁAPKA: getter NIE robi round-tripu 1:1 — GetReturnExpression() wyciąga tekst między
            // 'return' a ';', więc zwraca wyrażenie z prefiksem rzutowania (np.
            // "(Soneta.Business.GuidedRow)Row"), a nie surowe "Row"; po zapisie/reloadzie blob
            // algorytmu bywa normalizowany. Trwały, deterministyczny fakt: wyrażenie ląduje w kodzie.
            wezel.GetParentExpression = "Row";
            wezel.GetParentCode.Should().Contain("Row",
                "setter GetParentExpression osadza wyrażenie w metodzie GetParent (GetParentCode)");

            guidWezel = wezel.Guid;
            UsunAutoTriggeryWorkaround();
        });
        SaveDisposeConfig();

        var wezel2 = GetConfig<TaskDefinition>(guidWezel);

        // Diagnostyka źródła: tabela i typ wiersza nadrzędnego wynikają z TableName konstruktora:
        wezel2.TableName.Should().Be("Kontrahenci");
        Table tabela = wezel2.ObjTable;
        tabela.Should().NotBeNull("ObjTable zwraca tabelę obiektu nadrzędnego");
        // ObjTable.Name to wewnętrzna nazwa tabeli (rdzeń "Kontrahen..."), niekoniecznie 1:1 z
        // TableName z konstruktora — pewny sprawdzian tożsamości daje ParentType poniżej:
        tabela.Name.Should().Contain("Kontrahen");
        Type typWiersza = wezel2.ParentType;
        typWiersza.Should().NotBeNull("ParentType zwraca typ wiersza nadrzędnego (np. Soneta.CRM.Kontrahent)");
        typWiersza.Name.Should().Be("Kontrahent");
    }

    // ============================== WORKFLOW-B6 — Kopiowanie definicji zadania (★) ==============================

    [Test]
    [Description("WORKFLOW-B6: CopyTaskDefinitionWorker.CopyTaskDefinition() kopiuje pojedynczy węzeł " +
                 "z nowym Guid i unikalną parą (TableName, Name) w TEJ SAMEJ definicji procesu; " +
                 "worker sam zarządza transakcją — wystarczy Save sesji.")]
    public void WORKFLOW_B6_KopiowanieWezla_TworzyKopieWTejSamejDefinicji()
    {
        // KROK 1: budujemy i zapisujemy definicję z węzłem źródłowym.
        Guid guidDef = Guid.Empty;
        Guid guidWezel = Guid.Empty;

        InConfigTransaction(() =>
        {
            var definicja = NowaDefinicja("B6");
            var wezel = NowyWezel(definicja, "Zadanie");
            wezel.FormatedName = "Akceptacja przełożonego";
            wezel.Description = "Węzeł do skopiowania";

            guidDef = definicja.Guid;
            guidWezel = wezel.Guid;
            UsunAutoTriggeryWorkaround();
        });
        SaveDisposeConfig();

        // KROK 2: kopiowanie workerem — czynność wymaga sesji konfiguracyjnej edycyjnej
        // i węzła z określoną tabelą (IsVisibleCopyTaskDefinition: ObjTable != null).
        var zrodlo = GetConfig<TaskDefinition>(guidWezel);
        CopyTaskDefinitionWorker.IsVisibleCopyTaskDefinition(zrodlo).Should().BeTrue(
            "węzeł z określoną tabelą (ObjTable != null) można kopiować tym workerem");

        var worker = new CopyTaskDefinitionWorker { Row = zrodlo };
        // CopyTaskDefinition() SAM otwiera transakcję i robi Commit — bez własnego Logout:
        TaskDefinition kopia = worker.CopyTaskDefinition();
        Guid guidKopii = kopia.Guid;

        // Kopię identyfikujemy po zwróconej referencji (Name mógł dostać dopisek unikalności):
        kopia.Guid.Should().NotBe(guidWezel, "kopia dostaje NOWY Guid");
        kopia.TableName.Should().Be("Kontrahenci", "kopia działa na tej samej tabeli");
        kopia.Name.Should().NotBe(zrodlo.Name, "unikalność pary (TableName, Name) wymusza inną nazwę");

        // KROK 3: kopia trafia do TEJ SAMEJ definicji procesu — własna etykieta w nowej transakcji:
        InConfigTransaction(() =>
        {
            GetConfig<TaskDefinition>(guidKopii).FormatedName = "Akceptacja dyrektora";
            UsunAutoTriggeryWorkaround();   // kopia węzła też dostała automatyczny TaskTrigger
        });
        SaveDisposeConfig();

        // KROK 4: weryfikacja po zapisie:
        var def2 = GetConfig<WFDefinition>(guidDef);
        var wezly = ConfigBusiness.TaskDefs.WgWFDefinition[def2].Cast<TaskDefinition>().ToList();
        wezly.Should().HaveCount(2, "definicja ma węzeł źródłowy i jego kopię");
        wezly.Select(w => w.FormatedName).Should().BeEquivalentTo(
            "Akceptacja przełożonego", "Akceptacja dyrektora");

        var kopia2 = GetConfig<TaskDefinition>(guidKopii);
        kopia2.Description.Should().Be("Węzeł do skopiowania",
            "kopiowanie przez serializację XML przenosi ustawienia węzła (np. opis)");
        // Tranzycje NIE są kopiowane (kopiowany jest pojedynczy węzeł) — kopia jest „luźna":
        kopia2.HasSourceWFTransitions.Should().BeFalse("tranzycje wychodzące źródła nie są kopiowane");
        kopia2.HasTargetWFTransitions.Should().BeFalse("tranzycje wchodzące źródła nie są kopiowane");
    }

    // ============================== WORKFLOW-B7 — Terminy i eskalacja ==============================

    [Test]
    [Description("WORKFLOW-B7: eskalację włącza OverdueHandling=true (NAJPIERW flaga — wcześniej pola są " +
                 "readonly); węzeł obsługi eskalacji musi działać na tabeli Tasks (Parent = przeterminowane " +
                 "zadanie); OverdueTimeExpression buduje algorytm terminu eskalacji.")]
    public void WORKFLOW_B7_Eskalacja_KonfiguracjaWezlaObslugiNaTabeliTasks()
    {
        Guid guidWezel = Guid.Empty;
        Guid guidEskalacja = Guid.Empty;

        InConfigTransaction(() =>
        {
            var definicja = NowaDefinicja("B7");

            // Węzeł pilnowany (zwykłe zadanie operatora):
            var wezel = NowyWezel(definicja, "Zadanie");
            wezel.FormatedName = "Akceptacja przełożonego";

            // Węzeł OBSŁUGI ESKALACJI — MUSI działać na tabeli Tasks
            // (Parent przyszłego zadania eskalacyjnego = przeterminowane zadanie):
            var eskalacja = NowyWezel(definicja, "Powiadomienie", tabela: "Tasks");
            eskalacja.FormatedName = "Eskalacja — zadanie przeterminowane";

            // Kolejność pól eskalacji: NAJPIERW flaga (odblokowuje pola), POTEM wskazanie i wyrażenie:
            wezel.OverdueHandling = true;
            wezel.OverdueServiceType = eskalacja;                      // nie może wskazywać samego siebie
            wezel.OverdueTimeExpression = "DateTime.Now.AddDays(3)";   // eskalacja po 3 dniach
            // (puste wyrażenie ⇒ GetOverdueTime()=DateTime.MaxValue — eskalacja nigdy nie nastąpi)

            guidWezel = wezel.Guid;
            guidEskalacja = eskalacja.Guid;
            UsunAutoTriggeryWorkaround();
        });
        SaveDisposeConfig();

        // Konfiguracja eskalacji jest utrwalona (samo „wystrzelenie" wymaga harmonogramu zadań —
        // testujemy konfigurację, nie upływ czasu):
        var wezel2 = GetConfig<TaskDefinition>(guidWezel);
        wezel2.OverdueHandling.Should().BeTrue();
        wezel2.OverdueServiceType.Should().NotBeNull();
        wezel2.OverdueServiceType.Guid.Should().Be(guidEskalacja);
        wezel2.OverdueServiceType.TableName.Should().Be("Tasks",
            "węzeł obsługi eskalacji działa na tabeli Tasks");
        wezel2.OverdueTimeExpression.Should().Be("DateTime.Now.AddDays(3)");
    }
}
