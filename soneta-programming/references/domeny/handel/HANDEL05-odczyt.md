# HANDEL05 — Odczyt i wyszukiwanie

> Wspólne fakty o typie, podstawowe typy i szablon wzorca: [../handel.md](../handel.md).

Odczyt dokumentów handlowych prawie zawsze sprowadza się do **filtrowania serwerowego**: warunek
budujesz wyrażeniem LINQ i aplikujesz na **kluczu** tabeli (`DokHandlowe.WgXxx[dok => …]`) albo na
**kolekcji podrzędnej** (`towar.Pozycje[…]`, `dok.Pozycje[…]`). Z bazy do pamięci trafiają wtedy
wyłącznie pasujące wiersze. `DokHandlowe` to duża tabela **operacyjna** (`guided="Exported"`) —
nigdy nie iteruj jej w całości z `if` w pamięci; zawsze zawężaj zakres (okres, kontrahent, definicja)
przez SQL i — przy analizach poprzecznych — ogranicz przedział czasowy.

> **Fundamenty** (sesja, transakcja, blokada optymistyczna) opisuje [`safe-code.md`](../safe-code.md),
> a mechanikę warunków serwerowych [`rowcondition.md`](../rowcondition.md) — tu się do nich
> odwołujemy, nie powtarzamy. Cały kod jest zgodny z **C# 10** i operuje wyłącznie na **publicznym
> kontrakcie** platformy. W wyrażeniu LINQ wolno użyć **tylko pól bazodanowych**; pole kalkulowane
> rzuci `LinqConditionException`.

**Fakty o odczycie (zweryfikowane na tabeli `DokHandlowe` i `PozycjeDokHan`):**

- **Klucze tabeli `DokHandlowe`** (do filtrowania serwerowego i sortowania): `WgDaty`
  (`Data`, `Czas`), `WgMagazynuNumer` (`Magazyn`, `Numer.Pelny`), `WgMagazynuObcy`
  (`Magazyn`, `Obcy.Numer`), `WgKontrahentaObcy` (`Kontrahent`, `Obcy.Numer`, `Kategoria`),
  `WgOkresIntrastat`, oraz `PrimaryKey`. **Nie ma** „gołego" klucza `WgKontrahenta` ani `WgNumeru` —
  filtruj wyrażeniem na dowolnym z powyższych kluczy (sortowanie bierze się z wybranego klucza).
- **Indeksator po Guid:** `hm.DokHandlowe[guid]` (zwraca `DokumentHandlowy`; **rzuca `RowNotFoundException`** dla nieznanego Guid).
- **Pozycje dokumentu:** `dok.Pozycje` — `LpSubTable<PozycjaDokHandlowego>` (sortowane po `Lp`).
- **Pozycje danego towaru (historia obrotu):** `towar.Pozycje` — `SubTable<PozycjaDokHandlowego>`
  (klucz `WgTowar`). Klucze na `PozycjeDokHan`: `WgDaty` (`Data`), `WgKierunek`
  (`Towar`, `KierunekMagazynu`, `Data`, `Czas`), `WgTowarDokumentu` (`Towar`, `Dokument`).
- **Numer dokumentu:** pole `dok.Numer: NumerDokumentu`. Pełny numer do **odczytu** to
  `dok.Numer.NumerPelny` (kalkulowane). W warunku serwerowym używaj pola bazodanowego `Numer.Pelny`
  (np. `dok => dok.Numer.Pelny == "FV 1/2026"`).
- **Korekty:** `dok.DokumentKorygowany` (dokument korygowany przez tę korektę),
  `dok.DokumentyKorygujące` (`IEnumerable<DokumentHandlowy>` — łańcuch korekt tego dokumentu),
  `dok.Korekta: bool` (pole bazodanowe — czy dokument jest korektą). Wszystkie powiązania korekt to
  pola **kalkulowane** (oprócz `Korekta`).
- **Kolekcje na `Kontrahent` (z modułu CRM):** `k.DokumentyHandlowe` i `k.DokumentyHandloweOdbiorcy`
  to **nietypowane** `SubTable` (CRM nie referuje Handlu). Iteracja działa, ale typowane filtrowanie
  serwerowe rób od strony Handlu: `hm.DokHandlowe.WgKontrahentaObcy[dok => dok.Kontrahent == k]`.

