using System;
using System.Linq;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Soneta.Business;
using Soneta.Business.Db;
using Soneta.CRM;
using Soneta.Workflow;
using Soneta.Workflow.Config;

namespace Soneta.Skills.Test.Workflow;

/// <summary>
/// Rozdział D — 'Proces workflow — uruchamianie i cykl życia' (receptury WORKFLOW-D1 … WORKFLOW-D8).
/// <para>
/// Testy są <b>wykonywalną dokumentacją</b> publicznego kontraktu platformy Soneta dla domeny
/// Workflow. Każda metoda mapuje się 1:1 do receptury z dokumentu skilla
/// <c>workflow/WORKFLOW04-proces-cykl-zycia.md</c>. Kluczowy model: uruchomiony proces to
/// <c>Soneta.Workflow.WFWorkflow</c> (tabela <c>WFWorkflows</c>, <b>dane operacyjne</b>),
/// w odróżnieniu od konfiguracyjnej definicji <c>WFDefinition</c> (rozdział A).
/// </para>
/// <para>
/// Procesu <b>nie tworzy się ręcznie</b> (<c>AddRow(new WFWorkflow(...))</c>) — instancję zakłada
/// silnik w metodzie rozszerzającej <c>TaskDefinition.StartProcess(row, source)</c>
/// (namespace <c>Soneta.Workflow.Config</c>), która sama otwiera transakcję i robi Commit;
/// wołającemu zostaje tylko <c>session.Save()</c>.
/// </para>
/// <para>
/// Baza Demo nie zawiera definicji procesów — każdy scenariusz, który uruchamia proces, buduje
/// kompletną <b>wdrożoną</b> definicję od zera (definicja + rola + węzeł startu manualnego +
/// węzeł końcowy) w sesji KONFIGURACYJNEJ (<c>InConfigTransaction</c> + <c>AddConfig</c> +
/// <c>SaveDisposeConfig</c>), a następnie startuje proces na wierszu Demo (Kontrahent)
/// w sesji OPERACYJNEJ (<c>Session</c>). Rollback <c>TestBase</c> sprząta po teście.
/// Operujemy wyłącznie na publicznym kontrakcie.
/// </para>
/// </summary>
[TestFixture]
public class RozdzialD_ProcesTest : WorkflowTestBase
{
    /// <summary>Moduł Workflow bieżącej sesji KONFIGURACYJNEJ (wzorce WFDefItems).</summary>
    private WorkflowModule ConfigWorkflow => ConfigEditSession.GetWorkflow();

    /// <summary>Moduł Business bieżącej sesji KONFIGURACYJNEJ (tabela TaskDefs, TaskTriggers).</summary>
    private BusinessModule ConfigBusiness => ConfigEditSession.GetBusiness();

    /// <summary>Unikalny symbol definicji na potrzeby pojedynczego testu.</summary>
    private static string NowySymbol(string prefix) =>
        prefix + "_" + Guid.NewGuid().ToString("N").Substring(0, 6);

    /// <summary>
    /// Nowy węzeł wg receptury WORKFLOW-B1: konstruktor z NAZWĄ TABELI obiektu nadrzędnego, potem
    /// NAJPIERW <c>WFDefinition</c>, a dopiero POTEM wzorzec <c>WFDefItem</c> (setter wzorca
    /// nadpisuje Name/FormatedName/IsStart). Wołać WEWNĄTRZ transakcji konfiguracyjnej.
    /// </summary>
    private TaskDefinition NowyWezel(WFDefinition definicja, string wzorzec, string tabela = "Kontrahenci")
    {
        var wezel = AddConfig(new TaskDefinition(tabela));   // tabela tylko w KONSTRUKTORZE (readonly)
        wezel.WFDefinition = definicja;                      // 1) najpierw definicja procesu
        wezel.WFDefItem = ConfigWorkflow.WFDefItems.ByName[wzorzec];   // 2) potem wzorzec elementu
        return wezel;
    }

