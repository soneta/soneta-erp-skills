# KADRY06 — RCP — rejestracja czasu pracy

> Wspólne fakty o typie, podstawowe typy i szablon wzorca: [../kadry.md](../kadry.md).

> **Dwie tabele.** Zarejestrowany (surowy) czas pracy z czytników RCP leży w
> `pracownik.DniRCP : DateSubTable<Soneta.Kalend.DzienRCP>` (tabela `DniRCP`). Pojedyncze zdarzenia
> wejścia/wyjścia (`Soneta.Kalend.WejscieWyjscie`, tabela `WejsciaWyjscia`) są **childem `DzienPracy`**
> (pole `WejscieWyjscie.Dzien : DzienPracy`, kolekcja `dzienPracy.WeWy`), a `DzienPracy` to
> ewidencja w `pracownik.DniPracy`. `DzienRCP` jest stanem zweryfikowanym RCP (z polem
> `StanRCP : StanWeryfikacjiRCP`), powstaje z importu/przeliczenia.

### KADRY-F1 — Rejestracja czasu pracy pracownika (★)

**Cel:** odczytać zarejestrowany/zewidencjonowany czas pracy pracownika za dzień oraz (gdy trzeba)
utworzyć dzień ewidencji.

**Pola i typy:**

| Element | Lokalizacja | Typ | Uwaga |
|---|---|---|---|
| Ewidencja (kolekcja) | `pracownik.DniPracy` | `DateSubTable<Soneta.Kalend.DzienPracy>` | indeksator `[Date]` (get); element ctorem |
| Dzień ewidencji | `pracownik.DniPracy[data]` | `Soneta.Kalend.DzienPracy` | `null` przy braku |
| RCP zweryfikowane (kolekcja) | `pracownik.DniRCP` | `DateSubTable<Soneta.Kalend.DzienRCP>` | analogicznie |
| Dzień RCP | `pracownik.DniRCP[data]` | `Soneta.Kalend.DzienRCP` | `null` przy braku |
| Przepracowany czas (subrow) | `DzienPracy.Praca` / `DzienRCP.Praca` | `Soneta.Kalend.CzasPracy` | `Praca.OdGodziny`, `Praca.DoGodziny`, `Praca.Czas : Time` |
| Czas/Od (odczyt) | `Dzien*.Czas`, `Dzien*.OdGodziny` | `Soneta.Types.Time` | kalkulowane |
| Stan weryfikacji RCP | `DzienRCP.StanRCP` | `Soneta.Kalend.StanWeryfikacjiRCP` | zapisywalne |
| Flaga importu RCP | `Dzien*.RcpOK` | `bool` | zapisywalne; stan rekordu po imporcie |
| Zdarzenia we/wy dnia | `DzienPracy.WeWy` | `LpSubTable<Soneta.Kalend.WejscieWyjscie>` | patrz KADRY-F2 |
| Uwagi / błędy (RCP) | `DzienRCP.Uwagi`, `DzienRCP.Bledy` | `Soneta.Business.MemoText` | zapisywalne |

**Snippet:**

```csharp
var kalend = session.GetKadry().Session.GetKalend();
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
var data = new Date(2026, 6, 1);

// --- Odczyt zaewidencjonowanego czasu (ewidencja) ---
var dzienPracy = pracownik.DniPracy[data];          // typowane: DzienPracy lub null
if (dzienPracy is not null)
{
    Time przepracowano = dzienPracy.Praca.Czas;     // suma czasu pracy dnia
    Time od = dzienPracy.Praca.OdGodziny;
    Time @do = dzienPracy.Praca.DoGodziny;
}

// --- Odczyt stanu RCP (zweryfikowany rejestr) ---
var dzienRcp = pracownik.DniRCP[data];              // DzienRCP lub null
if (dzienRcp is not null)
{
    Time czasRcp = dzienRcp.Praca.Czas;
    StanWeryfikacjiRCP stan = dzienRcp.StanRCP;
}

// --- Utworzenie dnia ewidencji (gdy potrzebny ręczny wpis) ---
using (var t = session.Logout(editMode: true))
{
    var dp = pracownik.DniPracy[data];
    if (dp is null)
    {
        dp = session.AddRow(new DzienPracy(pracownik, data));   // ctor (Pracownik, Date)
        kalend.DniPracy.AddRow(dp);                              // alternatywnie przez Module.DniPracy
    }
    dp.Praca.OdGodziny = new Time(8, 0);
    dp.Praca.DoGodziny = new Time(16, 0);
    t.Commit();
}
session.Save();
```

