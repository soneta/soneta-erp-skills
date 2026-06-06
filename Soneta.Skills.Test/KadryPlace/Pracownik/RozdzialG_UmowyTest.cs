using System;
using System.Linq;
using AwesomeAssertions;
using NUnit.Framework;
using Soneta.Kadry;
using Soneta.Place;
using Soneta.Types;
using Prac = Soneta.Kadry.Pracownik;

namespace Soneta.Skills.Test.KadryPlace.Pracownik;

/// <summary>
/// Rozdział G — „Umowy cywilnoprawne" (receptury G1, G2).
/// <para>
/// Testy są <b>wykonywalną dokumentacją</b> publicznego kontraktu platformy Soneta dla umów
/// cywilnoprawnych pracownika. <c>Soneta.Kadry.Umowa</c> to <b>root historyczny</b> (tabela
/// <c>Umowy</c>, child pracownika): dane nagłówkowe (definicja elementu = rodzaj umowy, okres,
/// sposób rozliczenia, typ wartości) siedzą na roocie, a <b>kwota/wartość umowy</b> jest historyczna
/// i siedzi na <c>UmowaHistoria.Wartosc</c> (zapis <c>umowa.Last</c>).
/// </para>
/// <para>
/// Wszystko działa na bazie Demo (GoldStandard) z automatycznym rollbackiem po teście. Pracownicy
/// etatowi z Demo (kody "006".."039") nie mają jeszcze umów cywilnoprawnych — to czysty punkt
/// wejścia dla asercji. Operujemy wyłącznie na <b>publicznym kontrakcie</b> — tak jak dodatek
/// programisty zewnętrznego bez dostępu do kodu źródłowego aplikacji.
/// </para>
/// </summary>
[TestFixture]
public class RozdzialG_UmowyTest : PracownikTestBase
{
    // Pobranie definicji elementu = rodzaju umowy ze słownika konfiguracyjnego po stałej Guid.
    // Indeksator DefElementow[Guid] zwraca definicję; rzutujemy na DefinicjaElementu.
    private DefinicjaElementu DefUmowy(Guid rodzaj) =>
        Place.DefElementow[rodzaj] as DefinicjaElementu;

    // ============================== G1 — Dodawanie umów cywilnoprawnych ==============================

