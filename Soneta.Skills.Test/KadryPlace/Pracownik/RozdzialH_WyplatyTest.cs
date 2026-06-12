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
/// Rozdział H — „Płace: naliczanie wypłat" (receptury KADRY-H1, KADRY-H2, KADRY-H3, KADRY-H4).
/// <para>
/// Testy są <b>wykonywalną dokumentacją</b> publicznego kontraktu naliczania płac w Soneta.
/// Naliczanie realizuje worker <c>Soneta.Place.NaliczanieSeryjne</c> z zagnieżdżonymi klasami
/// parametrów (<c>PracownikParams</c>, <c>UmowaParams</c>) oraz wykonawców
/// (<c>NaliczanieSeryjne.Pracownika</c>, <c>NaliczanieSeryjne.Umowy</c>). Wynikiem jest
/// <c>NaliczanieWypłat</c> z kolekcją <c>WszystkieWypłaty: IList</c> (elementy <c>Wyplata</c>)
/// oraz <c>Nienaliczeni</c> (powody niepowodzenia). <c>Nalicz()</c> sam otwiera i commituje
/// transakcję w sesji — nie owijamy go w dodatkową transakcję.
/// </para>
/// <para>
/// Wszystko działa na bazie Demo (GoldStandard) z automatycznym rollbackiem po teście.
/// Pracownik "006" ma jeden zapis historii — datę wypłaty dobieramy dynamicznie tak, by mieściła
/// się w okresie aktywnego etatu (<c>pracownik.Last.Etat.Okres</c>). Operujemy wyłącznie na
/// <b>publicznym kontrakcie</b> platformy.
/// </para>
/// </summary>
[TestFixture]
public class RozdzialH_WyplatyTest : PracownikTestBase
{
    // Dobiera datę wypłaty mieszczącą się w okresie etatu pracownika: bierzemy ostatni dzień
    // miesiąca początku etatu, ale nie wcześniej niż From i nie później niż To okresu etatu.
    // Dla pracowników Demo etat zwykle zaczyna się wiele lat wstecz i jest otwarty (To = MaxValue),
    // więc bezpieczną, deterministyczną datą jest koniec miesiąca rozpoczęcia zatrudnienia.
    private static Date DataWyplatyWEtacie(Prac pracownik)
    {
        var okres = pracownik.Last.Etat.Okres;
        var from = okres.From;
        // Koniec miesiąca rozpoczęcia etatu (28-31 dzień).
        var koniecMiesiaca = new Date(from.Year, from.Month, 1).AddMonths(1).AddDays(-1);
        if (koniecMiesiaca < from) koniecMiesiaca = from;
        if (okres.To != Date.MaxValue && koniecMiesiaca > okres.To) koniecMiesiaca = okres.To;
        return koniecMiesiaca;
    }

    // Diagnostyka: zbiera powody niepoliczenia (Nienaliczeni) do czytelnego komunikatu asercji.
    private static string OpisNienaliczonych(NaliczanieWypłat wynik)
    {
        if (wynik.Nienaliczeni == null) return "(brak kolekcji Nienaliczeni)";
        var sb = new StringBuilder();
        foreach (var b in wynik.Nienaliczeni)
            sb.Append(b).Append(" | ");
        return sb.Length == 0 ? "(brak nienaliczonych)" : sb.ToString();
    }

    // ============================== KADRY-H1 — Naliczanie wypłat etatowych ==============================

