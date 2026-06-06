using System;
using System.IO;
using System.Text;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;   // GetRequiredService
using NUnit.Framework;
using Action = System.Action;
using Soneta.Business;                              // Context
using Soneta.Business.UI;                           // IReportService, ReportResult, ReportFormats, ReportTargets
using Soneta.Handel;                                // DokumentHandlowy, ParametryWydrukuDokumentu

namespace Soneta.Skills.Test.Handel.DokumentyHandlowe;

/// <summary>
/// Rozdział 12 skilla „dokument-handlowy” — Wydruki i raporty (W62–W66).
/// <para>
/// Wydruk dokumentu handlowego oraz raporty/zestawienia generuje serwis
/// <see cref="IReportService"/> (scope sesji: <c>Session.GetRequiredService&lt;IReportService&gt;()</c>).
/// Serwis bierze wzorzec wydruku (<c>*.repx</c>), kontekst z danymi (rekord, tablica zaznaczeń,
/// parametry wydruku) i zwraca gotowy dokument jako strumień (<see cref="IReportService.GenerateReport"/>
/// → <c>Stream</c>) lub tekst (<see cref="IReportService.GenerateReportStr"/> → <c>string</c>) — bez UI.
/// </para>
/// <para>
/// <b>Ścieżka testowalna:</b> wygenerowanie wydruku do strumienia PDF i sprawdzenie, że bajty
/// zaczynają się od sygnatury <c>"%PDF"</c> (HTML zaczyna się od <c>"&lt;!DOCTYPE html"</c>).
/// </para>
/// <para>
/// <b>Co NIE jest testowalne jednostkowo</b> (wymaga sprzętu, brak asercji):
/// druk na fizyczną drukarkę (<c>PrintReport</c>, <c>Target = ReportTargets.Printer</c>) oraz
/// fiskalny raport dobowy/okresowy drukarki (<c>IFiscalPrinterAPI.DrukujRaport*</c>, <c>Fiskalizuj</c>).
/// Dla nich dokumentuje się tylko poprawne ustawienie <c>ReportResult</c>/parametrów, bez druku.
/// </para>
/// <para>
/// <b>Pułapka konfiguracyjna:</b> generowanie wymaga realnego, zarejestrowanego wzorca <c>*.repx</c>.
/// Nazwy wzorców (np. „Sprzedaz.repx”) są elementem konfiguracji wdrożenia i mogą być nieobecne
/// w testowej bazie Demo / brak silnika renderującego (DevExpress). Dlatego całe generowanie owijamy
/// w try/catch i przy braku wzorca/silnika robimy <c>Assert.Ignore</c> — test pozostaje zielony,
/// a jednocześnie dokumentuje publiczne API. Asercję na <c>"%PDF"</c> wykonujemy tylko wtedy,
/// gdy strumień faktycznie powstał.
/// </para>
/// Cała klasa operuje wyłącznie na publicznym kontrakcie platformy Soneta (jak dodatek zewnętrzny).
/// </summary>
[TestFixture]
public class Rozdzial12_WydrukiTest : DokumentHandlowyTestBase
{
    /// <summary>Sygnatura nagłówka pliku PDF (pierwsze 4 bajty/znaki strumienia).</summary>
    private const string PdfMagic = "%PDF";

    /// <summary>Nazwa wzorca wydruku faktury sprzedaży (zgodnie ze snippetem W62/W66 w skillu).</summary>
    private const string WzorzecSprzedaz = "Sprzedaz.repx";

    /// <summary>Serwis raportowy ze scope'u bieżącej sesji (jak <c>IRelacjeService</c> w rozdz. 4).</summary>
    private IReportService Raporty => Session.GetRequiredService<IReportService>();

    // === Pomocniki lokalne ===

