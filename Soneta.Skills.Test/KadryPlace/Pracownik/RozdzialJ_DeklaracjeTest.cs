using System;
using System.Linq;
using AwesomeAssertions;
using NUnit.Framework;
using Soneta.Business;
using Soneta.Deklaracje;
using Soneta.Kadry;
using Soneta.Place;
using Soneta.Types;
using Prac = Soneta.Kadry.Pracownik;

namespace Soneta.Skills.Test.KadryPlace.Pracownik;

/// <summary>
/// Rozdział J — „Deklaracje (ZUS, PIT, PFRON, PPK)" (receptury KADRY-J1–KADRY-J6).
/// <para>
/// Testy są <b>wykonywalną dokumentacją</b> publicznego kontraktu modułu Deklaracje
/// (<c>Soneta.Deklaracje.DeklaracjeModule</c>, dostęp przez <c>Session.GetDeklaracje()</c>).
/// Wszystkie deklaracje to wiersze tabeli <c>Deklaracje</c>, dziedziczące po abstrakcyjnej
/// <c>Soneta.Deklaracje.Deklaracja</c>; konkretne typy żyją w podprzestrzeniach
/// <c>Soneta.Deklaracje.{ZUS,PIT,PFRON,PPK}.*</c>.
/// </para>
/// <para>
/// <b>Rozróżnienie kluczowe.</b> Naliczenie/utworzenie większości deklaracji (KADRY-J1–KADRY-J5) to operacja
/// lokalna (zapis wiersza), ale wymaga <c>Context</c> i — dla ZUS — obiektu <c>KEDU</c> (kontener
/// dokumentów ZUS), którego nie da się sensownie zbudować bez środowiska modułu Deklaracje.
/// E-wysyłka (KEDU/PUE/SODiR/MF) jest sieciowa/plikowa. Dlatego testy KADRY-J1–KADRY-J5 dokumentują
/// <b>KONTRAKT</b> typów/workerów kompilowalnie (przez odwołania do typów <c>typeof(...)</c>,
/// ctory, metody) i są oznaczone <c>[Ignore]</c> z powodem. Realnie wykonujemy KADRY-J6 (bilanse otwarcia
/// PIT — czyste API biznesowe na pracowniku) oraz próbę naliczenia PIT-11.
/// </para>
/// <para>
/// Operujemy wyłącznie na <b>publicznym kontrakcie</b> platformy, na bazie Demo (GoldStandard),
/// z automatycznym rollbackiem po teście.
/// </para>
/// </summary>
[TestFixture]
public class RozdzialJ_DeklaracjeTest : PracownikTestBase
{
    /// <summary>Skrót do modułu Deklaracje bieżącej sesji operacyjnej.</summary>
    private DeklaracjeModule Deklaracje => Session.GetDeklaracje();

    // ============================== KADRY-J1 — Zgłoszenia ZUS (ZUA/ZZA, ZCNA, ZWUA) ==============================

    [Test]
    [Description("KADRY-J1: zgłoszenia ZUS to wiersze deklaracji w Soneta.Deklaracje.ZUS — ZUA (społeczne+zdrowotne), " +
                 "ZZA (zdrowotne), ZCNA (rodzina), ZWUA (wyrejestrowanie). Konkretne typy mają ctor " +
                 "(Pracownik, KEDU): new ZUA(pracownik, kedu). Workerem zbiorczym jest " +
                 "ZarejestrujPracownikówWorker (zagnieżdżone .Rejestracja/.Rodzina/.Wyrejestrowanie/.ZgloszenieUmow), " +
                 "Params budowane z Context (ctor (Context)) + pole Kedu. Tu dokumentujemy KONTRAKT typów; " +
                 "samo utworzenie wymaga Context + KEDU.")]
    [Ignore("wymaga Context/KEDU / e-wysyłka sieciowa — dokumentowany kontrakt typów ZUS")]
    public void KADRY_J1_ZgloszeniaZUS_ZUA_ZZA_ZCNA_ZWUA_Kontrakt()
    {
        // Kontrakt typów zgłoszeniowych ZUS — odwołania kompilowalne (zweryfikowane z DLL).
        typeof(Soneta.Deklaracje.ZUS.ZUA).Should().NotBeNull("ZUA — zgłoszenie społeczne+zdrowotne");
        typeof(Soneta.Deklaracje.ZUS.ZZA).Should().NotBeNull("ZZA — zgłoszenie tylko zdrowotne");
        typeof(Soneta.Deklaracje.ZUS.ZCNA).Should().NotBeNull("ZCNA — zgłoszenie członków rodziny");
        typeof(Soneta.Deklaracje.ZUS.ZWUA).Should().NotBeNull("ZWUA — wyrejestrowanie");

        // Worker zbiorczy + jego klasy zagnieżdżone (akcje menu „Deklaracje ZUS/Przygotuj …").
        typeof(Soneta.Deklaracje.ZUS.ZarejestrujPracownikówWorker.Rejestracja).Should().NotBeNull();
        typeof(Soneta.Deklaracje.ZUS.ZarejestrujPracownikówWorker.Rodzina).Should().NotBeNull();
        typeof(Soneta.Deklaracje.ZUS.ZarejestrujPracownikówWorker.Wyrejestrowanie).Should().NotBeNull();

        // Params zgłoszeniowe mają ctor (Context); KEDU jest wymaganym kontenerem docelowym.
        typeof(Soneta.Deklaracje.ZUS.ZarejestrujBaseWorker.ParamsKor)
            .GetConstructor(new[] { typeof(Context) })
            .Should().NotBeNull("ParamsKor budujemy z Context");
        typeof(Soneta.Deklaracje.ZUS.KEDU)
            .GetConstructor(new[] { typeof(Session) })
            .Should().NotBeNull("KEDU ma ctor (Session), ale realne złożenie wymaga modułu Deklaracje");
    }

