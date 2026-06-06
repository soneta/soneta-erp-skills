using System;
using System.Linq;
using AwesomeAssertions;
using NUnit.Framework;
using Soneta.Business;
using Soneta.Core;
using Soneta.HR;
using Soneta.Kadry;
using Soneta.Types;
using Prac = Soneta.Kadry.Pracownik;

namespace Soneta.Skills.Test.KadryPlace.Pracownik;

/// <summary>
/// Rozdział B (pozostałe receptury etatu) — B2..B7 z dokumentu skilla <c>pracownik.md</c>:
/// <list type="bullet">
/// <item><b>B2</b> — aneks (zmiana warunków zatrudnienia „od daty");</item>
/// <item><b>B3</b> — przeszeregowanie (zmiana stawki / grupy zaszeregowania);</item>
/// <item><b>B4</b> — rozwiązanie / wygaśnięcie umowy o pracę;</item>
/// <item><b>B5</b> — obniżenie wymiaru etatu;</item>
/// <item><b>B6</b> — podzielniki kosztów (rozdział kosztów wynagrodzenia);</item>
/// <item><b>B7</b> — aktualizacja danych wg definicji stanowiska (matrycy).</item>
/// </list>
/// <para>
/// Wszystkie zmiany „od daty" realizujemy wzorcem A14: <c>Historia.Update(date)</c> klonuje zapis
/// aktualny na datę, skraca stary do dnia poprzedniego i zwraca nowy klon (okres od daty), który
/// MUSI trafić do tabeli <c>PracHistorie</c> (<c>AddRow</c>). Na świeżym zapisie obowiązuje bramka B1:
/// <c>Etat.Okres</c> ustawiamy jako pierwsze pole etatu (odblokowuje pozostałe), a do <c>Save()</c>
/// wymagane są <c>Etat.Wydzial</c> i <c>Etat.Stanowisko</c>.
/// </para>
/// <para>
/// Kody słowników (przyczyna rozwiązania, definicja stanowiska, grupa zaszeregowania) pobieramy
/// DYNAMICZNIE z bazy Demo (iteracja słownika / pierwszy wpis) — nie zakładamy konkretnych kodów.
/// Wszystko działa na bazie Demo (GoldStandard) z automatycznym rollbackiem; operujemy wyłącznie na
/// publicznym kontrakcie platformy.
/// </para>
/// </summary>
[TestFixture]
public class RozdzialBrest_EtatTest : PracownikTestBase
{
    // === Pomocnik: świeży pracownik etatowy z kompletem warunków wymaganych przy Save ===

    /// <summary>
    /// Tworzy świeżego <see cref="PracownikFirmy"/> z pierwszym zapisem historii i kompletnym etatem
    /// (Okres → Wydzial/Stanowisko → stawka). Zwraca pracownika; zakładamy bycie w transakcji.
    /// </summary>
    private Prac UtworzPracownikaZEtatem(string prefix, FromTo okres,
        RodzajStawkiZaszeregowania rodzaj = RodzajStawkiZaszeregowania.Miesieczna,
        decimal stawka = 6000m)
    {
        var pracownik = Session.AddRow(new PracownikFirmy());
        pracownik.Kod = prefix + "_" + Guid.NewGuid().ToString("N").Substring(0, 6);
        pracownik.Last.Nazwisko = "Testowy";
        pracownik.Last.Imie = "Jan";

        var etat = pracownik.Last.Etat;
        etat.Okres = okres;                                   // BRAMKA B1: Okres najpierw
        etat.TypUmowy = TypUmowyOPrace.NaCzasNieokreślony;
        etat.Podstawa = StosPracyNaPodstawie.UmowyOPrace;
        etat.Stanowisko = "Specjalista";                      // wymagane przy Save
        etat.Wydzial = Kadry.Wydzialy.Firma;                  // wymagane przy Save (referencja)

        var z = etat.Zaszeregowanie;
        z.RodzajStawki = rodzaj;
        z.TypStawki = TypStawkiZaszeregowania.Dowolna;
        z.Wymiar = Fraction.One;
        z.Stawka = (Currency)stawka;
        return pracownik;
    }

    // ============================== B2 — Zmiana warunków zatrudnienia (aneks) ==============================

