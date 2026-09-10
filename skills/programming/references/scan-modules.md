# Skanowanie modułów i tabel z DLL (Roslyn MetadataReference)

Narzędzie do wylistowania wszystkich modułów (`*Module`) platformy Soneta oraz tabel
(`*Row` / `*Table`) zdefiniowanych w każdym z nich. Czyta metadane skompilowanych bibliotek dodatku,
nie wymaga źródeł.

> **Ścieżki poleceń** w tym dokumencie są względne wobec katalogu skilla (`skills/programming/`
> w repozytorium) — ustal lokalizację zainstalowanego skilla i uruchamiaj polecenia z tego
> katalogu albo poprzedź jego ścieżką.

> **Przegląd modułów/tabel masz już gotowy — bez skanowania.** Wygenerowany indeks jest
> zarazem pełną inwentaryzacją, w układzie dwupoziomowym:
> [`../data/props/INDEX.md`](../data/props/INDEX.md) (lista modułów z `Opis` i liczbą tabel)
> → `../data/props/<Moduł>/INDEX.md` (tabele modułu:
> `RowType | Tytuł | Tabela | Konfig | Guided | Historia | Interfaces | Selektor | Plik`,
> z linkiem do kontraktu pól każdej tabeli). Sięgaj po `scan-modules.csx` (poniżej) tylko dla
> **innego katalogu DLL** niż ten, z którego zbudowano `data/props/` (regeneracja indeksu:
> `export-props-all.csx` — patrz [scan-props.md](scan-props.md)). Ten skaner i indeks pokazują
> ten sam zestaw informacji; indeks dodatkowo linkuje do plików pól, więc dla istniejącej
> kompilacji jest wygodniejszy.

## Cel

W modelu Soneta każda baza danych jest opisana zbiorem modułów (`HandelModule`, `KadryModule`,
`CoreModule`, …), a każdy moduł zawiera zagnieżdżone klasy `*Row` definiujące pojedyncze tabele.
Skrypt pozwala szybko zinwentaryzować całą strukturę: jakie moduły są obecne w bibliotekach,
jakie tabele zawierają i jak nazywa się klasa `*Table` używana w sesji (`Session.Tables.*`).

Używaj tego narzędzia, gdy:
- eksplorujesz nieznany zestaw bibliotek i chcesz zobaczyć pełną listę modułów/tabel;
- chcesz znaleźć właściwą nazwę `RowType` lub `TableType` przed użyciem skryptu
  [scan-props](./scan-props.md);
- przygotowujesz dodatek/raport, który potrzebuje pełnego mapowania klasa biznesowa ↔ nazwa tabeli;
- weryfikujesz, że nowy dodatek został poprawnie zarejestrowany (jego `*Module` pojawia się na liście).

## Mechanizm

Skrypt używa **Roslyn** (`Microsoft.CodeAnalysis.CSharp`) i `MetadataReference.CreateFromFile`,
czyli metadane są czytane bez ładowania IL do CLR — bezpiecznie, bez ryzyka konfliktów wersji,
x86/x64 itp.

Algorytm:
1. Zbierz wszystkie `*.dll` z podanego katalogu i zarejestruj jako `MetadataReference`. Dodatkowo
   dołącz biblioteki runtime'u .NET z listy TPA
   (`AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")`) — bez tego Roslyn nie rozwiązuje
   `CaptionAttribute` / `DescriptionAttribute` i `ConstructorArguments` zwraca pustą tablicę,
   przez co `Tytuł`/`Opis` zostają puste.
2. Zbuduj `CSharpCompilation` z tymi referencjami.
3. Przejdź rekurencyjnie po `IAssemblySymbol.GlobalNamespace` każdej referencji.
4. Wybierz wszystkie publiczne klasy top-level o nazwie kończącej się na `Module`,
   które dziedziczą z `Soneta.Business.Module` (sprawdzane po `BaseType`).
   Filtr eliminuje "śmieci" w stylu `System.Reflection.RuntimeModule`.
