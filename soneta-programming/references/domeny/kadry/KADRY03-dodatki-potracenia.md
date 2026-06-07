# KADRY03 — Dodatki, potrącenia, akordy

> Wspólne fakty o typie, podstawowe typy i szablon wzorca: [../kadry.md](../kadry.md).

### KADRY-C1 — Dodatki / stałe elementy wynagrodzenia (★)

**Cel:** przypisać pracownikowi stały element wynagrodzenia (dodatek — np. premia, dodatek
funkcyjny), oparty o definicję elementu płacowego, z okresem obowiązywania i parametrami
(podstawa/procent/czas). W UI: menu czynności *Dodatki i potrącenia → Dodaj nowy*.

**Klasa i model:** `Soneta.Kadry.Dodatek` — `GuidedRow` root, tabela `Dodatki`, obiekt
**historyczny** (kolekcja `Dodatek.Historia: HistorySubTable<Soneta.Kadry.DodHistoria>`, parametry
„od–do" siedzą w zapisach `DodHistoria`). Dodatek jest childem pracownika i pojawia się w
`pracownik.Dodatki: SubTable<Soneta.Kadry.Dodatek>`.

**Tworzenie:** `new Dodatek(pracownik)` + `session.GetKadry().Dodatki.AddRow(d)`. Dodanie do tabeli
tworzy **pierwszy zapis** `DodHistoria` — dostępny od razu jako `d.Last`. Parametry ustawiamy na
`d.Last`.

**Pola i typy (`DodHistoria` — `d.Last`):**

| Pole | Typ | Uwaga |
|---|---|---|
| `Element` | `Soneta.Place.DefinicjaElementu` | definicja elementu wynagrodzenia (wymagana); pobierz istniejącą z `session.GetPlace().DefElementow.WgNazwy[nazwa]` |
| `Okres` | `Soneta.Types.FromTo` | okres obowiązywania dodatku |
| `Podstawa` | `Soneta.Types.Currency` | kwota podstawy (gdy algorytm definicji jej wymaga) |
| `Procent` | `Soneta.Types.Percent` | procent (gdy algorytm definicji go wymaga) |
| `Czas` | `Soneta.Types.Time` | czas (gdy algorytm definicji go wymaga) |
| `Ulamek` | `Soneta.Types.Fraction` | ułamek (zależnie od definicji) |
| `Dni` | `int` | liczba dni (zależnie od definicji) |
| `Aktualnosc` | `Soneta.Types.FromTo` | okres zapisu historycznego (zarządzany przez historię — nie ustawiaj ręcznie) |

**Pola na rootcie `Dodatek`:** `Nazwa: string`, `Pracownik: Soneta.Kadry.Pracownik` (właściciel,
ustawiany ctorem), `DataZakonczeniaWyplaty: Date`, `Last: DodHistoria`,
`Historia: HistorySubTable<DodHistoria>`, `Dodatki` (tabela: `session.GetKadry().Dodatki`).

**Pobranie definicji elementu:** słownik `session.GetPlace().DefElementow` (kolekcja konfiguracyjna).
Indeksowanie po nazwie: `DefElementow.WgNazwy["Premia"]`. Definicje dodatków mają
`RodzajZrodla == Soneta.Place.RodzajŹródłaWypłaty.Dodatek` — można nimi filtrować dostępne
definicje. W bazie Demo istnieją gotowe definicje, m.in. `"Premia"`, `"Premia procentowa"`.

**Pułapki:**
- **`new Dodatek(pracownik)` + `Dodatki.AddRow(d)` to para** — sam konstruktor nie włącza dodatku do
  sesji ani nie tworzy zapisu historii. Pierwszy `DodHistoria` powstaje przy `AddRow`; dopiero potem
  istnieje `d.Last`.
- `Podstawa`/`Procent`/`Czas` **mogą być tylko-do-odczytu** w zależności od algorytmu wskazanej
  `DefinicjaElementu` — element kwotowy udostępnia `Podstawa`, element procentowy `Procent` itd.
  Ustawiaj tylko te pola, których wymaga definicja (próba zapisu pola read-only rzuci wyjątek).
- `Element` jest **wymagany** — bez wskazania definicji elementu dodatek nie ma sensu płacowego.
  Definicję pobierasz z istniejącego słownika (`DefElementow`), nie tworzysz „w locie" w tym scenariuszu.
- Zmiana parametrów dodatku **od konkretnego dnia** to nowy zapis historii dodatku
  (`d.Historia.Update(date)` + `Dodatki.Module.DodHistorie.AddRow(nowy)`), analogicznie do KADRY-A14 — nie
  nadpisuj bieżącego zapisu, jeśli chcesz zachować poprzedni okres.
- `DodHistoria.Aktualnosc` (okres zapisu) zarządza mechanizm historii — sam ustawiasz `Okres`,
  `Aktualnosc` zostaw historii.

**Snippet:**

```csharp
var kadry = session.GetKadry();
var pracownik = kadry.Pracownicy.WgKodu["006"];

// Definicja elementu wynagrodzenia ze słownika konfiguracyjnego (po nazwie):
var definicjaPremii = session.GetPlace().DefElementow.WgNazwy["Premia"];

using (var t = session.Logout(editMode: true))
{
    // new Dodatek(pracownik) + AddRow — AddRow tworzy pierwszy zapis DodHistoria (d.Last):
    var dodatek = new Dodatek(pracownik);
    kadry.Dodatki.AddRow(dodatek);

    var h = dodatek.Last;                                   // pierwszy zapis historii dodatku
    h.Element  = definicjaPremii;                          // definicja elementu (wymagana)
    h.Okres    = new FromTo(new Date(2026, 1, 1), Date.MaxValue);
    h.Podstawa = (Currency)500m;                           // gdy algorytm definicji wymaga podstawy

    t.Commit();
}
session.Save();

// Odczyt dodatków pracownika i ich definicji elementu (kolekcja childów):
foreach (Dodatek d in pracownik.Dodatki)
{
    DefinicjaElementu element = d.Last.Element;
    FromTo okres = d.Last.Okres;
}
```

### KADRY-C2 — Potrącenia (stałe i jednorazowe) (★)

**Cel:** przypisać pracownikowi potrącenie z wynagrodzenia (np. składka związkowa, spłata
rozliczana ręcznie, potrącenie dobrowolne). W modelu płacowym **potrącenie nie ma osobnej klasy** —
to **`Soneta.Kadry.Dodatek`** (jak KADRY-C1), tyle że oparty o **definicję elementu o charakterze
potrącenia**. O „minusowym" charakterze decyduje wyłącznie wskazana `DefinicjaElementu` (jej algorytm),
a nie typ obiektu po stronie pracownika.

**Jak rozpoznać definicję potrącenia (`Soneta.Place.DefinicjaElementu`, słownik `DefElementow`):**

| Pole definicji | Typ | Znaczenie dla potrącenia |
|---|---|---|
| `Algorytm.Potracenie` | `bool` | **kluczowy znacznik** — `true` dla elementu potrącającego (element pomniejsza wynagrodzenie) |
| `Algorytm.LimitPotracenia` | `Soneta.Place.TypLimituPotrącenia` | rodzaj limitu (np. do kwoty wolnej) — gdy potrącenie limitowane |
| `Algorytm.TylkoPelnePotracenie` | `bool` | potrącać tylko w pełnej kwocie (bez częściowego) |
| `RodzajZrodla` | `Soneta.Place.RodzajŹródłaWypłaty` | dla potrącenia przez dodatek **musi być** `Dodatek` (= `6`); enum **nie ma** wartości „Potrącenie" (ma natomiast m.in. `ZajęcieKomornicze` = 23, `Świadczenie` = 12, `Pożyczka` = 18, `PożyczkaSpłata` = 19). Minus realizuje algorytm, ale `DodHistoria.Element` **odrzuca** definicje o `RodzajZrodla != Dodatek` (np. „Alimenty" jako `ZajęcieKomornicze`) — patrz pułapki |

