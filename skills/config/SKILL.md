---
name: config
description: >
  Konfiguracja i funkcje domenowe platformy Soneta (enova365, Triva) — dla partnerów i zespołu Soneta:
  (A) IMPORT/EKSPORT DANYCH I USTAWIEŃ przez XML —
  element session, import według rekordów (*.dbinit.xml, demo) i przez logikę biznesową
  (business="true"), eksport datapacku, GUID; (B) `scan-folders` — foldery menu
  (`[assembly: FolderView]`) z DLL; (C) KONFIGURACJA URUCHOMIENIOWA `appsettings.json` — porty i adresy,
  warstwy nadpisań (`-c`, `SONETA_`, CLI); (D) REJESTR KONFIGURACJI (ConfigReg,
  „Zarządzanie konfiguracją") — zrzut konfiguracji bazy do `*.reg.json`, porównanie i scalanie,
  sigile (`$strict`, `$v`, `#klucz`, `@atrybut`). Używaj gdy użytkownik: (1) buduje XML importu,
  pyta o atrybuty guid/where/key/id/business/deleted/dbversion; (2) eksportuje rekordy guidowane;
  (3) mapuje menu z DLL; (4) pyta o klucz `appsettings.json`; (5) pyta o rejestr konfiguracji,
  debuguje `*.reg.json` albo przenosi ustawienia między bazami. Kod importu → programming;
  operacje na bazie z CLI → tools.
---

# Ustawienia, konfiguracja i funkcje domenowe platformy Soneta (enova365, Triva)

Skill gromadzi narzędzia i mechanizmy związane z **konfiguracją systemu** oraz
**funkcjami domenowymi** platformy Soneta. Zawartość jest rozwijana — poniżej udokumentowane
są wyłącznie mechanizmy faktycznie obecne w skillu; kolejne artykuły dodawane są sukcesywnie.

## Kiedy ten skill, a kiedy inny

| Potrzeba | Skill |
|---|---|
| **Budowa pliku XML importu/eksportu danych i ustawień** (dbinit.xml, demo, przenoszenie konfiguracji) | **config** ([import-export-xml](references/import-export-xml.md)) |
| **Gotowy wzorzec pliku importu** dla typowego obiektu (słownik, definicja, rola, cecha, szablon, kokpit) | **config** ([import-xml-examples](references/import-xml-examples.md)) |
| Inwentaryzacja/mapa **folderów statycznych menu** (`[assembly: FolderView]`) z DLL | **config** ([scan-folders](references/scan-folders.md)) |
| **Konfiguracja uruchomieniowa** (porty, adresy komponentów, warstwy nadpisań `appsettings.json`) | **config** ([appsettings](references/appsettings.md)) |
| **Rejestr konfiguracji** (ConfigReg): `*.reg.json`, porównanie i scalanie ustawień między bazami | **config** ([config-reg](references/config-reg.md)) |
| Kod obsługujący import/eksport (`SessionReader`/`SessionWriter`), klasy ORM, workery | [programming](../programming/SKILL.md) |
| Inwentaryzacja modułów/tabel (`scan-modules`), pól (`scan-props`), workerów (`scan-workers`) | [programming](../programming/SKILL.md) |
| Operacje na bazie z CLI (dbmgr), test na żywej aplikacji (buscall) | [tools](../tools/SKILL.md) |
| Definicja struktury tabel/kolumn/relacji w XML (business.xml) | [business-xml](../business-xml/SKILL.md) |
| Formularze i widoki UI (form.xml) | [form-xml](../form-xml/SKILL.md) |
| Planowanie całego dodatku/modułu | [addon-planning](../addon-planning/SKILL.md) |

## Artykuły i narzędzia

### Import i eksport danych przez XML — [references/import-export-xml.md](references/import-export-xml.md)

Struktura i sposób budowania plików XML (`<session xmlns="http://www.soneta.pl/schema/business">`)
do wczytywania danych i ustawień konfiguracyjnych oraz ich eksportu. Trzy części:

1. **Import według rekordów** (domyślny) — dane wprost do pól rekordów, bez logiki biznesowej;
   kolejność pól bez znaczenia; do danych konfiguracyjnych, plików `*.dbinit.xml` i bazy demo.
2. **Import przez logikę biznesową** (`business="true"`) — ustawianie właściwości biznesowych
   z pełną walidacją; kolejność elementów jak przy wpisywaniu danych na formularzu.
3. **Eksport** — wskazany rekord guidowany + rekordy powiązane (datapack), wynik zdatny
   do ponownego importu; podstawa przenoszenia ustawień między bazami.

Artykuł specyfikuje identyfikację rekordów (GUID, `where`, `key`, `id`), formaty wartości
(liczby, daty, referencje, kwoty z walutą), atrybuty specjalne oraz reguły plików
`*.dbinit.xml` (`priority`, `versionName`, `dbversion`). Zawiera też gotowy, zweryfikowany
importem **przykład** (obiekt w modelu „root + historia") —
[examples/import-pracownik-etatowy.xml](examples/import-pracownik-etatowy.xml).

### Katalog wzorców importu XML — [references/import-xml-examples.md](references/import-xml-examples.md)

Wzorce wyniesione z kilkuset standardowych plików `*.dbinit.xml` platformy: nagłówek i piętra
`priority`, strukturalne GUID-y, trzy formy referencji (`GUID`, `Tabela:GUID`, `id` z pliku),
patch rekordu w kolejnej wersji (`updateonly`), `insertonly`, `key`, `duplicate`/
`duplicateKeyField`, semantyka pustych elementów, kod C#/HTML/XML w treści pola. Szkielety dla
typowych obiektów: słowniki, definicje dokumentów i zadań, cechy, szablony e-mail, konfiguracja
(`CfgNode`), role systemowe i prawa (`Right`), projekty runtime, kokpity, tuple. Pliki
w [examples/](examples/): `dbinit-slownik-i-poprawki.dbinit.xml`, `import-rola-i-prawa.xml`,
`import-cecha-i-szablon-email.xml`, `import-definicja-elementu-wynagrodzenia.xml` (kreator
algorytmu i edytor z kodem C#; parametry algorytmu → [place-def-elementow](../place-def-elementow/SKILL.md)).

### Mechanizm zasilania bazy Demo — [references/demo-data.md](references/demo-data.md)

Jak działa import danych przykładowych przy tworzeniu bazy (`dbmgr create --demo gold|silver`,
kreator baz): katalog `Demo` obok binariów (bez manifestu, nierekurencyjnie), kolejność plików
przez sortowanie leksykograficzne (prefiksy numeryczne stałej szerokości, referencje tylko
„w przód"), sufiksy koloru licencji `.gold`/`.silver`, GUID-y wstawiane 1:1, rekordy standardowe
(dbinit) przed danymi demo. Z checklistą nowego pliku demo i sposobami testowania
(podwójny `importxml` / pełne `create --demo`).

### `scan-folders` — [references/scan-folders.md](references/scan-folders.md)

Buduje drzewo **folderów statycznych menu** programu z atrybutów assembly
`[assembly: FolderView(...)]` (oraz pochodnych) w skompilowanych bibliotekach. Klasyfikuje
węzły na listy (`ViewInfoType`/`ViewType`/`TableName`), formularze (`ObjectType`) i menu;
przy widokach pokazuje `Description` oraz powiązaną tabelę/`ViewInfo`. Filtr prefiksem ścieżki
i tryb `--flat`. Czyta metadane przez Roslyn — bez uruchamiania aplikacji i bez ładowania IL
do CLR. Skrypt: [scripts/scan-folders.csx](scripts/scan-folders.csx).

Perspektywa **funkcjonalno-użytkowa** (co użytkownik klika w menu) — komplementarna do
perspektywy **danych** ([scan-modules](../programming/references/scan-modules.md)) i **pól** ([scan-props](../programming/references/scan-props.md)) ze skilla
`programming`. Planistyczne użycie inwentaryzacji
opisuje [addon-planning](../addon-planning/SKILL.md).

### Konfiguracja uruchomieniowa `appsettings.json` — [references/appsettings.md](references/appsettings.md)

Ustawienia uruchomieniowe komponentów (orchestrator, server, web, webapi, webwcf, router,
commhub): **dwa pliki** `appsettings.json` (część back-end i część front-end) i które
komponenty obsługują, mapa domyślnych **portów** oraz **trzy pary adresów, które muszą się
zgadzać** (`Server:Urls`↔`ServerEndpoint`, `Router:Urls`↔`RouterEndpoint`,
`CommHub:Urls`↔`CommHubClient`), **kolejność warstw nadpisań** (plik bazowy → profil systemu
→ nakładka `-c` → zmienne `SONETA_` → argumenty CLI) — źródło najczęstszej pułapki „zmieniłem
`appsettings.json`, a nic się nie zmieniło" — dwa tryby połączenia frontend→serwer
(bezpośredni vs przez router) oraz słownik znaczeń kluczy. Zawiera checklistę zmiany
portu/adresu.

Uruchamianie komponentów, ramki hostującej i zarządzanie bazami opisuje
[tools](../tools/SKILL.md).

### Rejestr konfiguracji (ConfigReg) — [references/config-reg.md](references/config-reg.md)

Zrzut **całej konfiguracji bazy** do jednego drzewa zapisywanego jako `*.reg.json`, który da się
porównać z inną bazą, scalić i wgrać z powrotem (menu **Narzędzia → Zarządzanie konfiguracją**).
Artykuł opisuje pięć operacji formularza, zakres rejestru (co wchodzi, a co zostaje poza nim —
w tym pola wrażliwe), format pliku wraz z sigilami (`$strict`, `$v`, `#klucz`, `@atrybut`,
`$blob`), składnię ścieżek węzłów, semantykę kolekcji `$strict` (**usuwa** wiersze nieobecne
w pliku) oraz diagnostykę najczęstszej pułapki — wiersza pokazywanego jednocześnie jako usunięty
i dodany. Zawiera dwie checklisty: przeniesienie ustawień między bazami i kroki przed scaleniem
do bazy operacyjnej.

**Nie myl dwóch mechanizmów przenoszenia ustawień.** XML (`<session>`, `*.dbinit.xml`) przenosi
*wskazane rekordy* i nie wykrywa różnic. Rejestr zdejmuje *stan konfiguracji jako całość*,
porównuje i scala. Formaty plików są rozłączne — kryterium wyboru i porównanie w tabeli na
początku artykułu.

## Powiązania

- [programming](../programming/SKILL.md) — warstwa kodu importu/eksportu
  ([sessionreader-sessionwriter](../programming/references/sessionreader-sessionwriter.md)), rekordy guidowane ([datapack-guidedrow](../programming/references/datapack-guidedrow.md)), `OnImporting`/
  `OnImported` ([row-types](../programming/references/row-types.md)), testy z `ImportBusinessXml` ([integration-tests](../programming/references/integration-tests.md)), skany DLL
  ([scan-modules](../programming/references/scan-modules.md), [scan-props](../programming/references/scan-props.md), [scan-workers](../programming/references/scan-workers.md)) oraz pisanie folderów/list w C#.
- [addon-planning](../addon-planning/SKILL.md) — użycie inwentaryzacji menu
  na etapie planowania dodatku.
- [tools](../tools/SKILL.md) — operacje na bazie z CLI, weryfikacja efektów
  importu na żywej aplikacji (buscall).
- [business-xml](../business-xml/SKILL.md) — definicja tabel i kolumn, w tym
  oznaczanie tabeli jako konfiguracyjnej, co decyduje o jej obecności w rejestrze konfiguracji.
- [erp](../erp/SKILL.md) — mapa wyboru skilla.
