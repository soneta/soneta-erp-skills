---
name: soneta-form-xml
description: "Specjalistyczna wiedza o WŁASNOŚCIOWYM formacie plików form.xml platformy enova365/Soneta — bez tego skilla Claude generuje błędne XML z nieistniejącymi elementami. ZAWSZE używaj tego skilla gdy użytkownik: (1) prosi o utworzenie lub modyfikację pliku pageform.xml, viewform.xml, form.xml, lookupform.xml lub gridform.xml dla enova365/Soneta; (2) pyta o elementy DataForm, Page, Group, Grid, Field, Row, Stack, Flow, Command, Include, Appearance, GroupBy w enova365; (3) pyta o składnię EditValue, DataContext, Visibility, RowCondition, Renderable, CaptionHtml, Footer, Class lub układ UI formularzy enova365; (4) pokazuje istniejący plik form.xml/pageform.xml/viewform.xml i pyta o jego strukturę lub chce go rozszerzyć; (5) pyta o warunkową widoczność, formatowanie warunkowe (Appearance), bindowanie danych lub wzorce UI w Soneta/enova365."
---

# Soneta Form XML - Formularze UI

## Typy plików formularzy

| Typ pliku | Wzorzec nazwy | Przeznaczenie |
|-----------|---------------|---------------|
| **pageform.xml** | `{DataType}.{PageName}.pageform.xml` | Zakładka formularza edycji obiektu |
| **viewform.xml** | `{NazwaWidoku}.viewform.xml` | Widok listy zarejestrowanej jako folder (listy główne) |
| **gridform.xml** | `{IdentyfikatorListy}.gridform.xml` | Indywidualne ustawienia listy na formularzu |
| **lookupform.xml** | `{NazwaPodpowiedzi}.lookupform.xml` | Lista wyboru (lookup) |
| **form.xml** | `{Nazwa}.form.xml` | Współdzielony fragment UI (include) |

**Przykłady pageform.xml:** `Towar.Ogolne.pageform.xml`, `Kontrahent.Adresy.pageform.xml`

## Struktura dokumentu

Każdy plik formularza zaczyna się od deklaracji XML i elementu `DataForm`:

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
| `RightName` | Opcjonalny. Nazwa uprawnienia do zakładki |
| `Contexts` | Warunki licencyjne, np. `"Licence.HAN \| Licence.FA"` |
| `ViewType` | Typ widoku: `None`, `Dialog`, `Form`, `Folder` |
| `Mode` | Tryb: `None`, `Form`, `Folder`, `Wizard`, `Modal`, `Popup`, `Frame` |
| `DataType` | Opcjonalny. Pełna nazwa typu gdy nie wynika z nazwy pliku |

## Wspólne atrybuty elementów

