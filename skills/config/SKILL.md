---
name: config
description: >
  Konfiguracja platformy Soneta (enova365, Triva) — dla partnerów i zespołu Soneta:
  (A) IMPORT/EKSPORT DANYCH I USTAWIEŃ przez XML —
  element session, import według rekordów (*.dbinit.xml, demo) i przez logikę biznesową
  (business="true"), eksport datapacku, GUID; eksport listy do Excela/CSV; (B) `scan-folders` — foldery menu
  (`[assembly: FolderView]`) z DLL; (C) KONFIGURACJA URUCHOMIENIOWA `appsettings.json` — porty i adresy,
  warstwy nadpisań (`-c`, `SONETA_`, CLI); (D) REJESTR KONFIGURACJI (ConfigReg,
  „Zarządzanie konfiguracją") — `*.reg.json`, porównanie i scalanie,
  sigile (`$strict`, `#klucz`, `@atrybut`). Używaj gdy użytkownik: (1) buduje XML importu,
  pyta o atrybuty guid/where/key/id/business/deleted/dbversion; (2) eksportuje rekordy guidowane;
  (3) mapuje menu z DLL; (4) pyta o klucz `appsettings.json`; (5) pyta o rejestr konfiguracji,
  debuguje `*.reg.json` albo przenosi ustawienia między bazami; (6) eksportuje listę do
  Excela/CSV. Kod importu → /soneta:programming;
  operacje na bazie z CLI → /soneta:tools.
---

# Ustawienia, konfiguracja i funkcje domenowe platformy Soneta (enova365, Triva)

Skill gromadzi narzędzia i mechanizmy związane z **konfiguracją systemu** oraz
**funkcjami domenowymi** platformy Soneta. Zawartość jest rozwijana — poniżej udokumentowane
są wyłącznie mechanizmy faktycznie obecne w skillu; kolejne artykuły dodawane są sukcesywnie.

## Kiedy ten skill, a kiedy inny

| Potrzeba | Skill |
|---|---|
| **Budowa pliku XML importu danych i ustawień** (dbinit.xml, demo, tryby importu) | **/soneta:config** ([import-xml](references/import-xml.md)) |
| **Eksport rekordów do XML** (datapack, zakres eksportu, przenoszenie konfiguracji) | **/soneta:config** ([export-xml](references/export-xml.md)) |
| **Eksport listy do Excela/CSV** (ścieżki w nagłówku, pola kluczowe, kolejność kolumn, podlisty) | **/soneta:config** ([export-excel](references/export-excel.md)) |
| **Gotowy wzorzec pliku importu** dla typowego obiektu (słownik, definicja, rola, cecha, szablon, kokpit) | **/soneta:config** ([import-xml-examples](references/import-xml-examples.md)) |
| Inwentaryzacja/mapa **folderów statycznych menu** (`[assembly: FolderView]`) z DLL | **/soneta:config** ([scan-folders](references/scan-folders.md)) |
| **Konfiguracja uruchomieniowa** (porty, adresy komponentów, warstwy nadpisań `appsettings.json`) | **/soneta:config** ([appsettings](references/appsettings.md)) |
| **Rejestr konfiguracji** (ConfigReg): `*.reg.json`, porównanie i scalanie ustawień między bazami | **/soneta:config** ([config-reg](references/config-reg.md)) |
| Kod obsługujący import/eksport (`SessionReader`/`SessionWriter`), klasy ORM, workery | [/soneta:programming](../programming/SKILL.md) |
| Inwentaryzacja modułów/tabel (`scan-modules`), pól (`scan-props`), workerów (`scan-workers`) | [/soneta:programming](../programming/SKILL.md) |
| Operacje na bazie z CLI (dbmgr), test na żywej aplikacji (buscall) | [/soneta:tools](../tools/SKILL.md) |
| Definicja struktury tabel/kolumn/relacji w XML (business.xml) | [/soneta:business-xml](../business-xml/SKILL.md) |
| Formularze i widoki UI (form.xml) | [/soneta:form-xml](../form-xml/SKILL.md) |
| Planowanie całego dodatku/modułu | [/soneta:addon-planning](../addon-planning/SKILL.md) |

## Artykuły i narzędzia

### Import danych przez XML — [references/import-xml.md](references/import-xml.md)

Struktura i sposób budowania plików XML (`<session xmlns="http://www.soneta.pl/schema/business">`)
do wczytywania danych i ustawień konfiguracyjnych. Dwa tryby:

1. **Import według rekordów** (domyślny) — dane wprost do pól rekordów, bez logiki biznesowej;
   kolejność pól bez znaczenia; do danych konfiguracyjnych, plików `*.dbinit.xml` i bazy demo.
2. **Import przez logikę biznesową** (`business="true"`) — ustawianie właściwości biznesowych
   z pełną walidacją; kolejność elementów jak przy wpisywaniu danych na formularzu.

Artykuł specyfikuje identyfikację rekordów (GUID, `where`, `key`, `id`), formaty wartości
(liczby, daty, referencje, kwoty z walutą), atrybuty specjalne oraz reguły plików
`*.dbinit.xml` (`priority`, `versionName`, `dbversion`). Zawiera też gotowy, zweryfikowany
importem **przykład** (obiekt w modelu „root + historia") —
[examples/import-pracownik-etatowy.xml](examples/import-pracownik-etatowy.xml).

### Eksport listy do Excela i CSV — [references/export-excel.md](references/export-excel.md)

Eksport dowolnej listy do `.xlsx` albo CSV (TAB, UTF-16) na właściwościach biznesowych obiektów.
Nagłówek „ścieżka dostępu do danych" czyni plik szablonem importu. Artykuł opisuje opcje okna
eksportu, zasady budowy ścieżek (`A.B`, `Obiekt:Klucz`, `Obiekt:Class`, sufiksy wartości
złożonych, `//`), pola kluczowe identyfikujące obiekty przy imporcie (`Guid`, `ID`, klucze
unikalne), kolejność kolumn zgodną z formularzem, podlisty (`Kolekcja:Pole` i wiersze
kontynuacji) oraz checklistę eksportu pod ponowny import.

### Eksport danych do XML — [references/export-xml.md](references/export-xml.md)

Eksport według rekordów: wskazany rekord guidowany + rekordy powiązane (datapack; relacje
inner jako kolekcje w rekordzie, outer jako osobne elementy główne). Artykuł opisuje ustalanie
zakresu eksportu (`scan-forms`/`scan-props`), strukturę pliku wynikowego (`id`, `guid`,
`Obiekt:GUID`, `deleted`, `fromto`), wybór między eksportem XML a rejestrem konfiguracji oraz
checklistę eksportu do przeniesienia na inną bazę. Wynik jest gotowym plikiem importu według
rekordów.

### Katalog wzorców importu XML — [references/import-xml-examples.md](references/import-xml-examples.md)

Wzorce wyniesione z kilkuset standardowych plików `*.dbinit.xml` platformy: nagłówek i piętra
`priority`, strukturalne GUID-y, trzy formy referencji (`GUID`, `Tabela:GUID`, `id` z pliku),
patch rekordu w kolejnej wersji (`updateonly`), `insertonly`, `key`, `duplicate`/
`duplicateKeyField`, semantyka pustych elementów, kod C#/HTML/XML w treści pola. Szkielety dla
typowych obiektów: słowniki, definicje dokumentów i zadań, cechy, szablony e-mail, konfiguracja
(`CfgNode`), role systemowe i prawa (`Right`), projekty runtime, kokpity, tuple. Pliki
w [examples/](examples/): `dbinit-slownik-i-poprawki.dbinit.xml`, `import-rola-i-prawa.xml`,
`import-cecha-i-szablon-email.xml`, `import-definicja-elementu-wynagrodzenia.xml` (kreator
algorytmu i edytor z kodem C#; parametry algorytmu → [/soneta:place-def-elementow](../place-def-elementow/SKILL.md)).

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
perspektywy **danych** (`scan-modules`) i **pól** (`scan-props`) ze skilla
[/soneta:programming](../programming/SKILL.md). Planistyczne użycie inwentaryzacji
opisuje [/soneta:addon-planning](../addon-planning/SKILL.md).

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
[/soneta:tools](../tools/SKILL.md).

### Rejestr konfiguracji (ConfigReg) — [references/config-reg.md](references/config-reg.md)

Zrzut **całej konfiguracji bazy** do jednego drzewa zapisywanego jako `*.reg.json`, który da się
porównać z inną bazą, scalić i wgrać z powrotem (menu **Narzędzia → Zarządzanie konfiguracją**).
Artykuł opisuje pięć operacji formularza, strukturę drzewa (moduły → `Config` i tabele
konfiguracyjne, `Addins`, `Storage` z mapą folderów magazynu plików i sufiksami kontekstów),
pola wyłączone z rejestru (m.in. wrażliwe), format pliku wraz z sigilami (`$strict`, `$v`, `#klucz`, `@atrybut`,
`$blob`), składnię ścieżek węzłów, semantykę kolekcji `$strict` (**usuwa** wiersze nieobecne
w pliku) oraz diagnostykę najczęstszej pułapki — wiersza pokazywanego jednocześnie jako usunięty
i dodany. Zawiera dwie checklisty: przeniesienie ustawień między bazami i kroki przed scaleniem
do bazy operacyjnej.

**Nie myl dwóch mechanizmów przenoszenia ustawień.** XML (`<session>`, `*.dbinit.xml`) przenosi
*wskazane rekordy* i nie wykrywa różnic. Rejestr zdejmuje *stan konfiguracji jako całość*,
porównuje i scala. Formaty plików są rozłączne — kryterium wyboru i porównanie w tabeli na
początku artykułu.

## Powiązania

- [/soneta:programming](../programming/SKILL.md) — warstwa kodu importu/eksportu
  (*sessionreader-sessionwriter*), rekordy guidowane (*datapack-guidedrow*), `OnImporting`/
  `OnImported` (*row-types*), testy z `ImportBusinessXml` (*integration-tests*), skany DLL
  (`scan-modules`, `scan-props`, `scan-workers`) oraz pisanie folderów/list w C#.
- [/soneta:addon-planning](../addon-planning/SKILL.md) — użycie inwentaryzacji menu
  na etapie planowania dodatku.
- [/soneta:tools](../tools/SKILL.md) — operacje na bazie z CLI, weryfikacja efektów
  importu na żywej aplikacji (buscall).
- [/soneta:business-xml](../business-xml/SKILL.md) — definicja tabel i kolumn, w tym
  oznaczanie tabeli jako konfiguracyjnej, co decyduje o jej obecności w rejestrze konfiguracji.
- [/soneta:erp](../erp/SKILL.md) — mapa wyboru skilla.
