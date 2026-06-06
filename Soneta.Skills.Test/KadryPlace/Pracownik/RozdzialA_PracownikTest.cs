using System;
using System.Linq;
using AwesomeAssertions;
using NUnit.Framework;
using Soneta.Business;
using Soneta.Kadry;
using Soneta.Types;
using Prac = Soneta.Kadry.Pracownik;

namespace Soneta.Skills.Test.KadryPlace.Pracownik;

/// <summary>
/// Rozdział A — „Pracownik: zatrudnienie i dane kartotekowe" (receptury A1, A2, A7, A9, A10, A14).
/// <para>
/// Testy są <b>wykonywalną dokumentacją</b> publicznego kontraktu platformy Soneta dla domeny
/// Kadry/Płace. Każda metoda mapuje się 1:1 do receptury z dokumentu skilla <c>pracownik.md</c> i
/// pokazuje realny model „root + historia": <c>Pracownik</c> (tabela <c>Pracownicy</c>) trzyma tylko
/// nieliczne pola niezmienne, a praktycznie wszystkie dane kadrowe siedzą w zapisach historycznych
/// <c>PracHistoria</c> (kolekcja <c>Pracownik.Historia</c>), w tym złożone pole <c>Etat</c>.
/// </para>
/// <para>
/// Wszystko działa na bazie Demo (GoldStandard) z automatycznym rollbackiem po teście, więc można
/// swobodnie tworzyć i modyfikować dane. Operujemy wyłącznie na <b>publicznym kontrakcie</b> — tak
/// jak dodatek programisty zewnętrznego bez dostępu do kodu źródłowego aplikacji.
/// </para>
/// </summary>
[TestFixture]
public class RozdzialA_PracownikTest : PracownikTestBase
{
    // ============================== A1 — Zatrudnienie nowego pracownika ==============================

    [Test]
    [Description("A1: dodanie nowego PracownikFirmy (AddRow) automatycznie tworzy pierwszy zapis " +
                 "historii (Last); dane osobowe ustawiamy na Last; Save() utrwala pracownika.")]
    public void A1_ZatrudnienieNowego_TworzyPierwszyZapisHistorii_IZapisuje()
    {
        Guid guid = Guid.Empty;
        var kod = "A1_" + Guid.NewGuid().ToString("N").Substring(0, 6);

        InTransaction(() =>
        {
            // Pracownik jest typem ABSTRAKCYJNYM — tworzymy konkretny PracownikFirmy.
            // AddRow zwraca typowany wiersz; w OnAdded powstaje pierwszy PracHistoria + kalendarz,
            // dlatego NIE tworzymy zapisu historii ręcznie — od razu mamy pracownik.Last.
            var pracownik = Session.AddRow(new PracownikFirmy());
            pracownik.Kod = kod;                 // pole rootu

            // Mechanizm „root + historia": dane osobowe idą na ZAPIS historii (pierwszy zapis = Last),
            // nie na root. Last istnieje już bezpośrednio po AddRow.
            pracownik.Last.Should().NotBeNull("OnAdded tworzy pierwszy zapis historii (Last)");
            pracownik.Last.Nazwisko = "Kowalska";
            pracownik.Last.Imie = "Gabriela";
            pracownik.Last.PESEL = "94010812345";

            guid = pracownik.Guid;
        });
        // Save w osobnej sesji-zamknięciu: wykrywanie konfliktów/duplikatów dzieje się w Save().
        SaveDispose();

        // Odczyt na świeżej sesji po Guid — potwierdza utrwalenie pracownika i jego pierwszego zapisu.
        var zapis = Get<Prac>(guid);
        zapis.Should().NotBeNull("pracownik został zapisany do bazy");
        zapis.Kod.Should().Be(kod);
        zapis.Last.Should().NotBeNull("nadal istnieje pierwszy zapis historii");
        zapis.Last.Nazwisko.Should().Be("Kowalska");
        zapis.Last.Imie.Should().Be("Gabriela");
        // Pierwszy zapis historii ma okres otwarty (do końca czasu).
        zapis.Last.Aktualnosc.To.Should().Be(Date.MaxValue, "pierwszy zapis ma okres otwarty");
    }

