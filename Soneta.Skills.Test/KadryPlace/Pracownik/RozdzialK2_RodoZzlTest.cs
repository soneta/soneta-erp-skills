using System.Linq;
using AwesomeAssertions;
using NUnit.Framework;
using Soneta.Business;
using Soneta.Core;
using Soneta.HR;
using Soneta.HR2;
using Soneta.Kadry;
using Soneta.Types;
using Prac = Soneta.Kadry.Pracownik;

namespace Soneta.Skills.Test.KadryPlace.Pracownik;

/// <summary>
/// Rozdział K (część druga) — RODO/GIODO, struktura organizacyjna, oceny, rekrutacja (receptury K6–K9).
/// <para>
/// Testy to <b>wykonywalna dokumentacja</b> publicznego kontraktu platformy Soneta dla zaawansowanych
/// obszarów kadrowych. Wszystkie te obszary łączy jedna cecha: rekordy operacyjne wymagają
/// <b>referencji do definicji konfiguracyjnych</b> (słowników GIODO, struktury organizacyjnej, ocen,
/// stanowisk/etapów rekrutacji), które w bazie Demo (GoldStandard) <b>mogą nie istnieć</b>. Strategia
/// jest jednolita: definicję pobieramy dynamicznie (pierwszy rekord z tabeli konfiguracyjnej); gdy
/// jej brak — test jest oznaczany <c>Assert.Ignore</c> z powodem. Tam, gdzie da się przetestować
/// realnie (odczyt kolekcji, dodanie wpisu przy dostępnej definicji), robimy to na żywych danych.
/// </para>
/// <list type="bullet">
/// <item><b>K6</b> — RODO/GIODO: <c>new GIODOOświadczenie(pracownik, def)</c>, <c>new GIODOUprawnienie(pracownik, def)</c>;
///   kolekcje <c>GIODOOświadczenia</c>/<c>GIODOUprawnienia</c>/<c>GIODOUdostępnienia</c>;
///   <c>GIODOWymianaDanych</c> bez publicznego ctora → tylko odczyt + [Ignore]; zapis teczki do pliku → [Ignore].</item>
/// <item><b>K7</b> — struktura organizacyjna: <c>new PowiązanieStrukturyOrganizacyjnej(element, pracownik)</c>,
///   <c>Etat.Wydzial</c> (dane historyczne), manager <c>StrukturaOraganizacyjna</c> (odczyt).</item>
/// <item><b>K8</b> — oceny: <c>new OcenaPracownika(pracownik)</c> + <c>new ElementOcenyPracownika(ocena)</c>,
///   <c>new CelOkresowyPracownika(pracownik)</c>.</item>
/// <item><b>K9</b> — rekrutacja: <c>new RekrutacjaAplikacja(pracownik, wydziałDefStanowiska)</c>,
///   <c>new Rekrutacja(pracownik)</c>, <c>new EtapRekrutacji(rekrutacja)</c>.</item>
/// </list>
/// <para>
/// Operujemy wyłącznie na <b>publicznym kontrakcie</b> — jak dodatek programisty zewnętrznego bez
/// dostępu do źródeł. Baza Demo z rollbackiem po teście (helper <c>InTransaction</c> z <c>TestBase</c>).
/// </para>
/// </summary>
[TestFixture]
public class RozdzialK2_RodoZzlTest : PracownikTestBase
{
    // Pracownik-host dla wpisów — dowolny etatowy z Demo (stabilny punkt wejścia).
    private Prac Host() => Pracownik(Pracownik_.Andrzejewski) ?? PierwszyPracownik();

    // Pierwszy rekord z tabeli konfiguracyjnej (lub null) — bez twardej zależności od nazwy słownika.
    private static T Pierwsza<T>(Table tabela) where T : Row =>
        tabela.Cast<T>().FirstOrDefault();

    // ============================== K6 — RODO/GIODO ==============================

