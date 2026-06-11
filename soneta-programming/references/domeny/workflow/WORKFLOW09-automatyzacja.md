# WORKFLOW09 — Powiadomienia, triggery i automatyzacja procesu

> Wspólne fakty o typie, podstawowe typy i szablon wzorca: [../workflow.md](../workflow.md).

> **Model „automatyzacja = konfiguracja + zapis".** Cała automatyzacja procesu workflow jest
> opisana **danymi konfiguracyjnymi** podpiętymi do definicji (edycja wymaga sesji
> `login.CreateSession(readOnly: false, config: true, …)` — patrz WORKFLOW01 i session-login.md):
>
> | Mechanizm | Obiekt konfiguracyjny | Kiedy działa |
> |---|---|---|
> | Powiadomienie (system / e-mail / SMS) | `Soneta.Business.Db.Notifications.SysNotification` (tabela `SysNotifications`) | przy utworzeniu / realizacji zadania węzła lub z opóźnieniem |
> | Trigger (wyzwalacz) | `Soneta.Business.Db.TaskTrigger` (tabela `TaskTriggers`, child `TaskDefinition`) | w trakcie **`session.Save()`** — zapis wiersza obserwowanej tabeli |
> | Automatyczny start procesu | węzeł startowy: `TaskDefinition.IsStart = true` + `ActionRunAt.Auto` | w trakcie `session.Save()` obiektu źródłowego |
> | Automatyczna tranzycja | `WFTransition.IsUserDecision = false` + warunek `IsRealized` | przy przeliczeniu zadania (zapis obiektu / decyzja) |
> | Harmonogram | `Soneta.Core.Schedule.ScheduleDefinition` (tabela `ScheduleDefs`) | wg cyklu — wykonuje **usługa Harmonogram Zadań** |
>
> Kluczowa konsekwencja: **silnik automatyzacji odpala się w `session.Save()`** (wewnętrzny Saver
> przegląda triggery po kluczu `TaskTriggers.ByTableName` dla każdej zapisywanej tabeli) — nie ma
> publicznej metody „przelicz zadanie teraz"; przeliczenie wymuszasz zapisem powiązanego obiektu
> albo decyzją (WORKFLOW06). Wyjątkiem jest harmonogram, który wymaga działającej usługi HZ
> (Harmonogram Zadań) na serwerze.

### WORKFLOW-I1 — Wysyłanie powiadomienia z węzła procesu (★)

**Cel:** skonfigurować na węźle procesu (definicji zadania) powiadomienie wysyłane automatycznie
przy utworzeniu / realizacji zadania tego węzła — systemowe (zadanie-powiadomienie na panelu
operatora), e-mail lub SMS.

**Mechanizm (kluczowy):** definicją powiadomienia jest wiersz
`Soneta.Business.Db.Notifications.SysNotification` (tabela `SysNotifications`, konfiguracyjna),
tworzony konstruktorem **`new SysNotification(host)`**, gdzie `host` to węzeł (`TaskDefinition`),
którego zadania mają generować powiadomienia. Powiadomienia węzła widać w kolekcji
`TaskDefinition.SysNotifications: SubTable<SysNotification>`; tabela ma klucz
`session.GetBusiness().SysNotifications.WgTaskDefinition`. Gdy na zadaniu węzła zajdzie
okoliczność `Fact`, silnik wylicza odbiorców (wg `RecipientType`) i — w trybie systemowym —
**tworzy zadanie-powiadomienie** o definicji `NotificationType` (osobny `Task` dla każdego
odbiorcy, powiązany z powiadomieniem przez `Task.SysNotification`); w trybie e-mail/SMS buduje
wiadomość z szablonu `Template`.

**Warianty (tryb wyznacza pole `Template` — property `Mode` jest tylko do odczytu):**

| Wariant | `Mode` | Konfiguracja |
|---|---|---|
| Systemowe (zadanie-powiadomienie) | `NotificationMode.System` | `Template == null`; wymagane `NotificationType` |
| E-mail | `NotificationMode.Email` | `Template` = `Soneta.CRM.SzablonEmail` (szablon z polami `DO`, `Temat`, `Tresc`) |
| SMS | `NotificationMode.Sms` | `Template` = `Soneta.CRM.SzablonSms` |
| Plugin (Engine) | — | tylko definicje typu Engine — powiadomienie w kodzie silnika (`WePlugInNotification`); poza zakresem konfiguracji Standard |

**Pola i typy (`Soneta.Business.Db.Notifications.SysNotification`, tabela `SysNotifications`, konfiguracyjna):**

