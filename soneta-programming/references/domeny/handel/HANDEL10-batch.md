# HANDEL10 — Operacje zbiorcze (batch)

> Wspólne fakty o typie, podstawowe typy i szablon wzorca: [../handel.md](../handel.md).

Operacje na zbiorze dokumentów (ewidencjonowanie do księgowości, hurtowe zatwierdzanie,
generowanie dokumentów podrzędnych) wykonujemy efektywnie i bezpiecznie: filtr **serwerowy**
zamiast pełnego skanu tabeli, **krótkie transakcje** (paczki), świadoma obsługa **blokady
optymistycznej** w `Save()`. Tabela `DokHandlowe` jest operacyjna (guided) — pełny skan bez
zakresu czasowego jest zabroniony (`safe-code.md` §6.3). Duże pętle dziel na paczki, by nie
trzymać długiej transakcji edycyjnej (§13.1).

### HANDEL-W53 — Ewidencjonowanie / eksport do księgowości wielu dokumentów

**Cel:** zbiorczo zaewidencjonować (zaksięgować do ewidencji księgowej) wiele dokumentów
handlowych z danego okresu — np. raport fiskalny zbiorczy z paragonów lub korekt paragonów.
Realizuje to publiczny worker `EwidencjonowanieZbiorczeWorker`, który sam grupuje dokumenty
(po drukarce / oddziale / rodzaju podmiotu) i tworzy zbiorcze dokumenty ewidencji `DokEwidencji`.

**Warianty:**

| Wariant | Ustawienie `Params` |
|---|---|
| Raport fiskalny z paragonów | `RaportDla = RaportDla.Paragonów` |
| Raport dla korekt paragonów | `RaportDla = RaportDla.KorektParagonów` |
| Zawężenie do jednej drukarki | `SymbolKasy = "D1"` (puste = wszystkie z niepustym symbolem kasy) |
| Wskazanie definicji ewidencji | `Definicja` (typ `SprzedażZbiorczaEwidencja`) — gdy chcemy inną niż domyślna |
| Filtr po dacie wystawienia | `ZaOkres: FromTo` |
| Filtr po dacie dostawy / zaliczki | `OkresDostawyZaliczki: FromTo` |
| Wielooddziałowość | `Oddzial: OddzialFirmy` (gdy włączona w konfiguracji) |

**Pola i typy:**
- Worker: `Soneta.Handel.EwidencjonowanieZbiorczeWorker` (**public**), metoda publiczna
  `void Ewidencjonuj()`, property `[Context] Params Param`.
- `EwidencjonowanieZbiorczeWorker.Params(Context cx)` — konstruktor z `Context`. Pola:
  `ZaOkres: FromTo`, `OkresDostawyZaliczki: FromTo`, `RaportDla: RaportDla`,
  `SymbolKasy: string`, `Definicja: Soneta.Core.DefinicjaDokumentu`, `Oddzial: OddzialFirmy`.
- `EwidencjonowanieZbiorczeWorker.RaportDla` (enum): `Paragonów`, `KorektParagonów`.
- Worker przetwarza tylko dokumenty w stanie `Zatwierdzony` / `Zablokowany`; pomija już
  zaewidencjonowane (`EwidencjaZbiorcza != null`).

**Snippet:**

```csharp
// Worker SAM otwiera transakcję edycyjną i robi CommitUI() w środku — NIE owijaj go
// w session.Logout(true). Wystarczy go skonfigurować, wywołać i zapisać.
var worker = new EwidencjonowanieZbiorczeWorker
{
    Param = new EwidencjonowanieZbiorczeWorker.Params(context)
    {
        RaportDla = EwidencjonowanieZbiorczeWorker.RaportDla.Paragonów,
        ZaOkres = new FromTo(new Date(2026, 6, 1), new Date(2026, 6, 30)),  // data wystawienia
        OkresDostawyZaliczki = FromTo.All,                                  // bez filtra dostawy
        SymbolKasy = "D1",                                                  // jedna drukarka
        Definicja = CoreModule.GetInstance(session).DefDokumentow.WgSymbolu["SPZE"],
    }
};

worker.Ewidencjonuj();   // tworzy zbiorcze DokEwidencji w transakcji wewnętrznej (CommitUI)
session.Save();          // dopiero teraz zapis do bazy — tu wykrywane konflikty optymistyczne
```

**Pułapki:**
- `Ewidencjonuj()` **samodzielnie** otwiera `Session.Logout(true)` i kończy `CommitUI()`. Nie
  wywołuj go we własnej transakcji edycyjnej (zagnieżdżenie/podwójny commit). Po nim wykonaj
  `session.Save()` (w testach `SaveDispose()`).
- `Param` ustaw **przed** `Ewidencjonuj()` — jest to property `[Context]`; bez niej worker
  rzuci `NullReferenceException`.
