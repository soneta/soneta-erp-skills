# WORKFLOW04 — Proces workflow — uruchamianie i cykl życia

> Wspólne fakty o typie, podstawowe typy i szablon wzorca: [../workflow.md](../workflow.md).

> **Model „instancja procesu".** Uruchomiony proces to `Soneta.Workflow.WFWorkflow` (tabela
> `WFWorkflows`, caption „Proces") — **dane operacyjne**, w odróżnieniu od konfiguracyjnej
> definicji `WFDefinition` (WORKFLOW01). Cykl życia: **start** (silnik tworzy `WFWorkflow`
> i pierwsze zadanie `Task` z węzła startowego) → **realizacja** (decyzje na zadaniach —
> WORKFLOW06 — tworzą zadania kolejnych węzłów) → **zakończenie** (węzeł końcowy / brak aktywnych
> zadań / `Terminate()`) → opcjonalnie **skasowanie** zakończonego procesu (`ForceDelete()`).
>
> Procesem **nie steruje się przez `AddRow(new WFWorkflow(...))`** — instancję tworzy silnik
> w `TaskDefinition.StartProcess(...)` (metoda rozszerzająca z namespace
> `Soneta.Workflow.Config`). Punkt wejścia: `WFDefinition.GetStartPoint(): TaskDefinition`.
>
> Snippety zakładają, że w bazie istnieje **wdrożona** definicja procesu (np. `Symbol == "URLOP"`)
> zbudowana wg WORKFLOW01 (definicja), WORKFLOW02 (węzły, w tym startowy) i WORKFLOW03 (tranzycje)
> — w bazie Demo definicji procesów **nie ma**, scenariusze budują je od zera.

### WORKFLOW-D1 — Uruchomienie procesu workflow (utworzenie instancji) (★)

**Cel:** wystartować nową instancję procesu (`WFWorkflow`) z wdrożonej definicji — silnik tworzy
proces, nadaje numer, ustawia operatora inicjującego i zakłada pierwsze zadanie (`Task`) z węzła
startowego.

**Mechanizm:** `definicja.GetStartPoint()` zwraca węzeł startowy (`TaskDefinition` z
`IsStart == true`, nie zablokowany, uruchamiany z menu — `ActionRunAt.InMenu`); na nim wywołuje się
metodę rozszerzającą `StartProcess(GuidedRow row, Row source, Context context = null): Task`
(klasa `Soneta.Workflow.Config.TaskDefinitionExtender`). Metoda **sama otwiera transakcję
i robi Commit** — wystarczy potem `session.Save()`. Zwraca pierwsze zadanie procesu albo `null`,
gdy start został zablokowany (np. `SingleWorkflowInstance` — patrz Pułapki).

**Warianty:**

| Wariant | Droga publiczna | Uwagi |
|---|---|---|
| Start z kodu (bez UI) | `definicja.GetStartPoint().StartProcess(row, source)` | zwraca `Task`; pełna kontrola programowa |
| Start z kodu przez serwis | `session.GetRequiredService<IWorkflowToolsService>().StartWorkflowProcess(definicja)` | równoważne `GetStartPoint().StartProcess(null, null)` |
| Start interaktywny (UI) | `definicja.GetStartPoint().StartProcessUI(source, context)` | zwraca `object` — `Task`, kreator lub formularz pytań (QueryContext); wymaga `Context` |
| Start z dokumentu / listy | menu Czynności dokumentu (workery platformy) | sterowany polami `ShowInMenu`/`ShowInToolbar`/`ShowInListMenu` węzła — WORKFLOW-D2/D8 |
| Start automatyczny (na zdarzenie) | trigger + węzeł `ActionRunAt.Auto` | WORKFLOW09 (automatyzacja) |
| Start z harmonogramu | zadanie cykliczne harmonogramu | WORKFLOW09 |
| Podproces z zadania | konfiguracja `SubprocessDef` węzła | WORKFLOW-D4 |

**Pola i typy (`Soneta.Workflow.WFWorkflow`, tabela `WFWorkflows`, operacyjna):**

| Pole | Typ | Opis |
|---|---|---|
| `Name` | `string` | nazwa procesu (numerowana wg `Numerator` definicji) |
| `Number` | `Soneta.Core.NumerDokumentu` (subrow) | numer procesu (`Number.NumerPelny: string`) |
| `WorkflowDefinition` | `Soneta.Workflow.Config.WFDefinition` | definicja, z której powstał proces |
| `Operator` | `Soneta.Business.App.Operator` | operator inicjujący |
| `IsClosed` | `bool` | status procesu — `false` = aktywny, `true` = zakończony (WORKFLOW-D6) |
| `DateFrom` / `TimeFrom` | `Soneta.Types.Date` / `Soneta.Types.TimeSec` | data/godzina rozpoczęcia (`DateTimeFrom: System.DateTime`) |
| `ManagingRow` | `Soneta.Business.IManagingRow` | wiersz zarządzający (WORKFLOW-D3) |
| `AllTasks` | `Soneta.Business.SubTable<Soneta.Business.Db.Task>` | wszystkie zadania procesu |

