using System;
using System.Linq;
using AwesomeAssertions;
using NUnit.Framework;
using Soneta.Business;
using Soneta.Types;
using Soneta.Workflow.Dms;
using Soneta.Workflow.Workers;

namespace Soneta.Skills.Test.Workflow;

/// <summary>
/// Rozdział J — 'Sprawy (DMS / Matters) i rejestr' (receptury WORKFLOW-J1 … WORKFLOW-J4).
/// <para>
/// Testy są <b>wykonywalną dokumentacją</b> publicznego kontraktu platformy Soneta dla domeny
/// DMS / Sprawy. Każda metoda mapuje się 1:1 do receptury z dokumentu skilla
/// <c>workflow/WORKFLOW10-sprawy-dms.md</c>. Kluczowy model: <b>sprawa</b>
/// (<c>Soneta.Workflow.Dms.Matter</c>, tabela <c>Matters</c>) grupuje <b>dokumenty podstawowe</b>
/// (<c>Soneta.Workflow.Dms.BasicDocument</c>, tabela <c>BasicDocs</c>) rejestrowane w
/// <b>rejestrach</b> (<c>Register</c>). Dostęp z sesji: <c>session.GetDms()</c>
/// (<c>Soneta.Workflow.Dms.DmsModule</c>) — tabele <c>Matters</c>, <c>MatterDefs</c>,
/// <c>BasicDocs</c>, <c>BasicDocDefs</c>, <c>Registers</c>.
/// </para>
/// <para>
/// W przeciwieństwie do definicji procesów (rozdziały A/B — dane KONFIGURACYJNE), sprawy i pisma to
/// dane <b>OPERACYJNE</b> oparte o definicje istniejące standardowo w bazie Demo (dbinit:
/// 'Sprawa Ogólna' / SO, definicje pism, rejestry P/W/WEW). Definicje pobieramy DYNAMICZNIE
/// (<c>ByName.First()</c> / <c>ByName.Last()</c>), tworzymy obiekty konstruktorem publicznym i
/// zapisujemy w sesji operacyjnej (<c>InTransaction</c>/<c>SaveDispose</c>); rollback
/// <c>WorkflowTestBase</c> sprząta. Operujemy wyłącznie na publicznym kontrakcie.
/// </para>
/// </summary>
[TestFixture]
public class RozdzialJ_SprawyDmsTest : WorkflowTestBase
{
    /// <summary>Moduł DMS bieżącej sesji operacyjnej (sprawy, definicje, pisma, rejestry).</summary>
    private DmsModule Dms => Session.GetDms();

    // ============================== WORKFLOW-J1 — Założenie sprawy (★) ==============================

    [Test]
    [Description("WORKFLOW-J1: nowa sprawa = new Matter(definition) + Matters.AddRow(...) w sesji OPERACYJNEJ; " +
                 "definicję sprawy pobieramy dynamicznie z Demo (MatterDefs.ByName.First()); OnAdded ustawia " +
                 "automatycznie Creator, RegistrationDatetime i Number — programista podaje tylko Title/Leader; " +
                 "po zapisie sprawę odnajdujemy kluczem Matters.WgLeader.")]
    public void WORKFLOW_J1_ZalozenieSprawy_OnAddedUstawiaCreatoraINumer_IZapisuje()
    {
        // Definicja sprawy to dane CONFIG istniejące standardowo w Demo — pobieramy dynamicznie.
        // Powiązanie sprawy z procesem workflow (StartProcess) POMINIĘTE — wymaga WDROŻONEJ definicji
        // procesu (WORKFLOW-A7), której Demo nie gwarantuje; powiązanie i tak idzie przez zadania
        // (Task.Parent = matter), a nie przez pole na sprawie.
        var definition = Dms.MatterDefs.ByName.First();
        definition.Should().NotBeNull("baza Demo zawiera standardowe definicje spraw (np. 'Sprawa Ogólna\')");

        Matter matter = null;
        InTransaction(() =>
        {
            matter = new Matter(definition);     // Definition jest wymagana i readonly — tylko przez konstruktor
            Dms.Matters.AddRow(matter);          // OnAdded: RegistrationDatetime, Creator, Number

            // OnAdded zrobił już swoje — NIE nadpisujemy Creator/RegistrationDatetime/Number ręcznie:
            matter.Creator.Should().NotBeNull("OnAdded ustawia zakładającego (operator sesji)");
            matter.RegistrationDatetime.Should().NotBe(DateTime.MinValue,
                "OnAdded ustawia datę i czas rejestracji sprawy");
            matter.Definition.Should().BeSameAs(definition, "Definition ustawia konstruktor (readonly)");

            // Programista uzupełnia tylko dane merytoryczne:
            matter.Title = "Reklamacja dostawy 04/2026";
            // Leader to Operator (wiersz) — MUSI pochodzic z tej samej sesji, w ktorej zapisujemy.
            // Login.Operator zyje w sesji Login (inna sesja) => setter rzuca AssertException
            // (Session==value.Session). Session.Get(...) przepina wiersz do sesji operacyjnej.
            matter.Leader = Session.Get(Session.Login.Operator);
        });
        SaveDispose();

        // Odczyt po zapisie — świeża sesja operacyjna (Session po SaveDispose tworzy nowy kontekst):
        var zapisana = (Matter)Session[matter];
        zapisana.Should().NotBeNull("sprawa została utrwalona w danych operacyjnych");
        zapisana.Title.Should().Be("Reklamacja dostawy 04/2026");
        zapisana.Number.NumerPelny.Should().NotBeNullOrEmpty("numer nadaje numerator definicji w OnAdded");

        // Klucz WgLeader — sprawy prowadzone przez operatora (podzbiór, serwerowo):
        var moje = Dms.Matters.WgLeader[zapisana.Leader].Cast<Matter>().ToList();
        moje.Should().Contain(m => m.Guid == zapisana.Guid,
            "sprawa jest odnajdywana kluczem WgLeader[prowadzący]");
    }