- `Date` i `FromTo` to typy biznesowe — używaj `Date`/`Date.Today`, nie `DateTime`
  (`safe-code.md` §10). `FromTo.All` = bez ograniczenia, `FromTo.Empty` worker zamienia na `All`.
- `Definicja` to rekord konfiguracyjny — pobierz istniejący (`DefDokumentow.WgTypu[...]` /
  `WgSymbolu[...]`), nie twórz „w locie". Gdy `Definicja == null`, worker użyje domyślnej.
- Worker działa na danych z `ZaOkres` (data wystawienia) — zawsze podaj zakres, nie zostawiaj
  pełnego skanu całej historii.
- Konflikt edycji (ktoś zapisał ten sam dokument) wybuchnie w `session.Save()` jako
  `RowConflictException` — obsłuż go (refresh + retry lub eskalacja), nie połykaj (§4).

### HANDEL-W54 — Hurtowe zatwierdzanie / generowanie dokumentów dla zaznaczonego zbioru

**Cel:** wykonać operację cyklu życia (zatwierdzenie, cofnięcie do bufora, anulowanie) na
**wielu** dokumentach naraz, albo wygenerować dla zaznaczonego zbioru dokumenty podrzędne
(np. wiele zamówień → faktury, wiele faktur → jeden zbiorczy WZ) za pomocą `IRelacjeService`,
który przyjmuje **tablicę** dokumentów.

**Warianty:**

| Wariant | Mechanizm |
|---|---|
| Hurtowe zatwierdzanie | pętla po zbiorze, `dok.Stan = StanDokumentuHandlowego.Zatwierdzony`, jedna (krótka) transakcja |
| Hurtowe cofnięcie do bufora / anulowanie | `dok.Stan = StanDokumentuHandlowego.Bufor` / `.Anulowany` |
| Indywidualne generowanie podrzędnych | `IRelacjeService.NowyPodrzednyIndywidualny(DokumentHandlowy[], symbol)` — N nadrzędnych → N podrzędnych |
| Zbiorcze generowanie podrzędnego | `IRelacjeService.NowyPodrzednyZbiorczy(DokumentHandlowy[], symbol)` — wiele FA → 1 WZ |
| Zbiorcza korekta | `IRelacjeService.NowaKorektaZbiorcza(DokumentHandlowy[])` |
| Dołączenie nadrzędnego / podrzędnego | `DolaczNadrzedny`, `DolaczPodrzednyIndywidualny` |

**Pola i typy:**
- `dok.Stan: Soneta.Handel.StanDokumentuHandlowego` (`Bufor=0`, `Zatwierdzony=1`,
  `Zablokowany=2`, `Anulowany=3`). Skróty read-only: `dok.Bufor`, `dok.Zatwierdzony`,
  `dok.Anulowany`.
- `IRelacjeService` (namespace `Soneta.Handel.RelacjeDokumentow.Api`): metody przyjmują
  `DokumentHandlowy[]` i zwracają `DokumentHandlowy[]`. Dokumenty nadrzędne muszą być
  **zatwierdzone**. Dostęp: `session.GetRequiredService<IRelacjeService>()`
  (`using Microsoft.Extensions.DependencyInjection;`).

**Snippet:**

```csharp
var hm = session.GetHandel();
var fv = hm.DefDokHandlowych.WgSymbolu["FV"];
var od = new Date(2026, 6, 1);

// (1) Hurtowe zatwierdzanie zamówień z czerwca — filtr SERWEROWY + krótka transakcja
using (var t = session.Logout(editMode: true))
{
    foreach (DokumentHandlowy d in hm.DokHandlowe[(DokumentHandlowy d) =>
                 d.Data >= od && d.Definicja == fv && d.Stan == StanDokumentuHandlowego.Bufor])
    {
        d.Stan = StanDokumentuHandlowego.Zatwierdzony;   // pętla po Stan na zaznaczonym zbiorze
    }
    t.Commit();   // CommitUI() w workerze/extenderze
}
session.Save();

// (2) Wygenerowanie faktur dla zaznaczonych (zatwierdzonych) zamówień — IRelacjeService na tablicy
var rel = session.GetRequiredService<IRelacjeService>();
DokumentHandlowy[] zamowienia = /* zaznaczone, zatwierdzone ZO */;
using (var t = session.Logout(editMode: true))
{
    DokumentHandlowy[] faktury = rel.NowyPodrzednyIndywidualny(zamowienia, "FV");
    t.Commit();
}
session.Save();
```

**Pułapki:**
- `IRelacjeService` wymaga, by dokumenty nadrzędne były **zatwierdzone** — najpierw zatwierdź
  (wariant 1), potem generuj podrzędne.
