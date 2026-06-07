# HANDEL06 — Magazyn, zasoby, partie, obroty

> Wspólne fakty o typie, podstawowe typy i szablon wzorca: [../handel.md](../handel.md).

> Sekcja opisuje **odczyt** efektów magazynowych dokumentu (zasoby, obroty) oraz
> **sterowanie** rozchodem przez wskazanie partii (`GrupaDostaw`) i kontekst wyceny
> (FIFO/LIFO/wg dostaw). Cały kod operuje wyłącznie na **publicznym kontrakcie**
> platformy i jest zgodny z C# 10.
>
> **Klucz do zrozumienia całej sekcji:** magazyn księguje obroty i zasoby **dopiero po
> `Session.Save()`** dokumentu. Samo `Commit()`/`CommitUI()` w transakcji nie nalicza
> stanów. W bazie Demo działa `StanUjemnyVerifier` — **rozchód** (FV/WZ/RW) wymaga
> wcześniejszego **zapisanego** przyjęcia (PW/PZ) tego towaru; w przeciwnym razie zapis
> rozchodu zostanie odrzucony.
>
> **Słowniczek typów (moduł `Soneta.Magazyny`):**
> - `Zasob` (tabela `Zasoby`) — stan towaru: ilość na partii w danym magazynie i okresie.
> - `Obrot` (tabela `Obroty`) — pojedynczy ruch (przychód lub rozchód) wiążący partie.
> - `GrupaDostaw` (tabela `GrupyDostaw`, namespace `Soneta.Magazyny.Dostawy`) — **partia**
>   towaru (identyfikowana `Numer` + `Towar`).
> - `OkresMagazynowy` (tabela `OkresyMag`) — przedział czasu, w którym ewidencjonowane są
>   obroty/zasoby; po zamknięciu blokuje modyfikacje.
> - `PartiaTowaru` — **subrow** (nie tabela) opisujący stronę partii w `Obrot`/`Zasob`:
>   `Dokument`, `PozycjaIdent`, `PartiaTowaru: GrupaDostaw`, `KontrahentPartii`, `Data`, `Czas`, `Typ`, `Wartosc`.
> - Enum `KierunekPartii`: `Rozchód=-1`, `Brak=0`, `Przychód=1`.
> - Enum `Magazyn.Algorytm` (`AlgorytmMagazynowy`): `FIFO=0`, `LIFO=1`, `NieLiczyćStanów=2`,
>   `WgDostawy=3`, `WgDostawyPrzyZatwierdzaniu=10`, `OdNajdroższych=4`, `OdNajtańszych=5`,
>   `WgCechyPozycji=6/7`, `WgCechyDokumentu=8/9`.
>
> Dostęp do modułu: `var mag = session.GetMagazyny();` → `mag.Zasoby`, `mag.Obroty`,
> `mag.GrupyDostaw`, `mag.OkresyMag`, `mag.Magazyny`.

---

### HANDEL-W31 — Przeglądanie zasobów utworzonych przez dokument przychodowy (`dok.Zasoby`)

**Cel:** po zapisaniu dokumentu przychodowego (PW/PZ/FZ) odczytać zasoby magazynowe,
które ten dokument wprowadził na stan — np. żeby zweryfikować ilości albo powiązać je z
partią.

**Warianty:**

| Wariant | Źródło | Uwaga |
|---|---|---|
| Zasoby utworzone bezpośrednio przez dokument | `dok.Zasoby` (`SubTable<Zasob>`) | filtr po `Partia.Dokument == dok` |
| Zasoby łącznie z dokumentami zależnymi | `dok.ZasobyWszystkie` (`ListWithView`) | obejmuje powiązane dok. magazynowe |
| Iteracja po module | `mag.Zasoby.WgTowar[towar, okres, magazyn]` | gdy nie mamy uchwytu do dokumentu |

**Pola i typy:** `dok.Zasoby: SubTable` (elementy `Soneta.Magazyny.Zasob`). `Zasob`:
`Ilosc: Quantity`, `IloscRezerwowana: Quantity`, `Kierunek: KierunekPartii`,
`Magazyn: Magazyn`, `Towar: Towar`, `Okres: OkresMagazynowy`, `Partia: PartiaTowaru` (subrow),
`PartiaPierwotna: PartiaTowaru`.

**Snippet:**

