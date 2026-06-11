# WORKFLOW06 — Realizacja zadania workflow

> Wspólne fakty o typie, podstawowe typy i szablon wzorca: [../workflow.md](../workflow.md).

> **Model „realizacji zadania".** Zadanie procesowe (`Soneta.Business.Db.Task` z
> `WFWorkflow != null`) realizuje się przez **podjęcie decyzji** — wybór tranzycji wychodzącej
> z węzła zadania. Decyzja na zadaniu to publiczna property-krotka
> `Task.UserDecision: (IWFTransition Transition, object Variant, Context Context)`.
> Jej ustawienie (wprost albo przez `SetUserDecision`/`GoThru`) rejestruje **zdarzenie serwerowe**
> przeliczenia — silnik wykonuje tranzycję dopiero przy **`Commit()`** transakcji: zadanie
> przechodzi w `Progress == Realized`, powstają zadania węzła docelowego (albo proces się kończy).
>
> Stanem zadania **workflow** nie steruje się ręcznie — `Task.Progress` ma publiczny setter tylko
> dla zadań własnych/powiadomień (`DefinitionType == DefinitionTypeEnum.None`); dla zadań
> procesowych rzuca `ColReadOnlyException` (stan zmienia silnik w reakcji na decyzje).
>
> **Uwaga o kontrakcie publicznym:** typ `UserDecision` (z `Caption`, `IsEnabled`, `IsVisible`)
> oraz workery menu „Przejmij" / „Zastąp właściciela" / „Nie przypominaj" / „Potwierdź" są
> **wewnętrzne** (internal). Publiczna droga to: lista tranzycji
> (`Task.GetListWfTransitionInternal`), metody `SetUserDecision`/`GoThru` oraz bezpośrednia edycja
> pól `Operator` / `TakeOverITaskUser` / `IsNotification` / `Progress` — i tak są opisane
> poniższe receptury. Tabela `Tasks` to dane **operacyjne** — wystarczy zwykła sesja i transakcja.

### WORKFLOW-F1 — Odczytanie szczegółów zadania — dostępne operacje (★)

**Cel:** dla aktywnego zadania workflow ustalić, **co operator może z nim zrobić**: jakie decyzje
(tranzycje) są dostępne, która jest domyślna, oraz jakie czynności (akcje) udostępnia kalkulator
zadania.

**Warianty:**

| Wariant | Droga publiczna |
|---|---|
| Decyzje użytkownika (tranzycje `IsUserDecision`) dostępne na zadaniu | `task.GetListWfTransitionInternal(isUserDecision: true)` → `SubTable` |
| Wszystkie wyjścia z węzła (także tranzycje automatyczne) | `task.GetListWfTransitionInternal(isUserDecision: null)` |
| Lista do kontrolki UI (lookup) | `task.GetListWFTransition()` → `Soneta.Business.LookupInfo` |
| Czy w ogóle można podejmować decyzję | `!task.IsReadOnlyUserDecision()` |
| Aktualnie podjęta (niezatwierdzona) decyzja | `task.UserDecision.Transition` (`null` = brak) |
| Czynności (akcje) zadania | `task.GetActionMethods(context)` → `ICollection<TaskAction>` |
| Warianty decyzji (tranzycja wielowariantowa) | wariant przekazuje się jako parametr `variant` przy podjęciu decyzji (WORKFLOW-F2, lista wariantów — WORKFLOW-C4) |

**Pola i typy:**

| Element | Typ / sygnatura | Uwagi |
|---|---|---|
| `Task.GetListWfTransitionInternal(bool? isUserDecision)` | `→ Soneta.Business.SubTable` | wiersze `WFTransition`; **odfiltrowane** są tranzycje niewidoczne (`IsVisible == false`) i zablokowane (`IsReadOnly == true`) dla tego zadania; `null` = bez filtra po `IsUserDecision` |
| `Task.GetListWFTransition()` | `→ Soneta.Business.LookupInfo` | wariant dla kontrolek UI |
| `Task.IsReadOnlyUserDecision()` | `→ bool` | `true` gdy: zadanie spoza workflow, nieaktywne, w trybie zewnętrznej obsługi błędów lub **nie należy do zalogowanego** (`Tasks.IsLoggedUserTask`) |
| `Task.UserDecision` | `(Soneta.Business.IWFTransition Transition, object Variant, Soneta.Business.Context Context)` | bieżąca, jeszcze niezatwierdzona decyzja |
| `Task.Definition.SourceWFTransitions` | `Soneta.Business.SubTable` | **wszystkie** tranzycje wychodzące z węzła (bez filtrowania dostępności) |
| `IWFTransition` | `Soneta.Business` (interfejs; implementacja: `Soneta.Workflow.Config.WFTransition`) | `Name`, `LP`, `IsUserDecision`, `IsDefaultTransition`, `WeakTransition`, `GetLocalizedName()`, `IsVisible(Task)`, `IsReadOnly(Task)` |
| `Task.GetActionMethods(Context context = null)` | `→ ICollection<Soneta.Business.Db.TaskAction>` | czynności kalkulatora zadania, posortowane po `Priority` |
| `TaskAction` | `Soneta.Business.Db` (abstrakcyjna, dziedziczy z `Action`) | `Name: string`, `Priority: int`, `IsVisible`/`IsEnabled: bool`, `Invoke(object instance, Context context)` |

**Snippet:**

