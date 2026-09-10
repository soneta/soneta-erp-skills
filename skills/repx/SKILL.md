---
name: repx
description: >
  Wydruki i raporty w Soneta (enova365, Triva) — pliki .repx (DevExpress XtraReports, serializowany
  XML); dla dodatków partnerów i standardowych wydruków platformy. Sięgaj
  przy KAŻDYM zadaniu z wydrukiem/raportem: faktura, dokument, deklaracja, lista, zestawienie;
  utworzenie, przeróbka lub poprawa .repx; pytanie „jak w wydruku zrobić X”.
  Typowe X: podpięcie danych (lista, dokument z pozycjami, master-detail, podraport), podział stron,
  kolumna Lp, tabele, grupowanie i sumy, formatowanie warunkowe, kwoty, wartości na skanie formularza, nagłówek/stopka/podpisy, arkusz stylów (.repss), rejestracja wydruku. Wyzwalają też:
  XtraReportsLayoutSerializer, pasma (DetailBand/DetailReportBand/
  GroupHeaderBand/SubBand), kontrolki (XRLabel/XRTable/AmountLabel), BusinessDataSource/DataKind,
  ReportContext, ExpressionBindings/Summary/CalculatedFields/FilterString, [assembly: DxReport],
  ReportSnippet/[DxBind]. Wystarczy „wydruk”, „raport” lub „.repx” w kontekście Soneta. NIE dla:
  business.xml, form.xml, SQL, Excel, Word .dotx.
---

# Soneta DX Reports — wydruki DevExpress XtraReports (.repx)

Plik `.repx` to **zserializowany do XML** raport DevExpress XtraReports używany jako wydruk
w platformie Soneta (produkty enova365, Triva). Soneta osadza w nim **własne komponenty
źródła danych i własne kontrolki**, dlatego surowy szablon DevExpress ≠ szablon Soneta.
Runtime DevExpress w programie to **25.2**, ale pliki są zapisywane w formacie serializera
**`Version="20.2"`** (metadana zgodności wstecz — nie zmieniaj jej ręcznie).

## Minimalny szkielet (lista z bieżącego widoku)

```xml
<?xml version="1.0" encoding="utf-8"?>
<XtraReportsLayoutSerializer SerializerVersion="20.2.7.0" Ref="1"
    ControlType="DevExpress.XtraReports.UI.XtraReport, DevExpress.XtraReports.v20.2"
    Name="MojRaport" DisplayName="Mój raport" ReportUnit="TenthsOfAMillimeter"
    Margins="198, 198, 200, 200" PaperKind="A4" PageWidth="2100" PageHeight="2970"
    Version="20.2" DataSource="#Ref-0" Dpi="254" Font="Calibri, 9pt">
  <Bands>
    <Item1 Ref="2" ControlType="TopMarginBand"    Name="topMargin"    HeightF="200" Dpi="254" />
    <Item2 Ref="3" ControlType="DetailBand"       Name="detail"       HeightF="80"  Dpi="254">
      <Controls>
        <Item1 Ref="4" ControlType="XRLabel" Name="lblKod" Text="[Kod]"
               SizeF="600,80" LocationFloat="0,0" Dpi="254" Padding="5,5,0,0,254" />
      </Controls>
    </Item2>
    <Item3 Ref="5" ControlType="BottomMarginBand" Name="bottomMargin" HeightF="200" Dpi="254" />
  </Bands>
  <ComponentStorage>
    <Item1 Ref="0" ObjectType="Soneta.Business.UI.DxReports.BusinessDataSource,Soneta.Business.UI.DxReports"
           Name="BusinessSource" DataKind="CurrentList" />
    <Item2 Ref="6" ObjectType="Soneta.Business.UI.DxReports.Components.BusinessContext,Soneta.Business.UI.DxReports"
           Name="BusinessContext" StylesSource="standardowy" />
  </ComponentStorage>
</XtraReportsLayoutSerializer>
```

## 6 rzeczy, które musisz wiedzieć zanim zaczniesz

1. **Referencje przez `Ref`/`#Ref-N`.** Każdy węzeł to `ItemN` z unikalnym `Ref="N"`.
   `DataSource="#Ref-0"` na raporcie/paśmie wskazuje komponent `Ref="0"` w `ComponentStorage`.
   Numery `Ref` muszą być **unikalne w całym pliku** — pilnuj tego przy dodawaniu elementów.
