# Weryfikacja kodu na żywej aplikacji przez `buscall` (zrzuty ekranu)

Konkretne zastosowanie `buscall`: **wizualna weryfikacja działania aplikacji podczas tworzenia
i sprawdzania kodu**. Zamiast klikać ręcznie, sterujesz uruchomioną aplikacją z CLI (nawigacja,
otwieranie formularzy, edycja pól) i robisz **zrzuty ekranu**, które oglądasz, aby potwierdzić
layout, wartości pól, widoczność kontrolek czy motyw — na realnej bazie i na **swoim** kodzie.

Składnię wywołań, katalog metod i kody wyjścia opisuje [buscall.md](../../tools/references/buscall.md). Tu skupiamy się
na tym, co jest specyficzne dla weryfikacji na żywo: **konfiguracji bazy startującej z Twojego
kodu**, przeładowaniu kodu i pułapkach procesów.

> **Zanim pierwszy raz uruchomisz `buscall`:** zapoznaj się z **aktualną składnią dostępnych metod**.
> Katalog metod bywa różny między wersjami/gałęziami kodu — nie zakładaj z pamięci nazw metod ani ich
> parametrów. Odczytaj bieżącą listę bezpośrednio z uruchomionego frame'a:
>
> ```bash
> buscall --db Demo call methods.list
> ```
>
> Pełny opis odkrywania metod (`methods.list`) i ich kontraktów znajdziesz w [buscall.md](../../tools/references/buscall.md).

Używamy trybu `call` — każde wywołanie jest niezależne (`buscall --db <Baza> call <metoda> …`),
frame (GUI) startuje przy pierwszym wywołaniu i **zostaje** w tle, więc kolejne `call`-e są szybkie.

## Wymaganie wstępne: baza startująca z Twojego kodu

Parametr `--db Demo` odwołuje się do bazy o nazwie **`Demo` zdefiniowanej w aplikacji
`SonetaFrame`**. To połączenie musi być ustawione tak, aby **startowało z kodu bieżącego projektu**
(biblioteki z katalogu build `bin/Debug`). Bez tego `buscall --db Demo call …` albo nie znajdzie
bazy, albo uruchomi frame na innym (nieaktualnym) kodzie — i zweryfikujesz nie to, co trzeba.

Innymi słowy: nazwa po `--db` = nazwa połączenia w SonetaFrame, a to połączenie decyduje, skąd
ładowany jest kod. Testujesz inny projekt/bazę → zdefiniuj analogiczne połączenie i podaj jego nazwę.

### Gdzie jest definicja bazy