**Pułapki:**
- `DniPracy`/`DniRCP` są **typowane** (`DateSubTable<DzienPracy>` / `<DzienRCP>`) — indeksator
  `[Date]` zwraca od razu właściwy typ lub `null`. Nie iteruj całej kolekcji, by „znaleźć" dzień —
  użyj indeksatora po dacie albo `[FromTo]` dla zakresu.
- Czas pracy ustawiaj na subrowie `Praca` (od–do); `Dzien*.Czas`/`Dzien*.OdGodziny` na rootcie dnia są
  kalkulowane (read-only).
- `DzienRCP` to **wynik weryfikacji** importu RCP (z czytników) — w normalnym przepływie nie tworzysz
  go ręcznie, lecz odczytujesz po imporcie/przeliczeniu. `DzienPracy` (ewidencja) to właściwe miejsce
  na ręczny wpis.
- Świeży `DzienPracy` z `new DzienPracy(pracownik, data)` trzeba dodać do tabeli
  (`Module.DniPracy.AddRow(...)` lub `session.AddRow(...)`) — sam ctor go nie rejestruje.

### KADRY-F2 — Rejestracja wejścia/wyjścia (RCP) (★)

**Cel:** dodać zdarzenie wejścia/wyjścia do dnia oraz odczytać listę zdarzeń RCP danego dnia.

**Pola i typy (`Soneta.Kalend.WejscieWyjscie`, tabela `WejsciaWyjscia`):**

| Pole | Typ | Uwaga |
|---|---|---|
| `Dzien` | `Soneta.Kalend.DzienPracy` | właściciel (guided-parent); ustawiany przez ctor `(DzienPracy)` |
| `Godzina` | `Soneta.Types.Time` | godzina zdarzenia (zapisywalne) |
| `Typ` | `Soneta.Kalend.TypWejsciaWyjscia` | enum: `Niezdefiniowany`, `Wejscie`, `Wyjscie`, `WejscieSluzbowe`, `WyjscieSluzbowe`, `WejsciePrywatne`, `WyjsciePrywatne` |
| `Operacja` | `int` | kod operacji urządzenia (zapisywalne) |
| `Lp` | `int` | liczba porządkowa zdarzeń w dniu (bazodanowe) |
| `DefinicjaZdarzenia` | `Soneta.Kalend.DefinicjaZdarzeniaRCP` | opcjonalna definicja zdarzenia ze słownika `DefZdarzenRCP` |
| Kolekcja zdarzeń dnia | `DzienPracy.WeWy : LpSubTable<WejscieWyjscie>` | uporządkowana po `Lp` |

**Snippet:**

```csharp
var kalend = session.GetKadry().Session.GetKalend();
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
var data = new Date(2026, 6, 1);

using (var t = session.Logout(editMode: true))
{
    // Upewnij się, że istnieje dzień ewidencji (właściciel zdarzeń):
    var dp = pracownik.DniPracy[data];
    if (dp is null)
    {
        dp = session.AddRow(new DzienPracy(pracownik, data));
        kalend.DniPracy.AddRow(dp);
    }

    // Wejście 8:00
    var we = new WejscieWyjscie(dp);          // ctor wiąże zdarzenie z dniem
    kalend.WejsciaWyjscia.AddRow(we);
    we.Godzina = new Time(8, 0);
    we.Typ = TypWejsciaWyjscia.Wejscie;

    // Wyjście 16:00
    var wy = new WejscieWyjscie(dp);
    kalend.WejsciaWyjscia.AddRow(wy);
    wy.Godzina = new Time(16, 0);
    wy.Typ = TypWejsciaWyjscia.Wyjscie;

    t.Commit();
}
session.Save();

// --- Odczyt zdarzeń dnia ---
var dzien = pracownik.DniPracy[data];
if (dzien is not null)
{
    foreach (WejscieWyjscie wewy in dzien.WeWy)   // posortowane po Lp
    {
        // wewy.Godzina, wewy.Typ, wewy.Operacja
    }
}
```

**Pułapki:**
- `WejscieWyjscie` jest childem **`DzienPracy`**, nie `DzienRCP` — najpierw potrzebujesz dnia
  ewidencji (`pracownik.DniPracy[data]`); zdarzenia wiążesz ctorem `new WejscieWyjscie(dzienPracy)`
  i dodajesz do tabeli `kalend.WejsciaWyjscia.AddRow(...)`.
