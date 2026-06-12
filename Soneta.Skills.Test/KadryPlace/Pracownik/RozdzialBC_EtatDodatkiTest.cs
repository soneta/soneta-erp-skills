using System;
using System.Linq;
using AwesomeAssertions;
using NUnit.Framework;
using Soneta.Business;
using Soneta.Kadry;
using Soneta.Place;
using Soneta.Types;
using Prac = Soneta.Kadry.Pracownik;

namespace Soneta.Skills.Test.KadryPlace.Pracownik;

/// <summary>
/// Rozdział B+C — „Etat (umowa o pracę)" i „Dodatki / stałe elementy wynagrodzenia"
/// (receptury KADRY-B1 z <c>kadry/KADRY02-etat.md</c> i KADRY-C1 z <c>kadry/KADRY03-dodatki-potracenia.md</c>).
/// <para>
/// Testy są <b>wykonywalną dokumentacją</b> publicznego kontraktu platformy Soneta. Pokazują:
/// <list type="bullet">
/// <item><b>KADRY-B1</b> — warunki etatu siedzą w subrowie <c>PracHistoria.Etat</c>; stawkę ustawiamy na
/// subrowie <c>Etat.Zaszeregowanie</c> w wymaganej KOLEJNOŚCI (najpierw <c>RodzajStawki</c>, potem
/// <c>Wymiar</c>) — odwrócenie kolejności rzuca <see cref="ColReadOnlyException"/>;</item>
/// <item><b>KADRY-C1</b> — dodatek (stały element wynagrodzenia) jest obiektem historycznym; tworzymy go
/// przez <c>new Dodatek(pracownik)</c> + <c>Kadry.Dodatki.AddRow</c>, a parametry (Element, Okres)
/// ustawiamy na pierwszym zapisie <c>d.Last</c>.</item>
/// </list>
/// </para>
/// <para>
/// Wszystko działa na bazie Demo (GoldStandard) z automatycznym rollbackiem po teście. Operujemy
/// wyłącznie na <b>publicznym kontrakcie</b> — tak jak dodatek programisty zewnętrznego bez dostępu
/// do kodu źródłowego aplikacji.
/// </para>
/// </summary>
[TestFixture]
public class RozdzialBC_EtatDodatkiTest : PracownikTestBase
{
    // ============================== KADRY-B1 — Definiowanie etatu (umowa o pracę) ==============================

    [Test]
    [Description("KADRY-B1: warunki etatu ustawiamy na subrowie Etat zapisu historii. KOLEJNOŚĆ: najpierw " +
                 "Etat.Okres (odblokowuje pozostałe pola etatu), potem TypUmowy/Podstawa/Stanowisko/Wydzial " +
                 "oraz stawka na subrowie Zaszeregowanie. Wydzial to referencja do korzenia (Wydzialy.Firma).")]
    public void KADRY_B1_DefiniowanieEtatu_NaNowymPracowniku_UstawiaWarunkiIStawke()
    {
        Guid guid = Guid.Empty;
        var kod = "B1_" + Guid.NewGuid().ToString("N").Substring(0, 6);
        var okres = new FromTo(new Date(2026, 1, 1), Date.MaxValue);

        InTransaction(() =>
        {
            // KADRY-A1: AddRow tworzy pierwszy zapis historii (Last) + kalendarz — warunki etatu ustawiamy
            // na Etat tego pierwszego zapisu.
            var pracownik = Session.AddRow(new PracownikFirmy());
            pracownik.Kod = kod;
            pracownik.Last.Nazwisko = "Etatowy";
            pracownik.Last.Imie = "Robert";

            // Etat to SUBROW zapisu PracHistoria — modyfikujemy jego pola, nie przypisujemy obiektu.
            var etat = pracownik.Last.Etat;

            // KLUCZOWA KOLEJNOŚĆ: na świeżym (auto-utworzonym) zapisie cały Etat jest read-only,
            // dopóki nie ustawimy zakresu zatrudnienia Etat.Okres. Okres MUSI być pierwszy —
            // dopiero on odblokowuje TypUmowy/Podstawa/Stanowisko/Zaszeregowanie.
            etat.Okres = okres;                                   // FromTo, nie DateTime — USTAWIAMY PIERWSZE
            etat.TypUmowy = TypUmowyOPrace.NaCzasNieokreślony;   // enum, nie string
            etat.Podstawa = StosPracyNaPodstawie.UmowyOPrace;     // podstawa stosunku pracy (enum)
            etat.DataZawarcia = new Date(2025, 12, 20);
            etat.DataRozpPracy = new Date(2026, 1, 1);
            etat.Stanowisko = "Specjalista";
            etat.Wydzial = Kadry.Wydzialy.Firma;                  // referencja do istniejącego wydziału (korzeń)

            // Stawka — subrow Zaszeregowanie. Po ustawieniu Etat.Okres wszystkie pola stawki są
            // zapisywalne; ustawiamy je w czytelnej kolejności RodzajStawki -> TypStawki -> Wymiar -> Stawka.
            var z = etat.Zaszeregowanie;
            z.RodzajStawki = RodzajStawkiZaszeregowania.Miesieczna;   // rodzaj stawki
            z.TypStawki = TypStawkiZaszeregowania.Dowolna;           // typ stawki
            z.Wymiar = Fraction.One;                                 // pełny etat
            z.Stawka = (Currency)6000m;                              // kwota brutto miesięcznie

            guid = pracownik.Guid;
        });
        SaveDispose();

        // Odczyt na świeżej sesji po Guid — potwierdza utrwalenie warunków etatu i stawki.
        var etat2 = Get<Prac>(guid).Last.Etat;
        etat2.TypUmowy.Should().Be(TypUmowyOPrace.NaCzasNieokreślony);
        etat2.Podstawa.Should().Be(StosPracyNaPodstawie.UmowyOPrace);
        etat2.Stanowisko.Should().Be("Specjalista");
        etat2.Wydzial.Should().NotBeNull("Wydzial wskazuje na istniejący wydział (korzeń struktury)");
        // FromTo implementuje IEnumerable<Date> — porównujemy granice okresu, nie cały obiekt.
        etat2.Okres.From.Should().Be(okres.From);

        var z2 = etat2.Zaszeregowanie;
        z2.RodzajStawki.Should().Be(RodzajStawkiZaszeregowania.Miesieczna);
        z2.Wymiar.Should().Be(Fraction.One, "pełny etat");
        z2.Stawka.Should().Be((Currency)6000m, "kwota brutto miesięcznie");
    }

