
# Proces planowania projektu modułu/dodatku Soneta

## Część I: Zasady procesu planowania

### Przeznaczenie dokumentu planu projektu
Planowanie projektów modułów funkcjonalności dla platformy Soneta. Firma Soneta tworzy dwa programy: enova365 oraz Triva — oba oparte na tej samej platformie technologicznej.
Przygotowuje opis funkcjonalności nowego modułu z punktu widzenia używania go do stworzenia kolejnych planów implementacji poszczególnych elementów programu Soneta.
Plan będzie wykorzystywany w kolejnych etapach do budowania:
* struktury bazy danych,
* listy dostępnych list i widoków,
* innych ważnych/kluczowych elementów interfejsu,
* formularzy,
* tworzenia raportów i wydruków,
* praw dostępu,
* ról operatorów,
* zaplanowania testów,
* budowy dokumentacji
* i innych tego typu elementów projektu.

### Poziom szczegółowości
Plan projektu zawiera ogólne opisy elementów projektu. Nie zawiera szczegółowych informacji, które będą uzupełniane w kolejnych etapach. Na tym etapie chodzi o określenie ogólnych założeń, zakresu funkcjonalności i kluczowych elementów modułu.

### Etapowość procesu
Proces planowania składa się z trzech etapów o rosnącym poziomie szczegółowości:
- **ETAP 1** — Wizja i kontekst biznesowy (dla decydentów, marketingu, sprzedaży)
- **ETAP 2** — Architektura modułu (dla zespołu projektowego)
- **ETAP 3** — Specyfikacja szczegółowa (dla zespołu implementacyjnego i AI)

Każdy etap powinien być zatwierdzony przed przejściem do kolejnego.

---

## Część II: Struktura dokumentu planu projektu

## ETAP 1: Wizja i kontekst biznesowy

### 1.1. Cel biznesowy projektu
Krótka informacja — cztery, pięć zdań o tym co jest celem projektu. Jaki jest jego kluczowy element. Najważniejsza rzecz, którą chcemy osiągnąć.
Plan powinien rozpoczynać się podstawową ideą, którą chcemy osiągnąć w danym module — np. przygotowanie danych, które dopiero później będą wykorzystywane do analizy za pomocą innych narzędzi.

### 1.2. Profil klienta docelowego
Ogólne określenie użytkownika końcowego, który będzie używał tego rozwiązania. Firma mała, średnia, duża? Branża? Działalność?

### 1.3. Korzyści dla klienta
Zdefiniowanie najważniejszych korzyści dla klienta, określenie problemów rozwiązywanych przez moduł. Informacja ma być wykorzystywana w działach marketingu i sprzedaży, a także jako wprowadzenie do spotkań w temacie modułu.

### 1.4. Najważniejsze funkcjonalności (priorytetyzacja)
Krótka lista najważniejszych funkcjonalności modułu z podziałem na priorytety:
- **Krytyczne (must-have)** — funkcjonalności bez których moduł nie ma sensu, sedno modułu. Wskazać które elementy interfejsu i funkcjonalności są kluczowe dla sukcesu produktu.
- **Ważne (should-have)** — istotne funkcjonalności, ale moduł może działać bez nich w pierwszej wersji.
- **Opcjonalne (nice-to-have)** — funkcjonalności dodatkowe, które mogą być zrealizowane w kolejnych wersjach.

Te funkcjonalności będą szczegółowo opisywane w kolejnych etapach — w Etapie 2 na poziomie architektury, a w Etapie 3 jako specyfikacja szczegółowa (sekcje 3.1–3.12).

### 1.5. Scenariusze użytkownika
Opis kilku kluczowych scenariuszy użytkownika, które pokazują jak użytkownik będzie korzystał z modułu. Scenariusze powinny być opisane w formie krok po kroku, pokazując jakie działania użytkownik będzie podejmował, ogólnie jakie dane będzie wprowadzał, jakie wyniki będzie otrzymywał. Scenariusze powinny ograniczyć się tylko do najważniejszych, kluczowych danych. Szczegóły będą uzupełniane w kolejnych etapach.

