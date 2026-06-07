# KADRY01 — Pracownik — zatrudnienie i dane kartotekowe

> Wspólne fakty o typie, podstawowe typy i szablon wzorca: [../kadry.md](../kadry.md).

> **Model „root + historia".** `Pracownik` (root, tabela `Pracownicy`) trzyma tylko nieliczne pola
> niezmienne w czasie (`Kod`, `Net`, `NumerRachunkuUS`, `NumerRachunkuZUS`, `Rodzaj`, `Typ`,
> `Wieloetatowosc`). **Praktycznie wszystkie dane kadrowe są historyczne** i leżą w zapisach
> `PracHistoria` (tabela `PracHistorie`, child `Pracownik`-a, też `GuidedRow` root z własnym Guid).
> Kolekcją zapisów jest `Pracownik.Historia: HistorySubTable<Soneta.Kadry.PracHistoria>`. Warunki
> zatrudnienia (umowa, wymiar, ubezpieczenia, stanowisko) siedzą w złożonym polu
> `PracHistoria.Etat: Soneta.Kadry.Etat`.
>
> **Skróty na rootcie** (delegują do zapisu „na dziś"/ostatniego):
> - `pracownik.Last : PracHistoria` — **ostatni** (najświehigy) zapis historii; do edycji świeżo
>   utworzonego pracownika i odczytu „bieżących" danych kartotekowych.
> - `pracownik[date] : PracHistoria` — indeksator zwracający zapis **obowiązujący na zadaną datę**.
> - Wiele pól osobowych jest też dostępnych z poziomu rootu jako property delegujące (np. `Imie`,
>   `Nazwisko`, `PESEL`) — ale **kanonicznie ustawiamy je na zapisie** (`Last.Imie`, `pracownik[d].PESEL`).
>
> **`Pracownik` jest klasą abstrakcyjną** — nie da się zrobić `new Pracownik()`. Konkretny typ
> pracownika firmy to **`Soneta.Kadry.PracownikFirmy`**.

### KADRY-A1 — Zatrudnienie nowego pracownika (★)

**Cel:** utworzyć nowego pracownika z minimalnym kompletem danych pozwalającym na `Session.Save()`.

**Mechanizm (kluczowy):** dodanie nowego `PracownikFirmy` do tabeli (`AddRow`) automatycznie tworzy
**pierwszy zapis** `PracHistoria` oraz kalendarz pracownika (dzieje się w `OnAdded`). Dlatego **nie
tworzymy** `PracHistoria` ręcznie przy zatrudnieniu — bezpośrednio po `AddRow` istnieje już
`pracownik.Last` (pierwszy zapis), na którym ustawiamy dane osobowe.

**Pola minimalne do zapisu:**

| Pole | Gdzie | Typ | Uwaga |
|---|---|---|---|
| `Kod` | `Pracownik` (root) | `string` | identyfikator; przy pustym platforma podstawia prefiks + `?` |
| `Imie` | `PracHistoria` (`Last.Imie`) | `string` | wymagane (domyślnie `"?"` z `OnAdded`) |
| `Nazwisko` | `PracHistoria` (`Last.Nazwisko`) | `string` | wymagane (domyślnie `"?"`) |

Pierwszy zapis historii ma okres otwarty (do `Date.MaxValue`); warunki etatu (KADRY-A1 → B) ustawia się
na `Last.Etat` (np. `Etat.Okres`, `Etat.TypUmowy`, `Etat.Wymiar`) — szczegóły w sekcji B.

**Snippet:**

```csharp
var kadry = session.GetKadry();

using (var t = session.Logout(editMode: true))
{
    // AddRow zwraca dodany, typowany wiersz; w OnAdded powstaje pierwszy PracHistoria + kalendarz
    var pracownik = session.AddRow(new PracownikFirmy());

    pracownik.Kod = "555";                 // pole rootu

    // dane osobowe — na ZAPISIE historii (pierwszy zapis = Last)
    pracownik.Last.Nazwisko = "Kowalska";
    pracownik.Last.Imie     = "Gabriela";
    pracownik.Last.PESEL    = "94010812345";

    t.Commit();                            // Commit() w kodzie biznesowym; CommitUI() w workerze/UI
}
session.Save();                            // zapis do bazy; tu wykrywane konflikty/duplikaty
```

**Pułapki:**
- **Nie** rób `new Pracownik()` — typ jest abstrakcyjny. Używaj `new PracownikFirmy()`.
- **Nie** dodawaj ręcznie pierwszego `PracHistoria` — robi to `OnAdded`. Ręczne dodanie zapisu
  dotyczy dopiero *aktualizacji* danych „od daty" (KADRY-A14).
- Dane osobowe ustawiaj na `Last`/`pracownik[date]`, nie próbuj ich „obejść" przez root — root
  deleguje do zapisu, ale zapis jest właściwym miejscem.
- `Kod` bywa polem wymagającym unikalności (zależnie od konfiguracji) — kolizja wybuchnie w
  `Save()` jako `RowException` (z `DuplicateKeyException` w `InnerException`).
- Całość w transakcji (`session.Logout(editMode: true)`); brak `Commit()` = rollback przy `Dispose()`.

### KADRY-A2 — Podstawowe dane kadrowe (★)

**Cel:** uzupełnić dane ewidencyjno-identyfikacyjne pracownika (PESEL, NIP, urodzenie, płeć,
obywatelstwo, rodzice, dokument tożsamości, adresy).

**Gdzie leżą pola — root vs PracHistoria:**

| Dana | Lokalizacja | Pole / typ |
|---|---|---|
| Imię, drugie imię, nazwisko | `PracHistoria` | `Imie`, `ImieDrugie`, `Nazwisko: string` |
| Nazwisko rodowe, imię ojca/matki, nazwisko rodowe matki | `PracHistoria` | `NazwiskoRodowe`, `ImieOjca`, `ImieMatki`, `NazwiskoRodoweMatki: string` |
| PESEL | `PracHistoria` (oraz delegat na root) | `PESEL: string` |
| NIP | `PracHistoria` | `NIP: string` |
| Płeć | `PracHistoria` | `Plec: Soneta.Kadry.PłećOsoby` (`Kobieta`/`Mężczyzna`) |
| Data i miejsce urodzenia | `PracHistoria` (subrow `Urodzony`) | `Urodzony.Data: Date`, `Urodzony.Miejsce: string` |
| Obywatelstwo | `PracHistoria` (subrow `Obywatelstwo`) | `Obywatelstwo.Nazwa: string` (słownik), `Obywatelstwo.KodKraju: string` |
| Dokument tożsamości | `PracHistoria` (subrow `Dokument`) | `Dokument.Rodzaj: KodRodzajuDokumentu`, `Dokument.SeriaNumer: string`, `Dokument.WydanyPrzez`, `Dokument.DataWydania/DataWaznosci: Date` |
| Adres zamieszkania / zameldowania / korespondencji | `PracHistoria` | `Adres`, `AdresZameldowania`, `AdresZamieszkania`, `AdresDoKorespondencji: Soneta.Core.Adres` |
| Urząd skarbowy, koszty/ulga | `PracHistoria` (subrow `Podatki`) | `Podatki.UrzadSkarbowy`, `Podatki.KosztyRodzaj`, `Podatki.UlgaMnoznik`, … |
| Kod, numery rachunków US/ZUS | `Pracownik` (root) | `Kod: string`, `NumerRachunkuUS`, `NumerRachunkuZUS` |

**Pułapki:**
- `Plec` jest **wyliczana z PESEL** przez weryfikator — przy poprawnym PESEL nie musisz jej ustawiać;
  ustawienie ręczne ma sens dla osób bez PESEL. Typ to enum `PłećOsoby`, nie string.
- `Urodzony`, `Obywatelstwo`, `Dokument`, `Podatki` to **subrowy** (pola złożone) — edytujesz ich
  pola (`Last.Urodzony.Data = …`), nie przypisujesz całego obiektu.
- `Adres` (i pozostałe) to property zwracające `Soneta.Core.Adres` — modyfikuj ich pola
  (`Last.Adres.Miejscowosc = …`), nie przypisuj `Last.Adres = …`. `KodPocztowy` jest `int`; do
  wartości z myślnikiem używaj `Adres.KodPocztowyS` (string).
- PESEL/NIP są danymi wrażliwymi — nie loguj ich (safe-code §12).

**Snippet:**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["555"];

using (var t = session.Logout(editMode: true))
{
    var ph = pracownik.Last;               // bieżący (ostatni) zapis kadrowy

    ph.NazwiskoRodowe = "Nowak";
    ph.ImieOjca       = "Jan";
    ph.NIP            = "1234563218";

    ph.Urodzony.Data    = new Date(1994, 1, 8);   // subrow Urodzony
    ph.Urodzony.Miejsce = "Kraków";
    ph.Obywatelstwo.Nazwa = "polskie";            // subrow Obywatelstwo (słownik)

    ph.Adres.Ulica       = "Wadowicka";           // subrow Adres
    ph.Adres.NrDomu      = "8A";
    ph.Adres.KodPocztowyS = "30-415";
    ph.Adres.Miejscowosc = "Kraków";

    t.Commit();
}
session.Save();
```

### KADRY-A7 — Dane ubezpieczenia społecznego i zdrowotnego (★)

**Cel:** ustawić/odczytać tytuł ubezpieczenia, oddział NFZ oraz parametry ubezpieczeń społecznych
(emerytalne, rentowe, chorobowe, wypadkowe) i zdrowotnego — daty zgłoszeń i wyrejestrowań.

**Gdzie leżą pola:**

| Dana | Lokalizacja | Pole / typ |
|---|---|---|
| Tytuł ubezpieczenia (kod) | `PracHistoria.Etat.Ubezpieczenia` | `Tyub4: Soneta.Kadry.TytulUbezpieczenia4` (rekord słownika, tytuł 6-znakowy) |
| Zbiorczy stan ubezpieczeń | `PracHistoria.Etat` | `Etat.Ubezpieczenia: Soneta.Kadry.Ubezpieczenia` (subrow) |
| Społeczne (poszczególne) | `…Etat.Ubezpieczenia.*` | `Emerytalne`, `Rentowe`, `Chorobowe`, `Wypadkowe : Soneta.Kadry.Spoleczne` |
| Zdrowotne | `…Etat.Ubezpieczenia.Zdrowotne` | `Soneta.Kadry.Zdrowotne` (subrow) |
| Data objęcia ubezpieczeniami społ. (od) | `…Etat.Ubezpieczenia` (zbiorczo) | `ObowiazkoweOd: Date` — **zapisywalne** na zbiorczym subrowie `Ubezpieczenia` |
| Objęcie poszczególnym społ. | `…Ubezpieczenia.Emerytalne` itd. | `Obowiazkowe: bool`, `Dobrowolne: bool`, `DobrowolneOd: Date`, `Do: Date` (wyrej.) — **zapisywalne**; `Od: Date` jest tylko do odczytu (wyliczane) |
| Data objęcia zdrowotnym | `…Ubezpieczenia.Zdrowotne` | `ObowiazkoweOd: Date` — **zapisywalne** (asymetria względem `Spoleczne`) |
| Przyczyna wyrejestrowania | `…Ubezpieczenia.Emerytalne.Przyczyna` | `Przyczyna: Soneta.Kadry.Wyrejestrowanie` (subrow z kodem) |
| Oddział NFZ | `PracHistoria.OddzialNFZ` | `OddzialNFZ: Soneta.Kadry.OddzialNFZ` (subrow; `Oddział`, `KodGminy`, `OdDnia`) |
| Tytuł na dzień (odczyt) | `PracHistoria.Etat.Ubezpieczenia` | `WyliczTyubNaDzień(Date)` |

**Pułapki:**
- Cała struktura ubezpieczeń jest **historyczna** (siedzi w `Etat` danego `PracHistoria`) — zmiana
  „od daty" wymaga nowego zapisu historii (KADRY-A14), nie nadpisywania bieżącego.
- `Tyub4` to rekord **konfiguracyjnego** słownika `TytulUbezpieczenia4` — pobierz istniejący wpis
  przez `session.GetKadry().TytulyUbezpiecz4.WgKodu[kod]`, gdzie **`kod` jest typu `int`** (np. `110`,
  `2241`), nie twórz „w locie". (Pole `Tyub`/`TypUbezpieczenia` to starsze typy — używaj `Tyub4`.)
- `OddzialNFZ` to subrow z polem `Oddział` (enum oddziałów) — ustawiasz `OddzialNFZ.Oddział`, nie
  całą strukturę.
- `Emerytalne`/`Rentowe`/`Chorobowe`/`Wypadkowe` to subrowy `Spoleczne`. Ustawiasz na nich flagi
  `Obowiazkowe`/`Dobrowolne` oraz `DobrowolneOd`/`Do`. **`Od` jest tylko do odczytu** (wyliczane) —
  datę objęcia ubezpieczeniami obowiązkowymi ustawiasz **zbiorczo** przez `Ubezpieczenia.ObowiazkoweOd`.
  Na subrowie `Zdrowotne` z kolei `ObowiazkoweOd` jest zapisywalne bezpośrednio.

**Snippet:**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["555"];

// Odczyt tytułu ubezpieczenia obowiązującego na dziś:
var data = Date.Today;
TytulUbezpieczenia tyubNaDzis = pracownik[data].Etat.Ubezpieczenia.WyliczTyubNaDzień(data);

using (var t = session.Logout(editMode: true))
{
    var ub = pracownik.Last.Etat.Ubezpieczenia;       // subrow ubezpieczeń bieżącego zapisu

    // Tytuł ubezpieczenia (rekord słownika konfiguracyjnego); klucz WgKodu jest po Kod: int:
    ub.Tyub4 = session.GetKadry().TytulyUbezpiecz4.WgKodu[110];   // np. 0110 = pracownik

    // Data objęcia ubezpieczeniami społecznymi obowiązkowymi — ZBIORCZO (Od na Spoleczne jest read-only):
    ub.ObowiazkoweOd = new Date(2026, 1, 1);
    ub.Emerytalne.Obowiazkowe = true;
    ub.Rentowe.Obowiazkowe    = true;
    ub.Chorobowe.Obowiazkowe  = true;

    // Ubezpieczenie zdrowotne — datę objęcia ustawiasz wprost na subrowie Zdrowotne:
    ub.Zdrowotne.ObowiazkoweOd = new Date(2026, 1, 1);

    // Oddział NFZ (subrow):
    pracownik.Last.OddzialNFZ.OdDnia = new Date(2026, 1, 1);

    t.Commit();
}
session.Save();
```

### KADRY-A9 — Dane o rodzinie pracownika (★)

**Cel:** ewidencjonować członków rodziny i zgłaszać ich do ubezpieczenia zdrowotnego (ZCNA).

**Kolekcja i typ:** `Pracownik.Rodzina: SubTable<Soneta.Kadry.CzlonekRodziny>` (tabela `Rodzina`,
`GuidedRow` root, child `Pracownik`-a). Nowy członek rodziny tworzony jest konstruktorem
`new CzlonekRodziny(pracownik)`.

**Pola i typy (`CzlonekRodziny`):**

| Pole | Typ | Opis |
|---|---|---|
| `Nazwisko`, `Imie`, `ImieDrugie` | `string` | dane osobowe (wymagane: `Nazwisko`, `Imie`) |
| `PESEL`, `NIP` | `string` | identyfikatory |
| `Urodzony` | `Soneta.Kadry.Urodzony` (subrow) | `Urodzony.Data: Date`, `Urodzony.Miejsce: string` |
| `Dokument` | `Soneta.Kadry.DokumentOsoby` (subrow) | dokument tożsamości |
| `StPokrewienstwa` | `Soneta.Kadry.KodStPokrewienstwa` (enum) | stopień pokrewieństwa |
| `Ubezpieczony` | `bool` | zgłoszony do ubezpieczenia zdrowotnego (ZCNA) |
| `UbezpieczenieOkres` | `Soneta.Types.FromTo` | okres zgłoszenia do ubezpieczenia |
| `StNiepelnosprawnosci` | `Soneta.Kadry.KodStNiepelnosprawnosci` (enum) | stopień niepełnosprawności |
| `WspolneGospDomowe`, `NaUtrzymaniu`, `OdbKsztalcenie` | `bool` | przesłanki zgłoszenia |
| `Adres` | `Soneta.Core.Adres` | adres (gdy inny niż pracownika) |
| `Pracownik` | `Soneta.Kadry.Pracownik` | właściciel (ustawiany ctorem) |

**Pułapki:**
- Zgłoszenie do ubezpieczenia zdrowotnego (ZCNA) realizuje się przez `Ubezpieczony = true` +
  `UbezpieczenieOkres` + `StPokrewienstwa` — to z tych pól generowana jest deklaracja ZCNA. Brak
  dedykowanego „pola daty wysyłki ZCNA" na członku rodziny.
- `CzlonekRodziny` nie jest historyczny — to płaski child pracownika; okres ubezpieczenia trzyma
  pole `UbezpieczenieOkres: FromTo`.
- Konstruktor `new CzlonekRodziny(pracownik)` od razu wiąże rekord z pracownikiem; pojawia się on
  w `pracownik.Rodzina`.

**Snippet:**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["555"];

using (var t = session.Logout(editMode: true))
{
    var dziecko = new CzlonekRodziny(pracownik);   // ctor wiąże z pracownikiem
    session.AddRow(dziecko);
    dziecko.Nazwisko = "Kowalska";
    dziecko.Imie     = "Zofia";
    dziecko.PESEL    = "20290512345";
    dziecko.Urodzony.Data = new Date(2020, 9, 5);
    dziecko.StPokrewienstwa = KodStPokrewienstwa.Dziecko;   // wartość enum wg słownika

    // Zgłoszenie do ubezpieczenia zdrowotnego (ZCNA):
    dziecko.Ubezpieczony      = true;
    dziecko.UbezpieczenieOkres = new FromTo(new Date(2026, 1, 1), Date.MaxValue);
    dziecko.NaUtrzymaniu       = true;

    t.Commit();
}
session.Save();

// Odczyt aktualnie ubezpieczonych członków rodziny — filtr serwerowy po kolekcji:
foreach (CzlonekRodziny cr in pracownik.Rodzina[(CzlonekRodziny c) => c.Ubezpieczony])
{
    // cr.Nazwisko, cr.Imie, cr.StPokrewienstwa
}
```

### KADRY-A10 — Poprzednie miejsca pracy (★)

**Cel:** rejestrować historię zatrudnienia u poprzednich pracodawców i okresy nauki (do wyliczenia
stażu pracy i uprawnień urlopowych).

**Kolekcja i typ:** `Pracownik.HistoriaZatrudnienia: SubTable<Soneta.Kadry.HistoriaZatrudnieniaBase>`
(tabela `HistZatrudnien`, `GuidedRow` root, child `Pracownik`-a). To **inna kolekcja niż
`Pracownik.Historia`** — `Historia` to historia *bieżącego* zatrudnienia (zapisy `PracHistoria`),
a `HistoriaZatrudnienia` to staż u *poprzednich* pracodawców.

`HistoriaZatrudnieniaBase` jest typem bazowym z **konstruktorem `protected`** — nie da się go
utworzyć bezpośrednio. Konkretne typy do tworzenia wpisów to:
- `Soneta.Kadry.HistoriaZatrudnienia` — poprzedni pracodawca (ustawia `Typ = Zatrudnienie`),
- `Soneta.Kadry.UkonczonaSzkola` — okres nauki.
Oba mają publiczny ctor `(Pracownik)`.

**Pola i typy (`HistoriaZatrudnieniaBase`):**

| Pole | Typ | Opis |
|---|---|---|
| `Typ` | `Soneta.Kadry.TypHistoriiZatrudnienia` | rodzaj wpisu (praca / nauka) — ustawiany przez ctor konkretnej klasy, readonly |
| `Nazwa` | `string` | nazwa zakładu pracy / szkoły (wymagane) |
| `Okres` | `Soneta.Types.FromTo` | okres zatrudnienia/nauki |
| `EfektywnyOkres` | `Soneta.Types.FromTo` | okres efektywnie wliczany do stażu |
| `Adres1`, `Adres2` | `string` | adres pracodawcy |
| `Korekta` | `Soneta.Kadry.StazPracy` | ręczna korekta naliczonego stażu |
| `Staz` | `Soneta.Kadry.StazPracyPracownika` | wyliczony staż (kalkulowane) |
| `RodzajDokumentu` | `Soneta.Kadry.RodzajDokumentu` | dokument potwierdzający |
| `Pracownik` | `Soneta.Kadry.Pracownik` | właściciel (relacja, readonly) |

**Pułapki:**
- **Nie** rób `new HistoriaZatrudnieniaBase(...)` — ctor jest `protected`. Twórz konkretny typ:
  `new HistoriaZatrudnienia(pracownik)` (praca u poprzedniego pracodawcy, `Typ = Zatrudnienie`) albo
  `new UkonczonaSzkola(pracownik)` (nauka).
- `EfektywnyOkres` ⊆ `Okres` — to on (a nie sam `Okres`) decyduje o wliczeniu do stażu; jeśli go nie
  ustawisz, obowiązują weryfikatory ciągłości.
- Wpisy są niezależne od `PracHistoria` — nie myl `HistoriaZatrudnienia` (poprzedni pracodawcy)
  z `Historia` (zapisy bieżącego zatrudnienia).

**Snippet:**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["555"];

using (var t = session.Logout(editMode: true))
{
    // konkretny typ: poprzedni pracodawca (Typ = Zatrudnienie ustawia ctor); AddRow zwraca typowany wiersz
    var hz = session.AddRow(new HistoriaZatrudnienia(pracownik));
    hz.Nazwa  = "Poprzednia Firma Sp. z o.o.";
    hz.Okres  = new FromTo(new Date(2018, 3, 1), new Date(2025, 12, 31));
    hz.EfektywnyOkres = hz.Okres;
    hz.Adres1 = "ul. Główna 1, Kraków";
    t.Commit();
}
session.Save();

// Odczyt historii zatrudnienia (wszystkie typy wpisów, bazowy typ kolekcji):
foreach (HistoriaZatrudnieniaBase hz in pracownik.HistoriaZatrudnienia)
{
    // hz.Nazwa, hz.Okres, hz.EfektywnyOkres, hz.Typ
}
```

### KADRY-A14 — Aktualizacja danych historycznych: zmiana „od daty" vs korekta (★)

**Cel:** poprawnie zmienić dane kadrowe — **nowy zapis obowiązujący od wskazanego dnia** (zmiana
warunków: podwyżka, zmiana wymiaru etatu, zmiana danych podatkowych) **kontra korekta istniejącego
zapisu** (poprawa błędu w obecnym okresie). Plus: odczyt zapisu obowiązującego „na dzień".

**Mechanizm `HistorySubTable<PracHistoria>`:**

| Operacja | API | Efekt |
|---|---|---|
| Odczyt zapisu na dzień | `pracownik[date]` (== `(PracHistoria)Historia[date]`) | zwraca zapis, którego `Aktualnosc` zawiera `date` |
| Pierwszy zapis | `pracownik.Historia.GetFirst()` | najstarszy zapis |
| Ostatni zapis | `pracownik.Last` (== `Historia.GetPrev()`) | najświeższy zapis |
| **Nowy zapis „od daty"** | `(PracHistoria)pracownik.Historia.Update(date)` | **klonuje** zapis aktualny na `date`, skraca jego okres do `date-1`, zwraca **nowy** klon z okresem od `date`; nowy klon trzeba dodać do tabeli |
| Okres obowiązywania | `PracHistoria.Aktualnosc: Soneta.Types.FromTo` | „od–do" zapisu (zarządzane przez historię) |

**Wzorzec aktualizacji „od daty" (zmiana warunków od dnia):**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["555"];
var odDnia = new Date(2026, 7, 1);

using (var t = session.Logout(editMode: true))
{
    // 1) Update klonuje zapis aktualny na `odDnia` i zwraca nowy klon (okres od `odDnia`).
    //    Stary zapis zostaje skrócony do dnia poprzedniego.
    var nowy = (PracHistoria)pracownik.Historia.Update(odDnia);

    // 2) Klon trzeba dodać do tabeli zapisów historii.
    pracownik.Module.PracHistorie.AddRow(nowy);

    // 3) Na nowym zapisie wprowadzamy zmienione warunki (od `odDnia`):
    nowy.Etat.MiejscePracy = "Oddział Kraków";   // np. zmiana miejsca pracy
    nowy.Podatki.UlgaMnoznik = 1m;               // np. zmiana danych podatkowych

    // Uwaga: część pól Etat (np. Etat.Zaszeregowanie.Wymiar) na świeżym klonie potrafi być
    // w trybie tylko-do-odczytu (ColReadOnlyException) — odblokowanie zależy od konfiguracji etatu
    // (patrz pułapki w sekcji KADRY-B1: bramką jest Etat.Okres). Dla pewności w przykładzie zmieniamy
    // pola bezpiecznie zapisywalne (MiejscePracy, dane podatkowe).

    t.Commit();
}
session.Save();
```

**Wzorzec korekty istniejącego zapisu (bez nowego okresu):**

```csharp
using (var t = session.Logout(editMode: true))
{
    // Modyfikujemy zapis obowiązujący na zadaną datę — bez Update, bez AddRow.
    var ph = pracownik[new Date(2026, 3, 15)];
    ph.NazwiskoRodowe = "PoprawioneNazwisko";   // korekta w istniejącym okresie
    t.Commit();
}
session.Save();
```

**Pułapki:**
- **`Update(date)` + `AddRow(nowy)` to nierozłączna para.** Sam `Update` tworzy „odpięty" klon —
  bez `PracHistorie.AddRow(nowy)` zmiana nie zostanie zapisana.
- `Update(date)` rzuca `HistorySubTable.DateDuplicateException`, gdy na `date` już zaczyna się zapis
  (`Aktualnosc.From == date`) — nie da się „aktualizować" dwa razy tego samego dnia; wtedy
  modyfikuj istniejący zapis (`pracownik[date]`).
- **Korekta** (modyfikacja `pracownik[date]`) zmienia dane w **całym** okresie tego zapisu — używaj
  jej tylko do poprawy błędu, nie do „zmiany od dnia".
- `Aktualnosc` (okres zapisu) jest zarządzany przez mechanizm historii — **nie ustawiaj go ręcznie**;
  do skrócenia/wstawienia okresu służy `Update`.
- Odczyt „na dzień": `pracownik[date]` zwraca `null`, jeśli dla daty brak zapisu — dla daty sprzed
  zatrudnienia. `pracownik.Last` zawsze zwraca najświeższy zapis.
- Aktualizacja danych to operacja na danych operacyjnych pracownika — trzymaj transakcje krótkie
  (safe-code §13.1) i obsłuż `RowConflictException` z `Save()` (safe-code §4).

### KADRY-A3 — Adresy (zameldowania / zamieszkania / korespondencyjny)

**Cel:** uzupełnić/odczytać adresy pracownika. Adresy są **historyczne** — leżą na zapisie
`PracHistoria`, dostęp przez `pracownik.Last` (bieżący) lub `pracownik[date]` (na dzień). Każdy adres
to subrow typu `Soneta.Core.Adres` — modyfikujesz jego pola, nie przypisujesz całego obiektu.

**Gdzie leżą pola — `PracHistoria` (subrowy `Soneta.Core.Adres`):**

| Dana | Pole / typ | Uwaga |
|---|---|---|
| Adres podstawowy (kanoniczny) | `Adres: Soneta.Core.Adres` | adres główny pracownika |
| Adres zameldowania | `AdresZameldowania: Soneta.Core.Adres` | |
| Adres zamieszkania | `AdresZamieszkania: Soneta.Core.Adres` | |
| Adres do korespondencji | `AdresDoKorespondencji: Soneta.Core.Adres` | |
| Adres na przelewach | `AdresNaPrzelewach: Soneta.Kadry.AdresPracownikaNaPrzelewach` | osobny typ — adres umieszczany na przelewach |

**Pola subrowa `Soneta.Core.Adres` (zapisywalne, `bazodanowe`):**

| Pole | Typ | Opis |
|---|---|---|
| `Miejscowosc` | `string` | miejscowość |
| `Ulica` | `string` | nazwa ulicy/alei/osiedla |
| `NrDomu` | `string` | numer domu/bloku |
| `NrLokalu` | `string` | numer lokalu |
| `KodPocztowy` | `int` | kod pocztowy (liczbowo) |
| `KodPocztowyS` | `string` | kod pocztowy z myślnikiem (np. `"30-415"`) |
| `Poczta` | `string` | poczta |
| `Gmina`, `Powiat` | `string` | gmina, powiat |
| `Wojewodztwo` | `Soneta.Core.Wojewodztwa` (enum) | województwo |
| `Kraj`, `KodKraju` | `string` | kraj / kod kraju |
| `ZagranicznyKodPocztowy` | `string` | zagraniczny kod pocztowy |
| `Telefon`, `Faks` | `string` | telefon/faks związany z adresem |
| `Pełny` | `string` | sformatowany adres (tylko odczyt) |

**Pułapki:**
- `Adres`, `AdresZameldowania`, … to **subrowy** — modyfikuj pola (`Last.AdresZamieszkania.Ulica = …`),
  **nie** przypisuj całego obiektu (`Last.AdresZamieszkania = …` — błąd).
- `KodPocztowy` to `int`; do wartości z myślnikiem używaj `KodPocztowyS` (string).
- Cała struktura jest historyczna — zmiana adresu „od daty" to nowy zapis historii (KADRY-A14), korekta
  bieżącego okresu to modyfikacja `pracownik[date]`.
- `Wojewodztwo` to enum `Soneta.Core.Wojewodztwa`, nie string.

**Snippet:**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];

using (var t = session.Logout(editMode: true))
{
    var ph = pracownik.Last;

    ph.AdresZamieszkania.Miejscowosc  = "Kraków";
    ph.AdresZamieszkania.Ulica        = "Wadowicka";
    ph.AdresZamieszkania.NrDomu       = "8A";
    ph.AdresZamieszkania.NrLokalu     = "12";
    ph.AdresZamieszkania.KodPocztowyS = "30-415";

    ph.AdresZameldowania.Miejscowosc  = "Wieliczka";
    ph.AdresDoKorespondencji.Miejscowosc = "Kraków";

    t.Commit();
}
session.Save();

// Odczyt adresu na dzień:
Adres adr = pracownik[Date.Today].AdresZamieszkania;
string opis = $"{adr.Ulica} {adr.NrDomu}, {adr.KodPocztowyS} {adr.Miejscowosc}";
```

---

### KADRY-A4 — Dane kontaktowe (e-mail, telefon) i dostęp WWW/Pulpity

**Cel:** ustawić/odczytać dane kontaktowe pracownika (e-mail, telefon komórkowy, WWW). Dane kontaktowe
leżą w subrowie `Kontakt: Soneta.Core.Kontakt` — dostępnym zarówno na rootcie `Pracownik`, jak i na
zapisie historii `PracHistoria` (historyczne). Pracownik dodatkowo udostępnia `EMAIL: string` na rootcie.

**Gdzie leżą pola — subrow `Soneta.Core.Kontakt` (`PracHistoria.Kontakt` / `Pracownik.Kontakt`):**

| Pole | Typ | Opis |
|---|---|---|
| `EMAIL` | `string` | adres poczty elektronicznej |
| `TelefonKomorkowy` | `string` | telefon komórkowy |
| `WWW` | `string` | adres strony internetowej |
| `Skype` | `string` | identyfikator Skype |
| `SkrytkaPocztowa` | `string` | skrytka pocztowa |

**Telefon stacjonarny/faks** — w kontekście adresu: `PracHistoria.Adres.Telefon`, `…Adres.Faks: string`
(patrz KADRY-A3). **Rozbudowane kanały kontaktu** (wiele kontaktów z rodzajem/celem): kolekcja
`PracHistoria.Kontakty: SubTable<Soneta.Core.DaneKontaktowe>` (pola `Kontakt: string`,
`Rodzaj: Soneta.Core.RodzajKontaktu`, `Domyslny: bool`, `Opis: string`).

**Dostęp WWW / Pulpity (IWebOperator):** `Pracownik` implementuje interfejs
`Soneta.…IWebOperator`. Konto dostępu do Pulpitów (operator web, login, uprawnienia) **nie jest
zwykłym zapisywalnym polem** pracownika — jest zarządzane osobnym mechanizmem operatorów/uprawnień
modułu web (poza prostym kontraktem ustawiania pól na pracowniku). W publicznym kontrakcie danych
kartotekowych operujesz danymi kontaktowymi (e-mail/telefon/WWW), a powiązanie operatora web jest
realizowane przez konfigurację operatorów, nie przez `pracownik.Last`.

**Pułapki:**
- `Kontakt` to **subrow** — modyfikuj pola (`Last.Kontakt.EMAIL = …`), nie przypisuj całego obiektu.
- Pole nazywa się `EMAIL` (wielkimi literami) — uwaga na wielkość liter.
- E-mail/telefon w kontekście „na przelewach"/PPK to inne pola (`OdpisPPK.Email`,
  `OdpisPPK.TelefonKomorkowy`) — nie myl z kontaktem osobowym.
