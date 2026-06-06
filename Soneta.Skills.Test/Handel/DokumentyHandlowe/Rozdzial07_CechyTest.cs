using System.Linq;
using AwesomeAssertions;
using NUnit.Framework;
using Soneta.Business;
using Soneta.Handel;
using Soneta.Types;

namespace Soneta.Skills.Test.Handel.DokumentyHandlowe;

/// <summary>
/// Rozdział 7 — Cechy (Features) na dokumencie handlowym (wzorce W40–W42).
/// <para>
/// Cechy (<see cref="FeatureCollection"/>) to definiowalne informacje przypisane do <c>Row</c> —
/// tu: do dokumentu (<see cref="DokumentHandlowy"/>) i pozycji (<see cref="PozycjaDokHandlowego"/>).
/// Cecha jest adresowana <b>po nazwie definicji</b> (<c>FeatureDefinition</c>), a samo jej istnienie
/// zależy od konfiguracji wdrożenia — nie jest gwarantowane w bazie Demo.
/// </para>
/// <para>
/// Z tego powodu testy w tym rozdziale celują w <b>bezpieczną ścieżkę</b>: dostępność kolekcji
/// <c>Features</c>. Jednocześnie dokumentują <b>kontraktowe rzucanie wyjątku</b> przy odwołaniu do
/// cechy bez <c>FeatureSetDefinition</c>: zarówno <c>Features.Exists(nazwa)</c>, jak i warunek
/// serwerowy po string-path <c>"Features.Nazwa"</c> (<c>FieldCondition</c>) dla NIEZDEFINIOWANEJ
/// cechy rzucają <see cref="System.ArgumentException"/> — NIE zwracają false ani pustego zbioru.
/// Testy zapisu wartości cech (W41) oraz filtrowania zwracającego rekordy (W42) są <b>pominięte</b>,
/// bo wymagałyby wcześniej utworzonej definicji cechy, której Demo nie gwarantuje.
/// </para>
/// </summary>
[TestFixture]
public class Rozdzial07_CechyTest : DokumentHandlowyTestBase
{
    // Nazwa cechy gwarantowanie niezdefiniowana w Demo — używana do testów bezpiecznej ścieżki.
    // (Losowy, mało prawdopodobny identyfikator, by uniknąć kolizji z realną definicją wdrożenia.)
    private const string NieistniejacaCecha = "SkillTestCechaXyz";

    // ---------------------------------------------------------------------------------------------
    // W41 — Odczyt i zapis cech (Features)
    // ---------------------------------------------------------------------------------------------

    [Test]
    [Description("W41: property Features dokumentu jest dostępna (nie-null) zaraz po utworzeniu dokumentu.")]
    public void Features_NaDokumencie_JestDostepna()
    {
        // Tworzymy minimalny dokument przychodowy (PW) na magazynie Firma — bez kontrahenta.
        var dok = UtworzDokument(Definicje.PrzyjecieWewnetrzne, magazyn: Magazyn(Magazyn_.Firma));

        // Kolekcja Features istnieje zawsze, niezależnie od tego, czy zdefiniowano jakiekolwiek cechy.
        dok.Features.Should().NotBeNull();
        // Definicje cech to obiekt FeatureDefinitions (może być pusty, ale dostępny).
        dok.Features.Definitions.Should().NotBeNull();
        // Features.Row wskazuje z powrotem na dokument-właściciela.
        dok.Features.Row.Should().BeSameAs(dok);
    }

    [Test]
    [Description("W41: property Features pozycji dokumentu jest dostępna (nie-null) po dodaniu pozycji.")]
    public void Features_NaPozycji_JestDostepna()
    {
        var dok = UtworzDokument(Definicje.PrzyjecieWewnetrzne, magazyn: Magazyn(Magazyn_.Firma));

        // Pozycję dodajemy w transakcji edycyjnej (każde tworzenie/edycja Row tego wymaga).
        PozycjaDokHandlowego poz = null;
        InTransaction(() => poz = DodajPozycje(dok, Towar(Towar_.Bikini), ilosc: 1, cena: 5));

        // Kolekcja Features pozycji jest dostępna analogicznie do dokumentu.
        poz.Features.Should().NotBeNull();
        poz.Features.Row.Should().BeSameAs(poz);
    }

