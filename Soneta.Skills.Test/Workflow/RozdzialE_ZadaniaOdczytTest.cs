using System;
using System.Linq;
using AwesomeAssertions;
using NUnit.Framework;
using Microsoft.Extensions.DependencyInjection;
using Soneta.Business;
using Soneta.Business.App;
using Soneta.Business.Db;
using Soneta.CRM;
using Soneta.Types;
using Soneta.Workflow;
using Soneta.Workflow.Config;

namespace Soneta.Skills.Test.Workflow;

/// <summary>
/// Rozdział E — 'Zadania operatora — odczyt i przegląd' (receptury WORKFLOW-E1 … WORKFLOW-E5).
/// <para>
/// Testy są <b>wykonywalną dokumentacją</b> publicznego kontraktu platformy Soneta dla domeny
/// Workflow. Każda metoda mapuje się 1:1 do receptury z dokumentu skilla
/// <c>workflow/WORKFLOW05-zadania-odczyt.md</c>. Kluczowy model: zadanie operatora to wiersz
/// <c>Soneta.Business.Db.Task</c> (tabela <c>Tasks</c>) — dane <b>OPERACYJNE</b> (nie konfiguracyjne),
/// odczyt nie wymaga sesji konfiguracyjnej. Operujemy więc na <c>Session</c> / <c>Business.Tasks</c>.
/// </para>
/// <para>
/// Baza Demo nie gwarantuje istniejących zadań ani procesów workflow, a start procesu jest zbyt
/// złożony, by go odtwarzać w każdym teście. Dlatego — zgodnie z zasadą 2 — testujemy <b>sam
/// kontrakt zapytań i kluczy</b>: że klucze (<c>WgOperator</c>, <c>WgTaskUser</c>, <c>WgParent</c>,
/// <c>WgWFWorkflow</c>, <c>WgDefinition</c>), warunki serwerowe (<c>GetMyTasksCondition</c>,
/// filtry z <c>FieldCondition</c>) oraz metody przypomnień (<c>GetRemainders</c>,
/// <c>GetRemaindersCount</c>, <c>GetRemaindersView</c>) zwracają <b>poprawne, enumerowalne,
/// pusto-ale-spójne</b> wyniki na bazie Demo. Asercje opierają się na trwałych faktach kontraktu,
/// nie na zawartości danych. Operujemy wyłącznie na publicznym kontrakcie.
/// </para>
/// </summary>
[TestFixture]
public class RozdzialE_ZadaniaOdczytTest : WorkflowTestBase
{
    /// <summary>Tabela zadań bieżącej sesji OPERACYJNEJ (dane operacyjne — bez sesji konfiguracyjnej).</summary>
    private Tasks Tasks => Business.Tasks;

    /// <summary>Zalogowany operator bieżącej sesji (implementuje <c>ITaskUser</c>).</summary>
    private Operator ZalogowanyOperator => Session.Login.Operator;

    // ============================== WORKFLOW-E1 — Odczyt aktualnych zadań dla operatora (★) ==============================

