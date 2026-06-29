# HANDEL03 — Stany dokumentu i cykl życia

> Wspólne fakty o typie, podstawowe typy i szablon wzorca: [../handel.md](../handel.md).

Stan dokumentu handlowego steruje całym jego cyklem życia: od bufora (rekord roboczy, swobodnie
edytowalny i usuwalny), przez zatwierdzenie (księgowanie obrotów magazynowych, generowanie
płatności, blokada większości pól), aż po anulowanie. Stanem steruje **jedno zapisywalne pole**
`dok.Stan`, a dodatkowe operacje serwisowe (naprawa, przeliczenie) wykonują publiczne workery.

> **Fundamenty** (sesja, transakcja edycyjna `session.Logout(editMode: true)`, `Commit`/`CommitUI`,
> blokada optymistyczna w `Save()`) opisuje [`safe-code.md`](../../safe-code.md) — tu się do nich
> odwołujemy, nie powtarzamy. Cały kod jest zgodny z **C# 10** i operuje wyłącznie na **publicznym
> kontrakcie** platformy.

**Fakty o stanie (zweryfikowane):**

- **Pole sterujące:** `dok.Stan: Soneta.Handel.StanDokumentuHandlowego` (zapisywalne w transakcji).
- **Enum `StanDokumentuHandlowego`:** `Bufor=0`, `Zatwierdzony=1`, `Zablokowany=2`, `Anulowany=3`.
  Wartość `Zablokowany` ustawia **platforma** (np. po zaksięgowaniu w ewidencji) — nie ustawiaj jej z
  dodatku „z palca".
- **Skróty kalkulowane (tylko do odczytu, `bool`):** `dok.Bufor`, `dok.Zatwierdzony`, `dok.Anulowany`.
- **Usunięcie z bufora:** `dok.Delete()` w transakcji (tylko gdy brak zależności).
- **Workery publiczne (cykl życia / naprawa):** `Soneta.Handel.PoprawaStanuDokumentuWorker`,
  `Soneta.Magazyny.PrzeliczenieStanuWorker`.

---

### HANDEL-W12 — Zatwierdzenie dokumentu (bufor → zatwierdzony)

**Cel:** przeprowadzić dokument z bufora do stanu zatwierdzonego. Dopiero zatwierdzenie + `Save()`
księguje obroty magazynowe, tworzy zasoby/partie, generuje płatności i czyni dokument nadrzędnym dla
relacji (np. ZO→FV, FA→WZ — patrz rozdział o relacjach).

**Warianty:**

| Wariant | Operacja | Uwaga |
|---|---|---|
| Zatwierdzenie pojedyncze | `dok.Stan = StanDokumentuHandlowego.Zatwierdzony` | w transakcji + `Save()` |
| Zatwierdzenie zbiorcze | worker `EwidencjonowanieZbiorczeWorker` (`[Context] DokumentHandlowy[]`) | wiele dokumentów naraz |
| Sprawdzenie stanu | `dok.Zatwierdzony` / `dok.Bufor` (kalkulowane `bool`) | bez porównywania enuma |
| Stan `Zablokowany` | ustawiany przez platformę (księgowanie ewidencji) | nie ustawiaj ręcznie |

**Pola i typy:** `dok.Stan: StanDokumentuHandlowego` (zapisywalne), `dok.Bufor/Zatwierdzony/Anulowany:
bool` (kalkulowane). Wartości magazynowe widoczne **po** `Save()`: `dok.Zasoby`, `dok.Obroty`,
`dok.SumyVAT`.

**Snippet:**

```csharp
var hm = session.GetHandel();
var dok = hm.DokHandlowe.WgDaty[/* ... */];  // odczytany dokument w buforze

using (var t = session.Logout(editMode: true))
{
    dok.Stan = StanDokumentuHandlowego.Zatwierdzony;  // bufor -> zatwierdzony
    t.Commit();                                        // CommitUI() w workerze/extenderze
}
session.Save();   // DOPIERO TERAZ księgowane są obroty/zasoby/płatności

// Sprawdzenie po zapisie — czytaj pola kalkulowane, nie porównuj enuma:
if (dok.Zatwierdzony)
{
    foreach (var z in dok.Zasoby) { /* zasoby utworzone przez dokument przychodowy */ }
}
```

**Pułapki:**