    [Test]
    [Description("B2: aneks 'od daty' to nowy zapis historii — Historia.Update(odDnia) + PracHistorie.AddRow; " +
                 "na sklonowanym Etat (Okres już ustawiony) zmieniamy warunki: Stanowisko, MiejscePracy, " +
                 "DataZawarcia, Wydzial. Stary okres skraca się do odDnia-1, nowy obowiązuje od odDnia.")]
    public void B2_Aneks_ZmianaWarunkow_OdDaty_TworzyNowyZapis()
    {
        Guid guid = Guid.Empty;
        var okres = new FromTo(new Date(2026, 1, 1), Date.MaxValue);
        var odDnia = new Date(2026, 7, 1);

        InTransaction(() =>
        {
            var pracownik = UtworzPracownikaZEtatem("B2", okres);

            // Aneks od daty — klon zapisu aktualnego na odDnia (skraca stary, zwraca nowy z okresem od odDnia).
            var nowy = (PracHistoria)pracownik.Historia.Update(odDnia);
            pracownik.Module.PracHistorie.AddRow(nowy);       // nierozłączna para z Update

            var etat = nowy.Etat;                              // Okres etatu sklonowany — pola zapisywalne
            etat.Stanowisko = "Starszy specjalista";
            etat.MiejscePracy = "Oddział Kraków";
            etat.DataZawarcia = new Date(2026, 6, 20);
            etat.Wydzial = Kadry.Wydzialy.Firma;               // referencja (wymagana)

            guid = pracownik.Guid;
        });
        SaveDispose();

        var pracownik2 = Get<Prac>(guid);
        var zapisy = pracownik2.Historia.Cast<PracHistoria>().OrderBy(h => h.Aktualnosc.From).ToList();
        zapisy.Should().HaveCount(2, "aneks utworzył drugi zapis historii");

        zapisy[0].Aktualnosc.To.Should().Be(odDnia.AddDays(-1), "stary okres skrócony do dnia poprzedzającego aneks");
        zapisy[1].Aktualnosc.From.Should().Be(odDnia, "nowe warunki obowiązują od daty aneksu");
        zapisy[1].Etat.Stanowisko.Should().Be("Starszy specjalista");
        zapisy[1].Etat.MiejscePracy.Should().Be("Oddział Kraków");
        // Stanowisko sprzed aneksu pozostaje na starym zapisie.
        zapisy[0].Etat.Stanowisko.Should().Be("Specjalista");
    }

    // ============================== B3 — Przeszeregowanie (zmiana stawki / grupy) ==============================

    [Test]
    [Description("B3: przeszeregowanie 'od daty' — nowy zapis historii, a na Etat.Zaszeregowanie podnosimy " +
                 "Stawka (Currency). Stawka to Currency, Wymiar to Fraction. Grupę pobieramy DYNAMICZNIE " +
                 "ze słownika GrupyZaszer (pierwszy wpis), nie hardkodujemy kodu.")]
    public void B3_Przeszeregowanie_ZmianaStawki_IGrupy_OdDaty()
    {
        // Grupa zaszeregowania — referencja do słownika; bierzemy pierwszy istniejący wpis (jeśli jest).
        var grupa = Kadry.GrupyZaszer.Cast<GrupaZaszeregowania>().FirstOrDefault();

        Guid guid = Guid.Empty;
        var okres = new FromTo(new Date(2026, 1, 1), Date.MaxValue);
        var odDnia = new Date(2026, 7, 1);

        InTransaction(() =>
        {
            var pracownik = UtworzPracownikaZEtatem("B3", okres, stawka: 6000m);

            var nowy = (PracHistoria)pracownik.Historia.Update(odDnia);
            pracownik.Module.PracHistorie.AddRow(nowy);

            var etat = nowy.Etat;                              // Okres sklonowany — pola zapisywalne
            etat.Zaszeregowanie.Stawka = (Currency)7200m;      // podwyżka stawki zasadniczej
            if (grupa != null)
                etat.Grupa = grupa;                            // grupa zaszeregowania leży na Etat (nie na Zaszeregowanie)

            guid = pracownik.Guid;
        });
        SaveDispose();

        var pracownik2 = Get<Prac>(guid);
        var nowyZapis = pracownik2.Historia.Cast<PracHistoria>().OrderBy(h => h.Aktualnosc.From).Last();
        nowyZapis.Etat.Zaszeregowanie.Stawka.Should().Be((Currency)7200m, "stawka podwyższona od daty przeszeregowania");
        if (grupa != null)
            nowyZapis.Etat.Grupa.Should().NotBeNull("grupa zaszeregowania powiązana z etatem");
    }

    // ============================== B4 — Rozwiązanie / wygaśnięcie umowy o pracę ==============================

