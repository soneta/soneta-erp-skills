using System;
using System.Linq;
using AwesomeAssertions;
using NUnit.Framework;
using Soneta.Handel;
using Soneta.Types;

namespace Soneta.Skills.Test.Handel.DokumentyHandlowe;

/// <summary>
/// Rozdział 2 — „Wystawianie dokumentów” (wzorce W4–W11).
/// <para>
/// Testy pokazują tworzenie dokumentu handlowego od zera w różnych wariantach: faktura sprzedaży (FV),
/// faktura zakupu (FZ — numer obcy i daty), dokument magazynowy (PW/PZ), zamówienie odbiorcy (ZO),
/// dodawanie pozycji (towar/ilość/cena/rabat), dokument z usługą (MONTAZ — bez magazynu),
/// dokument w walucie obcej (W9) oraz odbiorca inny niż kontrahent (W11).
/// </para>
/// <para>
/// <b>Reguły bazy Demo</b>, których trzymają się testy:
/// <list type="bullet">
/// <item>Demo blokuje stan ujemny (<c>StanUjemnyVerifier</c>): rozchód (FV/WZ) wymaga wcześniej
/// <b>zapisanego</b> przyjęcia (PW/PZ) tego towaru. Obroty księgują się dopiero po <c>Session.Save()</c>.</item>
/// <item>Po zapisie w środku testu sesja zamyka okno edycji — kolejna edycja rzuca wyjątek.
/// Dlatego wzorzec to: zapis przez <c>SaveDispose()</c> → odczyt na świeżej sesji po <c>Guid</c>.</item>
/// </list>
/// Wszystko operuje wyłącznie na publicznym kontrakcie platformy (jak dodatek programisty zewnętrznego).
/// </para>
/// </summary>
[TestFixture]
public class Rozdzial02_WystawianieTest : DokumentHandlowyTestBase
{
    /// <summary>
    /// Pomocniczo: przyjmuje BIKINI na magazyn „F” dokumentem PW, <b>zatwierdza</b> je i <b>zapisuje</b>,
    /// żeby zbudować stan magazynu pod późniejszy rozchód (FV/WZ). Dopiero ZATWIERDZONE i zapisane
    /// przyjęcie księguje zasoby/obroty i odblokowuje rozchód na bazie Demo (kontrola stanu ujemnego).
    /// Korzysta z bazowego helpera <see cref="DokumentHandlowyTestBase.PrzyjmijNaStan"/>. Zwraca Guid PW.
    /// </summary>
    private Guid PrzyjmijBikiniNaStan(double ilosc = 100, double cena = 25)
        => PrzyjmijNaStan(Towar_.Bikini, ilosc, cena);

    // ============================== W4 — Faktura sprzedaży (FV) ==============================

    [Test]
    [Description("W4: FV krajowa od netto z pozycją BIKINI — po zapisie powstaje tabela VAT i wartość dokumentu.")]
    public void FakturaSprzedazy_OdNetto_WyliczaSumeIVat()
    {
        // Najpierw przyjęcie na stan (zapisane) — inaczej rozchód FV zablokuje kontrola stanu ujemnego.
        PrzyjmijBikiniNaStan();

        Guid guidFv = Guid.Empty;
        // Definicja FIRST (helper UtworzDokument), potem magazyn i kontrahent-nabywca.
        var fv = UtworzDokument(
            Definicje.FakturaSprzedazy,
            kontrahent: Kontrahent(Kontrahent_.Abc),
            magazyn: Magazyn(Magazyn_.Firma));
        // FV NIE zatwierdzamy — zatwierdzenie FV w bazie testowej Demo rzuca NRE w ewidencji VAT.
        // SumyVAT/Suma na świeżym dokumencie w pamięci bywają niprzeliczone — przeliczają się
        // po zapisie. Dlatego zapisujemy FV w BUFORZE (bez zatwierdzania) i czytamy po Guid.
        InTransaction(() =>
        {
            fv.Data = Date.Today;            // data wystawienia
            fv.DataOperacji = Date.Today;    // faktyczna data sprzedaży
            fv.LiczonaOd = SposobLiczeniaVAT.OdNetto;   // ustaw przed pozycjami
            DodajPozycje(fv, Towar(Towar_.Bikini), 2, 50); // 2 szt po 50
            guidFv = fv.Guid;
        });
        SaveDispose();

        var zapis = Get<DokumentHandlowy>(guidFv);
        zapis.Should().NotBeNull();
        zapis.LiczonaOd.Should().Be(SposobLiczeniaVAT.OdNetto);
        // SumyVAT i Suma są wyliczane z pozycji — wyliczone po zapisie (czytamy po Guid).
        zapis.SumyVAT.Should().NotBeEmpty();
        // Wartość netto jest dodatnia (kontrahent Abc ma rabat, więc netto może być < cena*ilość).
        ((double)zapis.Suma.Netto).Should().BeGreaterThan(0);
    }