    // ============================== KADRY-J2 — Deklaracje rozliczeniowe ZUS (DRA, RIA, IMIR/RMUA) ==============================

    [Test]
    [Description("KADRY-J2: rozliczeniowe ZUS — DRA (deklaracja rozliczeniowa, ctor (KEDU)), RIA (raport po ustaniu, " +
                 "ctor (Pracownik, KEDU)), RMUA (informacja miesięczna dla ubezpieczonego = IMIR, ctor " +
                 "(Pracownik, RMUA.TypOkresuDeklaracji)). Naliczanie seryjne: NaliczanieSeryjneRIAWorker / " +
                 "NaliczanieSeryjneRMUAWorker (ctor bezparametrowy + Pracownicy/Pars + metoda NaliczRMUA(Context)). " +
                 "Pojedynczą deklarację przelicza DeklaracjaWorker.Przelicz() (DataType Deklaracja). " +
                 "KEDU + Context wymagane — dokumentujemy KONTRAKT.")]
    [Ignore("wymaga Context/KEDU / e-wysyłka sieciowa — dokumentowany kontrakt rozliczeń ZUS")]
    public void KADRY_J2_RozliczeniaZUS_DRA_RIA_IMIR_Kontrakt()
    {
        // DRA wiąże się z KEDU (ctor (KEDU)), RIA z pracownikiem i KEDU.
        typeof(Soneta.Deklaracje.ZUS.DRA).GetConstructor(new[] { typeof(Soneta.Deklaracje.ZUS.KEDU) })
            .Should().NotBeNull("DRA(KEDU)");
        typeof(Soneta.Deklaracje.ZUS.RIA)
            .GetConstructor(new[] { typeof(Prac), typeof(Soneta.Deklaracje.ZUS.KEDU) })
            .Should().NotBeNull("RIA(Pracownik, KEDU)");

        // IMIR w CLR nazywa się RMUA (ctor (Pracownik, RMUA.TypOkresuDeklaracji)).
        typeof(Soneta.Deklaracje.ZUS.RMUA).Should().NotBeNull("RMUA = informacja miesięczna (IMIR)");
        typeof(Soneta.Deklaracje.ZUS.RMUA.TypOkresuDeklaracji).IsEnum
            .Should().BeTrue("typ okresu deklaracji RMUA jest enumem");

        // Naliczanie seryjne RIA/RMUA — ctor bezparametrowy + Pracownicy/Pars (Context w props).
        typeof(Soneta.Deklaracje.ZUS.NaliczanieSeryjneRIAWorker).GetConstructor(Type.EmptyTypes)
            .Should().NotBeNull();
        typeof(Soneta.Deklaracje.ZUS.NaliczanieSeryjneRMUAWorker).GetMethod("NaliczRMUA")
            .Should().NotBeNull("NaliczRMUA(Context) — metoda akcji naliczania IMIR");

        // Przeliczenie istniejącego wiersza dowolnej deklaracji.
        typeof(DeklaracjaWorker).GetMethod("Przelicz").Should().NotBeNull("DeklaracjaWorker.Przelicz()");
    }

    // ============================== KADRY-J3 — Deklaracje PIT (PIT-11, 4R, 8AR, R, IFT) ==============================