    [Test]
    [Description("G1: umowę zlecenie tworzymy przez Session.AddRow(new Umowa(pracownik)); w OnAdded " +
                 "powstaje pierwszy zapis UmowaHistoria (umowa.Last). Element = rodzaj umowy " +
                 "(DefElementow[DefinicjaElementu.UmowaZlecenie]); dane nagłówkowe na roocie, " +
                 "a kwota (Wartosc) na zapisie historycznym Last. Odczyt z pracownik.Umowy.")]
    public void G1_UmowaZlecenie_DodawanaZElementemIWartosciaNaLast()
    {
        // Definicja elementu płacowego = rodzaj umowy (zlecenie) ze słownika konfiguracyjnego.
        var defZlecenie = DefUmowy(DefinicjaElementu.UmowaZlecenie);
        defZlecenie.Should().NotBeNull("baza Demo zawiera definicję umowy zlecenie (stała Guid)");
        // Element przyjmuje tylko definicje o RodzajZrodla == Umowa.
        defZlecenie.RodzajZrodla.Should().Be(RodzajŹródłaWypłaty.Umowa,
            "definicja umowy zlecenie ma źródło typu Umowa");

        Guid guidPrac = Guid.Empty;
        var okres = new FromTo(new Date(2026, 1, 1), new Date(2026, 12, 31));

        InTransaction(() =>
        {
            // Pracownik z Demo nie ma umów cywilnoprawnych — czysty punkt wejścia.
            var pracownik = Pracownik(Pracownik_.Andrzejewski);
            pracownik.Should().NotBeNull();
            pracownik.Umowy.Cast<Umowa>().Should().BeEmpty("pracownik Demo nie ma jeszcze umów");

            // 1) Utworzenie umowy + dodanie do tabeli; w OnAdded powstaje pierwszy UmowaHistoria.
            //    NIE tworzymy UmowaHistoria ręcznie — od razu mamy umowa.Last.
            var umowa = Session.AddRow(new Umowa(pracownik));
            umowa.Last.Should().NotBeNull("OnAdded tworzy pierwszy zapis historii (Last)");

            // 2) Definicja elementu = rodzaj umowy (zlecenie).
            umowa.Element = defZlecenie;

            // 3) Dane nagłówkowe na roocie:
            umowa.Data = new Date(2026, 1, 1);
            umowa.Okres = okres;
            umowa.Tytul = "Umowa zlecenie - obsługa projektu";
            umowa.RodzajRozliczenia = RodzajeRozliczeniaUmowy.KwotaDoWypłaty;
            umowa.TypWartosci = TypWartosciUmowy.Brutto;
            // Jednostka organizacyjna (Wydzial) jest WYMAGANA przez weryfikator przy Save
            // (WydzialRequiredVerifier) — wskazujemy korzeń struktury (Wydzialy.Firma).
            umowa.Wydzial = Kadry.Wydzialy.Firma;

            // 4) KWOTA umowy — na zapisie historycznym Last (UmowaHistoria.Wartosc), nie na roocie.
            //    umowa.Wartosc/umowa.Brutto na roocie są wyliczane (read-only).
            umowa.Last.Wartosc = new Currency(5000m);

            guidPrac = pracownik.Guid;
        });
        SaveDispose();

        // Odczyt: umowa pojawia się w kolekcji childów pracownika (pracownik.Umowy).
        var pracownik2 = Get<Prac>(guidPrac);
        var umowy = pracownik2.Umowy.Cast<Umowa>().ToList();
        umowy.Should().ContainSingle("dodaliśmy jedną umowę cywilnoprawną");

        var u = umowy[0];
        u.Element.Should().NotBeNull("Element (rodzaj umowy) jest wymagany");
        u.Element.RodzajZrodla.Should().Be(RodzajŹródłaWypłaty.Umowa);
        u.Tytul.Should().Be("Umowa zlecenie - obsługa projektu");
        u.RodzajRozliczenia.Should().Be(RodzajeRozliczeniaUmowy.KwotaDoWypłaty);
        u.TypWartosci.Should().Be(TypWartosciUmowy.Brutto);
        u.Okres.From.Should().Be(okres.From);
        u.Okres.To.Should().Be(okres.To);
        // Kwota odczytana z zapisu historycznego Last.
        u.Last.Wartosc.Should().Be(new Currency(5000m));
    }

    [Test]
    [Description("G1 (o dzieło): wariant rodzaju umowy wskazujemy inną definicją elementu — " +
                 "DefElementow[DefinicjaElementu.Umowa20] (umowa o dzieło 20% KUP). Mechanizm " +
                 "tworzenia identyczny jak dla zlecenia (root + zapis historyczny Last).")]
    public void G1_UmowaODzielo_WskazywanaInnaDefinicjaElementu()
    {
        // Wariant „o dzieło" = definicja Umowa20 (20% KUP).
        var defDzielo = DefUmowy(DefinicjaElementu.Umowa20);
        defDzielo.Should().NotBeNull("baza Demo zawiera definicję umowy o dzieło (Umowa20)");
        defDzielo.RodzajZrodla.Should().Be(RodzajŹródłaWypłaty.Umowa);

        Guid guidPrac = Guid.Empty;
        var okres = new FromTo(new Date(2026, 3, 1), new Date(2026, 5, 31));

        InTransaction(() =>
        {
            var pracownik = Pracownik(Pracownik_.Bednarek);
            var umowa = Session.AddRow(new Umowa(pracownik));
            umowa.Element = defDzielo;
            umowa.Data = new Date(2026, 3, 1);
            umowa.Okres = okres;
            umowa.Tytul = "Umowa o dzieło - projekt graficzny";
            umowa.RodzajRozliczenia = RodzajeRozliczeniaUmowy.KwotaDoWypłaty;
            umowa.TypWartosci = TypWartosciUmowy.Brutto;
            umowa.Wydzial = Kadry.Wydzialy.Firma;   // jednostka organizacyjna wymagana przy Save
            umowa.Last.Wartosc = new Currency(3000m);

            guidPrac = pracownik.Guid;
        });
        SaveDispose();

        var u = Get<Prac>(guidPrac).Umowy.Cast<Umowa>().Single();
        u.Element.RodzajZrodla.Should().Be(RodzajŹródłaWypłaty.Umowa);
        u.Tytul.Should().Be("Umowa o dzieło - projekt graficzny");
        u.Last.Wartosc.Should().Be(new Currency(3000m));
    }

