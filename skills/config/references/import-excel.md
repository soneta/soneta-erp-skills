# Import danych z Excela i CSV do list (także migracja z innych systemów)

Każdą listę programu, do której operator może dodawać lub edytować obiekty, można zasilić
danymi z arkusza Excel (`.xlsx`) albo pliku CSV. Import ustawia **właściwości biznesowe**
obiektów, czyli te same, które operator wypełnia na formularzu. Dlatego działa cała logika
biznesowa: podpowiedzi, przeliczenia, blokady pól i weryfikatory poprawności. Ten mechanizm
służy do bieżącego wczytywania danych i do **migracji** z innych systemów. Budowę pliku
i ścieżki nagłówka opisuje [export-excel.md](export-excel.md). Plik wyeksportowany z nagłówkiem
„ścieżka dostępu do danych" jest gotowym szablonem importu.

## Najważniejsze zasady

1. **Pierwszy wiersz to ścieżki właściwości** (`Kod`, `Kontrahent:Kod`, `Cena.Waluta`). Tytuły
   kolumn listy nie zadziałają.
2. **Wszystko albo nic.** Import z listy głównej zapisuje dane w jednej transakcji bazy.
   Wystarczy jeden błąd w pliku i nic nie zostaje zapisane.
3. **Klucz w pliku decyduje: aktualizacja czy nowy obiekt.** Wyszukiwanie odbywa się po `Guid`,
   `ID`, kluczu historii i kluczach unikalnych. Bez klucza każdy wiersz to nowy obiekt.
4. **Kolumny są ustawiane po kolei, od lewej.** Kolejność ma odpowiadać kolejności pól na
   formularzu: pola sterujące przed zależnymi.