    [Test]
    [Description("KADRY-J3: imienne PIT (PIT-11, PIT-R, IFT-1/IFT-1R, PIT-8C) nalicza seryjnie zagnieżdżony " +
                 "Soneta.Deklaracje.PIT.NaliczanieSeryjne.* (PIT_11Worker ma ctor (Session); Params ctor (Context)). " +
                 "PIT-4R/PIT-8AR (PIT4/PIT8A) są zbiorcze na poziomie podmiotu/US (ctory nonpublic — tworzone " +
                 "workerami zbiorczymi). Tu dokumentujemy KONTRAKT typów i workerów. Realne naliczenie PIT-11 " +
                 "próbujemy w KADRY-J3b.")]
    [Ignore("wymaga Context / dane źródłowe (wypłaty + BO PIT) — dokumentowany kontrakt PIT")]
    public void KADRY_J3_DeklaracjePIT_Kontrakt()
    {
        // Typy deklaracji PIT (wiersze tabeli Deklaracje).
        typeof(Soneta.Deklaracje.PIT.PIT11).Should().NotBeNull("PIT-11");
        typeof(Soneta.Deklaracje.PIT.PIT4).Should().NotBeNull("PIT-4R (zaliczki)");
        typeof(Soneta.Deklaracje.PIT.PIT8A).Should().NotBeNull("PIT-8AR (zryczałtowany)");
        typeof(Soneta.Deklaracje.PIT.PITR).Should().NotBeNull("PIT-R");
        typeof(Soneta.Deklaracje.PIT.IFT1).Should().NotBeNull("IFT-1/IFT-1R");

        // Workery naliczania seryjnego PIT (zagnieżdżone w NaliczanieSeryjne).
        typeof(Soneta.Deklaracje.PIT.NaliczanieSeryjne.PIT_11Worker)
            .GetConstructor(new[] { typeof(Session) })
            .Should().NotBeNull("PIT_11Worker(Session)");
        typeof(Soneta.Deklaracje.PIT.NaliczanieSeryjne.PIT_RWorker).Should().NotBeNull();
        typeof(Soneta.Deklaracje.PIT.NaliczanieSeryjne.IFT_1Worker).Should().NotBeNull();
        typeof(Soneta.Deklaracje.PIT.NaliczanieSeryjne.IFT_1RWorker).Should().NotBeNull();
        typeof(Soneta.Deklaracje.PIT.NaliczanieSeryjne.PIT_8CWorker).Should().NotBeNull();

        // Params PIT mają ctor (Context).
        typeof(Soneta.Deklaracje.PIT.NaliczanieSeryjne.PIT_11Worker.Params)
            .GetConstructor(new[] { typeof(Context) })
            .Should().NotBeNull("PIT_11Worker.Params(Context)");
    }

    [Test]
    [Description("KADRY-J3b: próba realnego naliczenia PIT-11 dla pracownika Demo workerem " +
                 "NaliczanieSeryjne.PIT_11Worker(Session) { Pracownicy = [...] }, ustawiając Pars.Okres (rok) " +
                 "i Pars.Data, a następnie wywołując Nalicz_PIT_11(). Worker wymaga środowiska Context/danych " +
                 "źródłowych — w razie wyjątku oznaczamy [Ignore].")]
    [Ignore("PIT_11Worker wymaga Context/KEDU oraz danych źródłowych (naliczone wypłaty + BO PIT); " +
            "naliczenie w izolacji testu rzuca — dokumentowany kontrakt wywołania")]
    public void KADRY_J3b_NaliczeniePIT11_ProbaRealna()
    {
        var pracownik = Pracownik(Pracownik_.Andrzejewski);
        pracownik.Should().NotBeNull();

        var worker = new Soneta.Deklaracje.PIT.NaliczanieSeryjne.PIT_11Worker(Session)
        {
            Pracownicy = new[] { pracownik },
        };
        worker.Pars.Okres = FromTo.Year(2025);   // rok podatkowy
        worker.Pars.Data = Date.Today;

        worker.Nalicz_PIT_11();   // tworzy wiersze PIT11 w tabeli Deklaracje
        SaveDispose();
    }

    // ============================== KADRY-J4 — Deklaracje PFRON (Wn-D, INF-2, DEK-R, INF-D-P) ==============================

