# Dane, wiązania, wyrażenia i podsumowania

Referencja uzupełniająca [../SKILL.md](../SKILL.md). To warstwa **Soneta-specyficzna** — sposób,
w jaki raport `.repx` pobiera dane z platformy Soneta (enova365, Triva) i je przetwarza.

## `BusinessDataSource` i `DataKind`

Soneta zastępuje natywne źródła DevExpress własnym komponentem `BusinessDataSource` w sekcji
`<ComponentStorage>`. Atrybut `DataSource="#Ref-N"` na raporcie i na `DetailReportBand` wskazuje,
z którego źródła pasmo czerpie dane. O tym, **skąd** pochodzą dane, decyduje `DataKind`:

| `DataKind` | Źródło danych |
|---|---|
| `CurrentList` | **Lista, na której wywołano wydruk** (najczęstsze, „produkcyjne"). `DataMember` zwykle zbędny. |
| `Context` | Kontekst wywołania raportu — dostęp do parametrów / sesji / klasy snippet przez `DataMember`. Zwykle drugi, pomocniczy `BusinessDataSource` o nazwie `BusinessSourceContext`. |
| `SingleRow` | Pojedynczy bieżący rekord (wydruk z formularza jednego obiektu). |
| `Session` | Cała sesja bazy — dostęp do dowolnej tabeli przez `DataMember="Moduł.Tabela"`. Proste/testowe wydruki. |
| `Empty` | Brak danych (wartość domyślna — nie serializowana). |

```xml
<ComponentStorage>
  <Item1 Ref="0" ObjectType="Soneta.Business.UI.DxReports.BusinessDataSource,Soneta.Business.UI.DxReports"
         Name="BusinessSource" DataKind="Session" DataMember="CRM.Kontrahenci" />
</ComponentStorage>
```

### `DataMember` — nawigacja do danych

Ścieżka po właściwościach względem obiektu wskazanego przez `DataKind`, oddzielana kropkami:

- `This` — bieżący wiersz bez nawigacji. `This.This` / `This.This.This` — schodzenie w kolejne
  poziomy zagnieżdżonych `DetailReportBand`.
- Nazwa **kolekcji podrzędnej** (master-detail): `Pozycje`, `Wyplaty`, `Elementy`, `Rozliczenia`.
- **Tabela z modułu** (dla `DataKind="Session"`): `CRM.Kontrahenci`, `Towary.Towary`.
- Właściwość skalarna do bindowania kontrolki: `Kod`, `Kontrahent.Nazwa`, `Ceny.Podstawowa.Netto`.
- Klasa parametrów snippet (dla `DataKind="Context"`): `NazwaSnippet+KlasaContext.Wlasciwosc`
  (znak `+` = klasa zagnieżdżona CLR).
- **Kontekst nagłówka/stopki (dla `DataKind="Context"`):** `ReportContext.Title`, `…PlainTitle`,
  `…FiltersDescription`, `…UserFiltersDescription`, `…ParametersDescription` — tytuł i opisy
  filtrów/parametrów przekazane z raportu-rodzica. Wiązane zwykle na `XRRichText` (`PropertyName="Rtf"`)
  lub `XRLabel` (`Text`). Zob. [SUBREPORTS.md](SUBREPORTS.md).

`DataMember` jest **wymagany**, gdy `DataKind` = `Context` lub `Session`. Dla `CurrentList`/
`SingleRow` zwykle zbędny (lista jest gotowa).

### `DesignDataTypeName`

Nazwa typu wiersza (np. `Towar`, `Kontrahent`, `DokumentHandlowy`) — **tylko dla trybu Design**:
pozwala projektantowi pokazać listę dostępnych pól, gdy realne dane nie są dostępne. W runtime
dla `CurrentList`/`SingleRow` ustawiany automatycznie.

### `BusinessContext` — kontekst prezentacji

Komponent niosący ustawienia stylów i eksportu (nie dostarcza wierszy danych):

| Atrybut | Znaczenie |
|---|---|
| `StylesSource` | Nazwa arkusza stylów (`.repss`), zwykle `"standardowy"`. Zob. [REGISTRATION.md](REGISTRATION.md) → Style. |
| `SingleRow` | Czy raport dotyczy jednego wiersza (`true`) czy listy (`false`). |
| `SelectedRowsVisibility` | Czy dostępna opcja „tylko zaznaczone zapisy". |
| `TreatStringAsValueInExport` | Czy tekst traktować jak liczbę/datę przy eksporcie (np. do Excela). |

### Wzorzec produkcyjny: dwa źródła

Najczęstszy układ raportu listowego to **dwa** `BusinessDataSource`:

```xml
<ComponentStorage>
  <Item1 Ref="49" ObjectType="...Components.BusinessContext,..." Name="BusinessContext"
         StylesSource="standardowy" TreatStringAsValueInExport="false" />
  <Item2 Ref="5"  ObjectType="...BusinessDataSource,..." Name="BusinessSource"        DataKind="CurrentList" />
  <Item3 Ref="0"  ObjectType="...BusinessDataSource,..." Name="BusinessSourceContext" DataKind="Context" />
</ComponentStorage>
```

Root raportu bindowany do `#Ref-0` (`Context` — nagłówek, parametry), a `DetailReportBand`
listy do `#Ref-5` (`CurrentList` — właściwe dane).

## Wiązanie danych — `ExpressionBindings` vs `DataBindings`

- **`ExpressionBindings`** (standard) — wiąże dowolną właściwość z **wyrażeniem** obliczanym na
  zdarzenie (praktycznie zawsze `BeforePrint`). Pozwala na funkcje, warunki, agregaty i wiązanie
  innych właściwości niż `Text` (`Visible`, `BackColor`, `ForeColor`).
- **inline `Text="[Pole]"`** — najkrótszy zapis dla komórek; format przez `TextFormatString`.
- **`DataBindings`** (legacy) — tylko surowe pole: `PropertyName` + `DataMember` + `FormatString`.

```xml
<!-- ExpressionBindings: wyrażenie -->
<Item1 Ref="12" EventName="BeforePrint" PropertyName="Text" Expression="FormatString('{0:d}', [Data])" />

<!-- inline -->
<Item2 Ref="13" ControlType="XRTableCell" Text="[Netto]" TextFormatString="{0:n2}" Dpi="254" />

<!-- DataBindings (legacy) -->
<DataBindings>
  <Item1 Ref="14" PropertyName="Text" DataMember="Kod" FormatString="{0:n2}" />
</DataBindings>
```

## Podsumowania / agregacje

Dwa równoważne mechanizmy — element `<Summary>` na komórce lub funkcja `sumXxx` w wyrażeniu.

### Element `<Summary>`

Dziecko `XRLabel`/`XRTableCell`, zwykle w `GroupFooterBand`/`ReportFooterBand`:

```xml
<Summary Ref="62" FormatString="{0:n2}" Running="Report" IgnoreNullValues="true" />
<Summary Ref="18" Running="Group"  Func="RecordNumber" />   <!-- numeracja Lp. w grupie -->
<Summary Ref="16" Running="Report" Func="RunningSum" />      <!-- suma narastająca -->
```

| Atrybut | Znaczenie |
|---|---|
| `Func` | `Sum` (**domyślny — pomijany**), `RecordNumber` (Lp.), `RunningSum`, `Avg`, `Count`, `Custom`. |
| `Running` | Zakres agregacji: `Report`, `Group`, `Page`. |
| `IgnoreNullValues` | `true` → pomija wartości puste. |
| `FormatString` | Format .NET wyniku, np. `{0:n2}`. |

### Funkcje agregujące w wyrażeniu (częstsze)

W `ExpressionBindings` komórki stopki:

```xml
<Item1 Ref="21" EventName="BeforePrint" PropertyName="Text" Expression="sumSum([Wartosc])" />
```

`sumSum` (suma), `sumRunningSum` (narastająca), `sumAvg` (średnia), `sumRecordNumber` (numer),
`sumCount` (liczność). Argument to pole lub wyrażenie.

## Grupowanie i sortowanie

- **Grupowanie:** `<GroupFields>` na `GroupHeaderBand` — patrz [STRUCTURE-BANDS.md](STRUCTURE-BANDS.md).
- **Sortowanie detali:** `<SortFields>` na `DetailBand`:

```xml
<SortFields>
  <Item1 Ref="105" FieldName="Konto.Symbol" SortOrder="Ascending" />
</SortFields>
```

`SortOrder`: `Ascending` / `Descending` / `None`. `FieldName` może nawigować po relacjach.

## Pola kalkulowane — `CalculatedFields`

Definiowane na poziomie raportu; potem używasz ich jak zwykłego pola (`[Nazwa]`):

```xml
<CalculatedFields>
  <Item1 Ref="3" Name="LpD" FieldType="Int16"
         Expression="[DataSource.CurrentRowIndex] + 1" DataSource="#Ref-2" />
</CalculatedFields>
```

`FieldType` = typ .NET (`Int16`, `String`, `Decimal`, `DateTime`, `Boolean`…). Wariant bez
`Expression`, tylko z `DataMember`, oznacza pole liczone po stronie danych Soneta (raport je
tylko binduje).

**Pole kalkulowane liczone w kodzie (snippet).** Deklarujesz pole z pustym ciałem (bez `Expression`),
a wartość zwracasz w snippecie zdarzeniem `<Nazwa>_GetValue`:

```xml
<CalculatedFields>
  <Item1 Ref="2" Name="ParametersDescriptionStr" FieldType="String" />
</CalculatedFields>
```
```csharp
[DxBind] private void ParametersDescriptionStr_GetValue(object s, GetValueEventArgs e) {
    Context.Get(out ReportContext rc);
    e.Value = string.Join("\n", rc.PlainParametersDescription);   // wartość pola z kodu
}
```

Potem używasz `[ParametersDescriptionStr]` jak zwykłego pola (np. `DataMember` etykiety). Model
snippetu → [SNIPPET.md](SNIPPET.md), kontekst nagłówka → [SUBREPORTS.md](SUBREPORTS.md).

## Filtrowanie — `FilterString`

Atrybut na `XtraReportsLayoutSerializer` (składnia DevExpress Criteria Language). Zwykle
filtruje się po stronie źródła, ale `FilterString` przydaje się do doprecyzowania:

```
FilterString="[Typ] = 'SprzedażZbiorczaEwidencja'"
FilterString="[TypZdarzenia] = 'RaportowaniePostepu' Or [TypZdarzenia] = 'RejestracjaBraku'"
FilterString="[Kod] Like 'B%'"
FilterString="[AdresyWWW][]"
```

- Pole w `[...]`; nawigacja po relacjach `[Podmiot].[Kod]`; literały tekstowe w `'...'`.
- Operatory: `=`, `Like`, `Or`, `And`, `Not`, `<` (w XML `&lt;`).
- `[Kolekcja][]` = predykat egzystencjalny „kolekcja niepusta". Ogólnie
  `[Kolekcja][warunek].Agregat()`.

## Formatowanie warunkowe

Standard w Soneta: **wiązanie właściwości wyglądu przez `ExpressionBindings` z `Iif(...)`**
(deklaratywne `FormattingRuleSheet` jest rzadkie).

```xml
<!-- warunkowe tło -->
<Item1 EventName="BeforePrint" PropertyName="BackColor"
       Expression="Iif([WierszPodsumowania] == True, 'Gainsboro', 'Transparent')" />

<!-- warunkowy kolor tekstu (zagnieżdżony Iif) -->
<Item1 EventName="BeforePrint" PropertyName="ForeColor"
       Expression="Iif([Prawo] == 'Zakaz', 'Red', Iif([Prawo] == 'Pełne', 'Green', 'Black'))" />

<!-- warunkowa widoczność -->
<Item1 EventName="BeforePrint" PropertyName="Visible" Expression="Not IsNullOrEmpty([NazwaPliku])" />
```

Kolory jako nazwy .NET Color w apostrofach (`'Red'`, `'Gainsboro'`, `'Transparent'`).

## Funkcje w wyrażeniach

| Funkcja | Rola |
|---|---|
| `sumSum(x)` / `sumRunningSum(x)` / `sumAvg(x)` / `sumRecordNumber()` | Agregaty (patrz wyżej). |
| `Iif(warunek, a, b)` | Warunek trójargumentowy — wartości, kolory, widoczność. |
| `FormatString('{0:...}', x, …)` | Formatowanie/składanie tekstu wg wzorca .NET. |
| `Concat(a, b, …)` | Łączenie łańcuchów. |
| `IsNull(x)` / `IsNullOrEmpty(x)` | Testy pustości / wartość domyślna. |
| `ToInt(x)` / `ToStr(x)` / `ToString(x)` | Konwersje typów. |
| `Round(x, n)` | Zaokrąglanie. |
| `Substring(s, i, n)` / `Remove(...)` / `Replace(s, a, b)` / `Contains(s, x)` / `CharIndex(...)` / `Len(s)` | Operacje na tekście. |
| `Char(n)` / `NewLine()` | Znaki specjalne / nowa linia. |

Operatory: `==`, `!=`, `Is Null`, `Not`, `And`, `Or`, `%` (modulo), `+`. Nawigacja
wielopoziomowa `[A].[B].[C]`. Konteksty specjalne: `[DataSource.CurrentRowIndex]`.

## Skrypty — NIE używane

W raportach Soneta logika jest **deklaratywna** (`ExpressionBindings`, `Summary`,
pola kalkulowane) oraz — po stronie danych — w kodzie-behind snippetu (patrz
[REGISTRATION.md](REGISTRATION.md) i **[programming](../../programming/SKILL.md)**). Nie używaj sekcji `Scripts`
DevExpress ani atrybutów `*Script`.
