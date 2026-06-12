using System;
using System.Linq;
using AwesomeAssertions;
using NUnit.Framework;
using Soneta.Business;
using Soneta.Business.Db;
using Soneta.Workflow.Config;
using Soneta.Workflow.Enums;
using Soneta.Workflow.Workers;

namespace Soneta.Skills.Test.Workflow;

/// <summary>
/// Rozdział A — „Definicja procesu workflow (projektowanie)" (receptury WORKFLOW-A1 … WORKFLOW-A7).
/// <para>
/// Testy są <b>wykonywalną dokumentacją</b> publicznego kontraktu platformy Soneta dla domeny
/// Workflow. Każda metoda mapuje się 1:1 do receptury z dokumentu skilla
/// <c>workflow/WORKFLOW01-definicja-procesu.md</c>. Kluczowy model: definicja procesu
/// <c>WFDefinition</c> (tabela <c>WFDefs</c>) to dane <b>konfiguracyjne</b> — edycja wymaga sesji
/// konfiguracyjnej; w testach na <c>TestBase</c> używamy <c>InConfigTransaction</c> +
/// <c>AddConfig</c>/<c>GetConfig</c> + <c>SaveDisposeConfig()</c>.
/// </para>
/// <para>
/// Baza Demo nie musi zawierać żadnych definicji procesów — wszystkie scenariusze budują własną
/// definicję od zera (rollback po teście sprząta). Operujemy wyłącznie na publicznym kontrakcie.
/// </para>
/// </summary>
[TestFixture]
public class RozdzialA_DefinicjaProcesuTest : WorkflowTestBase
{
    /// <summary>Unikalny symbol definicji na potrzeby pojedynczego testu (Symbol nie jest unikalny
    /// w tabeli, ale chcemy jednoznacznie odnajdywać własne dane kluczem WgSymbolu).</summary>
    private static string NowySymbol(string prefix) =>
        prefix + "_" + Guid.NewGuid().ToString("N").Substring(0, 6);

    // ============================== WORKFLOW-A1 — Utworzenie nowej definicji procesu (★) ==============================

    [Test]
    [Description("WORKFLOW-A1: nowa definicja procesu (AddRow w sesji KONFIGURACYJNEJ); OnAdded automatycznie " +
                 "ustawia EditType=Simple, SingleWorkflowInstance=true, tworzy domyślną rolę procesową i tor " +
                 "diagramu; po zapisie definicję odnajdujemy kluczami WgSymbolu / ByName.")]
    public void WORKFLOW_A1_NowaDefinicja_OnAddedUstawiaDomyslne_IZapisuje()
    {
        var symbol = NowySymbol("A1");
        Guid guid = Guid.Empty;

        // Definicje procesów to dane KONFIGURACYJNE — edytujemy w transakcji konfiguracyjnej
        // (odpowiednik login.CreateSession(config: true) + session.Logout(editMode: true)).
        InConfigTransaction(() =>
        {
            // AddConfig = AddRow w sesji konfiguracyjnej; zwraca typowany wiersz.
            var definicja = AddConfig(new WFDefinition());
            definicja.Symbol = symbol;
            definicja.Name = "Obieg wniosku urlopowego " + symbol;
            definicja.Description = "Akceptacja wniosków urlopowych";

            // OnAdded zrobiło już całą robotę — NIE ustawiamy tych pól ręcznie:
            definicja.EditType.Should().Be(EditTypeEnum.Simple, "OnAdded ustawia EditType=Simple");
            definicja.SingleWorkflowInstance.Should().BeTrue("OnAdded ustawia SingleWorkflowInstance=true");
            definicja.ProcessRoles.Cast<WFProcessRole>().Should().ContainSingle(
                "OnAdded tworzy domyślną rolę procesową razem z definicją");
            string diagram = definicja.SerializedDiagram;
            string.IsNullOrEmpty(diagram).Should().BeFalse("OnAdded zakłada tor (swimlane) definicji na diagramie");

            // Konstruktor WFDefinition determinuje tryb Standard (diagramowy):
            definicja.DefinitionType.Should().Be(DefinitionTypeEnum.Standard);
            // Świeża definicja startuje w trybie MODELOWANIA:
            definicja.IsDeployed.Should().BeFalse("nowa definicja jest w trybie modelowania");

            guid = definicja.Guid;
        });
        // Zapis sesji konfiguracyjnej (odpowiednik session.Save()).
        SaveDisposeConfig();

        // Odczyt po zapisie — świeża sesja konfiguracyjna, wiersz po Guid:
        var def2 = GetConfig<WFDefinition>(guid);
        def2.Should().NotBeNull("definicja została utrwalona w danych konfiguracyjnych");
        def2.Symbol.Should().Be(symbol);
        def2.Description.Should().Be("Akceptacja wniosków urlopowych");

        // Klucz WgSymbolu — indeksator [string] zwraca pojedynczy wiersz (pierwszy o tym symbolu):
        var wfDefs = def2.Module.WFDefs;
        WFDefinition poSymbolu = wfDefs.WgSymbolu[symbol];
        poSymbolu.Should().BeSameAs(def2, "WgSymbolu[symbol] odnajduje zapisaną definicję");

        // Klucz ByName — indeksator [string] zwraca PODZBIÓR (SubTable), bierzemy GetFirst():
        var poNazwie = wfDefs.ByName[def2.Name].GetFirst();
        poNazwie.Should().BeSameAs(def2, "ByName[nazwa] odnajduje zapisaną definicję");
    }

