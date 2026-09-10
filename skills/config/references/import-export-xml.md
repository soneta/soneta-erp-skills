# Import i eksport danych oraz ustawień konfiguracyjnych przez pliki XML

Platforma Soneta wczytuje i zapisuje dane przez pliki XML o wspólnym schemacie. Jeden format
obsługuje trzy zastosowania: **import według rekordów** (dane konfiguracyjne, pliki `*.dbinit.xml`,
bazy demo), **import przez logikę biznesową** (dokumenty i dane operacyjne z pełną walidacją)
oraz **eksport** wskazanych rekordów wraz z powiązanymi danymi (datapack).

## Zasada nadrzędna — najpierw odczytaj rzeczywistą strukturę, potem generuj XML

**Weryfikacja nazw pól, typów, wartości enum i struktury to zadanie AGENTA, nie operatora.**
Nie zgaduj i **nie deleguj sprawdzenia użytkownikowi** — nie kończ pliku listą „do potwierdzenia
przez Ciebie" ani prośbą, by operator zweryfikował pola. Operator zna proces biznesowy; ustalenie
dokładnych nazw pól, ich typów i tego, czy pole jest kolekcją czy subrowem, należy do agenta
i robi się to **skanem DLL**. Zanim zbudujesz jakikolwiek plik importu, **obowiązkowo** odczytaj
rzeczywistą strukturę docelowego obiektu z bibliotek DLL — to część zadania, nie krok opcjonalny:

- **`scan-props`** ([programming](../../programming/SKILL.md)) — pola i właściwości obiektu oraz jego podkolekcje.
  Tryb *według rekordów* → tylko pola z `Rodzaj = bazodanowe`; tryb *biznesowy* → właściwości
  biznesowe. Stąd bierzesz **dokładne nazwy** pól, typy i dozwolone wartości (enum).
- **`scan-forms`** ([programming](../../programming/SKILL.md)) — zakładki, sekcje danych i **kolejność pól**
  formularza; w trybie `business="true"` kolejność wprowadzania = kolejność pól na formularzu.
  Stąd bierzesz kolejność elementów i to, co logicznie stanowi „dane obiektu" (zakres eksportu).

Dopiero na tej podstawie generuj XML. Samodzielny odczyt skanami jest **domyślną i wymaganą**
drogą; prośba do operatora o weryfikację pól to sygnał, że pominięto skan. Dostęp do DLL jest
częścią środowiska pracy — a gdy naprawdę go brak, jawnie to powiedz i oznacz nazwy jako
niezweryfikowane, zamiast zlecać ich sprawdzenie użytkownikowi.

**Rozgranicz dwie rzeczy:** weryfikacja struktury skanem (`scan-props`/`scan-forms`) jest
**read-only i zawsze po stronie agenta** — wchodzi w budowę pliku. Natomiast **próbny import do
bazy (`dbmgr importxml`) i odczyt efektu na żywej aplikacji (`buscall`) modyfikują bazę /
uruchamiają program — wykonuje je agent, ale dopiero na wyraźne żądanie użytkownika** (nie
importuj samowolnie po samym zbudowaniu pliku). Kolejność pracy: `scan-props`/`scan-forms` →
budowa pliku; a gdy użytkownik zleci test — `dbmgr importxml` → odczyt efektu z programu
(zob. *Testowanie plików XML*).

## Szkielet pliku

```xml
<?xml version="1.0" encoding="utf-8"?>
<session xmlns="http://www.soneta.pl/schema/business">
  <NazwaObiektu guid="...">       <!-- element = nazwa obiektu biznesowego -->
    <Pole>wartość</Pole>
    <Kolekcja>                    <!-- podkolekcja obiektu -->
      <ObiektPodrzedny> ... </ObiektPodrzedny>
    </Kolekcja>
  </NazwaObiektu>
</session>
```

- Element główny: `<session xmlns="http://www.soneta.pl/schema/business">`.
- `business="true"` na `<session>` → **import przez logikę biznesową**; bez tego atrybutu
  (lub `business="false"`) → **import według rekordów**. Atrybut `business` można też
  postawić na pojedynczym rekordzie lub polu — lokalnie przełącza tryb.
- Elementy bezpośrednio w `<session>` noszą **nazwy obiektów biznesowych** (nazwa logiczna,
  l.poj. — np. `FormaPrawna`, `DokumentHandlowy`; nie nazwa tabeli SQL); ich elementy
  podrzędne to **pola/właściwości** obiektu albo **nazwy kolekcji** obiektów podrzędnych.
- `fromto="..."` na `<session>` ogranicza okres przetwarzania kolekcji datowanych
  (`fromto="(wszystko)"` = bez ograniczenia).

## Który tryb wybrać

| Sytuacja | Tryb |
|---|---|
| Dane konfiguracyjne, słowniki, definicje (brak złożonej logiki biznesowej) | według rekordów |
| Inicjowanie bazy (`*.dbinit.xml`), konwersje ustawień, baza demo/testowa | według rekordów |
| Dane kartotekowe w modelu „root + historia" (np. pracownik, zapisy „od–do") | według rekordów |
| Dokumenty i dane operacyjne wymagające walidacji (np. dokumenty handlowe) | przez logikę biznesową |
| Obiekty ze złożoną logiką biznesową (przeliczenia, stany, zależności pól) | przez logikę biznesową |

