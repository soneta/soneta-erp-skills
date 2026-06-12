using System.Linq;
using AwesomeAssertions;
using NUnit.Framework;
using Soneta.Business;
using Soneta.Test;

namespace Soneta.Skills.Test.Workflow;

/// <summary>
/// Test dymny (smoke) potwierdzający, że infrastruktura testowa domeny Workflow działa:
/// sesja operacyjna jest powiązana z bazą Demo, moduły Workflow/Business/Zadania są dostępne,
/// a kluczowe tabele domeny (WFDefs, Tasks, Zadania) są osiągalne. To minimalny punkt wejścia,
/// na którym opierają się pozostałe rozdziały.
/// </summary>
[TestFixture]
public class SmokeTest : WorkflowTestBase
{
    [Test]
    [Description("Moduły Workflow/Business/Zadania są dostępne z sesji i wskazują z powrotem na tę samą sesję.")]
    public void Moduly_DostepneIWskazujaNaSesje()
    {
        // Punkt wejścia każdego scenariusza: z Session pobieramy moduły metodami rozszerzającymi
        // (GetWorkflow/GetBusiness/GetZadania). Każdy moduł implementuje ISessionable.
        Workflow.Should().NotBeNull("session.GetWorkflow() musi zwrócić moduł Workflow");
        Business.Should().NotBeNull("session.GetBusiness() musi zwrócić moduł Business");
        Zadania.Should().NotBeNull("session.GetZadania() musi zwrócić moduł Zadania");

        Workflow.Session.Should().BeSameAs(Session);
        Business.Session.Should().BeSameAs(Session);
        Zadania.Session.Should().BeSameAs(Session);
    }

    [Test]
    [Description("Kluczowe tabele domeny Workflow są osiągalne z modułów: definicje procesów (WFDefs), " +
                 "procesy (WFWorkflows), zadania (Tasks), definicje zadań/węzły (TaskDefs) i zadania CRM (Zadania).")]
    public void Tabele_Domeny_Osiagalne()
    {
        Workflow.WFDefs.Should().NotBeNull("tabela definicji procesów (WFDefs)");
        Workflow.WFWorkflows.Should().NotBeNull("tabela procesów (WFWorkflows)");
        Workflow.WFTransitions.Should().NotBeNull("tabela tranzycji (WFTransitions)");
        Business.Tasks.Should().NotBeNull("tabela zadań (Tasks)");
        Business.TaskDefs.Should().NotBeNull("tabela definicji zadań (TaskDefs)");
        Zadania.Zadania.Should().NotBeNull("tabela zadań CRM (Zadania)");
    }

    [Test]
    [Description("Diagnostyka danych Demo: wypisuje liczność definicji procesów, procesów, zadań i definicji " +
                 "zadań CRM w bazie Demo — punkty wejścia dla scenariuszy odczytu w kolejnych rozdziałach.")]
    public void DaneDemo_PunktyWejscia()
    {
        // Dane słownikowe pobieramy dynamicznie (iteracją), nie zakładamy konkretnych kodów na sztywno.
        var defs = Workflow.WFDefs.Cast<object>().Count();
        var procesy = Workflow.WFWorkflows.Cast<object>().Count();
        var zadaniaCrm = Zadania.Zadania.Cast<object>().Count();
        var defZadan = Zadania.DefZadan.Cast<object>().Count();

        TestContext.Out.WriteLine($"WFDefs={defs}, WFWorkflows={procesy}, ZadaniaCRM={zadaniaCrm}, DefZadan={defZadan}");

        // Definicje zadań CRM to dane konfiguracyjne obecne w każdej bazie — twarde minimum smoke testu.
        defZadan.Should().BeGreaterThan(0, "baza Demo zawiera definicje zadań CRM (DefZadania)");
    }
}
