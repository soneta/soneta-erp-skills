# HANDEL14 — Płatności dokumentu handlowego

> Wspólne fakty o typie, podstawowe typy i szablon wzorca: [../handel.md](../handel.md).

Płatności (należności i zobowiązania) powstają automatycznie z dokumentu handlowego płatnego (np. FV, FZ)
i opisują kwoty do uregulowania: termin, sposób zapłaty, ewidencję środków pieniężnych (ŚP) oraz stan
rozliczenia z zapłatami. Z poziomu dokumentu dostęp do nich daje kolekcja `dok.Platnosci`
(`SubTable<Soneta.Kasa.Platnosc>`). Pojedyncza płatność to obiekt `Soneta.Kasa.Platnosc` — w praktyce jedna
z dwóch klas konkretnych: `Naleznosc` (kierunek `Przychod`, sprzedaż) lub `Zobowiazanie` (kierunek
`Rozchod`, zakup). Wymagana referencja do `Soneta.Kasa`.

> **Pojęcia.** Kwota płatności (`Kwota: Currency`) jest w walucie dokumentu; `KwotaKsiegi: Currency` to jej
> przeliczenie na PLN po `Kurs`. Stan uregulowania to `StanRozliczenia` (+ `KwotaRozliczona`,
> `DoRozliczenia`). Płatności są edytowalne wyłącznie, gdy dokument (i sama płatność) są w **buforze** —
> po zatwierdzeniu pola płatności stają się tylko do odczytu.

---

### HANDEL-W75 — Przeglądanie płatności dokumentu

**Cel:** odczytać płatności wystawione z dokumentu — kwotę, walutę, sposób zapłaty, termin oraz stan
rozliczenia — bez modyfikacji.

**Warianty:**

| Wariant | Źródło / pole |
|---|---|
| Lista płatności dokumentu | `dok.Platnosci` (`SubTable<Platnosc>`) |
| Kwota i waluta | `p.Kwota: Currency` (`.Value`, `.Symbol`) |
| Sposób zapłaty | `p.SposobZaplaty: Soneta.Kasa.SposobZaplaty` (`.Nazwa`, `.Typ`, `.MPP`) |
| Termin płatności | `p.Termin: Date`, `p.TerminDni: int` (dni od daty odniesienia) |
| Stan rozliczenia | `p.StanRozliczenia`, `p.Rozliczono: bool`, `p.KwotaRozliczona`, `p.DoRozliczenia` |
| Kwota nierozliczona po terminie | `p.DoRozliczenia` + warunek `p.Termin < Date.Today` |
| Należność / zobowiązanie | `p.Kierunek`, `p.CzyNaleznosc: bool`, `p.CzyZobowiazanie: bool` |

**Pola i typy:** `Platnosc.Kwota: Soneta.Types.Currency`, `KwotaKsiegi: Currency` (PLN),
`SposobZaplaty: Soneta.Kasa.SposobZaplaty`, `Termin: Soneta.Types.Date`, `TerminDni: int`,
`StanRozliczenia: Soneta.Kasa.StanRozliczenia` (`Nierozliczony=0`, `Czesciowo=1`, `Calkowicie=2`,
`NiePodlega=3`), `Rozliczono: bool`, `KwotaRozliczona: Currency`, `DoRozliczenia: Currency`,
`Kierunek: Soneta.Kasa.KierunekPlatnosci`, `EwidencjaSP: Soneta.Kasa.EwidencjaSP`.

**Snippet:**

```csharp
var hm = session.GetHandel();
var dok = hm.DokHandlowe.WgDaty[...];        // lub inny lookup dokumentu

foreach (Platnosc p in dok.Platnosci)
{
    Currency kwota   = p.Kwota;                // w walucie dokumentu
    string waluta    = p.Kwota.Symbol;         // np. "PLN", "EUR"
    string sposob    = p.SposobZaplaty.Nazwa;  // np. "Przelew", "Gotówka"
    Date termin      = p.Termin;
    StanRozliczenia stan = p.StanRozliczenia;

    // Kwota pozostała do zapłaty i to, co już przeterminowane:
    Currency doZaplaty = p.DoRozliczenia;
    bool poTerminie    = !p.Rozliczono && p.Termin < Date.Today && p.DoRozliczenia > Currency.Zero;
}
```

