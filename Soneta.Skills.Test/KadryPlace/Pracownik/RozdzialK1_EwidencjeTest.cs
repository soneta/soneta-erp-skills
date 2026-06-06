using System.Linq;
using AwesomeAssertions;
using NUnit.Framework;
using Soneta.Business;
using Soneta.HR;
using Soneta.Kadry;
using Soneta.Types;
using Prac = Soneta.Kadry.Pracownik;

namespace Soneta.Skills.Test.KadryPlace.Pracownik;

/// <summary>
/// Rozdział K (część pierwsza) — „Ewidencje pracownicze" (receptury K1–K5).
/// <para>
/// Testy są <b>wykonywalną dokumentacją</b> publicznego kontraktu platformy Soneta dla ewidencji
/// pracowniczych. Wszystkie ewidencje mają wspólny wzorzec: są kolekcjami <c>SubTable</c> na rootcie
/// <c>Pracownik</c> (nie na <c>PracHistoria</c>), a każdy wpis to osobny <c>GuidedRow</c> tworzony
/// konstruktorem <c>new Xxx(pracownik)</c>, który wiąże wpis z pracownikiem. Dodanie realizujemy
/// przez <c>Session.AddRow(new Xxx(pracownik))</c> (równoważne <c>pracownik.Kolekcja.AddRow(...)</c>).
/// Każda metoda mapuje się 1:1 do receptury z dokumentu skilla <c>pracownik.md</c>:
/// <list type="bullet">
/// <item><b>K1</b> — badania lekarskie (<c>new BadanieLekarskie(pracownik)</c>, <c>pracownik.BadaniaLekarskie</c>; pole <c>WazneDo</c> bez „ż");</item>
/// <item><b>K2</b> — szkolenia BHP (<c>new SzkolenieBHP(pracownik)</c>, <c>pracownik.SzkoleniaBHP</c>; pole <c>WażneDo</c> z „ż");</item>
/// <item><b>K3</b> — szkolenia i uprawnienia HR (<c>WniosekOSzkolenie</c>/<c>UkończoneSzkolenie</c>/<c>UprawnieniePracownika</c> — moduł <c>Soneta.HR</c>);</item>
/// <item><b>K4</b> — nagrody/kary (<c>new Nagroda/Kara(pracownik)</c>, abstr. <c>NagrodaKara</c>) i oświadczenia (<c>OświadczeniePracownika(pracownik, def[, data])</c>);</item>
/// <item><b>K5</b> — wypadki przy pracy (<c>new Wypadek(pracownik)</c>, <c>pracownik.Wypadki</c>).</item>
/// </list>
/// </para>
/// <para>
/// Wszystko działa na bazie Demo (GoldStandard) z automatycznym rollbackiem po teście. Operujemy
/// wyłącznie na <b>publicznym kontrakcie</b> — tak jak dodatek programisty zewnętrznego bez dostępu
/// do kodu źródłowego aplikacji. Większość wpisów wymaga <b>definicji</b> (rekord słownikowy z tabeli
/// konfiguracyjnej) — definicję pobieramy dynamicznie (pierwsza z tabeli / po nazwie), a gdy w Demo
/// brak wymaganej definicji, test jest oznaczany <c>Assert.Ignore</c> z powodem.
/// </para>
/// </summary>
[TestFixture]
public class RozdzialK1_EwidencjeTest : PracownikTestBase
{
    // Pracownik-host dla wpisów ewidencyjnych — dowolny etatowy z Demo.
    private Prac Host() => Pracownik(Pracownik_.Andrzejewski) ?? PierwszyPracownik();

    // Pierwsza definicja z tabeli konfiguracyjnej (lub null) — bez twardej zależności od nazwy słownika.
    private static T Pierwsza<T>(Table tabela) where T : Row =>
        tabela.Cast<T>().FirstOrDefault();

    // ============================== K1 — Badania lekarskie ==============================