Kryterium **nie jest** podział „konfiguracja vs dane", lecz to, czy zapis obiektu wymaga
przeliczeń i walidacji zależnych pól, których nie wolno pominąć. Tak robią pliki standardowe:
dane kadrowe bazy demo idą według rekordów, tryb biznesowy występuje tylko przy dokumentach.
Gotowe wzorce dla typowych obiektów: [import-xml-examples.md](import-xml-examples.md).

## Identyfikacja rekordu

Głównym nośnikiem tożsamości jest **GUID** — importować i eksportować w całości można
**rekordy guidowane** (zob. artykuł *datapack-guidedrow* w [programming](../../programming/SKILL.md)). Sposoby
wskazania rekordu, w kolejności rozpoznawania:

| Atrybut | Działanie |
|---|---|
| `where="Pole=wartość"` | znajdź **istniejący** rekord po polu z kluczem; **błąd**, gdy brak lub gdy wiele pasuje |
| `key="Pole=wartość"` | jak `where`, ale gdy brak — **tworzy nowy**; jeśli podano też `guid`, nadpisuje GUID znalezionego |
| `guid="..."` | rekord o tym GUID: istnieje → aktualizacja, nie istnieje → nowy z tym GUID-em |
| *(brak)* | zawsze nowy rekord |

Dodatkowo `id="..."` nadaje rekordowi **identyfikator lokalny w pliku** (unikalny — duplikat
to błąd), do użycia w referencjach dalej w tym samym pliku.

### Referencje do innych rekordów

Wartość pola wskazującego inny rekord zapisuje się tekstowo:

| Forma | Znaczenie |
|---|---|
| `00000000-0009-0013-0001-000000000000` | GUID rekordu obiektu docelowego |
| `Kontrahent:ba66f540-...` | GUID z jawnym wskazaniem miejsca docelowego — **nazwą obiektu lub tabeli** (`Kontrahent:` / `Kontrahenci:`); wymagane przy referencjach polimorficznych/interfejsowych |
| `FormaPrawna_1` | identyfikator lokalny (`id`) rekordu zdefiniowanego w tym pliku |
| `#123` | wewnętrzny numer ID rekordu — **unikać** w plikach przenośnych między bazami |
| *(pusto)* | brak referencji (null) |

W trybie przez logikę biznesową referencję można też wskazać elementem zagnieżdżonym
z `where`: `<Kontrahent where="Kod=Abc" />`.

## Formaty wartości w elementach

Wartości zapisywane są w kulturze niezmiennej (invariant) — niezależnie od ustawień
regionalnych:

| Typ | Format | Przykład |
|---|---|---|
| Tekst | wprost; pusty element `<Pole />` = pusty tekst | `<Nazwa>Spółka z o.o.</Nazwa>` |
| Liczba | kropka dziesiętna, bez separatorów tysięcy | `<Kurs>4.1234</Kurs>` |
| Data | `RRRR-MM-DD` | `<Data>2014-09-01</Data>` |
| Okres (od–do, `FromTo`) | `od...do` (separator `...`); puste „od"/„do" = zakres otwarty; `(wszystko)` = bez ograniczeń; `(pusty)` = brak okresu | `<Okres>2026-01-02...</Okres>`, `<Aktualnosc>(wszystko)</Aktualnosc>` |
| Logiczny | `True` / `False` (wielkość liter dowolna) | `<Domyslna>True</Domyslna>` |
| Enum | nazwa wartości | `<Stan>Zatwierdzony</Stan>` |
| Kwota z walutą | liczba + kod waluty | `<Cena>5.13 PLN</Cena>` |
| Ilość z jednostką | liczba + jednostka | `<Ilosc>120 m</Ilosc>` |
| Ułamek/współczynnik | `licznik/mianownik` | `<Wspolczynnik>1/1</Wspolczynnik>` |
| Lista tekstów | wartości rozdzielone `\|` | `<KodyGUS>023\|999</KodyGUS>` |
| Dane binarne | Base64 | — |
| Referencja | zob. wyżej | `<SposobZaplaty>00000000-0003-...</SposobZaplaty>` |

Pole złożone (subrekord, np. numer rachunku bankowego) zapisuje się elementem zagnieżdżonym
z własnymi polami. Wartości prostych pól można też podawać **atrybutami** elementu rekordu
(`<Towar Kod="ABC">`), a w trybie biznesowym atrybuty służą ponadto do przekazania parametrów
tworzenia obiektu.

## Atrybuty specjalne — ściąga

