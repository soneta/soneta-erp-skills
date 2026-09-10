# Skanowanie folderów statycznych menu z DLL (Roslyn MetadataReference)

Narzędzie do zbudowania **drzewa folderów statycznych** programu Soneta — pozycji menu
zadeklarowanych atrybutem assembly `[assembly: FolderView(...)]`. Czyta metadane skompilowanych
bibliotek dodatku, nie wymaga źródeł ani uruchamiania aplikacji.

> **Ścieżki poleceń** w tym dokumencie są względne wobec katalogu skilla (`skills/config/`
> w repozytorium) — ustal lokalizację zainstalowanego skilla i uruchamiaj polecenia z tego
> katalogu albo poprzedź jego ścieżką.

## Cel

Struktura menu Soneta jest opisana zbiorem atrybutów `FolderViewAttribute` przypiętych do assembly
(`Soneta.Business/UI/FolderViewAttribute.cs`). Każdy atrybut deklaruje jedną pozycję menu przez
ścieżkę (`FullPath`, człony rozdzielone `/`), a z wszystkich ścieżek składa się drzewo folderów firmy.
Skrypt pozwala szybko zobaczyć:
- jaka jest pełna hierarchia folderów (menu → podmenu → listy/formularze);
- który folder pokazuje listę (`ViewInfoType`/`ViewType`/`TableName`), a który formularz (`ObjectType`);
- jaki `Description` (dwulinijkowy opis kafla) i jaka tabela/`ViewInfo` stoi za daną pozycją.

Używaj tego narzędzia, gdy:
- eksplorujesz nieznany dodatek i chcesz zobaczyć, jak rozbudowuje menu programu;
- projektujesz nowy folder i szukasz właściwej ścieżki-rodzica (żeby wpiąć się w istniejące menu);
- weryfikujesz, że nowo dodany `[assembly: FolderView]` trafił we właściwe miejsce drzewa;
- potrzebujesz mapy: pozycja menu ↔ `ViewInfo`/tabela/typ obiektu.

## Foldery statyczne vs dynamiczne

Skrypt czyta **atrybuty assembly**, więc łapie wyłącznie **foldery statyczne** — te zadeklarowane
w kodzie przez `[assembly: FolderView(...)]`. W programie istnieją też **foldery dynamiczne**,
generowane w czasie działania aplikacji (np. `SubFolderViewAttribute`, foldery pulpitów/ticketów
budowane w runtime z danych bazy). Tych skrypt nie widzi — nie istnieją jako atrybuty w metadanych.

## Mechanizm

Skrypt używa **Roslyn** (`Microsoft.CodeAnalysis.CSharp`) i `MetadataReference.CreateFromFile`,
czyli metadane są czytane bez ładowania IL do CLR — bezpiecznie, bez ryzyka konfliktów wersji,
x86/x64 itp.

Algorytm:
1. Zbierz wszystkie `*.dll` z podanego katalogu i zarejestruj jako `MetadataReference`. Dodatkowo
   dołącz biblioteki runtime'u .NET z listy TPA
   (`AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")`) — bez tego Roslyn nie rozwiązuje
   argumentów atrybutów i `Description`/typy zostają puste.
2. Zbuduj `CSharpCompilation` z tymi referencjami.
3. Dla każdej referencji przejdź po `IAssemblySymbol.GetAttributes()` i wybierz atrybuty będące
   `FolderViewAttribute` **lub dziedziczące z niego** (łańcuch `BaseType`, np. `HandelFolderViewAttribute`,
   `BIFolderViewAttribute`, `ConfigurationFolderViewAttribute`). Dzięki temu łapane są też
   licencyjne/kontekstowe warianty atrybutu, które w konstruktorze i tak przekazują `path` do bazowego.
4. Z każdego atrybutu odczytaj:
   - `FullPath` — pierwszy argument konstruktora typu `string` (ścieżka menu). Stałe (`const string`
     w klasach typu `FoldersPath…`) są w metadanych już rozwinięte do wartości, więc odczytują się poprawnie.
   - Named arguments: `Description`, `ViewInfoType`, `ObjectType`, `ViewType`, `TableName`, `IconName`, `Priority`.
5. Zbuduj drzewo: ścieżkę dziel po `/`, każdy człon to węzeł. Brakujące węzły pośrednie (rodzic bez
   własnej deklaracji) tworzone są jako niejawne foldery menu. Wiele deklaracji o tej samej ścieżce
   (różne `Contexts`/licencje) scala się w jeden węzeł — licznik `deklaracji: N` to sygnalizuje.
6. Sklasyfikuj każdy węzeł (zgodnie z `FolderViewAttribute.IsMenuFolder`, gdzie menu ⟺ brak
   `ViewInfoType`/`ViewType`/`ObjectType`). Rodzaj folderu oznaczany jest tekstowym skrótem:
   - `[FORMULARZ]` — ustawiony `ObjectType` (zakładka obiektu, widok inny niż lista);
   - `[LISTA]` — ustawiony `ViewInfoType`, `ViewType` lub `TableName`. W praktyce typ `ViewInfo`
     bywa przekazywany także przez `ViewType`, dlatego oba traktowane są wspólnie jako „folder z widokiem”;
   - `[MENU]` — pozostałe, czyli folder grupujący inne foldery.