    [Test]
    [Description("K6: new GIODOOświadczenie(pracownik, definicja) — Host wynika z ctora (Pracownik implementuje " +
                 "IGIODOOświadczenieHost), Definicja (GIODODefOswiadcz) jest WYMAGANA przez ctor; pole Data to Date; " +
                 "Rodzaj/Okres są WYLICZANE (read-only) z definicji; wpis trafia do pracownik.GIODOOświadczenia. " +
                 "Gdy w Demo brak definicji oświadczenia lub brak prawa zapisu do obszaru RODO → Ignore.")]
    public void K6_GIODOOswiadczenie_DodanieZDefinicja_TrafiaDoKolekcji()
    {
        // Tabela konfiguracyjna czytana wprost z sesji operacyjnej (jak słowniki w K1).
        var def = Pierwsza<GIODODefinicjaOświadczenia>(Session.GetCore().GIODODefOswiadcz);
        if (def == null)
            Assert.Ignore("Brak definicji oświadczenia GIODO (CoreModule.GIODODefOswiadcz) w bazie Demo — wpisu nie można utworzyć (Definicja jest wymagana w ctorze).");

        var pracownik = Host();
        GIODOOświadczenie oswiadczenie = null;

        try
        {
            InTransaction(() =>
            {
                // Definicja wynika z ctora; Rodzaj/Okres są wyliczane przez platformę — nie ustawiamy ich ręcznie.
                oswiadczenie = Session.AddRow(new GIODOOświadczenie(pracownik, def));
                oswiadczenie.Data = Date.Today;
                oswiadczenie.SposobPozyskania = "Formularz papierowy";
            });
        }
        catch (AccessWriteDeniedException)
        {
            // Egzekucji praw nie testujemy (safe-code §7.2) — rola Demo blokuje zapis do obszaru RODO/GIODO.
            Assert.Ignore("Rola bazy Demo nie ma prawa zapisu do GIODOOświadczenie — egzekucji praw nie testujemy (safe-code §7.2).");
        }

        oswiadczenie.Host.Should().Be(pracownik, "ctor (host, definicja) ustawia Host na pracownika");
        oswiadczenie.Definicja.Should().Be(def, "Definicja przekazywana jest w ctorze");
        oswiadczenie.Data.Should().Be(Date.Today);
        pracownik.GIODOOświadczenia.Cast<GIODOOświadczenie>()
            .Should().Contain(oswiadczenie, "wpis trafia do kolekcji SubTable pracownika");
    }

    [Test]
    [Description("K6: new GIODOUprawnienie(pracownik, definicja) — Uprawniony z ctora (IGIODOUprawnienieHost), " +
                 "Definicja (GIODODefUprawn) wymagana; pola Data/Przyznane/Odebrane to Date (Okres jest wyliczany, " +
                 "read-only); wpis trafia do pracownik.GIODOUprawnienia. Gdy brak definicji w Demo → Ignore.")]
    public void K6_GIODOUprawnienie_DodanieZDefinicja_TrafiaDoKolekcji()
    {
        var def = Pierwsza<GIODODefinicjaUprawnienia>(Session.GetCore().GIODODefUprawn);
        if (def == null)
            Assert.Ignore("Brak definicji uprawnienia GIODO (CoreModule.GIODODefUprawn) w bazie Demo — wpisu nie można utworzyć.");

        var pracownik = Host();
        GIODOUprawnienie uprawnienie = null;

        try
        {
            InTransaction(() =>
            {
                uprawnienie = Session.AddRow(new GIODOUprawnienie(pracownik, def));
                uprawnienie.Data = Date.Today;
                uprawnienie.Przyznane = Date.Today;   // Okres jest wyliczany — nie ustawiamy go bezpośrednio.
            });
        }
        catch (AccessWriteDeniedException)
        {
            Assert.Ignore("Rola bazy Demo nie ma prawa zapisu do GIODOUprawnienie — egzekucji praw nie testujemy (safe-code §7.2).");
        }

        uprawnienie.Uprawniony.Should().Be(pracownik, "ctor (uprawniony, definicja) ustawia Uprawniony");
        uprawnienie.Definicja.Should().Be(def);
        uprawnienie.Przyznane.Should().Be(Date.Today);
        pracownik.GIODOUprawnienia.Cast<GIODOUprawnienie>().Should().Contain(uprawnienie);
    }

