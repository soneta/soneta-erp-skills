# HANDEL09 — Korekty i dokumenty specjalne

> Wspólne fakty o typie, podstawowe typy i szablon wzorca: [../handel.md](../handel.md).

Rozdział obejmuje korekty (ilościowe, ceny, wartości przyjęcia) oraz dokumenty „specjalne": inwentaryzację (INW), fakturę zaliczkową wraz z jej rozliczeniem oraz przesunięcie międzymagazynowe (MM). Wszystkie wzorce operują **wyłącznie na publicznym kontrakcie** platformy. Kluczowym narzędziem jest serwis relacji `IRelacjeService` (namespace `Soneta.Handel.RelacjeDokumentow.Api`), opisany w rozdziale o relacjach — tutaj koncentrujemy się na metodzie `NowaKorekta` oraz na specyfice każdego typu dokumentu.

> **Wspólne reguły** (powtórzone z fundamentów, [`safe-code.md`](../safe-code.md)):
> - Dostęp do serwisu: `var rel = session.GetRequiredService<IRelacjeService>();` (wymaga `using Microsoft.Extensions.DependencyInjection;`).
> - Dokument **nadrzędny / korygowany musi być zatwierdzony** (`StanDokumentuHandlowego.Zatwierdzony`) przed wywołaniem relacji.
> - Każda modyfikacja w transakcji (`session.Logout(editMode: true)` + `Commit()` / `CommitUI()` w workerze), potem `session.Save()`. Magazyn księguje się dopiero po `Save()`.
> - Pola `DokumentKorygowany`, `DokumentyKorygujące`, `DokumentyZaliczkowe` są **kalkulowane (read-only)** — nie ustawiaj ich ręcznie; powstają jako efekt utworzenia relacji.

---

### HANDEL-W48 — Korekta ilościowa i korekta ceny

**Cel:** utworzyć dokument korygujący do zatwierdzonej faktury / dokumentu magazynowego (zmiana ilości, ceny, rabatu lub VAT) i zapisać poprawione wartości na pozycjach korekty.

**Warianty:**

| Wariant | Wywołanie | Uwaga |
|---|---|---|
| Korekta pojedynczego dokumentu | `NowaKorekta(new[]{ dok }, symbolKorekty)` | zwraca tablicę korekt (zwykle 1 element) |
| Korekta zbiorcza (wiele dok. → jedna) | `NowaKorektaZbiorcza(korygowane, symbolKorekty)` | grupuje korygowane dokumenty |
| Domyślny symbol korekty | `NowaKorekta(new[]{ dok })` (bez symbolu) | platforma dobiera definicję korekty wg definicji korygowanego |
| Korekta ilościowa | po utworzeniu: zmiana `poz.Ilosc` na pozycji korekty | różnica ilości |
| Korekta ceny / rabatu | zmiana `poz.Cena` / `poz.Rabat` | różnica wartości |
| Korekta „do zera" (zwrot całości) | ustaw `poz.Ilosc = Quantity.Zero` (w jednostce pozycji) | pełny storno |

**Pola i typy:**
- `IRelacjeService.NowaKorekta(DokumentHandlowy[] korygowane, string symbolKorekty = null, Context context = null, HandlerSet handlers = null): DokumentHandlowy[]`.
- `IRelacjeService.NowaKorektaZbiorcza(DokumentHandlowy[] korygowane, string symbolKorekty = null, …): DokumentHandlowy[]`.
- Na pozycji korekty: `PozycjaDokHandlowego.Ilosc: Quantity`, `Cena: DoubleCy`, `Rabat: Percent`, `PozycjaKorygowana` (powiązanie z pozycją oryginału, read-only).
- Odczyt powiązań: `dok.DokumentyKorygujące` (kolekcja korekt), `korekta.DokumentKorygowany` (oryginał).

**Snippet:**

