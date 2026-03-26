# Refaktoryzacja soneta-addon-webapi-create Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rozbić rozdmuchany skill `soneta-addon-webapi-create` (72 pliki) na 2 lekkie skille: `soneta-dll-inspector` (narzędzie do inspekcji DLL) i odchudzony `soneta-addon-webapi-create` (tylko scaffolding WebAPI).

**Architecture:** Nowy skill `soneta-dll-inspector` zawiera rozbudowany ApiDumper z 3 trybami pracy (list-types, --type, --search) i SKILL.md. Stary skill traci metadata/, scripts/, .skill ZIP i zostaje z SKILL.md + 5 szablonów w references/.

**Tech Stack:** C# / .NET 10.0 (ApiDumper), Markdown (SKILL.md)

**Spec:** `docs/superpowers/specs/2026-03-26-refaktoryzacja-webapi-skill-design.md`

---

### Task 1: Utworzenie struktury soneta-dll-inspector

**Files:**
- Create: `soneta-dll-inspector/SKILL.md`
- Create: `soneta-dll-inspector/scripts/ApiDumper/ApiDumper.csproj`
- Create: `soneta-dll-inspector/.gitignore`

- [ ] **Step 1: Utworzyć katalog skilla i .gitignore**

```bash
mkdir -p soneta-dll-inspector/scripts/ApiDumper
```

Plik `soneta-dll-inspector/.gitignore`:
```
scripts/ApiDumper/bin/
scripts/ApiDumper/obj/
```

- [ ] **Step 2: Utworzyć ApiDumper.csproj**

Plik `soneta-dll-inspector/scripts/ApiDumper/ApiDumper.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
```

- [ ] **Step 3: Commit**

```bash
git add soneta-dll-inspector/.gitignore soneta-dll-inspector/scripts/ApiDumper/ApiDumper.csproj
git commit -m "feat: scaffold soneta-dll-inspector skill structure"
```

---

### Task 2: Implementacja ApiDumper z 3 trybami

**Files:**
- Create: `soneta-dll-inspector/scripts/ApiDumper/Program.cs`

- [ ] **Step 1: Napisać Program.cs z parserem argumentów**

