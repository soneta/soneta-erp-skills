# KADRY04 — Nieobecności i czas pracy

> Wspólne fakty o typie, podstawowe typy i szablon wzorca: [../kadry.md](../kadry.md).

### KADRY-D1 — Wprowadzanie nieobecności (★)

**Cel:** zarejestrować nieobecność pracownika (urlop wypoczynkowy, zwolnienie chorobowe, urlop
bezpłatny, opieka itp.) za wskazany okres oraz odczytać nieobecności obowiązujące w danym przedziale dat.

**Fakty o typie (zweryfikowane skanem DLL):**

- **`Soneta.Kalend.Nieobecnosc` jest klasą abstrakcyjną** — tabela `Nieobecnosci` (`GuidedRow` root,
  child `Pracownik`-a). `new Nieobecnosc(...)` się nie skompiluje.
- Konkretny typ do tworzenia: **`Soneta.Kalend.NieobecnośćPracownika`** (dziedziczy z `Nieobecnosc`),
  z **publicznym konstruktorem `new NieobecnośćPracownika(Pracownik pracownik)`** — ctor od razu wiąże
  nieobecność z pracownikiem. (Drugi konkretny typ to `KorektaNieobecności` — patrz KADRY-D2.)
- Kolekcja na pracowniku: **`pracownik.Nieobecnosci: FromToSubTable<Soneta.Kalend.Nieobecnosc>`**
  (uporządkowana po okresie „od–do").
- Tabela z poziomu modułu: `session.GetKalend().Nieobecnosci`.

**Pola i typy (`Nieobecnosc` / `NieobecnośćPracownika`):**

| Pole | Typ | Rodzaj | Opis |
|---|---|---|---|
| `Definicja` | `Soneta.Kalend.DefinicjaNieobecnosci` | bazodanowe, **zapisywalne** | rodzaj nieobecności (słownik konfiguracyjny); decyduje o typie (urlop / zasiłek / bezpłatny) |
| `Okres` | `Soneta.Types.FromTo` | bazodanowe, **zapisywalne** | zakres dat nieobecności „od–do" |
| `OdGodziny`, `DoGodziny` | `Soneta.Types.Time` | — | godziny (nieobecności godzinowe) |
| `Norma`, `NormaNie` | `Soneta.Types.Time` | bazodanowe | normy czasowe |
| `IlośćDni` / `Dni` | `int` | kalkulowane/zapisywalne | liczba dni nieobecności |
| `Pracownik` | `Soneta.Kadry.Pracownik` | **tylko do odczytu** | właściciel (ustawiany ctorem, nie da się zmienić setterem) |
| `Zwolnienie` | `Soneta.Kalend.ZwolnienieZUS` (subrow) | bazodanowe | dane ZUS dla zwolnień chorobowych (`KodChoroby`, `Numer` ZLA, `PonownieUstalPodstawe`…) |
| `Urlop`, `Macierzynski`, `Wychowawczy`, `Okolicznosciowy` | subrowy | bazodanowe | szczegóły poszczególnych typów urlopów |
| `Korygowana` | `bool` | **tylko do odczytu** | czy nieobecność jest korektą (patrz KADRY-D2) |

**Dostęp do definicji nieobecności (`DefNieobecnosci`):**

- `session.GetKalend().DefNieobecnosci.WgNazwy[string]` — pobranie po nazwie, np.
  `WgNazwy["Urlop wypoczynkowy"]`, `WgNazwy["Zwolnienie chorobowe"]`,
  `WgNazwy["Urlop bezpłatny (art 174 kp)"]`. Nazwy muszą **dokładnie** odpowiadać słownikowi danej bazy
  (w Demo nie ma wpisu „Urlop bezpłatny" — jest „Urlop bezpłatny (art 174 kp)"); `WgNazwy[...]` dla
  nieistniejącej nazwy zwraca `null`.
- `session.GetKalend().DefNieobecnosci[string]` (indeksator domyślny po nazwie) — równoważne.
- `DefinicjaNieobecnosci` ma pola `Nazwa: string`, `Kod: string`, `Typ: TypNieobecnosci`.

**Wyszukiwanie po dacie/okresie:** `pracownik.Nieobecnosci.GetIntersectedRows(FromTo)` zwraca
`IList` nieobecności przecinających podany przedział.

**Snippet:**

```csharp
var kalend    = session.GetKalend();
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
// Nieobecność BEZ limitu (np. urlop bezpłatny) można wprowadzić wprost. Dla nieobecności
// LIMITOWANYCH (urlop wypoczynkowy) najpierw musi istnieć naliczony limit — patrz pułapki i KADRY-D7.
var defNieob  = kalend.DefNieobecnosci.WgNazwy["Urlop bezpłatny (art 174 kp)"];

using (var t = session.Logout(editMode: true))
{
    // typ konkretny; ctor wiąże nieobecność z pracownikiem
    var nieobecnosc = session.AddRow(new NieobecnośćPracownika(pracownik));
    nieobecnosc.Definicja = defNieob;                                  // rodzaj nieobecności
    nieobecnosc.Okres     = new FromTo(new Date(2026, 7, 1), new Date(2026, 7, 5));
    t.Commit();
}
session.Save();

// Odczyt nieobecności przecinających lipiec 2026:
var lipiec = new FromTo(new Date(2026, 7, 1), new Date(2026, 7, 31));
foreach (Nieobecnosc n in pracownik.Nieobecnosci.GetIntersectedRows(lipiec))
{
    // n.Definicja.Nazwa, n.Okres, n.Dni
}
```

**Pułapki:**
- **Nie** rób `new Nieobecnosc(...)` — typ jest abstrakcyjny. Używaj `new NieobecnośćPracownika(pracownik)`.
- **Nieobecności limitowane wymagają istniejącego limitu.** Ustawienie `Okres` dla nieobecności
  powiązanej z limitem (np. „Urlop wypoczynkowy") synchronicznie przelicza limit i rzuca
  `Soneta.Kalend.DefinicjaLimitu.LimitNotFoundException`, gdy pracownik nie ma naliczonego limitu na
  dany rok. Dlatego: albo najpierw nalicz limit (patrz KADRY-D7), albo użyj nieobecności bez limitu
  (np. „Urlop bezpłatny (art 174 kp)") — jak w snippetcie powyżej.
- `Definicja` jest **wymagana** — bez niej nieobecność nie zostanie poprawnie naliczona/zapisana.
  Pobieraj istniejący wpis słownika przez `DefNieobecnosci.WgNazwy[...]`, nie twórz „w locie".
- `Pracownik` jest **tylko do odczytu** — relację ustawia konstruktor, nie da się jej później zmienić.
- Tabela `Nieobecnosci` jest **operacyjna guided** — przy przeglądaniu poprzecznym (po wszystkich
  pracownikach) filtruj zakresem czasowym (safe-code §6.3). W zakresie jednego pracownika korzystaj
  z `pracownik.Nieobecnosci` i `GetIntersectedRows`.
- Nakładające się nieobecności i niepoprawne okresy wychwytują weryfikatory przy `Save()`
  (`RowException`) — obsłuż wyjątek.
- Pełna transakcja w `session.Logout(editMode: true)`; brak `Commit()` = rollback przy `Dispose()`.

---

### KADRY-D2 — Korygowanie nieobecności już wypłaconych (★)

**Cel:** poprawić nieobecność, która została już rozliczona w wypłacie — zmienić jej okres lub typ
(definicję) i/lub wymusić ponowne ustalenie podstawy naliczania zasiłku. enova rozróżnia dwie ścieżki:
(a) **modyfikacja istniejącej nieobecności** + ponowne ustalenie podstawy, (b) **korekta** jako odrębny
rekord typu `KorektaNieobecności`.

**Fakty o typie (zweryfikowane skanem DLL):**

- Pola `Definicja: DefinicjaNieobecnosci` i `Okres: FromTo` na `Nieobecnosc` są **zapisywalne**
  publicznie — można je zmienić na istniejącym rekordzie.
- `Nieobecnosc.Korygowana: bool` i `Nieobecnosc.Pracownik` są **tylko do odczytu**.
- Subrow `Zwolnienie: Soneta.Kalend.ZwolnienieZUS` posiada flagę `PonownieUstalPodstawe: bool`
  oraz **publiczną metodę `SetPonownieUstalPodstawe(bool)`** — to ona steruje przeliczeniem podstawy
  zasiłku przy kolejnym naliczeniu wypłaty.
- Worker (czynność menu, `DataType = Nieobecnosc`): klasa
  **`Soneta.Kalend.Nieobecnosc.UstalPonowniePodstawęNaliczaniaWorker`** — czynność
  „Ustal ponownie podstawę naliczania". Worker:
  - ma publiczny bezparametrowy ctor;
  - przyjmuje kontekst przez settowalną property `[Context] public Params Nieobecność`;
  - klasa `…Worker.Params : ContextBase` ma **publiczny ctor `Params(Context context)`**, który czyta
    nieobecność z `context[typeof(Nieobecnosc)]`, oraz settowalną property `UstalPodstawę: bool`;
  - metoda `public void PonownieUstalPodstawę()` jest jego akcją;
  - `static bool IsEnabledPonownieUstalPodstawę(Nieobecnosc)` mówi, kiedy czynność jest aktywna
    (dotyczy zwolnień ZUS i urlopów macierzyńskich: `Zwolnienie.IsZUS || Macierzynski.IsMacierzyński`,
    przy braku `BlokadaOkresu`).
- Drugi konkretny typ nieobecności: **`Soneta.Kalend.KorektaNieobecności`** (dziedziczy `Nieobecnosc`),
  z **publicznym ctor `new KorektaNieobecności(NieobecnośćPracownika nieobecność)`** — tworzy rekord
  korygujący wskazaną nieobecność. Ma zapisywalne `Definicja`, `Okres`, `IlośćDni`,
  `RozliczenieWDniu`, `RozliczenieData`, a kolekcje `ElementyKorygowane`/`ElementyKorygowaneStorno`
  są tylko do odczytu (wyliczane).

**Wariant A — zmiana okresu/typu + ponowne ustalenie podstawy (modyfikacja istniejącego rekordu):**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
var okresStary = new FromTo(new Date(2026, 3, 2), new Date(2026, 3, 10));

// odszukaj istniejącą (już rozliczoną) nieobecność po przecięciu z okresem
var nieobecnosc = (Nieobecnosc)pracownik.Nieobecnosci.GetIntersectedRows(okresStary)[0];

using (var t = session.Logout(editMode: true))
{
    nieobecnosc.Okres = new FromTo(new Date(2026, 3, 2), new Date(2026, 3, 12));  // wydłużenie okresu
    // dla zwolnień ZUS — wymuś ponowne ustalenie podstawy przy najbliższym naliczeniu wypłaty:
    nieobecnosc.Zwolnienie.SetPonownieUstalPodstawe(true);
    t.Commit();
}
session.Save();
```

**Wariant B — czynność „Ustal ponownie podstawę naliczania" przez worker (kontekst):**

```csharp
var worker = new Nieobecnosc.UstalPonowniePodstawęNaliczaniaWorker();
var ctx    = Context.Empty.Clone(session);
ctx[typeof(Nieobecnosc)] = nieobecnosc;                     // worker czyta nieobecność z kontekstu
worker.Nieobecność = new Nieobecnosc.UstalPonowniePodstawęNaliczaniaWorker.Params(ctx)
{
    UstalPodstawę = true
};
worker.PonownieUstalPodstawę();                             // wykonuje własną transakcję + Commit
session.Save();
```

**Wariant C — odrębny rekord korekty (`KorektaNieobecności`):**

```csharp
using (var t = session.Logout(editMode: true))
{
    var nPrac = (NieobecnośćPracownika)nieobecnosc;          // korekta dotyczy NieobecnośćPracownika
    var korekta = session.AddRow(new KorektaNieobecności(nPrac));
    korekta.Definicja = nPrac.Definicja;
    // Okres korekty MUSI być podzbiorem okresu korygowanej nieobecności (tu: 2..10):
    korekta.Okres     = new FromTo(new Date(2026, 3, 3), new Date(2026, 3, 8));
    t.Commit();
}
session.Save();
// Po zapisie korygowana nieobecność ma flagę Korygowana == true.
```

**Pułapki:**
- **Faktyczne** przeliczenie wartości zasiłku NIE następuje w momencie ustawienia flagi/wywołania
  workera — flaga `PonownieUstalPodstawe` jest odczytywana dopiero przy **ponownym naliczeniu wypłaty**
  (mechanizm `PodstawaZasilku`). Sam test korekty rekordu nieobecności (Demo, rollback) zweryfikuje
  zmianę `Okres`/`Definicja`/flagi, ale **nie zweryfikuje przeliczonych kwot wypłaty** bez pełnego
  scenariusza naliczenia listy płac (patrz sekcja „funkcjonalności niewykonalne").
- `IsEnabledPonownieUstalPodstawę` ogranicza czynność do zwolnień ZUS / macierzyńskich — dla zwykłego
  urlopu wypoczynkowego worker nie ma zastosowania; tam korektę robisz przez zmianę `Okres`/`Definicja`
  albo rekord `KorektaNieobecności`.
- **Okres korekty (`KorektaNieobecności.Okres`) musi być podzbiorem okresu korygowanej nieobecności** —
  wyjście poza ten zakres rzuca `Nieobecnosc.KorygowanyOkresException`.
- Dla nieobecności bez skutków płacowych (np. urlop bezpłatny) `KorektaNieobecności` **nie pojawia się
  jako osobny wiersz** w `pracownik.Nieobecnosci` — obserwowalnym efektem jest flaga `Korygowana == true`
  na nieobecności pierwotnej.
- Korekta zmienia dane operacyjne powiązane z wypłatą — trzymaj transakcję krótką i obsłuż
  `RowConflictException` / `RowException` z `Save()` (safe-code §4, §13.1).
- Worker wykonuje własną transakcję (`Session.Logout(true)` + `Commit`) — nie zagnieżdżaj go w innej
  otwartej transakcji edycyjnej.

---

### KADRY-D7 — Analiza limitów urlopowych (★)

**Cel:** odczytać limit nieobecności (np. urlop wypoczynkowy) pracownika za dany rok — ile przysługuje,
ile wykorzystano, ile pozostało. Limity **nie są tworzone ręcznie** — powstają przez naliczanie.

**Fakty o typie (zweryfikowane skanem DLL):**

- **`Soneta.Kalend.LimitNieobecnosci`** — tabela `LimNieobecnosci`, `GuidedRow` **child** pracownika
  (relacja przez pole `Pracownik`). Instancje powstają wyłącznie przez naliczanie — **nie twórz ich
  konstruktorem**.
- Kolekcja na pracowniku: **`pracownik.Limity: SubTable<Soneta.Kalend.LimitNieobecnosci>`**
  (nazwa kolekcji to `Limity`, nie „LimityNieobecnosci").
- Tabela z poziomu modułu: `session.GetKalend().LimNieobecnosci`.

**Pola i typy (`LimitNieobecnosci`) — odczyt:**

| Pole | Typ | Rodzaj | Opis |
|---|---|---|---|
| `Definicja` | `Soneta.Kalend.DefinicjaLimitu` | bazodanowe | rodzaj limitu (urlop wypoczynkowy itd.) |
| `Okres` | `Soneta.Types.FromTo` | bazodanowe | okres limitu (zwykle rok) |
| `OkresWażności` | `Soneta.Types.FromTo` | kalkulowane | okres ważności limitu |
| `Limit` | `int` | bazodanowe | limit (dni) wynikający z kodeksu pracy |
| `LimitDni` | `int` | kalkulowane | limit w dniach |
| `LimitGodz` | `Soneta.Types.Time` | bazodanowe | limit w godzinach |
| `Razem` / `RazemGodz` | `int` / `Time` | kalkulowane | łączny przysługujący (limit + przeniesienia + zmiany) |
| `Wykorzystane` / `WykorzystaneGodz` | `int` / `Time` | bazodanowe | wykorzystane dni/godziny |
| `Pozostalo` | `int` | kalkulowane | pozostało (dni, int) |
| `PozostaloDni` | `double` | kalkulowane | pozostało dni (z częścią ułamkową) |
| `PozostaloGodz` | `Soneta.Types.Time` | kalkulowane | pozostało godzin |
| `ZaleglyDni` / `ZaleglyGodz` | `double` / `Time` | kalkulowane | zaległy z poprzednich okresów |
| `Przeniesienie` / `PrzeniesienieDni` | `int` / `double` | kalkulowane | przeniesione z poprzedniego roku |
| `Korekta`, `Zmiana` | `int` | bazodanowe | korekty/zmiany limitu |
| `Pracownik` | `Soneta.Kadry.Pracownik` | bazodanowe (guided-parent), **read-only** | właściciel |

> **Wykorzystany = `Razem - Pozostalo`** (lub bezpośrednio pole `Wykorzystane`). „Przysługujący" to
> `Razem` (limit kodeksowy + przeniesienia + zmiany), a nie samo `Limit`.

**Dostęp do definicji limitów (`DefinicjeLimitow`):**

- `session.GetKalend().DefinicjeLimitow.WgNazwy[string]` — np. `WgNazwy["Urlop wypoczynkowy"]`.
- Skróty typowane (property zwracające `DefinicjaLimitu`): `DefinicjeLimitow.UrlopWypoczynkowy`,
  `.UrlopDodatkowy`, `.OpiekaNadZdrowym`, `.UrlopOpiekunczy`, `.ZwolnienieOdPracySilaWyzsza` itd.
- `DefinicjaLimitu` ma pola `Nazwa: string`, `Typ: TypLimitu`.

**Naliczanie limitu (by mógł istnieć do odczytu) — `Soneta.Kalend.NaliczanieLimitow`:**

- Klasa z **publicznym bezparametrowym ctor**; settowalne property:
  - `Pars: NaliczanieLimitow.Params` (set),
  - `Pracownicy: ICollection<Pracownik>` (set) **albo** `PracownicyIdx: Pracownik[]` (set).
- Klasa `NaliczanieLimitow.Params : ContextBase` ma **publiczny ctor `Params(Context context)`**
  oraz settowalne: `Definicja: DefinicjaLimitu`, `Okres: FromTo`, `KopiujKorekty: bool`,
  `ZapisPerPracownik: bool`.
- Metoda **`public void DodajLimit()`** — nalicza limit (zapisuje rekordy `LimitNieobecnosci`).
  (Jest też `DodajLimitUrlopowy()`.)

**Snippet — naliczenie + odczyt:**

```csharp
var kalend    = session.GetKalend();
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
var defUrlop  = kalend.DefinicjeLimitow.WgNazwy["Urlop wypoczynkowy"];   // lub DefinicjeLimitow.UrlopWypoczynkowy
var rok       = FromTo.Year(new Date(2026, 1, 1));

using (var t = session.Logout(editMode: true))
{
    var naliczanie = new NaliczanieLimitow
    {
        Pars = new NaliczanieLimitow.Params(Context.Empty.Clone(session))
        {
            Definicja     = defUrlop,
            Okres         = rok,
            KopiujKorekty = true
        },
        Pracownicy = new Pracownik[] { pracownik }
    };
    naliczanie.DodajLimit();          // tworzy/aktualizuje LimitNieobecnosci
    t.Commit();
}
session.Save();

// Odczyt limitu urlopu wypoczynkowego za rok 2026.
// UWAGA: filtr serwerowy obejmuje TYLKO pola bazodanowe i prostych porównań — Okres (FromTo)
// NIE da się porównać serwerowo (==), więc filtrujemy serwerowo po Definicja, a rok w pamięci:
var lim = pracownik.Limity[(LimitNieobecnosci l) => l.Definicja == defUrlop]
              .Cast<LimitNieobecnosci>()
              .FirstOrDefault(l => l.Okres.From == rok.From);
if (lim != null)
{
    int przysluguje  = lim.Razem;            // przysługujący (limit + przeniesienia + zmiany)
    int pozostalo    = lim.Pozostalo;        // pozostało
    int wykorzystany = przysluguje - pozostalo;  // == lim.Wykorzystane
    // lim.PozostaloDni, lim.PozostaloGodz, lim.ZaleglyDni
}
```

**Pułapki:**
- **Nie** twórz `new LimitNieobecnosci(...)` — limit powstaje przez naliczanie (`DodajLimit`). W bazie
  Demo limit dla danego roku może jeszcze nie istnieć — w teście trzeba go **najpierw naliczyć**.
- Kolekcja na pracowniku to `pracownik.Limity` (nie `LimityNieobecnosci`).
- **Nie porównuj `Okres` (FromTo) w filtrze serwerowym** — `l.Okres == rok` rzuca `ArgumentException`
  („pole nieznalezione"). Filtruj serwerowo po `Definicja`, a okres/rok porównaj w pamięci
  (`.FirstOrDefault(l => l.Okres.From == rok.From)`).
- `Razem` może wynosić `0` dla pracowników bez danych napędzających wymiar urlopu (staż, data
  urodzenia) — asercje opieraj na spójności (`Wykorzystane == Razem - Pozostalo`, `Razem >= 0`),
  a nie na założeniu `Razem > 0`.
- `Pracownik` na limicie jest read-only (relacja guided) — naliczanie samo wiąże rekord z pracownikiem.
- Filtruj limity serwerowo po `Definicja` i `Okres` (`pracownik.Limity[condition]`), nie iteruj całości
  z `if` w pamięci (safe-code §6.1). Tabela `LimNieobecnosci` jest operacyjna guided.
- `Context.Empty.Clone(session)` daje kontekst związany z bieżącą sesją — wymagany przez ctor
  `NaliczanieLimitow.Params(Context)`.
- Naliczanie modyfikuje dane operacyjne — w transakcji edycyjnej, krótko, z obsługą wyjątków z `Save()`.

### KADRY-D3 — Import e-ZLA z PUE ZUS (zwolnienia lekarskie)

**Cel:** zaewidencjonować w systemie zwolnienie lekarskie pobrane z PUE ZUS (e-ZLA). Sam **import to
operacja sieciowa** (komunikacja z PUE ZUS) — w kodzie biznesowym/teście dokumentujemy **model danych**
nieobecności chorobowej i jej dane ZUS, a nie samo połączenie z bramką PUE.

**Fakty o typie (zweryfikowane skanem DLL):**

- Zwolnienie chorobowe to `Soneta.Kalend.NieobecnośćPracownika` (typ konkretny z KADRY-D1) z `Definicja`
  wskazującą na rodzaj zasiłkowy (np. „Zwolnienie chorobowe").
- Dane ZUS zwolnienia leżą w subrowie **`Nieobecnosc.Zwolnienie: Soneta.Kalend.ZwolnienieZUS`**
  (bazodanowy subrow na rekordzie nieobecności).
- Dane samego dokumentu ZLA leżą w subrowie **`Nieobecnosc.ZLA: Soneta.Kalend.ZLA`**
  (`ZLA.Data: Date`, `ZLA.Wersja: WersjaZLA`, `ZLA.Zrodlo: MemoText`).

**Pola i typy (`Nieobecnosc.Zwolnienie: ZwolnienieZUS`) — zapisywalne, bazodanowe:**

| Pole | Typ | Opis |
|---|---|---|
| `Numer` | `string` | numer dokumentu ZLA (pole tekstowe — **maks. 9 znaków**) |
| `KodChoroby` | `string` | kod literowy choroby (A, B, C, D, …) |
| `Przyczyna` | `Soneta.Kalend.PrzyczynaZwolnienia` | przyczyna niezdolności do pracy |
| `Kwarantanna` | `Soneta.Kalend.ZwolnienieKwarantanna` | kwarantanna/izolacja |
| `LeczenieSzpitalne` | `bool` | pobyt w szpitalu |
| `ZwolnienieWystawione` | `Soneta.Types.Date` | data wystawienia ZLA |
| `ZwolnienieDostarczone` | `Soneta.Types.Date` | data dostarczenia |
| `PomniejszajZasilek` | `bool` | obniżenie zasiłku |
| `PonownieUstalPodstawe` | `bool` | wymuszenie przeliczenia podstawy (patrz KADRY-D2/KADRY-D6) |

**Pola i typy (`Nieobecnosc.ZLA: ZLA`):** `Data: Date`, `Wersja: WersjaZLA`, `Zrodlo: MemoText`.

**Snippet — ręczne odwzorowanie e-ZLA jako nieobecności chorobowej (bez sieci):**

```csharp
var kalend    = session.GetKalend();
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
var defChor   = kalend.DefNieobecnosci.WgNazwy["Zwolnienie chorobowe"];

using (var t = session.Logout(editMode: true))
{
    var nieob = session.AddRow(new NieobecnośćPracownika(pracownik));
    nieob.Definicja = defChor;
    nieob.Okres     = new FromTo(new Date(2026, 5, 4), new Date(2026, 5, 10));
    // dane ZUS z e-ZLA (subrow Zwolnienie):
    nieob.Zwolnienie.Numer      = "ZLA000001";   // pole Numer ma limit 9 znaków
    nieob.Zwolnienie.KodChoroby = "A";
    t.Commit();
}
session.Save();
```

**Pułapki:**
- **Sam import e-ZLA z PUE wymaga sieci** (uwierzytelnienie + bramka ZUS) — nie da się go odtworzyć
  w teście jednostkowym na bazie Demo; testuj wyłącznie **odwzorowanie modelu danych** (subrow `Zwolnienie`).
- `Zwolnienie` i `ZLA` to subrowy — nie tworzysz ich osobno, są częścią rekordu `Nieobecnosc`; ustawiasz
  ich pola po utworzeniu nieobecności.
- Definicja zasiłkowa musi istnieć w słowniku bazy (`DefNieobecnosci.WgNazwy[...]` ≠ `null`).
- **Faktyczne kwoty zasiłku** liczą się dopiero przy naliczeniu wypłaty — patrz uwaga przy KADRY-D2.

---

### KADRY-D4 — Generowanie deklaracji Z-3 / Z-3a dla nieobecności chorobowej

**Cel:** wygenerować zaświadczenie płatnika składek **Z-3** (pracownik etatowy) lub **Z-3a** (umowy/inni
ubezpieczeni) dla konkretnej nieobecności zasiłkowej.

**Fakty o typie (zweryfikowane skanem DLL):**

- Worker (czynność na `Nieobecnosc`): **`Soneta.Deklaracje.ZUS.ZUSZ3.Z3Worker`** — akcja
  „Generuj deklarację Z-3", metoda `public object UtworzDeklaracjeZ3()`.
- Analogicznie **`Soneta.Deklaracje.ZUS.ZUSZ3.Z3aWorker`** — akcja „Generuj deklarację Z-3a",
  metoda `public object UtworzDeklaracjeZ3a()`.
- Oba workery przyjmują przez `[Context]`:
  - `KeduContext: DeklaracjaZUS.PUEContext` (property `Kedu: KEDU`),
  - `Z3ParamContext: Z3ParamContext` / `Z3aParamContext` z polami m.in.: `Nieobecnosc: INieobecnoscLubZbieg`,
    `NieobecnoscZContextu: bool`, `Pracownik: Pracownik`, `PracownikZContextu: bool`, `Okres: FromTo`,
    `OkresZasiłkowy: FromTo`, `OkresZasilkowyOd: Date`, `Współczynnik: Fraction`, `RachBank: string`,
    `KontynuacjaŚwiadczenia: bool`.

**Snippet — generowanie Z-3 dla nieobecności (kontekst):**

```csharp
var worker = new Soneta.Deklaracje.ZUS.ZUSZ3.Z3Worker();
var ctx    = Context.Empty.Clone(session);
ctx[typeof(Nieobecnosc)] = nieobChorobowa;   // worker czyta nieobecność z kontekstu

var deklaracja = worker.UtworzDeklaracjeZ3();  // zwraca obiekt deklaracji Z-3
session.Save();
```

**Pułapki:**
- **Sensowny Z-3 wymaga naliczonej wypłaty/podstawy zasiłku** — bez naliczonej podstawy deklaracja
  powstanie z pustymi/zerowymi kwotami. W teście na czystej Demo zweryfikujesz fakt powstania obiektu
  i ustawienie pól nagłówkowych (pracownik, okres), ale **nie kwoty zasiłku**.
- Worker przyjmuje dane przez `Context` (`ctx[typeof(Nieobecnosc)]`/`ctx[typeof(Pracownik)]`) — nie ma
  prostego ctora parametrowego; zegnij pod swój scenariusz `Z3ParamContext`.
- Z-3 dotyczy etatu, Z-3a umów/innych ubezpieczonych — dobierz worker do tytułu ubezpieczenia.
- Metody zwracają `object` (deklaracja KEDU) — zachowaj/odczytaj wynik, nie zakładaj typu wprost.

---

### KADRY-D5 — Obsługa przestoju (dodanie/usunięcie, przestój ekonomiczny — % wynagrodzenia)

**Cel:** zaewidencjonować przestój pracownika (np. ekonomiczny) za okres oraz wskazać procent
wynagrodzenia przestojowego; usunąć przestój nakładający się na nieobecność ZUS.

**Fakty o typie (zweryfikowane skanem DLL):**

- **Dodanie przestoju:** worker **`Soneta.Kadry.DodajPrzestojWorker`** (czynność „Przestój/Dodaj przestój",
  metoda `public void DodajPrzestoj()`):
  - settowalne property: `Pracownicy: Pracownik[]`, `Pars: DodajPrzestojWorker.Params`;
  - `Params` z polami: `DefinicjaStrefy: Soneta.Kalend.DefinicjaStrefy`, `Okres: FromTo`.
- **Procent wynagrodzenia przestojowego (przestój ekonomiczny):** worker
  **`Soneta.Kadry.IndywidualnyProcentWynagrPrzestojowegoWorker`** (czynność
  „Przestój/Przestój ekonomiczny - procent wynagr.", metoda `public void Aktualizuj()`):
  - `Pracownicy: Pracownik[]`, `Pars.Data: Date`, `Pars.Procent: Soneta.Types.Percent`.
- **Usunięcie przestoju podczas nieobecności ZUS:** worker
  **`Soneta.Kadry.UsunPrzestojNieobecnoscWorker`** (czynność „Przestój/Usuń przestój podczas
  nieobecności ZUS", metoda `public void UsunPrzestoj()`): `Pracownicy: Pracownik[]`, `Pars.Okres: FromTo`.
- Procent wynagrodzenia przestojowego jest też trzymany na etacie:
  `PracHistoria.Etat.Postojowe: Soneta.Kadry.WynagrodzeniePostojowe` (`Procent: Percent`, `Standardowe: bool`).
- `DefinicjaStrefy` (`session.GetKalend().DefinicjeStref`) — słownik konfiguracyjny stref (m.in. przestoju).

**Snippet — dodanie przestoju:**

```csharp
var kalend     = session.GetKalend();
var pracownik  = session.GetKadry().Pracownicy.WgKodu["006"];
var defStrefa  = kalend.DefinicjeStref.WgNazwy["Przestój"];   // nazwa wg słownika danej bazy

var worker = new Soneta.Kadry.DodajPrzestojWorker
{
    Pracownicy = new[] { pracownik },
    Pars = new Soneta.Kadry.DodajPrzestojWorker.Params(Context.Empty.Clone(session))
    {
        DefinicjaStrefy = defStrefa,
        Okres           = new FromTo(new Date(2026, 6, 1), new Date(2026, 6, 5))
    }
};
worker.DodajPrzestoj();    // worker wykonuje własną transakcję
session.Save();
```

**Snippet — przestój ekonomiczny (procent):**

```csharp
var worker = new Soneta.Kadry.IndywidualnyProcentWynagrPrzestojowegoWorker
{
    Pracownicy = new[] { pracownik },
    Pars = new Soneta.Kadry.IndywidualnyProcentWynagrPrzestojowegoWorker.Params(Context.Empty.Clone(session))
    {
        Data    = new Date(2026, 6, 1),
        Procent = new Percent(0.5m)   // 50% wynagrodzenia
    }
};
worker.Aktualizuj();
session.Save();
```

**Pułapki:**
- `DefinicjeStref.WgNazwy[...]` zależy od słownika danej bazy — zweryfikuj nazwę przestoju w Demo
  (może być inna niż „Przestój"); dla nieistniejącej nazwy zwraca `null`.
- Worker wykonuje własną transakcję — nie zagnieżdżaj go w otwartej transakcji edycyjnej.
- `Percent` przyjmuj jako ułamek (`0.5m` = 50%), nie liczbę 50.
- `UsunPrzestojNieobecnoscWorker` usuwa przestój **kolidujący z nieobecnością ZUS** — to nie generyczne
  „usuń przestój"; zakres działania ogranicza okres + obecność nieobecności ZUS.
- Skutki płacowe (wynagrodzenie przestojowe) liczą się dopiero przy naliczeniu wypłaty.

---

### KADRY-D6 — Ustalanie/zmiana parametrów okresu zasiłkowego

**Cel:** zmienić parametry okresu zasiłkowego nieobecności chorobowej — kontynuację/przedłużenie okresu
zasiłkowego oraz wymusić ponowne ustalenie podstawy naliczania zasiłku.

**Fakty o typie (zweryfikowane skanem DLL):**

- Parametry okresu zasiłkowego są w subrowie **`Nieobecnosc.Zwolnienie: ZwolnienieZUS`** (bazodanowe,
  zapisywalne):
  - `KontynuacjaOkrZas: Soneta.Kalend.KontynuacjaOkrZas` (enum: `Warunkowo`, `Tak`, `Nie`),
  - `PrzedluzenieOkrZas: bool`, `PrzedluzeniaData: Soneta.Types.Date`,
  - `PonownieUstalPodstawe: bool` + metoda `SetPonownieUstalPodstawe(bool)` (patrz KADRY-D2).
- Worker korekty okresu zasiłkowego: **`Soneta.Kalend.Nieobecnosc.KorektaOkresuZasiłkowegoWorker`**
  (czynność „Zmień pozostałe parametry okresu zasiłkowego", metoda `public void PonownieUstalPodstawę()`):
  - settowalne `Pars: KorektaOkresuZasiłkowegoWorker.Params` z polami:
    `KontynuacjaOkrZas: KontynuacjaOkrZas`, `PrzedluzenieOkrZas: bool`, `PrzedluzeniaData: Date`.
- BO okresu zasiłkowego (przy wdrożeniu) — patrz KADRY-D10: `PracHistoria.ChorobowyBO`
  (`DniZasilkowe`, `ZasilekOdDnia`, `PrzedluzenieOZ`).

**Snippet — zmiana parametrów wprost na rekordzie:**

```csharp
using (var t = session.Logout(editMode: true))
{
    nieobChorobowa.Zwolnienie.KontynuacjaOkrZas  = KontynuacjaOkrZas.Tak;
    nieobChorobowa.Zwolnienie.PrzedluzenieOkrZas = true;
    nieobChorobowa.Zwolnienie.PrzedluzeniaData   = new Date(2026, 5, 31);
    nieobChorobowa.Zwolnienie.SetPonownieUstalPodstawe(true);
    t.Commit();
}
session.Save();
```

**Snippet — przez worker korekty okresu zasiłkowego:**

```csharp
var worker = new Nieobecnosc.KorektaOkresuZasiłkowegoWorker();
var ctx    = Context.Empty.Clone(session);
ctx[typeof(Nieobecnosc)] = nieobChorobowa;
worker.Pars = new Nieobecnosc.KorektaOkresuZasiłkowegoWorker.Params(ctx)
{
    KontynuacjaOkrZas  = KontynuacjaOkrZas.Tak,
    PrzedluzenieOkrZas = true,
    PrzedluzeniaData   = new Date(2026, 5, 31)
};
worker.PonownieUstalPodstawę();   // własna transakcja + Commit
session.Save();
```

**Pułapki:**
- **Faktyczne** przeliczenie kwot zasiłku następuje dopiero przy **ponownym naliczeniu wypłaty** — test
  na Demo zweryfikuje zmianę pól `KontynuacjaOkrZas`/`PrzedluzenieOkrZas`/`PrzedluzeniaData`/flagi,
  ale nie kwoty.
- Parametry okresu zasiłkowego mają sens tylko dla nieobecności **ZUS** (zwolnienia chorobowe/zasiłki) —
  dla urlopu wypoczynkowego są bez znaczenia.
- Worker wykonuje własną transakcję — nie zagnieżdżaj go w innej otwartej transakcji.

---

### KADRY-D8 — Naliczanie i przeliczanie limitów nieobecności

**Cel:** naliczyć limit nieobecności (jak KADRY-D7 — `NaliczanieLimitow.DodajLimit()`) oraz przeliczyć liczbę
wykorzystanych dni limitu (czynność „Przelicz wykorzystane").

**Fakty o typie (zweryfikowane skanem DLL):**

- **Naliczenie limitu:** klasa **`Soneta.Kalend.NaliczanieLimitow`** — publiczny bezparametrowy ctor;
  settowalne `Pars: NaliczanieLimitow.Params` (`Definicja: DefinicjaLimitu`, `Okres: FromTo`,
  `KopiujKorekty: bool`, `ZapisPerPracownik: bool`) oraz `Pracownicy: ICollection<Pracownik>` /
  `PracownicyIdx: Pracownik[]`; metoda `public void DodajLimit()` (i `DodajLimitUrlopowy()`).
  Wariant UI per-pracownik: worker **`Soneta.Kalend.UI.PracownikLimityNaliczanieWorker`**
  (czynność „Nalicz limit nieobecności", metoda `DodajLimit()`) — `Pracownik: Pracownik`,
  `Pars` jak wyżej.
- **Przeliczenie wykorzystanych:** worker
  **`Soneta.Kalend.LimitNieobecnosci.Pracownicy.PrzeliczWykorzystaneWorker`** (czynność
  „Limity nieobecności/Przelicz wykorzystane", metoda `public void PrzeliczWykorzystane()`):
  - settowalne `Pracownicy: Pracownik[]`, `Pars.Definicja: DefinicjaLimitu`, `Pars.Okres: FromTo`.

**Snippet — naliczenie + przeliczenie wykorzystanych:**

```csharp
var kalend    = session.GetKalend();
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
var defUrlop  = kalend.DefinicjeLimitow.WgNazwy["Urlop wypoczynkowy"];
var rok       = FromTo.Year(new Date(2026, 1, 1));

// 1) naliczenie limitu (jak KADRY-D7)
var naliczanie = new NaliczanieLimitow
{
    Pars = new NaliczanieLimitow.Params(Context.Empty.Clone(session))
    {
        Definicja     = defUrlop,
        Okres         = rok,
        KopiujKorekty = true
    },
    Pracownicy = new[] { pracownik }
};
naliczanie.DodajLimit();
session.Save();

// 2) przeliczenie wykorzystanych
var przelicz = new LimitNieobecnosci.Pracownicy.PrzeliczWykorzystaneWorker
{
    Pracownicy = new[] { pracownik },
    Pars = new LimitNieobecnosci.Pracownicy.PrzeliczWykorzystaneWorker.Params(Context.Empty.Clone(session))
    {
        Definicja = defUrlop,
        Okres     = rok
    }
};
przelicz.PrzeliczWykorzystane();
session.Save();
```

**Pułapki:**
- **Nie** twórz `new LimitNieobecnosci(...)` — limit powstaje przez naliczanie (jak w KADRY-D7).
- `PrzeliczWykorzystane` aktualizuje pole `LimitNieobecnosci.Wykorzystane` na podstawie wprowadzonych
  nieobecności — ma sens dopiero **po** naliczeniu limitu i wprowadzeniu nieobecności limitowanych.
- `Razem` może wynosić `0` dla pracownika bez danych napędzających wymiar — opieraj asercje na spójności
  (`Wykorzystane == Razem - Pozostalo`), nie na `Razem > 0` (patrz KADRY-D7).
- Workery wykonują własne transakcje — wywołuj poza otwartą transakcją edycyjną; obsłuż wyjątki z `Save()`.

---

### KADRY-D9 — Aktualizacja podstaw nieobecności ZUS / podstaw urlopu

**Cel:** odczytać/wprowadzić ręcznie podstawy naliczania zasiłków (chorobowe/macierzyńskie/opiekuńcze/
rehabilitacyjne) używane przy nieobecnościach ZUS — np. przy wdrożeniu lub korekcie podstawy.

**Fakty o typie (zweryfikowane skanem DLL):**

- Kolekcja na pracowniku: **`pracownik.PodstawyNieobecności: SubTable<Soneta.Place.PodstawaNieobecnosci>`**
  (jest też `PodstawyNieobecnościOkresowe: SubTable<PodstawaNieobecnosciOkresowa>`).
- **`Soneta.Place.PodstawaNieobecnosci`** — tabela `PodstawyNieobec`, `GuidedRow` **child** pracownika
  (relacja przez pole `Pracownik`).
- **Brak publicznego ctora** — `PodstawaNieobecnosci` ma jedynie ctory niepubliczne
  (`(RowCreator)`, `(Pracownik, TypyPodstawNieobecnosci)`). Rekordy powstają z **naliczenia wypłaty**;
  w kodzie biznesowym/teście realnie testowalny jest **odczyt** (dodawanie ręczne — patrz pułapki/spec).

**Pola i typy (`PodstawaNieobecnosci`) — bazodanowe, zapisywalne:**

| Pole | Typ | Opis |
|---|---|---|
| `Data` | `Soneta.Types.Date` | data podstawy |
| `Miesieczne` | `decimal` | podstawa miesięczna |
| `Kwartalne` / `Roczne` | `decimal` | składowe |
| `Podstawa` | `decimal` | podstawa naliczania chorobowego |
| `PodstawaM` / `PodstawaO` / `PodstawaR` | `decimal` | podstawa macierzyńskiego / opiekuńczego / rehabilitacyjnego |
| `Typ` | `Soneta.Place.TypyPodstawNieobecnosci` | `Chorobowa` / `Wypoczynkowy` |
| `Norma` / `NormaDni` | `Time` / `int` | norma czasu/dni |
| `Praca` / `PracaDni` | `Time` / `int` | przepracowane |
| `ProcentSkladki` | `Soneta.Types.Percent` | procent składki |

> **Podstawy urlopu wypoczynkowego** rozróżnia pole `Typ = TypyPodstawNieobecnosci.Wypoczynkowy`;
> podstawy zasiłków ZUS → `Typ = Chorobowa`.

**Snippet — odczyt podstaw + dodanie podstawy ręcznej:**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];

// Odczyt podstaw chorobowych (filtr serwerowy po Typ):
foreach (PodstawaNieobecnosci p in
         pracownik.PodstawyNieobecności[(PodstawaNieobecnosci x) => x.Typ == TypyPodstawNieobecnosci.Chorobowa])
{
    // p.Data, p.Podstawa, p.Miesieczne
}

// UWAGA: PodstawaNieobecnosci NIE ma publicznego ctora — normalnie powstaje z naliczenia wypłaty.
// Ręczne dodanie wymagałoby niepublicznego API → w teście testuj wyłącznie ODCZYT (powyżej).
```

**Pułapki:**
- Kwoty (`Miesieczne`, `Podstawa`, …) są typu `decimal` — to dane operacyjne podstaw; **normalnie
  podstawy powstają z naliczenia wypłaty** (brak publicznego ctora — patrz wyżej).
- `Pracownik` na podstawie jest read-only (guided-parent).
- Filtruj serwerowo po `Typ` (`PodstawyNieobecności[condition]`) — nie iteruj całości z `if` w pamięci.
- W teście na czystej Demo kolekcja `PodstawyNieobecności` może być pusta, dopóki nie naliczono wypłaty
  z zasiłkiem — testuj odczyt asercją na model/spójność, a scenariusz „dodaj ręcznie" oznacz `[Ignore]`.

---

### KADRY-D10 — Bilans otwarcia nieobecności i urlopów

**Cel:** wprowadzić bilans otwarcia (BO) przy wdrożeniu / starcie roku — historię chorobową (okres
zasiłkowy, dni wykorzystane) oraz urlop wykorzystany u poprzednich pracodawców / w pierwszym miesiącu.

**Fakty o typie (zweryfikowane skanem DLL):**

- BO leży na rekordzie historycznym **`Soneta.Kadry.PracHistoria`** w dwóch subrowach (bazodanowe,
  zapisywalne):
  - **`PracHistoria.ChorobowyBO: Soneta.Kadry.ChorobowyBO`** (BO chorobowy / okres zasiłkowy),
  - **`PracHistoria.DodatkowyBO: Soneta.Kadry.DodatkowyBO`** (BO urlopowy — urlop u poprzednich pracodawców).
- BO nieobecności pojedynczej oznacza też flaga `Nieobecnosc.BilansOtwarcia: bool`
  (interfejs `IBilansOtwarcia` na `Nieobecnosc`).

**Pola i typy (`ChorobowyBO`) — bazodanowe:**

| Pole | Typ | Opis |
|---|---|---|
| `Data` | `Soneta.Types.Date` | data BO |
| `MiesiacPodstawy` | `Soneta.Types.YearMonth` | miesiąc podstawy |
| `Podstawa` | `decimal` | podstawa BO |
| `DniWynagrodzenia` | `int` | dni zwolnienia finansowane przez pracodawcę |
| `DniZasilkowe` | `int` | dni wliczane do bieżącego okresu zasiłkowego |
| `DniZwolnienia` | `int` | dni nieprzerwanego zwolnienia dobrowolnego |
| `ZasilekOdDnia` | `Soneta.Types.Date` | zasiłek od dnia |
| `PrzedluzenieOZ` | `bool` | okres zasiłkowy przedłużony o 3 mies. |

**Pola i typy (`DodatkowyBO`) — bazodanowe:**

| Pole | Typ | Opis |
|---|---|---|
| `UPoprzednich` | `decimal` | urlop wykorzystany u poprzednich pracodawców (dni) |
| `Wykorzystany` | `Soneta.Types.Time` | wykorzystany przypadający na bieżące zatrudnienie (godz.) |
| `BezPierwszego` | `bool` | prawo do urlopu w 1. mies. nabyte u poprzedniego pracodawcy |

**Snippet — wprowadzenie BO chorobowego i urlopowego na zapisie historycznym:**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
var historia  = pracownik.Historia[Date.Today];   // właściwy zapis historyczny „na dzień"

using (var t = session.Logout(editMode: true))
{
    // BO chorobowy / okres zasiłkowy
    historia.ChorobowyBO.DniZasilkowe = 33;
    historia.ChorobowyBO.ZasilekOdDnia = new Date(2026, 1, 1);
    // BO urlopowy
    historia.DodatkowyBO.UPoprzednich = 10m;
    t.Commit();
}
session.Save();
```

**Pułapki:**
- `ChorobowyBO`/`DodatkowyBO` to **subrowy** zapisu `PracHistoria` — nie tworzysz ich osobno, edytujesz
  ich pola na istniejącym zapisie historycznym.
- **`ChorobowyBO`** (`DniZasilkowe`, `ZasilekOdDnia`, `PrzedluzenieOZ`, …) jest **zapisywalny** na zwykłym
  zapisie historii (zweryfikowane testem KADRY-D10 na Demo).
- **`DodatkowyBO`** (`UPoprzednich`, `BezPierwszego`, `Wykorzystany`) na zwykłym zapisie historii Demo
  rzuca **`ColReadOnlyException`** („pole w trybie tylko do odczytu") — BO urlopowy jest zapisywalny tylko
  na zapisie historycznym oznaczonym jako **bilans otwarcia / start zatrudnienia**, nie na dowolnym zapisie
  „na dzień". W teście na gotowych pracownikach Demo dodawanie `DodatkowyBO` oznacz `[Ignore]`.
- Pobierz właściwy zapis historyczny przez `pracownik.Historia[data]` (patrz KADRY-A14/KADRY-A15) — edycja BO na
  niewłaściwym zapisie da błędne dane „na dzień".
- BO ma sens przy wdrożeniu — nie miesza się z normalnym naliczaniem; po wprowadzeniu wpływa na limity
  (KADRY-D8) i okres zasiłkowy (KADRY-D6) dopiero przy przeliczeniu/naliczeniu.

---

### KADRY-D11 — Wnioski o urlop / delegację

**Cel:** zarejestrować wniosek urlopowy (lub o delegację), zmienić jego stan (akceptacja/odrzucenie/
przywrócenie) i — docelowo — przekształcić zaakceptowany wniosek w nieobecność.

**Fakty o typie (zweryfikowane skanem DLL):**

- Wniosek urlopowy: **`Soneta.Kadry.WniosekUrlopowy`** — tabela `WnioskiUrlopowe`, `GuidedRow` root.
  Konstruktory publiczne: **`new WniosekUrlopowy(Pracownik pracownik)`** oraz
  **`new WniosekUrlopowy(Pracownik pracownik, DefinicjaNieobecnosci definicja)`**.
- Kolekcja na pracowniku: **`pracownik.WnioskiUrlopowe: SubTable<Soneta.Kadry.WniosekUrlopowy>`**
  (oraz `WnioskiKierownika`, `WnioskiZastępcy` — te same wnioski w roli kierownika/zastępcy).
- Pola `WniosekUrlopowy` (bazodanowe, zapisywalne): `Pracownik: Pracownik`,
  `Definicja: DefinicjaNieobecnosci`, `Okres: FromTo`, `Data: Date`, `DataDecyzji: Date`,
  `Kierownik: Pracownik`, `Opis: MemoText`, `Stan: Soneta.Kadry.StanWnioskuUrlopowego`.
  - `StanWnioskuUrlopowego`: `Oczekujący`, `Anulowany`, `Zaakceptowany`, `Odrzucony`, `Korygowana`.
- Wniosek o delegację jest subrowem wniosku: `WniosekUrlopowy.Delegacja: Soneta.Kadry.WniosekODelegację`
  (`DataRozpoczeciaPlanowana`, `DataZakonczeniaPlanowana: DateShortTime`, `KrajDocelowy`, `Cel: MemoText`,
  `WnioskowanaZaliczka: Currency`); samodzielny `new WniosekODelegację()` ma publiczny ctor bezparametrowy.
- **Planowane nieobecności** (osobny model, np. plan urlopów): kolekcja
  **`pracownik.PlanowaneNieobecności: FromToSubTable<Soneta.Kalend.PlanowanaNieobecność>`**;
  typ `PlanowanaNieobecność` (tabela `PlanNieobecnosci`, root) z ctorem
  **`new PlanowanaNieobecność(Pracownik pracownik)`**, polami `Definicja`, `Okres: FromTo`.
  - **`Definicja` musi mieć zaznaczone pole `Planowana`** (`DefinicjaNieobecnosci.Planowana == true`) —
    inaczej setter rzuca `RowException` „Wybrana definicja musi mieć zaznaczone pole 'Planowana'."; dobierz
    definicję dynamicznie: `DefNieobecnosci.Cast<DefinicjaNieobecnosci>().First(d => d.Planowana)`.
  - **`Stan: StanPlanowanejNieobecności` jest READ-ONLY** (`Oczekująca`, `Wprowadzona`, `Korygowana`,
    `Zatwierdzona`, `Anulowana`) — **nie przypisujesz** go wprost (`plan.Stan = …` → błąd kompilacji
    „cannot be assigned to"); przejścia stanu wykonujesz metodami domenowymi
    **`StanWprowadzona()` / `StanZatwierdzona()` / `StanAnulowana()` / `StanOczekująca()`**.
- Akceptacja/odrzucenie/przywrócenie z poziomu Pulpitu: worker (UI/Net)
  **`PracownikNetWnioskiUrlopowe`** z akcjami „Zatwierdź wniosek"/`Zatwierdz`, „Odrzuć wniosek"/`Odrzuc`,
  „Przywróć wniosek"/`Przywroc`. W kodzie biznesowym/teście prościej ustawiać `Stan` wprost.

**Snippet — rejestracja wniosku urlopowego + akceptacja:**

```csharp
var kalend    = session.GetKalend();
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
// UWAGA: dla definicji limitowanej (np. „Urlop wypoczynkowy") akceptacja wniosku (set Stan) wyzwoli
// przeliczenie limitu → LimitNotFoundException, jeśli limit nie został wcześniej naliczony (patrz pułapki).
// Tu używamy definicji bezlimitowej (np. „Urlop bezpłatny (art 174 kp)") albo najpierw naliczamy limit (KADRY-D8).
var defUrlop  = kalend.DefNieobecnosci.WgNazwy["Urlop bezpłatny (art 174 kp)"];

using (var t = session.Logout(editMode: true))
{
    var wniosek = session.AddRow(new WniosekUrlopowy(pracownik, defUrlop));
    wniosek.Okres = new FromTo(new Date(2026, 8, 3), new Date(2026, 8, 7));
    wniosek.Data  = Date.Today;
    wniosek.Stan  = StanWnioskuUrlopowego.Oczekujący;
    t.Commit();
}
session.Save();

// Akceptacja (zmiana stanu):
using (var t = session.Logout(editMode: true))
{
    var wniosek = pracownik.WnioskiUrlopowe
        .Cast<WniosekUrlopowy>()
        .First(w => w.Stan == StanWnioskuUrlopowego.Oczekujący);
    wniosek.Stan        = StanWnioskuUrlopowego.Zaakceptowany;
    wniosek.DataDecyzji = Date.Today;
    t.Commit();
}
session.Save();
```

**Pułapki:**
- **Akceptacja wniosku na definicji limitowanej rzuca `LimitNotFoundException`** bez wcześniej naliczonego
  limitu: ustawienie `Stan` (np. `Zaakceptowany`) na wniosku z definicją „Urlop wypoczynkowy" wewnętrznie
  ustawia `Okres` nieobecności i wyzwala `DefinicjaLimitu.Przelicz(...)`, który dla pracownika bez limitu
  na ten dzień rzuca wyjątek. Rozwiązanie: albo nalicz limit (KADRY-D8) **przed** zmianą stanu, albo do scenariusza
  obsługi samego rekordu wniosku użyj definicji **bezlimitowej** (np. „Urlop bezpłatny (art 174 kp)").
- **Przekształcenie wniosku w nieobecność** wymaga, by nieobecność limitowana miała naliczony limit
  (jak KADRY-D1) — sama akceptacja wniosku nie tworzy automatycznie rozliczonej nieobecności w teście bez
  naliczonego limitu/wypłaty.
- `WniosekODelegację` to subrow wniosku (`WniosekUrlopowy.Delegacja`) — wnioskowanie o delegację
  ustawiasz na tym subrowie; pełne rozliczenie delegacji to moduł `Soneta.Delegacje` (osobny dokument
  handlowy PWS), poza zakresem wniosku.
- Filtruj kolekcję wniosków przez `WnioskiUrlopowe[condition]` lub iteruj w zakresie jednego pracownika;
  nie skanuj globalnej tabeli `WnioskiUrlopowe` bez zakresu (tabela operacyjna guided).
- Stan zmieniaj świadomie wg enuma `StanWnioskuUrlopowego` — workery Net robią to samo z dodatkową
  logiką workflow (powiadomienia), której w teście jednostkowym nie odtworzysz.

---

### KADRY-D12 — Praca zdalna (wnioski, lokalizacje, ewidencja)

**Cel:** skonfigurować pracę zdalną pracownika (model pracy, limit pracy zdalnej okazjonalnej),
zarejestrować wniosek o pracę zdalną i lokalizacje jej świadczenia oraz odczytać ewidencję.

**Fakty o typie (zweryfikowane skanem DLL):**

- Parametry pracy zdalnej leżą na etacie/historii: **`PracHistoria.PracaZdalna: Soneta.Kadry.PracZdalna`**
  (subrow, bazodanowe, zapisywalne):
  - `ModelPracy: Soneta.Kadry.ModelPracy` (`NieDotyczy`, `PracaStacjonarna`, `PracaHybrydowa`, `PracaZdalna`),
  - `OswiadczenieWarunki: bool` (warunki lokalowe/techniczne),
  - `LimitPZ: int`, `IndywidualnyLimitPZ: bool`, `TypLimituPZ: TypLimituPracyZdalnej`
    (`Roczny`, `Miesieczny`, `Tygodniowy`, `Kwartalny`, `Półroczny`).
- Lokalizacje pracy zdalnej: **`pracownik.LokalizacjePracyZdalnej: SubTable<Soneta.Kadry.LokalizacjaPracyZdalnej>`**
  (tabela `LokPracZdalnej`).
- Wnioski o pracę zdalną: **`pracownik.WnioskiPracyZdalnej: SubTable<Soneta.Kalend.WniosekPracyZdalnej>`**
  (oraz `WnioskiPracyZdalnejKierownika`); typ `WniosekPracyZdalnej` ma ctor
  `(Pracownik, DefinicjaRodzajuPracyZdalnej)` — **ctory są niepubliczne**, więc tworzenie wniosku idzie
  przez worker (`GrupoweZleceniePracyZdalnejWorker`) lub Pulpit, nie wprost `new`.
- Lokalizacja pracy zdalnej: `Soneta.Kadry.LokalizacjaPracyZdalnej` ma **publiczny ctor
  `new LokalizacjaPracyZdalnej(Pracownik pracownik)`**.
- Ewidencja/odczyt limitu pracy zdalnej okazjonalnej: worker
  **`Soneta.Kadry.Pracownik.PracaZdalnaWorker`** — property odczytowe (bez akcji modyfikującej):
  `DniPracyZdalnejRazem: int`, `DniPracyZdalnejOkazjonalnej: int`, `DniPracyZdalnejOkazjonalnejLimit: int`,
  `CzasPracyZdalnejRazem: Time`, `LimitPracaZdalnaOkazjonalna: int`, `PozostaloPracaZdalnaOkazjonalna: int`;
  kontekst: `Pracownik: Pracownik`, `Okres: FromTo`.
- Grupowe zlecenie pracy zdalnej (Pulpit/seryjne): worker
  **`Soneta.Kadry.UI.KadryNet.Workers.GrupoweZleceniePracyZdalnejWorker`** (akcja
  „Dodaj wnioski zlecenia pracy zdalnej"/`DodajZleceniaPracyZdalnej`): `Pracownicy: Pracownik[]`,
  `Pars.Okres: FromTo`, `Pars.Data: Date`, `Pars.Uwagi: string`.
- Aktualizacja podzielników kosztów na podstawie pracy hybrydowej: worker
  **`AktualizujPodzielnikowPracaZdalnaWorker`** (`DefinicjaPodzielnika`, `Okres: YearMonth`, …).

**Snippet — ustawienie modelu pracy zdalnej + lokalizacja + wniosek:**

```csharp
var kadry     = session.GetKadry();
var pracownik = kadry.Pracownicy.WgKodu["006"];

using (var t = session.Logout(editMode: true))
{
    var historia = pracownik.Historia[Date.Today];
    historia.PracaZdalna.ModelPracy          = ModelPracy.PracaHybrydowa;
    historia.PracaZdalna.OswiadczenieWarunki = true;

    // lokalizacja pracy zdalnej (np. adres domowy)
    var lok = session.AddRow(new LokalizacjaPracyZdalnej(pracownik));
    // … pola adresowe lokalizacji wg LokalizacjaPracyZdalnej
    t.Commit();
}
session.Save();

// Odczyt ewidencji pracy zdalnej okazjonalnej (worker odczytowy):
// Pracownik i Okres są zwykłymi, settowalnymi property (nie trzeba przekazywać przez Context):
var pz = new Soneta.Kadry.Pracownik.PracaZdalnaWorker
{
    Pracownik = pracownik,
    Okres     = FromTo.Year(new Date(2026, 1, 1))
};
// odczyt: pz.DniPracyZdalnejRazem, pz.LimitPracaZdalnaOkazjonalna, pz.PozostaloPracaZdalnaOkazjonalna
```

**Pułapki:**
- `PracaZdalnaWorker` to worker **odczytowy** (ma property, brak akcji modyfikującej) — służy do
  prezentacji ewidencji/limitu, nie do zapisu.
- `ModelPracy`/`OswiadczenieWarunki` są na **historycznym** zapisie etatu (`PracHistoria.PracaZdalna`) —
  edytuj właściwy zapis „na dzień".
- `WniosekPracyZdalnej` ma **niepubliczne ctory** — w teście jednostkowym nie utworzysz go przez `new`;
  zlecenie pracy zdalnej idzie przez worker `GrupoweZleceniePracyZdalnejWorker` (czynność Net/UI,
  wymaga `Context`). Testuj raczej `ModelPracy`/`OswiadczenieWarunki` na `PracHistoria.PracaZdalna`
  i `LokalizacjaPracyZdalnej` (ma publiczny ctor).
- `LokalizacjaPracyZdalnej` ma publiczny ctor `(Pracownik)` — testowalna wprost.