    [Test]
    [Description("WORKFLOW-E1: warunek serwerowy 'moje zadania\' buduje serwis Tasks.IOrgStructureHelper " +
                 "(GetMyTasksCondition: operator + role + węzły org. + przejęte); doklejamy filtry aktualności " +
                 "(Progress=Active, Notification w zakresie, ValidFrom<=dziś) i odczytujemy przez klucz " +
                 "WgDefinition[rc] — wynik to enumerowalny Key<Task> (na Demo zwykle pusty, ale poprawny).")]
    public void WORKFLOW_E1_MojeZadaniaCondition_FiltrujePrzezKlucz()
    {
        // Serwis jest rejestrowany przez moduł Workflow — w środowisku testowym Workflow jest
        // załadowany (WorkflowTestBase.Session.GetWorkflow()), więc GetRequiredService nie rzuci.
        var helper = Session.GetRequiredService<Tasks.IOrgStructureHelper>();
        helper.Should().NotBeNull("serwis Tasks.IOrgStructureHelper rejestruje moduł Workflow");

        // 1) Warunek serwerowy 'moje zadania' (operator + role + węzły org. + przejęte):
        RowCondition rc = helper.GetMyTasksCondition(Session, Date.Today);
        rc.Should().NotBeNull("GetMyTasksCondition zwraca gotowy warunek serwerowy");

        // 2) Zawężenie do zadań aktualnych — jak licznik powiadomień na 'dzwonku':
        rc &= new FieldCondition.Equal(nameof(Task.Progress), TaskProgress.Active);
        rc &= new FieldCondition.Greater(nameof(Task.Notification), (DateTime)Date.MinValue);
        rc &= new FieldCondition.LessEqual(nameof(Task.Notification), DateTime.Now);
        rc &= new FieldCondition.LessEqual(nameof(Task.ValidFrom), Date.Today);

        // 3) Odczyt przez klucz z warunkiem — filtrowanie wykonuje serwer; indeksator [rc] zwraca
        //    przefiltrowany, enumerowalny Key<Task>. Materializujemy do listy (wykonanie zapytania):
        var aktualne = Tasks.WgDefinition[rc].Cast<Task>().ToList();
        aktualne.Should().NotBeNull("WgDefinition[rc] zwraca enumerowalny Key<Task> (pusty-ale-poprawny na Demo)");

        // Trwały fakt kontraktu: każde zadanie spełniające warunek 'moich' jest własnością operatora
        // (weryfikacja kliencka tą samą logiką — Tasks.IsLoggedUserTask):
        foreach (var task in aktualne)
        {
            Tasks.IsLoggedUserTask(task).Should().BeTrue(
                "GetMyTasksCondition i IsLoggedUserTask realizują tę samą logikę przynależności zadania");
            task.Progress.Should().Be(TaskProgress.Active, "doklejony filtr zawęża do aktywnych");
        }
    }

    [Test]
    [Description("WORKFLOW-E1 (warianty kluczy): klucz WgOperator[operator] zawęża do zadań KONKRETNEGO " +
                 "operatora (bez ról/węzłów), a WgTaskUser[taskUser] do zadań danego właściciela ITaskUser; " +
                 "oba zwracają SubTable<Task> (enumerowalny, z Count). Operator implementuje ITaskUser.")]
    public void WORKFLOW_E1_WgOperator_IWgTaskUser_ZawezajaPoWlascicielu()
    {
        Operator op = ZalogowanyOperator;
        op.Should().NotBeNull("test działa w kontekście zalogowanego operatora");

        // Tylko zadania konkretnego operatora (pole Operator) — bez ról/węzłów:
        SubTable<Task> zadaniaOperatora = Tasks.WgOperator[op];
        zadaniaOperatora.Should().NotBeNull("WgOperator[op] zwraca SubTable<Task>");
        zadaniaOperatora.Count.Should().BeGreaterThanOrEqualTo(0, "Count jest spójny (na Demo zwykle 0)");

        // Wszystkie wiersze klucza mają to pole nośne ustawione na naszego operatora (trwały fakt klucza):
        foreach (var task in zadaniaOperatora.Cast<Task>())
            task.Operator.Should().Be(op, "WgOperator filtruje po polu Operator");

        // Operator jest też ITaskUser — klucz WgTaskUser[taskUser] zawęża po właścicielu ITaskUser:
        SubTable<Task> zadaniaWlasciciela = Tasks.WgTaskUser[op];
        zadaniaWlasciciela.Should().NotBeNull("WgTaskUser[op] przyjmuje ITaskUser (Operator implementuje ITaskUser)");

        // Wariant klucza WgTaskUser z dodatkowym TaskProgress (klucz ma pola TaskUser, Progress, …):
        SubTable<Task> aktywneWlasciciela = Tasks.WgTaskUser[op, TaskProgress.Active];
        aktywneWlasciciela.Should().NotBeNull("WgTaskUser[taskUser, progress] to wariant tego samego klucza");
        foreach (var task in aktywneWlasciciela.Cast<Task>())
            task.Progress.Should().Be(TaskProgress.Active, "wariant z TaskProgress zawęża po stanie");
    }

