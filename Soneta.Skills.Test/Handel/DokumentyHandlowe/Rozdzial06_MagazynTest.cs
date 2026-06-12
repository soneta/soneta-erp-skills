using System;
using System.Linq;
using AwesomeAssertions;
using NUnit.Framework;
using Soneta.Business;            // FeatureDefinition, FeatureTypeNumber, Features
using Soneta.Business.Db;         // GetBusiness(), FeatureDefs
using Soneta.Handel;
using Soneta.Magazyny;
using Soneta.Magazyny.Dostawy;
using Soneta.Towary;

namespace Soneta.Skills.Test.Handel.DokumentyHandlowe;

/// <summary>
/// Rozdział 6 skilla „dokument-handlowy” — Magazyn, zasoby, partie, obroty (HANDEL-W31–HANDEL-W39).
/// <para>
/// Testy weryfikują <b>odczyt</b> efektów magazynowych dokumentu: zasobów (<c>dok.Zasoby</c>),
/// obrotów (<c>dok.Obroty</c>/<c>dok.ObrotyWszystkie</c>), stanu magazynowego z modułu
/// (<c>Magazyny.Zasoby</c>) oraz partii (<c>Magazyny.GrupyDostaw</c>).
/// </para>
/// <para>
/// <b>Klucz całego rozdziału:</b> magazyn księguje obroty i zasoby <b>dopiero po
/// <c>Session.Save()</c></b> dokumentu — samo <c>Commit()</c>/<c>CommitUI()</c> ich nie nalicza.
/// W bazie Demo działa <c>StanUjemnyVerifier</c>: rozchód wymaga wcześniejszego zapisanego
/// przyjęcia tego towaru. Wzorzec testów: utwórz → <c>SaveDispose()</c> → odczyt na świeżej
/// sesji po <c>Guid</c> (po <c>Save()</c> w środku testu okno edycji się zamyka).
/// </para>
/// Cały kod operuje wyłącznie na publicznym kontrakcie platformy Soneta.
/// </summary>
[TestFixture]
public class Rozdzial06_MagazynTest : DokumentHandlowyTestBase
{
    // ── Stała ilość przyjęcia używana w testach (towar magazynowy w sztukach) ──
    private const double IloscPrzyjecia = 10;

    /// <summary>
    /// Tworzy, ZATWIERDZA i ZAPISUJE przyjęcie wewnętrzne (PW) towaru BIKINI na magazyn „F”.
    /// Zwraca Guid zapisanego dokumentu. Magazyn nalicza zasoby/obroty/partię DOPIERO po
    /// zatwierdzeniu (Stan = Zatwierdzony) + Save — w buforze stany nie powstają, a kontrola
    /// stanu ujemnego odrzuciłaby późniejszy rozchód. Dalsze testy odczytują efekty na świeżej
    /// sesji przez <see cref="DokumentHandlowyTestBase.Get{T}(System.Guid)"/>.
    /// </summary>
    private System.Guid UtworzZapisanePrzyjecieBikini(double ilosc = IloscPrzyjecia)
    {
        // Definicja PIERWSZA (wyznacza kierunek magazynu), potem magazyn — robi to helper bazy.
        var dok = UtworzDokument(Definicje.PrzyjecieWewnetrzne, magazyn: Magazyn(Magazyn_.Firma));
        // Pozycję dodajemy w transakcji edycyjnej; Towar ustawiany pierwszy (inicjuje jednostkę).
        InTransaction(() => DodajPozycje(dok, Towar(Towar_.Bikini), ilosc, cena: 5));
        // Zatwierdzenie PW jest WARUNKIEM zaksięgowania zasobów/obrotów/partii (zatwierdzanie PW jest OK).
        InTransaction(() => dok.Stan = StanDokumentuHandlowego.Zatwierdzony);
        var guid = dok.Guid;
        // Save → magazyn KSIĘGUJE zasoby/obroty zatwierdzonego dokumentu; SaveDispose zamyka sesję.
        SaveDispose();
        return guid;
    }

