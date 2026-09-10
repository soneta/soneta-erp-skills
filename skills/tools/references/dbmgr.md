# `dbmgr` — zarządzanie bazami danych z wiersza poleceń

Narzędzie CLI Soneta do pełnego cyklu życia baz danych platformy Soneta (enova365, Triva):
tworzenie, rejestracja, konwersja, backup/restore, licencje, rozszerzenia (extensions),
analiza i kompilacja algorytmów. Zastępuje klikanie w kreatorach programu — idealne do
skryptów CI/CD, przygotowywania baz testowych i operacji wsadowych.

## Uruchamianie

Binaria narzędzia (`dbmgr.dll` / `dbmgr.exe`) znajdują się w katalogu wyjściowym buildu
(`bin/Debug`) projektu `DbMgr` — konkretna ścieżka zależy od Twojego układu repozytoriów.
Podstaw własną ścieżkę w miejsce `<ścieżka>`:

```bash
# cross-platform (.NET 10):
dotnet <ścieżka>/dbmgr.dll <komenda> [argumenty] [opcje]

# Windows:
<ścieżka>\dbmgr.exe <komenda> [argumenty] [opcje]
```

W dalszej części dla zwięzłości piszemy `dbmgr <komenda>` — załóż alias, dodaj katalog
buildu do `PATH` albo podstawiaj pełną formę `dotnet <ścieżka>/dbmgr.dll`.

> **Podpowiedź:** wygodnie jest zapisać typowe wywołania jako konfiguracje uruchomieniowe
> w IDE, np. `create Test --standard --recreate --demo=gold` czy `list --standard`.

## Model działania: nazwa bazy = wpis w konfiguracji

Argument `<database_name>` **nie** jest bezpośrednią nazwą bazy SQL — to **nazwa wpisu
w pliku konfiguracji baz** (tym samym, którego używa aplikacja). Wpis wskazuje serwer SQL,
fizyczną nazwę bazy i sposób logowania. Wartość specjalna `_` każe narzędziu **odkryć** bazę
z konfiguracji (przydatne, gdy zdefiniowana jest dokładnie jedna baza).

- `--dbconfig <ścieżka>` — wskazuje inny plik konfiguracji baz niż domyślny.
- `--standard` — wybiera **standardową** konfigurację baz (tę samą listę, którą widzi
  uruchomiona aplikacja SonetaFrame). Bez niej narzędzie użyje konfiguracji domyślnej/pustej.
- `-c, --config-file <ścieżka>` — dodatkowy plik konfiguracji uzupełniający (można podać wiele
  razy; kolejność ma znaczenie).

## Opcje globalne (dostępne dla większości komend)

| Opcja | Znaczenie |
|---|---|
| `-h, --help` | Pomoc dla komendy (użyj `dbmgr <komenda> --help`) |
| `--no-logo` | Ukrywa nagłówek ASCII (czystszy output w skryptach) |
| `-o, --output table\|json` | Format wyniku — `json` do parsowania w skryptach |
| `--stacktrace` | Pokazuje pełny stack trace wyjątku (diagnostyka) |
| `--debug` | Generuje pliki debug dla kodu kompilowanego w runtime; ułatwia debugowanie |
| `--dbconfig <ścieżka>` | Lokalizacja pliku konfiguracji baz |
| `--standard` | Standardowa konfiguracja baz (jak w aplikacji) |
| `--culture <kod>` | Kultura (język/format) sesji |
| `--list-config` | Dopisuje do logu komponentu wykonaną konfigurację ze źródłem wartości |

## Komendy — przegląd

