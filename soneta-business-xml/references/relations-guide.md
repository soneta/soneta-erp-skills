# Relacje między obiektami - Przewodnik

## Spis treści

1. [Typy relacji](#typy-relacji)
2. [Relacja jeden-do-wielu (master-detail)](#relacja-jeden-do-wielu)
3. [Relacja wiele-do-wielu](#relacja-wiele-do-wielu)
4. [Relacja do interfejsu](#relacja-do-interfejsu)
5. [Historia (wersjonowanie)](#historia-wersjonowanie)
6. [Relacja zwrotna (self-reference)](#relacja-zwrotna)

---

## Typy relacji

| Typ | Opis | W bazie danych | Przykład |
|-----|------|----------------|----------|
| Jeden-do-wielu | Dokument → Pozycje | FK (ID) | Faktura ma wiele pozycji |
| Wiele-do-wielu | Przez tabelę łączącą | 2x FK | Towar ↔ Kategorie |
| Interface'owa | Polimorficzna do wielu tabel | (tabela, ID) | IKontrahent → Osoba lub Firma |
| Historia | Wersjonowanie temporalne | FK + okres | Ceny towaru w czasie |
| Zwrotna | Do tej samej tabeli | FK (ID) | Kategoria nadrzędna |

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

### Zasada relguided="inner"

Tabele bez atrybutu `guided` (tabele szczegółów) **muszą mieć dokładnie jedną** relację z `relguided="inner"`. Wskazuje ona obiekt główny, którego szczegóły są opisywane.

### Klucz złożony z Lp

```xml
<col name="Dokument" type="Dokument" 
     keyprimary="true" keyclass="Lp" keyclasscol="Lp"
     children="Pozycje" delete="cascade">
  <!-- keycol definiuje dodatkową kolumnę w kluczu -->
</col>
<col name="Lp" type="int" required="true"/>
```

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

## Wzorce złożone

### Relacja z dodatkowymi danymi

```xml
<table name="UdzialWProjekcie" tablename="UdzialyWProjektach">
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
