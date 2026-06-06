using System.Linq;
using AwesomeAssertions;
using NUnit.Framework;
using Soneta.Handel;
using Soneta.Kasa;
using Soneta.Types;

namespace Soneta.Skills.Test.Handel.DokumentyHandlowe;

/// <summary>
/// Rozdział 14 skilla „dokument-handlowy” — Płatności dokumentu handlowego (W75–W82).
/// <para>
/// Płatności (należności / zobowiązania) powstają automatycznie z dokumentu handlowego
/// płatnego (FV, FZ). Dostęp daje kolekcja <c>dok.Platnosci</c>
/// (<c>SubTable&lt;Soneta.Kasa.Platnosc&gt;</c>). Testy weryfikują przede wszystkim
/// <b>odczyt</b>: istnienie płatności, kwotę, sposób zapłaty, termin, stan rozliczenia
/// oraz kalkulowaną flagę <c>dok.InnyPłatnik</c>.
/// </para>
/// <para>
/// <b>Klucz rozdziału:</b> faktura sprzedaży to rozchód magazynowy — w bazie Demo
/// <c>StanUjemnyVerifier</c> wymaga wcześniejszego <b>zapisanego</b> przyjęcia (PW) towaru.
/// Dlatego najpierw tworzymy i zapisujemy PW na stan, dopiero potem FV z pozycją. Magazyn
/// księguje się po <c>Session.Save()</c>; po <c>Save()</c> w środku testu okno edycji się
/// zamyka, więc dokument odczytujemy na świeżej sesji przez <c>Get&lt;T&gt;(guid)</c>.
/// </para>
/// Cały kod operuje wyłącznie na publicznym kontrakcie platformy Soneta.
/// </summary>
[TestFixture]
public class Rozdzial14_PlatnosciTest : DokumentHandlowyTestBase
{
    // ── Stałe danych testowych (towar magazynowy w sztukach, kontrahent z Demo) ──
    private const double IloscPrzyjecia = 10;
    private const double IloscFv = 2;
    private const double CenaFv = 100;

    /// <summary>
    /// Tworzy fakturę sprzedaży (FV) z jedną pozycją BIKINI i zapisuje ją <b>w buforze</b>.
    /// Wymaga wcześniej ZATWIERDZONEGO i zapisanego przyjęcia (stan towaru) — robi to bazowy
    /// helper <c>PrzyjmijNaStan</c> (tworzy i zatwierdza PW, dopiero to księguje stan; bez tego
    /// Demo odrzuca rozchód FV przez kontrolę stanu ujemnego). Zwraca Guid zapisanej FV.
    /// <para>
    /// <b>Świadomie NIE zatwierdzamy FV</b>: w testowej bazie Demo zatwierdzenie faktury sprzedaży
    /// rzuca <c>NullReferenceException</c> w ewidencji VAT. Płatności (Należność), <c>Suma</c> i
    /// pozostałe pola są już wyliczone na dokumencie w buforze, więc asercje robimy na FV w buforze.
    /// </para>
    /// </summary>
    private System.Guid UtworzFvWBuforze()
    {
        // WARUNEK WSTĘPNY: zatwierdzone, zapisane przyjęcie tego towaru (stan ujemny zablokowany).
        PrzyjmijNaStan(Towar_.Bikini, IloscPrzyjecia, cena: 5);

        // FV: definicja PIERWSZA, potem kontrahent i magazyn (helper bazy).
        var fv = UtworzDokument(Definicje.FakturaSprzedazy,
            kontrahent: Kontrahent(Kontrahent_.Abc),
            magazyn: Magazyn(Magazyn_.Firma));
        InTransaction(() => DodajPozycje(fv, Towar(Towar_.Bikini), IloscFv, cena: CenaFv));

        var guid = fv.Guid;
        // Save (FV pozostaje w BUFORZE) → utrwala dokument i wyliczone płatności; SaveDispose zamyka okno edycji.
        SaveDispose();
        return guid;
    }

    // ===================================================================================
    // W75 — Przeglądanie płatności dokumentu (dok.Platnosci)
    // ===================================================================================

    [Test]
    [Description("W75: FV w buforze z pozycją ma niepustą kolekcję dok.Platnosci — " +
                 "dokument płatny automatycznie tworzy płatność (Należność) już w buforze.")]
    public void W75_FakturaTworzyPlatnosc()
    {
        // Arrange + Act: zatwierdzone przyjęcie na stan + FV w buforze (płatność tworzy się automatycznie).
        var guid = UtworzFvWBuforze();

        // Odczyt na świeżej sesji po Guid (po Save okno edycji jest zamknięte).
        var fv = Get<DokumentHandlowy>(guid);
        fv.Should().NotBeNull();

        // dok.Platnosci to SubTable<Platnosc>; iterujemy serwerowo i materializujemy do listy do asercji.
        var platnosci = fv.Platnosci.Cast<Platnosc>().ToList();

        // Asercja: dokument płatny wygenerował co najmniej jedną płatność.
        platnosci.Should().NotBeEmpty("faktura (dokument płatny) automatycznie tworzy płatność");
    }

