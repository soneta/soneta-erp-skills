# Import i eksport przez MCP — pakiet „Import i eksport danych"

Agent AI połączony z programem przez MCP (metody pakietów Bundle) może eksportować i importować
dane trzema mechanizmami: **lista do Excela/CSV**, **XML** i **rejestr konfiguracji**. Każda
metoda importu ma **tryb próbny** `testing=true`, który sprawdza dane bez zapisu. Takiej opcji nie
ma w oknach programu. Format plików jest ten sam co przy pracy ręcznej, więc obowiązują opisy
[export-excel.md](export-excel.md), [import-excel.md](import-excel.md),
[export-xml.md](export-xml.md), [import-xml.md](import-xml.md) i [config-reg.md](config-reg.md).

## Metody

| Metoda | Działanie | Opis formatu |
|---|---|---|
| `export_folder_list` | lista folderu → `.xlsx` / `.csv` | [export-excel.md](export-excel.md) |
| `import_folder_list` | `.xlsx` / `.csv` → lista folderu; `testing` | [import-excel.md](import-excel.md) |
| `export_business_xml` | wskazane obiekty + datapack → XML (według rekordów) | [export-xml.md](export-xml.md) |
| `import_business_xml` | XML → baza; `testing` | [import-xml.md](import-xml.md) |
| `export_configreg` | rejestr konfiguracji → `*.reg.json`; zmiany względem zdjęcia | [config-reg.md](config-reg.md) |
| `import_configreg` | `*.reg.json` → scalenie z bazą lub z innym rejestrem; `testing` | [config-reg.md](config-reg.md) |

## Tryby próbne — co sprawdzają

Najważniejsza różnica: **próba importu listy wykonuje pełny zapis i go wycofuje**, a próba XML
kończy się **przed zapisem**.

| | `import_folder_list` | `import_business_xml` | `import_configreg` |
|---|---|---|---|
| Ustawienie wartości i logika pól | tak | tak | — |
| Weryfikatory obiektów | tak, po każdym obiekcie i paczce | tak, wszystkie naraz | — |
| Zdarzenia sesji przed zapisem | tak | tak | — |
| Zapis do bazy (numeracja przy zapisie, unikalność, ograniczenia bazy) | **tak — w transakcji wycofywanej na końcu** | **nie** | — |
| Analiza scalenia (konflikty, `$strict`, pary usunięty+dodany) | — | — | tak |
| Zbiera wszystkie błędy, a nie tylko pierwszy | tak (do 50) | tak | tak |
| Ostrzeżenia weryfikatorów | tak, z numerem wiersza | tak, z tabelą i obiektem | ostrzeżenia analizy |
| Rejestracja w historii zmian | nie | nie | nie |

Wnioski:

- **Próba importu listy** jest najbliższa prawdziwemu importowi. Gdy przechodzi bez błędów,
  import właściwy też przejdzie, o ile w międzyczasie nikt nie zmieni danych. Na czas próby
  zapisywane tabele są blokowane transakcją, więc duże próby wykonuj poza godzinami pracy.
- **Próba importu XML** nie wykryje błędów pojawiających się dopiero przy zapisie (numeracja
  dokumentów, unikalność w bazie, kolizje z innymi użytkownikami). Ostatecznym testem pozostaje
  import na kopii bazy.
- **Próba importu rejestru** to ta sama analiza, która blokuje zapis. Import właściwy też niczego
  nie zapisze, gdy analiza zwróci błędy.

## Wyniki

| Metoda | Odpowiedź |
|---|---|
| `import_folder_list` | `rowsChanged` (wierszy do zmiany / zmienionych), `testing`, `saved`, `message`, `errors` i `warnings` jako `{row, message}` |
| `import_business_xml` | `message`, `testing`, `errors` i `warnings` jako `{table, object, message}` |
| `import_configreg` | `message`, `saved`, `testing`, `summary` `{added, modified, deleted, conflicts, changes[{path, state}]}`, `errors` i `warnings` jako `{kind, path, message}`, `resultRegJson` |
| `export_folder_list` | `programFolderPath`, `tableName`, `format`, `rowCount`, `size`, `dataBase64`, `warnings` |
| `export_business_xml` | `xml` |
| `export_configreg` | `regJson`, `size`, `changes`, `savedRegistry` |

- `row` w imporcie listy to **numer wiersza pliku tak jak w arkuszu** (nagłówek = 1, pierwszy
  wiersz danych = 2). W logu programu numeracja jest inna: zaczyna się od wierszy danych.
  Brak `row` oznacza błąd, który nie dotyczy jednego wiersza (np. zapis paczki).
