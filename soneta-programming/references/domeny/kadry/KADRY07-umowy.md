# KADRY07 — Umowy cywilnoprawne

> Wspólne fakty o typie, podstawowe typy i szablon wzorca: [../kadry.md](../kadry.md).

### KADRY-G1 — Dodawanie umów cywilnoprawnych (zlecenie, o dzieło) (★)

**Cel:** utworzyć dla pracownika umowę cywilnoprawną (zlecenie / o dzieło / ryczałtowa) z kompletem
danych pozwalającym na `Session.Save()`: definicja elementu płacowego (rodzaj umowy), okres, wartość,
sposób rozliczenia i typ wartości (brutto/netto).

**Mechanizm (kluczowy):** `Soneta.Kadry.Umowa` to **root historyczny** (tabela `Umowy`), child
pracownika. Dodanie umowy do tabeli (`Module.Umowy.AddRow(umowa)`) w `OnAdded` **automatycznie tworzy
pierwszy zapis** `Soneta.Kadry.UmowaHistoria` (tabela `UmowaHistorie`) oraz domyślną definicję
elementu, okres, datę i numerację. Dlatego **nie tworzymy `UmowaHistoria` ręcznie** — bezpośrednio po
`AddRow` istnieje już `umowa.Last` (pierwszy zapis), na którym ustawiamy **wartość umowy**.

> **Gdzie co siedzi.** Dane „nagłówkowe" umowy (definicja elementu, okres, sposób rozliczenia, typ
> wartości) są na **roocie** `Umowa`. **Kwota/wartość umowy** jest **historyczna** i siedzi na
> `UmowaHistoria.Wartosc` — ustawiasz ją przez `umowa.Last.Wartosc`. Property `umowa.Brutto`/
> `umowa.Wartosc` na roocie oraz `UmowaHistoria.Brutto` są **wyliczane** (read-only).

**Warianty (rodzaj umowy = `DefinicjaElementu`):**

| Rodzaj umowy | Jak wskazać definicję |
|---|---|
| Zlecenie | `DefElementow[DefinicjaElementu.UmowaZlecenie]` (to też wartość domyślna konfiguracji) |
| O dzieło (20% KUP) | `DefElementow[DefinicjaElementu.Umowa20]` |
| Ryczałtowa | `DefElementow[DefinicjaElementu.UmowaRyczałtowa]` |
| Inna (po kodzie/nazwie) | `DefElementow["<kod>"]` (indeksator string = wg kodu) — pod warunkiem że jej `RodzajZrodla == RodzajŹródłaWypłaty.Umowa` |

**Pola i typy:**

| Pole | Gdzie | Typ | Uwaga |
|---|---|---|---|
| `Element` | `Umowa` (root) | `Soneta.Place.DefinicjaElementu` | definicja elementu = rodzaj umowy; akceptowana tylko gdy `RodzajZrodla == RodzajŹródłaWypłaty.Umowa` |
| `Data` | `Umowa` (root) | `Soneta.Types.Date` | data zawarcia/dokumentu |
| `Okres` | `Umowa` (root) | `Soneta.Types.FromTo` | okres obowiązywania umowy |
| `Tytul` | `Umowa` (root) | `string` | tytuł/temat umowy |
| `RodzajRozliczenia` | `Umowa` (root) | `Soneta.Kadry.RodzajeRozliczeniaUmowy` | `KwotaDoWypłaty` / `StawkaZaOkres` / `StawkaZaGodzinę` |
| `TypWartosci` | `Umowa` (root) | `Soneta.Kadry.TypWartosciUmowy` | `Brutto` / `Netto` |
| `Wydzial` | `Umowa` (root) | `Soneta.Kadry.Wydzial` | jednostka organizacyjna — **wymagana** (weryfikator przy `Save()`); ustaw `kadry.Wydzialy.Firma` |
| `Wartosc` | `UmowaHistoria` (`Last`) | `Soneta.Types.Currency` | **kwota/wartość umowy** — ustawiana na zapisie historycznym |

**Snippet:**