- `Typ` to enum `TypWejsciaWyjscia` (`Wejscie`/`Wyjscie`/…), nie string ani `int`. Para
  wejście+wyjście jest podstawą wyliczenia czasu dnia z surowych zdarzeń.
- `DefinicjaZdarzenia` jest **opcjonalna** — przy ręcznym wpisie wystarczą `Godzina` + `Typ`. Jeśli
  używasz definicji, pobierz wpis ze słownika konfiguracyjnego `kalend.DefZdarzenRCP` (nie twórz w locie).
- `WeWy` to `LpSubTable` — kolejność zdarzeń wynika z `Lp` (nadawane automatycznie); nie ustawiaj `Lp`
  ręcznie. Do usunięcia wszystkich zdarzeń dnia (przy ponownym imporcie) służy kasowanie elementów kolekcji.
- Surowe zdarzenia są przeliczane na czas pracy/RCP przez kalkulator i import — samo dodanie
  wejść/wyjść nie aktualizuje automatycznie `DzienRCP` (to robi przeliczenie/import RCP).

### KADRY-F3 — Import danych z RCP (bezpośredni i przez tabelę pośrednią)

**Cel:** wczytać surowe odbicia z czytników RCP i przeliczyć je na ewidencję/zweryfikowany RCP.
**UWAGA: operacja plikowa/sieciowa — opis modelu; samego importu z pliku/urządzenia NIE testujemy.**

**Model danych:**

| Element | Lokalizacja | Typ | Uwaga |
|---|---|---|---|
| Tabela pośrednia (surowe odbicia) | `DzienPracy.WeWy` | `LpSubTable<Soneta.Kalend.WejscieWyjscie>` | zdarzenia we/wy (godzina, typ) — patrz KADRY-F2 |
| Zarejestrowany RCP (zweryfikowany) | `pracownik.DniRCP[data]` | `Soneta.Kalend.DzienRCP` | wynik importu/przeliczenia |
| Ewidencja | `pracownik.DniPracy[data]` | `Soneta.Kalend.DzienPracy` | docelowa realizacja |
| Flaga importu | `DzienPracy.RcpOK` / `DzienRCP.RcpOK` | `bool` | „stan rekordu po imporcie z RCP" |
| Stan weryfikacji | `DzienRCP.StanRCP` | `StanWeryfikacjiRCP` | patrz KADRY-F4 |

**Workery przeliczające (po wczytaniu odbić — operują na obiektach sesji):**

| Worker | Sygnatura | Rola |
|---|---|---|
| `Soneta.Kalend.ImportDniaWorker` | ctor `()`, `DzienPracy DzienPracy {get;set;}`, `void Przelicz()` | przelicza pojedynczy dzień z we/wy na czas pracy |
| `Soneta.Kalend.RCPWeryfikatorWorker` | `Dopasuj()`, `DopasujDlaZaznaczonych()`, `Dodaj()`, `Usun()` (+ `IsVisible*`), props `rw : RCPWeryfikator`, `Strefy`, `Wybrana` | dopasowanie odbić do plan/strefy (UI) |