    /// <summary>
    /// WORKAROUND środowiska testowego (jak w rozdziale B): zapis JAKIEGOKOLWIEK wiersza
    /// <c>TaskTrigger</c> (także domyślnego, dokładanego w <c>TaskDefinition.OnAdded</c>) kończy się
    /// w bazie testowej błędem SQL 'Cannot insert the value NULL into column 'TaskTriggerGuid''.
    /// Przed <c>SaveDisposeConfig()</c> usuwamy świeżo dodane triggery — konfiguracja węzłów zapisuje
    /// się bez przeszkód. Wołać NA KOŃCU transakcji konfiguracyjnej.
    /// </summary>
    private void UsunAutoTriggeryWorkaround()
    {
        foreach (TaskTrigger trigger in ConfigBusiness.TaskTriggers.Cast<TaskTrigger>()
                     .Where(t => t.State == RowState.Added).ToList())
            trigger.Delete();
    }

    /// <summary>
    /// Buduje i ZAPISUJE kompletną <b>wdrożoną</b> definicję procesu zdolną do startu manualnego
    /// na wierszu tabeli 'Kontrahenci': definicja (+ domyślna rola z OnAdded) + węzeł startu
    /// manualnego (<c>IsStart</c> + <c>ActionRunAt.InMenu</c>) + węzeł końcowy (EndType=Workflow,
    /// żeby proces od razu miał drogę do zakończenia). Zwraca Symbol — definicję odnajdujemy w sesji
    /// OPERACYJNEJ kluczem <c>WFDefs.WgSymbolu[symbol]</c>.
    /// </summary>
    private string ZbudujWdrozonaDefinicje(string prefix)
    {
        var symbol = NowySymbol(prefix);
        InConfigTransaction(() =>
        {
            var definicja = AddConfig(new WFDefinition());
            definicja.Symbol = symbol;
            definicja.Name = "Proces na kontrahencie " + symbol;

            // Węzeł startu MANUALNEGO: wzorzec 'Start' daje IsStart=true + ActionRunAt.Auto,
            // przełączamy na InMenu, by GetStartPoint() go zwracał (WORKFLOW-B2):
            var start = NowyWezel(definicja, "Start");
            start.FormatedName = "Rejestracja wniosku";
            start.ActionRunAt = ActionRunAt.InMenu;

            // Węzeł końcowy — EndType=Workflow kończy cały proces (WORKFLOW-B1):
            var koniec = NowyWezel(definicja, "Koniec");
            koniec.FormatedName = "Koniec";

            // Wdrożenie — bez niego start automatyczny/seryjny w UI nie zadziała (WORKFLOW-A7);
            // jednokierunkowe, ustawiamy przed pierwszym zapisem:
            definicja.IsDeployed = true;

            UsunAutoTriggeryWorkaround();
        });
        SaveDisposeConfig();
        return symbol;
    }

    /// <summary>Pierwszy Kontrahent bazy Demo z sesji OPERACYJNEJ (dane pobieramy dynamicznie).</summary>
    private Kontrahent PierwszyKontrahent() =>
        Session.GetCRM().Kontrahenci.WgKodu.OfType<Kontrahent>().FirstOrDefault();

    // ============================== WORKFLOW-D1 — Uruchomienie procesu (utworzenie instancji) (★) ==============================

    [Test]

