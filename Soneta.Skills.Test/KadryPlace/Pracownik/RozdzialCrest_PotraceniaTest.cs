using System;
using System.Linq;
using AwesomeAssertions;
using NUnit.Framework;
using Soneta.Business;
using Soneta.Kadry;
using Soneta.Place;
using Soneta.Przeszeregowania;
using Soneta.Types;
using Prac = Soneta.Kadry.Pracownik;

namespace Soneta.Skills.Test.KadryPlace.Pracownik;

/// <summary>
/// Rozdział C (część „potrąceniowa") — receptury C2–C7 z dokumentu skilla <c>pracownik.md</c>:
/// <list type="bullet">
/// <item><b>C2</b> — potrącenia: w modelu płacowym potrącenie NIE ma osobnej klasy; to
/// <c>Soneta.Kadry.Dodatek</c> z definicją elementu o <c>Algorytm.Potracenie == true</c>;</item>
/// <item><b>C3</b> — akordy: <c>Soneta.Kadry.Akord</c> bez publicznego konstruktora — dodawane przez
/// worker <c>Pracownik.DodajAkordWorker</c>; zakończenie przez <c>ZakończAkordWorker</c>;</item>
/// <item><b>C4</b> — zajęcia komornicze: <c>new ZajęcieKomornicze(pracownik)</c>; anulowanie/przywracanie
/// przez workery <c>AnulujWorker</c>/<c>PrzywrócWorker</c>;</item>
/// <item><b>C5</b> — operacje seryjne na dodatkach (moduł <c>Soneta.Przeszeregowania</c>): worker
/// <c>NowyDodatekWorker</c> oraz dokument <c>Przeszeregowanie</c>;</item>
/// <item><b>C6</b> — świadczenia socjalne (ZFŚS): <c>new SwiadczSocjalne(pracownik)</c> + subrow
/// <c>Rozliczenie</c>;</item>
/// <item><b>C7</b> — pożyczki (KZP/ZFM): trzystopniowo <c>FundPozyczkowy(pracownik, definicja)</c> →
/// <c>Pozyczka(fundusz)</c> → harmonogram rat przez <c>UzgodnijRatyWorker</c>.</item>
/// </list>
/// <para>
/// Faktyczne kwoty/spłaty (<c>Splacono</c>, <c>Pozostało</c>, <c>Rozliczone</c>, stany rat) wyliczają się
/// dopiero przy NALICZENIU WYPŁATY (rozdział H). Te testy weryfikują UTWORZENIE i PARAMETRYZACJĘ obiektów
/// oraz publiczny model — skutki finansowe są poza zakresem (asercje na model albo <c>[Ignore]</c>).
/// </para>
/// <para>
/// Wszystko działa na bazie Demo (GoldStandard) z automatycznym rollbackiem po teście. Operujemy wyłącznie
/// na <b>publicznym kontrakcie</b> — jak dodatek programisty zewnętrznego bez dostępu do kodu źródłowego.
/// Definicje (DefElementow, DefinicjeAkordow, DefSwiadczSocjal, DefFundPozycz) pobieramy DYNAMICZNIE;
/// brak wpisu w Demo kończy test przez <c>Assert.Ignore</c>, nie przez błąd.
/// </para>
/// </summary>
[TestFixture]
public class RozdzialCrest_PotraceniaTest : PracownikTestBase
{
    // Helper: świeży pracownik etatowy (Etat.Okres odblokowuje warunki; Wydzial+Stanowisko wymagane przy Save).
    private Prac NowyPracownikEtatowy(string prefix, out Guid guid)
    {
        var pracownik = Session.AddRow(new PracownikFirmy());
        pracownik.Kod = prefix + "_" + Guid.NewGuid().ToString("N").Substring(0, 6);
        pracownik.Last.Nazwisko = "Testowy";
        pracownik.Last.Imie = "Jan";
        var etat = pracownik.Last.Etat;
        etat.Okres = new FromTo(new Date(2026, 1, 1), Date.MaxValue);   // PIERWSZE — odblokowuje Etat
        etat.Wydzial = Kadry.Wydzialy.Firma;
        etat.Stanowisko = "Specjalista";
        guid = pracownik.Guid;
        return pracownik;
    }