**Snippet (przeliczenie dnia z już wczytanych we/wy — bez pliku):**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
var dzien = pracownik.DniPracy[new Date(2026, 6, 1)];   // dzień z wpisanymi WeWy (KADRY-F2)
if (dzien is not null)
{
    using (var t = session.Logout(editMode: true))
    {
        new ImportDniaWorker { DzienPracy = dzien }.Przelicz();   // we/wy -> czas pracy
        t.Commit();
    }
    session.Save();
}
```

**Pułapki / wykonalność:**
- **Sam import z pliku/urządzenia (czytnik, sieć, format) jest poza zakresem testu** — wymaga
  zewnętrznego źródła (plik/serwis), brak czystego API w tym kontrakcie.
- **Testowalny fragment**: przygotowanie tabeli pośredniej `DzienPracy.WeWy` (ctor `WejscieWyjscie(dp)`,
  patrz KADRY-F2) + `ImportDniaWorker.Przelicz()` — to przelicza już-wczytane odbicia bez I/O.
- `RCPWeryfikatorWorker` jest mocno UI (metody `IsVisible*`, `Strefy`/`Wybrana`) — to dopasowanie
  ręczne; nie wywoływać z kodu biznesowego.
- `DzienRCP` powstaje z importu/przeliczenia — w teście nie twórz go „z palca"; odczytuj po `Przelicz()`.

---

### KADRY-F4 — Weryfikacja i korekta danych RCP (★ testowalne)

**Cel:** odczytać i skorygować zweryfikowany rekord RCP — zmienić stan weryfikacji oraz poprawić
godziny pracy / opisać błędy i uwagi.

**Pola `Soneta.Kalend.DzienRCP` (tabela `DniRCP`, child `Pracownik`):**

| Pole | Typ | Rodzaj | Uwaga |
|---|---|---|---|
| `Data` | `Soneta.Types.Date` | bazodanowe | data dnia (ctor) |
| `Pracownik` | `Soneta.Kadry.Pracownik` | bazodanowe, guided-parent | właściciel |
| `Praca` | `Soneta.Kalend.CzasPracy` | bazodanowe | `Praca.OdGodziny`/`Praca.DoGodziny`/`Praca.Czas : Time` (zapisywalne) |
| `Czas`, `OdGodziny` | `Soneta.Types.Time` | kalkulowane | read-only (z `Praca`) |
| `StanRCP` | `Soneta.Kalend.StanWeryfikacjiRCP` | bazodanowe | stan weryfikacji (zapisywalne) |
| `RcpOK` | `bool` | bazodanowe | stan rekordu po imporcie (zapisywalne) |
| `Uwagi` | `Soneta.Business.MemoText` | bazodanowe | uwagi do weryfikacji |
| `Bledy` | `Soneta.Business.MemoText` | bazodanowe | opis błędów |
| `Strefy` | `SubTable<Soneta.Kalend.StrefaRCP>` | | strefy zarejestrowane |
| `StrefyOrg` | `Soneta.Business.MemoText` | bazodanowe | strefy źródłowe (org.) |

**`Soneta.Kalend.StanWeryfikacjiRCP` (enum):** `DoWeryfikacji`, `WymagaWeryfikacji`,
`PrzekazanyDoWyjaśnienia`, `DoZatwierdzenia`, `Modyfikowany`, `Naniesiony`, `Poprawny`, `Błędny`,
`Wszystkie`.

**Snippet (korekta godzin + zmiana stanu):**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
var dzienRcp = pracownik.DniRCP[new Date(2026, 6, 1)];   // DzienRCP lub null
if (dzienRcp is not null)
{
    using (var t = session.Logout(editMode: true))
    {
        dzienRcp.Praca.OdGodziny = new Time(8, 0);       // korekta na subrowie Praca
        dzienRcp.Praca.DoGodziny = new Time(16, 0);
        dzienRcp.StanRCP = StanWeryfikacjiRCP.Poprawny;  // zatwierdzenie weryfikacji
        dzienRcp.Uwagi = (MemoText)"Skorygowano wyjście";
        t.Commit();
    }
    session.Save();
}
```

**Pułapki:**
- `DniRCP` jest **typowane** (`DateSubTable<DzienRCP>`) — indeksator `[Date]` zwraca `DzienRCP`/`null`;
  do zakresu użyj `[FromTo]`. Nie iteruj kolekcji w poszukiwaniu dnia.
- Godziny koryguj na **subrowie `Praca`** (`Praca.OdGodziny`/`DoGodziny`); `DzienRCP.Czas`/`OdGodziny`
  na rootcie są kalkulowane (read-only).
- `StanRCP` to enum `StanWeryfikacjiRCP` — nie string. Zmiana stanu może podlegać weryfikatorom.
- W Demo `DzienRCP` istnieje tylko gdy był import/przeliczenie — test korekty zakłada istniejący dzień
  (sprawdzaj `is not null`), nie twórz `DzienRCP` ręcznie.

---

### KADRY-F5 — Rozliczenie pracy hybrydowej / aktualizacja podzielników na podstawie pracy hybrydowej

**Cel:** rozliczyć czas pracy hybrydowej (podział na strefy: stacjonarna / praca zdalna / zdalna
okazjonalna) i zaktualizować podzielniki (elementy rozliczenia czasu pracy / strefy dnia), na
podstawie których naliczane są składniki płacowe i koszty.

**Model danych (publiczny kontrakt):**

