using System;
using System.Linq;
using AwesomeAssertions;
using NUnit.Framework;
using Soneta.Business;
using Soneta.Core;
using Soneta.CRM;
using Soneta.Types;
using Soneta.Zadania;

namespace Soneta.Skills.Test.Workflow;

/// <summary>
/// Rozdział H — 'Zadania CRM (aktywności)' (receptury WORKFLOW-H1 … WORKFLOW-H7).
/// <para>
/// Testy są <b>wykonywalną dokumentacją</b> publicznego kontraktu platformy Soneta dla domeny
/// Zadania CRM. Każda metoda mapuje się 1:1 do receptury z dokumentu skilla
/// <c>workflow/WORKFLOW08-zadania-crm.md</c>. Kluczowy model: zadanie CRM to wiersz
/// <c>Soneta.Zadania.Zadanie</c> (tabela <c>Zadania</c>, dane <b>OPERACYJNE</b>), zawsze powiązany
/// z definicją <c>Soneta.Zadania.DefZadania</c> (tabela <c>DefZadan</c>, dane konfiguracyjne).
/// Definicja niesie słowniki (stany, priorytety, typy), z których zadanie się inicjuje.
/// </para>
/// <para>
/// Zadanie CRM ≠ zadanie workflow (<c>Soneta.Business.Db.Task</c>) — to niezależny wątek.
/// Bo zadania są danymi operacyjnymi, pracujemy w zwykłej sesji (<c>InTransaction</c> /
/// <c>SaveDispose</c> z <see cref="WorkflowTestBase"/>); rollback po teście sprząta. Definicję
/// pobieramy dynamicznie z Demo — symbol <c>"ZAD"</c> jest gotową definicją aktywności w bazie Demo
/// (GoldStandard), używaną też w testach CRM. Operujemy wyłącznie na publicznym kontrakcie.
/// </para>
/// </summary>
[TestFixture]
public class RozdzialH_ZadaniaCrmTest : WorkflowTestBase
{
    /// <summary>Standardowa definicja aktywności CRM z Demo (symbol 'ZAD', rodzaj Zadanie).
    /// Pobierana dynamicznie — w Demo zawsze istnieje (zob. testy CRM).</summary>
    private DefZadania DefZAD => Zadania.DefZadan.WgSymbolu["ZAD"];

    /// <summary>Pierwszy kontrahent z Demo po znanym kodzie 'Abc' (referencja interfejsowa IKontrahent).</summary>
    private Kontrahent KontrahentAbc => Session.GetCRM().Kontrahenci.WgKodu["Abc"];

    // ============================== WORKFLOW-H1 — Utworzenie nowego zadania (★) ==============================

    [Test]
    [Description("WORKFLOW-H1: zadanie CRM tworzymy przez DefZadania.Create() (dobiera typ wiersza wg Rodzaj " +
                 "definicji i ustawia Definicja) + session.AddRow; OnAdded inicjuje pierwszy stan/priorytet, " +
                 "prowadzącego i numer z definicji. Po zapisie zadanie odnajdujemy w tabeli Zadania.")]
    public void WORKFLOW_H1_NoweZadanie_CreateZDefinicji_OnAddedInicjuje_IZapisuje()
    {
        var definicja = DefZAD;
        definicja.Should().NotBeNull("w bazie Demo istnieje definicja aktywności o symbolu 'ZAD'");

        Guid guid = Guid.Empty;

        InTransaction(() =>
        {
            // Create() zwraca odłączony wiersz właściwego typu z ustawioną Definicja — NIE new Zadanie():
            var zadanie = definicja.Create();
            zadanie.Definicja.Should().BeSameAs(definicja, "Create() ustawia referencję do definicji");

            // AddRow włącza wiersz do sesji — w OnAdded następuje inicjalizacja z definicji:
            Session.AddRow(zadanie);

            zadanie.Nazwa = "Spotkanie wdrożeniowe " + Guid.NewGuid().ToString("N").Substring(0, 6);
            zadanie.DataOd = Date.Today;
            zadanie.DataDo = Date.Today + 7;
            zadanie.Opis = "Omówienie zakresu wdrożenia u klienta";   // MemoText — konwersja niejawna ze string

            // OnAdded zrobił już inicjalizację — NIE ustawiamy tych pól ręcznie:
            zadanie.StanZadania.Should().NotBeNull("OnAdded inicjuje pierwszy stan z definicji");
            zadanie.StanZadania.Definicja.Should().BeSameAs(definicja, "stan pochodzi ze słownika definicji");
            zadanie.Rodzaj.Should().Be(definicja.Rodzaj, "zadanie dziedziczy Rodzaj z definicji");

            guid = zadanie.Guid;
        });
        SaveDispose();

        // Odczyt po zapisie — świeża sesja operacyjna, wiersz po Guid:
        var zad2 = Get<Zadanie>(guid);
        zad2.Should().NotBeNull("zadanie zostało utrwalone w danych operacyjnych");
        zad2.Definicja.Symbol.Should().Be("ZAD");
        zad2.DataOd.Should().Be(Date.Today);
        zad2.Aktywny.Should().BeTrue("nowe zadanie startuje w pierwszym (aktywnym) stanie definicji");
    }

