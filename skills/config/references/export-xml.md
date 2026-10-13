# Eksport danych i ustawień konfiguracyjnych do plików XML

Platforma Soneta zapisuje wskazane rekordy do pliku XML w tym samym formacie, który wczytuje
import (element `<session>`). Plik wynikowy jest od razu gotowy do **importu według rekordów**,
dlatego eksport to podstawowy sposób **przenoszenia ustawień konfiguracyjnych między bazami**.
Format, atrybuty i zasady wczytywania opisuje [import-xml.md](import-xml.md).

## Co trafia do pliku

Standardowy eksport działa **wyłącznie według rekordów**. Eksportowany jest wskazany rekord
**guidowany** oraz wszystkie rekordy powiązane relacjami guidowanymi — czyli cały **datapack**
(zob. *datapack-guidedrow* w `/soneta:programming`):

- relacje wewnętrzne (inner) — zapisywane **wewnątrz** elementu rekordu jako kolekcje,
- relacje zewnętrzne (outer) — dopisywane jako **osobne elementy główne** tego samego pliku,
- można dodatkowo wskazać kolekcje, relacje lub cechy do dołączenia (np. `Features`,
  nazwy kolekcji, ścieżki rozdzielane kropką).

Rekordów nieguidowanych nie eksportuje się samodzielnie — trafiają do pliku tylko jako część
datapacku rekordu guidowanego.

## Ustalenie zakresu eksportu

Zanim wskażesz kolekcje, relacje i cechy do dołączenia, ustal, **co logicznie stanowi „dane
obiektu"**. Podpowiada to narzędzie **`scan-forms`** ze skilla `/soneta:programming`: zakładki
i sekcje formularza pokazują, które podkolekcje i cechy operator widzi jako część obiektu
(listy `Grid` = kolekcje, zakładki systemowe = załączniki/cechy). Pełny zestaw kolekcji
i relacji rekordu wylicza `scan-props` (`/soneta:programming`). **Świadomie ogranicz zakres**
do danych potrzebnych w bazie docelowej — nadmiarowe relacje zewnętrzne rozrastają datapack
o kolejne rekordy powiązane, których import może wymagać dodatkowych słowników.

## Struktura wyniku

- każdy rekord dostaje atrybuty `id` (identyfikator lokalny) i `guid`; typ pochodny — `class`;
- referencje zapisywane są jako `Obiekt:GUID` (rekordy guidowane, np.
  `DokumentHandlowy:1ee7f76c-...`) albo identyfikator lokalny (rekordy nieguidowane,
  dołączone do pliku);
- wartości w kulturze niezmiennej: daty `RRRR-MM-DD`, kropka dziesiętna itd. — pełna tabela
  w [import-xml.md](import-xml.md), *Formaty wartości w elementach*;
- skasowane rekordy można zaznaczyć wpisem `<NazwaObiektu guid="..." deleted="True" />`;
- `fromto` ogranicza eksport kolekcji datowanych do okresu.

```xml
<session xmlns="http://www.soneta.pl/schema/business">
  <Definicja id="Definicja_1" guid="00000000-0ab1-0001-0001-000000000000">
    <Nazwa>...</Nazwa>
    <Pozycje>                                 <!-- relacja wewnętrzna: kolekcja w rekordzie -->
      <PozycjaDefinicji id="PozycjaDefinicji_1"> ... </PozycjaDefinicji>
    </Pozycje>
    <Slownik>Slownik:00000000-0ab1-0002-0001-000000000000</Slownik>
  </Definicja>
  <Slownik id="Slownik_1" guid="00000000-0ab1-0002-0001-000000000000">  <!-- relacja zewnętrzna -->
    ...
  </Slownik>
</session>
```

## Eksport XML czy rejestr konfiguracji

Eksport XML przenosi **wskazane rekordy** i nie wykrywa różnic ani nie usuwa wierszy w bazie
docelowej. Gdy chcesz porównać konfigurację dwóch baz albo doprowadzić bazę do zadanego stanu,
użyj rejestru konfiguracji — [config-reg.md](config-reg.md) (porównanie kryteriów w sekcji
*Rejestr czy XML*).

## Eksport z kodu

Plik w tym formacie zapisuje klasa `SessionWriter` — dodawanie rekordów, dołączanie kolekcji
i cech (subexports), znaczniki usunięć i okres `FromTo` opisuje artykuł *sessionreader-sessionwriter* w `/soneta:programming`.

## Checklista — eksport do przeniesienia na inną bazę

- [ ] Eksportowany rekord jest guidowany (rekordy nieguidowane tylko w jego datapacku).
- [ ] Zakres ustalony skanami (`scan-forms` — „dane obiektu", `scan-props` — kolekcje i relacje),
      a nie dołączony „na wszelki wypadek".
- [ ] Relacje zewnętrzne przejrzane: każdy dopisany rekord jest potrzebny albo istnieje już
      w bazie docelowej pod tym samym GUID-em.
- [ ] Referencje w pliku wskazują GUID-y (`Obiekt:GUID`) lub identyfikatory lokalne, bez `#ID`.
- [ ] `fromto` ustawione, jeśli eksport ma objąć tylko okres kolekcji datowanych.
- [ ] Plik przejrzany pod kątem danych wrażliwych, jeśli ma opuścić firmę lub trafić do repozytorium.
- [ ] Import pliku na bazie docelowej zgodnie z checklistami w [import-xml.md](import-xml.md)
      (próbny import na kopii — tylko na wyraźne żądanie użytkownika).

## Powiązania

- [import-xml.md](import-xml.md) — format pliku, identyfikacja rekordów, formaty wartości,
  atrybuty specjalne, tryby importu, dbinit, testowanie.
- [config-reg.md](config-reg.md) — rejestr konfiguracji: porównanie i scalanie stanu
  konfiguracji między bazami (`*.reg.json`).
- [import-xml-examples.md](import-xml-examples.md) — wzorce plików dla typowych obiektów.
- [export-excel.md](export-excel.md) — eksport listy do Excela/CSV na właściwościach biznesowych
  (plik do importu przez logikę biznesową z poziomu listy).
- `/soneta:programming` — *datapack-guidedrow* (rekordy guidowane, datapack, relacje inner/outer),
  *sessionreader-sessionwriter* (`SessionWriter`), *scan-props* i *scan-forms* (zakres eksportu).
- `/soneta:tools` — `dbmgr` (kopia bazy przed importem wyeksportowanego pliku).
- [SKILL.md](../SKILL.md) — mapa tego skilla.
