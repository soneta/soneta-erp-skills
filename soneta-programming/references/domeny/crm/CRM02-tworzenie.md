# CRM02 — Tworzenie, modyfikacja, usuwanie

> Wspólne fakty o typie, podstawowe typy i szablon wzorca: [../crm.md](../crm.md).

### CRM-W3 — Tworzenie kontrahenta

**Cel:** utworzyć nowy rekord kontrahenta z poprawnym minimalnym zestawem pól i wartościami domyślnymi.

**Warianty:**

| Wariant | Charakterystyka | Pola krytyczne |
|---|---|---|
| Podmiot gospodarczy krajowy | firma w PL | `StatusPodmiotu=PodmiotGospodarczy`, `RodzajPodmiotu=Krajowy`, `NIP` |
| Unijny / zagraniczny | sprzedaż wewn.-unijna / eksport | `EuVAT`, `RodzajPodmiotu=Unijny/Eksportowy` |
| Osoba fizyczna / finalny | konsument | `StatusPodmiotu=Finalny`, `PESEL` |

**Pola i typy:** `Kod: string`, `Nazwa: string`, `StatusPodmiotu: Soneta.Core.StatusPodmiotu`
(`PodmiotGospodarczy=0`, `Finalny=1`), `RodzajPodmiotu: Soneta.Core.RodzajPodmiotu`
(`Krajowy=0`, `Eksportowy=1`, `EksportowyPodróżny=2`, `Unijny=3`, `UnijnyTrójstronny=4`, `BezVAT=5`),
`PodatnikVAT: bool`, `FormaPrawna: Soneta.CRM.FormaPrawna`.

**Nadawanie kodu / numeracji:** `Kod` jest polem tekstowym ustawianym jawnie. Może być wymagana jego
unikalność (zależnie od konfiguracji modułu CRM); w razie kolizji `Save()` zgłosi `RowException` z
`DuplicateKeyException` w `InnerException` (obsługa wyjątków zapisu — patrz [../../safe-code.md](../../safe-code.md)).

**Snippet:**

```csharp
var crm = session.GetCRM();

using (var t = session.Logout(editMode: true))
{
    var k = new Kontrahent();
    crm.Kontrahenci.AddRow(k);                 // najpierw dodaj do tabeli, potem ustawiaj pola

    k.Kod = "FIRMA001";
    k.Nazwa = "Firma XYZ Sp. z o.o.";
    k.StatusPodmiotu = StatusPodmiotu.PodmiotGospodarczy;
    k.RodzajPodmiotu = RodzajPodmiotu.Krajowy;
    k.PodatnikVAT = true;
    k.NIP = "1234563218";                      // ustawienie NIP synchronizuje EuVAT

    t.Commit();                                // Commit() w kodzie biznesowym
}
session.Save();                                // zapis do bazy; tu wykryte konflikty/duplikaty
```

**Pułapki:**
- Tworzenie **wyłącznie w transakcji** (`session.Logout(editMode: true)`). `AddRow` przed
  ustawianiem pól.
- W workerze/extenderze (uruchamianym z UI) używaj `t.CommitUI()` zamiast `t.Commit()`
  (safe-code, [`worker-extender.md`](../../worker-extender.md)).
- `Nazwa` jest zapisywalna; `NazwaFormatowana`/`NazwaPierwszaLinia` są kalkulowane — nie ustawiaj.
- Dla podmiotu unijnego ustaw `EuVAT` (z prefiksem kraju) — platforma sama dostosuje `RodzajPodmiotu`.
- Brak `Commit()` = automatyczny rollback przy `Dispose()`.

### CRM-W4 — Modyfikacja i statusy

**Cel:** zmienić dane istniejącego kontrahenta lub jego status dostępności/handlowy.

**Warianty:**

