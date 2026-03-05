---
name: soneta-form-xml
description: Tworzenie plików form.xml opisujących formularze, zakładki i widoki UI dla platformy enova365/Soneta Enterprise. Używaj gdy użytkownik prosi o utworzenie zakładki formularza (pageform.xml), widoku listy (viewform.xml), formularza (form.xml), lookupu (lookupform.xml), lub gdy pyta o strukturę i składnię plików form.xml dla enova365.
---

# Soneta Form XML - Formularze UI

Skill do tworzenia plików XML definiujących interfejs użytkownika w systemie enova365/Soneta Enterprise.

## Typy plików formularzy

| Typ pliku | Wzorzec nazwy | Przeznaczenie |
|-----------|---------------|---------------|
| **pageform.xml** | `{DataType}.{PageName}.pageform.xml` | Zakładka formularza edycji obiektu |
| **viewform.xml** | `{NazwaWidoku}.viewform.xml` | Widok listy zarejestrowanej jako folder (listy główne) |
| **gridform.xml** | `{IdentyfikatorListy}.gridform.xml` | Indywidualne ustawienia listy na formularzu |
| **lookupform.xml** | `{NazwaPodpowiedzi}.lookupform.xml` | Lista wyboru (lookup) |
| **form.xml** | `{Nazwa}.form.xml` | Współdzielony fragment UI (include) |

### Format nazwy pageform.xml

Nazwa pliku składa się z 4 części rozdzielonych kropkami:

```
{DataType}.{PageName}.pageform.xml
```

- **DataType** - typ danych, dla którego definiowana jest zakładka (np. `Towar`, `Kontrahent`, `DokumentHandlowy`)
- **PageName** - nazwa zakładki (np. `Ogolne`, `Dodatkowe`, `Adresy`)
- **pageform.xml** - stały sufiks

**Przykłady:**
- `Towar.Ogolne.pageform.xml`
- `Towar.Dodatkowe.pageform.xml`
- `Kontrahent.Adresy.pageform.xml`
- `DokumentHandlowy.Pozycje.pageform.xml`

### Format nazwy viewform.xml

Nazwa pliku składa się z 3 części rozdzielonych kropkami:

```
{NazwaWidoku}.viewform.xml
```

**Przykłady:**
- `Towary.viewform.xml`
- `Kontrahenci.viewform.xml`
- `DokumentyHandlowe.viewform.xml`

### Format nazwy gridform.xml

Nazwa pliku składa się z 3 części rozdzielonych kropkami:

```
{IdentyfikatorListy}.gridform.xml
```

**Przykłady:**
- `PozycjeDokumentu.gridform.xml`
- `RachunkiBankowe.gridform.xml`

### Format nazwy lookupform.xml

Nazwa pliku składa się z 3 części rozdzielonych kropkami:

```
{NazwaPodpowiedzi}.lookupform.xml
```

**Przykłady:**
- `Towary.lookupform.xml`
- `Kontrahenci.lookupform.xml`

## Hierarchia elementów XML

Elementy XML dzielą się na trzy grupy dziedziczące po `uiElement`:

1. **Elementy proste** - Field, Label, Command, Gap, Html, GroupBy, Axis
2. **Kontenery elementów** - Group, Stack, Row, Flow, Bar, Dashboard, Include, Page (dziedziczą po `ContainerElement`)
3. **Kolekcje** - Grid, Cards, Scheduler, Chart, Diagram, Pivot (dziedziczą po `CollectionElement`)

## Struktura dokumentu

Każdy plik formularza zaczyna się od deklaracji XML i elementu głównego `DataForm`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<DataForm xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
          xmlns:xsd="http://www.w3.org/2001/XMLSchema"
          xmlns="http://www.enova.pl/schema/form.xsd"
          xsi:schemaLocation="http://www.enova.pl/schema/ http://www.enova.pl/schema/form.xsd">
  <!-- zawartość -->
