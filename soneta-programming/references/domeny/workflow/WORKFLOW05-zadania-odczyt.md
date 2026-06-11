# WORKFLOW05 — Zadania operatora — odczyt i przegląd

> Wspólne fakty o typie, podstawowe typy i szablon wzorca: [../workflow.md](../workflow.md).

> **Model przypisania zadania.** Zadanie (`Soneta.Business.Db.Task`, tabela `Tasks`) może być
> przypisane na cztery sposoby, rozróżniane polem `OperatorRoleType: Soneta.Business.Db.OperatorRoleType`:
>
> | `OperatorRoleType` | Pole nośne na `Task` | Znaczenie |
> |---|---|---|
> | `Operator` | `Operator: Soneta.Business.App.Operator` | zadanie konkretnego operatora |
> | `Role` | `RoleGuid: System.Guid` | zadanie dla wszystkich operatorów z rolą |
> | `Node` | `Node: Soneta.Business.IElementStrukturyOrganizacyjnej` | zadanie węzła struktury organizacyjnej (i węzłów podrzędnych) |
> | `Other` | `TaskUser: Soneta.Business.ITaskUser` | inny właściciel (np. użytkownik zewnętrzny — `Pracownik`, `KontaktOsoba`, `Operator`, …) |
>
> Dodatkowo `TakeOverOperator` / `TakeOverITaskUser` wskazują, kto **przejął** zadanie roli/węzła
> (realizację przejęcia opisuje WORKFLOW-F5). „Aktualność" zadania wyznaczają pola:
> `Progress: TaskProgress` (`NonActive=0`, `Active=1`, `Waiting=2`, `Realized=3`, `Aborted=4`),
> `ValidFrom: Soneta.Types.Date` (od kiedy zadanie jest ważne) oraz `Notification: System.DateTime`
> (moment przypomnienia; `DateTime.MinValue` = brak przypomnienia).
>
> Tabela `Tasks` jest **operacyjna** (nie konfiguracyjna) — odczyt nie wymaga sesji konfiguracyjnej.

### WORKFLOW-E1 — Odczytanie aktualnych zadań dla operatora (★)

**Cel:** pobrać listę zadań, które „należą" do zalogowanego operatora — przypisanych do niego
wprost, przez rolę, przez węzeł struktury organizacyjnej lub przejętych — z zawężeniem do zadań
aktualnych (aktywnych, ważnych, z wymagalnym przypomnieniem).

**Warianty:**

| Wariant | Jak |
|---|---|
| Warunek serwerowy „moje zadania" (wszystkie sposoby przypisania) | `Tasks.IOrgStructureHelper.GetMyTasksCondition(session, Date.Today)` |
| Warunek dla wskazanych właścicieli (`ITaskUser[]`) | `Tasks.IOrgStructureHelper.GetTaskUsersCondition(taskUsers, date)` |
| Sprawdzenie pojedynczego zadania po stronie klienta | `Tasks.IsLoggedUserTask(task)` (statyczna) |
| Tylko zadania konkretnego operatora (bez ról/węzłów) | klucz `Tasks.WgOperator[operator]` |
| Tylko zadania danego właściciela `ITaskUser` | klucz `Tasks.WgTaskUser[taskUser]` (warianty z `TaskProgress`, `Notification`, `ValidFrom`) |

**Pola i typy:**

| Element | Typ / sygnatura | Uwagi |
|---|---|---|
| `session.GetBusiness().Tasks` | `Soneta.Business.Db.Tasks` | tabela zadań (`BusinessModule`) |
| `Tasks.IOrgStructureHelper` | publiczny interfejs zagnieżdżony w `Tasks` | serwis sesyjny: `session.GetRequiredService<Tasks.IOrgStructureHelper>()` (patrz `services.md`) |
| `GetMyTasksCondition(Session, Date)` | `→ Soneta.Business.RowCondition` | `Operator == zalogowany` OR `TakeOverITaskUser == zalogowany` OR `RoleGuid ∈ role operatora` OR `Node ∈ węzły operatora (z nadrzędnymi)` |
| `Tasks.IsLoggedUserTask(Task)` | `static bool` | ta sama logika dla pojedynczego wiersza |
| `Task.Progress` | `Soneta.Business.Db.TaskProgress` | aktualne = `Active` (ew. `Waiting`) |
| `Task.ValidFrom` | `Soneta.Types.Date` | zadanie ważne, gdy `ValidFrom <= Date.Today` |
| `Task.Notification` | `System.DateTime` | przypomnienie wymagalne, gdy `> DateTime.MinValue` i `<= DateTime.Now` |
| `Tasks.WgDefinition` / `WgOperator` / `WgTaskUser` | `Key<Task>` | klucze; indeksator `key[RowCondition]` zwraca przefiltrowany, enumerowalny `Key<Task>` |

