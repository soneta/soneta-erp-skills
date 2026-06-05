using System;
using Soneta.Core;
using Soneta.CRM;
using Soneta.Test;

namespace Soneta.Skills.Test.CRM.Kontrahenci;

/// <summary>
/// Wspólna baza testów kontrahenta. Dziedziczy z <see cref="TestBase"/>, dzięki czemu:
/// <list type="bullet">
/// <item>udostępnia gotową sesję operacyjną (<c>Session</c>) powiązaną z testową bazą Demo,</item>
/// <item>automatycznie wycofuje (rollback) wszystkie zmiany w bazie po zakończeniu testu,</item>
/// <item>daje metody pomocnicze <c>InTransaction</c>/<c>SaveDispose</c> do pracy w transakcjach.</item>
/// </list>
/// Baza dodaje skróty często powtarzane w testach kontrahenta (dostęp do modułu CRM,
/// generowanie unikalnego kodu, utworzenie minimalnego kontrahenta).
/// </summary>
public abstract class KontrahentTestBase : TestBase
{
    /// <summary>Moduł CRM bieżącej sesji operacyjnej.</summary>
    protected CRMModule Crm => Session.GetCRM();

    /// <summary>Generuje krótki, unikalny kod kontrahenta (na potrzeby testów).</summary>
    protected static string UnikalnyKod() => Guid.NewGuid().ToString("N").Substring(0, 10);

    /// <summary>
    /// Tworzy w bieżącej sesji nowego kontrahenta z minimalnym kompletem danych
    /// (kod, nazwa, status i rodzaj podmiotu) wewnątrz transakcji edycyjnej.
    /// Zwrócony obiekt żyje w bieżącej sesji — pozostaje ważny do czasu <c>SaveDispose</c>.
    /// </summary>
    protected Kontrahent UtworzKontrahenta(
        string kod,
        string nazwa = null,
        StatusPodmiotu status = StatusPodmiotu.PodmiotGospodarczy,
        RodzajPodmiotu rodzaj = RodzajPodmiotu.Krajowy)
    {
        Kontrahent k = null;
        InTransaction(() =>
        {
            // AddRow MUSI poprzedzać ustawianie pól — obiekt najpierw trafia do tabeli.
            k = new Kontrahent();
            Session.AddRow(k);
            k.Kod = kod;
            k.Nazwa = nazwa ?? kod;
            k.StatusPodmiotu = status;
            k.RodzajPodmiotu = rodzaj;
        });
        return k;
    }
}