```csharp
var kadry = session.GetKadry();
var place = session.GetPlace();
var pracownik = kadry.Pracownicy.WgKodu["006"];

using (var t = session.Logout(editMode: true))
{
    // 1) Utworzenie umowy + dodanie do tabeli; w OnAdded powstaje pierwszy UmowaHistoria.
    var umowa = session.AddRow(new Umowa(pracownik));

    // 2) Definicja elementu = rodzaj umowy (tu: zlecenie). Indeksator Guid + stała publiczna.
    umowa.Element = place.DefElementow[DefinicjaElementu.UmowaZlecenie];

    // 3) Dane nagłówkowe na roocie:
    umowa.Data   = new Date(2026, 1, 1);
    umowa.Okres  = new FromTo(new Date(2026, 1, 1), new Date(2026, 12, 31));
    umowa.Tytul  = "Umowa zlecenie - obsługa projektu";
    umowa.RodzajRozliczenia = RodzajeRozliczeniaUmowy.KwotaDoWypłaty;
    umowa.TypWartosci       = TypWartosciUmowy.Brutto;
    umowa.Wydzial           = kadry.Wydzialy.Firma;   // wymagane przy Save()

    // 4) KWOTA umowy — na zapisie historycznym Last (UmowaHistoria.Wartosc):
    umowa.Last.Wartosc = new Currency(5000m);

    t.Commit();   // Commit() w kodzie biznesowym; CommitUI() w workerze/UI
}
session.Save();   // tu wykrywane konflikty/duplikaty
```

**Pułapki:**
- **Nie** twórz ręcznie pierwszego `UmowaHistoria` — robi to `OnAdded` przy `AddRow`. Ręczny nowy
  zapis dotyczy dopiero *zmiany/aneksu* „od daty" (KADRY-G2).
- **Kwotę ustawiaj na `umowa.Last.Wartosc`**, nie na roocie — `umowa.Brutto`/`umowa.Wartosc` oraz
  `Last.Brutto` są wyliczane (read-only). `Wartosc` to `Soneta.Types.Currency`, nie `decimal`
  (safe-code §10.1).
- `Element` przyjmie tylko definicję o `RodzajZrodla == RodzajŹródłaWypłaty.Umowa`. Definicje
  „etatowe" (np. `EtatMies`) zostaną zignorowane (umowa dostanie domyślną definicję z konfiguracji).
- Jeśli `Element` **nie** zostanie ustawiony, umowa przyjmuje
  `Module.Config.Ogólne.DomyślnaDefinicjaUmowy` (domyślnie = `UmowaZlecenie`).
- Dodanie umowy do pracownika **w archiwum** rzuca wyjątek (`WArchiwumException`) z `OnAdded`.
- `RodzajRozliczenia`/`TypWartosci` bywają w UI tylko-do-odczytu zależnie od definicji elementu —
  w kodzie biznesowym ustawiasz je wprost (nie używaj `IsReadOnlyXxx`, safe-code §7.1).
- Całość w transakcji (`session.Logout(editMode: true)`); brak `Commit()` = rollback przy `Dispose()`.

### KADRY-G2 — Zmiana/aneks umowy (★)

**Cel:** zmienić warunki istniejącej umowy. Trzy rozłączne przypadki: **(a) korekta** danych
nagłówkowych w bieżącym okresie; **(b) aneks „od daty"** — nowy zapis historyczny `UmowaHistoria`
obowiązujący od wskazanego dnia (analogicznie do `PracHistoria`, sekcja KADRY-A14); **(c) seryjna
aktualizacja stawki** workerem.

**Mechanizm `HistorySubTable<UmowaHistoria>`** (`umowa.Historia`):

| Operacja | API | Efekt |
|---|---|---|
| Odczyt zapisu na dzień | `umowa[date]` (== `(UmowaHistoria)Historia[date]`) | zapis, którego `Aktualnosc` zawiera `date` |
| Ostatni (bieżący) zapis | `umowa.Last` (== `Historia.GetPrev()`) | najświeższy zapis |
| Pierwszy zapis | `umowa.Historia.GetFirst()` | najstarszy zapis |
| **Nowy zapis „od daty"** | `(UmowaHistoria)umowa.Historia.Update(date)` | **klonuje** zapis aktualny na `date`, skraca stary do `date-1`, zwraca **nowy** klon (okres od `date`); klon trzeba dodać do tabeli |
| Okres obowiązywania | `UmowaHistoria.Aktualnosc: FromTo` | „od–do" zapisu (zarządzany przez historię) |

**(a) Korekta danych nagłówkowych umowy (bez nowego okresu):**

```csharp
var umowa = pracownik.Umowy.First();   // lub wyszukanie po polu/numerze

using (var t = session.Logout(editMode: true))
{
    umowa.Tytul = "Umowa zlecenie - aneks zakresu prac";
    umowa.Okres = new FromTo(umowa.Okres.From, new Date(2027, 6, 30));   // przedłużenie
    t.Commit();
}
session.Save();
```

