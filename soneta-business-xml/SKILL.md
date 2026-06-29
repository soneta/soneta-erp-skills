---
name: soneta-business-xml
description: >
  Generator plików business.xml dla platformy Soneta (enova365, Soneta Enterprise). 
  Tworzy definicje obiektów biznesowych (tabel, kolumn, relacji, indeksów) zgodne 
  ze schematem XSD. Używaj gdy użytkownik prosi o stworzenie nowego modułu biznesowego, 
  zdefiniowanie obiektów lub encji do przechowywania w bazie danych, utworzenie relacji 
  między obiektami, lub generowanie plików business.xml dla platformy Soneta.
---

# Soneta Business XML Generator

Skill do generowania plików `business.xml` dla platform firmy Soneta:
- **Soneta** - system ERP dla firm (enova365, Soneta Enterprise)
- **Soneta Enterprise** - platforma enterprise

Pliki te definiują obiekty biznesowe (encje ORM), które platforma automatycznie mapuje na tabele w bazie danych i generuje klasy C#.

## Struktura pliku business.xml

```xml
<?xml version="1.0" encoding="utf-8" ?>
<module xmlns="http://www.enova.pl/schema/business_struct.xsd" 
        name="NazwaModulu" 
        namespace="Soneta.NazwaModulu" 
        versionName="soneta">
  
  <import>../..</import>
  <using>Soneta.Core</using>
  
  <!-- Definicje enum, subrow, interface, table -->
</module>
```

## Atrybuty modułu

| Atrybut | Wymagany | Opis |
|---------|----------|------|
| `name` | ✓ | Nazwa modułu (np. "Handel", "Kadry") |
| `namespace` | ✓ | Namespace C# (np. "Soneta.Handel") |
| `versionName` | ✓ | Zazwyczaj "soneta" |
| `versionNumber` | | Numer wersji (int) |
| `internal` | | true dla modułów wewnętrznych |

## Atrybuty table

| Atrybut | Wymagany | Opis |
|---------|----------|------|
| `name` | ✓ | Nazwa klasy C# (PascalCase, l.poj.) |
| `tablename` | ✓ | Nazwa tabeli w bazie danych (PascalCase, l.mn.). **Maks. 16 znaków** |
| `description` | | Krótki (2–3 zdania) opis zastosowania tabeli — patrz niżej |
| `guided` | | `Root` = główna tabela programu (dokument, kartoteka) |
| `config` | | `true` = tabela konfiguracyjna (tworzona podczas wdrożenia) |
| `caption` | | Etykieta pojedynczego rekordu |
| `tablecaption` | | Etykieta listy rekordów |

