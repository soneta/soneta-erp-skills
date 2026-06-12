using AwesomeAssertions;
using NUnit.Framework;
using Soneta.CRM;

namespace Soneta.Skills.Test.CRM.Kontrahenci;

/// <summary>
/// CRM-W4 — Modyfikacja danych i statusów kontrahenta.
/// Testy pokazują zmianę nazwy oraz ustawienie statusów dostępności/handlowych:
/// <c>Blokada</c> (ukrycie na listach) i <c>BlokadaSprzedazy</c> (zakaz dokumentów rozchodu).
/// </summary>
[TestFixture]
public class ModyfikacjaIStatusyTest : KontrahentTestBase
{
    [Test]
    [Description("Zmiana nazwy kontrahenta jest trwale zapisywana.")]
    public void ZmianaNazwy_JestZapisywana()
    {
        var kod = UnikalnyKod();
        UtworzKontrahenta(kod, "Nazwa Pierwotna");
        SaveDispose();

        var k = Crm.Kontrahenci.WgKodu[kod];
        InTransaction(() => k.Nazwa = "Nazwa Zmieniona");
        SaveDispose();

        Crm.Kontrahenci.WgKodu[kod].Nazwa.Should().Be("Nazwa Zmieniona");
    }

    [Test]
    [Description("Ustawienie Blokada i BlokadaSprzedazy jest trwale zapisywane.")]
    public void UstawienieBlokad_JestZapisywane()
    {
        var kod = UnikalnyKod();
        UtworzKontrahenta(kod, "Do Zablokowania");
        SaveDispose();

        var k = Crm.Kontrahenci.WgKodu[kod];
        InTransaction(() =>
        {
            k.Blokada = true;          // ukrycie na listach
            k.BlokadaSprzedazy = true; // zakaz wystawiania dokumentów rozchodu
        });
        SaveDispose();

        var zapisany = Crm.Kontrahenci.WgKodu[kod];
        zapisany.Blokada.Should().BeTrue();
        zapisany.BlokadaSprzedazy.Should().BeTrue();
    }
}