```csharp
// dok — zapisany dokument przychodowy (PW/PZ/FZ), po session.Save()
var mag = session.GetMagazyny();

foreach (Zasob z in dok.Zasoby)
{
    // strona partii zasobu: skąd pochodzi (dokument, pozycja, numer partii)
    GrupaDostaw partia = z.Partia.PartiaTowaru;   // rekord partii (może być null dla prostej ewidencji)
    Console.WriteLine(
        $"{z.Towar.Kod}  mag={z.Magazyn.Symbol}  kierunek={z.Kierunek}  " +
        $"ilość={z.Ilosc}  partia={partia?.Numer}");
}
```

**Pułapki:**
- `dok.Zasoby` jest **puste, dopóki nie wykonasz `session.Save()`** — przed zapisem magazyn
  nie zaksięgował zasobów (sam `Commit`/`CommitUI` nie wystarcza).
- Wzorzec testowy: zapis dokumentu → `SaveDispose()` → odczyt na świeżej sesji po `Guid`,
  bo po `Save()` w środku testu okno edycji się zamyka.
- Zasób przychodowy ma `Kierunek == KierunekPartii.Przychód`. Zasób rozchodowy na stanie
  ujemnym ma `Kierunek == KierunekPartii.Rozchód` — nie myl ich przy sumowaniu stanu.
- Nie modyfikuj `Zasob`/`Obrot` ręcznie — to tabele wyliczane przez moduł magazynowy.

---

### HANDEL-W32 — Przetwarzanie obrotów faktury sprzedaży i dokumentu rozchodowego (`dok.Obroty`, `dok.ObrotyWszystkie`)

**Cel:** odczytać obroty magazynowe (ruchy) wygenerowane przez dokument — rozchód
(FV/WZ/RW) lub przychód — w tym obroty z dokumentów zależnych.

**Warianty:**

| Wariant | Property | Co zwraca |
|---|---|---|
| Obroty związane bezpośrednio z dokumentem | `dok.Obroty` (`SubTable`) | dla przychodu: po stronie przychodowej; dla rozchodu: po stronie rozchodowej |
| Wszystkie obroty (z dok. zależnymi, bez storna zasobu) | `dok.ObrotyWszystkie` (`ListWithView`) | obroty wszystkich powiązanych dok. magazynowych |
| Obroty wszystkich pozycji | `dok.ObrotyWszystkiePozycji` (`ListWithView`) | po pozycjach (z pozycjami zależnymi) |
| Z korektami, wg partii pierwotnej | `dok.ObrotyWszystkieWgPartiiPierwotnej` (`ListWithView`) | uwzględnia dok. korygujące |

**Pola i typy:** `Obrot`: `Ilosc: Quantity`, `Towar: Towar`, `Magazyn: Magazyn`,
`Okres: OkresMagazynowy`, `Data: Date`, `Czas: Time`, `Korekta: KorektaObrotu`,
`Stornowany: Obrot`, `Przychod: PartiaTowaru`, `Rozchod: PartiaTowaru`,
`PrzychodPierwotny: PartiaTowaru`.

**Snippet:**

```csharp
// dok — zapisana faktura sprzedaży / dokument rozchodowy (po session.Save())
// 1) Obroty samego dokumentu (strona dobrana automatycznie wg kierunku magazynu):
foreach (Obrot o in dok.Obroty)
{
    // Przychod/Rozchod to subrow PartiaTowaru — wskazuje partię i dokument źródłowy
    GrupaDostaw partiaRozchodu = o.Rozchod.PartiaTowaru;     // z której partii zszedł towar
    GrupaDostaw partiaPrzychodu = o.Przychod.PartiaTowaru;   // partia przychodowa (źródło)
    Console.WriteLine($"{o.Towar.Kod}  ilość={o.Ilosc}  z partii={partiaPrzychodu?.Numer}");
}

// 2) Wszystkie obroty łącznie z dokumentami magazynowymi powiązanymi z fakturą:
foreach (Obrot o in dok.ObrotyWszystkie.Cast<Obrot>())
{
    if (o.Korekta == KorektaObrotu.StornoZasobu) continue;   // ObrotyWszystkie już to pomija
    // ... agregacja ilości/wartości
}
```

**Pułapki:**
- `dok.Obroty` automatycznie dobiera stronę (przychodowa vs rozchodowa) na podstawie
  kierunku magazynowego dokumentu — nie filtruj jej ręcznie po kierunku.