> **`tablename` ≤ 16 znaków** — w relacjach interfejsowych identyfikatorem tabeli jest
> 16-znakowe pole bazodanowe, dłuższe nazwy trzeba skracać **z zachowaniem liczby mnogiej**
> (np. `DokumentyHandlowe` → `DokHandlowe`). `name` (klasa C#) nie ma tego limitu.
>
> **`description`** — 2–3 zdania o przeznaczeniu tabeli, do szybkiej orientacji w strukturze
> programu (również dla modeli językowych). Opisuje, po co tabela powstała, nie pojedyncze pola.

### Rodzaje tabel

**Tabele główne (`guided="Root"`):**
- Główne obiekty biznesowe: dokumenty, kartoteki (towar, pracownik, kontrahent)
- Dostępne z menu głównego programu
- Stanowią bazę definicji obiektów biznesowych

**Tabele eksportowalne (`guided="Exported"`):**
- Jak `Root`, ale dodatkowo mogą być eksportowane do innych systemów
- Najważniejsze tabele transakcyjne: DokumentHandlowy, Platnosc, DokEwidencja
- Używaj dla dokumentów wymagających integracji z systemami zewnętrznymi

**Tabele szczegółów (bez `guided`):**
- Opisują szczegóły obiektów głównych: pozycje dokumentu, kody towaru, adresy
- Muszą mieć dokładnie jedną relację z `relguided="inner"` wskazującą na obiekt główny

**Tabele konfiguracyjne (`config="true"`):**
- Określają sposób działania programu
- Konfiguracja algorytmów, formularzy, wydruków, słowników
- Dane tworzone podczas wdrożenia systemu
- Przykłady: definicje dokumentów, jednostki miary, stawki VAT

**Tabele operacyjne (bez `config`):**
- Dane zbierane podczas codziennej pracy
- Dokumenty, kartoteki, transakcje

## Elementy wewnętrzne

### 1. Import i using

```xml
<import>../..</import>
<using>Soneta.Core</using>
<using>Soneta.CRM</using>
```

- **import** - ścieżka do katalogu z innymi plikami business.xml, do których można referować (np. typy z innych modułów)
- **using** - namespace C# dla obiektów używanych w tym business.xml (potrzebne gdy referujesz typy z innych modułów)

### 2. Enum - definicja typu wyliczeniowego

```xml
<enum name="TypTowaru"/>
<enum name="StatusDokumentu"/>
```

Enum musi być zdefiniowany w osobnym pliku C# - tutaj tylko deklaracja.

> **Jawne wartości liczbowe.** Enum używany w kolumnie bazy danych powinien mieć w C#
> **explicite przypisane numery** (`Reklamacja = 1, Naprawa = 2`). Wartość trafia do bazy,
> więc nowe pozycje dopisuje się **na końcu** — bez zmiany istniejących, by uniknąć
> renumeracji i rozjazdu z zapisanymi danymi. Dotyczy to zwłaszcza pól selector'a.

### 3. Interface - relacje polimorficzne

Interface może być implementowany przez wiele tabel. Deklaracja samego interfejsu (jego metody/właściwości) jest w kodzie C#. W business.xml deklarujemy tylko nazwę interfejsu, aby móc tworzyć **relacje interface'owe**.

```xml
<interface name="IKontrahent"/>
<interface name="IPodmiotKasowy"/>
```

> **`IRightsSource` — obiekt jako źródło praw.** Dodanie do tabeli `<interface>IRightsSource</interface>`
> czyni obiekt (zwykle konfiguracyjny, np. magazyn) **źródłem uprawnień**: operatorowi przypisuje się
> prawa do tego obiektu, co steruje dostępem do **danych operacyjnych referujących** do niego (np.
> dokumentów z danego magazynu). System sam dba o widoczność i filtrowanie list. Mechanizm po stronie
> kodu (`AccessRight`, `Login.GetObjectRight`) — patrz skill **soneta-programming**.

**Relacja interface'owa** - kolumna typu interface może wskazywać na obiekt z dowolnej tabeli implementującej ten interface. W bazie danych zapisywana jest para: `(nazwa_tabeli, ID)`.

```xml
<!-- Relacja interface'owa - może wskazywać na Osobę, Firmę lub inny obiekt implementujący IKontrahent -->
<col name="Kontrahent" type="IKontrahent" required="true"/>
```

### 4. Subrow - typ złożony (value object)

Subrow to zagnieżdżony obiekt bez własnej tabeli - przechowywany jako kolumny w tabeli rodzica.

```xml
<subrow name="Adres">
  <col name="Ulica" type="string" length="100"/>
  <col name="Miasto" type="string" length="50"/>
  <col name="KodPocztowy" type="string" length="10"/>
</subrow>

<subrow name="NumerDokumentu">
  <key name="WgSymbolu" keyunique="true">
    <keycol name="Symbol"/>
    <keycol name="Numer"/>
  </key>
  <col name="Symbol" type="string" length="50"/>
  <col name="Numer" type="int"/>
  <col name="Pelny" type="string" length="40"/>
</subrow>
```

### 5. Table - główna definicja obiektu biznesowego

Pełna dokumentacja: [references/table-reference.md](references/table-reference.md)

```xml
<table name="Towar" tablename="Towary" guided="Root" caption="Towar" tablecaption="Towary">
  <interface>IElementSlownika</interface>
  
  <key name="WgKodu" keyunique="true" keyprimary="true">
    <keycol name="Kod"/>
  </key>
  
  <col name="Kod" type="string" length="100" required="true" important="true"/>
  <col name="Nazwa" type="string" length="200" required="true"/>
  <col name="Cena" type="currency"/>
  <col name="Aktywny" type="boolean"/>
</table>
```

## Klasy biznesowe (generowane obok)

Plik `business.xml` to **połowa** definicji — druga to **klasy C#** tworzone równolegle.
Z każdej `<table name="X" tablename="Xs">` generator tworzy bazy `XRow`/`XTable`, a programista
dopisuje klasy konkretne: rekord `class X : <Moduł>Module.XRow` i tabelę
`sealed partial class Xs : <Moduł>Module.XTable`.

- **Pola `readonly`** (w tym selector) wymagają konstruktora inicjującego oraz konstruktora
  `(RowCreator creator)` dla ORM; bez pól readonly wystarcza konstruktor domyślny.
- **Selector** (`selector="true"`, pole `int`/enum) pozwala przechowywać wiele typów obiektów
  w jednej tabeli: klasa obiektu biznesowego jest `abstract`, a warianty to podtypy rejestrowane
  atrybutem `[BusinessRow]`.
- Atrybut `[NewRow]` wyznacza pozycje menu „Nowy..."; jego brak blokuje dodawanie obiektu z UI.

Pełny wzorzec (XML ↔ klasy, selector'y, konstruktory, `[BusinessRow]`, `[NewRow]`):
[references/generated-classes.md](references/generated-classes.md).

## Typy danych kolumn

### Typy proste

| Typ | Opis | Dodatkowe atrybuty |
|-----|------|-------------------|
| `string` | Tekst (wczytywany z rekordem) | `length` - wymagane, lub `"max"` dla nieograniczonego |
| `text` | Długi tekst (wczytywany na żądanie, osobne SQL) | nie może być kluczem |
| `binary` | Dane binarne | nie może być kluczem |
| `int` | Liczba całkowita | - |
| `double` | Liczba zmiennoprzecinkowa | - |
| `decimal` | Liczba z dokładnością do 2 miejsc (kwota bez waluty) | - |
| `currency` | Kwota z walutą (para: kwota + waluta) | - |
| `doublecy` | Liczba z walutą (para: liczba + waluta) | - |
| `percent` | Procent | - |
| `boolean` | Tak/Nie | - |
| `date` | Data | - |
| `time` | Czas | - |
| `datetime` | Data i czas | - |
| `FromTo` | Okres dat (para: from + to) | - |
| `guid` | Unikalny identyfikator | - |

### Typy relacyjne

| Typ | Opis | Dodatkowe atrybuty |
|-----|------|-------------------|
| `NazwaTabeli` | Relacja do innej tabeli | `children`, `delete`, `relname`, `relguided` |
| `NazwaInterface` | Relacja interface'owa (polimorficzna) | `children`, `delete`, `relname` |
| `NazwaEnum` | Typ wyliczeniowy | - |
| `NazwaSubrow` | Typ złożony (value object) | - |

### Uwagi do typów

- **`string` vs `text`**: Używaj `string` dla krótszych tekstów (wczytywane z rekordem). Używaj `text` dla długich opisów (wczytywane osobnym zapytaniem SQL).
- **`string length="max"`**: Tekst bez ograniczenia rozmiaru, ale wczytywany razem z rekordem.
- **`text` i `binary`**: Nie mogą być używane jako klucze (`keyprimary`, `keyunique`).

> **`text` vs `string length="max"` w tabelach konfiguracyjnych (cache).** Tabele konfiguracyjne
> są cache'owane — z wyjątkiem kolumn `text`, których dane **nie trafiają do cache** i są wczytywane
> osobnym zapytaniem SQL na żądanie. Dlatego:
> - **`type="text"`** ma sens tylko dla danych **bardzo dużych** i odczytywanych **sporadycznie /
>   jednokrotnie** (np. duży załącznik, log).
> - dla pól odczytywanych **wielokrotnie** (nawet jeśli bywają długie) użyj **`type="string" length="max"`**
>   — też bez limitu rozmiaru, ale wczytywane **razem z rekordem** (są w cache), co ogranicza liczbę
>   zapytań SQL. Dotyczy to np. szablonów, instrukcji, treści wstrzykiwanych przy każdym użyciu.

## Workflow tworzenia business.xml

1. **Analiza wymagań** - określ jakie obiekty i relacje są potrzebne
2. **Zdefiniuj enum'y** - typy wyliczeniowe używane w kolumnach
3. **Zdefiniuj interfejsy** - dla relacji polimorficznych (gdy kolumna może wskazywać na różne typy obiektów)
4. **Zdefiniuj subrow** - typy złożone (adresy, numery dokumentów)
5. **Zdefiniuj tabele** - główne obiekty biznesowe
6. **Dodaj relacje** - powiązania między tabelami (zwykłe i interface'owe)
7. **Dodaj indeksy** - klucze dla wyszukiwania
8. **Utwórz klasy biznesowe obok** - dla każdej tabeli klasa obiektu biznesowego i klasa tabeli;
   przy polach `readonly` konstruktory; dla tabel z selector'em - `abstract` baza, podtypy
   z `[BusinessRow]` i `[DefaultConstructor]`, pozycje `[NewRow]` (patrz [references/generated-classes.md](references/generated-classes.md))
9. **Waliduj** - sprawdź zgodność ze schematem XSD

## Szczegółowa dokumentacja

- **[references/modules-catalog.md](references/modules-catalog.md)** - katalog 34 modułów Soneta, tabele i interfejsy do relacji
- **[references/table-reference.md](references/table-reference.md)** - kompletna dokumentacja atrybutów table i col
- **[references/generated-classes.md](references/generated-classes.md)** - klasy biznesowe tworzone obok XML (Row/Table, konstruktory, selector, `[BusinessRow]`, `[NewRow]`)
- **[references/relations-guide.md](references/relations-guide.md)** - tworzenie relacji między obiektami
- **[references/examples.md](references/examples.md)** - przykłady z rzeczywistych modułów Soneta

## Konwencje nazewnicze Soneta

- **Nazwa tabeli (name)**: PascalCase, liczba pojedyncza (np. `Towar`, `DokumentHandlowy`)
- **Nazwa w bazie (tablename)**: PascalCase, liczba mnoga, **maks. 16 znaków** (np. `Towary`, `DokHandlowe`)
- **Nazwa kolumny**: PascalCase (np. `KodPocztowy`, `DataWystawienia`)
- **Klucz**: `Wg` + nazwa kolumny (np. `WgKodu`, `WgNazwy`)
- **Namespace**: `Soneta.NazwaModulu`

### Język nazewnictwa

- **Obiekty biznesowe** (domenowe) - język **polski**: `Towar`, `Faktura`, `Kontrahent`, `Pracownik`
- **Obiekty systemowe** (techniczne) - język **angielski**: `Session`, `Config`, `Cache`, `Runtime`

## Typowe wzorce

### Słownik (tabela konfiguracyjna)

Tabele `config="true"` zawierają dane konfiguracyjne tworzone podczas wdrożenia.

```xml
<table name="Jednostka" tablename="Jednostki" guided="Root" config="true"
       caption="Jednostka" tablecaption="Jednostki">
  <key name="WgKodu" keyunique="true" keyprimary="true">
    <keycol name="Kod"/>
  </key>
  <col name="Kod" type="string" length="10" required="true"
       description="Symbol jednostki używany przy wprowadzaniu ilości."/>
  <col name="Nazwa" type="string" length="80"
       description="Pełna nazwa jednostki miary."/>
  <col name="Blokada" type="boolean"
       description="Jednostka nie będzie wyświetlana na listach wyboru."/>
</table>
```

### Dokument z pozycjami (master-detail)

Tabela szczegółów (bez `guided`) musi mieć dokładnie jedną relację `relguided="inner"`.

```xml
<!-- TABELA GŁÓWNA (guided="Root") -->
<table name="Dokument" tablename="Dokumenty" guided="Root"
       caption="Dokument" tablecaption="Dokumenty">
  <key name="WgNumeru" keyunique="true" keyprimary="true">
    <keycol name="Numer"/>
  </key>
  <col name="Numer" type="string" length="30" required="true"
       description="Numer dokumentu."/>
  <col name="Data" type="date" required="true"
       description="Data wystawienia dokumentu."/>
  <col name="Kontrahent" type="Kontrahent" required="true"
       description="Kontrahent, dla którego wystawiono dokument."/>
</table>

<!-- TABELA SZCZEGÓŁÓW (bez guided, jedna relacja relguided="inner") -->
<table name="PozycjaDokumentu" tablename="PozycjeDokumentow"
       caption="Pozycja" tablecaption="Pozycje dokumentu">
  <col name="Dokument" type="Dokument" 
       required="true" readonly="true" keyprimary="true"
       children="Pozycje" delete="cascade" relguided="inner"
       description="Dokument, do którego należy pozycja."/>
  <col name="Lp" type="int" required="true" batchfield="false"
       description="Numer kolejny pozycji."/>
  <col name="Towar" type="Towar" required="true"
       description="Towar na pozycji."/>
  <col name="Ilosc" type="double" required="true"
       description="Ilość towaru."/>
  <col name="Cena" type="decimal"
       description="Cena jednostkowa."/>
</table>
```

### Historia zmian (wersjonowanie)

Typ `FromTo` przechowuje okres dat (from + to).

```xml
<table name="CenaHistoria" tablename="CenyHistoria"
       caption="Historia ceny" tablecaption="Historia cen">
  <col name="Towar" type="Towar" 
       keyprimary="true" keyclass="History" keyclasscol="Okres"
       children="HistoriaCen" delete="cascade" relguided="inner"
       description="Towar, którego dotyczy historia cen."/>
  <col name="Okres" type="FromTo" required="true"
       description="Okres obowiązywania ceny."/>
  <col name="CenaNetto" type="decimal"
       description="Cena netto w okresie."/>
  <col name="CenaBrutto" type="decimal"
       description="Cena brutto w okresie."/>
</table>
```
