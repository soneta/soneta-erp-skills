# `buscall` — zdalne wywoływanie metod aplikacji (referencja)

Narzędzie CLI do **zdalnego sterowania działającą aplikacją Soneta/enova365**: nawigacja po
folderach programu, otwieranie formularzy, odczyt i edycja gridów, zmiana pól, zrzuty ekranu.
`buscall` mostuje pojedyncze wywołanie do uruchomionego GUI (`SonetaFrameNew`), które wykonuje
metodę warstwy **Bundle** i zwraca wynik jako JSON.

To referencja **funkcji i składni**. Konkretne zastosowanie — wizualna weryfikacja kodu podczas
developmentu (konfiguracja bazy, rebuild, pułapki procesów) — opisuje
[buscall-live-testing.md](../../programming/references/buscall-live-testing.md).

## Uruchamianie i tryby

Binarka `buscall` (oraz `BusCall.dll`) leży w katalogu build projektu BusCall
(`bin/Debug/net10.0/`) — podstaw własną ścieżkę:

```bash
<ścieżka>/buscall --db <Baza> <tryb> …          # albo: dotnet <ścieżka>/BusCall.dll …
```

> **Windows:** przykłady w tym dokumencie są w składni bash (podstawienia `$(...)`, potoki z `jq`).
> W PowerShell wywołania wyglądają analogicznie (`$wynik = & <ścieżka>\buscall.exe --db <Baza> call …`
> lub `dotnet <ścieżka>\BusCall.dll …`), a `jq` trzeba doinstalować (np. `winget install jqlang.jq`);
> zamiast `jq` można też parsować wynik przez `ConvertFrom-Json`.

- **`call`** — pojedyncze wywołanie: `buscall --db <Baza> call <metoda> [klucz=wartość ...]`.
  Wykonuje **jedną** metodę, wypisuje wynik JSON na STDOUT i kończy proces. Bez handshake'u
  i utrzymywania procesu. **Domyślny, najprostszy tryb.**
- **`callmcp`** — wariant zgodny ze standardem **MCP**: czyta z STDIN komunikat JSON-RPC 2.0
  `tools/call` i zwraca odpowiedź MCP (patrz sekcja na końcu). Do własnych orkiestratorów.
- `mcp` (dawny) — długożyjący serwer stdio JSON-RPC; nadal działa, ale do większości zadań
  zbędnie skomplikowany względem `call`.
- **`open`** — `buscall --db <Baza> open [folder]` uruchamia SonetaFrame i opcjonalnie ustawia
  folder, nie wywołując żadnej metody. Przydatne, gdy chcesz najpierw wystartować aplikację
  (start trwa kilkadziesiąt sekund), a dopiero potem mierzyć czas kolejnych `call`.

`--db <Baza>` wskazuje **źródło bazy** zdefiniowane w SonetaFrame (nie fizyczną nazwę bazy SQL).
Dopasowanie idzie w kolejności: **jednoznaczny identyfikator** (`IdentPart|NazwaBazy`, np.
`Process|Demo`) → **`caption`** źródła → **nazwa bazy** — szczegóły w
[sonetaframe.md](sonetaframe.md). Przykład: wpis `"process:Demo;caption=dev;path=…"` otwiera się
przez `--db dev` (caption), a **nie** `--db Demo`. Pułapka: gdy caption **jednego** źródła pokrywa
się z nazwą bazy **innego** (np. wpis HTTP z `caption=Demo` obok `process:Demo;caption=dev`),
`--db Demo` trafi w źródło z `caption=Demo` — jeśli jest martwe, każde wywołanie kończy się
`[-32603] Brak połączenia z serwerem: Demo`, a aplikacja stoi na ekranie wyboru baz. Rozwiązania:
caption właściwego źródła (`--db dev`) albo jednoznaczny identyfikator (`--db "Process|Demo"`).
To połączenie decyduje też, z jakiego kodu startuje aplikacja — szczegóły
w [buscall-live-testing.md](../../programming/references/buscall-live-testing.md).

## Argumenty metod: pary `klucz=wartość`

W trybie `call` argumenty podaje się jako **pary `klucz=wartość`** po nazwie metody. Wartość jest
interpretowana jako JSON, gdy jest poprawnym literałem (liczba, `true`/`false`/`null`, tekst
w cudzysłowie, obiekt/tablica); w przeciwnym razie traktowana jako zwykły string:

```bash
buscall --db Demo call navigate_to_folder programFolderPath=Handel        # string
buscall --db Demo call open_form tableName=Towary objectID=2              # objectID = liczba
buscall --db Demo call update_field_value 'fieldsValues=["Nazwa=Buciki"]' # wartość = tablica JSON
```

