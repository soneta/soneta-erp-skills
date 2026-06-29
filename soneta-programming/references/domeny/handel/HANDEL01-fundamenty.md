# HANDEL01 — Fundamenty i identyfikacja

> Wspólne fakty o typie, podstawowe typy i szablon wzorca: [../handel.md](../handel.md).

> Rozdział opisuje, jak z poziomu sesji dotrzeć do modułów handlowo-magazynowych, jak poprawnie
> wskazać **definicję dokumentu** (`DefDokHandlowego`) zanim utworzysz dokument, oraz jak na podstawie
> definicji i flag dokumentu **rozpoznać jego rodzaj** (faktura / magazynowy / zamówienie / korekta /
> zaliczka). Cały kod jest zgodny z **C# 10** i operuje wyłącznie na **publicznym kontrakcie**
> platformy. Fundamenty wspólne (sesja, transakcja `session.Logout(true)` + `Commit`/`CommitUI`,
> blokada optymistyczna, praca z `SubTable`) opisują [`safe-code.md`](../../safe-code.md),
> [`session-login.md`](../../session-login.md) oraz [`worker-extender.md`](../../worker-extender.md) — tutaj
> się do nich odwołujemy, nie powtarzamy ich.

### HANDEL-W1 — Dostęp do modułów handlowo-magazynowych i tabeli `DokHandlowe`

**Cel:** z obiektu `Session` (lub dowolnego `ISessionable` — `Row`, `Table`, `Context`) dotrzeć do
modułów, na których opiera się logika handlu i magazynu, oraz do tabeli dokumentów `DokHandlowe`.
To punkt wejścia każdego scenariusza w tym dokumencie.

**Warianty:**

| Wariant | Wywołanie (extension method na `Session`) | Co udostępnia |
|---|---|---|
| Moduł handlowy | `session.GetHandel()` → `HandelModule` | `.DokHandlowe` (tabela dokumentów), `.DefDokHandlowych` (definicje) |
| Moduł magazynowy | `session.GetMagazyny()` → `MagazynyModule` | `.Magazyny`, `.Zasoby`, `.Obroty`, `.GrupyDostaw` (partie), `.OkresyMag` |
| Moduł towarów | `session.GetTowary()` → `TowaryModule` | `.Towary`, `.Jednostki` |
| Moduł CRM | `session.GetCRM()` → `CRMModule` | `.Kontrahenci` |
| Moduł kasowy | `session.GetKasa()` → `KasaModule` | formy płatności, rozrachunki (dot. płatności dokumentu) |
| Waluty | `Soneta.Waluty.WalutyModule.GetInstance(session)` | `.Waluty`, `.TabeleKursowe` |

**Pola i typy:** `HandelModule.DokHandlowe: DokHandlowe` (tabela `DokumentHandlowy`),
`HandelModule.DefDokHandlowych` (tabela `DefDokHandlowego`),
`MagazynyModule.Magazyny`, `TowaryModule.Towary`, `CRMModule.Kontrahenci`. Wszystkie moduły
implementują `ISessionable` i mają property `.Session`.

**Snippet:**

```csharp
// Punkt wejścia — z sesji pobieramy moduły handlowo-magazynowe:
var handel    = session.GetHandel();      // HandelModule
var magazyny  = session.GetMagazyny();    // MagazynyModule
var towary    = session.GetTowary();      // TowaryModule
var crm       = session.GetCRM();         // CRMModule

// Tabela dokumentów handlowych (operacyjna, guided):
var dokumenty = handel.DokHandlowe;

// Iteracja po dokumentach — ZAWSZE zawężaj zakres (data/definicja/kontrahent),
// to tabela operacyjna rosnąca z biznesem. Filtr aplikujemy na indeksie (warunek serwerowy):
var od = Date.Today.AddMonths(-1);
foreach (DokumentHandlowy d in handel.DokHandlowe.WgDaty[(DokumentHandlowy x) => x.Data >= od])
{
    // d.* — Numer, Data, Definicja, Kontrahent, Suma, Stan ...
}

// Z dowolnego ISessionable można zejść do modułu również metodą GetInstance:
var hm = Soneta.Handel.HandelModule.GetInstance(jakisRow);   // gdy nie mamy zmiennej Session
```

