# KADRY05 — Plan pracy i kalendarz

> Wspólne fakty o typie, podstawowe typy i szablon wzorca: [../kadry.md](../kadry.md).

> **Model kalendarza pracownika.** Każdy `Pracownik` ma kalendarz roboczy
> (`pracownik.Etat.Kalendarz : Soneta.Kalend.Kalendarz`), którego dni leżą w tabeli
> `DniKalendarza` (`DzienKalendarzaBase`, child kalendarza). Pracownik wystawia trzy
> niezależne kolekcje dni typu `DateSubTable` (indeksator po dacie `[Date]`, **tylko do
> odczytu** — element tworzysz konstruktorem + `AddRow`):
> - `pracownik.DniPlanu : DateSubTable` — **plan/harmonogram** (dni `DzienPlanu : DzienKalendarzaBase`); to `pracownik.Etat.Kalendarz.Dni`.
> - `pracownik.DniPracy : DateSubTable<Soneta.Kalend.DzienPracy>` — **ewidencja** (realizacja) czasu pracy.
> - `pracownik.DniRCP : DateSubTable<Soneta.Kalend.DzienRCP>` — **zarejestrowany** czas pracy (RCP) — patrz sekcja F.
>
> Wszystkie dni współdzielą subrow `Praca : Soneta.Kalend.CzasPracy` z polami
> `OdGodziny`/`DoGodziny`/`Czas : Soneta.Types.Time`. Definicja dnia (`Definicja :
> Soneta.Kalend.DefinicjaDnia`) to rekord **konfiguracyjny** (słownik `DefinicjeDni`,
> indeksator `[Kod]`).
>
> **Ograniczenie wykonalności.** Plan i ewidencja są normalnie wyliczane przez kalkulator
> czasu pracy z definicji kalendarza/serii — ręczne tworzenie pojedynczego dnia jest możliwe
> publicznym kontraktem (ctor `(Pracownik, Date)` + `AddRow`), ale **wymaga zdefiniowanego
> `DefinicjaDnia` w konfiguracji**. Operacje masowe (przeliczenie planu na okres) są zaszyte
> w workerach/kalkulatorach UI — patrz KADRY-E2.

### KADRY-E1 — Wprowadzanie planowanego czasu pracy (★)

**Cel:** odczytać lub ustawić plan pracy (harmonogram) pracownika na konkretny dzień —
godziny od–do, normę dobową oraz typ dnia.

**Pola i typy:**

| Element | Lokalizacja | Typ | Uwaga |
|---|---|---|---|
| Plan pracy (cała kolekcja) | `pracownik.DniPlanu` | `Soneta.Business.DateSubTable` | == `pracownik.Etat.Kalendarz.Dni`; indeksator `[Date]` (get) |
| Dzień planu | `pracownik.DniPlanu[data]` | `Soneta.Kalend.DzienPlanu` (`DzienKalendarzaBase`) | `null`, gdy dla daty brak dnia planu |
| Data dnia | `DzienPlanu.Data` | `Soneta.Types.Date` | bazodanowe; ustawiane przez ctor |
| Godziny pracy (subrow) | `DzienPlanu.Praca` | `Soneta.Kalend.CzasPracy` | `Praca.OdGodziny`, `Praca.DoGodziny`, `Praca.Czas : Time` (zapisywalne) |
| Czas (norma dnia, odczyt) | `DzienPlanu.Czas` | `Soneta.Types.Time` | kalkulowane (czas pracy dnia) |
| Od (odczyt) | `DzienPlanu.OdGodziny` | `Soneta.Types.Time` | kalkulowane |
| Definicja dnia | `DzienPlanu.Definicja` | `Soneta.Kalend.DefinicjaDnia` | rekord słownika konfiguracyjnego `DefinicjeDni` |
| Tolerancja wejścia | `DzienPlanu.TolerancjaWe` | `Soneta.Types.Time` | bazodanowe |
| Norma dobowa kalendarza | `pracownik.Etat.Kalendarz.NormaDobowa` | `Soneta.Types.Time` | poziom kalendarza, nie dnia |
| Słownik definicji dni | `session.GetKalend().DefinicjeDni` | `DefinicjeDni` | indeksator `[kod: string]`; skróty `WolnaSobota`, `Niedziela` |