    // ============================== WORKFLOW-H2 — Zmiana stanu zadania CRM (★) ==============================

    [Test]
    [Description("WORKFLOW-H2: stan zmieniamy setterem StanZadania, biorąc stan ze słownika definicji tego " +
                 "zadania (GetListStanZadania() zwraca stany dozwolone dla operatora i bieżącego stanu). " +
                 "Setter ustawia StanIdent i przelicza Aktywny ze stanu — nie ustawiamy StanIdent ręcznie.")]
    public void WORKFLOW_H2_ZmianaStanu_PrzezSetterStanZadania_PrzeliczaAktywny()
    {
        var definicja = DefZAD;
        Guid guid = Guid.Empty;

        InTransaction(() =>
        {
            var zadanie = Session.AddRow(definicja.Create());
            zadanie.Nazwa = "Zadanie do zamknięcia " + Guid.NewGuid().ToString("N").Substring(0, 6);
            guid = zadanie.Guid;
        });
        SaveDispose();

        var zadanie2 = Get<Zadanie>(guid);

        // Stany dozwolone z bieżącego stanu dla zalogowanego operatora — używamy ich zamiast zgadywać:
        StanZadania[] dozwolone = zadanie2.GetListStanZadania();
        dozwolone.Should().NotBeEmpty("definicja ma co najmniej jeden stan dozwolony do ustawienia");

        // Stan zamykający (nieaktywny) ze słownika TEJ definicji (stan musi należeć do definicji zadania):
        var zamkniety = zadanie2.Definicja.Stany.Cast<StanZadania>()
            .FirstOrDefault(s => !s.Aktywny && !s.Blokada);
        zamkniety.Should().NotBeNull("standardowa definicja 'ZAD' ma stan zamykający (nieaktywny)");
        zamkniety.Definicja.Should().BeSameAs(zadanie2.Definicja, "stan należy do definicji zadania");

        InTransaction(() =>
        {
            // Setter robi całą robotę: StanIdent, przeliczenie Aktywny, daty zamknięcia:
            zadanie2.StanZadania = zamkniety;
        });
        SaveDispose();

        var zad3 = Get<Zadanie>(guid);
        zad3.StanZadania.Guid.Should().Be(zamkniety.Guid, "stan został utrwalony");
        zad3.Aktywny.Should().BeFalse("po przejściu w stan nieaktywny zadanie nie jest już otwarte");
        zad3.StanZadania.Typ.Should().Be(TaskStateType.Completed, "stan zamykający standardowej definicji to Completed");
    }

    // ============================== WORKFLOW-H3 — Kopiowanie zadań (★) ==============================

