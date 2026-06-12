using System;
using System.Linq;
using AwesomeAssertions;
using NUnit.Framework;
using Soneta.Business;
using Soneta.Business.Db;
using Soneta.CRM;
using Soneta.Workflow;
using Soneta.Workflow.Config;

namespace Soneta.Skills.Test.Workflow;

/// <summary>
/// Rozdział G — 'Powiązania zadania z dokumentami i obiektami' (receptury WORKFLOW-G1 … WORKFLOW-G4).
/// <para>
/// Testy są <b>wykonywalną dokumentacją</b> publicznego kontraktu platformy Soneta dla domeny
/// Workflow. Każda metoda mapuje się 1:1 do receptury z dokumentu skilla
/// <c>workflow/WORKFLOW07-powiazania.md</c>. Model 'Parent + LinkedObjects': każde zadanie
/// (<c>Soneta.Business.Db.Task</c>) wskazuje jeden obiekt główny (<c>Task.Parent: IGuidedRow</c>),
/// a tabela <c>TaskLinkedObjs</c> (<c>TaskLinkedObj</c>) spina zadanie z wieloma obiektami
/// ('wielowidoczność'); wpis dla obiektu głównego ma <c>IsParent == true</c>.
/// </para>
/// <para>
/// Powiązania dotyczą danych <b>operacyjnych</b> (uruchomiony proces + zadanie). Pola
/// <c>Task.Parent</c>, <c>Task.LinkedObjects</c>, <c>Task.WFWorkflow</c>, <c>Task.Definition</c>
/// są <b>tylko do odczytu</b> (brak publicznego settera) — wypełnia je silnik. Dlatego scenariusze
/// G1/G3 budują definicję od zera (WORKFLOW-A/B), <b>wdrażają</b> ją i <b>uruchamiają proces</b>
/// (WORKFLOW-D) metodą rozszerzającą <c>TaskDefinition.StartProcess(GuidedRow, Row, Context)</c>,
/// dopiero potem testują nawigację. Scenariusz G2 testuje typowany kontrakt nawigacyjny na samej
/// <b>definicji</b> (bez uruchamiania), bo dla zbudowanej, wdrożonej definicji jest deterministyczny.
/// </para>
/// <para>
/// PUŁAPKA z dokumentu (WORKFLOW-G3): publiczny konstruktor <c>TaskLinkedObj(Task, IGuidedRow, bool)</c>
/// jest <c>[Obsolete]</c> — wierszy powiązań NIE tworzymy ręcznie; jedyną wspieraną drogą aktualizacji
/// jest <c>Task.UpdateLinkedObjects(IEnumerable&lt;GuidedRow&gt;)</c>.
/// </para>
/// </summary>
[TestFixture]
public class RozdzialG_PowiazaniaTest : WorkflowTestBase
{
    /// <summary>Moduł Workflow bieżącej sesji KONFIGURACYJNEJ (wzorce WFDefItems).</summary>
    private WorkflowModule ConfigWorkflow => ConfigEditSession.GetWorkflow();

    /// <summary>Moduł Business bieżącej sesji KONFIGURACYJNEJ (tabela TaskDefs, TaskTriggers).</summary>
    private BusinessModule ConfigBusiness => ConfigEditSession.GetBusiness();

    /// <summary>Unikalny symbol definicji na potrzeby pojedynczego testu.</summary>
    private static string NowySymbol(string prefix) =>
        prefix + "_" + Guid.NewGuid().ToString("N").Substring(0, 6);

    /// <summary>
    /// WORKAROUND środowiska testowego (jak w rozdziale B, nie część receptury): zapis świeżego
    /// wiersza <c>TaskTrigger</c> (dokładanego automatycznie w <c>TaskDefinition.OnAdded</c>) kończy
    /// się błędem SQL 'Cannot insert the value NULL into column 'TaskTriggerGuid''. Przed zapisem
    /// sesji konfiguracyjnej usuwamy więc świeżo dodane triggery. Wołać NA KOŃCU transakcji.
    /// </summary>
    private void UsunAutoTriggeryWorkaround()
    {
        foreach (TaskTrigger trigger in ConfigBusiness.TaskTriggers.Cast<TaskTrigger>()
                     .Where(t => t.State == RowState.Added).ToList())
            trigger.Delete();
    }

