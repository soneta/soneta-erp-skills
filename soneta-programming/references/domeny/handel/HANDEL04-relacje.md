# HANDEL04 — Relacje i generowanie dokumentów

> Wspólne fakty o typie, podstawowe typy i szablon wzorca: [../handel.md](../handel.md).

Rozdział opisuje **publiczny tor przekształceń dokumentów handlowych**: generowanie dokumentów
podrzędnych z nadrzędnych (zamówienie → faktura → dokument magazynowy), wiązanie i rozwiązywanie
powiązań oraz odczyt łańcucha relacji i stanu pokrycia zamówienia.

> **Punkt wejścia — `IRelacjeService`.** Cała logika relacji handlowych jest udostępniona dodatkom
> zewnętrznym **wyłącznie** przez serwis `Soneta.Handel.RelacjeDokumentow.Api.IRelacjeService`
> (scope: `Session`; pobieranie serwisów z DI — patrz [../../services.md](../../services.md)). Workery wykonawcze (`PowiazDokumentyWorker`, `UsunPowiazanieDokumentowWorker`,
> akcje menu „Relacje”) są **internal** — nie instancjonuj ich z dodatku. Pobranie serwisu:
>
> ```csharp
> using Microsoft.Extensions.DependencyInjection;            // GetRequiredService
> using Soneta.Handel.RelacjeDokumentow.Api;                 // IRelacjeService, HandlerSet
>
> var rel = session.GetRequiredService<IRelacjeService>();   // rzuca, gdy serwisu brak
> // albo: var rel = session.GetService<IRelacjeService>();  // zwraca null, gdy brak
> ```
>
> **Reguły wspólne dla całego rozdziału:**
> - Dokumenty **nadrzędne muszą być zatwierdzone** (`dok.Stan = StanDokumentuHandlowego.Zatwierdzony`)
>   — z bufora relacja nie powstanie.
> - Wywołanie metody serwisu (`NowyPodrzedny*`, `Dolacz*`) jest operacją modyfikującą — musi działać
>   **w otwartej transakcji edycyjnej** (`session.Logout(editMode: true)`), a po zamknięciu transakcji
>   zatwierdź zmiany przez `session.Save()`.
> - Wynik to `DokumentHandlowy[]` — tablica utworzonych/dołączonych dokumentów podrzędnych.
> - `Context` (zaznaczenie / parametry UI) i `HandlerSet` (callbacki rozstrzygające) są **opcjonalne**.
>   Jeśli definicja relacji wymaga rozstrzygnięcia (np. wyboru dostaw, magazynu, pozycji) i **nie
>   dostarczysz odpowiedniego callbacka**, platforma rzuci `NotImplementedException`.

### HandlerSet — callbacki rozstrzygające

`HandlerSet` to zbiór delegatów wołanych przez silnik relacji, gdy przekształcenie wymaga decyzji,
którą w UI podejmuje użytkownik. W trybie programowym (dodatek, test, worker bez UI) musisz je
dostarczyć sam — inaczej `NotImplementedException`. Najważniejsze:

| Callback | Typ | Kiedy potrzebny |
|---|---|---|
| `WybierzMagazynCallback` | `Func<Context, Magazyn>` | definicja relacji ma `WyborPozycji = WybórMagazynu` — wskaż magazyn docelowy |
| `WybierzMagazynDocelowyCallback` | `Func<DokumentDocelowy, Magazyn>` | wybór magazynu dla dokumentu docelowego (domyślnie `d.MagazynDo`) |
| `WybierzPozycjeCallback` | `Action<DokumentDocelowy>` | definicja ma `WyborPozycji = WybórPozycji` — zaznacz pozycje (domyślnie `PrzeliczPozycje()`) |
| `WybierzDostawyCallback` | `Action<DostawaWorker>` | wskazanie partii/dostaw przy rozchodzie (gdy `WskazaniePartii` wymuszone) |
| `WybierzDokumentyZaliczkoweCallback` | `Action<DokumentDocelowy>` | faktura z zaliczkami |
| `UstawParametryFakturowania` | `Action<DefRelacjiCyklicznaFakturowanieParams>` | fakturowanie cykliczne |

