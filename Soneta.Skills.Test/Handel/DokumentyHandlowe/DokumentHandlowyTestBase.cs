using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;   // GetRequiredService
using Soneta.Business;
using Soneta.Business.UI;                           // IReportService, ReportResult, ReportFormats
using Soneta.CRM;
using Soneta.Handel;
using Soneta.Magazyny;
using Soneta.Towary;
using Soneta.Types;
using Soneta.Test;

namespace Soneta.Skills.Test.Handel.DokumentyHandlowe;

/// <summary>
/// Wspólna baza testów dokumentu handlowego. Dziedziczy z <see cref="TestBase"/>, dzięki czemu:
/// <list type="bullet">
/// <item>udostępnia gotową sesję operacyjną (<c>Session</c>) powiązaną z testową bazą Demo,</item>
/// <item>automatycznie wycofuje (rollback) wszystkie zmiany w bazie po zakończeniu testu,</item>
/// <item>daje metody pomocnicze <c>InTransaction</c>/<c>SaveDispose</c> do pracy w transakcjach.</item>
/// </list>
/// Baza dodaje skróty często powtarzane w testach dokumentu handlowego: dostęp do modułów
/// (Handel, Magazyny, Towary, CRM), pobieranie definicji dokumentów i danych słownikowych z bazy Demo
/// oraz publiczne metody tworzenia dokumentu i jego pozycji.
/// <para>
/// Cała baza operuje wyłącznie na <b>publicznym kontrakcie</b> platformy Soneta — tak jak dodatek
/// programisty zewnętrznego, który nie ma dostępu do kodu źródłowego aplikacji.
/// </para>
/// </summary>
public abstract class DokumentHandlowyTestBase : TestBase
{
    // === Moduły bieżącej sesji operacyjnej ===

    /// <summary>Moduł Handel — definicje dokumentów, tabela dokumentów handlowych.</summary>
    protected HandelModule Handel => Session.GetHandel();

    /// <summary>Moduł Magazyny — magazyny, zasoby, obroty, partie (grupy dostaw).</summary>
    protected MagazynyModule Magazyny => Session.GetMagazyny();

    /// <summary>Moduł Towary — kartoteka towarów, jednostki, ceny.</summary>
    protected TowaryModule Towary => Session.GetTowary();

    /// <summary>Moduł CRM — kartoteka kontrahentów.</summary>
    protected CRMModule Crm => Session.GetCRM();

    // === Symbole danych dostępnych w bazie Demo (GoldStandard) ===

    /// <summary>Symbole definicji dokumentów dostępnych w bazie Demo (pole <c>DefDokHandlowego.Symbol</c>).</summary>
    protected static class Definicje
    {
        public const string FakturaSprzedazy = "FV";
        /// <summary>
        /// Zakup. UWAGA: w bazie Demo (GoldStandard) NIE ma faktury zakupu jako dokumentu handlowego —
        /// wszystkie definicje F* mają kategorię „Sprzedaż". Stronę zakupową reprezentuje przyjęcie
        /// magazynowe od dostawcy „PZ" (przychód). W produkcyjnym enova faktura zakupu ma zwykle symbol „FZ".
        /// </summary>
        public const string FakturaZakupu = "PZ";
        public const string Paragon = "PAR";
        public const string PrzyjecieZewnetrzne = "PZ";
        public const string PrzyjecieWewnetrzne = "PW";
        public const string WydanieZewnetrzne = "WZ";
        public const string RozchodWewnetrzny = "RW";
        public const string ZamowienieOdbiorcy = "ZO";
        public const string ZamowienieDoDostawcy = "ZD";
        public const string PrzesuniecieMM = "MM";
        public const string Inwentaryzacja = "INW";
    }

    /// <summary>Kody towarów z bazy Demo.</summary>
    protected static class Towar_
    {
        /// <summary>Towar magazynowy w sztukach.</summary>
        public const string Bikini = "BIKINI";
        /// <summary>Usługa (bez wpływu na magazyn).</summary>
        public const string Montaz = "MONTAZ";
        /// <summary>Towar rozliczany w km.</summary>
        public const string Transport = "TRANSPORT";
    }

    /// <summary>Kody kontrahentów z bazy Demo.</summary>
    protected static class Kontrahent_
    {
        public const string Abc = "Abc";
        public const string Zefir = "ZEFIR";
    }

    /// <summary>Symbole magazynów z bazy Demo.</summary>
    protected static class Magazyn_
    {
        /// <summary>Magazyn „Firma" (symbol „F").</summary>
        public const string Firma = "F";
    }

    // === Wyszukiwanie obiektów słownikowych / kartotekowych ===

    /// <summary>Pobiera definicję dokumentu handlowego po symbolu (np. „FV", „PW").</summary>
    protected DefDokHandlowego Definicja(string symbol) => Handel.DefDokHandlowych.WgSymbolu[symbol];