    [Test]
    [Description("B4: rozwiązanie umowy — skrócenie Etat.Okres.To do dnia rozwiązania, dane wypowiedzenia " +
                 "(OkresWypowiedzenia.*), przyczyna ze słownika PrzyczRozwUmow (pobrana DYNAMICZNIE, pierwszy " +
                 "wpis), tryb (PodstawaPrawna/Inicjatywa enumy) oraz flaga Etat.PracownikZwolniony.")]
    public void B4_RozwiazanieUmowy_SkracaOkres_UstawiaPrzyczyneIWypowiedzenie()
    {
        // Przyczyna rozwiązania to REKORD słownika (referencja), nie enum — bierzemy pierwszy wpis z Demo.
        var przyczyna = Kadry.PrzyczRozwUmow.Cast<PrzyczynaRozwUmowy>().FirstOrDefault();

        Guid guid = Guid.Empty;
        var okres = new FromTo(new Date(2026, 1, 1), Date.MaxValue);
        var dataRozwiazania = new Date(2026, 9, 30);

        InTransaction(() =>
        {
            var pracownik = UtworzPracownikaZEtatem("B4", okres);
            var etat = pracownik.Last.Etat;

            // 1) skrócenie okresu etatu do dnia rozwiązania (zmiana w całym bieżącym okresie zapisu)
            etat.Okres = new FromTo(etat.Okres.From, dataRozwiazania);

            // 2) dane wypowiedzenia
            etat.OkresWypowiedzenia.DataZlozenia = new Date(2026, 8, 31);
            etat.OkresWypowiedzenia.Miesiace = 1;

            // 3) przyczyna / tryb rozwiązania
            if (przyczyna != null)
                etat.RozwiazanieUmowy.PrzyczynaRozwUmowy = przyczyna;       // referencja do słownika
            etat.RozwiazanieUmowy.Inicjatywa = KodInicjatywyZwolnienia.Pracownik;   // enum

            etat.PracownikZwolniony = true;                                 // znacznik zakończenia

            guid = pracownik.Guid;
        });
        SaveDispose();

        var etat2 = Get<Prac>(guid).Last.Etat;
        etat2.Okres.To.Should().Be(dataRozwiazania, "okres etatu skrócony do dnia rozwiązania");
        etat2.OkresWypowiedzenia.DataZlozenia.Should().Be(new Date(2026, 8, 31));
        etat2.OkresWypowiedzenia.Miesiace.Should().Be(1);
        etat2.RozwiazanieUmowy.Inicjatywa.Should().Be(KodInicjatywyZwolnienia.Pracownik);
        etat2.PracownikZwolniony.Should().BeTrue();
        if (przyczyna != null)
            etat2.RozwiazanieUmowy.PrzyczynaRozwUmowy.Should().NotBeNull("przyczyna rozwiązania ze słownika");
    }

    [Test]
    [Description("B4 (rozróżnienie): PrzyczynaRozwUmowy to rekord słownika (referencja) z polem Typ " +
                 "(enum TypPrzyczynyRozwUmowy: Rozwiązanie/Wygaśnięcie) — to ono rozróżnia rozwiązanie od " +
                 "wygaśnięcia. Referencja (rekord) != Typ (enum na rekordzie).")]
    public void B4_PrzyczynaRozwUmowy_JestRekordemSlownika_ZTypem()
    {
        var przyczyny = Kadry.PrzyczRozwUmow.Cast<PrzyczynaRozwUmowy>().ToList();
        przyczyny.Should().NotBeEmpty("baza Demo zawiera słownik przyczyn rozwiązania umowy");

        // Typ jest enumem na rekordzie słownika — przyjmuje jedną z dwóch wartości domeny.
        foreach (var p in przyczyny)
            p.Typ.Should().BeOneOf(TypPrzyczynyRozwUmowy.Rozwiązanie, TypPrzyczynyRozwUmowy.Wygaśnięcie);
    }

    // ============================== B5 — Obniżenie wymiaru etatu ==============================

