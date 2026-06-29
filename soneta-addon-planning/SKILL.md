---
name: soneta-addon-planning
description: >
  Planowanie projektów dodatków dla platformy Soneta (enova365, Triva). Tworzy
  kompletną dokumentację projektową obejmującą: strukturę danych (tabele, relacje),
  elementy konfigurowalne, definicje list i menu, formularze, workery i raporty.
  Używaj gdy użytkownik prosi o zaplanowanie nowego modułu/dodatku enova365,
  przygotowanie założeń projektu, stworzenie specyfikacji funkcjonalnej dodatku,
  lub zdefiniowanie struktury danych i interfejsu użytkownika dla nowego modułu.
---

# Planowanie projektu modułu/dodatku Soneta

Skill prowadzi interaktywny proces planowania nowego modułu dla platformy Soneta (programy enova365 i Triva). Proces przebiega etapowo — każdy etap wymaga zatwierdzenia przez użytkownika przed przejściem do kolejnego.

## Przebieg procesu

Proces planowania składa się z trzech etapów o rosnącym poziomie szczegółowości:

1. **ETAP 1** — Wizja i kontekst biznesowy (dla decydentów, marketingu, sprzedaży)
2. **ETAP 2** — Architektura modułu (dla zespołu projektowego)
3. **ETAP 3** — Specyfikacja szczegółowa (dla zespołu implementacyjnego i AI)

Na końcu generowany jest dokument TODO z kolejnymi krokami implementacji.

## Jak prowadzić rozmowę

Proces jest interaktywny. Nie generuj całego dokumentu naraz — pracuj etap po etapie:

1. **Zbierz informacje** — Na początku zapytaj użytkownika o ogólną ideę modułu. Co chce osiągnąć? Dla kogo jest ten moduł? Jaki problem rozwiązuje?
2. **Opracuj etap** — Na podstawie zebranych informacji wygeneruj dokument danego etapu. Jeśli brakuje Ci informacji, zadaj konkretne pytania zamiast zgadywać.
3. **Poczekaj na zatwierdzenie** — Przedstaw dokument etapu użytkownikowi i poczekaj na jego akceptację, uwagi lub poprawki. Nie przechodź do kolejnego etapu bez wyraźnej zgody.
4. **Iteruj** — Jeśli użytkownik ma uwagi, popraw dokument i ponownie przedstaw do zatwierdzenia.
5. **Przejdź dalej** — Po zatwierdzeniu przejdź do kolejnego etapu, zadając dodatkowe pytania potrzebne do jego opracowania.

Prowadź rozmowę w języku polskim. Zadawaj pytania jedno po drugim — nie zasypuj użytkownika listą 15 pytań naraz. Grupuj pytania tematycznie (2–4 pytania na raz) i dostosowuj kolejne pytania do udzielonych odpowiedzi.

## Dane referencyjne — tabele programu Soneta

Plik `tables.md` zawiera kompletny opis aktualnych modułów i tabel programu Soneta. Jest duży, więc czytaj go selektywnie — tylko moduły istotne dla projektowanego dodatku.

Struktura pliku: nagłówki `# Moduł: NazwaModułu` z opisem, a pod nimi tabele w formacie:

| Tabela | Obiekt | Tytuł tabeli | Nadrzędny | Konfiguracyjna | Opis |

Kolumny:
- **Tabela** — nazwa tabeli w bazie danych
- **Obiekt** — nazwa klasy C# (obiekt biznesowy)
- **Nadrzędny** — tabela nadrzędna (`root` = tabela główna, inna nazwa = tabela szczegółów w relacji inner)
- **Konfiguracyjna** — `X` = tabela konfiguracyjna (definicje, słowniki, ustawienia), pusta = tabela operacyjna
- **Opis** — opis przeznaczenia tabeli

Korzystaj z tego pliku aby:
- sprawdzić, czy potrzebne struktury danych już istnieją (unikanie duplikacji),
- wskazać konkretne tabele, do których nowy moduł będzie się odwoływać,
- rozpoznać wzorce projektowe (podział na tabele konfiguracyjne/operacyjne, hierarchia nadrzędny-szczegółowy, stosowanie definicji dokumentów),
- zidentyfikować moduły, z którymi projektowany moduł będzie współpracować.