```csharp
using Soneta.Business;
using Soneta.Business.Db;

// Aktywne zadanie workflow zalogowanego operatora (odczyt zadań — WORKFLOW-E1)
var tasks = session.GetBusiness().Tasks;
RowCondition rc = new FieldCondition.Null(nameof(Task.WFWorkflow), isNull: false);
rc &= new FieldCondition.Equal(nameof(Task.Progress), TaskProgress.Active);
Task zadanie = tasks.WgDefinition[rc].GetFirst();

if (zadanie != null && !zadanie.IsReadOnlyUserDecision())
{
    // 1. Dostępne decyzje użytkownika (widoczne i niezablokowane tranzycje)
    foreach (IWFTransition tranzycja in zadanie.GetListWfTransitionInternal(isUserDecision: true))
        Console.WriteLine(
            $"Decyzja: {tranzycja.GetLocalizedName()}" +
            $" | domyślna: {tranzycja.IsDefaultTransition}" +
            $" | nie kończy zadania: {tranzycja.WeakTransition}");

    // 2. Wszystkie wyjścia z węzła (też automatyczne) — pełen obraz przepływu
    foreach (IWFTransition t in zadanie.Definition.SourceWFTransitions)
        Console.WriteLine($"{t.Name} | decyzja użytkownika: {t.IsUserDecision}" +
                          $" | dostępna teraz: {t.IsVisible(zadanie) && !t.IsReadOnly(zadanie)}");

    // 3. Czynności (akcje) zadania — np. czynności kalkulatora węzła
    foreach (TaskAction akcja in zadanie.GetActionMethods())
        if (akcja.IsVisible)
            Console.WriteLine($"Akcja: {akcja.Name} | dostępna: {akcja.IsEnabled}");
}
```