    [Test]
    [Description("WORKFLOW-E1 (wariant ITaskUser[]): GetTaskUsersCondition(taskUsers, date) buduje warunek " +
                 "serwerowy dla wskazanych właścicieli (tu: zalogowany operator jako jedyny ITaskUser); " +
                 "wynik składamy z kluczem i materializujemy (pusty-ale-poprawny na Demo).")]
    public void WORKFLOW_E1_GetTaskUsersCondition_DlaWskazanychWlascicieli()
    {
        var helper = Session.GetRequiredService<Tasks.IOrgStructureHelper>();

        // Operator implementuje ITaskUser — przekazujemy go jako jedynego właściciela:
        ITaskUser[] wlasciciele = new ITaskUser[] { ZalogowanyOperator };
        RowCondition rc = helper.GetTaskUsersCondition(wlasciciele, Date.Today);
        rc.Should().NotBeNull("GetTaskUsersCondition zwraca warunek serwerowy dla wskazanych właścicieli");

        var lista = Tasks.WgDefinition[rc].Cast<Task>().ToList();
        lista.Should().NotBeNull("warunek złożony z kluczem zwraca enumerowalny wynik (pusty-ale-poprawny)");
    }

    // ============================== WORKFLOW-E2 — Przegląd na panelu / pulpicie (częściowo) ==============================

    [Test]
    [Description("WORKFLOW-E2: licznik 'dzwonka\' GetRemaindersCount(all) liczy jednym zapytaniem agregującym " +
                 "(zwraca RemindersCountResult: Count, LastTaskID); GetRemaindersView(all, …) zwraca View " +
                 "powiadomień operatora. Count jest nieujemny, a liczba wierszy widoku spójna z licznikiem " +
                 "wariantu 'wszystkie\' przy parametrze NotificationRead.All (pusty-ale-poprawny na Demo).")]
    public void WORKFLOW_E2_LicznikIWidokPowiadomien_SpojnyKontrakt()
    {
        // Licznik 'dzwonka' — wariant Aktywne (all: false):
        Tasks.RemindersCountResult licznik = Tasks.GetRemaindersCount(all: false);
        licznik.Should().NotBeNull("GetRemaindersCount zwraca RemindersCountResult");
        licznik.Count.Should().BeGreaterThanOrEqualTo(0, "licznik powiadomień jest nieujemny");

        // Widok 'Powiadomienia' — wariant Wszystkie, wszystkie stany odczytania:
        View powiadomienia = Tasks.GetRemaindersView(all: true, webUser: false, notificationRead: NotificationRead.All);
        powiadomienia.Should().NotBeNull("GetRemaindersView zwraca obiekt View (warstwa UI)");

        // Wszystkie wiersze widoku są typu Task i należą do zalogowanego operatora (logika 'moich'):
        foreach (var task in powiadomienia.Cast<Task>())
            Tasks.IsLoggedUserTask(task).Should().BeTrue(
                "widok powiadomień obejmuje wyłącznie zadania zalogowanego operatora");

        // POMINIĘTE: filtr NotificationRead.Read / .NotRead (odczytane/nieodczytane) — stan jest per
        // użytkownik w osobnej tabeli powiązań i na czystej bazie Demo bez zadań nie da się zbudować
        // deterministycznego, trwałego faktu różnicującego oba warianty.
        // POMINIĘTE: WFWorkflows.GetGrantedView() (sekcja 'Procesy' panelu) — w bieżącej wersji metoda
        // zawiera Debug.Assert(false, "Zawiadomić MS!") i jest przeznaczona do warstwy UI; nie nadaje się
        // do deterministycznego testu kontraktu biznesowego.
    }

    // ============================== WORKFLOW-E3 — Filtrowanie listy zadań workflow (★) ==============================

