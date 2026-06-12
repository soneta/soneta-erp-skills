using System.Linq;
using AwesomeAssertions;
using NUnit.Framework;
using Soneta.Core;
using Soneta.CRM;

namespace Soneta.Skills.Test.CRM.Kontrahenci;

/// <summary>
/// CRM-W1 — Wyszukiwanie i identyfikacja kontrahenta.
/// Testy pokazują trzy podstawowe sposoby odnajdywania kontrahenta używane w kodzie dodatków:
/// po kodzie (klucz unikalny), po nazwie (klucz nieunikalny) oraz po NIP (filtr serwerowy
/// <c>SubTable[condition]</c>, zamiast iteracji całej tabeli w pamięci).
/// </summary>
[TestFixture]
public class WyszukiwanieKontrahentaTest : KontrahentTestBase
{
    [Test]
    [Description("Wyszukanie po kodzie (indeks WgKodu) zwraca dokładnie utworzony rekord.")]
    public void WyszukiwaniePoKodzie_ZwracaUtworzonyRekord()
    {
        var kod = UnikalnyKod();
        UtworzKontrahenta(kod, "Firma Po Kodzie");
        SaveDispose();

        // WgKodu to klucz unikalny — indeksator zwraca pojedynczy rekord lub null.
        var znaleziony = Crm.Kontrahenci.WgKodu[kod];

        znaleziony.Should().NotBeNull();
        znaleziony.Nazwa.Should().Be("Firma Po Kodzie");
    }

    [Test]
    [Description("Wyszukanie po nazwie (indeks WgNazwy, nieunikalny) zwraca zbiór z rekordem.")]
    public void WyszukiwaniePoNazwie_ZwracaRekordWZbiorze()
    {
        var kod = UnikalnyKod();
        var nazwa = "Wyszukiwarka " + kod;
        UtworzKontrahenta(kod, nazwa);
        SaveDispose();

        // WgNazwy jest kluczem nieunikalnym — zwraca zbiór, z którego bierzemy pierwszy.
        var znaleziony = Crm.Kontrahenci.WgNazwy[nazwa].FirstOrDefault();

        znaleziony.Should().NotBeNull();
        znaleziony.Kod.Should().Be(kod);
    }

    [Test]
    [Description("Wyszukanie po NIP filtrem serwerowym SubTable[condition] zwraca rekord; " +
                 "dedup wykrywa istniejący podmiot.")]
    public void WyszukiwaniePoNip_FiltrSerwerowy_ZnajdujeISygnalizujeDuplikat()
    {
        var kod = UnikalnyKod();
        var nip = "1234563218"; // poprawny NIP (suma kontrolna)
        var k = UtworzKontrahenta(kod, "Firma Z NIP");
        InTransaction(() => k.NIP = nip);
        SaveDispose();

        // Filtr po stronie serwera (klauzula WHERE w SQL), nie iteracja w pamięci.
        // Warunek aplikujemy na indeksie tabeli (WgNIP); porównania tekstowe są case-insensitive.
        var znaleziony = Crm.Kontrahenci.WgNIP[(Kontrahent x) => x.NIP == nip].FirstOrDefault();
        znaleziony.Should().NotBeNull();
        znaleziony.Kod.Should().Be(kod);

        // Typowy dedup przed dodaniem nowego kontrahenta:
        bool juzIstnieje = Crm.Kontrahenci.WgNIP[(Kontrahent x) => x.NIP == nip].Any();
        juzIstnieje.Should().BeTrue();
    }
}