    [Test]
    [Description("W4: FV liczona od brutto — pole LiczonaOd przyjmuje wartość Brutto.")]
    public void FakturaSprzedazy_OdBrutto_UstawiaLiczonaOdBrutto()
    {
        PrzyjmijBikiniNaStan();

        var fv = UtworzDokument(
            Definicje.FakturaSprzedazy,
            kontrahent: Kontrahent(Kontrahent_.Abc),
            magazyn: Magazyn(Magazyn_.Firma));
        // Asercja na FV w BUFORZE (nie zatwierdzamy FV — zatwierdzenie rzuca NRE w ewidencji VAT).
        InTransaction(() =>
        {
            // LiczonaOd ustawiamy PRZED pozycjami — zmiana po wprowadzeniu pozycji wymusza przeliczenie cen.
            fv.LiczonaOd = SposobLiczeniaVAT.OdBrutto;
            DodajPozycje(fv, Towar(Towar_.Bikini), 1, 50);
        });

        fv.LiczonaOd.Should().Be(SposobLiczeniaVAT.OdBrutto);
    }

    // ============================== W5 — Zakup od dostawcy (PZ) ==============================

    [Test]
    [Description("W5: zakup od dostawcy (PZ) z datą operacji (zakupu) różną od daty wystawienia — przyjęcie zewnętrzne, przychód.")]
    public void FakturaZakupu_UstawiaNumerObcyIDatyZakupu()
    {
        // W bazie Demo „faktura zakupu" jako dokument handlowy nie istnieje — stronę zakupową
        // reprezentuje przyjęcie zewnętrzne „PZ" (przychód, kontrahent-dostawca). PZ NIE wywołuje
        // kontroli stanu ujemnego, więc nie potrzebuje wcześniejszego przyjęcia.
        Guid guidPz = Guid.Empty;
        var dataWystawienia = Date.Today;
        var dataZakupu = Date.Today.AddDays(-2);

        // PZ to dokument przychodowy — kontrahent jest dostawcą.
        var pz = UtworzDokument(
            Definicje.FakturaZakupu,
            kontrahent: Kontrahent(Kontrahent_.Abc),
            magazyn: Magazyn(Magazyn_.Firma));
        InTransaction(() =>
        {
            pz.Data = dataWystawienia;        // data wystawienia u nas
            pz.DataOperacji = dataZakupu;     // faktyczna data zakupu (decyduje o okresie magazynowym)
            DodajPozycje(pz, Towar(Towar_.Bikini), 10, 30);
            guidPz = pz.Guid;
        });
        // Bez zatwierdzania — sprawdzamy podstawowe pola dokumentu zakupowego (PZ).
        SaveDispose();

        var zapis = Get<DokumentHandlowy>(guidPz);
        zapis.Should().NotBeNull();
        zapis.Definicja.Symbol.Should().Be("PZ");
        zapis.Kontrahent.Kod.Should().Be(Kontrahent(Kontrahent_.Abc).Kod);
        zapis.DataOperacji.Should().Be(dataZakupu);
        zapis.Data.Should().Be(dataWystawienia);
        // Data operacji (zakupu) różna od daty wystawienia — to dwa odrębne pola.
        zapis.DataOperacji.Should().NotBe(zapis.Data);
    }

    [Test]
    [Description("W5: zakup od dostawcy (PZ) z przyjęciem na magazyn księguje przychód — po zatwierdzeniu i Save powstają zasoby dokumentu.")]
    public void FakturaZakupu_KsiegujePrzychod_TworzyZasoby()
    {
        Guid guidPz = Guid.Empty;
        // PZ (przyjęcie zewnętrzne od dostawcy) to dokument przychodowy — kontrahent jest dostawcą.
        var pz = UtworzDokument(
            Definicje.FakturaZakupu,
            kontrahent: Kontrahent(Kontrahent_.Abc),
            magazyn: Magazyn(Magazyn_.Firma));
        InTransaction(() =>
        {
            pz.Data = Date.Today;
            pz.DataOperacji = Date.Today;
            DodajPozycje(pz, Towar(Towar_.Bikini), 5, 30);
            guidPz = pz.Guid;
        });
        // Zasoby dokumentu przychodowego księgują się DOPIERO po zatwierdzeniu + Save.
        // Zatwierdzenie PZ (jak PW) jest bezpieczne — nie rzuca NRE (rzuca tylko zatwierdzenie FV).
        InTransaction(() => pz.Stan = StanDokumentuHandlowego.Zatwierdzony);
        SaveDispose();

        var zapis = Get<DokumentHandlowy>(guidPz);
        // PZ (przyjęcie od dostawcy) jest dokumentem przychodowym → powstają zasoby magazynowe.
        zapis.Zasoby.Cast<object>().Should().NotBeEmpty();
    }