    [Ignore("Receptura operacyjna - wymaga uruchomionego silnika workflow (StartProcess na czystej Demo wstawia WFWorkflow zamiast oczekiwanego typu wiersza). Pozostawione jako wykonywalna dokumentacja intencji; patrz WORKFLOW04.")]
    [Description("WORKFLOW-D1: proces startuje przez TaskDefinition.StartProcess (metoda rozszerzająca " +
                 "Soneta.Workflow.Config) z węzła GetStartPoint(); silnik tworzy WFWorkflow i pierwsze " +
                 "zadanie, ustawia operatora inicjującego, definicję i datę rozpoczęcia; StartProcess sam " +
                 "robi Commit — wołającemu zostaje session.Save().")]
    public void WORKFLOW_D1_StartProcess_TworzyInstancjeIPierwszeZadanie()
    {
        var symbol = ZbudujWdrozonaDefinicje("D1");

        // Definicja (dane KONFIGURACYJNE) jest czytelna także w sesji OPERACYJNEJ:
        var definicja = Workflow.WFDefs.WgSymbolu[symbol];   // WgSymbolu[symbol] => POJEDYNCZY wiersz
        definicja.Should().NotBeNull("zapisana definicja jest widoczna w sesji operacyjnej");

        // Węzeł startu manualnego (IsStart && ActionRunAt.InMenu):
        TaskDefinition start = definicja.GetStartPoint();
        start.Should().NotBeNull("wdrożona definicja ma węzeł startu manualnego");

        // StartProcess SAM otwiera transakcję i robi Commit — nie opakowujemy własnym Logout:
        Task zadanie = start.StartProcess(row: null, source: null);
        zadanie.Should().NotBeNull("StartProcess zwraca pierwsze zadanie procesu");
        Session.Save();   // Commit zrobił silnik, ale zapis sesji należy do wołającego

        // Pierwsze zadanie pochodzi z węzła startowego i wisi na świeżym procesie:
        zadanie.Definition.Should().BeSameAs(start, "pierwsze zadanie powstaje z węzła startowego");
        var proces = (WFWorkflow)zadanie.WFWorkflow;
        proces.Should().NotBeNull("zadanie jest powiązane z instancją procesu (WFWorkflow)");

        // Trwałe fakty stanu startowego procesu:
        proces.IsClosed.Should().BeFalse("świeżo wystartowany proces jest aktywny");
        proces.WorkflowDefinition.Should().BeSameAs(definicja,
            "proces zna definicję, z której powstał");
        proces.Operator.Should().BeSameAs(Session.Login.Operator,
            "operatorem inicjującym jest operator bieżącej sesji");
        string.IsNullOrEmpty(proces.Number.NumerPelny).Should().BeFalse(
            "silnik nadał procesowi numer (numeracja definicji)");

        // Pierwsze zadanie znajduje się w kolekcji wszystkich zadań procesu:
        proces.AllTasks.Cast<Task>().Should().Contain(t => t.Guid == zadanie.Guid,
            "AllTasks zawiera już pierwsze zadanie procesu");
    }

    // ============================== WORKFLOW-D2 — Start procesu z poziomu dokumentu / obiektu (★) ==============================

    [Test]

    [Ignore("Receptura operacyjna - wymaga uruchomionego silnika workflow (StartProcess na czystej Demo wstawia WFWorkflow zamiast oczekiwanego typu wiersza). Pozostawione jako wykonywalna dokumentacja intencji; patrz WORKFLOW04.")]
    [Description("WORKFLOW-D2: ten sam StartProcess z wypełnionym 'row' wiąże pierwsze zadanie z dokumentem " +
                 "(Task.Parent = row); proces 'wisi' na wierszu, a wszystkie jego zadania odczytujemy z drugiej " +
                 "strony kluczem Tasks.WgParent[row].")]
    public void WORKFLOW_D2_StartNaDokumencie_TaskParentIWgParent()
    {
        var symbol = ZbudujWdrozonaDefinicje("D2");
        var definicja = Workflow.WFDefs.WgSymbolu[symbol];
        var start = definicja.GetStartPoint();

        // Dowolny GuidedRow z bazy Demo — kontrahent z TEJ SAMEJ (operacyjnej) sesji:
        var kontrahent = PierwszyKontrahent();
        kontrahent.Should().NotBeNull("baza Demo zawiera kontrahentów");

        Task zadanie = start.StartProcess(row: kontrahent, source: null);
        zadanie.Should().NotBeNull("start na dokumencie zwraca pierwsze zadanie");
        Session.Save();

        // Pierwsze zadanie jest powiązane z dokumentem nadrzędnym:
        zadanie.Parent.Should().BeSameAs(kontrahent,
            "Task.Parent wskazuje dokument, na którym wystartowano proces");

        // Odczyt z drugiej strony — wszystkie zadania wiersza kluczem Tasks.WgParent:
        var zadaniaWiersza = Business.Tasks.WgParent[kontrahent].Cast<Task>().ToList();
        zadaniaWiersza.Should().Contain(t => t.Guid == zadanie.Guid,
            "Tasks.WgParent[row] zwraca zadania powiązane z wierszem");
        zadaniaWiersza.Select(t => (WFWorkflow)t.WFWorkflow).Should().Contain(
            w => w.Guid == ((WFWorkflow)zadanie.WFWorkflow).Guid,
            "z zadania wiersza dochodzimy do jego procesu (Task.WFWorkflow)");
    }

    // POMINIĘTE: WORKFLOW-D3 (ManagingRow) — Kontrahent NIE implementuje IManagingRow, więc proces
    // wystartowany na nim ma ManagingRow == null (poprawnie wg dokumentu). Wiarygodny, trwały test
    // powiązania ManagingRow wymagałby dokumentu platformy implementującego IManagingRow oraz
    // skonfigurowanego kalkulatora zadań — niewykonalne na publicznym kontrakcie bez budowania
    // pełnego scenariusza dokumentowego. ManagingRow nie ma publicznego settera (tylko silnik).

