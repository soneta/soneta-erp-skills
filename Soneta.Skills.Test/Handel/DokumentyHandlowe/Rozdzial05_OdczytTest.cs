using System.Linq;
using AwesomeAssertions;
using NUnit.Framework;
using Soneta.Business;
using Soneta.Handel;
using Soneta.Types;

namespace Soneta.Skills.Test.Handel.DokumentyHandlowe;

/// <summary>
/// Rozdział 5 — „Odczyt i wyszukiwanie” (wzorce W25–W30).
/// <para>
/// Testy pokazują, jak dodatek zewnętrzny odczytuje i wyszukuje dokumenty handlowe wyłącznie na
/// publicznym kontrakcie platformy: odczyt pozycji (<c>dok.Pozycje</c>), wyszukiwanie serwerowe wg
/// okresu / definicji / stanu na kluczach tabeli (<c>hm.DokHandlowe.WgDaty[condition]</c>,
/// <c>WgMagazynuNumer</c>, <c>WgKontrahentaObcy</c>), odczyt po <c>Guid</c>
/// (<c>hm.DokHandlowe[guid]</c> / <c>Get&lt;DokumentHandlowy&gt;(guid)</c>), dokumenty kontrahenta
/// oraz korekty (<c>DokumentKorygowany</c> / <c>DokumentyKorygujące</c> / pole <c>Korekta</c>).
/// </para>
/// <para>
/// Wzorzec danych: tworzymy znany dokument (PW — przyjęcie wewnętrzne, dokument przychodowy, więc
/// nie wymaga wcześniejszego stanu magazynowego), zapisujemy trwale przez <c>SaveDispose()</c>,
/// a następnie na świeżej sesji odczytujemy i wyszukujemy go serwerowo. Filtrowanie zawsze trafia
/// do klauzuli <c>WHERE</c> — nigdy nie iterujemy całej tabeli operacyjnej w pamięci.
/// </para>
/// <para>
/// <b>Uwaga o kluczach:</b> tabela <c>DokHandlowe</c> nie ma „gołych” kluczy <c>WgNumeru</c> ani
/// <c>WgKontrahenta</c>. Filtrujemy wyrażeniem na dostępnym kluczu (<c>WgDaty</c>,
/// <c>WgMagazynuNumer</c>, <c>WgKontrahentaObcy</c>) — wybór klucza decyduje wyłącznie o sortowaniu,
/// warunek i tak trafia do SQL.
/// </para>
/// </summary>
[TestFixture]
public class Rozdzial05_OdczytTest : DokumentHandlowyTestBase
{
    /// <summary>
    /// Tworzy znane przyjęcie wewnętrzne (PW) z jedną pozycją towaru BIKINI na magazynie F,
    /// zapisuje je trwale i zamyka sesję edycji. Zwraca <c>Guid</c> dokumentu, po którym kolejne
    /// testy odczytują rekord na świeżej sesji.
    /// </summary>
    private System.Guid UtworzZnanyDokumentPW(double ilosc = 3, double cena = 12)
    {
        // PW to dokument przychodowy — Demo (StanUjemnyVerifier) nie blokuje go brakiem stanu.
        var dok = UtworzDokument(Definicje.PrzyjecieWewnetrzne, magazyn: Magazyn(Magazyn_.Firma));
        InTransaction(() => DodajPozycje(dok, Towar(Towar_.Bikini), ilosc, cena));
        var guid = dok.Guid;

        // Zapis trwały + zamknięcie sesji: dalej czytamy na świeżej sesji po Guid (wzorzec z facts).
        SaveDispose();
        return guid;
    }

    // === W25 — Odczytanie pozycji dokumentu ===

    [Test]
    [Description("W25: dok.Pozycje (LpSubTable) zwraca zapisane pozycje z poprawnym towarem, " +
                 "ilością i wyliczoną wartością.")]
    public void W25_OdczytPozycji_ZwracaTowarIloscIWartosc()
    {
        var guid = UtworzZnanyDokumentPW(ilosc: 3, cena: 12);

        // Odczyt na świeżej sesji po Guid (W29).
        var dok = Get<DokumentHandlowy>(guid);
        dok.Should().NotBeNull();

        // dok.Pozycje to LpSubTable — posortowana po Lp, iterowalna bez dodatkowego filtra.
        dok.Pozycje.Count.Should().Be(1);

        var poz = dok.Pozycje.First();
        poz.Towar.Kod.Should().Be(Towar_.Bikini);
        // Ilosc to Quantity (Value + Symbol), nie decimal.
        poz.Ilosc.Value.Should().Be(3);
        // Wartość pozycji jest przeliczana przez platformę — czytamy ją, nie wyliczamy ręcznie.
        poz.Suma.NettoCy.Value.Should().BeGreaterThan(0);
    }

