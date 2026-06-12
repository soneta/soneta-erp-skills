using System;
using System.Linq;
using AwesomeAssertions;
using NUnit.Framework;
using Soneta.Business;
using Soneta.Kadry;
using Soneta.Kalend;
using Soneta.Types;
using Prac = Soneta.Kadry.Pracownik;

namespace Soneta.Skills.Test.KadryPlace.Pracownik;

/// <summary>
/// Rozdział E/F (część druga) — operacje na planie pracy i RCP wykraczające poza CRUD dni:
/// <list type="bullet">
/// <item>KADRY-E3 — aktualizacja kalendarza pracownika (worker seryjny, wymaga Context → <c>[Ignore]</c>),</item>
/// <item>KADRY-E4 — uzgodnienie doby pracowniczej (worker dnia/grupowy, wymaga Context → <c>[Ignore]</c>),</item>
/// <item>KADRY-E5 — odczyt normy i czasu przepracowanego przez <c>pracownik.Czasy : KalkulatorPracownika</c> (★ pełny odczyt),</item>
/// <item>KADRY-F3 — import RCP: sam import plikowy <c>[Ignore]</c>; przeliczenie we/wy przez <c>ImportDniaWorker</c> (★),</item>
/// <item>KADRY-F4 — weryfikacja/korekta RCP: <c>DzienRCP</c>/<c>StanRCP</c> (★ korekta na świeżym dniu),</item>
/// <item>KADRY-F5 — praca hybrydowa: strefy dnia i podzielniki (★ odczyt).</item>
/// </list>
/// <para>
/// Operujemy wyłącznie na <b>publicznym kontrakcie</b> platformy Soneta (jak dodatek zewnętrzny),
/// na bazie Demo (GoldStandard) z automatycznym rollbackiem. Daty Demo planu/pracy są nieznane, więc
/// odczyty istniejących danych traktujemy defensywnie (kolekcja istnieje / indeksator nie rzuca),
/// a scenariusze zapisu budujemy na własnych, jawnych datach dla pracownika „006".
/// </para>
/// <para>
/// <b>Granica testowalności.</b> Operacje wymagające <see cref="Context"/> (worker KADRY-E3/KADRY-E4 grupowy —
/// <c>Params : ContextBase</c> z ctorem <c>(Context)</c>, karmiony zaznaczeniem listy) lub źródła
/// zewnętrznego (import RCP z pliku/czytnika) są oznaczone <c>[Ignore]</c> z uzasadnieniem — opisują
/// kontrakt, nie wykonują operacji. <c>KalkulatorPracownika</c>/<c>CzasDni</c>/<c>ZestawienieNadgodzin</c>
/// nie są wierszami ORM — to obiekty liczące (czysty odczyt bez transakcji).
/// </para>
/// </summary>
[TestFixture]
public class RozdzialEFrest_PlanRcpTest : PracownikTestBase
{
    // Jawne daty/okresy do scenariuszy (nie Date.Today — data biznesowa Demo bywa inna).
    private static readonly Date Dzien = new(2026, 6, 1);
    private static readonly FromTo Okres = new(new Date(2026, 6, 1), new Date(2026, 6, 30));
    private static readonly YearMonth Miesiac = new(2026, 6);

    // ============================== KADRY-E3 — Aktualizacja kalendarza pracownika ==============================

    [Test]
    [Description("KADRY-E3 (kontrakt, [Ignore]): AktualizujKalendarzWorker to worker seryjny z menu Czynności. " +
                 "Pracownicy/Pars są set-only, a Params : ContextBase ma ctor (Context) — bez zaznaczenia " +
                 "listy (Context) nie da się zbudować parametrów, więc operacji nie wykonujemy w teście.")]
    [Ignore("KADRY-E3: AktualizujKalendarzWorker.Params : ContextBase wymaga Context (zaznaczenie listy pracowników) — brak czystego API bezkontekstowego.")]
    public void KADRY_E3_AktualizujKalendarz_WymagaContext_Ignore()
    {
        // Świadomie nie wykonujemy — operacja seryjna sterowana zaznaczeniem UI (Context).
        // worker.Pracownicy = context.Get<Pracownik[]>();
        // worker.Pars = new AktualizujKalendarzWorker.Params(context) { Data = ..., Docelowy = ..., Zmiana = true };
        // worker.Aktualizuj();   // Logout + Commit wewnątrz
        Assert.Fail("Test oznaczony [Ignore] — nie powinien być uruchamiany.");
    }

