# Skanowanie pól klasy biznesowych z DLL (Roslyn MetadataReference)

Narzędzie do odczytu rzeczywistych pól bazodanowych obiektu biznesowego ze skompilowanych bibliotek dodatku 
enova365/Soneta.
Pozwala to na budowanie kodu opierającego się na tych klasach biznesowych, wyrażeń bindujących form.xml oraz
(Warunki filtrujące)[./rowconditions.md].

## Cel

W modelu Soneta klasa `Row` (np. `DokumentHandlowy`) udostępnia właściwości publiczne, które można wykorzystać w 
generowanych kodzie biznesowym, oraz do budowania wyrażeń bindujących form.xml. Natomiast w 
(warunkach filtrujących)[./rowconditions.md], można używać TYLKO pól bazodananowych udostępnianych przez to narzędzie.

Używaj tego narzędzia, gdy:
- piszesz kod operujący bezpośrednio na polach rekordu (np. w extenderze, workerze, datapack);
- chcesz zweryfikować rzeczywisty typ pola (np. `decimal?` vs `decimal`) bez czytania wygenerowanego kodu;
- pracujesz na dodatku innej osoby i nie masz dostępu do źródeł, tylko do DLL.
- generujesz warunki filtrujące (serwerowe - LINQ)
- Przygotowujesz formularze form.xml.

## Mechanizm

Skrypt używa **Roslyn** (`Microsoft.CodeAnalysis.CSharp`) i `MetadataReference.CreateFromFile`, co oznacza, że **metadane są czytane bez ładowania IL** do CLR — bezpiecznie, bez ryzyka konfliktów wersji, x86/x64 itp.

Algorytm:
1. Zbierz wszystkie `*.dll` z podanego katalogu i zarejestruj jako `MetadataReference`. Dodatkowo dołącz wszystkie biblioteki runtime'u .NET z listy TPA (`AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")`) — bez tego Roslyn nie rozwiązuje typu `System.ComponentModel.DescriptionAttribute` (i podobnych) i `ConstructorArguments` atrybutów zwraca pustą tablicę, przez co `Tytuł`/`Opis` zostają puste.
2. Zbuduj `CSharpCompilation` z tymi referencjami.
3. Przejdź rekurencyjnie po `IAssemblySymbol.GlobalNamespace` każdej referencji.
4. Znajdź pierwszy typ kończący się na `Module`, który zawiera typ zagnieżdżony o nazwie `{NazwaRekordu}Record`.
5. Odczytaj publiczne pola (`IFieldSymbol`, `DeclaredAccessibility == Public`) i ich typy → oznacz jako **bazodanowe**.
6. Znajdź publiczną klasę najwyższego poziomu o nazwie `{NazwaRekordu}` (klasę biznesową, np. `DokumentHandlowy`) i wczytaj jej publiczne, instancyjne `IPropertySymbol` (wraz z dziedziczonymi).
7. Scal listy:
   - property o nazwie unikalnej (brak takiego pola w rekordzie) → oznacz jako **kalkulowane**;
   - property o nazwie pokrywającej się z polem rekordu → zachowaj znacznik **bazodanowe**, ale podmień typ na ten z property (bo property zwykle precyzuje typ, np. zwraca konkretny enum lub `Row` zamiast `Guid`/`int`).
8. Dla każdego wpisu odczytaj `Tytuł` i `Opis` — pierwszy parametr `string` konstruktora atrybutu. Matching nazwy atrybutu jest dopasowywany do `Caption`/`CaptionAttribute` oraz `Description`/`DescriptionAttribute` (np. `System.ComponentModel.DescriptionAttribute` używany w generowanym kodzie Soneta). Kolejność źródeł:
   1. property klasy biznesowej (`{NazwaRekordu}`) — z uwzględnieniem dziedziczenia (atrybut może być na property bazowej klasy, np. `{NazwaRekordu}Row`);
   2. pole rekordu (`{NazwaModulu}+{NazwaRekordu}Record`);
   3. odpowiadający member w typie zagnieżdżonym `{NazwaModulu}+{NazwaRekordu}Row` (fallback — przeszukiwany wraz z klasami bazowymi). To tam Soneta zwykle deklaruje `[Description("...")]` na publicznych property delegujących do pola rekordu.