    // ============================== WORKFLOW-J2 — Rejestracja pisma w sprawie (★) ==============================

    [Test]
    [Description("WORKFLOW-J2: pismo = new BasicDocument(definition, register) + BasicDocs.AddRow(...); " +
                 "definicja pisma i rejestr istnieją standardowo w Demo; dołączenie do sprawy przez " +
                 "basicDocument.Matter = matter (wypełnia AddingDatetime). PUŁAPKI: Date edytowalne tylko gdy " +
                 "IsAdded (po zapisie IsReadOnlyDate()==true); pisma dołączonego do sprawy nie da się usunąć " +
                 "(GetCanDelete()==false dopóki Matter != null).")]
    public void WORKFLOW_J2_RejestracjaPismaWSprawie_DolaczenieBlokujeUsuniecie()
    {
        // Dane słownikowe DMS pobieramy dynamicznie — definicja pisma i rejestr są w każdej bazie:
        var defPisma = Dms.BasicDocDefs.ByName.Last();
        var rejestr = Dms.Registers.ByName.First();
        defPisma.Should().NotBeNull("Demo zawiera standardowe definicje pism (np. 'Pismo\')");
        rejestr.Should().NotBeNull("Demo zawiera standardowe rejestry (Przychodzące/Wychodzące/Wewnętrzne)");

        Matter matter = null;
        BasicDocument pismo = null;
        InTransaction(() =>
        {
            // Sprawa, do której dołączymy pismo:
            matter = new Matter(Dms.MatterDefs.ByName.First());
            Dms.Matters.AddRow(matter);
            matter.Title = "Sprawa z pismem";

            pismo = new BasicDocument(defPisma, rejestr);   // Register wymagany — przez konstruktor
            Dms.BasicDocs.AddRow(pismo);                    // OnAdded: Date=dzisiaj, DateOfReceipt, DocumentAccess

            // PUŁAPKA: Date jest edytowalne TYLKO na świeżo dodanym wierszu (IsAdded). Tu jeszcze można:
            pismo.IsAdded.Should().BeTrue("świeżo dodane pismo jest w stanie Added");
            pismo.IsReadOnlyDate().Should().BeFalse("przed pierwszym zapisem Date jest edytowalne (IsAdded)");

            // Dołączenie do sprawy — wypełnia AddingDatetime i dziedziczy komórkę merytoryczną ze sprawy:
            pismo.Matter = matter;
            pismo.Matter.Should().BeSameAs(matter, "pismo zostało dołączone do sprawy");

            // PUŁAPKA: pisma dołączonego do sprawy NIE można usunąć — GetCanDelete()==false dopóki Matter != null:
            pismo.GetCanDelete().Should().BeFalse(
                "GetCanDelete() blokuje usunięcie pisma dopóki Matter != null (OnDeleting rzuca wyjątek)");
        });
        SaveDispose();

        // Po zapisie pismo widać przez kolekcję sprawy oraz przez klucz WgMatter (serwerowo):
        var sprawa2 = (Matter)Session[matter];
        sprawa2.BasicDocuments.Cast<BasicDocument>().Should().ContainSingle(
            "pismo należy do kolekcji BasicDocuments sprawy");
        Dms.BasicDocs.WgMatter[sprawa2].Cast<BasicDocument>().Should().ContainSingle(
            "klucz WgMatter[sprawa] zwraca pisma sprawy");

        // PUŁAPKA potwierdzona po zapisie: Date jest już tylko-do-odczytu (nie jest już IsAdded):
        var pismo2 = (BasicDocument)Session[pismo];
        pismo2.IsAdded.Should().BeFalse("po zapisie wiersz nie jest już w stanie Added");
        pismo2.IsReadOnlyDate().Should().BeTrue(
            "po zapisie Date pisma jest tylko-do-odczytu (próba zmiany dałaby ColReadOnlyException)");
        pismo2.GetCanDelete().Should().BeFalse("dopóki pismo jest w sprawie, pozostaje nieusuwalne");
    }

