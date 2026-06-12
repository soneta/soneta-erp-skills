using System;
using System.Linq;
using System.Text;
using AwesomeAssertions;
using NUnit.Framework;
using Soneta.Kadry;
using Soneta.Place;
using Soneta.Types;
using Prac = Soneta.Kadry.Pracownik;

namespace Soneta.Skills.Test.KadryPlace.Pracownik;

/// <summary>
/// Rozdział H (część rozszerzona) — „Płace: odczyt i operacje na naliczonych wypłatach"
/// (receptury KADRY-H5–KADRY-H11).
/// <para>
/// Każdy test najpierw nalicza wypłatę etatową pracownika Demo workerem
/// <c>Soneta.Place.NaliczanieSeryjne</c> (wzorzec z KADRY-H1: <c>PracownikParams(Context)</c> +
/// <c>DataWypłaty</c> w okresie etatu + <c>Nalicz()</c>), a następnie odczytuje elementy
/// (<c>Wyplata.Elementy</c> / <c>WypElement.Podatki</c>) albo wykonuje operację publicznym
/// workerem płacowym (zaliczka, przeliczenie podatków, dochód, storno, bufor).
/// </para>
/// <para>
/// Testy operują wyłącznie na <b>publicznym kontrakcie</b> platformy (jak dodatek programisty
/// zewnętrznego) i na bazie Demo (GoldStandard) z automatycznym rollbackiem po teście.
/// Nie ustawiamy <c>PracownikParams.Naliczanie</c> (setter rzuca bez licencji „PL Złoty").
/// </para>
/// </summary>
[TestFixture]
public class RozdzialHrest_WyplatyTest : PracownikTestBase
{
    // ====================================================================================
    // Helpery wspólne (skopiowane z RozdzialH_WyplatyTest — ten sam, sprawdzony wzorzec KADRY-H1).
    // ====================================================================================

    // Dobiera datę wypłaty mieszczącą się w okresie etatu pracownika: koniec miesiąca początku
    // etatu, nie wcześniej niż From i nie później niż To okresu etatu.
    private static Date DataWyplatyWEtacie(Prac pracownik)
    {
        var okres = pracownik.Last.Etat.Okres;
        var from = okres.From;
        var koniecMiesiaca = new Date(from.Year, from.Month, 1).AddMonths(1).AddDays(-1);
        if (koniecMiesiaca < from) koniecMiesiaca = from;
        if (okres.To != Date.MaxValue && koniecMiesiaca > okres.To) koniecMiesiaca = okres.To;
        return koniecMiesiaca;
    }

    // Diagnostyka: powody niepoliczenia (Nienaliczeni) w czytelnym komunikacie asercji.
    private static string OpisNienaliczonych(NaliczanieWypłat wynik)
    {
        if (wynik.Nienaliczeni == null) return "(brak kolekcji Nienaliczeni)";
        var sb = new StringBuilder();
        foreach (var b in wynik.Nienaliczeni)
            sb.Append(b).Append(" | ");
        return sb.Length == 0 ? "(brak nienaliczonych)" : sb.ToString();
    }

    // Nalicza pojedynczą wypłatę etatową pracownika (wzorzec KADRY-H1) i zwraca pierwszą wypłatę.
    // Nalicz() otwiera i commituje własną transakcję — nie owijamy w InTransaction.
    private Wyplata NaliczWyplateEtatowa(Prac pracownik, Date dataWyplaty)
    {
        var pars = new NaliczanieSeryjne.PracownikParams(Context);
        pars.DataWypłaty = dataWyplaty;   // ustawia Okres i MiesiącDeklaracji automatycznie
        pars.DataListy = pars.DataWypłaty;
        pars.TypWypłaty = TypWyplaty.Etat;

        var naliczanie = new NaliczanieSeryjne.Pracownika(pars) { Pracownik = pracownik };
        NaliczanieWypłat wynik = naliczanie.Nalicz();

        var wyplaty = wynik.WszystkieWypłaty.Cast<Wyplata>().ToList();
        wyplaty.Should().NotBeEmpty(
            "naliczenie etatu pracownika Demo w okresie etatu powinno dać wypłatę; " +
            $"data={dataWyplaty}, nienaliczeni: {OpisNienaliczonych(wynik)}");
        return wyplaty[0];
    }