Domyślnie `WybierzPozycjeCallback` przepisuje wszystkie pozycje (`PrzeliczPozycje()`). Callbacki bez
sensownej wartości domyślnej (`WybierzMagazynCallback`, `WybierzDostawyCallback`,
`WybierzDokumentyZaliczkoweCallback`) rzucają `NotImplementedException`, dopóki ich nie nadpiszesz.

---

### HANDEL-W17 — Generowanie faktury z zamówienia (ZO → FV)

**Cel:** z zatwierdzonego zamówienia (odbiorcy `ZO` lub do dostawcy `ZD`) wygenerować pojedynczy
dokument podrzędny o wskazanym symbolu (np. fakturę `FV`). Relacja **jeden nadrzędny → jeden
podrzędny** (indywidualna).

**Warianty:**

| Wariant | Wejście | Symbol podrzędnego | Uwaga |
|---|---|---|---|
| ZO → FV | jedno zamówienie odbiorcy | `"FV"` | klasyczna realizacja sprzedaży |
| ZD → ZK (FZ) | zamówienie do dostawcy | `"ZK"` / `"FZ"` | zakup; może wymagać `WybierzMagazynCallback` |
| FA → WZ pojedynczo | jedna faktura | `"WZ"` | wydanie magazynowe do faktury (patrz HANDEL-W21) |
| Wszystkie pozycje | bez `HandlerSet` lub `WybierzPozycjeCallback` = przepisz wszystko | — | gdy definicja relacji ma `BrakOkna` |
| Wybrane pozycje | `WybierzPozycjeCallback` zaznacza podzbiór | — | gdy definicja ma `WybórPozycji` |

**Pola i typy:**
`IRelacjeService.NowyPodrzednyIndywidualny(DokumentHandlowy[] nadrzedne, string symbolPodrzednego,
Context context = null, HandlerSet handlers = null) → DokumentHandlowy[]`.
Wynik ma `Length == nadrzedne.Length` (każdy nadrzędny dostaje własny podrzędny).
Pozycja podrzędnego: `poz.Dostawa` (wskazana partia/dostawa, gdy dotyczy).

**Snippet:**

```csharp
using Microsoft.Extensions.DependencyInjection;
using Soneta.Handel;
using Soneta.Handel.RelacjeDokumentow.Api;

var rel = session.GetRequiredService<IRelacjeService>();

// zamowienie jest już zatwierdzone (StanDokumentuHandlowego.Zatwierdzony)
DokumentHandlowy[] faktury;
using (var t = session.Logout(editMode: true))
{
    faktury = rel.NowyPodrzednyIndywidualny(
        new[] { zamowienie },
        "FV");                                  // bez HandlerSet — gdy relacja nie wymaga rozstrzygnięć
    t.Commit();                                 // CommitUI() w workerze/extenderze
}
session.Save();

DokumentHandlowy faktura = faktury[0];          // jeden nadrzędny → jeden podrzędny
```

Wariant z wyborem pozycji (przepisz tylko pozycje danego towaru):

```csharp
using (var t = session.Logout(editMode: true))
{
    var wynik = rel.NowyPodrzednyIndywidualny(
        new[] { zamowienie }, "FV",
        handlers: new HandlerSet
        {
            WybierzPozycjeCallback = docelowy =>
            {
                // docelowy: DokumentDocelowy — zaznacz pozycje do przeniesienia
                docelowy.PrzeliczPozycje();      // domyślnie: wszystkie
            }
        });
    t.Commit();
}
session.Save();
```

**Pułapki:**
- Dokument nadrzędny **musi być zatwierdzony** — z bufora `NowyPodrzedny*` nie zadziała.
- Gdy definicja relacji wymaga rozstrzygnięcia (magazyn, dostawy, pozycje), a `HandlerSet` go nie
  dostarcza → `NotImplementedException`. Zacznij od wywołania bez `HandlerSet`; jeśli rzuca, dodaj
  konkretny callback (patrz tabela powyżej).
- Symbol podrzędnego musi odpowiadać **istniejącej definicji relacji** wychodzącej z definicji
  nadrzędnego (konfiguracja `DefRelacji` na `DefDokHandlowego`). Brak pasującej relacji → pusty wynik
  lub wyjątek.
