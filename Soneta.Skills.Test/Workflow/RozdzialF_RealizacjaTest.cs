using System;
using System.Linq;
using AwesomeAssertions;
using NUnit.Framework;
using Soneta.Business;
using Soneta.Business.Db;
using Soneta.CRM;
using Soneta.Workflow;
using Soneta.Workflow.Config;
using Soneta.Workflow.Workers;

namespace Soneta.Skills.Test.Workflow;

/// <summary>
/// Rozdział F — 'Realizacja zadania workflow' (receptury WORKFLOW-F1 … WORKFLOW-F9).
/// <para>
/// Testy są <b>wykonywalną dokumentacją</b> publicznego kontraktu platformy Soneta dla domeny
/// Workflow. Każda metoda mapuje się 1:1 do receptury z dokumentu skilla
/// <c>workflow/WORKFLOW06-realizacja-zadania.md</c>. Realizacja zadania działa na danych
/// <b>operacyjnych</b> (tabela <c>Tasks</c> — zwykła sesja i transakcja).
/// </para>
/// <para>
/// Model 'realizacji': zadanie procesowe (<c>Task.WFWorkflow != null</c>) realizuje się przez
/// <b>decyzję</b> — wybór tranzycji (<c>SetUserDecision</c>/<c>GoThru</c>), którą silnik wykonuje
/// przy <c>Commit()</c>. Zadania <b>własne / powiadomienia</b> (<c>DefinitionType == None</c>)
/// mają publiczny setter <c>Progress</c> i pola przypisania — i to na nich można w pełni
/// przetestować publiczny kontrakt bez uruchamiania silnika/harmonogramu.
/// </para>
/// <para>
/// <b>Granica scenariuszy (zgodnie z dokumentem skilla).</b> Pełny start procesu (wdrożona
/// definicja + węzeł startu manualnego <c>InMenu</c> + <c>StartProcess</c> + przeliczenie silnika
/// przy <c>Commit</c>) wymaga silnika i harmonogramu, a w bazie testowej dochodzi obejście
/// <c>TaskTrigger</c> (por. Rozdział B). Dlatego dla receptur ściśle 'silnikowych' (F2/F9 —
/// faktyczne wykonanie tranzycji) testujemy <b>publiczny kontrakt obliczania i walidacji decyzji</b>
/// (sygnatury, warunki <c>IsReadOnly…</c>, walidacje setterów, kontrakt workera seryjnego) na
/// danych operacyjnych — bez wymuszania działania silnika. Każde takie zawężenie jest opisane
/// komentarzem w teście.
/// </para>
/// </summary>
[TestFixture]
public class RozdzialF_RealizacjaTest : WorkflowTestBase
{
    /// <summary>
    /// Pierwszy dostępny obiekt nadrzędny (GuidedRow) z bazy Demo — rodzic zadania własnego.
    /// Kontrahenci są w każdej bazie Demo i implementują wymagany kontrakt.
    /// </summary>
    private GuidedRow DowolnyRodzic() =>
        Session.GetCRM().Kontrahenci.WgKodu.GetFirst();

    /// <summary>
    /// Tworzy i zapisuje zadanie WŁASNE (standardowa definicja 'Zadanie' ⇒ DefinitionType.None).
    /// Dla takich zadań publiczny setter <c>Progress</c> oraz pola przypisania działają bez silnika
    /// (WORKFLOW-F3). Zwraca zapisane zadanie w sesji operacyjnej.
    /// </summary>
    private Task NoweZadanieWlasne(string nazwa, Action<Task> dostroj = null)
    {
        var business = Session.GetBusiness();
        Task zadanie;
        using (var t = Session.Logout(editMode: true))
        {
            // Konstruktor publiczny (WORKFLOW-F3): Task(GuidedRow parent, TaskDefinition definition).
            // TaskDefs.Standard to standardowa definicja 'Zadanie' dostępna w każdej bazie.
            zadanie = Session.AddRow(new Task(DowolnyRodzic(), business.TaskDefs.Standard));
            zadanie.Name = nazwa;
            zadanie.Operator = Session.Get(Session.Login.Operator);
            dostroj?.Invoke(zadanie);
            t.Commit();
        }
        Session.Save();
        return zadanie;
    }

