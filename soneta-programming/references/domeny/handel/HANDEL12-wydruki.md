# HANDEL12 — Wydruki i raporty

> Wspólne fakty o typie, podstawowe typy i szablon wzorca: [../handel.md](../handel.md).

Wydruk dokumentu handlowego (faktura, dokument magazynowy, paragon) oraz raporty
i zestawienia tworzy się przez **serwis `IReportService`** z modułu `Soneta.Business.UI`.
Serwis bierze wzorzec wydruku (`*.repx` / `*.aspx` / `*.dotx`), kontekst z danymi
(rekord, zaznaczenie, parametry) i zwraca **gotowy dokument jako strumień** (`Stream`) —
bez udziału interfejsu użytkownika. To jest jedyny mechanizm, którego dodatek zewnętrzny
powinien używać do programowego generowania wydruków (export do PDF, wysyłka e-mail,
archiwizacja). Klasa `ReportResult` opisuje *co* i *jak* wydrukować.

> **Dostęp do serwisu (publiczny kontrakt):**
> ```csharp
> using Microsoft.Extensions.DependencyInjection;   // GetRequiredService
> using Soneta.Business.UI;                          // IReportService, ReportResult, ReportFormats, ReportTargets
>
> var raporty = session.GetRequiredService<IReportService>();
> ```

**Metody `IReportService` (publiczne):**

| Metoda | Zwraca | Zastosowanie |
|---|---|---|
| `Stream GenerateReport(ReportResult rr)` | strumień (PDF/XLSX/PNG/…) | generowanie wydruku binarnego do strumienia/pliku/e-maila |
| `string GenerateReportStr(ReportResult rr)` | string | wydruk tekstowy (`HTML`, `TXT`) |
| `void PrintReport(ReportResult rr, bool archive = false, string archivePath = "")` | — | wydruk **na drukarkę** (sprzęt), opcjonalna archiwizacja na dysk |
| `Type[] GetParameterTypes(string templateFileName, Context context)` | typy parametrów | sprawdzenie, jakich obiektów parametrów wymaga wzorzec |

**Pola `ReportResult` (publiczne, najważniejsze):**

| Pole | Typ | Znaczenie |
|---|---|---|
| `TemplateFileName` | `string` | nazwa wzorca (np. `"Sprzedaz.repx"`, `"Zakup.repx"`). Ustawienie go włącza tryb automatyczny (bez UI). |
| `DataType` | `Type` | typ danych branych z kontekstu: `typeof(DokumentHandlowy)` (jeden), `typeof(DokumentHandlowy[])` (zaznaczone), `typeof(DokHandlowe)` (cały widok). |
| `Context` | `Context` | kontekst z rekordem(-ami) i parametrami wydruku (`Context.Set(...)`). |
| `OutputFormat` | `ReportFormats` | `PDF`, `XLSX`, `XLS`, `CSV`, `DOCX`, `TXT`, `HTML`, `MHT`, `PNG`. Domyślnie `HTML`. |
| `Target` | `ReportTargets` | cel: `File`, `Printer`, `PrinterService`, `Preview`, `Attachment`, `Email`, `ShareDocument`, `OpenApplication`. Domyślnie `File`. |
| `AskForParameters` | `bool` | `false` = brak okien z pytaniem o parametry (tryb wsadowy). |
| `PrinterName` | `string` | nazwa drukarki dla `Target = Printer`. |
| `Encrypt` | `string` | hasło szyfrujące PDF. |
| `Sign`, `VisibleSignature` | `bool` | podpis certyfikatem (tylko tryb interaktywny okienkowy). |
| `OutputHandler` | `Func<Stream,object>` | własna obsługa gotowego strumienia (tryb wzorca; **nieobsługiwane przez `IReportService`** — patrz HANDEL-W66). |
| `ReportName` | `string` | nazwa wydruku z menu (tryb interaktywny; **wyklucza się** z `TemplateFileName`/`IReportService`). |
| `ParametersHandler` | `Action<Type,Context>` | wołany dla **każdego** typu parametru wymaganego przez wzorzec, którego brak w kontekście — tu uzupełnij `Context` instancją parametru (patrz pułapka „parametry wzorca” w HANDEL-W62). |