    [Test]
    [Description("G1 (warianty rodzaju): stałe Guid definicji elementów umów (UmowaZlecenie, Umowa20, " +
                 "UmowaRyczałtowa) wskazują w słowniku DefElementow definicje o RodzajZrodla == Umowa.")]
    public void G1_StaleDefinicjiElementow_WskazujaDefinicjeOZrodleUmowa()
    {
        // Dokumentujemy warianty rodzaju umowy bez modyfikacji danych — same stałe + słownik.
        foreach (var rodzaj in new[]
                 {
                     DefinicjaElementu.UmowaZlecenie,
                     DefinicjaElementu.Umowa20,
                     DefinicjaElementu.UmowaRyczałtowa,
                 })
        {
            var def = DefUmowy(rodzaj);
            def.Should().NotBeNull("definicja elementu umowy o danej stałej Guid istnieje w Demo");
            def.RodzajZrodla.Should().Be(RodzajŹródłaWypłaty.Umowa,
                "tylko definicje o źródle Umowa są akceptowane jako rodzaj umowy");
        }
    }

    // ============================== G2 — Zmiana/aneks umowy ==============================

    [Test]
    [Description("G2 (korekta): zmiana danych nagłówkowych umowy (Tytul, Okres) w bieżącym okresie — " +
                 "bez Update/AddRow. Liczba zapisów historii się nie zmienia.")]
    public void G2_Korekta_ZmieniaNaglowekBezNowegoOkresu()
    {
        Guid guidUmowy = Guid.Empty;
        var okresPocz = new FromTo(new Date(2026, 1, 1), new Date(2026, 12, 31));

        InTransaction(() =>
        {
            var pracownik = Pracownik(Pracownik_.Bujak);
            var umowa = Session.AddRow(new Umowa(pracownik));
            umowa.Element = DefUmowy(DefinicjaElementu.UmowaZlecenie);
            umowa.Data = new Date(2026, 1, 1);
            umowa.Okres = okresPocz;
            umowa.Tytul = "Umowa zlecenie";
            umowa.RodzajRozliczenia = RodzajeRozliczeniaUmowy.KwotaDoWypłaty;
            umowa.TypWartosci = TypWartosciUmowy.Brutto;
            umowa.Wydzial = Kadry.Wydzialy.Firma;   // jednostka organizacyjna wymagana przy Save
            umowa.Last.Wartosc = new Currency(4000m);
            guidUmowy = umowa.Guid;
        });
        SaveDispose();

        // Korekta: modyfikujemy dane nagłówkowe — bez Update, bez AddRow.
        InTransaction(() =>
        {
            var umowa = Get<Umowa>(guidUmowy);
            umowa.Tytul = "Umowa zlecenie - aneks zakresu prac";
            umowa.Okres = new FromTo(umowa.Okres.From, new Date(2027, 6, 30));   // przedłużenie
        });
        SaveDispose();

        var u2 = Get<Umowa>(guidUmowy);
        u2.Tytul.Should().Be("Umowa zlecenie - aneks zakresu prac");
        u2.Okres.To.Should().Be(new Date(2027, 6, 30), "przedłużono okres umowy");
        // Korekta nie dzieli okresu — nadal jeden zapis historii.
        u2.Historia.Cast<UmowaHistoria>().Should().ContainSingle("korekta nie tworzy nowego okresu");
    }