- Dostęp do Pulpitu (IWebOperator) nie jest częścią `PracHistoria` — nie szukaj „pola WWW dostępu"
  na zapisie kadrowym.

**Snippet:**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];

using (var t = session.Logout(editMode: true))
{
    var k = pracownik.Last.Kontakt;           // subrow Kontakt bieżącego zapisu
    k.EMAIL            = "g.kowalska@firma.pl";
    k.TelefonKomorkowy = "600100200";
    k.WWW              = "https://firma.pl/g.kowalska";
    t.Commit();
}
session.Save();

// Odczyt:
string mail = pracownik.Last.Kontakt.EMAIL;
```

---

### KADRY-A5 — Rachunki bankowe (rachunek do przelewu wynagrodzenia)

**Cel:** zarejestrować/odczytać rachunki bankowe pracownika oraz wskazać rachunek główny do przelewu
wynagrodzenia.

**Kolekcja i typ:** `Pracownik.Rachunki: SubTable<Soneta.Kasa.RachunekBankowyPodmiotu>` (rachunki są
na rootcie pracownika, nie w historii). Rachunek główny (domyślny) zwraca
`Pracownik.DomyslnyRachunek: Soneta.Kasa.RachunekBankowyPodmiotu`. Numery rachunków US/ZUS pracownika
to osobne pola rootu: `Pracownik.NumerRachunkuUS: Soneta.Core.NumerRachunkuUS`,
`Pracownik.NumerRachunkuZUS: Soneta.Core.NumerRachunkuZUS`.

**Pola i typy (`Soneta.Kasa.RachunekBankowyPodmiotu`):**

| Pole | Typ | Opis |
|---|---|---|
| `Rachunek` | `Soneta.Kasa.RachunekBankowy` (subrow) | właściwy rachunek; `Rachunek.Numer: Soneta.Kasa.NumerRachunku`, `Rachunek.Bank: Soneta.Kasa.IBank` |
| `Rachunek.Numer.Pełny` / `.PełnyNRB` | `string` | pełny numer rachunku (do odczytu/ustawienia) |
| `Domyslne` | `bool` | rachunek domyślny (do odczytu — odpowiada `DomyslnyRachunek`) |
| `Priorytet` | `int` | priorytet rachunku |
| `Procent` | `Soneta.Types.Percent` | udział % (przy podziale wynagrodzenia na rachunki) |
| `Kwota` | `Soneta.Types.Currency` | kwota stała (przy podziale wynagrodzenia) |
| `Nazwa1`, `Nazwa2` | `string` | linie informacji na przelewie |
| `Oddzial` | `Soneta.Core.OddzialFirmy` | oddział |
| `Blokada` | `bool` | blokada rachunku |
| `Podmiot` | `Soneta.Kasa.IPodmiotKasowy` | właściciel (pracownik) |

**Pułapki:**
- `RachunekBankowyPodmiotu` to typ z modułu `Soneta.Kasa` — element kolekcji `pracownik.Rachunki`.
- Rachunek główny do wynagrodzenia odczytujesz przez `pracownik.DomyslnyRachunek` (nie iteruj
  kolekcji szukając `Domyslne == true`, gdy wystarczy property).
- `Rachunek` to **subrow** — numer ustawiasz na `r.Rachunek.Numer` (typ biznesowy `NumerRachunku`),
  nie jako prosty string na poziomie `RachunekBankowyPodmiotu`.
- Numer rachunku to typ biznesowy (`NumerRachunku`/`NumerRachunkuPodmiotu`), z walidacją IBAN/NRB —
  nie traktuj go jak zwykły `string`.

**Snippet (odczyt — bezpieczny, bez zależności od konstruktora rachunku):**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];

// Rachunek główny do przelewu wynagrodzenia:
RachunekBankowyPodmiotu glowny = pracownik.DomyslnyRachunek;
if (glowny != null)
{
    string numer = glowny.Rachunek.Numer.Pełny;
}

// Wszystkie rachunki pracownika:
foreach (RachunekBankowyPodmiotu r in pracownik.Rachunki)
{
    bool czyDomyslny = r.Domyslne;
    int  priorytet   = r.Priorytet;
}
```