    [Test]
    [Description("W41: Features.Exists(nazwa) dla NIEZDEFINIOWANEJ cechy RZUCA ArgumentException " +
                 "(odwołanie do cechy bez FeatureSetDefinition nie jest bezpieczne — nie zwraca false).")]
    public void Features_Exists_DlaNiezdefiniowanejCechy_RzucaArgumentException()
    {
        var dok = UtworzDokument(Definicje.PrzyjecieWewnetrzne, magazyn: Magazyn(Magazyn_.Firma));

        // Kolekcja Features jest dostępna zawsze — niezależnie od konfiguracji cech.
        dok.Features.Should().NotBeNull();

        // UWAGA: Exists NIE jest bezpiecznym sprawdzeniem istnienia dla cechy, której nikt nie
        // zdefiniował (brak FeatureSetDefinition). Odwołanie do takiej cechy rzuca ArgumentException
        // ("nie znaleziono definicji cechy") — Exists NIE zwraca false dla nieznanej cechy.
        Assert.Throws<System.ArgumentException>(() => dok.Features.Exists(NieistniejacaCecha));
    }

    // --- POMINIĘTE (W41 zapis): ustawienie wartości cechy ---
    // Powód: zapis dok["Nazwa"] = wartość wymaga istniejącej definicji cechy (FeatureDefinition)
    // zarejestrowanej dla tabeli DokHandlowe / PozycjeDokHan. Baza Demo nie gwarantuje żadnej
    // takiej definicji, a tworzenie nowych definicji cech wykracza poza zakres tego rozdziału
    // (i poza bezpieczną ścieżkę dla dodatku zewnętrznego). Odwołanie do niezdefiniowanej cechy
    // rzuciłoby wyjątek, więc testu zapisu świadomie NIE piszemy.

    // ---------------------------------------------------------------------------------------------
    // W42 — Filtrowanie / wyszukiwanie po wartości cechy (serwerowo)
    // ---------------------------------------------------------------------------------------------

    [Test]
    [Description("W42: warunek serwerowy FieldCondition.Equal po string-path 'Features.Nazwa' " +
                 "dla NIEZDEFINIOWANEJ cechy RZUCA ArgumentException przy aplikacji na indeksie dokumentów.")]
    public void FiltrPoCesze_NaIndeksieDokumentow_DlaNiezdefiniowanejCechy_RzucaArgumentException()
    {
        // Cechy adresuje się STRING-PATHEM "Features.Nazwa" — Features.X nie jest typowaną property
        // Row, więc nie da się jej użyć w wyrażeniu LINQ. Warunek budujemy jako FieldCondition.
        var warunek = new FieldCondition.Equal($"Features.{NieistniejacaCecha}", "dowolna");

        // Filtr serwerowy po cesze BEZ FeatureSetDefinition nie zwraca pustego zbioru — rzuca
        // ArgumentException ("nie znaleziono definicji cechy") już przy budowaniu/aplikacji zapytania.
        // Demo nie gwarantuje żadnej zdefiniowanej cechy, więc to zachowanie jest deterministyczne.
        Assert.Throws<System.ArgumentException>(() =>
            Handel.DokHandlowe.WgDaty[warunek].Cast<DokumentHandlowy>().ToArray());
    }