| Wariant | Pole / operacja |
|---|---|
| Edycja danych identyfikacyjnych | `Kod`, `Nazwa`, `NIP`, … (blokada optymistyczna) |
| Ukrycie na listach | `Blokada: bool` |
| Blokada sprzedaży | `BlokadaSprzedazy: bool` |
| Zmiana formy prawnej | `FormaPrawna` (poj. lub masowo: worker `ZmienFormePrawnaKontrahentowWorker`) |
| Zastąpienie (zamiennik) | `Zamiennik: Kontrahent` (ustawia automatycznie `Blokada=true`) |
| Kopiowanie kontrahenta | worker `Soneta.CRM.KopiujKontrahentaWorker` (akcja „Kopiuj kontrahenta...") |

**Pola i typy:** `Blokada: bool`, `BlokadaSprzedazy: bool`, `FormaPrawna: Soneta.CRM.FormaPrawna`,
`Zamiennik: Soneta.CRM.Kontrahent`.

**Snippet:**

```csharp
var crm = session.GetCRM();
var k = crm.Kontrahenci.WgKodu["FIRMA001"];
if (k == null) return;

using (var t = session.Logout(editMode: true))
{
    k.Nazwa = "Firma XYZ S.A.";
    k.BlokadaSprzedazy = true;                 // zakaz wystawiania dokumentów rozchodu
    k.Blokada = true;                          // ukrycie na listach
    t.Commit();
}
session.Save();

// Kopiowanie kontrahenta — programowe użycie workera (bez UI):
var kopiarka = new KopiujKontrahentaWorker { Kontrahent = k };
using (var t = session.Logout(editMode: true))
{
    Kontrahent nowy = kopiarka.Kopiuj();
    t.Commit();
}
session.Save();
```

**Pułapki:**
- **Blokada optymistyczna**: konflikt edycji (ktoś inny zapisał ten rekord) wybucha w `session.Save()`
  jako `RowConflictException` — obsłuż go (refresh + retry lub eskalacja), nie połykaj (safe-code §4).
- Nie nadpisuj `Kod` rekordów standardowych (`IsStandard == true`) ani incydentalnego
  (`JestIncydentalny == true`).
- `Zamiennik` ma efekt uboczny — ustawienie zamiennika włącza `Blokada=true`. Do rozwiązania
  „aktualnego" kontrahenta służy `Kontrahent.Coalesce(k)` (zwraca zamiennika albo sam rekord).
- Worker `KopiujKontrahentaWorker` ma property `[Context] Kontrahent` — przy ręcznym użyciu ustaw ją
  przed wywołaniem `Kopiuj()`; operacja musi być w transakcji.

### CRM-W5 — Bezpieczne usuwanie

**Cel:** usunąć kontrahenta albo świadomie odmówić usunięcia, gdy istnieją powiązania.

**Warianty:**

| Wariant | Sytuacja | Zalecenie |
|---|---|---|
| Usunięcie czyste | brak dokumentów/rozrachunków/zadań/zdarzeń | dozwolone (`DeleteRow`) |
| Usunięcie zablokowane | są dokumenty/rozrachunki/zapisy | zamiast usuwać → `Blokada=true` |
| Kontrahent systemowy | `IsStandard` / `JestIncydentalny` | nie usuwać |

**Pola i typy:** `DokumentyHandlowe`, `Rozrachunki`, `Zadania`, `Zdarzenia` (kolekcje `SubTable`),
`IsStandard: bool`, `JestIncydentalny: bool`, `Blokada: bool`.

**Snippet:**

```csharp
var crm = session.GetCRM();
var k = crm.Kontrahenci.WgKodu["FIRMA001"];
if (k == null) return;

if (k.IsStandard || k.JestIncydentalny)
    throw new BusException("Nie można usunąć kontrahenta systemowego.".Translate());

bool maPowiazania = !k.DokumentyHandlowe.IsEmpty || !k.Rozrachunki.IsEmpty
                    || !k.Zadania.IsEmpty || !k.Zdarzenia.IsEmpty;

using (var t = session.Logout(editMode: true))
{
    if (maPowiazania)
        k.Blokada = true;                      // miękkie wycofanie zamiast usunięcia
    else
        k.Delete();                            // twarde usunięcie tylko gdy brak powiązań
    t.Commit();
}
session.Save();
```

**Pułapki:**
- Sprawdź powiązania **przed** `DeleteRow()`. Próba usunięcia powiązanego rekordu i tak zostanie
  odrzucona przez integralność (wyjątek w `Save()`), ale lepiej zdecydować świadomie.
- Preferuj `Blokada=true` (kontrahent znika z list, dane pozostają) zamiast kasowania, gdy są
  powiązania historyczne.
- `IsEmpty`/`Any` na kolekcji `SubTable` to **właściwości** (test serwerowy `exists …`, bez
  nawiasów) — nie materializuj kolekcji do pamięci (`.ToList().Count`).

---