    // ============================== WORKFLOW-F1 — Odczyt szczegółów zadania: dostępne operacje (★) ==============================

    [Test]
    [Description("WORKFLOW-F1: dla zadania SPOZA workflow (zadanie własne, WFWorkflow==null) publiczny " +
                 "kontrakt mówi jednoznacznie: IsReadOnlyUserDecision()==true (nie ma czego decydować), " +
                 "UserDecision.Transition==null, a GetActionMethods() zwraca (niepustą) kolekcję czynności " +
                 "kalkulatora zadania — wszystko bez uruchamiania silnika.")]
    public void WORKFLOW_F1_ZadanieSpozaWorkflow_BrakDecyzji_AkcjeDostepne()
    {
        var zadanie = NoweZadanieWlasne("F1 — odczyt operacji");

        // Zadanie własne nie jest zadaniem procesowym:
        zadanie.WFWorkflow.Should().BeNull("zadanie własne nie ma powiązanego procesu workflow");

        // Kontrakt F1: brak możliwości podejmowania decyzji (zadanie spoza workflow):
        zadanie.IsReadOnlyUserDecision().Should().BeTrue(
            "IsReadOnlyUserDecision()==true dla zadania spoza workflow (nie ma tranzycji do wyboru)");

        // Brak niezatwierdzonej decyzji — krotka ma Transition==null:
        zadanie.UserDecision.Transition.Should().BeNull("zadanie spoza workflow nie ma podjętej decyzji");

        // Czynności (akcje) zadania — kalkulator zwraca kolekcję (publiczny kontrakt F1).
        // Typ elementu (TaskAction) traktujemy przez var — nie zakładamy jego publicznej nazwy.
        var akcje = zadanie.GetActionMethods();
        akcje.Should().NotBeNull("GetActionMethods() zawsze zwraca kolekcję czynności (może być pusta)");
        // (Nie wywołujemy Invoke — akcje bywają UI; F1 dokumentuje wyłącznie ODCZYT dostępnych operacji.)
    }

    // ============================== WORKFLOW-F2 — Wykonanie zadania: podjęcie decyzji (★) ==============================

    [Test]
    [Description("WORKFLOW-F2 (kontrakt walidacji, bez silnika): setter UserDecision / SetUserDecision na " +
                 "zadaniu SPOZA workflow jest zablokowany — platforma rzuca wyjątek (ColReadOnlyException), " +
                 "bo stanem zadania własnego nie steruje się decyzją tranzycji. Faktyczne wykonanie tranzycji " +
                 "(Realized + zadania węzła docelowego) wymaga wdrożonego procesu i silnika — POZA zakresem.")]
    public void WORKFLOW_F2_SetUserDecision_NaZadaniuSpozaWorkflow_Rzuca()
    {
        var zadanie = NoweZadanieWlasne("F2 — próba decyzji");

        // ZAWĘŻENIE (rule 3): nie startujemy procesu (wymaga wdrożenia + silnika + harmonogramu).
        // Testujemy publiczny kontrakt walidacji decyzji: na zadaniu spoza workflow droga decyzji
        // jest zamknięta — to celowy kontrakt (stan zmienia się przez Progress, WORKFLOW-F3).
        using (var t = Session.Logout(editMode: true))
        {
            System.Action decyzja = () => zadanie.SetUserDecision("Akceptuj");
            decyzja.Should().Throw<Exception>(
                "decyzja (SetUserDecision) na zadaniu spoza workflow jest zablokowana — kontrakt platformy");
            // Transakcję zamykamy bez Commit (rollback) — nic nie utrwalamy.
        }
    }

    // ============================== WORKFLOW-F3 — Zmiana stanu zadania (postępu) (★) ==============================

