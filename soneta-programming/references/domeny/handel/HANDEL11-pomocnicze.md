# HANDEL11 — Operacje pomocnicze (przekrojowe)

> Wspólne fakty o typie, podstawowe typy i szablon wzorca: [../handel.md](../handel.md).

Rozdział zbiera wzorce „okołodokumentowe": bezpieczne pozyskanie kontrahenta i towaru do pozycji,
przeliczanie jednostek, walidację przed zatwierdzeniem, obsługę błędów i blokady optymistycznej,
odczyt metadanych (`ChangeInfos`) oraz pracę z definicjami i numeracją dokumentu. Fundamenty (sesja,
transakcja, `Save`, blokada optymistyczna) opisuje [`safe-code.md`](../../safe-code.md) i
[`session-login.md`](../../session-login.md) — tutaj się do nich odwołujemy.

> Cały kod jest zgodny z C# 10 (target-typed `new`, `var`, file-scoped namespace, wyrażenia `switch`,
> nazwane parametry `bool`) i operuje **wyłącznie na publicznym kontrakcie** platformy.

---

### HANDEL-W56 — Bezpieczne pobranie / utworzenie kontrahenta i towaru pozycji

**Cel:** przed dodaniem pozycji lub ustawieniem nabywcy bezpiecznie zlokalizować istniejący rekord
(kontrahent, towar), a gdy go brak — świadomie utworzyć nowy albo użyć kontrahenta jednorazowego
(systemowego rekordu „incydentalnego"). Chroni przed `NullReferenceException` w trakcie transakcji.

**Warianty:**

| Wariant | Mechanizm | Uwaga |
|---|---|---|
| Kontrahent po kodzie | `crm.Kontrahenci.WgKodu["Abc"]` | klucz unikalny, może być `null` |
| Kontrahent po NIP (dedup) | `crm.Kontrahenci.WgNIP[(Kontrahent k)=>k.NIP==nip]` | filtr serwerowy, normalizuj `Nip.Flat` |
| Kontrahent jednorazowy / incydentalny | `Kontrahent.INCYDENTALNY` (stała `Guid`), `k.JestIncydentalny` | rekord systemowy — dane nabywcy zapisz na dokumencie |
| Utworzenie nowego kontrahenta | `new Kontrahent()` + `AddRow` | patrz CRM-W3 w `crm.md` |
| Towar po kodzie | `tm.Towary.WgKodu["BIKINI"]` | klucz unikalny, może być `null` |
| Brak towaru | przerwij operację (`BusException`) | nie twórz towaru „w locie" w trakcie wystawiania |

**Pola i typy:** `crm.Kontrahenci.WgKodu: GuidedTable` (indeks po `Kod`), `Kontrahent.JestIncydentalny:
bool` (kalkulowane), `Kontrahent.INCYDENTALNY: System.Guid` (stała), `tm.Towary.WgKodu` (indeks po
`Kod`), `dok.Kontrahent: Kontrahent`. Dostęp do kontrahenta incydentalnego po `Guid`:
`crm.Kontrahenci[Kontrahent.INCYDENTALNY]` (indeksator `GuidedTable` po `Guid`).

**Snippet:**

```csharp
var crm = session.GetCRM();
var tm  = session.GetTowary();

// 1. Kontrahent po kodzie — może nie istnieć
Kontrahent kontrahent = crm.Kontrahenci.WgKodu["Abc"];

// 2. Gdy brak po kodzie — dedup po NIP, zanim ewentualnie utworzymy nowego
if (kontrahent == null && !string.IsNullOrEmpty(nip))
{
    var flat = Nip.Flat(nip);                         // normalizacja przed porównaniem
    kontrahent = crm.Kontrahenci.WgNIP[(Kontrahent k) => k.NIP == flat].FirstOrDefault();
}

// 3. Sprzedaż jednorazowa (klient detaliczny bez kartoteki) — kontrahent incydentalny
if (kontrahent == null)
    kontrahent = crm.Kontrahenci[Kontrahent.INCYDENTALNY];   // systemowy rekord „incydentalny"

// 4. Towar pozycji — gdy brak, przerywamy świadomie (nie wystawiamy „pustej" pozycji)
Towar towar = tm.Towary.WgKodu["BIKINI"];
if (towar == null)
    throw new BusException("Brak towaru o kodzie BIKINI.".Translate());

using (var t = session.Logout(editMode: true))
{
    dok.Kontrahent = kontrahent;                      // gdy definicja wymaga nabywcy
    t.Commit();                                       // CommitUI() w workerze/extenderze
}
session.Save();
```

**Pułapki:**
- `WgKodu[...]` zwraca **jeden** rekord lub `null` (klucz unikalny). `WgNIP[condition]` /
  `WgNazwy[...]` zwracają **zbiór** — użyj `.FirstOrDefault()`. Nie iteruj całej tabeli `Kontrahenci`
  / `Towary` w pamięci — to kartoteki; filtruj serwerowo (`SubTable[condition]`, `safe-code.md` §6).
- **Kontrahenta incydentalnego nie wolno ustawić na każdym typie dokumentu** — na fakturze sprzedaży
  (np. `FV`) przypisanie `dok.Kontrahent = crm.Kontrahenci[Kontrahent.INCYDENTALNY]` rzuca
  `ArgumentException` („Nie można ustawiać kontrahenta incydentalnego w dokumentach typu 'FV'"). Rekord
  incydentalny jest przeznaczony do sprzedaży detalicznej (np. paragon) — na fakturze podaj realnego nabywcę.
- Kontrahenta jednorazowego pobieraj jako rekord **incydentalny** (`Kontrahent.INCYDENTALNY`) — nie
  twórz za każdym razem nowego rekordu w kartotece. Rekordu incydentalnego nie modyfikuj
  (`JestIncydentalny == true`); dane konkretnego nabywcy (nazwa, NIP, adres) zapisz na samym
  dokumencie / w jego polach adresowych, nie na rekordzie kontrahenta.
- Brak towaru przy zwykłym wystawianiu faktury to **błąd danych** — nie uzupełniaj go po cichu.
  Ale technicznie **kontrahent i towar to dane KARTOTEKOWE (operacyjne), nie konfiguracyjne** —
  gdy scenariusz tego wymaga (np. import, kreator), możesz utworzyć NOWY towar
  (`Session.AddRow(new Towar { … })`), NOWEGO kontrahenta (`new Kontrahent { … }`) i dokument
  używający ich **w jednej transakcji edycyjnej tej samej sesji operacyjnej** — to wykonalne
  i wspierane (zweryfikowane HANDEL-W56). Towar minimalnie potrzebuje `Kod` + `Nazwa` (jednostka
  domyślna — HANDEL-W57); nie wymaga osobnej sesji konfiguracyjnej.
- W `RowCondition` używaj tylko pól bazodanowych. `JestIncydentalny`, `NazwaFormatowana` itp. są
  kalkulowane → w wyrażeniu LINQ rzucą `LinqConditionException`.

---

### HANDEL-W57 — Przeliczanie jednostek miary towaru przy dodawaniu pozycji

**Cel:** dodać pozycję w jednostce pomocniczej (np. opakowanie zbiorcze, „km", „kg") i poprawnie
przeliczyć ją na jednostkę podstawową towaru, korzystając z przeliczników zdefiniowanych dla towaru.

**Warianty:**

| Wariant | Mechanizm | Uwaga |
|---|---|---|
| Pozycja w jednostce podstawowej | `poz.Ilosc = new Quantity(n, poz.Ilosc.Symbol)` | symbol z pozycji po ustawieniu `Towar` |
| Pozycja w jednostce pomocniczej | `new Quantity(n, "OPAK")` | symbol jednostki pomocniczej |
| Jawne przeliczenie ilości | `towar.PrzeliczJednostkę(jednostka, qty, throwError)` | zwraca `Quantity` w jednostce docelowej |
| Jednostka podstawowa towaru | `towar.Jednostka: Jednostka` | jednostka, w której prowadzony jest magazyn |
| Jednostka uzupełniająca (Intrastat/CN) | `towar.JednostkaUzupelniajaca: Jednostka` | wymaga zdefiniowanego przelicznika |
| Brak przelicznika | `throwError: true` → wyjątek | brak przelicznika = niejednoznaczne przeliczenie |

**Pola i typy:** `Towar.Jednostka: Soneta.Handel.Jednostka`, `Towar.JednostkaUzupelniajaca:
Jednostka`, `Towar.PrzeliczJednostkę(Jednostka jednostka, Quantity qty, bool throwError): Quantity`,
`tm.Jednostki` (tabela jednostek, indeks `WgKodu`). `Quantity` (`Soneta.Types`) = wartość + symbol
jednostki; `poz.Ilosc.Symbol` po ustawieniu `poz.Towar` przyjmuje symbol jednostki podstawowej.

**Snippet:**

```csharp
var tm = session.GetTowary();
var towar = tm.Towary.WgKodu["TRANSPORT"];           // towar prowadzony np. w „km"

using (var t = session.Logout(editMode: true))
{
    var poz = new PozycjaDokHandlowego(dok);         // ctor wymaga dokumentu
    session.AddRow(poz);
    poz.Towar = towar;                               // USTAW PIERWSZY — inicjuje jednostkę na Ilosc/Cena

    // Wariant A: ilość w jednostce podstawowej towaru (symbol z pozycji)
    poz.Ilosc = new Quantity(10, poz.Ilosc.Symbol);

    // Wariant B: ilość podana w jednostce pomocniczej i przeliczona na podstawową
    var jednPom = tm.Jednostki.WgKodu["OPAK"];       // jednostka pomocnicza
    var iloscPom = new Quantity(3, jednPom.Kod);
    // throwError: true — brak przelicznika OPAK→podstawowa zgłosi wyjątek zamiast cichego błędu
    Quantity iloscPodstawowa = towar.PrzeliczJednostkę(towar.Jednostka, iloscPom, throwError: true);
    poz.Ilosc = iloscPodstawowa;

    t.Commit();
}
session.Save();
```

**Pułapki:**
- `poz.Towar` ustaw **przed** `Ilosc`/`Cena` — to on inicjuje symbol jednostki na pozycji. Konstrukcja
  `new Quantity(n, poz.Ilosc.Symbol)` gwarantuje zgodny symbol; podanie surowego symbolu spoza
  jednostek towaru daje przeliczenie tylko przy istniejącym przeliczniku.
- `PrzeliczJednostkę(..., throwError: true)` rzuci wyjątek, gdy **brak przelicznika** między
  jednostkami — to świadomy wybór: lepszy twardy błąd niż cicha, niepoprawna ilość. Dla `false`
  zwraca ilość bez przeliczenia (ryzykowne).
- `Quantity` to typ wartość+symbol (nie `double`). Nie mieszaj `Quantity` o różnych symbolach w
  arytmetyce — najpierw sprowadź do jednej jednostki przez `PrzeliczJednostkę`.
- `JednostkaUzupelniajaca` (CN/Intrastat) wymaga przelicznika z jednostki podstawowej; jego brak
  zgłaszany jest przy wyliczeniach Intrastat — zdefiniuj przelicznik na towarze.
- Przeliczniki to dane konfiguracyjne towaru — nie twórz ich „w locie" w trakcie wystawiania
  dokumentu; brak przelicznika to sygnał błędu konfiguracji, nie do obejścia w kodzie pozycji.

---

### HANDEL-W58 — Walidacja przed zatwierdzeniem (kompletność, zasób, limit kredytowy)

**Cel:** przed zmianą stanu na `Zatwierdzony` sprawdzić kompletność danych (kontrahent, pozycje),
dostępność zasobu magazynowego oraz przygotować się na automatyczną kontrolę limitu kredytowego
nabywcy. Pozwala zgłosić czytelny błąd zamiast łapać wyjątek głęboko w `Save()`.

**Warianty:**

| Wariant | Sprawdzenie (publiczny kontrakt) | Egzekwowanie |
|---|---|---|
| Kompletność danych | `dok.Kontrahent != null`, `!dok.Pozycje.IsEmpty` | własna walidacja przed `Stan` |
| Dostępność zasobu (stan ujemny) | przyjęcie (PW/PZ) zapisane przed rozchodem | weryfikator Demo `StanUjemnyVerifier` — wyjątek w `Save()` |
| Limit kredytowy nabywcy | `dok.Kontrahent.LimitKredytu`, `KontrolaAktywna`, `TypLimituKredytowego` | platforma kontroluje **automatycznie** przy zatwierdzeniu |
| Termin / forma płatności | `dok.Platnosci` (W z sekcji N) | wynika z definicji i kontrahenta |

**Pola i typy:** `dok.Pozycje: SubTable<PozycjaDokHandlowego>` (`.IsEmpty: bool`), `dok.Kontrahent:
Kontrahent`, `dok.Stan: StanDokumentuHandlowego`. Po stronie kontrahenta (odczyt):
`Kontrahent.LimitKredytu: Currency`, `Kontrahent.TypLimituKredytowego`, `Kontrahent.KontrolaAktywna:
bool` (kalkulowane) — patrz CRM-W9 w `crm.md`.

**Snippet:**

```csharp
// Walidacja PRZED próbą zmiany stanu — czytelny błąd zamiast wyjątku z głębi Save()
if (dok.Kontrahent == null)
    throw new RowException(dok, "Dokument nie ma nabywcy.".Translate());
if (dok.Pozycje.IsEmpty)
    throw new RowException(dok, "Dokument nie ma pozycji.".Translate());

// Informacyjnie: czy nabywca ma aktywną kontrolę kredytową (odczyt pól kalkulowanych)
if (dok.Kontrahent.KontrolaAktywna)
{
    // limit jest egzekwowany automatycznie przy zatwierdzeniu — patrz pułapki
}

using (var t = session.Logout(editMode: true))
{
    dok.Stan = StanDokumentuHandlowego.Zatwierdzony;     // tu uruchamia się kontrola limitu/zasobu
    t.Commit();
}
session.Save();   // brak zasobu (StanUjemnyVerifier) / przekroczony limit → wyjątek właśnie tutaj
```

**Pułapki:**
- **Kontrola limitu kredytowego jest wewnętrzna i automatyczna** — uruchamia się przy zatwierdzaniu
  dokumentu rozchodowego, gdy definicja ma ustawione „zachowanie po przekroczeniu limitu". Z dodatku
  zewnętrznego **nie wywołujesz jej ręcznie** (logika `LimitKredytowyDokumentu` jest `internal`) —
  czytasz pola kontrahenta (`LimitKredytu`, `KontrolaAktywna`) i obsługujesz `InvalidOperationException`
  zgłaszany przez platformę przy zatwierdzaniu.
- W bazie Demo `StanUjemnyVerifier` blokuje rozchód bez wcześniejszego **zapisanego** przyjęcia.
  Samo `CommitUI` nie księguje zasobów — magazyn księguje się dopiero po `Session.Save()`, więc błąd
  pojawia się w `Save()`, nie w transakcji.
- `IsEmpty` na kolekcji `SubTable` to **właściwość** (serwerowy `exists`, bez nawiasów) — nie
  materializuj `Pozycje.ToList().Count`.
- Walidację własną rzucaj jako `RowException(dok, "…".Translate())` **przed** `Commit()`. Wyjątek po
  `Commit()` nie wycofa zmiany z sesji (safe-code §5.1).

---

### HANDEL-W59 — Obsługa błędów i blokada optymistyczna (kolizje `Save`, ponowienie)

**Cel:** poprawnie obsłużyć wyjątki zgłaszane przez `Session.Save()` — w szczególności konflikt
optymistyczny (ktoś inny zapisał ten sam rekord) — zamiast je „połykać"; w razie konfliktu odświeżyć
dane i ponowić operację.

**Warianty:**

| Wariant | Wyjątek | Reakcja |
|---|---|---|
| Konflikt optymistyczny | `Soneta.Business.ConcurrencyException` | świeża sesja → ponów operację (retry) |
| Naruszenie integralności / unikalności | `RowException` (z `InnerException`) | komunikat dla użytkownika, bez retry |
| Walidacja biznesowa | `RowException` / `BusException` | zgłoś użytkownikowi, popraw dane |
| Brak praw / okno edycji zamknięte | `AccessWriteDenied` | edytuj na świeżej, zalogowanej sesji |

**Pola i typy:** `Session.Save()`, `Session.Logout(editMode: true)`, wyjątki z `Soneta.Business`
(`ConcurrencyException` — konflikt optymistyczny; `RowException`, `BusException`, `AccessWriteDenied`).
Po `Save()` w środku operacji okno edycji bywa zamknięte — kolejna edycja na tej samej sesji rzuci
`AccessWriteDenied`.

**Snippet:**

```csharp
// Ponowienie przy konflikcie optymistycznym (retry na świeżych danych)
const int maxProb = 3;
for (int proba = 1; ; proba++)
{
    var dok = session.GetHandel().DokHandlowe[guidDokumentu];   // świeży odczyt po Guid
    try
    {
        using (var t = session.Logout(editMode: true))
        {
            dok.Stan = StanDokumentuHandlowego.Zatwierdzony;
            t.Commit();
        }
        session.Save();
        break;                                                  // sukces
    }
    catch (ConcurrencyException) when (proba < maxProb)
    {
        // ktoś zapisał rekord równolegle — odśwież i spróbuj ponownie
        session = session.Login.CreateSession(readOnly: false, config: false, name: "Retry");
    }
    catch (RowException ex)
    {
        // naruszenie integralności / unikalności / walidacja — bez retry
        throw new BusException($"Nie udało się zapisać dokumentu: {ex.Message}".Translate(), ex);
    }
}
```

**Pułapki:**
- Konflikt optymistyczny ujawnia się **dopiero w `Save()`** (nie w `Commit`) i ma typ
  `Soneta.Business.ConcurrencyException`. Nie połykaj go — albo ponów na świeżych danych, albo
  eskaluj (safe-code §4).
- **Realny konflikt odtworzysz edytując TEN SAM dokument w DWÓCH sesjach** (`Login.CreateSession(
  readOnly: false, config: false, …)` ×2): sesja A edytuje pole (np. `dok.Opis` — **na dokumencie
  buforowym**; na zatwierdzonym `Opis` jest read-only) i `Save()` → bumpuje wersję rekordu; sesja B
  ma już starą wersję i przy `Save()` rzuca `ConcurrencyException`. Działa w domyślnym fixture
  `TestBase` — nie wymaga wyłączania transakcji (zweryfikowane HANDEL-W59).
- Retry rób na **świeżym odczycie** rekordu (po `Guid`) w nowej/odświeżonej sesji — ponowne
  zapisanie tej samej, „starej" instancji odtworzy konflikt.
- Po `Save()` wewnątrz dłuższej operacji okno edycji jest zamknięte → następna edycja na tej samej
  sesji rzuci `AccessWriteDenied`. Wzorzec: zapis → świeża sesja → odczyt po `Guid` → kolejna edycja.
- Nie używaj `catch (Exception)` bez ponownego rzutu — zgubisz informację o przyczynie. Ogranicz
  retry liczbą prób, by nie zapętlić przy trwałym konflikcie.

---

### HANDEL-W60 — Odczyt metadanych dokumentu (`ChangeInfos` — kto/kiedy założył i zmienił)

**Cel:** odczytać informacje audytowe rekordu dokumentu: kto i kiedy go założył oraz kto ostatnio go
zmodyfikował. Dane pochodzą z tabeli `ChangeInfos` i są dostępne przez kalkulowane właściwości
`GuidedRow` (dokument jest `GuidedRow`).

**Warianty:**

| Wariant | Właściwość (kalkulowana) | Zawartość |
|---|---|---|
| Kto/kiedy założył | `dok.FirstChangeInfo: ChangeInfo` | operator i czas utworzenia |
| Kto/kiedy ostatnio zmienił | `dok.LastChangeInfo: ChangeInfo` | operator i czas ostatniej zmiany |
| Pełna historia zmian | `session.GetBusiness().ChangeInfos[dok]` | kolekcja wpisów (`SubTable`) |
| Wyłączenie zapisu historii dla rekordu | `dok.SetChangeInfo(false)` | wyłącza rejestrację `ChangeInfo` dla tego wiersza |

**Pola i typy:** `GuidedRow.FirstChangeInfo: Soneta.Business.ChangeInfo` (Caption „Założył"),
`GuidedRow.LastChangeInfo: ChangeInfo` (Caption „Ostatnia zmiana"). `ChangeInfo` udostępnia m.in.
`Operator` (rekord operatora), `Time`/`Godzina` (czas) oraz `Type: ChangeInfoType`. Kolekcja:
`session.GetBusiness().ChangeInfos[row]`.

**Snippet:**

```csharp
var dok = session.GetHandel().DokHandlowe[guidDokumentu];

// Kto i kiedy założył dokument (najwcześniejszy wpis ChangeInfos)
ChangeInfo zalozyl = dok.FirstChangeInfo;
if (zalozyl != null)
{
    Operator ktoZalozyl = zalozyl.Operator;     // rekord operatora
    // zalozyl.Time / zalozyl.Godzina — czas utworzenia
}

// Kto ostatnio zmodyfikował
ChangeInfo ostatnia = dok.LastChangeInfo;
if (ostatnia != null)
{
    Operator ktoZmienil = ostatnia.Operator;
}

// Pełna historia zmian rekordu
foreach (ChangeInfo ci in session.GetBusiness().ChangeInfos[dok])
{
    // ci.Operator, ci.Time, ci.Type (ChangeInfoType: Added / Modified / Deleted ...)
}
```

**Pułapki:**
- `FirstChangeInfo` / `LastChangeInfo` są **kalkulowane** (zapytania `select top 1 ... from
  ChangeInfos`) — tylko do odczytu, nie ustawiaj. Mogą zwrócić `null`, gdy historia rekordu jest
  pusta (np. import bez rejestracji `ChangeInfo`) — zawsze sprawdź `!= null`.
- Rejestracja `ChangeInfo` zależy od konfiguracji (`ChangeInfoMode` per tabela). Jeśli historia jest
  wyłączona, właściwości mogą być puste — nie zakładaj, że audyt jest zawsze włączony.
- Każdy odczyt `FirstChangeInfo`/`LastChangeInfo` to osobne zapytanie SQL — przy przeglądaniu wielu
  dokumentów nie wywołuj ich w pętli po całej tabeli; ogranicz zakres (safe-code §6).
- Nie loguj danych operatora w sposób ujawniający wrażliwe informacje (safe-code §12).

---

### HANDEL-W61 — Praca z definicjami i numeracją (seria, wymuszenie numeru, bufor `Numer`)

**Cel:** rozpoznać definicję dokumentu i jej schemat numeracji, ustawić/odczytać serię, w razie
potrzeby wymusić konkretny numer, oraz zrozumieć relację między buforem a numerem końcowym
(dokument w buforze ma numer „BUFOR", numer właściwy nadawany jest przy zatwierdzeniu).

**Warianty:**

| Wariant | Mechanizm (publiczny) | Uwaga |
|---|---|---|
| Pobranie definicji | `session.GetHandel().DefDokHandlowych.WgSymbolu["FV"]` | symbol z bazy Demo |
| Ustawienie definicji na dokumencie | `dok.Definicja = def` | ustaw **pierwszą**, przed innymi polami |
| Rozpoznanie / ustawienie serii | `dok.Seria`, `dok.GetListSeria()` | seria tylko gdy numeracja ma komponent „Seria" |
| Numer w buforze | `dok.BuforNumer` → `"BUFOR"`, `dok.Numer.NumerPelny` | numer właściwy nadawany przy zatwierdzeniu |
| Wymuszenie numeru | `dok.Numer.NumerPelny = "..."` | tylko gdy definicja na to pozwala |
| Pełny numer (do odczytu) | `dok.Numer.NumerPelny`, `dok.NumerPelnyZapisany` | string z serią i numerem |

**Pola i typy:** `dok.Definicja: Soneta.Handel.DefDokHandlowego`, `dok.Seria: string`,
`dok.GetListSeria(): string[]`, `dok.Numer: Soneta.Core.NumerDokumentu` (bufor numeracji:
`NumerPelny: string`, `PrzeliczSymbol(string component)`), `dok.NumerPelnyZapisany: string`,
`dok.BuforNumer: string` (kalkulowane → `"BUFOR"` w buforze), `dok.Bufor: bool` (kalkulowane).

**Snippet:**

```csharp
var hm = session.GetHandel();

using (var t = session.Logout(editMode: true))
{
    var dok = new DokumentHandlowy();
    session.AddRow(dok);
    dok.Definicja = hm.DefDokHandlowych.WgSymbolu["FV"];   // definicja PIERWSZA — niesie schemat numeracji
    dok.Kontrahent = session.GetCRM().Kontrahenci.WgKodu["Abc"];

    // Seria — tylko gdy schemat numeracji definicji ma komponent „Seria"
    string[] dostepneSerie = dok.GetListSeria();
    if (dostepneSerie.Length > 0)
        dok.Seria = dostepneSerie[0];                      // ustawienie serii przelicza numer

    t.Commit();
}
session.Save();

// Odczyt numeru: w buforze numer właściwy nie jest jeszcze nadany
bool wBuforze = dok.Bufor;          // true → BuforNumer == "BUFOR"
string numer  = dok.Numer.NumerPelny;   // pełny numer (z serią), nadany przy zatwierdzeniu

// Zatwierdzenie nadaje numer właściwy
using (var t = session.Logout(editMode: true))
{
    dok.Stan = StanDokumentuHandlowego.Zatwierdzony;
    t.Commit();
}
session.Save();
```

**Pułapki:**
- `Definicja` ustaw **jako pierwszą** — to ona określa wymagane pola (magazyn, kontrahent) oraz
  schemat numeracji (`Numeracja`). Zmiana definicji po wypełnieniu dokumentu jest ograniczona
  (`IsReadOnlyDefinicja()`).
- `Seria` można ustawić **tylko**, gdy numeracja definicji ma komponent „Seria" — w przeciwnym razie
  setter rzuci `RowException` („SeriesDeniedErr"). Sprawdź przez `GetListSeria()` (zwraca dozwolone
  wartości; przy słowniku serii — tylko wartości ze słownika).
- Numer właściwy nadawany jest **przy zatwierdzeniu**; dokument w buforze ma `BuforNumer == "BUFOR"`,
  a `Numer.NumerPelny` zawiera znacznik „/BUFOR". Nie traktuj numeru z bufora jako ostatecznego.
- Wymuszenie numeru przez `dok.Numer.NumerPelny = "..."` działa tylko w granicach dozwolonych przez
  definicję (`IsReadOnlyNumerPelny()`); kolizja z istniejącym numerem ujawni się jako `RowException`
  z `DuplicateKeyException` w `Save()`.
- `Numer` to obiekt `NumerDokumentu` (bufor numeracji), nie zwykły string — pełny numer czytaj przez
  `Numer.NumerPelny` lub `NumerPelnyZapisany`, nie składaj go ręcznie z serii i liczby.

---

---

