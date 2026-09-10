# Kolekcje i listy — `Grid`, filtry, zaznaczanie

Elementy listowe (`Grid`, `List`, `Scheduler`, `Gantt`, `Pivot`, `Chart`…) i wzorce ich
zasilania danymi. Podstawy składni formularza: [../SKILL.md](../SKILL.md);
pełna specyfikacja elementów: [ELEMENTS.md](ELEMENTS.md).

## Grid / List — tabela danych

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

> **Szerokość kolumn.** `Width="*"` (wypełnij) działa wyłącznie na **kontenerach**
> (`Grid`, `Group`, `Stack`) oraz na samodzielnym polu w układzie formularza. **Kolumny listy**
> — czyli `Field` będący bezpośrednim dzieckiem `Grid` — muszą mieć **stałą** szerokość w
> znakach (np. `Width="30"`). Siatka czyta szerokość kolumny jako liczbę: wartość nieliczbowa
> (w tym `*`) jest **po cichu ignorowana** i kolumna dostaje szerokość domyślną. Nie licz więc,
> że `Width="*"` „rozciągnie ostatnią kolumnę" — po prostu nic nie zrobi, a układ wyjdzie inny
> niż zamierzony. Sam `Grid` ma natomiast zwykle `Width="*" Height="*"`, żeby wypełnić zakładkę.

## Lista sterowana kodem — `EditValue` wskazujące `ViewInfo`

`Grid` można zasilić nie tylko prostą kolekcją (`{Pozycje}`), ale też property zwracającą
`ViewInfo` (`EditValue="{MojaListaView}"`). Wtedy zawartość listy, filtr, sortowanie i blokady
(dodawanie/edycja/usuwanie) buduje kod w handlerze tworzącym widok. To standard dla list z
nietrywialnym filtrowaniem — zwłaszcza diagnostycznych/konfiguracyjnych — osadzonych na
formularzu lub w oknie:

```xml
<Grid Width="*" Height="*" EditValue="{MojaListaView}" IsToolbarVisible="true" OrderBy="Data desc">
  <Field CaptionHtml="Data" Width="14" EditValue="{Data}" />
  <Field CaptionHtml="Opis" Width="60" EditValue="{Opis}" />
</Grid>
```

Stronę logiki (jak zbudować `ViewInfo` jako property/folder i filtrować widok) opisują [viewinfo.md](../../programming/references/viewinfo.md) i [rowcondition.md](../../programming/references/rowcondition.md).

## Multi-select — `SelectedValue` i reaktywne pole pochodne

`SelectedValue="{Zaznaczone}"` wiąże **wiele** zaznaczonych wierszy z property typu tablica
wierszy (np. `Faktura[]`) w obiekcie kontekstu. `FocusedValue` to inny scenariusz — **jeden**
aktywny wiersz.

Jeśli pod listą ma się pojawić wartość zbudowana z zaznaczenia (połączony tekst, suma, dowolny
algorytm), zwiąż zależne pole z property **tylko-do-odczytu**, która liczy wynik z tej tablicy.
Property `SelectedValue`/`FocusedValue` może być **zwykłą auto-property** — sama zmiana
zaznaczenia/fokusu wymusza przeliczenie pól zależnych, więc **nie** trzeba w jej setterze ręcznie
odświeżać UI:

```xml
<Grid Width="*" Height="*" EditValue="{FakturyView}" SelectedValue="{Zaznaczone}" IsToolbarVisible="true" />
<Field CaptionHtml="Suma zaznaczonych" Width="*" IsReadOnly="true" EditValue="{SumaZaznaczonych}" />
```

## Pasek filtra listy — `Flow Class="DataBar"` wewnątrz `Grid`

Pola filtrujące listę umieszcza się zwykle w `Flow Class="DataBar"` **wewnątrz** `<Grid>`,
obok kolumn `Field`. Mimo zagnieżdżenia w XML taki `Flow` działa na poziomie bindowania
**samego gridu** (nie schodzi do kontekstu wiersza), a `Class="DataBar"` renderuje go jako
pasek parametrów listy. Dzięki temu filtr jest **zarządzany przez organizator listy** i tworzy
z listą jeden spójny element (zamiast luźnego `Flow` postawionego nad gridem).

**Etykiety pól filtrujących umieszczaj na górze, nad polem** (`Class="LabelTop"`) — niezależnie od
typu formularza (nawet gdy poza filtrem obowiązuje domyślny `LabelLeft`; o pozycji etykiet ogólnie
patrz [ux-design.md](ux-design.md)).

Są **dwa sposoby** wskazania, skąd filtr czyta/zapisuje wartości:

**1. Filtry w obiekcie kontekstu (dominujący wzorzec w programie).** `DataContext="{Context}"`,
a pola bindują się do właściwości klasy parametrów (`Params : ContextBase`) trzymanej w
kontekście — przez `{NazwaParams.Pole}`:
```xml
<Grid Width="*" Height="*" EditValue="{ObrotyView}" IsToolbarVisible="true">
  <Field CaptionHtml="Towar" Width="17" EditValue="{Towar}" />
  <Field CaptionHtml="Marża" Width="11" EditValue="{Marża}" Footer="Sum" />
  <Flow Class="DataBar" DataContext="{Context}" Align="true">
    <Field CaptionHtml="Okres" Width="22" EditValue="{ObrotyParams.OkresCzasu}" />
  </Flow>
</Grid>
```

**2. Filtry na obiekcie głównym (dozwolony, choć w programie rzadki).** Gdy property filtrów
leżą wprost na obiekcie sterującym oknem (`{DataSource}`), **nie ustawiaj `DataContext`** na
`Flow` — odziedziczy on kontekst strony i bindy rozwiążą się względem obiektu głównego:
```xml
<Grid Width="*" Height="*" EditValue="{WpisyView}" IsToolbarVisible="true">
  <Field CaptionHtml="Data"    Width="14" EditValue="{Data}" />
  <Field CaptionHtml="Request" Width="80" EditValue="{Opis}" />
  <Flow Class="DataBar" Align="true">
    <Field CaptionHtml="Operator" Width="25" EditValue="{Operator}" />  <!-- property obiektu głównego -->
    <Field CaptionHtml="Okres"    Width="22" EditValue="{Okres}" />
  </Flow>
</Grid>
```

Stronę C# (klasę parametrów `Params : ContextBase` vs property na obiekcie głównym z
`[Accessor(AutoChange = true)]`) opisują [contextbase.md](../../programming/references/contextbase.md) i [context.md](../../programming/references/context.md).

## Powiązania

- [../SKILL.md](../SKILL.md) — składnia formularza, `Field`, kontenery, `Class`, `Appearance`.
- [binding.md](binding.md) — zmiana kontekstu danych w elementach listowych, składnia wyrażeń.
- [ux-design.md](ux-design.md) — pozycja etykiet, wizualna weryfikacja układu.
- [ELEMENTS.md](ELEMENTS.md) — pełna specyfikacja elementów i wartości `Class`.
- `ViewInfo` ([viewinfo.md](../../programming/references/viewinfo.md)), warunki serwerowe ([rowcondition.md](../../programming/references/rowcondition.md)),
  klasy parametrów ([contextbase.md](../../programming/references/contextbase.md), [context.md](../../programming/references/context.md)), cechy ([features.md](../../programming/references/features.md)).
