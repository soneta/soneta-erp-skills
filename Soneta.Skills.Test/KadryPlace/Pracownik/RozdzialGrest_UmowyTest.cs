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
/// Rozdział G (reszta) — „Umowy cywilnoprawne" (receptury KADRY-G3, KADRY-G4, KADRY-G5).
/// <para>
/// Testy są <b>wykonywalną dokumentacją</b> publicznego kontraktu platformy Soneta dla operacji na
/// umowach cywilnoprawnych: operacja seryjna „Dodaj umowy" dla grupy osób (KADRY-G3), rachunek/rozliczenie
/// umowy = wypłata <c>WyplataUmowa</c> naliczana mechanizmem płac (KADRY-G4), oraz zgłoszenia ZUS
/// zleceniobiorców na podstawie schematu ubezpieczeń umowy (KADRY-G5).
/// </para>
/// <para>
/// Wszystko działa na bazie Demo (GoldStandard) z automatycznym rollbackiem po teście. Pracownicy
/// etatowi z Demo (kody "006".."039") nie mają jeszcze umów cywilnoprawnych — czysty punkt wejścia.
/// Operujemy wyłącznie na <b>publicznym kontrakcie</b> — tak jak dodatek programisty zewnętrznego bez
/// dostępu do kodu źródłowego aplikacji.
/// </para>
/// </summary>
[TestFixture]
public class RozdzialGrest_UmowyTest : PracownikTestBase
{
    // Pobranie definicji elementu = rodzaju umowy ze słownika konfiguracyjnego po stałej Guid.
    private DefinicjaElementu DefUmowy(Guid rodzaj) =>
        Place.DefElementow[rodzaj] as DefinicjaElementu;

    // Dobiera datę mieszczącą się w okresie aktywnego etatu pracownika (jak w H): koniec miesiąca
    // rozpoczęcia etatu, ograniczony do [From, To]. Etaty Demo są zwykle otwarte (To = MaxValue).
    private static Date DataWEtacie(Prac pracownik)
    {
        var okres = pracownik.Last.Etat.Okres;
        var from = okres.From;
        var koniecMiesiaca = new Date(from.Year, from.Month, 1).AddMonths(1).AddDays(-1);
        if (koniecMiesiaca < from) koniecMiesiaca = from;
        if (okres.To != Date.MaxValue && koniecMiesiaca > okres.To) koniecMiesiaca = okres.To;
        return koniecMiesiaca;
    }

    // ====================== KADRY-G3 — Operacja seryjna „Dodaj umowy" dla grupy osób ======================

    [Test]
    [Description("KADRY-G3 (wariant B - petla, jak KADRY-G1): operacja seryjna 'Dodaj umowy' = KADRY-G1 powtorzone dla " +
                 "każdej osoby z grupy. Dla każdego pracownika tworzymy Session.AddRow(new Umowa(p)) " +
                 "z tymi samymi danymi nagłówkowymi (Element, Okres, RodzajRozliczenia, TypWartosci, " +
                 "Wydzial) i kwotą na umowa.Last.Wartosc. Każda osoba dostaje osobny rekord Umowa.")]
    public void KADRY_G3_DodajUmowySeryjnie_PetlaPoGrupie_TworzyUmoweKazdejOsobie()
    {
        var defZlecenie = DefUmowy(DefinicjaElementu.UmowaZlecenie);
        defZlecenie.Should().NotBeNull("baza Demo zawiera definicję umowy zlecenie");

        var okres = new FromTo(new Date(2026, 1, 1), new Date(2026, 12, 31));
        var kody = new[] { Pracownik_.Andrzejewski, Pracownik_.Bednarek, Pracownik_.Bujak };
        var guidy = new Guid[kody.Length];

        InTransaction(() =>
        {
            for (int i = 0; i < kody.Length; i++)
            {
                var p = Pracownik(kody[i]);
                p.Should().NotBeNull();
                p.Umowy.Cast<Umowa>().Should().BeEmpty("pracownik Demo nie ma jeszcze umów");

                // Jawne tworzenie jak w KADRY-G1 — operacja seryjna to to samo powtórzone w pętli.
                var umowa = Session.AddRow(new Umowa(p));
                umowa.Element = defZlecenie;
                umowa.Data = okres.From;
                umowa.Okres = okres;
                umowa.Tytul = "Umowa zlecenie - projekt grupowy";
                umowa.RodzajRozliczenia = RodzajeRozliczeniaUmowy.KwotaDoWypłaty;
                umowa.TypWartosci = TypWartosciUmowy.Brutto;
                umowa.Wydzial = Kadry.Wydzialy.Firma;     // jednostka organizacyjna wymagana przy Save
                umowa.Last.Wartosc = new Currency(4000m); // kwota na zapisie historycznym
                guidy[i] = p.Guid;
            }
        });
        SaveDispose();

        // Każda osoba z grupy ma teraz jedną umowę o tych samych danych nagłówkowych.
        foreach (var g in guidy)
        {
            var u = Get<Prac>(g).Umowy.Cast<Umowa>().Single();
            // Element to definicja konfiguracyjna — po SaveDispose porównujemy po Guid (inna instancja).
            u.Element.Guid.Should().Be(defZlecenie.Guid);
            u.Element.RodzajZrodla.Should().Be(RodzajŹródłaWypłaty.Umowa);
            u.Tytul.Should().Be("Umowa zlecenie - projekt grupowy");
            u.RodzajRozliczenia.Should().Be(RodzajeRozliczeniaUmowy.KwotaDoWypłaty);
            u.Okres.From.Should().Be(okres.From);
            u.Last.Wartosc.Should().Be(new Currency(4000m));
        }
    }

