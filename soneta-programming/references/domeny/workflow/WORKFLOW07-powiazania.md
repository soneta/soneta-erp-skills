# WORKFLOW07 — Powiązania zadania z dokumentami i obiektami

> Wspólne fakty o typie, podstawowe typy i szablon wzorca: [../workflow.md](../workflow.md).

> **Model „Parent + LinkedObjects".** Każde zadanie (`Soneta.Business.Db.Task`) wskazuje **jeden
> obiekt główny** — `Task.Parent: IGuidedRow` (dokument, kontrahent, pracownik… dowolny wiersz
> guided). Dodatkowo zadanie może być „widoczne" z wielu innych obiektów — służy do tego tabela
> `TaskLinkedObjs` (caption „Wielowidoczność tasków"), której wiersze
> (`Soneta.Business.Db.TaskLinkedObj`) spinają zadanie (`Task`) z obiektem
> (`LinkedObject: IGuidedRow`). Wpis dla obiektu głównego ma `IsParent = true` i jest utrzymywany
> automatycznie. Nawigacja działa w obie strony: z zadania do obiektów (`Task.Parent`,
> `Task.LinkedObjects`) i z obiektu do zadań (klucze `Tasks.WgParent[row]`,
> `TaskLinkedObjs.WgLinkedObject[row]`).

---

### WORKFLOW-G1 — Znajdowanie dokumentu powiązanego z zadaniem (★)

**Cel:** z poziomu zadania (`Task`) dotrzeć do dokumentu/obiektu biznesowego, którego zadanie
dotyczy — oraz odwrotnie: dla dokumentu znaleźć jego zadania.

**Warianty:**

| Wariant | Droga | Wynik |
|---|---|---|
| Obiekt główny zadania | `task.Parent` | `IGuidedRow` (jeden, może być `null`) |
| Wszystkie obiekty powiązane | `task.LinkedObjects` | `SubTable<TaskLinkedObj>` (wpis z `IsParent=true` to obiekt główny) |
| Zadania dla obiektu (odwrotnie) | `business.Tasks.WgParent[row]` | `SubTable<Task>` — zadania, których `Parent == row` |
| Zadania „widoczne" z obiektu (odwrotnie) | `business.TaskLinkedObjs.WgLinkedObject[row]` | `SubTable<TaskLinkedObj>` — także powiązania dodatkowe |

**Pola i typy:**

| Pole | Typ | Uwaga |
|---|---|---|
| `Task.Parent` | `Soneta.Business.IGuidedRow` | obiekt główny; rzutuj na konkretny typ (`is DokumentHandlowy dok`) |
| `Task.LinkedObjects` | `Soneta.Business.SubTable<Soneta.Business.Db.TaskLinkedObj>` | wszystkie powiązania zadania |
| `TaskLinkedObj.LinkedObject` | `Soneta.Business.IGuidedRow` | obiekt podpięty do zadania |
| `TaskLinkedObj.IsParent` | `bool` | `true` = wpis odpowiada `Task.Parent` |
| `TaskLinkedObj.Task` | `Soneta.Business.Db.Task` | zadanie, do którego podpięto obiekt |
| `Tasks.WgParent` | klucz: indeksery `[IGuidedRow]`, `[IGuidedRow, DateTime]` | serwerowe wyszukanie zadań wiersza |
| `TaskLinkedObjs.WgLinkedObject` | klucz: `[IGuidedRow]`, `[IGuidedRow, Task]` | serwerowe wyszukanie powiązań wiersza |
| `TaskLinkedObjs.WgTask` | klucz: `[Task]` | powiązania konkretnego zadania (to samo co `task.LinkedObjects`) |

**Snippet:**

```csharp
var business = session.GetBusiness();

// Zadanie wejściowe — np. pierwsze zadanie zalogowanego operatora z ustawionym Parent
// (pełne wzorce wyszukiwania zadań operatora: WORKFLOW05).
Task task = business.Tasks.WgTaskUser[session.Login.Operator]
    .FirstOrDefault(t => t.Parent != null);

// 1) Obiekt główny zadania:
IGuidedRow parent = task.Parent;
if (parent is DokumentHandlowy dok)
{
    // pracujemy na dokumencie handlowym powiązanym z zadaniem
}

// 2) Wszystkie obiekty powiązane (główny + dodatkowe):
foreach (TaskLinkedObj link in task.LinkedObjects)
{
    IGuidedRow obj = link.LinkedObject;   // obiekt
    bool isMain    = link.IsParent;       // true = to jest Task.Parent
}

// 3) Odwrotnie — zadania powiązane z konkretnym dokumentem/obiektem:
var row = (IGuidedRow)session.GetCRM().Kontrahenci.WgKodu["ABC"];

foreach (Task t in business.Tasks.WgParent[row])                  // row jest obiektem głównym
{ /* t.Name, t.Progress, t.WFWorkflow */ }

foreach (TaskLinkedObj link in business.TaskLinkedObjs.WgLinkedObject[row])  // także powiązania dodatkowe
{ /* link.Task — zadanie widoczne z tego wiersza */ }
```

**Pułapki:**
- `Parent` to `IGuidedRow` — interfejs. Zanim użyjesz pól dokumentu, **rzutuj wzorcem `is`**
  (`parent is DokumentHandlowy dok`); typ obiektu zależy od definicji węzła, nie zakładaj go na sztywno.
- `Tasks.WgParent[row]` znajduje tylko zadania, których `Parent == row`. Zadania powiązane z wierszem
  **dodatkowo** (wielowidoczność) znajdziesz wyłącznie przez `TaskLinkedObjs.WgLinkedObject[row]` —
  to dwa różne zapytania.
- Klucze `Wg…` filtrują serwerowo — używaj ich zamiast iterowania całej tabeli `Tasks`
  (patrz `rowcondition.md`).
- Wpisy `TaskLinkedObj` dla zadań workflow utrzymuje silnik (kalkulator zadania przy
  `Task.Recalculate()`); odczyt jest zawsze bezpieczny, ale nie modyfikuj ich ręcznie —
  do aktualizacji służy `Task.UpdateLinkedObjects` (WORKFLOW-G3).

---

### WORKFLOW-G2 — Nawigacja z zadania do procesu i jego definicji (★)

**Cel:** z egzemplarza zadania dotrzeć do procesu (instancji `WFWorkflow`), węzła
(`TaskDefinition`), definicji procesu (`WFDefinition`) oraz po historii procesu (pierwsze /
poprzednie zadanie, poprzedni obiekt).

**Warianty:**

| Skąd → dokąd | Droga | Typ wyniku |
|---|---|---|
| Zadanie → proces | `task.WFWorkflow` | `Soneta.Business.IWFWorkflow` (konkretnie: `Soneta.Workflow.WFWorkflow`) |
| Zadanie → węzeł | `task.Definition` | `Soneta.Business.Db.TaskDefinition` |
| Węzeł → definicja procesu | `task.Definition.WFDefinition` | `Soneta.Business.IWFDefinition` (konkretnie: `Soneta.Workflow.Config.WFDefinition`) |
| Proces → definicja (typowane) | `workflow.WorkflowDefinition` | `Soneta.Workflow.Config.WFDefinition` |
| Proces → zadania | `workflow.AllTasks` / `LiveTasks` / `ActiveTasks` | `SubTable<Task>` / `IEnumerable<Task>` |
| Historia: pierwsze zadanie procesu | `task.WFFirstTask` | `Task` |
| Historia: zadanie/tranzycja poprzedzające | `task.WFPreviousTask`, `task.WFPreviousTransition` | `Task`, `IWFTransition` |
| Historia: obiekt poprzedniego zadania | `task.WFPreviousParent` | `IGuidedRow` |

**Pola i typy:**

| Pole | Typ | Uwaga |
|---|---|---|
| `Task.WFWorkflow` | `Soneta.Business.IWFWorkflow` | `null` dla zadań spoza workflow (zadania „luźne") |
| `Task.Definition` | `Soneta.Business.Db.TaskDefinition` | węzeł, z którego powstało zadanie |
| `TaskDefinition.WFDefinition` | `Soneta.Business.IWFDefinition` | rzutuj na `Soneta.Workflow.Config.WFDefinition` |
| `WFWorkflow.WorkflowDefinition` | `Soneta.Workflow.Config.WFDefinition` | typowana definicja — bez rzutowania |
| `WFWorkflow.AllTasks` | `Soneta.Business.SubTable<Task>` | wszystkie zadania procesu (z historią) |
| `WFWorkflow.ActiveTasks`, `LiveTasks` | `IEnumerable<Soneta.Business.Db.Task>` | tylko aktywne / „żywe" |
| `Task.WFFirstTask`, `Task.WFPreviousTask` | `Soneta.Business.Db.Task` | tylko dla zadań workflow |
| `Task.WFPreviousParent` | `Soneta.Business.IGuidedRow` | obiekt powiązany z zadaniem poprzedzającym |
| `Task.WFPreviousParentsByType` | `List<Soneta.Business.IGuidedRow>` | wiersze wcześniejszych zadań wg typu danych z definicji |

**Snippet:**

```csharp
var business = session.GetBusiness();

// Zadanie workflow zalogowanego operatora (zadania „luźne" mają WFWorkflow == null):
Task task = business.Tasks.WgTaskUser[session.Login.Operator]
    .FirstOrDefault(t => t.WFWorkflow != null);

// Proces (instancja) — interfejs rzutujemy na pełny typ z Soneta.Workflow:
var workflow = (WFWorkflow)task.WFWorkflow;
bool isClosed = workflow.IsClosed;                    // status procesu
Date started  = workflow.DateFrom;                    // data rozpoczęcia

// Węzeł (definicja zadania) i definicja całego procesu:
TaskDefinition node = task.Definition;
var wfDefinition = workflow.WorkflowDefinition;       // Soneta.Workflow.Config.WFDefinition
// równoważnie z węzła: var wfDefinition2 = (WFDefinition)node.WFDefinition;

// Pozostałe zadania tego procesu:
foreach (Task t in workflow.AllTasks)
{ /* t.Definition.Name, t.Progress */ }

// Nawigacja po historii procesu:
Task first        = task.WFFirstTask;                 // pierwsze zadanie procesu
Task previous     = task.WFPreviousTask;              // zadanie poprzedzające
IGuidedRow prevParent = task.WFPreviousParent;        // obiekt poprzedniego zadania
```

**Pułapki:**
- `Task.WFWorkflow` i `TaskDefinition.WFDefinition` są typowane **interfejsami**
  (`IWFWorkflow`, `IWFDefinition`) zadeklarowanymi w `Soneta.Business` — pełne API procesu
  (`AllTasks`, `WorkflowDefinition`, `ManagingRow`…) wymaga rzutowania na typy z `Soneta.Workflow`
  (referencja do `Soneta.Workflow.dll`).
- Właściwości `WFFirstTask`, `WFPreviousTask`, `WFPreviousParent`, `WFPreviousParents…` działają
  **tylko dla zadań workflow** — dla zadania bez procesu zwracają `null`/puste.
- Zadanie informacyjne/powiadomienie też jest wierszem `Tasks` — filtruj po `WFWorkflow != null`,
  jeśli interesują Cię wyłącznie zadania procesowe.
- W UI tę nawigację realizuje czynność **„Przejdź do procesu Workflow"**
  (`Soneta.Workflow.Workers.TaskGoToWorkflowWorker`, akcja menu na `Task`) — jest widoczna tylko,
  gdy zalogowany operator jest opiekunem procesu (`WFWorkflow.Operator`). W kodzie biznesowym
  używaj bezpośrednio właściwości zadania, jak wyżej.
- `WFWorkflow.AllTasks` zawiera również zadania historyczne (zrealizowane) — do zadań „w toku"
  używaj `ActiveTasks`/`LiveTasks`.

---

### WORKFLOW-G3 — Aktualizacja powiązanych obiektów zadania (★)

**Cel:** ustawić/odświeżyć listę obiektów dodatkowo powiązanych z zadaniem (wielowidoczność) —
tak, by zadanie było widoczne np. z poziomu kontrahenta i projektu, a nie tylko z dokumentu
głównego.

**Mechanizm (kluczowy):** jedyną publiczną drogą jest metoda
`Task.UpdateLinkedObjects(IEnumerable<GuidedRow> objs)`. Metoda synchronizuje tabelę
`TaskLinkedObjs` do stanu: **`Parent` + przekazane obiekty** — dodaje brakujące wpisy, usuwa
nadmiarowe, pomija duplikaty, wartości `null` i obiekty równe `Parent`. W procesach workflow
wywołuje ją silnik podczas `Task.Recalculate()` (kalkulator węzła dostarcza listę obiektów);
ręczne wywołanie ma sens dla zadań tworzonych z kodu lub w algorytmach definicji.

**Pola i typy:**

| Element | Typ / sygnatura | Uwaga |
|---|---|---|
| `Task.UpdateLinkedObjects` | `void UpdateLinkedObjects(IEnumerable<Soneta.Business.GuidedRow> objs)` | `objs` może być `null` → zostaje tylko wpis dla `Parent` |
| `Task.Parent` | `Soneta.Business.IGuidedRow` | **musi być ustawiony** — inaczej `NotSupportedException` |
| `Task.LinkedObjects` | `SubTable<Soneta.Business.Db.TaskLinkedObj>` | stan po aktualizacji (zawiera wpis `IsParent=true`) |

**Snippet:**

```csharp
var business = session.GetBusiness();
var crm = session.GetCRM();

// Zadanie z ustawionym obiektem głównym (Parent) — np. zadanie operatora:
Task task = business.Tasks.WgTaskUser[session.Login.Operator]
    .FirstOrDefault(t => t.Parent != null);

var kontrahent = crm.Kontrahenci.WgKodu["ABC"];

using (var t = session.Logout(editMode: true))
{
    // Po wywołaniu zadanie jest powiązane z: Parent (zawsze) + kontrahent.
    // Wcześniejsze dodatkowe powiązania, których nie ma na liście, zostaną usunięte.
    task.UpdateLinkedObjects(new GuidedRow[] { kontrahent });

    t.Commit();
}
session.Save();

// Weryfikacja:
foreach (TaskLinkedObj link in task.LinkedObjects)
{ /* link.LinkedObject, link.IsParent */ }
```

**Pułapki:**
- `Parent == null` → metoda rzuca `NotSupportedException`. Powiązania dodatkowe istnieją zawsze
  „obok" obiektu głównego, nie zamiast niego.
- Metoda jest **synchronizacją pełnej listy**, nie „dodaniem": obiekty nieobecne w `objs` (poza
  `Parent`) zostaną odpięte. `UpdateLinkedObjects(null)` czyści wszystkie powiązania dodatkowe.
- Parametr to `IEnumerable<GuidedRow>` (klasa, nie interfejs `IGuidedRow`) — przekazuj konkretne
  wiersze; `null`-e i duplikaty na liście są ignorowane.
- **Nie twórz wierszy `TaskLinkedObj` ręcznie** — publiczny konstruktor
  `TaskLinkedObj(Task, IGuidedRow, bool)` jest oznaczony `[Obsolete]` i będzie usunięty;
  `UpdateLinkedObjects` to jedyna wspierana droga.
- Dla zadań workflow listę powiązań nadpisze kolejne `Task.Recalculate()` (silnik wyliczy ją
  z algorytmu węzła) — trwałą regułę powiązań definiuj w algorytmie definicji zadania,
  a nie jednorazowo w kodzie.
- Operacja modyfikuje dane — wymaga transakcji (`session.Logout(editMode: true)` + `Commit()`)
  i `session.Save()`; patrz `safe-code.md`.

---

### WORKFLOW-G4 — Odczyt informacji o workflow dla dowolnego wiersza (★)

**Cel:** dla dowolnego obiektu biznesowego (`GuidedRow` — dokument, kontrahent, pracownik…)
odpowiedzieć na pytania: jakie zadania go przetwarzają, w jakich procesach uczestniczy
i czy jest przez proces zablokowany do edycji.

**Warianty:**

| Pytanie | Droga publiczna | Wynik |
|---|---|---|
| Aktywne zadania wiersza | `business.Tasks.WgParent[row]` + filtr `IsActiveProgress` | `Task[]` |
| Procesy przetwarzające wiersz | z zadań: `t.WFWorkflow` (distinct) | `IWFWorkflow[]` |
| Blokada edycji przez proces | `BusTools.CheckIsReadOnlyForWorkflow(row)` | `bool` |
| Procesy, którymi wiersz **zarządza** | `workflow.WFWorkflows.WgManagingRow[row]` (gdy `row is IManagingRow`) | `SubTable<WFWorkflow>` |
| To samo w UI / na wydrukach | worker `Soneta.Workflow.Workers.RowWorkflowInfoWorker` | property `Zadania`, `Procesy`, `Zablokowany przez proces` |

**Pola i typy:**

| Element | Typ | Uwaga |
|---|---|---|
| `Tasks.WgParent[IGuidedRow]` | `Soneta.Business.SubTable<Task>` | zadania, których `Parent == row` |
| `Task.IsActiveProgress` | `bool` | zadanie aktywne (uwzględnia kalkulator) |
| `Task.WFWorkflow` | `Soneta.Business.IWFWorkflow` | proces zadania (`null` = zadanie luźne) |
| `Task.ParentWFWorkflows` | `Soneta.Business.SubTable` | procesy dla `Parent` zadania, których **opiekunem jest zalogowany operator** |
| `BusTools.CheckIsReadOnlyForWorkflow` | `static bool (Soneta.Business.GuidedRow)` | `Soneta.Business.BusTools` |
| `WFWorkflows.WgManagingRow[IManagingRow]` | `Soneta.Business.SubTable<Soneta.Workflow.WFWorkflow>` | procesy z `ManagingRow == row` |
| `RowWorkflowInfoWorker` | worker dla `Soneta.Business.GuidedRow` | property kalkulowane: `Tasks: List<Task>`, `Processes: List<IWFWorkflow>`, `IsLocked: bool` |

**Snippet:**

```csharp
var business = session.GetBusiness();
var workflow = session.GetWorkflow();

// Dowolny wiersz guided — tu kontrahent z bazy Demo:
GuidedRow row = session.GetCRM().Kontrahenci.WgKodu["ABC"];

// 1) Aktywne zadania przetwarzające wiersz:
var activeTasks = business.Tasks.WgParent[row]
    .Where(t => t.IsActiveProgress)
    .ToList();

// 2) Procesy, w których wiersz uczestniczy (przez aktywne zadania):
var processes = activeTasks
    .Where(t => t.WFWorkflow != null)
    .Select(t => t.WFWorkflow)
    .Distinct()
    .ToList();

// 3) Czy proces blokuje edycję wiersza:
bool isLocked = BusTools.CheckIsReadOnlyForWorkflow(row);

// 4) Procesy, którymi wiersz zarządza (ManagingRow — patrz WORKFLOW04):
if (row is IManagingRow managing)
{
    foreach (WFWorkflow p in workflow.WFWorkflows.WgManagingRow[managing])
    { /* p.Name, p.IsClosed, p.WorkflowDefinition */ }
}
```

**Pułapki:**
- Rozróżniaj trzy relacje wiersz↔workflow: **`Task.Parent`** (wiersz jest obiektem zadania),
  **`TaskLinkedObj.LinkedObject`** (wiersz dodatkowo powiązany — WORKFLOW-G1) i
  **`WFWorkflow.ManagingRow`** (wiersz zarządza całym procesem). To trzy różne zapytania
  o różnych wynikach.
- `Task.ParentWFWorkflows` filtruje procesy do tych, których **opiekunem jest zalogowany
  operator** — nie używaj jej jako „wszystkie procesy wiersza"; pełną listę daje przejście
  przez zadania (punkt 2 snippetu).
- `BusTools.CheckIsReadOnlyForWorkflow` zwraca `false`, gdy sesja nie ma przypisanego operatora
  (np. sesja techniczna) — wynik zależy od zalogowanego użytkownika, nie tylko od danych.
- Worker `RowWorkflowInfoWorker` jest zarejestrowany dla każdego `GuidedRow` — jego property
  (kategoria „Dodatkowe": *Zadania*, *Procesy*, *Zablokowany przez proces*) są dostępne jako pola
  na listach/wydrukach; w kodzie biznesowym używaj bezpośrednio kluczy jak w snippecie
  (worker robi dokładnie to samo). O mechanice workerów: `worker-extender.md`.
- Filtr `Where(...)` na `SubTable` z klucza działa już po serwerowym zawężeniu do wiersza —
  to akceptowalny wzorzec; nie iteruj natomiast całej tabeli `Tasks` (patrz `rowcondition.md`).