    [Test]
    [Description("W25: filtr serwerowy dok.Pozycje[p => p.Towar == towar] zawęża pozycje do " +
                 "wskazanego towaru.")]
    public void W25_FiltrPozycjiWgTowaru_ZwracaTylkoPasujace()
    {
        var guid = UtworzZnanyDokumentPW();
        var dok = Get<DokumentHandlowy>(guid);

        var bikini = Towar(Towar_.Bikini);
        var transport = Towar(Towar_.Transport);

        // Warunek na kolekcji jednego dokumentu — wykona się serwerowo (preferowane mimo małej kolekcji).
        var pozycjeBikini = dok.Pozycje[(PozycjaDokHandlowego p) => p.Towar == bikini].ToArray();
        pozycjeBikini.Should().HaveCount(1);
        pozycjeBikini[0].Towar.Kod.Should().Be(Towar_.Bikini);

        // Towar, którego na dokumencie nie ma — pusty zbiór.
        var pozycjeTransport = dok.Pozycje[(PozycjaDokHandlowego p) => p.Towar == transport].ToArray();
        pozycjeTransport.Should().BeEmpty();
    }

    // === W28 — Wyszukiwanie wg okresu, definicji, stanu (serwerowo) ===

    [Test]
    [Description("W28: hm.DokHandlowe.WgDaty[condition] z koniunkcją definicja + okres + magazyn " +
                 "odnajduje utworzony dokument serwerowo.")]
    public void W28_WyszukiwanieWgDefinicjiOkresuMagazynu_ZnajdujeDokument()
    {
        var guid = UtworzZnanyDokumentPW();

        var def = Definicja(Definicje.PrzyjecieWewnetrzne);
        var mag = Magazyn(Magazyn_.Firma);
        // Szeroki, ale ograniczony przedział wokół „dziś” — nie ładujemy całej historii.
        var od = Date.Today.AddMonths(-1);
        var doDt = Date.Today.AddMonths(1);

        // Klucz WgDaty nadaje sortowanie po Data, Czas; warunek (definicja, magazyn, okres) idzie do WHERE.
        var znalezione = Handel.DokHandlowe.WgDaty[(DokumentHandlowy dok) =>
                dok.Definicja == def
                && dok.Magazyn == mag
                && dok.Data >= od && dok.Data <= doDt]
            .ToArray();

        // Wśród wyników musi być nasz dokument (po Guid).
        znalezione.Should().Contain(d => d.Guid == guid);
    }

    [Test]
    [Description("W28: filtr po stanie dokumentu — Bufor znajduje świeży dokument, " +
                 "Zatwierdzony go nie zawiera.")]
    public void W28_WyszukiwanieWgStanu_RozrozniaBuforOdZatwierdzonego()
    {
        var guid = UtworzZnanyDokumentPW();

        // Nowy dokument pozostaje w Buforze — stan porównujemy enumem (pole bazodanowe).
        var wBuforze = Handel.DokHandlowe.WgDaty[(DokumentHandlowy dok) =>
                dok.Stan == StanDokumentuHandlowego.Bufor]
            .ToArray();
        wBuforze.Should().Contain(d => d.Guid == guid);

        // Ten sam dokument NIE może pojawić się w filtrze po stanie Zatwierdzony.
        var zatwierdzone = Handel.DokHandlowe.WgDaty[(DokumentHandlowy dok) =>
                dok.Stan == StanDokumentuHandlowego.Zatwierdzony]
            .ToArray();
        zatwierdzone.Should().NotContain(d => d.Guid == guid);
    }

    // === W29 — Odczyt dokumentu wg Guid oraz wg pełnego numeru ===

    [Test]
    [Description("W29: indeksator hm.DokHandlowe[guid] zwraca zapisany dokument dla istniejącego " +
                 "Guid, a dla nieznanego Guid rzuca RowNotFoundException (nie zwraca null).")]
    public void W29_OdczytPoGuid_ZwracaDokumentLubRzucaDlaNieznanego()
    {
        var guid = UtworzZnanyDokumentPW();

        // Indeksator GuidedTable po Guid — jednoznaczny dostęp do istniejącego rekordu.
        var dok = Handel.DokHandlowe[guid];
        dok.Should().NotBeNull();
        dok.Guid.Should().Be(guid);

        // Dla nieistniejącego Guid indeksator RZUCA RowNotFoundException (nie zwraca null).
        Assert.Throws<RowNotFoundException>(() =>
        {
            var _ = Handel.DokHandlowe[System.Guid.NewGuid()];
        });
    }