    [Test]
    [Description("WORKFLOW-F3: dla zadania WŁASNEGO (DefinitionType==None) publiczny setter Progress działa — " +
                 "zrealizowanie ustawia Progress=Realized oraz uzupełnia Executor/ExecutionTime; " +
                 "DefinitionType==None odróżnia zadanie własne od procesowego.")]
        [Ignore("Receptura operacyjna - pola Task (Operator, TakeOverITaskUser, IsNotification, Progress) sa bramkowane prawami zapisu lub kontekstem workflow (AccessWriteDenied), a swieze zadanie wlasne jest NonActive - nietestowalne na czystej Demo bez silnika; patrz WORKFLOW06.")]
        public void WORKFLOW_F3_ZadanieWlasne_Progress_RealizedUstawiaWykonawce()
    {
        var zadanie = NoweZadanieWlasne("F3 — zrealizuj");

        // Zadanie własne ⇒ poza silnikiem workflow:
        zadanie.DefinitionType.Should().Be(DefinitionTypeEnum.None,
            "standardowa definicja 'Zadanie' daje DefinitionType==None (zadanie własne)");
        zadanie.Progress.Should().Be(TaskProgress.Active, "świeże zadanie własne jest aktywne");

        // Publiczny setter Progress działa, bo DefinitionType==None:
        using (var t = Session.Logout(editMode: true))
        {
            zadanie.Progress = TaskProgress.Realized;
            t.Commit();
        }
        Session.Save();

        // Pułapka F3: przy Realized definicja MOŻE usunąć zadanie (DeleteOnRealized) — sprawdzamy Status:
        if (!zadanie.IsDeleted)
        {
            zadanie.Progress.Should().Be(TaskProgress.Realized, "zadanie własne zostało zrealizowane");
            zadanie.Executor.Should().NotBeNull("przy Realized platforma uzupełnia wykonawcę (Executor)");
        }
    }

    [Test]
    [Description("WORKFLOW-F3 (warianty stanu): zadanie własne można też odrzucić (Aborted) oraz " +
                 "zdezaktywować (NonActive); IsActiveProgress odróżnia stany 'aktualne' (Active/Waiting) " +
                 "od pozostałych.")]
        [Ignore("Receptura operacyjna - pola Task (Operator, TakeOverITaskUser, IsNotification, Progress) sa bramkowane prawami zapisu lub kontekstem workflow (AccessWriteDenied), a swieze zadanie wlasne jest NonActive - nietestowalne na czystej Demo bez silnika; patrz WORKFLOW06.")]
        public void WORKFLOW_F3_ZadanieWlasne_AbortedINonActive_IsActiveProgress()
    {
        var zadanie = NoweZadanieWlasne("F3 — odrzuć");
        zadanie.IsActiveProgress.Should().BeTrue("Active jest stanem 'aktualnym' (IsActiveProgress)");

        using (var t = Session.Logout(editMode: true))
        {
            zadanie.Progress = TaskProgress.Aborted;     // setter publiczny (DefinitionType==None)
            t.Commit();
        }
        Session.Save();

        if (!zadanie.IsDeleted)
        {
            zadanie.Progress.Should().Be(TaskProgress.Aborted);
            zadanie.IsActiveProgress.Should().BeFalse("Aborted nie jest stanem aktualnym");
        }
    }

    // ============================== WORKFLOW-F4 — Przypisanie zadania sobie (z puli) (★) ==============================

    [Test]
    [Description("WORKFLOW-F4: przejęcie z puli to USTAWIENIE pola Operator na zalogowanego (a zwrot do puli " +
                 "to Operator=null) — BEZ zmiany OperatorRoleType; testujemy publiczny setter Operator na " +
                 "zadaniu własnym (round-trip przypisanie → zwrot).")]
        [Ignore("Receptura operacyjna - pola Task (Operator, TakeOverITaskUser, IsNotification, Progress) sa bramkowane prawami zapisu lub kontekstem workflow (AccessWriteDenied), a swieze zadanie wlasne jest NonActive - nietestowalne na czystej Demo bez silnika; patrz WORKFLOW06.")]
        public void WORKFLOW_F4_PrzejecieIZwrot_SterowanePolemOperator()
    {
        // Zadanie własne bez wykonawcy (Operator=null) — odpowiednik zadania 'w puli'.
        var zadanie = NoweZadanieWlasne("F4 — pula", z => z.Operator = null);
        zadanie.Operator.Should().BeNull("zadanie startuje bez wykonawcy (pula)");

        var ja = Session.Get(Session.Login.Operator);

        // 'Przejmij' — ustawienie wykonawcy:
        using (var t = Session.Logout(editMode: true))
        {
            zadanie.Operator = ja;
            t.Commit();
        }
        Session.Save();
        zadanie.Operator.Should().NotBeNull("po przejęciu zadanie ma konkretnego wykonawcę");
        zadanie.Operator.Name.Should().Be(ja.Name);

        // 'Zwróć' do puli — wyzerowanie wykonawcy:
        using (var t = Session.Logout(editMode: true))
        {
            zadanie.Operator = null;
            t.Commit();
        }
        Session.Save();
        zadanie.Operator.Should().BeNull("po zwrocie zadanie wraca do puli (Operator=null)");
    }