    [Test]
    [Description("WORKFLOW-A1 (pułapka): definicja bez węzła startowego — GetStartPoint() rzuca " +
                 "NotSupportedException dopóki nie istnieje węzeł startowy (WORKFLOW02).")]
    public void WORKFLOW_A1_GetStartPoint_BezWezlaStartowego_RzucaWyjatek()
    {
        InConfigTransaction(() =>
        {
            var definicja = AddConfig(new WFDefinition());
            definicja.Symbol = NowySymbol("A1S");
            definicja.Name = "Definicja bez startu " + definicja.Symbol;

            // Świeża definicja nie ma żadnych węzłów (TaskDefs puste):
            definicja.TaskDefs.Cast<TaskDefinition>().Should().BeEmpty("nowa definicja nie ma węzłów");

            // Punkt startowy nie istnieje — publiczny kontrakt rzuca NotSupportedException:
            System.Action start = () => definicja.GetStartPoint();
            start.Should().Throw<NotSupportedException>(
                "GetStartPoint() rzuca, dopóki nie istnieje węzeł startowy (IsStart)");
        });
        // Rollback TestBase posprząta — nie zapisujemy.
    }

    // ============================== WORKFLOW-A2 — Tryb i sposób edycji (Standard / Engine) ==============================

    [Test]
    [Description("WORKFLOW-A2: DefinitionType ustawia KONSTRUKTOR klasy (WFDefinition→Standard, " +
                 "WFDefinitionExtend→Engine), nie setter; sposób edycji (EditType) definicji standardowej " +
                 "można podnieść z Simple na Advanced.")]
    public void WORKFLOW_A2_DefinitionType_DeterminujeKonstruktor_EditTypeMoznaZmienic()
    {
        var symbolEng = NowySymbol("A2E");
        var symbolStd = NowySymbol("A2S");
        Guid guidEng = Guid.Empty, guidStd = Guid.Empty;

        InConfigTransaction(() =>
        {
            // Definicja kodowa (Engine) — OSOBNA klasa; DefinitionType ustawia konstruktor:
            var engineDef = AddConfig(new WFDefinitionExtend());
            engineDef.Symbol = symbolEng;
            engineDef.Name = "Proces kodowy " + symbolEng;
            engineDef.DefinitionType.Should().Be(DefinitionTypeEnum.Engine,
                "konstruktor WFDefinitionExtend ustawia tryb Engine (jednozakładkowy)");

            // Definicja standardowa (diagramowa) — sposób edycji można podnieść z Simple:
            var stdDef = AddConfig(new WFDefinition());
            stdDef.Symbol = symbolStd;
            stdDef.Name = "Proces diagramowy " + symbolStd;
            stdDef.DefinitionType.Should().Be(DefinitionTypeEnum.Standard,
                "konstruktor WFDefinition ustawia tryb Standard (wielozakładkowy)");
            stdDef.EditType = EditTypeEnum.Advanced;   // Soneta.Workflow.Enums

            guidEng = engineDef.Guid;
            guidStd = stdDef.Guid;
        });
        SaveDisposeConfig();

        // Po zapisie tryby i sposób edycji są utrwalone:
        GetConfig<WFDefinition>(guidEng).DefinitionType.Should().Be(DefinitionTypeEnum.Engine);
        var std2 = GetConfig<WFDefinition>(guidStd);
        std2.DefinitionType.Should().Be(DefinitionTypeEnum.Standard);
        std2.EditType.Should().Be(EditTypeEnum.Advanced);
    }

    // ============================== WORKFLOW-A3 — Role procesowe (tory diagramu) (★) ==============================