    [Test]
    [Description("A1: typ Pracownik jest abstrakcyjny — konkretnym typem zatrudnienia jest PracownikFirmy " +
                 "(dziedziczy po Pracownik); to on jest dodawany do kartoteki.")]
    public void A1_PracownikFirmy_JestKonkretnymTypemPracownika()
    {
        // Dokumentujemy regułę z receptury: new Pracownik() jest niemożliwe (typ abstrakcyjny),
        // więc używamy PracownikFirmy. Sprawdzamy relację dziedziczenia bez instancjonowania abstrakta.
        typeof(Prac).IsAbstract.Should().BeTrue("Pracownik jest klasą abstrakcyjną");
        typeof(Prac).IsAssignableFrom(typeof(PracownikFirmy))
            .Should().BeTrue("PracownikFirmy jest konkretnym typem pracownika firmy");
    }

    // ============================== A2 — Podstawowe dane kadrowe ==============================

    [Test]
    [Description("A2: dane ewidencyjno-identyfikacyjne (NazwiskoRodowe, ImieOjca, NIP, Urodzony, " +
                 "Obywatelstwo, Adres) ustawiamy na zapisie historii (Last) oraz jego subrowach.")]
    public void A2_DaneKadrowe_UstawianeNaZapisieHistorii_ISubrowach()
    {
        Guid guid = Guid.Empty;
        var kod = "A2_" + Guid.NewGuid().ToString("N").Substring(0, 6);

        InTransaction(() =>
        {
            var pracownik = Session.AddRow(new PracownikFirmy());
            pracownik.Kod = kod;

            var ph = pracownik.Last;          // bieżący (ostatni) zapis kadrowy
            ph.Nazwisko = "Nowak";
            ph.Imie = "Anna";
            ph.NazwiskoRodowe = "Wiśniewska";
            ph.ImieOjca = "Jan";
            ph.NIP = "1234563218";

            // Urodzony, Obywatelstwo, Adres to SUBROWY (pola złożone) — modyfikujemy ich pola,
            // nie przypisujemy całego obiektu.
            ph.Urodzony.Data = new Date(1994, 1, 8);
            ph.Urodzony.Miejsce = "Kraków";
            ph.Obywatelstwo.Nazwa = "polskie";

            ph.Adres.Ulica = "Wadowicka";
            ph.Adres.NrDomu = "8A";
            ph.Adres.KodPocztowyS = "30-415";   // wersja stringowa kodu (z myślnikiem)
            ph.Adres.Miejscowosc = "Kraków";

            guid = pracownik.Guid;
        });
        SaveDispose();

        var ph2 = Get<Prac>(guid).Last;
        ph2.NazwiskoRodowe.Should().Be("Wiśniewska");
        ph2.ImieOjca.Should().Be("Jan");
        ph2.NIP.Should().Be("1234563218");
        ph2.Urodzony.Data.Should().Be(new Date(1994, 1, 8));
        ph2.Urodzony.Miejsce.Should().Be("Kraków");
        ph2.Obywatelstwo.Nazwa.Should().Be("polskie");
        ph2.Adres.Ulica.Should().Be("Wadowicka");
        ph2.Adres.NrDomu.Should().Be("8A");
        ph2.Adres.Miejscowosc.Should().Be("Kraków");
        // KodPocztowyS to string z myślnikiem; KodPocztowy to int (bez myślnika).
        ph2.Adres.KodPocztowyS.Should().Be("30-415");
        ph2.Adres.KodPocztowy.Should().Be(30415);
    }

    [Test]
    [Description("A2: Plec to enum PłećOsoby; przy poprawnym numerze PESEL płeć jest wyliczana " +
                 "automatycznie przez weryfikator (parzysta cyfra przed kontrolną = kobieta).")]
    public void A2_Plec_WyliczanaZPESEL()
    {
        var p = PierwszyPracownik();
        // Pracownik etatowy z Demo ma ustawiony PESEL — płeć powinna być jedną z wartości enuma.
        p.Last.Plec.Should().BeOneOf(PłećOsoby.Kobieta, PłećOsoby.Mężczyzna);
    }

    // ============================== A7 — Ubezpieczenia społeczne i zdrowotne ==============================