</DataForm>
```

### Atrybuty DataForm

| Atrybut | Opis |
|---------|------|
| `Priority` | Kolejność zakładek (domyślnie 100, niższa = wcześniej) |
| `RightName` | Opcjonalny. Nazwa uprawnienia do zakładki (tylko gdy inna niż standardowa) |
| `Contexts` | Warunki licencyjne, np. `"Licence.HAN \| Licence.FA"` (nie bindowane) |
| `ViewType` | Typ widoku: `None`, `Dialog`, `Form`, `Folder` |
| `Mode` | Tryb: `None`, `Form`, `Folder`, `Wizard`, `Modal`, `Popup`, `Frame` |
| `DataType` | Opcjonalny. Pełne określenie typu danych (wymagany tylko gdy nazwa pliku nie określa jednoznacznie typu), np. `"Soneta.Handel.DokumentHandlowy,Soneta.Handel"` |

## Wspólne atrybuty elementów

Następujące atrybuty mogą być użyte w **dowolnym** elemencie form.xml:

| Atrybut | Opis |
|---------|------|
| `Name` | Identyfikator elementu, który można wykorzystać w kodzie C# |
| `Class` | Klasy stylów (lista wartości oddzielonych spacją) |
| `DataContext` | Zmienia kontekst danych dla elementu i wszystkich jego elementów podrzędnych |
| `Visibility` | Warunek widoczności (bindowalne z logiką biznesową) |
| `Renderable` | Czy element ma być dostępny. Wyrażenie liczone **raz** przy logowaniu operatora - optymalne dla warunków zależnych od środowiska, licencji i innych parametrów niezmiennych w trakcie sesji |

### Atrybut CaptionHtml

Atrybut `CaptionHtml` występuje w elementach: `Label`, `Field`, `Group`, `Page`, `Command` i innych.

Może zawierać:
- Tekst etykiety (w formacie HTML)
- Wyrażenia bindowane w klamrach: `{wyrażenie}` - wartość tekstowa jest automatycznie kodowana do HTML
- Wyrażenia zwracające kod HTML: `{WłaściwośćHtml}` - nazwa musi mieć sufiks `Html`, wtedy wartość nie jest kodowana
- Podwójne klamry dla literalnych znaków: `{{` → `{`, `}}` → `}`

**Specjalne przypadki w `Field`:**
- Brak atrybutu → automatyczna etykieta wyliczana na podstawie danych
- `CaptionHtml=" "` (spacja) → pusta etykieta (miejsce na etykietę zostaje zachowane)
- `CaptionHtml=""` (pusty) → brak etykiety (pole bez miejsca na etykietę)

**Alternatywa:** Zamiast `CaptionHtml` można użyć `CaptionMarkdown` dla etykiet w formacie Markdown.

## Elementy kontenerowe

### Page - Zakładka

Główny kontener dla zawartości zakładki:

```xml
<Page CaptionHtml="Ogólne" DataContext="{DataSource}">
  <!-- zawartość zakładki -->
</Page>
```

| Atrybut | Opis |
|---------|------|
| `Name` | Opcjonalny. Unikalny identyfikator zakładki |
| `CaptionHtml` | Tytuł zakładki. Może zawierać `/` do grupowania zakładek (np. `"Dokumenty/Faktury"`) |
| `DataContext` | Źródło danych. `{DataSource}` oznacza obiekt edytowany na zakładce, ale można wskazać inne dane |
| `Visibility` | Wyrażenie warunkowe widoczności (bindowalne) |
| `Renderable` | Czy zakładka ma być dostępna. Wyrażenie liczone raz przy logowaniu - optymalne dla warunków zależnych od środowiska, licencji itp. |
| `Key` | Skrót klawiaturowy wywołujący zakładkę formularza |
| `GroupIcon` | Ikona grupy zakładek |

### Zasada budowania zakładki formularza

Zakładka składa się z elementów `<Group>`. Każda grupa może zawierać:

1. **Listę pól w kolumnie** - pola ułożone pionowo jedno pod drugim
2. **Układ wielokolumnowy** - `<Row>` zawierający kilka `<Stack>` z polami
3. **Listę elementów podrzędnych** - np. Grid, ewentualnie poprzedzony polami filtrującymi

```xml
<Page CaptionHtml="Ogólne" DataContext="{DataSource}">
  <!-- Grupa z polami w jednej kolumnie -->
  <Group CaptionHtml="Dane podstawowe">
    <Field CaptionHtml="Kod" Width="20" EditValue="{Kod}" />
    <Field CaptionHtml="Nazwa" Width="*" EditValue="{Nazwa}" />
  </Group>
  
  <!-- Grupa z układem dwukolumnowym -->
  <Group CaptionHtml="Dane szczegółowe">
    <Row>
      <Stack>
        <Field CaptionHtml="Data" Width="15" EditValue="{Data}" />
        <Field CaptionHtml="Status" Width="15" EditValue="{Status}" />
      </Stack>
      <Stack>
        <Field CaptionHtml="Typ" Width="15" EditValue="{Typ}" />
        <Field CaptionHtml="Źródło" Width="15" EditValue="{Zrodlo}" />
      </Stack>
    </Row>
  </Group>
  
  <!-- Grupa z listą -->
  <Group CaptionHtml="Pozycje">
    <Grid Width="*" Height="*" EditValue="{Pozycje}">
      <Field CaptionHtml="Nazwa" Width="*" EditValue="{Nazwa}" />
    </Grid>
  </Group>