> **Reguła spójności (`CheckConsistency`):** `IReportService` wymaga ustawionego
> `TemplateFileName` i **nie** akceptuje `OutputHandler` ani `ReportName`. `ReportName`
> i `TemplateFileName` wzajemnie się wykluczają. Naruszenie → `ArgumentException`.

---

### HANDEL-W62 — Wydruk faktury do PDF / na drukarkę

**Cel:** wygenerować wydruk pojedynczego dokumentu handlowego (faktura sprzedaży FV,
faktura zakupu FZ, paragon) do strumienia PDF albo wysłać go na drukarkę.

**Warianty:**

| Wariant | Ustawienie | Uwaga |
|---|---|---|
| Faktura sprzedaży → PDF | `TemplateFileName = "Sprzedaz.repx"`, `OutputFormat = PDF` | strumień `%PDF…` |
| Faktura zakupu → PDF | `TemplateFileName = "Zakup.repx"` | analogicznie |
| Wydruk HTML / TXT | `OutputFormat = HTML` / `TXT` | użyj `GenerateReportStr` lub `GenerateReport` |
| Duplikat / oryginał | parametr `ParametryWydrukuDokumentu { Duplikat = … }` w kontekście | parametr wzorca |
| Na drukarkę (sprzęt) | `Target = Printer`, `PrintReport(rr)` | wymaga drukarki — patrz „Pułapki” |
| PDF szyfrowany | `Encrypt = "hasło"` | hasło otwarcia pliku |

**Pola i typy:** `IReportService.GenerateReport(ReportResult) : Stream`,
`ReportResult.TemplateFileName : string`, `ReportResult.DataType : Type`,
`ReportResult.OutputFormat : ReportFormats`, `ReportResult.Context : Context`,
`ParametryWydrukuDokumentu : ContextBase` (parametry wzorca dokumentu, m.in. `Duplikat : bool`).

**Snippet:**

```csharp
using Microsoft.Extensions.DependencyInjection;
using Soneta.Business.UI;
using Soneta.Handel;

// 'dok' to zatwierdzona faktura sprzedaży (FV). 'session' — bieżąca sesja.
var raporty = session.GetRequiredService<IReportService>();

// 1. Kontekst: pojedynczy dokument + jego elementy + parametry wzorca.
var context = Context.Empty.Clone(session);
context.Set(dok);
context.Set(dok.Definicja);
context.Set(dok.Kontrahent);
context.Set(new DokumentHandlowy[] { dok });           // wymagane przez niektóre wzorce
context.Set(new ParametryWydrukuDokumentu(context) { Duplikat = false });

// 2. Opis wydruku — tryb automatyczny (TemplateFileName) → bez UI.
var rr = new ReportResult {
    TemplateFileName = "Sprzedaz.repx",                // "Zakup.repx" dla faktury zakupu
    DataType = typeof(DokumentHandlowy),               // wydruk dla pojedynczego dokumentu
    Context = context,
    OutputFormat = ReportFormats.PDF,
    AskForParameters = false,                          // tryb wsadowy — nie pytaj o parametry
    // Uzupełnij KAŻDY wymagany typ parametru wzorca instancją (ctor z Context). Bez tego
    // GenerateReport zwróci QueryContextInformation zamiast strumienia (patrz pułapki niżej):
    ParametersHandler = (type, cx) => {
        var ctor = type.GetConstructor(new[] { typeof(Context) });
        var par  = ctor != null ? ctor.Invoke(new object[] { cx }) : Activator.CreateInstance(type);
        if (par != null) cx.Set(par);
    }
};

// 3. Generowanie do strumienia i zapis do pliku.
using (Stream pdf = raporty.GenerateReport(rr))
using (var plik = new FileStream(@"C:\Temp\FV.pdf", FileMode.Create, FileAccess.Write))
    pdf.CopyTo(plik);
```

