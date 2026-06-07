# HANDEL08 — VAT, wartości i waluty

> Wspólne fakty o typie, podstawowe typy i szablon wzorca: [../handel.md](../handel.md).

Rozdział opisuje publiczny kontrakt dokumentu handlowego w zakresie tabeli VAT, podsumowań
wartości, ręcznej korekty VAT, sposobu liczenia VAT oraz zmiany waluty dokumentu i cen. Cały kod
jest zgodny z **C# 10** i operuje wyłącznie na **publicznych** typach i workerach platformy.

> **Wartości pieniężne** na pozycjach tabeli VAT i podsumowaniach mają dwie reprezentacje:
> `BruttoNetto` — kwoty w walucie systemowej jako `decimal` (`Netto`, `VAT`, `Brutto`); `BruttoNettoCy`
> — kwoty w walucie dokumentu jako `Currency` (`NettoCy`, `VATCy`, `BruttoCy`). Nie operuj na
> niezaokrąglonych `decimal` — platforma weryfikuje zaokrąglenie (safe-code §10).

---

### HANDEL-W43 — Odczytanie tabeli VAT (`SumyVAT`)

**Cel:** odczytać rozbicie wartości dokumentu na stawki VAT (netto / VAT / brutto wg stawki) — np.
do wydruku, eksportu lub kontroli sumy podatku.

**Warianty:**

| Wariant | Źródło | Uwaga |
|---|---|---|
| Tabela VAT dokumentu | `dok.SumyVAT` (`SubTable<SumaVAT>`) | po jednej pozycji na stawkę |
| Kwoty w walucie systemowej | `suma.Suma` (`BruttoNetto`) | `Netto`/`VAT`/`Brutto` jako `decimal` |
| Kwoty w walucie dokumentu | `suma.SumaCy` (`BruttoNettoCy`) | `NettoCy`/`VATCy`/`BruttoCy` jako `Currency` |
| Procent / opis stawki | `suma.Stawka`, `suma.DefinicjaStawki` | `StawkaVat.Procent: Percent` |
| Sumy z dokumentów nadrzędnych | `dok.NadrzędneSumyVAT` (`IList`) | scalone stawki nadrzędnych |

**Pola i typy:** `dok.SumyVAT: SubTable<SumaVAT>`. `SumaVAT` udostępnia: `DefinicjaStawki:
DefinicjaStawkiVat`, `Stawka: StawkaVat` (`Stawka.Procent: Percent`), `Suma: BruttoNetto`
(`Netto`, `VAT`, `Brutto` — `decimal`), `SumaCy: BruttoNettoCy` (`NettoCy`, `VATCy`, `BruttoCy` —
`Currency`), `Dokument: DokumentHandlowy`.

**Snippet:**

```csharp
var dok = session.GetHandel().DokHandlowe.WgDaty[...];   // lub po Guid

// Iteracja po tabeli VAT — jedna pozycja (SumaVAT) na każdą stawkę dokumentu:
foreach (SumaVAT s in dok.SumyVAT)
{
    Percent stawka = s.Stawka.Procent;       // np. 23%
    decimal netto  = s.Suma.Netto;           // kwota netto w walucie systemowej
    decimal vat    = s.Suma.VAT;             // kwota podatku VAT
    decimal brutto = s.Suma.Brutto;          // kwota brutto

    // Kwoty w walucie dokumentu (Currency = wartość + symbol waluty):
    Currency vatCy = s.SumaCy.VATCy;

    Console.WriteLine($"{stawka}: netto={netto} VAT={vat} brutto={brutto}");
}

// Łączna kwota VAT dokumentu z tabeli VAT:
decimal vatRazem = dok.SumyVAT.Sum(s => s.Suma.VAT);
```

**Pułapki:**
- `dok.SumyVAT` to `SubTable<SumaVAT>` — kolekcja serwerowa; iteruj po niej, nie materializuj do listy,
  jeśli wystarczy przebieg jednorazowy. Tabela VAT jest mała (kilka stawek), więc `.Sum(...)` jest
  akceptowalne.
