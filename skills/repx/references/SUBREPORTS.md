# Nagłówki, stopki i podraporty — budowa i mechanizm

Referencja uzupełniająca [../SKILL.md](../SKILL.md). Dotyczy platformy Soneta (enova365, Triva).
Dwie strony tematu: **(1) jak wstawić** gotowy nagłówek/stopkę/podraport do swojego raportu
(kontrolki `Header`/`Footer`/`DatabaseSubReport` → [CONTROLS.md](CONTROLS.md)) oraz **(2) jak
zbudować** sam plik nagłówka/stopki/podraportu jako **osobny, samodzielny raport**. Ta referencja
opisuje głównie (2) i łączący je mechanizm.

## Mechanizm luźnego wiązania (najważniejsze)

Raport **nie** wskazuje pliku podraportu po nazwie pliku. Wskazuje **nazwę logiczną** przez
`ReportSourceName` (np. `"nagłówek - lista"`, `"stopka"`), a program rozwiązuje ją w czasie druku:

```
kontrolka Header/Footer/DatabaseSubReport (ReportSourceName="nazwa logiczna")
   → folder wg typu kontrolki: "Nagłówki" | "Stopki" | "Podraporty"
   → wpis konfiguracji w bazie o tej nazwie → treść wzorca (własny lub standardowy)
   → podpięcie jako ReportSource podraportu
```

Konsekwencje, o których musisz pamiętać:

- **Standardowe wzorce** (nagłówki, stopki, arkusze stylów, podraporty) są zarządzane centralnie
  w konfiguracji wydruków; ich treść jest osadzona w bibliotekach, a baza trzyma tylko strukturę
  i odwołanie. Klient może **podmienić** standardowy nagłówek/stopkę własnym wzorcem („Wzorce
  użytkownika”) — **bez ruszania samego raportu**, bo wiązanie idzie po nazwie logicznej.
- Twój własny nagłówek/stopka/podraport to **zwykły plik `.repx`** w katalogu `Repx/` dodatku
  (osadzany automatycznie → [REGISTRATION.md](REGISTRATION.md)). Odwołujesz się do niego z raportu
  głównego przez `ReportSourceName`.
- Arkusz stylów rozwiązuje się analogicznie po nazwie logicznej (`StylesSource`/`StyleSheetPath`,
  np. `"standardowy"`) → [STYLES.md](STYLES.md).

## Anatomia pliku nagłówka strony

Nagłówek to samodzielny raport, który **dane bierze z raportu-rodzica**, nie z listy:

```xml
<XtraReportsLayoutSerializer ... Name="NaglowekLista" DisplayName="nagłówek - lista - v1"
    StyleSheetPath="standardowy" DataSource="#Ref-0" ...>
  <Bands>
    <Item1 ControlType="TopMarginBand" HeightF="0" .../>
    <Item2 ControlType="DetailBand" Name="detailBand1" HeightF="0" ...>
      <SubBands>
        <!-- treść na PIERWSZEJ stronie (pełna pieczątka firmy + tytuł + opis filtrów) -->
        <Item1 ControlType="SubBand" Name="subband_FirstPage" HeightF="447" ...>
          <Controls>
            <Item ControlType="XRLabel" Name="label_CompanyName" StyleName="NaglowekWyroznienieStyl" .../>
            <Item ControlType="XRRichText" Name="richText1" StyleName="NaglowekTytulStyl" ...>
              <DataBindings>
                <Item PropertyName="Rtf" DataMember="ReportContext.Title" />   <!-- tytuł z rodzica -->
              </DataBindings>
            </Item>
          </Controls>
        </Item1>
        <!-- treść na KOLEJNYCH stronach (skrócona nazwa firmy + sam tytuł) -->
        <Item2 ControlType="SubBand" Name="subband_NextPage" HeightF="184" .../>
      </SubBands>
    </Item2>
    <Item3 ControlType="BottomMarginBand" HeightF="0" .../>
  </Bands>
  <ComponentStorage>
    <Item1 Ref="0" ObjectType="...BusinessDataSource,..." Name="BusinessSource" DataKind="Context" />
    <Item2 ObjectType="...Components.BusinessContext,..." Name="BusinessContext" StylesSource="standardowy" />
    <Item3 ObjectType="...ReportSnippetComponent,..." Name="Snippet" SnippetTypeName="...NaglowekListaSnippet,..." />
  </ComponentStorage>
</XtraReportsLayoutSerializer>
```

Zasady konstrukcji nagłówka:

- **`DataKind="Context"`** — nagłówek czerpie z kontekstu rodzica (tytuł, filtry, dane firmy),
  nie z listy danych. Zob. [DATA.md](DATA.md).
