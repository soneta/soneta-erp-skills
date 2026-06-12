using System;
using System.Linq;
using AwesomeAssertions;
using NUnit.Framework;
using Soneta.Business;
using Soneta.Business.Db;
using Soneta.Workflow;
using Soneta.Workflow.Config;

namespace Soneta.Skills.Test.Workflow;

/// <summary>
/// Rozdział C — 'Tranzycje i przepływ procesu' (receptury WORKFLOW-C1 … WORKFLOW-C7).
/// <para>
/// Testy są <b>wykonywalną dokumentacją</b> publicznego kontraktu platformy Soneta dla domeny
/// Workflow. Każda metoda mapuje się 1:1 do receptury z dokumentu skilla
/// <c>workflow/WORKFLOW03-tranzycje.md</c>. Kluczowy model: tranzycja
/// <c>Soneta.Workflow.Config.WFTransition</c> (tabela <c>WFTransitions</c>, <b>konfiguracyjna</b>)
/// łączy dwa węzły (<c>Source</c>/<c>Target: TaskDefinition</c>) w obrębie jednej definicji
/// procesu; pole <c>WfDefinition</c> ustawia się <b>automatycznie</b> z <c>Source</c>/<c>Target</c>.
/// </para>
/// <para>
/// Baza Demo nie musi zawierać żadnych definicji procesów — wszystkie scenariusze budują własną
/// definicję od zera (WORKFLOW-A1) wraz z węzłami (WORKFLOW-B1) w sesji konfiguracyjnej
/// (<c>InConfigTransaction</c> + <c>AddConfig</c>/<c>GetConfig</c> + <c>SaveDisposeConfig</c>);
/// rollback po teście sprząta. Operujemy wyłącznie na publicznym kontrakcie.
/// </para>
/// </summary>
[TestFixture]
public class RozdzialC_TranzycjeTest : WorkflowTestBase
{
    /// <summary>Moduł Workflow bieżącej sesji KONFIGURACYJNEJ (WFDefs, WFTransitions, WFTransitionDefs, OGSchemas).</summary>
    private WorkflowModule ConfigWorkflow => ConfigEditSession.GetWorkflow();

    /// <summary>Moduł Business bieżącej sesji KONFIGURACYJNEJ (tabela TaskDefs — węzły/definicje zadań).</summary>
    private BusinessModule ConfigBusiness => ConfigEditSession.GetBusiness();

    /// <summary>Stały Guid standardowego wzorca tranzycji 'Wybór operatora' (Standard) — WORKFLOW-C6.</summary>
    private static readonly Guid WyborOperatoraGuid = new Guid("00000000-0016-0001-0002-000000000000");

    /// <summary>Stały Guid standardowego wzorca tranzycji 'Bezwarunkowe przejście' (Standard) — WORKFLOW-C6.</summary>
    private static readonly Guid BezwarunkowePrzejscieGuid = new Guid("00000000-0016-0001-0001-000000000000");

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
    /// potem NAJPIERW <c>WFDefinition</c>, a dopiero POTEM wzorzec <c>WFDefItem</c>.
    /// Wołać WEWNĄTRZ transakcji konfiguracyjnej.
    /// </summary>
    private TaskDefinition NowyWezel(WFDefinition definicja, string wzorzec, string tabela = "Kontrahenci")
    {
        var wezel = AddConfig(new TaskDefinition(tabela));
        wezel.WFDefinition = definicja;
        wezel.WFDefItem = ConfigWorkflow.WFDefItems.ByName[wzorzec];
        return wezel;
    }

    /// <summary>
    /// WORKAROUND środowiska testowego (jak w RozdzialB, nie część receptury): w bieżącej bazie
    /// testowej zapis JAKIEGOKOLWIEK wiersza <c>TaskTrigger</c> (także domyślnego, dokładanego
    /// w <c>TaskDefinition.OnAdded</c>) kończy się błędem SQL 'Cannot insert the value NULL into
    /// column 'TaskTriggerGuid''. Przed <c>SaveDisposeConfig()</c> usuwamy świeżo dodane triggery.
    /// Wołać NA KOŃCU transakcji konfiguracyjnej.
    /// </summary>
    private void UsunAutoTriggeryWorkaround()
    {
        foreach (TaskTrigger trigger in ConfigBusiness.TaskTriggers.Cast<TaskTrigger>()
                     .Where(t => t.State == RowState.Added).ToList())
            trigger.Delete();
    }

