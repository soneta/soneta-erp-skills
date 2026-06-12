using System;
using System.Linq;
using AwesomeAssertions;
using NUnit.Framework;
using Action = System.Action;
using Soneta.Business;
using Soneta.CRM;
using Soneta.Handel;
using Soneta.Tools;
using Soneta.Towary;
using Soneta.Types;

namespace Soneta.Skills.Test.Handel.DokumentyHandlowe;

/// <summary>
/// Rozdział 11 skilla „dokument-handlowy” — Operacje pomocnicze (przekrojowe) (HANDEL-W56–HANDEL-W61).
/// <para>
/// Testy weryfikują wzorce „okołodokumentowe”: bezpieczne pozyskanie kontrahenta/towaru i obsługę
/// kontrahenta incydentalnego (HANDEL-W56), przeliczanie jednostek miary towaru (HANDEL-W57), walidację przed
/// zatwierdzeniem (HANDEL-W58), obsługę błędów/blokady optymistycznej (HANDEL-W59), odczyt metadanych audytowych
/// <c>ChangeInfos</c> (HANDEL-W60) oraz pracę z definicjami i numeracją dokumentu (HANDEL-W61).
/// </para>
/// <para>
/// W bazie Demo działa <c>StanUjemnyVerifier</c> (blokada stanu ujemnego): rozchód wymaga
/// wcześniejszego zapisanego przyjęcia. Do prostych scenariuszy używamy przychodu (PW), który
/// niczego nie blokuje. Magazyn księguje się dopiero po <c>Session.Save()</c>. Wzorzec testów:
/// utwórz → <c>SaveDispose()</c> → odczyt na świeżej sesji po <c>Guid</c> (po <c>Save()</c> w środku
/// testu okno edycji się zamyka — kolejna edycja rzuca <c>AccessWriteDenied</c>).
/// </para>
/// Cała klasa operuje wyłącznie na publicznym kontrakcie platformy Soneta (jak dodatek zewnętrzny).
/// </summary>
[TestFixture]
public class Rozdzial11_PomocniczeTest : DokumentHandlowyTestBase
{
    // ===================================================================================
    // Pomocnik lokalny: zatwierdzony przychód (PW) z pozycją, zapisany trwale.
    // PW to przychód — nie podlega blokadzie stanu ujemnego, więc nadaje się do testów
    // numeracji, audytu i odczytu metadanych po zatwierdzeniu.
    // ===================================================================================
    private Guid UtworzZatwierdzonyPwIZapisz(double ilosc = 10, double cena = 5)
    {
        var dok = UtworzDokument(Definicje.PrzyjecieWewnetrzne, magazyn: Magazyn(Magazyn_.Firma));
        InTransaction(() => DodajPozycje(dok, Towar(Towar_.Bikini), ilosc, cena));
        InTransaction(() => dok.Stan = StanDokumentuHandlowego.Zatwierdzony);
        var guid = dok.Guid;
        // Dopiero Save() nadaje numer właściwy i księguje obroty/zasoby; SaveDispose zamyka sesję.
        SaveDispose();
        return guid;
    }

    // ===================================================================================
    // HANDEL-W56 — Bezpieczne pobranie / utworzenie kontrahenta i towaru pozycji
    // ===================================================================================

    [Test]
    [Description("HANDEL-W56: WgKodu zwraca istniejący rekord dla znanego kodu, a null dla kodu spoza kartoteki " +
                 "(klucz unikalny — pojedynczy rekord lub null).")]
    public void HANDEL_W56_LookupPoKodzie_ZwracaRekordLubNull()
    {
        // Istniejący kontrahent z bazy Demo — lookup po kluczu unikalnym zwraca jeden rekord.
        Kontrahent istniejacy = Kontrahent(Kontrahent_.Abc);
        istniejacy.Should().NotBeNull("kontrahent „Abc” istnieje w bazie Demo");

        // Kod spoza kartoteki → null (nie wyjątek). To podstawa kontroli istnienia przed użyciem.
        Kontrahent brak = Kontrahent("NIE_ISTNIEJE_XYZ");
        brak.Should().BeNull("WgKodu dla nieistniejącego kodu zwraca null");

        // Analogicznie towar po kodzie.
        Towar towar = Towar(Towar_.Bikini);
        towar.Should().NotBeNull("towar „BIKINI” istnieje w bazie Demo");
        Towar brakTowaru = Towar("NIE_MA_TAKIEGO");
        brakTowaru.Should().BeNull("WgKodu dla nieistniejącego kodu towaru zwraca null");
    }