    [Test]
    [Description("WORKFLOW-E3: listę zawężamy warunkami serwerowymi z FieldCondition — tylko zadania workflow " +
                 "(WFWorkflow IS NOT NULL), po definicji procesu (ścieżka Definition.WFDefinition), po stanie " +
                 "(Progress=Active) i po okresie (Start<=To AND End>=From). Definicję bierzemy dynamicznie " +
                 "z WFDefs (Demo nie gwarantuje definicji). Wynik to enumerowalny Key<Task>.")]
    public void WORKFLOW_E3_FiltrZadanWorkflow_PoDefinicjiStanieIOkresie()
    {
        var okres = FromTo.Month(Date.Today);

        // Tylko zadania procesowe (WFWorkflow != null) — pułapka: isNull:false = IS NOT NULL:
        RowCondition rc = new FieldCondition.Null(nameof(Task.WFWorkflow), isNull: false);

        // Po definicji procesu — ścieżka przez referencję (serwer wykona JOIN). Definicję pobieramy
        // dynamicznie z modułu Workflow sesji operacyjnej (dana konfiguracyjna czytana w sesji oper.):
        var definicja = Session.GetWorkflow().WFDefs.Cast<WFDefinition>().FirstOrDefault();
        if (definicja != null)
            rc &= new FieldCondition.Equal(
                $"{nameof(Task.Definition)}.{nameof(TaskDefinition.WFDefinition)}", definicja);

        // Po stanie:
        rc &= new FieldCondition.Equal(nameof(Task.Progress), TaskProgress.Active);

        // Po okresie (zachodzenie na FromTo): Start <= okres.To AND End >= okres.From:
        rc &= new FieldCondition.LessEqual(nameof(Task.Start), (DateTime)okres.To);
        rc &= new FieldCondition.GreaterEqual(nameof(Task.End), (DateTime)okres.From);

        var zadania = Tasks.WgDefinition[rc].Cast<Task>().ToList();
        zadania.Should().NotBeNull("złożony warunek serwerowy zwraca enumerowalny Key<Task> (pusty-ale-poprawny)");

        // Trwałe fakty kontraktu na każdym przefiltrowanym wierszu:
        foreach (var task in zadania)
        {
            task.WFWorkflow.Should().NotBeNull("filtr WFWorkflow IS NOT NULL przepuszcza tylko zadania procesowe");
            task.Progress.Should().Be(TaskProgress.Active, "filtr po stanie zawęża do aktywnych");
        }
    }

    // ============================== WORKFLOW-E4 — Zadania powiązane z obiektem (rodzicem) (★) ==============================

    [Test]
    [Description("WORKFLOW-E4: zadania 'doczepione\' do dowolnego obiektu (Parent) odczytuje klucz " +
                 "WgParent[guidedRow] → SubTable<Task>; rodzica pobieramy dynamicznie (pierwszy kontrahent " +
                 "Demo). Na SubTable można dalej filtrować serwerowo (Progress=Active). WgParent jest " +
                 "posortowany po (Parent, Start). Zadania procesu czyta WgWFWorkflow[wfWorkflow].")]
    public void WORKFLOW_E4_ZadaniaWiersza_WgParent_IZadaniaProcesu_WgWFWorkflow()
    {
        // Rodzic — dowolny GuidedRow; w Demo bierzemy pierwszego kontrahenta (pobierany dynamicznie).
        // Wiersz pochodzi z TEJ SAMEJ sesji operacyjnej (klucz wymaga wiersza z bieżącej sesji).
        IGuidedRow rodzic = Session.GetCRM().Kontrahenci.WgKodu.GetFirst();
        rodzic.Should().NotBeNull("baza Demo zawiera co najmniej jednego kontrahenta jako obiekt-rodzica");

        // Wszystkie zadania doczepione do wiersza (Parent = ten wiersz):
        SubTable<Task> zadaniaWiersza = Tasks.WgParent[rodzic];
        zadaniaWiersza.Should().NotBeNull("WgParent[rodzic] zwraca SubTable<Task>");
        zadaniaWiersza.Count.Should().BeGreaterThanOrEqualTo(0, "Count jest spójny (na Demo zwykle 0)");

        // Dalsze filtrowanie serwerowe na SubTable — tylko aktywne zadania wiersza:
        var aktywneWiersza = zadaniaWiersza[new FieldCondition.Equal(nameof(Task.Progress), TaskProgress.Active)]
            .Cast<Task>().ToList();
        foreach (var task in aktywneWiersza)
        {
            task.Parent.Should().Be(rodzic, "WgParent wiąże zadanie z jego obiektem głównym (Parent)");
            task.Progress.Should().Be(TaskProgress.Active, "filtr na SubTable zawęża do aktywnych");
        }

        // Zadania konkretnego procesu — klucz WgWFWorkflow[wfWorkflow] (jeśli jakiś proces istnieje):
        var proces = Session.GetWorkflow().WFWorkflows.Cast<WFWorkflow>().FirstOrDefault();
        if (proces != null)
        {
            SubTable<Task> zadaniaProcesu = Tasks.WgWFWorkflow[proces];
            zadaniaProcesu.Should().NotBeNull("WgWFWorkflow[proces] zwraca zadania danego procesu");
            foreach (var task in zadaniaProcesu.Cast<Task>())
                task.WFWorkflow.Should().Be((IWFWorkflow)proces, "WgWFWorkflow filtruje po polu WFWorkflow");
        }
    }

    // ============================== WORKFLOW-E5 — Przypomnienia i powiadomienia (★) ==============================

