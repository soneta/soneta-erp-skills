# HANDEL02 — Wystawianie dokumentów

> Wspólne fakty o typie, podstawowe typy i szablon wzorca: [../handel.md](../handel.md).

Rozdział pokazuje, jak **utworzyć dokument handlowy od zera** w różnych wariantach (faktura
sprzedaży, faktura zakupu, dokument magazynowy, zamówienie, dokument walutowy, dokument z usługą)
oraz jak **dodawać i parametryzować pozycje**. Wszystkie wzorce operują na publicznym kontrakcie
platformy: tabela `DokHandlowe` (`session.GetHandel().DokHandlowe`), definicje
`DefDokHandlowych.WgSymbolu[...]`, pozycje `PozycjaDokHandlowego`.

> **Kolejność ustawiania pól jest istotna.** Najpierw `AddRow(dok)`, potem `Definicja` (inicjuje
> kategorię, kierunek magazynu, sposób liczenia VAT, walutę płatności), następnie `Magazyn`,
> `Kontrahent`, daty. Na pozycji najpierw `Towar` (inicjuje jednostkę, stawkę VAT, cenę i rabat),
> dopiero potem `Ilosc`, `Cena`, `Rabat`. Cała operacja w jednej transakcji
> `session.Logout(editMode: true)` zakończonej `Commit()` (kod biznesowy) / `CommitUI()`
> (worker/extender), a po niej `session.Save()` — dopiero `Save()` księguje obroty magazynowe i
> wykrywa konflikty.

---

### HANDEL-W4 — Faktura sprzedaży (FV)

**Cel:** wystawić fakturę sprzedaży: dokument rozchodowy z kontrahentem-nabywcą, pozycjami
towarowymi, automatycznie wyliczoną tabelą VAT i płatnością.

**Warianty:**

| Wariant | Charakterystyka | Pola krytyczne |
|---|---|---|
| FV krajowa od netto | standardowa sprzedaż | `Definicja=FV`, `LiczonaOd=Netto`, `Kontrahent` krajowy |
| FV liczona od brutto | sprzedaż detaliczna / paragonowa | `LiczonaOd=Brutto` |
| FV z rabatem nagłówkowym | rabat przepisywany na pozycje | `Rabat: Percent` na dokumencie |
| FV dla odbiorcy unijnego | WDT — stawka 0% | kontrahent `RodzajPodmiotu=Unijny`, stawka z karty/UE (HANDEL-W11) |
| FV walutowa | sprzedaż w EUR/USD | patrz **HANDEL-W9** |

**Pola i typy:** `Definicja: DefDokHandlowego` (`DefDokHandlowych.WgSymbolu["FV"]`),
`Magazyn: Magazyn`, `Kontrahent: Kontrahent`, `Data: Date` (data wystawienia),
`DataOperacji: Date` (faktyczna data sprzedaży), `LiczonaOd: SposobLiczeniaVAT` (`Netto`/`Brutto`),
`Rabat: Percent`. Wartości wyliczane: `Suma: BruttoNetto`, `SumyVAT: SubTable<SumaVAT>`,
`Platnosci: SubTable<Soneta.Kasa.Platnosc>` (powstaje automatycznie wg formy/terminu kontrahenta).

**Snippet:**

```csharp
var handel = session.GetHandel();
var magazyny = session.GetMagazyny();
var crm = session.GetCRM();

using (var t = session.Logout(editMode: true))
{
    var fv = new DokumentHandlowy();
    session.AddRow(fv);                                              // AddRow PRZED ustawianiem pól

    fv.Definicja = handel.DefDokHandlowych.WgSymbolu["FV"];          // definicja PIERWSZA
    fv.Magazyn = magazyny.Magazyny.WgSymbol["F"];
    fv.Kontrahent = crm.Kontrahenci.WgKodu["Abc"];                   // nabywca
    fv.Data = Date.Today;                                            // data wystawienia
    fv.DataOperacji = Date.Today;                                    // faktyczna data sprzedaży
    fv.LiczonaOd = SposobLiczeniaVAT.Netto;                          // VAT liczony od netto

    // Pozycja towarowa (szczegóły w HANDEL-W8):
    var poz = new PozycjaDokHandlowego(fv);
    session.AddRow(poz);
    poz.Towar = session.GetTowary().Towary.WgKodu["BIKINI"];         // Towar PIERWSZY
    poz.Ilosc = new Quantity(2, poz.Ilosc.Symbol);
    poz.Cena  = new DoubleCy(50m, poz.Cena.Symbol);

    t.Commit();                                                      // CommitUI() w workerze/extenderze
}
session.Save();                                                     // tu księgują się obroty i VAT

// Odczyt wyliczonej tabeli VAT i wartości:
foreach (SumaVAT v in fv.SumyVAT) { /* v.Stawka, v.Suma (Netto/VAT/Brutto) */ }
BruttoNetto suma = fv.Suma;
```