**(b) Aneks „od daty" — zmiana wartości umowy nowym zapisem historycznym:**

```csharp
var odDnia = new Date(2026, 7, 1);

using (var t = session.Logout(editMode: true))
{
    // 1) Update klonuje zapis aktualny na `odDnia` i zwraca nowy klon (okres od `odDnia`);
    //    stary zapis zostaje skrócony do dnia poprzedniego.
    var nowy = (UmowaHistoria)umowa.Historia.Update(odDnia);

    // 2) Klon trzeba dodać do tabeli zapisów historii umowy.
    umowa.Module.UmowaHistorie.AddRow(nowy);

    // 3) Na nowym zapisie ustawiamy zmienioną wartość (od `odDnia`):
    nowy.Wartosc = new Currency(6000m);
    // Uwaga: UmowaHistoria.PowodAktualizacji jest tylko do odczytu (ustawiane wewnętrznie) — nie przypisuj.

    t.Commit();
}
session.Save();
```

**(c) Seryjna aktualizacja stawki — worker `Umowa.AktualizacjaStawkiWorker`:**

```csharp
var worker = new Umowa.AktualizacjaStawkiWorker
{
    Umowy = new[] { umowa },
    Pars  = ... // Umowa.AktualizacjaStawkiWorker.Params: Data: Date, Wartosc: Currency
};
// uruchomienie zgodnie z konwencją workerów (patrz worker-extender.md)
```

> Pokrewne workery (wywoływane jak każdy worker enova): `Umowa.KopiujUmowe2Worker`
> (`Umowa Umowa` — kopiuje umowę), `Umowa.WyrejestrujUmoweWorker` (wyrejestrowanie umowy).

**Pułapki:**
- **`Update(date)` + `UmowaHistorie.AddRow(nowy)` to nierozłączna para.** Sam `Update` tworzy
  „odpięty" klon — bez `AddRow` zmiana nie zostanie zapisana.
- `Update(date)` rzuca wyjątek duplikatu, gdy na `date` już zaczyna się zapis (`Aktualnosc.From == date`)
  — nie da się „aktualizować" dwa razy tego samego dnia; wtedy modyfikuj istniejący zapis (`umowa[date]`).
- **Korekta** (`umowa.Last.Wartosc = …` lub modyfikacja `umowa[date]`) zmienia dane w **całym** okresie
  tego zapisu — używaj jej do poprawy błędu, nie do „zmiany od dnia"; do zmiany od dnia → wariant (b).
- `Aktualnosc` (okres zapisu) jest zarządzany przez historię — **nie ustawiaj go ręcznie**; do
  skrócenia/wstawienia okresu służy `Update`.
- Wartość zawsze jako `Soneta.Types.Currency`, nie `decimal` (safe-code §10.1); daty jako
  `Soneta.Types.Date`/`Date.Today` (§10.2).
- Obsłuż `RowConflictException` z `Save()` (safe-code §4); transakcje trzymaj krótkie (§13.1).

### KADRY-G3 — Operacja seryjna „Dodaj umowy" dla grupy osób (★)

**Cel:** dodać jednakową umowę cywilnoprawną (zlecenie / o dzieło / ryczałtowa) **naraz dla wielu
zaznaczonych pracowników** — operacja seryjna z listy osób. W UI: menu *Operacje seryjne → Dodaj
umowy…*. Każdej osobie z zaznaczenia tworzona jest osobna `Umowa` z tymi samymi danymi nagłówkowymi
(definicja elementu, okres, wartość, sposób rozliczenia), analogicznie do KADRY-G1.

**Worker (publiczny kontrakt):** `Soneta.Kadry.Pracownik.DodajUmowęWorker` — worker **przypisany do
`Pracownik`** (`DataType = Pracownik`). Udostępnia akcję `DodajUmowę` w menu czynności listy
pracowników.

| Składowa | Typ / sygnatura | Uwaga |
|---|---|---|
| Konstruktor | `DodajUmowęWorker(Session session)` | worker ma **ctor z `Session`** (nie bezparametrowy) |
| Zaznaczone osoby | `DodajUmowęWorker.Pracownicy: Pracownik[]` | `[Context]` — tablica pracowników z zaznaczenia listy |
| Parametry | `DodajUmowęWorker.Pars: DodajUmowęWorker.Params` | `[Context]` — okno parametrów operacji; `Params(Context)` |
| Akcja | `void DodajUmowę()` | tworzy umowy dla wszystkich `Pracownicy` (zwraca **`void`**, nie `object`) |