    // ====================================================================================
    // KADRY-H5 — Odczyt elementów wypłaty (brutto/składki/podatek/netto)
    // ====================================================================================

    [Test]
    [Description("KADRY-H5: składniki naliczonej wypłaty czytamy z Wyplata.Elementy (WypElement). " +
                 "Pola elementu: Wartosc/Netto/DoWypłaty (decimal), Podatki (subrow Podatki). " +
                 "Podatki: ZalFIS (zaliczka PIT), Emerytalna/Rentowa/Chorobowa/Zdrowotna (SkladkaZUS " +
                 "z polami Prac/Firma). Agregaty liczymy ręcznie z elementów; Wyplata.Wartosc to " +
                 "Currency (kwota do wypłaty) -> .Value na decimal.")]
    public void KADRY_H5_OdczytElementowWyplaty_WartoscNettoPodatki()
    {
        var pracownik = Pracownik(Pracownik_.Andrzejewski);
        pracownik.Should().NotBeNull();

        var dataWyplaty = DataWyplatyWEtacie(pracownik);
        var wyplata = NaliczWyplateEtatowa(pracownik, dataWyplaty);

        // Składniki muszą istnieć (wypłata etatowa zawsze ma elementy wynagrodzenia).
        var elementy = wyplata.Elementy.Cast<WypElement>().ToList();
        elementy.Should().NotBeEmpty("naliczona wypłata etatowa zawiera składniki Elementy");

        // Ręczna agregacja z elementów (wzorzec z dokumentacji KADRY-H5).
        decimal brutto = 0m, netto = 0m, zalPit = 0m, zusPrac = 0m, zusFirma = 0m;
        foreach (WypElement e in elementy)
        {
            e.Definicja.Should().NotBeNull("każdy składnik ma definicję elementu");

            brutto += e.Wartosc;        // decimal — wartość brutto składnika
            netto += e.Netto;           // decimal — wartość netto składnika

            // Struktura podatkowo-składkowa elementu.
            Podatki p = e.Podatki;
            p.Should().NotBeNull("WypElement ma subrow Podatki");
            zalPit += p.ZalFIS;         // zaliczka PIT (fiskus)

            // SkladkaZUS: Prac = część pracownika, Firma = część pracodawcy.
            zusPrac += p.Emerytalna.Prac + p.Rentowa.Prac + p.Chorobowa.Prac + p.Zdrowotna.Prac;
            zusFirma += p.Emerytalna.Firma + p.Rentowa.Firma + p.Wypadkowa.Firma;
        }

        decimal doWyplaty = wyplata.Wartosc.Value;   // Currency -> decimal

        brutto.Should().BeGreaterThan(0m, "wypłata etatowa ma dodatni przychód brutto");
        netto.Should().BeGreaterThan(0m, "wypłata etatowa ma dodatnie netto");
        zusPrac.Should().BeGreaterThan(0m, "od wynagrodzenia etatowego naliczane są składki pracownika");
        zusFirma.Should().BeGreaterThan(0m, "pracodawca opłaca część składek (narzuty)");
        doWyplaty.Should().BeGreaterThan(0m, "kwota do wypłaty jest dodatnia");
        // Zaliczka PIT bywa 0 (np. niska podstawa / ulgi) — sprawdzamy tylko brak ujemności.
        zalPit.Should().BeGreaterThanOrEqualTo(0m, "zaliczka PIT nie jest ujemna");

        SaveDispose();
    }