- Wartości ze spacjami/znakami specjalnymi ujmij w cudzysłów powłoki:
  `"programFolderPath=Handel/Kartoteki/Towary i usługi"`.
- Wartość będąca JSON-em (obiekt/tablica) — cały argument w apostrofach powłoki, aby powłoka
  nie interpretowała `{}`/`[]`.
- Metoda bez argumentów: po prostu `buscall --db Demo call where_am_I`.

> **Parametr tablicowy bez nawiasów = błąd.** Jeśli metoda oczekuje tablicy, a podasz gołą wartość,
> dostaniesz:
> ```json
> {"kind":"error","error":"The requested operation requires an element of type 'Array', but the target element has type 'String'."}
> ```
> Komunikat mówi wyłącznie o typie, nie o nazwie parametru — sprawdź w `methods.list`, który
> argument jest tablicą, i ujmij go w nawiasy kwadratowe:
> ```bash
> buscall --db Demo call jakas_metoda 'buttons=Zapisz|Zapisano'      # ŹLE — string
> buscall --db Demo call jakas_metoda 'buttons=["Zapisz|Zapisano"]'  # DOBRZE — tablica JSON
> ```

## Odkrywanie metod i ich parametrów

Nie zgaduj nazw ani parametrów — odpytaj `methods.list` (zwraca schematy: `name`, `description`,
`parameters`, `*Hint`):

```bash
buscall --db Demo call methods.list                                        # pełne schematy
buscall --db Demo call methods.list | jq -r '.[].name'                     # same nazwy metod
buscall --db Demo call methods.list | jq '.[] | select(.name=="open_form") | .parameters'
```

## Odkrywanie ścieżki folderów

Ścieżkę do listy odkrywaj **nawigacją**, nie zgadując. `navigate_to_folder` zwraca albo **menu
podfolderów** (`kind: folderMenu`, pole `folders[]` z `programFolderPath`/`type`), albo — dla
foldera `type: list` — **od razu otwiera listę** (nie trzeba wtedy osobnego `retrieve_list`, aby
ją wyświetlić czy zrobić zrzut; `retrieve_list` jest wymagany dopiero przed `open_form`). Korzeń
menu programu odkryjesz podając **pusty** `programFolderPath`:

```bash
buscall --db Demo call navigate_to_folder programFolderPath=""                      # korzeń: główne moduły
buscall --db Demo call navigate_to_folder "programFolderPath=Kadry i płace/Kadry"   # drążenie w głąb
# wpis z "type":"list" — nawigacja do niego otwiera listę:
buscall --db Demo call navigate_to_folder "programFolderPath=Kadry i płace/Kadry/Pracownicy"
```

## Katalog metod (warstwa Bundle)