- Rozróżniaj `Suma` (`BruttoNetto`, `decimal` w walucie systemowej) od `SumaCy` (`BruttoNettoCy`,
  `Currency` w walucie dokumentu). Dla dokumentu walutowego do prezentacji używaj `SumaCy`.
- `Stawka` to `StawkaVat` (typ stawki), `Procent` zwraca `Percent` — nie myl z `decimal`.
- Tabela VAT jest **wyliczana z pozycji** dokumentu (chyba że włączono `KorektaVAT` — patrz HANDEL-W45). Nie
  modyfikuj jej, gdy chcesz tylko odczytać wartości.

---

### HANDEL-W44 — Odczyt podsumowań wartości dokumentu

**Cel:** odczytać zsumowane wartości netto / VAT / brutto całego dokumentu oraz proponowany rabat —
bez ręcznego sumowania pozycji.

**Warianty:**

| Wariant | Pole | Typ | Uwaga |
|---|---|---|---|
| Podsumowanie dokumentu | `dok.Suma` | `BruttoNetto` | `Netto`/`VAT`/`Brutto` (`decimal`, waluta systemowa) |
| Wartość brutto w walucie | `dok.BruttoCy` | `Currency` | brutto w walucie dokumentu |
| Suma wyliczona z pozycji | `dok.SumaPozycji` | `BruttoNettoPozycji` | `Netto`/`VAT`/`Brutto` (read-only) |
| Suma pozycji tow./prod. | `dok.SumaPozycjiTowProd` | `BruttoNettoPozycji` | tylko towary i produkty |
| Proponowany rabat | `dok.Rabat` | `Percent` | przepisywany do pozycji |

**Pola i typy:** `dok.Suma: BruttoNetto` (podsumowana wartość dokumentu), `dok.BruttoCy: Currency`,
`dok.SumaPozycji: BruttoNettoPozycji` (`Netto`/`VAT`/`Brutto` — `decimal`, **tylko do odczytu**,
liczone na bieżąco z pozycji), `dok.Rabat: Percent`.

**Snippet:**

```csharp
var dok = session.GetHandel().DokHandlowe.WgDaty[...];

// Podsumowanie całego dokumentu (waluta systemowa):
decimal netto  = dok.Suma.Netto;
decimal vat    = dok.Suma.VAT;
decimal brutto = dok.Suma.Brutto;

// Brutto w walucie dokumentu (dla dokumentów walutowych):
Currency bruttoCy = dok.BruttoCy;

// Suma wyliczana z pozycji (przydatne do kontroli spójności z dok.Suma):
var sp = dok.SumaPozycji;
Console.WriteLine($"Pozycje: netto={sp.Netto} VAT={sp.VAT} brutto={sp.Brutto}");

// Proponowany rabat dokumentu (przepisywany do nowych pozycji):
Percent rabat = dok.Rabat;
```

**Pułapki:**
- `dok.Suma` to **stan zapisany** podsumowania, a `dok.SumaPozycji` jest **wyliczane na bieżąco**
  z pozycji za każdym odczytem. Dla dokumentu w buforze, przed ponownym przeliczeniem, mogą się
  chwilowo różnić.
- `SumaPozycji`/`SumaPozycjiTowProd` zwracają `BruttoNettoPozycji` — typ **tylko do odczytu** (brak
  setterów); nie próbuj przez nie modyfikować wartości.
- `dok.Rabat` to `Percent` — proponowany rabat dokumentu, przepisywany do nowo dodawanych pozycji;
  ustawienie nie przelicza wstecznie pozycji już istniejących.
- Wartości brutto/netto na poziomie dokumentu zależą od `LiczonaOd` (HANDEL-W46) i ewentualnej korekty
  tabeli VAT (`KorektaVAT`, HANDEL-W45).

---

### HANDEL-W45 — Ręczna korekta tabeli VAT (`KorektaVAT`)

**Cel:** ręcznie skorygować kwoty w tabeli VAT (gdy wyliczenie z pozycji nie odpowiada wartości
docelowej — np. zaokrąglenia faktury źródłowej), włączając flagę `KorektaVAT` i edytując wiersze
`SumyVAT`.

**Warianty:**