---

### HANDEL-W25 — Odczytanie pozycji dokumentu

**Cel:** przejść po pozycjach (towar, ilość, cena, rabat, wartość) wczytanego dokumentu — np. do
wydruku, eksportu czy przeliczeń własnych.

**Warianty:**

| Wariant | Źródło / operacja |
|---|---|
| Wszystkie pozycje wg Lp | `dok.Pozycje` (`LpSubTable`, sortowane po `Lp`) |
| Tylko pozycje danego towaru | `dok.Pozycje[(PozycjaDokHandlowego p) => p.Towar == towar]` |
| Pozycje o niezerowej ilości | warunek serwerowy na `p.Ilosc.Value` |
| Wartości pozycji | `p.WartoscCy`, `p.Suma` (`BruttoNetto`: `NettoCy`/`VATCy`/`BruttoCy`) |

**Pola i typy (`PozycjaDokHandlowego`):** `Towar: Towar`, `Ilosc: Quantity`
(`.Value`, `.Symbol`), `Cena: DoubleCy`, `Rabat: Percent`, `WartoscCy: Currency`,
`Suma: BruttoNetto` (`NettoCy`, `VATCy`, `BruttoCy` — typ `Currency`; `Netto`/`VAT`/`Brutto` — `decimal`),
`Lp: int`, `Stawka: StawkaVat`, `Opis: string`.

**Snippet:**

```csharp
var hm = session.GetHandel();
var dok = hm.DokHandlowe[guid];                 // dokument wczytany po Guid (HANDEL-W29)
if (dok == null) return;

// Iteracja po pozycjach (LpSubTable jest już posortowana po Lp):
foreach (PozycjaDokHandlowego p in dok.Pozycje)
{
    string towar  = p.Towar?.Kod;
    Quantity ilosc = p.Ilosc;                   // p.Ilosc.Value + p.Ilosc.Symbol (jednostka)
    DoubleCy cena = p.Cena;
    Percent rabat = p.Rabat;
    Currency netto  = p.Suma.NettoCy;           // wartość netto pozycji w PLN
    Currency brutto = p.Suma.BruttoCy;
    Currency wartosc = p.WartoscCy;             // wartość pozycji w walucie ceny
}

// Tylko pozycje wybranego towaru — filtr serwerowy na kolekcji:
var towar = session.GetTowary().Towary.WgKodu["BIKINI"];
foreach (PozycjaDokHandlowego p in dok.Pozycje[(PozycjaDokHandlowego p) => p.Towar == towar])
{
    // ...
}
```

**Pułapki:**
- `Ilosc` to `Quantity`, a `Cena`/`WartoscCy` to `DoubleCy`/`Currency` (kwota + waluta), **nie**
  `decimal`/`double` (safe-code §10). Składowe: `p.Ilosc.Value`, `p.Ilosc.Symbol`.
- Do filtrowania pozycji **na jednym dokumencie** możesz iterować `dok.Pozycje` (to mała kolekcja),
  ale i tak preferuj warunek `dok.Pozycje[p => …]` — wykona się serwerowo.
- `p.Suma`/`p.WartoscCy` są przeliczane przez platformę — czytaj je, nie wyliczaj „ręcznie".
- `p.Towar` bywa `null` dla pozycji nietowarowych (opis/koszt) — zabezpiecz dostęp (`?.`).

---

### HANDEL-W26 — Odczytanie dokumentów dla kontrahenta

**Cel:** pobrać dokumenty wystawione na danego kontrahenta — jako nabywcę (`Kontrahent`) lub jako
odbiorcę (`Odbiorca`).

**Warianty:**

| Wariant | Źródło | Typ |
|---|---|---|
| Kontrahent jako nabywca (kolekcja CRM) | `k.DokumentyHandlowe` | nietypowany `SubTable` |
| Odbiorca (kolekcja CRM) | `k.DokumentyHandloweOdbiorcy` | nietypowany `SubTable` |
| Filtr typowany od strony Handlu | `hm.DokHandlowe.WgKontrahentaObcy[dok => dok.Kontrahent == k]` | `SubTable<DokumentHandlowy>` |
| Zawężenie okresem | dołóż `&& dok.Data >= od` w warunku | — |