    [Test]
    [Description("A7: tytuł ubezpieczenia (Tyub4) to rekord słownika TytulyUbezpiecz4 pobierany " +
                 "WgKodu[int]; daty objęcia ubezpieczeniami społecznymi ustawiamy na subrowach Spoleczne.")]
    public void A7_Ubezpieczenia_UstawiajaTytulIDatyObjecia()
    {
        // Tytuł ubezpieczenia jest rekordem słownika KONFIGURACYJNEGO o kluczu int (np. 110 = pracownik).
        // Wymaga, by w bazie Demo istniał wpis o tym kodzie — w przeciwnym razie pomijamy część tytułu.
        var tyub110 = Kadry.TytulyUbezpiecz4.WgKodu[110] as TytulUbezpieczenia4;

        Guid guid = Guid.Empty;
        var kod = "A7_" + Guid.NewGuid().ToString("N").Substring(0, 6);
        var od = new Date(2026, 1, 1);

        InTransaction(() =>
        {
            var pracownik = Session.AddRow(new PracownikFirmy());
            pracownik.Kod = kod;
            pracownik.Last.Nazwisko = "Ubezpieczony";   // Nazwisko jest wymagane przy Save
            pracownik.Last.Imie = "Tomasz";

            // Cała struktura ubezpieczeń jest HISTORYCZNA — siedzi w Etat danego zapisu (Last.Etat).
            var ub = pracownik.Last.Etat.Ubezpieczenia;

            if (tyub110 != null)
                ub.Tyub4 = tyub110;            // tytuł ubezpieczenia (słownik), klucz int

            // Data objęcia ubezpieczeniami społecznymi (publiczny setter na subrowie Ubezpieczenia).
            // UWAGA: na poszczególnych subrowach Spoleczne (Emerytalne/Rentowe) pole `Od` NIE ma
            // publicznego settera — jest wyliczane. Publicznie ustawiamy flagi Obowiazkowe/Dobrowolne
            // oraz datę ObowiazkoweOd na zbiorczym subrowie Ubezpieczenia.
            ub.ObowiazkoweOd = od;
            ub.Emerytalne.Obowiazkowe = true;
            ub.Rentowe.Obowiazkowe = true;

            // Oddział NFZ to subrow — ustawiamy jego pola (np. datę OdDnia), nie cały obiekt.
            pracownik.Last.OddzialNFZ.OdDnia = od;

            guid = pracownik.Guid;
        });
        SaveDispose();

        var ub2 = Get<Prac>(guid).Last.Etat.Ubezpieczenia;
        ub2.ObowiazkoweOd.Should().Be(od);
        ub2.Emerytalne.Obowiazkowe.Should().BeTrue();
        ub2.Rentowe.Obowiazkowe.Should().BeTrue();
        if (tyub110 != null)
            ub2.Tyub4.Should().NotBeNull("ustawiliśmy tytuł ubezpieczenia ze słownika");
    }

    [Test]
    [Description("A7 (odczyt): tytuł ubezpieczenia obowiązujący na dzień odczytujemy metodą " +
                 "Ubezpieczenia.WyliczTyubNaDzień(Date) — bez modyfikacji danych.")]
    public void A7_WyliczTyubNaDzien_ZwracaTytulNaDzien()
    {
        // Odczyt „na dzień" dla istniejącego pracownika z Demo (zatrudniony etatowo).
        var p = PierwszyPracownik();
        var data = Date.Today;

        // pracownik[data] zwraca zapis obowiązujący na datę; z jego Etat.Ubezpieczenia liczymy tytuł.
        var zapisNaDzis = p[data];
        zapisNaDzis.Should().NotBeNull("pracownik etatowy z Demo ma zapis obowiązujący na dziś");

        // Metoda zwraca rekord TytulUbezpieczenia (starszy typ tytułu); może być null, gdy
        // pracownik nie ma określonego tytułu na tę datę — istotne, że metoda działa bez wyjątku.
        System.Action odczyt = () =>
        {
            TytulUbezpieczenia _ = zapisNaDzis.Etat.Ubezpieczenia.WyliczTyubNaDzień(data);
        };
        odczyt.Should().NotThrow("WyliczTyubNaDzień(Date) to publiczny odczyt tytułu na dzień");
    }

    // ============================== A9 — Rodzina pracownika (ZCNA) ==============================