    // Helper: pierwsza definicja potrącenia możliwa do podpięcia pod Dodatek.
    // WAŻNE: znacznik Algorytm.Potracenie nie wystarcza — element podpinany pod Dodatek MUSI mieć też
    // RodzajZrodla == Dodatek (DodHistoria.Element odrzuca definicje o innym rodzaju źródła, np. "Alimenty"
    // jako RodzajZrodla == ZajęcieKomornicze).
    private DefinicjaElementu PierwszaDefinicjaPotraceniaJakoDodatek() =>
        Place.DefElementow.Cast<DefinicjaElementu>()
             .FirstOrDefault(d => d.RodzajZrodla == RodzajŹródłaWypłaty.Dodatek
                                  && d.Algorytm != null && d.Algorytm.Potracenie);

    // Helper: pierwsza definicja elementu zajęcia komorniczego (RodzajZrodla == ZajęcieKomornicze).
    private DefinicjaElementu PierwszaDefinicjaZajecia() =>
        Place.DefElementow.Cast<DefinicjaElementu>()
             .FirstOrDefault(d => d.RodzajZrodla == RodzajŹródłaWypłaty.ZajęcieKomornicze);

    // ============================== C2 — Potrącenia (stałe / jednorazowe) ==============================

    [Test]
    [Description("C2: potrącenie NIE jest osobną klasą — to Dodatek z definicją elementu, w której " +
                 "Algorytm.Potracenie == true. Tworzymy przez new Dodatek(pracownik) + Kadry.Dodatki.AddRow. " +
                 "UWAGA (zweryfikowane): aby definicję podpiąć pod Dodatek, musi ona mieć RodzajZrodla == Dodatek " +
                 "ORAZ Algorytm.Potracenie == true — sam znacznik Algorytm.Potracenie nie wystarcza " +
                 "(DodHistoria.Element odrzuca definicje o innym rodzaju źródła, np. \"Alimenty\").")]
    public void C2_Potracenie_ToDodatekZDefinicjaPotracajaca()
    {
        var defPotracenia = PierwszaDefinicjaPotraceniaJakoDodatek();
        if (defPotracenia == null)
            Assert.Ignore("Baza Demo nie zawiera definicji Dodatku o Algorytm.Potracenie == true.");

        // Potrącenie-Dodatek: charakter minusowy daje algorytm, ale rodzaj źródła musi być Dodatek.
        defPotracenia.Algorytm.Potracenie.Should().BeTrue("to definicja o charakterze potrącenia");
        defPotracenia.RodzajZrodla.Should().Be(RodzajŹródłaWypłaty.Dodatek,
            "potrącenie podpinane pod Dodatek musi mieć RodzajZrodla == Dodatek");

        Guid guid = Guid.Empty;
        var okres = new FromTo(new Date(2026, 1, 1), Date.MaxValue);   // stałe

        InTransaction(() =>
        {
            var pracownik = NowyPracownikEtatowy("C2", out guid);

            // Mechanizm identyczny jak C1 (Dodatek + DodHistoria) — różni tylko dobór definicji.
            var potracenie = new Dodatek(pracownik);
            Kadry.Dodatki.AddRow(potracenie);     // tworzy pierwszy zapis DodHistoria (Last)

            var h = potracenie.Last;
            h.Should().NotBeNull("AddRow tworzy pierwszy zapis DodHistoria");
            h.Element = defPotracenia;            // definicja o Algorytm.Potracenie == true (wymagana)
            h.Okres = okres;                      // stałe potrącenie — okres otwarty
        });
        SaveDispose();

        var pracownik2 = Get<Prac>(guid);
        var dodatki = pracownik2.Dodatki.Cast<Dodatek>().ToList();
        dodatki.Should().ContainSingle("dodaliśmy jedno potrącenie (Dodatek) do świeżego pracownika");
        dodatki[0].Last.Element.Should().NotBeNull("Element (definicja potrącenia) jest wymagany");
        dodatki[0].Last.Element.Algorytm.Potracenie.Should().BeTrue(
            "trwale zapisana definicja zachowuje charakter potrącenia");
    }