**Pułapki:**
- Moduł i tabela są **single-threaded** — nie współdziel ich między wątkami; pobieraj je z sesji
  bieżącego wątku (thread-safety w SKILL.md).
- `session.GetWaluty()` jest **internal** — z dodatku zewnętrznego użyj
  `Soneta.Waluty.WalutyModule.GetInstance(session)`.
- **Nie ładuj całej tabeli `DokHandlowe`** do pamięci z `if`-em w pętli. Filtruj serwerowo —
  warunek aplikuj na indeksie tabeli (np. `WgDaty[(DokumentHandlowy x) => …]`), żeby wykonał się
  po stronie SQL (safe-code §6). W warunku `RowCondition` używaj **tylko pól bazodanowych** — pola
  kalkulowane rzucą `LinqConditionException`.
- Pobranie modułu nie tworzy ani nie modyfikuje danych — modyfikacje zawsze w transakcji
  (`session.Logout(true)` + `Commit`/`CommitUI`, potem `Save`).

### HANDEL-W2 — Wybór definicji dokumentu (`DefDokHandlowego`) wg symbolu

**Cel:** zanim utworzysz dokument, musisz wskazać jego **definicję** — to ona określa typ dokumentu
(sprzedaż, zakup, magazynowy, zamówienie…), numerację, zachowanie magazynu i płatności. Definicja
jest **pierwszym** ustawianym polem nowego dokumentu (`dok.Definicja = …`), zanim ustawisz magazyn,
kontrahenta czy pozycje.

**Warianty:**

| Wariant | Klucz / mechanizm | Uwaga |
|---|---|---|
| Po symbolu | `DefDokHandlowych.WgSymbolu["FV"]` | indeks **unikalny** — zwraca pojedynczy rekord lub `null` |
| Filtr po kategorii (typie) | `DefDokHandlowych.WgKategorii[KategoriaHandlowa.Sprzedaż]` | zbiór wszystkich definicji danej kategorii |
| Po symbolu w obrębie kategorii | warunek serwerowy na `WgSymbolu` + sprawdzenie `Kategoria` | gdy w bazie istnieje kilka wariantów sprzedaży |
| Walidacja istnienia | `WgSymbolu[symbol] != null` | brak definicji = nie da się utworzyć dokumentu |

Typowe symbole w bazie Demo: **FV** (faktura sprzedaży), **FZ** (faktura zakupu), **PAR** (paragon),
**PZ**/**PW** (przyjęcia magazynowe), **WZ**/**RW** (rozchody magazynowe), **ZO** (zamówienie
odbiorcy), **ZD** (zamówienie do dostawcy), **MM** (przesunięcie międzymagazynowe),
**INW** (inwentaryzacja), **KS** (korekta sprzedaży). Symbole zależą od konfiguracji konkretnej bazy —
nie zakładaj ich „na sztywno", weryfikuj `!= null`.

**Pola i typy:** `DefDokHandlowego.Symbol: string` (maks. 12 znaków, unikalny),
`DefDokHandlowego.Kategoria: Soneta.Handel.KategoriaHandlowa`. Indeks `WgSymbolu` jest unikalny
(zwraca pojedynczy rekord), `WgKategorii` grupuje definicje po kategorii.

**Snippet:**

```csharp
var handel = session.GetHandel();

// 1. Po symbolu — klucz unikalny: pojedynczy rekord albo null
DefDokHandlowego defFV = handel.DefDokHandlowych.WgSymbolu["FV"];
if (defFV == null)
    throw new BusException("Brak definicji dokumentu o symbolu FV w tej bazie.".Translate());

// 2. Wszystkie definicje danej kategorii (np. wszystkie definicje sprzedaży):
foreach (DefDokHandlowego d in handel.DefDokHandlowych.WgKategorii[KategoriaHandlowa.Sprzedaż])
{
    // d.Symbol, d.Kategoria ...
}

// 3. Użycie definicji przy tworzeniu dokumentu — Definicja USTAWIANA PIERWSZA:
using (var t = session.Logout(editMode: true))
{
    var dok = new DokumentHandlowy();
    session.AddRow(dok);                                       // AddRow przed ustawianiem pól
    dok.Definicja = handel.DefDokHandlowych.WgSymbolu["PW"];   // definicja jako pierwsze pole
    // dok.Magazyn / dok.Kontrahent ustawiamy dopiero PO definicji (gdy definicja ich wymaga)
    t.Commit();                                                // CommitUI() w workerze/extenderze
}
session.Save();
```