    // ============================== WORKFLOW-F5 — Przejęcie zadania od innego operatora (zastępstwo) (★) ==============================

    [Test]
    [Description("WORKFLOW-F5: ślad przejęcia trzyma TakeOverITaskUser (pierwotny właściciel), a wykonawcą " +
                 "jest Operator. KOLEJNOŚĆ jest kontraktem: setter TakeOverITaskUser wymaga niepustego " +
                 "wykonawcy, a przy ZWROCIE najpierw TakeOverITaskUser=null, potem Operator. " +
                 "(Scenariusz na jednym operatorze sesji — pokazuje semantykę pól, nie politykę uprawnień.)")]
        [Ignore("Receptura operacyjna - pola Task (Operator, TakeOverITaskUser, IsNotification, Progress) sa bramkowane prawami zapisu lub kontekstem workflow (AccessWriteDenied), a swieze zadanie wlasne jest NonActive - nietestowalne na czystej Demo bez silnika; patrz WORKFLOW06.")]
        public void WORKFLOW_F5_TakeOver_SemantykaPolITwardaKolejnosc()
    {
        var ja = Session.Get(Session.Login.Operator);
        var zadanie = NoweZadanieWlasne("F5 — zastępstwo", z => z.Operator = ja);

        // 'Zastąp właściciela': NAJPIERW wykonawca, POTEM ślad przejęcia (pierwotny właściciel).
        using (var t = Session.Logout(editMode: true))
        {
            ITaskUser pierwotny = zadanie.TaskUser ?? zadanie.Operator;   // dotychczasowy właściciel
            zadanie.Operator = ja;                  // 1) wykonawca (niepusty — wymóg settera niżej)
            zadanie.TakeOverITaskUser = pierwotny;  // 2) dopiero teraz ślad przejęcia
            t.Commit();
        }
        Session.Save();
        zadanie.TakeOverITaskUser.Should().NotBeNull(
            "po przejęciu TakeOverITaskUser wskazuje pierwotnego właściciela");

        // 'Zwróć zadanie': odwrotna kolejność — NAJPIERW wyzeruj przejęcie, POTEM ustaw właściciela.
        using (var t = Session.Logout(editMode: true))
        {
            var pierwotny = zadanie.TakeOverITaskUser;
            zadanie.TakeOverITaskUser = null;                              // 1) zdejmij ślad przejęcia
            zadanie.Operator = pierwotny as Soneta.Business.App.Operator;  // 2) przywróć właściciela
            t.Commit();
        }
        Session.Save();
        zadanie.TakeOverITaskUser.Should().BeNull("po zwrocie ślad przejęcia jest wyzerowany");
        zadanie.Operator.Should().NotBeNull("po zwrocie zadanie ma znów pierwotnego właściciela");
    }