    [Test]
    [Description("W42: złożony warunek RowCondition.And/FieldCondition po NIEZDEFINIOWANEJ cesze " +
                 "RZUCA ArgumentException przy wykonaniu serwerowym (brak FeatureSetDefinition).")]
    public void FiltrPoCesze_WarunekZlozony_DlaNiezdefiniowanejCechy_RzucaArgumentException()
    {
        // Składanie warunków serwerowych: cecha-bool ORAZ cecha-data >= dziś.
        // Wartości podajemy w typie zgodnym z typem cechy (bool dla Bool, Date dla Date) — zgodnie
        // z W42. Sam warunek się składa, ale wykonanie na indeksie wymaga definicji cechy.
        var warunek = new RowCondition.And(
            new FieldCondition.Equal($"Features.{NieistniejacaCecha}", true),
            new FieldCondition.GreaterEqual($"Features.{NieistniejacaCecha}Data", Date.Today));

        // Brak FeatureSetDefinition dla cechy → ArgumentException przy aplikacji warunku na indeksie
        // (nie pusty zbiór). Deterministyczne w Demo, które nie gwarantuje żadnej zdefiniowanej cechy.
        Assert.Throws<System.ArgumentException>(() =>
            Handel.DokHandlowe.WgDaty[warunek].Cast<DokumentHandlowy>().ToArray());
    }

    [Test]
    [Description("W42: filtr po cesze na kolekcji SubTable pozycji dokumentu (dok.Pozycje[condition]) " +
                 "wykonuje się bez błędu i dla nieistniejącej cechy zwraca pusty zbiór.")]
    public void FiltrPoCesze_NaPozycjachDokumentu_WykonujeSieBezBledu()
    {
        // Tworzymy dokument z jedną pozycją — sam dokument istnieje, ale żadna pozycja nie ma
        // ustawionej (ani zdefiniowanej) testowej cechy.
        var dok = UtworzDokument(Definicje.PrzyjecieWewnetrzne, magazyn: Magazyn(Magazyn_.Firma));
        InTransaction(() => DodajPozycje(dok, Towar(Towar_.Bikini), ilosc: 1, cena: 5));

        // Filtr na kolekcji SubTable (dok.Pozycje[condition]) również wykonuje się serwerowo.
        var warunek = new FieldCondition.Equal($"Features.{NieistniejacaCecha}", "S-2026-001");
        var pozycje = dok.Pozycje[warunek].Cast<PozycjaDokHandlowego>().ToArray();

        // Brak pozycji o takiej cesze — zbiór pusty, bez wyjątku.
        pozycje.Should().BeEmpty();
    }

    // --- POMINIĘTE (W42 z trafieniami): filtr po cesze zwracający rekordy ---
    // Powód: aby warunek FieldCondition.Equal("Features.Nazwa", wartość) zwrócił jakikolwiek
    // dokument/pozycję, musi istnieć definicja cechy ORAZ zapisana wartość tej cechy na rekordzie.
    // Oba elementy wymagałyby zdefiniowania własnej cechy (FeatureDefinition) i zapisu jej wartości,
    // czego Demo nie gwarantuje. Testujemy więc jedynie, że konstrukcja i wykonanie warunku
    // serwerowego są poprawne (powyżej), nie zaś zawartość zwróconego zbioru.

    // --- POMINIĘTE (W40): przenoszenie cech z partii / dokumentu nadrzędnego ---
    // Powód: przenoszenie cech to mechanizm KONFIGURACYJNY (flagi DefDokHandlowego.KopiujCechyDostawy,
    // KopiujCechyDokumentu/KopiujCechyPozycji na definicji relacji), a faktyczne skopiowanie cechy
    // wymaga: (1) istniejącej definicji cechy zarejestrowanej dla pozycji/partii, (2) zapisanego
    // przyjęcia z ustawioną cechą i (3) rozchodu ze wskazaniem partii. Bez gwarantowanej definicji
    // cechy w Demo nie da się zweryfikować przeniesienia wartości bezpieczną ścieżką, więc W40
    // pomijamy w testach (pozostaje udokumentowany w skillu jako konfiguracja, nie API).
}