</Page>
```

### Group - Grupa pól

Wizualna ramka grupująca powiązane pola:

```xml
<Group CaptionHtml="Dane podstawowe">
  <Field CaptionHtml="Kod" Width="20" EditValue="{Kod}" />
  <Field CaptionHtml="Nazwa" Width="*" EditValue="{Nazwa}" />
</Group>
```

| Atrybut | Opis |
|---------|------|
| `CaptionHtml` | Tytuł grupy |
| `LabelWidth` | Opcjonalny. Szerokość etykiet dla wszystkich pól w kontenerze |
| `Visibility` | Warunek widoczności (bindowalne z logiką biznesową) |
| `Renderable` | Czy grupa ma być dostępna (liczone raz przy logowaniu) |

### Stack - Układ pionowy

Układa elementy jeden pod drugim:

```xml
<Stack LabelWidth="15">
  <Field CaptionHtml="Pole 1" EditValue="{Pole1}" />
  <Field CaptionHtml="Pole 2" EditValue="{Pole2}" />
</Stack>
```

### Row - Układ poziomy

Układa elementy obok siebie w wierszu:

```xml
<Row>
  <Field CaptionHtml="Kod" Width="20" EditValue="{Kod}" />
  <Field CaptionHtml="Typ" Width="15" EditValue="{Typ}" />
</Row>
```

**Pole na całą szerokość:** użyj `Width="*"`

**Pola dosunięte do prawej:** umieść `<Gap Width="*"/>` na początku:

```xml
<Row>
  <Gap Width="*" />
  <Field CaptionHtml="Kod" Width="20" EditValue="{Kod}" />
  <Field CaptionHtml="Typ" Width="15" EditValue="{Typ}" />
</Row>
```

### Flow - Układ płynny

Elementy umieszczane są od lewej do prawej. Gdy nie mieszczą się na formularzu, przenoszone są do nowego wiersza:

```xml
<Flow Align="true">
  <Field CaptionHtml="Data od" Width="15" EditValue="{DataOd}" />
  <Field CaptionHtml="Data do" Width="15" EditValue="{DataDo}" />
  <Field CaptionHtml="Status" Width="12" EditValue="{Status}" />