| Pole | Typ | Opis |
|---|---|---|
| `Name` | `string` | nazwa powiadomienia (bez znaków specjalnych — weryfikator) |
| `Host` | `Soneta.Business.ISysNotificationHost` (iface-ref) | właściciel — węzeł `TaskDefinition` (ustawiany **konstruktorem**) |
| `Fact` | `Soneta.Business.Db.Notifications.SysNotificationFact` | okoliczność: `OnCreate` (utworzenie zadania), `OnRealize` (realizacja), `OnDelayedTime` (opóźnione) |
| `DelayUnit`, `DelayValue` | `SysNotificationDelayUnit` (`Hours`/`Days`), `int` | opóźnienie — zapisywalne **tylko** przy `Fact == OnDelayedTime`; najprościej `SetDelay(delayUnit, delayValue)` |
| `RecipientType` | `SysNotificationRecipientType` | odbiorcy: `TaskRecipient` (adresat zadania), `ProcessOwner`, `ObjectCreator`, `ObjectModifier`, `Recipients` (grupa), `Role`, `Expression`, `Node` |
| `Role`, `Group` | `string` | nazwa roli / grupy — gdy `RecipientType` = `Role` / `Recipients` |
| `Node`, `OrgStructure` | `IElementStrukturyOrganizacyjnej`, `IStrukturaOrganizacyjna` (iface-ref) | odbiorca = element struktury organizacyjnej (`RecipientType.Node`) |
| `NotificationType` | `Soneta.Business.Db.TaskDefinition` | definicja **zadania-powiadomienia** (musi mieć `MultiTaskType == ByGetSysNotifications`) — tryb systemowy |
| `Template` | `Soneta.Business.ITemplate` (iface-ref: `SzablonEmail`/`SzablonSms`) | szablon — tryb e-mail/SMS; `null` = systemowe |
| `Finalization` | `SysNotificationFinalization` | auto-zakończenie zadania-powiadomienia: `Never`, `OnTaskTerminated`, `OnTaskRealized`, `OnTaskFinalized`, `OnProcessFinalized` |
| `ErrorReaction` | `SysNotificationErrorReactionType` | reakcja na błąd wysyłki: `ThrowError` (wstrzymaj zadanie), `GenerateSystemNotification`, `None` |
| `CanCreateSysNotificationCode` | `Soneta.Business.Db.AlgorithmColumn` | algorytm warunku „czy wysłać" (kod C#) |
| `GetRecipientsExpressionCode` | `AlgorithmColumn` | algorytm odbiorców (`RecipientType.Expression`) |
| `GetSysNotificationContentCode` | `AlgorithmColumn` | algorytm treści (temat/treść/adresaci) |
| `Locked` | `bool` | powiadomienie wyłączone |
| `WfDefinition`, `TaskDefinition` | `IWFDefinition`, `TaskDefinition` | definicja procesu / węzeł (relacje informacyjne) |

**Snippet:**

```csharp
// Powiadomienia to dane KONFIGURACYJNE — sesja konfiguracyjna:
using (var session = login.CreateSession(readOnly: false, config: true, name: "Powiadomienie WF"))
{
    var business = session.GetBusiness();

    // Typ powiadomienia systemowego: definicja zadania-powiadomienia — pobierz dynamicznie
    // (definicje z MultiTaskType.ByGetSysNotifications; standardowa istnieje w każdej bazie):
    var typPowiadomienia = business.TaskDefs.OfType<TaskDefinition>()
        .First(td => td.MultiTaskType == MultiTaskType.ByGetSysNotifications);

    using (var t = session.Logout(editMode: true))
    {
        // Minimalna definicja procesu z węzłem (szczegóły: WORKFLOW01/WORKFLOW02):
        var definicja = session.AddRow(new WFDefinition { Symbol = "PWFN", Name = "Proces z powiadomieniem" });
        var wezel = session.AddRow(new TaskDefinition("Kontrahenci") { Name = "Akceptacja" });
        wezel.WFDefinition = definicja;

        // Powiadomienie SYSTEMOWE wysyłane przy realizacji zadania węzła:
        var powiadomienie = session.AddRow(new SysNotification(wezel));   // host = węzeł
        powiadomienie.Name = "Po akceptacji";
        powiadomienie.NotificationType = typPowiadomienia;                // tryb System (Template == null)
        powiadomienie.Fact = SysNotificationFact.OnRealize;               // okoliczność
        powiadomienie.RecipientType = SysNotificationRecipientType.ProcessOwner;
        powiadomienie.Finalization = SysNotificationFinalization.OnProcessFinalized;
        powiadomienie.ErrorReaction = SysNotificationErrorReactionType.GenerateSystemNotification;

        // Wariant opóźniony w czasie (Fact + DelayUnit/DelayValue jednym wywołaniem):
        // powiadomienie.SetDelay(SysNotificationDelayUnit.Hours, delayValue: 24);

        t.Commit();
    }
    session.Save();
}

// Odczyt powiadomień węzła (dowolna sesja):
// foreach (SysNotification sn in wezel.SysNotifications) { sn.Name, sn.Fact, sn.Mode }
```

