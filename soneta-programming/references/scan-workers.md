# Skanowanie workerów i extenderów z DLL (Roslyn MetadataReference)

Narzędzie do wylistowania wszystkich klas `*Worker` / `*Extender` zarejestrowanych w bibliotekach
dodatków enova365 / Soneta przez atrybut `WorkerAttribute` (assembly). Czyta metadane skompilowanych
bibliotek dodatku — nie wymaga źródeł.

## Cel

W modelu Soneta workery i extendery są rejestrowane atrybutem assembly:

```csharp
[assembly: Worker<NazwaWorker, TypDanych>]   // worker przypięty do typu danych
[assembly: Worker<NazwaExtender>]            // extender (bez typu danych)
```

Skrypt wyciąga wszystkie takie rejestracje, grupuje workery wg typu danych oraz wypisuje dla każdej
klasy: parametry inicjowane z `Context`, property dostępne do bindowania/odczytu oraz pozycje
menu Czynności (metody z atrybutem `[Action]`).

Używaj tego narzędzia, gdy:
- robisz inwentaryzację rozszerzeń (workery / extendery) w dodatku innej osoby albo w całej aplikacji;
- chcesz znaleźć dostępne workery dla danego typu danych zanim napiszesz form.xml (`{Workers.<Alias>.<Property>}`);
- sprawdzasz, jakie pozycje menu Czynności są dostępne na danym obiekcie;
- weryfikujesz, że Twój nowy worker / extender został poprawnie zarejestrowany i jest widoczny dla platformy;
- przygotowujesz raport dla code review (dodatek powinien mieć spójną listę workerów / akcji).

Po komplementarne dane — patrz `scan-modules.csx` (lista tabel) i `scan-props.csx` (pola tabeli).

## Mechanizm

Skrypt używa **Roslyn** (`Microsoft.CodeAnalysis.CSharp`) i `MetadataReference.CreateFromFile`,
czyli metadane są czytane bez ładowania IL do CLR — bezpiecznie, bez ryzyka konfliktów wersji.

Algorytm:
1. Zbierz wszystkie `*.dll` z podanego katalogu, dodaj jako `MetadataReference`. Dołącz biblioteki
   runtime'u .NET (TPA — `AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")`) — bez tego Roslyn
   nie rozwiązuje atrybutów ramowych i zwraca pustą `ConstructorArguments`.
2. Zbuduj `CSharpCompilation` z tymi referencjami.
3. Dla każdego `IAssemblySymbol` przejdź po atrybutach assembly (`asm.GetAttributes()`)
   i odfiltruj te, których klasa to `WorkerAttribute` z namespace zaczynającego się od `Soneta`
   (chroni przed kolizją z atrybutami o tej samej nazwie z innych bibliotek).
4. Wyciągnij dane rejestracji w zależności od wariantu atrybutu:
   - **wariant generyczny** (`[Worker<TWorker>]`, `[Worker<TWorker, TData>]`) — typy biorę z
     `AttributeClass.TypeArguments[0]` (worker) oraz `TypeArguments[1]` (data, opcjonalnie).
     W metadanych klasa atrybutu ma backtick (`WorkerAttribute\`1`, `WorkerAttribute\`2`), ale
     `INamedTypeSymbol.Name` zwraca `"WorkerAttribute"` bez backticka — wystarczy porównanie po nazwie.
   - **wariant z parametrami** (`[Worker(typeof(TWorker), typeof(TData))]`) — typy biorę
     z `ConstructorArguments` (`TypedConstantKind.Type`). Dodatkowy string z konstruktora lub
     `NamedArgument` `Name` traktuję jako alias bindingu (nadpisuje nazwę domyślną).
5. Pogrupuj rejestracje wg `DataType` (workery) i posortuj alfabetycznie. Rejestracje bez
   `DataType` trafiają pod osobny klucz `__extenders__` w JSON-ie.