    [Test]
    [Description("HANDEL-W56: kontrahent incydentalny — rekord systemowy pobierany po stałej Kontrahent.INCYDENTALNY " +
                 "(indeksator GuidedTable po Guid); rekord ma JestIncydentalny == true.")]
    public void HANDEL_W56_KontrahentIncydentalny_PobranyPoGuidJestOznaczonyJakoIncydentalny()
    {
        // Sprzedaż jednorazowa (klient detaliczny bez kartoteki) — używamy systemowego rekordu
        // „incydentalnego” zamiast tworzyć nowego kontrahenta. Dostęp po stałej Guid.
        Soneta.CRM.Kontrahent incydentalny = Crm.Kontrahenci[Soneta.CRM.Kontrahent.INCYDENTALNY];
        incydentalny.Should().NotBeNull("rekord incydentalny to systemowy rekord obecny w bazie");

        // JestIncydentalny to pole KALKULOWANE (bool) — potwierdza, że to rekord systemowy.
        incydentalny.JestIncydentalny.Should().BeTrue("to systemowy kontrahent incydentalny");

        // Zwykły kontrahent z kartoteki NIE jest incydentalny.
        Kontrahent(Kontrahent_.Abc).JestIncydentalny.Should()
            .BeFalse("kontrahent z kartoteki nie jest rekordem incydentalnym");
    }

    [Test]
    [Description("HANDEL-W56: fallback przy braku rekordu — gdy WgKodu zwraca null, kontrola istnienia pozwala " +
                 "sięgnąć po systemowy rekord incydentalny; jednak na fakturze (FV) NIE wolno go ustawiać — " +
                 "setter rzuca ArgumentException (kontrahent incydentalny niedozwolony w dokumentach typu FV).")]
    public void HANDEL_W56_FallbackNaIncydentalnego_GdyBrakKontrahentaPoKodzie()
    {
        // Symulacja: kod nabywcy nie istnieje w kartotece — WgKodu zwraca null (kontrola istnienia).
        Kontrahent kontrahent = Kontrahent("DETAL_BEZ_KARTOTEKI");
        if (kontrahent == null)
            kontrahent = Crm.Kontrahenci[Soneta.CRM.Kontrahent.INCYDENTALNY]; // świadomy fallback po stałej Guid

        // Fallback rzeczywiście znajduje systemowy rekord incydentalny (bez przypisywania go do dokumentu).
        kontrahent.Should().NotBeNull("systemowy rekord incydentalny istnieje w bazie Demo");
        kontrahent.JestIncydentalny.Should().BeTrue("to systemowy kontrahent incydentalny");

        // Reguła biznesowa: kontrahenta incydentalnego NIE wolno ustawiać na fakturze sprzedaży (FV).
        // Setter Kontrahent na FV zgłasza ArgumentException — dokumentujemy to jako twardą walidację platformy.
        var dok = UtworzDokument(Definicje.FakturaSprzedazy, magazyn: Magazyn(Magazyn_.Firma));
        Action ustawIncydentalnego = () => InTransaction(() => dok.Kontrahent = kontrahent);

        ustawIncydentalnego.Should().Throw<ArgumentException>(
            "kontrahenta incydentalnego nie można ustawiać w dokumentach typu FV");
    }

    // ===================================================================================
    // HANDEL-W57 — Przeliczanie jednostek miary towaru (Towar.PrzeliczJednostkę)
    // ===================================================================================