    [Test]
    [Description("G2 (aneks 'od daty'): Historia.Update(odDnia) klonuje zapis aktualny na odDnia, " +
                 "skraca stary do odDnia-1 i zwraca NOWY klon (okres od odDnia); klon dodajemy do " +
                 "tabeli UmowaHistorie i ustawiamy na nim nową Wartosc.")]
    public void G2_AneksOdDaty_TworzyNowyZapisHistoriiOdDnia_ISkracaStary()
    {
        Guid guidUmowy = Guid.Empty;
        var odDnia = new Date(2026, 7, 1);

        InTransaction(() =>
        {
            var pracownik = Pracownik(Pracownik_.Strzelecki);
            var umowa = Session.AddRow(new Umowa(pracownik));
            umowa.Element = DefUmowy(DefinicjaElementu.UmowaZlecenie);
            umowa.Data = new Date(2026, 1, 1);
            umowa.Okres = new FromTo(new Date(2026, 1, 1), new Date(2026, 12, 31));
            umowa.Tytul = "Umowa zlecenie";
            umowa.RodzajRozliczenia = RodzajeRozliczeniaUmowy.KwotaDoWypłaty;
            umowa.TypWartosci = TypWartosciUmowy.Brutto;
            umowa.Wydzial = Kadry.Wydzialy.Firma;   // jednostka organizacyjna wymagana przy Save
            umowa.Last.Wartosc = new Currency(5000m);   // wartość początkowa
            guidUmowy = umowa.Guid;
        });
        SaveDispose();

        // Aneks „od daty": nowy zapis historyczny obowiązujący od odDnia (analogicznie do PracHistoria/A14).
        InTransaction(() =>
        {
            var umowa = Get<Umowa>(guidUmowy);

            // 1) Update klonuje zapis aktualny na odDnia, skraca stary do dnia poprzedniego
            //    i zwraca NOWY klon z okresem od odDnia.
            var nowy = (UmowaHistoria)umowa.Historia.Update(odDnia);
            // 2) Update + AddRow to nierozłączna para — bez AddRow klon zostaje „odpięty".
            umowa.Module.UmowaHistorie.AddRow(nowy);
            // 3) Na nowym zapisie ustawiamy zmienioną wartość (od odDnia).
            //    UWAGA: UmowaHistoria.PowodAktualizacji jest TYLKO DO ODCZYTU (brak settera),
            //    mimo że skan oznaczał je jako pole bazodanowe — nie ustawiamy go w kodzie.
            nowy.Wartosc = new Currency(6000m);
        });
        SaveDispose();

        var u2 = Get<Umowa>(guidUmowy);
        // Mamy teraz dwa zapisy: stary (do odDnia-1) i nowy (od odDnia).
        var zapisy = u2.Historia.Cast<UmowaHistoria>().OrderBy(h => h.Aktualnosc.From).ToList();
        zapisy.Should().HaveCount(2, "Update utworzył drugi zapis historii umowy");

        var stary = zapisy[0];
        var nowy2 = zapisy[1];
        // Stary zapis został skrócony do dnia poprzedzającego aneks.
        stary.Aktualnosc.To.Should().Be(odDnia.AddDays(-1));
        nowy2.Aktualnosc.From.Should().Be(odDnia, "nowy zapis obowiązuje od wskazanego dnia");
        // Wartość różni się między okresami.
        stary.Wartosc.Should().Be(new Currency(5000m));
        nowy2.Wartosc.Should().Be(new Currency(6000m));
        // Odczyt „na dzień": indeksator umowa[date] zwraca zapis obowiązujący na datę.
        u2[odDnia].Wartosc.Should().Be(new Currency(6000m), "od odDnia obowiązuje nowa wartość");
        u2[odDnia.AddDays(-1)].Wartosc.Should().Be(new Currency(5000m),
            "przed odDnia obowiązuje wartość początkowa");
    }
}
