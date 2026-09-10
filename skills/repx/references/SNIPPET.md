# Snippety — kod-behind wydruku (`.repx.cs`)

Referencja uzupełniająca [../SKILL.md](../SKILL.md) i [REGISTRATION.md](REGISTRATION.md). Snippet to
**klasa C# towarzysząca plikowi `.repx`** (plik `NazwaWydruku.repx.cs`), w której umieszcza się
logikę, jakiej nie da się wyrazić deklaratywnie (wyrażeniami/`Summary`): warunkowe sterowanie
kontrolkami, obliczenia, dostarczanie policzonych danych, odczyt parametrów. To dominujący wzorzec
produkcyjny wydruków Soneta (enova365, Triva).

> ## ⚠️ Uwaga licencyjna DevExpress — przeczytaj przed użyciem `ReportSnippet`
>
> Klasa `ReportSnippet` i atrybut `[DxBind]` operują **bezpośrednio na typach biblioteki DevExpress
> XtraReports** (`XtraReport`, `XRLabel`, `XRTableCell`, `DetailReportBand`…). Odwołanie się do tych
> typów w kodzie **Twojego dodatku** wymaga posiadania **własnej, komercyjnej licencji DevExpress**.
> **Nie możesz** oprzeć się na licencji, którą Soneta ma na potrzeby projektowania własnych raportów —
> ona nie rozciąga się na kod produkcyjny partnera/klienta.
>
> **Jeśli nie masz licencji DevExpress:** nie odwołuj się w swoim kodzie do obiektów DevExpress
> (typów `XR*`, `XtraReport`). Zamiast tego skorzystaj z **generycznego mechanizmu snippetów** —
> klasy bazowej **`Snippet`** (`Soneta.Business.UI.Snippets`) i atrybutu **`[Bind]`** wiążącego
> **generyczne** elementy **`SnippetLabel`** / **`SnippetCollection`** (nie zależą od DevExpress).
> Ten sam mechanizm wiązania po nazwie i po zdarzeniach działa, ale bez zależności od biblioteki
> DevExpress. Uwaga: raporty w kodzie samej Soneta używają `ReportSnippet`+`[DxBind]`, bo Soneta ma
> licencję DevExpress — nie kopiuj tego wzorca 1:1 do swojego dodatku bez własnej licencji.
>
> Poniższe przykłady z `ReportSnippet`/`XR*` zakładają, że **masz** licencję DevExpress. Wariant bez
> licencji → sekcja „Wariant bez licencji DevExpress” na końcu dokumentu.

## Model w pigułce

```csharp
using System.ComponentModel;
using DevExpress.XtraReports.UI;
using Soneta.Business.UI.DxReports;

namespace MojDodatek.Reports;

// Nazwa klasy MUSI kończyć się sufiksem "Snippet"; dziedziczy po ReportSnippet
public class FakturaSnippet : ReportSnippet {

    // (1) POLE = uchwyt do kontrolki z .repx (dopasowanie po nazwie kontrolki)
    [DxBind] private readonly XRLabel lblRazem;

    // (2) METODA = handler zdarzenia; nazwa "Kontrolka_Zdarzenie"; "Report" = cały raport
    [DxBind(Name = "Report")]
    private void Faktura_DataSourceRowChanged(object sender, DataSourceRowEventArgs e) {
        if (GetCurrentRow() is not DokumentHandlowy dokument) return;   // (3) bieżący rekord danych
        lblRazem.Text = $"{dokument.Wartosc:n2}";
        lblRazem.Visible = dokument.Wartosc > 0;
    }
}
```

W pliku `.repx` snippet jest podpięty jako komponent w `ComponentStorage` (patrz sekcja „Podpięcie”):

```xml
<Item4 Ref="33" ObjectType="Soneta.Business.UI.DxReports.ReportSnippetComponent,Soneta.Business.UI.DxReports"
       Name="Snippet" SnippetTypeName="MojDodatek.Reports.FakturaSnippet,MojDodatek.Reports" />
```

## 5 zasad, które musisz znać

1. **Nazwa klasy = nazwa wydruku + `Snippet`.** Framework wiąże snippet z raportem po nazwie
   (sufiks `Snippet` jest odcinany). Plik `.repx.cs` leży obok `.repx` w katalogu `Repx/`
   (SDK kompiluje go i osadza automatycznie — patrz [REGISTRATION.md](REGISTRATION.md)).