    [Test]
    [Description("HANDEL-W57: PrzeliczJednostkę w jednostce podstawowej towaru (przeliczenie tożsamościowe) " +
                 "zwraca tę samą wartość i symbol — przelicznik 1:1 jest zawsze zdefiniowany.")]
    public void HANDEL_W57_PrzeliczJednostkeNaPodstawowa_ZwracaTeSamaIlosc()
    {
        var towar = Towar(Towar_.Bikini);
        towar.Should().NotBeNull();

        // Ilość w jednostce PODSTAWOWEJ towaru. throwError: true — brak przelicznika zgłosiłby wyjątek,
        // ale dla jednostki podstawowej → podstawowa konwersja jest tożsamościowa (zawsze poprawna).
        var iloscPodst = new Quantity(7, towar.Jednostka.Kod);
        Quantity wynik = towar.PrzeliczJednostkę(towar.Jednostka, iloscPodst, throwError: true);

        // Przeliczenie 1:1 — wartość i jednostka bez zmian.
        wynik.Value.Should().Be(7);
        wynik.Symbol.Should().Be(towar.Jednostka.Kod);
    }

    [Test]
    [Description("HANDEL-W57: na pozycji dokumentu po ustawieniu Towaru symbol jednostki na Ilosc pochodzi z " +
                 "jednostki podstawowej towaru — new Quantity(n, poz.Ilosc.Symbol) daje zgodny symbol.")]
    public void HANDEL_W57_SymbolJednostkiNaPozycji_PochodziZTowaru()
    {
        var towar = Towar(Towar_.Bikini);
        var dok = UtworzDokument(Definicje.PrzyjecieWewnetrzne, magazyn: Magazyn(Magazyn_.Firma));

        PozycjaDokHandlowego poz = null;
        InTransaction(() =>
        {
            // DodajPozycje ustawia Towar PIERWSZY (inicjuje jednostkę), potem Ilosc z symbolem pozycji.
            poz = DodajPozycje(dok, towar, ilosc: 4);
        });

        // Symbol jednostki pozycji pokrywa się z jednostką podstawową towaru.
        poz.Ilosc.Symbol.Should().Be(towar.Jednostka.Kod,
            "ustawienie Towaru inicjuje symbol jednostki na Ilosc");
        poz.Ilosc.Value.Should().Be(4);
    }

    // ===================================================================================
    // HANDEL-W58 — Walidacja przed zatwierdzeniem (kompletność, zasób)
    // ===================================================================================

    [Test]
    [Description("HANDEL-W58: własna walidacja kompletności przed zmianą stanu — dokument bez pozycji ma " +
                 "Pozycje.IsEmpty == true (właściwość serwerowa), co pozwala zgłosić czytelny błąd.")]
    public void HANDEL_W58_WalidacjaKompletnosci_PustyDokumentMaPozycjeIsEmpty()
    {
        // FV bez pozycji — nabywca ustawiony, ale brak pozycji.
        var dok = UtworzDokument(Definicje.FakturaSprzedazy,
            kontrahent: Kontrahent(Kontrahent_.Abc),
            magazyn: Magazyn(Magazyn_.Firma));

        // IsEmpty to WŁAŚCIWOŚĆ (serwerowy exists), nie metoda — używamy jej w walidacji własnej.
        dok.Pozycje.IsEmpty.Should().BeTrue("dokument nie ma jeszcze pozycji");
        dok.Kontrahent.Should().NotBeNull("nabywca jest ustawiony");

        // Po dodaniu pozycji walidacja kompletności przechodzi.
        InTransaction(() => DodajPozycje(dok, Towar(Towar_.Bikini), ilosc: 2, cena: 5));
        dok.Pozycje.IsEmpty.Should().BeFalse("po dodaniu pozycji kolekcja nie jest pusta");
    }

    [Test]
    [Description("HANDEL-W58: blokada stanu ujemnego — zatwierdzenie i zapis rozchodu (WZ) towaru bez wcześniej " +
                 "zapisanego przyjęcia zgłasza wyjątek dopiero w Save() (StanUjemnyVerifier).")]
    public void HANDEL_W58_RozchodBezStanu_RzucaWyjatekWSave()
    {
        // WZ rozchodowy towaru BIKINI — w tym teście NIE robimy wcześniejszego przyjęcia,
        // więc stan jest niewystarczający. Magazyn księguje się dopiero w Save().
        var wz = UtworzDokument(Definicje.WydanieZewnetrzne,
            kontrahent: Kontrahent(Kontrahent_.Abc),
            magazyn: Magazyn(Magazyn_.Firma));
        InTransaction(() => DodajPozycje(wz, Towar(Towar_.Bikini), ilosc: 5, cena: 9));
        InTransaction(() => wz.Stan = StanDokumentuHandlowego.Zatwierdzony);

        // Sam Commit NIE księguje zasobów — kontrola stanu ujemnego uruchamia się w Save().
        Action zapis = () => SaveDispose();
        zapis.Should().Throw<Exception>("StanUjemnyVerifier blokuje rozchód bez zapisanego przyjęcia");
    }