**Parametry operacji (`DodajUmowęWorker.Params`):**

| Pole | Typ | Odpowiednik na `Umowa` (KADRY-G1) |
|---|---|---|
| `Definicja` | `Soneta.Core.DefinicjaDokumentu` | definicja dokumentu umowy (numeracja/seria) |
| `Seria` | `string` | seria numeracji |
| `Wydział` | `Soneta.Kadry.Wydzial` | `Umowa.Wydzial` (wymagany) |
| `Data` | `Soneta.Types.Date` | `Umowa.Data` (data zawarcia) |
| `Okres` | `Soneta.Types.FromTo` | `Umowa.Okres` |
| `Tytuł` | `string` | `Umowa.Tytul` |
| `Element` | `Soneta.Place.DefinicjaElementu` | `Umowa.Element` (rodzaj umowy) |
| `RodzajRozliczenia` | `Soneta.Kadry.RodzajeRozliczeniaUmowy` | `Umowa.RodzajRozliczenia` |
| `Wartość` | `Soneta.Types.Currency` | `umowa.Last.Wartosc` (kwota umowy) |
| `TypWartości` | `Soneta.Kadry.TypWartosciUmowy` | `Umowa.TypWartosci` (`Brutto`/`Netto`) |

**Wariant A — wywołanie workera platformy (zalecane):** zainicjuj `DodajUmowęWorker`, ustaw
`Pracownicy` i `Pars`, wywołaj `DodajUmowę()` (worker uruchamia się jak każdy worker — patrz
[`worker-extender.md`](../worker-extender.md), sekcja *Programowe użycie workera*).

```csharp
var kadry = session.GetKadry();
var place = session.GetPlace();

// Grupa osób (np. z zaznaczenia listy). Tu: kilku pracowników po kodzie:
var osoby = new[]
{
    kadry.Pracownicy.WgKodu["006"],
    kadry.Pracownicy.WgKodu["007"],
    kadry.Pracownicy.WgKodu["008"],
};

var pars = new Pracownik.DodajUmowęWorker.Params(context);
pars.Element           = place.DefElementow[DefinicjaElementu.UmowaZlecenie];
pars.Okres             = new FromTo(new Date(2026, 1, 1), new Date(2026, 12, 31));
pars.Data              = new Date(2026, 1, 1);
pars.Tytuł             = "Umowa zlecenie - projekt grupowy";
pars.RodzajRozliczenia = RodzajeRozliczeniaUmowy.KwotaDoWypłaty;
pars.TypWartości       = TypWartosciUmowy.Brutto;
pars.Wartość           = new Currency(4000m);
pars.Wydział           = kadry.Wydzialy.Firma;   // wymagany

// Worker ma konstruktor z Session (nie bezparametrowy); Pracownicy/Pars przez inicjalizator:
var worker = new Pracownik.DodajUmowęWorker(session) { Pracownicy = osoby, Pars = pars };
worker.DodajUmowę();   // void
session.Save();
```

**Wariant B — pętla po pracownikach (jawne tworzenie, jak KADRY-G1):** gdy nie chcesz przechodzić przez
worker — dla każdej osoby twórz `new Umowa(p)` i ustaw te same pola co w KADRY-G1. To jawnie pokazuje, że
operacja seryjna = KADRY-G1 powtórzone w pętli.

```csharp
var kadry = session.GetKadry();
var place = session.GetPlace();
var defElementu = place.DefElementow[DefinicjaElementu.UmowaZlecenie];
var okres = new FromTo(new Date(2026, 1, 1), new Date(2026, 12, 31));

using (var t = session.Logout(editMode: true))
{
    foreach (var p in osoby)
    {
        var umowa = session.AddRow(new Umowa(p));   // OnAdded tworzy pierwszy UmowaHistoria
        umowa.Element           = defElementu;
        umowa.Data              = okres.From;
        umowa.Okres             = okres;
        umowa.Tytul             = "Umowa zlecenie - projekt grupowy";
        umowa.RodzajRozliczenia = RodzajeRozliczeniaUmowy.KwotaDoWypłaty;
        umowa.TypWartosci       = TypWartosciUmowy.Brutto;
        umowa.Wydzial           = kadry.Wydzialy.Firma;   // wymagany przy Save()
        umowa.Last.Wartosc      = new Currency(4000m);    // kwota na zapisie historycznym
    }
    t.Commit();   // Commit() w kodzie biznesowym; CommitUI() w workerze/UI
}
session.Save();
```