**Pułapki:**
- **Demo blokuje stan ujemny** (`StanUjemnyVerifier`): FV (rozchód) wymaga wcześniejszego
  **zapisanego** przyjęcia (PW/PZ) tego towaru. Samo `CommitUI` nie księguje obrotów — magazyn
  aktualizuje się dopiero po `Session.Save()` dokumentu przychodowego.
- `SumyVAT`, `Suma`, `Platnosci` są **wyliczane** z pozycji i parametrów dokumentu — nie ustawiaj
  ich ręcznie (ręczna korekta tabeli VAT to osobny mechanizm: `KorektaVAT=true`).
- `LiczonaOd` ustaw przed pozycjami — zmiana po wprowadzeniu pozycji wymusza przeliczenie cen
  netto↔brutto.
- Stawka VAT pozycji jest inicjowana z karty towaru — nie ustawiaj jej „z palca", jeśli nie musisz
  jej nadpisać.

---

### HANDEL-W5 — Faktura zakupu (FZ)

**Cel:** wprowadzić fakturę zakupu otrzymaną od dostawcy: dokument przychodowy z numerem obcym
dostawcy oraz datami zakupu i wystawienia dokumentu obcego.

**Warianty:**

| Wariant | Charakterystyka | Pola krytyczne |
|---|---|---|
| FZ krajowa | zakup od dostawcy PL | `Definicja=FZ`, `Obcy.Numer`, `DataOperacji` (data zakupu) |
| FZ z dostawą magazynową | zakup z przyjęciem na magazyn | `Magazyn`, kierunek przychodowy z definicji |
| FZ od dostawcy unijnego (WNT) | nabycie wewnątrzwspólnotowe | kontrahent `RodzajPodmiotu=Unijny` |
| FZ walutowa | zakup w walucie obcej | patrz **HANDEL-W9** |

**Pola i typy:** `Definicja=DefDokHandlowych.WgSymbolu["FZ"]`, `Kontrahent` = dostawca,
`Obcy: DokumentObcy` (subrow): `Obcy.Numer: string` (numer obcy nadany przez dostawcę),
`Obcy.DataOtrzymania: Date` (data dokumentu obcego). `Data: Date` (data wystawienia w naszym
systemie), `DataOperacji: Date` (faktyczna data zakupu).

**Snippet:**

```csharp
var handel = session.GetHandel();

using (var t = session.Logout(editMode: true))
{
    var fz = new DokumentHandlowy();
    session.AddRow(fz);

    fz.Definicja = handel.DefDokHandlowych.WgSymbolu["FZ"];
    fz.Magazyn = session.GetMagazyny().Magazyny.WgSymbol["F"];
    fz.Kontrahent = session.GetCRM().Kontrahenci.WgKodu["ZEFIR"];   // dostawca
    fz.Data = Date.Today;                                           // data wystawienia u nas
    fz.DataOperacji = Date.Today.AddDays(-2);                       // faktyczna data zakupu

    // Numer i data dokumentu obcego (od dostawcy):
    fz.Obcy.Numer = "FV/2026/06/123";                              // numer obcy
    fz.Obcy.DataOtrzymania = Date.Today.AddDays(-2);              // data dokumentu obcego

    var poz = new PozycjaDokHandlowego(fz);
    session.AddRow(poz);
    poz.Towar = session.GetTowary().Towary.WgKodu["BIKINI"];
    poz.Ilosc = new Quantity(10, poz.Ilosc.Symbol);
    poz.Cena  = new DoubleCy(30m, poz.Cena.Symbol);

    t.Commit();
}
session.Save();
```

**Pułapki:**
- `Obcy` to subrow (pole złożone) — nie da się przypisać `fz.Obcy = …`; ustawiaj jego pola
  (`fz.Obcy.Numer`, `fz.Obcy.DataOtrzymania`).