6. Dla każdej klasy workera / extendera odczytaj:
   - **Alias bindingu** (pole `name`) — jawnie podany `Name` z atrybutu albo nazwa klasy
     bez sufiksu `Worker` / pełna nazwa dla extendera. Binding na UI:
     - worker (lista): `{Workers.<name>.<Property>}`
     - extender (formularz): `{new <name>.<Property>}`
   - **Konstruktor inicjowany z Context** — parametry pierwszego publicznego konstruktora
     z parametrami trafiają do `params` z `kind: "ctor"`.
   - **Property z `[Context]`** — publiczne instancyjne property z atrybutem `ContextAttribute`,
     inicjowane z Context — trafiają do `params` bez pola `kind`.
   - **Pod-parametry typu `ContextBase`** — gdy typ parametru dziedziczy z
     `Soneta.Business.ContextBase`, wpis dostaje zagnieżdżone `props` z publicznymi property
     tej klasy (pomija property samego `ContextBase`).
   - **Property do bindowania / odczytu** (`props` workera) — pozostałe publiczne instancyjne
     property z getterem (bez `[Context]`).
   - **Akcje menu Czynności** (`actions`) — publiczne instancyjne metody z atrybutem
     `ActionAttribute`; każda akcja ma `name` (tytuł), `method` (nazwa metody), `result`
     (typ wyniku, `void` dla `void`).
7. Wypisz JSON na stdout (sformatowany, z polskimi znakami w czystej formie). Sekcje
   `params` / `actions` / `props` są pomijane, gdy są puste.

## Wymagania

- .NET SDK (8.0+)
- `dotnet-script`:
  ```bash
  dotnet tool install -g dotnet-script
  ```

## Uruchomienie

```bash
dotnet script ~/.claude/skills/soneta-programming/scripts/scan-workers.csx \
    -- <KatalogDll> [<NazwaTypuDanych>] [--related]
```

Drugi argument (opcjonalny) ogranicza wynik do workerów przypiętych do wskazanego typu danych —
pełne skanowanie bibliotek Soneta zwraca tysiące rejestracji, więc filtr jest praktycznie
niezbędny w codziennej pracy. Dopasowanie po:
- **prostej nazwie** klasy (np. `DokumentHandlowy`), albo
- **pełnej nazwie** z namespace (np. `Soneta.Handel.DokumentHandlowy`).

Gdy filtr jest podany, extendery (rejestracje bez `DataType`) są pomijane — ich nie da się
przypisać do typu danych.

### Flaga `--related` — typy powiązane

`--related` rozszerza filtr o typy powiązane z podanym typem. Pozwala jednym wywołaniem zebrać
workery z całej „rodziny" obiektu (rekord, tabela, historia), bez konieczności trzech osobnych
uruchomień. Reguły rozpoznawania powiązań (po metadanych typu):

| Typ wejściowy | Powiązany typ | Sposób odczytu |
|---|---|---|
| Klasa dziedzicząca z `Soneta.Business.Row` (np. `Pracownik`, `DokumentHandlowy`) | Klasa tabeli (`Pracownicy`, `DokHandlowe`) | property `Table` w klasie `Row` (lub klas bazowych) |
| Klasa dziedzicząca z `Soneta.Business.Table` (np. `Pracownicy`, `DokHandlowe`) | Klasa rekordu (`Pracownik`, `DokumentHandlowy`) | indekser `this[int]` — typ zwracany |
| Typ implementujący `IRowWithHistory` (np. `Pracownik`) | Typ rekordu historycznego (`PracHistoria`) | indekser `this[Soneta.Types.Date]` — typ zwracany |

Reguły działają **przechodnio** i **łącznie** — np. dla `Pracownik` (Row + IRowWithHistory)
zestaw to: `Pracownik`, `Pracownicy`, `PracHistoria`, `PracHistorie` (tabela historii dochodzi,
bo `PracHistoria` to też Row z własną `Table`).

Dodatkowo dla każdego znalezionego Row (oryginał + history-Row) skrypt **rozszerza zestaw o całą
hierarchię**:
- **klasy bazowe** (`baseClasses`) — chodzi w górę po `BaseType` aż do `object` wyłącznie
  (włącznie z generowanym `*Row` z `*Module`, frameworkowymi `Row` / `GuidedRow` / `RowBase`);
- **klasy pochodne** (`derivedClasses`) — przeszukuje wszystkie referencje w poszukiwaniu klas
  mających dany Row w łańcuchu `BaseType` (np. `OsobaWspolpracujaca`, `PracownikFirmy`,
  `Wlasciciel` dla `Pracownik`).

Tabele tego rozszerzenia nie dostają — zbędne, intermediate `*Table` rzadko bywa celem rejestracji
workera.

