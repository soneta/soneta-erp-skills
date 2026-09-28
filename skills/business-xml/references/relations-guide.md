# Relacje między obiektami - Przewodnik

## Spis treści

1. [Typy relacji](#typy-relacji)
2. [Relacja jeden-do-wielu (master-detail)](#relacja-jeden-do-wielu) — w tym [klucz kolumny relacji i `keycol`](#klucz-kolumny-relacji-i-keycol)
3. [Relacja wiele-do-wielu](#relacja-wiele-do-wielu)
4. [Relacja do interfejsu](#relacja-do-interfejsu)
5. [Historia (wersjonowanie)](#historia-wersjonowanie)
6. [Relacja zwrotna (self-reference)](#relacja-zwrotna)
7. [Kolekcja odwrotna dla tabel z innego modułu](#kolekcja-odwrotna-dla-tabel-z-innego-modułu)
8. [Automatyczne wypełnianie z kontekstu (Context)](#automatyczne-wypełnianie-z-kontekstu-context)

---

## Typy relacji

| Typ | Opis | W bazie danych | Przykład |
|-----|------|----------------|----------|
| Jeden-do-wielu | Dokument → Pozycje | FK (ID) | Faktura ma wiele pozycji |
| Wiele-do-wielu | Przez tabelę łączącą | 2x FK | Towar ↔ Kategorie |
| Interface'owa | Polimorficzna do wielu tabel | (tabela, ID) | IKontrahent → Osoba lub Firma |
| Historia | Wersjonowanie temporalne | FK + okres | Ceny towaru w czasie |
| Zwrotna | Do tej samej tabeli | FK (ID) | Kategoria nadrzędna |

> Jak korzystać z relacji w kodzie (nawigacja, LINQ, RowCondition) opisuje skill
> `/soneta:programming` (rowcondition.md).

---

## Relacja jeden-do-wielu

### Wzorzec: Dokument z pozycjami

```xml
<!-- TABELA NADRZĘDNA (master) -->
<table name="Dokument" tablename="Dokumenty" guided="Root">
  <key name="WgNumeru" keyunique="true" keyprimary="true">
    <keycol name="Numer"/>
  </key>
  <col name="Numer" type="string" length="30" required="true"/>
  <col name="Data" type="date" required="true"/>
</table>

<!-- TABELA PODRZĘDNA (detail) -->
<table name="PozycjaDokumentu" tablename="PozycjeDok">
  <col name="Dokument" type="Dokument" 
       required="true" 
       readonly="true"
       keyprimary="true"
       children="Pozycje"
       delete="cascade"
       relguided="inner"
       relname="Pozycje dokumentu"
       description="Dokument, do którego należy pozycja"/>
  <col name="Lp" type="int" required="true" batchfield="false"/>
  <col name="Opis" type="string" length="200"/>
</table>
```

### Kluczowe atrybuty relacji podrzędnej

| Atrybut | Wartość | Znaczenie |
|---------|---------|-----------|
| `required="true"` | | Pozycja musi mieć dokument |
| `readonly="true"` | | Nie można przenosić między dokumentami |
| `keyprimary="true"` | | Klucz główny zaczyna się od dokumentu |
| `children="Pozycje"` | | Nazwa kolekcji w dokumencie: `dokument.Pozycje` |
| `delete="cascade"` | | Usunięcie dokumentu usuwa pozycje |
| `relguided="inner"` | | **Wymagane** dla tabel szczegółów (bez `guided`) |
| `relright="true"` | | Prawa dziedziczone z obiektu nadrzędnego (patrz niżej) |

### Zasada relguided="inner"

Tabele bez atrybutu `guided` (tabele szczegółów) **muszą mieć dokładnie jedną** relację z `relguided="inner"`. Wskazuje ona obiekt główny, którego szczegóły są opisywane.

### Relacja praw — `relright`

`relright` wskazuje relację, przez którą tabela **dziedziczy prawa** dostępu z obiektu
nadrzędnego. Bez niej tabela jest samodzielnym korzeniem drzewa uprawnień.

| Wartość | Znaczenie |
|---------|-----------|
| `relright="true"` | Relacja praw. Tabela dziedziczy prawa z obiektu wskazanego tą kolumną — **chyba że** wskazany obiekt jest źródłem praw obiektowych (`IRightsSource`); wtedy dostępem steruje prawo obiektowe, a tabela pozostaje samodzielna. |
| `relright="Primary"` | Jawne wskazanie relacji nadrzędnej praw; ma pierwszeństwo przed `relright="true"` i nie podlega wyjątkowi `IRightsSource`. |
| `relright="false"` | Zwykła referencja bez wpływu na prawa (wartość domyślna). |
| `relright="other"` / `relright="config"` | Warianty pomocnicze — **nie** ustanawiają dziedziczenia praw. |

Gdy tabela ma kilka relacji, nadrzędną wybiera się w kolejności: `relguided` → `relright="Primary"`
→ `reldefault="true"` → `relright="true"` (z pominięciem relacji do `IRightsSource`).

Zasady:
- **najwyżej jedna** relacja `relright="true"` w tabeli i powinna być `required="true"`
  (obiekt nadrzędny musi istnieć, inaczej wiersz zostaje bez praw);
- relacja z `relguided` powinna mieć również `relright="true"`; gdy jej brakuje, platforma
  i tak traktuje relację guided jako relację praw, ale zapisuje ostrzeżenie do logu;
- nie prowadź relacji praw z tabeli **operacyjnej** do tabeli **konfiguracyjnej** — prawa do
  danych operacyjnych opiera się wtedy o obiekt konfiguracyjny; do takiego sterowania służą
  prawa obiektowe (`IRightsSource`, skill `/soneta:programming`).

**Konsekwencja dla drzewa uprawnień:** tabela z `relright="true"` albo `relguided` **nie ma**
wpisu w pliku `*.rightstree.xml` — miejsce w drzewie dziedziczy po obiekcie nadrzędnym. Wpis
dostają wyłącznie tabele bez tych relacji: [rights-tree.md](rights-tree.md).

### Klucz złożony z Lp

```xml
<col name="Dokument" type="Dokument" 
     keyprimary="true" keyclass="Lp" keyclasscol="Lp"
     children="Pozycje" delete="cascade">
  <!-- keycol definiuje dodatkową kolumnę w kluczu -->
</col>
<col name="Lp" type="int" required="true"/>
```

### Klucz kolumny relacji i `keycol`

**Każda kolumna relacji generuje własny klucz**, także bez atrybutów `key*`:

- pola `[<Kolumna>, ID]`; z `<keycol>` wewnątrz `<col>` — `[<Kolumna>, <keycol>…]` (bez `ID`);
- nazwa klucza = nazwa kolumny, akcesor tabeli `Wg<Kolumna>`;
- indeksery: `Wg<Kolumna>[rodzic]` → `SubTable` dzieci; kolejne pola klucza dają indeksery
  z prefiksem (`[rodzic, pole1]` → `SubTable`), a pełny klucz unikalny → pojedynczy wiersz albo `null`;
- `keyunique`/`keyprimary` na `<col>` czynią ten klucz unikalnym/głównym.

**Pułapka — osobny `<key>` zaczynający się od relacji:**

```xml
<!-- ŹLE: dwa klucze [Provider, ID] i [Provider, Name] — pierwszy jest nadmiarowy -->
<key name="ByName" keyunique="true" keyprimary="true">
  <keycol name="Provider"/>
  <keycol name="Name"/>
</key>
<col name="Provider" type="Dostawca" required="true" readonly="true"
     children="Modele" delete="cascade" relguided="inner"/>
```

Po pominięciu `ID` pierwszy klucz jest prefiksem drugiego: dodatkowy indeks w bazie i w pamięci
sesji bez nowych możliwości wyszukiwania. W modułach standardowych taka para zatrzymuje
weryfikację kluczy aplikacji.

```xml
<!-- DOBRZE: jeden klucz Provider = [Provider, Name], unikalny i główny -->
<col name="Provider" type="Dostawca" required="true" readonly="true"
     keyunique="true" keyprimary="true"
     children="Modele" delete="cascade" relguided="inner">
  <keycol name="Name"/>
</col>
<col name="Name" type="string" length="128" required="true"/>
```

Akcesor zmienia nazwę z `ByName` na `WgProvider` — popraw odwołania w kodzie
(`Table.WgProvider[dostawca]`, `Table.WgProvider[dostawca, "nazwa"]`). Kilka pól:
`<keycol name="Rodzaj"/><keycol name="Name"/>` w relacji `Agent` → `[Agent, Rodzaj, Name]`
z indekserami `[agent]`, `[agent, rodzaj]`, `[agent, rodzaj, nazwa]`.

**Reguła ogólna:** żaden klucz tabeli nie może być identyczny z innym ani być jego prefiksem
(bez `ID`) — dotyczy też dwóch zwykłych `<key>` (np. `[Symbol]` i `[Symbol, Numer]`). Zwykle
wystarczy dłuższy klucz, bo wyszukiwanie po prefiksie obsługuje indekser z mniejszą liczbą
argumentów. Wyjątek: krótszy klucz jest unikalny, a dłuższy nie — wtedy zostaje krótszy.

Checklista:
- [ ] `<key>` zaczyna się od kolumny relacji → przenieś go na `<col>` relacji (`keyunique`/`keyprimary` + `<keycol>`).
- [ ] Żaden klucz nie jest identyczny z innym ani jego prefiksem (pole `ID` się nie liczy).
- [ ] Po przeniesieniu poprawione odwołania do akcesora (`By…` → `Wg<Kolumna>`).

---

## Relacja wiele-do-wielu

### Wzorzec: Tabela łącząca

```xml
<!-- TABELA ŁĄCZĄCA -->
<table name="TowarKategoria" tablename="TowaryKategorie">
  <col name="Towar" type="Towar" 
       required="true" 
       keyprimary="true" 
       keyunique="true"
       children="Kategorie"
       delete="cascade"
       relname="Kategorie towaru">
    <keycol name="Kategoria"/>
  </col>
  <col name="Kategoria" type="Kategoria" 
       required="true"
       children="Towary"
       delete="cascade"
       relname="Towary w kategorii">
    <keycol name="Towar"/>
  </col>
</table>
```

### Wzajemne keycol

Element `<keycol>` w kolumnie relacji tworzy indeks złożony:

```xml
<col name="Towar" type="Towar" keyunique="true">
  <keycol name="Kategoria"/>  <!-- Indeks: (Towar, Kategoria) -->
</col>
<col name="Kategoria" type="Kategoria">
  <keycol name="Towar"/>      <!-- Indeks: (Kategoria, Towar) -->
</col>
```

---

## Relacja do interfejsu (polimorficzna)

Relacja interface'owa pozwala na wskazanie obiektu z **dowolnej tabeli** implementującej dany interface. W bazie danych zapisywana jest para: `(nazwa_tabeli, ID)`.

### Deklaracja interfejsu

Interface musi być zadeklarowany w business.xml. Sama definicja interfejsu (metody, właściwości) jest w kodzie C#.

```xml
<!-- DEKLARACJA INTERFEJSU w business.xml -->
<interface name="IKontrahent"/>
<interface name="IPodmiotKasowy"/>
```

### Wzorzec: Relacja interface'owa

```xml
<!-- TABELA Z RELACJĄ INTERFACE'OWĄ -->
<table name="Zamowienie" tablename="Zamowienia">
  <!-- Typ to nazwa interfejsu - może wskazywać na Osobę, Firmę, lub inny obiekt implementujący IKontrahent -->
  <col name="Kontrahent" type="IKontrahent" 
       required="true"
       relname="Kontrahent zamówienia"
       description="Może być osobą fizyczną lub firmą"/>
  
  <!-- Relacja interface'owa do podmiotu kasowego -->
  <col name="Platnik" type="IPodmiotKasowy"
       relname="Płatnik zamówienia"/>
</table>
```

### Różnica: relacja zwykła vs interface'owa

| Cecha | Relacja zwykła | Relacja interface'owa |
|-------|----------------|----------------------|
| Typ kolumny | `NazwaTabeli` | `INazwaInterface` |
| W bazie danych | tylko `ID` | `(nazwa_tabeli, ID)` |
| Wskazuje na | jedną konkretną tabelę | dowolną tabelę implementującą interface |
| Przykład | `type="Kontrahent"` | `type="IKontrahent"` |

### Popularne interfejsy platformy Soneta

| Interfejs | Opis |
|-----------|------|
| `IKontrahent` | Kontrahent (osoba/firma) |
| `IPodmiotKasowy` | Podmiot kasowy |
| `IRightsSource` | Źródło uprawnień |
| `IElementSlownika` | Element słownika |
| `IGuidedRow` | Wiersz z nawigacją |

---

## Historia (wersjonowanie)

### Wzorzec: Dane historyczne

```xml
<table name="CenaHistoria" tablename="CenyHistoria">
  <col name="Towar" type="Towar" 
       required="true" 
       readonly="true"
       keyprimary="true"
       keyclass="History"
       keyclasscol="Okres"
       children="HistoriaCen"
       delete="cascade"
       relguided="inner"
       relname="Historia cen towaru"/>
  <col name="Okres" type="FromTo" required="true"
       caption="Okres obowiązywania"/>
  <col name="CenaNetto" type="currency"/>
  <col name="CenaBrutto" type="currency"/>
</table>
```

### Kluczowe atrybuty historii

| Atrybut | Wartość | Znaczenie |
|---------|---------|-----------|
| `keyclass="History"` | | Klasa indeksu historycznego |
| `keyclasscol="Okres"` | | Kolumna z okresem (FromTo) |

### Typ FromTo

Wbudowany typ dla okresów czasowych z polami `From` i `To`.

---

## Relacja zwrotna

### Wzorzec: Struktura hierarchiczna

```xml
<table name="Kategoria" tablename="Kategorie" guided="Root">
  <key name="WgKodu" keyunique="true" keyprimary="true">
    <keycol name="Kod"/>
  </key>
  <col name="Kod" type="string" length="50" required="true"/>
  <col name="Nazwa" type="string" length="100"/>
  <col name="Nadrzedna" type="Kategoria"
       children="Podkategorie"
       relname="Kategoria nadrzędna"
       description="Kategoria nadrzędna w hierarchii"/>
</table>
```

### Z poziomem zagnieżdżenia

```xml
<col name="Nadrzedna" type="Kategoria" children="Podkategorie">
  <keycol name="Poziom"/>
</col>
<col name="Poziom" type="int" readonly="true"
     description="Poziom w hierarchii (0 = root)"/>
```

---

## Kolekcja odwrotna dla tabel z innego modułu

`children="X"` na relacji do tabeli **zewnętrznej** (z innego modułu/assembly, np. `Kontrahent`
z CRM) **nie tworzy** property kolekcji na klasie zewnętrznej — generator nie może modyfikować
obcego assembly. Dostęp do „dzieci" realizuje się przez tabelę **własnego** modułu i akcesor
indeksu `Wg<NazwaKolumnyRelacji>`:

```csharp
var modul = session.Get<SerwisModule>();
SubTable<Zgloszenie> zgloszenia = modul.Zgloszenia.WgKontrahent[kontrahent];
```

`Wg…[parent]` zwraca przefiltrowaną `SubTable<T>` — wybiera po indeksie, bez skanu tabeli.
Dla relacji między tabelami **własnego** modułu property kolekcji (`dokument.Pozycje`)
generuje się normalnie. Kontrakt akcesorów widać w wygenerowanym `*.business.cs`
(patrz [generated-classes.md](generated-classes.md)).

---

## Automatyczne wypełnianie z kontekstu (Context)

`<attribute>Context</attribute>` na kolumnie relacji powoduje automatyczne wypełnienie pola
z kontekstu UI — nowy rekord otwierany „z" kontrahenta (z jego listy lub formularza) dostaje
wypełnionego kontrahenta. Przydatne dla czynności „Nowy z…".

```xml
<col name="Kontrahent" type="Kontrahent" required="true" relname="Dokumenty kontrahenta">
  <attribute>Context</attribute>
</col>
```

Warianty (`Context(Required=false)`) — patrz [table-reference.md](table-reference.md),
sekcja „Element attribute".

---

## Wzorce złożone

### Relacja z dodatkowymi danymi

```xml
<table name="UdzialWProjekcie" tablename="UdzialyWProj">
  <col name="Projekt" type="Projekt" 
       keyprimary="true" keyunique="true"
       children="Udzialy" delete="cascade">
    <keycol name="Pracownik"/>
  </col>
  <col name="Pracownik" type="Pracownik" 
       required="true"
       children="Projekty" delete="cascade">
    <keycol name="Projekt"/>
  </col>
  <!-- Dodatkowe dane relacji -->
  <col name="Rola" type="RolaWProjekcie"/>
  <col name="OdKiedy" type="date"/>
  <col name="DoKiedy" type="date"/>
  <col name="Stawka" type="currency"/>
</table>
```

### Relacja z kaskadowym usuwaniem warunkowym

```xml
<col name="Kontrahent" type="Kontrahent"
     delete="cascade"
     children="Dokumenty"
     relname="Dokumenty kontrahenta">
  <!-- Weryfikator sprawdza warunki przed usunięciem -->
  <verifier name="Dokument.KontrahentDeleteVerifier"/>
</col>
```