</Flow>
```

## Elementy pól i kontrolek

### Field - Pole edycyjne

Podstawowy element do wyświetlania i edycji danych. Jest generowany **dynamicznie** - w zależności od typu właściwości wyświetla odpowiednią kontrolkę (int → pole numeryczne, bool → checkbox, double → pole z kalkulatorem, typ Sonety → lookup z listą wyboru).

Minimalny `<Field>` powinien zawierać:
- `EditValue` - **wymagany**, binding do właściwości
- `CaptionHtml` - etykieta pola  
- `Width` - szerokość pola

```xml
<Field CaptionHtml="Nazwa pola" Width="20" EditValue="{Właściwość}" />
```

| Atrybut | Opis |
|---------|------|
| `EditValue` | **Wymagany**. Binding do właściwości: `{Właściwość}` lub `{new Extender.Właściwość}` |
| `CaptionHtml` | Etykieta pola (patrz sekcja "Atrybut CaptionHtml") |
| `Width` | Szerokość pola w znakach lub px (`*` = wypełnij) |
| `OuterWidth` | Całkowita szerokość z etykietą (do wyrównywania w pionie) |
| `LabelWidth` | Szerokość etykiety |
| `Height` | Wysokość w wierszach lub px (dla pól wieloliniowych) |
| `IsReadOnly` | Warunek tylko do odczytu (bindowalne z logiką biznesową). Logika biznesowa ma wyższy priorytet |
| `Visibility` | Warunek widoczności (bindowalne z logiką biznesową): `true`/`false`/`Visible`/`Hidden`/`Collapsed` |
| `Renderable` | Czy pole ma być dostępne (liczone raz przy logowaniu) |
| `Format` | Formatowanie wartości w standardzie .NET `string.Format`. Pole `{0}` to wartość edytowana |
| `Footer` | Agregacja w stopce **tylko na listach**: `Sum`, `Count`, `Average`, `Min`, `Max` |
| `CheckedValue` | Wartość dla **RadioButton** |
| `Class` | Klasy stylów (patrz sekcja Class) |
| `DataContext` | Zmienia kontekst danych dla tego pola i elementów podrzędnych |

#### RadioButton

Aby utworzyć RadioButton, dodaj parametr `CheckedValue`:

```xml
<Field Width="20" CaptionHtml="Towar" EditValue="{Typ}" CheckedValue="Towar" />
<Field Width="20" CaptionHtml="Usługa" EditValue="{Typ}" CheckedValue="Usługa" />
<Field Width="20" CaptionHtml="Receptura" EditValue="{Typ}" CheckedValue="Receptura" />
```

Pola z tym samym `EditValue` i różnymi `CheckedValue` tworzą grupę RadioButton.

### Label - Etykieta

Tekst bez możliwości edycji:

```xml
<Label CaptionHtml="Tekst informacyjny" Width="30" />
```

### Gap - Odstęp

Wypełniacz przestrzeni. Użyteczny do dosunięcia elementów do prawej strony (gdy `<Gap Width="*" />` jest na początku wiersza).

```xml
<Row>
  <Gap Width="*" />
  <Field CaptionHtml="Kod" Width="20" EditValue="{Kod}" />
</Row>
```

**Uwaga:** `<Gap Width="*" />` jako ostatni element w wierszu nie jest potrzebny - nic nie zmienia.

### Command - Przycisk/Polecenie

```xml
<Command CaptionHtml="Zapisz" 
         MethodName="Zapisz" 
         DataContext="{new MojExtender}"
         Visibility="{IsVisibleButton}" />
```

| Atrybut | Opis |
|---------|------|
| `MethodName` | Nazwa metody do wywołania (szukana w klasie z kontekstu formularza) |
| `MoreMethodName` | Dodatkowa metoda (dla przycisków dzielonych - wersja przeglądowa) |
| `Key` | Skrót klawiszowy |
| `CommandStyle` | Styl: `Default`, `Important`, `Red`, `Green`, `Blue` |
| `Icon` | Nazwa ikony (nie działa w enova365 desktop) |
| `Class` | Klasy stylów |
| `DataContext` | Zmienia kontekst danych (np. `{new MojExtender}` - patrz sekcja Bindowanie danych) |
| `Renderable` | Czy przycisk ma być dostępny (liczone raz przy logowaniu) |

### Include - Dołączenie fragmentu

Wstawia zawartość innego pliku form.xml lub dynamicznie generowany element UI:

```xml
<!-- Statyczne dołączenie pliku -->
<Include Source="Adres.form.xml" />

<!-- Z kontekstem danych -->
<Include Source="Adres.form.xml" DataContext="{AdresKorespondencyjny}" />