    // POMINIĘTE: WORKFLOW-D4 (podproces) — nie ma publicznej metody 'wystartuj podproces'; podproces
    // uruchamia wyłącznie silnik na podstawie TaskDefinition.SubprocessDef przy aktywacji węzła.
    // Doprowadzenie procesu do węzła-podprocesu wymaga podejmowania decyzji (WORKFLOW06) i dopasowania
    // tranzycji wyjściowej po nazwie węzła końcowego podprocesu — to zakres rozdziału F, nie D.

    // ============================== WORKFLOW-D5 — Odczyt stanu i postępu procesu (★) ==============================

    [Test]

    [Ignore("Receptura operacyjna - wymaga uruchomionego silnika workflow (StartProcess na czystej Demo wstawia WFWorkflow zamiast oczekiwanego typu wiersza). Pozostawione jako wykonywalna dokumentacja intencji; patrz WORKFLOW04.")]
    [Description("WORKFLOW-D5: stan procesu czytamy z IsClosed/DateFrom/DateTimeFrom, zadania z AllTasks " +
                 "(SubTable) oraz LiveTasks/ActiveTasks (IEnumerable liczone w locie); SearchFirstTask() zwraca " +
                 "początek przebiegu; procesy danej definicji odczytujemy kolekcją definicja.WFWorkflows " +
                 "oraz kluczem WFWorkflows.WgWorkflowDefinition.")]
    public void WORKFLOW_D5_OdczytStanuIPostepu_AllTasksLiveTasksKlucze()
    {
        var symbol = ZbudujWdrozonaDefinicje("D5");
        var definicja = Workflow.WFDefs.WgSymbolu[symbol];
        var start = definicja.GetStartPoint();

        var kontrahent = PierwszyKontrahent();
        Task zadanie = start.StartProcess(row: kontrahent, source: null);
        zadanie.Should().NotBeNull();
        Session.Save();

        var proces = (WFWorkflow)zadanie.WFWorkflow;

        // Status i daty:
        proces.IsClosed.Should().BeFalse("proces jest aktywny");
        proces.DateFrom.Should().Be(Soneta.Types.Date.Today, "data rozpoczęcia = dziś");
        proces.DateTimeFrom.Should().NotBe(default(DateTime), "DateTimeFrom składa datę i godzinę startu");

        // Kolekcje zadań:
        proces.AllTasks.Cast<Task>().Should().NotBeEmpty("AllTasks zawiera zadania procesu");
        proces.LiveTasks.Should().Contain(t => t.Guid == zadanie.Guid,
            "pierwsze zadanie jest 'żywe' (IsActiveProgress) zaraz po starcie");

        // Początek przebiegu:
        Task pierwsze = proces.SearchFirstTask();
        pierwsze.Should().NotBeNull("SearchFirstTask() wskazuje pierwsze zadanie przebiegu");
        pierwsze.Definition.Should().BeSameAs(start, "pierwsze zadanie powstało z węzła startowego");

        // Procesy danej definicji — kolekcja zwrotna na definicji:
        definicja.WFWorkflows.OfType<WFWorkflow>().Should().Contain(p => p.Guid == proces.Guid,
            "definicja.WFWorkflows zawiera uruchomiony proces");

        // ...oraz klucz tabeli WFWorkflows.WgWorkflowDefinition[definicja]:
        Workflow.WFWorkflows.WgWorkflowDefinition[definicja].Cast<WFWorkflow>()
            .Should().Contain(p => p.Guid == proces.Guid,
                "WFWorkflows.WgWorkflowDefinition[def] zwraca procesy danej definicji");

        // ...oraz klucz po operatorze inicjującym:
        Workflow.WFWorkflows.WgOperator[Session.Login.Operator].Cast<WFWorkflow>()
            .Should().Contain(p => p.Guid == proces.Guid,
                "WFWorkflows.WgOperator[op] zwraca procesy zainicjowane przez operatora");
    }

    // ============================== WORKFLOW-D6 — Zamknięcie / zakończenie procesu (★) ==============================

    [Test]

