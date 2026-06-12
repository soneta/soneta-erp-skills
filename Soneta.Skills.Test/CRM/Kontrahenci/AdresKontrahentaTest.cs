using AwesomeAssertions;
using NUnit.Framework;

namespace Soneta.Skills.Test.CRM.Kontrahenci;

/// <summary>
/// CRM-W6 — Adres kontrahenta.
/// Test pokazuje, że <c>Adres</c> to property zwracająca obiekt złożony (nie da się przypisać
/// całego adresu) — modyfikujemy jego pola. Uwaga na typ <c>KodPocztowy</c> = <c>int</c>
/// (do formatu „00-000" służy <c>KodPocztowyS</c>).
/// </summary>
[TestFixture]
public class AdresKontrahentaTest : KontrahentTestBase
{
    [Test]
    [Description("Ustawienie pól adresu głównego (ulica, kod pocztowy, miejscowość) jest zapisywane.")]
    public void UstawienieAdresuGlownego_JestZapisywane()
    {
        var kod = UnikalnyKod();
        UtworzKontrahenta(kod, "Firma Z Adresem");
        SaveDispose();

        var k = Crm.Kontrahenci.WgKodu[kod];
        InTransaction(() =>
        {
            var a = k.Adres; // edytujemy pola obiektu adresu
            a.Ulica = "Wadowicka";
            a.NrDomu = "8A";
            a.NrLokalu = "2";
            a.KodPocztowyS = "30-415"; // string z myślnikiem; pole int KodPocztowy = 30415
            a.Miejscowosc = "Kraków";
            a.Poczta = "Kraków";
            a.Kraj = "Polska";
        });
        SaveDispose();

        var a2 = Crm.Kontrahenci.WgKodu[kod].Adres;
        a2.Ulica.Should().Be("Wadowicka");
        a2.NrDomu.Should().Be("8A");
        a2.Miejscowosc.Should().Be("Kraków");
        a2.KodPocztowy.Should().Be(30415);
    }

    [Test]
    [Description("Adres do korespondencji jest odrębnym obiektem od adresu głównego.")]
    public void AdresDoKorespondencji_JestOdrebnyOdGlownego()
    {
        var kod = UnikalnyKod();
        UtworzKontrahenta(kod, "Firma Z Korespondencja");
        SaveDispose();

        var k = Crm.Kontrahenci.WgKodu[kod];
        InTransaction(() =>
        {
            k.Adres.Miejscowosc = "Kraków";
            k.AdresDoKorespondencji.Miejscowosc = "Warszawa";
        });
        SaveDispose();

        var zapisany = Crm.Kontrahenci.WgKodu[kod];
        zapisany.Adres.Miejscowosc.Should().Be("Kraków");
        zapisany.AdresDoKorespondencji.Miejscowosc.Should().Be("Warszawa");
    }
}
