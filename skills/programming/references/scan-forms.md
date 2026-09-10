# Zakładki, sekcje i pola formularzy z DLL (skaner zasobów osadzonych)

Odczyt rzeczywistej struktury formularzy platformy Soneta — **zakładek**, **sekcji danych**
(grup) i **pól w kolejności wprowadzania** — wprost z bibliotek DLL, bez dostępu do źródeł.
Służy do budowania kodu wprowadzającego dane oraz do przygotowania [importu XML](../../config/SKILL.md)
w trybie `business="true"`, gdzie **kolejność pól i przynależność do sekcji** ma znaczenie.
Zakładki i listy (`Grid`) pokazują też, co logicznie stanowi „dane obiektu" — pomaga to
**ustalić zakres eksportu** (które podkolekcje i cechy dołączyć do datapacku).

> **Ścieżki poleceń** w tym dokumencie są względne wobec katalogu skilla (`skills/programming/`
> w repozytorium) — ustal lokalizację zainstalowanego skilla i uruchamiaj polecenia z tego
> katalogu albo poprzedź jego ścieżką.

## Najpierw dane wygenerowane (`data/forms/`) — szybkie wyszukanie „obiekt → zakładki”

Katalog **wszystkich zakładek** (pageform) jest wyeksportowany do
[`../data/forms/`](../data/forms/). Podział jest dwupoziomowy: [`INDEX.md`](../data/forms/INDEX.md)
to router (lista 26 przestrzeni nazw + sekcja systemowa), a zakładki biznesowe leżą
w plikach `<Przestrzeń>.md` — pełna lista (572 KB) nie mieściła się w jednym odczycie.

**Nie musisz wiedzieć, w której przestrzeni jest typ** — przeszukaj wszystkie naraz:

```bash
rg '^\| Kontrahent \|' data/forms/*.md            # wszystkie zakładki typu
rg -l '^\| DokumentHandlowy \|' data/forms/*.md   # sam plik przestrzeni
rg '^\| \w*Pracownik\w* \|' data/forms/*.md       # gdy nie znasz dokładnej nazwy
```

