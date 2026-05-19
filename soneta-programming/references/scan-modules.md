# Skanowanie modułów i tabel z DLL (Roslyn MetadataReference)

Narzędzie do wylistowania wszystkich modułów (`*Module`) platformy enova365/Soneta oraz tabel
(`*Row` / `*Table`) zdefiniowanych w każdym z nich. Czyta metadane skompilowanych bibliotek dodatku,
nie wymaga źródeł.

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
   - `Guided` = `tak`, gdy klasa `*Table` dziedziczy (bezpośrednio lub pośrednio) z `GuidedTable`
     albo `ExportedTable`. Tabele oznaczone `Guided=tak` są **rootami drzewa obiektów** —
     stanowią korzeń paczki danych (`Datapack`/`GuidedRow`/`ExportedRow`) i to one są obsługiwane
     przez mechanizm synchronizacji i eksportu/importu. Tabele bez tej flagi to elementy
     szczegółowe (subrowy, info-rowy), które są częścią paczki danej tabeli-korzenia, ale nie
     stanowią samodzielnego rootu.
   - Dla samego modułu (`*Module`) Tytuł/Opis czytane są analogicznie z atrybutów na klasie modułu.
6. Wypisz markdown: sekcja `##` per moduł (z jego `Caption`/`Description` jeśli są), w każdej
   sekcji tabela `RowType | TableType | Tytuł | Opis`.

## Wymagania

- .NET SDK (8.0+)
- `dotnet-script`:
  ```bash
  dotnet tool install -g dotnet-script
  ```

## Uruchomienie

```bash
dotnet script ~/.claude/skills/soneta-programming/scripts/scan-modules.csx \
    -- <KatalogDll>
```

### Przykład

```bash
dotnet script ~/.claude/skills/soneta-programming/scripts/scan-modules.csx \
    -- ./bin/Debug/net8.0
```

### Przykładowe wyjście

```markdown
# Moduły i tabele (Soneta)

Znaleziono modułów: 37

## `Soneta.Handel.HandelModule`

- Opis: Moduł handlowy obsługujący dokumenty sprzedaży, zakupu, zamówień i innych operacji handlowych...
- Tabel: 62

| RowType | TableType | Guided | Tytuł | Opis |
|---------|-----------|--------|-------|------|
| DefDokHandlowego | DefDokHandlowych | tak | Definicje dokumentów handlowych | Konfigurowalna definicja (szablon) dokumentu handlowego... |
| DefRelacjiHandlowej | DefRelHandlowych | tak | Definicje relacji handlowych | Konfigurowalna definicja relacji między dokumentami handlowymi... |
| DokumentHandlowy | DokHandlowe | tak | Dokumenty handlowe | Główna tabela dokumentów handlowych (faktury, paragony, zamówienia, korekty, umowy itp.)... |
| DokumentHandlowyKoszt | DokHandloweKoszt |  | Koszty dodatkowe | Koszt dodatkowy przypisany do dokumentu handlowego... |
| DrukarkaFiskalna | DrukarkiFiskalne | tak | Lista drukarek fiskalnych | Konfiguracja drukarki fiskalnej... |
| ...  | ... | ... | ... | ... |

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

- [scan-props.md](./scan-props.md) — drugi skrypt skanujący, dla pojedynczego rekordu wypisuje
  pełną listę pól (bazodanowych + kalkulowanych) wraz z `Tytuł`/`Opis` i rekurencyjnym
  rozwinięciem subrowów.
- Patrz skill `soneta-business-xml` — definicje schematu z których `BusinessGenerator`
  produkuje klasy `*Module`, `*Row`, `*Table` i `*Record`.
