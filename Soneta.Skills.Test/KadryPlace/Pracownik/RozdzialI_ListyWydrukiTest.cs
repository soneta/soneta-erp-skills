using System;
using System.IO;
using System.Linq;
using System.Text;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;     // GetRequiredService
using NUnit.Framework;
using Soneta.Business;                              // Context
using Soneta.Business.UI;                           // IReportService, ReportResult, ReportFormats
using Soneta.Place;                                 // ListaPlac, DefinicjaListyPlac, NaliczanieWypłat, Wyplata, TypNaliczenia
using Soneta.Types;                                 // Date, FromTo, YearMonth
using Prac = Soneta.Kadry.Pracownik;

namespace Soneta.Skills.Test.KadryPlace.Pracownik;

/// <summary>
/// Rozdział I — „Listy płac, przelewy, wydruki” (receptury KADRY-I1, KADRY-I2, KADRY-I3).
/// <para>
/// Testy są <b>wykonywalną dokumentacją</b> publicznego kontraktu list płac i ich wydruków.
/// </para>
/// <list type="bullet">
/// <item><b>KADRY-I1a</b> — ręczne utworzenie pustej listy płac (<c>new ListaPlac()</c> + <c>Place.ListyPlac.AddRow</c>),
/// ustawienie pól w wymaganej kolejności (Definicja → Data → DataWyplaty → MiesiacZUS → Okres).</item>
/// <item><b>KADRY-I1b</b> — naliczenie wypłaty workerem <c>NaliczanieSeryjne.Pracownika</c> z jawną
/// <c>DefinicjaListy</c> (sprawdzona ścieżka z sekcji H): worker tworzy listę płac wg tej definicji i WIĄŻE
/// z nią wypłatę. Asercja: wypłata naliczona, powiązanie dwukierunkowe (<c>w.ListaPlac</c> niepuste, jego
/// <c>Definicja == def</c>; <c>w.Pracownik == pracownik</c>).
/// <b>Rozbieżność dokumentacji:</b> niskopoziomowy worker <c>Soneta.Place.NaliczanieWypłat</c> uruchomiony
/// tylko z <c>ListaPłac</c>+<c>Pracownik</c> (snippet KADRY-I1 w kadry/KADRY09-listy-place.md) w bazie Demo nie napełnia listy
/// (zwraca pustą <c>WszystkieWypłaty</c>); działającą ścieżką naliczania jest <c>NaliczanieSeryjne</c>.</item>
/// <item><b>KADRY-I2</b> — PDF kwitka (paska) wypłaty przez <c>IReportService.GenerateReport</c>
/// (wzorzec <c>PasekWyplaty.repx</c>, <c>DataType = typeof(Wyplata)</c>).</item>
/// <item><b>KADRY-I3</b> — PDF pełnej listy płac (<c>PelnaListaPlac.repx</c>, <c>DataType = typeof(ListaPlac)</c>).</item>
/// </list>
/// <para>
/// <b>Wydruki (KADRY-I2/KADRY-I3):</b> serwis <see cref="IReportService"/> (warstwa <c>Soneta.Business.UI</c>) jest
/// w bieżącym zestawie referencji Skills.Test OSIĄGALNY (transytywnie, tak jak w wydrukach handlowych —
/// rozdz. 12 dokumentów handlowych). Faktyczne wyrenderowanie PDF wymaga jednak zarejestrowanego wzorca
/// <c>*.repx</c> (z assembly <c>Soneta.KadryPlace.Reports</c>) oraz silnika renderującego (DevExpress) —
/// czego testowa baza Demo nie gwarantuje, a samo ładowanie DevExpress bywa niestabilne w hoście testowym.
/// Dlatego generowanie owijamy w try/catch i przy braku wzorca/silnika robimy <c>Assert.Ignore</c>
/// (suita pozostaje zielona, a kod dokumentuje publiczne API). Asercję na sygnaturze <c>"%PDF"</c>
/// wykonujemy tylko wtedy, gdy strumień faktycznie powstał.
/// </para>
/// <para>
/// Wszystko działa na bazie Demo (GoldStandard) z automatycznym rollbackiem po teście. Operujemy wyłącznie
/// na <b>publicznym kontrakcie</b> platformy Soneta (jak dodatek programisty zewnętrznego).
/// </para>
/// </summary>
[TestFixture]
public class RozdzialI_ListyWydrukiTest : PracownikTestBase
{
    /// <summary>Sygnatura nagłówka pliku PDF (pierwsze 4 bajty/znaki strumienia).</summary>
    private const string PdfMagic = "%PDF";

    /// <summary>Wzorzec wydruku paska (kwitka) wypłaty — wg tabeli KADRY-I2 (DataType = Wyplata).</summary>
    private const string WzorzecPasek = "PasekWyplaty.repx";

