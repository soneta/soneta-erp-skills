# WORKFLOW08 — Zadania CRM (aktywności)

> Wspólne fakty o typie, podstawowe typy i szablon wzorca: [../workflow.md](../workflow.md).

> **Zadanie CRM ≠ zadanie workflow.** Zadanie CRM (aktywność) to `Soneta.Zadania.Zadanie`
> (tabela `Zadania`, dane **operacyjne**, moduł `ZadaniaModule` — `session.GetZadania()`),
> wątek niezależny od `Soneta.Business.Db.Task` (zadania silnika workflow, WORKFLOW05–07).
>
> **Model „definicja + słowniki na definicji".** Każde zadanie ma wymaganą referencję
> `Definicja: Soneta.Zadania.DefZadania` (tabela `DefZadan`, dane **konfiguracyjne**). To definicja
> niesie słowniki, z których czerpie zadanie:
>
> | Słownik na definicji | Kolekcja | Element | Pole na zadaniu |
> |---|---|---|---|
> | Stany | `DefZadania.Stany: LpSubTable<StanZadania>` | `Soneta.Zadania.StanZadania` | `StanZadania` (+ kopia `StanIdent: int`) |
> | Priorytety | `DefZadania.Priorytety: LpSubTable<PriorytetZadania>` | `Soneta.Zadania.PriorytetZadania` | `PriorytetZadania` (+ `PriorytetIdent: int`) |
> | Typy | `DefZadania.Typy: LpSubTable<TypZadania>` | `Soneta.Zadania.TypZadania` | `TypZadania` |
>
> Rodzaj aktywności wyznacza enum `Soneta.Core.RodzajZadania` na definicji (`Zadanie = 1`,
> `Zdarzenie = 2`, `Zlecenie = 3`, `Wypożyczenie = 4`, dalsze rodzaje wewnętrzne/branżowe) —
> zadanie dziedziczy go z definicji (pole `Zadanie.Rodzaj` jest pochodną `Definicja.Rodzaj`).
> Nowe zadanie najbezpieczniej tworzyć przez **`DefZadania.Create()`** — metoda dobiera właściwy
> typ wiersza dla rodzaju definicji.
>
> Edycja `DefZadania`/`StanZadania`/`PriorytetZadania` to praca na **konfiguracji** (transakcja na
> sesji konfiguracyjnej — `login.CreateSession(false, true)`); samo dodawanie i edycja zadań to
> zwykłe dane operacyjne. W bazie Demo istnieją gotowe definicje aktywności (np. symbol `ZAD`) —
> mimo to pobieraj definicję dynamicznie i nie twórz jej „w locie".

### WORKFLOW-H1 — Utworzenie nowego zadania (★)

**Cel:** utworzyć nową aktywność CRM (zadanie/zdarzenie) z minimalnym kompletem danych
pozwalającym na `Session.Save()`.

