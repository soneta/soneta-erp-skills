# Pracownik / Kadry-Płace — receptury kodu biznesowego (platforma Soneta)

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

Każdy wzorzec (`KADRY-Xn`, gdzie `X` = litera sekcji z listy zadań) ma stałą strukturę:

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


## Mapa receptur

| Rozdział | Plik | Receptury |
|---|---|---|
| KADRY01 — Pracownik — zatrudnienie i dane kartotekowe | [kadry/KADRY01-pracownik.md](kadry/KADRY01-pracownik.md) | KADRY-A* |
| KADRY02 — Etat — zatrudnienie etatowe | [kadry/KADRY02-etat.md](kadry/KADRY02-etat.md) | KADRY-B* |
| KADRY03 — Dodatki, potrącenia, akordy | [kadry/KADRY03-dodatki-potracenia.md](kadry/KADRY03-dodatki-potracenia.md) | KADRY-C* |
| KADRY04 — Nieobecności i czas pracy | [kadry/KADRY04-nieobecnosci.md](kadry/KADRY04-nieobecnosci.md) | KADRY-D* |
| KADRY05 — Plan pracy i kalendarz | [kadry/KADRY05-plan-pracy.md](kadry/KADRY05-plan-pracy.md) | KADRY-E* |
| KADRY06 — RCP — rejestracja czasu pracy | [kadry/KADRY06-rcp.md](kadry/KADRY06-rcp.md) | KADRY-F* |
| KADRY07 — Umowy cywilnoprawne | [kadry/KADRY07-umowy.md](kadry/KADRY07-umowy.md) | KADRY-G* |
| KADRY08 — Płace — naliczanie wypłat | [kadry/KADRY08-place.md](kadry/KADRY08-place.md) | KADRY-H* |
| KADRY09 — Listy płac, przelewy, wydruki | [kadry/KADRY09-listy-place.md](kadry/KADRY09-listy-place.md) | KADRY-I* |
| KADRY10 — Deklaracje (ZUS, PIT, PFRON, PPK) | [kadry/KADRY10-deklaracje.md](kadry/KADRY10-deklaracje.md) | KADRY-J* |
| KADRY11 — Ewidencje pracownicze | [kadry/KADRY11-ewidencje.md](kadry/KADRY11-ewidencje.md) | KADRY-K* |

## Powiązane dokumenty

- [`safe-code.md`](../safe-code.md) — sesja, transakcje, blokada optymistyczna, zasady bezpiecznego kodu.
- [`session-login.md`](../session-login.md) — `Session`, `Login`, `Database`.
- [`worker-extender.md`](../worker-extender.md) — workery, akcje menu Czynności, bindowanie.
- [`rowcondition.md`](../rowcondition.md) — serwerowy LINQ, `RowCondition`, `SubTable[condition]`.
- [`features.md`](../features.md) — cechy (`Features`), typy, dostęp typowany/nietypowany.
- [`scan-props.md`](../scan-props.md) / [`scan-workers.md`](../scan-workers.md) — inwentaryzacja pól i workerów; weryfikacja dokładnych nazw i typów pól obiektu z DLL.