    [Test]
    [Description("KADRY-G3 (wariant A — worker platformy): Pracownik.DodajUmowęWorker (DataType Pracownik, " +
                 "ctor przyjmuje Session) z ustawionymi Pracownicy (grupa) i Pars " +
                 "(DodajUmowęWorker.Params(Context): Element, Okres, Data, Tytuł, RodzajRozliczenia, " +
                 "TypWartości, Wartość, Wydział). Akcja DodajUmowę() (void) tworzy umowę każdej osobie.")]
    public void KADRY_G3_DodajUmowyWorker_TworzyUmoweKazdejZaznaczonejOsobie()
    {
        var defZlecenie = DefUmowy(DefinicjaElementu.UmowaZlecenie);
        var okres = new FromTo(new Date(2026, 1, 1), new Date(2026, 12, 31));

        var osoby = new[]
        {
            Pracownik(Pracownik_.Andrzejewski),
            Pracownik(Pracownik_.Bednarek),
            Pracownik(Pracownik_.Bujak),
        };
        var guidy = osoby.Select(p => p.Guid).ToArray();
        foreach (var p in osoby)
            p.Umowy.Cast<Umowa>().Should().BeEmpty("pracownik Demo nie ma jeszcze umów");

        // Parametry operacji seryjnej — Params(Context) (ContextBase), pola z diakrytykami.
        var pars = new Prac.DodajUmowęWorker.Params(Context);
        pars.Element = defZlecenie;
        pars.Okres = okres;
        pars.Data = okres.From;
        pars.Tytuł = "Umowa zlecenie - operacja seryjna";
        pars.RodzajRozliczenia = RodzajeRozliczeniaUmowy.KwotaDoWypłaty;
        pars.TypWartości = TypWartosciUmowy.Brutto;
        pars.Wartość = new Currency(3500m);
        pars.Wydział = Kadry.Wydzialy.Firma;   // wymagany

        // Worker przyjmuje Session w konstruktorze; Pracownicy = grupa z zaznaczenia.
        var worker = new Prac.DodajUmowęWorker(Session) { Pracownicy = osoby, Pars = pars };
        worker.DodajUmowę();   // void — tworzy umowy wszystkim Pracownicy
        SaveDispose();

        // Każda osoba dostała umowę o danych z Pars.
        foreach (var g in guidy)
        {
            var u = Get<Prac>(g).Umowy.Cast<Umowa>().Single();
            u.Element.Guid.Should().Be(defZlecenie.Guid);   // porównanie po Guid (inna instancja)
            u.Tytul.Should().Be("Umowa zlecenie - operacja seryjna");
            u.Okres.From.Should().Be(okres.From);
            u.Last.Wartosc.Should().Be(new Currency(3500m));
        }
    }