    [Test]
    [Description("K6: GIODOWymianaDanych (pozyskanie/udostępnienie/powierzenie) NIE ma publicznego ctora — " +
                 "rekordy tworzą wyłącznie workery (DodajPozyskanieDanychWorker itd.). Kolekcja GIODOUdostępnienia " +
                 "jest jednak dostępna do ODCZYTU jako część publicznego kontraktu.")]
    public void K6_GIODOUdostepnienia_KolekcjaDostepnaDoOdczytu()
    {
        var pracownik = Host();

        // GIODOUdostępnienia to SubTable<GIODOWymianaDanych> — odczyt jest częścią kontraktu,
        // nawet gdy w Demo nie ma żadnych zapisów wymiany danych.
        pracownik.GIODOUdostępnienia.Should().NotBeNull("kolekcja wymiany danych jest zawsze dostępna (odczyt)");
        pracownik.GIODOUdostępnienia.Cast<GIODOWymianaDanych>().Should().OnlyContain(w => w != null);
    }

    [Test]
    [Ignore("Dodanie GIODOWymianaDanych wymaga workera (DodajPozyskanieDanychWorker/DodajUdostępnienieDanychWorker/" +
            "DodajPowierzenieDanychWorker) oraz podmiotu (IKontrahent) i — w zależności od kierunku — definicji " +
            "dokumentu/zbioru danych z konfiguracji modułu RODO, których baza Demo może nie mieć. Brak publicznego " +
            "ctora uniemożliwia deterministyczny zapis bez tej konfiguracji.")]
    [Description("K6: dodanie zapisu wymiany danych GIODO przez DodajPozyskanieDanychWorker (CommitUI + Save).")]
    public void K6_GIODOWymianaDanych_DodaniePrzezWorker()
    {
    }

    [Test]
    [Ignore("Zapis teczki personalnej (Pracownik.ZapiszTeczkęDoPlikuWorker.ZapiszTeczkeDoPliku()) to operacja " +
            "plikowa — serializuje dokumentację pracownika do plików/katalogu na dysku. Poza zakresem testów " +
            "jednostkowych (zależność od systemu plików).")]
    [Description("K6: zapis teczki personalnej RODO do pliku (operacja plikowa).")]
    public void K6_ZapisTeczkiDoPliku()
    {
    }

    // ============================== K7 — Struktura organizacyjna ==============================

    [Test]
    [Description("K7: new PowiązanieStrukturyOrganizacyjnej(element, pracownik) — Zrodlo z ctora (Pracownik " +
                 "implementuje IŹródłoPowiązaniaStrukturyOrganizacyjnej), Element to istniejący element struktury " +
                 "(CoreModule.ElementyStrOrg — NIE definicja DefElStrukturOrg); Okres to FromTo; wpis trafia do " +
                 "pracownik.PowiązaniaStrOrg. Gdy brak elementów struktury w Demo lub brak prawa zapisu → Ignore.")]
    public void K7_PowiazanieStruktury_DodanieZElementem_TrafiaDoKolekcji()
    {
        // Elementy struktury (instancje) są w ElementyStrOrg; DefElStrukturOrg trzyma DEFINICJE elementów.
        var element = Pierwsza<ElementStrukturyOrganizacyjnej>(Session.GetCore().ElementyStrOrg);
        if (element == null)
            Assert.Ignore("Brak elementów struktury organizacyjnej (CoreModule.ElementyStrOrg) w bazie Demo — powiązania nie można utworzyć.");

        var pracownik = Host();
        PowiązanieStrukturyOrganizacyjnej powiazanie = null;

        try
        {
            InTransaction(() =>
            {
                powiazanie = Session.AddRow(new PowiązanieStrukturyOrganizacyjnej(element, pracownik));
                powiazanie.Okres = new FromTo(Date.Today, Date.MaxValue);
            });
        }
        catch (AccessWriteDeniedException)
        {
            Assert.Ignore("Rola bazy Demo nie ma prawa zapisu do PowiązanieStrukturyOrganizacyjnej — egzekucji praw nie testujemy (safe-code §7.2).");
        }

        powiazanie.Zrodlo.Should().Be(pracownik, "ctor (element, zrodlo) ustawia Zrodlo na pracownika");
        powiazanie.Element.Should().Be(element);
        pracownik.PowiązaniaStrOrg.Cast<PowiązanieStrukturyOrganizacyjnej>().Should().Contain(powiazanie);
    }