**Pola i typy:** `dok.Kontrahent: Kontrahent`, `dok.Odbiorca: Kontrahent` (oba bazodanowe).
`Kontrahent.DokumentyHandlowe` / `DokumentyHandloweOdbiorcy` to kolekcje `SubTable` na kontrahencie
(zawężone już do jednego kontrahenta).

**Snippet:**

```csharp
var hm = session.GetHandel();
var k = session.GetCRM().Kontrahenci.WgKodu["Abc"];
if (k == null) return;

// Wariant A — kolekcja na kontrahencie (nietypowana, ale wygodna do prostego przejścia):
foreach (DokumentHandlowy dok in k.DokumentyHandlowe)
{
    // dok.Numer.NumerPelny, dok.Data, dok.Suma ...
}

// Wariant B — typowany filtr serwerowy od strony Handlu + zawężenie okresem
// (klucz WgKontrahentaObcy nadaje sortowanie wg kontrahenta):
var od = Date.Today.AddMonths(-3);
foreach (DokumentHandlowy dok in hm.DokHandlowe.WgKontrahentaObcy[
             (DokumentHandlowy dok) => dok.Kontrahent == k && dok.Data >= od])
{
    // tylko dokumenty kontrahenta z ostatnich 3 miesięcy
}

// Dokumenty, w których kontrahent jest ODBIORCĄ:
foreach (DokumentHandlowy dok in hm.DokHandlowe[
             (DokumentHandlowy dok) => dok.Odbiorca == k])
{
    // ...
}
```

**Pułapki:**
- `k.DokumentyHandlowe` jest **nietypowane** (`SubTable`, nie `SubTable<DokumentHandlowy>`) — pętla
  `foreach (DokumentHandlowy …)` działa, ale do filtrowania wyrażeniem LINQ użyj kolekcji od strony
  Handlu (`hm.DokHandlowe.WgXxx[…]`), gdzie typ wiersza jest znany kompilatorowi.
- `Kontrahent` i `Odbiorca` to **dwa różne pola** — wybierz świadomie (nabywca ≠ odbiorca towaru).
- To dane operacyjne — przy szerokich analizach **zawężaj okres** (`dok.Data >= od`), nie ładuj całej
  historii (safe-code §6.3).
- Porównuj po referencji rekordu (`dok.Kontrahent == k`), a nie po `Kod` — referencja generuje
  szybkie `JOIN` po `ID`.

---

### HANDEL-W27 — Ostatnie pozycje dokumentów dla wskazanego towaru

**Cel:** prześledzić historię obrotu danym towarem — pozycje dokumentów, w których towar wystąpił
(np. ostatnie zakupy/sprzedaże, kierunek magazynowy, ceny historyczne).

**Warianty:**

| Wariant | Źródło / warunek |
|---|---|
| Wszystkie pozycje towaru | `towar.Pozycje` (klucz `WgTowar`) |
| Tylko rozchody / przychody | filtr na `p.KierunekMagazynu` (`KierunekPartii`) |
| Z zakresu dat | `towar.Pozycje[p => p.Data >= od]` |
| Tylko z dokumentów zatwierdzonych | warunek przez referencję: `p.Dokument.Stan == StanDokumentuHandlowego.Zatwierdzony` |
| Ostatnie N po dacie | sortuj kluczem `WgKierunek`/`WgDaty` i ogranicz w pamięci po zawężeniu |

**Pola i typy (`PozycjaDokHandlowego`):** `Towar: Towar`, `Dokument: DokumentHandlowy`,
`Data: Date`, `Czas: Time`, `KierunekMagazynu: Soneta.Magazyny.KierunekPartii`
(`Rozchód=-1`, `Brak=0`, `Przychód=1`), `Cena: DoubleCy`, `Ilosc: Quantity`. Kolekcja
`towar.Pozycje: SubTable<PozycjaDokHandlowego>`.

**Snippet:**