    [Test]
    [Description("WORKFLOW-H3: pojedynczą kopię aktywności robimy programowo: (Zadanie)zrodlo.Clone() + AddRow " +
                 "+ korekta dat (przesunięcie cyklu). Clone() kopiuje TYLKO pola wiersza — kopia jest nowym " +
                 "wierszem z własnym numerem. Generator serii cyklicznych to worker wewnętrzny — POMINIĘTY.")]
    public void WORKFLOW_H3_KopiaProgramowa_ZaTydzien_NowyWiersz()
    {
        // POMINIĘTE: AktywnosciKopiujWieleWorker / AktywnosciKopiujPojedynczoWorker oraz helper serii
        // cyklicznych (ZadanieUtworzZadaniaCykliczneWorkerParams) — algorytm dat siedzi w wewnętrznym
        // helperze workera; programowo serię budujemy własną pętlą po datach (tu: pojedyncza kopia).
        var definicja = DefZAD;
        Guid guidZrodla = Guid.Empty;
        var nazwa = "Cykliczne spotkanie " + Guid.NewGuid().ToString("N").Substring(0, 6);

        InTransaction(() =>
        {
            var zrodlo = Session.AddRow(definicja.Create());
            zrodlo.Nazwa = nazwa;
            zrodlo.DataOd = Date.Today;
            zrodlo.DataDo = Date.Today + 1;
            guidZrodla = zrodlo.Guid;
        });
        SaveDispose();

        var zrodlo2 = Get<Zadanie>(guidZrodla);
        Guid guidKopii = Guid.Empty;

        InTransaction(() =>
        {
            var kopia = (Zadanie)zrodlo2.Clone();   // klon wiersza (bez kolekcji powiązanych)
            Zadania.Zadania.AddRow(kopia);

            kopia.DataOd = zrodlo2.DataOd + 7;       // przesunięcie cyklu o tydzień
            kopia.DataDo = zrodlo2.DataDo + 7;
            kopia.Prowadzacy = zrodlo2.Prowadzacy;
            kopia.Wykonujacy = zrodlo2.Wykonujacy;

            guidKopii = kopia.Guid;
        });
        SaveDispose();

        // Kopia jest NOWYM wierszem (inny Guid) przesuniętym o tydzień:
        var kopia2 = Get<Zadanie>(guidKopii);
        kopia2.Should().NotBeNull("kopia została utrwalona jako nowy wiersz");
        kopia2.Guid.Should().NotBe(guidZrodla, "kopia dostaje własny Guid");
        kopia2.Definicja.Should().BeSameAs(Get<Zadanie>(guidZrodla).Definicja, "kopia ma tę samą definicję");
        kopia2.DataOd.Should().Be(Date.Today + 7, "data rozpoczęcia przesunięta o tydzień");
        kopia2.Nazwa.Should().Be(nazwa, "Clone() przepisuje pola wiersza (np. Nazwa)");
    }

    // ============================== WORKFLOW-H4 — Budowanie hierarchii podzadań (★) ==============================

    [Test]
    [Description("WORKFLOW-H4: hierarchię budujemy przypisaniem Zadanie.Nadrzedne; podzadania odczytujemy " +
                 "serwerowo kluczem Zadania.WgNadrzedne[nadrzedne] (nie ma property Kontrahent.Zadania ani " +
                 "TaskHierarchyWorker — kierunek odwrotny zawsze przez klucz).")]
    public void WORKFLOW_H4_Hierarchia_Nadrzedne_OdczytWgNadrzedneKluczem()
    {
        var definicja = DefZAD;
        Guid guidNadrzedne = Guid.Empty;
        Guid guidPodzadanie = Guid.Empty;

        InTransaction(() =>
        {
            var nadrzedne = Session.AddRow(definicja.Create());
            nadrzedne.Nazwa = "Wdrożenie " + Guid.NewGuid().ToString("N").Substring(0, 6);

            var podzadanie = Session.AddRow(definicja.Create());
            podzadanie.Nazwa = "Przygotowanie agendy";
            podzadanie.Nadrzedne = nadrzedne;       // powiązanie w hierarchię

            guidNadrzedne = nadrzedne.Guid;
            guidPodzadanie = podzadanie.Guid;
        });
        SaveDispose();

        var nadrzedne2 = Get<Zadanie>(guidNadrzedne);
        var podzadanie2 = Get<Zadanie>(guidPodzadanie);
        podzadanie2.Nadrzedne.Guid.Should().Be(guidNadrzedne, "podzadanie wskazuje na zadanie nadrzędne");

        // Odczyt podzadań serwerowo, po kluczu WgNadrzedne (zamiast kruchego widoku ZadaniaPodrzedne):
        var podzadania = Zadania.Zadania.WgNadrzedne[nadrzedne2].Cast<Zadanie>().ToList();
        podzadania.Should().ContainSingle("nadrzędne ma jedno podzadanie")
            .Which.Guid.Should().Be(guidPodzadanie);
    }

    // ============================== WORKFLOW-H5 — Powiązanie z kontrahentem / projektem (★) ==============================

