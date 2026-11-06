# Konwersje danych — skrypty `*.convert.sql`

Skrypt konwersji przekształca dane istniejącej bazy przy podniesieniu wersji (np. scalenie
rekordów, przepięcie relacji po zmianie struktury). Uruchamia go konwersja bazy (`dbmgr convert`,
`/soneta:tools`).

## Najważniejsze

- **Jedna paczka.** Plik jest wykonywany w całości jednym poleceniem — zmienne, kursory i tabele
  tymczasowe działają w całym pliku.
- **Pierwsza linia to atrybuty:** `-- database:MSSQL versionname:soneta version:NNNNNNNN`.
  Skrypt bez `database:` dotyczy wszystkich silników baz.
- **Wykonuje się tylko, gdy wersja bazy jest niższa niż `version`.** Baza testowa w tej samej wersji
  nie uruchomi go sama — uruchom go ręcznie albo w teście (niżej).

## Kolumny polimorficzne

Relacja do interfejsu ma w bazie parę kolumn: `X` (ID) + `XType` (nazwa tabeli, np. `'Pracownicy'`) —
[relations-guide.md](relations-guide.md#relacja-do-interfejsu-polimorficzna). Nie każda kolumna
o nazwie `…Type` jest taką parą — bywają kolumny `int` (np. `OperatorType`). Ogólne przepinanie
filtruje więc kolumny `…Type` typu tekstowego.

## Przepinanie wierszy podrzędnych z rekordu A do B

Przed `UPDATE` usuń wiersze A, które po zmianie naruszyłyby indeks unikalny zawierający przepinaną
kolumnę. Indeksy odczytaj z `sys.indexes` / `sys.index_columns`; pozostałe kolumny indeksu porównuj
z obsługą `NULL`.

```sql
-- database:MSSQL versionname:soneta version:NNNNNNNN
DECLARE @A int = ..., @B int = ...;

DELETE p FROM Pozycje p
WHERE p.Rekord = @A
  AND EXISTS (SELECT 1 FROM Pozycje q
              WHERE q.Rekord = @B
                AND (q.Kod = p.Kod OR (q.Kod IS NULL AND p.Kod IS NULL)));

UPDATE Pozycje SET Rekord = @B WHERE Rekord = @A;
```

## Test integracyjny konwersji

1. Zbuduj stan „przed konwersją” builderami; brakujące elementy dopracuj surowym SQL
   (`Database.OpenConnection(DatabaseType.Operational).ExecuteCommand(...)` działa w transakcji testu).
2. Uruchom skrypt odczytany z zasobu assembly, a po nim inicjalizator
   `IDatabaseInitializer.Initialize(login, …)`, jeśli konwersja go wymaga.
3. Wynik sprawdź w nowej sesji.

Klasa bazowa i transakcja testu: `/soneta:programming` (integration-tests.md).

## Diagnostyka na żywej bazie

Konsola bazy w IDE trzyma połączenie — `dbmgr convert` czeka wtedy na wylogowanie pozostałych
operatorów (timeout `ALTER DATABASE`; `/soneta:tools`, dbmgr.md). Zapytania diagnostyczne kieruj
z kontekstu `master`, z nazwami trzyczęściowymi (`baza.dbo.Tabela`).

## Checklista

- [ ] Pierwsza linia z `version` wyższą niż bieżąca wersja bazy; `database:` tylko, gdy SQL jest specyficzny dla silnika.
- [ ] Przepinanie usuwa najpierw kolizje z indeksami unikalnymi.
- [ ] Pary `X`/`XType` rozpoznane po typie tekstowym kolumny `…Type`.
- [ ] Test integracyjny uruchamia skrypt z zasobu i sprawdza wynik w nowej sesji.
