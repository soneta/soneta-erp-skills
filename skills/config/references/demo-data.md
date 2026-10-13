# Mechanizm zasilania bazy Demo

Dane standardowej bazy demo to pliki XML w formacie sesji importu
(`<session xmlns="http://www.soneta.pl/schema/business">` — składnia:
[import-xml.md](import-xml.md)), leżące w katalogu **`Demo`** obok binariów
programu. Import wykonuje się przy tworzeniu bazy z danymi przykładowymi — `dbmgr create X
--demo gold|silver` (`/soneta:tools`), kreator baz, pierwszy start programu.

**Najważniejsze reguły:**

- **Bez manifestu** — nowy plik wystarczy dodać do katalogu `Demo`; **nierekurencyjnie** —
  plik w podkatalogu NIE zostanie zaimportowany.
- **Kolejność** = sortowanie leksykograficzne (ordinal) pełnych ścieżek → stosuj prefiksy
  numeryczne **stałej szerokości** (`001`, `100`, `102` — nie `1`, `10`); wielkość liter ma
  znaczenie. Każdy plik importowany jest **osobną sesją** → referencje tylko „w przód"
  (późniejszy plik może wskazywać rekordy wcześniejszego, nie odwrotnie).
- **Sufiksy koloru licencji:** `*.silver.xml` — tylko wersja srebrna; `*.gold.xml` — wszystko
  poza srebrną (złota i platynowa; osobnego sufiksu platinum nie ma); bez sufiksu — zawsze.
- Import wykonuje operator `Administrator`, **GUID-y z pliku wstawiane 1:1** (bez mapowania) —
  muszą być globalnie unikalne względem pozostałych plików.
- **Rekordy standardowe (dbinit) powstają PRZED danymi demo** — pliki demo mogą odwoływać się
  **po GUID** do rekordów standardowych (w tym do rekordów `<Right>` i definicji z dbinit —
  zob. [import-xml.md](import-xml.md)).
- Rekordy w plikach demo zapisuje się **bez `dbversion`** — import następuje raz, przy
  tworzeniu bazy (odwrotnie niż w `*.dbinit.xml`, gdzie `dbversion` jest obowiązkowy).
- Instalacje Premium pomijają standardowe dane demo.
- **Dodatek nie osadza danych demo w bibliotece** — z zasobów assembly czytane są tylko
  `*.dbinit.xml`. Plik demo dodatku trzymaj w repozytorium (np. `Demo/900.<Moduł>.gold.xml`)
  i kopiuj do katalogu `Demo` produktu przy instalacji.

## Import bez operatora — `dbmgr importxml`

Sesja `dbmgr importxml` nie ma operatora ani roli (`Session.AuthorizationInfo.Operator == null`,
`Login.CurrentRole == null`). Skutki dla kodu wiersza:

- `OnAdded` ustawiające autora z sesji nic nie ustawi — przy polu wymaganym import pada. W pliku
  wskaż autora jawnie: `<Operator where="Name=Administrator" />`.
- Kod praw w `CalcReadOnly`/`OnDeleting` bez obsługi `null` rzuca `NullReferenceException` —
  brak operatora ma oznaczać kontekst systemowy (`/soneta:programming` → *rights-source*,
  sekcja o prawach prostych).
- Atrybut `guid` działa także przy `business="true"` — ponowny import aktualizuje rekord. Import
  po GUID przechodzi przez `CalcReadOnly`, więc wiersz tylko do odczytu dla „obcego" operatora
  zablokowałby aktualizację.

## Odróżnij od `--sampledata` (wzorce)

Opcja `dbmgr create --sampledata` i katalog `Patterns` to **inny mechanizm** (wzorców) —
nie mylić z katalogiem `Demo` opisanym tutaj. Szczegóły opcji `create`: `/soneta:tools`
(artykuł *dbmgr*).

## Testowanie pliku demo

- **Szybko:** `dbmgr importxml <baza> <plik>` wykonane **×2** — drugi import weryfikuje
  idempotencję (detale zagnieżdżone w rodzicu — zob.
  [import-xml.md](import-xml.md), *Zachowanie kolekcji przy aktualizacji rekordu*).
- **Pełna ścieżka:** `dbmgr create X --demo gold --recreate` (ok. 2–3 min) i kontrola rekordów;
  sprzątanie `dbmgr drop X`.
- Import do bazy wykonuj na żądanie użytkownika, na bazie testowej — zasady jak w
  [import-xml.md](import-xml.md), *Testowanie plików XML*.

## Checklista — nowy plik danych demo

- [ ] Plik bezpośrednio w katalogu `Demo` (nie w podkatalogu), rozszerzenie `.xml`.
- [ ] Prefiks numeryczny stałej szerokości; numer większy niż pliki, do których się odwołujesz.
- [ ] Sufiks `.gold`/`.silver` tylko gdy dane zależą od koloru licencji.
- [ ] GUID-y unikalne; referencje do rekordów standardowych po GUID; bez `dbversion`.
- [ ] Pola ustawiane w `OnAdded` z operatora sesji (np. autor) podane w pliku jawnie.
- [ ] Plik demo dodatku w repozytorium, kopiowany do katalogu `Demo` produktu przy instalacji.
- [ ] Detale zagnieżdżone w rodzicu, tabele z selectorem z elementem selectora
      ([import-xml.md](import-xml.md)).
- [ ] Weryfikacja: podwójny `importxml` albo pełne `create --demo`.

## Powiązania

- [import-xml.md](import-xml.md) — format plików, identyfikacja rekordów,
  kolekcje, prawa `<Right>`.
- `/soneta:tools` — `dbmgr create --demo`, `importxml`, `drop` (artykuł *dbmgr*).
- `/soneta:programming` — *rights-source* (prawa dla funkcji zasilanych w demo/dbinit).