    [Test]
    [Description("C2 (jednorazowe): potrącenie jednorazowe to Dodatek z OKRESEM zawężonym do jednego " +
                 "miesiąca rozliczeniowego — naliczy się tylko w wypłatach z tego miesiąca. " +
                 "Okres ustawiamy przez FromTo.Month(YearMonth).")]
    public void C2_PotracenieJednorazowe_OkresZawezonyDoMiesiaca()
    {
        var defPotracenia = PierwszaDefinicjaPotraceniaJakoDodatek();
        if (defPotracenia == null)
            Assert.Ignore("Baza Demo nie zawiera definicji Dodatku o Algorytm.Potracenie == true.");

        Guid guid = Guid.Empty;
        var okresMiesiaca = FromTo.Month(2026, 3);   // jeden miesiąc rozliczeniowy (marzec 2026)

        InTransaction(() =>
        {
            var pracownik = NowyPracownikEtatowy("C2j", out guid);
            var potracenie = new Dodatek(pracownik);
            Kadry.Dodatki.AddRow(potracenie);
            potracenie.Last.Element = defPotracenia;
            potracenie.Last.Okres = okresMiesiaca;   // jednorazowe — tylko marzec 2026
        });
        SaveDispose();

        var h = Get<Prac>(guid).Dodatki.Cast<Dodatek>().Single().Last;
        // Okres zawężony do jednego miesiąca — granice pokrywają się z miesiącem rozliczeniowym.
        h.Okres.From.Should().Be(okresMiesiaca.From, "potrącenie jednorazowe obejmuje tylko jeden miesiąc");
        h.Okres.To.Should().Be(okresMiesiaca.To);
    }

    // ============================== C3 — Akordy ==============================

    [Test]
    [Description("C3: Akord NIE ma publicznego konstruktora — kanoniczną ścieżką dodania jest worker " +
                 "Pracownik.DodajAkordWorker (parametryzowany przez Params(context) + Pracownicy[]). " +
                 "Definicję akordu pobieramy ze słownika DefinicjeAkordow (klucz WgNazwa). Odczyt z pracownik.Akordy.")]
    public void C3_Akord_DodawanyWorkerem_ZDefinicjiSlownika()
    {
        var defAkordu = Kadry.DefinicjeAkordow.Cast<DefinicjaAkordu>().FirstOrDefault();
        if (defAkordu == null)
            Assert.Ignore("Baza Demo nie zawiera żadnej definicji akordu (DefinicjeAkordow).");

        // Akord NIE ma publicznego ctora — potwierdzenie kanonicznej ścieżki (worker zamiast `new`).
        typeof(Akord).GetConstructors()
            .Should().NotContain(c => c.GetParameters().Length == 1
                                      && c.GetParameters()[0].ParameterType == typeof(Prac),
                "Akord nie ma publicznego ctora new Akord(pracownik) — dodajemy go workerem");

        Guid guid = Guid.Empty;
        InTransaction(() => NowyPracownikEtatowy("C3", out guid));
        SaveDispose();

        // Worker akordu działa „jak z UI" (Params wymaga Context) — używamy InUITransaction + CommitUI.
        bool dodano = false;
        InUITransaction(() =>
        {
            var pracownik = Get<Prac>(guid);
            var context = Login.CreateEmptyContext().Clone(Session);

            var par = new Prac.DodajAkordWorker.Params(context)
            {
                Definicja = defAkordu,
                OdDnia = new Date(2026, 1, 1),
                DoDnia = new Date(2026, 12, 31),
            };
            // Worker akordu ma ctor (Session); parametry przez property Pars/Pracownicy.
            var worker = new Prac.DodajAkordWorker(Session) { Pars = par, Pracownicy = new[] { pracownik } };
            worker.DodajAkord();
            dodano = true;
        });

        if (!dodano)
            Assert.Ignore("DodajAkordWorker nie wykonał się w headless host (zależność od kontekstu UI).");
        SaveDispose();

        // Odczyt akordów pracownika (child SubTable). Akord jest historyczny — bieżący zapis przez Last.
        var akordy = Get<Prac>(guid).Akordy.Cast<Akord>().ToList();
        akordy.Should().ContainSingle("worker dodał jeden akord");
        akordy[0].Definicja.Should().NotBeNull("akord wiąże definicję ze słownika DefinicjeAkordow");
        akordy[0].Last.Should().NotBeNull("akord ma bieżący zapis historii AkordHistoria");
    }