- Listy błędów i ostrzeżeń są obcinane do 50 pozycji, posortowanych po wierszu. Popraw pierwsze
  i powtórz próbę.
- Przy błędzie importu listy żaden wiersz nie jest zapisywany (`saved=false`), tak samo jak
  w programie.

## Pliki — parametry `…FileName`

Każda treść ma parę parametrów: treść wprost (`dataBase64`, `xml`, `regJson`) albo nazwę pliku
(`dataFileName`, `xmlFileName`, `regJsonFileName`).

- **Serwer nie czyta ani nie zapisuje plików.** Plik odczytuje lub zapisuje **klient**:
  aplikacja desktop (SonetaFrame) na dysku operatora, a agent serwerowy w swojej pamięci
  plikowej. Bez takiego klienta (np. `buscall call`) przekazuj treść wprost.
- Treść zwracana wprost ma limit 1 MB (eksport listy i rejestru). Większy wynik wymaga podania
  nazwy pliku albo zawężenia (`filters`, `paths`).
- Aplikacja desktop przesyła pliki powyżej 64 KB poza wywołaniem metody (upload i pobranie przez
  adres). Dzieje się to automatycznie, wystarczy podać nazwę pliku.
- Pliki XLSX i CSV przekazywane wprost są kodowane w Base64. Format importu jest rozpoznawany
  z treści (`xlsx` = archiwum ZIP), a w razie potrzeby wskazuje go parametr `format`.

## Prawa i ustawienia pakietu

| Warunek | Dotyczy |
|---|---|
| Ustawienia pakietu **„Eksport danych"** / **„Import danych"** (domyślnie włączone) | wyłączone ukrywają metody eksportu / importu przed agentem |
| Prawo roli **Bundle → „Dane operacyjne"** / **„Dane konfiguracyjne"** | odczyt wystarcza do eksportu; import wymaga pełnego dostępu; rodzaj danych wynika z tabeli (XML: z elementów pod `<session>`) |
| Prawa listy **„Lista - Excel"** / **„Lista - Wklej"** | `export_folder_list` / `import_folder_list` |
| Rola pełnego dostępu | `export_configreg`, `import_configreg` (jak folder „Zarządzanie konfiguracją") |
| Obiekt wcześniej odczytany przez agenta | `export_business_xml` przyjmuje tylko identyfikatory obiektów pokazanych agentowi (`search_object`, `retrieve_list`, `open_form`); tylko tabele z GUID-em |

## Procedura: import z próbą

1. Szablon: `export_folder_list` z `fields` (kolumny w kolejności formularza, z kluczem)
   i `headerMode="path"`, albo `export_business_xml` / `export_configreg`.
2. Przygotowanie danych w pliku według dokumentu formatu.
3. **Próba:** metoda importu z `testing=true`. Popraw błędy z `errors` (numer wiersza / obiekt /
   ścieżka) i powtarzaj do zera błędów. Ostrzeżenia z `warnings` przekaż operatorowi.
4. Import właściwy tym samym wywołaniem bez `testing`.
5. Kontrola: ponowny eksport i porównanie; dla rejestru `export_configreg`
   z `compareToRegJson` (powinien nie wykazać różnic względem zaimportowanego pliku).

## Checklista

- [ ] Metoda importu wywołana najpierw z `testing=true`; zero błędów przed importem właściwym.
- [ ] Dla XML: świadomość, że próba nie obejmuje zapisu — test ostateczny na kopii bazy.
- [ ] Dla listy: próba poza godzinami pracy przy dużych plikach (blokady transakcji).
- [ ] Ostrzeżenia (`warnings`) przekazane operatorowi.
- [ ] Pliki przez `…FileName` przy klientach, które je obsługują; treść wprost przy `buscall call`.
- [ ] Rola agenta ma prawo do odpowiedniego rodzaju danych i prawa listy.

## Powiązania

- [import-excel.md](import-excel.md), [export-excel.md](export-excel.md) — format arkusza i CSV.
- [import-xml.md](import-xml.md), [export-xml.md](export-xml.md) — format XML.
- [config-reg.md](config-reg.md) — rejestr konfiguracji, ścieżki węzłów, `$strict`.
- `/soneta:tools` — `buscall` (wywołanie metod z wiersza poleceń, `callmcp`), `dbmgr` (kopia bazy
  do prób).
- [SKILL.md](../SKILL.md) — mapa tego skilla.