```csharp
var towar = session.GetTowary().Towary.WgKodu["BIKINI"];
if (towar == null) return;

// Pozycje towaru z ostatnich 6 miesięcy — filtr serwerowy na kolekcji towaru:
var od = Date.Today.AddMonths(-6);
foreach (PozycjaDokHandlowego p in towar.Pozycje[(PozycjaDokHandlowego p) => p.Data >= od])
{
    DokumentHandlowy dok = p.Dokument;          // dokument macierzysty pozycji
    string numer = dok.Numer.NumerPelny;
    // p.KierunekMagazynu, p.Ilosc, p.Cena, p.Data ...
}

// Tylko rozchody (sprzedaż/wydania) danego towaru z dokumentów zatwierdzonych:
foreach (PozycjaDokHandlowego p in towar.Pozycje[(PozycjaDokHandlowego p) =>
             p.KierunekMagazynu == KierunekPartii.Rozchód
             && p.Dokument.Stan == StanDokumentuHandlowego.Zatwierdzony
             && p.Data >= od])
{
    // historia rozchodów towaru
}
```

**Pułapki:**
- Filtruj na `towar.Pozycje[…]` (kolekcja zawężona do jednego towaru), nie iteruj globalnie
  `PozycjeDokHan` — to jedna z największych tabel operacyjnych (safe-code §6.3).
- Warunek przez referencję (`p.Dokument.Stan == …`) jest dozwolony — `Stan` jest polem
  bazodanowym i wygeneruje `JOIN`. Nie używaj w warunku pól kalkulowanych dokumentu
  (np. `p.Dokument.Zatwierdzony` rzuci `LinqConditionException`).
- „Ostatnie N" realizuj przez sortowanie kluczem (`WgKierunek`/`WgDaty`) **po** zawężeniu okresem;
  nie pobieraj całości po to, by wziąć kilka rekordów.
- `KierunekPartii` żyje w `Soneta.Magazyny` — wymagana referencja do modułu Magazyny.

---

### HANDEL-W28 — Wyszukiwanie dokumentów wg okresu, definicji, stanu, serii

**Cel:** odfiltrować dokumenty po kryteriach nagłówkowych (data, definicja, stan, magazyn, seria)
serwerowo, bez obiektów warstwy UI (`View`).

**Warianty:**

| Wariant | Warunek (pole bazodanowe) |
|---|---|
| Okres dat | `dok.Data >= od && dok.Data <= do` |
| Konkretna definicja (symbol) | `dok.Definicja == def` (rekord z `DefDokHandlowych.WgSymbolu[...]`) |
| Stan dokumentu | `dok.Stan == StanDokumentuHandlowego.Zatwierdzony` |
| Magazyn | `dok.Magazyn == mag` |
| Seria | `dok.Seria == "A"` |
| Wiele kryteriów | koniunkcja `&&` / alternatywa `||` w jednym wyrażeniu |

**Pola i typy:** `dok.Data: Date`, `dok.Definicja: DefDokHandlowego`,
`dok.Stan: StanDokumentuHandlowego`, `dok.Magazyn: Magazyn`, `dok.Seria: string`,
`dok.Kategoria: KategoriaHandlowa`. Klucz `WgDaty` daje sortowanie po dacie.

**Snippet:**

```csharp
var hm = session.GetHandel();

var def  = hm.DefDokHandlowych.WgSymbolu["FV"];                 // definicja faktury sprzedaży
var mag  = session.GetMagazyny().Magazyny.WgSymbol["F"];
var od   = new Date(2026, 1, 1);
var doDt = new Date(2026, 3, 31);

// Zatwierdzone faktury FV z I kwartału na magazynie F — jeden warunek serwerowy.
// Klucz WgDaty nadaje sortowanie po Data, Czas:
foreach (DokumentHandlowy dok in hm.DokHandlowe.WgDaty[(DokumentHandlowy dok) =>
             dok.Definicja == def
             && dok.Magazyn == mag
             && dok.Stan == StanDokumentuHandlowego.Zatwierdzony
             && dok.Data >= od && dok.Data <= doDt])
{
    // dok.Numer.NumerPelny, dok.Suma, dok.Kontrahent ...
}

// Wariant: warunek jako wartość przekazywana dalej (np. do metody):
var cond = RowCondition.FromExpression<DokumentHandlowy>(
    dok => dok.Definicja == def && dok.Seria == "A");
foreach (DokumentHandlowy dok in hm.DokHandlowe.WgDaty[cond]) { /* ... */ }
```

**Pułapki:**
- **Nie używaj `View`** w kodzie biznesowym (to obiekt UI) — filtruj `SubTable[expression]` lub
  `RowCondition.FromExpression` ([`rowcondition.md`](../rowcondition.md)).
