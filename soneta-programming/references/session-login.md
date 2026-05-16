# Session, Login, Database, BusApplication

Dokumentacja klas zarządzających połączeniem z bazą danych i sesjami w platformie enova365/Soneta.

## Hierarchia obiektów

```
BusApplication.Instance (Singleton)
    └── Database[] (kolekcja baz danych)
         └── Login (uwierzytelniony użytkownik)
              └── Session[] (sesje robocze)
                   └── Module → Table → Row
```

## Klasa BusApplication

Singleton reprezentujący instancję aplikacji ERP. Tworzony podczas inicjalizacji systemu.

### Dostęp do instancji

```csharp
// Singleton
BusApplication app = BusApplication.Instance;

// Dostęp do bazy danych po nazwie
Database db = BusApplication.Instance["BazaDemo"];

// Iteracja po wszystkich bazach
foreach (Database db in BusApplication.Instance)
{
    Console.WriteLine(db.Name);
}
```

### Właściwości

| Właściwość | Typ | Opis |
|------------|-----|------|
| `Instance` | `BusApplication` | Statyczny singleton |
| `Is365` | `bool` | `true` = wersja HTML, `false` = wersja okienkowa |
| `this[string]` | `Database` | Indeksator - baza po nazwie |

## Klasa Database

Abstrakcyjna klasa reprezentująca bazę danych. Konkretne implementacje dla wspieranych silników:

```
Database (abstrakcyjna)
 └── SqlDatabase (abstrakcyjna)
      ├── MsSqlDatabase (SQL Server)
      └── AzureDatabase (Azure SQL)
```

**Uwaga:** MySqlDatabase oraz OracleDatabase nie są już obsługiwane.

### Konfiguracja

Obiekty Database są deserializowane z pliku `Lista baz danych.xml`.

### Właściwości

| Właściwość | Typ | Opis |
|------------|-----|------|
| `Name` | `string` | Nazwa bazy danych |
| `DefaultDatabase` | `bool` | Czy baza domyślna |
| `DatabaseName` | `string` | Nazwa bazy w silniku SQL (SqlDatabase) |

### Logowanie do bazy

```csharp
Database db = BusApplication.Instance["BazaDemo"];

// ZALECANE: Logowanie z LoginParameters
Login login = db.Login(new LoginParameters
{
    UserName = "Administrator",
    UserPassword = "password"
});

// Logowanie zintegrowane Windows
Login login = db.Login(new LoginParameters
{
    UserName = LoginParameters.WindowsAuthenticationUser
});
```

## Klasa Login

Obiekt reprezentujący zalogowanego użytkownika. Zarządza sesjami. IDisposable.

### Tworzenie

```csharp
Database db = BusApplication.Instance["BazaDemo"];
Login login = db.Login(new LoginParameters
{
    UserName = "Administrator",
    UserPassword = "password"
});
```

### Dostęp do informacji o operatorze

**WAŻNE:** Właściwości `Operator` i `Entitle` w klasie `Login` nie należy stosować, ponieważ używają obiektu z `ConfigSession` w sposób niekontrolowany.

```csharp
// NIE ZALECANE:
// var op = login.Operator;  // unikać!
// var ent = login.Entitle;  // unikać!

// ZALECANE: Użyj w konkretnej sesji
using (var session = login.CreateSession(readOnly: true, config: false, name: "Odczyt"))
{
    var op = session.AuthorizationInfo.Operator;
    Console.WriteLine($"Zalogowany: {op.Name} - {op.FullName}");
}
```

### Właściwości informacyjne

| Właściwość | Typ | Opis |
|------------|-----|------|
| `IsWebUser` | `bool` | Czy operator pulpitu (web) |
| `Licencje` | `IEnumerable` | Pobrane licencje |
| `Database` | `Database` | Baza danych logowania |

### Tworzenie sesji

