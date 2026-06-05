using System.Linq;
using AwesomeAssertions;
using NUnit.Framework;
using Soneta.Core;

namespace Soneta.Skills.Test.CRM.Kontrahenci;

/// <summary>
/// W7 — Dane kontaktowe i adresy WWW.
/// Testy pokazują dodanie kanału e-mail do kolekcji <c>Kontakty</c> (typ rodzaju pobierany ze
/// słownika <c>RodzajeKontaktow</c>) oraz dodanie adresu WWW (konstruktor z hostem
/// <c>new AdresWWW(kontrahent)</c>, pole URL nazywa się <c>Adres</c>).
/// </summary>
[TestFixture]
public class DaneKontaktoweTest : KontrahentTestBase
{
    [Test]
    [Description("Dodanie domyślnego kontaktu e-mail pojawia się w kolekcji Kontakty kontrahenta.")]
    public void DodanieEmaila_PojawiaSieWKolekcjiKontakty()
    {
        var kod = UnikalnyKod();
        UtworzKontrahenta(kod, "Firma Z Mailem");
        SaveDispose();

        var email = "kontakt@firma-" + kod + ".pl";
        var k = Crm.Kontrahenci.WgKodu[kod];
        var rodzajEmail = Session.GetCore().RodzajeKontaktow[RodzajeKontaktow.AdresEmail];

        InUITransaction(() =>
        {
            var dk = Add(new DaneKontaktowe { Host = k });
            dk.Rodzaj = rodzajEmail;
            dk.Kontakt = email;
            dk.Domyslny = true;
        });
        SaveDispose();

        var zapisany = Crm.Kontrahenci.WgKodu[kod];
        zapisany.Kontakty.Cast<DaneKontaktowe>()
            .Any(d => d.Kontakt == email)
            .Should().BeTrue();
    }

    [Test]
    [Description("Dodanie adresu WWW (new AdresWWW(host)) pojawia się w kolekcji AdresyWWW.")]
    public void DodanieAdresuWWW_PojawiaSieWKolekcji()
    {
        var kod = UnikalnyKod();
        UtworzKontrahenta(kod, "Firma Z WWW");
        SaveDispose();

        var url = "https://www.firma-" + kod + ".pl";
        var k = Crm.Kontrahenci.WgKodu[kod];

        InUITransaction(() =>
        {
            var www = Add(new AdresWWW(k)); // ctor przyjmuje IAdresyWWWHost
            www.Adres = url;                // pole URL nazywa się Adres
            www.Domyslny = true;
        });
        SaveDispose();

        var zapisany = Crm.Kontrahenci.WgKodu[kod];
        zapisany.AdresyWWW.Cast<AdresWWW>()
            .Any(w => w.Adres == url)
            .Should().BeTrue();
    }
}