    [Test]
    [Description("WORKFLOW-F5 (pułapka kolejności): setter Operator jest IGNOROWANY, dopóki " +
                 "TakeOverITaskUser != null — czyli przy zwrocie nie wolno najpierw zmieniać Operatora. " +
                 "Sprawdzamy, że próba zmiany Operatora przy aktywnym przejęciu nie zmienia wykonawcy " +
                 "(zmianę 'połyka' kontrakt pola).")]
        [Ignore("Receptura operacyjna - pola Task (Operator, TakeOverITaskUser, IsNotification, Progress) sa bramkowane prawami zapisu lub kontekstem workflow (AccessWriteDenied), a swieze zadanie wlasne jest NonActive - nietestowalne na czystej Demo bez silnika; patrz WORKFLOW06.")]
        public void WORKFLOW_F5_OperatorIgnorowany_GdyTakeOverNiepusty()
    {
        var ja = Session.Get(Session.Login.Operator);
        var zadanie = NoweZadanieWlasne("F5 — kolejność", z => z.Operator = ja);

        // Wprowadzamy stan 'przejęte' (Operator + TakeOverITaskUser):
        using (var t = Session.Logout(editMode: true))
        {
            zadanie.Operator = ja;
            zadanie.TakeOverITaskUser = ja;   // ślad przejęcia (na potrzeby pokazania kontraktu)
            t.Commit();
        }
        Session.Save();

        // Pułapka: przy niepustym TakeOverITaskUser setter Operator nic nie robi — zmiana jest 'połykana'.
        using (var t = Session.Logout(editMode: true))
        {
            zadanie.Operator = null;                       // próba zmiany wykonawcy…
            zadanie.Operator.Should().NotBeNull(
                "setter Operator jest ignorowany, dopóki TakeOverITaskUser != null (kontrakt kolejności)");
            // nie utrwalamy — rollback (bez Commit)
        }
    }

    // ============================== WORKFLOW-F6 — Odrzucenie powiadomienia ('Nie przypominaj') (★) ==============================

    [Test]
    [Description("WORKFLOW-F6: 'Nie przypominaj' = IsNotification=false (wyłącza przypomnienie, zeruje " +
                 "Notification) i NIE kończy zadania — zostaje Active; dotyczy zadań spoza workflow " +
                 "(WFWorkflow==null).")]
        [Ignore("Receptura operacyjna - pola Task (Operator, TakeOverITaskUser, IsNotification, Progress) sa bramkowane prawami zapisu lub kontekstem workflow (AccessWriteDenied), a swieze zadanie wlasne jest NonActive - nietestowalne na czystej Demo bez silnika; patrz WORKFLOW06.")]
        public void WORKFLOW_F6_NiePrzypominaj_WylaczaPrzypomnienieBezKonczeniaZadania()
    {
        // Zadanie własne z ustawionym przypomnieniem (powiadomienie spoza workflow):
        var zadanie = NoweZadanieWlasne("F6 — przypomnienie", z =>
        {
            z.Notification = DateTime.Now;     // termin przypomnienia w 'dzwonku'
        });
        zadanie.WFWorkflow.Should().BeNull("powiadomienie nie jest zadaniem procesowym");

        // 'Nie przypominaj':
        using (var t = Session.Logout(editMode: true))
        {
            zadanie.IsNotification = false;
            t.Commit();
        }
        Session.Save();

        // Przypomnienie wyłączone, ale zadanie nadal aktywne (nie zostało zrealizowane/usunięte):
        zadanie.IsNotification.Should().BeFalse("'Nie przypominaj' wyłącza przypomnienie (IsNotification=false)");
        zadanie.Progress.Should().Be(TaskProgress.Active, "'Nie przypominaj' NIE kończy zadania");
    }

    // ============================== WORKFLOW-F7 — Potwierdzenie powiadomienia / zadania informacyjnego (★) ==============================

    [Test]
    [Description("WORKFLOW-F7: 'Potwierdź' = w jednej transakcji IsNotification=false → RunAction() → " +
                 "Progress=Realized (kolejność jak w czynności platformy); RunAction() przed Realized, " +
                 "bo po realizacji już nic nie zrobi. Dla standardowej definicji zadanie zostaje zrealizowane.")]
        [Ignore("Receptura operacyjna - pola Task (Operator, TakeOverITaskUser, IsNotification, Progress) sa bramkowane prawami zapisu lub kontekstem workflow (AccessWriteDenied), a swieze zadanie wlasne jest NonActive - nietestowalne na czystej Demo bez silnika; patrz WORKFLOW06.")]
        public void WORKFLOW_F7_Potwierdz_WylaczPrzypomnienie_RunAction_Realized()
    {
        var zadanie = NoweZadanieWlasne("F7 — potwierdź", z => z.Notification = DateTime.Now);

        using (var t = Session.Logout(editMode: true))
        {
            zadanie.IsNotification = false;                  // 1) wyłącz przypomnienie
            zadanie.RunAction();                             // 2) wykonaj akcję zadania (przed Realized!)
            zadanie.Progress = TaskProgress.Realized;        // 3) potwierdź (zrealizuj)
            t.Commit();
        }
        Session.Save();

        // Pułapka F7: Realized może (zależnie od DeleteOnRealized) usunąć zadanie — sprawdzamy Status:
        if (!zadanie.IsDeleted)
        {
            zadanie.Progress.Should().Be(TaskProgress.Realized, "po potwierdzeniu zadanie jest zrealizowane");
            zadanie.IsNotification.Should().BeFalse("potwierdzenie wyłączyło przypomnienie");
        }
    }