    [Test]
    [Description("KADRY-H1: wypłatę etatową naliczamy workerem NaliczanieSeryjne. Parametry: " +
                 "new NaliczanieSeryjne.PracownikParams(Context); DataWypłaty (ustawia Okres i " +
                 "MiesiącDeklaracji automatycznie); DataListy; TypWypłaty = Etat. NIE ustawiamy " +
                 "Naliczanie (domyślnie PłatnaZDołu). Wykonawca: new NaliczanieSeryjne.Pracownika(pars) " +
                 "{ Pracownik = p }.Nalicz() — sam commituje w sesji. Wynik: WszystkieWypłaty (IList).")]
    public void KADRY_H1_WyplataEtatowa_NaliczanaWorkeremNaliczanieSeryjne()
    {
        var pracownik = Pracownik(Pracownik_.Andrzejewski);
        pracownik.Should().NotBeNull();

        // Datę wypłaty dobieramy w obrębie aktywnego etatu pracownika.
        var dataWyplaty = DataWyplatyWEtacie(pracownik);

        // Parametry naliczania — Context z tej samej sesji co pracownik (TestBase.Context).
        var pars = new NaliczanieSeryjne.PracownikParams(Context);
        pars.DataWypłaty = dataWyplaty;     // ustawia Okres i MiesiącDeklaracji automatycznie
        pars.DataListy = pars.DataWypłaty;
        // pars.Naliczanie pozostaje domyślnie PłatnaZDołu (setter rzuca bez licencji PL Złoty).
        pars.TypWypłaty = TypWyplaty.Etat;  // tylko wypłaty etatowe

        // Nalicz() otwiera własną transakcję i commituje — nie owijamy w InTransaction.
        var naliczanie = new NaliczanieSeryjne.Pracownika(pars) { Pracownik = pracownik };
        NaliczanieWypłat wynik = naliczanie.Nalicz();

        // Diagnostyka: jeśli nic nie naliczono, powód jest w Nienaliczeni.
        var wyplaty = wynik.WszystkieWypłaty.Cast<Wyplata>().ToList();
        wyplaty.Should().NotBeEmpty(
            "naliczanie etatu dla pracownika Demo w okresie etatu powinno dać wypłatę; " +
            $"data={dataWyplaty}, nienaliczeni: {OpisNienaliczonych(wynik)}");

        // Naliczona wypłata jest typu etatowego i wiąże się z pracownikiem.
        var w = wyplaty[0];
        w.Typ.Should().Be(TypWyplaty.Etat, "filtr TypWypłaty = Etat");
        w.Pracownik.Should().Be(pracownik);
        w.Data.Should().Be(dataWyplaty, "data wypłaty wg DataWypłaty parametrów");

        SaveDispose();   // utrwalenie w bazie (rollback po teście i tak wycofa)
    }

    // ============================== KADRY-H2 — Naliczanie wypłat z umów ==============================