    [Test]
    [Description("A9: członka rodziny tworzymy konstruktorem CzlonekRodziny(pracownik); zgłoszenie do " +
                 "ubezpieczenia zdrowotnego (ZCNA) to Ubezpieczony=true + UbezpieczenieOkres + StPokrewienstwa.")]
    public void A9_CzlonekRodziny_ZglaszanyDoUbezpieczeniaZdrowotnego()
    {
        Guid guidPrac = Guid.Empty;
        var kod = "A9_" + Guid.NewGuid().ToString("N").Substring(0, 6);
        var od = new Date(2026, 1, 1);

        InTransaction(() =>
        {
            var pracownik = Session.AddRow(new PracownikFirmy());
            pracownik.Kod = kod;
            pracownik.Last.Nazwisko = "Kowalski";
            pracownik.Last.Imie = "Adam";

            // Konstruktor CzlonekRodziny(pracownik) wiąże rekord z pracownikiem; AddRow włącza go do sesji.
            var dziecko = Session.AddRow(new CzlonekRodziny(pracownik));
            dziecko.Nazwisko = "Kowalska";
            dziecko.Imie = "Zofia";
            dziecko.PESEL = "20290512345";
            dziecko.Urodzony.Data = new Date(2020, 9, 5);   // Urodzony to subrow
            dziecko.StPokrewienstwa = KodStPokrewienstwa.Dziecko;   // enum stopnia pokrewieństwa

            // Zgłoszenie do ubezpieczenia zdrowotnego (ZCNA):
            dziecko.Ubezpieczony = true;
            dziecko.UbezpieczenieOkres = new FromTo(od, Date.MaxValue);
            dziecko.NaUtrzymaniu = true;

            guidPrac = pracownik.Guid;
        });
        SaveDispose();

        var pracownik2 = Get<Prac>(guidPrac);
        // CzlonekRodziny pojawia się w kolekcji Rodzina pracownika (płaski child, nie historyczny).
        var rodzina = pracownik2.Rodzina.Cast<CzlonekRodziny>().ToList();
        rodzina.Should().ContainSingle("dodaliśmy jednego członka rodziny");

        var cr = rodzina[0];
        cr.Imie.Should().Be("Zofia");
        cr.StPokrewienstwa.Should().Be(KodStPokrewienstwa.Dziecko);
        cr.Ubezpieczony.Should().BeTrue();
        cr.NaUtrzymaniu.Should().BeTrue();
        cr.UbezpieczenieOkres.From.Should().Be(od);

        // Odczyt aktualnie ubezpieczonych członków rodziny — filtr serwerowy po kolekcji (lambda).
        var ubezpieczeni = pracownik2.Rodzina[(CzlonekRodziny c) => c.Ubezpieczony].Cast<CzlonekRodziny>().ToList();
        ubezpieczeni.Should().ContainSingle("jedyny członek rodziny jest zgłoszony do ubezpieczenia");
    }

    // ============================== A10 — Poprzednie miejsca pracy ==============================