```csharp
using Microsoft.Extensions.DependencyInjection;
using Soneta.Handel;
using Soneta.Handel.RelacjeDokumentow.Api;
using Soneta.Types;

// 1. Oryginał musi być zatwierdzony:
using (var t = session.Logout(editMode: true)) {
    faktura.Stan = StanDokumentuHandlowego.Zatwierdzony;
    t.Commit();
}
session.Save();

// 2. Utworzenie korekty przez serwis relacji:
var rel = session.GetRequiredService<IRelacjeService>();

DokumentHandlowy korekta;
using (var t = session.Logout(editMode: true)) {
    korekta = rel.NowaKorekta(new[] { faktura }, "KWN")[0];   // symbol definicji korekty

    // 3. Korekta ilościowa: zmiana ilości na pozycji korekty
    //    (pozycje korekty są wstępnie zainicjowane wartościami oryginału)
    var poz = korekta.Pozycje.First();
    poz.Ilosc = new Quantity(8, poz.Ilosc.Symbol);   // było 10 -> korygujemy do 8

    // 4. Korekta ceny / rabatu — alternatywnie:
    // poz.Cena  = new DoubleCy(4.5m, poz.Cena.Symbol);
    // poz.Rabat = new Percent(0.15);

    t.Commit();
}
session.Save();

// Odczyt powiązania:
DokumentHandlowy oryginal = korekta.DokumentKorygowany;
```

**Pułapki:**
- `NowaKorekta` zwraca **tablicę** `DokumentHandlowy[]` — dla jednego dokumentu bierz `[0]` / `.Single()`.
- Korygowany dokument musi być **zatwierdzony**; korekta do dokumentu w buforze nie powstanie.
- Pozycje korekty są inicjowane wartościami oryginału — modyfikujesz je „do wartości docelowej", a system sam policzy różnicę. Nie wpisuj różnicy „z palca".
- `symbolKorekty` to symbol **definicji korekty** (np. „KWN", „KS"), a nie symbol korygowanej faktury. Definicja korekty musi istnieć i być odblokowana.
- Całą sekwencję (utworzenie + edycja pozycji) wykonuj w **jednej transakcji**, dopiero potem `Save()`.
- Symbol jednostki na `Ilosc` musi pochodzić z istniejącej pozycji (`poz.Ilosc.Symbol`) — nie twórz `Quantity` z gołą liczbą.

---

### HANDEL-W49 — Korekta wartości przyjęcia magazynowego

**Cel:** skorygować ilość/wartość przyjęcia magazynowego (PZ/PW) tak, aby poprawić zaksięgowane obroty i partie dostaw.

**Warianty:**

| Wariant | Mechanizm publiczny |
|---|---|
| Korekta przyjęcia ilościowa | `IRelacjeService.NowaKorekta(new[]{ przyjecie }, …)` + korekta `Ilosc` na pozycji |
| Korekta wartości (ceny) przyjęcia | jw., zmiana `Cena` na pozycji korekty |
| Korekta wskazanej dostawy / partii | korekta z odwołaniem do partii — `Soneta.Magazyny.GrupaDostaw` |

**Pola i typy:** te same co HANDEL-W48 — `IRelacjeService.NowaKorekta(...)`, `PozycjaDokHandlowego.Ilosc/Cena`, `PozycjaKorygowana`.

**Snippet:**

```csharp
var rel = session.GetRequiredService<IRelacjeService>();

DokumentHandlowy korektaPrzyjecia;
using (var t = session.Logout(editMode: true)) {
    // przyjecie = zatwierdzony dokument PZ/PW
    korektaPrzyjecia = rel.NowaKorekta(new[] { przyjecie })[0];

    var poz = korektaPrzyjecia.Pozycje.First();
    poz.Ilosc = new Quantity(9, poz.Ilosc.Symbol);   // przyjęto 10, korygujemy stan do 9

    t.Commit();
}
session.Save();   // tu księgują się skorygowane obroty/partie
```

**Pułapki:**
- **Dedykowany worker `UtworzKorektePrzyjeciaWorker` jest `internal`** — nie da się go zainstancjonować z dodatku zewnętrznego. Publiczny tor to **`IRelacjeService.NowaKorekta`** (wewnętrznie worker robi dokładnie to samo: `NowaKorekta` + dostosowanie `Pozycje[].Ilosc` z uwzględnieniem obrotów/storn).
- Korekta przyjęcia działa na zaksięgowanych obrotach i partiach — różnicowe wyliczenia ilości względem obrotów (`MagazynyModule.Obroty`) i storn wykonuje platforma. Z poziomu publicznego kontraktu ustaw docelową `Ilosc`/`Cena` na pozycji korekty.
- Magazyn (zasoby/obroty) aktualizuje się dopiero po `session.Save()`, nie po `Commit()`.
- Jeśli przyjęcie wskazywało partię/dostawę, korekta musi odnosić się do tej samej dostawy — przy złożonych scenariuszach (rozchody z tej partii, przesunięcia) korektę realizuj na pełnej, zalogowanej sesji aplikacyjnej.