**Snippet:**

```csharp
var kalend = session.GetKalend();
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];

// --- Odczyt planu na dzień (bezpiecznie: indeksator zwraca null dla braku dnia) ---
var data = new Date(2026, 6, 1);
var dzienPlanu = (DzienPlanu)pracownik.DniPlanu[data];
if (dzienPlanu is not null)
{
    Time odGodz = dzienPlanu.Praca.OdGodziny;   // np. 8:00
    Time doGodz = dzienPlanu.Praca.DoGodziny;   // np. 16:00
    Time normaDnia = dzienPlanu.Czas;           // wyliczona norma dnia (kalkulowane)
    DefinicjaDnia typDnia = dzienPlanu.Definicja;
}

// --- Ustawienie/utworzenie dnia planu (wymaga DefinicjaDnia z konfiguracji) ---
using (var t = session.Logout(editMode: true))
{
    var dp = (DzienPlanu)pracownik.DniPlanu[data];
    if (dp is null)
    {
        dp = session.AddRow(new DzienPlanu(pracownik, data));   // ctor (Pracownik, Date)
        dp.Definicja = kalend.DefinicjeDni["RB"];               // typ dnia ze słownika (np. dzień roboczy)
    }
    dp.Praca.OdGodziny = new Time(8, 0);
    dp.Praca.DoGodziny = new Time(16, 0);   // Czas dnia wylicza się z od–do

    t.Commit();
}
session.Save();
```

**Pułapki:**
- `DniPlanu` to `DateSubTable` **nietypowany** (zwraca `Row`) — rzutuj na `DzienPlanu`. Indeksator
  `[Date]` jest **tylko do odczytu**: nowego dnia nie „przypiszesz", tworzysz go ctorem
  `new DzienPlanu(pracownik, data)` + `session.AddRow(...)`.
- Godziny ustawiasz na **subrowie** `Praca` (`dp.Praca.OdGodziny = …`), nie na `dp.OdGodziny` —
  to ostatnie jest kalkulowane (read-only). Po ustawieniu od–do `Praca.Czas`/`Czas` przeliczają się.
- `Definicja` to rekord **konfiguracyjnego** słownika `DefinicjeDni` — pobierz istniejący wpis
  (`kalend.DefinicjeDni[kod]`), nie twórz „w locie". Bez przypisanego `Definicja` świeży dzień planu
  może nie przejść weryfikatorów.
- Plan jest zwykle generowany przez kalkulator z definicji kalendarza (serie dni, święta) —
  ręczne nadpisywanie pojedynczego dnia to korekta, nie sposób budowy całego harmonogramu (do tego
  służy operacja seryjna / kopiowanie planu, KADRY-E2).
- Norma dobowa to atrybut **kalendarza** (`Etat.Kalendarz.NormaDobowa`), nie pojedynczego dnia.

### KADRY-E2 — Planowanie czasu pracy grupy (kopiowanie planu) (★)

**Cel:** skopiować wyliczony plan pracy (harmonogram) na wskazany okres — dla jednego pracownika
albo dla grupy, oraz seryjnie zaktualizować kalendarz pracowników (zmiana kalendarza docelowego).

**Publiczny kontrakt — dwie drogi:**

| Operacja | API | Charakter |
|---|---|---|
| Kopiowanie **planu** pracownika na okres | `Soneta.Kalend.KalendarzPlanuKopia.Kopiuj(Pracownik pracownik, FromTo okres)` (**public static**) | bez UI — proste API |
| Kopiowanie **pracy/realizacji** na okres | `Soneta.Kalend.KalendarzPracyKopia.Kopiuj(Pracownik pracownik, FromTo okres)` (**public static**) | bez UI — proste API |
| Kopiowanie grupy (worker UI) | `KalendarzPlanuKopia.KopiujWorker` / `KalendarzPracyKopia.KopiujWorker` | wymaga `Context` z zaznaczeniem |
| Aktualizacja kalendarza grupy | `Soneta.Kadry.AktualizujKalendarzWorker` | wymaga `Params` z `Context` |