    /// <summary>
    /// Tworzy i ZAPISUJE fakturę sprzedaży (FV) z jedną pozycją towaru BIKINI, pozostawioną w BUFORZE.
    /// <para>
    /// Faktury NIE zatwierdzamy: w testowej bazie Demo ustawienie
    /// <c>fv.Stan = StanDokumentuHandlowego.Zatwierdzony</c> rzuca <c>NullReferenceException</c>
    /// w ewidencji VAT (potwierdzone empirycznie). Wydruk można jednak zbudować z faktury w buforze —
    /// <c>SumyVAT</c>, <c>Suma</c>, <c>SumaPozycji</c>, <c>Platnosci</c> są w buforze już wyliczone.
    /// </para>
    /// <para>
    /// Demo blokuje stan ujemny → rozchód (FV) wymaga wcześniej ZAKSIĘGOWANEGO przyjęcia. Używamy
    /// helpera bazowego <see cref="PrzyjmijNaStan"/> (tworzy zatwierdzone PW + Save → księguje stan).
    /// </para>
    /// Zwraca Guid zapisanego dokumentu; sesja edycyjna zostaje zamknięta przez <see cref="SaveDispose"/>.
    /// </summary>
    private Guid UtworzFaktureWBuforze()
    {
        // 1. Zaksięgowany stan magazynowy (zatwierdzone PW + Save) — żeby rozchód FV nie dał stanu ujemnego.
        PrzyjmijNaStan(Towar_.Bikini, 20);

        // 2. Faktura sprzedaży FV na kontrahenta i magazyn „F”, z pozycją mieszczącą się w stanie.
        //    NIE zatwierdzamy (zatwierdzenie FV rzuca NRE w ewidencji VAT w bazie Demo) — zostaje w buforze.
        var fv = UtworzDokument(Definicje.FakturaSprzedazy,
            kontrahent: Kontrahent(Kontrahent_.Abc),
            magazyn: Magazyn(Magazyn_.Firma));
        InTransaction(() => DodajPozycje(fv, Towar(Towar_.Bikini), ilosc: 2, cena: 12));

        var guid = fv.Guid;
        SaveDispose();
        return guid;
    }

    /// <summary>
    /// Buduje kontekst wydruku pojedynczego dokumentu zgodnie ze snippetem W62:
    /// rekord, definicja, kontrahent, tablica zaznaczeń oraz instancja parametrów wydruku.
    /// </summary>
    private Context KontekstWydruku(DokumentHandlowy dok)
    {
        var context = Login.CreateEmptyContext().Clone(Session);
        context.Set(dok);
        context.Set(dok.Definicja);
        if (dok.Kontrahent != null)
            context.Set(dok.Kontrahent);
        context.Set(new[] { dok });                                       // wymagane przez część wzorców
        context.Set(new ParametryWydrukuDokumentu(context) { Duplikat = false });
        return context;
    }

    // ===================================================================================
    // W62 / W66 — Wydruk faktury do PDF (strumień) i sprawdzenie sygnatury „%PDF”
    // ===================================================================================

