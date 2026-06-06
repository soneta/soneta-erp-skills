using System.Linq;
using AwesomeAssertions;
using NUnit.Framework;
using Soneta.Handel;
using Soneta.Types;

namespace Soneta.Skills.Test.Handel.DokumentyHandlowe;

/// <summary>
/// Rozdział 1 — „Fundamenty i identyfikacja” (W1–W3) dokumentu handlowego.
/// Testy pełnią podwójną rolę: weryfikują publiczny kontrakt platformy ORAZ stanowią dokumentację
/// poprawnych wzorców kodu dla programisty dodatku zewnętrznego. Pokrywają:
/// <list type="bullet">
/// <item>W1 — dostęp z sesji do modułów handlowo-magazynowych (Handel/Magazyny/Towary/CRM)
/// oraz do tabeli dokumentów <c>DokHandlowe</c>;</item>
/// <item>W2 — wybór definicji dokumentu (<c>DefDokHandlowego</c>) po symbolu (klucz unikalny);</item>
/// <item>W3 — rozpoznanie rodzaju dokumentu (faktura / magazynowy / zamówienie / korekta / zaliczka)
/// wg <c>Definicja.Kategoria</c> oraz flag dokumentu.</item>
/// </list>
/// Wszystko operuje wyłącznie na <b>publicznym kontrakcie</b> — tak jak dodatek bez dostępu do kodu źródłowego.
/// </summary>
[TestFixture]
public class Rozdzial01_FundamentyTest : DokumentHandlowyTestBase
{
    // ============================================================================================
    // W1 — Dostęp do modułów handlowo-magazynowych i tabeli DokHandlowe
    // ============================================================================================

    [Test]
    [Description("W1: z sesji dostępne są wszystkie cztery moduły (Handel, Magazyny, Towary, CRM) " +
                 "i każdy wskazuje z powrotem na tę samą sesję (ISessionable.Session).")]
    public void W1_DostepDoModulow_ModulyDostepneIWskazujaNaSesje()
    {
        // Punkt wejścia każdego scenariusza: z Session pobieramy moduły metodami rozszerzającymi.
        // Helpery bazy (Handel/Magazyny/Towary/Crm) opakowują session.GetHandel()/GetMagazyny() itd.
        Handel.Should().NotBeNull("session.GetHandel() musi zwrócić moduł Handel");
        Magazyny.Should().NotBeNull("session.GetMagazyny() musi zwrócić moduł Magazyny");
        Towary.Should().NotBeNull("session.GetTowary() musi zwrócić moduł Towary");
        Crm.Should().NotBeNull("session.GetCRM() musi zwrócić moduł CRM");

        // Każdy moduł implementuje ISessionable — property Session zamyka pętlę dostępu do danych.
        Handel.Session.Should().BeSameAs(Session);
        Magazyny.Session.Should().BeSameAs(Session);
        Towary.Session.Should().BeSameAs(Session);
        Crm.Session.Should().BeSameAs(Session);
    }

    [Test]
    [Description("W1: moduł Handel udostępnia tabelę dokumentów DokHandlowe oraz tabelę definicji " +
                 "DefDokHandlowych — to dwa podstawowe punkty dostępu do danych handlowych.")]
    public void W1_ModulHandel_UdostepniaTabeleDokumentowIDefinicji()
    {
        // DokHandlowe — operacyjna tabela dokumentów (faktur, magazynowych, zamówień...).
        // DefDokHandlowych — konfiguracyjna tabela definicji wyznaczających rodzaj dokumentu.
        Handel.DokHandlowe.Should().NotBeNull("tabela dokumentów handlowych musi istnieć w module");
        Handel.DefDokHandlowych.Should().NotBeNull("tabela definicji dokumentów musi istnieć w module");

        // Obie tabele należą do tej samej sesji co moduł (spójność kontekstu danych).
        Handel.DokHandlowe.Session.Should().BeSameAs(Session);
    }