- **Magazyn księguje się dopiero po `Save()`** — samo `Commit()`/`CommitUI()` nie tworzy obrotów ani
  zasobów. Jeśli baza blokuje stan ujemny (weryfikator `StanUjemnyVerifier`, jak w bazie Demo),
  rozchód (FV/WZ/RW) wymaga **wcześniej zapisanego** przyjęcia (PW/PZ) tego towaru — inaczej `Save()`
  rzuci wyjątek.
- **Faktura sprzedaży (FV) — zapisz pozycje PRZED zatwierdzeniem.** Zatwierdzenie dokumentu sprzedaży
  tworzy **ewidencję VAT** (`DokumentHandlowy.CreateEwidencja`), która czyta `dok.KrajPodatkuVat`. Pole to
  jest przeliczane z `pozycja.Stawka.Kraj` przez **odroczone zdarzenie sesji wykonywane na `Save()`**
  (nie na `Commit()`). Jeśli dodasz pozycje i zatwierdzisz w **tej samej** sesji (sam `CommitUI`, bez
  `Save()` pomiędzy), `KrajPodatkuVat` jest jeszcze `null` → zatwierdzenie rzuca `NullReferenceException`
  w ewidencji VAT. Poprawna kolejność: utwórz FV z pozycjami → **`Save()`** (przelicza `KrajPodatkuVat`)
  → odczytaj na świeżej sesji → dopiero teraz `Stan = Zatwierdzony`. (W testach robi to helper
  `UtworzZatwierdzonaFakture` w `DokumentHandlowyTestBase`.)
- Zatwierdzenie uruchamia walidatory dokumentu (kompletność pozycji, magazyn, kontrahent, tabela
  VAT). Błędy wychodzą w `Commit()`/`Save()` jako `RowException` — nie połykaj ich (safe-code §4).
- W workerze/extenderze użyj `t.CommitUI()` zamiast `t.Commit()`
  ([`worker-extender.md`](../../worker-extender.md)).
- Po `Save()` w środku jednej sesji zamyka się okno edycji; kolejna edycja na **tym samym** obiekcie
  bez ponownego `Logout` rzuci `AccessWriteDenied`. Wzorzec: zapis → odczyt na świeżej sesji.
- Nie ustawiaj `Stan = Zablokowany` z dodatku — to stan wewnętrzny platformy (np. po zaksięgowaniu w
  ewidencji).

---

### HANDEL-W13 — Cofnięcie do bufora / odtwierdzenie

**Cel:** wycofać zatwierdzony dokument z powrotem do bufora, aby go poprawić. Operacja odksięgowuje
to, co zatwierdzenie zaksięgowało (obroty, płatności), więc jest dozwolona **tylko** gdy nie ma
zależności blokujących (zamknięty okres magazynowy/VAT, zaksięgowanie w ewidencji, dokumenty
podrzędne).

**Warianty:**

| Wariant | Operacja | Warunek dozwolenia |
|---|---|---|
| Cofnięcie do bufora | `dok.Stan = StanDokumentuHandlowego.Bufor` | okres otwarty, brak podrzędnych, nie zaksięgowany |
| Dokument zablokowany | najpierw zdjąć blokadę po stronie ewidencji/księgowości | `dok.Stan == Zablokowany` blokuje cofnięcie |
| Z dokumentami podrzędnymi | najpierw usuń/rozłącz podrzędne (relacje) | patrz rozdział o relacjach i HANDEL-W16 |

**Pola i typy:** `dok.Stan: StanDokumentuHandlowego`, `dok.Zatwierdzony/Bufor: bool` (kalkulowane),
`dok.DokumentyMagazynowe`, `dok.DokumentyHandlowe`, `dok.DokumentyKorygujące` (kalkulowane — do
sprawdzenia zależności przed cofnięciem).

**Snippet:**

```csharp
var dok = session.GetHandel().DokHandlowe.WgDaty[/* ... */];

if (!dok.Zatwierdzony) return;                  // już w buforze / anulowany — nic do zrobienia

// Cofnięcie jest zablokowane, gdy istnieją dokumenty podrzędne (korekty, magazynowe):
bool maZaleznosci = dok.DokumentyKorygujące.Any() || dok.DokumentyMagazynowe.Length > 0;
if (maZaleznosci)
    throw new BusException(
        "Nie można cofnąć dokumentu do bufora — istnieją powiązane dokumenty.".Translate());

using (var t = session.Logout(editMode: true))
{
    dok.Stan = StanDokumentuHandlowego.Bufor;   // odtwierdzenie: zatwierdzony -> bufor
    t.Commit();                                  // CommitUI() w workerze/extenderze
}
session.Save();   // tu odksięgowanie obrotów/płatności i wykrycie konfliktów
```