---

### KADRY-A6 — Dane podatkowe (PIT)

**Cel:** ustawić/odczytać dane podatkowe pracownika: koszty uzyskania przychodu, ulgę podatkową
(PIT-2), próg/typ progów podatkowych, urząd skarbowy oraz numer rachunku US. Dane są **historyczne** —
subrow `PracHistoria.Podatki`; numer rachunku US to pole rootu pracownika.

**Gdzie leżą pola — subrow `PracHistoria.Podatki` (`pracownik.Last.Podatki`):**

| Dana | Pole / typ | Uwaga |
|---|---|---|
| Rodzaj kosztów uzyskania | `Podatki.KosztyRodzaj: Soneta.Kadry.RodzajKosztowUzyskania` (enum) | podstawowe / podwyższone / brak |
| Mnożnik kosztów | `Podatki.KosztyMnoznik: decimal` | np. 1 / 0 |
| Koszty autorskie 50% | `Podatki.Koszty50Procent: Percent`, `Koszty50Limit: decimal`, `Koszty50NieNaliczajOd: YearMonth`, `Koszty50NieNaliczajOdDnia: Date` | koszty autorskie |
| Ulga podatkowa (mnożnik) | `Podatki.UlgaMnoznik: decimal` | PIT-2: pełna kwota zmniejszająca = mnożnik (np. `1m`), `0` = brak |
| Część ulgi (podział PIT-2) | `Podatki.UlgaCzesc: Soneta.Kadry.UlgaPodatkowaCzesc` (enum) | 1/1, 1/2, 1/3 (podział między płatników) |
| Limit ulgi | `Podatki.UlgaLimit: bool` | |
| Ulga „klasa średnia" / emeryt / duża rodzina / zagranica | `Podatki.UlgaKlasaSrednia`, `UlgaEmeryt`, `UlgaDuzaRodzina`, `UlgaZagranica: bool`; `UlgaZagranicaOd/Do: int` | dodatkowe ulgi |
| Typ progów podatkowych | `Podatki.TypProgow: Soneta.Kadry.TypProgowPodatkowych` (enum) | standardowe / indywidualne |
| Progi podatkowe (indywidualne) | `Podatki.ProgiPodatkowe: SubTable` | gdy `TypProgow` = indywidualne |
| Podwyższona zaliczka (próg) | `Podatki.PodwProg2019: bool` | |
| Naliczanie PIT po 26 r.ż. (ulga dla młodych) | `Podatki.Pit26: Soneta.Kadry.NaliczajPit26` (enum) | „zerowy PIT" dla młodych |
| Rezygnacja z rozp. 07.01.22 | `Podatki.RezygnacjaRozp070122`, `…Umowa: bool` | |
| Kwota wolna przy umowie | `Podatki.UmowaKwotaWolna: bool` | |
| Adres na PIT = zameldowania | `Podatki.NaPITAdresZameldowania: bool` | |
| Urząd skarbowy | `Podatki.UrzadSkarbowy: Soneta.Core.IPodmiotUI` (ref); `UrzadSkarbowyEx: Soneta.CRM.UrzadSkarbowy` | pobierz istniejący US |
| Numer rachunku US (pracownika) | `Pracownik.NumerRachunkuUS: Soneta.Core.NumerRachunkuUS` (root) | `NumerRachunkuUS.Numer/.Pełny` |