    [Test]
    [Description("W1: tabelę DokHandlowe iterujemy ZAWSZE z zawężeniem zakresu (filtr serwerowy na " +
                 "indeksie WgDaty), zamiast ładować całą rosnącą tabelę operacyjną do pamięci.")]
    public void W1_IteracjaDokumentow_FiltrSerwerowyPoDacie_NieRzucaIDziala()
    {
        // Wzorzec safe-code: warunek RowCondition aplikujemy na indeksie (wykona się po stronie SQL).
        // W warunku używamy wyłącznie pól bazodanowych (Data) — pole kalkulowane rzuciłoby wyjątek.
        var od = Date.Today.AddMonths(-1);

        // Sama materializacja zapytania (Count) potwierdza, że filtr serwerowy jest poprawny składniowo
        // i wykonalny; nie zakładamy konkretnej liczby dokumentów w bazie Demo (fakt niestabilny).
        var liczba = Handel.DokHandlowe
            .WgDaty[(DokumentHandlowy x) => x.Data >= od]
            .Count();

        liczba.Should().BeGreaterThanOrEqualTo(0, "filtr serwerowy powinien się wykonać bez błędu");
    }

    // ============================================================================================
    // W2 — Wybór definicji dokumentu (DefDokHandlowego) wg symbolu
    // ============================================================================================

    [Test]
    [Description("W2: WgSymbolu to indeks UNIKALNY — dla istniejącego symbolu (FV) zwraca pojedynczy " +
                 "rekord, którego Symbol odpowiada żądanemu (lookup symboli jest spójny).")]
    public void W2_DefinicjaPoSymbolu_KluczUnikalny_ZwracaRekordOZgodnymSymbolu()
    {
        // WgSymbolu["FV"] — klucz unikalny: zwraca pojedynczy DefDokHandlowego albo null.
        var defFV = Definicja(Definicje.FakturaSprzedazy);

        defFV.Should().NotBeNull("baza Demo zawiera definicję faktury sprzedaży o symbolu FV");
        defFV.Symbol.Should().Be(Definicje.FakturaSprzedazy,
            "indeks WgSymbolu musi zwrócić rekord o dokładnie tym symbolu");
    }

    [Test]
    [Description("W2: dla symbolu NIEISTNIEJĄCEGO indeks unikalny WgSymbolu zwraca null — to sygnał " +
                 "do walidacji przed utworzeniem dokumentu (nie zakładaj obecności symbolu na sztywno).")]
    public void W2_DefinicjaPoNieistniejacymSymbolu_ZwracaNull()
    {
        // Symbole zależą od konfiguracji bazy — zawsze sprawdzaj != null przed użyciem.
        var brak = Definicja("NIE_ISTNIEJE_XYZ");

        brak.Should().BeNull("dla nieznanego symbolu klucz unikalny zwraca null, nie wyjątek");
    }

    [Test]
    [Description("W2: definicja jest PIERWSZYM polem nowego dokumentu — po jej ustawieniu dokument " +
                 "ma przypisaną definicję o oczekiwanym symbolu (UtworzDokument ustawia ją jako pierwszą).")]
    public void W2_UtworzenieDokumentu_DefinicjaUstawionaJakoPierwszaJestPrzypisana()
    {
        // Kolejność z helpera UtworzDokument: AddRow -> Definicja (pierwsza) -> Magazyn -> Kontrahent.
        // Tu sprawdzamy sam fakt poprawnego przypisania definicji do świeżego dokumentu.
        var dok = UtworzDokument(Definicje.PrzyjecieWewnetrzne, magazyn: Magazyn(Magazyn_.Firma));

        dok.Should().NotBeNull();
        dok.Definicja.Should().NotBeNull("definicja musi być ustawiona jako pierwsze pole dokumentu");
        dok.Definicja.Symbol.Should().Be(Definicje.PrzyjecieWewnetrzne);
    }

    [Test]
    [Description("W2: ten sam rekord definicji jest osiągalny z dwóch dróg — bezpośrednio z tabeli " +
                 "definicji (WgSymbolu) oraz przez utworzony dokument (dok.Definicja) — to jeden obiekt.")]
    public void W2_DefinicjaDokumentu_TozsamaZRekordemZTabeliDefinicji()
    {
        // Tożsamość referencyjna potwierdza, że dok.Definicja wskazuje rekord z tabeli DefDokHandlowych,
        // a nie kopię — kluczowe dla rozpoznawania rodzaju dokumentu po Definicja.Kategoria (W3).
        var defPW = Definicja(Definicje.PrzyjecieWewnetrzne);
        var dok = UtworzDokument(Definicje.PrzyjecieWewnetrzne, magazyn: Magazyn(Magazyn_.Firma));

        dok.Definicja.Should().BeSameAs(defPW,
            "definicja dokumentu to ten sam rekord co pobrany z tabeli definicji");
    }

    // ============================================================================================
    // W3 — Rozpoznanie rodzaju dokumentu (kategoria + flagi)
    // ============================================================================================