### 1.6. Procesy biznesowe
Procesy biznesowe, które będą realizowane w całości przez moduł, ale także inne procesy biznesowe, w których części realizacji będzie wykorzystana funkcjonalność modułu, włącznie z pozostałymi elementami programu.
Zdefiniowane procesy będą wykorzystane później do stworzenia procesów workflow, a także do przypisania ich do poszczególnych operatorów i ról.

### 1.7. Ograniczenia modułu i wymagania licencyjne
Określenie ograniczeń modułu, które mogą dotyczyć:
- zakresu funkcjonalności,
- wymagań technicznych,
- integracji z innymi systemami,
- ograniczeń wynikających z przepisów prawa.

Określenie wymagań licencyjnych — jakie licencje są wymagane do działania modułu, zarówno licencje Soneta, jak i ewentualne licencje zewnętrzne.

### 1.8. Założenia i zależności
Określenie warunków, które muszą być spełnione, aby projekt mógł być zrealizowany:
- **Założenia** — co zakładamy jako pewne (np. dostępność określonych modułów programu Soneta, wersja platformy, dostęp do API zewnętrznych systemów).
- **Zależności** — od czego projekt jest uzależniony (np. zakończenie innego projektu, dostarczenie danych przez klienta, dostępność zespołu).

### 1.9. Ryzyka projektu
Identyfikacja kluczowych ryzyk z określeniem prawdopodobieństwa i wpływu:
- **Ryzyka techniczne** — np. ograniczenia platformy, wydajność przy dużych wolumenach danych.
- **Ryzyka biznesowe** — np. zmiana wymagań prawnych, brak zainteresowania rynku.
- **Ryzyka integracyjne** — np. niekompatybilność z innymi modułami, zmiany w API zewnętrznych systemów.

Dla każdego ryzyka wskazać plan mitygacji lub działania zapobiegawcze.

### 1.10. Kryteria akceptacji
Określenie mierzalnych kryteriów, po spełnieniu których moduł zostanie uznany za gotowy do wdrożenia:
- Kryteria funkcjonalne — jakie scenariusze muszą działać poprawnie.
- Kryteria wydajnościowe — orientacyjne wolumeny danych (ile rekordów, jak często przetwarzane), akceptowalne czasy odpowiedzi.
- Kryteria jakościowe — pokrycie testami, brak błędów krytycznych.

### 1.11. Harmonogram i kamienie milowe
Określenie ramowego harmonogramu realizacji projektu z uwzględnieniem kluczowych kamieni milowych:
- **Fazy projektu** — podział realizacji na fazy (np. planowanie, implementacja MVP, testy, wdrożenie pilotażowe, produkcja) z orientacyjnymi ramami czasowymi.
- **Kamienie milowe** — kluczowe punkty kontrolne projektu, np.:
  - Zatwierdzenie specyfikacji (koniec Etapu 3).
  - Gotowość MVP (funkcjonalności krytyczne).
  - Zakończenie testów integracyjnych.
  - Wdrożenie pilotażowe u pierwszego klienta.
  - Wydanie wersji produkcyjnej.
- **Zależności czasowe** — powiązanie kamieni milowych z zależnościami zdefiniowanymi w sekcji 1.8, uwzględniając czynniki mogące wpłynąć na harmonogram.

Harmonogram na tym etapie ma charakter orientacyjny i służy do ogólnego planowania zasobów oraz koordynacji z innymi projektami. Szczegółowy plan realizacji powstaje w fazie implementacji.


## ETAP 2: Architektura modułu

### 2.1. Role użytkowników
Lista operatorów, którzy będą korzystać z modułu. Jakie zadania będą realizować. W jakich procesach będą uczestniczyć.