    [Test]
    [Description("KADRY-E3 (odczyt konfiguracji): kalendarz docelowy/źródłowy aktualizacji to konfiguracja " +
                 "Etat.Kalendarz oraz interpretacja Etat.InterpretacjaKalendarza — odczyt nie wymaga workera " +
                 "ani Context i nie rzuca; pokazuje skąd worker KADRY-E3 bierze stan wejściowy.")]
    public void KADRY_E3_KalendarzIInterpretacja_OdczytKonfiguracjiEtatu_NieRzuca()
    {
        var p = Pracownik(Pracownik_.Andrzejewski);
        p.Should().NotBeNull("pracownik '006' istnieje w bazie Demo");

        System.Action odczyt = () =>
        {
            // Etat leży na bieżącym zapisie historycznym (pracownik.Last.Etat); kalendarz i interpretacja
            // sterują aktualizacją (KADRY-E3).
            var etat = p.Last?.Etat;
            if (etat is not null)
            {
                Kalendarz kal = etat.Kalendarz;                    // kalendarz roboczy (źródło/cel zmiany)
                InterpretacjaKalendarza interpretacja = etat.InterpretacjaKalendarza;
                _ = interpretacja;
                if (kal is not null)
                {
                    Time _ = kal.NormaDobowa;                      // norma dobowa kalendarza
                }
            }
        };
        odczyt.Should().NotThrow("odczyt kalendarza/interpretacji z Etatu nie wymaga Context ani transakcji");
    }

    // ============================== KADRY-E4 — Uzgodnienie doby pracowniczej ==============================

    [Test]
    [Description("KADRY-E4 (kontrakt, odczyt): granica doby to atrybuty KONFIGURACYJNE Etatu " +
                 "(ConfigPoczątekDobyNiedzieledIŚwięta — read-only, NormaDobowa) — nie ma edytowalnego pola " +
                 "początku doby na pojedynczym DzienPracy. Odczyt tych pól nie rzuca.")]
    public void KADRY_E4_ModelDoby_OdczytKonfiguracjiEtatu_NieRzuca()
    {
        var p = Pracownik(Pracownik_.Andrzejewski);
        p.Should().NotBeNull("pracownik '006' istnieje w bazie Demo");

        System.Action odczyt = () =>
        {
            var etat = p.Last?.Etat;
            if (etat is not null)
            {
                Time poczatekDobySwieta = etat.ConfigPoczątekDobyNiedzieledIŚwięta; // konfiguracyjne, read-only
                Time normaDobowa = etat.NormaDobowa;
                _ = poczatekDobySwieta;
                _ = normaDobowa;
            }
        };
        odczyt.Should().NotThrow("granica doby/normy to konfiguracja Etatu — czysty odczyt");
    }

    [Test]
    [Description("KADRY-E4 (kontrakt, [Ignore]): worker pojedynczego dnia DzienPracy.UzgodnijDobePracowniczaWorker " +
                 "ma Dzień set-only i wymaga istniejącego dnia ewidencji oraz IsEnabled; worker grupowy " +
                 "(Params : ContextBase) wymaga Context. W Demo brak deterministycznej doby nocnej do uzgodnienia, " +
                 "więc operacji nie wykonujemy — opisujemy kontrakt (IsEnabled + Uzgodnij/Przenieś).")]
    [Ignore("KADRY-E4: UzgodnijDobePracownicza — worker dnia wymaga deterministycznego dnia nocnego (brak w Demo); worker grupowy wymaga Context.")]
    public void KADRY_E4_UzgodnijDobePracownicza_WymagaContextLubDanych_Ignore()
    {
        // var dzien = pracownik.DniPracy[data];
        // if (DzienPracy.UzgodnijDobePracowniczaWorker.IsEnabledUzgodnijDobePracownicza(dzien)) { ... }
        // new DzienPracy.UzgodnijDobePracowniczaWorker { Dzień = dzien }.UzgodnijDobePracownicza();
        Assert.Fail("Test oznaczony [Ignore] — nie powinien być uruchamiany.");
    }

    // ============================== KADRY-E5 — Odczyt normy / czasu przepracowanego (★ testowalne) ==============================

