using System;
using System.Linq;
using AwesomeAssertions;
using NUnit.Framework;
using Soneta.Business;
using Soneta.Core;
using Soneta.CRM;
using Soneta.Kadry;
using Soneta.Types;
using Prac = Soneta.Kadry.Pracownik;

namespace Soneta.Skills.Test.KadryPlace.Pracownik;

/// <summary>
/// Rozdział A (część kartotekowa) — pozostałe receptury danych osobowych/kadrowych pracownika:
/// A3 (adresy), A4 (kontakt), A5 (rachunki — odczyt), A6 (PIT), A8 (ZUS/NFZ), A11 (wykształcenie/
/// języki/wojsko), A12 (GUS/kod zawodu), A13 (PFRON), A15 (odczyt „na dzień"), A16 (powiązanie
/// z kontrahentem), A17 (archiwum — workery), A18 (zwolnienie), A19 (przerejestrowanie — zmiana Tyub4).
/// <para>
/// Testy są <b>wykonywalną dokumentacją</b> publicznego kontraktu Soneta dla domeny Kadry/Płace.
/// Operujemy wyłącznie na publicznym API — jak dodatek zewnętrzny bez dostępu do kodu źródłowego.
/// Wszystko działa na bazie Demo (GoldStandard) z rollbackiem po teście. Wartości enumów i klucze
/// słowników pobieramy/weryfikujemy dynamicznie, nie zgadujemy.
/// </para>
/// </summary>
[TestFixture]
public class RozdzialArest_KartotekaTest : PracownikTestBase
{
    // Helper: świeży pracownik z danymi osobowymi (Last istnieje od razu po AddRow).
    private Prac NowyPracownik(string prefix, out Guid guid)
    {
        var pracownik = Session.AddRow(new PracownikFirmy());
        pracownik.Kod = prefix + "_" + Guid.NewGuid().ToString("N").Substring(0, 6);
        pracownik.Last.Nazwisko = "Testowy";   // Nazwisko wymagane przy Save
        pracownik.Last.Imie = "Jan";
        guid = pracownik.Guid;
        return pracownik;
    }

    // Helper: pracownik z USTAWIONYM etatem. Cały subrow Etat jest tylko-do-odczytu, dopóki nie
    // ustawi się Etat.Okres (bramka, patrz B1). Po jego ustawieniu pracownik staje się etatowy, więc
    // Save wymaga Etat.Wydzial ORAZ Etat.Stanowisko — ustawiamy oba.
    private Prac NowyPracownikEtatowy(string prefix, out Guid guid)
    {
        var pracownik = NowyPracownik(prefix, out guid);
        var etat = pracownik.Last.Etat;
        etat.Okres = new FromTo(new Date(2026, 1, 1), Date.MaxValue);   // PIERWSZE — odblokowuje Etat
        etat.Wydzial = Kadry.Wydzialy.Firma;                            // wymagane dla etatu
        etat.Stanowisko = "Specjalista";                               // wymagane dla etatu
        return pracownik;
    }

    // ============================== A3 — Adresy ==============================