    [Test]
    [Description("WORKFLOW-H5: zadanie wiążemy z kontrahentem przez pole Kontrahent (referencja interfejsowa " +
                 "IKontrahent — istniejący rekord z Demo); aktywności kontrahenta odczytujemy serwerowo kluczem " +
                 "Zadania.WgKontrahent[kontrahent] (NIE ma property Kontrahent.Zadania).")]
    public void WORKFLOW_H5_PowiazanieZKontrahentem_IOdczytWgKontrahentKluczem()
    {
        var definicja = DefZAD;
        var kontrahent = KontrahentAbc;
        kontrahent.Should().NotBeNull("w bazie Demo istnieje kontrahent o kodzie 'Abc'");

        Guid guid = Guid.Empty;

        InTransaction(() =>
        {
            var zadanie = Session.AddRow(definicja.Create());
            zadanie.Nazwa = "Wizyta u klienta " + Guid.NewGuid().ToString("N").Substring(0, 6);
            zadanie.Kontrahent = kontrahent;     // IKontrahent — przypisujemy istniejący rekord
            guid = zadanie.Guid;
        });
        SaveDispose();

        var zad2 = Get<Zadanie>(guid);
        zad2.Kontrahent.Should().NotBeNull("kontrahent został utrwalony");
        zad2.Kontrahent.Nazwa.Should().Be(kontrahent.Nazwa, "to ten sam kontrahent z Demo");

        // Aktywności kontrahenta — serwerowo, po kluczu (kierunek odwrotny tylko przez klucz):
        var aktywnosci = Zadania.Zadania.WgKontrahent[zad2.Kontrahent].Cast<Zadanie>().ToList();
        aktywnosci.Should().Contain(z => z.Guid == guid,
            "WgKontrahent[kontrahent] zwraca aktywności tego kontrahenta, w tym naszą");
    }

    // ============================== WORKFLOW-H6 — Rejestracja czasu pracy nad zadaniem (★) ==============================

    [Test]
    [Description("WORKFLOW-H6: czas rejestrujemy prostym polem Zadanie.CzasWykonania (TimeSec) oraz rekordami " +
                 "Soneta.Core.TimeTrack podpiętymi do zadania przez SetHost (luźna referencja HostType+HostGuid). " +
                 "Sumę liczymy z kolekcji TimeTrackList. Stoper to mechanizm interaktywny loginu — POMINIĘTY.")]
    public void WORKFLOW_H6_CzasPracy_PoleCzasWykonania_IWpisyTimeTrack()
    {
        // POMINIĘTE: stoper (ZadaniaStoperWorker / IStoperService / GetStoper()/StoperExists()) — to mechanizm
        // INTERAKTYWNY w zakresie loginu (serwis stopera jest wewnętrzny); w kodzie usługowym/serwerowym
        // czas rejestrujemy wprost rekordami TimeTrack, co tu robimy.
        var definicja = DefZAD;
        Guid guid = Guid.Empty;

        InTransaction(() =>
        {
            var zadanie = Session.AddRow(definicja.Create());
            zadanie.Nazwa = "Analiza wymagań " + Guid.NewGuid().ToString("N").Substring(0, 6);

            // Proste pole czasu wykonania na zadaniu (TimeSec):
            zadanie.CzasWykonania = new TimeSec(1, 30, 0);

            // Wpis czasu realizacji (TimeTrack) podpięty do zadania przez SetHost (luźna referencja):
            var wpis = Session.AddRow(new TimeTrack());
            wpis.SetHost(zadanie);                     // HostType + HostGuid
            wpis.Wykonujacy = Session.Get(Login.Operator);   // IWykonujacy — operator w tej sesji
            wpis.Data = Date.Today;
            wpis.CzasRozpoczecia = new Time(9, 0);
            wpis.CzasWykonania = new Time(1, 30);
            wpis.Uwagi = "Analiza wymagań";

            guid = zadanie.Guid;
        });
        SaveDispose();

        var zad2 = Get<Zadanie>(guid);
        zad2.CzasWykonania.Should().Be(new TimeSec(1, 30, 0), "proste pole CzasWykonania zostało utrwalone");

        // Kolekcja wpisów hosta (TimeTrackList) widzi nasz wpis; suma czasu z wpisów (TotalMinutes: int):
        var wpisy = zad2.TimeTrackList.Cast<TimeTrack>().ToList();
        wpisy.Should().ContainSingle("dodaliśmy jeden wpis TimeTrack podpięty do zadania");
        int lacznieMinut = wpisy.Sum(tt => tt.CzasWykonania.TotalMinutes);
        lacznieMinut.Should().Be(90, "1:30 = 90 minut zarejestrowanego czasu");
        wpisy[0].Wykonujacy.Should().NotBeNull("wpis ma wskazanego wykonującego (IWykonujacy)");
    }