| Wariant | Operacja |
|---|---|
| Włączenie trybu korekty | `dok.KorektaVAT = true` |
| Ręczna zmiana kwoty stawki | edycja `suma.Suma.Netto` / `.VAT` / `.Brutto` na wierszu `SumaVAT` |
| Dostępność korekty | `dok.IsReadOnlyKorektaVAT()`, `dok.IsReadOnlySumyVAT()` (sterowanie UI) |
| Powrót do automatu | `dok.KorektaVAT = false` (tabela liczona ponownie z pozycji) |

**Pola i typy:** `dok.KorektaVAT: bool` (czy sumy VAT zmieniono ręcznie i nie zależą od pozycji),
`SumaVAT.Suma: BruttoNetto` (`Netto`/`VAT`/`Brutto` — `decimal`). Wiersze tabeli VAT są edytowalne
**tylko gdy** `KorektaVAT == true` (`SumaVAT.IsReadOnly()` zwraca `true` przy wyłączonej fladze).

> **Worker `KorektaTabeliVATWorker` jest `internal`** — nie da się go zainstancjonować z dodatku
> zewnętrznego. Publiczny tor korekty prowadzi przez flagę `dok.KorektaVAT` i bezpośrednią edycję
> pól wierszy `dok.SumyVAT`.

**Snippet:**

```csharp
var dok = session.GetHandel().DokHandlowe.WgDaty[...];

using (var t = session.Logout(editMode: true))    // CommitUI() w workerze/extenderze
{
    // 1. Włącz ręczną korektę — odblokowuje edycję wierszy tabeli VAT:
    dok.KorektaVAT = true;

    // 2. Skoryguj kwoty na wybranej stawce (np. wyrównanie groszowe na 23%):
    foreach (SumaVAT s in dok.SumyVAT)
    {
        if (s.Stawka.Procent == new Percent(0.23))
        {
            s.Suma.VAT    = 230.01m;       // wartości MUSZĄ być zaokrąglone do grosza
            s.Suma.Brutto = 1230.01m;
        }
    }
    t.Commit();
}
session.Save();
```

**Pułapki:**
- Edycja wierszy `SumyVAT` bez `dok.KorektaVAT = true` zostanie zablokowana — `SumaVAT` jest wtedy
  read-only (sumy zależą od pozycji).
- Przypisywane kwoty muszą być **zaokrąglone do grosza** — w trybie DEBUG ustawienie
  niezaokrąglonej wartości `Netto`/`VAT`/`Brutto` rzuca `ArgumentException`. Zaokrąglaj wejście
  (`Soneta.Tools.Math.RoundCy(...)`).
- `KorektaVAT` jest dostępna tylko, gdy definicja dokumentu na to pozwala
  (`Definicja.SumyVAT` w trybie korekty) — sprawdzaj `dok.IsReadOnlyKorektaVAT()` zanim ustawisz
  flagę z poziomu UI.
- Po włączeniu korekty tabela VAT **przestaje** śledzić zmiany pozycji. Wyłączenie
  (`KorektaVAT = false`) przywraca wyliczanie z pozycji i nadpisuje ręczne kwoty.
- `DefinicjaStawki` na wierszu `SumaVAT` można zmieniać tylko przy włączonej korekcie
  (`IsReadOnlyDefinicjaStawki()` zależy od `KorektaVAT`).

---

### HANDEL-W46 — Sposób liczenia VAT (`LiczonaOd`) i przeliczenie procedur VAT

**Cel:** ustawić, czy dokument jest liczony od netto czy od brutto (`LiczonaOd`), oraz przeliczyć
procedury VAT (JPK) na dokumencie zatwierdzonym/zaksięgowanym przy użyciu publicznego workera.

**Warianty:**

| Wariant | Mechanizm |
|---|---|
| Liczenie od netto | `dok.LiczonaOd = SposobLiczeniaVAT.OdNetto` |
| Liczenie od brutto | `dok.LiczonaOd = SposobLiczeniaVAT.OdBrutto` |
| Od brutto minus netto | `dok.LiczonaOd = SposobLiczeniaVAT.OdBruttoMinusNetto` |
| Wg ustawień kontrahenta | `dok.LiczonaOd = SposobLiczeniaVAT.ZależyOdKontrahenta` |
| Przeliczenie procedur VAT | worker `PrzeliczProceduryVATWorker` (publiczny) |