Informacje o znalezionych typach skrypt wypisuje **dwojako**:

1. **W JSON pod kluczem `scope`** (na stdout, sformatowane do parsowania):

   ```json
   {
     "description": "Workery przypięte do typu `Pracownik` (Soneta)",
     "scope": {
       "primary": "Soneta.Kadry.Pracownik",
       "related": [
         { "type": "Soneta.Kadry.Pracownicy",    "kind": "table" },
         { "type": "Soneta.Kadry.PracHistoria",  "kind": "history-row" },
         { "type": "Soneta.Kadry.PracHistorie",  "kind": "history-table" }
       ],
       "baseClasses": [
         "Soneta.Kadry.KadryModule.PracownikRow",
         "Soneta.Business.GuidedRow",
         "Soneta.Business.Row",
         "Soneta.Kadry.KadryModule.PracHistoriaRow"
       ],
       "derivedClasses": [
         "Soneta.Kadry.OsobaWspolpracujaca",
         "Soneta.Kadry.PracownikFirmy",
         "Soneta.Kadry.Wlasciciel"
       ]
     },
     "Soneta.Kadry.Pracownik": [ /* … */ ],
     "Soneta.Kadry.Pracownicy": [ /* … */ ]
   }
   ```

   Pole `scope` pojawia się **wyłącznie** w trybie `--related` (przy zwykłym filtrze JSON nie ma
   tego klucza). Dozwolone wartości `scope.related[].kind`: `table`, `row`, `history-row`,
   `history-table`.

2. **W logu na stderr** (`# Typ podstawowy: …`, `# Typ powiązany (kind): …`, `# Klasa bazowa: …`,
   `# Klasa pochodna: …`) — wygodne do szybkiego podglądu w konsoli.

Gdy `--related` jest podany, ale typu z `<NazwaTypuDanych>` nie da się znaleźć w referencjach
(np. literówka), skrypt loguje ostrzeżenie na stderr i wraca do prostego dopasowania po nazwie
(bez sekcji `scope`).

### Przykłady

Pełna inwentaryzacja:

```bash
dotnet script ~/.claude/skills/soneta-programming/scripts/scan-workers.csx \
    -- ./bin/Debug/net8.0
```

Tylko workery przypięte do `DokumentHandlowy`:

```bash
dotnet script ~/.claude/skills/soneta-programming/scripts/scan-workers.csx \
    -- ./bin/Debug/net8.0 DokumentHandlowy
```

### Format wyjścia: JSON

Skrypt **zawsze** wypisuje JSON na stdout — nadaje się do dalszego przetwarzania
(`jq`, skrypty, narzędzia). Markdown został usunięty, żeby utrzymać jedno, stabilne
źródło danych dla automatów i klientów.

Struktura JSON:

```json
{
  "description": "Workery przypięte do typu `DokumentHandlowy` (Soneta)",
  "Soneta.Handel.DokumentHandlowy": [
    {
      "workerAssembly": "Soneta.Zadania",
      "workerType": "Soneta.Zadania.Smsing.WyslijSmsWorker",
      "name": "WyslijSms",
      "params": [
        { "name": "ConstructorParam", "type": "Soneta.X.Y", "kind": "ctor" },
        { "name": "PropWithContextAttr", "type": "Soneta.X.Y" },
        {
          "name": "Pars",
          "type": "Soneta.X.SomeWorker.Params",
          "props": [
            { "name": "DataOd", "type": "Soneta.Types.Date" },
            { "name": "DataDo", "type": "Soneta.Types.Date" }
          ]
        }
      ],
      "actions": [
        { "name": "Wyślij SMS", "method": "WyslijSmsa", "result": "object" }
      ],
      "props": [
        { "name": "PublicPropWithoutContextAttr", "type": "Soneta.X.Y" }
      ]
    }
  ]
}
```

- Klucze top-level: `description` + jeden klucz na każdy `DataType` (pełna nazwa z namespace).
- `params` łączy parametry konstruktora (z `kind: "ctor"`) oraz property z atrybutem `[Context]`
  (bez pola `kind`) — wszystko, co Soneta inicjuje z `Context` przy tworzeniu workera.