Plik `soneta-dll-inspector/scripts/ApiDumper/Program.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ApiDumper;

class Program
{
    // Metody odziedziczone z System.Object — pomijane w outputcie
    static readonly HashSet<string> IgnoredMethods = new()
    {
        "Equals", "GetHashCode", "ToString", "GetType",
        "ReferenceEquals", "MemberwiseClone", "Finalize"
    };

    static void Main(string[] args)
    {
        if (args.Length < 1)
        {
            PrintUsage();
            return;
        }

        string assemblyPath = Path.GetFullPath(args[0]);
        if (!File.Exists(assemblyPath))
        {
            Console.Error.WriteLine($"Error: File not found: {assemblyPath}");
            Environment.Exit(1);
        }

        // Resolver zależności — szuka DLL-ek w tym samym katalogu
        string? assemblyDir = Path.GetDirectoryName(assemblyPath);
        AppDomain.CurrentDomain.AssemblyResolve += (_, resolveArgs) =>
        {
            string name = new AssemblyName(resolveArgs.Name).Name + ".dll";
            if (assemblyDir == null) return null;
            string file = Path.Combine(assemblyDir, name);
            return File.Exists(file) ? Assembly.LoadFrom(file) : null;
        };

        try
        {
            Assembly assembly = Assembly.LoadFrom(assemblyPath);
            var exportedTypes = assembly.GetExportedTypes().OrderBy(t => t.FullName).ToList();

            if (args.Length == 1)
            {
                // Tryb domyślny: lista typów
                ListTypes(exportedTypes);
            }
            else if (args[1] == "--type" && args.Length >= 3)
            {
                string typeName = args[2];
                InspectType(exportedTypes, typeName);
            }
            else if (args[1] == "--search" && args.Length >= 3)
            {
                string query = args[2];
                SearchTypes(exportedTypes, query);
            }
            else
            {
                PrintUsage();
                Environment.Exit(1);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            if (ex.InnerException != null)
                Console.Error.WriteLine($"Inner: {ex.InnerException.Message}");
            Environment.Exit(1);
        }
    }

    static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  ApiDumper <assembly.dll>                   List all public types");
        Console.WriteLine("  ApiDumper <assembly.dll> --type ClassName  Inspect specific type");
        Console.WriteLine("  ApiDumper <assembly.dll> --search phrase   Search types and members");
    }

    /// <summary>
    /// Tryb 1: Lista wszystkich publicznych typów (tylko nazwy).
    /// Kompaktowy output — kilka KB nawet dla dużych bibliotek.
    /// </summary>
    static void ListTypes(List<Type> types)
    {
        Console.WriteLine($"Public types ({types.Count}):");
        Console.WriteLine();
        foreach (var type in types)
        {
            string kind = type.IsInterface ? "interface"
                : type.IsEnum ? "enum"
                : type.IsAbstract ? "abstract class"
                : "class";
            Console.WriteLine($"  {kind,-16} {type.FullName}");
        }
    }

    /// <summary>
    /// Tryb 2: Szczegóły jednej klasy — properties i metody (bez szumu z System.Object).
    /// Szuka po FullName lub krótkiej nazwie.
    /// </summary>
    static void InspectType(List<Type> types, string typeName)
    {
        var matches = types.Where(t =>
            string.Equals(t.Name, typeName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t.FullName, typeName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0)
        {
            Console.Error.WriteLine($"Type '{typeName}' not found. Use without arguments to list all types.");
            Environment.Exit(1);
        }

        foreach (var type in matches)
        {
            Console.WriteLine($"Type: {type.FullName}");
            if (type.BaseType != null && type.BaseType != typeof(object))
                Console.WriteLine($"  Base: {type.BaseType.FullName}");

            var interfaces = type.GetInterfaces();
            if (interfaces.Length > 0)
                Console.WriteLine($"  Implements: {string.Join(", ", interfaces.Select(i => i.Name))}");

            Console.WriteLine();

            // Enum values
            if (type.IsEnum)
            {
                Console.WriteLine("  Values:");
                foreach (var name in Enum.GetNames(type))
                    Console.WriteLine($"    {name} = {Convert.ToInt32(Enum.Parse(type, name))}");
                Console.WriteLine();
                continue;
            }

            // Properties (zadeklarowane w tym typie, nie odziedziczone)
            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .OrderBy(p => p.Name);
            if (props.Any())
            {
                Console.WriteLine("  Properties:");
                foreach (var p in props)
                {
                    string access = p.CanRead && p.CanWrite ? "get/set"
                        : p.CanRead ? "get"
                        : "set";
                    Console.WriteLine($"    {p.PropertyType.Name} {p.Name} {{ {access} }}");
                }
                Console.WriteLine();
            }

            // Methods (zadeklarowane, bez accessorów i szumu)
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName && !IgnoredMethods.Contains(m.Name))
                .OrderBy(m => m.Name);
            if (methods.Any())
            {
                Console.WriteLine("  Methods:");
                foreach (var m in methods)
                {
                    var pars = string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                    string stat = m.IsStatic ? "static " : "";
                    Console.WriteLine($"    {stat}{m.ReturnType.Name} {m.Name}({pars})");
                }
                Console.WriteLine();
            }
        }
    }

    /// <summary>
    /// Tryb 3: Szukanie po nazwie typu lub członka.
    /// Zwraca typy pasujące do frazy + ich pasujące członki.
    /// </summary>
    static void SearchTypes(List<Type> types, string query)
    {
        bool found = false;

        foreach (var type in types)
        {
            bool typeMatches = type.FullName != null &&
                type.FullName.Contains(query, StringComparison.OrdinalIgnoreCase);

            var matchingProps = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(p => p.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var matchingMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName && !IgnoredMethods.Contains(m.Name)
                    && m.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!typeMatches && matchingProps.Count == 0 && matchingMethods.Count == 0)
                continue;

            found = true;
            Console.WriteLine($"Type: {type.FullName}");

            if (typeMatches && matchingProps.Count == 0 && matchingMethods.Count == 0)
            {
                Console.WriteLine("  (type name matches)");
            }

            foreach (var p in matchingProps)
                Console.WriteLine($"  Prop: {p.PropertyType.Name} {p.Name}");

            foreach (var m in matchingMethods)
            {
                var pars = string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                Console.WriteLine($"  Meth: {m.ReturnType.Name} {m.Name}({pars})");
            }

            Console.WriteLine();
        }

        if (!found)
            Console.WriteLine($"No results for '{query}'.");
    }
}
```

- [ ] **Step 2: Zbudować ApiDumper i sprawdzić że kompiluje się**

```bash
dotnet build soneta-dll-inspector/scripts/ApiDumper/ApiDumper.csproj
```

Expected: Build succeeded.

- [ ] **Step 3: Szybki test — uruchomić na dowolnej DLL systemowej**