- Rozróżniaj trzy daty: `Data` (wystawienia u nas), `DataOperacji` (faktyczna data
  zakupu/sprzedaży, decyduje o okresie magazynowym), `Obcy.DataOtrzymania` (data na dokumencie
  obcym). To trzy różne pola — nie myl ich.
- FZ z przyjęciem na magazyn księguje **przychód** → po `Save()` powstają zasoby (`dok.Zasoby`).
- Indeks `WgKontrahentaObcy` (Kontrahent + numer obcy) pozwala wykryć duplikat faktury od tego
  samego dostawcy — sprawdzaj przed dodaniem.

---

### HANDEL-W6 — Dokument magazynowy (PZ / WZ / RW / PW)

**Cel:** wystawić czysto magazynowy dokument wpływający na stan magazynu, bez części handlowej
(VAT/płatności) lub z minimalną.

**Warianty:**

| Wariant | Symbol | Kierunek | Zastosowanie |
|---|---|---|---|
| Przyjęcie zewnętrzne | `PZ` | przychód | przyjęcie od dostawcy |
| Przyjęcie wewnętrzne | `PW` | przychód | przyjęcie z produkcji / bilans otwarcia |
| Wydanie zewnętrzne | `WZ` | rozchód | wydanie odbiorcy |
| Rozchód wewnętrzny | `RW` | rozchód | zużycie wewnętrzne |

**Pola i typy:** `Definicja=DefDokHandlowych.WgSymbolu["PW"]` (itd.), `Magazyn: Magazyn` (wymagany),
`Kontrahent` (gdy dotyczy — PZ/WZ tak, RW/PW zwykle nie), `Data`, `DataOperacji`. Kierunek
magazynu (`KierunekMagazynu: KierunekPartii` — `Przychód=1`, `Rozchód=-1`) jest ustawiany z
definicji (`readonly="set"`). Wynik: `dok.Zasoby` (przy przychodzie), `dok.Obroty`.

**Snippet:**

```csharp
var handel = session.GetHandel();

// Przyjęcie wewnętrzne PW (przychód — buduje stan magazynu pod późniejsze rozchody):
using (var t = session.Logout(editMode: true))
{
    var pw = new DokumentHandlowy();
    session.AddRow(pw);
    pw.Definicja = handel.DefDokHandlowych.WgSymbolu["PW"];          // kierunek z definicji
    pw.Magazyn = session.GetMagazyny().Magazyny.WgSymbol["F"];
    pw.Data = Date.Today;
    pw.DataOperacji = Date.Today;

    var poz = new PozycjaDokHandlowego(pw);
    session.AddRow(poz);
    poz.Towar = session.GetTowary().Towary.WgKodu["BIKINI"];
    poz.Ilosc = new Quantity(100, poz.Ilosc.Symbol);
    poz.Cena  = new DoubleCy(25m, poz.Cena.Symbol);

    t.Commit();
}
session.Save();                                                     // dopiero teraz powstają zasoby

// Stan magazynowy po przyjęciu:
foreach (var z in pw.Zasoby) { /* z.* — partia, ilość, magazyn */ }
```

**Pułapki:**
- `Magazyn` jest **wymagany** (`required`) dla dokumentów magazynowych — bez niego `Save()` rzuci
  `RowException`.
- `KierunekMagazynu`/`TypPartii` są `readonly="set"` — wynikają z definicji, nie ustawiaj ich
  ręcznie.
- Rozchód (WZ/RW) na bazie Demo wymaga wcześniejszego **zapisanego** przychodu (PW/PZ) — inaczej
  `StanUjemnyVerifier` zablokuje `Save()`.
- Obroty/zasoby księgują się **po `Session.Save()`**, nie po `Commit()`/`CommitUI()`. Aby je
  odczytać, zapisz dokument i odśwież.

---

### HANDEL-W7 — Zamówienie (ZO / ZD)

**Cel:** wystawić zamówienie od odbiorcy (ZO) lub zamówienie do dostawcy (ZD). Zamówienie nie
wpływa na stan magazynowy (może tworzyć rezerwacje), jest dokumentem nadrzędnym dla realizacji
(FV/WZ — patrz rozdział o relacjach).

**Warianty:**

| Wariant | Symbol | Strona | Realizacja |
|---|---|---|---|
| Zamówienie odbiorcy | `ZO` | klient zamawia u nas | → FV / WZ przez `IRelacjeService` |
| Zamówienie do dostawcy | `ZD` | my zamawiamy u dostawcy | → FZ / PZ przez `IRelacjeService` |
| ZO z rezerwacją | `ZO` | jw. | rezerwacja zasobu (`dok.Rezerwacja`) |
| ZO z terminem dostawy | `ZO` | jw. | `Dostawa.Termin` |