**Pułapki:**
- `GenerateReport` zwraca **`Stream`** dla formatów binarnych (PDF, XLSX, PNG). Dla
  `HTML`/`TXT` użyj `GenerateReportStr` (zwraca `string`). Zwrócony strumień **opakuj w `using`**.
- **Parametry wzorca (najczęstsza przyczyna „Problem z przygotowaniem raportu”):** wzorzec ma
  zwykle własne typy parametrów (np. parametry wydruku faktury), których **instancje muszą być
  w kontekście**. Gdy ich brak, silnik nie ma jak zapytać o nie bez UI i `GenerateReport` zwraca
  `QueryContextInformation` zamiast strumienia → rzut `InvalidOperationException("Problem z
  przygotowaniem raportu")`. Rozwiązanie publiczne i agnostyczne: ustaw `ParametersHandler`, który
  dla każdego żądanego typu tworzy instancję (ctor z `Context`) i woła `Context.Set(...)` (jak w
  snippecie). Listę wymaganych typów podejrzysz wcześniej przez
  `GetParameterTypes(templateFileName, context)`.
- Kontekst musi zawierać też rekord (`Context.Set(dok)`) i tablicę zaznaczeń (`Context.Set(new[]{dok})`).
  W kodzie bez interfejsu **zawsze** ustaw `AskForParameters = false`.
- Wydruk faktury produkcyjnie dotyczy dokumentu **zatwierdzonego** (`Stan == Zatwierdzony`).
  W teście na bazie Demo można wyrenderować FV także **z bufora** — sumy/VAT/płatności są w buforze już
  wyliczone, więc PDF powstaje poprawnie. (Samo zatwierdzenie FV jest wykonalne, ale wymaga zapisania
  pozycji przed `Stan = Zatwierdzony` — patrz pułapka ewidencji VAT / `KrajPodatkuVat` w HANDEL03-W12.)
- Sprawdzenie poprawności PDF w teście: pierwsze 4 znaki strumienia to `"%PDF"`;
  HTML zaczyna się od `"<!DOCTYPE html"`.
- **Druk na fizyczną drukarkę** (`PrintReport`, `Target = Printer`) wymaga sprzętu i
  sterownika. W testach i integracjach **zastępuj go drukiem do pliku tekstowego**: renderuj
  dokument przez `GenerateReportStr(rr)` z `OutputFormat = ReportFormats.TXT` i zapisz do `.txt`
  (`File.WriteAllText`), sprawdzając niepustość treści — to pełny, **nie-sprzętowy** odpowiednik
  wydruku (zweryfikowane HANDEL-W62). Do integracji binarnych użyj `GenerateReport` → strumień/PDF.

---

### HANDEL-W63 — Wydruk dokumentu magazynowego (PZ/WZ/MM)

**Cel:** wydrukować dokument magazynowy (PZ, WZ, MM, RW, PW) — identyczny mechanizm jak
dla faktury, różni się tylko wzorcem dobranym do rodzaju dokumentu (wg jego definicji).

**Warianty:**

| Wariant | Wzorzec / `DataType` |
|---|---|
| Przyjęcie / wydanie magazynowe | wzorzec magazynowy `"Magazyn.repx"`, `DataType = typeof(DokumentHandlowy)` |
| Przesunięcie MM | wzorzec MM |
| Wydruk wg definicji dokumentu | wzorzec domyślny przypisany do `dok.Definicja` |
| Druk do pliku tekstowego (zamiast drukarki) | `"Magazyn.repx"` + `OutputFormat = TXT` + `GenerateReportStr` → `.txt` |

**Pola i typy:** jak w HANDEL-W62 — `IReportService.GenerateReport`, `ReportResult.TemplateFileName`,
`DokumentHandlowy.Definicja` (decyduje o domyślnym wzorcu).

**Snippet:**

