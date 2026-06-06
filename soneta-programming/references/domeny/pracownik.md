# Pracownik / Kadry-Płace — receptury kodu biznesowego (Soneta / enova365)

Zbiór gotowych wzorców kodu dla domeny **Kadry i Płace**: obiekt biznesowy
**`Soneta.Kadry.Pracownik`** (tabela `Pracownicy`) wraz z jego historią kadrową, etatem,
nieobecnościami, planem pracy, umowami cywilnoprawnymi i wypłatami. Dokument jest częścią skilla
`soneta-programming`. Celem jest, aby agent pisał **bezbłędny kod biznesowy** operujący na
pracowniku — trafiający w realne pola, kolekcje i workery platformy.

> Format **zwarty**: każdy wzorzec opisuje ogólny przypadek + tabelę wariantów. Fundamenty (sesja,
> transakcja, blokada optymistyczna, praca z `SubTable`, obsługa błędów, wywoływanie workerów)
> są opisane w [`safe-code.md`](../safe-code.md), [`session-login.md`](../session-login.md) oraz
> [`worker-extender.md`](../worker-extender.md) — tutaj się do nich odwołujemy, nie powtarzamy ich.
>
> **Cały kod w tym dokumencie jest zgodny z C# 10** (target-typed `new`, `var`, wyrażenia `switch`,
> nazwane parametry `bool`). Snippety operują wyłącznie na **publicznym kontrakcie** platformy — nie
> ma odwołań do prywatnych klas ani kodu źródłowego aplikacji.

## Fakty o typie (zweryfikowane skanem DLL — `scan-props.csx`)

- **Klasa biznesowa:** `Soneta.Kadry.Pracownik` — `GuidedRow` (root), tabela `Soneta.Kadry.Pracownicy`.
- **Moduły i dostęp z sesji:**
  - `Soneta.Kadry.KadryModule` — `session.GetKadry()`; tabela `kadry.Pracownicy`.
  - `Soneta.Place.PlaceModule` — `session.GetPlace()`; wypłaty, listy płac, definicje elementów.
  - `Soneta.Kalend.KalendModule` — `session.GetKalend()`; nieobecności, kalendarze, plan pracy, RCP, limity.
  - `Soneta.HR.HRModule` (`session.GetHR()`), `Soneta.HR2.HR2Module` (`session.GetHR2()`) — definicje
    stanowisk, struktura, ZZL/oceny/rekrutacja.
- **Obiekt historyczny:** dane kadrowe i warunki etatu obowiązują „od–do" i są przechowywane w
  zapisach historycznych. Kolekcja `Pracownik.Historia: HistorySubTable<Soneta.Kadry.PracHistoria>`.
  Rekord `PracHistoria` (tabela `PracHistorie`, child pracownika) zawiera m.in. złożone pole
  `Etat: Soneta.Kadry.Etat` (warunki zatrudnienia), adresy, dane podatkowe/ubezpieczeniowe.
- **Najważniejsze pola bazodanowe `Pracownik` (poziom root):** `Kod: string`, `Nazwisko: string`,
  `Imie: string`, `PESEL: string`, `ArchiwumInfo`, `NumerRachunkuUS`, `NumerRachunkuZUS`.
  (Większość danych kadrowych jest w `PracHistoria`, nie na root.)
- **Kluczowe kolekcje (`SubTable`) na `Pracownik`:**
  - `Historia: HistorySubTable<PracHistoria>` — zapisy historyczne (dane kadrowe + `Etat`).
  - `Nieobecnosci: FromToSubTable<Soneta.Kalend.Nieobecnosc>` — nieobecności.
  - `Limity: SubTable<Soneta.Kalend.LimitNieobecnosci>` — limity nieobecności (np. urlop).
  - `Dodatki: SubTable<Soneta.Kadry.Dodatek>` — stałe elementy wynagrodzenia (dodatki).
  - `Akordy: SubTable<Soneta.Kadry.Akord>` — akordy.
  - `Umowy: SubTable<Soneta.Kadry.Umowa>` — umowy cywilnoprawne; `UmowyZewnetrzne: SubTable<UmowaZewnetrzna>`.
  - `Rachunki: SubTable<Soneta.Kasa.RachunekBankowyPodmiotu>` — rachunki bankowe pracownika.
  - `DniPracy: DateSubTable<Soneta.Kalend.DzienPracy>` — plan/realizacja czasu pracy (dzień po dniu).
  - `DniRCP: DateSubTable<Soneta.Kalend.DzienRCP>` — zarejestrowany czas pracy (RCP).
  - `DniPlanu: DateSubTable` — plan pracy (harmonogram).
  - `Kalendarze: SubTable<Soneta.Kalend.KalendarzBase>` — kalendarze pracownika.
  - `PlanowaneWypłaty`, `PlanowaneElementy`, `PlanowaneNieobecności` — dane planistyczne.
- **Cechy:** `Features: Soneta.Business.FeatureCollection` (indeksator po nazwie definicji cechy).
- **Dane w bazie Demo (GoldStandard):** ~80 zatrudnionych pracowników etatowych, kody `"006"`, `"007"`,
  `"008"`, … (po jednym zapisie historii każdy). To stabilne punkty wejścia do scenariuszy odczytu.

## Podstawowe typy domenowe

| Typ | Namespace | Zastosowanie |
|---|---|---|
| `Date` | `Soneta.Types` | data bez czasu (daty zatrudnienia, obowiązywania) |
| `FromTo` | `Soneta.Types` | zakres dat „od–do" (okres etatu, nieobecności); `FromTo.Parse`, `FromTo.Year` |
| `Time` | `Soneta.Types` | czas/norma (np. norma dobowa `8:00`) |
| `Fraction` | `Soneta.Types` | wymiar etatu jako ułamek (np. `Fraction.One` = pełny etat, `1/2`) |
| `Currency` / `decimal` | `Soneta.Types` / — | kwoty (stawka, wartość wypłaty) |
| `YearMonth` | `Soneta.Types` | miesiąc rozliczeniowy (okres wypłaty) |

## Szablon wzorca

Każdy wzorzec (`Xn`, gdzie `X` = litera sekcji z listy zadań) ma stałą strukturę:

- **Cel** — co robi i kiedy go użyć.
- **Warianty** — tabela odmian przypadku (gdy dotyczy).
- **Pola i typy** — realne właściwości/kolekcje i ich typy.
- **Snippet** — kod C# 10 na publicznym kontrakcie.
- **Pułapki** — typowe błędy i zasady safe-code.

> **Konwencja testów:** każdy wzorzec ma odpowiadający test w
> `Soneta.Skills.Test/KadryPlace/Pracownik/` (klasa dziedzicząca z `PracownikTestBase : TestBase`).
> Testy są wykonywane na bazie Demo z automatycznym rollbackiem — można w nich tworzyć i modyfikować
> dowolne dane. Stanowią wykonywalną dokumentację publicznego API.

---

<!-- SEKCJE FUNKCJONALNOŚCI — uzupełniane przez subagentów dokumentujących.
     Kolejność wg listy zadań (A–K). Każda pozycja gwiazdkowana (★) ma własny wzorzec. -->

## Spis treści

- [Fakty o typie](#fakty-o-typie-zweryfikowane-skanem-dll--scan-propscsx)
- [Podstawowe typy domenowe](#podstawowe-typy-domenowe)
- [Szablon wzorca](#szablon-wzorca)
Pozycje oznaczone (★) to funkcjonalności priorytetowe (wprost wskazane). Każda receptura ma własny
podrozdział `### Xn` oraz odpowiadający test.

- **A. Pracownik — zatrudnienie i dane kartotekowe:** A1 (★) zatrudnienie · A2 (★) dane kadrowe ·
  A3 adresy · A4 kontakt/WWW · A5 rachunki · A6 PIT · A7 (★) ubezpieczenia · A8 ZUS/NFZ ·
  A9 (★) rodzina · A10 (★) poprzednie miejsca pracy · A11 wykształcenie/języki/wojsko · A12 GUS ·
  A13 PFRON · A14 (★) aktualizacja historyczna · A15 odczyt na dzień · A16 powiązanie z kontrahentem ·
  A17 archiwum · A18 zwolnienie/ZWUA · A19 przerejestrowanie
- **B. Etat:** B1 (★) definiowanie etatu · B2 aneks · B3 przeszeregowanie · B4 rozwiązanie umowy ·
  B5 obniżenie wymiaru · B6 podzielniki kosztów · B7 definicja stanowiska
- **C. Dodatki, potrącenia, akordy:** C1 (★) dodatki · C2 potrącenia · C3 akordy · C4 zajęcia
  komornicze · C5 operacje seryjne · C6 ZFŚS · C7 pożyczki
- **D. Nieobecności i czas pracy:** D1 (★) wprowadzanie · D2 (★) korygowanie · D3 e-ZLA · D4 Z-3/Z-3a ·
  D5 przestój · D6 okres zasiłkowy · D7 (★) limity (odczyt) · D8 naliczanie limitów · D9 podstawy ·
  D10 bilans otwarcia · D11 wnioski urlopowe · D12 praca zdalna
- **E. Plan pracy i kalendarz:** E1 (★) plan czasu pracy · E2 (★) kopiowanie planu ·
  E3 aktualizacja kalendarza · E4 doba pracownicza · E5 odczyt normy/czasu przepracowanego
- **F. RCP:** F1 (★) rejestracja czasu · F2 (★) wejścia/wyjścia · F3 import RCP · F4 weryfikacja/korekta ·
  F5 praca hybrydowa/podzielniki
- **G. Umowy cywilnoprawne:** G1 (★) dodawanie · G2 (★) zmiana/aneks · G3 operacja seryjna ·
  G4 rozliczenie/rachunek umowy · G5 zgłoszenia ZUS zleceniobiorców
- **H. Płace — naliczanie wypłat:** H1 (★) etatowe · H2 (★) z umów · H3 (★) pozostałe · H4 (★) odczyt
  za rok · H5 elementy wypłaty · H6 zaliczka · H7 przelicz podatki · H8 dochód · H9 kalkulator ·
  H10 stornowanie · H11 anulowanie/bufor
- **I. Listy płac, przelewy, wydruki:** I1 (★) listy płac · I2 (★) PDF paska · I3 (★) PDF listy ·
  I4 przelewy · I5 eksport do banku · I6 rozliczenia/faktura
- **J. Deklaracje (ZUS, PIT, PFRON, PPK):** J1 zgłoszenia ZUS · J2 rozliczeniowe ZUS · J3 PIT ·
  J4 PFRON · J5 PPK · J6 bilanse otwarcia
- **K. Ewidencje pracownicze:** K1 badania lekarskie · K2 BHP · K3 szkolenia/uprawnienia ·
  K4 nagrody/kary/oświadczenia · K5 wypadki · K6 RODO · K7 struktura organizacyjna · K8 oceny ·
  K9 rekrutacja

> **Testy weryfikujące** (wykonywalna dokumentacja, baza Demo z rollbackiem) w
> `Soneta.Skills.Test/KadryPlace/Pracownik/`: `SmokeTest`, `RozdzialA_PracownikTest`,
> `RozdzialArest_KartotekaTest`, `RozdzialBC_EtatDodatkiTest`, `RozdzialBrest_EtatTest`,
> `RozdzialCrest_PotraceniaTest`, `RozdzialD_NieobecnosciTest`, `RozdzialDrest_NieobecnosciTest`,
> `RozdzialEF_PlanRcpTest`, `RozdzialEFrest_PlanRcpTest`, `RozdzialG_UmowyTest`, `RozdzialGrest_UmowyTest`,
> `RozdzialH_WyplatyTest`, `RozdzialHrest_WyplatyTest`, `RozdzialI_ListyWydrukiTest`,
> `RozdzialIrest_PrzelewyTest`, `RozdzialJ_DeklaracjeTest`, `RozdzialK1_EwidencjeTest`,
> `RozdzialK2_RodoZzlTest`. Łącznie **172 testy** (134 zielone, 38 świadomie pominiętych `[Ignore]` —
> operacje wymagające sieci, `Context`/`KEDU`, konfiguracji niedostępnej w Demo, praw dostępu lub
> składowych `internal`; powód podany przy każdym pominiętym teście i w pułapkach receptury).

---

## A. Pracownik — zatrudnienie i dane kartotekowe

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

### A1 — Zatrudnienie nowego pracownika (★)

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

Pierwszy zapis historii ma okres otwarty (do `Date.MaxValue`); warunki etatu (A1 → B) ustawia się
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
  dotyczy dopiero *aktualizacji* danych „od daty" (A14).
- Dane osobowe ustawiaj na `Last`/`pracownik[date]`, nie próbuj ich „obejść" przez root — root
  deleguje do zapisu, ale zapis jest właściwym miejscem.
- `Kod` bywa polem wymagającym unikalności (zależnie od konfiguracji) — kolizja wybuchnie w
  `Save()` jako `RowException` (z `DuplicateKeyException` w `InnerException`).
- Całość w transakcji (`session.Logout(editMode: true)`); brak `Commit()` = rollback przy `Dispose()`.

### A2 — Podstawowe dane kadrowe (★)

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

### A7 — Dane ubezpieczenia społecznego i zdrowotnego (★)

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
  „od daty" wymaga nowego zapisu historii (A14), nie nadpisywania bieżącego.
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

### A9 — Dane o rodzinie pracownika (★)

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

### A10 — Poprzednie miejsca pracy (★)

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

### A14 — Aktualizacja danych historycznych: zmiana „od daty" vs korekta (★)

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
    // (patrz pułapki w sekcji B1: bramką jest Etat.Okres). Dla pewności w przykładzie zmieniamy
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

### A3 — Adresy (zameldowania / zamieszkania / korespondencyjny)

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
- Cała struktura jest historyczna — zmiana adresu „od daty" to nowy zapis historii (A14), korekta
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

### A4 — Dane kontaktowe (e-mail, telefon) i dostęp WWW/Pulpity

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
(patrz A3). **Rozbudowane kanały kontaktu** (wiele kontaktów z rodzajem/celem): kolekcja
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

### A5 — Rachunki bankowe (rachunek do przelewu wynagrodzenia)

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

### A6 — Dane podatkowe (PIT)

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
  przypisuj całego obiektu; zmiana „od daty" to nowy zapis historii (A14).
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

### A8 — Pozostałe dane ubezpieczeniowe / informacje ZUS (oddział, kod)

**Cel:** ustawić/odczytać dodatkowe dane ZUS pracownika — oddział ZUS oraz dodatkowe świadczenia ZUS
(emerytury/renty z dodatkowymi świadczeniami). Dane są **historyczne** — subrow
`PracHistoria.DodSwiadczeniaZUS`. (Tytuł ubezpieczenia i parametry ubezpieczeń społ./zdrow. opisuje A7.)

**Gdzie leżą pola — subrow `PracHistoria.DodSwiadczeniaZUS: Soneta.Kadry.DodatkoweŚwiadczeniaZUS`:**

| Pole | Typ | Opis |
|---|---|---|
| `OddzialZUS` | `Soneta.CRM.OddziałZUS` (ref) | oddział ZUS (referencja do podmiotu/słownika ZUS) |
| `Rodzaj` | `Soneta.Kadry.RodzajeDodatkowychŚwiadczeńZUS` (enum) | rodzaj dodatkowego świadczenia ZUS |
| `Okres` | `Soneta.Types.FromTo` | okres obowiązywania świadczenia |
| `Numer` | `string` | numer (decyzji/świadczenia) |

**Oddział NFZ (komplementarny do ZUS):** `PracHistoria.OddzialNFZ: Soneta.Kadry.OddzialNFZ` —
pola `OddzialNFZ.Oddział: OddziałNFZ` (enum oddziałów), `OddzialNFZ.KodGminy: string`,
`OddzialNFZ.OdDnia: Date` (patrz też A7).

**Pułapki:**
- `DodSwiadczeniaZUS` to **subrow** — modyfikuj pola (`Last.DodSwiadczeniaZUS.OddzialZUS = …`).
- `OddzialZUS` to **referencja** (`Soneta.CRM.OddziałZUS`) do istniejącego rekordu — pobierz
  istniejący, nie twórz „w locie".
- `Rodzaj` to **enum** `RodzajeDodatkowychŚwiadczeńZUS`, nie string.
- **Cały subrow `DodSwiadczeniaZUS` bywa tylko-do-odczytu** na świeżym zapisie (rzuca
  `ColReadOnlyException` nawet dla `Numer`) — pola te aktywuje dopiero zainicjowanie świadczenia
  w kreatorze/UI. Zapisywalne wprost jest `OddzialNFZ.OdDnia`.
- Zmiana „od daty" to nowy zapis historii (A14).

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

### A11 — Wykształcenie, znajomość języków obcych, służba wojskowa

**Cel:** ewidencjonować wykształcenie, znane języki obce oraz dane służby wojskowej pracownika.
Wykształcenie i wojsko to subrowy zapisu historii (`PracHistoria`); języki obce to kolekcja na rootcie
pracownika.

**Wykształcenie — subrow `PracHistoria.Wyksztalcenie: Soneta.Kadry.Wyksztalcenie`:**

| Pole | Typ | Opis |
|---|---|---|
| `Kod` | `Soneta.Kadry.KodWyksztalcenia` (enum) | poziom/rodzaj wykształcenia |
| `StopienNaukowy` | `string` | stopień naukowy |
| `TytulNaukowy` | `string` | tytuł naukowy |

(Kod wykształcenia GUS jest osobno — patrz A12: `PracHistoria.GUS.KodWyksztalcenia`.)

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
  „od daty" przez A14. `JęzykiObce` to **kolekcja na rootcie** pracownika (nie historyczna).
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

### A12 — Dane statystyczne GUS, kod zawodu GUS

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
- `GUS` to **subrow** `PracHistoria` (historyczny) — modyfikuj pola; zmiana „od daty" przez A14.
- `KodWyksztalcenia` (GUS, enum `KodWykształceniaGUS`) to **inne pole** niż A11
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

### A13 — PFRON i niepełnosprawność / schorzenia

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
- `PFRON` to **subrow** `PracHistoria` (historyczny) — modyfikuj pola; zmiana „od daty" przez A14.
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

### A15 — Odczyt danych pracownika „na dzień" (★)

**Cel:** pobrać właściwy rekord historyczny `PracHistoria` obowiązujący dla zadanej daty — i odróżnić
**odczyt** (nie zmienia historii) od **zmiany „od daty"** z A14 (`Update` + `AddRow`). Receptura
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

**Różnica odczyt (A15) vs zmiana (A14):**

| Aspekt | A15 — odczyt | A14 — zmiana „od daty" |
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
  to korekta (`pracownik[date].Pole = …` w transakcji, A14) lub nowy zapis (`Update`, A14).
- `Aktualnosc` jest zarządzana przez mechanizm historii — odczytujesz, nie ustawiasz.
- `data` to `Soneta.Types.Date`, nie `DateTime`; do „dziś" używaj `Date.Today` (safe-code §10.2).
- Czysty odczyt nie wymaga transakcji edycyjnej — nie otwieraj `Logout(editMode: true)` bez potrzeby.

### A16 — Powiązanie pracownika z kontrahentem (★)

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

### A17 — Przeniesienie do archiwum i przywrócenie (★)

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

### A18 — Wyrejestrowanie / zwolnienie pracownika (★)

**Cel:** zakończyć zatrudnienie — ustawić rozwiązanie umowy (data, tryb, inicjatywa, podstawa prawna),
ewentualnie okres wypowiedzenia, oraz wygenerować wyrejestrowanie z ZUS (ZWUA) workerem.

**Publiczny kontrakt — `PracHistoria.Etat` (dane historyczne zatrudnienia):**

| Dana | Pole / typ | Uwaga |
|---|---|---|
| Koniec okresu zatrudnienia | `Etat.Okres: Soneta.Types.FromTo` | `Okres.To` = ostatni dzień zatrudnienia (zmiana „od daty" → A14) |
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

**Snippet (ustawienie rozwiązania umowy — nowy zapis „od daty", A14):**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
var dataRozwiazania = new Date(2026, 6, 30);

using (var t = session.Logout(editMode: true))
{
    var ph = pracownik[dataRozwiazania];           // zapis obowiązujący na dzień rozwiązania (A15)
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
- Zmiana warunków „od dnia" to nowy zapis (A14); samo zamknięcie `Etat.Okres.To` na bieżącym zapisie jest
  korektą całego okresu — używaj świadomie.

### A19 — Przerejestrowanie pracownika (★)

**Cel:** zmienić kod tytułu ubezpieczenia (`Tyub4`) lub jednostkę (`Wydzial`) od konkretnego dnia —
co skutkuje **wyrejestrowaniem ze starym kodem i ponownym zgłoszeniem z nowym** (ZUS ZWUA + ZUA).
Realizacja: nowy zapis historii „od daty" (A14) z innym `Etat.Ubezpieczenia.Tyub4`/`Etat.Wydzial`,
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
    // Nowy zapis historii „od daty" (A14): Update klonuje + skraca poprzedni, AddRow dopina klon.
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
- Przerejestrowanie to **nowy zapis historii** (A14: `Update(odDnia)` + `PracHistorie.AddRow(nowy)`) — nie
  nadpisuj `Tyub4`/`Wydzial` na bieżącym zapisie (to zmieniłoby cały okres wstecz).
- `Tyub4` pobierasz ze słownika `TytulyUbezpiecz4` po **`int`** (`WgKodu[110]`), nie po stringu i nie „w locie".
- `Wydzial` to referencja do istniejącego wydziału (korzeń: `session.GetKadry().Wydzialy.Firma`).
- `PrzerejestrowaniePracownikaWorker` żyje w `Soneta.Deklaracje.UI` i jego `Params` wymaga m.in. `KEDU`
  (zbiór deklaracji) oraz `Context` — generowanie ZWUA+ZUA jest realnie wykonalne tylko w środowisku z
  `Context`/`KEDU`. Sama zmiana danych kadrowych (`Tyub4`/`Wydzial`) jest w pełni wykonalna publicznym API
  bez workera; deklaracje ZUS — tylko przez worker UI.
- `Update(odDnia)` rzuca `DateDuplicateException`, jeśli na `odDnia` już zaczyna się zapis (A14).

## B. Etat — zatrudnienie etatowe

### B1 — Definiowanie etatu (umowa o pracę) (★)

**Cel:** ustalić warunki zatrudnienia etatowego pracownika — rodzaj umowy o pracę, okres, daty
zawarcia/rozpoczęcia pracy, stanowisko, jednostkę organizacyjną oraz stawkę zaszeregowania
(wymiar etatu, rodzaj/typ stawki, kwota). Warunki etatu są **historyczne**: siedzą w polu
`Etat` konkretnego zapisu `PracHistoria`. Etat ustawiamy albo na świeżo utworzonym pracowniku
(`pracownik.Last.Etat`, patrz A1), albo na nowym zapisie historii „od daty" (patrz A14).

**Gdzie leżą pola — `PracHistoria.Etat: Soneta.Kadry.Etat` (subrow zapisu historii):**

| Dana | Pole / typ | Uwaga |
|---|---|---|
| Rodzaj umowy o pracę | `Etat.TypUmowy: Soneta.Kadry.TypUmowyOPrace` | enum: `NaCzasNieokreślony`, `NaOkresPróbny`, `NaCzasOkreślony`, `NaOkresZastępstwa`, `DoDniaPorodu`, `NaCzasWykonywniaPracy`, … (`Brak = 0` = nie dotyczy) |
| Okres etatu (od–do) | `Etat.Okres: Soneta.Types.FromTo` | okres obowiązywania warunków zatrudnienia |
| Data zawarcia umowy | `Etat.DataZawarcia: Soneta.Types.Date` | data podpisania umowy |
| Data rozpoczęcia pracy | `Etat.DataRozpPracy: Soneta.Types.Date` | data faktycznego rozpoczęcia |
| Stanowisko (opis tekstowy) | `Etat.Stanowisko: string` | **wymagane dla etatu** (weryfikator przy `Save()`) |
| Jednostka organizacyjna (wydział) | `Etat.Wydzial: Soneta.Kadry.Wydzial` | **wymagane dla etatu**; pobierz istniejący wydział, korzeń struktury: `session.GetKadry().Wydzialy.Firma` |
| Oddział firmy | `Etat.Oddzial: Soneta.Core.OddzialFirmy` | opcjonalny oddział |
| Miejsce wykonywania pracy | `Etat.MiejscePracy: string` | tekst |
| Podstawa stosunku pracy | `Etat.Podstawa: Soneta.Kadry.StosPracyNaPodstawie` | enum |

**Stawka — subrow `Etat.Zaszeregowanie: Soneta.Kadry.Zaszeregowanie`:**

| Dana | Pole / typ | Uwaga |
|---|---|---|
| Rodzaj stawki | `Zaszeregowanie.RodzajStawki: Soneta.Kadry.RodzajStawkiZaszeregowania` | enum: `Godzinowa = 0`, `Miesieczna = 1`, `DochodDeklarowany = 2` |
| Typ stawki | `Zaszeregowanie.TypStawki: Soneta.Kadry.TypStawkiZaszeregowania` | enum: `Dowolna = 0`, `Minimalna = 1`, `ZZakresu = 2`, `WgWskaźnika = 3`, `Nieokreślona = 10` |
| Wymiar etatu (ułamek) | `Zaszeregowanie.Wymiar: Soneta.Types.Fraction` | `Fraction.One` = pełny etat; `new Fraction(1, 2)` = ½ etatu |
| Kwota stawki | `Zaszeregowanie.Stawka: Soneta.Types.Currency` | kwota brutto (miesięczna lub godzinowa wg `RodzajStawki`) |
| Grupa zaszeregowania | `Zaszeregowanie.Grupa: Soneta.Kadry.GrupaZaszeregowania` | rekord słownika (opcjonalny) |
| Definicja elementu wynagrodzenia | `Zaszeregowanie.Element: Soneta.Place.DefinicjaElementu` | element płacowy wiązany ze stawką (opcjonalny) |

**Pułapki:**
- **Kolejność ma znaczenie — `Etat.Okres` ustaw jako PIERWSZE.** Na świeżo utworzonym
  pracowniku (lub świeżym zapisie historii) **cały subrow `Etat` jest w trybie tylko-do-odczytu**,
  dopóki nie ustawisz `Etat.Okres` (zakres zatrudnienia). Próba ustawienia `Etat.TypUmowy`,
  `Etat.Podstawa` czy `Zaszeregowanie.RodzajStawki`/`Wymiar` przed `Etat.Okres` rzuca
  `Soneta.Business.ColReadOnlyException` (np. „'Etat.Typ umowy' — pole w trybie 'tylko do odczytu'").
  Po ustawieniu `Etat.Okres` pozostałe pola (w tym `Zaszeregowanie.Wymiar`) są zapisywalne —
  kolejność wśród nich nie ma już znaczenia.
- **Pola wymagane dla etatu:** po ustawieniu `Etat.Okres` (pracownik staje się etatowy) `Save()`
  wymaga `Etat.Wydzial` **oraz** `Etat.Stanowisko` — bez nich zapis rzuca wyjątek weryfikatora.
- `Etat` to **subrow** zapisu `PracHistoria` — modyfikujesz jego pola (`Last.Etat.Okres = …`), nie
  przypisujesz całego obiektu `Etat`.
- `Zaszeregowanie` to z kolei subrow `Etat` — analogicznie modyfikujesz pola
  (`Last.Etat.Zaszeregowanie.Stawka = …`).
- `Etat.Wymiar` i `Etat.TypStawki` istnieją także na poziomie `Etat` (delegaty/odczyt) — **kanonicznie
  ustawiamy je na `Etat.Zaszeregowanie`** (`Zaszeregowanie.Wymiar`, `Zaszeregowanie.RodzajStawki`,
  `Zaszeregowanie.TypStawki`), bo to one są polami bazodanowymi tej struktury.
- `Etat.Wydzial` i `Etat.Oddzial` to **referencje** do istniejących rekordów — nie twórz „w locie";
  korzeń struktury organizacyjnej pobierzesz przez `session.GetKadry().Wydzialy.Firma`.
- Zmiana warunków etatu **od konkretnego dnia** to nowy zapis historii (`Historia.Update(date)` +
  `PracHistorie.AddRow`, patrz A14), a nie nadpisanie bieżącego zapisu (to byłaby korekta całego okresu).
- `TypUmowyOPrace` to enum, nie string; `Okres`/`DataZawarcia`/`DataRozpPracy` to typy biznesowe
  `FromTo`/`Date`, nie `DateTime` (safe-code §10.1).

**Snippet:**

```csharp
var kadry = session.GetKadry();

using (var t = session.Logout(editMode: true))
{
    // Nowy pracownik (A1) — AddRow tworzy pierwszy zapis historii (Last) + kalendarz.
    var pracownik = session.AddRow(new PracownikFirmy());
    pracownik.Kod = "555";
    pracownik.Last.Nazwisko = "Kowalska";
    pracownik.Last.Imie     = "Gabriela";

    // Warunki etatu — na Etat bieżącego (pierwszego) zapisu historii.
    // KLUCZOWE: Etat.Okres MUSI być pierwszy — odblokowuje (z trybu read-only) resztę pól Etat.
    var etat = pracownik.Last.Etat;
    etat.Okres         = new FromTo(new Date(2026, 1, 1), Date.MaxValue);   // 1) NAJPIERW okres
    etat.TypUmowy      = TypUmowyOPrace.NaCzasNieokreślony;
    etat.DataZawarcia  = new Date(2025, 12, 20);
    etat.DataRozpPracy = new Date(2026, 1, 1);
    etat.Stanowisko    = "Specjalista";                  // wymagane dla etatu
    etat.Wydzial       = kadry.Wydzialy.Firma;           // wymagane dla etatu (korzeń struktury)

    // Stawka zaszeregowania (po ustawieniu Etat.Okres pola są zapisywalne):
    var z = etat.Zaszeregowanie;
    z.RodzajStawki = RodzajStawkiZaszeregowania.Miesieczna;
    z.TypStawki    = TypStawkiZaszeregowania.Dowolna;
    z.Wymiar       = Fraction.One;                            // pełny etat
    z.Stawka       = (Currency)6000m;                         // kwota brutto miesięcznie

    t.Commit();
}
session.Save();
```

> **Zmiany warunków zatrudnienia (B2–B7).** Warunki zatrudnienia etatowego siedzą w polu `PracHistoria.Etat: Soneta.Kadry.Etat`
> (subrow zapisu historii). `Etat` jest **historyczny** wraz z całym `PracHistoria` — okres
> obowiązywania warunków trzyma `Etat.Okres: FromTo`, a okres zapisu historii `PracHistoria.Aktualnosc`.
> Zmiana warunków „od dnia" to **nowy zapis historii** (`Historia.Update(date)` + `PracHistorie.AddRow`,
> wzorzec z A14) — modyfikacja bieżącego zapisu byłaby korektą całego jego okresu.
>
> **Bramka edycji etatu (B1).** Na świeżym zapisie cały subrow `Etat` jest tylko-do-odczytu, dopóki
> nie ustawisz `Etat.Okres` — ustaw go **PIERWSZY**, inaczej dotknięcie `TypUmowy`/`Zaszeregowanie.*`
> rzuca `Soneta.Business.ColReadOnlyException`. Pola wymagane przy zapisie etatu: `Etat.Wydzial`
> **oraz** `Etat.Stanowisko`. Po `Update(date)` klon ma już ustawiony `Etat.Okres` (sklonowany ze
> starego zapisu) — zwykle nie trzeba go ustawiać ponownie, ale jeśli zmieniasz okres etatu, rób to
> jako pierwsze.

---

### B2 — Zmiana warunków zatrudnienia (aneks)

**Cel:** zarejestrować aneks do umowy o pracę — zmianę warunków obowiązującą **od wskazanego dnia**
(np. zmiana stanowiska, miejsca pracy, wymiaru, jednostki organizacyjnej). Realizuje się przez
**nowy zapis historyczny** etatu „od daty", nie przez nadpisanie bieżącego.

**Pola `Etat` (subrow `PracHistoria.Etat: Soneta.Kadry.Etat`):**

| Dana | Pole / typ | Uwaga |
|---|---|---|
| Okres etatu (od–do) | `Etat.Okres: Soneta.Types.FromTo` | okres obowiązywania warunków; po `Update` zwykle już ustawiony |
| Rodzaj umowy o pracę | `Etat.TypUmowy: Soneta.Kadry.TypUmowyOPrace` | enum |
| Data zawarcia aneksu | `Etat.DataZawarcia: Soneta.Types.Date` | data podpisania |
| Stanowisko (opis) | `Etat.Stanowisko: string` | wymagane dla etatu |
| Jednostka organizacyjna | `Etat.Wydzial: Soneta.Kadry.Wydzial` | wymagane dla etatu; referencja (`session.GetKadry().Wydzialy.Firma`) |
| Oddział firmy | `Etat.Oddzial: Soneta.Core.OddzialFirmy` | opcjonalny |
| Miejsce wykonywania pracy | `Etat.MiejscePracy: string` | tekst |
| Podstawa stosunku pracy | `Etat.Podstawa: Soneta.Kadry.StosPracyNaPodstawie` | enum |
| Forma organizacji pracy | `Etat.FormaOrganizacjiPracy: Soneta.Kadry.FormaOrganizacjiPracy` | enum |
| Wymiar / stawka (na `Zaszeregowanie`) | `Etat.Zaszeregowanie.Wymiar: Fraction`, `Etat.Zaszeregowanie.Stawka: Currency` | patrz B3 |

**Snippet:**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
var odDnia = new Date(2026, 7, 1);

using (var t = session.Logout(editMode: true))
{
    // Nowy zapis historii „od daty" — klonuje zapis aktualny na `odDnia`, skraca stary do dnia
    // poprzedniego i zwraca nowy klon (okres od `odDnia`). Klon MUSI trafić do tabeli zapisów.
    var nowy = (PracHistoria)pracownik.Historia.Update(odDnia);
    pracownik.Module.PracHistorie.AddRow(nowy);

    // Etat na klonie ma już Okres (sklonowany) — pola Etat są zapisywalne. Aneksowane warunki:
    var etat = nowy.Etat;
    etat.Stanowisko   = "Starszy specjalista";
    etat.MiejscePracy = "Oddział Kraków";
    etat.DataZawarcia = new Date(2026, 6, 20);
    etat.Wydzial      = session.GetKadry().Wydzialy.Firma;   // wymagane (referencja)

    t.Commit();
}
session.Save();
```

**Pułapki:**
- `Update(date)` + `PracHistorie.AddRow(nowy)` to **nierozłączna para** — sam `Update` zwraca odpięty
  klon; bez `AddRow` zmiana nie zostanie zapisana.
- `Update(date)` rzuca `HistorySubTable.DateDuplicateException`, gdy na `date` już zaczyna się zapis
  (`Aktualnosc.From == date`) — wtedy modyfikuj istniejący zapis (`pracownik[date]`).
- Nie ustawiaj `PracHistoria.Aktualnosc` ani (zwykle) `Etat.Okres` ręcznie — zarządza nimi historia.
  Jeśli aneks zmienia długość okresu etatu, ustaw `Etat.Okres` **przed** pozostałymi polami (bramka B1).
- `Etat` to subrow — modyfikuj jego pola, nie przypisuj całego obiektu.

---

### B3 — Przeszeregowanie (zmiana stawki / grupy zaszeregowania)

**Cel:** zmienić wynagrodzenie zasadnicze — stawkę i/lub grupę zaszeregowania, od wskazanego dnia.

**Pola `Etat.Zaszeregowanie: Soneta.Kadry.Zaszeregowanie` (subrow `Etat`):**

| Dana | Pole / typ | Uwaga |
|---|---|---|
| Rodzaj stawki | `Zaszeregowanie.RodzajStawki: Soneta.Kadry.RodzajStawkiZaszeregowania` | enum: `Godzinowa = 0`, `Miesieczna = 1`, `DochodDeklarowany = 2` |
| Typ stawki | `Zaszeregowanie.TypStawki: Soneta.Kadry.TypStawkiZaszeregowania` | enum: `Dowolna`, `Minimalna`, `ZZakresu`, `WgWskaźnika`, `Nieokreślona` |
| Kwota stawki | `Zaszeregowanie.Stawka: Soneta.Types.Currency` | brutto (miesięczna/godzinowa wg `RodzajStawki`) |
| Wymiar etatu | `Zaszeregowanie.Wymiar: Soneta.Types.Fraction` | `Fraction.One` = pełny etat |
| Grupa zaszeregowania | `Etat.Grupa: Soneta.Kadry.GrupaZaszeregowania` | **leży na `Etat`, nie na `Zaszeregowanie`**; referencja do słownika `session.GetKadry().GrupyZaszer` (opcjonalna) |
| Element wynagrodzenia | `Zaszeregowanie.Element: Soneta.Place.DefinicjaElementu` | element płacowy wiązany ze stawką (opcjonalny) |
| Wskaźnik (wg wskaźnika) | `Zaszeregowanie.WskaznikNazwa: string`, `Zaszeregowanie.WskaznikKrotnosc: double` | gdy `TypStawki = WgWskaźnika` |

**Snippet (bezpośrednia zmiana, „od daty"):**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
var odDnia = new Date(2026, 7, 1);

using (var t = session.Logout(editMode: true))
{
    var nowy = (PracHistoria)pracownik.Historia.Update(odDnia);
    pracownik.Module.PracHistorie.AddRow(nowy);

    var etat = nowy.Etat;                    // subrow zapisu; Okres ustawiony przez Update
    etat.Zaszeregowanie.Stawka = (Currency)7200m;   // podwyżka stawki zasadniczej
    // etat.Grupa = session.GetKadry().GrupyZaszer...  // ewentualna zmiana grupy (leży na Etat, nie na Zaszeregowanie)

    t.Commit();
}
session.Save();
```

**Worker platformy (alternatywa seryjna):** przeszeregowania realizuje moduł
`Soneta.Przeszeregowania` — dokument `Soneta.Kadry.Przeszeregowanie` z elementami
`ElementPrzeszeregowania` (m.in. `Soneta.Kadry.ZmianaStawki`), wykonywany czynnością
`ZmianaStawkiWorker` (zmiana kwoty / procentowa / grupa) dla zaznaczonej grupy pracowników. Element
`ZmianaStawki` ma pola `Grupa: GrupaZaszeregowania`, `Kwota: Currency` i zapisuje wynik do
`Etat.Zaszeregowanie` właściwego zapisu historii.

**Pułapki:**
- `Wymiar`/`Stawka`/`RodzajStawki` na **świeżym** zapisie są zapisywalne dopiero po `Etat.Okres`
  (bramka B1); po `Update` okres jest już sklonowany, więc pola są zapisywalne.
- `Stawka` to `Currency` (nie `decimal`), `Wymiar` to `Fraction` (nie `double`) — safe-code §10.1.
- `Etat.Grupa`/`Zaszeregowanie.Element` to **referencje** do istniejących rekordów — nie twórz „w locie".
  **Uwaga:** `Grupa` jest polem `Etat` (pobierasz ze słownika `session.GetKadry().GrupyZaszer`), a **nie**
  polem `Zaszeregowanie` — `Zaszeregowanie` nie ma property `Grupa`.
- Kanonicznie ustawiasz pola stawki na `Etat.Zaszeregowanie` (pola bazodanowe), nie na delegatach
  `Etat.Wymiar`/`Etat.TypStawki`.

---

### B4 — Rozwiązanie / wygaśnięcie umowy o pracę

**Cel:** zakończyć stosunek pracy z dniem rozwiązania — ustawić datę końca okresu etatu, dane
wypowiedzenia oraz przyczynę/kod rozwiązania (na potrzeby świadectwa pracy i deklaracji ZUS).

**Wzorzec (zgodny z czynnością „Zwolnij zaznaczonych pracowników"):**
1. skróć `Etat.Okres.To` do dnia rozwiązania (na bieżącym zapisie albo na nowym zapisie „od daty"),
2. ustaw dane wypowiedzenia (`Etat.OkresWypowiedzenia.*`) i przyczynę (`Etat.RozwiazanieUmowy.*`),
3. opcjonalnie oznacz `Etat.PracownikZwolniony = true` i wyrejestruj z ubezpieczeń.

**Pola — `Etat.OkresWypowiedzenia: Soneta.Kadry.OkresWypowiedzenia` (subrow):**

| Dana | Pole / typ | Uwaga |
|---|---|---|
| Data złożenia wypowiedzenia | `OkresWypowiedzenia.DataZlozenia: Soneta.Types.Date` | data wręczenia wypowiedzenia |
| Długość — dni / tygodnie / miesiące | `OkresWypowiedzenia.Dni: int`, `.Tygodnie: int`, `.Miesiace: int` | okres wypowiedzenia |
| Data upływu | `OkresWypowiedzenia.Uplywa: Soneta.Types.Date` | data upływu okresu wypowiedzenia |
| Skrócony | `OkresWypowiedzenia.Skrocony: bool` | skrócony okres wypowiedzenia |
| Zwolnienie z obowiązku pracy od | `OkresWypowiedzenia.ZwolnionyZObowiazkuPracyOd: Date` | |
| Data rozwiązania umowy (odczyt) | `OkresWypowiedzenia.DataRozwiązaniaUmowy: Date` | kalkulowane |

**Pola — `Etat.RozwiazanieUmowy: Soneta.Kadry.RozwiazanieUmowy` (subrow):**

| Dana | Pole / typ | Uwaga |
|---|---|---|
| Przyczyna rozwiązania | `RozwiazanieUmowy.PrzyczynaRozwUmowy: Soneta.Kadry.PrzyczynaRozwUmowy` | **referencja do słownika** `session.GetKadry().PrzyczRozwUmow` (indeks `WgNazwy` lub iteracja; **brak indeksera `WgKodu`**); rekord ma `Typ: TypPrzyczynyRozwUmowy` |
| Opis przyczyny | `RozwiazanieUmowy.PrzyczynaRozwUmowyOpis: string` | tekst |
| Podstawa prawna | `RozwiazanieUmowy.PodstawaPrawna: Soneta.Kadry.KodPodstawyPrawnejZwolnienia` | enum (tryb rozwiązania: za wypowiedzeniem, porozumienie, wygaśnięcie itd.) |
| Kod zwolnienia (ZUS) | `RozwiazanieUmowy.KodZwolnienia: Soneta.Kadry.KodZwolnienia` | enum (kod do ZUS RA/świadectwa) |
| Inicjatywa | `RozwiazanieUmowy.Inicjatywa: Soneta.Kadry.KodInicjatywyZwolnienia` | enum (pracodawca/pracownik) |
| Za odszkodowaniem | `RozwiazanieUmowy.ZaOdszkodowaniem: bool` | |
| Pracownik zwolniony (flaga) | `Etat.PracownikZwolniony: bool` | znacznik zakończenia |

**Snippet:**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
var dataRozwiazania = new Date(2026, 9, 30);

using (var t = session.Logout(editMode: true))
{
    var ph = pracownik.Last;
    var etat = ph.Etat;

    // 1) skrócenie okresu etatu do dnia rozwiązania
    etat.Okres = new FromTo(etat.Okres.From, dataRozwiazania);

    // 2) dane wypowiedzenia
    etat.OkresWypowiedzenia.DataZlozenia = new Date(2026, 8, 31);
    etat.OkresWypowiedzenia.Miesiace     = 1;

    // 3) przyczyna / tryb rozwiązania
    //    PrzyczynaRozwUmowy to rekord słownika — pobierz po nazwie (WgNazwy) albo iteracją (brak WgKodu):
    etat.RozwiazanieUmowy.PrzyczynaRozwUmowy = session.GetKadry().PrzyczRozwUmow.WgNazwy["Wypowiedzenie przez pracownika"]; // referencja
    //    PodstawaPrawna to enum kodów: NieDotyczy, _400.._463, _550 (kody GUS/ZUS) — wybierz właściwy kod:
    etat.RozwiazanieUmowy.PodstawaPrawna     = KodPodstawyPrawnejZwolnienia._400;
    etat.RozwiazanieUmowy.Inicjatywa         = KodInicjatywyZwolnienia.Pracownik;

    etat.PracownikZwolniony = true;          // znacznik zakończenia zatrudnienia

    t.Commit();
}
session.Save();
```

**Pułapki:**
- **Wygaśnięcie** vs **rozwiązanie** rozróżnia `PodstawaPrawna` (enum trybu) oraz `KodZwolnienia` —
  to one trafiają do świadectwa pracy i deklaracji ZUS.
- `PrzyczynaRozwUmowy` to **rekord słownika** (referencja), nie enum — pobierz istniejący wpis z
  `session.GetKadry().PrzyczRozwUmow` (indeks `WgNazwy` lub iteracja — **słownik nie ma indeksera
  `WgKodu`**). Pomyłka: `RozwiazanieUmowy.PrzyczynaRozwUmowy` (referencja) ≠
  `PrzyczynaRozwUmowy.Typ` (enum `TypPrzyczynyRozwUmowy` na rekordzie słownika).
- `KodPodstawyPrawnejZwolnienia` to enum kodów GUS/ZUS o wartościach `NieDotyczy`, `_400`..`_463`,
  `_550` (nazwy z prefiksem `_`) — **nie ma** wartości opisowych typu
  `RozwiazanieZaWypowiedzeniemPrzezPracownika`. `KodInicjatywyZwolnienia`: `NieDotyczy`, `Pracownik`,
  `Pracodawca`.
- Skrócenie `Etat.Okres.To` zmienia warunki w **całym** bieżącym okresie zapisu. Jeśli rozwiązanie
  ma obowiązywać od konkretnego dnia z zachowaniem poprzedniego okresu, użyj nowego zapisu
  (`Historia.Update(data)` + `PracHistorie.AddRow`), a zmiany rób na klonie.
- Wyrejestrowanie z ubezpieczeń (`IUbezpieczenie.Wyrejestrowany`/daty `Do`) to osobny krok — patrz A7.
- `Okres`/`DataZlozenia`/`Uplywa` to `FromTo`/`Date`, nie `DateTime`.

---

### B5 — Obniżenie / przywrócenie wymiaru etatu

**Cel:** czasowo obniżyć wymiar etatu i stawkę (operacje typu COVID / seryjne), a następnie
przywrócić warunki. Stan obniżenia jest **odczytowo** widoczny w subrowie
`Etat.ObnizenieEtatu: Soneta.Kadry.ObniżenieWymiaruEtatu` (delegat do zapisu historii etatu).

> **Ważne (zweryfikowane na DLL):** subrow `ObniżenieWymiaruEtatu` jest **w całości tylko-do-odczytu**
> — wszystkie jego property (`Wymiar`, `Stawka`, `RodzajStawki`, `TypStawki`, `Element`, `Kalendarz`,
> `Info`) mają `CanWrite == false`, a klasa **nie udostępnia publicznej metody `Save(...)`**.
> Z poziomu kodu biznesowego **nie da się** ustawić tych pól ani „utrwalić" obniżenia przez ten subrow.
> Pełny zapis stanu obniżenia (z metadanymi `ObniżenieWymiaruEtatuInfo`) realizują **workery platformy**.
> W zwykłym kodzie obniżenie sprowadzasz do ustawienia docelowego `Etat.Zaszeregowanie.Wymiar`
> (i ewentualnie `Stawka`) na nowym zapisie „od daty".

**Pola odczytowe — `Etat.ObnizenieEtatu: Soneta.Kadry.ObniżenieWymiaruEtatu` (subrow, read-only):**

| Dana | Pole / typ | Uwaga |
|---|---|---|
| Obniżony wymiar etatu | `ObnizenieEtatu.Wymiar: Soneta.Types.Fraction` | **read-only** |
| Obniżona stawka | `ObnizenieEtatu.Stawka: Soneta.Types.Currency` | **read-only** |
| Rodzaj / typ stawki | `ObnizenieEtatu.RodzajStawki`, `.TypStawki` | enumy, **read-only** |
| Kalendarz | `ObnizenieEtatu.Kalendarz: Soneta.Kalend.KalendarzBase` | referencja, **read-only** |
| Element wynagrodzenia | `ObnizenieEtatu.Element: Soneta.Place.DefinicjaElementu` | referencja, **read-only** |
| Zakres obniżenia (przełącznik) | `ObnizenieEtatu.Info: Soneta.Kadry.ObniżenieWymiaruEtatuInfo` | enum (`Brak`/`Wymiar`/`Stawka`/`Zaszeregowanie`/`Kalendarz`/…), **read-only** |

**Snippet (obniżenie wymiaru „od daty" w kodzie biznesowym):**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
var odDnia = new Date(2026, 7, 1);

using (var t = session.Logout(editMode: true))
{
    var ph = (PracHistoria)pracownik.Historia.Update(odDnia);
    pracownik.Module.PracHistorie.AddRow(ph);

    // Subrow ObnizenieEtatu jest read-only — NIE ustawiamy go bezpośrednio.
    // Docelowy wymiar po obniżeniu utrwalamy na Etat.Zaszeregowanie.Wymiar (pole zapisywalne):
    ph.Etat.Zaszeregowanie.Wymiar = new Fraction(4, 5);     // np. obniżenie do 4/5 etatu

    t.Commit();
}
session.Save();
```

**Workery platformy (seryjne, na zaznaczonych pracownikach, tabela `Pracownicy`):**
`ObniżWymiarEtatuWorker` (obniżenie: proporcjonalnie / do / o), `ZmianaStawkiZaszeregowaniaWorker`,
`ZmianaKalendarzaWorker`, `PrzywróćWarunkiZatrudnieniaWorker` (przywrócenie warunków sprzed
obniżenia). To one zakładają nowy zapis historii „od daty" i zapisują pełny stan obniżenia
(`ObniżenieWymiaruEtatuInfo`), którego nie da się ustawić przez publiczny kontrakt subrowa.

**Pułapki:**
- `Etat.ObnizenieEtatu` to **odczytowy delegat** do zapisu historii etatu — wszystkie property są
  read-only i klasa nie ma metody `Save(...)`. W kodzie biznesowym obniżenie wymiaru realizujesz
  ustawiając `Etat.Zaszeregowanie.Wymiar` (i `Stawka`) na nowym zapisie; pełny zapis stanu obniżenia
  z `ObniżenieWymiaruEtatuInfo` zostaw workerom platformy.
- Operacja jest „od daty" — zawsze przez nowy zapis (`Update` + `AddRow`); inaczej zmienisz wymiar
  wstecz w całym bieżącym okresie.
- Przywrócenie warunków to osobna operacja (`PrzywróćWarunkiZatrudnieniaWorker`) — nie polega na
  usuwaniu obniżenia, lecz na nowym zapisie z przywróconym wymiarem.

---

### B6 — Podzielniki kosztów (rozdział kosztów wynagrodzenia)

**Cel:** rozdzielić koszty wynagrodzenia pracownika na wydziały/projekty/centra kosztów wg
współczynników. Struktura: `Pracownik` jako **źródło** podzielnika →
`pracownik.Podzielniki: SubTable<Soneta.Core.PodzielnikKosztow>` → każdy podzielnik ma **historię**
`PodzielnikKosztow.Historia: HistorySubTable<HistoriaPodzielnika>` → a zapis historii ma kolekcję
`HistoriaPodzielnika.Elementy: SubTable<Soneta.Core.ElementPodzielnika>` (poszczególne udziały).

> Uwaga: `Pracownik.ElementyPodzielnika: SubTable<ElementPodzielnika>` to widok zbiorczy elementów
> ze wszystkich podzielników pracownika (do odczytu). **Tworzysz** elementy na konkretnym zapisie
> `HistoriaPodzielnika`, nie przez tę kolekcję.

**Tworzenie obiektów (konstruktory + AddRow):**

| Obiekt | Konstruktor | Tabela / AddRow |
|---|---|---|
| Podzielnik | `new PodzielnikKosztow(pracownik)` (pracownik jako `IZrodloPodzielnikaKosztow`) | `session.GetCore().PodzielKosztow.AddRow(p)` |
| Zapis historii | `podzielnik.Historia.Update(odDnia)` | `session.GetCore().HistPodzielnikow.AddRow(h)` |
| Element udziału | `new ElementPodzielnika(historia)` | `session.GetCore().ElemPodzielnikow.AddRow(e)` |

**Pola — `PodzielnikKosztow`:** `Nazwa: string`, `Definicja: Soneta.Core.DefinicjaPodzielnikaKosztow`,
`Zrodlo: IZrodloPodzielnikaKosztow` (pracownik, ustawiany ctorem), `Last/Historia`.
**Pola — `HistoriaPodzielnika`:** `Aktualnosc: FromTo` (okres zapisu, zarządzany), `Podstawa: decimal`,
`Elementy: SubTable<ElementPodzielnika>`.
**Pola — `ElementPodzielnika`:** `ElementPodzialowy: Soneta.Core.IElementSlownika` (cel rozdziału —
m.in. `Wydzial`, `Projekt`, `CentrumKosztow`, `OddzialFirmy` — iface-ref), `Wspolczynnik: double`,
`Procent: Percent` (kalkulowany z współczynników).

**Snippet:**

```csharp
var core = session.GetCore();
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
var odDnia = new Date(2026, 1, 1);
var wydzialA = session.GetKadry().Wydzialy.Firma;   // referencja do celu rozdziału (IElementSlownika)

using (var t = session.Logout(editMode: true))
{
    var podzielnik = new PodzielnikKosztow(pracownik);     // ctor wiąże ze źródłem (pracownik)
    core.PodzielKosztow.AddRow(podzielnik);
    podzielnik.Nazwa     = "Rozdział kosztów";
    // podzielnik.Definicja = ...                          // referencja do definicji (opcjonalnie)

    // zapis historii „od daty" (klon + AddRow)
    var historia = podzielnik.Historia.Update(odDnia);
    core.HistPodzielnikow.AddRow(historia);

    // udział: cel rozdziału + współczynnik
    var element = new ElementPodzielnika(historia);
    core.ElemPodzielnikow.AddRow(element);
    element.ElementPodzialowy = wydzialA;
    element.Wspolczynnik      = 100d;                       // Procent wyliczany z współczynników

    t.Commit();
}
session.Save();

// Odczyt zbiorczy elementów podzielnika pracownika:
foreach (ElementPodzielnika e in pracownik.ElementyPodzielnika)
{
    IElementSlownika cel = e.ElementPodzialowy;   // np. Wydzial / Projekt
    double wsp = e.Wspolczynnik;
}
```

**Pułapki:**
- Trójpoziomowa struktura — `PodzielnikKosztow` (root, źródło = pracownik) → `HistoriaPodzielnika`
  (historia „od daty") → `ElementPodzielnika` (udziały). Każdy poziom: konstruktor **+** `AddRow` do
  właściwej tabeli `Core`. Sam konstruktor nie wystarczy.
- `Historia.Update(odDnia)` + `HistPodzielnikow.AddRow` — para jak w A14; zmiana udziałów „od dnia"
  to nowy zapis historii (a wcześniej zwykle usunięcie/`Delete()` elementów starego zapisu przy
  aktualizacji tego samego okresu — patrz worker pracy zdalnej).
- `ElementPodzialowy` to **referencja interfejsowa** (`IElementSlownika`) — przypisz istniejący
  rekord (`Wydzial`, `Projekt`, `CentrumKosztow`, …), nie twórz „w locie".
- `Procent` jest kalkulowany z `Wspolczynnik` poszczególnych elementów — ustawiasz współczynniki,
  nie procenty.

---

### B7 — Aktualizacja danych wg definicji stanowiska (matrycy)

**Cel:** powiązać etat z definicją stanowiska i przejąć z niej parametry (stawka/grupa/wymiar,
kalendarz, kod zawodu). Definicja stanowiska to **matryca** — wzorzec wartości dla etatu.

**Pole na etacie:** `Etat.Definicja: Soneta.HR.DefinicjaStanowiska` (referencja do słownika
konfiguracyjnego `session.GetHR().DefStanowisk`). Pokrewne: `Etat.DefinicjaFunkcji: DefinicjaFunkcji`.

**Pola `DefinicjaStanowiska` (matryca, do skopiowania na etat):**

| Dana | Pole / typ |
|---|---|
| Nazwa / stanowisko | `Nazwa: string`, `Stanowisko: string`, `StanowiskoPelne: string` |
| Funkcja | `Funkcja: string`, `DefinicjaFunkcji: Soneta.HR.DefinicjaFunkcji` |
| Zaszeregowanie (wzorzec) | `Zaszeregowanie: Soneta.Kadry.Zaszeregowanie` (`Stawka`, `Wymiar`, `RodzajStawki`, `Element`, `WskaznikNazwa/Krotnosc`) |
| Typ stawki / grupa | `TypStawki: TypStawkiZaszeregowania`, `Grupa: Soneta.Kadry.GrupaZaszeregowania` |
| Kalendarz | `Kalendarz: Soneta.Kalend.Kalendarz`, `NieNadpisujKalendarza: bool` |
| Kod zawodu / praca w szcz. warunkach | `KodWykonywanegoZawodu`, `KodPracyWSzczWarunkach`, `InterpretacjaKalendarza` |

**Snippet:**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
var def = session.GetHR().DefStanowisk.WgNazwa["Specjalista ds. kadr"];   // matryca (referencja; klucz WgNazwa)
var odDnia = new Date(2026, 7, 1);

using (var t = session.Logout(editMode: true))
{
    var nowy = (PracHistoria)pracownik.Historia.Update(odDnia);
    pracownik.Module.PracHistorie.AddRow(nowy);

    var etat = nowy.Etat;
    etat.Definicja  = def;                       // powiązanie z definicją stanowiska
    etat.Stanowisko = def.Stanowisko;            // przeniesienie wartości z matrycy
    etat.Zaszeregowanie.Wymiar = def.Zaszeregowanie.Wymiar;
    etat.Zaszeregowanie.Stawka = def.Zaszeregowanie.Stawka;

    t.Commit();
}
session.Save();
```

**Pułapki:**
- `Etat.Definicja` to **referencja** do rekordu konfiguracyjnego `DefStanowisk` — pobierz istniejącą
  (`session.GetHR().DefStanowisk`), nie twórz „w locie". Indeks po nazwie to `WgNazwa`
  (**nie `WgNazwy`**); w bazie Demo słownik bywa pusty — zabezpiecz się na brak definicji.
- Definicja jest matrycą — przeniesienie wartości (stawka/wymiar/kalendarz) na etat zrób jawnie;
  samo wskazanie `Etat.Definicja` nie nadpisuje automatycznie wszystkich pól etatu w kodzie biznesowym.
- Dostępność definicji potrafi zależeć od konfiguracji (`DefinicjeStanowiskDlaWydziałów`) — definicja
  może być filtrowana po wydziale.
- Zmiana stanowiska „od dnia" to nowy zapis historii (A14), nie nadpisanie bieżącego.

## C. Dodatki, potrącenia, akordy

### C1 — Dodatki / stałe elementy wynagrodzenia (★)

**Cel:** przypisać pracownikowi stały element wynagrodzenia (dodatek — np. premia, dodatek
funkcyjny), oparty o definicję elementu płacowego, z okresem obowiązywania i parametrami
(podstawa/procent/czas). W UI: menu czynności *Dodatki i potrącenia → Dodaj nowy*.

**Klasa i model:** `Soneta.Kadry.Dodatek` — `GuidedRow` root, tabela `Dodatki`, obiekt
**historyczny** (kolekcja `Dodatek.Historia: HistorySubTable<Soneta.Kadry.DodHistoria>`, parametry
„od–do" siedzą w zapisach `DodHistoria`). Dodatek jest childem pracownika i pojawia się w
`pracownik.Dodatki: SubTable<Soneta.Kadry.Dodatek>`.

**Tworzenie:** `new Dodatek(pracownik)` + `session.GetKadry().Dodatki.AddRow(d)`. Dodanie do tabeli
tworzy **pierwszy zapis** `DodHistoria` — dostępny od razu jako `d.Last`. Parametry ustawiamy na
`d.Last`.

**Pola i typy (`DodHistoria` — `d.Last`):**

| Pole | Typ | Uwaga |
|---|---|---|
| `Element` | `Soneta.Place.DefinicjaElementu` | definicja elementu wynagrodzenia (wymagana); pobierz istniejącą z `session.GetPlace().DefElementow.WgNazwy[nazwa]` |
| `Okres` | `Soneta.Types.FromTo` | okres obowiązywania dodatku |
| `Podstawa` | `Soneta.Types.Currency` | kwota podstawy (gdy algorytm definicji jej wymaga) |
| `Procent` | `Soneta.Types.Percent` | procent (gdy algorytm definicji go wymaga) |
| `Czas` | `Soneta.Types.Time` | czas (gdy algorytm definicji go wymaga) |
| `Ulamek` | `Soneta.Types.Fraction` | ułamek (zależnie od definicji) |
| `Dni` | `int` | liczba dni (zależnie od definicji) |
| `Aktualnosc` | `Soneta.Types.FromTo` | okres zapisu historycznego (zarządzany przez historię — nie ustawiaj ręcznie) |

**Pola na rootcie `Dodatek`:** `Nazwa: string`, `Pracownik: Soneta.Kadry.Pracownik` (właściciel,
ustawiany ctorem), `DataZakonczeniaWyplaty: Date`, `Last: DodHistoria`,
`Historia: HistorySubTable<DodHistoria>`, `Dodatki` (tabela: `session.GetKadry().Dodatki`).

**Pobranie definicji elementu:** słownik `session.GetPlace().DefElementow` (kolekcja konfiguracyjna).
Indeksowanie po nazwie: `DefElementow.WgNazwy["Premia"]`. Definicje dodatków mają
`RodzajZrodla == Soneta.Place.RodzajŹródłaWypłaty.Dodatek` — można nimi filtrować dostępne
definicje. W bazie Demo istnieją gotowe definicje, m.in. `"Premia"`, `"Premia procentowa"`.

**Pułapki:**
- **`new Dodatek(pracownik)` + `Dodatki.AddRow(d)` to para** — sam konstruktor nie włącza dodatku do
  sesji ani nie tworzy zapisu historii. Pierwszy `DodHistoria` powstaje przy `AddRow`; dopiero potem
  istnieje `d.Last`.
- `Podstawa`/`Procent`/`Czas` **mogą być tylko-do-odczytu** w zależności od algorytmu wskazanej
  `DefinicjaElementu` — element kwotowy udostępnia `Podstawa`, element procentowy `Procent` itd.
  Ustawiaj tylko te pola, których wymaga definicja (próba zapisu pola read-only rzuci wyjątek).
- `Element` jest **wymagany** — bez wskazania definicji elementu dodatek nie ma sensu płacowego.
  Definicję pobierasz z istniejącego słownika (`DefElementow`), nie tworzysz „w locie" w tym scenariuszu.
- Zmiana parametrów dodatku **od konkretnego dnia** to nowy zapis historii dodatku
  (`d.Historia.Update(date)` + `Dodatki.Module.DodHistorie.AddRow(nowy)`), analogicznie do A14 — nie
  nadpisuj bieżącego zapisu, jeśli chcesz zachować poprzedni okres.
- `DodHistoria.Aktualnosc` (okres zapisu) zarządza mechanizm historii — sam ustawiasz `Okres`,
  `Aktualnosc` zostaw historii.

**Snippet:**

```csharp
var kadry = session.GetKadry();
var pracownik = kadry.Pracownicy.WgKodu["006"];

// Definicja elementu wynagrodzenia ze słownika konfiguracyjnego (po nazwie):
var definicjaPremii = session.GetPlace().DefElementow.WgNazwy["Premia"];

using (var t = session.Logout(editMode: true))
{
    // new Dodatek(pracownik) + AddRow — AddRow tworzy pierwszy zapis DodHistoria (d.Last):
    var dodatek = new Dodatek(pracownik);
    kadry.Dodatki.AddRow(dodatek);

    var h = dodatek.Last;                                   // pierwszy zapis historii dodatku
    h.Element  = definicjaPremii;                          // definicja elementu (wymagana)
    h.Okres    = new FromTo(new Date(2026, 1, 1), Date.MaxValue);
    h.Podstawa = (Currency)500m;                           // gdy algorytm definicji wymaga podstawy

    t.Commit();
}
session.Save();

// Odczyt dodatków pracownika i ich definicji elementu (kolekcja childów):
foreach (Dodatek d in pracownik.Dodatki)
{
    DefinicjaElementu element = d.Last.Element;
    FromTo okres = d.Last.Okres;
}
```

### C2 — Potrącenia (stałe i jednorazowe) (★)

**Cel:** przypisać pracownikowi potrącenie z wynagrodzenia (np. składka związkowa, spłata
rozliczana ręcznie, potrącenie dobrowolne). W modelu płacowym **potrącenie nie ma osobnej klasy** —
to **`Soneta.Kadry.Dodatek`** (jak C1), tyle że oparty o **definicję elementu o charakterze
potrącenia**. O „minusowym" charakterze decyduje wyłącznie wskazana `DefinicjaElementu` (jej algorytm),
a nie typ obiektu po stronie pracownika.

**Jak rozpoznać definicję potrącenia (`Soneta.Place.DefinicjaElementu`, słownik `DefElementow`):**

| Pole definicji | Typ | Znaczenie dla potrącenia |
|---|---|---|
| `Algorytm.Potracenie` | `bool` | **kluczowy znacznik** — `true` dla elementu potrącającego (element pomniejsza wynagrodzenie) |
| `Algorytm.LimitPotracenia` | `Soneta.Place.TypLimituPotrącenia` | rodzaj limitu (np. do kwoty wolnej) — gdy potrącenie limitowane |
| `Algorytm.TylkoPelnePotracenie` | `bool` | potrącać tylko w pełnej kwocie (bez częściowego) |
| `RodzajZrodla` | `Soneta.Place.RodzajŹródłaWypłaty` | dla potrącenia przez dodatek **musi być** `Dodatek` (= `6`); enum **nie ma** wartości „Potrącenie" (ma natomiast m.in. `ZajęcieKomornicze` = 23, `Świadczenie` = 12, `Pożyczka` = 18, `PożyczkaSpłata` = 19). Minus realizuje algorytm, ale `DodHistoria.Element` **odrzuca** definicje o `RodzajZrodla != Dodatek` (np. „Alimenty" jako `ZajęcieKomornicze`) — patrz pułapki |

**Mechanizm — identyczny jak C1 (Dodatek + DodHistoria):**
- `new Dodatek(pracownik)` + `session.GetKadry().Dodatki.AddRow(d)` → powstaje pierwszy `DodHistoria`
  (`d.Last`).
- Na `d.Last` ustawiamy `Element` (definicja potrącenia), `Okres` oraz `Podstawa`/`Procent`/`Kwota`
  zależnie od algorytmu definicji.
- **Potrącenie stałe**: `Okres` otwarty (do `Date.MaxValue`) lub na czas określony — naliczane w każdej
  wypłacie z okresu.
- **Potrącenie jednorazowe**: `Okres` zawężony do jednego miesiąca rozliczeniowego (tylko ten miesiąc
  obejmie naliczenie).
- Zakończenie potrącenia: `d.DataZakonczeniaWyplaty` + ewentualnie `d.PrzyczynaZakonczenia`, albo nowy
  zapis historii „od daty" (`d.Historia.Update(date)`), analogicznie do C1/A14.

**Pułapki:**
- Nie szukaj klasy „Potrącenie" — jej **nie ma**. Potrącenie = `Dodatek` z definicją, w której
  `Algorytm.Potracenie == true`. Dobór definicji jest jedynym wyróżnikiem.
- **Filtruj po DWÓCH warunkach** (zweryfikowane testem): `d.Algorytm.Potracenie && d.RodzajZrodla ==
  RodzajŹródłaWypłaty.Dodatek`. Sam `Algorytm.Potracenie` **nie wystarcza** — przy ustawianiu
  `DodHistoria.Element` definicja o innym `RodzajZrodla` (np. „Alimenty" jako `ZajęcieKomornicze`)
  rzuca `System.Exception: "Zły rodzaj źródła wypłaty elementu …"`. Element zajęcia komorniczego ma
  `RodzajZrodla == ZajęcieKomornicze` i podpinasz go pod `ZajęcieKomornicze`, nie pod `Dodatek` (C4).
- `Podstawa`/`Procent`/`Czas` na `DodHistoria` bywają tylko-do-odczytu zależnie od algorytmu definicji
  (jak w C1) — ustawiaj tylko te, których definicja wymaga.
- `Element` wymagany; pobierany z istniejącego słownika `DefElementow`, nie tworzony „w locie".

**Snippet:**

```csharp
var kadry = session.GetKadry();
var pracownik = kadry.Pracownicy.WgKodu["006"];

// Definicja potrącenia ze słownika — DWA warunki: Potracenie ORAZ RodzajZrodla == Dodatek:
var def = session.GetPlace().DefElementow.Cast<DefinicjaElementu>()
    .First(d => d.RodzajZrodla == RodzajŹródłaWypłaty.Dodatek
                && d.Algorytm != null && d.Algorytm.Potracenie);

using (var t = session.Logout(editMode: true))
{
    var potracenie = new Dodatek(pracownik);
    kadry.Dodatki.AddRow(potracenie);                       // tworzy pierwszy DodHistoria

    var h = potracenie.Last;
    h.Element = def;                                        // definicja o Algorytm.Potracenie == true
    h.Okres   = new FromTo(new Date(2026, 1, 1), Date.MaxValue);   // stałe
    h.Podstawa = (Currency)50m;                             // gdy algorytm definicji wymaga kwoty

    t.Commit();
}
session.Save();
```

---

### C3 — Akordy (★)

**Cel:** przypisać pracownikowi pracę akordową (rozliczaną wg ilości/strefy), z okresem i definicją
akordu; zakończyć akord. W UI: menu czynności *Akordy → Dodaj nowy / Zakończ*.

**Klasa i model:** `Soneta.Kadry.Akord` — `GuidedRow` root, tabela `Akordy`, obiekt **historyczny**
(`Akord.Historia: HistorySubTable<Soneta.Kadry.AkordHistoria>`; parametry „od–do" w zapisach
`AkordHistoria`, dostęp do bieżącego przez `Akord.Last`). Akord jest childem pracownika:
`pracownik.Akordy: SubTable<Soneta.Kadry.Akord>`.

**Pola root `Akord`:**

| Pole | Typ | Uwaga |
|---|---|---|
| `Pracownik` | `Soneta.Kadry.Pracownik` | właściciel (relacja) |
| `Definicja` | `Soneta.Kadry.DefinicjaAkordu` | definicja akordu (słownik `DefinicjeAkordow`) |
| `Okres` | `Soneta.Types.FromTo` | okres obowiązywania akordu |
| `Typ` | `Soneta.Kadry.TypAkordu` | typ akordu |
| `Wydzial` | `Soneta.Kadry.Wydzial` | jednostka organizacyjna realizacji |
| `Last` | `Soneta.Kadry.AkordHistoria` | bieżący zapis historii |
| `Dni` | `DateSubTable<Soneta.Kalend.DzienAkorduBase>` | dzienna realizacja akordu |

**Pola `AkordHistoria` (`Akord.Last`):** `Okres: FromTo`, `Algorytm: Soneta.Kadry.AlgorytmAkordu`
(subrow z `Algorytm.Element: DefinicjaElementu`, `Algorytm.Wspolczynnik`, `Algorytm.Progi`,
`Algorytm.WgCzasu`/`Progresywny` itd.), `Jednostka: string`, `Aktualnosc: FromTo` (zarządzane przez
historię), `Progi: SubTable<Soneta.Kadry.ProgAkordu>`.

**Tworzenie — brak publicznego konstruktora `Akord(pracownik)`.** Akord dodaje się **workerem**
operacyjnym (kanonicznie), nie `new`. Konstruktor `Akord` jest niepubliczny (poza `RowCreator`).
Worker jest „jak z UI" (`Params` dziedziczy z `ContextBase`, ctor wymaga `Context`) — uruchamiaj go w
transakcji `CommitUI`.

**Workery (zagnieżdżone w `Pracownik`):** ctor `(Session)`, parametry przez właściwości `Pars`/`Pracownicy`;
`Params` ma ctor `(Context)`.

| Worker | Metoda | Wzorzec użycia |
|---|---|---|
| `Soneta.Kadry.Pracownik.DodajAkordWorker` | `DodajAkord` | `new Params(ctx) { Definicja, OdDnia, DoDnia, DodajKolejny }`; `new DodajAkordWorker(session) { Pars = par, Pracownicy = tab }` |
| `Soneta.Kadry.Pracownik.ZakończAkordWorker` | `ZakończAkord` | `new Params(ctx) { Definicja, DoDnia, ZakończWszystkie }`; `new ZakończAkordWorker(session) { Pars = par, Pracownicy = tab }` |

**Pułapki:**
- Akordu **nie twórz przez `new Akord(...)`** — kanoniczna ścieżka to `DodajAkordWorker` (analogicznie
  `ZakończAkordWorker` do zakończenia). Workery przyjmują **tablicę pracowników**, więc nadają się też do
  operacji grupowej.
- `Definicja` (akordu) to rekord słownika `DefinicjeAkordow` — pobierz istniejący, nie twórz „w locie".
  Sam akord wiąże dopiero z `DefinicjaElementu` (płacowym) przez `Algorytm.Element` definicji akordu.
- Akord jest historyczny — zmiana parametrów „od daty" to nowy zapis `AkordHistoria`
  (`Historia.Update(date)`), analogicznie do C1/A14.
- Tabela `Akordy` to dane operacyjne — przy przeglądaniu poprzecznym filtruj zakresem (safe-code §6.3);
  w zakresie jednego pracownika korzystaj z `pracownik.Akordy`.

**Snippet:**

```csharp
var kadry = session.GetKadry();
var pracownik = kadry.Pracownicy.WgKodu["006"];
var defAkordu = kadry.DefinicjeAkordow.WgNazwa["Akord prosty"];   // klucz WgNazwa (l.poj.)
var context = login.CreateEmptyContext().Clone(session);

using (var t = session.Logout(editMode: true))
{
    var par = new Pracownik.DodajAkordWorker.Params(context)   // Params: ctor (Context)
    {
        Definicja = defAkordu,
        OdDnia    = new Date(2026, 1, 1),
        DoDnia    = new Date(2026, 12, 31),
    };
    // ctor (Session); parametry przez właściwości Pars/Pracownicy:
    new Pracownik.DodajAkordWorker(session) { Pars = par, Pracownicy = new[] { pracownik } }.DodajAkord();
    t.CommitUI();
}
session.Save();

// Odczyt akordów pracownika:
foreach (Akord a in pracownik.Akordy)
{
    DefinicjaAkordu def = a.Definicja;
    FromTo okres = a.Okres;
    DefinicjaElementu element = a.Last.Algorytm.Element;
}
```

---

### C4 — Zajęcia wynagrodzenia (komornicze, alimentacyjne) (★)

**Cel:** zarejestrować zajęcie wynagrodzenia (egzekucja komornicza lub alimentacyjna) z numerem sprawy,
kwotą, priorytetem i wierzycielem (komornikiem/rachunkiem odbiorcy); anulować/przywrócić zajęcie.

**Klasa i model:** `Soneta.Kadry.ZajęcieKomornicze` — `GuidedRow` root, tabela `ZajKomornicze`, obiekt
**historyczny** (`Historia: HistorySubTable<Soneta.Kadry.ZajęcieKomorniczeHistoria>`; limity i kwoty
„od–do" w zapisach historii, bieżący przez `Last`). Child pracownika:
`pracownik.ZajęciaKomornicze: SubTable<Soneta.Kadry.ZajęcieKomornicze>`. **Konstruktor publiczny:**
`new ZajęcieKomornicze(pracownik)`.

**Pola root `ZajęcieKomornicze`:**

| Pole | Typ | Uwaga |
|---|---|---|
| `Pracownik` | `Soneta.Kadry.Pracownik` | właściciel |
| `Rodzaj` | `Soneta.Kadry.RodzajeZajęciaWynagrodzenia` | enum: `Kwota = 0`, `KwotaMiesięczna = 1` (jednorazowa kwota vs miesięczna) |
| `Element` | `Soneta.Place.DefinicjaElementu` | element płacowy zajęcia — **wymagany**; musi mieć `RodzajZrodla == ZajęcieKomornicze` (= 23) |
| `NumerSprawy` | `string` | numer sprawy egzekucyjnej |
| `Data` | `Soneta.Types.Date` | data zajęcia |
| `DataSplaty` | `Soneta.Types.Date` | data spłaty/zakończenia |
| `Rozliczenie.Odbiorca` | `Soneta.Kasa.IPodmiotKasowy` | **wierzyciel** — komornik/odbiorca (iface; może być `Kontrahent`, `Bank`, `Pracownik`, `UrzadSkarbowy`…) |
| `Rozliczenie.RachunekOdbiorcy` | `Soneta.Kasa.RachunekBankowyPodmiotu` | rachunek wierzyciela do przelewu |
| `Splacono` | `Soneta.Types.Currency` | kwota spłacona (kalkulowane/narastające) |
| `Pozostało` | `Soneta.Types.Currency` | kwota pozostała (kalkulowane) |
| `SplataZakonczona` | `bool` | spłata zakończona |
| `Anulowane` | `bool` | zajęcie anulowane (patrz workery) |
| `Korekty` | `SubTable<Soneta.Kadry.KorektaZajęciaKomorniczego>` | korekty zajęcia |
| `OpisPrzelewu` | `string` | tytuł przelewu |

**Limity i kwoty — na zapisie `ZajęcieKomorniczeHistoria` (`Last`):** kwota do potrącenia, limity
procentowe i kwotowe, zawieszenie spłaty, priorytet, ustawienia potrąceń z zasiłków/świadczeń (zmiana
„od daty" = nowy zapis historii).

**Workery (zagnieżdżone w `ZajęcieKomornicze`):** ctor **bezparametrowy**, parametr przez właściwość `Zajęcie`.

| Worker | Metoda | Wzorzec użycia |
|---|---|---|
| `Soneta.Kadry.ZajęcieKomornicze.AnulujWorker` | `Anuluj` | `new ZajęcieKomornicze.AnulujWorker { Zajęcie = zaj }.Anuluj()` |
| `Soneta.Kadry.ZajęcieKomornicze.PrzywrócWorker` | `Przywróć` | `new ZajęcieKomornicze.PrzywrócWorker { Zajęcie = zaj }.Przywróć()` |

**Pułapki:**
- **Pole `Priorytet` NIE istnieje** na `ZajęcieKomornicze` (sprostowanie). **Alimentacyjne vs
  niealimentacyjne** rozstrzyga konfiguracja: wskazana `DefinicjaElementu` (`RodzajZrodla ==
  ZajęcieKomornicze`) i parametry zapisu historii (limity), nie osobny typ klasy — to **jedna klasa**
  `ZajęcieKomornicze`.
- `Anulowane` jest **tylko-do-odczytu** (brak publicznego settera) — anuluj **workerem** `AnulujWorker`.
- `Rozliczenie.Odbiorca` jest **interfejsem** `IPodmiotKasowy` — wskaż istniejący podmiot (zwykle
  `Kontrahent`-komornik); nie twórz odbiorcy „w locie" w tym scenariuszu.
- Faktyczne **kwoty potrącenia (`Splacono`, `Pozostało`) wyliczają się przy naliczeniu wypłaty** — po
  samym dodaniu zajęcia są zerowe/wyjściowe. Pełne rozliczenie wymaga naliczonej wypłaty (patrz sekcja
  „niewykonalne publicznym API bez naliczenia").
- Anulowanie/przywracanie realizuj **workerami** (`AnulujWorker`/`PrzywrócWorker`), nie ręcznym
  ustawianiem `Anulowane` — workery dbają o storna i spójność rozliczenia.
- Tabela operacyjna — przegląd poprzeczny z filtrem (safe-code §6.3).

**Snippet:**

```csharp
var kadry = session.GetKadry();
var pracownik = kadry.Pracownicy.WgKodu["006"];
// Element zajęcia — definicja o RodzajZrodla == ZajęcieKomornicze (nie zwykłe potrącenie-Dodatek):
var elementZajecia = session.GetPlace().DefElementow.Cast<DefinicjaElementu>()
    .First(d => d.RodzajZrodla == RodzajŹródłaWypłaty.ZajęcieKomornicze);
var komornik = session.GetCRM().Kontrahenci.WgKodu["KOMORNIK1"];   // wierzyciel (IPodmiotKasowy)

using (var t = session.Logout(editMode: true))
{
    var zajecie = new ZajęcieKomornicze(pracownik);      // ctor publiczny
    kadry.ZajKomornicze.AddRow(zajecie);

    zajecie.Rodzaj     = RodzajeZajęciaWynagrodzenia.KwotaMiesięczna;
    zajecie.Element    = elementZajecia;                 // wymagany (RodzajZrodla == ZajęcieKomornicze)
    zajecie.NumerSprawy = "KM 123/2026";
    zajecie.Data       = new Date(2026, 1, 1);
    zajecie.Rozliczenie.Odbiorca = komornik;             // wierzyciel
    zajecie.Rozliczenie.RachunekOdbiorcy = komornik.Rachunki.WgKodu["GŁÓWNY"];

    t.Commit();
}
session.Save();

// Anulowanie zajęcia (worker bezparametrowy + property Zajęcie, nie ręczna flaga):
using (var t = session.Logout(editMode: true))
{
    var zaj = pracownik.ZajęciaKomornicze.First();
    new ZajęcieKomornicze.AnulujWorker { Zajęcie = zaj }.Anuluj();
    t.CommitUI();
}
session.Save();
```

---

### C5 — Operacje seryjne na dodatkach (moduł Przeszeregowania) (★)

**Cel:** dodać / zmienić / zakończyć dodatek (oraz zmienić stawkę) dla **grupy pracowników** jedną
operacją. Realizuje to moduł **`Soneta.Przeszeregowania.PrzeszeregowaniaModule`**. Dokumentem zbiorczym
jest `Przeszeregowanie` (tabela `Przeszeregowania`, root) z pozycjami `ElementPrzeszeregowania`
(tabela `ElementyPrzeszer`, child). Pracownik widzi swoje pozycje przez
`pracownik.ElementyPrzeszeregowania`.

**Workery operacyjne** — ctor **bezparametrowy**, parametry przez właściwości `Pars` (typu `Params`,
ctor `(Context)`) i `Pracownicy: Pracownik[]`. Uruchamiaj w transakcji `CommitUI`. **Uwaga:** workery
te w bezgłowym hoście testowym (bez operatora/kontekstu UI) rzucają `NullReferenceException` — wymagają
realnego środowiska aplikacji.

| Worker | Metoda | Params (publiczne pola) | Działanie |
|---|---|---|---|
| `Soneta.Przeszeregowania.NowyDodatekWorker` | `NowyDodatek` | `Definicja: DefinicjaElementu, Podstawa: Currency, Procent: Percent` | wypłata/nadanie nowego dodatku grupie |
| `Soneta.Przeszeregowania.ZmianaDodatkuWorker` | `ZmianaDodatku` | `Definicja, Podstawa, ZmianaPodstawy: Currency, ProcentowaZmianaPodstawy: Percent, Procent, ZmianaProcentu: Percent, DataStawki: Date, PodstawaPrecyzja, PodstawaSposob` | zmiana parametrów istniejącego dodatku |
| `Soneta.Przeszeregowania.ZakończDodatekWorker` | `ZakończDodatek` | `Definicja: DefinicjaElementu` | zakończenie wypłaty dodatku |
| `Soneta.Przeszeregowania.DodajZmienDodatekWorker` | `DodajZmienDodatek` | `Params` (dodanie lub zmiana łącznie) | dodanie albo zmiana dodatku |
| `Soneta.Przeszeregowania.DodajNagrodęWorker` | (nagroda) | — | seryjne nagrody |
| `Soneta.Przeszeregowania.ZmianaStawkiWorker` | `ZmianaStawki` | — | seryjna zmiana stawki zaszeregowania |

**Dokument `Przeszeregowanie` (alternatywa: zbuduj dokument i wykonaj).** Tworzenie: `new
Przeszeregowanie()` + `session.GetPrzeszeregowania().Przeszeregowania.AddRow(doc)` (kolekcja **nie ma**
`AddNew` — to standardowy `GuidedRow` root z publicznym ctorem bezparametrowym).

| Pole | Typ |
|---|---|
| `Data` | `Soneta.Types.Date` (data przeszeregowania) |
| `DataWykonania` | `Soneta.Types.Date` |
| `Nazwa` | `string` |
| `Realizacja` | `Soneta.Przeszeregowania.RealizacjaPrzeszeregowania` (stan) |
| `Pracownicy` | `ICollection<Soneta.Kadry.Pracownik>` |
| `Elementy` | `SubTable<Soneta.Przeszeregowania.ElementPrzeszeregowania>` |
| `ZarzadzaneWnioskiem` | `bool` |

`ElementPrzeszeregowania` (child) niesie zmianę per pracownik: `Definicja: DefinicjaElementu`,
`Kwota`/`ZmianaKwoty`/`ProcentowaZmianaKwoty`, `Procent`/`ZmianaProcentu`, `Grupa: GrupaZaszeregowania`,
`Krotnosc`/`ZmianaKrotnosci`, `RodzajPrzeszergowania`, `Pracownik`, `PracHistoria`.

**Wykonanie dokumentu:** `Soneta.Przeszeregowania.Przeszeregowanie.WykonajWorker` (metoda `Wykonaj`,
`Params { Wykonaj: bool }`) — materializuje dokument na danych pracowników (tworzy/zmienia dodatki).
`ElementPrzeszeregowania.Wykonaj(Log)` realizuje pojedynczą pozycję.

**Pułapki:**
- To **operacja seryjna na danych operacyjnych** — trzymaj transakcje krótkie, duże grupy dziel na paczki
  (safe-code §13.1). Workery przyjmują tablicę pracowników — przekaż przefiltrowaną listę (po stronie
  serwera, safe-code §6).
- Workery `NowyDodatek`/`ZmianaDodatku`/`ZakończDodatek` operują na **definicji elementu** (`Definicja`),
  więc wybór właściwej `DefinicjaElementu` jest kluczowy (po nazwie / `RodzajZrodla == Dodatek`).
- Sam dokument `Przeszeregowanie` **nie zmienia danych** dopóki nie zostanie wykonany (`WykonajWorker`);
  do tego momentu to plan. Po `Wykonaj` zmiany trafiają w dodatki/etat pracowników.
- Indywidualne (jednostkowe) odpowiedniki to workery z C2/C1 na pojedynczym pracowniku
  (`Pracownik.DodajDodatekWorker`/`ZmieńDodatekWorker`/`ZabierzDodatekWorker`); moduł Przeszeregowania
  jest dla **grupy**.

**Snippet (operacja seryjna — nowy dodatek dla grupy):**

```csharp
var kadry = session.GetKadry();
var def = session.GetPlace().DefElementow.WgNazwy["Premia"];

// Grupa pracowników — filtr serwerowy (np. po wydziale), nie pełny skan:
Pracownik[] grupa = kadry.Pracownicy[(Pracownik p) => p.Last.Etat.Okres.Contains(Date.Today)]
                          .Cast<Pracownik>().ToArray();

var context = login.CreateEmptyContext().Clone(session);

using (var t = session.Logout(editMode: true))
{
    var par = new NowyDodatekWorker.Params(context)   // Params: ctor (Context)
    {
        Definicja = def,
        Podstawa  = (Currency)300m,
    };
    // ctor bezparametrowy; parametry przez właściwości Pars/Pracownicy:
    new NowyDodatekWorker { Pars = par, Pracownicy = grupa }.NowyDodatek();
    t.CommitUI();
}
session.Save();
```

---

### C6 — Świadczenia socjalne (ZFŚS) i ich rozliczenie (★)

**Cel:** przyznać pracownikowi świadczenie socjalne z ZFŚS (zapomoga, dopłata do wypoczynku, paczka)
i ustawić jego rozliczenie płacowe (element, kwota, okres).

**Klasa i model:** `Soneta.Kadry.SwiadczSocjalne` — `GuidedRow` root, tabela `SwiadczeniaSoc`. Child
pracownika: `pracownik.Swiadczenia: SubTable<Soneta.Kadry.SwiadczSocjalne>`. **Konstruktor publiczny:**
`new SwiadczSocjalne(pracownik)`.

**Pola `SwiadczSocjalne`:**

| Pole | Typ | Uwaga |
|---|---|---|
| `Pracownik` | `Soneta.Kadry.Pracownik` | właściciel |
| `Definicja` | `Soneta.Kadry.DefinicjaŚwiadczeniaSocjalnego` | rodzaj świadczenia (słownik `DefSwiadczSocjal`) |
| `Data` | `Soneta.Types.Date` | data przyznania |
| `Nazwa` | `string` | nazwa |
| `Opis` | `Soneta.Business.MemoText` | opis |
| `Rozliczenie` | `Soneta.Kadry.RozliczenieSwiadczenia` (subrow) | dane rozliczeniowe (poniżej) |

**Subrow `Rozliczenie` (`RozliczenieSwiadczenia`):**

| Pole | Typ | Uwaga |
|---|---|---|
| `Rozliczenie.Element` | `Soneta.Place.DefinicjaElementu` | element płacowy do rozliczenia świadczenia |
| `Rozliczenie.Kwota` | `Soneta.Types.Currency` | kwota świadczenia |
| `Rozliczenie.Okres` | `Soneta.Types.FromTo` | okres rozliczenia |
| `Rozliczenie.Data` | `Soneta.Types.Date` | data rozliczenia |
| `Rozliczenie.Rozliczone` | `bool` | czy rozliczone (po naliczeniu wypłaty) |

**Definicja (`DefinicjaŚwiadczeniaSocjalnego`, słownik `DefSwiadczSocjal`):** `Nazwa: string`,
`Element: DefinicjaElementu` (domyślny element rozliczenia), `Kwota: Currency` (domyślna kwota). Z niej
dziedziczy świadczenie domyślny element i kwotę.

**Worker seryjny:** `Soneta.Kadry.SwiadczSocjalne.DodajŚwiadczenieWorker` (metoda `DodajŚwiadczenie`) —
ctor **bezparametrowy**, parametry przez właściwości `Pars` i `Pracownicy: Pracownik[]`; `Params` ma
ctor `(Context)`: `Params { Definicja: DefinicjaŚwiadczeniaSocjalnego, DataPrzyznania: Date, Kwota:
Currency, Element: DefinicjaElementu, DataRozliczenia: Date }` — nadaje świadczenie grupie (menu
*Operacje seryjne / Dodaj świadczenia socjalne*). Wzorzec:
`new DodajŚwiadczenieWorker { Pars = new …Params(ctx){…}, Pracownicy = tab }.DodajŚwiadczenie()`.

**Pułapki:**
- `Definicja` (świadczenia) pobierana ze słownika `DefSwiadczSocjal`; jej `Element`/`Kwota` są domyślne —
  na konkretnym świadczeniu nadpisujesz przez `Rozliczenie.Element`/`Rozliczenie.Kwota`.
- **Faktyczne rozliczenie (wypłata świadczenia, `Rozliczenie.Rozliczone == true`) następuje przy
  naliczeniu wypłaty** — samo dodanie świadczenia tworzy tylko zlecenie rozliczenia.
- Dla grupy używaj `DodajŚwiadczenieWorker`; pojedynczo — `new SwiadczSocjalne(pracownik)` + `AddRow`.

**Snippet:**

```csharp
var kadry = session.GetKadry();
var pracownik = kadry.Pracownicy.WgKodu["006"];
var defSwiadcz = kadry.DefSwiadczSocjal.WgNazwy["Dopłata do wypoczynku"];
var element = session.GetPlace().DefElementow.WgNazwy["Świadczenie socjalne"];

using (var t = session.Logout(editMode: true))
{
    var sw = new SwiadczSocjalne(pracownik);
    kadry.SwiadczeniaSoc.AddRow(sw);

    sw.Definicja = defSwiadcz;
    sw.Data      = new Date(2026, 6, 1);
    sw.Rozliczenie.Element = element;                  // element płacowy rozliczenia
    sw.Rozliczenie.Kwota   = (Currency)1000m;
    sw.Rozliczenie.Okres   = FromTo.Month(new YearMonth(2026, 6));

    t.Commit();
}
session.Save();

// Odczyt świadczeń pracownika:
foreach (SwiadczSocjalne s in pracownik.Swiadczenia)
{
    Currency kwota = s.Rozliczenie.Kwota;
    bool rozliczone = s.Rozliczenie.Rozliczone;
}
```

---

### C7 — Pożyczki (KZP / ZFM) (★)

**Cel:** zarejestrować członkostwo pracownika w funduszu pożyczkowym, udzielić pożyczki, zbudować
harmonogram rat i potrącać raty z wynagrodzenia.

**Hierarchia obiektów (wszystkie `GuidedRow` root, childy pracownika):**
- `Soneta.Kadry.FundPozyczkowy` (tabela `FundPozyczkowe`) — **członkostwo** w funduszu;
  `pracownik.FunduszePozyczkowe: SubTable<Soneta.Kadry.FundPozyczkowy>`. Ctor:
  `new FundPozyczkowy(pracownik, definicja)`.
- `Soneta.Kadry.Pozyczka` (tabela `Pozyczki`) — **pożyczka** udzielona w ramach funduszu; kolekcja
  `fundusz.Pozyczki: SubTable<Soneta.Kadry.Pozyczka>`. Ctor: `new Pozyczka(fundusz)`.
- `Soneta.Kadry.RataPozyczki` (tabela `RatyPozyczek`) — **rata** harmonogramu; `pozyczka.Raty:
  SubTable<Soneta.Kadry.RataPozyczki>`. Raty pracownik widzi przez `pracownik.SplacaneRaty`
  (oraz `ZyrowaneRaty` jako żyrant). Ctor: `new RataPozyczki(pozyczka)`.
- `Soneta.Kadry.DefinicjaFunduszuPozyczkowego` (słownik `DefFundPozycz`, konfiguracyjny) — zasady
  funduszu (oprocentowanie, elementy płacowe wpisowego/składki/wycofania).

**Pola `Pozyczka` (kluczowe):**

| Pole | Typ | Uwaga |
|---|---|---|
| `Fundusz` | `Soneta.Kadry.FundPozyczkowy` | fundusz, w ramach którego udzielono |
| `Data` | `Soneta.Types.Date` | data udzielenia |
| `Kwota` | `Soneta.Types.Currency` | kwota pożyczki |
| `Element` | `Soneta.Place.DefinicjaElementu` | element wypłaty pożyczki |
| `ElementRaty` | `Soneta.Place.DefinicjaElementu` | element potrącenia raty |
| `IloscRat` | `int` | liczba rat |
| `KwotaRaty` | `Soneta.Types.Currency` | kwota raty |
| `SplatyOd` | `Soneta.Types.YearMonth` | miesiąc rozpoczęcia spłat |
| `Procent` | `Soneta.Types.Percent` | oprocentowanie |
| `Sposob` | `Soneta.Kadry.SposóbSpłatyOdsetek` | sposób spłaty odsetek |
| `AlgorytmRaty` | `Soneta.Kadry.AlgorytmRatyPożyczki` | algorytm wyliczania raty |
| `Raty` | `SubTable<Soneta.Kadry.RataPozyczki>` | harmonogram rat |
| `Stan` | `Soneta.Kadry.StanSpłat` | enum: `NieSpłacona = 0`, `Częściowo = 1`, `Całkowicie = 2` |
| `Splacona` | `bool` | spłacona w całości |

**Pola `RataPozyczki`:** `Pozyczka`, `Data: Date`, `Miesiąc: YearMonth`, `Kapital: Currency`,
`Odsetki: Currency`, `Element: DefinicjaElementu` (potrącenie raty), `Stan: StanSpłat`,
`Pozostaje`/`PozostajeKapitał`/`PozostajeOdsetki` (kalkulowane), `Zyrant: Pracownik`,
`Splacajacy: Pracownik`.

**Generowanie harmonogramu (workery):**

| Worker | Metoda | Params / sygnatura |
|---|---|---|
| `Soneta.Kadry.Pozyczka.UzgodnijRatyWorker` | `UzgodnijRaty` | ctor bezparam.; `Pars = new Params(ctx) { UzgodnijRaty = true }` (uwaga: `PrzeliczRaty` jest **tylko-do-odczytu**), `Pożyczka = pozyczka` — **buduje/przelicza harmonogram rat** wg `IloscRat`/`KwotaRaty`/`SplatyOd` |
| `Soneta.Kadry.Pozyczka.PożyczkaWorker` | `Pożyczka` | podsumowanie spłat (props: `Razem`, `Spłaty`, `Pozostaje`, `RazemOdsetki`…) |
| `Soneta.Kadry.Pozyczka.ElementWypłatyWorker` | `Pokaż` | podgląd elementu wypłaty pożyczki |

Metody na samym `Pozyczka`: `pozyczka.UpdatePozyczka()` (przelicz), `pozyczka.Rata(YearMonth,
DefinicjaElementu)`, `pozyczka.RatyZaMiesiąc(YearMonth)`, `pozyczka.SąRaty(YearMonth)`.

**Pułapki:**
- Ścieżka tworzenia jest **trzystopniowa**: `FundPozyczkowy(pracownik, definicja)` → `Pozyczka(fundusz)`
  → harmonogram. Pożyczki **nie da się** utworzyć bez funduszu (ctor wymaga `FundPozyczkowy`).
- Harmonogram rat generuj **workerem** `UzgodnijRatyWorker` (albo `UpdatePozyczka()`), nie ręcznym
  dodawaniem `RataPozyczki` — worker rozkłada kapitał/odsetki wg algorytmu.
- `Element` (wypłaty) i `ElementRaty` (potrącenia) to **różne** definicje elementów — `ElementRaty`
  realizuje potrącenie raty w wypłacie.
- **Faktyczne potrącenie raty następuje przy naliczeniu wypłaty** — `Stan`/`Splacono`/`Pozostaje`
  aktualizują się po naliczeniu. Samo udzielenie pożyczki ich nie zmienia.
- `DefinicjaFunduszuPozyczkowego` to słownik konfiguracyjny — pobierz istniejący wpis, nie twórz „w locie".

**Snippet:**

```csharp
var kadry = session.GetKadry();
var pracownik = kadry.Pracownicy.WgKodu["006"];
var defFunduszu = kadry.DefFundPozycz.WgNazwy["KZP"];
var elWyplata = session.GetPlace().DefElementow.WgNazwy["Pożyczka"];
var elRata    = session.GetPlace().DefElementow.WgNazwy["Spłata pożyczki"];

using (var t = session.Logout(editMode: true))
{
    // 1) Członkostwo w funduszu
    var fundusz = new FundPozyczkowy(pracownik, defFunduszu);
    kadry.FundPozyczkowe.AddRow(fundusz);
    fundusz.Okres = new FromTo(new Date(2026, 1, 1), Date.MaxValue);

    // 2) Pożyczka w ramach funduszu
    var pozyczka = new Pozyczka(fundusz);
    kadry.Pozyczki.AddRow(pozyczka);
    pozyczka.Data       = new Date(2026, 1, 10);
    pozyczka.Kwota      = (Currency)12000m;
    pozyczka.Element    = elWyplata;
    pozyczka.ElementRaty = elRata;
    pozyczka.IloscRat   = 12;
    pozyczka.SplatyOd   = new YearMonth(2026, 2);

    // 3) Harmonogram rat (worker bezparametrowy; Params: ctor (Context); PrzeliczRaty read-only)
    var context = login.CreateEmptyContext().Clone(session);
    var par = new Pozyczka.UzgodnijRatyWorker.Params(context) { UzgodnijRaty = true };
    new Pozyczka.UzgodnijRatyWorker { Pars = par, Pożyczka = pozyczka }.UzgodnijRaty();

    t.CommitUI();
}
session.Save();

// Odczyt harmonogramu:
foreach (FundPozyczkowy f in pracownik.FunduszePozyczkowe)
    foreach (Pozyczka p in f.Pozyczki)
        foreach (RataPozyczki r in p.Raty)
        {
            YearMonth m = r.Miesiąc;
            Currency kapital = r.Kapital, odsetki = r.Odsetki;
            StanSpłat stan = r.Stan;
        }
```

## D. Nieobecności i czas pracy

### D1 — Wprowadzanie nieobecności (★)

**Cel:** zarejestrować nieobecność pracownika (urlop wypoczynkowy, zwolnienie chorobowe, urlop
bezpłatny, opieka itp.) za wskazany okres oraz odczytać nieobecności obowiązujące w danym przedziale dat.

**Fakty o typie (zweryfikowane skanem DLL):**

- **`Soneta.Kalend.Nieobecnosc` jest klasą abstrakcyjną** — tabela `Nieobecnosci` (`GuidedRow` root,
  child `Pracownik`-a). `new Nieobecnosc(...)` się nie skompiluje.
- Konkretny typ do tworzenia: **`Soneta.Kalend.NieobecnośćPracownika`** (dziedziczy z `Nieobecnosc`),
  z **publicznym konstruktorem `new NieobecnośćPracownika(Pracownik pracownik)`** — ctor od razu wiąże
  nieobecność z pracownikiem. (Drugi konkretny typ to `KorektaNieobecności` — patrz D2.)
- Kolekcja na pracowniku: **`pracownik.Nieobecnosci: FromToSubTable<Soneta.Kalend.Nieobecnosc>`**
  (uporządkowana po okresie „od–do").
- Tabela z poziomu modułu: `session.GetKalend().Nieobecnosci`.

**Pola i typy (`Nieobecnosc` / `NieobecnośćPracownika`):**

| Pole | Typ | Rodzaj | Opis |
|---|---|---|---|
| `Definicja` | `Soneta.Kalend.DefinicjaNieobecnosci` | bazodanowe, **zapisywalne** | rodzaj nieobecności (słownik konfiguracyjny); decyduje o typie (urlop / zasiłek / bezpłatny) |
| `Okres` | `Soneta.Types.FromTo` | bazodanowe, **zapisywalne** | zakres dat nieobecności „od–do" |
| `OdGodziny`, `DoGodziny` | `Soneta.Types.Time` | — | godziny (nieobecności godzinowe) |
| `Norma`, `NormaNie` | `Soneta.Types.Time` | bazodanowe | normy czasowe |
| `IlośćDni` / `Dni` | `int` | kalkulowane/zapisywalne | liczba dni nieobecności |
| `Pracownik` | `Soneta.Kadry.Pracownik` | **tylko do odczytu** | właściciel (ustawiany ctorem, nie da się zmienić setterem) |
| `Zwolnienie` | `Soneta.Kalend.ZwolnienieZUS` (subrow) | bazodanowe | dane ZUS dla zwolnień chorobowych (`KodChoroby`, `Numer` ZLA, `PonownieUstalPodstawe`…) |
| `Urlop`, `Macierzynski`, `Wychowawczy`, `Okolicznosciowy` | subrowy | bazodanowe | szczegóły poszczególnych typów urlopów |
| `Korygowana` | `bool` | **tylko do odczytu** | czy nieobecność jest korektą (patrz D2) |

**Dostęp do definicji nieobecności (`DefNieobecnosci`):**

- `session.GetKalend().DefNieobecnosci.WgNazwy[string]` — pobranie po nazwie, np.
  `WgNazwy["Urlop wypoczynkowy"]`, `WgNazwy["Zwolnienie chorobowe"]`,
  `WgNazwy["Urlop bezpłatny (art 174 kp)"]`. Nazwy muszą **dokładnie** odpowiadać słownikowi danej bazy
  (w Demo nie ma wpisu „Urlop bezpłatny" — jest „Urlop bezpłatny (art 174 kp)"); `WgNazwy[...]` dla
  nieistniejącej nazwy zwraca `null`.
- `session.GetKalend().DefNieobecnosci[string]` (indeksator domyślny po nazwie) — równoważne.
- `DefinicjaNieobecnosci` ma pola `Nazwa: string`, `Kod: string`, `Typ: TypNieobecnosci`.

**Wyszukiwanie po dacie/okresie:** `pracownik.Nieobecnosci.GetIntersectedRows(FromTo)` zwraca
`IList` nieobecności przecinających podany przedział.

**Snippet:**

```csharp
var kalend    = session.GetKalend();
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
// Nieobecność BEZ limitu (np. urlop bezpłatny) można wprowadzić wprost. Dla nieobecności
// LIMITOWANYCH (urlop wypoczynkowy) najpierw musi istnieć naliczony limit — patrz pułapki i D7.
var defNieob  = kalend.DefNieobecnosci.WgNazwy["Urlop bezpłatny (art 174 kp)"];

using (var t = session.Logout(editMode: true))
{
    // typ konkretny; ctor wiąże nieobecność z pracownikiem
    var nieobecnosc = session.AddRow(new NieobecnośćPracownika(pracownik));
    nieobecnosc.Definicja = defNieob;                                  // rodzaj nieobecności
    nieobecnosc.Okres     = new FromTo(new Date(2026, 7, 1), new Date(2026, 7, 5));
    t.Commit();
}
session.Save();

// Odczyt nieobecności przecinających lipiec 2026:
var lipiec = new FromTo(new Date(2026, 7, 1), new Date(2026, 7, 31));
foreach (Nieobecnosc n in pracownik.Nieobecnosci.GetIntersectedRows(lipiec))
{
    // n.Definicja.Nazwa, n.Okres, n.Dni
}
```

**Pułapki:**
- **Nie** rób `new Nieobecnosc(...)` — typ jest abstrakcyjny. Używaj `new NieobecnośćPracownika(pracownik)`.
- **Nieobecności limitowane wymagają istniejącego limitu.** Ustawienie `Okres` dla nieobecności
  powiązanej z limitem (np. „Urlop wypoczynkowy") synchronicznie przelicza limit i rzuca
  `Soneta.Kalend.DefinicjaLimitu.LimitNotFoundException`, gdy pracownik nie ma naliczonego limitu na
  dany rok. Dlatego: albo najpierw nalicz limit (patrz D7), albo użyj nieobecności bez limitu
  (np. „Urlop bezpłatny (art 174 kp)") — jak w snippetcie powyżej.
- `Definicja` jest **wymagana** — bez niej nieobecność nie zostanie poprawnie naliczona/zapisana.
  Pobieraj istniejący wpis słownika przez `DefNieobecnosci.WgNazwy[...]`, nie twórz „w locie".
- `Pracownik` jest **tylko do odczytu** — relację ustawia konstruktor, nie da się jej później zmienić.
- Tabela `Nieobecnosci` jest **operacyjna guided** — przy przeglądaniu poprzecznym (po wszystkich
  pracownikach) filtruj zakresem czasowym (safe-code §6.3). W zakresie jednego pracownika korzystaj
  z `pracownik.Nieobecnosci` i `GetIntersectedRows`.
- Nakładające się nieobecności i niepoprawne okresy wychwytują weryfikatory przy `Save()`
  (`RowException`) — obsłuż wyjątek.
- Pełna transakcja w `session.Logout(editMode: true)`; brak `Commit()` = rollback przy `Dispose()`.

---

### D2 — Korygowanie nieobecności już wypłaconych (★)

**Cel:** poprawić nieobecność, która została już rozliczona w wypłacie — zmienić jej okres lub typ
(definicję) i/lub wymusić ponowne ustalenie podstawy naliczania zasiłku. enova rozróżnia dwie ścieżki:
(a) **modyfikacja istniejącej nieobecności** + ponowne ustalenie podstawy, (b) **korekta** jako odrębny
rekord typu `KorektaNieobecności`.

**Fakty o typie (zweryfikowane skanem DLL):**

- Pola `Definicja: DefinicjaNieobecnosci` i `Okres: FromTo` na `Nieobecnosc` są **zapisywalne**
  publicznie — można je zmienić na istniejącym rekordzie.
- `Nieobecnosc.Korygowana: bool` i `Nieobecnosc.Pracownik` są **tylko do odczytu**.
- Subrow `Zwolnienie: Soneta.Kalend.ZwolnienieZUS` posiada flagę `PonownieUstalPodstawe: bool`
  oraz **publiczną metodę `SetPonownieUstalPodstawe(bool)`** — to ona steruje przeliczeniem podstawy
  zasiłku przy kolejnym naliczeniu wypłaty.
- Worker (czynność menu, `DataType = Nieobecnosc`): klasa
  **`Soneta.Kalend.Nieobecnosc.UstalPonowniePodstawęNaliczaniaWorker`** — czynność
  „Ustal ponownie podstawę naliczania". Worker:
  - ma publiczny bezparametrowy ctor;
  - przyjmuje kontekst przez settowalną property `[Context] public Params Nieobecność`;
  - klasa `…Worker.Params : ContextBase` ma **publiczny ctor `Params(Context context)`**, który czyta
    nieobecność z `context[typeof(Nieobecnosc)]`, oraz settowalną property `UstalPodstawę: bool`;
  - metoda `public void PonownieUstalPodstawę()` jest jego akcją;
  - `static bool IsEnabledPonownieUstalPodstawę(Nieobecnosc)` mówi, kiedy czynność jest aktywna
    (dotyczy zwolnień ZUS i urlopów macierzyńskich: `Zwolnienie.IsZUS || Macierzynski.IsMacierzyński`,
    przy braku `BlokadaOkresu`).
- Drugi konkretny typ nieobecności: **`Soneta.Kalend.KorektaNieobecności`** (dziedziczy `Nieobecnosc`),
  z **publicznym ctor `new KorektaNieobecności(NieobecnośćPracownika nieobecność)`** — tworzy rekord
  korygujący wskazaną nieobecność. Ma zapisywalne `Definicja`, `Okres`, `IlośćDni`,
  `RozliczenieWDniu`, `RozliczenieData`, a kolekcje `ElementyKorygowane`/`ElementyKorygowaneStorno`
  są tylko do odczytu (wyliczane).

**Wariant A — zmiana okresu/typu + ponowne ustalenie podstawy (modyfikacja istniejącego rekordu):**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
var okresStary = new FromTo(new Date(2026, 3, 2), new Date(2026, 3, 10));

// odszukaj istniejącą (już rozliczoną) nieobecność po przecięciu z okresem
var nieobecnosc = (Nieobecnosc)pracownik.Nieobecnosci.GetIntersectedRows(okresStary)[0];

using (var t = session.Logout(editMode: true))
{
    nieobecnosc.Okres = new FromTo(new Date(2026, 3, 2), new Date(2026, 3, 12));  // wydłużenie okresu
    // dla zwolnień ZUS — wymuś ponowne ustalenie podstawy przy najbliższym naliczeniu wypłaty:
    nieobecnosc.Zwolnienie.SetPonownieUstalPodstawe(true);
    t.Commit();
}
session.Save();
```

**Wariant B — czynność „Ustal ponownie podstawę naliczania" przez worker (kontekst):**

```csharp
var worker = new Nieobecnosc.UstalPonowniePodstawęNaliczaniaWorker();
var ctx    = Context.Empty.Clone(session);
ctx[typeof(Nieobecnosc)] = nieobecnosc;                     // worker czyta nieobecność z kontekstu
worker.Nieobecność = new Nieobecnosc.UstalPonowniePodstawęNaliczaniaWorker.Params(ctx)
{
    UstalPodstawę = true
};
worker.PonownieUstalPodstawę();                             // wykonuje własną transakcję + Commit
session.Save();
```

**Wariant C — odrębny rekord korekty (`KorektaNieobecności`):**

```csharp
using (var t = session.Logout(editMode: true))
{
    var nPrac = (NieobecnośćPracownika)nieobecnosc;          // korekta dotyczy NieobecnośćPracownika
    var korekta = session.AddRow(new KorektaNieobecności(nPrac));
    korekta.Definicja = nPrac.Definicja;
    // Okres korekty MUSI być podzbiorem okresu korygowanej nieobecności (tu: 2..10):
    korekta.Okres     = new FromTo(new Date(2026, 3, 3), new Date(2026, 3, 8));
    t.Commit();
}
session.Save();
// Po zapisie korygowana nieobecność ma flagę Korygowana == true.
```

**Pułapki:**
- **Faktyczne** przeliczenie wartości zasiłku NIE następuje w momencie ustawienia flagi/wywołania
  workera — flaga `PonownieUstalPodstawe` jest odczytywana dopiero przy **ponownym naliczeniu wypłaty**
  (mechanizm `PodstawaZasilku`). Sam test korekty rekordu nieobecności (Demo, rollback) zweryfikuje
  zmianę `Okres`/`Definicja`/flagi, ale **nie zweryfikuje przeliczonych kwot wypłaty** bez pełnego
  scenariusza naliczenia listy płac (patrz sekcja „funkcjonalności niewykonalne").
- `IsEnabledPonownieUstalPodstawę` ogranicza czynność do zwolnień ZUS / macierzyńskich — dla zwykłego
  urlopu wypoczynkowego worker nie ma zastosowania; tam korektę robisz przez zmianę `Okres`/`Definicja`
  albo rekord `KorektaNieobecności`.
- **Okres korekty (`KorektaNieobecności.Okres`) musi być podzbiorem okresu korygowanej nieobecności** —
  wyjście poza ten zakres rzuca `Nieobecnosc.KorygowanyOkresException`.
- Dla nieobecności bez skutków płacowych (np. urlop bezpłatny) `KorektaNieobecności` **nie pojawia się
  jako osobny wiersz** w `pracownik.Nieobecnosci` — obserwowalnym efektem jest flaga `Korygowana == true`
  na nieobecności pierwotnej.
- Korekta zmienia dane operacyjne powiązane z wypłatą — trzymaj transakcję krótką i obsłuż
  `RowConflictException` / `RowException` z `Save()` (safe-code §4, §13.1).
- Worker wykonuje własną transakcję (`Session.Logout(true)` + `Commit`) — nie zagnieżdżaj go w innej
  otwartej transakcji edycyjnej.

---

### D7 — Analiza limitów urlopowych (★)

**Cel:** odczytać limit nieobecności (np. urlop wypoczynkowy) pracownika za dany rok — ile przysługuje,
ile wykorzystano, ile pozostało. Limity **nie są tworzone ręcznie** — powstają przez naliczanie.

**Fakty o typie (zweryfikowane skanem DLL):**

- **`Soneta.Kalend.LimitNieobecnosci`** — tabela `LimNieobecnosci`, `GuidedRow` **child** pracownika
  (relacja przez pole `Pracownik`). Instancje powstają wyłącznie przez naliczanie — **nie twórz ich
  konstruktorem**.
- Kolekcja na pracowniku: **`pracownik.Limity: SubTable<Soneta.Kalend.LimitNieobecnosci>`**
  (nazwa kolekcji to `Limity`, nie „LimityNieobecnosci").
- Tabela z poziomu modułu: `session.GetKalend().LimNieobecnosci`.

**Pola i typy (`LimitNieobecnosci`) — odczyt:**

| Pole | Typ | Rodzaj | Opis |
|---|---|---|---|
| `Definicja` | `Soneta.Kalend.DefinicjaLimitu` | bazodanowe | rodzaj limitu (urlop wypoczynkowy itd.) |
| `Okres` | `Soneta.Types.FromTo` | bazodanowe | okres limitu (zwykle rok) |
| `OkresWażności` | `Soneta.Types.FromTo` | kalkulowane | okres ważności limitu |
| `Limit` | `int` | bazodanowe | limit (dni) wynikający z kodeksu pracy |
| `LimitDni` | `int` | kalkulowane | limit w dniach |
| `LimitGodz` | `Soneta.Types.Time` | bazodanowe | limit w godzinach |
| `Razem` / `RazemGodz` | `int` / `Time` | kalkulowane | łączny przysługujący (limit + przeniesienia + zmiany) |
| `Wykorzystane` / `WykorzystaneGodz` | `int` / `Time` | bazodanowe | wykorzystane dni/godziny |
| `Pozostalo` | `int` | kalkulowane | pozostało (dni, int) |
| `PozostaloDni` | `double` | kalkulowane | pozostało dni (z częścią ułamkową) |
| `PozostaloGodz` | `Soneta.Types.Time` | kalkulowane | pozostało godzin |
| `ZaleglyDni` / `ZaleglyGodz` | `double` / `Time` | kalkulowane | zaległy z poprzednich okresów |
| `Przeniesienie` / `PrzeniesienieDni` | `int` / `double` | kalkulowane | przeniesione z poprzedniego roku |
| `Korekta`, `Zmiana` | `int` | bazodanowe | korekty/zmiany limitu |
| `Pracownik` | `Soneta.Kadry.Pracownik` | bazodanowe (guided-parent), **read-only** | właściciel |

> **Wykorzystany = `Razem - Pozostalo`** (lub bezpośrednio pole `Wykorzystane`). „Przysługujący" to
> `Razem` (limit kodeksowy + przeniesienia + zmiany), a nie samo `Limit`.

**Dostęp do definicji limitów (`DefinicjeLimitow`):**

- `session.GetKalend().DefinicjeLimitow.WgNazwy[string]` — np. `WgNazwy["Urlop wypoczynkowy"]`.
- Skróty typowane (property zwracające `DefinicjaLimitu`): `DefinicjeLimitow.UrlopWypoczynkowy`,
  `.UrlopDodatkowy`, `.OpiekaNadZdrowym`, `.UrlopOpiekunczy`, `.ZwolnienieOdPracySilaWyzsza` itd.
- `DefinicjaLimitu` ma pola `Nazwa: string`, `Typ: TypLimitu`.

**Naliczanie limitu (by mógł istnieć do odczytu) — `Soneta.Kalend.NaliczanieLimitow`:**

- Klasa z **publicznym bezparametrowym ctor**; settowalne property:
  - `Pars: NaliczanieLimitow.Params` (set),
  - `Pracownicy: ICollection<Pracownik>` (set) **albo** `PracownicyIdx: Pracownik[]` (set).
- Klasa `NaliczanieLimitow.Params : ContextBase` ma **publiczny ctor `Params(Context context)`**
  oraz settowalne: `Definicja: DefinicjaLimitu`, `Okres: FromTo`, `KopiujKorekty: bool`,
  `ZapisPerPracownik: bool`.
- Metoda **`public void DodajLimit()`** — nalicza limit (zapisuje rekordy `LimitNieobecnosci`).
  (Jest też `DodajLimitUrlopowy()`.)

**Snippet — naliczenie + odczyt:**

```csharp
var kalend    = session.GetKalend();
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
var defUrlop  = kalend.DefinicjeLimitow.WgNazwy["Urlop wypoczynkowy"];   // lub DefinicjeLimitow.UrlopWypoczynkowy
var rok       = FromTo.Year(new Date(2026, 1, 1));

using (var t = session.Logout(editMode: true))
{
    var naliczanie = new NaliczanieLimitow
    {
        Pars = new NaliczanieLimitow.Params(Context.Empty.Clone(session))
        {
            Definicja     = defUrlop,
            Okres         = rok,
            KopiujKorekty = true
        },
        Pracownicy = new Pracownik[] { pracownik }
    };
    naliczanie.DodajLimit();          // tworzy/aktualizuje LimitNieobecnosci
    t.Commit();
}
session.Save();

// Odczyt limitu urlopu wypoczynkowego za rok 2026.
// UWAGA: filtr serwerowy obejmuje TYLKO pola bazodanowe i prostych porównań — Okres (FromTo)
// NIE da się porównać serwerowo (==), więc filtrujemy serwerowo po Definicja, a rok w pamięci:
var lim = pracownik.Limity[(LimitNieobecnosci l) => l.Definicja == defUrlop]
              .Cast<LimitNieobecnosci>()
              .FirstOrDefault(l => l.Okres.From == rok.From);
if (lim != null)
{
    int przysluguje  = lim.Razem;            // przysługujący (limit + przeniesienia + zmiany)
    int pozostalo    = lim.Pozostalo;        // pozostało
    int wykorzystany = przysluguje - pozostalo;  // == lim.Wykorzystane
    // lim.PozostaloDni, lim.PozostaloGodz, lim.ZaleglyDni
}
```

**Pułapki:**
- **Nie** twórz `new LimitNieobecnosci(...)` — limit powstaje przez naliczanie (`DodajLimit`). W bazie
  Demo limit dla danego roku może jeszcze nie istnieć — w teście trzeba go **najpierw naliczyć**.
- Kolekcja na pracowniku to `pracownik.Limity` (nie `LimityNieobecnosci`).
- **Nie porównuj `Okres` (FromTo) w filtrze serwerowym** — `l.Okres == rok` rzuca `ArgumentException`
  („pole nieznalezione"). Filtruj serwerowo po `Definicja`, a okres/rok porównaj w pamięci
  (`.FirstOrDefault(l => l.Okres.From == rok.From)`).
- `Razem` może wynosić `0` dla pracowników bez danych napędzających wymiar urlopu (staż, data
  urodzenia) — asercje opieraj na spójności (`Wykorzystane == Razem - Pozostalo`, `Razem >= 0`),
  a nie na założeniu `Razem > 0`.
- `Pracownik` na limicie jest read-only (relacja guided) — naliczanie samo wiąże rekord z pracownikiem.
- Filtruj limity serwerowo po `Definicja` i `Okres` (`pracownik.Limity[condition]`), nie iteruj całości
  z `if` w pamięci (safe-code §6.1). Tabela `LimNieobecnosci` jest operacyjna guided.
- `Context.Empty.Clone(session)` daje kontekst związany z bieżącą sesją — wymagany przez ctor
  `NaliczanieLimitow.Params(Context)`.
- Naliczanie modyfikuje dane operacyjne — w transakcji edycyjnej, krótko, z obsługą wyjątków z `Save()`.

### D3 — Import e-ZLA z PUE ZUS (zwolnienia lekarskie)

**Cel:** zaewidencjonować w systemie zwolnienie lekarskie pobrane z PUE ZUS (e-ZLA). Sam **import to
operacja sieciowa** (komunikacja z PUE ZUS) — w kodzie biznesowym/teście dokumentujemy **model danych**
nieobecności chorobowej i jej dane ZUS, a nie samo połączenie z bramką PUE.

**Fakty o typie (zweryfikowane skanem DLL):**

- Zwolnienie chorobowe to `Soneta.Kalend.NieobecnośćPracownika` (typ konkretny z D1) z `Definicja`
  wskazującą na rodzaj zasiłkowy (np. „Zwolnienie chorobowe").
- Dane ZUS zwolnienia leżą w subrowie **`Nieobecnosc.Zwolnienie: Soneta.Kalend.ZwolnienieZUS`**
  (bazodanowy subrow na rekordzie nieobecności).
- Dane samego dokumentu ZLA leżą w subrowie **`Nieobecnosc.ZLA: Soneta.Kalend.ZLA`**
  (`ZLA.Data: Date`, `ZLA.Wersja: WersjaZLA`, `ZLA.Zrodlo: MemoText`).

**Pola i typy (`Nieobecnosc.Zwolnienie: ZwolnienieZUS`) — zapisywalne, bazodanowe:**

| Pole | Typ | Opis |
|---|---|---|
| `Numer` | `string` | numer dokumentu ZLA (pole tekstowe — **maks. 9 znaków**) |
| `KodChoroby` | `string` | kod literowy choroby (A, B, C, D, …) |
| `Przyczyna` | `Soneta.Kalend.PrzyczynaZwolnienia` | przyczyna niezdolności do pracy |
| `Kwarantanna` | `Soneta.Kalend.ZwolnienieKwarantanna` | kwarantanna/izolacja |
| `LeczenieSzpitalne` | `bool` | pobyt w szpitalu |
| `ZwolnienieWystawione` | `Soneta.Types.Date` | data wystawienia ZLA |
| `ZwolnienieDostarczone` | `Soneta.Types.Date` | data dostarczenia |
| `PomniejszajZasilek` | `bool` | obniżenie zasiłku |
| `PonownieUstalPodstawe` | `bool` | wymuszenie przeliczenia podstawy (patrz D2/D6) |

**Pola i typy (`Nieobecnosc.ZLA: ZLA`):** `Data: Date`, `Wersja: WersjaZLA`, `Zrodlo: MemoText`.

**Snippet — ręczne odwzorowanie e-ZLA jako nieobecności chorobowej (bez sieci):**

```csharp
var kalend    = session.GetKalend();
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
var defChor   = kalend.DefNieobecnosci.WgNazwy["Zwolnienie chorobowe"];

using (var t = session.Logout(editMode: true))
{
    var nieob = session.AddRow(new NieobecnośćPracownika(pracownik));
    nieob.Definicja = defChor;
    nieob.Okres     = new FromTo(new Date(2026, 5, 4), new Date(2026, 5, 10));
    // dane ZUS z e-ZLA (subrow Zwolnienie):
    nieob.Zwolnienie.Numer      = "ZLA000001";   // pole Numer ma limit 9 znaków
    nieob.Zwolnienie.KodChoroby = "A";
    t.Commit();
}
session.Save();
```

**Pułapki:**
- **Sam import e-ZLA z PUE wymaga sieci** (uwierzytelnienie + bramka ZUS) — nie da się go odtworzyć
  w teście jednostkowym na bazie Demo; testuj wyłącznie **odwzorowanie modelu danych** (subrow `Zwolnienie`).
- `Zwolnienie` i `ZLA` to subrowy — nie tworzysz ich osobno, są częścią rekordu `Nieobecnosc`; ustawiasz
  ich pola po utworzeniu nieobecności.
- Definicja zasiłkowa musi istnieć w słowniku bazy (`DefNieobecnosci.WgNazwy[...]` ≠ `null`).
- **Faktyczne kwoty zasiłku** liczą się dopiero przy naliczeniu wypłaty — patrz uwaga przy D2.

---

### D4 — Generowanie deklaracji Z-3 / Z-3a dla nieobecności chorobowej

**Cel:** wygenerować zaświadczenie płatnika składek **Z-3** (pracownik etatowy) lub **Z-3a** (umowy/inni
ubezpieczeni) dla konkretnej nieobecności zasiłkowej.

**Fakty o typie (zweryfikowane skanem DLL):**

- Worker (czynność na `Nieobecnosc`): **`Soneta.Deklaracje.ZUS.ZUSZ3.Z3Worker`** — akcja
  „Generuj deklarację Z-3", metoda `public object UtworzDeklaracjeZ3()`.
- Analogicznie **`Soneta.Deklaracje.ZUS.ZUSZ3.Z3aWorker`** — akcja „Generuj deklarację Z-3a",
  metoda `public object UtworzDeklaracjeZ3a()`.
- Oba workery przyjmują przez `[Context]`:
  - `KeduContext: DeklaracjaZUS.PUEContext` (property `Kedu: KEDU`),
  - `Z3ParamContext: Z3ParamContext` / `Z3aParamContext` z polami m.in.: `Nieobecnosc: INieobecnoscLubZbieg`,
    `NieobecnoscZContextu: bool`, `Pracownik: Pracownik`, `PracownikZContextu: bool`, `Okres: FromTo`,
    `OkresZasiłkowy: FromTo`, `OkresZasilkowyOd: Date`, `Współczynnik: Fraction`, `RachBank: string`,
    `KontynuacjaŚwiadczenia: bool`.

**Snippet — generowanie Z-3 dla nieobecności (kontekst):**

```csharp
var worker = new Soneta.Deklaracje.ZUS.ZUSZ3.Z3Worker();
var ctx    = Context.Empty.Clone(session);
ctx[typeof(Nieobecnosc)] = nieobChorobowa;   // worker czyta nieobecność z kontekstu

var deklaracja = worker.UtworzDeklaracjeZ3();  // zwraca obiekt deklaracji Z-3
session.Save();
```

**Pułapki:**
- **Sensowny Z-3 wymaga naliczonej wypłaty/podstawy zasiłku** — bez naliczonej podstawy deklaracja
  powstanie z pustymi/zerowymi kwotami. W teście na czystej Demo zweryfikujesz fakt powstania obiektu
  i ustawienie pól nagłówkowych (pracownik, okres), ale **nie kwoty zasiłku**.
- Worker przyjmuje dane przez `Context` (`ctx[typeof(Nieobecnosc)]`/`ctx[typeof(Pracownik)]`) — nie ma
  prostego ctora parametrowego; zegnij pod swój scenariusz `Z3ParamContext`.
- Z-3 dotyczy etatu, Z-3a umów/innych ubezpieczonych — dobierz worker do tytułu ubezpieczenia.
- Metody zwracają `object` (deklaracja KEDU) — zachowaj/odczytaj wynik, nie zakładaj typu wprost.

---

### D5 — Obsługa przestoju (dodanie/usunięcie, przestój ekonomiczny — % wynagrodzenia)

**Cel:** zaewidencjonować przestój pracownika (np. ekonomiczny) za okres oraz wskazać procent
wynagrodzenia przestojowego; usunąć przestój nakładający się na nieobecność ZUS.

**Fakty o typie (zweryfikowane skanem DLL):**

- **Dodanie przestoju:** worker **`Soneta.Kadry.DodajPrzestojWorker`** (czynność „Przestój/Dodaj przestój",
  metoda `public void DodajPrzestoj()`):
  - settowalne property: `Pracownicy: Pracownik[]`, `Pars: DodajPrzestojWorker.Params`;
  - `Params` z polami: `DefinicjaStrefy: Soneta.Kalend.DefinicjaStrefy`, `Okres: FromTo`.
- **Procent wynagrodzenia przestojowego (przestój ekonomiczny):** worker
  **`Soneta.Kadry.IndywidualnyProcentWynagrPrzestojowegoWorker`** (czynność
  „Przestój/Przestój ekonomiczny - procent wynagr.", metoda `public void Aktualizuj()`):
  - `Pracownicy: Pracownik[]`, `Pars.Data: Date`, `Pars.Procent: Soneta.Types.Percent`.
- **Usunięcie przestoju podczas nieobecności ZUS:** worker
  **`Soneta.Kadry.UsunPrzestojNieobecnoscWorker`** (czynność „Przestój/Usuń przestój podczas
  nieobecności ZUS", metoda `public void UsunPrzestoj()`): `Pracownicy: Pracownik[]`, `Pars.Okres: FromTo`.
- Procent wynagrodzenia przestojowego jest też trzymany na etacie:
  `PracHistoria.Etat.Postojowe: Soneta.Kadry.WynagrodzeniePostojowe` (`Procent: Percent`, `Standardowe: bool`).
- `DefinicjaStrefy` (`session.GetKalend().DefinicjeStref`) — słownik konfiguracyjny stref (m.in. przestoju).

**Snippet — dodanie przestoju:**

```csharp
var kalend     = session.GetKalend();
var pracownik  = session.GetKadry().Pracownicy.WgKodu["006"];
var defStrefa  = kalend.DefinicjeStref.WgNazwy["Przestój"];   // nazwa wg słownika danej bazy

var worker = new Soneta.Kadry.DodajPrzestojWorker
{
    Pracownicy = new[] { pracownik },
    Pars = new Soneta.Kadry.DodajPrzestojWorker.Params(Context.Empty.Clone(session))
    {
        DefinicjaStrefy = defStrefa,
        Okres           = new FromTo(new Date(2026, 6, 1), new Date(2026, 6, 5))
    }
};
worker.DodajPrzestoj();    // worker wykonuje własną transakcję
session.Save();
```

**Snippet — przestój ekonomiczny (procent):**

```csharp
var worker = new Soneta.Kadry.IndywidualnyProcentWynagrPrzestojowegoWorker
{
    Pracownicy = new[] { pracownik },
    Pars = new Soneta.Kadry.IndywidualnyProcentWynagrPrzestojowegoWorker.Params(Context.Empty.Clone(session))
    {
        Data    = new Date(2026, 6, 1),
        Procent = new Percent(0.5m)   // 50% wynagrodzenia
    }
};
worker.Aktualizuj();
session.Save();
```

**Pułapki:**
- `DefinicjeStref.WgNazwy[...]` zależy od słownika danej bazy — zweryfikuj nazwę przestoju w Demo
  (może być inna niż „Przestój"); dla nieistniejącej nazwy zwraca `null`.
- Worker wykonuje własną transakcję — nie zagnieżdżaj go w otwartej transakcji edycyjnej.
- `Percent` przyjmuj jako ułamek (`0.5m` = 50%), nie liczbę 50.
- `UsunPrzestojNieobecnoscWorker` usuwa przestój **kolidujący z nieobecnością ZUS** — to nie generyczne
  „usuń przestój"; zakres działania ogranicza okres + obecność nieobecności ZUS.
- Skutki płacowe (wynagrodzenie przestojowe) liczą się dopiero przy naliczeniu wypłaty.

---

### D6 — Ustalanie/zmiana parametrów okresu zasiłkowego

**Cel:** zmienić parametry okresu zasiłkowego nieobecności chorobowej — kontynuację/przedłużenie okresu
zasiłkowego oraz wymusić ponowne ustalenie podstawy naliczania zasiłku.

**Fakty o typie (zweryfikowane skanem DLL):**

- Parametry okresu zasiłkowego są w subrowie **`Nieobecnosc.Zwolnienie: ZwolnienieZUS`** (bazodanowe,
  zapisywalne):
  - `KontynuacjaOkrZas: Soneta.Kalend.KontynuacjaOkrZas` (enum: `Warunkowo`, `Tak`, `Nie`),
  - `PrzedluzenieOkrZas: bool`, `PrzedluzeniaData: Soneta.Types.Date`,
  - `PonownieUstalPodstawe: bool` + metoda `SetPonownieUstalPodstawe(bool)` (patrz D2).
- Worker korekty okresu zasiłkowego: **`Soneta.Kalend.Nieobecnosc.KorektaOkresuZasiłkowegoWorker`**
  (czynność „Zmień pozostałe parametry okresu zasiłkowego", metoda `public void PonownieUstalPodstawę()`):
  - settowalne `Pars: KorektaOkresuZasiłkowegoWorker.Params` z polami:
    `KontynuacjaOkrZas: KontynuacjaOkrZas`, `PrzedluzenieOkrZas: bool`, `PrzedluzeniaData: Date`.
- BO okresu zasiłkowego (przy wdrożeniu) — patrz D10: `PracHistoria.ChorobowyBO`
  (`DniZasilkowe`, `ZasilekOdDnia`, `PrzedluzenieOZ`).

**Snippet — zmiana parametrów wprost na rekordzie:**

```csharp
using (var t = session.Logout(editMode: true))
{
    nieobChorobowa.Zwolnienie.KontynuacjaOkrZas  = KontynuacjaOkrZas.Tak;
    nieobChorobowa.Zwolnienie.PrzedluzenieOkrZas = true;
    nieobChorobowa.Zwolnienie.PrzedluzeniaData   = new Date(2026, 5, 31);
    nieobChorobowa.Zwolnienie.SetPonownieUstalPodstawe(true);
    t.Commit();
}
session.Save();
```

**Snippet — przez worker korekty okresu zasiłkowego:**

```csharp
var worker = new Nieobecnosc.KorektaOkresuZasiłkowegoWorker();
var ctx    = Context.Empty.Clone(session);
ctx[typeof(Nieobecnosc)] = nieobChorobowa;
worker.Pars = new Nieobecnosc.KorektaOkresuZasiłkowegoWorker.Params(ctx)
{
    KontynuacjaOkrZas  = KontynuacjaOkrZas.Tak,
    PrzedluzenieOkrZas = true,
    PrzedluzeniaData   = new Date(2026, 5, 31)
};
worker.PonownieUstalPodstawę();   // własna transakcja + Commit
session.Save();
```

**Pułapki:**
- **Faktyczne** przeliczenie kwot zasiłku następuje dopiero przy **ponownym naliczeniu wypłaty** — test
  na Demo zweryfikuje zmianę pól `KontynuacjaOkrZas`/`PrzedluzenieOkrZas`/`PrzedluzeniaData`/flagi,
  ale nie kwoty.
- Parametry okresu zasiłkowego mają sens tylko dla nieobecności **ZUS** (zwolnienia chorobowe/zasiłki) —
  dla urlopu wypoczynkowego są bez znaczenia.
- Worker wykonuje własną transakcję — nie zagnieżdżaj go w innej otwartej transakcji.

---

### D8 — Naliczanie i przeliczanie limitów nieobecności

**Cel:** naliczyć limit nieobecności (jak D7 — `NaliczanieLimitow.DodajLimit()`) oraz przeliczyć liczbę
wykorzystanych dni limitu (czynność „Przelicz wykorzystane").

**Fakty o typie (zweryfikowane skanem DLL):**

- **Naliczenie limitu:** klasa **`Soneta.Kalend.NaliczanieLimitow`** — publiczny bezparametrowy ctor;
  settowalne `Pars: NaliczanieLimitow.Params` (`Definicja: DefinicjaLimitu`, `Okres: FromTo`,
  `KopiujKorekty: bool`, `ZapisPerPracownik: bool`) oraz `Pracownicy: ICollection<Pracownik>` /
  `PracownicyIdx: Pracownik[]`; metoda `public void DodajLimit()` (i `DodajLimitUrlopowy()`).
  Wariant UI per-pracownik: worker **`Soneta.Kalend.UI.PracownikLimityNaliczanieWorker`**
  (czynność „Nalicz limit nieobecności", metoda `DodajLimit()`) — `Pracownik: Pracownik`,
  `Pars` jak wyżej.
- **Przeliczenie wykorzystanych:** worker
  **`Soneta.Kalend.LimitNieobecnosci.Pracownicy.PrzeliczWykorzystaneWorker`** (czynność
  „Limity nieobecności/Przelicz wykorzystane", metoda `public void PrzeliczWykorzystane()`):
  - settowalne `Pracownicy: Pracownik[]`, `Pars.Definicja: DefinicjaLimitu`, `Pars.Okres: FromTo`.

**Snippet — naliczenie + przeliczenie wykorzystanych:**

```csharp
var kalend    = session.GetKalend();
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
var defUrlop  = kalend.DefinicjeLimitow.WgNazwy["Urlop wypoczynkowy"];
var rok       = FromTo.Year(new Date(2026, 1, 1));

// 1) naliczenie limitu (jak D7)
var naliczanie = new NaliczanieLimitow
{
    Pars = new NaliczanieLimitow.Params(Context.Empty.Clone(session))
    {
        Definicja     = defUrlop,
        Okres         = rok,
        KopiujKorekty = true
    },
    Pracownicy = new[] { pracownik }
};
naliczanie.DodajLimit();
session.Save();

// 2) przeliczenie wykorzystanych
var przelicz = new LimitNieobecnosci.Pracownicy.PrzeliczWykorzystaneWorker
{
    Pracownicy = new[] { pracownik },
    Pars = new LimitNieobecnosci.Pracownicy.PrzeliczWykorzystaneWorker.Params(Context.Empty.Clone(session))
    {
        Definicja = defUrlop,
        Okres     = rok
    }
};
przelicz.PrzeliczWykorzystane();
session.Save();
```

**Pułapki:**
- **Nie** twórz `new LimitNieobecnosci(...)` — limit powstaje przez naliczanie (jak w D7).
- `PrzeliczWykorzystane` aktualizuje pole `LimitNieobecnosci.Wykorzystane` na podstawie wprowadzonych
  nieobecności — ma sens dopiero **po** naliczeniu limitu i wprowadzeniu nieobecności limitowanych.
- `Razem` może wynosić `0` dla pracownika bez danych napędzających wymiar — opieraj asercje na spójności
  (`Wykorzystane == Razem - Pozostalo`), nie na `Razem > 0` (patrz D7).
- Workery wykonują własne transakcje — wywołuj poza otwartą transakcją edycyjną; obsłuż wyjątki z `Save()`.

---

### D9 — Aktualizacja podstaw nieobecności ZUS / podstaw urlopu

**Cel:** odczytać/wprowadzić ręcznie podstawy naliczania zasiłków (chorobowe/macierzyńskie/opiekuńcze/
rehabilitacyjne) używane przy nieobecnościach ZUS — np. przy wdrożeniu lub korekcie podstawy.

**Fakty o typie (zweryfikowane skanem DLL):**

- Kolekcja na pracowniku: **`pracownik.PodstawyNieobecności: SubTable<Soneta.Place.PodstawaNieobecnosci>`**
  (jest też `PodstawyNieobecnościOkresowe: SubTable<PodstawaNieobecnosciOkresowa>`).
- **`Soneta.Place.PodstawaNieobecnosci`** — tabela `PodstawyNieobec`, `GuidedRow` **child** pracownika
  (relacja przez pole `Pracownik`).
- **Brak publicznego ctora** — `PodstawaNieobecnosci` ma jedynie ctory niepubliczne
  (`(RowCreator)`, `(Pracownik, TypyPodstawNieobecnosci)`). Rekordy powstają z **naliczenia wypłaty**;
  w kodzie biznesowym/teście realnie testowalny jest **odczyt** (dodawanie ręczne — patrz pułapki/spec).

**Pola i typy (`PodstawaNieobecnosci`) — bazodanowe, zapisywalne:**

| Pole | Typ | Opis |
|---|---|---|
| `Data` | `Soneta.Types.Date` | data podstawy |
| `Miesieczne` | `decimal` | podstawa miesięczna |
| `Kwartalne` / `Roczne` | `decimal` | składowe |
| `Podstawa` | `decimal` | podstawa naliczania chorobowego |
| `PodstawaM` / `PodstawaO` / `PodstawaR` | `decimal` | podstawa macierzyńskiego / opiekuńczego / rehabilitacyjnego |
| `Typ` | `Soneta.Place.TypyPodstawNieobecnosci` | `Chorobowa` / `Wypoczynkowy` |
| `Norma` / `NormaDni` | `Time` / `int` | norma czasu/dni |
| `Praca` / `PracaDni` | `Time` / `int` | przepracowane |
| `ProcentSkladki` | `Soneta.Types.Percent` | procent składki |

> **Podstawy urlopu wypoczynkowego** rozróżnia pole `Typ = TypyPodstawNieobecnosci.Wypoczynkowy`;
> podstawy zasiłków ZUS → `Typ = Chorobowa`.

**Snippet — odczyt podstaw + dodanie podstawy ręcznej:**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];

// Odczyt podstaw chorobowych (filtr serwerowy po Typ):
foreach (PodstawaNieobecnosci p in
         pracownik.PodstawyNieobecności[(PodstawaNieobecnosci x) => x.Typ == TypyPodstawNieobecnosci.Chorobowa])
{
    // p.Data, p.Podstawa, p.Miesieczne
}

// UWAGA: PodstawaNieobecnosci NIE ma publicznego ctora — normalnie powstaje z naliczenia wypłaty.
// Ręczne dodanie wymagałoby niepublicznego API → w teście testuj wyłącznie ODCZYT (powyżej).
```

**Pułapki:**
- Kwoty (`Miesieczne`, `Podstawa`, …) są typu `decimal` — to dane operacyjne podstaw; **normalnie
  podstawy powstają z naliczenia wypłaty** (brak publicznego ctora — patrz wyżej).
- `Pracownik` na podstawie jest read-only (guided-parent).
- Filtruj serwerowo po `Typ` (`PodstawyNieobecności[condition]`) — nie iteruj całości z `if` w pamięci.
- W teście na czystej Demo kolekcja `PodstawyNieobecności` może być pusta, dopóki nie naliczono wypłaty
  z zasiłkiem — testuj odczyt asercją na model/spójność, a scenariusz „dodaj ręcznie" oznacz `[Ignore]`.

---

### D10 — Bilans otwarcia nieobecności i urlopów

**Cel:** wprowadzić bilans otwarcia (BO) przy wdrożeniu / starcie roku — historię chorobową (okres
zasiłkowy, dni wykorzystane) oraz urlop wykorzystany u poprzednich pracodawców / w pierwszym miesiącu.

**Fakty o typie (zweryfikowane skanem DLL):**

- BO leży na rekordzie historycznym **`Soneta.Kadry.PracHistoria`** w dwóch subrowach (bazodanowe,
  zapisywalne):
  - **`PracHistoria.ChorobowyBO: Soneta.Kadry.ChorobowyBO`** (BO chorobowy / okres zasiłkowy),
  - **`PracHistoria.DodatkowyBO: Soneta.Kadry.DodatkowyBO`** (BO urlopowy — urlop u poprzednich pracodawców).
- BO nieobecności pojedynczej oznacza też flaga `Nieobecnosc.BilansOtwarcia: bool`
  (interfejs `IBilansOtwarcia` na `Nieobecnosc`).

**Pola i typy (`ChorobowyBO`) — bazodanowe:**

| Pole | Typ | Opis |
|---|---|---|
| `Data` | `Soneta.Types.Date` | data BO |
| `MiesiacPodstawy` | `Soneta.Types.YearMonth` | miesiąc podstawy |
| `Podstawa` | `decimal` | podstawa BO |
| `DniWynagrodzenia` | `int` | dni zwolnienia finansowane przez pracodawcę |
| `DniZasilkowe` | `int` | dni wliczane do bieżącego okresu zasiłkowego |
| `DniZwolnienia` | `int` | dni nieprzerwanego zwolnienia dobrowolnego |
| `ZasilekOdDnia` | `Soneta.Types.Date` | zasiłek od dnia |
| `PrzedluzenieOZ` | `bool` | okres zasiłkowy przedłużony o 3 mies. |

**Pola i typy (`DodatkowyBO`) — bazodanowe:**

| Pole | Typ | Opis |
|---|---|---|
| `UPoprzednich` | `decimal` | urlop wykorzystany u poprzednich pracodawców (dni) |
| `Wykorzystany` | `Soneta.Types.Time` | wykorzystany przypadający na bieżące zatrudnienie (godz.) |
| `BezPierwszego` | `bool` | prawo do urlopu w 1. mies. nabyte u poprzedniego pracodawcy |

**Snippet — wprowadzenie BO chorobowego i urlopowego na zapisie historycznym:**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
var historia  = pracownik.Historia[Date.Today];   // właściwy zapis historyczny „na dzień"

using (var t = session.Logout(editMode: true))
{
    // BO chorobowy / okres zasiłkowy
    historia.ChorobowyBO.DniZasilkowe = 33;
    historia.ChorobowyBO.ZasilekOdDnia = new Date(2026, 1, 1);
    // BO urlopowy
    historia.DodatkowyBO.UPoprzednich = 10m;
    t.Commit();
}
session.Save();
```

**Pułapki:**
- `ChorobowyBO`/`DodatkowyBO` to **subrowy** zapisu `PracHistoria` — nie tworzysz ich osobno, edytujesz
  ich pola na istniejącym zapisie historycznym.
- **`ChorobowyBO`** (`DniZasilkowe`, `ZasilekOdDnia`, `PrzedluzenieOZ`, …) jest **zapisywalny** na zwykłym
  zapisie historii (zweryfikowane testem D10 na Demo).
- **`DodatkowyBO`** (`UPoprzednich`, `BezPierwszego`, `Wykorzystany`) na zwykłym zapisie historii Demo
  rzuca **`ColReadOnlyException`** („pole w trybie tylko do odczytu") — BO urlopowy jest zapisywalny tylko
  na zapisie historycznym oznaczonym jako **bilans otwarcia / start zatrudnienia**, nie na dowolnym zapisie
  „na dzień". W teście na gotowych pracownikach Demo dodawanie `DodatkowyBO` oznacz `[Ignore]`.
- Pobierz właściwy zapis historyczny przez `pracownik.Historia[data]` (patrz A14/A15) — edycja BO na
  niewłaściwym zapisie da błędne dane „na dzień".
- BO ma sens przy wdrożeniu — nie miesza się z normalnym naliczaniem; po wprowadzeniu wpływa na limity
  (D8) i okres zasiłkowy (D6) dopiero przy przeliczeniu/naliczeniu.

---

### D11 — Wnioski o urlop / delegację

**Cel:** zarejestrować wniosek urlopowy (lub o delegację), zmienić jego stan (akceptacja/odrzucenie/
przywrócenie) i — docelowo — przekształcić zaakceptowany wniosek w nieobecność.

**Fakty o typie (zweryfikowane skanem DLL):**

- Wniosek urlopowy: **`Soneta.Kadry.WniosekUrlopowy`** — tabela `WnioskiUrlopowe`, `GuidedRow` root.
  Konstruktory publiczne: **`new WniosekUrlopowy(Pracownik pracownik)`** oraz
  **`new WniosekUrlopowy(Pracownik pracownik, DefinicjaNieobecnosci definicja)`**.
- Kolekcja na pracowniku: **`pracownik.WnioskiUrlopowe: SubTable<Soneta.Kadry.WniosekUrlopowy>`**
  (oraz `WnioskiKierownika`, `WnioskiZastępcy` — te same wnioski w roli kierownika/zastępcy).
- Pola `WniosekUrlopowy` (bazodanowe, zapisywalne): `Pracownik: Pracownik`,
  `Definicja: DefinicjaNieobecnosci`, `Okres: FromTo`, `Data: Date`, `DataDecyzji: Date`,
  `Kierownik: Pracownik`, `Opis: MemoText`, `Stan: Soneta.Kadry.StanWnioskuUrlopowego`.
  - `StanWnioskuUrlopowego`: `Oczekujący`, `Anulowany`, `Zaakceptowany`, `Odrzucony`, `Korygowana`.
- Wniosek o delegację jest subrowem wniosku: `WniosekUrlopowy.Delegacja: Soneta.Kadry.WniosekODelegację`
  (`DataRozpoczeciaPlanowana`, `DataZakonczeniaPlanowana: DateShortTime`, `KrajDocelowy`, `Cel: MemoText`,
  `WnioskowanaZaliczka: Currency`); samodzielny `new WniosekODelegację()` ma publiczny ctor bezparametrowy.
- **Planowane nieobecności** (osobny model, np. plan urlopów): kolekcja
  **`pracownik.PlanowaneNieobecności: FromToSubTable<Soneta.Kalend.PlanowanaNieobecność>`**;
  typ `PlanowanaNieobecność` (tabela `PlanNieobecnosci`, root) z ctorem
  **`new PlanowanaNieobecność(Pracownik pracownik)`**, polami `Definicja`, `Okres: FromTo`.
  - **`Definicja` musi mieć zaznaczone pole `Planowana`** (`DefinicjaNieobecnosci.Planowana == true`) —
    inaczej setter rzuca `RowException` „Wybrana definicja musi mieć zaznaczone pole 'Planowana'."; dobierz
    definicję dynamicznie: `DefNieobecnosci.Cast<DefinicjaNieobecnosci>().First(d => d.Planowana)`.
  - **`Stan: StanPlanowanejNieobecności` jest READ-ONLY** (`Oczekująca`, `Wprowadzona`, `Korygowana`,
    `Zatwierdzona`, `Anulowana`) — **nie przypisujesz** go wprost (`plan.Stan = …` → błąd kompilacji
    „cannot be assigned to"); przejścia stanu wykonujesz metodami domenowymi
    **`StanWprowadzona()` / `StanZatwierdzona()` / `StanAnulowana()` / `StanOczekująca()`**.
- Akceptacja/odrzucenie/przywrócenie z poziomu Pulpitu: worker (UI/Net)
  **`PracownikNetWnioskiUrlopowe`** z akcjami „Zatwierdź wniosek"/`Zatwierdz`, „Odrzuć wniosek"/`Odrzuc`,
  „Przywróć wniosek"/`Przywroc`. W kodzie biznesowym/teście prościej ustawiać `Stan` wprost.

**Snippet — rejestracja wniosku urlopowego + akceptacja:**

```csharp
var kalend    = session.GetKalend();
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
// UWAGA: dla definicji limitowanej (np. „Urlop wypoczynkowy") akceptacja wniosku (set Stan) wyzwoli
// przeliczenie limitu → LimitNotFoundException, jeśli limit nie został wcześniej naliczony (patrz pułapki).
// Tu używamy definicji bezlimitowej (np. „Urlop bezpłatny (art 174 kp)") albo najpierw naliczamy limit (D8).
var defUrlop  = kalend.DefNieobecnosci.WgNazwy["Urlop bezpłatny (art 174 kp)"];

using (var t = session.Logout(editMode: true))
{
    var wniosek = session.AddRow(new WniosekUrlopowy(pracownik, defUrlop));
    wniosek.Okres = new FromTo(new Date(2026, 8, 3), new Date(2026, 8, 7));
    wniosek.Data  = Date.Today;
    wniosek.Stan  = StanWnioskuUrlopowego.Oczekujący;
    t.Commit();
}
session.Save();

// Akceptacja (zmiana stanu):
using (var t = session.Logout(editMode: true))
{
    var wniosek = pracownik.WnioskiUrlopowe
        .Cast<WniosekUrlopowy>()
        .First(w => w.Stan == StanWnioskuUrlopowego.Oczekujący);
    wniosek.Stan        = StanWnioskuUrlopowego.Zaakceptowany;
    wniosek.DataDecyzji = Date.Today;
    t.Commit();
}
session.Save();
```

**Pułapki:**
- **Akceptacja wniosku na definicji limitowanej rzuca `LimitNotFoundException`** bez wcześniej naliczonego
  limitu: ustawienie `Stan` (np. `Zaakceptowany`) na wniosku z definicją „Urlop wypoczynkowy" wewnętrznie
  ustawia `Okres` nieobecności i wyzwala `DefinicjaLimitu.Przelicz(...)`, który dla pracownika bez limitu
  na ten dzień rzuca wyjątek. Rozwiązanie: albo nalicz limit (D8) **przed** zmianą stanu, albo do scenariusza
  obsługi samego rekordu wniosku użyj definicji **bezlimitowej** (np. „Urlop bezpłatny (art 174 kp)").
- **Przekształcenie wniosku w nieobecność** wymaga, by nieobecność limitowana miała naliczony limit
  (jak D1) — sama akceptacja wniosku nie tworzy automatycznie rozliczonej nieobecności w teście bez
  naliczonego limitu/wypłaty.
- `WniosekODelegację` to subrow wniosku (`WniosekUrlopowy.Delegacja`) — wnioskowanie o delegację
  ustawiasz na tym subrowie; pełne rozliczenie delegacji to moduł `Soneta.Delegacje` (osobny dokument
  handlowy PWS), poza zakresem wniosku.
- Filtruj kolekcję wniosków przez `WnioskiUrlopowe[condition]` lub iteruj w zakresie jednego pracownika;
  nie skanuj globalnej tabeli `WnioskiUrlopowe` bez zakresu (tabela operacyjna guided).
- Stan zmieniaj świadomie wg enuma `StanWnioskuUrlopowego` — workery Net robią to samo z dodatkową
  logiką workflow (powiadomienia), której w teście jednostkowym nie odtworzysz.

---

### D12 — Praca zdalna (wnioski, lokalizacje, ewidencja)

**Cel:** skonfigurować pracę zdalną pracownika (model pracy, limit pracy zdalnej okazjonalnej),
zarejestrować wniosek o pracę zdalną i lokalizacje jej świadczenia oraz odczytać ewidencję.

**Fakty o typie (zweryfikowane skanem DLL):**

- Parametry pracy zdalnej leżą na etacie/historii: **`PracHistoria.PracaZdalna: Soneta.Kadry.PracZdalna`**
  (subrow, bazodanowe, zapisywalne):
  - `ModelPracy: Soneta.Kadry.ModelPracy` (`NieDotyczy`, `PracaStacjonarna`, `PracaHybrydowa`, `PracaZdalna`),
  - `OswiadczenieWarunki: bool` (warunki lokalowe/techniczne),
  - `LimitPZ: int`, `IndywidualnyLimitPZ: bool`, `TypLimituPZ: TypLimituPracyZdalnej`
    (`Roczny`, `Miesieczny`, `Tygodniowy`, `Kwartalny`, `Półroczny`).
- Lokalizacje pracy zdalnej: **`pracownik.LokalizacjePracyZdalnej: SubTable<Soneta.Kadry.LokalizacjaPracyZdalnej>`**
  (tabela `LokPracZdalnej`).
- Wnioski o pracę zdalną: **`pracownik.WnioskiPracyZdalnej: SubTable<Soneta.Kalend.WniosekPracyZdalnej>`**
  (oraz `WnioskiPracyZdalnejKierownika`); typ `WniosekPracyZdalnej` ma ctor
  `(Pracownik, DefinicjaRodzajuPracyZdalnej)` — **ctory są niepubliczne**, więc tworzenie wniosku idzie
  przez worker (`GrupoweZleceniePracyZdalnejWorker`) lub Pulpit, nie wprost `new`.
- Lokalizacja pracy zdalnej: `Soneta.Kadry.LokalizacjaPracyZdalnej` ma **publiczny ctor
  `new LokalizacjaPracyZdalnej(Pracownik pracownik)`**.
- Ewidencja/odczyt limitu pracy zdalnej okazjonalnej: worker
  **`Soneta.Kadry.Pracownik.PracaZdalnaWorker`** — property odczytowe (bez akcji modyfikującej):
  `DniPracyZdalnejRazem: int`, `DniPracyZdalnejOkazjonalnej: int`, `DniPracyZdalnejOkazjonalnejLimit: int`,
  `CzasPracyZdalnejRazem: Time`, `LimitPracaZdalnaOkazjonalna: int`, `PozostaloPracaZdalnaOkazjonalna: int`;
  kontekst: `Pracownik: Pracownik`, `Okres: FromTo`.
- Grupowe zlecenie pracy zdalnej (Pulpit/seryjne): worker
  **`Soneta.Kadry.UI.KadryNet.Workers.GrupoweZleceniePracyZdalnejWorker`** (akcja
  „Dodaj wnioski zlecenia pracy zdalnej"/`DodajZleceniaPracyZdalnej`): `Pracownicy: Pracownik[]`,
  `Pars.Okres: FromTo`, `Pars.Data: Date`, `Pars.Uwagi: string`.
- Aktualizacja podzielników kosztów na podstawie pracy hybrydowej: worker
  **`AktualizujPodzielnikowPracaZdalnaWorker`** (`DefinicjaPodzielnika`, `Okres: YearMonth`, …).

**Snippet — ustawienie modelu pracy zdalnej + lokalizacja + wniosek:**

```csharp
var kadry     = session.GetKadry();
var pracownik = kadry.Pracownicy.WgKodu["006"];

using (var t = session.Logout(editMode: true))
{
    var historia = pracownik.Historia[Date.Today];
    historia.PracaZdalna.ModelPracy          = ModelPracy.PracaHybrydowa;
    historia.PracaZdalna.OswiadczenieWarunki = true;

    // lokalizacja pracy zdalnej (np. adres domowy)
    var lok = session.AddRow(new LokalizacjaPracyZdalnej(pracownik));
    // … pola adresowe lokalizacji wg LokalizacjaPracyZdalnej
    t.Commit();
}
session.Save();

// Odczyt ewidencji pracy zdalnej okazjonalnej (worker odczytowy):
// Pracownik i Okres są zwykłymi, settowalnymi property (nie trzeba przekazywać przez Context):
var pz = new Soneta.Kadry.Pracownik.PracaZdalnaWorker
{
    Pracownik = pracownik,
    Okres     = FromTo.Year(new Date(2026, 1, 1))
};
// odczyt: pz.DniPracyZdalnejRazem, pz.LimitPracaZdalnaOkazjonalna, pz.PozostaloPracaZdalnaOkazjonalna
```

**Pułapki:**
- `PracaZdalnaWorker` to worker **odczytowy** (ma property, brak akcji modyfikującej) — służy do
  prezentacji ewidencji/limitu, nie do zapisu.
- `ModelPracy`/`OswiadczenieWarunki` są na **historycznym** zapisie etatu (`PracHistoria.PracaZdalna`) —
  edytuj właściwy zapis „na dzień".
- `WniosekPracyZdalnej` ma **niepubliczne ctory** — w teście jednostkowym nie utworzysz go przez `new`;
  zlecenie pracy zdalnej idzie przez worker `GrupoweZleceniePracyZdalnejWorker` (czynność Net/UI,
  wymaga `Context`). Testuj raczej `ModelPracy`/`OswiadczenieWarunki` na `PracHistoria.PracaZdalna`
  i `LokalizacjaPracyZdalnej` (ma publiczny ctor).
- `LokalizacjaPracyZdalnej` ma publiczny ctor `(Pracownik)` — testowalna wprost.

## E. Plan pracy i kalendarz

> **Model kalendarza pracownika.** Każdy `Pracownik` ma kalendarz roboczy
> (`pracownik.Etat.Kalendarz : Soneta.Kalend.Kalendarz`), którego dni leżą w tabeli
> `DniKalendarza` (`DzienKalendarzaBase`, child kalendarza). Pracownik wystawia trzy
> niezależne kolekcje dni typu `DateSubTable` (indeksator po dacie `[Date]`, **tylko do
> odczytu** — element tworzysz konstruktorem + `AddRow`):
> - `pracownik.DniPlanu : DateSubTable` — **plan/harmonogram** (dni `DzienPlanu : DzienKalendarzaBase`); to `pracownik.Etat.Kalendarz.Dni`.
> - `pracownik.DniPracy : DateSubTable<Soneta.Kalend.DzienPracy>` — **ewidencja** (realizacja) czasu pracy.
> - `pracownik.DniRCP : DateSubTable<Soneta.Kalend.DzienRCP>` — **zarejestrowany** czas pracy (RCP) — patrz sekcja F.
>
> Wszystkie dni współdzielą subrow `Praca : Soneta.Kalend.CzasPracy` z polami
> `OdGodziny`/`DoGodziny`/`Czas : Soneta.Types.Time`. Definicja dnia (`Definicja :
> Soneta.Kalend.DefinicjaDnia`) to rekord **konfiguracyjny** (słownik `DefinicjeDni`,
> indeksator `[Kod]`).
>
> **Ograniczenie wykonalności.** Plan i ewidencja są normalnie wyliczane przez kalkulator
> czasu pracy z definicji kalendarza/serii — ręczne tworzenie pojedynczego dnia jest możliwe
> publicznym kontraktem (ctor `(Pracownik, Date)` + `AddRow`), ale **wymaga zdefiniowanego
> `DefinicjaDnia` w konfiguracji**. Operacje masowe (przeliczenie planu na okres) są zaszyte
> w workerach/kalkulatorach UI — patrz E2.

### E1 — Wprowadzanie planowanego czasu pracy (★)

**Cel:** odczytać lub ustawić plan pracy (harmonogram) pracownika na konkretny dzień —
godziny od–do, normę dobową oraz typ dnia.

**Pola i typy:**

| Element | Lokalizacja | Typ | Uwaga |
|---|---|---|---|
| Plan pracy (cała kolekcja) | `pracownik.DniPlanu` | `Soneta.Business.DateSubTable` | == `pracownik.Etat.Kalendarz.Dni`; indeksator `[Date]` (get) |
| Dzień planu | `pracownik.DniPlanu[data]` | `Soneta.Kalend.DzienPlanu` (`DzienKalendarzaBase`) | `null`, gdy dla daty brak dnia planu |
| Data dnia | `DzienPlanu.Data` | `Soneta.Types.Date` | bazodanowe; ustawiane przez ctor |
| Godziny pracy (subrow) | `DzienPlanu.Praca` | `Soneta.Kalend.CzasPracy` | `Praca.OdGodziny`, `Praca.DoGodziny`, `Praca.Czas : Time` (zapisywalne) |
| Czas (norma dnia, odczyt) | `DzienPlanu.Czas` | `Soneta.Types.Time` | kalkulowane (czas pracy dnia) |
| Od (odczyt) | `DzienPlanu.OdGodziny` | `Soneta.Types.Time` | kalkulowane |
| Definicja dnia | `DzienPlanu.Definicja` | `Soneta.Kalend.DefinicjaDnia` | rekord słownika konfiguracyjnego `DefinicjeDni` |
| Tolerancja wejścia | `DzienPlanu.TolerancjaWe` | `Soneta.Types.Time` | bazodanowe |
| Norma dobowa kalendarza | `pracownik.Etat.Kalendarz.NormaDobowa` | `Soneta.Types.Time` | poziom kalendarza, nie dnia |
| Słownik definicji dni | `session.GetKalend().DefinicjeDni` | `DefinicjeDni` | indeksator `[kod: string]`; skróty `WolnaSobota`, `Niedziela` |

**Snippet:**

```csharp
var kalend = session.GetKalend();
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];

// --- Odczyt planu na dzień (bezpiecznie: indeksator zwraca null dla braku dnia) ---
var data = new Date(2026, 6, 1);
var dzienPlanu = (DzienPlanu)pracownik.DniPlanu[data];
if (dzienPlanu is not null)
{
    Time odGodz = dzienPlanu.Praca.OdGodziny;   // np. 8:00
    Time doGodz = dzienPlanu.Praca.DoGodziny;   // np. 16:00
    Time normaDnia = dzienPlanu.Czas;           // wyliczona norma dnia (kalkulowane)
    DefinicjaDnia typDnia = dzienPlanu.Definicja;
}

// --- Ustawienie/utworzenie dnia planu (wymaga DefinicjaDnia z konfiguracji) ---
using (var t = session.Logout(editMode: true))
{
    var dp = (DzienPlanu)pracownik.DniPlanu[data];
    if (dp is null)
    {
        dp = session.AddRow(new DzienPlanu(pracownik, data));   // ctor (Pracownik, Date)
        dp.Definicja = kalend.DefinicjeDni["RB"];               // typ dnia ze słownika (np. dzień roboczy)
    }
    dp.Praca.OdGodziny = new Time(8, 0);
    dp.Praca.DoGodziny = new Time(16, 0);   // Czas dnia wylicza się z od–do

    t.Commit();
}
session.Save();
```

**Pułapki:**
- `DniPlanu` to `DateSubTable` **nietypowany** (zwraca `Row`) — rzutuj na `DzienPlanu`. Indeksator
  `[Date]` jest **tylko do odczytu**: nowego dnia nie „przypiszesz", tworzysz go ctorem
  `new DzienPlanu(pracownik, data)` + `session.AddRow(...)`.
- Godziny ustawiasz na **subrowie** `Praca` (`dp.Praca.OdGodziny = …`), nie na `dp.OdGodziny` —
  to ostatnie jest kalkulowane (read-only). Po ustawieniu od–do `Praca.Czas`/`Czas` przeliczają się.
- `Definicja` to rekord **konfiguracyjnego** słownika `DefinicjeDni` — pobierz istniejący wpis
  (`kalend.DefinicjeDni[kod]`), nie twórz „w locie". Bez przypisanego `Definicja` świeży dzień planu
  może nie przejść weryfikatorów.
- Plan jest zwykle generowany przez kalkulator z definicji kalendarza (serie dni, święta) —
  ręczne nadpisywanie pojedynczego dnia to korekta, nie sposób budowy całego harmonogramu (do tego
  służy operacja seryjna / kopiowanie planu, E2).
- Norma dobowa to atrybut **kalendarza** (`Etat.Kalendarz.NormaDobowa`), nie pojedynczego dnia.

### E2 — Planowanie czasu pracy grupy (kopiowanie planu) (★)

**Cel:** skopiować wyliczony plan pracy (harmonogram) na wskazany okres — dla jednego pracownika
albo dla grupy, oraz seryjnie zaktualizować kalendarz pracowników (zmiana kalendarza docelowego).

**Publiczny kontrakt — dwie drogi:**

| Operacja | API | Charakter |
|---|---|---|
| Kopiowanie **planu** pracownika na okres | `Soneta.Kalend.KalendarzPlanuKopia.Kopiuj(Pracownik pracownik, FromTo okres)` (**public static**) | bez UI — proste API |
| Kopiowanie **pracy/realizacji** na okres | `Soneta.Kalend.KalendarzPracyKopia.Kopiuj(Pracownik pracownik, FromTo okres)` (**public static**) | bez UI — proste API |
| Kopiowanie grupy (worker UI) | `KalendarzPlanuKopia.KopiujWorker` / `KalendarzPracyKopia.KopiujWorker` | wymaga `Context` z zaznaczeniem |
| Aktualizacja kalendarza grupy | `Soneta.Kadry.AktualizujKalendarzWorker` | wymaga `Params` z `Context` |

**Worker `KopiujWorker` (BI/„Kopiuj plan…", „Kopiuj pracę…"):** klasa `ContextBase` z ctorem
`(Context context)`; pola `[Context] FromTo Okres`, `[Context] Pracownik[] Pracownicy`; metoda
`void Kopiuj()`. Działa **wyłącznie** z kontekstem UI (zaznaczona lista pracowników) i jest gardzona
licencją BI/BI_PL/PL oraz `IsVisibleKopiuj` (niedostępny na mobile).

**Worker `AktualizujKalendarzWorker`:** pola `[Context] Pracownik[] Pracownicy`,
`Params Pars` (`Pars.Data`, `Pars.TylkoOstatni: bool`, `Pars.PowodAktualizacji: string`,
`Pars.Kalendarze: KalendarzBase[]`, `Pars.Docelowy: Kalendarz`, `Pars.Zmiana: bool`,
`Pars.Interpretacja`), metoda `void Aktualizuj()`. `Params` to `ContextBase` (ctor `(Context)`).

**Snippet (proste API dla jednego pracownika — bez UI):**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
var okres = new FromTo(new Date(2026, 6, 1), new Date(2026, 6, 30));

using (var t = session.Logout(editMode: true))
{
    // Wylicza plan z kalendarza i zapisuje do kopii planu pracownika za wskazany okres:
    KalendarzPlanuKopia.Kopiuj(pracownik, okres);     // public static
    // analogicznie realizacja:  KalendarzPracyKopia.Kopiuj(pracownik, okres);
    t.Commit();
}
session.Save();
```

**Snippet (grupa — przez worker; wymaga Context z zaznaczeniem):**

```csharp
// Tylko w warstwie UI/Czynności — Context dostarcza zaznaczonych pracowników.
var worker = new KalendarzPlanuKopia.KopiujWorker(context)
{
    Okres = new FromTo(new Date(2026, 6, 1), new Date(2026, 6, 30)),
    Pracownicy = context.Get<Pracownik[]>()
};
worker.Kopiuj();   // wewnątrz: Session.Logout + Commit
```

**Pułapki:**
- **Kopiowanie grupy nie ma „czystego" API bezkontekstowego** — `KopiujWorker` i
  `AktualizujKalendarzWorker.Params` dziedziczą po `ContextBase` i wymagają `Context` (zaznaczenie z
  listy UI). Dla kodu serwerowego/testów używaj **publicznej statycznej** `KalendarzPlanuKopia.Kopiuj(pracownik, okres)`
  w pętli po pracownikach — to ona realizuje właściwą logikę (worker w `KopiujInt` woła ją per pracownik).
- `KopiujWorker.Kopiuj()` jest gardzony licencją (BI/BI_PL/PL) i `IsVisibleKopiuj` (m.in. blokada na
  mobile) — to logika UI, nie wywołuj jej z kodu biznesowego.
- Kopia planu/pracy trafia do **osobnych** kolekcji `pracownik.DniPlanuKopia`/`pracownik.DniPracyKopia`
  (`DateSubTable`), powiązanych z `KalendarzPlanuKopia`/`KalendarzPracyKopia` — to bufor kopii, odrębny
  od właściwego `DniPlanu`/`DniPracy`.
- `okres` jest normalizowany przez setter workera do pełnych miesięcy (otwarty `From`/`To` →
  pierwszy/ostatni dzień miesiąca); przy statycznym `Kopiuj` podawaj zamknięty `FromTo`.
- Operacja seryjna na grupie pracowników = długa transakcja → dziel na paczki, trzymaj transakcje
  krótkie (safe-code §13.1).

### E3 — Aktualizacja kalendarza pracownika (operacja seryjna „Zaktualizuj kalendarz pracownika")

**Cel:** seryjnie zmienić kalendarz roboczy zaznaczonych pracowników (zmiana kalendarza
docelowego, przeliczenie planu na nowy kalendarz od wskazanej daty) — operacja z menu
„Czynności" na liście pracowników.

**Publiczny kontrakt — worker `Soneta.Kadry.AktualizujKalendarzWorker`:**

| Element | Sygnatura / typ | Uwaga |
|---|---|---|
| Konstruktor | `new AktualizujKalendarzWorker()` | bezparametrowy; worker UI |
| Pracownicy (wejście) | `Pracownicy : Pracownik[]` | **set-only**; karmione z `Context` (zaznaczenie listy) |
| Parametry | `Pars : Params` | **set-only**; `Params` to `ContextBase`, ctor `(Context context)` |
| Wykonanie | `void Aktualizuj()` | właściwa operacja seryjna (Logout + Commit wewnątrz) |

**`Soneta.Kadry.AktualizujKalendarzWorker.Params` (`: ContextBase`, ctor `(Context)`):**

| Pole | Typ | Uwaga |
|---|---|---|
| `Data` | `Soneta.Types.Date` | data, od której obowiązuje nowy kalendarz |
| `TylkoOstatni` | `bool` | aktualizuj tylko ostatni (bieżący) zapis historyczny |
| `PowodAktualizacji` | `string` | opis powodu (do dokumentu aktualizacji) |
| `Kalendarze` | `KalendarzBase[]` | kalendarze źródłowe objęte zmianą; lista przez `GetListKalendarze()` |
| `Docelowy` | `Soneta.Kalend.Kalendarz` | kalendarz docelowy; lista przez `GetListDocelowy()` |
| `Zmiana` | `bool` | flaga: czy zmienić kalendarz (a nie tylko przeliczyć) |
| `Interpretacja` | `Soneta.Kadry.InterpretacjaKalendarza` | `WgPlanu` / `WgObecnosci` / `WgZestawien`; `IsReadOnlyInterpretacja()` |

**Snippet (warstwa UI/Czynności — wymaga `Context` z zaznaczeniem):**

```csharp
// Tylko w warstwie UI: Context dostarcza zaznaczonych pracowników.
var worker = new AktualizujKalendarzWorker
{
    Pracownicy = context.Get<Pracownik[]>(),
    Pars = new AktualizujKalendarzWorker.Params(context)
    {
        Data = new Date(2026, 7, 1),
        Docelowy = session.GetKalend().Kalendarze.WgKodu["PODSTAWOWY"],
        Zmiana = true,
        Interpretacja = InterpretacjaKalendarza.WgPlanu,
        PowodAktualizacji = "Zmiana systemu czasu pracy"
    }
};
worker.Aktualizuj();   // wewnątrz: Session.Logout + Commit
```

**Pułapki:**
- `Params` dziedziczy po `ContextBase` (ctor `(Context)`) — **nie da się go zbudować bez `Context`**.
  Dlatego E3 nie ma „czystego" API bezkontekstowego; to operacja UI/serwerowa z zaznaczeniem.
- `Pracownicy` i `Pars` są **set-only** — nie odczytasz ich z powrotem; ustaw przed `Aktualizuj()`.
- Operacja seryjna = długa transakcja na wielu pracownikach → w realnym użyciu dziel na paczki
  (safe-code §13.1). Sam worker zarządza transakcją wewnętrznie.
- Zmiana kalendarza jest **historyczna** (operuje na zapisach `Etat`) — `TylkoOstatni`/`Data`
  decydują, których zapisów historycznych dotyczy.

---

### E4 — Uzgodnienie doby pracowniczej (model doby; godziny rozpoczęcia doby)

**Cel:** przesunąć granicę doby pracowniczej dla dnia ewidencji — gdy zmiana zaczyna się w jednej
dobie kalendarzowej, a kończy w następnej (nocna), uzgodnienie „przenosi" początek/koniec pracy do
właściwej doby pracowniczej. Operacja na pojedynczym dniu (`DzienPracy`) lub seryjnie na grupie.

**Model doby (publiczny kontrakt):**

| Element | Lokalizacja | Typ | Uwaga |
|---|---|---|---|
| Początek doby w niedziele/święta | `pracownik.Last.Etat.ConfigPoczątekDobyNiedzieledIŚwięta` | `Soneta.Types.Time` | **read-only** (konfiguracyjne); godzina startu doby |
| Norma dobowa | `pracownik.Last.Etat.NormaDobowa` | `Soneta.Types.Time` | bazodanowe; norma czasu doby |
| Norma dobowa kalendarza | `pracownik.Last.Etat.Kalendarz.NormaDobowa` | `Soneta.Types.Time` | poziom kalendarza |
| Interpretacja kalendarza | `pracownik.Last.Etat.InterpretacjaKalendarza` | `Soneta.Kadry.InterpretacjaKalendarza` | `WgPlanu`/`WgObecnosci`/`WgZestawien` — jak interpretować dobę |

> **Uwaga:** `Etat` leży na bieżącym **zapisie historycznym** (`pracownik.Last.Etat : Soneta.Kadry.Etat`,
> gdzie `Last : PracHistoria`) — nie ma property `pracownik.Etat` bezpośrednio na roocie pracownika.
| Godziny pracy dnia | `DzienPracy.Praca` | `Soneta.Kalend.CzasPracy` | `OdGodziny`/`DoGodziny`/`Czas` — granice realizacji w dobie |

**Worker pojedynczego dnia — `Soneta.Kalend.DzienPracy.UzgodnijDobePracowniczaWorker`:**

| Element | Sygnatura | Uwaga |
|---|---|---|
| Konstruktor | `new DzienPracy.UzgodnijDobePracowniczaWorker()` | |
| Dzień (wejście) | `Dzień : DzienPracy` | **set-only** |
| Warunek dostępności | `static bool IsEnabledUzgodnijDobePracownicza(DzienPracy dzień)` | czy operacja ma sens dla dnia |
| Uzgodnienie | `object UzgodnijDobePracownicza()` | przelicza dobę |
| Przeniesienie początku | `DzienPracy PrzenieśPoczątek()` | przenosi początek pracy do poprz. doby |
| Przeniesienie końca | `DzienPracy PrzenieśKoniec()` | przenosi koniec pracy do nast. doby |
| Dokument aktualizacji | `DokumentAktualizacjiKalendarza : IDokumentAktualizacjiKalendarza`, `DataAktualizacji : System.DateTime` | kontekst historii |

**Worker seryjny (grupa) — `Soneta.Kadry.UzgodnijDobePracowniczaPracownikowWorker`:**

| Element | Sygnatura / typ | Uwaga |
|---|---|---|
| Konstruktor | `new UzgodnijDobePracowniczaPracownikowWorker()` | |
| Pracownicy | `Pracownicy : Pracownik[]` | **set-only**; z `Context` |
| Parametry | `Pars : Params` (`ContextBase`, ctor `(Context)`); pole `Okres : FromTo` | **set-only** |
| Wykonanie | `UzgodnijDobePracowniczaResult UzgodnijDobePracownicza()` | zwraca wynik |

**Snippet (pojedynczy dzień):**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
var dzien = pracownik.DniPracy[new Date(2026, 6, 1)];   // DzienPracy lub null
if (dzien is not null && DzienPracy.UzgodnijDobePracowniczaWorker.IsEnabledUzgodnijDobePracownicza(dzien))
{
    using (var t = session.Logout(editMode: true))
    {
        var worker = new DzienPracy.UzgodnijDobePracowniczaWorker { Dzień = dzien };
        worker.UzgodnijDobePracownicza();
        t.Commit();
    }
    session.Save();
}
```

**Pułapki:**
- Godzina rozpoczęcia doby to atrybut **konfiguracyjny `Etat`** (`ConfigPoczątekDobyNiedzieledIŚwięta`,
  read-only) i normy `Etat.NormaDobowa`/`Etat.Kalendarz.NormaDobowa` — nie ma osobnego, edytowalnego
  pola „początek doby" na pojedynczym `DzienPracy`.
- `Dzień` workera pojedynczego jest **set-only**; `Pracownicy`/`Pars` workera grupowego również.
- Worker grupowy `Params` to `ContextBase` (ctor `(Context)`) — **wymaga `Context`** (zaznaczenie UI),
  brak czystego API bezkontekstowego.
- Uzgodnienie modyfikuje `DzienPracy.Praca` (od–do) i może rozbić pracę na dwie doby — wykonuj w
  transakcji (`Logout(editMode:true)` + `Commit`) i zapisz `Save()`.

---

### E5 — Odczyt normy czasu pracy i czasu przepracowanego za okres (★ testowalne)

**Cel:** dla pracownika odczytać za zadany okres (`FromTo`/`YearMonth`): normę czasu pracy
(planowaną), czas przepracowany (zrealizowany), nadgodziny, czas nocny, liczbę/normę nieobecności —
bez modyfikacji danych (czysty odczyt statystyk).

**Punkt wejścia — `pracownik.Czasy : Soneta.Kalend.KalkulatorPracownika`:**

| Metoda (publiczna, instancyjna) | Zwraca | Znaczenie |
|---|---|---|
| `Norma(FromTo okres, params Item[] condition)` | `CzasDni` | norma (planowana) czasu pracy za okres |
| `Norma(FromTo okres, DefinicjaStrefy def, params Item[] condition)` | `CzasDni` | norma w obrębie strefy |
| `NormaKodeksowa(YearMonth miesiąc)` | `CzasDni` | norma kodeksowa miesiąca (pełny etat) |
| `NormaKodeksowaWym(Fraction wymiar, Time normaDobowa, YearMonth miesiąc)` | `CzasDni` | norma kodeksowa wg wymiaru etatu |
| `Praca(FromTo okres, params Item[] condition)` | `CzasDni` | czas **przepracowany** (zrealizowany) za okres |
| `Praca(FromTo okres, DefinicjaStrefy def, params Item[] condition)` | `CzasDni` | przepracowany w obrębie strefy |
| `PracaRozliczana(FromTo okres, params Item[] condition)` | `CzasDni` | czas pracy rozliczany (do nadgodzin) |
| `PracaZatr(FromTo okres, bool usprPłatne)` | `CzasDni` | praca w okresie zatrudnienia |
| `Nadgodziny(YearMonth okres)` / `Nadgodziny(FromTo okres)` | `ZestawienieNadgodzin` | nadgodziny |
| `NadgodzinyDobaOkres(FromTo okres)` | `ZestawienieNadgodzin` | nadgodziny dobowe/okresowe |
| `Nocne(YearMonth\|FromTo okres)` | `Time` | czas nocny |
| `NormaNie(YearMonth\|FromTo okres, params Item[] condition)` | `CzasDni` | norma nieobecności |
| `DniNie(YearMonth\|FromTo okres, params Item[] condition)` | `int` | liczba dni nieobecności |
| `Nieobecność(Date data[, bool clip])` | `INieobecnosc` | nieobecność w danym dniu |

**`Soneta.Kalend.CzasDni` (typ wyniku):**

| Pole | Typ | Uwaga |
|---|---|---|
| `Czas` | `Soneta.Types.Time` | sumaryczny czas (read-only) |
| `Dni` | `int` | liczba dni (read-only) |
| `CzasDni.Empty`, `CzasDni.Invalid` | `CzasDni` | wartości specjalne; operatory `+`/`-`/`==` |

**`Soneta.Kalend.ZestawienieNadgodzin` (struct):** `N50`, `N100`, `NSW`, `N100Doba`, `N100Okres`,
`Razem` — wszystkie `Time` (read-only); `ZestawienieNadgodzin.Zero`.

**Snippet (czysty odczyt):**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
var kalk = pracownik.Czasy;                                  // KalkulatorPracownika
var okres = new FromTo(new Date(2026, 6, 1), new Date(2026, 6, 30));

CzasDni norma = kalk.Norma(okres);                           // norma planowana
CzasDni przepracowano = kalk.Praca(okres);                   // czas zrealizowany
ZestawienieNadgodzin nadg = kalk.Nadgodziny(new YearMonth(2026, 6));
Time nocne = kalk.Nocne(okres);

Time normaCzas = norma.Czas;          int normaDni = norma.Dni;
Time pracaCzas = przepracowano.Czas;  Time nadgRazem = nadg.Razem;
```

**Pułapki:**
- `KalkulatorPracownika` **nie jest `Row`** — to obiekt liczący (zwykły `object`). Nie zapisuje się,
  nie wymaga transakcji; to czysty odczyt. Pobieraj go zawsze przez `pracownik.Czasy` (ma kontekst
  pracownika), nie twórz ręcznie ctorem chyba że masz `Pracownik` + ewentualny `Log`.
- Parametr `condition` to **serwerowy filtr** (`Item[]`, RowCondition) — można zawęzić np. do strefy;
  zwykle pusty.
- `Norma` = plan, `Praca` = realizacja; nie myl `Praca(okres)` (statystyka) z `DzienPracy` (rekord dnia).
- Wynik `CzasDni.Invalid` sygnalizuje brak danych/błąd okresu — sprawdzaj zanim policzysz różnice.

---

## F. RCP — rejestracja czasu pracy

> **Dwie tabele.** Zarejestrowany (surowy) czas pracy z czytników RCP leży w
> `pracownik.DniRCP : DateSubTable<Soneta.Kalend.DzienRCP>` (tabela `DniRCP`). Pojedyncze zdarzenia
> wejścia/wyjścia (`Soneta.Kalend.WejscieWyjscie`, tabela `WejsciaWyjscia`) są **childem `DzienPracy`**
> (pole `WejscieWyjscie.Dzien : DzienPracy`, kolekcja `dzienPracy.WeWy`), a `DzienPracy` to
> ewidencja w `pracownik.DniPracy`. `DzienRCP` jest stanem zweryfikowanym RCP (z polem
> `StanRCP : StanWeryfikacjiRCP`), powstaje z importu/przeliczenia.

### F1 — Rejestracja czasu pracy pracownika (★)

**Cel:** odczytać zarejestrowany/zewidencjonowany czas pracy pracownika za dzień oraz (gdy trzeba)
utworzyć dzień ewidencji.

**Pola i typy:**

| Element | Lokalizacja | Typ | Uwaga |
|---|---|---|---|
| Ewidencja (kolekcja) | `pracownik.DniPracy` | `DateSubTable<Soneta.Kalend.DzienPracy>` | indeksator `[Date]` (get); element ctorem |
| Dzień ewidencji | `pracownik.DniPracy[data]` | `Soneta.Kalend.DzienPracy` | `null` przy braku |
| RCP zweryfikowane (kolekcja) | `pracownik.DniRCP` | `DateSubTable<Soneta.Kalend.DzienRCP>` | analogicznie |
| Dzień RCP | `pracownik.DniRCP[data]` | `Soneta.Kalend.DzienRCP` | `null` przy braku |
| Przepracowany czas (subrow) | `DzienPracy.Praca` / `DzienRCP.Praca` | `Soneta.Kalend.CzasPracy` | `Praca.OdGodziny`, `Praca.DoGodziny`, `Praca.Czas : Time` |
| Czas/Od (odczyt) | `Dzien*.Czas`, `Dzien*.OdGodziny` | `Soneta.Types.Time` | kalkulowane |
| Stan weryfikacji RCP | `DzienRCP.StanRCP` | `Soneta.Kalend.StanWeryfikacjiRCP` | zapisywalne |
| Flaga importu RCP | `Dzien*.RcpOK` | `bool` | zapisywalne; stan rekordu po imporcie |
| Zdarzenia we/wy dnia | `DzienPracy.WeWy` | `LpSubTable<Soneta.Kalend.WejscieWyjscie>` | patrz F2 |
| Uwagi / błędy (RCP) | `DzienRCP.Uwagi`, `DzienRCP.Bledy` | `Soneta.Business.MemoText` | zapisywalne |

**Snippet:**

```csharp
var kalend = session.GetKadry().Session.GetKalend();
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
var data = new Date(2026, 6, 1);

// --- Odczyt zaewidencjonowanego czasu (ewidencja) ---
var dzienPracy = pracownik.DniPracy[data];          // typowane: DzienPracy lub null
if (dzienPracy is not null)
{
    Time przepracowano = dzienPracy.Praca.Czas;     // suma czasu pracy dnia
    Time od = dzienPracy.Praca.OdGodziny;
    Time @do = dzienPracy.Praca.DoGodziny;
}

// --- Odczyt stanu RCP (zweryfikowany rejestr) ---
var dzienRcp = pracownik.DniRCP[data];              // DzienRCP lub null
if (dzienRcp is not null)
{
    Time czasRcp = dzienRcp.Praca.Czas;
    StanWeryfikacjiRCP stan = dzienRcp.StanRCP;
}

// --- Utworzenie dnia ewidencji (gdy potrzebny ręczny wpis) ---
using (var t = session.Logout(editMode: true))
{
    var dp = pracownik.DniPracy[data];
    if (dp is null)
    {
        dp = session.AddRow(new DzienPracy(pracownik, data));   // ctor (Pracownik, Date)
        kalend.DniPracy.AddRow(dp);                              // alternatywnie przez Module.DniPracy
    }
    dp.Praca.OdGodziny = new Time(8, 0);
    dp.Praca.DoGodziny = new Time(16, 0);
    t.Commit();
}
session.Save();
```

**Pułapki:**
- `DniPracy`/`DniRCP` są **typowane** (`DateSubTable<DzienPracy>` / `<DzienRCP>`) — indeksator
  `[Date]` zwraca od razu właściwy typ lub `null`. Nie iteruj całej kolekcji, by „znaleźć" dzień —
  użyj indeksatora po dacie albo `[FromTo]` dla zakresu.
- Czas pracy ustawiaj na subrowie `Praca` (od–do); `Dzien*.Czas`/`Dzien*.OdGodziny` na rootcie dnia są
  kalkulowane (read-only).
- `DzienRCP` to **wynik weryfikacji** importu RCP (z czytników) — w normalnym przepływie nie tworzysz
  go ręcznie, lecz odczytujesz po imporcie/przeliczeniu. `DzienPracy` (ewidencja) to właściwe miejsce
  na ręczny wpis.
- Świeży `DzienPracy` z `new DzienPracy(pracownik, data)` trzeba dodać do tabeli
  (`Module.DniPracy.AddRow(...)` lub `session.AddRow(...)`) — sam ctor go nie rejestruje.

### F2 — Rejestracja wejścia/wyjścia (RCP) (★)

**Cel:** dodać zdarzenie wejścia/wyjścia do dnia oraz odczytać listę zdarzeń RCP danego dnia.

**Pola i typy (`Soneta.Kalend.WejscieWyjscie`, tabela `WejsciaWyjscia`):**

| Pole | Typ | Uwaga |
|---|---|---|
| `Dzien` | `Soneta.Kalend.DzienPracy` | właściciel (guided-parent); ustawiany przez ctor `(DzienPracy)` |
| `Godzina` | `Soneta.Types.Time` | godzina zdarzenia (zapisywalne) |
| `Typ` | `Soneta.Kalend.TypWejsciaWyjscia` | enum: `Niezdefiniowany`, `Wejscie`, `Wyjscie`, `WejscieSluzbowe`, `WyjscieSluzbowe`, `WejsciePrywatne`, `WyjsciePrywatne` |
| `Operacja` | `int` | kod operacji urządzenia (zapisywalne) |
| `Lp` | `int` | liczba porządkowa zdarzeń w dniu (bazodanowe) |
| `DefinicjaZdarzenia` | `Soneta.Kalend.DefinicjaZdarzeniaRCP` | opcjonalna definicja zdarzenia ze słownika `DefZdarzenRCP` |
| Kolekcja zdarzeń dnia | `DzienPracy.WeWy : LpSubTable<WejscieWyjscie>` | uporządkowana po `Lp` |

**Snippet:**

```csharp
var kalend = session.GetKadry().Session.GetKalend();
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
var data = new Date(2026, 6, 1);

using (var t = session.Logout(editMode: true))
{
    // Upewnij się, że istnieje dzień ewidencji (właściciel zdarzeń):
    var dp = pracownik.DniPracy[data];
    if (dp is null)
    {
        dp = session.AddRow(new DzienPracy(pracownik, data));
        kalend.DniPracy.AddRow(dp);
    }

    // Wejście 8:00
    var we = new WejscieWyjscie(dp);          // ctor wiąże zdarzenie z dniem
    kalend.WejsciaWyjscia.AddRow(we);
    we.Godzina = new Time(8, 0);
    we.Typ = TypWejsciaWyjscia.Wejscie;

    // Wyjście 16:00
    var wy = new WejscieWyjscie(dp);
    kalend.WejsciaWyjscia.AddRow(wy);
    wy.Godzina = new Time(16, 0);
    wy.Typ = TypWejsciaWyjscia.Wyjscie;

    t.Commit();
}
session.Save();

// --- Odczyt zdarzeń dnia ---
var dzien = pracownik.DniPracy[data];
if (dzien is not null)
{
    foreach (WejscieWyjscie wewy in dzien.WeWy)   // posortowane po Lp
    {
        // wewy.Godzina, wewy.Typ, wewy.Operacja
    }
}
```

**Pułapki:**
- `WejscieWyjscie` jest childem **`DzienPracy`**, nie `DzienRCP` — najpierw potrzebujesz dnia
  ewidencji (`pracownik.DniPracy[data]`); zdarzenia wiążesz ctorem `new WejscieWyjscie(dzienPracy)`
  i dodajesz do tabeli `kalend.WejsciaWyjscia.AddRow(...)`.
- `Typ` to enum `TypWejsciaWyjscia` (`Wejscie`/`Wyjscie`/…), nie string ani `int`. Para
  wejście+wyjście jest podstawą wyliczenia czasu dnia z surowych zdarzeń.
- `DefinicjaZdarzenia` jest **opcjonalna** — przy ręcznym wpisie wystarczą `Godzina` + `Typ`. Jeśli
  używasz definicji, pobierz wpis ze słownika konfiguracyjnego `kalend.DefZdarzenRCP` (nie twórz w locie).
- `WeWy` to `LpSubTable` — kolejność zdarzeń wynika z `Lp` (nadawane automatycznie); nie ustawiaj `Lp`
  ręcznie. Do usunięcia wszystkich zdarzeń dnia (przy ponownym imporcie) służy kasowanie elementów kolekcji.
- Surowe zdarzenia są przeliczane na czas pracy/RCP przez kalkulator i import — samo dodanie
  wejść/wyjść nie aktualizuje automatycznie `DzienRCP` (to robi przeliczenie/import RCP).

### F3 — Import danych z RCP (bezpośredni i przez tabelę pośrednią)

**Cel:** wczytać surowe odbicia z czytników RCP i przeliczyć je na ewidencję/zweryfikowany RCP.
**UWAGA: operacja plikowa/sieciowa — opis modelu; samego importu z pliku/urządzenia NIE testujemy.**

**Model danych:**

| Element | Lokalizacja | Typ | Uwaga |
|---|---|---|---|
| Tabela pośrednia (surowe odbicia) | `DzienPracy.WeWy` | `LpSubTable<Soneta.Kalend.WejscieWyjscie>` | zdarzenia we/wy (godzina, typ) — patrz F2 |
| Zarejestrowany RCP (zweryfikowany) | `pracownik.DniRCP[data]` | `Soneta.Kalend.DzienRCP` | wynik importu/przeliczenia |
| Ewidencja | `pracownik.DniPracy[data]` | `Soneta.Kalend.DzienPracy` | docelowa realizacja |
| Flaga importu | `DzienPracy.RcpOK` / `DzienRCP.RcpOK` | `bool` | „stan rekordu po imporcie z RCP" |
| Stan weryfikacji | `DzienRCP.StanRCP` | `StanWeryfikacjiRCP` | patrz F4 |

**Workery przeliczające (po wczytaniu odbić — operują na obiektach sesji):**

| Worker | Sygnatura | Rola |
|---|---|---|
| `Soneta.Kalend.ImportDniaWorker` | ctor `()`, `DzienPracy DzienPracy {get;set;}`, `void Przelicz()` | przelicza pojedynczy dzień z we/wy na czas pracy |
| `Soneta.Kalend.RCPWeryfikatorWorker` | `Dopasuj()`, `DopasujDlaZaznaczonych()`, `Dodaj()`, `Usun()` (+ `IsVisible*`), props `rw : RCPWeryfikator`, `Strefy`, `Wybrana` | dopasowanie odbić do plan/strefy (UI) |

**Snippet (przeliczenie dnia z już wczytanych we/wy — bez pliku):**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
var dzien = pracownik.DniPracy[new Date(2026, 6, 1)];   // dzień z wpisanymi WeWy (F2)
if (dzien is not null)
{
    using (var t = session.Logout(editMode: true))
    {
        new ImportDniaWorker { DzienPracy = dzien }.Przelicz();   // we/wy -> czas pracy
        t.Commit();
    }
    session.Save();
}
```

**Pułapki / wykonalność:**
- **Sam import z pliku/urządzenia (czytnik, sieć, format) jest poza zakresem testu** — wymaga
  zewnętrznego źródła (plik/serwis), brak czystego API w tym kontrakcie.
- **Testowalny fragment**: przygotowanie tabeli pośredniej `DzienPracy.WeWy` (ctor `WejscieWyjscie(dp)`,
  patrz F2) + `ImportDniaWorker.Przelicz()` — to przelicza już-wczytane odbicia bez I/O.
- `RCPWeryfikatorWorker` jest mocno UI (metody `IsVisible*`, `Strefy`/`Wybrana`) — to dopasowanie
  ręczne; nie wywoływać z kodu biznesowego.
- `DzienRCP` powstaje z importu/przeliczenia — w teście nie twórz go „z palca"; odczytuj po `Przelicz()`.

---

### F4 — Weryfikacja i korekta danych RCP (★ testowalne)

**Cel:** odczytać i skorygować zweryfikowany rekord RCP — zmienić stan weryfikacji oraz poprawić
godziny pracy / opisać błędy i uwagi.

**Pola `Soneta.Kalend.DzienRCP` (tabela `DniRCP`, child `Pracownik`):**

| Pole | Typ | Rodzaj | Uwaga |
|---|---|---|---|
| `Data` | `Soneta.Types.Date` | bazodanowe | data dnia (ctor) |
| `Pracownik` | `Soneta.Kadry.Pracownik` | bazodanowe, guided-parent | właściciel |
| `Praca` | `Soneta.Kalend.CzasPracy` | bazodanowe | `Praca.OdGodziny`/`Praca.DoGodziny`/`Praca.Czas : Time` (zapisywalne) |
| `Czas`, `OdGodziny` | `Soneta.Types.Time` | kalkulowane | read-only (z `Praca`) |
| `StanRCP` | `Soneta.Kalend.StanWeryfikacjiRCP` | bazodanowe | stan weryfikacji (zapisywalne) |
| `RcpOK` | `bool` | bazodanowe | stan rekordu po imporcie (zapisywalne) |
| `Uwagi` | `Soneta.Business.MemoText` | bazodanowe | uwagi do weryfikacji |
| `Bledy` | `Soneta.Business.MemoText` | bazodanowe | opis błędów |
| `Strefy` | `SubTable<Soneta.Kalend.StrefaRCP>` | | strefy zarejestrowane |
| `StrefyOrg` | `Soneta.Business.MemoText` | bazodanowe | strefy źródłowe (org.) |

**`Soneta.Kalend.StanWeryfikacjiRCP` (enum):** `DoWeryfikacji`, `WymagaWeryfikacji`,
`PrzekazanyDoWyjaśnienia`, `DoZatwierdzenia`, `Modyfikowany`, `Naniesiony`, `Poprawny`, `Błędny`,
`Wszystkie`.

**Snippet (korekta godzin + zmiana stanu):**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
var dzienRcp = pracownik.DniRCP[new Date(2026, 6, 1)];   // DzienRCP lub null
if (dzienRcp is not null)
{
    using (var t = session.Logout(editMode: true))
    {
        dzienRcp.Praca.OdGodziny = new Time(8, 0);       // korekta na subrowie Praca
        dzienRcp.Praca.DoGodziny = new Time(16, 0);
        dzienRcp.StanRCP = StanWeryfikacjiRCP.Poprawny;  // zatwierdzenie weryfikacji
        dzienRcp.Uwagi = (MemoText)"Skorygowano wyjście";
        t.Commit();
    }
    session.Save();
}
```

**Pułapki:**
- `DniRCP` jest **typowane** (`DateSubTable<DzienRCP>`) — indeksator `[Date]` zwraca `DzienRCP`/`null`;
  do zakresu użyj `[FromTo]`. Nie iteruj kolekcji w poszukiwaniu dnia.
- Godziny koryguj na **subrowie `Praca`** (`Praca.OdGodziny`/`DoGodziny`); `DzienRCP.Czas`/`OdGodziny`
  na rootcie są kalkulowane (read-only).
- `StanRCP` to enum `StanWeryfikacjiRCP` — nie string. Zmiana stanu może podlegać weryfikatorom.
- W Demo `DzienRCP` istnieje tylko gdy był import/przeliczenie — test korekty zakłada istniejący dzień
  (sprawdzaj `is not null`), nie twórz `DzienRCP` ręcznie.

---

### F5 — Rozliczenie pracy hybrydowej / aktualizacja podzielników na podstawie pracy hybrydowej

**Cel:** rozliczyć czas pracy hybrydowej (podział na strefy: stacjonarna / praca zdalna / zdalna
okazjonalna) i zaktualizować podzielniki (elementy rozliczenia czasu pracy / strefy dnia), na
podstawie których naliczane są składniki płacowe i koszty.

**Model danych (publiczny kontrakt):**

| Element | Lokalizacja | Typ | Uwaga |
|---|---|---|---|
| Strefy pracy dnia | `DzienPracy.Strefy` | `SubTable<Soneta.Kalend.StrefaPracy>` | podział dnia na strefy |
| Strefa pracy | `Soneta.Kalend.StrefaPracy` (ctor `(DzienPracy dzien)`) | — | `Definicja : DefinicjaStrefy`, `CzasRozliczany : Time`, `OdGodziny`/`Czas` (kalk.) |
| Definicja strefy | `Soneta.Kalend.DefinicjaStrefy` | konfiguracja | `Typ : TypStrefy`, `Wchodzi`, `Rozliczana`; stałe `Praca_Zdalna`, `PracaZdalnaOkazjonalna : Guid` |
| Dokumenty rozliczenia | `pracownik.RozliczeniaCzasuPracy` | `SubTable<Soneta.Kalend.RozliczenieCzasuPracy>` | dokumenty rozliczenia czasu (podzielniki) |
| Elementy rozliczenia | `pracownik.ElementyRozliczeniaCzasuPracy` | `SubTable<Soneta.Kalend.ElementRozliczeniaCzasuPracy>` | pozycje podzielnika |
| Dokument rozliczenia | `Soneta.Kalend.RozliczenieCzasuPracy` (ctor `(Pracownik, DefinicjaRozliczeniaCzasuPracy)`) | root | `Data`, `Seria`, `Stan : StanyRozliczeniaCzasuPracy` |
| Pozycja rozliczenia | `Soneta.Kalend.ElementRozliczeniaCzasuPracy` (ctor `(RozliczenieCzasuPracy dokument)`) | child | `Definicja : DefinicjaStrefy`, `Data`, `OdGodziny`, `Czas`, `CzasPozostały`/`CzasDostępny`/`Zrealizowane` (kalk.) |

**`Soneta.Kalend.TypStrefy` (enum):** `NieWplywa`, `Zwieksza`, `Zmniejsza`.

**Workery (UI/extendery — praca zdalna/hybrydowa):**
- `Soneta.Kalend.StrefaPracy.PracaZdalnaWorker` (`Strefa : StrefaPracy`) — oznaczenie strefy jako
  praca zdalna.
- `Soneta.Kadry.PracaZdalna.DzienStrefaExtWorker`, `ElementRozliczeniaCzasuPracyExtWorker`,
  `DzienZestawienieExtender` — extendery zestawień/dni dla pracy zdalnej.

**Snippet (odczyt rozkładu na strefy + dokument rozliczenia):**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];

// Odczyt podziału dnia na strefy (stacjonarna / zdalna):
var dzien = pracownik.DniPracy[new Date(2026, 6, 1)];
if (dzien is not null)
{
    foreach (StrefaPracy s in dzien.Strefy)
    {
        DefinicjaStrefy def = s.Definicja;   // strefa (np. praca zdalna)
        Time rozliczany = s.CzasRozliczany;  // czas rozliczany w strefie
    }
}

// Pozycje podzielnika (elementy rozliczenia czasu pracy):
foreach (ElementRozliczeniaCzasuPracy el in pracownik.ElementyRozliczeniaCzasuPracy)
{
    DefinicjaStrefy def = el.Definicja;
    Time czas = el.Czas;
}
```

**Pułapki / wykonalność:**
- Rozkład pracy hybrydowej to **strefy** (`DzienPracy.Strefy` / `DefinicjaStrefy` z flagą zdalna) +
  dokument `RozliczenieCzasuPracy` z pozycjami `ElementRozliczeniaCzasuPracy` (podzielniki).
- `RozliczenieCzasuPracy` to **root** (ctor `(Pracownik, DefinicjaRozliczeniaCzasuPracy)`) — utworzenie
  wymaga istniejącej `DefinicjaRozliczeniaCzasuPracy` z konfiguracji; pozycje ctorem
  `new ElementRozliczeniaCzasuPracy(dokument)`. Czysty odczyt jest bezpieczny i bez transakcji.
- Aktualizacja podzielników na podstawie pracy hybrydowej przebiega **głównie przez extendery/UI**
  (`DzienStrefaExtWorker`, `ElementRozliczeniaCzasuPracyExtWorker`, `PracaZdalnaWorker`) zależne od
  `Context`/wniosków e-pracownika — brak prostego, czystego API operacyjnego.
- Wymaga skonfigurowanych `DefinicjaStrefy` (Praca_Zdalna / PracaZdalnaOkazjonalna) — w Demo strefy
  mogą nie być włączone, co czyni budowę rozliczenia kruchą do testu (raczej odczyt niż zapis).

## G. Umowy cywilnoprawne

### G1 — Dodawanie umów cywilnoprawnych (zlecenie, o dzieło) (★)

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
  zapis dotyczy dopiero *zmiany/aneksu* „od daty" (G2).
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

### G2 — Zmiana/aneks umowy (★)

**Cel:** zmienić warunki istniejącej umowy. Trzy rozłączne przypadki: **(a) korekta** danych
nagłówkowych w bieżącym okresie; **(b) aneks „od daty"** — nowy zapis historyczny `UmowaHistoria`
obowiązujący od wskazanego dnia (analogicznie do `PracHistoria`, sekcja A14); **(c) seryjna
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

### G3 — Operacja seryjna „Dodaj umowy" dla grupy osób (★)

**Cel:** dodać jednakową umowę cywilnoprawną (zlecenie / o dzieło / ryczałtowa) **naraz dla wielu
zaznaczonych pracowników** — operacja seryjna z listy osób. W UI: menu *Operacje seryjne → Dodaj
umowy…*. Każdej osobie z zaznaczenia tworzona jest osobna `Umowa` z tymi samymi danymi nagłówkowymi
(definicja elementu, okres, wartość, sposób rozliczenia), analogicznie do G1.

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

| Pole | Typ | Odpowiednik na `Umowa` (G1) |
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

**Wariant B — pętla po pracownikach (jawne tworzenie, jak G1):** gdy nie chcesz przechodzić przez
worker — dla każdej osoby twórz `new Umowa(p)` i ustaw te same pola co w G1. To jawnie pokazuje, że
operacja seryjna = G1 powtórzone w pętli.

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
  go z workerami na `Umowa` (G2: `AktualizacjaStawkiWorker`, `KopiujUmowe2Worker`).
- W wariancie B obowiązują wszystkie pułapki G1: kwota na `umowa.Last.Wartosc` (root `Brutto`/`Wartosc`
  są wyliczane), `Element` tylko o `RodzajZrodla == RodzajŹródłaWypłaty.Umowa`, `Wydzial` wymagany,
  dodanie umowy pracownikowi **w archiwum** rzuca `WArchiwumException`.
- Pętlę edycyjną trzymaj krótką (safe-code §13.1); konflikty/duplikaty wykrywane w `Save()` (§4).
- Wartość zawsze jako `Soneta.Types.Currency`, daty jako `Date`/`FromTo`, nie `decimal`/`DateTime`
  (safe-code §10).

---

### G4 — Rachunek do umowy zlecenia (★)

**Cel:** wystawić **rachunek do umowy zlecenia** — czyli rozliczyć (naliczyć i wypłacić) umowę
cywilnoprawną. W modelu Soneta „rachunek do umowy zlecenia" **nie jest osobnym rekordem** na `Umowa`
ani w `pracownik.Rachunki` — to **wypłata z umowy** typu `Soneta.Place.WyplataUmowa`, naliczana
mechanizmem płac (jak H2). `pracownik.Rachunki: SubTable<Soneta.Kasa.RachunekBankowyPodmiotu>` to
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

**Tworzenie rachunku (wypłaty) do umowy — wykonawca naliczania `NaliczanieSeryjne.Umowy` (jak H2):**

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

// Umowa zlecenie pracownika (np. utworzona w G1):
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
- `Wyplata` nie ma agregatów `Brutto`/`Netto` — sumuj składniki z `Wyplata.Elementy` (jak w H2/H4).
- Kwoty jako `Soneta.Types.Currency`, daty jako `Date` (safe-code §10).

---

### G5 — Zgłoszenia ZUS zleceniobiorców (ZUA / ZZA na podstawie umowy) (★)

**Cel:** przygotować zgłoszenie zleceniobiorcy do ZUS — **ZUA** (zgłoszenie do ubezpieczeń
społecznych + zdrowotnego) albo **ZZA** (tylko zdrowotne) — **na podstawie schematu ubezpieczeń
umowy**, oraz wyrejestrowanie (**ZWUA**) po jej zakończeniu. O tym, czy powstaje ZUA czy ZZA, decyduje
**schemat ubezpieczeń zapisu umowy**, a nie odrębne pole „rodzaj zgłoszenia".

> **Schemat ubezpieczeń umowy — gdzie siedzi.** Ubezpieczenia umowy są **historyczne** i leżą na
> zapisie `UmowaHistoria.Ubezpieczenia: Soneta.Kadry.Ubezpieczenia` (analogicznie do
> `PracHistoria.Etat.Ubezpieczenia` z A7; ta sama struktura `Spoleczne`/`Zdrowotne`). Z roota dostępne
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
  (`umowa.Historia.Update(date)` + `UmowaHistorie.AddRow`, jak G2/A14), nie nadpisywanie bieżącego.
- `Spoleczne.Od` jest **tylko do odczytu** (wyliczane) — datę objęcia społecznymi obowiązkowymi
  ustawiasz zbiorczo przez `Ubezpieczenia.ObowiazkoweOd`; na `Zdrowotne` `ObowiazkoweOd` jest
  zapisywalne wprost (asymetria — jak w A7).
- `Tyub4` to rekord **konfiguracyjnego** słownika `TytulyUbezpiecz4`, klucz `WgKodu[int]` — pobierz
  istniejący tytuł zleceniobiorcy, nie twórz „w locie".
- `ZarejestrujUmowyWorker` jest na `Umowa` (umowy), a `ZarejestrujPracownikówWorker` na `Pracownik`
  (etatowi) — do zleceniobiorców używaj wersji „Umowy".
- Workery deklaracji uruchamiaj jak każdy worker enova (Context z tej samej sesji); po akcji wołasz
  `Session.Save()`. Obsłuż `RowConflictException` z `Save()` (safe-code §4).
- `ZarejestrujRodzinę`/`WyrejestrujRodzinę` sterują dołączeniem ZCNA dla członków rodziny
  (`pracownik.Rodzina`, A9) — dla zleceniobiorcy zgłoszenie rodziny działa analogicznie do etatu.

## H. Płace — naliczanie wypłat

> **Model danych.** `Wyplata` (`Soneta.Place.Wyplata`) jest klasą **abstrakcyjną**, root `GuidedRow`,
> tabela `Wyplaty`. Konkretne typy: `WyplataEtat` (etat), `WyplataUmowa` (umowy), `WyplataInne`
> (pozostałe). Każda wypłata należy do jednej **listy płac** (`ListaPlac`, tabela `ListyPlac`) i do
> jednego pracownika. Składniki wynagrodzenia to **elementy** (`WypElement`, tabela `WypElementy`,
> root guided) w kolekcji `Wyplata.Elementy: SubTable<WypElement>`.
>
> **Naliczanie** realizuje publiczny worker `Soneta.Place.NaliczanieSeryjne` (klasa abstrakcyjna
> `partial`) z zagnieżdżonymi klasami:
> - parametry: `NaliczanieSeryjne.Params` (bazowa), `NaliczanieSeryjne.PracownikParams : Params`
>   (etat + pozostałe), `NaliczanieSeryjne.UmowaParams : Params` (umowy);
> - wykonawcy: `NaliczanieSeryjne.Pracownika : NaliczanieSeryjne` (wypłaty pracownika),
>   `NaliczanieSeryjne.Umowy : NaliczanieSeryjne` (wypłaty z umów).
>
> Wynik to obiekt `Soneta.Place.NaliczanieWypłat` z kolekcją `WszystkieWypłaty: IList` (elementy są
> typu `Wyplata`). **Naliczanie samo zatwierdza zmiany w sesji** (`Nalicz()` wewnętrznie otwiera i
> commituje transakcję edycyjną na sesji pracownika) — utrwalenie w bazie wymaga osobnego
> `session.Save()`.

### H1 — Naliczanie wypłat etatowych (★)

**Cel:** naliczyć wypłatę etatową (wynagrodzenie zasadnicze etatu + dodatki/potrącenia) dla jednego
pracownika za wskazany okres rozliczeniowy.

**Klasy, pola i typy:**

| Element | Typ / sygnatura | Uwaga |
|---|---|---|
| Parametry | `new NaliczanieSeryjne.PracownikParams(Context)` | ctor przyjmuje `Context` (sesja operacyjna) |
| Data wypłaty | `PracownikParams.DataWypłaty: Date` | ustawienie **automatycznie** wylicza `Okres` (z konfiguracji listy) i `MiesiącDeklaracji` |
| Data listy | `PracownikParams.DataListy: Date` | data dokumentu listy płac |
| Okres naliczania | `PracownikParams.Okres: FromTo` | zwykle wyliczony z `DataWypłaty`; można nadpisać |
| Typ naliczenia | `PracownikParams.Naliczanie: TypNaliczenia` | `PłatnaZGóry`/`PłatnaZDołu`; **domyślnie `PłatnaZDołu`** — patrz Pułapki (licencja) |
| Filtr typu wypłaty | `PracownikParams.TypWypłaty: TypWyplaty` | `Wszystkie`/`Etat`/`Umowa`/`Inne` — dla etatu `Etat` lub `Wszystkie` |
| Wykonawca | `new NaliczanieSeryjne.Pracownika(PracownikParams)` | |
| Pracownik | `NaliczanieSeryjne.Pracownika.Pracownik: Pracownik` | komu naliczamy (z tej samej sesji co `Context`) |
| Uruchomienie | `NaliczanieSeryjne.Pracownika.Nalicz(): NaliczanieWypłat` | nalicza i zatwierdza w sesji |
| Wynik | `NaliczanieWypłat.WszystkieWypłaty: IList` (elementy `Wyplata`) | naliczone wypłaty |
| Błędy naliczania | `NaliczanieWypłat.Nienaliczeni: IEnumerable<BłądNaliczaniaWynagrodzenia>` | pracownicy, dla których się nie udało |

`TypNaliczenia` (`Soneta.Place`): `PłatnaZGóry = 1`, `PłatnaZDołu = 2`.
`TypWyplaty` (`Soneta.Place`): `Wszystkie = 0`, `Etat = 1`, `Umowa = 2`, `Inne = 3`.

**Snippet:**

```csharp
var place = session.GetPlace();
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];

// Parametry naliczania — Context z tej samej sesji co pracownik:
var pars = new NaliczanieSeryjne.PracownikParams(context);
pars.DataWypłaty = new Date(2024, 6, 28);    // ustawia Okres i MiesiącDeklaracji automatycznie
pars.DataListy   = pars.DataWypłaty;
// pars.Naliczanie pozostaje domyślnie PłatnaZDołu (nie ustawiamy — patrz Pułapki)
pars.TypWypłaty  = TypWyplaty.Etat;          // tylko wypłaty etatowe

var naliczanie = new NaliczanieSeryjne.Pracownika(pars) { Pracownik = pracownik };
NaliczanieWypłat wynik = naliczanie.Nalicz();   // nalicza + commit w sesji

foreach (Wyplata w in wynik.WszystkieWypłaty)
{
    // w.Pracownik, w.ListaPlac, w.Data, w.MiesiacDeklaracji, w.Wartosc (Currency, do wypłaty)
}

session.Save();   // utrwalenie w bazie (opcjonalne — bez tego zmiany żyją tylko w sesji)
```

**Pułapki:**
- **`Context` musi pochodzić z tej samej sesji co pracownik.** `PracownikParams(Context)` wiąże się z
  `Context.Session`; pracownik pobrany z innej sesji spowoduje niespójność.
- **Nie ustawiaj `Naliczanie` jawnie, jeśli nie masz pewności co do licencji.** Setter
  `Params.Naliczanie` rzuca wyjątek, gdy licencja nie jest „PL Złoty/Platynowy" — getter wtedy i tak
  zwraca `PłatnaZDołu`. Pozostawienie wartości domyślnej (`PłatnaZDołu`) jest bezpieczne.
- `Nalicz()` **otwiera własną transakcję** na sesji pracownika i commituje ją — **nie owijaj** wywołania
  w dodatkowy `session.Logout(true)`. Po naliczeniu zmiany są w sesji; do bazy idą dopiero w `Save()`.
- `WszystkieWypłaty` to `IList` nietypowana — iteruj jako `foreach (Wyplata w in ...)`.
- Pracownik w archiwum (`Pracownik.ArchiwumInfo == InformacjeOArchiwum.WArchiwum`) jest pomijany —
  `WszystkieWypłaty` będzie puste, bez wyjątku.
- Naliczanie to operacja na danych operacyjnych — sprawdź `wynik.Nienaliczeni` zamiast łapać ogólny
  wyjątek; przy `KontynacjaNaliczenia` (tryb seryjny) błędy lądują tam, a nie w `throw`.

### H2 — Naliczanie wypłat z umów (★)

**Cel:** naliczyć wypłatę z konkretnej umowy cywilnoprawnej (`Soneta.Kadry.Umowa`).

**Klasy, pola i typy:**

| Element | Typ / sygnatura | Uwaga |
|---|---|---|
| Parametry | `new NaliczanieSeryjne.UmowaParams(Context)` | jak `PracownikParams`, ale `Naliczanie` jest na sztywno `PłatnaZDołu` (setter rzuca `NotSupportedException`) |
| Data wypłaty / listy / okres | `UmowaParams.DataWypłaty`, `.DataListy`, `.Okres` | jak w H1 |
| Wykonawca | `new NaliczanieSeryjne.Umowy(UmowaParams)` | w ctorze ustawia `TypWypłaty = Umowa` |
| Umowa | `NaliczanieSeryjne.Umowy.Umowa: Umowa` | ustawienie umowy ustawia też `Pracownik` z `umowa.Pracownik` |
| Uruchomienie | `NaliczanieSeryjne.Umowy.Nalicz(): NaliczanieWypłat` | |

**Snippet:**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
Umowa umowa = pracownik.Umowy.Cast<Umowa>().First();   // przykładowa umowa pracownika

var pars = new NaliczanieSeryjne.UmowaParams(context);
pars.DataWypłaty = new Date(2024, 6, 28);
pars.DataListy   = pars.DataWypłaty;

var naliczanie = new NaliczanieSeryjne.Umowy(pars) { Umowa = umowa };
NaliczanieWypłat wynik = naliczanie.Nalicz();

foreach (Wyplata w in wynik.WszystkieWypłaty)
{
    // w.Typ == TypWyplaty.Umowa; w.Wartosc; w.Elementy
}
session.Save();
```

**Pułapki:**
- **Nie ustawiaj `UmowaParams.Naliczanie`** — setter rzuca `NotSupportedException` (umowy zawsze
  „płatne z dołu").
- Ustawienie `Umowy.Umowa` nadpisuje `Pracownik` na właściciela umowy — nie ustawiaj `Pracownik` ręcznie.
- Pozostałe pułapki jak w H1 (Context z tej samej sesji, własna transakcja w `Nalicz()`, `Save()`).

### H3 — Naliczanie pozostałych wypłat (★)

**Cel:** naliczyć wypłaty „pozostałe" — pojedynczy dodatek/potrącenie (np. premia, zasiłek
jednorazowy) poza zasadniczym wynagrodzeniem etatu, bądź wypłaty typu `Inne`.

**Mechanizm:** używamy tego samego wykonawcy co H1 — `NaliczanieSeryjne.Pracownika` — sterując
zakresem przez `PracownikParams`:
- `PracownikParams.TypWypłaty = TypWyplaty.Inne` — naliczanie tylko składników typu „inne",
- `PracownikParams.Dodatek: DefinicjaElementu` — **zawężenie do jednej definicji** dodatku/potrącenia
  (naliczany jest tylko wskazany składnik).

**Pola i typy:**

| Element | Typ / sygnatura | Uwaga |
|---|---|---|
| Filtr typu | `PracownikParams.TypWypłaty: TypWyplaty` | `Inne` — pozostałe; `Wszystkie` — łącznie z etatem |
| Pojedynczy składnik | `PracownikParams.Dodatek: DefinicjaElementu` | definicja konkretnego dodatku/potrącenia; `null` = bez zawężenia |

**Snippet:**

```csharp
var place = session.GetPlace();
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];

// Definicja konkretnego dodatku/potrącenia (rekord konfiguracyjny):
DefinicjaElementu defDodatku = place.DefElementow.WgKodu["PREMIA"];   // przykładowy kod

var pars = new NaliczanieSeryjne.PracownikParams(context);
pars.DataWypłaty = new Date(2024, 6, 28);
pars.DataListy   = pars.DataWypłaty;
pars.TypWypłaty  = TypWyplaty.Inne;     // pozostałe wypłaty
pars.Dodatek     = defDodatku;          // tylko ten składnik

var naliczanie = new NaliczanieSeryjne.Pracownika(pars) { Pracownik = pracownik };
NaliczanieWypłat wynik = naliczanie.Nalicz();

foreach (Wyplata w in wynik.WszystkieWypłaty)
{
    foreach (WypElement e in w.Elementy)
    {
        // e.Definicja, e.Nazwa, e.Wartosc (decimal), e.Okres
    }
}
session.Save();
```

**Pułapki:**
- `Dodatek` to rekord **konfiguracyjny** `DefinicjaElementu` — pobierz istniejącą definicję
  (np. przez klucz kodu w `place.DefElementow`), nie twórz „w locie".
- `TypWyplaty.Inne` i `TypWyplaty.Etat` są rozłączne — by naliczyć etat + dodatki łącznie użyj
  `Wszystkie`.
- Pozostałe pułapki jak w H1.

### H4 — Przeglądanie/odczyt wypłat za rok (★)

**Cel:** odczytać naliczone wypłaty pracownika za dany rok i zagregować wartości (suma do wypłaty,
brutto/netto/składki/podatek, sumy składników).

**Dostęp do wypłat (publiczny kontrakt):**

| Punkt wejścia | Typ | Uwaga |
|---|---|---|
| `pracownik.Wyplaty` | `SubTable<Wyplata>` | wszystkie wypłaty pracownika (klucz `WgPracownik`) |
| `session.GetPlace().Wyplaty.WgPracownik[pracownik]` | `SubTable<Wyplata>` | równoważnie z modułu |
| `session.GetPlace().Wyplaty.WgData[date]` | `SubTable<Wyplata>` | wypłaty z datą `date` |
| `listaPlac.Wyplaty` | `SubTable<Wyplata>` | wypłaty danej listy płac |

**Pola wypłaty (`Wyplata`) do odczytu:**

| Pole | Typ | Opis |
|---|---|---|
| `Pracownik` | `Pracownik` | właściciel |
| `ListaPlac` | `ListaPlac` | lista płac (`ListaPlac.Okres: FromTo`, `ListaPlac.DataWyplaty: Date`, `ListaPlac.Zatwierdzona: bool`) |
| `Data` | `Date` | data wypłaty (klucz `WgData`) |
| `MiesiacDeklaracji` | `YearMonth` | miesiąc rozliczenia PIT |
| `MiesiacZUS` | `YearMonth` | miesiąc rozliczenia ZUS |
| `Wartosc` | `Currency` | kwota **do wypłaty** (netto) w PLN |
| `Numer` | `NumerDokumentu` | numer dokumentu (`Numer.NumerPelny`) |
| `Typ` | `TypWyplaty` | etat / umowa / inne |
| `Bufor` | `bool` | wypłata w buforze (niezatwierdzona) |
| `Elementy` | `SubTable<WypElement>` | składniki wynagrodzenia |

**Kwoty na poziomie wypłaty (`Soneta.Place.Wyplata`, typ `Soneta.Types.Currency`):** `Wartosc`
(kwota **do wypłaty**, PLN), `WartoscCy` (w walucie listy), `DoWypłaty`, `Gotówka`, `Inne`.
Aby otrzymać `decimal`, użyj **`.Value`** (`w.Wartosc.Value`) — `Currency` nie ma jawnego rzutowania
na `decimal`.

> **Uwaga:** `Wyplata`/`WyplataEtat` **nie udostępnia** publicznych agregatów typu `Brutto`, `Netto`,
> `SkładkiZUS`, `Podatek` jako gotowych właściwości. Brutto/netto/składki/podatek **liczymy sumując
> składniki** z kolekcji `Wyplata.Elementy` (`WypElement.Wartosc`, `WypElement.Netto`, `WypElement.Podatki.*`).

**Składniki (`WypElement`) i ich struktura podatkowo-składkowa:**

| Pole | Typ | Opis |
|---|---|---|
| `Definicja` | `DefinicjaElementu` | definicja składnika |
| `Nazwa` | `string` | nazwa składnika |
| `Wartosc` | `decimal` | wartość składnika |
| `Okres` | `FromTo` | okres, za który naliczono |
| `Podatki` | `Podatki` (subrow) | struktura podatków/składek |
| `Podatki.PodstawaZUS` | `decimal` | podstawa ZUS |
| `Podatki.Emerytalna` / `Rentowa` / `Chorobowa` / `Wypadkowa` / `Zdrowotna` | `SkladkaZUS` (subrow) | każda z polami `Podstawa`, `Prac`, `Firma: decimal` |
| `Podatki.Koszty`, `Podatki.Ulga`, `Podatki.ZalFIS` | `decimal` | koszty, ulga, zaliczka PIT |

**Snippet:**

```csharp
var place = session.GetPlace();
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];

int rok = 2024;
var od = new Date(rok, 1, 1);
var doD = new Date(rok, 12, 31);

// Filtr serwerowy po dacie wypłaty (zakres roku) — bez pełnego skanu:
decimal sumaDoWypłaty = 0m;
decimal sumaBrutto    = 0m;

foreach (Wyplata w in pracownik.Wyplaty[(Wyplata x) => x.Data >= od && x.Data <= doD])
{
    sumaDoWypłaty += w.Wartosc.Value;       // kwota do wypłaty (Currency -> decimal przez .Value)

    // brutto/składki/podatek liczymy z elementów (nie ma gotowych agregatów na wypłacie):
    foreach (WypElement e in w.Elementy)
    {
        sumaBrutto += e.Wartosc;            // WypElement.Wartosc to decimal
        decimal netto       = e.Netto;
        decimal podstawaZUS = e.Podatki.PodstawaZUS;
        decimal zaliczkaPit = e.Podatki.ZalFIS;
    }
}
```

**Pułapki:**
- `Wyplaty` to tabela **operacyjna guided** — zawsze ograniczaj zakresem czasowym (rok), nie iteruj
  całości (`safe-code §6.3`). Filtruj serwerowo przez `SubTable[condition]` po `Data`, nie w pamięci.
- `Wartosc` to `Currency` (kwota do wypłaty); konwersja na `decimal` przez `.Value`. Składnik
  `WypElement.Wartosc`/`WypElement.Netto` to już `decimal` — nie myl typów ani znaczeń.
- **Nie ma** gotowych właściwości agregujących (`Brutto`/`Netto`/`SkładkiZUS`/`Podatek`) na `Wyplata`
  ani `WyplataEtat` — sumuj składniki z `Wyplata.Elementy` (i ich `Podatki.*`).
- `SkladkaZUS` ma pola `Podstawa`, `Prac`, `Firma` (część pracownika i pracodawcy) oraz właściwość
  pomocniczą `Składka` (suma) — wybierz właściwą do potrzeb.
- Filtruj po `Data` (data wypłaty) lub `MiesiacDeklaracji`/`MiesiacZUS` zależnie od potrzeby
  raportowej — to różne pojęcia roku (rok wypłaty vs rok deklaracji).

### H5 — Odczyt elementów wypłaty (brutto/składki/podatek/netto) (★)

**Cel:** odczytać składniki konkretnej **naliczonej** wypłaty (`Soneta.Place.Wyplata`) i wyliczyć
agregaty: brutto, składki ZUS (część pracownika i firmy), zaliczka PIT, netto.

**Model.** Składniki to `Wyplata.Elementy: SubTable<WypElement>` (`Soneta.Place.WypElement`, tabela
operacyjna guided `WypElementy`). `Wyplata` **nie** ma gotowych agregatów `Brutto`/`Netto`/`SkładkiZUS`/
`Podatek` — liczymy je z elementów albo przez worker `Wyplata.PITInfoWorker` (patrz niżej).

**Pola składnika `WypElement` (do odczytu):**

| Pole | Typ | Opis |
|---|---|---|
| `Definicja` | `DefinicjaElementu` | definicja składnika (konfiguracja) |
| `Nazwa` | `string` | nazwa składnika |
| `Wartosc` | `decimal` | wartość brutto składnika (kwota elementu) |
| `Netto` | `decimal` | wartość netto składnika |
| `DoWypłaty` | `decimal` | kwota do wypłaty z tego składnika |
| `Okres` | `FromTo` | okres, za który naliczono |
| `MiesiacDeklaracji` | `YearMonth` | miesiąc rozliczenia PIT |
| `MiesiacZUS` | `YearMonth` | miesiąc rozliczenia ZUS |
| `Podatki` | `Podatki` (subrow) | struktura podatkowo-składkowa |

**Subrow `WypElement.Podatki` (`Soneta.Place.Podatki`) — pola istotne:**

| Pole | Typ | Opis |
|---|---|---|
| `PodstawaZUS` | `decimal` | podstawa wymiaru składek ZUS |
| `Emerytalna` / `Rentowa` / `Chorobowa` / `Wypadkowa` / `Zdrowotna` | `SkladkaZUS` (subrow) | każda z polami `Podstawa`, `Prac`, `Firma: decimal` oraz wyliczanym `Składka` (suma) |
| `Koszty` | `decimal` | koszty uzyskania przychodu |
| `Ulga` | `decimal` | ulga podatkowa (kwota wolna) |
| `ZalFIS` | `decimal` | zaliczka na podatek dochodowy (fiskus) |
| `ZdrowotneDoOdliczenia` | `decimal` | składka zdrowotna do odliczenia |

Subrow `SkladkaZUS` (`Soneta.Place.SkladkaZUS`): `Podstawa` (podstawa), `Prac` (część pracownika,
`decimal`), `Firma` (część pracodawcy, `decimal`), wyliczane `Składka` (suma) i `JestMinus` (`bool`).

**Worker-agregator `Wyplata.PITInfoWorker`** (klasa publiczna, `[Context] Wypłata`) — udostępnia gotowe
sumy podatkowe dla wypłaty:

| Właściwość | Typ | Opis |
|---|---|---|
| `DoOpodatkowania` | `Currency` | suma elementów opodatkowanych (brutto opodatkowane) |
| `Nieopodatkowane` | `Currency` | suma elementów nieopodatkowanych |
| `Razem` | `decimal` | opodatkowane + nieopodatkowane (przychód razem) |
| `NettoRazem` | `decimal` | wynagrodzenie netto razem |
| `NettoOpodat` | `Currency` | netto opodatkowane |
| `SkładkiZUS` | `decimal` | suma składek ZUS pracownika |
| `SkładkaZdrow` | `decimal` | składka zdrowotna |
| `Koszty` | `decimal` | koszty uzyskania razem |
| `Ulga` | `decimal` | ulga podatkowa |
| `ZalFIS` | `decimal` | zaliczka PIT |
| `Dochód_Bez26` / `Dochód_26` | `decimal` | dochód (z podziałem na ulgę „do 26 lat") |

> `PITInfoWorker.Brutto` i `PITInfoWorker.Netto` są oznaczone `[Obsolete]` — używaj `DoOpodatkowania`,
> `Nieopodatkowane`, `Razem`, `NettoRazem`. Worker przyjmuje też kolekcję `Elementy: IEnumerable`
> (zamiast `Wypłata`) i `WykluczoneElementy: DefinicjaElementu[]`.

**Snippet (agregacja ręczna z elementów):**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];

decimal brutto = 0m, netto = 0m, zusPrac = 0m, zusFirma = 0m, zalPit = 0m;

// jedna konkretna wypłata pracownika (np. ostatnia z czerwca):
var od  = new Date(2024, 6, 1);
var doD = new Date(2024, 6, 30);
Wyplata wyplata = pracownik.Wyplaty[(Wyplata x) => x.Data >= od && x.Data <= doD].Cast<Wyplata>().First();

foreach (WypElement e in wyplata.Elementy)
{
    brutto += e.Wartosc;                 // WypElement.Wartosc to decimal
    netto  += e.Netto;
    zalPit += e.Podatki.ZalFIS;

    zusPrac  += e.Podatki.Emerytalna.Prac  + e.Podatki.Rentowa.Prac
              + e.Podatki.Chorobowa.Prac    + e.Podatki.Zdrowotna.Prac;
    zusFirma += e.Podatki.Emerytalna.Firma + e.Podatki.Rentowa.Firma
              + e.Podatki.Wypadkowa.Firma;
}

decimal doWyplaty = wyplata.Wartosc.Value;   // Currency -> decimal przez .Value
```

**Snippet (przez worker — gotowe agregaty):**

```csharp
var pit = new Wyplata.PITInfoWorker { Wypłata = wyplata };
decimal brutto  = pit.Razem;          // przychód razem
decimal nettoR  = pit.NettoRazem;
decimal zus     = pit.SkładkiZUS;
decimal zdrow   = pit.SkładkaZdrow;
decimal zaliczka = pit.ZalFIS;
```

**Pułapki:**
- `WypElement.Wartosc`/`Netto`/`DoWypłaty` to `decimal`; `Wyplata.Wartosc` (do wypłaty) to `Currency` —
  konwersja przez `.Value` (§10.1).
- `SkladkaZUS.Prac` to część pracownika, `SkladkaZUS.Firma` to część pracodawcy — wybierz właściwą
  zależnie od potrzeby (koszt pracownika vs koszt pracodawcy).
- `Wyplaty`/`WypElementy` to tabele operacyjne guided — pobieraj zakresem czasowym (§6.3), nie iteruj
  całości.
- Pomijaj elementy stornowane przy sumowaniu, jeśli liczysz stan bieżący — patrz `WypElement.RozliczenieStorna`
  (H10); naliczona wypłata po korekcie zawiera zarówno element pierwotny (`Wystornowany`) jak i `Stornujący`.

---

### H6 — Wypłata zaliczki / dołączenie zaliczki (★)

**Cel:** naliczyć i wypłacić zaliczkę (wypłata środków „na poczet" przyszłego wynagrodzenia), tworząc
dokument `Soneta.Place.Zaliczka` i element realizacji zaliczki na wypłacie.

**Model.** Zaliczka to rekord operacyjny `Soneta.Place.Zaliczka` (root guided, tabela `Zaliczki`,
`session.GetPlace().Zaliczki`), implementuje `IBazaZrodlaWyplaty` i `IPowiązanieWypłaty`. Element
realizujący zaliczkę to `WypElementZaliczka.Realizacja : WypElementZaliczka : WypElement`, spłata to
`WypElementZaliczka.Spłata`. Powiązanie elementu z zaliczką: `WypElement.BazaZrodla = Zaliczka`,
`RodzajŹródłaWypłaty.Zaliczka`.

**Ścieżka publiczna — worker `Soneta.Place.WypłaćZaliczkęWorker`** (na `Soneta.Kadry.Pracownicy`):

| Element | Typ / sygnatura | Uwaga |
|---|---|---|
| Parametry | `WypłaćZaliczkęWorker.ZalParams : WypElement.Params` | ctor `(Context)`; `Rodzaj == RodzajŹródłaWypłaty.Zaliczka` |
| Definicja | `ZalParams.Definicja: DefinicjaElementu` | definicja elementu zaliczki (z `WypElement.Params`); **musi mieć** `DefinicjaElementu.RodzajZrodla == RodzajŹródłaWypłaty.Zaliczka` — inaczej worker rzuca `WypElement.RodzajDefinicjiException` (np. „Korekta zaliczki podatku" ma `Dodatek`) |
| Data | `ZalParams.Data: Date` | data wypłaty zaliczki (wymagana) |
| Kwota | `ZalParams.Kwota: Currency` | kwota zaliczki (wymagana) |
| Pracownicy | `WypłaćZaliczkęWorker.Pracownicy: Pracownik[]` | dla kogo |
| Akcja | `[Action("Wypłać zaliczkę")] object WypłataZaliczki()` | tworzy `Zaliczka`, nalicza element realizacji |

**Stan zaliczki (`Zaliczka`):** `Wartosc: Currency`, `Splacono: Currency`, `Pozostaje: Currency`
(`= Wartosc - Splacono`), `Stan: StanZaliczki` (`NieSpłacona`/`CzęściowoSpłacona`/`CałkowicieSpłacona`),
`Realizacje: SubTable` (elementy realizacji), `Spłaty: SubTable<WypElement>` (elementy spłaty).

**Mechanizm naliczenia** (realizowany przez worker): dla każdego pracownika tworzony jest
`new Zaliczka(pracownik)`, dodawany przez `Zaliczki.AddRow(zaliczka)`, a następnie niskopoziomowy
obiekt `Soneta.Place.NaliczanieWypłat` z `NaliczŹródłoWypłaty = zaliczka` wykonuje `Nalicz()`.
Dołączenie/spłata zaliczki w kolejnej wypłacie etatowej dzieje się automatycznie podczas zwykłego
naliczania (H1) — naliczanie wyszukuje niespłacone zaliczki pracownika i generuje element
`WypElementZaliczka.Spłata`.

**Snippet (uruchomienie workera zaliczki):**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
// definicję zaliczki rozpoznajemy po RodzajZrodla (nie po Kodzie/Nazwie — „Korekta zaliczki podatku"
// to RodzajZrodla.Dodatek, którego worker NIE przyjmie):
DefinicjaElementu defZaliczki = session.GetPlace().DefElementow.Cast<DefinicjaElementu>()
    .First(d => d.RodzajZrodla == RodzajŹródłaWypłaty.Zaliczka);

var pars = new WypłaćZaliczkęWorker.ZalParams(context) {
    Data  = new Date(2024, 6, 15),
    Kwota = new Currency(1000m, Currency.SystemSymbol),
};
pars.Definicja = defZaliczki;

var worker = new WypłaćZaliczkęWorker { Params = pars, Pracownicy = new[] { pracownik } };
object wynik = worker.WypłataZaliczki();   // tworzy Zaliczka + nalicza; otwiera własną transakcję
session.Save();
```

**Pułapki:**
- `ZalParams.Definicja` to **istniejąca** definicja elementu o `RodzajZrodla == RodzajŹródłaWypłaty.Zaliczka` —
  pobierz z `place.DefElementow` (filtruj po `RodzajZrodla`, nie po `Kod`/`Nazwa`), nie twórz w locie.
- Baza Demo może nie zawierać definicji o `RodzajZrodla == Zaliczka` — wtedy worker jest niewykonalny
  (w teście: `Assert.Ignore`).
- `Zaliczka.SetWartość(...)` jest `internal` — wartości nie ustawiaj ręcznie; przekaż `ZalParams.Kwota`
  do workera.
- `Zaliczka` nie kasuje się bezpośrednio, gdy ma realizacje/spłaty (`OnDeleting` rzuca `RowException`).
- Worker otwiera własną transakcję (`Session.Logout(true)` + `CommitUI`) — nie owijaj dodatkowo;
  utrwalenie w bazie przez `Save()`.

---

### H7 — Korekta podatków i składek; „Przelicz składki ZUS i podatki" (★)

**Cel:** ponownie przeliczyć (skorygować) składki ZUS i zaliczki PIT na już naliczonych elementach
wypłat pracownika za dany miesiąc deklaracji — np. po zmianie progu, tytułu ubezpieczenia, korekcie
danych kadrowych.

**Worker `Soneta.Place.NaliczaniePodatkówMiesięcznie`** (na `Pracownik`/`PracHistoria`):

| Element | Typ / sygnatura | Uwaga |
|---|---|---|
| Miesiąc | publiczny ctor `(YearMonth miesiącDeklaracji)` (atrybut `[Context(typeof(MiesiącDeklaracji),"Miesiąc")]`) | przy ręcznym wywołaniu przekaż `YearMonth` (np. `pars.Miesiąc`); property odczytu `MiesiącDeklaracji: YearMonth` (get) |
| Klasa parametru | `Soneta.Place.MiesiącDeklaracji : ContextBase` | `MiesiącDeklaracji.Miesiąc: YearMonth` (domyślnie `YearMonth.Today`) |
| Pracownik | `NaliczaniePodatkówMiesięcznie.Pracownik: Pracownik` | `[Context]` |
| `NoTrace` | `bool` | wyłączenie śladu (logu) operacji |
| Akcja | `[Action("Przelicz składki ZUS i podatki")] void PrzeliczPodatki()` | przelicza elementy z danego miesiąca |

**Mechanizm:** worker iteruje elementy (`WypElementy.WgDaty`) wszystkich pracowników powiązanych
(`Pracownik.PracownicyPowiązani`) w okresie `MiesiącDeklaracji.ToFromTo()`, dla niezablokowanych
(`!element.Podatki.Korekta && element.Wyplata.Bufor`) wykonuje przeliczenie flag i naliczenie
podatków (`NaliczaniePodatków.NaliczRozrzuć()`). Wszystko w transakcji `Session.Logout(true)` +
`Commit()`.

**Snippet:**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];

var pars = new MiesiącDeklaracji(context) { Miesiąc = new YearMonth(2024, 6) };
var worker = new NaliczaniePodatkówMiesięcznie(pars.Miesiąc) { Pracownik = pracownik };
worker.PrzeliczPodatki();    // przelicza składki ZUS i zaliczki PIT za czerwiec 2024
session.Save();
```

**Pułapki:**
- Elementy z ręczną korektą podatków (`element.Podatki.Korekta == true`) oraz elementy z wypłat
  zatwierdzonych (`!Wyplata.Bufor`) są **pomijane** — przeliczane są tylko elementy z bufora.
- `MiesiącDeklaracji.Miesiąc` to `YearMonth` — to miesiąc deklaracji, nie data wypłaty.
- Worker przelicza także pracowników powiązanych (`PracownicyPowiązani`) — operacja może objąć więcej
  niż jedną kartotekę.

---

### H8 — Rozliczenie pracownika; dochód / roczny dochód (★)

**Cel:** odczytać dochód z naliczonej wypłaty oraz (dla właścicieli) pobrać roczny dochód do rozliczeń;
opcjonalnie uruchomić rozliczenie pracownika.

**A. Dochód z wypłaty — `Wyplata.PITInfoWorker`** (publiczny, jak w H5). Dochód podatkowy:

| Właściwość | Typ | Opis |
|---|---|---|
| `Dochód_Bez26` | `decimal` | dochód poza ulgą „do 26 lat" (`= przychód + przychód50 − koszty − koszty50`) |
| `Dochód_26` | `decimal` | dochód objęty ulgą „do 26 lat" |
| `DoOpodatkowania` | `Currency` | podstawa opodatkowania (brutto opodatkowane) |
| `Podstawa` | `decimal` | podstawa naliczenia zaliczki |
| `ZalFIS` | `decimal` | zaliczka PIT |

Dochód roczny pracownika sumuje się iterując wypłaty roku (H4/H5) i sumując `Dochód_Bez26 + Dochód_26`
(lub `DoOpodatkowania`) z `PITInfoWorker` każdej wypłaty.

**B. „Pobierz roczny dochód" — worker `Soneta.Kadry.PobierzDochodRocznyWorker`** (na `Pracownik`/
`PracHistoria`) — **tylko dla właściciela** (`Pracownik is Wlasciciel`):

| Element | Typ / sygnatura | Uwaga |
|---|---|---|
| Parametry | property `PobierzDochodRocznyWorker.Pars : PobierzDochodRocznyWorker.Params` | `Pars.Rok: int` (domyślnie rok ubiegły) |
| Pracownik | `PobierzDochodRocznyWorker.Pracownik: Pracownik` | `[Context]` |
| Akcja | `[Action("Pobierz roczny dochód")] void Pobierz()` | zapisuje `PrzychodRyczalt` (RoczDochSkala/RoczDochLiniowy/RoczDochRyczalt) za rok |

Korzysta z serwisu `IDochódWłaściciela.KwotaDochoduStraty(pracownik, YearMonth, FormaOpodatkowania)`.

**C. „Rozlicz pracownika" — worker `Soneta.Place.RozliczaniePracownikowWorker`** (na `Pracownik`) —
**tylko dla folderu pracowników zewnętrznych** (`KadryIPlace/Kadry/PracownicyZewnetrzni`):

| Element | Typ / sygnatura | Uwaga |
|---|---|---|
| Parametry | `RozliczeniePracownikowParams : RozliczanieUmowZewnetrznychParams` | `Okres: FromTo`, `Data: Date` |
| Akcja | `[Action("Rozlicz pracownika")] RozliczanieUmowZewnetrznych Rozlicz()` | rozlicza umowy zewnętrzne pracownika |

**Snippet (dochód roczny z wypłat):**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
var od = new Date(2024, 1, 1); var doD = new Date(2024, 12, 31);

decimal dochodRoczny = 0m;
foreach (Wyplata w in pracownik.Wyplaty[(Wyplata x) => x.Data >= od && x.Data <= doD])
{
    var pit = new Wyplata.PITInfoWorker { Wypłata = w };
    dochodRoczny += pit.Dochód_Bez26 + pit.Dochód_26;
}
```

**Pułapki:**
- `PobierzDochodRocznyWorker` działa wyłącznie dla `Wlasciciel` i form opodatkowania ogólnych/ryczałtu —
  dla zwykłego pracownika nie ma zastosowania (zwraca bez efektu).
- „Rozlicz pracownika" (`RozliczaniePracownikowWorker`) dotyczy **pracowników zewnętrznych** (umowy
  zewnętrzne), nie standardowego rozliczenia płacowego.
- Wewnętrzny `Wyplata.RozliczenieManager` (rozliczanie płatności/należności) jest **niepubliczny** —
  rozliczenie płatności inicjuje setter `Wyplata.Bufor` (zejście z bufora), nie wywołuj go bezpośrednio.

---

### H9 — Kalkulator wynagrodzeń (brutto↔netto, koszt pracodawcy) (★)

**Cel:** wyliczyć netto z brutto (lub odwrotnie) oraz całkowity koszt pracodawcy.

**Brak dedykowanej publicznej klasy „kalkulatora wynagrodzeń"** w publicznym kontrakcie (patrz sekcja
„niewykonalne"). Wyliczenie realizujemy przez **naliczenie próbne** (H1/H3 — `NaliczanieSeryjne`) i
odczyt agregatów workera `Wyplata.PITInfoWorker` oraz `Wyplata.KosztyUzyskaniaPrzychoduWorker`.

**Koszt pracodawcy — `Wyplata.PITInfoWorker` + składki firmy z elementów:**
- brutto: `pit.Razem` / `pit.DoOpodatkowania`,
- netto: `pit.NettoRazem`,
- składki pracownika: `pit.SkładkiZUS`, `pit.SkładkaZdrow`,
- zaliczka PIT: `pit.ZalFIS`,
- składki firmy (narzuty pracodawcy): suma `WypElement.Podatki.{Emerytalna,Rentowa,Wypadkowa}.Firma`
  (plus FP/FGŚP/FEP) — patrz `WyplataSkładkiWorker` niżej.

**Agregator składek — `Soneta.Place.WyplataSkładkiWorker`** (publiczny, `[Context] Wypłata` lub
`Elementy: IEnumerable`): udostępnia `Razem: ZestawienieSkładek` z m.in.:

| Właściwość `ZestawienieSkładek` | Typ | Opis |
|---|---|---|
| `KosztyZUS` | `decimal` | składki ZUS pracownika (emer.+rent.+chor.+wyp., część `Prac`) |
| `FirmaZUS` | `decimal` | składki ZUS pracodawcy (część `Firma`) |
| `Narzuty` | `decimal` | narzuty pracodawcy (`FirmaZUS` + FP.Firma + FGSP.Firma + FEP.Firma) |
| `ZUS` | `decimal` | `KosztyZUS + FirmaZUS` |
| `Emerytalna`/`Rentowa`/… | `ISkładka` | pojedyncze składki (`Podstawa`/`Prac`/`Firma`/`Składka`) |

Koszt pracodawcy ≈ brutto (`pit.DoOpodatkowania`/`Razem`) + `skladki.Razem.Narzuty`.

**Snippet (kalkulacja przez naliczenie próbne):**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];

var pars = new NaliczanieSeryjne.PracownikParams(context);
pars.DataWypłaty = new Date(2024, 6, 28);
pars.DataListy   = pars.DataWypłaty;
pars.TypWypłaty  = TypWyplaty.Etat;

var nal = new NaliczanieSeryjne.Pracownika(pars) { Pracownik = pracownik };
NaliczanieWypłat wynik = nal.Nalicz();

Wyplata w = (Wyplata)wynik.WszystkieWypłaty[0];
var pit = new Wyplata.PITInfoWorker { Wypłata = w };
var skl = new WyplataSkładkiWorker { Wypłata = w };

decimal brutto       = pit.Razem;
decimal netto        = pit.NettoRazem;
decimal kosztPracod  = brutto + skl.Razem.Narzuty;   // brutto + narzuty pracodawcy
// (jeśli to tylko kalkulacja — nie wywołuj Save(), wynik istnieje w sesji)
```

**Pułapki:**
- Brak osobnego „kalkulatora" — wynik zawsze powstaje przez naliczenie i workery agregujące.
- Kalkulacja brutto↔netto zależy od pełnej konfiguracji pracownika (etat, ulgi, koszty, PPK) — nie ma
  bezstanowej funkcji „brutto→netto" w publicznym API.
- Jeśli naliczenie ma być tylko próbne, nie wywołuj `Save()` (zmiany zostaną w sesji i znikną z `Dispose`),
  albo wykonaj na osobnej sesji „brudnopisowej".

---

### H10 — Stornowanie elementów wypłaty; obsługa elementów stornowanych (★)

**Cel:** zastornować (wycofać/skorygować) element już zatwierdzonej wypłaty i poprawnie odczytać stan
storna.

**Model.** Storno opisuje rekord `Soneta.Place.StornoElementu` (tabela `StornaElementow`). Element
ma stan `WypElement.StanStorna: StanStornaElementu` oraz dostęp do storna `WypElement.Storno: StornoElementu`.

Enum `Soneta.Place.StanStornaElementu`: `NieDotyczy=0`, `DoStornowania=1`, `Wystornowany=2`,
`Stornujący=3`, `WycofaneStorno=10` (tylko wyliczany).
Enum `Soneta.Place.RodzajStornaElementu`: `NieDotyczy=0`, `Anulowanie=1`, `Przeliczenie=2`.

**Pola `WypElement` związane ze storno:**

| Pole | Typ | Opis |
|---|---|---|
| `StanStorna` | `StanStornaElementu` | bieżący stan storna elementu |
| `StanStornaEx` | `StanStornaElementu` | jw. + `WycofaneStorno` gdy `Wystornowany` historyczny |
| `Storno` | `StornoElementu` | powiązany rekord storna (lub `null`) |
| `RozliczenieStorna` | `bool` | `true` gdy `Wystornowany` lub `Stornujący` (element nie liczy się do bieżącego stanu) |
| `Wystornowany` | `bool` | do elementu naliczono element stornujący |
| `Stornowane` / `Stornujące` | `SubTable<StornoElementu>` | relacje storn |
| `Korekta` | `bool` | element zmodyfikowany ręcznie przez operatora |
| `UtwórzStorno()` | `WypElement` | (wirtualna) tworzy element stornujący danego typu |

**Workery oznaczania (publiczne, na `WypElement` / `Wyplata`):**
- `StornoElementu.ElementDoPrzeliczeniaWorker` (na `WypElement`):
  - `[Action("Oznacz element do przeliczenia")] ZaznaczElementDoPrzeliczenia()` — `RodzajStornaElementu.Przeliczenie`,
  - `[Action("Oznacz element do anulowania")] ZaznaczElementDoAnulowania()` — `RodzajStornaElementu.Anulowanie`,
  - `[Action("Wycofaj oznaczenie anulowania lub przeliczenia")] WycofajZaznaczenie()` — kasuje `Storno`.
- `StornoElementu.WypłataDoPrzeliczeniaWorker` (na `Wyplata`):
  - `ZaznaczElementyDoPrzeliczenia()` / `WycofajZaznaczenie()` — dla wszystkich elementów wypłaty.
- `StornoElementu.ListaPłacDoPrzeliczeniaWorker` (na `ListaPlac`, z `Params.Definicja` / `WszystkieElementy`).

**Mechanizm.** Oznaczenie tworzy `StornoElementu` i ustawia element na `DoStornowania`. Właściwe
wytworzenie elementu stornującego (`UtwórzStornujący()`, stan `Wystornowany` na pierwotnym +
`Stornujący` na nowym) następuje przy ponownym naliczeniu wypłaty (H1) lub przeliczeniu. Wymagane:
wypłata zatwierdzona (`Wyplata.Zatwierdzona`) i element w stanie `NieDotyczy`.

**Snippet (oznaczenie do anulowania + przeliczenie):**

```csharp
Wyplata w = ...; // zatwierdzona wypłata pracownika 006
WypElement element = w.Elementy.Cast<WypElement>().First(e => e.Definicja.Kod == "PREMIA");

// oznacz element do anulowania:
var worker = new StornoElementu.ElementDoPrzeliczeniaWorker { Element = element };
worker.ZaznaczElementDoAnulowania();   // otwiera własną transakcję
// element.StanStorna == StanStornaElementu.DoStornowania, element.Storno.RodzajStorna == Anulowanie

// ponowne naliczenie wypłaty (H1) wygeneruje element stornujący:
var pars = new NaliczanieSeryjne.PracownikParams(context) { DataWypłaty = w.Data, DataListy = w.Data };
new NaliczanieSeryjne.Pracownika(pars) { Pracownik = w.Pracownik }.Nalicz();
session.Save();
```

**Odczyt elementów stornowanych:**

```csharp
foreach (WypElement e in w.Elementy)
{
    if (e.RozliczenieStorna) continue;   // pomiń wystornowane i stornujące przy sumowaniu stanu bieżącego
    // ... e to element „żywy"
}
```

**Pułapki:**
- Oznaczać można tylko elementy wypłaty **zatwierdzonej** i w stanie `NieDotyczy` (`IsEnabled...` to
  egzekwuje); na buforze storno nie ma sensu.
- Storno samo w sobie tylko **oznacza** (`DoStornowania`) — wystornowanie (`Wystornowany`/`Stornujący`)
  powstaje dopiero przy ponownym naliczeniu/przeliczeniu.
- Przy sumowaniu kwot bieżących pomijaj `RozliczenieStorna == true`, inaczej policzysz element pierwotny
  i jego storno podwójnie.
- Nie można przenieść do bufora wypłaty z elementami `DoStornowania`/`Wystornowany` (rzuca `RowException`
  — patrz H11).

---

### H11 — Anulowanie/usunięcie naliczonej wypłaty (bufor, ponowne naliczenie) (★)

**Cel:** „cofnąć" naliczoną i zatwierdzoną wypłatę do edycji (bufor) lub usunąć, by naliczyć ponownie.

**Model.** Wypłata ma flagi `Wyplata.Bufor: bool` (niezatwierdzona/edytowalna) oraz `Wyplata.Zatwierdzona: bool`
(odwrotność `Bufor`). Zejście z bufora = zatwierdzenie; powrót do bufora = otwarcie do edycji.

**Workery (publiczne, na `Wyplata`):**

| Worker / akcja | Sygnatura | Efekt |
|---|---|---|
| `Wyplata.ZatwierdźWorker` | property `Lista: Wyplata`; `[Action("Zatwierdź wypłatę")] void Zatwierdź()` | `Zatwierdzona = true` (zejście z bufora) |
| `Wyplata.OtwórzWorker` | property `Wypłata: Wyplata`; `[Action("Przenieś do bufora")] void Otwórz()` | `Zatwierdzona = false` (powrót do bufora) |

Obie akcje działają w transakcji `Session.Logout(true)` + `Commit()`. **Uwaga na nazwy property:**
worker zatwierdzania przypina wypłatę przez `ZatwierdźWorker.Lista`, a otwierania — przez
`OtwórzWorker.Wypłata`. `IsEnabled...` wymaga `Wyplata.CanBufor` — ale `CanBufor` jest **`protected`**
(niedostępny z dodatku); stan czytaj przez publiczne `Wyplata.Bufor` / `Wyplata.Zatwierdzona`.

**Bezpośrednia flaga `Wyplata.Bufor`:**
- setter `Bufor` rzuca `ColReadOnlyException`, gdy `!CanBufor`;
- zejście z bufora (`Bufor=false`) wyzwala rozliczenie płatności (wewnętrzny `RozliczenieManager`);
- `IsReadOnlyBufor()` true gdy brak praw / `!CanBufor` / wyłączone „ZatwierdzanieFlagą" / lista zatwierdzona.

**Usunięcie / ponowne naliczenie.** Aby przeliczyć od nowa: przenieś wypłatę do bufora
(`OtwórzWorker.Otwórz()`), a następnie wykonaj ponowne naliczenie (H1 — `NaliczanieSeryjne`), które
nadpisze elementy. Usunięcie samej wypłaty realizuje standardowe `Row.Delete()` w transakcji (gdy
dozwolone — wypłata w buforze, bez powiązań blokujących).

**Snippet (powrót do bufora + ponowne naliczenie):**

```csharp
Wyplata w = ...; // zatwierdzona wypłata pracownika 006

// 1) przenieś do bufora:
new Wyplata.OtwórzWorker { Wypłata = w }.Otwórz();   // Zatwierdzona = false

// 2) ponowne naliczenie (H1):
var pars = new NaliczanieSeryjne.PracownikParams(context) {
    DataWypłaty = w.Data, DataListy = w.Data, TypWypłaty = TypWyplaty.Etat,
};
new NaliczanieSeryjne.Pracownika(pars) { Pracownik = w.Pracownik }.Nalicz();
session.Save();
```

**Snippet (usunięcie wypłaty z bufora):**

```csharp
using (ITransaction t = session.Logout(true)) {
    w.Bufor = true;     // upewnij się, że w buforze (lub OtwórzWorker)
    w.Delete();
    t.Commit();
}
session.Save();
```

**Pułapki:**
- `Otwórz()` rzuca `RowException`, gdy wypłata nie jest zatwierdzona; `Zatwierdź()` — gdy już
  zatwierdzona. Sprawdzaj `IsEnabled...` / stan przed wywołaniem.
- `UpdateBufor()` rzuca `RowException`, gdy na wypłacie są elementy `DoStornowania`/`Wystornowany` —
  najpierw wycofaj oznaczenia storna (H10) lub dokończ przeliczenie.
- Zejście z bufora wykonuje rozliczenie płatności i kopiowanie kursu — nie traktuj go jak zwykłej
  zmiany pola.
- Operacje płacowe to dane operacyjne — łap `RowException`/`RowConflictException` z `Save()` (§4, §9),
  nie ogólny `Exception`.

## I. Listy płac, przelewy, wydruki

> **Model.** Lista płac to dokument operacyjny `Soneta.Place.ListaPlac` (root `GuidedRow`,
> tabela `ListyPlac`, `session.GetPlace().ListyPlac`). Trzyma kolekcję wypłat
> `ListaPlac.Wyplaty: SubTable<Wyplata>`. Każda `Wyplata` (root `GuidedRow`, tabela `Wyplaty`)
> wskazuje wstecz listę (`Wyplata.ListaPlac: IRow`) i pracownika (`Wyplata.Pracownik: IRow`).
> Wzorzec listy to `DefinicjaListyPlac` (tabela konfiguracyjna `DefListPlac`,
> `session.GetPlace().DefListPlac`, dostęp `WgSymbolu`/`WgNazwy`).
>
> **`Wyplata` jest abstrakcyjna** — konkretne typy: `WyplataEtat`, `WyplataUmowa`, `WyplataInne`
> (ctor `(ListaPlac listaplac, Pracownik pracownik)` oraz wariant z `IPowiązanieWypłaty`).
> W praktyce wypłat **nie tworzy się ręcznie** — robi to worker naliczania.

### I1 — Naliczanie/generowanie list płac (★)

**Cel:** utworzyć listę płac dla wybranego okresu i naliczyć na niej wypłaty pracowników
(etat/umowy), tak by `ListaPlac.Wyplaty` zawierała policzone `Wyplata`.

**Warianty:**

| Wariant | Mechanizm | Uwaga |
|---|---|---|
| Ręczne utworzenie pustej listy | `new ListaPlac()` + `ListyPlac.AddRow(lp)` + pola | sterujesz wszystkim sam |
| Naliczanie wypłat na istniejącej liście | worker `Soneta.Place.NaliczanieWypłat` (akcja `Nalicz`) | tworzy `Wyplata*` i liczy elementy |
| Naliczanie planowanych list (zbiorczo) | worker `Soneta.Place.NaliczaniePlanowanychListPłacWorker` (akcja `Nalicz`) | wg `DefinicjaPlanowanejListyPłac` |

**Pola i typy (`ListaPlac`, kolejność ustawiania jest istotna):**

| Pole | Typ | Uwaga |
|---|---|---|
| `Definicja` | `Soneta.Place.DefinicjaListyPlac` | wzorzec listy; ustawić **pierwsze** po `AddRow` |
| `Wydzial` | `Soneta.Kadry.Wydzial` | tylko gdy `Definicja.Wydzial == true` |
| `Seria` | `string` | tylko gdy `Definicja.Seria == true` |
| `Data` | `Soneta.Types.Date` | data naliczania listy |
| `Naliczanie` | `Soneta.Place.TypNaliczenia` | wartości: `PłatnaZGóry`/`PłatnaZDołu`; **nie ustawiaj** — setter rzuca bez licencji „PL Złoty" |
| `DataWyplaty` | `Soneta.Types.Date` | data postawienia środków; wyznacza mies./rok |
| `MiesiacZUS` | `Soneta.Types.YearMonth` | miesiąc rozliczenia ZUS |
| `Okres` | `Soneta.Types.FromTo` | okres listy; **po** `DataWyplaty` i `Naliczanie` |
| `MiesWstecz` | `int` | |
| `Wyplaty` | `SubTable<Wyplata>` | wypełniana przez worker naliczania |
| `Numer` | `Soneta.Core.NumerDokumentu` | nadawany automatycznie |
| `Bufor` / `Zatwierdzona` | `bool` | stan dokumentu |

**Worker `Soneta.Place.NaliczanieWypłat`** — `[Context]`: `Context`, `ListaPłac: ListaPlac`,
`Pracownik: Soneta.Kadry.Pracownik`; akcja `NaliczanieWypłat Nalicz()`; właściwości
wynikowe m.in. `Wypłaty: IList`, `Nienaliczeni: IEnumerable<BłądNaliczaniaWynagrodzenia>`,
`DataWypłaty/DataListy/DataZUS: Date`, `Okres: FromTo`, `Naliczanie: TypNaliczenia`.

**Worker `Soneta.Place.NaliczaniePlanowanychListPłacWorker`** — `[Context]`:
`Pracownik: Pracownik[]`; `Params Pars` z polami `Definicja: DefinicjaPlanowanejListyPłac`,
`DataWypłaty: Date`, `Okres: FromTo`, `Naliczanie: TypNaliczenia`, `TypWypłaty: TypWyplaty`,
`MiesiącZUS/MiesiącDeklaracji: YearMonth`, `Seria: string`, `MiesWstecz: int`,
`UwzgledniajNieZatwierdzoneListyPlac/EdycjaMiesiącaZUS: bool`;
akcja `NaliczaniePlanowanychListPłac Nalicz()`.

**Snippet (ręczne utworzenie listy + naliczenie wypłaty pracownika):**

```csharp
using Soneta.Business;
using Soneta.Place;
using Soneta.Kadry;
using Soneta.Types;

var place = session.GetPlace();

// 1. Wzorzec listy płac (definicja konfiguracyjna).
var def = place.DefListPlac.WgSymbolu["ETAT"]
          ?? throw new BusException("Brak definicji listy płac".Translate());

// 2. Pusta lista płac — KOLEJNOŚĆ: AddRow → Definicja → daty/naliczanie → Okres.
var lp = new ListaPlac();
place.ListyPlac.AddRow(lp);
lp.Definicja   = def;                          // pierwsze po AddRow
lp.Data        = new Date(2026, 6, 30);
lp.DataWyplaty = new Date(2026, 6, 30);        // wyznacza miesiąc/rok
lp.MiesiacZUS  = new YearMonth(2026, 6);
lp.Okres       = new FromTo(new Date(2026, 6, 1), new Date(2026, 6, 30));  // po DataWyplaty
// Uwaga: NIE ustawiaj lp.Naliczanie — setter rzuca bez licencji „PL Złoty"; getter ma sensowny domyślny.

// 3. Naliczenie wypłaty pracownika — sprawdzona ścieżka to NaliczanieSeryjne (patrz sekcja H);
//    naliczona wypłata zostaje automatycznie powiązana z bieżącą listą płac.
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
var pars = new NaliczanieSeryjne.PracownikParams(context) // context: w UI z workera, w teście z TestBase
{
    DataWypłaty = new Date(2026, 6, 30),
    DataListy   = new Date(2026, 6, 30),
    TypWypłaty  = TypWyplaty.Etat,
};
var wynik = new NaliczanieSeryjne.Pracownika(pars) { Pracownik = pracownik }.Nalicz();

// 4. Powiązanie wypłaty z listą jest dwukierunkowe (Wyplata.ListaPlac / Wyplata.Pracownik):
foreach (Wyplata w in wynik.WszystkieWypłaty)
{
    // w.ListaPlac, w.Pracownik
}

session.Save();
```

**Pułapki:**
- **Kolejność pól krytyczna:** `Okres` i `MiesWstecz` ustaw **po** `DataWyplaty` i `Naliczanie`
  (wzajemne zależności wyliczeń) — patrz wzorzec w kodzie naliczania list.
- `Wydzial`/`Seria` ustawiaj **warunkowo** wg `Definicja.Wydzial`/`Definicja.Seria` — inaczej
  ryzyko niespójności klucza `WgDefinicja`.
- Wypłat **nie twórz przez `new WyplataEtat(...)` ręcznie** — naliczaj. Sprawdzoną ścieżką
  naliczania jest **`NaliczanieSeryjne.Pracownika(...).Nalicz()`** (sekcja H); sam worker
  `NaliczanieWypłat { ListaPłac, Pracownik }.Nalicz()` w bazie Demo potrafi zwrócić pustą listę.
- `Wyplata.ListaPlac`/`Wyplata.Pracownik` to relacje **tylko do odczytu** — powiązania nie ustawisz
  setterem; powstają w trakcie naliczania.
- `ListyPlac` to tabela operacyjna guided — przy odczycie filtruj zakresem (`WgDatyWyplaty`,
  `WgOkresu`, `WgDefinicja`), nie skanuj całości (safe-code §6.3).
- `Wyplata.ListaPlac`/`Wyplata.Pracownik` to `IRow` (relacje interfejsowe) — porównuj/rzutuj
  świadomie.

### I2 — Drukowanie/PDF kwitków (pasków) wypłaty (★)

**Cel:** wygenerować pasek (kwitek) wypłaty pracownika do PDF.

**Mechanizm.** Wydruk realizuje serwis **`IReportService`** (namespace `Soneta.Business.UI`,
identycznie jak wydruki handlowe — patrz `dokument-handlowy.md` rozdz. 12). Wzorce pasków to
szablony `*.repx` zarejestrowane atrybutem `[DxReport]` w assembly
**`Soneta.KadryPlace.Reports`** dla `DataType = typeof(Soneta.Place.Wyplata)`:

| Wzorzec (ReportName) | Plik szablonu (`TemplateFileName`) | `DataType` |
|---|---|---|
| „Pasek wypłaty" | `PasekWyplaty.repx` | `Soneta.Place.Wyplata` |
| „Duży pasek wypłaty" | `DuzyPasekWyplaty.repx` | `Soneta.Place.Wyplata` |
| „Paski wypłat" (zbiorczy) | `PaskiWyplaty.repx` | `Soneta.Place.ListaPlac` |

**API (`IReportService` / `ReportResult` — `Soneta.Business.UI`):**
`Stream GenerateReport(ReportResult rr)`,
`ReportResult.TemplateFileName: string`, `.DataType: Type`,
`.OutputFormat: ReportFormats` (`PDF`), `.Context: Context`, `.Target: ReportTargets`.

**Snippet (pasek jednej wypłaty do strumienia PDF):**

```csharp
using Soneta.Business.UI;          // IReportService, ReportResult, ReportFormats
using Soneta.Place;

var raporty = session.GetRequiredService<IReportService>();

var context = new Context(session.Context);
context.Set(wyplata);              // pojedyncza Wyplata

var rr = new ReportResult {
    TemplateFileName = "PasekWyplaty.repx",
    DataType         = typeof(Wyplata),
    OutputFormat     = ReportFormats.PDF,
    Context          = context,
};

using Stream pdf = raporty.GenerateReport(rr);   // pierwsze 4 bajty == "%PDF"
```

**Pułapki:**
- `IReportService` pobierasz z kontenera: `session.GetRequiredService<IReportService>()`
  (potrzebne `using Microsoft.Extensions.DependencyInjection;`). Serwis i silnik raportów
  (DevExpress) oraz szablony pasków z `Soneta.KadryPlace.Reports` są dostępne **transytywnie** —
  generowanie PDF działa bez dodatkowych referencji (wzorzec jak w `dokument-handlowy.md` rozdz. 12).
- Poprawny PDF zaczyna się od bajtów `"%PDF"` — to wygodna asercja w teście.
- Druk na fizyczną drukarkę (`Target = Printer`, `PrintReport`) wymaga sprzętu — NIE testować.

### I3 — Drukowanie/PDF list płac (★)

**Cel:** wygenerować wydruk całej listy płac (pełna lista, zestawienie wypłat) do PDF.

**Mechanizm.** Identyczny jak I2 — `IReportService.GenerateReport`, szablony `[DxReport]`
w `Soneta.KadryPlace.Reports`, dla `DataType = typeof(Soneta.Place.ListaPlac)` /
`typeof(Soneta.Place.ListyPlac)`:

| Wzorzec (ReportName) | Plik szablonu | `DataType` |
|---|---|---|
| „Pełna lista płac" | `PelnaListaPlac.repx` | `Soneta.Place.ListaPlac` |
| „Wspólna pełna lista płac" | `Wspolnapelnalistaplac.repx` | `Soneta.Place.ListyPlac` (zbiór) |
| „Paski wypłat" | `PaskiWyplaty.repx` | `Soneta.Place.ListaPlac` |
| Zestawienie wypłat | `ZestawienieWyplat.repx` | `Soneta.Place.ListaPlac` |

**Snippet (pełna lista płac → PDF):**

```csharp
using Soneta.Business.UI;
using Soneta.Place;

var raporty = session.GetRequiredService<IReportService>();

var context = new Context(session.Context);
context.Set(listaPlac);            // ListaPlac

var rr = new ReportResult {
    TemplateFileName = "PelnaListaPlac.repx",
    DataType         = typeof(ListaPlac),
    OutputFormat     = ReportFormats.PDF,
    Context          = context,
};

using Stream pdf = raporty.GenerateReport(rr);
```

**Pułapki:**
- Mechanizm i dostępność serwisu — jak w I2 (działa transytywnie, bez dodatkowych referencji).
- Lista musi być policzona (mieć `Wyplaty`) — inaczej wydruk będzie pusty.
- **Niektóre szablony list wymagają pełnego kontekstu danych.** W bazie Demo wzorzec
  `PelnaListaPlac.repx` potrafi rzucić `InvalidOperationException` („Problem z przygotowaniem
  raportu") na sztucznie utworzonej liście — to ograniczenie konkretnego szablonu/kontekstu, nie
  brak referencji (pasek wypłaty `PasekWyplaty.repx` z I2 generuje się poprawnie).
- Do wydruku zbiorczego wielu list ustaw `DataType = typeof(Soneta.Place.ListyPlac)` i przekaż
  zbiór przez `Context.Set(...)` / `ReportResult.Rows`.


### I4 — Generowanie przelewów wynagrodzeń (przygotowanie przelewów) (★)

**Cel:** z naliczonej, zatwierdzonej listy płac wygenerować dokumenty przelewu wynagrodzeń
(do paczki przelewów), tak by wypłaty pracowników trafiły do zapłaty/preliminarza i mogły zostać
wyeksportowane do banku (I5).

> **Dwie różne klasy `Wyplata` — nie myl ich.** W domenie współistnieją:
> - **`Soneta.Place.Wyplata`** (moduł `PlaceModule`, tabela `Wyplaty`) — *naliczona wypłata
>   pracownika* (wynik naliczania z sekcji H/I1); to dokument **płacowy** ze składnikami
>   (`Elementy`), powiązany z listą płac (`Wyplata.ListaPlac`).
> - **`Soneta.Kasa.Wyplata`** (moduł `KasaModule`, tabela `Wyplaty`/`Zaplaty`) — *zapłata kasowa*
>   (rozchód środków). To **ona** implementuje `IDokumentPlatny`/`IDokumentKsiegowalny`, ma pola
>   rozliczeniowe (`DoRozliczenia`, `Stan`, `StanRozliczenia`, `KwotaRozliczona`, `Rozliczono`,
>   `Rozrachunki`, `Zaplaty`, `PreliminarzPoz`, `PozycjePrzelewu`, `BlokadaPrzelewow`).
>
> Mechanizm „z wypłaty do przelewu” łączy oba światy: worker płacowy czyta `Place.Wyplata` z listy
> płac i tworzy dokumenty przelewu w module Kasa (`Soneta.Kasa.PrzelewBase`, w paczce `PaczkaPrzelewow`).

**Mechanizm (publiczny kontrakt — worker płacowy):** sprawdzoną ścieżką tworzenia przelewów z
wynagrodzeń jest worker **`Soneta.Place.ListaPlac.PrzygotujPrzelewyWorker`** (assembly
`Soneta.KadryPlace`, akcja menu *„Przygotuj przelewy”* na liście/listach płac). Kontekstem
działania jest **lista płac** (`Soneta.Place.ListaPlac`) — przygotowuje przelewy dla zatwierdzonych
wypłat tej listy.

**Parametry — `PrzygotujPrzelewyWorker.Params`:**

| Pole | Typ | Uwaga |
|---|---|---|
| `Data` | `Soneta.Types.Date` | data dokumentów przelewu |
| `Paczka` | `Soneta.Kasa.PaczkaPrzelewow` | istniejąca paczka, do której trafią przelewy (opcjonalnie) |
| `DefinicjaPaczki` | `Soneta.Kasa.DefinicjaPaczkiPrzelewu` | definicja, wg której utworzyć nową paczkę (gdy `Paczka == null`) |
| `ZRachunku` | `Soneta.Kasa.RachunekBankowyFirmy` | rachunek firmy obciążany przelewami |
| `Łączone` | `bool` | łączenie przelewów do jednego podmiotu w jeden dokument |
| `ListyPłac` | `string` | opis/oznaczenie list płac (informacyjnie w tytule) |
| `ModyfikacjaTytułów` | `bool` | czy nadpisać tytuły przelewu (`Tytułem1`/`Tytułem2`) |
| `Tytułem1`, `Tytułem2` | `string` | tytuł przelewu (gdy `ModyfikacjaTytułów == true`) |
| `ZEwidencjiZrodlowej` | `bool` | bierz dane rachunku z ewidencji źródłowej |

**Akcja:** `object PrzygotujPrzelewy()` — tworzy w sesji dokumenty `Soneta.Kasa.PrzelewBase`
(tabela `Przelewy`) w paczce `PaczkaPrzelewow`; utrwalenie w bazie wymaga `session.Save()`.

**Model dokumentu przelewu (`Soneta.Kasa.PrzelewBase`, tabela `Przelewy`, root `GuidedRow`):**

| Pole | Typ | Opis |
|---|---|---|
| `Kwota` | `Soneta.Types.Currency` | kwota przelewu |
| `Podmiot` | `Soneta.Kasa.IPodmiotKasowy` | odbiorca (m.in. `Pracownik`, `ZUS`, `UrzadSkarbowy`, `Bank`, `Kontrahent`) |
| `Rachunek` | `Soneta.Kasa.RachunekBankowyPodmiotu` | rachunek odbiorcy |
| `RachunekZleceniodawcy` | `Soneta.Kasa.NumerRachunku` | rachunek firmy (obciążany) |
| `Data` | `Soneta.Types.Date` | data przelewu |
| `Definicja` | `Soneta.Core.DefinicjaDokumentu` | definicja dokumentu |
| `Numer` | `Soneta.Core.NumerDokumentu` | numer (nadawany automatycznie) |
| `Tytulem1`, `Tytulem2` | `string` | tytuł przelewu |
| `Typ2` | `Soneta.Kasa.TypPrzelewu2` | wariant przelewu (zwykły / **MPP** / itp.) |
| `PaczkaPrzelewow` | `Soneta.Kasa.PaczkaPrzelewow` | paczka, do której należy przelew |
| `Bufor` / `Zatwierdzony` | `bool` | stan dokumentu |
| `Exported` | `bool` | czy wyeksportowany (po I5 — `true`, blokuje edycję) |

**Przelewy okresowe / MPP:**
- **MPP (mechanizm podzielonej płatności)** to *wariant* przelewu — wyrażany przez
  `PrzelewBase.Typ2: Soneta.Kasa.TypPrzelewu2` (oraz na `Kasa.Wyplata` polem `KwotaMPP`,
  `MozliweMechanizmyMPP`). Dla wynagrodzeń MPP zwykle nie dotyczy (to mechanizm faktur VAT), ale
  kontrakt go przewiduje.
- **Przelewy okresowe** (cykliczne płatności np. składek z list) realizuje osobny worker
  księgowy `Soneta.Ksiega.Kasowe.NaliczaniePrzelewowOkresowych` (poza zakresem płac pracownika).

**Powiązanie z wypłatą / preliminarzem (publiczne kolekcje na `Pracownik`):**

| Kolekcja na `Pracownik` | Typ | Zawiera |
|---|---|---|
| `Pracownik.Przelewy` | `SubTable<Soneta.Kasa.PrzelewBase>` | przelewy pracownika |
| `Pracownik.DokumentyPreliminarza` | `SubTable<Soneta.Kasa.PreliminarzDokument>` | dokumenty preliminarza |
| `Pracownik.DokumentyRozliczeniowe` | `SubTable<Soneta.Kasa.DokRozliczBase>` | dokumenty rozliczeniowe |
| `Pracownik.Rozrachunki` | `SubTable<Soneta.Kasa.RozrachunekIdx>` | rozrachunki |
| `Pracownik.Rachunki` | `SubTable<Soneta.Kasa.RachunekBankowyPodmiotu>` | rachunki bankowe pracownika |

> **Korekta (zweryfikowane kompilacją + skanem DLL):** `Pracownik.Platnosci` **nie istnieje** w publicznym
> kontrakcie kartoteki pracownika — kolekcja `Platnosci` występuje tylko na interfejsie
> `Soneta.Kasa.IDokumentPlatny` (np. `Kasa.Wyplata.Platnosci`), nie na `Pracownik`. Płatności podmiotu
> czytaj przez `Pracownik.Rozrachunki` / `Pracownik.DokumentyRozliczeniowe`.

**Snippet (worker — w UI/teście z dostępnym `Context`):**

```csharp
using Soneta.Business;
using Soneta.Place;          // ListaPlac, ListaPlac.PrzygotujPrzelewyWorker
using Soneta.Kasa;           // PaczkaPrzelewow, PrzelewBase, RachunekBankowyFirmy
using Soneta.Types;

// listaPlac: zatwierdzona lista płac z naliczonymi wypłatami (sekcja I1)
var pars = new ListaPlac.PrzygotujPrzelewyWorker.Params
{
    Data = Date.Today,
    // Paczka = istniejacaPaczka,              // albo nowa wg DefinicjaPaczki:
    // DefinicjaPaczki = session.GetKasa().DefPaczekPrzelewow.WgSymbolu["..."],
    // ZRachunku = rachunekFirmy,              // RachunekBankowyFirmy
    Łączone = false,
};

var worker = new ListaPlac.PrzygotujPrzelewyWorker { Pars = pars };
// kontekstem workera jest lista płac; uruchomienie akcji:
worker.PrzygotujPrzelewy();

session.Save();   // utrwalenie dokumentów przelewu w bazie
```

**Pułapki / ograniczenia (bądź szczery):**
- **`Place.Wyplata` ≠ `Kasa.Wyplata`** — pola rozliczeniowe (`DoRozliczenia`, `Stan`,
  `StanRozliczenia`, `Rozrachunki`, `BlokadaPrzelewow`) są na **kasowej** `Soneta.Kasa.Wyplata`
  (`IDokumentPlatny`), nie na płacowej. Skanując „Wyplata” trafia się na kasową.
- **Lista płac musi być zatwierdzona i naliczona** — `PrzygotujPrzelewy` na pustej/niezatwierdzonej
  liście nie ma czego przelać.
- **Wymaga konfiguracji modułu Kasa** — definicji paczki przelewów (`DefinicjaPaczkiPrzelewu`),
  rachunku firmy (`RachunekBankowyFirmy`) oraz rachunku pracownika (`Pracownik.Rachunki`). Brak
  rachunku odbiorcy → przelew nie powstanie albo będzie niekompletny. **W bazie Demo te elementy
  mogą nie być skonfigurowane**, dlatego generowanie przelewów w teście jednostkowym jest
  niepewne (patrz spec testowy).
- Worker **sam zatwierdza zmiany w sesji** (otwiera transakcję) — nie owijaj w dodatkowy
  `session.Logout(true)`; do bazy idą w `Save()`.
- `PrzelewBase.Podmiot`/`Powiazanie` to relacje **interfejsowe** (`IRow`/`IPodmiotKasowy`) —
  rzutuj świadomie.
- `Przelewy` to tabela operacyjna guided — przy odczycie filtruj zakresem (safe-code §6.3).

---

### I5 — Eksport wynagrodzeń do banku / pliku przelewów (★)

> **UWAGA — operacja plikowa/integracyjna.** Eksport zapisuje **fizyczny plik** w formacie
> bankowym (Elixir, MT940-pochodne, formaty walutowe). To wejście/wyjście do systemu zewnętrznego —
> **nie jest to przedmiot testu jednostkowego** (zależy od ścieżki na dysku, formatu banku,
> sterownika eksportu i — przy wysyłce online — od sieci). Dokumentujemy **model i publiczny
> kontrakt**, a sam eksport pliku oznaczamy jako nietestowalny jednostkowo.

**Cel:** wyeksportować przygotowane przelewy (I4) do pliku przelewów dla systemu bankowości
elektronicznej.

**Mechanizm (publiczny kontrakt — worker Kasa):** worker **`Soneta.Kasa.EksportPrzelewowWorker`**
(akcja menu *„Eksport przelewów”*, metoda `Eksport()`), sterowany przez
`Soneta.Kasa.EksportPrzelewowParams`.

> **Korekta (zweryfikowane kompilacją):** `EksportPrzelewowParams` **nie ma konstruktora
> bezparametrowego** — wymaga `EksportPrzelewowParams(Context ctx, RachunekBankowyFirmy rachunek, PrzelewBase[] przelewy)`.
> Co więcej, **sam konstruktor waliduje rachunek** i rzuca `System.ApplicationException`
> („Eksport niemożliwy. Nie wskazano rachunku w filtrach listy.”), gdy `rachunek == null`. Dlatego nie da się
> utworzyć parametrów samym inicjalizatorem obiektu. W teście jednostkowym kontrakt API weryfikuj **refleksją**
> (istnienie typu, sygnatura konstruktora, property `FileName`/`Params`, metoda `Eksport`), bez instancjonowania.

**Parametry — `Soneta.Kasa.EksportPrzelewowParams`:**

| Pole | Typ | Uwaga |
|---|---|---|
| `FileName` | `string` | **ścieżka pliku wyjściowego** — operacja na dysku |
| `AppendToFile` | `bool` | dopisanie do istniejącego pliku |
| `PrzelewyZgodne` | `IList<Soneta.Kasa.PrzelewBase>` | przelewy do wyeksportowania |
| `Rachunek` | `Soneta.Kasa.RachunekBankowyFirmy` | rachunek firmy (zleceniodawca) |
| `PrmDataPrzelewow` | `Soneta.Types.Date` | data realizacji |
| `PrmNumerPaczki` | `string` | numer paczki |
| `PrmZakres` | `Soneta.Kasa.ZakresEksportuPrzelewow` | zakres (wszystkie / wg paczki / zaznaczone) |
| `EksportujWBuforze` | `bool` | uwzględnij przelewy w buforze |
| `InfoBank`, `InfoFormatKraj`, `InfoFormatWalutowy`, `InfoRachunekBankowy` | `string` | parametry formatu/banku |
| `WithoutHelper` | `bool` | tryb bez kreatora |

**Akcja:** `object Eksport()` — zapisuje plik wg `FileName`. Po eksporcie przelewy są oznaczane
jako wyeksportowane (`PrzelewBase.Exported == true`, blokada dalszej edycji).

**Powiązane (kontekst):**
- Eksport całych **paczek**: worker `Soneta.Kasa.EksportPaczekPrzelewowWorker`.
- Eksport przelewów PPK z pulpitu KBR: `Soneta.EI.UI.PulpitKBR.Workers.PulpitKBEksportPrzelewowWorker`.

**Snippet (kontrakt — w realnej integracji, nie w teście jednostkowym):**

```csharp
using Soneta.Kasa;          // EksportPrzelewowWorker, EksportPrzelewowParams, PrzelewBase
using System.Collections.Generic;

PrzelewBase[] przelewy = /* przelewy z I4, np. z paczki */;

// Konstruktor jest WYMAGANY (brak ctora bezparametrowego) i waliduje rachunek (rzuca, gdy null):
var par = new EksportPrzelewowParams(context, rachunekFirmy, przelewy)   // rachunekFirmy: RachunekBankowyFirmy
{
    FileName = @"C:\przelewy\wynagrodzenia.txt",   // ŚCIEŻKA PLIKU — operacja I/O
    PrmDataPrzelewow = Date.Today,
    EksportujWBuforze = false,
};

var worker = new EksportPrzelewowWorker { Params = par };
worker.Eksport();   // zapis pliku na dysk — efekt uboczny poza sesją
```

**Pułapki / ograniczenia (bądź szczery):**
- **Eksport pliku NIE nadaje się do testu jednostkowego** — pisze na dysk, zależy od formatu banku
  i sterownika eksportu; w teście co najwyżej dokumentujemy istnienie API
  (`EksportPrzelewowWorker`, `EksportPrzelewowParams.FileName`), bez wywołania `Eksport()`.
- Format pliku zależy od **konfiguracji formatu eksportu** danego banku — nie ma jednego
  uniwersalnego formatu; `InfoFormat*`/`InfoBank` parametryzują wynik.
- Wysyłka online (bankowość elektroniczna / API banku) to dodatkowo operacja **sieciowa** — poza
  zakresem testów jednostkowych.
- Po eksporcie `PrzelewBase.Exported = true` blokuje edycję — ponowny eksport wymaga
  `EksportujWBuforze`/zmiany stanu.

---

### I6 — Wystawienie faktury / faktury zbiorczej z zapłaty (rozliczenia) (★)

> **Zakres i szczerość.** Faktura jest dokumentem **handlowym** (`Soneta.Handel.DokumentHandlowy`),
> nie płacowym — to nie jest funkcja kartoteki pracownika ani list płac. Powiązanie „z zapłaty”
> dotyczy **rozrachunków/rozliczeń** (moduł Kasa): zapłata (`Soneta.Kasa.Wyplata`/`Wplata` —
> `IDokumentPlatny`) jest **rozliczana** z dokumentem płatnym (np. fakturą) przez rozrachunki.
> Wystawianie faktury z poziomu pracownika/płac w publicznym kontrakcie **nie istnieje**;
> tutaj dokumentujemy model rozliczeń, który łączy zapłatę z fakturą.

**Cel:** powiązać zapłatę z dokumentem płatnym (fakturą) na poziomie rozrachunków/rozliczeń —
oraz wskazać, gdzie w publicznym API leży rozliczanie należności/zobowiązań pracownika.

**Model rozliczeń (publiczny kontrakt, moduł `KasaModule`):**

| Element | Typ / kolekcja | Rola |
|---|---|---|
| Zapłata (rozchód/wpływ) | `Soneta.Kasa.Wyplata` / `Soneta.Kasa.Wplata` | dokument płatny (`IDokumentPlatny`) |
| Płatność (zobowiązanie/należność) | `Soneta.Kasa.Platnosc` (tabela `Platnosci`, `IRozliczalny`) | to z nią rozlicza się zapłatę |
| Rozliczenie (powiązanie SP) | `Soneta.Kasa.RozliczenieSP` (tabela `RozliczeniaSP`, `IRozliczenie`) | wiąże zapłatę z płatnością/dokumentem |
| Rozrachunek | `Soneta.Kasa.RozrachunekIdx` (tabela `RozrachunkiIdx`) | indeks rozrachunkowy podmiotu |
| Stan rozliczenia zapłaty | `Wyplata.StanRozliczenia: Soneta.Kasa.StanRozliczenia`, `Wyplata.DoRozliczenia`, `Wyplata.KwotaRozliczona`, `Wyplata.Rozliczono` | ile pozostało / czy rozliczono |

**Kolekcje na zapłacie (`Soneta.Kasa.Wyplata`):**
- `Wyplata.Zaplaty: SubTable<RozliczenieSP>` oraz `Wyplata.Dokumenty: SubTable<RozliczenieSP>` —
  rozliczenia,
- `Wyplata.Rozrachunki: SubTable<RozrachunekIdx>` — rozrachunki,
- `Wyplata.PreliminarzPoz: PreliminarzPozycja` — pozycja preliminarza.

**Kolekcje na `Pracownik` (rozrachunki/faktury podmiotu):**
- `Pracownik.Rozrachunki`, `Pracownik.DokumentyRozliczeniowe`,
  `Pracownik.DokumentyPreliminarza` (jak w tabeli I4). **Uwaga:** `Pracownik.Platnosci` **nie istnieje** —
  kolekcja `Platnosci` jest tylko na `IDokumentPlatny` (np. `Kasa.Wyplata.Platnosci`).

**Workery rozliczeniowe (publiczny kontrakt, akcje menu):**

| Worker | Rola |
|---|---|
| `Soneta.Kasa.RozliczWgPrzelewowWyplataWorker` | rozliczenie zapłaty wg przelewów |
| `Soneta.Kasa.RozliczPreliminarzIdxWorker` / `...TblWorker` / `...FrmWorker` | rozliczenie z preliminarzem |
| `Soneta.Kasa.PreliminarzPozycja.DodajRozliczenieWorker` | dodanie rozliczenia do pozycji preliminarza |
| `Soneta.Ksiega.UtworzPlatnoscZZapisuWorker` | utworzenie płatności z zapisu (księga) |

**Faktura zbiorcza:** powstaje po stronie **handlowej** — z wielu zapłat/płatności tworzy się jeden
dokument handlowy (faktura) zbiorąc je jako rozliczenia. To domena `dokument-handlowy.md`
(wystawianie i rozliczanie faktur), nie kartoteki pracownika. Z poziomu rozliczeń pracownika
publiczny kontrakt udostępnia **odczyt i rozliczanie** rozrachunków, a nie „wystaw fakturę”.

**Snippet (odczyt stanu rozliczenia zapłat — publiczny kontrakt):**

```csharp
using Soneta.Kasa;          // Wyplata, StanRozliczenia
using Soneta.Types;

// Zapłaty pracownika rozliczane z dokumentami (np. fakturami) — odczyt stanu rozliczeń.
// Iteruj zawsze w zakresie/okresie (tabela operacyjna guided — safe-code §6.3).
foreach (RozrachunekIdx r in pracownik.Rozrachunki)
{
    // r — pozycja rozrachunkowa pracownika (powiązanie zapłata ↔ dokument)
}

// Stan rozliczenia konkretnej zapłaty kasowej:
// Wyplata zaplata = ...;
// var doRozl   = zaplata.DoRozliczenia;     // ile pozostało do rozliczenia (Currency)
// var stan     = zaplata.StanRozliczenia;   // StanRozliczenia (enum)
// var czyRozl  = zaplata.Rozliczono;        // bool
```

**Pułapki / ograniczenia (bądź szczery):**
- **„Wystaw fakturę z pracownika/płac” nie istnieje w publicznym kontrakcie.** Faktura to dokument
  handlowy; powiązanie z zapłatą realizują **rozrachunki/rozliczenia** (moduł Kasa), nie kartoteka
  pracownika. To zadanie jest z pogranicza domen — opis kierujemy do `dokument-handlowy.md`.
- Pola rozliczeniowe (`DoRozliczenia`, `Stan`, `StanRozliczenia`, `KwotaRozliczona`, `Rozliczono`,
  `Rozrachunki`) są na **`Soneta.Kasa.Wyplata`** (`IDokumentPlatny`), a nie na płacowej
  `Soneta.Place.Wyplata`.
- Rozliczanie/tworzenie faktury zbiorczej **wymaga skonfigurowanego modułu Kasa/Handel** (definicje
  dokumentów, rachunki, płatności). W bazie Demo część konfiguracji może nie być gotowa — operacje
  zapisujące są niepewne w teście (patrz spec testowy).
- `Platnosc`/`RozliczenieSP`/`RozrachunekIdx` to obiekty operacyjne — przy odczycie filtruj zakresem
  i nie skanuj całych tabel (safe-code §6.3).

---

#### Spec testowy (zwarty) — I4 / I5 / I6

Konwencja: `Soneta.Skills.Test/KadryPlace/Pracownik/`, klasa `RozdzialI_ListyWydrukiTest`
(lub nowa `RozdzialI_PrzelewyRozliczeniaTest : PracownikTestBase`); baza Demo + rollback;
operujemy wyłącznie na publicznym kontrakcie.

**I4 — `I4_PrzygotujPrzelewy_ZListyPlac`**
- *Co testowalne:* naliczenie wypłaty etatowej (`NaliczanieSeryjne.Pracownika`, jak I1b) → uzyskanie
  `ListaPlac` z `Wyplata.ListaPlac`; **konstrukcja** `ListaPlac.PrzygotujPrzelewyWorker` z `Params`
  (asercja, że worker i typ `Params` istnieją w publicznym API; że pola `Data`/`Paczka`/`ZRachunku`
  są dostępne). Odczyt kolekcji `Pracownik.Przelewy`, `Pracownik.DokumentyPreliminarza`,
  `Pracownik.Rozrachunki` (asercja: kolekcje dostępne, iterowalne).
- *Niepewne / `[Ignore]`/`Assert.Ignore`:* faktyczne **wywołanie** `worker.PrzygotujPrzelewy()` i
  powstanie dokumentów `PrzelewBase` — zależy od konfiguracji modułu Kasa (definicja paczki,
  `RachunekBankowyFirmy`, rachunek pracownika `Pracownik.Rachunki`), której baza Demo nie gwarantuje.
  Owinąć w `try/catch` + `Assert.Ignore` z opisem (wzorzec jak I2/I3) i asercję na powstaniu
  przelewu robić tylko, gdy się udało.

**I5 — `I5_EksportPrzelewow_KontraktApi`**
- *Co testowalne:* **istnienie publicznego API** — weryfikacja **refleksją** (NIE instancjonuj!):
  typ `EksportPrzelewowParams`, konstruktor `(Context, RachunekBankowyFirmy, PrzelewBase[])`,
  property `FileName`; typ `EksportPrzelewowWorker`, property `Params`, metoda `Eksport`.
  **Nie używaj inicjalizatora `new EksportPrzelewowParams { ... }`** — nie ma ctora bezparametrowego,
  a ctor `(ctx, rachunek, przelewy)` rzuca `ApplicationException`, gdy `rachunek == null` (brak konfiguracji w Demo).
- *Niewykonalne w teście jednostkowym → `[Ignore]`:* wywołanie `worker.Eksport()` — **operacja
  plikowa** (zapis na dysk wg `FileName`), zależna od formatu banku/sterownika; wysyłka online =
  operacja sieciowa. **Nie wołać `Eksport()`** w teście; udokumentować jako `[Ignore("operacja
  plikowa/sieciowa — poza testem jednostkowym")]`.

**I6 — `I6_Rozliczenia_OdczytStanu`**
- *Co testowalne:* odczyt kolekcji rozliczeniowych pracownika — `Pracownik.Rozrachunki`,
  `Pracownik.DokumentyRozliczeniowe`, `Pracownik.DokumentyPreliminarza`
  (asercja: dostępne, iterowalne, typy zgodne — `RozrachunekIdx`, `DokRozliczBase`,
  `PreliminarzDokument`). **`Pracownik.Platnosci` NIE istnieje** — pomiń (kolekcja `Platnosci` jest tylko na
  `IDokumentPlatny`); odczyt pól rozliczeniowych z `Soneta.Kasa.Wyplata` (`DoRozliczenia`,
  `Stan`, `StanRozliczenia`, `Rozliczono`) — gdy istnieje zapłata kasowa w Demo.
- *Niewykonalne / `[Ignore]`:* **wystawienie faktury (zbiorczej) z zapłaty** — funkcja handlowa,
  brak w kontrakcie pracownika; rozliczanie zapisujące (`RozliczWgPrzelewowWyplataWorker`,
  `RozliczPreliminarz*Worker`) wymaga konfiguracji Kasa/Handel → `Assert.Ignore` przy braku danych.
  Dla wystawiania faktur kierować do testów domeny handlowej (`dokument-handlowy.md`).

**Dokładne nazwy (do użycia w testach):**
- Worker płacowy: `Soneta.Place.ListaPlac.PrzygotujPrzelewyWorker` (+ zagn. `.Params`;
  akcja `PrzygotujPrzelewy`).
- Worker eksportu: `Soneta.Kasa.EksportPrzelewowWorker` + `Soneta.Kasa.EksportPrzelewowParams`
  (akcja `Eksport`); paczki: `Soneta.Kasa.EksportPaczekPrzelewowWorker`.
- Dokumenty: `Soneta.Kasa.PrzelewBase` (tabela `Przelewy`), `Soneta.Kasa.PaczkaPrzelewow`
  (tabela `PaczkiPrzelewow`), `Soneta.Kasa.DefinicjaPaczkiPrzelewu`, `Soneta.Kasa.RachunekBankowyFirmy`.
- Rozliczenia: `Soneta.Kasa.Platnosc`, `Soneta.Kasa.RozliczenieSP`, `Soneta.Kasa.RozrachunekIdx`,
  `Soneta.Kasa.PreliminarzDokument`, `Soneta.Kasa.PreliminarzPozycja`.
- Zapłata kasowa (`IDokumentPlatny`): `Soneta.Kasa.Wyplata` (NIE `Soneta.Place.Wyplata`).
- Kolekcje na `Pracownik`: `Przelewy`, `Rozrachunki`, `DokumentyPreliminarza`,
  `DokumentyRozliczeniowe`, `Rachunki` (**bez `Platnosci`** — ta kolekcja jest tylko na `IDokumentPlatny`).

## J. Deklaracje (ZUS, PIT, PFRON, PPK)

> **Moduł.** `Soneta.Deklaracje.DeklaracjeModule` — dostęp z sesji przez `session.GetDeklaracje()`.
> Wszystkie deklaracje (ZUS, PIT, PFRON, PPK) to wiersze tabeli `Deklaracje`, dziedziczące po
> abstrakcyjnej klasie root `Soneta.Deklaracje.Deklaracja` (`GuidedRow`, implementuje m.in.
> `IDeklaracja`, `IDokumentPlatny`, `IDokumentKsiegowalny`). Konkretne typy żyją w podprzestrzeniach:
> `Soneta.Deklaracje.ZUS.*`, `Soneta.Deklaracje.PIT.*`, `Soneta.Deklaracje.PFRON.*`,
> `Soneta.Deklaracje.PPK.*`.
>
> **Rozróżnienie kluczowe dla testów — NALICZENIE/UTWORZENIE vs E-WYSYŁKA.**
> - **Naliczenie/utworzenie deklaracji** (workery `*Worker` z akcjami „Przygotuj…/Nalicz…/Przelicz”,
>   operacje PPK) tworzy **wiersze w bazie** — to operacja lokalna, w zasadzie testowalna na Demo,
>   ale **wymaga `Context`** (i dla ZUS zwykle obiektu `KEDU`). Workery nie mają konstruktorów
>   bezparametrowych dających pełny kontrakt — `Params` budujemy z `Context`/`Session`.
> - **E-wysyłka** to osobne typy: `EDeklaracja` (tabela `EDeklaracje` — XML, podpis, UPO) oraz
>   `ETransmisja` (tabela `ETransmisje` — pojedyncze transmisje do bramki). Eksport KEDU/PUE realizują
>   workery `Soneta.Deklaracje.UI.KeduEksportForm.EksportWorker` (akcje „Eksport KEDU”, „Pobierz KEDU”)
>   i `Soneta.Deklaracje.UI.PUEEksportForm.EksportWorker` (akcja „Eksport PUE (RUD)”), a uruchomienie
>   Programu Płatnika — `Soneta.Deklaracje.ZUS.DeklaracjaZUS.UruchomPPWorker` (akcja
>   „Uruchom 'Program Płatnika'”). **To operacje sieciowe/plikowe/zewnętrzne — NIE do testu** (nawet
>   utworzenie `EDeklaracja` wymaga podpisu i bramki ZUS/US).
>
> **`KEDU` (`Soneta.Deklaracje.ZUS.KEDU`)** — „zestaw deklaracji”: kontener (komplet dokumentów ZUS),
> do którego workery zgłoszeniowe i rozliczeniowe dopinają wygenerowane bloki. Praktycznie każdy worker
> ZUS przyjmuje `Kedu` w swoich `Params`; bez przekazanego `KEDU` generowanie deklaracji ZUS nie ma
> gdzie zapisać wyniku. KEDU nie jest tworzony „w locie” w sposób trywialny — jest częścią mechanizmu
> deklaracji rozliczeniowych ZUS i jego zbudowanie wymaga środowiska modułu Deklaracje (`Context`).

---

### J1 — Zgłoszenia ZUS (ZUA/ZZA, ZCNA, ZWUA)

**Cel:** zgłosić/wyrejestrować pracownika i jego umowy w ZUS oraz zgłosić członków rodziny do
ubezpieczenia zdrowotnego. Typy zgłoszeń to wiersze deklaracji: `ZUA` (społeczne + zdrowotne),
`ZZA` (tylko zdrowotne), `ZCNA` (rodzina), `ZWUA` (wyrejestrowanie), `ZIUA` (zmiana danych
identyfikacyjnych), `ZCZA` (zmiana danych członka rodziny) — wszystkie w `Soneta.Deklaracje.ZUS`.

**Workery — poziom `Pracownicy` (klasy zagnieżdżone `Soneta.Deklaracje.ZUS.ZarejestrujPracownikówWorker`):**

| Worker (akcja) | `Params` (typ) | Pola `Params` | Metoda akcji |
|---|---|---|---|
| `ZarejestrujPracownikówWorker.Rejestracja` — „Deklaracje ZUS/Przygotuj ZUA i ZZA” | `ZarejestrujBaseWorker.ParamsKor` | `Okres: FromTo`, `DataDokumentu`/`DataWypełnienia: Date`, `Kedu: KEDU`, `KorektaZmiana: ZgloszenieZUS.KorektaZmiana`, `ZarejestrujRodzinę: bool` | `object ZarejestrujPracowników()` |
| `ZarejestrujPracownikówWorker.Rodzina` — „Deklaracje ZUS/Przygotuj ZCNA” | `ZarejestrujBaseWorker.Params` | `Okres`, `DataDokumentu`, `DataWypełnienia`, `Kedu` | `object ZarejestrujRodzinę()` |
| `ZarejestrujPracownikówWorker.Wyrejestrowanie` — „Deklaracje ZUS/Przygotuj ZWUA” | `Wyrejestrowanie.ParamsWR` | `Okres`, `DataDokumentu`, `DataWypełnienia`, `Kedu`, `RIA: bool`, `WyrejestrujRodzinę: bool` | `object WyrejestrujPracowników()` |
| `ZarejestrujPracownikówWorker.ZgloszenieUmow` — „Deklaracje ZUS/Przygotuj RUD” | `ZgloszenieUmow.UParams` | `Okres`, `DataWypełnienia`, `Kedu`, `Trwajace: bool` | `object ZgłośUmowy()` |

> Worker przyjmuje zaznaczone osoby przez `Pracownicy: Pracownik[]` (`[Context]`). Wszystkie `Params`
> mają ctor `(Context)`. Po akcji wynik (lista wygenerowanych deklaracji) odczytasz z bazowego
> `Deklaracje: View`, a `Save()` zatwierdza.

**Workery — poziom `Umowy` (zleceniobiorcy), `Soneta.Deklaracje.ZUS.ZarejestrujUmowyWorker`** —
opisane w **G5** (`Rejestracja.ZarejestrujUmowy()` → ZUA/ZZA wg schematu `UmowaHistoria.Ubezpieczenia`,
`Wyrejestrowanie.WyrejestrujUmowy()` → ZWUA). `ParamsZ`/`ParamsW` mają ctor `(Context)`; pola
bazowe `Okres`/`DataDokumentu`/`DataWypełnienia`/`Kedu` + `ZarejestrujRodzinę`/`WyrejestrujRodzinę`.

**ZCNA na rodzinie (A9).** Zgłoszenie członka rodziny do ubezpieczenia zdrowotnego startuje z danych
`CzlonekRodziny` (`Ubezpieczony = true`, `UbezpieczenieOkres`, `StPokrewienstwa` — patrz A9), a samą
deklarację ZCNA generuje `ZarejestrujPracownikówWorker.Rodzina` (lub `Rejestracja` z
`Pars.ZarejestrujRodzinę = true`). Dla zleceniobiorcy analogicznie przez `ZarejestrujUmowyWorker`.

**Przerejestrowanie (A19).** `Soneta.Deklaracje.UI.PrzerejestrowaniePracownikaWorker` (DataType
`PracHistoria`) oraz `Soneta.Deklaracje.UI.PrzerejestrowanieZleceniobiorcyWorker` (DataType
`UmowaHistoria`) — generują ZWUA+ZUA przy zmianie tytułu/wydziału. `Params` wymaga `KEDU` + `Context`.

**Snippet (przygotowanie ZUA/ZZA dla zaznaczonych pracowników):**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];

var pars = new Soneta.Deklaracje.ZUS.ZarejestrujBaseWorker.ParamsKor(context)
{
    Okres           = new FromTo(new Date(2026, 1, 1), Date.MaxValue),
    DataDokumentu   = new Date(2026, 1, 1),
    DataWypełnienia = Date.Today,
    Kedu            = kedu,              // KEDU z modułu Deklaracje (Context)
    ZarejestrujRodzinę = false,
};
var rejestracja = new Soneta.Deklaracje.ZUS.ZarejestrujPracownikówWorker.Rejestracja
{
    Pracownicy = new[] { pracownik },
    Pars       = pars,
};
rejestracja.ZarejestrujPracowników();   // tworzy ZUA/ZZA (i ZCNA, gdy ZarejestrujRodzinę)
session.Save();
```

**Pułapki:**
- Typ zgłoszenia (ZUA vs ZZA) wynika ze schematu ubezpieczeń (`Etat.Ubezpieczenia` / `UmowaHistoria.Ubezpieczenia`,
  A7/G5) — nie z parametru workera. Ustaw `Tyub4` i flagi `Spoleczne`/`Zdrowotne` przed zgłoszeniem.
- Każdy `Params` wymaga `Context` (ctor `(Context)`) i pola `Kedu` — bez `KEDU` deklaracja nie ma
  kontenera docelowego. Operacja jest **lokalna** (zapis wiersza), ale niewykonalna bez `Context`/`KEDU`.
- `ZWUA` z `RIA = true` powiązany jest z mechanizmem RIA (J2).
- Workery zgłoszeniowe na `Pracownicy` dotyczą etatowych; na `Umowy` — zleceniobiorców (G5).

---

### J2 — Deklaracje rozliczeniowe ZUS (DRA, RIA, IMIR, RUD, IWA; KEDU)

**Cel:** naliczyć/utworzyć deklaracje rozliczeniowe i informacyjne ZUS. Typy (`Soneta.Deklaracje.ZUS`,
wiersze tabeli `Deklaracje`): `DRA` (deklaracja rozliczeniowa z załącznikami RCA/RSA/RZA; ctor `(KEDU)`),
`RIA` (informacja roczna / raport po ustaniu zatrudnienia; ctor `(Pracownik, KEDU)`), `RMUA` —
informacja miesięczna dla ubezpieczonego, potocznie **IMIR** (ctor `(Pracownik, RMUA.TypOkresuDeklaracji)`;
**brak osobnego typu `IMIR` w CLR — to `RMUA`**), `RUD` (zgłoszenie umowy o dzieło), `IWA` (informacja o wypadkach/składce wypadkowej),
`OSW` (oświadczenie), `Z3`/`Z3a` (zaświadczenia płatnika ERP-7 — patrz niżej), `KEDU` (zestaw).

**Naliczanie seryjne — poziom `Pracownicy`:**

| Worker (akcja) | `Params` (typ) | Pola `Params` | Metoda |
|---|---|---|---|
| `Soneta.Deklaracje.ZUS.NaliczanieSeryjneRIAWorker` — „Deklaracje ZUS/Przygotuj RIA” | `…RIAWorker.Params` | `DataDokumentu`/`DataWypełnienia: Date`, `Kedu: KEDU`, `Wydział: Wydzial`, `Wszystkie: bool`, `Zerowa: bool` | `object NaliczRMUA(Context)` |
| `Soneta.Deklaracje.ZUS.NaliczanieSeryjneRMUAWorker` — „Deklaracje ZUS/Przygotuj IMIR” | `…RMUAWorker.Params` | `DataDokumentu`/`DataWypełnienia: Date`, `Miesiac: YearMonth`, `Rok: int`, `TypOkresu: RMUA.TypOkresuDeklaracji`, `Oskladkowani: bool`, `Wydział`, `Wszystkie` | `object NaliczRMUA(Context)` |

> Oba workery mają **ctor bezparametrowy**, przyjmują `Pracownicy: Pracownik[]` (`[Context]`) i mają w props
> `Context`, `Kedu`, `Deklaracje: View`. Metoda akcji `NaliczRMUA(Context)` (ta sama nazwa dla RIA i RMUA).
> `Params` są property `Pars` (setter); na workerze `RMUAWorker` pola `Params` są też wystawione bezpośrednio jako property.

**Przeliczenie pojedynczej deklaracji — `Soneta.Deklaracje.DeklaracjaWorker`** (DataType `Deklaracja`,
więc działa dla **dowolnej** deklaracji ZUS/PIT/PFRON): akcja **„Przelicz”** → `void Przelicz()`;
parametr `Deklaracja: Soneta.Deklaracje.Deklaracja` (`[Context]`).

**RUD** generuje `ZarejestrujPracownikówWorker.ZgloszenieUmow` (J1) lub jest dostępna na liście umów.
**DRA z załącznikami** to root `DeklaracjaZUS`; nalicza się przez mechanizm KEDU + `Przelicz`.

**E-wysyłka (NIE testować):** eksport KEDU — `KeduEksportForm.EksportWorker` („Eksport KEDU”,
„Pobierz KEDU”); eksport PUE/RUD — `PUEEksportForm.EksportWorker` („Eksport PUE (RUD)”); Program
Płatnika — `DeklaracjaZUS.UruchomPPWorker`.

**Pułapki:**
- `KEDU` jest osią całego rozliczenia ZUS — wszystkie workery rozliczeniowe wpisują wynik do
  przekazanego `Kedu`. Bez modułu Deklaracje (`Context`) i `KEDU` operacji nie złożysz.
- `DeklaracjaWorker.Przelicz()` przelicza **istniejący** wiersz deklaracji — najpierw musi powstać
  (np. z naliczania seryjnego), więc to nie jest „utworzenie od zera”.

---

### J3 — Deklaracje PIT (PIT-11, PIT-4R, PIT-8AR, PIT-R, IFT-1/IFT-1R, PIT-8C)

**Cel:** naliczyć imienne i zbiorcze deklaracje podatkowe. Typy (`Soneta.Deklaracje.PIT`, wiersze
tabeli `Deklaracje`): `PIT11`, `PIT4`/PIT-4R (rozliczeniowa zaliczek), `PIT8A`/PIT-8AR (zryczałtowany),
`PITR` (PIT-R), `IFT1`/`IFT1R`, `PIT8C`, `PIT40`, plus `ZbiorczaPIT`/`IEDeklaracjaZbiorczaItem`
(deklaracje zbiorcze).

**Naliczanie seryjne — poziom `Pracownicy` (klasy zagnieżdżone `Soneta.Deklaracje.PIT.NaliczanieSeryjne`):**

| Worker (akcja) | Ctor | `Params` — pola | Metoda |
|---|---|---|---|
| `NaliczanieSeryjne.PIT_11Worker` — „Deklaracje PIT/Nalicz PIT 11” | `(Session session)` | `Okres: FromTo`, `Data: Date`, `Naliczaj: NaliczanieDeklaracje`, `BezPotwierdzenia: bool`, dane podpisującego (`Imię`/`Nazwisko`/`Stanowisko` + `…Odp`), `TreśćUzasadnienia: string` | `object Nalicz_PIT_11()` |
| `NaliczanieSeryjne.PIT_RWorker` — „Deklaracje PIT/Nalicz PIT R” | `(Session)` | jw. (`Params`) | `Nalicz…()` |
| `NaliczanieSeryjne.PIT_8CWorker` — „Deklaracje PIT/Nalicz PIT 8C” | `(Session)` | jw. | `Nalicz…()` |
| `NaliczanieSeryjne.IFT_1Worker` / `IFT_1RWorker` — „Deklaracje PIT/Nalicz IFT-1 / IFT-1R” | `(Session)` | jw. | `Nalicz…()` |

> `Params` mają ctor `(Context)`; worker `PIT_11Worker` dodatkowo ma ctor `(Session)`. Zaznaczeni
> pracownicy przez `[Context]`.

**Deklaracje płatnika (PIT-4R/PIT-8AR)** są zbiorcze na poziomie podmiotu/oddziału (`PIT4`/`PIT8A`,
`ZbiorczaPIT`) — tworzone/dodawane workerami zbiorczymi (`DodajDoZbiorczejPITWorker`,
`WybierzDeklaracjeDoZbiorczejPITWorker`) i przeliczane `DeklaracjaWorker.Przelicz()` (J2) lub
dedykowanymi `…PrzeliczWorker` (np. `PITR.PrzeliczWorker`, `PIT8S.PrzeliczWorker`).

**Snippet (naliczenie PIT-11 dla zaznaczonych pracowników):**

```csharp
var pracownicy = new[] { session.GetKadry().Pracownicy.WgKodu["006"] };

var worker = new Soneta.Deklaracje.PIT.NaliczanieSeryjne.PIT_11Worker(session)
{
    Pracownicy = pracownicy,
};
worker.Pars.Okres = FromTo.Year(2025);     // rok podatkowy
worker.Pars.Data  = Date.Today;
worker.Nalicz_PIT_11();                     // tworzy wiersze PIT11 w tabeli Deklaracje
session.Save();
```

**Pułapki:**
- Naliczenie PIT bazuje na naliczonych wypłatach (H) i bilansach otwarcia PIT (J6) — bez danych
  źródłowych deklaracja będzie zerowa.
- Sygnatury `Params` PIT mają ctor `(Context)`; `PIT_11Worker` ma też ctor `(Session)` — w teście
  użyj `(session)` + ustaw `Pracownicy`/`Pars`.
- **E-wysyłka PIT to `EDeklaracja`/`ETransmisja` (bramka MF) — NIE testować.** Samo naliczenie
  wiersza PIT jest lokalne (zapis do bazy).

---

### J4 — Deklaracje PFRON (Wn-D, INF-2, DEK-R, INF-D-P)

**Cel:** utworzyć/naliczyć deklaracje PFRON. Typy (`Soneta.Deklaracje.PFRON`, wiersze tabeli
`Deklaracje`): `WN_D` (Wn-D — wniosek o dofinansowanie), `WN_U` (Wn-U), `INF_D`/`INF_D_P`
(informacje o pracownikach niepełnosprawnych — załączniki do Wn-D), `INF_2` (informacja roczna),
`DEK_R` (deklaracja roczna wpłat).

**Workery:**
- `Soneta.Deklaracje.DeklaracjaWorker` — akcja **„Przelicz”** (`Przelicz()`) dla każdego z typów PFRON
  (są DataType `Deklaracja`).
- `Soneta.Deklaracje.PFRON.INF_D.InfoWorker`, `…INF_D_P.InfoWorker` — properties informacyjne (UI).
- **E-wysyłka SOD (NIE testować):** `Soneta.Deklaracje.UI.SODEksportForm.EksportWorker` (DataType
  `WN_D`/`WN_U`/`INF_D`) — eksport do systemu SODiR.

**Dane źródłowe** PFRON pochodzą z `PracHistoria.PFRON` (A13: stopień niepełnosprawności, efekt
zachęty, schorzenia SOD) — bez nich deklaracja będzie pusta.

**Pułapki:**
- PFRON nie ma dedykowanego „NaliczanieSeryjne” na `Pracownicy` — deklarację (`WN_D` itd.) tworzy się
  w module Deklaracje, a przelicza `DeklaracjaWorker.Przelicz()`. Tworzenie/edycja wymaga `Context`.
- Konfiguracja procentów/odpisu PFRON to workery na `OddzialFirmy`
  (`Soneta.Deklaracje.Config.*PFRON*Worker`) — to dane konfiguracyjne, nie deklaracje.

---

### J5 — Operacje PPK

**Cel:** obsłużyć cykl życia uczestnictwa w PPK — kwalifikacja/auto-zapis, rejestracja uczestnika,
rezygnacja, wznowienie, zmiana danych, zakończenie zatrudnienia, dokumenty i rozliczenie składek.
Typy dokumentów PPK (`Soneta.Deklaracje.PPK`, wiersze tabeli `Deklaracje`): `RejestracjaUczestnikaPPK`,
`DeklaracjaUczestnikaPPK`, `ZmianaDanychIdentyfikacyjnychUczestnikaPPK`,
`ZmianaDanychKontaktowychUczestnikaPPK`, `ZakończenieZatrudnieniaUczestnikaPPK`, `TransferPPK`,
`WypłataTransferowaPPK`, `WypłataŚrodkówPrzezUczestnikaPPK`, `ZwrotŚrodkówPPK`, `RozliczenieSkładekPPK`,
`RozliczenieNadpłatPPK`, `ZwrotNadpłatyPPK`, `NadanieUczestnikowiNumeruPPK`,
`DokumentyPracodawcyPPK`, `DokumentyInstytucjiFinansowejPPK`.

**Workery operacji PPK — poziom `Pracownicy` (zagnieżdżone `Soneta.Deklaracje.PPK.DeklaracjePPKPracownikówWorker`),
wspólny `Params = DeklaracjePPKBaseWorker.Params` (`Okres: FromTo`, `DokumentPPK: DokumentyPracodawcyPPK`):**

| Worker (akcja) | Metoda |
|---|---|
| `…Worker.Rejestracja` — „Operacje PPK/Rejestracja uczestnika” | `object RejestracjaPracownikow()` |
| `…Worker.Rezygnacja` — „Operacje PPK/Rezygnacja uczestnika” | `object RezygnacjaPracownikow()` |
| `…Worker.Wznowienie` — „Operacje PPK/Automatyczne wznowienie uczestnictwa” | `object WznowieniePracownikow()` |
| `…Worker.ZakończenieZatrudnienia` — „Operacje PPK/Zakończenie zatrudnienia uczestnika” | `object ZakończenieZatrudnieniaPracownikow()` |
| `…Worker.ZmianaDanychIdentyfikacyjnych` — „Operacje PPK/Zmiana danych identyfikacyjnych” | `object ZmianaDanychIdentyfikacyjnychPracownikow()` |

> Przystąpienie/auto-zapis i zmiana procentu składki realizowane są na poziomie **pracownika**
> (dane PPK pracownika), nie tymi workerami zbiorczymi.

**Workery na pracowniku (kwalifikacja PPK) — `Soneta.Kadry.Pracownik`:**

| Worker | Ctor | Wybrane pola/props |
|---|---|---|
| `Pracownik.PPKWorker` (alias `PPK`) | `(Context context)` | `Data: Date`, `Idx: Pracownik`; `Kwalifikacja: PPKWorker.RodzajZgłoszenia`, `DataKwalifikacji[/Min/Max]: Date`, `Kwalifikacja[Min/Max]` |
| `Pracownik.AutoZapisPPKWorker` (alias `AutoZapisPPK`) | `(Context context)` | `Data: Date`, `Pracownik: Pracownik`; `Kwalifikacja: AutoZapisPPKWorker.CzyAutoZapisPPK` |

> Te workery służą do **odczytu kwalifikacji** (czy/kiedy pracownik podlega przystąpieniu lub
> auto-zapisowi do PPK na dany dzień) — mają ctor `(Context)`.

**Przeliczanie/rozliczenie PPK:**
- `Soneta.Deklaracje.PPK.PrzeliczPPKWorker` (DataType m.in. `RozliczenieNadpłatPPK`,
  `WypłataTransferowaPPK`, `WypłataŚrodkówPrzezUczestnikaPPK`, `ZwrotŚrodkówPPK`,
  `NadanieUczestnikowiNumeruPPK`) — przelicza dokument rozliczeniowy PPK.
- `Soneta.Deklaracje.PPK.NadanieNumeruPPKWorker` (DataType `NadanieUczestnikowiNumeruPPK`).
- `RozliczenieSkładekPPK` / `RejestracjaUczestnikaPPK` / `DeklaracjaUczestnikaPPK` przeliczane przez
  `DeklaracjaWorker.Przelicz()` (J2, DataType `Deklaracja`).

**E-wysyłka / import-eksport PPK (NIE testować):**
- `Soneta.Deklaracje.PPK.DokumentyPPKEksportWorker` (DataType `DokumentyPracodawcyPPK`,
  `DokumentyInstytucjiFinansowejPPK`) — eksport do instytucji finansowej.
- `Soneta.Deklaracje.PPK.DokumentyPPKImportWorker` (DataType `DokumentyInstytucjiFinansowejPPK`) —
  import zwrotny.

**Snippet (rejestracja uczestnika PPK dla zaznaczonych):**

```csharp
var pracownicy = new[] { session.GetKadry().Pracownicy.WgKodu["006"] };

var pars = new Soneta.Deklaracje.PPK.DeklaracjePPKBaseWorker.Params(context)
{
    Okres = FromTo.Year(2026),
    // DokumentPPK = … (DokumentyPracodawcyPPK z modułu Deklaracje)
};
var rej = new Soneta.Deklaracje.PPK.DeklaracjePPKPracownikówWorker.Rejestracja
{
    Pracownicy = pracownicy,
    Pars       = pars,
};
rej.RejestracjaPracownikow();   // tworzy dokumenty rejestracji uczestnika PPK
session.Save();
```

**Pułapki:**
- Zmiana procentu składki PPK / przystąpienie to dane **pracownika** (deklaracja uczestnika PPK,
  `DeklaracjaUczestnikaPPK`) — workery zbiorcze obejmują rejestrację, rezygnację, wznowienie, zmianę
  danych identyfikacyjnych i zakończenie zatrudnienia.
- `DeklaracjePPKBaseWorker.Params` ma ctor `(Context)`; operacja jest lokalna (tworzy wiersze
  dokumentów PPK), ale niewykonalna bez `Context` i zwykle `DokumentPPK`.
- `PPKWorker`/`AutoZapisPPKWorker` na pracowniku są **diagnostyczne** (kwalifikacja na dzień), nie
  tworzą dokumentów — i wymagają `Context`.

---

### J6 — Bilanse otwarcia deklaracji (PIT, ZUS, ERP-7) przy wdrożeniu

**Cel:** wprowadzić dane historyczne sprzed startu systemu, potrzebne do poprawnego naliczenia
deklaracji w pierwszym okresie. Bilanse są **kolekcjami na pracowniku** (`SubTable`) — tworzy się je
i odczytuje czystym API biznesowym, **bez `Context`/`KEDU`/sieci**.

**Kolekcje na `Soneta.Kadry.Pracownik`:**

| Kolekcja | Typ | Przeznaczenie |
|---|---|---|
| `Pracownik.BilansyOtwarciaPIT` | `SubTable<Soneta.Place.BilansOtwarciaPIT>` | bilans otwarcia PIT (przychody/koszty/składki na start) |
| `Pracownik.WynagrodzeniaERP7` | `SubTable<Soneta.Kalend.WynagrodzenieERP7>` | wynagrodzenia do ERP-7 / Z-3 |
| `Pracownik.NieobecnosciERP7` | `SubTable<Soneta.Kalend.NieobecnoscERP7>` | nieobecności do ERP-7 / Z-3 |
| `Pracownik.DeklaracjePodmiotu` | `SubTable` | deklaracje powiązane z pracownikiem-podmiotem |

**Typ `Soneta.Place.BilansOtwarciaPIT`** (root `GuidedRow`, tabela `BilansyOtwPIT`) jest
**ABSTRAKCYJNY** — instancjonuje się jedną z konkretnych wersji odpowiadających wartościom enuma
`Soneta.Place.WersjaBilansuOtwarciaPIT` (`PIT11_11`, `PIT11_29`):
`Soneta.Place.BilansOtwarciaPIT_11` (Wersja = `PIT11_11`) lub `Soneta.Place.BilansOtwarciaPIT_29`
(Wersja = `PIT11_29`). Konkretne klasy mają publiczny ctor `(Pracownik pracownik)`; bazowy
`BilansOtwarciaPIT` ma ctor `(Pracownik, WersjaBilansuOtwarciaPIT)`, ale jest abstrakcyjny.
Property `Pracownik` i `Wersja` są **read-only** (ustawiane przez ctor; brak ctora bezparametrowego).
Pola bazodanowe m.in.: `Data: Date`, kwoty przychodów/kosztów/składek w rozbiciu
etat/umowa/macierzyński (`Przychod26ZwolEtat`, `Przychod26ZwolUmowa`, `PrzychodUlgaEtat`,
`PrzychodUlgaUmowa`, `Spoleczne`, `Spoleczne26`, `Zdrowotne9Procent`, `SkladkiCzlonkowskie` itd.)
oraz kolekcja `Elementy: SubTable<Soneta.Place.ElementBilansuOtwarciaPIT>`.

**ERP-7** (wcześniej druk ZUS Rp-7) opiera się na `WynagrodzeniaERP7`/`NieobecnosciERP7` pracownika
oraz zaświadczeniach `Soneta.Deklaracje.ZUS.Z3`/`Z3a` (workery `ZUSZ3.Z3Worker`/`Z3aWorker` na
`Nieobecnosc`) — sam druk Z-3/ERP-7 to generowanie dokumentu w module Deklaracje.

**Snippet (dodanie bilansu otwarcia PIT i odczyt):**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];

using (var t = session.Logout(editMode: true))
{
    // BilansOtwarciaPIT jest abstrakcyjny — tworzymy konkretną wersję (_29 => PIT11_29, _11 => PIT11_11).
    // Pracownik ustawia ctor (property read-only), więc NIE używamy inicjalizatora obiektu na Pracownik.
    var bo = session.AddRow(new Soneta.Place.BilansOtwarciaPIT_29(pracownik));
    bo.Data            = new Date(2026, 1, 1);
    bo.PrzychodUlgaEtat = 12000m;
    bo.Spoleczne        = 1645.20m;
    t.Commit();
}
session.Save();

// Odczyt bilansów otwarcia PIT pracownika (typ kolekcji: SubTable<BilansOtwarciaPIT>):
foreach (Soneta.Place.BilansOtwarciaPIT bo in pracownik.BilansyOtwarciaPIT)
{
    // bo.Data, bo.PrzychodUlgaEtat, bo.Spoleczne, bo.Wersja
}
```

**Pułapki:**
- `BilansOtwarciaPIT` ma kolekcję `Elementy` — niektóre kwoty są wyliczane z elementów; sprawdź na
  Demo, czy ustawiasz pola root, czy elementy.
- Bilanse są **danymi wdrożeniowymi** (jednorazowe na start) — nie myl z naliczonymi deklaracjami.
- ERP-7 (Z-3/Z-3a) wymaga modułu Deklaracje i `KEDU`/PUE do eksportu — samo wprowadzenie
  `WynagrodzeniaERP7`/`NieobecnosciERP7` jest lokalne, ale wygenerowanie druku — nie.

## K. Ewidencje pracownicze

> **Wzorzec wspólny.** Wszystkie ewidencje pracownicze to **kolekcje `SubTable` na rootcie
> `Pracownik`** (nie na `PracHistoria`). Każdy element jest osobnym `GuidedRow` (child pracownika)
> z polem `Pracownik: Soneta.Kadry.Pracownik` ustawianym automatycznie przez konstruktor
> `new Xxx(pracownik)`. Schemat dodania jest jednolity:
>
> ```csharp
> using (var t = session.Logout(editMode: true)) {
>     var wpis = session.AddRow(new Xxx(pracownik));   // ctor wiąże wpis z pracownikiem
>     // ... ustaw pola ...
>     t.Commit();                                       // Commit() w kodzie biznesowym
> }
> session.Save();
> ```
>
> `session.AddRow(new Xxx(pracownik))` i `pracownik.Kolekcja.AddRow(new Xxx(pracownik))` są
> równoważne — wpis trafia do tej samej tabeli i do `SubTable` pracownika. Większość typów wymaga
> wskazania **definicji** (rekord słownikowy, tabela konfiguracyjna) — definicję pobierasz przez
> `WgNazwy[...]` z odpowiedniego modułu, **nie** tworzysz jej w teście operacyjnym.

| Ewidencja | Kolekcja na `Pracownik` | Typ elementu | Tabela |
|---|---|---|---|
| K1 Badania lekarskie | `BadaniaLekarskie: SubTable<BadanieLekarskie>` | `Soneta.Kadry.BadanieLekarskie` | `BadaniaLekarskie` |
| K2 Szkolenia BHP | `SzkoleniaBHP: SubTable<SzkolenieBHP>` | `Soneta.Kadry.SzkolenieBHP` | `SzkoleniaBHP` |
| K3 Wnioski o szkolenia | `WnioskiOSzkolenia: SubTable<WniosekOSzkolenie>` | `Soneta.HR.WniosekOSzkolenie` | `WnioskiOSzkol` |
| K3 Ukończone szkolenia | `UkończoneSzkolenia: SubTable<UkończoneSzkolenie>` | `Soneta.HR.UkończoneSzkolenie` | `UkonczSzkolenia` |
| K3 Uprawnienia | `Uprawnienia: SubTable<UprawnieniePracownika>` | `Soneta.HR.UprawnieniePracownika` | `UprawnieniaPrac` |
| K4 Nagrody i kary | `NagrodyKary: SubTable<NagrodaKara>` | `Nagroda` / `Kara` (`NagrodaKara` abstr.) | `NagrodyKary` |
| K4 Oświadczenia | `Oświadczenia: SubTable<OświadczeniePracownika>` | `Soneta.Kadry.OświadczeniePracownika` | `OswiadczeniaPrac` |
| K5 Wypadki przy pracy | `Wypadki: SubTable<Wypadek>` | `Soneta.Kadry.Wypadek` | `Wypadki` |

---

### K1 — Badania lekarskie

**Cel:** zarejestrować badanie lekarskie pracownika (wstępne/okresowe/kontrolne) wraz z terminami
ważności i datą następnego badania; ewentualnie wykonać operację seryjną dla grupy osób.

**Mechanizm:** `BadanieLekarskie` ma publiczny konstruktor `BadanieLekarskie(Pracownik pracownik)`.
Wpis wymaga `Definicja: DefinicjaBadaniaLekarskiego` (słownik, tabela konfiguracyjna `DefBadanLek`,
pobierana przez `WgNazwy[...]`). Jeśli definicja jest **cykliczna** (`Definicja.Cykliczne == true`,
ma `NastepneDefinicja`/`NastepneTermin`), platforma wylicza termin kolejnego badania —
udostępniony jako wyliczane `NastępneTermin`/`NastępneDefinicja`.

**Pola i typy (rekord `BadanieLekarskie`):**

| Pole | Typ | Uwaga |
|---|---|---|
| `Definicja` | `Soneta.Kadry.DefinicjaBadaniaLekarskiego` | wymagana; słownik `DefBadanLek` |
| `Data` | `Soneta.Types.Date` | data wykonania badania |
| `Termin` | `Soneta.Types.Date` | termin badania — **read-only** (wyliczany z `Data`+definicji); ustawienie rzuca `ColReadOnlyException` |
| `WazneDo` | `Soneta.Types.Date` | „Ważne do" — koniec ważności (ustawialny) |
| `PracaWOkularach` | `bool` | adnotacja medyczna |
| `KwotaDofinansowania` | `decimal`, `DataDofinansowania: Date` | dofinansowanie badania |
| `Opis` | `Soneta.Business.MemoText` | opis/uwagi |
| `Anulowany` | `bool` | flaga anulowania |
| `Pracownik` | `Soneta.Kadry.Pracownik` | ustawiany przez ctor |
| `NastępneTermin`, `NastępneDefinicja`, `Następne` | (wyliczane) | termin/def./wpis następnego badania |

**Manager:** `pracownik.Badania: Pracownik.BadaniaLekarskieManager` — pomocnik tylko do odczytu;
`pracownik.Badania.ZNajkrótszymTerminem(definicja = null): BadanieLekarskie` zwraca badanie z
najbliższym terminem wygaśnięcia (do raportów „badania okresowe do wykonania").

**Snippet:**

```csharp
var kadry = session.GetKadry();
var pracownik = kadry.Pracownicy.WgKodu["006"];
var definicja = kadry.DefBadanLek.WgNazwy["Wstępne"];     // słownik konfiguracyjny

using (var t = session.Logout(editMode: true))
{
    var badanie = session.AddRow(new BadanieLekarskie(pracownik));  // ctor wiąże z pracownikiem
    badanie.Definicja = definicja;
    badanie.Data      = Date.Today;
    // UWAGA: badanie.Termin jest read-only (wyliczany) — NIE ustawiaj go ręcznie.
    badanie.WazneDo   = new Date(Date.Today.Year + 2, Date.Today.Month, Date.Today.Day);

    t.Commit();
}
session.Save();
```

**Operacja seryjna (grupa pracowników):** w warstwie UI istnieje worker
`DodajBadaniaLekarskieWorker` (warianty `ZListyBadań`, `ZListyPracowników`) z akcją menu
„Operacje seryjne/Dodaj badania lekarskie" — iteruje po wybranych pracownikach i dla każdego robi
`new BadanieLekarskie(pracownik)` + `BadaniaLekarskie.AddRow(...)`. W kodzie biznesowym
seryjność realizujesz tą samą pętlą `foreach (var p in wybrani) { … AddRow … }` w jednej transakcji.

**Pułapki:**
- `Definicja` jest **wymagana** — bez niej `Save()` rzuci `RowException`.
- `Data`/`WazneDo` to `Soneta.Types.Date`, nie `DateTime`. `Termin` jest **read-only** (wyliczany) —
  próba ustawienia rzuca `ColReadOnlyException`. Reguła w weryfikatorach: `WazneDo` nie może być
  wcześniejsze niż `Termin`; termin następnego badania musi być **późniejszy** niż termin badania
  bieżącego — naruszenie wybucha jako `RowException` przy zapisie.
- `pracownik.Badania` to manager (odczyt), a kolekcją CRUD jest `pracownik.BadaniaLekarskie`
  (`SubTable<BadanieLekarskie>`). Nie myl tych dwóch.

---

### K2 — Szkolenia BHP

**Cel:** zarejestrować odbyte szkolenie BHP (wstępne/okresowe) z terminem ważności i datą szkolenia
następnego (analogicznie do badań lekarskich).

**Mechanizm:** konstruktor `SzkolenieBHP(Pracownik pracownik)`; kolekcja `pracownik.SzkoleniaBHP`.
Wymagana `Definicja: DefinicjaSzkoleniaBHP` (słownik konfiguracyjny `DefSzkolenBHP`, `WgNazwy[...]`).
Cykliczność (`Definicja.Cykliczne`) wylicza `NastępneTermin`/`NastępneDefinicja`.

**Pola i typy (rekord `SzkolenieBHP`):**

| Pole | Typ | Uwaga |
|---|---|---|
| `Definicja` | `Soneta.Kadry.DefinicjaSzkoleniaBHP` | wymagana; słownik `DefSzkolenBHP` |
| `Data` | `Soneta.Types.Date` | data szkolenia |
| `Termin` | `Soneta.Types.Date` | termin — **read-only** (wyliczany); ustawienie rzuca `ColReadOnlyException` |
| `WażneDo` | `Soneta.Types.Date` | koniec ważności (wyliczane) |
| `Zakres` | `string` | zakres szkolenia |
| `Osoba` | `string` | prowadzący / osoba szkoląca |
| `Opis` | `Soneta.Business.MemoText` | uwagi |
| `Anulowany` | `bool` | flaga anulowania |
| `Pracownik` | `Soneta.Kadry.Pracownik` | ustawiany przez ctor |
| `NastępneTermin`, `NastępneDefinicja`, `Następne` | (wyliczane) | następne szkolenie |

**Snippet:**

```csharp
var kadry = session.GetKadry();
var pracownik = kadry.Pracownicy.WgKodu["007"];
var definicja = kadry.DefSzkolenBHP.WgNazwy["Wstępne"];

using (var t = session.Logout(editMode: true))
{
    var szkolenie = session.AddRow(new SzkolenieBHP(pracownik));
    szkolenie.Definicja = definicja;
    szkolenie.Data      = Date.Today;
    // UWAGA: szkolenie.Termin jest read-only (wyliczany) — NIE ustawiaj go ręcznie.
    szkolenie.Zakres    = "Instruktaż ogólny";

    t.Commit();
}
session.Save();
```

**Operacja seryjna:** UI udostępnia `DodajSzkolenieBHPWorker` (akcja menu, lista pracowników) —
w kodzie biznesowym pętla `foreach` + `new SzkolenieBHP(p)` + `AddRow` w jednej transakcji.

**Pułapki:**
- `Definicja` wymagana (jak w K1).
- Uwaga na pisownię: pole nazywa się `WażneDo` (z „ż"), a w `BadanieLekarskie` — `WazneDo` (bez).
- `Termin` jest **read-only** (wyliczany) — ustawienie rzuca `ColReadOnlyException`.
- `Termin` następnego szkolenia musi być późniejszy niż bieżący — inaczej `RowException`.

---

### K3 — Szkolenia i uprawnienia (moduł HR/HR2)

**Cel:** obsłużyć cykl rozwoju kompetencji: **wniosek o szkolenie** → **ukończone szkolenie** →
**uprawnienie/certyfikat**, wraz z kosztem i budżetem szkoleń. Typy leżą w module `Soneta.HR`
(`session.GetHR()`).

**K3a — Wniosek o szkolenie** — `WniosekOSzkolenie([Required] Pracownik pracownik)`; kolekcja
`pracownik.WnioskiOSzkolenia`. Pola:

| Pole | Typ | Uwaga |
|---|---|---|
| `Definicja` | `Soneta.HR.DefinicjaSzkolenia` | rodzaj szkolenia (słownik HR) |
| `Etap` | `Soneta.HR.EtapRealizacjiSzkolenia` | np. „Wniosek zatwierdzony" (`hr.EtapRealizSzkol.WgNazwy[...]`) |
| `Realizacja` | `Soneta.HR.RealizacjaSzkolenia` | konkretna realizacja |
| `Budzet` | `Soneta.HR.BudżetSzkoleń` | budżet, z którego finansowane |
| `Koszt` | `Soneta.Types.Currency` | koszt szkolenia |
| `DataZgloszenia`, `Termin`, `DataAnulowania` | `Soneta.Types.Date` | daty cyklu wniosku |
| `Kierownik` | `Soneta.Kadry.Pracownik` | akceptujący |
| `SkierowanyPrzezZaklad` | `bool` | skierowanie pracodawcy |
| `Ocena` | `string`, `Opis: MemoText` | ocena/uwagi |

**K3b — Ukończone szkolenie** — dwa ctory: `UkończoneSzkolenie([Required] Pracownik pracownik)`
oraz `UkończoneSzkolenie(WniosekOSzkolenie wniosek)` (przepina pracownika z wniosku). Kolekcja
`pracownik.UkończoneSzkolenia`. Pola: `Nazwa: string`, `Okres: FromTo`, `Ocena: string`,
`Opis: MemoText`, `Wniosek: WniosekOSzkolenie` (powiązanie).

**K3c — Uprawnienie / certyfikat** — `UprawnieniePracownika([Required] Pracownik pracownik)`;
kolekcja `pracownik.Uprawnienia`. Pola:

| Pole | Typ | Uwaga |
|---|---|---|
| `Definicja` | `Soneta.HR.DefinicjaUprawnienia` | rodzaj uprawnienia |
| `Numer` | `string` | numer uprawnienia/certyfikatu |
| `DataUzyskania`, `DataUtraty`, `TerminWaznosci` | `Soneta.Types.Date` | daty ważności |
| `Okres` | `Soneta.Types.FromTo` | okres obowiązywania |
| `WydanePrzez` | `string` | organ wydający |
| `Zrodlo` | `Soneta.HR.IŹródłoUzyskaniaUprawnienia` | źródło (np. ukończone szkolenie) |

**Snippet (wniosek → koszt z budżetu):**

```csharp
var hr = session.GetHR();
var pracownik = session.GetKadry().Pracownicy.WgKodu["008"];

using (var t = session.Logout(editMode: true))
{
    var wniosek = session.AddRow(new WniosekOSzkolenie(pracownik));
    wniosek.Definicja     = hr.DefinicjeSzkolen.WgNazwy["Kurs zawodowy"];
    wniosek.Etap          = hr.EtapRealizSzkol.WgNazwy["Wniosek zatwierdzony"];
    wniosek.DataZgloszenia = Date.Today;
    wniosek.Koszt          = new Currency(1500m);

    t.Commit();
}
session.Save();
```

**Pułapki:**
- Typy K3 są w `Soneta.HR` (`session.GetHR()`), nie w `Soneta.Kadry`.
- `Etap`/`Definicja` to wpisy słownikowe HR — pobieraj `WgNazwy[...]`, nie twórz w teście.
- `Koszt`/`Budżet` używają `Soneta.Types.Currency` (waluta), nie `decimal`.

---

### K4 — Nagrody i kary; oświadczenia (PIT-2, RODO, zgody)

**K4a — Nagrody i kary.** Klasa bazowa `Soneta.Kadry.NagrodaKara` jest **abstrakcyjna** — używaj
konkretnych podtypów: `Soneta.Kadry.Nagroda(Pracownik)` i `Soneta.Kadry.Kara(Pracownik)`. Oba ctory
delegują do `NagrodaKara(pracownik, TypNagrodyKary)` ustawiając `Typ` na `Nagroda`/`Kara`. Kolekcja
`pracownik.NagrodyKary: SubTable<NagrodaKara>`. Pola:

| Pole | Typ | Uwaga |
|---|---|---|
| `Definicja` | `Soneta.Kadry.DefinicjaNagrodyKary` | słownik `DefNagrodKar`; ma własne pole `Typ` (Nagroda/Kara) — musi zgadzać się z podtypem wpisu, inaczej `set_Definicja` rzuca `ArgumentException`; może nieść `Element`/`Kwota` |
| `Typ` | `Soneta.Kadry.TypNagrodyKary` | `Nagroda`/`Kara` (ustawia ctor podtypu) |
| `Data` | `Soneta.Types.Date` | data nadania |
| `DataAnulowania` | `Soneta.Types.Date` | anulowanie |
| `Rozliczenie` | `Soneta.Kadry.RozliczenieSwiadczenia` (subrow) | `Rozliczenie.Kwota: Currency`, `Rozliczenie.Element: DefinicjaElementu`, `Rozliczenie.Okres: FromTo` — powiązanie z wypłatą |
| `Opis` | `Soneta.Business.MemoText` | treść nagrody/kary |
| `Pracownik` | `Soneta.Kadry.Pracownik` | ustawiany przez ctor |

**K4b — Oświadczenia (PIT-2, RODO, zgody).** `Soneta.Kadry.OświadczeniePracownika` — trzy ctory:
`OświadczeniePracownika([Required] Pracownik pracownik, [Required] DefinicjaOświadczenia definicja)`,
wariant z `Date dataZłożenia`, oraz `(RowCreator)`. Kolekcja `pracownik.Oświadczenia`. Rodzaj
oświadczenia (PIT-2, zgoda RODO, zgoda na e-doręczenia itp.) określa `Definicja` (słownik
konfiguracyjny `DefOswiadczen`). Pola:

| Pole | Typ | Uwaga |
|---|---|---|
| `Definicja` | `Soneta.Kadry.DefinicjaOświadczenia` | wymagana w ctorze; słownik `DefOswiadczen` |
| `DataZlozenia` | `Soneta.Types.Date` | data złożenia |
| `DataWycofania` | `Soneta.Types.Date` | data wycofania |
| `Okres` | `Soneta.Types.FromTo` | okres obowiązywania (z `Definicja.OkresWaznosci`/`OkresIlosc`) |
| `Tresc` | `Soneta.Business.MemoText` | treść |
| `TrescOswiadczenia` | `Soneta.Kadry.TreśćOświadczenia` | treść strukturalna |
| `Pracownik` | `Soneta.Kadry.Pracownik` | ustawiany przez ctor |

**Snippet (nagroda + oświadczenie PIT-2):**

```csharp
var kadry = session.GetKadry();
var pracownik = kadry.Pracownicy.WgKodu["009"];

using (var t = session.Logout(editMode: true))
{
    // nagroda — konkretny podtyp, NIE abstrakcyjna NagrodaKara
    var nagroda = session.AddRow(new Nagroda(pracownik));
    nagroda.Definicja = kadry.DefNagrodKar.WgNazwy["Nagroda uznaniowa"];
    nagroda.Data      = Date.Today;

    // oświadczenie — definicja jest wymagana w konstruktorze
    var defPit2 = kadry.DefOswiadczen.WgNazwy["PIT-2"];
    var oswiadczenie = session.AddRow(new OświadczeniePracownika(pracownik, defPit2, Date.Today));

    t.Commit();
}
session.Save();
```

**Pułapki:**
- **Nie** rób `new NagrodaKara(...)` — typ abstrakcyjny. Używaj `Nagroda`/`Kara`.
- `Definicja` musi mieć **`Typ` zgodny** z podtypem wpisu (`Nagroda` → def. o `Typ==Nagroda`, `Kara` →
  def. o `Typ==Kara`); przypisanie niezgodnej typem definicji rzuca `ArgumentException` w `set_Definicja`.
  Filtruj słownik: `DefNagrodKar.Cast<DefinicjaNagrodyKary>().FirstOrDefault(d => d.Typ == TypNagrodyKary.Nagroda)`.
- `OświadczeniePracownika` **nie ma** ctora samego `(Pracownik)` — definicja jest `[Required]`
  w konstruktorze; bez niej kod się nie skompiluje.
- `Rozliczenie.*` na nagrodzie/karze to subrow powiązania z wypłatą (`Currency`, `DefinicjaElementu`)
  — wypełniane przy rozliczaniu w płacach, nie przy samym wpisie.

---

### K5 — Wypadki przy pracy

**Cel:** zarejestrować wypadek przy pracy wraz z dokumentacją powypadkową (protokół, decyzja,
okoliczności, skutki) i ewentualnym świadczeniem.

**Mechanizm:** `Soneta.Kadry.Wypadek(Pracownik pracownik)`; kolekcja `pracownik.Wypadki`. Wpis jest
numerowany (`Numer: Soneta.Core.NumerDokumentu`) i wymaga `Definicja: Soneta.Core.DefinicjaDokumentu`
(definicja dokumentu wypadku).

**Pola i typy (rekord `Wypadek`):**

| Pole | Typ | Uwaga |
|---|---|---|
| `Definicja` | `Soneta.Core.DefinicjaDokumentu` | definicja dokumentu (numeracja) |
| `Numer` | `Soneta.Core.NumerDokumentu` (subrow) | `Numer.Pelny`, `Numer.Symbol`, `Numer.Numer` |
| `Data` | `Soneta.Types.Date` | data wypadku |
| `Godzina` | `Soneta.Types.Time` | godzina wypadku |
| `DataZgloszenia` | `Soneta.Types.Date` | data zgłoszenia |
| `Miejsce` | `string` | miejsce wypadku |
| `Rodzaj` | `Soneta.Kadry.RodzajWypadku` | klasyfikacja wypadku |
| `PrzyPracy`, `Ciezki`, `Smiertelny`, `Niezdolnosc` | `bool` | kwalifikacja skutków |
| `Okolicznosci`, `Skutki`, `Odmowa` | `Soneta.Business.MemoText` | dokumentacja opisowa |
| `ProtokolNumer`, `ProtokolData` | `string` / `Date` | protokół powypadkowy |
| `DecyzjaNumer`, `DecyzjaData` | `string` / `Date` | decyzja |
| `PismoNumer`, `PismoData` | `string` / `Date` | pismo |
| `SKW` | `string` | statystyczna karta wypadku |
| `Kwota` | `decimal` | kwota świadczenia |
| `PracHistoria` | `Soneta.Kadry.PracHistoria` | (wyliczane) zapis kadrowy na datę |
| `Pracownik` | `Soneta.Kadry.Pracownik` | ustawiany przez ctor |

**Snippet:**

```csharp
var kadry = session.GetKadry();
var pracownik = kadry.Pracownicy.WgKodu["010"];

using (var t = session.Logout(editMode: true))
{
    var wypadek = session.AddRow(new Wypadek(pracownik));
    wypadek.Data          = Date.Today;
    wypadek.Godzina       = new Time(10, 30);
    wypadek.DataZgloszenia = Date.Today;
    wypadek.Miejsce       = "Hala produkcyjna";
    wypadek.PrzyPracy     = true;
    wypadek.Okolicznosci  = new MemoText("Poślizgnięcie na mokrej posadzce.");

    t.Commit();
}
session.Save();
```

**Pułapki:**
- `Numer` jest subrowem nadawanym wg `Definicja` (numeracja) — nie ustawiaj `Numer.Pelny` ręcznie,
  numer nadaje platforma; gdy `Definicja` ma własną numerację, podpięcie definicji wystarcza.
- `Godzina` to `Soneta.Types.Time`, `Data` to `Soneta.Types.Date` — nie `DateTime`.
- Pola opisowe (`Okolicznosci`, `Skutki`, `Odmowa`) to `MemoText`, nie `string`.

### K6 — RODO/GIODO: oświadczenia, uprawnienia do przetwarzania, wymiana danych

**Cel:** ewidencjonować zgody/oświadczenia RODO pracownika, uprawnienia do przetwarzania danych
osobowych oraz fakty wymiany danych (pozyskanie / udostępnienie / powierzenie). Pracownik jest
hostem GIODO — implementuje `IGIODOOświadczenieHost`, `IGIODOUprawnienieHost`, `IGIODOWymianaDanychHost`,
`IGIODOZgodnyHost`. Zapis „teczki" personalnej do pliku jest operacją plikową (poza zakresem testów).

**Publiczny kontrakt — kolekcje na `Pracownik` (moduł `Soneta.Core`):**

| Kolekcja | Typ elementu | Zawartość |
|---|---|---|
| `GIODOOświadczenia` | `SubTable<Soneta.Core.GIODOOświadczenie>` | oświadczenia / zgody RODO |
| `GIODOUprawnienia` | `SubTable<Soneta.Core.GIODOUprawnienie>` | uprawnienia do przetwarzania danych |
| `GIODOUdostępnienia` | `SubTable<Soneta.Core.GIODOWymianaDanych>` | pozyskanie / udostępnienie / powierzenie danych |
| `PotwierdzeniaGIODO` | `SubTable<Soneta.Core.GIODOZgodny>` | potwierdzenia zgodności; `ZgodnoscGIODOPotwierdzona: bool` (kalkulowane) |

**`GIODOOświadczenie` (tabela `GIODOOswiadcz`, root) — pola bazodanowe:**

| Pole | Typ | Uwaga |
|---|---|---|
| `Host` | `IGIODOOświadczenieHost` | składający oświadczenie (= `Pracownik`) |
| `Definicja` | `Soneta.Core.GIODODefinicjaOświadczenia` | **referencja konfiguracyjna** (wymagana przez ctor) |
| `Data` | `Soneta.Types.Date` | data oświadczenia (zapisywalne) |
| `Okres` | `Soneta.Types.FromTo` | okres obowiązywania zgody — **read-only** (wyliczane z definicji) |
| `Rodzaj` | `Soneta.Core.RodzajeOświadczeńGIODO` | `Oświadczenie`, `UdzielenieZgody`, `WycofanieZgody` — **read-only** (wynika z definicji) |
| `Oswiadczenie` | `bool` | flaga oświadczenia |
| `Tresc` | `Soneta.Business.MemoText` | treść |
| `SposobPozyskania` | `string` | — |
| `DataWycofaniaZgody` | `Soneta.Types.Date` | — |
| `WycofanieZgody` | `GIODOOświadczenie` | powiązanie z zapisem wycofania |
| `Bufor` | `bool` | zatwierdzenie |

Ctor: `new GIODOOświadczenie(IGIODOOświadczenieHost host, GIODODefinicjaOświadczenia definicja)`.

**`GIODOUprawnienie` (tabela `GIODOUprawnienia`, root) — pola bazodanowe:**

| Pole | Typ | Uwaga |
|---|---|---|
| `Uprawniony` | `IGIODOUprawnienieHost` | = `Pracownik` |
| `Definicja` | `Soneta.Core.GIODODefinicjaUprawnienia` | **referencja konfiguracyjna** (wymagana przez ctor) |
| `Data`, `Przyznane`, `Odebrane` | `Soneta.Types.Date` | data zapisu / od kiedy przyznane / od kiedy odebrane |
| `Okres` | `Soneta.Types.FromTo` | okres przyznania |
| `Tresc` | `Soneta.Business.MemoText` | — |
| `WycofanieUprawnienia` | `GIODOUprawnienie` | powiązanie z wycofaniem |

Ctor: `new GIODOUprawnienie(IGIODOUprawnienieHost uprawniony, GIODODefinicjaUprawnienia definicja)`.

**`GIODOWymianaDanych` (tabela `GIODOWymDanych`, root) — pola bazodanowe:**

| Pole | Typ | Uwaga |
|---|---|---|
| `Host` | `IGIODOWymianaDanychHost` | = `Pracownik` |
| `Kierunek` | `Soneta.Core.GIODOKierunekWymianyDanych` | `Powierzenie`, `Pozyskanie`, `PowierzenieZbioru`, `PozyskanieZbioru`, `Udostępnienie` |
| `Podmiot` | `Soneta.Core.IKontrahent` | druga strona wymiany |
| `Data` | `Soneta.Types.Date` | data wymiany |
| `Zakres` | `Soneta.Business.MemoText` | zakres danych |
| `SposobPozyskania` | `string` | — |
| `PozyskaneOdOsoby`, `UdostepnioneOsobie`, `NaWniosekOsoby`, `TylkoDostep` | `bool` | flagi |
| `Definicja` | `Soneta.Core.DefinicjaDokumentu` | def. numeracji dokumentu |
| `ZbiorDanych` | `Soneta.Core.GIODO.GIODOZbiorDanych` | zbiór danych |

`GIODOWymianaDanych` **nie ma publicznego konstruktora** — rekordy tworzą wyłącznie workery poniżej
(zwracają konkretne podtypy `GIODOPozyskanieDanych` / `GIODOUdostępnienieDanych` / `GIODOPowierzenieDanych`).

**Workery RODO (jedyna droga dodania przez API; klasy zagnieżdżone w `Soneta.Kadry.Pracownicy`):**

| Worker | Metoda | Zwraca | Parametry (`Pars` / `Params`) |
|---|---|---|---|
| `Pracownicy.DodajOświadczeniaWorker` | `GIODOOświadczenie DodajOświadczenia()` | oświadczenie | `Pars`: `Definicja: GIODODefinicjaOświadczenia`, `Data`, `Oddział`, `SposobPozyskania`, `Zatwierdź: bool` |
| `Pracownicy.DodajUprawnieniaWorker` | `GIODOUprawnienie DodajUprawnienia()` | uprawnienie | `Pars`: `Definicja: GIODODefinicjaUprawnienia`, `Data`, `Przyznane`, `Odebrane`, `Oddział`, `Zatwierdź: bool` |
| `Pracownicy.DodajPozyskanieDanychWorker` | `GIODOPozyskanieDanych DodajPozyskanieDanych()` | wymiana (pozyskanie) | `Pars`: `Podmiot: IKontrahent`, `Data`, `Zakres: string`, `Oddział`, `SposobPozyskania`, `Zatwierdź: bool` |
| `Pracownicy.DodajUdostępnienieDanychWorker` | `GIODOUdostępnienieDanych DodajUdostępnienieDanych()` | wymiana (udostępnienie) | `Pars`: `Podmiot: IKontrahent`, `Data`, `Zakres: string`, `Oddział`, `Zatwierdź: bool` |
| `Pracownicy.DodajPowierzenieDanychWorker` | `GIODOPowierzenieDanych DodajPowierzenieDanych()` | wymiana (powierzenie) | `Pars` (analogicznie) |

Wszystkie workery RODO mają bezparametrowy ctor oraz property `Hosts: Pracownik[]` (`[Context]`, lista
pracowników, których dotyczy operacja) i `Session`.

**Zapis teczki personalnej do pliku — `Pracownik.ZapiszTeczkęDoPlikuWorker`** (akcja
„Teczka.../Zapisz teczkę do pliku", metoda `ZapiszTeczkeDoPliku()`, property `Param`) — to
**operacja plikowa** (serializacja dokumentacji do plików XML/katalogu na dysku). **Poza zakresem
testów jednostkowych → `[Ignore]`** (zależność od systemu plików).

**Snippet (dodanie oświadczenia GIODO workerem):**

```csharp
var kadry = session.GetKadry();
var pracownik = kadry.Pracownicy.WgKodu["006"];

// Definicja oświadczenia z konfiguracji (musi istnieć w bazie):
var defOswiadczenia = session.ExecuteConfig(s =>
    s.GetCore().GIODODefinicjeOświadczeń.WgNazwy["Zgoda na przetwarzanie danych"]);

using (var t = session.Logout(editMode: true))
{
    var worker = new Pracownik.Pracownicy.DodajOświadczeniaWorker { Hosts = new[] { pracownik } };
    worker.Pars.Definicja = session.Get(defOswiadczenia);
    worker.Pars.Data = Date.Today;
    worker.Pars.Zatwierdź = true;
    GIODOOświadczenie oswiadczenie = worker.DodajOświadczenia();
    t.CommitUI();
}
session.Save();

// Odczyt oświadczeń pracownika:
foreach (GIODOOświadczenie o in pracownik.GIODOOświadczenia)
{
    // o.Definicja, o.Okres, o.Rodzaj, o.Data
}
```

**Snippet (dodanie oświadczenia bez workera — bezpośrednim ctorem):**

```csharp
using (var t = session.Logout(editMode: true))
{
    var o = session.AddRow(new GIODOOświadczenie(pracownik, session.Get(defOswiadczenia)));
    // host i Definicja wynikają z ctora; Rodzaj/Okres są WYLICZANE (read-only) z definicji — nie ustawiaj ich.
    o.Data = Date.Today;
    o.SposobPozyskania = "Formularz papierowy";
    t.Commit();
}
session.Save();
```

**Pułapki:**
- `GIODOOświadczenie`/`GIODOUprawnienie` wymagają **referencji do definicji konfiguracyjnej**
  (`GIODODefinicjaOświadczenia` / `GIODODefinicjaUprawnienia`) — pobierz istniejący rekord
  (`ExecuteConfig`), nie twórz „w locie". Bez definicji w bazie scenariusz wymaga uprzedniej
  konfiguracji modułu RODO/GIODO.
- `GIODOWymianaDanych` **nie ma publicznego ctora** — dodawaj wyłącznie workerami
  `DodajPozyskanieDanychWorker` / `DodajUdostępnienieDanychWorker` / `DodajPowierzenieDanychWorker`.
- Workery RODO modyfikują dane i są uruchamiane „jak z UI" → transakcja edycyjna + `CommitUI()` +
  `Save()`. `Hosts`/`Podmiot` muszą pochodzić z bieżącej sesji (safe-code §2.1).
- Obowiązywanie zgody jest „na dzień" — czytaj `Okres`/`Data`, nie zakładaj bezterminowości.
- Dane wrażliwe (treść oświadczeń, podmioty) — nie loguj nadmiarowo (safe-code §12).
- Workery RODO wymagają praw do obszaru GIODO; w teście biznesowym egzekucji praw nie sprawdzamy
  (safe-code §7.2).

### K7 — Struktura organizacyjna: przypisanie do wydziału/struktury, powiązania

**Cel:** przypisać pracownika do jednostki organizacyjnej (wydziału) oraz do elementów struktury
organizacyjnej (np. stanowiska w strukturze, relacje przełożony–podwładny). Wydział wynika z warunków
etatu (`Etat.Wydzial`, historyczne — patrz sekcja B), a powiązania ze strukturą trzyma osobna kolekcja.

**Publiczny kontrakt:**

| Składnik | Typ | Rodzaj | Uwaga |
|---|---|---|---|
| `Etat.Wydzial` | `Soneta.Kadry.Wydzial` | bazodanowe (na `PracHistoria.Etat`) | jednostka organizacyjna; korzeń: `session.GetKadry().Wydzialy.Firma` (zmiana „od daty" — A14) |
| `PowiązaniaStrOrg` | `SubTable<Soneta.Core.PowiązanieStrukturyOrganizacyjnej>` | kolekcja na `Pracownik` | powiązania ze strukturą organizacyjną |
| `StrukturaOraganizacyjna` | `Pracownik.StrukturaOraganizacyjnaManager` | manager (read-only API) | nawigacja przełożeni/podwładni |
| Pracownik implementuje | `IŹródłoPowiązaniaStrukturyOrganizacyjnej` | interfejs | jest źródłem powiązań |

**`PowiązanieStrukturyOrganizacyjnej` (tabela `PowiazaniaStrOrg`, child przez `Zrodlo`) — pola:**

| Pole | Typ | Uwaga |
|---|---|---|
| `Zrodlo` | `IŹródłoPowiązaniaStrukturyOrganizacyjnej` | guided-parent (= `Pracownik`) |
| `Element` | `Soneta.Core.ElementStrukturyOrganizacyjnej` | referencja do **instancji** elementu struktury z tabeli `CoreModule.ElementyStrOrg` (NIE z `DefElStrukturOrg`, która trzyma `DefinicjaElementuStrukturyOrganizacyjnej`); `ElementStrukturyOrganizacyjnej` nie ma publicznego ctora — pobierz istniejący rekord |
| `Okres` | `Soneta.Types.FromTo` | okres obowiązywania powiązania (zapisywalne) |

Ctor: `new PowiązanieStrukturyOrganizacyjnej(ElementStrukturyOrganizacyjnej element, IŹródłoPowiązaniaStrukturyOrganizacyjnej zrodlo)`.

**Manager `StrukturaOraganizacyjnaManager` (tylko odczyt nawigacyjny):**

| Metoda / property | Sygnatura | Zwraca |
|---|---|---|
| `Przełożony(...)` | `Pracownik Przełożony(StrukturaOrganizacyjna, Date, bool, Func<...>)` | bezpośredni przełożony |
| `PrzełożonyWgPodległości(...)` | `Pracownik PrzełożonyWgPodległości(Date, bool, Func<...>)` | przełożony wg podległości |
| `Przełożeni(...)` | `IEnumerable<Pracownik> …` | przełożeni |
| `Podwładni(...)` | `IEnumerable<Pracownik> Podwładni(FromTo, bool, Func<...>)` | podwładni w okresie |
| `GetDomyślnyPrzełożony(naDzień[, bezpośredni, warunek])` | `Pracownik GetDomyślnyPrzełożony(Date, bool=…, Func=…)` | domyślny przełożony na dzień (property `DomyślnyPrzełożony` jest **przestarzała** — używaj metody) |

**Workery zmiany powiązań (klasy zagnieżdżone w `Soneta.Kadry.Pracownik`):**

| Worker | Akcja (menu) | Metoda | Parametry |
|---|---|---|---|
| `Pracownik.DodajPowiązanieStrukturyWorker` | „Struktura organizacyjna/Dodaj lub modyfikuj powiązanie…" | `object DodajPowiązanieStruktury()` | `Params: WybórElementuContext`, `Pracownicy: Pracownik[]` (`[Context]`) |
| `Pracownik.UsuńPowiązanieStrukturyWorker` | „Struktura organizacyjna/Usuń powiązanie…" | `void DodajPowiązanieStruktury()` | `Params: WybórElementuContext`, `Pracownicy: Pracownik[]` |

**Snippet (dodanie powiązania ze strukturą — bezpośrednim ctorem):**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];

// Instancja elementu struktury (musi istnieć w bazie — tabela ElementyStrOrg, NIE DefElStrukturOrg):
ElementStrukturyOrganizacyjnej element =
    session.GetCore().ElementyStrOrg.Cast<ElementStrukturyOrganizacyjnej>().FirstOrDefault();

using (var t = session.Logout(editMode: true))
{
    var p = session.AddRow(new PowiązanieStrukturyOrganizacyjnej(element, pracownik));
    p.Okres = new FromTo(Date.Today, Date.MaxValue);
    t.Commit();
}
session.Save();

// Odczyt nawigacyjny struktury:
Pracownik przelozony = pracownik.StrukturaOraganizacyjna.GetDomyślnyPrzełożony(Date.Today);
```

**Snippet (zmiana wydziału — nowy zapis „od daty", A14):**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
var kadry = session.GetKadry();

using (var t = session.Logout(editMode: true))
{
    var ph = pracownik[Date.Today];           // zapis obowiązujący na dzień (A15)
    ph.Etat.Wydzial = kadry.Wydzialy.Firma;   // referencja do istniejącego wydziału (korzeń struktury)
    t.Commit();
}
session.Save();
```

**Pułapki:**
- `Etat.Wydzial` jest **danymi historycznymi** (na `PracHistoria.Etat`) i jest **wymagany dla etatu** —
  zmieniaj nowym zapisem „od daty" (A14), nie nadpisuj bieżącego (zmieniłoby cały okres wstecz).
- `PowiązanieStrukturyOrganizacyjnej.Element` to **referencja konfiguracyjna** — pobierz istniejący
  element struktury; bez zdefiniowanej struktury organizacyjnej scenariusz wymaga konfiguracji.
- `StrukturaOraganizacyjnaManager` jest **tylko do odczytu** — zmiany realizują workery
  `DodajPowiązanieStrukturyWorker` / `UsuńPowiązanieStrukturyWorker` lub bezpośredni zapis do
  `PowiązaniaStrOrg`.
- Workery struktury modyfikują dane „jak z UI" → transakcja + `CommitUI()` + `Save()`; rekordy z
  bieżącej sesji (safe-code §2.1).

### K8 — Oceny okresowe: arkusze ocen, cele okresowe, karty kompetencji

**Cel:** prowadzić oceny okresowe pracownika (arkusz oceny z elementami), cele okresowe wraz z ich
realizacją, karty kompetencji i karty opisu stanowiska. Funkcjonalność należy do modułów **HR**
(`session.GetHR()`, `OcenyPracownikow`, `EtapyRekrutacji`) i **HR2** (`session.GetHR2()`,
`CeleOkresowePrac`, `KartyKompPrac`, `KartyOpStanowisk`). Pracownik implementuje `IOceniany`,
`IOceniający`, `IOdpowiedzialnyZaOcenę`, `IŹródłoKartyOpisuStanowiska`.

**Publiczny kontrakt — kolekcje na `Pracownik`:**

| Kolekcja | Typ elementu | Zawartość |
|---|---|---|
| `Oceny` | `SubTable<Soneta.HR.OcenaPracownika>` | arkusze ocen okresowych (root) |
| `ElementyOceny` | `SubTable<Soneta.HR.ElementOcenyPracownika>` | pojedyncze elementy/pozycje arkuszy ocen |
| `CeleOkresowe` | `SubTable<Soneta.HR2.CelOkresowyPracownika>` | cele okresowe |
| `KartyKompetencji` | `SubTable<Soneta.HR2.KartaKompetencjiPracownika>` | karty kompetencji |
| `KartyOpisuStanowiska` | `SubTable<Soneta.HR2.KartaOpisuStanowiskaBase>` | karty opisu stanowiska |
| `KartyRealizacjiCelu` | `SubTable<Soneta.HR2.KartaRealizacjiCelu>` | karty realizacji celów |
| `Oceniani` / `Oceniający` | `SubTable<Soneta.Oceny.OcenaOceniany/OcenaOceniający>` | role pracownika w ocenie |

**`OcenaPracownika` (tabela `OcenyPracownikow`, root; impl. `IOcenaPracownika`) — pola:**

| Pole | Typ | Uwaga |
|---|---|---|
| `Pracownik` | `Soneta.Kadry.Pracownik` | oceniany |
| `Nazwa` | `string` | nazwa arkusza |
| `Data`, `Termin` | `Soneta.Types.Date` | data oceny / termin |
| `Opis` | `Soneta.Business.MemoText` | — |
| `Anulowany` | `bool` | — |
| `ElementyOceny` | `SubTable<ElementOcenyPracownika>` | pozycje arkusza |

Ctor: `new OcenaPracownika(Pracownik pracownik)`.

**`ElementOcenyPracownika` (tabela `ElementyOcenPrac`, child przez `Ocena`) — pola:**

| Pole | Typ | Uwaga |
|---|---|---|
| `Ocena` | `IOcenaPracownika` | guided-parent (arkusz oceny lub etap rekrutacji) |
| `Pracownik` | `Soneta.Kadry.Pracownik` | — |
| `Definicja` | `Soneta.HR.DefElementuOcenyPracownika` | **referencja konfiguracyjna** (z tabeli `HRModule.DefElemOcenPrac`); zapisywalna i wymagana do zapisu |
| `Typ` | `Soneta.HR.TypyElementowOceny` | `Historyczny`, `Aktualny` — **read-only** (wynika z definicji) |
| `Data` | `Soneta.Types.Date` | **read-only** (wyliczane) |
| `Wartosc` | `decimal` | wartość liczbowa oceny (zapisywalna) |

Ctor: `new ElementOcenyPracownika(IOcenaPracownika ocena)`. Dodawaj przez `session.AddRow(new ElementOcenyPracownika(ocena))` (NIE `ocena.ElementyOceny.AddRow(...)` — `SubTable` nie udostępnia `AddRow`).

**`CelOkresowyPracownika` (tabela `CeleOkresowePrac`, root) — pola:**

| Pole | Typ | Uwaga |
|---|---|---|
| `Pracownik` | `Soneta.Kadry.Pracownik` | — |
| `Nazwa` | `string` | nazwa celu |
| `Data`, `Termin` | `Soneta.Types.Date` | — |
| `Opis` | `Soneta.Business.MemoText` | — |
| `Definicja` | `Soneta.Oceny.DefinicjaElementuOceny` | **referencja konfiguracyjna** (opcjonalna) |
| `Anulowany` | `bool` | — |
| `Realizacja` | `Soneta.HR2.RealizacjaCelu` | bieżąca realizacja (subrow) |
| `Realizacje` | `SubTable<Soneta.HR2.RealizacjaCelu>` | realizacje celu |

> `CelOkresowyPracownika` **nie ma pola `Wartosc`** — postęp/ocena celu jest reprezentowana przez `Realizacja`/`Realizacje` (`Soneta.HR2.RealizacjaCelu`). Pole `Wartosc` (typu decimal) ma natomiast `ElementOcenyPracownika`.

Ctor: `new CelOkresowyPracownika(Pracownik pracownik)`.

**`KartaOpisuStanowiskaBase` (tabela `KartyOpStanowisk`, root) — pola:**

| Pole | Typ | Uwaga |
|---|---|---|
| `Zrodlo` | `IŹródłoKartyOpisuStanowiska` | = `Pracownik` / `DefinicjaStanowiska` / wakat / oferta |
| `Typ` | `Soneta.HR2.TypyKartOpisuStanowiska` | `KartaOpisuStanowiska`, `OgłoszenieOPracę` |
| `Data` | `Soneta.Types.Date` | — |
| `Elementy` | `SubTable<ElementKartyOpisuStanowiska>` | elementy opisu |
| `Kompetencje` | `SubTable<KompetencjaKartyOpisuStanowiska>` | kompetencje |

`KartaOpisuStanowiskaBase` i `KartaKompetencjiPracownika` **nie mają publicznego ctora bezparametrowego**;
`KartaKompetencjiPracownika` ma ctor `(Pracownik pracownik, IŹródłoKartyCharakterystykiPracownika zrodlo)`.
Karty zwykle tworzone są workerami kopiującymi (`KopiujKartęOpisuStanowiskaWorker.KopiujZDefinicjiStanowiska()`,
`KopiujKartęKompetencjiWorker.KopiujZKOS()`/`KopiujZPoprzedniej()`).

**Workery oceniania (klasy w `Soneta.HR` / `Soneta.HR2`):**

| Worker | Metoda | Parametry |
|---|---|---|
| `Soneta.HR.OcenaPracownikowWorker` | `Oceń()` | `Pars`, `Idxs: Pracownik[]` (`[Context]`); ctor `(Context)` |
| `Soneta.HR.WzorOcenyPracownika.ZainicjujOcenęWorker` | `Zainicjuj()` | `Ocena: IOcenaPracownika`, `Pars`; ctor `(Session)` |
| `Soneta.HR2.KopiujKartęOpisuStanowiskaWorker` | `KopiujZDefinicjiStanowiska()`, `KopiujZPoprzedniej()` | `Karta: KartaOpisuStanowiskaBase` |
| `Soneta.HR2.KopiujKartęKompetencjiWorker` | `KopiujZKOS()`, `KopiujZPoprzedniej()` | `Karta: KartaKompetencjiPracownika` |

**Snippet (dodanie celu okresowego — wymaga definicji elementu oceny w bazie HR2):**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
var hr2 = session.GetHR2();

// Definicja elementu oceny z konfiguracji modułu Oceny (musi istnieć):
var defElementu = session.ExecuteConfig(s =>
    /* pobranie DefinicjaElementuOceny z modułu Oceny */ default);

using (var t = session.Logout(editMode: true))
{
    var cel = new CelOkresowyPracownika(pracownik);
    hr2.CeleOkresowePrac.AddRow(cel);
    cel.Nazwa = "Wdrożenie nowego modułu";
    cel.Data = Date.Today;
    cel.Termin = new Date(2026, 12, 31);
    cel.Definicja = session.Get(defElementu);
    t.Commit();
}
session.Save();

// Odczyt celów okresowych:
foreach (CelOkresowyPracownika c in pracownik.CeleOkresowe)
{
    // c.Nazwa, c.Termin, c.Wartosc.Punktacja
}
```

**Snippet (utworzenie arkusza oceny i dodanie elementu):**

```csharp
using (var t = session.Logout(editMode: true))
{
    var ocena = new OcenaPracownika(pracownik);
    session.GetHR().OcenyPracownikow.AddRow(ocena);
    ocena.Nazwa = "Ocena roczna 2026";
    ocena.Data = Date.Today;

    var el = session.AddRow(new ElementOcenyPracownika(ocena));  // ocena jako IOcenaPracownika
    el.Definicja = defElementu;   // wymagana (z HRModule.DefElemOcenPrac); Typ/Data są wyliczane (read-only)
    el.Wartosc = 4m;              // Wartosc to decimal
    t.Commit();
}
session.Save();
```

**Pułapki:**
- Cele/elementy ocen wymagają **referencji do definicji konfiguracyjnych** (`DefElementuOcenyPracownika`,
  `Soneta.Oceny.DefinicjaElementuOceny`) — bez nich scenariusz wymaga uprzedniej konfiguracji modułu
  Oceny/HR/HR2. W bazie Demo te definicje **mogą nie istnieć** — najpierw sprawdź dostępność.
- Karty opisu stanowiska / kompetencji nie mają prostego ctora — twórz je workerami kopiującymi
  (`KopiujKartę…Worker`) z definicji stanowiska lub poprzedniej karty.
- `ElementOcenyPracownika.Ocena` to `IOcenaPracownika` — może to być arkusz oceny **lub etap
  rekrutacji** (`EtapRekrutacji` także implementuje `IOcenaPracownika`, patrz K9).
- `CelOkresowyPracownika` **nie ma pola `Wartosc`** — postęp/wynik celu reprezentują `Realizacja`/`Realizacje`
  (`RealizacjaCelu`). Liczbową wartość ma `ElementOcenyPracownika.Wartosc` (`decimal`).
- `ElementOcenyPracownika`: `Typ`/`Data` są **read-only** (wyliczane z definicji), a do tabeli dodawaj przez
  `session.AddRow(...)` — `SubTable<ElementOcenyPracownika>` nie ma metody `AddRow`.
- Workery oceniania uruchamiane „jak z UI" → transakcja + `CommitUI()` + `Save()`.

### K9 — Rekrutacja: wakaty, ogłoszenia, aplikacje, etapy, stan zatrudnienia

**Cel:** prowadzić proces rekrutacji — wakaty (zapotrzebowanie), oferty/ogłoszenia o pracę, aplikacje
kandydatów oraz etapy rekrutacji z ocenami, aż do zatrudnienia kandydata. Funkcjonalność należy do
modułów **HR2** (`session.GetHR2()`, `RekrutAplikacje`, `RekrutWakaty`) i **HR**
(`session.GetHR()`, `Rekrutacje`, `EtapyRekrutacji`).

**Publiczny kontrakt — kolekcje na `Pracownik` (kandydat jest reprezentowany rekordem `Pracownik`):**

| Kolekcja | Typ elementu | Zawartość |
|---|---|---|
| `Aplikacje` | `SubTable<Soneta.HR2.RekrutacjaAplikacja>` | aplikacje kandydata |
| `Wakaty` | `SubTable<Soneta.HR2.RekrutacjaWakat>` | wakaty |
| `Rekrutacje` / `Kandydatury` | `SubTable<Soneta.HR.Rekrutacja>` | rekrutacje (kandydatury) |
| `EtapyRekrutacji` | `SubTable<Soneta.HR.EtapRekrutacji>` | etapy rekrutacji |

**`RekrutacjaAplikacja` (tabela `RekrutAplikacje`, root; impl. `IŹródłoRekrutacji`) — pola:**

| Pole | Typ | Uwaga |
|---|---|---|
| `Pracownik` | `Soneta.Kadry.Pracownik` | kandydat |
| `Stanowisko` | `Soneta.HR.DefinicjaStanowiska` | **referencja konfiguracyjna** stanowiska |
| `Wydzial` | `Soneta.Kadry.Wydzial` | jednostka organizacyjna |
| `Oferta` | `Soneta.HR2.OfertaPracy` | oferta, na podstawie której wpłynęła aplikacja |
| `Stan` | `Soneta.HR2.StanAplikacji` | `Wprowadzona`, `Zakończona`, `Anulowana` |
| `Data` | `Soneta.Types.Date` | data aplikacji |
| `PlanowanaDataZatrudnienia` | `Soneta.Types.Date` | — |

Ctor: `new RekrutacjaAplikacja(Pracownik pracownik, Soneta.HR.WydziałDefinicjiStanowiska stanowisko)`.
`WydziałDefinicjiStanowiska` jest w module **`Soneta.HR`** (NIE `Soneta.HR2`) i ma ctor
`new WydziałDefinicjiStanowiska(DefinicjaStanowiska definicjaStanowiska)` — definicję pobierz
z `session.GetHR().DefStanowisk`. `RekrutacjaAplikacja.Stanowisko` zwraca tę `DefinicjaStanowiska`.

**`EtapRekrutacji` (tabela `EtapyRekrutacji`, root; impl. `IOcenaPracownika`) — pola:**

| Pole | Typ | Uwaga |
|---|---|---|
| `Rekrutacja` | `Soneta.HR.Rekrutacja` | rekrutacja nadrzędna |
| `Definicja` | `Soneta.HR.DefinicjaEtapuRekrutacji` | **referencja konfiguracyjna** |
| `Lp` | `int` | numer etapu |
| `Data`, `Termin` | `Soneta.Types.Date` | — |
| `Odpowiedzialny` | `Soneta.Oceny.IOceniający` | osoba odpowiedzialna |
| `Opis` | `Soneta.Business.MemoText` | — |
| `ElementyOceny` | `SubTable<ElementOcenyPracownika>` | oceny etapu (etap jest `IOcenaPracownika`) |

Ctor: `new EtapRekrutacji(Rekrutacja rekrutacja)`.

**`Rekrutacja` (tabela; impl. `IOcenaPracownika`) — ctory:**
`new Rekrutacja(Pracownik pracownik)`, `new Rekrutacja(Pracownik pracownik, IŹródłoRekrutacji źródło)`.

**`RekrutacjaWakat` (tabela `RekrutWakaty`, root) — ctory:**
`new RekrutacjaWakat(WydziałDefinicjiStanowiska stanowisko)`,
`new RekrutacjaWakat(DefinicjaStanowiska definicjaStanowiska, Wydzial wydzial)`.

**`OfertaPracy` (tabela; ogłoszenie o pracę) — ctory:**
`new OfertaPracy(WydziałDefinicjiStanowiska stanowisko)`, `new OfertaPracy(RekrutacjaWakat wakat)`.

**Workery rekrutacji (klasy zagnieżdżone):**

| Worker | Metoda | Parametry |
|---|---|---|
| `Soneta.HR2.RekrutacjaAplikacja.NowaRekrutacjaWorker` | rozpoczęcie rekrutacji z aplikacji | `Aplikacje: RekrutacjaAplikacja[]` |
| `Soneta.HR2.RekrutacjaWakat.NowaRekrutacjaWorker` | rozpoczęcie rekrutacji z wakatu | `Wakat: RekrutacjaWakat`, `Pracownicy: Pracownik[]` |
| `Soneta.HR2.OfertaPracy.NowaRekrutacjaWorker` | rozpoczęcie rekrutacji z oferty | `Oferta: OfertaPracy`, `Pracownicy: Pracownik[]` |
| `Soneta.HR.OcenaKandydatowWorker` | `Oceń()` | `Pars`, `Elementy: Rekrutacja[]`; ctor `(Context)` |
| `Soneta.HR.Rekrutacja.ZatrudnijWorker` | `PracHistoria Zatrudnij()` | `Pars`, `Rekrutacja: Rekrutacja` — tworzy zatrudnienie (zapis historii) |

**Snippet (dodanie aplikacji kandydata — wymaga def. stanowiska w bazie):**

```csharp
var hr2 = session.GetHR2();
var kandydat = session.GetKadry().Pracownicy.WgKodu["006"];

// Definicja stanowiska z konfiguracji HR (musi istnieć w session.GetHR().DefStanowisk):
var defStanowiska = session.GetHR().DefStanowisk
    .Cast<Soneta.HR.DefinicjaStanowiska>().FirstOrDefault();
var wydzialDefStanowiska = new Soneta.HR.WydziałDefinicjiStanowiska(defStanowiska);

using (var t = session.Logout(editMode: true))
{
    var aplikacja = session.AddRow(new RekrutacjaAplikacja(kandydat, wydzialDefStanowiska));
    aplikacja.Data = Date.Today;
    aplikacja.Stan = StanAplikacji.Wprowadzona;
    t.Commit();
}
session.Save();

// Odczyt aplikacji kandydata:
foreach (RekrutacjaAplikacja a in kandydat.Aplikacje)
{
    // a.Stanowisko, a.Stan, a.Data, a.Oferta
}
```

**Pułapki:**
- Cały proces rekrutacji wymaga **konfiguracji HR/HR2** (`DefinicjaStanowiska`,
  `DefinicjaEtapuRekrutacji`, `WydziałDefinicjiStanowiska`). W bazie Demo te definicje **mogą nie
  istnieć** — przed scenariuszem sprawdź dostępność, inaczej `Save()` rzuci wyjątek weryfikatora.
- `RekrutacjaAplikacja` przyjmuje w ctorze `WydziałDefinicjiStanowiska`, nie samą `DefinicjaStanowiska`
  (wydział definicji powstaje z `new WydziałDefinicjiStanowiska(definicjaStanowiska)`).
- `EtapRekrutacji` i `Rekrutacja` implementują `IOcenaPracownika` — oceny etapów trzyma
  `EtapRekrutacji.ElementyOceny` (te same `ElementOcenyPracownika` co w K8).
- `new Rekrutacja(pracownik)` ustawia pole `Pracownik` i dodaje rekord do **roota** `HRModule.Rekrutacje`
  (oraz `EtapRekrutacji` do `HRModule.EtapyRekrutacji`). Kolekcje na `Pracownik` (`Rekrutacje`/`Kandydatury`/
  `EtapyRekrutacji`) to `ChildTable` wiązane przez relacje — do weryfikacji w teście pewniejszy jest root
  `session.GetHR().Rekrutacje` niż `pracownik.Rekrutacje` (zależnie od relacji może być pusta dla samego `Pracownik`).
- Zatrudnienie kandydata realizuje `Rekrutacja.ZatrudnijWorker.Zatrudnij()` (zwraca `PracHistoria`) —
  spina rekrutację z zatrudnieniem (sekcja A). Worker modyfikuje dane → transakcja + `CommitUI()` + `Save()`.
- `Stan` aplikacji (`Wprowadzona`/`Zakończona`/`Anulowana`) steruje cyklem życia — nie usuwaj aplikacji
  z historią, oznaczaj `Anulowana`.