**Snippet:**

```csharp
// using Soneta.Business; using Soneta.Business.Db; using Soneta.Workflow;
// using Soneta.Workflow.Config;                    // metody rozszerzające StartProcess / StartProcessUI
// using Microsoft.Extensions.DependencyInjection;  // GetRequiredService

using (var session = login.CreateSession(readOnly: false, config: false, name: "Start procesu WF"))
{
    // Definicja (dane konfiguracyjne) jest czytelna także w sesji operacyjnej:
    var definicja = session.GetWorkflow().WFDefs.WgSymbolu["URLOP"];

    // Węzeł startowy: IsStart == true, niezablokowany, uruchamiany z menu.
    TaskDefinition start = definicja.GetStartPoint();

    // StartProcess SAM otwiera transakcję i robi Commit — nie opakowuj własnym Logout:
    Task zadanie = start.StartProcess(row: null, source: null);
    if (zadanie != null)
    {
        var proces = (WFWorkflow)zadanie.WFWorkflow;
        // proces.IsClosed == false, proces.Operator == session.Login.Operator,
        // proces.WorkflowDefinition == definicja, proces.DateFrom == Date.Today,
        // proces.AllTasks zawiera już pierwsze zadanie (zadanie.Definition == start).
    }
    session.Save();

    // Alternatywa — serwis publiczny (równoważne StartProcess(null, null)):
    var wfService = session.GetRequiredService<IWorkflowToolsService>();
    Task zadanie2 = wfService.StartWorkflowProcess(definicja);
    session.Save();
}
```

**Pułapki:**
- **Nie twórz `WFWorkflow` ręcznie** (`AddRow(new WFWorkflow(def))`) — pominiesz silnik (numerację,
  pierwsze zadanie, wiersz zarządzający, powiadomienia). Jedyna poprawna droga to `StartProcess`.
- `GetStartPoint()` rzuca `NotSupportedException`, gdy definicja nie ma węzła startowego
  uruchamianego z menu (`IsStart`, `ActionRunAt.InMenu`). Węzeł czysto automatyczny
  (`ActionRunAt.Auto`) **nie jest** zwracany — taki proces startuje wyłącznie triggerem (WORKFLOW09).
- `StartProcess` zwraca **`null` bez wyjątku**, m.in. gdy definicja ma `SingleWorkflowInstance ==
  true` i dla tego samego obiektu (`row`) istnieje już aktywny proces tej definicji — zawsze
  sprawdzaj wynik.
- Definicja **zablokowana** (`Locked`) nie wystartuje (jedyny wyjątek: podproces z procesu definicji
  standardowej); definicja **niewdrożona** (`IsDeployed == false`) nie wystartuje automatycznie —
  wdrożenie to WORKFLOW-A7.
- `StartProcessUI` wymaga niepustego `Context` z tej samej sesji i może zwrócić formularz pytań /
  kreator zamiast `Task` — to droga dla czynności UI, nie dla kodu wsadowego.
- Po `StartProcess` pamiętaj o `session.Save()` — Commit zrobił silnik, ale zapis sesji należy
  do wołającego (blokada optymistyczna — patrz safe-code.md).

### WORKFLOW-D2 — Start procesu z poziomu dokumentu / obiektu biznesowego (★)

**Cel:** uruchomić proces „na dokumencie" — dowolny `GuidedRow` (dokument handlowy, kontrahent,
pracownik…) staje się obiektem nadrzędnym (`Task.Parent`) pierwszego zadania i punktem zaczepienia
całego obiegu.

**Mechanizm:** ten sam `StartProcess`, z wypełnionym parametrem `row`. Silnik wiąże zadanie
z wierszem (`Task.Parent = row`), wyznacza wiersz zarządzający procesu (WORKFLOW-D3) i — przy
`SingleWorkflowInstance` — pilnuje jednej aktywnej instancji na wiersz. W UI czynności startu
w menu dokumentu/listy generują workery platformy na podstawie pól węzła startowego:
`ShowInMenu`/`ShowInToolbar` (formularz) i `ShowInListMenu`/`ShowInListToolbar` (lista).

**Pola i typy:**

| Element | Typ | Opis |
|---|---|---|
| `Task.Parent` | `Soneta.Business.IGuidedRow` | obiekt nadrzędny zadania (dokument, na którym wystartowano proces) |
| `Tasks.WgParent` | klucz (`Key`) na `session.GetBusiness().Tasks` | zadania powiązane z wierszem — `WgParent[guidedRow]` |
| `TaskDefinition.ShowInMenu` / `ShowInToolbar` | `bool` | widoczność czynności startu na formularzu obiektu |
| `TaskDefinition.ShowInListMenu` / `ShowInListToolbar` | `bool` | widoczność czynności startu na liście obiektów |
| `WFDefinition.SingleWorkflowInstance` | `bool` | blokada drugiego aktywnego procesu tej definicji dla tego samego wiersza |

**Snippet:**