---

### HANDEL-W50 — Dokument inwentaryzacji (INW)

**Cel:** utworzyć dokument spisu z natury (INW), na którym wprowadza się stany rzeczywiste; system wylicza różnice (nadwyżka / strata) względem stanu ewidencyjnego i generuje dokumenty korygujące stan.

**Warianty:**

| Wariant | Charakterystyka |
|---|---|
| Spis z natury | pozycje = stan rzeczywisty zliczony fizycznie |
| Stan początkowy / bilans otwarcia | INW jako dokument ustalający stany na start |
| Nadwyżka | stan rzeczywisty > ewidencyjny → relacja `InwentaryzacjaNadwyżka` |
| Strata / niedobór | stan rzeczywisty < ewidencyjny → relacja `InwentaryzacjaStrata` |
| Inwentaryzacja wg partii / wskazania dostawy | spis z dokładnością do partii (`GrupaDostaw`) |

**Pola i typy:**
- Definicja: `session.GetHandel().DefDokHandlowych.WgSymbolu["INW"]`.
- `DokumentHandlowy.Magazyn` (`Soneta.Magazyny.Magazyn`) — inwentaryzowany magazyn (wymagany).
- `PozycjaDokHandlowego.Ilosc: Quantity` — stan rzeczywisty.
- Dokumenty różnic (odczyt): `dok.Podrzędne[...]` / relacje inwentaryzacyjne; różnica wartości dostępna na dokumencie różnicy (np. `Ewidencja.Wartosc`).

**Snippet:**

```csharp
var hm = session.GetHandel();
var magazyny = session.GetMagazyny();
var towary = session.GetTowary();

DokumentHandlowy inw;
using (var t = session.Logout(editMode: true)) {
    inw = new DokumentHandlowy();
    session.AddRow(inw);
    inw.Definicja = hm.DefDokHandlowych.WgSymbolu["INW"];   // definicja PIERWSZA
    inw.Magazyn = magazyny.Magazyny.WgSymbol["F"];          // inwentaryzowany magazyn

    // Pozycja = stan rzeczywisty zliczony fizycznie:
    var poz = new PozycjaDokHandlowego(inw);
    session.AddRow(poz);
    poz.Towar = towary.Towary.WgKodu["BIKINI"];             // Towar PIERWSZY (inicjuje jednostkę)
    poz.Ilosc = new Quantity(9, poz.Ilosc.Symbol);          // ewidencyjnie 10 -> spis 9

    inw.Stan = StanDokumentuHandlowego.Zatwierdzony;        // zatwierdzenie wylicza różnice
    t.Commit();
}
session.Save();   // tu powstają dokumenty różnic i korekta stanu
```

**Pułapki:**
- INW wymaga **wskazanego magazynu**; bez niego nie da się policzyć różnic.
- Różnice (nadwyżka/strata) i ich zaksięgowanie powstają przy **zatwierdzeniu + Save**, nie wcześniej. Dokumenty różnic to obiekty podrzędne — czytaj je przez kolekcje relacji, nie twórz ręcznie.
- Inwentaryzacja wg partii wymaga wskazania dostawy/partii (`Soneta.Magazyny.GrupaDostaw`) — bez tego spis odnosi się do stanu zbiorczego.
- W bazie Demo obowiązuje blokada stanu ujemnego (`StanUjemnyVerifier`) — żeby spis miał sens, towar musi mieć wcześniejsze, **zapisane** przyjęcie (PW/PZ).
- Nie modyfikuj wartości na dokumentach różnic ręcznie — to wynik wyliczeń platformy.

---

### HANDEL-W51 — Faktura zaliczkowa i jej rozliczenie dokumentem końcowym