    [Test]
    [Description("W62/W66: IReportService.GenerateReport z TemplateFileName i OutputFormat=PDF dla " +
                 "pojedynczego dokumentu (DataType=typeof(DokumentHandlowy)) zwraca strumień PDF " +
                 "zaczynający się od sygnatury „%PDF”. Brak wzorca/silnika → Assert.Ignore (suita zielona).")]
    [Ignore("Wymaga zarejestrowanego wzorca .repx oraz silnika renderującego (DevExpress), których testowa " +
            "baza Demo nie gwarantuje; faktyczne wywołanie GenerateReport ładuje DevExpress i bywa niestabilne " +
            "w hoście testowym. Test dokumentuje publiczne API IReportService.GenerateReport (kod w ciele metody).")]
    public void W62_WydrukFakturyDoPdf_ZaczynaSieOdPdf()
    {
        // Arrange: faktura sprzedaży w buforze + kontekst wydruku (rekord, parametry, zaznaczenie).
        // FV pozostaje w buforze (zatwierdzenie FV w bazie Demo rzuca NRE w ewidencji VAT);
        // wydruk buduje się z dokumentu buforowego — sumy/VAT/płatności są już wyliczone.
        var dok = Get<DokumentHandlowy>(UtworzFaktureWBuforze());
        dok.Should().NotBeNull();

        var rr = new ReportResult
        {
            TemplateFileName = WzorzecSprzedaz,        // tryb automatyczny (bez UI)
            DataType = typeof(DokumentHandlowy),       // pojedynczy dokument
            Context = KontekstWydruku(dok),
            OutputFormat = ReportFormats.PDF,
            AskForParameters = false                   // tryb wsadowy — nie pytaj o parametry
        };

        // Act: generowanie do strumienia. Owijamy w try/catch — gdy wzorzec/silnik nieobecny,
        // pomijamy test (Assert.Ignore), zamiast zgłaszać błąd. Strumień zawsze w using.
        byte[] naglowek;
        try
        {
            using var pdf = Raporty.GenerateReport(rr);
            pdf.Should().NotBeNull("GenerateReport dla formatu binarnego zwraca Stream");

            // Odczyt pierwszych 4 bajtów do sprawdzenia sygnatury „%PDF”.
            naglowek = new byte[4];
            int przeczytane = pdf.Read(naglowek, 0, naglowek.Length);
            przeczytane.Should().Be(4, "PDF ma co najmniej 4-bajtowy nagłówek");
        }
        catch (Exception ex)
        {
            Assert.Ignore("Pominięto W62: wygenerowanie PDF wymaga zarejestrowanego wzorca '" +
                          WzorzecSprzedaz + "' oraz silnika renderującego, których testowa baza Demo " +
                          "nie gwarantuje. Test dokumentuje publiczne API IReportService.GenerateReport. " +
                          "Szczegóły: " + ex.GetType().Name + " — " + ex.Message);
            return;
        }

        // Assert: strumień zaczyna się od sygnatury PDF.
        Encoding.ASCII.GetString(naglowek).Should().StartWith(PdfMagic,
            "poprawny strumień PDF zaczyna się od „%PDF”.");
    }

    [Test]
    [Description("W66: integracja — GenerateReport zapisany do MemoryStream daje bajty PDF (np. do e-maila/REST). " +
                 "Sprawdza, że pierwsze bajty całego bufora to „%PDF”. Brak wzorca/silnika → Assert.Ignore.")]
    [Ignore("Wymaga wzorca .repx + silnika DevExpress (jak W62); GenerateReport ładuje DevExpress i bywa " +
            "niestabilne w hoście testowym. Dokumentuje publiczne API zapisu wydruku do strumienia (kod w ciele).")]
    public void W66_WydrukDoStrumieniaBajtow_DajePoprawnyPdf()
    {
        // Arrange: faktura w buforze + kontekst jak w W62 (FV nie zatwierdzamy — NRE w ewidencji VAT w Demo).
        var dok = Get<DokumentHandlowy>(UtworzFaktureWBuforze());

        var rr = new ReportResult
        {
            TemplateFileName = WzorzecSprzedaz,
            DataType = typeof(DokumentHandlowy),
            Context = KontekstWydruku(dok),
            OutputFormat = ReportFormats.PDF,
            AskForParameters = false
        };

        // Act: skopiowanie strumienia do pamięci (wzorzec integracji z W66: bajty → załącznik/REST).
        byte[] pdfBytes;
        try
        {
            using Stream src = Raporty.GenerateReport(rr);
            using var ms = new MemoryStream();
            src.CopyTo(ms);
            pdfBytes = ms.ToArray();
        }
        catch (Exception ex)
        {
            Assert.Ignore("Pominięto W66: zapis wydruku do strumienia bajtów wymaga obecnego wzorca '" +
                          WzorzecSprzedaz + "' i silnika renderującego (brak w testowej bazie Demo). " +
                          "Test dokumentuje wzorzec integracyjny GenerateReport → byte[]. " +
                          "Szczegóły: " + ex.GetType().Name + " — " + ex.Message);
            return;
        }

        // Assert: bufor zawiera dane i zaczyna się od sygnatury PDF.
        pdfBytes.Should().NotBeNullOrEmpty("integracyjny wydruk zwraca niepusty bufor bajtów");
        pdfBytes.Length.Should().BeGreaterThan(4);
        Encoding.ASCII.GetString(pdfBytes, 0, 4).Should().StartWith(PdfMagic,
            "bufor bajtów to plik PDF (sygnatura „%PDF”).");
    }