    [Test]
    [Description("W3: definicja faktury sprzedaży (FV) ma kategorię w zakresie HANDLOWYM " +
                 "(HandelPierwszy..HandelOstatni) — rodzaj rozpoznajemy po zakresie kategorii, nie po symbolu.")]
    public void W3_FakturaSprzedazy_KategoriaWZakresieHandlowym()
    {
        // Rozpoznanie rodzaju opieramy na Definicja.Kategoria, NIE na porównaniu Symbol == "FV"
        // (symbol jest dowolny i zależny od bazy). Markery zakresów enuma są publiczne.
        var defFV = Definicja(Definicje.FakturaSprzedazy);
        defFV.Should().NotBeNull();

        var kat = defFV.Kategoria;
        kat.Should().BeOneOf(KategoriaHandlowa.Sprzedaż, KategoriaHandlowa.KorektaSprzedaży);
        WCzyZakresie(kat, KategoriaHandlowa.HandelPierwszy, KategoriaHandlowa.HandelOstatni)
            .Should().BeTrue("kategoria faktury mieści się w zakresie handlowym");
    }

    [Test]
    [Description("W3: definicje dokumentów magazynowych (PW/PZ/WZ/RW) mają kategorie w zakresie " +
                 "MAGAZYNOWYM (MagazynPierwszy..MagazynOstatni) — rozpoznanie grupy zakresem markerów.")]
    public void W3_DokumentyMagazynowe_KategorieWZakresieMagazynowym()
    {
        // Klasyfikacja „grupy” dokumentu po zakresie wartości enuma — bez wyliczania wszystkich symboli.
        foreach (var symbol in new[]
                 {
                     Definicje.PrzyjecieWewnetrzne, Definicje.PrzyjecieZewnetrzne,
                     Definicje.WydanieZewnetrzne, Definicje.RozchodWewnetrzny
                 })
        {
            var def = Definicja(symbol);
            def.Should().NotBeNull($"baza Demo zawiera definicję magazynową {symbol}");

            WCzyZakresie(def.Kategoria, KategoriaHandlowa.MagazynPierwszy, KategoriaHandlowa.MagazynOstatni)
                .Should().BeTrue($"kategoria dokumentu {symbol} ma być w zakresie magazynowym");
        }
    }

    [Test]
    [Description("W3: definicje zamówień (ZO/ZD) mają kategorie zamówień (ZamówienieOdbiorcy/" +
                 "ZamówienieDostawcy) — leżą poza zakresami handlowym i magazynowym.")]
    public void W3_Zamowienia_RozpoznawaneJakoKategorieZamowien()
    {
        var defZO = Definicja(Definicje.ZamowienieOdbiorcy);
        var defZD = Definicja(Definicje.ZamowienieDoDostawcy);
        defZO.Should().NotBeNull();
        defZD.Should().NotBeNull();

        // Zamówienie to ani dokument handlowy (faktura), ani magazynowy — własna grupa kategorii.
        defZO.Kategoria.Should().Be(KategoriaHandlowa.ZamówienieOdbiorcy);
        defZD.Kategoria.Should().Be(KategoriaHandlowa.ZamówienieDostawcy);

        WCzyZakresie(defZO.Kategoria, KategoriaHandlowa.HandelPierwszy, KategoriaHandlowa.HandelOstatni)
            .Should().BeFalse("zamówienie nie należy do zakresu handlowego (faktur)");
        WCzyZakresie(defZO.Kategoria, KategoriaHandlowa.MagazynPierwszy, KategoriaHandlowa.MagazynOstatni)
            .Should().BeFalse("zamówienie nie należy do zakresu magazynowego");
    }

    [Test]
    [Description("W3: pełna klasyfikacja rodzaju przez funkcję rozgałęziającą po zakresie kategorii — " +
                 "FV→handlowy, PW/WZ→magazynowy, ZO→zamówienie (wzorzec z dokumentacji rozdziału).")]
    public void W3_RozpoznajRodzaj_ZwracaPoprawnaGrupeDlaKazdejDefinicji()
    {
        // Wzorzec RozpoznajRodzaj klasyfikuje dokument po Definicja.Kategoria zakresami markerów.
        RozpoznajRodzaj(UtworzDokument(Definicje.FakturaSprzedazy, kontrahent: Kontrahent(Kontrahent_.Abc)))
            .Should().Be(RodzajDokumentu.Handlowy);

        RozpoznajRodzaj(UtworzDokument(Definicje.PrzyjecieWewnetrzne, magazyn: Magazyn(Magazyn_.Firma)))
            .Should().Be(RodzajDokumentu.Magazynowy);

        RozpoznajRodzaj(UtworzDokument(Definicje.WydanieZewnetrzne, magazyn: Magazyn(Magazyn_.Firma)))
            .Should().Be(RodzajDokumentu.Magazynowy);

        RozpoznajRodzaj(UtworzDokument(Definicje.ZamowienieOdbiorcy, kontrahent: Kontrahent(Kontrahent_.Abc)))
            .Should().Be(RodzajDokumentu.Zamowienie);
    }

