# ETAP 3 — Specyfikacja szczegółowa

Cel: dostarczyć szczegółowy opis każdego elementu modułu na poziomie implementacyjnym. Dokument dla zespołu implementacyjnego i AI (jest podstawą do generowania `business.xml`, formularzy i kodu w kolejnych skillach).

## Pytania do zadania użytkownikowi

Na tym etapie pytania dotyczą szczegółów poszczególnych obiektów. Pracuj obiekt po obiekcie:
- Jakie pola powinien mieć ten dokument/kartoteka?
- Jakie reguły poprawności muszą spełniać dane? Co blokuje zapis, a co jest tylko ostrzeżeniem? (→ weryfikatory, 3.6)
- Jakie stany przechodzi? (bufor, zatwierdzony, anulowany…)
- Jakie czynności i algorytmy są dostępne? (zatwierdzanie, kopiowanie, generowanie, przeliczenia…)
- Czy któryś algorytm zależy od równoległej pracy innych stanowisk? (np. ciągła numeracja, rezerwacja, limit → transakcja serwerowa, 3.8)
- Jakie wydruki i raporty?
- Kto ma dostęp do czego?
- Czy moduł wymaga danych konfiguracyjnych zainicjowanych w bazie od razu po instalacji? (słowniki, definicje dokumentów, role, ustawienia domyślne → dane inicjujące, 3.14)

## Sekcje dokumentu Etapu 3

### 3.1. Dane operacyjne
Dla każdego obiektu danych:
- pola danych z typami,
- cechy szczególne (historyczność, numeracja dokumentów, stany),
- listy szczegółowe (relacje inner),
- relacje do innych danych modułu i do danych spoza modułu,
- część konfiguracyjna (typy, definicje, słowniki).

Przykład: dokument jest numerowany, ma datę wprowadzenia i zatwierdzenia, stany (bufor/zatwierdzony/odrzucony), jest powiązany z pracownikiem i przypisany do definicji dokumentu określającej zasady numeracji, tytuł, warunki akceptacji.

Na podstawie inwentaryzacji modułów (`scan-modules`, patrz `dane-referencyjne.md`):
- odwołuj się do istniejących tabel po nazwach (`TableType`) i obiektach biznesowych (`RowType`),
- wykorzystuj hierarchię `Guided` (`root` / `child: Pole→TypRow`) jako wzorzec relacji inner,
- rozróżniaj tabele konfiguracyjne i operacyjne (kolumna `Konfig`) — ten sam podział stosuj w nowym module,
- identyfikuj istniejące słowniki i kartoteki zamiast tworzyć duplikaty; do pól wybranego rekordu użyj `scan-props`.

**Mapowanie nazw logicznych na `tablename`.** Plan operuje nazwami logicznymi obiektów, ale
fizyczna nazwa tabeli (`tablename` w business.xml, skill [business-xml](../../business-xml/SKILL.md)) ma **limit
≤16 znaków**. Już w Etapie 3 zapisz w dokumencie tabelę mapowań i utrzymuj ją przy każdym nowym
obiekcie — inaczej przy generowaniu business.xml spójność nazw się rozjedzie:

| Nazwa logiczna (plan) | `tablename` (≤16 znaków) |
|---|---|
| Ocena kontrahenta | `OcenyKontrah` |
| Kryterium oceny | `KryteriaOcen` |

Zasady: **liczba mnoga**, **unikalność globalna** (w całej bazie, nie tylko w module — sprawdź
kolizje z inwentaryzacją `scan-modules`), skróty czytelne i konsekwentne.

### 3.2. Diagram relacji
Graficzne przedstawienie relacji (Mermaid lub tabela):
- Relacje 1:N (inner) — tabele szczegółów
- Relacje N:1 (lookup) — odwołania do słowników i kartotek
- Relacje do tabel spoza modułu (z nazwą modułu źródłowego, np. `Kontrahent` z CRM, `Pracownik` z Kadry — nazwy z inwentaryzacji `scan-modules`)

### 3.3. Relacje do danych platformy
Dla każdej relacji do danych spoza modułu wskaż na podstawie inwentaryzacji modułów (`scan-modules`, patrz `dane-referencyjne.md`):
- nazwę modułu platformy Soneta (np. Handel, Kadry, Ksiega, CRM, Towary, Kasa),
- konkretny `TableType` i `RowType` (np. tabela `Kontrahenci`, obiekt `Kontrahent` z CRM),
- typ relacji (lookup, inner, powiązanie logiczne),
- cel użycia danych w kontekście projektowanego modułu.

### 3.4. Podstawowe listy modułu
Dla każdej listy:
- kolumny podstawowe i opcjonalne (dostępne w opcjach konfiguracyjnych),
- pola filtrujące (podstawowe i dodatkowe po rozwinięciu),
- filtry predefiniowane.

### 3.5. Formularze
Dla każdego formularza:
- zakładki i grupy na zakładkach,
- pola w każdej grupie,
- listy szczegółów (sublists).