- `ObrotyWszystkie`/`ObrotyWszystkiePozycji`/`ObrotyWszystkieWgPartiiPierwotnej` zwracają
  `ListWithView` — iteruj przez `.Cast<Obrot>()`. Pomijają już obroty `StornoZasobu`.
- Obroty pojawiają się **po `Session.Save()`** dokumentu, nie po `Commit()`.
- `Przychod`/`Rozchod`/`PrzychodPierwotny` to **subrow `PartiaTowaru`**, nie rekord partii —
  do rekordu `GrupaDostaw` sięgaj przez `.PartiaTowaru`, do dokumentu źródłowego przez
  `.Dokument`, do pozycji przez `.PozycjaIdent`.

---

### HANDEL-W33 — Odczyt stanu magazynowego towaru (magazyn / data) — `mag.Zasoby` z filtrem

**Cel:** wyliczyć aktualny stan towaru w danym magazynie (i ewentualnie okresie), bez
otwierania konkretnego dokumentu — np. do walidacji dostępności przed rozchodem.

**Warianty:**

| Wariant | Indeks | Sygnatura |
|---|---|---|
| Stan towaru w magazynie | `mag.Zasoby.WgTowar[towar, okres, magazyn]` | zawęź serwerowo do magazynu i okresu |
| Stan towaru we wszystkich okresach/magazynach | `mag.Zasoby.WgTowar[towar]` | szersze — sumuj ostrożnie |
| Zasoby konkretnej partii | `mag.Zasoby.WgPartiaTowaruMagazyn[partia, magazyn, towar]` | gdy znamy `GrupaDostaw` |
| Zasoby magazynu w okresie | `mag.Zasoby.WgMagazyn[magazyn, okres]` | przegląd całego magazynu |

**Pola i typy:** `mag.Zasoby: Zasoby` (tabela). Indeksy zwracają `SubTable<Zasob>`.
`OkresMagazynowy` z `mag.OkresyMag` (patrz HANDEL-W39). Ilości to `Quantity`.

**Snippet:**

```csharp
var mag = session.GetMagazyny();
var towar = session.GetTowary().Towary.WgKodu["BIKINI"];
var magazyn = mag.Magazyny.WgSymbol["F"];
var okres = mag.OkresyMag.WgOkres[Date.Today];   // okres obejmujący dzień (patrz HANDEL-W39)

// Stan = suma ilości zasobów przychodowych pomniejszona o rozchodowe (stan ujemny)
Quantity stan = new(0, towar.JednostkaMag.Symbol);
foreach (Zasob z in mag.Zasoby.WgTowar[towar, okres, magazyn])
{
    if (z.Kierunek == KierunekPartii.Przychód)
        stan += z.Ilosc;
    else if (z.Kierunek == KierunekPartii.Rozchód)
        stan -= z.Ilosc;
}
```

**Pułapki:**
- **Nie ładuj całej tabeli `Zasoby` do pamięci** — zawsze zawężaj indeksem
  (`WgTowar[...]`, `WgMagazyn[...]`, `WgPartiaTowaruMagazyn[...]`). Patrz `safe-code.md` §6.
- Ilości są typu `Quantity` (ilość + jednostka), nie `double` — operuj na `Quantity` i
  pilnuj zgodności jednostek (`z.Ilosc.Symbol`).
- Stan „na dzień" zależy od okresu magazynowego — dla daty historycznej wybierz właściwy
  `OkresMagazynowy`, nie zawsze bieżący.
- Towary **bez magazynu** (np. usługi „MONTAZ", „TRANSPORT" w Demo) nie mają zasobów —
  zapytanie zwróci pustą kolekcję.
- W bazie Demo stan ujemny jest blokowany przy zapisie rozchodu — odczyt stanu służy do
  wcześniejszej walidacji, ale ostateczną kontrolę i tak wykona `Session.Save()`.

---

### HANDEL-W34 — Wyszukiwanie partii magazynowych (`GrupaDostaw`) według cech

**Cel:** odnaleźć partię (`GrupaDostaw`) po numerze, towarze lub cesze (np. numer serii,
data ważności zapisana jako cecha), zanim wskażemy ją przy rozchodzie.

**Warianty:**

