---
name: soneta-programming-basics
description: >
  Fundamentalne klasy ORM platformy enova365/Soneta Enterprise. Obejmuje mapowanie 
  obiektowo-relacyjne (Row, Table, Module), zarządzanie sesją (Session), logowanie 
  (Login, Database, BusApplication), paczki danych (Datapack, GuidedRow) oraz 
  kontekst (Context). Używaj gdy użytkownik pyta o podstawowe klasy logiki biznesowej, 
  strukturę obiektów ORM, sesje i transakcje, hierarchię klas Row/Table/Module, 
  mechanizm Datapack i synchronizację danych, lub kontekst aplikacji enova365.
---

# Soneta Programming Basics - Podstawowe klasy ORM

Skill zawiera dokumentację fundamentalnych klas logiki biznesowej platformy enova365/Soneta Enterprise. Klasy te stanowią podstawę mapowania obiektowo-relacyjnego (ORM) i są niezbędne do tworzenia dodatków.

## Architektura warstw

```
┌─────────────────────────────────────────────────────┐
│              Interfejs graficzny (UI)               │
├─────────────────────────────────────────────────────┤
│                    Context                          │  ← Komunikacja UI ↔ logika
├─────────────────────────────────────────────────────┤
│              Logika biznesowa                       │
│  ┌─────────────────────────────────────────────┐   │
│  │ BusApplication (Singleton)                   │   │
│  │  └── Database (konfiguracja bazy)            │   │
│  │       └── Login (uwierzytelnienie)           │   │
│  │            └── Session (zarządzanie danymi)  │   │
│  │                 └── Module → Table → Row     │   │
│  └─────────────────────────────────────────────┘   │
├─────────────────────────────────────────────────────┤
│              Baza danych SQL                        │
└─────────────────────────────────────────────────────┘
```

## 3 poziomy logiki biznesowej

| Poziom | Opis | Przykłady klas |
|--------|------|----------------|
| **1. Bazowe** | Klasy wspólne dla wszystkich modułów (Soneta.Business.dll) | `Row`, `Table`, `Module`, `Session`, `Context` |
| **2. Generowane** | Klasy generowane przez BusinessGenerator z *.business.xml (sufiksy: Row, Table, Module) | `TowarRow`, `TowarTable`, `TowaryModule` |
| **3. Implementowane** | Klasy konkretne tworzone przez programistę | `Towar`, `Towary` (bez sufiksów) |

**BusinessGenerator** jest automatycznie uruchamiany podczas kompilacji dla plików `*.business.xml`. Szczegółowy opis definiowania business.xml znajduje się w skill **soneta-business-xml**.

## Hierarchia głównych klas

```
Row (abstrakcyjna)
 └── GuidedRow (+ Guid, Attachments, ChangeInfos)
      └── ExportedRow (+ Exported flag)

Table (abstrakcyjna)
 └── GuidedTable (indeksator po Guid)
      └── ExportedTable

Module (abstrakcyjna)
 └── [NazwaModulu]Module (np. TowaryModule)
```

## Thread-safety

### Obiekty single-threaded (NIE współdzielić między wątkami)
- `Session`
- `Module`
- `Table`
- `Row`
- `Context`
- oraz wszystkie klasy pochodne

Każdy wątek powinien tworzyć własną sesję.

### Obiekty multi-threaded (można współdzielić)
- `BusApplication`
- `Database`
- `Login`

## Klasa Session - fundamenty

Session to kluczowa klasa do zarządzania danymi. **Każda operacja na danych wymaga sesji.**

### Tworzenie sesji

```csharp
// Przez Login
Session session = login.CreateSession(readOnly: false, config: false, name: "MojaSesja");

// Parametry konstruktora:
// readOnly: true = tylko odczyt, false = edycja
// config: true = dane konfiguracyjne (cache), false = dane operacyjne (aktualne)
```

### Typy sesji

