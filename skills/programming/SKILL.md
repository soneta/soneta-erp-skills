---
name: programming
description: >
  Klasy ORM i wzorce kodu biznesowego platformy Soneta (enova365, Soneta Enterprise, Triva) — dla
  dodatków partnerów i dla kodu samej platformy pisanego przez zespół Soneta w repozytorium
  źródłowym (przestrzenie nazw `Soneta.*`, moduły standardowe). Używaj gdy użytkownik: (1) pisze,
  modyfikuje lub refaktoruje kod biznesowy (Row/Table/Module, sesje i transakcje, selector, typy
  wierszy) albo edytuje plik `.cs` z klasami `Row`/`Table`/`Module`, workerem, `[Action]`,
  `Session`/`Transaction`; (2) pyta o mechanizm ORM — Session, Commit/Save, optimistic lock,
  Context, RowCondition, Datapack, ViewInfo, Features, thread-safety; (3) prosi o code review kodu
  biznesowego Soneta (safe-code); (4) pisze worker, extender, akcję w menu Czynności, folder/listę;
  (5) inwentaryzuje moduły, pola, workery lub formularze/zakładki z bibliotek DLL; (6) zaczyna nowy
  dodatek — szkielet z CLI (`dotnet new soneta-addon`, Soneta.MsBuild.SDK). Sięgnij też, gdy inny
  skill potrzebuje warstwy ORM/kodu biznesowego Soneta.
---

# Soneta Programming Basics - Podstawowe klasy ORM

Skill zawiera dokumentację fundamentalnych klas logiki biznesowej platformy Soneta. Klasy te stanowią podstawę mapowania obiektowo-relacyjnego (ORM) i są niezbędne do tworzenia kodu i dodatków.

> **Code review / refaktoring:** po napisaniu nowego kodu biznesowego oraz po każdym refaktoringu, **zawsze** zweryfikuj go względem [references/safe-code.md](references/safe-code.md). Ten sam dokument służy jako lista kontrolna do review PR-ów.

## Mapa skilla

SKILL.md zawiera "duży obraz" - hierarchię klas, thread-safety, kanoniczne wzorce. Po szczegóły konkretnego tematu sięgaj do referencji:

| Temat                                                                                             | Gdzie szukać |
|---------------------------------------------------------------------------------------------------|---|
| **Nowy dodatek od zera** — wygenerowanie szkieletu źródeł przez CLI (`dotnet new soneta-addon`, Soneta.MsBuild.SDK, `global.json`/`Directory.Build.props`, solucja, debug VS Code) | [references/new-addon-cli.md](references/new-addon-cli.md) |
| Hierarchia ORM, Row / Table / Module, klucze, ISessionable                                        | sekcje poniżej |
| Implementacja klas Row/Table, konstruktory (pola readonly, `RowCreator`), selector + `[BusinessRow]`, `[NewRow]`, `[DefaultConstructor]`, jawne wartości enum'ów, **zdarzenia cyklu życia** (`OnAdded`/`OnLoaded`/`OnEditing`/`OnDeleting`/`OnDeleted`/`OnRepacked` — override z `base.OnXxx()`; alternatywa bez dziedziczenia: statyczne delegaty `XSchema`) | [references/row-types.md](references/row-types.md) |
| `AssemblyAttributes` - odczyt atrybutów z załadowanych modułów (`GetCustom<T>`, `Find`, iteracja po assembly, cache, analiza DLL w runtime) | [references/assembly-attributes.md](references/assembly-attributes.md) |
| Sesje, transakcje, Login, Database, BusApplication, optimistic locking                            | [references/session-login.md](references/session-login.md) |
| Paczki danych, Datapack, GuidedRow, ExportedRow, synchronizacja, blokady                          | [references/datapack-guidedrow.md](references/datapack-guidedrow.md) |
| Klasa Context - dane z UI, zaznaczenia, parametry workera                                         | [references/context.md](references/context.md) |
| Klasy parametrów (ContextBase) - filtry, trwałość, InvokeChanged (ta sama konstrukcja służy jako **parametry wydruków** → [repx](../repx/SKILL.md)) | [references/contextbase.md](references/contextbase.md) |
| Obiekty Worker i Extender - rozszerzenia modelu, akcje w menu Czynności (`[Action]` i dynamiczne `GetActions`), **rozszerzanie obiektów obcych** (property/kolekcje dodatku na obcym typie Soneta bez modyfikacji jego kodu) | [references/worker-extender.md](references/worker-extender.md) |
| Serwisy biznesowe (App / Database / Login / Session scope), **rejestracja warunkowa ServiceInitializer** (per instancja właściciela, np. per silnik bazy; kolejność po Priority) | [references/services.md](references/services.md) |
| Tłumaczenia (Translate, TranslateIgnore), ILogger, ActSource, **lokalizacja plików logów** (katalog `Soneta/Logs/` — macOS: `~/Library/Application Support/`, Windows: `%APPDATA%\`; pliki `server-*`/`dbmgr-*`/`test-*`) | [references/translations-logging.md](references/translations-logging.md) |
| Action result (rezultaty workera) zwracany przez worker / extender / Command - raporty, dialogi, nawigacja | [references/action-result.md](references/action-result.md) |
| RowCondition - serwerowe warunki LINQ, filtrowanie SubTable / View / Query                        | [references/rowcondition.md](references/rowcondition.md) |
| ViewInfo - definicja widoków list (folderów i inline jako property), CreateView, args.DataSource, klasa Params, `[Accessor(AutoChange)]`, powiązanie z viewform.xml | [references/viewinfo.md](references/viewinfo.md) |
| ChangeInfos - dziennik zmian / audyt (`session.ChangeInfos.Add`, pola Info/Data, pułapka 255 znaków, ChangeInfoType, prezentacja listy) | [references/changeinfos.md](references/changeinfos.md) |
| Cechy (Features) - tabela Features, typy cech, dostęp typowany/nietypowany, bindowanie w form.xml | [references/features.md](references/features.md) |
| Weryfikatory - walidacja spójności danych (`Verifier`, `RowVerifier<T>`, `ColVerifier<T>`, `MultiColVerifier<T>`, `RequiredVerifier`), poziomy `Error`/`Warning`/`Information`, uzbrajanie na zmianę pól-źródeł (`Session.Verifiers.Add`, `<verifier>` w business.xml), blokada `Save()` przy błędzie | [references/verifiers.md](references/verifiers.md) |
| Eventy (zdarzenia sesji) - odraczanie ciężkich obliczeń i logika w transakcji serwerowej: **sesyjne** (`Session.Events`, odpalane na `CommitUI`/`Save`/`Invoke`) vs **serwerowe** (`Session.ServerEvents`, w transakcji `Save` — numeracja). Rejestracja `Add(handler[, args])`, dedup po `Equals`/`GetHashCode`, wymuszanie `Invoke(handler)`, argumenty (`BusEventArgs`, `RowEventArgs<TRow>`, `SessionBusEventArgs`), priorytety, transakcyjność | [references/events.md](references/events.md) |
| Źródła praw (`IRightsSource`) - obiekt sterujący dostępem do danych operacyjnych, `AccessRight`, `Login.GetObjectRight`; nowe źródło domyślnie Denied, `AccessDeniedException` przy odczycie kolumn bez prawa, nadawanie praw i cache ról. **Miejsce tabeli w drzewie uprawnień** (pliki `*.rightstree.xml`, reguła korzenia praw, relacje `relright`/`relguided`) → skill [business-xml](../business-xml/SKILL.md) | [references/rights-source.md](references/rights-source.md) |
| Notacja klamrowa (`AccessorFormatter`) - wstawki `{ścieżka}` / `{ścieżka:format}` w captionach, szablonach, promptach; metadane modeli (`ApplicationInfo`, `TableInfo`) | [references/metadata-formatting.md](references/metadata-formatting.md) |
| Gotowe wzorce kodu end-to-end (import, CRUD, obsługa błędów)                                      | [references/examples.md](references/examples.md) |
| Receptury kodu per obiekt biznesowy (domena CRM) — `Kontrahent` (pola, kolekcje, workery, finanse, RODO, KSeF). Indeks + mapa receptur (CRM-W1–W18); rozdziały `references/domeny/crm/CRM01..CRM10` | [references/domeny/crm.md](references/domeny/crm.md) |
| Receptury kodu per obiekt biznesowy (domena Handel) — `DokumentHandlowy` (faktury/magazynowe/zamówienia/korekty, relacje `IRelacjeService`, cykl życia, magazyn/partie/obroty, VAT/waluty, płatności, KSeF/fiskal/Intrastat, wydruki). Indeks + mapa receptur (HANDEL-W1–W82); rozdziały `references/domeny/handel/HANDEL01..HANDEL14` | [references/domeny/handel.md](references/domeny/handel.md) |
| Receptury kodu per obiekt biznesowy (domena Kadry-Płace) — `Pracownik` (zatrudnienie i dane kadrowe, historia `PracHistoria`+`Etat`, dodatki, nieobecności/limity, plan pracy/RCP, umowy cywilnoprawne, naliczanie wypłat, listy płac, wydruki PDF). Indeks + mapa receptur (KADRY-A*…K*); rozdziały `references/domeny/kadry/KADRY01..KADRY11` | [references/domeny/kadry.md](references/domeny/kadry.md) |
| **Zasady bezpiecznego kodu biznesowego — checklist do review i refaktoringu**                     | [references/safe-code.md](references/safe-code.md) |
| Pola obiektów biznesowych — dane wygenerowane (`data/props/`) + skaner/regeneracja z DLL      | [references/scan-props.md](references/scan-props.md) |
| Moduły i tabele (`*Module` / `*Row` / `*Table`) — gotowy przegląd w `data/props/` (`INDEX.md` → `<Moduł>/INDEX.md`) + skaner z DLL | [references/scan-modules.md](references/scan-modules.md) |
| Inwentaryzacja workerów i extenderów (`[Worker<…>]`) z DLL                            | [references/scan-workers.md](references/scan-workers.md) |
| Zakładki, sekcje danych i pola formularzy (zasoby `*.pageform.xml`/`*.form.xml`) z DLL — katalog `data/forms/` (`INDEX.md` → `<Przestrzeń>.md`) + skaner na żądanie (`DataContext`/`EditValue`, `Include`, listy, kolejność pól pod kod i import XML) | [references/scan-forms.md](references/scan-forms.md) |
| Inwentaryzacja folderów statycznych menu (`[assembly: FolderView]`) z DLL — drzewo, listy, formularze | narzędzie `scan-folders` w skillu [config](../config/SKILL.md) |
| Wydruki DevExpress (pliki `.repx`) — struktura raportu, źródło danych `BusinessDataSource`, rejestracja `[assembly: DxReport(...)]`; **kod-behind wydruku** (`ReportSnippet`/`[DxBind]`, lub generyczny `Snippet`/`[Bind]` bez licencji DevExpress) liczący dane raportu w oparciu o ORM. **Parametry wydruku konstruuje się jak parametry workerów — klasa `ContextBase` + `[Context]`** (patrz wyżej) | skill [repx](../repx/SKILL.md) (warstwa `.repx`) |
| **Testowanie na żywej aplikacji przez `buscall call` (CLI)** — zdalne sterowanie programem (nawigacja, formularze, gridy, edycja) i **zrzuty ekranu** do analizy wizualnej; jednorazowe wywołania CLI bez zarządzania procesem (`buscall --db <Baza> call <metoda> klucz=wartość`), plus wariant MCP `callmcp` | [references/buscall-live-testing.md](references/buscall-live-testing.md) (weryfikacja na żywo) |
| **Import/eksport XML z kodu — `SessionReader`/`SessionWriter`** — konstruktory (`Session` vs `Login`), obsługa błędów (`CollectExceptions`, zdarzenie `ReaderException`/`ReaderResponse`), mapowanie GUID-ów (`AddGuidMap`, `GuidMapPolicy`), tryby kolekcji (`RelationsImportMode`), eksport datapacku z subexports, `ImportBusinessXml` w testach; struktura samego pliku XML → artykuł [import-export-xml](../config/references/import-export-xml.md) | [references/sessionreader-sessionwriter.md](references/sessionreader-sessionwriter.md) |
| **Testy integracyjne — klasa bazowa `TestBase`** — testy na realnej bazie z automatycznym rollbackiem (dwupoziomowa transakcja), wybór bazy `[TestDatabase]` (`nunit_default`/`nunit_ui`/`nunit_premiumui`), cykl życia (`ClassSetup`/`TestSetup`), `Session`/`ConfigSession`/`Context`, `InTransaction`/`SaveDispose`, podmiana DI (`ConfigureLoginServices`), asercje `AwesomeAssertions`, testy SQL (`SqlTraceInfo`), konwencje: nazewnictwo `Should_..._When_...` + prefiks grupy, `[Description]`, struktura AAA | [references/integration-tests.md](references/integration-tests.md) |

## Nowy dodatek od zera (CLI)

Gdy zaczynasz **nowy** dodatek (a nie modyfikujesz istniejący), najpierw wygeneruj szkielet źródeł
z wiersza poleceń, a dopiero potem wypełniaj go treścią. Robi to paczka szablonów **Soneta Platform
Developer** w oparciu o **Soneta.MsBuild.SDK** — jednym poleceniem powstają rozdzielone projekty
(logika biznesowa, UI, testy) już skonfigurowane pod platformę Soneta:

```bash
dotnet new install Soneta.Platform.Developer          # raz na maszynę (starsza składnia: dotnet new -i …)
dotnet new soneta-addon -n MyExtension -o ./MyExtension
# >>> obowiązkowy krok konfiguracji (patrz niżej) — nadpisz 3 pliki poprawną treścią <<<
cd ./MyExtension && dotnet build                       # weryfikacja
```

Powstaje gotowa solucja `MyExtension.sln` z projektami `MyExtension` (logika), `MyExtension.UI`,
`MyExtension.Tests` oraz pliki `global.json` (wersja SDK) i `Directory.Build.props` (wersja bibliotek
`SonetaPackageVersion`). SDK sam pobiera biblioteki Soneta i uruchamia generator
`*.business.xml → *.business.cs` przy buildzie. Dodatki celują w **`net10.0`** i są **cross-platform**
— pełny cykl (generowanie, build, testy) działa na macOS/Linux/Windows.

**Obowiązkowy krok po wygenerowaniu — nadpisz pliki konfiguracyjne poprawną treścią.** Szablon
`1.0.7` generuje konfigurację, która **nie buduje się z pudełka**, więc zaraz po `dotnet new` (a przed
`dotnet build`) doprowadź trzy pliki do poprawnej postaci — nie czekaj, aż build padnie:

1. **`global.json`** — przypnij `"Soneta.Sdk": "1.2.0"` (szablon wpisuje starszą, np. `1.1.8`).
2. **`Directory.Build.props`** — dodaj w pierwszym `PropertyGroup`
   `<SonetaTargetFramework>net10.0</SonetaTargetFramework>` (szablon zostawia tę zmienną pustą).
3. **`NuGet.Config`** — utwórz w katalogu solucji z feedem Soneta
   (`https://nuget.soneta.pl/v3/index.json`); szablon **nie generuje** tego pliku.

Gotowa treść wszystkich trzech plików: [references/new-addon-cli.md](references/new-addon-cli.md#5a-pliki-konfiguracyjne--poprawna-treść-krok-obowiązkowy).
Dopiero po tym uruchom `dotnet build` (powinien przejść z 0 ostrzeżeń, 0 błędów).

**`dotnet build` = walidator obu warstw** (generacja `business.xml → business.cs` **i** C#) — jednym
poleceniem wychodzą błędy generatora i braki klas konkretnych; to najszybsza pętla iteracji.
W raportach rozróżniaj „zweryfikowane buildem" vs „wymaga weryfikacji runtime" (żywa aplikacja /
testy) — zielony build ≠ działające runtime.

**Kryterium ukończenia:** w katalogu docelowym istnieją trzy `*.csproj`, `global.json`
i `Directory.Build.props`. Dalej wypełniasz szkielet: definicje (**[business-xml](../business-xml/SKILL.md)**) → kod
biznesowy (ten skill) → UI (**[form-xml](../form-xml/SKILL.md)**).

Pełna procedura — szablony, pliki konfiguracyjne, parametry MSBuild, solucja, debug w VS Code,
referencje WinForms, ręczny fallback bez szablonów i troubleshooting:
[references/new-addon-cli.md](references/new-addon-cli.md).

## Architektura warstw

```
BusApplication.Instance (singleton) - multithreaded
  └── Database
       └── Login
            └── Session - single-threaded
                 └── Module
                      └── Table
                           └── Row
```

## 3 poziomy logiki biznesowej

| Poziom | Opis | Przykłady klas |
|--------|------|----------------|
| **1. Bazowe** | Klasy wspólne dla wszystkich modułów (Soneta.Business.dll) | `Row`, `Table`, `Module`, `Session`, `Context` |
| **2. Generowane** | Klasy generowane przez BusinessGenerator z `*.business.xml` (sufiksy: Row, Table, Module) | `TowarRow`, `TowarTable`, `TowaryModule` |
| **3. Implementowane** | Klasy konkretne tworzone przez programistę | `Towar`, `Towary` (bez sufiksów) |

**BusinessGenerator** jest automatycznie uruchamiany podczas kompilacji dla plików `*.business.xml`. Szczegółowy opis definiowania business.xml znajduje się w skill **[business-xml](../business-xml/SKILL.md)**.

Klasy poziomu 3 (`Towar`, `Towary`) pisze programista, dziedzicząc po klasach generowanych.
Gdy tabela ma pola `readonly` (w tym **selector**), klasa obiektu biznesowego wymaga
dodatkowych konstruktorów; selector pozwala też przechowywać **wiele typów obiektów w jednej
tabeli**. Wzorce implementacji (konstruktory, `[BusinessRow]`, `[NewRow]`) —
[references/row-types.md](references/row-types.md).

## Hierarchia głównych klas

```
Row (abstrakcyjna)
 └── GuidedRow (+ Guid, Attachments, ChangeInfos)
      └── ExportedRow (+ Exported flag - rozróżnia dane konfiguracyjne od operacyjnych)

Table (abstrakcyjna)
 └── GuidedTable (indeksator po Guid)
      └── ExportedTable

Module (abstrakcyjna)
 └── [NazwaModulu]Module (np. TowaryModule)
```

Szczegóły GuidedRow / ExportedRow (flaga `Exported`, atrybuty `guided` / `relguided` w business.xml, ChangeInfos, blokady) - patrz [references/datapack-guidedrow.md](references/datapack-guidedrow.md).

## Thread-safety

### Obiekty single-threaded (NIE współdzielić między wątkami)
- `Session`
- `Module`
- `Table`
- `Row`
- `Context`
- oraz wszystkie klasy pochodne

### Obiekty multi-threaded (można współdzielić)
- `BusApplication`
- `Database`
- `Login`

## Klasa Session - fundamenty

Session to kluczowa klasa do zarządzania danymi. **Każda operacja na danych wymaga sesji.** Sesja jest single-threaded i implementuje `IDisposable` - zawsze opakowuj w `using`.

```csharp
Session session = login.CreateSession(readOnly: false, config: false, name: "MojaSesja");
```

**Każda modyfikacja obiektu MUSI być w transakcji biznesowej** otwieranej przez `Session.Logout(editMode: true)` - dotyczy dodawania, modyfikacji właściwości oraz kasowania:

```csharp
using (var transaction = session.Logout(editMode: true))
{
    towar.Nazwa = "Zmieniona nazwa";
    transaction.Commit();   // Commit() w kodzie biznesowym
    // transaction.CommitUI();  // CommitUI() w kodzie UI (worker, extender, Command)
}
session.Save();   // zapis do bazy - optimistic-lock; konflikty wykrywane tu
```

**Brak Commit() = automatyczny rollback przy Dispose()** - dotyczy też transakcji tylko do odczytu.

Pełna dokumentacja (typy sesji edycyjna / readonly / konfiguracyjna, transakcje zagnieżdżone, Commit vs CommitUI, optimistic locking, mieszanie obiektów z różnych sesji przez `session.Get(obiekt)`) - patrz [references/session-login.md](references/session-login.md).

## Klasa Module

Moduł grupuje logicznie powiązane tabele. **Nie ma odwzorowania w bazie danych.**

**Preferowany odczyt modułu to metoda rozszerzająca `session.GetX()` (generowanymi z `business.xml`, także dla modułów dodatków). ** Wariant
`Module.GetInstance(ISessionable)` stosuj tylko, gdy nie masz dostępu do property `Session`.
Nie używaj ogólnego `session.Get<TModule>()` ani `session.Modules[...]` — zasada
[§14.4 w safe-code.md](references/safe-code.md).

```csharp
// Dostęp do modułu - extension method (preferowane)
var tm = session.GetTowary();
var hm = session.GetHandel();
var crm = session.GetCRM();
var bm = session.GetBusiness();

// Dostęp do tabel
Towary towary = tm.Towary;
Jednostki jednostki = tm.Jednostki;

// Moduł implementuje ISessionable
Session s = tm.Session;
```

## Klasa Table

Reprezentuje tabelę w bazie danych. Udostępnia dostęp do kolekcji wierszy.

```csharp
var towary = tm.Towary;

// Iteracja po kluczu podstawowym
foreach (Towar t in towary.WgKodu) { ... }

// Iteracja po innym kluczu
foreach (Towar t in towary.WgNazwy) { ... }

// Właściwości
Table.AccessRight      // Prawa dostępu
Table.Session          // Sesja (ISessionable)
Table.Module           // Moduł nadrzędny
Table.PrimaryKey       // Klucz podstawowy
```

## Klasa Row

Reprezentuje pojedynczy wiersz (rekord) z tabeli.

### Właściwości bazowe

```csharp
Row.ID        // int - PODSTAWOWY identyfikator obiektu w tabeli (autoincrement, Primary Key)
Row.State     // RowState - stan obiektu w sesji
Row.Table     // Table - tabela nadrzędna
Row.Module    // Module - moduł
Row.Session   // Session - sesja
```

**ID** jest automatycznie generowany przez bazę danych i stanowi klucz główny (Primary Key) tabeli.

### Stany obiektu (RowState)

| Stan | Opis |
|------|------|
| `Detached` | Nowy obiekt, nie przypisany do tabeli |
| `Unchanged` | Wczytany z bazy, bez zmian |
| `Modified` | Zmodyfikowany w sesji |
| `Added` | Nowy, dodany do tabeli, nie zapisany w bazie |
| `Deleted` | Skasowany, do usunięcia z bazy |

## Klucze i indeksy

Definiowane w *.business.xml, mapowane na indeksy w bazie danych.

```xml
<key name="WgKodu" keyunique="true" keyprimary="true">
  <keycol name="Kod"/>
</key>
```

| Atrybut | Znaczenie |
|---------|-----------|
| `keyprimary="true"` | Klucz podstawowy (domyślne sortowanie) |
| `keyunique="true"` | Wartości unikalne w tabeli |

**Uwaga:** `keyprimary` w business.xml to **nie to samo** co Primary Key w bazie (który jest zawsze na kolumnie ID).

## Interfejs ISessionable

```csharp
public interface ISessionable {
    Session Session { get; }
}
```

Implementują go: `Session`, `Module`, `Table`, `Row`, `Context`, `Key`.

Używany jako argument funkcji wymagających kontekstu sesji:

```csharp
// Metoda statyczna GetInstance - akceptuje ISessionable
public static TowaryModule GetInstance(ISessionable session)

// Extension method - wygodniejsza składnia (tylko dla Session)
public static TowaryModule GetTowary(this Session session)
```

**Zalecane użycie:**
```csharp
// Extension method (prostsze)
var tm = session.GetTowary();
var tm = context.Session.GetTowary();
var tm = towar.Session.GetTowary();

// GetInstance (gdy mamy ISessionable, ale property Session jest niedostępne)
var tm = TowaryModule.GetInstance(sessionable);
```

## Kod biznesowy vs UI

Kod biznesowy realizuje operacje logiki biznesowej (jak backend). Kod UI (frontend) jest odpowiedzialny za prezentację danych i interakcję z użytkownikiem. Kod biznesowy może być umieszczony w tej samej klasie z kodem UI.

Kod UI to np.:
- obiekty `View`, `ViewInfo`, extender
- metody sterujące `IsReadOnlyXxx`, `IsVisibleXxxx`, `GetListXxx`, `IsEnabledXxx`, `GetNameXxx`, `GetAppearanceXxx`

### Ważne zasady do stosowania w kodzie biznesowym

- Nie używaj żadnych obiektów kodu UI, w szczególności `View` - zamiast tego możesz użyć `SubTable[condition]`.
- Nie należy stosować warunków na prawa dostępu (np. `if (Table.AccessRight == AccessRights.Denied) {...}`).

Szczegóły konstruowania ViewInfo (atrybut `FolderView`, eventy `CreateView`/`InitContext`, klasa `Params`, powiązanie z `viewform.xml`) opisuje [references/viewinfo.md](references/viewinfo.md).

## Szybki start - wzorce kodu

Kanoniczny wzorzec transakcji biznesowej (`Logout → zmiana → Commit → Save`) pokazuje
sekcja [Klasa Session - fundamenty](#klasa-session---fundamenty) powyżej. Gotowe wzorce
end-to-end (odczyt, tworzenie, modyfikacja, kasowanie, obsługa błędów, pełny import) -
patrz [references/examples.md](references/examples.md).

Gotowe **receptury per obiekt biznesowy** (realne pola, kolekcje i workery) są w `references/domeny/`:
[crm.md](references/domeny/crm.md) (`Kontrahent`), [handel.md](references/domeny/handel.md)
(`DokumentHandlowy`), [kadry.md](references/domeny/kadry.md) (`Pracownik`),
[workflow.md](references/domeny/workflow.md) (procesy i zadania).

## Narzędzia pomocnicze

Skill udostępnia skrypty `dotnet script` (`scripts/`) do statycznej inwentaryzacji bibliotek Soneta — bez ładowania IL do CLR (metadane typów: Roslyn `MetadataReference.CreateFromFile`; zasoby osadzone: `System.Reflection.Metadata`):

- **Pola tabel — najpierw dane wygenerowane.** Kontrakty pól **wszystkich ~1200 tabel** są już wyeksportowane do [`data/props/`](data/props/) (plik `<Moduł>/<RowType>.md` na tabelę). Odczyt jest natychmiastowy — nie skanuj DLL, gdy tabela tam jest. Znasz `RowType`? Nie czytaj indeksów, tylko `ls data/props/*/DokumentHandlowy.md` (pełny zestaw poleceń niżej). Jak regenerować po zmianie wersji: [references/scan-props.md](references/scan-props.md).
- **Moduły i tabele — indeks dwupoziomowy.** [`data/props/INDEX.md`](data/props/INDEX.md) to **router**: statystyki, polecenia wyszukiwania i lista 37 modułów. Inwentaryzacja tabel modułu jest w `data/props/<Moduł>/INDEX.md` (`RowType | Tytuł | Tabela | Konfig | Guided | Historia | Interfaces | Selektor | Plik`; kolumna `Historia`: `historyczna → H` / `historia → P`). Podział jest celowy — monolityczny indeks (212 KB) przekraczał limit jednego odczytu.
- **Interfejsy — [`data/props/Interfaces.md`](data/props/Interfaces.md).** Wszystkie interfejsy z `[TableInfo(Interfaces=...)]` i tabele je implementujące (relacje interfejsowe w jednym miejscu, tabele linkowane do plików).
- `scan-modules.csx` — skaner **na żądanie** listujący moduły i tabele z DLL (dla innego katalogu DLL niż ten, z którego zbudowano `data/props/`). Szczegóły: [references/scan-modules.md](references/scan-modules.md).
- `export-props-all.csx` — **wsadowo** regeneruje cały `data/props/` z DLL (kompilacja budowana raz, ~1200 tabel w kilka sekund). Uruchamiaj po zmianie wersji platformy lub przebudowie dodatku. Szczegóły: [references/scan-props.md](references/scan-props.md).
- `scan-props.csx` — skaner **na żądanie** dla pojedynczej tabeli (fallback, gdy tabeli nie ma w `data/props/`). Wypisuje pola i właściwości kalkulowane klasy biznesowej, rekurencyjnie po polach typu subrow, oraz — dla tabel z **selektorem** — sekcję podtypów (`[BusinessRow]`/`[NewRow]`). Szczegóły: [references/scan-props.md](references/scan-props.md).
- `scan-workers.csx` — wypisuje na stdout **JSON** z workerami i extenderami zarejestrowanymi atrybutem assembly `[Worker<…>]`, pogrupowanymi wg `DataType`. Dla każdej klasy: parametry inicjowane z `Context` (ctor + `[Context]`, z rozwinięciem pod-property dla typów dziedziczących z `ContextBase`), property do bindowania, akcje menu Czynności. Opcjonalny drugi argument filtruje wynik do konkretnego typu danych (np. `DokumentHandlowy`) — w praktyce konieczny, bo pełne skanowanie zwraca tysiące rejestracji. Wynik łatwo przetwarzać `jq`. Szczegóły: [references/scan-workers.md](references/scan-workers.md).
- **Formularze — najpierw INDEX.** Katalog **wszystkich ~5700 zakładek** (pageform) jest wyeksportowany do [`data/forms/`](data/forms/): [`INDEX.md`](data/forms/INDEX.md) to **router** (sposoby wyszukiwania, lista 26 przestrzeni nazw, sekcja systemowa), a zakładki biznesowe leżą w `data/forms/<Przestrzeń>.md` (`Typ danych | Zakładka (plik) | Nazwa zakładki | Priority | Biblioteka | Przestrzeń`, sortowane po typie). **Nie musisz znać przestrzeni** — `rg '^\| Kontrahent \|' data/forms/*.md` przeszukuje wszystkie naraz. Typ = segment przed nazwą zakładki w `…<TYP>.<ZAKŁADKA>.pageform.xml` (albo `DataType`). Zakładki typów ogólnych (`Row`/`IRow`/…) wydzielone jako **systemowe** (osobna sekcja routera; `scan-forms` nie raportuje ich per obiekt). Odczyt natychmiastowy, bez DLL. Regeneracja: `export-forms-index.csx`. Szczegóły: [references/scan-forms.md](references/scan-forms.md).
- `scan-forms.csx` — skaner **na żądanie** dla pojedynczego obiektu: odczytuje z zasobów osadzonych (`*.pageform.xml`/`*.form.xml`) **zakładki, sekcje danych (grupy) i pola** w **kolejności wprowadzania**, rozwijając ścieżki pól przez łańcuch `DataContext`/`EditValue`, dołączane `Include` (także między bibliotekami) oraz listy (`Grid`/`Scheduler`/… → kolumny elementu kolekcji, separator `:` między wczytaniem listy a polami elementu). Argument = prefiks nazwy pliku lub `DataType`/`Namespace.Typ`. Do budowania danych kodem i importu XML `business="true"`. Szczegóły: [references/scan-forms.md](references/scan-forms.md).
- `export-forms-index.csx` — **wsadowo** generuje `data/forms/INDEX.md` i pliki przestrzeni z DLL (jeden przebieg, ~5700 zakładek w ~1 s). Uruchamiaj po zmianie wersji platformy lub przebudowie dodatku.

### Jak szukać w danych wygenerowanych

Dane w `data/` są tak zorganizowane, że **nazwa pliku niesie informację** — najczęściej
wystarczy `ls` albo jeden `rg`, bez czytania indeksów. Polecenia uruchamiaj z katalogu skilla:

```bash
# Pola tabeli, gdy znasz RowType — plik nazywa się <Moduł>/<RowType>.md
ls data/props/*/DokumentHandlowy.md          # → data/props/Handel/DokumentHandlowy.md
ls data/props/*/*Pracownik*.md               # gdy nie znasz dokładnej nazwy

# Tabela po nazwie w bazie (kolumna `Tabela`) lub po tytule
rg -l '`DokHandlowe`' data/props/*/INDEX.md

# Które tabele mają pole wskazujące na dany obiekt (relacje)
rg -l 'Soneta\.CRM\.Kontrahenci\.Kontrahent`' data/props/*/*.md

# Zakładki obiektu — bez znajomości przestrzeni nazw
rg '^\| Kontrahent \|' data/forms/*.md

# Które tabele implementują interfejs
rg '^\| `IDokument' data/props/Interfaces.md
```

**Nie czytaj `data/props/<Moduł>/INDEX.md` ani `data/forms/<Przestrzeń>.md` w całości**, gdy
szukasz jednego obiektu — to setki wierszy. Indeksy służą do przeglądu modułu, `rg`/`ls` do
trafienia w konkret.

Inwentaryzację **folderów statycznych menu** (`[assembly: FolderView]`) — drzewo pozycji menu, list i formularzy — realizuje narzędzie **`scan-folders`** przeniesione do skilla **[config](../config/SKILL.md)** (komplementarne do `scan-modules`: perspektywa funkcjonalno-użytkowa zamiast danych).

## Konwencje nazewnicze

| Element | Konwencja | Przykład |
|---------|-----------|----------|
| Klasa wiersza (Row) | PascalCase, l.poj. | `Towar`, `Kontrahent` |
| Klasa tabeli | PascalCase, l.mn. | `Towary`, `Kontrahenci` |
| Klasa modułu | Nazwa + Module | `TowaryModule` |
| Klucz | Wg + nazwa kolumny | `WgKodu`, `WgNazwy` |
| Namespace | Soneta.NazwaModułu | `Soneta.Towary` |

### Język identyfikatorów

| Typ | Język | Przykłady |
|-----|-------|-----------|
| Logika biznesowa | **polski** | `Towar`, `Kontrahent`, `DokumentHandlowy`, `Faktura` |
| Identyfikatory systemowe | **angielski** | `Session`, `Context`, `Row`, `Table`, `Module` |

**Można łączyć polski i angielski** w nazwach metod i klas:

```csharp
RetrieveTowary()
UpdateKontrahent()
GetDokumentyHandlowe()
CreateFaktura()
```