Gdy użytkownik wspomina o integracji z istniejącymi danymi (np. pracownicy, kontrahenci, towary), przeczytaj odpowiedni moduł z `tables.md` i wskaż konkretne tabele i obiekty.

---

## ETAP 1: Wizja i kontekst biznesowy

Cel: ustalić co budujemy, dla kogo i dlaczego. Dokument tego etapu jest przeznaczony dla decydentów, marketingu i sprzedaży.

### Pytania do zadania użytkownikowi

Zacznij od ogólnej idei, potem doprecyzowuj. Nie zadawaj wszystkich pytań naraz.

**Pierwsza tura** — zrozumienie idei:
- Co jest głównym celem modułu? Jaki problem rozwiązuje?
- Kto jest docelowym użytkownikiem? (mała/średnia/duża firma, branża)

**Druga tura** — funkcjonalności i korzyści:
- Jakie są najważniejsze funkcjonalności? (kluczowe vs opcjonalne)
- Jakie korzyści uzyska klient?

**Trzecia tura** — kontekst i ograniczenia:
- Jakie procesy biznesowe realizuje moduł?
- Czy są ograniczenia techniczne, prawne, licencyjne?
- Jakie moduły programu Soneta będą wykorzystywane?

### Sekcje dokumentu Etapu 1

Wygeneruj dokument Markdown zawierający te sekcje:

#### 1.1. Cel biznesowy projektu
4–5 zdań o celu projektu, kluczowym elemencie, najważniejszej rzeczy do osiągnięcia. Zacznij od podstawowej idei modułu.

#### 1.2. Profil klienta docelowego
Typ firmy (mała/średnia/duża), branża, działalność.

#### 1.3. Korzyści dla klienta
Najważniejsze korzyści, problemy rozwiązywane przez moduł. Informacja dla marketingu i sprzedaży.

#### 1.4. Najważniejsze funkcjonalności (priorytetyzacja)
Podział na:
- **Krytyczne (must-have)** — bez nich moduł nie ma sensu
- **Ważne (should-have)** — istotne, ale moduł może działać bez nich w v1
- **Opcjonalne (nice-to-have)** — kolejne wersje

#### 1.5. Scenariusze użytkownika
Kilka kluczowych scenariuszy krok po kroku — jakie działania, jakie dane, jakie wyniki. Tylko najważniejsze dane, bez szczegółów.

#### 1.6. Procesy biznesowe
Procesy realizowane w całości przez moduł oraz procesy, w których moduł uczestniczy częściowo (z innymi elementami programu).

#### 1.7. Ograniczenia modułu i wymagania licencyjne
Ograniczenia funkcjonalne, techniczne, integracyjne, prawne. Wymagane licencje programu Soneta i ewentualne licencje zewnętrzne.

#### 1.8. Założenia i zależności
- **Założenia** — co zakładamy jako pewne (np. dostępność modułów, wersja platformy)
- **Zależności** — od czego projekt zależy (inne projekty, dane od klienta, zespół)

#### 1.9. Ryzyka projektu
Ryzyka techniczne, biznesowe, integracyjne — z prawdopodobieństwem, wpływem i planem mitygacji.

#### 1.10. Kryteria akceptacji
Mierzalne kryteria gotowości: funkcjonalne (scenariusze), wydajnościowe (wolumeny, czasy), jakościowe (testy, błędy).

#### 1.11. Harmonogram i kamienie milowe
Ramowy harmonogram: fazy projektu, kamienie milowe (zatwierdzenie specyfikacji, MVP, testy, pilotaż, produkcja), zależności czasowe. Charakter orientacyjny.

---

## ETAP 2: Architektura modułu

Cel: określić jak moduł będzie zbudowany — role, dane, interfejs, integracje. Dokument dla zespołu projektowego.

### Pytania do zadania użytkownikowi