**Pola i typy:** `dok.LiczonaOd: SposobLiczeniaVAT` — enum `Soneta.Handel.SposobLiczeniaVAT`:
`OdNetto=1`, `OdBrutto=2`, `OdBruttoMinusNetto=3`, `ZależyOdKontrahenta=4` (wartość `0` jest
niedozwolona — rzuca `RequiredException`). Worker `PrzeliczProceduryVATWorker` ma publiczną klasę
parametrów `PrzeliczProceduryVATParams : ContextBase` (`Zatwierdzone: bool = true`,
`Zaksiegowane: bool = false`) oraz właściwości `[Context]`: `Dokument: DokumentHandlowy`,
`Params: PrzeliczProceduryVATParams`.

**Snippet:**

```csharp
var dok = session.GetHandel().DokHandlowe.WgDaty[...];

// 1. Zmiana sposobu liczenia VAT (dokument w buforze):
using (var t = session.Logout(editMode: true))
{
    dok.LiczonaOd = SposobLiczeniaVAT.OdBrutto;   // 0 jest niedozwolone
    t.Commit();
}
session.Save();

// 2. Przeliczenie procedur VAT (JPK) workerem publicznym.
//    Worker działa tylko dla dokumentu zatwierdzonego (Params.Zatwierdzone)
//    lub zablokowanego/zaksięgowanego (Params.Zaksiegowane):
var p = new PrzeliczProceduryVATWorker.PrzeliczProceduryVATParams(context)
{
    Zatwierdzone = true,
    Zaksiegowane = false,
};
var worker = new PrzeliczProceduryVATWorker
{
    Dokument = dok,
    Params   = p,
};
worker.PrzeliczProceduryVAT();    // sam otwiera transakcję i Commit
session.Save();
```

**Pułapki:**
- `LiczonaOd` nie przyjmuje wartości `0` (`RequiredException`). Zawsze ustaw konkretny wariant enuma.
- Zmiana `LiczonaOd` na dokumencie z pozycjami wpływa na sposób przeliczenia netto↔brutto pozycji
  i tabeli VAT — rób to przed wprowadzeniem cen lub świadomie po przeliczeniu.
- `PrzeliczProceduryVATWorker.PrzeliczProceduryVAT()` **nic nie zrobi**, jeśli dokument jest w
  buforze albo stan nie pasuje do flag `Params` (`Zatwierdzone`/`Zaksiegowane`). Worker sam otwiera
  transakcję (`Logout(true)` + `Commit`) — nie owijaj go w dodatkową transakcję edycyjną.
- Worker jest widoczny tylko, gdy definicja liczy sumy VAT i ma definicję ewidencji
  (`IsVisiblePrzeliczProceduryVAT`); z poziomu kodu i tak sprawdź stan dokumentu przed wywołaniem.
- `PrzeliczProceduryVATParams` dziedziczy po `ContextBase` — przy ręcznym tworzeniu przekaż `Context`
  do konstruktora.

---

### HANDEL-W47 — Zmiana waluty dokumentu i cen

**Cel:** zmienić walutę dokumentu handlowego (i opcjonalnie przeliczyć ceny pozycji) — np. wystawić
fakturę w EUR zamiast PLN, z kursem z wybranej tabeli kursowej.

**Warianty:**

| Wariant | Mechanizm |
|---|---|
| Zmiana waluty z przeliczeniem cen | parametry `DokumentHandlowyZmianaWalutyWorkerParams` + akcja „Zmień walutę dokumentu i cen..." |
| Zmiana waluty bez cen | te same parametry z `ZmienCeny = false` |
| Ręczne ustawienie waluty/kursu | `dok.TabelaKursowa`, `dok.KursWaluty`, `dok.DataOgłoszeniaKursu`, `dok.BruttoCy` |