    [Test]
    [Description("KADRY-H2: wypłatę z umowy cywilnoprawnej naliczamy wykonawcą NaliczanieSeryjne.Umowy. " +
                 "Najpierw tworzymy umowę zlecenie (jak w KADRY-G1), potem: " +
                 "new NaliczanieSeryjne.Umowy(new UmowaParams(Context)) { Umowa = u }.Nalicz(). " +
                 "Ustawienie Umowa nadpisuje Pracownik. NIE ustawiamy UmowaParams.Naliczanie " +
                 "(setter rzuca NotSupportedException — umowy zawsze płatne z dołu).")]
    public void KADRY_H2_WyplataZUmowy_NaliczanaWykonawcaUmowy()
    {
        var pracownik = Pracownik(Pracownik_.Bednarek);
        pracownik.Should().NotBeNull();

        // Datę wypłaty (i okres umowy) dobieramy w obrębie aktywnego etatu pracownika.
        var dataWyplaty = DataWyplatyWEtacie(pracownik);
        var okresUmowy = new FromTo(new Date(dataWyplaty.Year, dataWyplaty.Month, 1), dataWyplaty);

        // 1) Tworzymy umowę zlecenie (mechanizm jak w sekcji G) — tworzenie danych operacyjnych
        //    MUSI być w trybie edycji (InTransaction), inaczej AddRow rzuca CannotEditException.
        Guid guidUmowy = Guid.Empty;
        InTransaction(() =>
        {
            var u = Session.AddRow(new Umowa(pracownik));
            u.Element = Place.DefElementow[DefinicjaElementu.UmowaZlecenie] as DefinicjaElementu;
            u.Data = okresUmowy.From;
            u.Okres = okresUmowy;
            u.Tytul = "Umowa zlecenie - naliczanie KADRY-H2";
            u.RodzajRozliczenia = RodzajeRozliczeniaUmowy.KwotaDoWypłaty;
            u.TypWartosci = TypWartosciUmowy.Brutto;
            u.Wydzial = Kadry.Wydzialy.Firma;       // jednostka organizacyjna wymagana przy zapisie
            u.Last.Wartosc = new Currency(3000m);   // kwota na zapisie historycznym
            guidUmowy = u.Guid;
        });
        SaveDispose();                              // utrwalamy umowę przed naliczaniem

        var umowa = Get<Umowa>(guidUmowy);

        // 2) Naliczanie wypłaty z umowy.
        var pars = new NaliczanieSeryjne.UmowaParams(Context);
        pars.DataWypłaty = dataWyplaty;
        pars.DataListy = pars.DataWypłaty;
        // pars.Naliczanie NIE jest ustawiane (NotSupportedException).

        var naliczanie = new NaliczanieSeryjne.Umowy(pars) { Umowa = umowa };
        NaliczanieWypłat wynik = naliczanie.Nalicz();

        var wyplaty = wynik.WszystkieWypłaty.Cast<Wyplata>().ToList();
        wyplaty.Should().NotBeEmpty(
            "naliczanie umowy zlecenie powinno dać wypłatę typu Umowa; " +
            $"data={dataWyplaty}, nienaliczeni: {OpisNienaliczonych(wynik)}");

        var w = wyplaty[0];
        w.Typ.Should().Be(TypWyplaty.Umowa, "wypłata z umowy ma typ Umowa");
        // Porównujemy po Guid (różne instancje Row po SaveDispose/re-fetch).
        w.Pracownik.Guid.Should().Be(pracownik.Guid,
            "ustawienie Umowa nadpisuje Pracownik na właściciela umowy");

        SaveDispose();
    }

    // ============================== KADRY-H3 — Naliczanie pozostałych wypłat ==============================

    [Test]
    [Description("KADRY-H3: pozostałe wypłaty naliczamy tym samym wykonawcą co etat " +
                 "(NaliczanieSeryjne.Pracownika), sterując PracownikParams.TypWypłaty = Inne. " +
                 "Opcjonalnie PracownikParams.Dodatek = DefinicjaElementu zawęża do jednego składnika. " +
                 "Wynik czytamy przez Wyplata.Elementy (WypElement: Definicja, Nazwa, Wartosc).")]
    public void KADRY_H3_PozostaleWyplaty_TypWyplatyInne()
    {
        var pracownik = Pracownik(Pracownik_.Bujak);
        pracownik.Should().NotBeNull();

        var dataWyplaty = DataWyplatyWEtacie(pracownik);

        var pars = new NaliczanieSeryjne.PracownikParams(Context);
        pars.DataWypłaty = dataWyplaty;
        pars.DataListy = pars.DataWypłaty;
        pars.TypWypłaty = TypWyplaty.Inne;   // tylko pozostałe składniki (bez etatu)

        var naliczanie = new NaliczanieSeryjne.Pracownika(pars) { Pracownik = pracownik };
        NaliczanieWypłat wynik = naliczanie.Nalicz();

        // Pracownik Demo bez dodatkowych składników "Inne" może nie mieć nic do naliczenia —
        // to poprawne zachowanie (puste WszystkieWypłaty, BEZ wyjątku i bez Nienaliczonych-błędów).
        // Dokumentujemy więc kontrakt: naliczanie zwraca obiekt wyniku, a wszelkie naliczone
        // wypłaty są typu Inne. Asercja nie wymaga niepustego wyniku (zależy od danych pracownika).
        wynik.Should().NotBeNull("Nalicz() zawsze zwraca obiekt NaliczanieWypłat");

        var wyplaty = wynik.WszystkieWypłaty.Cast<Wyplata>().ToList();
        foreach (var w in wyplaty)
        {
            w.Typ.Should().Be(TypWyplaty.Inne, "filtr TypWypłaty = Inne");
            // Składniki wynagrodzenia: WypElement (Definicja, Nazwa, Wartosc).
            foreach (WypElement e in w.Elementy)
            {
                e.Definicja.Should().NotBeNull("każdy składnik ma definicję elementu");
            }
        }

        SaveDispose();
    }