9. **Rekurencja po subrowach** — jeśli któreś z pól rekordu ma typ kończący się na `Record` (np. `CoreModule.DefinicjaNumeracjiRecord Numeracja`), traktuj je jako subrow:
   - na bazie nazwy typu wylicz nazwę bazową (`DefinicjaNumeracjiRecord` → `DefinicjaNumeracji`);
   - znajdź klasę biznesową (`DefinicjaNumeracji`) oraz typ `*Module+DefinicjaNumeracjiRow` (mogą być w innym module — np. `CoreModule`);
   - powtórz całą procedurę (kroki 5–8) dla tego rekordu, używając prefiksu `Numeracja.` w kluczach wyników (`Numeracja.Pole1`, `Numeracja.Pole2`, …).
   Rekurencja działa dowolnie głęboko (subrow w subrowie). Pętle (rekord zawierający siebie pośrednio) są zabezpieczone przez zbiór odwiedzonych typów.
10. **Metadane tabeli** — dodatkowo do nagłówka trafiają:
    - `Tabela konfiguracyjna: Tak/Nie` — czytane z `[TableInfo(IsConfig=true)]` na zagnieżdżonej
      klasie `*Module.*Table` (atrybut siedzi tam, nie na top-levelowym typie zwracanym przez
      property `Table` w `*Row`).
    - `Guided: root` — gdy `*Table` dziedziczy z `GuidedTable`/`ExportedTable`.
    - `Guided: child — nadrzędna przez pole \`X\` → \`Y\`` — gdy w rekordzie istnieje pole
      z `[ColumnInfo(GuidedRelation=…)]` wskazujące tabelę nadrzędną w drzewie obiektów.
    - `Implementuje interfejsy: …` — lista interfejsów z `[TableInfo(Interfaces=…)]` tej tabeli.
11. **Relacje interfejsowe** — skrypt buduje globalny indeks `interfejs → lista tabel implementujących`
    (iteracja po wszystkich `*Module.*Table` we wszystkich referencjach). Dla każdego pola, którego
    typ jest interfejsem występującym w tym indeksie (heurystyka: nazwa zaczyna się od `I` + wielka
    litera), kolumna `Rodzaj` dostaje znacznik `iface-ref`, a po głównej tabeli pól wypisywana
    jest sekcja `## Relacje interfejsowe` z listą `Pole | Interfejs | Tabele implementujące`.
    Pozwala to od razu zobaczyć alternatywy, do których pole może wskazywać.
12. **Znacznik `guided-parent`** — pole rekordu z atrybutem `[ColumnInfo(GuidedRelation=…)]`
    dostaje w kolumnie `Rodzaj` dodatkowy tag `guided-parent`, sygnalizując, że to ono trzyma
    referencję do rootu drzewa.
13. Wypisz tabelę markdown na stdout (kolumny: `Pole | Typ | Rodzaj | Tytuł | Opis`).

## Wymagania

- .NET SDK (8.0+)
- `dotnet-script`:
  ```bash
  dotnet tool install -g dotnet-script
  ```

## Uruchomienie

```bash
dotnet script ~/.claude/skills/soneta-programming/scripts/scan-props.csx \
    -- <NazwaRow> <KatalogDll>
```

### Przykład

```bash
dotnet script ~/.claude/skills/soneta-programming/scripts/scan-props.csx \
    -- DokumentHandlowy ./bin/Debug/net8.0
```

### Przykładowe wyjście