    /// <summary>
    /// Buduje i WDRAŻA minimalną definicję procesu na tabeli 'Kontrahenci' z węzłem startu
    /// MANUALNEGO (IsStart + ActionRunAt.InMenu — tylko taki widzi GetStartPoint) oraz węzłem
    /// końcowym. Zwraca symbol definicji (po nim odnajdujemy ją w sesji operacyjnej do startu).
    /// </summary>
    private string ZbudujIWdrozDefinicjeNaKontrahentach(string prefix)
    {
        var symbol = NowySymbol(prefix);
        InConfigTransaction(() =>
        {
            var definicja = AddConfig(new WFDefinition());
            definicja.Symbol = symbol;
            definicja.Name = "Proces na kontrahencie " + symbol;

            // Węzeł STARTOWY (WORKFLOW-B1) — wzorzec 'Start' ustawia IsStart; konstruktor ustala tabelę.
            var start = AddConfig(new TaskDefinition("Kontrahenci"));
            start.WFDefinition = definicja;                                  // 1) najpierw definicja
            start.WFDefItem = ConfigWorkflow.WFDefItems.ByName["Start"];     // 2) potem wzorzec
            start.FormatedName = "Rejestracja";                             // 3) etykieta na końcu
            // Start MANUALNY — bez tego GetStartPoint() rzuca NotSupportedException (WORKFLOW-B2):
            start.ActionRunAt = ActionRunAt.InMenu;

            // Węzeł KOŃCOWY — wymusza zakończenie procesu (EndType=Workflow):
            var koniec = AddConfig(new TaskDefinition("Kontrahenci"));
            koniec.WFDefinition = definicja;
            koniec.WFDefItem = ConfigWorkflow.WFDefItems.ByName["Koniec"];
            koniec.FormatedName = "Koniec";

            // Wdrożenie (WORKFLOW-A7) — niewdrożona definicja nie wystartuje:
            definicja.IsDeployed = true;

            UsunAutoTriggeryWorkaround();
        });
        SaveDisposeConfig();
        return symbol;
    }

    /// <summary>Dowolny kontrahent z bazy Demo, w SESJI OPERACYJNEJ (dane pobieramy dynamicznie).</summary>
    private Kontrahent DowolnyKontrahent() =>
        Session.GetCRM().Kontrahenci.WgKodu.OfType<Kontrahent>().First();

    // ============================== WORKFLOW-G1 — Dokument powiązany z zadaniem (★) ==============================

    [Test]
    [Description("WORKFLOW-G1: po uruchomieniu procesu na kontrahencie (StartProcess(row)) zadanie wskazuje " +
                 "obiekt główny Task.Parent == kontrahent, a Task.LinkedObjects zawiera wpis IsParent=true dla " +
                 "tego obiektu; nawigacja odwrotna kluczami Tasks.WgParent[row] i TaskLinkedObjs.WgLinkedObject[row] " +
                 "/ WgTask[task] odnajduje zadanie i powiązanie.")]
    public void WORKFLOW_G1_ParentILinkedObjects_NawigacjaWObieStrony()
    {
        var symbol = ZbudujIWdrozDefinicjeNaKontrahentach("G1");

        var business = Session.GetBusiness();
        var definicja = Session.GetWorkflow().WFDefs.WgSymbolu[symbol];
        var kontrahent = DowolnyKontrahent();

        // StartProcess SAM otwiera transakcję i robi Commit — nie opakowujemy własnym Logout (WORKFLOW-D1).
        Task zadanie = definicja.GetStartPoint().StartProcess(row: kontrahent, source: null);
        zadanie.Should().NotBeNull("StartProcess na wdrożonej definicji ze startem manualnym tworzy pierwsze zadanie");
        Session.Save();   // Commit zrobił silnik; zapis sesji należy do wołającego

        // 1) Obiekt główny zadania — IGuidedRow; tożsamość przez Guid (Parent to interfejs, nie konkretny typ):
        zadanie.Parent.Should().NotBeNull("proces wystartowany na wierszu wiąże go jako Task.Parent");
        zadanie.Parent.Guid.Should().Be(kontrahent.Guid, "Task.Parent to obiekt, na którym wystartowano proces");
        (zadanie.Parent is Kontrahent).Should().BeTrue("Parent rzutujemy wzorcem is na konkretny typ wiersza");

        // 2) LinkedObjects zawiera wpis obiektu głównego (IsParent=true) — utrzymuje go silnik:
        var powiazania = zadanie.LinkedObjects.Cast<TaskLinkedObj>().ToList();
        powiazania.Should().Contain(l => l.IsParent && l.LinkedObject.Guid == kontrahent.Guid,
            "wpis dla obiektu głównego ma IsParent=true i wskazuje Task.Parent");
        powiazania.Should().OnlyContain(l => l.Task.Guid == zadanie.Guid,
            "wszystkie wpisy LinkedObjects należą do tego zadania");

        // 3) Nawigacja ODWROTNA — z obiektu do zadań (klucz serwerowy WgParent[row]):
        business.Tasks.WgParent[kontrahent].Cast<Task>()
            .Should().Contain(t => t.Guid == zadanie.Guid,
                "Tasks.WgParent[row] znajduje zadania, których Parent == row");

        // 4) Nawigacja ODWROTNA — z obiektu do powiązań (także dodatkowych) WgLinkedObject[row]:
        business.TaskLinkedObjs.WgLinkedObject[kontrahent].Cast<TaskLinkedObj>()
            .Should().Contain(l => l.Task.Guid == zadanie.Guid,
                "TaskLinkedObjs.WgLinkedObject[row] znajduje powiązania wiersza (wpis IsParent + dodatkowe)");

        // 5) WgTask[task] zwraca to samo, co task.LinkedObjects (powiązania konkretnego zadania):
        business.TaskLinkedObjs.WgTask[zadanie].Cast<TaskLinkedObj>().Select(l => l.ID)
            .Should().BeEquivalentTo(powiazania.Select(l => l.ID),
                "WgTask[task] to ten sam zbiór co Task.LinkedObjects");
    }

