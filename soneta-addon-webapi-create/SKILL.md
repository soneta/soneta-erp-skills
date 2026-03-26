---
name: soneta-addon-webapi-create
description: "Tworzenie nowych dodatków WebAPI (DynamicAPI) dla systemu enova365/Soneta. ZAWSZE używaj tego skilla gdy użytkownik: (1) prosi o utworzenie nowego projektu dodatku/rozszerzenia enova365 z endpointami WebAPI/DynamicAPI; (2) pyta o strukturę projektu Soneta.Sdk, pliki .csproj, Directory.Build.props lub nuget.config dla enova365; (3) chce dodać nowy kontroler DynamicApiController lub interfejs z atrybutami DynamicApiMethod; (4) pyta o wzorce dostępu do modułów enova365 (HandelModule, Session, WorkSession, tabele, rekordy); (5) potrzebuje poznać API bibliotek Soneta (nazwy klas, metod, właściwości) z plików metadanych."
---

# Tworzenie dodatku WebAPI enova365

## Przepływ pracy

1. **Analiza wymagań** — zidentyfikować moduł enova365 (Handel, Kadry, CRM, Kasa itp.) do rozszerzenia.
2. **Inicjalizacja środowiska** — skopiować `references/nuget-config-template.xml` jako `nuget.config` w katalogu głównym projektu. Utworzyć `Directory.Build.props` na bazie `references/props-template.xml`.
3. **Utworzenie projektu** — utworzyć folder i plik `.csproj` na bazie `references/csproj-template.xml`. Dodać wymagane `PackageReference` dla modułów.
4. **Pierwszy build** — jeśli `bin/Debug/` nie istnieje, uruchomić `dotnet build` aby pobrać NuGety i wygenerować DLL-ki potrzebne do inspekcji metadanych.
5. **Inspekcja metadanych** — poznać nazwy tabel, klas i metod (patrz sekcja poniżej).
6. **Implementacja** — utworzyć interfejs (`references/interface-template.cs`) i klasę API (`references/api-class-template.cs`), używając **pełnych nazw typów** w atrybutach assembly.
7. **Weryfikacja** — uruchomić `dotnet build`.

## Inspekcja metadanych

### Pliki metadanych (preferowane)

Przeszukać pliki `.txt` w katalogu `metadata/` skilla:

```bash
# Szukanie klasy i jej członków
grep -A 100 "Type: Soneta.Handel.DokumentHandlowy" metadata/Soneta.Handel.txt

# Szukanie konkretnej metody
grep "Meth: .*Zatwierdz" metadata/Soneta.Handel.txt
```

Dostępne pliki metadanych: `Soneta.Business`, `Soneta.Core`, `Soneta.CRM`, `Soneta.Data`, `Soneta.Drawing`, `Soneta.Handel`, `Soneta.KadryPlace`, `Soneta.Kasa`, `Soneta.Types`.

### Generowanie nowych metadanych (ApiDumper)

Dla bibliotek bez gotowych metadanych użyć skryptu `scripts/ApiDumper`:

```bash
dotnet run --project scripts/ApiDumper/ApiDumper.csproj -- bin/Debug/Soneta.NazwaBiblioteki.dll metadata/Soneta.NazwaBiblioteki.txt
```

### Bezpośrednia inspekcja DLL (fallback)

```bash
strings bin/Debug/Soneta.Handel.dll | grep -E "Module|Table|get_" | grep "Dok"
```

## Wzorce enova365

### Pobieranie modułów

```csharp
var hm = HandelModule.GetInstance(workSession);
// alternatywnie:
var hm = (HandelModule)workSession.Modules[typeof(HandelModule)];
```

### Tabele i rekordy (moduł Handel)

| Cel | Klasa rekordu | Tabela w `HandelModule` |
| :--- | :--- | :--- |
| Dokument handlowy | `DokumentHandlowy` | `hm.DokHandlowe` |
| Pozycja dokumentu | `PozycjaDokHandlowego` | `hm.PozycjeDokHan` |
| Magazyn | `Soneta.Magazyny.Magazyn` | `hm.Magazyny` |

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