| Atrybut | Gdzie | Działanie |
|---|---|---|
| `business` | session / rekord / pole | `true` = tryb przez logikę biznesową |
| `fromto` | session | okres przetwarzania kolekcji datowanych |
| `guid`, `where`, `key`, `id` | rekord | identyfikacja — zob. wyżej |
| `class` | rekord | podtyp selektora w tabeli z selektorem (oba tryby) — zob. sekcje o selektorze w Części 1 i 2 |
| `deleted="True"` | rekord | kasuje wskazany rekord (bez treści elementu) |
| `updateonly="true"` | rekord | tylko aktualizacja — gdy rekord nie istnieje, pomiń |
| `insertonly="true"` | rekord | tylko nowy — gdy rekord istnieje, pomiń |
| `dbversion` | rekord | wczytaj tylko przy konwersji do wersji ≥ tej wartości (pliki dbinit) |
| `addnew="true"` | kolekcja | tylko dopisuj — nie kasuj istniejących elementów kolekcji |
| `relationsimportmode="update"` | kolekcja | aktualizuj po GUID zamiast zastępować |
| `duplicate="Number"` | pole | przy konflikcie unikalności dołóż przyrostek ` 2`, ` 3`… |
| `duplicateKeyField="Pole"` | pole (obok `duplicate`) | drugie pole klucza unikalności złożonego (np. nazwa unikalna w obrębie tabeli) |
| `date` | rekord w kolekcji historycznej | aktualizacja **od tej daty** — cięcie okresu: nowy zapis (klon poprzedniego z nadpisanymi polami), poprzedni zostaje do dnia przed. Zob. *Aktualizacja historyczna* |
| `ctor` | rekord (tryb biznesowy) | wybór wariantu tworzenia obiektu |
| `priority`, `versionName` | session (dbinit) | zob. sekcję o dbinit |

---

## Część 1 — Import według rekordów (`business="false"`, domyślny)

Dane trafiają **bezpośrednio do pól rekordów**, z pominięciem logiki biznesowej. Właściwy dla
danych konfiguracyjnych i inicjujących, gdzie logika biznesowa nie istnieje lub nie jest
potrzebna. **Nie używać** na obiektach ze złożoną logiką biznesową — pominięcie jej może
zostawić dane niespójne.

Zasady:

- Używaj wyłącznie **pól bazodanowych** rekordu — nie właściwości kalkulowanych ani innych
  properties dodanych w obiektach biznesowych. Pola obiektu inwentaryzuje narzędzie
  `scan-props` ze skilla [programming](../../programming/SKILL.md) — w tym trybie importu interesują nas tylko
  pola oznaczone w kolumnie `Rodzaj` znacznikiem **`bazodanowe`**.
- **Kolejność elementów pól nie ma znaczenia** — wartości trafiają wprost do rekordu.
- Obiekty przystosowane do tego trybu mają metody `OnImporting`/`OnImported` — po zakończeniu
  wczytywania pól rekordu wywoływana jest `OnImported`, która uzupełnia skutki logiki
  biznesowej (opis w artykule *row-types* skilla [programming](../../programming/SKILL.md)).
- Podkolekcje (np. pozycje rekordu nadrzędnego) wczytuje się elementem o **nazwie kolekcji**;
  powiązanie z rodzicem realizują klucze bazodanowe relacji. Wewnątrz kolekcji `where`/`key`
  wyszukują w obrębie elementów tego rodzica.

```xml
<session xmlns="http://www.soneta.pl/schema/business">
  <FormaPrawna id="FormaPrawna_1" guid="00000000-0009-0013-0001-000000000000">
    <Kod>NO</Kod>
    <Nazwa>Nieokreślona</Nazwa>
    <Domyslna>true</Domyslna>
  </FormaPrawna>
</session>
```

### Wiersze tabel z selektorem — atrybut `class` i obowiązkowy element selectora

Tabele z kolumną `selector="true"` przechowują w jednej tabeli wiele typów wierszy (podklas
rejestrowanych w kodzie). Import rekordowy takich wierszy rządzi się trzema regułami:

- **Element XML wiersza to zawsze nazwa typu wiersza tabeli** (typ bazowy), nigdy nazwa podklasy
  selektorowej — element o nazwie podklasy kończy się błędem „Tabela nieznaleziona".
- Podklasę wybiera atrybut **`class="Pełny.Typ.Podklasy,Assembly"`** — działa także w imporcie
  rekordowym (nie tylko `business="true"`); jest konieczny m.in. gdy typ bazowy wiersza jest
  abstrakcyjny.
- **Element kolumny selectora jest OBOWIĄZKOWY mimo atrybutu `class`** — import rekordowy tworzy
  wiersz z pustym rekordem i nie wyprowadza wartości selectora z rejestracji podklasy; `class`
  steruje wyłącznie klasą tworzonego obiektu. Bez elementu selector zapisze się jako `0`.

```xml
<!-- tabela PozycjeDefinicji: typ wiersza PozycjaDefinicji, selector Rodzaj,
     podklasy zarejestrowane w kodzie -->
<PozycjaDefinicji class="MojaFirma.Modul.PozycjaSpecjalna,MojaFirma.Modul">
  <Rodzaj>Specjalna</Rodzaj>   <!-- selector: OBOWIĄZKOWY mimo class -->
  <Nazwa>...</Nazwa>
</PozycjaDefinicji>
```