    // ============================== WORKFLOW-C1 — Utworzenie tranzycji pomiędzy węzłami (★) ==============================

    [Test]
    [Description("WORKFLOW-C1: tranzycja = new WFTransition() + Source/Target (TaskDefinition), które " +
                 "AUTOMATYCZNIE ustawiają WfDefinition; wzorzec 'Wybór operatora\' (Guid) nadaje IsUserDecision; " +
                 "po zapisie tranzycje odnajdujemy kluczami WgSource/WgWfDefinition oraz kolekcją " +
                 "WFDefinition.Transitions.")]
    public void WORKFLOW_C1_NowaTranzycja_SourceTargetUstawiaWfDefinition_IZapisuje()
    {
        Guid guidDef = Guid.Empty;
        Guid guidAkceptuj = Guid.Empty;
        Guid guidOdrzuc = Guid.Empty;

        InConfigTransaction(() =>
        {
            var definicja = NowaDefinicja("C1");

            // Trzy węzły procesu (WORKFLOW-B1): zadanie źródłowe + dwa końce:
            var wniosek = NowyWezel(definicja, "Zadanie");
            wniosek.FormatedName = "Wniosek";
            var akceptacja = NowyWezel(definicja, "Koniec");
            akceptacja.FormatedName = "Zaakceptowany";
            var odrzucenie = NowyWezel(definicja, "Koniec");
            odrzucenie.FormatedName = "Odrzucony";

            // 1) Decyzja użytkownika — Source/Target PRZED Name i wzorcem (pułapka kolejności).
            //    Setter Source ustawia WfDefinition z definicji węzła:
            var akceptuj = AddConfig(new WFTransition());
            akceptuj.Source = wniosek;
            akceptuj.Target = akceptacja;
            ((WFDefinition)akceptuj.WfDefinition).Should().BeSameAs(definicja,
                "setter Source/Target ustawia WfDefinition automatycznie z definicji węzła");
            // Wzorzec 'Wybór operatora' (stały Guid) — nadpisuje flagi/kod, więc PRZED własnymi modyfikacjami:
            akceptuj.WFTransitionDefinition = ConfigWorkflow.WFTransitionDefs[WyborOperatoraGuid];
            akceptuj.Name = "Akceptuj";
            akceptuj.LP = 1;
            akceptuj.IsUserDecision.Should().BeTrue("wzorzec 'Wybór operatora\' ustawia IsUserDecision=true");

            // 2) Druga decyzja z tego samego węzła — flagi PO przypięciu wzorca:
            var odrzuc = AddConfig(new WFTransition());
            odrzuc.Source = wniosek;
            odrzuc.Target = odrzucenie;
            odrzuc.WFTransitionDefinition = ConfigWorkflow.WFTransitionDefs[WyborOperatoraGuid];
            odrzuc.Name = "Odrzuć";
            odrzuc.LP = 2;
            odrzuc.IsDefaultTransition = true;   // własna flaga PO wzorcu (wzorzec by ją nadpisał)

            guidDef = definicja.Guid;
            guidAkceptuj = akceptuj.Guid;
            guidOdrzuc = odrzuc.Guid;
            UsunAutoTriggeryWorkaround();
        });
        SaveDisposeConfig();

        // Odczyt po zapisie — świeża sesja konfiguracyjna:
        var def2 = GetConfig<WFDefinition>(guidDef);
        var wniosek2 = ConfigBusiness.TaskDefs.WgWFDefinition[def2].Cast<TaskDefinition>()
            .First(td => td.FormatedName == "Wniosek");

        // Klucz WgSource — tranzycje wychodzące z węzła:
        var wychodzace = ConfigWorkflow.WFTransitions.WgSource[wniosek2].Cast<WFTransition>().ToList();
        wychodzace.Should().HaveCount(2, "z węzła 'Wniosek\' wychodzą dwie tranzycje");
        wychodzace.Select(t => t.Name).Should().BeEquivalentTo("Akceptuj", "Odrzuć");

        // Klucz WgWfDefinition — wszystkie tranzycje definicji; kolekcja zwrotna WFDefinition.Transitions:
        ConfigWorkflow.WFTransitions.WgWfDefinition[def2].Cast<WFTransition>().Should().HaveCount(2);
        def2.Transitions.Cast<WFTransition>().Should().HaveCount(2,
            "Transitions to kolekcja zwrotna pola WfDefinition tranzycji");

        // Tranzycje utrwaliły konfigurację:
        var akceptuj2 = GetConfig<WFTransition>(guidAkceptuj);
        akceptuj2.IsUserDecision.Should().BeTrue("wzorzec 'Wybór operatora\' utrwalił IsUserDecision");
        akceptuj2.Target.FormatedName.Should().Be("Zaakceptowany");
        akceptuj2.LP.Should().Be(1);

        var odrzuc2 = GetConfig<WFTransition>(guidOdrzuc);
        odrzuc2.IsDefaultTransition.Should().BeTrue("flaga ustawiona po wzorcu nie została nadpisana");
        odrzuc2.LP.Should().Be(2);
    }