    // ====================== KADRY-G4 — Rachunek do umowy (rozliczenie = WyplataUmowa) ======================

    [Test]
    [Description("KADRY-G4: 'rachunek do umowy zlecenia' = wyplata WyplataUmowa naliczana mechanizmem plac " +
                 "(jak KADRY-H2), NIE rekord w pracownik.Rachunki (to rachunki bankowe). Tworzymy umowę " +
                 "(KADRY-G1), potem new NaliczanieSeryjne.Umowy(new UmowaParams(Context)) { Umowa = u }." +
                 "Nalicz(); wynik to WyplataUmowa (Typ == Umowa). Stan rozliczenia: Umowa.Stan, " +
                 "Umowa.Splacono, Umowa.Pozostało.")]
    public void KADRY_G4_RachunekDoUmowy_NaliczanieTworzyWyplateUmowa_IZmieniaStan()
    {
        var pracownik = Pracownik(Pracownik_.Strzelecki);
        pracownik.Should().NotBeNull();

        var data = DataWEtacie(pracownik);
        var okresUmowy = new FromTo(new Date(data.Year, data.Month, 1), data);

        // 1) Umowa zlecenie (jak KADRY-G1) — dane operacyjne tworzymy w trybie edycji.
        Guid guidUmowy = Guid.Empty;
        InTransaction(() =>
        {
            var u = Session.AddRow(new Umowa(pracownik));
            u.Element = DefUmowy(DefinicjaElementu.UmowaZlecenie);
            u.Data = okresUmowy.From;
            u.Okres = okresUmowy;
            u.Tytul = "Umowa zlecenie - rachunek KADRY-G4";
            u.RodzajRozliczenia = RodzajeRozliczeniaUmowy.KwotaDoWypłaty;
            u.TypWartosci = TypWartosciUmowy.Brutto;
            u.Wydzial = Kadry.Wydzialy.Firma;
            u.Last.Wartosc = new Currency(3000m);
            guidUmowy = u.Guid;
        });
        SaveDispose();

        var umowa = Get<Umowa>(guidUmowy);
        // Przed rozliczeniem umowa jest niewypłacona.
        umowa.Stan.Should().Be(StanUmowy.Niewypłacona, "świeżo dodana umowa nie ma rachunku");

        // 2) Rachunek = naliczenie wypłaty z umowy (jak KADRY-H2). UmowaParams NIE ustawia Naliczanie.
        var pars = new NaliczanieSeryjne.UmowaParams(Context);
        pars.DataWypłaty = data;
        pars.DataListy = pars.DataWypłaty;

        // Ustawienie Umowa nadpisuje Pracownik właścicielem umowy. Nalicz() commituje sam.
        var naliczanie = new NaliczanieSeryjne.Umowy(pars) { Umowa = umowa };
        NaliczanieWypłat wynik = naliczanie.Nalicz();

        var wyplaty = wynik.WszystkieWypłaty.Cast<Wyplata>().ToList();
        wyplaty.Should().NotBeEmpty("naliczenie umowy tworzy rachunek (WyplataUmowa)");

        var w = wyplaty[0];
        w.Typ.Should().Be(TypWyplaty.Umowa, "rachunek do umowy to wypłata typu Umowa");
        w.Should().BeAssignableTo<WyplataUmowa>("rachunek to konkretny typ WyplataUmowa");
        ((WyplataUmowa)w).Umowa.Guid.Should().Be(umowa.Guid, "WyplataUmowa wskazuje swoją umowę");
        SaveDispose();

        // 3) Stan rozliczenia umowy po wystawieniu rachunku.
        var umowa2 = Get<Umowa>(guidUmowy);
        umowa2.Stan.Should().NotBe(StanUmowy.Niewypłacona,
            "po naliczeniu rachunku umowa nie jest już całkowicie niewypłacona");
        umowa2.Splacono.Value.Should().BeGreaterThan(0m, "część/całość kwoty została rozliczona");
        // Splacono + Pozostało odpowiada modelowi rozliczenia (kwoty Currency).
        (umowa2.Splacono.Value + umowa2.Pozostało.Value).Should().BeGreaterThanOrEqualTo(0m);
    }

