using System;
using System.Linq;
using AwesomeAssertions;
using NUnit.Framework;
using Soneta.Business;
using Soneta.Kalend;
using Soneta.Types;
using Prac = Soneta.Kadry.Pracownik;

namespace Soneta.Skills.Test.KadryPlace.Pracownik;

/// <summary>
/// Rozdział E/F — „Plan pracy i kalendarz" (KADRY-E1, KADRY-E2) oraz „RCP — rejestracja czasu pracy" (KADRY-F1, KADRY-F2).
/// <para>
/// Testy są <b>wykonywalną dokumentacją</b> publicznego kontraktu platformy Soneta dla planu pracy
/// i rejestracji czasu. Model: pracownik wystawia trzy niezależne kolekcje dni typu
/// <see cref="DateSubTable"/> (indeksator po <see cref="Date"/>, tylko do odczytu — element tworzysz
/// konstruktorem + <c>AddRow</c>):
/// <list type="bullet">
/// <item><c>DniPlanu</c> — plan/harmonogram (dni <see cref="DzienPlanu"/> : <see cref="DzienKalendarzaBase"/>),</item>
/// <item><c>DniPracy</c> — ewidencja czasu pracy (<see cref="DzienPracy"/>),</item>
/// <item><c>DniRCP</c> — zarejestrowany (zweryfikowany) czas pracy (<see cref="DzienRCP"/>) — wynik importu RCP.</item>
/// </list>
/// Wszystkie dni współdzielą subrow <c>Praca : CzasPracy</c> z polami <c>OdGodziny</c>/<c>DoGodziny</c>/<c>Czas</c>.
/// Zdarzenia wejścia/wyjścia (<see cref="WejscieWyjscie"/>) są childem <see cref="DzienPracy"/> (kolekcja <c>WeWy</c>).
/// </para>
/// <para>
/// Operujemy wyłącznie na <b>publicznym kontrakcie</b> (jak dodatek zewnętrzny), na bazie Demo
/// (GoldStandard) z automatycznym rollbackiem. Daty Demo dla planu/pracy są nieznane, więc odczyty
/// istniejących danych traktujemy defensywnie (kolekcja istnieje / indeksator nie rzuca), a scenariusze
/// zapisu budujemy na własnych, jawnych datach dla pracownika "006".
/// </para>
/// </summary>
[TestFixture]
public class RozdzialEF_PlanRcpTest : PracownikTestBase
{
    // Data biznesowa do scenariuszy zapisu (jawna, nie Date.Today — data biznesowa Demo bywa inna).
    private static readonly Date Dzien = new(2026, 6, 1);

    /// <summary>
    /// Definicja dnia (typ dnia) ze słownika konfiguracyjnego <c>DefinicjeDni</c>. Demo zawiera kilka
    /// definicji; bierzemy pierwszą z brzegu (dowolny istniejący typ dnia), aby świeży dzień planu/pracy
    /// miał wymaganą <c>Definicja</c>. Skróty <c>WolnaSobota</c>/<c>Niedziela</c> też są dostępne.
    /// </summary>
    private DefinicjaDnia DowolnaDefinicjaDnia()
    {
        return Kalend.DefinicjeDni.Rows.Cast<DefinicjaDnia>().FirstOrDefault();
    }

    // ============================== KADRY-E1 — Plan pracy (harmonogram) ==============================

