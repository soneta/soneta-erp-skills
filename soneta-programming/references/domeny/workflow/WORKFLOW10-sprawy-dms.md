# WORKFLOW10 — Sprawy (DMS / Matters) i rejestr

> Wspólne fakty o typie, podstawowe typy i szablon wzorca: [../workflow.md](../workflow.md).

> **Model pojęciowy DMS.** **Sprawa** (`Soneta.Workflow.Dms.Matter`, tabela `Matters`, dane
> operacyjne) grupuje **dokumenty podstawowe** (`Soneta.Workflow.Dms.BasicDocument`, tabela
> `BasicDocs`) — pisma rejestrowane w **rejestrach** (`Soneta.Workflow.Dms.Register`, tabela
> `Registers`, config). Sprawa zachowuje się jak dokument (implementuje `IDokument`): ma definicję
> (`Soneta.Workflow.Dms.Config.MatterDefinition`, tabela `MatterDefs`, config), numer
> (`Soneta.Core.NumerDokumentu`) i automatyczną numerację. Klasyfikację kancelaryjną zapewnia
> **wykaz akt** (`UnifiedRegister` / `UnifiedRegisterClass` — JRWA, config). Dostęp z sesji:
> `session.GetDms()` (`Soneta.Workflow.Dms.DmsModule`) — tabele `Matters`, `MatterDefs`,
> `BasicDocs`, `BasicDocDefs`, `Registers`, `UnifiedRgs`, `UnifiedRgClasses`.
> Powiązanie z silnikiem workflow jest **przez zadania**: proces uruchomiony „na sprawie" ma
> `Task.Parent = matter` — sprawa jest obiektem głównym zadań procesu.

### WORKFLOW-J1 — Założenie sprawy i powiązanie z procesem workflow (★)

**Cel:** utworzyć nową sprawę DMS na podstawie definicji sprawy i (opcjonalnie) uruchomić na niej
proces workflow, tak aby zadania procesu wskazywały sprawę jako obiekt główny.

**Warianty:**

| Wariant | Droga |
|---|---|
| Sprawa zakładana ręcznie (kod) | `new Matter(definition)` + `Matters.AddRow(...)` — snippet poniżej |
| Sprawa z dokumentu rejestru | czynność **„Załóż nową sprawę"** na formularzu dokumentu podstawowego (worker UI); publiczny odpowiednik w kodzie: utwórz `Matter` jak niżej i ustaw `basicDocument.Matter = matter` (WORKFLOW-J2) |
| Sprawa generowana z procesu | generator obiektów (`OGSchema`) tworzący `Matter` w węźle procesu — patrz WORKFLOW09 |
| Powiązanie z procesem workflow | start procesu na sprawie: `wfDefinition.GetStartPoint().StartProcess(matter, null)` — patrz WORKFLOW04 |

**Pola i typy:**

| Pole | Typ | Uwaga |
|---|---|---|
| `Definition` | `Soneta.Workflow.Dms.Config.MatterDefinition` | wymagane, tylko-do-odczytu (ustawiane konstruktorem) |
| `Number` | `Soneta.Core.NumerDokumentu` (subrow) | numer nadawany z numeratora definicji; `Number.NumerPelny: string` |
| `Title` | `string` (255) | tytuł sprawy |
| `Description` | `Soneta.Business.MemoText` | opis |
| `Creator` | `Soneta.Business.App.Operator` | ustawiany automatycznie w `OnAdded` (operator sesji) |
| `Leader` | `Soneta.Business.App.Operator` | prowadzący sprawę |
| `RegistrationDatetime` | `System.DateTime` | data/czas rejestracji — ustawiany automatycznie |
| `Date` | `Soneta.Types.Date` | data sprawy (kalkulowana z `RegistrationDatetime`) |
| `Parent` / `ChildMatters` | `Matter` / `SubTable<Matter>` | hierarchia spraw (przypisanie samej siebie rzuca `BusException`) |
| `SubstantiveCell` | `Soneta.Core.ElementStrukturyOrganizacyjnej` | komórka merytoryczna (uczestniczy w numeracji) |
| `MatterAccess` | `Soneta.Workflow.Enums.AccessEnum` | dostęp do sprawy; steruje prawami rekordowymi |
| `UnifiedRegisterClass` | `Soneta.Workflow.Dms.Config.UnifiedRegisterClass` | klasa wykazu akt (WORKFLOW-J2) |
| `BasicDocuments` | `Soneta.Business.SubTable<BasicDocument>` | dokumenty podstawowe sprawy |
| `CloseDatetime` / `ArchivedTerm` | `System.DateTime` | zakończenie / archiwizacja (readonly-set) |