7. Wypisz markdown: drzewo z wcięciami (`- [LISTA] **Nazwa** — View: …, Table: …`), a pod pozycjami
   z opisem dodatkowa linia `<br>_Description_`. `Description` brany jest z reprezentatywnej
   deklaracji, a gdy jej brak — z dowolnej deklaracji tego węzła, która opis niesie.

## Wymagania

- .NET SDK 10
- `dotnet-script`:
  ```bash
  dotnet tool install -g dotnet-script
  ```
  Global tool ląduje w `~/.dotnet/tools`, który bywa **poza PATH** (`which dotnet-script` nie
  znajduje mimo `dotnet tool list -g`). Dodaj go do PATH — macOS/Linux:
  `export PATH="$PATH:$HOME/.dotnet/tools"`; Windows (PowerShell):
  `$env:PATH += ";$env:USERPROFILE\.dotnet\tools"`.

## Uruchomienie

```bash
dotnet script scripts/scan-folders.csx \
    -- <KatalogDll> [<PrefiksSciezki>] [--flat]
```

- `<KatalogDll>` — katalog z bibliotekami dodatku (wymagany).
- `<PrefiksSciezki>` — opcjonalny filtr: pokaż tylko foldery, których `FullPath` zaczyna się od
  tego prefiksu (np. `Handel`, `"CRM/Poczta"`). W praktyce niemal zawsze potrzebny — pełne drzewo
  to ponad 1000 węzłów.
- `--flat` — płaska lista pełnych ścieżek zamiast drzewa (wygodne do grepowania).

### Przykłady

```bash
# Całe drzewo (duże!)
dotnet script scripts/scan-folders.csx -- ./bin/Debug/net10.0

# Tylko poddrzewo Handel
dotnet script scripts/scan-folders.csx -- ./bin/Debug/net10.0 Handel

# Płaska lista folderów poczty CRM
dotnet script scripts/scan-folders.csx -- ./bin/Debug/net10.0 "CRM/Poczta" --flat
```

### Przykładowe wyjście

```markdown
# Foldery statyczne (Soneta)

Znaleziono deklaracji `FolderView`: 1046
Węzłów w drzewie: 1002
Filtr ścieżki: `Handel`

Legenda typów: `[LISTA]` folder z widokiem (ViewInfoType/ViewType/TableName) · `[FORMULARZ]` folder z formularzem obiektu (ObjectType) · `[MENU]` folder grupujący inne foldery

- `[MENU]` **Handel**
  <br>_Dokumenty handlowe, magazynowe, towary i usługi, kompletacja_
  - `[MENU]` **Deklaracje**
    <br>_Deklaracje Intrastat Przywóz i Intrastat Wywóz_
    - `[LISTA]` **INTRASTAT Przywóz** — View: `IntrastatPrzywózViewInfo`, Table: `Deklaracja`
      <br>_Deklaracje Intrastat Przywóz_
  - `[MENU]` **Dokumenty razem i pozostałe**
    - `[LISTA]` **Wszystkie dokumenty** — View: `DokHandloweViewInfo`, Table: `DokumentHandlowy`
      <br>_Wszystkie dokumenty faktur, korekt, magazynowe i pozostałe razem_

_Łącznie folderów: …_
```

## Kody wyjścia

| Kod | Znaczenie |
|-----|-----------|
| `0` | OK — wypisano drzewo folderów |
| `1` | Błąd argumentów / nie istnieje katalog / brak DLL / brak folderów dla prefiksu |

## Ograniczenia

- Widzi tylko **foldery statyczne** (atrybuty assembly) — patrz sekcja „Foldery statyczne vs dynamiczne”.
- Skanuje tylko górny poziom katalogu (`SearchOption.TopDirectoryOnly`) — jeśli DLL są rozproszone,
  skopiuj je do jednego katalogu.
- `IsVisible`/`Contexts`/licencje nie są ewaluowane — drzewo pokazuje wszystkie zadeklarowane foldery
  niezależnie od tego, czy dany operator/licencja by je zobaczył.
- Pierwsze uruchomienie pobiera pakiet NuGet `Microsoft.CodeAnalysis.CSharp` — wymaga połączenia
  internetowego (kolejne odpalenia działają offline).

## Powiązania

- `scan-modules` (skill [programming](../../programming/SKILL.md)) — inwentaryzacja modułów i tabel; z niego weźmiesz
  nazwę `TableName`, którą zobaczysz jako źródło listy w folderze.
- `scan-props` (skill [programming](../../programming/SKILL.md)) — pola konkretnej tabeli/`ViewInfo` widocznej w folderze.
- `viewinfo` (skill [programming](../../programming/SKILL.md)) — jak działa `ViewInfo` sterujący zawartością listy w folderze.
- Narzędzia inwentaryzacyjne komplementarne do tego skanu opisuje skill [programming](../../programming/SKILL.md).