    [Test]
    [Description("K7: pracownik.StrukturaOraganizacyjna to manager (StrukturaOraganizacyjnaManager) — API tylko " +
                 "do odczytu nawigacji przełożeni/podwładni. Jest zawsze dostępny, niezależnie od konfiguracji struktury.")]
    public void K7_StrukturaOrganizacyjna_ManagerOdczytuJestDostepny()
    {
        var pracownik = Host();

        pracownik.StrukturaOraganizacyjna.Should().NotBeNull("manager struktury jest zawsze dostępny (odczyt)");
        pracownik.StrukturaOraganizacyjna.Should().BeOfType<Prac.StrukturaOraganizacyjnaManager>();
        // Przełożony „na dzień" może być null (brak skonfigurowanej struktury) — czytamy bez wyjątku.
        var _ = pracownik.StrukturaOraganizacyjna.GetDomyślnyPrzełożony(Date.Today);
    }

    [Test]
    [Description("K7: Etat.Wydzial to dane HISTORYCZNE (na PracHistoria.Etat) i jednostka organizacyjna pracownika. " +
                 "Dla etatowego pracownika z Demo wydział na zapisie obowiązującym dziś jest ustawiony (wymagany dla etatu).")]
    public void K7_EtatWydzial_JestUstawionyDlaEtatowca()
    {
        var pracownik = Host();
        var ph = pracownik[Date.Today];   // zapis historii obowiązujący na dzień (A15)

        ph.Should().NotBeNull("etatowy pracownik z Demo ma zapis historii obowiązujący dziś");
        // Wydzial jest wymagany dla etatu — odczyt jako część kontraktu (referencja do Soneta.Kadry.Wydzial).
        ph.Etat.Should().NotBeNull();
        ph.Etat.Wydzial.Should().NotBeNull("Etat.Wydzial (jednostka organizacyjna) jest wymagany dla etatu");
    }

    // ============================== K8 — Oceny okresowe ==============================

    [Test]
    [Description("K8: new OcenaPracownika(pracownik) (arkusz, root w HR.OcenyPracownikow) + new ElementOcenyPracownika(ocena) " +
                 "gdzie ocena jest IOcenaPracownika; ElementOcenyPracownika.Wartosc to decimal (Typ/Data są wyliczane, read-only). " +
                 "Element wymaga Definicja (HR.DefElemOcenPrac) — gdy brak w Demo, sam arkusz i pusta kolekcja elementów wystarczają.")]
    public void K8_OcenaPracownika_ArkuszZElementem_TrafiaDoKolekcji()
    {
        var hr = Session.GetHR();
        var pracownik = Host();
        var defElementu = Pierwsza<DefElementuOcenyPracownika>(hr.DefElemOcenPrac);

        OcenaPracownika ocena = null;
        ElementOcenyPracownika element = null;

        InTransaction(() =>
        {
            ocena = Session.AddRow(new OcenaPracownika(pracownik));
            ocena.Nazwa = "Ocena roczna 2026";
            ocena.Data = Date.Today;

            // Element dodajemy tylko gdy istnieje definicja (Definicja jest wymagana do zapisu elementu).
            if (defElementu != null)
            {
                element = Session.AddRow(new ElementOcenyPracownika(ocena)); // ocena jako IOcenaPracownika
                element.Definicja = defElementu;
                element.Wartosc = 4m;   // Wartosc to decimal (Typ/Data ustawia platforma na podstawie definicji)
            }
        });

        ocena.Pracownik.Should().Be(pracownik, "ctor (Pracownik) ustawia ocenianego");
        ocena.Nazwa.Should().Be("Ocena roczna 2026");
        pracownik.Oceny.Cast<OcenaPracownika>().Should().Contain(ocena, "arkusz trafia do kolekcji pracownika");

        if (defElementu != null)
        {
            element.Ocena.Should().Be(ocena, "ctor (IOcenaPracownika) wiąże element z arkuszem");
            element.Wartosc.Should().Be(4m);
            ocena.ElementyOceny.Cast<ElementOcenyPracownika>().Should().Contain(element);
        }
        else
        {
            Assert.Warn("Brak definicji elementu oceny (HR.DefElemOcenPrac) w Demo — przetestowano sam arkusz oceny bez pozycji.");
        }
    }

