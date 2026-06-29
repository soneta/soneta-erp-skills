# Nowy dodatek Soneta od zera (CLI / Soneta SDK)

Jak z wiersza poleceń wygenerować **kompletny szkielet źródeł dodatku** dla platformy
Soneta — gotowy do wypełnienia logiką biznesową (`*.business.xml`, kod Row/Table/Module),
interfejsem (`*.form.xml`) i testami. Procedura opiera się o dwa narzędzia Soneta:

- **Soneta.MsBuild.SDK** (`Soneta.Sdk`) — zestaw MSBuild, który automatycznie konfiguruje
  projekt: pobiera biblioteki Soneta we wskazanej wersji, uruchamia generator
  `*.business.xml → *.business.cs` przy buildzie, oznacza pliki `*.pageform.xml`/`*.dbinit.xml`
  jako `EmbeddedResource`, ustawia program startowy do debugowania. Dzięki niemu nie dodaje się
  ręcznie referencji ani nie podpina generatora.
- **Soneta Platform Developer** — paczka szablonów `dotnet new`, która jednym poleceniem tworzy
  rozdzielone projekty (logika / UI / testy) już skonfigurowane pod SDK.

> **Środowisko:** aktualne biblioteki Soneta celują w **`net10.0`** i są **cross-platform** — cały
> cykl (generowanie szkieletu, build, testy) działa na **macOS, Linux i Windows**. Wymagany jest
> .NET SDK 10. Debugowanie z żywą instancją programu zależy od platformy hosta (patrz [sekcja 9](#9-debugowanie-w-vs-code)).
>
> **Wersje (NuGet, stan bieżący):** biblioteki Soneta (`Soneta.Products.Modules` i moduły jak
> `Soneta.Business`, `Soneta.CRM`, `Soneta.Handel`) — `2606.0.0`; MSBuild SDK `Soneta.Sdk` — `1.2.0`;
> szablony `Soneta.Platform.Developer` — `1.0.7`. Wersje bibliotek są **datowe** (`RRMM.x.x`, np.
> `2606` = czerwiec 2026); zawsze sprawdź najnowszą na feedzie i dobierz zgodną z docelową instalacją Soneta.
>
> **Wersja SDK:** pracujemy na `Soneta.Sdk` `1.2.0`. Szablon przypina starszą wersję w `global.json` —
> po wygenerowaniu ustaw `"Soneta.Sdk": "1.2.0"`. Świeży szkielet wymaga jeszcze nadpisania trzech
> plików konfiguracyjnych poprawną treścią (`global.json`, `Directory.Build.props`, `NuGet.Config`) —
> gotowce w [sekcji 5a](#5a-pliki-konfiguracyjne--poprawna-treść-krok-obowiązkowy). To **obowiązkowy
> krok zaraz po `dotnet new`**, a nie ratunek po nieudanym buildzie.

## Spis treści

1. [Wymagania wstępne](#1-wymagania-wstępne)
2. [Szybka ścieżka — pełny dodatek jednym szablonem](#2-szybka-ścieżka--pełny-dodatek-jednym-szablonem)
3. [Co powstaje — struktura](#3-co-powstaje--struktura)
4. [Szablony Soneta Platform Developer](#4-szablony-soneta-platform-developer)
5. [Pliki konfiguracyjne SDK](#5-pliki-konfiguracyjne-sdk)
5a. [Pliki konfiguracyjne — poprawna treść (krok obowiązkowy)](#5a-pliki-konfiguracyjne--poprawna-treść-krok-obowiązkowy)
6. [Typy projektów i konwencja nazw](#6-typy-projektów-i-konwencja-nazw)
7. [Parametry MSBuild](#7-parametry-msbuild)
8. [Solucja (.sln)](#8-solucja-sln)
9. [Debugowanie w VS Code](#9-debugowanie-w-vs-code)
10. [Intellisense dla plików XML (VS Code)](#10-intellisense-dla-plików-xml-vs-code)
11. [Referencje do bibliotek spoza SonetaPackage](#11-referencje-do-bibliotek-spoza-sonetapackage)
12. [Fallback — ręczny szkielet bez szablonów](#12-fallback--ręczny-szkielet-bez-szablonów)
13. [Po wygenerowaniu — co dalej](#13-po-wygenerowaniu--co-dalej)
14. [Troubleshooting](#14-troubleshooting)

---

## 1. Wymagania wstępne

- Zainstalowany **.NET SDK 10** (`dotnet --version`).
- Dostęp do źródła NuGet z paczkami Soneta (`Soneta.Platform.Developer`, `Soneta.Sdk`,
  `Soneta.Products.Modules`) — publiczny feed nuget.org lub wewnętrzny/lokalny feed firmowy.
  Jeśli paczki nie są dostępne, użyj [fallbacku ręcznego](#12-fallback--ręczny-szkielet-bez-szablonów).
  > **Uwaga:** szablon `soneta-addon` **nie generuje `NuGet.Config`** — projekt zna wtedy tylko
  > globalnie skonfigurowane źródła (zwykle sam `nuget.org`). Biblioteki biznesowe i część paczek
  > testowych żyją na **feedzie Soneta** (`https://nuget.soneta.pl/v3/index.json`). Bez `NuGet.Config`
  > z tym źródłem restore się wysypie. Dodaj go ręcznie —
  > patrz [sekcja 5](#nugetconfig--źródła-paczek-szablon-go-nie-generuje).
- Do debugowania z żywą instancją: zainstalowana platforma Soneta (host do uruchomienia dodatku).

Instalacja szablonów (raz na maszynę):

```bash
# .NET SDK 7+ (zalecane)
dotnet new install Soneta.Platform.Developer

# starsza składnia (.NET SDK ≤ 6) — równoważna
dotnet new -i Soneta.Platform.Developer
```

Sprawdzenie, że szablony są widoczne:

```bash
dotnet new list soneta        # SDK 7+
dotnet new -l                 # starsza składnia — pełna lista szablonów
```

Powinien pojawić się m.in. szablon o short-name `soneta-addon`.

## 2. Szybka ścieżka — pełny dodatek jednym szablonem

Najprostsza droga do gotowego rusztowania. `MyExtension` zastąp nazwą dodatku
(PascalCase, np. `Firma.NazwaModulu`).

```bash
# 1. (raz) zainstaluj szablony — patrz sekcja 1
dotnet new install Soneta.Platform.Developer

# 2. wygeneruj kompletną solucję (logika + UI + testy + .sln + pliki konfiguracyjne)
dotnet new soneta-addon -n MyExtension -o ./MyExtension

# 3. wejdź do katalogu i zbuduj, by zweryfikować konfigurację
cd ./MyExtension
dotnet build
```

- `-n MyExtension` — nazwa bazowa projektów (i namespace).
- `-o ./MyExtension` — katalog docelowy (utworzony, jeśli nie istnieje). Bez `-o` zawartość
  powstaje w bieżącym katalogu.
- `-nt` / `--noTest` — pomiń projekt testowy. `-nui` / `--noUI` — pomiń projekt UI.

Szablon `soneta-addon` to **szablon solucji** — tworzy gotowy `MyExtension.sln` z dodanymi
projektami **oraz** pliki konfiguracyjne SDK (`global.json`, `Directory.Build.props`). Nie trzeba
ręcznie zakładać solucji ani dodawać projektów.

**Kryterium ukończenia:** w katalogu docelowym istnieje `MyExtension.sln`, trzy `*.csproj`
(`MyExtension`, `MyExtension.UI`, `MyExtension.Tests`), `global.json`, `Directory.Build.props`
oraz dodany `NuGet.Config`, a `dotnet build` przechodzi. Świeży szkielet **nie buduje się od razu** —
dlatego zaraz po `dotnet new`, a przed pierwszym buildem, nadpisz trzy pliki konfiguracyjne poprawną
treścią (gotowce: [sekcja 5a](#5a-pliki-konfiguracyjne--poprawna-treść-krok-obowiązkowy)). Dopiero
zielony build (0 ostrzeżeń, 0 błędów) kończy ten etap.

## 3. Co powstaje — struktura

```
MyExtension/
├── MyExtension.sln              # gotowa solucja z dodanymi 3 projektami
├── global.json                 # wersja Soneta.Sdk + runner testów
├── Directory.Build.props        # wersja bibliotek Soneta, Version, wykrywanie projektu testowego
├── MyExtension/                 # logika biznesowa (Row/Table/Module, *.business.xml)
│   └── MyExtension.csproj
├── MyExtension.UI/              # interfejs użytkownika (*.form.xml, View/ViewInfo)
│   ├── EnsureSonetaTypesReferenceClass.cs
│   └── MyExtension.UI.csproj
└── MyExtension.Tests/           # testy (Microsoft.Testing.Platform + AwesomeAssertions)
    └── MyExtension.Tests.csproj
```

Podział na trzy projekty odzwierciedla architekturę platformy Soneta: **logika biznesowa**, **UI**,
**testy**. Projekt UI/testowy można pominąć flagami `-nui`/`-nt` ([sekcja 2](#2-szybka-ścieżka--pełny-dodatek-jednym-szablonem)).
Gotowy szkielet wypełnia się gotowcami (`*.business.xml`, `*.pageform.xml`, worker…) generowanymi
**szablonami elementów** — patrz [sekcja 4](#4-szablony-soneta-platform-developer).

## 4. Szablony Soneta Platform Developer

Paczka dostarcza **jeden szablon solucji** (rusztowanie całego dodatku) oraz **szablony elementów**
(`soneta-item-*`) do dokładania pojedynczych plików do istniejącego projektu. Aktualną listę i
short-name'y zawsze sprawdzisz `dotnet new list soneta`.

**Szablon solucji** — start nowego dodatku:

| Short name | Tworzy |
|------------|--------|
| `soneta-addon` | Kompletna solucja: projekty logiki + UI + testów, `.sln`, `global.json`, `Directory.Build.props`. Opcje `-nt` (bez testów), `-nui` (bez UI). |

**Szablony elementów** — wypełnianie szkieletu treścią (uruchamiane wewnątrz katalogu projektu):

| Short name | Dodaje | Powiązany skill |
|------------|--------|-----------------|
| `soneta-item-businessxml` | Plik `*.business.xml` (definicja obiektów biznesowych) | **soneta-business-xml** |
| `soneta-item-pageform` | Plik `*.pageform.xml` (formularz) | **soneta-form-xml** |
| `soneta-item-viewinfo` | Klasa `ViewInfo` (definicja widoku listy) | [references/viewinfo.md](viewinfo.md) |
| `soneta-item-worker` | Klasa Worker (akcja w menu Czynności) | [references/worker-extender.md](worker-extender.md) |
| `soneta-item-dashboard` | Pulpit / dashboard | **soneta-form-xml** |

Szablony elementów przyjmują własne parametry — sprawdź `dotnet new <short-name> --help`. Przykład
(worker dla wybranego typu danych, z parametrami i tytułem okna):

```bash
cd MyExtension/MyExtension          # wewnątrz projektu logiki
dotnet new soneta-item-worker -n KsiegowanieWorker \
  --workername Ksiegowanie \
  --worker-datatype Soneta.Handel.DokumentHandlowy
```

Wybrane parametry `soneta-item-worker`: `-wn/--workername` (nazwa klasy bez słowa „Worker"),
`-t/--worker-datatype` (typ danych `DataType`, np. `Soneta.CRM.Kontrahent`), `-wp/--worker-params`
(generuj parametry), `-pc/--worker-params-caption` (tytuł okna `CaptionHtml`), `-pr/--worker-priority`.

## 5. Pliki konfiguracyjne SDK

Te pliki sprawiają, że projekt jest „dodatkiem Soneta". Szablon generuje `global.json`,
`Directory.Build.props` i `*.csproj`; **`NuGet.Config` musisz dodać sam** (szablon go nie tworzy).
Przy [ręcznym szkielecie](#12-fallback--ręczny-szkielet-bez-szablonów) tworzysz wszystkie cztery.

### `NuGet.Config` — źródła paczek (szablon go NIE generuje)

Bez tego pliku restore widzi tylko globalne źródła (zwykle sam `nuget.org`) i nie znajdzie bibliotek
Soneta — typowy pierwszy błąd po wygenerowaniu szkieletu. Utwórz `NuGet.Config` w
katalogu głównym solucji z feedem Soneta:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="soneta" value="https://nuget.soneta.pl/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="nuget.org"><package pattern="*" /></packageSource>
    <packageSource key="soneta"><package pattern="*" /></packageSource>
  </packageSourceMapping>
</configuration>
```

`packageSourceMapping` nie jest obowiązkowy, ale porządkuje, skąd lecą paczki. Powyższe dwa źródła
(`nuget.org` + `soneta`) **w zupełności wystarczają** — zweryfikowane czystym buildem dodatku z logiką,
UI i testami (0 ostrzeżeń, 0 błędów). Nie dokładaj innych feedów (np. DevExpress) „na zapas" — SDK
ciągnie wszystkie biblioteki z feedu Soneta. Feed Soneta bywa prywatny — może wymagać
uwierzytelnienia (sekcja `<packageSourceCredentials>` lub `dotnet nuget add source … -u … -p …`).

### `global.json` — wersja SDK

Przypina wersję MSBuild SDK dla całej solucji, dzięki czemu w `*.csproj` wystarczy
`Sdk="Soneta.Sdk"` bez numeru wersji:

```json
{
  "test": {
    "runner": "Microsoft.Testing.Platform"
  },
  "msbuild-sdks": {
    "Soneta.Sdk": "1.2.0"
  }
}
```

`Soneta.Sdk` (MSBuild SDK) ma własną wersję `1.x` — **niezależną** od datowej wersji bibliotek
(`SonetaPackageVersion`). Najnowszą sprawdź na nuget.org / GitHubie Soneta. Sekcja `test` ustawia
runner testów na **Microsoft.Testing.Platform**.

### `Directory.Build.props` — wersja bibliotek i ustawienia wspólne

Definiuje globalnie wersję bibliotek Soneta pobieranych przez SDK oraz ustawienia wspólne dla
wszystkich projektów. Zmiana `SonetaPackageVersion` w jednym miejscu przełącza cały dodatek na inną
wersję bibliotek. Tak generuje go szablon:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="14.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
    <SonetaPackageVersion>2606.0.0</SonetaPackageVersion>  <!-- wersja paczki Soneta.Products.Modules -->
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <AppendRuntimeIdentifierToOutputPath>false</AppendRuntimeIdentifierToOutputPath>
  </PropertyGroup>
  <PropertyGroup>
    <Version>2606.0.1.0</Version>                          <!-- wersja samego dodatku -->
  </PropertyGroup>
  <PropertyGroup Condition="$(MSBuildProjectName.Contains('.Test'))">
    <IsTestProject>true</IsTestProject>
    <EnableMicrosoftTestingPlatformRunner>true</EnableMicrosoftTestingPlatformRunner>
  </PropertyGroup>
</Project>
```

- `SonetaPackageVersion` — wersja paczki `Soneta.Products.Modules` (= wersja bibliotek Soneta).
  Wersje są datowe (`RRMM.x.x`, np. `2606.0.0`); użyj zgodnej z docelową instalacją Soneta.
- **Docelowy framework** (`net10.0`) ma dostarczać sam SDK przez zmienną `$(SonetaTargetFramework)`,
  ale `Soneta.Sdk 1.2.0` zostawia ją pustą, przez co `*.csproj` z
  `<TargetFramework>$(SonetaTargetFramework)</TargetFramework>` pada na restore z
  `error : Invalid framework identifier ''`. **Ustaw go jawnie** w `Directory.Build.props`:
  `<SonetaTargetFramework>net10.0</SonetaTargetFramework>` (zgodnie z docelową instalacją Soneta). To samo
  miejsce służy do świadomego wyboru innej wersji.

### Grupa testowa i wersje paczek

Grupa warunkowa `$(MSBuildProjectName.Contains('.Test'))` w `Directory.Build.props` dotyczy tylko
projektów testowych. SDK `1.2.0` przypina zgodne wersje paczek testowych (NUnit, NUnit3TestAdapter,
Microsoft.NET.Test.Sdk) — nie trzeba ich nadpisywać. Gdyby zaszła potrzeba podmiany konkretnej wersji,
służą do tego właściwości `Soneta*PackageVersion` (**nie** jawny `PackageReference` — SDK je wstrzykuje,
co dałoby `NU1504: Duplicate PackageReference`). Pełną listę właściwości i ich domyślne wartości
znajdziesz w `Sdk.props` paczki SDK — na macOS/Linux:
`~/.nuget/packages/soneta.sdk/<wersja>/Sdk/Sdk.props` (m.in. `SonetaNUnitPackageVersion`,
`SonetaNUnitTestAdapterPackageVersion`, `SonetaMicrosoftNETTestSdkPackageVersion`,
`SonetaNSubstitutePackageVersion`, `SonetaAwesomeAssertionsPackageVersion`).
- Projekt jest rozpoznawany jako **testowy**, gdy nazwa zawiera `.Test` (warunek `MSBuildProjectName.Contains('.Test')`).
- Można tu definiować własne zmienne wielokrotnego użytku dla wszystkich projektów.

### `*.csproj` — projekt na bazie SDK

Wszystkie trzy projekty są identyczne — referencje i framework biorą się z plików powyżej:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project Sdk="Soneta.Sdk">
  <PropertyGroup>
    <TargetFramework>$(SonetaTargetFramework)</TargetFramework>
  </PropertyGroup>
</Project>
```

Bez `global.json` numer wersji SDK podaje się wprost: `<Project Sdk="Soneta.Sdk/1.2.0">`.

## 5a. Pliki konfiguracyjne — poprawna treść (krok obowiązkowy)

Świeżo wygenerowany `dotnet new soneta-addon` (szablony `1.0.7`, biblioteki `2606.0.0`)
**nie buduje się z pudełka** — `dotnet build` pada na restore. Nie czekaj na ten błąd: **zaraz po
`dotnet new`, a przed pierwszym `dotnet build`**, doprowadź trzy pliki do poprawnej postaci. Skopiuj
poniższą treść 1:1 (nazwę `MyExtension` zostaw bez zmian — w tych plikach nie występuje).

**1. `global.json`** — podbij wersję MSBuild SDK (szablon wpisuje starszą, np. `1.1.8`):

```json
{
  "test": {
    "runner": "Microsoft.Testing.Platform"
  },
  "msbuild-sdks": {
    "Soneta.Sdk": "1.2.0"
  }
}
```

**2. `Directory.Build.props`** — dodaj `SonetaTargetFramework` (szablon zostawia tę zmienną pustą,
przez co `<TargetFramework>$(SonetaTargetFramework)</TargetFramework>` w `*.csproj` daje
`error : Invalid framework identifier ''`):

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="14.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
    <SonetaPackageVersion>2606.0.0</SonetaPackageVersion>
    <SonetaTargetFramework>net10.0</SonetaTargetFramework>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <AppendRuntimeIdentifierToOutputPath>false</AppendRuntimeIdentifierToOutputPath>
  </PropertyGroup>
  <PropertyGroup>
    <Version>2606.0.1.0</Version>
  </PropertyGroup>
  <PropertyGroup Condition="$(MSBuildProjectName.Contains('.Test'))">
    <IsTestProject>true</IsTestProject>
    <EnableMicrosoftTestingPlatformRunner>true</EnableMicrosoftTestingPlatformRunner>
  </PropertyGroup>
</Project>
```

**3. `NuGet.Config`** — utwórz w katalogu solucji (szablon **nie generuje** tego pliku, więc restore
zna tylko globalne źródła, zwykle sam `nuget.org`, i nie znajdzie bibliotek Soneta):

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="soneta" value="https://nuget.soneta.pl/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="nuget.org"><package pattern="*" /></packageSource>
    <packageSource key="soneta"><package pattern="*" /></packageSource>
  </packageSourceMapping>
</configuration>
```

Po nadpisaniu tych trzech plików `dotnet build` przechodzi z **0 ostrzeżeń, 0 błędów**. Datowe
wersje (`SonetaPackageVersion 2606.0.0`, `Version`) dobierz pod docelową instalację Soneta; wersja
MSBuild SDK (`1.2.0`) jest niezależna od wersji bibliotek.

## 6. Typy projektów i konwencja nazw

SDK rozpoznaje typ projektu **po nazwie** i dobiera odpowiednie biblioteki:

| Typ | Rozpoznanie | Biblioteki |
|-----|-------------|------------|
| **Testowy** | nazwa zawiera `.Test` | + biblioteki testowe (Microsoft.Testing.Platform, AwesomeAssertions) |
| **UI** | nazwa kończy się na `.UI` | + biblioteki interfejsu |
| **Logika biznesowa** | pozostałe | biblioteki biznesowe |

Projekt testowy łamiący tę konwencję oznacz jawnie flagą w `.csproj`:

```xml
<PropertyGroup>
  <IsTestProject>true</IsTestProject>
</PropertyGroup>
```

## 7. Parametry MSBuild

Dodatkowe właściwości ustawiane w `.csproj` lub `Directory.Build.props`:

| Parametr | Domyślnie | Działanie |
|----------|-----------|-----------|
| `AggregateOutput` | `true` | `true` → wszystkie projekty (poza testowym) budują się do wspólnego folderu (domyślnie `..\bin`), zachowując strukturę (`..\bin\debug\net10.0\`). `false` → przywraca domyślny output per projekt. |
| `AggregatePath` | `..\` | Ścieżka (względna/bezwzględna) doklejana na początku wyliczonego `OutputPath`, gdy `AggregateOutput=true`. Ignorowana, gdy `OutputPath` ustawiono jawnie. |
| `EnableDefaultSonetaPackageReferences` | `true` | `false` → SDK nie dołącza automatycznie referencji do bibliotek biznesowych. |
| `UsingSonetaSdk` | `true` | `false` → projekt nie korzysta z Soneta.MsBuild.SDK. |
| `SonetaAddonStartProgram` | auto (Windows) | Ścieżka do programu uruchamianego z dodatkiem do debugowania. Na Windows wyliczana z `C:\Program Files (x86)\Soneta\` (najnowsza wersja); na macOS/Linux ustaw ją jawnie (host nie jest auto-wykrywany). Mechanizm ustawia program startowy i przekazuje `/extpath=<binaria projektu>`. Pusta wartość = wyłączenie. Nie nadpisuje jawnego `StartProgram` ani `launchSettings.json`. |
| `SonetaValueTuplePackageVersion`, `SonetaNUnitPackageVersion`, `SonetaNUnitTestAdapterPackageVersion` | — | Wersje pakietów pomocniczych. |

`AggregateOutput`/`AggregatePath` istnieją po to, by **wiele projektów solucji budowało się do
jednej lokalizacji** (`..\bin`) — wygodne przy uruchamianiu dodatku przez `/extpath`.

Przy buildzie SDK uruchamia **generator**, który konwertuje pliki `*.xml` (m.in. `*.business.xml`)
na `*.cs`. Odpowiednia wersja generatora pobierana jest razem z bibliotekami — nie podpina się go
ręcznie.

## 8. Solucja (.sln)

Szablon `soneta-addon` **sam tworzy** `MyExtension.sln` i dodaje do niej projekty — zwykle nic tu
nie trzeba robić. Poniższe komendy przydają się tylko przy [ręcznym szkielecie](#12-fallback--ręczny-szkielet-bez-szablonów)
albo gdy dokładasz nowy projekt do istniejącej solucji:

```bash
dotnet new sln -n MyExtension
dotnet sln add ./MyExtension/MyExtension.csproj
dotnet sln add ./MyExtension.UI/MyExtension.UI.csproj
dotnet sln add ./MyExtension.Tests/MyExtension.Tests.csproj
```

Dodanie wszystkich projektów z drzewa jedną komendą:

```bash
find . -name '*.csproj' -exec dotnet sln add {} +     # bash/zsh (macOS/Linux)
```
```powershell
Get-ChildItem *.csproj -Recurse | ForEach-Object { dotnet sln add $_.FullName }   # PowerShell (Windows)
```

## 9. Debugowanie w VS Code

1. Zainstaluj rozszerzenie **C# for Visual Studio Code**.
2. Utwórz domyślny task builda: `F1` → *Tasks: Configure Default Build Task* →
   *Create tasks.json file from template* → **.NET Core**. Powstaje `.vscode/tasks.json`.
   Build: `F1` → *Tasks: Run Build Task* → **build** (powstaje folder `bin`).
3. Dodaj `.vscode/launch.json` wskazujący host Soneta do debugowania i przekazujący
   `/extpath` do binariów dodatku. Dla `net10.0` używamy debuggera `coreclr` (z rozszerzenia C#),
   a binaria leżą w `bin/Debug/net10.0`:

```jsonc
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "Debug",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build",
      // host Soneta — ustaw ścieżkę do swojej instalacji:
      //   Windows: ".../SonetaExplorer.exe" lub ".../SonetaServer.exe"
      //   macOS/Linux: odpowiedni host Soneta (.NET 10) z instalacji
      "program": "<ścieżka do hosta Soneta>",
      "args": ["/extpath=${workspaceFolder}/bin/Debug/net10.0"],
      "console": "internalConsole",
      "stopAtEntry": false,
      "internalConsoleOptions": "openOnSessionStart"
    }
  ]
}
```

Start debugowania: `F5`. Kluczowy parametr to **`/extpath=<ścieżka do binariów dodatku>`** — mówi
programowi Soneta, skąd doładować skompilowany dodatek. Dostosuj ścieżkę `program` do platformy
i wybranego produktu (Explorer / Server).

## 10. Intellisense dla plików XML (VS Code)

Visual Studio (pełne) nie wymaga konfiguracji. VS Code domyślnie nie podpowiada w plikach XML wg
schematów XSD — doinstaluj rozszerzenie **Xml Complete**. Działa w oparciu o schematy Soneta.
Nowo generowane pliki XML mają już potrzebne atrybuty; istniejące uzupełnij o:

```xml
xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
xmlns:xsd="http://www.w3.org/2001/XMLSchema"
xmlns="http://www.enova.pl/schema/form.xsd"
xsi:schemaLocation="http://www.enova.pl/schema/ http://www.enova.pl/schema/form.xsd"
```

(`form.xsd` zamień na właściwy schemat dla danego typu pliku — np. `business.xsd`, `config.xsd`.)

## 11. Referencje do bibliotek spoza SonetaPackage

Zwykle SDK dostarcza wszystkie potrzebne biblioteki automatycznie. Czasem trzeba sięgnąć po
bibliotekę spoza `SonetaPackage` — typowy przypadek to stary dodatek z UI opartym o **WinForms**
(sprzed `form.xml`). **Najlepiej zaktualizować UI do `form.xml`** (w pełni wspierany przez SDK).
Gdy to niemożliwe, zareferuj brakującą bibliotekę jawnie:

```xml
<ItemGroup>
  <Reference Include="Soneta.Forms">
    <HintPath>C:\Program Files (x86)\Soneta\enova365 <wersja>\Soneta.Forms.dll</HintPath>
    <SpecificVersion>false</SpecificVersion>
    <Private>false</Private>
  </Reference>
</ItemGroup>
```

Dla wielu bibliotek wskaż folder wyszukiwania zamiast `HintPath`:

```xml
<PropertyGroup>
  <ReferencePath>C:\Program Files (x86)\Soneta\enova365 <wersja>\</ReferencePath>
  <AssemblySearchPaths>$(AssemblySearchPaths);$(ReferencePath);</AssemblySearchPaths>
</PropertyGroup>
```

**Zgodność wersji:** jeśli zareferowane biblioteki same odwołują się do bibliotek Soneta
dostarczanych przez SDK, wersje z obu źródeł powinny być zgodne. Po podniesieniu
`SonetaPackageVersion` zwykle nadal działają, ale duże zmiany w paczce mogą wymusić aktualizację
jawnych referencji do nowszej wersji DLL.

## 12. Fallback — ręczny szkielet bez szablonów

Gdy `dotnet new install Soneta.Platform.Developer` nie zadziała (brak dostępu do feedu),
odtwórz szkielet ręcznie — efekt jest taki sam jak z szablonu:

```bash
mkdir MyExtension && cd MyExtension
dotnet new sln -n MyExtension

# katalogi projektów
mkdir MyExtension MyExtension.UI MyExtension.Tests
```

1. W każdym katalogu utwórz `*.csproj` na bazie `Sdk="Soneta.Sdk"` ([sekcja 5](#5-pliki-konfiguracyjne-sdk)).
2. W katalogu głównym utwórz `global.json` i `Directory.Build.props` ([sekcja 5](#5-pliki-konfiguracyjne-sdk)).
3. Dodaj projekty do solucji ([sekcja 8](#8-solucja-sln)).

Nazewnictwo projektów musi trzymać konwencję z [sekcji 6](#6-typy-projektów-i-konwencja-nazw)
(`.UI`, `Test`), inaczej SDK dobierze złe biblioteki.

## 13. Po wygenerowaniu — co dalej

Szkielet jest pusty — teraz wypełnia się go treścią. Pojedyncze pliki dokładaj **szablonami
elementów** (`dotnet new soneta-item-*` wewnątrz właściwego projektu — [sekcja 4](#4-szablony-soneta-platform-developer)),
a samą treść pisz wg odpowiedniego skilla. Typowa kolejność:

1. **Założenia / struktura modułu** — skill **soneta-addon-planning**.
2. **Definicje obiektów biznesowych** → projekt logiki. `dotnet new soneta-item-businessxml`;
   treść wg **soneta-business-xml**. Generator zamieni `*.business.xml` na `*.business.cs` przy buildzie.
3. **Kod biznesowy** (klasy `Row`/`Table`/`Module`, workery, extendery) → projekt logiki.
   Worker: `dotnet new soneta-item-worker`. Wzorce: cały skill **soneta-programming** (ten) +
   [references/safe-code.md](safe-code.md).
4. **Interfejs** → projekt `.UI`. `dotnet new soneta-item-pageform` / `soneta-item-viewinfo` /
   `soneta-item-dashboard`; treść wg **soneta-form-xml** i [references/viewinfo.md](viewinfo.md).
5. **Testy** → projekt `*.Test*`.

## 14. Troubleshooting

| Objaw | Przyczyna / rozwiązanie |
|-------|-------------------------|
| `dotnet new soneta-addon` — *No templates found* | Szablony niezainstalowane lub brak feedu. Powtórz `dotnet new install Soneta.Platform.Developer`; sprawdź `dotnet new list`. |
| Build nie znajduje bibliotek Soneta / `Unable to find package …` | Brak `NuGet.Config` z feedem Soneta (szablon go nie generuje — [sekcja 5](#nugetconfig--źródła-paczek-szablon-go-nie-generuje)), zły `SonetaPackageVersion`, albo brak dostępu/uwierzytelnienia do feedu `Soneta.Products.Modules`. |
| `error : Invalid framework identifier ''` na restore | `$(SonetaTargetFramework)` puste — `Soneta.Sdk 1.2.0` nie ustawia tej zmiennej. Ustaw jawnie `<SonetaTargetFramework>net10.0</SonetaTargetFramework>` w `Directory.Build.props` ([sekcja 5](#directorybuildprops--wersja-bibliotek-i-ustawienia-wspólne)). |
| `warning NU1504: Duplicate PackageReference` w projekcie testowym | Dodano wprost `PackageReference` na paczkę, którą SDK już wstrzykuje. Usuń jawną referencję i steruj wersją przez właściwość `Soneta*PackageVersion` zamiast dublować. |
| `Soneta.Sdk` nie rozwiązuje wersji | Brak `global.json` z `msbuild-sdks` **lub** brak `/<numerWersji>` w atrybucie `Sdk` w `.csproj`. |
| Build pada na brak .NET 10 | Zainstaluj **.NET SDK 10** (`dotnet --version`); biblioteki Soneta celują w `net10.0`. |
| F5 uruchamia zły/brak host Soneta | Popraw `program` w `launch.json` (na macOS/Linux ustaw ścieżkę jawnie); sprawdź `SonetaAddonStartProgram` i `launchSettings.json` (mają priorytet nad mechanizmem SDK). |
| Brak podpowiedzi w plikach `*.xml` w VS Code | Doinstaluj **Xml Complete** i uzupełnij atrybuty `xmlns`/`schemaLocation` ([sekcja 10](#10-intellisense-dla-plików-xml-vs-code)). |
| Stare rozszerzenie *Soneta Studio Ext* w VS | Odinstaluj — kłóci się z Soneta Platform Developer. |