    /// <summary>Pobiera kontrahenta po kodzie (klucz unikalny, case-insensitive).</summary>
    protected Kontrahent Kontrahent(string kod) => Crm.Kontrahenci.WgKodu[kod];

    /// <summary>Pobiera towar po kodzie.</summary>
    protected Towar Towar(string kod) => Towary.Towary.WgKodu[kod];

    /// <summary>Pobiera magazyn po symbolu (np. „F").</summary>
    protected Magazyn Magazyn(string symbol) => Magazyny.Magazyny.WgSymbol[symbol];

    // === Tworzenie dokumentu i pozycji (publiczne API) ===

    /// <summary>
    /// Tworzy nowy dokument handlowy w bieżącej sesji wewnątrz transakcji edycyjnej.
    /// Kolejność jest istotna: najpierw <c>AddRow</c>, potem <c>Definicja</c> (wyznacza kierunek
    /// magazynu i przelicza parametry dokumentu), następnie kontrahent i magazyn.
    /// </summary>
    /// <param name="defSymbol">Symbol definicji dokumentu (np. „FV", „PW").</param>
    /// <param name="kontrahent">Kontrahent dokumentu; <c>null</c> dla dokumentów wewnętrznych.</param>
    /// <param name="magazyn">Magazyn dokumentu; <c>null</c> gdy definicja go nie wymaga.</param>
    protected DokumentHandlowy UtworzDokument(
        string defSymbol,
        Kontrahent kontrahent = null,
        Magazyn magazyn = null)
    {
        DokumentHandlowy dok = null;
        InTransaction(() =>
        {
            dok = new DokumentHandlowy();
            Session.AddRow(dok);
            dok.Definicja = Definicja(defSymbol);
            if (magazyn != null)
                dok.Magazyn = magazyn;
            if (kontrahent != null)
                dok.Kontrahent = kontrahent;
        });
        return dok;
    }

    /// <summary>
    /// Dodaje pozycję do dokumentu. Ustawienie <c>Towar</c> inicjuje jednostkę miary na polach
    /// <c>Ilosc</c> i <c>Cena</c> — dlatego ilość i cenę tworzymy z symbolem już ustawionym przez towar.
    /// Wywołuj wewnątrz transakcji edycyjnej (np. w <c>InTransaction</c>).
    /// </summary>
    /// <param name="dok">Dokument, do którego dodajemy pozycję (musi być „żywy" w sesji).</param>
    /// <param name="towar">Towar pozycji.</param>
    /// <param name="ilosc">Ilość w jednostce towaru.</param>
    /// <param name="cena">Cena jednostkowa; <c>null</c> = nie nadpisuj (zostanie pobrana z cennika).</param>
    protected static PozycjaDokHandlowego DodajPozycje(
        DokumentHandlowy dok,
        Towar towar,
        double ilosc,
        double? cena = null)
    {
        var poz = new PozycjaDokHandlowego(dok);
        dok.Session.AddRow(poz);
        poz.Towar = towar;
        poz.Ilosc = new Quantity(ilosc, poz.Ilosc.Symbol);
        if (cena.HasValue)
            poz.Cena = new DoubleCy(cena.Value, poz.Cena.Symbol);
        return poz;
    }

    /// <summary>
    /// Wprowadza towar na stan magazynu „F" przez utworzenie i <b>zatwierdzenie</b> przyjęcia (PW),
    /// a następnie zapis (<c>SaveDispose</c>). Dopiero zatwierdzone przyjęcie księguje zasoby/obroty —
    /// bez tego baza Demo (kontrola stanu ujemnego) odrzuci każdy rozchód (FV/WZ/RW) tego towaru.
    /// <para>Wywołuj na początku testu rozchodowego; po nim pracuj na świeżej sesji (np. tworząc FV).</para>
    /// </summary>
    /// <returns>Guid zapisanego, zatwierdzonego dokumentu przyjęcia.</returns>
    protected Guid PrzyjmijNaStan(string towarKod, double ilosc, double cena = 10)
    {
        var pw = UtworzDokument(Definicje.PrzyjecieWewnetrzne, magazyn: Magazyn(Magazyn_.Firma));
        InTransaction(() => DodajPozycje(pw, Towar(towarKod), ilosc, cena));
        InTransaction(() => pw.Stan = StanDokumentuHandlowego.Zatwierdzony);
        var guid = pw.Guid;
        SaveDispose();
        return guid;
    }

    // === Wydruki / raporty (IReportService) — wspólne dla rozdziałów 12 i 13 ===

    /// <summary>Sygnatura nagłówka pliku PDF (pierwsze 4 bajty/znaki strumienia).</summary>
    protected const string PdfMagic = "%PDF";

    /// <summary>Nazwa zarejestrowanego wzorca wydruku faktury sprzedaży (REPX).</summary>
    protected const string WzorzecSprzedaz = "Sprzedaz.repx";