**Pułapki:**

- Cofnięcie dokumentu w **zamkniętym okresie** magazynowym/VAT albo zaksięgowanego w ewidencji
  zakończy się wyjątkiem w `Commit()`/`Save()`. Sprawdź stan otwarcia okresu zanim spróbujesz.
- Dokument w stanie `Zablokowany` nie cofniesz przez `dok.Stan = Bufor` — blokada wynika z innego
  modułu (np. ewidencja zaksięgowana). Do diagnozy/naprawy rozbieżności stanu dokument↔ewidencja służy
  `PoprawaStanuDokumentuWorker` (HANDEL-W15).
- Jeśli istnieją dokumenty podrzędne (korekty, powiązane magazynowe), cofnięcie się nie powiedzie —
  najpierw rozwiąż powiązania (rozdział o relacjach), patrz też HANDEL-W16.
- To **nie** to samo co anulowanie (HANDEL-W14): cofnięcie wraca do edytowalnego bufora, anulowanie zamyka
  dokument w stanie nieodwracalnym.

---

### HANDEL-W14 — Anulowanie dokumentów

**Cel:** unieważnić dokument, który nie powinien już brać udziału w obrocie (np. wystawiony omyłkowo),
zachowując go w bazie dla ciągłości numeracji i audytu. Anulowanie odksięgowuje skutki magazynowe i
finansowe, ale rekord pozostaje (w przeciwieństwie do `Delete()`).

**Warianty:**

| Wariant | Operacja | Uwaga |
|---|---|---|
| Anulowanie z bufora | `dok.Stan = StanDokumentuHandlowego.Anulowany` | bufor → anulowany |
| Anulowanie zatwierdzonego | `dok.Stan = StanDokumentuHandlowego.Anulowany` | odksięgowuje obroty/płatności; tylko gdy okres otwarty |
| Sprawdzenie | `dok.Anulowany` (kalkulowane `bool`) | bez porównywania enuma |

**Pola i typy:** `dok.Stan: StanDokumentuHandlowego`, `dok.Anulowany: bool` (kalkulowane).

**Snippet:**

```csharp
var dok = session.GetHandel().DokHandlowe.WgDaty[/* ... */];

if (dok.Anulowany) return;                      // już anulowany

using (var t = session.Logout(editMode: true))
{
    dok.Stan = StanDokumentuHandlowego.Anulowany;   // bufor lub zatwierdzony -> anulowany
    t.Commit();                                      // CommitUI() w workerze/extenderze
}
session.Save();

// Po anulowaniu dokument pozostaje w bazie (numeracja zachowana), ale nie wpływa na stany:
bool wycofany = dok.Anulowany;
```

**Pułapki:**

- Anulowanie zatwierdzonego dokumentu odksięgowuje jego skutki — w **zamkniętym okresie** albo gdy
  istnieją dokumenty podrzędne kończy się wyjątkiem. Najpierw rozwiąż zależności (jak w HANDEL-W13).
- Anulowanie jest **nieodwracalne** — nie ma przejścia `Anulowany → Bufor` na poziomie pola `Stan`.
  Gdy chcesz tylko poprawić dokument, użyj cofnięcia do bufora (HANDEL-W13).
- Anulowany dokument zwykle nie powinien być źródłem relacji ani korekt — generowanie podrzędnych z
  anulowanego nadrzędnego zostanie odrzucone.
- Do trwałego usunięcia rekordu (gdy dozwolone) służy `Delete()` (HANDEL-W16), a nie anulowanie —
  anulowanie zachowuje rekord i numer.

---

### HANDEL-W15 — Naprawa i przeliczenie stanu dokumentu

**Cel:** naprawić rozbieżności między dokumentem a jego skutkami: stan dokumentu vs stan dokumentu
ewidencji (`PoprawaStanuDokumentuWorker`) oraz zgodność obrotów/zasobów magazynowych z pozycjami
(`PrzeliczenieStanuWorker`). To operacje serwisowe — uruchamiaj świadomie, nie w pętli zwykłej
logiki.

**Warianty:**