### 2.2. Zakres konfiguracji modułu
Opisy elementów systemu, które będą konfigurowane. Ogólny opis zakresu konfiguracji, pozwalający na dostosowanie modułu do różnych potrzeb różnych klientów. Określenie ewentualnych definicji dokumentów, słowników, konfigurowanych ustawień, opcji programu dostępnych tylko dla pewnych grup klientów.
Uwzględnić ustawienia definiowane w procesie konfigurowania programu, a także różnego rodzaju definicje wykorzystywane jako podpowiedzi przy przetwarzaniu danych operacyjnych.

### 2.3. Kluczowe struktury danych
Określenie najważniejszych struktur danych, które będą podstawą modułu. Podstawowe listy dokumentów, kartoteki. Bez opisu szczegółowej zawartości, która będzie określona w kolejnym etapie.
Wskazanie ogólnego diagramu relacji między głównymi obiektami modułu.

Przy projektowaniu struktur danych należy odwoływać się do aktualnego modelu danych programu Soneta opisanego w pliku `tables.md`. Plik zawiera pełną listę modułów, tabel, obiektów i relacji platformy. Pozwala to na:
- sprawdzenie, czy potrzebne struktury danych już istnieją w programie (unikanie duplikacji),
- identyfikację tabel nadrzędnych, do których nowe obiekty mogą się odwoływać,
- rozpoznanie wzorców projektowych stosowanych w istniejących modułach (np. podział na tabele konfiguracyjne i operacyjne, stosowanie definicji dokumentów, definiowanie obiektów głównych - Guided oraz szczegółownych będących w realcji do nich, budowania datapack-ów).

### 2.4. Struktura menu i elementy interfejsu
Określenie folderów i elementów interfejsu dostępnych z punktu widzenia modułu. Wskazanie hierarchii list w menu głównym i grupowania funkcjonalnego.
Wyróżnienie elementów interfejsu, które są szczególnie ważne dla sukcesu produktu — np. w module kontrolingu kluczowym elementem może być miejsce do budowania zapytań i warunków (na podobieństwo arkusza Excel, na zasadzie AND/OR).

### 2.5. Relacje z modułami programu Soneta
Określenie zmian w aktualnych strukturach programu Soneta, które są potrzebne do realizacji tego modułu.
Określenie tabel i danych z programu Soneta, które będą wykorzystywane przez moduł — z krótkim opisem celu użycia tych danych.

Plik `tables.md` zawiera kompletny opis aktualnych modułów i tabel programu Soneta. Na jego podstawie należy:
- wskazać konkretne moduły programu Soneta, z którymi projektowany moduł będzie współpracować (np. Handel, Kadry, Ksiega, CRM),
- wymienić konkretne tabele i obiekty z tych modułów, do których nowy moduł będzie się odwoływać (relacje lookup/inner),
- określić, czy realizacja modułu wymaga rozszerzenia istniejących tabel programu Soneta (np. dodania nowych kolumn lub relacji do już istniejących obiektów).

### 2.6. Relacje z innymi systemami
Określenie relacji z innymi systemami, które będą wykorzystywane przez moduł, ale także innych systemów, które będą integrowane z modułem. Określenie jakie dane będą wymieniane między modułem a innymi systemami, jakie procesy będą realizowane w ramach tej integracji.
Określenie jakiego typu dane będą potrzebne do zasilania systemu BI, a także jakie dane będą dostarczane z systemu BI do modułu.

### 2.7. Migracja danych
Określenie czy klient posiada dane do zaimportowania z istniejących systemów. Wskazanie:
- Jakie dane wymagają migracji (kartoteki, dokumenty historyczne, salda).
- Źródła danych (inne systemy ERP, arkusze Excel, bazy danych).
- Wymagania dotyczące zachowania historii danych.