- **`<SubBands>` z parą `subband_FirstPage` / `subband_NextPage`** — DevExpress pozwala dołączyć do
  `DetailBand` podpasma; snippet wybiera, które pokazać na danej stronie (patrz niżej). To standardowy
  wzorzec „pełna pieczątka na 1. stronie, skrót na kolejnych”. `SubBand` może mieć `PageBreak="AfterBand"`
  (np. strona tytułowa).
- **Teksty z rodzica przez `ReportContext.*`** (wiązanie `DataBindings`), tabela poniżej. Tytuł i opisy
  filtrów bywają w RTF (`XRRichText`, `PropertyName="Rtf"`); wersja „płaska” jako `Text` (`XRLabel`).
- **Dane firmy wypełnia snippet** (`ReportTools.GetHeaderData(Context)`), bo wymagają logiki
  (adres wieloliniowy, opcjonalne biuro rachunkowe, REGON/PKD).

### `ReportContext` — dane nagłówka z raportu-rodzica

`DataMember` względem źródła `DataKind="Context"`:

| `DataMember` | Zawartość |
|---|---|
| `ReportContext.Title` / `ReportContext.PlainTitle` | Tytuł wydruku (RTF / czysty tekst). |
| `ReportContext.FiltersDescription` / `…UserFiltersDescription` | Opis filtrów systemowych / użytkownika (RTF). |
| `ReportContext.ParametersDescription` / `…PlainParametersDescription` | Opis parametrów wydruku (RTF / lista tekstów). |

W kodzie snippetu ten sam obiekt: `Context.Get(out ReportContext rc); rc.PlainParametersDescription`.

## Anatomia pliku stopki

Stopka drukuje numer strony, datę, operatora, numer seryjny i znacznik końca raportu:

```xml
<XtraReportsLayoutSerializer ... Name="FooterReport" DisplayName="stopka" StyleSheetPath="standardowy" ...>
  <Bands>
    <Item ControlType="TopMarginBand" .../>
    <Item ControlType="DetailBand" Expanded="false" HeightF="0" .../>
    <Item ControlType="BottomMarginBand" HeightF="0" .../>
    <Item ControlType="PageFooterBand" Name="PageFooter" HeightF="150" StyleName="StopkaSzczegolyStyl">
      <Controls>
        <Item ControlType="XRLabel"    Name="label_Operator"     StyleName="StopkaSzczegolyStyl" .../>
        <Item ControlType="XRPageInfo" Name="pageInfo_Main"      StyleName="StopkaSzczegolyStyl" .../>
        <Item ControlType="XRLabel"    Name="label_SerialNumber" StyleName="StopkaSzczegolyStyl" .../>
      </Controls>
    </Item>
    <Item ControlType="ReportHeaderBand" Name="ReportHeader" HeightF="5">     <!-- znacznik strony -->
      <Controls><Item ControlType="XRPageInfo" Name="pageInfo_Marker" .../></Controls>
    </Item>
    <Item ControlType="GroupFooterBand" Name="GroupFooter" HeightF="50">      <!-- „koniec raportu” -->
      <Controls><Item ControlType="XRLabel" Name="label_ReportEnd" .../></Controls>
    </Item>
  </Bands>
  <ComponentStorage>… DataKind="Context" + BusinessContext + Snippet …</ComponentStorage>
</XtraReportsLayoutSerializer>
```

- **`XRPageInfo`** — numeracja stron; `RunningBand` ustawia snippet, by numer liczył się w obrębie
  właściwej bandy raportu-rodzica.
- Dane stopki dostarcza snippet: `ReportTools.GetFooterData(Context)` → `FooterData`
  (`Copyright`, `SerialNumber`, `ReportEnd`, `DateTime`, `Operator`, `AdditionalText`, `PageNumbers`).
- „Drukuj tylko na ostatniej stronie” realizuje się zdarzeniem `PrintOnPage` + `XtraReportHelper`
  (patrz niżej).

## Snippet nagłówka/stopki — wzorce i helpery

> ⚠️ **Licencja DevExpress.** Poniższy kod używa `ReportSnippet` i typów `XR*` — w kodzie dodatku
> wymaga własnej licencji DevExpress. Wariant bez licencji (`Snippet` + `[Bind]`) → [SNIPPET.md](SNIPPET.md).

