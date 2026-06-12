using AwesomeAssertions;
using NUnit.Framework;

namespace Soneta.Skills.Test.CRM.Kontrahenci;

/// <summary>
/// CRM-W5 — Bezpieczne usuwanie kontrahenta.
/// Test pokazuje czyste usunięcie świeżo utworzonego rekordu (brak powiązań) oraz alternatywę
/// „miękkiego" wycofania (<c>Blokada=true</c>), zalecaną gdy istnieją dokumenty/rozrachunki.
/// </summary>
[TestFixture]
public class UsuwanieKontrahentaTest : KontrahentTestBase
{
    [Test]
    [Description("Usunięcie kontrahenta bez powiązań (DeleteRow) usuwa rekord z bazy.")]
    public void CzysteUsuniecie_UsuwaRekord()
    {
        var kod = UnikalnyKod();
        UtworzKontrahenta(kod, "Do Usuniecia");
        SaveDispose();

        var k = Crm.Kontrahenci.WgKodu[kod];
        k.Should().NotBeNull();

        InTransaction(() => k.Delete());
        SaveDispose();

        // Po usunięciu indeksator zwraca null.
        Crm.Kontrahenci.WgKodu[kod].Should().BeNull();
    }

    [Test]
    [Description("Miękkie wycofanie: zamiast usuwać, ustawiamy Blokada=true (rekord pozostaje).")]
    public void MiekkieWycofanie_UstawiaBlokade()
    {
        var kod = UnikalnyKod();
        UtworzKontrahenta(kod, "Do Wycofania");
        SaveDispose();

        var k = Crm.Kontrahenci.WgKodu[kod];
        InTransaction(() => k.Blokada = true);
        SaveDispose();

        var zapisany = Crm.Kontrahenci.WgKodu[kod];
        zapisany.Should().NotBeNull();
        zapisany.Blokada.Should().BeTrue();
    }
}