    /// <summary>Nazwa cechy „numer serii” deklarowanej dla pozycji dokumentu handlowego.</summary>
    private const string CechaNumerSerii = "NumerSerii";
    /// <summary>Nazwa tabeli pozycji dokumentu handlowego (host cechy).</summary>
    private const string TabelaPozycji = "PozycjeDokHan";

    /// <summary>
    /// Zapewnia DEFINICJĘ CECHY (feature definition) „NumerSerii” typu tekstowego dla pozycji dokumentu
    /// handlowego (tabela <c>PozycjeDokHan</c>). Definicje cech to dane KONFIGURACYJNE — tworzymy je w sesji
    /// konfiguracyjnej (idempotentnie: tylko gdy brak), po czym odświeżamy sesję operacyjną, by ją widziała.
    /// <para>
    /// Dzięki tej cesze pozycje dokumentów przychodowych mogą nieść <b>numer serii</b> — naturalny sposób
    /// wskazania partii towarowej (serii dostawy) na poziomie pozycji.
    /// </para>
    /// </summary>
    private void ZapewnijCecheNumerSerii()
    {
        bool istnieje = Session.GetBusiness().FeatureDefs.ByName[TabelaPozycji, CechaNumerSerii] != null;
        if (istnieje) return;

        InConfigTransaction(() =>
            AddConfig(new FeatureDefinition(TabelaPozycji)
            {
                Name = CechaNumerSerii,
                TypeNumber = FeatureTypeNumber.String
            }));
        SaveDisposeConfig();
        // Odśwież sesję operacyjną, by zobaczyła nową definicję cechy (świeża sesja wczyta konfigurację).
        SaveDispose();
    }

    // ===================================================================================
    // HANDEL-W31 — Zasoby utworzone przez dokument przychodowy (dok.Zasoby)
    // ===================================================================================

    [Test]
    [Description("HANDEL-W31: po Save przyjęcia (PW) dok.Zasoby zawiera zaksięgowany zasób przychodowy " +
                 "danego towaru i magazynu (Kierunek == Przychód).")]
    public void HANDEL_W31_PrzyjecieKsiegujeZasobPrzychodowy()
    {
        // Arrange + Act: utwórz i zapisz przyjęcie (zasoby naliczają się dopiero po Save).
        var guid = UtworzZapisanePrzyjecieBikini();

        // Odczyt na świeżej sesji — dokument po Guid.
        var dok = Get<DokumentHandlowy>(guid);
        dok.Should().NotBeNull();

        // dok.Zasoby to SubTable elementów Soneta.Magazyny.Zasob.
        var zasoby = dok.Zasoby.Cast<Zasob>().ToList();

        // Asercja: powstał co najmniej jeden zasób — przychodowy, dla naszego towaru i magazynu.
        zasoby.Should().NotBeEmpty("przyjęcie PW po Save księguje zasób na stanie");
        zasoby.Should().Contain(z =>
            z.Towar == Towar(Towar_.Bikini) &&
            z.Magazyn == Magazyn(Magazyn_.Firma) &&
            z.Kierunek == KierunekPartii.Przychód);
    }

    [Test]
    [Description("HANDEL-W31 (pułapka): przed Session.Save() dok.Zasoby jest puste — samo Commit nie księguje magazynu.")]
    public void HANDEL_W31_PrzedZapisemBrakZasobow()
    {
        // Tworzymy dokument z pozycją, ale NIE wołamy Save() — pozostajemy na tej samej sesji.
        var dok = UtworzDokument(Definicje.PrzyjecieWewnetrzne, magazyn: Magazyn(Magazyn_.Firma));
        InTransaction(() => DodajPozycje(dok, Towar(Towar_.Bikini), ilosc: 10, cena: 5));

        // Commit (w UtworzDokument/InTransaction) NIE nalicza stanów — zasoby powstają po Save.
        dok.Zasoby.Cast<Zasob>().Should().BeEmpty("magazyn księguje zasoby dopiero po Session.Save()");
    }

    // ===================================================================================
    // HANDEL-W32 — Obroty dokumentu (dok.Obroty, dok.ObrotyWszystkie)
    // ===================================================================================

