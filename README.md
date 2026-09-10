# Soneta AI Skills

Skille do programowania, projektowania i konfiguracji platformy Soneta (enova365, Triva)
w Claude Code, Codexie i innych asystentach AI.

Skille dokumentują publiczną bibliotekę platformy, dlatego są przeznaczone zarówno dla **partnerów** tworzących dodatki, jak i dla **zespołu Soneta** piszącego kod modułów standardowych w repozytorium źródłowym programu — te same wzorce i checklisty obowiązują w obu przypadkach.

## Dostępne skille

### 0. [erp](skills/erp/SKILL.md) (meta-skill)

Mapa pozostałych skilli platformy Soneta.

**Zakres:** wybór skilla według warstwy zadania i łączenie kilku skilli.

**Kiedy używać:** gdy nie wiesz, który skill wybrać do zadania dla Soneta, lub potrzebujesz kilku skilli.

### 1. [programming](skills/programming/SKILL.md)

Praca z klasami ORM platformy Soneta (enova365, Triva).

**Zakres:**

- Mapowanie obiektowo-relacyjne (`Row`, `Table`, `Module`)
- Zarządzanie sesją (`Session`) i transakcjami biznesowymi
- Logowanie i dostęp do bazy (`Login`, `Database`, `BusApplication`)
- Paczki danych (`Datapack`, `GuidedRow`) i synchronizacja
- Kontekst aplikacji (`Context`)

**Kiedy używać:** gdy potrzebujesz informacji o klasach logiki biznesowej, sesjach,
transakcjach lub hierarchii `Row` → `Table` → `Module`.

### 2. [business-xml](skills/business-xml/SKILL.md)

Generowanie plików `business.xml` definiujących strukturę obiektów biznesowych.

**Zakres:**

- Definiowanie tabel i kolumn
- Typy danych (proste, relacyjne, złożone)
- Relacje między obiektami (1:N, N:1, polimorficzne)
- Klucze i indeksy
- Wzorce: słowniki, dokumenty z pozycjami, historia zmian

**Kiedy używać:** gdy tworzysz nowy moduł biznesowy, definiujesz encje lub generujesz pliki `*.business.xml`.

### 3. [form-xml](skills/form-xml/SKILL.md)

Interfejs użytkownika platformy Soneta w plikach `form.xml`.

**Zakres:**

- Formularze stron (`pageform.xml`), widoki list (`viewform.xml`), lookupy (`lookupform.xml`), gridy (`gridform.xml`)
- Elementy: `DataForm`, `Page`, `Group`, `Grid`, `Field`, `Row`, `Stack`, `Flow`, `Command`
- Atrybuty: `EditValue`, `DataContext`, `Visibility`, `RowCondition`, `Renderable`, `CaptionHtml`
- Warunkowe formatowanie, wiązanie danych, wzorce UI

**Kiedy używać:** gdy tworzysz lub zmieniasz zakładki, formularze, widoki list i lookupy
albo analizujesz ich definicje XML.

### 4. [addon-planning](skills/addon-planning/SKILL.md)

Założenia projektu i specyfikacja funkcjonalna dodatku dla platformy Soneta.

**Zakres:**

- Planowanie w rozmowie z użytkownikiem w 3 etapach: wizja, architektura, specyfikacja szczegółowa
- Struktura danych (tabele, relacje)
- Elementy konfigurowalne, definicje list i menu
- Formularze, workery i raporty
- Dokumentacja implementacyjna z TODO

**Kiedy używać:** gdy planujesz nowy moduł lub dodatek i potrzebujesz uzgodnić jego
zakres, architekturę oraz szczegóły implementacji.

### 5. [ui-style](skills/ui-style/SKILL.md)

System projektowy (design system) platformy Soneta do budowania aplikacji webowych.

**Zakres:**

- Palety kolorów dla motywów jasnego i ciemnego; kolor główny: szmaragdowy `#016E46`
- Typografia (Roboto), układ strony, system odstępów (skala 4 px)
- Komponenty UI: przyciski, formularze, tabele, pasek boczny, nagłówek, kafelki, okna modalne
- Cienie, ikony SVG (~370 ikon liniowych), animacje, responsywność
- Minimalistyczny, płaski interfejs z subtelnymi cieniami

**Kiedy używać:** gdy projektujesz strony lub aplikacje w stylu Soneta, w tym dashboardy,
formularze, strony logowania i panele administracyjne.

### 6. [place-def-elementow](skills/place-def-elementow/SKILL.md)

Tworzenie i konfiguracja definicji elementów wynagrodzenia na platformie Soneta (moduł Płace).

**Zakres:**