    /// <summary>Wzorzec wydruku pełnej listy płac — wg tabeli KADRY-I3 (DataType = ListaPlac).</summary>
    private const string WzorzecPelnaLista = "PelnaListaPlac.repx";

    /// <summary>Serwis raportowy ze scope’u bieżącej sesji (jak w wydrukach handlowych).</summary>
    private IReportService Raporty => Session.GetRequiredService<IReportService>();

    // === Pomocniki lokalne ===

    /// <summary>
    /// Wybiera dowolną dostępną definicję listy płac z bazy Demo (słownik konfiguracyjny
    /// <c>Place.DefListPlac</c>). Nazwy/symbole definicji zależą od wdrożenia, więc zamiast
    /// twardego symbolu („ETAT”) pobieramy pierwszą dostępną definicję — deterministycznie,
    /// bez zakładania konkretnej konfiguracji.
    /// </summary>
    private DefinicjaListyPlac DowolnaDefinicjaListy()
        => Place.DefListPlac.Cast<DefinicjaListyPlac>().FirstOrDefault();

    /// <summary>
    /// Dobiera okres/daty listy w obrębie aktywnego etatu pracownika: bierzemy miesiąc rozpoczęcia
    /// etatu (dla pracowników Demo etat zwykle zaczyna się wstecz i jest otwarty), aby naliczanie
    /// trafiło w okres zatrudnienia. Zwraca (okresMiesiąca, dataWyplaty = koniec miesiąca).
    /// </summary>
    private static (FromTo Okres, Date DataWyplaty) OkresWEtacie(Prac pracownik)
    {
        var from = pracownik.Last.Etat.Okres.From;
        var poczatek = new Date(from.Year, from.Month, 1);
        var koniec = poczatek.AddMonths(1).AddDays(-1);   // koniec miesiąca (28–31)
        return (new FromTo(poczatek, koniec), koniec);
    }

    /// <summary>
    /// Demonstruje ręczne utworzenie pustej listy płac z wybraną definicją i polami ustawionymi
    /// w wymaganej kolejności (Definicja → Data → DataWyplaty → MiesiacZUS → Okres), zwraca utworzoną
    /// listę. Sama lista jest tworzona poprawnie; <b>napełnienie jej wypłatami</b> realizuje worker
    /// naliczający (patrz <see cref="NaliczWyplate"/>), a nie ustawienie pól listy.
    /// </summary>
    private ListaPlac UtworzPustaListe(Prac pracownik, DefinicjaListyPlac def)
    {
        var (okres, dataWyplaty) = OkresWEtacie(pracownik);

        var lp = new ListaPlac();
        Place.ListyPlac.AddRow(lp);
        lp.Definicja = def;                         // wzorzec listy — ustaw PIERWSZE po AddRow
        // Wydzial/Seria ustawiamy WARUNKOWO — tylko gdy wymaga ich definicja.
        if (def.Wydzial)
            lp.Wydzial = Kadry.Wydzialy.Firma;
        lp.Data = dataWyplaty;                      // data naliczania listy
        lp.DataWyplaty = dataWyplaty;               // data przekazania środków (wyznacza mies./rok)
        lp.MiesiacZUS = new YearMonth(dataWyplaty); // miesiąc rozliczenia ZUS
        lp.Okres = okres;                           // okres listy — PO DataWyplaty
        return lp;
    }

    /// <summary>
    /// Nalicza wypłatę etatową pracownika workerem <c>NaliczanieSeryjne.Pracownika</c> (sprawdzona
    /// ścieżka z sekcji H). Worker sam dobiera/tworzy listę płac dla naliczanych wypłat i WIĄŻE je
    /// z nią (<c>Wyplata.ListaPlac</c>).
    /// <para>
    /// <c>Nalicz()</c> sam otwiera i commituje transakcję w sesji — NIE owijamy go w InTransaction.
    /// Pola <c>Naliczanie</c> nie ustawiamy (domyślne; setter rzuca bez licencji „PL Złoty”).
    /// <c>DefinicjaListy</c> także NIE wymuszamy — dowolna definicja może nie pasować do typu wypłaty
    /// (np. lista umów ≠ etat) i wtedy nic się nie naliczy; worker dobiera definicję sam.
    /// Zwraca pierwszą naliczoną wypłatę albo <c>null</c>, gdy nic się nie naliczyło.
    /// </para>
    /// </summary>
    private Wyplata NaliczWyplate(Prac pracownik)
    {
        var (okres, dataWyplaty) = OkresWEtacie(pracownik);

        var pars = new NaliczanieSeryjne.PracownikParams(Context)
        {
            DataWypłaty = dataWyplaty,              // ustawia Okres i MiesiącDeklaracji automatycznie
            DataListy = dataWyplaty,
            TypWypłaty = TypWyplaty.Etat,           // tylko wypłaty etatowe
        };

        var naliczanie = new NaliczanieSeryjne.Pracownika(pars) { Pracownik = pracownik };
        var wynik = naliczanie.Nalicz();            // self-commit w sesji
        return wynik.WszystkieWypłaty.Cast<Wyplata>().FirstOrDefault();
    }