<!-- Dynamiczny element z kodu C# -->
<Include Source="{DynamicznyFormularz}" />
```

| Atrybut | Opis |
|---------|------|
| `Source` | Nazwa pliku form.xml **lub** wyrażenie bindujące zwracające element DOM (`UIElement`) z kodu. Wyrażenie bindujące jest wywoływane po każdej zmianie danych, co pozwala budować dynamiczne formularze |
| `DataContext` | Opcjonalny. Kontekst danych dla dołączanego fragmentu |
| `Path` | Opcjonalny. Określa element pliku form.xml, który zostanie wstawiony (gdy Source jest nazwą pliku) |
| `Suffix` | Opcjonalny. Napis dodawany do każdej nazwy (`Name`) elementów dołączonych przez Include |

## Kolekcje i listy

### Grid / List - Tabela danych

```xml
<Grid Width="*" Height="*" 
      EditValue="{Pozycje}" 
      SelectedValue="{WybranePozycje}"
      FocusedValue="{AktualnaPozycja}"
      IsToolbarVisible="true"
      EditInPlace="true"
      NewInPlace="true"
      OrderBy="Data desc"
      SumType="All"
      OpenMethodName="OtworzFormularz"
      IsSmartOpen="true">
  <Field CaptionHtml="Kod" Width="15" EditValue="{Kod}" />
  <Field CaptionHtml="Nazwa" Width="30" EditValue="{Nazwa}" />
  <Field CaptionHtml="Ilość" Width="10" EditValue="{Ilość}" Footer="Sum" />
  <GroupBy EditValue="{Kategoria}" IsDescending="false" />
</Grid>
```

| Atrybut | Opis | Default |
|---------|------|---------|
| `EditValue` | Źródło danych kolekcji | - |
| `SelectedValue` | Binding do zaznaczonych wierszy - typ `DataType[]` (tablica) | - |
| `FocusedValue` | Binding do podświetlonego wiersza - typ `DataType` (obiekt) | - |
| `IsToolbarVisible` | Pokazuj pasek narzędzi | `false` |
| `IsFilterRowVisible` | Pokazuj wiersz filtrujący | `false` |
| `IsHeaderVisible` | Widoczność nagłówków z tytułami kolumn | `true` |
| `EditInPlace` | Edycja bezpośrednio w komórkach | `false` |
| `NewInPlace` | Dodawanie przez kliknięcie pustego wiersza | `false` |
| `AlwaysAddNewRow` | Nowy wiersz od razu dodawany (Esc nie usuwa) | `false` |
| `PreventNewRowOnFocus` | Zapobiegaj nowemu wierszowi przy fokusie | `false` |
| `KeepsSequence` | Czy po edycji in-place zachować kolejność wierszy (`true`) czy ponownie sortować (`false`) | `false` |
| `OpenMethodName` | Metoda wywoływana po Enter/double-click (domyślnie otwarcie formularza) | - |
| `IsSmartOpen` | Kolumna ze strzałką do otwarcia formularza | - |
| `NewButton` | Stan przycisku Nowy: `Auto`, `None`, `Visible` | auto |
| `EditButton` | Stan przycisku Otwórz | auto |
| `UpdateButton` | Stan przycisku Aktualizuj | auto |
| `RemoveButton` | Stan przycisku Usuń | auto |
| `SearchButton` | Stan przycisku Szukaj | auto |
| `MoreButton` | Przycisk więcej: `None`, `Visible`, `Actions` | auto |
| `OrderBy` | Domyślne sortowanie (np. `"Kolumna desc"`) | - |
| `Filter` | Filtr danych | - |
| `ResourceName` | Nazwa pliku grid.xml z indywidualnymi ustawieniami | - |
| `SumType` | Typ sum: `None`, `Selected`, `All`, `Groups`, `GroupsNewLine` | `None` |
| `ActionsMode` | Czy workery przypięte do listy mają być w menu Czynności formularza: `FormAndControl`, `Control`, `Form` | - |

#### Atrybuty dla drzewa (Tree)

| Atrybut | Opis | Default |
|---------|------|---------|
| `TreeNodesValue` | Binding do węzłów | - |
| `TreeHasNodesValue` | Binding sprawdzający czy ma węzły | - |
| `TreeParentValue` | Binding do rodzica | - |
| `TreeExpandingLevel` | Poziom rozwinięcia: `Collapsed`, `ExpandRoot`, `ExpandRootFix`, `ExpandAll` | `Collapsed` |

### GroupBy - Grupowanie w Grid

```xml
<Grid EditValue="{Pozycje}">
  <GroupBy EditValue="{Kategoria}" />
  <Field CaptionHtml="Nazwa" EditValue="{Nazwa}" />