```markdown
# Pola i właściwości klasy biznesowej: `Soneta.Handel.DokumentHandlowy`
Nazwa tabeli: `DokHandlowe`
Tabela konfiguracyjna: Nie
Guided: root
Implementuje interfejsy: `IDokument`, `IKontrahentRef`

- pola bazodanowe: 128
- pola kalkulowane (z klas biznesowych): 388

| Pole | Typ | Rodzaj | Tytuł | Opis |
|------|-----|--------|-------|------|
| Brutto | `decimal` | bazodanowe | Brutto | Wartość brutto dokumentu |
| DataDokumentu | `System.DateTime` | bazodanowe | Data dokumentu |  |
| Kontrahent | `Soneta.Kontrahenci.Kontrahent` | bazodanowe, iface-ref | Kontrahent |  |
| Netto | `decimal` | bazodanowe | Netto |  |
| Numer | `string` | bazodanowe | Numer |  |
| SaldoWaluta | `decimal` |  | Saldo w walucie |  |
| ...  | ... | ... | ... | ... |

## Relacje interfejsowe

Pola, których typ jest interfejsem zadeklarowanym w `[TableInfo(Interfaces=...)]` innych tabel.
Pole może wskazywać na rekord dowolnej z poniższych tabel.

| Pole | Interfejs | Tabele implementujące |
|------|-----------|------------------------|
| Kontrahent | `IKontrahent` | `Kontrahent`, `Pracownik`, `Urzad` |
```

Kolumna `Rodzaj` jest kombinacją znaczników rozdzielonych przecinkami:
- `bazodanowe` — pole rekordu (`*Record`); brak znacznika = property kalkulowana klasy biznesowej.
- `guided-parent` — pole z `[ColumnInfo(GuidedRelation=…)]` trzymające referencję do nadrzędnej
  tabeli w drzewie obiektów guided.
- `iface-ref` — typ pola jest interfejsem zadeklarowanym w `[TableInfo(Interfaces=…)]` innej tabeli;
  konkretne tabele docelowe są wymienione w sekcji `## Relacje interfejsowe` pod tabelą pól.

## Kody wyjścia

| Kod | Znaczenie |
|-----|-----------|
| `0` | OK — wypisano tabelę pól |
| `1` | Błąd argumentów / nie istnieje katalog / brak DLL |
| `2` | Nie znaleziono typu `*Module+{NazwaRekordu}Record` w referencjach |

## Ograniczenia

- Skanuje tylko górny poziom katalogu (`SearchOption.TopDirectoryOnly`) — jeśli DLL są rozproszone, skopiuj je do jednego katalogu.
- Zwraca pierwszy znaleziony typ pasujący do wzorca `*Module+{Nazwa}Record` — jeśli dwa moduły mają taki sam zagnieżdżony rekord, dostaniesz tylko jeden (niedeterministycznie wg kolejności assembly).
- Zwraca **publiczne pola** rekordu (`IFieldSymbol`) oraz **publiczne, instancyjne właściwości** klasy biznesowej (`IPropertySymbol`, łącznie z dziedziczonymi). Pola rekordu = źródło prawdy o schemacie DB (rodzaj `bazodanowe`); właściwości spoza rekordu = wyliczane w kodzie (rodzaj `kalkulowane`).
- Jeśli klasa biznesowa o nazwie `{NazwaRekordu}` nie zostanie znaleziona w referencjach, skrypt zwraca tylko listę pól bazodanowych (z odpowiednią adnotacją w nagłówku) i kończy się kodem `0`.
- Pierwsze uruchomienie pobiera pakiet NuGet `Microsoft.CodeAnalysis.CSharp` — wymaga połączenia internetowego (kolejne odpalenia działają offline).

## Powiązania

- Patrz [datapack-guidedrow.md](datapack-guidedrow.md) — struktury `GuidedRow` / `ExportedRow` i mechanizm Datapack operujący na polach rekordu.
- Patrz skill `soneta-business-xml` — definicja schematu, z którego `BusinessGenerator` produkuje klasę `XxxRecord`.