**Pola i typy:** klasa parametrów (publiczna) `DokumentHandlowyZmianaWalutyWorkerParams :
PozycjaDokHandlowegoZmianaWalutyCenyWorkerParams` (ctor `(Context, [Context] DokumentHandlowy)`)
udostępnia: `Waluta: Waluta` („na walutę"), `WalutaBazowa: Waluta` (read-only, „z waluty"),
`TabelaKursowa: TabelaKursowa`, `Data: Date`, `KursWaluty: double`, `ZmienCeny: bool`. Pola
dokumentu: `dok.TabelaKursowa: TabelaKursowa`, `dok.KursWaluty: double`, `dok.BruttoCy: Currency`.
Moduł walut (jest `internal` jako extension): `Soneta.Waluty.WalutyModule.GetInstance(session)` →
`.Waluty.WgSymbolu["EUR"]`, `.TabeleKursowe`.

> **Worker `DokumentHandlowyZmianaWalutyWorker` jest `internal`** — nie da się go zainstancjonować
> bezpośrednio z dodatku zewnętrznego. Jest jednak zarejestrowany jako akcja menu Czynności („Zmień
> walutę dokumentu i cen...", `Shift+F11`) i przyjmuje publiczne parametry
> `DokumentHandlowyZmianaWalutyWorkerParams`. Z poziomu kodu dodatku zewnętrznego dostępne tory to:
> (1) uruchomienie akcji przez mechanizm Czynności z przygotowanym `Context`, albo (2) bezpośrednie
> ustawienie pól waluty/kursu na dokumencie i pozycjach.

**Snippet:**

```csharp
using Microsoft.Extensions.DependencyInjection;   // jeśli korzystasz z serwisów
using Soneta.Waluty;

var dok = session.GetHandel().DokHandlowe.WgDaty[...];

// --- Tor 1: przygotowanie parametrów workera (do uruchomienia przez akcję Czynności) ---
// Worker jest internal — z dodatku przygotowujemy publiczne Params i uruchamiamy akcję
// przez mechanizm menu Czynności (Context z zaznaczonym dokumentem).
var wm = WalutyModule.GetInstance(session);
var p = new DokumentHandlowyZmianaWalutyWorkerParams(context, dok)
{
    Waluta        = wm.Waluty.WgSymbolu["EUR"],   // waluta docelowa
    TabelaKursowa = wm.TabeleKursowe.NBP,
    Data          = Date.Today,
    ZmienCeny     = true,                          // przelicz też ceny pozycji
};
// KursWaluty wylicza się automatycznie po ustawieniu Waluta/TabelaKursowa/Data;
// w razie potrzeby można nadpisać: p.KursWaluty = 4.30;

// --- Tor 2: ręczne ustawienie waluty i kursu na dokumencie (bez workera) ---
using (var t = session.Logout(editMode: true))
{
    dok.TabelaKursowa = wm.TabeleKursowe.NBP;
    dok.KursWaluty    = 4.30;
    // dok.BruttoCy = new Currency(..., "EUR");   // kwoty w walucie dokumentu
    t.Commit();
}
session.Save();
```

**Pułapki:**
- Worker `DokumentHandlowyZmianaWalutyWorker` jest `internal` — **nie** wywołasz `new ...Worker(...)`
  ani `.ZmienWalute()` z dodatku zewnętrznego. Używaj publicznych `Params` + akcji Czynności lub
  bezpośredniej edycji pól dokumentu.
- `session.GetWaluty()` jest **internal** — moduł walut pobieraj przez
  `WalutyModule.GetInstance(session)` (namespace `Soneta.Waluty`).
- Jeśli w bazie **brak kursu** na żądaną datę (np. Demo nie ma kursu EUR „na dziś"), platforma rzuci
  `KursWalutyNotFoundException`. `KursWaluty` w parametrach wylicza się automatycznie tylko, gdy kurs
  istnieje; w przeciwnym razie ustaw `KursWaluty` ręcznie.
- Zmiana waluty ma sens tylko dla dokumentu w **buforze** (`IsVisibleZmienWalute` wymaga
  `dok.Bufor`); dla dokumentu zatwierdzonego operacja jest niedostępna.
- `WalutaBazowa` jest read-only — wyznaczana z bieżącej waluty dokumentu (`dok.BruttoCy.Symbol`).
  Ustawiasz tylko `Waluta` (docelową).
- Kwoty pieniężne to `Currency` (wartość + symbol), nie `decimal`/`double`. Sam `KursWaluty` jest
  `double`.

---

---