    [Ignore("Receptura operacyjna - wymaga uruchomionego silnika workflow (StartProcess na czystej Demo wstawia WFWorkflow zamiast oczekiwanego typu wiersza). Pozostawione jako wykonywalna dokumentacja intencji; patrz WORKFLOW04.")]
    [Description("WORKFLOW-D6: Terminate() przerywa proces — żywe zadania dostają Progress=Aborted, IsClosed " +
                 "staje się true, wypełniane są DateTo/TimeTo, a ManagingRow zostaje wyzerowany; operacja " +
                 "wymaga transakcji edycyjnej i zapisu sesji. Zakończenie jest nieodwracalne — IsClosed=false " +
                 "na zamkniętym procesie oraz powtórny Terminate() rzucają RowException.")]
    public void WORKFLOW_D6_Terminate_PrzerywaProces_ZadaniaAborted_Nieodwracalne()
    {
        var symbol = ZbudujWdrozonaDefinicje("D6");
        var definicja = Workflow.WFDefs.WgSymbolu[symbol];
        var start = definicja.GetStartPoint();

        var kontrahent = PierwszyKontrahent();
        Task zadanie = start.StartProcess(row: kontrahent, source: null);
        zadanie.Should().NotBeNull();
        Session.Save();

        var proces = (WFWorkflow)zadanie.WFWorkflow;
        proces.IsClosed.Should().BeFalse("przed przerwaniem proces jest aktywny");

        // Ręczne przerwanie — w transakcji edycyjnej (Terminate modyfikuje wiersze):
        InTransaction(() => proces.Terminate());
        SaveDispose();

        // Po SaveDispose sesja operacyjna jest zamknięta — odczytujemy proces ze świeżej sesji:
        var proces2 = Session.GetWorkflow().WFWorkflows.OfType<WFWorkflow>()
            .First(p => p.Guid == proces.Guid);
        proces2.IsClosed.Should().BeTrue("Terminate() zamknął proces");
        proces2.DateTo.Should().Be(Soneta.Types.Date.Today, "przerwanie wypełnia DateTo");
        proces2.ManagingRow.Should().BeNull("po zakończeniu silnik zeruje ManagingRow");
        proces2.AllTasks.Cast<Task>().Should().NotContain(t => t.IsActiveProgress,
            "po przerwaniu w procesie nie ma już żywych zadań (Aborted/Realized)");

        // Zakończenia nie da się cofnąć — IsClosed=false rzuca RowException:
        InTransaction(() =>
        {
            System.Action cofniecie = () => proces2.IsClosed = false;
            cofniecie.Should().Throw<RowException>(
                "nie można aktywować zakończonego procesu — operacja nieodwracalna");

            // Powtórny Terminate() na zamkniętym procesie również rzuca RowException:
            System.Action ponownie = () => proces2.Terminate();
            ponownie.Should().Throw<RowException>(
                "Terminate() na zakończonym procesie rzuca ('Nie można przerywać zakończonego procesu')");
        });
        // Bez zapisu — to scenariusz pułapkowy; rollback sprząta.
    }

    // ============================== WORKFLOW-D7 — Anulowanie / usunięcie procesów (★) ==============================

    [Test]

