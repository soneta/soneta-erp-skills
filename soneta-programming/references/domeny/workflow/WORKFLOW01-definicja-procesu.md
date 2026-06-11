# WORKFLOW01 — Definicja procesu workflow (projektowanie)

> Wspólne fakty o typie, podstawowe typy i szablon wzorca: [../workflow.md](../workflow.md).

> **Model „definicja = konfiguracja".** Definicja procesu `Soneta.Workflow.Config.WFDefinition`
> (tabela `WFDefs`) i wszystkie obiekty projektowe (`TaskDefinition`, `WFTransition`,
> `WFProcessRole`, `OGSchema`) to **dane konfiguracyjne** — modyfikacja wymaga **sesji
> konfiguracyjnej** `login.CreateSession(readOnly: false, config: true, name: …)`; w sesji
> operacyjnej dane te są dostępne tylko do odczytu (patrz session-login.md). Uruchomione procesy
> (`WFWorkflow`) i zadania (`Task`) to dane operacyjne — opisują je rozdziały WORKFLOW04–06.
>
> Cykl życia definicji: **modelowanie** (`IsDeployed == false`, definicję można swobodnie
> przebudowywać) → **wdrożenie** (`IsDeployed = true`, jednokierunkowe — WORKFLOW-A7) →
> uruchamianie instancji procesów. Dostęp: `session.GetWorkflow().WFDefs`, klucze
> `WFDefs.WgSymbolu[symbol]` (zwraca **pojedynczy** `WFDefinition` — pierwszy o danym symbolu),
> `WFDefs.ByName[nazwa]` (zwraca `SubTable<WFDefinition>` — użyj `.GetFirst()`) oraz indeksator
> `WFDefs[guid]`.
>
> W testach na `Soneta.Test.TestBase` odpowiednikiem sesji konfiguracyjnej są
> `ConfigEditSession` + `InConfigTransaction(...)` + `SaveDisposeConfig()`.

### WORKFLOW-A1 — Utworzenie nowej definicji procesu workflow (★)

**Cel:** utworzyć od zera nową definicję procesu workflow (szablon procesu), gotową do dodawania
węzłów (WORKFLOW02) i tranzycji (WORKFLOW03).