**Cel:** wystawić fakturę zaliczkową (FZAL) na poczet przyszłej dostawy, a następnie rozliczyć ją dokumentem końcowym (FV), tak by wartość końcowej została pomniejszona o wpłaconą zaliczkę.

**Warianty:**

| Wariant | Mechanizm |
|---|---|
| Utworzenie zaliczkowej z zamówienia | `NowyPodrzednyIndywidualny(new[]{ zamowienie }, "FZAL")` |
| Rozliczenie zaliczki na dokumencie końcowym | `NowyPodrzednyIndywidualny(new[]{ zaliczkowa }, "FV", handlers: …)` |
| Przenoszenie zaliczki **na pozycje** | callback `WybierzDokumentyZaliczkoweCallback` + `DokumentHandlowyRealizacjaZaliczkiWorker` |
| Przenoszenie zaliczki **wg stawki VAT** | callback `WybierzZaliczkiWgStawkiVatCallback` |
| Wiele zaliczek do jednej końcowej | dodaj wszystkie w callbacku (`Wybrany = true` dla każdej) |

**Pola i typy:**
- `IRelacjeService.NowyPodrzednyIndywidualny(DokumentHandlowy[] nadrzedne, string symbolPodrzednego, Context context = null, HandlerSet handlers = null): DokumentHandlowy[]`.
- `HandlerSet.WybierzDokumentyZaliczkoweCallback: Action<DokumentDocelowy>` — wskazanie zaliczek (tor „na pozycje").
- `HandlerSet.WybierzZaliczkiWgStawkiVatCallback: Action<DokumentDocelowy>` — tor „wg stawki VAT".
- Worker publiczny do wskazania zaliczki: `DokumentHandlowyRealizacjaZaliczkiWorker` z property `[Context] Dokument: DokumentHandlowy`, `[Context] Docelowy: DokumentDocelowy`, `Wybrany: bool`.
- Odczyt: `dok.DokumentyZaliczkowe` (kalkulowane) — zaliczki powiązane z końcowym; `dok.SumyVAT: SubTable<SumaVAT>`; `dok.BruttoCy`.

**Snippet:**

```csharp
using Microsoft.Extensions.DependencyInjection;
using Soneta.Handel;
using Soneta.Handel.RelacjeDokumentow.Api;

var rel = session.GetRequiredService<IRelacjeService>();

// zaliczkowa = zatwierdzona faktura zaliczkowa (FZAL).
// Rozliczamy ją dokumentem końcowym FV — callback wskazuje, które zaliczki przenieść:
DokumentHandlowy[] koncowy;
using (var t = session.Logout(editMode: true)) {
    koncowy = rel.NowyPodrzednyIndywidualny(
        new[] { zaliczkowa },
        "FV",
        handlers: new HandlerSet {
            WybierzDokumentyZaliczkoweCallback = WybierzZaliczki
        });
    t.Commit();
}
session.Save();

// koncowy[0].BruttoCy == 0, jeśli zaliczka pokryła całość

// Callback: zaznacza wszystkie dokumenty zaliczkowe powiązane z dokumentem docelowym.
static void WybierzZaliczki(DokumentDocelowy target) {
    var w = new DokumentHandlowyRealizacjaZaliczkiWorker { Docelowy = target };
    foreach (var d in target.DokumentyZaliczkowe.Cast<DokumentHandlowy>()) {
        w.Dokument = d;
        w.Wybrany = true;     // przenosi zaliczkę na dokument końcowy
    }
}
```

**Pułapki:**
- Bez dostarczenia odpowiedniego callbacka (`WybierzDokumentyZaliczkoweCallback` / `WybierzZaliczkiWgStawkiVatCallback`) domyślne handlery rzucają `NotImplementedException` — **musisz** wskazać tryb przenoszenia zaliczki zgodny z konfiguracją definicji końcowej (`SposobPrzenoszeniaZaliczki`: `NaPozycje` vs `NaDokument`).
- Tryb przenoszenia (na pozycje / wg stawki VAT) jest **cechą definicji** dokumentu końcowego — użyj callbacka pasującego do konfiguracji, inaczej rozliczenie nie zadziała.
- Worker rozliczenia (`RealizacjaZaliczkiWorker`, edytor kwot wg stawki) jest `internal` — z dodatku używaj publicznego `DokumentHandlowyRealizacjaZaliczkiWorker` (wskazanie dokumentów) wewnątrz callbacka.
- Faktura zaliczkowa musi być **zatwierdzona** przed rozliczeniem; `DokumentyZaliczkowe` to pole **kalkulowane** — nie ustawiasz go, czytasz.
- Tabela VAT dokumentu zaliczkowego jest przeliczana proporcjonalnie do wpłaconej zaliczki (logika `DokumentZaliczkowyWorker`) — nie modyfikuj `SumyVAT` ręcznie.

---

### HANDEL-W52 — Przesunięcie międzymagazynowe (MM)

**Cel:** przesunąć zasób z jednego magazynu do drugiego dokumentem MM — rozchód z magazynu źródłowego i przychód do magazynu docelowego w jednej operacji.

**Warianty:**

| Wariant | Mechanizm |
|---|---|
| Przesunięcie w obrębie firmy | MM z `MagazynZ` (źródło) i `MagazynDo` (cel) |
| Wskazanie partii / dostawy przy rozchodzie | pozycja z odwołaniem do `GrupaDostaw` |
| Korekta przesunięcia | `IRelacjeService.NowaKorekta(new[]{ mm }, …)` |

**Pola i typy:**
- Definicja: `session.GetHandel().DefDokHandlowych.WgSymbolu["MM"]`.
- `DokumentHandlowy.MagazynZ: Soneta.Magazyny.Magazyn` — magazyn źródłowy (rozchód).
- `DokumentHandlowy.MagazynDo: Soneta.Magazyny.Magazyn` — magazyn docelowy (**kalkulowane**: ustawia magazyn na podrzędnym dokumencie przesunięcia `Podrzędne[TypRelacjiHandlowej.PrzesunięcieDo]`; wymaga, by dokument przesunięcia już istniał — ustawiaj po `Definicja`).
- `PozycjaDokHandlowego.Towar`, `Ilosc: Quantity`.

**Snippet:**

```csharp
var hm = session.GetHandel();
var magazyny = session.GetMagazyny();
var towary = session.GetTowary();

DokumentHandlowy mm;
using (var t = session.Logout(editMode: true)) {
    mm = new DokumentHandlowy();
    session.AddRow(mm);
    mm.Definicja = hm.DefDokHandlowych.WgSymbolu["MM"];     // definicja PIERWSZA

    mm.MagazynZ  = magazyny.Magazyny.WgSymbol["F"];          // magazyn źródłowy
    mm.MagazynDo = magazyny.Magazyny.WgNazwa["Magazyn 2"];   // magazyn docelowy (po ustawieniu definicji)

    var poz = new PozycjaDokHandlowego(mm);
    session.AddRow(poz);
    poz.Towar = towary.Towary.WgKodu["BIKINI"];             // Towar PIERWSZY
    poz.Ilosc = new Quantity(5, poz.Ilosc.Symbol);

    mm.Stan = StanDokumentuHandlowego.Zatwierdzony;
    t.Commit();
}
session.Save();   // tu księguje się rozchód ze źródła i przychód do celu
```

**Pułapki:**
- `MagazynDo` jest **polem kalkulowanym** delegującym do podrzędnego dokumentu przesunięcia — ustaw je **po** `Definicja` (a najlepiej przed dodaniem pozycji), bo `IsReadOnlyMagazynDo()` blokuje zmianę magazynu, gdy istnieją już pozycje.
- `MagazynZ` i `MagazynDo` **muszą być różne** i oba dostępne (prawa do magazynów / przypisanie definicji do magazynu wg konfiguracji `Ogólne.PrzypisanieDefinicjiDoMagazynu`).
- Rozchód MM podlega blokadzie stanu ujemnego (Demo: `StanUjemnyVerifier`) — magazyn źródłowy musi mieć **zapisany** zasób przesuwanego towaru.
- Obroty (rozchód + przychód) księgują się po `session.Save()`, nie po `Commit()`.
- Korektę przesunięcia wykonuj przez `IRelacjeService.NowaKorekta` (jak w HANDEL-W48/HANDEL-W49); ręczna korekta partii przy MM jest złożona i wymaga pełnej sesji aplikacyjnej.

---