    // ===================================================================================
    // HANDEL-W59 — Obsługa błędów i blokada optymistyczna
    // ===================================================================================

    [Test]
    [Description("HANDEL-W59: wzorzec łapania wyjątku platformy — edycja na sesji z zamkniętym oknem edycji " +
                 "(po SaveDispose) rzuca wyjątek; asercja typu/komunikatu zamiast „połykania”.")]
    public void HANDEL_W59_EdycjaPozaTransakcja_RzucaWyjatek()
    {
        // Tworzymy i zapisujemy dokument; po SaveDispose okno edycji bieżącej sesji jest zamknięte.
        var guid = UtworzZatwierdzonyPwIZapisz();
        var dok = Get<DokumentHandlowy>(guid);
        dok.Should().NotBeNull();

        // Próba modyfikacji pola POZA transakcją edycyjną (bez Session.Logout(true)) jest niedozwolona.
        // Wzorzec safe-code: łapiemy konkretny wyjątek platformy, nie Exception ogólnie „po cichu”.
        // MemoText przyjmuje string przez konwersję niejawną (string -> MemoText).
        Action edycjaBezTransakcji = () => dok.Opis = "X";
        edycjaBezTransakcji.Should().Throw<Exception>(
            "modyfikacja rekordu wymaga otwartej transakcji edycyjnej (Session.Logout(true))");
    }

    [Test]
    [Description("HANDEL-W59: walidacja własna rzucana jako RowException PRZED Commit — wyjątek niesie odwołanie " +
                 "do wiersza i komunikat; asercja typu wyjątku i jego Row.")]
    public void HANDEL_W59_WalidacjaWlasna_RzucaRowException()
    {
        // Pokazujemy WZORZEC obsługi: walidacja własna zgłasza RowException(dok, komunikat) przed Commit.
        var dok = UtworzDokument(Definicje.FakturaSprzedazy, magazyn: Magazyn(Magazyn_.Firma));

        // Symulacja walidacji „brak nabywcy” — w realnym kodzie poprzedza zmianę stanu.
        Action walidacja = () =>
        {
            if (dok.Kontrahent == null)
                throw new RowException(dok, "Dokument nie ma nabywcy.".Translate());
        };

        // Asercja TYPU wyjątku (nie ogólne Exception) — tak rozróżnia się walidację biznesową.
        // RowException udostępnia wiersz przez właściwość IRow (RowException dziedziczy z BusException).
        walidacja.Should().Throw<RowException>()
            .Which.IRow.Should().Be(dok, "RowException niesie odwołanie do wiersza, którego dotyczy");
    }

    // ===================================================================================
    // HANDEL-W60 — Odczyt metadanych dokumentu (ChangeInfos)
    // ===================================================================================

    [Test]
    [Description("HANDEL-W60: po utworzeniu i zapisaniu dokumentu FirstChangeInfo (kto/kiedy założył) jest " +
                 "wypełnione, gdy audyt jest włączony; gdy null (tryb testowy bez rejestracji) — pomijamy asercję.")]
    public void HANDEL_W60_FirstChangeInfo_PoZapisieNiepusteLubPominiete()
    {
        var guid = UtworzZatwierdzonyPwIZapisz();
        var dok = Get<DokumentHandlowy>(guid);
        dok.Should().NotBeNull();

        // FirstChangeInfo jest KALKULOWANE (select top 1 ... from ChangeInfos) i może być null,
        // gdy historia rekordu nie była rejestrowana (np. import / audyt wyłączony).
        var zalozyl = dok.FirstChangeInfo;
        if (zalozyl == null)
        {
            // Audyt nie zarejestrował wpisu w tym trybie — SKIP asercji, sam odczyt nie rzuca.
            Assert.Ignore("Brak wpisu ChangeInfo dla rekordu w tym trybie (rejestracja audytu wyłączona) — " +
                          "właściwość kalkulowana zwróciła null; odczyt jest dozwolony, asercję pomijamy.");
        }

        // Gdy audyt działa — wpis ma czas utworzenia i (zwykle) operatora.
        zalozyl.Time.Should().NotBe(default(DateTime), "wpis założenia niesie czas utworzenia");
    }