| Wariant | Worker (publiczny) | Akcja menu / wejście |
|---|---|---|
| Naprawa stanu dokumentu (synchron. z ewidencją) | `Soneta.Handel.PoprawaStanuDokumentuWorker` | „Narzędziowe/Naprawa stanu dokumentu"; `[Context] Dokument` |
| Sprawdzenie poprawności obrotów (bez zapisu) | `Soneta.Magazyny.PrzeliczenieStanuWorker`, `Opcje.SprawdzićPoprawność` | „Narzędziowe/Naliczenie obrotów towaru" |
| Ponowne pełne przeliczenie | `PrzeliczenieStanuWorker`, `Opcje.PonowniePrzeliczyć` | jw. (zapis w transakcji) |
| Poprawa tylko błędnych | `PrzeliczenieStanuWorker`, `Opcje.PoprawićTylkoBłędne` | jw. |
| Poprawa / sprawdzenie samych obrotów | `Opcje.PoprawićObroty` / `Opcje.SprawdzićObroty` | jw. |

**Pola i typy (publiczny kontrakt workerów):**

- `PoprawaStanuDokumentuWorker`: property `[Context] public DokumentHandlowy Dokument`; akcja
  `public void NaprawStan()`; predykat widoczności
  `public static bool IsVisibleNaprawStan(DokumentHandlowy dokument)`. Worker sam zarządza
  transakcją wewnątrz `NaprawStan()` (synchronizuje `dok.Stan` z dokumentem ewidencji, w razie potrzeby
  tworzy/kasuje ewidencję, może przestawić `Stan` na `Zablokowany`/`Zatwierdzony`).
- `PrzeliczenieStanuWorker`: enum `public enum Opcje { SprawdzićPoprawność, PoprawićTylkoBłędne,
  PrzeliczyćTylkoNiepoprawione, PonowniePrzeliczyć, PoprawićObroty, SprawdzićObroty }`; konstruktor
  publiczny `PrzeliczenieStanuWorker(Opcje wykonaj, bool wszystkieMagazyny, bool rozchód0, bool
  przywracajWartość)`; property `[Context]` `Dokument`, `Towar`, `Magazyny` (`Magazyn[]`); akcja
  `public void PrzeliczStan()`. Worker sam otwiera transakcje wewnątrz `PrzeliczStan()`.

**Snippet:**

```csharp
var dok = session.GetHandel().DokHandlowe.WgDaty[/* ... */];

// 1. Naprawa rozbieżności stanu dokumentu względem dokumentu ewidencji.
//    Worker sam prowadzi transakcje — ustaw tylko kontekst i wywołaj akcję.
var naprawa = new PoprawaStanuDokumentuWorker { Dokument = dok };
naprawa.NaprawStan();
session.Save();   // utrwalenie zmian dokonanych przez workera

// 2. Sprawdzenie poprawności obrotów dokumentu BEZ wprowadzania zmian (tryb diagnostyczny):
var sprawdz = new PrzeliczenieStanuWorker(
    PrzeliczenieStanuWorker.Opcje.SprawdzićPoprawność,
    wszystkieMagazyny: false, rozchód0: false, przywracajWartość: true) { Dokument = dok };
sprawdz.PrzeliczStan();   // tryb SprawdzićPoprawność nie commituje — tylko raportuje (Trace)

// 3. Pełne ponowne przeliczenie obrotów dokumentu (modyfikuje dane):
var przelicz = new PrzeliczenieStanuWorker(
    PrzeliczenieStanuWorker.Opcje.PoprawićTylkoBłędne,
    wszystkieMagazyny: false, rozchód0: false, przywracajWartość: true) { Dokument = dok };
przelicz.PrzeliczStan();
session.Save();
```

**Pułapki:**

- Oba workery **same zarządzają transakcjami** wewnątrz swoich akcji (`NaprawStan`/`PrzeliczStan`).
  Nie owijaj wywołania własnym `session.Logout(true)` — wystarczy `session.Save()` po akcji, by
  utrwalić zmiany.
- W realnej aplikacji akcje są rejestrowane z `Mode = ActionMode.IsolatedSession | Progress`, czyli
  uruchamiają się w **izolowanej sesji**. Przy programowym wywołaniu działasz na bieżącej sesji —
  upewnij się, że nie koliduje to z innymi otwartymi transakcjami.
- `Opcje.SprawdzićPoprawność` to tryb **tylko diagnostyczny** — nie zmienia danych, raportuje przez
  `Trace`. Do faktycznej naprawy użyj `PoprawićTylkoBłędne`/`PonowniePrzeliczyć`.