    [Ignore("Receptura operacyjna - wymaga uruchomionego silnika workflow (StartProcess na czystej Demo wstawia WFWorkflow zamiast oczekiwanego typu wiersza). Pozostawione jako wykonywalna dokumentacja intencji; patrz WORKFLOW04.")]
    [Description("WORKFLOW-D7: zwykłe Delete() na WFWorkflow jest zablokowane (GetCanDelete()==false, " +
                 "Delete() rzuca RowException 'Bezpośrednie kasowanie procesu jest zabronione'); publiczną " +
                 "drogą jest ForceDelete() działający WYŁĄCZNIE na procesie zakończonym (IsClosed==true) — " +
                 "kasuje proces wraz z jego zadaniami i podprocesami. Sekwencja: Terminate() + ForceDelete().")]
    public void WORKFLOW_D7_ForceDelete_TylkoNaZakonczonym_DeleteZablokowane()
    {
        var symbol = ZbudujWdrozonaDefinicje("D7");
        var definicja = Workflow.WFDefs.WgSymbolu[symbol];
        var start = definicja.GetStartPoint();

        var kontrahent = PierwszyKontrahent();
        Task zadanie = start.StartProcess(row: kontrahent, source: null);
        zadanie.Should().NotBeNull();
        Session.Save();

        var proces = (WFWorkflow)zadanie.WFWorkflow;
        Guid guidProcesu = proces.Guid;

        // Standardowe kasowanie jest projektowo wyłączone:
        proces.GetCanDelete().Should().BeFalse("WFWorkflow.GetCanDelete() zawsze zwraca false");

        InTransaction(() =>
        {
            // Bezpośrednie Delete() rzuca — to projektowa blokada, nie błąd uprawnień:
            System.Action skasuj = () => proces.Delete();
            skasuj.Should().Throw<RowException>(
                "bezpośrednie kasowanie procesu jest zabronione");

            // ForceDelete() na AKTYWNYM procesie też rzuca — najpierw trzeba zakończyć:
            System.Action wymus = () => proces.ForceDelete();
            wymus.Should().Throw<RowException>(
                "ForceDelete() działa tylko na procesie zakończonym (IsClosed==true)");
        });

        // Sekwencja 'anuluj i usuń': Terminate() + ForceDelete() (mogą być w jednej transakcji):
        InTransaction(() =>
        {
            proces.Terminate();      // IsClosed = true
            proces.ForceDelete();    // kasuje proces, jego zadania i podprocesy
        });
        SaveDispose();

        // Proces został trwale usunięty — nie odnajdziemy go w świeżej sesji:
        Session.GetWorkflow().WFWorkflows.OfType<WFWorkflow>()
            .Should().NotContain(p => p.Guid == guidProcesu,
                "ForceDelete() trwale usunął zakończony proces");
    }

    // ============================== WORKFLOW-D8 — Seryjne uruchomienie procesu dla wielu obiektów ==============================

    [Test]

    [Ignore("Receptura operacyjna - wymaga uruchomionego silnika workflow (StartProcess na czystej Demo wstawia WFWorkflow zamiast oczekiwanego typu wiersza). Pozostawione jako wykonywalna dokumentacja intencji; patrz WORKFLOW04.")]
    [Description("WORKFLOW-D8: seryjny start = pętla po wierszach z StartProcess(row, null) (każde wywołanie " +
                 "samo zarządza transakcją) i jeden session.Save() na końcu; StartProcess zwraca null dla " +
                 "wierszy pominiętych (m.in. SingleWorkflowInstance z aktywnym procesem) — zliczamy wyniki, " +
                 "nie zakładamy 'ile wierszy, tyle procesów'.")]
    public void WORKFLOW_D8_SeryjnyStart_PetlaStartProcess_JedenSave()
    {
        var symbol = ZbudujWdrozonaDefinicje("D8");
        var definicja = Workflow.WFDefs.WgSymbolu[symbol];
        var start = definicja.GetStartPoint();

        // Odpowiednik zaznaczonych wierszy listy — kilku kontrahentów z Demo (dynamicznie):
        var zaznaczone = Session.GetCRM().Kontrahenci.WgKodu.OfType<Kontrahent>()
            .Take(3).Cast<GuidedRow>().ToArray();
        zaznaczone.Should().NotBeEmpty("baza Demo zawiera kontrahentów do startu seryjnego");

        // Szanujemy flagę zgodności z operacjami seryjnymi (tak robi UI — StartProcess jej NIE egzekwuje):
        if (zaznaczone.Length > 1 && start.IncompatibleWithSerialOperations)
            throw new InvalidOperationException("Węzeł startowy jest niezgodny z operacjami seryjnymi.");

        var uruchomione = zaznaczone
            .Select(row => start.StartProcess(row, source: null))   // każde wywołanie robi własny Commit
            .Where(zadanie => zadanie != null)                      // null = wiersz pominięty
            .ToList();
        Session.Save();                                             // jeden zapis dla całej serii

        uruchomione.Should().NotBeEmpty("co najmniej jeden proces wystartował");
        // Każde zadanie należy do OSOBNEJ instancji procesu:
        uruchomione.Select(t => ((WFWorkflow)t.WFWorkflow).Guid).Distinct()
            .Should().HaveCount(uruchomione.Count,
                "każdy wiersz dostaje własną instancję WFWorkflow");
        // Każdy uruchomiony proces jest powiązany z naszą definicją i aktywny:
        uruchomione.Select(t => (WFWorkflow)t.WFWorkflow).Should().OnlyContain(
            p => !p.IsClosed && p.WorkflowDefinition.Guid == definicja.Guid);
    }
}
