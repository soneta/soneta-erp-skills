---
name: soneta-addon-webapi-create
description: "Tworzenie nowych dodatków WebAPI (DynamicAPI) dla systemu enova365/Soneta. Używaj gdy użytkownik: (1) prosi o utworzenie nowego projektu dodatku z endpointami WebAPI/DynamicAPI; (2) pyta o strukturę projektu Soneta.Sdk (.csproj, Directory.Build.props, nuget.config); (3) chce dodać kontroler DynamicApiController lub interfejs z DynamicApiMethod; (4) pyta o wzorce dostępu do modułów enova365 (Session, WorkSession, tabele, rekordy)."
---

# Tworzenie dodatku WebAPI enova365

## Przepływ pracy

1. **Analiza wymagań** — zidentyfikować moduł enova365 (Handel, Kadry, CRM, Kasa itp.) do rozszerzenia.
2. **Inicjalizacja środowiska** — skopiować `references/nuget-config-template.xml` jako `nuget.config` w katalogu głównym projektu. Utworzyć `Directory.Build.props` na bazie `references/props-template.xml`.
3. **Utworzenie projektu** — utworzyć folder i plik `.csproj` na bazie `references/csproj-template.xml`. Dodać wymagane `PackageReference` dla modułów.
4. **Pierwszy build** — uruchomić `dotnet build` żeby pobrać NuGety i wygenerować DLL-ki.
5. **Inspekcja API** — użyć skilla **`soneta-dll-inspector`** żeby zbadać API modułu. Przykład: `ApiDumper bin/Debug/Soneta.Handel.dll --type DokumentHandlowy`.
6. **Implementacja** — utworzyć interfejs (`references/interface-template.cs`) i klasę API (`references/api-class-template.cs`), używając **pełnych nazw typów** w atrybutach assembly.
7. **Weryfikacja** — uruchomić `dotnet build`.

## Wzorce enova365

### Pobieranie modułów

```csharp
var hm = HandelModule.GetInstance(workSession);
// alternatywnie:
var hm = (HandelModule)workSession.Modules[typeof(HandelModule)];
```

### Wyszukiwanie w tabelach

Jeśli `WgKodu[]` nie działa, użyć rzutowania na `IEnumerable`:

```csharp
using System.Collections;
var magazyn = ((IEnumerable)hm.Magazyny).Cast<Soneta.Magazyny.Magazyn>()
    .FirstOrDefault(m => m.Symbol == "KOD");
```

### Atrybuty assembly (implementacja)

Używać **pełnych nazw typów** (z namespace) w atrybutach:

```csharp
[assembly: Service(typeof(Namespace.Interfaces.IYourApi), typeof(Namespace.YourApi), ServiceScope.Session)]
[assembly: DynamicApiController(typeof(Namespace.Interfaces.IYourApi), typeof(Namespace.YourApi))]
```

### Sesja i transakcja

```csharp
using var workSession = session.Login.CreateSession(readOnly: false, config: false);
using var transaction = workSession.Logout(true);

// Logika...
transaction.Commit();
```