    [Test]
    [Description("KADRY-G4 (odczyt): rachunki (wypłaty) wystawione do umowy odczytujemy przez " +
                 "pracownik.Wyplaty.OfType<WyplataUmowa>().Where(x => x.Umowa == umowa); składniki " +
                 "rachunku to WypElement (Wartosc). pracownik.Rachunki to rachunki BANKOWE — nie umowy.")]
    public void KADRY_G4_OdczytRachunkowUmowy_PrzezWyplatyUmowa()
    {
        var pracownik = Pracownik(Pracownik_.Andrzejewski);
        var data = DataWEtacie(pracownik);
        var okresUmowy = new FromTo(new Date(data.Year, data.Month, 1), data);

        Guid guidUmowy = Guid.Empty;
        InTransaction(() =>
        {
            var u = Session.AddRow(new Umowa(pracownik));
            u.Element = DefUmowy(DefinicjaElementu.UmowaZlecenie);
            u.Data = okresUmowy.From;
            u.Okres = okresUmowy;
            u.Tytul = "Umowa zlecenie - odczyt rachunków";
            u.RodzajRozliczenia = RodzajeRozliczeniaUmowy.KwotaDoWypłaty;
            u.TypWartosci = TypWartosciUmowy.Brutto;
            u.Wydzial = Kadry.Wydzialy.Firma;
            u.Last.Wartosc = new Currency(2500m);
            guidUmowy = u.Guid;
        });
        SaveDispose();

        var umowa = Get<Umowa>(guidUmowy);
        var pars = new NaliczanieSeryjne.UmowaParams(Context);
        pars.DataWypłaty = data;
        pars.DataListy = pars.DataWypłaty;
        new NaliczanieSeryjne.Umowy(pars) { Umowa = umowa }.Nalicz();
        SaveDispose();

        var pracownik2 = Pracownik(Pracownik_.Andrzejewski);
        var umowa2 = Get<Umowa>(guidUmowy);

        // Rachunki = wypłaty z umowy filtrowane po umowie (po Guid, bo różne instancje Row).
        var rachunki = pracownik2.Wyplaty.OfType<WyplataUmowa>()
            .Where(x => x.Umowa != null && x.Umowa.Guid == umowa2.Guid)
            .ToList();
        rachunki.Should().NotBeEmpty("wystawiliśmy rachunek do umowy");

        foreach (var r in rachunki)
            foreach (WypElement e in r.Elementy)
                e.Definicja.Should().NotBeNull("każdy składnik rachunku ma definicję elementu");

        // Składniki naliczone bezpośrednio z umowy (Umowa.Elementy).
        umowa2.Elementy.Cast<WypElement>().Should().NotBeEmpty(
            "naliczony rachunek wiąże składniki z umową (Umowa.Elementy)");
    }

    // ====================== KADRY-G5 — Zgłoszenia ZUS zleceniobiorców (ZUA / ZZA / ZWUA) ======================