```csharp
using Microsoft.Extensions.DependencyInjection;
using Soneta.Business.UI;
using Soneta.Handel;

// 'wz' — zatwierdzony dokument WZ (rozchód magazynowy).
var raporty = session.GetRequiredService<IReportService>();

var context = Context.Empty.Clone(session);
context.Set(wz);
context.Set(wz.Definicja);
context.Set(wz.Magazyn);
context.Set(new DokumentHandlowy[] { wz });
context.Set(new ParametryWydrukuDokumentu(context) { Duplikat = false });

var rr = new ReportResult {
    TemplateFileName = "WydanieZewnetrzne.repx",   // wzorzec właściwy dla danego rodzaju dokumentu
    DataType = typeof(DokumentHandlowy),
    Context = context,
    OutputFormat = ReportFormats.PDF,
    AskForParameters = false
};

using (Stream pdf = raporty.GenerateReport(rr)) {
    // pdf → plik / e-mail / archiwum
}
```

**Pułapki:**
- Dokument magazynowy i faktura to ten sam typ `DokumentHandlowy` — różni je **definicja**
  (`dok.Definicja`) i przypisany wzorzec. Dobierz `TemplateFileName` zgodny z rodzajem
  dokumentu; nie drukuj WZ wzorcem faktury sprzedaży.
- Dla dokumentów magazynowych ustaw w kontekście `dok.Magazyn` (część wzorców go wymaga).
- Nazwy wzorców są elementem konfiguracji wdrożenia (lista wydruków zarejestrowanych dla typu).
  Listę typów parametrów, których wymaga konkretny wzorzec, sprawdzisz przez
  `GetParameterTypes(templateFileName, context)` przed wywołaniem `GenerateReport`.
- **Wzorzec dokumentu magazynowego (PW/PZ/WZ/RW) to `"Magazyn.repx"`** (`DataType =
  typeof(DokumentHandlowy)`). Zamiast druku na drukarkę renderuj do pliku tekstowego tą samą ścieżką
  co faktura: `GenerateReportStr(rr)` z `OutputFormat = ReportFormats.TXT` → `File.WriteAllText(...)`
  i sprawdzenie niepustości — bez sprzętu (zweryfikowane HANDEL-W63).

---

### HANDEL-W64 — Raport dobowy i okresowy (zestawienie za dzień / okres)

**Cel:** wygenerować zestawienie/rejestr dokumentów za **wskazany dzień** (raport dobowy)
lub **wskazany okres** (raport okresowy). Dwie odrębne ścieżki:
1. **Zestawienie/raport bazodanowy** — przez `IReportService` z wzorcem zestawienia i
   parametrem okresu (analizowalny, zapisywalny do PDF/XLSX) — **ścieżka testowalna**.
2. **Raport fiskalny drukarki** (`RaportDobowy`/`RaportOkresowy`) — wydruk na **drukarce
   fiskalnej** przez `IFiscalPrinterAPI` — wymaga sprzętu, **nietestowalny jednostkowo**.

**Warianty:**

| Wariant | Mechanizm | Parametr okresu |
|---|---|---|
| Zestawienie sprzedaży za dzień → PDF | `IReportService` + wzorzec zestawienia, `DataType = typeof(DokHandlowe)` | `FromTo(dzień, dzień)` w parametrach wzorca |
| Zestawienie za okres → PDF/XLSX | jw. | `FromTo(od, do)` |
| Fiskalny raport dobowy (sprzęt) | `IFiscalPrinterAPI.DrukujRaport(nazwaDrukarki)` | dzień bieżący |
| Fiskalny raport okresowy (sprzęt) | `IFiscalPrinterAPI.DrukujRaportOkresowy(nazwaDrukarki, RaportOkresowyParams)` | `RaportOkresowyParams.RaportZaOkres : FromTo` |

**Pola i typy:**
`Soneta.Fiskal.IFiscalPrinterAPI` (publiczny): `DrukujRaport(string nazwaDrukarki)`,
`DrukujRaportOkresowy(string nazwaDrukarki, RaportOkresowyParams pars)`,
`Fiskalizuj(DokumentHandlowy dok, string nazwaDrukarki)`.
`Soneta.Fiskal.RaportOkresowyParams : ContextBase` — `RaportZaOkres : FromTo` (`[Required]`),
inicjalizowany na dzień bieżący; ctor `RaportOkresowyParams(Context)`.

**Snippet:**