2. **Jednostki: trzymaj parę `ReportUnit` + `Dpi` spójnie.** Standard produkcyjny:
   `ReportUnit="TenthsOfAMillimeter"` + `Dpi="254"` → wszystkie wymiary w **0,1 mm**
   (A4 = `2100×2970`, marginesy `198`≈20 mm). Alternatywa: brak `ReportUnit` = piksele/96 dpi
   (A4 = `827×1169`). Nie mieszaj — te same liczby znaczą różne rozmiary. Zob. [references/STRUCTURE-BANDS.md](references/STRUCTURE-BANDS.md).
3. **Skąd dane: `BusinessDataSource` + `DataKind`.** To Soneta-specyficzne. `CurrentList` =
   lista, na której wywołano wydruk (najczęstsze). `Context` = parametry/nagłówek/sesja
   (przez `DataMember`). `SingleRow` = pojedynczy rekord. `Session` + `DataMember="Moduł.Tabela"`.
   Zob. [references/DATA.md](references/DATA.md).
4. **Wiązanie danych: `ExpressionBindings` to standard**, `DataBindings` to legacy.
   Najprościej — wyrażenie **inline w `Text="[Pole]"`** na komórce/etykiecie. Zob. niżej i DATA.md.
5. **Kontrolki: preferuj układ tabelaryczny `XRTable`** (nie luźne etykiety). Kwoty wstawiaj
   przez **`AmountLabel`** (kontrolka Soneta), nie `XRLabel`. Zob. [references/CONTROLS.md](references/CONTROLS.md).
6. **Aby wydruk pojawił się w programie**, potrzebna jest rejestracja atrybutem
   **`[assembly: DxReport(...)]`** wiążącym plik z typem listy/obiektu. Zob. [references/REGISTRATION.md](references/REGISTRATION.md).
7. **Logika niewyrażalna deklaratywnie** (warunkowe sterowanie kontrolkami, obliczenia, dostarczanie
   policzonych danych, parametry) mieszka w **snippecie** — klasie C# `NazwaWydruku.repx.cs`
   dziedziczącej po `ReportSnippet`, z wiązaniem `[DxBind]`. **⚠️ Uwaga licencyjna:** `ReportSnippet`
   i `[DxBind]` odwołują się do typów DevExpress — w kodzie dodatku wymaga to **własnej licencji
   DevExpress**. Bez niej użyj generycznego `Snippet` + `[Bind]`. Zob. [references/SNIPPET.md](references/SNIPPET.md).

## Wiązanie danych — 3 sposoby

```xml
<!-- (1) INLINE w Text — najczęstsze w komórkach tabeli. Format przez TextFormatString -->
<Item1 Ref="10" ControlType="XRTableCell" Name="c1" Text="[Netto]"
       TextAlignment="MiddleRight" TextFormatString="{0:n2}" Dpi="254" />

<!-- (2) ExpressionBindings — standard; pozwala na wyrażenia, funkcje, inne właściwości -->
<Item2 Ref="11" ControlType="XRTableCell" Name="c2" Dpi="254">
  <ExpressionBindings>
    <Item1 Ref="12" EventName="BeforePrint" PropertyName="Text"
           Expression="FormatString('{0:d}', [Data])" />
  </ExpressionBindings>
</Item2>

<!-- (3) DataBindings — LEGACY, tylko proste pole -->
<Item3 Ref="13" ControlType="XRLabel" Name="lbl" Text="label" SizeF="600,60" LocationFloat="0,0">
  <DataBindings>
    <Item1 Ref="14" PropertyName="Text" DataMember="Kod" FormatString="{0:n2}" />
  </DataBindings>
</Item3>
```

- `[Pole]` = właściwość bieżącego wiersza danych. Nawigacja po relacjach kropką: `[Kontrahent].[Nazwa]`.
- `EventName="BeforePrint"` — moment obliczenia (praktycznie zawsze taki).
- Formaty .NET: `{0:n}` (liczba 2 miejsca, separator tysięcy), `{0:n0}`, `{0:d}` (data krótka),
  `{0:dd.MM.yyyy}`, `{0:0%}`, `{0:c}` (waluta). Pełna lista → CONTROLS.md.

## Pasma (Bands) — skrót