- Porównuj definicję/magazyn po **rekordzie** (`dok.Definicja == def`), nie po stringu symbolu —
  rekord pobierz raz przez `WgSymbolu[...]`/`WgSymbol[...]` poza pętlą.
- Stan porównuj enumem (`dok.Stan == StanDokumentuHandlowego.Zatwierdzony`); skróty `dok.Zatwierdzony`
  są kalkulowane i **nie wolno** ich użyć w warunku LINQ.
- Wybór klucza (`WgDaty`, `WgMagazynuNumer`, `WgKontrahentaObcy`) decyduje tylko o **sortowaniu** —
  warunek i tak trafia do `WHERE`. Dla dużych zbiorów dobierz klucz pasujący do oczekiwanej kolejności.

---

### HANDEL-W29 — Odczyt dokumentu wg numeru lub Guid

**Cel:** odnaleźć pojedynczy dokument po jego pełnym numerze (`Numer.Pelny`) albo po globalnym
identyfikatorze `Guid` (np. zapisanym wcześniej w innym systemie / w teście).

**Warianty:**

| Wariant | Mechanizm | Zwraca |
|---|---|---|
| Po Guid | `hm.DokHandlowe[guid]` (indeksator `GuidedTable`) | `DokumentHandlowy`; **rzuca `RowNotFoundException`**, gdy brak |
| Po pełnym numerze | filtr serwerowy `dok => dok.Numer.Pelny == numer` | zbiór (bierz `.FirstOrDefault()`) |
| Po numerze w obrębie magazynu | klucz `WgMagazynuNumer` (`Magazyn` + `Numer.Pelny`) | precyzyjniej (numer bywa unikalny per magazyn) |
| Po numerze obcym | klucz `WgMagazynuObcy` / pole `dok.Obcy.Numer` | dokument z numerem dostawcy |

**Pola i typy:** `dok.Numer: NumerDokumentu` (odczyt pełnego numeru: `dok.Numer.NumerPelny`;
pole bazodanowe w warunku: `Numer.Pelny`), `dok.Guid: Guid` (z `GuidedRow`),
`dok.Obcy.Numer: string` (numer dokumentu obcego).

**Snippet:**

```csharp
var hm = session.GetHandel();

// 1. Po Guid — najpewniejszy, jednoznaczny dostęp. UWAGA: indeksator GuidedTable RZUCA
//    RowNotFoundException dla nieznanego Guid (nie zwraca null) — obuduj try/catch, gdy brak pewności:
DokumentHandlowy poGuid;
try { poGuid = hm.DokHandlowe[guid]; }
catch (Soneta.Business.RowNotFoundException) { poGuid = null; }

// 2. Po pełnym numerze — warunek serwerowy na polu bazodanowym Numer.Pelny.
//    Numer może się powtarzać między magazynami, więc bierzemy pierwszy / iterujemy:
DokumentHandlowy poNumerze = hm.DokHandlowe.WgMagazynuNumer[
    (DokumentHandlowy dok) => dok.Numer.Pelny == "FV 1/2026"].FirstOrDefault();

// 3. Po numerze w obrębie magazynu (precyzyjniej — numeracja zwykle per magazyn):
var mag = session.GetMagazyny().Magazyny.WgSymbol["F"];
DokumentHandlowy wMagazynie = hm.DokHandlowe.WgMagazynuNumer[(DokumentHandlowy dok) =>
    dok.Magazyn == mag && dok.Numer.Pelny == "FV 1/2026"].FirstOrDefault();

if (poGuid != null)
{
    string pelny = poGuid.Numer.NumerPelny;     // odczyt pełnego numeru (kalkulowane)
}
```

**Pułapki:**
- W warunku LINQ używaj pola bazodanowego `Numer.Pelny`; do **odczytu** sformatowanego numeru służy
  kalkulowane `dok.Numer.NumerPelny` — w wyrażeniu serwerowym rzuciłoby `LinqConditionException`.
- Pełny numer **nie jest** globalnie unikalny (numeracja bywa per magazyn/seria/rok) — dlatego filtr
  zwraca zbiór; bierz `.FirstOrDefault()` albo dołóż `dok.Magazyn == mag`.
