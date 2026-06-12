# KADRY09 — Listy płac, przelewy, wydruki

> Wspólne fakty o typie, podstawowe typy i szablon wzorca: [../kadry.md](../kadry.md).

> **Model.** Lista płac to dokument operacyjny `Soneta.Place.ListaPlac` (root `GuidedRow`,
> tabela `ListyPlac`, `session.GetPlace().ListyPlac`). Trzyma kolekcję wypłat
> `ListaPlac.Wyplaty: SubTable<Wyplata>`. Każda `Wyplata` (root `GuidedRow`, tabela `Wyplaty`)
> wskazuje wstecz listę (`Wyplata.ListaPlac: IRow`) i pracownika (`Wyplata.Pracownik: IRow`).
> Wzorzec listy to `DefinicjaListyPlac` (tabela konfiguracyjna `DefListPlac`,
> `session.GetPlace().DefListPlac`, dostęp `WgSymbolu`/`WgNazwy`).
>
> **`Wyplata` jest abstrakcyjna** — konkretne typy: `WyplataEtat`, `WyplataUmowa`, `WyplataInne`
> (ctor `(ListaPlac listaplac, Pracownik pracownik)` oraz wariant z `IPowiązanieWypłaty`).
> W praktyce wypłat **nie tworzy się ręcznie** — robi to worker naliczania.

### KADRY-I1 — Naliczanie/generowanie list płac (★)

**Cel:** utworzyć listę płac dla wybranego okresu i naliczyć na niej wypłaty pracowników
(etat/umowy), tak by `ListaPlac.Wyplaty` zawierała policzone `Wyplata`.

**Warianty:**

| Wariant | Mechanizm | Uwaga |
|---|---|---|
| Ręczne utworzenie pustej listy | `new ListaPlac()` + `ListyPlac.AddRow(lp)` + pola | sterujesz wszystkim sam |
| Naliczanie wypłat na istniejącej liście | worker `Soneta.Place.NaliczanieWypłat` (akcja `Nalicz`) | tworzy `Wyplata*` i liczy elementy |
| Naliczanie planowanych list (zbiorczo) | worker `Soneta.Place.NaliczaniePlanowanychListPłacWorker` (akcja `Nalicz`) | wg `DefinicjaPlanowanejListyPłac` |

**Pola i typy (`ListaPlac`, kolejność ustawiania jest istotna):**

| Pole | Typ | Uwaga |
|---|---|---|
| `Definicja` | `Soneta.Place.DefinicjaListyPlac` | wzorzec listy; ustawić **pierwsze** po `AddRow` |
| `Wydzial` | `Soneta.Kadry.Wydzial` | tylko gdy `Definicja.Wydzial == true` |
| `Seria` | `string` | tylko gdy `Definicja.Seria == true` |
| `Data` | `Soneta.Types.Date` | data naliczania listy |
| `Naliczanie` | `Soneta.Place.TypNaliczenia` | wartości: `PłatnaZGóry`/`PłatnaZDołu`; **nie ustawiaj** — setter rzuca bez licencji „PL Złoty" |
| `DataWyplaty` | `Soneta.Types.Date` | data postawienia środków; wyznacza mies./rok |
| `MiesiacZUS` | `Soneta.Types.YearMonth` | miesiąc rozliczenia ZUS |
| `Okres` | `Soneta.Types.FromTo` | okres listy; **po** `DataWyplaty` i `Naliczanie` |
| `MiesWstecz` | `int` | |
| `Wyplaty` | `SubTable<Wyplata>` | wypełniana przez worker naliczania |
| `Numer` | `Soneta.Core.NumerDokumentu` | nadawany automatycznie |
| `Bufor` / `Zatwierdzona` | `bool` | stan dokumentu |

**Worker `Soneta.Place.NaliczanieWypłat`** — `[Context]`: `Context`, `ListaPłac: ListaPlac`,
`Pracownik: Soneta.Kadry.Pracownik`; akcja `NaliczanieWypłat Nalicz()`; właściwości
wynikowe m.in. `Wypłaty: IList`, `Nienaliczeni: IEnumerable<BłądNaliczaniaWynagrodzenia>`,
`DataWypłaty/DataListy/DataZUS: Date`, `Okres: FromTo`, `Naliczanie: TypNaliczenia`.

**Worker `Soneta.Place.NaliczaniePlanowanychListPłacWorker`** — `[Context]`:
`Pracownik: Pracownik[]`; `Params Pars` z polami `Definicja: DefinicjaPlanowanejListyPłac`,
`DataWypłaty: Date`, `Okres: FromTo`, `Naliczanie: TypNaliczenia`, `TypWypłaty: TypWyplaty`,
`MiesiącZUS/MiesiącDeklaracji: YearMonth`, `Seria: string`, `MiesWstecz: int`,
`UwzgledniajNieZatwierdzoneListyPlac/EdycjaMiesiącaZUS: bool`;
akcja `NaliczaniePlanowanychListPłac Nalicz()`.

**Snippet (ręczne utworzenie listy + naliczenie wypłaty pracownika):**