2. **`[DxBind]` na polu** → wstrzyknięcie uchwytu do kontrolki. Nazwa pola musi odpowiadać
   atrybutowi `Name` kontrolki w `.repx` (bez rozróżniania wielkości liter) albo podaj jawnie
   `[DxBind(Name="...")]`. Typ pola = typ kontrolki (`XRLabel`, `XRTableCell`, `DetailReportBand`,
   `AmountLabel`, `Header`, `BusinessDataSource`…).
3. **`[DxBind]` na metodzie 2-argumentowej** `(object sender, XxxEventArgs e)` → handler zdarzenia.
   Nazwa metody `NazwaKontrolki_NazwaZdarzenia` (rozbicie na **ostatnim** `_`). Specjalna nazwa
   kontrolki **`Report`** = cały raport.
4. **Bieżący rekord danych: `GetCurrentRow() as TypObiektu`.** Dostęp do ORM: `Session`, `Context`.
5. **`BeforePrint` woła się wielokrotnie** — zabezpiecz jednorazową inicjalizację flagą/`if (pole is not null) return;`.

## Hierarchia klas: `Snippet` → `ReportSnippet`

Snippety opierają się na dwuwarstwowej hierarchii — to rozróżnienie jest też sednem kwestii
licencyjnej (patrz „Wariant bez licencji DevExpress”):

- **`Snippet`** (przestrzeń `Soneta.Business.UI.Snippets`) — **generyczna, niezależna od DevExpress**
  klasa bazowa (implementuje `IContextable`, `ISessionable`). Nie odwołuje się do żadnego typu
  DevExpress. To jej używa wariant bez licencji DevExpress.
- **`ReportSnippet`** (przestrzeń `Soneta.Business.UI.DxReports`) — dziedziczy po `Snippet` i dokłada
  dostęp do faktycznego raportu DevExpress (`XtraReport`) oraz fasadę nad jego właściwościami i
  zdarzeniami. **Odwołuje się do typów DevExpress** → wymaga licencji DevExpress.

### Klasa `Snippet` (generyczna — bez DevExpress)

Dostępna w obu wariantach (dziedziczą ją wszystkie snippety):

| Składowa | Typ | Rola |
|---|---|---|
| `Context` | `Context` | Kontekst wywołania (parametry, nawigator, sesja). Zwraca `Host.Context`. |
| `Session` | `Session` | Sesja ORM — dostęp do modułów i danych (`XxxModule.GetInstance(Context)`). Zwraca `Context.Session`. |
| `GetCurrentRow()` | `object` | **Bieżący rekord** aktualnie drukowanej bandy. |
| `DataSource` | `object` | Źródło danych (get/set). |
| `Initialize()` / `Uninitialize()` | `protected virtual void` | Haki cyklu życia (wołane po podpięciu / przy odpięciu; rzadko nadpisywane — logika wisi na zdarzeniach). |
| `BindValue(object ctrl, Func<Snippet, object>)` | `protected virtual void` | Punkt rozszerzenia wiązania „wartościowego” dla `SnippetLabel`/`SnippetCollection`. |

> Konwencja: nazwa klasy snippetu **musi** kończyć się sufiksem `Snippet` — sufiks jest odcinany
> przy wiązaniu snippetu z raportem po nazwie.

### Klasa `ReportSnippet` (rozszerzenie DevExpress)

Dziedziczy całe API `Snippet` i dodaje dostęp do raportu DevExpress:

| Składowa | Typ | Rola |
|---|---|---|
| `Report` | `XtraReport` | Faktyczny obiekt raportu DevExpress. |
| `MasterReport` | `XtraReport` | Raport nadrzędny (np. w stopce — rozpoznanie podraportu). |
| `CurrentRowIndex` | `int` | Indeks bieżącego wiersza. |
| `AllControls<T>()` | `IEnumerable<T>` | Iteracja po wszystkich kontrolkach danego typu. |
| `SuspendLayout()` / `ResumeLayout()` | `void` | Wstrzymanie/wznowienie przeliczania układu. |

Dodatkowo wystawia wprost wiele właściwości raportu (np. `PageWidth`, `PageHeight`, `Margins`,
`Landscape`, `Dpi`, `Font`, `CalculatedFields`, `StyleSheet`, `FormattingRuleSheet`) oraz zdarzenia
(`BeforePrint`, `DataSourceRowChanged`, `DataSourceDemanded`).

## Atrybuty wiązania: `[Bind]` i `[DxBind]`