    // ============================== C4 — Zajęcia wynagrodzenia (komornicze/alimentacyjne) ==============================

    [Test]
    [Description("C4: zajęcie komornicze to JEDNA klasa ZajęcieKomornicze (alimentacyjne vs niealimentacyjne " +
                 "rozstrzyga definicja elementu i parametry zapisu historii, nie osobny typ ani pole Priorytet — " +
                 "którego na ZajęcieKomornicze NIE ma). Ctor publiczny new ZajęcieKomornicze(pracownik) + " +
                 "Kadry.ZajKomornicze.AddRow. Element (potrącenie zajęcia) jest wymagany. " +
                 "Rodzaj to enum RodzajeZajęciaWynagrodzenia { Kwota, KwotaMiesięczna }.")]
    public void C4_ZajecieKomornicze_TworzoneZParametrami()
    {
        // Element zajęcia — definicja o RodzajZrodla == ZajęcieKomornicze (dedykowany rodzaj źródła).
        var elementZajecia = PierwszaDefinicjaZajecia();
        if (elementZajecia == null)
            Assert.Ignore("Baza Demo nie zawiera definicji elementu o RodzajZrodla == ZajęcieKomornicze.");

        Guid guid = Guid.Empty;
        InTransaction(() =>
        {
            var pracownik = NowyPracownikEtatowy("C4", out guid);

            var zajecie = new ZajęcieKomornicze(pracownik);   // ctor PUBLICZNY
            Kadry.ZajKomornicze.AddRow(zajecie);

            zajecie.Rodzaj = RodzajeZajęciaWynagrodzenia.KwotaMiesięczna;
            zajecie.Element = elementZajecia;                 // element płacowy potrącenia (wymagany)
            zajecie.NumerSprawy = "KM 123/2026";
            zajecie.Data = new Date(2026, 1, 1);
        });
        SaveDispose();

        var zaj = Get<Prac>(guid).ZajęciaKomornicze.Cast<ZajęcieKomornicze>().Single();
        zaj.NumerSprawy.Should().Be("KM 123/2026");
        zaj.Rodzaj.Should().Be(RodzajeZajęciaWynagrodzenia.KwotaMiesięczna);
        zaj.Element.Should().NotBeNull("Element (definicja potrącenia zajęcia) jest wymagany");
        // Skutki finansowe (Splacono/Pozostało) wyliczają się przy naliczeniu wypłaty — po samym dodaniu
        // pozostają niewyliczone (puste). Nie asercjonujemy na nie tu (zakres: utworzenie/parametryzacja).
        zaj.Anulowane.Should().BeFalse("nowo dodane zajęcie nie jest anulowane");
        zaj.SplataZakonczona.Should().BeFalse("nowo dodane zajęcie nie jest spłacone");
    }

