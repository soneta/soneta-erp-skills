using System.Linq;
using AwesomeAssertions;
using NUnit.Framework;
using Soneta.Deklaracje.UE;
using Soneta.Handel;
using Soneta.Handel.Kompletacje;
using Soneta.Types;

namespace Soneta.Skills.Test.Handel.DokumentyHandlowe;

/// <summary>
/// Rozdział 13 skilla „dokument-handlowy” — tematy specjalistyczne (W67–W74):
/// KSeF, fiskalizacja, e-paragon, kompletacja oraz Intrastat.
/// <para>
/// <b>Zasada całego rozdziału:</b> większość operacji łączy dokument z systemem zewnętrznym
/// (bramka KSeF, wysyłka e-mail) albo ze sprzętem (drukarka fiskalna). Takich fragmentów
/// <b>nie da się odtworzyć w teście jednostkowym</b> — są oznaczone <c>[Ignore]</c> z uzasadnieniem.
/// Testujemy wyłącznie część <b>offline/lokalną</b>: ustawienie pól i parametrów oraz strukturę
/// (parametry workerów, pola dokumentu, warunki widoczności/aktywności akcji).
/// </para>
/// <para>
/// Cały kod operuje wyłącznie na <b>publicznym kontrakcie</b> platformy Soneta — tak jak dodatek
/// programisty zewnętrznego.
/// </para>
/// <para>
/// <b>Fakty zweryfikowane skanem DLL (różnice względem treści skilla):</b>
/// <list type="bullet">
/// <item><c>RodzajIntrastat</c> oraz <c>KodRodzajuTransakcji</c> żyją w <c>Soneta.Handel</c>
///   (a nie w <c>Soneta.Magazyny</c>); <c>RodzajIntrastat</c>: <c>NieUwzględniaj=0</c>,
///   <c>Przywóz=257</c>, <c>Wywóz=258</c>.</item>
/// <item><c>dok.RodzajTransakcji</c> (typ <c>KodRodzajuTransakcji</c>) oraz <c>dok.OkresIntrastat</c>
///   (<c>Date</c>) są <b>publicznie zapisywalne</b>; <c>dok.EParagonAdresEmail</c> również.</item>
/// <item><c>dok.SymbolKasy</c>, <c>dok.EParagon</c>, <c>dok.Kategoria</c>, <c>dok.KierunekMagazynu</c>
///   <b>nie są publicznymi właściwościami</b> — nie da się ich odczytać/ustawić z dodatku zewnętrznego,
///   dlatego testy operują na parametrach workerów i polach faktycznie publicznych.</item>
/// <item><c>dok.UaktualnijIntrastat(kodCN, masa, kraj, przelicznik)</c> to publiczna metoda
///   zwracająca <c>int</c> (liczbę zaktualizowanych pozycji).</item>
/// </list>
/// </para>
/// </summary>
[TestFixture]
public class Rozdzial13_SpecjalistyczneTest : DokumentHandlowyTestBase
{
    // =================================================================================================
    // W74 — INTRASTAT (offline, w pełni testowalne)
    // =================================================================================================

    [Test]
    [Description("W74: pole dokumentu RodzajTransakcji (KodRodzajuTransakcji) jest publicznie zapisywalne " +
                 "— ustawiamy rodzaj transakcji Intrastat na dokumencie i odczytujemy go z powrotem.")]
    public void W74_RodzajTransakcji_MoznaUstawicNaDokumencie()
    {
        // Dokument zakupu unijnego (FF, faktura od dostawcy) — Intrastat dotyczy przepływów towarów w UE.
        // FF to dokument przychodowy — nie wymaga stanu magazynowego, więc można go utworzyć w Demo bez przyjęcia.
        var dok = UtworzDokument(Definicje.FakturaZakupu, kontrahent: Kontrahent(Kontrahent_.Abc));

        // RodzajTransakcji to bazodanowy enum KodRodzajuTransakcji — ustawiamy w transakcji edycyjnej.
        // Wartość „Różne” (=1) to bezpieczny, istniejący wariant enuma.
        InTransaction(() => dok.RodzajTransakcji = KodRodzajuTransakcji.Różne);

        // Asercja: pole zostało zapisane na dokumencie (odczyt publicznym getterem).
        dok.RodzajTransakcji.Should().Be(KodRodzajuTransakcji.Różne);
    }