    [Test]
    [Description("KADRY-H5 (worker-agregator): Wyplata.PITInfoWorker (publiczny, [Context] Wypłata) udostępnia " +
                 "gotowe sumy: DoOpodatkowania/Nieopodatkowane (Currency), Razem/NettoRazem/SkładkiZUS/" +
                 "SkładkaZdrow/ZalFIS (decimal). Używamy zamiast ręcznej agregacji elementów.")]
    public void KADRY_H5_PITInfoWorker_GotoweAgregaty()
    {
        var pracownik = Pracownik(Pracownik_.Bednarek);
        pracownik.Should().NotBeNull();

        var dataWyplaty = DataWyplatyWEtacie(pracownik);
        var wyplata = NaliczWyplateEtatowa(pracownik, dataWyplaty);

        // Worker-agregator wypłaty — przypinamy wypłatę przez property Wypłata.
        var pit = new Wyplata.PITInfoWorker { Wypłata = wyplata };

        decimal razem = pit.Razem;            // przychód razem (opodatkowane + nieopodatkowane)
        decimal nettoRazem = pit.NettoRazem;  // wynagrodzenie netto razem
        decimal zus = pit.SkładkiZUS;         // składki ZUS pracownika
        decimal zaliczka = pit.ZalFIS;        // zaliczka PIT

        razem.Should().BeGreaterThan(0m, "przychód razem wypłaty etatowej jest dodatni");
        nettoRazem.Should().BeGreaterThan(0m, "netto razem jest dodatnie");
        nettoRazem.Should().BeLessThanOrEqualTo(razem, "netto nie przekracza przychodu brutto");
        zus.Should().BeGreaterThan(0m, "od etatu naliczane są składki ZUS pracownika");
        zaliczka.Should().BeGreaterThanOrEqualTo(0m, "zaliczka PIT nie jest ujemna");

        // DoOpodatkowania to Currency — konwersja przez .Value.
        pit.DoOpodatkowania.Value.Should().BeGreaterThan(0m, "podstawa opodatkowania dodatnia");

        SaveDispose();
    }

    // ====================================================================================
    // KADRY-H6 — Wypłata zaliczki (worker WypłaćZaliczkęWorker)
    // ====================================================================================

    [Test]
    [Description("KADRY-H6: zaliczkę wypłacamy publicznym workerem WypłaćZaliczkęWorker. Parametry: " +
                 "ZalParams(Context) { Data, Kwota } + ZalParams.Definicja (z WypElement.Params) — " +
                 "ISTNIEJĄCA definicja elementu z place.DefElementow o RodzajZrodla == RodzajŹródłaWypłaty.Zaliczka; " +
                 "Pracownicy: Pracownik[]. " +
                 "Akcja WypłataZaliczki() tworzy rekord Zaliczka i nalicza element realizacji; otwiera " +
                 "własną transakcję. Brak definicji zaliczki w Demo => Ignore (kontrakt workera udokumentowany).")]
    public void KADRY_H6_WyplataZaliczki_WorkerWyplacZaliczke()
    {
        var pracownik = Pracownik(Pracownik_.Bujak);
        pracownik.Should().NotBeNull();

        // Worker wymaga ISTNIEJĄCEJ definicji elementu typu zaliczka — identyfikujemy ją po publicznym
        // dyskryminatorze DefinicjaElementu.RodzajZrodla == RodzajŹródłaWypłaty.Zaliczka (brak stałej
        // DefinicjaElementu.* dla zaliczki). Sam Kod/Nazwa nie wystarcza (np. „Korekta zaliczki podatku"
        // ma RodzajZrodla == Dodatek i worker odrzuca takie podstawienie).
        DefinicjaElementu defZaliczki = Place.DefElementow.Cast<DefinicjaElementu>()
            .FirstOrDefault(d => d.RodzajZrodla == RodzajŹródłaWypłaty.Zaliczka);

        if (defZaliczki == null)
            Assert.Ignore("Baza Demo nie zawiera definicji elementu typu zaliczka — " +
                          "worker WypłaćZaliczkęWorker wymaga istniejącej DefinicjaElementu (ZalParams.Definicja). " +
                          "Kontrakt workera udokumentowany w KADRY-H6.");

        var dataWyplaty = DataWyplatyWEtacie(pracownik);

        var pars = new WypłaćZaliczkęWorker.ZalParams(Context)
        {
            Data = dataWyplaty,
            Kwota = new Currency(1000m),
        };
        pars.Definicja = defZaliczki;   // z bazowej WypElement.Params

        var worker = new WypłaćZaliczkęWorker { Params = pars, Pracownicy = new[] { pracownik } };
        object wynik = worker.WypłataZaliczki();   // tworzy Zaliczka + nalicza; własna transakcja
        wynik.Should().NotBeNull("akcja WypłataZaliczki zwraca obiekt wyniku");

        SaveDispose();

        // Po wypłaceniu zaliczki pracownik ma rekord Zaliczka z dodatnią wartością.
        var zaliczki = Place.Zaliczki.Cast<Zaliczka>()
            .Where(z => z.Pracownik != null && z.Pracownik.Guid == pracownik.Guid)
            .ToList();
        zaliczki.Should().NotBeEmpty("worker utworzył rekord Zaliczka dla pracownika");
        zaliczki.Should().Contain(z => z.Wartosc.Value > 0m, "zaliczka ma dodatnią wartość");
    }