```csharp
public class NaglowekListaSnippet : ReportSnippet {
    [DxBind] private readonly XRLabel label_CompanyName;
    private HeaderData headerData;

    // jednorazowy odczyt danych firmy (BeforePrint woła się wielokrotnie — strzeż flagą)
    [DxBind] private void Report_BeforePrint(object s, CancelEventArgs e) {
        headerData = ReportTools.GetHeaderData(Context);
        label_CompanyName.Text = headerData.CompanyName;
        label_tin.Text = "NIP: {0}".TranslateFormat(headerData.CompanyTIN);   // lokalizacja
    }

    // wybór podpasma: pełna pieczątka tylko na pierwszej stronie nagłówka
    [DxBind] private void subband_FirstPage_BeforePrint(object s, CancelEventArgs e)
        => e.Cancel = !XtraReportHelper.IsFirstHeader(Report);
    [DxBind] private void subband_NextPage_BeforePrint(object s, CancelEventArgs e)
        => e.Cancel = XtraReportHelper.IsFirstHeader(Report);
}
```

Stopka — numer strony i sekcje „tylko na ostatniej stronie” przez `PrintOnPage`:

```csharp
[DxBind] private void Report_BeforePrint(object s, CancelEventArgs e) {
    if (initFooter && MasterReport != null) {                 // MasterReport ≠ null → jesteśmy podraportem
        initFooter = false;
        var f = ReportTools.GetFooterData(Context);
        label_Operator.Text = f.Operator;
        pageInfo_Main.RunningBand = XtraReportHelper.GetRunningBand(Report);
    }
}
[DxBind] private void pageInfo_Marker_PrintOnPage(object s, PrintOnPageEventArgs e)
    => lastPage = XtraReportHelper.IsLastPageForGroupItem(s as XRPageInfo);
[DxBind] private void label_ReportEnd_PrintOnPage(object s, PrintOnPageEventArgs e)
    => e.Cancel = !lastPage;                                  // „koniec raportu” tylko na ostatniej
```

### Helpery raportowe

| Element | Rola |
|---|---|
| `ReportTools.GetHeaderData(Context)` → `HeaderData` | Dane firmy: `CompanyName`, `ShortCompanyName`, `AddressLine1/2`, `CompanyTIN`, `REGON`, `PKD`, `AccountingOfficeName/Header/TIN/REGON`, `ShortAccountingOfficeName`. |
| `ReportTools.GetFooterData(Context)` → `FooterData` | `Copyright`, `SerialNumber`, `ReportEnd`, `DateTime`, `Operator`, `AdditionalText`, `PageNumbers`. |
| `XtraReportHelper.IsFirstHeader(Report)` | Czy to pierwsze wystąpienie nagłówka (pierwsza strona). |
| `XtraReportHelper.IsFirstPageForDataSource(Report)` | Pierwsza strona dla bieżącego źródła danych (np. strona tytułowa dokumentu). |
| `XtraReportHelper.GetRunningBand(Report)` | Banda, w obrębie której liczyć numery stron. |
| `XtraReportHelper.IsLastPageForGroupItem(XRPageInfo)` | Czy ostatnia strona grupy/dokumentu. |
| `"…{0}…".TranslateFormat(x)` / `"…".Translate()` / `[TranslateIgnore]` | Tłumaczenie tekstów; `[TranslateIgnore]` wyłącza tłumaczenie metody. |

## Podraport statyczny (np. blok podpisów)

Najprostszy podraport — stała treść wiązana do bieżącego rekordu, bez `ReportContext`:

```xml
<ComponentStorage>
  <Item ObjectType="...BusinessDataSource,..." Name="businessDataSource1" DataKind="SingleRow" DataMember="This" />
  <Item ObjectType="...Components.BusinessContext,..." Name="BusinessContext" StylesSource="standardowy" />
</ComponentStorage>
```

Treść (np. rubryki „Sporządził / Sprawdził / Zatwierdził”) to zwykły `DetailBand` z `XRTable`
i `XRLabel`/`XRLine` (linia podpisu: `ControlType="XRLine" LineStyle="Dot"`). Atrybut `Bookmark`
na raporcie nadaje pozycję w drzewie zakładek PDF.

## Podraport master-detail z policzonymi danymi

Gdy podraport pokazuje **kilka niezależnych sekcji** (np. różne tabele podsumowań), buduje się je
jako **rodzeństwo `DetailReportBand` z rosnącym `Level`** (`Level="0"`, `"1"`, `"2"`…), a snippet
w `DataSourceRowChanged` **dynamicznie ustawia źródło i widoczność** każdej sekcji:

```csharp
[DxBind] private readonly DetailReportBand SekcjaA;   // pole = DetailReportBand po nazwie
public DokumentHandlowy Dokument;

[DxBind] private void Report_DataSourceRowChanged(object s, DataSourceRowEventArgs e) {
    if (GetCurrentRow() is not DokumentHandlowy dok) { SekcjaA.Visible = false; return; }
    Dokument = dok;
    if (!dok.Wydruk.CzyPokazacSekcjeA) SekcjaA.Visible = false;
    else SekcjaA.DataSource = ObliczPozycje(dok).Select(x => new PozycjaProxy(dok, x)).ToArray();
}
```