**Pułapki:**
- Kolekcja `UserDecision` (z `Caption`, `IsEnabled`, `IsVisible`), którą posługuje się UI
  (np. worker „Podejmij decyzję"), jest typem **wewnętrznym** — z kodu dodatku operuj na
  **tranzycjach** (`IWFTransition`); podpis decyzji odtworzysz z `GetLocalizedName()` (dla
  tranzycji wielowariantowych podpis zawiera dodatkowo wariant — WORKFLOW-C4).
- `GetListWfTransitionInternal` mimo słowa „Internal" w nazwie jest **publiczną** metodą `Task` —
  to kanoniczne źródło „co mogę teraz wybrać". `Definition.SourceWFTransitions` zwraca także
  tranzycje aktualnie niedostępne — zawsze odróżniaj te dwie listy.
- `IsReadOnlyUserDecision()` zwraca `true` także dla **cudzego** zadania
  (`Tasks.IsLoggedUserTask(task) == false`) — kod działający „w imieniu" operatora widzi tylko
  jego możliwości; to celowe zachowanie zgodne z UI.
- O dostępności tranzycji decyduje jej kalkulator (`Statement`/`CheckCode` — WORKFLOW-C2):
  wynik `IsVisible`/`IsReadOnly` zależy od stanu danych i **może się zmienić** po każdej edycji —
  nie cache'uj listy między transakcjami.
- Kolejność decyzji w UI to `LP`, potem nazwa — sortowanie `SubTable` może być inne; jeżeli
  odtwarzasz menu, posortuj po `LP` samodzielnie.
- `GetActionMethods` bez `Context` zbuduje akcje w pustym kontekście — `Invoke` akcji
  wymagających UI (formularzy) wykonuj tylko w warstwie workera (worker-extender.md).

### WORKFLOW-F2 — Wykonanie konkretnego zadania (podjęcie decyzji) (★)

**Cel:** zrealizować zadanie workflow przez wybór tranzycji — silnik kończy bieżące zadanie
i tworzy zadania kolejnego węzła (albo zamyka proces).

**Mechanizm (kluczowy):** decyzję **ustawia się w transakcji**, a wykonuje ją silnik **przy
`Commit()`** (zdarzenie serwerowe przeliczenia zadania). Po `Commit()`: zadanie ma
`Progress == Realized` (chyba że tranzycja jest „nie kończąca" — `WeakTransition`), w
`proces.AllTasks` są zadania węzła docelowego (`Transition.Target`), a `ExecutionTime`/`Executor`
wskazują kto i kiedy zrealizował. Na końcu — `session.Save()`.

**Warianty:**

| Wariant | Droga publiczna |
|---|---|
| Po nazwie tranzycji | `task.SetUserDecision("Akceptuj")` lub skrót `task.GoThru("Akceptuj")` |
| Przez obiekt tranzycji | `task.UserDecision = (tranzycja, null, null)` |
| Z wariantem (tranzycja wielowariantowa) | `task.SetUserDecision("Decyzja", wariant)` — wariant to np. wiersz wskazany przez `VariantTypeName` (WORKFLOW-C4) |
| Z parametrami / kontekstem (QueryContext) | `task.SetUserDecision("Decyzja", null, context)` — `Context` z tej samej sesji, z ustawionymi obiektami parametrów |
| Wycofanie niezatwierdzonej decyzji | `task.UserDecision = (null, null, null)` (przed `Commit`) |
| Zgodność wsteczna | `task.WFTransition = tranzycja` (property delegująca do `UserDecision`) |
| Seryjnie na wielu zadaniach | WORKFLOW-F9 |

**Pola i typy:**

| Element | Typ / sygnatura | Uwagi |
|---|---|---|
| `Task.SetUserDecision(string transitionName, object variant = null, Context context = null)` | `void` | szuka tranzycji **po nazwie** wśród `Definition.SourceWFTransitions`; brak → `RowException` („Nie znaleziono tranzycji…") |
| `Task.GoThru(string transitionName)` | `void` | skrót dla `SetUserDecision(name)` |
| `Task.UserDecision` | `(IWFTransition, object, Context)` — settable | walidacje settera: patrz Pułapki |
| `Task.WFTransition` | `Soneta.Business.IWFTransition` (settable) | kompatybilność wsteczna; `get` = `UserDecision.Transition` |
| `Task.Progress` | `Soneta.Business.Db.TaskProgress` | po `Commit`: `Realized` (lub bez zmiany przy `WeakTransition`) |
| `Task.ExecutionTime` / `Executor` | `System.DateTime` / `Soneta.Business.ITaskUser` | uzupełniane przy realizacji |
| `WFWorkflow.AllTasks` / `ActiveTasks` | `SubTable<Task>` / `IEnumerable<Task>` | tu pojawiają się zadania węzła docelowego |
| `WFWorkflow.IsClosed` | `bool` | `true`, gdy tranzycja prowadziła do węzła końcowego (WORKFLOW-D6) |

**Snippet:**

```csharp
using Soneta.Business;
using Soneta.Business.Db;
using Soneta.Workflow;
using Soneta.Workflow.Config;   // metoda rozszerzająca StartProcess

// Wdrożona definicja procesu (zbudowana wg WORKFLOW01–03; w Demo definicji nie ma)
var definicja = session.GetWorkflow().WFDefs.WgSymbolu["URLOP"];

// 1. Start procesu — silnik tworzy WFWorkflow i pierwsze zadanie (WORKFLOW-D1)
Task zadanie = definicja.GetStartPoint().StartProcess(row: null, source: null);
session.Save();

// 2. Wybór decyzji — pierwsza dostępna decyzja użytkownika (WORKFLOW-F1)
IWFTransition decyzja = zadanie.GetListWfTransitionInternal(isUserDecision: true)
    .Cast<IWFTransition>()
    .FirstOrDefault();

// 3. Podjęcie decyzji: ustawienie w transakcji, realizacja przy Commit
using (var t = session.Logout(editMode: true))
{
    zadanie.GoThru(decyzja.Name);          // = SetUserDecision(decyzja.Name)
    t.Commit();                            // TU silnik wykonuje tranzycję
}
session.Save();

// 4. Skutki — po Commit
var proces = (WFWorkflow)zadanie.WFWorkflow;
Console.WriteLine($"Zadanie: {zadanie.Progress}");            // Realized
foreach (Task nowe in proces.ActiveTasks)                      // zadania węzła docelowego
    Console.WriteLine($"Nowe zadanie: {nowe.Name} | węzeł: {nowe.Definition.Name}");
```

**Pułapki:**
- Setter `UserDecision` **rzuca wyjątkiem**, gdy: zadanie jest spoza workflow
  (`ColReadOnlyException`), nieaktywne (`Progress != Active` — „Zadanie nie jest aktywne…"),
  w trybie zewnętrznej obsługi błędów (`ExternalErrorHandling`), tranzycja nie wychodzi z węzła
  zadania (`Transition.Source != task.Definition`) albo podano `variant`/`context` bez tranzycji.
- `SetUserDecision(name)` porównuje nazwę **ordinal, z uwzględnieniem wielkości liter** —
  literówka kończy się `RowException`; nazwy pobieraj z listy F1, nie wpisuj „na sztywno".
- Skutki decyzji (nowe zadania, `Realized`, zamknięcie procesu) **widać dopiero po `Commit()`** —
  asercje/odczyty rób poza transakcją. Wyjątek z algorytmów tranzycji (`CheckCode`/`ActionCode`)
  poleci właśnie w `Commit()`.
- `Context` przekazany do decyzji musi pochodzić **z tej samej sesji** co zadanie
  (`ArgumentOutOfRangeException`); klonuj go: `Context.Empty.Clone(task.Session)`.
- Tranzycja **`WeakTransition`** nie kończy zadania (zostaje `Active`, a mimo to powstają zadania
  węzła docelowego) — nie zakładaj `Realized` po każdej decyzji.
- Decyzję może podjąć tylko **właściciel** zadania w rozumieniu `Tasks.IsLoggedUserTask` —
  UI to wymusza (`IsReadOnlyUserDecision`); w kodzie wsadowym działasz na prawach zalogowanego
  operatora sesji (session-login.md).
- Po `Commit()` pamiętaj o `session.Save()` — bez niego zmiany nie trafią do bazy
  (blokada optymistyczna — safe-code.md).

### WORKFLOW-F3 — Zmiana stanu zadania (postępu) (★)

**Cel:** zmienić `Progress` zadania **własnego / powiadomienia** (zrealizuj, odrzuć, dezaktywuj)
oraz rozumieć, dlaczego dla zadania workflow ta droga jest zamknięta.

**Warianty:**

| Wariant | Jak |
|---|---|
| Zrealizowanie zadania własnego | `task.Progress = TaskProgress.Realized` |
| Odrzucenie | `task.Progress = TaskProgress.Aborted` |
| Dezaktywacja / zawieszenie | `task.Progress = TaskProgress.NonActive` / `TaskProgress.Waiting` |
| Zadanie workflow | **nie przez setter** — stan zmienia silnik po decyzji (WORKFLOW-F2) |
| Stan „efektywny" (z `IsActive` kalkulatora) | odczyt `task.ProgressWithIsActive` |

**Pola i typy:**

| Element | Typ | Uwagi |
|---|---|---|
| `Task.Progress` | `Soneta.Business.Db.TaskProgress` (`NonActive=0`, `Active=1`, `Waiting=2`, `Realized=3`, `Aborted=4`) | setter publiczny **tylko** dla `DefinitionType == DefinitionTypeEnum.None`; inaczej `ColReadOnlyException` |
| `Task.DefinitionType` | `Soneta.Business.Db.DefinitionTypeEnum` (`Standard=0`, `Engine=1`, `None=2`) | `None` = zadanie własne / powiadomienie (poza silnikiem workflow) |
| `Task.IsActiveProgress` | `bool` | `Active` lub `Waiting` |
| `Task.ProgressWithIsActive` | `TaskProgress` | dla `Waiting` pyta kalkulator `IsActive()` |
| `Task.ExecutionTime` / `Executor` / `ExecutorId` / `ExecutorType` | `DateTime` / `ITaskUser` / `int` / `string` | uzupełniane automatycznie przy `Realized`/`Aborted` (kto faktycznie wykonał — z uwzględnieniem zastępstw) |
| `Task(GuidedRow parent, TaskDefinition definition)` | publiczny konstruktor | zadanie własne (`DefinitionType.None`); przeciążenie z `ITaskUser` |
| `session.GetBusiness().TaskDefs.Standard` | `Soneta.Business.Db.TaskDefinition` | standardowa definicja „Zadanie" (dostępna w każdej bazie) |
| `TaskDefinition.DeleteOnRealized` | `Soneta.Business.Db.DeleteOnRealized` (`BeforeValidDate` / `Never` / `Always`) | patrz Pułapki |

**Snippet:**

```csharp
using Soneta.Business;
using Soneta.Business.Db;

var business = session.GetBusiness();

// Rodzic — dowolny GuidedRow z Demo (pobierany dynamicznie)
GuidedRow rodzic = session.GetCRM().Kontrahenci.WgKodu.GetFirst();

Task zadanie;
using (var t = session.Logout(editMode: true))
{
    // Zadanie własne: standardowa definicja "Zadanie" => DefinitionType.None
    zadanie = session.AddRow(new Task(rodzic, business.TaskDefs.Standard));
    zadanie.Name = "Zadzwonić do klienta";
    zadanie.Operator = session.Login.Operator;
    t.Commit();
}
session.Save();

// Zmiana stanu — publiczny setter działa, bo DefinitionType == None
using (var t = session.Logout(editMode: true))
{
    zadanie.Progress = TaskProgress.Realized;
    t.Commit();
}
session.Save();

Console.WriteLine($"{zadanie.Progress} | wykonał: {zadanie.Executor} o {zadanie.ExecutionTime}");
```

**Pułapki:**
- Dla zadania **workflow** (`DefinitionType != None`) setter `Progress` rzuca
  `ColReadOnlyException` — to nie błąd, tylko kontrakt: stan zmieniaj decyzją (WORKFLOW-F2);
  anulowanie całego procesu — WORKFLOW-D7.
- Przy przejściu z `Active` na `Realized`/`Aborted`/`NonActive` definicja może wymusić
  **fizyczne usunięcie** zadania zamiast zmiany stanu (`DeleteOnRealized.Always`, a
  `BeforeValidDate` — gdy `ValidFrom > Date.Today`). Po `Commit` sprawdź `task.Status`,
  zanim odczytasz pola.
- Gdy zadanie staje się nieaktywne, platforma **kasuje powiązane harmonogramy** i zamyka
  (`Terminate`) ewentualny podproces — skutki uboczne są częścią settera, nie omijaj go
  „ręczną" edycją innych pól.
- W UI pole bywa dodatkowo zablokowane (`IsReadOnlyProgress()`: brak prawa do zadań innych
  operatorów, brak prawa modyfikacji) — kod wsadowy działa na prawach operatora sesji.
- `Waiting` to stan „oczekujący" sterowany kalkulatorem — na listach „aktualnych" zadań używaj
  `IsActiveProgress`/`ProgressWithIsActive`, nie samego `Progress == Active`.

### WORKFLOW-F4 — Przypisanie zadania sobie (z puli roli / węzła) (★)

**Cel:** przejąć do realizacji zadanie przypisane do **roli** lub **węzła struktury
organizacyjnej** (wspólna pula) — odpowiednik czynności **„Przejmij"** — oraz zwrócić je do puli
(czynność **„Zwróć"**).

**Mechanizm:** zadanie puli ma `OperatorRoleType == Role` (lub `Node`) i `Operator == null`.
Przejęcie = ustawienie `Operator` na zalogowanego operatora (**bez** zmiany `OperatorRoleType` —
zadanie nadal „należy" do roli, ale ma konkretnego wykonawcę). Zwrot = `Operator = null`.
Workery menu („Przejmij", „Zwróć") są wewnętrzne — publiczną drogą jest edycja pól.

**Warianty:**

| Wariant | Warunek wyjściowy | Operacja |
|---|---|---|
| Przejęcie z puli roli | `OperatorRoleType == Role`, `Operator == null`, `Tasks.IsLoggedUserTask(task)` | `task.Operator = session.Login.Operator` |
| Przejęcie z puli węzła org. | `OperatorRoleType == Node`, `Operator == null` | jw. |
| Zwrot do puli | `OperatorRoleType ∈ {Role, Node}`, `Operator == ja`, `TakeOverITaskUser == null` | `task.Operator = null` |

**Pola i typy:**

| Element | Typ | Uwagi |
|---|---|---|
| `Task.OperatorRoleType` | `Soneta.Business.Db.OperatorRoleType` (`Operator`/`Role`/`Other`/`Node`) | sposób przypisania (model — nagłówek WORKFLOW05) |
| `Task.Operator` | `Soneta.Business.App.Operator` (settable) | wykonawca; `null` = zadanie w puli |
| `Task.RoleGuid` / `Task.RoleName` | `System.Guid` / `string` | rola, do której należy zadanie |
| `Task.Node` | `Soneta.Business.IElementStrukturyOrganizacyjnej` | węzeł struktury organizacyjnej |
| `Task.ResponsibleName` | `string` | podpis „odpowiedzialnego" (operator albo rola/węzeł) |
| `Tasks.IsLoggedUserTask(Task)` | `static bool` | czy zadanie należy (też przez rolę/węzeł) do zalogowanego |

**Snippet:**

```csharp
using Soneta.Business;
using Soneta.Business.Db;

var tasks = session.GetBusiness().Tasks;

// Aktywne zadanie z puli roli, należące (przez rolę) do zalogowanego operatora
RowCondition rc = new FieldCondition.Equal(nameof(Task.Progress), TaskProgress.Active);
rc &= new FieldCondition.Equal(nameof(Task.OperatorRoleType), OperatorRoleType.Role);
rc &= new FieldCondition.Null(nameof(Task.Operator), isNull: true);

Task zadanie = tasks.WgDefinition[rc].ToArray<Task>()
    .FirstOrDefault(Tasks.IsLoggedUserTask);

if (zadanie != null)
{
    // Przejęcie z puli ("Przejmij")
    using (var t = session.Logout(editMode: true))
    {
        zadanie.Operator = session.Login.Operator;
        t.Commit();
    }
    session.Save();
    Console.WriteLine($"Wykonawca: {zadanie.Operator} | rola: {zadanie.RoleName}");

    // Zwrot do puli ("Zwróć")
    using (var t = session.Logout(editMode: true))
    {
        zadanie.Operator = null;
        t.Commit();
    }
    session.Save();
}
```

**Pułapki:**
- **Nie zmieniaj `OperatorRoleType`** przy przejęciu — zadanie ma pozostać zadaniem roli/węzła
  z wypełnionym wykonawcą; dzięki temu `Operator = null` zwraca je do puli. (Zmiana typu
  przypisania jest zresztą zablokowana — `IsReadOnlyOperatorRoleType()` dla ról/węzłów.)
- Setter `Operator` **nic nie robi**, gdy `TakeOverITaskUser != null` (zadanie przejęte
  w zastępstwie — WORKFLOW-F5) — najpierw rozlicz przejęcie, potem zmieniaj wykonawcę.
- Ustawienie `Operator = null` automatycznie **zeruje `TakeOverITaskUser`**.
- Przejęcie cudzą rolą: warunek `Tasks.IsLoggedUserTask` to filtr po stronie klienta — żeby
  pobrać kandydatów serwerowo, użyj `Tasks.IOrgStructureHelper.GetMyTasksCondition`
  (WORKFLOW-E1) i dołóż warunki z tabeli wariantów.
- Widoczność czynności w UI ogranicza prawo proste „Zadania innych operatorów"
  (`AccessToOtherOperatorsTasks`) — kod wsadowy go nie sprawdza; zachowaj spójność z prawami.
- Pełny wzorzec transakcji: `Logout(editMode: true)` → `Commit()` → `session.Save()`;
  brak `Commit()` = rollback (session-login.md).

### WORKFLOW-F5 — Przejęcie zadania od innego operatora (zastępstwo) (★)

**Cel:** przejąć zadanie przypisane do **konkretnego, innego operatora** (czynność
**„Zastąp właściciela"**), przekazać własne zadanie zastępcy (**„Przekaż do zastępcy"**)
i zwrócić je pierwotnemu właścicielowi (**„Zwróć zadanie"**).

**Mechanizm:** pierwotny właściciel zostaje zapamiętany w polu `TakeOverITaskUser`,
a `Operator` (lub `TaskUser` dla użytkowników pulpitów) wskazuje **aktualnego wykonawcę**:

| Krok | `Operator` | `TakeOverITaskUser` |
|---|---|---|
| przed przejęciem | pierwotny właściciel | `null` |
| po przejęciu / przekazaniu | przejmujący (np. ja albo zastępca) | pierwotny właściciel |
| po zwrocie | pierwotny właściciel | `null` |

**Pola i typy:**

| Element | Typ | Uwagi |
|---|---|---|
| `Task.TakeOverITaskUser` | `Soneta.Business.ITaskUser` (settable; iface-ref: `Operator`, `Pracownik`, `KontaktOsoba`, …) | pierwotny właściciel przejętego zadania; `null` = brak przejęcia |
| `Task.TakeOverOperator` | `Soneta.Business.App.Operator` | wariant „operatorowy" — getter/setter delegują do `TakeOverITaskUser` |
| `Task.Operator` / `Task.TaskUser` | `Operator` / `ITaskUser` | aktualny wykonawca (operator / użytkownik zewnętrzny) |
| `Task.OperatorRoleType` | `OperatorRoleType` | po przejęciu przez operatora: `Operator` |
| `Operator.AccessToOtherOperatorsTasks()` | `→ Soneta.Business.AccessRights` | prawo proste warunkujące operacje na cudzych zadaniach |

**Snippet:**

```csharp
using Soneta.Business;
using Soneta.Business.Db;

var tasks = session.GetBusiness().Tasks;
var ja = session.Login.Operator;

// Aktywne zadanie innego operatora, jeszcze nie przejęte
Task zadanie = tasks.WgDefinition[
        new FieldCondition.Equal(nameof(Task.Progress), TaskProgress.Active)
        & new FieldCondition.Equal(nameof(Task.OperatorRoleType), OperatorRoleType.Operator)
        & new FieldCondition.NotEqual(nameof(Task.Operator), ja)
        & new FieldCondition.Null(nameof(Task.TakeOverITaskUser), isNull: true)]
    .GetFirst();

if (zadanie != null)
{
    // "Zastąp właściciela" — przejęcie zadania dla siebie
    using (var t = session.Logout(editMode: true))
    {
        ITaskUser pierwotny = zadanie.TaskUser ?? zadanie.Operator;
        zadanie.Operator = ja;                       // NAJPIERW wykonawca…
        zadanie.OperatorRoleType = OperatorRoleType.Operator;
        zadanie.TakeOverITaskUser = pierwotny;       // …POTEM ślad przejęcia
        t.Commit();
    }
    session.Save();

    // "Zwróć zadanie" — przywrócenie pierwotnego właściciela
    using (var t = session.Logout(editMode: true))
    {
        var pierwotny = zadanie.TakeOverITaskUser;
        zadanie.TakeOverITaskUser = null;            // NAJPIERW wyzeruj przejęcie…
        zadanie.Operator = pierwotny as Soneta.Business.App.Operator; // …potem właściciel
        t.Commit();
    }
    session.Save();
}
```

**Pułapki:**
- **Kolejność jest istotna.** Setter `Operator` jest **ignorowany**, dopóki
  `TakeOverITaskUser != null` — przy zwrocie najpierw `TakeOverITaskUser = null`, dopiero potem
  `Operator = pierwotny`. Przy przejęciu odwrotnie: najpierw `Operator`, na końcu
  `TakeOverITaskUser` (jego setter wymaga niepustego `Operator`/`TaskUser` — inaczej
  `RowException`).
- Pole bazodanowe w skanie ma opis „użytkownik, który przejmuje zadanie" — **semantyka w kodzie
  platformy jest odwrotna**: `TakeOverITaskUser` przechowuje **pierwotnego właściciela**,
  a wykonawcą jest `Operator`/`TaskUser`. Tak działają czynności „Zastąp właściciela",
  „Przekaż do zastępcy" i „Zwróć zadanie".
- Przekazanie zastępcy to ta sama sekwencja z `zadanie.Operator = zastepca` zamiast `ja`;
  pierwotny właściciel może być typu innego niż `Operator` (np. `Pracownik` z pulpitu) —
  wtedy przy zwrocie ustaw `TaskUser` i `OperatorRoleType = OperatorRoleType.Other`.
- Zadań **roli/węzła** nie przejmuje się tą drogą (tam obowiązuje WORKFLOW-F4 — pula);
  czynności zastępstwa są w UI ukryte dla `OperatorRoleType ∈ {Role, Node}`.
- Operacja w UI wymaga prawa prostego „Zadania innych operatorów" (`Granted`) — kod wsadowy
  praw nie sprawdza, ale nie obchodź nimi polityki uprawnień (safe-code.md).
- `TakeOverOperator` to wygodny wariant typowany na `Operator` (kompatybilność) — nowy kod
  powinien używać `TakeOverITaskUser`.

### WORKFLOW-F6 — Odrzucenie zadania (powiadomienia) — „Nie przypominaj" (★)

**Cel:** odrzucić zadanie-przypomnienie (powiadomienie **spoza** workflow) tak, by zniknęło
z „dzwonka" i panelu — odpowiednik czynności **„Nie przypominaj"**.

**Mechanizm:** czynność dotyczy wyłącznie zadań z `IsNotification == true`,
`WFWorkflow == null` i `Progress == Active`; jej istotą jest **wyłączenie przypomnienia**:
`IsNotification = false` (zeruje pole `Notification`). Zadanie pozostaje `Active` — nie jest
realizowane ani usuwane.

**Pola i typy:**

| Element | Typ | Uwagi |
|---|---|---|
| `Task.IsNotification` | `bool` (kalkulowane, zapisywalne) | `false` zeruje `Notification`; szczegóły pól przypomnienia — WORKFLOW-E5 |
| `Task.Notification` | `System.DateTime` | `DateTime.MinValue` = brak przypomnienia |
| `Task.WFWorkflow` | `Soneta.Business.IWFWorkflow` | musi być `null` (powiadomienie, nie zadanie procesowe) |
| `Task.Progress` | `TaskProgress` | dla pełnego „odrzucenia" zadania własnego: `TaskProgress.Aborted` (WORKFLOW-F3) |

**Snippet:**

```csharp
using Soneta.Business.Db;

var tasks = session.GetBusiness().Tasks;

// Pierwsze wymagalne przypomnienie operatora (WORKFLOW-E5)
Task powiadomienie = tasks.GetRemainders()
    .FirstOrDefault(z => z.WFWorkflow == null && z.Progress == TaskProgress.Active);

if (powiadomienie != null)
{
    using (var t = session.Logout(editMode: true))
    {
        powiadomienie.IsNotification = false;   // "Nie przypominaj"
        // pełne odrzucenie zadania własnego (opcjonalnie):
        // powiadomienie.Progress = TaskProgress.Aborted;
        t.Commit();
    }
    session.Save();
}
```

**Pułapki:**
- „Nie przypominaj" **nie kończy zadania** — tylko wyłącza przypomnienie. Jeśli zadanie własne
  ma zostać odrzucone merytorycznie, ustaw dodatkowo `Progress = TaskProgress.Aborted`
  (WORKFLOW-F3).
- Zadań **workflow** (`WFWorkflow != null`) nie odrzuca się tą drogą — ich przypomnieniami
  steruje silnik (pole jest w UI tylko do odczytu), a „odrzucenie" realizuje się odpowiednią
  tranzycją (WORKFLOW-F2) albo anulowaniem procesu (WORKFLOW-D7).
- Oznaczenie powiadomienia jako „przeczytane" (per użytkownik) jest obsługiwane przez
  **wewnętrzne** API platformy (robi to UI przy otwarciu formularza) — publicznie dostępny jest
  tylko odczyt flagi `TaskNotificationIsNotRead`.
- Czynność w UI bywa niedostępna w wersji 365 (na liście) — to ograniczenie interfejsu,
  nie publicznego API.

### WORKFLOW-F7 — Potwierdzenie powiadomienia / zadania informacyjnego (★)

**Cel:** potwierdzić zadanie **informacyjne** (powiadomienie systemowe / „ping" użytkownika,
generowane np. przez procesy lub harmonogram) — odpowiednik czynności **„Potwierdź"** —
czyli wykonać akcję zadania i oznaczyć je jako zrealizowane.

**Mechanizm (jak czynność platformy):** w jednej transakcji: `IsNotification = false`
(wyłączenie przypomnienia) → `RunAction()` (wykonanie akcji z definicji zadania, o ile
jeszcze niezrealizowane) → `Progress = TaskProgress.Realized`.

**Pola i typy:**

| Element | Typ / sygnatura | Uwagi |
|---|---|---|
| `Task.RunAction()` | `void` | uruchamia akcję kalkulatora zadania (algorytm/akcja definicji), jeżeli zadanie nie jest jeszcze zrealizowane |
| `Task.IsNotification` | `bool` | jw. (WORKFLOW-F6) |
| `Task.Progress` | `TaskProgress` | setter publiczny — zadania informacyjne mają `DefinitionType == None` |
| `Task.Definition` | `Soneta.Business.Db.TaskDefinition` | zadania „ping" używają standardowej definicji platformy (`TaskDefs.Standard`) |
| `Task.ExecutionTime` / `Executor` | `DateTime` / `ITaskUser` | uzupełniane przy `Realized` |

**Snippet:**

```csharp
using Soneta.Business.Db;

var business = session.GetBusiness();

// Aktywne powiadomienie standardowej definicji ("ping" użytkownika)
Task powiadomienie = business.Tasks.WgDefinition[business.TaskDefs.Standard]
    .ToArray<Task>()
    .FirstOrDefault(z => z.IsActiveProgress && z.WFWorkflow == null);

if (powiadomienie != null)
{
    using (var t = session.Logout(editMode: true))
    {
        powiadomienie.IsNotification = false;            // 1. wyłącz przypomnienie
        powiadomienie.RunAction();                       // 2. wykonaj akcję zadania
        powiadomienie.Progress = TaskProgress.Realized;  // 3. potwierdź (zrealizuj)
        t.Commit();
    }
    session.Save();
}
```

**Pułapki:**
- Zachowaj **kolejność** jak w czynności platformy: `RunAction()` przed `Progress = Realized` —
  po realizacji `RunAction()` nic już nie zrobi (sprawdza, czy zadanie niezrealizowane).
- `Progress = Realized` może — zależnie od `DeleteOnRealized` definicji — **usunąć** zadanie
  zamiast je zrealizować (WORKFLOW-F3); standardowa definicja platformy zwykle realizuje.
- Czynność „Potwierdź" w UI jest pokazywana tylko dla zadań **standardowej definicji
  powiadomienia** — dla innych zadań własnych używaj WORKFLOW-F3 (zmiana stanu), a dla zadań
  workflow — decyzji (WORKFLOW-F2).
- `RunAction()` wykonuje algorytm definiowany w konfiguracji — może modyfikować dane; traktuj
  go jak każdy `Commit`-owalny efekt uboczny (wyjątki algorytmu lecą tu, nie w `Commit`).

### WORKFLOW-F8 — Uruchomienie kreatora przypisanego do zadania

**Cel:** uruchomić **kreator** (wizard) obsługujący zadanie — okno krokowe, którym operator
realizuje zadanie (wprowadza dane, podejmuje decyzję) — odpowiednik czynności **„Uruchom"**
(Ctrl+R) na zadaniu.

**Charakter:** to operacja **warstwy UI** — wynikiem jest `FormActionResult` (definicja okna
kreatora), który musi skonsumować formularz/worker. W czystym kodzie wsadowym kreatora się nie
„wykonuje" — decyzję podejmuje się wprost (WORKFLOW-F2). Konfigurację kreatora na węźle opisuje
WORKFLOW-B5.

**Warianty:**

| Wariant | Droga publiczna |
|---|---|
| Sprawdzenie, czy zadanie ma kreator do uruchomienia | `WizardTools.CheckWizardStart(task, throwException: false)` |
| Uruchomienie kreatora zadania (w workerze) | `WizardTools.StartWizard(task, context)` → `object` (`FormActionResult`) |
| Uruchomienie kreatora o znanej definicji | `definicjaKreatora.StartWizard(context, task)` → `FormActionResult` |
| Czynność platformy | worker `Soneta.Workflow.Workers.OpenWizardOnTaskWorker` (publiczny), akcja „Uruchom" |

**Pola i typy:**

| Element | Typ / sygnatura | Uwagi |
|---|---|---|
| `WizardTools` | `static class`, `Soneta.Business.Db.Wizard` | narzędzia kreatorów |
| `WizardTools.CheckWizardStart(Task task, bool throwException)` | `→ bool` | `throwException: true` rzuca wyjątek z przyczyną odmowy |
| `WizardTools.StartWizard(GuidedRow row, Context context)` | `→ object` | dla `Task` buduje kreator zadania; wynik: `FormActionResult` albo sam wiersz (gdy brak kreatora) |
| `WizardDefinition.StartWizard(Context context, Task task = null)` | `→ Soneta.Business.UI.FormActionResult` | metoda rozszerzająca; dodaje do kontekstu `task`, `task.WFWorkflow`, `task.Parent` |
| `Task.WizardDefinition` | `Soneta.Business.Db.Wizard.WizardDefinition` | definicja kreatora powiązana z zadaniem |
| `OpenWizardOnTaskWorker` | `Soneta.Workflow.Workers` (publiczna) | property `[Context] Task`, `[Context] Context`; akcja `OpenWizard(Context, UILocation)` |

**Snippet (worker / extender — warstwa UI):**

```csharp
using Soneta.Business;
using Soneta.Business.Db;
using Soneta.Business.Db.Wizard;
using Soneta.Business.UI;

// Kontekst workera: Task + Context (worker-extender.md)
public object UruchomKreator(Task task, Context context)
{
    // Czy zadanie ma kreator i można go wystartować (bez wyjątku)?
    if (!WizardTools.CheckWizardStart(task, throwException: false))
        return null;

    // Wynik (FormActionResult) zwracamy z akcji workera — UI otworzy okno kreatora
    return WizardTools.StartWizard(task, context);
}
```

**Pułapki:**
- **Nie wywołuj** `StartWizard` w kodzie wsadowym/serwerowym bez UI — zwrócony
  `FormActionResult` nie ma kto obsłużyć; do automatyzacji służy `SetUserDecision`
  (WORKFLOW-F2) z ewentualnym `Context` parametrów.
- `StartWizard` wymaga `Context` **z tej samej sesji** co zadanie i rzuca `RowException`,
  gdy definicja kreatora nie ma kroków albo kalkulator jej zabrania (`IsEnable`).
- Zadanie bez kreatora (`CheckWizardStart == false`, brak `WizardDefinition` i kreatorów
  węzła) zwróci z `StartWizard(GuidedRow, Context)` **sam wiersz** zamiast formularza —
  sprawdzaj typ wyniku.
- W pulpitach (web) definicje kreatorów z zakładkami spoza dozwolonych typów rzucają
  `BusException` — kreator musi być oznaczony jako dozwolony w pulpitach.
- Dla powiadomień i zadań nieaktywnych kreator się nie uruchamia (czynność jest ukryta).

### WORKFLOW-F9 — Seryjne podejmowanie decyzji na wielu zadaniach (★)

**Cel:** podjąć **tę samą decyzję** (tranzycję o tej samej nazwie) na wielu zadaniach naraz —
jak czynność listy **„Podejmij decyzję"** na zaznaczonych wierszach Panelu Workflow.

**Warianty:**

| Wariant | Droga publiczna |
|---|---|
| Seryjnie z kodu (po nazwie) | `new TransitionsForTasksWorker().DoWfTransition(tasks, "Akceptuj")` w transakcji |
| Własna pętla (pełna kontrola) | `foreach (...) task.SetUserDecision(name)` w jednej transakcji |
| Czynność UI z dialogiem wyboru decyzji | worker `TransitionsForTasksWorker`, akcja `AddTransitionForTasks()` → `FormActionResult` (dialog) |

**Pola i typy:**

| Element | Typ / sygnatura | Uwagi |
|---|---|---|
| `TransitionsForTasksWorker` | `Soneta.Workflow.Workers` (publiczna) | property `[Context] Task[] Tasks`, `[Context] Context Context` |
| `TransitionsForTasksWorker.DoWfTransition(Task[] tasks, string name)` | `void` | pętla `SetUserDecision(name)` po zadaniach; rzuca `ArgumentNullException` / `ArgumentOutOfRangeException` przy pustych argumentach |
| `TransitionsForTasksWorker.AddTransitionForTasks()` | `→ object` (`FormActionResult`) | wariant interaktywny: waliduje zadania i otwiera dialog wyboru wspólnej decyzji |
| `WFTransition.IncompatibleWithSerialOperations` | `bool` | tranzycja wykluczona z operacji seryjnych (np. wymaga indywidualnych parametrów) |
| `Tasks.IsLoggedUserTask(Task)` | `static bool` | warunek workera dla każdego zadania |

**Snippet:**

```csharp
using Soneta.Business;
using Soneta.Business.Db;
using Soneta.Workflow.Config;
using Soneta.Workflow.Workers;

var tasks = session.GetBusiness().Tasks;
var definicja = session.GetWorkflow().WFDefs.WgSymbolu["URLOP"];

// Wszystkie moje aktywne zadania tego samego węzła (ta sama definicja zadania
// gwarantuje, że tranzycja o danej nazwie istnieje na każdym z nich)
Task[] doDecyzji = tasks.WgDefinition[
        new FieldCondition.Equal(nameof(Task.Progress), TaskProgress.Active)
        & new FieldCondition.Equal(
            $"{nameof(Task.Definition)}.{nameof(TaskDefinition.WFDefinition)}", definicja)]
    .ToArray<Task>()
    .Where(Tasks.IsLoggedUserTask)
    .Where(z => z.Definition.Name == "Akceptacja wniosku")
    .ToArray();

// Wspólna decyzja — z pominięciem tranzycji niezgodnych z operacjami seryjnymi
WFTransition decyzja = doDecyzji.First()
    .GetListWfTransitionInternal(isUserDecision: true)
    .Cast<WFTransition>()
    .First(t => !t.IncompatibleWithSerialOperations);

using (var t = session.Logout(editMode: true))
{
    new TransitionsForTasksWorker().DoWfTransition(doDecyzji, decyzja.Name);
    t.Commit();                       // jeden Commit realizuje wszystkie tranzycje
}
session.Save();
```

**Pułapki:**
- `DoWfTransition` to czysta pętla `SetUserDecision` — **sam nie filtruje** tranzycji
  `IncompatibleWithSerialOperations` ani nie sprawdza właściciela; zrób to przed wywołaniem
  (UI robi to w `MultiSourceDecisionManager`, który jest wewnętrzny). Tranzycje z parametrami
  (QueryContext) seryjnie nie zadziałają poprawnie bez indywidualnego `Context`.
- Wszystkie zadania muszą mieć tranzycję **o tej samej nazwie** wychodzącą z ich węzła —
  inaczej `SetUserDecision` rzuci `RowException` w połowie pętli; najprościej brać zadania
  jednej `TaskDefinition`.
- Wariant interaktywny (`AddTransitionForTasks`) **waliduje twardo** (`BusException`): zadania
  spoza workflow, cudze, nieaktywne, z kreatorem do uruchomienia, brak wspólnych decyzji —
  używaj go tylko w warstwie UI (zwraca dialog `FormActionResult`).
- Jeden `Commit()` uruchamia silnik dla **wszystkich** zadań — przy dużych partiach rośnie czas
  transakcji i ryzyko konfliktu blokady optymistycznej; dziel na rozsądne paczki i zapisuj
  (`session.Save()`) między nimi.
- Wyjątek algorytmu jednej tranzycji w `Commit()` wycofuje **całą** transakcję (wszystkie
  decyzje) — obsłuż go i ponów paczkę bez wadliwego zadania.
- Seryjny **start** procesów dla wielu obiektów to osobna receptura — WORKFLOW-D8
  (analogiczny filtr `Definition.IncompatibleWithSerialOperations`).