```csharp
using Microsoft.Extensions.DependencyInjection;
using Soneta.Business.UI;
using Soneta.Types;          // FromTo, Date

// --- Ścieżka 1: zestawienie bazodanowe za wskazany dzień → PDF (testowalne) ---
var raporty = session.GetRequiredService<IReportService>();

var dzien = Date.Today;
var context = Context.Empty.Clone(session);
context.Set(new FromTo(dzien, dzien));               // parametr okresu wzorca zestawienia

var rr = new ReportResult {
    TemplateFileName = "ZestawienieSprzedazy.repx",  // wzorzec rejestru/zestawienia
    DataType = typeof(Soneta.Handel.DokHandlowe),    // wydruk dla zbioru dokumentów z widoku
    Context = context,
    OutputFormat = ReportFormats.PDF,
    AskForParameters = false
};

using (Stream pdf = raporty.GenerateReport(rr)) {
    // zapis / wysyłka
}

// --- Ścieżka 2: fiskalny raport okresowy (WYMAGA DRUKARKI FISKALNEJ) ---
// var fiskal = session.GetRequiredService<Soneta.Fiskal.IFiscalPrinterAPI>();
// var pars = new Soneta.Fiskal.RaportOkresowyParams(context) {
//     RaportZaOkres = new FromTo(new Date(2026, 6, 1), new Date(2026, 6, 30))
// };
// fiskal.DrukujRaportOkresowy("Posnet Thermal", pars);   // druk na sprzęcie
```

**Pułapki:**
- Rozróżnij dwie rzeczy o podobnej nazwie: **raport dobowy/okresowy drukarki fiskalnej**
  (`IFiscalPrinterAPI`, rozliczenie utargu na sprzęcie) vs. **bazodanowe zestawienie/rejestr**
  za dzień/okres (`IReportService` + wzorzec). Dodatek raportujący zwykle chce ścieżki 2.
- `RaportOkresowyParams.RaportZaOkres` jest `[Required]`; pusty `FromTo` resetuje się do dnia
  bieżącego, a otwarty zakres (`From == MinValue`/`To == MaxValue`) zwija się do jednego dnia.
- **Fiskalny raport (`DrukujRaport*`) wymaga podłączonej drukarki fiskalnej** — operacja
  sprzętowa, **nie do testów jednostkowych**. Testuj wyłącznie ustawienie `RaportOkresowyParams`
  i ścieżkę bazodanową `GenerateReport`.

---

### HANDEL-W65 — Wydruk zbiorczy dla zaznaczonego zbioru dokumentów

**Cel:** wygenerować jeden wydruk obejmujący wiele dokumentów naraz (np. seria faktur z
zaznaczenia listy) zamiast drukować każdy osobno.

**Warianty:**

| Wariant | `DataType` | Kontekst |
|---|---|---|
| Zaznaczone rekordy | `typeof(DokumentHandlowy[])` | `context.Set(tablica)` zaznaczonych dokumentów |
| Wszystkie z widoku | `typeof(DokHandlowe)` | rekordy dostarcza `View`/`ViewInfo` |
| Pojedynczy | `typeof(DokumentHandlowy)` | jeden rekord (HANDEL-W62) |

> `DataType` decyduje, które rekordy trafiają na wydruk: `typeof(T)` — jeden obiekt,
> `typeof(T[])` — zaznaczone, `typeof(Tabela)` — wszystkie z widoku.

**Pola i typy:** `ReportResult.DataType : Type`, `ReportResult.Rows : IEnumerable`
(jawne wskazanie rekordów do wydruku), `Context.Set(DokumentHandlowy[])`.

**Snippet:**