    [Test]
    [Description("A3: adresy (zameldowania/zamieszkania/korespondencyjny) to subrowy Soneta.Core.Adres " +
                 "na zapisie historii (Last) — modyfikujemy ich pola, nie przypisujemy całego obiektu.")]
    public void A3_Adresy_SaSubrowamiNaZapisieHistorii()
    {
        var g = Guid.Empty;
        InTransaction(() =>
        {
            var ph = NowyPracownik("A3", out g).Last;
            ph.AdresZamieszkania.Miejscowosc = "Kraków";
            ph.AdresZamieszkania.Ulica = "Wadowicka";
            ph.AdresZamieszkania.NrDomu = "8A";
            ph.AdresZamieszkania.NrLokalu = "12";
            ph.AdresZamieszkania.KodPocztowyS = "30-415";
            ph.AdresZameldowania.Miejscowosc = "Wieliczka";
            ph.AdresDoKorespondencji.Miejscowosc = "Kraków";
        });
        SaveDispose();

        var ph2 = Get<Prac>(g).Last;
        ph2.AdresZamieszkania.Ulica.Should().Be("Wadowicka");
        ph2.AdresZamieszkania.NrDomu.Should().Be("8A");
        ph2.AdresZamieszkania.KodPocztowyS.Should().Be("30-415");
        ph2.AdresZamieszkania.KodPocztowy.Should().Be(30415);   // int (bez myślnika)
        ph2.AdresZameldowania.Miejscowosc.Should().Be("Wieliczka");
        ph2.AdresDoKorespondencji.Miejscowosc.Should().Be("Kraków");

        // Odczyt adresu na dzień:
        Adres adr = Get<Prac>(g)[Date.Today].AdresZamieszkania;
        adr.Miejscowosc.Should().Be("Kraków");
    }

    // ============================== A4 — Dane kontaktowe ==============================

    [Test]
    [Description("A4: dane kontaktowe (EMAIL/TelefonKomorkowy/WWW) to subrow Soneta.Core.Kontakt " +
                 "na zapisie historii — pole nazywa się EMAIL (wielkie litery).")]
    public void A4_Kontakt_EmailTelefonWWW_NaSubrowieKontakt()
    {
        var g = Guid.Empty;
        InTransaction(() =>
        {
            var k = NowyPracownik("A4", out g).Last.Kontakt;   // subrow Kontakt
            k.EMAIL = "g.kowalska@firma.pl";
            k.TelefonKomorkowy = "600100200";
            k.WWW = "https://firma.pl/g.kowalska";
        });
        SaveDispose();

        var k2 = Get<Prac>(g).Last.Kontakt;
        k2.EMAIL.Should().Be("g.kowalska@firma.pl");
        k2.TelefonKomorkowy.Should().Be("600100200");
        k2.WWW.Should().Be("https://firma.pl/g.kowalska");
    }

    [Test]
    [Description("A4 (dostęp WWW/Pulpity): konto operatora web (IWebOperator) NIE jest zwykłym " +
                 "zapisywalnym polem PracHistoria — zarządza nim osobny mechanizm operatorów modułu web.")]
    [Ignore("Dostęp do Pulpitów (IWebOperator) to osobny mechanizm operatorów/uprawnień web, " +
            "nie pole kartoteki kadrowej — poza publicznym kontraktem ustawiania pól na pracowniku.")]
    public void A4_DostepWWW_PulpityToOsobnyMechanizm()
    {
    }

    // ============================== A5 — Rachunki bankowe (ODCZYT) ==============================

    [Test]
    [Description("A5 (odczyt): rachunki pracownika to kolekcja Pracownik.Rachunki " +
                 "(SubTable<RachunekBankowyPodmiotu>); rachunek główny zwraca Pracownik.DomyslnyRachunek.")]
    public void A5_Rachunki_OdczytKolekcjiIRachunkuGlownego()
    {
        // Czysty odczyt na pracowniku z Demo — bez tworzenia rachunku (ctor numeru rachunku to typ
        // biznesowy z walidacją IBAN/NRB, poza prostym kontraktem ustawiania pól).
        var p = PierwszyPracownik();

        // API odczytu istnieje i nie rzuca — kolekcja i property domyślnego rachunku.
        System.Action odczyt = () =>
        {
            var glowny = p.DomyslnyRachunek;   // może być null gdy brak rachunku
            if (glowny != null)
            {
                _ = glowny.Domyslne;
                _ = glowny.Rachunek;           // subrow rachunku
            }
            foreach (var r in p.Rachunki)
            {
                _ = r.Domyslne;
                _ = r.Priorytet;
            }
        };
        odczyt.Should().NotThrow("Rachunki/DomyslnyRachunek to publiczny odczyt kontraktu A5");
    }