</Grid>
```

## Elementy specjalne

### Chart - Wykres

```xml
<Chart EditValue="{DaneWykresu}" 
       Type="Bar" 
       ChartColor="Blue"
       IsLegendVisible="true">
  <Field CaptionHtml="Miesiąc" EditValue="{Miesiac}" />
  <Field CaptionHtml="Wartość" EditValue="{Wartosc}" />
  <Axis Direction="X" EditValue="{Miesiac}" />
  <Axis Direction="Y" EditValue="{Wartosc}" />
</Chart>
```

Typy wykresów: `Line`, `Bar`, `Pie`, `Donut`, `Spider`, `Polar`, `Area`, `Pyramid`, `Funnel`, `Bubble`, `Scatter`

### Scheduler - Kalendarz

```xml
<Scheduler EditValue="{Zdarzenia}" 
           View="Weekly"
           SelectedInterval="{WybranyOkres}">
  <Field CaptionHtml="Tytuł" EditValue="{Tytul}" />
  <Field CaptionHtml="Początek" EditValue="{DataOd}" />
  <Field CaptionHtml="Koniec" EditValue="{DataDo}" />
</Scheduler>
```

### Gantt / GanttDiagram - Harmonogram

```xml
<Gantt EditValue="{Zadania}" ViewType="WeekDay">
  <Field CaptionHtml="Zadanie" EditValue="{Nazwa}" />
  <Field CaptionHtml="Początek" EditValue="{Start}" />
  <Field CaptionHtml="Koniec" EditValue="{Finish}" />
</Gantt>
```

### Html / Markdown - Treść formatowana

```xml
<Html EditValue="{TrescHtml}" Width="*" Height="200" />
<Markdown EditValue="{TrescMarkdown}" Width="*" />
```

### Indicator - Wskaźnik

```xml
<Indicator CaptionHtml="Sprzedaż" 
           EditValue="{WartoscSprzedazy}" 
           UnitSymbol="PLN" />
```

### Dashboard - Panel kafelkowy

```xml
<Dashboard>
  <Group Class="DashboardItem" CaptionHtml="Sprzedaż">
    <Indicator EditValue="{Sprzedaz}" />
  </Group>
</Dashboard>
```

## Bindowanie danych

Każdy element form.xml znajduje się w odpowiednim kontekście powiązanego obiektu C#. Powiązany typ danych zależy od typu danych dla którego zdefiniowany jest pageform.xml.

### Powiązanie typu z formularzem

Powiązanie z typem w elemencie root odbywa się przez:

1. **Przez nazwę pliku pageform.xml** - pierwszy człon nazwy pliku (DataType) określa typ obiektu. Np. dla pliku `Towar.Ogolne.pageform.xml` kontekstem jest klasa `Towar`.

2. **Przez atrybut DataType w DataForm** - jawne określenie typu:
   ```xml
   <DataForm DataType="Soneta.Handel.Towar,Soneta.Handel">
   ```

3. **Przez rejestrację FolderViewAttribute** - wiążącą folder programu z obiektem biznesowym i formularzem prezentującym ten folder.

### Zmiana kontekstu danych

- **DataContext** - zmienia kontekst **aktualnego elementu i wszystkich elementów podrzędnych**:
  ```xml
  <Group DataContext="{Adres}">
    <!-- wszystkie pola wewnątrz odwołują się do właściwości obiektu Adres -->
    <Field CaptionHtml="Miasto" EditValue="{Miasto}" />
  </Group>
  ```

- **EditValue** - zmienia kontekst **tylko elementów podrzędnych** (bez aktualnego kontenera):
  ```xml
  <Grid EditValue="{Pozycje}">
    <!-- pola wewnątrz odwołują się do właściwości elementu kolekcji Pozycje -->
    <Field CaptionHtml="Nazwa" EditValue="{Nazwa}" />
  </Grid>
  ```

### Składnia wyrażeń w klamrach

W wyrażeniach `{...}` można odwoływać się do:

| Składnia | Opis |
|----------|------|
| `{Właściwość}` | Publiczna właściwość obiektu w kontekście |
| `{Metoda()}` | Wartość zwracana przez publiczną metodę. Parametry metody są uzupełniane automatycznie na podstawie aktualnego kontekstu UI (`Soneta.Business.Context`) |
| `{Obiekt.Właściwość}` | Właściwość zagnieżdżona |
| `{Context.TypDanych.Pole}` | Wartość z aktualnego kontekstu UI (`Soneta.Business.Context`) |
| `{Features.NazwaCechy}` | Cechy powiązane z obiektem biznesowym `Row` |
| `{Workers.NazwaWorkera.Pole}` | Właściwość workera powiązanego z obiektem (`WorkerAttribute`) |
| `{new NazwaExtender.Pole}` | Właściwość extendera (worker niepowiązany z konkretnymi danymi) |
| `{Tablica[indeks]}` | Element tablicy pod wskazanym indeksem |
| `{.}` | Aktualna wartość w kontekście elementu |

### Wyrażenia porównań

| Składnia | Opis |
|----------|------|
| `{Pole=wartość}` | Porównanie równości |
| `{Pole!=wartość}` | Porównanie nierówności |
| `{!PoleLogiczne}` | Negacja wartości logicznej |
| `{?warunek}` | Warunek oparty o klasę `RowCondition` |

### Przykłady bindingów

```xml
<!-- Proste właściwości -->
<Field CaptionHtml="Kod" EditValue="{Kod}" />
<Field CaptionHtml="Nazwa kontrahenta" EditValue="{Kontrahent.Nazwa}" />