**Pola i typy:** `Definicja=DefDokHandlowych.WgSymbolu["ZO"]` / `["ZD"]`, `Kontrahent`,
`Magazyn`, `Data`, `DataOperacji`, `Dostawa: DokumentDostawa` (subrow): `Dostawa.Termin: Date`
(termin realizacji), `Dostawa.Sposob: string`. Powiązanie z realizacją: `dok.Rezerwacja`,
generowanie dokumentu podrzędnego przez `IRelacjeService.NowyPodrzednyIndywidualny(...)`.

**Snippet:**

```csharp
var handel = session.GetHandel();

using (var t = session.Logout(editMode: true))
{
    var zo = new DokumentHandlowy();
    session.AddRow(zo);
    zo.Definicja = handel.DefDokHandlowych.WgSymbolu["ZO"];         // zamówienie odbiorcy
    zo.Magazyn = session.GetMagazyny().Magazyny.WgSymbol["F"];
    zo.Kontrahent = session.GetCRM().Kontrahenci.WgKodu["Abc"];     // zamawiający odbiorca
    zo.Data = Date.Today;
    zo.DataOperacji = Date.Today;
    zo.Dostawa.Termin = Date.Today.AddDays(7);                      // oczekiwany termin dostawy

    var poz = new PozycjaDokHandlowego(zo);
    session.AddRow(poz);
    poz.Towar = session.GetTowary().Towary.WgKodu["BIKINI"];
    poz.Ilosc = new Quantity(5, poz.Ilosc.Symbol);
    poz.Cena  = new DoubleCy(50m, poz.Cena.Symbol);

    t.Commit();
}
session.Save();
```

**Pułapki:**
- Zamówienie **nie buduje stanu magazynu** — to dokument planistyczny. Realizację (FV/WZ z ZO,
  FZ/PZ z ZD) tworzysz przez `IRelacjeService` (rozdział o relacjach) — dokument nadrzędny musi
  być wtedy **zatwierdzony**.
- `Dostawa` to subrow — ustawiaj pola (`zo.Dostawa.Termin`), nie przypisuj całego obiektu.
- Rezerwacja ilościowa zamówienia jest zarządzana wewnętrznym workerem
  (`ZmienRezerwacjeIlosciowaWorker` — **internal**, niedostępny z dodatku); z poziomu publicznego
  odczytuj `zo.Rezerwacja`, a rezerwacje steruj przez definicję dokumentu i relacje.

---

### HANDEL-W8 — Dodawanie pozycji (towar, ilość, cena, rabat, jednostka)

**Cel:** dodać pozycję towarową do dokumentu — z automatycznym pobraniem ceny/rabatu z cennika lub
z ręcznym nadpisaniem.

**Warianty:**

| Wariant | Operacja | Pole |
|---|---|---|
| Pozycja z automatyczną ceną | cena i rabat pobrane z cennika/karty | tylko `Towar` + `Ilosc` |
| Ręczna cena | nadpisanie ceny | `Cena: DoubleCy` (ustawia `KorektaCeny=true`) |
| Ręczny rabat | nadpisanie rabatu | `Rabat: Percent` (ustawia `KorektaRabatu=true`) |
| Inna jednostka | sprzedaż w jednostce zbiorczej | `Ilosc` z symbolem jednostki towaru |
| Pozycja bez rabatu | wyłączenie rabatu | `BezRabatu=true` |
| Ręczna wartość | korekta wartości pozycji | `WartoscCy: Currency` |

**Pola i typy (`PozycjaDokHandlowego`):** `Towar: Towar` (ustaw pierwszy — inicjuje jednostkę,
stawkę VAT, cenę i rabat), `Ilosc: Quantity` (ilość + symbol jednostki), `Cena: DoubleCy` (cena
+ symbol waluty; netto lub brutto wg `Dokument.LiczonaOd`), `Rabat: Percent`,
`RabatCeny: DoubleCy` (rabat kwotowy), `WartoscCy: Currency` (wartość pozycji),
`DefinicjaStawki: DefinicjaStawkiVat` (stawka VAT). Flagi nadpisań: `KorektaCeny: bool`,
`KorektaRabatu: bool`, `BezRabatu: bool`.

