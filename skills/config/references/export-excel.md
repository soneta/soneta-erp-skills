# Eksport danych z listy do Excela i CSV

Każdą listę programu można zapisać do arkusza Excel (`.xlsx`) albo pliku CSV. Eksport działa na
**właściwościach biznesowych** obiektów (tych samych, które widać w kolumnach listy i na
formularzu), a nie na polach tabel bazy. Plik z nagłówkiem **„Ścieżka dostępu do danych"** jest
gotowym szablonem do ponownego wczytania. Import ustawia wtedy te same właściwości przez logikę
biznesową, z pełną walidacją, tak jak operator na formularzu.

## Najważniejsze zasady

1. **Kolumny pliku = kolumny listy**, w kolejności wyświetlania. Plik projektuje się przez
   ustawienie kolumn listy, a nie w samym arkuszu.
2. **Nagłówek „Ścieżka dostępu do danych"** (tryb ścieżek) to jedyny tryb, który umożliwia
   import. Tytuły kolumn listy służą tylko do czytania.
3. **Pola kluczowe w pliku** decydują, czy import zaktualizuje istniejący obiekt, czy doda
   nowy. Zob. [Pola kluczowe](#pola-kluczowe--identyfikacja-przy-imporcie).
4. **Kolejność kolumn = kolejność wprowadzania przy imporcie.** Ustaw kolumny tak, jak operator
   wypełnia formularz: najpierw pola, od których zależą inne (definicja, kontrahent, magazyn),
   potem pozostałe.
5. **Eksport z listy nie rozwija podlist.** Każdy obiekt to jeden wiersz pliku. Podlisty
   eksportuje się osobno albo dopisuje do pliku według zasad z sekcji [Podlisty](#podlisty).

## Spis treści

- [Skąd uruchomić eksport i jakie są opcje](#skąd-uruchomić-eksport-i-jakie-są-opcje)
- [Budowa pliku](#budowa-pliku)
- [Zasady budowy ścieżek w nagłówku](#zasady-budowy-ścieżek-w-nagłówku)
- [Pola kluczowe — identyfikacja przy imporcie](#pola-kluczowe--identyfikacja-przy-imporcie)
- [Kolejność kolumn](#kolejność-kolumn)
- [Podlisty](#podlisty)
- [Formaty wartości](#formaty-wartości)
- [Checklista — eksport pod ponowny import](#checklista--eksport-pod-ponowny-import)
- [Powiązania](#powiązania)

## Skąd uruchomić eksport i jakie są opcje

Eksport uruchamia się z listy: polecenie eksportu do arkusza albo **Kopiuj** (dane trafiają do
schowka jako tekst CSV). Wymagają praw **„Lista - Excel"** i **„Lista - Kopiuj"**. W aplikacji
webowej kreator eksportu oferuje formaty CSV, XLSX i XML. XML to osobny mechanizm, opisany w [export-xml.md](export-xml.md).

| Opcja okna „Eksport danych" | Znaczenie |
|---|---|
| Format: CSV / XLSX | CSV to tekst rozdzielany tabulatorami; XLSX to arkusz Excel |
| Wiersz nagłówka: brak | same wiersze danych |
| Wiersz nagłówka: tytuły listy | podpisy kolumn listy — plik do czytania, **nie do importu** |
| Wiersz nagłówka: ścieżka dostępu do danych | ścieżki właściwości — **plik do importu** (domyślnie) |
| Tylko zaznaczone dane na liście | eksport zaznaczonych wierszy zamiast całej listy |
| Pomiń pola tylko do odczytu | pomija kolumny, których nie da się ustawić (wyliczane); uwaga: pomija też `Guid` |
| Dane w formacie niezależnym od kultury | CSV z kropką dziesiętną i datami w formacie niezależnym od ustawień regionalnych; dla XLSX bez znaczenia |
| Zapis do pliku, otwórz po eksporcie | folder domyślny to pulpit; nazwa pliku to `<Tabela>_<RRRRMMDDGGMMSS>.xlsx` |

Użytkownicy pulpitu (aplikacja webowa dla pracowników i kontrahentów) dostają zawsze tytuły
kolumn, bez ścieżek. Jeśli konfiguracja programu rejestruje eksporty w historii zmian, każdy
eksport jest odnotowywany, a w zależności od ustawienia także z treścią pliku.

## Budowa pliku

| Element | Zawartość |
|---|---|
| Wiersz 1 | nagłówek: ścieżka (lub tytuł) każdej kolumny |
| Wiersze 2… | jeden wiersz na obiekt listy |
| Kolumna `Class` (pierwsza) | tylko na listach z wieloma typami obiektów w jednej tabeli: nazwa typu obiektu w wierszu |
| Kolumna wartości złożonej | rozbita na kilka kolumn — zob. sufiksy niżej |
| Kolumna odwołania do obiektu | klucz obiektu, a gdy typ jest niejednoznaczny — dodatkowo kolumna typu |

Kolumny listy, które nie wskazują właściwości obiektu (kolumny specjalne siatki), nie trafiają
do pliku. Wartość historyczna (np. dane z historii pracownika) i cecha historyczna są zapisywane
**na datę aktualną programu**. Błąd odczytu wartości trafia do komórki jako tekst błędu i nie
przerywa eksportu.

## Zasady budowy ścieżek w nagłówku

Ścieżka to nazwa właściwości biznesowej obiektu, a nie kolumny w bazie.

| Zapis | Znaczenie | Przykład |
|---|---|---|
| `Pole` | właściwość obiektu | `Kod`, `Nazwa`, `Data` |
| `A.B.C` (kropka) | zejście we właściwość złożoną, cechę albo pod-rekord | `Ceny.Podstawowa.Netto`, `Features.Kolor`, `Adres.Miejscowosc` |
| `Obiekt:Klucz` (dwukropek) | odwołanie do innego obiektu, wskazanego kluczem | `Kontrahent:Kod`, `Definicja:Symbol`, `Magazyn:Symbol` |
| `Obiekt:Class` + `Obiekt:Guid` | odwołanie, którego typ jest niejednoznaczny (różne typy obiektów, interfejs) | `Podmiot:Class`, `Podmiot:Guid` |
| `Kolekcja:Pole` | pole elementu podlisty (zob. [Podlisty](#podlisty)) | `Pozycje:Ilosc`, `Pozycje:Towar:Kod` |
| `Pole.Sufiks` | składnik wartości złożonej | `Cena.Waluta`, `Ilosc.Jednostka` |
| `//Nazwa` | kolumna informacyjna, pomijana przy imporcie | `//Uwagi` |

**Klucz w odwołaniu** wybiera eksport: najpierw jednopolowy klucz główny tabeli, potem pierwszy
jednopolowy klucz unikalny (np. `Kod`, `Symbol`). Jeśli takiego nie ma, wybiera `Guid` (obiekty
z GUID-em) albo `ID`. Klucza w odwołaniu nie wybierasz ręcznie. Wynika on z kolumny listy,
która pokazuje obiekt (np. kolumna `Kontrahent`).

**Sufiksy wartości złożonych:**

| Typ wartości | Kolumny |
|---|---|
| Kwota z walutą | `Pole`, `Pole.Waluta` |
| Ilość z jednostką | `Pole`, `Pole.Jednostka` |
| Okres od–do | `Pole.Od`, `Pole.Do` |
| Ułamek | `Pole.L`, `Pole.M` |
| Rok i miesiąc | `Pole.Rok`, `Pole.Miesiąc` |

W trybie tytułów sufiks dopisywany jest po spacji (`Cena Waluta`).

> **Ścieżka przez odwołanie z kropką to pułapka.** Kolumna listy `Kontrahent.Nazwa` eksportuje
> się jako `Kontrahent.Nazwa`. Przy imporcie oznacza to **zmianę nazwy powiązanego kontrahenta**
> albo błąd pola tylko do odczytu. Takie kolumny informacyjne usuń z pliku lub poprzedź
> nagłówek znakami `//`. Do wskazania obiektu służy kolumna `Kontrahent` (eksport zapisze
> `Kontrahent:Kod`).

## Pola kluczowe — identyfikacja przy imporcie

Import szuka w liście docelowej obiektu, którego dotyczy wiersz, kolejno:

| Kolejność | Kolumna w pliku | Uwagi |
|---|---|---|
| 1 | `Guid` | tylko obiekty z GUID-em; jednoznaczne w obrębie bazy i baz, które mają wspólne GUID-y |
| 2 | `ID` | wewnętrzny numer rekordu — **tylko w tej samej bazie** |
| 3 | klucz historii | przy aktualizacji danych historycznych od daty |
| 4 | **wszystkie pola klucza unikalnego** tabeli | np. `Kod` towaru, `Kod` kontrahenta, `Symbol` definicji; pole klucza będące odwołaniem w postaci `Pole:Klucz` |

Brakujące pola klucza złożonego import bierze z kontekstu listy, np. z obiektu nadrzędnego, gdy
lista jest podlistą (pozycje dokumentu), albo z filtra listy. Gdy żaden klucz nie pasuje, import
**dodaje nowy obiekt**.

Zasady przygotowania eksportu:

- **Umieść w liście kolumnę klucza.** Bez klucza każdy import doda nowe obiekty zamiast
  zaktualizować istniejące.
- **Ta sama baza** (popraw dane w arkuszu i wczytaj z powrotem): najpewniejszy jest `Guid`.
  Opcja „Pomiń pola tylko do odczytu" usuwa kolumnę `Guid`, więc przy niej oprzyj się na kluczu
  naturalnym.
- **Inna baza:** klucz naturalny (`Kod`, `Symbol`, numer), bo `ID` i GUID-y obiektów
  wprowadzonych ręcznie różnią się między bazami.
- **Klucz złożony:** wszystkie jego pola muszą być w pliku albo wynikać z kontekstu listy.
- **Odwołania** w pliku (`Kontrahent:Kod`) identyfikują obiekty powiązane tym samym
  mechanizmem. Obiekt powiązany musi istnieć w bazie docelowej pod tym kluczem.
- Kolumnę klucza warto ustawić na początku listy. Służy ona do wyszukania obiektu, a nie do
  ustawiania wartości.

Jakie klucze unikalne ma tabela, opisuje jej definicja w `business.xml` (skill
`/soneta:business-xml`, element klucza z atrybutem unikalności). W praktyce kluczem naturalnym
jest pole, którego program nie pozwala powtórzyć: kod, symbol, numer.

## Kolejność kolumn

Import ustawia wartości **w kolejności kolumn pliku**, a każda wartość może uruchomić logikę
biznesową: podpowiedzi, przeliczenia, blokady pól. Dlatego:

- kolejność kolumn listy ustaw jak **kolejność pól na formularzu obiektu**. Odczytasz ją
  narzędziem `scan-forms` w `/soneta:programming` albo z formularza w programie;
- pola sterujące (definicja dokumentu, typ, kontrahent, magazyn, waluta) daj przed polami
  zależnymi (ceny, rabaty, terminy);
- pola wyliczane pomiń. Opcja „Pomiń pola tylko do odczytu" robi to automatycznie.

Układ kolumn zapisany jako ustawienia listy trafia do magazynu plików (`tableviews/`). Można go
przenieść na inną bazę razem z konfiguracją, zob. [config-reg.md](config-reg.md).

## Podlisty

Eksport z listy zapisuje **jeden wiersz na obiekt** i nie rozwija jego podlist (pozycji,
płatności, zapisów). Są dwa sposoby przeniesienia podlist:

**1. Eksport podlisty z formularza obiektu.** Podlista na formularzu, np. pozycje dokumentu,
też jest listą. Eksportuje się ją tak samo, a plik wczytuje do tej samej podlisty innego
obiektu. Obiekt nadrzędny wynika wtedy z formularza i nie ma go w pliku.

**2. Plik obiektu z podlistami.** Import rozumie strukturę, w której kolumny podlisty mają
ścieżkę `Kolekcja:Pole`, a kolejne elementy podlisty leżą w kolejnych wierszach z **pustymi**
kolumnami obiektu głównego. Taki plik budujesz z eksportu listy głównej, dopisując kolumny
podlisty w arkuszu:

| Definicja:Symbol | Kontrahent:Kod | Pozycje:Towar:Kod | Pozycje:Ilosc | Pozycje:Ilosc.Jednostka | Pozycje:Cena |
|---|---|---|---|---|---|
| FV | ABC | T-001 | 2 | szt | 10 |
| | | T-002 | 5 | kg | 7,5 |
| FV | XYZ | T-001 | 1 | szt | 10 |

- Wiersz z wypełnionymi kolumnami obiektu głównego zaczyna nowy obiekt. Wiersz z pustymi
  kolumnami obiektu dopisuje kolejny element podlisty do obiektu z wiersza wyżej.
- Podlisty mogą być wielopoziomowe: `Dekrety:Zapisy:Konto:Symbol` (zapisy dekretu dokumentu).
  Wiersz z pustymi kolumnami `Dekrety:…` dopisuje kolejny zapis do bieżącego dekretu.
- Ten sam dwukropek oznacza odwołanie albo podlistę. Program rozróżnia je po typie właściwości:
  obiekt jest wyszukiwany po kluczu, kolekcja dostaje elementy.
- Elementy podlisty są identyfikowane jak obiekty główne (`Guid`, `ID`, klucz unikalny).
  Brakujące pole klucza, czyli obiekt nadrzędny, wynika z wiersza głównego. Aby **zaktualizować**
  istniejące pozycje, dodaj kolumnę klucza pozycji (np. `Pozycje:Lp`). Bez niej import dopisze
  nowe pozycje.
- Podlista z elementami różnych typów (np. płatności) może wymagać kolumny `Kolekcja:Class`.

Szczegóły wczytywania podlist i kolumny sterujące importem (`!Action`, `!Ignore`, aktualizacja
historii) opisuje dokument importu.

## Formaty wartości

| | XLSX | CSV |
|---|---|---|
| Kodowanie | arkusz Excel | tekst UTF-16, kolumny rozdzielone tabulatorem |
| Liczby, daty | wartości typowane arkusza | wg kultury interfejsu (przecinek dziesiętny) albo niezależnie od kultury — opcja okna |
| Listy wyboru (enum) | podpis wartości | podpis wartości |
| Lista tekstów | — | wartości rozdzielone znakiem `\|` |
| Teksty z `"` lub znakiem końca wiersza | bez zmian | w cudzysłowie, `"` podwojony |
| Tabulator w tekście | bez zmian | zamieniany na spacje |
| Tekst zaczynający się od `=` lub `@` | — | poprzedzony znakiem `` ` ``, gdy konfiguracja włącza neutralizację formuł |

## Checklista — eksport pod ponowny import

- [ ] Nagłówek: **ścieżka dostępu do danych** (nie tytuły, nie brak nagłówka).
- [ ] W liście jest **kolumna klucza**: `Guid` dla tej samej bazy, klucz naturalny (`Kod`,
      `Symbol`, numer) dla innej bazy; przy kluczu złożonym wszystkie pola.
- [ ] „Pomiń pola tylko do odczytu" nie usunęło kolumny klucza (zwłaszcza `Guid`).
- [ ] Kolejność kolumn jak kolejność pól na formularzu; pola sterujące przed zależnymi.
- [ ] Odwołania mają postać `Obiekt:Klucz`; kolumny `Obiekt.Pole` (dane obiektu powiązanego)
      usunięte albo oznaczone `//`.
- [ ] Obiekty wskazywane odwołaniami istnieją w bazie docelowej pod tym samym kluczem.
- [ ] Podlisty: wyeksportowane osobno z formularza albo dopisane jako kolumny `Kolekcja:Pole`
      z wierszami kontynuacji; klucz pozycji, jeśli pozycje mają być aktualizowane.
- [ ] CSV na inną kulturę albo do innego narzędzia: opcja „niezależny od kultury".
- [ ] Układ kolumn zapisany jako ustawienia listy, jeśli eksport będzie powtarzany.

## Powiązania

- [export-xml.md](export-xml.md) — eksport rekordów do XML (datapack, według rekordów, bez
  logiki biznesowej); inny format i inne zastosowanie.
- [config-reg.md](config-reg.md) — ustawienia list (`tableviews/`) w magazynie plików
  i przenoszenie ich między bazami.
- `/soneta:programming` — *scan-forms* (kolejność pól formularza = kolejność kolumn), *scan-props*
  (nazwy właściwości biznesowych do ścieżek), *viewinfo* (definicja list).
- `/soneta:business-xml` — klucze unikalne tabel (identyfikacja wierszy przy imporcie).
- `/soneta:form-xml` — kolumny list (`Grid`, `Field`, `EditValue`) w definicjach widoków.
- `/soneta:tools` — `buscall`: weryfikacja eksportu i importu na działającej aplikacji.
- [SKILL.md](../SKILL.md) — mapa tego skilla.