- `PrzeliczenieStanuWorker` rzuca `RowException`, gdy napotka obrót w **zamkniętym okresie**
  magazynowym albo dokument korygowany w buforze („Dokument korygowany … w buforze. Należy go
  zatwierdzić.") — obsłuż te przypadki, nie wywołuj przeliczenia na ślepo.
- `PoprawaStanuDokumentuWorker.IsVisibleNaprawStan` zwraca `false` dla dokumentów z obsługą
  technologii produkcji i magazynu pozabilansowego — to sygnał, że dla takich dokumentów naprawa nie
  ma zastosowania.
- To są narzędzia serwisowe — nie używaj ich jako rutynowego elementu logiki tworzenia dokumentów.

---

### HANDEL-W16 — Bezpieczne usunięcie dokumentu z bufora i obsługa zależności

**Cel:** trwale usunąć dokument z bazy (`Delete()`), gdy jest błędny i jeszcze niepowiązany. Usuwanie
jest dozwolone **wyłącznie w buforze** i tylko gdy nie istnieją zależności (rezerwacje, dokumenty
magazynowe/handlowe powiązane, korekty). W przeciwnym razie świadomie odmów (lub anuluj — HANDEL-W14).

**Warianty:**

| Wariant | Sytuacja | Zalecenie |
|---|---|---|
| Usunięcie czyste | bufor, brak powiązań i rezerwacji | dozwolone (`dok.Delete()`) |
| Dokument zatwierdzony | poza buforem | najpierw cofnij do bufora (HANDEL-W13) lub anuluj (HANDEL-W14) |
| Z rezerwacją | `dok.Rezerwacja != null` | usuń/zwolnij rezerwację najpierw (relacje) |
| Z dokumentami powiązanymi | `DokumentyMagazynowe`/`DokumentyHandlowe`/korekty niepuste | rozłącz/usuń podrzędne lub anuluj |

**Pola i typy (do oceny zależności — kalkulowane, tylko odczyt):** `dok.Bufor: bool`,
`dok.Rezerwacja`, `dok.DokumentyMagazynowe: DokumentHandlowy[]`, `dok.DokumentyHandlowe:
DokumentHandlowy[]`, `dok.DokumentyKorygujące: IEnumerable`, `dok.DokumentKorygowany`,
`dok.DokumentyZaliczkowe`.

**Snippet:**

```csharp
var dok = session.GetHandel().DokHandlowe.WgDaty[/* ... */];

// 1. Usuwać można tylko z bufora:
if (!dok.Bufor)
    throw new BusException(
        "Usunąć można tylko dokument w buforze. Cofnij do bufora lub anuluj.".Translate());

// 2. Zależności blokujące usunięcie (rezerwacja, powiązane, korekty):
bool maZaleznosci =
    dok.Rezerwacja != null ||
    dok.DokumentyMagazynowe.Length > 0 ||
    dok.DokumentyHandlowe.Length > 0 ||
    dok.DokumentyKorygujące.Any();

if (maZaleznosci)
    throw new BusException(
        "Nie można usunąć dokumentu — istnieją powiązania (rezerwacja/dokumenty/korekty).".Translate());

using (var t = session.Logout(editMode: true))
{
    dok.Delete();        // twarde usunięcie — tylko gdy bufor i brak zależności
    t.Commit();          // CommitUI() w workerze/extenderze
}
session.Save();          // integralność weryfikowana także tutaj
```

**Pułapki:**

- Sprawdzaj zależności **przed** `Delete()`. Próba usunięcia powiązanego dokumentu i tak zostanie
  odrzucona przez integralność (wyjątek w `Save()`), ale lepiej zdecydować świadomie i zwrócić czytelny
  komunikat.
- Usunięcie usuwa też **pozycje** dokumentu — wykonuj je jedną transakcją; nie kasuj pozycji „ręcznie"
  przed `dok.Delete()`, jeśli i tak usuwasz cały dokument.
- Gdy dokument jest **zatwierdzony**, najpierw cofnij go do bufora (HANDEL-W13). Jeśli cofnięcie jest
  zablokowane (okres zamknięty, podrzędne), rozważ **anulowanie** (HANDEL-W14) zamiast usuwania — anulowanie
  zachowuje numer i ścieżkę audytu.
- Rezerwacje rozwiązuje logika relacji/magazynu (workery rezerwacji są **internal** — z dodatku
  operuj przez publiczne API relacji oraz pola `dok.Rezerwacja`), nie kasuj rekordów rezerwacji
  bezpośrednio z dodatku.
- `Delete()` na dokumencie poza buforem (zatwierdzony/zablokowany/anulowany) jest zabronione — nie
  obchodź tego przez bezpośrednie operacje na tabeli.

---