- Algorytmy naliczania: kreator, edytor C# (`_Param`, `_Wylicz`, `_Wartość1h`), algorytmy wbudowane
- 12 wzorców dla Dodatków, 5 dla Nieobecności, 5 dla Dodatków automatycznych (z analizy ~247 definicji)
- Receptury kodu C#: iterowanie po elementach, staż pracy, wymiar etatu, czas pracy, wskaźniki, cechy pracownika
- Konfiguracja zakładek: Ogólne, Deklaracje (PIT/ZUS), Nieobecności, Algorytm
- Metody sterujące naliczaniem (`_PodstawaUrlopu`, `_PodstawaZasiłku`)

**Kiedy używać:** gdy tworzysz lub zmieniasz definicję elementu wynagrodzenia albo piszesz
algorytm płacowy, np. premii procentowej, dodatku stażowego, zasiłku chorobowego lub ekwiwalentu za urlop.

### 7. [tools](skills/tools/SKILL.md)

Narzędzia CLI platformy Soneta.

**Zakres:**

- `dbmgr`: tworzenie, rejestracja, usuwanie i konwersja baz; tworzenie i przywracanie kopii
  zapasowych (binarnych `.bac` i tekstowych `.zip`), licencje i klucz wirtualny, rozszerzenia (extensions), import XML,
  analiza i kompilacja algorytmów
- `buscall`: sterowanie uruchomioną aplikacją (nawigacja, formularze, gridy, edycja)
  i zrzuty ekranu do oceny wyglądu; wariant MCP `callmcp`
- Przygotowanie baz testowych i demonstracyjnych oraz automatyzacja operacji w skryptach i CI

**Kiedy używać:** gdy zarządzasz bazą enova365 z CLI, tworzysz bazę demo lub kopię zapasową,
konwertujesz bazę albo sprawdzasz zmiany w kodzie w uruchomionej aplikacji.

### 8. [config](skills/config/SKILL.md)

Konfiguracja działającego programu (ustawienia, cechy, prawa) i uruchamianie funkcji domenowych
(czynności, harmonogram).

**Zakres:**

- Import i eksport danych oraz ustawień przez pliki XML o strukturze `<session>`.
  Import według rekordów obejmuje dane konfiguracyjne, pliki `*.dbinit.xml` i bazę demo;
  import przez logikę biznesową (`business="true"`) wykonuje pełną walidację.
  Skill opisuje też eksport rekordów guidowanych z datapackiem, identyfikację rekordów
  (GUID, `where`, `key`, `id`), formaty wartości, atrybuty specjalne i przenoszenie ustawień między bazami.
- `scan-folders`: odczyt statycznych folderów menu (`[assembly: FolderView]`) z bibliotek DLL.
  Pokazuje drzewo menu (listy, formularze) oraz powiązania z tabelami i `ViewInfo`.
  Czyta metadane przez Roslyn, bez uruchamiania aplikacji. Uzupełnia skan danych
  `scan-modules` ze skilla [programming](skills/programming/SKILL.md) o strukturę menu widoczną dla użytkownika.

**Kiedy używać:** gdy tworzysz lub analizujesz plik XML do importu danych, przenosisz
konfigurację między bazami lub eksportujesz dane do XML. Także gdy odwzorowujesz strukturę
menu dodatku lub szukasz ścieżki folderu nadrzędnego dla nowego folderu.

### 9. [containers](skills/containers/SKILL.md)

Uruchamianie i wdrażanie platformy Soneta (enova365, Triva) w kontenerach bez odwołań do kodu programu.

**Zakres:**

- Docker Compose (wariant domyślny): gotowe pliki `docker-compose.yaml` (dbinit + server + web), cykl życia, zmienne `SONETA_...`
- Apple `container` / Container Desktop (macOS): różnice, wklejanie YAML, grupy, `x-init`
- Helm / Kubernetes (beta): `helm repo add soneta`, `values.yaml`, `dblist`, `adminMode`
- Wersje obrazów `soneta/server.standard`, `web.standard`: Docker Hub (publiczne) i `registry.soneta.pl` (alfa)
- Baza w kontenerze: usługa init z `dbmgr create` (`--demo`, `--recreate`), SQL zewnętrzny lub kontener `mssql`

**Kiedy używać:** gdy uruchamiasz środowisko testowe lub demonstracyjne na obrazach Soneta
przez `docker compose up`, Container Desktop lub `helm install`, wybierasz tag lub wersję obrazu
albo rozwiązujesz problemy z uruchomieniem usług (kolejność, host-alias, porty).

### 10. [repx](skills/repx/SKILL.md)

Wydruki DevExpress XtraReports (`.repx`) dla platformy Soneta.

**Zakres:**