- Cała operacja w **jednej** transakcji + `Save()`. Mieszane sesje rekordów → użyj `session.Get(...)`.

---

### HANDEL-W18 — Zbiorczy dokument magazynowy z wielu faktur (wiele FA → 1 WZ/PZ)

**Cel:** z wielu zatwierdzonych faktur utworzyć **jeden** zbiorczy dokument podrzędny (np. jeden
dokument magazynowy `WZ`/`PZ` zbierający pozycje wszystkich faktur). Relacja **wiele nadrzędnych →
jeden podrzędny** (zbiorcza).

**Warianty:**

| Wariant | Wejście | Symbol | Wynik |
|---|---|---|---|
| Wiele FA → 1 WZ | tablica faktur sprzedaży | `"WZ"` | 1 wydanie zbiorcze |
| Wiele FZ → 1 PZ | tablica faktur zakupu | `"PZ"` | 1 przyjęcie zbiorcze |
| Wiele ZO → 1 FV | zbiorcza faktura z zamówień | `"FV"` | 1 faktura zbiorcza |

**Pola i typy:**
`IRelacjeService.NowyPodrzednyZbiorczy(DokumentHandlowy[] nadrzedne, string symbolPodrzednego,
Context context = null, HandlerSet handlers = null) → DokumentHandlowy[]`.
W przeciwieństwie do HANDEL-W17 zwraca zwykle tablicę **jednoelementową** (jeden dokument zbiorczy).

**Snippet:**

```csharp
var rel = session.GetRequiredService<IRelacjeService>();

// faktury: DokumentHandlowy[] — wszystkie zatwierdzone, zgodne (ten sam kontrahent/magazyn wg konfiguracji)
DokumentHandlowy wz;
using (var t = session.Logout(editMode: true))
{
    var wynik = rel.NowyPodrzednyZbiorczy(faktury, "WZ");
    wz = wynik[0];                              // jeden zbiorczy dokument magazynowy
    t.Commit();
}
session.Save();
```

**Pułapki:**
- Dokumenty zbiorcze powstają tylko z dokumentów **zgodnych** (wymóg ten sam kontrahent / magazyn /
  waluta — zależnie od definicji relacji zbiorczej). Niezgodne wejście → wyjątek lub pominięcie.
- Wszystkie nadrzędne muszą być **zatwierdzone**.
- **Relacja musi być zarejestrowaną akcją przekształcenia dla dokumentu źródłowego** — sprawdza to
  `dok.ResolveActions()`. Gdy danej relacji zbiorczej nie ma (zależy od konfiguracji/wersji), serwis
  rzuca `InvalidOperationException("Operacja tworzenia relacji nie jest dostępna")`. Np. w czystej bazie
  Demo zbiorcza relacja **FV → WZ** nie jest dostępną akcją faktury (FV→WZ działa tylko indywidualnie —
  HANDEL-W17/relacja indywidualna); zbiorczy wariant jest pewny np. dla ZO → FV.
- Tak jak w HANDEL-W17 — brak wymaganego callbacka w `HandlerSet` → `NotImplementedException`.
- Nie zakładaj `Length == nadrzedne.Length` — tu wynik jest **agregatem** (zwykle 1 dokument).

---

### HANDEL-W19 — Zbiorcza faktura z wielu dokumentów magazynowych (wiele WZ → 1 FA)

**Cel:** „odwrotny” kierunek HANDEL-W18 — z wielu zatwierdzonych dokumentów magazynowych (np. `WZ`)
utworzyć **jedną** zbiorczą fakturę sprzedaży.

**Warianty:**

| Wariant | Wejście | Symbol | Uwaga |
|---|---|---|---|
| Wiele WZ → 1 FV | wydania magazynowe | `"FV"` | fakturowanie zbiorcze rozchodów |
| Wiele PZ → 1 FZ | przyjęcia magazynowe | `"FZ"` | zbiorczy zakup |

**Pola i typy:** ta sama metoda `NowyPodrzednyZbiorczy(...)` co w HANDEL-W18 — różni się tylko kierunkiem
(nadrzędne = dokumenty magazynowe, symbol podrzędnego = faktura).

**Snippet:**