    [Test]
    [Description("K8: new CelOkresowyPracownika(pracownik) (root w HR2.CeleOkresowePrac); pola Nazwa/Data/Termin/Opis; " +
                 "Definicja to Soneta.Oceny.DefinicjaElementuOceny (opcjonalna referencja konfiguracyjna); wpis trafia " +
                 "do pracownik.CeleOkresowe.")]
    public void K8_CelOkresowy_Dodanie_TrafiaDoKolekcji()
    {
        var pracownik = Host();
        CelOkresowyPracownika cel = null;

        InTransaction(() =>
        {
            cel = Session.AddRow(new CelOkresowyPracownika(pracownik));
            cel.Nazwa = "Wdrożenie nowego modułu";
            cel.Data = Date.Today;
            cel.Termin = new Date(2026, 12, 31);
            cel.Opis = (MemoText)"Cel rozwojowy na bieżący okres oceny.";
        });

        cel.Pracownik.Should().Be(pracownik, "ctor (Pracownik) ustawia pracownika celu");
        cel.Nazwa.Should().Be("Wdrożenie nowego modułu");
        cel.Termin.Should().Be(new Date(2026, 12, 31));
        pracownik.CeleOkresowe.Cast<CelOkresowyPracownika>().Should().Contain(cel);
    }

    // ============================== K9 — Rekrutacja ==============================

    [Test]
    [Description("K9: new RekrutacjaAplikacja(kandydat, wydziałDefStanowiska) — kandydat to Pracownik, ctor przyjmuje " +
                 "WydziałDefinicjiStanowiska (powstaje z new WydziałDefinicjiStanowiska(DefinicjaStanowiska) — typ z Soneta.HR). " +
                 "Stan to StanAplikacji; wpis trafia do kandydat.Aplikacje. Gdy brak definicji stanowiska (HR.DefStanowisk) → Ignore.")]
    public void K9_RekrutacjaAplikacja_DodanieZeStanowiskiem_TrafiaDoKolekcji()
    {
        var hr = Session.GetHR();
        var defStanowiska = Pierwsza<DefinicjaStanowiska>(hr.DefStanowisk);
        if (defStanowiska == null)
            Assert.Ignore("Brak definicji stanowiska (HR.DefStanowisk) w bazie Demo — aplikacji rekrutacyjnej nie można utworzyć (ctor wymaga WydziałDefinicjiStanowiska).");

        var kandydat = Host();
        RekrutacjaAplikacja aplikacja = null;

        InTransaction(() =>
        {
            // WydziałDefinicjiStanowiska powstaje z DefinicjaStanowiska (ctor w Soneta.HR).
            var wydzialDef = new WydziałDefinicjiStanowiska(defStanowiska);
            aplikacja = Session.AddRow(new RekrutacjaAplikacja(kandydat, wydzialDef));
            aplikacja.Data = Date.Today;
            aplikacja.Stan = StanAplikacji.Wprowadzona;
        });

        aplikacja.Pracownik.Should().Be(kandydat, "ctor (Pracownik, …) ustawia kandydata");
        aplikacja.Stanowisko.Should().Be(defStanowiska, "WydziałDefinicjiStanowiska niesie referencję do DefinicjaStanowiska");
        aplikacja.Stan.Should().Be(StanAplikacji.Wprowadzona);
        kandydat.Aplikacje.Cast<RekrutacjaAplikacja>().Should().Contain(aplikacja);
    }