    [Test]
    [Description("W74: pole OkresIntrastat (Date) — miesiąc, w którym dokument trafi na deklarację — " +
                 "jest publicznie zapisywalne; ustawiamy je i weryfikujemy odczyt.")]
    public void W74_OkresIntrastat_MoznaUstawicNaDokumencie()
    {
        var dok = UtworzDokument(Definicje.FakturaZakupu, kontrahent: Kontrahent(Kontrahent_.Abc));

        // Okres deklaracji = pierwszy dzień bieżącego miesiąca (data decyduje o miesiącu deklaracji).
        var okres = Date.Today.FirstDayMonth();
        InTransaction(() => dok.OkresIntrastat = okres);

        dok.OkresIntrastat.Should().Be(okres);
    }

    [Test]
    [Description("W74: konstrukcja parametrów workera DokumentHandlowyZmienIntrastatParams przez Context " +
                 "i osadzenie ich w workerze przez konstruktor — parametry (KodCN/Masa/Kraj/Przelicznik) " +
                 "są ustawiane i widoczne przez worker.Params (offline; bez wywołania Update()).")]
    public void W74_ParametryWorkeraIntrastat_KonstrukcjaIPrzekazanie()
    {
        var dok = UtworzDokument(Definicje.FakturaZakupu, kontrahent: Kontrahent(Kontrahent_.Abc));

        // Worker wymaga Params przez konstruktor; Params budujemy z kontekstu zawierającego dokument.
        var ctx = Session.GetEmptyContext();
        ctx.TryAdd(() => dok);
        var parametry = new DokumentHandlowyZmienIntrastatWorker.DokumentHandlowyZmienIntrastatParams(ctx)
        {
            KodCN = true,        // przepisz kod CN z kartoteki towaru
            Masa = true,         // przelicz masę pozycji
            Kraj = false,        // nie aktualizuj kraju pochodzenia
            Przelicznik = true   // ilość w jednostce uzupełniającej
        };

        // Worker z Params przez konstruktor; właściwości [Context] (Dokument) inicjatorem obiektu.
        var worker = new DokumentHandlowyZmienIntrastatWorker(parametry) { Dokument = dok };

        // Asercja: Params zostały przekazane do workera (read-only property Params).
        // (Same flagi mają tylko publiczny setter — weryfikujemy referencję obiektu Params.)
        worker.Params.Should().BeSameAs(parametry);
    }

    [Test]
    [Description("W74: IsVisibleUpdate workera Intrastat jest false dla dokumentu, którego definicja ma " +
                 "Intrastat == NieUwzględniaj (akcja pomijana) — sprawdzane czysto lokalnie, bez Update().")]
    public void W74_IsVisibleUpdate_DlaDefinicjiNieUwzgledniajacej_False()
    {
        // FV (faktura sprzedaży) w Demo nie jest dokumentem unijnym uwzględnianym w Intrastacie:
        // jego definicja ma RodzajIntrastat.NieUwzględniaj, więc akcja aktualizacji jest niewidoczna.
        var dok = UtworzDokument(Definicje.FakturaSprzedazy, kontrahent: Kontrahent(Kontrahent_.Abc));

        // Warunek wstępny czytamy z definicji (publiczny getter Definicja.Intrastat).
        dok.Definicja.Intrastat.Should().Be(RodzajIntrastat.NieUwzględniaj,
            "definicja FV w Demo nie uwzględnia dokumentu w Intrastacie");

        var ctx = Session.GetEmptyContext();
        ctx.TryAdd(() => dok);
        var parametry = new DokumentHandlowyZmienIntrastatWorker.DokumentHandlowyZmienIntrastatParams(ctx);
        var worker = new DokumentHandlowyZmienIntrastatWorker(parametry) { Dokument = dok };

        // IsVisibleUpdate to czysta logika lokalna (bez sieci): dla NieUwzględniaj zwraca false.
        DokumentHandlowyZmienIntrastatWorker.IsVisibleUpdate(dok).Should().BeFalse(
            "dokument z Definicja.Intrastat == NieUwzględniaj jest pomijany (akcja niewidoczna)");
    }