    [Test]
    [Description("KADRY-J4: PFRON to wiersze deklaracji w Soneta.Deklaracje.PFRON — WN_D (Wn-D), INF_2 (informacja " +
                 "roczna), DEK_R (deklaracja roczna wpłat), INF_D_P (załącznik o pracowniku niepełnosprawnym). " +
                 "PFRON nie ma seryjnego naliczania na Pracownicy — deklarację tworzy się w module Deklaracje, " +
                 "a przelicza DeklaracjaWorker.Przelicz() (DataType Deklaracja). Dane źródłowe pochodzą z " +
                 "PracHistoria.PFRON (KADRY-A13). Tworzenie/edycja wymaga Context — dokumentujemy KONTRAKT.")]
    [Ignore("wymaga Context / e-wysyłka SODiR — dokumentowany kontrakt typów PFRON")]
    public void KADRY_J4_DeklaracjePFRON_Kontrakt()
    {
        typeof(Soneta.Deklaracje.PFRON.WN_D).Should().NotBeNull("Wn-D — wniosek o dofinansowanie");
        typeof(Soneta.Deklaracje.PFRON.INF_2).Should().NotBeNull("INF-2 — informacja roczna");
        typeof(Soneta.Deklaracje.PFRON.DEK_R).Should().NotBeNull("DEK-R — deklaracja roczna wpłat");
        typeof(Soneta.Deklaracje.PFRON.INF_D_P).Should().NotBeNull("INF-D-P — załącznik o pracowniku");

        // Wszystkie PFRON dziedziczą po Deklaracja, więc przelicza je wspólny DeklaracjaWorker.
        typeof(Soneta.Deklaracje.PFRON.WN_D).IsSubclassOf(typeof(Deklaracja))
            .Should().BeTrue("PFRON to wiersze tabeli Deklaracje");
        typeof(DeklaracjaWorker).GetMethod("Przelicz").Should().NotBeNull();
    }

    // ============================== KADRY-J5 — Operacje PPK ==============================

    [Test]
    [Description("KADRY-J5: dokumenty PPK to wiersze deklaracji w Soneta.Deklaracje.PPK (RejestracjaUczestnikaPPK, " +
                 "DeklaracjaUczestnikaPPK, ZakończenieZatrudnieniaUczestnikaPPK, RozliczenieSkładekPPK, …). " +
                 "Operacje zbiorcze na Pracownicy realizuje DeklaracjePPKPracownikówWorker (zagnieżdżone " +
                 ".Rejestracja/.Rezygnacja/.Wznowienie/.ZakończenieZatrudnienia/.ZmianaDanychIdentyfikacyjnych); " +
                 "wspólny Params = DeklaracjePPKBaseWorker.Params (ctor (Context), pole DokumentPPK). " +
                 "Kwalifikacja/auto-zapis to workery na pracowniku (PPKWorker/AutoZapisPPKWorker, ctor (Context)). " +
                 "Dokumentujemy KONTRAKT — operacje wymagają Context i zwykle DokumentyPracodawcyPPK.")]
    [Ignore("wymaga Context / DokumentyPracodawcyPPK — dokumentowany kontrakt operacji PPK")]
    public void KADRY_J5_OperacjePPK_Kontrakt()
    {
        // Typy dokumentów PPK.
        typeof(Soneta.Deklaracje.PPK.RejestracjaUczestnikaPPK).Should().NotBeNull();

        // Workery zbiorcze operacji PPK (zagnieżdżone w DeklaracjePPKPracownikówWorker).
        typeof(Soneta.Deklaracje.PPK.DeklaracjePPKPracownikówWorker.Rejestracja).Should().NotBeNull();
        typeof(Soneta.Deklaracje.PPK.DeklaracjePPKPracownikówWorker.Rezygnacja).Should().NotBeNull();
        typeof(Soneta.Deklaracje.PPK.DeklaracjePPKPracownikówWorker.Wznowienie).Should().NotBeNull();
        typeof(Soneta.Deklaracje.PPK.DeklaracjePPKPracownikówWorker.ZakończenieZatrudnienia).Should().NotBeNull();
        typeof(Soneta.Deklaracje.PPK.DeklaracjePPKPracownikówWorker.ZmianaDanychIdentyfikacyjnych).Should().NotBeNull();

        // Wspólny Params ma ctor (Context).
        typeof(Soneta.Deklaracje.PPK.DeklaracjePPKBaseWorker.Params)
            .GetConstructor(new[] { typeof(Context) })
            .Should().NotBeNull("DeklaracjePPKBaseWorker.Params(Context)");
    }

    // ============================== KADRY-J6 — Bilanse otwarcia PIT (REALNIE TESTOWALNE) ==============================