Konfiguracja połączeń SonetaFrame to plik `Settings_Standard.json` — macOS:
`~/Library/Application Support/Soneta.Frame/`, Windows: `%APPDATA%\Soneta.Frame\`
(pełna tabela lokalizacji: [sonetaframe.md](../../tools/references/sonetaframe.md)) — tablica **`Sources`**. Każdy
wpis to string z parametrami rozdzielonymi `;`. Przykład bazy `Demo` startującej kod z projektu:

```json
"Sources": [
  "process:Demo;path=<katalog-projektu>;user=Administrator;pwd="
]
```

Znaczenie pól:
- `process:Demo` — tryb `process` + **nazwa bazy** `Demo` (przy braku `caption` to wartość
  podawana w `buscall --db Demo`),
- `path=<katalog-projektu>` — **katalog projektu, z którego ładowany jest kod** (biblioteki z jego `bin/Debug`),
- `user` / `pwd` — poświadczenia logowania (tu operator `Administrator`, puste hasło).

> **⚠️ `--db` dopasowuje w kolejności: identyfikator → `caption` → nazwa bazy.** Gdy źródło ma
> etykietę, np. `process:Demo;caption=dev;…`, wywołuj `buscall --db dev` — a **nie** `--db Demo`
> (`--db Demo` może przy tym trafić w INNE źródło, którego caption to `Demo`). Objaw złej nazwy:
> `[-32603] Brak połączenia z serwerem: Demo` przy każdym wywołaniu (aplikacja stoi na ekranie
> wyboru baz). Jednoznacznie wskażesz źródło identyfikatorem: `--db "Process|Demo"`.
> Szczegóły: [buscall](../../tools/references/buscall.md) i [sonetaframe](../../tools/references/sonetaframe.md).

> **Poświadczenia a automatyzacja.** Podanie tu `user`/`pwd` sprawia, że frame **loguje się do
> bazy automatycznie** — dzięki temu `buscall`/refaktoryzacja działają w pełni bezobsługowo
> (start aplikacji + zalogowanie bez ingerencji). **Bez** tych poświadczeń logowanie do bazy trzeba
> **za każdym razem wykonać ręcznie** w GUI i **nie da się tego obejść** żadnym parametrem `buscall`.
>
> **⚠️ Nigdy nie wpisuj tu poświadczeń produkcyjnych — wyłącznie testowe.** Ten plik przechowuje
> hasło **jawnie/niezabezpieczone**, więc każda tu wpisana wartość jest w praktyce dostępna dla
> każdego, kto ma dostęp do dysku. Używaj konta testowego bez dostępu do danych produkcyjnych.

Aby przetestować inny projekt: dodaj analogiczny wpis w `Sources` z własną nazwą bazy i `path`,
po czym wywołuj `buscall --db <nazwa> call …`. Dodatkowo `IsDeveloperMode: true` w tym samym pliku
włącza tryb dewelopera.

### Baza z WŁASNYM dodatkiem — przepis w trzech krokach

Gdy testujesz **własny dodatek** (np. `Soneta.MojDodatek`), potrzebna jest baza z jego tabelami
i frame ładujący jego DLL-e. Przepis (szczegóły w skillu [tools](../../tools/SKILL.md)):

1. **Per-bazowy `serversettings.json`** — tablica `Ext` (DLL logiki + `.UI` z `bin/Debug` dodatku)
   oraz `Server.DbRegister` (połączenie SQL). Struktura pliku: [dbmgr.md](../../tools/references/dbmgr.md),
   sekcja „Baza z własnym dodatkiem".
2. **Utworzenie bazy z tabelami dodatku i danymi demo** (~3 min):
   ```bash
   dbmgr create moja_baza --config-file=<ścieżka>/serversettings.json --demo gold --recreate
   ```
3. **Wpięcie w SonetaFrame** — wpis w `Sources` z **tym samym** `serversettings.json`
   (jedno źródło prawdy):
   ```
   process:moja_baza;caption=moja_baza;path=<katalog-kodu-soneta>;user=Administrator;pwd=;config-file=<ścieżka>/serversettings.json
   ```
   ⚠️ Klucz to **`config-file=`**, nie `config=` (starsza forma nie działa). Szczegóły
   modyfikatorów: [sonetaframe.md](../../tools/references/sonetaframe.md).

Pułapki:
- **Prawa do dodatku:** operator `Administrator` z bazy demo-gold **nie ma praw** do obiektów
  nowego dodatku — nadaj rolę z `Dodatki=Granted` (np. `dbmgr importxml` pliku roli; plik musi być
  UTF-8 i poprawnie zaescape'owany — błędny import potrafi **zablokować logowanie**).
- **Okno „Wersja demonstracyjna":** po autologinie na bazie demo wyskakuje dialog — zamknij go
  pierwszym wywołaniem `buscall --db moja_baza call cancel_form`, inaczej blokuje nawigację.

## Architektura

```
CLI (jednorazowo):  buscall --db Demo call <metoda> [klucz=wartość ...]
   │  connect (named pipe "SonetaFrameBridge"); jak brak — spawn frame
   ▼