| Pasmo | Rola |
|---|---|
| `TopMarginBand` / `BottomMarginBand` | Marginesy strony — **obowiązkowe**, po jednym. |
| `DetailBand` | Powtarzane raz na każdy rekord bieżącego poziomu. Rdzeń raportu. |
| `DetailReportBand` | Master-detail: zagnieżdżony raport iterujący kolekcję (`DataMember="Pozycje"`). |
| `GroupHeaderBand` / `GroupFooterBand` | Nagłówek/stopka grupy (`<GroupFields>`); stopka = miejsce na sumy. |
| `ReportHeaderBand` / `ReportFooterBand` | Raz na początku / końcu całości. |
| `PageHeaderBand` / `PageFooterBand` | Na górze / dole każdej strony. |

Typowe atrybuty: `HeightF` (wysokość), `KeepTogether="true"`, `RepeatEveryPage="true"`
(nagłówek listy na każdej stronie), `StyleName`. Szczegóły i master-detail → [references/STRUCTURE-BANDS.md](references/STRUCTURE-BANDS.md).

## Podsumowania i grupowanie — skrót

```xml
<!-- suma w komórce stopki grupy/raportu (Func domyślny = Sum) -->
<Item1 Ref="20" ControlType="XRTableCell" Name="cSuma" Dpi="254">
  <ExpressionBindings>
    <Item1 Ref="21" EventName="BeforePrint" PropertyName="Text" Expression="sumSum([Wartosc])" />
  </ExpressionBindings>
</Item1>

<!-- alternatywnie element <Summary> (bez Func = Sum) -->
<Summary Ref="22" FormatString="{0:n2}" Running="Report" IgnoreNullValues="true" />

<!-- grupowanie na GroupHeaderBand -->
<GroupFields>
  <Item1 Ref="30" FieldName="Definicja" />
</GroupFields>
```

Funkcje agregujące w wyrażeniach: `sumSum`, `sumRunningSum` (narastająca), `sumAvg`,
`sumRecordNumber` (Lp.). `Running` = zakres agregacji (`Report`/`Group`/`Page`). Pełny
zestaw funkcji, formatowanie warunkowe (`Iif` na `BackColor`/`Visible`) i pola kalkulowane
→ [references/DATA.md](references/DATA.md).

## Kontrolki Soneta (nie ma odpowiedników XR*)

| Kontrolka | Zamiast | Do czego |
|---|---|---|
| `AmountLabel` | XRLabel | **Kwoty** — obsługa formatowania kwot, `SpacingForComma`. |
| `ShrinkableLabel` | XRLabel | Tekst zmniejszający czcionkę, by zmieścić się (`MaxLenght`). |
| `CrossCheckBox` | XRCheckBox | Pole wyboru (deklaracje). |
| `ResourcePictureBox` | XRPictureBox | Obraz z zasobu osadzonego (`ImageResourceName`), np. wzór deklaracji. |
| `Header` / `Footer` | (podraport) | Szablonowy nagłówek/stopka przez `ReportSourceName` + `Title`. |
| `DatabaseSubReport` | XRSubreport | Podraport zdefiniowany w bazie (`ReportSourceName`). |

> **Uwaga:** Soneta **nie** używa `XRSubreport`, `XRCheckBox` ani `XRZipCode` — zastępują je
> powyższe kontrolki i zagnieżdżone `DetailReportBand`. Szczegóły → [references/CONTROLS.md](references/CONTROLS.md).

## Nagłówki, stopki, podraporty i style — wzorce współdzielone

Nagłówek/stopkę/podraport **wstawiasz** kontrolką `Header`/`Footer`/`DatabaseSubReport` z
`ReportSourceName` (nazwa logiczna, nie plik) — program rozwiązuje ją na wzorzec z konfiguracji, więc
klient może podmienić standardowy nagłówek własnym bez ruszania raportu. **Budowa** samego pliku
nagłówka/stopki (`DataKind="Context"`, `SubBand` pierwszej/kolejnej strony, `ReportContext.Title`,
helpery `ReportTools`/`XtraReportHelper`) oraz wielosekcyjnych podraportów →
[references/SUBREPORTS.md](references/SUBREPORTS.md).

Formatowanie jest **centralne w arkuszu `.repss`**: kontrolki wskazują styl po nazwie (`StyleName`),
a raport wybiera arkusz przez `StyleSheetPath`/`StylesSource` (`"standardowy"`). Struktura pliku
`.repss`, właściwości stylów i katalog nazwanych stylów → [references/STYLES.md](references/STYLES.md).

## Gotowe przykłady (assets)