```csharp
var rel = session.GetRequiredService<IRelacjeService>();

// wydania: DokumentHandlowy[] — zatwierdzone WZ tego samego kontrahenta
DokumentHandlowy fakturaZbiorcza;
using (var t = session.Logout(editMode: true))
{
    fakturaZbiorcza = rel.NowyPodrzednyZbiorczy(wydania, "FV")[0];
    t.Commit();
}
session.Save();
```

**Pułapki:**
- Kierunek relacji (magazynowy → handlowy) musi być skonfigurowany jako `DefRelacji` na definicji
  dokumentu magazynowego. Brak relacji → pusty wynik.
- Dokumenty magazynowe muszą być **zatwierdzone** i zgodne (kontrahent / waluta).
- Walidator stanu ujemnego nie dotyczy tej operacji (rozchód już się dokonał na WZ), ale faktura
  przejmie wartości z dokumentów źródłowych — nie modyfikuj pozycji ręcznie po przekształceniu, jeśli
  ma zachować zgodność z magazynem.

---

### HANDEL-W20 — Wyszukiwanie dokumentów powiązanych (odczyt pól kalkulowanych)

**Cel:** odczytać dokumenty powiązane bez ręcznego przeszukiwania relacji — przez pola kalkulowane na
`DokumentHandlowy`. Działa w obie strony: dla faktury → jej dokumenty magazynowe, dla magazynowego →
jego faktury.

**Warianty:**

| Wariant | Pole kalkulowane | Typ | Zwraca |
|---|---|---|---|
| Magazynowe dla faktury | `dok.DokumentyMagazynowe` | `DokumentHandlowy[]` | WZ/PZ powiązane z fakturą |
| Główny dok. magazynowy | `dok.DokumentMagazynowyGłówny` | `DokumentHandlowy` | pierwszy/główny magazynowy |
| Faktury dla magazynowego | `dok.DokumentyHandlowe` | `DokumentHandlowy[]` | faktury powiązane z WZ/PZ/ZO/ofertą |

**Pola i typy:** wszystkie trzy to **właściwości kalkulowane (read-only)** na `DokumentHandlowy`.
`DokumentyMagazynowe` dla dokumentu, który **sam jest magazynowy** (`TypPartii.Magazynowy` itd.),
zwraca `{ this }`. Analogicznie `DokumentyHandlowe` dla samego dokumentu handlowego zwraca `{ this }`.

**Snippet:**

```csharp
// 1. Dla faktury — jej dokumenty magazynowe (wydania/przyjęcia)
foreach (DokumentHandlowy mag in faktura.DokumentyMagazynowe)
{
    // mag.Numer, mag.Magazyn, mag.Pozycje ...
}

// główny dokument magazynowy (gdy potrzebny jeden)
DokumentHandlowy glowny = faktura.DokumentMagazynowyGłówny;

// 2. Dla dokumentu magazynowego — faktury, które go „obsługują”
foreach (DokumentHandlowy fa in wz.DokumentyHandlowe)
{
    // fa.Numer, fa.Suma ...
}
```

**Pułapki:**
- To pola **kalkulowane** — czytaj, nie ustawiaj. Każde odwołanie uruchamia wyszukiwanie po relacjach,
  więc **nie wołaj ich w pętli** dla tysięcy rekordów — buforuj wynik w zmiennej lokalnej.
- Zwracają **tablicę** (może być pusta), nie `null` — bezpiecznie iterować, ale sprawdzaj `.Length`
  przed `[0]`.
- Pola respektują **prawa dostępu** — dokumenty bez prawa odczytu są pomijane (wynik może być węższy
  niż faktyczny łańcuch relacji).

---

### HANDEL-W21 — Generowanie dokumentu magazynowego z faktury (FA → WZ pojedynczo)

**Cel:** do pojedynczej zatwierdzonej faktury wygenerować odpowiadający dokument magazynowy
(np. wydanie `WZ`). To wariant indywidualny (HANDEL-W17), tylko z innym symbolem docelowym.

**Warianty:**

