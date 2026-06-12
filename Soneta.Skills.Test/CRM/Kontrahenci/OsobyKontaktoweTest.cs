using System.Linq;
using AwesomeAssertions;
using NUnit.Framework;
using Soneta.CRM;

namespace Soneta.Skills.Test.CRM.Kontrahenci;

/// <summary>
/// CRM-W8 — Osoby kontaktowe.
/// Test pokazuje dodanie osoby kontaktowej i powiązanie jej z kontrahentem przez
/// <c>KontaktOsoba.Kontrahent</c> — osoba pojawia się wtedy w kolekcji <c>Osoby</c> kontrahenta.
/// </summary>
[TestFixture]
public class OsobyKontaktoweTest : KontrahentTestBase
{
    [Test]
    [Description("Dodana i powiązana osoba kontaktowa pojawia się w kolekcji Osoby kontrahenta.")]
    public void DodanieOsoby_PojawiaSieWKolekcjiOsoby()
    {
        var kod = UnikalnyKod();
        UtworzKontrahenta(kod, "Firma Z Osoba");
        SaveDispose();

        var email = "a.nowak@firma-" + kod + ".pl";
        var k = Crm.Kontrahenci.WgKodu[kod];

        InTransaction(() =>
        {
            var os = new KontaktOsoba();
            Session.AddRow(os);
            os.Kontrahent = k; // powiązanie osoby z kontrahentem
            os.Imie = "Anna";
            os.Nazwisko = "Nowak";
            os.Stanowisko = "Kierownik zakupów";
            os.EMAIL = email;
        });
        SaveDispose();

        var zapisany = Crm.Kontrahenci.WgKodu[kod];
        zapisany.Osoby.Cast<KontaktOsoba>()
            .Any(o => o.Nazwisko == "Nowak" && o.Imie == "Anna")
            .Should().BeTrue();
    }
}