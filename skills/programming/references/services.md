# Serwisy biznesowe

Serwisy pozwalają tworzyć obiekty (komponenty), których czas życia zależy od scope:

- **App** (BusApplication.Instance) - cały okres działania aplikacji
- **Database** - per baza danych
- **Login** - per zalogowany użytkownik
- **Session** (default - nie trzeba określać w deklaracji)

Umieszczając deklarację interfejsu serwisu w assembly wspólnym, można udostępniać serwisy między modułami, nawet gdy nie ma między nimi referencji.

## Zasady

- **Tylko serwisy scope Session są single-threaded**, pozostałe są multi-threaded.
- Serwis może być `IDisposable`.
- Dla serwisów App, Database, Login **nie przechowuj obiektów sesyjnych** (Session, Row, Module, Table, Context).
- `[RequireOwnService]` tylko dla serwisów, które nie mogą być nadpisywane.
- **Zasięg inny niż `Session` deklaruje także interfejs** — `[RequireServiceScope(ServiceScope.Login)]`.
  Bez tego start programu kończy się wyjątkiem „Zasięg serwisu … nie jest zgodny z wymaganym zasięgiem
  interfejsu”.
- Serwis `Login` jest wspólny dla wszystkich sesji operatora. Trzymaj w nim stan interfejsu, który
  ma przetrwać zapis (np. rozwinięcie sekcji panelu listy): lista w kliencie web po zapisie dostaje
  nową sesję, a serwisy `Session` powstają od nowa.

## Deklaracja

```csharp
[assembly: Service<MyNamespace.IRegistry, MyNamespace.Registry>(ServiceScope.Login)]

namespace MyNamespace;

[RequireServiceScope(ServiceScope.Login)]
public interface IRegistry
{
    void Method();
}

internal sealed class Registry : IRegistry
{
    public void Method() { }
}
```

## Odczyt serwisu

```csharp
IRegistry registerRequired = login.GetRequiredService<IRegistry>();
IRegistry? registerOptional = login.GetService<IRegistry>();

foreach (IRegistry registers in login.GetServices<IRegistry>())
{
    // wiele implementacji
}
```

Scope odpowiada obiektowi, z którego pobieramy serwis: `BusApplication.Instance.GetRequiredService<T>()`, `database.GetRequiredService<T>()`, `login.GetRequiredService<T>()`, `session.GetRequiredService<T>()`.

## Użycie w worker / extender

Obiekt jest tworzony przez `Context.CreateObject()`, więc zależności są wstrzykiwane automatycznie.

```csharp
// Rozwiązanie lepsze - constructor injection
class MyWorker1(IRegistry registry)
{
    // używaj registry
}

// Alternatywa - atrybut [Context] na property
class MyWorker2
{
    [Context]
    private IRegistry Registry { get; set; }
}
```

## Rejestracja warunkowa — ServiceInitializer

`[assembly: Service]` rejestruje implementację bezwarunkowo dla wszystkich instancji danego
scope. Gdy rejestracja ma zależeć od instancji właściciela (np. od typu silnika bazy danych),
zamiast atrybutu `Service` używa się **`ServiceInitializer`** — kod wykonywany przy budowaniu
kolekcji serwisów **każdej instancji** scope'a (każdej bazy, każdego loginu itd.):

```csharp
[assembly: ServiceInitializer(typeof(DbNotificationInitializer), ServiceScope.Database)]

// rejestracja tylko dla baz MS SQL; inny silnik rejestruje własną implementację
// analogicznym initializerem we własnym assembly - bez [assembly: Service]
class DbNotificationInitializer : IServiceInitializer {
    public void Initialize(ServiceInitializerArgs args) {
        if (args.Owner is MsSqlDatabase)
            args.Services.AddSingleton<IDbNotification, DbNotification>();
    }
}
```

- `args.Owner` — instancja właściciela scope'a (Database/Login/Session…); **internal** — dostępne
  w assembly platformy przez `InternalsVisibleTo` (np. drivery baz danych), nie w dodatkach.
- `args.Services` — standardowa `IServiceCollection` (`AddSingleton` itd.); platforma ma też
  internal `ReplaceService`/`RemoveService` (`ServiceCollectionExtensions`).
- **Kolejność**: `ServiceInitializerAttribute` dziedziczy z `PriorityAttribute` (default 100),
  initializery wykonują się rosnąco po `Priority`. Atrybuty `[assembly: Service]` rejestruje
  `CustomServicesInitializer` (priorytet 100) — initializer z `Priority = 200` wykona się po nim
  i może podmienić wcześniejszą rejestrację.
- Przy wielu rejestracjach tego samego interfejsu `GetRequiredService<T>` zwraca **ostatnią**,
  `GetServices<T>` — wszystkie.

Wzorzec silnikowy (rdzeń: `IDbNotification` — MS SQL `DbNotification`/SqlDependency,
PostgreSQL `PgDbNotification`/LISTEN+NOTIFY): każdy silnik rejestruje swoją implementację
własnym initializerem warunkowo po `args.Owner is XxxDatabase` — bez priorytetów i podmian,
bo dla jednej instancji bazy powstaje najwyżej jedna rejestracja.