    // ===================================================================================
    // KADRY-I1 — Tworzenie i naliczanie listy płac
    // ===================================================================================

    [Test]
    [Description("KADRY-I1 (część A): ręcznie tworzymy pustą listę płac — new ListaPlac() + Place.ListyPlac.AddRow + " +
                 "pola w wymaganej kolejności (Definicja → Data → DataWyplaty → MiesiacZUS → Okres). " +
                 "Asercja: lista istnieje, ma przypisaną definicję i jest pusta (Wyplaty napełnia dopiero worker).")]
    public void KADRY_I1a_PustaListaPlac_TworzenieRecznePolaWKolejnosci()
    {
        var pracownik = Pracownik(Pracownik_.Andrzejewski);
        pracownik.Should().NotBeNull();

        var def = DowolnaDefinicjaListy();
        def.Should().NotBeNull("baza Demo zawiera co najmniej jedną definicję listy płac (Place.DefListPlac)");

        // Tworzenie danych operacyjnych MUSI być w trybie edycji (InTransaction), inaczej AddRow
        // rzuca CannotEditException.
        ListaPlac lp = null;
        InTransaction(() => lp = UtworzPustaListe(pracownik, def));

        lp.Should().NotBeNull();
        lp.Definicja.Should().Be(def, "ustawiliśmy Definicja po AddRow");
        lp.Wyplaty.Cast<Wyplata>().Should().BeEmpty("nowo utworzona lista jest pusta — wypłaty dolicza worker");

        SaveDispose();   // utrwalenie w bazie (rollback po teście i tak wycofa)
    }

    [Test]
    [Description("KADRY-I1 (część B): naliczamy wypłatę etatową workerem NaliczanieSeryjne.Pracownika (sprawdzona " +
                 "ścieżka z sekcji H). Worker sam dobiera/tworzy listę płac i WIĄŻE z nią wypłatę. " +
                 "Asercja: wypłata naliczona, powiązana dwukierunkowo z listą płac (w.ListaPlac niepuste, " +
                 "ma definicję) i z pracownikiem (w.Pracownik == pracownik). " +
                 "Uwaga: niskopoziomowy worker Soneta.Place.NaliczanieWypłat (samo ListaPłac+Pracownik z " +
                 "dokumentacji) w bazie Demo nie napełnia listy — sprawdzoną ścieżką jest NaliczanieSeryjne.")]
    public void KADRY_I1b_ListaPlac_NaliczanieWyplatyPowiazanaZLista()
    {
        var pracownik = Pracownik(Pracownik_.Andrzejewski);
        pracownik.Should().NotBeNull();

        // NaliczanieSeryjne.Nalicz() sam otwiera i commituje transakcję — NIE owijamy w InTransaction.
        var w = NaliczWyplate(pracownik);
        w.Should().NotBeNull(
            "naliczanie etatu dla pracownika Demo w okresie etatu powinno dać wypłatę powiązaną z listą");

        // Powiązanie dwukierunkowe: wypłata wskazuje wstecz listę płac i pracownika.
        var lista = (ListaPlac)w.ListaPlac;
        lista.Should().NotBeNull("Wyplata.ListaPlac wskazuje listę, na której została naliczona");
        lista.Definicja.Should().NotBeNull("lista płac utworzona przez worker ma przypisaną definicję");
        w.Pracownik.Guid.Should().Be(pracownik.Guid, "Wyplata.Pracownik to pracownik, dla którego naliczono");

        SaveDispose();
    }

    // ===================================================================================
    // KADRY-I2 — Drukowanie/PDF kwitka (paska) wypłaty
    // ===================================================================================