    // ============================== A6 — Dane podatkowe (PIT) ==============================

    [Test]
    [Description("A6: dane PIT to subrow PracHistoria.Podatki — KosztyRodzaj/TypProgow/UlgaCzesc to ENUMY, " +
                 "UlgaMnoznik to decimal (PIT-2). Wartości enumów pobieramy z realnych nazw składowych.")]
    public void A6_DanePodatkowe_NaSubrowiePodatki()
    {
        var g = Guid.Empty;
        InTransaction(() =>
        {
            var pdt = NowyPracownik("A6", out g).Last.Podatki;
            pdt.KosztyRodzaj = RodzajKosztowUzyskania.JedenStosPracy;   // enum (jeden stosunek pracy)
            pdt.UlgaMnoznik = 1m;                                       // pełna kwota zmniejszająca (PIT-2)
            pdt.UlgaCzesc = UlgaPodatkowaCzesc.Ulga112;                 // podział PIT-2 (1/1)
            pdt.TypProgow = TypProgowPodatkowych.Standardowe;           // enum
        });
        SaveDispose();

        var pdt2 = Get<Prac>(g).Last.Podatki;
        pdt2.KosztyRodzaj.Should().Be(RodzajKosztowUzyskania.JedenStosPracy);
        pdt2.UlgaMnoznik.Should().Be(1m);
        pdt2.UlgaCzesc.Should().Be(UlgaPodatkowaCzesc.Ulga112);
        pdt2.TypProgow.Should().Be(TypProgowPodatkowych.Standardowe);
    }

    // ============================== A8 — ZUS / NFZ ==============================

    [Test]
    [Description("A8: oddział NFZ to subrow PracHistoria.OddzialNFZ (OdDnia: Date) — zapisywalny. " +
                 "DodSwiadczeniaZUS na świeżym zapisie jest tylko-do-odczytu (cały subrow zablokowany).")]
    public void A8_DodatkoweSwiadczeniaZUS_IOddzialNFZ()
    {
        var g = Guid.Empty;
        InTransaction(() =>
        {
            var ph = NowyPracownik("A8", out g).Last;
            // ROZBIEŻNOŚĆ z dokumentem: na świeżym zapisie CAŁY subrow DodSwiadczeniaZUS jest
            // tylko-do-odczytu (ColReadOnlyException nawet dla Numer) — staje się edytowalny dopiero
            // gdy świadczenie zostanie zainicjowane (np. przez UI/kreator). Tu ustawiamy NFZ.
            ph.OddzialNFZ.OdDnia = new Date(2026, 1, 1);
        });
        SaveDispose();

        var ph2 = Get<Prac>(g).Last;
        ph2.OddzialNFZ.OdDnia.Should().Be(new Date(2026, 1, 1));

        // Odczyt dodatkowych świadczeń ZUS — publiczny i nie rzuca (Rodzaj/Okres do odczytu).
        System.Action odczyt = () =>
        {
            _ = ph2.DodSwiadczeniaZUS.Rodzaj;
            _ = ph2.DodSwiadczeniaZUS.Okres;
        };
        odczyt.Should().NotThrow("dane dodatkowych świadczeń ZUS są dostępne do odczytu");
    }

    // ============================== A11 — Wykształcenie / języki / wojsko ==============================