**Mechanizm — identyczny jak KADRY-C1 (Dodatek + DodHistoria):**
- `new Dodatek(pracownik)` + `session.GetKadry().Dodatki.AddRow(d)` → powstaje pierwszy `DodHistoria`
  (`d.Last`).
- Na `d.Last` ustawiamy `Element` (definicja potrącenia), `Okres` oraz `Podstawa`/`Procent`/`Kwota`
  zależnie od algorytmu definicji.
- **Potrącenie stałe**: `Okres` otwarty (do `Date.MaxValue`) lub na czas określony — naliczane w każdej
  wypłacie z okresu.
- **Potrącenie jednorazowe**: `Okres` zawężony do jednego miesiąca rozliczeniowego (tylko ten miesiąc
  obejmie naliczenie).
- Zakończenie potrącenia: `d.DataZakonczeniaWyplaty` + ewentualnie `d.PrzyczynaZakonczenia`, albo nowy
  zapis historii „od daty" (`d.Historia.Update(date)`), analogicznie do KADRY-C1/KADRY-A14.

**Pułapki:**
- Nie szukaj klasy „Potrącenie" — jej **nie ma**. Potrącenie = `Dodatek` z definicją, w której
  `Algorytm.Potracenie == true`. Dobór definicji jest jedynym wyróżnikiem.