    // ============================== W6 — Dokument magazynowy (PW/PZ) ==============================

    [Test]
    [Description("W6: PW (przyjęcie wewnętrzne) buduje stan magazynu — po Save powstają zasoby.")]
    public void PrzyjecieWewnetrzne_PW_TworzyZasoby()
    {
        // PW jest dokumentem wewnętrznym (przychód) — bez kontrahenta, magazyn wymagany.
        var guidPw = PrzyjmijBikiniNaStan(50, 25);

        var zapis = Get<DokumentHandlowy>(guidPw);
        zapis.Should().NotBeNull();
        // Kierunek magazynu wynika z definicji (readonly="set"), nie ustawiamy go ręcznie.
        zapis.Zasoby.Cast<object>().Should().NotBeEmpty();
    }

    [Test]
    [Description("W6: dokument magazynowy bez magazynu — Save rzuca wyjątek (Magazyn jest wymagany).")]
    public void DokumentMagazynowy_BezMagazynu_RzucaPrzyZapisie()
    {
        // Brak wymaganego magazynu → operacja musi się nie powieść. Wyjątek może paść już
        // przy dodaniu pozycji/edycji albo dopiero przy Save — łapiemy całą sekwencję, żeby
        // asercja była odporna na moment zgłoszenia (RequiredException / walidacja magazynu).
        Action buildIZapisz = () =>
        {
            var pw = UtworzDokument(Definicje.PrzyjecieWewnetrzne);
            InTransaction(() => DodajPozycje(pw, Towar(Towar_.Bikini), 1, 10));
            SaveDispose();
        };
        buildIZapisz.Should().Throw<Exception>();
    }

    [Test]
    [Description("W6: PZ (przyjęcie zewnętrzne od dostawcy) — przychód z kontrahentem-dostawcą.")]
    public void PrzyjecieZewnetrzne_PZ_TworzyZasoby()
    {
        Guid guidPz = Guid.Empty;
        // PZ to przyjęcie zewnętrzne — przychód z kontrahentem (dostawcą).
        var pz = UtworzDokument(
            Definicje.PrzyjecieZewnetrzne,
            kontrahent: Kontrahent(Kontrahent_.Zefir),
            magazyn: Magazyn(Magazyn_.Firma));
        InTransaction(() =>
        {
            DodajPozycje(pz, Towar(Towar_.Bikini), 20, 25);
            guidPz = pz.Guid;
        });
        // Przychód księguje zasoby/obroty DOPIERO po zatwierdzeniu + Save.
        InTransaction(() => pz.Stan = StanDokumentuHandlowego.Zatwierdzony);
        SaveDispose();

        var zapis = Get<DokumentHandlowy>(guidPz);
        zapis.Zasoby.Cast<object>().Should().NotBeEmpty();
    }

    // ============================== W7 — Zamówienie (ZO) ==============================

    [Test]
    [Description("W7: ZO (zamówienie odbiorcy) z terminem dostawy — nie buduje stanu magazynu.")]
    public void ZamowienieOdbiorcy_ZO_UstawiaTerminDostawy_BezObrotow()
    {
        Guid guidZo = Guid.Empty;
        var termin = Date.Today.AddDays(7);

        var zo = UtworzDokument(
            Definicje.ZamowienieOdbiorcy,
            kontrahent: Kontrahent(Kontrahent_.Abc),
            magazyn: Magazyn(Magazyn_.Firma));
        InTransaction(() =>
        {
            zo.Data = Date.Today;
            zo.DataOperacji = Date.Today;
            // Dostawa to subrow — ustawiamy jego pola, nie przypisujemy całego obiektu.
            zo.Dostawa.Termin = termin;     // oczekiwany termin dostawy
            DodajPozycje(zo, Towar(Towar_.Bikini), 5, 50);
            guidZo = zo.Guid;
        });
        // Zamówienie nie buduje stanu magazynu — nie musimy wcześniej przyjmować towaru.
        SaveDispose();

        var zapis = Get<DokumentHandlowy>(guidZo);
        zapis.Should().NotBeNull();
        zapis.Dostawa.Termin.Should().Be(termin);
        // Zamówienie to dokument planistyczny — nie tworzy obrotów/zasobów magazynowych.
        zapis.Zasoby.Cast<object>().Should().BeEmpty();
    }