| Komenda | Argumenty | Do czego |
|---|---|---|
| `info` | — | Informacje o logice biznesowej: wersje, kompilacja, daty |
| `list` | — | Lista zarejestrowanych baz |
| `status` | `<db>` | Szczegóły pojedynczej bazy |
| `register` | `<db>` | Rejestruje bazę w konfiguracji (bez tworzenia) |
| `unregister` | `<db>` | Usuwa bazę z konfiguracji (bez kasowania danych) |
| `create` | `<db>` | Tworzy bazę (opcjonalnie z danymi demo/przykładowymi) |
| `drop` | `<db>` | Kasuje bazę SQL |
| `convert` | `<db>` | Konwertuje bazę do bieżącej wersji logiki |
| `backup` | `<db> <plik.bac>` | Backup binarny (SQL `.bac`) |
| `restore` | `<db> <plik.bac>` | Odtworzenie z backupu binarnego |
| `backuptxt` | `<db> <plik.zip>` | Backup do plików tekstowych (`.zip`) |
| `restoretxt` | `<db> <plik.zip>` | Odtworzenie z backupu tekstowego |
| `resetadminpwd` | `<db>` | Reset hasła administratora bazy |
| `licence` | `<db> <licencja>` | Dodaje licencję (numer / plik xml / zip) |
| `importxml` | `<db> <plik.xml>` | Import danych z pliku XML |
| `extlist` | `<db>` | Lista rozszerzeń (extensions) bazy |
| `extupdate` | `<db> <plik>` | Dodaje/aktualizuje rozszerzenie |
| `extremove` | `<db> <plik>` | Usuwa rozszerzenie |
| `analyse` | `<db> <plik.xml>` | Analiza bazy do pliku XML |
| `compile` | `<db>` | Kompiluje algorytmy bazy |
| `config list` | — | Listuje wykonaną konfigurację i źródła wartości |
| `virtualkey` (`vk`, `vkey`) | `set … / remove <db>` | Zarządza kluczem wirtualnym (licencja) |
| `pause` | — | Pauza (przydatna w skryptach/oknach) |

## Połączenie SQL (`register`, `create`)

Te opcje definiują, gdzie i jak łączyć się z serwerem SQL:

| Opcja | Domyślnie | Znaczenie |
|---|---|---|
| `--mssql` | **true** | MS SQL Server (tryb domyślny) |
| `--azuresql` | | Azure SQL |
| `--sqlserver <adres>` | | Adres serwera SQL |
| `--sqldb <nazwa>` | | Fizyczna nazwa bazy na serwerze |
| `--sqltrusted` | | Windows Authentication (zamiast user/pwd) |
| `--sqluser <user>` | | Użytkownik SQL |
| `--sqlpwd <hasło>` | | Hasło SQL |
| `--generatesqluser` | | Generuje użytkownika SQL |
| `--default` | | Oznacza bazę jako domyślną |
| `--active` | **true** | Oznacza bazę jako aktywną |
| `--trustservercertificate` | **true** | Ufa certyfikatowi serwera, nawet niezaufanemu |
| `--multisubnetfailover` | | `MultiSubnetFailover` (grupy dostępności SQL) |
| `--useserveranddatabasenamefromsettings` | | Serwer i nazwa bazy z ustawień (klaster SQL) |

```bash
# rejestracja bazy (tylko wpis w konfiguracji) — logowanie zintegrowane:
dbmgr register exampleDb --mssql --sqlserver localhost --sqldb exampleDbName --sqltrusted
# rejestracja z użytkownikiem SQL:
dbmgr register exampleDb --sqlserver localhost --sqldb exampleDbName --sqluser user --sqlpwd pwd
```

## Tworzenie bazy (`create`) — dodatkowe opcje

| Opcja | Znaczenie |
|---|---|
| `--recreate` | Kasuje bazę, jeśli istnieje, i tworzy nową (idempotentnie w skryptach) |
| `--demo silver\|gold\|platinum` | Wypełnia bazę danymi demo + licencją danego poziomu (skąd pochodzą dane i jak dodać własny plik demo — artykuł [demo-data](../../config/references/demo-data.md)) |
| `--sampledata` | Wypełnia danymi przykładowymi z podkatalogu `Patterns` — **inny mechanizm** (wzorce) niż dane demo z katalogu `Demo` |
| `--licence <nr\|plik>` | Nakłada licencję na tworzoną bazę |
| `--generateadminpwd` | Generuje i ustawia hasło administratora |
| `--adminpwd <hasło>` | Ustawia konkretne hasło administratora |
| `--regeneratesqluser` | Regeneruje użytkownika SQL |
| `--elasticpoolname <nazwa>` | Tworzy bazę we wskazanym Elastic Pool (Azure) |
| `--skip-compile` | Pomija kompilację algorytmów po operacji |

```bash
# szybka baza testowa z danymi demo, nadpisująca istniejącą:
dbmgr create Test --standard --recreate --demo gold
# baza produkcyjna na wskazanym serwerze:
dbmgr create exampleDb --sqlserver localhost --sqldb exampleDbName --sqltrusted --licence licence.xml
# sprzątanie po testach — usunięcie bazy:
dbmgr drop Test
```

## Baza z własnym dodatkiem — `serversettings.json` + `--config-file`

Aby `create` utworzył bazę **wraz z tabelami Twojego dodatku**, przygotuj per-bazowy plik
konfiguracji (nazwa umowna: `serversettings.json`) i podaj go przez `-c/--config-file`.
Plik łączy dwie rzeczy: **ścieżki DLL dodatku** (`Ext`) i **połączenie SQL** (`Server.DbRegister`):