**Worker `KopiujWorker` (BI/„Kopiuj plan…", „Kopiuj pracę…"):** klasa `ContextBase` z ctorem
`(Context context)`; pola `[Context] FromTo Okres`, `[Context] Pracownik[] Pracownicy`; metoda
`void Kopiuj()`. Działa **wyłącznie** z kontekstem UI (zaznaczona lista pracowników) i jest gardzona
licencją BI/BI_PL/PL oraz `IsVisibleKopiuj` (niedostępny na mobile).

**Worker `AktualizujKalendarzWorker`:** pola `[Context] Pracownik[] Pracownicy`,
`Params Pars` (`Pars.Data`, `Pars.TylkoOstatni: bool`, `Pars.PowodAktualizacji: string`,
`Pars.Kalendarze: KalendarzBase[]`, `Pars.Docelowy: Kalendarz`, `Pars.Zmiana: bool`,
`Pars.Interpretacja`), metoda `void Aktualizuj()`. `Params` to `ContextBase` (ctor `(Context)`).

**Snippet (proste API dla jednego pracownika — bez UI):**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
var okres = new FromTo(new Date(2026, 6, 1), new Date(2026, 6, 30));

using (var t = session.Logout(editMode: true))
{
    // Wylicza plan z kalendarza i zapisuje do kopii planu pracownika za wskazany okres:
    KalendarzPlanuKopia.Kopiuj(pracownik, okres);     // public static
    // analogicznie realizacja:  KalendarzPracyKopia.Kopiuj(pracownik, okres);
    t.Commit();
}
session.Save();
```

**Snippet (grupa — przez worker; wymaga Context z zaznaczeniem):**

```csharp
// Tylko w warstwie UI/Czynności — Context dostarcza zaznaczonych pracowników.
var worker = new KalendarzPlanuKopia.KopiujWorker(context)
{
    Okres = new FromTo(new Date(2026, 6, 1), new Date(2026, 6, 30)),
    Pracownicy = context.Get<Pracownik[]>()
};
worker.Kopiuj();   // wewnątrz: Session.Logout + Commit
```

**Pułapki:**
- **Kopiowanie grupy nie ma „czystego" API bezkontekstowego** — `KopiujWorker` i
  `AktualizujKalendarzWorker.Params` dziedziczą po `ContextBase` i wymagają `Context` (zaznaczenie z
  listy UI). Dla kodu serwerowego/testów używaj **publicznej statycznej** `KalendarzPlanuKopia.Kopiuj(pracownik, okres)`
  w pętli po pracownikach — to ona realizuje właściwą logikę (worker w `KopiujInt` woła ją per pracownik).
- `KopiujWorker.Kopiuj()` jest gardzony licencją (BI/BI_PL/PL) i `IsVisibleKopiuj` (m.in. blokada na
  mobile) — to logika UI, nie wywołuj jej z kodu biznesowego.
- Kopia planu/pracy trafia do **osobnych** kolekcji `pracownik.DniPlanuKopia`/`pracownik.DniPracyKopia`
  (`DateSubTable`), powiązanych z `KalendarzPlanuKopia`/`KalendarzPracyKopia` — to bufor kopii, odrębny
  od właściwego `DniPlanu`/`DniPracy`.
- `okres` jest normalizowany przez setter workera do pełnych miesięcy (otwarty `From`/`To` →
  pierwszy/ostatni dzień miesiąca); przy statycznym `Kopiuj` podawaj zamknięty `FromTo`.
- Operacja seryjna na grupie pracowników = długa transakcja → dziel na paczki, trzymaj transakcje
  krótkie (safe-code §13.1).

### KADRY-E3 — Aktualizacja kalendarza pracownika (operacja seryjna „Zaktualizuj kalendarz pracownika")

**Cel:** seryjnie zmienić kalendarz roboczy zaznaczonych pracowników (zmiana kalendarza
docelowego, przeliczenie planu na nowy kalendarz od wskazanej daty) — operacja z menu
„Czynności" na liście pracowników.

**Publiczny kontrakt — worker `Soneta.Kadry.AktualizujKalendarzWorker`:**

| Element | Sygnatura / typ | Uwaga |
|---|---|---|
| Konstruktor | `new AktualizujKalendarzWorker()` | bezparametrowy; worker UI |
| Pracownicy (wejście) | `Pracownicy : Pracownik[]` | **set-only**; karmione z `Context` (zaznaczenie listy) |
| Parametry | `Pars : Params` | **set-only**; `Params` to `ContextBase`, ctor `(Context context)` |
| Wykonanie | `void Aktualizuj()` | właściwa operacja seryjna (Logout + Commit wewnątrz) |

**`Soneta.Kadry.AktualizujKalendarzWorker.Params` (`: ContextBase`, ctor `(Context)`):**

| Pole | Typ | Uwaga |
|---|---|---|
| `Data` | `Soneta.Types.Date` | data, od której obowiązuje nowy kalendarz |
| `TylkoOstatni` | `bool` | aktualizuj tylko ostatni (bieżący) zapis historyczny |
| `PowodAktualizacji` | `string` | opis powodu (do dokumentu aktualizacji) |
| `Kalendarze` | `KalendarzBase[]` | kalendarze źródłowe objęte zmianą; lista przez `GetListKalendarze()` |
| `Docelowy` | `Soneta.Kalend.Kalendarz` | kalendarz docelowy; lista przez `GetListDocelowy()` |
| `Zmiana` | `bool` | flaga: czy zmienić kalendarz (a nie tylko przeliczyć) |
| `Interpretacja` | `Soneta.Kadry.InterpretacjaKalendarza` | `WgPlanu` / `WgObecnosci` / `WgZestawien`; `IsReadOnlyInterpretacja()` |

**Snippet (warstwa UI/Czynności — wymaga `Context` z zaznaczeniem):**

```csharp
// Tylko w warstwie UI: Context dostarcza zaznaczonych pracowników.
var worker = new AktualizujKalendarzWorker
{
    Pracownicy = context.Get<Pracownik[]>(),
    Pars = new AktualizujKalendarzWorker.Params(context)
    {
        Data = new Date(2026, 7, 1),
        Docelowy = session.GetKalend().Kalendarze.WgKodu["PODSTAWOWY"],
        Zmiana = true,
        Interpretacja = InterpretacjaKalendarza.WgPlanu,
        PowodAktualizacji = "Zmiana systemu czasu pracy"
    }
};
worker.Aktualizuj();   // wewnątrz: Session.Logout + Commit
```

**Pułapki:**
- `Params` dziedziczy po `ContextBase` (ctor `(Context)`) — **nie da się go zbudować bez `Context`**.
  Dlatego KADRY-E3 nie ma „czystego" API bezkontekstowego; to operacja UI/serwerowa z zaznaczeniem.
- `Pracownicy` i `Pars` są **set-only** — nie odczytasz ich z powrotem; ustaw przed `Aktualizuj()`.
- Operacja seryjna = długa transakcja na wielu pracownikach → w realnym użyciu dziel na paczki
  (safe-code §13.1). Sam worker zarządza transakcją wewnętrznie.
- Zmiana kalendarza jest **historyczna** (operuje na zapisach `Etat`) — `TylkoOstatni`/`Data`
  decydują, których zapisów historycznych dotyczy.

---

### KADRY-E4 — Uzgodnienie doby pracowniczej (model doby; godziny rozpoczęcia doby)

**Cel:** przesunąć granicę doby pracowniczej dla dnia ewidencji — gdy zmiana zaczyna się w jednej
dobie kalendarzowej, a kończy w następnej (nocna), uzgodnienie „przenosi" początek/koniec pracy do
właściwej doby pracowniczej. Operacja na pojedynczym dniu (`DzienPracy`) lub seryjnie na grupie.

**Model doby (publiczny kontrakt):**

| Element | Lokalizacja | Typ | Uwaga |
|---|---|---|---|
| Początek doby w niedziele/święta | `pracownik.Last.Etat.ConfigPoczątekDobyNiedzieledIŚwięta` | `Soneta.Types.Time` | **read-only** (konfiguracyjne); godzina startu doby |
| Norma dobowa | `pracownik.Last.Etat.NormaDobowa` | `Soneta.Types.Time` | bazodanowe; norma czasu doby |
| Norma dobowa kalendarza | `pracownik.Last.Etat.Kalendarz.NormaDobowa` | `Soneta.Types.Time` | poziom kalendarza |
| Interpretacja kalendarza | `pracownik.Last.Etat.InterpretacjaKalendarza` | `Soneta.Kadry.InterpretacjaKalendarza` | `WgPlanu`/`WgObecnosci`/`WgZestawien` — jak interpretować dobę |

> **Uwaga:** `Etat` leży na bieżącym **zapisie historycznym** (`pracownik.Last.Etat : Soneta.Kadry.Etat`,
> gdzie `Last : PracHistoria`) — nie ma property `pracownik.Etat` bezpośrednio na roocie pracownika.
| Godziny pracy dnia | `DzienPracy.Praca` | `Soneta.Kalend.CzasPracy` | `OdGodziny`/`DoGodziny`/`Czas` — granice realizacji w dobie |

**Worker pojedynczego dnia — `Soneta.Kalend.DzienPracy.UzgodnijDobePracowniczaWorker`:**

| Element | Sygnatura | Uwaga |
|---|---|---|
| Konstruktor | `new DzienPracy.UzgodnijDobePracowniczaWorker()` | |
| Dzień (wejście) | `Dzień : DzienPracy` | **set-only** |
| Warunek dostępności | `static bool IsEnabledUzgodnijDobePracownicza(DzienPracy dzień)` | czy operacja ma sens dla dnia |
| Uzgodnienie | `object UzgodnijDobePracownicza()` | przelicza dobę |
| Przeniesienie początku | `DzienPracy PrzenieśPoczątek()` | przenosi początek pracy do poprz. doby |
| Przeniesienie końca | `DzienPracy PrzenieśKoniec()` | przenosi koniec pracy do nast. doby |
| Dokument aktualizacji | `DokumentAktualizacjiKalendarza : IDokumentAktualizacjiKalendarza`, `DataAktualizacji : System.DateTime` | kontekst historii |

**Worker seryjny (grupa) — `Soneta.Kadry.UzgodnijDobePracowniczaPracownikowWorker`:**

| Element | Sygnatura / typ | Uwaga |
|---|---|---|
| Konstruktor | `new UzgodnijDobePracowniczaPracownikowWorker()` | |
| Pracownicy | `Pracownicy : Pracownik[]` | **set-only**; z `Context` |
| Parametry | `Pars : Params` (`ContextBase`, ctor `(Context)`); pole `Okres : FromTo` | **set-only** |
| Wykonanie | `UzgodnijDobePracowniczaResult UzgodnijDobePracownicza()` | zwraca wynik |

**Snippet (pojedynczy dzień):**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
var dzien = pracownik.DniPracy[new Date(2026, 6, 1)];   // DzienPracy lub null
if (dzien is not null && DzienPracy.UzgodnijDobePracowniczaWorker.IsEnabledUzgodnijDobePracownicza(dzien))
{
    using (var t = session.Logout(editMode: true))
    {
        var worker = new DzienPracy.UzgodnijDobePracowniczaWorker { Dzień = dzien };
        worker.UzgodnijDobePracownicza();
        t.Commit();
    }
    session.Save();
}
```

