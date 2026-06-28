---
name: soneta-programming
description: >
  Klasy ORM i wzorce kodu biznesowego enova365 / Soneta Enterprise / Triva:
  Row/Table/Module, sesja i transakcje (Session, Commit/CommitUI, Save,
  optimistic lock), Login/Database/BusApplication, Datapack/GuidedRow/ExportedRow,
  serwerowy LINQ (RowCondition, SubTable[condition]), Context, Worker/Extender/[Action],
  ViewInfo/FolderView, Features, typy wierszy w jednej tabeli (selector, [BusinessRow],
  [NewRow], [DefaultConstructor], konstruktory Row, enum z jawnymi wartościami),
  Translate/ILogger oraz zasady bezpiecznego kodu
  (safe-code, code review). Używaj gdy użytkownik: (1) pisze, modyfikuje lub
  refaktoruje kod biznesowy enova365/Soneta/Triva; (2) pyta o Session, Row, Table,
  Module, Login, Database, Context, Datapack, Worker, Extender, ViewInfo,
  RowCondition; (3) wspomina sesje, transakcje, Commit, Save, optimistic lock,
  blokady wierszy; (4) prosi o code review kodu biznesowego Soneta; (5) pisze
  dodatek, worker, extender, akcję w menu Czynności, folder/listę; (6) pyta
  o thread-safety, ExecuteConfig, dane konfiguracyjne vs operacyjne; (7) implementuje
  klasy Row/Table, selector (selektor), podtypy [BusinessRow], pozycje menu [NewRow],
  konstruktory z polami readonly lub RowCreator.
---

# Soneta Programming Basics - Podstawowe klasy ORM

Skill zawiera dokumentację fundamentalnych klas logiki biznesowej platformy enova365/Soneta Enterprise. Klasy te stanowią podstawę mapowania obiektowo-relacyjnego (ORM) i są niezbędne do tworzenia kodu i dodatków.

> **Code review / refaktoring:** po napisaniu nowego kodu biznesowego oraz po każdym refaktoringu, **zawsze** zweryfikuj go względem [references/safe-code.md](references/safe-code.md). Ten sam dokument służy jako lista kontrolna do review PR-ów.

## Mapa skilla

SKILL.md zawiera "duży obraz" - hierarchię klas, thread-safety, kanoniczne wzorce. Po szczegóły konkretnego tematu sięgaj do referencji:

| Temat                                                                                             | Gdzie szukać |
|---------------------------------------------------------------------------------------------------|---|
| Hierarchia ORM, Row / Table / Module, klucze, ISessionable                                        | sekcje poniżej |
| Implementacja klas Row/Table, konstruktory (pola readonly, `RowCreator`), selector + `[BusinessRow]`, `[NewRow]`, `[DefaultConstructor]`, jawne wartości enum'ów | [references/row-types.md](references/row-types.md) |
| `AssemblyAttributes` - odczyt atrybutów z załadowanych modułów (`GetCustom<T>`, `Find`, iteracja po assembly, cache, analiza DLL w runtime) | [references/assembly-attributes.md](references/assembly-attributes.md) |
| Sesje, transakcje, Login, Database, BusApplication, optimistic locking                            | [references/session-login.md](references/session-login.md) |
| Paczki danych, Datapack, GuidedRow, ExportedRow, synchronizacja, blokady                          | [references/datapack-guidedrow.md](references/datapack-guidedrow.md) |
| Klasa Context - dane z UI, zaznaczenia, parametry workera                                         | [references/context.md](references/context.md) |
| Klasy parametrów (ContextBase) - filtry, trwałość, InvokeChanged                                  | [references/contextbase.md](references/contextbase.md) |
| Obiekty Worker i Extender - rozszerzenia modelu, akcje w menu Czynności                           | [references/worker-extender.md](references/worker-extender.md) |
| Serwisy biznesowe (App / Database / Login / Session scope)                                        | [references/services.md](references/services.md) |
| Tłumaczenia (Translate, TranslateIgnore), ILogger, ActSource                                      | [references/translations-logging.md](references/translations-logging.md) |
| Action result (rezultaty workera) zwracany przez worker / extender / Command - raporty, dialogi, nawigacja | [references/action-result.md](references/action-result.md) |
| RowCondition - serwerowe warunki LINQ, filtrowanie SubTable / View / Query                        | [references/rowcondition.md](references/rowcondition.md) |
| ViewInfo - definicja widoków list (folderów i inline jako property), CreateView, args.DataSource, klasa Params, `[Accessor(AutoChange)]`, powiązanie z viewform.xml | [references/viewinfo.md](references/viewinfo.md) |
| ChangeInfos - dziennik zmian / audyt (`session.ChangeInfos.Add`, pola Info/Data, pułapka 255 znaków, ChangeInfoType, prezentacja listy) | [references/changeinfos.md](references/changeinfos.md) |
| Cechy (Features) - tabela Features, typy cech, dostęp typowany/nietypowany, bindowanie w form.xml | [references/features.md](references/features.md) |
| Źródła praw (`IRightsSource`) - obiekt sterujący dostępem do danych operacyjnych, `AccessRight`, `Login.GetObjectRight` | sekcja poniżej |
| Notacja klamrowa (`AccessorFormatter`) - wstawki `{ścieżka}` / `{ścieżka:format}` w captionach, szablonach, promptach | sekcja poniżej |
| Gotowe wzorce kodu end-to-end (import, CRUD, obsługa błędów)                                      | [references/examples.md](references/examples.md) |
| Receptury kodu per obiekt biznesowy (domena CRM) — `Kontrahent` (pola, kolekcje, workery, finanse, RODO, KSeF). Indeks + mapa receptur (CRM-W1–W18); rozdziały `references/domeny/crm/CRM01..CRM10` | [references/domeny/crm.md](references/domeny/crm.md) |
| Receptury kodu per obiekt biznesowy (domena Handel) — `DokumentHandlowy` (faktury/magazynowe/zamówienia/korekty, relacje `IRelacjeService`, cykl życia, magazyn/partie/obroty, VAT/waluty, płatności, KSeF/fiskal/Intrastat, wydruki). Indeks + mapa receptur (HANDEL-W1–W82); rozdziały `references/domeny/handel/HANDEL01..HANDEL14` | [references/domeny/handel.md](references/domeny/handel.md) |
| Receptury kodu per obiekt biznesowy (domena Kadry-Płace) — `Pracownik` (zatrudnienie i dane kadrowe, historia `PracHistoria`+`Etat`, dodatki, nieobecności/limity, plan pracy/RCP, umowy cywilnoprawne, naliczanie wypłat, listy płac, wydruki PDF). Indeks + mapa receptur (KADRY-A*…K*); rozdziały `references/domeny/kadry/KADRY01..KADRY11` | [references/domeny/kadry.md](references/domeny/kadry.md) |
| **Zasady bezpiecznego kodu biznesowego — checklist do review i refaktoringu**                     | [references/safe-code.md](references/safe-code.md) |
| Skanowanie pól obiektu biznesowego z DLL (Roslyn MetadataReference)                   | [references/scan-props.md](references/scan-props.md) |
| Inwentaryzacja modułów i tabel (`*Module` / `*Row` / `*Table`) z DLL                  | [references/scan-modules.md](references/scan-modules.md) |
| Inwentaryzacja workerów i extenderów (`[Worker<…>]`) z DLL                            | [references/scan-workers.md](references/scan-workers.md) |

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

**BusinessGenerator** jest automatycznie uruchamiany podczas kompilacji dla plików `*.business.xml`. Szczegółowy opis definiowania business.xml znajduje się w skill **soneta-business-xml**.

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