    [Test]
    [Description("KADRY-G5 (schemat ubezpieczeń): typ zgłoszenia (ZUA vs ZZA) wynika ze schematu " +
                 "UmowaHistoria.Ubezpieczenia (umowa.Last.Ubezpieczenia), nie z parametru workera. " +
                 "ZUA = społeczne obowiązkowe (Emerytalne/Rentowe) + zdrowotne; Tyub4 pobierany ze " +
                 "słownika konfiguracyjnego Kadry.TytulyUbezpiecz4. Spoleczne.Od jest read-only — " +
                 "datę objęcia ustawiamy zbiorczo przez Ubezpieczenia.ObowiazkoweOd.")]
    public void KADRY_G5_SchematUbezpieczenUmowy_ZUA_SpoleczneObowiazkoweIZdrowotne()
    {
        var pracownik = Pracownik(Pracownik_.Bednarek);
        var okres = new FromTo(new Date(2026, 1, 1), new Date(2026, 12, 31));

        // Tytuł ubezpieczenia zleceniobiorcy pobieramy DYNAMICZNIE ze słownika (nie tworzymy w locie).
        var tyub4 = Kadry.TytulyUbezpiecz4.Cast<TytulUbezpieczenia4>().FirstOrDefault();
        tyub4.Should().NotBeNull("baza Demo zawiera słownik tytułów ubezpieczenia (TytulyUbezpiecz4)");

        Guid guidUmowy = Guid.Empty;
        InTransaction(() =>
        {
            var u = Session.AddRow(new Umowa(pracownik));
            u.Element = DefUmowy(DefinicjaElementu.UmowaZlecenie);
            u.Data = okres.From;
            u.Okres = okres;
            u.Tytul = "Umowa zlecenie - ZUA";
            u.RodzajRozliczenia = RodzajeRozliczeniaUmowy.KwotaDoWypłaty;
            u.TypWartosci = TypWartosciUmowy.Brutto;
            u.Wydzial = Kadry.Wydzialy.Firma;
            u.Last.Wartosc = new Currency(4000m);

            // Schemat ubezpieczeń umowy (historyczny) — ZUA: społeczne obowiązkowe + zdrowotne.
            var ub = u.Last.Ubezpieczenia;
            ub.Tyub4 = tyub4;
            ub.ObowiazkoweOd = okres.From;          // data objęcia społecznymi obowiązkowymi
            ub.Emerytalne.Obowiazkowe = true;
            ub.Rentowe.Obowiazkowe = true;
            ub.Zdrowotne.ObowiazkoweOd = okres.From; // na Zdrowotne ObowiazkoweOd jest zapisywalne
            guidUmowy = u.Guid;
        });
        SaveDispose();

        var umowa = Get<Umowa>(guidUmowy);
        var ub2 = umowa.Last.Ubezpieczenia;
        ub2.Tyub4.Should().NotBeNull("tytuł ubezpieczenia zapisany na schemacie umowy");
        ub2.Emerytalne.Obowiazkowe.Should().BeTrue("ZUA: społeczne obowiązkowe (emerytalne)");
        ub2.Rentowe.Obowiazkowe.Should().BeTrue("ZUA: społeczne obowiązkowe (rentowe)");
        ub2.Zdrowotne.ObowiazkoweOd.Should().Be(okres.From, "ZUA obejmuje też zdrowotne");
        // Schemat ubezpieczeń umowy leży na zapisie historycznym (delegat umowa.Ubezpieczenia).
        umowa.Ubezpieczenia.Should().NotBeNull("Umowa.Ubezpieczenia to delegat do Last.Ubezpieczenia");
    }

    [Test]
    [Description("KADRY-G5 (ZZA): zleceniobiorca podlegający TYLKO zdrowotnemu (np. uczeń/student/zbieg " +
                 "tytułów) → ZZA. Na schemacie UmowaHistoria.Ubezpieczenia zostawiamy Emerytalne/" +
                 "Rentowe.Obowiazkowe = false, ustawiamy tylko Zdrowotne.ObowiazkoweOd.")]
    public void KADRY_G5_SchematUbezpieczenUmowy_ZZA_TylkoZdrowotne()
    {
        var pracownik = Pracownik(Pracownik_.Bujak);
        var okres = new FromTo(new Date(2026, 1, 1), new Date(2026, 6, 30));
        var tyub4 = Kadry.TytulyUbezpiecz4.Cast<TytulUbezpieczenia4>().FirstOrDefault();

        Guid guidUmowy = Guid.Empty;
        InTransaction(() =>
        {
            var u = Session.AddRow(new Umowa(pracownik));
            u.Element = DefUmowy(DefinicjaElementu.UmowaZlecenie);
            u.Data = okres.From;
            u.Okres = okres;
            u.Tytul = "Umowa zlecenie - ZZA";
            u.RodzajRozliczenia = RodzajeRozliczeniaUmowy.KwotaDoWypłaty;
            u.TypWartosci = TypWartosciUmowy.Brutto;
            u.Wydzial = Kadry.Wydzialy.Firma;
            u.Last.Wartosc = new Currency(2000m);

            var ub = u.Last.Ubezpieczenia;
            ub.Tyub4 = tyub4;
            // ZZA: brak społecznych obowiązkowych, tylko zdrowotne.
            // UWAGA: domyślnie umowa zlecenie ma Emerytalne/Rentowe.Obowiazkowe = true (schemat ZUA);
            // dla ZZA trzeba je JAWNIE wyłączyć — samo ustawienie zdrowotnego nie wystarcza.
            ub.Emerytalne.Obowiazkowe = false;
            ub.Rentowe.Obowiazkowe = false;
            ub.Zdrowotne.ObowiazkoweOd = okres.From;
            guidUmowy = u.Guid;
        });
        SaveDispose();

        var ub2 = Get<Umowa>(guidUmowy).Last.Ubezpieczenia;
        ub2.Emerytalne.Obowiazkowe.Should().BeFalse("ZZA: brak społecznych obowiązkowych (emerytalne)");
        ub2.Rentowe.Obowiazkowe.Should().BeFalse("ZZA: brak społecznych obowiązkowych (rentowe)");
        ub2.Zdrowotne.ObowiazkoweOd.Should().Be(okres.From, "ZZA: tylko zdrowotne");
    }

