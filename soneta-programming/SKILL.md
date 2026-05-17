---
name: soneta-programming
description: >
  Fundamentalne klasy ORM platformy enova365/Soneta Enterprise. Obejmuje mapowanie 
  obiektowo-relacyjne (Row, Table, Module), zarządzanie sesją (Session), logowanie 
  (Login, Database, BusApplication), paczki danych (Datapack, GuidedRow) oraz 
  kontekst (Context). Używaj gdy użytkownik pyta o podstawowe klasy logiki biznesowej, 
  strukturę obiektów ORM, sesje i transakcje, hierarchię klas Row/Table/Module,
  mechanizm Datapack i synchronizację danych, lub kontekst aplikacji enova365.
---

# Soneta Programming Basics - Podstawowe klasy ORM

Skill zawiera dokumentację fundamentalnych klas logiki biznesowej platformy enova365/Soneta Enterprise. Klasy te stanowią podstawę mapowania obiektowo-relacyjnego (ORM) i są niezbędne do tworzenia kodu i dodatków.

## Mapa skilla

SKILL.md zawiera "duży obraz" - hierarchię klas, thread-safety, kanoniczne wzorce. Po szczegóły konkretnego tematu sięgaj do referencji:

| Temat | Gdzie szukać |
|---|---|
| Hierarchia ORM, Row / Table / Module, klucze, ISessionable | sekcje poniżej |
| Sesje, transakcje, Login, Database, BusApplication, optimistic locking | [references/session-login.md](references/session-login.md) |
| Paczki danych, Datapack, GuidedRow, ExportedRow, synchronizacja, blokady | [references/datapack-guidedrow.md](references/datapack-guidedrow.md) |
| Klasa Context - dane z UI, zaznaczenia, parametry workera | [references/context.md](references/context.md) |
| Klasy parametrów (ContextBase) - filtry, trwałość, InvokeChanged | [references/contextbase.md](references/contextbase.md) |
| Obiekty Worker i Extender - rozszerzenia modelu, akcje w menu Czynności | [references/worker-extender.md](references/worker-extender.md) |
| Serwisy biznesowe (App / Database / Login / Session scope) | [references/services.md](references/services.md) |
| Tłumaczenia (Translate, TranslateIgnore), ILogger, ActSource | [references/translations-logging.md](references/translations-logging.md) |
| Action result zwracany przez worker / extender / Command - raporty, dialogi, nawigacja | [references/action-result.md](references/action-result.md) |
| RowCondition - serwerowe warunki LINQ, filtrowanie SubTable / View / Query | [references/rowcondition.md](references/rowcondition.md) |
| Gotowe wzorce kodu end-to-end (import, CRUD, obsługa błędów) | [references/examples.md](references/examples.md) |

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
