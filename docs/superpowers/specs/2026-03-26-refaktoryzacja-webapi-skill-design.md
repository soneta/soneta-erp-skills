# Refaktoryzacja soneta-addon-webapi-create na 2 skille

## Problem

Skill `soneta-addon-webapi-create` ma 72 pliki i łączy za dużo odpowiedzialności:
- Scaffolding projektu WebAPI (szablony, wzorce)
- Dumpy metadanych z DLL-ek (12 plików .txt, setki KB każdy)
- Narzędzie ApiDumper (projekt C# z artefaktami buildowymi)
- Archiwum .skill ZIP (1.1MB duplikat zawartości)

Inne skille w projekcie mają 5-8 plików i są skupione na jednej rzeczy.

Metadata z API Sonety przydaje się nie tylko do WebAPI, ale do każdego typu dodatku enova365 (UI desktopowe, UI webowe, formularze, modele danych).

## Decyzja

Rozbić na 2 skille:
1. **`soneta-dll-inspector`** — narzędziowy skill do inspekcji publicznego API bibliotek Sonety
2. **`soneta-addon-webapi-create`** — odchudzony skill do scaffoldingu WebAPI/DynamicAPI

## Skill 1: soneta-dll-inspector

### Cel

Pozwala AI zbadać publiczne API dowolnej biblioteki Sonety. Lekki — nie trzyma żadnych predefiniowanych danych, generuje metadane na żądanie z DLL-ek projektu użytkownika.

### Struktura plików

```
soneta-dll-inspector/
├── SKILL.md
└── scripts/
    └── ApiDumper/
        ├── ApiDumper.csproj    (net10.0, Microsoft.NET.Sdk)
        └── Program.cs
```

3-4 pliki. Artefakty buildowe (bin/obj) w .gitignore.

### ApiDumper — tryby pracy

| Komenda | Output | Zastosowanie |
|---------|--------|-------------|
| `ApiDumper <dll>` | Lista typów (tylko pełne nazwy) | "Co jest w tej bibliotece?" |
| `ApiDumper <dll> --type NazwaKlasy` | Właściwości + metody jednej klasy | Drill-down na konkretny typ |
| `ApiDumper <dll> --search fraza` | Typy/metody pasujące do frazy | "Jak się nazywa metoda do X?" |

### Filtrowanie szumu

- Pomija odziedziczone z `System.Object`: `Equals`, `GetHashCode`, `ToString`, `GetType`
- Pomija property accessory (`get_`, `set_`)
- Pomija typy wewnętrzne (internal) — tylko publiczne API

### Wymaganie wstępne

Projekt użytkownika musi być zbudowany (`dotnet build`) żeby DLL-ki były dostępne w `bin/Debug/`. Skill instruuje AI, żeby to zrobiła przed inspekcją.

### SKILL.md — zawartość

- Opis: kiedy i jak używać inspektora
- Workflow: `dotnet build` → `--list-types` → `--type X` → `--search Y`
- Jak zbudować ApiDumper: `dotnet build scripts/ApiDumper/ApiDumper.csproj`
- Jak uruchomić: `dotnet run --project <ścieżka>/scripts/ApiDumper/ApiDumper.csproj -- <dll> [opcje]`
- Lista standardowych DLL-ek Sonety (nazwy, nie zawartość): Soneta.Business, Soneta.Handel, Soneta.CRM, Soneta.Kasa, Soneta.KadryPlace, Soneta.Core, Soneta.Data, Soneta.Types

### Konsumenci

Ten skill jest narzędziowy — korzystają z niego inne skille:
- `soneta-addon-webapi-create` — szuka klas do wystawienia przez DynamicAPI
- `soneta-form-xml` — szuka properties do bindowania w formularzach
- `soneta-business-xml` — szuka istniejących tabel/relacji
- przyszłe skille do UI desktopowego/webowego

## Skill 2: soneta-addon-webapi-create (odchudzony)

### Cel

Tworzenie dodatków WebAPI/DynamicAPI dla enova365. Skupiony wyłącznie na scaffoldingu projektu i wzorcach implementacji.

### Struktura plików

```
soneta-addon-webapi-create/
├── SKILL.md
└── references/
    ├── api-class-template.cs
    ├── interface-template.cs
    ├── csproj-template.xml
    ├── props-template.xml
    └── nuget-config-template.xml
```

6 plików. Bez metadata/, bez ApiDumper, bez .skill ZIP.

### Przepływ pracy (SKILL.md)

1. **Analiza wymagań** — zidentyfikować moduł enova365 do rozszerzenia
2. **Inicjalizacja środowiska** — skopiować nuget.config i Directory.Build.props z szablonów
3. **Utworzenie projektu** — folder + .csproj z szablonu, dodać wymagane PackageReference
4. **Pierwszy build** — `dotnet build` żeby pobrać NuGety i wygenerować DLL-ki
5. **Inspekcja API** — **użyj skilla `soneta-dll-inspector`** żeby zbadać API modułu
6. **Implementacja** — interfejs + klasa API z szablonów, pełne nazwy typów w atrybutach
7. **Weryfikacja** — `dotnet build`

### Wzorce (pozostają w SKILL.md)

- Pobieranie modułów: `HandelModule.GetInstance(workSession)`
- Wyszukiwanie w tabelach: `IEnumerable` cast pattern
- Atrybuty assembly: `[assembly: Service(...)]`, `[assembly: DynamicApiController(...)]`
- Sesja i transakcja: `Login.CreateSession` + `Logout(true)` + `Commit()`

### Usunięte z obecnego skilla

- `metadata/` — 12 plików .txt z dumpami API (zastąpione przez `soneta-dll-inspector`)
- `scripts/ApiDumper/` — przeniesiony do `soneta-dll-inspector`
- `soneta-addon-webapi-create.skill` — 1.1MB archiwum ZIP (niepotrzebne)
- `.DS_Store`
- `.claude/settings.local.json`

## Podsumowanie zmian

| Aspekt | Przed | Po |
|--------|-------|-----|
| Skille | 1 (72 pliki) | 2 (razem ~10 plików) |
| Metadata | 12 plików .txt trzymanych w repo | Generowane on-demand |
| ApiDumper | Dumpuje całą DLL | 3 tryby: list-types, type, search |
| Kontekst AI | Setki KB per inspekcja | Kilka KB per zapytanie |
| Reużywalność | Tylko WebAPI | Inspektor dostępny dla wszystkich skilli |