```csharp
using Microsoft.Extensions.DependencyInjection;
using Soneta.Business.UI;
using Soneta.Handel;

// 'zaznaczone' — tablica zatwierdzonych dokumentów do wydruku zbiorczego.
DokumentHandlowy[] zaznaczone = /* ... */;

var raporty = session.GetRequiredService<IReportService>();

var context = Context.Empty.Clone(session);
context.Set(zaznaczone);                       // zbiór rekordów do wydruku

var rr = new ReportResult {
    TemplateFileName = "Sprzedaz.repx",
    DataType = typeof(DokumentHandlowy[]),     // wydruk dla ZAZNACZONYCH rekordów
    Rows = zaznaczone,                         // jawne wskazanie zbioru (opcjonalne)
    Context = context,
    OutputFormat = ReportFormats.PDF,
    AskForParameters = false
};

using (Stream pdf = raporty.GenerateReport(rr)) {
    // jeden strumień PDF z wieloma dokumentami
}
```

**Pułapki:**
- Kluczowa różnica vs HANDEL-W62 to **`DataType = typeof(DokumentHandlowy[])`** — typ tablicowy
  przełącza wzorzec w tryb wielu rekordów. Z `typeof(DokumentHandlowy)` wydrukuje się tylko
  pierwszy/bieżący dokument.
- `Rows` (`IEnumerable`) pozwala jawnie podać zbiór; pole **nie działa dla wydruków z menu**
  (tylko dla automatycznego trybu z `TemplateFileName`).
- Do wydruków masowych ustaw `AskForParameters = false` — inaczej każdy dokument mógłby
  wywołać okno parametrów.
- Wszystkie dokumenty w zbiorze powinny pasować do jednego wzorca (ten sam rodzaj/definicja).

---

### HANDEL-W66 — Zapis wydruku do strumienia/pliku (integracja, e-mail)

**Cel:** uzyskać wydruk jako strumień bajtów, bez drukowania — do zapisania w pliku,
dołączenia jako załącznik do e-maila, archiwizacji lub przesłania do zewnętrznego systemu.

**Warianty:**

| Wariant | Mechanizm |
|---|---|
| Do pliku / strumienia | `GenerateReport` → `Stream` → `FileStream`/`MemoryStream` |
| Wydruk tekstowy (HTML/TXT) | `GenerateReportStr` → `string` |
| Załącznik e-mail | `Target = ReportTargets.Email` lub strumień z `GenerateReport` jako załącznik |
| Z archiwizacją na druk | `PrintReport(rr, archive: true, archivePath: @"C:\Archiwum")` |
| Własna obsługa strumienia (tryb wzorca, **nie** `IReportService`) | `ReportResult.OutputHandler` jako rezultat operacji |

**Pola i typy:** `IReportService.GenerateReport(ReportResult) : Stream`,
`IReportService.GenerateReportStr(ReportResult) : string`,
`ReportResult.OutputFormat : ReportFormats`, `ReportResult.Target : ReportTargets`,
`ReportResult.Encrypt : string` (hasło PDF),
`ReportResult.OutputHandler : Func<Stream, object>` (tylko rezultat operacji UI).

**Snippet:**

```csharp
using Microsoft.Extensions.DependencyInjection;
using Soneta.Business.UI;
using Soneta.Handel;

var raporty = session.GetRequiredService<IReportService>();

var context = Context.Empty.Clone(session);
context.Set(dok);
context.Set(new DokumentHandlowy[] { dok });
context.Set(new ParametryWydrukuDokumentu(context) { Duplikat = false });

var rr = new ReportResult {
    TemplateFileName = "Sprzedaz.repx",
    DataType = typeof(DokumentHandlowy),
    Context = context,
    OutputFormat = ReportFormats.PDF,
    Encrypt = "tajne-haslo",            // (opcjonalnie) PDF chroniony hasłem
    AskForParameters = false
};

// 1. Do pamięci — np. bajty do wysyłki e-mailem przez własny mechanizm:
byte[] pdfBytes;
using (Stream src = raporty.GenerateReport(rr))
using (var ms = new MemoryStream()) {
    src.CopyTo(ms);
    pdfBytes = ms.ToArray();
}
// pdfBytes → załącznik wiadomości, REST API, repozytorium dokumentów...

// 2. Wariant: niech mechanizm sam wyśle e-mail (rezultat operacji w workerze UI):
//    rr.Target = ReportTargets.Email;   // wymaga konfiguracji konta pocztowego i szablonu
```