    [Test]
    [Description("A11: wykształcenie i wojsko to subrowy PracHistoria (Kod/Stosunek/KategoriaZdrowia = " +
                 "ENUMY); języki obce to kolekcja na rootcie Pracownik.JęzykiObce.")]
    public void A11_WyksztalcenieWojsko_NaHistorii_JezykiNaRootcie()
    {
        var g = Guid.Empty;
        InTransaction(() =>
        {
            var ph = NowyPracownik("A11", out g).Last;

            ph.Wyksztalcenie.Kod = KodWyksztalcenia.Wyzsze;             // enum
            ph.Wyksztalcenie.TytulNaukowy = "mgr inż.";

            ph.Wojsko.Stosunek = KodStosDoSluzbyWojskowej.Rezerwa;      // enum (uregulowany = rezerwa)
            ph.Wojsko.KategoriaZdrowia = KategoriaZdrowia.A;            // enum
            ph.Wojsko.NrKsiazeczki = "AB123456";
        });
        SaveDispose();

        var ph2 = Get<Prac>(g).Last;
        ph2.Wyksztalcenie.Kod.Should().Be(KodWyksztalcenia.Wyzsze);
        ph2.Wyksztalcenie.TytulNaukowy.Should().Be("mgr inż.");
        ph2.Wojsko.Stosunek.Should().Be(KodStosDoSluzbyWojskowej.Rezerwa);
        ph2.Wojsko.KategoriaZdrowia.Should().Be(KategoriaZdrowia.A);
        ph2.Wojsko.NrKsiazeczki.Should().Be("AB123456");

        // Odczyt kolekcji języków obcych (na rootcie) — nie rzuca; może być pusta.
        System.Action czytajJezyki = () =>
        {
            foreach (var j in Get<Prac>(g).JęzykiObce) { _ = j.Jezyk; }
        };
        czytajJezyki.Should().NotThrow("JęzykiObce to publiczna kolekcja na rootcie pracownika");
    }

    // ============================== A12 — GUS / kod zawodu ==============================

    [Test]
    [Description("A12: dane statystyczne GUS to subrow PracHistoria.GUS (KodWyksztalcenia = enum " +
                 "KodWykształceniaGUS, INNE niż A11); kod zawodu to Etat.KodWykonywanegoZawodu (int).")]
    public void A12_DaneGUS_IKodZawodu()
    {
        var g = Guid.Empty;
        InTransaction(() =>
        {
            // Etat.KodWykonywanegoZawodu jest tylko-do-odczytu, dopóki nie ustawi się Etat.Okres
            // (bramka subrowa Etat, patrz B1) — używamy więc pracownika z ustawionym etatem.
            var ph = NowyPracownikEtatowy("A12", out g).Last;
            ph.GUS.KodWyksztalcenia = KodWykształceniaGUS.Wyższe;   // enum GUS (z diakrytykiem)
            ph.GUS.GlowneMiejscePracy = true;
            ph.GUS.PierwszaPraca = false;
            ph.Etat.KodWykonywanegoZawodu = 251401;                // kod zawodu GUS (int)
        });
        SaveDispose();

        var ph2 = Get<Prac>(g).Last;
        ph2.GUS.KodWyksztalcenia.Should().Be(KodWykształceniaGUS.Wyższe);
        ph2.GUS.GlowneMiejscePracy.Should().BeTrue();
        ph2.Etat.KodWykonywanegoZawodu.Should().Be(251401);
    }

    // ============================== A13 — PFRON ==============================

    [Test]
    [Description("A13: dane PFRON/niepełnosprawność to subrow PracHistoria.PFRON — Stopien = enum " +
                 "StNiepełnosprawności, Okres = FromTo, daty = Soneta.Types.Date.")]
    public void A13_PFRON_StopienOkresIDaty()
    {
        var g = Guid.Empty;
        var okres = new FromTo(new Date(2026, 1, 1), new Date(2028, 12, 31));
        InTransaction(() =>
        {
            var pfron = NowyPracownik("A13", out g).Last.PFRON;
            pfron.Stopien = StNiepełnosprawności.Umiarkowany;     // enum
            pfron.Okres = okres;
            pfron.DataOrzeczenia = new Date(2025, 12, 1);
            pfron.DataDostarczenia = new Date(2025, 12, 15);
        });
        SaveDispose();

        var pfron2 = Get<Prac>(g).Last.PFRON;
        pfron2.Stopien.Should().Be(StNiepełnosprawności.Umiarkowany);
        pfron2.Okres.From.Should().Be(okres.From);
        pfron2.Okres.To.Should().Be(okres.To);
        pfron2.DataOrzeczenia.Should().Be(new Date(2025, 12, 1));
        pfron2.DataDostarczenia.Should().Be(new Date(2025, 12, 15));
    }

