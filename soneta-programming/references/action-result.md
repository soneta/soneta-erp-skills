# Action result zwracany przez akcje (worker, extender, Command)

> Alternatywna nazwa tego tematu: **rezultaty workera**.

Akcje (metody worker, akcje extender, handlery Command, callbacki) zwracają **action result** — obiekt
sterujący tym, co stanie się w UI po wykonaniu logiki biznesowej. Typ zwróconego obiektu decyduje
o sposobie obsługi.

## Spis treści

- [Najważniejsza zasada](#najważniejsza-zasada)
- [Wartości specjalne](#wartości-specjalne) - `null`, `string`, `bool`
- [Sterowanie aktualnym formularzem](#sterowanie-aktualnym-formularzem) - close, refresh
- [Okna informacyjne i pytania do użytkownika](#okna-informacyjne-i-pytania-do-użytkownika) - MessageBox, potwierdzenia
- [Otwieranie obiektów i okien](#otwieranie-obiektów-i-okien) - edycja, podgląd
- [Nawigacja po programie](#nawigacja-po-programie) - listy, foldery, URL
- [Pliki i strumienie](#pliki-i-strumienie) - pobieranie plików, podgląd
- [Raporty](#raporty) - generowanie i parametry raportów
- [Dialogi parametrów](#dialogi-parametrów) - kreatory, dialog z ContextBase
- [Operacje klienta i bezpieczeństwo](#operacje-klienta-i-bezpieczeństwo)
- [Tabela szybkiego wyboru](#tabela-szybkiego-wyboru) - mapa typ → efekt UI

## Najważniejsza zasada

**Nie wywołuj sam UI z poziomu worker/extender.** Zamiast pokazywać MessageBox, otwierać formularz
czy generować plik — **zwróć odpowiedni action result**. Framework zrobi resztę, panując nad sesją,
transakcją i wątkiem UI.

```csharp
// ŹLE - kod biznesowy nie powinien znać UI
[Action("Sprawdź")]
public void Sprawdz() {
    MessageBoxWindow.Show("Niepoprawne dane".Translate());  // <- nie tak
}

// DOBRZE - zwracamy action result
[Action("Sprawdź")]
public object Sprawdz() {
    return new MessageBoxInformation("Walidacja".Translate(), "Niepoprawne dane".Translate());
}
```

## Wartości specjalne

| Zwrócona wartość | Działanie |
|---|---|
| `null` | Brak akcji. Transakcja jest commitowana. |
| `void` (metoda bez return) | Brak akcji. |
| `Task` / `Task<T>` | Framework czeka na zakończenie i obsługuje zwróconą wartość. |
| `Exception` | Pokazuje okno błędu (`MessageBoxWindow.ShowException`). Transakcja jest commitowana (rollback w wyjątku robi się wcześniej). |

## Sterowanie aktualnym formularzem

### `FormAction` (enum)

Najprostsza forma — sama wartość enum jako action result. Handler konwertuje to
do `FormActionResult` z ustawioną akcją.

| Wartość | Działanie |
|---|---|
| `None` | Brak akcji. |
| `Save` | Zapis bez zamykania, bez potwierdzeń warningów. |
| `SaveWithConfirmation` | Zapis bez zamykania, z potwierdzeniem warningów. |
| `SaveAndClose` | Zapis i zamknięcie, bez potwierdzeń. |
| `SaveAndCloseWithConfirmation` | Zapis i zamknięcie, z potwierdzeniem warningów. |
| `Refresh` | Odczyt danych formularza z bazy. |
| `RefreshOwner` | Odświeżenie formularza-rodzica. |
| `Close` | Zamknięcie bez zapisu, bez ostrzeżeń. |

```csharp
[Action("Zatwierdź")]
public FormAction Zatwierdz() {
    Status = Status.Zatwierdzony;
    return FormAction.SaveAndClose;
}
```

### `FormActionResult`

Rozszerzona wersja `FormAction` — pozwala dodać `EditValue` (kontynuacja po zapisie), `Context`,
`CommittedHandler` (kod wywoływany po commit transakcji).

```csharp
return new FormActionResult {
    Action = FormAction.SaveAndClose,
    EditValue = nowyDokument,         // otwarcie kolejnego obiektu po zapisie
    CommittedHandler = context => null,
};
```

## Okna informacyjne i pytania do użytkownika

### `string`

Krótki komunikat — handler konwertuje do `MessageBoxInformation` z domyślnym tytułem "Informacja".

```csharp
return "Operacja zakończona pomyślnie".Translate();
```

### `MessageBoxInformation`

Pełna kontrola nad okienkiem dialogowym. Przyciski (OK / Anuluj / Tak / Nie) pojawiają się
automatycznie na podstawie ustawionych handlerów (`OKHandler`, `CancelHandler`, `YesHandler`,
`NoHandler`). Handler każdego przycisku jest typu `Func<object>` — **może zwrócić kolejny action result**,
który zostanie obsłużony rekurencyjnie (mechanizm `DelayedHandler`).

| Property | Znaczenie |
|---|---|
| `Caption` | Tytuł okna (NULL = standardowy zależny od `Type`). |
| `Text` / `TextMarkdown` | Treść (czysty tekst lub markdown). |
| `Type` | `Information` / `Warning` / `Error` — kolorystyka. |
| `OKHandler` / `CancelHandler` / `YesHandler` / `NoHandler` | Akcje po wybraniu przycisku. |
| `IsSecondDefault` | `Enter` wywołuje drugą akcję (No/Cancel zamiast Yes/OK). |

```csharp
return new MessageBoxInformation("Potwierdzenie".Translate(), "Czy na pewno usunąć?".Translate()) {
    Type = MessageBoxInformationType.Warning,
    YesHandler = () => {
        Usun();
        return FormAction.RefreshOwner;
    },
    NoHandler = () => null,
};
```

## Otwieranie obiektów i okien

### Zwrócenie `Row` lub dowolnego `object`

Domyślny handler otwiera obiekt w nowym formularzu
(`ObjectWindow` w HTML). Działa to dla każdego typu nieobsługiwanego przez specyficzne handlery — w tym
`GuidedRow`, kreatorów, dialogów.

```csharp
[Action("Otwórz kontrahenta")]
public Kontrahent OtworzKontrahenta() => Wystawca;
```

## Nawigacja po programie

### `NavigationResult`

Przejście do innego folderu danych lub bazy. Działa jak kliknięcie w drzewie folderów.

| Property | Znaczenie |
|---|---|
| `Address` | Ścieżka folderu (np. `"Handel/Sprzedaż/Faktury sprzedaży"`) lub `/<DB>/<ścieżka>` dla innej bazy. |
| `Context` | Kontekst (filtry) folderu. |
| `Target` | `Self` / `NewWindow` / `NewTab`. |
| `KeepCurrentCredentials` | Logowanie do innej bazy aktualnymi danymi (JWT). |
| `KeepSessionLiving` | Przeniesienie żywej sesji do folderu docelowego (`Self` only). |
| `KeepCurrentViewInHistory` | Aktualny widok zostaje w historii nawigacji. |

Najważniejsze ścieżki do folderów aplikacji znajdują się w skill `/soneta-mcp-ui/common-folders.md`.

```csharp
return new NavigationResult("Handel/Sprzedaż/Faktury sprzedaży") {
    Target = NavigationTarget.NewTab
};
```

### `HyperlinkResult`

Otwarcie URL w przeglądarce.

```csharp
return new HyperlinkResult {
    Address = "https://soneta.pl",
    Target = NavigationTarget.NewWindow,
};
```

### `CommandMenu`

Menu poleceń wyświetlone użytkownikowi (drop-down z opcjami). Po wybraniu wykonuje się akcja
przypisana do polecenia.

### `LoginParameters`

Wymusza przelogowanie (np. po zmianie operatora). Framework wywołuje `LoginService.Relogin`.

## Pliki i strumienie

### `NamedStream`

Pojedynczy plik do pobrania przez przeglądarkę lub ramkę SonetaFrame.

```csharp
return new NamedStream("raport.pdf", () => GenerujPdfStream());
```

### `NamedStream[]`

Tablica plików → spakowane jako ZIP (`Pliki_yyyyMMddHHmmssfff.zip`) i pobrane jako jeden plik lub zapisywane pojedynczo 
przez ramkę SonetaFrame.

### `StorageFileEditor`

Edycja pliku przechowywanego w storage bazy danych (XML, DOCX, REPX, itp.)

## Raporty

### `ReportResult`

Główny action result raportu — uruchamia mechanizm wydruków. Działa w **trzech trybach**:

1. **Tryb interaktywny (menu)** — bez `TemplateFileName`, framework otwiera okno raportu i ewentualne
   okno parametrów (`UseReportMenu == true`). Stosować, gdy chcemy pokazać użytkownikowi listę
   dostępnych wzorców dla danego typu/ViewInfo.
2. **Tryb automatyczny (z kodu)** — z `TemplateFileName`, bez UI raportu. Wzorzec wskazany jawnie,
   wydruk od razu generowany. Można dodać `OutputHandler` do przejęcia strumienia wynikowego.
3. **Tryb serwisowy (bez rezultatu)** — przez `IReportService.GenerateReport / GenerateReportStr /
   PrintReport`. Stosować w testach, taskach tła i kodzie nie-UI.

#### Najważniejsze property

| Property | Znaczenie |
|---|---|
| `TemplateFileName` | Nazwa lub ścieżka wzorca (`.repx`, `.repx.cs`, `.aspx`, `.dotx`) lub XML z ustawieniami wydruku. Ustawienie wyklucza `ReportName`. |
| `TemplateFileSource` | `AspxSource.Local` (plik na dysku) / `Storage` (konfiguracja w bazie — pliki w `XtraReports/...`). |
| `ReportName` | Nazwa raportu z menu (tryb interaktywny). Wyklucza `TemplateFileName`. |
| `DataType` | Typ danych — `typeof(Towar)` (jeden obiekt), `typeof(Towary)` (cały View), `typeof(Towar[])` (zaznaczone wiersze). Steruje też ładowaniem listy dostępnych wydruków. |
| `ViewInfo` | ViewInfo źródłowego widoku (np. dla raportów listy z customowym ViewInfo). |
| `ViewNames` | Nazwy widoków, w których wyszukać raport po nazwie. |
| `Rows` | `IEnumerable` z wierszami źródłowymi (zamiast pobierania z `Context`). **Nie działa** w trybie menu. |
| `Context` | Kontekst raportu — źródło parametrów (`Context.Set(params)`), zaznaczeń, kultury. |
| `OutputFormat` | `HTML` (domyślnie) / `PDF` / `TXT`. |
| `Target` | `File` / `Preview` / `Printer`. Z `OutputHandler` traktowany jak `File`. |
| `PrinterName` | Drukarka dla `Target = Printer`. |
| `CultureInfo` | Język wydruku (nadpisuje kulturę sesji — patrz `DxReport_Generator_Dx_Zakup_English_ReportResult`). |
| `AskForParameters` | `false` = wydruk bez pytania o parametry (wymaga ustawionych parametrów w `Context`). |
| `Sign` / `VisibleSignature` / `Encrypt` | Podpis certyfikatem i hasło PDF (tylko tryb interaktywny w wersji desktop). |
| `OutputHandler` | `Func<Stream, object>` — przejmuje wygenerowany strumień, jego wynik staje się rezultatem akcji. Tylko z `TemplateFileName`. |
| `ParametersHandler` | `Action<Type, Context>` wywoływany przed pytaniem o parametry — sposób na zainicjowanie obiektów parametrów (oddzielnie dla każdego typu). |
| `Caption` | Tytuł okna. |

#### Wzorzec: wydruk z akcji (worker / extender)

```csharp
[Action("Drukuj fakturę")]
public object DrukujFV() {
    Context.Set(new SprzedazSnippet.MyParametryWydruku(Context));
    return new ReportResult {
        TemplateFileName = "Sprzedaz.repx",
        DataType = typeof(DokumentHandlowy),
        Context = Context,
        AskForParameters = false,
    };
}
```

#### Wzorzec: wydruk z testu / taska (bez UI)

```csharp
var rs = Session.GetRequiredService<IReportService>();

Context.Set(new SprzedazSnippet.MyParametryWydruku(Context));

var rr = new ReportResult {
    TemplateFileName = "Sprzedaz.repx",
    DataType = typeof(DokumentHandlowy),
    Context = Context,
    AskForParameters = false,
};

string html = rs.GenerateReportStr(rr);             // HTML jako string
// lub
using var stream = rs.GenerateReport(rr);           // strumień (PDF / inny)
// lub
rs.PrintReport(rr, archive: true, archivePath: "C:\\Archive");
```

#### Wzorzec: wzorzec ze storage (XtraReports/Wzorce użytkownika)

```csharp
var rr = new ReportResult {
    TemplateFileName = "XtraReports/Wzorce użytkownika/EmptyReport.repx.cs",
    TemplateFileSource = AspxSource.Storage,
    Context = Context,
    AskForParameters = false,
};
```

#### Wzorzec: zaznaczone wiersze + custom params

```csharp
Context.Set(towary);                         // tablica zaznaczonych Row
Context.Set(new CennikViewInfo.CennikParams(Context));

var rr = new ReportResult {
    DataType = typeof(Towar[]),               // l.mn. = zaznaczone
    TemplateFileName = "...",
    TemplateFileSource = AspxSource.Storage,
    Context = Context,
    OutputFormat = ReportFormats.TXT,
    AskForParameters = false,
};
```

#### Wzorzec: jeden obiekt + powiązane konteksty

```csharp
var fvNewSession = Session.Get(fv);
Context.Set(fvNewSession);                    // single
Context.Set(new[] { fvNewSession });          // jako tablica (dla DataType = Towar[])
Context.Set(new SprzedazSnippet.MyParametryWydruku(Context));

var rr = new ReportResult {
    TemplateFileName = "Sprzedaz.repx",
    DataType = typeof(DokumentHandlowy),
    Context = Context,
    AskForParameters = false,
};
```

#### Wzorzec: wymuszenie języka wydruku

```csharp
var rr = new ReportResult {
    TemplateFileName = "Zakup.repx",
    DataType = typeof(DokumentHandlowy),
    Context = Context,
    OutputFormat = ReportFormats.TXT,
    CultureInfo = CoreTools.CultureEN,
    AskForParameters = false,
};
```

#### Sprawdzenie wymaganych typów parametrów

Przykład uzupełnia context standardowymi wartościami parametrów potrzebnymi dla raportu. 

```csharp
Type[] paramTypes = rs.GetParameterTypes("Sprzedaz.repx", Context);
foreach (Type t in paramTypes) {
    Context.Set(Activator.CreateInstance(t, Context));
}
```

#### Deprecated property

- `TemplateType` → używać `TemplateFileName`.
- `Format` (`ReportResultFormat`) → używać `OutputFormat` (`ReportFormats`).
- `Preview` → używać `Target = ReportTargets.Preview`.
- `SilentProgress` → używać `!AskForParameters`.

### `ReportOrganizerResult`

Menedżer raportów (lista raportów dla tabeli/widoku) — okno zarządzania zestawem raportów dla
podanego typu danych.

### `ReportEditorResult`

Otwarcie edytora raportu (DOTX, DX) — w zależności od `Format` wybierany jest odpowiedni handler.

### `PdfResult`

Pojedynczy PDF (do podglądu, druku lub pobrania) sterowany przez `Action`:
`Preview` / `Print` / `Save` / `OpenInBrowser`.

### `PdfResult[]`

Tablica PDF — handler (`PdfsResultHandler`) wysyła wszystkie na drukarkę przez `IPrintingService`.

### `DotxReportPrintResult`, `TextReportPrintResult`, `DxReportPrintResult`

Specjalizowane action result wydruku / podglądu konkretnych formatów (DOTX edytor, raport tekstowy,
DevExpress).

## Dialogi parametrów

### `QueryContextInformation`

Otwiera okno z parametrami uzupełniającymi `Context` przed wywołaniem właściwej operacji. To samo
okno pojawia się przed niektórymi czynnościami z menu lub przed raportami. Po `OK` wywoływany jest
handler, który dostaje uzupełnione obiekty parametrów jako argumenty. Po `Cancel` — `CancelHandler`.

#### Mechanizm

1. Akcja zwraca `QueryContextInformation` z handlerem typu `Func<Tn, object>`.
2. Framework patrzy na typy parametrów handlera (`T1`, `T2`, ...).
3. Dla każdego typu sprawdza, czy `Context` zawiera już instancję — jeśli **tak**, pomija
   pytanie i od razu wywołuje handler.
4. Jeśli czegoś brakuje, otwiera okno parametrów (renderowane przez `QueryContextRenderer`).
5. Po `OK` wywołuje handler z parametrami pobranymi z `Context`.
6. Wartość zwrócona z handlera staje się **kolejnym action result** (rekurencyjnie obsługiwana).

#### Tworzenie — fabryki `Create`

```csharp
// Jeden typ parametrów
QueryContextInformation.Create<Params>(p => DoWork(p));

// Kilka typów (do 6)
QueryContextInformation.Create<Params1, Params2>((p1, p2) => DoWork(p1, p2));

// Context + parametry (Context można dostać jako pierwszy parametr)
QueryContextInformation.Create<Context, Params>((cx, p) => DoWork(cx, p));

// Bez handlera — sama informacja "wypełnij te typy w kontekście"
QueryContextInformation.CreateForTypes(typeof(Params1), typeof(Params2));

// Z dowolnego Delegate
QueryContextInformation.CreateFromHandler(myDelegate);
```

#### Klasa parametrów

Typowo dziedziczy z `ContextBase` (przyjmuje `Context` w konstruktorze, ma `OnChanged()` do
odświeżania), używa atrybutów Soneta i `System.ComponentModel`:

```csharp
[Caption("Parametry wydruku")]
class MyParams(Context context) : ContextBase(context) {

    [Category("Daty")]                      // grupowanie wizualne
    [Caption("Data od")]
    [Priority(1)]                           // kolejność w grupie
    [Accessor(AutoChange=true)]
    public Date DataOd { get; set; }

    [Category("Daty")]
    [Caption("Zakres")]
    [Priority(3)]
    [Accessor(AutoChange=true)]
    public FromTo Okres { get; set; }

    [Category("Informacje")]
    [Caption("Opis")]
    [Required]
    [Accessor(AutoChange=true)]
    public string Info { get; set; }

    [Hidden]                                // ukrycie pola
    public int Internal { get; set; }
}
```

Renderer:

- grupuje pola po `[Category]` (`GroupContainer` z `CaptionHtml = nazwa kategorii`),
- sortuje wewnątrz grupy po `[Priority]` (rosnąco),
- pomija pola z `[Hidden]` / `[Browsable(false)]`,
- `[Accessor(AutoChange=true)]` zadba o automatycznie wywołanie `Session.InvokeChanged()` na `set`.
- może renderować tę samą kategorię wielokrotnie, jeśli pola są przeplatane innymi.

#### Wzorzec: zapytanie o parametr w workerze

```csharp
[Action("Zmień podstawę zwolnienia")]
public object Zmien() {
    return QueryContextInformation.Create<Params>(p => {
        using var t = Towar.Session.Logout(true);
        Towar.PodstawaZwolnienia = p.Podstawa;
        t.Commit();
        return null;
    });
}
```

#### Wzorzec: łańcuch dialogów (wynik handlera = kolejny `QueryContextInformation`)

```csharp
[Action("Dwuetapowy dialog")]
public object Wieloetapowo() {
    return QueryContextInformation.Create<Params1>(p1 =>
        QueryContextInformation.Create<Params2>(p2 => {
            using var t = Session.Logout(true);
            // wykorzystaj p1 i p2
            t.Commit();
            return null;
        })
    );
}
```

#### Wzorzec: dialog wewnątrz `FormActionResult.CommittedHandler`

```csharp
return new FormActionResult {
    EditValue = new SecondTestObject { Context = Context },
    UseDialog = true,
    CommittingHandler = cx => FormAction.Close,
    CommittedHandler = cx => QueryContextInformation.Create<Params>(p => {
        using var t = Towar.Session.Logout(true);
        Towar.PodstawaZwolnienia = p.Podstawa;
        t.Commit();
        return Towar;        // otworzy formularz dla Towar po pytaniu
    })
};
```

#### Wzorzec: dialog z `MessageBoxInformation` jako wynikiem

```csharp
public object Pokaz() {
    return QueryContextInformation.Create((MessageParams p) =>
        new MessageBoxInformation {
            Caption = "Test".Translate(),
            Text = p.Message,
            Type = p.IsWarning
                ? MessageBoxInformationType.Warning
                : MessageBoxInformationType.Information
        });
}
```

#### Wzorzec: dialog z plikami (`NamedStream[]`)

`QueryContextInformation` obsługuje też typy z plikami — w klasie parametrów `NamedStream` lub
`NamedStream[]` z `GetListXxx()` zwracającym `FileDialogInfo`:

```csharp
class NsArgs : ContextBase {
    public NsArgs(Context cx) : base(cx) {}

    public NamedStream[] Streams { get; set; }

    public FileDialogInfo GetListStreams()
        => new FileDialogInfo { InitialDirectory = "x:\\" }
            .AddTxtFilter()
            .AddXmlFilter();
}

// użycie:
return QueryContextInformation.Create((NsArgs ns) => ns.Streams[0].FileName);
```

#### Property obiektu

| Property | Znaczenie |
|---|---|
| `Context` | Kontekst, na którym pracuje okno. Standardowo `null` → użyty zostanie kontekst wywołania. |
| `Caption` | Tytuł okna. |
| `Data` | Dane przekazane do konstruktora (rzadko używane bezpośrednio). |
| `Types` | Tablica typów do uzupełnienia w kontekście — gdy używamy `CreateForTypes`. |
| `AcceptHandler` | `Func<object>` bez parametrów. Alias na handler ustawiony przez `Create<>`. |
| `CancelHandler` | `Func<object>` — wynik traktowany jak kolejny action result. |
| `ResultHandler` | `Func<object, object>` — postprocessing wartości zwróconej z handlera. |
| `ObjectConstructor` | `ConstructorInfo` do tworzenia obiektu wynikowego (zaawansowane). |
| `GetHandler()` | Zwraca `Delegate` przekazany do `Create<>` (dla refleksji w testach). |
| `SetCaption(c)` / `SetCancelHandler(h)` | Fluent setter. |

#### `TryInvoke(Context)`

Pozwala uniknąć okna, gdy parametry **są już** w kontekście:

```csharp
// 1) gdy w kontekście brakuje Params - zwraca self (otwarcie okna)
object r = QueryContextInformation
    .Create<Params>(MyAction)
    .TryInvoke(cx);
// r is QueryContextInformation == true

// 2) gdy Params jest w kontekście - wywołuje handler natychmiast
cx.Set(new Params(cx) { ... });
object r = QueryContextInformation
    .Create<Params>(MyAction)
    .TryInvoke(cx);
// r == wynik MyAction
```

#### Walidacja typu propertysów

Pola z atrybutem `[Context<T>(nameof(T.Prop))]` muszą mieć właściwy typ wartości docelowej (typ
property `T.Prop`). Niezgodność → `Context.InvalidValueException` przy wywołaniu
`ContextInvoker.CreateObject<>()`.

### `StartActionInformation`

Wewnętrzny action result — uruchomienie akcji w nowym kontekście (używany przez framework
do delegowania wykonania workera). Rzadko zwracany ręcznie.

## Operacje klienta i bezpieczeństwo

### `ClientActionResult`

Wywołanie operacji w aplikacji klienckiej **SonetaFrame** (desktop wrapper). Działa tylko gdy
`BusTools.DeviceType.IsBrowserApp()`. Pozwala wykonać akcję po stronie klienta (otwarcie pliku
lokalnego, integracja z systemem operacyjnym) i opcjonalnie odebrać odpowiedź przez `ResponseHandler`.

### `MfaRequest` / `MfaSourceBase`

2FA (Multi-Factor Authentication) — uruchamia okno weryfikacji tożsamości użytkownika. Handler
deleguje do serwisu `IMfaHandleResolver`. Stosować dla akcji wymagających dodatkowego potwierdzenia.

### `ClipboardStream`

Operacja na schowku przeglądarki (kopiowanie do / odczyt z).

## Tabela szybkiego wyboru

| Co chcę zrobić | Co zwrócić |
|---|---|
| Pokazać komunikat | `string` lub `MessageBoxInformation` |
| Pokazać błąd | `Exception` (throw) lub `MessageBoxInformation` (Type = Error) |
| Zapisać/zamknąć formularz | `FormAction.Save*` / `FormAction.Close` |
| Zadać pytanie tak/nie | `MessageBoxInformation` z `YesHandler`/`NoHandler` |
| Otworzyć obiekt do edycji | sam obiekt (`Row` lub inny) |
| Otworzyć inny folder | `NavigationResult` |
| Otworzyć stronę WWW | `HyperlinkResult` |
| Pobrać plik | `NamedStream` |
| Pobrać archiwum plików | `NamedStream[]` |
| Wydruk PDF | `PdfResult` lub `PdfResult[]` |
| Raport | `ReportResult` |
| Zapytać o parametry | `QueryContextInformation` |
| Nic nie robić | `null` / `void` |

