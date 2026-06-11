# WORKFLOW02 — Węzły procesu (definicje zadań)

> Wspólne fakty o typie, podstawowe typy i szablon wzorca: [../workflow.md](../workflow.md).

> **Model „węzeł = definicja zadania".** Węzeł procesu workflow to wiersz
> `Soneta.Business.Db.TaskDefinition` (tabela `TaskDefs`, **konfiguracyjna**, moduł
> `BusinessModule` — `session.GetBusiness().TaskDefs`). Uruchomiony egzemplarz węzła to `Task`
> (WORKFLOW05/06). Węzeł jest „przypięty" do definicji procesu polem `WFDefinition`
> (kolekcja zwrotna `WFDefinition.TaskDefs`), a jego **rodzaj** (start, zadanie, decyzja,
> powiadomienie, koniec, podproces…) określa wzorzec elementu `WFDefItem`
> (`Soneta.Workflow.Config.WFDefItem`, tabela `WFDefItems` — standardowe wzorce: `Start`,
> `Zadanie`, `Decyzja`, `Akcja`, `Powiadomienie`, `Koniec`, `Podproces`).
>
> Klucze: `TaskDefs.ByName[tablename]` (podzbiór) i `TaskDefs.ByName[tablename, name]`
> (pojedynczy wiersz), `TaskDefs.WgWFDefinition[definicja]`, `TaskDefs.WgWFDefItem[wzorzec]`.
> Modyfikacja wymaga **sesji konfiguracyjnej** (`login.CreateSession(readOnly: false,
> config: true, …)`); w testach na `TestBase`: `ConfigEditSession` + `InConfigTransaction(...)` +
> `SaveDisposeConfig()`. Scenariusze buduj na własnej definicji (WORKFLOW-A1) — nie zakładaj
> istnienia definicji procesów w bazie Demo.

### WORKFLOW-B1 — Utworzenie węzła w konkretnym procesie workflow (★)

**Cel:** dodać do definicji procesu nowy węzeł (definicję zadania) — startowy, pośredni lub
końcowy — operujący na wskazanej tabeli obiektów biznesowych.