**Mechanizm (kluczowy):** dodanie `new WFDefinition()` przez `AddRow` uruchamia `OnAdded`, które
automatycznie: ustawia `EditType = EditTypeEnum.Simple` i `SingleWorkflowInstance = true`, tworzy
**domyślną rolę procesową** („Domyślna rola procesowa", WORKFLOW-A3) oraz zakłada tor (swimlane)
definicji na diagramie (`SerializedDiagram`). Nie trzeba (i nie należy) robić tego ręcznie.

**Warianty:**

| Wariant | Klasa | Charakterystyka |
|---|---|---|
| Definicja standardowa (diagramowa) | `WFDefinition` | `DefinitionType = Standard` („Wielozakładkowy"); węzły/tranzycje modelowane na diagramie |
| Definicja typu Engine (kodowa) | `WFDefinitionExtend` | `DefinitionType = Engine` („Jednozakładkowy"); przebieg opisany kodem — patrz WORKFLOW-A2 |

**Pola i typy (`Soneta.Workflow.Config.WFDefinition`, tabela `WFDefs`, konfiguracyjna):**

| Pole | Typ | Opis |
|---|---|---|
| `Symbol` | `string` | skrótowa nazwa; klucz `WgSymbolu[string]` → pojedynczy `WFDefinition` (pierwszy o tym symbolu) |
| `Name` | `string` | nazwa definicji; klucz `ByName[string]` → `SubTable<WFDefinition>` (wymaga `.GetFirst()`) |
| `Description` | `string` | opis definicji |
| `DefinitionType` | `Soneta.Business.Db.DefinitionTypeEnum` | `Standard` / `Engine` / `None` — ustawiany **konstruktorem** klasy, nie setterem |
| `EditType` | `Soneta.Workflow.Enums.EditTypeEnum` | sposób edycji (WORKFLOW-A2); `Simple` po `OnAdded` |
| `IsDeployed` | `bool` | tryb modelowania/wdrożenia (WORKFLOW-A7) |
| `SingleWorkflowInstance` | `bool` | `true` (domyślne) = blokada drugiego **aktywnego** procesu tej definicji dla tego samego obiektu nadrzędnego |
| `Locked` | `bool` | definicja zablokowana (wyłączona z użycia) |
| `Numerator` | `Soneta.Core.DefinicjaNumeracji` (subrow) | sposób numerowania (`Numerator.Wzor: string`, `Numerator.PodczasEdycji: bool`) |
| `Host` | `Soneta.Business.IWFDefinitionHost` (iface-ref) | właściciel definicji wbudowanej (np. `DefZadania`, `DefProjektu`); `null` dla samodzielnej |
| `SerializedDiagram` | `Soneta.Business.MemoText` | dane diagramu (JSON) — WORKFLOW-A4 |
| `TaskDefs` | `SubTable<Soneta.Business.Db.TaskDefinition>` | węzły procesu (WORKFLOW02) |
| `Transitions` | `SubTable<Soneta.Workflow.Config.WFTransition>` | tranzycje (WORKFLOW03) |
| `ProcessRoles` | `LpSubTable<Soneta.Workflow.Config.WFProcessRole>` | role procesowe (WORKFLOW-A3) |
| `WFWorkflows` | `SubTable<Soneta.Workflow.WFWorkflow>` | uruchomione instancje procesów (WORKFLOW04) |
| `Version`, `PrevVersionGuid` | `WFDefVersion` (subrow: `Major`, `Minor`), `Guid` | wersjonowanie definicji |

**Snippet:**

```csharp
// Definicje procesów to dane KONFIGURACYJNE — edycja wymaga sesji konfiguracyjnej:
using (var session = login.CreateSession(readOnly: false, config: true, name: "Nowa definicja WF"))
{
    WFDefinition definicja;
    using (var t = session.Logout(editMode: true))
    {
        // AddRow zwraca typowany wiersz; OnAdded ustawia EditType=Simple,
        // SingleWorkflowInstance=true i tworzy domyślną rolę procesową + tor diagramu.
        definicja = session.AddRow(new WFDefinition());
        definicja.Symbol = "URLOP";
        definicja.Name = "Obieg wniosku urlopowego";
        definicja.Description = "Akceptacja wniosków urlopowych";
        t.Commit();
    }
    session.Save();
}

// Odczyt (może być dowolna sesja — dane konfiguracyjne są czytelne także operacyjnie):
using (var session = login.CreateSession(readOnly: true, config: false, name: "Odczyt WF"))
{
    var wfDefs = session.GetWorkflow().WFDefs;
    var def = wfDefs.WgSymbolu["URLOP"];                 // klucz po Symbol — pojedynczy wiersz
    foreach (WFDefinition d in wfDefs)
    {
        // d.Symbol, d.Name, d.IsDeployed, d.TaskDefs.Count, d.Transitions.Count
    }
}
```

**Pułapki:**
- **Sesja operacyjna nie zmodyfikuje `WFDefs`** — próba edycji danych konfiguracyjnych poza sesją
  `config: true` kończy się wyjątkiem. Nie używaj `login.ExecuteConfig(...)` do zwracania wierszy
  definicji na zewnątrz (sesja współdzielona, inne wątki) — zwracaj wartości proste.
- Indeksatory działają **odwrotnie** niż mogłoby się wydawać: `WFDefs.WgSymbolu[symbol]` zwraca
  **pojedynczy** `WFDefinition` (pierwszy o danym symbolu — `Symbol` nie musi być unikalny, ale
  indeksator i tak zwraca jeden wiersz, **bez** `GetFirst()`), natomiast `WFDefs.ByName[nazwa]`
  zwraca `SubTable<WFDefinition>` i wymaga `.GetFirst()`. Zmiana `Symbol` przepina licznik numeracji ról.
- Nie zakładaj istnienia definicji procesów w bazie Demo — scenariusze buduj od zera.
- Świeża definicja jest w trybie **modelowania** (`IsDeployed == false`) — nie wystartuje z niej
  proces automatyczny; wdrożenie to WORKFLOW-A7.
- `DefinitionType` jest determinowany konstruktorem (`WFDefinition` → `Standard`,
  `WFDefinitionExtend` → `Engine`) — nie da się „przestawić" istniejącego wiersza setterem.
- Punkt startowy procesu zwraca `definicja.GetStartPoint(): TaskDefinition` — rzuca
  `NotSupportedException`, dopóki nie istnieje węzeł startowy (`IsStart`, start z menu) — WORKFLOW02.

### WORKFLOW-A2 — Tryb i sposób edycji definicji (Standard / Engine, EditType)

**Cel:** świadomie wybrać tryb definicji (diagramowa vs kodowa) i sposób edycji (`EditType`).

**Warianty:**

| Pole | Wartości | Znaczenie |
|---|---|---|
| `DefinitionType: Soneta.Business.Db.DefinitionTypeEnum` | `Standard` („Wielozakładkowy"), `Engine` („Jednozakładkowy"), `None` („Brak") | tryb definicji; `Standard` = modelowanie na diagramie, `Engine` = przebieg opisany kodem |
| `EditType: Soneta.Workflow.Enums.EditTypeEnum` | `Simple` („Uproszczony"), `Extended` („Rozszerzony"), `Advanced` („Zaawansowany"), `DependentOn` („Zależny"), `AdvancedEngine` („Zaawansowany jednozakładkowy") | sposób/zakres edycji definicji w konfiguracji |
| `IsDiagramEditedInHtml: bool` | `true`/`false` | diagram edytowany w designerze HTML (interfejs przeglądarkowy) zamiast desktopowego |

**Pola i typy (uzupełniające, `WFDefinition`):**

| Pole | Typ | Opis |
|---|---|---|
| `DependentEngine` | `bool` | wariant „jednozakładkowy zależny" |
| `EngineCode` | `Soneta.Business.MemoText` | kod algorytmu definicji typu Engine |
| `InterfaceMode` | `Soneta.Business.Db.TaskInterfaceModeEnum` | tryb wyboru ścieżki w procesie (prezentacja decyzji) |
| `TaskNameInTransitionVisibility` | `Soneta.Business.Db.TaskNameVisibility` | widoczność nazwy zadania docelowego przy wyborze tranzycji (skrót: `HideTaskNameInTransitionComboBox: bool`) |

**Snippet:**

```csharp
using (var session = login.CreateSession(readOnly: false, config: true, name: "Definicja Engine"))
{
    using (var t = session.Logout(editMode: true))
    {
        // Definicja kodowa (Engine) — osobna klasa, DefinitionType ustawia konstruktor:
        var engineDef = session.AddRow(new WFDefinitionExtend());
        engineDef.Symbol = "ENG";
        engineDef.Name = "Proces kodowy";

        // Definicja standardowa — sposób edycji można podnieść z Simple:
        var stdDef = session.AddRow(new WFDefinition());
        stdDef.Symbol = "STD";
        stdDef.Name = "Proces diagramowy";
        stdDef.EditType = EditTypeEnum.Advanced;   // Soneta.Workflow.Enums

        t.Commit();
    }
    session.Save();
}
```

**Pułapki:**
- **Zmiana trybu Standard↔Engine na istniejącej definicji nie jest dostępna w publicznym
  kontrakcie** — `DefinitionType` ustawia konstruktor klasy; w UI istnieje wewnętrzna czynność
  konwersji (worker `internal`). Programowo wybierz właściwą klasę przy tworzeniu
  (`WFDefinition` vs `WFDefinitionExtend`).
- `IsDiagramEditedInHtml` da się ustawić **tylko na definicji wdrożonej** (`IsDeployed == true`)
  albo w trakcie kopiowania — w trybie modelowania setter rzuca wyjątek z komunikatem o trybie
  wdrożenia. Nie przełączaj tego pola „na zapas" na świeżej definicji.
- `EngineCode` to kod kompilowany przez platformę (`AlgorithmColumn`/`ICodeEditorSource`) —
  traktuj jak każdy algorytm w konfiguracji (safe-code: brak efektów ubocznych, krótkie transakcje).
- Czynność „Tryb wdrożeniowy" widoczna w menu definicji to przełączenie `IsDeployed` (WORKFLOW-A7),
  a nie zmiana `EditType`.

### WORKFLOW-A3 — Role procesowe (tory diagramu) (★)

**Cel:** zdefiniować role procesowe definicji — tory (swimlane) diagramu wskazujące, kto wykonuje
zadania węzłów: operator, rola operatora, element struktury organizacyjnej, użytkownik web lub
wykonawca wyliczany algorytmem.

**Mechanizm:** rolę tworzy konstruktor `new WFProcessRole(wfDefinition)` (wiąże ją z definicją);
`OnAdded` automatycznie dokłada nowy tor do `SerializedDiagram`. Pierwsza, domyślna rola powstaje
razem z definicją (WORKFLOW-A1). Kolekcja: `WFDefinition.ProcessRoles: LpSubTable<WFProcessRole>`
(porządek po `Lp`).

**Pola i typy (`Soneta.Workflow.Config.WFProcessRole`, tabela `WFProcessRoles`, konfiguracyjna):**

| Pole | Typ | Opis |
|---|---|---|
| `Name` | `string` | nazwa roli procesowej (etykieta toru) |
| `WorkflowDefinition` | `Soneta.Workflow.Config.WFDefinition` | definicja-właściciel (ustawiana konstruktorem) |
| `ExecutorType` | `Soneta.Business.Db.WFProcessRoleExecutorType` | rodzaj wykonującego: `TaskExecutor` („Wskazany na zadaniu"), `Operator`, `Role`, `TaskUser` („Użytkownik web"), `Custom` (algorytm jednego wykonawcy), `MultitaskAlgorithm`, `MultitaskRecipientsList` |
| `Operator` | `Soneta.Business.App.Operator` | operator (gdy `ExecutorType = Operator`) |
| `RoleGuid` / `RoleName` | `System.Guid` / `string` | rola operatorów; `RoleName` (setter) mapuje nazwę istniejącej roli na `RoleGuid` |
| `Node` | `Soneta.Core.ElementStrukturyOrganizacyjnej` | węzeł struktury organizacyjnej |
| `OrgStructure` | `Soneta.Core.StrukturaOrganizacyjna` | struktura organizacyjna |
| `TaskUser` | `Soneta.Business.ITaskUser` (iface-ref) | użytkownik web — rekord `Operator`, `Pracownik`, `KontaktOsoba`, … |
| `SetExecutorByProcessRoleCode`, `GetTaskUsersByProcessRoleCode` | `Soneta.Business.Db.AlgorithmColumn` | algorytmy wyznaczania wykonawcy/wykonawców (warianty `Custom`/`Multitask…`) |
| `Lp` | `int` | liczba porządkowa roli na definicji |

**Snippet:**

```csharp
using (var session = login.CreateSession(readOnly: false, config: true, name: "Role procesowe"))
{
    var definicja = session.GetWorkflow().WFDefs.WgSymbolu["URLOP"];

    using (var t = session.Logout(editMode: true))
    {
        // Konstruktor wiąże rolę z definicją; OnAdded dokłada tor na diagram.
        var rola = session.AddRow(new WFProcessRole(definicja));
        rola.Name = "Przełożony";
        rola.ExecutorType = WFProcessRoleExecutorType.Role;     // wykonawca = rola operatorów
        rola.RoleName = "Kierownicy";                           // mapuje nazwę roli na RoleGuid

        var rolaOperatora = session.AddRow(new WFProcessRole(definicja));
        rolaOperatora.Name = "Kadrowa";
        rolaOperatora.ExecutorType = WFProcessRoleExecutorType.Operator;
        // UWAGA: setter Operator asertuje Session==value.Session — wiersz operatora musi
        // pochodzić z TEJ sesji konfiguracyjnej. Login.Operator (inna sesja) rzuca AssertException.
        // W sesji konfiguracyjnej przepnij wiersz: session.Get(session.Login.Operator).
        rolaOperatora.Operator = session.Get(session.Login.Operator);   // konkretny operator

        t.Commit();
    }
    session.Save();

    // Odczyt ról definicji (porządek po Lp):
    foreach (WFProcessRole r in definicja.ProcessRoles)
    {
        // r.Name, r.ExecutorType, r.Operator, r.RoleName
    }
}
```

**Pułapki:**
- **Definicja musi mieć co najmniej jedną rolę** — usunięcie ostatniej roli procesowej rzuca
  wyjątek. Domyślnej roli z `OnAdded` nie kasuj „na czysto" przed dodaniem własnej.
- `RoleName` to property pomostowe: setter szuka **istniejącej** roli operatorów po nazwie i wpisuje
  jej `Guid` do `RoleGuid`; nieznana nazwa ⇒ `RoleGuid = Guid.Empty` (bez wyjątku!) — po ustawieniu
  zweryfikuj `RoleGuid != Guid.Empty`.
- `TaskUser` to referencja interfejsowa (`ITaskUser`) — wskazuje rekord jednej z tabel
  implementujących (`Operator`, `Pracownik`, `KontaktOsoba`, …); pobieraj istniejące rekordy.
- Wybierz pole zgodne z `ExecutorType` — ustawienie `Operator` przy `ExecutorType = Role` nic nie da;
  to `ExecutorType` decyduje, które źródło wykonawcy jest brane pod uwagę przy tworzeniu `Task`ów.
- Przypisanie wykonawcy do **węzła** (która rola obsługuje który węzeł) konfiguruje się na
  `TaskDefinition` — patrz WORKFLOW02.

### WORKFLOW-A4 — Projektowanie diagramu procesu

**Cel:** zrozumieć, czym jest diagram definicji i jak powstaje — żeby świadomie budować definicje
programowo bez ręcznego rysowania.

**Mechanizm:** układ graficzny diagramu jest przechowywany w polu
`WFDefinition.SerializedDiagram: Soneta.Business.MemoText` jako **JSON** (lista figur: tory,
węzły, strzałki tranzycji, z `Guid`ami obiektów konfiguracyjnych). Diagram jest **warstwą
prezentacji** — platforma utrzymuje go automatycznie: dodanie definicji tworzy tor główny
(WORKFLOW-A1), dodanie `WFProcessRole` dokłada tor (WORKFLOW-A3), dodanie `TaskDefinition` /
`WFTransition` dokłada figurę węzła / strzałkę (WORKFLOW02/03). Interaktywne projektowanie
(przesuwanie figur, rysowanie przejść) odbywa się w designerze UI — desktopowym lub HTML
(`IsDiagramEditedInHtml`).

**Pola i typy:**

| Pole | Typ | Opis |
|---|---|---|
| `SerializedDiagram` | `Soneta.Business.MemoText` | JSON figur diagramu (tylko prezentacja; logika siedzi w `TaskDefs`/`Transitions`) |
| `IsDiagramEditedInHtml` | `bool` | edycja diagramu w designerze HTML (patrz pułapka w WORKFLOW-A2) |

Podgląd realizacji uruchomionego procesu na diagramie udostępnia publiczny worker
`Soneta.Workflow.Workers.DiagramDesignerProgressWorker` (czynność „Podgląd realizacji procesu"
na `WFWorkflow`) — to czynność UI (zwraca obiekt designera), nie operacja na danych.

**Snippet (odczyt — diagram jako dane diagnostyczne):**

```csharp
var definicja = session.GetWorkflow().WFDefs.WgSymbolu["URLOP"];

// Surowy JSON diagramu — np. do diagnostyki / porównań wersji definicji:
string diagramJson = definicja.SerializedDiagram;
bool maDiagram = !string.IsNullOrEmpty(diagramJson);

// Struktura logiczna procesu (to ona jest źródłem prawdy, nie JSON):
foreach (TaskDefinition wezel in definicja.TaskDefs) { /* wezel.Name, wezel.IsStart */ }
foreach (WFTransition tranzycja in definicja.Transitions) { /* tranzycja.Name, Source, Target */ }
```

**Pułapki:**
- **Nie edytuj `SerializedDiagram` ręcznie** — JSON zawiera `Guid`y obiektów konfiguracyjnych;
  niespójność diagramu z `TaskDefs`/`Transitions` psuje designer. Buduj proces przez obiekty
  (`TaskDefinition`, `WFTransition`, `WFProcessRole`) — figury dokładają się same.
- Logika procesu (węzły, przejścia, warunki) **nie siedzi** w diagramie — silnik czyta wyłącznie
  `TaskDefs`/`Transitions`; pusty/uszkodzony diagram nie zatrzyma procesu, ale uniemożliwi edycję
  graficzną.
- Workery designera diagramu definicji (otwarcie edytora) to czynności UI — nie są drogą do
  programowej budowy procesu.

### WORKFLOW-A5 — Kopiowanie definicji procesu (★)

**Cel:** utworzyć kopię całej definicji procesu (węzły, tranzycje, role, opcjonalnie schematy
generowania, definicje kreatorów, komentarze) z nowymi `Guid`ami — np. wariant procesu albo kopia
robocza do modelowania.

**Mechanizm:** publiczny worker `Soneta.Workflow.Workers.WFDefinitionCopyWorker` (czynność
„Kopiuj definicję procesu" na liście definicji). Kopiuje przez serializację XML: nadaje nowe
`Guid`y wszystkim obiektom (definicja, węzły, tranzycje, role, powiadomienia), uzgadnia je w
diagramie, wymusza unikalność `Symbol`/nazw węzłów i **zeruje `IsDeployed`** na kopii (kopia
startuje w trybie modelowania).

**Parametry (`WFDefinitionCopyWorker.WFDefinitionCopyWorkerParams`):**

| Parametr | Typ | Opis |
|---|---|---|
| `CopyOgSchemas` | `bool` | kopiuj schematy generowania obiektów (`OGSchema`) |
| `CopyWizardDefs` | `bool` | kopiuj definicje kreatorów |
| `CopyWFItemDescriptions` | `bool` | kopiuj komentarze diagramu |
| `UpdateRights` | `WFDefinitionCopyWorker.UpdateRightsMode` | prawa dostępu: `Nothing` / `Delete` / `Move` / `Copy` (domyślnie `Copy` — te same prawa na kopii) |

**Snippet:**

```csharp
using (var session = login.CreateSession(readOnly: false, config: true, name: "Kopia definicji WF"))
{
    var definicja = session.GetWorkflow().WFDefs.WgSymbolu["URLOP"];

    var cx = Context.Empty.Clone(session);
    cx.Set(definicja);                                   // parametry workera czytają definicję z Context

    var worker = new WFDefinitionCopyWorker
    {
        Row = definicja,
        Params = new WFDefinitionCopyWorker.WFDefinitionCopyWorkerParams(cx)
        {
            CopyOgSchemas = true,
            CopyWizardDefs = true,
            CopyWFItemDescriptions = true,
            UpdateRights = WFDefinitionCopyWorker.UpdateRightsMode.Copy
        }
    };

    WFDefinition kopia = worker.CopyWfDefinition();      // worker sam otwiera transakcję i robi Commit
    session.Save();

    // kopia.Guid != definicja.Guid; kopia.IsDeployed == false (tryb modelowania)
}
```

**Pułapki:**
- Czynność jest aktywna tylko w **sesji konfiguracyjnej edycyjnej**
  (`IsEnabledCopyWfDefinition: session.IsConfig && !session.ReadOnly`), a widoczna przy licencji
  modułu BPM — wywołanie z sesji operacyjnej nie zadziała.
- `CopyWfDefinition()` **sam zarządza transakcją** (wewnętrzny `Logout(true)` + `Commit`) — nie
  opakowuj go we własny `Logout`; po wywołaniu wystarczy `session.Save()`.
- Kopia ma `IsDeployed == false` niezależnie od źródła — przed użyciem produkcyjnym wymaga
  wdrożenia (WORKFLOW-A7).
- Worker wymusza unikalność `Symbol` (dopisek przy duplikacie) — nie zakładaj, że kopia ma
  identyczny `Symbol` jak źródło; odszukaj ją po zwróconej referencji/`Guid`.
- Prawa dostępu do definicji są kopiowane wg `UpdateRights` — wariant `Move` przenosi prawa ze
  źródła (źródło zostaje bez praw); wybieraj świadomie.

### WORKFLOW-A6 — Import / eksport definicji procesu

**Cel:** przenieść definicję procesu między bazami: zapisać definicję (z węzłami, tranzycjami,
rolami, schematami) do pliku XML i wczytać ją w innej bazie.

**Warianty:**

| Wariant | Worker / czynność | Wynik |
|---|---|---|
| Eksport pojedynczej definicji | `SerializeWFDefinitionWorker.Eksport()` („Zapisz zaznaczoną definicję do pliku") | `NamedStream` `<Nazwa>.dbinit.xml` |
| Eksport pełnej konfiguracji | `WorkflowExportWorker.ExportFullConfiguration()` („Eksportuj pełną konfigurację") | `NamedStream` XML — definicja + definicje cech, rekordy zarządcy (`DbTupleDefinition`), pluginy |
| Import pełnej konfiguracji | `WorkflowImportWorker.ImportFullConfiguration()` („Importuj pełną konfigurację") | czynność interaktywna (`QueryContextInformation` pyta o plik i strategię) |

**Parametry istotne:** `SerializeWFDefinitionWorker.WorkerParams`: `PlugIns: bool` (dołącz
pluginy), `RemoveProject: bool` (usuń odwołania do projektu kompilacji — zalecane przy przenoszeniu
między bazami) + parametry wspólne `CopyOgSchemas`/`CopyWizardDefs`/`CopyWFItemDescriptions`.
`WorkflowImportWorker.WorkflowImportWorkerParams`: `ImportStrategy:
ImportStrategyMode` (`DeleteAndAdd` — usuń istniejącą i dodaj nową, `Swap` — podmień, `Copy` —
utwórz kopię) oraz `UpdateRights` (jak w WORKFLOW-A5).

**Snippet (eksport definicji do pliku):**

```csharp
using (var session = login.CreateSession(readOnly: false, config: true, name: "Eksport WF"))
{
    var definicja = session.GetWorkflow().WFDefs.WgSymbolu["URLOP"];

    var cx = Context.Empty.Clone(session);
    cx.Set(definicja);

    var worker = new SerializeWFDefinitionWorker
    {
        Row = definicja,
        Params = new SerializeWFDefinitionWorker.WorkerParams(cx)
        {
            RemoveProject = true        // bez odwołań do projektu kompilacji źródłowej bazy
        }
    };

    var wynik = (NamedStream)worker.Eksport();           // <Nazwa>.dbinit.xml
    File.WriteAllBytes(Path.Combine(Path.GetTempPath(), wynik.Name), wynik.GetData());
}
```

**Pułapki:**
- **Import jest czynnością interaktywną** — `ImportFullConfiguration()` zwraca
  `QueryContextInformation` oczekujące `NamedStream` (plik) i ewentualnie parametrów strategii;
  programowe wywołanie wymaga obsługi potoku QueryContext (patrz worker-extender.md /
  action-result.md). Nie ma prostej publicznej metody „importuj z pliku jedną linijką".
- Gdy w bazie istnieje już definicja o tym samym `Guid`, import pyta o strategię
  (`DeleteAndAdd`/`Swap`/`Copy`); dla nowego `Guid` definicja jest dodawana wprost.
- Czynności eksportu działają na **liście definicji** (`ActionMode.OnlyTable`) i przy licencji BPM;
  `Eksport()` wewnętrznie otwiera transakcję — nie opakowuj własną.
- Eksport pojedynczej definicji nie zawiera definicji cech ani rekordów zarządcy — do pełnego
  przeniesienia środowiska używaj pary „Eksportuj/Importuj pełną konfigurację".
- Po imporcie definicja wymaga zapisania sesji (`session.Save()`) i zwykle wdrożenia (WORKFLOW-A7).

### WORKFLOW-A7 — Wdrożenie definicji (tryb modelowania → produkcja) (★)

**Cel:** przełączyć definicję z trybu modelowania w tryb wdrożenia (`IsDeployed = true`), od którego
silnik dopuszcza uruchamianie procesów (w tym start automatyczny i triggery).

**Mechanizm:** ustawienie pola `IsDeployed` na zapisanej definicji jest **jednokierunkowe** —
z trybu wdrożenia nie wraca się do modelowania (setter rzuca wyjątek wtórnej zmiany); drogą do
poprawek jest kopia definicji (WORKFLOW-A5), która zawsze startuje z `IsDeployed == false`.
Czynność menu „Tryb wdrożeniowy" na definicji robi dokładnie to samo (`IsDeployed = true`).

**Pola i typy:**

| Pole | Typ | Opis |
|---|---|---|
| `IsDeployed` | `bool` | `false` = modelowanie (definicję można przebudowywać), `true` = wdrożona (produkcja) |
| `Locked` | `bool` | blokada definicji — wyłącza ją z użycia bez cofania wdrożenia |

**Snippet:**

```csharp
using (var session = login.CreateSession(readOnly: false, config: true, name: "Wdrożenie WF"))
{
    var definicja = session.GetWorkflow().WFDefs.WgSymbolu["URLOP"];

    using (var t = session.Logout(editMode: true))
    {
        definicja.IsDeployed = true;     // jednokierunkowe: wdrożonej definicji nie cofniesz
        t.Commit();
    }
    session.Save();
}
```

**Pułapki:**
- **Operacja jest nieodwracalna** — ponowna zmiana `IsDeployed` na zapisanej, wdrożonej definicji
  rzuca wyjątek wtórnej zmiany pola („Tryb wdrożenia"). Do dalszego modelowania utwórz kopię
  (WORKFLOW-A5).
- Start automatyczny i triggery filtrują definicje po `IsDeployed && !Locked` (oraz `!Locked`
  węzła startowego) — niewdrożona definicja nie wystartuje automatycznie; przed wdrożeniem
  upewnij się, że istnieje węzeł startowy (`definicja.GetStartPoint()` nie rzuca wyjątku).
- Wdrożenie nie waliduje kompletności procesu — definicja bez tranzycji wyjściowych „utknie" na
  pierwszym węźle; zbuduj i sprawdź przebieg (WORKFLOW02/03) przed `IsDeployed = true`.
- Dopiero po wdrożeniu można przełączyć `IsDiagramEditedInHtml` (WORKFLOW-A2).
- Tymczasowe wyłączenie wdrożonej definicji z użycia: `Locked = true` (bez cofania wdrożenia).