**Pierwsza tura** — role i konfiguracja:
- Jakie role użytkowników będą korzystać z modułu? Jakie zadania realizują?
- Jakie elementy powinny być konfigurowalne przez klienta?

**Druga tura** — dane i interfejs:
- Jakie są główne obiekty danych? (dokumenty, kartoteki, słowniki)
- Jak powinna wyglądać struktura menu?

**Trzecia tura** — integracje i przyszłość:
- Jakie dane z istniejących modułów Soneta będą wykorzystywane? (w tym momencie przeczytaj odpowiednie moduły z `tables.md`)
- Czy moduł integruje się z systemami zewnętrznymi?
- Czy klient ma dane do migracji?

### Sekcje dokumentu Etapu 2

#### 2.1. Role użytkowników
Lista operatorów, ich zadania, procesy w których uczestniczą.

#### 2.2. Zakres konfiguracji modułu
Elementy konfigurowalne: definicje dokumentów, słowniki, ustawienia. Zarówno konfiguracja wstępna jak i definicje używane przy przetwarzaniu danych operacyjnych.

#### 2.3. Kluczowe struktury danych
Najważniejsze struktury danych — dokumenty, kartoteki. Bez szczegółowej zawartości (to Etap 3). Ogólny diagram relacji między głównymi obiektami.

Na podstawie `tables.md` sprawdź:
- czy potrzebne struktury już istnieją w programie,
- jakie tabele nadrzędne mogą być wykorzystane,
- jakie wzorce projektowe stosują istniejące moduły (podział konfiguracyjne/operacyjne, definicje dokumentów, obiekty Guided i szczegółowe, datapacki).

#### 2.4. Struktura menu i elementy interfejsu
Foldery, hierarchia list w menu głównym, grupowanie funkcjonalne. Wyróżnienie elementów kluczowych dla sukcesu produktu.

#### 2.5. Relacje z modułami programu Soneta
Na podstawie `tables.md`:
- wskaż konkretne moduły, z którymi nowy moduł współpracuje,
- wymień tabele i obiekty, do których się odwołuje (relacje lookup/inner),
- określ, czy wymagane są rozszerzenia istniejących tabel.

#### 2.6. Relacje z innymi systemami
Integracje z systemami zewnętrznymi, wymiana danych, procesy integracyjne. Zasilanie systemu BI.

#### 2.7. Migracja danych
Dane do importu, źródła (inne ERP, Excel, bazy), zachowanie historii.

#### 2.8. Wydajność i skalowalność
- **Wolumeny danych** — przewidywana liczba rekordów, częstotliwość operacji
- **Optymalizacja** — indeksy, widoki, cache
- **Przetwarzanie wsadowe vs online** — co w czasie rzeczywistym, co w tle
- **Skalowalność** — wzrost użytkowników i danych, wąskie gardła

#### 2.9. Kierunki rozwoju
Przyszłe funkcjonalności, które nie wchodzą w zakres bieżącej wersji.

---

## ETAP 3: Specyfikacja szczegółowa

Cel: dostarczyć szczegółowy opis każdego elementu modułu na poziomie implementacyjnym. Dokument dla zespołu implementacyjnego i AI.

### Pytania do zadania użytkownikowi

Na tym etapie pytania dotyczą szczegółów poszczególnych obiektów. Pracuj obiekt po obiekcie:

- Jakie pola powinien mieć ten dokument/kartoteka?
- Jakie stany przechodzi? (bufor, zatwierdzony, anulowany...)
- Jakie czynności są dostępne? (zatwierdzanie, kopiowanie, generowanie...)
- Jakie wydruki i raporty?
- Kto ma dostęp do czego?

### Sekcje dokumentu Etapu 3

#### 3.1. Dane operacyjne
Dla każdego obiektu danych:
- pola danych z typami,
- cechy szczególne (historyczność, numeracja dokumentów, stany),
- listy szczegółowe (relacje inner),
- relacje do innych danych modułu i do danych spoza modułu,
- część konfiguracyjna (typy, definicje, słowniki).