**Pułapki:**
- `Podatki` to **subrow** zapisu historii — modyfikuj pola (`Last.Podatki.UlgaMnoznik = …`), nie
  przypisuj całego obiektu; zmiana „od daty" to nowy zapis historii (KADRY-A14).
- PIT-2 (ulga) reprezentowana jest przez `UlgaMnoznik` (pełna/część kwoty zmniejszającej) oraz
  `UlgaCzesc` (podział między płatników) — nie ma jednego pola „PIT2 bool".
- `KosztyRodzaj`, `TypProgow`, `UlgaCzesc`, `Pit26` to **enumy**, nie string.
- `UrzadSkarbowy` to **referencja** do istniejącego podmiotu — nie twórz „w locie".
- `NumerRachunkuUS` jest na **rootcie** pracownika, nie w `Podatki`.

**Snippet:**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];

using (var t = session.Logout(editMode: true))
{
    var p = pracownik.Last.Podatki;
    p.KosztyRodzaj = RodzajKosztowUzyskania.JedenStosPracy; // JedenStosPracy/JedenStos25/WiecejStosPracy/WiecejStos25
    p.UlgaMnoznik  = 1m;                                   // pełna kwota zmniejszająca (PIT-2)
    p.UlgaCzesc    = UlgaPodatkowaCzesc.Ulga112;           // podział PIT-2: Ulga112/Ulga124/Ulga136
    p.TypProgow    = TypProgowPodatkowych.Standardowe;     // enum
    t.Commit();
}
session.Save();