    [Test]
    [Description("WORKFLOW-C1 (pułapka): typ klasy tranzycji musi pasować do DefinitionType węzłów. " +
                 "WFTransition (Standard) ma DefinitionType=Standard; ustawienie Source węzła z definicji " +
                 "Standard jest spójne. Konstruktor klasy — nie setter — determinuje DefinitionType.")]
    public void WORKFLOW_C1_TypTranzycji_OdpowiadaTypowiDefinicji_Standard()
    {
        InConfigTransaction(() =>
        {
            var definicja = NowaDefinicja("C1T");
            definicja.DefinitionType.Should().Be(DefinitionTypeEnum.Standard,
                "WFDefinition (diagramowa) jest typu Standard");

            var wezelA = NowyWezel(definicja, "Zadanie");
            wezelA.FormatedName = "A";
            var wezelB = NowyWezel(definicja, "Koniec");
            wezelB.FormatedName = "B";

            // WFTransition (Standard) — DefinitionType determinuje konstruktor klasy, nie setter:
            var tranzycja = AddConfig(new WFTransition());
            tranzycja.DefinitionType.Should().Be(DefinitionTypeEnum.Standard,
                "konstruktor WFTransition ustawia tryb Standard");

            // Source z węzła definicji Standard jest spójny — setter nie rzuca ArgumentException:
            System.Action ustaw = () => { tranzycja.Source = wezelA; tranzycja.Target = wezelB; };
            ustaw.Should().NotThrow("Standard ↔ Standard: typ tranzycji pasuje do typu definicji");
            ((WFDefinition)tranzycja.WfDefinition).Should().BeSameAs(definicja);
        });
        // Bez zapisu — scenariusz pokazuje zgodność typów; rollback sprząta.
    }

    // ============================== WORKFLOW-C2 — Algorytmy Statement / Check / Action (★) ==============================