**Mechanizm (kluczowy):** zadanie tworzy się na podstawie definicji — `DefZadania.Create()`
zwraca **odłączony** wiersz właściwego typu (zależnego od `Rodzaj` definicji) z ustawioną
`Definicja`; po `session.AddRow(...)` w `OnAdded` następuje inicjalizacja z definicji:
pierwszy stan (`StanZadania` o najniższym `Lp`), pierwszy priorytet, `Prowadzacy` = zalogowany
operator, `Wykonujacy` = zalogowany operator (gdy definicja nie jest „dla roli/uprawnienia"),
nazwa z definicji (gdy `DefZadania.InicjujNazwe`), numer wg `DefZadania.Numeracja`.

**Warianty:**

| Wariant | Jak |
|---|---|
| Zadanie / zdarzenie / zlecenie / wypożyczenie | wybór definicji o odpowiednim `Rodzaj: Soneta.Core.RodzajZadania` |
| Utworzenie z kontrolą typu wiersza | `definicja.Create<T>()` (rzuca `ArgumentException` przy niezgodności typu) |
| Wykonawca: operator | `Wykonujacy: Soneta.Business.App.Operator` (domyślnie zalogowany) |
| Wykonawca: rola | `ZadanieDlaRoli = true` + `Role: Soneta.Business.App.Role` (nośnik: `RoleGuid: Guid`) |
| Wykonawca: uprawnienie | `Uprawnienie: Soneta.Business.App.Entitle` (nośnik: `EntitleGuid: Guid`) |
| Jednorazowe / cykliczne | cykl = kopiowanie aktywności (WORKFLOW-H3) |

**Pola i typy (najważniejsze, `Soneta.Zadania.Zadanie`):**

| Pole | Typ | Uwagi |
|---|---|---|
| `Definicja` | `Soneta.Zadania.DefZadania` | wymagane; ustawiane przez `Create()` |
| `Nazwa` | `string` | krótka nazwa aktywności |
| `Opis` | `Soneta.Business.MemoText` | dokładny opis (przypisanie `string` działa — konwersja niejawna) |
| `Prowadzacy` | `Soneta.Business.App.Operator` | zlecający; propagowany na zadania grupowe |
| `Wykonujacy` | `Soneta.Business.App.Operator` | operator wykonujący |
| `DataOd` / `DataDo` | `Soneta.Types.Date` | planowany okres |
| `CzasOd` / `CzasDo` | `Soneta.Types.TimeSec` | planowane godziny rozpoczęcia/zakończenia |
| `Calodzienne` | `bool` | aktywność całodzienna (domyślnie z definicji) |
| `Kontrahent` | `Soneta.Core.IKontrahent` | referencja interfejsowa (`Kontrahent`, `Pracownik`, …) |
| `StanZadania` | `Soneta.Zadania.StanZadania` | inicjowany pierwszym stanem definicji |
| `PriorytetZadania` | `Soneta.Zadania.PriorytetZadania` | inicjowany pierwszym priorytetem |
| `Koszt` / `Przychod` | `Soneta.Types.Currency` | wartości szacowane |
| `Numer` | `Soneta.Core.NumerDokumentu` (subrow) | `Numer.Pelny: string` — numer wg numeracji definicji |

**Snippet:**

```csharp
using System.Linq;
using Soneta.Business;
using Soneta.Core;
using Soneta.Types;
using Soneta.Zadania;

var zadania = session.GetZadania();

// Definicja pobierana dynamicznie — w Demo istnieje symbol "ZAD";
// fallback: pierwsza niezablokowana definicja rodzaju Zadanie.
var definicja = zadania.DefZadan.WgSymbolu["ZAD"]
                ?? zadania.DefZadan.OfType<DefZadania>()
                    .First(d => d.Rodzaj == RodzajZadania.Zadanie && !d.Blokada);

using (var t = session.Logout(editMode: true))
{
    var zadanie = definicja.Create();          // wiersz właściwego typu, Definicja ustawiona
    session.AddRow(zadanie);                   // OnAdded: stan, priorytet, prowadzący, numer

    zadanie.Nazwa  = "Spotkanie wdrożeniowe";
    zadanie.DataOd = Date.Today;
    zadanie.DataDo = Date.Today + 7;
    zadanie.Opis   = "Omówienie zakresu wdrożenia u klienta";
    zadanie.Kontrahent = session.GetCRM().Kontrahenci.WgKodu["ABC"];   // IKontrahent

    t.Commit();
}
session.Save();
```

**Pułapki:**
- **Twórz przez `definicja.Create()`**, nie `new Zadanie()` z ręcznym typem — rodzaje
  Zlecenie/Wypożyczenie itd. mają własne klasy wierszy; `Create()` dobiera typ wg `Rodzaj`.
  Konstrukcja `new Zadanie { Definicja = def }` działa tylko dla „zwykłych" definicji.
- `Definicja` musi być ustawiona **zanim** wiersz trafi do `AddRow` (tak robi `Create()`) albo
  natychmiast po — bez niej `OnAdded` nie zainicjuje stanu/priorytetu, a weryfikator wymagalności
  wywali zapis.
- `OnAdded` sprawdza prawo proste dodawania zadań — operator bez prawa dostanie
  `ApplicationException` („Operator nie ma prawa dodawania zadań"). Dodatkowo obowiązują prawa
  dostępu do samej definicji (`DefZadania.GetObjectRight()`).
- `Kontrahent` to **referencja interfejsowa** `IKontrahent` — przypisuj istniejący rekord
  (`Kontrahent`, `Pracownik`, urząd…), nie twórz „w locie".
- `DataOd`/`DataDo` to `Soneta.Types.Date`, `CzasOd`/`CzasDo` — `TimeSec`; pomocnicze
  `Start`/`End: System.DateTime` łączą datę z godziną.
- Definicja może wymuszać cechy wymagane (`DefZadania.CechyWymagane`) — brak wartości wybucha
  w `Save()`.

### WORKFLOW-H2 — Zmiana stanu zadania CRM (★)

**Cel:** zmienić stan aktywności (np. zamknąć zadanie), respektując słownik stanów definicji,
dozwolone przejścia i prawa proste.

**Model stanów:** `StanZadania` (rekord konfiguracyjny w `DefZadania.Stany`) ma pola:

| Pole | Typ | Uwagi |
|---|---|---|
| `Nazwa` | `string` | np. „Otwarty", „Zamknięty" (definicja domyślnie dostaje tę parę w `OnAdded`) |
| `Aktywny` | `bool` | czy zadanie w tym stanie jest otwarte |
| `Blokada` | `bool` | stan zablokowany — niewidoczny na listach wyboru |
| `Typ` | `Soneta.Zadania.TaskStateType` | `Open` / `Completed` / `Rejected` |
| `Lp` / `Ident` | `int` | porządek / identyfikator (relacja `Zadanie.StanIdent`) |
| `Kolor` | `string` | kolor stanu (hex) |
| `Stany` | `SubTable<Soneta.Zadania.AvaliableState>` | **dozwolone przejścia** ze stanu (pusta = wszystkie stany dozwolone) |

Setter `Zadanie.StanZadania` robi całą robotę: ustawia `StanIdent`, przelicza `Aktywny`
(z `StanZadania.Aktywny`), przy stanie nieaktywnym wypełnia daty zamknięcia
(`DataZamkniecia`/`CzasZamkniecia`, przy `DefZadania.AktualizujCzas` także `DataZakonczenia`),
propaguje stan na zadania grupowe i urządzenia zleceń/wypożyczeń. Listę stanów **dozwolonych dla
operatora i bieżącego stanu** zwraca `zadanie.GetListStanZadania(): StanZadania[]`
(uwzględnia `AvaliableState` i prawa proste), a blokadę edycji — `zadanie.IsReadOnlyStanZadania()`.
Czynność seryjna na liście to worker `ZadaniaZmienStanWorker` (menu „Zmień stan").

**Snippet:**

```csharp
using System.Linq;
using Soneta.Business;
using Soneta.Zadania;

var zadanie = session.GetZadania().Zadania.WgNazwy["Spotkanie wdrożeniowe"].FirstOrDefault();

// Stany dozwolone z bieżącego stanu dla zalogowanego operatora:
StanZadania[] dozwolone = zadanie.GetListStanZadania();

// Stan zamykający (nieaktywny) ze słownika definicji:
var zamkniety = zadanie.Definicja.Stany.First(s => !s.Aktywny && !s.Blokada);

using (var t = session.Logout(editMode: true))
{
    zadanie.StanZadania = zamkniety;   // ustawia StanIdent, Aktywny=false, daty zamknięcia
    t.Commit();
}
session.Save();

bool czyOtwarte = zadanie.Aktywny;                       // false
var typStanu = zadanie.StanZadania.Typ;                  // TaskStateType.Completed
```

**Pułapki:**
- Przypisywany stan **musi należeć do definicji tego zadania** (`stan.Definicja == zadanie.Definicja`)
  — stany różnych definicji to różne rekordy, nawet o tej samej nazwie.
- Prawa proste: zamknięcie (zmiana stanu) wymaga prawa „zamykania zadań"; zmiana stanu zadania
  **nieaktywnego** — osobnego prawa (przy `Granted` brak ograniczeń, przy `ReadOnly` tylko stany
  aktywne). `GetListStanZadania()` filtruje wg tych praw — używaj jej zamiast zgadywać.
- Gdy do definicji podpięto proces workflow (`DefZadania.WFDefinition != null`) i zadanie ma
  podpięty proces, pole `StanZadania` jest **tylko-do-odczytu** — stanem steruje proces
  (`IsReadOnlyStanZadania()`); także rekordy `StanZadania` takiej definicji są wtedy readonly.
- `AvaliableState` (kolekcja `StanZadania.Stany`) ogranicza przejścia: pusta kolekcja = wolny
  wybór; niepusta = tylko wskazane stany docelowe (`AvaliableState.Child`).
- Stan i propagacja na zadania grupowe to logika settera — **nie** ustawiaj `StanIdent` ręcznie.

### WORKFLOW-H3 — Tworzenie zadań cyklicznych / kopiowanie zadań (★)

**Cel:** powielić aktywność — pojedyncza kopia (np. „to samo zadanie za tydzień") albo seria
cykliczna (powtarzanie co N dni/tygodni/miesięcy).

**Warianty:**

| Wariant | Jak |
|---|---|
| Kopia programowa (publiczny kontrakt) | `(Zadanie)zrodlo.Clone()` + `Zadania.AddRow(kopia)` + korekta dat |
| Seria cykliczna z UI | czynności „Kopiuj zadania"/„Kopiuj zdarzenia" (Shift+F6) — workery `AktywnosciKopiujWieleWorker` (lista, max 10 zaznaczonych) i `AktywnosciKopiujPojedynczoWorker` (formularz) |
| Parametry cyklu | `Soneta.Zadania.ZadanieUtworzZadaniaCykliczneWorkerParams`: `CoIle: int`, `RodzajOdstepuCzasu: CyklZadania` (dzień/tydzień/miesiąc/rok), `DataDoKiedy: Date`, `WykluczoneDaty`, `UnikanieDniSwiatecznych`, `Unikanie…` (dni tygodnia), `KopiowanieCech: bool` |

**Mechanizm workera (do odtworzenia w kodzie):** dla każdej wyliczonej daty robi
`Clone()` źródła, `AddRow`, przepisuje `Start`/`End` (data + `CzasOd`/`CzasDo` źródła),
`Prowadzacy`, wykonawcę (`Wykonujacy` albo `Role` + `ZadanieDlaRoli`), kopiuje dokumenty CRM
oznaczone `DokumentCRM.KopiowanieDokCRM == KopiowanieDokCRM.Kopiowanie` oraz (opcjonalnie) cechy
(`Features`). Daty kolidujące z innymi zadaniami lub dniami wykluczonymi są raportowane do `Log`.

**Snippet (kopia programowa „za tydzień"):**

```csharp
using System.Linq;
using Soneta.Business;
using Soneta.Types;
using Soneta.Zadania;

var zadania = session.GetZadania();
var zrodlo = zadania.Zadania.WgNazwy["Spotkanie wdrożeniowe"].FirstOrDefault();

using (var t = session.Logout(editMode: true))
{
    var kopia = (Zadanie)zrodlo.Clone();       // klon wiersza (bez kolekcji powiązanych)
    zadania.Zadania.AddRow(kopia);

    kopia.DataOd = zrodlo.DataOd + 7;          // przesunięcie cyklu o tydzień
    kopia.DataDo = zrodlo.DataDo + 7;
    kopia.Prowadzacy = zrodlo.Prowadzacy;
    kopia.Wykonujacy = zrodlo.Wykonujacy;

    // Dokumenty CRM oznaczone do kopiowania — przepisanie ręczne (Clone ich nie kopiuje):
    foreach (var dokZrodla in zrodlo.DokumentyCRM
                 .Where(d => d.KopiowanieDokCRM == KopiowanieDokCRM.Kopiowanie))
    {
        var dok = new DokumentCRM();
        zadania.DokumentyCRM.AddRow(dok);
        dok.Host = kopia;
        dok.Dokument = dokZrodla.Dokument;
        dok.RodzajDokCRM = dokZrodla.RodzajDokCRM;
    }

    t.Commit();
}
session.Save();
```

**Pułapki:**
- `Clone()` kopiuje **tylko pola wiersza** — dokumenty CRM, zasoby, podzadania, cechy trzeba
  przepisać ręcznie (wzorzec jak w snippecie / `Features[def] = wartość`).
- Algorytm generowania serii dat (z wykluczeniami i kontrolą konfliktów) siedzi w wewnętrznym
  helperze workera — publicznie dostępne są tylko czynności menu i klasa parametrów; programowo
  serię budujesz własną pętlą po datach (wzorzec wyżej).
- Zadanie sklonowane jest **nowym** wierszem — dostaje własny numer z numeracji definicji.
- Workery kopiowania ograniczają zaznaczenie do **10 aktywności** i rozdzielają czynności dla
  zadań (`Rodzaj` ∈ Zadanie/Grupujące/Projektowe) i zdarzeń (`Zdarzenie`).
- Powiązanie „przy zamknięciu utwórz następne zadanie" to inna ścieżka: pole konfiguracyjne
  `DefZadania.Powiazane: DefZadania` (generowanie automatyczne przy zamknięciu).

### WORKFLOW-H4 — Budowanie hierarchii podzadań (★)

**Cel:** powiązać zadania w hierarchię — zadanie główne z podzadaniami (grupowe) albo łańcuch
zadań poprzedzających (wątek).

**Pola i typy:**

| Element | Typ | Uwagi |
|---|---|---|
| `Zadanie.Nadrzedne` | `Soneta.Zadania.Zadanie` | zadanie główne lub poprzedzające |
| `Zadanie.TypNadrzednego` | `Soneta.Core.TypZadaniaNadrzednego` | `Główne = 0`, `Wątek = 1`, `Grupowe = 2` |
| `Zadanie.ZadaniaPodrzedne` | `Soneta.Business.View` | podzadania (odczyt) |
| `Zadanie.ZadaniaPowiazane` | `Soneta.Business.View` | zadania powiązane (odczyt) |
| `Zadanie.ZadaniaGrupowe` | `Soneta.Business.View` | człony zadania grupowego (odczyt) |
| `Zadania.WgNadrzedne` | klucz (`Key<Zadanie>`) | serwerowy dostęp do podzadań: `WgNadrzedne[zadanie]` |
| `Zadanie.Poziom` | `int` | głębokość w hierarchii (odczyt) |

**Mechanizm:** hierarchię buduje się przypisaniem `Nadrzedne`; `TypNadrzednego` decyduje
o zachowaniu — dla `Grupowe` stan, priorytet i prowadzący są **propagowane z nadrzędnego**
(settery `StanZadania`/`PriorytetZadania`/`Prowadzacy` iterują po `ZadaniaGrupowe`), a aktywność
członu zależy od aktywności rodzica. Czynność UI „Dodaj istniejące zadanie powiązane"
(worker `ZadanieDolaczZadaniePodrzedneWorker`, Shift+F10) robi dokładnie
`zadaniePodrzedne.Nadrzedne = zadanieNadrzedne` w transakcji.

**Snippet:**

```csharp
using System.Linq;
using Soneta.Business;
using Soneta.Zadania;

var zadania = session.GetZadania();
var nadrzedne = zadania.Zadania.WgNazwy["Spotkanie wdrożeniowe"].FirstOrDefault();
var definicja = nadrzedne.Definicja;

using (var t = session.Logout(editMode: true))
{
    var podzadanie = definicja.Create();
    session.AddRow(podzadanie);
    podzadanie.Nazwa = "Przygotowanie agendy";
    podzadanie.Nadrzedne = nadrzedne;          // powiązanie w hierarchię
    t.Commit();
}
session.Save();

// Odczyt podzadań — serwerowo, po kluczu:
foreach (Zadanie z in zadania.Zadania.WgNadrzedne[nadrzedne])
{
    // z.Nazwa, z.TypNadrzednego, z.StanZadania
}
```

**Pułapki:**
- **Nie buduj cykli** — zadanie nie może być nadrzędne (pośrednio) samo dla siebie; czynność UI
  filtruje listę wyboru (wyklucza samo zadanie i już powiązane).
- `TypNadrzednego == Grupowe` oznacza pełną propagację z rodzica — stan członu grupy nadpisze
  zmiana stanu rodzica; nie zmieniaj stanu członów w oderwaniu od rodzica.
- `ZadaniaPodrzedne`/`ZadaniaPowiazane`/`ZadaniaGrupowe` to widoki (`View`) pod UI; w kodzie
  biznesowym preferuj klucz `Zadania.WgNadrzedne[zadanie]` (filtr serwerowy, patrz
  `rowcondition.md`).
- W konfiguracji budżetowania istnieją relacje definicji (`DefZadania.Nadrzedne/Podrzedne:
  SubTable<DefZadaniaRelacja>`) — to hierarchia **definicji**, nie myl jej z hierarchią zadań.

### WORKFLOW-H5 — Powiązanie zadania z kontrahentem / projektem / kampanią (★)

**Cel:** przypiąć aktywność do kontrahenta, projektu lub kampanii i odczytywać aktywności
z perspektywy tych obiektów.

**Pola i typy:**

| Pole na `Zadanie` | Typ | Uwagi |
|---|---|---|
| `Kontrahent` | `Soneta.Core.IKontrahent` | podmiot, którego dotyczy zadanie |
| `Przedstawiciel` | `Soneta.CRM.KontaktOsoba` | osoba po stronie kontrahenta |
| `Projekt` | `Soneta.Zadania.Projekt` | projekt CRM |
| `EtapProjektu` | `Soneta.Zadania.EtapProjektu` | etap (inicjowany z projektu) |
| `Kampania` | `Soneta.Zadania.Kampania` | kampania CRM |
| `Lead` / `Transakcja` | `Soneta.CRM.Lead` / `Soneta.CRM.Transakcja` | powiązania sprzedażowe |
| `ZadaniaPodmiotu` | `SubTable<Soneta.Zadania.Podmioty_Zadania.PodmiotZadanie>` | **dodatkowe** podmioty zadania (rekord wiążący `PodmiotZadanie` z polami `Zadanie`, `Kontrahent`) |

**Odczyt „od strony" obiektu** — klucze tabeli `Zadania` (filtr serwerowy):
`Zadania.WgKontrahent[IKontrahent]` (warianty z `Date`/`TimeSec`), `Zadania.WgProjekt[projekt]`,
`Zadania.WgKampania[kampania]`, `Zadania.WgLead[...]`, `Zadania.WgTransakcja[...]`.
Nie ma property `Kontrahent.Zadania` — kierunek odwrotny zawsze przez klucz.

**Skutki uboczne settera `Projekt`:** ustawia `OddzialFirmy` i `EtapProjektu` z projektu;
gdy zadanie nie ma kontrahenta — dziedziczy `Kontrahent`/`Przedstawiciel` z projektu; przy
włączonej kontroli dat definicji projektu dociąga `DataOd`/`DataDo` do okresu projektu; propaguje
projekt na zadania grupowe. Czynności UI: „Dodaj aktywność" na kontrahencie
(`KontrahentDodajAktywnoscWorker` — otwiera formularz nowego zadania per definicja) oraz
„Dodaj istniejące Zadanie" na projekcie (`ProjektDolaczZadanieWorker` — przypisuje `Projekt`
zaznaczonym zadaniom).

**Snippet:**

```csharp
using System.Linq;
using Soneta.Business;
using Soneta.Zadania;

var zadania = session.GetZadania();
var zadanie = zadania.Zadania.WgNazwy["Spotkanie wdrożeniowe"].FirstOrDefault();
var kontrahent = session.GetCRM().Kontrahenci.WgKodu["ABC"];

using (var t = session.Logout(editMode: true))
{
    zadanie.Kontrahent = kontrahent;                                   // IKontrahent

    var projekt = zadania.Projekty.WgKontrahent[kontrahent].FirstOrDefault();
    if (projekt != null)
        zadanie.Projekt = projekt;     // side-effects: oddział, etap, ew. kontrahent, daty

    t.Commit();
}
session.Save();

// Aktywności kontrahenta — serwerowo, po kluczu (nie ma property Kontrahent.Zadania):
foreach (Zadanie z in zadania.Zadania.WgKontrahent[kontrahent])
{
    // z.Nazwa, z.DataOd, z.StanZadania
}
```

**Pułapki:**
- `Kontrahent` to referencja **interfejsowa** (`IKontrahent`) — może wskazywać `Kontrahent`,
  `Pracownik`, urząd itd.; przypisuj istniejące rekordy.
- Setter `Projekt` ma skutki uboczne (etap, oddział, korekta dat przy kontroli dat projektu) —
  ustawiaj projekt **po** ustawieniu dat, jeśli daty mają być zachowane, i sprawdź je po
  przypisaniu.
- Wielu kontrahentów na jednym zadaniu = rekordy `PodmiotZadanie` (`new PodmiotZadanie()` +
  `AddRow`, pola `Zadanie` i `Kontrahent`) — pole `Zadanie.Kontrahent` pozostaje podmiotem
  głównym.
- `Przedstawiciel` musi być osobą kontaktową powiązaną z ustawionym kontrahentem — weryfikatory
  pilnują spójności pary.

### WORKFLOW-H6 — Rejestracja czasu pracy nad zadaniem (★)

**Cel:** rejestrować czas realizacji aktywności — prostym polem czasu wykonania albo wpisami
`TimeTrack` (host `ITimeTrackHost`), z opcją stopera w UI.

**Warianty:**

| Wariant | Jak |
|---|---|
| Proste pole na zadaniu | `Zadanie.CzasWykonania: Soneta.Types.TimeSec` |
| Wpisy czasu (rozliczalne) | rekordy `Soneta.Core.TimeTrack` (tabela `TimeTracks`) podpięte do zadania (`Host`) |
| Stoper (UI) | czynności „Stoper START/STOP" (worker `ZadaniaStoperWorker`); programowo: `zadanie.StoperExists()`, `zadanie.GetStoper()` (`Soneta.Core.StoperHostExtensions`), `Stoper.Start(session)` / `Stop(session)` |

**Pola i typy (`Soneta.Core.TimeTrack`, `GuidedRow` root):**

| Pole | Typ | Uwagi |
|---|---|---|
| `Host` | `Soneta.Core.ITimeTrackHost` | host wpisu (m.in. `Zadanie`); ustawiany też `SetHost(host)` |
| `HostType` / `HostGuid` | `string` / `System.Guid` | luźna referencja (nazwa tabeli + Guid) — nośnik `Host` |
| `Wykonujacy` | `Soneta.Business.IWykonujacy` | wykonujący (operator/pracownik); wymagane |
| `Data` | `Soneta.Types.Date` | dzień pracy |
| `CzasRozpoczecia` | `Soneta.Types.Time` | godzina startu (walidacja < 24 h) |
| `CzasWykonania` | `Soneta.Types.Time` | czas trwania (obcinany do 23:59) |
| `Uwagi` | `string` | opis |
| `PracaTworcza` | `bool` | znacznik pracy twórczej (inicjowany z `Host.Data.HasCreativeHours`) |
| `Rozliczone` | `bool` | wpis rozliczony ⇒ **readonly** |

Po stronie zadania: `Zadanie.TimeTrackList: SubTable<Soneta.Core.TimeTrack>` (kolekcja wpisów
hosta), konfiguracja stopera na definicji: `DefZadania.StoperDostepny`, `StoperAuto`, `StoperWTle`,
`PowiazCzasWykonania` (wiąże `CzasWykonania` zadania z rozpoczęciem/zakończeniem).

**Snippet:**

```csharp
using System.Linq;
using Soneta.Business;
using Soneta.Core;
using Soneta.Types;
using Soneta.Zadania;

var zadanie = session.GetZadania().Zadania.WgNazwy["Spotkanie wdrożeniowe"].FirstOrDefault();

using (var t = session.Logout(editMode: true))
{
    // Proste pole czasu wykonania na zadaniu:
    zadanie.CzasWykonania = new TimeSec(1, 30, 0);

    // Wpis czasu realizacji (TimeTrack) podpięty do zadania:
    var wpis = session.AddRow(new TimeTrack());
    wpis.SetHost(zadanie);                                  // HostType + HostGuid (luźna referencja)
    wpis.Wykonujacy = session.Get(session.Login.Operator);  // IWykonujacy — operator w tej sesji
    wpis.Data = Date.Today;
    wpis.CzasRozpoczecia = new Time(9, 0);
    wpis.CzasWykonania   = new Time(1, 30);
    wpis.Uwagi = "Analiza wymagań";
    t.Commit();
}
session.Save();

// Suma zarejestrowanego czasu z wpisów zadania (TotalMinutes: int):
int lacznieMinut = zadanie.TimeTrackList.Sum(tt => tt.CzasWykonania.TotalMinutes);
```

**Pułapki:**
- `Host` na `TimeTrack` to **luźna referencja** (`HostType` + `HostGuid`) — ustawiaj przez
  `SetHost(...)`/property `Host`, nie ręcznie po polach; usunięcie hosta nie kasuje wpisów.
- Wpis jest **readonly**, gdy `Rozliczone == true` albo gdy trwa na nim stoper — edycja rzuci
  wyjątek; rozliczanie wykonuje się czynnościami projektów (np. rozliczenie `TimeTracków`
  projektu).
- `CzasWykonania` wpisu jest obcinany do `23:59`, a `CzasRozpoczecia` waliduje format godziny —
  to `Soneta.Types.Time`, nie `TimeSpan`.
- Stoper to mechanizm **interaktywny** w zakresie loginu (serwis stopera jest wewnętrzny;
  publicznie dostępne są rozszerzenia `GetStoper()`/`StoperExists()` i klasa `Stoper`
  z `Start(Session)`/`Stop(Session)`) — w kodzie usługowym/serwerowym rejestruj czas wprost
  rekordami `TimeTrack`. Dostęp do stopera globalnego kontroluje prawo proste na `TimeTrack`.
- Dostępność stopera na formularzu zależy od definicji (`StoperDostepny`); przy
  `PowiazCzasWykonania` pole `CzasWykonania` zadania jest sprzęgnięte z godzinami rozpoczęcia
  i zakończenia.

### WORKFLOW-H7 — Powiązanie dokumentów z zadaniem CRM (★)

**Cel:** podpiąć dokument (handlowy, ewidencyjny, kasowy…) do aktywności i odczytywać dokumenty
powiązane zadania.

**Model:** rekord wiążący `Soneta.Zadania.DokumentCRM` (tabela `DokumentyCRM`) łączy **host**
(`IDocumentHostCRM` — implementują m.in. `Zadanie`, `Projekt`, `Kampania`) z **dokumentem**
(`Soneta.Core.IDokumentCRM` — implementują m.in. `Soneta.Handel.DokumentHandlowy`, dokumenty
ewidencji i zapłaty). Kolekcja po stronie zadania: `Zadanie.DokumentyCRM:
SubTable<Soneta.Zadania.DokumentCRM>`; tabela modułu: `ZadaniaModule.DokumentyCRM`.

**Pola i typy (`Soneta.Zadania.DokumentCRM`):**

| Pole | Typ | Uwagi |
|---|---|---|
| `Host` | `Soneta.Zadania.IDocumentHostCRM` | zadanie/projekt/kampania — właściciel powiązania |
| `Dokument` | `Soneta.Core.IDokumentCRM` | dokument źródłowy (ref. interfejsowa) |
| `RodzajDokCRM` | `Soneta.Zadania.RodzajDokCRM` | `Brak` / `DokHandlowy` / `Część` / `Usługa` |
| `PozycjaDokHandl` | `Soneta.Handel.PozycjaDokHandlowego` | opcjonalna pozycja (musi należeć do `Dokument`) |
| `SumowanieWartosci` | `Soneta.Zadania.SumowanieWartosciCRM` | Przychód/Koszt/Brak — inicjowane z kierunku płatności dokumentu |
| `Wartosc` | `Soneta.Types.Currency` (odczyt) | wartość dokumentu netto/brutto wg `DefZadania.WartoscDokCRM` |
| `KopiowanieDokCRM` | `Soneta.Zadania.KopiowanieDokCRM` | czy powiązanie kopiować przy kopiowaniu zadania (H3) |

**Snippet:**

```csharp
using System.Linq;
using Soneta.Business;
using Soneta.Zadania;

var zadania = session.GetZadania();
var zadanie = zadania.Zadania.WgNazwy["Spotkanie wdrożeniowe"].FirstOrDefault();

// Istniejący dokument handlowy kontrahenta zadania (filtr serwerowy po kluczu,
// patrz HANDEL05 — nie ma „gołego" klucza WgKontrahenta):
var dokument = session.GetHandel().DokHandlowe
    .WgKontrahentaObcy[d => d.Kontrahent == zadanie.Kontrahent]
    .FirstOrDefault();

using (var t = session.Logout(editMode: true))
{
    var powiazanie = session.AddRow(new DokumentCRM());
    powiazanie.Host = zadanie;                          // IDocumentHostCRM
    powiazanie.Dokument = dokument;                     // IDokumentCRM (np. DokumentHandlowy)
    powiazanie.RodzajDokCRM = RodzajDokCRM.DokHandlowy;
    t.Commit();
}
session.Save();

// Odczyt dokumentów powiązanych zadania:
foreach (DokumentCRM dok in zadanie.DokumentyCRM)
{
    // dok.Dokument, dok.RodzajDokCRM, dok.Wartosc (netto/brutto wg definicji zadania)
}
```

**Pułapki:**
- **Host musi być aktywny** — dodanie dokumentu do zadania w stanie nieaktywnym rzuca
  `InvalidOperationException`, chyba że operator ma prawo proste dodawania do zadań nieaktywnych
  (`SimpleRightAddToInactiveTasks`).
- `PozycjaDokHandl` jest walidowana względem `Dokument` — pozycja innego dokumentu rzuca
  `ArgumentException`; zmiana `Dokument` zeruje pozycję.
- Property `DokumentCRM.Zadanie` jest **przestarzałe** — używaj `Host` (działa też dla projektów
  i kampanii).
- Definicja zadania kontroluje wiązanie tego samego dokumentu z wieloma aktywnościami
  (`DefZadania.KontrolaDokCRM: Soneta.Core.TypKontroli` — domyślnie `Ostrzegaj`) oraz sposób
  liczenia `Wartosc` (`DefZadania.WartoscDokCRM`: netto/brutto). Dostęp do poszczególnych
  rodzajów dokumentów na liście ograniczają prawa proste na `DokumentCRM`.
- Podgląd dokumentu z poziomu zadania to czynność UI workera `ZadaniePokazDokumentCRMWorker` —
  w kodzie biznesowym nawiguj wprost po `powiazanie.Dokument`.