    [Test]
    [Description("KADRY-E5: pracownik.Czasy zwraca KalkulatorPracownika (NIE Row — obiekt liczący, czysty odczyt " +
                 "bez transakcji). Kalkulator istnieje dla pracownika z bazy Demo.")]
    public void KADRY_E5_Czasy_ZwracaKalkulatorPracownika_NieNull()
    {
        var p = Pracownik(Pracownik_.Andrzejewski);
        p.Should().NotBeNull("pracownik '006' istnieje w bazie Demo");

        KalkulatorPracownika kalk = p.Czasy;
        kalk.Should().NotBeNull("pracownik.Czasy daje kalkulator czasu pracy (kontekst pracownika)");
    }

    [Test]
    [Description("KADRY-E5: Norma(okres) (plan) i Praca(okres) (realizacja) zwracają CzasDni (Czas : Time, Dni : int). " +
                 "Wywołanie to czysty odczyt — nie rzuca i nie wymaga transakcji. Wartości mogą być Empty/Invalid " +
                 "(brak danych Demo w okresie), więc sprawdzamy tylko sam kontrakt odczytu.")]
    public void KADRY_E5_NormaIPraca_OdczytZaOkres_ZwracaCzasDni_NieRzuca()
    {
        var p = Pracownik(Pracownik_.Andrzejewski);
        var kalk = p.Czasy;

        CzasDni norma = CzasDni.Invalid;
        CzasDni praca = CzasDni.Invalid;

        System.Action odczyt = () =>
        {
            norma = kalk.Norma(Okres);                 // params Item[] condition — wywołanie bez filtra
            praca = kalk.Praca(Okres);                 // czas przepracowany (realizacja)
            _ = kalk.PracaRozliczana(Okres);           // czas rozliczany (do nadgodzin)
        };
        odczyt.Should().NotThrow("odczyt Norma/Praca przez KalkulatorPracownika jest bezpieczny (bez transakcji)");

        // CzasDni to obiekt wynikowy (Time + int) — pola tylko do odczytu; dostęp nie rzuca.
        System.Action poleCzasDni = () =>
        {
            Time _ = norma.Czas; int __ = norma.Dni;
            Time ___ = praca.Czas; int ____ = praca.Dni;
        };
        poleCzasDni.Should().NotThrow("CzasDni wystawia Czas/Dni jako odczyt");
    }

    [Test]
    [Description("KADRY-E5: NormaKodeksowa(YearMonth) zwraca normę kodeksową miesiąca (pełny etat) jako CzasDni; " +
                 "dla czerwca 2026 (20 dni roboczych × 8h) norma kodeksowa jest dodatnia — wynik nie jest Invalid " +
                 "i ma policzalne Dni/Czas.")]
    public void KADRY_E5_NormaKodeksowa_DlaMiesiaca_JestDodatnia()
    {
        var p = Pracownik(Pracownik_.Andrzejewski);
        var kalk = p.Czasy;

        CzasDni norma = kalk.NormaKodeksowa(Miesiac);

        // Norma kodeksowa miesiąca nie zależy od danych pracownika — to kalendarz kodeksowy.
        norma.Should().NotBe(CzasDni.Invalid, "norma kodeksowa istnieje dla każdego pełnego miesiąca");
        norma.Dni.Should().BeGreaterThan(0, "czerwiec 2026 ma dni robocze");
        norma.Czas.TotalMinutes.Should().BeGreaterThan(0, "pełny etat = dodatnia norma czasu pracy");
    }

    [Test]
    [Description("KADRY-E5: Nadgodziny(YearMonth) zwraca ZestawienieNadgodzin (struct: N50/N100/NSW/Razem — wszystkie Time, " +
                 "read-only). Nocne(okres) zwraca Time. Czysty odczyt — nie rzuca; przy braku danych Demo wynik = Zero.")]
    public void KADRY_E5_NadgodzinyINocne_OdczytStatystyk_NieRzuca()
    {
        var p = Pracownik(Pracownik_.Andrzejewski);
        var kalk = p.Czasy;

        ZestawienieNadgodzin nadg = ZestawienieNadgodzin.Zero;
        Time nocne = new(0);

        System.Action odczyt = () =>
        {
            nadg = kalk.Nadgodziny(Miesiac);
            nocne = kalk.Nocne(Okres);
        };
        odczyt.Should().NotThrow("odczyt nadgodzin/czasu nocnego jest bezpieczny");

        // Pola zestawienia to odczyt; Razem agreguje składowe (nie rzuca, może być Zero).
        System.Action pola = () => { Time _ = nadg.N50; Time __ = nadg.N100; Time ___ = nadg.Razem; _ = nocne; };
        pola.Should().NotThrow("ZestawienieNadgodzin wystawia N50/N100/Razem jako odczyt");
    }