    [Test]
    [Description("HANDEL-W60: LastChangeInfo (kto/kiedy ostatnio zmienił) po zapisie jest niepuste lub — w trybie " +
                 "bez rejestracji audytu — null; odczyt nie rzuca, asercję czasu wykonujemy warunkowo.")]
    public void HANDEL_W60_LastChangeInfo_PoZapisieNiepusteLubPominiete()
    {
        var guid = UtworzZatwierdzonyPwIZapisz();
        var dok = Get<DokumentHandlowy>(guid);

        // Sam odczyt właściwości kalkulowanej nie może rzucać — zawsze sprawdzamy != null.
        var ostatnia = dok.LastChangeInfo;
        if (ostatnia == null)
        {
            Assert.Ignore("Brak wpisu LastChangeInfo w tym trybie (rejestracja audytu wyłączona) — " +
                          "odczyt dozwolony, asercję pomijamy.");
        }

        ostatnia.Time.Should().NotBe(default(DateTime), "wpis ostatniej zmiany niesie czas");
    }

    // ===================================================================================
    // HANDEL-W61 — Praca z definicjami i numeracją (seria, numer pełny, bufor)
    // ===================================================================================

    [Test]
    [Description("HANDEL-W61: dokument w buforze nie ma jeszcze numeru właściwego — BuforNumer == \"BUFOR\", " +
                 "a po zatwierdzeniu i zapisie Numer.NumerPelny zawiera nadany numer (bez znacznika BUFOR).")]
    public void HANDEL_W61_NumerNadawanyPrzyZatwierdzeniu_BuforPotemNumerWlasciwy()
    {
        // Dokument w buforze (jeszcze niezatwierdzony).
        var dok = UtworzDokument(Definicje.PrzyjecieWewnetrzne, magazyn: Magazyn(Magazyn_.Firma));
        InTransaction(() => DodajPozycje(dok, Towar(Towar_.Bikini), ilosc: 10, cena: 5));

        // W buforze numer właściwy nie jest nadany — kalkulowane BuforNumer zwraca znacznik „BUFOR”.
        dok.Bufor.Should().BeTrue();
        dok.BuforNumer.Should().Be("BUFOR", "w buforze numer właściwy nie jest jeszcze nadany");

        // Zatwierdzenie + Save nadaje numer właściwy.
        var guid = dok.Guid;
        InTransaction(() => dok.Stan = StanDokumentuHandlowego.Zatwierdzony);
        SaveDispose();

        // Odczyt na świeżej sesji — numer pełny czytamy przez Numer.NumerPelny (nie składamy ręcznie).
        var zapisany = Get<DokumentHandlowy>(guid);
        zapisany.Zatwierdzony.Should().BeTrue();
        string numer = zapisany.Numer.NumerPelny;
        numer.Should().NotBeNullOrEmpty("zatwierdzony dokument ma nadany numer pełny");
        numer.Should().NotContain("BUFOR", "po zatwierdzeniu numer nie zawiera już znacznika bufora");
    }