**Pułapki:**
- `Pracownik.DodajUmowęWorker` jest workerem na typie `Pracownik`, a tworzy obiekty `Umowa` — nie myl
  go z workerami na `Umowa` (KADRY-G2: `AktualizacjaStawkiWorker`, `KopiujUmowe2Worker`).
- W wariancie B obowiązują wszystkie pułapki KADRY-G1: kwota na `umowa.Last.Wartosc` (root `Brutto`/`Wartosc`
  są wyliczane), `Element` tylko o `RodzajZrodla == RodzajŹródłaWypłaty.Umowa`, `Wydzial` wymagany,
  dodanie umowy pracownikowi **w archiwum** rzuca `WArchiwumException`.
- Pętlę edycyjną trzymaj krótką (safe-code §13.1); konflikty/duplikaty wykrywane w `Save()` (§4).
- Wartość zawsze jako `Soneta.Types.Currency`, daty jako `Date`/`FromTo`, nie `decimal`/`DateTime`
  (safe-code §10).

---

### KADRY-G4 — Rachunek do umowy zlecenia (★)

**Cel:** wystawić **rachunek do umowy zlecenia** — czyli rozliczyć (naliczyć i wypłacić) umowę
cywilnoprawną. W modelu Soneta „rachunek do umowy zlecenia" **nie jest osobnym rekordem** na `Umowa`
ani w `pracownik.Rachunki` — to **wypłata z umowy** typu `Soneta.Place.WyplataUmowa`, naliczana
mechanizmem płac (jak KADRY-H2). `pracownik.Rachunki: SubTable<Soneta.Kasa.RachunekBankowyPodmiotu>` to
**rachunki bankowe** pracownika (numer konta), a nie rachunki do umów — nie myl tych pojęć.

> **Gdzie to siedzi.** Każda umowa ma wstecz powiązane rozliczenia/wypłaty:
> - `Umowa.RozliczeniaWynagrodzenia: LpSubTable<Soneta.Place.RozliczenieWynagrodzenia>` — rozliczenia
>   wynagrodzenia z umowy,
> - `Umowa.Elementy: SubTable<Soneta.Place.WypElement>` — naliczone składniki wypłat tej umowy,
> - sama wypłata to `Soneta.Place.WyplataUmowa` (konkretny typ `Wyplata`), z polem zwrotnym
>   `WyplataUmowa.Umowa: Soneta.Kadry.Umowa`.
> Stan rozliczenia umowy odczytasz z `Umowa.Stan: Soneta.Kadry.StanUmowy`
> (`Niewypłacona` / `WypłaconaCzęściowo` / `WypłaconaCałkowicie` / `Anulowana`) oraz z
> `Umowa.Splacono`, `Umowa.Pozostało` (`Soneta.Types.Currency`).

**Tworzenie rachunku (wypłaty) do umowy — wykonawca naliczania `NaliczanieSeryjne.Umowy` (jak KADRY-H2):**

| Element | Typ / sygnatura | Uwaga |
|---|---|---|
| Parametry | `new NaliczanieSeryjne.UmowaParams(context)` | `Naliczanie` na sztywno `PłatnaZDołu` (setter rzuca `NotSupportedException`) |
| Data rachunku / listy | `UmowaParams.DataWypłaty`, `.DataListy` | daty rachunku |
| Wykonawca | `new NaliczanieSeryjne.Umowy(UmowaParams) { Umowa = umowa }` | ustawienie `Umowa` ustawia też `Pracownik` z `umowa.Pracownik` |
| Uruchomienie | `Umowy.Nalicz(): NaliczanieWypłat` | tworzy `WyplataUmowa` i liczy składniki |
| Wynik | `NaliczanieWypłat.WszystkieWypłaty: IList` | elementy `Wyplata` (tu `WyplataUmowa`) |

