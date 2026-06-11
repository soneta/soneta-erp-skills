# WORKFLOW03 — Tranzycje i przepływ procesu

> Wspólne fakty o typie, podstawowe typy i szablon wzorca: [../workflow.md](../workflow.md).

> **Model „tranzycja = kalkulator".** Tranzycja `Soneta.Workflow.Config.WFTransition` (tabela
> `WFTransitions`, **konfiguracyjna** — edycja wymaga sesji `login.CreateSession(readOnly: false,
> config: true, …)`, jak w WORKFLOW01) łączy dwa węzły (`Source`/`Target: TaskDefinition`)
> w obrębie jednej definicji (`WfDefinition`). Z kodu tranzycji (pola `Statement`, `CheckCode`,
> `ActionCode`) platforma **kompiluje klasę kalkulatora** dziedziczącą z publicznej
> `Soneta.Workflow.Config.WFTransitionCalculator` o kontrakcie:
>
> | Metoda | Znaczenie |
> |---|---|
> | `bool IsRealized(Task task)` | **warunek przejścia** — czy tranzycja ma się zrealizować |
> | `bool IsVisible(Task task)` | czy decyzja jest widoczna na liście decyzji operatora |
> | `bool IsReadOnly(Task task)` | czy decyzja jest zablokowana (widoczna, ale niedostępna) |
> | `void Check(...)` | walidacja **przed** przejściem — wyjątek przerywa realizację i zapis |
> | `void Action(...)` | efekty uboczne **po** pozytywnym `Check` |
>
> Cykl realizacji (silnik, przy przeliczaniu zadania — np. po `Task.Recalculate()` lub zmianie
> decyzji): `IsRealized == true` → `Check` (w trybie tylko-do-odczytu) → `Action` → zadanie
> źródłowe przechodzi w `Realized` (chyba że `WeakTransition`) i powstaje `Task` węzła `Target`.
> Klasa tranzycji musi odpowiadać typowi definicji: `WFTransition` (Standard) /
> `WFTransitionExtend` (Engine) — analogicznie jak `WFDefinition`/`WFDefinitionExtend`
> (WORKFLOW-A2).

### WORKFLOW-C1 — Utworzenie tranzycji pomiędzy węzłami procesu (★)

**Cel:** połączyć dwa węzły (`TaskDefinition`) definicji procesu tranzycją — przejściem, którym
silnik przeprowadzi proces z zadania źródłowego do docelowego.

**Warianty:**