    [Test]
    [Description("W29: wyszukanie po pełnym numerze warunkiem na polu bazodanowym Numer.Pelny " +
                 "(klucz WgMagazynuNumer); odczyt sformatowanego numeru przez Numer.NumerPelny.")]
    public void W29_OdczytPoPelnymNumerze_FiltrSerwerowy_ZnajdujeDokument()
    {
        var guid = UtworzZnanyDokumentPW();

        // Najpierw odczytujemy pełny numer dokumentu (kalkulowane NumerPelny) — to wartość do porównania.
        var dok = Get<DokumentHandlowy>(guid);
        var pelnyNumer = dok.Numer.NumerPelny;
        pelnyNumer.Should().NotBeNullOrEmpty();

        var mag = Magazyn(Magazyn_.Firma);

        // W warunku LINQ używamy POLA BAZODANOWEGO Numer.Pelny (nie kalkulowanego NumerPelny).
        // Numer bywa unikalny per magazyn, więc filtr dokładamy magazynem i bierzemy FirstOrDefault.
        var znaleziony = Handel.DokHandlowe.WgMagazynuNumer[(DokumentHandlowy d) =>
                d.Magazyn == mag && d.Numer.Pelny == pelnyNumer]
            .FirstOrDefault();

        znaleziony.Should().NotBeNull();
        znaleziony.Guid.Should().Be(guid);
    }

    // === W26 — Odczytanie dokumentów dla kontrahenta ===

    [Test]
    [Description("W26: typowany filtr serwerowy od strony Handlu (WgKontrahentaObcy) zawężony " +
                 "okresem zwraca dokumenty wskazanego kontrahenta.")]
    public void W26_DokumentyKontrahenta_FiltrServerowyOdStronyHandlu()
    {
        // PW nie nosi kontrahenta — by mieć dokument WG kontrahenta tworzymy FV (sprzedaż).
        // FV rozchodowe wymaga ZATWIERDZONEGO przyjęcia na stan (Demo blokuje stan ujemny).
        PrzyjmijNaStan(Towar_.Bikini, 20);

        var k = Kontrahent(Kontrahent_.Abc);

        // FV z kontrahentem — trzymamy w BUFORZE (zatwierdzenie FV rzuca NRE w ewidencji VAT, p. facts §3).
        var fv = UtworzDokument(
            Definicje.FakturaSprzedazy,
            kontrahent: k,
            magazyn: Magazyn(Magazyn_.Firma));
        InTransaction(() => DodajPozycje(fv, Towar(Towar_.Bikini), ilosc: 2, cena: 50));
        var guid = fv.Guid;
        SaveDispose();

        var kontrahent = Kontrahent(Kontrahent_.Abc);
        var od = Date.Today.AddMonths(-1);

        // Filtr serwerowy po kontrahencie i dacie — tylko pola bazodanowe (JOIN po referencji rekordu).
        var dokumenty = Handel.DokHandlowe.WgDaty[(DokumentHandlowy d) =>
                d.Kontrahent == kontrahent && d.Data >= od]
            .ToArray();

        dokumenty.Should().Contain(d => d.Guid == guid);
        dokumenty.Should().OnlyContain(d => d.Kontrahent == kontrahent);
    }

    // === W30 — Korekty: pole bazodanowe Korekta + powiązania kalkulowane ===

    [Test]
    [Description("W30: świeży dokument zwykły nie jest korektą (pole bazodanowe Korekta == false), " +
                 "a DokumentKorygowany jest null.")]
    public void W30_DokumentZwykly_NieJestKorekta_BrakDokumentuKorygowanego()
    {
        var guid = UtworzZnanyDokumentPW();
        var dok = Get<DokumentHandlowy>(guid);

        // Korekta to pole bazodanowe (read-only z perspektywy biznesowej) — dla zwykłego dokumentu false.
        dok.Korekta.Should().BeFalse();

        // DokumentKorygowany jest kalkulowane i zwraca null, gdy dokument nie jest korektą.
        dok.DokumentKorygowany.Should().BeNull();

        // DokumentyKorygujące to łańcuch (IEnumerable) — dla dokumentu bez korekt jest pusty.
        dok.DokumentyKorygujące.Should().BeEmpty();
    }

    [Test]
    [Description("W30: serwerowy filtr korekt na polu bazodanowym Korekta (WgDaty) NIE zawiera " +
                 "zwykłego dokumentu.")]
    public void W30_SerwerowyFiltrKorekt_NieZawieraZwyklegoDokumentu()
    {
        var guid = UtworzZnanyDokumentPW();

        var od = Date.Today.AddMonths(-1);

        // W warunku serwerowym wolno użyć tylko pola bazodanowego Korekta (powiązania korekt są kalkulowane).
        var korekty = Handel.DokHandlowe.WgDaty[(DokumentHandlowy d) =>
                d.Korekta && d.Data >= od]
            .ToArray();

        // Nasz dokument jest zwykłym PW — nie może wystąpić w zbiorze korekt.
        korekty.Should().NotContain(d => d.Guid == guid);
        // Wszystkie elementy zbioru (jeśli są) faktycznie są korektami.
        korekty.Should().OnlyContain(d => d.Korekta);
    }
}
