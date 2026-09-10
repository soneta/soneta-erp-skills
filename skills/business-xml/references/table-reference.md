# Table Reference - Kompletna dokumentacja

## Spis treści

1. [Atrybuty table](#atrybuty-table)
2. [Atrybuty col](#atrybuty-col)
3. [Element key](#element-key)
4. [Element verifier](#element-verifier)
5. [Element attribute](#element-attribute)

> ⚠ **`description` i `caption` zawsze w JEDNEJ linii XML.** Generator wstawia wartość atrybutu
> dosłownie do stałej C# `[Description("…")]` — wartość zawinięta na kilka linii daje
> `error CS1010: Newline in constant` i kaskadę błędów kompilacji. Skracaj treść, nie łam linii.
> Dotyczy wszystkich `description`/`caption` (module, table, col).

---

## Atrybuty table

| Atrybut | Wymagany | Typ | Opis |
|---------|----------|-----|------|
| `name` | ✓ | string | Nazwa klasy C# (PascalCase, l.poj.) |
| `tablename` | ✓ | string | Nazwa tabeli w bazie (PascalCase, l.mn.). **Maks. 16 znaków** (patrz niżej) |
| `description` | | string | Krótki (2–3 zdania) opis **zastosowania** tabeli — patrz niżej |
| `guided` | | string | `Root` = główna tabela (dokument, kartoteka) |
| `config` | | boolean | `true` = tabela konfiguracyjna (wdrożeniowa) |
| `caption` | | string | Etykieta pojedynczego rekordu |
| `tablecaption` | | string | Etykieta listy rekordów |
| `namespace` | | string | Nadpisuje namespace z modułu |
| `name8` | | string | **Obsolete** — nie stosować (pozostałość legacy) |
| `cached` | | boolean | `true` = cache'owanie w pamięci |
| `timestamp` | | boolean | `true` = automatyczne pole timestamp |
| `optimisticlocking` | | boolean | `true` = optymistyczne blokowanie |
| `lock` | | string | Tryb blokowania |
| `warnings` | | string | `Off` = wyłącza ostrzeżenia |

### Atrybut `tablename` — limit 16 znaków

`tablename` może mieć **maksymalnie 16 znaków**. To nie jest wskazówka stylistyczna — to twardy
limit SQL, który **nie ujawnia się przy kompilacji**, tylko przy pierwszej transakcji zapisu na
tej tabeli w runtime: `String or binary data would be truncated`. Przyczyna: kolumna
`ChangeInfos.SourceTable` (dziennik zmian/audyt, uruchamiany dla praktycznie **każdej** tabeli
`guided="Root"`/`guided="Exported"` przy każdym zapisie) ma `length="16"` — dłuższa nazwa tabeli
zwyczajnie się w niej nie mieści. Ten sam limit (`length="16"`) występuje też na kolumnach
referencyjnych do nazw tabel w Cechach, `LockInfos.RecordTable` i `Attachments`, więc problem
dotyczy każdej tabeli, nie tylko tych uczestniczących w relacjach interfejsowych.

**Nic tego nie sprawdza za Ciebie.** Dla dodatków budowanych przez Soneta SDK nie ma żadnej
walidacji długości `tablename` — ani w XSD (zwykły `xs:string`), ani w generatorze. Limit trzeba
pilnować ręcznie przy każdej nowej tabeli. Dłuższe nazwy skraca się, ale **skrócona forma nadal
musi być w liczbie mnogiej** (to wciąż nazwa tabeli/kolekcji), np. `DokumentyHandlowe` →
`tablename="DokHandlowe"`, `PozycjeDokumentow` → `tablename="PozDokumentow"`. Nazwa klasy C#
(`name`, l. poj.) nie ma tego ograniczenia.

`tablename` musi być też **globalnie unikalny w bazie danych** — służy jako identyfikator tabeli
w relacjach interfejsowych, więc uważaj na kolizje z tabelami modułów platformy
(katalog: [modules-catalog.md](modules-catalog.md)).

### Atrybut `description` — opis zastosowania tabeli

Krótki, **dwu- trzyzdaniowy** opis tego, po co tabela powstała. Pozwala szybko zorientować się
w strukturze programu (również modelom językowym analizującym schemat). Opisuje przeznaczenie
tabeli, nie pojedyncze pola; dla tabel szczegółów warto wskazać tabelę nadrzędną.

```xml
<table name="Zgloszenie" tablename="Zgloszenia" guided="Root"
       description="Zgłoszenie serwisowe od klienta. Rejestruje reklamacje, naprawy i przeglądy wraz z opisem i datą przyjęcia.">
```

> Wartość `description` musi być w **jednej linii** (patrz ostrzeżenie na górze dokumentu) —
> nawet długi opis nie może być zawinięty w XML.

### Rodzaje tabel

**`guided="Root"`** - Główne tabele programu:
- Dokumenty, kartoteki (towar, pracownik, kontrahent)
- Dostępne z menu głównego

**`guided="Exported"`** - Tabele eksportowalne:
- Jak Root, ale z możliwością eksportu do systemów zewnętrznych
- Najważniejsze tabele transakcyjne (DokumentHandlowy, Platnosc)
- Używaj dla dokumentów wymagających integracji

> Semantykę paczek danych/eksportu (Datapack, ExportedRow) opisuje skill [programming](../../programming/SKILL.md)
> (datapack-guidedrow.md).

**Bez `guided`** - Tabele szczegółów:
- Pozycje dokumentu, kody towaru, adresy
- Muszą mieć dokładnie jedną relację `relguided="inner"`

**`config="true"`** - Tabele konfiguracyjne:
- Dane wdrożeniowe: definicje, słowniki, ustawienia
- Konfiguracja algorytmów, formularzy, wydruków

**Bez `config`** - Tabele operacyjne:
- Dane zbierane podczas pracy: dokumenty, transakcje

> **Miejsce tabeli w drzewie uprawnień.** Tabela bez relacji praw (`relright="true"`) i bez
> `relguided` jest korzeniem drzewa praw — wymaga wpisu w pliku `*.rightstree.xml`, inaczej jej
> uprawnienia trafią do gałęzi „Dodatki". `config="true"` decyduje o gałęzi Konfiguracja zamiast
> Program. Szczegóły: [rights-tree.md](rights-tree.md).

### Przykład table z wszystkimi atrybutami

```xml
<table name="Towar" 
       tablename="Towary" 
       guided="Root" 
       config="false"
       caption="Towar" 
       tablecaption="Towary"
       description="Kartoteka towarów i usług. Podstawa dokumentów handlowych i magazynowych."
       cached="false"
       timestamp="false"
       optimisticlocking="true">
  <!-- zawartość -->
</table>
```

---

## Atrybuty col

### Podstawowe

| Atrybut | Wymagany | Typ | Opis |
|---------|----------|-----|------|
| `name` | ✓ | string | Nazwa właściwości C# |
| `type` | ✓ | string | Typ danych (patrz typy) |
| `length` | dla string | uint/max | Długość tekstu lub `"max"` dla nieograniczonej |
| `required` | | RequiredType | `true`/`false`/`noverified` |
| `readonly` | | ReadonlyType | `true`/`false`/`set` |
| `caption` | | string | Etykieta pola w UI |
| `description` | | string | Jedno/dwuzdaniowy opis pola (tooltip) |
| `category` | | string | Kategoria w edytorze właściwości |

#### Kolizje nazw kolumn z członkami klasy `Row`

Kolumna o nazwie pokrywającej się z publicznym członkiem bazowej klasy wiersza generuje property
**przykrywającą** member odziedziczony — ostrzeżenie **CS0108** i mylący dostęp (raz pole, raz
mechanizm platformy, zależnie od typu referencji). Publiczne property bazowej klasy wiersza,
których **nie wolno użyć jako nazwy kolumny**:

`AccessRight`, `Caption`, `Features`, `ID`, `IsLive`, `ReadOnlyFlag`, `Session`, `Stamp`,
`State`, **`Status`**, `Table`, `TouchCounter` (+ `Guid` w wierszach guidowanych).

Najczęstszy realny przypadek to kolumna **`Status`**. Rozwiązanie: prefiks domenowy —
`UsageStatus`, `DocumentStatus` zamiast `Status`. Reguła praktyczna: przy „ogólnych" nazwach
kolumn (Status, State, Caption) zawsze prefiksuj nazwą domeny.

Checklista:
- [ ] żadna kolumna nie nazywa się jak publiczny członek `Row` (lista wyżej)
- [ ] build modułu bez ostrzeżeń CS0108

(Analogiczna pułapka dla nazw **tabel**: ostatni segment `namespace` ≠ `name` tabeli —
zob. zasady krytyczne w [SKILL.md](../SKILL.md).)

### Ograniczenia typów

- **`text`** i **`binary`** - nie mogą być kluczami (`keyprimary`, `keyunique`)
- **`string length="max"`** - tekst bez ograniczenia, wczytywany z rekordem
- **`text`** - wczytywany na żądanie (osobne zapytanie SQL)

### Modyfikatory

| Atrybut | Typ | Opis |
|---------|-----|------|
| `modifier` | string | Modyfikator C#: `public virtual`, `protected`, `internal` |
| `important` | boolean | `true` = pole wyświetlane na liście |
| `selector` | boolean | `true` = pole selector typu (patrz niżej) |
| `batchfield` | boolean | `false` = pomijane przy batch operations |
| `fulltext` | boolean | `true` = indeksowanie pełnotekstowe |
| `specialaccess` | boolean | `true` = specjalne uprawnienia |

#### Modyfikator widoczności property (`modifier`)

`modifier` steruje modyfikatorami C# generowanego property w klasie obiektu biznesowego. Pozwala
świadomie kontrolować, kto i jak korzysta z pola:

| Wartość | Efekt | Kiedy stosować |
|---------|-------|----------------|
| `public virtual` | property można **nadpisać** (`override`) w klasie obiektu biznesowego | **zalecany wzorzec projektowy** — gdy chcesz opakować odczyt/zapis pola własną logiką, zachowując pełną kontrolę tylko nad tym dziedziczonym property |
| `internal` | dostęp **tylko wewnątrz dodatku** (assembly) | gdy pole nie powinno być widoczne dla obcego kodu / innych dodatków |
| `protected` | dostęp z klasy i jej podtypów | pola pomocnicze, ustawiane wyłącznie w logice obiektu |

```xml
<col name="Krotnosc" type="int" modifier="public virtual"/>
<col name="KluczWewnetrzny" type="string" length="40" modifier="internal"/>
```

`public virtual` umożliwia w klasie C# nadpisanie generowanego property:

```csharp
public class DefinicjaCyklu : HarmonogramModule.DefinicjaCykluRow {
    public override int Krotnosc {
        get => base.Krotnosc;
        set => base.Krotnosc = value < 1 ? 1 : value;   // własna walidacja przy zapisie
    }
}
```

> Nadpisywanie generowanego property (`public virtual` → `override`) opisuje też [row-types.md](../../programming/references/row-types.md). Ograniczanie widoczności między projektami dodatku
> (`internal` + `InternalsVisibleTo`) — patrz [new-addon-cli.md](../../programming/references/new-addon-cli.md).

#### Pole selector (`selector="true"`)

Selector pozwala przechowywać **wiele typów obiektów w jednej tabeli** — jego wartość
decyduje, którą klasę C# ORM zbuduje dla danego wiersza. Jest to pole typu **`int`**,
najczęściej zadeklarowane jako **enum** (bardziej opisowy, zalecany), choć dopuszczalny jest
też zwykły `int`. Typowa deklaracja:

```xml
<col name="Typ" type="TypZgloszenia" selector="true" readonly="true" required="true"/>
```

- `type` = enum lub `int` (dla enum'a — wartości w C# z jawnymi numerami),
- `readonly="true"` — typ ustala się przy tworzeniu obiektu i nie zmienia,
- `required="true"` — obiekt musi mieć określony typ.

**Enum dyskryminatora numeruj od 1** (`Pierwszy=1, Drugi=2, …`) — wartość `0` jest traktowana
jako „puste" (mechanizm `required` + `default(T)` opisany w zasadach krytycznych
[SKILL.md](../SKILL.md)). Skutki `0` w selectorze są jednak cięższe niż zwykły
`RequiredException`: wiersz z selectorem `0` (np. wprowadzony importem XML bez elementu kolumny
selectora) **zatruwa całą tabelę** — każda materializacja dowolnego wiersza kończy się
`UnrecognizedRowException` („Selektor 0 w tabeli X nieznaleziony"), a naprawa wymaga usunięcia
wierszy wprost w SQL (`DELETE FROM Tabela WHERE KolumnaSelectora = 0`). Reguły importu wierszy
tabel z selectorem (atrybut `class`, obowiązkowy element selectora) opisuje
[import-export-xml](../../config/references/import-export-xml.md), sekcja *Wiersze tabel z selektorem*.

Checklista selectora:
- [ ] enum zaczyna się od 1 (`0` niezdefiniowane albo jawnie „puste")
- [ ] pliki dbinit/demo z wierszami tej tabeli zawierają element kolumny selectora w każdym wierszu

Po stronie C# klasa obiektu biznesowego jest `abstract`, a warianty to podtypy rejestrowane
atrybutem `[BusinessRow]`; pozycje menu „Nowy" wyznacza `[NewRow]`. Pełny wzorzec:
[generated-classes.md](generated-classes.md). Wzorzec selektora po stronie kodu opisuje też
[row-types.md](../../programming/references/row-types.md).

> ⚠ **`selector="true"` ≠ „pole enum, po którym filtruję/wyświetlam".** To rozróżnienie
> decyduje, czy tabela w ogóle się wczyta. Selector oznacza **dyskryminator polimorficzny**:
> ta sama tabela SQL przechowuje różne typy biznesowe, każdy jako osobna klasa `abstract`+podtyp
> zarejestrowany `[assembly: BusinessRow(typeof(Podtyp), WartośćEnum)]` (patrz krok 3 w
> [generated-classes.md](generated-classes.md)). Jeśli tabela ma **jedną** klasę C# dla
> wszystkich wierszy — pole typu enum takie jak `StatusFaktury`, `RecipientType`, `TypWpisu`,
> `Kategoria` **nie dostaje `selector="true"`**, nawet jeśli jest `important="true"` i steruje
> wyświetlaniem. Błędne dodanie selectora do zwykłego pola enum kompiluje się bez ostrzeżeń,
> ale przy pierwszym odczycie listy rzuca w runtime `Nierozpoznany typ wiersza. Selektor N
> w tabeli X nieznaleziony.` — bo ORM szuka klasy zarejestrowanej dla tej wartości, a żadna
> nie istnieje (jest jedna zwykła klasa Row dla całej tabeli).

### Relacje

| Atrybut | Typ | Opis |
|---------|-----|------|
| `children` | string | Nazwa kolekcji dzieci w obiekcie nadrzędnym |
| `relname` | string | Opis relacji |
| `relguided` | string | `inner` = nawigacja wewnętrzna |
| `relright` | boolean | `true` = prawa do relacji |
| `reldefault` | boolean | `true` = domyślna relacja |
| `delete` | string | Akcja przy usuwaniu: `cascade`, `setnull` |
| `setonlynull` | boolean | `true` = można ustawić tylko raz |

### Indeksy (inline w col)

| Atrybut | Typ | Opis |
|---------|-----|------|
| `keyprimary` | boolean | `true` = część klucza głównego |
| `keyunique` | boolean | `true` = wartość unikalna |
| `keyclass` | string | Klasa indeksu: `History`, `Lp` |
| `keyclasscol` | string | Kolumna dla keyclass |

### Lokalizacja

| Atrybut | Typ | Opis |
|---------|-----|------|
| `localization` | LocalizationType | `none`/`dictionary`/`db` |
| `name12` | string | Skrócona nazwa kolumny (max 12 zn.) |
| `cstype` | string | Nadpisanie typu C# |

---

## RequiredType - wartości

- `true` - pole wymagane, walidowane
- `false` - pole opcjonalne
- `noverified` - pole wymagane (kolumna SQL), ale generator **nie dodaje** sprawdzenia w setterze —
  żadna wartość (łącznie z domyślną) nie rzuca wyjątku

### ⚠ `required="true"` traktuje wartość domyślną typu jako „puste" — dla KAŻDEGO typu wartościowego

To najczęstsza pułapka tego atrybutu i **nie dotyczy tylko `int`**. Generator, tworząc setter dla
`required="true"`, porównuje przypisywaną wartość z `default(T)` tego typu i rzuca
`RequiredException`, jeśli są równe — dokładnie tak samo, jak dla `null` w typach referencyjnych:

| Typ kolumny | Wartość traktowana jako „puste" (rzuca `RequiredException`) |
|---|---|
| `int`, `double`, `decimal`, `currency`, `doublecy` | `0` |
| `guid` | `Guid.Empty` |
| enum | wartość o numerze `0` (niezależnie od tego, czy w C# ma w ogóle nazwę) |
| `date` / `datetime` | `Date.MinValue` / `DateTime.MinValue` |
| `time` | `Time.Zero` |
| `percent`, typy ilościowe | odpowiednik `Zero`/`Empty` danego typu |
| `string` | `""` **lub** `null` (`string.IsNullOrEmpty`) |
| referencja do wiersza | `null` (jedyny przypadek zgodny z intuicją „required = nie-null") |

**Zasada:** stosuj `required="true"` tylko wtedy, gdy wartość domyślna typu jest **semantycznie
niedopuszczalna** w domenie tego pola — np. `MaxAttempts` (0 maksymalnych prób nie ma sensu),
`Lp` (pozycja 0 nie istnieje), `Ilosc` na pozycji dokumentu (0 sztuk to brak pozycji). Dla pól,
w których wartość domyślna typu jest poprawnym, spodziewanym stanem — liczniki i progresje od
zera (`AttemptCount`, `Retries`), pola opcjonalne inicjalizowane na `0`/`Guid.Empty` — używaj
**`required="false"`**. Kolumna SQL pozostaje bez zmian (typ wartościowy i tak nie przechowuje
NULL) — zmienia się wyłącznie walidacja C# w setterze.

Objaw błędnego `required="true"` w runtime: `Wymagane jest wprowadzenie wartości pola 'X'`
rzucane **przy tworzeniu obiektu**, gdy kod inicjalizuje pole wartością domyślną (np.
`AttemptCount = 0;` w `OnAdded()`) — build przechodzi bez ostrzeżeń, błąd wychodzi dopiero przy
pierwszym uruchomieniu tej ścieżki kodu.

## ReadonlyType - wartości

- `true` — property **tylko z getterem**; wartość ustawialna wyłącznie w konstruktorze (generator
  dodaje konstruktor z parametrem). Dobre dla pól ustalanych raz przy tworzeniu (np. relacja
  nadrzędna `relguided="inner"`, selector). **Błędne** dla pól wyliczanych/agregatów
  aktualizowanych z kodu po utworzeniu obiektu.
- `false` — edytowalne (getter + setter).
- `set` — jak `true`, ale dodatkowo generuje `protected` property `base<Pole>` **tylko z setterem**,
  pozwalające ustawić wartość z kodu obiektu biznesowego. Używaj dla pól liczonych z kodu
  (sumy pozycji, statusy wyliczane).

> Konsekwencje dla konstruktorów klas i przykłady — [generated-classes.md](generated-classes.md).

---

## Element key

Definiuje indeks na tabeli.

```xml
<key name="WgKodu" keyunique="true" keyprimary="true">
  <keycol name="Kod"/>
</key>

<key name="WgKontrahentaIDaty" keyunique="false">
  <keycol name="Kontrahent"/>
  <keycol name="Data"/>
</key>
```

### Atrybuty key

| Atrybut | Typ | Opis |
|---------|-----|------|
| `name` | string | Nazwa indeksu (konwencja: `Wg` + kolumny) |
| `keyunique` | boolean | `true` = indeks unikalny |
| `keyprimary` | boolean | `true` = klucz główny |
| `keyclass` | string | Klasa indeksu |
| `keyclasscol` | string | Kolumna dla klasy |
| `lock` | string | Tryb blokowania: `ExclusiveGet` |

### Element keycol

```xml
<keycol name="NazwaKolumny"/>
```

### Element keyinclude

Dodatkowe kolumny w indeksie (INCLUDE w SQL):

```xml
<key name="WgKodu">
  <keycol name="Kod"/>
  <keyinclude name="Nazwa"/>
</key>
```

---

## Element verifier

Walidator pola wywoływany przy zapisie.

```xml
<col name="Nazwa" type="string" length="100">
  <verifier name="Towar.NazwaVerifier"/>
  <verifier name="Towar.NazwaUniqueVerifier" onadded="true"/>
</col>
```

### Atrybuty verifier

| Atrybut | Wymagany | Typ | Opis |
|---------|----------|-----|------|
| `name` | ✓ | string | Pełna nazwa klasy weryfikatora |
| `onadded` | | boolean | `true` = tylko przy dodawaniu |

> `business.xml` deklaruje jedynie nazwę weryfikatora — **kod** weryfikatora (klasa dziedzicząca
> po `Verifier`/`RowVerifier<T>`/`ColVerifier<T>`, poziomy `Error`/`Warning`/`Information`,
> uzbrajanie na zmianę pola) pisze się po stronie klasy obiektu biznesowego: [verifiers.md](../../programming/references/verifiers.md). Kolumna z elementem `<verifier>` staje się
> **źródłem** uzbrajającym weryfikator; `onadded="true"` ogranicza to do dodania wiersza.

---

## Element attribute

Atrybut C# dodawany do właściwości.

```xml
<col name="KodPocztowy" type="int">
  <attribute>MaskEdit("00-000", SaveLiteral=false)</attribute>
  <attribute>Browsable(false)</attribute>
  <attribute>Dictionary("Miejscowość")</attribute>
  <attribute>Obsolete("Użyj pola X")</attribute>
  <attribute>Context</attribute>
  <attribute>Context(Required=false)</attribute>
</col>
```

### Popularne atrybuty

| Atrybut | Opis |
|---------|------|
| `Browsable(false)` | Ukrywa pole w UI |
| `Context` | Automatyczne wypełnienie kolumny relacji z kontekstu UI (patrz niżej) |
| `Context(Required=false)` | Opcjonalny kontekst |
| `Dictionary("nazwa")` | Słownik podpowiedzi |
| `MaskEdit("maska")` | Maska wprowadzania |
| `Obsolete("msg")` | Oznacza jako przestarzałe |
| `NumeratorItem` | Element numeratora |

### `Context` — automatyczne wypełnianie z kontekstu UI

`<attribute>Context</attribute>` na kolumnie relacji powoduje **automatyczne wypełnienie** pola
z kontekstu interfejsu: nowy rekord otwierany „z" kontrahenta (z jego listy lub formularza)
dostaje wypełnionego kontrahenta. Przydatne dla czynności „Nowy z…".
Wzorce relacji — [relations-guide.md](relations-guide.md).

---

## Przykład kompletnej tabeli

```xml
<table name="Faktura" tablename="Faktury" guided="Root" 
       caption="Faktura" tablecaption="Faktury">
  
  <interface>IRightsSource</interface>
  <interface>IDefinicjaDokumentuOA</interface>
  
  <key name="WgNumeru" keyunique="true" keyprimary="true">
    <keycol name="Numer"/>
  </key>
  <key name="WgKontrahenta">
    <keycol name="Kontrahent"/>
    <keycol name="Data"/>
  </key>
  
  <col name="Numer" type="string" length="30" required="true" 
       category="Ogólne" important="true"
       description="Numer dokumentu"/>
  
  <col name="Data" type="date" required="true" 
       category="Ogólne"
       description="Data wystawienia">
    <verifier name="Faktura.DataVerifier"/>
  </col>
  
  <col name="Kontrahent" type="Kontrahent" required="true"
       category="Ogólne"
       relname="Kontrahent faktury"
       children="Faktury">
    <attribute>Context</attribute>
  </col>
  
  <!-- nie "Status" — kolizja z publicznym członkiem klasy Row (CS0108); zob. „Kolizje nazw kolumn" -->
  <col name="StatusFaktury" type="StatusFaktury" 
       category="Ogólne" important="true"
       description="Status dokumentu"/>
  
  <col name="WartoscNetto" type="currency" readonly="true"
       category="Wartości" caption="Wartość netto"/>
  
  <col name="WartoscBrutto" type="currency" readonly="true"
       category="Wartości" caption="Wartość brutto"/>
  
  <col name="Uwagi" type="text" category="Dodatkowe"/>
</table>
```