<!-- Właściwości workerów -->
<Field CaptionHtml="Saldo" EditValue="{Workers.SaldoWorker.SaldoOgolem}" />

<!-- Extendery (obiekty tworzone dynamicznie) -->
<Field EditValue="{new MojExtender.Właściwość}" />
<Command MethodName="Wykonaj" DataContext="{new MojExtender}" />

<!-- Cechy -->
<Field CaptionHtml="Cecha" EditValue="{Features.MojaCecha}" />

<!-- Kontekst UI -->
<Field EditValue="{Context.Operator.Nazwa}" IsReadOnly="true" />

<!-- Aktualna wartość -->
<Field EditValue="{.}" />
```

### Wyrażenia warunkowe (RowCondition)

```xml
Visibility="{?State=Added}"              <!-- stan obiektu -->
Visibility="{?!State=Added}"             <!-- negacja -->
Visibility="{?Typ=Towar or Typ=Usługa}"  <!-- OR -->
Visibility="{?Aktywny and Widoczny}"     <!-- AND -->
```

## Atrybut Class - Style i zachowania

Atrybut `Class` może zawierać wiele wartości oddzielonych spacją:

```xml
<Field Class="BoldLabel LeftAlign ImageEdit" ... />
```

### Style etykiet
- `BoldLabel` - pogrubiona etykieta
- `CenterLabel` - wycentrowana
- `RightLabel` - wyrównana do prawej
- `MultilineLabel` - wieloliniowa
- `NoColonLabel` - bez dwukropka
- `WarningLabel` - ostrzeżenie
- `InfoLabel` - informacja
- `TipLabel` - podpowiedź

### Style czcionek
- `LargeFont` - duża czcionka
- `BoldFont` - pogrubiona
- `GreenFont` - zielona
- `RedFont` - czerwona
- `WarningFont` - ostrzegawcza
- `FixedWidthFont` - stała szerokość

### Wyrównanie
- `LeftAlign`, `RightAlign`, `TextRight`

### Typy edytorów
- `PasswordEdit` - hasło
- `ColorEdit` - wybór koloru
- `RichEdit` - edytor HTML
- `ImageEdit` - obraz
- `FileEdit` - wybór pliku
- `FolderEdit` - wybór folderu
- `PathPropertyEdit` - ścieżka
- `HyperlinkEdit` - hiperłącze
- `EmailEdit` - email
- `PhoneEdit` - telefon
- `RatingEdit` - ocena gwiazdkowa
- `ProgressEdit` - pasek postępu
- `IconEdit` - ikona
- `CheckButtonEdit` - checkbox jako przycisk
- `XmlEdit` - edytor XML
- `DataTextEdit` - tekst danych

### Zachowania
- `Collapsable` - zwijalna grupa
- `Expandable` - rozwijalna
- `Expanded` - domyślnie rozwinięta
- `Hidden` - ukryty
- `FirstResponder` - pole z fokusem zaraz po otwarciu formularza
- `Scrollable` - przewijalna
- `ImageCircle` - okrągły obraz
- `Tree` - drzewo

### Układy i pozycjonowanie
- `GroupItem` - element umieszczony na wysokości nagłówka `<Group>` (zwykle po prawej stronie), służy do sterowania zawartością pozostałych elementów grupy
- `Reverse` - układa elementy w odwrotnej kolejności (od prawej do lewej lub od dołu do góry)
- `SmartOpen` - kolumna w `Grid` wyróżniona do szybkiego otwierania formularza (ze strzałką)

### Przyciski
- `MainCommand` - główny przycisk
- `SplitCommand` - przycisk z menu
- `CommandNoText` - tylko ikona
- `CommandIcoText` - ikona i tekst
- `WorkerCommand` - przycisk workera
- `WizardCommand` - przycisk kreatora
- `PrintButton` - przycisk drukowania

## Appearance - Warunkowe formatowanie

```xml
<Field CaptionHtml="Saldo" EditValue="{Saldo}">
  <Appearance Condition="{?Saldo&lt;0}" ForeColor="Red" FontBold="true" />
  <Appearance Condition="{?Saldo&gt;1000}" BackColor="LightGreen" />