    [Test]
    [Description("KADRY-B1 (pułapka kolejności): na świeżym zapisie historii cały Etat jest tylko-do-odczytu " +
                 "dopóki nie ustawimy Etat.Okres. Próba ustawienia TypUmowy/RodzajStawki/Wymiar PRZED " +
                 "Etat.Okres rzuca ColReadOnlyException; po ustawieniu Okres pola stają się zapisywalne.")]
    public void KADRY_B1_Pulapka_PolaEtatuReadOnlyDopokiNieUstawionoOkresu()
    {
        InTransaction(() =>
        {
            var pracownik = Session.AddRow(new PracownikFirmy());
            pracownik.Kod = "B1x_" + Guid.NewGuid().ToString("N").Substring(0, 6);
            pracownik.Last.Nazwisko = "Pulapka";
            pracownik.Last.Imie = "Karol";

            var etat = pracownik.Last.Etat;

            // PRZED ustawieniem Etat.Okres pola etatu są tylko-do-odczytu — przypisanie rzuca wyjątek.
            System.Action typUmowyPrzedOkresem = () => etat.TypUmowy = TypUmowyOPrace.NaCzasNieokreślony;
            typUmowyPrzedOkresem.Should().Throw<ColReadOnlyException>(
                "TypUmowy jest read-only dopóki nie ustawiono Etat.Okres");

            System.Action rodzajStawkiPrzedOkresem = () => etat.Zaszeregowanie.RodzajStawki = RodzajStawkiZaszeregowania.Miesieczna;
            rodzajStawkiPrzedOkresem.Should().Throw<ColReadOnlyException>(
                "Zaszeregowanie.RodzajStawki też jest read-only przed Etat.Okres");

            System.Action wymiarPrzedOkresem = () => etat.Zaszeregowanie.Wymiar = new Fraction(1, 2);
            wymiarPrzedOkresem.Should().Throw<ColReadOnlyException>(
                "Zaszeregowanie.Wymiar też jest read-only przed Etat.Okres");

            // Ustawienie Etat.Okres ODBLOKOWUJE pozostałe pola etatu i stawki.
            etat.Okres = new FromTo(new Date(2026, 1, 1), Date.MaxValue);

            System.Action poOkresie = () =>
            {
                etat.TypUmowy = TypUmowyOPrace.NaCzasNieokreślony;
                etat.Zaszeregowanie.RodzajStawki = RodzajStawkiZaszeregowania.Miesieczna;
                etat.Zaszeregowanie.Wymiar = new Fraction(1, 2);
            };
            poOkresie.Should().NotThrow("po ustawieniu Etat.Okres pola etatu i stawki są zapisywalne");
            etat.Zaszeregowanie.Wymiar.Should().Be(new Fraction(1, 2), "½ etatu");

            // Nie commitujemy realnych danych — pracownik bez kompletnych warunków;
            // mechanizm testów i tak wycofuje transakcję, ale dla jasności nie utrwalamy.
        });
    }

