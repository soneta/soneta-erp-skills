using System;
using System.Linq;
using AwesomeAssertions;
using NUnit.Framework;
using Soneta.Handel;
using Soneta.Types;

namespace Soneta.Skills.Test.Handel.DokumentyHandlowe;

/// <summary>
/// Rozdział 8 skilla „dokument-handlowy” — VAT, wartości i waluty (W43–W47).
/// <para>
/// Testy weryfikują publiczny kontrakt dokumentu w zakresie tabeli VAT (<c>dok.SumyVAT</c>),
/// podsumowań wartości (<c>dok.Suma</c>, <c>dok.SumaPozycji</c>), ręcznej korekty VAT
/// (<c>dok.KorektaVAT</c>), sposobu liczenia VAT (<c>dok.LiczonaOd</c>) oraz — w zakresie, w jakim
/// nie wymaga to sieci/kursu — zmiany waluty dokumentu (W47).
/// </para>
/// <para>
/// <b>Reguły bazy Demo</b>, których trzymają się testy:
/// <list type="bullet">
/// <item>Demo blokuje stan ujemny (<c>StanUjemnyVerifier</c>): rozchód (FV) wymaga wcześniej
/// <b>zapisanego</b> przyjęcia (PW) tego towaru. Magazyn księguje się dopiero po <c>Session.Save()</c>.</item>
/// <item>Po zapisie w środku testu sesja zamyka okno edycji — kolejna edycja rzuca wyjątek.
/// Wzorzec: zapis przez <c>SaveDispose()</c> → odczyt na świeżej sesji po <c>Guid</c>.</item>
/// </list>
/// Wartości pieniężne tabeli VAT i podsumowań mają dwie reprezentacje: <c>BruttoNetto</c>
/// (<c>Netto</c>/<c>VAT</c>/<c>Brutto</c> jako <c>decimal</c>, waluta systemowa) oraz
/// <c>BruttoNettoCy</c> (<c>NettoCy</c>/<c>VatCy</c>/<c>BruttoCy</c> jako <c>Currency</c>, waluta dokumentu).
/// </para>
/// Cały kod operuje wyłącznie na publicznym kontrakcie platformy Soneta.
/// </summary>
[TestFixture]
public class Rozdzial08_VatWalutyTest : DokumentHandlowyTestBase
{
    /// <summary>
    /// Przyjmuje BIKINI na magazyn „F” dokumentem PW, <b>zatwierdza</b> je i zapisuje — buduje stan
    /// magazynu pod późniejszy rozchód (FV). Dopiero ZATWIERDZONE i zapisane przyjęcie księguje
    /// zasoby/obroty; przyjęcie w buforze NIE księguje stanu, więc rozchód FV odrzuciłaby kontrola
    /// stanu ujemnego bazy Demo. Deleguje do bazowego helpera <see cref="PrzyjmijNaStan"/>.
    /// </summary>
    private void PrzyjmijBikiniNaStan(double ilosc = 100, double cena = 25)
    {
        // PW musi być ZATWIERDZONE przed Save, aby zaksięgować stan — robi to PrzyjmijNaStan.
        PrzyjmijNaStan(Towar_.Bikini, ilosc, cena);
    }

    /// <summary>
    /// Tworzy i ZAPISUJE fakturę sprzedaży (FV) z jedną pozycją BIKINI liczoną od netto.
    /// Najpierw przyjmuje towar na stan (rozchód FV inaczej zablokuje kontrola stanu ujemnego).
    /// Zwraca Guid zapisanej FV — dalsze asercje odczytują dokument na świeżej sesji.
    /// </summary>
    private Guid UtworzZapisanaFvOdNetto(double ilosc = 2, double cena = 50)
    {
        // Warunek wstępny: zapisane przyjęcie tego towaru (rozchód FV inaczej zablokowany).
        PrzyjmijBikiniNaStan(ilosc: Math.Max(100, ilosc), cena: 25);

        Guid guidFv = Guid.Empty;
        var fv = UtworzDokument(
            Definicje.FakturaSprzedazy,
            kontrahent: Kontrahent(Kontrahent_.Abc),
            magazyn: Magazyn(Magazyn_.Firma));
        InTransaction(() =>
        {
            fv.Data = Date.Today;
            fv.DataOperacji = Date.Today;
            // LiczonaOd ustawiamy PRZED pozycjami (W46) — zmiana po pozycjach wymusza przeliczenie cen.
            fv.LiczonaOd = SposobLiczeniaVAT.OdNetto;
            DodajPozycje(fv, Towar(Towar_.Bikini), ilosc, cena);
            guidFv = fv.Guid;
        });
        SaveDispose();
        return guidFv;
    }