Wariant e-mail — zamiast `NotificationType` ustaw szablon (rekord konfiguracyjny `Soneta.CRM.SzablonEmail`):

```csharp
var szablon = session.AddRow(new SzablonEmail
    { Nazwa = "Akceptacja", DO = "{EMAIL}", Temat = "Zadanie zrealizowane", Tresc = "Dotyczy: {Nazwa}" });
powiadomienie.Template = szablon;     // Mode = Email; pola {…} rozwijane z obiektu zadania
```

**Pułapki:**
- `Host` ustawia się **wyłącznie konstruktorem** `new SysNotification(host)` — nie ma settera;
  do węzłów definicji Engine służy osobna klasa `Soneta.Workflow.Config.WfSysNotificationExtend`
  (też ctor z hostem), w której treść/odbiorców opisuje kod silnika.
- `DelayUnit`/`DelayValue` rzucają `ColReadOnlyException`, dopóki `Fact != OnDelayedTime` —
  ustawiaj przez `SetDelay(...)` albo najpierw `Fact`.
- `NotificationType` przyjmuje tylko definicje zadań z `MultiTaskType.ByGetSysNotifications` —
  pobierz istniejącą standardową dynamicznie, nie twórz własnej bez potrzeby i **nie wpisuj
  Guidów na sztywno**.
- `Mode` jest **wyliczane** z `Template` (`null` → `System`) — nie da się go ustawić wprost.
- Wysyłka e-mail/SMS wymaga skonfigurowanego kanału wysyłki po stronie serwera (usługa/operator
  pocztowy) — sama definicja jest danymi konfiguracyjnymi, ale dostarczenie wiadomości nie zadziała
  „offline"; powiadomienie **systemowe** działa bez sieci (tworzy `Task`).
- Powiadomienie systemowe tworzy **osobne zadanie** (`Task` z `Task.SysNotification != null`,
  definicja = `NotificationType`) dla każdego odbiorcy — potwierdzanie takich zadań opisuje
  WORKFLOW06, odczyt przypomnień WORKFLOW05.
- `Name` ze znakami specjalnymi (np. `/`) zatrzyma zapis weryfikatorem.

### WORKFLOW-I2 — Definiowanie triggera (wyzwalacza) zadania/procesu (★)

**Cel:** sprawić, by zapis wiersza wskazanej tabeli (np. `Kontrahenci`, `DokHandlowe`)
automatycznie przeliczał zadania węzła lub uruchamiał proces — bez udziału operatora.

**Mechanizm (kluczowy):** trigger to wiersz `Soneta.Business.Db.TaskTrigger` (tabela
`TaskTriggers`, konfiguracyjna, **child** węzła `TaskDefinition` — kolekcja
`TaskDefinition.TaskTriggers: SubTable<TaskTrigger>`). W trakcie `session.Save()` platforma dla
każdego zapisywanego wiersza szuka triggerów po kluczu
`session.GetBusiness().TaskTriggers.ByTableName[nazwaTabeli]` i wykonuje algorytm `Code` —
metodę `GetGuidedRows()`, która **mapuje zapisany wiersz na obiekty nadrzędne zadań** (typu
tabeli węzła). Dla zwróconych obiektów silnik przelicza istniejące zadania, a gdy węzeł jest
startowy — uruchamia nowy proces (WORKFLOW-I3).

**Ważne:** `TaskDefinition.OnAdded` **automatycznie dodaje domyślny trigger** dla własnej tabeli
węzła (`TableName` węzła) — ręcznie dodajesz triggery tylko dla **innych** tabel, których zmiany
mają wpływać na zadania węzła.

**Pola i typy (`Soneta.Business.Db.TaskTrigger`, tabela `TaskTriggers`, konfiguracyjna):**