**Pułapki:**
- `WgSymbolu[...]` zwraca **pojedynczy** rekord (klucz unikalny) i może być `null` — zawsze sprawdź
  przed użyciem. `WgKategorii[...]` zwraca **zbiór** — iteruj lub `.FirstOrDefault()`.
- **Definicja musi być ustawiona jako pierwsze pole** dokumentu — od niej zależy widoczność i
  wymagalność pozostałych pól (magazyn, kontrahent, numeracja). Ustawienie magazynu/kontrahenta
  przed definicją jest błędem.
- Symbole **nie są gwarantowane** — zależą od konfiguracji bazy klienta. Nie polegaj na obecności
  „FV"/„WZ"; pobierz definicję i sprawdź `!= null`, a w razie potrzeby filtruj po `Kategoria`.
- `DefDokHandlowego` to dane **konfiguracyjne** (`GuidedRow`) — odczytuj je, nie twórz „w locie" w
  kodzie operacyjnym.

### HANDEL-W3 — Rozpoznanie rodzaju dokumentu (faktura / magazynowy / zamówienie / korekta / zaliczka)

**Cel:** ustalić, „czym jest" dany dokument — fakturą, dokumentem magazynowym, zamówieniem, korektą
czy dokumentem zaliczkowym — by rozgałęzić logikę (np. inaczej traktować rozchód magazynowy niż
zamówienie). Rozpoznanie opiera się na **kategorii definicji** (`Definicja.Kategoria`) oraz na
gotowych flagach dokumentu (`Korekta`, `JestDokZaliczkowy()`).

**Warianty:**

| Co rozpoznajemy | Mechanizm (publiczny kontrakt) | Wartości / zakres `KategoriaHandlowa` |
|---|---|---|
| Faktura/handlowy (sprzedaż, zakup, korekty, f. wewnętrzna) | `Definicja.Kategoria` w zakresie handlowym | `Sprzedaż=2`, `KorektaSprzedaży=3`, `Zakup=4`, `KorektaZakupu=5`, `FakturaWewnętrzna=6` (zakres `HandelPierwszy=1 … HandelOstatni=100`) |
| Magazynowy (PW/PZ/WZ/RW/MM/INW…) | `Definicja.Kategoria` w zakresie magazynowym | `PrzyjęcieMagazynowe=102`, `WydanieMagazynowe=104`, `PrzesunięcieMagazynowe=106`, `Inwentaryzacja=107` … (zakres `MagazynPierwszy=101 … MagazynOstatni=200`) |
| Zamówienie (ZO/ZD/wewn.) | `Definicja.Kategoria` | `ZamówienieOdbiorcy=302`, `ZamówienieDostawcy=303`, `ZamówienieWewnętrzne=312` |
| Korekta | flaga `dok.Korekta` **lub** kategoria typu `Korekta*` | `dok.Korekta == true`; kategorie: `KorektaSprzedaży`, `KorektaZakupu`, `KorektaPrzyjęciaMagazynowego`, `KorektaWydaniaMagazynowego` … |
| Dokument zaliczkowy | metoda `dok.JestDokZaliczkowy()` / `dok.JestDokZaliczkowy(out bool korekta)` | `true` = zaliczkowy; `out korekta` = korekta zaliczki |

**Pola i typy:**
- `DokumentHandlowy.Definicja: Soneta.Handel.DefDokHandlowego` — definicja dokumentu.
- `DefDokHandlowego.Kategoria: Soneta.Handel.KategoriaHandlowa` — **kluczowy** wyznacznik rodzaju.
- `DokumentHandlowy.Korekta: bool` (kalkulowane, read-only) — czy dokument jest korektą.
- `DokumentHandlowy.JestDokZaliczkowy(): bool` oraz `JestDokZaliczkowy(out bool korekta): bool` —
  rozpoznanie zaliczki (drugi przeciążony wariant zwraca też, czy to korekta zaliczki).
- `DefDokHandlowego.Symbol: string` — symbol (do logów / komunikatów).