    // ===================================================================================
    // W43 — Odczytanie tabeli VAT (dok.SumyVAT)
    // ===================================================================================

    [Test]
    [Description("W43: po zapisaniu FV (od netto, pozycja BIKINI) dok.SumyVAT zawiera co najmniej jedną " +
                 "stawkę, a kwoty Netto/VAT/Brutto na wierszu SumaVAT są spójne (netto+vat == brutto, wszystkie > 0).")]
    public void W43_TabelaVat_NiepustaISensowneKwoty()
    {
        // Arrange + Act: zapisana FV od netto (2 szt po 50 = netto 100).
        var guidFv = UtworzZapisanaFvOdNetto(ilosc: 2, cena: 50);

        // Odczyt na świeżej sesji po Guid — potwierdza trwały zapis i wyliczoną tabelę VAT.
        var dok = Get<DokumentHandlowy>(guidFv);
        dok.Should().NotBeNull();

        // dok.SumyVAT to SubTable<SumaVAT> — jedna pozycja na każdą stawkę dokumentu.
        var sumy = dok.SumyVAT.Cast<SumaVAT>().ToList();
        sumy.Should().NotBeEmpty("tabela VAT jest wyliczana z pozycji dokumentu");

        // Dla każdego wiersza VAT: kwoty w walucie systemowej (BruttoNetto, decimal).
        foreach (var s in sumy)
        {
            decimal netto = s.Suma.Netto;
            decimal vat = s.Suma.VAT;
            decimal brutto = s.Suma.Brutto;

            netto.Should().BeGreaterThan(0m, "wiersz VAT pochodzi z pozycji o dodatniej wartości");
            vat.Should().BeGreaterThanOrEqualTo(0m, "kwota podatku nie jest ujemna");
            brutto.Should().BeGreaterThan(0m);
            // Spójność rozbicia: brutto = netto + vat (na poziomie pojedynczej stawki).
            brutto.Should().Be(netto + vat, "brutto stawki to suma netto i VAT");
        }

        // Łączny VAT z tabeli VAT (tabela jest mała — .Sum jest akceptowalne, patrz pułapki W43).
        decimal vatRazem = sumy.Sum(s => s.Suma.VAT);
        vatRazem.Should().BeGreaterThan(0m, "FV ze stawką VAT > 0 nalicza podatek");
    }

    [Test]
    [Description("W43: wiersz SumaVAT udostępnia kwoty w walucie dokumentu (SumaCy: BruttoNettoCy) jako Currency; " +
                 "dla dokumentu krajowego (PLN) brutto walutowe odpowiada brutto w walucie systemowej.")]
    public void W43_TabelaVat_KwotyWalutoweCy()
    {
        var guidFv = UtworzZapisanaFvOdNetto(ilosc: 1, cena: 100);
        var dok = Get<DokumentHandlowy>(guidFv);

        var pierwszy = dok.SumyVAT.Cast<SumaVAT>().First();

        // SumaCy to BruttoNettoCy — kwoty jako Currency (wartość + symbol waluty).
        Currency bruttoCy = pierwszy.SumaCy.BruttoCy;

        // Dla dokumentu krajowego waluta dokumentu = systemowa; wartość brutto musi się zgadzać.
        ((double)bruttoCy.Value).Should().BeApproximately((double)pierwszy.Suma.Brutto, 0.005,
            "dla dokumentu krajowego SumaCy.BruttoCy odpowiada Suma.Brutto");
    }