    [Test]
    [Description("K9: new Rekrutacja(kandydat) (root w HR.Rekrutacje; impl. IOcenaPracownika) ustawia pole Pracownik; " +
                 "+ new EtapRekrutacji(rekrutacja) wiąże etap przez pole Rekrutacja; Etap.Definicja to HR.DefEtaRekrutacji " +
                 "(wymagana do zapisu etapu), Etap.Lp/Data. Gdy brak definicji etapu w Demo, testujemy samą rekrutację (warn). " +
                 "Gdy brak prawa zapisu → Ignore.")]
    public void K9_RekrutacjaIEtap_Dodanie_TrafiaDoKolekcji()
    {
        var hr = Session.GetHR();
        var kandydat = Host();
        var defEtapu = Pierwsza<DefinicjaEtapuRekrutacji>(hr.DefEtaRekrutacji);

        Rekrutacja rekrutacja = null;
        EtapRekrutacji etap = null;

        try
        {
            InTransaction(() =>
            {
                rekrutacja = Session.AddRow(new Rekrutacja(kandydat));

                if (defEtapu != null)
                {
                    etap = Session.AddRow(new EtapRekrutacji(rekrutacja));
                    etap.Definicja = defEtapu;
                    etap.Lp = 1;
                    etap.Data = Date.Today;
                }
            });
        }
        catch (AccessWriteDeniedException)
        {
            Assert.Ignore("Rola bazy Demo nie ma prawa zapisu do Rekrutacja/EtapRekrutacji — egzekucji praw nie testujemy (safe-code §7.2).");
        }

        rekrutacja.Should().NotBeNull("ctor (Pracownik) tworzy rekrutację dla kandydata");
        rekrutacja.Pracownik.Should().Be(kandydat, "ctor (Pracownik) ustawia kandydata rekrutacji");
        // Rekrutacja jest rootem w HR.Rekrutacje (kolekcje na Pracowniku wiążą się przez relacje child).
        hr.Rekrutacje.Cast<Rekrutacja>().Should().Contain(rekrutacja, "rekrutacja trafia do tabeli głównej HR.Rekrutacje");

        if (defEtapu != null)
        {
            etap.Rekrutacja.Should().Be(rekrutacja, "ctor (Rekrutacja) wiąże etap z rekrutacją");
            etap.Lp.Should().Be(1);
            hr.EtapyRekrutacji.Cast<EtapRekrutacji>().Should().Contain(etap, "etap trafia do tabeli głównej HR.EtapyRekrutacji");
        }
        else
        {
            Assert.Warn("Brak definicji etapu rekrutacji (HR.DefEtaRekrutacji) w Demo — przetestowano samą rekrutację bez etapów.");
        }
    }

    [Test]
    [Description("K9: kandydat.Aplikacje / Rekrutacje / EtapyRekrutacji / Kandydatury to kolekcje SubTable dostępne " +
                 "do odczytu jako część publicznego kontraktu — niezależnie od stanu konfiguracji rekrutacji.")]
    public void K9_KolekcjeRekrutacji_DostepneDoOdczytu()
    {
        var kandydat = Host();

        kandydat.Aplikacje.Should().NotBeNull();
        kandydat.Rekrutacje.Should().NotBeNull();
        kandydat.EtapyRekrutacji.Should().NotBeNull();
        kandydat.Kandydatury.Should().NotBeNull();
    }
}