    [Test]
    [Description("A10: poprzedniego pracodawcę dodajemy konkretnym typem HistoriaZatrudnienia(pracownik) " +
                 "do kolekcji HistoriaZatrudnienia (inna niż Historia bieżącego zatrudnienia).")]
    public void A10_PoprzedniPracodawca_DodawanyDoHistoriiZatrudnienia()
    {
        Guid guidPrac = Guid.Empty;
        var kod = "A10_" + Guid.NewGuid().ToString("N").Substring(0, 6);
        var okres = new FromTo(new Date(2018, 3, 1), new Date(2025, 12, 31));

        InTransaction(() =>
        {
            var pracownik = Session.AddRow(new PracownikFirmy());
            pracownik.Kod = kod;
            pracownik.Last.Nazwisko = "Zieliński";
            pracownik.Last.Imie = "Piotr";

            // HistoriaZatrudnieniaBase ma ctor protected — tworzymy konkretny typ:
            // HistoriaZatrudnienia (poprzedni pracodawca; ctor ustawia Typ = Zatrudnienie).
            var hz = Session.AddRow(new HistoriaZatrudnienia(pracownik));
            hz.Nazwa = "Poprzednia Firma Sp. z o.o.";
            hz.Okres = okres;
            hz.EfektywnyOkres = okres;     // to EfektywnyOkres decyduje o wliczeniu do stażu
            hz.Adres1 = "ul. Główna 1, Kraków";

            // Drugi typ wpisu: okres nauki (UkonczonaSzkola) — także child pracownika.
            var szkola = Session.AddRow(new UkonczonaSzkola(pracownik));
            szkola.Nazwa = "Technikum nr 1";
            szkola.Okres = new FromTo(new Date(2014, 9, 1), new Date(2018, 6, 30));

            guidPrac = pracownik.Guid;
        });
        SaveDispose();

        var pracownik2 = Get<Prac>(guidPrac);
        // HistoriaZatrudnienia to kolekcja stażu u POPRZEDNICH pracodawców (typ bazowy w kolekcji).
        var wpisy = pracownik2.HistoriaZatrudnienia.Cast<HistoriaZatrudnieniaBase>().ToList();
        wpisy.Should().HaveCount(2, "dodaliśmy wpis pracy i wpis nauki");

        var praca = wpisy.OfType<HistoriaZatrudnienia>().Single();
        praca.Nazwa.Should().Be("Poprzednia Firma Sp. z o.o.");
        // FromTo implementuje IEnumerable<Date>, więc porównujemy granice okresu, nie cały obiekt.
        praca.Okres.From.Should().Be(okres.From);
        praca.Okres.To.Should().Be(okres.To);
        praca.EfektywnyOkres.From.Should().Be(okres.From);
        praca.EfektywnyOkres.To.Should().Be(okres.To);
        // Typ jest ustawiany przez ctor konkretnej klasy (praca vs nauka) — dwa różne wpisy.
        wpisy.OfType<UkonczonaSzkola>().Should().ContainSingle("jeden wpis nauki");
    }

    // ============================== A14 — Aktualizacja historyczna „od daty" vs korekta ==============================

    [Test]
    [Description("A14: zmiana warunkow 'od daty' - Historia.Update(date) klonuje zapis i skraca stary; " +
                 "nowy klon dodajemy do tabeli PracHistorie i ustawiamy na nim zmienione warunki.")]
    public void A14_AktualizacjaOdDaty_TworzyNowyZapisOdDnia_ISkracaStary()
    {
        Guid guidPrac = Guid.Empty;
        var odDnia = new Date(2026, 7, 1);

        InTransaction(() =>
        {
            var pracownik = Session.AddRow(new PracownikFirmy());
            pracownik.Kod = "A14_" + Guid.NewGuid().ToString("N").Substring(0, 6);
            pracownik.Last.Nazwisko = "Aktualizowany";
            pracownik.Last.Imie = "Marek";
            // Stan „przed zmianą" na pierwszym zapisie (pola pewnie zapisywalne na świeżym zapisie).
            pracownik.Last.Etat.MiejscePracy = "Kraków";
            pracownik.Last.Podatki.UlgaMnoznik = 0m;

            // 1) Update klonuje zapis aktualny na odDnia, skraca stary do dnia poprzedniego
            //    i zwraca NOWY klon z okresem od odDnia.
            var nowy = pracownik.Historia.Update(odDnia);
            // 2) Update + AddRow to nierozłączna para — bez AddRow klon zostaje „odpięty".
            pracownik.Module.PracHistorie.AddRow(nowy);
            // 3) Na nowym zapisie wprowadzamy zmienione warunki (obowiązujące od odDnia).
            nowy.Etat.MiejscePracy = "Warszawa";   // zmiana miejsca pracy od odDnia
            nowy.Podatki.UlgaMnoznik = 1m;          // zmiana danych podatkowych od odDnia

            guidPrac = pracownik.Guid;
        });
        SaveDispose();

        var pracownik2 = Get<Prac>(guidPrac);
        // Mamy teraz dwa zapisy: stary (do odDnia-1) i nowy (od odDnia).
        var zapisy = pracownik2.Historia.Cast<PracHistoria>().OrderBy(h => h.Aktualnosc.From).ToList();
        zapisy.Should().HaveCount(2, "Update utworzył drugi zapis historii");

        var stary = zapisy[0];
        var nowy2 = zapisy[1];
        // Stary zapis został skrócony do dnia poprzedzającego zmianę.
        stary.Aktualnosc.To.Should().Be(odDnia.AddDays(-1));
        nowy2.Aktualnosc.From.Should().Be(odDnia, "nowy zapis obowiązuje od wskazanego dnia");
        // Warunki różnią się między okresami: inne miejsce pracy i ulga przed/od zmiany.
        stary.Etat.MiejscePracy.Should().Be("Kraków");
        nowy2.Etat.MiejscePracy.Should().Be("Warszawa");
        stary.Podatki.UlgaMnoznik.Should().Be(0m);
        nowy2.Podatki.UlgaMnoznik.Should().Be(1m);
    }