    [Test]
    [Description("KADRY-E5: DniNie(okres)/NormaNie(okres) odczytują liczbę i normę dni nieobecności za okres. " +
                 "DniNie zwraca int (>=0), NormaNie zwraca CzasDni. Czysty odczyt — nie rzuca.")]
    public void KADRY_E5_NieobecnosciZaOkres_OdczytLiczbyINormy_NieRzuca()
    {
        var p = Pracownik(Pracownik_.Andrzejewski);
        var kalk = p.Czasy;

        int dniNie = -1;
        System.Action odczyt = () =>
        {
            dniNie = kalk.DniNie(Okres);          // liczba dni nieobecności
            _ = kalk.NormaNie(Okres);             // norma nieobecności (CzasDni)
        };
        odczyt.Should().NotThrow("odczyt nieobecności za okres przez kalkulator jest bezpieczny");
        dniNie.Should().BeGreaterThanOrEqualTo(0, "liczba dni nieobecności nie jest ujemna");
    }

    // ============================== KADRY-F3 — Import RCP (przeliczenie we/wy, ★) ==============================

    [Test]
    [Description("KADRY-F3 ([Ignore]): import surowych odbić z pliku/czytnika RCP wymaga zewnętrznego źródła " +
                 "(plik/serwis/format) — brak czystego API w publicznym kontrakcie. Testowalny jest jedynie " +
                 "fragment po wczytaniu: przeliczenie już-wpisanych we/wy przez ImportDniaWorker (osobny test).")]
    [Ignore("KADRY-F3: import z pliku/urządzenia RCP wymaga zewnętrznego źródła (I/O) — poza zakresem testu kontraktu.")]
    public void KADRY_F3_ImportZPliku_WymagaZrodlaZewnetrznego_Ignore()
    {
        Assert.Fail("Test oznaczony [Ignore] — nie powinien być uruchamiany.");
    }

    [Test]
    [Description("KADRY-F3 (przeliczenie, ★): po wpisaniu zdarzeń we/wy na dzień ewidencji (jak po imporcie) " +
                 "ImportDniaWorker { DzienPracy = dzien }.Przelicz() przelicza odbicia na czas pracy — operacja " +
                 "na obiektach sesji (bez I/O). Worker ma bezparametrowy ctor i property DzienPracy {get;set;}.")]
    public void KADRY_F3_ImportDniaWorker_PrzeliczWeWy_NieRzuca()
    {
        Guid guidPrac = Guid.Empty;

        InTransaction(() =>
        {
            var p = Pracownik(Pracownik_.Andrzejewski);
            guidPrac = p.Guid;

            // Dzień ewidencji (właściciel zdarzeń) — tworzymy ctorem + AddRow (sam ctor nie rejestruje).
            var dp = p.DniPracy[Dzien] ?? Session.AddRow(new DzienPracy(p, Dzien));

            // Surowe odbicia we/wy (tabela pośrednia) — tak wyglądają dane „po imporcie", przed przeliczeniem.
            var we = new WejscieWyjscie(dp);
            Kalend.WejsciaWyjscia.AddRow(we);
            we.Godzina = new Time(8, 0);
            we.Typ = TypWejsciaWyjscia.Wejscie;

            var wy = new WejscieWyjscie(dp);
            Kalend.WejsciaWyjscia.AddRow(wy);
            wy.Godzina = new Time(16, 0);
            wy.Typ = TypWejsciaWyjscia.Wyjscie;

            // Przeliczenie odbić na czas pracy dnia (bez pliku/urządzenia).
            System.Action przelicz = () => new ImportDniaWorker { DzienPracy = dp }.Przelicz();
            przelicz.Should().NotThrow("ImportDniaWorker.Przelicz() przelicza we/wy na czas pracy bez I/O");
        });
        SaveDispose();

        // Po przeliczeniu dzień ewidencji nadal jest dostępny przez indeksator [Date].
        var p2 = Get<Prac>(guidPrac);
        var dp2 = p2.DniPracy[Dzien];
        dp2.Should().NotBeNull("dzień ewidencji z przeliczonymi odbiciami istnieje po zapisie");
        dp2.WeWy.Cast<WejscieWyjscie>().Should().HaveCount(2, "wejście i wyjście zostały zachowane");
    }