buscall  ── named pipe "SonetaFrameBridge" ──►  SonetaFrameNew (GUI, zostaje w tle)
(most)                                            └── ładuje Soneta.*.dll z bin/Debug projektu
```

- `buscall call` **tylko mostuje** jedno wywołanie przez named pipe do GUI **`SonetaFrameNew`**.
  To **frame** wykonuje metody i **ładuje biblioteki z `bin/Debug` Twojego projektu**.
- Jeśli frame nie działa, `buscall` **sam go uruchamia** przy pierwszym wywołaniu i czeka na gotowość.
  Frame pozostaje uruchomiony po zakończeniu `call` — kolejne `call`-e podłączą się do niego przez pipe.

### Aby frame miał NOWY kod

1. **Zamknij działający frame** metodą `application_close` — zamyka GUI *i* sprząta jego serwery
   (`server.dll`/`web.dll`), więc nie zostają osierocone procesy trzymające porty i stary kod:
   ```bash
   buscall call application_close     # bez --db; zamyka bieżącą instancję
   ```
   - Gdy frame **nie działa**, metoda nic nie robi (**nie uruchamia** go po to, by zaraz zamknąć)
     i zwraca komunikat „…nie jest uruchomiona…".
   - Wywołanie wraca **dopiero** gdy proces faktycznie zniknął, więc kolejne `call`-e nie wstrzelą
     się w zamykaną aplikację.
2. Przebuduj właściwy projekt logiki: `dotnet build <projekt>.csproj` (ten, którego zmiany testujesz).
3. Pierwsze wywołanie `call` uruchomi świeży frame z nowym DLL.

**Dlaczego `application_close`, a nie `kill`:** łagodne zamknięcie przechodzi przez
`SourceManager.CloseAll()` → `ConnectionSource.Close()`, które zatrzymuje procesy serwerów
(`Kill(true)` na całym drzewie). Dzięki temu porty są zwalniane, a stary kod nie zostaje w tle.
Osierocone serwery (PPID=1, zajęte porty, nieaktualny kod) powstają **tylko** gdy frame zostanie
ubity gwałtownie — na taki wypadek jest nota ratunkowa niżej.

### Ratunek: osierocone serwery i porty (awaryjnie, gdy `application_close` nie pomógł)

> Ta sekcja to **ręczne czyszczenie awaryjne** — potrzebne tylko gdy frame padł/został ubity
> gwałtownie i zostawił osierocone serwery, albo gdy z jakiegoś powodu `application_close` nie
> posprzątał (np. frame nie odpowiada). W normalnym trybie zamykaj przez `application_close`.

Frame **nie jest** procesem o nazwie `SonetaFrameNew` — uruchamia dwa procesy `dotnet` z DLL-kami
z katalogu build projektu, które **nasłuchują na portach TCP**:
- **`server.dll`** — `--runjobs=true --SingleDbName=<Baza> --Urls=http://+:<port>` (obserwowane: `22101`, dodatkowo `4000`),
- **`web.dll`** — `--server-endpoint=http://localhost:<portServera> --Urls=http://+:<port>` (obserwowane: `5101`).

Osierocone (`PPID=1`) trzymają porty i **ładują STARY kod** (sprzed rebuildu). Podłączenie do nich =
weryfikacja nieaktualnego kodu. `grep SonetaFrameNew` ich **nie znajdzie** — szukaj po
`server.dll`/`web.dll`/`--SingleDbName`.

**WAŻNE — porty per baza:** każda baza otwierana w programie dostaje **własny port**. Nie sprawdzaj
tylko `22101`/`5101` — przeskanuj **kilka–kilkanaście kolejnych** (np. `22101–22120`, `5101–5120`,
`4000–4010`), bo równolegle otwarte bazy zajmują sąsiednie porty.

```bash
# 1) Znajdź osierocone serwery Soneta (dowolna baza) — po DLL, nie po nazwie frame:
ps ax -o pid,ppid,etime,command | grep -Ei 'server\.dll|web\.dll|--SingleDbName' | grep -v grep
# 2) Sprawdź zajęte porty w ZAKRESIE (nie pojedyncze!) — każda baza = inny port:
lsof -nP -iTCP -sTCP:LISTEN 2>/dev/null | grep -E ':(4[0-9]{3}|22[0-9]{3}|5[0-9]{3})\b'
# 3) Ubij osierocone procesy (web przed server), potwierdź zwolnienie portów:
ps ax -o pid,command | grep -Ei 'web\.dll|server\.dll' | grep -v grep | awk '{print $1}' | xargs -r kill
lsof -nP -iTCP -sTCP:LISTEN 2>/dev/null | grep -E ':(4[0-9]{3}|22[0-9]{3}|5[0-9]{3})\b'   # ma być pusto
```