- **Klasy proxy** (`PozycjaProxy`) wystawiają dokładnie te właściwości, które wiążą komórki
  (`[Pole]` w `ExpressionBindings`) — wygodny sposób na policzone/przekształcone wiersze.
- W komórkach nagłówka/stopki sekcji sumujesz przez `sumSum([Pole])` + `<Summary Running="Report">`
  → [DATA.md](DATA.md).
- Pierwsza vs kolejna strona całego dokumentu: `DetailReport_BeforePrint` z flagą
  `firstPageForDataSource` albo `XtraReportHelper.IsFirstPageForDataSource(Report)`.
- Drzewiaste/hierarchiczne wcięcie z wcięciem: `DetailBand` z `<HierarchyPrintOptions Indent="50.8" />`.

## Zdarzenia snippetu używane w nagłówkach/stopkach/podraportach

| Metoda (wzorzec nazwy) | Args | Efekt |
|---|---|---|
| `Report_BeforePrint` | `CancelEventArgs` | Jednorazowa inicjalizacja danych (strzeż flagą). |
| `Report_DataSourceRowChanged` | `DataSourceRowEventArgs` | Przeliczenie per rekord; ustawianie `Section.DataSource`/`Visible`. |
| `<banda>_BeforePrint` | `CancelEventArgs` | `e.Cancel = true` ukrywa pasmo (wybór FirstPage/NextPage). |
| `<kontrolka>_PrintOnPage` | `PrintOnPageEventArgs` | Decyzje zależne od strony („tylko na ostatniej”). |
| `<poleKalkulowane>_GetValue` | `GetValueEventArgs` | Zwrot wartości pola kalkulowanego z kodu (`e.Value = …`) → [DATA.md](DATA.md). |

Jawna forma wiązania, gdy nazwa metody nie niesie zdarzenia:
`[DxBind(Name="Report", EventName="DataSourceRowChanged")]`. Dostęp: `Report`, `MasterReport`,
`Report.DataAdapter` (bieżący obiekt bandy), `Tag` (parametr przekazany do podraportu), `Context`,
`Session`. Pełny model snippetu → [SNIPPET.md](SNIPPET.md).

## Checklista nagłówka/stopki/podraportu

- [ ] Plik to samodzielny `.repx` w `Repx/`; raport główny odwołuje się przez `ReportSourceName` (nazwa logiczna).
- [ ] Nagłówek/stopka: `DataKind="Context"`; teksty z rodzica przez `ReportContext.*` (`Rtf`/`Text`).
- [ ] Nagłówek: `DetailBand`→`<SubBands>` `subband_FirstPage`/`subband_NextPage`; przełączanie w snippecie przez `XtraReportHelper.IsFirstHeader`.
- [ ] Stopka: `PageFooterBand` + `XRPageInfo`; `RunningBand`/`PrintOnPage` sterowane snippetem; guard `MasterReport != null`.
- [ ] Dane firmy/stopki z `ReportTools.GetHeaderData`/`GetFooterData(Context)`; teksty przez `TranslateFormat`.
- [ ] Podraport wielosekcyjny: rodzeństwo `DetailReportBand` z `Level`, `Section.DataSource`/`Visible` ustawiane w `DataSourceRowChanged`.
- [ ] `StyleName` z arkusza (`.repss`) zamiast lokalnego formatowania → [STYLES.md](STYLES.md).
- [ ] Kod snippetu: licencja DevExpress albo wariant generyczny → [SNIPPET.md](SNIPPET.md).

## Powiązane

- [CONTROLS.md](CONTROLS.md) — kontrolki `Header`/`Footer`/`DatabaseSubReport` (strona „wstawiania”), `XRPageInfo`, `XRLine`, `XRRichText`.
- [STRUCTURE-BANDS.md](STRUCTURE-BANDS.md) — `<SubBands>`/`SubBand`, `DetailReportBand`/`Level`, `HierarchyPrintOptions`, `StyleSheetPath`.
- [STYLES.md](STYLES.md) — arkusze `.repss` i nazwy logiczne stylów.
- [DATA.md](DATA.md) — `DataKind="Context"`, `ReportContext`, pola kalkulowane `_GetValue`, podsumowania.
- [SNIPPET.md](SNIPPET.md) — pełny model snippetu, licencja DevExpress, wariant generyczny.
- **[programming](../../programming/SKILL.md)** — logika licząca dane (workery, sesja, klasy proxy).