    // ============================== A15 — Odczyt „na dzień" ==============================

    [Test]
    [Description("A15 (odczyt): indeksator pracownik[date] zwraca zapis obowiązujący na datę (Aktualnosc " +
                 "zawiera date), null dla daty sprzed zatrudnienia; GetFirst()/Last to skrajne zapisy.")]
    public void A15_OdczytNaDzien_ZwracaZapisLubNull()
    {
        var p = PierwszyPracownik();   // zatrudniony etatowo pracownik z Demo

        // 1) Zapis na dziś — istnieje dla zatrudnionego pracownika.
        var phDzis = p[Date.Today];
        phDzis.Should().NotBeNull("pracownik etatowy z Demo ma zapis obowiązujący na dziś");

        // 2) Indeksator == kolekcja Historia[date].
        p[Date.Today].Should().BeSameAs(p.Historia[Date.Today]);

        // 3) Skrajne zapisy.
        var pierwszy = p.Historia.GetFirst();
        var ostatni = p.Last;
        pierwszy.Should().NotBeNull();
        ostatni.Should().NotBeNull();
        p[Date.Today].Should().BeSameAs(p.Historia.GetLast(), "Last == Historia.GetLast()");

        // 4) Data sprzed zatrudnienia → brak zapisu (null).
        if (pierwszy.Aktualnosc.From > Date.MinValue)
        {
            var przed = pierwszy.Aktualnosc.From.AddDays(-1);
            p[przed].Should().BeNull("dla daty sprzed pierwszego zapisu nie ma zapisu historii");
        }
    }

    // ============================== A16 — Powiązanie z kontrahentem ==============================

    [Test]
    [Description("A16: powiązanie pracownika z istniejącym kontrahentem to zapisywalne pole rootu " +
                 "Pracownik.PowiazanyKontrahent (referencja, ta sama sesja); null = brak powiązania.")]
    public void A16_PowiazanyKontrahent_UstawianyNaRootcie()
    {
        // Istniejący kontrahent z Demo (z tej samej sesji co pracownik).
        var kontrahent = Session.GetCRM().Kontrahenci.WgKodu.Cast<Soneta.CRM.Kontrahent>().First();

        var g = Guid.Empty;
        InTransaction(() =>
        {
            var pracownik = NowyPracownik("A16", out g);
            pracownik.PowiazanyKontrahent = kontrahent;   // relacja na rootcie
        });
        SaveDispose();

        var p2 = Get<Prac>(g);
        p2.PowiazanyKontrahent.Should().NotBeNull("ustawiliśmy powiązanie z istniejącym kontrahentem");
        p2.PowiazanyKontrahent.Guid.Should().Be(kontrahent.Guid);
    }

    // ============================== A17 — Archiwum (workery) ==============================

    [Test]
    [Description("A17 (odczyt): manager Pracownik.Archiwum udostępnia tylko-do-odczytu status archiwizacji " +
                 "(Status: enum InformacjeOArchiwum) i flagę Anonimizowany; pracownik aktywny = NieDotyczy.")]
    public void A17_Archiwum_ManagerUdostepniaStatusDoOdczytu()
    {
        // Aktywny pracownik z Demo — nie jest w archiwum. Manager Archiwum to read-only API:
        // Przenieś/Przywróć dostępne są WYŁĄCZNIE przez workery (patrz test poniżej).
        var p = PierwszyPracownik();
        p.Archiwum.Status.Should().Be(InformacjeOArchiwum.NieDotyczy,
            "aktywny pracownik nie jest w archiwum (status = NieDotyczy)");
        p.Archiwum.Anonimizowany.Should().BeFalse("aktywny pracownik nie jest zanonimizowany");
    }