| Wariant | Wejście | Symbol | Uwaga |
|---|---|---|---|
| FV → WZ | faktura sprzedaży | `"WZ"` | wydanie z magazynu |
| FZ → PZ | faktura zakupu | `"PZ"` | przyjęcie do magazynu |
| Z wyborem partii | + `WybierzDostawyCallback` | — | gdy `WskazaniePartii` wymuszone na definicji WZ |

**Pola i typy:** `IRelacjeService.NowyPodrzednyIndywidualny(...)` — jak HANDEL-W17. Pozycje magazynowe mają
`poz.Dostawa` (wskazana partia/dostawa).

**Snippet (z wyborem partii — wymusza `HandlerSet`):**

```csharp
using Soneta.Magazyny;

var rel = session.GetRequiredService<IRelacjeService>();

DokumentHandlowy wz;
using (var t = session.Logout(editMode: true))
{
    var wynik = rel.NowyPodrzednyIndywidualny(
        new[] { faktura }, "WZ",
        handlers: new HandlerSet
        {
            WybierzDostawyCallback = dostawaWorker =>
            {
                // dla każdej pozycji wskaż pobierane zasoby/partie
                foreach (var poz in dostawaWorker.GetListPozycja())
                {
                    dostawaWorker.Pozycja = poz;
                    foreach (Zasob z in dostawaWorker.Zasoby.Cast<Zasob>())
                    {
                        using var tz = z.Session.Logout(editMode: true);
                        // ... oznacz zasób jako pobrany (Pobrano = true)
                        tz.Commit();
                    }
                }
            }
        });
    wz = wynik[0];
    t.Commit();
}
session.Save();
```

**Pułapki:**
- Gdy definicja `WZ` ma `WskazaniePartii = WymuszonyDodawanie`, **musisz** dostarczyć
  `WybierzDostawyCallback` — inaczej `NotImplementedException`.
- Rozchód wymaga wcześniejszego **zapisanego** przyjęcia towaru (`StanUjemnyVerifier` w Demo). Magazyn
  księguje się dopiero po `Session.Save()` — samo `Commit`/`CommitUI` nie tworzy obrotów/zasobów.
- Po wygenerowaniu WZ odczytaj go zwrotnie przez `faktura.DokumentyMagazynowe` (HANDEL-W20).

---

### HANDEL-W22 — Kopiowanie faktury klientowi (`KopiujKlientowiFaktureWorker`)

**Cel:** skopiować zatwierdzone faktury sprzedaży klienta jako dokumenty zakupu **do bazy klienta**
(scenariusz biura rachunkowego pracującego na wielu bazach). Worker **publiczny**.

**Dostępność:** `Soneta.EI.KopiujKlientowiFaktureWorker` jest **public** (rejestracja
`[assembly: Worker(typeof(KopiujKlientowiFaktureWorker), typeof(DokHandlowe))]`). Akcja menu
„Kopiuj klientowi...”. **Widoczna tylko** gdy bieżąca baza jest *master* w konfiguracji „Praca na
wielu bazach” **i** licencja to `Biuro Rachunkowe` (`IsVisibleKopiuj`). Bez tej konfiguracji
nie zadziała (nie znajdzie bazy klienta).

**Pola i typy:**
- `[Context] DokumentHandlowy[] Dokumenty` — kopiowane faktury (brane są tylko `Zatwierdzony`).
- `[Context] Params Prms` — parametry; `Params : ContextBase`:
  - `DefinicjaDokumentu Definicja` — definicja dokumentu zakupu w bazie klienta (lista z
    `DefDokumentow.WgTypu[TypDokumentu.ZakupEwidencja]`);
  - `bool PrzygotujPrzelewy` (domyślnie `true`) — czy generować przelewy dla zobowiązań.
- `object Kopiuj()` — akcja `[Action("Kopiuj klientowi...", Mode = SingleSession | Progress)]`;
  zwraca komunikat tekstowy, szczegóły pisze do logu.

**Snippet (programowe użycie workera z `Params`):**

```csharp
using Soneta.EI;

// dokumenty: zaznaczone faktury sprzedaży (worker bierze tylko zatwierdzone)
var prms = new KopiujKlientowiFaktureWorker.Params(context)
{
    Definicja = /* DefinicjaDokumentu zakupu */,
    PrzygotujPrzelewy = true,
};

var worker = new KopiujKlientowiFaktureWorker
{
    Dokumenty = dokumenty,
    Prms = prms,
};

object komunikat = worker.Kopiuj();   // tworzy dokumenty w bazie klienta; Save robi worker wewnętrznie
```