```csharp
var kadry = session.GetKadry();
var pracownik = kadry.Pracownicy.WgKodu["006"];

// Umowa zlecenie pracownika (np. utworzona w KADRY-G1):
Umowa umowa = pracownik.Umowy.Cast<Umowa>()
    .First(u => u.Element == session.GetPlace().DefElementow[DefinicjaElementu.UmowaZlecenie]);

var pars = new NaliczanieSeryjne.UmowaParams(context);
pars.DataWypłaty = new Date(2026, 2, 10);   // data wystawienia rachunku
pars.DataListy   = pars.DataWypłaty;

var naliczanie = new NaliczanieSeryjne.Umowy(pars) { Umowa = umowa };
NaliczanieWypłat wynik = naliczanie.Nalicz();   // tworzy WyplataUmowa (rachunek)

foreach (Wyplata w in wynik.WszystkieWypłaty)
{
    // w.Typ == TypWyplaty.Umowa; w to WyplataUmowa; w.Elementy = składniki rachunku
}
session.Save();

// Po naliczeniu — stan rozliczenia umowy:
StanUmowy stan = umowa.Stan;          // np. WypłaconaCałkowicie
Currency splacono = umowa.Splacono;   // kwota rozliczona
Currency pozostalo = umowa.Pozostało; // pozostała do wypłaty
```

**Odczyt rachunków (wypłat) wystawionych do umowy:**

```csharp
// Wypłaty (rachunki) tej umowy — przez wypłaty pracownika filtrowane po umowie:
foreach (WyplataUmowa w in pracownik.Wyplaty.OfType<WyplataUmowa>().Where(x => x.Umowa == umowa))
{
    // w.Data, w.Elementy (WypElement.Wartosc / .Netto / .Podatki.*)
}

// Składniki naliczone bezpośrednio z umowy:
foreach (WypElement e in umowa.Elementy)
{
    // e.Wartosc, e.Netto
}
```

**Pułapki:**
- „Rachunek do umowy zlecenia" = `WyplataUmowa`, a nie rekord w `pracownik.Rachunki` (to rachunki
  bankowe). Tworzysz go naliczaniem (`NaliczanieSeryjne.Umowy.Nalicz()`), nie `AddRow` po wypłacie.
- **Nie ustawiaj `UmowaParams.Naliczanie`** — umowy są zawsze „płatne z dołu" (setter rzuca
  `NotSupportedException`).
- Ustawienie `Umowy.Umowa` nadpisuje `Pracownik` właścicielem umowy — nie ustawiaj `Pracownik` ręcznie.
- `Nalicz()` wewnętrznie otwiera własną transakcję i zatwierdza zmiany w sesji — po nim wołasz tylko
  `Session.Save()`; nie owijaj go w dodatkowy `Logout(editMode: true)`.
- `Wyplata` nie ma agregatów `Brutto`/`Netto` — sumuj składniki z `Wyplata.Elementy` (jak w KADRY-H2/KADRY-H4).
- Kwoty jako `Soneta.Types.Currency`, daty jako `Date` (safe-code §10).

---

### KADRY-G5 — Zgłoszenia ZUS zleceniobiorców (ZUA / ZZA na podstawie umowy) (★)

**Cel:** przygotować zgłoszenie zleceniobiorcy do ZUS — **ZUA** (zgłoszenie do ubezpieczeń
społecznych + zdrowotnego) albo **ZZA** (tylko zdrowotne) — **na podstawie schematu ubezpieczeń
umowy**, oraz wyrejestrowanie (**ZWUA**) po jej zakończeniu. O tym, czy powstaje ZUA czy ZZA, decyduje
**schemat ubezpieczeń zapisu umowy**, a nie odrębne pole „rodzaj zgłoszenia".

> **Schemat ubezpieczeń umowy — gdzie siedzi.** Ubezpieczenia umowy są **historyczne** i leżą na
> zapisie `UmowaHistoria.Ubezpieczenia: Soneta.Kadry.Ubezpieczenia` (analogicznie do
> `PracHistoria.Etat.Ubezpieczenia` z KADRY-A7; ta sama struktura `Spoleczne`/`Zdrowotne`). Z roota dostępne
> przez `umowa.Last.Ubezpieczenia` (oraz `umowa.Ubezpieczenia` jako delegat). Kluczowe pola:
> - `Ubezpieczenia.Tyub4: Soneta.Kadry.TytulUbezpieczenia4` — **tytuł ubezpieczenia** (decyduje o ZUA
>   vs ZZA); pobierany ze słownika `session.GetKadry().TytulyUbezpiecz4.WgKodu[int]`,
> - `Ubezpieczenia.ObowiazkoweOd: Date` — data objęcia ubezpieczeniami społecznymi obowiązkowymi,
> - `Ubezpieczenia.Emerytalne` / `Rentowe` / `Chorobowe` / `Wypadkowe : Soneta.Kadry.Spoleczne` —
>   poszczególne społeczne (`Obowiazkowe`, `Dobrowolne`, `DobrowolneOd`, `Do`; `Od` read-only),
> - `Ubezpieczenia.Zdrowotne: Soneta.Kadry.Zdrowotne` — zdrowotne (`ObowiazkoweOd` zapisywalne).
>
> **Reguła ZUA vs ZZA:** zleceniobiorca podlegający ubezpieczeniom **społecznym** (emerytalne/rentowe
> obowiązkowe) → **ZUA**; podlegający **tylko zdrowotnemu** (np. uczeń/student do 26 r.ż., zbieg
> tytułów) → **ZZA**. Worker rozpoznaje to automatycznie po schemacie `UmowaHistoria.Ubezpieczenia`.
>
> **Uwaga (zweryfikowane testem):** świeży zapis ubezpieczeń umowy zlecenie ma **domyślnie**
> `Emerytalne.Obowiazkowe == true` i `Rentowe.Obowiazkowe == true` (schemat ZUA). Aby uzyskać **ZZA**,
> trzeba je **jawnie wyłączyć** (`ub.Emerytalne.Obowiazkowe = false; ub.Rentowe.Obowiazkowe = false;`)
> — samo ustawienie `Zdrowotne.ObowiazkoweOd` nie wystarcza.