    [Test]
    [Description("WORKFLOW-A3: rolę procesową tworzy konstruktor WFProcessRole(definicja); ExecutorType " +
                 "wybiera źródło wykonawcy (tu Operator); kolekcja ProcessRoles jest uporządkowana po Lp, " +
                 "a OnAdded dokłada nowy tor do diagramu definicji.")]
    public void WORKFLOW_A3_RolaProcesowa_Operator_DodawanaDoDefinicji()
    {
        var symbol = NowySymbol("A3");
        Guid guid = Guid.Empty;

        InConfigTransaction(() =>
        {
            var definicja = AddConfig(new WFDefinition());
            definicja.Symbol = symbol;
            definicja.Name = "Definicja z rolami " + symbol;

            // Razem z definicją powstała JEDNA domyślna rola (WORKFLOW-A1) — nie kasujemy jej
            // (definicja musi mieć co najmniej jedną rolę).
            definicja.ProcessRoles.Cast<WFProcessRole>().Should().ContainSingle();
            string diagramPrzed = definicja.SerializedDiagram;

            // Konstruktor wiąże rolę z definicją; AddConfig włącza wiersz do sesji konfiguracyjnej.
            // OnAdded automatycznie dokłada nowy tor (swimlane) do SerializedDiagram.
            var rola = AddConfig(new WFProcessRole(definicja));
            rola.Name = "Kadrowa";
            rola.ExecutorType = WFProcessRoleExecutorType.Operator;   // wykonawca = konkretny operator
            // UWAGA: wiersz operatora musi pochodzić z TEJ SAMEJ sesji (konfiguracyjnej) —
            // GetConfig(row) „przepina" wiersz Login.Operator do sesji konfiguracyjnej.
            rola.Operator = GetConfig(Login.Operator);

            // Tor doszedł na diagram (warstwa prezentacji utrzymywana automatycznie — WORKFLOW-A4):
            string diagramPo = definicja.SerializedDiagram;
            diagramPo.Should().NotBe(diagramPrzed, "OnAdded roli dokłada tor do SerializedDiagram");

            guid = definicja.Guid;
        });
        SaveDisposeConfig();

        // Odczyt ról definicji po zapisie — porządek po Lp:
        var def2 = GetConfig<WFDefinition>(guid);
        var role = def2.ProcessRoles.Cast<WFProcessRole>().ToList();
        role.Should().HaveCount(2, "domyślna rola z OnAdded + nasza rola 'Kadrowa'");
        role.Select(r => r.Lp).Should().BeInAscendingOrder("LpSubTable porządkuje role po Lp");

        var kadrowa = role.Single(r => r.Name == "Kadrowa");
        kadrowa.ExecutorType.Should().Be(WFProcessRoleExecutorType.Operator);
        kadrowa.Operator.Should().NotBeNull("przy ExecutorType=Operator wykonawcę wskazuje pole Operator");
        kadrowa.WorkflowDefinition.Should().BeSameAs(def2, "konstruktor związał rolę z definicją");
    }

    [Test]
    [Description("WORKFLOW-A3 (pułapka): RoleName to property pomostowe — setter mapuje nazwę ISTNIEJĄCEJ " +
                 "roli operatorów na RoleGuid; nieznana nazwa daje RoleGuid=Guid.Empty BEZ wyjątku, " +
                 "więc po ustawieniu trzeba zweryfikować RoleGuid.")]
    public void WORKFLOW_A3_RoleName_NieznanaNazwa_DajeGuidEmpty_BezWyjatku()
    {
        InConfigTransaction(() =>
        {
            var definicja = AddConfig(new WFDefinition());
            definicja.Symbol = NowySymbol("A3R");
            definicja.Name = "Definicja test RoleName " + definicja.Symbol;

            var rola = AddConfig(new WFProcessRole(definicja));
            rola.Name = "Przełożony";
            rola.ExecutorType = WFProcessRoleExecutorType.Role;   // wykonawca = rola operatorów

            // Nazwa roli operatorów, która NA PEWNO nie istnieje w bazie Demo:
            var nieistniejaca = "Rola_" + Guid.NewGuid().ToString("N");
            System.Action ustaw = () => rola.RoleName = nieistniejaca;

            // Setter NIE rzuca wyjątku dla nieznanej nazwy…
            ustaw.Should().NotThrow("nieznana nazwa roli nie powoduje wyjątku (pułapka!)");
            // …tylko zostawia RoleGuid=Guid.Empty — to programista musi to zweryfikować:
            rola.RoleGuid.Should().Be(Guid.Empty,
                "RoleName z nieistniejącą nazwą wpisuje Guid.Empty do RoleGuid");
        });
        // Celowo bez zapisu — pokazujemy wyłącznie zachowanie settera; rollback sprząta.
    }