// Odczyt:
decimal mnoznikUlgi = pracownik.Last.Podatki.UlgaMnoznik;
RodzajKosztowUzyskania koszty = pracownik.Last.Podatki.KosztyRodzaj;
```

---

### KADRY-A8 — Pozostałe dane ubezpieczeniowe / informacje ZUS (oddział, kod)

**Cel:** ustawić/odczytać dodatkowe dane ZUS pracownika — oddział ZUS oraz dodatkowe świadczenia ZUS
(emerytury/renty z dodatkowymi świadczeniami). Dane są **historyczne** — subrow
`PracHistoria.DodSwiadczeniaZUS`. (Tytuł ubezpieczenia i parametry ubezpieczeń społ./zdrow. opisuje KADRY-A7.)

**Gdzie leżą pola — subrow `PracHistoria.DodSwiadczeniaZUS: Soneta.Kadry.DodatkoweŚwiadczeniaZUS`:**

| Pole | Typ | Opis |
|---|---|---|
| `OddzialZUS` | `Soneta.CRM.OddziałZUS` (ref) | oddział ZUS (referencja do podmiotu/słownika ZUS) |
| `Rodzaj` | `Soneta.Kadry.RodzajeDodatkowychŚwiadczeńZUS` (enum) | rodzaj dodatkowego świadczenia ZUS |
| `Okres` | `Soneta.Types.FromTo` | okres obowiązywania świadczenia |
| `Numer` | `string` | numer (decyzji/świadczenia) |

**Oddział NFZ (komplementarny do ZUS):** `PracHistoria.OddzialNFZ: Soneta.Kadry.OddzialNFZ` —
pola `OddzialNFZ.Oddział: OddziałNFZ` (enum oddziałów), `OddzialNFZ.KodGminy: string`,
`OddzialNFZ.OdDnia: Date` (patrz też KADRY-A7).

**Pułapki:**
- `DodSwiadczeniaZUS` to **subrow** — modyfikuj pola (`Last.DodSwiadczeniaZUS.OddzialZUS = …`).
- `OddzialZUS` to **referencja** (`Soneta.CRM.OddziałZUS`) do istniejącego rekordu — pobierz
  istniejący, nie twórz „w locie".
- `Rodzaj` to **enum** `RodzajeDodatkowychŚwiadczeńZUS`, nie string.
- **Cały subrow `DodSwiadczeniaZUS` bywa tylko-do-odczytu** na świeżym zapisie (rzuca
  `ColReadOnlyException` nawet dla `Numer`) — pola te aktywuje dopiero zainicjowanie świadczenia
  w kreatorze/UI. Zapisywalne wprost jest `OddzialNFZ.OdDnia`.
- Zmiana „od daty" to nowy zapis historii (KADRY-A14).

**Snippet:**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];

// Odczyt dodatkowych świadczeń ZUS i oddziału NFZ na dziś:
var ph = pracownik.Last;
RodzajeDodatkowychŚwiadczeńZUS rodzaj = ph.DodSwiadczeniaZUS.Rodzaj;
FromTo okres = ph.DodSwiadczeniaZUS.Okres;
var oddzialNfz = ph.OddzialNFZ.Oddział;     // enum oddziałów NFZ

using (var t = session.Logout(editMode: true))
{
    // Zapisywalne wprost na świeżym zapisie: data objęcia oddziałem NFZ.
    ph.OddzialNFZ.OdDnia = new Date(2026, 1, 1);
    t.Commit();
}
session.Save();
```

---

### KADRY-A11 — Wykształcenie, znajomość języków obcych, służba wojskowa

**Cel:** ewidencjonować wykształcenie, znane języki obce oraz dane służby wojskowej pracownika.
Wykształcenie i wojsko to subrowy zapisu historii (`PracHistoria`); języki obce to kolekcja na rootcie
pracownika.

**Wykształcenie — subrow `PracHistoria.Wyksztalcenie: Soneta.Kadry.Wyksztalcenie`:**

| Pole | Typ | Opis |
|---|---|---|
| `Kod` | `Soneta.Kadry.KodWyksztalcenia` (enum) | poziom/rodzaj wykształcenia |
| `StopienNaukowy` | `string` | stopień naukowy |
| `TytulNaukowy` | `string` | tytuł naukowy |

(Kod wykształcenia GUS jest osobno — patrz KADRY-A12: `PracHistoria.GUS.KodWyksztalcenia`.)

**Języki obce — kolekcja `Pracownik.JęzykiObce: SubTable<Soneta.Kadry.ZnajomośćJęzykaObcego>`:**

| Pole | Typ | Opis |
|---|---|---|
| `Jezyk` | `Soneta.Kadry.DefinicjaJęzykaObcego` (ref słownik) | język |
| `Mowa` | `Soneta.Kadry.DefinicjaStopiaZnajomościJęzykaObcego` (ref) | stopień znajomości w mowie |
| `Pismo` | `Soneta.Kadry.DefinicjaStopiaZnajomościJęzykaObcego` (ref) | stopień znajomości w piśmie |
| `Zaswiadczenie` | `string` | nr/opis zaświadczenia |
| `DataWydaniaZaswiadczenia` | `Soneta.Types.Date` | data wydania zaświadczenia |
| `Uwagi` | `Soneta.Business.MemoText` | uwagi |

**Służba wojskowa — subrow `PracHistoria.Wojsko: Soneta.Kadry.Wojsko`:**

| Pole | Typ | Opis |
|---|---|---|
| `Stosunek` | `Soneta.Kadry.KodStosDoSluzbyWojskowej` (enum) | stosunek do służby wojskowej |
| `KategoriaZdrowia` | `Soneta.Kadry.KategoriaZdrowia` (enum) | kategoria zdrowia (A, B, …) |
| `Stopien` | `string` | stopień wojskowy |
| `NrKsiazeczki` | `string` | nr książeczki wojskowej |
| `NrSpecjalnosci` | `string` | nr specjalności wojskowej |
| `WKU` | `string` | właściwa WKU |
| `PrzydzialMobilizacyjny` | `string` | przydział mobilizacyjny |
| `Podlega` | `bool` | czy podlega obowiązkowi (odczyt) |

**Pułapki:**
- `Wyksztalcenie` i `Wojsko` to **subrowy** `PracHistoria` (historyczne) — modyfikuj pola, zmiana
  „od daty" przez KADRY-A14. `JęzykiObce` to **kolekcja na rootcie** pracownika (nie historyczna).
- `Jezyk`, `Mowa`, `Pismo` to **referencje** do rekordów słownika (`DefinicjaJęzykaObcego`,
  `DefinicjaStopiaZnajomościJęzykaObcego`) — pobierz istniejące, nie twórz „w locie".
- `Kod` (wykształcenie), `Stosunek`, `KategoriaZdrowia` to **enumy**, nie string.

**Snippet:**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];

using (var t = session.Logout(editMode: true))
{
    // Wykształcenie (subrow historii):
    pracownik.Last.Wyksztalcenie.Kod          = KodWyksztalcenia.Wyzsze;     // enum
    pracownik.Last.Wyksztalcenie.TytulNaukowy = "mgr inż.";

    // Służba wojskowa (subrow historii):
    pracownik.Last.Wojsko.Stosunek        = KodStosDoSluzbyWojskowej.Rezerwa;  // NieDotyczy/NiePodlega/Przedpoborowy/Poborowy/Rezerwa/Inne
    pracownik.Last.Wojsko.KategoriaZdrowia = KategoriaZdrowia.A;
    pracownik.Last.Wojsko.NrKsiazeczki    = "AB123456";

    t.Commit();
}
session.Save();