**Pułapki:**
- `dok.Platnosci` to `SubTable` — iteruj serwerowo, nie materializuj do `List` tylko po to, by policzyć
  elementy (`IsEmpty`/`Count` są dostępne na kolekcji). Patrz [`rowcondition.md`](references/rowcondition.md).
- `StanRozliczenia.NiePodlega` oznacza płatność **nierozliczaną** (`p.Rozliczana == false`) — nie myl jej
  z `Nierozliczony` (rozliczana, ale jeszcze niezapłacona).
- `Kwota` jest w walucie dokumentu; do raportu w PLN użyj `KwotaKsiegi` (HANDEL-W81), nie mnóż „ręcznie".
- „Po terminie" liczysz z `Termin` i `DoRozliczenia` względem `Date.Today` — w samej płatności nie ma
  gotowego pola „kwota po terminie".

---

### HANDEL-W76 — Rozbicie płatności na raty

**Cel:** zamienić pojedynczą płatność dokumentu na zestaw rat (cyklicznych miesięcznych) albo na rozbicie
netto + VAT, przy użyciu publicznego workera `PodzialPlatnosciWorker`.

**Warianty:**

| Wariant | Ustawienie `WParams` |
|---|---|
| Raty miesięczne wg liczby rat | `Metoda = WOptions.Raty`, `IlośćRat = n` |
| Raty miesięczne wg kwoty raty | `Metoda = WOptions.Raty`, `Kwota = kwotaRaty` (worker wyliczy liczbę rat) |
| Rozbicie netto + VAT (MPP) | `Metoda = WOptions.NettoPlusVat` |

**Pola i typy:** worker `Soneta.Handel.PodzialPlatnosci.PodzialPlatnosciWorker`, parametry
`Soneta.Handel.PodzialPlatnosci.WParams : ContextBase` (inicjowane z `Context` zawierającego
`DokumentHandlowy`): `Metoda: WOptions` (`NettoPlusVat=0x1`, `Raty=0x2`), `IlośćRat: int`,
`Kwota: Currency` (kwota pojedynczej raty), `TerminPierwszejWpłaty: Date` (read-only — z warunków
płatności), `Cykl: WOptions` (`Miesięczny`). Akcja: `PodzielPlatnosci([Context] DokumentHandlowy)`.

**Snippet:**

```csharp
// Worker działa na dokumencie w BUFORZE z kierunkiem płatności (FV/FZ).
// Parametry tworzymy przez Context (wzorzec worker-z-Params), patrz worker-extender.md.
var context = new Context(session);
context.Set(dok);                              // DokumentHandlowy w kontekście

var wp = new PodzialPlatnosci.WParams(context)
{
    Metoda  = PodzialPlatnosci.WOptions.Raty,
    IlośćRat = 3,                              // 3 równe raty miesięczne
};

var worker = new PodzialPlatnosci.PodzialPlatnosciWorker(wp);
worker.PodzielPlatnosci(dok);                  // sam otwiera transakcję i robi CommitUI

session.Save();
```

**Pułapki:**
- Akcja jest dostępna tylko gdy `dok.Bufor == true` i `dok.Definicja.KierunekPlatnosci != Brak`
  (`IsVisiblePodzielPlatnosci`) — na zatwierdzonym dokumencie się nie wykona.
- `PodzielPlatnosci` **sam otwiera transakcję** (`Session.Logout(true)` + `CommitUI`) i **usuwa**
  istniejące płatności dokumentu, zastępując je wyliczonymi ratami/podziałem. Nie zawijaj go w drugą
  transakcję edycyjną; po nim wywołaj `session.Save()`.
- W trybie `Raty` ustawienie `Kwota` przelicza `IlośćRat` (i odwrotnie) — ustaw jedno z dwóch.
- Ostatnia rata przejmuje resztę z zaokrągleń (kwoty rat sumują się do `BruttoCy` dokumentu) — nie zakładaj
  równego podziału co do grosza.