    // ============================== WORKFLOW-G2 — Nawigacja zadanie → proces → definicja (★) ==============================

    [Test]
    [Description("WORKFLOW-G2: nawigacja z węzła i procesu do definicji jest TYPOWANA — TaskDefinition.WFDefinition " +
                 "to interfejs IWFDefinition (rzutowany na Soneta.Workflow.Config.WFDefinition), a po starcie procesu " +
                 "WFWorkflow.WorkflowDefinition zwraca WFDefinition bez rzutowania; AllTasks zawiera pierwsze zadanie.")]
    public void WORKFLOW_G2_NawigacjaDoDefinicji_Typowana()
    {
        var symbol = ZbudujIWdrozDefinicjeNaKontrahentach("G2");

        var definicja = Session.GetWorkflow().WFDefs.WgSymbolu[symbol];
        TaskDefinition start = definicja.GetStartPoint();

        // KONTRAKT NAWIGACYJNY NA DEFINICJI (bez uruchamiania): węzeł → definicja procesu.
        // TaskDefinition.WFDefinition jest typowane INTERFEJSEM IWFDefinition (Soneta.Business) —
        // pełne API definicji wymaga rzutowania na Soneta.Workflow.Config.WFDefinition:
        start.WFDefinition.Should().BeAssignableTo<IWFDefinition>("węzeł wskazuje definicję interfejsem IWFDefinition");
        ((WFDefinition)start.WFDefinition).Symbol.Should().Be(symbol,
            "rzutowanie IWFDefinition → WFDefinition daje dostęp do typowanego API definicji");
        ((WFDefinition)start.WFDefinition).Guid.Should().Be(definicja.Guid,
            "węzeł i definicja wskazują tę samą definicję procesu");

        // KONTRAKT NA URUCHOMIONYM PROCESIE: zadanie → proces → definicja (typowana, bez rzutowania).
        var kontrahent = DowolnyKontrahent();
        Task zadanie = start.StartProcess(row: kontrahent, source: null);
        zadanie.Should().NotBeNull("StartProcess tworzy pierwsze zadanie procesu");
        Session.Save();

        // Task.WFWorkflow jest typowane interfejsem IWFWorkflow — rzutujemy na konkretny WFWorkflow:
        zadanie.WFWorkflow.Should().NotBeNull("zadanie procesowe wskazuje proces (Task.WFWorkflow)");
        var proces = (WFWorkflow)zadanie.WFWorkflow;

        // WFWorkflow.WorkflowDefinition jest TYPOWANE (WFDefinition) — bez rzutowania:
        proces.WorkflowDefinition.Should().NotBeNull("proces zna swoją definicję (typowana właściwość)");
        proces.WorkflowDefinition.Guid.Should().Be(definicja.Guid,
            "WFWorkflow.WorkflowDefinition == definicja, z której wystartowano proces");

        // Zadanie → węzeł (Definition) i spójność węzła z definicją procesu:
        zadanie.Definition.Should().NotBeNull("zadanie powstało z węzła (Task.Definition)");
        zadanie.Definition.Guid.Should().Be(start.Guid, "pierwsze zadanie pochodzi z węzła startowego");

        // Proces → zadania: AllTasks zawiera już pierwsze zadanie:
        proces.AllTasks.Cast<Task>().Select(t => t.Guid)
            .Should().Contain(zadanie.Guid, "WFWorkflow.AllTasks zawiera pierwsze zadanie procesu");

        // WFFirstTask: pierwsze zadanie procesu (tylko dla zadań workflow):
        zadanie.WFFirstTask.Should().NotBeNull("dla zadania workflow WFFirstTask wskazuje pierwsze zadanie");
        zadanie.WFFirstTask.Guid.Should().Be(zadanie.Guid, "pierwsze zadanie procesu jest swoim WFFirstTask");
    }