    [Test]
    [Description("W75: odczyt podstawowych pól płatności — Kwota (waluta dokumentu, PLN), " +
                 "SposobZaplaty.Nazwa, Termin oraz StanRozliczenia.")]
    public void W75_OdczytPolPlatnosci()
    {
        var guid = UtworzFvWBuforze();
        var fv = Get<DokumentHandlowy>(guid);

        // Bierzemy pierwszą (zwykle jedyną) płatność faktury.
        var p = fv.Platnosci.Cast<Platnosc>().First();

        // Kwota płatności jest w walucie dokumentu — dla zwykłej FV to PLN (symbol systemowy).
        p.Kwota.Symbol.Should().Be(Currency.SystemSymbol, "płatność zwykłej FV jest w PLN");
        // Kwota powinna odpowiadać wartości brutto dokumentu (jedna płatność = całość).
        p.Kwota.Value.Should().Be(fv.BruttoCy.Value,
            "pojedyncza płatność pokrywa pełną wartość brutto dokumentu");

        // Sposób zapłaty to rekord konfiguracyjny — ma niepustą nazwę (np. „Przelew”/„Gotówka”).
        p.SposobZaplaty.Should().NotBeNull("płatność dziedziczy sposób zapłaty z warunków");
        p.SposobZaplaty.Nazwa.Should().NotBeNullOrEmpty();

        // Termin jest realną datą (nie MaxValue) — wyznaczonym przez warunki płatności.
        p.Termin.Should().NotBe(Date.MaxValue, "termin płatności jest wyznaczony");

        // StanRozliczenia to enum kasowy — odczytujemy go bez modyfikacji.
        p.StanRozliczenia.Should().BeOneOf(
            StanRozliczenia.Nierozliczony,
            StanRozliczenia.Czesciowo,
            StanRozliczenia.Calkowicie,
            StanRozliczenia.NiePodlega);
    }

    [Test]
    [Description("W75: płatność FV jest należnością — Kierunek == Przychod, CzyNaleznosc == true, " +
                 "CzyZobowiazanie == false.")]
    public void W75_PlatnoscFakturySprzedazyToNaleznosc()
    {
        var guid = UtworzFvWBuforze();
        var fv = Get<DokumentHandlowy>(guid);
        var p = fv.Platnosci.Cast<Platnosc>().First();

        // Sprzedaż → należność (przychód środków pieniężnych).
        p.Kierunek.Should().Be(Soneta.Core.KierunekPlatnosci.Przychod);
        p.CzyNaleznosc.Should().BeTrue("płatność faktury sprzedaży to należność");
        p.CzyZobowiazanie.Should().BeFalse();
    }

    // ===================================================================================
    // W80 — Stan rozliczenia płatności (nowa, nierozliczona)
    // ===================================================================================

    [Test]
    [Description("W80: świeżo wystawiona (nieopłacona) płatność jest nierozliczona — " +
                 "StanRozliczenia == Nierozliczony, Rozliczono == false, KwotaRozliczona == 0, " +
                 "DoRozliczenia == Kwota.")]
    public void W80_NowaPlatnoscJestNierozliczona()
    {
        var guid = UtworzFvWBuforze();
        var fv = Get<DokumentHandlowy>(guid);

        // Płatność podlegająca rozliczeniu (Rozliczana == true) i bez żadnych zapłat.
        var p = fv.Platnosci.Cast<Platnosc>().First();

        // Brak operacji kasowych → płatność nierozliczona.
        p.StanRozliczenia.Should().Be(StanRozliczenia.Nierozliczony,
            "nowa płatność bez zapłat jest nierozliczona");
        p.Rozliczono.Should().BeFalse("nic jeszcze nie zapłacono");
        p.KwotaRozliczona.Value.Should().Be(0, "brak rozliczeń");
        // Całość zostaje do rozliczenia (DoRozliczenia == Kwota dla płatności nierozliczonej rozliczanej).
        p.DoRozliczenia.Value.Should().Be(p.Kwota.Value,
            "dla nierozliczonej płatności do rozliczenia pozostaje pełna kwota");
    }

    [Test]
    [Description("W80: DataRozliczenia nierozliczonej płatności to Date.MaxValue (sentinel „nierozliczona”), " +
                 "a nie realna data.")]
    public void W80_DataRozliczeniaNierozliczonejToMaxValue()
    {
        var guid = UtworzFvWBuforze();
        var fv = Get<DokumentHandlowy>(guid);
        var p = fv.Platnosci.Cast<Platnosc>().First();

        // Pułapka z rozdziału: MaxValue oznacza „nierozliczona”, nie traktuj go jak realnej daty.
        p.DataRozliczenia.Should().Be(Date.MaxValue,
            "nierozliczona płatność ma DataRozliczenia == Date.MaxValue");
    }