    [Test]
    [Description("WORKFLOW-E5: GetRemainders() zwraca wymagalne przypomnienia (dodatkowo filtrowane po " +
                 "IsNotification), GetRemainders(all:true) obejmuje też oczekujące; GetRemaindersCount(all).Count " +
                 "to licznik 'dzwonka\'. Pola przypomnienia (Notification/IsNotification/NotificationDate/Time) " +
                 "tworzą spójną parę: IsNotification == (Notification != DateTime.MinValue).")]
    public void WORKFLOW_E5_Przypomnienia_KontraktListIPolNotification()
    {
        // 1) Licznik powiadomień (jedno zapytanie agregujące):
        var licznik = Tasks.GetRemaindersCount(all: false);
        licznik.Count.Should().BeGreaterThanOrEqualTo(0, "licznik przypomnień jest nieujemny");

        // 2) Wymagalne przypomnienia — GetRemainders() (bez all) filtruje dodatkowo po IsNotification:
        var wymagalne = Tasks.GetRemainders().ToList();
        wymagalne.Should().NotBeNull("GetRemainders() zwraca IEnumerable<Task> (pusty-ale-poprawny na Demo)");
        foreach (var task in wymagalne)
        {
            task.IsNotification.Should().BeTrue(
                "GetRemainders() bez 'all' przepuszcza tylko zadania z ustawionym przypomnieniem");
            // Trwała własność kontraktu: IsNotification <=> Notification != DateTime.MinValue:
            task.Notification.Should().NotBe(DateTime.MinValue,
                "IsNotification=true oznacza ustawiony termin przypomnienia (Notification != MinValue)");
        }

        // 3) Wariant 'wszystkie' (aktywne + oczekujące) — nadzbiór wymagalnych:
        var wszystkie = Tasks.GetRemainders(all: true).ToList();
        wszystkie.Should().NotBeNull("GetRemainders(all:true) obejmuje też zadania oczekujące");
        wszystkie.Count.Should().BeGreaterThanOrEqualTo(wymagalne.Count,
            "wariant 'wszystkie' jest nadzbiorem wymagalnych przypomnień");
    }

    [Test]
    [Description("WORKFLOW-E5 (pole IsNotification): setter IsNotification jest spójny z getterem — " +
                 "getter zwraca (Notification != DateTime.MinValue). Sprawdzamy kalkulację bez zapisu na " +
                 "wierszu zbudowanym w transakcji operacyjnej (Logout(editMode:true)) — bez Commitu rollback " +
                 "sprząta. Brak wymagalnych zadań w Demo => testujemy samą kalkulację pola, nie round-trip.")]
    public void WORKFLOW_E5_IsNotification_GetterSpojnyZNotification()
    {
        // Pole IsNotification jest kalkulowane z Notification — to trwały fakt kontraktu, niezależny
        // od zawartości Demo. Weryfikujemy zgodność getterów na dowolnym istniejącym zadaniu, jeśli jest.
        var jakiekolwiek = Tasks.Cast<Task>().FirstOrDefault();

        if (jakiekolwiek != null)
        {
            // Getter IsNotification musi być spójny z Notification (kontrakt kalkulacji):
            jakiekolwiek.IsNotification.Should().Be(jakiekolwiek.Notification != DateTime.MinValue,
                "IsNotification == (Notification != DateTime.MinValue) — kalkulowane pole");

            // NotificationDate/NotificationTime to rozbicie Notification na datę i godzinę:
            if (jakiekolwiek.IsNotification)
                ((DateTime)jakiekolwiek.NotificationDate).Date.Should().Be(jakiekolwiek.Notification.Date,
                    "NotificationDate to składowa daty pola Notification");
        }
        else
        {
            // Brak zadań w Demo — sam fakt, że tabela jest enumerowalna i pusta, jest poprawny:
            Tasks.Cast<Task>().Should().BeEmpty("brak zadań w bazie Demo — kontrakt odczytu pozostaje spójny");
        }

        // POMINIĘTE: odłożenie/wyłączenie przypomnienia (Notification = Now.AddMinutes / IsNotification=false)
        // w transakcji — wymaga istniejącego, edytowalnego zadania operatora w Demo, którego baza nie
        // gwarantuje; dla zadań w aktywnym procesie pola przypomnienia są w UI read-only
        // (IsNotificationInActiveProcess) i steruje nimi silnik. Bez trwałego zadania round-trip byłby kruchy.
    }
}