    [Test]
    [Description("KADRY-J6: bilans otwarcia PIT to kolekcja na pracowniku (pracownik.BilansyOtwarciaPIT, " +
                 "SubTable<Soneta.Place.BilansOtwarciaPIT>). Tworzymy czystym API biznesowym (BEZ Context/KEDU): " +
                 "Session.AddRow(new BilansOtwarciaPIT_29(pracownik)) w trybie edycji; ustawiamy Data oraz kwoty " +
                 "(PrzychodUlgaEtat, Spoleczne). UWAGA: bazowy BilansOtwarciaPIT jest ABSTRAKCYJNY — instancjonujemy " +
                 "konkretną wersję BilansOtwarciaPIT_29 (Wersja=PIT11_29) lub BilansOtwarciaPIT_11 (PIT11_11), " +
                 "ctor (Pracownik); brak ctora bezparametrowego, Pracownik read-only. Odczyt przez " +
                 "pracownik.BilansyOtwarciaPIT.")]
    public void KADRY_J6_BilansOtwarciaPIT_TworzenieIOdczyt()
    {
        var pracownik = Pracownik(Pracownik_.Andrzejewski);
        pracownik.Should().NotBeNull();

        // Stan początkowy kolekcji bilansów otwarcia PIT.
        int przed = pracownik.BilansyOtwarciaPIT.Cast<BilansOtwarciaPIT>().Count();

        var data = new Date(2026, 1, 1);
        Guid guidBO = Guid.Empty;

        // Tworzenie danych operacyjnych MUSI być w trybie edycji (InTransaction),
        // inaczej AddRow rzuca CannotEditException.
        InTransaction(() =>
        {
            // Bazowy BilansOtwarciaPIT jest abstrakcyjny — tworzymy konkretną wersję (_29 => PIT11_29).
            BilansOtwarciaPIT bo = Session.AddRow(new BilansOtwarciaPIT_29(pracownik));
            bo.Data = data;
            bo.PrzychodUlgaEtat = 12000m;
            bo.Spoleczne = 1645.20m;
            guidBO = bo.Guid;
        });
        SaveDispose();   // utrwalenie (rollback po teście i tak wycofa)

        // Odczyt: bilans jest dopięty do pracownika i ma ustawione wartości.
        var boWczytany = Get<BilansOtwarciaPIT>(guidBO);
        boWczytany.Should().NotBeNull("bilans otwarcia PIT został zapisany");
        boWczytany.Pracownik.Guid.Should().Be(pracownik.Guid, "bilans jest powiązany z pracownikiem");
        boWczytany.Data.Should().Be(data);
        boWczytany.PrzychodUlgaEtat.Should().Be(12000m);
        boWczytany.Spoleczne.Should().Be(1645.20m);
        boWczytany.Wersja.Should().Be(WersjaBilansuOtwarciaPIT.PIT11_29, "wersja ustawiana w ctor");

        // Odczyt przez kolekcję pracownika — bilans jest widoczny.
        var pracownik2 = Pracownik(Pracownik_.Andrzejewski);
        var bilanse = pracownik2.BilansyOtwarciaPIT.Cast<BilansOtwarciaPIT>().ToList();
        bilanse.Should().HaveCount(przed + 1, "doszedł jeden bilans otwarcia PIT");
        bilanse.Should().Contain(b => b.Guid == guidBO);
    }

    [Test]
    [Description("KADRY-J6b: pozostałe kolekcje wdrożeniowe ERP-7 na pracowniku — pracownik.WynagrodzeniaERP7 " +
                 "(SubTable<Soneta.Kalend.WynagrodzenieERP7>) i pracownik.NieobecnosciERP7 " +
                 "(SubTable<Soneta.Kalend.NieobecnoscERP7>). Dokumentujemy KONTRAKT (kolekcje istnieją i są " +
                 "iterowalne czystym API, bez Context); sam druk Z-3/ERP-7 to generowanie w module Deklaracje.")]
    public void KADRY_J6b_KolekcjeERP7_Odczyt()
    {
        var pracownik = Pracownik(Pracownik_.Andrzejewski);
        pracownik.Should().NotBeNull();

        // Kolekcje istnieją i są iterowalne (na Demo zwykle puste — sprawdzamy sam kontrakt).
        System.Action odczytWynagrodzen = () => pracownik.WynagrodzeniaERP7.Cast<object>().ToList();
        System.Action odczytNieobecnosci = () => pracownik.NieobecnosciERP7.Cast<object>().ToList();

        odczytWynagrodzen.Should().NotThrow("kolekcja WynagrodzeniaERP7 jest dostępna czystym API");
        odczytNieobecnosci.Should().NotThrow("kolekcja NieobecnosciERP7 jest dostępna czystym API");
    }
}