    // ====================================================================================
    // KADRY-H7 — Przelicz składki ZUS i podatki (worker NaliczaniePodatkówMiesięcznie)
    // ====================================================================================

    [Test]
    [Description("KADRY-H7: ponowne przeliczenie składek ZUS i zaliczek PIT na elementach wypłat z bufora " +
                 "za dany miesiąc deklaracji realizuje publiczny worker NaliczaniePodatkówMiesięcznie. " +
                 "ctor przyjmuje YearMonth (miesiąc deklaracji); property Pracownik [Context]; akcja " +
                 "PrzeliczPodatki() działa we własnej transakcji. Przelicza tylko elementy z bufora " +
                 "(Wyplata.Bufor) bez ręcznej korekty podatków.")]
    public void KADRY_H7_PrzeliczPodatki_WorkerNaliczaniePodatkowMiesiecznie()
    {
        var pracownik = Pracownik(Pracownik_.Strzelecki);
        pracownik.Should().NotBeNull();

        var dataWyplaty = DataWyplatyWEtacie(pracownik);
        // Wypłata w buforze (świeżo naliczona, niezatwierdzona) — przeliczalna.
        var wyplata = NaliczWyplateEtatowa(pracownik, dataWyplaty);
        wyplata.Bufor.Should().BeTrue("świeżo naliczona wypłata jest w buforze");

        // Miesiąc deklaracji = miesiąc daty wypłaty.
        var miesiac = new YearMonth(dataWyplaty.Year, dataWyplaty.Month);

        // Sumy zaliczki PIT przed przeliczeniem (powinny być stabilne — brak zmian danych kadrowych).
        decimal zalPrzed = new Wyplata.PITInfoWorker { Wypłata = wyplata }.ZalFIS;

        var worker = new NaliczaniePodatkówMiesięcznie(miesiac) { Pracownik = pracownik };
        worker.PrzeliczPodatki();   // przelicza składki ZUS i zaliczki PIT; własna transakcja
        SaveDispose();

        // Po przeliczeniu odczytujemy wypłatę ponownie i sprawdzamy stabilność zaliczki PIT
        // (przeliczenie bez zmian danych nie powinno zmienić wyniku).
        var prac2 = Pracownik(Pracownik_.Strzelecki);
        var od = new Date(dataWyplaty.Year, dataWyplaty.Month, 1);
        var doD = new Date(dataWyplaty.Year, dataWyplaty.Month,
            DateTime.DaysInMonth(dataWyplaty.Year, dataWyplaty.Month));
        var wyplata2 = prac2.Wyplaty[(Wyplata x) => x.Data >= od && x.Data <= doD]
            .Cast<Wyplata>().First();

        decimal zalPo = new Wyplata.PITInfoWorker { Wypłata = wyplata2 }.ZalFIS;
        zalPo.Should().Be(zalPrzed,
            "przeliczenie podatków bez zmiany danych kadrowych daje tę samą zaliczkę PIT");
    }