**Pułapki:**
- `GenerateReport` to właściwa droga dla integracji — zwraca strumień, którym dysponujesz
  dowolnie (plik, e-mail, sieć). **Zawsze `using`** na zwróconym strumieniu (PDF i inne
  formaty binarne).
- `OutputHandler` **nie jest obsługiwany przez `IReportService`** (`CheckConsistency` rzuci
  `ArgumentException`). Służy jako rezultat operacji w trybie wzorca (worker/Command z UI),
  nie do wsadowego generowania w czystym kodzie biznesowym.
- `Target = Email`/`Attachment` to ścieżki integrujące się z modułem pocztowym (konto
  `KontoPocztowe`, szablon `SzablonEmail`) — wymagają pełnej, skonfigurowanej sesji
  aplikacyjnej; w czystym kodzie integracyjnym prościej pobrać strumień z `GenerateReport`
  i wysłać go własnym kanałem.
- Format dobieraj świadomie: `PDF`/`XLSX`/`PNG` → `GenerateReport` (`Stream`);
  `HTML`/`TXT` → `GenerateReportStr` (`string`).
- Szyfrowanie (`Encrypt`) i podpis (`Sign`) dotyczą PDF; podpis certyfikatem działa tylko
  w trybie interaktywnym okienkowym (wymaga okna certyfikatu).

---

> **Co jest testowalne, a co nie (sekcja 12):**
> - **Testowalne i zweryfikowane testami** (`Soneta.Skills.Test/Handel/DokumentyHandlowe/Rozdzial12_WydrukiTest.cs`):
>   generowanie wydruku przez `IReportService.GenerateReport`/`GenerateReportStr` — silnik
>   DevExpress renderuje na bazie Demo (zob. też `Soneta.Wydruki.Test`). Aktywne testy:
>   HANDEL-W62 (FV → PDF, sygnatura `"%PDF"`; oraz **druk do pliku tekstowego** — `GenerateReportStr`
>   + `ReportFormats.TXT` → `.txt`, zamiast druku na drukarkę), HANDEL-W66 (PDF → `byte[]` do integracji),
>   HANDEL-W65 (wydruk zbiorczy `DataType = typeof(DokumentHandlowy[])` + `Rows` → PDF),
>   HANDEL-W64 (render do `ReportFormats.TXT` przez `GenerateReportStr`),
>   HANDEL-W63 (dokument magazynowy wzorcem `"Magazyn.repx"` → plik `.txt`). Warunek: poprawny
>   `ParametersHandler` (uzupełnia parametry wzorca) — inaczej „Problem z przygotowaniem raportu”.
> - **Wydruki fiskalne — mockowane przez `ReportFormats.TXT`:** faktyczny wydruk fiskalny
>   (`Fiscalizer`, `PonownyWydrukParagonuWorker`, `IFiscalPrinterAPI.DrukujRaport*`/`Fiskalizuj`)
>   steruje **drukarką fiskalną** (sprzęt) i ustawia niepubliczne pola (`SymbolKasy`, `EParagon`) —
>   nie do odtworzenia jednostkowo. Jako nie-sprzętowy zamiennik renderuje się dokument do
>   `ReportFormats.TXT` (`GenerateReportStr`) i sprawdza niepustą treść — tak działają testy
>   HANDEL-W64 (raport), HANDEL-W71, HANDEL-W72 (`Rozdzial13_SpecjalistyczneTest.cs`).
> - **Druk na fizyczną drukarkę zastępujemy drukiem do pliku tekstowego:** zamiast `PrintReport`
>   (`Target = Printer`, sprzęt) renderuj `GenerateReportStr` + `ReportFormats.TXT` i zapisz do `.txt`
>   (HANDEL-W62 dla faktury, HANDEL-W63 dla dokumentu magazynowego wzorcem `"Magazyn.repx"`).
> - **Pozostaje nietestowalne (konfiguracja/integracja) — `[Ignore]`:** wysyłka e-mail
>   (`Target = Email`, `OutputHandler` — wymaga konta/szablonu, tryb UI; CheckConsistency odrzuca
>   `OutputHandler` w `IReportService`).

---