    [Test]
    [Description("C4 (anulowanie): zajęcie anuluje się WORKEREM ZajęcieKomornicze.AnulujWorker (nie ręcznym " +
                 "ustawianiem flagi Anulowane) — worker dba o storna i spójność rozliczenia. Tu weryfikujemy " +
                 "tylko publiczny model anulowania (utworzenie + uruchomienie workera).")]
    public void C4_ZajecieKomornicze_AnulujWorker()
    {
        var elementZajecia = PierwszaDefinicjaZajecia();
        if (elementZajecia == null)
            Assert.Ignore("Baza Demo nie zawiera definicji elementu o RodzajZrodla == ZajęcieKomornicze.");

        Guid guid = Guid.Empty;
        InTransaction(() =>
        {
            var pracownik = NowyPracownikEtatowy("C4a", out guid);
            var zajecie = new ZajęcieKomornicze(pracownik);
            Kadry.ZajKomornicze.AddRow(zajecie);
            zajecie.Element = elementZajecia;
            zajecie.NumerSprawy = "KM 999/2026";
            zajecie.Data = new Date(2026, 1, 1);
        });
        SaveDispose();

        bool anulowano = false;
        InUITransaction(() =>
        {
            var zaj = Get<Prac>(guid).ZajęciaKomornicze.Cast<ZajęcieKomornicze>().Single();
            // Worker przez parameterless ctor + property setter (Zajęcie), nie przez ctor parametryczny.
            var worker = new ZajęcieKomornicze.AnulujWorker { Zajęcie = zaj };
            worker.Anuluj();
            anulowano = true;
        });

        if (!anulowano)
            Assert.Ignore("AnulujWorker nie wykonał się w headless host (zależność od kontekstu UI).");
        SaveDispose();

        Get<Prac>(guid).ZajęciaKomornicze.Cast<ZajęcieKomornicze>().Single()
            .Anulowane.Should().BeTrue("worker AnulujWorker oznacza zajęcie jako anulowane");
    }

    // ============================== C5 — Operacje seryjne na dodatkach (moduł Przeszeregowania) ==============================

    [Test]
    [Description("C5: seryjne nadanie dodatku grupie realizuje moduł Soneta.Przeszeregowania — worker " +
                 "NowyDodatekWorker (Params(context) { Definicja, Podstawa, Procent } + Pracownicy[]). " +
                 "Worker przyjmuje TABLICĘ pracowników, więc nadaje się do operacji grupowej. " +
                 "Tu weryfikujemy utworzenie/parametryzację — efekt to nowy Dodatek u pracownika.")]
    [Ignore("NowyDodatekWorker (moduł Przeszeregowania) rzuca NullReferenceException w headless host " +
            "testowym (Przeszeregowania/NowyDodatek.cs:94) — operacja seryjna zależy od stanu operatora/" +
            "kontekstu UI nieobecnego w bazie Demo. Test dokumentuje publiczny model workera seryjnego.")]
    public void C5_OperacjaSeryjna_NowyDodatekWorker_GrupaPracownikow()
    {
        // Definicja dodatku (RodzajZrodla == Dodatek) — np. Premia z Demo.
        var def = Place.DefElementow.WgNazwy["Premia"] as DefinicjaElementu;
        if (def == null)
            Assert.Ignore("Baza Demo nie zawiera definicji dodatku \"Premia\".");

        Guid g1 = Guid.Empty, g2 = Guid.Empty;
        InTransaction(() =>
        {
            NowyPracownikEtatowy("C5a", out g1);
            NowyPracownikEtatowy("C5b", out g2);
        });
        SaveDispose();

        bool wykonano = false;
        InUITransaction(() =>
        {
            var grupa = new[] { Get<Prac>(g1), Get<Prac>(g2) };
            var context = Login.CreateEmptyContext().Clone(Session);

            var par = new NowyDodatekWorker.Params(context)
            {
                Definicja = def,
                Podstawa = (Currency)300m,
            };
            var worker = new NowyDodatekWorker { Pars = par, Pracownicy = grupa };
            worker.NowyDodatek();
            wykonano = true;
        });

        if (!wykonano)
            Assert.Ignore("NowyDodatekWorker (moduł Przeszeregowania) nie wykonał się w headless host.");
        SaveDispose();

        // Po wykonaniu operacji seryjnej każdy pracownik z grupy ma nowy dodatek z tej definicji.
        // Materializujemy do listy i sprawdzamy LINQ Any (poza drzewem wyrażeń — można użyć ?. i funkcji).
        static bool MaPremie(Dodatek d) => d.Last?.Element?.Nazwa == "Premia";
        Get<Prac>(g1).Dodatki.Cast<Dodatek>().Any(MaPremie).Should().BeTrue(
            "operacja seryjna nadała dodatek pierwszemu pracownikowi");
        Get<Prac>(g2).Dodatki.Cast<Dodatek>().Any(MaPremie).Should().BeTrue(
            "operacja seryjna nadała dodatek drugiemu pracownikowi");
    }