- Gdy typ parametru **dziedziczy z `Soneta.Business.ContextBase`** (klasa parametrów workera —
  zwykle nested `Params` w klasie workera), wpis zawiera dodatkowo `props` z listą publicznych,
  instancyjnych property tej klasy. To pod-parametry, które użytkownik widzi w oknie parametrów
  workera. Property samego `ContextBase` (np. `Context`) są pomijane.
- `actions` — metody z atrybutem `[Action]`. `name` to tytuł z atrybutu, `method` to nazwa
  metody w C#, `result` to deklarowany typ wyniku (`void` dla metod bez wartości).
- `props` — publiczne, instancyjne property z getterem, bez `[Context]` — kandydaci do
  bindowania w `form.xml` przez `{Workers.<name>.<Property>}`.
- Sekcje puste (`params`/`actions`/`props`) są pomijane, żeby JSON pozostał zwięzły.
- Extendery (rejestracje bez `DataType`) trafiają — wyłącznie w trybie bez filtra typu —
  pod klucz `__extenders__`.

### Przykłady filtrowania `jq`

```bash
# Lista workerów dla typu:
jq '."Soneta.Handel.DokumentHandlowy"[] | .workerType' /tmp/out.json

# Tylko z akcjami menu Czynności:
jq '."Soneta.Handel.DokumentHandlowy"[] | select(.actions)' /tmp/out.json

# Konkretny worker po aliasie:
jq '."Soneta.Handel.DokumentHandlowy"[] | select(.name=="KSeFWyslij")' /tmp/out.json

# Workery, których parametr `Params` ma pole `Magazyn`:
jq '.[] | arrays | .[] | select((.params // [])
    | map(.props // []) | flatten | map(.name) | index("Magazyn"))' /tmp/out.json
```

## Kody wyjścia

| Kod | Znaczenie |
|-----|-----------|
| `0` | OK — wypisano listę workerów i extenderów |
| `1` | Błąd argumentów / nie istnieje katalog / brak DLL |

## Ograniczenia

- Skanuje tylko górny poziom katalogu (`SearchOption.TopDirectoryOnly`) — jeśli DLL są
  rozproszone, skopiuj je do jednego katalogu.
- Filtruje atrybut `WorkerAttribute` po nazwie i namespace `Soneta*`. Jeśli inny dodatek
  zarejestruje atrybut o tej samej nazwie w innym namespace, nie zostanie ujęty.
- Skrypt wypisuje **publiczne instancyjne** property/metody. Property prywatne lub statyczne
  są pomijane (nie biorą udziału w bindowaniu / akcjach).
- Property z modyfikatorem `internal` nie są ujęte — Soneta wymaga publicznych członków
  do bindowania UI.
- Pierwsze uruchomienie pobiera pakiet NuGet `Microsoft.CodeAnalysis.CSharp` — wymaga
  połączenia internetowego (kolejne odpalenia działają offline).

## Typowy workflow

1. **Inwentaryzacja workerów** — uruchom `scan-workers.csx`, znajdź wszystkie workery
   zarejestrowane dla interesującego Cię typu danych (np. `DokumentHandlowy`).
2. **Wybór aliasu do bindingu** — z sekcji workera odczytaj `Alias` i `Property do bindowania`
   — to bezpośrednio wartości do podstawienia w `form.xml`:
   `{Workers.<Alias>.<Property>}` (worker) lub `{new <Alias>.<Property>}` (extender).
3. **Lista akcji** — kolumna „Menu Czynności" pokazuje, które pozycje pojawią się
   w menu Czynności dla danego obiektu.
4. **Code review** — porównaj listę z oczekiwaną zawartością dodatku (wszystkie spodziewane
   rejestracje są obecne, aliasy się nie pokrywają, akcje mają komplet metod sterujących).

## Powiązania

- [worker-extender.md](./worker-extender.md) — semantyka workerów/extenderów, atrybut `[Context]`,
  `[Action]`, metody sterujące `IsVisibleXxx` / `IsEnabledXxx` / `GetNameXxx` / `IsCheckedXxx`,
  bindowanie w form.xml.
- [context.md](./context.md) — jak działa `Context` i co może być źródłem parametrów workera.
- [scan-modules.md](./scan-modules.md) — lista modułów i tabel platformy (komplementarne do scan-workers).
- [scan-props.md](./scan-props.md) — pola konkretnego rekordu (do których workery doklejają property kalkulowane).