**Pułapki:**
- Godzina rozpoczęcia doby to atrybut **konfiguracyjny `Etat`** (`ConfigPoczątekDobyNiedzieledIŚwięta`,
  read-only) i normy `Etat.NormaDobowa`/`Etat.Kalendarz.NormaDobowa` — nie ma osobnego, edytowalnego
  pola „początek doby" na pojedynczym `DzienPracy`.
- `Dzień` workera pojedynczego jest **set-only**; `Pracownicy`/`Pars` workera grupowego również.
- Worker grupowy `Params` to `ContextBase` (ctor `(Context)`) — **wymaga `Context`** (zaznaczenie UI),
  brak czystego API bezkontekstowego.
- Uzgodnienie modyfikuje `DzienPracy.Praca` (od–do) i może rozbić pracę na dwie doby — wykonuj w
  transakcji (`Logout(editMode:true)` + `Commit`) i zapisz `Save()`.

---

### KADRY-E5 — Odczyt normy czasu pracy i czasu przepracowanego za okres (★ testowalne)

**Cel:** dla pracownika odczytać za zadany okres (`FromTo`/`YearMonth`): normę czasu pracy
(planowaną), czas przepracowany (zrealizowany), nadgodziny, czas nocny, liczbę/normę nieobecności —
bez modyfikacji danych (czysty odczyt statystyk).