    // ====================================================================================
    // KADRY-H8 — Dochód z wypłaty (PITInfoWorker.Dochód_*) + dochód roczny
    // ====================================================================================

    [Test]
    [Description("KADRY-H8: dochód podatkowy wypłaty czytamy z Wyplata.PITInfoWorker: Dochód_Bez26 + Dochód_26 " +
                 "(decimal), Podstawa (podstawa naliczenia zaliczki), DoOpodatkowania (Currency). " +
                 "Dochód roczny sumujemy iterując wypłaty roku (filtr serwerowy po dacie) i sumując " +
                 "Dochód_Bez26+Dochód_26 z PITInfoWorker każdej wypłaty. RozliczanieManager jest internal — " +
                 "nie wywołujemy go bezpośrednio.")]
    public void KADRY_H8_DochodZWyplaty_IDochodRoczny()
    {
        var pracownik = Pracownik(Pracownik_.Andrzejewski);
        pracownik.Should().NotBeNull();

        var dataWyplaty = DataWyplatyWEtacie(pracownik);
        var wyplata = NaliczWyplateEtatowa(pracownik, dataWyplaty);

        var pit = new Wyplata.PITInfoWorker { Wypłata = wyplata };
        decimal dochodWyplaty = pit.Dochód_Bez26 + pit.Dochód_26;

        dochodWyplaty.Should().BeGreaterThan(0m, "wypłata etatowa daje dodatni dochód podatkowy");
        pit.Podstawa.Should().BeGreaterThanOrEqualTo(0m, "podstawa naliczenia zaliczki nie jest ujemna");
        pit.DoOpodatkowania.Value.Should().BeGreaterThan(0m, "podstawa opodatkowania dodatnia");

        SaveDispose();

        // Dochód roczny: suma dochodów z wypłat roku (filtr serwerowy po dacie — bez skanu tabeli).
        int rok = dataWyplaty.Year;
        var od = new Date(rok, 1, 1);
        var doD = new Date(rok, 12, 31);

        var prac2 = Pracownik(Pracownik_.Andrzejewski);
        decimal dochodRoczny = 0m;
        foreach (Wyplata w in prac2.Wyplaty[(Wyplata x) => x.Data >= od && x.Data <= doD])
        {
            var p = new Wyplata.PITInfoWorker { Wypłata = w };
            dochodRoczny += p.Dochód_Bez26 + p.Dochód_26;
        }

        dochodRoczny.Should().BeGreaterThanOrEqualTo(dochodWyplaty,
            "dochód roczny obejmuje co najmniej naliczoną wypłatę");
    }

    [Test]
    [Ignore("KADRY-H8.B/C: PobierzDochodRocznyWorker działa tylko dla właściciela (Pracownik is Wlasciciel), " +
            "a RozliczaniePracownikowWorker tylko dla folderu pracowników zewnętrznych — pracownik " +
            "etatowy Demo \"006\" nie spełnia tych warunków. Wewnętrzny Wyplata.RozliczenieManager jest " +
            "niepubliczny. Dochód standardowego pracownika czytamy z PITInfoWorker (test KADRY-H8 wyżej).")]
    public void KADRY_H8_PobierzDochodRoczny_TylkoWlasciciel()
    {
        // Udokumentowane jako niewykonalne dla zwykłego pracownika etatowego — patrz powód w [Ignore].
    }

    // ====================================================================================
    // KADRY-H9 — Kalkulator wynagrodzeń (przez naliczenie próbne + workery agregujące)
    // ====================================================================================