    // ============================== WORKFLOW-A5 — Kopiowanie definicji procesu (★) ==============================

    [Test]
    [Description("WORKFLOW-A5: WFDefinitionCopyWorker.CopyWfDefinition() kopiuje całą definicję z nowymi " +
                 "Guidami (sam zarządza transakcją — bez własnego Logout); kopia ma IsDeployed=false " +
                 "i komplet ról; Symbol kopii może się różnić (wymuszona unikalność).")]
    public void WORKFLOW_A5_KopiowanieDefinicji_TworzyKopieWTrybieModelowania()
    {
        // KROK 1: budujemy i zapisujemy definicję źródłową (z dodatkową rolą procesową).
        var symbol = NowySymbol("A5");
        Guid guidZrodla = Guid.Empty;

        InConfigTransaction(() =>
        {
            var definicja = AddConfig(new WFDefinition());
            definicja.Symbol = symbol;
            definicja.Name = "Źródło kopii " + symbol;
            definicja.Description = "Definicja do skopiowania";

            var rola = AddConfig(new WFProcessRole(definicja));
            rola.Name = "Akceptujący";
            rola.ExecutorType = WFProcessRoleExecutorType.Operator;
            rola.Operator = GetConfig(Login.Operator);   // operator z sesji KONFIGURACYJNEJ

            guidZrodla = definicja.Guid;
        });
        SaveDisposeConfig();

        // KROK 2: kopiowanie workerem — czynność wymaga sesji KONFIGURACYJNEJ edycyjnej
        // (IsEnabledCopyWfDefinition: session.IsConfig && !session.ReadOnly).
        var zrodlo = GetConfig<WFDefinition>(guidZrodla);
        int liczbaRolZrodla = zrodlo.ProcessRoles.Cast<WFProcessRole>().Count();

        // Parametry workera czytają definicję z Context:
        var cx = Context.Empty.Clone(ConfigEditSession);
        cx.Set(zrodlo);

        var worker = new WFDefinitionCopyWorker
        {
            Row = zrodlo,
            Params = new WFDefinitionCopyWorker.WFDefinitionCopyWorkerParams(cx)
            {
                CopyOgSchemas = true,
                CopyWizardDefs = true,
                CopyWFItemDescriptions = true,
                UpdateRights = WFDefinitionCopyWorker.UpdateRightsMode.Copy   // te same prawa na kopii
            }
        };

        // CopyWfDefinition() SAM otwiera transakcję i robi Commit — nie opakowujemy go w InConfigTransaction.
        WFDefinition kopia = worker.CopyWfDefinition();
        Guid guidKopii = kopia.Guid;

        // Kopia istnieje od razu w sesji konfiguracyjnej; po workerze wystarczy zapis sesji:
        SaveDisposeConfig();

        // KROK 3: weryfikacja kopii po zapisie — odszukujemy ją po zwróconym Guid (nie po Symbol,
        // bo worker wymusza unikalność Symbol dopiskiem przy duplikacie).
        var kopia2 = GetConfig<WFDefinition>(guidKopii);
        kopia2.Should().NotBeNull("kopia definicji została utrwalona");
        kopia2.Guid.Should().NotBe(guidZrodla, "kopia dostaje NOWY Guid");
        kopia2.IsDeployed.Should().BeFalse("kopia zawsze startuje w trybie modelowania (IsDeployed=false)");
        kopia2.ProcessRoles.Cast<WFProcessRole>().Should().HaveCount(liczbaRolZrodla,
            "kopiowane są wszystkie role procesowe definicji źródłowej");

        // Źródło pozostało nietknięte:
        var zrodlo2 = GetConfig<WFDefinition>(guidZrodla);
        zrodlo2.Symbol.Should().Be(symbol, "definicja źródłowa nie zmienia się przy kopiowaniu");
    }

    // ============================== WORKFLOW-A7 — Wdrożenie definicji (★) ==============================