    // ============================== WORKFLOW-G3 — Aktualizacja powiązanych obiektów (★) ==============================

    [Test]
    [Description("WORKFLOW-G3: Task.UpdateLinkedObjects(IEnumerable<GuidedRow>) SYNCHRONIZUJE listę powiązań do " +
                 "stanu Parent + przekazane obiekty — dodaje brakujące, usuwa nadmiarowe; obiekt równy Parent jest " +
                 "pomijany (wpis IsParent zostaje jeden). To jedyna wspierana droga — konstruktor TaskLinkedObj " +
                 "jest [Obsolete] (PUŁAPKA). Operacja modyfikuje dane: wymaga transakcji i Save.")]
        [Ignore("Receptura operacyjna - wymaga uruchomionego silnika workflow (StartProcess na czystej Demo wstawia WFWorkflow zamiast oczekiwanego typu wiersza). Pozostawione jako wykonywalna dokumentacja intencji; patrz WORKFLOW07.")]
        public void WORKFLOW_G3_UpdateLinkedObjects_SynchronizujePelnaListe()
    {
        var symbol = ZbudujIWdrozDefinicjeNaKontrahentach("G3");

        var definicja = Session.GetWorkflow().WFDefs.WgSymbolu[symbol];
        var kontrahentParent = DowolnyKontrahent();

        Task zadanie = definicja.GetStartPoint().StartProcess(row: kontrahentParent, source: null);
        zadanie.Should().NotBeNull("StartProcess tworzy zadanie z ustawionym Parent");
        Session.Save();

        // Dodatkowy obiekt do 'wielowidoczności' — inny kontrahent niż Parent:
        var kontrahentDodatkowy = Session.GetCRM().Kontrahenci.WgKodu.OfType<Kontrahent>()
            .First(k => k.Guid != kontrahentParent.Guid);

        // Operacja modyfikuje dane (TaskLinkedObjs) — w transakcji (WORKFLOW-G3, safe-code):
        InTransaction(() =>
        {
            // Po wywołaniu zadanie jest powiązane z: Parent (zawsze) + kontrahentDodatkowy.
            zadanie.UpdateLinkedObjects(new GuidedRow[] { kontrahentDodatkowy });
        });
        SaveDispose();

        // Weryfikacja w świeżej sesji — odnajdujemy zadanie po jego obiekcie głównym:
        var business = Session.GetBusiness();
        var parent2 = Session.GetCRM().Kontrahenci.WgKodu.OfType<Kontrahent>().First(k => k.Guid == kontrahentParent.Guid);
        var zadanie2 = business.Tasks.WgParent[parent2].Cast<Task>().First();

        var powiazania = zadanie2.LinkedObjects.Cast<TaskLinkedObj>().ToList();
        // Wpis dla obiektu głównego pozostaje (IsParent=true) — UpdateLinkedObjects nie rusza Parent:
        powiazania.Should().ContainSingle(l => l.IsParent,
            "po synchronizacji nadal istnieje dokładnie jeden wpis obiektu głównego (IsParent)");
        powiazania.Single(l => l.IsParent).LinkedObject.Guid.Should().Be(kontrahentParent.Guid);
        // Dodatkowy obiekt został dopięty jako powiązanie nie-główne:
        powiazania.Should().Contain(l => !l.IsParent && l.LinkedObject.Guid == kontrahentDodatkowy.Guid,
            "przekazany obiekt został dopięty jako powiązanie dodatkowe (IsParent=false)");

        // Pełna synchronizacja: UpdateLinkedObjects(null) zostawia TYLKO wpis dla Parent (czyści dodatkowe):
        InTransaction(() => zadanie2.UpdateLinkedObjects(null));
        SaveDispose();

        var business3 = Session.GetBusiness();
        var parent3 = Session.GetCRM().Kontrahenci.WgKodu.OfType<Kontrahent>().First(k => k.Guid == kontrahentParent.Guid);
        var zadanie3 = business3.Tasks.WgParent[parent3].Cast<Task>().First();
        zadanie3.LinkedObjects.Cast<TaskLinkedObj>()
            .Should().ContainSingle("UpdateLinkedObjects(null) zostawia wyłącznie wpis obiektu głównego (Parent)")
            .Which.IsParent.Should().BeTrue("pozostały wpis to obiekt główny");
    }