| Wariant | Pola krytyczne | Charakterystyka |
|---|---|---|
| Decyzja użytkownika | `IsUserDecision = true` (zwykle przez wzorzec „Wybór operatora") | operator wybiera przejście na zadaniu — WORKFLOW-C3 |
| Automatyczna | `IsUserDecision = false` + warunek `IsRealized` | realizuje się sama, gdy warunek spełniony (patrz też WORKFLOW09) |
| Domyślna | `IsDefaultTransition = true` | preselekcja na liście decyzji operatora |
| Nie kończąca zadania (słaba) | `WeakTransition = true` | zadanie źródłowe pozostaje `Active` po przejściu |
| Wielowariantowa | `VariantTypeName` | jedna tranzycja → wiele pozycji decyzji — WORKFLOW-C4 |
| Z generatorem obiektów | `OGSchema` | przejście tworzy nowy obiekt biznesowy — WORKFLOW-C5 |

**Pola i typy (`Soneta.Workflow.Config.WFTransition`, tabela `WFTransitions`, konfiguracyjna):**

| Pole | Typ | Opis |
|---|---|---|
| `Name` | `string` | nazwa tranzycji (etykieta decyzji); klucz `ByNameAndSourceAndTargetAndWfDefinition` |
| `LP` | `int` | numer porządkowy — kolejność na liście decyzji (rosnąco, potem `Name`) |
| `Source` / `Target` | `Soneta.Business.Db.TaskDefinition` | węzeł źródłowy / docelowy |
| `WfDefinition` | `Soneta.Business.IWFDefinition` (iface-ref → `WFDefinition`) | definicja procesu; ustawiana **automatycznie** z `Source`/`Target` |
| `WFTransitionDefinition` | `Soneta.Workflow.Config.WFTransitionDefinition` | wzorzec tranzycji — WORKFLOW-C6 |
| `IsUserDecision` | `bool` | dalszy krok zależy od decyzji operatora |
| `IsDefaultTransition` | `bool` | tranzycja domyślna |
| `WeakTransition` | `bool` | nie kończy zadania źródłowego |
| `VariantTypeName` | `string` | typ tranzycji wielowariantowej (WORKFLOW-C4) |
| `OGSchema` | `Soneta.Workflow.Config.OGSchema` | schemat generatora obiektów (WORKFLOW-C5) |
| `Statement` | `Soneta.Business.MemoText` | kod kalkulatora (`IsRealized`/`IsReadOnly`/`IsVisible`) — WORKFLOW-C2 |
| `CheckCode` / `ActionCode` | `Soneta.Business.Db.AlgorithmColumn` | algorytmy `Check`/`Action` — WORKFLOW-C2 |
| `QuickAccess`, `Icon`, `ForeColor` | `string` | prezentacja decyzji — WORKFLOW-C3 |
| `IncompatibleWithSerialOperations` | `bool` | wyklucza decyzję z operacji seryjnych (WORKFLOW-F9) |
| `DefinitionType` | `Soneta.Business.Db.DefinitionTypeEnum` | `Standard`/`Engine` — determinowany **konstruktorem** klasy |

Klucze tabeli `WFTransitions` (`session.GetWorkflow().WFTransitions`): `WgSource[taskDef]`,
`WgTarget[taskDef]`, `WgWfDefinition[wfDefinition]`, `WgWFTransitionDefinition[wzorzec]`,
`WgOGSchema[schemat]`, `ByNameAndSourceAndTargetAndWfDefinition[name, source, target, def]`,
indeksator `[guid]`.

**Snippet:**

```csharp
// Tranzycje to dane KONFIGURACYJNE — edycja wymaga sesji konfiguracyjnej:
using (var session = login.CreateSession(readOnly: false, config: true, name: "Tranzycje WF"))
{
    var workflow = session.GetWorkflow();
    var definicja = workflow.WFDefs.WgSymbolu["URLOP"];      // WORKFLOW-A1

    // Węzły procesu (WORKFLOW02) — wyszukiwane w kolekcji definicji:
    var wniosek    = definicja.TaskDefs.First(td => td.Name == "Wniosek");
    var akceptacja = definicja.TaskDefs.First(td => td.Name == "Akceptacja");
    var odrzucenie = definicja.TaskDefs.First(td => td.Name == "Odrzucony");

    using (var t = session.Logout(editMode: true))
    {
        // 1. Decyzja użytkownika (wzorzec "Wybór operatora" — standardowy zapis dbinit):
        var akceptuj = session.AddRow(new WFTransition());
        akceptuj.Source = wniosek;                 // ustawia też WfDefinition (z definicji węzła)
        akceptuj.Target = akceptacja;
        akceptuj.WFTransitionDefinition = workflow.WFTransitionDefs[
            new Guid("00000000-0016-0001-0002-000000000000")];   // "Wybór operatora" — WORKFLOW-C6
        akceptuj.Name = "Akceptuj";                // nazwa PO wzorcu i Source (unikalność per węzeł)
        akceptuj.LP = 1;

        // 2. Druga decyzja z tego samego węzła:
        var odrzuc = session.AddRow(new WFTransition());
        odrzuc.Source = wniosek;
        odrzuc.Target = odrzucenie;
        odrzuc.WFTransitionDefinition = workflow.WFTransitionDefs[
            new Guid("00000000-0016-0001-0002-000000000000")];
        odrzuc.Name = "Odrzuć";
        odrzuc.LP = 2;
        odrzuc.IsDefaultTransition = true;         // flagi PO przypięciu wzorca (wzorzec je nadpisuje)

        t.Commit();
    }
    session.Save();

    // Odczyt — tranzycje wychodzące z węzła / całej definicji:
    foreach (WFTransition tr in workflow.WFTransitions.WgSource[wniosek])
    {
        // tr.Name, tr.LP, tr.Target.Name, tr.IsUserDecision, tr.IsDefaultTransition
    }
}
```

**Pułapki:**
- **Klasa tranzycji musi pasować do typu definicji**: `WFTransition` → `Standard`,
  `WFTransitionExtend` → `Engine`. Setter `WfDefinition` (oraz `WFTransitionDefinition`) rzuca
  `ArgumentException` przy niezgodności `DefinitionType` — nie da się „przestawić" setterem.
- `Source`/`Target` ustawiaj **przed** `Name` i wzorcem: setter `Source` ustawia `WfDefinition`
  i rejestruje weryfikatory (m.in. blokady węzłów `Locked`, unikalności nazw plików per węzeł);
  setter `Name` wymusza unikalny `FileName` wśród tranzycji tego samego `Source`.
- **Wzorzec nadpisuje flagi**: przypisanie `WFTransitionDefinition` ustawia `IsUserDecision`,
  `IsDefaultTransition`, `Statement`, `CheckCode`, `ActionCode` z wzorca — własne wartości tych
  pól ustawiaj dopiero **po** przypięciu wzorca.
- Strzałka na diagramie dokłada się automatycznie przy dodaniu tranzycji — nie edytuj
  `WFDefinition.SerializedDiagram` ręcznie (WORKFLOW-A4).
- Nazwa tranzycji nie jest globalnie unikalna — jednoznaczny jest dopiero komplet
  `ByNameAndSourceAndTargetAndWfDefinition`; metoda `CheckName(source, target)` (z `IWFTransition`)
  dokleja licznik przy duplikacie.
- Węzeł bez tranzycji wyjściowej kończy proces, gdy nie ma już innych żywych zadań — definicję
  przebudowuj w trybie modelowania (`IsDeployed == false`, WORKFLOW-A7).
- `GetCanDelete()` zwraca `false`, gdy oba węzły (`Source` i `Target`) są standardowe — tranzycji
  wbudowanych procesów nie usuniesz.

### WORKFLOW-C2 — Warunek realizacji tranzycji — algorytmy Statement / Check / Action (★)

**Cel:** zdefiniować, **kiedy** tranzycja się realizuje (`IsRealized`) i **co** się wtedy dzieje
(`Check` — walidacja, `Action` — efekty uboczne), kodem C# kompilowanym przez platformę.

**Warianty:**

| Wariant | Jak | Kiedy używać |
|---|---|---|
| Pełne metody kalkulatora | `IsRealizedCode` / `IsReadOnlyCode` / `IsVisibleCode` (string — całe `public override bool …`) | dowolna logika C# |
| Wyrażenia warunkowe (kreator) | `IsRealizedExpressionTask` / `…Transition` / `…Parent` (`RowCondition` jako string) + `OperatorType` (`And`/`Or`) | proste warunki po polach zadania / tranzycji / obiektu nadrzędnego |
| Walidacja przejścia | `CheckCode.Code` — handler `public void CheckHandle(CheckHandleEventArgs args)` | zablokowanie przejścia wyjątkiem |
| Efekt przejścia | `ActionCode.Code` — handler `public void ActionHandle(ActionHandleEventArgs args)` | modyfikacja danych po pozytywnym `Check` |

**Pola i typy:**

| Element | Typ | Opis |
|---|---|---|
| `Statement` | `Soneta.Business.MemoText` | złożony kod kalkulatora (regiony `IsRealized`/`IsReadOnly`/`IsVisible`); settery `…Code` składają go automatycznie |
| `IsRealizedCode`, `IsReadOnlyCode`, `IsVisibleCode` | `string` (kalkulowane) | pojedyncze metody kalkulatora; pusty string przywraca kod domyślny (`return base.…`) |
| `IsRealizedExpressionTask/Transition/Parent` | `string` (`RowCondition`) | wyrażenia kreatora — generują `IsRealizedCode` |
| `OperatorType` | `WFTransition.OperatorEnum` (`And`/`Or`) | operator łączący wyrażenia kreatora |
| `CheckCode`, `ActionCode` | `Soneta.Business.Db.AlgorithmColumn` (subrow; kod w `.Code: MemoText`) | handlery `Check`/`Action` |
| `WFTransition.GetWFTransitionCalculator()` | `→ WFTransitionCalculator` | skompilowany kalkulator tranzycji (publiczny) |

W kodzie algorytmów dostępne są (kontrakt `WFTransitionCalculator` + klasa generowana):
`Transition: WFTransition`, `Session`, `Login`, `Module`, `Table`; w handlerach `Check`/`Action`
dodatkowo `Task` (bieżące zadanie).

**Snippet:**

```csharp
using (var session = login.CreateSession(readOnly: false, config: true, name: "Algorytmy tranzycji"))
{
    var workflow = session.GetWorkflow();
    var definicja = workflow.WFDefs.WgSymbolu["URLOP"];
    var tranzycja = workflow.WFTransitions.WgWfDefinition[definicja]
        .First(tr => tr.Name == "Akceptuj");

    using (var t = session.Logout(editMode: true))
    {
        // Wariant A: pełna metoda IsRealized (silnik realizuje przejście, gdy zwróci true):
        tranzycja.IsRealizedCode =
            "public override bool IsRealized(Soneta.Business.Db.Task task) {\n" +
            "\treturn task.WFTransition != null && task.WFTransition == Transition;\n" + // = decyzja operatora
            "}";

        // Walidacja przed przejściem — wyjątek PRZERYWA realizację i zapis sesji:
        tranzycja.CheckCode.Code =
            "public void CheckHandle(CheckHandleEventArgs args) {\n" +
            "\tif (Task.Description.IsEmpty)\n" +
            "\t\tthrow new Soneta.Business.RowException(Task, \"Uzupełnij opis zadania przed akceptacją.\");\n" +
            "}";

        // Efekty uboczne po pozytywnym Check:
        tranzycja.ActionCode.Code =
            "public void ActionHandle(ActionHandleEventArgs args) {\n" +
            "\t// np. modyfikacja Task.Parent, zapis cechy, powiadomienie\n" +
            "}";

        // Wariant B (kreator): wyrażenia RowCondition zamiast pełnego kodu —
        // generują IsRealizedCode automatycznie (łączone OperatorType):
        // tranzycja.IsRealizedExpressionParent = "Stan = 'Zatwierdzony'";   // warunek po task.Parent
        // tranzycja.OperatorType = WFTransition.OperatorEnum.And;

        t.Commit();
    }
    session.Save();
}
```

**Pułapki:**
- **Kolejność wykonania silnika**: `IsRealized` → `Check` → `Action`; gdy `IsRealized == false`,
  `Check`/`Action` w ogóle się nie wykonują. Wyjątek z `Check` wypływa jako `BusException` przy
  zapisie sesji, która wywołała przeliczenie — projektuj komunikaty zrozumiałe dla operatora.
- `Check` jest wykonywany w **transakcji tylko-do-odczytu** — nie modyfikuj w nim danych
  (safe-code); miejsce na modyfikacje to `Action`.
- Settery `IsRealizedCode`/`CheckCode.Code`/… oczekują **kompletnych** metod (z sygnaturą), nie
  samych wyrażeń. Pusty string przywraca implementację domyślną (`base.IsRealized` ⇒ `false` —
  tranzycja bez własnego kodu i wzorca **nigdy** się nie zrealizuje).
- Wyrażenia kreatora (`IsRealizedExpression…`) i pełny kod to **dwa wejścia do tego samego
  `Statement`** — ustawienie `IsRealizedCode` czyści wyrażenia i odwrotnie; nie mieszaj obu dróg
  na jednej tranzycji.
- Kod kompiluje się przy zapisie konfiguracji (Roslyn, `AssemblyCache`) — błąd składni ujawni się
  jako błąd weryfikacji/zapisu definicji, nie w trakcie procesu.
- Tranzycja **automatyczna** (bez decyzji) to `IsUserDecision = false` + `IsRealized` zwracające
  `true` po spełnieniu warunku biznesowego (np. wzorzec „Bezwarunkowe przejście" — WORKFLOW-C6);
  silnik sprawdza warunki przy każdym przeliczeniu zadań procesu.

### WORKFLOW-C3 — Tranzycja jako decyzja użytkownika

**Cel:** skonfigurować tranzycję prezentowaną operatorowi jako **decyzja** na zadaniu (lista
„Decyzja" / przyciski szybkiego dostępu na formularzu zadania i panelu Workflow).

**Warianty:**

| Wariant | Jak |
|---|---|
| Zwykła decyzja z listy | `IsUserDecision = true` (wzorzec „Wybór operatora") |
| Decyzja domyślna (preselekcja) | + `IsDefaultTransition = true` |
| Przycisk szybkiego dostępu | + `QuickAccess` (tekst przycisku) |
| Wyróżnienie wizualne | + `Icon` (nazwa ikony), `ForeColor` (kolor) |
| Decyzja z wariantami | + `VariantTypeName` — WORKFLOW-C4 |

**Pola i typy:**

| Pole | Typ | Opis |
|---|---|---|
| `IsUserDecision` | `bool` | tranzycja zależy od decyzji operatora |
| `IsDefaultTransition` | `bool` | decyzja proponowana domyślnie |
| `QuickAccess` | `string` | opis przycisku szybkiego dostępu (pusty = brak przycisku) |
| `Icon` | `string` | ikona decyzji; gdy pusta — brana z węzła docelowego (`Target.Icon`), domyślnie „naprzod" |
| `ForeColor` | `string` | kolor decyzji; gdy pusty — z węzła docelowego |
| `LP` | `int` | kolejność na liście decyzji (rosnąco, potem alfabetycznie po `Name`) |
| `Task.UserDecision` | `(IWFTransition Transition, object Variant, Context Context)` | wybrana decyzja na zadaniu (operacyjnie) |
| `Task.SetUserDecision(string, object, Context)` / `Task.GoThru(string)` | metody `Soneta.Business.Db.Task` | ustawienie decyzji po **nazwie tranzycji** (realizacja — WORKFLOW-F2) |

**Snippet:**

```csharp
// KONFIGURACJA — wygląd i zachowanie decyzji:
using (var session = login.CreateSession(readOnly: false, config: true, name: "Decyzje"))
{
    var workflow = session.GetWorkflow();
    var definicja = workflow.WFDefs.WgSymbolu["URLOP"];
    var akceptuj = workflow.WFTransitions.WgWfDefinition[definicja].First(tr => tr.Name == "Akceptuj");
    var odrzuc   = workflow.WFTransitions.WgWfDefinition[definicja].First(tr => tr.Name == "Odrzuć");

    using (var t = session.Logout(editMode: true))
    {
        akceptuj.QuickAccess = "Akceptuj wniosek";   // przycisk szybkiego dostępu
        akceptuj.Icon = "akceptuj";
        akceptuj.IsDefaultTransition = true;         // preselekcja na liście decyzji

        odrzuc.ForeColor = "#C00000";                // wyróżnienie decyzji negatywnej
        t.Commit();
    }
    session.Save();
}

// OPERACYJNIE — podjęcie decyzji na aktywnym zadaniu (szczegóły: WORKFLOW-F2):
using (var session = login.CreateSession(readOnly: false, config: false, name: "Decyzja"))
{
    var task = session.GetBusiness().Tasks.WgDefinition[/* RowCondition — WORKFLOW-E1 */]
        .GetFirst();

    using (var t = session.Logout(editMode: true))
    {
        task.SetUserDecision("Akceptuj");            // po nazwie tranzycji węzła zadania
        t.Commit();
    }
    session.Save();                                  // tu silnik realizuje przejście (Check/Action)
}
```

**Pułapki:**
- Decyzję może ustawić tylko **właściciel aktywnego zadania**: `Task.IsReadOnlyUserDecision()`
  zwraca `true` (blokada), gdy `Progress != Active`, zadanie nie należy do zalogowanego operatora
  (`Tasks.IsLoggedUserTask`) albo zadanie nie ma procesu.
- Standardowy wzorzec „Wybór operatora" generuje `IsRealized` ⇒
  `task.WFTransition == Transition` — przejście realizuje się dopiero, gdy decyzja zostanie
  ustawiona na zadaniu **i sesja zapisana**; samo `SetUserDecision` bez `Commit` + `Save()` nic
  nie zmienia w procesie.
- `IsDefaultTransition` to **preselekcja**, nie automat — bez akcji operatora przejście nie
  nastąpi (test wewnętrzny platformy potwierdza: domyślna tranzycja nie decyduje „za" użytkownika).
  Automat = `IsUserDecision == false` + warunek (WORKFLOW-C2).
- Etykieta decyzji to `Name` tranzycji + `[nazwa węzła docelowego]` — chyba że na definicji
  ustawiono `HideTaskNameInTransitionComboBox` (WORKFLOW-A2). `SetUserDecision` dopasowuje po
  samej nazwie tranzycji.
- `SetUserDecision` z nazwą nieistniejącej tranzycji **nie rzuca wyjątku** — po prostu nie ustawia
  decyzji; w kodzie krytycznym sprawdź po wywołaniu `task.UserDecision.Transition != null`.
- Kolejność listy decyzji: `LP` rosnąco, potem `Name` — nadawaj `LP` świadomie, gdy kolejność
  decyzji ma znaczenie dla operatora.

### WORKFLOW-C4 — Warianty tranzycji (tranzycja wielowariantowa)

**Cel:** jedna tranzycja prezentowana jako **wiele pozycji decyzji** — po jednej na „wariant"
(np. po jednej na każdą definicję dokumentu, którą można wygenerować przejściem).

**Mechanizm:** pole `VariantTypeName: string` przechowuje pełną nazwę typu wariantu (np.
`"Soneta.Core.DefinicjaDokumentu"`). Lista decyzji rozwija taką tranzycję do pozycji
`Nazwa/wariant [Węzeł docelowy]`. Wybrany wariant trafia do `Task.UserDecision.Variant: object`
i jest dostępny w algorytmach przejścia (`Check`/`Action`) oraz w schemacie generatora.

**Pola i typy:**

| Element | Typ | Opis |
|---|---|---|
| `VariantTypeName` | `string` (bazodanowe) | pełna nazwa typu wariantu; pusty = tranzycja jednowariantowa |
| `Task.SetUserDecision(string transitionName, object variant = null, Context context = null)` | metoda `Task` | decyzja z konkretnym wariantem |
| `Task.UserDecision.Variant` | `object` | wybrany wariant (np. wiersz `DefinicjaDokumentu`) |

**Snippet:**

```csharp
// KONFIGURACJA — oznaczenie tranzycji jako wielowariantowej:
using (var session = login.CreateSession(readOnly: false, config: true, name: "Warianty"))
{
    var workflow = session.GetWorkflow();
    var definicja = workflow.WFDefs.WgSymbolu["OBIEG"];
    var tranzycja = workflow.WFTransitions.WgWfDefinition[definicja]
        .First(tr => tr.Name == "Generuj dokument");

    using (var t = session.Logout(editMode: true))
    {
        tranzycja.VariantTypeName = "Soneta.Core.DefinicjaDokumentu";   // typ wariantu
        t.Commit();
    }
    session.Save();
}

// OPERACYJNIE — decyzja ze wskazaniem konkretnego wariantu:
using (var session = login.CreateSession(readOnly: false, config: false, name: "Decyzja z wariantem"))
{
    var task = /* aktywne zadanie węzła źródłowego — WORKFLOW-E1 */ default(Task);
    // Wariant = obiekt typu z VariantTypeName, pobrany z TEJ SAMEJ sesji co zadanie:
    var wariant = /* np. wybrany wiersz DefinicjaDokumentu */ default(object);

    using (var t = session.Logout(editMode: true))
    {
        task.SetUserDecision("Generuj dokument", wariant);   // tranzycja + wariant
        t.Commit();
    }
    session.Save();

    // Odczyt wybranego wariantu (np. w algorytmie Action lub po zapisie):
    object wybrany = task.UserDecision.Variant;
}
```

**Pułapki:**
- **Enumeracja listy wariantów nie jest publiczna** — `WFTransitionCalculator.GetVariants(task)`
  jest `internal`, a warianty wylicza wewnętrzny mechanizm (dla definicji Engine — executor
  tranzycji / pluginy). Z kodu zewnętrznego: znasz typ z `VariantTypeName`, sam wyznaczasz
  kandydatów (np. wiersze wskazanej tabeli) i przekazujesz wybrany obiekt do `SetUserDecision`.
- Pełne wsparcie wielowariantowości (automatyczne rozwijanie pozycji decyzji) dotyczy przede
  wszystkim definicji typu **Engine** (`WFTransitionExtend`); dla definicji Standard
  `VariantTypeName` nie wygeneruje sam z siebie listy wariantów.
- Wariant będący wierszem (`Row`) musi pochodzić z **tej samej sesji** co zadanie — przy
  przenoszeniu między sesjami użyj `session.Get(row)`.
- Dwie „decyzje" tej samej tranzycji różnią się tylko `Variant` — porównując decyzje, porównuj
  parę (`Transition`, `Variant`), nie samą tranzycję.
- W operacjach seryjnych (WORKFLOW-F9) tranzycje wielowariantowe bywają wykluczane —
  `IncompatibleWithSerialOperations = true` ustaw, gdy wariant wymaga indywidualnego wyboru.

### WORKFLOW-C5 — Schemat generatora obiektów (OGSchema) (★)

**Cel:** zdefiniować **mapowanie danych** źródłowego obiektu na nowy obiekt docelowy (np. z procesu
obiegu utworzyć zadanie CRM, z dokumentu — inny dokument) i podpiąć je pod tranzycję
(albo uruchomić ręcznie `WorkflowTools.Generate`).

**Warianty:**

| Wariant | Jak |
|---|---|
| Mapowanie pól 1:1 | `XML` — elementy `PropertyMapping` z `sourceproperty` → `targetproperty` |
| Wartość wyliczana kodem | `PropertyMapping` z `translation` (wyrażenie C#) zamiast `sourceproperty` |
| Inicjalizacja obiektu docelowego | `InitTargetRowCode` — `public override void InitTargetRow(object targetRow)` |
| Niestandardowy konstruktor | `ConstructorInfo` / `InvokeConstructorCode` — `public override object InvokeConstructor(ConstructorInfo ci, Context context)` |
| Kopiowanie załączników / notatek | `CopyAttachments` / `CopyNotes` |
| Generowanie w przejściu procesu | `WFTransition.OGSchema = schemat` |
| Generowanie ręczne | `WorkflowTools.Generate(source, ogSchema, context)` |

**Pola i typy (`Soneta.Workflow.Config.OGSchema`, tabela `OGSchemas`, konfiguracyjna):**

| Pole | Typ | Opis |
|---|---|---|
| `Name` | `string` | nazwa schematu; klucz `OGSchemas.ByName[name]` (pojedynczy wiersz) |
| `SourceType` / `TargetType` | `string` | **nazwy tabel** obiektu źródłowego / docelowego (np. `"WFWorkflows"`, `"Zadania"`, `"DokHandlowe"`) |
| `SourceDataType` / `TargetDataType` | `System.Type` (kalkulowane) | typy wierszy wyliczone z nazw tabel |
| `XML` | `Soneta.Business.MemoText` | definicja mapowania pól (XML, xmlns `http://www.enova.pl/schemas/OGPropertyMapping`) |
| `InitTargetRowCode` / `InvokeConstructorCode` | `string` | algorytmy inicjalizacji / konstrukcji obiektu docelowego |
| `CopyAttachments` / `CopyNotes` | `bool` | kopiowanie załączników / notatek źródła |
| `Getters` | `List<Soneta.Workflow.Config.OGSchemaInfo>` (odczyt) | sparsowane mapowania (`SourceProperty`, `TargetProperty`, `Translation`, `IsRequired`) |
| `WorkflowTools.Generate(IGuidedRow, OGSchema, Context)` | `static → GuidedRow` (`Soneta.Workflow`) | tworzy obiekt docelowy, **dodaje go do sesji**, mapuje pola, kopiuje załączniki/notatki |

**Snippet:**

```csharp
// 1. KONFIGURACJA — schemat: proces (WFWorkflows) -> zadanie CRM (Zadania):
using (var session = login.CreateSession(readOnly: false, config: true, name: "Schemat OG"))
{
    using (var t = session.Logout(editMode: true))
    {
        var schemat = session.AddRow(new OGSchema());
        schemat.Name = "Proces -> Zadanie";
        schemat.SourceType = "WFWorkflows";          // nazwa TABELI źródłowej
        schemat.TargetType = "Zadania";              // nazwa TABELI docelowej
        schemat.CopyAttachments = true;              // przenieś załączniki źródła
        schemat.XML =
            @"<?xml version=""1.0"" encoding=""UTF-8""?>" +
            @"<PropertyMappings xmlns=""http://www.enova.pl/schemas/OGPropertyMapping"">" +
            @"<PropertyMapping>" +
            @"<sourceproperty>Name</sourceproperty>" +          // pole źródła (WFWorkflow.Name)
            @"<targetproperty>Nazwa</targetproperty>" +         // pole celu (Zadanie.Nazwa)
            @"<isrequired>1</isrequired><iscontextrequired>0</iscontextrequired>" +
            @"</PropertyMapping></PropertyMappings>";
        schemat.InitTargetRowCode =
            "public override void InitTargetRow(object targetRow) {\n" +
            "\tbase.InitTargetRow(targetRow);\n" +
            "\tif (targetRow is Soneta.Zadania.Zadanie zadanie)\n" +
            "\t\tzadanie.Definicja = Session.GetZadania().DefZadan.WgSymbolu[\"ZAD\"];\n" +
            "}";
        t.Commit();
    }
    session.Save();

    // 2. Podpięcie do tranzycji — generowanie w momencie przejścia:
    var workflow = session.GetWorkflow();
    var definicja = workflow.WFDefs.WgSymbolu["OBIEG"];
    var tranzycja = workflow.WFTransitions.WgWfDefinition[definicja]
        .First(tr => tr.Name == "Generuj zadanie");
    using (var t = session.Logout(editMode: true))
    {
        tranzycja.OGSchema = workflow.OGSchemas.ByName["Proces -> Zadanie"];
        t.Commit();
    }
    session.Save();
}

// 3. RĘCZNE uruchomienie schematu (sesja operacyjna — OGSchema jest czytelny):
using (var session = login.CreateSession(readOnly: false, config: false, name: "Generuj"))
{
    var og = session.GetWorkflow().OGSchemas.ByName["Proces -> Zadanie"];
    var proces = /* WFWorkflow — źródło (WORKFLOW04) */ default(WFWorkflow);

    using (var t = session.Logout(editMode: true))
    {
        // Generate: tworzy wiersz docelowy, DODAJE do sesji i mapuje pola wg XML:
        var nowy = WorkflowTools.Generate(proces, og, Context.Empty.Clone(session));
        t.Commit();
    }
    session.Save();
}
```

**Pułapki:**
- `SourceType`/`TargetType` to **nazwy tabel**, nie typów C# (`"Zadania"`, nie
  `"Soneta.Zadania.Zadanie"`); listy dopuszczalnych wartości zwracają `GetListSourceType()` /
  `GetListTargetType()`.
- `Generate` **sam dodaje wiersz do sesji** (`AddRow` na tabeli docelowej) — nie dodawaj wyniku
  drugi raz; wołaj w otwartej transakcji edycyjnej tej sesji, w której żyje przekazany `OGSchema`.
- Obiekt docelowy bez konstruktora bezparametrowego wymaga `ConstructorInfo` /
  `InvokeConstructorCode` — inaczej `Generate` rzuci wyjątek o braku możliwości utworzenia
  instancji. Kolejność wykonania: `InvokeConstructor` → mapowanie `PropertyMapping` →
  `InitTargetRow`.
- `translation` w `PropertyMapping` to wyrażenie C# wyliczające wartość pola docelowego — można
  go użyć **zamiast** `sourceproperty` (źródłem jest wtedy kod, np. `Date.Today`).
- Mapowanie ustawia pola w kolejności `PropertyMapping` — pamiętaj o zależnościach inicjalizacji
  (np. `Definicja` zadania przed polami zależnymi; bezpieczniej ustawiać ją w `InitTargetRow`).
- Na tranzycji `GetListOGSchema()` podpowiada tylko schematy zgodne z węzłami
  (`SourceType == tabela Source`, `TargetType == tabela Target`) — schemat „niezgodny" formalnie
  da się przypisać kodem, ale silnik nie znajdzie danych źródłowych właściwego typu.
- `CopyAttachments`/`CopyNotes` działają dla źródeł `GuidedRow` (przeciążenie `Generate` z
  `IGuidedRow`); kopiowane są linki załączników i treści notatek.

### WORKFLOW-C6 — Wzorzec tranzycji (WFTransitionDefinition)

**Cel:** użyć **reużywalnej definicji elementu tranzycji** — wzorca z gotowym kodem
(`Statement`/`Check`/`Action`) i flagami (`IsUserDecision`, `IsDefaultTransition`) — zamiast pisać
algorytmy na każdej tranzycji od nowa.

**Standardowe wzorce (dbinit — stałe `Guid`, `IsStandard`):**

| Guid | Nazwa | Typ | Charakterystyka |
|---|---|---|---|
| `00000000-0016-0001-0001-…` | Bezwarunkowe przejście | Standard | `IsRealized` ⇒ `true` — tranzycja automatyczna |
| `00000000-0016-0001-0002-…` | Wybór operatora | Standard | `IsUserDecision = true`; `IsRealized` ⇒ `task.WFTransition == Transition` |
| `00000000-0016-0001-0003-…` | Podproces zakończony | Standard | `IsSubprocess = true`; `IsRealized` ⇒ `task.Subprocess.IsClosed` |
| `00000000-0016-0001-0004-…` | Bezwarunkowe przejście | Engine (`WFTransitionDefinitionExtend`) | odpowiednik dla definicji kodowych |
| `00000000-0016-0001-0005-…` | Decyzja użytkownika | Engine | decyzja operatora |
| `00000000-0016-0001-0006-…` | Zakończenie podprocesu | Engine | `IsSubprocess = true` |
| `00000000-0016-0001-0007-…` | Wyjście ze złączenia | Engine | `IsSingleInstance = true` (złączenie gałęzi) |
| `00000000-0016-0001-0008-…` | Przejście nie kończące zadania (słabe) | Engine | wariant `WeakTransition` |

**Pola i typy (`Soneta.Workflow.Config.WFTransitionDefinition`, tabela `WFTransitionDefs`,
konfiguracyjna):**

| Pole | Typ | Opis |
|---|---|---|
| `Name` / `FormattedName` | `string` | nazwa wzorca; klucz `ByNameAndParentType[name, parentType]` |
| `ParentType` | `string` | opcjonalne zawężenie do typu obiektu nadrzędnego (pusty = uniwersalny) |
| `IsUserDecision`, `IsDefaultTransition` | `bool` | flagi przepisywane na tranzycję przy przypięciu |
| `IsSubprocess` | `bool` | wzorzec powiązany z podprocesem (WORKFLOW-D4) |
| `IsSingleInstance` | `bool` | wzorzec powiązany ze złączeniem gałęzi |
| `Statement`, `CheckCode`, `ActionCode` | jak na `WFTransition` | kod kopiowany na tranzycję |
| `IsRealizedCode` / `IsReadOnlyCode` / `IsVisibleCode` | `string` | jak na `WFTransition` (WORKFLOW-C2) |
| `WFTransitions` | `SubTable<WFTransition>` | tranzycje używające wzorca |
| `DefinitionType` | `DefinitionTypeEnum` | `Standard` (`WFTransitionDefinition`) / `Engine` (`WFTransitionDefinitionExtend`) — z konstruktora |

**Snippet:**

```csharp
using (var session = login.CreateSession(readOnly: false, config: true, name: "Wzorce tranzycji"))
{
    var workflow = session.GetWorkflow();

    using (var t = session.Logout(editMode: true))
    {
        // Własny, reużywalny wzorzec (raz na bazę):
        var wzorzec = session.AddRow(new WFTransitionDefinition());
        wzorzec.Name = "Zatwierdzony dokument";
        wzorzec.IsUserDecision = false;                       // tranzycja automatyczna
        wzorzec.IsRealizedCode =
            "public override bool IsRealized(Soneta.Business.Db.Task task) {\n" +
            "\treturn task.Parent is Soneta.Handel.DokumentHandlowy dok\n" +
            "\t\t&& dok.Stan == Soneta.Handel.StanDokumentuHandlowego.Zatwierdzony;\n" +
            "}";
        t.Commit();
    }
    session.Save();

    // Użycie wzorca na tranzycji (kopiuje kod i flagi — patrz pułapki):
    var definicja = workflow.WFDefs.WgSymbolu["OBIEG"];
    var tranzycja = workflow.WFTransitions.WgWfDefinition[definicja]
        .First(tr => tr.Name == "Po zatwierdzeniu");
    using (var t = session.Logout(editMode: true))
    {
        tranzycja.WFTransitionDefinition =
            workflow.WFTransitionDefs.ByNameAndParentType["Zatwierdzony dokument", ""];
        t.Commit();
    }
    session.Save();

    // Standardowy wzorzec — stabilny Guid w każdej bazie:
    var wyborOperatora = workflow.WFTransitionDefs[new Guid("00000000-0016-0001-0002-000000000000")];
}
```

**Pułapki:**
- Przypięcie wzorca to **kopia w momencie przypisania** (snapshot): `Statement`, `CheckCode`,
  `ActionCode`, `IsUserDecision`, `IsDefaultTransition` są przepisywane na tranzycję; późniejsza
  edycja wzorca **nie aktualizuje** tranzycji, które już go używają.
- Przypięcie **nadpisuje** wcześniejsze ustawienia tranzycji (kod i flagi) — wzorzec ustawiaj
  przed własnymi modyfikacjami (jak w WORKFLOW-C1).
- `DefinitionType` wzorca musi zgadzać się z tranzycją (`WFTransitionDefinition` ↔ `WFTransition`,
  `WFTransitionDefinitionExtend` ↔ `WFTransitionExtend`) — setter rzuca `ArgumentException`.
- Standardowych wzorców (`IsStandard`) nie edytuj i nie usuwaj — odwołuj się do nich po stałym
  `Guid` (tabela wyżej) albo kluczem `ByNameAndParentType` (uwaga: nazwy standardowe są
  tłumaczone, `Guid` jest bezpieczniejszy).
- Lista wzorców podpowiadanych na tranzycji jest zawężana m.in. po `ParentType` względem typu
  węzła źródłowego — wzorzec z wypełnionym `ParentType` nie pojawi się na tranzycjach innych
  typów obiektów.

### WORKFLOW-C7 — Obliczenie dostępnych tranzycji dla zadania (★)

**Cel:** dla **uruchomionego** zadania (`Task`) wyznaczyć tranzycje wychodzące z jego węzła
i ocenić każdą publicznym kontraktem kalkulatora: widoczna? zablokowana? spełniony warunek
realizacji? — czyli odtworzyć w kodzie listę „Decyzja" z formularza zadania.

**Warianty:**

| Wariant | Jak |
|---|---|
| Tranzycje wychodzące z węzła zadania | `task.GetTransitions()` (`Soneta.Workflow.Extensions.WFTaskPublicExtender`) |
| Tranzycje wychodzące z definicji węzła (bez zadania) | `session.GetWorkflow().WFTransitions.WgSource[taskDefinition]` (też: `taskDefinition.SourceWFTransitions`) |
| Tranzycje wchodzące do węzła | `WFTransitions.WgTarget[taskDefinition]` |
| Ocena tranzycji dla zadania | `tr.GetWFTransitionCalculator()` → `IsVisible` / `IsReadOnly` / `IsRealized` |
| Czynność UI „Podejmij decyzję" | worker `Soneta.Workflow.Workers.TransitionsForTasksWorker` (menu Czynności na liście zadań) |

**Pola i typy:**

| Element | Typ / sygnatura | Uwagi |
|---|---|---|
| `task.GetTransitions()` | `→ IEnumerable<WFTransition>` (extension, `Soneta.Workflow.Extensions`) | wymaga `task.WFWorkflow != null` (inaczej `NotSupportedException`) |
| `WFTransition.GetWFTransitionCalculator()` | `→ Soneta.Workflow.Config.WFTransitionCalculator` | skompilowany kalkulator (cache na wierszu) |
| `calc.IsVisible(Task)` / `calc.IsReadOnly(Task)` / `calc.IsRealized(Task)` | `bool` | sam predykat — **bez** `Check`/`Action` |
| `Soneta.Business.IWFTransition` | interfejs (`IsReadOnly(Task)`, `IsVisible(Task)`, `Source`, `Target`, `IsUserDecision`, `IsDefaultTransition`, `WeakTransition`, `LP`) | równoważna droga przez rzutowanie `(IWFTransition)tr` |
| `task.GetSourceTasks()` / `task.GetTargetTasks(bool active = true)` | `→ IEnumerable<Task>` (extension) | zadania węzłów poprzedzających / następnych w tym procesie |
| `task.ForceProcessRecalculation()` | extension | wymusza przeliczenie pozostałych żywych zadań procesu |

**Snippet:**

```csharp
using Soneta.Workflow.Extensions;   // WFTaskPublicExtender (GetTransitions, GetTargetTasks)

// Zadanie aktywne zalogowanego operatora (odczyt zadań: WORKFLOW-E1):
var task = /* Task z WFWorkflow != null */ default(Task);

// Lista "Decyzja" jak na formularzu zadania — tranzycje węzła + ocena kalkulatorem:
var decyzje = task.GetTransitions()
    .Where(tr => tr.IsUserDecision)
    .OrderBy(tr => tr.LP).ThenBy(tr => tr.Name.ToLower())
    .Select(tr => new
    {
        Tranzycja   = tr,
        Widoczna    = tr.GetWFTransitionCalculator().IsVisible(task),
        Zablokowana = tr.GetWFTransitionCalculator().IsReadOnly(task),
        Domyslna    = tr.IsDefaultTransition,
        Wezel       = tr.Target?.Name
    })
    .Where(d => d.Widoczna && !d.Zablokowana)
    .ToList();

// Tranzycje automatyczne — sprawdzenie warunku bez realizowania przejścia:
foreach (WFTransition tr in task.GetTransitions().Where(tr => !tr.IsUserDecision))
{
    bool warunekSpelniony = tr.GetWFTransitionCalculator().IsRealized(task);
    // realizację (Check + Action) wykonuje silnik przy zapisie / Task.Recalculate()
}

// To samo na poziomie definicji (bez uruchomionego procesu):
var wfTransitions = task.Session.GetWorkflow().WFTransitions;
foreach (WFTransition tr in wfTransitions.WgSource[task.Definition])
{
    // tr.Name, tr.Target, tr.IsUserDecision — struktura przepływu (WORKFLOW-C1)
}
```

**Pułapki:**
- `task.GetTransitions()` rzuca `NotSupportedException` dla zadań **bez procesu**
  (`task.WFWorkflow == null`, np. zadania CRM / globalne) — sprawdź proces przed wywołaniem.
- Wywołuj **`calc.IsRealized(task)`** (publiczna metoda kalkulatora) — to czysty predykat. Pełną
  realizację (z `Check` i `Action`) wykonuje wyłącznie silnik podczas przeliczania zadań; nie
  próbuj „przeprowadzać" tranzycji ręcznie poza `SetUserDecision`/`GoThru` + zapis (WORKFLOW-F2).
- Lista dostępnych **decyzji** to coś więcej niż lista tranzycji: zadanie musi być `Active`
  i należeć do zalogowanego operatora (`Task.IsReadOnlyUserDecision()`), a tranzycje
  wielowariantowe rozwijają się na wiele pozycji (WORKFLOW-C4). Snippet odtwarza widoczność
  i blokadę; pełną listę pozycji decyzji buduje wewnętrzny mechanizm platformy
  (czynność „Podejmij decyzję" — `TransitionsForTasksWorker`).
- Kalkulator jest **cache'owany na wierszu** tranzycji — w długo żyjącej sesji konfiguracyjnej po
  zmianie kodu pobierz wiersz ponownie (nowa sesja), by dostać świeżo skompilowaną klasę.
- `IsRealized` tranzycji z wzorca „Wybór operatora" zwraca `true` tylko, gdy decyzja jest już
  ustawiona na zadaniu (`task.WFTransition == tranzycja`) — `false` na „świeżym" zadaniu to nie
  błąd, tylko brak decyzji.
- Czynność „Podejmij decyzję" (worker `TransitionsForTasksWorker`, akcja `AddTransitionForTasks`)
  jest **interaktywna** (`FormActionResult` + `QueryContext`) i waliduje m.in.
  `Tasks.IsLoggedUserTask`, `IsActiveProgress` — programowo używaj `SetUserDecision`
  (WORKFLOW-F2), worker zostaw UI.