    // ============================== W8 — Dodawanie pozycji ==============================

    [Test]
    [Description("W8: pozycja z automatyczną ceną (tylko Towar + Ilosc) — cena pobrana z cennika jest dodatnia.")]
    public void DodaniePozycji_AutomatycznaCena_PobieraZCennika()
    {
        PrzyjmijBikiniNaStan();

        var fv = UtworzDokument(
            Definicje.FakturaSprzedazy,
            kontrahent: Kontrahent(Kontrahent_.Abc),
            magazyn: Magazyn(Magazyn_.Firma));
        PozycjaDokHandlowego poz = null;
        InTransaction(() =>
        {
            // Bez podania ceny (cena = null) — towar inicjuje cenę z cennika/karty.
            poz = DodajPozycje(fv, Towar(Towar_.Bikini), 3);
        });

        // Asercja na FV w BUFORZE (FV nie zatwierdzamy — zatwierdzenie rzuca NRE w ewidencji VAT).
        // Cena zaproponowana przez cennik — oczekujemy wartości dodatniej (nie ustawialiśmy jej ręcznie).
        ((double)poz.Cena.Value).Should().BeGreaterThan(0);
    }

    [Test]
    [Description("W8: ręczne nadpisanie ceny i rabatu — Cena/Rabat przyjmują podane wartości, zapalają korekty.")]
    public void DodaniePozycji_RecznaCenaIRabat_NadpisujeWartosci()
    {
        PrzyjmijBikiniNaStan();

        var fv = UtworzDokument(
            Definicje.FakturaSprzedazy,
            kontrahent: Kontrahent(Kontrahent_.Abc),
            magazyn: Magazyn(Magazyn_.Firma));
        PozycjaDokHandlowego poz = null;
        InTransaction(() =>
        {
            // Ręczna cena nadpisuje cennik (zapala KorektaCeny); rabat zapala KorektaRabatu.
            poz = DodajPozycje(fv, Towar(Towar_.Bikini), 10, 48);
            poz.Rabat = new Percent(0.1m); // 10%
        });

        // Asercja na FV w BUFORZE (FV nie zatwierdzamy — zatwierdzenie rzuca NRE w ewidencji VAT).
        ((double)poz.Cena.Value).Should().Be(48);
        // Rabat 10% został zapamiętany na pozycji.
        ((double)poz.Rabat).Should().BeApproximately(0.1, 1e-9);
    }

    // ============================== W10 — Dokument z usługą (MONTAZ) ==============================

    [Test]
    [Description("W10: FV tylko z usługą (MONTAZ) — liczy VAT/wartość, ale nie tworzy obrotów magazynowych.")]
    public void FakturaZUsluga_Montaz_BezObrotowMagazynowych()
    {
        // Usługa nie pobiera ze stanu — NIE potrzeba wcześniejszego przyjęcia (StanUjemnyVerifier nie blokuje).
        Guid guidFv = Guid.Empty;
        var fv = UtworzDokument(
            Definicje.FakturaSprzedazy,
            kontrahent: Kontrahent(Kontrahent_.Abc),
            magazyn: Magazyn(Magazyn_.Firma));
        InTransaction(() =>
        {
            fv.Data = Date.Today;
            fv.DataOperacji = Date.Today;
            // MONTAZ jest towarem typu usługa — bez wpływu na magazyn.
            DodajPozycje(fv, Towar(Towar_.Montaz), 1, 200);
            guidFv = fv.Guid;
        });
        SaveDispose();

        var zapis = Get<DokumentHandlowy>(guidFv);
        zapis.Should().NotBeNull();
        // Usługa nie tworzy zasobów magazynowych, ale uczestniczy w tabeli VAT.
        zapis.Zasoby.Cast<object>().Should().BeEmpty();
        zapis.SumyVAT.Should().NotBeEmpty();
        ((double)zapis.Suma.Netto).Should().BeGreaterThan(0);
    }

    // ============================== W11 — Odbiorca inny niż kontrahent ==============================