- [assets/Szkielet.repx](assets/Szkielet.repx) — minimalny raport listy (`CurrentList`).
- [assets/ListaProsta.repx](assets/ListaProsta.repx) — lista w `XRTable` z nagłówkiem kolumn
  (`GroupHeaderBand RepeatEveryPage`), numeracją Lp., sumą w stopce.
- [assets/Dokument.repx](assets/Dokument.repx) — master-detail: pola nagłówka dokumentu +
  zagnieżdżony `DetailReportBand DataMember="Pozycje"` z pozycjami i podsumowaniem.
- [assets/Dokument.repx.cs](assets/Dokument.repx.cs) — **snippet** (kod-behind) do powyższego:
  `[DxBind]`, `GetCurrentRow()`, parametry, warunkowe sterowanie kontrolką (wymaga licencji DevExpress).
- [assets/DokumentGeneryczny.repx.cs](assets/DokumentGeneryczny.repx.cs) — snippet **bez licencji
  DevExpress**: generyczny `Snippet` + `[Bind]` (`SnippetLabel`/`SnippetCollection`), gettery wartości.
  Zob. [references/SNIPPET.md](references/SNIPPET.md).

## Checklista przed oddaniem pliku .repx

- [ ] Deklaracja `<?xml version="1.0" encoding="utf-8"?>` i element główny `XtraReportsLayoutSerializer`.
- [ ] Wszystkie `Ref="N"` **unikalne**; każde `#Ref-N` wskazuje istniejący węzeł.
- [ ] `ReportUnit` spójne z `Dpi` i z wymiarami (`TenthsOfAMillimeter`+`254` → A4 `2100×2970`).
- [ ] Są `TopMarginBand` i `BottomMarginBand` (po jednym) oraz co najmniej jeden `DetailBand`.
- [ ] `ComponentStorage` zawiera `BusinessDataSource` z właściwym `DataKind`
      (oraz `BusinessContext StylesSource="standardowy"` dla stylów).
- [ ] `DataSource="#Ref-N"` na raporcie i na każdym `DetailReportBand` wskazuje właściwe źródło.
- [ ] Master-detail: `DetailReportBand` ma `DataMember` = nazwa kolekcji podrzędnej.
- [ ] Wiązania używają istniejących pól obiektu (`[Pole]`, `[Relacja].[Pole]`).
- [ ] Kwoty przez `AmountLabel`; format liczb/dat przez `TextFormatString`/`FormatString`.
- [ ] Style: kontrolki/pasma używają `StyleName` z arkusza `.repss` (+ `StylePriority Use*="false"`),
      raport ma `StyleSheetPath`/`StylesSource`. Zob. [references/STYLES.md](references/STYLES.md).
- [ ] Nagłówek/stopka/podraport: wstawiony przez `ReportSourceName` (nazwa logiczna); własny plik
      budowany wg wzorca (`DataKind="Context"`, `SubBand`, `ReportContext.*`). Zob. [references/SUBREPORTS.md](references/SUBREPORTS.md).
- [ ] Jeśli wydruk ma pojawić się w programie: przygotowana rejestracja `[assembly: DxReport(...)]`
      (lub dodanie jako wzorzec użytkownika w edytorze wydruków). Zob. REGISTRATION.md.

## Powiązane skille

- **[programming](../programming/SKILL.md)** — warstwa kodu C#, na której opiera się snippet (kod-behind). Powiązane
  tematy: *Klasy parametrów (`ContextBase`)* (tak samo konstruowane jak parametry wydruku),
  *Klasa `Context`*, *Sesje/transakcje* (`Session`, ORM), *Worker i Extender* (obliczanie danych
  raportu), *Zasady bezpiecznego kodu* (review snippetu), oraz typy wierszy, do których wydruk się
  wiąże. Zob. też [references/SNIPPET.md](references/SNIPPET.md).
- **[form-xml](../form-xml/SKILL.md)** — formularz parametrów wydruku (`RepxParams/*.pageform.xml`) pytany przed
  generowaniem.
- **[business-xml](../business-xml/SKILL.md)** — definicje obiektów/pól, które wydruk odczytuje w wiązaniach.
- **[config](../config/SKILL.md)** — import/eksport konfiguracji (wzorce wydruków w bazie) przez XML.
- **[tools](../tools/SKILL.md)** — generowanie i testowanie wydruków z wiersza poleceń.
- **[erp](../erp/SKILL.md)** — mapa skilli platformy Soneta.