Na podstawie `tables.md`:
- odwołuj się do istniejących tabel po nazwach (kolumna „Tabela") i obiektach (kolumna „Obiekt"),
- wykorzystuj hierarchię nadrzędności jako wzorzec relacji inner,
- rozróżniaj tabele konfiguracyjne i operacyjne — ten sam podział stosuj w nowym module,
- identyfikuj istniejące słowniki i kartoteki zamiast tworzyć duplikaty.

#### 3.2. Diagram relacji
Graficzne przedstawienie relacji (w formie tekstowej Mermaid lub tabeli):
- Relacje 1:N (inner) — tabele szczegółów
- Relacje N:1 (lookup) — odwołania do słowników i kartotek
- Relacje do tabel spoza modułu (z nazwą modułu źródłowego)

#### 3.3. Relacje do danych programu
Dla każdej relacji do danych spoza modułu wskaż na podstawie `tables.md`:
- nazwę modułu programu Soneta,
- konkretną tabelę i obiekt biznesowy,
- typ relacji (lookup, inner, powiązanie logiczne),
- cel użycia danych w kontekście projektowanego modułu.

#### 3.4. Podstawowe listy modułu
Dla każdej listy:
- kolumny podstawowe i opcjonalne,
- pola filtrujące (podstawowe i dodatkowe po rozwinięciu),
- filtry predefiniowane.

#### 3.5. Formularze
Dla każdego formularza:
- zakładki i grupy na zakładkach,
- pola w każdej grupie,
- listy szczegółów (sublists).

#### 3.6. Workery i czynności
- **Czynności na formularzach** — warunki dostępności, efekt, wymagane uprawnienia
- **Czynności na listach** — operacje grupowe
- **Workery** — procesy w tle, wyzwalacze (ręczne/harmonogramowe/zdarzeniowe)

#### 3.7. Raporty i wydruki
- **Wydruki dokumentów** — format, szablon, dane
- **Raporty zbiorcze** — parametry, układ, grupowania
- **Eksport danych** — formaty (Excel, CSV)

#### 3.8. Procesy Workflow
- **Stany obiektów** — lista stanów i przejść
- **Przejścia** — warunki, reguły, odwracalność
- **Automatyzacje** — akcje przy zmianie stanu
- **Ścieżki akceptacji** — reguły eskalacji

#### 3.9. Uprawnienia i role
- **Matryca uprawnień** — tabela ról vs funkcjonalności
- **Uprawnienia do danych** — ograniczenia widoczności
- **Uprawnienia konfiguracyjne** — kto modyfikuje ustawienia

#### 3.10. Integracje szczegółowe
- API i protokoły (REST, SOAP, CSV/XML)
- Formaty danych i mapowanie pól
- Częstotliwość synchronizacji
- Obsługa błędów

#### 3.11. Scenariusze testowe
- Testy funkcjonalne (kroki, dane, oczekiwany rezultat)
- Testy integracyjne
- Testy wydajnościowe
- Przypadki brzegowe

#### 3.12. Dane demonstracyjne
- Dane do bazy Demo
- Dane do testów

#### 3.13. Słownik terminów
Definicje kluczowych terminów biznesowych i technicznych, szczególnie przy modułach domenowych.

---

## Otwarte kwestie

Otwarte kwestie to **jedno wspólne miejsce, w którym zbierane są wszystkie decyzje projektowe, które muszą jeszcze zostać podjęte**. Dzięki niej użytkownik skilla ma w każdym momencie jasny obraz tego, co pozostaje do ustalenia na danym etapie i co blokuje przejście dalej. Prowadź tę listę przez cały proces — jest tak samo ważna jak same dokumenty etapów.

### Co trafia na listę

Dopisuj nową kwestię zawsze, gdy:
- pojawia się pytanie projektowe, na które nie znasz odpowiedzi — **zamiast zgadywać, zapisz je jako otwartą kwestię**,
- istnieje kilka alternatyw i wybór należy do użytkownika (np. wariant struktury danych, sposób integracji),
- brakuje informacji od osoby trzeciej (klient, inny zespół, dział prawny, licencjonowanie),
- decyzję świadomie odkładasz „na później", aby nie blokować bieżącego etapu,
- użytkownik mówi „zastanowię się", „dopytam", „nie wiem jeszcze" — to sygnał do założenia wpisu.