    [Test]
    [Description("W74: metoda dokumentu UaktualnijIntrastat(kodCN, masa, kraj, przelicznik) jest publiczna, " +
                 "wykonuje się lokalnie i zwraca liczbę zaktualizowanych pozycji (>= 0). Dla dokumentu bez " +
                 "pozycji zwraca 0 — operacja jest bezpieczna i nie wymaga sieci.")]
    public void W74_UaktualnijIntrastat_ZwracaLiczbeZaktualizowanychPozycji()
    {
        // Dokument bez pozycji — metoda nie ma czego aktualizować, ale musi się wykonać i zwrócić 0.
        var dok = UtworzDokument(Definicje.FakturaZakupu, kontrahent: Kontrahent(Kontrahent_.Abc));

        int zaktualizowane = 0;
        // Metoda modyfikuje pozycje, więc wykonujemy ją w transakcji edycyjnej.
        InTransaction(() => zaktualizowane = dok.UaktualnijIntrastat(
            kodCN: true, masa: false, kraj: false, przelicznik: false));

        // Brak pozycji ⇒ 0 zaktualizowanych; metoda zadziałała lokalnie bez wyjątku.
        zaktualizowane.Should().Be(0, "dokument bez pozycji nie ma czego aktualizować dla Intrastatu");
    }

    [Test]
    [Description("W74: wyszukanie dokumentów do deklaracji za okres — filtr SERWEROWY po dacie (klucz WgDaty), " +
                 "a kwalifikację do Intrastatu weryfikujemy odczytem zapisanego pola OkresIntrastat. " +
                 "Dokument zapisujemy (SaveDispose) i odnajdujemy po Guid.")]
    public void W74_WyszukanieDokumentowDoDeklaracji_FiltrSerwerowy()
    {
        // PZ (przywóz unijny) to dokument magazynowy → wymaga magazynu.
        var dok = UtworzDokument(
            Definicje.FakturaZakupu,
            kontrahent: Kontrahent(Kontrahent_.Abc),
            magazyn: Magazyn(Magazyn_.Firma));

        // Oznaczamy dokument okresem Intrastat (bieżący miesiąc) i rodzajem transakcji — pola bazodanowe.
        var okres = Date.Today.FirstDayMonth();
        InTransaction(() =>
        {
            dok.OkresIntrastat = okres;
            dok.RodzajTransakcji = KodRodzajuTransakcji.Różne;
        });
        var guid = dok.Guid;

        // Zapisujemy do bazy — pola OkresIntrastat/RodzajTransakcji są wtedy trwałe i widoczne dla filtru.
        SaveDispose();

        // Filtr SERWEROWY po dacie (klucz WgDaty — sprawdzony, niezawodny dla przedziału dat).
        // NIE ładujemy całej tabeli; warunek na polu bazodanowym Data trafia do WHERE.
        var od = Date.Today.AddMonths(-1);
        var doDnia = Date.Today.AddMonths(1);
        var dokumenty = Handel.DokHandlowe.WgDaty[(DokumentHandlowy d) =>
                d.Data >= od && d.Data <= doDnia]
            .Cast<DokumentHandlowy>()
            .ToArray();

        // Nasz dokument musi się znaleźć w zbiorze (po Guid).
        dokumenty.Should().Contain(d => d.Guid == guid,
            "dokument z bieżącego miesiąca mieści się w zapytaniu serwerowym po dacie");

        // Kwalifikacja do deklaracji Intrastat: odczytujemy zapisane pole OkresIntrastat z bazy.
        var zapisany = Get<DokumentHandlowy>(guid);
        zapisany.OkresIntrastat.Should().Be(okres,
            "dokument z OkresIntrastat w bieżącym miesiącu kwalifikuje się do deklaracji za ten okres");
    }

    // =================================================================================================
    // W73 — KOMPLETACJA (offline; pełne tworzenie kompletu wymaga konfiguracji spoza Demo)
    // =================================================================================================

    [Test]
    [Description("W73: SposobEdycjiKompletacji odczytany z definicji zwykłego dokumentu (FV) to None — " +
                 "czyli definicja nie obsługuje kompletacji (warunek widoczności akcji PrzeliczWgKartoteki).")]
    public void W73_DefinicjaZwyklaNieObslugujeKompletacji()
    {
        // FV to zwykła faktura — jej definicja nie jest definicją kompletacji.
        var dok = UtworzDokument(Definicje.FakturaSprzedazy, kontrahent: Kontrahent(Kontrahent_.Abc));

        // Publiczny getter Definicja.SposobEdycjiKompletacji; None == brak obsługi kompletacji.
        dok.Definicja.SposobEdycjiKompletacji.Should().Be(SposobEdycjiKompletacji.None,
            "definicja FV nie jest definicją kompletacji");
    }