- Układ raportu
- Źródła danych, wiązania i podraporty
- Kod `ReportSnippet` i rejestracja wydruku

**Kiedy używać:** gdy tworzysz, modyfikujesz lub analizujesz wydruk `.repx` albo szukasz
sposobu uzyskania określonego wyniku w raporcie.

## Powiązania między skillami

- Model obiektów w XML opisuje [business-xml](skills/business-xml/SKILL.md), pracę
  z wygenerowanymi klasami C# [programming](skills/programming/SKILL.md), a formularze
  tych obiektów [form-xml](skills/form-xml/SKILL.md).
- Przy pracy z danymi wybierz [config](skills/config/SKILL.md) do konfiguracji oraz importu
  i eksportu XML, [programming](skills/programming/SKILL.md) do kodu importu i eksportu
  (`SessionReader`/`SessionWriter`) oraz ORM, a [tools](skills/tools/SKILL.md) do operacji na bazie z CLI.
- W środowisku opisanym przez [containers](skills/containers/SKILL.md) bazę tworzy `dbmgr`
  w kontenerze. Składnię jego poleceń opisuje [tools](skills/tools/SKILL.md).

## Struktura repozytorium

```
AGENTS.md                # odnośnik do wspólnych zasad
CLAUDE.md                # wspólne zasady dla autorów skilli
.codex-plugin/
└── plugin.json          # manifest pluginu Codexa (name: soneta)
.agents/plugins/
└── marketplace.json     # marketplace Codexa (name: soneta-erp-skills)
.claude-plugin/
├── plugin.json          # manifest pluginu (name: soneta)
└── marketplace.json     # manifest marketplace'u (name: soneta-erp-skills)
Prompts/                 # prompty do tworzenia dokumentacji domenowej
Soneta.Skills.Test/      # testy przykładów kodu
skills/
├── erp/  programming/  business-xml/  form-xml/  addon-planning/
├── ui-style/  place-def-elementow/  tools/  config/  containers/  repx/
```

Każdy skill zawiera plik `SKILL.md` z polami `name` i `description` w sekcji frontmatter.
Może też zawierać katalogi `references/`, `scripts/`, `assets/`, `examples/` i `data/`.

## Instalacja