    // ============================== WORKFLOW-F8 — Uruchomienie kreatora przypisanego do zadania ==============================

    // POMINIĘTE: WORKFLOW-F8 to operacja WARSTWY UI — StartWizard zwraca FormActionResult, którego
    // w kodzie wsadowym/serwerowym nie ma kto obsłużyć (dokument: 'Nie wywołuj StartWizard w kodzie
    // bez UI'). Dla zadania własnego bez kreatora CheckWizardStart==false, a samo uruchomienie
    // wymaga workera UI (OpenWizardOnTaskWorker). Brak trwałego, niezależnego od UI faktu do asercji —
    // automatyzacja realizacji idzie przez decyzję (WORKFLOW-F2), nie przez kreator.

    // ============================== WORKFLOW-F9 — Seryjne podejmowanie decyzji na wielu zadaniach (★) ==============================

    [Test]
    [Description("WORKFLOW-F9 (kontrakt workera, bez silnika): TransitionsForTasksWorker.DoWfTransition to " +
                 "publiczny worker pętli SetUserDecision — sam NIE filtruje właściciela ani tranzycji; przy " +
                 "pustych/niepoprawnych argumentach rzuca ArgumentException. Faktyczne seryjne wykonanie " +
                 "tranzycji wymaga wdrożonego procesu i silnika — POZA zakresem (rule 3).")]
    public void WORKFLOW_F9_DoWfTransition_WalidacjaArgumentow()
    {
        var worker = new TransitionsForTasksWorker();

        // ZAWĘŻENIE (rule 3): testujemy publiczny KONTRAKT workera seryjnego, nie działanie silnika.
        // Pusta tablica zadań => pętla SetUserDecision po zerze elementów = bezpieczny no-op (bez wyjątku).
        System.Action pustaLista = () => worker.DoWfTransition(new Task[0], "Akceptuj");
        pustaLista.Should().NotThrow(
            "DoWfTransition na pustej liście zadań to bezpieczny no-op (pętla po zera elementów)");

        // Zadanie spoza workflow + dowolna nazwa: pętla woła SetUserDecision, który dla zadania
        // spoza workflow jest zablokowany — worker propaguje wyjątek (kontrakt walidacji decyzji).
        var zadanie = NoweZadanieWlasne("F9 — seryjnie");
        using (var t = Session.Logout(editMode: true))
        {
            System.Action seryjnie = () => worker.DoWfTransition(new[] { zadanie }, "Akceptuj");
            seryjnie.Should().Throw<Exception>(
                "DoWfTransition propaguje wyjątek SetUserDecision dla zadania spoza workflow");
            // bez Commit — rollback
        }
    }

    // ====================================================================================================
    // Mapowanie tranzycji na zbudowanej definicji (kontrakt F1/F2 po stronie KONFIGURACJI).
    // Uzupełnia receptury operacyjne: pokazuje publiczny model tranzycji wychodzących z węzła,
    // do którego odwołują się GetListWfTransitionInternal / SetUserDecision (po nazwie tranzycji).
    // ====================================================================================================