**Snippet:**

```csharp
var towary = session.GetTowary();

using (var t = session.Logout(editMode: true))
{
    // Wariant A — cena i rabat pobrane automatycznie z cennika/karty towaru:
    var poz1 = new PozycjaDokHandlowego(dok);
    session.AddRow(poz1);
    poz1.Towar = towary.Towary.WgKodu["BIKINI"];                    // ustawia jednostkę, cenę, rabat, stawkę VAT
    poz1.Ilosc = new Quantity(3, poz1.Ilosc.Symbol);               // symbol jednostki z towaru
    // Cena i rabat zostają takie, jakie zaproponował cennik.

    // Wariant B — ręczne nadpisanie ceny i rabatu:
    var poz2 = new PozycjaDokHandlowego(dok);
    session.AddRow(poz2);
    poz2.Towar = towary.Towary.WgKodu["BIKINI"];
    poz2.Ilosc = new Quantity(10, poz2.Ilosc.Symbol);
    poz2.Cena  = new DoubleCy(48m, poz2.Cena.Symbol);              // nadpisanie ceny → KorektaCeny=true
    poz2.Rabat = new Percent(0.1);                                 // 10% rabatu → KorektaRabatu=true

    t.Commit();
}
session.Save();
```

**Pułapki:**
- **`Towar` ustawiaj jako pierwszy.** Dopiero on inicjuje symbol jednostki na `Ilosc`/`Cena`, stawkę
  VAT i proponowaną cenę/rabat. Ustawienie `Ilosc`/`Cena` przed `Towar` operowałoby na pustych
  symbolach.
- `Ilosc` to `Quantity` (wartość + symbol jednostki), `Cena` to `DoubleCy` (wartość + symbol
  waluty) — twórz je z symbolem już ustawionym na pozycji: `new Quantity(n, poz.Ilosc.Symbol)`,
  `new DoubleCy(c, poz.Cena.Symbol)`. Nie wstawiaj „gołego" `decimal`.
- Ręczne ustawienie `Cena`/`Rabat` zapala flagi `KorektaCeny`/`KorektaRabatu` — od tej chwili
  platforma **nie przeliczy** już automatycznie tej wartości (np. po zmianie kontrahenta/ilości).
- `Cena` jest netto albo brutto zależnie od `Dokument.LiczonaOd` — interpretuj ją spójnie z
  dokumentem.
- Konstruktor pozycji wymaga dokumentu: `new PozycjaDokHandlowego(dok)`, a po nim `session.AddRow(poz)`.

---

### HANDEL-W9 — Dokument w walucie obcej

**Cel:** wystawić dokument rozliczany w walucie obcej (EUR/USD): wskazać walutę płatności, tabelę
kursową, datę kursu oraz — w razie potrzeby — wpisać kurs ręcznie.

**Warianty:**

| Wariant | Mechanizm | Pola |
|---|---|---|
| Kurs z tabeli na datę | kurs pobierany z `TabelaKursowa` | `TabelaKursowa`, `DataKursu` |
| Kurs ręczny | użytkownik podaje kurs | `KursWaluty: double` |
| Zmiana waluty istniejącego dokumentu | przeliczenie dokumentu i cen | akcja „Zmień walutę dokumentu i cen..." (worker) |
| Waluta na pozycji | cena w walucie | `poz.Cena: DoubleCy` z symbolem waluty |

**Pola i typy:** `TabelaKursowa: TabelaKursowa` (wymagana — `WalutyModule.GetInstance(session).TabeleKursowe`),
`DataKursu: Date`, `KursWaluty: double`, `BruttoCy: Currency` (kwota płatności w walucie).
Waluta płatności wynika z definicji (`DefDokHandlowego.WalutaPlatnosci`). Zmianę waluty
istniejącego dokumentu realizuje akcja menu Czynności sterowana klasą parametrów
`DokumentHandlowyZmianaWalutyWorkerParams` (publiczna): `Waluta`, `WalutaBazowa` (read-only),
`TabelaKursowa`, `Data`, `KursWaluty`, `ZmienCeny: bool`.

**Snippet:**