    [Test]
    [Description("W3: świeżo utworzony zwykły dokument (nie z relacji korekty) ma flagę Korekta=false — " +
                 "korektę tworzy się przez relacje dokumentów, a nie przez przestawienie flagi.")]
    public void W3_ZwyklyDokument_FlagaKorektaFalsz()
    {
        // dok.Korekta rozpoznaje korektę. Zwykły dokument utworzony „od zera” nie jest korektą.
        var dok = UtworzDokument(Definicje.PrzyjecieWewnetrzne, magazyn: Magazyn(Magazyn_.Firma));

        dok.Korekta.Should().BeFalse("dokument utworzony od zera (nie z relacji) nie jest korektą");
    }

    [Test]
    [Description("W3: zwykły dokument (faktura/magazynowy/zamówienie) nie jest dokumentem zaliczkowym — " +
                 "flaga rozpoznająca zaliczkę jest false dla dokumentów utworzonych bez powiązania zaliczki.")]
    public void W3_ZwyklyDokument_NieJestZaliczkowy()
    {
        // Rozpoznanie zaliczki ma pierwszeństwo przed klasyfikacją zakresową (zaliczka bywa fakturą),
        // ale zwykły dokument utworzony od zera zaliczką nie jest.
        var faktura = UtworzDokument(Definicje.FakturaSprzedazy, kontrahent: Kontrahent(Kontrahent_.Abc));
        var magazynowy = UtworzDokument(Definicje.PrzyjecieWewnetrzne, magazyn: Magazyn(Magazyn_.Firma));

        faktura.JestZaliczkowy.Should().BeFalse("zwykła faktura sprzedaży nie jest dokumentem zaliczkowym");
        magazynowy.JestZaliczkowy.Should().BeFalse("dokument magazynowy nie jest dokumentem zaliczkowym");
    }

    // ============================================================================================
    // Pomocnicze (wzorce klasyfikacji z dokumentacji rozdziału)
    // ============================================================================================

    /// <summary>Grupa rodzajowa dokumentu rozpoznana po kategorii jego definicji.</summary>
    private enum RodzajDokumentu { Handlowy, Magazynowy, Zamowienie, Inny }

    /// <summary>
    /// Klasyfikacja rodzaju dokumentu po <c>Definicja.Kategoria</c> z użyciem publicznych markerów
    /// zakresów enuma <see cref="KategoriaHandlowa"/> — odzwierciedla wzorzec ze snippetu rozdziału.
    /// </summary>
    private static RodzajDokumentu RozpoznajRodzaj(DokumentHandlowy dok)
    {
        // Definicja może być null na świeżo nieskonfigurowanym dokumencie — zabezpieczamy dostęp.
        if (dok.Definicja == null)
            return RodzajDokumentu.Inny;

        var kat = dok.Definicja.Kategoria;

        return kat switch
        {
            >= KategoriaHandlowa.HandelPierwszy and <= KategoriaHandlowa.HandelOstatni
                => RodzajDokumentu.Handlowy,
            >= KategoriaHandlowa.MagazynPierwszy and <= KategoriaHandlowa.MagazynOstatni
                => RodzajDokumentu.Magazynowy,
            KategoriaHandlowa.ZamówienieOdbiorcy
                or KategoriaHandlowa.ZamówienieDostawcy
                or KategoriaHandlowa.ZamówienieWewnętrzne
                => RodzajDokumentu.Zamowienie,
            _ => RodzajDokumentu.Inny
        };
    }

    /// <summary>Sprawdza, czy kategoria mieści się w zakresie [od, do] (markery zakresów enuma).</summary>
    private static bool WCzyZakresie(KategoriaHandlowa kat, KategoriaHandlowa od, KategoriaHandlowa gora)
        => kat >= od && kat <= gora;
}