    [Test]
    [Description("WORKFLOW-C2: warunek realizacji i efekty tranzycji to kod C# w polach kalkulowanych — " +
                 "settery IsRealizedCode/IsReadOnlyCode/IsVisibleCode składają wspólny Statement (MemoText); " +
                 "CheckCode/ActionCode to algorytmy (subrow z polem .Code: MemoText). Trwały fakt: kod ląduje " +
                 "w odpowiednim blobie (unikamy kruchego round-tripu na znormalizowanym tekście).")]
    public void WORKFLOW_C2_AlgorytmyTranzycji_KodLadujeWStatementICheckActionCode()
    {
        Guid guidTranzycja = Guid.Empty;

        InConfigTransaction(() =>
        {
            var definicja = NowaDefinicja("C2");
            var wniosek = NowyWezel(definicja, "Zadanie");
            wniosek.FormatedName = "Wniosek";
            var koniec = NowyWezel(definicja, "Koniec");
            koniec.FormatedName = "Zatwierdzony";

            var tranzycja = AddConfig(new WFTransition());
            tranzycja.Source = wniosek;
            tranzycja.Target = koniec;
            tranzycja.Name = "Zatwierdź";

            // Wariant A: pełna metoda IsRealized (setter oczekuje KOMPLETNEJ metody z sygnaturą).
            // Setter składa kod do wspólnego Statement (MemoText):
            tranzycja.IsRealizedCode =
                "public override bool IsRealized(Soneta.Business.Db.Task task) {\n" +
                "\treturn task.WFTransition != null && task.WFTransition == Transition;\n" +
                "}";
            string statement = tranzycja.Statement;
            statement.Should().Contain("IsRealized",
                "setter IsRealizedCode osadza metodę w polu Statement (kod kalkulatora)");

            // Walidacja przed przejściem — handler w CheckCode.Code (MemoText subrow):
            tranzycja.CheckCode.Code =
                "public void CheckHandle(CheckHandleEventArgs args) {\n" +
                "\tif (Task.Description.IsEmpty)\n" +
                "\t\tthrow new Soneta.Business.RowException(Task, \"Uzupełnij opis zadania.\");\n" +
                "}";
            ((string)tranzycja.CheckCode.Code).Should().Contain("CheckHandle",
                "kod walidacji ląduje w CheckCode.Code");

            // Efekty uboczne po pozytywnym Check — handler w ActionCode.Code:
            tranzycja.ActionCode.Code =
                "public void ActionHandle(ActionHandleEventArgs args) {\n" +
                "\t// efekty uboczne po Check\n" +
                "}";
            ((string)tranzycja.ActionCode.Code).Should().Contain("ActionHandle",
                "kod efektu przejścia ląduje w ActionCode.Code");

            guidTranzycja = tranzycja.Guid;
            UsunAutoTriggeryWorkaround();
        });
        SaveDisposeConfig();

        // Po zapisie kod algorytmów jest utrwalony (sprawdzamy obecność handlerów, nie dokładny tekst —
        // gettery/kompilacja normalizują blob):
        var tr2 = GetConfig<WFTransition>(guidTranzycja);
        ((string)tr2.Statement).Should().Contain("IsRealized", "Statement utrwalił metodę IsRealized");
        ((string)tr2.CheckCode.Code).Should().Contain("CheckHandle", "CheckCode utrwalił handler Check");
        ((string)tr2.ActionCode.Code).Should().Contain("ActionHandle", "ActionCode utrwalił handler Action");
    }

    // POMINIĘTE WORKFLOW-C3: receptura nie jest oznaczona ★ (test dedykowany). Konfiguracja decyzji
    // (QuickAccess/Icon/ForeColor/IsDefaultTransition) to proste settery stringów/bool, a część
    // operacyjna (SetUserDecision/GoThru) wymaga aktywnego zadania uruchomionego procesu (silnik),
    // niedostępnego deklaratywnie na Demo.

    // POMINIĘTE WORKFLOW-C4: receptura nie jest oznaczona ★. Pełne wsparcie wielowariantowości dotyczy
    // definicji Engine, a enumeracja wariantów (GetVariants) jest internal; części operacyjnej nie da
    // się odtworzyć bez uruchomionego procesu (SetUserDecision z wariantem).

    // ============================== WORKFLOW-C5 — Schemat generatora obiektów (OGSchema) (★) ==============================