- Operacje masowe wykonuj w jednej transakcji **tylko gdy zbiór jest mały**; dla dużych dziel na
  paczki (HANDEL-W55) — długa transakcja blokuje innych i zwiększa ryzyko konfliktu (§13.1).
- Zmiana `Stan` musi być w transakcji (`session.Logout(true)`); w workerze/extenderze
  `t.CommitUI()` zamiast `t.Commit()`.
- Nie iteruj całej tabeli `DokHandlowe` z `if` w pamięci — filtr serwerowy z zakresem czasowym
  (§6.1, §6.3). Zaznaczony w UI zbiór masz w `context` jako `DokumentHandlowy[]`.
- `Save()` po operacji relacji może rzucić `RowConflictException` (optimistic lock) — obsłuż (§4).

### HANDEL-W55 — Wydajne przetwarzanie wielu dokumentów w jednej sesji (paczki)

**Cel:** przetworzyć duży zbiór dokumentów (tysiące) w jednej sesji bez blokowania innych
użytkowników i bez ryzyka, że pojedynczy konflikt unieważni całą operację — przez podział na
**paczki** (krótkie transakcje, okresowy `Save()`).

**Warianty:**

| Wariant | Technika |
|---|---|
| Filtr serwerowy z zakresem czasowym | `hm.DokHandlowe[(DokumentHandlowy d) => d.Data >= od && d.Data <= doD && …]` |
| Paczki o stałym rozmiarze | licznik w pętli + `Commit()` / `Save()` co N rekordów |
| Izolacja konfliktu paczki | `try/catch (RowConflictException)` wokół `Save()` paczki, retry/log paczki |
| Tylko odczyt (raport) | `login.CreateSession(readOnly: true, …)` — bez transakcji edycyjnej |

**Pola i typy:** `Soneta.Types.Date` (zakres), `StanDokumentuHandlowego`, `RowConflictException`
(`session.Save()`), `IDisposable` na sesji i transakcji.

**Snippet:**

```csharp
const int rozmiarPaczki = 200;       // przetwarzaj po 200 dokumentów na transakcję
var hm = session.GetHandel();
var od  = new Date(2026, 1, 1);
var doD = Date.Today;

// Materializujemy KLUCZE/ID po stronie serwera (filtr), nie całe rekordy w pamięci wszystkie naraz.
// Iterujemy serwerowy zbiór i commitujemy paczkami — krótka transakcja na każdą paczkę.
int licznik = 0;
ITransaction t = session.Logout(editMode: true);
try
{
    foreach (DokumentHandlowy d in hm.DokHandlowe[(DokumentHandlowy d) =>
                 d.Data >= od && d.Data <= doD && d.Stan == StanDokumentuHandlowego.Bufor])
    {
        d.Stan = StanDokumentuHandlowego.Zatwierdzony;

        if (++licznik % rozmiarPaczki == 0)
        {
            t.Commit();
            t.Dispose();
            session.Save();                  // zamknięcie paczki — krótka transakcja
            t = session.Logout(editMode: true);
        }
    }
    t.Commit();
}
finally
{
    t.Dispose();
}
session.Save();                              // ostatnia (niepełna) paczka
```

**Pułapki:**
- **Krótka transakcja** to bezpieczeństwo, nie tylko wydajność — operacja > ~30 s powinna iść
  paczkami (§13.1). Jedna gigantyczna transakcja blokuje innych i zwiększa szansę konfliktu.
- Filtruj **serwerowo** (`SubTable[condition]`), z zakresem czasowym dla tabeli operacyjnej
  guided (`DokHandlowe`) — nigdy pełny skan (§6.1, §6.3). Nie używaj `.ToList().Where(...)`
  (§13.2).
- Po `session.Save()` w środku pętli okno edycji jest zamknięte — kolejną edycję otwórz **nową**
  transakcją (`session.Logout(true)`), inaczej `AccessWriteDenied`. (W testach wzorzec to
  `Save()` → `SaveDispose()` → odczyt na świeżej sesji po `Guid`.)
- Obsłuż `RowConflictException` per paczka (refresh + retry lub log i kontynuacja), nie łap
  `Exception` ogólnie (§4, §9.1). Połknięty wyjątek z `Save()` = utrata danych.
- Nie współdziel `Session`/`Row` między wątkami — równoległe przetwarzanie wymaga osobnej sesji
  na wątek (§3.1).
- Sesja zawsze w `using`/`try-finally` z `Dispose()` (§1.1); transakcja bez `Commit()` =
  automatyczny rollback.

---

> Powiązane: rozdz. 5 (cykl życia / `Stan`), rozdz. 8 (relacje, `IRelacjeService`),
> `safe-code.md` §4 (optimistic lock), §6 (filtr serwerowy), §13 (paczki),
> `rowcondition.md` (serwerowy LINQ).

---