    // ============================== KADRY-F4 — Weryfikacja / korekta RCP (★ testowalne) ==============================

    [Test]
    [Description("KADRY-F4 (odczyt): DniRCP to DateSubTable<DzienRCP> (typowane) — indeksator [Date] zwraca DzienRCP/null " +
                 "i nie rzuca. DzienRCP to wynik importu/weryfikacji; w Demo zwykle brak (null) dla naszej daty. " +
                 "Odczytujemy StanRCP (enum StanWeryfikacjiRCP) i Praca.Czas defensywnie.")]
    public void KADRY_F4_DniRCP_OdczytIndeksatoremPoDacie_NieRzuca()
    {
        var p = Pracownik(Pracownik_.Andrzejewski);
        p.DniRCP.Should().NotBeNull("kolekcja zweryfikowanego RCP (DniRCP) istnieje");

        System.Action odczyt = () =>
        {
            DzienRCP dzienRcp = p.DniRCP[Dzien];          // typowane: DzienRCP lub null
            if (dzienRcp is not null)
            {
                StanWeryfikacjiRCP stan = dzienRcp.StanRCP;   // enum stanu weryfikacji
                Time czas = dzienRcp.Praca.Czas;              // czas na subrowie Praca
                bool rcpOk = dzienRcp.RcpOK;                  // flaga stanu po imporcie
                _ = stan; _ = czas; _ = rcpOk;
            }
        };
        odczyt.Should().NotThrow("indeksator [Date] na DniRCP to bezpieczny odczyt");
    }

    [Test]
    [Description("KADRY-F4 (korekta, ★): na świeżo utworzonym DzienRCP korygujemy godziny na subrowie Praca, " +
                 "ustawiamy StanRCP (enum) na Poprawny i dopisujemy Uwagi (MemoText). Po zapisie DniRCP[data] " +
                 "zwraca dzień ze zmienionym stanem i godzinami. Czas/OdGodziny na rootcie są kalkulowane (read-only).")]
    public void KADRY_F4_KorektaDzienRCP_ZmianaStanuIGodzin_ZapisOdczyt()
    {
        Guid guidPrac = Guid.Empty;

        InTransaction(() =>
        {
            var p = Pracownik(Pracownik_.Andrzejewski);
            guidPrac = p.Guid;

            // W Demo DzienRCP zwykle nie istnieje na naszej dacie — do scenariusza korekty
            // tworzymy go ctorem + AddRow (analogicznie do DzienPracy). Korekta dotyczy istniejącego rekordu.
            var dzienRcp = p.DniRCP[Dzien] ?? Session.AddRow(new DzienRCP(p, Dzien));

            // Korekta godzin na subrowie Praca (root Czas/OdGodziny są kalkulowane).
            dzienRcp.Praca.OdGodziny = new Time(8, 0);
            dzienRcp.Praca.DoGodziny = new Time(16, 0);

            // Zmiana stanu weryfikacji (enum, nie string) + uwagi.
            dzienRcp.StanRCP = StanWeryfikacjiRCP.Poprawny;
            dzienRcp.Uwagi = (MemoText)"Skorygowano wyjście";
        });
        SaveDispose();

        var p2 = Get<Prac>(guidPrac);
        var rcp2 = p2.DniRCP[Dzien];
        rcp2.Should().NotBeNull("po zapisie dzień RCP jest dostępny przez indeksator [Date]");
        rcp2.StanRCP.Should().Be(StanWeryfikacjiRCP.Poprawny, "stan weryfikacji został ustawiony");
        rcp2.Praca.OdGodziny.Should().Be(new Time(8, 0));
        rcp2.Praca.DoGodziny.Should().Be(new Time(16, 0));
    }

    // ============================== KADRY-F5 — Praca hybrydowa / strefy / podzielniki (odczyt) ==============================