```csharp
using Microsoft.Extensions.DependencyInjection;   // dla GetRequiredService, jeśli potrzebne
using Soneta.Waluty;

var wm = WalutyModule.GetInstance(session);        // session.GetWaluty() jest internal
var eur = wm.Waluty.WgSymbolu["EUR"];
var tabela = wm.TabeleKursowe.NBP;                 // np. tabela NBP

// Zmiana waluty istniejącego (buforowego) dokumentu na EUR z ręcznym kursem.
// Worker uruchamiany jest jak akcja menu Czynności — parametry przekazujemy przez Context:
var paramy = new DokumentHandlowyZmianaWalutyWorkerParams(context, dok)
{
    Waluta = eur,
    TabelaKursowa = tabela,
    KursWaluty = 4.3344,        // kurs ręczny; przy zmianie tabeli/daty platforma proponuje kurs sama
    ZmienCeny = true,           // przelicz także ceny pozycji
};
context.Set(paramy);
// akcja „Zmień walutę dokumentu i cen..." (ZmienWalute) wykonuje przeliczenie w transakcji UI

// Dokument walutowy „od zera": ustaw tabelę i datę kursu przed pozycjami:
using (var t = session.Logout(editMode: true))
{
    dok.TabelaKursowa = tabela;
    dok.DataKursu = Date.Today;
    // dok.KursWaluty = 4.3344;   // tylko gdy chcesz wymusić kurs ręczny
    t.Commit();
}
session.Save();
```

**Pułapki:**
- **Brak kursu na datę = wyjątek.** Jeśli w bazie nie ma kursu danej waluty na `DataKursu`, operacja
  rzuca `KursWalutyNotFoundException`. Na bazie Demo nie ma kursu EUR „na dziś" — albo dodaj kurs do
  tabeli kursowej, albo wpisz kurs ręcznie (`KursWaluty`).
- `TabelaKursowa` jest **wymagana** dla dokumentu walutowego.
- `session.GetWaluty()` jest **internal** — używaj `WalutyModule.GetInstance(session)`.
- Worker `DokumentHandlowyZmianaWalutyWorker` jest klasą **internal** — z dodatku nie tworzysz jej
  instancji bezpośrednio; uruchamiasz akcję przez framework Czynności, przekazując publiczną klasę
  `DokumentHandlowyZmianaWalutyWorkerParams` przez `Context`.
- Zmiana waluty dokumentu jest możliwa tylko w **buforze** (`dok.Bufor == true`).

---

### HANDEL-W10 — Dokument z usługą (pozycja usługowa bez wpływu na magazyn)

**Cel:** dodać do dokumentu pozycję usługową (np. „MONTAZ", „TRANSPORT") — towar typu usługa nie
ma wpływu na stan magazynu, ale uczestniczy w wartości i tabeli VAT.

**Warianty:**