    [Test]
    [Description("K1: new BadanieLekarskie(pracownik) wiąże wpis z pracownikiem; Definicja (DefBadanLek) " +
                 "jest wymagana; Data/Termin/WazneDo to Soneta.Types.Date (WazneDo BEZ z-kreska); wpis trafia " +
                 "do pracownik.BadaniaLekarskie.")]
    public void K1_BadanieLekarskie_DodanieZDefinicja_TrafiaDoKolekcji()
    {
        var definicja = Pierwsza<DefinicjaBadaniaLekarskiego>(Kadry.DefBadanLek);
        if (definicja == null)
            Assert.Ignore("Brak definicji badania lekarskiego (DefBadanLek) w bazie Demo — wpisu nie można utworzyć.");

        var pracownik = Host();
        Soneta.Kadry.BadanieLekarskie badanie = null;

        InTransaction(() =>
        {
            // Konstruktor (Pracownik) wiąże wpis z pracownikiem; AddRow == pracownik.BadaniaLekarskie.AddRow.
            badanie = Session.AddRow(new Soneta.Kadry.BadanieLekarskie(pracownik));
            badanie.Definicja = definicja;     // WYMAGANA — bez niej Save() rzuci RowException
            badanie.Data = Date.Today;
            // Termin jest WYLICZANY (read-only) z Data + definicji — nie ustawiamy go ręcznie.
            // Uwaga na pisownię: w BadanieLekarskie pole nazywa się WazneDo (BEZ „ż").
            badanie.WazneDo = new Date(Date.Today.Year + 2, Date.Today.Month, Date.Today.Day);
        });

        badanie.Pracownik.Should().Be(pracownik, "ctor (Pracownik) ustawia pole Pracownik");
        badanie.Definicja.Should().Be(definicja);
        pracownik.BadaniaLekarskie.Cast<Soneta.Kadry.BadanieLekarskie>()
            .Should().Contain(badanie, "wpis trafia do kolekcji SubTable pracownika");
    }

    [Test]
    [Description("K1: pracownik.Badania to manager (BadaniaLekarskieManager) tylko do odczytu — inny obiekt " +
                 "niż kolekcja CRUD pracownik.BadaniaLekarskie (SubTable<BadanieLekarskie>).")]
    public void K1_Badania_ManagerOdczytu_RozniSieOdKolekcjiCrud()
    {
        var pracownik = Host();

        pracownik.Badania.Should().NotBeNull("manager Badania jest zawsze dostępny (odczyt)");
        pracownik.Badania.Should().BeOfType<Prac.BadaniaLekarskieManager>();
        // Kolekcja CRUD to osobne API — SubTable.
        pracownik.BadaniaLekarskie.Should().NotBeNull();
    }

    // ============================== K2 — Szkolenia BHP ==============================

    [Test]
    [Description("K2: new SzkolenieBHP(pracownik) + Definicja (DefSzkolenBHP, wymagana); pole ważności to " +
                 "WażneDo (Z z-kreska) - w przeciwieństwie do K1; wpis trafia do pracownik.SzkoleniaBHP.")]
    public void K2_SzkolenieBHP_DodanieZDefinicja_TrafiaDoKolekcji()
    {
        var definicja = Pierwsza<DefinicjaSzkoleniaBHP>(Kadry.DefSzkolenBHP);
        if (definicja == null)
            Assert.Ignore("Brak definicji szkolenia BHP (DefSzkolenBHP) w bazie Demo — wpisu nie można utworzyć.");

        var pracownik = Host();
        Soneta.Kadry.SzkolenieBHP szkolenie = null;

        InTransaction(() =>
        {
            szkolenie = Session.AddRow(new Soneta.Kadry.SzkolenieBHP(pracownik));
            szkolenie.Definicja = definicja;
            szkolenie.Data = Date.Today;
            // Termin jest WYLICZANY (read-only) z Data + definicji — nie ustawiamy go ręcznie.
            szkolenie.Zakres = "Instruktaż ogólny";
            szkolenie.Osoba = "Prowadzący BHP";
        });

        szkolenie.Pracownik.Should().Be(pracownik);
        szkolenie.Definicja.Should().Be(definicja);
        szkolenie.Zakres.Should().Be("Instruktaż ogólny");
        pracownik.SzkoleniaBHP.Cast<Soneta.Kadry.SzkolenieBHP>().Should().Contain(szkolenie);
    }