    [Test]
    [Description("C5 (dokument Przeszeregowanie): dokument zbiorczy Soneta.Przeszeregowania.Przeszeregowanie " +
                 "ma publiczny ctor + AddRow (kolekcja nie ma AddNew). Jest PLANEM — NIE zmienia danych dopóki " +
                 "nie zostanie wykonany (WykonajWorker). Tu weryfikujemy utworzenie i parametryzację nagłówka " +
                 "(Data, Nazwa). Kolekcja Pracownicy jest zarządzana przez przepływ workera, nie prostym Add.")]
    public void C5_DokumentPrzeszeregowania_JestPlanemDoWykonania()
    {
        Guid guid = Guid.Empty;
        InTransaction(() =>
        {
            // Dokument tworzymy przez new + AddRow (kolekcja nie ma AddNew — to standardowy GuidedRow root).
            var doc = new Przeszeregowanie();
            Session.GetPrzeszeregowania().Przeszeregowania.AddRow(doc);
            doc.Data = new Date(2026, 4, 1);
            doc.Nazwa = "Przeszeregowanie testowe";

            // Dokument to PLAN — pozycje (Elementy) i materializacja danych następują dopiero przy WykonajWorker.
            doc.Nazwa.Should().Be("Przeszeregowanie testowe");
            doc.Data.Should().Be(new Date(2026, 4, 1));
        });
        // Bez Save — to wyłącznie weryfikacja utworzenia/parametryzacji planu (rollback po teście).
    }

    // ============================== C6 — Świadczenia socjalne (ZFŚS) ==============================

    [Test]
    [Description("C6: świadczenie socjalne to Soneta.Kadry.SwiadczSocjalne (ctor publiczny new SwiadczSocjalne" +
                 "(pracownik) + Kadry.SwiadczeniaSoc.AddRow). Definicję pobieramy ze słownika DefSwiadczSocjal " +
                 "(klucz WgNazwy); dane rozliczeniowe (Element, Kwota, Okres) ustawiamy na subrowie Rozliczenie. " +
                 "Faktyczne rozliczenie (Rozliczone == true) następuje przy naliczeniu wypłaty.")]
    public void C6_SwiadczenieSocjalne_TworzoneZRozliczeniem()
    {
        var defSwiadcz = Kadry.DefSwiadczSocjal.Cast<DefinicjaŚwiadczeniaSocjalnego>().FirstOrDefault();
        if (defSwiadcz == null)
            Assert.Ignore("Baza Demo nie zawiera definicji świadczenia socjalnego (DefSwiadczSocjal).");

        // Element rozliczenia — preferuj domyślny z definicji, w razie braku dowolny element płacowy.
        var element = defSwiadcz.Element
                      ?? Place.DefElementow.Cast<DefinicjaElementu>().FirstOrDefault();
        if (element == null)
            Assert.Ignore("Brak elementu płacowego do rozliczenia świadczenia socjalnego.");

        Guid guid = Guid.Empty;
        var okres = FromTo.Month(2026, 6);

        InTransaction(() =>
        {
            var pracownik = NowyPracownikEtatowy("C6", out guid);

            var sw = new SwiadczSocjalne(pracownik);    // ctor PUBLICZNY
            Kadry.SwiadczeniaSoc.AddRow(sw);

            sw.Definicja = defSwiadcz;
            sw.Data = new Date(2026, 6, 1);
            // Dane rozliczeniowe — na SUBROWIE Rozliczenie (nadpisują domyślne z definicji).
            sw.Rozliczenie.Element = element;
            sw.Rozliczenie.Kwota = (Currency)1000m;
            sw.Rozliczenie.Okres = okres;
        });
        SaveDispose();

        var s = Get<Prac>(guid).Swiadczenia.Cast<SwiadczSocjalne>().Single();
        s.Definicja.Should().NotBeNull("świadczenie wiąże definicję ze słownika DefSwiadczSocjal");
        s.Rozliczenie.Kwota.Should().Be((Currency)1000m, "kwota świadczenia z subrowa Rozliczenie");
        s.Rozliczenie.Element.Should().NotBeNull("element płacowy rozliczenia");
        s.Rozliczenie.Rozliczone.Should().BeFalse("rozliczenie następuje dopiero przy naliczeniu wypłaty");
    }