---

### HANDEL-W77 — Ręczne dodanie / edycja pojedynczej płatności

**Cel:** ręcznie ułożyć płatności dokumentu — np. część gotówką, resztę przelewem — ustawiając sposób
zapłaty, ewidencję ŚP, termin i kwotę.

**Warianty:**

| Wariant | Operacja |
|---|---|
| Dodanie należności (sprzedaż) | `new Naleznosc(dok)` + `AddRow` |
| Dodanie zobowiązania (zakup) | `new Zobowiazanie(dok)` + `AddRow` |
| Edycja istniejącej | zmiana pól na elemencie `dok.Platnosci` |
| Częściowo gotówka + przelew | dwie płatności o różnym `SposobZaplaty`, suma `Kwota` = wartość dokumentu |

**Pola i typy:** konstruktory `Naleznosc(IDokumentPlatny)`, `Zobowiazanie(IDokumentPlatny)` (publiczne).
Tabela płatności: `KasaModule.GetInstance(session).Platnosci`. Pola zapisywalne:
`SposobZaplaty: SposobZaplaty`, `EwidencjaSP: EwidencjaSP`, `Termin: Date` (lub `TerminDni: int`),
`Kwota: Currency`, `KwotaMPP: Currency`, `Rachunek: RachunekBankowyPodmiotu`, `Priorytet: int`.

**Snippet:**

```csharp
var kasa = KasaModule.GetInstance(session);
var spZaplaty = kasa.SposobyZaplaty;

using (var t = session.Logout(editMode: true))   // dokument MUSI być w buforze
{
    // 1) część gotówką
    var gotowka = new Naleznosc(dok);             // sprzedaż -> Naleznosc; zakup -> Zobowiazanie
    kasa.Platnosci.AddRow(gotowka);
    gotowka.SposobZaplaty = spZaplaty.Gotówka;
    gotowka.Kwota  = new Currency(300m, "PLN");
    gotowka.Termin = dok.DataDokumentu;           // gotówka -> termin = data dokumentu

    // 2) reszta przelewem
    var przelew = new Naleznosc(dok);
    kasa.Platnosci.AddRow(przelew);
    przelew.SposobZaplaty = spZaplaty.WgNazwy["Przelew"];
    przelew.Kwota  = new Currency(dok.BruttoCy.Value - 300m, "PLN");
    przelew.TerminDni = 14;                        // 14 dni od daty odniesienia
    // przelew.Rachunek = ...                       // dla przelewu wskaż rachunek podmiotu

    t.Commit();                                     // CommitUI() w workerze/extenderze
}
session.Save();
```