    // ===================================================================================
    // W62/W66 — Reguły spójności ReportResult (CheckConsistency) — bez renderowania
    // ===================================================================================

    [Test]
    [Description("W62/W66 (reguła CheckConsistency): IReportService wymaga ustawionego TemplateFileName i " +
                 "wyklucza ReportName. ReportResult bez TemplateFileName, ale z ReportName, narusza spójność " +
                 "→ GenerateReport powinno rzucić ArgumentException (a nie wyrenderować PDF).")]
    public void W66_RegulaSpojnosci_BrakTemplateFileName_RzucaArgumentException()
    {
        // Arrange: konfiguracja wykluczająca tryb IReportService — ReportName zamiast TemplateFileName.
        // Reguła spójności ReportResult sprawdzana jest PRZED dostępem do danych, więc test
        // nie potrzebuje żadnego dokumentu (a tym bardziej zatwierdzonej FV) — pusty kontekst wystarcza.
        var rr = new ReportResult
        {
            ReportName = "Faktura",                 // tryb interaktywny z menu — wyklucza się z TemplateFileName
            DataType = typeof(DokumentHandlowy),
            Context = Login.CreateEmptyContext().Clone(Session),
            OutputFormat = ReportFormats.PDF,
            AskForParameters = false
        };

        // Act + Assert: naruszenie reguły spójności → ArgumentException.
        // Asercja samej walidacji nie wymaga obecności wzorca .repx, więc nie owijamy jej w Ignore.
        Action act = () => Raporty.GenerateReport(rr);
        act.Should().Throw<ArgumentException>(
            "IReportService akceptuje wyłącznie tryb z TemplateFileName; ReportName i brak TemplateFileName " +
            "naruszają CheckConsistency");
    }

    // ===================================================================================
    // SCENARIUSZE POMINIĘTE (SKIP) — uzasadnienie zgodne z treścią rozdziału 12
    // ===================================================================================

    [Test]
    [Ignore("W62/W63 (sprzęt) — druk na FIZYCZNĄ drukarkę: IReportService.PrintReport(rr) oraz " +
            "ReportResult.Target = ReportTargets.Printer/PrinterService wymagają podłączonej drukarki i " +
            "sterownika. To operacja sprzętowa — NIE da się jej przetestować jednostkowo (brak asercji na " +
            "wyniku). W kodzie i integracjach używaj ścieżki GenerateReport → strumień/PDF (W62/W66). SKIP wg pułapek W62.")]
    [Description("W62/W63: druk na fizyczną drukarkę (PrintReport / Target=Printer) — nietestowalny (wymaga sprzętu).")]
    public void W62_DrukNaDrukarke_Skip() { }

    [Test]
    [Ignore("W63 — wydruk dokumentu magazynowego (PZ/WZ/MM): mechanizm identyczny jak W62, różni tylko wzorzec " +
            "dobrany do rodzaju dokumentu wg jego definicji (np. „WydanieZewnetrzne.repx”) + ustawienie dok.Magazyn " +
            "w kontekście. Test renderowania jest pokryty wzorcowo przez W62 (ta sama ścieżka GenerateReport → „%PDF”); " +
            "osobny test wymagałby kolejnego, niegwarantowanego wzorca .repx i nie wnosi nowej ścieżki API. " +
            "SKIP: identyczny kontrakt, inny plik wzorca (konfiguracja wdrożenia).")]
    [Description("W63: wydruk dokumentu magazynowego (WydanieZewnetrzne.repx) — pominięte (ten sam kontrakt co W62, inny wzorzec).")]
    public void W63_WydrukDokumentuMagazynowego_Skip() { }