**Snippet:**

```csharp
using Soneta.Business;
using Soneta.Business.Db;
using Soneta.Types;

var tasks = session.GetBusiness().Tasks;

// 1. Warunek serwerowy "moje zadania" (operator + role + węzły org. + przejęte)
var helper = session.GetRequiredService<Tasks.IOrgStructureHelper>();
RowCondition rc = helper.GetMyTasksCondition(session, Date.Today);

// 2. Zawężenie do zadań aktualnych (jak licznik powiadomień na "dzwonku")
rc &= new FieldCondition.Equal(nameof(Task.Progress), TaskProgress.Active);
rc &= new FieldCondition.Greater(nameof(Task.Notification), (DateTime)Date.MinValue);
rc &= new FieldCondition.LessEqual(nameof(Task.Notification), DateTime.Now);
rc &= new FieldCondition.LessEqual(nameof(Task.ValidFrom), Date.Today);

// 3. Odczyt przez klucz z warunkiem — filtrowanie wykonuje serwer (rowcondition.md)
foreach (Task task in tasks.WgDefinition[rc])
    Console.WriteLine($"{task.Name} | {task.Progress} | {task.ResponsibleName}");

// Alternatywa kliencka — weryfikacja pojedynczego wiersza:
Task pierwsze = tasks.WgDefinition[rc].GetFirst();
if (pierwsze != null && Tasks.IsLoggedUserTask(pierwsze))
{
    // zadanie należy do zalogowanego operatora
}
```

**Pułapki:**
- **Nie składaj warunku „moje zadania" ręcznie** z `Operator`/`RoleGuid` — pominiesz zadania
  przejęte (`TakeOverITaskUser`) i zadania węzłów struktury organizacyjnej (te wymagają rozwinięcia
  hierarchii węzłów, co robi `GetMyTasksCondition`).
- Serwis `Tasks.IOrgStructureHelper` jest rejestrowany przez moduł **Workflow** — w środowisku bez
  załadowanego `Soneta.Workflow` `GetRequiredService` rzuci wyjątek. W kodzie dodatku, który ma
  działać także bez Workflow, użyj `session.GetService<Tasks.IOrgStructureHelper>()` i obsłuż `null`.
- `Notification == DateTime.MinValue` oznacza **brak przypomnienia** — stąd dolne ograniczenie
  `Greater(Notification, Date.MinValue)`; bez niego lista „aktualnych" obejmie też zadania bez
  terminu przypomnienia. Jeśli chcesz *wszystkie aktywne* (nie tylko przypomnienia), pomiń warunki
  na `Notification`.
- `WgDefinition[rc]` zwraca `Key<Task>` — sortowanie pochodzi z klucza (tu: po `Definition`,
  `Start`). Dobierz klucz do potrzebnego porządku (`WgName`, `WgOperator`, …).
- Widoczność zadań innych operatorów ogranicza prawo proste „Zadania innych operatorów"
  (`Operator.AccessToOtherOperatorsTasks()`); kod działający „w imieniu" operatora z prawem
  `Denied` zobaczy mniej wierszy na listach UI.

### WORKFLOW-E2 — Przegląd zadań na panelu / pulpicie Workflow