```bash
# Test trybu list-types
dotnet run --project soneta-dll-inspector/scripts/ApiDumper/ApiDumper.csproj -- /usr/share/dotnet/shared/Microsoft.NETCore.App/10.0.0/System.Console.dll

# Test trybu --type
dotnet run --project soneta-dll-inspector/scripts/ApiDumper/ApiDumper.csproj -- /usr/share/dotnet/shared/Microsoft.NETCore.App/10.0.0/System.Console.dll --type Console

# Test trybu --search
dotnet run --project soneta-dll-inspector/scripts/ApiDumper/ApiDumper.csproj -- /usr/share/dotnet/shared/Microsoft.NETCore.App/10.0.0/System.Console.dll --search Write

# Test bez argumentów (usage)
dotnet run --project soneta-dll-inspector/scripts/ApiDumper/ApiDumper.csproj
```

Expected: Każde polecenie daje sensowny output, żadne nie crashuje.

Uwaga: ścieżka do DLL systemowej może się różnić. Użyj `ls /usr/share/dotnet/shared/Microsoft.NETCore.App/` żeby znaleźć aktualną wersję, lub użyj innej DLL z systemu.

- [ ] **Step 4: Commit**

```bash
git add soneta-dll-inspector/scripts/ApiDumper/Program.cs
git commit -m "feat: implement ApiDumper with 3 modes (list-types, --type, --search)"
```

---

### Task 3: Napisanie SKILL.md dla soneta-dll-inspector

**Files:**
- Create: `soneta-dll-inspector/SKILL.md`

- [ ] **Step 1: Napisać SKILL.md**

Plik `soneta-dll-inspector/SKILL.md`:

```markdown
---
name: soneta-dll-inspector
description: "Inspekcja publicznego API bibliotek Sonety z DLL-ek. Używaj tego skilla gdy AI potrzebuje poznać nazwy klas, metod, właściwości z dowolnej biblioteki enova365/Soneta — np. przed implementacją dodatku WebAPI, formularza UI, czy modelu danych. Skill nie trzyma predefiniowanych danych — generuje metadane on-demand z DLL-ek projektu użytkownika."
---

# Inspekcja API bibliotek Soneta

## Kiedy używać

Gdy AI potrzebuje poznać publiczne API biblioteki Sonety:
- Jakie klasy/interfejsy są w danym module?
- Jakie właściwości i metody ma konkretna klasa?
- Jak się nazywa metoda do konkretnej operacji?

## Wymaganie wstępne

Projekt użytkownika musi być zbudowany żeby DLL-ki były dostępne:

```bash
dotnet build
```

DLL-ki Sonety pojawią się w `bin/Debug/` projektu (np. `Soneta.Handel.dll`, `Soneta.Business.dll`).

## Jak uruchomić ApiDumper

```bash
# Ścieżka do ApiDumper relatywna do katalogu tego skilla
dotnet run --project <ścieżka-do-skilla>/scripts/ApiDumper/ApiDumper.csproj -- <argumenty>
```

Jeśli ApiDumper nie był wcześniej budowany, `dotnet run` zbuduje go automatycznie.

## Tryby pracy

### 1. Lista typów (domyślny)

Zwraca kompaktową listę wszystkich publicznych typów z DLL:

```bash
dotnet run --project .../ApiDumper.csproj -- bin/Debug/Soneta.Handel.dll
```

Output:
```
Public types (245):

  class            Soneta.Handel.DokumentHandlowy
  class            Soneta.Handel.PozycjaDokHandlowego
  interface        Soneta.Handel.IDokumentHandlowy
  enum             Soneta.Handel.StanDokumentuHandlowego
  ...
```

Użyj tego trybu na początek, żeby zobaczyć co jest dostępne.

### 2. Szczegóły typu (`--type`)

Zwraca właściwości i metody konkretnej klasy (bez szumu z System.Object):

```bash
dotnet run --project .../ApiDumper.csproj -- bin/Debug/Soneta.Handel.dll --type DokumentHandlowy
```

Output:
```
Type: Soneta.Handel.DokumentHandlowy
  Base: Soneta.Handel.DokumentHandlowyRow

  Properties:
    Kontrahent Kontrahent { get/set }
    Decimal Netto { get }
    StanDokumentuHandlowego Stan { get }
    ...

  Methods:
    Void Zatwierdz()
    Void Anuluj()
    ...