Enum `Soneta.Handel.KategoriaHandlowa` (wartości publiczne) ma czytelne **markery zakresów**:
`HandelPierwszy=1`/`HandelOstatni=100`, `MagazynPierwszy=101`/`MagazynOstatni=200`,
`PozostałePierwszy=301`/`PozostałeOstatni=400`. Pozwalają one rozpoznać „grupę" dokumentu zakresem,
bez wyliczania wszystkich symboli.

**Snippet:**

```csharp
// Rozpoznanie rodzaju dokumentu na podstawie kategorii jego definicji + flag dokumentu.
// KategoriaHandlowa to enum — markery zakresów (HandelPierwszy/Ostatni, MagazynPierwszy/Ostatni)
// pozwalają klasyfikować grupę dokumentu bez wymieniania wszystkich symboli.
static string RozpoznajRodzaj(DokumentHandlowy dok)
{
    KategoriaHandlowa kat = dok.Definicja.Kategoria;

    // Zaliczka i korekta mają dedykowane, jednoznaczne testy — sprawdzamy je najpierw:
    if (dok.JestDokZaliczkowy(out bool korektaZaliczki))
        return korektaZaliczki ? "Korekta zaliczki" : "Dokument zaliczkowy";

    if (dok.Korekta)
        return "Korekta";

    // Klasyfikacja grupy po zakresie wartości enuma (markery są publiczne):
    return kat switch
    {
        >= KategoriaHandlowa.HandelPierwszy  and <= KategoriaHandlowa.HandelOstatni  => "Faktura / dokument handlowy",
        >= KategoriaHandlowa.MagazynPierwszy and <= KategoriaHandlowa.MagazynOstatni => "Dokument magazynowy",
        KategoriaHandlowa.ZamówienieOdbiorcy
            or KategoriaHandlowa.ZamówienieDostawcy
            or KategoriaHandlowa.ZamówienieWewnętrzne                                => "Zamówienie",
        _ => "Inny"
    };
}

// Przykład użycia — rozgałęzienie logiki po rodzaju:
DokumentHandlowy dok = session.GetHandel().DokHandlowe.WgDaty[
    (DokumentHandlowy d) => d.Data == Date.Today].FirstOrDefault();

if (dok != null && dok.Definicja.Kategoria == KategoriaHandlowa.WydanieMagazynowe)
{
    // ... logika dotycząca rozchodu magazynowego
}
```

**Pułapki:**
- **Rodzaj wynika z definicji, nie z symbolu.** Symbol (np. „FV") jest dowolny i zależny od bazy —
  rozpoznawaj po `Definicja.Kategoria`, a nie po porównaniu `Symbol == "FV"`.
- Pomocnicze metody rozszerzające na enumie (`JestHandlowa`, `JestMagazynowa`, `JestZamowienie`)
  są **`internal`** — z dodatku zewnętrznego ich nie wywołasz. Klasyfikuj **zakresami markerów**
  (`>= HandelPierwszy and <= HandelOstatni` itd.) lub porównaniem do konkretnych wartości — tak jak
  w snippetcie.
- Wartości `*Pierwszy`/`*Ostatni` są oznaczone `[Hidden]` (nie pokazują się w UI), ale to **publiczne**
  stałe enuma — wolno ich użyć w kodzie jako granic zakresu.
- `Korekta` i wyniki `JestDokZaliczkowy()` są **kalkulowane (read-only)** — służą tylko do odczytu;
  nie próbuj ich ustawiać. Korektę tworzy się przez relacje dokumentów (`IRelacjeService.NowaKorekta`),
  a nie przez przestawienie flagi.
- Sprawdzaj zaliczkę/korektę **przed** klasyfikacją zakresową: korekta sprzedaży nadal mieści się w
  zakresie handlowym, a zaliczka bywa fakturą — dedykowane testy (`JestDokZaliczkowy`, `Korekta`)
  są bardziej szczegółowe i powinny mieć pierwszeństwo.
- `dok.Definicja` może w teorii być `null` na świeżo utworzonym, jeszcze nieskonfigurowanym
  dokumencie — przy klasyfikacji dokumentów „w trakcie tworzenia" zabezpiecz dostęp do `Kategoria`.

---