- Indeksator `hm.DokHandlowe[guid]` to dostęp po `Guid` (z `GuidedTable`) — dla nieznanego `Guid`
  **rzuca `Soneta.Business.RowNotFoundException`** (NIE zwraca `null`). Gdy brak pewności istnienia,
  obuduj go `try/catch`. Nie myl z dostępem po `ID` (klucz wewnętrzny tabeli).
- Numer obcy (dostawcy) jest w `dok.Obcy.Numer` — to inne pole niż własny `Numer`.

---

### HANDEL-W30 — Korekty dokumentu i dokument korygowany

**Cel:** dla danego dokumentu ustalić jego korekty (dokumenty korygujące) oraz — dla korekty —
dokument, który koryguje.

**Warianty:**

| Wariant | Pole / kierunek | Typ |
|---|---|---|
| Dokument korygowany przez tę korektę | `korekta.DokumentKorygowany` | `DokumentHandlowy` (lub `null`) |
| Wszystkie korekty danego dokumentu | `dok.DokumentyKorygujące` | `IEnumerable<DokumentHandlowy>` (łańcuch) |
| Najbliższa korekta | `dok.DokumentKorygujący` | `DokumentHandlowy` (lub `null`) |
| Ostatnia korekta w łańcuchu | `dok.DokumentKorygującyOstatni` | `DokumentHandlowy` |
| Czy dokument jest korektą | `dok.Korekta` | `bool` (pole bazodanowe) |
| Serwerowy filtr korekt | `hm.DokHandlowe[d => d.Korekta]` | `SubTable<DokumentHandlowy>` |

**Pola i typy:** `dok.Korekta: bool` (bazodanowe — czy dokument jest korektą),
`dok.DokumentKorygowany: DokumentHandlowy`, `dok.DokumentyKorygujące: IEnumerable<DokumentHandlowy>`,
`dok.DokumentKorygujący`/`DokumentKorygującyOstatni: DokumentHandlowy`,
`dok.DokumentyKorygowane: IEnumerable<DokumentHandlowy>` (cały łańcuch korygowanych) —
wszystkie powiązania **kalkulowane** (tylko do odczytu; korekty zakładaj przez `IRelacjeService`).

**Snippet:**

```csharp
var hm = session.GetHandel();
var dok = hm.DokHandlowe[guid];
if (dok == null) return;

// Korekty tego dokumentu (łańcuch korekt — kolejne korekty korekt):
foreach (DokumentHandlowy korekta in dok.DokumentyKorygujące)
{
    string nr = korekta.Numer.NumerPelny;
    DokumentHandlowy korygowany = korekta.DokumentKorygowany;   // wskazuje z powrotem na dok
}

// Gdy mamy w ręku korektę — odczyt dokumentu korygowanego:
if (dok.Korekta)
{
    DokumentHandlowy zrodlo = dok.DokumentKorygowany;           // dokument pierwotny
}

// Serwerowe wyszukanie samych korekt w okresie (pole Korekta jest bazodanowe):
var od = Date.Today.AddMonths(-1);
foreach (DokumentHandlowy k in hm.DokHandlowe.WgDaty[(DokumentHandlowy d) =>
             d.Korekta && d.Data >= od])
{
    // d.DokumentKorygowany — dokument, którego dotyczy korekta
}
```

**Pułapki:**
- `DokumentKorygowany`/`DokumentyKorygujące`/`DokumentKorygujący` są **kalkulowane** (liczone z
  relacji handlowych) — tylko do odczytu. Tworzenie korekt realizuje `IRelacjeService.NowaKorekta(...)`
  (rozdział o relacjach), nie przypisywanie tych pól.
- W warunku serwerowym wolno użyć tylko pola **`Korekta`** (bazodanowe). Pola powiązań korekt są
  kalkulowane → w LINQ rzucą `LinqConditionException`.
- `DokumentKorygowany` zwraca `null`, gdy dokument **nie** jest korektą (`Korekta == false`) — zawsze
  sprawdź `dok.Korekta` albo `!= null` przed użyciem.
- `DokumentyKorygujące` to **łańcuch** (korekta korekty korekty…), a nie pojedynczy element — gdy
  potrzebujesz tylko najbliższej, użyj `DokumentKorygujący`; gdy ostatniej — `DokumentKorygującyOstatni`.

---