Nie zostawiaj nierozstrzygniętych założeń ukrytych w treści dokumentu — każde takie miejsce powinno mieć odpowiadający wpis na liście otwartych kwestii.

### Struktura tabeli

| Nr | Etap | Obszar | Kwestia | Wpływ | Blokująca | Status | Decyzja i uzasadnienie | Data |
|----|------|--------|---------|-------|-----------|--------|------------------------|------|
| 1 | 2 | Dane | [Opis problemu do rozstrzygnięcia] | Wysoki/Średni/Niski | Tak/Nie | Otwarta / W trakcie / Zamknięta | [Podjęta decyzja + dlaczego] | RRRR-MM-DD |

- **Obszar** — czego dotyczy (dane, UI, integracje, uprawnienia, wydajność, licencje, proces…).
- **Wpływ** — jak duże są konsekwencje decyzji dla projektu.
- **Blokująca** — czy kwestia uniemożliwia zatwierdzenie etapu lub rozpoczęcie implementacji.
- **Status** — cykl życia: *Otwarta* (czeka na decyzję) → *W trakcie* (analizowana/konsultowana) → *Zamknięta* (decyzja zapadła).
- **Decyzja i uzasadnienie** — przy zamknięciu wpisz nie tylko *co* postanowiono, ale i *dlaczego*. Nigdy nie usuwaj zamkniętych kwestii — stanowią historię decyzji projektowych.

### Zasady prowadzenia

1. **Numeracja jest stała** — raz nadany numer kwestii nie zmienia się; zamknięte pozycje zostają na liście.
2. **Aktualizuj na bieżąco** — gdy w rozmowie zapadnie decyzja, od razu zmień status na *Zamknięta* i uzupełnij kolumnę decyzji.
3. **Prezentuj na końcu każdego etapu** — przed przejściem dalej pokaż pełną listę i wyraźnie wskaż, które kwestie są wciąż otwarte oraz które są **blokujące**.
4. **Bramka jakości** — nie przechodź do kolejnego etapu z otwartymi kwestiami *blokującymi* dla tego etapu. Kwestie nieblokujące można przenieść dalej, ale muszą pozostać widoczne.
5. **Powiązanie z TODO** — „Zamknięcie otwartych kwestii" jest pozycją dokumentu TODO; do implementacji nie wchodzimy z otwartymi kwestiami blokującymi.

---

## Dokument TODO

Po zakończeniu wszystkich etapów wygeneruj dokument TODO z kolejnymi krokami:

### Uzupełnienie planu
- [ ] Weryfikacja i zatwierdzenie Etapu 1 przez interesariuszy
- [ ] Weryfikacja i zatwierdzenie Etapu 2 przez zespół projektowy
- [ ] Uzupełnienie specyfikacji szczegółowej (Etap 3) dla wszystkich obiektów
- [ ] Zamknięcie otwartych kwestii

### Implementacja
- [ ] Model danych — tabele, pola, relacje
- [ ] Plik business.xml (→ skill `soneta-business-xml`)
- [ ] Struktura menu, listy, widoki
- [ ] Formularze i zakładki (→ skill `soneta-form-xml`)
- [ ] Konfiguracja — słowniki, definicje, ustawienia
- [ ] Workery i czynności
- [ ] Raporty i wydruki
- [ ] Procesy Workflow
- [ ] Wskaźniki i wykresy BI
- [ ] Uprawnienia i role
- [ ] Integracje z innymi systemami
- [ ] Baza Demo i dane demonstracyjne
- [ ] Testy integracyjne i interfejsowe
- [ ] Dokumentacja użytkownika i techniczna

## Powiązanie z innymi skillami

Po zatwierdzeniu planu projektu:
1. **soneta-business-xml** — generowanie pliku business.xml na podstawie modelu danych z Etapu 3
2. **soneta-form-xml** — generowanie formularzy i widoków UI na podstawie sekcji 3.4 i 3.5
3. **soneta-programming** — implementacja logiki biznesowej, workerów