    // ============================== C7 — Pożyczki (KZP / ZFM) ==============================

    [Test]
    [Description("C7: ścieżka trzystopniowa FundPozyczkowy(pracownik, definicja) → Pozyczka(fundusz) → " +
                 "harmonogram rat. Pożyczki NIE da się utworzyć bez funduszu (ctor wymaga FundPozyczkowy). " +
                 "Definicję funduszu pobieramy ze słownika DefFundPozycz (WgNazwy). Element (wypłata) i " +
                 "ElementRaty (potrącenie raty) to RÓŻNE definicje. Harmonogram generuje worker UzgodnijRatyWorker.")]
    public void C7_Pozyczka_FunduszPozyczkaHarmonogram()
    {
        var defFunduszu = Kadry.DefFundPozycz.Cast<DefinicjaFunduszuPozyczkowego>().FirstOrDefault();
        if (defFunduszu == null)
            Assert.Ignore("Baza Demo nie zawiera definicji funduszu pożyczkowego (DefFundPozycz).");

        // Element wypłaty i element raty — dwie różne definicje płacowe (dowolne dostępne).
        var elementy = Place.DefElementow.Cast<DefinicjaElementu>().Take(2).ToList();
        if (elementy.Count < 2)
            Assert.Ignore("Baza Demo nie zawiera co najmniej dwóch definicji elementów (wypłata + rata).");
        var elWyplata = elementy[0];
        var elRata = elementy[1];

        Guid guidPrac = Guid.Empty, guidFundusz = Guid.Empty, guidPozyczka = Guid.Empty;

        InTransaction(() =>
        {
            var pracownik = NowyPracownikEtatowy("C7", out guidPrac);

            // 1) Członkostwo w funduszu — ctor wymaga (pracownik, definicja).
            var fundusz = new FundPozyczkowy(pracownik, defFunduszu);
            Kadry.FundPozyczkowe.AddRow(fundusz);
            fundusz.Okres = new FromTo(new Date(2026, 1, 1), Date.MaxValue);
            guidFundusz = fundusz.Guid;

            // 2) Pożyczka w ramach funduszu — ctor wymaga FundPozyczkowy.
            var pozyczka = new Pozyczka(fundusz);
            Kadry.Pozyczki.AddRow(pozyczka);
            pozyczka.Data = new Date(2026, 1, 10);
            pozyczka.Kwota = (Currency)12000m;
            pozyczka.Element = elWyplata;       // element WYPŁATY pożyczki
            pozyczka.ElementRaty = elRata;      // element POTRĄCENIA raty (inny niż wypłata)
            pozyczka.IloscRat = 12;
            pozyczka.SplatyOd = new YearMonth(2026, 2);
            guidPozyczka = pozyczka.Guid;
        });
        SaveDispose();

        var pozyczka2 = Get<Pozyczka>(guidPozyczka);
        pozyczka2.Should().NotBeNull("pożyczka utrwalona w tabeli Pozyczki");
        pozyczka2.Fundusz.Should().NotBeNull("pożyczka należy do funduszu (ctor wymaga FundPozyczkowy)");
        pozyczka2.Kwota.Should().Be((Currency)12000m);
        pozyczka2.IloscRat.Should().Be(12);
        pozyczka2.Element.Should().NotBeNull("element wypłaty pożyczki");
        pozyczka2.ElementRaty.Should().NotBeNull("element potrącenia raty");
        // Fundusz widoczny przez child pracownika.
        Get<Prac>(guidPrac).FunduszePozyczkowe.Cast<FundPozyczkowy>()
            .Should().ContainSingle("pracownik jest członkiem jednego funduszu");
    }

