using AwesomeAssertions;
using NUnit.Framework;
using Soneta.Core;

namespace Soneta.Skills.Test.CRM.Kontrahenci;

/// <summary>
/// W2 — Walidacja NIP / REGON / EU VAT przed zapisem.
/// Testy weryfikują publiczne, statyczne walidatory z <c>Soneta.Core</c>
/// (<see cref="Nip"/>, <see cref="Regon"/>, <see cref="EuVat"/>) oraz normalizację numerów.
/// Walidatory sprawdzają format i sumę kontrolną — to NIE jest weryfikacja w MF/VIES (patrz W15).
/// </summary>
[TestFixture]
public class WalidacjaNipRegonTest : KontrahentTestBase
{
    [Test]
    [Description("Nip.Test akceptuje poprawny NIP (10 cyfr i format z myślnikami), odrzuca błędny.")]
    public void NipTest_RozrozniaPoprawnyIBledny()
    {
        // 1234563218 ma poprawną sumę kontrolną.
        Nip.Test("1234563218").Should().BeTrue();
        Nip.Test("123-456-32-18").Should().BeTrue();

        // Zmiana ostatniej cyfry psuje sumę kontrolną.
        Nip.Test("1234563219").Should().BeFalse();
        Nip.Test("123").Should().BeFalse();

        // Normalizacja: Flat usuwa myślniki, Format dodaje.
        Nip.Flat("123-456-32-18").Should().Be("1234563218");
        Nip.Format("1234563218").Should().Be("123-456-32-18");
    }

    [Test]
    [Description("Regon.Test akceptuje poprawny REGON 9-znakowy, odrzuca błędny i o złej długości.")]
    public void RegonTest_RozrozniaPoprawnyIBledny()
    {
        // 123456785 ma poprawną sumę kontrolną dla 9-znakowego REGON.
        Regon.Test("123456785").Should().BeTrue();
        Regon.Test("123456784").Should().BeFalse();
        Regon.Test("12345").Should().BeFalse();
    }

    [Test]
    [Description("EuVat.Test akceptuje krajowy numer z prefiksem PL nad poprawnym NIP, odrzuca błędny.")]
    public void EuVatTest_PrefiksPL_DzialaNaPoprawnymNip()
    {
        // EuVat.Test wymaga ISessionable (sprawdza listę krajów UE w bazie).
        EuVat.Test("PL1234563218", Session).Should().BeTrue();
        EuVat.Test("PL1234563219", Session).Should().BeFalse();

        // Rozbicie numeru na kod kraju + identyfikator.
        EuVat.Split("PL1234563218", out var kraj, out var numer);
        kraj.Should().Be("PL");
        numer.Should().Be("1234563218");
    }
}