**Worker (publiczny kontrakt):** `Soneta.Deklaracje.ZUS.ZarejestrujUmowyWorker` — worker
**przypisany do `Umowa`** (`DataType = Umowa`). Operuje na zaznaczonych umowach i generuje deklaracje
zgłoszeniowe ZUS. W UI: menu czynności listy umów *Deklaracje ZUS → Przygotuj ZUA i ZZA* (oraz
wyrejestrowanie).

| Składowa | Typ / sygnatura | Uwaga |
|---|---|---|
| Worker | `ZarejestrujUmowyWorker()` | ctor **bezparametrowy**; `Umowy: Umowa[]` jest **set-only** (ustaw przez inicjalizator) |
| Zaznaczone umowy | `ZarejestrujUmowyWorker.Umowy: Umowa[]` | `[Context]` — umowy do zgłoszenia (write-only) |
| Akcja: zgłoszenie | `object ZarejestrujUmowyWorker.Rejestracja.ZarejestrujUmowy()` | tworzy ZUA/ZZA (i ZCNA dla rodziny — `Pars.ZarejestrujRodzinę`); `Rejestracja()` ctor bezparam. |
| Akcja: wyrejestrowanie | `object ZarejestrujUmowyWorker.Wyrejestrowanie.WyrejestrujUmowy()` | tworzy ZWUA |
| Parametry zgłoszenia | `Rejestracja.Pars: ParamsZ` | **set-only**; `ParamsZ(Context)`; pola bazowe `Okres`/`DataDokumentu`/`DataWypełnienia`/`Kedu` (write-only) + własne `ZarejestrujRodzinę: bool` |
| Parametry wyrejestrowania | `Wyrejestrowanie.Pars: ParamsW` | **set-only**; `ParamsW(Context)`; `Okres`/`DataDokumentu`/`DataWypełnienia`/`Kedu` + `WyrejestrujRodzinę: bool` |

**Wspólny kontrakt bazowy `ZarejestrujBaseWorker`** (do odczytu wyniku i sterowania okresem):

| Pole / metoda | Typ / sygnatura | Uwaga |
|---|---|---|
| `Okres` | `Soneta.Types.FromTo` | okres deklaracji |
| `DataDokumentu`, `DataWypełnienia` | `Soneta.Types.Date` | daty na dokumencie |
| `KEDU` | `Soneta.Deklaracje.ZUS.KEDU` | zestaw dokumentów ZUS, do którego trafiają wygenerowane bloki |
| `Deklaracje` | `System.Collections.Generic.IList` | wygenerowane deklaracje (do odczytu po akcji) |
| `CzyJestZUA()`, `CzyJestZZA()` | — | rozpoznanie typu zgłoszenia ze schematu ubezpieczeń |

**Schemat ubezpieczeń umowy + zgłoszenie ZUA/ZZA:**