    // ===================================================================================
    // W79 — Flaga InnyPłatnik (kalkulowana, read-only)
    // ===================================================================================

    [Test]
    [Description("W79: dla zwykłej FV (płatnik = kontrahent) kalkulowana flaga dok.InnyPłatnik == false.")]
    public void W79_ZwyklyDokumentNieMaInnegoPlatnika()
    {
        var guid = UtworzFvWBuforze();
        var fv = Get<DokumentHandlowy>(guid);

        // InnyPłatnik jest wyliczane z porównania Platnosc.Podmiot z dok.Kontrahent.
        // Nie ustawialiśmy odrębnego płatnika, więc flaga jest false.
        fv.InnyPłatnik.Should().BeFalse(
            "nie ustawiono płatnika innego niż kontrahent — flaga kalkulowana jest false");
    }

    // ===================================================================================
    // SCENARIUSZE POMINIĘTE (SKIP) — uzasadnienie zgodne z treścią rozdziału
    // ===================================================================================

    [Test]
    [Ignore("W76 — podział na raty (PodzialPlatnosciWorker). Worker jest publiczny, ale jego akcja " +
            "PodzielPlatnosci SAMA otwiera transakcję (Session.Logout(true) + CommitUI) i USUWA istniejące " +
            "płatności, zastępując je wyliczonymi ratami. Poprawne wywołanie wymaga zbudowania Context z " +
            "dokumentem, instancjacji WParams(context) i sterowania własną transakcją workera wewnątrz " +
            "harnessu testowego (który już zarządza sesją i robi rollback) — splot transakcji zewnętrznej i " +
            "wewnętrznej jest tu kruchy i wykracza poza prosty, wiarygodny przypadek. SKIP wg wytycznych " +
            "rozdziału (testuj tylko proste, jednoznaczne zachowania).")]
    [Description("W76: rozbicie płatności na raty — pominięte (worker steruje własną transakcją i usuwa płatności).")]
    public void W76_PodzialNaRaty_Skip() { }

    [Test]
    [Ignore("W77 — ręczne dodanie płatności (new Naleznosc(dok)/Zobowiazanie(dok) + Platnosci.AddRow). " +
            "Konstruktory są publiczne, ale poprawne ułożenie płatności podlega twardym weryfikatorom: suma " +
            "Kwota wszystkich płatności musi równać się wartości brutto dokumentu, symbol waluty musi zgadzać " +
            "się z dokumentem/ewidencją, a dla przelewu wymagany jest Rachunek należący do podmiotu. " +
            "Zbudowanie spójnego, przechodzącego weryfikację układu „część gotówką + reszta przelewem” " +
            "jest zbyt złożone na prosty test jednostkowy. SKIP wg wytycznych rozdziału (zbyt złożone).")]
    [Description("W77: ręczne dodanie/edycja płatności — pominięte (twarde weryfikatory sumy/waluty/rachunku).")]
    public void W77_RecznaPlatnosc_Skip() { }

    [Test]
    [Ignore("W81 — płatność w walucie obcej (Kwota w walucie vs PLN, Kurs, TabelaKursowa). Wymaga dokumentu " +
            "walutowego oraz tabeli kursowej z kursem na DataDokumentu. Baza Demo nie ma kursów „na dziś” " +
            "(np. EUR), więc operacja walutowa rzuca KursWalutyNotFoundException. Test wymagałby konfiguracji " +
            "kursów/ewidencji walutowej, co wykracza poza zakres rozdziału. SKIP wg pułapek W81 (brak kursu w Demo).")]
    [Description("W81: płatności walutowe — pominięte (wymaga kursu/tabeli kursowej, brak w Demo).")]
    public void W81_PlatnoscWalutowa_Skip() { }

    [Test]
    [Ignore("W82 — rabat za wcześniejszą zapłatę (skonto). Naliczony Rabat (dok.RabatZaTerminPlatnosci.Rabat) " +
            "jest wyliczany z parametrów rabatu skonfigurowanych NA KONTRAHENCIE (RodzajRabatuZaTerminPlatnosci, " +
            "tryb, progi/wartości, IloscDniDlaRabatu). Kontrahenci bazy Demo nie mają tych parametrów ustawionych, " +
            "więc Rabat pozostałby Percent.Zero — test nie weryfikowałby realnego naliczenia. Ustawienie samego " +
            "terminu skonta wymaga ponadto, by wszystkie płatności miały ten sam termin (inaczej RowException). " +
            "SKIP wg pułapek W82 (wymaga konfiguracji rabatu na kontrahencie).")]
    [Description("W82: rabat za termin płatności (skonto) — pominięte (wymaga parametrów rabatu na kontrahencie).")]
    public void W82_RabatZaTermin_Skip() { }
}