- **Filtruj po DWÓCH warunkach** (zweryfikowane testem): `d.Algorytm.Potracenie && d.RodzajZrodla ==
  RodzajŹródłaWypłaty.Dodatek`. Sam `Algorytm.Potracenie` **nie wystarcza** — przy ustawianiu
  `DodHistoria.Element` definicja o innym `RodzajZrodla` (np. „Alimenty" jako `ZajęcieKomornicze`)
  rzuca `System.Exception: "Zły rodzaj źródła wypłaty elementu …"`. Element zajęcia komorniczego ma
  `RodzajZrodla == ZajęcieKomornicze` i podpinasz go pod `ZajęcieKomornicze`, nie pod `Dodatek` (KADRY-C4).
- `Podstawa`/`Procent`/`Czas` na `DodHistoria` bywają tylko-do-odczytu zależnie od algorytmu definicji
  (jak w KADRY-C1) — ustawiaj tylko te, których definicja wymaga.
- `Element` wymagany; pobierany z istniejącego słownika `DefElementow`, nie tworzony „w locie".

**Snippet:**

```csharp
var kadry = session.GetKadry();
var pracownik = kadry.Pracownicy.WgKodu["006"];

// Definicja potrącenia ze słownika — DWA warunki: Potracenie ORAZ RodzajZrodla == Dodatek:
var def = session.GetPlace().DefElementow.Cast<DefinicjaElementu>()
    .First(d => d.RodzajZrodla == RodzajŹródłaWypłaty.Dodatek
                && d.Algorytm != null && d.Algorytm.Potracenie);

using (var t = session.Logout(editMode: true))
{
    var potracenie = new Dodatek(pracownik);
    kadry.Dodatki.AddRow(potracenie);                       // tworzy pierwszy DodHistoria

    var h = potracenie.Last;
    h.Element = def;                                        // definicja o Algorytm.Potracenie == true
    h.Okres   = new FromTo(new Date(2026, 1, 1), Date.MaxValue);   // stałe
    h.Podstawa = (Currency)50m;                             // gdy algorytm definicji wymaga kwoty

    t.Commit();
}
session.Save();
```

---

### KADRY-C3 — Akordy (★)

**Cel:** przypisać pracownikowi pracę akordową (rozliczaną wg ilości/strefy), z okresem i definicją
akordu; zakończyć akord. W UI: menu czynności *Akordy → Dodaj nowy / Zakończ*.

**Klasa i model:** `Soneta.Kadry.Akord` — `GuidedRow` root, tabela `Akordy`, obiekt **historyczny**
(`Akord.Historia: HistorySubTable<Soneta.Kadry.AkordHistoria>`; parametry „od–do" w zapisach
`AkordHistoria`, dostęp do bieżącego przez `Akord.Last`). Akord jest childem pracownika:
`pracownik.Akordy: SubTable<Soneta.Kadry.Akord>`.

**Pola root `Akord`:**

| Pole | Typ | Uwaga |
|---|---|---|
| `Pracownik` | `Soneta.Kadry.Pracownik` | właściciel (relacja) |
| `Definicja` | `Soneta.Kadry.DefinicjaAkordu` | definicja akordu (słownik `DefinicjeAkordow`) |
| `Okres` | `Soneta.Types.FromTo` | okres obowiązywania akordu |
| `Typ` | `Soneta.Kadry.TypAkordu` | typ akordu |
| `Wydzial` | `Soneta.Kadry.Wydzial` | jednostka organizacyjna realizacji |
| `Last` | `Soneta.Kadry.AkordHistoria` | bieżący zapis historii |
| `Dni` | `DateSubTable<Soneta.Kalend.DzienAkorduBase>` | dzienna realizacja akordu |

**Pola `AkordHistoria` (`Akord.Last`):** `Okres: FromTo`, `Algorytm: Soneta.Kadry.AlgorytmAkordu`
(subrow z `Algorytm.Element: DefinicjaElementu`, `Algorytm.Wspolczynnik`, `Algorytm.Progi`,
`Algorytm.WgCzasu`/`Progresywny` itd.), `Jednostka: string`, `Aktualnosc: FromTo` (zarządzane przez
historię), `Progi: SubTable<Soneta.Kadry.ProgAkordu>`.

**Tworzenie — brak publicznego konstruktora `Akord(pracownik)`.** Akord dodaje się **workerem**
operacyjnym (kanonicznie), nie `new`. Konstruktor `Akord` jest niepubliczny (poza `RowCreator`).
Worker jest „jak z UI" (`Params` dziedziczy z `ContextBase`, ctor wymaga `Context`) — uruchamiaj go w
transakcji `CommitUI`.

**Workery (zagnieżdżone w `Pracownik`):** ctor `(Session)`, parametry przez właściwości `Pars`/`Pracownicy`;
`Params` ma ctor `(Context)`.

| Worker | Metoda | Wzorzec użycia |
|---|---|---|
| `Soneta.Kadry.Pracownik.DodajAkordWorker` | `DodajAkord` | `new Params(ctx) { Definicja, OdDnia, DoDnia, DodajKolejny }`; `new DodajAkordWorker(session) { Pars = par, Pracownicy = tab }` |
| `Soneta.Kadry.Pracownik.ZakończAkordWorker` | `ZakończAkord` | `new Params(ctx) { Definicja, DoDnia, ZakończWszystkie }`; `new ZakończAkordWorker(session) { Pars = par, Pracownicy = tab }` |

**Pułapki:**
- Akordu **nie twórz przez `new Akord(...)`** — kanoniczna ścieżka to `DodajAkordWorker` (analogicznie
  `ZakończAkordWorker` do zakończenia). Workery przyjmują **tablicę pracowników**, więc nadają się też do
  operacji grupowej.
- `Definicja` (akordu) to rekord słownika `DefinicjeAkordow` — pobierz istniejący, nie twórz „w locie".
  Sam akord wiąże dopiero z `DefinicjaElementu` (płacowym) przez `Algorytm.Element` definicji akordu.
- Akord jest historyczny — zmiana parametrów „od daty" to nowy zapis `AkordHistoria`
  (`Historia.Update(date)`), analogicznie do KADRY-C1/KADRY-A14.
- Tabela `Akordy` to dane operacyjne — przy przeglądaniu poprzecznym filtruj zakresem (safe-code §6.3);
  w zakresie jednego pracownika korzystaj z `pracownik.Akordy`.

**Snippet:**

```csharp
var kadry = session.GetKadry();
var pracownik = kadry.Pracownicy.WgKodu["006"];
var defAkordu = kadry.DefinicjeAkordow.WgNazwa["Akord prosty"];   // klucz WgNazwa (l.poj.)
var context = login.CreateEmptyContext().Clone(session);

using (var t = session.Logout(editMode: true))
{
    var par = new Pracownik.DodajAkordWorker.Params(context)   // Params: ctor (Context)
    {
        Definicja = defAkordu,
        OdDnia    = new Date(2026, 1, 1),
        DoDnia    = new Date(2026, 12, 31),
    };
    // ctor (Session); parametry przez właściwości Pars/Pracownicy:
    new Pracownik.DodajAkordWorker(session) { Pars = par, Pracownicy = new[] { pracownik } }.DodajAkord();
    t.CommitUI();
}
session.Save();

// Odczyt akordów pracownika:
foreach (Akord a in pracownik.Akordy)
{
    DefinicjaAkordu def = a.Definicja;
    FromTo okres = a.Okres;
    DefinicjaElementu element = a.Last.Algorytm.Element;
}
```

---

### KADRY-C4 — Zajęcia wynagrodzenia (komornicze, alimentacyjne) (★)

**Cel:** zarejestrować zajęcie wynagrodzenia (egzekucja komornicza lub alimentacyjna) z numerem sprawy,
kwotą, priorytetem i wierzycielem (komornikiem/rachunkiem odbiorcy); anulować/przywrócić zajęcie.

**Klasa i model:** `Soneta.Kadry.ZajęcieKomornicze` — `GuidedRow` root, tabela `ZajKomornicze`, obiekt
**historyczny** (`Historia: HistorySubTable<Soneta.Kadry.ZajęcieKomorniczeHistoria>`; limity i kwoty
„od–do" w zapisach historii, bieżący przez `Last`). Child pracownika:
`pracownik.ZajęciaKomornicze: SubTable<Soneta.Kadry.ZajęcieKomornicze>`. **Konstruktor publiczny:**
`new ZajęcieKomornicze(pracownik)`.

**Pola root `ZajęcieKomornicze`:**

| Pole | Typ | Uwaga |
|---|---|---|
| `Pracownik` | `Soneta.Kadry.Pracownik` | właściciel |
| `Rodzaj` | `Soneta.Kadry.RodzajeZajęciaWynagrodzenia` | enum: `Kwota = 0`, `KwotaMiesięczna = 1` (jednorazowa kwota vs miesięczna) |
| `Element` | `Soneta.Place.DefinicjaElementu` | element płacowy zajęcia — **wymagany**; musi mieć `RodzajZrodla == ZajęcieKomornicze` (= 23) |
| `NumerSprawy` | `string` | numer sprawy egzekucyjnej |
| `Data` | `Soneta.Types.Date` | data zajęcia |
| `DataSplaty` | `Soneta.Types.Date` | data spłaty/zakończenia |
| `Rozliczenie.Odbiorca` | `Soneta.Kasa.IPodmiotKasowy` | **wierzyciel** — komornik/odbiorca (iface; może być `Kontrahent`, `Bank`, `Pracownik`, `UrzadSkarbowy`…) |
| `Rozliczenie.RachunekOdbiorcy` | `Soneta.Kasa.RachunekBankowyPodmiotu` | rachunek wierzyciela do przelewu |
| `Splacono` | `Soneta.Types.Currency` | kwota spłacona (kalkulowane/narastające) |
| `Pozostało` | `Soneta.Types.Currency` | kwota pozostała (kalkulowane) |
| `SplataZakonczona` | `bool` | spłata zakończona |
| `Anulowane` | `bool` | zajęcie anulowane (patrz workery) |
| `Korekty` | `SubTable<Soneta.Kadry.KorektaZajęciaKomorniczego>` | korekty zajęcia |
| `OpisPrzelewu` | `string` | tytuł przelewu |

**Limity i kwoty — na zapisie `ZajęcieKomorniczeHistoria` (`Last`):** kwota do potrącenia, limity
procentowe i kwotowe, zawieszenie spłaty, priorytet, ustawienia potrąceń z zasiłków/świadczeń (zmiana
„od daty" = nowy zapis historii).

**Workery (zagnieżdżone w `ZajęcieKomornicze`):** ctor **bezparametrowy**, parametr przez właściwość `Zajęcie`.

| Worker | Metoda | Wzorzec użycia |
|---|---|---|
| `Soneta.Kadry.ZajęcieKomornicze.AnulujWorker` | `Anuluj` | `new ZajęcieKomornicze.AnulujWorker { Zajęcie = zaj }.Anuluj()` |
| `Soneta.Kadry.ZajęcieKomornicze.PrzywrócWorker` | `Przywróć` | `new ZajęcieKomornicze.PrzywrócWorker { Zajęcie = zaj }.Przywróć()` |

**Pułapki:**
- **Pole `Priorytet` NIE istnieje** na `ZajęcieKomornicze` (sprostowanie). **Alimentacyjne vs
  niealimentacyjne** rozstrzyga konfiguracja: wskazana `DefinicjaElementu` (`RodzajZrodla ==
  ZajęcieKomornicze`) i parametry zapisu historii (limity), nie osobny typ klasy — to **jedna klasa**
  `ZajęcieKomornicze`.
- `Anulowane` jest **tylko-do-odczytu** (brak publicznego settera) — anuluj **workerem** `AnulujWorker`.
- `Rozliczenie.Odbiorca` jest **interfejsem** `IPodmiotKasowy` — wskaż istniejący podmiot (zwykle
  `Kontrahent`-komornik); nie twórz odbiorcy „w locie" w tym scenariuszu.
- Faktyczne **kwoty potrącenia (`Splacono`, `Pozostało`) wyliczają się przy naliczeniu wypłaty** — po
  samym dodaniu zajęcia są zerowe/wyjściowe. Pełne rozliczenie wymaga naliczonej wypłaty (patrz sekcja
  „niewykonalne publicznym API bez naliczenia").
- Anulowanie/przywracanie realizuj **workerami** (`AnulujWorker`/`PrzywrócWorker`), nie ręcznym
  ustawianiem `Anulowane` — workery dbają o storna i spójność rozliczenia.
- Tabela operacyjna — przegląd poprzeczny z filtrem (safe-code §6.3).

**Snippet:**

```csharp
var kadry = session.GetKadry();
var pracownik = kadry.Pracownicy.WgKodu["006"];
// Element zajęcia — definicja o RodzajZrodla == ZajęcieKomornicze (nie zwykłe potrącenie-Dodatek):
var elementZajecia = session.GetPlace().DefElementow.Cast<DefinicjaElementu>()
    .First(d => d.RodzajZrodla == RodzajŹródłaWypłaty.ZajęcieKomornicze);
var komornik = session.GetCRM().Kontrahenci.WgKodu["KOMORNIK1"];   // wierzyciel (IPodmiotKasowy)

using (var t = session.Logout(editMode: true))
{
    var zajecie = new ZajęcieKomornicze(pracownik);      // ctor publiczny
    kadry.ZajKomornicze.AddRow(zajecie);

    zajecie.Rodzaj     = RodzajeZajęciaWynagrodzenia.KwotaMiesięczna;
    zajecie.Element    = elementZajecia;                 // wymagany (RodzajZrodla == ZajęcieKomornicze)
    zajecie.NumerSprawy = "KM 123/2026";
    zajecie.Data       = new Date(2026, 1, 1);
    zajecie.Rozliczenie.Odbiorca = komornik;             // wierzyciel
    zajecie.Rozliczenie.RachunekOdbiorcy = komornik.Rachunki.WgKodu["GŁÓWNY"];

    t.Commit();
}
session.Save();

// Anulowanie zajęcia (worker bezparametrowy + property Zajęcie, nie ręczna flaga):
using (var t = session.Logout(editMode: true))
{
    var zaj = pracownik.ZajęciaKomornicze.First();
    new ZajęcieKomornicze.AnulujWorker { Zajęcie = zaj }.Anuluj();
    t.CommitUI();
}
session.Save();
```

---

### KADRY-C5 — Operacje seryjne na dodatkach (moduł Przeszeregowania) (★)

**Cel:** dodać / zmienić / zakończyć dodatek (oraz zmienić stawkę) dla **grupy pracowników** jedną
operacją. Realizuje to moduł **`Soneta.Przeszeregowania.PrzeszeregowaniaModule`**. Dokumentem zbiorczym
jest `Przeszeregowanie` (tabela `Przeszeregowania`, root) z pozycjami `ElementPrzeszeregowania`
(tabela `ElementyPrzeszer`, child). Pracownik widzi swoje pozycje przez
`pracownik.ElementyPrzeszeregowania`.

**Workery operacyjne** — ctor **bezparametrowy**, parametry przez właściwości `Pars` (typu `Params`,
ctor `(Context)`) i `Pracownicy: Pracownik[]`. Uruchamiaj w transakcji `CommitUI`. **Uwaga:** workery
te w bezgłowym hoście testowym (bez operatora/kontekstu UI) rzucają `NullReferenceException` — wymagają
realnego środowiska aplikacji.

| Worker | Metoda | Params (publiczne pola) | Działanie |
|---|---|---|---|
| `Soneta.Przeszeregowania.NowyDodatekWorker` | `NowyDodatek` | `Definicja: DefinicjaElementu, Podstawa: Currency, Procent: Percent` | wypłata/nadanie nowego dodatku grupie |
| `Soneta.Przeszeregowania.ZmianaDodatkuWorker` | `ZmianaDodatku` | `Definicja, Podstawa, ZmianaPodstawy: Currency, ProcentowaZmianaPodstawy: Percent, Procent, ZmianaProcentu: Percent, DataStawki: Date, PodstawaPrecyzja, PodstawaSposob` | zmiana parametrów istniejącego dodatku |
| `Soneta.Przeszeregowania.ZakończDodatekWorker` | `ZakończDodatek` | `Definicja: DefinicjaElementu` | zakończenie wypłaty dodatku |
| `Soneta.Przeszeregowania.DodajZmienDodatekWorker` | `DodajZmienDodatek` | `Params` (dodanie lub zmiana łącznie) | dodanie albo zmiana dodatku |
| `Soneta.Przeszeregowania.DodajNagrodęWorker` | (nagroda) | — | seryjne nagrody |
| `Soneta.Przeszeregowania.ZmianaStawkiWorker` | `ZmianaStawki` | — | seryjna zmiana stawki zaszeregowania |

**Dokument `Przeszeregowanie` (alternatywa: zbuduj dokument i wykonaj).** Tworzenie: `new
Przeszeregowanie()` + `session.GetPrzeszeregowania().Przeszeregowania.AddRow(doc)` (kolekcja **nie ma**
`AddNew` — to standardowy `GuidedRow` root z publicznym ctorem bezparametrowym).

| Pole | Typ |
|---|---|
| `Data` | `Soneta.Types.Date` (data przeszeregowania) |
| `DataWykonania` | `Soneta.Types.Date` |
| `Nazwa` | `string` |
| `Realizacja` | `Soneta.Przeszeregowania.RealizacjaPrzeszeregowania` (stan) |
| `Pracownicy` | `ICollection<Soneta.Kadry.Pracownik>` |
| `Elementy` | `SubTable<Soneta.Przeszeregowania.ElementPrzeszeregowania>` |
| `ZarzadzaneWnioskiem` | `bool` |

`ElementPrzeszeregowania` (child) niesie zmianę per pracownik: `Definicja: DefinicjaElementu`,
`Kwota`/`ZmianaKwoty`/`ProcentowaZmianaKwoty`, `Procent`/`ZmianaProcentu`, `Grupa: GrupaZaszeregowania`,
`Krotnosc`/`ZmianaKrotnosci`, `RodzajPrzeszergowania`, `Pracownik`, `PracHistoria`.

**Wykonanie dokumentu:** `Soneta.Przeszeregowania.Przeszeregowanie.WykonajWorker` (metoda `Wykonaj`,
`Params { Wykonaj: bool }`) — materializuje dokument na danych pracowników (tworzy/zmienia dodatki).
`ElementPrzeszeregowania.Wykonaj(Log)` realizuje pojedynczą pozycję.

**Pułapki:**
- To **operacja seryjna na danych operacyjnych** — trzymaj transakcje krótkie, duże grupy dziel na paczki
  (safe-code §13.1). Workery przyjmują tablicę pracowników — przekaż przefiltrowaną listę (po stronie
  serwera, safe-code §6).
- Workery `NowyDodatek`/`ZmianaDodatku`/`ZakończDodatek` operują na **definicji elementu** (`Definicja`),
  więc wybór właściwej `DefinicjaElementu` jest kluczowy (po nazwie / `RodzajZrodla == Dodatek`).
- Sam dokument `Przeszeregowanie` **nie zmienia danych** dopóki nie zostanie wykonany (`WykonajWorker`);
  do tego momentu to plan. Po `Wykonaj` zmiany trafiają w dodatki/etat pracowników.
- Indywidualne (jednostkowe) odpowiedniki to workery z KADRY-C2/KADRY-C1 na pojedynczym pracowniku
  (`Pracownik.DodajDodatekWorker`/`ZmieńDodatekWorker`/`ZabierzDodatekWorker`); moduł Przeszeregowania
  jest dla **grupy**.

**Snippet (operacja seryjna — nowy dodatek dla grupy):**

```csharp
var kadry = session.GetKadry();
var def = session.GetPlace().DefElementow.WgNazwy["Premia"];

// Grupa pracowników — filtr serwerowy (np. po wydziale), nie pełny skan:
Pracownik[] grupa = kadry.Pracownicy[(Pracownik p) => p.Last.Etat.Okres.Contains(Date.Today)]
                          .Cast<Pracownik>().ToArray();

var context = login.CreateEmptyContext().Clone(session);

using (var t = session.Logout(editMode: true))
{
    var par = new NowyDodatekWorker.Params(context)   // Params: ctor (Context)
    {
        Definicja = def,
        Podstawa  = (Currency)300m,
    };
    // ctor bezparametrowy; parametry przez właściwości Pars/Pracownicy:
    new NowyDodatekWorker { Pars = par, Pracownicy = grupa }.NowyDodatek();
    t.CommitUI();
}
session.Save();
```

---

### KADRY-C6 — Świadczenia socjalne (ZFŚS) i ich rozliczenie (★)

**Cel:** przyznać pracownikowi świadczenie socjalne z ZFŚS (zapomoga, dopłata do wypoczynku, paczka)
i ustawić jego rozliczenie płacowe (element, kwota, okres).

**Klasa i model:** `Soneta.Kadry.SwiadczSocjalne` — `GuidedRow` root, tabela `SwiadczeniaSoc`. Child
pracownika: `pracownik.Swiadczenia: SubTable<Soneta.Kadry.SwiadczSocjalne>`. **Konstruktor publiczny:**
`new SwiadczSocjalne(pracownik)`.

**Pola `SwiadczSocjalne`:**

| Pole | Typ | Uwaga |
|---|---|---|
| `Pracownik` | `Soneta.Kadry.Pracownik` | właściciel |
| `Definicja` | `Soneta.Kadry.DefinicjaŚwiadczeniaSocjalnego` | rodzaj świadczenia (słownik `DefSwiadczSocjal`) |
| `Data` | `Soneta.Types.Date` | data przyznania |
| `Nazwa` | `string` | nazwa |
| `Opis` | `Soneta.Business.MemoText` | opis |
| `Rozliczenie` | `Soneta.Kadry.RozliczenieSwiadczenia` (subrow) | dane rozliczeniowe (poniżej) |

**Subrow `Rozliczenie` (`RozliczenieSwiadczenia`):**

| Pole | Typ | Uwaga |
|---|---|---|
| `Rozliczenie.Element` | `Soneta.Place.DefinicjaElementu` | element płacowy do rozliczenia świadczenia |
| `Rozliczenie.Kwota` | `Soneta.Types.Currency` | kwota świadczenia |
| `Rozliczenie.Okres` | `Soneta.Types.FromTo` | okres rozliczenia |
| `Rozliczenie.Data` | `Soneta.Types.Date` | data rozliczenia |
| `Rozliczenie.Rozliczone` | `bool` | czy rozliczone (po naliczeniu wypłaty) |

**Definicja (`DefinicjaŚwiadczeniaSocjalnego`, słownik `DefSwiadczSocjal`):** `Nazwa: string`,
`Element: DefinicjaElementu` (domyślny element rozliczenia), `Kwota: Currency` (domyślna kwota). Z niej
dziedziczy świadczenie domyślny element i kwotę.

**Worker seryjny:** `Soneta.Kadry.SwiadczSocjalne.DodajŚwiadczenieWorker` (metoda `DodajŚwiadczenie`) —
ctor **bezparametrowy**, parametry przez właściwości `Pars` i `Pracownicy: Pracownik[]`; `Params` ma
ctor `(Context)`: `Params { Definicja: DefinicjaŚwiadczeniaSocjalnego, DataPrzyznania: Date, Kwota:
Currency, Element: DefinicjaElementu, DataRozliczenia: Date }` — nadaje świadczenie grupie (menu
*Operacje seryjne / Dodaj świadczenia socjalne*). Wzorzec:
`new DodajŚwiadczenieWorker { Pars = new …Params(ctx){…}, Pracownicy = tab }.DodajŚwiadczenie()`.

**Pułapki:**
- `Definicja` (świadczenia) pobierana ze słownika `DefSwiadczSocjal`; jej `Element`/`Kwota` są domyślne —
  na konkretnym świadczeniu nadpisujesz przez `Rozliczenie.Element`/`Rozliczenie.Kwota`.
- **Faktyczne rozliczenie (wypłata świadczenia, `Rozliczenie.Rozliczone == true`) następuje przy
  naliczeniu wypłaty** — samo dodanie świadczenia tworzy tylko zlecenie rozliczenia.
- Dla grupy używaj `DodajŚwiadczenieWorker`; pojedynczo — `new SwiadczSocjalne(pracownik)` + `AddRow`.

**Snippet:**

```csharp
var kadry = session.GetKadry();
var pracownik = kadry.Pracownicy.WgKodu["006"];
var defSwiadcz = kadry.DefSwiadczSocjal.WgNazwy["Dopłata do wypoczynku"];
var element = session.GetPlace().DefElementow.WgNazwy["Świadczenie socjalne"];

using (var t = session.Logout(editMode: true))
{
    var sw = new SwiadczSocjalne(pracownik);
    kadry.SwiadczeniaSoc.AddRow(sw);

    sw.Definicja = defSwiadcz;
    sw.Data      = new Date(2026, 6, 1);
    sw.Rozliczenie.Element = element;                  // element płacowy rozliczenia
    sw.Rozliczenie.Kwota   = (Currency)1000m;
    sw.Rozliczenie.Okres   = FromTo.Month(new YearMonth(2026, 6));

    t.Commit();
}
session.Save();

// Odczyt świadczeń pracownika:
foreach (SwiadczSocjalne s in pracownik.Swiadczenia)
{
    Currency kwota = s.Rozliczenie.Kwota;
    bool rozliczone = s.Rozliczenie.Rozliczone;
}
```

---

### KADRY-C7 — Pożyczki (KZP / ZFM) (★)

**Cel:** zarejestrować członkostwo pracownika w funduszu pożyczkowym, udzielić pożyczki, zbudować
harmonogram rat i potrącać raty z wynagrodzenia.

**Hierarchia obiektów (wszystkie `GuidedRow` root, childy pracownika):**
- `Soneta.Kadry.FundPozyczkowy` (tabela `FundPozyczkowe`) — **członkostwo** w funduszu;
  `pracownik.FunduszePozyczkowe: SubTable<Soneta.Kadry.FundPozyczkowy>`. Ctor:
  `new FundPozyczkowy(pracownik, definicja)`.
- `Soneta.Kadry.Pozyczka` (tabela `Pozyczki`) — **pożyczka** udzielona w ramach funduszu; kolekcja
  `fundusz.Pozyczki: SubTable<Soneta.Kadry.Pozyczka>`. Ctor: `new Pozyczka(fundusz)`.
- `Soneta.Kadry.RataPozyczki` (tabela `RatyPozyczek`) — **rata** harmonogramu; `pozyczka.Raty:
  SubTable<Soneta.Kadry.RataPozyczki>`. Raty pracownik widzi przez `pracownik.SplacaneRaty`
  (oraz `ZyrowaneRaty` jako żyrant). Ctor: `new RataPozyczki(pozyczka)`.
- `Soneta.Kadry.DefinicjaFunduszuPozyczkowego` (słownik `DefFundPozycz`, konfiguracyjny) — zasady
  funduszu (oprocentowanie, elementy płacowe wpisowego/składki/wycofania).

**Pola `Pozyczka` (kluczowe):**

| Pole | Typ | Uwaga |
|---|---|---|
| `Fundusz` | `Soneta.Kadry.FundPozyczkowy` | fundusz, w ramach którego udzielono |
| `Data` | `Soneta.Types.Date` | data udzielenia |
| `Kwota` | `Soneta.Types.Currency` | kwota pożyczki |
| `Element` | `Soneta.Place.DefinicjaElementu` | element wypłaty pożyczki |
| `ElementRaty` | `Soneta.Place.DefinicjaElementu` | element potrącenia raty |
| `IloscRat` | `int` | liczba rat |
| `KwotaRaty` | `Soneta.Types.Currency` | kwota raty |
| `SplatyOd` | `Soneta.Types.YearMonth` | miesiąc rozpoczęcia spłat |
| `Procent` | `Soneta.Types.Percent` | oprocentowanie |
| `Sposob` | `Soneta.Kadry.SposóbSpłatyOdsetek` | sposób spłaty odsetek |
| `AlgorytmRaty` | `Soneta.Kadry.AlgorytmRatyPożyczki` | algorytm wyliczania raty |
| `Raty` | `SubTable<Soneta.Kadry.RataPozyczki>` | harmonogram rat |
| `Stan` | `Soneta.Kadry.StanSpłat` | enum: `NieSpłacona = 0`, `Częściowo = 1`, `Całkowicie = 2` |
| `Splacona` | `bool` | spłacona w całości |

**Pola `RataPozyczki`:** `Pozyczka`, `Data: Date`, `Miesiąc: YearMonth`, `Kapital: Currency`,
`Odsetki: Currency`, `Element: DefinicjaElementu` (potrącenie raty), `Stan: StanSpłat`,
`Pozostaje`/`PozostajeKapitał`/`PozostajeOdsetki` (kalkulowane), `Zyrant: Pracownik`,
`Splacajacy: Pracownik`.

**Generowanie harmonogramu (workery):**

| Worker | Metoda | Params / sygnatura |
|---|---|---|
| `Soneta.Kadry.Pozyczka.UzgodnijRatyWorker` | `UzgodnijRaty` | ctor bezparam.; `Pars = new Params(ctx) { UzgodnijRaty = true }` (uwaga: `PrzeliczRaty` jest **tylko-do-odczytu**), `Pożyczka = pozyczka` — **buduje/przelicza harmonogram rat** wg `IloscRat`/`KwotaRaty`/`SplatyOd` |
| `Soneta.Kadry.Pozyczka.PożyczkaWorker` | `Pożyczka` | podsumowanie spłat (props: `Razem`, `Spłaty`, `Pozostaje`, `RazemOdsetki`…) |
| `Soneta.Kadry.Pozyczka.ElementWypłatyWorker` | `Pokaż` | podgląd elementu wypłaty pożyczki |

Metody na samym `Pozyczka`: `pozyczka.UpdatePozyczka()` (przelicz), `pozyczka.Rata(YearMonth,
DefinicjaElementu)`, `pozyczka.RatyZaMiesiąc(YearMonth)`, `pozyczka.SąRaty(YearMonth)`.

**Pułapki:**
- Ścieżka tworzenia jest **trzystopniowa**: `FundPozyczkowy(pracownik, definicja)` → `Pozyczka(fundusz)`
  → harmonogram. Pożyczki **nie da się** utworzyć bez funduszu (ctor wymaga `FundPozyczkowy`).
- Harmonogram rat generuj **workerem** `UzgodnijRatyWorker` (albo `UpdatePozyczka()`), nie ręcznym
  dodawaniem `RataPozyczki` — worker rozkłada kapitał/odsetki wg algorytmu.
- `Element` (wypłaty) i `ElementRaty` (potrącenia) to **różne** definicje elementów — `ElementRaty`
  realizuje potrącenie raty w wypłacie.
- **Faktyczne potrącenie raty następuje przy naliczeniu wypłaty** — `Stan`/`Splacono`/`Pozostaje`
  aktualizują się po naliczeniu. Samo udzielenie pożyczki ich nie zmienia.
- `DefinicjaFunduszuPozyczkowego` to słownik konfiguracyjny — pobierz istniejący wpis, nie twórz „w locie".

**Snippet:**

```csharp
var kadry = session.GetKadry();
var pracownik = kadry.Pracownicy.WgKodu["006"];
var defFunduszu = kadry.DefFundPozycz.WgNazwy["KZP"];
var elWyplata = session.GetPlace().DefElementow.WgNazwy["Pożyczka"];
var elRata    = session.GetPlace().DefElementow.WgNazwy["Spłata pożyczki"];

using (var t = session.Logout(editMode: true))
{
    // 1) Członkostwo w funduszu
    var fundusz = new FundPozyczkowy(pracownik, defFunduszu);
    kadry.FundPozyczkowe.AddRow(fundusz);
    fundusz.Okres = new FromTo(new Date(2026, 1, 1), Date.MaxValue);

    // 2) Pożyczka w ramach funduszu
    var pozyczka = new Pozyczka(fundusz);
    kadry.Pozyczki.AddRow(pozyczka);
    pozyczka.Data       = new Date(2026, 1, 10);
    pozyczka.Kwota      = (Currency)12000m;
    pozyczka.Element    = elWyplata;
    pozyczka.ElementRaty = elRata;
    pozyczka.IloscRat   = 12;
    pozyczka.SplatyOd   = new YearMonth(2026, 2);

    // 3) Harmonogram rat (worker bezparametrowy; Params: ctor (Context); PrzeliczRaty read-only)
    var context = login.CreateEmptyContext().Clone(session);
    var par = new Pozyczka.UzgodnijRatyWorker.Params(context) { UzgodnijRaty = true };
    new Pozyczka.UzgodnijRatyWorker { Pars = par, Pożyczka = pozyczka }.UzgodnijRaty();

    t.CommitUI();
}
session.Save();

// Odczyt harmonogramu:
foreach (FundPozyczkowy f in pracownik.FunduszePozyczkowe)
    foreach (Pozyczka p in f.Pozyczki)
        foreach (RataPozyczki r in p.Raty)
        {
            YearMonth m = r.Miesiąc;
            Currency kapital = r.Kapital, odsetki = r.Odsetki;
            StanSpłat stan = r.Stan;
        }
```