    [Test]
    [Description("KADRY-H9: brak dedykowanej publicznej klasy kalkulatora — brutto/netto/koszt pracodawcy " +
                 "liczymy z naliczenia próbnego (KADRY-H1) i workerów agregujących: Wyplata.PITInfoWorker " +
                 "(brutto=Razem, netto=NettoRazem, składki pracownika=SkładkiZUS) oraz Wyplata.WyplataSkładkiWorker " +
                 "(Razem: ZestawienieSkładek z Narzuty = narzuty pracodawcy). " +
                 "Koszt pracodawcy ≈ brutto + Narzuty. Naliczenie próbne nie wymaga Save().")]
    public void KADRY_H9_KalkulatorWynagrodzen_NaliczenieProbne()
    {
        var pracownik = Pracownik(Pracownik_.Bednarek);
        pracownik.Should().NotBeNull();

        var dataWyplaty = DataWyplatyWEtacie(pracownik);
        var wyplata = NaliczWyplateEtatowa(pracownik, dataWyplaty);

        var pit = new Wyplata.PITInfoWorker { Wypłata = wyplata };
        var skl = new WyplataSkładkiWorker { Wypłata = wyplata };

        decimal brutto = pit.Razem;
        decimal netto = pit.NettoRazem;
        decimal narzuty = skl.Razem.Narzuty;          // narzuty pracodawcy (ZUS firmy + FP/FGŚP/FEP)
        decimal kosztPracodawcy = brutto + narzuty;

        brutto.Should().BeGreaterThan(0m, "brutto dodatnie");
        netto.Should().BeGreaterThan(0m, "netto dodatnie");
        netto.Should().BeLessThanOrEqualTo(brutto, "netto nie przekracza brutto");
        narzuty.Should().BeGreaterThan(0m, "pracodawca ponosi narzuty na wynagrodzenie etatowe");
        kosztPracodawcy.Should().BeGreaterThan(brutto, "koszt pracodawcy = brutto + narzuty > brutto");

        // Składki pracownika i firmy są spójne z ZestawienieSkładek.
        skl.Razem.KosztyZUS.Should().BeGreaterThan(0m, "składki ZUS pracownika dodatnie");
        skl.Razem.FirmaZUS.Should().BeGreaterThan(0m, "składki ZUS pracodawcy dodatnie");

        // To była kalkulacja — nie utrwalamy (Save pominięty świadomie; rollback i tak wycofa).
    }

    // ====================================================================================
    // KADRY-H10 — Stornowanie elementów wypłaty
    // ====================================================================================

    [Test]
    [Description("KADRY-H10: oznaczenie elementu do storna realizuje publiczny worker " +
                 "StornoElementu.ElementDoPrzeliczeniaWorker (na WypElement): ZaznaczElementDoAnulowania()/" +
                 "ZaznaczElementDoPrzeliczenia()/WycofajZaznaczenie(). Oznaczać można tylko elementy wypłaty " +
                 "ZATWIERDZONEJ w stanie StanStorna == NieDotyczy. Najpierw zatwierdzamy wypłatę " +
                 "(Wyplata.ZatwierdźWorker, property Lista), potem oznaczamy i sprawdzamy StanStorna/Storno. " +
                 "Wytworzenie elementu stornującego (Wystornowany/Stornujący) następuje przy ponownym naliczeniu.")]
    public void KADRY_H10_StornowanieElementu_WorkerElementDoPrzeliczenia()
    {
        var pracownik = Pracownik(Pracownik_.Bujak);
        pracownik.Should().NotBeNull();

        var dataWyplaty = DataWyplatyWEtacie(pracownik);
        var wyplata = NaliczWyplateEtatowa(pracownik, dataWyplaty);

        // Storno dotyczy wypłaty ZATWIERDZONEJ — zatwierdzamy ją workerem (property Lista, nie Wypłata).
        new Wyplata.ZatwierdźWorker { Lista = wyplata }.Zatwierdź();
        SaveDispose();

        var prac2 = Pracownik(Pracownik_.Bujak);
        var od = new Date(dataWyplaty.Year, dataWyplaty.Month, 1);
        var doD = new Date(dataWyplaty.Year, dataWyplaty.Month,
            DateTime.DaysInMonth(dataWyplaty.Year, dataWyplaty.Month));
        var wyplata2 = prac2.Wyplaty[(Wyplata x) => x.Data >= od && x.Data <= doD]
            .Cast<Wyplata>().First();
        wyplata2.Zatwierdzona.Should().BeTrue("po Zatwierdź() wypłata jest zatwierdzona");

        // Wybieramy element w stanie NieDotyczy (kandydat do storna).
        WypElement element = wyplata2.Elementy.Cast<WypElement>()
            .First(e => e.StanStorna == StanStornaElementu.NieDotyczy);

        // Oznaczamy element do anulowania — worker otwiera własną transakcję.
        var worker = new StornoElementu.ElementDoPrzeliczeniaWorker { Element = element };
        worker.ZaznaczElementDoAnulowania();
        SaveDispose();

        // Po oznaczeniu element jest DoStornowania i ma powiązany rekord Storno.
        var prac3 = Pracownik(Pracownik_.Bujak);
        var wyplata3 = prac3.Wyplaty[(Wyplata x) => x.Data >= od && x.Data <= doD]
            .Cast<Wyplata>().First();
        WypElement element3 = wyplata3.Elementy.Cast<WypElement>()
            .First(e => e.StanStorna == StanStornaElementu.DoStornowania);

        element3.StanStorna.Should().Be(StanStornaElementu.DoStornowania,
            "oznaczenie ustawia element na DoStornowania");
        element3.Storno.Should().NotBeNull("oznaczenie tworzy powiązany rekord StornoElementu");
    }