| Typ | ReadOnly | Config | Użycie |
|-----|----------|--------|--------|
| Edycyjna operacyjna | false | false | Modyfikacja dokumentów, kartotek |
| ReadOnly operacyjna | true | false | Odczyt danych transakcyjnych |
| Edycyjna konfiguracyjna | false | true | Modyfikacja ustawień |
| ReadOnly konfiguracyjna | true | true | Odczyt konfiguracji |

**WAŻNE:** W sesji operacyjnej nie można modyfikować obiektów konfiguracyjnych - wymagana jest sesja konfiguracyjna (`config: true`).

### Transakcje biznesowe (WAŻNE!)

**Każda zmiana obiektu MUSI być w transakcji biznesowej** otwieranej przez `Session.Logout(editMode: true)`:
- Dodawanie nowych obiektów
- Modyfikacja właściwości (properties)
- Kasowanie obiektów

```csharp
// Logout(editMode: true) - transakcja edycyjna (można modyfikować)
using (var transaction = session.Logout(editMode: true))
{
    towar.Nazwa = "Zmieniona nazwa";
    transaction.Commit();  // lub CommitUI()
}

// Logout(editMode: false) - transakcja tylko do odczytu
// (modyfikacje możliwe tylko w zagnieżdżonej transakcji edycyjnej)
using (var readTransaction = session.Logout(editMode: false))
{
    // odczyt danych...
    
    // zagnieżdżona transakcja edycyjna
    using (var editTransaction = session.Logout(editMode: true))
    {
        var cena = towar.Ceny["Hurtowa"];
        cena.Netto = new DoubleCy(100m);
        editTransaction.Commit();
    }
    
    readTransaction.Commit();  // WYMAGANE! Inaczej zmiany z zagnieżdżonych transakcji przepadną
}
```

**Brak Commit() = automatyczny rollback przy Dispose()** (dotyczy też transakcji tylko do odczytu!)

### Optimistic locking

Zmiany wykonywane są w trybie **optimistic-lock**:
- Zmiany kumulują się w sesji
- `Session.Save()` zapisuje wszystkie zmiany razem
- Konflikty wykrywane w momencie zapisu

### Ważne zasady

- Session implementuje `IDisposable` - **zawsze wywołuj Dispose()** lub używaj `using`
- Wiele sesji może współistnieć jednocześnie
- Sesja konfiguracyjna używa cache'a (optymalizacja odczytów)
- Sesja operacyjna zawsze czyta z bazy (aktualność danych)
- **Nie mieszaj obiektów z różnych sesji** - użyj `session.Get(obiekt)` aby doczytać obiekt w bieżącej sesji

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

// GetInstance (gdy mamy ISessionable, np. Row lub Context)
var tm = TowaryModule.GetInstance(context);
var tm = TowaryModule.GetInstance(towar);  // towar implementuje ISessionable
```

## Szczegółowa dokumentacja

- **[references/session-login.md](references/session-login.md)** - BusApplication, Database, Login, Session
- **[references/datapack-guidedrow.md](references/datapack-guidedrow.md)** - Paczki danych, GuidedRow, ExportedRow, synchronizacja
- **[references/context.md](references/context.md)** - Klasa Context, komunikacja UI ↔ logika
- **[references/examples.md](references/examples.md)** - Przykłady kodu i wzorce użycia

## Szybki start - wzorce kodu

### Odczyt danych

```csharp
using (var session = login.CreateSession(true, false, "Odczyt"))
{
    var tm = session.GetTowary();  // Extension method
    foreach (Towar t in tm.Towary.WgKodu)
    {
        Console.WriteLine($"{t.Kod}: {t.Nazwa}");
    }
}
```

### Tworzenie nowego obiektu

```csharp
using (var session = login.CreateSession(false, false, "Dodawanie"))
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
using (var session = login.CreateSession(false, false, "Edycja"))
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