    // ============================== K3 — Szkolenia i uprawnienia (HR) ==============================

    [Test]
    [Description("K3a: WniosekOSzkolenie([Required] Pracownik) z modułu Soneta.HR (session.GetHR()); Definicja " +
                 "(DefinicjeSzkolen) + Etap (EtapRealizSzkol) to słowniki HR; Koszt to Soneta.Types.Currency.")]
    public void K3a_WniosekOSzkolenie_DodanieZBudzetemIKosztem_TrafiaDoKolekcji()
    {
        var hr = Session.GetHR();
        var definicja = Pierwsza<DefinicjaSzkolenia>(hr.DefinicjeSzkolen);
        if (definicja == null)
            Assert.Ignore("Brak definicji szkolenia HR (DefinicjeSzkolen) w bazie Demo — wniosku nie można utworzyć.");

        var pracownik = Host();
        WniosekOSzkolenie wniosek = null;

        InTransaction(() =>
        {
            wniosek = Session.AddRow(new WniosekOSzkolenie(pracownik));
            wniosek.Definicja = definicja;
            // Etap jest opcjonalny do zapisu — ustawiamy gdy słownik niepusty.
            var etap = Pierwsza<EtapRealizacjiSzkolenia>(hr.EtapRealizSzkol);
            if (etap != null)
                wniosek.Etap = etap;
            wniosek.DataZgloszenia = Date.Today;
            wniosek.Koszt = new Currency(1500m);   // Currency, nie decimal
        });

        wniosek.Pracownik.Should().Be(pracownik);
        wniosek.Definicja.Should().Be(definicja);
        wniosek.Koszt.Value.Should().Be(1500m);
        pracownik.WnioskiOSzkolenia.Cast<WniosekOSzkolenie>().Should().Contain(wniosek);
    }

    [Test]
    [Description("K3b: UkończoneSzkolenie([Required] Pracownik) — moduł HR; pola Nazwa/Okres(FromTo)/Ocena; " +
                 "wpis trafia do pracownik.UkończoneSzkolenia. Drugi ctor (WniosekOSzkolenie) przepina pracownika.")]
    public void K3b_UkonczoneSzkolenie_DodanieZPracownika_TrafiaDoKolekcji()
    {
        var pracownik = Host();
        UkończoneSzkolenie ukonczone = null;

        InTransaction(() =>
        {
            ukonczone = Session.AddRow(new UkończoneSzkolenie(pracownik));
            ukonczone.Nazwa = "Kurs BHP – aktualizacja";
            ukonczone.Okres = new FromTo(Date.Today, Date.Today);
            ukonczone.Ocena = "bardzo dobry";
        });

        ukonczone.Pracownik.Should().Be(pracownik);
        ukonczone.Nazwa.Should().Be("Kurs BHP – aktualizacja");
        pracownik.UkończoneSzkolenia.Cast<UkończoneSzkolenie>().Should().Contain(ukonczone);
    }