    [Test]
    [Description("W73: akcja PrzeliczWgKartoteki jest niewidoczna (IsVisiblePrzeliczWgKartoteki == false) " +
                 "dla dokumentu, którego definicja ma SposobEdycjiKompletacji == None — sprawdzane lokalnie.")]
    public void W73_AkcjaPrzeliczWgKartoteki_NiewidocznaDlaDefinicjiBezKompletacji()
    {
        var dok = UtworzDokument(Definicje.FakturaSprzedazy, kontrahent: Kontrahent(Kontrahent_.Abc));

        // Worker kompletacji ma bezparametrowy konstruktor; sprawdzamy czystą logikę widoczności akcji.
        var worker = new Soneta.Handel.Kompletacje.DokumentKompletacjaWorker();

        // Dla SposobEdycjiKompletacji == None akcja jest niewidoczna (operacja lokalna, bez sieci).
        DokumentKompletacjaWorker.IsVisiblePrzeliczWgKartoteki(dok).Should().BeFalse(
            "akcja kompletacji jest ukryta, gdy definicja nie obsługuje kompletacji (None)");
    }

    [Test]
    [Ignore("W73 (utworzenie dokumentu kompletacji + PrzeliczWgKartoteki): wymaga definicji dokumentu z " +
            "SposobEdycjiKompletacji != None oraz kartoteki kompletacji (wyrób + składniki) i magazynu z " +
            "zapisanym przychodem składników (Demo blokuje stan ujemny). Baza Demo nie gwarantuje gotowej " +
            "definicji kompletacji ani kartoteki kompletu — utworzenie ich to dane KONFIGURACYJNE spoza " +
            "zakresu testu dokumentu handlowego. Logika widoczności akcji jest pokryta lokalnie powyżej.")]
    [Description("W73: utworzenie kompletu i przeliczenie wg kartoteki — pominięte (brak definicji/kartoteki kompletacji w Demo).")]
    public void W73_UtworzenieKompletuIPrzeliczenie_Skip() { }

    // =================================================================================================
    // W69 — WALIDACJA STRUKTURY XML KSeF (offline; wymaga wcześniej wygenerowanego XML)
    // =================================================================================================

    [Test]
    [Ignore("W69 (walidacja struktury XML — KSeFSprawdzXMLWorker.Check / KSeFSchemaVerifier.Verify): część " +
            "samej walidacji jest offline (lokalny XSD), ALE warunkiem wstępnym (IsEnabledCheck) jest, by " +
            "dokument miał już WYGENEROWANY plik KSeF (ImportExportKSeF.Xml niepusty). Generowanie XML KSeF " +
            "to operacja modułu KSeF na zatwierdzonej fakturze sprzedaży z kompletem danych podatkowych " +
            "(pieczątka firmy, NIP-y, stawki) — w bazie Demo nie jest to gwarantowane bez konfiguracji KSeF. " +
            "Bez wygenerowanego XML Check() jest no-op / rzuca, więc test offline nie jest wiarygodny. " +
            "Sama wysyłka i pobranie UPO to operacje SIECIOWE (W67/W68) — patrz testy poniżej.")]
    [Description("W69: walidacja struktury XML KSeF — pominięte (wymaga wcześniej wygenerowanego pliku KSeF; offline część nieosiągalna w Demo).")]
    public void W69_WalidacjaStrukturyXml_Skip() { }

    // =================================================================================================
    // W71 — FISKALIZACJA (offline: ustawienie parametrów workera; wydruk = sprzęt → SKIP)
    // =================================================================================================

    [Test]
    [Description("W71: konstrukcja parametrów FiskalizacjaDokumentuWorker.ParametryFiskalizacjiDokumentu " +
                 "przez Context oraz osadzenie ich w workerze (offline — BEZ wywołania Execute/druku). " +
                 "Weryfikujemy, że worker i jego parametry dają się złożyć z publicznego kontraktu.")]
    public void W71_ParametryFiskalizacji_KonstrukcjaIPrzekazanie()
    {
        // Paragon (PAR) to dokument sprzedaży — kandydat do fiskalizacji.
        var dok = UtworzDokument(Definicje.Paragon, kontrahent: Kontrahent(Kontrahent_.Abc));

        var ctx = Session.GetEmptyContext();
        ctx.TryAdd(() => dok);
        // SymbolKasy = symbol drukarki (max 12 znaków) — pole parametru, nie wymaga sprzętu.
        var parametry = new FiskalizacjaDokumentuWorker.ParametryFiskalizacjiDokumentu(ctx)
        {
            SymbolKasy = "DRUK1"
        };

        // Worker z bezparametrowym ctor; właściwości [Context] inicjatorem obiektu.
        var worker = new FiskalizacjaDokumentuWorker { Dokument = dok, Parametry = parametry };

        // Asercja struktury: parametry zostały przekazane do workera (referencja Parametry).
        worker.Parametry.Should().BeSameAs(parametry);
    }

