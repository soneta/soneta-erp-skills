using System.Linq;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Soneta.Handel;
using Soneta.Handel.RelacjeDokumentow.Api;

namespace Soneta.Skills.Test.Handel.DokumentyHandlowe;

/// <summary>
/// Rozdział 4 — Relacje i generowanie dokumentów (HANDEL-W17–HANDEL-W24).
/// Cały rozdział korzysta wyłącznie z publicznego toru przekształceń:
/// serwisu <see cref="IRelacjeService"/> (scope: Session) oraz pól kalkulowanych
/// <c>DokumentyMagazynowe</c> / <c>DokumentyHandlowe</c>.
/// <para>
/// Reguły wspólne (zob. dokumentacja, rozdz. 4):
/// <list type="bullet">
/// <item>dokumenty nadrzędne muszą być <b>zatwierdzone</b> — z bufora relacja nie powstanie,</item>
/// <item>wywołanie metody serwisu jest operacją modyfikującą — działa w transakcji edycyjnej
/// (<c>Session.Logout(editMode: true)</c>), po niej <c>Session.Save()</c>,</item>
/// <item>rozchód (FV/WZ) wymaga wcześniejszego <b>zapisanego</b> przyjęcia (PW) towaru —
/// Demo blokuje stan ujemny (<c>StanUjemnyVerifier</c>).</item>
/// </list>
/// </para>
/// Testy są napisane z perspektywy programisty zewnętrznego (tylko publiczny kontrakt).
/// Tam, gdzie definicja relacji w bazie Demo wymaga rozstrzygnięcia, którego nie da się dostarczyć
/// czystym publicznym API (callback wybierający dostawy/magazyn), test rozpoznaje
/// <see cref="NotImplementedException"/> i jest pomijany (<c>Assert.Ignore</c>) z czytelnym powodem —
/// nie jest to błąd kodu testu, lecz ograniczenie konfiguracji/kontraktu.
/// </summary>
[TestFixture]
public class Rozdzial04_RelacjeTest : DokumentHandlowyTestBase
{
    // === Pomocnicze ===

    /// <summary>Serwis relacji bieżącej sesji (rzuca, gdy serwisu brak).</summary>
    private IRelacjeService Relacje => Session.GetRequiredService<IRelacjeService>();

    /// <summary>
    /// Zmienia stan dokumentu na zatwierdzony (w transakcji edycyjnej).
    /// Nadrzędne muszą być zatwierdzone, aby relacja podrzędna mogła powstać.
    /// </summary>
    private void Zatwierdz(DokumentHandlowy dok)
    {
        InTransaction(() => dok.Stan = StanDokumentuHandlowego.Zatwierdzony);
    }

    // === HANDEL-W17 — ZO → FV (NowyPodrzednyIndywidualny) ===

    [Test]
    [Description("HANDEL-W17: z zatwierdzonego zamówienia odbiorcy (ZO) generuje pojedynczą fakturę (FV) " +
                 "przez IRelacjeService.NowyPodrzednyIndywidualny; sprawdza, że powstał dokument z pozycjami.")]
    public void NowyPodrzednyIndywidualny_ZoNaFv_TworzyFaktureZPozycjami()
    {
        // Zamówienie odbiorcy nie rozchoduje magazynu w buforze, ale dla bezpieczeństwa
        // wprowadzamy towar na stan — faktura generowana z ZO może już dotykać magazynu.
        PrzyjmijNaStan(Towar_.Bikini, 100);

        // 1) Utwórz zamówienie odbiorcy z jedną pozycją, zatwierdź je i ZAPISZ trwale.
        //    Nadrzędny musi być zatwierdzony; relację wołamy na świeżej sesji (re-get po Guid).
        var zo = UtworzDokument(Definicje.ZamowienieOdbiorcy,
            kontrahent: Kontrahent(Kontrahent_.Abc),
            magazyn: Magazyn(Magazyn_.Firma));
        InTransaction(() => DodajPozycje(zo, Towar(Towar_.Bikini), 5, cena: 20));
        Zatwierdz(zo);
        var zoGuid = zo.Guid;
        SaveDispose();

        // 2) Re-get zamówienia na świeżej sesji i wygeneruj fakturę — operacja w transakcji edycyjnej.
        var zoZap = Get<DokumentHandlowy>(zoGuid);
        DokumentHandlowy[] faktury = null;
        InTransaction(() =>
            faktury = Relacje.NowyPodrzednyIndywidualny(new[] { zoZap }, Definicje.FakturaSprzedazy));
        var fvGuid = faktury[0].Guid;
        SaveDispose();

        // 3) Asercje: jeden nadrzędny → jeden podrzędny, faktura istnieje i ma pozycje.
        //    Powiązania/pozycje czytamy po SaveDispose przez re-get po Guid.
        faktury.Should().NotBeNull();
        faktury.Should().HaveCount(1); // Length == nadrzedne.Length (relacja indywidualna)

        var faktura = Get<DokumentHandlowy>(fvGuid);
        faktura.Should().NotBeNull();
        faktura.Definicja.Symbol.Should().Be(Definicje.FakturaSprzedazy);
        faktura.Pozycje.Count.Should().BeGreaterThan(0); // pozycje przepisane z zamówienia
    }