    // ============================== KADRY-C1 — Dodatki / stałe elementy wynagrodzenia ==============================

    [Test]
    [Description("KADRY-C1: dodatek tworzymy przez new Dodatek(pracownik) + Kadry.Dodatki.AddRow (para); " +
                 "AddRow tworzy pierwszy zapis DodHistoria (d.Last), na którym ustawiamy Element " +
                 "(z Place.DefElementow.WgNazwy[\"Premia\"]) oraz Okres. Odczyt z pracownik.Dodatki.")]
    public void KADRY_C1_Dodatek_TworzonyZDefinicjaElementu_IOkresem()
    {
        // Definicja elementu wynagrodzenia ze słownika KONFIGURACYJNEGO (po nazwie).
        // W bazie Demo istnieje gotowa definicja "Premia".
        var definicjaPremii = Place.DefElementow.WgNazwy["Premia"] as DefinicjaElementu;
        definicjaPremii.Should().NotBeNull("baza Demo zawiera definicję elementu \"Premia\"");

        Guid guidPrac = Guid.Empty;
        var okres = new FromTo(new Date(2026, 1, 1), Date.MaxValue);

        InTransaction(() =>
        {
            // Tworzymy świeżego pracownika z etatem (świeży = nie ma jeszcze żadnych dodatków,
            // w odróżnieniu od pracowników z Demo, którym już przypisano premie/składki).
            var pracownik = Session.AddRow(new PracownikFirmy());
            pracownik.Kod = "C1_" + Guid.NewGuid().ToString("N").Substring(0, 6);
            pracownik.Last.Nazwisko = "Premiowany";
            pracownik.Last.Imie = "Lucjan";
            // Etat.Okres najpierw — odblokowuje warunki etatu (patrz KADRY-B1). Po ustawieniu Okres
            // weryfikator wymaga jednostki organizacyjnej (Wydzial) przy Save.
            pracownik.Last.Etat.Okres = okres;
            pracownik.Last.Etat.Wydzial = Kadry.Wydzialy.Firma;
            pracownik.Last.Etat.Stanowisko = "Specjalista";

            // new Dodatek(pracownik) + AddRow — PARA. Sam ctor nie włącza dodatku do sesji ani
            // nie tworzy zapisu historii; pierwszy DodHistoria powstaje przy AddRow.
            var dodatek = new Dodatek(pracownik);
            Kadry.Dodatki.AddRow(dodatek);

            // Parametry ustawiamy na pierwszym zapisie historii dodatku (d.Last).
            var h = dodatek.Last;
            h.Should().NotBeNull("AddRow tworzy pierwszy zapis DodHistoria (Last)");
            h.Element = definicjaPremii;   // definicja elementu (wymagana)
            h.Okres = okres;

            guidPrac = pracownik.Guid;
        });
        SaveDispose();

        // Odczyt: dodatek pojawia się w kolekcji childów pracownika (pracownik.Dodatki).
        var pracownik2 = Get<Prac>(guidPrac);
        var dodatki = pracownik2.Dodatki.Cast<Dodatek>().ToList();
        dodatki.Should().ContainSingle("dodaliśmy jeden dodatek do świeżego pracownika");

        var d = dodatki[0];
        d.Last.Element.Should().NotBeNull("Element jest wymagany");
        d.Last.Element.Nazwa.Should().Be("Premia");
        d.Last.Okres.From.Should().Be(okres.From, "okres obowiązywania dodatku");
    }

    [Test]
    [Description("KADRY-C1 (definicja elementu): definicje dodatków pobieramy ze słownika Place.DefElementow; " +
                 "definicja \"Premia\" istnieje w bazie Demo i jest źródłem typu Dodatek.")]
    public void KADRY_C1_DefinicjaElementu_PobieranaZeSlownika_PoNazwie()
    {
        // DefElementow to kolekcja konfiguracyjna; indeksowanie WgNazwy zwraca definicję po nazwie.
        var premia = Place.DefElementow.WgNazwy["Premia"] as DefinicjaElementu;
        premia.Should().NotBeNull("baza Demo zawiera definicję \"Premia\"");
        premia.Nazwa.Should().Be("Premia");

        // Definicje przeznaczone na dodatki mają RodzajZrodla == RodzajŹródłaWypłaty.Dodatek —
        // tym kryterium można filtrować dostępne definicje dodatków.
        premia.RodzajZrodla.Should().Be(RodzajŹródłaWypłaty.Dodatek,
            "Premia jest definicją źródła typu Dodatek");
    }
}