    // ===================================================================================
    // W44 — Odczyt podsumowań wartości dokumentu (dok.Suma, dok.SumaPozycji)
    // ===================================================================================

    [Test]
    [Description("W44: dok.Suma (BruttoNetto) podaje podsumowanie netto/VAT/brutto całego dokumentu; " +
                 "dla FV 2 szt po 50 (od netto) netto == 100, a brutto == netto + VAT.")]
    public void W44_PodsumowanieDokumentu_Suma()
    {
        var guidFv = UtworzZapisanaFvOdNetto(ilosc: 2, cena: 50);
        var dok = Get<DokumentHandlowy>(guidFv);

        // dok.Suma to BruttoNetto — kwoty decimal w walucie systemowej.
        decimal netto = dok.Suma.Netto;
        decimal vat = dok.Suma.VAT;
        decimal brutto = dok.Suma.Brutto;

        // Netto jest dodatnie i nie większe niż cena*ilość (kontrahent Abc ma rabat → netto może być < 100).
        netto.Should().BeGreaterThan(0m, "dokument z pozycją ma dodatnią wartość netto");
        ((double)netto).Should().BeLessThanOrEqualTo(100.0, "netto nie przekracza ceny*ilości (2*50); rabat może je obniżyć");
        vat.Should().BeGreaterThan(0m, "FV ze stawką VAT nalicza podatek");
        brutto.Should().Be(netto + vat, "brutto dokumentu = netto + VAT");
    }

    [Test]
    [Description("W44: dok.SumaPozycji (BruttoNettoPozycji, read-only) liczona z pozycji jest spójna z dok.Suma " +
                 "dla zapisanego dokumentu (po przeliczeniu obie reprezentacje są zgodne).")]
    public void W44_SumaPozycji_SpojnaZSuma()
    {
        var guidFv = UtworzZapisanaFvOdNetto(ilosc: 3, cena: 40);
        var dok = Get<DokumentHandlowy>(guidFv);

        // SumaPozycji jest wyliczana na bieżąco z pozycji; dla zapisanego dokumentu == dok.Suma.
        var sp = dok.SumaPozycji;
        sp.Netto.Should().Be(dok.Suma.Netto, "po zapisie suma z pozycji odpowiada podsumowaniu dokumentu");
        sp.VAT.Should().Be(dok.Suma.VAT);
        sp.Brutto.Should().Be(dok.Suma.Brutto);

        // Wartość netto wynika z pozycji (3 szt * 40 = 120 przed rabatem); kontrahent Abc ma rabat,
        // więc asercja jest na dodatniość i górne ograniczenie, nie na sztywną kwotę.
        sp.Netto.Should().BeGreaterThan(0m);
        ((double)sp.Netto).Should().BeLessThanOrEqualTo(120.0, "netto pozycji nie przekracza ceny*ilości (rabat może obniżyć)");
    }

    // ===================================================================================
    // W45 — Ręczna korekta tabeli VAT (dok.KorektaVAT)
    // ===================================================================================

    [Test]
    [Description("W45: ustawienie dok.KorektaVAT = true jest trwałe — po zapisie i odczycie na świeżej sesji " +
                 "flaga pozostaje włączona (publiczny tor korekty tabeli VAT, worker korekty jest internal).")]
    public void W45_KorektaVat_FlagaUstawiana()
    {
        // Tworzymy FV od netto z pozycją (potrzebny stan magazynu pod rozchód).
        var guidFv = UtworzZapisanaFvOdNetto(ilosc: 1, cena: 100);

        // Po Save okno edycji jest zamknięte → odczyt świeżej sesji i edycja w nowej transakcji.
        var dok = Get<DokumentHandlowy>(guidFv);

        // Włączenie ręcznej korekty — publiczny tor (KorektaTabeliVATWorker jest internal).
        InTransaction(() => dok.KorektaVAT = true);
        var guid = dok.Guid;
        SaveDispose();

        // Asercja na świeżej sesji: flaga zapisana trwale.
        var zapis = Get<DokumentHandlowy>(guid);
        zapis.KorektaVAT.Should().BeTrue("KorektaVAT = true odblokowuje ręczną edycję tabeli VAT i jest zapisywana");
    }