```csharp
// ZALECANA sygnatura (czytelność + debugowanie)
Session session = login.CreateSession(
    readOnly: false,    // true = tylko odczyt
    config: false,      // true = dane konfiguracyjne
    name: "MojaSesja"   // nazwa sesji - pomocna przy debugowaniu
);
```

**Zawsze używaj wersji z parametrem `name`** - ułatwia debugowanie i identyfikację sesji.

### Dostęp do danych konfiguracyjnych

```csharp
// ZALECANE: ExecuteConfig z lambdą (zwraca wartość prostą)
var opisDlaSzt = login.ExecuteConfig(configSession =>
    configSession.GetTowary().Jednostki.WgKodu["szt"]?.Opis);

// NIE ZALECANE: login.ConfigSession
// var configSession = login.ConfigSession;  // unikać!
```

**WAŻNE:** Używając `ExecuteConfig()` nie można zwracać obiektów sesyjnych z sesji konfiguracyjnej, ponieważ może być używana w innym wątku do innych celów. Zwracaj tylko wartości proste lub kopie danych.

### Sygnatury CreateSession

```csharp
public Session CreateSession(bool readOnly, bool config, string name) // rekomendowana
public Session CreateSession(bool readOnly, bool config)
public Session CreateSession()  // readOnly=false, config=false
```

## Klasa Session

Fundamentalna klasa do zarządzania danymi. **Każda operacja na danych wymaga sesji.** IDisposable.

### Tworzenie

```csharp
// ZAWSZE używaj using lub wywołuj Dispose()
using (var session = login.CreateSession(readOnly: false, config: false, name: "MojaSesja"))
{
    // operacje na danych
    session.Save();
}
```

### Typy sesji - macierz

| ReadOnly | Config | Zastosowanie |
|----------|--------|--------------|
| `false` | `false` | **Edycja operacyjna** - dokumenty, kartoteki |
| `true` | `false` | **Odczyt operacyjny** - raporty, wyświetlanie |
| `false` | `true` | **Edycja konfiguracyjna** - ustawienia systemu |
| `true` | `true` | **Odczyt konfiguracyjny** - odczyt słowników |

**WAŻNE:** W sesji operacyjnej (`config: false`) nie można modyfikować obiektów konfiguracyjnych. Do modyfikacji konfiguracji wymagana jest sesja konfiguracyjna (`config: true`).

### Właściwości

| Właściwość | Typ | Opis |
|------------|-----|------|
| `ReadOnly` | `bool` | Czy sesja tylko do odczytu |
| `IsConfig` | `bool` | Czy sesja konfiguracyjna |
| `Name` | `string` | Nazwa sesji |
| `Login` | `Login` | Obiekt logowania |
| `Database` | `Database` | Baza danych |

### Metody zarządzania danymi

```csharp
// Zapisanie zmian do bazy
session.Save();
```

### Dane operacyjne vs konfiguracyjne

**Dane operacyjne (`config=false`):**
- Dokumenty, kartoteki, transakcje
- Zawsze odczytywane z bazy (aktualność)
- Modyfikacje zapisywane natychmiast przy `Save()`

**Dane konfiguracyjne (`config=true`):**
- Słowniki, definicje, ustawienia
- Buforowane w cache (optymalizacja)
- Modyfikowane rzadko, odczytywane często
- Określane atrybutem `config="true"` w business.xml

### Wiele sesji jednocześnie

```csharp
// Normalne zjawisko - wiele sesji może współistnieć
using (var session1 = login.CreateSession(readOnly: true, config: false, name: "Lista1"))
using (var session2 = login.CreateSession(readOnly: true, config: false, name: "Lista2"))
{
    // Obie sesje mogą odczytywać te same dane
    var tm1 = session1.GetTowary();
    var tm2 = session2.GetTowary();
}
```

### Transakcje biznesowe - Session.Logout()

**WAŻNE:** Każda zmiana obiektu biznesowego MUSI być w transakcji!

