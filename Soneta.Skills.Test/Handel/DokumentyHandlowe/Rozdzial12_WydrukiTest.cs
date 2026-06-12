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
/// Rozdział 12 skilla „dokument-handlowy” — Wydruki i raporty (HANDEL-W62–HANDEL-W66).
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
    // Wspólne helpery wydruków (Raporty, WzorzecSprzedaz, PdfMagic, KontekstWydruku,
    // WypelnijParametryWydruku, UtworzFaktureWBuforze) są w DokumentHandlowyTestBase.

    // ===================================================================================
    // HANDEL-W62 / HANDEL-W66 — Wydruk faktury do PDF (strumień) i sprawdzenie sygnatury „%PDF”
    // ===================================================================================

    [Test]
    [Description("HANDEL-W62/HANDEL-W66: IReportService.GenerateReport z TemplateFileName i OutputFormat=PDF dla " +
                 "pojedynczego dokumentu (DataType=typeof(DokumentHandlowy)) zwraca strumień PDF " +
                 "zaczynający się od sygnatury „%PDF”.")]
    public void HANDEL_W62_WydrukFakturyDoPdf_ZaczynaSieOdPdf()
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
            AskForParameters = false,                  // tryb wsadowy — nie pytaj o parametry
            ParametersHandler = WypelnijParametryWydruku   // wypełnij wymagane parametry wzorca
        };

        // Act: generowanie do strumienia (silnik DevExpress renderuje wzorzec .repx do PDF).
        using var pdf = Raporty.GenerateReport(rr);
        pdf.Should().NotBeNull("GenerateReport dla formatu binarnego (PDF) zwraca Stream");

        // Odczyt pierwszych 4 bajtów do sprawdzenia sygnatury „%PDF”.
        var naglowek = new byte[4];
        int przeczytane = pdf.Read(naglowek, 0, naglowek.Length);
        przeczytane.Should().Be(4, "PDF ma co najmniej 4-bajtowy nagłówek");

        // Assert: strumień zaczyna się od sygnatury PDF.
        Encoding.ASCII.GetString(naglowek).Should().StartWith(PdfMagic,
            "poprawny strumień PDF zaczyna się od „%PDF”.");
    }

    [Test]
    [Description("HANDEL-W66: integracja — GenerateReport zapisany do MemoryStream daje bajty PDF (np. do e-maila/REST). " +
                 "Sprawdza, że pierwsze bajty całego bufora to „%PDF”.")]
    public void HANDEL_W66_WydrukDoStrumieniaBajtow_DajePoprawnyPdf()
    {
        // Arrange: faktura w buforze + kontekst jak w HANDEL-W62 (FV nie zatwierdzamy — NRE w ewidencji VAT w Demo).
        var dok = Get<DokumentHandlowy>(UtworzFaktureWBuforze());

        var rr = new ReportResult
        {
            TemplateFileName = WzorzecSprzedaz,
            DataType = typeof(DokumentHandlowy),
            Context = KontekstWydruku(dok),
            OutputFormat = ReportFormats.PDF,
            AskForParameters = false,
            ParametersHandler = WypelnijParametryWydruku
        };

        // Act: skopiowanie strumienia do pamięci (wzorzec integracji z HANDEL-W66: bajty → załącznik/REST).
        byte[] pdfBytes;
        using (Stream src = Raporty.GenerateReport(rr))
        using (var ms = new MemoryStream())
        {
            src.CopyTo(ms);
            pdfBytes = ms.ToArray();
        }

        // Assert: bufor zawiera dane i zaczyna się od sygnatury PDF.
        pdfBytes.Should().NotBeNullOrEmpty("integracyjny wydruk zwraca niepusty bufor bajtów");
        pdfBytes.Length.Should().BeGreaterThan(4);
        Encoding.ASCII.GetString(pdfBytes, 0, 4).Should().StartWith(PdfMagic,
            "bufor bajtów to plik PDF (sygnatura „%PDF”).");
    }

    // ===================================================================================
    // HANDEL-W62/HANDEL-W66 — Reguły spójności ReportResult (CheckConsistency) — bez renderowania
    // ===================================================================================

    [Test]
    [Description("HANDEL-W62/HANDEL-W66 (reguła CheckConsistency): IReportService wymaga ustawionego TemplateFileName i " +
                 "wyklucza ReportName. ReportResult bez TemplateFileName, ale z ReportName, narusza spójność " +
                 "→ GenerateReport powinno rzucić ArgumentException (a nie wyrenderować PDF).")]
    public void HANDEL_W66_RegulaSpojnosci_BrakTemplateFileName_RzucaArgumentException()
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
    [Description("HANDEL-W62 (druk do PLIKU zamiast na drukarkę): zamiast PrintReport na fizyczną drukarkę (sprzęt) " +
                 "renderujemy fakturę do tekstu (GenerateReportStr + ReportFormats.TXT) i zapisujemy do pliku .txt — " +
                 "sprawdzamy, że plik powstał i ma niepustą treść. To pełny, nie-sprzętowy odpowiednik wydruku.")]
    public void HANDEL_W62_DrukFakturyDoPlikuTekstowego()
    {
        var dok = Get<DokumentHandlowy>(UtworzFaktureWBuforze());

        var rr = new ReportResult
        {
            TemplateFileName = WzorzecSprzedaz,
            DataType = typeof(DokumentHandlowy),
            Context = KontekstWydruku(dok),
            OutputFormat = ReportFormats.TXT,          // druk do pliku tekstowego (nie na drukarkę)
            AskForParameters = false,
            ParametersHandler = WypelnijParametryWydruku
        };

        string tresc = Raporty.GenerateReportStr(rr);

        var sciezka = Path.Combine(Path.GetTempPath(), "FV_" + dok.Guid.ToString("N") + ".txt");
        try
        {
            File.WriteAllText(sciezka, tresc, Encoding.UTF8);
            File.Exists(sciezka).Should().BeTrue("wydruk zapisano do pliku tekstowego");
            new FileInfo(sciezka).Length.Should().BeGreaterThan(0, "plik wydruku nie jest pusty");
            File.ReadAllText(sciezka).Should().NotBeNullOrWhiteSpace("plik zawiera treść wydruku");
        }
        finally
        {
            if (File.Exists(sciezka)) File.Delete(sciezka);
        }
    }

    [Test]
    [Description("HANDEL-W63 (druk do PLIKU): wydruk dokumentu MAGAZYNOWEGO (przyjęcie PW) wzorcem „Magazyn.repx” " +
                 "do pliku tekstowego — ta sama ścieżka co HANDEL-W62, inny wzorzec dobrany do rodzaju dokumentu. " +
                 "Renderujemy do TXT i zapisujemy do pliku, bez fizycznej drukarki.")]
    public void HANDEL_W63_WydrukMagazynowegoDoPlikuTekstowego()
    {
        // Dokument magazynowy: zatwierdzone i zapisane przyjęcie (PW) towaru BIKINI na magazyn „F”.
        var pwGuid = PrzyjmijNaStan(Towar_.Bikini, 10);
        var pw = Get<DokumentHandlowy>(pwGuid);

        var rr = new ReportResult
        {
            TemplateFileName = "Magazyn.repx",         // wzorzec dokumentu magazynowego (DataType=DokumentHandlowy)
            DataType = typeof(DokumentHandlowy),
            Context = KontekstWydruku(pw),
            OutputFormat = ReportFormats.TXT,
            AskForParameters = false,
            ParametersHandler = WypelnijParametryWydruku
        };

        string tresc = Raporty.GenerateReportStr(rr);

        var sciezka = Path.Combine(Path.GetTempPath(), "PW_" + pw.Guid.ToString("N") + ".txt");
        try
        {
            File.WriteAllText(sciezka, tresc, Encoding.UTF8);
            File.Exists(sciezka).Should().BeTrue("wydruk magazynowy zapisano do pliku tekstowego");
            new FileInfo(sciezka).Length.Should().BeGreaterThan(0, "plik wydruku magazynowego nie jest pusty");
        }
        finally
        {
            if (File.Exists(sciezka)) File.Delete(sciezka);
        }
    }

    [Test]
    [Description("HANDEL-W64 (mock raportu/wydruku fiskalnego przez format tekstowy): zamiast sterować DRUKARKĄ " +
                 "(IFiscalPrinterAPI.DrukujRaport*, IReportService.PrintReport — sprzęt), renderujemy dokument do " +
                 "formatu tekstowego ReportFormats.TXT przez GenerateReportStr. To nie-sprzętowy zamiennik wydruku " +
                 "fiskalnego/zestawienia: dostajemy gotową treść tekstową bez podłączonej drukarki.")]
    public void HANDEL_W64_RaportDoTekstu_MockWydrukuFiskalnego()
    {
        var dok = Get<DokumentHandlowy>(UtworzFaktureWBuforze());

        var rr = new ReportResult
        {
            TemplateFileName = WzorzecSprzedaz,
            DataType = typeof(DokumentHandlowy),
            Context = KontekstWydruku(dok),
            OutputFormat = ReportFormats.TXT,          // format tekstowy — mock zamiast drukarki fiskalnej
            AskForParameters = false,
            ParametersHandler = WypelnijParametryWydruku
        };

        // GenerateReportStr zwraca treść tekstową (dla formatów string: TXT/HTML).
        string tekst = Raporty.GenerateReportStr(rr);

        tekst.Should().NotBeNullOrWhiteSpace(
            "render do ReportFormats.TXT zwraca tekstową treść wydruku (mock wydruku fiskalnego bez sprzętu)");
    }

    [Test]
    [Description("HANDEL-W65: wydruk zbiorczy dla zaznaczonego zbioru — DataType=typeof(DokumentHandlowy[]) + " +
                 "Rows=tablica dokumentów; GenerateReport renderuje wszystkie rekordy do jednego PDF (tryb wielu rekordów).")]
    public void HANDEL_W65_WydrukZbiorczy_DajePdf()
    {
        // Dwie faktury w buforze — zbiór do wydruku zbiorczego. Każde UtworzFaktureWBuforze robi
        // SaveDispose (zamyka sesję edycyjną), więc oba dokumenty pobieramy DOPIERO po utworzeniu —
        // w jednej (bieżącej) sesji, inaczej Rows naruszają warunek rows[i].Session==rows[0].Session.
        var g1 = UtworzFaktureWBuforze();
        var g2 = UtworzFaktureWBuforze();
        var dok1 = Get<DokumentHandlowy>(g1);
        var dok2 = Get<DokumentHandlowy>(g2);
        var doki = new[] { dok1, dok2 };

        var context = Login.CreateEmptyContext().Clone(Session);
        context.Set(doki);                              // tablica = tryb wielu rekordów
        context.Set(dok1.Definicja);

        var rr = new ReportResult
        {
            TemplateFileName = WzorzecSprzedaz,
            DataType = typeof(DokumentHandlowy[]),      // typ tablicowy = wydruk dla zaznaczonych
            Rows = doki,
            Context = context,
            OutputFormat = ReportFormats.PDF,
            AskForParameters = false,
            ParametersHandler = WypelnijParametryWydruku
        };

        using var pdf = Raporty.GenerateReport(rr);
        pdf.Should().NotBeNull("wydruk zbiorczy też zwraca strumień PDF");

        var naglowek = new byte[4];
        pdf.Read(naglowek, 0, naglowek.Length).Should().Be(4);
        Encoding.ASCII.GetString(naglowek).Should().StartWith(PdfMagic,
            "zbiorczy wydruk wielu dokumentów to jeden plik PDF („%PDF”).");
    }

    [Test]
    [Ignore("HANDEL-W66 (e-mail/OutputHandler) — Target=ReportTargets.Email/Attachment wymaga skonfigurowanego konta " +
            "pocztowego (KontoPocztowe) i szablonu (SzablonEmail) w pełnej sesji aplikacyjnej — poza zakresem testu " +
            "jednostkowego. ReportResult.OutputHandler NIE jest obsługiwany przez IReportService (CheckConsistency " +
            "rzuca ArgumentException) — służy jako rezultat operacji w trybie wzorca (worker/Command z UI). Testowalny " +
            "rdzeń HANDEL-W66 (GenerateReport → byte[]) pokrywa HANDEL_W66_WydrukDoStrumieniaBajtow. SKIP: integracja pocztowa / tryb UI.")]
    [Description("HANDEL-W66: wysyłka e-mail (Target=Email) i OutputHandler — pominięte (wymaga konta/szablonu / tryb UI).")]
    public void HANDEL_W66_EmailIOutputHandler_Skip() { }
}