    // ============================== WORKFLOW-J3 — Przegląd spraw na liście DMS (★) ==============================

    [Test]
    [Description("WORKFLOW-J3: odpowiednik listy DMS/Sprawy — odczyt serwerowy. Filtr kluczem " +
                 "Matters.WgDefinition[definicja] + SubTable[RowCondition]; widok z prawami dostępu " +
                 "Matters.GetGrantedView(); sprawa po pełnym numerze przez Matters.NumberWgNumeruDokumentu. " +
                 "'Sprawa zakończona\' to CloseDatetime != DateTime.MinValue (brak osobnej flagi bool).")]
    public void WORKFLOW_J3_PrzegladSprawNaLiscieDms_FiltryKluczamiIGrantedView()
    {
        var definition = Dms.MatterDefs.ByName.First();

        Matter matter = null;
        InTransaction(() =>
        {
            matter = new Matter(definition);
            Dms.Matters.AddRow(matter);
            matter.Title = "Sprawa do przeglądu";
            // Leader (Operator) musi byc z sesji operacyjnej, nie z sesji Login (cross-session assert).
            matter.Leader = Session.Get(Session.Login.Operator);
        });
        SaveDispose();

        var zapisana = (Matter)Session[matter];

        // Po SaveDispose Session to NOWY kontekst sesji — wiersz `definition` zostal pobrany
        // w poprzedniej sesji. Przepinamy go do biezacej sesji, zanim uzyjemy go jako klucza/warunku.
        var definitionWBiezacej = Session.Get(definition);

        // 1) Filtr listy DMS/Sprawy: definicja sprawy — klucz WgDefinition (podzbiór, serwerowo):
        var wgDefinicji = Dms.Matters.WgDefinition[definitionWBiezacej].Cast<Matter>().ToList();
        wgDefinicji.Should().Contain(m => m.Guid == zapisana.Guid,
            "WgDefinition[definicja] zwraca sprawy danej definicji");

        // Złożony filtr: aktywne sprawy prowadzącego (CloseDatetime == MinValue) — SubTable[condition]:
        var aktywneMoje = Dms.Matters.WgLeader[zapisana.Leader]
            [new FieldCondition.Equal("CloseDatetime", DateTime.MinValue)]
            .Cast<Matter>().ToList();
        aktywneMoje.Should().Contain(m => m.Guid == zapisana.Guid,
            "sprawa niezakończona ma CloseDatetime == DateTime.MinValue (brak osobnej flagi bool)");
        zapisana.CloseDatetime.Should().Be(DateTime.MinValue, "świeża sprawa nie jest zakończona");

        // 2) Widok listy UI z prawami dostępu — tak buduje się lista 'DMS/Sprawy' (FolderView):
        var view = Dms.Matters.GetGrantedView();
        view.Should().NotBeNull("GetGrantedView() zwraca widok filtrujący sprawy niedostępne dla operatora");
        view.Condition &= new FieldCondition.Equal("Definition", definitionWBiezacej);
        // (Operator testowy ma prawa do standardowej definicji — sprawa jest widoczna w widoku.)
        view.Cast<Matter>().Should().Contain(m => m.Guid == zapisana.Guid,
            "sprawa dostępna operatorowi jest widoczna w GetGrantedView() po dołożeniu warunku Definition");

        // 3) Sprawa po PEŁNYM numerze (format wg numeratora definicji) — klucz NumberWgNumeruDokumentu:
        var poNumerze = Dms.Matters.NumberWgNumeruDokumentu[zapisana.Number.NumerPelny];
        poNumerze.Should().NotBeNull("NumberWgNumeruDokumentu[pelny] odnajduje sprawę po numerze");
        ((Matter)poNumerze).Guid.Should().Be(zapisana.Guid, "klucz po numerze wskazuje tę samą sprawę");
    }