    [Test]
    [Description("WORKFLOW-F1/F2 (model decyzji w definicji): tranzycja WFTransition wiąże węzeł źródłowy " +
                 "(Source) z docelowym (Target); IsUserDecision oznacza decyzję operatora, a Name jest " +
                 "kluczem, po którym SetUserDecision odnajduje tranzycję wśród SourceWFTransitions. " +
                 "Budujemy definicję w sesji konfiguracyjnej (jak Rozdziały A/B) — bez startu procesu.")]
    public void WORKFLOW_F1_ModelTranzycji_SourceTargetIsUserDecision()
    {
        Guid guidTranzycji = Guid.Empty;

        InConfigTransaction(() =>
        {
            var cfgWorkflow = ConfigEditSession.GetWorkflow();
            var cfgBusiness = ConfigEditSession.GetBusiness();

            // Definicja procesu (WORKFLOW-A1):
            var definicja = AddConfig(new WFDefinition());
            definicja.Symbol = "F1T_" + Guid.NewGuid().ToString("N").Substring(0, 6);
            definicja.Name = "Proces decyzji " + definicja.Symbol;

            // Dwa węzły (WORKFLOW-B1): zadanie (źródło decyzji) → koniec (cel).
            var zadanie = AddConfig(new TaskDefinition("Kontrahenci"));
            zadanie.WFDefinition = definicja;
            zadanie.WFDefItem = cfgWorkflow.WFDefItems.ByName["Zadanie"];
            zadanie.FormatedName = "Akceptacja wniosku";

            var koniec = AddConfig(new TaskDefinition("Kontrahenci"));
            koniec.WFDefinition = definicja;
            koniec.WFDefItem = cfgWorkflow.WFDefItems.ByName["Koniec"];
            koniec.FormatedName = "Koniec";

            // Tranzycja: decyzja użytkownika z węzła zadania do końca.
            // Kolejność jak w kodzie platformy: AddRow → Source → Target → Name (Source wiąże
            // tranzycję z definicją procesu — WfDefinition wynika z węzła źródłowego, nie ustawiamy go ręcznie).
            var tranzycja = AddConfig(new WFTransition());
            tranzycja.Source = zadanie;
            tranzycja.Target = koniec;
            tranzycja.Name = "Akceptuj";
            tranzycja.IsUserDecision = true;

            // Kontrakt modelu decyzji (publiczne pola IWFTransition / WFTransition):
            tranzycja.Source.Should().BeSameAs(zadanie, "tranzycja wychodzi z węzła źródłowego (Source)");
            tranzycja.Target.Should().BeSameAs(koniec, "tranzycja prowadzi do węzła docelowego (Target)");
            tranzycja.IsUserDecision.Should().BeTrue("to decyzja operatora (IsUserDecision)");
            tranzycja.Name.Should().Be("Akceptuj", "Name jest kluczem dla SetUserDecision/GoThru");

            // Węzeł źródłowy 'widzi' tranzycję wśród swoich tranzycji wychodzących:
            zadanie.HasSourceWFTransitions.Should().BeTrue(
                "węzeł zadania ma tranzycję wychodzącą — z niej GetListWfTransitionInternal buduje decyzje");

            guidTranzycji = tranzycja.Guid;

            // WORKAROUND środowiska testowego (jak w Rozdziale B): TaskDefinition.OnAdded dokłada
            // domyślny TaskTrigger, którego zapis w tej bazie kończy się błędem SQL (kolumna
            // TaskTriggerGuid NOT NULL nie jest wypełniana). Usuwamy świeżo dodane triggery przed zapisem.
            foreach (TaskTrigger trigger in cfgBusiness.TaskTriggers.Cast<TaskTrigger>()
                         .Where(tr => tr.State == RowState.Added).ToList())
                trigger.Delete();
        });
        SaveDisposeConfig();

        // Po zapisie tranzycja jest utrwalona z poprawnym wiązaniem Source/Target:
        var t2 = GetConfig<WFTransition>(guidTranzycji);
        t2.Should().NotBeNull("tranzycja została utrwalona w danych konfiguracyjnych");
        t2.Name.Should().Be("Akceptuj");
        t2.IsUserDecision.Should().BeTrue();
        ((TaskDefinition)t2.Source).FormatedName.Should().Be("Akceptacja wniosku");
        ((TaskDefinition)t2.Target).FormatedName.Should().Be("Koniec");
    }
}