```csharp
// using Soneta.Business; using Soneta.Business.Db; using Soneta.CRM;
// using Soneta.Workflow; using Soneta.Workflow.Config;

using (var session = login.CreateSession(readOnly: false, config: false, name: "Start WF z dokumentu"))
{
    var definicja = session.GetWorkflow().WFDefs.WgSymbolu["URLOP"];
    var start = definicja.GetStartPoint();

    // Dowolny GuidedRow z bazy — dane Demo pobieraj dynamicznie:
    var kontrahent = session.GetCRM().Kontrahenci.WgKodu.OfType<Kontrahent>().FirstOrDefault();

    Task zadanie = start.StartProcess(row: kontrahent, source: null);
    if (zadanie != null)
    {
        // zadanie.Parent == kontrahent — proces "wisi" na dokumencie:
        var proces = (WFWorkflow)zadanie.WFWorkflow;

        // Odczyt z drugiej strony — wszystkie zadania wiersza:
        foreach (Task t in session.GetBusiness().Tasks.WgParent[kontrahent])
        {
            // t.WFWorkflow, t.Definition.Name, t.Progress
        }
    }
    session.Save();
}
```

**Pułapki:**
- Przy `SingleWorkflowInstance == true` drugi start na tym samym wierszu zwraca `null` (dopóki
  pierwszy proces jest aktywny) — to zachowanie projektowe, nie błąd.
- `row` musi pochodzić **z tej samej sesji** co definicja — wiersz z innej sesji przepnij przez
  `session.Get(row)` / `session[row]`.
- Parametr `source` służy zaawansowanym źródłom danych węzła (źródło danych zadania, mapowanie
  obiektu na nowo tworzony wiersz procesu — WORKFLOW02). Dla zwykłego startu „na dokumencie"
  przekazuj dokument w `row`, a `source` zostaw `null`; obiekt nie będący `GuidedRow` w `source`
  bez skonfigurowanego źródła danych ⇒ `NotSupportedException`.
- Czynności startu w menu obiektu pojawią się dopiero dla definicji **wdrożonej** i przy
  odpowiednich flagach `ShowIn…` węzła startowego; programowy `StartProcess` flag nie sprawdza.
- Start procesu nie gwarantuje, że zadanie trafi do bieżącego operatora — wykonawcę wyznacza rola
  procesowa węzła (WORKFLOW-A3, WORKFLOW02); odczyt „moich zadań" to WORKFLOW05.

### WORKFLOW-D3 — Powiązanie procesu z dokumentem zarządzającym (ManagingRow)

**Cel:** odczytać i wykorzystać **wiersz zarządzający** procesu — obiekt biznesowy
(np. dokument), który „prowadzi" proces i jest informowany o jego zakończeniu.

**Mechanizm:** `WFWorkflow.ManagingRow: Soneta.Business.IManagingRow` jest na publicznym kontrakcie
**tylko do odczytu** — wyznacza go silnik podczas startu (obiekt przekazany do `StartProcess`,
o ile jego tabela implementuje `IManagingRow`; ostateczną decyzję podejmuje kalkulator zadań
definicji). Interfejs `IManagingRow : IGuidedRow` (namespace `Soneta.Business`) ma metodę
`WfWorkflowFinished(IWFWorkflow)` — wywoływaną przy zakończeniu procesu.

**Pola i typy:**

| Element | Typ | Opis |
|---|---|---|
| `WFWorkflow.ManagingRow` | `Soneta.Business.IManagingRow` (tylko get) | wiersz zarządzający; `null` dla procesu bez dokumentu oraz **po zakończeniu** procesu |
| `WFWorkflows.WgManagingRow` | klucz na `session.GetWorkflow().WFWorkflows` | procesy danego wiersza zarządzającego — `WgManagingRow[managingRow]: SubTable<WFWorkflow>` |
| `IManagingRow.WfWorkflowFinished` | `void (IWFWorkflow)` | powiadomienie dokumentu o zakończeniu procesu (np. odblokowanie edycji) |
| `IWorkflowToolsService.GetParentWorkflows` | `SubTable (IGuidedRow)` | procesy mające zadania powiązane z danym wierszem (`Task.Parent`) |

**Snippet:**

```csharp
// using Soneta.Business; using Soneta.Workflow;
// using Microsoft.Extensions.DependencyInjection;

// Od strony procesu — co nim zarządza:
var proces = session.GetWorkflow().WFWorkflows.OfType<WFWorkflow>()
    .FirstOrDefault(p => !p.IsClosed);
IManagingRow zarzadzajacy = proces?.ManagingRow;     // null = proces bez dokumentu lub zakończony

// Od strony dokumentu — które procesy on prowadzi:
if (zarzadzajacy != null)
    foreach (WFWorkflow p in session.GetWorkflow().WFWorkflows.WgManagingRow[zarzadzajacy])
    {
        // p.Name, p.IsClosed, p.WorkflowDefinition.Name
    }

// Procesy powiązane z dowolnym wierszem przez zadania (Task.Parent):
var wfService = session.GetRequiredService<IWorkflowToolsService>();
var procesyWiersza = wfService.GetParentWorkflows(zarzadzajacy);  // SubTable procesów
```