```csharp
using Soneta.Business;
using Soneta.Place;
using Soneta.Kadry;
using Soneta.Types;

var place = session.GetPlace();

// 1. Wzorzec listy płac (definicja konfiguracyjna).
var def = place.DefListPlac.WgSymbolu["ETAT"]
          ?? throw new BusException("Brak definicji listy płac".Translate());

// 2. Pusta lista płac — KOLEJNOŚĆ: AddRow → Definicja → daty/naliczanie → Okres.
var lp = new ListaPlac();
place.ListyPlac.AddRow(lp);
lp.Definicja   = def;                          // pierwsze po AddRow
lp.Data        = new Date(2026, 6, 30);
lp.DataWyplaty = new Date(2026, 6, 30);        // wyznacza miesiąc/rok
lp.MiesiacZUS  = new YearMonth(2026, 6);
lp.Okres       = new FromTo(new Date(2026, 6, 1), new Date(2026, 6, 30));  // po DataWyplaty
// Uwaga: NIE ustawiaj lp.Naliczanie — setter rzuca bez licencji „PL Złoty"; getter ma sensowny domyślny.

// 3. Naliczenie wypłaty pracownika — sprawdzona ścieżka to NaliczanieSeryjne (patrz sekcja H);
//    naliczona wypłata zostaje automatycznie powiązana z bieżącą listą płac.
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
var pars = new NaliczanieSeryjne.PracownikParams(context) // context: w UI z workera, w teście z TestBase
{
    DataWypłaty = new Date(2026, 6, 30),
    DataListy   = new Date(2026, 6, 30),
    TypWypłaty  = TypWyplaty.Etat,
};
var wynik = new NaliczanieSeryjne.Pracownika(pars) { Pracownik = pracownik }.Nalicz();

// 4. Powiązanie wypłaty z listą jest dwukierunkowe (Wyplata.ListaPlac / Wyplata.Pracownik):
foreach (Wyplata w in wynik.WszystkieWypłaty)
{
    // w.ListaPlac, w.Pracownik
}

session.Save();
```

**Pułapki:**
- **Kolejność pól krytyczna:** `Okres` i `MiesWstecz` ustaw **po** `DataWyplaty` i `Naliczanie`
  (wzajemne zależności wyliczeń) — patrz wzorzec w kodzie naliczania list.
- `Wydzial`/`Seria` ustawiaj **warunkowo** wg `Definicja.Wydzial`/`Definicja.Seria` — inaczej
  ryzyko niespójności klucza `WgDefinicja`.
- Wypłat **nie twórz przez `new WyplataEtat(...)` ręcznie** — naliczaj. Sprawdzoną ścieżką
  naliczania jest **`NaliczanieSeryjne.Pracownika(...).Nalicz()`** (sekcja H); sam worker
  `NaliczanieWypłat { ListaPłac, Pracownik }.Nalicz()` w bazie Demo potrafi zwrócić pustą listę.
- `Wyplata.ListaPlac`/`Wyplata.Pracownik` to relacje **tylko do odczytu** — powiązania nie ustawisz
  setterem; powstają w trakcie naliczania.
- `ListyPlac` to tabela operacyjna guided — przy odczycie filtruj zakresem (`WgDatyWyplaty`,
  `WgOkresu`, `WgDefinicja`), nie skanuj całości (safe-code §6.3).
- `Wyplata.ListaPlac`/`Wyplata.Pracownik` to `IRow` (relacje interfejsowe) — porównuj/rzutuj
  świadomie.

### KADRY-I2 — Drukowanie/PDF kwitków (pasków) wypłaty (★)

**Cel:** wygenerować pasek (kwitek) wypłaty pracownika do PDF.

**Mechanizm.** Wydruk realizuje serwis **`IReportService`** (namespace `Soneta.Business.UI`,
identycznie jak wydruki handlowe — patrz `handel.md` rozdz. 12). Wzorce pasków to
szablony `*.repx` zarejestrowane atrybutem `[DxReport]` w assembly
**`Soneta.KadryPlace.Reports`** dla `DataType = typeof(Soneta.Place.Wyplata)`:

| Wzorzec (ReportName) | Plik szablonu (`TemplateFileName`) | `DataType` |
|---|---|---|
| „Pasek wypłaty" | `PasekWyplaty.repx` | `Soneta.Place.Wyplata` |
| „Duży pasek wypłaty" | `DuzyPasekWyplaty.repx` | `Soneta.Place.Wyplata` |
| „Paski wypłat" (zbiorczy) | `PaskiWyplaty.repx` | `Soneta.Place.ListaPlac` |

**API (`IReportService` / `ReportResult` — `Soneta.Business.UI`):**
`Stream GenerateReport(ReportResult rr)`,
`ReportResult.TemplateFileName: string`, `.DataType: Type`,
`.OutputFormat: ReportFormats` (`PDF`), `.Context: Context`, `.Target: ReportTargets`.

**Snippet (pasek jednej wypłaty do strumienia PDF):**

```csharp
using Soneta.Business.UI;          // IReportService, ReportResult, ReportFormats
using Soneta.Place;

var raporty = session.GetRequiredService<IReportService>();

var context = Context.Empty.Clone(session);
context.Set(wyplata);              // pojedyncza Wyplata

var rr = new ReportResult {
    TemplateFileName = "PasekWyplaty.repx",
    DataType         = typeof(Wyplata),
    OutputFormat     = ReportFormats.PDF,
    Context          = context,
};

using Stream pdf = raporty.GenerateReport(rr);   // pierwsze 4 bajty == "%PDF"
```