Oba atrybuty dziedziczą po wspólnej bazie `BindAttribute` i mają ten sam mechanizm wiązania po
nazwie/zdarzeniu — różni je tylko to, **co** zwracają.

### Klasa bazowa `BindAttribute`

```csharp
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method,
                AllowMultiple = true)]
public class BindAttribute : Attribute {
    public string Name { get; set; }        // nazwa kontrolki / "Report" / zdarzenia
    public string EventName { get; set; }   // jawna nazwa zdarzenia (gdy nie wynika z nazwy metody)
    public bool   Required { get; set; }     // domyślnie false
}
```

| Właściwość | Domyślnie | Rola |
|---|---|---|
| `Name` | `null` | Jawna nazwa kontrolki (lub `"Report"` = cały raport, albo zdarzenia). Gdy `null` → dopasowanie po nazwie pola/metody. |
| `EventName` | `null` | Jawna nazwa zdarzenia — używana, gdy nazwa metody jej nie niesie (np. `Reload`). |
| `Required` | **`false`** | Brakująca kontrolka jest po cichu pomijana; `Required=true` rzuca wyjątek, gdy kontrolki nie ma. |
| (`AllowMultiple`) | — | Jedno pole/metodę można podpiąć do wielu kontrolek, powtarzając atrybut. |

### Dwie specjalizacje

| Atrybut | Zwraca | Licencja DevExpress | Kiedy |
|---|---|---|---|
| **`[Bind]`** (bazowy) | **generyczne** `SnippetLabel` / `SnippetCollection` (owijki nad kontrolkami) | nie wymaga | Wariant bez licencji — patrz sekcja niżej. |
| **`[DxBind]`** (`: BindAttribute`) | **surowe kontrolki DevExpress** (`XRLabel`, `XRTableCell`, `DetailReportBand`…) | wymaga | Wariant produkcyjny Soneta; `AttributeUsage` = pole/metoda. |

Różnica sprowadza się do tego, co dostajesz w kodzie: `[Bind]` owija kontrolkę w generyczny element
Soneta (`SnippetLabel`/`SnippetCollection`), a `[DxBind]` udostępnia surową kontrolkę DevExpress.
Poniższe przykłady dotyczą `[DxBind]`; odpowiednik `[Bind]` → sekcja „Wariant bez licencji DevExpress”.

**Wiązanie pola (uchwyt do kontrolki):**
```csharp
[DxBind] private readonly XRTableCell cWartosc;          // pole 'cWartosc' ↔ kontrolka Name="cWartosc"
[DxBind(Name = "td_Data")] private readonly XRTableCell komorkaDaty;   // jawna nazwa kontrolki
[DxBind] private readonly DetailReportBand GridPozycje;  // można wiązać bandy…
[DxBind] private readonly BusinessDataSource GridSource; // …komponenty danych…
[DxBind] private readonly Soneta.Business.UI.DxReports.Controls.Header Naglowek;  // …i kontrolki Soneta
```
`[DxBind]` potrafi rozwiązać po nazwie: **raport (`Report`), dowolną kontrolkę, bandę,
komponent `BusinessDataSource`, pole kalkulowane (`CalculatedField`) oraz styl.**

**Wiązanie metody (handler zdarzenia):**
```csharp
[DxBind]                                     // 'lblRazem' + 'BeforePrint'
private void lblRazem_BeforePrint(object s, CancelEventArgs e) { ... }

[DxBind(Name = "Report")]                    // Name z atrybutu nadpisuje prefiks z nazwy metody
private void Cokolwiek_BeforePrint(object s, CancelEventArgs e) { ... }

[DxBind(Name = "GridStraty",  EventName = "BeforePrint")]   // pełne rozdzielenie nazwy i zdarzenia
[DxBind(Name = "GridNadwyzki", EventName = "BeforePrint")]  // ta sama metoda → dwie kontrolki
private void Reload(object s, CancelEventArgs e) { ... }
```
Podkreślenia w nazwie kontrolki są dozwolone (rozbicie następuje na ostatnim `_`), więc metoda
`td_Data_BeforePrint` wiąże kontrolkę `td_Data`, zdarzenie `BeforePrint`.

## Zdarzenia i cykl życia