    // ====================================================================================
    // KADRY-H11 — Anulowanie/usunięcie naliczonej wypłaty (bufor)
    // ====================================================================================

    [Test]
    [Description("KADRY-H11: powrót zatwierdzonej wypłaty do bufora (do ponownego naliczenia) realizuje " +
                 "publiczny worker Wyplata.OtwórzWorker (property Wypłata, akcja Otwórz() => Zatwierdzona=false), " +
                 "zatwierdzanie — Wyplata.ZatwierdźWorker (property Lista). CanBufor jest protected (niedostępny " +
                 "z dodatku). Po Otwórz() wypłata jest znów w buforze i można ją przeliczyć ponownie (KADRY-H1).")]
    public void KADRY_H11_PowrotDoBufora_WorkerOtworz()
    {
        var pracownik = Pracownik(Pracownik_.Strzelecki);
        pracownik.Should().NotBeNull();

        var dataWyplaty = DataWyplatyWEtacie(pracownik);
        var wyplata = NaliczWyplateEtatowa(pracownik, dataWyplaty);
        wyplata.Bufor.Should().BeTrue("świeżo naliczona wypłata jest w buforze");

        // Zatwierdzamy (zejście z bufora).
        new Wyplata.ZatwierdźWorker { Lista = wyplata }.Zatwierdź();
        SaveDispose();

        var od = new Date(dataWyplaty.Year, dataWyplaty.Month, 1);
        var doD = new Date(dataWyplaty.Year, dataWyplaty.Month,
            DateTime.DaysInMonth(dataWyplaty.Year, dataWyplaty.Month));

        var prac2 = Pracownik(Pracownik_.Strzelecki);
        var wyplata2 = prac2.Wyplaty[(Wyplata x) => x.Data >= od && x.Data <= doD]
            .Cast<Wyplata>().First();
        wyplata2.Zatwierdzona.Should().BeTrue("po Zatwierdź() wypłata jest zatwierdzona");
        wyplata2.Bufor.Should().BeFalse("zatwierdzona wypłata nie jest w buforze");

        // Powrót do bufora workerem OtwórzWorker.
        new Wyplata.OtwórzWorker { Wypłata = wyplata2 }.Otwórz();
        SaveDispose();

        var prac3 = Pracownik(Pracownik_.Strzelecki);
        var wyplata3 = prac3.Wyplaty[(Wyplata x) => x.Data >= od && x.Data <= doD]
            .Cast<Wyplata>().First();
        wyplata3.Bufor.Should().BeTrue("po Otwórz() wypłata wraca do bufora");
        wyplata3.Zatwierdzona.Should().BeFalse("po Otwórz() wypłata nie jest zatwierdzona");
    }
}