    [Test]
    [Ignore("Generowanie zgłoszenia ZUA/ZZA workerem ZarejestrujUmowyWorker.Rejestracja wymaga " +
            "kompletnej konfiguracji płatnika/KEDU i kontekstu deklaracji ZUS, niedostępnego w " +
            "izolowanym środowisku testów Demo (bez sieci). Dokumentujemy kontrakt workera bez " +
            "uruchamiania generowania (ZarejestrujUmowy() / WyrejestrujUmowy()).")]
    [Description("KADRY-G5 (worker — kontrakt): Soneta.Deklaracje.ZUS.ZarejestrujUmowyWorker (DataType " +
                 "Umowa, ctor bezparametrowy, Umowy: Umowa[]). Zgłoszenie: zagnieżdżona Rejestracja " +
                 "(Pars: ParamsZ — Okres, DataDokumentu, DataWypełnienia, ZarejestrujRodzinę) i akcja " +
                 "ZarejestrujUmowy(): object generująca ZUA/ZZA wg schematu ubezpieczeń umowy. " +
                 "Wyrejestrowanie analogicznie WyrejestrujUmowy() → ZWUA. KEDU/wysyłka → sieć.")]
    public void KADRY_G5_ZgloszenieZUS_Worker_KontraktBezGenerowania()
    {
        var pracownik = Pracownik(Pracownik_.Bednarek);
        var okres = new FromTo(new Date(2026, 1, 1), new Date(2026, 12, 31));

        Guid guidUmowy = Guid.Empty;
        InTransaction(() =>
        {
            var u = Session.AddRow(new Umowa(pracownik));
            u.Element = DefUmowy(DefinicjaElementu.UmowaZlecenie);
            u.Data = okres.From;
            u.Okres = okres;
            u.Tytul = "Umowa zlecenie - zgłoszenie ZUS";
            u.RodzajRozliczenia = RodzajeRozliczeniaUmowy.KwotaDoWypłaty;
            u.TypWartosci = TypWartosciUmowy.Brutto;
            u.Wydzial = Kadry.Wydzialy.Firma;
            u.Last.Wartosc = new Currency(4000m);
            guidUmowy = u.Guid;
        });
        SaveDispose();

        var umowa = Get<Umowa>(guidUmowy);

        // Worker zgłoszeniowy na typie Umowa — operuje na zaznaczonych umowach.
        // Uwaga: Umowy oraz Pars są write-only (set-only) — przekazujemy je przez inicjalizator/setter.
        var worker = new Soneta.Deklaracje.ZUS.ZarejestrujUmowyWorker { Umowy = new[] { umowa } };

        // Parametry zgłoszenia: ParamsZ(Context) — bazowe Okres/DataDokumentu/DataWypełnienia ustawiane
        // na wspólnym kontrakcie ZarejestrujBaseWorker (ParamsZ przekazujemy jako Pars do Rejestracji).
        var pars = new Soneta.Deklaracje.ZUS.ZarejestrujBaseWorker.ParamsZ(Context);
        pars.ZarejestrujRodzinę = false;

        var rejestracja = new Soneta.Deklaracje.ZUS.ZarejestrujUmowyWorker.Rejestracja { Pars = pars };

        // Generowanie (ZUA/ZZA wg schematu ubezpieczeń) — wymaga kontekstu deklaracji/KEDU:
        rejestracja.ZarejestrujUmowy();
        SaveDispose();
    }
}