// Odczyt znajomości języków obcych (kolekcja na rootcie):
foreach (ZnajomośćJęzykaObcego j in pracownik.JęzykiObce)
{
    var jezyk = j.Jezyk;     // DefinicjaJęzykaObcego
    var mowa  = j.Mowa;      // DefinicjaStopiaZnajomościJęzykaObcego
}
```

---

### KADRY-A12 — Dane statystyczne GUS, kod zawodu GUS

**Cel:** ustawić/odczytać dane statystyczne GUS pracownika (kod wykształcenia GUS, rodzaj zatrudnienia
GUS, przesłanki statystyczne) oraz kod wykonywanego zawodu. Dane są **historyczne** — subrow
`PracHistoria.GUS`; kod zawodu siedzi w `Etat`.

**Dane statystyczne — subrow `PracHistoria.GUS: Soneta.Kadry.StatystykaGUS`:**

| Pole | Typ | Opis |
|---|---|---|
| `KodWyksztalcenia` | `Soneta.Kadry.KodWykształceniaGUS` (enum) | kod wykształcenia wg GUS |
| `RodzajZatrudnienia` | `Soneta.Kadry.RodzajZatrudnieniaGUS` (enum) | rodzaj zatrudnienia wg GUS |
| `PopMiejsceZatrudnienia` | `Soneta.Kadry.PopMiejsceZatrudnienia` (enum) | poprzednie miejsce zatrudnienia |
| `GlowneMiejscePracy` | `bool` | główne miejsce pracy |
| `PierwszaPraca` | `bool` | pierwsza praca |
| `PracaWNocy` | `bool` | praca w nocy |
| `StRobotnicze` | `bool` | stanowisko robotnicze |
| `SezonowyDorywczy` | `bool` | zatrudnienie sezonowe/dorywcze |
| `PraceInterwencyjne` | `bool` | prace interwencyjne |

**Kod wykonywanego zawodu — `PracHistoria.Etat`:**

| Pole | Typ | Opis |
|---|---|---|
| `Etat.KodWykonywanegoZawodu` | `int` | kod zawodu GUS (liczbowo) |
| `Etat.KodWykonywanegoZawoduLnk` | `Soneta.Kadry.KodWykonywanegoZawodu` (ref/odczyt) | dowiązany rekord słownika kodu zawodu |

**Pułapki:**
- `GUS` to **subrow** `PracHistoria` (historyczny) — modyfikuj pola; zmiana „od daty" przez KADRY-A14.
- `KodWyksztalcenia` (GUS, enum `KodWykształceniaGUS`) to **inne pole** niż KADRY-A11
  `Wyksztalcenie.Kod` (enum `KodWyksztalcenia`) — nie myl ich.
- `Etat.KodWykonywanegoZawodu` to `int`; `…Lnk` to dowiązanie do słownika (kanonicznie ustawiasz
  kod liczbowo, dowiązanie jest pochodne).
- Pola GUS to enumy/bool, nie string.

**Snippet:**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];

using (var t = session.Logout(editMode: true))
{
    var ph = pracownik.Last;
    ph.GUS.KodWyksztalcenia   = KodWykształceniaGUS.Wyższe;        // enum GUS (uwaga na diakrytyk)
    ph.GUS.GlowneMiejscePracy = true;
    ph.GUS.PierwszaPraca      = false;

    ph.Etat.KodWykonywanegoZawodu = 251401;    // kod zawodu GUS (int)
    t.Commit();
}
session.Save();

// Odczyt:
KodWykształceniaGUS kw = pracownik.Last.GUS.KodWyksztalcenia;
int kodZawodu = pracownik.Last.Etat.KodWykonywanegoZawodu;
```

---

### KADRY-A13 — PFRON i niepełnosprawność / schorzenia

**Cel:** ewidencjonować dane o niepełnosprawności (stopień, orzeczenie, okresy) oraz dane PFRON
(dofinansowania, schorzenia szczególne). Dane są **historyczne** — subrow `PracHistoria.PFRON`.

**Gdzie leżą pola — subrow `PracHistoria.PFRON: Soneta.Kadry.DanePFRON`:**

| Dana | Pole / typ | Uwaga |
|---|---|---|
| Stopień niepełnosprawności | `PFRON.Stopien: Soneta.Kadry.StNiepełnosprawności` (enum) | stopień orzeczony |
| Stopień wg PFRON | `PFRON.StopienPFRON: Soneta.Kadry.KodStNiepelnosprawnosciPFRON` (enum) | klasyfikacja PFRON |
| Okres orzeczenia/uprawnień | `PFRON.Okres: Soneta.Types.FromTo` | okres niepełnosprawności |
| Data orzeczenia | `PFRON.DataOrzeczenia: Soneta.Types.Date` | |
| Data dostarczenia orzeczenia | `PFRON.DataDostarczenia: Soneta.Types.Date` | |
| Data wniosku / zaświadczenia | `PFRON.DataWniosku`, `PFRON.DataZaswiadczenia: Date` | |
| Data zgłoszenia do ewidencji PFRON | `PFRON.DataZgloszeniaDoEwidencji: Date` | |
| Organ wydający orzeczenie | `PFRON.OrganWydajacyOrzeczenie: Soneta.Kadry.OrganWydajacyOrzeczenie` (enum) | |
| Schorzenie szczególne (flaga) | `PFRON.SzczegolneSchorzenie: bool`, `PFRON.SzczegolneSchorzeniePFRON: bool` | |
| Typ schorzenia | `PFRON.TypSchorzenia: Soneta.Kadry.SzczegolneSchorzenia` (enum) | rodzaj schorzenia |
| Schorzenia SOD (1–4) | `PFRON.TypSchorzeniaSOD`, `…2SOD`, `…3SOD`, `…4SOD: Soneta.Kadry.SzczególneSchorzeniaSOD` (enum) | schorzenia dla dofinansowania SOD |
| Lista schorzeń SOD (odczyt) | `PFRON.SchorzeniaSOD: IEnumerable<SzczególneSchorzeniaSOD>` | wyliczane |
| Efekt zachęty | `PFRON.EfektZachety: bool` | warunek dofinansowania |
| Pomoc publiczna | `PFRON.PomocPubliczna: Soneta.Kadry.StanowiPomocPubliczną` (enum) | |
| Dofinansowanie dodatkowe SOD | `PFRON.DodatkoweDofinansowanieSOD: bool` | |
| Urlop dodatkowy (niepełnospr.) | `PFRON.NaliczajUrlopDodatkowy: bool`, `…Od: Date` | |
| Wymiar urlopu podstawowego | `PFRON.WymiarUPodstawowego: Soneta.Types.Fraction` | |
| Wiek emerytalny od | `PFRON.WiekEmerytalnyOd: Date` | |
| Zgoda na przekazanie danych | `PFRON.ZgodaNaPrzekazanieDanych: bool` | |

(Zgody na pracę powyżej norm dla osób niepełnosprawnych są na `Etat`:
`Etat.PracownikNiepelnosprawnyZgodaNaPrace8h`, `…ZgodaNaPraceNadgodziny`,
`…ZgodaNaPraceWPorzeNocnej: bool`.)

**Pułapki:**
- `PFRON` to **subrow** `PracHistoria` (historyczny) — modyfikuj pola; zmiana „od daty" przez KADRY-A14.
- `Stopien`, `StopienPFRON`, `TypSchorzenia`, `…SOD`, `OrganWydajacyOrzeczenie`, `PomocPubliczna`
  to **enumy**, nie string.
- `SchorzeniaSOD` (lista) jest **wyliczana** — schorzenia ustawiasz przez pola `TypSchorzeniaSOD…4SOD`.
- Daty to `Soneta.Types.Date`, okres to `FromTo` — nie `DateTime`.

**Snippet:**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];

using (var t = session.Logout(editMode: true))
{
    var pfron = pracownik.Last.PFRON;
    pfron.Stopien          = StNiepełnosprawności.Umiarkowany;     // enum
    pfron.Okres            = new FromTo(new Date(2026, 1, 1), new Date(2028, 12, 31));
    pfron.DataOrzeczenia   = new Date(2025, 12, 1);
    pfron.DataDostarczenia = new Date(2025, 12, 15);
    pfron.SzczegolneSchorzenie = true;
    pfron.TypSchorzeniaSOD  = SzczególneSchorzeniaSOD.ChoróbPsychiczna;   // wg słownika SOD
    t.Commit();
}
session.Save();

// Odczyt stopnia i okresu niepełnosprawności na dzień:
var ph = pracownik[Date.Today];
StNiepełnosprawności stopien = ph.PFRON.Stopien;
FromTo okresNiepeln = ph.PFRON.Okres;
```

### KADRY-A15 — Odczyt danych pracownika „na dzień" (★)

**Cel:** pobrać właściwy rekord historyczny `PracHistoria` obowiązujący dla zadanej daty — i odróżnić
**odczyt** (nie zmienia historii) od **zmiany „od daty"** z KADRY-A14 (`Update` + `AddRow`). Receptura
czysto odczytowa: idealna do uruchomienia na bazie Demo (kody `"006"`–`"009"`).

**API odczytu — `Pracownik` + `HistorySubTable<PracHistoria>`:**

| Operacja | API | Zwraca |
|---|---|---|
| Zapis obowiązujący na dzień | `pracownik[date]` (indeksator, `Item[Date]`) | `PracHistoria` którego `Aktualnosc` zawiera `date`, albo `null` dla daty sprzed zatrudnienia |
| Równoważnie przez kolekcję | `pracownik.Historia[date]` | jw. (indeksator `HistorySubTable<T>.Item[Date]`) |
| Najstarszy (pierwszy) zapis | `pracownik.Historia.GetFirst()` | `PracHistoria` (pierwszy okres zatrudnienia) |
| Najświeższy (ostatni) zapis | `pracownik.Last` (== `Historia.GetLast()`) | `PracHistoria` (zawsze niepusty dla istniejącego pracownika) |
| Sąsiedni zapis | `Historia.GetPrev(ph)` / `Historia.GetNext(ph)` | poprzedni / następny zapis względem podanego |
| Okres obowiązywania zapisu | `PracHistoria.Aktualnosc: FromTo` | „od–do" zapisu (read-only z punktu widzenia kodu — zarządza historia) |

**Różnica odczyt (KADRY-A15) vs zmiana (KADRY-A14):**

| Aspekt | KADRY-A15 — odczyt | KADRY-A14 — zmiana „od daty" |
|---|---|---|
| Wywołanie | `pracownik[date]` | `pracownik.Historia.Update(date)` + `PracHistorie.AddRow(nowy)` |
| Efekt na historii | **żaden** (nie tworzy/skraca zapisów) | klonuje zapis aktualny na `date`, skraca poprzedni do `date-1`, dodaje nowy |
| Transakcja | niepotrzebna (sam odczyt) | wymagana (`session.Logout(editMode: true)` + `Save()`) |
| Zwraca | istniejący zapis (lub `null`) | **nowy** klon do uzupełnienia |

**Snippet (odczyt — bez transakcji):**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];

// 1) Zapis obowiązujący na konkretny dzień:
var data = new Date(2026, 3, 15);
PracHistoria phNaDzien = pracownik[data];          // == pracownik.Historia[data]
if (phNaDzien != null)
{
    string nazwisko = phNaDzien.Nazwisko;
    FromTo okresZapisu = phNaDzien.Aktualnosc;     // okres obowiązywania tego zapisu
    Fraction wymiar = phNaDzien.Etat.Wymiar;       // warunki etatu „na dzień"
}

// 2) Pierwszy i ostatni zapis historii:
PracHistoria pierwszy = pracownik.Historia.GetFirst();   // najstarszy
PracHistoria ostatni  = pracownik.Last;                  // najświeższy (== GetLast())

// 3) Odczyt aktualny „na dziś" (uwzględnia datę biznesową aplikacji):
PracHistoria phDzis = pracownik[Date.Today];
```