**Mechanizm (kluczowy):** konstruktor `new TaskDefinition(tablename)` wymaga **nazwy tabeli**
obiektu nadrzędnego (`Parent` przyszłych zadań, np. `"Kontrahenci"`, `"DokHandlowe"`, `"Tasks"`).
Po `AddRow` ustaw **najpierw** `WFDefinition` (dokłada figurę węzła na diagram, przejmuje
`DefinitionType` z definicji i automatycznie przypisuje **pierwszą rolę procesową** do
`ProcessRole`), a **potem** `WFDefItem` (wzorzec elementu) — setter `WFDefItem` sam nadaje
unikalne `Name`, opisowe `FormatedName` („Definicja – Wzorzec"), `IsStart`, kod akcji i ewentualny
kreator wzorca. Na końcu możesz nadpisać `Name`/`FormatedName` własnymi.

**Warianty:**

| Wariant | Jak skonfigurować | Charakterystyka |
|---|---|---|
| Węzeł startowy | `WFDefItem = ByName["Start"]` (ustawia `IsStart = true`) | początek procesu — WORKFLOW-B2 |
| Węzeł pośredni (zadanie/decyzja) | `WFDefItem = ByName["Zadanie"]` / `ByName["Decyzja"]` | kończony tranzycjami (`EndType = WFTransitions`) |
| Węzeł końcowy | `WFDefItem = ByName["Koniec"]` | wymusza zakończenie całego procesu (`EndType = Workflow`) |
| Akcja / powiadomienie | `WFDefItem = ByName["Akcja"]` / `ByName["Powiadomienie"]` | nie wymusza zakończenia wątku (`EndType = None`) |
| Podproces | `WFDefItem = ByName["Podproces"]` + `SubprocessDef` | uruchamia proces zależny (start podprocesu — WORKFLOW04) |
| Węzeł wielozadaniowy (multitask) | `MultiTaskType = ByGetTaskUsers` / `ByGetSysNotifications` | jedno przejście tworzy zadania dla wielu wykonawców — WORKFLOW-B3 |

**Pola i typy (`Soneta.Business.Db.TaskDefinition`, tabela `TaskDefs`, konfiguracyjna):**

| Pole | Typ | Opis |
|---|---|---|
| `TableName` | `string` | tabela obiektu nadrzędnego — ustawiana **konstruktorem** (readonly); `"?"` = dowolna (`AnyTable`) |
| `Name` | `string` | nazwa techniczna (unikalna w ramach definicji; klucz `ByName[tablename, name]`) |
| `FormatedName` | `string` | nazwa wyświetlana (etykieta figury na diagramie) |
| `WFDefinition` | `Soneta.Business.IWFDefinition` (iface-ref) | definicja procesu-właściciel |
| `WFDefItem` | `Soneta.Business.IWFDefItem` (iface-ref) | wzorzec elementu workflow (rodzaj węzła) |
| `ProcessRole` | `Soneta.Business.IWFProcessRole` (iface-ref) | rola procesowa (tor) — WORKFLOW-B3 |
| `IsStart` | `bool` | węzeł startowy (WORKFLOW-B2) |
| `EndType` | `Soneta.Business.Db.TaskEndTypeEnum` | `WFTransitions` / `None` / `Workflow` — **setter tylko dla definicji Engine**; w Standard wynika z `WFDefItem` (odczyt: `IsEndTypeNone`, `IsEndTypeWorkflow`) |
| `MultiTaskType` | `Soneta.Business.Db.MultiTaskType` | `NoMultiTask` / `ByGetTaskUsers` / `ByGetSysNotifications` |
| `SubprocessDef` | `Soneta.Business.IWFDefinition` (iface-ref) | definicja podprocesu (wariant „Podproces") |
| `DeleteOnRealized` | `Soneta.Business.Db.DeleteOnRealized` | los zadania po realizacji: `BeforeValidDate` / `Never` / `Always` |
| `MakeParentReadOnlyFor` | `Soneta.Business.Db.MakeParentReadOnlyFor` | blokada edycji obiektu nadrzędnego: `None` / `AnyExceptOwner` / `Any` |
| `Locked` | `bool` | węzeł zablokowany (pomijany przez silnik) |
| `IncompatibleWithSerialOperations` | `bool` | wyklucza węzeł z operacji seryjnych |
| `Description` | `string` | opis |
| `ActiveTask` | `bool` (wyliczane) | czy węzeł ma jak się zakończyć (tranzycja niesłaba albo `EndType` None/Workflow) |

**Snippet:**

```csharp
// Węzły to dane KONFIGURACYJNE — wymagana sesja konfiguracyjna:
using (var session = login.CreateSession(readOnly: false, config: true, name: "Węzły procesu"))
{
    var business = session.GetBusiness();
    var workflow = session.GetWorkflow();
    var definicja = workflow.WFDefs.WgSymbolu["URLOP"];   // WORKFLOW-A1

    using (var t = session.Logout(editMode: true))
    {
        // Węzeł startowy na tabeli Kontrahenci:
        var start = session.AddRow(new TaskDefinition("Kontrahenci"));
        start.WFDefinition = definicja;                              // NAJPIERW definicja
        start.WFDefItem = workflow.WFDefItems.ByName["Start"];       // potem wzorzec (ustawia IsStart)
        start.FormatedName = "Rejestracja wniosku";

        // Węzeł pośredni (zadanie operatora):
        var akceptacja = session.AddRow(new TaskDefinition("Kontrahenci"));
        akceptacja.WFDefinition = definicja;
        akceptacja.WFDefItem = workflow.WFDefItems.ByName["Zadanie"];
        akceptacja.FormatedName = "Akceptacja przełożonego";

        // Węzeł końcowy (wymusza zakończenie procesu):
        var koniec = session.AddRow(new TaskDefinition("Kontrahenci"));
        koniec.WFDefinition = definicja;
        koniec.WFDefItem = workflow.WFDefItems.ByName["Koniec"];
        koniec.FormatedName = "Koniec";

        t.Commit();
    }
    session.Save();

    // Odczyt węzłów definicji:
    foreach (TaskDefinition wezel in business.TaskDefs.WgWFDefinition[definicja])
    {
        // wezel.FormatedName, wezel.IsStart, wezel.IsEndTypeWorkflow, wezel.ActiveTask
    }
}
```

**Pułapki:**
- **Kolejność ma znaczenie:** najpierw `WFDefinition`, potem `WFDefItem`, na końcu własne
  `Name`/`FormatedName` — setter `WFDefItem` **nadpisuje** `Name`, `FormatedName`, `IsStart`,
  kod akcji i `EnableCondition` wartościami wzorca.
- `TableName` ustawia wyłącznie konstruktor — węzła nie da się „przepiąć" na inną tabelę;
  utwórz nowy. Wartość `"?"` (dowolna tabela) wyłącza automatyczny `TaskTrigger`.
- `OnAdded` automatycznie dokłada domyślny `TaskTrigger` (wyzwalacz na tabeli `TableName`) i
  ustawia `IsVisibleInScheduler = true` — nie twórz triggera ręcznie (WORKFLOW09).
- `EndType` da się ustawić setterem **tylko w definicjach Engine** — w definicji Standard typ
  końca wynika z `WFDefItem` (`Koniec` ⇒ `Workflow`, `Akcja`/`Powiadomienie` ⇒ `None`).
- Weryfikator ostrzega przy węźle **automatycznym** na tabelach `Tasks`, `TaskDefs`, `WFDefs`,
  `WFWorkflows` (ryzyko rekurencji silnika) — na tych tabelach buduj tylko węzły manualne
  (wyjątek: definicja eskalacyjna na `Tasks` — WORKFLOW-B7).
- Węzeł bez tranzycji wyjściowych i z `EndType = WFTransitions` nie ma jak się zakończyć
  (`ActiveTask == false`) — proces „utknie"; sprawdź `ActiveTask` przed wdrożeniem definicji.
- Usunięcie węzła odpina go od tranzycji (ich `Source`/`Target` dostają `null`) — tranzycje
  zostają jako „wiszące"; sprzątaj je razem z węzłem (WORKFLOW03).
- `Locked = true` na węźle powiązanym tranzycjami rzuca błąd weryfikatora — najpierw odepnij
  tranzycje.

### WORKFLOW-B2 — Konfiguracja węzła startowego procesu (★)

**Cel:** wskazać punkt startowy procesu i sposób jego uruchamiania (z menu dokumentu /
automatycznie na zdarzenie / w panelu workflow).

**Mechanizm:** o starcie decyduje para pól `IsStart` + `ActionRunAt`. Punkt startowy do startu
manualnego zwraca `WFDefinition.GetStartPoint(): TaskDefinition` — pierwszy węzeł spełniający
`!Locked && IsStart && ActionRunAt == ActionRunAt.InMenu`. Start automatyczny (silnik, na
`OnAdded` wiersza źródłowego) wybiera węzły `IsStart && ActionRunAt == Auto` przez ich
`TaskTrigger`y — i tylko z definicji `IsDeployed && !Locked` (WORKFLOW-A7, WORKFLOW09).

**Warianty:**

| Wariant | Konfiguracja (definicja Standard) | Charakterystyka |
|---|---|---|
| Start z menu / panelu | `IsStart = true`, `ActionRunAt = InMenu` (tak ustawia wzorzec `Start`) | operator uruchamia proces czynnością; widoczność steruje `ShowInToolbar` / `ShowInListMenu` / `ShowInListToolbar` |
| Start automatyczny | `IsStart = true`, `ActionRunAt = Auto` | silnik startuje proces przy dodaniu wiersza tabeli `TableName` (przez `TaskTrigger`) |
| Węzeł niestartowy | `IsStart = false`, `ActionRunAt = Default` | uruchamiany wyłącznie tranzycją |

**Pola i typy:**

| Pole | Typ | Opis |
|---|---|---|
| `IsStart` | `bool` | element startowy; **setter rzuca `ColReadOnlyException` w definicji Engine** |
| `ActionRunAt` | `Soneta.Business.Db.ActionRunAt` | `Auto` („Automatyczna") / `InMenu` („W menu") / `Default`; setter także zablokowany w Engine |
| `StartPointType` | `Soneta.Business.Db.TaskStartPointTypeEnum` (`[Flags]`) | **tylko definicje Engine**: `NoStartPoint` / `Automatic` / `InWorkflowPanel` / `InDocumentMenu` / `Subprocess`; setter sam uzgadnia `IsStart` i `ActionRunAt` |
| `ShowInToolbar`, `ShowInListMenu`, `ShowInListToolbar` | `bool` | gdzie widać czynność startu; **zapisywalne wyłącznie w definicji Engine + `ActionRunAt = InMenu`** — w definicji Standard są read-only (`IsReadOnlyShowInToolbar()`), próba zapisu rzuca `ColReadOnlyException` |
| `EnableCondition` | `string` | algorytm „Warunek utworzenia" — kiedy zadanie startowe wolno utworzyć |

**Snippet:**

```csharp
using (var session = login.CreateSession(readOnly: false, config: true, name: "Węzeł startowy"))
{
    var workflow = session.GetWorkflow();
    var definicja = workflow.WFDefs.WgSymbolu["URLOP"];

    using (var t = session.Logout(editMode: true))
    {
        var start = session.AddRow(new TaskDefinition("Kontrahenci"));
        start.WFDefinition = definicja;
        start.WFDefItem = workflow.WFDefItems.ByName["Start"];   // IsStart=true, ActionRunAt=InMenu
        start.FormatedName = "Start procesu";

        // wariant: start automatyczny przy dodaniu kontrahenta —
        // start.ActionRunAt = ActionRunAt.Auto;                 // (definicja Standard)
        t.Commit();
    }
    session.Save();

    // Punkt startowy do startu manualnego (rzuca NotSupportedException, gdy brak):
    TaskDefinition punktStartu = definicja.GetStartPoint();
    bool jestStartem = punktStartu.IsStart && punktStartu.ActionRunAt == ActionRunAt.InMenu;
}
```

**Pułapki:**
- **Tylko jeden „ręczny" węzeł startowy** na definicję Standard — weryfikator
  (`IsStartAllowedVerifier`) odrzuca drugi węzeł `IsStart` z `ActionRunAt = InMenu` w tej samej
  definicji (definicje Engine mogą mieć wiele punktów startu).
- W definicji **Engine** settery `IsStart` i `ActionRunAt` rzucają `ColReadOnlyException` —
  używaj `StartPointType` (flagi), który sam uzgadnia oba pola.
- `GetStartPoint()` zwraca wyłącznie węzeł startu **manualnego** (`ActionRunAt == InMenu`) i
  rzuca `NotSupportedException`, gdy go nie ma — węzeł startu automatycznego nie jest „widziany"
  przez tę metodę.
- Start automatyczny zadziała dopiero na definicji **wdrożonej** (`IsDeployed && !Locked`,
  WORKFLOW-A7) i niezablokowanym węźle; w trybie modelowania testuj start manualny
  (`StartProcess` — WORKFLOW04).
- Widoczność czynności startu (`ShowInToolbar`/`ShowInListMenu`/`ShowInListToolbar`) jest
  zapisywalna **tylko w definicji Engine** przy `ActionRunAt = InMenu` (warunek
  `IsReadOnlyShowInToolbar() => IsReadOnly() || ActionRunAt != InMenu || DefinitionType != Engine`).
  W definicji **Standard** te pola są read-only — sterowanie widocznością odbywa się tam pośrednio
  (`ShowInToolbar` zwraca `ShowInMenu`); jawny zapis rzuca `ColReadOnlyException`.

### WORKFLOW-B3 — Określenie wykonawcy węzła (operator / rola / węzeł organizacyjny) (★)

**Cel:** wskazać, kto dostanie zadanie utworzone z węzła: konkretny operator, rola operatorów,
element struktury organizacyjnej, wykonawca wyliczany wyrażeniem albo wielu wykonawców naraz
(multitask).

**Mechanizm:** wykonawcę wyznacza w pierwszej kolejności **rola procesowa** węzła
(`ProcessRole` — WORKFLOW-A3): jeżeli jej `ExecutorType != TaskExecutor` („Wskazany na
zadaniu"), to rola w całości decyduje o wykonawcy, a pola węzła są ignorowane (w UI stają się
readonly). Dopiero przy roli typu `TaskExecutor` działają pola węzła: `OperatorType` + `Operator`
/ `RoleGuid` / `OperatorExpression`. Po ustawieniu `WFDefinition` węzeł dostaje automatycznie
**pierwszą** rolę procesową definicji. Na egzemplarzu wykonawcę odzwierciedlają
`Task.OperatorRoleType` (`Operator`/`Role`/`Other`), `Task.Operator`, `Task.TaskUser`.

**Warianty (pola węzła, gdy rola = `TaskExecutor`):**

| `OperatorType` (`Soneta.Business.Db.TaskOperatorType`) | Znaczenie | Pole towarzyszące |
|---|---|---|
| `Current` („Aktualny") | operator zalogowany w chwili tworzenia zadania | — |
| `Manual` („Wybrany") | konkretny operator | `Operator` |
| `Created` / `Modified` | twórca / ostatnio modyfikujący dokument nadrzędny | — |
| `Expression` („Wyrażenie…") | operator wyliczany wyrażeniem od `Parent` | `OperatorExpression` |
| `Role` („Rola") | wszyscy operatorzy roli | `RoleGuid` / `RoleName` |

**Pola i typy:**

| Pole | Typ | Opis |
|---|---|---|
| `ProcessRole` | `Soneta.Business.IWFProcessRole` (iface-ref) | rola procesowa (tor) węzła; domyślnie pierwsza rola definicji |
| `OperatorType` | `Soneta.Business.Db.TaskOperatorType` | sposób wyznaczenia operatora (tabela wyżej) |
| `Operator` | `Soneta.Business.App.Operator` | operator (dla `Manual`) |
| `RoleGuid` / `RoleName` | `System.Guid` / `string` | rola operatorów (dla `Role`); `RoleName` mapuje nazwę na `RoleGuid` |
| `OperatorExpression` | `string` | wyrażenie wyznaczające operatora (dla `Expression`) |
| `Node` | `Soneta.Business.IElementStrukturyOrganizacyjnej` (iface-ref) | element struktury organizacyjnej |
| `OrgStructure` | `Soneta.Business.IStrukturaOrganizacyjna` (iface-ref) | struktura organizacyjna |
| `MultiTaskType` | `Soneta.Business.Db.MultiTaskType` | `ByGetTaskUsers` = wiele zadań wg algorytmu/listy odbiorców |
| `GetTaskUsers` | `string` | kod metody `GetTaskUsers(): IEnumerable<ITaskUser>` (multitask) |
| odbiorcy multitask | `Soneta.Workflow.Config.WFRecipient` (tabela `WFRecipients`) | lista odbiorców: `new WFRecipient(taskDef) { TaskUser = … }`; odczyt `WFRecipients.WgHost[taskDef]` |

**Snippet:**

```csharp
using (var session = login.CreateSession(readOnly: false, config: true, name: "Wykonawca węzła"))
{
    var workflow = session.GetWorkflow();
    var definicja = workflow.WFDefs.WgSymbolu["URLOP"];
    var wezel = session.GetBusiness().TaskDefs.WgWFDefinition[definicja]
        .First(td => td.FormatedName == "Akceptacja przełożonego");

    using (var t = session.Logout(editMode: true))
    {
        // 1) Wykonawca przez rolę procesową (tor) — np. rola "Przełożony" z WORKFLOW-A3:
        wezel.ProcessRole = definicja.ProcessRoles.First(r => r.Name == "Przełożony");

        // 2) Albo wykonawca wskazany na węźle (rola procesowa typu TaskExecutor):
        var inny = session.GetBusiness().TaskDefs.WgWFDefinition[definicja]
            .First(td => td.FormatedName == "Rejestracja wniosku");
        inny.OperatorType = TaskOperatorType.Manual;
        inny.Operator = session.Login.Operator;          // konkretny operator

        // 3) Multitask — zadanie dla listy odbiorców:
        wezel.MultiTaskType = MultiTaskType.ByGetTaskUsers;
        var odbiorca = new WFRecipient(wezel) { TaskUser = session.Login.Operator };
        workflow.WFRecipients.AddRow(odbiorca);

        t.Commit();
    }
    session.Save();
}
```

**Pułapki:**
- **Rola procesowa ma pierwszeństwo:** przy `ProcessRole.ExecutorType != TaskExecutor` pola
  `OperatorType`/`Operator`/`RoleGuid` węzła są ignorowane — najczęstsza przyczyna „zadanie
  trafiło nie do tego operatora".
- Setter `OperatorType` **czyści** pola niepasujące do trybu (`Operator = null` poza `Manual`,
  `RoleName = ""` poza `Role`) — ustawiaj `OperatorType` przed `Operator`/`RoleName`.
- `RoleName` na `TaskDefinition` rzuca `ArgumentOutOfRangeException` dla nieznanej nazwy roli
  (inaczej niż `WFProcessRole.RoleName`, które po cichu zeruje `RoleGuid`).
- Odbiorców multitask (`WFRecipient`) trzeba dodać do tabeli (`WFRecipients.AddRow`) —
  konstruktor wiąże tylko hosta. Usunięcie węzła kasuje jego odbiorców kaskadowo.
- `MultiTaskType = ByGetTaskUsers` bez listy odbiorców i bez kodu `GetTaskUsers` nie utworzy
  żadnego zadania — uzupełnij jedno z dwóch źródeł wykonawców.
- `TaskUser` (`ITaskUser`) to referencja interfejsowa — może wskazywać `Operator`, `Pracownik`,
  `KontaktOsoba` itd.; w scenariuszach na Demo pobieraj istniejące rekordy dynamicznie.

### WORKFLOW-B4 — Źródło danych węzła i obiekt nadrzędny

**Cel:** określić, na jakim obiekcie biznesowym pracuje zadanie węzła (`Task.Parent`), skąd ten
obiekt pochodzi (istniejący wiersz / wygenerowany schematem OGSchema) i który wiersz „zarządza"
procesem (`WFWorkflow.ManagingRow`).

**Mechanizm:** bazowym źródłem jest tabela z konstruktora (`TableName`) — zadanie węzła dostaje
jako `Parent` wiersz tej tabeli (przekazany przy starcie procesu albo wyliczony z poprzedniego
zadania). Wybór wiersza można przejąć algorytmem `GetParentExpression`/`GetParentCode`, a wiersz
zarządzający — `GetManagingRowCode`. Gdy węzeł ma generować nowy obiekt, wskaż schemat
`OGSchema` (WORKFLOW03) i `InitParent = true` — zadanie zostanie zainicjowane wierszem
utworzonym przez generator. Dla definicji **Engine** istnieje dodatkowo jawne „źródło zadania":
`Soneta.Workflow.Config.WfAdvTaskSource` (tabela `WfTaskSources`) — mapuje tabelę źródłową na
definicję zadania i pozwala startować proces z wierszy nie będących `GuidedRow`.

**Pola i typy:**

| Pole | Typ | Opis |
|---|---|---|
| `TableName` | `string` | tabela obiektu nadrzędnego (konstruktor; readonly) |
| `GetParentExpression` | `string` | wyrażenie „Wybór wiersza" — buduje metodę wyboru `Parent` dla zadania |
| `GetParentCode` | `string` | pełny kod metody wyboru wiersza (alternatywa dla wyrażenia) |
| `GetManagingRowCode` | `string` | kod metody wyboru wiersza zarządzającego procesem (`IManagingRow`) |
| `OGSchema` | `Soneta.Business.IOGSchema` (iface-ref) | schemat generatora obiektów (definicja mapowania — WORKFLOW03) |
| `InitParent` | `bool` | inicjuj zadanie wierszem utworzonym przez generator |
| `ParentType` / `ObjTable` | `System.Type` / `Soneta.Business.Table` | typ wiersza nadrzędnego / tabela (odczyt) |

**Pola `WfAdvTaskSource` (`Soneta.Workflow.Config`, tabela `WfTaskSources`, konfiguracyjna, tylko definicje Engine):**

| Pole | Typ | Opis |
|---|---|---|
| `TaskDefinition` | `Soneta.Business.Db.TaskDefinition` | definicja zadania-właściciel (konstruktor) |
| `TableName` | `string` | tabela źródłowa (konstruktor) |
| `OGSchema` | `Soneta.Workflow.Config.OGSchema` | schemat generatora budujący `Parent` ze źródła |
| `VariantTypeName` | `string` | typ źródła wielowariantowego |

**Snippet:**

```csharp
using (var session = login.CreateSession(readOnly: false, config: true, name: "Źródło węzła"))
{
    var definicja = session.GetWorkflow().WFDefs.WgSymbolu["URLOP"];
    var wezel = session.GetBusiness().TaskDefs.WgWFDefinition[definicja]
        .First(td => td.FormatedName == "Rejestracja wniosku");

    using (var t = session.Logout(editMode: true))
    {
        // Wiersz zadania wybierany wyrażeniem od obiektu źródłowego (prefiks Row.):
        wezel.GetParentExpression = "Row";          // ten sam wiersz, na którym wystartowano

        // Węzeł generujący obiekt: schemat generatora + inicjacja wierszem generatora:
        // wezel.OGSchema = session.GetWorkflow().OGSchemas.GetFirst();   // WORKFLOW03
        // wezel.InitParent = true;

        t.Commit();
    }
    session.Save();

    // Diagnostyka źródła:
    Table tabela = wezel.ObjTable;                  // np. Kontrahenci
    Type typWiersza = wezel.ParentType;             // np. Soneta.CRM.Kontrahent

    // Źródło zadania definicji Engine (WfTaskSources, klucz po definicji + tabeli):
    var zrodlo = session.GetWorkflow().WfTaskSources.WgTaskDefinition[wezel, "Kontrahenci"];
}
```

**Pułapki:**
- `GetParentExpression` **nie robi round-tripu 1:1**: setter osadza wyrażenie w pełnej metodzie
  `GetParent(...)` (zapisywanej w `GetParentCode`), a getter wyciąga tekst między `return` a `;`
  (`GetReturnExpression`) — więc dla `"Row"` getter zwróci wyrażenie z prefiksem rzutowania
  (`"(Soneta.Business.GuidedRow)Row"`), nie surowe `"Row"`. Trwały, weryfikowalny fakt: po zapisie
  wyrażenie jest osadzone w `GetParentCode`. (`GetParentExpression` jest read-only tylko gdy
  `Algorithm || IsStandardDef`, a `IsStandardDef` ⇔ `DefinitionType == None && TableName == "?"` —
  zwykła definicja Standard/Engine ma je zapisywalne.) Również `ObjTable.Name` to wewnętrzna nazwa
  tabeli (rdzeń `"Kontrahen…"`), niekoniecznie równa `TableName` — tożsamość pewniej sprawdzaj `ParentType`.
- `GetParentExpression`/`GetParentCode` i pozostałe algorytmy węzła to **kod kompilowany przez
  platformę** (`AlgorithmColumn`) — po zmianach kodu baza przebudowuje assembly; traktuj jak
  każdy algorytm konfiguracji (safe-code: bez efektów ubocznych).
- `OGSchema` na węźle jest readonly, dopóki węzeł nie należy do definicji workflow — najpierw
  `WFDefinition`, potem schemat. Sam schemat generatora i jego mapowanie pól to WORKFLOW03
  (na tranzycji) — węzeł tylko wskazuje definicję schematu.
- `InitParent` bez `OGSchema` nie ma czego zainicjować — para pól działa razem.
- `WfAdvTaskSource` można utworzyć **wyłącznie dla definicji Engine**
  (`new WfAdvTaskSource(taskDefinition, tablename)`, sesja konfiguracyjna). Wykonanie źródła
  (executor `WeTaskSource`) jest częścią frameworku silnika kodowego — programowo używa go
  `StartProcess`/`StartProcessUI` (WORKFLOW04), nie wywołuje się go wprost.
- Klucz `WfTaskSources.WgTaskDefinition[definicjaZadania, tableName]` zwraca pojedyncze źródło —
  para (definicja, tabela) jest unikalna.

### WORKFLOW-B5 — Podpięcie kreatora (wizard) do węzła

**Cel:** sprawić, żeby realizacja zadania węzła prowadziła operatora przez kreator (kroki z
zakładkami, listami, wydrukami) zamiast surowego formularza zadania.

**Mechanizm:** kreator to `Soneta.Business.Db.Wizard.WizardDefinition` (tabela `WizardDefs`) z
krokami `WizardStepDefinition` (tabela `WizardStepDefs`). Do węzła podpina się go wierszem
łączącym `Soneta.Business.Db.Wizard.WizardReference` — `new WizardReference(taskDef) { Wizard =
definicjaKreatora }` (konstruktor wiąże hosta `IWizardReferenceHost`). Kreatory węzła:
`WizardsRef: LpSubTable<WizardReference>` (kolejność po `Lp`); `AdditionalWizardsRef:
SubTable<WizardReference>` to kreatory innych hostów wycelowane w ten węzeł (pole
`WizardReference.TaskDefinition`). W UI istnieje też czynność „Utwórz kreator z jednym krokiem"
(publiczny worker `Soneta.Workflow.Workers.CreateSimpleWizardWorker` — czynność interaktywna,
`QueryContextInformation`), a uruchomienie kreatora na zadaniu — czynność „Uruchom" (publiczny
worker `Soneta.Workflow.Workers.OpenWizardOnTaskWorker`, WORKFLOW06).

**Pola i typy:**

| Pole | Typ | Opis |
|---|---|---|
| `WizardsRef` | `Soneta.Business.LpSubTable<Soneta.Business.Db.Wizard.WizardReference>` | kreatory podpięte do węzła |
| `AdditionalWizardsRef` | `Soneta.Business.SubTable<Soneta.Business.Db.Wizard.WizardReference>` | referencje kreatorów wskazujące węzeł polem `TaskDefinition` |
| `WizardDefinition` | `Soneta.Business.Db.Wizard.WizardDefinition` | definicja kreatora powiązana z definicją zadania (readonly w typowych trybach — podpinaj przez `WizardReference`) |
| `WizardInstruction` | `Soneta.Business.MemoText` | instrukcja wyświetlana w kreatorze |
| `InterfaceMode` | `Soneta.Business.Db.TaskInterfaceModeEnum` | `Default` / `SelectMode` (lista) / `WizardMode` („Zapisz i zamknij") / `WizardModeSave`; skuteczny tryb: `GetInterfaceMode()` (dziedziczy z definicji procesu), skrót `WizardMode: bool` |
| `WizardReference.Wizard` | `WizardDefinition` | wskazywany kreator |
| `WizardReference.Priority` | `int` | priorytet przy wielu kreatorach |

**Snippet:**

```csharp
using (var session = login.CreateSession(readOnly: false, config: true, name: "Kreator węzła"))
{
    var business = session.GetBusiness();
    var definicja = session.GetWorkflow().WFDefs.WgSymbolu["URLOP"];
    var wezel = business.TaskDefs.WgWFDefinition[definicja]
        .First(td => td.FormatedName == "Akceptacja przełożonego");

    using (var t = session.Logout(editMode: true))
    {
        // Definicja kreatora z jednym krokiem (zakładka standardowa obiektu nadrzędnego):
        var kreator = session.AddRow(new WizardDefinition(wezel.TableName));
        kreator.Name = "Akceptacja wniosku";
        kreator.FormattedName = "Akceptacja wniosku";

        var krok = session.AddRow(new WizardStepDefinition());
        krok.WizardDefinition = kreator;
        krok.Name = "Dane wniosku";
        krok.StepType = WizardStepType.Page;

        // Podpięcie kreatora do węzła (host = definicja zadania):
        session.AddRow(new WizardReference(wezel) { Wizard = kreator });

        wezel.WizardInstruction = "Sprawdź dane wniosku i podejmij decyzję.";
        t.Commit();
    }
    session.Save();

    // Kreatory węzła (porządek po Lp):
    foreach (WizardReference wr in wezel.WizardsRef)
    {
        // wr.Wizard.Name, wr.Priority
    }
}
```

**Pułapki:**
- **Wzorzec elementu może sam podpiąć kreator:** setter `WFDefItem` z wzorcem mającym
  `DefinitionWizard` automatycznie dokłada `WizardReference`; żeby tego uniknąć, użyj
  `taskDef.SetWfDefItemWithoutWizardReference(wzorzec)`. Sprawdzaj `WizardsRef` przed dodaniem
  własnej referencji — łatwo o duplikat.
- `WizardDefinition` tworzony jest dla **typu danych** (`new WizardDefinition(tableName)`) —
  użyj tej samej tabeli co węzeł, inaczej kroki nie zobaczą obiektu nadrzędnego zadania.
- Worker `CreateSimpleWizardWorker` to czynność **interaktywna** (parametry + `QueryContext` dla
  kroków typu lista/definiowany) — programowo szybciej zbudować `WizardDefinition` +
  `WizardStepDefinition` wprost, jak w snippecie.
- Uruchomienie kreatora (`OpenWizardOnTaskWorker.OpenWizard`) to czynność **UI** na zadaniu —
  zwraca obiekt kreatora do prezentacji; nie jest drogą do „wykonania" zadania w kodzie
  (od tego są decyzje — WORKFLOW06).
- O tym, czy zadanie otwiera się w trybie kreatora, decyduje `GetInterfaceMode()` — wartość
  `Default` na węźle dziedziczy tryb z definicji procesu; nie czytaj samego `InterfaceMode`.

### WORKFLOW-B6 — Kopiowanie definicji zadania (węzła) (★)

**Cel:** utworzyć kopię pojedynczego węzła (z algorytmami, ustawieniami wykonawcy i kreatorami)
z nowym `Guid` i unikalną nazwą — np. drugi podobny krok w tym samym procesie.

**Mechanizm:** publiczny worker `Soneta.Workflow.Workers.CopyTaskDefinitionWorker` (czynność
„Kopiuj definicję zadania" na definicji zadania). Metoda `CopyTaskDefinition()` kopiuje wiersz
przez serializację XML (rozszerzenie `MakeCopy` z `Soneta.Business.Generic`): nadaje nowy `Guid`
i wymusza unikalność pary (`TableName`, `Name`); kopia pozostaje w tej samej definicji procesu.
Worker **sam otwiera transakcję** i robi `Commit` — wystarczy `session.Save()`.

**Pola i typy:**

| Element | Typ | Opis |
|---|---|---|
| `Row` | `Soneta.Business.Db.TaskDefinition` | węzeł źródłowy (właściwość workera `RowBasedWorker<TaskDefinition>`) |
| `CopyTaskDefinition()` | `TaskDefinition` | wykonuje kopię i zwraca nowy wiersz |
| `IsVisibleCopyTaskDefinition(TaskDefinition)` | `bool` (statyczna) | warunek czynności: wiersz nie-Detached z określoną tabelą (`ObjTable != null`) |

**Snippet:**

```csharp
using (var session = login.CreateSession(readOnly: false, config: true, name: "Kopia węzła"))
{
    var definicja = session.GetWorkflow().WFDefs.WgSymbolu["URLOP"];
    var wezel = session.GetBusiness().TaskDefs.WgWFDefinition[definicja]
        .First(td => td.FormatedName == "Akceptacja przełożonego");

    var worker = new CopyTaskDefinitionWorker { Row = wezel };
    TaskDefinition kopia = worker.CopyTaskDefinition();   // worker sam otwiera transakcję i robi Commit
    session.Save();

    // kopia.Guid != wezel.Guid; kopia.Name unikalna w (TableName, Name); ta sama definicja procesu
    using (var t = session.Logout(editMode: true))
    {
        kopia.FormatedName = "Akceptacja dyrektora";       // własna etykieta kopii
        t.Commit();
    }
    session.Save();
}
```

**Pułapki:**
- `CopyTaskDefinition()` **sam zarządza transakcją** — nie opakowuj wywołania własnym
  `Logout(true)`.
- Kopiowany jest **pojedynczy węzeł** — tranzycje wchodzące/wychodzące źródła nie są kopiowane;
  kopię trzeba samodzielnie wpiąć w przepływ (WORKFLOW03). Pełną definicję z węzłami i
  tranzycjami kopiuje `WFDefinitionCopyWorker` (WORKFLOW-A5).
- Unikalność wymuszana jest na parze (`TableName`, `Name`) — kopia może dostać dopisek w `Name`;
  identyfikuj ją po zwróconej referencji, nie po nazwie.
- Czynność wymaga sesji konfiguracyjnej edycyjnej i węzła z określoną tabelą
  (`ObjTable != null`) — węzła „dowolna tabela" (`TableName == "?"`) nie skopiujesz tym workerem.
- Po skopiowaniu węzła startowego sprawdź weryfikator pojedynczego startu (WORKFLOW-B2) — dwie
  definicje `IsStart` + `InMenu` w jednej definicji procesu nie przejdą walidacji.

### WORKFLOW-B7 — Terminy zadania i obsługa przeterminowania (eskalacja)

**Cel:** skonfigurować terminy zadania (rozpoczęcie, zakończenie, przypomnienie) oraz reakcję
procesu na przeterminowanie — eskalację, czyli automatyczne uruchomienie dodatkowego węzła
obsługi po przekroczeniu wyliczonego czasu.

**Mechanizm:** terminy zadania wyliczają wyrażenia węzła (`StartExpression`,
`EndDateExpression`, `ValidFromExpression`, `NotificationExpression` + `NotificationType`/
`NotificationTime`). Eskalację włącza `OverdueHandling = true`: przy tworzeniu/przeliczaniu
zadania silnik planuje (przez harmonogram, `ScheduleDefs`) wykonanie w czasie zwracanym przez
algorytm `GetOverdueTime()` (budowany z `OverdueTimeExpression`); po przekroczeniu terminu
uruchamiany jest węzeł wskazany w `OverdueServiceType` — **definicja zadania na tabeli `Tasks`**,
której `Parent` to przeterminowane zadanie.

**Pola i typy:**

| Pole | Typ | Opis |
|---|---|---|
| `StartExpression` / `EndDateExpression` | `string` | wyrażenia daty rozpoczęcia / zakończenia zadania (prefiks `Row.` — obiekt nadrzędny) |
| `ValidFromExpression` | `string` | data, od której zadanie jest ważne/widoczne |
| `NotificationType` | `Soneta.Business.Db.NotificationType` | `None` / `BeforeStart` / `BeforeEnd` / `Expression` — kiedy przypominać |
| `NotificationTime` / `NotificationExpression` | `Soneta.Types.Time` / `string` | czas przypomnienia / wyrażenie terminu przypomnienia |
| `OverdueHandling` | `bool` | włącza obsługę eskalacji |
| `OverdueServiceType` | `Soneta.Business.Db.TaskDefinition` | węzeł obsługi eskalacji — musi działać na tabeli `Tasks` i nie może wskazywać samego siebie |
| `OverdueTimeExpression` | `string` | wyrażenie „Data i czas eskalacji" — buduje `GetOverdueTime(): DateTime`; puste ⇒ `DateTime.MaxValue` (nigdy) |
| `ScheduleDefs` | `Soneta.Business.SubTable` | definicje harmonogramu powiązane z węzłem (planowanie eskalacji) |

**Snippet:**

```csharp
using (var session = login.CreateSession(readOnly: false, config: true, name: "Eskalacja węzła"))
{
    var workflow = session.GetWorkflow();
    var definicja = workflow.WFDefs.WgSymbolu["URLOP"];
    var wezel = session.GetBusiness().TaskDefs.WgWFDefinition[definicja]
        .First(td => td.FormatedName == "Akceptacja przełożonego");

    using (var t = session.Logout(editMode: true))
    {
        // Węzeł obsługi eskalacji — MUSI działać na tabeli Tasks (Parent = przeterminowane zadanie):
        var eskalacja = session.AddRow(new TaskDefinition("Tasks"));
        eskalacja.WFDefinition = definicja;
        eskalacja.WFDefItem = workflow.WFDefItems.ByName["Powiadomienie"];
        eskalacja.FormatedName = "Eskalacja — zadanie przeterminowane";

        // Terminy i eskalacja na węźle pilnowanym:
        wezel.OverdueHandling = true;
        wezel.OverdueServiceType = eskalacja;
        wezel.OverdueTimeExpression = "DateTime.Now.AddDays(3)";   // eskalacja po 3 dniach

        t.Commit();
    }
    session.Save();
}
```

**Pułapki:**
- Weryfikator eskalacji odrzuca zapis, gdy `OverdueHandling = true` a: `OverdueServiceType` jest
  pusty, wskazuje **samego siebie** albo jego tabelą nie jest `Tasks` — najpierw zbuduj węzeł
  eskalacyjny, potem włączaj flagę.
- Puste `OverdueTimeExpression` ⇒ `GetOverdueTime()` zwraca `DateTime.MaxValue` — eskalacja
  formalnie włączona, ale nigdy nie nastąpi; zawsze podaj wyrażenie terminu.
- `OverdueServiceType` i `OverdueTimeExpression` są readonly przy `OverdueHandling == false` oraz
  na definicjach standardowych (`IsStandard`) — ustawiaj pola w tej kolejności: najpierw flaga,
  potem wskazanie i wyrażenie.
- Faktyczne wykonanie eskalacji wymaga działającego **harmonogramu zadań** (usługa planująca,
  `ScheduleDefs`) — w środowisku bez harmonogramu (np. test jednostkowy) skonfigurujesz pola, ale
  eskalacja nie „wystrzeli"; testuj konfigurację, nie upływ czasu.
- Wyrażenia terminów (`StartExpression`, `OverdueTimeExpression`, …) liczone są od obiektu
  nadrzędnego (`Row.` = `Task.Parent`) i kompilowane przez platformę — nieistniejąca ścieżka
  property wyjdzie dopiero przy kompilacji algorytmów, nie przy zapisie pola.