| Wariant | Klucz / mechanizm | Uwaga |
|---|---|---|
| Po numerze + towarze | `mag.GrupyDostaw.WgNumer[numer, towar]` | klucz unikalny — pojedynczy rekord lub null |
| Po numerze (zbiór) | `mag.GrupyDostaw.WgNumer[numer]` | zwraca `SubTable<GrupaDostaw>` |
| Wszystkie partie towaru | `mag.GrupyDostaw.WgTowar[towar]` | partie danego towaru |
| Po dacie | `mag.GrupyDostaw.WgData[data]` | indeks po `Data` |
| Po cesze | `partie[(GrupaDostaw g) => warunek]` na indeksie | cecha musi być zdefiniowana |

**Pola i typy:** `GrupaDostaw`: `Numer: string` (`public virtual`, czasem nadawany
automatycznie), `Towar: Towar`, `Data: Date`, `Blokada: bool`,
`Features: FeatureCollection`, `KodKreskowy: string`. Klucz `WgNumer` = (`Numer`, `Towar`).

**Snippet:**

```csharp
var mag = session.GetMagazyny();
var towar = session.GetTowary().Towary.WgKodu["BIKINI"];

// 1) Partia po numerze i towarze — klucz unikalny:
GrupaDostaw partia = mag.GrupyDostaw.WgNumer["LOT-2026-001", towar];

// 2) Wszystkie niezablokowane partie towaru — filtr serwerowy na indeksie:
foreach (GrupaDostaw g in mag.GrupyDostaw.WgTowar[(GrupaDostaw g) => !g.Blokada])
{
    // odczyt cechy zapisanej na partii (np. numer serii / data ważności):
    object seria = g.Features["NumerSerii"];   // cecha musi być wcześniej zdefiniowana
}

// 3) Filtr po dacie powstania partii:
foreach (GrupaDostaw g in mag.GrupyDostaw.WgData[Date.Today]) { /* ... */ }
```

**Pułapki:**
- `WgNumer[numer, towar]` zwraca **pojedynczy** rekord (może być `null`); `WgNumer[numer]`
  i `WgTowar[towar]` zwracają **zbiór** (`SubTable`).
- W `RowCondition` używaj tylko **pól bazodanowych** (`Numer`, `Towar`, `Data`, `Blokada`).
  Pola kalkulowane (np. `KodKreskowy`) i wartości cech rzucą `LinqConditionException` —
  cechę filtruj dopiero po materializacji albo przez dedykowany warunek na cesze.
- Cecha (`Features["…"]`) wymaga wcześniej zdefiniowanej definicji cechy — odwołanie do
  niezdefiniowanej cechy rzuca wyjątek (patrz `features.md`).
- `Numer` partii bywa **nadawany automatycznie** (autonumerowanie wg karty towaru lub wg
  cechy) — nie zakładaj, że zawsze ustawisz go ręcznie.

---

### HANDEL-W35 — Dokument rozchodowy ze wskazaniem JEDNEJ partii

**Cel:** wystawić rozchód (WZ/RW/FV), w którym pozycja schodzi z **konkretnej, wskazanej
partii** — a nie z partii wybranej automatycznie przez algorytm magazynu.

**Warianty:**

| Wariant | Mechanizm | Uwaga |
|---|---|---|
| Wskazanie partii przez pozycję dostawy | `poz.Dostawa = pozycjaPrzyjęcia` | `Dostawa: PozycjaDokHandlowego` (pozycja PW/PZ) |
| Wskazanie partii pierwotnej | `poz.DostawaPierwotna` | dla łańcucha korekt |
| Tryb wskazania na definicji | `DefDokHandlowego.WskazaniePartii` | `WyborPartiiOpcje` (Dozwolony/Wymuszony…) |
| Identyfikacja przez cechę | gdy magazyn `WgCechyPozycji` | partia wybierana wg cechy pozycji (HANDEL-W37, HANDEL-W39) |