</Field>
```

## Przykłady

### Prosta zakładka (pageform.xml)

Plik: `MojObiekt.Ogolne.pageform.xml`

```xml
<?xml version="1.0" encoding="utf-8"?>
<DataForm xmlns="http://www.enova.pl/schema/form.xsd"
          xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
          xmlns:xsd="http://www.w3.org/2001/XMLSchema"
          xsi:schemaLocation="http://www.enova.pl/schema/ http://www.enova.pl/schema/form.xsd"
          Priority="10">
  <Page CaptionHtml="Ogólne" DataContext="{DataSource}">
    <Group CaptionHtml="Dane podstawowe">
      <Field CaptionHtml="Kod" Width="20" EditValue="{Kod}" />
      <Field CaptionHtml="Nazwa" Width="*" EditValue="{Nazwa}" />
      <Row>
        <Field CaptionHtml="Data" Width="15" EditValue="{Data}" />
        <Field CaptionHtml="Status" Width="15" EditValue="{Status}" />
      </Row>
    </Group>
    <Group CaptionHtml="Pozycje">
      <Grid Width="*" Height="*" EditValue="{Pozycje}" IsToolbarVisible="true">
        <Field CaptionHtml="Lp" Width="5" EditValue="{Lp}" />
        <Field CaptionHtml="Opis" Width="*" EditValue="{Opis}" />
        <Field CaptionHtml="Wartość" Width="15" EditValue="{Wartosc}" Footer="Sum" />
      </Grid>
    </Group>
  </Page>
</DataForm>
```

### Współdzielony fragment (form.xml)

Plik: `Adres.form.xml`

```xml
<?xml version="1.0" encoding="utf-8"?>
<DataForm xmlns="http://www.enova.pl/schema/form.xsd"
          xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
          xmlns:xsd="http://www.w3.org/2001/XMLSchema"
          xsi:schemaLocation="http://www.enova.pl/schema/ http://www.enova.pl/schema/form.xsd">
  <Stack>
    <Row>
      <Field CaptionHtml="Ulica" Width="40" EditValue="{Ulica}" />
      <Field CaptionHtml="Nr domu" Width="10" EditValue="{NrDomu}" />
    </Row>
    <Row>
      <Field CaptionHtml="Kod pocztowy" Width="12" EditValue="{KodPocztowy}" />
      <Field CaptionHtml="Miasto" Width="*" EditValue="{Miasto}" />
    </Row>
  </Stack>
</DataForm>
```

## Referencje

- Pełna specyfikacja elementów: [references/ELEMENTS.md](references/ELEMENTS.md)
- Schemat XSD: [references/Form.xsd](references/Form.xsd)