```json
{
  "Ext": [
    "<katalog-dodatku>/bin/Debug/Soneta.MojDodatek.dll",
    "<katalog-dodatku>/bin/Debug/Soneta.MojDodatek.UI.dll"
  ],
  "Server": {
    "DbRegister": {
      "Name": "moja_baza",
      "Server": "localhost",
      "DatabaseName": "moja_baza",
      "Trusted": false,
      "User": "sa",
      "Password": "<hasło>"
    }
  }
}
```

- `Ext` — biblioteki dodatku ładowane przy starcie: logika **i** projekt `.UI`.
- `DbRegister` — połączenie SQL: `Server`, `DatabaseName` oraz `User`/`Password`
  albo `Trusted: true` (Windows Authentication).

```bash
# tworzy bazę Z tabelami dodatku (bo Ext jest w config-file) + dane demo (~3 min):
dbmgr create moja_baza --config-file=<ścieżka>/serversettings.json --demo gold --recreate
```

**Jedno źródło prawdy:** ten sam plik podłącz do połączenia w SonetaFrame modyfikatorem
`config-file=` w źródle `process:` (patrz [sonetaframe.md](sonetaframe.md)) — aplikacja
wystartuje z tym samym dodatkiem i tą samą bazą SQL, którą utworzył `dbmgr`.

> **Serwer SQL w Dockerze.** Lokalny serwer bywa kontenerem `mcr.microsoft.com/mssql/server`
> (`localhost:1433`). Hasło SA odczytasz ze zmiennych środowiskowych kontenera zamiast pytać
> użytkownika:
> ```bash
> docker inspect <kontener> --format '{{range .Config.Env}}{{println .}}{{end}}' | grep MSSQL_SA_PASSWORD
> ```

> **`dbmgr` w kontenerze (bez lokalnego .NET).** Obraz `soneta/server.standard` zawiera
> `dbmgr.dll` — bazę tworzy się usługą init w compose (`entrypoint: ["dotnet","dbmgr.dll"]`)
> albo ad hoc: `docker compose run --rm dbinit <komenda>`. Uruchamianie stacku (server + web),
> wybór wersji obrazu i wariant z kontenerem `mssql` opisuje **[containers](../../containers/SKILL.md)**.

> **⚠️ Prawa operatora do dodatku.** Operator `Administrator` z bazy demo-gold **nie ma praw**
> do obiektów nowego dodatku. Nadaj rolę z prawem `Dodatki=Granted` — np. importując plik roli
> przez `dbmgr importxml moja_baza rola.xml`. Plik roli musi być w **UTF-8** i poprawnie
> zaescape'owany — **błędny import potrafi zablokować logowanie** do bazy.

## Konwersja (`convert`)

Podnosi strukturę bazy do wersji bieżącej logiki biznesowej — wymagana po aktualizacji DLL.

| Opcja | Znaczenie |
|---|---|
| `--checklicence` | Sprawdza licencję programu przed konwersją |
| `--force` | Wymusza konwersję bez sprawdzania stanu |
| `--skip-indexrepair` / `--noindexrepair` | Pomija naprawę indeksów po konwersji |
| `--skip-compile` | Pomija kompilację algorytmów po konwersji |
| `--convert-unicode` | Konwertuje bazę na Unicode |

```bash
dbmgr convert exampleDb
dbmgr convert exampleDb --force --skip-compile --skip-indexrepair --convert-unicode
```

### Pułapka: „Version OK" — konwersja nic nie zrobiła

`dbmgr convert <db>` bez `--force` porównuje **numer wersji** bazy z wersją logiki — gdy wersja
nie została podbita, kończy się komunikatem „Version OK" i **nic nie robi**, nawet jeśli schemat
w kodzie się zmienił (nowe tabele/kolumny bez podbicia wersji, typowe w trakcie developmentu).
Objaw w aplikacji: `Invalid column name 'X'` / `InvalidDatabaseStructureException`. W trakcie
developmentu używaj **`dbmgr convert <db> --force`**.

### Pułapka: zajęta baza — SINGLE_USER i uśpione sesje SQL

Konwersja przełącza bazę w `SINGLE_USER` **bez zrywania połączeń** — każde otwarte połączenie
(w tym **uśpione sesje klientów SQL, np. okno Database w IDE**) blokuje ją timeoutem. Dotyczy
też `drop` i `create --recreate`. Przed tymi operacjami zamknij/ubij sesje do bazy:

```sql
DECLARE @kill varchar(2000) = '';
SELECT @kill = @kill + 'KILL ' + CONVERT(varchar(8), session_id) + ';'
FROM sys.dm_exec_sessions WHERE database_id = DB_ID('NazwaBazy') AND session_id <> @@SPID;
EXEC(@kill);
```

**Checklista przed konwersją:**
- [ ] sesje SQL do bazy zamknięte (IDE, inne narzędzia)
- [ ] zmiana schematu bez podbicia wersji → `--force`

## Backup / Restore

Dwa formaty: **binarny** (`.bac`, natywny SQL Server) oraz **tekstowy** (`.zip`,
przenośny między silnikami/wersjami).

```bash
dbmgr backup     exampleDb exampleDb.bac
dbmgr restore    exampleDb exampleDb.bac --force            # --force: nadpisuje istniejącą
dbmgr restore    exampleDb exampleDb.bac --analyze-mode     # oznacza bazę jako wysłaną do analizy
dbmgr backuptxt  exampleDb exampleDb.zip
dbmgr restoretxt exampleDb exampleDb.zip --force
```

- `--force` — odtwarza, nawet jeśli baza już istnieje.
- `--analyze-mode` — oznacza bazę jako wysłaną do analizy (tryb serwisowy).

## Licencje i klucz wirtualny

```bash
# licencja: numer, plik xml lub zip:
dbmgr licence exampleDb licence.xml

# klucz wirtualny (alias: vk / vkey):
dbmgr vk set exampleDb <virtualKey> <virtualKeyServer> <licenceServer> --sqltrusted
dbmgr vk set exampleDb <virtualKey> <virtualKeyServer> <licenceServer> --sqluser user --sqlpwd pwd
dbmgr vk remove exampleDb          # lub: dbmgr virtualkey remove exampleDb
```

## Rozszerzenia (extensions)

```bash
dbmgr extlist   exampleDb                          # lista rozszerzeń
dbmgr extupdate exampleDb extension.dll --common   # dodaj/aktualizuj dla aplikacji desktop
dbmgr extupdate exampleDb extension.dll --server   # dla aplikacji serwerowej
dbmgr extremove exampleDb extension.dll
```

- `--common` — rozszerzenie dla aplikacji desktopowej.
- `--server` — rozszerzenie dla aplikacji serwerowej.
- `--skip-compile` — pomija kompilację algorytmów po operacji.

## Import, analiza, kompilacja

```bash
dbmgr importxml exampleDb dane.xml --skip-compile
dbmgr analyse   exampleDb output.xml --category --name --withdb  # analiza wg kategorii/nazwy + baza
dbmgr compile   exampleDb                                        # kompilacja algorytmów
```

- `analyse`: `--category` (wg kategorii), `--name` (wg nazwy), `--withdb` (dołącz analizę bazy).

## Diagnostyka i wyjście dla skryptów

```bash
dbmgr info -o json                    # wersje/kompilacja logiki jako JSON
dbmgr list -o json                    # lista baz do sparsowania
dbmgr status exampleDb -o json        # szczegóły jednej bazy jako JSON
dbmgr <komenda> --stacktrace          # pełny stack trace przy błędzie
dbmgr config list                     # co realnie zostało wczytane i skąd
```

- Dodaj `--no-logo`, aby uniknąć nagłówka ASCII zaśmiecającego parsowany output.
- `-o json` obsługują komendy prezentujące dane: `info`, `list`, `status`, `extlist`,
  a także `register`/`create`.

## Typowe przepływy

**Świeża baza testowa z danymi demo (idempotentnie):**
```bash
dbmgr create Test --standard --recreate --demo gold
```

**Klon bazy między środowiskami (tekstowo, przenośnie):**
```bash
dbmgr backuptxt  Prod prod.zip
dbmgr restoretxt Test prod.zip --force
dbmgr convert    Test           # dociągnij strukturę do bieżącej wersji logiki
```

**Po aktualizacji DLL logiki biznesowej:**
```bash
dbmgr convert exampleDb         # migracja struktury; potem ewentualnie compile
```

> **Interaktywne menu zamiast wpisywania komend** — jak owinąć `dbmgr` w menu CLI
> (wybór bazy z listy, gotowe akcje) na jednoplikowej aplikacji C#: [dbmgr-cli-menu.md](dbmgr-cli-menu.md).

> **Testowanie logiki na żywej aplikacji** (nawigacja, formularze, zrzuty ekranu) to
> osobne narzędzie — patrz [buscall.md](buscall.md).