    [Test]
    [Description("KADRY-F5 (odczyt): DzienPracy.Strefy to SubTable<StrefaPracy> — podział dnia na strefy " +
                 "(stacjonarna / zdalna). Każda StrefaPracy ma Definicja : DefinicjaStrefy i CzasRozliczany : Time. " +
                 "Kolekcja istnieje (może być pusta w Demo); iteracja i odczyt pól nie rzucają.")]
    public void KADRY_F5_StrefyDniaPracy_OdczytPodzialuNaStrefy_NieRzuca()
    {
        Guid guidPrac = Guid.Empty;

        // Świeży dzień pracy daje deterministyczną (pustą) kolekcję Strefy do bezpiecznego odczytu.
        InTransaction(() =>
        {
            var p = Pracownik(Pracownik_.Andrzejewski);
            guidPrac = p.Guid;
            _ = p.DniPracy[Dzien] ?? Session.AddRow(new DzienPracy(p, Dzien));
        });
        SaveDispose();

        var p2 = Get<Prac>(guidPrac);
        var dzien = p2.DniPracy[Dzien];
        dzien.Should().NotBeNull("dzień ewidencji istnieje");
        dzien.Strefy.Should().NotBeNull("kolekcja stref pracy (Strefy) zawsze istnieje");

        System.Action odczyt = () =>
        {
            foreach (StrefaPracy s in dzien.Strefy.Cast<StrefaPracy>())
            {
                DefinicjaStrefy def = s.Definicja;   // strefa (np. praca zdalna)
                Time rozliczany = s.CzasRozliczany;  // czas rozliczany w strefie
                _ = def; _ = rozliczany;
            }
        };
        odczyt.Should().NotThrow("iteracja po strefach dnia i odczyt pól są bezpieczne");
    }

    [Test]
    [Description("KADRY-F5 (odczyt podzielników): pracownik.RozliczeniaCzasuPracy (dokumenty) oraz " +
                 "pracownik.ElementyRozliczeniaCzasuPracy (pozycje) to SubTable — kolekcje istnieją (mogą być puste " +
                 "w Demo). Element ma Definicja : DefinicjaStrefy i Czas : Time; odczyt nie rzuca. Budowy dokumentu " +
                 "rozliczenia nie testujemy — wymaga DefinicjaRozliczeniaCzasuPracy i przebiega przez extendery/UI.")]
    public void KADRY_F5_PodzielnikiRozliczeniaCzasuPracy_OdczytKolekcji_NieRzuca()
    {
        var p = Pracownik(Pracownik_.Andrzejewski);
        p.RozliczeniaCzasuPracy.Should().NotBeNull("kolekcja dokumentów rozliczenia czasu pracy istnieje");
        p.ElementyRozliczeniaCzasuPracy.Should().NotBeNull("kolekcja pozycji rozliczenia (podzielniki) istnieje");

        System.Action odczyt = () =>
        {
            foreach (ElementRozliczeniaCzasuPracy el in p.ElementyRozliczeniaCzasuPracy.Cast<ElementRozliczeniaCzasuPracy>())
            {
                DefinicjaStrefy def = el.Definicja;
                Time czas = el.Czas;
                _ = def; _ = czas;
            }
        };
        odczyt.Should().NotThrow("iteracja po pozycjach podzielnika i odczyt pól są bezpieczne");
    }

    [Test]
    [Description("KADRY-F5 (kontrakt typów): DefinicjaStrefy wystawia stałe Guid Praca_Zdalna / PracaZdalnaOkazjonalna " +
                 "(identyfikacja stref pracy zdalnej) oraz enum TypStrefy (NieWplywa/Zwieksza/Zmniejsza). " +
                 "Stałe są niepuste — to publiczne punkty zaczepienia rozliczenia pracy hybrydowej.")]
    public void KADRY_F5_DefinicjaStrefy_StalePracaZdalnaIEnumTypStrefy_SaDostepne()
    {
        DefinicjaStrefy.Praca_Zdalna.Should().NotBe(Guid.Empty, "stała identyfikuje strefę pracy zdalnej");
        DefinicjaStrefy.PracaZdalnaOkazjonalna.Should().NotBe(Guid.Empty, "stała identyfikuje strefę pracy zdalnej okazjonalnej");

        // Enum TypStrefy steruje wpływem strefy na rozliczenie czasu.
        System.Enum.IsDefined(typeof(TypStrefy), TypStrefy.NieWplywa).Should().BeTrue();
        System.Enum.IsDefined(typeof(TypStrefy), TypStrefy.Zwieksza).Should().BeTrue();
        System.Enum.IsDefined(typeof(TypStrefy), TypStrefy.Zmniejsza).Should().BeTrue();
    }
}