| Zdarzenie | Args | Kiedy / do czego |
|---|---|---|
| `BeforePrint` | `CancelEventArgs` | **Najczęstsze.** Ustawianie `Text`/`Visible`/`BackColor`; `e.Cancel = true` ukrywa kontrolkę/bandę. |
| `DataSourceRowChanged` | `DataSourceRowEventArgs` | Zmiana bieżącego rekordu bandy — przelicz nagłówek/dane raz na rekord. |
| `PrintOnPage` | `PrintOnPageEventArgs` | Decyzje zależne od strony (np. „drukuj tylko na ostatniej”); `e.Cancel` chowa element na danej stronie. |
| `GetValue` | `GetValueEventArgs` | Zwrot wartości pola kalkulowanego z kodu (`e.Value = …`) — pole zadeklarowane bez `Expression`. Zob. [DATA.md](DATA.md). |

Dodatkowo w handlerach dostępne: `Report.DataAdapter` (bieżący obiekt danych bandy — alternatywa dla
`GetCurrentRow()`), `Tag` (parametr przekazany do podraportu), `MasterReport` (≠ `null` → jesteśmy
podraportem), `Context.Get(out ReportContext rc)` (kontekst nagłówka/stopki). Teksty tłumacz przez
`"…{0}…".TranslateFormat(x)` / `"…".Translate()`; `[TranslateIgnore]` wyłącza tłumaczenie metody.
Gotowe helpery nagłówka/stopki (`ReportTools.GetHeaderData`/`GetFooterData`, `XtraReportHelper.*`) →
[SUBREPORTS.md](SUBREPORTS.md).

> **Pułapka:** `BeforePrint` na tej samej kontrolce odpala się wielokrotnie (raz na wystąpienie).
> Ciężką inicjalizację (odczyt konfiguracji, dane firmy) rób raz — pod strażą flagi:
> ```csharp
> private HeaderData naglowek;
> [DxBind] private void Report_BeforePrint(object s, CancelEventArgs e) {
>     if (naglowek is not null) return;             // guard — tylko pierwszy przebieg
>     naglowek = /* ... odczyt danych z Context/Session ... */;
> }
> ```

## Parametry wydruku

Parametry pytane od użytkownika przed wydrukiem to **zagnieżdżona klasa dziedzicząca po
`ContextBase`** (lub `SerializableContextBase`, gdy mają być pamiętane między sesjami), wstrzykiwana
do snippetu właściwością z atrybutem **`[Context]`**:

```csharp
// primary constructor + inicjalizatory właściwości (wartości domyślne)
public class ParametryContext(Context cx) : ContextBase(cx) {
    [Priority(1)] [Translate] public bool Nadwyzki { get; set; } = true;
    [Priority(2)] [Translate] public bool Straty   { get; set; } = true;
}

[Context]                                            // framework wstrzykuje tę samą instancję,
public ParametryContext Parametry { get; set; }      // którą edytuje użytkownik w oknie parametrów

[DxBind(Name = "Report")]
private void Report_BeforePrint(object s, CancelEventArgs e) {
    GridNadwyzki.Visible = Parametry.Nadwyzki;       // sterowanie bandami przez parametry
    GridStraty.Visible   = Parametry.Straty;
}
```

`[Priority]` ustala kolejność pól, `[Caption]`/`[Translate]` — etykiety w oknie parametrów.
Formularz parametrów można też opisać osobnym plikiem `pageform.xml` (→ **[form-xml](../../form-xml/SKILL.md)**).

> **To ten sam mechanizm, co parametry workerów i list.** Klasa parametrów wydruku jest
> konstruowana identycznie jak klasa parametrów workera/widoku: dziedziczy po `ContextBase`
> (lub `SerializableContextBase` dla trwałości między sesjami), pola z `[Priority]`/`[Caption]`,
> wstrzykiwanie przez `[Context]`, powiadamianie o zmianie przez `InvokeChanged`/`OnChanged`.
> Pełny opis (cykl życia, trwałość, `InvokeChanged`, wzorce) → **[programming](../../programming/SKILL.md)**, temat
> *Klasy parametrów (`ContextBase`)*; sama klasa `Context` i odczyt zaznaczeń/danych z UI →
> **[programming](../../programming/SKILL.md)**, temat *Klasa `Context`*.

## Dostarczanie własnych, policzonych danych

Gdy dane raportu trzeba wyliczyć w kodzie (nie są prostą listą z widoku), snippet liczy kolekcję
i podłącza ją do komponentu `BusinessDataSource`, a bandy (`DetailReportBand`) wiążą się z nią przez
`DataMember`. Dwa warianty:

```csharp
// (A) BusinessDataSource DataKind="Context": ustaw obiekt kontekstu
[DxBind] private readonly BusinessDataSource GridSource;
...
var dane = new ReportData { /* policzone listy */ };
GridSource.Context.Set(dane);

// (B) puste źródło + CustomDataSource: podłącz gotową listę (często z pomocą DxReportHelpers)
var empty = DxReportHelpers.GetDataSourceEmpty(this, "ReportBusinessSource");
List<WierszWydruku> lista =                                     // collection expression + LINQ
    [.. DxReportHelpers.GetDataSourceList<DokRozliczBase>(this)  // lista wejściowa (CurrentList)
          .Select(rec => new WierszWydruku(rec))];
empty.CustomDataSource = lista;
```

`DataKind` komponentu (`CurrentList`/`Context`/`SingleRow`/`Session`) i wiązanie band przez
`DataSource="#Ref-N"` + `DataMember` → [DATA.md](DATA.md). Logikę liczącą dane buduje się według
wzorców z **[programming](../../programming/SKILL.md)**: dostęp do `Session` i transakcji (temat *Sesje, transakcje*),
odczyt danych i modułów ORM, obliczenia w **workerach/extenderach** (temat *Worker i Extender*),
oraz weryfikacja bezpieczeństwa kodu (temat *Zasady bezpiecznego kodu biznesowego*).

## Podpięcie snippetu w `.repx`

Komponent `ReportSnippetComponent` w `ComponentStorage`, atrybut `SnippetTypeName` w formacie
**`PełnaNazwaTypu,Assembly`**:

```xml
<ComponentStorage>
  <Item1 Ref="0" ObjectType="...BusinessDataSource,..." Name="BusinessSource" DataKind="CurrentList" />
  <Item2 Ref="9" ObjectType="...Components.BusinessContext,..." Name="BusinessContext" StylesSource="standardowy" />
  <Item3 Ref="33" ObjectType="Soneta.Business.UI.DxReports.ReportSnippetComponent,Soneta.Business.UI.DxReports"
         Name="Snippet" SnippetTypeName="MojDodatek.Reports.FakturaSnippet,MojDodatek.Reports" />
</ComponentStorage>
```

## Wzorce (recepty)

**Placeholder** — pusty snippet jako punkt zaczepienia (logika w pełni deklaratywna):
```csharp
public class CennikSnippet : ReportSnippet { }
```

**Nagłówek** — jednorazowy odczyt danych firmy + warunkowe pasmo:
```csharp
[DxBind] private readonly XRLabel lblFirma;
private bool init = true;
[DxBind] private void Report_BeforePrint(object s, CancelEventArgs e) {
    if (init) { init = false; lblFirma.Text = /* dane z Context */; }
}
[DxBind] private void subFirstPage_BeforePrint(object s, CancelEventArgs e)
    => e.Cancel = /* nie pierwsza strona */;
```

**Dokument** — przeliczenie per rekord danych:
```csharp
[DxBind(Name = "Report")]
private void Rap_DataSourceRowChanged(object s, DataSourceRowEventArgs e) {
    if (GetCurrentRow() is not DokumentHandlowy dok) return;
    tdNumer.Text = dok.Numer.NumerPelny;
    trRabat.Visible = dok.MaRabat;
}
```

## Wariant bez licencji DevExpress — generyczny `Snippet`

Gdy Twój dodatek **nie ma** licencji DevExpress, kod snippetu nie może odwoływać się do typów
`XR*` / `XtraReport`. Skorzystaj z **generycznej klasy bazowej `Snippet`** (`Soneta.Business.UI.Snippets`)
i atrybutu **`[Bind]`** (zamiast `[DxBind]`). `[Bind]` wiąże po nazwie **generyczne** elementy
**`SnippetLabel`** (etykieta) i **`SnippetCollection`** (kolekcja/podlista) — abstrakcje Soneta, które
wewnętrznie owijają kontrolki raportu, ale **nie wprowadzają zależności od DevExpress**.

Zamiast pobierać uchwyt do kontrolki i ustawiać `.Text`, deklarujesz **getter wartości** — metodę
0-argumentową (lub pole/właściwość), której wynik trafia jako drukowana wartość (`EditValue`) danego
elementu w momencie druku:

```csharp
using Soneta.Business.UI.Snippets;   // generyczny mechanizm — bez DevExpress

namespace MojDodatek.Reports;

public class FakturaSnippet : Snippet {              // generyczna klasa bazowa (nie ReportSnippet)

    // Getter wartości → wiązany do elementu 'lblKontrahent' (SnippetLabel).
    // Wynik staje się drukowaną wartością elementu; brak odwołań do typów DevExpress.
    [Bind(Name = "lblKontrahent")]
    public object Kontrahent() =>
        (GetCurrentRow() as DokumentHandlowy)?.Kontrahent?.Nazwa;   // dostęp do danych — generyczny
}
```

Dostępne (generyczne, bez DevExpress): `Context`, `Session`, `GetCurrentRow()`, `DataSource`,
`Initialize()`/`Uninitialize()`. Parametry (`ContextBase` + `[Context]`) i dostarczanie danych przez
`BusinessDataSource` (`.Context.Set(...)` / `.CustomDataSource`) działają tak samo — to również typy
Soneta, nie DevExpress. Sam plik `.repx` (layout, kontrolki `XR*`) projektuje się w narzędziu z
licencją; **ograniczenie dotyczy kodu Twojego dodatku**, nie samego szablonu.

Mechanizm wiązania (po nazwie i po zdarzeniach) jest ten sam co przy `[DxBind]` — różni się tylko
zestaw typów (porównanie → „Dwie specjalizacje” wyżej).

Gotowy przykład: [../assets/DokumentGeneryczny.repx.cs](../assets/DokumentGeneryczny.repx.cs)
(getter etykiety, getter warunkowy wg parametru, getter kolekcji podlisty). Wariant z licencją
DevExpress: [../assets/Dokument.repx.cs](../assets/Dokument.repx.cs).

## Checklista snippetu

- [ ] **Licencja DevExpress:** jeśli używasz `ReportSnippet`/`[DxBind]`/typów `XR*` — masz własną licencję DevExpress. Jeśli nie — użyj generycznego `Snippet` + `[Bind]` (`SnippetLabel`/`SnippetCollection`), bez odwołań do DevExpress.
- [ ] Klasa `public class NazwaSnippet : ReportSnippet` (lub `: Snippet` bez licencji) — nazwa kończy się na `Snippet`, w `namespace` dodatku.
- [ ] Plik `NazwaWydruku.repx.cs` leży obok `.repx` w katalogu `Repx/`.
- [ ] W `.repx` jest komponent `ReportSnippetComponent` z `SnippetTypeName="Typ,Assembly"`.
- [ ] Pola `[DxBind]` mają typ zgodny z kontrolką i nazwę zgodną z `Name` w `.repx` (lub jawne `Name`).
- [ ] Metody-zdarzenia mają sygnaturę `(object sender, XxxEventArgs e)` i nazwę `Kontrolka_Zdarzenie`.
- [ ] Ciężka inicjalizacja w `BeforePrint` pod strażą flagi (odpala się wielokrotnie).
- [ ] Parametry jako klasa `: ContextBase` + właściwość `[Context]`.
- [ ] Dane liczone: `BusinessDataSource.Context.Set(...)` lub `CustomDataSource`, bandy wiązane `DataMember`.
- [ ] Logika ORM (sesja, workery, odczyt pól) zweryfikowana wg **[programming](../../programming/SKILL.md)**.

## Powiązane

- **[programming](../../programming/SKILL.md)** — warstwa kodu C#, na której opiera się snippet. Konkretne tematy:
  - *Klasy parametrów (`ContextBase`)* — ta sama konstrukcja co parametry wydruku (`[Priority]`, `[Context]`, `InvokeChanged`, trwałość `SerializableContextBase`).
  - *Klasa `Context`* — odczyt danych/zaznaczeń z UI, źródło parametrów.
  - *Sesje, transakcje* — `Session`, dostęp do modułów i danych ORM.
  - *Worker i Extender* — obliczenia dostarczające dane raportu.
  - *Zasady bezpiecznego kodu biznesowego* — checklist do review kodu snippetu.
- **[form-xml](../../form-xml/SKILL.md)** — formularz parametrów wydruku (`pageform.xml`).
- [REGISTRATION.md](REGISTRATION.md) — osadzanie `.repx.cs`, rejestracja wydruku, style.
- [DATA.md](DATA.md) — `BusinessDataSource`, `DataKind`, wiązanie band i `DataMember`.
- [SUBREPORTS.md](SUBREPORTS.md) — snippety nagłówków/stopek/podraportów: helpery `ReportTools`/`XtraReportHelper`, `SubBand` FirstPage/NextPage, sekcje `DetailReportBand` sterowane snippetem.