**Pułapki:**
- `IReportService` pobierasz z kontenera: `session.GetRequiredService<IReportService>()`
  (potrzebne `using Microsoft.Extensions.DependencyInjection;`). Serwis i silnik raportów
  (DevExpress) oraz szablony pasków z `Soneta.KadryPlace.Reports` są dostępne **transytywnie** —
  generowanie PDF działa bez dodatkowych referencji (wzorzec jak w `handel.md` rozdz. 12).
- Poprawny PDF zaczyna się od bajtów `"%PDF"` — to wygodna asercja w teście.
- Druk na fizyczną drukarkę (`Target = Printer`, `PrintReport`) wymaga sprzętu — NIE testować.

### KADRY-I3 — Drukowanie/PDF list płac (★)

**Cel:** wygenerować wydruk całej listy płac (pełna lista, zestawienie wypłat) do PDF.

**Mechanizm.** Identyczny jak KADRY-I2 — `IReportService.GenerateReport`, szablony `[DxReport]`
w `Soneta.KadryPlace.Reports`, dla `DataType = typeof(Soneta.Place.ListaPlac)` /
`typeof(Soneta.Place.ListyPlac)`:

| Wzorzec (ReportName) | Plik szablonu | `DataType` |
|---|---|---|
| „Pełna lista płac" | `PelnaListaPlac.repx` | `Soneta.Place.ListaPlac` |
| „Wspólna pełna lista płac" | `Wspolnapelnalistaplac.repx` | `Soneta.Place.ListyPlac` (zbiór) |
| „Paski wypłat" | `PaskiWyplaty.repx` | `Soneta.Place.ListaPlac` |
| Zestawienie wypłat | `ZestawienieWyplat.repx` | `Soneta.Place.ListaPlac` |

**Snippet (pełna lista płac → PDF):**

```csharp
using Soneta.Business.UI;
using Soneta.Place;

var raporty = session.GetRequiredService<IReportService>();

var context = Context.Empty.Clone(session);
context.Set(listaPlac);            // ListaPlac

var rr = new ReportResult {
    TemplateFileName = "PelnaListaPlac.repx",
    DataType         = typeof(ListaPlac),
    OutputFormat     = ReportFormats.PDF,
    Context          = context,
};

using Stream pdf = raporty.GenerateReport(rr);
```