**Pułapki:**
- `pracownik[date]` zwraca **`null`** dla daty sprzed pierwszego zapisu (przed zatrudnieniem) — zawsze
  sprawdzaj `null` przy datach historycznych. `pracownik.Last` jest niepusty dla istniejącego pracownika.
- Indeksator to **tylko odczyt** — nie próbuj „ustawiać" `pracownik[date] = …`. Zmiana danych w okresie
  to korekta (`pracownik[date].Pole = …` w transakcji, KADRY-A14) lub nowy zapis (`Update`, KADRY-A14).
- `Aktualnosc` jest zarządzana przez mechanizm historii — odczytujesz, nie ustawiasz.
- `data` to `Soneta.Types.Date`, nie `DateTime`; do „dziś" używaj `Date.Today` (safe-code §10.2).
- Czysty odczyt nie wymaga transakcji edycyjnej — nie otwieraj `Logout(editMode: true)` bez potrzeby.

### KADRY-A16 — Powiązanie pracownika z kontrahentem (★)

**Cel:** powiązać pracownika z istniejącym kontrahentem (np. gdy pracownik jest jednocześnie
kontrahentem firmy). Dwie drogi: bezpośrednie ustawienie relacji na rootcie **albo** worker
„Powiąż z kontrahentem".

**Publiczny kontrakt — pole relacji na `Pracownik` (root):**

| Pole | Typ | Rodzaj | Uwaga |
|---|---|---|---|
| `PowiazanyKontrahent` | `Soneta.CRM.Kontrahent` | bazodanowe, **zapisywalne** | referencja do istniejącego kontrahenta; `null` = brak powiązania |

**Worker (alternatywa, ta sama operacja):** `Soneta.Kadry.PowiazZKontrahentemWorker`
(`[Action("Powiąż z kontrahentem")]`, metoda `Powiaz()`):

| Składnik | Sygnatura | Uwaga |
|---|---|---|
| Pracownik | `Pracownik { get; set; }` | `[Context]` — pracownik do powiązania |
| Parametry | `Prms: Soneta.Kadry.MyParams` | `MyParams.Kontrahent: Soneta.CRM.Kontrahent` — kontrahent docelowy |
| Akcja | `void Powiaz()` | ustawia powiązanie (działa na danych sesji workera) |

**Snippet (bezpośrednio — zalecane w kodzie biznesowym):**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
var kontrahent = session.GetCRM().Kontrahenci.WgKodu["KLIENT01"];   // istniejący kontrahent

using (var t = session.Logout(editMode: true))
{
    pracownik.PowiazanyKontrahent = kontrahent;   // relacja na rootcie pracownika
    t.Commit();
}
session.Save();

// Odczyt powiązania:
Kontrahent powiazany = pracownik.PowiazanyKontrahent;   // null gdy brak
```

**Snippet (przez worker — gdy chcesz przejść tą samą ścieżką co UI):**

```csharp
using (var t = session.Logout(editMode: true))
{
    var worker = new PowiazZKontrahentemWorker
    {
        Pracownik = pracownik,
        Prms = new MyParams(context) { Kontrahent = kontrahent },
    };
    worker.Powiaz();
    t.CommitUI();   // worker uruchamiany „jak z UI"
}
session.Save();
```

**Pułapki:**
- `Kontrahent` i `Pracownik` muszą pochodzić z **tej samej sesji** (safe-code §2.1) — kontrahenta z innej
  sesji przepuść przez `session.Get(...)`.
- Relacja wskazuje **istniejący** rekord kontrahenta — nie twórz kontrahenta „w locie" w tym scenariuszu.
- `MyParams` ma konstruktor `MyParams(Context context)` — wymaga `Context` (dlatego ścieżka workera ma sens
  głównie z UI). W czystym kodzie biznesowym prościej ustawić `PowiazanyKontrahent` wprost.
- W teście jednostkowym preferuj bezpośrednie pole `PowiazanyKontrahent` — nie wymaga `Context`.

### KADRY-A17 — Przeniesienie do archiwum i przywrócenie (★)

**Cel:** przenieść pracownika do archiwum (po zakończeniu zatrudnienia) oraz przywrócić go z archiwum.
**Operacja przenoszenia/przywracania jest dostępna wyłącznie przez workery** — manager `Archiwum`
udostępnia tylko odczyt statusu.

**Publiczny kontrakt odczytu — `Pracownik`:**

| Składnik | Typ | Rodzaj | Uwaga |
|---|---|---|---|
| `Archiwum` | `Pracownik.ArchiwumManager` | manager (read-only API) | `Archiwum.Status: InformacjeOArchiwum`, `Archiwum.Anonimizowany: bool`, `Archiwum.Okresy: Periods` |
| `ArchiwumInfo` | `Soneta.Kadry.InformacjeOArchiwum` | bazodanowe | bieżąca informacja o archiwizacji |
| `InformacjeOArchiwum` | `FromToSubTable<Soneta.Kadry.PracownikWArchiwum>` | kolekcja | historia okresów w archiwum (`PracownikWArchiwum.Okres: FromTo`) |

> **`ArchiwumManager` nie ma publicznej metody Przenieś/Przywróć** — wystawia jedynie właściwości
> tylko-do-odczytu (`Status`, `Anonimizowany`, `Okresy`). Zmiana stanu archiwum następuje wyłącznie
> przez workery poniżej.

**Workery (jedyna droga zmiany stanu):**

| Worker | Akcja (menu) | Metoda | Parametry |
|---|---|---|---|
| `Pracownik.PrzenieśDoArchiwumWorker` | „Archiwum/Przenieś do archiwum" | `void PrzenieśDoArchiwum()` | `Pracownik { get; set; }` (`[Context]`, pojedynczy) |
| `Pracownik.PrzywróćZArchiwumWorker` | „Archiwum/Przywróć z archiwum" | `void PrzywróćZArchiwum()` | `Pracownik { get; set; }` (`[Context]`, pojedynczy) |
| `Pracownik.PrzenieśDoArchiwumLstWorker` | „Operacje seryjne/Archiwum/Przenieś do archiwum…" | `void PrzenieśDoArchiwum()` | `Pracownicy: Pracownik[]` (grupowo) |
| `Pracownik.PrzywróćZArchiwumLstWorker` | „Operacje seryjne/Archiwum/Przywróć z archiwum…" | `void PrzywróćZArchiwum()` | `Pracownicy: Pracownik[]` (grupowo) |

**Snippet (programowe wywołanie workera — pojedynczy pracownik):**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];

// Przeniesienie do archiwum:
using (var t = session.Logout(editMode: true))
{
    var worker = new Pracownik.PrzenieśDoArchiwumWorker { Pracownik = pracownik };
    worker.PrzenieśDoArchiwum();
    t.CommitUI();
}
session.Save();

// Odczyt stanu archiwizacji:
InformacjeOArchiwum status = pracownik.Archiwum.Status;
bool zanonimizowany = pracownik.Archiwum.Anonimizowany;

// Przywrócenie z archiwum:
using (var t = session.Logout(editMode: true))
{
    var worker = new Pracownik.PrzywróćZArchiwumWorker { Pracownik = pracownik };
    worker.PrzywróćZArchiwum();
    t.CommitUI();
}
session.Save();
```

**Snippet (operacja seryjna — wielu pracowników):**

```csharp
var lista = session.GetKadry().Pracownicy
    .Cast<Pracownik>()
    .Where(p => /* kryterium */ true)
    .ToArray();

using (var t = session.Logout(editMode: true))
{
    var worker = new Pracownik.PrzenieśDoArchiwumLstWorker { Pracownicy = lista };
    worker.PrzenieśDoArchiwum();
    t.CommitUI();
}
session.Save();
```

**Pułapki:**
- **Brak publicznej metody na managerze** — nie szukaj `pracownik.Archiwum.Przenieś(...)`; jedyne
  publiczne API zmiany to workery `PrzenieśDoArchiwumWorker`/`PrzywróćZArchiwumWorker`.
- Workery archiwizacji modyfikują dane → wywołuj w transakcji edycyjnej i `Save()`. Worker uruchamiany
  „jak z UI" → `CommitUI()` (worker-extender §3, pkt 4).
- `Archiwum`, `ArchiwumInfo`, `InformacjeOArchiwum` służą **tylko do odczytu** stanu/historii archiwum.
- Pracownik z workera i `Pracownicy[]` muszą być z bieżącej sesji (safe-code §2.1).
- Archiwizacja bywa powiązana z anonimizacją (`Archiwum.Anonimizowany`) — to oddzielny stan; przeniesienie
  do archiwum nie musi oznaczać anonimizacji.

### KADRY-A18 — Wyrejestrowanie / zwolnienie pracownika (★)

**Cel:** zakończyć zatrudnienie — ustawić rozwiązanie umowy (data, tryb, inicjatywa, podstawa prawna),
ewentualnie okres wypowiedzenia, oraz wygenerować wyrejestrowanie z ZUS (ZWUA) workerem.

**Publiczny kontrakt — `PracHistoria.Etat` (dane historyczne zatrudnienia):**