    [Test]
    [Description("A14 (odczyt na dzień): indeksator pracownik[date] zwraca zapis obowiązujący na datę " +
                 "(Aktualnosc zawiera date), a dla daty sprzed zatrudnienia zwraca null.")]
    public void A14_OdczytNaDzien_ZwracaWlasciwyZapis_INullPrzedZatrudnieniem()
    {
        Guid guidPrac = Guid.Empty;
        var odDnia = new Date(2026, 7, 1);

        InTransaction(() =>
        {
            var pracownik = Session.AddRow(new PracownikFirmy());
            pracownik.Kod = "A14r_" + Guid.NewGuid().ToString("N").Substring(0, 6);
            pracownik.Last.Nazwisko = "Czytany";
            pracownik.Last.Imie = "Ewa";

            var nowy = pracownik.Historia.Update(odDnia);
            pracownik.Module.PracHistorie.AddRow(nowy);
            nowy.NazwiskoRodowe = "PoZmianie";

            guidPrac = pracownik.Guid;
        });
        SaveDispose();

        var pracownik2 = Get<Prac>(guidPrac);
        var pierwszy = pracownik2.Historia.GetFirst();   // najstarszy zapis (okres do odDnia-1)

        // Odczyt „na dzień": data wewnątrz okresu pierwszego zapisu → zwraca pierwszy zapis.
        var dzienWStarymOkresie = pierwszy.Aktualnosc.From;
        pracownik2[dzienWStarymOkresie].Should().BeSameAs(pierwszy,
            "pracownik[date] zwraca zapis, którego Aktualnosc zawiera date");

        // Data w okresie nowego zapisu → zwraca nowy (najświeższy) zapis = Last.
        pracownik2[odDnia].Should().BeSameAs(pracownik2.Last,
            "od odDnia obowiązuje nowy zapis (Last)");

        // Data sprzed zatrudnienia (przed początkiem pierwszego zapisu) → brak zapisu (null).
        if (pierwszy.Aktualnosc.From > Date.MinValue)
        {
            var przedZatrudnieniem = pierwszy.Aktualnosc.From.AddDays(-1);
            pracownik2[przedZatrudnieniem].Should().BeNull(
                "dla daty sprzed zatrudnienia nie ma zapisu historii");
        }
    }

    [Test]
    [Description("A14 (korekta): modyfikacja zapisu pracownik[date] BEZ Update/AddRow zmienia dane " +
                 "w CAŁYM okresie tego zapisu — nie tworzy nowego okresu.")]
    public void A14_Korekta_ZmieniaIstniejacyZapis_BezNowegoOkresu()
    {
        Guid guidPrac = Guid.Empty;

        InTransaction(() =>
        {
            var pracownik = Session.AddRow(new PracownikFirmy());
            pracownik.Kod = "A14k_" + Guid.NewGuid().ToString("N").Substring(0, 6);
            pracownik.Last.Nazwisko = "Korygowany";
            pracownik.Last.Imie = "Jan";
            guidPrac = pracownik.Guid;
        });
        SaveDispose();

        // Korekta: modyfikujemy zapis obowiązujący na wskazaną datę — bez Update, bez AddRow.
        InTransaction(() =>
        {
            var ph = Get<Prac>(guidPrac)[Date.Today];
            ph.Should().NotBeNull();
            ph.NazwiskoRodowe = "PoprawioneNazwisko";   // korekta w istniejącym okresie
        });
        SaveDispose();

        var pracownik2 = Get<Prac>(guidPrac);
        // Liczba zapisów się nie zmieniła — korekta nie tworzy nowego okresu.
        pracownik2.Historia.Cast<PracHistoria>().Should().ContainSingle("korekta nie dzieli okresu");
        pracownik2.Last.NazwiskoRodowe.Should().Be("PoprawioneNazwisko");
    }
}