```csharp
using (var session = login.CreateSession(readOnly: false, config: false, name: "Edycja"))
{
    var tm = session.GetTowary();
    var towar = tm.Towary.WgKodu["KOD001"];
    
    // Logout(editMode: true) - transakcja EDYCYJNA (można modyfikować)
    using (var transaction = session.Logout(editMode: true))
    {
        towar.Nazwa = "Nowa nazwa";
        var cena = towar.Ceny["Hurtowa"];
        cena.Netto = new DoubleCy(100.00m);
        transaction.Commit();  // Zatwierdza zmiany w sesji
    }
    // Brak Commit() = rollback (przywrócenie stanu sprzed Logout)
    
    session.Save();  // Zapisuje do bazy danych
}
```

### Logout(editMode: false) - transakcja tylko do odczytu

```csharp
using (var session = login.CreateSession(readOnly: false, config: false, name: "Przeglad"))
{
    var tm = session.GetTowary();
    var towar = tm.Towary.WgKodu["KOD001"];
    
    // Transakcja tylko do odczytu
    using (var readTrans = session.Logout(editMode: false))
    {
        // Tutaj NIE można modyfikować
        
        // Ale można otworzyć zagnieżdżoną transakcję edycyjną
        using (var editTrans = session.Logout(editMode: true))
        {
            var cena = towar.Ceny["Hurtowa"];
            cena.Netto = new DoubleCy(200m);
            editTrans.Commit();
        }
        
        // WAŻNE: Commit wymagany też dla transakcji readonly!
        // Inaczej zmiany z zagnieżdżonych transakcji edycyjnych przepadną
        readTrans.Commit();
    }
    session.Save();
}
```

| Metoda | Opis |
|--------|------|
| `session.Logout(editMode: true)` | Transakcja edycyjna - można modyfikować |
| `session.Logout(editMode: false)` | Transakcja tylko do odczytu |
| `transaction.Commit()` | Zatwierdza zmiany (wymagane dla obu typów!) |
| `transaction.CommitUI()` | Zatwierdza + odświeża UI |
| `transaction.Dispose()` | Bez Commit = rollback (także zagnieżdżonych zmian) |

## Kompletny przykład

```csharp
// 1. Pobranie instancji aplikacji
BusApplication app = BusApplication.Instance;

// 2. Pobranie bazy danych
Database db = app["MojaBaza"];

// 3. Logowanie
Login login = db.Login(new LoginParameters
{
    UserName = "admin",
    UserPassword = "haslo"
});

// 4. Utworzenie sesji (zawsze z nazwą!)
using (var session = login.CreateSession(readOnly: false, config: false, name: "Import"))
{
    // 5. Pobranie modułu (extension method)
    var tm = session.GetTowary();
    
    // 6. Transakcja biznesowa - WYMAGANA dla zmian!
    using (var transaction = session.Logout(editMode: true))
    {
        var nowyTowar = new Towar();
        tm.Towary.AddRow(nowyTowar);
        nowyTowar.Kod = "IMPORT001";
        nowyTowar.Nazwa = "Zaimportowany towar";
        
        transaction.Commit();
    }
    
    // 7. Zapis do bazy
    session.Save();
}
// 8. Session automatycznie Dispose() przez using
```

## Thread-safety

### Obiekty multi-threaded (można współdzielić między wątkami)
- `BusApplication`
- `Database`
- `Login`

### Obiekty single-threaded (NIE współdzielić)
- `Session`, `Module`, `Table`, `Row`, `Context`

```csharp
// DOBRZE - Login można współdzielić, każdy wątek tworzy własną sesję
Login sharedLogin = db.Login(new LoginParameters { UserName = "admin", UserPassword = "haslo" });

Parallel.ForEach(items, item => {
    using (var session = sharedLogin.CreateSession(readOnly: false, config: false, name: "Watek"))
    {
        var tm = session.GetTowary();
        using (var transaction = session.Logout(editMode: true))
        {
            // operacje...
            transaction.Commit();
        }
        session.Save();
    }
});
```