| Wariant | Charakterystyka |
|---|---|
| FV tylko z usługą | faktura za samą usługę (np. montaż) — brak obrotu magazynowego |
| FV mieszana | towar magazynowy + pozycja usługowa na jednym dokumencie |
| Usługa rozliczana ilościowo | usługa w jednostce (np. „TRANSPORT" w km) |

**Pola i typy:** identyczne jak w HANDEL-W8 (`Towar`, `Ilosc`, `Cena`, `Rabat`, `DefinicjaStawki`).
Różnica jest w **karcie towaru**: towar usługowy nie generuje obrotu magazynowego —
`poz.IloscMagazynu` pozostaje zerowa, `dok.Zasoby`/`dok.Obroty` nie powstają dla tej pozycji.

**Snippet:**

```csharp
var handel = session.GetHandel();
var towary = session.GetTowary();

using (var t = session.Logout(editMode: true))
{
    var fv = new DokumentHandlowy();
    session.AddRow(fv);
    fv.Definicja = handel.DefDokHandlowych.WgSymbolu["FV"];
    fv.Magazyn = session.GetMagazyny().Magazyny.WgSymbol["F"];
    fv.Kontrahent = session.GetCRM().Kontrahenci.WgKodu["Abc"];
    fv.Data = Date.Today;
    fv.DataOperacji = Date.Today;

    // Pozycja usługowa — towar "MONTAZ" jest usługą (BEZ wpływu na magazyn):
    var poz = new PozycjaDokHandlowego(fv);
    session.AddRow(poz);
    poz.Towar = towary.Towary.WgKodu["MONTAZ"];                     // usługa
    poz.Ilosc = new Quantity(1, poz.Ilosc.Symbol);
    poz.Cena  = new DoubleCy(200m, poz.Cena.Symbol);

    t.Commit();
}
session.Save();
```

**Pułapki:**
- O tym, czy pozycja wpływa na magazyn, decyduje **typ towaru** (usługa vs towar magazynowy), a nie
  pole na pozycji. Dla usługi `StanUjemnyVerifier` nie blokuje wystawienia rozchodu — usługa nie
  pobiera ze stanu.
- Faktura zawierająca **wyłącznie** usługi nie tworzy obrotów magazynowych, ale nadal liczy tabelę
  VAT i płatność.
- Usługa też ma jednostkę (np. „TRANSPORT" w km) — `Ilosc` używa symbolu jednostki z karty towaru.

---

### HANDEL-W11 — Odbiorca / płatnik inny niż kontrahent + miejsce dostawy

**Cel:** wystawić dokument, na którym **nabywca** (`Kontrahent`) różni się od **odbiorcy** towaru
(`Odbiorca`), wskazać miejsce dostawy oraz — gdy płatnikiem jest inny podmiot — rozliczyć
płatność na płatnika.

**Warianty:**

| Wariant | Pole / mechanizm |
|---|---|
| Inny odbiorca towaru | `Odbiorca: Kontrahent` |
| Miejsce dostawy odbiorcy | `OdbiorcaMiejsceDostawy: Lokalizacja` |
| Osoba odbierająca | `OsobaKontrahenta: KontaktOsoba`, `Osoba: string` (podpisujący) |
| Adres / parametry przesyłki | subrow `Dostawa` (`Dostawa.Termin`, `Dostawa.Sposob`, `Dostawa.Odpowiedzialny`) |
| Inny płatnik | `dok.InnyPłatnik` (kalkulowane — wynika z relacji podmiotów / płatności) |

**Pola i typy:** `Kontrahent: Kontrahent` (nabywca — strona transakcji/VAT),
`Odbiorca: Kontrahent` (odbiorca towaru — dane dostawy), `OdbiorcaMiejsceDostawy: Lokalizacja`
(miejsce docelowe dostawy), `OsobaKontrahenta: KontaktOsoba`, `Osoba: string`.
`InnyPłatnik` jest **kalkulowane (read-only)** — płatnika ustawia się przez relacje podmiotów
(płatnik podmiotu) lub przez płatność, nie przez bezpośrednie przypisanie na dokumencie.

**Snippet:**

```csharp
var crm = session.GetCRM();

using (var t = session.Logout(editMode: true))
{
    // dok utworzony jak w HANDEL-W4; Kontrahent = nabywca (np. centrala):
    dok.Kontrahent = crm.Kontrahenci.WgKodu["Abc"];                 // nabywca / strona VAT
    dok.Odbiorca   = crm.Kontrahenci.WgKodu["ZEFIR"];               // odbiorca towaru (inny podmiot)

    // Miejsce dostawy odbiorcy (lokalizacja zdefiniowana u odbiorcy):
    // dok.OdbiorcaMiejsceDostawy = ... // rekord Lokalizacja powiązany z odbiorcą

    dok.Osoba = "Jan Kowalski";                                     // osoba podpisująca po stronie kontrahenta

    // Parametry dostawy (subrow):
    dok.Dostawa.Termin = Date.Today.AddDays(3);
    dok.Dostawa.Sposob = "Kurier";

    t.Commit();
}
session.Save();

// Odczyt płatnika (kalkulowane):
bool jestInnyPlatnik = dok.InnyPłatnik;
```

**Pułapki:**
- `Kontrahent` to **nabywca** (strona transakcji i VAT), `Odbiorca` to fizyczny odbiorca towaru —
  to dwa różne pola, oba typu `Kontrahent`. Faktura wystawiana jest na `Kontrahent`, dostawa idzie
  do `Odbiorca`.
- `InnyPłatnik` jest **kalkulowane** — nie przypisuj go ręcznie. Innego płatnika ustala się przez
  relacje podmiotów (płatnik nadrzędny) lub przez konfigurację płatności dokumentu.
- `OdbiorcaMiejsceDostawy` to referencja do rekordu `Lokalizacja` (zwykle zdefiniowanego u
  odbiorcy) — pobierz istniejącą lokalizację, nie twórz „w locie".
- `Dostawa` to subrow — ustawiaj jego pola, nie przypisuj całego obiektu.
- Zmiana płatnika rozkłada się na płatności; do podziału płatności na raty/płatników służy publiczny
  worker `PodzialPlatnosciWorker`.

---