    [Test]
    [Description("W71: IsVisibleExecute jest false dla dokumentu niesprzedażowego (przyjęcie magazynowe PZ) — " +
                 "fiskalizacja dotyczy tylko Sprzedaży/KorektySprzedaży. Czysta logika lokalna, bez druku.")]
    public void W71_IsVisibleExecute_DlaZakupu_False()
    {
        // PZ to przyjęcie magazynowe (przychód, kategoria PrzyjęcieMagazynowe), NIE sprzedaż —
        // nie podlega fiskalizacji (paragon/fiskalizacja dotyczy wyłącznie dokumentów sprzedaży).
        // PZ jest dokumentem magazynowym, więc wymaga magazynu.
        var dok = UtworzDokument(
            Definicje.FakturaZakupu,
            kontrahent: Kontrahent(Kontrahent_.Abc),
            magazyn: Magazyn(Magazyn_.Firma));
        var worker = new FiskalizacjaDokumentuWorker { Dokument = dok };

        // IsVisibleExecute to lokalny warunek widoczności (kategoria dokumentu) — bez sieci/sprzętu.
        FiskalizacjaDokumentuWorker.IsVisibleExecute(dok).Should().BeFalse(
            "fiskalizacja dotyczy tylko dokumentów sprzedaży / korekt sprzedaży");
    }

    [Test]
    [Description("W71: IsEnabledExecute jest false dla dokumentu w BUFORZE — oznaczyć jako zafiskalizowane " +
                 "można tylko dokument zatwierdzony (z pustym SymbolKasy). Sprawdzane lokalnie, bez druku.")]
    public void W71_IsEnabledExecute_DlaBufora_False()
    {
        // Paragon w buforze (świeżo utworzony, Stan == Bufor).
        var dok = UtworzDokument(Definicje.Paragon, kontrahent: Kontrahent(Kontrahent_.Abc));
        dok.Bufor.Should().BeTrue("świeżo utworzony dokument jest w buforze");

        var worker = new FiskalizacjaDokumentuWorker { Dokument = dok };

        // IsEnabledExecute wymaga dokumentu zatwierdzonego — dla bufora zwraca false (logika lokalna).
        FiskalizacjaDokumentuWorker.IsEnabledExecute(dok).Should().BeFalse(
            "oznaczyć jako zafiskalizowane można tylko dokument zatwierdzony");
    }

    [Test]
    [Ignore("W71 (faktyczny wydruk / odczyt SymbolKasy po Execute): klasa Fiscalizer drukuje na DRUKARCE " +
            "FISKALNEJ — operacja SPRZĘTOWA, nie do odtworzenia w teście jednostkowym. Dodatkowo dok.SymbolKasy " +
            "NIE jest publiczną właściwością DokumentHandlowy (brak getter/setter w publicznym kontrakcie), " +
            "więc efekt FiskalizacjaDokumentuWorker.Execute() nie jest odczytywalny z dodatku zewnętrznego. " +
            "Testujemy więc tylko konstrukcję parametrów i warunki IsVisible/IsEnabled (powyżej).")]
    [Description("W71: wydruk fiskalny i odczyt SymbolKasy po Execute — pominięte (sprzęt + pole niepubliczne).")]
    public void W71_WydrukFiskalnyIOdczytSymbolKasy_Skip() { }

    // =================================================================================================
    // W72 — E-PARAGON (offline: ustawienie adresu e-mail; wysyłka/wydruk = sieć/sprzęt → SKIP)
    // =================================================================================================