**Pułapki:**
- Płatność można dodać **tylko do dokumentu w buforze** — `OnAdded` rzuca wyjątek
  („Nie można dodawać płatności do zatwierdzonego dokumentu"). `Platnosc.Bufor`/`IsReadOnly` chronią
  edycję po zatwierdzeniu.
- Dobierz klasę do kierunku dokumentu: sprzedaż (`KierunekPlatnosci.Przychod`) → `Naleznosc`, zakup
  (`Rozchod`) → `Zobowiazanie`. Zła klasa = niespójny kierunek.
- `Kwota` to `Currency` — twórz `new Currency(wartość, symbolWaluty)`; symbol musi być zgodny z walutą
  dokumentu/ewidencji (weryfikator ostrzega o niezgodności).
- Dla sposobu zapłaty typu „przelew" wymagany jest `Rachunek` (weryfikator-ostrzeżenie). Ustaw rachunek
  należący do podmiotu płatności (twardy weryfikator `RachunekPodmiotuVerifier`).
- `SposobZaplaty` pobieraj z tabeli (`kasa.SposobyZaplaty.Gotówka`, `...WgNazwy["Przelew"]`) — to rekord
  konfiguracyjny, nie ustawiaj „z palca".

---

### HANDEL-W78 — Warunki płatności z kontrahenta i ich przeliczenie na dokumencie

**Cel:** odczytać/ustawić warunki płatności dokumentu (sposób, termin w dniach, ewidencja ŚP) spójnie
z domyślnymi warunkami kontrahenta, przez publiczny `WarunkiPłatnościWorker`.

**Warianty:**

| Wariant | Mechanizm |
|---|---|
| Domyślne warunki z kontrahenta | `Kontrahent.SposobZaplaty`, `Kontrahent.Termin` (HANDEL-W9) — inicjują płatność |
| Odczyt warunków dokumentu | `WarunkiPłatnościWorker`: `Sposób`, `TerminDni`, `Termin`, `EwidencjaSP`, `Kwota`, `Raty` |
| Zmiana terminu (w dniach) | `worker.TerminDni = n` lub `worker.Termin = data` |
| Zmiana sposobu zapłaty | `worker.Sposób = ...` (przelicza też ewidencję ŚP) |
| Bezpośrednio na płatności | `p.TerminDni`, `p.Termin`, `p.SposobZaplaty`, `p.EwidencjaSP` |

**Pola i typy:** worker `Soneta.Kasa.WarunkiPłatnościWorker` (publiczny, zarejestrowany dla
`IDokumentPlatny`): `[Context] Dokument: IDokumentPlatny`, `TerminDni: int`, `Termin: Date`,
`Sposób: SposobZaplaty`, `EwidencjaSP: EwidencjaSP`, `Kwota: Currency` (read-only), `Raty: int`
(liczba płatności). Operuje na **pierwszej** płatności dokumentu. Na kontrahencie:
`Kontrahent.SposobZaplaty: FormaPlatnosci`, `Kontrahent.Termin: int` (patrz kontrahent HANDEL-W9).

**Snippet:**

```csharp
// Warunki płatności kontrahenta są przenoszone na płatność przy jej tworzeniu/zmianie podmiotu.
// Do odczytu/zmiany "zbiorczej" warunków dokumentu służy WarunkiPłatnościWorker:
var context = new Context(session);
context.Set(dok);                                  // dok : IDokumentPlatny (DokumentHandlowy)

var warunki = new WarunkiPłatnościWorker { Dokument = dok };

int dni        = warunki.TerminDni;                // termin liczony w dniach
SposobZaplaty sp = warunki.Sposób;
int liczbaRat  = warunki.Raty;

using (var t = session.Logout(editMode: true))     // dokument w buforze
{
    if (!warunki.IsReadOnlyTerminDni())
        warunki.TerminDni = 21;                    // przelicza Termin na pierwszej płatności
    if (!warunki.IsReadOnlySposób())
        warunki.Sposób = session.GetKasa().SposobyZaplaty.WgNazwy["Przelew"];
    t.Commit();
}
session.Save();
```

**Pułapki:**
- `WarunkiPłatnościWorker` działa na **pierwszej** płatności i tylko gdy `Raty <= 1` (jedna płatność);
  przy wielu płatnościach (`Raty > 1`) pola są read-only (`IsReadOnly...` zwracają `true`) — wtedy edytuj
  poszczególne płatności bezpośrednio (HANDEL-W77) albo użyj podziału (HANDEL-W76).
- `TerminDni` to dni od **daty odniesienia** (`TerminLiczonyOd`/data dokumentu), nie data bezwzględna —
  ustawienie `TerminDni` przelicza `Termin`.
- Edycja terminu może być zablokowana polityką (`IEdycjaTerminuPlatnosci`) — zawsze sprawdzaj
  `IsReadOnlyTermin()`/`IsReadOnlyTerminDni()` przed zapisem.
- Zmiana `Sposób` przelicza ewidencję ŚP (subewidencję) — nie ustawiaj `EwidencjaSP` „obok", licz na
  spójność workera.

---

### HANDEL-W79 — Zmiana płatnika (inny niż kontrahent)

**Cel:** ustawić na płatności podmiot inny niż kontrahent dokumentu (np. płatnik trzeci) i wykryć tę
sytuację z poziomu dokumentu.

**Warianty:**

| Wariant | Mechanizm |
|---|---|
| Zmiana płatnika płatności | `p.Podmiot = innyPodmiot` (`IPodmiotKasowy`) |
| Wykrycie „innego płatnika" | `dok.InnyPłatnik: bool` (read-only — `true`, gdy jakaś płatność ma `Podmiot != Kontrahent`) |
| Płatnik domyślny kontrahenta | `Kontrahent.Platnik: IPodmiotKasowy` (kalkulowane — nadrzędny z relacji) |

**Pola i typy:** `Platnosc.Podmiot: Soneta.Kasa.IPodmiotKasowy` (zapisywalne),
`DokumentHandlowy.InnyPłatnik: bool` (**kalkulowane, read-only**),
`IsReadOnlyPodmiot()`. `Kontrahent` implementuje `IPodmiotKasowy`.

**Snippet:**

```csharp
// "Inny płatnik" ustawiamy na poziomie POJEDYNCZEJ płatności — pole Podmiot:
IPodmiotKasowy platnik = session.GetCRM().Kontrahenci.WgKodu["PLATNIK"];

using (var t = session.Logout(editMode: true))     // dokument w buforze
{
    foreach (Platnosc p in dok.Platnosci)
        if (!p.IsReadOnlyPodmiot())
            p.Podmiot = platnik;                    // rozrachunek przejdzie na nowy podmiot
    t.Commit();
}
session.Save();

// Odczyt: czy dokument ma płatnika innego niż kontrahent:
bool inny = dok.InnyPłatnik;                        // kalkulowane, tylko do odczytu
```

**Pułapki:**
- `dok.InnyPłatnik` jest **wyłącznie do odczytu** — to flaga wyliczana z porównania `p.Podmiot` z
  `dok.Kontrahent`. Aby „zmienić płatnika", ustaw `Platnosc.Podmiot`, nie próbuj przypisać `InnyPłatnik`.
- `Podmiot` jest read-only, gdy płatność jest częściowo rozliczona (`KwotaRozliczona != 0`) — sprawdzaj
  `IsReadOnlyPodmiot()`.
- Zmiana podmiotu przenosi rozrachunek na nowy podmiot i może podmienić zablokowany podmiot na jego
  zamiennik (wbudowana logika) — odczytaj `p.Podmiot` po zmianie, nie zakładaj wartości wejściowej.
- `Rachunek` musi należeć do nowego `Podmiot` (twardy weryfikator) — po zmianie płatnika zweryfikuj/wyczyść
  rachunek.

---

### HANDEL-W80 — Odczyt stanu rozliczenia płatności

**Cel:** ustalić, czy płatność jest rozliczona w całości, częściowo czy nierozliczona, oraz dotrzeć do
powiązanych rozliczeń (zapłat).

**Warianty:**

| Wariant | Pole / kolekcja |
|---|---|
| Stan zbiorczy | `p.StanRozliczenia` (`Nierozliczony`/`Czesciowo`/`Calkowicie`/`NiePodlega`) |
| Rozliczono całkowicie? | `p.Rozliczono: bool`, `p.Zrealizowane: bool` |
| Kwoty | `p.KwotaRozliczona`, `p.DoRozliczenia` |
| Data rozliczenia | `p.DataRozliczenia: Date` (`Date.MaxValue` = nierozliczona) |
| Rozliczono na dzień | `p.RozliczonoDoDnia(Date data)` |
| Powiązane rozliczenia/transakcje | `p.Dokumenty`, `p.Zaplaty` (kolekcje `RozliczenieSP`) |
| Czy podlega rozliczeniu | `p.Rozliczana: bool` |

**Pola i typy:** `StanRozliczenia: Soneta.Kasa.StanRozliczenia`, `Rozliczono: bool`, `Zrealizowane: bool`,
`KwotaRozliczona/DoRozliczenia: Currency`, `DataRozliczenia: Date`, `Rozliczana: bool`,
`Dokumenty`/`Zaplaty` (rozliczenia typu `Soneta.Kasa.RozliczenieSP`),
metoda `RozliczonoDoDnia(Date, bool wgDatyKsi = false): Currency`.

**Snippet:**

```csharp
foreach (Platnosc p in dok.Platnosci)
{
    switch (p.StanRozliczenia)
    {
        case StanRozliczenia.Calkowicie: /* zapłacona w całości */ break;
        case StanRozliczenia.Czesciowo:  /* część zapłacona: p.DoRozliczenia > 0 */ break;
        case StanRozliczenia.Nierozliczony: /* brak zapłat */ break;
        case StanRozliczenia.NiePodlega:    /* płatność nierozliczana */ break;
    }

    Currency zaplaconoDoDzis = p.RozliczonoDoDnia(Date.Today);

    // Powiązane rozliczenia (transakcje zapłaty):
    foreach (RozliczenieSP r in p.Zaplaty) { /* r.Data, r.KwotaDokumentu, ... */ }
    foreach (RozliczenieSP r in p.Dokumenty) { /* r.Data, r.KwotaZaplaty, ... */ }
}
```

**Pułapki:**
- `StanRozliczenia` jest kalkulowane z `KwotaRozliczona`/`Kwota` — nie ustawiaj go; rozliczenia powstają
  przez operacje kasowe/rozliczeniowe, nie przez bezpośredni zapis na płatności.
- `DataRozliczenia == Date.MaxValue` oznacza „nierozliczona" — nie traktuj `MaxValue` jako realnej daty.
- Rozliczenia są rozdzielone na dwie kolekcje (`Dokumenty` i `Zaplaty`) zależnie od strony powiązania —
  do pełnego obrazu przejrzyj obie.
- Dla płatności `Rozliczana == false` (`NiePodlega`) `DoRozliczenia` wynosi zero — nie analizuj jej jak
  zaległości.

---

### HANDEL-W81 — Płatności w walucie obcej (kwota w walucie vs PLN, kurs)

**Cel:** poprawnie odczytać/ustawić płatność walutową — kwotę w walucie obcej, jej przeliczenie na PLN
oraz kurs i tabelę kursową.

**Warianty:**

| Wariant | Pole |
|---|---|
| Kwota w walucie dokumentu | `p.Kwota: Currency` (symbol = waluta, np. „EUR") |
| Kwota w PLN (księgowa) | `p.KwotaKsiegi: Currency` |
| Kurs i tabela | `p.Kurs: double`, `p.TabelaKursowa: TabelaKursowa` |
| Interfejs walutowy | `IRowWithKurs`: `KwotaWaluty` (= `Kwota`), `KwotaPLN` (= `KwotaKsiegi`) |
| Słownie | `p.Słownie: string` |

**Pola i typy:** `Kwota: Currency` (waluta dokumentu), `KwotaKsiegi: Currency` (PLN),
`Kurs: double`, `TabelaKursowa: Soneta.Waluty.TabelaKursowa`. `Platnosc` implementuje
`Soneta.Waluty.IRowWithKurs` (`KwotaWaluty`, `KwotaPLN`).

**Snippet:**

```csharp
foreach (Platnosc p in dok.Platnosci)
{
    if (p.Kwota.Symbol != Currency.SystemSymbol)   // płatność walutowa (np. "EUR")
    {
        Currency wWalucie = p.Kwota;               // np. 1000 EUR
        Currency wPln     = p.KwotaKsiegi;         // przeliczenie na PLN
        double kurs       = p.Kurs;                // kurs zastosowany
        TabelaKursowa tab = p.TabelaKursowa;       // tabela kursów (lub null)
    }
}

// Ustawienie kursu ręcznie (gdy dokument/ewidencja walutowa, w buforze):
using (var t = session.Logout(editMode: true))
{
    foreach (Platnosc p in dok.Platnosci)
        if (p.Kwota.Symbol != Currency.SystemSymbol && !p.IsReadOnlyTabelaKursowa())
            p.TabelaKursowa = session.GetKasa().EwidencjeSP /* ... */ ?.TabelaKursowa;
    t.Commit();
}
session.Save();
```

**Pułapki:**
- Dla płatności w PLN `Kurs == 1.0` i `TabelaKursowa == null` — przeliczeniem zajmuj się tylko, gdy
  `Kwota.Symbol != Currency.SystemSymbol`.
- `KwotaKsiegi` wylicza się z `Kwota * Kurs`; jeśli ustawisz tabelę bez kursu na datę dokumentu, kurs może
  pozostać `0.0` (brak kursu) — wtedy `KwotaKsiegi` będzie zerowa. Upewnij się, że tabela kursowa ma kurs
  na `DataDokumentu` (w bazie Demo brak kursów „na dziś" → operacja walutowa rzuca
  `KursWalutyNotFoundException`, por. rozdz. o walutach).
- Kwota płatności walutowej musi mieć symbol zgodny z walutą dokumentu/ewidencji ŚP — weryfikator ostrzega
  o niezgodności symboli.
- Sumę płatności w PLN czytaj z `KwotaKsiegi` (lub `IRowWithKurs.KwotaPLN`), nie przeliczaj `Kwota` własnym
  kursem.

---

### HANDEL-W82 — Powiązanie płatności z terminem i rabatem za wcześniejszą zapłatę

**Cel:** obsłużyć rabat za wcześniejszą zapłatę (skonto) — wskazać termin uprawniający do rabatu i odczytać
jego wpływ na warunki płatności dokumentu.

**Warianty:**

| Wariant | Mechanizm |
|---|---|
| Ustawienie terminu rabatu na dokumencie | `dok.RabatZaTerminPlatnosci.Termin = data` |
| Odczyt naliczonego rabatu | `dok.RabatZaTerminPlatnosci.Rabat: Percent` |
| Rodzaj rabatu | `dok.RabatZaTerminPlatnosci.Rodzaj: RodzajRabatuZaTerminPlatnosci` |
| Termin samej płatności | `p.Termin`, `p.TerminDni` (HANDEL-W77/HANDEL-W78) |
| Parametry rabatu na kontrahencie | `Kontrahent.RodzajRabatuZaTerminPlatnosci`, `TrybRabatu...`, `IloscDniDlaRabatu`, `WartoscRabatuZaKazdyDzien` |

**Pola i typy:** `DokumentHandlowy.RabatZaTerminPlatnosci: Soneta.Handel.RabatZaTerminPlatnosci`
(subrow) z polami `Termin: Date` (zapisywalne — termin uprawniający do rabatu), `Rabat: Percent`
(wyliczane), `Rodzaj: RodzajRabatuZaTerminPlatnosci`. Na płatności: `Termin: Date`,
`TerminDni: int`, `TerminLiczonyOd: Date` (data odniesienia, read-only).

**Snippet:**

```csharp
using (var t = session.Logout(editMode: true))     // dokument w buforze, z kontrahentem
{
    // Termin uprawniający do rabatu za wcześniejszą zapłatę (skonto):
    if (!dok.RabatZaTerminPlatnosci.IsReadOnlyTermin())
        dok.RabatZaTerminPlatnosci.Termin = dok.DataDokumentu.AddDays(7);
    t.Commit();
}
session.Save();

// Odczyt naliczonego rabatu (zależny od parametrów rabatu kontrahenta):
Percent rabat = dok.RabatZaTerminPlatnosci.Rabat;
Date terminRabatu = dok.RabatZaTerminPlatnosci.Termin;
```

**Pułapki:**
- `RabatZaTerminPlatnosci.Rabat` jest **wyliczany** z parametrów kontrahenta (tryb: progresywny /
  podstawowy / progowy) i różnicy dni między `Termin` rabatu a terminem płatności — nie ustawiaj go wprost.
- Ustawienie `Termin` < `Date.Today` zeruje rabat i czyści termin — przekazuj datę przyszłą.
- Termin rabatu można ustawić tylko, gdy **wszystkie** płatności dokumentu mają ten sam termin
  (`Dokument.Platnosci` zgrupowane po `Termin` → jedna grupa); w przeciwnym razie rzuca `RowException`.
- Edycja może być zablokowana polityką `IEdycjaTerminuPlatnosci` — sprawdzaj `IsReadOnlyTermin()`.
- Naliczenie rabatu wymaga skonfigurowanych parametrów na kontrahencie
  (`RodzajRabatuZaTerminPlatnosci`, `Tryb...`, progi/wartości) — bez nich `Rabat` pozostanie `Percent.Zero`.

---