```csharp
// Dostęp do modułu - extension method (zalecane)
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

## Źródła praw (`IRightsSource`)

Obiekt (zwykle **konfiguracyjny**, np. magazyn, rejestr, definicja) może być **źródłem praw**:
operatorowi/roli przypisuje się uprawnienia do tego obiektu, co steruje dostępem do **danych
operacyjnych referujących** do niego — a nie tylko do samego obiektu konfiguracyjnego. Przykład:
operator z prawem do danego magazynu widzi tylko dokumenty przypisane do tego magazynu, a do
dokumentów z innych magazynów dostępu nie ma.

- Włączenie: w `business.xml` dodaj do tabeli `<interface>IRightsSource</interface>`. Od tego momentu
  system sam dba o widoczność obiektów i propagację praw. (`IRightsSourceEx` dokłada pod-kategorię,
  `IsRightsSourceEnable()`, `IsRightsSourceVisible()`.)
- Odczyt uprawnień: `Row.AccessRight`, `Table.AccessRight`, `Login.GetObjectRight(source)` →
  `AccessRights` (`Granted`/`Denied`/…).
- **Listy/`View` automatycznie filtrują** dane po prawach — pokazują tylko rekordy ze źródeł, do
  których operator ma dostęp. Na formularzu definicji pojawia się zakładka z przypisaniami uprawnień.
- W **kodzie biznesowym nie sprawdzaj** `AccessRight` warunkami — system egzekwuje prawa sam
  (patrz [references/safe-code.md](references/safe-code.md) §7.2).

## Notacja klamrowa (`AccessorFormatter`)

Tekst z **wstawkami danych** (captiony, szablony, opisy, treści promptów) komponuje się notacją
klamrową obsługiwaną przez `AccessorFormatter`. Wartości pól pobierane są przez `Accessor` obiektu
kontekstu — bez ręcznego sklejania stringów.

- `{ścieżka}` — wartość accessora, np. `{Kontrahent.Nazwa}`,
- `{ścieżka:format}` — z formatem po dwukropku, np. `{Data:yyyy-MM-dd}`,
- `{{` / `}}` — literalna klamra (escape).

```csharp
var f = new AccessorFormatter { UseHtmlEncoding = false };
f.Compile("Faktura dla {Kontrahent.Nazwa} z dnia {Data:yyyy-MM-dd}", accessor);
string text = f.ToString();
```

Bez accessora można podać własne źródło wartości przez `GetValueHandler`. Obsługuje też pola
Html/Markdown (kodowanie wyniku). Tej samej notacji używaj zamiast definiowania osobnych
„parametrów" tekstu.

## Metadane modułów, tabel, kluczy, pól

Dostęp do metadanych obiektów biznesowych jest dostępny przez metody `static` klasy `ApplicationInfo`.

- Odczyt informacji o tabeli: `TableInfo info = ApplicationInfo.GetTableInfo(nazwaTabeli)`. Istnieje tylko jedna referencja obiektu `TableInfo` dla tabeli - można używać `ReferenceEquals`, `Dictionary`, itp.
- Wszystkie tabele: `ApplicationInfo.GetTablesInfo()`.
- Tabele dla modułu: `ApplicationInfo.GetModuleInfo(moduleName).TableInfos`.

### Wykorzystuj `TableInfo` do weryfikacji tabeli

```csharp
Row row1 = ...;
Row row2 = ...;
if (row1.Table.TableInfo == row2.Table.TableInfo) {
    // Ta sama tabela, nawet gdy różne sesje
}
```

## Szybki start - wzorce kodu

### Odczyt danych

```csharp
using (var session = login.CreateSession(readOnly: true, config: false, name: "Odczyt"))
{
    var tm = session.GetTowary();
    foreach (Towar t in tm.Towary.WgKodu)
    {
        Console.WriteLine($"{t.Kod}: {t.Nazwa}");
    }
}
```

### Tworzenie nowego obiektu

```csharp
using (var session = login.CreateSession(readOnly: false, config: false, name: "Dodawanie"))
{
    var tm = session.GetTowary();

    using (var transaction = session.Logout(editMode: true))
    {
        var towar = new Towar();
        tm.Towary.AddRow(towar);
        towar.Kod = "NOWY001";
        towar.Nazwa = "Nowy towar";
        transaction.Commit();
    }

    session.Save();
}
```

### Modyfikacja istniejącego obiektu

```csharp
using (var session = login.CreateSession(readOnly: false, config: false, name: "Edycja"))
{
    var tm = session.GetTowary();
    var towar = tm.Towary.WgKodu["STARY001"];
    if (towar != null)
    {
        using (var transaction = session.Logout(editMode: true))
        {
            towar.Nazwa = "Zmieniona nazwa";
            transaction.Commit();
        }
    }
    session.Save();
}
```

Więcej wzorców (kasowanie, obsługa błędów, pełny import end-to-end) - patrz [references/examples.md](references/examples.md).

## Narzędzia pomocnicze

Skill udostępnia trzy skrypty `dotnet script` (`scripts/`) do statycznej inwentaryzacji bibliotek Soneta — bez ładowania IL do CLR (Roslyn `MetadataReference.CreateFromFile`):

- `scan-modules.csx` — listuje moduły (`*Module`) i ich tabele (`*Row`/`*Table`) z Caption/Description. Dobre na start. Szczegóły, parametry i przykłady uruchomienia: [references/scan-modules.md](references/scan-modules.md).
- `scan-props.csx` — wypisuje pola i właściwości kalkulowane konkretnej klasy biznesowej, rekurencyjnie po polach typu subrow. Sięgnij po niego, gdy znasz już tabelę i potrzebujesz jej kontraktu. Szczegóły: [references/scan-props.md](references/scan-props.md).
- `scan-workers.csx` — wypisuje na stdout **JSON** z workerami i extenderami zarejestrowanymi atrybutem assembly `[Worker<…>]`, pogrupowanymi wg `DataType`. Dla każdej klasy: parametry inicjowane z `Context` (ctor + `[Context]`, z rozwinięciem pod-property dla typów dziedziczących z `ContextBase`), property do bindowania, akcje menu Czynności. Opcjonalny drugi argument filtruje wynik do konkretnego typu danych (np. `DokumentHandlowy`) — w praktyce konieczny, bo pełne skanowanie zwraca tysiące rejestracji. Wynik łatwo przetwarzać `jq`. Szczegóły: [references/scan-workers.md](references/scan-workers.md).

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