    [Test]
    [Description("B5: obniżenie wymiaru 'od daty' przez nowy zapis historii; obniżony wymiar utrwalamy na " +
                 "Etat.Zaszeregowanie.Wymiar (Fraction). UWAGA: subrow Etat.ObnizenieEtatu jest w PEŁNI " +
                 "tylko-do-odczytu (brak publicznego settera i metody Save) — pełny zapis stanu obniżenia " +
                 "realizują workery platformy; w kodzie biznesowym ustawiamy docelowy wymiar na Zaszeregowaniu.")]
    public void B5_ObnizenieWymiaru_UstawiaDocelowyWymiar_NaZaszeregowaniu()
    {
        Guid guid = Guid.Empty;
        var okres = new FromTo(new Date(2026, 1, 1), Date.MaxValue);
        var odDnia = new Date(2026, 7, 1);
        var obnizonyWymiar = new Fraction(4, 5);               // np. 4/5 etatu

        InTransaction(() =>
        {
            var pracownik = UtworzPracownikaZEtatem("B5", okres);

            var nowy = (PracHistoria)pracownik.Historia.Update(odDnia);
            pracownik.Module.PracHistorie.AddRow(nowy);

            // Subrow ObnizenieEtatu jest read-only (delegat odczytowy) — nie ustawiamy go bezpośrednio.
            // Docelowy wymiar po obniżeniu utrwalamy na Etat.Zaszeregowanie.Wymiar (pole zapisywalne).
            nowy.Etat.Zaszeregowanie.Wymiar = obnizonyWymiar;

            guid = pracownik.Guid;
        });
        SaveDispose();

        var pracownik2 = Get<Prac>(guid);
        var nowyZapis = pracownik2.Historia.Cast<PracHistoria>().OrderBy(h => h.Aktualnosc.From).Last();
        nowyZapis.Etat.Zaszeregowanie.Wymiar.Should().Be(obnizonyWymiar, "wymiar obniżony od daty obniżenia");
        // Stary okres zachowuje pełny wymiar.
        pracownik2.Historia.GetFirst().Etat.Zaszeregowanie.Wymiar.Should().Be(Fraction.One, "przed obniżeniem pełny etat");
    }

    [Test]
    [Description("B5 (kontrakt): wszystkie property subrowa ObniżenieWymiaruEtatu (Wymiar, Stawka, " +
                 "RodzajStawki, TypStawki, Element, Kalendarz, Info) są tylko-do-odczytu — bezpośrednia " +
                 "modyfikacja nie jest możliwa przez publiczny kontrakt; stan obniżenia ustawiają workery.")]
    public void B5_ObnizenieEtatu_JestTylkoDoOdczytu()
    {
        var t = typeof(ObniżenieWymiaruEtatu);
        foreach (var nazwa in new[] { "Wymiar", "Stawka", "RodzajStawki", "TypStawki", "Element", "Kalendarz", "Info" })
        {
            var p = t.GetProperty(nazwa);
            p.Should().NotBeNull($"subrow ObniżenieWymiaruEtatu ma property {nazwa}");
            p!.CanWrite.Should().BeFalse($"property {nazwa} jest tylko-do-odczytu (zapisywana przez worker, nie wprost)");
        }
    }

    // ============================== B6 — Podzielniki kosztów ==============================

    [Test]
    [Description("B6: trójpoziomowa struktura podzielnika — PodzielnikKosztow(pracownik)+PodzielKosztow.AddRow, " +
                 "Historia.Update(odDnia)+HistPodzielnikow.AddRow, ElementPodzielnika(historia)+ElemPodzielnikow.AddRow. " +
                 "ElementPodzialowy to referencja (Wydzial), ustawiamy Wspolczynnik; Procent jest kalkulowany.")]
    public void B6_PodzielnikKosztow_TworzyHistorieIElementUdzialu()
    {
        var core = Session.GetCore();
        // Cel rozdziału musi pochodzić z tabeli zgodnej z definicją podzielnika (domyślna definicja
        // w Demo opiera się o tabelę CentraKosztow) — bierzemy pierwszy istniejący wpis (DYNAMICZNIE).
        var celRozdzialu = core.CentraKosztow.Cast<IElementSlownika>().FirstOrDefault();
        if (celRozdzialu == null)
            Assert.Ignore("Baza Demo nie zawiera centrów kosztów (CentraKosztow) — brak celu rozdziału zgodnego z domyślną definicją podzielnika.");

        Guid guidPrac = Guid.Empty;
        var okres = new FromTo(new Date(2026, 1, 1), Date.MaxValue);
        var odDnia = new Date(2026, 1, 1);

        InTransaction(() =>
        {
            var pracownik = UtworzPracownikaZEtatem("B6", okres);

            // Poziom 1: root podzielnika (źródło = pracownik) + AddRow do tabeli Core.
            // Domyślna definicja (TabelaPodzielnika) decyduje, z jakiej tabeli mogą pochodzić elementy udziału.
            var podzielnik = new PodzielnikKosztow(pracownik);
            core.PodzielKosztow.AddRow(podzielnik);
            podzielnik.Nazwa = "Rozdział kosztów";

            // Poziom 2: zapis historii „od daty" + AddRow.
            var historia = podzielnik.Historia.Update(odDnia);
            core.HistPodzielnikow.AddRow(historia);

            // Poziom 3: element udziału (cel + współczynnik) + AddRow.
            var element = new ElementPodzielnika(historia);
            core.ElemPodzielnikow.AddRow(element);
            element.ElementPodzialowy = celRozdzialu;
            element.Wspolczynnik = 100d;                       // Procent wyliczany z współczynników

            guidPrac = pracownik.Guid;
        });
        SaveDispose();

        var pracownik2 = Get<Prac>(guidPrac);
        // Odczyt poprzez strukturę: pracownik (źródło) → podzielnik → historia → elementy udziału.
        var podzielnik2 = Session.GetCore().PodzielKosztow.Cast<PodzielnikKosztow>()
            .First(p => p.Zrodlo is Prac pr && pr.Guid == guidPrac);
        var elementy = podzielnik2.Last.Elementy.Cast<ElementPodzielnika>().ToList();
        elementy.Should().ContainSingle("dodaliśmy jeden element udziału");
        elementy[0].ElementPodzialowy.Should().NotBeNull("cel rozdziału (centrum kosztów) jest referencją");
        elementy[0].Wspolczynnik.Should().Be(100d);
    }