    // ============================== KADRY-H4 — Odczyt wypłat za rok ==============================

    [Test]
    [Description("KADRY-H4: po naliczeniu wypłaty etatowej (KADRY-H1) odczytujemy wypłaty pracownika za rok " +
                 "filtrem serwerowym pracownik.Wyplaty[(Wyplata x) => x.Data >= od && x.Data <= doD]. " +
                 "Sumujemy Wartosc (Currency, kwota do wypłaty) oraz składniki Elementy " +
                 "(WypElement.Wartosc/.Netto, decimal). UWAGA: WyplataEtat nie ma CLR-property " +
                 "Brutto/Netto (wbrew dokumentacji) — agregujemy przez Wartosc i składniki Elementy.")]
    public void KADRY_H4_OdczytWyplatZaRok_FiltrSerwerowyPoDacie()
    {
        var pracownik = Pracownik(Pracownik_.Strzelecki);
        pracownik.Should().NotBeNull();

        var dataWyplaty = DataWyplatyWEtacie(pracownik);

        // Najpierw nalicz wypłatę etatową, by mieć co odczytywać (KADRY-H1 jako warunek wstępny KADRY-H4).
        var pars = new NaliczanieSeryjne.PracownikParams(Context);
        pars.DataWypłaty = dataWyplaty;
        pars.DataListy = pars.DataWypłaty;
        pars.TypWypłaty = TypWyplaty.Etat;

        var naliczanie = new NaliczanieSeryjne.Pracownika(pars) { Pracownik = pracownik };
        var wynikNaliczania = naliczanie.Nalicz();
        wynikNaliczania.WszystkieWypłaty.Cast<Wyplata>().Should().NotBeEmpty(
            $"warunek wstępny KADRY-H4: wypłata etatowa musi się naliczyć; data={dataWyplaty}, " +
            $"nienaliczeni: {OpisNienaliczonych(wynikNaliczania)}");
        SaveDispose();

        // Odczyt: filtr serwerowy po dacie wypłaty (cały rok), bez pełnego skanu tabeli operacyjnej.
        int rok = dataWyplaty.Year;
        var od = new Date(rok, 1, 1);
        var doD = new Date(rok, 12, 31);

        var pracownik2 = Pracownik(Pracownik_.Strzelecki);
        var wyplaty = pracownik2.Wyplaty[(Wyplata x) => x.Data >= od && x.Data <= doD]
            .Cast<Wyplata>().ToList();

        wyplaty.Should().NotBeEmpty("po naliczeniu wypłata mieści się w roku odczytu");

        // Agregacja: suma do wypłaty (Currency.Value -> decimal) i suma składników.
        decimal sumaDoWyplaty = 0m;
        decimal sumaSkladnikow = 0m;
        bool maEtat = false;

        foreach (var w in wyplaty)
        {
            sumaDoWyplaty += w.Wartosc.Value;       // kwota do wypłaty; Currency.Value -> decimal

            if (w is WyplataEtat)                   // typ etatowy (agregatów Brutto/Netto brak na CLR)
                maEtat = true;

            foreach (WypElement e in w.Elementy)
                sumaSkladnikow += e.Wartosc;        // wartość składnika (decimal)
        }

        maEtat.Should().BeTrue("naliczyliśmy wypłatę etatową (WyplataEtat)");
        sumaSkladnikow.Should().NotBe(0m, "wypłata zawiera składniki (Elementy)");
        sumaDoWyplaty.Should().BeGreaterThan(0m, "kwota do wypłaty jest dodatnia");
    }
}