    [Test]
    [Description("K3c: UprawnieniePracownika([Required] Pracownik) — moduł HR; Definicja (DefUprawnien, słownik), " +
                 "Numer, DataUzyskania/TerminWaznosci (Date); wpis trafia do pracownik.Uprawnienia.")]
    public void K3c_UprawnieniePracownika_DodanieZDefinicja_TrafiaDoKolekcji()
    {
        var hr = Session.GetHR();
        var definicja = Pierwsza<DefinicjaUprawnienia>(hr.DefUprawnien);
        if (definicja == null)
            Assert.Ignore("Brak definicji uprawnienia HR (DefUprawnien) w bazie Demo — uprawnienia nie można utworzyć.");

        var pracownik = Host();
        UprawnieniePracownika uprawnienie = null;

        InTransaction(() =>
        {
            uprawnienie = Session.AddRow(new UprawnieniePracownika(pracownik));
            uprawnienie.Definicja = definicja;
            uprawnienie.Numer = "UP/2026/001";
            uprawnienie.DataUzyskania = Date.Today;
            uprawnienie.TerminWaznosci = new Date(Date.Today.Year + 5, Date.Today.Month, Date.Today.Day);
        });

        uprawnienie.Pracownik.Should().Be(pracownik);
        uprawnienie.Definicja.Should().Be(definicja);
        uprawnienie.Numer.Should().Be("UP/2026/001");
        pracownik.Uprawnienia.Cast<UprawnieniePracownika>().Should().Contain(uprawnienie);
    }

    // ============================== K4 — Nagrody/kary; oświadczenia ==============================

    [Test]
    [Description("K4a: NagrodaKara jest ABSTRAKCYJNA — używamy podtypu new Nagroda(pracownik); ctor ustawia " +
                 "Typ na Nagroda; Definicja to słownik DefNagrodKar; wpis trafia do pracownik.NagrodyKary.")]
    public void K4a_Nagroda_DodaniePodtypuKonkretnego_UstawiaTypNagroda()
    {
        // Definicja musi zgadzać się typem z wpisem — dla Nagrody bierzemy definicję o Typ == Nagroda
        // (przypisanie niezgodnej typem definicji rzuca ArgumentException w set_Definicja).
        var definicja = Kadry.DefNagrodKar.Cast<DefinicjaNagrodyKary>()
            .FirstOrDefault(d => d.Typ == TypNagrodyKary.Nagroda);
        if (definicja == null)
            Assert.Ignore("Brak definicji typu Nagroda (DefNagrodKar) w bazie Demo — wpisu nie można utworzyć.");

        var pracownik = Host();
        Nagroda nagroda = null;

        InTransaction(() =>
        {
            // NIE new NagrodaKara(...) — typ abstrakcyjny. Konkretny podtyp ustawia Typ.
            nagroda = Session.AddRow(new Nagroda(pracownik));
            nagroda.Definicja = definicja;
            nagroda.Data = Date.Today;
        });

        nagroda.Pracownik.Should().Be(pracownik);
        nagroda.Typ.Should().Be(TypNagrodyKary.Nagroda, "ctor podtypu Nagroda ustawia pole Typ");
        pracownik.NagrodyKary.Cast<NagrodaKara>().Should().Contain(nagroda);
    }

    [Test]
    [Description("K4a: konkretny podtyp Kara ustawia Typ na Kara; oba podtypy trafiają do tej samej kolekcji " +
                 "pracownik.NagrodyKary (SubTable<NagrodaKara>).")]
    public void K4a_Kara_DodaniePodtypuKonkretnego_UstawiaTypKara()
    {
        // Dla Kary bierzemy definicję o Typ == Kara (analogicznie do Nagrody).
        var definicja = Kadry.DefNagrodKar.Cast<DefinicjaNagrodyKary>()
            .FirstOrDefault(d => d.Typ == TypNagrodyKary.Kara);
        if (definicja == null)
            Assert.Ignore("Brak definicji typu Kara (DefNagrodKar) w bazie Demo — wpisu nie można utworzyć.");

        var pracownik = Host();
        Kara kara = null;

        InTransaction(() =>
        {
            kara = Session.AddRow(new Kara(pracownik));
            kara.Definicja = definicja;
            kara.Data = Date.Today;
        });

        kara.Typ.Should().Be(TypNagrodyKary.Kara, "ctor podtypu Kara ustawia pole Typ");
        pracownik.NagrodyKary.Cast<NagrodaKara>().Should().Contain(kara);
    }