    [Test]
    [Description("KADRY-E1 (odczyt): DniPlanu to DateSubTable nietypowany (zwraca Row, rzutujemy na DzienPlanu); " +
                 "DniPlanu == Etat.Kalendarz.Dni; indeksator [Date] jest tylko do odczytu i zwraca null dla braku dnia.")]
    public void KADRY_E1_DniPlanu_OdczytIndeksatoremPoDacie_ZwracaDzienPlanuLubNull()
    {
        var p = Pracownik(Pracownik_.Andrzejewski);
        p.Should().NotBeNull("pracownik '006' istnieje w bazie Demo");

        // DniPlanu jest DateSubTable (nietypowany) — element zwracany jako Row, rzutujemy na DzienPlanu.
        p.DniPlanu.Should().NotBeNull("kolekcja planu (harmonogramu) zawsze istnieje");

        // Indeksator [Date] to odczyt — nie rzuca; dla daty bez dnia planu zwraca null.
        System.Action odczyt = () =>
        {
            var dp = (DzienPlanu)p.DniPlanu[Dzien];
            if (dp is not null)
            {
                // Godziny pracy leżą na subrowie Praca; Czas/OdGodziny na rootcie dnia są kalkulowane.
                Time _ = dp.Praca.OdGodziny;
                Time __ = dp.Czas;
                DefinicjaDnia ___ = dp.Definicja;
            }
        };
        odczyt.Should().NotThrow("indeksator [Date] na DniPlanu jest bezpiecznym odczytem");

        // DzienPlanu dziedziczy z DzienKalendarzaBase (dzień kalendarza pracownika).
        typeof(DzienKalendarzaBase).IsAssignableFrom(typeof(DzienPlanu))
            .Should().BeTrue("DzienPlanu jest dniem kalendarza (DzienKalendarzaBase)");
    }

    [Test]
    [Description("KADRY-E1 (zapis): nowy dzień planu tworzymy ctorem DzienPlanu(pracownik, data) + AddRow, " +
                 "ustawiamy Definicja (ze słownika DefinicjeDni) i godziny na subrowie Praca; po zapisie " +
                 "indeksator DniPlanu[data] zwraca utworzony dzień.")]
    public void KADRY_E1_UtworzenieDniaPlanu_UstawiaGodzinyNaSubrowiePraca()
    {
        var def = DowolnaDefinicjaDnia();
        def.Should().NotBeNull("Demo zawiera definicje dni (słownik DefinicjeDni)");

        Guid guidPrac = Guid.Empty;

        InTransaction(() =>
        {
            var p = Pracownik(Pracownik_.Andrzejewski);
            guidPrac = p.Guid;

            // Indeksator [Date] jest read-only — nowego dnia nie „przypiszemy", tworzymy ctorem.
            var dp = (DzienPlanu)p.DniPlanu[Dzien];
            if (dp is null)
            {
                dp = Session.AddRow(new DzienPlanu(p, Dzien));   // ctor (Pracownik, Date)
                dp.Definicja = def;                              // typ dnia ze słownika (wymagany dla weryfikatorów)
            }

            // Godziny ustawiamy na subrowie Praca; Czas dnia wylicza się z od–do.
            dp.Praca.OdGodziny = new Time(8, 0);
            dp.Praca.DoGodziny = new Time(16, 0);
        });
        SaveDispose();

        // Odczyt po zapisie: dzień planu istnieje na wskazanej dacie i ma ustawione godziny.
        var p2 = Get<Prac>(guidPrac);
        var dp2 = (DzienPlanu)p2.DniPlanu[Dzien];
        dp2.Should().NotBeNull("po zapisie dzień planu jest dostępny przez indeksator [Date]");
        dp2.Data.Should().Be(Dzien);
        dp2.Praca.OdGodziny.Should().Be(new Time(8, 0));
        dp2.Praca.DoGodziny.Should().Be(new Time(16, 0));
    }

    // ============================== KADRY-E2 — Kopiowanie planu / pracy (publiczne static) ==============================