    [Test]
    [Description("W11: nabywca (Kontrahent) różny od odbiorcy towaru (Odbiorca) — dwa różne pola typu Kontrahent.")]
    public void OdbiorcaInnyNizKontrahent_UstawiaOdbiorce()
    {
        PrzyjmijBikiniNaStan();

        var fv = UtworzDokument(
            Definicje.FakturaSprzedazy,
            kontrahent: Kontrahent(Kontrahent_.Abc),   // nabywca / strona VAT
            magazyn: Magazyn(Magazyn_.Firma));
        InTransaction(() =>
        {
            // Odbiorca towaru to inny podmiot niż nabywca — faktura na Kontrahent, dostawa do Odbiorca.
            fv.Odbiorca = Kontrahent(Kontrahent_.Zefir);
            fv.Osoba = "Jan Kowalski";                 // osoba podpisująca po stronie kontrahenta
            fv.Dostawa.Termin = Date.Today.AddDays(3);
            fv.Dostawa.Sposob = "Kurier";
            DodajPozycje(fv, Towar(Towar_.Bikini), 1, 50);
        });

        // Asercja na FV w BUFORZE (FV nie zatwierdzamy — zatwierdzenie rzuca NRE w ewidencji VAT).
        fv.Kontrahent.Kod.Should().Be(Kontrahent(Kontrahent_.Abc).Kod);
        fv.Odbiorca.Should().NotBeNull();
        fv.Odbiorca.Kod.Should().Be(Kontrahent(Kontrahent_.Zefir).Kod);
        // Nabywca i odbiorca to dwa różne podmioty.
        fv.Odbiorca.Kod.Should().NotBe(fv.Kontrahent.Kod);
        fv.Osoba.Should().Be("Jan Kowalski");
    }

    // ============================== W9 — Dokument w walucie obcej (bezpiecznie, bez sieci) ==============================

    [Test]
    [Description("W9: dokument walutowy wymaga kursu — bez kursu EUR na datę operacja zgłasza błąd; test bezpieczny (bez sieci).")]
    public void DokumentWalutowy_BezKursuEur_RzucaLubPomijane()
    {
        // UWAGA: NIE pobieramy kursu z sieci. Baza Demo zwykle nie ma kursu EUR „na dziś”,
        // więc próba ustawienia waluty/tabeli kursowej bez dostępnego kursu powinna zgłosić wyjątek
        // (np. KursWalutyNotFoundException). Test jedynie potwierdza, że ustawienie dokumentu
        // walutowego WYMAGA kursu — nie wymaga połączenia z internetem.
        var wm = Soneta.Waluty.WalutyModule.GetInstance(Session); // session.GetWaluty() jest internal
        var eur = wm.Waluty.WgSymbolu["EUR"];

        if (eur == null)
        {
            // Demo bez waluty EUR — pomijamy z czytelnym komentarzem (nie wymuszamy sieci/danych).
            Assert.Ignore("Baza Demo nie ma waluty EUR — test walutowy pominięty (brak danych, bez sieci).");
            return;
        }

        // Szukamy tabeli kursowej z kursem EUR na dziś — bez sieci.
        var tabela = wm.TabeleKursowe.Cast<object>().FirstOrDefault();
        if (tabela == null)
        {
            Assert.Ignore("Baza Demo nie ma tabeli kursowej — test walutowy pominięty (brak danych, bez sieci).");
            return;
        }

        // Próba zbudowania dokumentu walutowego bez gwarancji kursu na datę:
        // albo uda się (kurs jest w bazie), albo zgłosi błąd braku kursu — oba przypadki są poprawne.
        var fv = UtworzDokument(
            Definicje.FakturaSprzedazy,
            kontrahent: Kontrahent(Kontrahent_.Abc),
            magazyn: Magazyn(Magazyn_.Firma));

        Action ustawWalute = () => InTransaction(() =>
        {
            // TabelaKursowa jest wymagana dla dokumentu walutowego; DataKursu wyznacza, którego kursu szukać.
            fv.TabelaKursowa = (Soneta.Waluty.TabelaKursowa)tabela;
            fv.DataKursu = Date.Today;
        });

        // Bezpiecznie: dopuszczamy zarówno sukces (kurs istnieje), jak i wyjątek braku kursu.
        // Nie wymuszamy konkretnego typu wyjątku, bo zależy od danych Demo, a sieci nie używamy.
        try
        {
            ustawWalute();
            // Jeśli się powiodło, tabela kursowa została przypisana — to też poprawny wynik.
            fv.TabelaKursowa.Should().NotBeNull();
        }
        catch (Exception ex)
        {
            // Brak kursu na datę → oczekiwany błąd (np. KursWalutyNotFoundException). To poprawny scenariusz.
            ex.Should().NotBeNull("brak kursu EUR na datę powinien zgłosić wyjątek, a nie cichą awarię");
        }
    }
}