**Pułapki:**
- Worker działa **na wielu bazach** (`DBItemContext`) — sam otwiera/zamyka transakcje i `Save()`
  w bazie klienta. Nie opakowuj wywołania w zewnętrzną transakcję na bazie master.
- Kopiowane są **tylko faktury zatwierdzone**; dokumenty z zobowiązaniem (nie należnością) są
  **pomijane** (zakup wymaga należności po stronie sprzedaży).
- W bazie klienta tworzony jest automatycznie kontrahent „biuro” (wg NIP z pieczątki firmy), jeśli go
  brak. Brakujący sposób zapłaty w bazie klienta → dokument pominięty (log).
- Wymaga licencji `Biuro Rachunkowe` i roli master — w innym układzie akcja jest niewidoczna.
- Do zwykłego „kopiuj dokument w tej samej bazie” ten worker **nie służy** — to specjalizowany scenariusz
  wielobazowy.

---

### HANDEL-W23 — Ręczne wiązanie i rozwiązywanie powiązań

**Cel:** **dołączyć** istniejący dokument do innego jako podrzędny/nadrzędny (bez generowania nowego)
oraz rozwiązać błędnie utworzone powiązanie. Tor publiczny = `IRelacjeService.Dolacz*`.

> **Uwaga o dostępności:** workery wykonawcze `PowiazDokumentyWorker` i
> `UsunPowiazanieDokumentowWorker` są **internal** — nie używaj ich z dodatku. Wiązanie realizuj przez
> `IRelacjeService.DolaczPodrzednyIndywidualny` / `DolaczNadrzedny`. **Programowego, publicznego API do
> *rozwiązywania* powiązań brak** — rozwiązywanie powiązań jest dostępne tylko interaktywnie (menu
> „Relacje” w aplikacji), bo odpowiedni worker jest internal. To ograniczenie publicznego kontraktu.

**Warianty:**

| Wariant | Metoda | `relationName` |
|---|---|---|
| Dołącz podrzędny do nadrzędnego | `DolaczPodrzednyIndywidualny(documents, relationName)` | nazwa definicji relacji wychodzącej (np. `"Faktura"`) |
| Dołącz dokument do nadrzędnego | `DolaczNadrzedny(documents, relationName)` | nazwa relacji od strony nadrzędnego (np. `"Zamówienie"`) |
| Rozwiązanie powiązania | — | **tylko interaktywnie** (worker internal) |

**Pola i typy:**
```csharp
DokumentHandlowy[] DolaczPodrzednyIndywidualny(
    DokumentHandlowy[] documents, string relationName,
    Context context = null, HandlerSet handlers = null);
DokumentHandlowy[] DolaczNadrzedny(
    DokumentHandlowy[] documents, string relationName,
    Context context = null, HandlerSet handlers = null);
```
`relationName` to **nazwa definicji relacji** (`DefRelacji`), nie symbol dokumentu — np. `"Zamówienie"`,
`"Faktura"`, `"Korekta wydania magazynowego 2"`.

**Snippet:**

```csharp
var rel = session.GetRequiredService<IRelacjeService>();

// Dołącz fakturę do istniejącego zamówienia jako nadrzędnego (relacja "Zamówienie")
using (var t = session.Logout(editMode: true))
{
    var powiazane = rel.DolaczNadrzedny(new[] { faktura }, "Zamówienie");
    t.Commit();
}
session.Save();
```

**Pułapki:**
- `relationName` musi dokładnie pasować do **nazwy `DefRelacji`** skonfigurowanej w bazie (wielkość
  liter / spacje istotne) — niepasująca nazwa daje pusty/`null` wynik w tablicy.
- `Dolacz*` przetwarza dokumenty **pojedynczo** (`Array.ConvertAll`) — wynik na pozycji `i` może być
  `null`, jeśli dołączenie konkretnego dokumentu się nie powiodło. Sprawdzaj elementy wyniku.