```csharp
var kadry = session.GetKadry();
var pracownik = kadry.Pracownicy.WgKodu["006"];
Umowa umowa = pracownik.Umowy.Cast<Umowa>().First();

// 1) Schemat ubezpieczeń umowy (historyczny) — ZUA: społeczne obowiązkowe + zdrowotne:
using (var t = session.Logout(editMode: true))
{
    var ub = umowa.Last.Ubezpieczenia;                 // UmowaHistoria.Ubezpieczenia
    ub.Tyub4 = kadry.TytulyUbezpiecz4.WgKodu[kodTytulu];  // tytuł zleceniobiorcy (klucz int, ze słownika)
    ub.ObowiazkoweOd = umowa.Okres.From;               // data objęcia społecznymi
    ub.Emerytalne.Obowiazkowe = true;
    ub.Rentowe.Obowiazkowe    = true;
    ub.Zdrowotne.ObowiazkoweOd = umowa.Okres.From;
    // (ZZA = tylko zdrowotne: JAWNIE ustaw Emerytalne.Obowiazkowe = false i Rentowe.Obowiazkowe = false
    //  — domyślnie są true; samo zdrowotne nie wystarcza)
    t.Commit();
}
session.Save();

// 2) Zgłoszenie ZUA/ZZA na podstawie umowy — worker (DataType Umowa):
//    Uwaga: Umowy oraz Pars są SET-ONLY (brak gettera) — ustawiamy je przez inicjalizator,
//    a parametry budujemy jako osobny obiekt ParamsZ(context) i przypisujemy do Pars.
var worker = new Soneta.Deklaracje.ZUS.ZarejestrujUmowyWorker { Umowy = new[] { umowa } };

var pars = new Soneta.Deklaracje.ZUS.ZarejestrujBaseWorker.ParamsZ(context);
pars.Okres           = new FromTo(umowa.Okres.From, Date.MaxValue);
pars.DataDokumentu   = umowa.Okres.From;
pars.DataWypełnienia = Date.Today;
pars.ZarejestrujRodzinę = false;

var rejestracja = new Soneta.Deklaracje.ZUS.ZarejestrujUmowyWorker.Rejestracja { Pars = pars };
rejestracja.ZarejestrujUmowy();     // generuje ZUA lub ZZA wg schematu ubezpieczeń umowy
session.Save();
```

**Wyrejestrowanie po zakończeniu umowy (ZWUA):**

```csharp
var parsW = new Soneta.Deklaracje.ZUS.ZarejestrujBaseWorker.ParamsW(context);
parsW.Okres           = new FromTo(umowa.Okres.To, umowa.Okres.To);
parsW.DataDokumentu   = umowa.Okres.To;
parsW.DataWypełnienia = Date.Today;

var wyrej = new Soneta.Deklaracje.ZUS.ZarejestrujUmowyWorker.Wyrejestrowanie { Pars = parsW };
wyrej.WyrejestrujUmowy();   // generuje ZWUA
session.Save();
```

**Pułapki:**
- **Typ zgłoszenia (ZUA vs ZZA) wynika ze schematu `UmowaHistoria.Ubezpieczenia`**, nie z parametru
  workera — ustaw poprawnie `Tyub4` + flagi `Spoleczne.Obowiazkowe`/`Zdrowotne` **przed** zgłoszeniem.
- `Ubezpieczenia` jest **historyczne** — zmiana schematu „od daty" to nowy zapis `UmowaHistoria`
  (`umowa.Historia.Update(date)` + `UmowaHistorie.AddRow`, jak KADRY-G2/KADRY-A14), nie nadpisywanie bieżącego.
- `Spoleczne.Od` jest **tylko do odczytu** (wyliczane) — datę objęcia społecznymi obowiązkowymi
  ustawiasz zbiorczo przez `Ubezpieczenia.ObowiazkoweOd`; na `Zdrowotne` `ObowiazkoweOd` jest
  zapisywalne wprost (asymetria — jak w KADRY-A7).
- `Tyub4` to rekord **konfiguracyjnego** słownika `TytulyUbezpiecz4`, klucz `WgKodu[int]` — pobierz
  istniejący tytuł zleceniobiorcy, nie twórz „w locie".
- `ZarejestrujUmowyWorker` jest na `Umowa` (umowy), a `ZarejestrujPracownikówWorker` na `Pracownik`
  (etatowi) — do zleceniobiorców używaj wersji „Umowy".
- Workery deklaracji uruchamiaj jak każdy worker enova (Context z tej samej sesji); po akcji wołasz
  `Session.Save()`. Obsłuż `RowConflictException` z `Save()` (safe-code §4).
- `ZarejestrujRodzinę`/`WyrejestrujRodzinę` sterują dołączeniem ZCNA dla członków rodziny
  (`pracownik.Rodzina`, KADRY-A9) — dla zleceniobiorcy zgłoszenie rodziny działa analogicznie do etatu.