| Atrybut | Opis |
|---------|------|
| `Name` | Identyfikator elementu (dostępny w kodzie C#) |
| `Class` | Klasy stylów (lista wartości oddzielonych spacją) |
| `DataContext` | Zmienia kontekst danych dla elementu i elementów podrzędnych |
| `Visibility` | Warunek widoczności (bindowalne): `true`/`false`/`{wyrażenie}` |
| `Renderable` | Czy element ma być dostępny — liczone **raz** przy logowaniu |
| `Width` | Szerokość w znakach lub px; `"*"` = wypełnij |
| `Height` | Wysokość w wierszach lub px; `"*"` = wypełnij |

### Atrybut CaptionHtml

- Tekst etykiety (w formacie HTML)
- Wyrażenia bindowane: `{Właściwość}` → wartość automatycznie kodowana do HTML
- Wyrażenia HTML: `{WłaściwośćHtml}` → sufiks `Html` wyłącza kodowanie
- Podwójne klamry dla literałów: `{{` → `{`
- Alternatywa: `CaptionMarkdown` dla Markdown

**Specjalne przypadki w `Field`:**
- Brak atrybutu → automatyczna etykieta wyliczana na podstawie danych
- `CaptionHtml=" "` (spacja) → pusta etykieta (miejsce zachowane)
- `CaptionHtml=""` (pusty) → brak etykiety i miejsca

## Elementy kontenerowe

### Page - Zakładka

```xml
<Page CaptionHtml="Ogólne" DataContext="{DataSource}">
  <!-- zawartość zakładki -->
</Page>
```

| Atrybut | Opis |
|---------|------|
| `CaptionHtml` | Tytuł; może zawierać `/` do grupowania (np. `"Dokumenty/Faktury"`) |
| `DataContext` | Źródło danych; `{DataSource}` = obiekt edytowany |
| `Visibility` | Wyrażenie warunkowe widoczności (bindowalne) |
| `Renderable` | Liczone raz przy logowaniu — dla warunków licencji/środowiska |
| `Key` | Skrót klawiaturowy |

### Group - Grupa pól

```xml
<Group CaptionHtml="Dane podstawowe" LabelWidth="20">
  <Field CaptionHtml="Kod" Width="20" EditValue="{Kod}" />
  <Field CaptionHtml="Nazwa" Width="*" EditValue="{Nazwa}" />
</Group>
```

`LabelWidth` ustawia szerokość etykiet dla wszystkich pól w grupie.

### Stack, Row, Flow - Układ elementów

```xml
<!-- Stack: układ pionowy (elementy jeden pod drugim) -->
<Stack LabelWidth="15">
  <Field CaptionHtml="Pole 1" EditValue="{Pole1}" />
</Stack>

<!-- Row: układ poziomy -->
<Row>
  <Field CaptionHtml="Kod" Width="20" EditValue="{Kod}" />
  <Gap Width="*" />  <!-- przesuwa kolejne elementy do prawej -->
  <Field CaptionHtml="Status" Width="15" EditValue="{Status}" />
</Row>

<!-- Flow: elementy od lewej do prawej z zawijaniem -->
<Flow Align="true">
  <Field CaptionHtml="Data od" Width="15" EditValue="{DataOd}" />
  <Field CaptionHtml="Data do" Width="15" EditValue="{DataDo}" />
</Flow>
```

**Układ dwukolumnowy:** `<Row>` zawierający dwa `<Stack>`.

### Zasada budowania zakładki

```xml
<Page CaptionHtml="Ogólne" DataContext="{DataSource}">
  <Group CaptionHtml="Dane podstawowe">
    <!-- pola pionowo -->
    <Field CaptionHtml="Kod" Width="20" EditValue="{Kod}" />
    <!-- lub wielokolumnowo: -->
    <Row>
      <Stack><Field ... /></Stack>
      <Stack><Field ... /></Stack>
    </Row>
  </Group>
  <Group CaptionHtml="Pozycje">
    <Grid Width="*" Height="*" EditValue="{Pozycje}" IsToolbarVisible="true">
      <Field CaptionHtml="Nazwa" Width="*" EditValue="{Nazwa}" />
    </Grid>
  </Group>
</Page>
```

## Elementy pól i kontrolek

### Field - Pole edycyjne

Jest generowany **dynamicznie** — typ właściwości decyduje o kontrolce (int → liczba, bool → checkbox, typ Sonety → lookup).

```xml
<Field CaptionHtml="Nazwa pola" Width="20" EditValue="{Właściwość}"
       Important="true" IsReadOnly="{Warunek}" />
```

| Atrybut | Opis |
|---------|------|
| `EditValue` | **Wymagany**. Binding do właściwości: `{Właściwość}` lub `{new Ext.Właściwość}` |
| `CaptionHtml` | Etykieta pola |
| `Width` | Szerokość pola (`*` = wypełnij) |
| `Height` | Wysokość (dla pól wieloliniowych) |
| `Important` | `true` — pole oznaczone jako ważne (wyróżnione w widoku) |
| `IsReadOnly` | Warunek tylko do odczytu (bindowalne) |
| `Format` | Formatowanie w standardzie .NET: `N2`, `d`, `C` |
| `Footer` | Agregacja w stopce listy: `Sum`, `Count`, `Average`, `Min`, `Max` |
| `CheckedValue` | Wartość dla RadioButton |
| `Class` | Klasy stylów |

**RadioButton** — pola z tym samym `EditValue` i różnymi `CheckedValue`:
```xml
<Field Width="15" CaptionHtml="Towar" EditValue="{Typ}" CheckedValue="Towar" />
<Field Width="15" CaptionHtml="Usługa" EditValue="{Typ}" CheckedValue="Usługa" />
```

### Label, Gap, Command, Include

```xml
<Label CaptionHtml="Tekst informacyjny" Width="30" />
<Gap Width="*" />   <!-- wypełniacz; pusty na końcu wiersza nic nie zmienia -->

<Command CaptionHtml="Zapisz" MethodName="Zapisz"
         DataContext="{new MojExtender}" Visibility="{IsVisible}"
         CommandStyle="Important" Key="F5" />

<!-- Dołączenie pliku form.xml -->
<Include Source="Adres.form.xml" DataContext="{Adres}" />
<!-- Dynamiczny element z kodu C# -->
<Include Source="{DynamicznyFormularz}" />
```

## Kolekcje i listy

### Grid / List - Tabela danych

```xml
<Grid Width="*" Height="*"
      EditValue="{Pozycje}"
      IsToolbarVisible="true"
      IsFilterRowVisible="false"
      EditInPlace="true"
      NewInPlace="true"
      OrderBy="Data desc"
      SumType="All"
      FilterPanelWidth="136"
      SelectedValue="{WybranePozycje}"
      FocusedValue="{AktualnaPozycja}">
  <Field CaptionHtml="Kod" Width="15" EditValue="{Kod}" />
  <Field CaptionHtml="Ilość" Width="10" EditValue="{Ilosc}" Footer="Sum" />
  <GroupBy EditValue="{Kategoria}" IsDescending="false" />
  <UserFilter Value="Status='Aktywny'" />
  <Data Name="nazwaParametru" Value="wartość" />
</Grid>
```

| Atrybut | Default | Opis |
|---------|---------|------|
| `EditValue` | — | Źródło danych kolekcji |
| `IsToolbarVisible` | `false` | Pasek narzędzi |
| `IsFilterRowVisible` | `false` | Wiersz filtrujący |
| `EditInPlace` | `false` | Edycja bezpośrednio w komórkach |
| `NewInPlace` | `false` | Dodawanie przez kliknięcie pustego wiersza |
| `OrderBy` | — | Domyślne sortowanie: `"Kolumna desc"` |
| `FilterPanelWidth` | — | Szerokość panelu filtrów |
| `SumType` | `None` | `None`, `Selected`, `All`, `Groups`, `GroupsNewLine` |
| `IsSmartOpen` | — | Kolumna ze strzałką do otwarcia formularza |
| `VisibleFeatures` | — | Lista widocznych cech: `"Asortyment,Producent"` |
| `SelectedValue` | — | Binding do zaznaczonych wierszy (tablica) |
| `FocusedValue` | — | Binding do aktywnego wiersza |

**Przyciski:** `NewButton`, `EditButton`, `RemoveButton`, `SearchButton` — wartości `Auto`, `None`, `Visible`.

## Bindowanie danych

### Powiązanie typu z formularzem

1. **Przez nazwę pliku** — `Towar.Ogolne.pageform.xml` → kontekst klasy `Towar`
2. **Przez atrybut DataType** — `<DataForm DataType="Soneta.Handel.Towar,Soneta.Handel">`
3. **Przez rejestrację FolderViewAttribute** — dla viewform.xml

### Zmiana kontekstu danych

- `DataContext="{Adres}"` — zmienia kontekst **aktualnego elementu i podrzędnych**
- `EditValue="{Pozycje}"` na Grid — zmienia kontekst **tylko podrzędnych** (pola wewnątrz Grida)

### Składnia wyrażeń `{...}`

| Składnia | Opis |
|----------|------|
| `{Właściwość}` | Publiczna właściwość w kontekście |
| `{Obiekt.Właściwość}` | Właściwość zagnieżdżona |
| `{Obiekt+SubObiekt.Właściwość}` | Operator `+` — nawigacja przez powiązany obiekt ViewInfo |
| `{Workers.NazwaWorkera.Pole}` | Właściwość workera |
| `{new NazwaExtender.Pole}` | Właściwość extendera |
| `{Features.NazwaCechy}` | Cechy powiązane z Row |
| `{Context.TypDanych.Pole}` | Wartość z kontekstu UI (`Soneta.Business.Context`) |
| `{Licence.HAN}` | Warunek licencji (używany w `Renderable`) |
| `{.}` | Aktualna wartość w kontekście elementu |

### Wyrażenia warunkowe (RowCondition)

```xml
Visibility="{?State=Added}"             <!-- równość -->
Visibility="{?!State=Added}"            <!-- negacja -->
Visibility="{?Typ=Towar or Typ=Usługa}" <!-- OR -->
Visibility="{?Aktywny and Widoczny}"    <!-- AND -->
```

## Atrybut Class — najważniejsze wartości

**Style etykiet:** `BoldLabel`, `CenterLabel`, `RightLabel`, `WarningLabel`, `InfoLabel`, `NoColonLabel`

**Style czcionek:** `BoldFont`, `LargeFont`, `GreenFont`, `RedFont`

**Typy edytorów:** `PasswordEdit`, `RichEdit`, `ImageEdit`, `HyperlinkEdit`, `EmailEdit`, `PhoneEdit`, `ColorEdit`, `ProgressEdit`, `RatingEdit`, `FileEdit`

**Zachowania kontenerów:** `Collapsable`, `Expandable`, `Expanded`, `Scrollable`, `FirstResponder`

**Przyciski:** `MainCommand`, `SplitCommand`, `CommandNoText`, `CommandIcoText`

**Wyrównanie:** `LeftAlign`, `RightAlign`, `TextRight`

**Pozycjonowanie:** `GroupItem` — element na poziomie nagłówka Group (zwykle po prawej)

> Pełna lista Class: [references/ELEMENTS.md](references/ELEMENTS.md)

## Appearance - Warunkowe formatowanie

```xml
<Field EditValue="{Saldo}">
  <Appearance Condition="{?Saldo&lt;0}" ForeColor="Red" FontBold="true" />
  <Appearance Condition="{?Saldo&gt;1000}" BackColor="LightGreen" />
</Field>
```

Składnia `Condition` może używać nawiasów kwadratowych gdy nazwa pola zawiera spacje lub operator porównania wymaga jawnego typu:
```xml
<Appearance Condition="{?[Typ] = 'usługa'}" ForeColor="#800080" />
```

## Przykład: pageform.xml

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
    </Group>
    <Group CaptionHtml="Pozycje">
      <Grid Width="*" Height="*" EditValue="{Pozycje}" IsToolbarVisible="true">
        <Field CaptionHtml="Lp" Width="5" EditValue="{Lp}" />
        <Field CaptionHtml="Wartość" Width="15" EditValue="{Wartosc}" Footer="Sum" />
      </Grid>
    </Group>
  </Page>
</DataForm>
```

## Przykład: viewform.xml z FilterPanel

Widok listy z panelem filtrów powyżej grida. `<Flow>` jako `FilterPanel` to standardowy wzorzec.

```xml
<?xml version="1.0" encoding="utf-8"?>
<DataForm xmlns="http://www.enova.pl/schema/form.xsd"
          xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
          xmlns:xsd="http://www.w3.org/2001/XMLSchema"
          xsi:schemaLocation="http://www.enova.pl/schema/ http://www.enova.pl/schema/form.xsd">
  <Flow Name="FilterPanel">
    <Field CaptionHtml="Status" Width="15" EditValue="{ViewInfo+Params.Status}" Important="true" />
    <Field CaptionHtml="Typ" Width="12" EditValue="{ViewInfo+Params.Typ}" Important="true" />
    <Field CaptionHtml="Data od" Width="14" EditValue="{ViewInfo+Params.DataOd}" />
  </Flow>
  <Grid Name="List" OrderBy="Nazwa" FilterPanelWidth="136"
        IsToolbarVisible="true" IsFilterRowVisible="false">
    <Appearance Condition="{?[Status] = 'nieaktywny'}" ForeColor="#808080" />
    <Field CaptionHtml="Kod" Width="20" EditValue="{Kod}" />
    <Field CaptionHtml="Nazwa" Width="30" EditValue="{Nazwa}" />
    <Field CaptionHtml="Status" Width="15" EditValue="{Status}" />
    <UserFilter Value="Status='Aktywny'" />
  </Grid>
</DataForm>
```

**Uwaga:** Pliki `viewform.xml` zawierają Grid bezpośrednio w `DataForm` (bez `Page`). Atrybuty `ViewType` i `Mode` na `DataForm` są opcjonalne — dodaj je tylko gdy rejestrujesz widok przez `FolderViewAttribute`.

## Współdzielony fragment (form.xml)

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

Dołączanie: `<Include Source="Adres.form.xml" DataContext="{Adres}" />`

## Referencje

- Pełna specyfikacja elementów: [references/ELEMENTS.md](references/ELEMENTS.md)
- Schemat XSD: [references/Form.xsd](references/Form.xsd)
- Przykład pageform.xml: [assets/MojObiekt.Ogolne.pageform.xml](assets/MojObiekt.Ogolne.pageform.xml)
- Przykład pageform.xml (warunki handlowe): [assets/Kontrahent.WarunkiHandlowe.pageform.xml](assets/Kontrahent.WarunkiHandlowe.pageform.xml)
- Przykład form.xml (adres): [assets/Adres.form.xml](assets/Adres.form.xml)