    // ============================== B7 — Aktualizacja wg definicji stanowiska (matrycy) ==============================

    [Test]
    [Description("B7: powiązanie etatu z definicją stanowiska (matrycą) 'od daty' — nowy zapis historii, " +
                 "Etat.Definicja = matryca (referencja z HR.DefStanowisk, pobrana DYNAMICZNIE), a wartości " +
                 "z matrycy (Stanowisko/wymiar/stawka) przenosimy JAWNIE na etat. Mark [Ignore] gdy brak matryc.")]
    public void B7_DefinicjaStanowiska_PowiazanieIPrzeniesienieWartosci()
    {
        // Definicja stanowiska — matryca konfiguracyjna; pobieramy pierwszą istniejącą (DYNAMICZNIE).
        var def = Session.GetHR().DefStanowisk.Cast<DefinicjaStanowiska>().FirstOrDefault();
        if (def == null)
            Assert.Ignore("Baza Demo nie zawiera definicji stanowisk (DefStanowisk) — nie ma matrycy do powiązania.");

        Guid guid = Guid.Empty;
        var okres = new FromTo(new Date(2026, 1, 1), Date.MaxValue);
        var odDnia = new Date(2026, 7, 1);

        InTransaction(() =>
        {
            var pracownik = UtworzPracownikaZEtatem("B7", okres);

            var nowy = (PracHistoria)pracownik.Historia.Update(odDnia);
            pracownik.Module.PracHistorie.AddRow(nowy);

            var etat = nowy.Etat;
            etat.Definicja = def;                              // powiązanie z definicją stanowiska (referencja)
            // Przeniesienie wartości z matrycy zrobiłbyś jawnie (samo wskazanie Definicja nie nadpisuje pól).
            if (!string.IsNullOrEmpty(def.Stanowisko))
                etat.Stanowisko = def.Stanowisko;

            guid = pracownik.Guid;
        });
        SaveDispose();

        var nowyZapis = Get<Prac>(guid).Historia.Cast<PracHistoria>().OrderBy(h => h.Aktualnosc.From).Last();
        nowyZapis.Etat.Definicja.Should().NotBeNull("etat powiązany z definicją stanowiska");
    }

    [Test]
    [Description("B7 (kontrakt): definicje stanowisk pobieramy ze słownika konfiguracyjnego HR.DefStanowisk; " +
                 "klucz po nazwie to WgNazwa (a nie WgNazwy). Iteracja słownika zwraca rekordy DefinicjaStanowiska.")]
    public void B7_DefStanowisk_JestSlownikiemKonfiguracyjnym()
    {
        var defs = Session.GetHR().DefStanowisk.Cast<DefinicjaStanowiska>().ToList();
        // Słownik może być pusty w Demo — istotne, że iteracja działa i klucz WgNazwa istnieje.
        Session.GetHR().DefStanowisk.WgNazwa.Should().NotBeNull("klucz po nazwie to WgNazwa");
        defs.Should().OnlyContain(d => d != null);
    }
}