5. Dla każdego modułu:
   - znajdź zagnieżdżone klasy o nazwie kończącej się na `Row` (`module.GetTypeMembers()`);
   - `RowType` = nazwa klasy bez sufiksu `Row` (np. `DokumentHandlowyRow` → `DokumentHandlowy`);
   - `TableType` = nazwa typu property `Table` w klasie `*Row` (przeszukiwany wraz z dziedziczeniem
     przez `FindMemberInherited`);
   - `Tytuł` = `CaptionAttribute`, `Opis` = `DescriptionAttribute` z klasy `*Table` zagnieżdżonej
     w tym samym module (np. `HandelModule.DokumentHandlowyTable`). Atrybuty są deklarowane
     w l.mn. („Dokumenty handlowe"), bo opisują tabelę. Fallback: jeśli klasy `*Table` brak
     lub nie ma atrybutu, czytane są te same atrybuty z klasy `*Row`. Wartością jest pierwszy
     parametr `string` konstruktora atrybutu.
   - `Guided` — rozróżnia trzy stany:
     - `root` — klasa `*Table` dziedziczy (bezpośrednio lub pośrednio) z `GuidedTable`
       albo `ExportedTable`. Tabele te są **korzeniami drzewa obiektów** — stanowią root
       paczki danych (`Datapack`/`GuidedRow`/`ExportedRow`) i to one są obsługiwane
       przez mechanizm synchronizacji i eksportu/importu.
     - `child: Pole→TypRow` — tabela jest częścią drzewa innego rootu; pole rekordu
       z atrybutem `[ColumnInfo(GuidedRelation=…)]` wskazuje na tabelę nadrzędną.
       `Pole` to nazwa pola w `*Record`, `TypRow` to konkretny typ `*Row` odczytany
       z odpowiadającej property w klasie `*Row` (w `*Record` pole ma zwykle typ `IRow`).
     - pusta wartość — tabela szczegółowa (subrow, info-row) niewchodząca w skład żadnego
       drzewa guided.
   - `Konfig` = `konfig`, gdy `*Table` ma `[TableInfo(IsConfig=true)]`. Tabele konfiguracyjne
     żyją w osobnej sesji (`ExecuteConfig`) i mają inne reguły zapisu niż tabele operacyjne.
   - `Interfaces` = lista nazw interfejsów zadeklarowanych w `[TableInfo(Interfaces = new[] { … })]`.
     Soneta używa ich jako **relacji interfejsowych** — pole typu `IXxx` może referować rekord
     z dowolnej tabeli deklarującej `IXxx` w swoim `TableInfo`.
   - Dla samego modułu (`*Module`) Tytuł/Opis czytane są analogicznie z atrybutów na klasie modułu.
6. Wypisz markdown: sekcja `##` per moduł (z jego `Caption`/`Description` jeśli są), w każdej
   sekcji tabela `RowType | TableType | Guided | Konfig | Interfaces | Tytuł | Opis`.

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
dotnet script scripts/scan-modules.csx \
    -- <KatalogDll>
```

### Przykład

```bash
dotnet script scripts/scan-modules.csx \
    -- ./bin/Debug/net10.0
```

### Przykładowe wyjście

```markdown
# Moduły i tabele (Soneta)

Znaleziono modułów: 37

## `Soneta.Handel.HandelModule`

- Opis: Moduł handlowy obsługujący dokumenty sprzedaży, zakupu, zamówień i innych operacji handlowych...
- Tabel: 62

| RowType | TableType | Guided | Konfig | Interfaces | Tytuł | Opis |
|---------|-----------|--------|--------|------------|-------|------|
| DefDokHandlowego | DefDokHandlowych | root | konfig |  | Definicje dokumentów handlowych | Konfigurowalna definicja (szablon) dokumentu handlowego... |
| DefRelacjiHandlowej | DefRelHandlowych | root | konfig |  | Definicje relacji handlowych | Konfigurowalna definicja relacji między dokumentami handlowymi... |
| DokumentHandlowy | DokHandlowe | root |  | IDokument, IKontrahentRef | Dokumenty handlowe | Główna tabela dokumentów handlowych (faktury, paragony, zamówienia, korekty, umowy itp.)... |
| DokumentHandlowyKoszt | DokHandloweKoszt | child: Dokument→DokumentHandlowy |  |  | Koszty dodatkowe | Koszt dodatkowy przypisany do dokumentu handlowego... |
| DrukarkaFiskalna | DrukarkiFiskalne | root | konfig |  | Lista drukarek fiskalnych | Konfiguracja drukarki fiskalnej... |
| ...  | ... | ... | ... | ... | ... | ... |

_Łącznie tabel: 1196_
```

## Kody wyjścia

| Kod | Znaczenie |
|-----|-----------|
| `0` | OK — wypisano listę modułów i tabel |
| `1` | Błąd argumentów / nie istnieje katalog / brak DLL |

## Ograniczenia

- Skanuje tylko górny poziom katalogu (`SearchOption.TopDirectoryOnly`) — jeśli DLL są
  rozproszone, skopiuj je do jednego katalogu.
- `TableType` dla abstrakcyjnych klas `*Row` (subrowy, klasy bazowe) jest często równy `Table` —
  to znaczy, że property `Table` pochodzi z klasy bazowej `Soneta.Business.Row` i zwraca ogólny
  typ `Soneta.Business.Table`, a klasa `*Row` nie ma własnej, dedykowanej tabeli.
- Pierwsze uruchomienie pobiera pakiet NuGet `Microsoft.CodeAnalysis.CSharp` — wymaga
  połączenia internetowego (kolejne odpalenia działają offline).

## Typowy workflow

1. **Wstępna inwentaryzacja** — uruchom `scan-modules.csx`, żeby zobaczyć pełną listę
   `RowType`/`TableType`.
2. **Drążenie szczegółów** — dla wybranego `RowType` (np. `DokumentHandlowy`) uruchom
   [scan-props.csx](./scan-props.md) i odczytaj listę pól bazodanowych oraz właściwości
   kalkulowanych klasy biznesowej.
3. **Generowanie kodu / form.xml / warunków** — użyj odczytanych nazw i typów do budowania
   wyrażeń bindujących, warunków filtrujących, kodu workerów lub Datapacków.

## Powiązania

- Dane wygenerowane: [`../data/props/INDEX.md`](../data/props/INDEX.md) — router modułów;
  tabele danego modułu w `../data/props/<Moduł>/INDEX.md` (`Tytuł`/`Konfig`/`Guided`/
  `Interfaces` + link do pól).
- [scan-props.md](./scan-props.md) — kontrakt pól pojedynczej tabeli; dane wygenerowane w
  [`../data/props/`](../data/props/) (plik `<Moduł>/<RowType>.md` na tabelę).
- Patrz skill [business-xml](../../business-xml/SKILL.md) — definicje schematu z których `BusinessGenerator`
  produkuje klasy `*Module`, `*Row`, `*Table` i `*Record`.