Kolumny w plikach przestrzeni:
`Typ danych | Zakładka (plik) | Nazwa zakładki | Priority | Biblioteka | Przestrzeń`,
**posortowane po typie danych**. Nazwa zasobu ma postać `…<TYP>.<ZAKŁADKA>.pageform.xml`,
więc `Typ danych` to **segment przed nazwą zakładki** (walidowany względem realnych `RowType`;
dla okien konfiguracji z folderem `Config` typ bierzemy z nazwy zakładki), albo atrybut
`DataType`, gdy jest. Dzięki temu zakładki jednego typu są razem — np. całe okno
`DokumentHandlowy` (pliki `Dokument*`) i ten sam plik zakładki użyty przez różne typy
(`KontrahentDodatkowe` = „Warunki płatności" pod `Kontrahent`, `Bank`, `UrzadSkarbowy`…).
Odczyt jest natychmiastowy i **nie wymaga DLL**. **Nie zawiera pól ani sekcji** — te wypisuje
skaner niżej.

Zakładki przypięte do **typów ogólnych** (`Row`, `GuidedRow`, `ExportedRow`, `IRow`,
`IGuidedRow`, `object`) to **zakładki systemowe** (Załączniki, Notatki, Dyskusja, Panel BI,
„Dodatkowe (cechy)"…) — platforma dokłada je do wielu obiektów, więc są w INDEX-ie w osobnej
sekcji „Zakładki systemowe", a `scan-forms` dla konkretnego obiektu ich **nie raportuje**
(pokaże je tylko przy skanie samego typu ogólnego, np. `-- Row`).

Regeneracja po zmianie wersji/kompilacji (jeden przebieg po DLL):

```bash
dotnet script scripts/export-forms-index.csx \
    -- <KatalogDll> data/forms
```
> **Miejsce regeneracji.** Katalog zainstalowanego skilla może być tylko do odczytu lub
> zostać zastąpiony podczas aktualizacji. Regeneruj w klonie repo `soneta-erp-skills`
> albo podaj zapisywalny katalog wynikowy w projekcie i korzystaj z danych z tej lokalizacji.

## Po co to

Kolejność, w jakiej operator wypełnia pola na formularzu, pośrednio odzwierciedla kolejność
wykonywanego kodu (settery, `Accessor`, przeliczenia, walidacje). Ta sama kolejność jest
potrzebna, gdy dane wprowadza się:
- **kodem** (ustawianie właściwości rekordu w poprawnej sekwencji — patrz [context.md](context.md), [safe-code.md](safe-code.md)),
- **importem XML `business="true"`** — logika biznesowa reaguje na kolejność ustawień jak przy
  ręcznym wprowadzaniu (patrz artykuł import/eksport w [config](../../config/SKILL.md)).

Grupy (`Group`) i zakładki (`Page`) wyznaczają **sekcje danych** do uzupełnienia — skaner
pokazuje je w kolumnie `Sekcja`, dzięki czemu widać, które pola tworzą logiczną całość.

## Mechanizm — inny niż `scan-props`

`scan-props`/`scan-modules` czytają **metadane zarządzane** (typy, atrybuty) przez Roslyn.
Definicje formularzy nie są typami — to **zasoby osadzone** (`ManifestResource`) w bibliotece
UI. Dlatego skaner używa `System.Reflection.PortableExecutable` + `System.Reflection.Metadata`:
czyta tablicę zasobów i wyciąga bajty pliku formularza **bez ładowania IL** (ta sama zasada
„tylko odczyt", co w pozostałych skanerach).

Algorytm:
1. Przejdź wszystkie `*.dll` w katalogu; z każdej odczytaj zasoby kończące się na
   `.pageform.xml` / `.form.xml` / `.viewform.xml` / `.gridform.xml` / `.lookupform.xml`.
   Zasób osadzony ma format: 4-bajtowa długość + dane; bajty to XML z **BOM-em** (`EF BB BF`) —
   trzeba go obciąć przed parsowaniem.
2. Zbuduj indeks `nazwa-pliku → zasób` (do rozwiązywania `Include` — także **między bibliotekami**,
   np. `AdresH.form.xml` z biblioteki Core dołączane w formularzu z innej biblioteki).
3. Wybierz zakładki: pliki `*.pageform.xml` dopasowane **po nazwie pliku** (prefiks) **lub po
   atrybucie `DataType`** z `<DataForm>` (patrz niżej — „Wybór zakładek"). Posortuj po `Priority`
   (rosnąco), potem po nazwie pliku.
4. Dla każdej zakładki przejdź drzewo UI w **kolejności dokumentu**, akumulując dwie ścieżki:
   - **kontekst danych** — z atrybutów `DataContext` (rozwijanie ścieżek pól, patrz niżej),
   - **sekcję** — z tytułów `Group` (`CaptionHtml`); zagnieżdżone grupy → ścieżka `A / B`.
5. Dla elementów z `EditValue` (`Field`, `Data`, `Html`, `Markdown`, `Axis`) wypisz wiersz:
   `Sekcja | Ścieżka pola | Etykieta | Uwagi`. Elementy **listowe** (`Grid`, `Scheduler`, `Gantt`,
   `Pivot`, `Chart`…) wypisz jako wiersz listy i zejdź w kolumny z kontekstem **elementu
   kolekcji** (separator `:`) — patrz sekcja „Listy".
6. `Include` z atrybutem `Source` będącym nazwą pliku → wczytaj dołączany fragment, złóż jego
   kontekst z `DataContext`/`Suffix` z elementu `Include` i rekurencyjnie rozwiń jego pola.
   Cykle są zabezpieczone (zbiór odwiedzonych `zasób|kontekst`).

## Rozwijanie ścieżek pól (DataContext + EditValue)

Pełna ścieżka pola = złożenie łańcucha `DataContext` z wartością `EditValue`. Szczegóły
składni bindowania opisuje skill [form-xml](../../form-xml/SKILL.md).

| Sytuacja | Efekt w ścieżce |
|---|---|
| `Page DataContext="{DataSource}"` | korzeń = otwarty obiekt (ścieżka pusta) |
| `Group DataContext="{Adres}"` + `Field EditValue="{Ulica}"` | `Adres.Ulica` |
| zagnieżdżony `EditValue="{AdresRozszerzony.Dzielnica}"` | `Adres.AdresRozszerzony.Dzielnica` |
| `Include Source="AdresH.form.xml" DataContext="{Adres}"` | pola fragmentu rebazowane na `Adres.*` |
| `EditValue="{Workers.Cena.Netto}"` | `Workers.Cena.Netto` (accessor workera — wprost w ścieżce) |
| `EditValue="{ObiektViewInfo+TypParams.Pole}"` | `ObiektViewInfo+TypParams.Pole` (nawigacja `ViewInfo`) |
| `DataContext="{new FooExtender}"` | `new FooExtender` — nowy korzeń (obiekt z kodu), pola: `new FooExtender.Bar` |
| obiekt z historią (`IRowWithHistory`, np. `Pracownik`) + `EditValue="{Historia.Current.Nazwisko}"` | `Historia.Current.Nazwisko` — pole siedzi na wierszu historii (`PracHistoria`), nie na obiekcie |

**Obiekty z historią (`IRowWithHistory`).** Własne zakładki takiego obiektu opisują zwykle tylko
interfejsy, a pola leżą na wierszu historii. Ścieżka to `<kolekcja historii>.Current.<pole>`
(np. `Historia.Current.Etat.Zaszeregowanie.Stawka`). `Current` nie jest polem wiersza — podtabela
historii rozwiązuje je **datą aktualności z kontekstu** (pozostałe nazwy: `First`, `Today`,
`Last`). W kodzie odpowiednikiem jest indekser datą: `pracownik[Date.Today].Nazwisko`
(patrz [scan-workers.md](scan-workers.md), reguła typu rekordu historycznego).

Wyrażenia dostępowe (`Workers.…`, `Features.…`, `+`, `()`, `new …Extender`) zostają **wprost
w ścieżce** — są standardową składnią accessor-ów, więc nie są opisywane osobną notą (mniej szumu).
Ścieżka sama sygnalizuje pochodzenie wartości; szczegóły workerów/extenderów bada się na bieżąco
([worker-extender.md](worker-extender.md)), cech — [features.md](features.md), `ViewInfo` — [viewinfo.md](viewinfo.md).

### Listy — `Grid`, `Scheduler`, `Gantt`, `Pivot`, `Chart`…

Elementy listowe (`Grid`, `TreeList`, `Scheduler`, `Gantt`, `GanttDiagram`, `KanbanDiagram`,
`Pivot`, `Chart`, `Diagram`, `TreeDiagram`) mają `EditValue` zwracające **kolekcję**. Pola
wewnątrz (kolumny) odnoszą się już do **elementu tej kolekcji** — innego obiektu niż kontekst
rodzica. Skaner:

- wypisuje sam element listy jednym wierszem (ścieżka = kolekcja) z notą `lista (Grid)`,
- kolumnom nadaje kontekst elementu kolekcji, oddzielając **dwukropkiem `:`** część wczytującą
  listę od pól na elemencie: `Kolekcja:Pole` (np. `Ceny:Netto`, `Ceny:Definicja.Priorytet`,
  `PrzelicznikiTowaru:Bazowa`).

Gdy kolekcja pochodzi z kodu, wyrażenie zostaje w ścieżce, np.
`new TowarExtender.Ceny:Workers.Cena.Netto` (kolekcja z extendera `:` accessor workera na
elemencie). Aby poznać realne pola elementu, sięgnij po typ zwracany przez kolekcję narzędziem
[scan-props.md](scan-props.md).

**Paski filtra (`Class="DataBar"`).** Element w liście z klasą `DataBar` (zwykle `Flow`/`Group`
z `DataContext="{Context}"`) to **filtry listy**, nie kolumny — jego pola dotyczą kontekstu
**nadrzędnego** (host listy / parametry `ViewInfo` w `Context`), więc rozwijają się jako
`Context.Params.Pole` (bez prefiksu kolekcji i bez `:`), a nie w kontekście elementu kolekcji.
W `Uwagi` dostają notę **`filtr listy: \`<kolekcja>\``** wskazującą, której listy dotyczą
(przydatne, gdy w jednej sekcji jest kilka list o tych samych parametrach filtra).

## Wybór zakładek — po nazwie pliku i po `DataType`

Platforma składa okno wielozakładkowe w runtime; **nie ma atrybutu assembly wiążącego
pojedynczy `pageform` z typem** (`FolderView` dotyczy tylko list/folderów). Skaner łączy więc
dwa tryby dopasowania (suma) do podanego argumentu:

**(a) po nazwie pliku** — nazwa zasobu ma postać `…<TYP>.<ZAKŁADKA>.pageform.xml`, więc typ
okna to **segment przed nazwą zakładki** (dla folderu `Config` typ jest w nazwie zakładki).
Skaner dopasowuje argument do tego typu **oraz** — zapasowo, by nie gubić — do samej nazwy
zakładki. Dzięki temu `DokumentHandlowy` łapie od razu całe okno dokumentu (pliki
`DokumentOgolne`, `DokumentPlatnosci`, `DokumentKontrahent`…), a `Kontrahent` — zakładki
kontrahenta (`KontrahentAdresy`, `KontrahentDodatkowe`…), **nie łapiąc** tego samego pliku
zakładki użytego przez inny typ (`Bank.KontrahentDodatkowe`). Typ w segmencie może być klasą,
interfejsem lub klasą dziedziczącą (selektorem). Warunek dopasowania: równość albo prefiks
zakończony wielką literą (`Kontrahent` nie łapie `Kontrahentowy`).

**(b) po `DataType`** — gdy `<DataForm>` ma atrybut `DataType="Namespace.Typ,Assembly"`, typ
jest **jawny** (nazwa pliku bywa niejednoznaczna: formularze parametrów workerów, konfiguracji,
selektory). Dla argumentu prostego skaner dopasowuje po **nazwie prostej** typu
(`Soneta.Business.Db.DashboardView` → `DashboardView`), więc łapie też zakładki o nazwach plików
niepowiązanych z typem (`GeneralBI.pageform.xml`, `GeneralCockpit.pageform.xml`).

W nagłówku każdej zakładki skaner pokazuje metadane: `plik` (DLL), `Priority`, `typ danych
(DataType)` gdy jest, `prawo` (`RightName`) oraz **`licencje`** — wymagane moduły licencyjne całej zakładki z atrybutu
`Contexts` na `<DataForm>`, **bez prefiksu** `License.`/`Licence.` (np. `HAN | FA`, czasem
z poziomem `_Złoty`/`_Platynowy`). Składnię `Contexts` opisuje skill
[form-xml](../../form-xml/SKILL.md).

### Zawężanie po namespace — ta sama nazwa w wielu modułach

Jeśli argument zawiera **kropkę**, jest traktowany jako **nazwa kwalifikowana namespace**
(`Kasa.Wyplata`, `Soneta.Kasa.Wyplata`). To rozstrzyga niejednoznaczność, gdy ta sama nazwa
prosta istnieje w wielu przestrzeniach — np. `Wyplata` jest w `Soneta.Kasa` **i** w
`Soneta.KadryPlace`. Zawężanie działa dwutorowo:
- dopasowanie **po `DataType`** — pełna nazwa typu równa argumentowi lub kończąca się na
  `.{argument}` (przyrostek namespace),
- dopasowanie **po nazwie pliku** — dodatkowo wymaga, by **nazwa zasobu** zawierała człon
  namespace (nazwa zasobu zaczyna się od domyślnej przestrzeni assembly modułu, np.
  `Soneta.KadryPlace.UI…`), bo sama nazwa pliku namespace nie niesie.

Gdy argument jest **prosty**, a trafienia pochodzą z wielu przestrzeni, skaner wypisuje
**ostrzeżenie** z listą przestrzeni i podpowiedzią kwalifikacji (np. `Soneta.KadryPlace.Wyplata`).

## Uruchomienie

```bash
dotnet script scripts/scan-forms.csx \
    -- <PrefiksNazwyFormularza> <KatalogDll>
```

### Przykłady

```bash
# Wszystkie zakładki kontrahenta
dotnet script .../scan-forms.csx -- Kontrahent ~/d/dev/bin/debug

# Okno dokumentu handlowego (pliki Dokument*)
dotnet script .../scan-forms.csx -- Dokument ~/d/dev/bin/debug

# Nazwa w wielu modułach — zawężenie po namespace (Wyplata jest w Kasa i KadryPlace)
dotnet script .../scan-forms.csx -- KadryPlace.Wyplata ~/d/dev/bin/debug
```

### Przykładowe wyjście

```markdown
# Formularze dla `Kontrahent` — zakładki, sekcje i pola

Dopasowano 51 zakładek (pageform) po typie danych lub `DataType`. …

## Zakładka: Ogólne

- plik: `Kontrahent.pageform.xml` (DLL `Soneta.CRM.UI.dll`), Priority=0
- prawo: `Page:KontrahentPage`
- licencje: `HAN | FA | KS`

| # | Sekcja | Ścieżka pola | Etykieta | Uwagi |
|---|--------|--------------|----------|-------|
| 1 | Dane identyfikacyjne | Kod | Kod |  |
| 14 | Adres | Adres.Ulica | Ulica |  |
| 31 | Adres | Adres.AdresRozszerzony.Dzielnica | Dzielnica |  |
| 44 | Kontakt | Kontakty |  | lista (Grid) — kolumny odnoszą się do elementu kolekcji `Kontakty` |
| 45 | Kontakt | Kontakty:Osoba | Osoba |  |
```

## Kody wyjścia

| Kod | Znaczenie |
|-----|-----------|
| `0` | OK — wypisano zakładki, sekcje i pola |
| `1` | Błąd argumentów / nie istnieje katalog |
| `2` | Nie znaleziono zakładek `*.pageform.xml` o podanym prefiksie/`DataType` |

## Ograniczenia

- **Grupowanie okna** to konwencja runtime — dopasowanie po nazwie pliku jest heurystyką, którą
  dobiera użytkownik (szeroki prefiks może objąć zakładki kilku typów `Prefiks*`). Dopasowanie
  po `DataType` jest jednoznaczne, ale obejmuje tylko formularze, które ten atrybut ustawiają.
- **Namespace przy nazwie z kropką** opiera się na przestrzeni ZASOBU (domyślny namespace
  assembly modułu) i na `DataType` — gdy oba są nietypowe (formularz w nietypowej przestrzeni),
  zawężenie może pominąć plik; wtedy użyj argumentu prostego i przejrzyj ostrzeżenie o przestrzeniach.
- Skanuje tylko górny poziom katalogu (`SearchOption.TopDirectoryOnly`).
- Gałęzi zależnych od `Visibility` skaner **nie wartościuje** — wypisuje wszystkie pola
  (warunku `Visibility` nie pokazuje). Faktyczna widoczność zależy od danych, cech i licencji
  w czasie działania; wymagane licencje całej zakładki są w metadanych (`licencje`).
- Kolumny list (`Grid`/`Scheduler`/…) odnoszą się do **elementu kolekcji**, nie do rodzica —
  patrz sekcja „Listy" niżej. Dwukropek `:` w ścieżce oddziela wczytanie listy od pól elementu.
- Pierwsze uruchomienie pobiera pakiety potrzebne `dotnet-script` — wymaga internetu.

## Powiązania

- Dane wygenerowane: [`../data/forms/`](../data/forms/) — katalog wszystkich zakładek
  (router [`INDEX.md`](../data/forms/INDEX.md) + pliki `<Przestrzeń>.md`; pierwsze źródło
  „obiekt → zakładki"); regeneracja skryptem `scripts/export-forms-index.csx`.
- [form-xml](../../form-xml/SKILL.md) — składnia `Page`/`Group`/`Field`/`Include`,
  `DataContext`, `EditValue`; strona źródłowa tego, co skaner odczytuje.
- [scan-props.md](scan-props.md) — pola bazodanowe i kalkulowane tabeli (typy, tytuły) do
  których prowadzą ścieżki pól z formularza.
- [config](../../config/SKILL.md) — import/eksport XML; sekcje i kolejność pól
  są istotne przy `business="true"`, a listy/zakładki podpowiadają zakres eksportu (datapack).
- [context.md](context.md), [safe-code.md](safe-code.md) — budowanie danych kodem w poprawnej sekwencji.
- Narzędzie [scan-folders](../../config/references/scan-folders.md) — statyczne foldery menu (listy, formularze).