| Metoda | Do czego |
|---|---|
| `where_am_I` | bieżące położenie w aplikacji (bez argumentów); wywołane w korzeniu — punkt startowy odkrywania folderów |
| `get_folders` | lista podfolderów wskazanego foldera: `programFolderPath=<folder>` (odkrywanie struktury menu) |
| `get_configuration_folders` | strony okna konfiguracji (Ustawienia); z `regexFilter=<wzorzec>` przeszukuje **rekurencyjnie w głąb** — najszybszy sposób znalezienia strony ustawień (bez filtra zwraca jeden poziom) |
| `navigate_to_folder` | przejście do foldera programu, np. `programFolderPath=Handel/Kartoteki/Towary i usługi` |
| `retrieve_list` | odczyt danych listy (stronicowane); zwraca `data.rows[{objectID,values}]` i oznacza wiersze jako „odwiedzone" (wymagane przez `open_form`) |
| `open_form` | otwarcie formularza obiektu: `tableName=Towary objectID=<id>` |
| `open_subform` | otwarcie formularza **wiersza grida** bieżącego okna: `gridPath=<ścieżka-grida> id=<#id-wiersza>` — parametr nazywa się `id` (nie `objectID`!); wartości `gridPath` i `id` z odpowiedzi `detail=full` (patrz niżej) |
| `search_object` | otwarcie formularza po warunku: `tableName=… objectSelector=Kod=…` |
| `get_form_pages` | lista zakładek otwartego formularza (zwraca `pageID` do `switch_form_page`) |
| `switch_form_page` | zmiana zakładki formularza: `pageID=TowarCennikKontrahentowPage` |
| `cancel_form` | zamknięcie bieżącego okna/dialogu bez zapisu (np. okna „Wersja demonstracyjna" po zalogowaniu) |
| `get_actions` | lista czynności dostępnych w bieżącym kontekście; zwraca `workerID` w formie `Namespace.Worker,Assembly\|Metoda` |
| `execute_action` | wykonanie czynności: `workerID=<Namespace.Worker,Assembly\|Metoda>` |
| `get_grid_rows` | pełny grid z formularza + filtr regex: `gridPath=… regexFilter=… regexOptions=IgnoreCase` |
| `update_field_value` | zmiana pól: `'fieldsValues=["Nazwa=Buciki"]'` |
| `edit_grid_rows` | edycja / dodanie / usunięcie wierszy grida in-place |
| `take_screenshot` | zrzut ekranu bieżącego widoku → ścieżka do PNG (patrz niżej) |
| `application_close` | zamknięcie aplikacji Frame **wraz z serwerami** (patrz niżej); nie uruchamia jej, gdy nie działa |

Pełny, aktualny zestaw metod i ich parametry daje `methods.list` — powyższa tabela to najczęściej
używane. Grid w danych formularza jest domyślnie **obcinany do kilku wierszy** (`data.truncated=true`);
pełną zawartość pobiera `get_grid_rows`.

### Parametr `detail=header|full|none` — ile okna zwrócić

Metody renderujące okno (`navigate_to_folder`, `open_form`, `open_subform`, `switch_form_page`, …)
domyślnie zwracają **sam nagłówek** (`detail=header`). Treść formularza — sekcje, gridy
z `gridPath`, dane wierszy (`rowsCsv`), komendy — dopiero przy **`detail=full`**; `detail=none`
wyłącza render (najtańsze, gdy wynik nie jest potrzebny). W skryptach automatyzujących `gridPath`
i `#id` wierszy do `open_subform`/`edit_grid_rows` pozyskuje się z odpowiedzi `detail=full`
(pole `rowsCsv`).

### Identyfikatory obiektów są ulotne

Identyfikatory (`#id` z gridów, `objectID` z `retrieve_list`) **tracą ważność po restarcie
aplikacji / wylogowaniu**. Objaw: „Nieznany identyfikator obiektu 'X'… wczytaj listę zawierającą
ten obiekt ponownie". Po `application_close` + ponownym starcie zawsze odczytaj listy od nowa —
nie zapisuj identyfikatorów na później.

### Szukanie stron konfiguracji — `get_configuration_folders` z `regexFilter`

Bez `regexFilter` metoda zwraca jeden poziom drzewa; **z `regexFilter` przeszukuje rekurencyjnie
w głąb** — to najszybszy sposób znalezienia strony ustawień (np. dodanej własnym plikiem
`Config.*.pageform.xml` — [form-xml](../../form-xml/SKILL.md)):

```bash
buscall --db dev call get_configuration_folders "regexFilter=Agenci" limit=30
# → Ustawienia/Systemowe/Agenci AI/{Agenci,Dostawcy,Prompty}
```

### `retrieve_list` przed `open_form`

`retrieve_list` **musi** poprzedzać `open_form` — oznacza wiersze jako „odwiedzone", inaczej próba
otwarcia kończy się błędem „unsafe open". Typowy łańcuch: `navigate_to_folder` → `retrieve_list`
→ `open_form`.

### `take_screenshot` — kontrakt

Robi zrzut **bieżącego widoku** aplikacji. **Frame** zapisuje PNG w katalogu tymczasowym i zwraca
**samą ścieżkę** do pliku (bez base64, bez obrazu inline):

```bash
SHOT=$(buscall --db Demo call take_screenshot)
echo "$SHOT"     # np. /var/folders/.../T/soneta-screenshots/screenshot-<data>.png
```

- Schemat deklaruje opcjonalny parametr `databaseName` („przełącza bazę przed zrzutem"); w praktyce
  wołaj **bez argumentów**, aby zrzucić bieżący widok.
- Wymaga prawa `Zrzuty ekranu` (`BundleRights.Screenshots`) w roli operatora.
- Pliki są efemeryczne — kasuje je **Frame** (proces długożyjący) przy starcie serwera pipe;
  krótkożyjący `call` pliku **nie** usuwa, więc ścieżka pozostaje ważna po zakończeniu polecenia.

- **Wymaga otwartej bazy** — gdy aplikacja stoi na ekranie wyboru baz albo na dialogu, zwraca
  „Brak otwartej bazy danych". Diagnostycznie ratuje wtedy systemowy zrzut całego ekranu (macOS):
  `screencapture -x /tmp/frame.png` i obejrzenie pliku.

Wykorzystanie zrzutu do wizualnej weryfikacji layoutu/pól opisuje [buscall-live-testing.md](../../programming/references/buscall-live-testing.md).

### `application_close` — kontrakt

Łagodnie zamyka aplikację **Frame** (bez argumentów). Zamknięcie przechodzi przez wewnętrzne
`SourceManager.CloseAll()`, więc **kończy też procesy serwerów** (`server.dll`/`web.dll`) i zwalnia
porty — nie zostają osierocone procesy ze starym kodem.

```bash
buscall call application_close      # zwykle bez --db; zamyka bieżącą instancję Frame
```

- **Nie uruchamia** aplikacji tylko po to, by ją zamknąć: gdy Frame nie działa, zwraca komunikat
  „…nie jest uruchomiona…" i nie startuje procesu.
- Wraca **dopiero** gdy proces Frame faktycznie zniknął, więc kolejne wywołania nie wstrzelą się
  w zamykaną aplikację.
- To **preferowany** sposób zamknięcia/przeładowania kodu (zamiast `kill`). Ręczne ubijanie
  osieroconych serwerów zostaje jako procedura awaryjna — patrz [buscall-live-testing.md](../../programming/references/buscall-live-testing.md).

## Typowy przepływ: zrzut formularza od zera

Od zimnego startu do obejrzanego zrzutu ekranu (szczegóły i konfiguracja bazy z własnym kodem —
[buscall-live-testing.md](../../programming/references/buscall-live-testing.md)):

```bash
# 0) pierwsze `call` z --db STARTUJE frame (wolno, potem zostaje w tle);
#    po autologinie może wyskoczyć okno „Wersja demonstracyjna" — zamknij je:
buscall --db moja_baza call cancel_form

# 1) odkrywanie folderów: korzeń + drążenie
buscall --db moja_baza call where_am_I
buscall --db moja_baza call get_folders programFolderPath=<folder>

# 2) lista → formularz → zakładka → zrzut
buscall --db moja_baza call navigate_to_folder "programFolderPath=<ścieżka-foldera>"
ID=$(buscall --db moja_baza call retrieve_list | jq -r '.data.rows[0].objectID')
buscall --db moja_baza call open_form tableName=<tbl> objectID="$ID"
buscall --db moja_baza call get_form_pages                       # dostępne pageID
buscall --db moja_baza call switch_form_page pageID=<x>
buscall --db moja_baza call take_screenshot                      # → ścieżka PNG: otwórz i OBEJRZYJ

# 3) czynności (menu „Czynności")
buscall --db moja_baza call get_actions                          # zwraca workerID
buscall --db moja_baza call execute_action "workerID=<Namespace.Worker,Assembly|Metoda>"

# koniec pracy / przeładowanie kodu:
buscall call application_close                                   # bez --db
```

## Wyniki i kody wyjścia

- **Wynik metody** wraca jako JSON na STDOUT.
- **Błąd wykonania metody** (np. złe `regexOptions`, brak prawa) → JSON `{"kind":"error","error":"..."}`
  na STDOUT, **kod wyjścia 0** (samo wywołanie się powiodło).
- **Błąd samego wywołania** (nieznana metoda, brak połączenia z pipe) → komunikat na **STDERR**
  i **kod wyjścia 1**.

### Metody, które nie odpowiadają od razu

Większość metod wraca natychmiast, ale niektóre **otwierają okno i czekają na reakcję operatora** —
`call` wisi wtedy tak długo, aż ktoś kliknie w aplikacji. To nie jest zawieszenie: dopiero
kliknięcie generuje wynik.

Żeby zobaczyć, na co właściwie czekasz, uruchom takie wywołanie **w tle** i zrób zrzut ekranu
drugim procesem:

```bash
buscall --db Demo call <metoda-czekajaca-na-operatora> ... &   # blokuje do czasu kliknięcia
buscall --db Demo call take_screenshot                         # osobny proces, wraca od razu
```

Uwaga: `application_close` **przerywa** takie oczekiwanie — zrywa named pipe, więc czekające
wywołanie kończy się komunikatem o zerwanym połączeniu i wyniku już nie zobaczysz. Jeśli zależy ci
na wyniku, zamknij okno w aplikacji (albo poproś operatora), a nie zamykaj całego frame'a.

### Diagnostyka: log serwera

Błędy widoczne w UI jako gołe dialogi (np. „Brak praw dostępu do danych") mają pełny stack trace
w logu serwera — na macOS: `~/Library/Application Support/Soneta/Logs/server-RRRRMMDD.log`
(JSON per linia, pole `Exception`). Pełny opis logowania: artykuł [translations-logging](../../programming/references/translations-logging.md). Błędy **samej ramki i `buscall`**
(np. sterowanie oknem, protokół) leżą gdzie indziej: `Soneta.Frame/Logs/{Frame,BusCall}/error-*.log`.
Patrz [Logi ramki i `buscall`](sonetaframe.md#logi-ramki-i-buscall).

## Checklista automatyzacji buscall

- [ ] `--db` = **caption** źródła (albo nazwa bazy, gdy caption brak) — nie fizyczna nazwa bazy SQL;
      przy niejednoznaczności: identyfikator `--db "Process|<NazwaBazy>"`.
- [ ] Nazwy metod z `methods.list`, nie z pamięci (np. `where_am_I`, nie „where_I_am";
      objaw literówki: `{"kind":"error","error":"Nazwa metody MCP nieznaleziona"}`).
- [ ] `detail=full` tam, gdzie potrzebne `gridPath`/`rowsCsv`; `detail=none`, gdy wynik zbędny.
- [ ] Po restarcie aplikacji identyfikatory (`#id`, `objectID`) czytane od nowa.
- [ ] Do testów źródło `process:` (`--db "Process|<Baza>"`) — stawia serwery samo; wpis `http://…`
      wymaga zewnętrznego serwera, a jego brak daje mylące „Niepoprawny adres serwera" / „Nie znaleziono bazy".
- [ ] Przy błędach UI: log serwera `server-RRRRMMDD.log`; błędy ramki/`buscall`: `Soneta.Frame/Logs/`.

## Wariant zgodny z MCP: `callmcp`

Gdy potrzebujesz warstwy zgodnej z protokołem MCP (np. własny orkiestrator budujący JSON-RPC),
użyj `callmcp` — czyta **cały STDIN** jako pojedynczy komunikat JSON-RPC 2.0 `tools/call`, wykonuje
go i wypisuje na STDOUT **odpowiedź JSON-RPC MCP** (`result` = CallToolResult lub `error`):

```bash
echo '{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"where_am_I","arguments":{}}}' \
  | buscall --db Demo callmcp
# -> {"jsonrpc":"2.0","id":1,"result":{"content":[{"type":"text","text":"…"}],"isError":false}}
```

Błąd wykonania narzędzia wraca zgodnie z konwencją MCP w `result` z `isError:true` (a nie jako
JSON-RPC `error`); błąd parsowania/nieprawidłowy komunikat → JSON-RPC `error` i kod wyjścia 1.

## Tryb `mcp` — pułapki testowania z wiersza poleceń

Długożyjący serwer stdio (`buscall --db <Baza> mcp`) ma trzy cechy, które mylą przy ręcznych testach:

- **STDIN musi zostać otwarty.** Przy `mcp < plik.jsonl` proces kończy się na EOF, zanim wypłucze
  odpowiedzi — STDOUT wychodzi pusty, choć w logu widać „sending message". Trzymaj wejście
  otwarte przez FIFO i czytaj odpowiedzi z pliku wyjściowego:

  ```bash
  mkfifo in.fifo
  buscall --db "Process|Demo" mcp < in.fifo > out.jsonl 2> err.log &
  exec 8> in.fifo                      # deskryptor trzyma FIFO otwarte
  head -4 wejscie.jsonl >&8            # initialize, initialized, tools/list, tools/call…
  until grep -q '"id":3' out.jsonl; do sleep 1; done
  echo '{"jsonrpc":"2.0","id":4,"method":"tools/list","params":{}}' >&8
  ```

- **Żądania są obsługiwane współbieżnie.** Wysłane hurtem `tools/call` i `tools/list` wykonają się
  równolegle, więc lista może powstać przed skutkiem wywołania. Scenariusz „wywołaj narzędzie,
  potem sprawdź odświeżoną listę" wymaga czekania na odpowiedź o danym `id` (jak wyżej).

- **Lista metod jest cache'owana** w `~/Library/Application Support/BusCall/methods.{pipe}.{db}.{agent}.json`
  (Windows: `%LOCALAPPDATA%\BusCall\`). `tools/list` czyta ten plik zamiast odpytywać program —
  żeby sam odczyt listy nie budził ramki. Przy zmianach w zestawie metod nieaktualny plik wygląda
  jak błąd serwera; lista odświeża się dopiero po pierwszym `tools/call` (wtedy przychodzi
  `notifications/tools/list_changed`). W razie wątpliwości usuń plik cache.
