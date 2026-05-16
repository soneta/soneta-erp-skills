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

Skill zawiera dokumentację fundamentalnych klas logiki biznesowej platformy enova365/Soneta Enterprise. Klasy te 
stanowią podstawę mapowania obiektowo-relacyjnego (ORM) i są niezbędne do tworzenia kodu i dodatków.

## Architektura warstw

```
BusApplication.Instance (singleton) - multihreaded 
  └── Database
       └── Login
            └── Session - singlethreaded
                 └── Module
                      └── Table
                           └── Row
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
- ID identyfikuje obiekt w tabeli, może powtarzać się w różnych tabelach

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

Kod biznesowy realizuje operacje logiki biznesowej (jak backend).
Kod UI (fronend) jest odpowiedzialny za prezentację danych i interakcję z użytkownikiem.
Kod biznesowy może być umieszczony w tej samej klasie z kodem UI.
Kod UI to np:
- obiekty `View`, `ViewInfo`, extender
- metody sterujące `IsReadOnlyXxx`, `IsVisibleXxxx`, `GetListXxx`, `IsEnabledXxx`, `GetNameXxx`, `GetAppearanceXxx`

### Ważne zasady do stosowania w kodzie biznesowym

- Nie używaj żadnych obiektów kodu UI, w szczególności `View` - zamiast tego możesz użyć `SubTable[condition]`
- Nie należy stosować warunków na prawa dostępu (np `if (Table.AccessRight == AccessRights.Denied) {...}`)

## Serwisy

Pozwalają tworzyć obiekty (komponenty), których czas życia będzie zależał od scope:
- App (BusApplication.Instance)
- Database
- Login
- Session (default - nie trzeba określać w deklaracji)

Umieszczając deklarację interface serwisu w assembly wspólnym, pozwalają na udostępnianie serwisów między modułami, 
nawet gdy nie ma odpowiedniej referencji.

* **Tylko serwisy scope Session są single-threded, pozostałe są multi-threaded.**
* Serwis może być `IDisposable`.
* Dla serwisów App, Database, Login nie przechowuj obiektów sesyjnych.
* `[RequireOwnService]` tylko dla serwisów, które nie mogą być nadpisywane.

### Przykład deklaracji

```csharp
[assembly: Service<MyNamespace.IRegistry, MyNamespace.Registry>(ServiceScope.Login)]

namespace MyNamespace;

[RequireServiceScope(ServiceScope.Login)]
public interface IRegistry {
    void Method();
}

internal sealed class Registry : IRegistry {
    public void Method() {}
}
```

### Odczytanie serwisu w kodzie

```csharp
IRegistry registerRequired = login.GetRequiredService<IRegistry>();
IRegistry? registerOptional = login.GetService<IRegistry>();

foreach (IRegistry registers in login.GetServices<IRegistry>())
{
    
}
```

### Użycie serwisu w worker lub extender - obiekt tworzony przez Context.CreareObject()

```csharp
// Rozwiązanie lepsze
class MyWorker1(IRegistry registry) {
}

class MyWorker2 {
    [Context]
    private IRegistry Registry { get; set; }
}
```

## Metadane modułów, tabel, kluczy, pól, itp

Dostęp do metadanych obiektów biznesowych dostępny przez metody `static` klasy `ApplicationInfo`.
Odczytanie informacji o tabli `TableInfo info = ApplicationInfo.GetTableInfo(nazwaTabeli)`. Istnieje tylko jedna 
referencja obiektu TableInfo dla tabeli. Można używać `ReferenceEquals`, `Dictionary`, itp.
Odczytanie wszystkich tabel `ApplicationInfo.GetTablesInfo()`, a np tabel dla modułu `ApplicationInfo.GetModuleInfo
(moduleName).TableInfos`.

### Wykorzystuj `TableInfo` do weryfikacji tabeli

```csharp
Row row1 = ...;
Row row2 = ...;
if (row1.Table.TableInfo==row2.Table.TableInfo) {
    // Ta sama tabela, nawet gdy różne sesje
}
```

## Tłumaczenie i formatowanie napisów, tekstów i string

Biblioteka obsługuje słowniki tłumaczące napisy w aplikacji Soneta. 
* Tłumaczone napisy muszą używać metody typu string-extender `"napis dotłumaczenia".Translate()`.
* Tłumaczenie tekstów formatowanych przez `"napis {0} z wartością {1}".TranslateFormat(arg0, arg1)`.
* Gdy string ma być zignorowany przez tłumacza, MUSISZ zaznaczyć do metodą `"nie tłumaczymy".TranslateIgnore()`,inaczej błąd kompilacji. 
* Jeżeli w metodzie lub klasie jest więcej napisów do zignorowania użyj atrybutu `[TranslateIgnore]`.
* Parametr metody jest ignorowany przez tłumacza, użyj atrybutu `[TranslateIgnore]` na parametrze.

## Log zmian i obserwowalność

Używaj standardowy narzędzi do logowania `ILogger<T>`. Użyj `[TranslateIgnore]` w metodzie wywołującej log.
Używaj `logger.IsEnable(LogLevel)` kiedy parametry wymagają dodatkowych operacji.

### Użycie `ILogger` oraz exception log

```csharp
class Test 
{
    private readonly logger = BusApplication.Instance.GetRequiredService<ILogger<Test>>();
    
    public  decimal Kwota;
    
    [TranslateIgnore]
    public void Metoda() 
    { 
        try {
            logger.LogInformation("Wywołanie metody {nazwa}", nameof(Validate));
            if (kwota<0) 
                logger.LogWarning("Kwota {kwota} nie może być ujemna w metodzie '{metoda}'", Kwota, nameof(Validate));
        }
        catch (Exception ex) {
            
            // Sposób na wrzucenie exception do trace
            ex.Log<Test>();
            
            throw;
        }
    }
}
```

### Śledzenie czasu wykonania z obsługą exception

```csharp
class Test {
    private static readonly ActSource actSource = new(nameof(Test), ActSource.TraceLevel.Default);

    public void Action() {
        using var activity = actSource.Start();
        
        try {
            // Algorytm do śledzenia
        }
        catch (Exception ex) {
            activity.AddExceptionWithError(ex);
            throw;
        }
    }
}
```

## Szczegółowa dokumentacja

- **[references/session-login.md](references/session-login.md)** - BusApplication, Database, Login, Session
- **[references/datapack-guidedrow.md](references/datapack-guidedrow.md)** - Paczki danych, GuidedRow, ExportedRow, synchronizacja
- **[references/context.md](references/context.md)** - Klasa Context, komunikacja UI ↔ logika
- **[references/examples.md](references/examples.md)** - Przykłady kodu i wzorce użycia

## Szybki start - wzorce kodu

### Odczyt danych

```csharp
using (var session = login.CreateSession(readOnly: true, config: false, name: "Odczyt"))
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