### 3.6. Weryfikatory (walidacja danych operatora)
Dla każdego obiektu danych wyspecyfikuj **listę weryfikatorów** — reguł sprawdzających poprawność i spójność danych wprowadzanych przez operatora. To one pilnują, by do bazy nie trafiły dane naruszające reguły biznesowe. Implementację po stronie kodu opisuje [verifiers.md](../../programming/references/verifiers.md).

Dla każdego weryfikatora podaj:
- **obiekt i pola-źródła** — czego dotyczy i zmiana których pól go uruchamia,
- **regułę poprawności** — warunek, który dane muszą spełnić (np. „data zakończenia ≥ data rozpoczęcia", „kod niepusty i unikalny", „suma pozycji = wartość nagłówka"),
- **poziom ważności** — *Error* (blokuje zapis), *Warning* (ostrzeżenie, można zignorować), *Information* (komunikat),
- **komunikat** dla operatora.

| Obiekt | Reguła | Pola-źródła | Poziom | Komunikat |
|--------|--------|-------------|--------|-----------|
| [nazwa] | [warunek poprawności] | [pola] | Error / Warning / Information | [treść] |

> Każdy weryfikator wymaga **testu integracyjnego** (patrz sekcja 3.13) potwierdzającego, że reguła blokuje/przepuszcza zapis zgodnie z założeniem.

### 3.7. Workery i czynności
- **Czynności na formularzach** — menu „Czynności" na obiektach (zatwierdzanie, anulowanie, kopiowanie, generowanie powiązanych dokumentów). Dla każdej: warunki dostępności (np. stan dokumentu), efekt, wymagane uprawnienia.
- **Czynności na listach** — operacje grupowe (zatwierdzanie wielu dokumentów, eksport, zbiorowe przypisanie).
- **Workery** — procesy w tle (przeliczenia, synchronizacja, raporty wsadowe). Wskaż wyzwalacze (ręczne/harmonogramowe/zdarzeniowe) i oczekiwane czasy.
- **Algorytmy obiektów biznesowych** — metody liczące i przekształcające dane (przeliczenia sum, generowanie powiązań, wyznaczanie stanów).

> Każdy worker, algorytm obiektu biznesowego i inny nietrywialny algorytm wymaga **testu integracyjnego** (patrz sekcja 3.13).

### 3.8. Algorytmy w transakcji serwerowej
Zidentyfikuj algorytmy i procesy, które **muszą wykonać się w transakcji serwerowej** — czyli takie, których poprawność zależy od zmian wykonywanych **równolegle na innych stanowiskach** (np. ciągła numeracja dokumentów, rezerwacja zasobu, kontrola limitu, sekwencyjne przydzielanie identyfikatorów). Bez transakcji serwerowej dwa stanowiska mogłyby pobrać ten sam numer lub przekroczyć limit. Mechanizm (eventy serwerowe `Session.ServerEvents`) opisuje [events.md](../../programming/references/events.md).

Dla każdego takiego procesu podaj:
- **obiekt i moment** wykonania (zwykle podczas `Save()`),
- **na czym polega zależność międzystanowiskowa** (co się zepsuje przy równoległej pracy bez transakcji),
- **wymóg krótkiego czasu** — logika serwerowa trzyma transakcję, więc musi być szybka (ciężkie obliczenia wynieś do workera/eventu sesyjnego).

> Logika w transakcji serwerowej wymaga **testu integracyjnego** (patrz sekcja 3.13), najlepiej sprawdzającego zachowanie przy współbieżnym zapisie.

### 3.9. Raporty i wydruki
- **Wydruki dokumentów** — format (PDF, Excel), szablon, dane.
- **Raporty zbiorcze** — parametry wejściowe (zakres dat, filtry), układ, grupowania.
- **Eksport danych** — formaty (Excel, CSV).

### 3.10. Procesy Workflow
Uszczegółowienie procesów z sekcji 1.7:
- **Stany obiektów** — lista stanów (np. Bufor → Zatwierdzony → W realizacji → Zakończony → Anulowany).
- **Przejścia** — warunki i reguły (kto zatwierdza, jakie warunki, czy odwracalne).
- **Automatyzacje** — akcje przy zmianie stanu (powiadomienie, zmiana pól, generowanie dokumentu).
- **Ścieżki akceptacji** — reguły eskalacji, jeśli proces wymaga akceptacji przełożonego.

### 3.11. Uprawnienia i role
- **Matryca uprawnień** — tabela ról (z sekcji 2.1) vs funkcjonalności:

| Funkcjonalność | Rola A | Rola B | Rola C |
|----------------|--------|--------|--------|
| Lista X — odczyt | Tak | Tak | Nie |
| Lista X — edycja | Tak | Nie | Nie |
| Czynność Y | Tak | Nie | Nie |

- **Uprawnienia do danych** — ograniczenia widoczności (operator widzi swoje dokumenty, kierownik — podwładnych).
- **Uprawnienia konfiguracyjne** — kto modyfikuje ustawienia, definicje, słowniki.
- **Umiejscowienie w drzewie uprawnień** — gdzie osoba konfigurująca role znajdzie prawa modułu:
  wspólna gałąź modułu czy rozbicie na obszary, oddzielenie danych operacyjnych od konfiguracji.
  Ustalenie przekłada się wprost na plik `*.rightstree.xml` towarzyszący definicjom danych —
  wpis dostaje każdy obiekt niedziedziczący praw po obiekcie nadrzędnym ([rights-tree.md](../../business-xml/references/rights-tree.md)). Bez tego prawa modułu trafią do zbiorczej
  gałęzi „Dodatki".

### 3.12. Integracje szczegółowe
Uszczegółowienie integracji z sekcji 2.5 i 2.6:
- **API i protokoły** — REST, SOAP, pliki CSV/XML, bezpośredni dostęp do bazy.
- **Formaty danych** — struktura komunikatów i plików, mapowanie pól.
- **Częstotliwość i tryb synchronizacji** — jednorazowy, cykliczny (harmonogram), w czasie rzeczywistym (zdarzeniowy).
- **Obsługa błędów** — niedostępność systemu zewnętrznego, walidacja danych wejściowych, logowanie błędów.

### 3.13. Scenariusze i testy integracyjne
Testy integracyjne pisze się na prawdziwej bazie (nie na mockach) — patrz [integration-tests.md](../../programming/references/integration-tests.md). Zaplanuj je **równolegle z logiką**, nie po fakcie.

- **Obowiązkowe pokrycie testami integracyjnymi** — dla **każdego** elementu logiki z Etapu 3 zaplanuj odpowiedni test:
  - każdy **worker** i proces w tle (3.7),
  - każdy **algorytm obiektu biznesowego** i inny nietrywialny algorytm (3.7),
  - każdy **weryfikator** — że blokuje/przepuszcza zapis zgodnie z poziomem ważności (3.6),
  - każda **logika w transakcji serwerowej** — poprawność przy zależności międzystanowiskowej (3.8).
- **Testy funkcjonalne** — scenariusze pokrywające ścieżki z sekcji 1.6 (kroki, dane wejściowe, oczekiwany rezultat).
- **Testy współpracy** — integracja z innymi modułami platformy i systemami zewnętrznymi.
- **Testy wydajnościowe** — weryfikacja założeń z sekcji 2.8 (wolumeny, czasy odpowiedzi).
- **Przypadki brzegowe** — puste dane, maksymalne wolumeny, równoczesna edycja, brak uprawnień.

Zestaw testów zapisz jako listę: *element logiki → scenariusz testu → oczekiwany rezultat*, tak aby pokrycie było widoczne (żaden worker/algorytm/weryfikator/proces serwerowy bez testu).

### 3.14. Dane konfiguracyjne inicjujące bazę (dbinit)
Określ, czy moduł potrzebuje danych konfiguracyjnych wczytywanych do bazy automatycznie
(przy tworzeniu bazy i przy konwersji do nowszej wersji) — **o ile takie dane w ogóle są
potrzebne**; jeśli nie, odnotuj to jawnie i pomiń sekcję.

Typowe kandydaci: słowniki i wartości domyślne, definicje dokumentów z numeracją, definicje
list/cech, role i uprawnienia, ustawienia startowe modułu. Dla każdego zestawu danych określ:
- **obiekty i rekordy** do zainicjowania (nazwy obiektów biznesowych, kluczowe pola),
- **stałe GUID-y** rekordów (nadane raz, niezmienne między wydaniami),
- **kolejność wczytywania** względem innych zestawów (zależności, np. słownik przed danymi,
  które się do niego odwołują → `priority`),
- **wersję wprowadzenia** każdego rekordu (→ `dbversion`; także przyszłe konwersje ustawień).

Dane te trafią do plików **`*.dbinit.xml`** osadzonych w projekcie jako **EmbeddedResource**
— strukturę i reguły plików opisuje artykuł [import-export-xml](../../config/references/import-export-xml.md),
a osadzanie w projekcie skill [programming](../../programming/SKILL.md) (SDK osadza `*.dbinit.xml` automatycznie).
W TODO planuje się z tej sekcji budowę i przetestowanie tych plików.

Nie myl z sekcją 3.15: dane demonstracyjne trafiają tylko do bazy Demo, dane inicjujące —
do **każdej** bazy z zainstalowanym dodatkiem.

### 3.15. Dane demonstracyjne
- Dane do bazy Demo — reprezentatywne scenariusze pokazujące możliwości modułu.
- Dane do testów — zestawy pokrywające przypadki typowe i brzegowe.

### 3.16. Słownik terminów
Definicje kluczowych terminów biznesowych i technicznych, szczególnie przy modułach domenowych (kontroling, logistyka), gdzie terminologia bywa niejednoznaczna lub branżowa.