**Pułapki:**
- **`ManagingRow` nie ma publicznego settera** — powiązanie ustala silnik przy starcie; nie da się
  „przepiąć" działającego procesu na inny dokument z kodu zewnętrznego.
- Przy zakończeniu procesu (`Terminate`, węzeł końcowy) silnik **zeruje `ManagingRow`** (po
  uprzednim wywołaniu `WfWorkflowFinished`) — dla zakończonych procesów pole jest `null`; trwałe
  powiązanie z dokumentem odczytasz przez zadania (`Tasks.WgParent[dokument]` →
  `task.WFWorkflow`) — patrz WORKFLOW07.
- Nie każdy `GuidedRow` jest `IManagingRow` — interfejs implementują wybrane tabele platformy
  (np. dokumenty obsługujące blokadę edycji przez workflow). Obiekt bez `IManagingRow` daje proces
  z `ManagingRow == null`, co jest poprawne.
- Dokument zarządzany przez aktywny proces bywa **tylko do odczytu** (definicja może wymuszać
  blokadę edycji obiektu nadrzędnego) — odblokowanie następuje wraz z postępem / zakończeniem
  procesu, nie przez ręczne zmiany pól.

### WORKFLOW-D4 — Uruchomienie podprocesu z zadania

**Cel:** skonfigurować węzeł, którego aktywacja uruchamia **podproces** (osobną instancję
`WFWorkflow` innej definicji) i nawigować między procesem głównym a podrzędnym.

**Mechanizm:** podproces konfiguruje się na **węźle** (`TaskDefinition`, dane konfiguracyjne):
pole `SubprocessDef: Soneta.Business.IWFDefinition` wskazuje definicję procesu podrzędnego.
Gdy silnik aktywuje zadanie takiego węzła, **sam** startuje podproces (z węzła startowego definicji
podrzędnej) i zapisuje go w `Task.Subprocess`. Zadanie główne czeka; zakończenie podprocesu wznawia
je — silnik dobiera tranzycję wyjściową **po nazwie** węzła końcowego podprocesu (porównanie
`WFTransition.Name` z `TaskDefinition.FormatedName` węzła końcowego) albo przelicza zadanie.
W definicjach typu Engine węzeł startowy podprocesu oznacza się
`StartPointType = TaskStartPointTypeEnum.Subprocess` (enum `Soneta.Business.Db`).

**Pola i typy:**

| Element | Typ | Opis |
|---|---|---|
| `TaskDefinition.SubprocessDef` | `Soneta.Business.IWFDefinition` (iface-ref) | definicja podprocesu (konfiguracja) |
| `TaskDefinition.StartPointType` | `Soneta.Business.Db.TaskStartPointTypeEnum` | `Subprocess` — węzeł startowy podprocesu (setter tylko dla definicji Engine) |
| `Task.Subprocess` | `Soneta.Business.IWFWorkflow` | uruchomiony podproces przypięty do zadania głównego |
| `Tasks.WgSubprocess` | klucz na `session.GetBusiness().Tasks` | nawigacja odwrotna: zadanie główne dla podprocesu — `WgSubprocess[proces].GetFirst()` |
| `TaskDefinition.EndType` | `Soneta.Business.Db.TaskEndTypeEnum` | sposób zakończenia węzła końcowego podprocesu (`WFTransitions` / `None` / `Workflow`) |

**Snippet:**

```csharp
// Konfiguracja węzła-podprocesu (dane KONFIGURACYJNE — sesja config, patrz WORKFLOW01/02):
using (var session = login.CreateSession(readOnly: false, config: true, name: "Węzeł podprocesu"))
{
    var defGlowna = session.GetWorkflow().WFDefs.WgSymbolu["URLOP"];
    var defPodrzedna = session.GetWorkflow().WFDefs.WgSymbolu["AKCEPT"];

    using (var t = session.Logout(editMode: true))
    {
        TaskDefinition wezel = defGlowna.TaskDefs.OfType<TaskDefinition>()
            .First(w => w.Name == "Akceptacja dyrektora");
        wezel.SubprocessDef = defPodrzedna;          // aktywacja węzła wystartuje podproces
        t.Commit();
    }
    session.Save();
}

// Nawigacja w danych operacyjnych (proces uruchomiony — WORKFLOW-D1/D2):
using (var session = login.CreateSession(readOnly: true, config: false, name: "Podprocesy"))
{
    var proces = session.GetWorkflow().WFWorkflows.OfType<WFWorkflow>().First(p => !p.IsClosed);

    foreach (Task t in proces.AllTasks)
        if (t.Subprocess is WFWorkflow podproces)
        {
            // t = zadanie główne; podproces.IsClosed, podproces.WorkflowDefinition.Name
            // Nawigacja odwrotna — od podprocesu do zadania głównego:
            Task zadanieGlowne = session.GetBusiness().Tasks.WgSubprocess[podproces].GetFirst();
        }
}
```