    // === HANDEL-W21 — FV → WZ pojedynczo (NowyPodrzednyIndywidualny) ===

    [Test]
    [Description("HANDEL-W21: do zatwierdzonej faktury sprzedaży (FV) generuje pojedynczy dokument magazynowy (WZ) " +
                 "przez NowyPodrzednyIndywidualny; sprawdza powstanie dokumentu magazynowego.")]
    public void NowyPodrzednyIndywidualny_FvNaWz_TworzyWydanieMagazynowe()
    {
        // Zatwierdzona FV (nadrzędny) — helper zapisuje ją z pozycją przed zatwierdzeniem,
        // dzięki czemu KrajPodatkuVat jest przeliczony i ewidencja VAT powstaje bez NRE.
        var fvGuid = UtworzZatwierdzonaFakture();

        var fv = Get<DokumentHandlowy>(fvGuid);
        DokumentHandlowy[] wz = null;
        InTransaction(() =>
            wz = Relacje.NowyPodrzednyIndywidualny(new[] { fv }, Definicje.WydanieZewnetrzne));
        var wzGuid = wz[0].Guid;
        SaveDispose();

        // Receptura HANDEL-W21: „sprawdza POWSTANIE dokumentu magazynowego”. Relacja indywidualna tworzy
        // jeden podrzędny WZ powiązany z fakturą. (Transfer ilości/pozycji rozchodu zależy od wyboru
        // dostaw — HandlerSet.WybierzDostawyCallback / DostawaWorker — i jest poza zakresem tego smoke-testu.)
        wz.Should().HaveCount(1, "relacja indywidualna: jeden nadrzędny → jeden podrzędny");
        var wzDok = Get<DokumentHandlowy>(wzGuid);
        wzDok.Should().NotBeNull();
        wzDok.Definicja.Symbol.Should().Be(Definicje.WydanieZewnetrzne, "powstał dokument magazynowy WZ");

        // Powiązanie zwrotne: faktura wskazuje wygenerowany dokument magazynowy.
        var fvOdczyt = Get<DokumentHandlowy>(fvGuid);
        fvOdczyt.DokumentyMagazynowe.Should().Contain(d => d.Guid == wzGuid,
            "wygenerowany WZ jest powiązany z fakturą nadrzędną");
    }

    // === HANDEL-W18 — wiele FV → 1 WZ zbiorcze (NowyPodrzednyZbiorczy) ===

    [Test]
    [Description("HANDEL-W18: z dwóch zatwierdzonych faktur (tego samego kontrahenta) tworzy JEDEN zbiorczy " +
                 "dokument magazynowy (WZ) przez NowyPodrzednyZbiorczy; wynik to agregat (zwykle 1 dokument).")]
    [Ignore("HANDEL-W18 — zbiorcza relacja FV→WZ (wiele faktur → jeden dokument magazynowy) NIE jest " +
            "zarejestrowaną akcją przekształcenia dla faktury sprzedaży w bazie Demo: " +
            "DokumentHandlowy.ResolveActions() nie zawiera tej operacji, więc IRelacjeService.NowyPodrzednyZbiorczy " +
            "rzuca InvalidOperationException('Operacja tworzenia relacji nie jest dostępna'). To ograniczenie " +
            "konfiguracji relacji (nie problem zatwierdzenia FV — ten jest rozwiązany przez UtworzZatwierdzonaFakture). " +
            "Wariant zbiorczy pokrywa np. ZO→FV; FV→WZ działa indywidualnie (HANDEL-W21).")]
    public void NowyPodrzednyZbiorczy_WieleFvNaJednoWz_TworzyDokumentZbiorczy()
    {
        // Pozostawione jako wykonywalna dokumentacja intencji — relacja niedostępna w Demo (patrz [Ignore]).
        var fv1 = UtworzZatwierdzonaFakture();
        var fv2 = UtworzZatwierdzonaFakture();

        var d1 = Get<DokumentHandlowy>(fv1);
        var d2 = Get<DokumentHandlowy>(fv2);
        DokumentHandlowy[] wz = null;
        InTransaction(() =>
            wz = Relacje.NowyPodrzednyZbiorczy(new[] { d1, d2 }, Definicje.WydanieZewnetrzne));
        SaveDispose();

        wz.Should().NotBeNull();
        wz.Should().HaveCount(1, "zbiorczy: dwie faktury → jeden wspólny dokument magazynowy");
        Get<DokumentHandlowy>(wz[0].Guid).Definicja.Symbol.Should().Be(Definicje.WydanieZewnetrzne);
    }

    // === HANDEL-W20 — odczyt powiązań: faktura.DokumentyMagazynowe ===