Dopiero po wyczyszczeniu portów pierwsze wywołanie `call` wystartuje świeży frame/serwer z nowym kodem.
Zabicie tych procesów jest bezpieczne i odwracalne (frame odtworzy je przy następnym wywołaniu).

## Zrzut ekranu → analiza wizualna

Sedno tej weryfikacji: `take_screenshot` zwraca **ścieżkę do PNG** bieżącego widoku (kontrakt metody
opisuje [buscall.md](../../tools/references/buscall.md), sekcja `take_screenshot — kontrakt`). Otwórz plik z tej ścieżki i **obejrzyj go**
narzędziem czytającym obrazy — tak potwierdzasz wizualnie layout formularza, wartości pól, widoczność
kontrolek, wyrównanie, motyw itp. To krok, którego nie zastąpi odczyt danych JSON-em.

```bash
SHOT=$(buscall --db Demo call take_screenshot)
echo "$SHOT"     # ścieżka do PNG — otwórz i obejrzyj
```

### Zasada raportowania: „zweryfikowane buildem" ≠ „zweryfikowane wizualnie"

Layout i zachowania UI (formularze, widoczność pól, wyrównanie, zakładki) weryfikuje się
**TYLKO wizualnie** — przez `buscall` + zrzut ekranu i obejrzenie PNG. Sam poprawny build
(kompilacja, brak błędów XML) **nie** potwierdza, że formularz wygląda i działa dobrze.
W raportach dla użytkownika **rozróżniaj jawnie**: „zweryfikowane buildem" (kod się kompiluje)
vs „wymaga weryfikacji runtime" (wygląd/zachowanie nie zostały jeszcze obejrzane na żywo).

## Minimalny przepływ (skopiuj i uruchom)

Przykład: otwarcie konkretnego towaru i zrzut ekranu do oceny wyglądu formularza.

```bash
BUSCALL=<ścieżka>/buscall     # binarka z katalogu build projektu BusCall
DB=Demo

# 1) nawigacja do listy (pierwsze wywołanie może wystartować frame — daj czas)
"$BUSCALL" --db "$DB" call navigate_to_folder "programFolderPath=Handel/Kartoteki/Towary i usługi"

# 2) retrieve_list — MUSI poprzedzać open_form (oznacza wiersze jako odwiedzone)
ID=$("$BUSCALL" --db "$DB" call retrieve_list | jq -r '.data.rows[0].objectID')

# 3) otwarcie formularza po liczbowym objectID
"$BUSCALL" --db "$DB" call open_form tableName=Towary objectID="$ID"

# 4) (opcjonalnie) zmiana zakładki
"$BUSCALL" --db "$DB" call switch_form_page pageID=TowarCennikKontrahentowPage

# 5) zrzut ekranu bieżącego widoku -> ścieżka do PNG (otwórz go i obejrzyj)
"$BUSCALL" --db "$DB" call take_screenshot
```

Uwagi praktyczne:
- Pierwsze wywołanie po starcie bywa wolne (uruchomienie/logowanie frame'a) — dawaj timeout ~30–60 s
  (zimny start po ubiciu serwerów potrafi przekroczyć 2 min). Kolejne `call`-e są szybkie (frame w tle).
- `retrieve_list` **musi** poprzedzać `open_form` (inaczej „unsafe open").
- Aby przeładować kod, zamknij frame przez `buscall call application_close` (posprząta też serwery),
  przebuduj i wywołaj `call` ponownie. Do zabijania osieroconych `server.dll`/`web.dll` sięgaj tylko
  awaryjnie (patrz nota ratunkowa wyżej) — samo `grep SonetaFrameNew` i tak NIE wystarczy.

Pełna składnia metod, `methods.list` i wariant `callmcp`: [buscall.md](../../tools/references/buscall.md).