| Dana | Pole / typ | Uwaga |
|---|---|---|
| Koniec okresu zatrudnienia | `Etat.Okres: Soneta.Types.FromTo` | `Okres.To` = ostatni dzień zatrudnienia (zmiana „od daty" → KADRY-A14) |
| Rozwiązanie umowy (subrow) | `Etat.RozwiazanieUmowy: Soneta.Kadry.RozwiazanieUmowy` | zbiorczy subrow trybu zwolnienia |
| Inicjatywa zwolnienia | `Etat.RozwiazanieUmowy.Inicjatywa: KodInicjatywyZwolnienia` | enum |
| Kod zwolnienia (ZUS) | `Etat.RozwiazanieUmowy.KodZwolnienia: KodZwolnienia` | kod trybu rozwiązania |
| Podstawa prawna | `Etat.RozwiazanieUmowy.PodstawaPrawna: KodPodstawyPrawnejZwolnienia` | enum |
| Przyczyna rozwiązania | `Etat.RozwiazanieUmowy.PrzyczynaRozwUmowy: PrzyczynaRozwUmowy` | rekord słownika; opis: `PrzyczynaRozwUmowyOpis: string` |
| Za odszkodowaniem | `Etat.RozwiazanieUmowy.ZaOdszkodowaniem: bool` | — |
| Okres wypowiedzenia (subrow) | `Etat.OkresWypowiedzenia: Soneta.Kadry.OkresWypowiedzenia` | parametry wypowiedzenia |
| Długość wypowiedzenia | `Etat.OkresWypowiedzenia.Dni / .Tygodnie / .Miesiace: int` | składowe okresu |
| Data złożenia wypowiedzenia | `Etat.OkresWypowiedzenia.DataZlozenia: Date` | — |
| Skrócony okres | `Etat.OkresWypowiedzenia.Skrocony: bool` | — |
| Zwolnienie z obowiązku pracy od | `Etat.OkresWypowiedzenia.ZwolnionyZObowiazkuPracyOd: Date` | — |
| Data upływu wypowiedzenia | `Etat.OkresWypowiedzenia.Uplywa: Date` | wyliczana data rozwiązania (`DataRozwiązaniaUmowy` read-only) |

**Worker ZUS (wyrejestrowanie ZWUA):** `Soneta.Kadry.Pracownik.WyrejestrujPracownikaWorker`
(`[Action("Operacje seryjne/Wyrejestruj pracowników...")]`, metoda `Wyrejestruj()`):

| Składnik | Sygnatura | Uwaga |
|---|---|---|
| Ctor (parametry z `Context`) | `WyrejestrujPracownikaWorker(WyrejestrujPracownikaParams pars)` | `pars` inicjowany z `Context` |
| Data wyrejestrowania | `WyrejestrujPracownikaParams.Data: Date` | data zdarzenia ZWUA |
| Pracownicy | `Pracownicy: Pracownik[]` (`[Context]`) | lista do wyrejestrowania |
| Bieżąca data | `Current: Date` | data robocza |
| Akcja | `void Wyrejestruj()` | tworzy wyrejestrowania ZUS (ZWUA) |

**Snippet (ustawienie rozwiązania umowy — nowy zapis „od daty", KADRY-A14):**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
var dataRozwiazania = new Date(2026, 6, 30);

using (var t = session.Logout(editMode: true))
{
    var ph = pracownik[dataRozwiazania];           // zapis obowiązujący na dzień rozwiązania (KADRY-A15)
    var etat = ph.Etat;

    // Zamknięcie okresu zatrudnienia ostatnim dniem pracy:
    etat.Okres = new FromTo(etat.Okres.From, dataRozwiazania);

    // Tryb rozwiązania (subrow RozwiazanieUmowy):
    etat.RozwiazanieUmowy.Inicjatywa   = KodInicjatywyZwolnienia.Pracownik;
    etat.RozwiazanieUmowy.PodstawaPrawna = KodPodstawyPrawnejZwolnienia._550;  // kody numeryczne wg słownika (NieDotyczy, _400.._463, _550)

    // Opcjonalnie okres wypowiedzenia:
    etat.OkresWypowiedzenia.DataZlozenia = new Date(2026, 5, 31);
    etat.OkresWypowiedzenia.Miesiace     = 1;

    t.Commit();
}
session.Save();
```

**Snippet (wyrejestrowanie z ZUS — worker):**

```csharp
using (var t = session.Logout(editMode: true))
{
    var pars = new Pracownik.WyrejestrujPracownikaWorker.WyrejestrujPracownikaParams(context)
    {
        Data = new Date(2026, 7, 1),
    };
    var worker = new Pracownik.WyrejestrujPracownikaWorker(pars)
    {
        Pracownicy = new[] { pracownik },
        Current = Date.Today,
    };
    worker.Wyrejestruj();
    t.CommitUI();
}
session.Save();
```

**Pułapki:**
- `RozwiazanieUmowy` i `OkresWypowiedzenia` to **subrowy** `Etat` — modyfikuj ich pola, nie przypisuj całych
  obiektów.
- Konkretne wartości enumów (`KodInicjatywyZwolnienia`, `KodPodstawyPrawnejZwolnienia`, `PrzyczynaRozwUmowy`)
  zależą od słownika danej bazy — w teście pobierz/odczytaj realne wartości z Demo zamiast zgadywać.
- `WyrejestrujPracownikaWorker` ma **konstruktor** przyjmujący `WyrejestrujPracownikaParams`, który z kolei
  wymaga `Context` (`WyrejestrujPracownikaParams(Context cx)`) — worker jest praktycznie wywoływalny tylko
  z dostępnym `Context`. Bez `Context` operację wyrejestrowania ZUS zrealizujesz tylko częściowo (samo
  ustawienie `Etat.Okres`/`RozwiazanieUmowy` nie tworzy dokumentu ZWUA).
- `Uplywa`/`DataRozwiązaniaUmowy` bywają wyliczane — nie nadpisuj pól read-only.
- Zmiana warunków „od dnia" to nowy zapis (KADRY-A14); samo zamknięcie `Etat.Okres.To` na bieżącym zapisie jest
  korektą całego okresu — używaj świadomie.

### KADRY-A19 — Przerejestrowanie pracownika (★)

**Cel:** zmienić kod tytułu ubezpieczenia (`Tyub4`) lub jednostkę (`Wydzial`) od konkretnego dnia —
co skutkuje **wyrejestrowaniem ze starym kodem i ponownym zgłoszeniem z nowym** (ZUS ZWUA + ZUA).
Realizacja: nowy zapis historii „od daty" (KADRY-A14) z innym `Etat.Ubezpieczenia.Tyub4`/`Etat.Wydzial`,
a generowanie deklaracji ZUS — workerem przerejestrowania.

**Publiczny kontrakt — pola do zmiany (na nowym zapisie `PracHistoria`):**

| Dana | Pole / typ | Uwaga |
|---|---|---|
| Tytuł ubezpieczenia | `Etat.Ubezpieczenia.Tyub4: Soneta.Kadry.TytulUbezpieczenia4` | rekord słownika; `session.GetKadry().TytulyUbezpiecz4.WgKodu[int]` (klucz `int`, np. `110`) |
| Jednostka organizacyjna | `Etat.Wydzial: Soneta.Kadry.Wydzial` | referencja do istniejącego wydziału |

**Worker ZUS (przerejestrowanie):** `Soneta.Deklaracje.UI.PrzerejestrowaniePracownikaWorker`
(`[Action("Przerejestrowanie pracownika …")]`, metoda `PrzerejestrowaniePracownika()`):

| Składnik (`PrzerejestrowaniePracownikaWorker.Params`) | Typ | Uwaga |
|---|---|---|
| `DataRejestracji` | `Soneta.Types.Date` | data ponownego zgłoszenia |
| `DataWypełnienia` | `Soneta.Types.Date` | data wypełnienia deklaracji |
| `Kedu` | `Soneta.Deklaracje.ZUS.KEDU` | zbiór deklaracji ZUS (KEDU) |
| `Przyczyna` | `Soneta.Kadry.Wyrejestrowanie` | przyczyna wyrejestrowania (do ZWUA) |

**Snippet (zmiana kodu tytułu ubezpieczenia / wydziału „od daty"):**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
var odDnia = new Date(2026, 7, 1);

using (var t = session.Logout(editMode: true))
{
    // Nowy zapis historii „od daty" (KADRY-A14): Update klonuje + skraca poprzedni, AddRow dopina klon.
    var nowy = (PracHistoria)pracownik.Historia.Update(odDnia);
    pracownik.Module.PracHistorie.AddRow(nowy);

    // Zmiana kodu tytułu ubezpieczenia (przerejestrowanie ubezpieczeniowe):
    nowy.Etat.Ubezpieczenia.Tyub4 = session.GetKadry().TytulyUbezpiecz4.WgKodu[110];

    // Lub/oraz zmiana jednostki organizacyjnej:
    nowy.Etat.Wydzial = session.GetKadry().Wydzialy.Firma;

    t.Commit();
}
session.Save();
```

**Pułapki:**
- Przerejestrowanie to **nowy zapis historii** (KADRY-A14: `Update(odDnia)` + `PracHistorie.AddRow(nowy)`) — nie
  nadpisuj `Tyub4`/`Wydzial` na bieżącym zapisie (to zmieniłoby cały okres wstecz).
- `Tyub4` pobierasz ze słownika `TytulyUbezpiecz4` po **`int`** (`WgKodu[110]`), nie po stringu i nie „w locie".
- `Wydzial` to referencja do istniejącego wydziału (korzeń: `session.GetKadry().Wydzialy.Firma`).
- `PrzerejestrowaniePracownikaWorker` żyje w `Soneta.Deklaracje.UI` i jego `Params` wymaga m.in. `KEDU`
  (zbiór deklaracji) oraz `Context` — generowanie ZWUA+ZUA jest realnie wykonalne tylko w środowisku z
  `Context`/`KEDU`. Sama zmiana danych kadrowych (`Tyub4`/`Wydzial`) jest w pełni wykonalna publicznym API
  bez workera; deklaracje ZUS — tylko przez worker UI.
- `Update(odDnia)` rzuca `DateDuplicateException`, jeśli na `odDnia` już zaczyna się zapis (KADRY-A14).