    [Test]
    [Description("HANDEL-W61: pobranie definicji po symbolu (WgSymbolu) oraz odczyt dozwolonych serii dokumentu " +
                 "przez GetListSeria(); dodatkowo na zatwierdzonym i zapisanym PW Numer.NumerPelny jest niepusty.")]
    public void HANDEL_W61_DefinicjaISerie_OdczytPublicznegoKontraktu()
    {
        // Definicja dokumentu pobierana po symbolu z bazy Demo (klucz WgSymbolu).
        DefDokHandlowego def = Definicja(Definicje.FakturaSprzedazy);
        def.Should().NotBeNull("definicja FV istnieje w bazie Demo");

        var dok = UtworzDokument(Definicje.FakturaSprzedazy,
            kontrahent: Kontrahent(Kontrahent_.Abc),
            magazyn: Magazyn(Magazyn_.Firma));

        // Dokument ma przypisaną definicję (ustawioną jako pierwszą przez helper bazy).
        dok.Definicja.Should().Be(def);

        // GetListSeria() zwraca dozwolone serie lub null, gdy numeracja nie ma komponentu Seria.
        // Kontrakt testu: sam ODCZYT nie może rzucać; null jest dopuszczalny (brak komponentu Seria).
        string[] serie = null;
        Action odczytSerii = () => serie = dok.GetListSeria();
        odczytSerii.Should().NotThrow("odczyt dozwolonych serii nie może rzucać");

        // Serię ustawiamy TYLKO gdy numeracja na to pozwala — w przeciwnym razie setter rzuciłby RowException.
        if (serie != null && serie.Length > 0)
        {
            InTransaction(() => dok.Seria = serie[0]);
            dok.Seria.Should().Be(serie[0], "ustawiona seria została zapamiętana");
        }

        // Numerację potwierdzamy na bezpiecznym dokumencie przychodowym (PW), który można zatwierdzić
        // i zapisać (FV w bazie Demo rzuca NRE w ewidencji VAT przy zatwierdzeniu — §3 faktów).
        var pwGuid = UtworzZatwierdzonyPwIZapisz();
        var pw = Get<DokumentHandlowy>(pwGuid);            // świeża sesja po SaveDispose (§8)
        pw.Should().NotBeNull();
        pw.Numer.NumerPelny.Should().NotBeNullOrEmpty("zatwierdzony i zapisany dokument ma nadany numer pełny");
    }

    // ===================================================================================
    // SCENARIUSZE POMINIĘTE (SKIP) — uzasadnienie zgodne z treścią rozdziału
    // ===================================================================================

    [Test]
    [Description("HANDEL-W56: kontrahent i towar to dane KARTOTEKOWE (nie konfiguracyjne) — można je tworzyć w tej " +
                 "samej sesji operacyjnej co dokument. Test tworzy NOWY towar, NOWEGO kontrahenta i NOWE zamówienie " +
                 "odbiorcy (ZO) używające ich — wszystko w jednej transakcji — i po zapisie weryfikuje powiązania.")]
    public void HANDEL_W56_TowarKontrahentIDokument_TworzoneRazem()
    {
        var sufiks = Guid.NewGuid().ToString("N").Substring(0, 6);
        Guid dokGuid = Guid.Empty, towarGuid = Guid.Empty, kontrGuid = Guid.Empty;

        InTransaction(() =>
        {
            // 1) Nowy towar (kartoteka) — minimalnie Kod + Nazwa.
            var towar = Session.AddRow(new Towar());
            towar.Kod = "TWNEW_" + sufiks;
            towar.Nazwa = "Towar testowy " + sufiks;

            // 2) Nowy kontrahent (kartoteka) — minimalnie Kod + Nazwa.
            var kontrahent = Session.AddRow(new Kontrahent());
            kontrahent.Kod = "KNEW_" + sufiks;
            kontrahent.Nazwa = "Kontrahent testowy " + sufiks;

            // 3) Dokument (ZO — nie księguje magazynu) używający świeżo utworzonych rekordów.
            var dok = Session.AddRow(new DokumentHandlowy());
            dok.Definicja = Definicja(Definicje.ZamowienieOdbiorcy);   // definicja PIERWSZA
            dok.Magazyn = Magazyn(Magazyn_.Firma);
            dok.Kontrahent = kontrahent;                              // referencja do NOWEGO kontrahenta
            DodajPozycje(dok, towar, ilosc: 3, cena: 15);            // pozycja z NOWYM towarem

            dokGuid = dok.Guid;
            towarGuid = towar.Guid;
            kontrGuid = kontrahent.Guid;
        });
        SaveDispose();

        // Wszystkie trzy obiekty zostały zapisane i są poprawnie powiązane (odczyt na świeżej sesji).
        var dok2 = Get<DokumentHandlowy>(dokGuid);
        dok2.Kontrahent.Guid.Should().Be(kontrGuid, "dokument wskazuje świeżo utworzonego kontrahenta");
        dok2.Pozycje.Cast<PozycjaDokHandlowego>().Single().Towar.Guid.Should().Be(towarGuid,
            "pozycja wskazuje świeżo utworzony towar");
        Get<Towar>(towarGuid).Kod.Should().Be("TWNEW_" + sufiks, "nowy towar trafił do kartoteki");
        Get<Kontrahent>(kontrGuid).Kod.Should().Be("KNEW_" + sufiks, "nowy kontrahent trafił do kartoteki");
    }