**Pola i typy:** `poz.Dostawa: PozycjaDokHandlowego` (kategoria „Magazyn", opis „Pozycja
dostawy dla danego rozchodu magazynowego"). Tryb sterowany przez
`DefDokHandlowego.WskazaniePartii: WyborPartiiOpcje` (`Zabroniony=0`, `Dozwolony=1`,
`Automatyczny=2`, `Wymuszony=4`, `WymuszonyDodawanie`, `WymuszonyZatwierdzanie`,
`WgTowaru=8`).

**Snippet:**

```csharp
var mag = session.GetMagazyny();
var towar = session.GetTowary().Towary.WgKodu["BIKINI"];
var magazyn = mag.Magazyny.WgSymbol["F"];

// WARUNEK WSTĘPNY: istnieje ZAPISANE przyjęcie (PW/PZ) tego towaru (Demo blokuje stan ujemny).
// Znajdź pozycję przyjęcia odpowiadającą partii, z której chcemy zejść:
GrupaDostaw partia = mag.GrupyDostaw.WgNumer["LOT-2026-001", towar];
Obrot przychod = mag.Obroty.WgPrzychodPartiaTowaruMagazyn[partia, magazyn, towar]
                    .Cast<Obrot>().FirstOrDefault();
PozycjaDokHandlowego pozycjaPrzyjecia = przychod?.Przychod.Dokument?
    .Pozycje.Cast<PozycjaDokHandlowego>()
    .FirstOrDefault(p => p.Towar == towar);

using (var t = session.Logout(editMode: true))
{
    var dok = new DokumentHandlowy();
    session.AddRow(dok);
    dok.Definicja = session.GetHandel().DefDokHandlowych.WgSymbolu["WZ"];
    dok.Magazyn = magazyn;

    var poz = new PozycjaDokHandlowego(dok);
    session.AddRow(poz);
    poz.Towar = towar;                                     // USTAW PIERWSZY
    poz.Ilosc = new Quantity(2, poz.Ilosc.Symbol);
    poz.Dostawa = pozycjaPrzyjecia;                        // WSKAZANIE JEDNEJ partii (dostawy)
    t.Commit();                                            // CommitUI() w workerze/extenderze
}
session.Save();                                            // tu nalicza się obrót/zasób rozchodowy
```

**Pułapki:**
- Wskazanie partii działa tylko, gdy definicja dokumentu na to pozwala
  (`WskazaniePartii != Zabroniony`). Przy `Zabroniony` partia jest dobierana wyłącznie
  algorytmem magazynu — ustawienie `poz.Dostawa` zostanie zignorowane lub odrzucone.
- `poz.Dostawa` to **pozycja dokumentu przyjęcia** (`PozycjaDokHandlowego`), a nie rekord
  `GrupaDostaw`. Partię `GrupaDostaw` mapujesz na pozycję przyjęcia przez obrót przychodowy
  (`Obrot.Przychod.Dokument` + `PozycjaIdent`) — jak w snippetcie.
- Demo blokuje stan ujemny: bez **zapisanego** przyjęcia tej partii `Session.Save()`
  rozchodu rzuci wyjątek (`StanUjemnyVerifier`).
- Pozycje obu dokumentów muszą być w **tej samej sesji** — nie mieszaj rekordów z różnych
  sesji (`session.Get(...)`).
- Ustaw `poz.Dostawa` **przed** `Commit()`; właściwy obrót zostaje naliczony dopiero w
  `Save()`.

---

### HANDEL-W36 — Dokument rozchodowy ze wskazaniem WIELU partii

**Cel:** wystawić rozchód, którego ilość pochodzi z **kilku różnych partii** (np. 10 szt:
6 z LOT-A, 4 z LOT-B) — każda partia jako osobna pozycja rozchodu wskazująca swoją dostawę.

**Warianty:**

| Wariant | Mechanizm | Uwaga |
|---|---|---|
| Pozycja per partia | po jednej `PozycjaDokHandlowego` na każdą wskazaną dostawę | najprostszy, czytelny |
| Wybór przez worker dostaw | `IRelacjeService` + `HandlerSet.WybierzDostawyCallback` | dla relacji nadrzędny→podrzędny |
| Automatyczny rozdział wg algorytmu | `WskazaniePartii = Automatyczny` | platforma sama dzieli na partie |

**Pola i typy:** jak HANDEL-W35 — wiele pozycji, każda z własnym `poz.Dostawa` i `poz.Ilosc`.
Przy generowaniu z dokumentu nadrzędnego: `IRelacjeService.NowyPodrzednyIndywidualny(...)`
z `HandlerSet { WybierzDostawyCallback = ... }` (namespace
`Soneta.Handel.RelacjeDokumentow.Api`, wymaga `using Microsoft.Extensions.DependencyInjection;`).

**Snippet:**

```csharp
var mag = session.GetMagazyny();
var towar = session.GetTowary().Towary.WgKodu["BIKINI"];
var magazyn = mag.Magazyny.WgSymbol["F"];

// Mapowanie: numer partii -> ilość do zejścia
var rozdzial = new (string numer, double ilosc)[] { ("LOT-A", 6), ("LOT-B", 4) };

using (var t = session.Logout(editMode: true))
{
    var dok = new DokumentHandlowy();
    session.AddRow(dok);
    dok.Definicja = session.GetHandel().DefDokHandlowych.WgSymbolu["WZ"];
    dok.Magazyn = magazyn;

    foreach (var (numer, ilosc) in rozdzial)
    {
        GrupaDostaw partia = mag.GrupyDostaw.WgNumer[numer, towar];
        Obrot przychod = mag.Obroty.WgPrzychodPartiaTowaruMagazyn[partia, magazyn, towar]
                            .Cast<Obrot>().FirstOrDefault();
        PozycjaDokHandlowego dostawa = przychod?.Przychod.Dokument?
            .Pozycje.Cast<PozycjaDokHandlowego>().FirstOrDefault(p => p.Towar == towar);

        var poz = new PozycjaDokHandlowego(dok);
        session.AddRow(poz);
        poz.Towar = towar;
        poz.Ilosc = new Quantity(ilosc, poz.Ilosc.Symbol);
        poz.Dostawa = dostawa;                 // każda pozycja wskazuje INNĄ partię
    }
    t.Commit();
}
session.Save();
```

**Pułapki:**
- Każda wskazana partia = **osobna pozycja** rozchodu. Nie da się jedną pozycją wskazać
  dwóch różnych partii — `poz.Dostawa` to pojedyncza referencja.
- Suma ilości wskazanych partii musi mieścić się w zapisanym stanie każdej partii
  (Demo blokuje stan ujemny per partia).
- Przy generowaniu z dokumentu nadrzędnego (ZO→FV) wybór wielu dostaw realizuje
  `HandlerSet.WybierzDostawyCallback` — brak implementacji callbacku przy
  `WyborPozycjiDlaRelacji != BrakOkna` skutkuje `NotImplementedException`.
- Wszystkie pozycje w jednej transakcji edycyjnej, zapis raz przez `Session.Save()`.

---

### HANDEL-W37 — Dokument przyjęcia (PW/PZ) z numerem serii — zapis numeru serii jako cecha

**Cel:** zarejestrować przyjęcie towaru i zapisać **numer serii / partii**. Jeśli nie ma
dedykowanego pola na serię, numer przenosimy jako **cechę** (`Features`) pozycji/dokumentu,
skąd platforma przenosi go na partię (`GrupaDostaw`) i obrót.

**Warianty:**

| Wariant | Mechanizm | Uwaga |
|---|---|---|
| Numer partii wprost | `GrupaDostaw.Numer` | gdy partia jest tworzona/wskazywana jawnie |
| Numer serii jako cecha pozycji | `poz.Features["NumerSerii"] = "..."` | przenoszony na partię/obrót |
| Autonumerowanie wg cechy | `WyborPartiiAutonumerowanie.WgCechy` | numer partii brany z cechy |
| Data ważności jako cecha | `poz.Features["DataWaznosci"] = date` | analogicznie do serii |

**Pola i typy:** `dok.Features["…"]` i `poz.Features["…"]`
(`FeatureCollection`, indeksator po nazwie definicji cechy, zwraca/przyjmuje `object`).
`GrupaDostaw.Numer: string`. Tryb numeracji partii:
`WyborPartiiAutonumerowanie` (`Brak=0`, `Standardowe=1`, `WgCechy=2`).

**Snippet:**

```csharp
var mag = session.GetMagazyny();
var towar = session.GetTowary().Towary.WgKodu["BIKINI"];

using (var t = session.Logout(editMode: true))
{
    var dok = new DokumentHandlowy();
    session.AddRow(dok);
    dok.Definicja = session.GetHandel().DefDokHandlowych.WgSymbolu["PW"];   // przyjęcie
    dok.Magazyn = mag.Magazyny.WgSymbol["F"];

    var poz = new PozycjaDokHandlowego(dok);
    session.AddRow(poz);
    poz.Towar = towar;
    poz.Ilosc = new Quantity(10, poz.Ilosc.Symbol);
    poz.Cena = new DoubleCy(5m, poz.Cena.Symbol);

    // Numer serii jako cecha pozycji — przeniesiony na partię/obrót po Save:
    poz.Features["NumerSerii"] = "LOT-2026-001";    // definicja cechy musi istnieć
    t.Commit();
}
session.Save();

// Po zapisie partia jest dostępna w GrupyDostaw; numer serii odczytasz z cechy partii:
GrupaDostaw partia = mag.GrupyDostaw.WgTowar[towar].Cast<GrupaDostaw>()
    .FirstOrDefault(g => Equals(g.Features["NumerSerii"], "LOT-2026-001"));
```

**Pułapki:**
- Cecha musi być **wcześniej zdefiniowana** (`FeatureSetDefinition`) i — by przenosiła się
  na partię — odpowiednio skonfigurowana w module magazynowym. Odwołanie do niezdefiniowanej
  cechy rzuca wyjątek.
- Partia powstaje dopiero **po `Session.Save()`** przyjęcia — przed zapisem
  `mag.GrupyDostaw` jej nie zawiera.
- Gdy magazyn ma autonumerowanie `WgCechy`, `GrupaDostaw.Numer` jest **wyliczany z cechy** —
  nie ustawiaj go ręcznie sprzecznie z cechą.
- Filtr partii po wartości cechy rób **po materializacji** (jak w snippetcie) — wartości
  cech nie są polami bazodanowymi, więc nie wejdą do `RowCondition`.

---

### HANDEL-W38 — Odczyt rozchodu zasobów: powiązanie pozycji rozchodu z partią pierwotną / przyjęciem

**Cel:** dla pozycji/obrotu rozchodowego ustalić, **z której partii (i którego przyjęcia)**
zszedł towar — np. do raportu pochodzenia (traceability) lub rozliczenia kosztu.

**Warianty:**

| Wariant | Źródło | Co zwraca |
|---|---|---|
| Partia rozchodu | `obrot.Rozchod.PartiaTowaru` | `GrupaDostaw` strony rozchodowej |
| Partia przychodowa (źródłowa) | `obrot.Przychod.PartiaTowaru` | partia, z której zszedł towar |
| Partia pierwotna | `obrot.PrzychodPierwotny.PartiaTowaru` | pierwotne przyjęcie (przed korektami) |
| Dokument/pozycja źródłowa | `obrot.Przychod.Dokument`, `.PozycjaIdent` | przyjęcie i jego pozycja |
| Dostawa na pozycji rozchodu | `poz.Dostawa`, `poz.DostawaPierwotna` | pozycja przyjęcia powiązana z rozchodem |

**Pola i typy:** subrow `PartiaTowaru` na `Obrot`/`Zasob`:
`Dokument: DokumentHandlowy`, `PozycjaIdent: int`, `PartiaTowaru: GrupaDostaw`,
`KontrahentPartii: Kontrahent`, `Data: Date`, `Czas: Time`, `Typ: TypPartii`,
`Wartosc: decimal`. Na pozycji: `poz.Dostawa: PozycjaDokHandlowego`,
`poz.DostawaPierwotna: PozycjaDokHandlowego`.

**Snippet:**

```csharp
// dok — zapisany dokument rozchodowy (FV/WZ/RW)
foreach (Obrot o in dok.Obroty)
{
    // Strona rozchodowa = partia, z której zeszła ilość:
    GrupaDostaw partiaRozchodu = o.Rozchod.PartiaTowaru;

    // Strona przychodowa = przyjęcie, z którego pochodzi towar (pochodzenie):
    DokumentHandlowy przyjecie = o.Przychod.Dokument;
    GrupaDostaw partiaZrodlowa = o.Przychod.PartiaTowaru;

    // Pierwotne przyjęcie (przed łańcuchem korekt):
    GrupaDostaw partiaPierwotna = o.PrzychodPierwotny.PartiaTowaru;

    Console.WriteLine(
        $"{o.Towar.Kod}  ilość={o.Ilosc}  z przyjęcia={przyjecie?.Numer}  " +
        $"partia={partiaZrodlowa?.Numer}  kontrahent={o.Przychod.KontrahentPartii?.Kod}");
}

// Powiązanie na poziomie pozycji rozchodu:
foreach (PozycjaDokHandlowego poz in dok.Pozycje)
{
    PozycjaDokHandlowego pozycjaPrzyjecia = poz.Dostawa;   // pozycja PW/PZ
}
```

**Pułapki:**
- Rozróżniaj `Przychod` (źródło, czyli przyjęcie), `Rozchod` (bieżący rozchód) i
  `PrzychodPierwotny` (źródło sprzed korekt). Do raportu pochodzenia używaj `Przychod`/
  `PrzychodPierwotny`.
- `obrot.Przychod`/`Rozchod` to **subrow `PartiaTowaru`** — nie jest `null` jako struktura,
  ale jego pola (np. `PartiaTowaru`, `Dokument`) mogą być puste dla prostej ewidencji bez
  partii. Zabezpiecz odczyt `?.`.
- Jedna pozycja rozchodu może wygenerować **wiele obrotów** (gdy zeszła z kilku przychodów,
  np. FIFO) — iteruj po obrotach, nie zakładaj relacji 1:1 pozycja↔partia.
- Odczyt sensowny dopiero **po `Session.Save()`** dokumentu (przed zapisem brak obrotów).

---

### HANDEL-W39 — Odczyt okresów magazynowych i kontekstu wyceny (FIFO/LIFO/wg dostaw)

**Cel:** ustalić aktywny okres magazynowy dla daty oraz dowiedzieć się, jakim algorytmem
magazyn wycenia rozchód (co decyduje o wyborze partii, gdy nie wskazujemy jej ręcznie).

**Warianty:**

| Wariant | Źródło | Uwaga |
|---|---|---|
| Okres dla daty | `mag.OkresyMag.WgOkres[data]` | klucz po `Okres.To` |
| Czy okres zamknięty | `okres.Zamkniety: bool` | zamknięcie blokuje modyfikacje |
| Algorytm rozchodu magazynu | `magazyn.Algorytm: AlgorytmMagazynowy` | FIFO/LIFO/wg dostaw/wg cechy |
| Cecha algorytmu (wg cechy) | `magazyn.CechaAlgorytmu: string` | nazwa cechy pozycji/dokumentu |

**Pola i typy:** `OkresMagazynowy`: `Okres: FromTo`, `Zamkniety: bool`. Tabela `OkresyMag`,
indeks `WgOkres` (po `Okres.To`). `Magazyn.Algorytm: AlgorytmMagazynowy` (`FIFO=0`,
`LIFO=1`, `NieLiczyćStanów=2`, `WgDostawy=3`, `WgDostawyPrzyZatwierdzaniu=10`,
`OdNajdroższych=4`, `OdNajtańszych=5`, `WgCechyPozycji=6/7`, `WgCechyDokumentu=8/9`),
`Magazyn.CechaAlgorytmu: string`.

**Snippet:**

```csharp
var mag = session.GetMagazyny();
var magazyn = mag.Magazyny.WgSymbol["F"];

// Okres magazynowy obejmujący wskazaną datę:
OkresMagazynowy okres = mag.OkresyMag.WgOkres[Date.Today];
bool zamkniety = okres != null && okres.Zamkniety;

// Kontekst wyceny rozchodu (jak magazyn dobiera partie automatycznie):
AlgorytmMagazynowy algorytm = magazyn.Algorytm;
bool rozchodWgCechy =
    algorytm is AlgorytmMagazynowy.WgCechyPozycji or AlgorytmMagazynowy.WgCechyPozycjiMalejąco
            or AlgorytmMagazynowy.WgCechyDokumentu or AlgorytmMagazynowy.WgCechyDokumentuMalejąco;

string cechaWyceny = rozchodWgCechy ? magazyn.CechaAlgorytmu : null;

string opisWyceny = algorytm switch
{
    AlgorytmMagazynowy.FIFO => "rozchód od najstarszych dostaw",
    AlgorytmMagazynowy.LIFO => "rozchód od najnowszych dostaw",
    AlgorytmMagazynowy.WgDostawy => "rozchód wg wskazanej dostawy (partii)",
    _ => algorytm.ToString()
};
```

**Pułapki:**
- Gdy magazyn liczy `WgDostawy` (wskazanie partii) lub `WgCechy*`, automatyczny dobór partii
  zależy od `poz.Dostawa` (HANDEL-W35/HANDEL-W36) lub cechy (`CechaAlgorytmu`) — bez nich rozchód nie
  zostanie poprawnie rozliczony.
- `NieLiczyćStanów` oznacza, że magazyn **nie prowadzi zasobów** — `dok.Zasoby` pozostanie
  puste, a kontroli stanu ujemnego nie ma.
- Modyfikacja dokumentów w **zamkniętym** okresie (`okres.Zamkniety == true`) zostanie
  odrzucona — sprawdź to przed edycją wstecz.
- `OkresMagazynowy` to dane konfiguracyjne (`config="true"`, `guided`) — nie twórz okresów
  „w locie" w kodzie operacyjnym; korzystaj z istniejących.

---