    [Test]
    [Description("A17 (zmiana stanu): przeniesienie/przywrócenie z archiwum jest dostępne WYŁĄCZNIE przez " +
                 "workery Pracownik.PrzenieśDoArchiwumWorker / PrzywróćZArchiwumWorker (CommitUI). Kod w ciele.")]
    [Ignore("Worker PrzenieśDoArchiwum rzuca NullReferenceException w hoście testowym headless " +
            "(Pracownik.ArchiwumManager) — archiwizacja zależy od stanu operatora/kontekstu UI nieobecnego " +
            "w bazie Demo. Test dokumentuje jedyną publiczną drogę zmiany stanu archiwum (workery).")]
    public void A17_Archiwum_PrzeniesienieIPrzywroceniePrzezWorkery()
    {
        var g = Guid.Empty;
        InTransaction(() => NowyPracownik("A17", out g));
        SaveDispose();

        // Przeniesienie do archiwum — worker pojedynczego pracownika (CommitUI: worker „jak z UI").
        InUITransaction(() =>
        {
            var worker = new Prac.PrzenieśDoArchiwumWorker { Pracownik = Get<Prac>(g) };
            worker.PrzenieśDoArchiwum();
        });
        SaveDispose();

        // Odczyt stanu archiwizacji (read-only API managera).
        Get<Prac>(g).Archiwum.Status.Should().Be(InformacjeOArchiwum.WArchiwum);

        // Przywrócenie z archiwum — drugi worker.
        InUITransaction(() =>
        {
            var worker = new Prac.PrzywróćZArchiwumWorker { Pracownik = Get<Prac>(g) };
            worker.PrzywróćZArchiwum();
        });
        SaveDispose();

        Get<Prac>(g).Archiwum.Status.Should().NotBe(InformacjeOArchiwum.WArchiwum);
    }

    // ============================== A18 — Zwolnienie / wyrejestrowanie ==============================

    [Test]
    [Description("A18: zamknięcie zatrudnienia — Etat.Okres.To (ostatni dzień) + subrow Etat.RozwiazanieUmowy " +
                 "(Inicjatywa/PodstawaPrawna = enumy; wartości pobierane z realnych nazw składowych).")]
    public void A18_Zwolnienie_EtatOkresIRozwiazanieUmowy()
    {
        var g = Guid.Empty;
        var dataRozwiazania = new Date(2026, 6, 30);

        // Podstawa prawna: enum o stałych „kodowych" (_400.._550, NieDotyczy) — bierzemy pierwszą realną
        // wartość różną od NieDotyczy, zamiast zgadywać nazwę.
        var podstawa = Enum.GetValues(typeof(KodPodstawyPrawnejZwolnienia))
            .Cast<KodPodstawyPrawnejZwolnienia>()
            .First(v => v != KodPodstawyPrawnejZwolnienia.NieDotyczy);

        InTransaction(() =>
        {
            // Etat ustawiony (Okres+Wydzial+Stanowisko) — inaczej Save rzuca weryfikatorem wymagań etatu.
            var etat = NowyPracownikEtatowy("A18", out g).Last.Etat;
            // Zamknięcie okresu zatrudnienia ostatnim dniem pracy:
            etat.Okres = new FromTo(etat.Okres.From, dataRozwiazania);

            // Tryb rozwiązania (subrow RozwiazanieUmowy):
            etat.RozwiazanieUmowy.Inicjatywa = KodInicjatywyZwolnienia.Pracownik;   // enum
            etat.RozwiazanieUmowy.PodstawaPrawna = podstawa;                          // enum (dynamicznie)

            // Opcjonalnie okres wypowiedzenia:
            etat.OkresWypowiedzenia.DataZlozenia = new Date(2026, 5, 31);
            etat.OkresWypowiedzenia.Miesiace = 1;
        });
        SaveDispose();

        var etat2 = Get<Prac>(g).Last.Etat;
        etat2.Okres.To.Should().Be(dataRozwiazania, "okres zatrudnienia zamknięty ostatnim dniem pracy");
        etat2.RozwiazanieUmowy.Inicjatywa.Should().Be(KodInicjatywyZwolnienia.Pracownik);
        etat2.RozwiazanieUmowy.PodstawaPrawna.Should().Be(podstawa);
        etat2.OkresWypowiedzenia.Miesiace.Should().Be(1);
    }