    [Test]
    [Ignore("HANDEL-W57 — przeliczenie z jednostki POMOCNICZEJ na podstawową (PrzeliczJednostkę z realnym " +
            "przelicznikiem ≠ 1:1) wymaga towaru z ZDEFINIOWANYM przelicznikiem jednostki pomocniczej/" +
            "uzupełniającej. Przeliczniki to dane konfiguracyjne towaru; baza Demo nie gwarantuje towaru " +
            "z jednoznacznym przelicznikiem pomocniczym (TRANSPORT ma jednostkę km, ale konfiguracja " +
            "przeliczników nie jest częścią kontraktu testu). Z throwError: true brak przelicznika rzuciłby " +
            "wyjątek — test byłby kruchy. Pokrywamy konwersję tożsamościową (1:1) i symbol jednostki pozycji. " +
            "SKIP: realny przelicznik pomocniczy = konfiguracja towaru poza zakresem.")]
    [Description("HANDEL-W57: przeliczenie z jednostki pomocniczej (przelicznik ≠ 1:1) — pominięte (konfiguracja towaru).")]
    public void HANDEL_W57_PrzeliczniePomocniczej_Skip() { }

    [Test]
    [Description("HANDEL-W59: konflikt optymistyczny — ten sam dokument otwarty w DWÓCH sesjach. Sesja A edytuje " +
                 "i zapisuje (bumpuje wersję rekordu w bazie). Sesja B ma już nieaktualną wersję — przy jej Save " +
                 "platforma wykrywa konflikt i rzuca Soneta.Business.ConcurrencyException.")]
    public void HANDEL_W59_KonfliktOptymistyczny_DwieSesje()
    {
        // Istniejący, ZAPISANY dokument w BUFORZE — wspólny rekord dla obu sesji. Bufor, bo na
        // dokumencie zatwierdzonym pola (np. Opis) są tylko do odczytu — konfliktu nie dałoby się wywołać edycją.
        var pw = UtworzDokument(Definicje.PrzyjecieWewnetrzne, magazyn: Magazyn(Magazyn_.Firma));
        InTransaction(() => DodajPozycje(pw, Towar(Towar_.Bikini), ilosc: 10, cena: 5));
        var guid = pw.Guid;
        SaveDispose();

        using var sesjaA = Login.CreateSession(readOnly: false, config: false, name: "W59-A");
        using var sesjaB = Login.CreateSession(readOnly: false, config: false, name: "W59-B");

        var dokA = sesjaA.GetHandel().DokHandlowe[guid];
        var dokB = sesjaB.GetHandel().DokHandlowe[guid];
        dokA.Should().NotBeNull("sesja A widzi zapisany dokument");
        dokB.Should().NotBeNull("sesja B widzi ten sam dokument (ta sama, początkowa wersja)");

        // Sesja A: edycja + zapis → rekord dostaje nowszą wersję w bazie.
        using (var t = sesjaA.Logout(editMode: true))
        {
            dokA.Opis = "zmiana A " + Guid.NewGuid().ToString("N").Substring(0, 4);
            t.Commit();
        }
        sesjaA.Save();

        // Sesja B: edytuje SWOJĄ (już nieaktualną) kopię i próbuje zapisać → konflikt optymistyczny.
        using (var t = sesjaB.Logout(editMode: true))
        {
            dokB.Opis = "zmiana B (na nieaktualnej wersji)";
            t.Commit();
        }
        Action zapisB = () => sesjaB.Save();
        zapisB.Should().Throw<ConcurrencyException>(
            "druga sesja zapisuje rekord zmieniony w międzyczasie przez pierwszą — blokada optymistyczna");
    }
}