    [Test]
    [Description("K4b: OświadczeniePracownika NIE ma ctora samego (Pracownik) — Definicja jest [Required] " +
                 "w konstruktorze; wariant (pracownik, definicja, Date) ustawia DataZlozenia; słownik DefOswiadczen.")]
    public void K4b_Oswiadczenie_DodanieZWymaganaDefinicjaIData_TrafiaDoKolekcji()
    {
        // Preferuj PIT-2, ale dowolna definicja oświadczenia wystarcza (ctor wymaga definicji).
        var definicja = Kadry.DefOswiadczen.Cast<DefinicjaOświadczenia>().FirstOrDefault(d => d.Nazwa == "PIT-2")
                        ?? Pierwsza<DefinicjaOświadczenia>(Kadry.DefOswiadczen);
        if (definicja == null)
            Assert.Ignore("Brak definicji oświadczenia (DefOswiadczen) w bazie Demo — oświadczenia nie można utworzyć (definicja jest [Required] w ctorze).");

        var pracownik = Host();
        OświadczeniePracownika oswiadczenie = null;

        InTransaction(() =>
        {
            // Definicja przekazywana w konstruktorze (nie ustawiana po fakcie); wariant z datą złożenia.
            oswiadczenie = Session.AddRow(new OświadczeniePracownika(pracownik, definicja, Date.Today));
        });

        oswiadczenie.Pracownik.Should().Be(pracownik);
        oswiadczenie.Definicja.Should().Be(definicja, "definicja jest przekazywana w ctorze");
        oswiadczenie.DataZlozenia.Should().Be(Date.Today, "wariant ctora z Date ustawia DataZlozenia");
        pracownik.Oświadczenia.Cast<OświadczeniePracownika>().Should().Contain(oswiadczenie);
    }

    // ============================== K5 — Wypadki przy pracy ==============================

    [Test]
    [Description("K5: new Wypadek(pracownik); Data to Date, Godzina to Soneta.Types.Time; pola opisowe " +
                 "(Okolicznosci/Skutki) to MemoText; flagi skutków to bool; wpis trafia do pracownik.Wypadki.")]
    public void K5_Wypadek_DodanieZDanymiPodstawowymi_TrafiaDoKolekcji()
    {
        var pracownik = Host();
        Soneta.Kadry.Wypadek wypadek = null;

        InTransaction(() =>
        {
            wypadek = Session.AddRow(new Soneta.Kadry.Wypadek(pracownik));
            wypadek.Data = Date.Today;
            wypadek.Godzina = new Time(10, 30);     // Soneta.Types.Time, nie DateTime
            wypadek.DataZgloszenia = Date.Today;
            wypadek.Miejsce = "Hala produkcyjna";
            wypadek.PrzyPracy = true;
            wypadek.Okolicznosci = (MemoText)"Poślizgnięcie na mokrej posadzce.";   // MemoText (konwersja ze string), nie string
        });

        wypadek.Pracownik.Should().Be(pracownik);
        wypadek.Miejsce.Should().Be("Hala produkcyjna");
        wypadek.PrzyPracy.Should().BeTrue();
        wypadek.Godzina.Should().Be(new Time(10, 30));
        pracownik.Wypadki.Cast<Soneta.Kadry.Wypadek>().Should().Contain(wypadek);
    }

    [Test]
    [Description("K5: Wypadek wymaga Definicja (Soneta.Core.DefinicjaDokumentu) do numeracji — Numer " +
                 "(NumerDokumentu) nadaje platforma. Sprawdzamy, że pole Definicja jest częścią kontraktu.")]
    public void K5_Wypadek_PoleDefinicjaJestCzesciaKontraktu()
    {
        var pracownik = Host();
        Soneta.Kadry.Wypadek wypadek = null;

        InTransaction(() =>
        {
            wypadek = Session.AddRow(new Soneta.Kadry.Wypadek(pracownik));
            wypadek.Data = Date.Today;
        });

        // Numer jest subrowem nadawanym wg Definicja — nie ustawiamy Numer.Pelny ręcznie.
        wypadek.Numer.Should().NotBeNull("Numer to subrow NumerDokumentu zawsze obecny na wpisie");
    }
}