### 2.8. Wydajność i skalowalność
Określenie założeń wydajnościowych wpływających na decyzje architektoniczne modułu:
- **Wolumeny danych** — przewidywana liczba rekordów w kluczowych tabelach (bieżąca i docelowa po kilku latach użytkowania), częstotliwość operacji zapisu i odczytu.
- **Optymalizacja dostępu do danych** — wskazanie tabel i operacji wymagających szczególnej uwagi pod kątem wydajności (np. dedykowane indeksy, widoki, denormalizacja, mechanizmy cache).
- **Przetwarzanie wsadowe vs. online** — określenie, które operacje muszą działać w czasie rzeczywistym (interakcja użytkownika), a które mogą być realizowane w tle lub w trybie wsadowym (np. przeliczenia, generowanie raportów, synchronizacja danych).
- **Skalowalność** — jak moduł powinien zachowywać się przy rosnącej liczbie użytkowników, rekordów i równoczesnych operacji. Wskazanie potencjalnych wąskich gardeł i sposobów ich unikania.

### 2.9. Kierunki rozwoju modułu w przyszłości
Określenie kierunków rozwoju modułu w przyszłości, jakie funkcjonalności mogą być dodane w kolejnych etapach, które nie powinny być rozwijane w tej wersji rozwiązania. O ile taki rozwój jest planowany.


## ETAP 3: Specyfikacja szczegółowa

### 3.1. Dane operacyjne
Rozwinięcie struktur danych operacyjnych. Wskazanie ich dodatkowych cech, jak historyczność danych, wykorzystanie numeracji dokumentów. Wyszczególnienie pól danych. Określenie list szczegółowych w ramach obiektu. Określenie relacji do innych danych modułu, a także innych danych programu Soneta (poza projektowanym modułem).
Określenie części konfiguracyjnej, jak typy, definicje, słowniki, itp. powiązanych z omawianym obiektem.
Przykład: Dokument jest numerowany, określa datę wprowadzenia, zatwierdzenia, stany (bufor, zatwierdzony, odrzucony), jest powiązany z pracownikiem.
Przypisany do definicji dokumentu określającej zasady numeracji, tytuł dokumentu, warunki akceptacji, itp.

