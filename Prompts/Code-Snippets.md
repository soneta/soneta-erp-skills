# Skill programowania Soneta — generowanie dokumentu dokumentacji domeny

## Parametry działania

> **Wszystkie parametry uzupełnij PRZED rozpoczęciem pracy.** Reszta promptu odwołuje się tylko do
> nich — nie wpisuj nazw domeny/plików na sztywno w opisie zadania.

| Parametr | Wartość (uzupełnij)                                                                                                                                      | Opis                                                                                                       |
|---|----------------------------------------------------------------------------------------------------------------------------------------------------------|------------------------------------------------------------------------------------------------------------|
| **Domena** | Workflow                                                                                                                                                 | Nazwa domeny biznesowej dokumentowanej w tym przebiegu.                                                    |
| **Główny obiekt biznesowy** | `Soneta.Business.Db.Task` (tabela `Tasks`, caption „Zadanie"), `Soneta.Workflow.WFWorkflow` (tabela `WFWorkflows`, caption „Proces")                     | Klasa `Row`/`GuidedRow` będąca rdzeniem dokumentacji.                                                      |
| **Prefiks kodów receptur** | `WORKFLOW`                                                                                                                                               | Prefiks kodu wzorca, np. `WORKFLOW-A1`, `WORKFLOW-B3` (litera = sekcja listy zadań, numer = receptura).    |
| **Lista funkcjonalności** | `lista-zadan-workflow.md`                                                                                                                                | Plik wejściowy z listą zadań (efekt promptu „Snippets List").                                              |
| **Zakres dokumentacji** | workflow, definicje procesów, węzły, tranzycje, procesy (instancje), zadania operatora.                                                                  | Granice tematyczne — co wchodzi, a co nie.                                                                 |
| **Folder rozdziałów** | `/soneta-programming/references/domeny/workflow/`                                                                                                        | Tu trafiają numerowane pliki rozdziałów `<PREFIKS>NN-nazwa.md`.                                            |
| **Plik indeksu** | `/soneta-programming/references/domeny/workflow.md`                                                                                                      | Strona tytułowa domeny: fakty o typie, typy domenowe, szablon wzorca, mapa receptur.                       |
| **Folder testów skilla** | `@Soneta.Skills.Test/Workflow`                                                                                                                           | Tu trafiają testy (po jednej klasie na rozdział).                                                          |
| **Klasa bazowa testów** | `WorkflowTestBase : TestBase`                                                                                                                            | Wspólna baza testów obiektu; jeśli nie istnieje — utwórz analogicznie do innych obiektów.                  |
| **Główne źródła kodu** | Soneta.Workflow/`, `Soneta.Workflow.UI/`, `Soneta.Workflow.Test/`, `Soneta.Zadania/`, `Soneta.Zadania.UI/`, `Soneta.Zadania.Test/`, `Soneta.Business/Db` | Miejsca pierwszego wyboru przy analizie kodu programu.                                                     |
| **Wersja języka** | C# 10                                                                                                                                                    | Cały kod (dokumentacja i testy) — target-typed `new`, `var`, wyrażenia `switch`, nazwane parametry `bool`. |

## Zadanie do zrobienia

Na podstawie **listy funkcjonalności** (parametr) przygotuj dokument dokumentacji Markdown będący
elementem skilla `/soneta-programming`, pomagający agentom kodować obiekty obsługujące **zakres
dokumentacji** (parametr). Dokument ma **trafiać w realne pola, kolekcje i workery platformy**, tak aby
na jego podstawie programista zewnętrzny pisał bezbłędny kod biznesowy bez dostępu do źródeł aplikacji.

Akcje i czynności widoczne w programie są zaimplementowane przez **Workery**. Kod implementujący daną
funkcję znajdziesz, wyszukując Workera po jego tytule w kodzie programu (pomocniczo:
`/soneta-programming/scripts/scan-workers.csx`). Po analizie kodu workera opisz algorytm realizujący
funkcję — z perspektywy publicznego kontraktu.

## Forma dokumentacji (struktura wyjściowa)

Dokumentacja **nie jest pojedynczym plikiem** — to **plik indeksu + zestaw numerowanych rozdziałów** w
folderze domeny. Wzoruj się ściśle na istniejących dokumentach: `domeny/kadry.md` + `domeny/kadry/KADRY*.md`
oraz `domeny/handel.md` + `domeny/handel/HANDEL*.md`. Zachowaj ten sam układ, nagłówki i konwencje.

### Plik indeksu (`<plik indeksu>`)

Strona tytułowa domeny — **bez receptur**, same fundamenty i mapa. Zawiera w kolejności:

1. **Nagłówek + akapit wprowadzający** — jaka domena, jaki główny obiekt biznesowy, że dokument jest
   częścią skilla `soneta-programming`, oraz cel (bezbłędny kod biznesowy).
2. **Notka „Format zwarty"** (blockquote) — każdy wzorzec = ogólny przypadek + tabela wariantów;
   fundamenty (sesja, transakcja, blokada optymistyczna, `SubTable`, obsługa błędów, wywoływanie
   workerów) **nie są powtarzane** — odsyłaj do [`safe-code.md`](../safe-code.md),
   [`session-login.md`](../session-login.md), [`worker-extender.md`](../worker-extender.md).
3. **Notka o C# 10 i publicznym kontrakcie** (blockquote) — cały kod w C# 10; snippety wyłącznie na
   publicznym kontrakcie, bez odwołań do prywatnych klas i kodu źródłowego aplikacji.
4. **`## Fakty o typie (zweryfikowane skanem DLL)`** — klasa biznesowa, moduły i dostęp z sesji
   (`session.GetXxx()`), kluczowe pola bazodanowe (root), kluczowe kolekcje (`SubTable` z typami),
   cechy (`Features`), oraz stabilne punkty wejścia w bazie Demo (kody/ilości). **Fakty MUSZĄ być
   zweryfikowane** skanem DLL (`scripts/scan-props.csx`, `scan-workers.csx`, `scan-modules.csx`) — nie
   zgaduj nazw pól i typów.
5. **`## Podstawowe typy domenowe`** — tabela: typ | namespace | zastosowanie.
6. **`## Szablon wzorca`** — opis stałej struktury każdej receptury (poniżej) + konwencja kodu `<PREFIKS>-Xn`
   i znacznika ★.
7. **Notka o konwencji testów** (blockquote) — każdy wzorzec ma odpowiadający test w folderze testów,
   na klasie dziedziczącej z `TestBase`, baza Demo z automatycznym rollbackiem, testy jako wykonywalna
   dokumentacja publicznego API.
8. **`## Mapa receptur`** — tabela: rozdział | plik (link względny do `<folder rozdziałów>/...`) | zakres kodów receptur.

### Pliki rozdziałów (`<folder rozdziałów>/<PREFIKS>NN-nazwa.md`)

- Numeracja dwucyfrowa wg kolejności listy zadań: `WORKFLOW01-pracownik.md`, `WORKFLOW02-etat.md`, …
- Każdy rozdział zaczyna się nagłówkiem `# <PREFIKS>NN — <tytuł>` i blockquote odsyłającym do indeksu:
  `> Wspólne fakty o typie, podstawowe typy i szablon wzorca: [../<plik indeksu>](../<plik indeksu>).`
- Rozdział grupuje receptury jednej sekcji listy zadań (np. wszystkie `WORKFLOW-B*` w `WORKFLOW02-cos_tam.md`).

### Szablon pojedynczego wzorca (receptury)

Każda receptura ma **stałą strukturę** i kod `<PREFIKS>-Xn` (litera sekcji + numer; ★ = ma własny,
dedykowany test):

- **Cel** — co robi i kiedy użyć.
- **Warianty** — tabela odmian przypadku (gdy dotyczy).
- **Pola i typy** — realne właściwości/kolekcje i ich typy (z namespace).
- **Snippet** — kod C# 10 na publicznym kontrakcie.
- **Pułapki** — typowe błędy, kolejność operacji, zasady safe-code (z odwołaniami do `safe-code.md §…`).

## Sposób działania

Dokument ma pokryć **wszystkie** funkcjonalności z listy oraz informacje dodatkowe w ramach zakresu.
Do każdej funkcjonalności aktywuj dwóch subagentów: **dokumentujący** i **testujący**.

### Subagent dokumentujący

Ma dostęp do wszystkiego: kodu źródłowego Soneta i wszystkich skillów. Przygotowuje opis receptury (wg
szablonu wzorca powyżej) z ewentualnymi snippetami — instrukcję, jak daną funkcjonalność realizuje się
biblioteką Soneta.

Posługuj się skillem `/soneta-programming`. Wykorzystaj dane z kodu programu; ważniejsze miejsca
(parametr „Główne źródła kodu"):

* `Soneta.Workflow/`
* `Soneta.Workflow.UI/`
* `Soneta.Workflow.Test/`
* `Soneta.Zadania/`
* `Soneta.Zadania.UI/`
* `Soneta.Zadania.Test/`
* `Soneta.Business/Db`

Możesz też szukać w pozostałych częściach programu. **Fakty o typie weryfikuj skanem DLL**
(`/soneta-programming/scripts/scan-props.csx`, `scan-workers.csx`, `scan-modules.csx`) — nazwy pól,
typów, modułów i workerów muszą być prawdziwe.

Ponieważ dokument jest elementem skilla, stosuj też zasady `/skill-creator`, żeby dokumentacja była
lepszej jakości.

Generowany skill jest **dla programistów zewnętrznych**, którzy nie mają dostępu do naszego kodu —
posługują się tylko publicznymi klasami i metodami. Dokument musi być na tyle szczegółowy, żeby na jego
podstawie generować kod dodatków bez znajomości kodu aplikacji. **Znany jest tylko kontrakt publiczny.**

Kod w dokumentacji respektuje zasady `/soneta-programming`. Nasz kod jest wiekowy i miejscami pisany w
starych wersjach C# — w dokumentacji używaj wyłącznie konstrukcji **C# 10**.

W `/soneta-programming/references/domeny` znajdują się już podobne dokumenty (`kadry.md`, `handel.md`,
`crm.md` z rozdziałami) — używaj ich jako wzorca formy i jakości.

### Subagent testujący

Ma dostęp **tylko** do tworzonego skilla `/soneta-programming` oraz, w razie potrzeby, do skillów
powiązanych. **Nie ma dostępu** do kodu źródłowego Soneta — może jedynie odczytywać nagłówki publicznych
klas, metod, properties. Jego wiedza ogranicza się do skillów. Zadanie: napisać test implementujący
wskazaną funkcjonalność (weryfikuje, czy dokumentacja wystarcza do napisania działającego kodu).

Testy umieszczaj w **folderze testów skilla** (parametr). Konwencja nazewnictwa jak w istniejących:
**jedna klasa testowa na rozdział** (`Rozdzial<Litera>_<Nazwa>Test.cs` / `RozdzialNN_<Nazwa>Test.cs`),
dziedzicząca z **klasy bazowej testów** (parametr, np. `PracownikTestBase : TestBase`).

Nie martw się o śmieci w testach — możesz modyfikować obiekty i bazę. Testy na `TestBase` automatycznie
wycofują wszystkie zmiany w bazie (rollback). Obszary rozrachunków, dokumentów i handlu też możesz
testować. **Nie testuj** funkcjonalności wymagających sieci. Asercje: biblioteka `AwesomeAssertions`.

Testy są również dokumentacją kodu dla programistów — w każdym teście umieść komentarze tłumaczące, co i
jak testuje. Każdy test ma atrybut `[Description("...")]` opisujący działanie. Dane słownikowe i
referencje (kody, definicje) pobieraj **dynamicznie z bazy Demo** (iteracja / pierwszy wpis), nie zakładaj
konkretnych kodów na sztywno.

### Praca subagentów

Zacznij od analizy **struktury testów** — ma być zgodna z `@Soneta.Skills.Test`. Użyj tych samych
technik i narzędzi co w pozostałych metodach testowych (przynajmniej klasa dziedzicząca z `TestBase`).

Następnie **przygotuj szablon pliku indeksu** (sekcje 1–8 z „Forma dokumentacji") oraz pusty szkielet
rozdziałów wynikający z listy zadań i mapy receptur. Przeanalizuj `domeny/kadry.md`, `domeny/handel.md`
i ich rozdziały, by odtworzyć dokładnie tę strukturę.

Dla każdej funkcjonalności: subagent dokumentujący przygotowuje recepturę (wg szablonu wzorca).
Subagent testujący wczytuje skill i pisze test realizujący przykładowy scenariusz. Jeśli się nie uda,
przekazuje raport problemów subagentowi dokumentującemu, który uzupełnia recepturę; testujący próbuje
ponownie od początku. Pętlę powtarzaj aż test przejdzie albo funkcjonalność zostanie zgłoszona jako
niedokumentowalna (patrz weryfikacja końcowa).

Po wykonaniu wszystkich kroków zbuduj **mapę receptur** (indeks) oraz spis treści.

## Weryfikacja końcowa

* Zweryfikuj, że każda funkcjonalność z listy ma swój opis (recepturę) w odpowiednim rozdziale.
* Zweryfikuj, że dla każdej funkcjonalności (przynajmniej ★) powstał test weryfikujący dokumentację.
* Zweryfikuj, że plik indeksu zawiera komplet sekcji (fakty o typie, typy domenowe, szablon wzorca,
  konwencja testów, mapa receptur) i poprawne linki do rozdziałów.
* Zweryfikuj, że każdy rozdział odsyła do indeksu i używa stałego szablonu wzorca (Cel / Warianty /
  Pola i typy / Snippet / Pułapki) oraz kodów `<PREFIKS>-Xn`.
* Zweryfikuj, że fakty o typie zostały potwierdzone skanem DLL (realne pola, typy, moduły, workery).
* Zweryfikuj, że testy są odpowiednio zdokumentowane (`[Description]` + komentarze).
* Zweryfikuj, że kod w dokumentacji jest spójny z kodem testowym.
* Pamiętaj o C# 10 — używaj nowych konstrukcji w dokumentacji i w testach.
* Zweryfikuj zgodność ze standardami `/soneta-programming`.
* Zweryfikuj brak odnośników do kodu źródłowego programu w dokumentacji (tylko publiczny kontrakt).
* W testach używaj tylko publicznych klas, metod i właściwości.
* Sprawdź `/skill-creator` na utworzonym dokumencie.
* Raportuj funkcjonalności, których nie umiesz poprawnie udokumentować i przetestować — dopytaj o ich
  ewentualne usunięcie.