| Element | Lokalizacja | Typ | Uwaga |
|---|---|---|---|
| Strefy pracy dnia | `DzienPracy.Strefy` | `SubTable<Soneta.Kalend.StrefaPracy>` | podział dnia na strefy |
| Strefa pracy | `Soneta.Kalend.StrefaPracy` (ctor `(DzienPracy dzien)`) | — | `Definicja : DefinicjaStrefy`, `CzasRozliczany : Time`, `OdGodziny`/`Czas` (kalk.) |
| Definicja strefy | `Soneta.Kalend.DefinicjaStrefy` | konfiguracja | `Typ : TypStrefy`, `Wchodzi`, `Rozliczana`; stałe `Praca_Zdalna`, `PracaZdalnaOkazjonalna : Guid` |
| Dokumenty rozliczenia | `pracownik.RozliczeniaCzasuPracy` | `SubTable<Soneta.Kalend.RozliczenieCzasuPracy>` | dokumenty rozliczenia czasu (podzielniki) |
| Elementy rozliczenia | `pracownik.ElementyRozliczeniaCzasuPracy` | `SubTable<Soneta.Kalend.ElementRozliczeniaCzasuPracy>` | pozycje podzielnika |
| Dokument rozliczenia | `Soneta.Kalend.RozliczenieCzasuPracy` (ctor `(Pracownik, DefinicjaRozliczeniaCzasuPracy)`) | root | `Data`, `Seria`, `Stan : StanyRozliczeniaCzasuPracy` |
| Pozycja rozliczenia | `Soneta.Kalend.ElementRozliczeniaCzasuPracy` (ctor `(RozliczenieCzasuPracy dokument)`) | child | `Definicja : DefinicjaStrefy`, `Data`, `OdGodziny`, `Czas`, `CzasPozostały`/`CzasDostępny`/`Zrealizowane` (kalk.) |

**`Soneta.Kalend.TypStrefy` (enum):** `NieWplywa`, `Zwieksza`, `Zmniejsza`.

**Workery (UI/extendery — praca zdalna/hybrydowa):**
- `Soneta.Kalend.StrefaPracy.PracaZdalnaWorker` (`Strefa : StrefaPracy`) — oznaczenie strefy jako
  praca zdalna.
- `Soneta.Kadry.PracaZdalna.DzienStrefaExtWorker`, `ElementRozliczeniaCzasuPracyExtWorker`,
  `DzienZestawienieExtender` — extendery zestawień/dni dla pracy zdalnej.

**Snippet (odczyt rozkładu na strefy + dokument rozliczenia):**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];

// Odczyt podziału dnia na strefy (stacjonarna / zdalna):
var dzien = pracownik.DniPracy[new Date(2026, 6, 1)];
if (dzien is not null)
{
    foreach (StrefaPracy s in dzien.Strefy)
    {
        DefinicjaStrefy def = s.Definicja;   // strefa (np. praca zdalna)
        Time rozliczany = s.CzasRozliczany;  // czas rozliczany w strefie
    }
}

// Pozycje podzielnika (elementy rozliczenia czasu pracy):
foreach (ElementRozliczeniaCzasuPracy el in pracownik.ElementyRozliczeniaCzasuPracy)
{
    DefinicjaStrefy def = el.Definicja;
    Time czas = el.Czas;
}
```

**Pułapki / wykonalność:**
- Rozkład pracy hybrydowej to **strefy** (`DzienPracy.Strefy` / `DefinicjaStrefy` z flagą zdalna) +
  dokument `RozliczenieCzasuPracy` z pozycjami `ElementRozliczeniaCzasuPracy` (podzielniki).
- `RozliczenieCzasuPracy` to **root** (ctor `(Pracownik, DefinicjaRozliczeniaCzasuPracy)`) — utworzenie
  wymaga istniejącej `DefinicjaRozliczeniaCzasuPracy` z konfiguracji; pozycje ctorem
  `new ElementRozliczeniaCzasuPracy(dokument)`. Czysty odczyt jest bezpieczny i bez transakcji.
- Aktualizacja podzielników na podstawie pracy hybrydowej przebiega **głównie przez extendery/UI**
  (`DzienStrefaExtWorker`, `ElementRozliczeniaCzasuPracyExtWorker`, `PracaZdalnaWorker`) zależne od
  `Context`/wniosków e-pracownika — brak prostego, czystego API operacyjnego.
- Wymaga skonfigurowanych `DefinicjaStrefy` (Praca_Zdalna / PracaZdalnaOkazjonalna) — w Demo strefy
  mogą nie być włączone, co czyni budowę rozliczenia kruchą do testu (raczej odczyt niż zapis).