    [Test]
    [Description("W72: pole dokumentu EParagonAdresEmail jest publicznie zapisywalne — ustawiamy adres " +
                 "e-mail odbiorcy e-paragonu i odczytujemy go z powrotem (offline; bez wysyłki e-mail).")]
    public void W72_EParagonAdresEmail_MoznaUstawicNaDokumencie()
    {
        // Paragon (PAR) — dokument, który może zostać e-paragonem.
        var dok = UtworzDokument(Definicje.Paragon, kontrahent: Kontrahent(Kontrahent_.Abc));

        // EParagonAdresEmail to bazodanowy string (publiczny setter) — ustawienie nie wysyła e-maila.
        InTransaction(() => dok.EParagonAdresEmail = "klient@example.com");

        dok.EParagonAdresEmail.Should().Be("klient@example.com");
    }

    [Test]
    [Ignore("W72 (flaga EParagon, polityka OznaczJakoEParagon, wysyłka e-mail, ponowny wydruk paragonu): " +
            "dok.EParagon NIE jest publiczną właściwością DokumentHandlowy (brak w publicznym kontrakcie), " +
            "więc efekt uboczny ustawienia EParagonAdresEmail (auto EParagon = true) nie jest odczytywalny " +
            "z dodatku zewnętrznego. Sama wysyłka e-paragonu wymaga SIECI (e-mail), a PonownyWydrukParagonuWorker " +
            "drukuje na DRUKARCE FISKALNEJ (sprzęt) — obie operacje nie do odtworzenia w teście jednostkowym. " +
            "Testujemy więc tylko ustawienie EParagonAdresEmail (powyżej).")]
    [Description("W72: flaga EParagon / polityka / wysyłka e-mail / ponowny wydruk — pominięte (pole niepubliczne + sieć/sprzęt).")]
    public void W72_FlagaWysylkaIPonownyWydruk_Skip() { }

    // =================================================================================================
    // W67 / W68 / W70 — KSeF: wysyłka, status, import (SIEĆ → SKIP)
    // =================================================================================================

    [Test]
    [Ignore("W67 (wysłanie faktury do KSeF — KSeFWyslijWorker.Wyslij / KSeFWysylkaWsadowaWorker.WyslijZbiorczo): " +
            "cała komunikacja z bramką KSeF (IKSeFAPIv2Service/IKSeFAPIService) wymaga SIECI — nie do " +
            "odtworzenia w teście jednostkowym. Warunkiem wstępnym jest też zwalidowany XML (W69), którego " +
            "Demo nie gwarantuje. Testujemy w skillu jedynie przygotowanie parametrów/weryfikatora, ale bez " +
            "realnej wysyłki nie ma odczytywalnego efektu offline na publicznym kontrakcie dokumentu.")]
    [Description("W67: wysyłka faktury do KSeF (pojedyncza/zbiorcza) — pominięte (operacja sieciowa).")]
    public void W67_WysylkaKSeF_Skip() { }

    [Test]
    [Ignore("W68 (sprawdzenie statusu KSeF i odczyt numeru KSeF): KSeFSprawdzStatusWorker.SprawdzStatus woła " +
            "bramkę KSeF (SIEĆ) — nie do odtworzenia jednostkowo. Odczyt zapisanego statusu (dok.StatusKSeF) " +
            "i numeru (dok.KSeFKomunikat.NumerDokumentuKSeF) byłby offline, ale wymaga wcześniejszej wysyłki " +
            "ustawiającej KSeFKomunikat — bez niej w Demo nie ma czego odczytać (StatusKSeF == NieDotyczy/Brak), " +
            "więc test nie weryfikowałby realnego zachowania. SKIP: zależność od stanu po operacji sieciowej.")]
    [Description("W68: sprawdzenie statusu i odczyt numeru KSeF — pominięte (sieć + brak danych KSeF w Demo).")]
    public void W68_StatusINumerKSeF_Skip() { }

    [Test]
    [Ignore("W70 (import faktur z KSeF — KSeFDownloadPartWorker.Pobierz): pobranie paczek wyników wymaga SIECI " +
            "(IKSeFAPIv2Service.PobierzFakturyZPaczek) i operuje na rekordach konfiguracyjno-systemowych " +
            "(KSeFZapytanieOFa, KSeFPlik), a nie bezpośrednio na DokumentHandlowy — dokument zakupu powstaje " +
            "dopiero w kolejnym kroku (import XML, obszar księgowy). Brak offline'owego, odczytywalnego efektu " +
            "na dokumencie handlowym. SKIP: operacja sieciowa poza zakresem dokumentu handlowego.")]
    [Description("W70: import faktur zakupu z KSeF — pominięte (operacja sieciowa; obszar konfiguracyjno-księgowy).")]
    public void W70_ImportZKSeF_Skip() { }
}