Przy specyfikowaniu danych operacyjnych należy korzystać z pliku `tables.md` w celu:
- odwoływania się do istniejących tabel programu Soneta po ich nazwach (kolumna „Tabela") i obiektach biznesowych (kolumna „Obiekt"),
- wykorzystania informacji o hierarchii nadrzędności tabel (kolumna „Nadrzędny") jako wzorca dla projektowania relacji inner w nowym module,
- rozróżnienia tabel konfiguracyjnych i operacyjnych (kolumna „Konfiguracyjna") — ten sam podział powinien być stosowany w nowym module,
- identyfikacji istniejących słowników, definicji i kartotek, do których nowe obiekty powinny się odwoływać zamiast tworzyć własne duplikaty.

### 3.2. Diagram relacji
Graficzne przedstawienie relacji między tabelami modułu oraz relacji do tabel innych modułów programu Soneta. Diagram powinien pokazywać:
- Relacje 1:N (inner) — tabele szczegółów
- Relacje N:1 (lookup) — odwołania do słowników i kartotek
- Relacje do tabel spoza modułu

Tabele spoza modułu powinny być identyfikowane na podstawie pliku `tables.md` — używając nazw tabel i obiektów tam zdefiniowanych. Diagram powinien wskazywać moduł źródłowy dla każdej tabeli zewnętrznej (np. `Kontrahenci` z modułu CRM, `Pracownicy` z modułu Kadry).

### 3.3. Relacje do danych programu
Określenie relacji do danych programu Soneta spoza modułu, które będą integrowane z modułem. Określenie jakie dane będą wymieniane między modułem, a innymi danymi programu Soneta.

Dla każdej relacji do danych programu należy na podstawie pliku `tables.md` wskazać:
- nazwę modułu programu Soneta (np. Handel, Kadry, Ksiega, CRM, Towary, Kasa),
- konkretną tabelę i obiekt biznesowy (np. tabela `Kontrahenci`, obiekt `Kontrahent` z modułu CRM),
- typ relacji (lookup — odwołanie do istniejącego obiektu, inner — tabela szczegółów, powiązanie logiczne),
- cel użycia danych z tej tabeli w kontekście projektowanego modułu.

### 3.4. Podstawowe listy modułu
Wyszczególnienie głównych list obiektów modułu.
Wyspecyfikowanie podstawowych kolumn listy, oraz kolumn opcjonalnych dostępnych dopiero w opcjach konfiguracyjnych.
Określenie podstawowych pól filtrujących dane, podstawowych filtrów oraz dodatkowych (po rozwinięciu).

### 3.5. Formularze
Wyszczególnienie podstawowych formularzy obiektów dostępnych z list. Opisanie ich zawartości, pól znajdujących się w obiekcie oraz list szczegółowych. Pogrupowanie danych w zakładki oraz grupy na zakładkach.

### 3.6. Workery i czynności
Określenie akcji i operacji dostępnych dla użytkownika na listach i formularzach modułu:
- **Czynności na formularzach** — akcje dostępne w menu „Czynności" na poszczególnych obiektach (np. zatwierdzanie, anulowanie, kopiowanie, generowanie powiązanych dokumentów). Dla każdej czynności wskazać: warunki dostępności (np. stan dokumentu), efekt działania, wymagane uprawnienia.
- **Czynności na listach** — operacje grupowe dostępne na listach (np. zatwierdzanie wielu dokumentów, eksport, zbiorowe przypisanie).
- **Workery** — procesy działające w tle, realizujące operacje wymagające dłuższego przetwarzania (np. przeliczenia, synchronizacja z systemami zewnętrznymi, generowanie raportów wsadowych). Wskazać wyzwalacze (ręczne, harmonogramowe, zdarzeniowe) oraz oczekiwane czasy wykonania.

### 3.7. Raporty i wydruki
Określenie raportów i wydruków dostępnych w module:
- **Wydruki dokumentów** — wydruki powiązane z konkretnymi obiektami (np. wydruk dokumentu, karty obiektu). Wskazać format (PDF, Excel), szablon, dane zawarte na wydruku.
- **Raporty zbiorcze** — raporty generowane na podstawie list lub danych zagregowanych (np. zestawienia, podsumowania okresowe). Wskazać parametry wejściowe (zakres dat, filtry), układ danych, grupowania.
- **Eksport danych** — możliwości eksportu danych z list do formatów zewnętrznych (Excel, CSV).

### 3.8. Procesy Workflow
Uszczegółowienie procesów biznesowych zdefiniowanych w sekcji 1.6 do poziomu implementacyjnego:
- **Stany obiektów** — lista stanów, przez które przechodzi obiekt w ramach procesu (np. Bufor → Zatwierdzony → W realizacji → Zakończony → Anulowany).
- **Przejścia między stanami** — warunki i reguły przejść (kto może zatwierdzić, jakie warunki muszą być spełnione, czy przejście jest odwracalne).
- **Automatyzacje** — akcje wykonywane automatycznie przy zmianie stanu (np. wysłanie powiadomienia, zmiana pól, wygenerowanie powiązanego dokumentu).
- **Ścieżki akceptacji** — jeśli proces wymaga akceptacji przez przełożonego lub inną rolę, określić ścieżkę i reguły eskalacji.

### 3.9. Uprawnienia i role
Szczegółowa specyfikacja modelu uprawnień modułu:
- **Matryca uprawnień** — tabela określająca dostęp poszczególnych ról (zdefiniowanych w sekcji 2.1) do funkcjonalności modułu:

| Funkcjonalność | Rola A | Rola B | Rola C |
|----------------|--------|--------|--------|
| Lista X — odczyt | Tak | Tak | Nie |
| Lista X — edycja | Tak | Nie | Nie |
| Czynność Y | Tak | Nie | Nie |

- **Uprawnienia do danych** — ograniczenia widoczności danych w zależności od roli (np. operator widzi tylko swoje dokumenty, kierownik widzi dokumenty podwładnych).
- **Uprawnienia konfiguracyjne** — kto może modyfikować ustawienia, definicje i słowniki modułu.

### 3.10. Integracje szczegółowe
Uszczegółowienie integracji zdefiniowanych w sekcjach 2.5 i 2.6 do poziomu implementacyjnego:
- **API i protokoły** — specyfikacja interfejsów wymiany danych (REST, SOAP, pliki CSV/XML, bezpośredni dostęp do bazy).
- **Formaty danych** — struktura wymienianych komunikatów i plików, mapowanie pól.
- **Częstotliwość i tryb synchronizacji** — import/eksport jednorazowy, cykliczny (harmonogram), w czasie rzeczywistym (zdarzeniowy).
- **Obsługa błędów** — postępowanie w przypadku niedostępności systemu zewnętrznego, walidacja danych wejściowych, logowanie błędów.

### 3.11. Scenariusze testowe
Określenie zakresu testów wymaganych do weryfikacji modułu:
- **Testy funkcjonalne** — scenariusze testowe pokrywające kluczowe ścieżki użytkownika zdefiniowane w sekcji 1.5. Dla każdego scenariusza: kroki, dane wejściowe, oczekiwany rezultat.
- **Testy integracyjne** — weryfikacja poprawności współpracy z innymi modułami programu Soneta i systemami zewnętrznymi.
- **Testy wydajnościowe** — weryfikacja założeń z sekcji 2.8 (wolumeny danych, czasy odpowiedzi).
- **Przypadki brzegowe** — scenariusze nietypowe i graniczne (puste dane, maksymalne wolumeny, równoczesna edycja, brak uprawnień).

### 3.12. Dane demonstracyjne
Określenie jakie dane przykładowe powinny być przygotowane:
- Dane do bazy Demo — reprezentatywne scenariusze pokazujące możliwości modułu.
- Dane do testów — zestawy danych pokrywające przypadki brzegowe i typowe scenariusze.

### 3.13. Słownik terminów
Definicje kluczowych terminów biznesowych i technicznych używanych w kontekście modułu. Szczególnie ważne przy modułach domenowych (np. kontroling, logistyka), gdzie terminologia może być niejednoznaczna lub specyficzna dla branży.


## Otwarte kwestie
Otwarte kwestie to centralne miejsce, w którym gromadzone są **wszystkie decyzje projektowe pozostające do podjęcia**. Pełni rolę jednego źródła prawdy o tym, co jeszcze trzeba ustalić — dzięki niej zarówno autor planu, jak i pozostali odbiorcy dokumentu (zespół projektowy, implementacyjny, marketing, sprzedaż) widzą w każdym momencie, co blokuje dalsze prace i czego brakuje do zamknięcia danego etapu. Kwestie mogą pojawiać się na każdym etapie — od wizji po specyfikację szczegółową.

### Co trafia na listę
Wpis zakładamy zawsze, gdy:
- pojawia się pytanie projektowe bez jednoznacznej odpowiedzi — zapisujemy je zamiast przyjmować domysł,
- istnieje kilka wariantów rozwiązania i wybór wymaga decyzji,
- brakuje informacji od osoby trzeciej (klient, inny zespół, dział prawny, kwestie licencyjne),
- decyzję świadomie odkładamy, aby nie blokować bieżącego etapu.

Każde nierozstrzygnięte założenie ukryte w treści dokumentu powinno mieć odpowiadający mu wpis na liście otwartych kwestii.

### Struktura tabeli

| Nr | Etap | Obszar | Kwestia | Wpływ | Blokująca | Status | Decyzja i uzasadnienie | Data |
|----|------|--------|---------|-------|-----------|--------|------------------------|------|
| 1 | 2 | Dane | [Opis problemu do rozstrzygnięcia] | Wysoki/Średni/Niski | Tak/Nie | Otwarta / W trakcie / Zamknięta | [Podjęta decyzja + uzasadnienie] | RRRR-MM-DD |

- **Obszar** — czego kwestia dotyczy (dane, UI, integracje, uprawnienia, wydajność, licencje, proces…).
- **Wpływ** — skala konsekwencji decyzji dla projektu.
- **Blokująca** — czy kwestia uniemożliwia zatwierdzenie etapu lub rozpoczęcie implementacji.
- **Status** — cykl życia kwestii: *Otwarta* → *W trakcie* → *Zamknięta*.
- **Decyzja i uzasadnienie** — przy zamknięciu zapisujemy *co* postanowiono oraz *dlaczego*. Zamkniętych kwestii nie usuwamy — stanowią historię decyzji projektowych.

### Zasady prowadzenia
1. Numeracja jest stała — raz nadany numer nie zmienia się, a zamknięte pozycje pozostają na liście.
2. Lista jest aktualizowana na bieżąco — decyzja podjęta w trakcie rozmowy od razu zmienia status na *Zamknięta* wraz z uzupełnieniem decyzji.
3. Pełną listę prezentujemy na końcu każdego etapu, wyraźnie wskazując kwestie wciąż otwarte oraz blokujące.
4. Bramka jakości — nie przechodzimy do kolejnego etapu z otwartymi kwestiami blokującymi dla tego etapu; kwestie nieblokujące można przenieść dalej, ale pozostają widoczne.
5. Powiązanie z TODO — „Zamknięcie otwartych kwestii" jest pozycją dokumentu TODO; do implementacji nie wchodzimy z otwartymi kwestiami blokującymi.


## Przygotowanie dokumentu TODO

Dokument powinien określać kolejne kroki w projekcie i procesie implementacji. Powinien to być osobny dokument, wykorzystywany przez zespół implementacyjny oraz AI do realizacji kolejnych kroków tworzenia modułu, ale także przez inne działy firmy Soneta, jak marketing, sprzedaż czy dział wsparcia. Dokument powinien być aktualizowany w trakcie realizacji projektu, a także po jego zakończeniu, w celu określenia kolejnych kroków rozwoju modułu.

### Uzupełnienie planu (do realizacji przed implementacją)
- [ ] Weryfikacja i zatwierdzenie Etapu 1 przez interesariuszy
- [ ] Weryfikacja i zatwierdzenie Etapu 2 przez zespół projektowy
- [ ] Uzupełnienie specyfikacji szczegółowej (Etap 3) dla wszystkich obiektów modułu
- [ ] Zamknięcie otwartych kwestii

### Implementacja
- [ ] Określenie modelu danych projektu, takich jak tabele, pola, relacje, itp.
- [ ] Zbudowanie na podstawie modelu pliku business.xml
- [ ] Struktura menu modułu, listy, widoki
- [ ] Wyglądy formularzy, ich zakładek
- [ ] Zakładki konfiguracji, takich jak słowniki, definicje, ustawienia
- [ ] Workery, menu "Czynności" dostępne na listach i formularzach
- [ ] Raporty i wydruki dostępne na listach i formularzach
- [ ] Procesy Workflow w których uczestniczy moduł i realizowane w module
- [ ] Wskaźniki, listy, agregaty, wykresy BI
- [ ] Uprawnienia i role użytkowników
- [ ] Punkty styku/integracji z innymi systemami
- [ ] Uzupełnienie bazy Demo i inne bazy demonstracyjne
- [ ] Testy integracyjne, interfejsowe
- [ ] Określenie struktury dokumentacji użytkownika i technicznej