- Dokumenty muszą być **zatwierdzone** i wzajemnie zgodne (kontrahent / pozycje).
- **Rozwiązywanie** powiązań programowo z dodatku **niedostępne** — zaplanuj operację jako działanie
  użytkownika w aplikacji (menu „Relacje”).

---

### HANDEL-W24 — Odczyt łańcucha powiązań i stan pokrycia zamówienia

**Cel:** prześledzić łańcuch relacji (oferta → zamówienie → faktura → dokument magazynowy) oraz
odczytać **stan pokrycia/realizacji zamówienia** (czy zamówienie zostało zrealizowane fakturami).

**Warianty:**

| Wariant | Mechanizm | Typ wyniku |
|---|---|---|
| W górę łańcucha (faktury dla magazynowego/zamówienia) | `dok.DokumentyHandlowe` (HANDEL-W20) | `DokumentHandlowy[]` |
| W dół łańcucha (magazynowe dla faktury) | `dok.DokumentyMagazynowe` (HANDEL-W20) | `DokumentHandlowy[]` |
| Stan pokrycia zamówienia (odczyt) | `StanPokryciaZamówieniaWorker.StanPokrycia` | enum `StanPokryciaZamówienia` |

**Pola i typy:**
- Odczyt stanu pokrycia: worker **public** `Soneta.Handel.StanPokryciaZamówieniaWorker`
  (`[Context] DokumentHandlowy Dokument`) → property `StanPokrycia : StanPokryciaZamówienia`.
- Enum `Soneta.Handel.StanPokryciaZamówienia`: `Brak = 0`, `Częściowe = 1`, `Pełne = 2`,
  `NiePodlega = 3`, `Niezweryfikowane = 4`.
- **Ważne:** worker tylko **odczytuje** wcześniej wyliczony stan (z cache na `Login`). Samo
  przeliczenie uruchamia akcja menu „Sprawdź pokrycie” (`StanPokryciaZamowienWorker`, `[HandelAction]`)
  — wywołuje ją użytkownik; dopóki nie zostanie odpalona, `StanPokrycia` zwraca `Niezweryfikowane`.

**Snippet:**

```csharp
using Soneta.Handel;

// Odczyt stanu pokrycia pojedynczego zamówienia (po wcześniejszym „Sprawdź pokrycie”):
var w = new StanPokryciaZamówieniaWorker { Dokument = zamowienie };
StanPokryciaZamówienia stan = w.StanPokrycia;

bool zrealizowane = stan == StanPokryciaZamówienia.Pełne;

// Łańcuch relacji w dół: zamówienie -> faktury -> ich dokumenty magazynowe
foreach (DokumentHandlowy fa in zamowienie.DokumentyHandlowe)        // faktury zamówienia
    foreach (DokumentHandlowy mag in fa.DokumentyMagazynowe)         // wydania faktury
    {
        // mag.Numer, mag.Magazyn ...
    }
```

**Pułapki:**
- `StanPokryciaZamówieniaWorker.StanPokrycia` zwraca `Niezweryfikowane`, dopóki w sesji/loginie nie
  wykonano przeliczenia (akcja „Sprawdź pokrycie”). **Programowego, publicznego wyzwalacza
  przeliczenia brak** — `StanPokryciaZamówień.Przelicz()` jest wywoływane przez internal akcję menu.
  Z dodatku traktuj `StanPokrycia` jako **odczyt** stanu policzonego interaktywnie.
- Pola `DokumentyHandlowe`/`DokumentyMagazynowe` respektują prawa dostępu i są kalkulowane — buforuj
  wynik, nie wołaj w gęstych pętlach (HANDEL-W20).
- Stan `NiePodlega` oznacza dokument, którego pokrycie nie dotyczy (np. nie jest zamówieniem) —
  rozróżniaj go od `Brak` (zamówienie bez realizacji).

---

> **Powiązane sekcje:** tworzenie/stan dokumentu (sekcja 1–2), korekty (`IRelacjeService.NowaKorekta`,
> `NowaKorektaZbiorcza` — analogiczne do HANDEL-W17/HANDEL-W18, symbol korekty opcjonalny), magazyn i partie
> (`dok.Zasoby`, `dok.Obroty`, `GrupaDostaw`).

---