    // ============================== WORKFLOW-J4 — Historia sprawy (MatterCaseWorker) ==============================

    [Test]
    [Description("WORKFLOW-J4: historię sprawy agreguje publiczny worker MatterCaseWorker (kontekst Matter) — " +
                 "nagłówek (Number/Title/RegistrationDatetime) i lista MatterCaseChangeInfos. Historia opiera " +
                 "się na rejestrze zmian (ChangeInfo) — przy WYŁĄCZONEJ rejestracji zmian w Demo lista bywa " +
                 "PUSTA i to nie jest błąd; testujemy więc trwały kontrakt: nagłówek + niepustość listy zmian, " +
                 "z dopuszczeniem listy pustej. Worker wołamy POZA transakcją, na zapisanych danych.")]
    public void WORKFLOW_J4_HistoriaSprawy_MatterCaseWorker_NaglowekILista()
    {
        Matter matter = null;
        InTransaction(() =>
        {
            matter = new Matter(Dms.MatterDefs.ByName.First());
            Dms.Matters.AddRow(matter);
            matter.Title = "Sprawa z historią";
            // Leader (Operator) musi byc z sesji operacyjnej, nie z sesji Login (cross-session assert).
            matter.Leader = Session.Get(Session.Login.Operator);
        });
        SaveDispose();

        var zapisana = (Matter)Session[matter];

        // Worker historii: kontekst to property Matter (wystarczy przypisać). Wołamy POZA transakcją,
        // na zapisanych danych — zmiana property Matter unieważnia zbuforowaną listę.
        var worker = new MatterCaseWorker { Matter = zapisana };

        // Nagłówek raportu historii — TRWAŁE fakty wynikające ze sprawy:
        worker.Number.Should().Be(zapisana.Number.NumerPelny, "nagłówek historii pokazuje numer sprawy");
        worker.Title.Should().Be("Sprawa z historią", "nagłówek historii pokazuje tytuł sprawy");
        worker.RegistrationDatetime.Should().Be(zapisana.RegistrationDatetime.ToString("g"),
            "nagłówek historii pokazuje datę rejestracji sprawy");

        // Lista zmian — istnieje tylko dla tabel z włączoną rejestracją zmian (ChangeInfo).
        // Sprawdzamy tryb rejestracji dla tabeli Matters; przy wyłączonym rejestrze lista jest pusta.
        var changeInfoMode = Login.GetTableChangeInfos("Matters").RowInfo;
        var historia = worker.MatterCaseChangeInfos;
        historia.Should().NotBeNull("MatterCaseChangeInfos zawsze zwraca listę (może być pusta)");

        if (changeInfoMode == Soneta.Business.App.ChangeInfoMode.None)
        {
            // Demo z wyłączoną rejestracją zmian dla Matters — pusta historia to NIE błąd:
            historia.Should().BeEmpty("przy wyłączonym rejestrze zmian (RowInfo=None) historia jest pusta");
        }
        else
        {
            // Przy włączonym rejestrze — założenie sprawy zostawia co najmniej jeden wpis:
            historia.Should().NotBeEmpty("przy włączonym rejestrze zmian założenie sprawy daje wpis historii");
            historia.Should().OnlyContain(w => w.DateTime != DateTime.MinValue,
                "każdy wpis historii ma moment zmiany");
        }

        // Informacja 'kto założył' jest dostępna BEZ rejestru zmian (FirstChangeInfo) — trwały fakt:
        zapisana.FirstChangeInfo.Should().NotBeNull(
            "FirstChangeInfo niesie info o założycielu niezależnie od trybu rejestru zmian");
    }
}