5. **Pusta komórka nie zmienia wartości.** Odwołanie czyści się wpisem `-` albo `(null)`.
6. **Nieznany klucz w odwołaniu tworzy nowy obiekt powiązany**, jeśli da się go utworzyć.
   W migracji zabezpiecz się kolumną `Obiekt:!Action Edit`, zob.
   [Akcje](#akcje--dodawanie-edycja-usuwanie).
7. **Kolejność importu przy migracji:** najpierw słowniki i dane, do których odwołują się
   kolejne pliki, potem kartoteki, na końcu dokumenty.
8. **Najpierw import próbny.** W oknie programu go nie ma. Przez MCP: `import_folder_list`
   z `testing=true`, czyli pełny przebieg z zapisem wycofanym na końcu (zob. [Import próbny
   przez MCP](#import-próbny-przez-mcp)). Bez MCP testuj na kopii bazy.

## Spis treści

- [Skąd uruchomić import](#skąd-uruchomić-import)
- [Budowa pliku](#budowa-pliku)
- [Identyfikacja obiektów](#identyfikacja-obiektów)
- [Akcje — dodawanie, edycja, usuwanie](#akcje--dodawanie-edycja-usuwanie)
- [Odwołania do innych obiektów](#odwołania-do-innych-obiektów)
- [Wartości](#wartości)
- [Podlisty](#podlisty)
- [Dane historyczne](#dane-historyczne)
- [Kolumny specjalne — ściąga](#kolumny-specjalne--ściąga)
- [Przebieg importu: transakcja, paczki, wątki](#przebieg-importu-transakcja-paczki-wątki)
- [Weryfikacja obiektów biznesowych](#weryfikacja-obiektów-biznesowych)
- [Raportowanie błędów](#raportowanie-błędów)
- [Opcje konfiguracji](#opcje-konfiguracji)
- [Import próbny przez MCP](#import-próbny-przez-mcp)
- [Migracja danych z innego systemu](#migracja-danych-z-innego-systemu)
- [Checklisty](#checklisty)
- [Powiązania](#powiązania)

## Skąd uruchomić import

| Miejsce | Sposób | Uwagi |
|---|---|---|
| Lista w programie (desktop) | zaznacz w Excelu zakres **z wierszem nagłówka**, skopiuj, na liście **Wklej** | prawo **„Lista - Wklej"**; lista nie może być tylko do odczytu |
| Lista w aplikacji webowej | okno **„Import danych"**: plik `.xlsx`, `.csv`, `.tsv`, `.txt` albo wklejony tekst; także wklejenie ze schowka | po imporcie komunikat z liczbą zmienionych zapisów |
| Podlista na formularzu obiektu (np. pozycje dokumentu) | wklejenie jak wyżej | dane trafiają do formularza; zapisuje je operator przyciskiem formularza |

Import trafia **do listy, z której go uruchomiono**. Lista wyznacza tabelę, typ nowych obiektów
i kontekst, np. obiekt nadrzędny podlisty. Import do listy głównej jest zapisywany od razu
(zob. [Przebieg importu](#przebieg-importu-transakcja-paczki-wątki)).

## Budowa pliku

- **Wiersz 1:** ścieżki właściwości, a od wiersza 2 dane. Wymagany jest co najmniej jeden wiersz
  danych.
- **XLSX:** czytany jest **pierwszy arkusz**, w zakresie wypełnionych komórek. Liczby i daty
  najlepiej trzymać jako komórki liczbowe i datowe, nie tekst.
- **CSV:** separator rozpoznawany z pierwszego wiersza w kolejności: tabulator, `;`, `,`.
  Kodowanie: UTF-16 albo UTF-8 **ze znacznikiem BOM**. Plik UTF-8 bez BOM straci polskie znaki.
  Wartości są obcinane ze spacji, a cudzysłowy zdejmowane (`""` → `"`). Liczby i daty są
  odczytywane według ustawień regionalnych użytkownika (przecinek dziesiętny).
- **Nagłówek:** ścieżki według [export-excel.md](export-excel.md), sekcja *Zasady budowy
  ścieżek*: `Pole`, `A.B`, `Obiekt:Klucz`, `Obiekt:Class`, `Kolekcja:Pole`, sufiksy wartości
  złożonych. Ta sama ścieżka nie może wystąpić dwa razy.
- Kolumny z nagłówkiem zaczynającym się od `//` są pomijane. Przydają się na dane źródłowe,
  opisy i formuły pomocnicze.
- Pusty nagłówek kolumny jest pomijany.

## Identyfikacja obiektów

Dla każdego wiersza import szuka obiektu w **całej tabeli** listy kolejno po:

| Kolejność | Kolumna | Kiedy używać |
|---|---|---|
| 1 | `Guid` | ponowny import do tej samej bazy (albo baz o wspólnych GUID-ach) |
| 2 | `ID` | tylko ta sama baza; w migracji nie używać |
| 3 | klucz historii + `!Update` | aktualizacja danych historycznych, zob. [Dane historyczne](#dane-historyczne) |
| 4 | wszystkie pola **klucza unikalnego** | migracja i inne bazy: `Kod`, `Symbol`, numer; pole-odwołanie jako `Pole:Klucz` |

Brakujące pola klucza złożonego import bierze z kontekstu listy, np. dokument dla pozycji.
Kolumny użyte do wyszukania nie są potem ustawiane. Gdy nic nie pasuje, działa akcja domyślna:
**nowy obiekt**. Jakie klucze unikalne ma tabela, opisuje jej definicja w `business.xml`
(`/soneta:business-xml`). Więcej o doborze kluczy: [export-excel.md](export-excel.md), sekcja
*Pola kluczowe*.

**Klasa nowego obiektu.** Na listach z wieloma typami obiektów kolumna `Class` (lub `!Class`)
wskazuje typ tworzonego obiektu po nazwie klasy. Bez niej powstaje typ domyślny listy.

## Akcje — dodawanie, edycja, usuwanie

Kolumna `!Action` (lub `!Akcja`) określa, co zrobić z wierszem:

| Wartość | Działanie | Błąd, gdy |
|---|---|---|
| `EditAdd` / `Edytuj Dodaj` (domyślna) | znajdź i zaktualizuj, a gdy brak — dodaj | — |
| `Add` / `Dodaj` | zawsze dodaj nowy | obiekt o tym kluczu już istnieje |
| `Edit` / `Edytuj` | tylko aktualizuj | obiektu nie znaleziono („Nieznaleziony zapis do importu") |
| `Delete` / `Usuń` | usuń znaleziony obiekt | obiektu nie znaleziono |

Akcję podaje się na dwa sposoby:
- **dla całego pliku** w samym nagłówku: `!Action Edit`, a komórki kolumny zostają puste;
- **dla wiersza** w komórce kolumny `!Action`; wartość komórki ma pierwszeństwo przed nagłówkiem.

Akcja działa też **na poziomie odwołania i podlisty**, gdy ścieżkę poprzedza nazwa obiektu:
`Kontrahent:!Action Edit` sprawia, że nieznany kod kontrahenta jest błędem, a nie nowym
kontrahentem.

## Odwołania do innych obiektów

- Kolumna `Obiekt:Klucz` (np. `Kontrahent:Kod`, `Definicja:Symbol`, `Magazyn:Symbol`) wskazuje
  obiekt powiązany. Szukanie odbywa się tak samo: `Guid`, `ID`, klucz unikalny, w całej tabeli
  obiektu powiązanego.
- Odwołanie do typu niejednoznacznego wymaga kolumny `Obiekt:Class` z nazwą typu i kolumny klucza.
- **Nie znaleziono** → przy akcji domyślnej import **tworzy nowy obiekt powiązany** z podanym
  kluczem (albo zgłasza błąd, gdy takiego obiektu nie da się utworzyć). W migracji dodawaj
  `Obiekt:!Action Edit`, aby literówka w kodzie była błędem.
- `-` albo `(null)` w kolumnie klucza **czyści** odwołanie.
- Kolumna `Obiekt.Pole` (z kropką) **zmienia pole obiektu powiązanego** zamiast wskazywać obiekt.
  Używaj jej świadomie albo oznacz ją `//`.
- Obiekty wymagające parametrów przy tworzeniu dostają je z kolumn o nazwie parametru albo
  z kontekstu listy. Gdy utworzenie się nie uda, pojawia się błąd „Błąd tworzenia zapisu".

## Wartości

| Typ | Zapis w komórce |
|---|---|
| Tekst | wprost; `(null)` = tekst pusty |
| Liczba, kwota | liczba (XLSX: komórka liczbowa; CSV: wg ustawień regionalnych) |
| Kwota z walutą, ilość z jednostką | dwie kolumny: `Cena` + `Cena.Waluta`, `Ilosc` + `Ilosc.Jednostka` |
| Okres, ułamek, rok i miesiąc | kolumny `.Od`/`.Do`, `.L`/`.M`, `.Rok`/`.Miesiąc` |
| Data | XLSX: komórka daty; CSV: data wg ustawień regionalnych (np. `01.02.2004`) |
| Czas | XLSX: komórka czasu; CSV: tekst w formacie czasu programu |
| Wartość logiczna | `Tak`/`Nie`, `T`/`N`, `True`/`False`, `Yes`/`No`, `1`/`0`; **inny tekst = Nie** |
| Lista wyboru | podpis lub nazwa wartości, bez rozróżniania wielkości liter |
| Lista tekstów | wartości rozdzielone `\|` |
| Odwołanie | kolumna `Obiekt:Klucz`; `-` lub `(null)` czyści |
| Cecha | kolumna `Features.<Nazwa cechy>` |

- **Pusta komórka = bez zmian.** Pola nie da się wyczyścić pustą komórką (wyjątki: tekst
  `(null)`, odwołanie `-`).
- Wartość złożona jest ustawiana tylko wtedy, gdy co najmniej jedna z jej kolumn jest wypełniona.
- **Cecha historyczna:**

  | Zapis | Działanie |
  |---|---|
  | `wartość` | wartość od daty aktualnej programu |
  | `wartość` + kolumna `Features.<Nazwa>.Date` | wartość od daty z tej kolumny |
  | `+data1\|wartość1\|data2\|wartość2…` | dopisuje zmiany od podanych dat |
  | `\|wartość0\|data1\|wartość1…` | zastępuje całą historię: `wartość0` od początku, dalej zmiany od dat |

## Podlisty

Elementy podlist (pozycje, płatności, dekrety i ich zapisy) wczytuje się razem z obiektem
głównym:

| Definicja:Symbol | Kontrahent:Kod | Pozycje:Towar:Kod | Pozycje:Ilosc | Pozycje:Cena |
|---|---|---|---|---|
| FV | ABC | T-001 | 2 | 10 |
| | | T-002 | 5 | 7,5 |
| FV | XYZ | T-001 | 1 | 10 |

- Wiersz z **pustymi wszystkimi kolumnami obiektu głównego** dopisuje kolejny element podlisty
  do obiektu z wiersza wyżej. Dotyczy to też kolumn `Class`, `!Thread` i `!Update`. Wypełnienie
  którejkolwiek z nich zaczyna nowy obiekt.
- Wielopoziomowo: `Dekrety:Zapisy:Konto:Symbol`. Wiersz z pustymi kolumnami `Dekrety:…`
  dopisuje zapis do bieżącego dekretu.
- Element podlisty jest identyfikowany jak obiekt główny. Obiekt nadrzędny uzupełnia klucz.
  Bez kolumny klucza pozycji (np. `Pozycje:Lp`, `Pozycje:Guid`) każda pozycja jest **dodawana**,
  więc ponowny import tego samego pliku zdubluje pozycje.
- Podlista z elementami różnych typów potrzebuje kolumny `Kolekcja:Class`.
- **Nie łącz podlist z kolumną `!Thread`.** Wiersze kontynuacji muszą trafić do tej samej paczki
  co obiekt główny.
- Alternatywa: wklejenie pliku samej podlisty do podlisty na formularzu obiektu, np. pozycji
  otwartego dokumentu.

## Dane historyczne

Obiekty wersjonowane w czasie (np. dane kadrowe w historii pracownika) importuje się do
**listy zapisów historycznych** z kolumną daty aktualizacji. Kluczem jest obiekt główny, np.
`Pracownik` (kod), a datę podaje kolumna specjalna:

| Kolumna | Brak zapisu na datę | Zakres zmian |
|---|---|---|
| `!Update` / `!Aktualizacja` | tworzy nowy zapis od tej daty | od daty do ostatniego zapisu historii |
| `!UpdateOne` / `!Aktualizacja jedna` | tworzy nowy zapis od tej daty | tylko zapis obowiązujący w tej dacie |
| `!GetUpdate` / `!Aktualny` | nie tworzy; zmienia zapis obowiązujący w dacie | od daty do ostatniego zapisu |
| `!GetUpdateOne` / `!Aktualny jeden` | nie tworzy | tylko zapis obowiązujący w dacie |

```text
Pracownik  !Update      Etat.Stanowisko
006        2004-02-01   Kierownik
006        2001-09-03   Specjalista
```

Kolumna aktualizacji na liście, której wiersze nie są zapisami historycznymi, kończy się błędem
„Zapis nie jest zapisem historycznym".

## Kolumny specjalne — ściąga

| Nagłówek | Działanie |
|---|---|
| `Class`, `!Class` | typ tworzonego obiektu (listy wielotypowe) |
| `!Action`, `!Akcja`; `!Action <akcja>` | akcja wiersza / domyślna akcja pliku |
| `!Ignore`, `!Ignoruj` | wypełniona komórka → wiersz pomijany, bez błędu |
| `!Importing`, `!Importuj` | **pusta** komórka → wiersz pomijany (odwrotność `!Ignore`) |
| `!Problem`, `!Problemy` | wypełniona komórka → wiersz pomijany **i liczony jako błąd**; dodatkowo raport „Problem" (zob. [Raportowanie błędów](#raportowanie-błędów)) |
| `!Update`, `!UpdateOne`, `!GetUpdate`, `!GetUpdateOne` | aktualizacja historii od daty, zob. wyżej |
| `!Thread`, `!Wątek` | import wielowątkowy, zob. niżej |
| `//nazwa` | kolumna pomijana |
| `Obiekt:!Action …` | akcja dla odwołania lub podlisty |

Polskie i angielskie nazwy są równoważne.

## Przebieg importu: transakcja, paczki, wątki

**Lista główna** (otwarta z menu):

- Cały import działa w **jednej transakcji bazy**. Zatwierdza się ją tylko wtedy, gdy nie było
  żadnego błędu. Inaczej nic nie zostaje zapisane.
- Wiersze są wczytywane w **paczkach po 2000** w osobnych sesjach (stała pamięć przy dużych
  plikach). Paczka nie rozdziela obiektu i jego wierszy kontynuacji. Po każdej paczce wykonują
  się zdarzenia sesji i weryfikatory, a potem zapis paczki w ramach transakcji.
- Po pierwszym błędzie kolejne wiersze są nadal wczytywane i sprawdzane (wartości pól,
  weryfikatory obiektu), ale nic nie jest już zapisywane. Log pokaże więc błędy z całego
  pliku, a nie tylko pierwszy.
- **`!Thread` / `!Wątek`:** wiersze o tej samej wartości trafiają do jednej sesji i wątku.
  Wiersze o różnych lub pustych wartościach mogą iść równolegle w paczkach po 500. To nadal
  jedna transakcja. Wartość ustaw tak, aby obiekty zależne (np. ten sam kontrahent, ten sam
  dokument) były w jednym wątku. Nie stosuj z podlistami.

**Podlista na formularzu obiektu** (sesja edycyjna formularza):

- dane trafiają do formularza bez zapisu w bazie; przy błędzie zmiany z pliku są wycofywane
  i pojawia się komunikat;
- weryfikatory nie są sprawdzane po każdym wierszu, tylko przy zapisie formularza przez operatora.

## Weryfikacja obiektów biznesowych

Import sprawdza obiekt na czterech etapach, tak jak przy wprowadzaniu ręcznym:

| Etap | Co sprawdza | Typowy komunikat |
|---|---|---|
| Ustawienie wartości | konwersja typu, blokada pola, logika właściwości | „Błąd wczytywania wartości pola…", „Próba ustawienia pola … tylko do odczytu", „Błąd ustawiania wartości pola…" |
| Koniec obiektu (lista główna) | weryfikatory typu *błąd* obiektu i jego podlist, czyli te same reguły, które blokują zapis formularza | treść weryfikatora z numerem wiersza |
| Koniec paczki | zdarzenia sesji (np. numeracja, przeliczenia zależne) i ponownie weryfikatory | treść weryfikatora / wyjątku |
| Zapis | ograniczenia bazy, unikalność, równoległe zmiany innych użytkowników | komunikat zapisu |

Weryfikatory typu *ostrzeżenie* nie zatrzymują importu i nie trafiają do logu programu. Sprawdź
je po imporcie na liście lub formularzu obiektów. Import przez MCP zwraca je w `warnings`.

## Raportowanie błędów

- **Log „Excel"** (okno logu programu) dostaje wpis dla każdego błędu. Format wybiera opcja
  *Logowanie błędów*:

  | Tryb | Postać wpisu |
  |---|---|
  | Standardowy z opisami | `Błąd w wierszu: N; komunikat` |
  | W formacie CSV | `N⇥obiekt⇥komunikat` (kolumny rozdzielone tabulatorem, do wklejenia w arkusz) |
  | Brak logowania | brak wpisów |

  **N to numer wiersza danych** (1 = pierwszy wiersz pod nagłówkiem), czyli wiersz arkusza N+1.
- Przy jednym błędzie program pokazuje jego komunikat. Przy wielu — komunikat zbiorczy
  „Wystąpiły błędy … Szczegóły znajdują się w logu 'Excel'".
- Komunikat błędu wiersza zawiera numer wiersza i pierwsze pięć wartości z wiersza, co ułatwia
  odnalezienie danych.
- **Raport „Problem" — praca iteracyjna.** Gdy plik ma kolumnę `!Problem` (może być pusta),
  po imporcie program wypisuje do logu „Problem" kolumnę gotową do wklejenia: nagłówek `!Problem`
  i dla każdego wiersza danych treść jego błędów, a dla poprawnych wierszy pustą linię. Do tego
  dochodzi sekcja błędów niezwiązanych z wierszem. Procedura:
  1. dodaj pustą kolumnę `!Problem` i uruchom import;
  2. wklej raport „Problem" w kolumnę `!Problem` arkusza — błędne wiersze są opisane;
  3. popraw dane w opisanych wierszach i wyczyść ich komórki `!Problem`;
  4. powtórz. Wiersz z wypełnionym `!Problem` jest pomijany, ale nadal liczy się jako błąd,
     więc import zapisze dane dopiero przy pustej kolumnie. Żeby świadomie pominąć wiersze,
     użyj `!Ignore`.

**Komunikaty i przyczyny:**

| Komunikat | Przyczyna | Naprawa |
|---|---|---|
| Brak danych do wklejenia… | brak nagłówka albo wierszy danych | nagłówek + co najmniej 1 wiersz |
| Błędna ścieżka dostępu do danych / listy | ścieżka z `:` przez pole, które nie jest obiektem ani podlistą | popraw nagłówek |
| Istnieje już pole … w liście | zdublowana kolumna | usuń duplikat |
| Błąd wczytywania pola … | nieistniejąca właściwość w ścieżce | nazwa właściwości z listy / `scan-props` |
| Błąd wczytywania wartości pola … typu … | wartość nie pasuje do typu (data, liczba, lista wyboru) | format wartości, ustawienia regionalne CSV |
| Próba ustawienia pola … tylko do odczytu | pole wyliczane lub zablokowane w tym stanie obiektu | usuń kolumnę albo zmień kolejność (pole odblokowuje się po innym) |
| Błąd ustawiania wartości pola … | logika biznesowa odrzuciła wartość | komunikat wewnętrzny wskazuje regułę |
| Nieznaleziony zapis do importu | `Edit`/`Delete` bez dopasowania; odwołanie z `!Action Edit` | klucz, akcja |
| Błąd tworzenia zapisu | `Add` przy istniejącym kluczu albo brak parametrów utworzenia | akcja, kolumny parametrów, kontekst listy |
| Nie można dodawać danych typu … | obiektu nie da się utworzyć (np. nieznane odwołanie) | popraw klucz odwołania |
| Nieokreślony typ danych / Nie można określić typu | brak `Class` przy typie niejednoznacznym | dodaj `Class` / `Obiekt:Class` |
| Zapis … nie jest zapisem historycznym | `!Update` na liście niehistorycznej | lista zapisów historycznych |
| Niepoprawny format danych historycznych… | błędny zapis `\|…` / `+…` cechy historycznej | pary `data\|wartość`, daty `RRRR-MM-DD` |
| Wiersz danych N ze problemem … | wypełniona komórka `!Problem` | popraw i wyczyść komórkę |

## Opcje konfiguracji

| Opcja (Narzędzia → Opcje) | Znaczenie dla importu |
|---|---|
| *Systemowe / Eksport i import* → **Logowanie błędów** | format logu „Excel": standardowy, CSV albo brak |
| *Systemowe / Eksport i import* → **Korzystaj z API Excela podczas eksportu i importu do csv** | CSV czytany mechanizmem arkusza (kodowanie UTF-16, ustawienia regionalne użytkownika) zamiast prostego czytnika tekstu |
| *Systemowe / Zmiany ustawienia* → **Importowanie danych** | rejestracja każdego importu w historii zmian: sama informacja albo informacja z treścią pliku |
| Prawo **„Lista - Wklej"** | pozwolenie na import do list |

Tryb próbny jest dostępny przez MCP (sekcja niżej). Wielkość paczek i sprawdzanie
weryfikatorów po wierszu można ustawić tylko przy imporcie wywołanym z kodu
(`/soneta:programming`). W programie przebiegiem sterują
[kolumny specjalne](#kolumny-specjalne--ściąga).

## Import próbny przez MCP

Metoda `import_folder_list` pakietu „Import i eksport danych" wczytuje plik do listy folderu
programu (`programFolderPath`, np. `Handel/Sprzedaż`). Lista jest budowana od domyślnych
filtrów widoku i zawężana przez `filters`, które wyznaczają kontekst importu (np. definicję
nowych dokumentów). Plik przekazuje się w `dataFileName` (czyta go klient) albo w `dataBase64`.
`format` (`xlsx`/`csv`) jest rozpoznawany z treści.

**`testing=true` wykonuje cały import**: ustawia wartości, sprawdza weryfikatory po każdym
obiekcie, uruchamia zdarzenia sesji i **zapisuje paczki do bazy w transakcji, która na końcu
jest wycofywana**. Próba wykrywa więc także błędy zapisu (unikalność, numeracja, ograniczenia
bazy), których nie wykryje próba importu XML. Nic nie zostaje zapisane ani odnotowane w historii
zmian.

| Pole odpowiedzi | Znaczenie |
|---|---|
| `testing`, `saved` | tryb próbny; czy dane zapisano (`false` przy próbie i przy każdym błędzie) |
| `rowsChanged` | liczba wierszy pliku do zmiany (próba) lub zmienionych |
| `message` | podsumowanie z liczbą ostrzeżeń |
| `errors` | `{row, message}`; `row` = **wiersz pliku jak w arkuszu** (nagłówek = 1); brak `row` = błąd paczki lub zapisu |
| `warnings` | ostrzeżenia weryfikatorów `{row, message}`, których program w oknie nie pokazuje |

Listy są posortowane po wierszu i obcięte do 50 pozycji. Popraw pierwsze błędy i powtórz próbę.
Na czas próby zapisywane tabele są zablokowane transakcją, dlatego duże pliki testuj poza
godzinami pracy. Wymagane prawa: „Lista - Wklej" oraz prawo roli do danych operacyjnych lub
konfiguracyjnych. Wspólne zasady metod MCP: [mcp-import-export.md](mcp-import-export.md).

## Migracja danych z innego systemu

1. **Inwentaryzacja obiektów docelowych.** Ustal listy programu, do których trafią dane, i ich
   właściwości (`scan-props`, `/soneta:programming`). Kolejność pól ustal z formularza
   (`scan-forms`).
2. **Szablon z eksportu.** Ustaw kolumny listy w kolejności formularza, z kolumną klucza.
   Wyeksportuj kilka obiektów z nagłówkiem ścieżek ([export-excel.md](export-excel.md)). Tak
   powstaje gotowy nagłówek.
3. **Mapowanie danych źródłowych** na kolumny szablonu. Dane pomocnicze ze starego systemu trzymaj
   w kolumnach `//`. Identyfikator ze starego systemu warto zachować w cesze (`Features.<Nazwa>`)
   dla śledzenia pochodzenia.
4. **Kolejność plików:** słowniki i ustawienia (np. przez [import-xml.md](import-xml.md) albo
   rejestr konfiguracji) → kartoteki (kontrahenci, towary, pracownicy) → dane historyczne →
   dokumenty z podlistami → salda i rozrachunki.
5. **Odwołania twarde:** kolumny `Obiekt:!Action Edit` przy każdym odwołaniu, żeby nieznany kod
   był błędem, a nie nowym obiektem.
6. **Próba:** przez MCP `import_folder_list` z `testing=true`, iteracyjnie do zera `errors`.
   Bez MCP próbuj na kopii bazy (kopia i odtworzenie: `dbmgr`, `/soneta:tools`) z pustą kolumną
   `!Problem` według [Raportowania błędów](#raportowanie-błędów).
7. **Duże pliki:** dziel na pliki po kilkadziesiąt tysięcy wierszy (jedna transakcja na plik).
   Dla niezależnych obiektów bez podlist rozważ `!Thread`.
8. **Import właściwy** na bazie docelowej, z włączoną rejestracją importu w historii zmian.
9. **Weryfikacja po imporcie** (zob. checklistę niżej).

## Checklisty

**Przed importem:**
- [ ] Nagłówek to ścieżki właściwości; brak zdublowanych kolumn; kolumny pomocnicze z `//`.
- [ ] Kolumna klucza (`Guid` dla tej samej bazy, klucz naturalny dla innej); przy kluczu
      złożonym wszystkie pola.
- [ ] Kolejność kolumn jak na formularzu; pola sterujące przed zależnymi.
- [ ] Odwołania jako `Obiekt:Klucz`; obiekty powiązane istnieją; w migracji `Obiekt:!Action Edit`.
- [ ] Brak kolumn `Obiekt.Pole`, chyba że zmiana obiektu powiązanego jest zamierzona.
- [ ] Wartości: daty i liczby typowane (XLSX) albo zgodne z ustawieniami regionalnymi (CSV);
      logiczne `Tak`/`Nie`; listy wyboru zgodne z podpisami.
- [ ] Puste komórki świadomie: nie czyszczą wartości.
- [ ] Podlisty: puste kolumny obiektu głównego (także `Class`, `!Thread`, `!Update`) w wierszach
      kontynuacji; klucz pozycji, jeśli plik będzie wczytywany ponownie.
- [ ] CSV: UTF-16 albo UTF-8 z BOM.
- [ ] Lista docelowa i jej kontekst (filtr, obiekt nadrzędny) właściwe; prawo „Lista - Wklej".

**Migracja:**
- [ ] Próba: MCP `import_folder_list` z `testing=true` do zera `errors` (ostrzeżenia przejrzane);
      bez MCP — kopia bazy i iteracje z kolumną `!Problem`.
- [ ] Kolejność plików: słowniki → kartoteki → historia → dokumenty.
- [ ] Rejestracja importu w historii zmian włączona.
- [ ] Kopia bazy docelowej przed importem właściwym.

**Po imporcie:**
- [ ] Liczba obiektów na liście zgodna z liczbą obiektów w pliku (komunikat z liczbą zmienionych
      zapisów, filtr listy).
- [ ] Ponowny eksport tych samych kolumn i porównanie z plikiem źródłowym.
- [ ] Ostrzeżenia weryfikatorów przejrzane na obiektach (nie trafiają do logu importu).
- [ ] Losowe obiekty otwarte na formularzu — wartości wyliczane i podlisty poprawne.
- [ ] Brak duplikatów: ponowny import tego samego pliku nie dodaje obiektów ani pozycji.

## Powiązania

- [export-excel.md](export-excel.md) — budowa pliku, ścieżki nagłówka, pola kluczowe, szablon
  importu z eksportu.
- [mcp-import-export.md](mcp-import-export.md) — eksport i import przez MCP, porównanie trybów próbnych.
- [import-xml.md](import-xml.md) — import XML (według rekordów lub przez logikę biznesową;
  słowniki, konfiguracja, `*.dbinit.xml`).
- [config-reg.md](config-reg.md) — przenoszenie konfiguracji między bazami przed migracją danych.
- `/soneta:programming` — *scan-props* (nazwy właściwości do ścieżek), *scan-forms* (kolejność
  pól formularza), *verifiers* (weryfikatory, które blokują zapis), import z kodu (tryb próbny,
  paczki).
- `/soneta:business-xml` — klucze unikalne tabel (identyfikacja wierszy).
- `/soneta:tools` — `dbmgr` (kopia bazy do prób migracji), `buscall` (weryfikacja na
  działającej aplikacji).
- [SKILL.md](../SKILL.md) — mapa tego skilla.
