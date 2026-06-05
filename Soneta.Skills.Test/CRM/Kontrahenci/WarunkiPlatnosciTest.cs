using AwesomeAssertions;
using NUnit.Framework;
using Soneta.CRM;
using Soneta.Kasa;

namespace Soneta.Skills.Test.CRM.Kontrahenci;

/// <summary>
/// W9 — Warunki płatności i limity kredytowe.
/// Testy pokazują ustawienie sposobu zapłaty (rekord <c>FormaPlatnosci</c> z modułu Kasa),
/// terminu płatności oraz typu limitu kredytowego. Pola kalkulowane (np.
/// <c>LimitNieograniczony</c>) są tylko do odczytu i wynikają z ustawień.
/// </summary>
[TestFixture]
public class WarunkiPlatnosciTest : KontrahentTestBase
{
    [Test]
    [Description("Ustawienie sposobu zapłaty (Przelew) i terminu płatności jest zapisywane.")]
    public void WarunkiPlatnosci_SposobIZaplatyTermin_SaZapisywane()
    {
        var kod = UnikalnyKod();
        UtworzKontrahenta(kod, "Firma Z Platnosciami");
        SaveDispose();

        var k = Crm.Kontrahenci.WgKodu[kod];
        var przelew = Session.GetKasa().FormyPlatnosci[FormaPlatnosci.Przelew];

        InTransaction(() =>
        {
            k.SposobZaplaty = przelew;
            k.Termin = 14; // dni
        });
        SaveDispose();

        var zapisany = Crm.Kontrahenci.WgKodu[kod];
        zapisany.SposobZaplaty.Should().NotBeNull();
        zapisany.Termin.Should().Be(14);
    }

    [Test]
    [Description("Typ limitu kredytowego = Nieograniczony skutkuje kalkulowanym LimitNieograniczony=true.")]
    public void LimitKredytowy_Nieograniczony_UstawiaFlageKalkulowana()
    {
        var kod = UnikalnyKod();
        UtworzKontrahenta(kod, "Firma Bez Limitu");
        SaveDispose();

        var k = Crm.Kontrahenci.WgKodu[kod];
        InTransaction(() => k.TypLimituKredytowego = TypLimituKredytowego.Nieograniczony);
        SaveDispose();

        var zapisany = Crm.Kontrahenci.WgKodu[kod];
        zapisany.TypLimituKredytowego.Should().Be(TypLimituKredytowego.Nieograniczony);
        zapisany.LimitNieograniczony.Should().BeTrue(); // pole kalkulowane (read-only)
    }
}