**Snippet:**

```csharp
using System.Linq;
using Soneta.Business;
using Soneta.Workflow.Config;     // rozszerzenie StartProcess(TaskDefinition, GuidedRow, Row)
using Soneta.Workflow.Dms;

var dms = session.GetDms();

// definicję sprawy pobieramy dynamicznie (w bazie standardowo jest "Sprawa Ogólna" / symbol "SO")
var definition = dms.MatterDefs.ByName.First();

Matter matter;
using (var t = session.Logout(editMode: true))
{
    matter = new Matter(definition);
    dms.Matters.AddRow(matter);            // OnAdded: RegistrationDatetime, Creator, numer

    matter.Title  = "Reklamacja dostawy 04/2026";
    matter.Leader = session.Login.Operator;

    t.Commit();
}
session.Save();

// powiązanie z procesem workflow — start procesu "na sprawie" (definicja procesu: WORKFLOW01/04)
// using (var t = session.Logout(editMode: true))
// {
//     var startTask = wfDefinition.GetStartPoint().StartProcess(matter, null);
//     t.Commit();
// }
// session.Save();

// zadania workflow powiązane ze sprawą (sprawa = obiekt główny zadania)
var taskiSprawy = session.GetBusiness().Tasks.WgParent[matter];
```

**Pułapki:**
- `Definition` jest **wymagana i readonly** — jedyna droga to konstruktor `new Matter(definition)`.
- `MatterDefinition` to dane **config** — nową definicję tworzy się w transakcji na **sesji
  konfiguracyjnej** (patrz `session-login.md`). Świeżo dodana definicja implementuje
  `IRightsSource` i **bez nadania praw** operatorzy dostają `AccessWriteDeniedException` przy
  próbie założenia sprawy. Prawa nadaje się publicznym workerem
  `Soneta.Business.Db.SourceEntitleRightWorker` (`Source` = definicja, `Entitle` = uprawnienie,
  `AccessRight = AccessRights.Granted`) — dlatego w scenariuszach na Demo wygodniej użyć
  **istniejącej** definicji standardowej.
- `Creator`, `RegistrationDatetime`, `Number` ustawia platforma — nie nadpisuj ich ręcznie.
- Numerator standardowej definicji zawiera segmenty `SubstantiveCell.Kod` i
  `UnifiedRegisterClass.Symbol` — zmiana tych pól **przelicza numer** sprawy.
- Lista „DMS/Sprawy" i czynności DMS wymagają **licencji DMS**; sam zapis danych w kodzie
  biznesowym licencji nie sprawdza, ale workery tak (kontekst `LicencjeModułu2.DMS`).