**Wiersz z selectorem = 0 zatruwa całą tabelę:** każda materializacja dowolnego wiersza kończy
się `UnrecognizedRowException` („Selektor 0 w tabeli X nieznaleziony") — w tym import naprawczy.
Jedyna naprawa to usunięcie wierszy wprost w SQL (`DELETE FROM Tabela WHERE KolumnaSelectora = 0`).
Dlatego projektując tabelę z selectorem, enum dyskryminatora numeruje się **od 1** — reguły
i pułapki po stronie definicji tabeli opisuje [business-xml](../../business-xml/SKILL.md) (table-reference, pole
selector; tam też pułapka „Nierozpoznany typ wiersza").

Checklista wierszy z selectorem:
- [ ] element = nazwa typu wiersza tabeli (nie podklasy)
- [ ] `class="Typ,Assembly"` przy podklasach selektorowych
- [ ] element kolumny selectora w KAŻDYM wierszu
- [ ] po imporcie: kontrola `SELECT KolumnaSelectora, COUNT(*)` — brak wartości 0

### Zachowanie kolekcji przy aktualizacji rekordu

Gdy rekord nadrzędny już istnieje, element kolekcji domyślnie **zastępuje** jej zawartość:
istniejące elementy są kasowane (guidowane rekordy standardowe pozostają), po czym wczytywane
są elementy z pliku. Modyfikatory: `addnew="true"` (tylko dopisywanie),
`relationsimportmode="update"` (aktualizacja po GUID; elementy nieobecne w pliku są kasowane
po zakończeniu), `fromto` (kasowanie ogranicza się do okresu).

**Detale (wiersze podrzędne) zapisuj zagnieżdżone w rodzicu, bez guidów.** Tabele detali nie
powinny być guidowane (guided to obiekty główne — zob. *datapack-guidedrow*
w [programming](../../programming/SKILL.md)), a wiersze **nieguidowane importowane top-level tworzą duplikaty** przy
każdym reimporcie — nie ma ich po czym rozpoznać. Poprawny zapis detali w plikach dbinit/demo to
**obiekty zagnieżdżone w rodzicu**: wrapper o nazwie kolekcji `children` z definicji relacji
(business.xml), w środku elementy typu wiersza detalu:

```xml
<Definicja id="..." guid="...">
  <Nazwa>...</Nazwa>
  <Pozycje>                       <!-- nazwa kolekcji children z business.xml -->
    <PozycjaDefinicji>...</PozycjaDefinicji>
  </Pozycje>
</Definicja>
```

Ponieważ import kolekcji **zastępuje** nieguidowane dzieci rodzica, reimport jest idempotentny
bez nadawania guidów detalom. Uwaga: **wewnątrz elementu kolekcji nie wolno umieszczać komentarzy
XML** — czytnik akceptuje wyłącznie elementy; komentarz = `XmlException`. Komentarze umieszczaj
przed/po kolekcji. Idempotencję weryfikuj **podwójnym** `dbmgr importxml` + policzeniem rekordów.

**Kolekcje historyczne (zapisy „od–do") wymagają `addnew="true"`.** Kolekcja przechowująca
zapisy historyczne obiektu (np. historia danych kadrowych pracownika) nie znosi domyślnej
podmiany: kasowanie zawartości przy ponownym wczytaniu usuwałoby zapisy historii, a **ostatniego
(jedynego) zapisu historii skasować nie można** — import kończy się wtedy błędem. Aby plik był
**idempotentny** (bezpieczny do ponownego wczytania i przenoszenia między bazami), na kolekcji
historycznej ustaw `addnew="true"` i nadaj rekordom historycznym **stały `guid`** — wtedy zapis
o istniejącym GUID jest aktualizowany, a nie kasowany. `relationsimportmode="update"` **nie
wystarcza** dla kolekcji historycznych.

### Aktualizacja historyczna — zmiana wartości „od dnia" (`date`)

Aby zmienić wartość **od wskazanej daty** (a nie nadpisać bieżący zapis), na rekordzie
w kolekcji historycznej ustaw `date="RRRR-MM-DD"`. Silnik wykonuje wtedy **cięcie okresu**:
dotychczasowy zapis obowiązuje do dnia poprzedzającego, a od podanej daty powstaje **nowy
zapis** — klon poprzedniego z nadpisanymi polami. W treści rekordu podaj **tylko pola, które
się zmieniają**; pozostałe (np. stanowisko, wymiar, powiązania) przechodzą z klonowanego zapisu.

- Łącz z `addnew="true"` na kolekcji — chroni pozostałe zapisy historii przed skasowaniem.
- `date` (nowy zapis **od daty**, „od dnia") vs `guid` na zapisie (**nadpisanie istniejącego**
  zapisu, zmiana „wstecz", bez cięcia okresu) — świadomie wybierz jedno.
- Identyfikuj obiekt nadrzędny po kluczu naturalnym (`where="Pole=wartość"`); gdy warunek
  pasuje do wielu rekordów, import zgłasza błąd — doprecyzuj klucz.

```xml
<session xmlns="http://www.soneta.pl/schema/business" fromto="(wszystko)">
  <Obiekt where="Klucz=wartość">
    <Historia addnew="true">
      <ZapisHistoryczny date="2026-08-01">
        <!-- tylko pole, które zmieniamy; reszta klonuje się z poprzednika -->
        <Pole>nowa wartość</Pole>
      </ZapisHistoryczny>
    </Historia>
  </Obiekt>
</session>
```

Zweryfikowane importem na bazie demo (zmiana stawki zaszeregowania pracownika od 1. dnia
miesiąca): powstał nowy zapis „ważny od" tej daty ze zmienioną stawką, a poprzednia stawka
pozostała na zapisie obowiązującym do dnia poprzedzającego. Efekt widać w programie na
zakładce **Historia zapisów** obiektu (kolumna „Ważny od" + zmienione pole). Gotowy plik:
[examples/aktualizacja-historyczna-stawka.xml](../examples/aktualizacja-historyczna-stawka.xml).

### Cechy (features)

```xml
<features>
  <feature name="Asortyment">Kraj</feature>
</features>
```

Wartością cechy referencyjnej jest GUID (lub `Obiekt:GUID`); cechy historyczne przyjmują
wartość obowiązującą od daty importu.

### Nadawanie praw obiektowych — rekord `<Right>`

Obiekt będący **źródłem praw** (`IRightsSource`) jest dla ról domyślnie **Denied** — funkcja
oparta o taki rekord jest martwa po utworzeniu bazy, dopóki prawo nie zostanie nadane (kontekst
i pułapki: artykuł [rights-source](../../programming/references/rights-source.md)). W dbinit/demo prawo nadaje rekord
`Right`, wiążący uprawnienie (`Entitle`) ze źródłem praw (`Source`); trzecia kolumna
`ReadOnlyRight` (bool) oznacza prawo tylko do odczytu.

```xml
<Right dbversion="26100000">
  <Entitle>Entitle:00000000-0015-0001-0001-000000000000</Entitle>
  <Source>MojaDefinicja:00000000-0AB1-0003-0001-000000000000</Source>
</Right>
```

- Kolumny typu interfejsowego (`IEntitle`, `IRightsSource`) serializują się w formacie
  **`NazwaTypuWiersza:guid`**.
- Standardowy **Entitle operatora Administrator** ma stały guid
  `00000000-0015-0001-0001-000000000000` — nadanie mu prawa w dbinit sprawia, że funkcja działa
  od razu na każdej nowej bazie (administrator i tak może nadać sobie wszystko).
- Wpis z `dbversion` importuje się przy podbiciu wersji bazy (jak inne rekordy dbinit);
  w plikach danych demo — bez `dbversion` (import raz, przy tworzeniu bazy —
  zob. [demo-data.md](demo-data.md)).

## Część 2 — Import przez logikę biznesową (`business="true"`)

Wartości ustawiane są przez **właściwości biznesowe** obiektów — z pełną walidacją,
ograniczeniami i skutkami ubocznymi (przeliczenia, generowanie numerów, zapisy powiązane).
Import może zgłaszać **błędy walidacji**, dokładnie tak jak przy ręcznym wprowadzaniu danych.

Zasady:

- **Kolejność elementów ma znaczenie** — właściwości są ustawiane po kolei, a każda może
  uruchamiać operacje biznesowe. Reguła praktyczna: odzwierciedlaj kolejność, w jakiej
  **operator wpisywałby dane na formularzu** (najpierw definicja dokumentu, potem kontrahent,
  potem pozycje, na końcu stan). Rzeczywistą kolejność pól i **sekcje danych** (zakładki, grupy)
  formularza — nawet gdy masz tylko skompilowane DLL — odczytasz narzędziem **`scan-forms`**
  ze skilla [programming](../../programming/SKILL.md) (kolejność pól = kolejność wprowadzania; rozwija też ścieżki
  pól i `Include`).
- Dostępne właściwości biznesowe obiektu (oraz jego podkolekcje) zwraca narzędzie `scan-props`
  ze skilla [programming](../../programming/SKILL.md).
- Nowy obiekt może wymagać parametrów tworzenia — przekazuje się je **atrybutami** elementu
  rekordu (nazwa atrybutu = nazwa parametru); w kolekcji rodzic jest przekazywany
  automatycznie. Atrybut `ctor` wybiera wariant tworzenia.
- **Atrybut `class` — podtyp selektora.** Gdy tabela ma **selektor** (jedna tabela przechowuje
  wiele typów obiektów), atrybutem `class` wskazujesz **konkretny podtyp** do utworzenia
  (np. `<EwidencjaSP class="Kasa">`). Stosuj go **tylko przy tworzeniu nowego** obiektu — przy
  aktualizacji istniejącego podtyp jest już ustalony, więc `class` jest zbędny (a niezgodny —
  błędny). W tym trybie logika biznesowa ustawia wartość selectora sama; `class` działa też
  w imporcie rekordowym, ale tam element kolumny selectora pozostaje obowiązkowy — zob.
  *Wiersze tabel z selektorem* w Części 1. Czy tabela ma selektor i jakie są dopuszczalne podtypy
  sprawdzisz w sekcji `## Selektor — podtypy w jednej tabeli` narzędzia `scan-props`
  ([programming](../../programming/SKILL.md)): wartością atrybutu `class` jest **nazwa klasy podtypu** (kolumna
  „Klasa podtypu"); brak sekcji `Selektor` = tabela bez selektora, `class` nie ma zastosowania.
- Referencje wygodnie wskazywać elementem z `where` po czytelnym kluczu (kod, symbol) —
  plik pozostaje przenośny między bazami.

```xml
<session xmlns="http://www.soneta.pl/schema/business" business="true">
  <DokumentHandlowy>
    <Definicja where="Symbol=PZ" />
    <Magazyn where="Symbol=02" />
    <Kontrahent where="Kod=Abc" />
    <Pozycje>
      <Pozycja>
        <Towar where="Kod=T-001" />
        <Ilosc>120 m</Ilosc>
        <Cena>5.13 PLN</Cena>
      </Pozycja>
    </Pozycje>
    <Stan>Zatwierdzony</Stan>       <!-- na końcu, jak operator -->
  </DokumentHandlowy>
</session>
```

## Część 3 — Eksport danych

Standardowy eksport działa **wyłącznie według rekordów**. Eksportowany jest wskazany rekord
**guidowany** oraz wszystkie rekordy powiązane relacjami guidowanymi — czyli cały **datapack**
(zob. *datapack-guidedrow* w [programming](../../programming/SKILL.md)):

- relacje wewnętrzne (inner) — zapisywane **wewnątrz** elementu rekordu jako kolekcje,
- relacje zewnętrzne (outer) — dopisywane jako **osobne elementy główne** tego samego pliku,
- można dodatkowo wskazać kolekcje, relacje lub cechy do dołączenia (np. `Features`,
  nazwy kolekcji, ścieżki rozdzielane kropką).

### Ustalenie zakresu eksportu

Zanim wskażesz kolekcje, relacje i cechy do dołączenia, ustal, **co logicznie stanowi „dane
obiektu"**. Podpowiada to narzędzie **`scan-forms`** ze skilla [programming](../../programming/SKILL.md): zakładki
i sekcje formularza pokazują, które podkolekcje i cechy operator widzi jako część obiektu
(listy `Grid` = kolekcje, zakładki systemowe = załączniki/cechy). Pełny zestaw kolekcji
i relacji rekordu wylicza `scan-props` ([programming](../../programming/SKILL.md)). **Świadomie ogranicz zakres**
do danych potrzebnych w bazie docelowej — nadmiarowe relacje zewnętrzne rozrastają datapack
o kolejne rekordy powiązane, których import może wymagać dodatkowych słowników.

Struktura wyniku:

- każdy rekord dostaje atrybuty `id` (identyfikator lokalny) i `guid`; typ pochodny — `class`;
- referencje zapisywane są jako `Obiekt:GUID` (rekordy guidowane, np.
  `DokumentHandlowy:1ee7f76c-...`) albo identyfikator lokalny (rekordy nieguidowane,
  dołączone do pliku);
- pola `RRRR-MM-DD`, kropka dziesiętna itd. — formaty jak w tabeli wyżej;
- skasowane rekordy można zaznaczyć wpisem `<NazwaObiektu guid="..." deleted="True" />`;
- `fromto` ogranicza eksport kolekcji datowanych do okresu.

Plik wyniku eksportu jest bezpośrednio zdatny do importu według rekordów — to podstawowy
sposób **przenoszenia ustawień konfiguracyjnych między bazami**.

## Przykład: kompletny plik importu (pracownik etatowy)

Gotowy, **zweryfikowany próbnym importem** plik: pracownik zatrudniony na umowę o pracę
z wynagrodzeniem zasadniczym miesięcznym — [examples/import-pracownik-etatowy.xml](../examples/import-pracownik-etatowy.xml).
Ilustruje obiekt w **modelu „root + historia"** (rekord główny + kolekcja zapisów historycznych,
w których leżą właściwe dane) importowany **według rekordów**.

Nieoczywiste ustalenia potwierdzone na żywej bazie (istotne dla każdego obiektu kadrowego,
a wzorzec „root + historia" ma też inne obszary platformy):

- **Kolekcja historyczna z `addnew="true"` + stały `guid` na zapisach** — inaczej ponowny
  import wybucha na „kasowaniu ostatniego zapisu historii" (zob. *Zachowanie kolekcji…* wyżej).
- **Dane trzymane w osobnej strukturze zapisuj tam, gdzie faktycznie są.** Część danych obiektu
  (np. adresy pracownika) nie jest przechowywana wprost w rekordzie — wpisanie ich jako
  podelementu głównego rekordu kończy się błędem. Miejsce i nazwy pól ustalaj skanami
  (`scan-props`/`scan-forms` w [programming](../../programming/SKILL.md)), nie zgaduj (zob. *Zasada nadrzędna*).
- **Referencje przez standardowe GUID-y (z zerami) są przenośne** między bazami — dobre do
  wskazywania słowników i korzeni struktur (wydział-korzeń, kalendarz podstawowy, definicja
  elementu wynagrodzenia, tytuł ubezpieczenia). Referencje do rekordów o GUID nadawanym per baza
  (np. konkretny wydział, urząd skarbowy) wymagają podmiany lub wskazania przez `where`.
- **Format okresu `FromTo`:** `od...do` (puste „do" = okres otwarty, np. `2026-01-02...`);
  **wymiar etatu** jako ułamek (`1/1`); **kwota z walutą** z kodem (`12,345.00 PLN`).

Strukturę i dokładne nazwy pól obiektu pracownika (root, historia, warunki etatu, stawka)
dokumentują receptury domeny Kadry-Płace w [programming](../../programming/SKILL.md) (rozdziały o zatrudnieniu
i o etacie).

## Pliki `*.dbinit.xml` — inicjowanie i konwersja bazy

Pliki `*.dbinit.xml` (osadzone w bibliotekach) inicjują nową bazę i **automatycznie konwertują
ustawienia** przy podnoszeniu wersji. Wczytywane są trybem według rekordów, z dodatkowymi
regułami:

- `<session>` musi mieć `versionName` (nazwa wersjonowania, np. `soneta`) i może mieć
  `priority` (kolejność wczytywania plików; mniejsza liczba = wcześniej; domyślnie 100 —
  np. słownik musi poprzedzać dane, które się do niego odwołują);
- **każdy rekord główny musi mieć `dbversion`** — rekord jest przetwarzany tylko, gdy wersja
  bazy jest niższa niż `dbversion` (czyli raz, przy konwersji do tej wersji); rekord bez
  `dbversion` jest w tym trybie pomijany;
- rekordy standardowe kasowane wpisem `deleted="True"` tracą status standardowego GUID-u,
  więc wpis może zostać ponownie zainicjowany w nowszej wersji.

- `dbversion` postawiony na `<session>` **nie jest czytany** — musi być na każdym rekordzie;
- poprawka w kolejnej wersji to **patch**: ten sam `guid`, nowy `dbversion`, unikalny `id`
  (sufiks wersji), tylko zmienione pola, zwykle `updateonly="true"`; miejsce na kod klienta —
  `insertonly="true"`. Konwencja plików: `Obiekt.dbinit.xml` + `Obiekt_RRMMDDPP.dbinit.xml`.

Katalog wzorców wyniesionych z plików standardowych (słowniki, definicje dokumentów, zadania,
cechy, szablony, konfiguracja, role i prawa, kokpity) z gotowymi szkieletami:
[import-xml-examples.md](import-xml-examples.md); pełny plik przykładowy —
[examples/dbinit-slownik-i-poprawki.dbinit.xml](../examples/dbinit-slownik-i-poprawki.dbinit.xml).

W projekcie dodatku pliki `*.dbinit.xml` osadza się jako **EmbeddedResource** — sposób
osadzania (automatyczny przez Soneta.Sdk lub ręczny wpis w projekcie) opisuje artykuł
*sessionreader-sessionwriter* w [programming](../../programming/SKILL.md).

## Testowanie plików XML

Próbny import do bazy i odczyt efektu na żywej aplikacji **modyfikują bazę / uruchamiają
program**, więc wykonuje je agent **tylko na wyraźne żądanie użytkownika** — nie importuj
samowolnie po samym zbudowaniu pliku (sam plik zweryfikuj strukturalnie skanem i tyle dostarcz,
proponując test). Gdy żądanie testu pada, **całość robi agent, nie operator**: nie odsyłaj
użytkownika, by sam wczytał plik i sprawdził wynik. Wtedy przetestuj plik **próbą wczytania do
bazy** narzędziem `dbmgr`, a następnie **sam odczytaj efekt z programu** (odczyt pól / zrzut
ekranu przez `buscall` — [tools](../../tools/SKILL.md)) i potwierdź, że dane zmieniły się zgodnie z zamiarem
(opis narzędzia: [tools](../../tools/SKILL.md)):

```bash
dbmgr importxml <NazwaBazy> plik.xml
```

Zasady:

- Testuj na **bazie testowej lub kopii** (backup/restore i tworzenie baz — `dbmgr`,
  [tools](../../tools/SKILL.md)), nigdy od razu na bazie produkcyjnej — nieudany import (np. błędny plik
  roli) potrafi zablokować logowanie do bazy.
- Import przez logikę biznesową (`business="true"`) zgłosi błędy walidacji dokładnie jak przy
  ręcznym wprowadzaniu — komunikat wskazuje rekord i właściwość, której ustawienie się nie
  powiodło; popraw plik i ponów.
- Wczytanie powtórzone na tej samej bazie weryfikuje też **idempotencję** identyfikacji
  (rekordy z `guid`/`where`/`key` aktualizują się zamiast duplikować).
- Efekt obejrzyj na działającej aplikacji (`buscall`, [tools](../../tools/SKILL.md)) albo w teście
  integracyjnym (`ImportBusinessXml` — artykuł *integration-tests* w [programming](../../programming/SKILL.md)).

## Checklisty

**Przed budową pliku (obowiązkowo):**
- [ ] Struktura obiektu odczytana skanami z DLL, a nie zgadnięta: `scan-props` (nazwy pól/właściwości, typy, wartości enum) i — dla `business="true"` — `scan-forms` (kolejność pól, sekcje danych). Zob. *Zasada nadrzędna* na górze.

**Przed importem:**
- [ ] Właściwy tryb: konfiguracja/inicjacja → według rekordów; dane operacyjne → logika biznesowa.
- [ ] Rekordy główne to rekordy guidowane; GUID-y stałe i unikalne (nie generuj ich losowo przy każdym wydaniu pliku).
- [ ] Pola zweryfikowane narzędziem `scan-props` ([programming](../../programming/SKILL.md)): record-mode → tylko pola bazodanowe; business-mode → właściwości biznesowe.
- [ ] Business-mode: kolejność elementów jak przy wpisywaniu na formularzu; stan dokumentu na końcu.
- [ ] Referencje przenośne: GUID lub `where` po kodzie/symbolu; bez `#ID`.
- [ ] Kolekcje: świadomy wybór zastąpienia (domyślne) vs `addnew` vs `relationsimportmode="update"`.
- [ ] Detale (wiersze podrzędne) bez guidów, zapisane **zagnieżdżone w rodzicu** (wrapper = nazwa kolekcji children); brak komentarzy XML wewnątrz elementów kolekcji.
- [ ] Tabele z selectorem: element = typ bazowy wiersza, `class` przy podklasach, element kolumny selectora w każdym wierszu (zob. *Wiersze tabel z selektorem*).
- [ ] Źródła praw (`IRightsSource`): rekord `<Right>` nadający prawo (zob. *Nadawanie praw obiektowych*).
- [ ] Kolekcje historyczne („od–do"): `addnew="true"` + stały `guid` na zapisach (idempotencja; inaczej błąd „kasowanie ostatniego zapisu historii").
- [ ] Zmiana wartości „od dnia" → `date="RRRR-MM-DD"` na zapisie (cięcie okresu, nowy zapis) + tylko zmieniane pola; nie mylić z nadpisaniem zapisu przez `guid`. Zob. *Aktualizacja historyczna*.
- [ ] Dane trzymane w osobnej strukturze (np. adresy) zapisane we właściwym miejscu, nie wprost w rekordzie — miejsce potwierdzone skanem.
- [ ] dbinit: `versionName`, `priority` i `dbversion` na każdym rekordzie głównym; poprawki jako patch (`guid` ten sam, nowy `dbversion`, unikalny `id`, `updateonly`) — zob. [import-xml-examples.md](import-xml-examples.md).

**Weryfikacja struktury (zawsze, część budowy pliku):**
- [ ] Nazwy pól/typy/struktura (pole vs kolekcja vs subrow) zweryfikowane skanem przez agenta — read-only, nie zlecone użytkownikowi.

**Testowanie na bazie — tylko na wyraźne żądanie użytkownika (wtedy wykonuje agent, nie operator):**
- [ ] Próbne wczytanie na bazie testowej/kopii: `dbmgr importxml <baza> plik.xml` (→ [tools](../../tools/SKILL.md)).
- [ ] Ponowne wczytanie nie duplikuje rekordów (idempotencja identyfikacji — **podwójny** `dbmgr importxml` + policzenie rekordów; szczególnie detale zagnieżdżone).
- [ ] Efekt odczytany z programu **przez agenta** (buscall: odczyt pól / zrzut ekranu → [tools](../../tools/SKILL.md)) lub testem
  integracyjnym (`ImportBusinessXml` → artykuł *integration-tests* w [programming](../../programming/SKILL.md)) — potwierdzona zgodność danych z zamiarem.

## Powiązania

- [import-xml-examples.md](import-xml-examples.md) — katalog wzorców i szkieletów dla typowych
  obiektów, wyniesiony z plików standardowych `*.dbinit.xml`; pliki w `examples/`.
- [demo-data.md](demo-data.md) — mechanizm zasilania bazy Demo: katalog `Demo`, kolejność
  plików, sufiksy `.gold`/`.silver`, relacja do rekordów standardowych (dbinit).
- [business-xml](../../business-xml/SKILL.md) — definicja tabel z selectorem (enum dyskryminatora od 1, pułapka
  „Nierozpoznany typ wiersza"), relacje i kolekcje `children`.
- Źródła praw ([rights-source](../../programming/references/rights-source.md): nowe `IRightsSource` domyślnie Denied —
  kontekst dla rekordów `<Right>`); warstwa programistyczna importu/eksportu (klasy `SessionReader` /
  `SessionWriter`): artykuł [sessionreader-sessionwriter](../../programming/references/sessionreader-sessionwriter.md); ponadto [datapack-guidedrow](../../programming/references/datapack-guidedrow.md)
  (rekordy guidowane, datapack), [row-types](../../programming/references/row-types.md) (`OnImporting`/`OnImported`), [scan-props](../../programming/references/scan-props.md)
  (inwentaryzacja pól i właściwości; wylicza też kolekcje i relacje do zakresu eksportu),
  [scan-forms](../../programming/references/scan-forms.md) (zakładki, sekcje danych i kolejność pól formularza — kolejność wprowadzania
  pod `business="true"`; podpowiada zakres eksportu — co stanowi „dane obiektu"),
  [integration-tests](../../programming/references/integration-tests.md) (`ImportBusinessXml`).
- [form-xml](../../form-xml/SKILL.md) — składnia formularzy (`Page`/`Group`/`Field`/`DataContext`/`EditValue`);
  zakładki i grupy jako sekcje danych do uzupełnienia.
- [tools](../../tools/SKILL.md) — `dbmgr` (operacje na bazach, import XML z CLI), `buscall` (weryfikacja
  efektów importu na żywej aplikacji).
- [SKILL.md](../SKILL.md) — mapa tego skilla.
