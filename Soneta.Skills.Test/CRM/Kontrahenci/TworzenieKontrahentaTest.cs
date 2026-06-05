using AwesomeAssertions;
using NUnit.Framework;
using Soneta.Core;
using Soneta.CRM;

namespace Soneta.Skills.Test.CRM.Kontrahenci;

/// <summary>
/// W3 — Tworzenie kontrahenta.
/// Testy pokazują utworzenie rekordu z minimalnym kompletem danych w transakcji edycyjnej
/// oraz trwały zapis (SaveDispose) i ponowny odczyt z nowej sesji. Pokrywają warianty:
/// podmiot gospodarczy krajowy, podmiot unijny oraz osoba fizyczna (finalny).
/// </summary>
[TestFixture]
public class TworzenieKontrahentaTest : KontrahentTestBase
{
    [Test]
    [Description("Tworzy krajowy podmiot gospodarczy z NIP i zapisuje go trwale w bazie.")]
    public void TworzeniePodmiotuKrajowego_ZapisujeRekord()
    {
        var kod = UnikalnyKod();

        var k = UtworzKontrahenta(kod, "Krajowa Firma Sp. z o.o.");
        InTransaction(() =>
        {
            k.PodatnikVAT = true;
            k.NIP = "1234563218"; // ustawienie NIP synchronizuje EuVAT
        });
        SaveDispose();

        // Ponowny odczyt z nowej sesji potwierdza trwały zapis.
        var zapisany = Crm.Kontrahenci.WgKodu[kod];
        zapisany.Should().NotBeNull();
        zapisany.Nazwa.Should().Be("Krajowa Firma Sp. z o.o.");
        zapisany.StatusPodmiotu.Should().Be(StatusPodmiotu.PodmiotGospodarczy);
        zapisany.RodzajPodmiotu.Should().Be(RodzajPodmiotu.Krajowy);
        zapisany.PodatnikVAT.Should().BeTrue();
    }

    [Test]
    [Description("Tworzy podmiot unijny (RodzajPodmiotu.Unijny).")]
    public void TworzeniePodmiotuUnijnego_UstawiaRodzajUnijny()
    {
        var kod = UnikalnyKod();

        UtworzKontrahenta(kod, "EU Trading GmbH",
            status: StatusPodmiotu.PodmiotGospodarczy,
            rodzaj: RodzajPodmiotu.Unijny);
        SaveDispose();

        var zapisany = Crm.Kontrahenci.WgKodu[kod];
        zapisany.Should().NotBeNull();
        zapisany.RodzajPodmiotu.Should().Be(RodzajPodmiotu.Unijny);
    }

    [Test]
    [Description("Tworzy osobę fizyczną (StatusPodmiotu.Finalny).")]
    public void TworzenieOsobyFizycznej_UstawiaStatusFinalny()
    {
        var kod = UnikalnyKod();

        UtworzKontrahenta(kod, "Jan Kowalski",
            status: StatusPodmiotu.Finalny,
            rodzaj: RodzajPodmiotu.Krajowy);
        SaveDispose();

        var zapisany = Crm.Kontrahenci.WgKodu[kod];
        zapisany.Should().NotBeNull();
        zapisany.StatusPodmiotu.Should().Be(StatusPodmiotu.Finalny);
    }
}