    [Test]
    [Description("KADRY-E2: KalendarzPlanuKopia.Kopiuj(pracownik, okres) to publiczna metoda STATYCZNA " +
                 "(bez Context) — kopiuje wyliczony plan na okres do bufora DniPlanuKopia. Test wykonuje " +
                 "wywołanie w transakcji i sprawdza, że nie rzuca oraz że bufor DniPlanuKopia jest dostępny.")]
    public void KADRY_E2_KalendarzPlanuKopia_Kopiuj_StaticNaOkres_NieRzuca()
    {
        var okres = new FromTo(new Date(2026, 6, 1), new Date(2026, 6, 30));

        InTransaction(() =>
        {
            var p = Pracownik(Pracownik_.Andrzejewski);

            // Publiczny static Kopiuj(Pracownik, FromTo) — właściwa droga dla kodu serwerowego/testów
            // (worker KopiujWorker wymaga Context/zaznaczenia i jest gardzony licencją BI — patrz KADRY-E2).
            System.Action kopiuj = () => KalendarzPlanuKopia.Kopiuj(p, okres);
            kopiuj.Should().NotThrow("Kopiuj(Pracownik, FromTo) to publiczne statyczne API bez Context");

            // Kopia trafia do osobnego bufora DniPlanuKopia (DateSubTable), odrębnego od DniPlanu.
            p.DniPlanuKopia.Should().NotBeNull("bufor kopii planu (DniPlanuKopia) jest dostępny");
        });
        SaveDispose();
    }

    [Test]
    [Description("KADRY-E2: KalendarzPracyKopia.Kopiuj(pracownik, okres) — analogiczny publiczny static dla " +
                 "kopiowania realizacji (pracy) na okres; kopia trafia do bufora DniPracyKopia.")]
    public void KADRY_E2_KalendarzPracyKopia_Kopiuj_StaticNaOkres_NieRzuca()
    {
        var okres = new FromTo(new Date(2026, 6, 1), new Date(2026, 6, 30));

        InTransaction(() =>
        {
            var p = Pracownik(Pracownik_.Andrzejewski);

            System.Action kopiuj = () => KalendarzPracyKopia.Kopiuj(p, okres);
            kopiuj.Should().NotThrow("Kopiuj(Pracownik, FromTo) to publiczne statyczne API bez Context");

            p.DniPracyKopia.Should().NotBeNull("bufor kopii pracy (DniPracyKopia) jest dostępny");
        });
        SaveDispose();
    }

    // ============================== KADRY-F1 — Odczyt zarejestrowanego/ewidencjonowanego czasu ==============================

    [Test]
    [Description("KADRY-F1 (odczyt): DniPracy i DniRCP to DateSubTable TYPOWANE (DzienPracy / DzienRCP); " +
                 "indeksator [Date] zwraca właściwy typ lub null i nie rzuca. DzienRCP testujemy tylko " +
                 "ODCZYTOWO — jest wynikiem importu/weryfikacji RCP, nie tworzymy go ręcznie.")]
    public void KADRY_F1_DniPracyIDniRCP_OdczytIndeksatoremPoDacie_NieRzuca()
    {
        var p = Pracownik(Pracownik_.Andrzejewski);
        p.DniPracy.Should().NotBeNull("kolekcja ewidencji (DniPracy) istnieje");
        p.DniRCP.Should().NotBeNull("kolekcja zweryfikowanego RCP (DniRCP) istnieje");

        System.Action odczyt = () =>
        {
            // DniPracy jest typowane — indeksator [Date] zwraca DzienPracy lub null.
            DzienPracy dzienPracy = p.DniPracy[Dzien];
            if (dzienPracy is not null)
            {
                Time _ = dzienPracy.Praca.Czas;        // przepracowany czas dnia (subrow Praca)
                Time __ = dzienPracy.Praca.OdGodziny;
            }

            // DniRCP jest typowane — DzienRCP lub null; odczyt stanu weryfikacji RCP.
            DzienRCP dzienRcp = p.DniRCP[Dzien];
            if (dzienRcp is not null)
            {
                StanWeryfikacjiRCP ___ = dzienRcp.StanRCP;
                Time ____ = dzienRcp.Praca.Czas;
            }
        };
        odczyt.Should().NotThrow("indeksatory [Date] na DniPracy/DniRCP to bezpieczny odczyt");
    }