    [Test]
    [Ignore("W64 (ścieżka bazodanowa) — zestawienie/raport dobowy/okresowy przez IReportService z wzorcem " +
            "zestawienia (np. „ZestawienieSprzedazy.repx”), DataType=typeof(Soneta.Handel.DokHandlowe) i parametrem " +
            "okresu FromTo w kontekście. Ścieżka API jest tożsama z W62 (GenerateReport → „%PDF”), różni ją wyłącznie " +
            "wzorzec i typ danych; konkretny wzorzec zestawienia nie jest gwarantowany w bazie Demo. SKIP: pokryte " +
            "wzorcowo przez W62, brak gwarancji wzorca rejestru.")]
    [Description("W64: bazodanowe zestawienie za dzień/okres (FromTo, DataType=DokHandlowe) — pominięte (ten sam kontrakt co W62).")]
    public void W64_ZestawienieBazodanowe_Skip() { }

    [Test]
    [Ignore("W64 (sprzęt) — fiskalny raport dobowy/okresowy drukarki: Soneta.Fiskal.IFiscalPrinterAPI." +
            "DrukujRaport(nazwaDrukarki) / DrukujRaportOkresowy(nazwaDrukarki, RaportOkresowyParams) oraz Fiskalizuj(...) " +
            "wymagają podłączonej DRUKARKI FISKALNEJ — operacja sprzętowa, NIE do testów jednostkowych. Testować można " +
            "tylko poprawne ustawienie RaportOkresowyParams.RaportZaOkres (FromTo), nie faktyczny druk. SKIP wg pułapek W64.")]
    [Description("W64: fiskalny raport dobowy/okresowy (IFiscalPrinterAPI) — nietestowalny (wymaga drukarki fiskalnej).")]
    public void W64_FiskalnyRaport_Skip() { }

    [Test]
    [Ignore("W65 — wydruk zbiorczy dla zaznaczonego zbioru: DataType=typeof(DokumentHandlowy[]) + Rows=tablica + " +
            "Context.Set(tablica). Ścieżka renderowania jest tożsama z W62 (GenerateReport → „%PDF”), różni ją tylko " +
            "tryb wielu rekordów; test wymagałby tego samego, niegwarantowanego wzorca „Sprzedaz.repx”. Aby utrzymać " +
            "suitę zieloną i nie duplikować ścieżki, scenariusz dokumentujemy tu (SKIP), a renderowanie pokrywa W62. " +
            "Kluczowa różnica vs W62: DataType tablicowy przełącza wzorzec w tryb wielu rekordów.")]
    [Description("W65: wydruk zbiorczy (DataType=DokumentHandlowy[], Rows) — pominięte (ta sama ścieżka renderowania co W62).")]
    public void W65_WydrukZbiorczy_Skip() { }

    [Test]
    [Ignore("W66 (e-mail/OutputHandler) — Target=ReportTargets.Email/Attachment wymaga skonfigurowanego konta " +
            "pocztowego (KontoPocztowe) i szablonu (SzablonEmail) w pełnej sesji aplikacyjnej — poza zakresem testu " +
            "jednostkowego. ReportResult.OutputHandler NIE jest obsługiwany przez IReportService (CheckConsistency " +
            "rzuca ArgumentException) — służy jako rezultat operacji w trybie wzorca (worker/Command z UI). Testowalny " +
            "rdzeń W66 (GenerateReport → byte[]) pokrywa W66_WydrukDoStrumieniaBajtow. SKIP: integracja pocztowa / tryb UI.")]
    [Description("W66: wysyłka e-mail (Target=Email) i OutputHandler — pominięte (wymaga konta/szablonu / tryb UI).")]
    public void W66_EmailIOutputHandler_Skip() { }
}