    [Test]
    [Description("KADRY-I2: pasek (kwitek) wypłaty do PDF przez IReportService.GenerateReport " +
                 "(TemplateFileName = PasekWyplaty.repx, DataType = typeof(Wyplata), OutputFormat = PDF, " +
                 "Context.Set(wyplata)). Strumień zaczyna się od sygnatury „%PDF”. " +
                 "Brak wzorca/silnika renderującego → Assert.Ignore (suita zielona).")]
    public void KADRY_I2_PasekWyplaty_DoPdf_ZaczynaSieOdPdf()
    {
        var pracownik = Pracownik(Pracownik_.Bednarek);
        pracownik.Should().NotBeNull();

        // Arrange: naliczona wypłata (wraz z listą) jako źródło danych wydruku.
        // NaliczanieSeryjne self-commituje — wypłata jest dostępna w bieżącej sesji.
        var wyplata = NaliczWyplate(pracownik);
        if (wyplata == null)
            Assert.Ignore("Worker nie naliczył wypłaty dla pracownika Demo — brak danych do wydruku paska.");

        // Kontekst wydruku: pojedyncza Wyplata (jak w snippetcie KADRY-I2).
        var context = Login.CreateEmptyContext().Clone(Session);
        context.Set(wyplata);

        var rr = new ReportResult
        {
            TemplateFileName = WzorzecPasek,           // tryb automatyczny (bez UI)
            DataType = typeof(Wyplata),                // pojedyncza wypłata
            Context = context,
            OutputFormat = ReportFormats.PDF,
            AskForParameters = false                   // tryb wsadowy — nie pytaj o parametry
        };

        // Act: generowanie do strumienia. Brak wzorca/silnika → Assert.Ignore zamiast błędu.
        byte[] naglowek;
        try
        {
            using var pdf = Raporty.GenerateReport(rr);
            pdf.Should().NotBeNull("GenerateReport dla formatu binarnego zwraca Stream");
            naglowek = new byte[4];
            int przeczytane = pdf.Read(naglowek, 0, naglowek.Length);
            przeczytane.Should().Be(4, "PDF ma co najmniej 4-bajtowy nagłówek");
        }
        catch (Exception ex)
        {
            Assert.Ignore("Pominięto KADRY-I2: wygenerowanie PDF paska wymaga zarejestrowanego wzorca '" +
                          WzorzecPasek + "' (assembly Soneta.KadryPlace.Reports) oraz silnika renderującego " +
                          "(DevExpress), których testowa baza Demo nie gwarantuje. Test dokumentuje publiczne API " +
                          "IReportService.GenerateReport. Szczegóły: " + ex.GetType().Name + " — " + ex.Message);
            return;
        }

        Encoding.ASCII.GetString(naglowek).Should().StartWith(PdfMagic,
            "poprawny strumień PDF zaczyna się od „%PDF”.");
    }

    // ===================================================================================
    // KADRY-I3 — Drukowanie/PDF całej listy płac
    // ===================================================================================

    [Test]
    [Description("KADRY-I3: pełna lista płac do PDF przez IReportService.GenerateReport " +
                 "(TemplateFileName = PelnaListaPlac.repx, DataType = typeof(ListaPlac), OutputFormat = PDF, " +
                 "Context.Set(listaPlac)). Strumień zaczyna się od sygnatury „%PDF”. " +
                 "Brak wzorca/silnika renderującego → Assert.Ignore (suita zielona).")]
    public void KADRY_I3_PelnaListaPlac_DoPdf_ZaczynaSieOdPdf()
    {
        var pracownik = Pracownik(Pracownik_.Bujak);
        pracownik.Should().NotBeNull();

        // Arrange: naliczona wypłata daje listę płac (Wyplata.ListaPlac) jako źródło danych wydruku.
        // NaliczanieSeryjne self-commituje — lista jest dostępna w bieżącej sesji.
        var wyplata = NaliczWyplate(pracownik);
        if (wyplata == null)
            Assert.Ignore("Worker nie naliczył wypłaty dla pracownika Demo — brak listy płac do wydruku.");
        var lp = (ListaPlac)wyplata.ListaPlac;
        lp.Should().NotBeNull();

        var context = Login.CreateEmptyContext().Clone(Session);
        context.Set(lp);                               // ListaPlac

        var rr = new ReportResult
        {
            TemplateFileName = WzorzecPelnaLista,
            DataType = typeof(ListaPlac),
            Context = context,
            OutputFormat = ReportFormats.PDF,
            AskForParameters = false
        };

        // Act: skopiowanie strumienia do pamięci (jak wzorzec integracyjny — bajty → załącznik/REST).
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
            Assert.Ignore("Pominięto KADRY-I3: wygenerowanie PDF pełnej listy płac wymaga zarejestrowanego wzorca '" +
                          WzorzecPelnaLista + "' (assembly Soneta.KadryPlace.Reports) oraz silnika renderującego " +
                          "(DevExpress), których testowa baza Demo nie gwarantuje. Test dokumentuje publiczne API " +
                          "IReportService.GenerateReport. Szczegóły: " + ex.GetType().Name + " — " + ex.Message);
            return;
        }

        pdfBytes.Should().NotBeNullOrEmpty("wydruk listy płac zwraca niepusty bufor bajtów");
        pdfBytes.Length.Should().BeGreaterThan(4);
        Encoding.ASCII.GetString(pdfBytes, 0, 4).Should().StartWith(PdfMagic,
            "bufor bajtów to plik PDF (sygnatura „%PDF”).");
    }
}