**Punkt wejścia — `pracownik.Czasy : Soneta.Kalend.KalkulatorPracownika`:**

| Metoda (publiczna, instancyjna) | Zwraca | Znaczenie |
|---|---|---|
| `Norma(FromTo okres, params Item[] condition)` | `CzasDni` | norma (planowana) czasu pracy za okres |
| `Norma(FromTo okres, DefinicjaStrefy def, params Item[] condition)` | `CzasDni` | norma w obrębie strefy |
| `NormaKodeksowa(YearMonth miesiąc)` | `CzasDni` | norma kodeksowa miesiąca (pełny etat) |
| `NormaKodeksowaWym(Fraction wymiar, Time normaDobowa, YearMonth miesiąc)` | `CzasDni` | norma kodeksowa wg wymiaru etatu |
| `Praca(FromTo okres, params Item[] condition)` | `CzasDni` | czas **przepracowany** (zrealizowany) za okres |
| `Praca(FromTo okres, DefinicjaStrefy def, params Item[] condition)` | `CzasDni` | przepracowany w obrębie strefy |
| `PracaRozliczana(FromTo okres, params Item[] condition)` | `CzasDni` | czas pracy rozliczany (do nadgodzin) |
| `PracaZatr(FromTo okres, bool usprPłatne)` | `CzasDni` | praca w okresie zatrudnienia |
| `Nadgodziny(YearMonth okres)` / `Nadgodziny(FromTo okres)` | `ZestawienieNadgodzin` | nadgodziny |
| `NadgodzinyDobaOkres(FromTo okres)` | `ZestawienieNadgodzin` | nadgodziny dobowe/okresowe |
| `Nocne(YearMonth\|FromTo okres)` | `Time` | czas nocny |
| `NormaNie(YearMonth\|FromTo okres, params Item[] condition)` | `CzasDni` | norma nieobecności |
| `DniNie(YearMonth\|FromTo okres, params Item[] condition)` | `int` | liczba dni nieobecności |
| `Nieobecność(Date data[, bool clip])` | `INieobecnosc` | nieobecność w danym dniu |