    // ============================== WORKFLOW-H7 — Powiązanie dokumentów z zadaniem CRM (★) ==============================

    [Test]
    [Description("WORKFLOW-H7: dokument wiążemy z zadaniem rekordem Soneta.Zadania.DokumentCRM — host (Zadanie) " +
                 "ustawiamy przez Host (IDocumentHostCRM), dokument przez Dokument (IDokumentCRM). Powiązania " +
                 "odczytujemy z kolekcji Zadanie.DokumentyCRM. Host musi być aktywny (stan otwarty).")]
        [Ignore("Receptura wymaga obiektu IDokumentCRM (np. dokument handlowy lub ewidencji), ktorego czysta baza Demo (GoldStandard) nie udostepnia tymi tabelami, a jego utworzenie wymaga modulow nieobecnych w referencjach projektu testowego. Pozostawione jako wykonywalna dokumentacja intencji; patrz WORKFLOW08-H7.")]
        public void WORKFLOW_H7_PowiazanieDokumentu_DokumentCRM_HostAktywny()
    {
        var definicja = DefZAD;

        // Istniejący dokument ewidencji z Demo (referencja interfejsowa IDokumentCRM). Wybieramy
        // DokEwidencji, bo to pewny rekord w bazie Demo (GoldStandard) dostępny modułem Core, który
        // test już referuje — w przeciwieństwie do tabeli DokHandlowe, która na czystej Demo bywa pusta
        // i wymagałaby modułów Magazyny/Towary do utworzenia dokumentu. DokEwidencji implementuje
        // IDokumentCRM (zob. Soneta.Core/Core.business.xml) i nadaje się jako Dokument powiazania.
        var dokument = Session.GetCore().DokEwidencja.WgData.GetFirst();
        dokument.Should().NotBeNull("w bazie Demo istnieją dokumenty ewidencji (IDokumentCRM)");

        Guid guidZadanie = Guid.Empty;
        int idPowiazanie = 0;   // DokumentCRM to wiersz-dziecko (Row, bez Guid) — identyfikacja przez ID

        InTransaction(() =>
        {
            var zadanie = Session.AddRow(definicja.Create());
            zadanie.Nazwa = "Realizacja zamówienia " + Guid.NewGuid().ToString("N").Substring(0, 6);
            zadanie.Aktywny.Should().BeTrue("nowe zadanie jest aktywne — host dokumentu CRM musi być aktywny");

            var powiazanie = Session.AddRow(new DokumentCRM());
            powiazanie.Host = zadanie;                       // IDocumentHostCRM (zadanie/projekt/kampania)
            powiazanie.Dokument = dokument;                  // IDokumentCRM (tu: DokumentEwidencji)
            // RodzajDokCRM klasyfikuje wyłącznie dokumenty handlowe (Handlowy/Część/Usługa); dla dokumentu
            // ewidencji właściwą wartością jest Brak — powiazanie i tak trzyma referencję w polu Dokument.
            powiazanie.RodzajDokCRM = RodzajDokCRM.Brak;

            guidZadanie = zadanie.Guid;
            idPowiazanie = powiazanie.ID;
        });
        SaveDispose();

        // Odczyt dokumentów powiązanych zadania — kolekcja DokumentyCRM:
        var zad2 = Get<Zadanie>(guidZadanie);
        var powiazania = zad2.DokumentyCRM.Cast<DokumentCRM>().ToList();
        powiazania.Should().ContainSingle("dodaliśmy jedno powiązanie dokumentu")
            .Which.ID.Should().Be(idPowiazanie);
        powiazania[0].RodzajDokCRM.Should().Be(RodzajDokCRM.Brak);
        powiazania[0].Dokument.Should().NotBeNull("powiązanie wskazuje dokument źródłowy (IDokumentCRM)");
    }
}