Skille w `skills/` są zapisane w [formacie Agent Skills](https://agentskills.io/specification).
Claude Code i Codex korzystają z tego samego zestawu skilli, udostępnianego jako plugin `soneta`.
Możesz też zainstalować skille osobno lub udostępnić je innemu asystentowi.

Nazwy skilli, np. `erp`, `programming` i `form-xml`, pochodzą z pola `name` w `SKILL.md`.
Odpowiadają im nazwy katalogów. Sposób wywołania zależy od asystenta.

### Claude Code (plugin)

W sesji Claude Code dodaj marketplace i zainstaluj plugin:

```text
/plugin marketplace add soneta/soneta-erp-skills
/plugin install soneta@soneta-erp-skills
```

Wywołuj skille przez `/soneta:erp`, `/soneta:programming`, `/soneta:form-xml` itd.
Prefiks `soneta:` pochodzi od nazwy pluginu. Aktualizację uruchom przez `/plugin update soneta`.

Po zainstalowaniu pluginu polecenie `claude plugin details soneta` w terminalu pokazuje
jego zawartość i szacowany koszt tokenów. Plugin musi być dostępny dla tego wywołania CLI;
udostępnienie go przez `--plugin-dir` w osobnej sesji nie wystarcza.

Do pracy na lokalnym klonie uruchom w jego katalogu:

```sh
claude --plugin-dir .
```

Aby użyć lokalnego klonu w swoim projekcie, uruchom w katalogu projektu:

```sh
claude --plugin-dir "<pełna-ścieżka-do-klonu>"
```

Wstaw rzeczywistą ścieżkę do klonu. Opcja `--plugin-dir` udostępnia plugin na czas tej sesji.

Ładowanie lokalnego pluginu i prefiksowanie nazw opisuje
[dokumentacja Claude Code](https://code.claude.com/docs/en/plugins).

Możesz też pracować nad pluginem, tworząc dowiązanie do klonu pod ścieżką `~/.claude/skills/soneta`;
katalog zawierający `.claude-plugin/plugin.json` jest ładowany jako plugin `soneta@skills-dir`.

### Codex (plugin)

W terminalu dodaj marketplace i zainstaluj plugin:

```sh
codex plugin marketplace add soneta/soneta-erp-skills
codex plugin add soneta@soneta-erp-skills
```

Instalacja z GitHuba wymaga opublikowania manifestów Codexa w repozytorium.
Aby zainstalować plugin z lokalnego klonu, użyj tych poleceń:

```sh
codex plugin marketplace add "<pełna-ścieżka-do-klonu>"
codex plugin add soneta@soneta-erp-skills
```

Wstaw rzeczywistą ścieżkę do klonu. Po instalacji rozpocznij nową sesję Codexa.

Plik `.codex-plugin/plugin.json` opisuje plugin, a `.agents/plugins/marketplace.json`
udostępnia go w marketplace `soneta-erp-skills`. Format tych plików opisuje
[dokumentacja pluginów OpenAI](https://learn.chatgpt.com/docs/build-plugins).

### Instalacja skilli bez pluginu

Jeśli asystent obsługuje Agent Skills, skopiuj całe katalogi skilli z `skills/` wraz ze
wszystkimi zasobami do lokalizacji wskazanej w jego dokumentacji:

| Asystent | Jeden projekt | Wszystkie projekty użytkownika |
|---|---|---|
| Obsługujący `.agents/skills/` | `<projekt>/.agents/skills/` | `~/.agents/skills/` (Windows: `$HOME/.agents/skills/`) |
| Claude Code | `<projekt>/.claude/skills/` | `~/.claude/skills/` (Windows: `$HOME/.claude/skills/`) |

Lokalizacje dla Claude Code opisuje [dokumentacja skilli](https://code.claude.com/docs/en/skills#where-skills-live).

Sprawdź, czy asystent obsługuje wybrany zakres instalacji. Obsługa katalogu w projekcie
nie musi oznaczać obsługi globalnego katalogu użytkownika.

Przykładowy wynik instalacji w projekcie:

```text
<projekt>/.agents/skills/
├── erp/SKILL.md
├── programming/
│   ├── SKILL.md
│   ├── references/
│   ├── scripts/
│   └── data/
├── form-xml/
└── ...pozostałe katalogi skilli
```

Zachowaj nazwy katalogów i ich wzajemne położenie, aby działały linki między skillami.
Jeśli nazwa jest już zajęta przez inny zestaw, zainstaluj skille w osobnym projekcie.
Podczas pracy nad skillami możesz zastąpić kopie dowiązaniami do katalogów w klonie,
jeśli asystent je obsługuje.

Sposób wywołania zależy od asystenta. W Codexie wpisz np. `$erp` albo `$programming`.
Jeśli skill nie pojawia się na liście, uruchom nową sesję Codexa. Lokalizacje i obsługę dowiązań opisuje
[dokumentacja skilli Codexa](https://learn.chatgpt.com/docs/build-skills).

Jeśli asystent nie obsługuje skilli, udostępnij mu klon repozytorium i dodaj do instrukcji projektu:

```text
Przy zadaniach dla platformy Soneta (enova365, Triva) przeczytaj
<ścieżka-do-repo>/skills/erp/SKILL.md i otwieraj wskazane tam pliki
odpowiednio do zadania.
```

Wstaw rzeczywistą ścieżkę do repozytorium i zapewnij agentowi dostęp do odczytu plików.

### Wymagania do wykonywania zadań

Niezależnie od sposobu instalacji przygotuj narzędzia wymagane przez wybrany skill.
Uruchamianie skryptów wymaga terminala. .NET, narzędzia Soneta i połączenie MCP trzeba
zainstalować lub skonfigurować osobno, jeśli zadanie ich wymaga. Bez połączenia z aplikacją
można przygotować kod i konfigurację, ale ich działanie trzeba sprawdzić w uruchomionej aplikacji.

## Rozwój i weryfikacja

Zasady dla autorów znajdziesz w [CLAUDE.md](CLAUDE.md). Plik [AGENTS.md](AGENTS.md) odsyła do tych samych zasad.
Jeśli używany asystent nie wczytuje tych instrukcji automatycznie, wskaż `CLAUDE.md`
w kontekście zadania.

Przed commitem wykonaj [checklistę weryfikacji](CLAUDE.md#weryfikacja).

Działanie na platformie Soneta wymaga osobnego sprawdzenia.

Jeśli masz Claude Code, możesz też sprawdzić strukturę pluginu:

```sh
claude plugin validate . --strict
claude plugin validate ./skills --strict
claude plugin validate .claude-plugin/plugin.json
```

Ostatnie polecenie celowo uruchom bez `--strict`: walidator zgłasza ostrzeżenie o `CLAUDE.md`
w korzeniu pluginu („not loaded as project context”). To oczekiwane ostrzeżenie: plik zawiera zasady
pisania skilli dla autorów tego repozytorium. Claude Code ładuje go jako kontekst projektu
podczas pracy w klonie repozytorium; instalacja pluginu nie wczytuje tych instrukcji.

## Licencja

MIT