**Pułapki:**
- **Nie ma publicznej metody „wystartuj podproces"** — odpowiednik `StartSubprocess` jest
  wewnętrzny; podproces uruchamia wyłącznie silnik na podstawie `SubprocessDef` węzła. Programowo
  możesz co najwyżej doprowadzić proces do tego węzła (decyzją — WORKFLOW06).
- Zakończenie podprocesu dobiera tranzycję wyjściową zadania głównego **po nazwie** — nazwa
  tranzycji ze źródłem w węźle-podprocesie musi odpowiadać nazwie węzła końcowego definicji
  podrzędnej; brak dopasowania ⇒ wyjątek braku tranzycji.
- `Terminate()` procesu **nadrzędnego** przerywa też aktywne podprocesy; przerwanie samego
  podprocesu wznawia (przelicza) zadanie główne.
- Definicję **zablokowaną** (`Locked`) wolno uruchomić jako podproces tylko z procesu definicji
  standardowej — inaczej start podprocesu kończy się wyjątkiem („definicja procesu jest
  zablokowana").
- `SubprocessDef` to pole konfiguracyjne — edycja wymaga sesji `config: true` i jest sensowna
  w trybie modelowania definicji (przed wdrożeniem — WORKFLOW-A7). `Task.Subprocess` ma publiczny
  setter, ale jest **własnością silnika** — nie przepinaj go ręcznie.

### WORKFLOW-D5 — Odczyt stanu i postępu procesu (★)

**Cel:** odczytać status procesu (aktywny / zakończony), daty, zadania na poszczególnych etapach
oraz „etap procesu i odpowiedzialnego" dla dowolnego wiersza.

**Warianty:**

| Co czytamy | Droga | Wynik |
|---|---|---|
| Status i daty | `proces.IsClosed`, `DateFrom`/`DateTo`, `DateTimeFrom`/`DateTimeTo` | `bool`, `Soneta.Types.Date`, `System.DateTime` |
| Wszystkie zadania | `proces.AllTasks` | `SubTable<Task>` |
| Zadania „żywe" | `proces.LiveTasks` | `IEnumerable<Task>` — `Task.IsActiveProgress` |
| Zadania aktywne (etap bieżący) | `proces.ActiveTasks` | `IEnumerable<Task>` — żywe i z `Definition.ActiveTask` |
| Przebieg historyczny | `proces.SearchFirstTask()`, `SearchPreviousTask(task)`, `SearchTasks(taskDefinition)` | `Task` / `List<Task>` |
| Obiekty powiązane | `proces.SearchParents(typeof(T))`, `SearchParents(taskDefinition)` | `List<IGuidedRow>` |
| Etap + odpowiedzialny (dowolny wiersz) | worker `Soneta.Workflow.Workers.WFWorkflowStageWorker` | `Stage: string`, `StageEnum: WfWorkflowStageEnum`, `Responsible: string` |
| Skrót na `GuidedRow` | `row.WfGetStage()` / `row.WfIsActive()` (`Soneta.Workflow.Extensions.WfGuidedRowExtensions`) | `WfWorkflowStageEnum` / `bool` |

**Pola i typy:** tabela w WORKFLOW-D1 oraz:

| Element | Typ | Opis |
|---|---|---|
| `WFWorkflows.WgWorkflowDefinition` | klucz | procesy danej definicji — `WgWorkflowDefinition[definicja]` (też: `definicja.WFWorkflows`) |
| `WFWorkflows.WgOperator` | klucz | procesy zainicjowane przez operatora |
| `WfWorkflowStageEnum` | `Soneta.Workflow.Enums` | `None` („Brak") / `Active` („Aktywny(e)") / `Finished` („Zakończony(e)") |
| `Task.Progress` | `Soneta.Business.Db.TaskProgress` | stan zadania (`Active`, `Realized`, `NonActive`, `Waiting`, `Aborted`) |

**Snippet:**

```csharp
// using Soneta.Business.Db; using Soneta.CRM; using Soneta.Workflow;
// using Soneta.Workflow.Enums; using Soneta.Workflow.Extensions; using Soneta.Workflow.Workers;

using (var session = login.CreateSession(readOnly: true, config: false, name: "Stan procesów"))
{
    var definicja = session.GetWorkflow().WFDefs.WgSymbolu["URLOP"];

    // Procesy danej definicji (kolekcja na definicji):
    foreach (WFWorkflow proces in definicja.WFWorkflows)
    {
        bool aktywny = !proces.IsClosed;
        // proces.DateTimeFrom; dla zakończonych: proces.DateTimeTo

        // Bieżący etap = zadania aktywne; pełna historia = AllTasks:
        foreach (Task t in proces.ActiveTasks)
        {
            // t.Definition.Name (nazwa węzła), t.Operator / t.TaskUser (wykonawca),
            // t.Progress == TaskProgress.Active
        }

        Task pierwsze = proces.SearchFirstTask();          // początek przebiegu
        var historia = proces.AllTasks.OfType<Task>()
            .OrderBy(t => t.Start)
            .Select(t => (Wezel: t.Definition.Name, t.Progress, t.Start, t.End));
    }

    // Etap i odpowiedzialny dla dowolnego wiersza (worker publiczny):
    var dokument = session.GetCRM().Kontrahenci.WgKodu.OfType<Kontrahent>().FirstOrDefault();
    var stage = new WFWorkflowStageWorker { GuidedRow = dokument };
    // stage.Stage ("Brak"/"Aktywny(e)"/"Zakończony(e)"), stage.StageEnum, stage.Responsible

    // Skróty rozszerzające na GuidedRow:
    WfWorkflowStageEnum etap = dokument.WfGetStage();
    bool wToku = dokument.WfIsActive();
}
```

**Pułapki:**
- `LiveTasks`/`ActiveTasks` to **`IEnumerable` liczone w locie** z `AllTasks` — nie są to widoki
  serwerowe; przy masowych odczytach filtruj po stronie serwera kluczami `Tasks.Wg…`
  (patrz rowcondition.md i WORKFLOW05).
- `ActiveTasks` ≠ `LiveTasks`: aktywne są tylko zadania węzłów z `Definition.ActiveTask` (etap
  „widoczny" procesu); zadania oczekujące / powiadomienia bywają żywe, ale nieaktywne.
- `WFWorkflowStageWorker` agreguje **wszystkie** procesy wiersza — `Active`, jeśli choć jeden
  proces trwa; `Finished`, gdy wszystkie zakończone; `None`, gdy wiersz nie ma procesów.
- Dla procesu zakończonego `ManagingRow == null` i `ActiveTasks` jest puste — stan końcowy odczytuj
  z `IsClosed`, `DateTo` i historii `AllTasks` (`Progress == Realized`/`Aborted`).
- Pole `proces.Tasks` (typu `Soneta.Business.View`) jest przeznaczone dla list UI — w kodzie
  biznesowym używaj `AllTasks` (`SubTable<Task>`).

### WORKFLOW-D6 — Zamknięcie / zakończenie procesu (★)

**Cel:** zakończyć proces — naturalnie (węzeł końcowy / brak aktywnych zadań) albo przerwać go
ręcznie (`Terminate()`).

**Mechanizm:** naturalne zakończenie wykonuje silnik podczas przeliczania zadań: zadanie węzła
końcowego (`EndType`) zostaje zrealizowane, a gdy `EndType == Workflow` lub w procesie nie ma już
zadań aktywnych — proces jest zamykany. Ręczne przerwanie to publiczna metoda
`WFWorkflow.Terminate()`: ustawia wszystkim żywym zadaniom `Progress = Aborted`, zamyka proces
(`IsClosed = true`), wypełnia `DateTo`/`TimeTo`, powiadamia `ManagingRow.WfWorkflowFinished(...)`
(i zeruje `ManagingRow`) oraz kończy aktywne podprocesy. Setter `IsClosed = true` jest aliasem
`Terminate()`. W UI tę samą operację wykonuje czynność „Zakończ proces" (publiczny worker
`Soneta.Workflow.Workers.WFWorkflowCloseWorker`, z potwierdzeniem).

**Warianty:**

| Wariant | Droga | Charakterystyka |
|---|---|---|
| Naturalne zakończenie | decyzje na zadaniach (WORKFLOW06) aż do węzła końcowego | `EndType = Workflow` wymusza zamknięcie całego procesu; `EndType = None` kończy tylko zadanie |
| Ręczne przerwanie z kodu | `proces.Terminate()` we własnej transakcji | żywe zadania → `Aborted` |
| Ręczne przerwanie z UI | worker `WFWorkflowCloseWorker.CloseProcess()` | `MessageBoxInformation` z potwierdzeniem; tylko procesy główne |
| Zamknięcie przez pole | `proces.IsClosed = true` | wywołuje `Terminate()`; `IsClosed = false` na zamkniętym ⇒ wyjątek |

**Pola i typy:**

| Element | Typ | Opis |
|---|---|---|
| `WFWorkflow.Terminate()` | `void` | przerwanie procesu (wymaga transakcji edycyjnej) |
| `WFWorkflow.IsClosed` | `bool` | status; setter `true` = `Terminate()`, `false` ⇒ `RowException` |
| `WFWorkflow.DateTo` / `TimeTo` | `Soneta.Types.Date` / `Soneta.Types.TimeSec` | wypełniane przy zamknięciu |
| `TaskDefinition.EndType` | `Soneta.Business.Db.TaskEndTypeEnum` | `WFTransitions` (przez tranzycje), `None`, `Workflow` (kończy cały proces) |
| `TaskProgress.Aborted` | `Soneta.Business.Db.TaskProgress` | stan żywych zadań po przerwaniu |

**Snippet:**

```csharp
using (var session = login.CreateSession(readOnly: false, config: false, name: "Zakończenie procesu"))
{
    var proces = session.GetWorkflow().WFWorkflows.OfType<WFWorkflow>()
        .FirstOrDefault(p => !p.IsClosed);
    if (proces != null)
    {
        using (var t = session.Logout(editMode: true))
        {
            proces.Terminate();      // żywe zadania -> Aborted; IsClosed = true; DateTo/TimeTo
            t.Commit();
        }
        session.Save();

        // proces.IsClosed == true, proces.ManagingRow == null,
        // proces.AllTasks: Progress in { Realized, Aborted, ... } — żadnych żywych.
    }
}
```

**Pułapki:**
- `Terminate()` na **zakończonym** procesie rzuca `RowException` („Nie można przerywać zakończonego
  procesu") — sprawdzaj `IsClosed` przed wywołaniem; analogicznie nie wolno przerywać procesu
  w trakcie przeliczania przez silnik (np. z wnętrza algorytmów definicji).
- **Zakończenia nie da się cofnąć** — `IsClosed = false` rzuca wyjątek („Nie można aktywować
  zakończonego procesu"). Jedyna droga to nowy proces.
- `Terminate()` modyfikuje wiersze — wywołuj w transakcji (`session.Logout(editMode: true)` +
  `t.Commit()`) i zapisz sesję.
- Czynność UI „Zakończ proces" jest niedostępna dla **podprocesów** (proces nadrzędny zarządza ich
  życiem) i wymaga licencji Workflow oraz prawa do definicji; programowy `Terminate()` podprocesu
  wznowi zadanie główne (WORKFLOW-D4).
- Przerwanie ≠ realizacja: zadania dostają `Aborted`, nie `Realized` — raporty po `Progress`
  odróżnią proces przerwany od ukończonego; sam proces w obu przypadkach ma `IsClosed == true`.

### WORKFLOW-D7 — Anulowanie / usunięcie procesów i ich zadań (★)

**Cel:** trwale usunąć **zakończony** proces wraz z jego zadaniami i podprocesami (porządkowanie
bazy, dane testowe).

**Mechanizm:** zwykłe `Delete()` na `WFWorkflow` jest **zablokowane** (`GetCanDelete() == false`,
próba kasowania rzuca wyjątek „Bezpośrednie kasowanie procesu jest zabronione"). Publiczną drogą
jest `WFWorkflow.ForceDelete()`: działa tylko na procesie z `IsClosed == true` (inaczej wyjątek
„Proces nie został zakończony…"), kasuje wszystkie zadania procesu (`AllTasks`) i **rekurencyjnie**
usuwa procesy podrzędne. W UI istnieje czynność „Skasuj zakończone procesy" na liście procesów —
widoczna wyłącznie dla operatorów z uprawnieniem rozwojowym (worker wewnętrzny). Kasowaniem
**pojedynczych zadań** powiązanych z workflow rządzi prawo proste „Kasowanie zadań Workflow"
(`Tasks.IsSimpleRightDeleteWorkflowTasksGranted`).

**Pola i typy:**

| Element | Typ | Opis |
|---|---|---|
| `WFWorkflow.ForceDelete()` | `void` | kasuje zakończony proces + zadania + podprocesy (wymaga transakcji) |
| `WFWorkflow.GetCanDelete()` | `bool` (zawsze `false`) | standardowe kasowanie wyłączone |
| `Tasks.IsSimpleRightDeleteWorkflowTasksGranted` | `bool` (property tabeli `session.GetBusiness().Tasks`) | prawo proste do kasowania zadań powiązanych z workflow |
| `Tasks.GetIsSimpleRightDeleteWorkflowTasksGranted(Session)` | `static bool` | wariant statyczny tego samego prawa |
| `Task.GetCanDelete()` | `bool` | uwzględnia prawa modyfikacji + powyższe prawo proste dla zadań z `WFWorkflow != null` |

**Snippet:**

```csharp
using (var session = login.CreateSession(readOnly: false, config: false, name: "Kasowanie procesów"))
{
    var doSkasowania = session.GetWorkflow().WFWorkflows.OfType<WFWorkflow>()
        .Where(p => p.IsClosed)
        .ToArray();                       // materializacja PRZED kasowaniem w pętli

    using (var t = session.Logout(editMode: true))
    {
        foreach (var proces in doSkasowania)
            proces.ForceDelete();         // kasuje proces, jego zadania i podprocesy
        t.Commit();
    }
    session.Save();
}

// Kasowanie pojedynczego zadania workflow — sprawdź prawo proste:
bool wolnoKasowac = session.GetBusiness().Tasks.IsSimpleRightDeleteWorkflowTasksGranted;
```

**Pułapki:**
- **Najpierw zakończ, potem kasuj** — `ForceDelete()` aktywnego procesu rzuca wyjątek; sekwencja
  „anuluj i usuń" to `Terminate()` (WORKFLOW-D6) + `ForceDelete()` (mogą być w jednej transakcji).
- **Podprocesów nie kasuje się bezpośrednio** — usuwa je `ForceDelete()` procesu nadrzędnego;
  czynność UI wprost odmawia kasowania zaznaczonych procesów podrzędnych.
- `proces.Delete()` zawsze rzuca wyjątek — to nie jest błąd uprawnień, tylko projektowa blokada;
  nie próbuj jej obchodzić przez prawa dostępu.
- Kasowanie w pętli po „żywej" kolekcji tabeli psuje iterator — zmaterializuj listę (`ToArray()`)
  przed `ForceDelete()` (patrz safe-code.md).
- Operacja jest **nieodwracalna** i usuwa historię przebiegu (zadania, decyzje) — do celów
  audytowych zostaw procesy zakończone, kasuj tylko dane robocze/testowe.
- Czynność UI „Skasuj zakończone procesy" jest dostępna tylko dla operatora z flagą rozwojową —
  w zwykłej instalacji użytkownicy nie kasują procesów; traktuj `ForceDelete()` jako narzędzie
  administracyjne.

### WORKFLOW-D8 — Seryjne uruchomienie procesu dla wielu obiektów

**Cel:** wystartować ten sam proces dla wielu wierszy naraz (odpowiednik czynności na liście
z zaznaczonymi wierszami).

**Mechanizm:** programowo — pętla po wierszach z `StartProcess(row, null)` (każde wywołanie samo
zarządza transakcją); na końcu jeden `session.Save()`. W UI czynności seryjne na liście generuje
platforma (worker wewnętrzny) i filtruje definicje: przy więcej niż jednym zaznaczonym wierszu
pomijane są węzły startowe z `IncompatibleWithSerialOperations == true`; analogiczna flaga na
tranzycjach (`WFTransition.IncompatibleWithSerialOperations`) wyłącza decyzje z operacji seryjnych
(WORKFLOW06).

**Pola i typy:**

| Element | Typ | Opis |
|---|---|---|
| `TaskDefinition.IncompatibleWithSerialOperations` | `bool` | węzeł startowy „Niezgodny z operacjami seryjnymi" — wyklucza definicję ze startu seryjnego w UI |
| `WFTransition.IncompatibleWithSerialOperations` | `bool` | analogicznie dla decyzji (tranzycji) |
| `TaskDefinition.ShowInListMenu` / `ShowInListToolbar` | `bool` | widoczność czynności startu na liście |
| `WFDefinition.SingleWorkflowInstance` | `bool` | wiersze z aktywnym procesem są pomijane (start zwraca `null`) |

**Snippet:**

```csharp
// using Soneta.Business; using Soneta.Business.Db; using Soneta.CRM;
// using Soneta.Workflow; using Soneta.Workflow.Config;

using (var session = login.CreateSession(readOnly: false, config: false, name: "Seryjny start WF"))
{
    var definicja = session.GetWorkflow().WFDefs.WgSymbolu["URLOP"];
    var start = definicja.GetStartPoint();

    // Odpowiednik zaznaczonych wierszy listy — tu: kilku kontrahentów z Demo:
    GuidedRow[] zaznaczone = session.GetCRM().Kontrahenci.WgKodu
        .OfType<Kontrahent>().Take(3).Cast<GuidedRow>().ToArray();

    // Szanuj flagę zgodności z operacjami seryjnymi (tak robi UI):
    if (zaznaczone.Length > 1 && start.IncompatibleWithSerialOperations)
        throw new InvalidOperationException(
            "Węzeł startowy jest niezgodny z operacjami seryjnymi.");

    var uruchomione = new List<Task>();
    foreach (var row in zaznaczone)
    {
        // Każde wywołanie samo otwiera transakcję i robi Commit:
        Task zadanie = start.StartProcess(row, source: null);
        if (zadanie != null)                 // null = pominięty (np. aktywny proces już istnieje)
            uruchomione.Add(zadanie);
    }
    session.Save();                          // jeden zapis dla całej serii

    // uruchomione.Count procesów; każde zadanie.WFWorkflow to osobna instancja.
}
```

**Pułapki:**
- `StartProcess` zwraca `null` dla pominiętych wierszy (m.in. `SingleWorkflowInstance` z aktywnym
  procesem) — zliczaj wyniki, nie zakładaj „ile wierszy, tyle procesów".
- Flagi `IncompatibleWithSerialOperations` **nie są** egzekwowane przez `StartProcess` — to filtr
  warstwy czynności UI; w kodzie seryjnym sprawdzaj je jawnie (jak w snippecie), inaczej uruchomisz
  procesy, których projektant zabronił seryjnie.
- Węzły z pytaniami do użytkownika (QueryContext) lub kreatorem wymagają interakcji — w pętli
  wsadowej takie definicje albo wystartują bez danych, albo nie nadają się do startu seryjnego;
  weryfikuj na środowisku testowym.
- Seria w jednej sesji = jeden `Save()` i jedna blokada optymistyczna — przy bardzo dużych
  partiach dziel pracę na mniejsze sesje / porcje (patrz safe-code.md).
- Seryjne **podejmowanie decyzji** na wielu zadaniach to osobna receptura — WORKFLOW-F9
  (WORKFLOW06).
