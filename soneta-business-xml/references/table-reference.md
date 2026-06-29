# Table Reference - Kompletna dokumentacja

## Spis treści

1. [Atrybuty table](#atrybuty-table)
2. [Atrybuty col](#atrybuty-col)
3. [Element key](#element-key)
4. [Element verifier](#element-verifier)
5. [Element attribute](#element-attribute)

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

`tablename` może mieć **maksymalnie 16 znaków**. Wynika to z relacji interfejsowych, w których
identyfikatorem wskazywanej tabeli jest 16-znakowe pole bazodanowe. Dłuższe nazwy skraca się,
ale **skrócona forma nadal musi być w liczbie mnogiej** (to wciąż nazwa tabeli/kolekcji), np.
`DokumentyHandlowe` → `tablename="DokHandlowe"`, `PozycjeDokumentow` → `tablename="PozDokumentow"`.
Nazwa klasy C# (`name`, l. poj.) nie ma tego ograniczenia.

### Atrybut `description` — opis zastosowania tabeli

Krótki, **dwu- trzyzdaniowy** opis tego, po co tabela powstała. Pozwala szybko zorientować się
w strukturze programu (również modelom językowym analizującym schemat). Opisuje przeznaczenie
tabeli, nie pojedyncze pola; dla tabel szczegółów warto wskazać tabelę nadrzędną.

```xml
<table name="Zgloszenie" tablename="Zgloszenia" guided="Root"
       description="Zgłoszenie serwisowe od klienta. Rejestruje reklamacje, naprawy
                    i przeglądy wraz z opisem i datą przyjęcia.">
```

### Rodzaje tabel

**`guided="Root"`** - Główne tabele programu:
- Dokumenty, kartoteki (towar, pracownik, kontrahent)
- Dostępne z menu głównego

**`guided="Exported"`** - Tabele eksportowalne:
- Jak Root, ale z możliwością eksportu do systemów zewnętrznych
- Najważniejsze tabele transakcyjne (DokumentHandlowy, Platnosc)
- Używaj dla dokumentów wymagających integracji

> Semantykę paczek danych/eksportu (Datapack, ExportedRow) opisuje skill `/soneta-programming`
> (datapack-guidedrow.md).

**Bez `guided`** - Tabele szczegółów:
- Pozycje dokumentu, kody towaru, adresy
- Muszą mieć dokładnie jedną relację `relguided="inner"`

**`config="true"`** - Tabele konfiguracyjne:
- Dane wdrożeniowe: definicje, słowniki, ustawienia
- Konfiguracja algorytmów, formularzy, wydruków

**Bez `config`** - Tabele operacyjne:
- Dane zbierane podczas pracy: dokumenty, transakcje

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

Po stronie C# klasa obiektu biznesowego jest `abstract`, a warianty to podtypy rejestrowane
atrybutem `[BusinessRow]`; pozycje menu „Nowy" wyznacza `[NewRow]`. Pełny wzorzec:
[generated-classes.md](generated-classes.md). Wzorzec selektora po stronie kodu opisuje też
skill `/soneta-programming` (row-types.md).

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
- `noverified` - pole wymagane, ale bez walidacji

## ReadonlyType - wartości

- `true` - tylko do odczytu
- `false` - edytowalne
- `set` - można ustawić tylko przy tworzeniu

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

> `business.xml` deklaruje jedynie nazwę weryfikatora — **kod** weryfikatora pisze się po
> stronie klasy obiektu biznesowego (skill `/soneta-programming`).

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
| `Context` | Pole kontekstowe |
| `Context(Required=false)` | Opcjonalny kontekst |
| `Dictionary("nazwa")` | Słownik podpowiedzi |
| `MaskEdit("maska")` | Maska wprowadzania |
| `Obsolete("msg")` | Oznacza jako przestarzałe |
| `NumeratorItem` | Element numeratora |

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
  
  <col name="Status" type="StatusFaktury" 
       category="Ogólne" selector="true"
       description="Status dokumentu"/>
  
  <col name="WartoscNetto" type="currency" readonly="true"
       category="Wartości" caption="Wartość netto"/>
  
  <col name="WartoscBrutto" type="currency" readonly="true"
       category="Wartości" caption="Wartość brutto"/>
  
  <col name="Uwagi" type="text" category="Dodatkowe"/>
</table>
```