    [Test]
    [Description("WORKFLOW-C5: OGSchema (tabela OGSchemas, konfiguracyjna) mapuje obiekt źródłowy na docelowy; " +
                 "SourceType/TargetType to NAZWY TABEL (nie typy C#), z których wyliczają się Source/TargetDataType; " +
                 "schemat odnajdujemy kluczem OGSchemas.ByName i podpinamy pod tranzycję polem OGSchema.")]
    public void WORKFLOW_C5_OGSchema_SourceTargetTypeToNazwyTabel_IPodpiecieDoTranzycji()
    {
        Guid guidSchemat = Guid.Empty;
        Guid guidTranzycja = Guid.Empty;
        var nazwaSchematu = "Proces -> Zadanie " + Guid.NewGuid().ToString("N").Substring(0, 6);

        InConfigTransaction(() =>
        {
            // 1) Schemat generatora: proces (WFWorkflows) -> zadanie CRM (Zadania):
            var schemat = AddConfig(new OGSchema());
            schemat.Name = nazwaSchematu;
            schemat.SourceType = "WFWorkflows";   // NAZWA TABELI źródłowej (nie typ C#)
            schemat.TargetType = "Zadania";       // NAZWA TABELI docelowej
            schemat.CopyAttachments = true;
            schemat.XML =
                @"<?xml version=""1.0"" encoding=""UTF-8""?>" +
                @"<PropertyMappings xmlns=""http://www.enova.pl/schemas/OGPropertyMapping"">" +
                @"<PropertyMapping>" +
                @"<sourceproperty>Name</sourceproperty>" +
                @"<targetproperty>Nazwa</targetproperty>" +
                @"<isrequired>1</isrequired><iscontextrequired>0</iscontextrequired>" +
                @"</PropertyMapping></PropertyMappings>";

            // SourceType/TargetType (nazwy tabel) wyliczają typy wierszy:
            schemat.SourceDataType.Should().NotBeNull(
                "SourceDataType wylicza typ wiersza z nazwy tabeli źródłowej (WFWorkflows)");
            schemat.TargetDataType.Should().NotBeNull(
                "TargetDataType wylicza typ wiersza z nazwy tabeli docelowej (Zadania)");

            guidSchemat = schemat.Guid;
            UsunAutoTriggeryWorkaround();
        });
        SaveDisposeConfig();

        // Odczyt schematu po zapisie — klucz ByName (pojedynczy wiersz):
        var schemat2 = ConfigWorkflow.OGSchemas.ByName[nazwaSchematu];
        schemat2.Should().NotBeNull("OGSchemas.ByName[name] odnajduje zapisany schemat");
        schemat2.SourceType.Should().Be("WFWorkflows");
        schemat2.TargetType.Should().Be("Zadania");
        schemat2.CopyAttachments.Should().BeTrue();
        ((string)schemat2.XML).Should().Contain("PropertyMapping", "definicja mapowania (XML) utrwalona");

        // 2) Podpięcie schematu pod tranzycję (pole OGSchema) — generowanie w momencie przejścia.
        //    Tranzycja musi wychodzić z węzła na tabeli WFWorkflows (zgodność z SourceType).
        InConfigTransaction(() =>
        {
            var definicja = NowaDefinicja("C5");
            var wezel = NowyWezel(definicja, "Zadanie", tabela: "WFWorkflows");
            wezel.FormatedName = "Krok procesu";
            var koniec = NowyWezel(definicja, "Koniec", tabela: "WFWorkflows");
            koniec.FormatedName = "Koniec";

            var tranzycja = AddConfig(new WFTransition());
            tranzycja.Source = wezel;
            tranzycja.Target = koniec;
            tranzycja.Name = "Generuj zadanie";
            // OGSchema z TEJ SAMEJ sesji konfiguracyjnej (schemat zapisany wyżej):
            tranzycja.OGSchema = ConfigWorkflow.OGSchemas[guidSchemat];

            guidTranzycja = tranzycja.Guid;
            UsunAutoTriggeryWorkaround();
        });
        SaveDisposeConfig();

        var tr2 = GetConfig<WFTransition>(guidTranzycja);
        tr2.OGSchema.Should().NotBeNull("schemat generatora został podpięty pod tranzycję");
        tr2.OGSchema.Guid.Should().Be(guidSchemat, "tranzycja wskazuje nasz schemat OGSchema");
    }

    // POMINIĘTE WORKFLOW-C6: receptura nie jest oznaczona ★. Własny wzorzec WFTransitionDefinition i jego
    // przypięcie testujemy pośrednio w WORKFLOW-C1 (standardowy wzorzec 'Wybór operatora' po stałym Guid).

    // ============================== WORKFLOW-C7 — Dostępne tranzycje dla zadania (★) ==============================