| Pole | Typ | Opis |
|---|---|---|
| `TaskDefinition` | `Soneta.Business.Db.TaskDefinition` | węzeł-właściciel (guided-parent); ustaw **przed** `TableName` |
| `TableName` | `string` | nazwa obserwowanej tabeli; setter **generuje szablon kodu** `GetGuidedRows()` w `Code` |
| `Code` | `Soneta.Business.MemoText` | kod C# metody `public override GuidedRow[] GetGuidedRows()` — mapowanie wiersza na obiekty zadań |
| `Purpose` | `Soneta.Business.Db.TaskTriggerPurpose` | `Uniwersal` (start + przeliczenie), `Creator` (tylko start procesu), `Activator` (tylko przeliczenie) — **zapisywalne tylko w definicjach Engine** |
| `DataType` | `System.Type` | typ wiersza obserwowanej tabeli (wyliczane) |

**Snippet:**

```csharp
using (var session = login.CreateSession(readOnly: false, config: true, name: "Trigger WF"))
{
    using (var t = session.Logout(editMode: true))
    {
        var definicja = session.AddRow(new WFDefinition { Symbol = "TRG", Name = "Proces z triggerem" });

        // Węzeł na tabeli Kontrahenci — OnAdded dodał mu już DOMYŚLNY trigger dla "Kontrahenci":
        var wezel = session.AddRow(new TaskDefinition("Kontrahenci") { Name = "Obsługa kontrahenta" });
        wezel.WFDefinition = definicja;

        // Dodatkowy trigger: zapis ZADANIA CRM (tabela Zadania) ma przeliczać zadania węzła.
        var trigger = session.AddRow(new TaskTrigger());
        trigger.TaskDefinition = wezel;     // 1) najpierw węzeł…
        trigger.TableName = "Zadania";      // 2) …potem tabela — setter generuje szablon GetGuidedRows()

        // 3) kod mapujący zapisany wiersz (zmienna o nazwie typu wiersza, tu: Zadanie)
        //    na obiekty nadrzędne zadań węzła (typ tabeli węzła, tu: Kontrahent):
        trigger.Code = @"public override GuidedRow[] GetGuidedRows() {
    var kontrahent = Zadanie.Kontrahent as Kontrahent;
    return kontrahent != null ? new GuidedRow[] { kontrahent } : new GuidedRow[0];
}";
        t.Commit();
    }
    session.Save();
}

// Odczyt triggerów węzła / tabeli (dowolna sesja):
// foreach (TaskTrigger tt in wezel.TaskTriggers) { tt.TableName }
// var dlaTabeli = session.GetBusiness().TaskTriggers.ByTableName["Zadania"];
```

**Pułapki:**
- **Kolejność setterów ma znaczenie:** `TaskDefinition` przed `TableName` — setter `TableName`
  generuje szablon kodu na podstawie tabeli węzła; odwrotna kolejność kończy się `NullReference`.
- W kodzie `GetGuidedRows()` dostępna jest zmienna o **nazwie typu wiersza** obserwowanej tabeli
  (np. `Zadanie`, `Kontrahent`) oraz `TaskTrigger`; metoda musi zwracać obiekty **typu tabeli
  węzła** — inne zostaną zignorowane.
- `Purpose` w definicjach Standard rzuca `ColReadOnlyException` (zostaje `Uniwersal` = start
  i przeliczenie); rozdział ról `Creator`/`Activator` jest dostępny tylko w definicjach Engine.
