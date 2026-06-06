using AwesomeAssertions;
using NUnit.Framework;
using Soneta.Handel;

namespace Soneta.Skills.Test.Handel.DokumentyHandlowe;

/// <summary>
/// Test dymny (smoke) weryfikujący, że infrastruktura testowa dokumentu handlowego działa:
/// pobranie modułów i danych Demo, utworzenie dokumentu z pozycją oraz trwały zapis i ponowny odczyt.
/// </summary>
[TestFixture]
public class SmokeTest : DokumentHandlowyTestBase
{
    [Test]
    [Description("Tworzy przyjęcie wewnętrzne (PW) z jedną pozycją i potwierdza trwały zapis.")]
    public void TworzyDokumentZPozycja_ZapisujeTrwale()
    {
        var dok = UtworzDokument(Definicje.PrzyjecieWewnetrzne, magazyn: Magazyn(Magazyn_.Firma));
        InTransaction(() => DodajPozycje(dok, Towar(Towar_.Bikini), ilosc: 10, cena: 5));
        var guid = dok.Guid;
        SaveDispose();

        var zapisany = Get<DokumentHandlowy>(guid);
        zapisany.Should().NotBeNull();
        zapisany.Pozycje.Count.Should().Be(1);
    }
}