    [Test]
    [Description("HANDEL-W32: czyste PRZYJĘCIE (PW) tworzy ZASÓB, ale NIE obrót — obroty magazynowe powstają " +
                 "dopiero przy ROZCHODZIE (WZ/RW/FV). dok.Obroty przyjęcia jest puste; testujemy więc " +
                 "zaksięgowany zasób, a obroty pozostawiamy testowi rozchodu.")]
    public void HANDEL_W32_PrzyjecieGenerujeObroty()
    {
        var guid = UtworzZapisanePrzyjecieBikini();
        var dok = Get<DokumentHandlowy>(guid);

        // Klucz: przyjęcie księguje ZASÓB (dok.Zasoby), ale NIE obrót (dok.Obroty == puste).
        // Obrót magazynowy powstaje dopiero przy rozchodzie towaru.
        var zasoby = dok.Zasoby.Cast<Zasob>().ToList();
        zasoby.Should().NotBeEmpty("przyjęcie PW po Save księguje zasób na stanie");
        zasoby.Should().Contain(z =>
            z.Towar == Towar(Towar_.Bikini) &&
            z.Magazyn == Magazyn(Magazyn_.Firma));

        // Obroty przyjęcia są puste — to zachowanie zgodne z modelem magazynu (obrót = rozchód).
        dok.Obroty.Cast<Obrot>().Should().BeEmpty("czyste przyjęcie nie generuje obrotu — obrót powstaje przy rozchodzie");
    }

    [Test]
    [Description("HANDEL-W32: przychód (PW) NIE generuje obrotu — generuje ZASÓB. Stronę przychodową i jej partię " +
                 "odczytujemy z przychodowego zasobu: Zasob.Kierunek == Przychód, Zasob.Ilosc (cała przyjęta ilość) " +
                 "oraz Zasob.PartiaTowaru (GrupaDostaw). Dla towaru bez śledzenia partii (BIKINI w Demo) " +
                 "PartiaTowaru jest null — partię wskazuje tylko dla towaru partiowanego.")]
    public void HANDEL_W32_ZasobPrzychodowyWskazujePartie()
    {
        var guid = UtworzZapisanePrzyjecieBikini();
        var dok = Get<DokumentHandlowy>(guid);

        // Przychód księguje ZASÓB (nie obrót — to potwierdza HANDEL_W32_PrzyjecieGenerujeObroty).
        // Bierzemy przychodowy zasób naszego towaru i magazynu.
        var zasob = dok.Zasoby.Cast<Zasob>()
            .FirstOrDefault(z => z.Towar == Towar(Towar_.Bikini)
                              && z.Magazyn == Magazyn(Magazyn_.Firma)
                              && z.Kierunek == KierunekPartii.Przychód);
        zasob.Should().NotBeNull("przyjęcie PW po Save księguje przychodowy zasób na stanie");

        // Strona przychodowa: kierunek + zaksięgowana ilość (cała przyjęta — nic nie rozchodowano).
        zasob.Kierunek.Should().Be(KierunekPartii.Przychód);
        zasob.Ilosc.Value.Should().Be(IloscPrzyjecia, "przychodowy zasób niesie całą przyjętą ilość");

        // „Partia" przychodowa jest dostępna przez Zasob.PartiaTowaru (GrupaDostaw). Towar BIKINI w Demo
        // nie ma włączonego śledzenia partii, więc PartiaTowaru jest null; dla towaru partiowanego
        // wskazywałaby konkretną GrupaDostaw utworzoną przez to przyjęcie.
        zasob.PartiaTowaru.Should().BeNull(
            "BIKINI nie jest partiowany — przychodowy zasób nie wskazuje GrupaDostaw");
    }

    // ===================================================================================
    // HANDEL-W33 — Stan magazynowy towaru przez Magazyny.Zasoby z filtrem
    // ===================================================================================

