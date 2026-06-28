# Przykłady z modułów enova365

## Spis treści

1. [Prosty słownik (Jednostka)](#prosty-słownik)
2. [Główna encja (Towar)](#główna-encja)
3. [Master-Detail (Przecena z pozycjami)](#master-detail)
4. [Konfiguracja z historią](#konfiguracja-z-historią)
5. [Subrow (typy złożone)](#subrow-typy-złożone)
6. [Kompletny moduł](#kompletny-moduł)

---

## Prosty słownik

### Jednostka miary (z Towary)

```xml
<table name="Jednostka" tablename="Jednostki" guided="Root" config="true"
       caption="Jednostka" tablecaption="Jednostki">

  <key name="WgKodu" keyunique="true" keyprimary="true">
    <keycol name="Kod"/>
  </key>

  <col name="Kod" type="string" length="10" localization="dictionary" 
       required="true" important="true"
       description="Nazwa jednostki wykorzystywana przy wprowadzaniu ilości."/>
  <col name="Opis" type="string" length="80" localization="dictionary"
       description="Opis jednostki."/>
  <col name="Precyzja" type="int"
       description="Precyzja zaokrąglenia ilości towaru"/>
  <col name="Typ" type="TypJednostki"
       description="Typ jednostki: masa, długość, czas, itp."/>
  <col name="Blokada" type="boolean" important="true"
       description="Jednostka nie będzie wyświetlana na listach."/>
  <col name="Uzupelniajaca" type="boolean"
       description="Jednostka uzupełniająca dla deklaracji UE."/>
  <col name="JednostkaDlugosci" type="Jednostka"
       description="Powiązana jednostka długości."/>
</table>
```

**Cechy:**
- `config="true"` - tabela konfiguracyjna
- `guided="Root"` - dostępna z menu głównego
- `localization="dictionary"` - tłumaczenie przez słownik
- Relacja zwrotna (`JednostkaDlugosci`)

---

## Główna encja

### Towar (fragment z Towary)

```xml
<table name="Towar" tablename="Towary" guided="Root" optimisticlocking="true">

  <interface>IElementSlownika</interface>
  <interface>IKodowany</interface>

  <key name="WgKodu" keyunique="true" keyprimary="true">
    <keycol name="Kod"/>
  </key>
  <key name="WgNazwy">
    <keycol name="Nazwa"/>
  </key>
  <key name="WgEAN">
    <keycol name="EAN"/>
  </key>

  <!-- OGÓLNE -->
  <col name="Kod" type="string" length="100" category="Ogólne" 
       required="true" important="true"
       description="Symbol, skrócona nazwa towaru"/>
  <col name="Typ" type="TypTowaru" required="true" category="Ogólne" 
       important="true"
       description="Określa czy towar, usługa, produkt"/>
  <col name="Nazwa" type="string" length="200" required="true" 
       category="Ogólne" important="true"
       description="Nazwa towaru na dokumentach.">
    <verifier name="Towar.NazwaVerifier"/>
  </col>
  <col name="EAN" type="string" length="130" category="Ogólne"
       description="Kod kreskowy do wyszukiwania.">
    <verifier name="Towar.EANUniqueVerifier"/>
  </col>
  
  <!-- RELACJE -->
  <col name="Jednostka" type="Jednostka" required="true" category="Ogólne"
       description="Podstawowa jednostka magazynowa."
       relname="Jednostka towarowa"/>
  <col name="DefinicjaStawki" type="DefinicjaStawkiVat" required="true" 
       category="Ogólne"
       description="Stawka VAT dla sprzedaży"
       relname="Stawka VAT towaru"/>
  <col name="Dostawca" type="Kontrahent" category="Generowanie zamówień"
       relname="Dostawca towaru"
       children="Towary"
       description="Standardowy dostawca towaru.">
    <keycol name="Kod"/>
  </col>

  <!-- CENY -->
  <col name="Narzut" type="percent" category="Ceny"
       description="Narzut do wyliczania ceny hurtowej"/>
  <col name="Marza" type="percent" caption="Marża" category="Ceny"
       description="Marża do wyliczania ceny detalicznej"/>
  <col name="CenaZakupuKartotekowa" type="doublecy" category="Ceny"/>

  <!-- DODATKOWE -->
  <col name="Blokada" type="boolean" category="Dodatkowe"
       description="Towar niewidoczny na listach wyboru.">
    <verifier name="Towar.BlokadaVerifier" onadded="false"/>
  </col>
  <col name="Opis" type="text" category="Opis na dokumencie"
       description="Rozszerzony opis towaru."/>
</table>
```

---

## Master-Detail

### Przecena okresowa z pozycjami (z Towary)

```xml
<!-- MASTER: Definicja przeceny -->
<table name="PrzecenaOkresowa" tablename="PrzecenyOkres" guided="Root">
  <key keyunique="true" name="WgNazwy" keyprimary="true">
    <keycol name="Cel"/>
    <keycol name="Nazwa"/>
  </key>
  <key name="Zatwierdzone">
    <keycol name="Zatwierdzona"/>
    <keycol name="Cel"/>
  </key>

  <col name="Cel" type="CelPrzecenyOkresowej" selector="true" readonly="true"/>
  <col name="Nazwa" type="string" length="32" required="true"
       description="Nazwa przeceny okresowej (promocji)."/>
  <col name="Okres" type="FromTo" required="true"
       description="Okres obowiązywania przeceny okresowej."/>
  <col name="Zatwierdzona" type="boolean"
       description="Czy promocja jest zatwierdzona."/>
  <col name="Typ" type="TypPrzecenyOkresowej"/>
  <col name="Kontrahent" type="Kontrahent"
       description="Kontrahent dla promocji indywidualnej."/>
</table>

<!-- DETAIL: Pozycje przeceny -->
<table name="PrzecenaOkresowaTowaru" tablename="PrzecenyOkresTwr">
  <col name="PrzecenaOkresowa" type="PrzecenaOkresowa"
       children="PrzecenyTowarow"
       delete="cascade"
       keyprimary="true">
    <attribute>Context</attribute>
    <keycol name="Towar"/>
  </col>
  <col name="Towar" type="Towar" required="true" 
       children="Przeceny"
       description="Towar, którego dotyczy przecena.">
    <attribute>Context</attribute>
  </col>
  <col name="Netto" type="doublecy" category="Cena"
       caption="Cena promocyjna netto"/>
  <col name="Brutto" type="doublecy" category="Cena" 
       caption="Cena promocyjna brutto"/>
  <col name="Rabat" type="percent" modifier="protected" category="Cena"
       caption="Rabat promocyjny"/>
</table>
```

---

## Konfiguracja z historią

### Procedura SME z historią (z Core)

```xml
<!-- GŁÓWNA TABELA -->
<table name="KrajSME" tablename="KrajeSME" caption="Kraj SME" 
       tablecaption="Kraje SME" config="true" guided="Root">
  <col name="Kraj" type="KrajTbl" caption="Kraj" 
       required="true" keyunique="true" keyprimary="true"/>
</table>

<!-- HISTORIA -->
<table name="ProceduraSME" tablename="ProcedurySME" caption="Procedura SME" 
       tablecaption="Procedury SME" config="true">
  <col name="Kraj" type="KrajSME" 
       readonly="true" required="true" 
       keyprimary="true" 
       keyclass="History" 
       keyclasscol="Aktualnosc"
       children="Historia" 
       relname="Dane historyczne procedury SME" 
       relguided="inner" 
       delete="cascade"
       description="Kraj SME"/>
  <col name="Aktywna" type="boolean" caption="Aktywna procedura SME"/>
  <col name="Aktualnosc" type="FromTo" required="true" 
       caption="Okres obowiązywania"/>
  <col name="ProgObrotu" type="currency" caption="Próg obrotu"/>
</table>
```

---

## Subrow (typy złożone)

### DefinicjaCyklu (z Core)

```xml
<subrow name="DefinicjaCyklu">
  <col name="Krotnosc" type="int" modifier="public virtual"
       description="Ile razy cykl będzie powtórzony.">
    <attribute>Browsable(false)</attribute>
  </col>
  <col name="Interwal" type="int" modifier="public virtual"/>
  <col name="Typ" type="DefinicjaCykluTyp" modifier="public virtual"
       description="Rodzaj cyklu (jednostka interwału)."/>
  <col name="Czas" type="time" 
       description="Czas wystąpienia cyklu."/>
  <col name="PozycjaDnia" type="DefinicjaCykluPozycjaDnia"
       description="Pozycja dnia w okresie."/>
  <col name="RodzajTerminu" type="DefinicjaCykluRodzajTerminu" 
       modifier="public virtual"/>
  <col name="SposobNaDniWolne" type="DefinicjaCykluSposobNaDniWolne"
       specialaccess="true"
       description="Zachowanie gdy cykl wypada w dzień wolny."/>
  <col name="Termin" type="int" readonly="set" modifier="protected"
       description="Termin wystąpienia (wartość wewnętrzna).">
    <attribute>Browsable(false)</attribute>
  </col>
</subrow>
```

### Adres (z Core)

```xml
<subrow name="Adres">
  <col name="KodPocztowy" type="int" description="Kod pocztowy">
    <attribute>MaskEdit("00-000", SaveLiteral=false)</attribute>
    <attribute>SqlResolving(IgnoreDashes=true, NoDashesData=true)</attribute>
  </col>
  <col name="ZagranicznyKodPocztowy" type="string" length="11"/>
  <col name="Poczta" type="string" length="56">
    <attribute>Dictionary("Miejscowość")</attribute>
  </col>
  <col name="Miejscowosc" type="string" length="56">
    <attribute>Dictionary("Miejscowość")</attribute>
  </col>
  <col name="Ulica" type="string" length="80">
    <attribute>Dictionary("Ulica")</attribute>
  </col>
  <col name="NrDomu" type="string" length="10"/>
  <col name="NrLokalu" type="string" length="10"/>
  <col name="Wojewodztwo" type="Wojewodztwa"/>
</subrow>
```

---

## Kompletny moduł

### Przykład prostego modułu

```xml
<?xml version="1.0" encoding="utf-8" ?>
<module xmlns="http://www.enova.pl/schema/business_struct.xsd" 
        name="Projekty" 
        namespace="Soneta.Projekty" 
        versionName="soneta">

  <!-- Ścieżka do katalogu z innymi business.xml (względna od tego pliku) -->
  <!-- Pozwala referować typy z innych modułów np. Kontrahent, Pracownik -->
  <import>../..</import>
  
  <!-- Namespaces C# potrzebne do użycia typów z innych modułów -->
  <using>Soneta.Core</using>
  <using>Soneta.CRM</using>      <!-- dla Kontrahent -->
  <using>Soneta.Kadry</using>    <!-- dla Pracownik -->

  <!-- ENUMY -->
  <enum name="StatusProjektu"/>
  <enum name="PriorytetProjektu"/>
  <enum name="TypZadania"/>

  <!-- INTERFEJSY -->
  <interface name="IProjektHost"/>

  <!-- SUBROW -->
  <subrow name="OkresProjektu">
    <col name="DataRozpoczecia" type="date"/>
    <col name="DataZakonczenia" type="date"/>
    <col name="Budzet" type="currency"/>
  </subrow>

  <!-- TABELE -->
  <table name="Projekt" tablename="Projekty" guided="Root"
         caption="Projekt" tablecaption="Projekty">
    <interface>IRightsSource</interface>
    <interface>IProjektHost</interface>

    <key name="WgKodu" keyunique="true" keyprimary="true">
      <keycol name="Kod"/>
    </key>
    <key name="WgKlienta">
      <keycol name="Klient"/>
      <keycol name="Status"/>
    </key>

    <col name="Kod" type="string" length="20" required="true" 
         important="true" category="Ogólne"/>
    <col name="Nazwa" type="string" length="200" required="true" 
         category="Ogólne"/>
    <col name="Klient" type="Kontrahent" required="true" 
         category="Ogólne"
         relname="Projekty klienta" children="Projekty"/>
    <col name="Kierownik" type="Pracownik" category="Ogólne"
         relname="Projekty kierownika"/>
    <col name="Status" type="StatusProjektu" category="Ogólne" 
         selector="true"/>
    <col name="Priorytet" type="PriorytetProjektu" category="Ogólne"/>
    <col name="Okres" type="OkresProjektu" category="Terminy"/>
    <col name="Opis" type="text" category="Dodatkowe"/>
  </table>

  <table name="Zadanie" tablename="Zadania"
         caption="Zadanie" tablecaption="Zadania">
    <col name="Projekt" type="Projekt" 
         required="true" readonly="true"
         keyprimary="true"
         children="Zadania" 
         delete="cascade" 
         relguided="inner"
         relname="Zadania projektu"/>
    <col name="Lp" type="int" required="true" batchfield="false"/>
    <col name="Nazwa" type="string" length="200" required="true"/>
    <col name="Typ" type="TypZadania"/>
    <col name="Wykonawca" type="Pracownik" 
         relname="Zadania pracownika" children="Zadania"/>
    <col name="TerminOd" type="date"/>
    <col name="TerminDo" type="date"/>
    <col name="Wykonane" type="boolean"/>
    <col name="Opis" type="text"/>
  </table>

</module>
```