**Pułapki:**
- Mechanizm i dostępność serwisu — jak w KADRY-I2 (działa transytywnie, bez dodatkowych referencji).
- Lista musi być policzona (mieć `Wyplaty`) — inaczej wydruk będzie pusty.
- **Niektóre szablony list wymagają pełnego kontekstu danych.** W bazie Demo wzorzec
  `PelnaListaPlac.repx` potrafi rzucić `InvalidOperationException` („Problem z przygotowaniem
  raportu") na sztucznie utworzonej liście — to ograniczenie konkretnego szablonu/kontekstu, nie
  brak referencji (pasek wypłaty `PasekWyplaty.repx` z KADRY-I2 generuje się poprawnie).
- Do wydruku zbiorczego wielu list ustaw `DataType = typeof(Soneta.Place.ListyPlac)` i przekaż
  zbiór przez `Context.Set(...)` / `ReportResult.Rows`.


### KADRY-I4 — Generowanie przelewów wynagrodzeń (przygotowanie przelewów) (★)

**Cel:** z naliczonej, zatwierdzonej listy płac wygenerować dokumenty przelewu wynagrodzeń
(do paczki przelewów), tak by wypłaty pracowników trafiły do zapłaty/preliminarza i mogły zostać
wyeksportowane do banku (KADRY-I5).

> **Dwie różne klasy `Wyplata` — nie myl ich.** W domenie współistnieją:
> - **`Soneta.Place.Wyplata`** (moduł `PlaceModule`, tabela `Wyplaty`) — *naliczona wypłata
>   pracownika* (wynik naliczania z sekcji H/KADRY-I1); to dokument **płacowy** ze składnikami
>   (`Elementy`), powiązany z listą płac (`Wyplata.ListaPlac`).
> - **`Soneta.Kasa.Wyplata`** (moduł `KasaModule`, tabela `Wyplaty`/`Zaplaty`) — *zapłata kasowa*
>   (rozchód środków). To **ona** implementuje `IDokumentPlatny`/`IDokumentKsiegowalny`, ma pola
>   rozliczeniowe (`DoRozliczenia`, `Stan`, `StanRozliczenia`, `KwotaRozliczona`, `Rozliczono`,
>   `Rozrachunki`, `Zaplaty`, `PreliminarzPoz`, `PozycjePrzelewu`, `BlokadaPrzelewow`).
>
> Mechanizm „z wypłaty do przelewu” łączy oba światy: worker płacowy czyta `Place.Wyplata` z listy
> płac i tworzy dokumenty przelewu w module Kasa (`Soneta.Kasa.PrzelewBase`, w paczce `PaczkaPrzelewow`).

**Mechanizm (publiczny kontrakt — worker płacowy):** sprawdzoną ścieżką tworzenia przelewów z
wynagrodzeń jest worker **`Soneta.Place.ListaPlac.PrzygotujPrzelewyWorker`** (assembly
`Soneta.KadryPlace`, akcja menu *„Przygotuj przelewy”* na liście/listach płac). Kontekstem
działania jest **lista płac** (`Soneta.Place.ListaPlac`) — przygotowuje przelewy dla zatwierdzonych
wypłat tej listy.

**Parametry — `PrzygotujPrzelewyWorker.Params`:**

| Pole | Typ | Uwaga |
|---|---|---|
| `Data` | `Soneta.Types.Date` | data dokumentów przelewu |
| `Paczka` | `Soneta.Kasa.PaczkaPrzelewow` | istniejąca paczka, do której trafią przelewy (opcjonalnie) |
| `DefinicjaPaczki` | `Soneta.Kasa.DefinicjaPaczkiPrzelewu` | definicja, wg której utworzyć nową paczkę (gdy `Paczka == null`) |
| `ZRachunku` | `Soneta.Kasa.RachunekBankowyFirmy` | rachunek firmy obciążany przelewami |
| `Łączone` | `bool` | łączenie przelewów do jednego podmiotu w jeden dokument |
| `ListyPłac` | `string` | opis/oznaczenie list płac (informacyjnie w tytule) |
| `ModyfikacjaTytułów` | `bool` | czy nadpisać tytuły przelewu (`Tytułem1`/`Tytułem2`) |
| `Tytułem1`, `Tytułem2` | `string` | tytuł przelewu (gdy `ModyfikacjaTytułów == true`) |
| `ZEwidencjiZrodlowej` | `bool` | bierz dane rachunku z ewidencji źródłowej |

**Akcja:** `object PrzygotujPrzelewy()` — tworzy w sesji dokumenty `Soneta.Kasa.PrzelewBase`
(tabela `Przelewy`) w paczce `PaczkaPrzelewow`; utrwalenie w bazie wymaga `session.Save()`.

**Model dokumentu przelewu (`Soneta.Kasa.PrzelewBase`, tabela `Przelewy`, root `GuidedRow`):**

| Pole | Typ | Opis |
|---|---|---|
| `Kwota` | `Soneta.Types.Currency` | kwota przelewu |
| `Podmiot` | `Soneta.Kasa.IPodmiotKasowy` | odbiorca (m.in. `Pracownik`, `ZUS`, `UrzadSkarbowy`, `Bank`, `Kontrahent`) |
| `Rachunek` | `Soneta.Kasa.RachunekBankowyPodmiotu` | rachunek odbiorcy |
| `RachunekZleceniodawcy` | `Soneta.Kasa.NumerRachunku` | rachunek firmy (obciążany) |
| `Data` | `Soneta.Types.Date` | data przelewu |
| `Definicja` | `Soneta.Core.DefinicjaDokumentu` | definicja dokumentu |
| `Numer` | `Soneta.Core.NumerDokumentu` | numer (nadawany automatycznie) |
| `Tytulem1`, `Tytulem2` | `string` | tytuł przelewu |
| `Typ2` | `Soneta.Kasa.TypPrzelewu2` | wariant przelewu (zwykły / **MPP** / itp.) |
| `PaczkaPrzelewow` | `Soneta.Kasa.PaczkaPrzelewow` | paczka, do której należy przelew |
| `Bufor` / `Zatwierdzony` | `bool` | stan dokumentu |
| `Exported` | `bool` | czy wyeksportowany (po KADRY-I5 — `true`, blokuje edycję) |

**Przelewy okresowe / MPP:**
- **MPP (mechanizm podzielonej płatności)** to *wariant* przelewu — wyrażany przez
  `PrzelewBase.Typ2: Soneta.Kasa.TypPrzelewu2` (oraz na `Kasa.Wyplata` polem `KwotaMPP`,
  `MozliweMechanizmyMPP`). Dla wynagrodzeń MPP zwykle nie dotyczy (to mechanizm faktur VAT), ale
  kontrakt go przewiduje.
- **Przelewy okresowe** (cykliczne płatności np. składek z list) realizuje osobny worker
  księgowy `Soneta.Ksiega.Kasowe.NaliczaniePrzelewowOkresowych` (poza zakresem płac pracownika).

**Powiązanie z wypłatą / preliminarzem (publiczne kolekcje na `Pracownik`):**

| Kolekcja na `Pracownik` | Typ | Zawiera |
|---|---|---|
| `Pracownik.Przelewy` | `SubTable<Soneta.Kasa.PrzelewBase>` | przelewy pracownika |
| `Pracownik.DokumentyPreliminarza` | `SubTable<Soneta.Kasa.PreliminarzDokument>` | dokumenty preliminarza |
| `Pracownik.DokumentyRozliczeniowe` | `SubTable<Soneta.Kasa.DokRozliczBase>` | dokumenty rozliczeniowe |
| `Pracownik.Rozrachunki` | `SubTable<Soneta.Kasa.RozrachunekIdx>` | rozrachunki |
| `Pracownik.Rachunki` | `SubTable<Soneta.Kasa.RachunekBankowyPodmiotu>` | rachunki bankowe pracownika |

> **Korekta (zweryfikowane kompilacją + skanem DLL):** `Pracownik.Platnosci` **nie istnieje** w publicznym
> kontrakcie kartoteki pracownika — kolekcja `Platnosci` występuje tylko na interfejsie
> `Soneta.Kasa.IDokumentPlatny` (np. `Kasa.Wyplata.Platnosci`), nie na `Pracownik`. Płatności podmiotu
> czytaj przez `Pracownik.Rozrachunki` / `Pracownik.DokumentyRozliczeniowe`.

**Snippet (worker — w UI/teście z dostępnym `Context`):**

```csharp
using Soneta.Business;
using Soneta.Place;          // ListaPlac, ListaPlac.PrzygotujPrzelewyWorker
using Soneta.Kasa;           // PaczkaPrzelewow, PrzelewBase, RachunekBankowyFirmy
using Soneta.Types;

// listaPlac: zatwierdzona lista płac z naliczonymi wypłatami (sekcja KADRY-I1)
var pars = new ListaPlac.PrzygotujPrzelewyWorker.Params
{
    Data = Date.Today,
    // Paczka = istniejacaPaczka,              // albo nowa wg DefinicjaPaczki:
    // DefinicjaPaczki = session.GetKasa().DefPaczekPrzelewow.WgSymbolu["..."],
    // ZRachunku = rachunekFirmy,              // RachunekBankowyFirmy
    Łączone = false,
};

var worker = new ListaPlac.PrzygotujPrzelewyWorker { Pars = pars };
// kontekstem workera jest lista płac; uruchomienie akcji:
worker.PrzygotujPrzelewy();

session.Save();   // utrwalenie dokumentów przelewu w bazie
```

**Pułapki / ograniczenia (bądź szczery):**
- **`Place.Wyplata` ≠ `Kasa.Wyplata`** — pola rozliczeniowe (`DoRozliczenia`, `Stan`,
  `StanRozliczenia`, `Rozrachunki`, `BlokadaPrzelewow`) są na **kasowej** `Soneta.Kasa.Wyplata`
  (`IDokumentPlatny`), nie na płacowej. Skanując „Wyplata” trafia się na kasową.
- **Lista płac musi być zatwierdzona i naliczona** — `PrzygotujPrzelewy` na pustej/niezatwierdzonej
  liście nie ma czego przelać.
- **Wymaga konfiguracji modułu Kasa** — definicji paczki przelewów (`DefinicjaPaczkiPrzelewu`),
  rachunku firmy (`RachunekBankowyFirmy`) oraz rachunku pracownika (`Pracownik.Rachunki`). Brak
  rachunku odbiorcy → przelew nie powstanie albo będzie niekompletny. **W bazie Demo te elementy
  mogą nie być skonfigurowane**, dlatego generowanie przelewów w teście jednostkowym jest
  niepewne (patrz spec testowy).
- Worker **sam zatwierdza zmiany w sesji** (otwiera transakcję) — nie owijaj w dodatkowy
  `session.Logout(true)`; do bazy idą w `Save()`.
- `PrzelewBase.Podmiot`/`Powiazanie` to relacje **interfejsowe** (`IRow`/`IPodmiotKasowy`) —
  rzutuj świadomie.
- `Przelewy` to tabela operacyjna guided — przy odczycie filtruj zakresem (safe-code §6.3).

---

### KADRY-I5 — Eksport wynagrodzeń do banku / pliku przelewów (★)

> **UWAGA — operacja plikowa/integracyjna.** Eksport zapisuje **fizyczny plik** w formacie
> bankowym (Elixir, MT940-pochodne, formaty walutowe). To wejście/wyjście do systemu zewnętrznego —
> **nie jest to przedmiot testu jednostkowego** (zależy od ścieżki na dysku, formatu banku,
> sterownika eksportu i — przy wysyłce online — od sieci). Dokumentujemy **model i publiczny
> kontrakt**, a sam eksport pliku oznaczamy jako nietestowalny jednostkowo.

**Cel:** wyeksportować przygotowane przelewy (KADRY-I4) do pliku przelewów dla systemu bankowości
elektronicznej.

**Mechanizm (publiczny kontrakt — worker Kasa):** worker **`Soneta.Kasa.EksportPrzelewowWorker`**
(akcja menu *„Eksport przelewów”*, metoda `Eksport()`), sterowany przez
`Soneta.Kasa.EksportPrzelewowParams`.

> **Korekta (zweryfikowane kompilacją):** `EksportPrzelewowParams` **nie ma konstruktora
> bezparametrowego** — wymaga `EksportPrzelewowParams(Context ctx, RachunekBankowyFirmy rachunek, PrzelewBase[] przelewy)`.
> Co więcej, **sam konstruktor waliduje rachunek** i rzuca `System.ApplicationException`
> („Eksport niemożliwy. Nie wskazano rachunku w filtrach listy.”), gdy `rachunek == null`. Dlatego nie da się
> utworzyć parametrów samym inicjalizatorem obiektu. W teście jednostkowym kontrakt API weryfikuj **refleksją**
> (istnienie typu, sygnatura konstruktora, property `FileName`/`Params`, metoda `Eksport`), bez instancjonowania.

**Parametry — `Soneta.Kasa.EksportPrzelewowParams`:**

| Pole | Typ | Uwaga |
|---|---|---|
| `FileName` | `string` | **ścieżka pliku wyjściowego** — operacja na dysku |
| `AppendToFile` | `bool` | dopisanie do istniejącego pliku |
| `PrzelewyZgodne` | `IList<Soneta.Kasa.PrzelewBase>` | przelewy do wyeksportowania |
| `Rachunek` | `Soneta.Kasa.RachunekBankowyFirmy` | rachunek firmy (zleceniodawca) |
| `PrmDataPrzelewow` | `Soneta.Types.Date` | data realizacji |
| `PrmNumerPaczki` | `string` | numer paczki |
| `PrmZakres` | `Soneta.Kasa.ZakresEksportuPrzelewow` | zakres (wszystkie / wg paczki / zaznaczone) |
| `EksportujWBuforze` | `bool` | uwzględnij przelewy w buforze |
| `InfoBank`, `InfoFormatKraj`, `InfoFormatWalutowy`, `InfoRachunekBankowy` | `string` | parametry formatu/banku |
| `WithoutHelper` | `bool` | tryb bez kreatora |

**Akcja:** `object Eksport()` — zapisuje plik wg `FileName`. Po eksporcie przelewy są oznaczane
jako wyeksportowane (`PrzelewBase.Exported == true`, blokada dalszej edycji).

**Powiązane (kontekst):**
- Eksport całych **paczek**: worker `Soneta.Kasa.EksportPaczekPrzelewowWorker`.
- Eksport przelewów PPK z pulpitu KBR: `Soneta.EI.UI.PulpitKBR.Workers.PulpitKBEksportPrzelewowWorker`.

**Snippet (kontrakt — w realnej integracji, nie w teście jednostkowym):**

```csharp
using Soneta.Kasa;          // EksportPrzelewowWorker, EksportPrzelewowParams, PrzelewBase
using System.Collections.Generic;

PrzelewBase[] przelewy = /* przelewy z KADRY-I4, np. z paczki */;

// Konstruktor jest WYMAGANY (brak ctora bezparametrowego) i waliduje rachunek (rzuca, gdy null):
var par = new EksportPrzelewowParams(context, rachunekFirmy, przelewy)   // rachunekFirmy: RachunekBankowyFirmy
{
    FileName = @"C:\przelewy\wynagrodzenia.txt",   // ŚCIEŻKA PLIKU — operacja I/O
    PrmDataPrzelewow = Date.Today,
    EksportujWBuforze = false,
};

var worker = new EksportPrzelewowWorker { Params = par };
worker.Eksport();   // zapis pliku na dysk — efekt uboczny poza sesją
```

**Pułapki / ograniczenia (bądź szczery):**
- **Eksport pliku NIE nadaje się do testu jednostkowego** — pisze na dysk, zależy od formatu banku
  i sterownika eksportu; w teście co najwyżej dokumentujemy istnienie API
  (`EksportPrzelewowWorker`, `EksportPrzelewowParams.FileName`), bez wywołania `Eksport()`.
- Format pliku zależy od **konfiguracji formatu eksportu** danego banku — nie ma jednego
  uniwersalnego formatu; `InfoFormat*`/`InfoBank` parametryzują wynik.
- Wysyłka online (bankowość elektroniczna / API banku) to dodatkowo operacja **sieciowa** — poza
  zakresem testów jednostkowych.
- Po eksporcie `PrzelewBase.Exported = true` blokuje edycję — ponowny eksport wymaga
  `EksportujWBuforze`/zmiany stanu.

---

### KADRY-I6 — Wystawienie faktury / faktury zbiorczej z zapłaty (rozliczenia) (★)

> **Zakres i szczerość.** Faktura jest dokumentem **handlowym** (`Soneta.Handel.DokumentHandlowy`),
> nie płacowym — to nie jest funkcja kartoteki pracownika ani list płac. Powiązanie „z zapłaty”
> dotyczy **rozrachunków/rozliczeń** (moduł Kasa): zapłata (`Soneta.Kasa.Wyplata`/`Wplata` —
> `IDokumentPlatny`) jest **rozliczana** z dokumentem płatnym (np. fakturą) przez rozrachunki.
> Wystawianie faktury z poziomu pracownika/płac w publicznym kontrakcie **nie istnieje**;
> tutaj dokumentujemy model rozliczeń, który łączy zapłatę z fakturą.

**Cel:** powiązać zapłatę z dokumentem płatnym (fakturą) na poziomie rozrachunków/rozliczeń —
oraz wskazać, gdzie w publicznym API leży rozliczanie należności/zobowiązań pracownika.

**Model rozliczeń (publiczny kontrakt, moduł `KasaModule`):**

| Element | Typ / kolekcja | Rola |
|---|---|---|
| Zapłata (rozchód/wpływ) | `Soneta.Kasa.Wyplata` / `Soneta.Kasa.Wplata` | dokument płatny (`IDokumentPlatny`) |
| Płatność (zobowiązanie/należność) | `Soneta.Kasa.Platnosc` (tabela `Platnosci`, `IRozliczalny`) | to z nią rozlicza się zapłatę |
| Rozliczenie (powiązanie SP) | `Soneta.Kasa.RozliczenieSP` (tabela `RozliczeniaSP`, `IRozliczenie`) | wiąże zapłatę z płatnością/dokumentem |
| Rozrachunek | `Soneta.Kasa.RozrachunekIdx` (tabela `RozrachunkiIdx`) | indeks rozrachunkowy podmiotu |
| Stan rozliczenia zapłaty | `Wyplata.StanRozliczenia: Soneta.Kasa.StanRozliczenia`, `Wyplata.DoRozliczenia`, `Wyplata.KwotaRozliczona`, `Wyplata.Rozliczono` | ile pozostało / czy rozliczono |

**Kolekcje na zapłacie (`Soneta.Kasa.Wyplata`):**
- `Wyplata.Zaplaty: SubTable<RozliczenieSP>` oraz `Wyplata.Dokumenty: SubTable<RozliczenieSP>` —
  rozliczenia,
- `Wyplata.Rozrachunki: SubTable<RozrachunekIdx>` — rozrachunki,
- `Wyplata.PreliminarzPoz: PreliminarzPozycja` — pozycja preliminarza.

**Kolekcje na `Pracownik` (rozrachunki/faktury podmiotu):**
- `Pracownik.Rozrachunki`, `Pracownik.DokumentyRozliczeniowe`,
  `Pracownik.DokumentyPreliminarza` (jak w tabeli KADRY-I4). **Uwaga:** `Pracownik.Platnosci` **nie istnieje** —
  kolekcja `Platnosci` jest tylko na `IDokumentPlatny` (np. `Kasa.Wyplata.Platnosci`).

**Workery rozliczeniowe (publiczny kontrakt, akcje menu):**

| Worker | Rola |
|---|---|
| `Soneta.Kasa.RozliczWgPrzelewowWyplataWorker` | rozliczenie zapłaty wg przelewów |
| `Soneta.Kasa.RozliczPreliminarzIdxWorker` / `...TblWorker` / `...FrmWorker` | rozliczenie z preliminarzem |
| `Soneta.Kasa.PreliminarzPozycja.DodajRozliczenieWorker` | dodanie rozliczenia do pozycji preliminarza |
| `Soneta.Ksiega.UtworzPlatnoscZZapisuWorker` | utworzenie płatności z zapisu (księga) |

**Faktura zbiorcza:** powstaje po stronie **handlowej** — z wielu zapłat/płatności tworzy się jeden
dokument handlowy (faktura) zbiorąc je jako rozliczenia. To domena `handel.md`
(wystawianie i rozliczanie faktur), nie kartoteki pracownika. Z poziomu rozliczeń pracownika
publiczny kontrakt udostępnia **odczyt i rozliczanie** rozrachunków, a nie „wystaw fakturę”.

**Snippet (odczyt stanu rozliczenia zapłat — publiczny kontrakt):**

```csharp
using Soneta.Kasa;          // Wyplata, StanRozliczenia
using Soneta.Types;

// Zapłaty pracownika rozliczane z dokumentami (np. fakturami) — odczyt stanu rozliczeń.
// Iteruj zawsze w zakresie/okresie (tabela operacyjna guided — safe-code §6.3).
foreach (RozrachunekIdx r in pracownik.Rozrachunki)
{
    // r — pozycja rozrachunkowa pracownika (powiązanie zapłata ↔ dokument)
}

// Stan rozliczenia konkretnej zapłaty kasowej:
// Wyplata zaplata = ...;
// var doRozl   = zaplata.DoRozliczenia;     // ile pozostało do rozliczenia (Currency)
// var stan     = zaplata.StanRozliczenia;   // StanRozliczenia (enum)
// var czyRozl  = zaplata.Rozliczono;        // bool
```

**Pułapki / ograniczenia (bądź szczery):**
- **„Wystaw fakturę z pracownika/płac” nie istnieje w publicznym kontrakcie.** Faktura to dokument
  handlowy; powiązanie z zapłatą realizują **rozrachunki/rozliczenia** (moduł Kasa), nie kartoteka
  pracownika. To zadanie jest z pogranicza domen — opis kierujemy do `handel.md`.
- Pola rozliczeniowe (`DoRozliczenia`, `Stan`, `StanRozliczenia`, `KwotaRozliczona`, `Rozliczono`,
  `Rozrachunki`) są na **`Soneta.Kasa.Wyplata`** (`IDokumentPlatny`), a nie na płacowej
  `Soneta.Place.Wyplata`.
- Rozliczanie/tworzenie faktury zbiorczej **wymaga skonfigurowanego modułu Kasa/Handel** (definicje
  dokumentów, rachunki, płatności). W bazie Demo część konfiguracji może nie być gotowa — operacje
  zapisujące są niepewne w teście (patrz spec testowy).
- `Platnosc`/`RozliczenieSP`/`RozrachunekIdx` to obiekty operacyjne — przy odczycie filtruj zakresem
  i nie skanuj całych tabel (safe-code §6.3).

---

#### Spec testowy (zwarty) — KADRY-I4 / KADRY-I5 / KADRY-I6

Konwencja: `Soneta.Skills.Test/KadryPlace/Pracownik/`, klasa `RozdzialI_ListyWydrukiTest`
(lub nowa `RozdzialI_PrzelewyRozliczeniaTest : PracownikTestBase`); baza Demo + rollback;
operujemy wyłącznie na publicznym kontrakcie.

**KADRY-I4 — `I4_PrzygotujPrzelewy_ZListyPlac`**
- *Co testowalne:* naliczenie wypłaty etatowej (`NaliczanieSeryjne.Pracownika`, jak KADRY-I1b) → uzyskanie
  `ListaPlac` z `Wyplata.ListaPlac`; **konstrukcja** `ListaPlac.PrzygotujPrzelewyWorker` z `Params`
  (asercja, że worker i typ `Params` istnieją w publicznym API; że pola `Data`/`Paczka`/`ZRachunku`
  są dostępne). Odczyt kolekcji `Pracownik.Przelewy`, `Pracownik.DokumentyPreliminarza`,
  `Pracownik.Rozrachunki` (asercja: kolekcje dostępne, iterowalne).
- *Niepewne / `[Ignore]`/`Assert.Ignore`:* faktyczne **wywołanie** `worker.PrzygotujPrzelewy()` i
  powstanie dokumentów `PrzelewBase` — zależy od konfiguracji modułu Kasa (definicja paczki,
  `RachunekBankowyFirmy`, rachunek pracownika `Pracownik.Rachunki`), której baza Demo nie gwarantuje.
  Owinąć w `try/catch` + `Assert.Ignore` z opisem (wzorzec jak KADRY-I2/KADRY-I3) i asercję na powstaniu
  przelewu robić tylko, gdy się udało.

**KADRY-I5 — `I5_EksportPrzelewow_KontraktApi`**
- *Co testowalne:* **istnienie publicznego API** — weryfikacja **refleksją** (NIE instancjonuj!):
  typ `EksportPrzelewowParams`, konstruktor `(Context, RachunekBankowyFirmy, PrzelewBase[])`,
  property `FileName`; typ `EksportPrzelewowWorker`, property `Params`, metoda `Eksport`.
  **Nie używaj inicjalizatora `new EksportPrzelewowParams { ... }`** — nie ma ctora bezparametrowego,
  a ctor `(ctx, rachunek, przelewy)` rzuca `ApplicationException`, gdy `rachunek == null` (brak konfiguracji w Demo).
- *Niewykonalne w teście jednostkowym → `[Ignore]`:* wywołanie `worker.Eksport()` — **operacja
  plikowa** (zapis na dysk wg `FileName`), zależna od formatu banku/sterownika; wysyłka online =
  operacja sieciowa. **Nie wołać `Eksport()`** w teście; udokumentować jako `[Ignore("operacja
  plikowa/sieciowa — poza testem jednostkowym")]`.

**KADRY-I6 — `I6_Rozliczenia_OdczytStanu`**
- *Co testowalne:* odczyt kolekcji rozliczeniowych pracownika — `Pracownik.Rozrachunki`,
  `Pracownik.DokumentyRozliczeniowe`, `Pracownik.DokumentyPreliminarza`
  (asercja: dostępne, iterowalne, typy zgodne — `RozrachunekIdx`, `DokRozliczBase`,
  `PreliminarzDokument`). **`Pracownik.Platnosci` NIE istnieje** — pomiń (kolekcja `Platnosci` jest tylko na
  `IDokumentPlatny`); odczyt pól rozliczeniowych z `Soneta.Kasa.Wyplata` (`DoRozliczenia`,
  `Stan`, `StanRozliczenia`, `Rozliczono`) — gdy istnieje zapłata kasowa w Demo.
- *Niewykonalne / `[Ignore]`:* **wystawienie faktury (zbiorczej) z zapłaty** — funkcja handlowa,
  brak w kontrakcie pracownika; rozliczanie zapisujące (`RozliczWgPrzelewowWyplataWorker`,
  `RozliczPreliminarz*Worker`) wymaga konfiguracji Kasa/Handel → `Assert.Ignore` przy braku danych.
  Dla wystawiania faktur kierować do testów domeny handlowej (`handel.md`).

**Dokładne nazwy (do użycia w testach):**
- Worker płacowy: `Soneta.Place.ListaPlac.PrzygotujPrzelewyWorker` (+ zagn. `.Params`;
  akcja `PrzygotujPrzelewy`).
- Worker eksportu: `Soneta.Kasa.EksportPrzelewowWorker` + `Soneta.Kasa.EksportPrzelewowParams`
  (akcja `Eksport`); paczki: `Soneta.Kasa.EksportPaczekPrzelewowWorker`.
- Dokumenty: `Soneta.Kasa.PrzelewBase` (tabela `Przelewy`), `Soneta.Kasa.PaczkaPrzelewow`
  (tabela `PaczkiPrzelewow`), `Soneta.Kasa.DefinicjaPaczkiPrzelewu`, `Soneta.Kasa.RachunekBankowyFirmy`.
- Rozliczenia: `Soneta.Kasa.Platnosc`, `Soneta.Kasa.RozliczenieSP`, `Soneta.Kasa.RozrachunekIdx`,
  `Soneta.Kasa.PreliminarzDokument`, `Soneta.Kasa.PreliminarzPozycja`.
- Zapłata kasowa (`IDokumentPlatny`): `Soneta.Kasa.Wyplata` (NIE `Soneta.Place.Wyplata`).
- Kolekcje na `Pracownik`: `Przelewy`, `Rozrachunki`, `DokumentyPreliminarza`,
  `DokumentyRozliczeniowe`, `Rachunki` (**bez `Platnosci`** — ta kolekcja jest tylko na `IDokumentPlatny`).

