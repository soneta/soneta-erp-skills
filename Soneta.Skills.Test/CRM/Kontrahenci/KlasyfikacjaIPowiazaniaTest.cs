using System.Linq;
using AwesomeAssertions;
using NUnit.Framework;
using Soneta.CRM;

namespace Soneta.Skills.Test.CRM.Kontrahenci;

/// <summary>
/// W13/W14 — Klasyfikacja i powiązania (odczyt kontraktu publicznego).
/// Testy dokumentują dostęp do kolekcji klasyfikacyjnych (<c>Kategorie</c>, <c>Branze</c>,
/// <c>Features</c>) oraz powiązań (<c>Opiekunowie</c>, <c>Podrzedni</c>, <c>PodmiotNadrzedny</c>).
/// Świeżo utworzony, samodzielny kontrahent ma te kolekcje puste i brak podmiotu nadrzędnego —
/// co czyni asercje deterministycznymi.
/// </summary>
[TestFixture]
public class KlasyfikacjaIPowiazaniaTest : KontrahentTestBase
{
    [Test]
    [Description("Świeży kontrahent ma dostępne i puste kolekcje klasyfikacyjne; Features != null.")]
    public void NowyKontrahent_KolekcjeKlasyfikacjiSaPusteAleDostepne()
    {
        var kod = UnikalnyKod();
        UtworzKontrahenta(kod, "Firma Klasyfikacja");
        SaveDispose();

        var k = Crm.Kontrahenci.WgKodu[kod];

        k.Features.Should().NotBeNull();          // cechy definiowalne — dostęp po nazwie
        k.Kategorie.Cast<KategoriaKth>().Should().BeEmpty();
        k.Branze.Cast<BranzaKth>().Should().BeEmpty();
    }

    [Test]
    [Description("Świeży kontrahent nie ma opiekunów, podmiotów podrzędnych ani nadrzędnego.")]
    public void NowyKontrahent_BrakPowiazan()
    {
        var kod = UnikalnyKod();
        UtworzKontrahenta(kod, "Firma Powiazania");
        SaveDispose();

        var k = Crm.Kontrahenci.WgKodu[kod];

        k.Opiekunowie.Cast<Opiekun>().Should().BeEmpty();
        k.Podrzedni.Cast<RelacjaPodmiotu>().Should().BeEmpty();
        k.PodmiotNadrzedny.Should().BeNull();
    }
}