    [Test]
    [Description("KADRY-F1 (zapis ewidencji): dzień ewidencji tworzymy ctorem DzienPracy(pracownik, data) + AddRow " +
                 "(sam ctor nie rejestruje wiersza); godziny ustawiamy na subrowie Praca. Po zapisie " +
                 "DniPracy[data] zwraca utworzony dzień.")]
    public void KADRY_F1_UtworzenieDniaPracy_UstawiaGodzinyNaSubrowiePraca()
    {
        Guid guidPrac = Guid.Empty;

        InTransaction(() =>
        {
            var p = Pracownik(Pracownik_.Andrzejewski);
            guidPrac = p.Guid;

            var dp = p.DniPracy[Dzien];
            if (dp is null)
            {
                // ctor (Pracownik, Date) + AddRow — sam ctor nie włącza wiersza do tabeli.
                dp = Session.AddRow(new DzienPracy(p, Dzien));
            }
            dp.Praca.OdGodziny = new Time(8, 0);
            dp.Praca.DoGodziny = new Time(16, 0);
        });
        SaveDispose();

        var p2 = Get<Prac>(guidPrac);
        var dp2 = p2.DniPracy[Dzien];
        dp2.Should().NotBeNull("po zapisie dzień ewidencji jest dostępny przez indeksator [Date]");
        dp2.Data.Should().Be(Dzien);
        dp2.Praca.OdGodziny.Should().Be(new Time(8, 0));
        dp2.Praca.DoGodziny.Should().Be(new Time(16, 0));
    }

    // ============================== KADRY-F2 — Wejścia/wyjścia (zdarzenia RCP na dniu pracy) ==============================

    [Test]
    [Description("KADRY-F2: zdarzenie WejscieWyjscie jest childem DzienPracy — ctor WejscieWyjscie(dzienPracy) + " +
                 "AddRow do kalend.WejsciaWyjscia; ustawiamy Godzina i Typ (enum TypWejsciaWyjscia). " +
                 "Odczyt przez DzienPracy.WeWy (LpSubTable, posortowane po Lp).")]
    public void KADRY_F2_WejscieWyjscie_DodanieWejsciaIWyjscia_DoDniaPracy()
    {
        Guid guidPrac = Guid.Empty;

        InTransaction(() =>
        {
            var p = Pracownik(Pracownik_.Andrzejewski);
            guidPrac = p.Guid;

            // Najpierw potrzebny dzień ewidencji (właściciel zdarzeń we/wy).
            var dp = p.DniPracy[Dzien];
            if (dp is null)
                dp = Session.AddRow(new DzienPracy(p, Dzien));

            // Wejście 8:00 — ctor wiąże zdarzenie z dniem; AddRow do tabeli WejsciaWyjscia.
            var we = new WejscieWyjscie(dp);
            Kalend.WejsciaWyjscia.AddRow(we);
            we.Godzina = new Time(8, 0);
            we.Typ = TypWejsciaWyjscia.Wejscie;   // enum, nie string/int

            // Wyjście 16:00.
            var wy = new WejscieWyjscie(dp);
            Kalend.WejsciaWyjscia.AddRow(wy);
            wy.Godzina = new Time(16, 0);
            wy.Typ = TypWejsciaWyjscia.Wyjscie;
        });
        SaveDispose();

        // Odczyt zdarzeń dnia przez kolekcję WeWy (LpSubTable — kolejność wg Lp).
        var p2 = Get<Prac>(guidPrac);
        var dzien = p2.DniPracy[Dzien];
        dzien.Should().NotBeNull("dzień ewidencji z dodanymi zdarzeniami istnieje");

        var zdarzenia = dzien.WeWy.Cast<WejscieWyjscie>().OrderBy(w => w.Lp).ToList();
        zdarzenia.Should().HaveCount(2, "dodaliśmy wejście i wyjście");
        zdarzenia.Should().Contain(w => w.Typ == TypWejsciaWyjscia.Wejscie && w.Godzina == new Time(8, 0));
        zdarzenia.Should().Contain(w => w.Typ == TypWejsciaWyjscia.Wyjscie && w.Godzina == new Time(16, 0));
        // Dzien (właściciel) ustawiony przez ctor — wszystkie zdarzenia wskazują nasz dzień pracy.
        zdarzenia.Should().OnlyContain(w => w.Dzien.Data == Dzien);
    }
}