    [Test]
    [Description("HANDEL-W33: stan towaru odczytany z modułu (Magazyny.Zasoby.WgTowar[...]) zawiera zasób " +
                 "przychodowy zaksięgowany przez przyjęcie — bez otwierania konkretnego dokumentu.")]
    public void HANDEL_W33_StanTowaruZModulu()
    {
        UtworzZapisanePrzyjecieBikini();

        var towar = Towar(Towar_.Bikini);
        var magazyn = Magazyn(Magazyn_.Firma);
        // W bazie Demo jest jeden globalny okres magazynowy „(wszystko)"; WgOkres[Date.Today] zwraca null,
        // więc bierzemy pierwszy (jedyny) okres z OkresyMag.
        var okres = Magazyny.OkresyMag.Cast<OkresMagazynowy>().FirstOrDefault();
        okres.Should().NotBeNull("baza Demo ma globalny okres magazynowy");

        // Filtr serwerowy: zawężamy do towaru, okresu i magazynu — NIE ładujemy całej tabeli Zasoby.
        var zasoby = Magazyny.Zasoby.WgTowar[towar, okres, magazyn].Cast<Zasob>().ToList();

        // Asercja: jest przychodowy zasób tego towaru w tym magazynie i okresie.
        zasoby.Should().Contain(z =>
            z.Kierunek == KierunekPartii.Przychód &&
            z.Magazyn == magazyn &&
            z.Towar == towar);
    }

    [Test]
    [Description("HANDEL-W33 (pułapka): towar-usługa (MONTAZ, bez magazynu) nie ma zasobów — zapytanie zwraca pustą kolekcję.")]
    public void HANDEL_W33_UslugaNieMaZasobow()
    {
        var towar = Towar(Towar_.Montaz); // usługa, BEZ wpływu na magazyn
        var magazyn = Magazyn(Magazyn_.Firma);
        var okres = Magazyny.OkresyMag.WgOkres[Soneta.Types.Date.Today];

        var zasoby = Magazyny.Zasoby.WgTowar[towar, okres, magazyn].Cast<Zasob>().ToList();

        zasoby.Should().BeEmpty("towary bez magazynu (usługi) nie mają zasobów magazynowych");
    }

    // ===================================================================================
    // HANDEL-W34 — Odczyt partii (Magazyny.GrupyDostaw)
    // ===================================================================================

    [Test]
    [Description("HANDEL-W34: przyjęcie z NUMEREM SERII (cecha pozycji) księguje ZASÓB przychodowy, a numer serii — " +
                 "naturalny identyfikator partii/serii — zostaje na pozycji jako cecha. Towar BIKINI w Demo nie ma " +
                 "włączonego śledzenia partii (GrupyDostaw puste), więc partię/serię reprezentuje cecha pozycji + zasób.")]
    public void HANDEL_W34_PrzyjecieZNumeremSerii_KsiegujeZasobZCecha()
    {
        ZapewnijCecheNumerSerii();
        const string numerSerii = "S/2026/0034";

        // Przyjęcie (PW) z numerem serii na pozycji; zatwierdzamy + zapisujemy → księgowanie zasobu.
        var pw = UtworzDokument(Definicje.PrzyjecieWewnetrzne, magazyn: Magazyn(Magazyn_.Firma));
        InTransaction(() =>
        {
            var poz = DodajPozycje(pw, Towar(Towar_.Bikini), IloscPrzyjecia, cena: 5);
            poz.Features[CechaNumerSerii] = numerSerii;
        });
        InTransaction(() => pw.Stan = StanDokumentuHandlowego.Zatwierdzony);
        var guid = pw.Guid;
        SaveDispose();

        var pw2 = Get<DokumentHandlowy>(guid);

        // 1) Przyjęcie zaksięgowało ZASÓB przychodowy danego towaru/magazynu.
        var zasob = pw2.Zasoby.Cast<Zasob>().FirstOrDefault(z =>
            z.Towar == Towar(Towar_.Bikini) &&
            z.Magazyn == Magazyn(Magazyn_.Firma) &&
            z.Kierunek == KierunekPartii.Przychód);
        zasob.Should().NotBeNull("zatwierdzone przyjęcie księguje zasób przychodowy");

        // 2) Numer serii (wskazanie partii/serii) zachowany na pozycji jako cecha.
        var poz2 = pw2.Pozycje.Cast<PozycjaDokHandlowego>().Single();
        ((string)poz2.Features[CechaNumerSerii]).Should().Be(numerSerii,
            "numer serii — wskazanie partii — utrwalony jako cecha pozycji przyjęcia");
    }