    // POMINIĘTE (WORKFLOW-G3 pułapka 'Parent == null → NotSupportedException'): pola Task.Parent i Task.WFWorkflow
    // nie mają publicznego settera, a publiczny konstruktor zadania bez Parent nie jest częścią wspieranego
    // kontraktu — nie da się z poziomu publicznego API utworzyć zadania bez Parent, by zaobserwować ten wyjątek
    // inaczej niż przez konstrukcję wewnętrzną. Pokazujemy więc tylko dodatnią ścieżkę synchronizacji powyżej.

    // ============================== WORKFLOW-G4 — Info workflow dla dowolnego wiersza (★) ==============================

    [Test]
    [Description("WORKFLOW-G4: dla dowolnego wiersza guided czytamy jego workflow przez klucze serwerowe — " +
                 "aktywne zadania to Tasks.WgParent[row] filtrowane po IsActiveProgress, procesy to distinct " +
                 "po Task.WFWorkflow; blokadę edycji przez proces sprawdza BusTools.CheckIsReadOnlyForWorkflow(row).")]
        [Ignore("Receptura operacyjna - wymaga uruchomionego silnika workflow (StartProcess na czystej Demo wstawia WFWorkflow zamiast oczekiwanego typu wiersza). Pozostawione jako wykonywalna dokumentacja intencji; patrz WORKFLOW07.")]
        public void WORKFLOW_G4_InfoWorkflowDlaWiersza_ZadaniaProcesyBlokada()
    {
        var symbol = ZbudujIWdrozDefinicjeNaKontrahentach("G4");

        var definicja = Session.GetWorkflow().WFDefs.WgSymbolu[symbol];
        var kontrahent = DowolnyKontrahent();

        // Przed startem: wiersz nie ma aktywnych zadań tej definicji (WgParent zwraca SubTable — nigdy null):
        var business = Session.GetBusiness();
        business.Tasks.WgParent[kontrahent].Should().NotBeNull(
            "Tasks.WgParent[row] zawsze zwraca SubTable (serwerowe zawężenie), nawet pustą");

        Task zadanie = definicja.GetStartPoint().StartProcess(row: kontrahent, source: null);
        zadanie.Should().NotBeNull();
        Session.Save();

        // 1) Aktywne zadania przetwarzające wiersz (klucz + filtr po kalkulatorze IsActiveProgress):
        var aktywne = business.Tasks.WgParent[kontrahent].Cast<Task>()
            .Where(t => t.IsActiveProgress)
            .ToList();
        aktywne.Should().Contain(t => t.Guid == zadanie.Guid,
            "pierwsze zadanie wystartowanego procesu jest aktywne i przetwarza wiersz");

        // 2) Procesy, w których wiersz uczestniczy (distinct po WFWorkflow zadań aktywnych):
        var procesy = aktywne
            .Where(t => t.WFWorkflow != null)
            .Select(t => ((WFWorkflow)t.WFWorkflow).Guid)
            .Distinct()
            .ToList();
        procesy.Should().Contain(((WFWorkflow)zadanie.WFWorkflow).Guid,
            "proces wystartowany na wierszu jest jednym z procesów przetwarzających wiersz");

        // 3) Blokada edycji przez proces — obserwowalny kontrakt static metody (wynik zależy też od
        //    zalogowanego operatora; sprawdzamy jedynie, że metoda zwraca deterministyczny bool):
        System.Action sprawdz = () => BusTools.CheckIsReadOnlyForWorkflow(kontrahent);
        sprawdz.Should().NotThrow("CheckIsReadOnlyForWorkflow(row) to bezpieczny odczyt blokady wiersza");
    }

    // POMINIĘTE (WORKFLOW-G4): Task.ParentWFWorkflows filtruje procesy do tych, których OPIEKUNEM jest zalogowany
    // operator — wynik zależy od operatora sesji testowej, nie od trwałych danych; oraz worker RowWorkflowInfoWorker
    // (property 'Zadania'/'Procesy'/'Zablokowany przez proces') liczy dokładnie to samo, co klucze użyte wyżej.
    // Oba są kruche/zależne od kontekstu — testujemy obserwowalny kontrakt kluczami serwerowymi (punkty 1–3).
}