    [Test]
    [Description("WORKFLOW-C7: strukturę przepływu (bez uruchomionego procesu) odtwarzamy na poziomie definicji — " +
                 "tranzycje WYCHODZĄCE z węzła to WFTransitions.WgSource[taskDef] (i TaskDefinition.SourceWFTransitions), " +
                 "tranzycje WCHODZĄCE to WgTarget[taskDef]; każdą tranzycję charakteryzują IsUserDecision/LP/Target.")]
    public void WORKFLOW_C7_DostepneTranzycje_NaPoziomieDefinicji_WgSourceIWgTarget()
    {
        Guid guidDef = Guid.Empty;
        Guid guidWniosek = Guid.Empty;
        Guid guidKoniec = Guid.Empty;

        InConfigTransaction(() =>
        {
            var definicja = NowaDefinicja("C7");
            var wniosek = NowyWezel(definicja, "Zadanie");
            wniosek.FormatedName = "Wniosek";
            var zaakceptowany = NowyWezel(definicja, "Koniec");
            zaakceptowany.FormatedName = "Zaakceptowany";
            var odrzucony = NowyWezel(definicja, "Koniec");
            odrzucony.FormatedName = "Odrzucony";

            // Tranzycja-decyzja użytkownika (wzorzec) + tranzycja automatyczna (wzorzec bezwarunkowy):
            var akceptuj = AddConfig(new WFTransition());
            akceptuj.Source = wniosek;
            akceptuj.Target = zaakceptowany;
            akceptuj.WFTransitionDefinition = ConfigWorkflow.WFTransitionDefs[WyborOperatoraGuid];
            akceptuj.Name = "Akceptuj";
            akceptuj.LP = 1;

            var odrzuc = AddConfig(new WFTransition());
            odrzuc.Source = wniosek;
            odrzuc.Target = odrzucony;
            odrzuc.WFTransitionDefinition = ConfigWorkflow.WFTransitionDefs[BezwarunkowePrzejscieGuid];
            odrzuc.Name = "Odrzuć";
            odrzuc.LP = 2;
            odrzuc.IsUserDecision.Should().BeFalse(
                "wzorzec 'Bezwarunkowe przejście\' daje tranzycję automatyczną (IsUserDecision=false)");

            guidDef = definicja.Guid;
            guidWniosek = wniosek.Guid;
            guidKoniec = zaakceptowany.Guid;
            UsunAutoTriggeryWorkaround();
        });
        SaveDisposeConfig();

        var wniosek2 = GetConfig<TaskDefinition>(guidWniosek);
        var transitions = ConfigWorkflow.WFTransitions;

        // Tranzycje WYCHODZĄCE z węzła — odtworzenie listy 'Decyzja'/przepływu (uporządkowane po LP, potem Name):
        var wychodzace = transitions.WgSource[wniosek2].Cast<WFTransition>()
            .OrderBy(tr => tr.LP).ThenBy(tr => tr.Name).ToList();
        wychodzace.Should().HaveCount(2, "z węzła 'Wniosek\' wychodzą dwie tranzycje");
        wychodzace.Select(tr => tr.Name).Should().ContainInOrder("Akceptuj", "Odrzuć");

        // Podział na decyzje operatora vs tranzycje automatyczne (czysty kontrakt po polach):
        wychodzace.Where(tr => tr.IsUserDecision).Should().ContainSingle("jedna decyzja użytkownika ('Akceptuj\')");
        wychodzace.Where(tr => !tr.IsUserDecision).Should().ContainSingle("jedna tranzycja automatyczna ('Odrzuć\')");

        // Tranzycje WCHODZĄCE do węzła docelowego — klucz WgTarget:
        var koniec2 = GetConfig<TaskDefinition>(guidKoniec);
        var wchodzace = transitions.WgTarget[koniec2].Cast<WFTransition>().ToList();
        wchodzace.Should().ContainSingle("do węzła 'Zaakceptowany\' wchodzi jedna tranzycja");
        wchodzace[0].Name.Should().Be("Akceptuj");
        wchodzace[0].Source.Guid.Should().Be(guidWniosek, "tranzycja wchodząca pochodzi z węzła 'Wniosek\'");

        // POMINIĘTE (część receptury): task.GetTransitions() i WFTransition.GetWFTransitionCalculator()
        // wymagają URUCHOMIONEGO procesu (Task z WFWorkflow != null) — silnik workflow nie startuje
        // deklaratywnie na bazie Demo bez wdrożonej definicji i triggerów; testujemy strukturę przepływu
        // na poziomie definicji (publiczne klucze WgSource/WgTarget).
    }
}