**Cel:** odtworzyć w kodzie zawartość folderu **Panel Workflow** (sekcje „Powiadomienia"
i „Procesy") — czyli przypomnienia zalogowanego operatora oraz jego procesy.

**Warianty:**

| Wariant | Jak |
|---|---|
| Powiadomienia „Aktywne" (jak `NotificationsOptionsEnum.Aktywne`) | `tasks.GetRemaindersView(all: false)` |
| Powiadomienia „Wszystkie" (`Aktywne` + `Oczekujące`) | `tasks.GetRemaindersView(all: true)` |
| Tylko nieodczytane / odczytane | parametr `notificationRead: NotificationRead.NotRead / .Read` |
| Procesy operatora (aktywne / nieaktywne / wszystkie) | `WFWorkflows.GetGrantedView()` + warunki `Operator`, `IsClosed` |

**Pola i typy:**

| Element | Typ / sygnatura | Uwagi |
|---|---|---|
| `Tasks.GetRemaindersView(bool all, bool webUser = false, NotificationRead notificationRead = NotificationRead.All)` | `→ Soneta.Business.View` | widok powiadomień operatora (to samo źródło co sekcja „Powiadomienia" panelu) |
| `Tasks.GetRemaindersCount(bool all, bool webUser = false)` | `→ Tasks.RemindersCountResult` (`Count: int`, `LastTaskID: int`) | licznik „dzwonka" jednym zapytaniem agregującym |
| `NotificationRead` | enum `Soneta.Business.Db` | `All = 0`, `Read = 10`, `NotRead = 20` |
| `session.GetWorkflow().WFWorkflows` | `Soneta.Workflow.WFWorkflows` | `GetGrantedView()` — widok procesów z prawami |
| `WFWorkflow.IsClosed` | `bool` | proces zakończony |
| `WorkflowPanelViewInfo` | `Soneta.Workflow.UI.ViewInfos` | klasa kontekstowa panelu; zagnieżdżone enumy `NotificationsOptionsEnum` (`Aktywne`/`Wszystkie`) i `ProcessesOptionsEnum` (`Aktywne`/`Niektywne`/`Wszystkie`) |

**Snippet:**

```csharp
using Soneta.Business;
using Soneta.Business.Db;
using Soneta.Workflow;

var tasks = session.GetBusiness().Tasks;

// Licznik powiadomień (odpowiednik "dzwonka")
var licznik = tasks.GetRemaindersCount(all: false);
Console.WriteLine($"Aktywnych powiadomień: {licznik.Count}");

// Sekcja "Powiadomienia" panelu — tryb Wszystkie, tylko nieodczytane
View powiadomienia = tasks.GetRemaindersView(
    all: true,
    webUser: false,
    notificationRead: NotificationRead.NotRead);
foreach (Task task in powiadomienia)
    Console.WriteLine($"{task.Name} | przypomnienie: {task.Notification:yyyy-MM-dd HH:mm}");

// Sekcja "Procesy" panelu — aktywne procesy zalogowanego operatora
View procesy = session.GetWorkflow().WFWorkflows.GetGrantedView();
procesy.Condition &= new FieldCondition.Equal(nameof(WFWorkflow.Operator), session.Login.Operator);
procesy.Condition &= new FieldCondition.Equal(nameof(WFWorkflow.IsClosed), false);
foreach (WFWorkflow proces in procesy)
    Console.WriteLine($"{proces.Name} | {proces.DateFrom}");
```

**Pułapki:**
- `GetRemaindersView` zwraca **`View`** (obiekt warstwy UI). W czystej logice biznesowej preferuj
  `GetRemainders()` (WORKFLOW-E5) albo własny `RowCondition` na kluczu (WORKFLOW-E1) — patrz
  `rowcondition.md` („kod biznesowy nie używa View").
- Parametr `webUser: true` dotyczy **użytkownika zewnętrznego** (pulpity www) — wtedy warunek
  buduje się po `TaskUser`, nie po operatorze. Dla zwykłego operatora zostaw `false`.
- Sam folder „Panel Workflow" w UI wymaga **platynowej licencji BPM** — programowy odczyt
  `GetRemaindersView`/`GetGrantedView` nie sprawdza licencji, ale budując własny interfejs
  zachowaj spójność z ograniczeniami licencyjnymi.
- `GetRemaindersCount` liczy wiersze bez nakładania praw do obiektu nadrzędnego — przy włączonej
  w konfiguracji opcji weryfikacji praw do `Parent` liczba na „dzwonku" może być większa niż
  liczba wierszy widocznych na liście (znane, świadome ograniczenie platformy).

### WORKFLOW-E3 — Filtrowanie listy zadań workflow (★)

**Cel:** zawęzić listę zadań do zadań procesowych (powiązanych z `WFWorkflow`) i przefiltrować
po definicji procesu, stanie, okresie, operatorze lub roli — jak na liście **Workflow/Zadania**.

**Warianty:**

| Filtr | Warunek serwerowy |
|---|---|
| Tylko zadania workflow | `new FieldCondition.Null(nameof(Task.WFWorkflow), isNull: false)` (IS NOT NULL) |
| Po definicji procesu | `new FieldCondition.Equal("Definition.WFDefinition", definicja)` (ścieżka przez referencję) |
| Po stanie | `new FieldCondition.Equal(nameof(Task.Progress), TaskProgress.Active)` |
| Po okresie (zachodzenie na `FromTo`) | `Start <= okres.To` AND `End >= okres.From` |
| Po operatorze wykonującym | `new FieldCondition.Equal(nameof(Task.Operator), op)` |
| Po roli | `new FieldCondition.Equal(nameof(Task.RoleGuid), roleGuid)` |

**Pola i typy:**

| Element | Typ | Uwagi |
|---|---|---|
| `Task.WFWorkflow` | `Soneta.Business.IWFWorkflow` (iface-ref → `WFWorkflow`) | `null` dla zadań spoza workflow (powiadomienia, zadania własne) |
| `Task.Definition` | `Soneta.Business.Db.TaskDefinition` | węzeł; `Definition.WFDefinition` prowadzi do definicji procesu |
| `Task.Start` / `Task.End` | `System.DateTime` | termin zadania (pola bazodanowe) |
| `Task.Operator` | `Soneta.Business.App.Operator` | wykonujący |
| `Task.RoleGuid` | `System.Guid` | rola (`Guid.Empty` = brak) |
| `WFDefinition` | `Soneta.Workflow.Config` | definicja procesu (dana konfiguracyjna — w sesji operacyjnej czytaj przez `session.GetWorkflow().WFDefs`) |
| `WorkflowTasksParams` | `Soneta.Workflow.UI.Params` | gotowy obiekt parametrów listy UI: property `Definition`, `Operator`, `RoleName`, `Period`, `TaskProgress`, `TaskActivity`; `GetRowCondition(bool myTasks, bool period) → RowCondition` |

**Snippet:**

```csharp
using Soneta.Business;
using Soneta.Business.Db;
using Soneta.Types;

var tasks = session.GetBusiness().Tasks;

// Definicję procesu pobieramy dynamicznie (Demo nie gwarantuje definicji workflow)
var definicja = session.GetWorkflow().WFDefs.GetFirst();
var okres = FromTo.Month(Date.Today);

// Zadania procesowe (WFWorkflow != null)
RowCondition rc = new FieldCondition.Null(nameof(Task.WFWorkflow), isNull: false);

if (definicja != null)
    rc &= new FieldCondition.Equal(
        $"{nameof(Task.Definition)}.{nameof(TaskDefinition.WFDefinition)}", definicja);

rc &= new FieldCondition.Equal(nameof(Task.Progress), TaskProgress.Active);

// Zadanie zachodzi na okres: Start <= okres.To AND End >= okres.From
rc &= new FieldCondition.LessEqual(nameof(Task.Start), (DateTime)okres.To);
rc &= new FieldCondition.GreaterEqual(nameof(Task.End), (DateTime)okres.From);

foreach (Task task in tasks.WgDefinition[rc])
    Console.WriteLine($"{task.Name} | {task.WFWorkflow?.Name} | {task.Start:d} – {task.End:d}");
```

**Pułapki:**
- `FieldCondition.Null(path, isNull)`: `isNull: true` = IS NULL, `isNull: false` = IS NOT NULL —
  łatwo o odwrócenie intencji; zawsze przekazuj parametr nazwany.
- Warunek po definicji procesu idzie **ścieżką przez referencję** (`"Definition.WFDefinition"`) —
  serwer wykona JOIN; działa tylko dla pól bazodanowych (patrz `rowcondition.md`).
- `Task.End` bywa `DateTime.MinValue`/`MaxValue` dla zadań bez terminu — warunek okresowy
  `GreaterEqual(End, from)` odfiltruje zadania z `End == MinValue`. Jeśli mają być widoczne,
  dodaj alternatywę `| new FieldCondition.Equal(nameof(Task.End), DateTime.MinValue)`.
- `WorkflowTasksParams` wymaga `Context` (warstwa UI) — używaj go w workerach/extenderach list,
  a w czystym kodzie biznesowym buduj `RowCondition` jak w snippecie.
- Filtr „moje zadania" nie wchodzi w zakres tej receptury — łącz powyższe warunki z
  `GetMyTasksCondition` z WORKFLOW-E1 przez `&=`.

### WORKFLOW-E4 — Odczyt zadań powiązanych z konkretnym obiektem (rodzicem) (★)

**Cel:** znaleźć zadania „doczepione" do dowolnego obiektu biznesowego (dokumentu, kontrahenta,
pracownika…) — czyli takie, których `Task.Parent` wskazuje na ten wiersz — oraz zadania danego
procesu lub węzła.

**Warianty:**

| Wariant | Klucz / property |
|---|---|
| Zadania wiersza (rodzica) | `Tasks.WgParent[guidedRow]` → `SubTable<Task>` |
| Zadania wiersza od daty startu | `Tasks.WgParent[guidedRow, start]` |
| Zadania procesu | `Tasks.WgWFWorkflow[wfWorkflow]` lub `wfWorkflow.AllTasks` |
| Zadania węzła (definicji zadania) | `Tasks.WgDefinition[taskDefinition]` |
| Procesy powiązane z wierszem (zalogowany operator jest opiekunem) | `task.ParentWFWorkflows: SubTable` |

**Pola i typy:**

| Element | Typ | Uwagi |
|---|---|---|
| `Task.Parent` | `Soneta.Business.IGuidedRow` (pole bazodanowe) | obiekt główny zadania; każdy `GuidedRow` implementuje `IGuidedRow` |
| `Tasks.WgParent` | `TaskTable.ParentRelation : Key<Task>` | klucz `(Parent, Start, ID)`; indeksatory `[IGuidedRow]`, `[IGuidedRow, DateTime]` |
| `Tasks.WgWFWorkflow` | `TaskTable.WFWorkflowRelation : Key<Task>` | indeksator `[IWFWorkflow]` |
| `Tasks.WgDefinition` | `TaskTable.DefinitionRelation : Key<Task>` | indeksatory `[TaskDefinition]`, `[TaskDefinition, DateTime]` |
| `Task.LinkedObjects` | `SubTable<Soneta.Business.Db.TaskLinkedObj>` | dodatkowe obiekty powiązane (szczegóły: WORKFLOW-G1) |

**Snippet:**

```csharp
using Soneta.Business;
using Soneta.Business.Db;

var tasks = session.GetBusiness().Tasks;

// Rodzic — dowolny GuidedRow; w Demo np. pierwszy kontrahent (pobierany dynamicznie)
IGuidedRow rodzic = session.GetCRM().Kontrahenci.WgKodu.GetFirst();

// Wszystkie zadania doczepione do wiersza
SubTable<Task> zadaniaWiersza = tasks.WgParent[rodzic];
Console.WriteLine($"Zadań rodzica: {zadaniaWiersza.Count}");

// Tylko aktywne zadania wiersza — dalsze filtrowanie serwerowe na SubTable
foreach (Task task in zadaniaWiersza[t => t.Progress == TaskProgress.Active])
    Console.WriteLine($"{task.Name} | proces: {task.WFWorkflow?.Name ?? "(poza workflow)"}");

// Zadania konkretnego procesu (jeśli istnieje)
var proces = session.GetWorkflow().WFWorkflows.GetFirst();
if (proces != null)
    foreach (Task task in tasks.WgWFWorkflow[proces])
        Console.WriteLine($"{task.Name} | {task.Progress}");
```

**Pułapki:**
- `WgParent[rodzic]` zwraca zadania, w których wiersz jest **obiektem głównym** (`Parent`).
  Zadanie może być dodatkowo „widoczne" przy innych obiektach przez `LinkedObjects`
  (`TaskLinkedObj`) — żeby znaleźć także te powiązania, przeszukaj `TaskLinkedObjs`
  (receptura WORKFLOW-G1).
- Indeksator klucza wymaga wiersza **z tej samej sesji** — wiersz z innej sesji najpierw
  zmaterializuj: `var lokalny = (Kontrahent)session[obcy];` (safe-code).
- `task.WFWorkflow` jest `null` dla zadań spoza workflow (przypomnienia, zadania ręczne) —
  zawsze używaj dostępu warunkowego.
- `WgParent` jest posortowany po `(Parent, Start)` — kolejność zadań to kolejność ich startu;
  „pierwsze zadanie procesu" odczytasz wprost z `task.WFFirstTask`.
- `ParentWFWorkflows` zwraca tylko procesy, których **opiekunem jest zalogowany operator** —
  to nie jest pełna lista procesów wiersza.

### WORKFLOW-E5 — Obsługa przypomnień i powiadomień o zadaniach (★)

**Cel:** odczytać przypomnienia zalogowanego operatora, sprawdzić licznik powiadomień,
a także odłożyć („drzemka") lub wyłączyć przypomnienie zadania.

**Warianty:**

| Wariant | Jak |
|---|---|
| Wymagalne przypomnienia (aktywne, termin minął) | `tasks.GetRemainders()` |
| Wszystkie przypomnienia (aktywne + oczekujące) | `tasks.GetRemainders(all: true)` |
| Licznik powiadomień | `tasks.GetRemaindersCount(all: false).Count` |
| Odłożenie przypomnienia | `task.Notification = DateTime.Now.AddMinutes(n)` w transakcji |
| Wyłączenie przypomnienia | `task.IsNotification = false` w transakcji |
| Ustawienie daty/godziny oddzielnie | `task.NotificationDate` / `task.NotificationTime` |

**Pola i typy:**

| Element | Typ | Uwagi |
|---|---|---|
| `Tasks.GetRemainders()` / `GetRemainders(bool all)` | `→ IEnumerable<Task>` | `all: false` zwraca tylko zadania z `IsNotification == true` |
| `Tasks.GetRemaindersCount(bool all, bool webUser = false)` | `→ Tasks.RemindersCountResult` | `Count`, `LastTaskID` — jedno zapytanie agregujące |
| `Task.Notification` | `System.DateTime` (pole bazodanowe) | `DateTime.MinValue` = brak przypomnienia |
| `Task.IsNotification` | `bool` (kalkulowane, zapisywalne) | getter: `Notification != DateTime.MinValue`; setter `true` podstawia `Start` lub `Now+10min`, `false` zeruje |
| `Task.NotificationDate` / `NotificationTime` | `Soneta.Types.Date` / `Soneta.Types.Time` | rozbicie `Notification` na datę i godzinę (zapisywalne) |
| `Task.TaskNotificationIsNotRead` | `bool` | `true` = powiadomienie nieodczytane przez bieżącego użytkownika |
| `Task.NotificationCategory` | `Soneta.Business.NotificationCategory` | kategoria powiadomienia |

**Snippet:**

```csharp
using Soneta.Business.Db;

var tasks = session.GetBusiness().Tasks;

// 1. Licznik powiadomień zalogowanego operatora
var licznik = tasks.GetRemaindersCount(all: false);
Console.WriteLine($"Powiadomień do obsłużenia: {licznik.Count}");

// 2. Lista wymagalnych przypomnień
foreach (Task task in tasks.GetRemainders())
    Console.WriteLine(
        $"{task.Name} | {task.NotificationDate} {task.NotificationTime}" +
        $" | nieodczytane: {task.TaskNotificationIsNotRead}");

// 3. Odłożenie pierwszego przypomnienia o 30 minut (transakcja + Save)
Task doOdlozenia = tasks.GetRemainders().FirstOrDefault();
if (doOdlozenia != null)
{
    using (var t = session.Logout(editMode: true))
    {
        doOdlozenia.Notification = DateTime.Now.AddMinutes(30);
        // wyłączenie przypomnienia: doOdlozenia.IsNotification = false;
        t.Commit();
    }
    session.Save();
}
```

**Pułapki:**
- `GetRemainders()` (bez `all`) filtruje wynik dodatkowo po `IsNotification` — zwróci wyłącznie
  zadania z ustawionym terminem przypomnienia; `GetRemainders(all: true)` obejmuje też zadania
  oczekujące (`Waiting`) i bez wymagalnego terminu.
- Edycja `Notification`/`IsNotification` to zwykła edycja danych operacyjnych — pełny wzorzec
  `Logout(editMode: true)` → `Commit()` → `session.Save()` (patrz `session-login.md`); brak
  `Commit()` = rollback.
- Dla zadania w **aktywnym procesie workflow** pola przypomnienia są w UI tylko do odczytu
  (`IsNotificationInActiveProcess()`) — przypomnieniem steruje silnik; nie nadpisuj go w kodzie
  dla zadań z `WFWorkflow != null && Progress == Active`, jeżeli nie chcesz rozjechać się
  z logiką procesu.
- Setter `IsNotification = true` ustawia termin na `Start` zadania (a gdy `Start` pusty —
  `DateTime.Now + 10 min`); jeśli chcesz konkretny termin, ustaw wprost `Notification`
  (albo `NotificationDate` + `NotificationTime`).
- Stan „odczytane/nieodczytane" jest per użytkownik (osobna tabela powiązań) — do filtrowania
  po nim służy parametr `notificationRead` metody `GetRemaindersView` (WORKFLOW-E2); nie ma
  publicznego pola bazodanowego „IsRead" na `Task`.
