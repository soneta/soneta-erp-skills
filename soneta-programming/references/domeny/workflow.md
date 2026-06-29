# Workflow — receptury kodu biznesowego (platforma Soneta)

Zbiór gotowych wzorców kodu dla domeny **Workflow**: definicje procesów
(**`Soneta.Workflow.Config.WFDefinition`**, tabela `WFDefs`), węzły procesów
(**`Soneta.Business.Db.TaskDefinition`**, tabela `TaskDefs`), tranzycje
(**`Soneta.Workflow.Config.WFTransition`**, tabela `WFTransitions`), uruchomione procesy
(**`Soneta.Workflow.WFWorkflow`**, tabela `WFWorkflows`, caption „Proces"), zadania operatora
(**`Soneta.Business.Db.Task`**, tabela `Tasks`, caption „Zadanie") oraz zadania CRM / aktywności
(**`Soneta.Zadania.Zadanie`**, tabela `Zadania`). Dokument jest częścią skilla
`soneta-programming`. Celem jest, aby agent pisał **bezbłędny kod biznesowy** operujący na
procesach i zadaniach — trafiający w realne pola, kolekcje i workery platformy.

> Format **zwarty**: każdy wzorzec opisuje ogólny przypadek + tabelę wariantów. Fundamenty (sesja,
> transakcja, blokada optymistyczna, praca z `SubTable`, obsługa błędów, wywoływanie workerów)
> są opisane w [`safe-code.md`](../safe-code.md), [`session-login.md`](../session-login.md) oraz
> [`worker-extender.md`](../worker-extender.md) — tutaj się do nich odwołujemy, nie powtarzamy ich.
>
> **Cały kod w tym dokumencie jest zgodny z C# 10** (target-typed `new`, `var`, wyrażenia `switch`,
> nazwane parametry `bool`). Snippety operują wyłącznie na **publicznym kontrakcie** platformy — nie
> ma odwołań do prywatnych klas ani kodu źródłowego aplikacji.

## Fakty o typie (zweryfikowane skanem DLL — `scan-props.csx`)

- **Model domeny — konfiguracja vs dane operacyjne:**
  - **Konfiguracja** (`config="true"`): `WFDefinition` (`WFDefs`), `TaskDefinition` (`TaskDefs`),
    `WFTransition` (`WFTransitions`), `WFTransitionDefinition` (`WFTransitionDefs`),
    `OGSchema` (`OGSchemas`), `WFProcessRole` (`WFProcessRoles`).
  - **Dane operacyjne:** `WFWorkflow` (`WFWorkflows`), `Task` (`Tasks`), `Zadanie` (`Zadania`),
    `Matter` (`Matters`).
- **Moduły i dostęp z sesji:**
  - `Soneta.Workflow.WorkflowModule` — `session.GetWorkflow()`; tabele `WFDefs`, `WFTransitions`,
    `WFTransitionDefs`, `OGSchemas`, `WFProcessRoles`, `WFWorkflows`.
  - `Soneta.Business.Db.BusinessModule` — `session.GetBusiness()`; tabele `Tasks`, `TaskDefs`,
    `TaskLinkedObjs`.
  - `Soneta.Zadania.ZadaniaModule` — `session.GetZadania()`; tabele `Zadania`, `DefZadan`.
  - `Soneta.Workflow.Dms.DmsModule` — `session.GetDms()`; tabela `Matters` (sprawy).
- **„Węzeł" procesu to `TaskDefinition`** (definicja zadania), a uruchomiony węzeł to `Task`
  (egzemplarz). Silnik workflow tworzy egzemplarze `Task` na podstawie `TaskDefinition` w trakcie
  realizacji tranzycji.
- **Najważniejsze pola bazodanowe `WFWorkflow` (proces):** `Name: string`,
  `Number: Soneta.Core.NumerDokumentu` (subrow), `WorkflowDefinition: WFDefinition`,
  `Operator: Soneta.Business.App.Operator` (inicjujący), `ManagingRow: Soneta.Business.IManagingRow`
  (wiersz zarządzający), `IsClosed: bool`, `DateFrom`/`DateTo: Soneta.Types.Date`,
  `TimeFrom`/`TimeTo: Soneta.Types.TimeSec`.
- **Kolekcje `WFWorkflow`:** `AllTasks: SubTable<Task>` (wszystkie zadania procesu),
  `LiveTasks`/`ActiveTasks: IEnumerable<Task>` (żywe / aktywne).
- **Najważniejsze pola bazodanowe `Task` (zadanie):** `Name: string`, `Definition: TaskDefinition`,
  `Parent: Soneta.Business.IGuidedRow` (obiekt główny), `WFWorkflow: IWFWorkflow`,
  `Progress: Soneta.Business.Db.TaskProgress` (Active/Realized/NonActive/Waiting/Aborted),
  `Operator: Soneta.Business.App.Operator`, `Executor: Soneta.Business.ITaskUser` (iface-ref),
  `Start`/`End: System.DateTime`, `ExecutionTime: System.DateTime`,
  `Description: Soneta.Business.MemoText`.
- **Kolekcje `Task`:** `LinkedObjects: SubTable<TaskLinkedObj>` (obiekty powiązane;
  `TaskLinkedObj.LinkedObject`, `IsParent`).
- **Najważniejsze pola `WFDefinition` (definicja procesu):** `Symbol: string`, `Name: string`,
  `Numerator: Soneta.Core.DefinicjaNumeracji`, `DefinitionType: Soneta.Business.Db.DefinitionTypeEnum`,
  `EditType: Soneta.Workflow.Enums.EditTypeEnum`, `IsDeployed: bool` (tryb wdrożenia),
  `IsDiagramEditedInHtml: bool`; kolekcje `TaskDefs: SubTable<TaskDefinition>`,
  `Transitions: SubTable<WFTransition>`.
- **Najważniejsze pola `WFTransition` (tranzycja):** `Name: string`, `LP: int`,
  `Source`/`Target: TaskDefinition`, `IsDefaultTransition: bool`, `WeakTransition: bool`,
  `IsUserDecision: bool`, `VariantTypeName: string`, `QuickAccess: string`, `Icon`/`ForeColor: string`,
  `Statement: Soneta.Business.MemoText` (kod kalkulatora realizacji),
  `CheckCode`/`ActionCode: Soneta.Business.Db.AlgorithmColumn`,
  `IncompatibleWithSerialOperations: bool`.
- **Cechy:** `Features: Soneta.Business.FeatureCollection` na `WFWorkflow`, `Task`, `WFDefinition`
  (indeksator po nazwie definicji cechy).
- **Dane w bazie Demo (GoldStandard):** dane słownikowe i definicje pobieraj **dynamicznie**
  (iteracja po tabeli / pierwszy wpis) — definicje procesów workflow nie są gwarantowane w Demo,
  dlatego scenariusze konfiguracyjne budują własną definicję od zera. Definicje zadań CRM
  (`DefZadan`) są zawsze dostępne.

## Podstawowe typy domenowe

| Typ | Namespace | Zastosowanie |
|---|---|---|
| `WFDefinition` | `Soneta.Workflow.Config` | definicja (szablon) procesu workflow |
| `TaskDefinition` | `Soneta.Business.Db` | węzeł procesu = definicja zadania |
| `WFTransition` | `Soneta.Workflow.Config` | tranzycja (przejście między węzłami) |
| `WFTransitionDefinition` | `Soneta.Workflow.Config` | reużywalny wzorzec tranzycji |
| `OGSchema` | `Soneta.Workflow.Config` | schemat generatora obiektów (mapowanie danych) |
| `WFProcessRole` | `Soneta.Workflow.Config` | rola procesowa (tor diagramu) |
| `WFWorkflow` | `Soneta.Workflow` | uruchomiona instancja procesu |
| `Task` | `Soneta.Business.Db` | zadanie operatora (egzemplarz węzła) |
| `TaskProgress` | `Soneta.Business.Db` | stan zadania (Active/Realized/NonActive/Waiting/Aborted) |
| `UserDecision` | `Soneta.Workflow` | decyzja użytkownika (dostępna tranzycja) na zadaniu |
| `Zadanie` | `Soneta.Zadania` | zadanie CRM / aktywność (wątek niezależny od workflow) |
| `Matter` | `Soneta.Workflow.Dms` | sprawa (DMS) |
| `Date` / `FromTo` / `Time` | `Soneta.Types` | daty i zakresy dat (terminy, okresy procesów) |

## Szablon wzorca

Każdy wzorzec (`WORKFLOW-Xn`, gdzie `X` = litera sekcji z listy zadań) ma stałą strukturę:

- **Cel** — co robi i kiedy go użyć.
- **Warianty** — tabela odmian przypadku (gdy dotyczy).
- **Pola i typy** — realne właściwości/kolekcje i ich typy.
- **Snippet** — kod C# 10 na publicznym kontrakcie.
- **Pułapki** — typowe błędy i zasady safe-code.

Znacznik ★ przy kodzie wzorca oznacza, że wzorzec ma własny, dedykowany test.

> **Konwencja testów:** każdy wzorzec ma odpowiadający test w
> `Soneta.Skills.Test/Workflow/` (klasa dziedzicząca z `WorkflowTestBase : TestBase`).
> Testy są wykonywane na bazie Demo z automatycznym rollbackiem — można w nich tworzyć i modyfikować
> dowolne dane. Stanowią wykonywalną dokumentację publicznego API.

## Mapa receptur

| Rozdział | Plik | Receptury |
|---|---|---|
| WORKFLOW01 — Definicja procesu workflow (projektowanie) | [workflow/WORKFLOW01-definicja-procesu.md](workflow/WORKFLOW01-definicja-procesu.md) | WORKFLOW-A* |
| WORKFLOW02 — Węzły procesu (definicje zadań) | [workflow/WORKFLOW02-wezly.md](workflow/WORKFLOW02-wezly.md) | WORKFLOW-B* |
| WORKFLOW03 — Tranzycje i przepływ procesu | [workflow/WORKFLOW03-tranzycje.md](workflow/WORKFLOW03-tranzycje.md) | WORKFLOW-C* |
| WORKFLOW04 — Proces — uruchamianie i cykl życia | [workflow/WORKFLOW04-proces-cykl-zycia.md](workflow/WORKFLOW04-proces-cykl-zycia.md) | WORKFLOW-D* |
| WORKFLOW05 — Zadania operatora — odczyt i przegląd | [workflow/WORKFLOW05-zadania-odczyt.md](workflow/WORKFLOW05-zadania-odczyt.md) | WORKFLOW-E* |
| WORKFLOW06 — Realizacja zadania workflow | [workflow/WORKFLOW06-realizacja-zadania.md](workflow/WORKFLOW06-realizacja-zadania.md) | WORKFLOW-F* |
| WORKFLOW07 — Powiązania zadania z dokumentami | [workflow/WORKFLOW07-powiazania.md](workflow/WORKFLOW07-powiazania.md) | WORKFLOW-G* |
| WORKFLOW08 — Zadania CRM (aktywności) | [workflow/WORKFLOW08-zadania-crm.md](workflow/WORKFLOW08-zadania-crm.md) | WORKFLOW-H* |
| WORKFLOW09 — Powiadomienia, triggery i automatyzacja | [workflow/WORKFLOW09-automatyzacja.md](workflow/WORKFLOW09-automatyzacja.md) | WORKFLOW-I* |
| WORKFLOW10 — Sprawy (DMS) i rejestr | [workflow/WORKFLOW10-sprawy-dms.md](workflow/WORKFLOW10-sprawy-dms.md) | WORKFLOW-J* |

## Powiązane dokumenty

- [`safe-code.md`](../safe-code.md) — sesja, transakcje, blokada optymistyczna, zasady bezpiecznego kodu.
- [`session-login.md`](../session-login.md) — `Session`, `Login`, `Database`.
- [`worker-extender.md`](../worker-extender.md) — workery, akcje menu Czynności, bindowanie.
- [`rowcondition.md`](../rowcondition.md) — serwerowy LINQ, `RowCondition`, `SubTable[condition]`.
- [`features.md`](../features.md) — cechy (`Features`), typy, dostęp typowany/nietypowany.
- [`scan-props.md`](../scan-props.md) / [`scan-workers.md`](../scan-workers.md) — inwentaryzacja pól i workerów; weryfikacja dokładnych nazw i typów pól obiektu z DLL.