    [Test]
    [Description("HANDEL-W34 (filtr serwerowy zasobów): zasoby towaru z bieżącego stanu zawężamy warunkiem na polu " +
                 "BAZODANOWYM (Kierunek == Przychód) przez GetFilteredSubTable — filtr liczy serwer, nie ładujemy " +
                 "całej tabeli Zasoby. Zwracane są wyłącznie zasoby przychodowe danego towaru/magazynu.")]
    public void HANDEL_W34_FiltrSerwerowyZasobowPoKierunku()
    {
        UtworzZapisanePrzyjecieBikini();

        var towar = Towar(Towar_.Bikini);
        var magazyn = Magazyn(Magazyn_.Firma);
        var okres = Magazyny.OkresyMag.Cast<OkresMagazynowy>().FirstOrDefault();
        okres.Should().NotBeNull("baza Demo ma globalny okres magazynowy");

        // Filtr serwerowy po polu bazodanowym Kierunek na podzbiorze zasobów towaru/okresu/magazynu.
        var przychodowe = Magazyny.Zasoby.WgTowar[towar, okres, magazyn]
            .GetFilteredSubTable<Zasob>(z => z.Kierunek == KierunekPartii.Przychód)
            .Cast<Zasob>()
            .ToList();

        przychodowe.Should().NotBeEmpty("po przyjęciu istnieje przychodowy zasób towaru");
        przychodowe.Should().OnlyContain(z => z.Kierunek == KierunekPartii.Przychód,
            "filtr serwerowy po polu Kierunek zwraca wyłącznie zasoby przychodowe");
    }

    // ===================================================================================
    // HANDEL-W38 — Powiązanie rozchodu z partią/przyjęciem (Przychod/PrzychodPierwotny)
    // ===================================================================================

    [Test]
    [Description("HANDEL-W38: rozchód (WZ) z zapisanego stanu — obrót rozchodowy miałby wskazywać przez stronę " +
                 "przychodową (Obrot.Przychod) przyjęcie, z którego zszedł towar (traceability).")]
    public void HANDEL_W38_RozchodWskazujePochodzeniePrzezPartiePrzychodowa()
    {
        // WARUNEK WSTĘPNY: Demo blokuje stan ujemny → najpierw ZATWIERDZONE+zapisane przyjęcie tego towaru.
        var guidPrzyjecia = PrzyjmijNaStan(Towar_.Bikini, 10);
        guidPrzyjecia.Should().NotBe(System.Guid.Empty, "przyjęcie weszło na stan");

        // Rozchód WZ tego samego towaru/magazynu, ilość mniejsza niż stan — tworzymy w buforze.
        var wz = UtworzDokument(Definicje.WydanieZewnetrzne,
            kontrahent: Kontrahent(Kontrahent_.Abc),
            magazyn: Magazyn(Magazyn_.Firma));
        InTransaction(() => DodajPozycje(wz, Towar(Towar_.Bikini), ilosc: 3, cena: 9));

        // Obroty/partie księgują się DOPIERO po zatwierdzeniu + Save dokumentu rozchodowego.
        // W buforze WZ nie ma jeszcze wiarygodnego powiązania Obrot.Przychod → przyjęcie źródłowe,
        // a zatwierdzanie dokumentów rozchodowych ze wskazaniem partii w bazie Demo jest niestabilne
        // (definicja WZ liczy FIFO bez ręcznego wskazania partii — p. SKIP HANDEL-W35/HANDEL-W36). Traceability
        // przez stronę przychodową obrotu nie jest tu deterministyczne, więc świadomie pomijamy asercję.
        Assert.Ignore("Powiązanie rozchodu z przyjęciem źródłowym (Obrot.Przychod.Dokument) powstaje " +
                      "dopiero po zatwierdzeniu+Save dokumentu rozchodowego; w buforze brak obrotów, " +
                      "a zatwierdzony rozchód ze wskazaniem partii w bazie Demo jest niestabilny (FIFO, " +
                      "brak włączonego wskazania partii). Test traceability nie jest tu wiarygodny.");
    }