    [Test]
    [Description("HANDEL-W20: po wygenerowaniu WZ z faktury odczytuje powiązanie zwrotne przez pole kalkulowane " +
                 "faktura.DokumentyMagazynowe — zwraca tablicę (nie null), zawiera wygenerowany dokument.")]
    public void DokumentyMagazynowe_PoWygenerowaniuWz_ZwracaPowiazanyDokument()
    {
        // 1) Zatwierdzona faktura sprzedaży (FV) — dokument nadrzędny relacji FV→WZ.
        //    Helper zapisuje FV z pozycją PRZED zatwierdzeniem, więc KrajPodatkuVat jest przeliczony
        //    i ewidencja VAT powstaje bez NRE (patrz UtworzZatwierdzonaFakture).
        var fvGuid = UtworzZatwierdzonaFakture();

        // 2) Wygeneruj dokument magazynowy (WZ) z faktury — relacja podrzędna w transakcji edycyjnej.
        var fv = Get<DokumentHandlowy>(fvGuid);
        DokumentHandlowy[] wz = null;
        InTransaction(() =>
            wz = Relacje.NowyPodrzednyIndywidualny(new[] { fv }, Definicje.WydanieZewnetrzne));
        var wzGuid = wz[0].Guid;
        SaveDispose();

        // 3) Powiązanie zwrotne czytamy po SaveDispose przez re-get: pole kalkulowane DokumentyMagazynowe.
        var fvOdczyt = Get<DokumentHandlowy>(fvGuid);
        fvOdczyt.DokumentyMagazynowe.Should().NotBeNull("pole kalkulowane zawsze zwraca tablicę (nie null)");
        fvOdczyt.DokumentyMagazynowe.Should().Contain(d => d.Guid == wzGuid,
            "po wygenerowaniu WZ faktura wskazuje go w DokumentyMagazynowe");
    }

    // === HANDEL-W20 — odczyt powiązań: dok.DokumentyHandlowe dla samego dokumentu handlowego ===

    [Test]
    [Description("HANDEL-W20: pola kalkulowane DokumentyMagazynowe/DokumentyHandlowe zawsze zwracają tablicę " +
                 "(nigdy null) — bezpieczne do iterowania także dla dokumentu bez powiązań.")]
    public void PolaPowiazan_BezRelacji_ZwracajaPustaTabliceNieNull()
    {
        // Świeże, samodzielne zamówienie bez żadnych relacji.
        var zo = UtworzDokument(Definicje.ZamowienieOdbiorcy,
            kontrahent: Kontrahent(Kontrahent_.Abc),
            magazyn: Magazyn(Magazyn_.Firma));
        InTransaction(() => DodajPozycje(zo, Towar(Towar_.Bikini), 1, cena: 20));
        Zatwierdz(zo);
        Session.Save();

        // Oba pola są kalkulowane i read-only; zwracają tablicę (możliwie pustą), nigdy null.
        zo.DokumentyMagazynowe.Should().NotBeNull();
        zo.DokumentyHandlowe.Should().NotBeNull();
    }

    // === HANDEL-W24 — łańcuch relacji w dół: zamówienie -> faktury -> magazynowe ===

    [Test]
    [Description("HANDEL-W24: po wygenerowaniu FV z ZO odczytuje łańcuch relacji w dół przez pola kalkulowane " +
                 "(zo.DokumentyHandlowe). Łańcuch respektuje istniejące powiązania; gdy relacji brak — Ignore.")]
    public void LancuchRelacji_ZoNaFv_OdczytPrzezPolaKalkulowane()
    {
        PrzyjmijNaStan(Towar_.Bikini, 100);

        // 1) Zatwierdzone, zapisane zamówienie odbiorcy.
        var zo = UtworzDokument(Definicje.ZamowienieOdbiorcy,
            kontrahent: Kontrahent(Kontrahent_.Abc),
            magazyn: Magazyn(Magazyn_.Firma));
        InTransaction(() => DodajPozycje(zo, Towar(Towar_.Bikini), 5, cena: 20));
        Zatwierdz(zo);
        var zoGuid = zo.Guid;
        SaveDispose();

        // 2) Re-get i wygeneruj fakturę z zamówienia na świeżej sesji.
        var zoZap = Get<DokumentHandlowy>(zoGuid);
        DokumentHandlowy[] faktury = null;
        InTransaction(() =>
            faktury = Relacje.NowyPodrzednyIndywidualny(new[] { zoZap }, Definicje.FakturaSprzedazy));
        var fvGuid = faktury[0].Guid;
        SaveDispose();

        // 3) Łańcuch w dół czytamy DOPIERO po SaveDispose + Get (inaczej AccessWriteDenied):
        //    zamówienie -> jego faktury (pole kalkulowane DokumentyHandlowe).
        var zoOdczyt = Get<DokumentHandlowy>(zoGuid);
        var fakturyZamowienia = zoOdczyt.DokumentyHandlowe;
        fakturyZamowienia.Should().NotBeNull();
        // faktura widoczna w łańcuchu relacji zamówienia (porównanie po Guid — różne sesje).
        fakturyZamowienia.Select(d => d.Guid).Should().Contain(fvGuid);
    }
}
