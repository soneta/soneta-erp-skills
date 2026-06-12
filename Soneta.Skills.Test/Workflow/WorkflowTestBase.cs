using Soneta.Business;
using Soneta.Business.Db;
using Soneta.Test;
using Soneta.Workflow;
using Soneta.Zadania;

namespace Soneta.Skills.Test.Workflow;

/// <summary>
/// Wspólna baza testów domeny Workflow (definicje procesów, węzły, tranzycje, procesy,
/// zadania operatora, zadania CRM).
/// Dziedziczy z <see cref="TestBase"/>, dzięki czemu:
/// <list type="bullet">
/// <item>udostępnia gotową sesję operacyjną (<c>Session</c>) powiązaną z testową bazą Demo (GoldStandard),</item>
/// <item>automatycznie wycofuje (rollback) wszystkie zmiany w bazie po zakończeniu testu,</item>
/// <item>daje metody pomocnicze <c>InTransaction</c>/<c>SaveDispose</c> do pracy w transakcjach.</item>
/// </list>
/// Baza dodaje skróty do modułów domeny: Workflow (definicje procesów, tranzycje, procesy),
/// Business (tabele <c>Tasks</c> i <c>TaskDefs</c>) oraz Zadania (zadania CRM / aktywności).
/// <para>
/// Cała baza operuje wyłącznie na <b>publicznym kontrakcie</b> platformy Soneta — tak jak dodatek
/// programisty zewnętrznego, który nie ma dostępu do kodu źródłowego aplikacji.
/// </para>
/// </summary>
public abstract class WorkflowTestBase : TestBase
{
    // === Moduły bieżącej sesji operacyjnej ===

    /// <summary>Moduł Workflow — definicje procesów (<c>WFDefs</c>), tranzycje (<c>WFTransitions</c>),
    /// role procesowe, schematy generatora obiektów oraz uruchomione procesy (<c>WFWorkflows</c>).</summary>
    protected WorkflowModule Workflow => Session.GetWorkflow();

    /// <summary>Moduł Business — tabele zadań (<c>Tasks</c>) i definicji zadań/węzłów (<c>TaskDefs</c>).</summary>
    protected BusinessModule Business => Session.GetBusiness();

    /// <summary>Moduł Zadania — zadania CRM / aktywności (<c>Zadania</c>) i ich definicje (<c>DefZadan</c>).</summary>
    protected ZadaniaModule Zadania => Session.GetZadania();
}