- Nie dodawaj drugiego triggera dla własnej tabeli węzła — domyślny powstaje w `OnAdded`
  (chyba że węzeł jest „uniwersalny", `TableName == "?"` — wtedy domyślnego nie ma).
- Trigger działa **w `session.Save()` sesji operacyjnej** — sama edycja w transakcji bez `Save()`
  niczego nie odpala; definicja procesu musi być wdrożona (`IsDeployed`, WORKFLOW-A7)
  i nieodłączona (`Locked == false`).
- Kod triggera to algorytm kompilowany (AssemblyCache) — błąd składni ujawni się przy zapisie
  konfiguracji/użyciu, nie w czasie edycji pola.

### WORKFLOW-I3 — Automatyczny start procesu na zdarzenie zapisu obiektu (★)

**Cel:** proces ma startować sam, gdy w systemie pojawi się (lub zmieni) obiekt biznesowy —
np. nowy kontrahent, nowy dokument — bez akcji operatora.

**Mechanizm (kluczowy):** o automatycznym starcie decyduje konfiguracja **węzła startowego**:
`IsStart = true` (element startowy) + `ActionRunAt = ActionRunAt.Auto` (akcja automatyczna).
Podczas `session.Save()` obiektu z tabeli triggera (domyślny trigger węzła obserwuje jego własną
tabelę — WORKFLOW-I2) silnik: znajduje triggery po `ByTableName`, mapuje wiersz przez
`GetGuidedRows()` i — jeśli dla tego obiektu **nie istnieją jeszcze zadania** węzła — uruchamia
nowy proces od węzła startowego. Warunki: definicja **wdrożona** (`IsDeployed == true`),
definicja i węzeł nie są `Locked`. Dla startu na dodanie obiektu silnik wykorzystuje też ścieżkę
`OnAdded` (start „AutoStart") — z perspektywy konfiguracji warunki są te same.

**Warianty:**

| Wariant | Konfiguracja | Kiedy startuje |
|---|---|---|
| Start automatyczny (Standard) | `IsStart = true`, `ActionRunAt = Auto` | `session.Save()` obiektu źródłowego |
| Start automatyczny (Engine) | `StartPointType = TaskStartPointTypeEnum.Automatic` (ustawia `IsStart` + `ActionRunAt.Auto`) | jw. — pole zapisywalne **tylko** w definicjach Engine |
| Start manualny (porównanie) | `IsStart = true`, `ActionRunAt = InMenu` | operator z menu/panelu — WORKFLOW04 |
| Start z harmonogramu | jw. + `ScheduleDefinition` | usługa HZ — WORKFLOW-I5 |

**Pola i typy (`Soneta.Business.Db.TaskDefinition` — pola istotne dla startu):**

| Pole | Typ | Opis |
|---|---|---|
| `IsStart` | `bool` | węzeł startowy procesu; w definicjach Engine pole tylko do odczytu (steruje nim `StartPointType`) |
| `ActionRunAt` | `Soneta.Business.Db.ActionRunAt` | `Auto` (start automatyczny), `InMenu` (start ręczny), `Default` |
| `StartPointType` | `Soneta.Business.Db.TaskStartPointTypeEnum` | `NoStartPoint` / `Automatic` / `InWorkflowPanel` / `InDocumentMenu` / `Subprocess` — zapisywalne tylko Engine |
| `EnableCondition` / `EnableExpression` | `string` | warunek utworzenia zadania (filtr obiektów, które mają startować proces) |
| `WFDefinition.IsDeployed` | `bool` | tylko wdrożona definicja startuje automatycznie |
| `WFDefinition.SingleWorkflowInstance` | `bool` | `true` blokuje drugi aktywny proces dla tego samego obiektu |

**Snippet:**

```csharp
// 1) KONFIGURACJA — definicja z węzłem startowym uruchamianym automatycznie:
using (var config = login.CreateSession(readOnly: false, config: true, name: "Autostart WF"))
{
    using (var t = config.Logout(editMode: true))
    {
        var definicja = config.AddRow(new WFDefinition { Symbol = "AKNT", Name = "Nowy kontrahent" });

        var start = config.AddRow(new TaskDefinition("Kontrahenci") { Name = "Uzupełnij dane" });
        start.WFDefinition = definicja;
        start.IsStart = true;                    // węzeł startowy…
        start.ActionRunAt = ActionRunAt.Auto;    // …uruchamiany automatycznie (domyślny trigger już istnieje)

        definicja.IsDeployed = true;             // tylko wdrożona definicja startuje sama (WORKFLOW-A7)
        t.Commit();
    }
    config.Save();
}

// 2) OPERACYJNIE — zapis nowego kontrahenta uruchamia proces W TRAKCIE Save():
using (var session = login.CreateSession(readOnly: false, config: false, name: "Nowy kontrahent"))
{
    Kontrahent kontrahent;
    using (var t = session.Logout(editMode: true))
    {
        kontrahent = session.AddRow(new Kontrahent());
        kontrahent.Kod = "AUTO-001";
        kontrahent.Nazwa = "Firma Testowa Sp. z o.o.";
        t.Commit();
    }
    session.Save();   // tu silnik odpala triggery: powstaje WFWorkflow + Task węzła startowego

    // Weryfikacja — zadanie i proces dla zapisanego obiektu:
    var zadania = session.GetBusiness().Tasks.WgParent[kontrahent];
    Task zadanie = zadania.OfType<Task>().FirstOrDefault();
    IWFWorkflow proces = zadanie?.WFWorkflow;          // instancja procesu (WORKFLOW04)
}
```

**Pułapki:**
- **Proces nie wystartuje**, dopóki `IsDeployed == false` — świeża definicja jest w trybie
  modelowania; wdrożenie (WORKFLOW-A7) to świadoma, jednokierunkowa decyzja.
- `StartPointType` w definicji Standard rzuca `ColReadOnlyException` — używaj pary
  `IsStart` + `ActionRunAt`; w definicji Engine odwrotnie: `IsStart`/`ActionRunAt` są readonly.
- Start następuje dopiero w **`session.Save()`** — w transakcji po `Commit()` zadania jeszcze
  nie ma; weryfikuj po zapisie.
- Modyfikacja (nie tylko dodanie) wiersza obserwowanej tabeli też odpala trigger — jeśli proces ma
  startować tylko dla wybranych obiektów, zawęź `EnableCondition`/kod triggera; przed masowymi
  operacjami seryjnymi sprawdź `Definition.IncompatibleWithSerialOperations` (WORKFLOW04).
- `SingleWorkflowInstance = true` (domyślne) wycisza drugi start dla tego samego obiektu, dopóki
  aktywny proces nie zostanie zamknięty — to nie błąd, to ochrona przed duplikatami.
- Zapis obiektu w **sesji konfiguracyjnej** nie uruchamia procesów operacyjnych — scenariusz
  wymaga dwóch sesji jak w snippecie.

### WORKFLOW-I4 — Automatyczna tranzycja (bez decyzji użytkownika)

**Cel:** przejście między węzłami ma wykonywać się samo, gdy spełni się warunek na danych
(np. uzupełniono pole obiektu) — operator nie podejmuje decyzji.

**Mechanizm (kluczowy):** tranzycja automatyczna to zwykła `WFTransition` (WORKFLOW03)
z `IsUserDecision = false` i zdefiniowanym warunkiem realizacji. Warunek opisuje generowana
metoda `IsRealized(Task)` kalkulatora tranzycji — najprościej budują ją **pola wyrażeniowe**
(`RowCondition` — patrz rowcondition.md): `IsRealizedExpressionParent` (warunek na obiekcie
nadrzędnym zadania), `IsRealizedExpressionTask` (na zadaniu), `IsRealizedExpressionTransition`
(na tranzycji), łączone operatorem `OperatorType` (`And`/`Or`). Ustawienie wyrażenia generuje kod
do `Statement` (pełny kod algorytmu można też wpisać ręcznie). Gdy przy przeliczaniu zadania
(zapis powiązanego obiektu → trigger; decyzja w innym węźle; `RecalculateAfterEditing`) warunek
zwróci `true`, silnik wykonuje tranzycję: `Check` → `Action` → realizacja zadania źródłowego
(chyba że `WeakTransition`) → utworzenie zadania węzła docelowego.

**Pola i typy (`Soneta.Workflow.Config.WFTransition` — pola automatyzacji):**

| Pole | Typ | Opis |
|---|---|---|
| `IsUserDecision` | `bool` | `false` = tranzycja nie czeka na decyzję operatora |
| `IsRealizedExpressionParent` | `string` (RowCondition) | warunek na `task.Parent` (obiekt nadrzędny; typ = tabela węzła źródłowego) |
| `IsRealizedExpressionTask` | `string` (RowCondition) | warunek na zadaniu (`Task`) |
| `IsRealizedExpressionTransition` | `string` (RowCondition) | warunek na tranzycji (`WFTransition`) |
| `OperatorType` | `WFTransition.OperatorEnum` (`And`/`Or`) | operator łączący powyższe warunki |
| `Statement` | `Soneta.Business.MemoText` | pełny kod algorytmu kalkulatora (`IsRealized`/`Check`/`Action`) — alternatywa dla wyrażeń |
| `CheckCode`, `ActionCode` | `Soneta.Business.Db.AlgorithmColumn` | algorytmy walidacji i akcji wykonywanej przy realizacji |
| `WeakTransition` | `bool` | tranzycja **nie kończy** zadania źródłowego (np. równoległe powiadomienie) |
| `Source`, `Target` | `Soneta.Business.Db.TaskDefinition` | węzeł źródłowy / docelowy |

**Snippet:**

```csharp
using (var session = login.CreateSession(readOnly: false, config: true, name: "Tranzycja automatyczna"))
{
    using (var t = session.Logout(editMode: true))
    {
        var definicja = session.AddRow(new WFDefinition { Symbol = "ATR", Name = "Proces z auto-tranzycją" });

        var start = session.AddRow(new TaskDefinition("Kontrahenci") { Name = "Weryfikacja danych" });
        start.WFDefinition = definicja;
        start.IsStart = true;
        start.ActionRunAt = ActionRunAt.Auto;

        var koniec = session.AddRow(new TaskDefinition("Kontrahenci") { Name = "Zakończenie" });
        koniec.WFDefinition = definicja;
        koniec.EndType = TaskEndTypeEnum.Workflow;          // węzeł końcowy procesu (WORKFLOW02)

        // Tranzycja automatyczna: wykonuje się sama, gdy kontrahent ma uzupełniony e-mail.
        var tranzycja = session.AddRow(new WFTransition());
        tranzycja.Name = "Dane kompletne";
        tranzycja.Source = start;
        tranzycja.Target = koniec;
        tranzycja.IsUserDecision = false;                   // bez decyzji operatora

        // Warunek RowCondition na obiekcie nadrzędnym zadania (Kontrahent) — generuje IsRealized:
        tranzycja.IsRealizedExpressionParent = "EMAIL <> ''";
        tranzycja.OperatorType = WFTransition.OperatorEnum.And;

        definicja.IsDeployed = true;
        t.Commit();
    }
    session.Save();
}
// Działanie: zapis kontrahenta z uzupełnionym EMAIL przelicza zadanie (trigger, WORKFLOW-I2)
// → IsRealized == true → zadanie "Weryfikacja danych" zrealizowane, proces przechodzi dalej.
```

**Pułapki:**
- Warunki `IsRealizedExpression…` to składnia **RowCondition** (jak filtry list), nie C# — patrz
  rowcondition.md; pełny C# wpisuje się do `Statement` (generowana klasa kalkulatora z metodami
  `IsRealized`/`Check`/`Action` — kompilacja przez AssemblyCache, błąd wyjdzie przy użyciu).
- Tranzycja automatyczna **nie wykona się sama z siebie** — potrzebne jest przeliczenie zadania:
  zapis obiektu obserwowanego przez trigger węzła źródłowego (WORKFLOW-I2) albo inna decyzja
  w procesie. Nie ma publicznego `Task.Recalculate()` do wymuszenia z kodu.
- `IsUserDecision = true` z warunkiem `IsRealized` to inny wzorzec (decyzja widoczna dopiero po
  spełnieniu warunku) — decyzje użytkownika i ich warianty opisuje WORKFLOW03/WORKFLOW06.
- `WeakTransition = true` tworzy zadanie docelowe **bez kończenia** źródłowego — używaj do
  rozgałęzień informacyjnych; uważaj na lawinę zadań przy wielokrotnym przeliczaniu.
- `IsDefaultTransition` nie czyni tranzycji automatyczną — to tylko domyślny wybór prezentowany
  operatorowi.
- Kolejność: najpierw `Source`/`Target` (z nich wynika definicja procesu i typ obiektu dla
  wyrażeń), potem warunki.

### WORKFLOW-I5 — Uruchamianie procesu z harmonogramu (ScheduleDefinition)

**Cel:** cykliczne (np. codzienne) automatyczne uruchamianie procesu / przeliczanie zadań węzła —
bez zdarzenia zapisu, sterowane zegarem.

**Mechanizm (kluczowy):** harmonogram opisuje wiersz `Soneta.Core.Schedule.ScheduleDefinition`
(tabela `ScheduleDefs`, konfiguracyjna; dostęp `session.GetCore().ScheduleDefs`, klucz
`WgTaskDefinition`). Definicję harmonogramu wiąże się z węzłem przez pole `TaskDefinition`
(węzeł widzi swoje harmonogramy w `TaskDefinition.ScheduleDefs`, property `HasScheduleAutoJob`).
Wystąpienia cyklu wykonuje **usługa Harmonogram Zadań (HZ)** działająca na serwerze — w terminie
cyklu woła na węźle `IScheduleAutoJob.AutoAction(scheduleDefinition)` (interfejs implementowany
przez `TaskDefinition`), co uruchamia akcję automatyczną węzła: domyślną (start procesu /
przeliczenie zadań) albo metodę oznaczoną atrybutem `[AutoAction]` w kodzie węzła
(`TaskDefinition.OtherMethods`).

**Pola i typy (`Soneta.Core.Schedule.ScheduleDefinition`, tabela `ScheduleDefs`, konfiguracyjna):**

| Pole | Typ | Opis |
|---|---|---|
| `Name` | `string` | nazwa definicji harmonogramu |
| `TaskDefinition` | `Soneta.Business.Db.TaskDefinition` | węzeł, którego akcję wywołuje harmonogram |
| `ScheduleType` | `Soneta.Core.ScheduleTypeEnum` | `UseCycleDefinition` (wg cyklu) / `UseFileWatcher` (wystąpienie pliku w `FolderIn`/`FolderOut`) |
| `CycleDefinition` | `Soneta.Core.DefinicjaCyklu` (subrow) | opis cyklu: `Typ` (`Jednorazowy`/`Minutowy`/`Godzinowy`/`Dzienny`/`Tygodniowy`/`Miesieczny`/`Roczny`/`Algorytm`), `Czas: Soneta.Types.Time`, `Interwal: int`, dni tygodnia/miesiące (`bool`), `SposobNaDniWolne`, … |
| `StartDate`, `EndDate` | `Soneta.Types.Date` | okres obowiązywania cyklu (`OkresCyklu: FromTo` — odczyt) |
| `DefaultAutoAction` | `bool` | `true` = użyj akcji domyślnej węzła; `false` = wskaż metodę `[AutoAction]` |
| `AutoActionName` | `AutoActionAttribute.AutoActionBase` | wybrana metoda automatyczna; ustawiaj przez `SetAutoActionName(nazwaMetody)` przy `DefaultAutoAction == false` |
| `HzInstance` | `string` | nazwa instancji usługi HZ (gdy działa wiele instancji) |
| `Priority` | `int` | priorytet zadania w kolejce HZ |
| `ExceptionStrategy` | `Soneta.Core.ExceptionStrategyEnum` | strategia obsługi wyjątków wykonania |
| `Locked` | `bool` | harmonogram wyłączony |
| `Host` | `Soneta.Business.IScheduleAutoJob` | opcjonalny inny rekord-host akcji (domyślnie działa `TaskDefinition`) |

**Snippet:**

```csharp
using (var session = login.CreateSession(readOnly: false, config: true, name: "Harmonogram WF"))
{
    using (var t = session.Logout(editMode: true))
    {
        // Węzeł startowy procesu (jak w WORKFLOW-I3); tu zakładamy minimalną definicję:
        var definicja = session.AddRow(new WFDefinition { Symbol = "HZWF", Name = "Proces cykliczny" });
        var start = session.AddRow(new TaskDefinition("Kontrahenci") { Name = "Przegląd kontrahentów" });
        start.WFDefinition = definicja;
        start.IsStart = true;
        definicja.IsDeployed = true;

        // Harmonogram: codziennie o 06:00, akcja domyślna węzła (start procesu):
        var harmonogram = session.AddRow(new ScheduleDefinition());
        harmonogram.Name = "Codzienny przegląd";
        harmonogram.TaskDefinition = start;
        harmonogram.ScheduleType = ScheduleTypeEnum.UseCycleDefinition;
        harmonogram.DefaultAutoAction = true;
        harmonogram.StartDate = Date.Today.AddDays(1);          // pierwsze wywołanie nie może być z przeszłości
        harmonogram.CycleDefinition.Typ = DefinicjaCykluTyp.Dzienny;
        harmonogram.CycleDefinition.Czas = new Time(6, 0);

        t.Commit();
    }
    session.Save();
}

// Odczyt harmonogramów węzła:
// var harmonogramy = session.GetCore().ScheduleDefs.WgTaskDefinition[wezel];
// bool ma = wezel.HasScheduleAutoJob;
```

**Pułapki:**
- **Wykonanie wymaga działającej usługi Harmonogram Zadań** na serwerze — sama definicja jest
  danymi konfiguracyjnymi (zapis przejdzie wszędzie), ale bez HZ nic nie wystartuje; nie testuj
  „odpalenia" cyklu na lokalnej bazie bez usługi.
- Data pierwszego wywołania nie może być z przeszłości — weryfikator zatrzyma zapis
  (komunikat „Data pierwszego wywołania nie może być z przeszłości…"); ustawiaj `StartDate`
  / `CycleDefinition` w przyszłości.
- `AutoActionName` jest **tylko do odczytu**, gdy `DefaultAutoAction == true`; własną metodę
  wybiera się parą: `DefaultAutoAction = false` + `SetAutoActionName("NazwaMetody")` — metoda
  musi istnieć w `TaskDefinition.OtherMethods` z atrybutem `[AutoAction("Opis")]`
  (dokładnie **jedna** metoda może być oznaczona jako domyślna `[AutoAction("…", true)]`,
  inaczej wyjątek przy odczycie `AutoActionName`).
- `CycleDefinition` to **subrow** — ustawiaj jego pola (`CycleDefinition.Typ = …`), nie podmieniaj
  całego obiektu; pola `Termin…` są wewnętrznym zapisem terminu, nie ustawiaj ich wprost.
- `ScheduleDefinition` należy do modułu `Soneta.Core` (`session.GetCore().ScheduleDefs`),
  a nie do `WorkflowModule` — mimo że steruje procesami workflow.
- Węzeł wskazany w `TaskDefinition` musi być startowy i wdrożony, by akcja domyślna uruchomiła
  proces (warunki jak w WORKFLOW-I3).