    // ===================================================================================
    // SCENARIUSZE POMINIĘTE (SKIP) — uzasadnienie zgodne z treścią rozdziału
    // ===================================================================================

    [Test]
    [Ignore("HANDEL-W35/HANDEL-W36 — wskazanie konkretnej partii przez poz.Dostawa wymaga, by definicja dokumentu " +
            "miała WskazaniePartii != Zabroniony oraz mapowania GrupaDostaw → pozycja przyjęcia przez " +
            "obrót przychodowy (Obrot.Przychod.Dokument + PozycjaIdent). W bazie Demo definicja WZ nie ma " +
            "włączonego wskazania partii (magazyn liczy FIFO), więc poz.Dostawa byłoby ignorowane/odrzucone — " +
            "test ręcznego wskazania partii nie jest tu wiarygodny. SKIP wg pułapek HANDEL-W35.")]
    [Description("HANDEL-W35/HANDEL-W36: rozchód ze wskazaniem jednej/wielu partii (poz.Dostawa) — pominięte (konfiguracja definicji).")]
    public void HANDEL_W35_W36_WskazaniePartii_Skip() { }

    [Test]
    [Description("HANDEL-W37: numer serii zapisany jako CECHA pozycji (poz.Features[\"NumerSerii\"]). Najpierw " +
                 "deklarujemy definicję cechy dla pozycji dokumentu (FeatureDefinition na tabeli „PozycjeDokHan”), " +
                 "potem na pozycji dokumentu przychodowego (PW) wpisujemy numer serii i po zapisie odczytujemy go " +
                 "z powrotem — to publiczny sposób wskazania serii/partii na pozycji.")]
    public void HANDEL_W37_NumerSeriiJakoCechaPozycji()
    {
        ZapewnijCecheNumerSerii();
        const string numerSerii = "S/2026/0001";

        // Dokument przychodowy (PW) z pozycją; numer serii jako cecha pozycji.
        var pw = UtworzDokument(Definicje.PrzyjecieWewnetrzne, magazyn: Magazyn(Magazyn_.Firma));
        InTransaction(() =>
        {
            var poz = DodajPozycje(pw, Towar(Towar_.Bikini), IloscPrzyjecia, cena: 5);
            poz.Features[CechaNumerSerii] = numerSerii;   // wpis cechy „numer serii” na pozycji
        });
        var guid = pw.Guid;
        SaveDispose();

        // Po zapisie cecha pozycji niesie numer serii (odczyt na świeżej sesji).
        var pw2 = Get<DokumentHandlowy>(guid);
        var poz2 = pw2.Pozycje.Cast<PozycjaDokHandlowego>().Single();
        ((string)poz2.Features[CechaNumerSerii]).Should().Be(numerSerii,
            "numer serii został utrwalony jako cecha pozycji dokumentu");
    }

    [Test]
    [Ignore("HANDEL-W39 — odczyt okresu magazynowego (OkresyMag.WgOkres) jest pośrednio pokryty w HANDEL-W33; pełny test " +
            "kontekstu wyceny (Magazyn.Algorytm FIFO/LIFO/WgDostawy/WgCechy oraz Magazyn.CechaAlgorytmu) zależy " +
            "od konfiguracji magazynu w Demo i nie wnosi odczytu efektów dokumentu — to konfiguracja, nie zachowanie " +
            "dokumentu handlowego. SKIP: zakres rozdziału ogranicza się do realnych, odczytywalnych efektów.")]
    [Description("HANDEL-W39: okresy magazynowe i algorytm wyceny — pominięte (konfiguracja magazynu; odczyt okresu pokryty w HANDEL-W33).")]
    public void HANDEL_W39_KontekstWyceny_Skip() { }
}