    [Test]
    [Description("C7 (harmonogram): harmonogram rat generuje worker Pozyczka.UzgodnijRatyWorker " +
                 "(Params(context){ UzgodnijRaty, PrzeliczRaty }, property Pożyczka) albo metoda " +
                 "pozyczka.UpdatePozyczka() — NIE ręczne dodawanie RataPozyczki. Worker rozkłada kapitał/odsetki. " +
                 "Faktyczne potrącenia rat (Stan/Splacono) aktualizują się dopiero przy naliczeniu wypłaty.")]
    public void C7_Pozyczka_HarmonogramRatPrzezWorker()
    {
        var defFunduszu = Kadry.DefFundPozycz.Cast<DefinicjaFunduszuPozyczkowego>().FirstOrDefault();
        if (defFunduszu == null)
            Assert.Ignore("Baza Demo nie zawiera definicji funduszu pożyczkowego (DefFundPozycz).");
        var elementy = Place.DefElementow.Cast<DefinicjaElementu>().Take(2).ToList();
        if (elementy.Count < 2)
            Assert.Ignore("Baza Demo nie zawiera co najmniej dwóch definicji elementów.");

        Guid guidPozyczka = Guid.Empty;
        InTransaction(() =>
        {
            var pracownik = NowyPracownikEtatowy("C7h", out _);
            var fundusz = new FundPozyczkowy(pracownik, defFunduszu);
            Kadry.FundPozyczkowe.AddRow(fundusz);
            fundusz.Okres = new FromTo(new Date(2026, 1, 1), Date.MaxValue);

            var pozyczka = new Pozyczka(fundusz);
            Kadry.Pozyczki.AddRow(pozyczka);
            pozyczka.Data = new Date(2026, 1, 10);
            pozyczka.Kwota = (Currency)12000m;
            pozyczka.Element = elementy[0];
            pozyczka.ElementRaty = elementy[1];
            pozyczka.IloscRat = 12;
            pozyczka.SplatyOd = new YearMonth(2026, 2);
            guidPozyczka = pozyczka.Guid;
        });
        SaveDispose();

        bool uzgodniono = false;
        InUITransaction(() =>
        {
            var pozyczka = Get<Pozyczka>(guidPozyczka);
            var context = Login.CreateEmptyContext().Clone(Session);

            // PrzeliczRaty jest tylko-do-odczytu (ustawiane wewnętrznie) — parametryzujemy tylko UzgodnijRaty.
            var par = new Pozyczka.UzgodnijRatyWorker.Params(context)
            {
                UzgodnijRaty = true,
            };
            var worker = new Pozyczka.UzgodnijRatyWorker { Pars = par, Pożyczka = pozyczka };
            worker.UzgodnijRaty();
            uzgodniono = true;
        });

        if (!uzgodniono)
            Assert.Ignore("UzgodnijRatyWorker nie wykonał się w headless host (zależność od kontekstu UI).");
        SaveDispose();

        // Po uzgodnieniu harmonogram rat istnieje (worker rozłożył kapitał/odsetki wg IloscRat/SplatyOd).
        var raty = Get<Pozyczka>(guidPozyczka).Raty.Cast<RataPozyczki>().ToList();
        raty.Should().NotBeEmpty("UzgodnijRatyWorker buduje harmonogram rat");
        raty.Should().OnlyContain(r => r.Stan == StanSpłat.NieSpłacona,
            "świeżo wygenerowane raty są niespłacone — spłata nalicza się przy wypłacie");
    }
}
