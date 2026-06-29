# Pełna specyfikacja elementów Form XML

Kompletna lista elementów XML dostępnych w plikach form.xml platformy Soneta.

## Spis treści

1. [Elementy kontenerowe](#elementy-kontenerowe)
2. [Elementy pól](#elementy-pól)
3. [Elementy kolekcji](#elementy-kolekcji)
4. [Elementy wykresów i wizualizacji](#elementy-wykresów-i-wizualizacji)
5. [Elementy specjalne](#elementy-specjalne)
6. [Typy wyliczeniowe (Enum)](#typy-wyliczeniowe)
7. [Wspólne atrybuty (uiElement)](#wspólne-atrybuty)

---

## Elementy kontenerowe

### Stack
Układ pionowy - elementy jeden pod drugim.

```xml
<Stack LabelWidth="15" Visibility="{Warunek}">
  <!-- elementy potomne -->
</Stack>
```

Dziedziczy atrybuty z `containerElement`.

### Row
Układ poziomy - elementy obok siebie.

```xml
<Row OuterWidth="80" Visibility="{Warunek}">
  <!-- elementy potomne -->
</Row>
```

### Flow
Układ płynny z automatycznym zawijaniem.

```xml
<Flow Align="true" Arrange="Horizontally">
  <!-- elementy potomne -->
</Flow>
```

| Atrybut | Typ | Opis |
|---------|-----|------|
| `Align` | boolean | Wyrównanie elementów |
| `Arrange` | enum | `Horizontally` lub `Vertically` |

### Group
Wizualna ramka grupująca elementy.

```xml
<Group CaptionHtml="Nazwa grupy" LabelWidth="20" DescriptionHtml="{Opis}">
  <!-- elementy potomne -->
</Group>
```

### Page
Zakładka formularza.

```xml
<Page Name="NazwaPage" 
      CaptionHtml="Tytuł zakładki" 
      DataContext="{DataSource}"
      Key="klucz"
      MultiDataSource="źródło"
      GroupIcon="ikona"
      DefaultVisible="true"
      FirstAction="akcja">
  <!-- zawartość zakładki -->
</Page>
```

| Atrybut | Typ | Opis |
|---------|-----|------|
| `Name` | string | Unikalny identyfikator |
| `CaptionHtml` | string | Tytuł zakładki |
| `Key` | string | Klucz zakładki |
| `MultiDataSource` | string | Wielokrotne źródło danych |
| `GroupIcon` | string | Ikona grupy |
| `DefaultVisible` | boolean | Domyślna widoczność |
| `FirstAction` | string | Pierwsza akcja |

### Bar
Pasek narzędzi.

```xml
<Bar>
  <Command CaptionHtml="Akcja" MethodName="Wykonaj" />
</Bar>
```

### Dashboard
Panel kafelkowy/kokpit.

```xml
<Dashboard TileChangedMethodName="OnTileChanged" 
           FocusedValue="{FocusedTile}"
           ArrangeMode="Default">
  <Group Class="DashboardItem">
    <!-- zawartość kafelka -->
  </Group>
</Dashboard>
```

| Atrybut | Typ | Wartości |
|---------|-----|----------|
| `ArrangeMode` | enum | `Default`, `Canvas`, `Size`, `Visibility` |

### Include
Dołączenie zewnętrznego fragmentu lub dynamicznie generowanego elementu UI.

```xml
<!-- Statyczne dołączenie pliku -->
<Include Source="Adres.form.xml" />

<!-- Z kontekstem danych -->
<Include Source="Adres.form.xml" DataContext="{Adres}" Suffix="Koresp" />

<!-- Dynamiczny element z kodu C# -->
<Include Source="{DynamicznyFormularz}" />
```

| Atrybut | Wymagany | Opis |
|---------|----------|------|
| `Source` | **Tak** | Nazwa pliku form.xml **lub** wyrażenie bindujące zwracające element DOM (`UIElement`). Wyrażenie jest wywoływane po każdej zmianie danych - umożliwia dynamiczne formularze |
| `DataContext` | Nie | Kontekst danych dla dołączanego fragmentu |
| `Path` | Nie | Określa element pliku form.xml do wstawienia (gdy Source jest nazwą pliku) |
| `Suffix` | Nie | Napis dodawany do każdej nazwy (`Name`) elementów dołączonych przez Include |

---

## Elementy pól

### Field
Podstawowe pole edycyjne.

```xml
<Field CaptionHtml="Etykieta"
       CaptionMarkdown="Etykieta _z formatowaniem_"
       EditValue="{Właściwość}"
       Width="20"
       OuterWidth="50"
       LabelWidth="15"
       Height="3"
       LabelHeight="2"
       Format="N2"
       IsReadOnly="{Warunek}"
       Visibility="{Warunek}"
       CheckedValue="Wartość"
       Footer="Sum"
       Aggregate="Average"
       Important="true"
       Class="BoldLabel LeftAlign" />
```

| Atrybut | Typ | Opis |
|---------|-----|------|
| `Format` | string | Format wyświetlania (np. `N2`, `d`, `C`) |
| `CheckedValue` | string | Wartość dla RadioButton |
| `Footer` | enum | Agregacja w stopce |
| `Aggregate` | enum | Typ agregacji |
| `Important` | boolean | Oznaczenie jako ważne |

### Label
Etykieta tekstowa (tylko do odczytu).

```xml
<Label CaptionHtml="Tekst etykiety" 
       Width="30"
       Class="BoldLabel CenterLabel" />
```

### Gap
Odstęp/wypełniacz.

```xml
<Gap Width="*" Height="10" />
```

### Splitter
Rozdzielacz paneli.

```xml
<Splitter Class="HorizontalSplitter" />
```

### Data
Element danych (niewidoczny).

```xml
<Data Name="nazwaParametru" Value="wartość" EditValue="{Binding}" />
```

---

## Elementy poleceń

### Command
Przycisk/polecenie.

```xml
<Command CaptionHtml="Tekst przycisku"
         MethodName="NazwaMetody"
         MoreMethodName="MetodaDodatkowa"
         Key="F5"
         CommandStyle="Important"
         DataContext="{new MojExtender}"
         Visibility="{IsVisible}"
         Icon="ikona">
  <!-- zagnieżdżone polecenia (submenu) -->
  <Command CaptionHtml="Podmenu" MethodName="Akcja" />
  <Group CaptionHtml="Grupa poleceń">
    <Command ... />
  </Group>
</Command>
```

| Atrybut | Typ | Wartości/Opis |
|---------|-----|---------------|
| `CommandStyle` | enum | `Default`, `Important`, `Red`, `Green`, `Blue` |
| `Key` | string | Skrót klawiszowy |

`MethodName`/`OpenMethodName` wskazują metodę w kontekście (zwykle extender/worker). Co taka akcja zwraca (action result) opisuje skill `/soneta-programming` (action-result.md, worker-extender.md).

---

## Elementy kolekcji

### Grid / List
Tabela danych.

```xml
<Grid Width="*" Height="*"
      EditValue="{Kolekcja}"
      SelectedValue="{Zaznaczony}"
      FocusedValue="{Aktywny}"
      OrderBy="Kolumna desc"
      Filter="Warunek"
      IsToolbarVisible="true"
      IsDateNavigatorVisible="false"
      IsSmartFilterVisible="true"
      IsFilterRowVisible="false"
      FilterPanelWidth="200"
      VisibleFeatures="search,filter"
      LocatorFields="Kod,Nazwa"
      EditInPlace="true"
      ForceEditInPlace="false"
      NewInPlace="true"
      AlwaysAddNewRow="false"
      PreventNewRowOnFocus="false"
      OpenMethodName="Otwórz"
      SequenceMethodName="Sekwencja"
      IsSmartOpen="true"
      IsNonOptimalWarning="false"
      NewButton="Visible"
      EditButton="Auto"
      UpdateButton="None"
      RemoveButton="Visible"
      SearchButton="Auto"
      MoreButton="Actions"
      SumType="All"
      ActionsMode="FormAndControl"
      ResourceName="NazwaZasobu"
      TreeNodesValue="{Węzły}"
      TreeHasNodesValue="{MaWęzły}"
      TreeParentValue="{Rodzic}"
      TreeExpandingLevel="ExpandRoot"
      FocusedColumnValue="{AktywnaKolumna}"
      IsHeaderVisible="true"
      KeepsSequence="false"
      IgnoreChangingInSort="Kolumna"
      SortAfterEditInPlace="true"
      DragAndDrop="true"
      AllowCellSelection="false"
      SelectedCellsValue="{ZaznaczoneKomórki}"
      Name="nazwaGrida">
  
  <Field CaptionHtml="Kolumna" Width="20" EditValue="{Pole}" Footer="Sum" />
  <GroupBy EditValue="{Grupowanie}" IsDescending="false" PreventSorting="false" />
  <Data Name="param" Value="wartość" />
  <UserFilter Value="filtr" />
</Grid>
```

Gdy `EditValue` wskazuje property zwracającą `ViewInfo` (a nie prostą kolekcję), zawartość, filtr i blokady listy buduje kod — jak zbudować ViewInfo jako property/folder opisuje skill `/soneta-programming` (viewinfo.md). `VisibleFeatures` odwołuje się do mechanizmu cech — patrz skill `/soneta-programming` (features.md).

#### Atrybuty przyciski (enumCollectionButtonState)
- `Auto` - automatycznie
- `None` - ukryty
- `Visible` - widoczny

#### TreeExpandingLevel
- `Collapsed` - zwinięte
- `ExpandRoot` - rozwiń korzeń
- `ExpandRootFix` - rozwiń korzeń (stałe)
- `ExpandAll` - rozwiń wszystko

#### SumType (enumCollectionSumType)
- `None` - brak
- `Selected` - zaznaczone
- `All` - wszystkie
- `Groups` - grupy
- `GroupsNewLine` - grupy w nowej linii

#### ActionsMode
- `FormAndControl` - formularz i kontrolka
- `Control` - tylko kontrolka
- `Form` - tylko formularz

### Cards
Widok kart (kafelków). Dziedziczy z Grid, ale **nie obsługuje**: układu kolumnowego, nagłówka, sortowania przez kliknięcie, edit-in-place, struktury drzewiastej.

```xml
<Cards Name="List" IsSmartOpen="true" MoreButton="Visible" Class="DisableSelection">
  <Row>
    <Field CaptionHtml="" Height="7" Width="22" EditValue="{DefaultImage}" Class="ImageEdit" />
    <Stack>
      <Field CaptionHtml="" Width="50" Height="2" EditValue="{Nazwa}" Class="LargeFont BoldFont" />
      <Row>
        <Field CaptionHtml="" Width="20" EditValue="{Kod}" />
        <Stack>
          <Field CaptionHtml="" Width="27" EditValue="{Cena}" />
          <Field CaptionHtml="" Width="27" EditValue="{Masa}" />
        </Stack>
      </Row>
    </Stack>
  </Row>
</Cards>
```

Elementy `Field` domyślnie układają się jeden pod drugim. Można grupować je za pomocą `Row` i `Stack`.

**Układ szeroki:** Dodanie `Width="*"` do najbardziej zewnętrznego kontenera rozciąga element na całą szerokość (elementy listy jeden pod drugim).

#### Pasek narzędziowy Cards
- Umiejscowiony powyżej listy z prawej strony
- Posiada tylko przyciski Dodaj i Usuń (brak Otwórz)
- Ikony zaznaczania, otwierania i menu widoczne po najechaniu myszą na kafelek

#### Atrybuty kontrolujące ikony:
- `Class="DisableSelection"` - wyłącza ikonę zaznaczania
- `IsSmartOpen="true"` - włącza przycisk otwierania formularza
- `MoreButton="Visible"` - włącza przycisk "więcej" z dodatkowymi akcjami

### CardTemplate
Szablon karty (dziedziczy z Stack).

---

## Elementy wykresów i wizualizacji

### Chart
Wykres. Element służący do prezentacji danych w postaci wykresów.

```xml
<Chart EditValue="{Dane}"
       Type="Bar"
       ChartColor="Blue"
       IsLegendVisible="true"
       LabelFormat="{value} ({percent})"
       XAxisTitle="Oś X"
       YAxisTitle="Oś Y"
       XAxisLabelFormat="{value} EUR"
       YAxisLabelFormat="{value:N0}"
       OpenMethodName="GetSerieInfo"
       FocusedValue="{FocusedValue}"
       FocusedColumnValue="{FocusedColumn}">
  
  <Axis CaptionHtml="Kategoria" EditValue="{Kategoria}" Dimension="Enum" />
  <Field CaptionHtml="Wartość" EditValue="{Wartosc}" Aggregate="Sum" />
</Chart>
```

| Atrybut | Typ | Opis |
|---------|-----|------|
| `Type` | enum | Typ wykresu (patrz poniżej) |
| `ChartColor` | enum | Kolor wykresu: `None` (wielokolorowy), `Green`, `Blue`, `Grey`, `Red`, `Orange`, `Yellow` |
| `IsLegendVisible` | boolean | Widoczność legendy |
| `LabelFormat` | string | Format etykiety. Zmienne: `{value}`, `{percent}`, `{label}` |
| `XAxisTitle` | string | Podpis osi X |
| `YAxisTitle` | string | Podpis osi Y |
| `XAxisLabelFormat` | string | Format wartości na osi X (zmienne: `{value}`, `{label}`) |
| `YAxisLabelFormat` | string | Format wartości na osi Y |
| `IsDateNavigatorVisible` | boolean | Nawigator daty dla WeekByDays/MonthByDays |
| `OpenMethodName` | string | Metoda wywoływana po kliknięciu na wykres |
| `FocusedValue` | string | Property do którego zostanie podstawiony wiersz po kliknięciu |
| `FocusedColumnValue` | string | Property z nazwą kolumny po kliknięciu (dla wielu Field) |

#### Typy wykresów (enumChartType)
- `Line` - liniowy (zmiany wartości między kategoriami)
- `Bar` - słupkowy/kolumnowy (porównanie kategorii)
- `Area` - warstwowy (suma serii i udział każdej)
- `Pie` - kołowy (proporcje części całości)
- `Donut` - pierścieniowy (jak kołowy, ale z dziurą)
- `Spider` - radarowy (porównanie wielu zmiennych)
- `Polar` - radarowy z wypełnieniem
- `Pyramid` - piramida (kategorie od największej do najmniejszej)
- `Funnel` - lejek (malejące proporcje)
- `Bubble` - bąbelkowy (3 wymiary: X, Y, rozmiar)
- `Scatter` - punktowy (zbiór punktów X/Y)

#### Axis - Oś wykresu

```xml
<Axis CaptionHtml="Etykieta" 
      EditValue="{Pole}" 
      Dimension="ByMonths"
      Grouping="true"
      FirstDayOfWeek="Monday" />
```

| Atrybut | Opis |
|---------|------|
| `Direction` | Kierunek dla Pivot: `X` (kolumny), `Y` (wiersze) |
| `Dimension` | Sposób grupowania miar |
| `Grouping` | Czy grupować dane (tworzy tabelę przestawną) |
| `FirstDayOfWeek` | Pierwszy dzień tygodnia |

#### Dimension (PivotDimension)
- `Enum` - wartości dyskretne, tekstowe (Bar, Pie, Donut)
- `AZ` - grupowanie po pierwszej literze
- `WeekByDays` - zawężenie do tygodnia + przyciski Następny/Poprzedni
- `MonthByDays` - zawężenie do miesiąca + przyciski Następny/Poprzedni
- `ByNumbers` - miara liczbowa, proporcjonalne rozmieszczenie
- `ByDays` - daty bez grupowania, wszystkie dni z zakresu
- `ByWeeks` - grupowanie wg tygodni
- `ByMonths` - grupowanie wg miesięcy
- `ByYears` - grupowanie wg lat

#### Field w Chart - seria danych

```xml
<Field CaptionHtml="Nazwa serii" EditValue="{Wartosc}" Aggregate="Sum" />
```

| Atrybut | Wartości |
|---------|----------|
| `Aggregate` | `None`, `Sum`, `Count`, `Average`, `Min`, `Max` |

#### Zasady tworzenia różnych typów wykresów

**Line/Area (liniowy/warstwowy):**
- 1 Axis + wiele Field → wiele serii danych
- 2 Axis (jeden z `Grouping="true"`) + 1 Field → serie wg grupowania

**Bar (słupkowy):**
- 1 Axis + 1 Field → wykres zwykły
- 1 Axis + wiele Field → wykres skumulowany (stacked)
- 2 Axis + 1 Field → wykres grupowany (slide)
- 2 Axis + wiele Field → stacked-slide

**Pie/Donut (kołowy/pierścieniowy):**
- Zawsze 1 Axis + 1 Field

**Scatter (punktowy):**
- 1 Axis + 2 Field (pierwszy Field = X, drugi Field = Y)

**Bubble (bąbelkowy):**
- 1 Axis + 3 Field (X, Y, rozmiar bąbelka)

### Scheduler
Kalendarz/harmonogram.

```xml
<Scheduler EditValue="{Zdarzenia}"
           View="Weekly"
           AllowViewChanging="true"
           SelectedInterval="{WybranyOkres}"
           VisibleInterval="{WidocznyOkres}"
           WorkInterval="{OkresPracy}"
           Resources="{Zasoby}"
           SelectedResource="{WybranyZasób}"
           HideDateField="false">
  <Field CaptionHtml="Tytuł" EditValue="{Tytul}" />
</Scheduler>
```

**UWAGA:** Niepoprawne wiązanie danych skutkuje błędem krytycznym (brak wyświetlenia pageform'a lub brak możliwości logowania).

| Atrybut | Typ | Opis |
|---------|-----|------|
| `View` | enum | Domyślny widok kalendarza |
| `AllowViewChanging` | boolean | Czy operator może zmieniać widok (domyślnie tak) |
| `SelectedInterval` | string | Okres zaznaczony przez użytkownika |
| `VisibleInterval` | string | Okres widoczny |
| `WorkInterval` | string | Roboczy czas dnia (domyślnie `"9:00..18:00"`). Można podać bezpośrednio lub bindować do `Soneta.Types.Interval` |
| `Resources` | string | Zasoby |
| `SelectedResource` | string | Wybrany zasób |
| `HideDateField` | boolean | Ukryj pole daty |

#### Widoki Scheduler (enumSchedulerViews)
- `Daily` - widok jednego dnia
- `WorkWeek` - widok tygodnia roboczego
- `Weekly` - widok tygodniowy
- `Monthly` - widok miesięczny
- `HoursTimeLine` - widok godzinowy na osi poziomej
- `DaysTimeLine` - widok dzienny na osi poziomej
- `SimpleMonthly` - uproszczony widok miesięczny

### Gantt
Wykres Gantta.

```xml
<Gantt EditValue="{Zadania}"
       ViewType="WeekDay"
       ViewMode="Standard"
       AllowViewChanging="true"
       Resources="{Zasoby}"
       MethodsHandler="Handler">
  <Field EditValue="{Nazwa}" />
  <Field EditValue="{Start}" />
  <Field EditValue="{Finish}" />
</Gantt>
```

#### GanttViews
- `HourMinute`, `DayHour`, `WeekDay`, `MonthDay`

#### GanttViewMode
- `Standard`, `ResourceView`

### GanttDiagram
Diagram Gantta z dodatkowymi opcjami.

```xml
<GanttDiagram EditValue="{Zadania}"
              WorkTimeStart="08:00"
              WorkTimeFinish="17:00"
              WorkOnSaturday="false"
              WorkOnSunday="false"
              MarkHolidays="true" />
```

### Diagram
Diagram ogólny.

```xml
<Diagram EditValue="{Elementy}" MethodsHandler="DiagramHandler" />
```

### CustomDiagram
Diagram niestandardowy.

### TreeDiagram
Diagram drzewa.

```xml
<TreeDiagram EditValue="{Węzły}"
             LayoutType="OrganizationalChart"
             MethodsHandler="TreeHandler" />
```

#### TreeDiagramLayoutType
- `OrganizationalChart` - schemat organizacyjny
- `ComplexHierarchicalTree` - złożone drzewo hierarchiczne

### KanbanDiagram
Tablica Kanban.

```xml
<KanbanDiagram EditValue="{Zadania}" MethodsHandler="KanbanHandler" />
```

### Pivot
Tabela przestawna.

```xml
<Pivot EditValue="{Dane}">
  <Axis Direction="X" EditValue="{Kategoria}" />
  <Axis Direction="Y" EditValue="{Wartość}" />
</Pivot>
```

### PivotGrid
Siatka przestawna.

---

## Elementy specjalne

### Indicator
Wskaźnik liczbowy.

```xml
<Indicator CaptionHtml="Sprzedaż"
           EditValue="{Wartość}"
           UnitSymbol="PLN"
           TimeSpanSymbol="miesiąc"
           MethodName="Szczegóły" />
```

### PercentIndicator
Wskaźnik procentowy.

```xml
<PercentIndicator EditValue="{Procent}"
                  StrokeWidth="10"
                  StrokeColor="Green" />
```

### CircularIndicator
Wskaźnik kołowy.

```xml
<CircularIndicator EditValue="{Dane}"
                   Title="Tytuł"
                   LegendPosition="Right" />
```

#### UIAlignment
- `None`, `Left`, `Right`, `Bottom`

### RangeSlider
Suwak zakresu.

```xml
<RangeSlider EditValue="{Zakres}"
             Min="0"
             Max="100"
             Step="5" />
```

### TimelineClock
Oś czasu z zegarem.

```xml
<TimelineClock EditValue="{Zdarzenia}" LegendPosition="Bottom" />
```

### DateNavigator
Navigator dat.

```xml
<DateNavigator EditValue="{WybranaData}" />
```

### Chips
Etykiety/tagi.

```xml
<Chips EditValue="{Tagi}" BgColor="GreenPastel" />
```

#### ChipsColorPalette
- `None`, `Black`, `White`
- `GrayPastel`, `GreenPastel`, `OrangePastel`, `RedPastel`

### Html
Treść HTML.

```xml
<Html EditValue="{TrescHtml}" 
      Width="*" 
      Height="200"
      ScrollBarsEnabled="true" />
```

### Markdown
Treść Markdown.

```xml
<Markdown EditValue="{TrescMd}" ScrollBarsEnabled="true" />
```

### ThreadComments
Komentarze wątkowe.

```xml
<ThreadComments EditValue="{Komentarze}" MethodsHandler="CommentsHandler" />
```

### UserControl
Kontrolka użytkownika.

```xml
<UserControl TypeName="MojaKontrolka">
  <Data Name="Param1" Value="Wartość" />
</UserControl>
```

### Template
Szablon warunkowy.

```xml
<Template Visibility="{Warunek}" AllowUpdating="true">
  <!-- zawartość wyświetlana gdy warunek spełniony -->
</Template>
```

---

## Typy wyliczeniowe

### enumAggregationType (Footer/Aggregate)
- `Auto` - automatycznie
- `None` - brak
- `Sum` - suma
- `Count` - liczba
- `Average` - średnia
- `Min` - minimum
- `Max` - maksimum
- `Concat` - konkatenacja
- `UniqueConcat` - unikalna konkatenacja
- `Same` - ta sama wartość

### enumDataFormView (ViewType)
- `None`, `Dialog`, `Form`, `Folder`

### enumDataFormMode (Mode)
- `None`, `Form`, `Folder`, `Wizard`, `Modal`, `Popup`, `Frame`

### enumLayoutMode
- `Default`, `Canvas`, `Size`, `Visibility`

### enumMoreButtonState
- `None`, `Visible`, `Actions`

---

## Wspólne atrybuty (uiElement)

Wszystkie elementy UI dziedziczą następujące atrybuty z `uiElement`:

| Atrybut | Typ | Opis |
|---------|-----|------|
| `Name` | string | Identyfikator elementu (musi być unikalny na poziomie strony). Automatycznie generowany jeśli nie podano. |
| `DataContext` | string (bindowalne) | Kontekst danych. `{DataSource}` = otwarty obiekt. Można wskazać podrzędną właściwość. |
| `EditValue` | string (bindowalne) | Binding do wartości |
| `CaptionHtml` | string (bindowalne) | Etykieta kontrolki (tekst lub `{Właściwość}`) |
| `CaptionMarkdown` | string | Etykieta Markdown |
| `DescriptionHtml` | string | Dodatkowy opis |
| `LangContext` | string | Kontekst językowy |
| `Icon` | string | Ikona |
| `Class` | string | Klasy stylów (lista wartości oddzielonych spacją) |
| `Width` | string | Szerokość w znakach lub px (np. `"50px"`). `"*"` = dopasuj do dostępnej przestrzeni |
| `Height` | string | Wysokość w wierszach lub px. `"*"` = dopasuj automatycznie |
| `OuterWidth` | string | Całkowita szerokość z etykietą (do wyrównywania kontrolek) |
| `OuterHeight` | string | Całkowita wysokość (może dodawać puste miejsce pod kontrolką) |
| `LabelWidth` | string | Szerokość etykiety |
| `LabelHeight` | string | Wysokość etykiety |
| `Column` | integer | Kolumna (w siatce) |
| `Row` | integer | Wiersz (w siatce) |
| `Visibility` | string (bindowalne) | Widoczność: `Visible`, `Hidden`, `Collapsed`, `true`, `false` |
| `LayoutMode` | enum | Tryb układu: `Default`, `Canvas`, `Size`, `Visibility` |
| `IsReadOnly` | string (bindowalne) | Tylko do odczytu (`true`/`false`). **Uwaga:** tryb z logiki biznesowej ma wyższy priorytet |
| `Tag` | string | Dodatkowa wartość do wykorzystania w skryptach |
| `TagInfo` | string | Informacja tagu |
| `Priority` | integer | Priorytet |
| `RenderMethodName` | string | Metoda renderowania |
| `Renderable` | string | Czy renderowalne |
| `RenderKey` | string | Klucz renderowania |

### Appearance (formatowanie warunkowe)

```xml
<Field EditValue="{Wartość}">
  <Appearance Condition="{?Wartość&lt;0}"
              BackColor="LightPink"
              ForeColor="Red"
              FontBold="true"
              FontItalic="false" />
</Field>
```

| Atrybut | Typ | Opis |
|---------|-----|------|
| `Condition` | string | Warunek (wyrażenie) |
| `BackColor` | string | Kolor tła |
| `ForeColor` | string | Kolor tekstu |
| `FontBold` | boolean | Pogrubienie |
| `FontItalic` | boolean | Kursywa |

**Składnia warunku Appearance** — dwie formy:
```xml
<!-- Forma 1: krótka, bez spacji (dla enumów i wartości bez spacji) -->
<Appearance Condition="{?Typ=usługa}" ForeColor="#800080" />

<!-- Forma 2: z nawiasami kwadratowymi i cudzysłowami (dla stringów, gdy pole ma spacje) -->
<Appearance Condition="{?[Typ] = 'usługa'}" ForeColor="#800080" />
```

Wyrażenie `Condition` (jak `Visibility="{?...}"`) to forma RowCondition; stronę kodu (`Expression<Predicate<TRow>>`) opisuje skill `/soneta-programming` (rowcondition.md) — to dwie strony tego samego pojęcia: XML vs C#.

---

## Składnia wyrażeń bindowania

### Operator `+` — nawigacja przez powiązane obiekty

Stosowany gdy widok listy (`viewform.xml`) korzysta z obiektu ViewInfo który agreguje parametry:

```xml
<!-- ViewInfo+Params.Właściwość — dostęp do parametrów widoku -->
<Field EditValue="{CennikViewInfo+CennikParams.Magazyn}" />
<Field EditValue="{ZamowieniaViewInfo+ZamowieniaParams.Status}" />
```

Wzorzec `{ObiektViewInfo+TypParams.Właściwość}` jest typowy dla paneli filtrów w viewform.xml — gdzie `+` łączy obiekt ViewInfo z typem jego pola będącego obiektem parametrów.

Stronę kodu opisuje skill `/soneta-programming`: budowę ViewInfo (viewinfo.md) oraz klasę parametrów `Params : ContextBase` (contextbase.md, context.md). Workery i extendery z bindów `{Workers.X.Y}` / `{new Ext.Y}` — worker-extender.md; cechy `{Features.X}` — features.md.

---

## Wartości Class (enumSingleClass)

### Zachowania kontenerów
- `Collapsable` - zwijalna
- `Expandable` - rozwijalna
- `Expanded` - rozwinięta
- `Dialog` - okno dialogowe
- `NoSave` - bez zapisu
- `MainPage` - strona główna
- `Menu` - menu
- `Panel`, `PanelItem`, `PanelWinItem`
- `NoLayout` - bez układu
- `RemoveEmpty` - usuń puste
- `GroupItem` - element grupy
- `Reverse` - odwrócony
- `MainGroup`, `MenuGroup`
- `NoClose` - bez zamykania
- `CascadeMenu`, `CascadeMenuGroup`, `CascadeSubmenuGroup`
- `FilterGroup` - grupa filtrów
- `Scrollable` - przewijalna

### Style przycisków
- `PrintButton` - drukowanie
- `MainCommand` - główne polecenie
- `SplitCommand` - polecenie dzielone
- `CommandNoText` - bez tekstu
- `CommandIcoText` - ikona i tekst
- `WorkerCommand` - polecenie workera
- `WizardCommand` - polecenie kreatora
- `SchedulerCommand` - polecenie harmonogramu
- `FormNavigationCommand` - nawigacja formularza

### Style pól
- `PasswordEdit`, `PathPropertyEdit`, `PropertyGridEdit`
- `CheckButtonEdit`, `ColorEdit`, `HistoryEdit`
- `RichEdit`, `AspxEdit`, `FolderEdit`, `FileEdit`
- `SaveFileEdit`, `ImageEdit`, `XmlEdit`
- `AlgorithmEdit`, `DataTextEdit`, `ConditionEdit`
- `RatingEdit`, `SignatureEdit`, `OneTimePasswordEdit`
- `HyperlinkEdit`, `EmailEdit`, `PhoneEdit`
- `SkypeEdit`, `GpsEdit`, `IconEdit`
- `ProgressEdit`, `TrafficLightEdit`

### Style etykiet
- `NoColonLabel` - bez dwukropka
- `BoldLabel` - pogrubiona
- `CenterLabel` - wyśrodkowana
- `RightLabel` - wyrównana w prawo
- `WarningLabel` - ostrzeżenie
- `MultilineLabel` - wieloliniowa
- `InfoLabel` - informacja
- `TipLabel` - podpowiedź
- `SchedulerLabel` - etykieta harmonogramu

### Style czcionek
- `LargeFont` - duża
- `GreenFont` - zielona
- `RedFont` - czerwona
- `FixedWidthFont` - stała szerokość
- `BoldFont` - pogrubiona
- `WarningFont` - ostrzegawcza

### Wyrównanie
- `RightAlign` - do prawej
- `LeftAlign` - do lewej
- `TextRight` - tekst do prawej

### Inne
- `Information`, `Question` - typy komunikatów
- `DataBar`, `PreviewLine`, `Header`
- `FirstResponder` - pierwszy fokus
- `SmartOpen` - inteligentne otwieranie
- `Tight`, `Special`, `Important`
- `LateCalculate` - późne obliczanie
- `AllowDragging`, `AllowEditing`
- `DisableSelection` - wyłącz zaznaczanie
- `HorizontalSplitter` - poziomy rozdzielacz
- `Info`, `Tree`
- `ImageCircle` - okrągły obraz
- `DashboardItem`, `LocatorItem`
- `SmallSize`, `NormalSize`, `LargeSize`
- `AutoUpdate` - automatyczna aktualizacja
- `ArrowsSelectNext` - strzałki wybierają następny
- `Hidden` - ukryty
- `NonClickable` - nieklikowalny
- `LabelTop`, `LebelLeft` - pozycja etykiety
- `FilesDropTarget` - cel upuszczania plików
- `Rss` - kanał RSS
- `PreventOrderBy` - zapobiegaj sortowaniu