    [Test]
    [Description("WORKFLOW-A7: wdrożenie = IsDeployed=true w transakcji konfiguracyjnej; operacja jest " +
                 "JEDNOKIERUNKOWA — próba cofnięcia wdrożonej, zapisanej definicji do modelowania " +
                 "rzuca wyjątek wtórnej zmiany pola.")]
    public void WORKFLOW_A7_Wdrozenie_JestJednokierunkowe()
    {
        // KROK 1: nowa definicja — startuje w trybie MODELOWANIA.
        var symbol = NowySymbol("A7");
        Guid guid = Guid.Empty;

        InConfigTransaction(() =>
        {
            var definicja = AddConfig(new WFDefinition());
            definicja.Symbol = symbol;
            definicja.Name = "Definicja do wdrożenia " + symbol;
            definicja.IsDeployed.Should().BeFalse("świeża definicja jest w trybie modelowania");
            guid = definicja.Guid;
        });
        SaveDisposeConfig();

        // KROK 2: wdrożenie — to samo robi czynność menu „Tryb wdrożeniowy".
        InConfigTransaction(() =>
        {
            var definicja = GetConfig<WFDefinition>(guid);
            definicja.IsDeployed = true;     // jednokierunkowe: wdrożonej definicji nie cofniesz
        });
        SaveDisposeConfig();

        // KROK 3: po zapisie definicja jest wdrożona.
        GetConfig<WFDefinition>(guid).IsDeployed.Should().BeTrue("wdrożenie zostało utrwalone");

        // KROK 4 (pułapka): próba powrotu do modelowania na zapisanej, wdrożonej definicji
        // rzuca wyjątek wtórnej zmiany pola („Tryb wdrożenia"). Droga do poprawek = kopia (A5).
        InConfigTransaction(() =>
        {
            var definicja = GetConfig<WFDefinition>(guid);
            System.Action cofniecie = () => definicja.IsDeployed = false;
            cofniecie.Should().Throw<Exception>(
                "zmiana IsDeployed na wdrożonej definicji jest zablokowana — operacja jednokierunkowa");
        });
    }

    [Test]
    [Description("WORKFLOW-A7 (Locked): tymczasowe wyłączenie wdrożonej definicji z użycia to Locked=true — " +
                 "bez cofania wdrożenia (start automatyczny filtruje po IsDeployed && !Locked).")]
    public void WORKFLOW_A7_Locked_WylaczaDefinicjeBezCofaniaWdrozenia()
    {
        var symbol = NowySymbol("A7L");
        Guid guid = Guid.Empty;

        InConfigTransaction(() =>
        {
            var definicja = AddConfig(new WFDefinition());
            definicja.Symbol = symbol;
            definicja.Name = "Definicja blokowana " + symbol;
            definicja.IsDeployed = true;     // wdrażamy od razu (przed pierwszym zapisem)
            guid = definicja.Guid;
        });
        SaveDisposeConfig();

        // Blokada wdrożonej definicji — wyłącza ją z użycia, ale NIE cofa wdrożenia:
        InConfigTransaction(() =>
        {
            var definicja = GetConfig<WFDefinition>(guid);
            definicja.Locked = true;
        });
        SaveDisposeConfig();

        var def2 = GetConfig<WFDefinition>(guid);
        def2.IsDeployed.Should().BeTrue("blokada nie cofa wdrożenia");
        def2.Locked.Should().BeTrue("Locked=true wyłącza definicję z użycia (filtr IsDeployed && !Locked)");
    }

    // ============================== WORKFLOW-A4 — Diagram procesu (odczyt) ==============================

    [Test]
    [Description("WORKFLOW-A4: SerializedDiagram (MemoText/JSON) to WARSTWA PREZENTACJI utrzymywana " +
                 "automatycznie; źródłem prawdy o logice procesu są kolekcje TaskDefs i Transitions.")]
    public void WORKFLOW_A4_Diagram_UtrzymywanyAutomatycznie_LogikaWTaskDefsITransitions()
    {
        InConfigTransaction(() =>
        {
            var definicja = AddConfig(new WFDefinition());
            definicja.Symbol = NowySymbol("A4");
            definicja.Name = "Definicja z diagramem " + definicja.Symbol;

            // Diagram powstał automatycznie razem z definicją (tor główny) — nie edytujemy JSON ręcznie:
            string diagramJson = definicja.SerializedDiagram;
            string.IsNullOrEmpty(diagramJson).Should().BeFalse("OnAdded definicji założył tor na diagramie");

            // Struktura LOGICZNA procesu (źródło prawdy dla silnika) jest na razie pusta:
            definicja.TaskDefs.Cast<TaskDefinition>().Should().BeEmpty("nowa definicja nie ma węzłów");
            definicja.Transitions.Cast<WFTransition>().Should().BeEmpty("nowa definicja nie ma tranzycji");
        });
        // Bez zapisu — scenariusz czysto poglądowy; rollback sprząta.
    }
}