- `matter.Parent = matter` rzuca `BusException` („nie może wskazywać samej siebie").
- Transakcja + `Commit()` + `session.Save()` — wzorce w `safe-code.md`.

### WORKFLOW-J2 — Rejestracja dokumentu w sprawie / rejestrze (★)

**Cel:** zarejestrować dokument podstawowy (pismo) w rejestrze DMS i dołączyć go do sprawy;
opcjonalnie sklasyfikować dokument/sprawę klasą wykazu akt (rejestr ujednolicony / JRWA).

**Warianty:**

| Wariant | Droga |
|---|---|
| Nowe pismo w rejestrze | `new BasicDocument(definition, register)` + `BasicDocs.AddRow(...)` |
| Dołączenie pisma do sprawy | `basicDocument.Matter = matter` (czynność UI: „Powiąż dokumenty podstawowe" na sprawie) |
| Klasyfikacja wykazem akt | `matter.UnifiedRegisterClass = klasa` / `basicDocument.UnifiedRegisterClass = klasa` |
| Dokumenty sprawy (odczyt) | `matter.BasicDocuments` lub `dms.BasicDocs.WgMatter[matter]` |

**Pola i typy (`Soneta.Workflow.Dms.BasicDocument`):**

| Pole | Typ | Uwaga |
|---|---|---|
| `Definition` | `Soneta.Workflow.Dms.Config.BasicDocumentDefinition` | wymagane, readonly (konstruktor); standardowo m.in. „Pismo" (PMO) |
| `Register` | `Soneta.Workflow.Dms.Register` | wymagany; standardowe rejestry: „Przychodzące" (P), „Wychodzące" (W), „Wewnętrzne" (WEW) |
| `Number` | `Soneta.Core.NumerDokumentu` (subrow) | numerator definicji (segmenty m.in. `Register.Symbol`) |
| `Date` | `Soneta.Types.Date` | data pisma — edytowalna **tylko przed pierwszym zapisem** (`IsAdded`) |
| `DateOfReceipt` / `DateOfDispatch` | `System.DateTime` / `Soneta.Types.Date` | data wpływu (automat w `OnAdded`) / data nadania |
| `Matter` | `Soneta.Workflow.Dms.Matter` | sprawa; ustawienie wypełnia `AddingDatetime` i dziedziczy `SubstantiveCell` ze sprawy |
| `AddingDatetime` | `System.DateTime` | data/czas dodania do sprawy (readonly-set, automat) |
| `UnifiedRegisterClass` | `Soneta.Workflow.Dms.Config.UnifiedRegisterClass` | klasa wykazu akt; zmiana przelicza numer i dodaje weryfikator ostrzegający przy klasach/wykazach zablokowanych (`Locked`) |
| `BusinessEntity` / `Person` | `Soneta.CRM.IPodmiot` / `Soneta.CRM.KontaktOsoba` | podmiot / osoba pisma |
| `Responsible` | `Soneta.Business.App.Operator` | odpowiedzialny za dokument |
| `DocumentAccess` | `Soneta.Workflow.Enums.AccessEnum` | dostęp do dokumentu (domyślnie z definicji) |
| `ArchiveCategory` | subrow (`Symbol: Soneta.Core.ElemSlownika`, `Period: int`) | kategoria archiwalna |

**Snippet:**

```csharp
using System.Linq;
using Soneta.Business;
using Soneta.Workflow.Dms;

var dms = session.GetDms();

// dane słownikowe dynamicznie — definicja pisma i rejestr istnieją standardowo w każdej bazie
var defPisma = dms.BasicDocDefs.ByName.First();
var rejestr  = dms.Registers.ByName.First();
var sprawa   = dms.Matters.WgDefinition[dms.MatterDefs.ByName.First()].GetNext() as Matter;

BasicDocument pismo;
using (var t = session.Logout(editMode: true))
{
    pismo = new BasicDocument(defPisma, rejestr);
    dms.BasicDocs.AddRow(pismo);           // OnAdded: Date=dzisiaj, DateOfReceipt, DocumentAccess

    pismo.DocumentNote = "Pismo przychodzące — reklamacja";
    pismo.Matter = sprawa;                 // dołączenie do sprawy: AddingDatetime + SubstantiveCell

    t.Commit();
}
session.Save();

// dokumenty sprawy — przez kolekcję lub klucz (serwerowo, bez iteracji po całej tabeli)
var dokumentySprawy = sprawa.BasicDocuments;                 // SubTable<BasicDocument>
var teSame          = dms.BasicDocs.WgMatter[sprawa];        // klucz WgMatter
```

**Pułapki:**
- `Register` jest **wymagany** — konstruktor `new BasicDocument(definition, register)`; gdy rejestr
  jest **główny** (`Register.IsMain == true`), pole `Register` na dokumencie jest tylko-do-odczytu
  (`IsReadOnlyRegister()`).
- `Date` można ustawić **tylko na świeżo dodanym wierszu** (`IsAdded`) — po zapisie
  `ColReadOnlyException`.
- Dokumentu dołączonego do sprawy **nie można usunąć** (`OnDeleting` rzuca wyjątek, a
  `GetCanDelete()` zwraca `false`, dopóki `Matter != null`) — najpierw `pismo.Matter = null`.
- Ustawienie `UnifiedRegisterClass` (na sprawie i na dokumencie) dodaje do sesji weryfikator
  **ostrzeżenia** (klasa lub jej wykaz akt `Locked`) i **przelicza numer** — nie zmieniaj klasy
  „po cichu" na zarejestrowanych dokumentach.
- `Series` (seria) jest edytowalna tylko, gdy definicja ma `Series == true` — inaczej
  `ColReadOnlyException`.
- Definicje (`BasicDocumentDefinition`, `Register`, wykazy akt) to dane **config** + `IRightsSource`
  — jak w J1: nowe wpisy wymagają sesji konfiguracyjnej i nadania praw.

### WORKFLOW-J3 — Przegląd spraw na liście DMS (★)

**Cel:** odczytać sprawy serwerowo — odpowiednik listy **DMS/Sprawy** (FolderView, tabela `Matters`)
— z filtrami: definicja, zakładający, prowadzący, klasa wykazu akt, komórka merytoryczna, okres.

**Warianty (filtry listy):**

| Filtr | Warunek / klucz |
|---|---|
| Definicja sprawy | `Matters.WgDefinition[definition]` lub `FieldCondition.Equal("Definition", definition)` |
| Zakładający / Prowadzący | `Matters.WgCreator[op]` / `Matters.WgLeader[op]` |
| Klasa wykazu akt | `Matters.WgUnifiedRegisterClass[klasa]` |
| Komórka merytoryczna | `Matters.WgSubstantiveCell[element]` |
| Okres (data sprawy) | `RowCondition.ContainsIn("Date", okres)` gdzie `okres: Soneta.Types.FromTo` |
| Hierarchia | `Matters.WgParent[matterNadrzedna]` / `matter.ChildMatters` |
| Prawa dostępu | `Matters.GetGrantedView()` — widok filtrujący sprawy niedostępne dla operatora |

**Pola i typy:** klucze `Soneta.Workflow.Dms.Matters` (tabela): `WgDefinition`, `WgCreator`,
`WgLeader`, `WgSubstantiveCell`, `WgParent`, `WgUnifiedRegisterClass`, `WgDivision`, `ByDate`
(klucz główny po `RegistrationDatetime`), `NumberWgNumeruDokumentu` (`this[string pelny] : Matter`),
`NumberWgSymboluDokumentu` (`this[string symbol, int numer] : Matter`).

**Snippet:**

```csharp
using System;
using System.Linq;
using Soneta.Business;
using Soneta.Types;
using Soneta.Workflow.Dms;

var dms = session.GetDms();

// 1) kod biznesowy: SubTable[condition] — serwerowo, bez materializacji View (rowcondition.md)
var mojeAktywneWTymMiesiacu = dms.Matters.WgLeader[session.Login.Operator]
    [RowCondition.ContainsIn("Date", FromTo.Month(Date.Today)) &
     new FieldCondition.Equal("CloseDatetime", DateTime.MinValue)];

foreach (Matter sprawa in mojeAktywneWTymMiesiacu)
    Console.WriteLine($"{sprawa.Number.NumerPelny}  {sprawa.Title}  ({sprawa.Leader?.Name})");

// 2) odpowiednik listy UI z prawami dostępu (tak buduje widok lista "DMS/Sprawy")
var view = dms.Matters.GetGrantedView();
view.Condition &= new FieldCondition.Equal("Definition", dms.MatterDefs.ByName.First());

// 3) sprawa po pełnym numerze (format wg numeratora definicji,
//    standardowo: SubstantiveCell.Kod/UnifiedRegisterClass.Symbol/numer/rok)
var dowolna   = dms.Matters.ByDate.Cast<Matter>().First();
var poNumerze = dms.Matters.NumberWgNumeruDokumentu[dowolna.Number.NumerPelny];
```

**Pułapki:**
- `GetGrantedView()` filtruje po prawach (`GetObjectRight()`/`AccessRight`) **po stronie klienta**
  (FilterCondition) — w masowym kodzie biznesowym filtruj kluczami/warunkami, a prawa sprawdzaj
  punktowo (`matter.AccessRight != AccessRights.Denied`).
- „Sprawa zakończona" to `CloseDatetime != DateTime.MinValue` — nie ma osobnej flagi bool;
  analogicznie archiwizacja (`ArchivedTerm`).
- Okresy podawaj jako `Soneta.Types.FromTo` (`FromTo.Month(...)`, `FromTo.Year(...)`) — tak
  filtruje standardowa lista (domyślnie bieżący miesiąc).
- Sprawy z `MatterAccess != Public` podlegają **prawom rekordowym** (`RecordPermission`) — operator
  spoza uprawnionych zobaczy `AccessRights.Denied` mimo trafienia w warunek.
- Folder listy: `FolderView("DMS/Sprawy", TableName = "Matters")` — wymaga licencji DMS.

### WORKFLOW-J4 — Śledzenie zmian i historii sprawy

**Cel:** odczytać chronologiczną historię sprawy — kto, kiedy i co zmienił w sprawie, jej
dokumentach podstawowych (wraz z dokumentami dodatkowymi) i zadaniach workflow powiązanych ze sprawą.

**Mechanizm:** publiczny worker `Soneta.Workflow.Workers.MatterCaseWorker` (zarejestrowany dla
kontekstu `Matter`) agreguje wpisy **rejestru zmian** (`Soneta.Business.Db.ChangeInfo`, klucz
`ChangeInfos.BySource`) dla: samej sprawy, jej `BasicDocuments` (i ich krotek dokumentów
dodatkowych) oraz zadań (`Task`), których `Parent` wskazuje te obiekty; wynik sortuje po czasie.

**Pola i typy:**

| Element | Typ | Uwaga |
|---|---|---|
| `MatterCaseWorker.Matter` | `Soneta.Workflow.Dms.Matter` | kontekst (`[Context]`) — wystarczy przypisać property |
| `MatterCaseWorker.MatterCaseChangeInfos` | `List<Soneta.Workflow.Workers.MatterCaseChangeInfo>` | gotowa historia (liczona leniwie) |
| `MatterCaseWorker.Number` / `Title` / `RegistrationDatetime` | `string` | nagłówek raportu historii |
| `MatterCaseChangeInfo.DateTime` | `System.DateTime` | moment zmiany |
| `MatterCaseChangeInfo.Responsible` | `string` | operator (lub użytkownik web) dokonujący zmiany |
| `MatterCaseChangeInfo.TakenAction` | `string` | typ operacji + opis/treść zmiany |
| `MatterCaseChangeInfo.RowDescription` | `string` | czego dotyczyła zmiana (Sprawa / Dokument podstawowy / Zadanie + numer) |

**Snippet:**

```csharp
using System;
using Soneta.Workflow.Dms;
using Soneta.Workflow.Workers;

var dms = session.GetDms();
var sprawa = dms.Matters.WgDefinition[dms.MatterDefs.ByName.First()].GetNext() as Matter;

var worker = new MatterCaseWorker { Matter = sprawa };

Console.WriteLine($"Historia sprawy {worker.Number} „{worker.Title}” " +
                  $"(zarejestrowana {worker.RegistrationDatetime})");

foreach (var wpis in worker.MatterCaseChangeInfos)
    Console.WriteLine($"{wpis.DateTime:g} | {wpis.Responsible} | {wpis.TakenAction} | {wpis.RowDescription}");
```

**Pułapki:**
- Historia opiera się na **rejestrze zmian** (`ChangeInfo`) — wpisy istnieją tylko dla tabel, dla
  których włączono rejestrację zmian w konfiguracji systemu
  (`session.Login.GetTableChangeInfos(tableName)`). Przy wyłączonym rejestrze lista jest pusta —
  to nie błąd.
- Zakres treści wpisu zależy od trybu rejestracji: przy `ChangeInfoMode.StoreContent` w
  `TakenAction` znajdzie się także zawartość zmiany (`Data`), inaczej tylko typ operacji i notatka.
- Worker czyta dane bieżącej sesji — wywołuj go **poza** transakcją edycyjną, na zapisanych danych;
  zmiana property `Matter` unieważnia zbuforowaną listę.
- `MatterCaseChangeInfos` obejmuje też **zadania workflow** dotyczące sprawy i jej dokumentów —
  to właściwe miejsce, by pokazać pełny przebieg obsługi sprawy (rejestracja pisma → proces →
  decyzje), bez ręcznego sklejania `ChangeInfos` po kluczach.
- Informacja „kto założył / kto ostatnio zmienił" jest też dostępna bez rejestru zmian jako
  `matter.FirstChangeInfo` / `matter.LastChangeInfo` (`Soneta.Business.Db.ChangeInfo`).
