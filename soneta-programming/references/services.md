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
