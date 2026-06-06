using System.Linq;
using Soneta.Business;
using Soneta.Kadry;
using Soneta.Kalend;
using Soneta.Place;
using Soneta.Test;
using Soneta.Types;
using Prac = Soneta.Kadry.Pracownik;

namespace Soneta.Skills.Test.KadryPlace.Pracownik;

/// <summary>
/// Wspólna baza testów domeny Kadry/Płace (pracownik, etat, nieobecności, kalendarz, umowy, wypłaty).
/// Dziedziczy z <see cref="TestBase"/>, dzięki czemu:
/// <list type="bullet">
/// <item>udostępnia gotową sesję operacyjną (<c>Session</c>) powiązaną z testową bazą Demo (GoldStandard),</item>
/// <item>automatycznie wycofuje (rollback) wszystkie zmiany w bazie po zakończeniu testu,</item>
/// <item>daje metody pomocnicze <c>InTransaction</c>/<c>SaveDispose</c> do pracy w transakcjach.</item>
/// </list>
/// Baza dodaje skróty często powtarzane w testach kadrowo-płacowych: dostęp do modułów
/// (Kadry, Płace, Kalendarz) oraz pobieranie pracowników z bazy Demo po kodzie/nazwisku.
/// <para>
/// Cała baza operuje wyłącznie na <b>publicznym kontrakcie</b> platformy Soneta — tak jak dodatek
/// programisty zewnętrznego, który nie ma dostępu do kodu źródłowego aplikacji.
/// </para>
/// </summary>
public abstract class PracownikTestBase : TestBase
{
    // === Moduły bieżącej sesji operacyjnej ===

    /// <summary>Moduł Kadry — kartoteka pracowników (<c>Pracownicy</c>), historia kadrowa, etaty, umowy.</summary>
    protected KadryModule Kadry => Session.GetKadry();

    /// <summary>Moduł Płace — wypłaty, listy płac, elementy wynagrodzenia, definicje elementów.</summary>
    protected PlaceModule Place => Session.GetPlace();

    /// <summary>Moduł Kalendarz — nieobecności, kalendarze, plan pracy, dni pracy, RCP, limity.</summary>
    protected KalendModule Kalend => Session.GetKalend();

    // === Kody pracowników dostępnych w bazie Demo (GoldStandard) ===
    // Baza Demo zawiera ~80 zatrudnionych pracowników etatowych (po jednym zapisie historii każdy).
    // Kody są stabilne między uruchomieniami — używamy ich jako punktów wejścia do scenariuszy odczytu.

    /// <summary>Kody przykładowych pracowników etatowych z bazy Demo (pole <c>Pracownik.Kod</c>).</summary>
    protected static class Pracownik_
    {
        public const string Andrzejewski = "006";
        public const string Bednarek = "007";
        public const string Bujak = "008";
        public const string Strzelecki = "009";
    }

    // === Wyszukiwanie pracowników (publiczne API) ===

    /// <summary>Pobiera pracownika po kodzie (klucz unikalny <c>WgKodu</c>, case-insensitive) albo <c>null</c>.</summary>
    protected Prac Pracownik(string kod) => Kadry.Pracownicy.WgKodu[kod];

    /// <summary>Pierwszy pracownik wg kodu — wygodny, deterministyczny punkt startu dla testów odczytu.</summary>
    protected Prac PierwszyPracownik() => Kadry.Pracownicy.WgKodu.Cast<Prac>().First();
}