**`Soneta.Kalend.CzasDni` (typ wyniku):**

| Pole | Typ | Uwaga |
|---|---|---|
| `Czas` | `Soneta.Types.Time` | sumaryczny czas (read-only) |
| `Dni` | `int` | liczba dni (read-only) |
| `CzasDni.Empty`, `CzasDni.Invalid` | `CzasDni` | wartości specjalne; operatory `+`/`-`/`==` |

**`Soneta.Kalend.ZestawienieNadgodzin` (struct):** `N50`, `N100`, `NSW`, `N100Doba`, `N100Okres`,
`Razem` — wszystkie `Time` (read-only); `ZestawienieNadgodzin.Zero`.

**Snippet (czysty odczyt):**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
var kalk = pracownik.Czasy;                                  // KalkulatorPracownika
var okres = new FromTo(new Date(2026, 6, 1), new Date(2026, 6, 30));

CzasDni norma = kalk.Norma(okres);                           // norma planowana
CzasDni przepracowano = kalk.Praca(okres);                   // czas zrealizowany
ZestawienieNadgodzin nadg = kalk.Nadgodziny(new YearMonth(2026, 6));
Time nocne = kalk.Nocne(okres);

Time normaCzas = norma.Czas;          int normaDni = norma.Dni;
Time pracaCzas = przepracowano.Czas;  Time nadgRazem = nadg.Razem;
```

**Pułapki:**
- `KalkulatorPracownika` **nie jest `Row`** — to obiekt liczący (zwykły `object`). Nie zapisuje się,
  nie wymaga transakcji; to czysty odczyt. Pobieraj go zawsze przez `pracownik.Czasy` (ma kontekst
  pracownika), nie twórz ręcznie ctorem chyba że masz `Pracownik` + ewentualny `Log`.
- Parametr `condition` to **serwerowy filtr** (`Item[]`, RowCondition) — można zawęzić np. do strefy;
  zwykle pusty.
- `Norma` = plan, `Praca` = realizacja; nie myl `Praca(okres)` (statystyka) z `DzienPracy` (rekord dnia).
- Wynik `CzasDni.Invalid` sygnalizuje brak danych/błąd okresu — sprawdzaj zanim policzysz różnice.

---