    /// <summary>Serwis raportowy ze scope'u bieżącej sesji (<c>Session.GetRequiredService&lt;IReportService&gt;()</c>).</summary>
    protected IReportService Raporty => Session.GetRequiredService<IReportService>();

    /// <summary>
    /// Publiczny, agnostyczny handler parametrów wydruku do <see cref="ReportResult.ParametersHandler"/>.
    /// <para>
    /// Wzorzec <c>.repx</c> może wymagać własnych typów parametrów, których instancje muszą znaleźć się
    /// w kontekście — inaczej <see cref="IReportService.GenerateReport"/> zwraca <c>QueryContextInformation</c>
    /// (żądanie parametrów), a nie strumień. Zamiast sięgać po wewnętrzne typy parametrów konkretnego wzorca,
    /// dla każdego ŻĄDANEGO typu tworzymy instancję (konstruktorem z <see cref="Context"/>, a w razie braku —
    /// bezparametrowym) i wstawiamy ją do kontekstu. Silnik woła ten handler dla każdego brakującego typu.
    /// </para>
    /// </summary>
    protected static void WypelnijParametryWydruku(Type type, Context cx)
    {
        var ctorZKontekstem = type.GetConstructor(new[] { typeof(Context) });
        object instancja = ctorZKontekstem != null
            ? ctorZKontekstem.Invoke(new object[] { cx })
            : Activator.CreateInstance(type);
        if (instancja != null)
            cx.Set(instancja);
    }

    /// <summary>
    /// Buduje kontekst wydruku pojedynczego dokumentu: rekord, definicja, kontrahent, tablica zaznaczeń
    /// oraz instancja parametrów wydruku (<see cref="ParametryWydrukuDokumentu"/>).
    /// </summary>
    protected Context KontekstWydruku(DokumentHandlowy dok)
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

    /// <summary>
    /// Tworzy i ZAPISUJE fakturę sprzedaży (FV) z jedną pozycją towaru BIKINI, pozostawioną w BUFORZE.
    /// FV nie jest zatwierdzana (zatwierdzenie FV w bazie Demo rzuca NRE w ewidencji VAT); wydruk buduje się
    /// z dokumentu buforowego — sumy/VAT/płatności są już wyliczone. Rozchód FV wymaga wcześniej
    /// zaksięgowanego przyjęcia (Demo blokuje stan ujemny) — używamy <see cref="PrzyjmijNaStan"/>.
    /// Zwraca Guid zapisanego dokumentu; sesja edycyjna zostaje zamknięta przez <see cref="SaveDispose"/>.
    /// </summary>
    protected Guid UtworzFaktureWBuforze()
    {
        PrzyjmijNaStan(Towar_.Bikini, 20);

        var fv = UtworzDokument(Definicje.FakturaSprzedazy,
            kontrahent: Kontrahent(Kontrahent_.Abc),
            magazyn: Magazyn(Magazyn_.Firma));
        InTransaction(() => DodajPozycje(fv, Towar(Towar_.Bikini), ilosc: 2, cena: 12));

        var guid = fv.Guid;
        SaveDispose();
        return guid;
    }

    /// <summary>
    /// Tworzy i zwraca <b>ZATWIERDZONĄ</b> fakturę sprzedaży (FV) z pozycją BIKINI — gotową jako dokument
    /// nadrzędny relacji (np. FV→WZ) albo do dalszego przetwarzania.
    /// <para>
    /// <b>Kluczowa kolejność (inaczej NRE w ewidencji VAT):</b> faktury NIE wolno zatwierdzać w tej samej
    /// sesji tuż po dodaniu pozycji (sam <c>CommitUI</c>). Zatwierdzenie tworzy ewidencję VAT
    /// (<c>DokumentHandlowy.CreateEwidencja</c>), która czyta <c>dok.KrajPodatkuVat</c>; pole to jest
    /// przeliczane z <c>pozycja.Stawka.Kraj</c> dopiero przez <b>odroczone zdarzenie sesji</b> wykonywane
    /// na <c>Session.Save()</c>. Dlatego najpierw zapisujemy FV z pozycją (<see cref="UtworzFaktureWBuforze"/>
    /// → <c>SaveDispose</c>), potem na świeżej sesji pobieramy ją i dopiero wtedy zatwierdzamy — wówczas
    /// <c>KrajPodatkuVat</c> jest już ustawiony i ewidencja VAT powstaje bez <c>NullReferenceException</c>.
    /// </para>
    /// </summary>
    /// <returns>Guid zatwierdzonej, zapisanej faktury sprzedaży.</returns>
    protected Guid UtworzZatwierdzonaFakture()
    {
        var guid = UtworzFaktureWBuforze();                 // FV + pozycja, ZAPISANE → KrajPodatkuVat przeliczony
        var fv = Get<DokumentHandlowy>(guid);
        InTransaction(() => fv.Stan = StanDokumentuHandlowego.Zatwierdzony);
        SaveDispose();
        return guid;
    }
}