```

Szuka po krótkiej nazwie (`DokumentHandlowy`) lub pełnej (`Soneta.Handel.DokumentHandlowy`).

### 3. Wyszukiwanie (`--search`)

Szuka typów i członków pasujących do frazy:

```bash
dotnet run --project .../ApiDumper.csproj -- bin/Debug/Soneta.Handel.dll --search Zatwierdz
```

Output:
```
Type: Soneta.Handel.DokumentHandlowy
  Meth: Void Zatwierdz()
  Meth: Void ZatwierdzDoWysylki()

Type: Soneta.Handel.ZamowienieHandlowe
  Meth: Void Zatwierdz()
```

## Standardowe biblioteki Soneta

| DLL | Zawartość |
|-----|-----------|
| `Soneta.Business.dll` | Bazowe klasy ORM (Session, Module, Table, Row) |
| `Soneta.Handel.dll` | Dokumenty handlowe, pozycje, magazyny |
| `Soneta.CRM.dll` | Kontrahenci, kontakty |
| `Soneta.Kasa.dll` | Raporty kasowe, rozrachunki |
| `Soneta.KadryPlace.dll` | Kadry i płace |
| `Soneta.Core.dll` | Konfiguracja, użytkownicy, uprawnienia |
| `Soneta.Data.dll` | Warstwa dostępu do danych |
| `Soneta.Types.dll` | Typy bazowe, DynamicApi atrybuty |

## Workflow

1. Zbuduj projekt: `dotnet build`
2. Lista typów: `ApiDumper bin/Debug/Soneta.Handel.dll` — zorientuj się co jest
3. Drill-down: `ApiDumper ... --type DokumentHandlowy` — zbadaj konkretną klasę
4. Szukaj: `ApiDumper ... --search Zatwierdz` — znajdź metody po nazwie
```

- [ ] **Step 2: Commit**

```bash
git add soneta-dll-inspector/SKILL.md
git commit -m "feat: add SKILL.md for soneta-dll-inspector"
```

---

### Task 4: Oczyszczenie soneta-addon-webapi-create

**Files:**
- Delete: `soneta-addon-webapi-create/metadata/` (cały katalog, 12 plików)
- Delete: `soneta-addon-webapi-create/scripts/` (cały katalog z ApiDumper + bin/obj)
- Delete: `soneta-addon-webapi-create/soneta-addon-webapi-create.skill` (1.1MB ZIP)
- Delete: `soneta-addon-webapi-create/.DS_Store`
- Delete: `soneta-addon-webapi-create/.claude/` (cały katalog)

- [ ] **Step 1: Usunąć zbędne pliki**

```bash
git rm -r soneta-addon-webapi-create/metadata/
git rm -r soneta-addon-webapi-create/scripts/
git rm soneta-addon-webapi-create/soneta-addon-webapi-create.skill
git rm -f soneta-addon-webapi-create/.DS_Store
git rm -rf soneta-addon-webapi-create/.claude/
```

- [ ] **Step 2: Sprawdzić że zostały tylko pożądane pliki**

```bash
ls -R soneta-addon-webapi-create/
```

Expected:
```
soneta-addon-webapi-create/:
SKILL.md  references/

soneta-addon-webapi-create/references:
api-class-template.cs  csproj-template.xml  interface-template.cs  nuget-config-template.xml  props-template.xml
```

- [ ] **Step 3: Commit**

```bash
git add -A soneta-addon-webapi-create/
git commit -m "refactor: remove metadata, ApiDumper, and .skill archive from soneta-addon-webapi-create"
```

---

### Task 5: Przepisanie SKILL.md dla soneta-addon-webapi-create

**Files:**
- Modify: `soneta-addon-webapi-create/SKILL.md`

- [ ] **Step 1: Nadpisać SKILL.md nową wersją**

Plik `soneta-addon-webapi-create/SKILL.md`:

```markdown
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
using (var workSession = _session.Login.CreateSession(false, false))
using (var transaction = workSession.Logout(true))
{
    // Logika...
    transaction.Commit();
}
```
```

- [ ] **Step 2: Commit**

```bash
git add soneta-addon-webapi-create/SKILL.md
git commit -m "refactor: rewrite SKILL.md to use soneta-dll-inspector instead of embedded metadata"
```

---

### Task 6: Aktualizacja README

**Files:**
- Modify: `README.md` (jeśli zawiera opis skilli)

- [ ] **Step 1: Sprawdzić README.md**

Przeczytać `README.md` i zaktualizować opis skilli — dodać `soneta-dll-inspector`, zaktualizować opis `soneta-addon-webapi-create`.

- [ ] **Step 2: Commit**

```bash
git add README.md
git commit -m "docs: update README with soneta-dll-inspector and updated soneta-addon-webapi-create description"
```