    // ===================================================================================
    // W46 — Sposób liczenia VAT (dok.LiczonaOd)
    // ===================================================================================

    [Test]
    [Description("W46: dok.LiczonaOd ustawione na OdNetto PRZED pozycjami jest zapisywane i odczytywane " +
                 "na świeżej sesji; enum SposobLiczeniaVAT.OdNetto == 1.")]
    public void W46_LiczonaOd_OdNetto()
    {
        // UtworzZapisanaFvOdNetto ustawia LiczonaOd = OdNetto przed dodaniem pozycji.
        var guidFv = UtworzZapisanaFvOdNetto(ilosc: 1, cena: 50);

        var dok = Get<DokumentHandlowy>(guidFv);
        dok.LiczonaOd.Should().Be(SposobLiczeniaVAT.OdNetto, "dokument liczony od kwot netto");
    }

    [Test]
    [Description("W46: dok.LiczonaOd ustawione na OdBrutto PRZED pozycjami jest trwałe; " +
                 "wartość 0 jest niedozwolona, więc zawsze ustawiamy konkretny wariant enuma (OdBrutto == 2).")]
    public void W46_LiczonaOd_OdBrutto()
    {
        // Warunek wstępny: zapisane przyjęcie pod rozchód FV.
        PrzyjmijBikiniNaStan();

        Guid guidFv = Guid.Empty;
        var fv = UtworzDokument(
            Definicje.FakturaSprzedazy,
            kontrahent: Kontrahent(Kontrahent_.Abc),
            magazyn: Magazyn(Magazyn_.Firma));
        InTransaction(() =>
        {
            fv.Data = Date.Today;
            fv.DataOperacji = Date.Today;
            // Ustawiamy sposób liczenia PRZED pozycjami (W46) — wpływa na przeliczenie netto↔brutto.
            fv.LiczonaOd = SposobLiczeniaVAT.OdBrutto;
            DodajPozycje(fv, Towar(Towar_.Bikini), 1, 123);
            guidFv = fv.Guid;
        });
        SaveDispose();

        var dok = Get<DokumentHandlowy>(guidFv);
        dok.LiczonaOd.Should().Be(SposobLiczeniaVAT.OdBrutto, "dokument liczony od kwot brutto");
        // Tabela VAT wyliczona także dla liczenia od brutto.
        dok.SumyVAT.Cast<SumaVAT>().Should().NotBeEmpty();
    }

    // ===================================================================================
    // W47 — Zmiana waluty dokumentu i cen (SKIP — wymaga kursu/sieci, worker internal)
    // ===================================================================================

    [Test]
    [Ignore("W47 — zmiana waluty dokumentu wymaga kursu na wskazaną datę. Worker " +
            "DokumentHandlowyZmianaWalutyWorker jest INTERNAL (nie do zainstancjonowania z dodatku " +
            "zewnętrznego), a baza Demo zwykle nie ma kursu EUR „na dziś” — próba przeliczenia rzuca " +
            "KursWalutyNotFoundException. Pobranie aktualnego kursu wymagałoby sieci (NBP), czego testy " +
            "nie robią (reguła: bez sieci). Publiczny tor to akcja Czynności z parametrami " +
            "DokumentHandlowyZmianaWalutyWorkerParams lub ręczne ustawienie pól waluty/kursu — oba " +
            "zależne od istniejącego kursu w bazie. SKIP wg pułapek W47 (brak gwarantowanego kursu, bez sieci).")]
    [Description("W47: zmiana waluty dokumentu (EUR) z przeliczeniem cen — pominięte (wymaga kursu/sieci; worker internal).")]
    public void W47_ZmianaWaluty_Skip() { }
}
