using System.Linq;
using AwesomeAssertions;
using NUnit.Framework;
using Soneta.Business;
using Soneta.Test;
using Prac = Soneta.Kadry.Pracownik;

namespace Soneta.Skills.Test.KadryPlace.Pracownik;

/// <summary>
/// Test dymny (smoke) potwierdzający, że infrastruktura testowa domeny Kadry/Płace działa:
/// sesja operacyjna jest powiązana z bazą Demo, moduły są dostępne, a kartoteka pracowników
/// jest niepusta. To minimalny punkt wejścia, na którym opierają się pozostałe rozdziały.
/// </summary>
[TestFixture]
public class SmokeTest : PracownikTestBase
{
    [Test]
    [Description("Moduły Kadry/Płace/Kalendarz są dostępne z sesji i wskazują z powrotem na tę samą sesję.")]
    public void Moduly_DostepneIWskazujaNaSesje()
    {
        // Punkt wejścia każdego scenariusza: z Session pobieramy moduły metodami rozszerzającymi
        // (GetKadry/GetPlace/GetKalend). Każdy moduł implementuje ISessionable.
        Kadry.Should().NotBeNull("session.GetKadry() musi zwrócić moduł Kadry");
        Place.Should().NotBeNull("session.GetPlace() musi zwrócić moduł Płace");
        Kalend.Should().NotBeNull("session.GetKalend() musi zwrócić moduł Kalendarz");

        Kadry.Session.Should().BeSameAs(Session);
        Place.Session.Should().BeSameAs(Session);
        Kalend.Session.Should().BeSameAs(Session);
    }

    [Test]
    [Description("Kartoteka pracowników (Pracownicy) z bazy Demo jest niepusta, a lookup po kodzie " +
                 "(WgKodu) zwraca rekord o zgodnym kodzie — to fundament scenariuszy odczytu.")]
    public void Pracownicy_KartotekaNiepusta_LookupPoKodzieDziala()
    {
        // Iteracja po kluczu WgKodu zwraca wiersze; klucz jest niegeneryczny, więc rzutujemy.
        var wszyscy = Kadry.Pracownicy.WgKodu.Cast<Prac>().ToList();
        wszyscy.Should().NotBeEmpty("baza Demo zawiera zatrudnionych pracowników");

        // Klucz unikalny WgKodu[kod] zwraca pojedynczy rekord lub null.
        var pierwszy = wszyscy.First();
        var poKodzie = Pracownik(pierwszy.Kod);
        poKodzie.Should().BeSameAs(pierwszy, "WgKodu[kod] to klucz unikalny — ten sam rekord co z iteracji");
    }

    [Test]
    [Description("Pracownik etatowy z Demo ma co najmniej jeden zapis historii kadrowej (PracHistoria), " +
                 "w której przechowywane są dane kadrowe i warunki etatu obowiązujące w danym okresie.")]
    public void Pracownik_MaZapisHistoriiKadrowej()
    {
        var p = PierwszyPracownik();

        // Pracownik to obiekt historyczny: dane „na dzień" leżą w kolekcji Historia (HistorySubTable).
        p.Historia.Cast<object>().Should().NotBeEmpty(
            "zatrudniony pracownik ma przynajmniej jeden zapis historyczny z danymi kadrowymi i etatem");
    }
}