    [Test]
    [Description("A18 (ZWUA): wyrejestrowanie z ZUS przez WyrejestrujPracownikaWorker wymaga Params(Context) " +
                 "oraz środowiska deklaracji ZUS — poza prostym kontraktem ustawiania pól etatu.")]
    [Ignore("Wyrejestrowanie ZUS (ZWUA) wymaga WyrejestrujPracownikaParams(Context) i kontekstu deklaracji/" +
            "KEDU; samo ustawienie Etat.Okres/RozwiazanieUmowy (test A18) nie tworzy dokumentu ZWUA.")]
    public void A18_WyrejestrowanieZUS_WymagaContextIKedu()
    {
    }

    // ============================== A19 — Przerejestrowanie (zmiana Tyub4) ==============================

    [Test]
    [Description("A19: przerejestrowanie = nowy zapis historii od daty (A14: Update + AddRow) ze zmianą " +
                 "Etat.Ubezpieczenia.Tyub4 (słownik TytulyUbezpiecz4, klucz int); deklaracje ZUS — osobny worker UI.")]
    public void A19_Przerejestrowanie_ZmianaTyub4OdDaty()
    {
        // Tyub4 to słownik o kluczu int — bierzemy dwie różne realne wartości z bazy Demo (nie hardkodujemy).
        var tytuly = Kadry.TytulyUbezpiecz4.Cast<TytulUbezpieczenia4>().Take(2).ToList();
        if (tytuly.Count < 1)
        {
            Assert.Ignore("Brak słownika tytułów ubezpieczenia (TytulyUbezpiecz4) w bazie Demo.");
            return;
        }
        var nowyTyub = tytuly.Last();

        var g = Guid.Empty;
        var odDnia = new Date(2026, 7, 1);

        InTransaction(() =>
        {
            var pracownik = NowyPracownik("A19", out g);

            // Nowy zapis historii „od daty" (A14): Update klonuje + skraca poprzedni, AddRow dopina klon.
            var nowy = pracownik.Historia.Update(odDnia);
            pracownik.Module.PracHistorie.AddRow(nowy);

            // Zmiana kodu tytułu ubezpieczenia (przerejestrowanie ubezpieczeniowe) na nowym zapisie:
            nowy.Etat.Ubezpieczenia.Tyub4 = nowyTyub;
        });
        SaveDispose();

        var pracownik2 = Get<Prac>(g);
        var zapisy = pracownik2.Historia.Cast<PracHistoria>().OrderBy(h => h.Aktualnosc.From).ToList();
        zapisy.Should().HaveCount(2, "Update utworzył drugi zapis historii (przerejestrowanie od daty)");
        zapisy[0].Aktualnosc.To.Should().Be(odDnia.AddDays(-1), "stary zapis skrócony do dnia poprzedzającego");
        zapisy[1].Aktualnosc.From.Should().Be(odDnia, "nowy zapis obowiązuje od dnia przerejestrowania");
        zapisy[1].Etat.Ubezpieczenia.Tyub4.Should().NotBeNull("nowy tytuł ubezpieczenia ustawiony od daty");
        zapisy[1].Etat.Ubezpieczenia.Tyub4.Guid.Should().Be(nowyTyub.Guid);
    }
}
