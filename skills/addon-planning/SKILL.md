---
name: addon-planning
description: >
  Planowanie projektów dodatków dla platformy Soneta (enova365, Triva) — interaktywny,
  etapowy proces od wizji biznesowej po specyfikację implementacyjną, zasilającą skille
  business-xml, form-xml i programming. Używaj, gdy użytkownik
  chce zaplanować nowy moduł lub dodatek — partnerski albo standardowy moduł platformy
  tworzony przez zespół Soneta (założenia, specyfikacja funkcjonalna,
  struktura danych i interfejsu użytkownika).
---

# Planowanie projektu modułu/dodatku Soneta

Skill prowadzi **interaktywny** proces planowania nowego modułu dla platformy Soneta (programy enova365 i Triva — oparte na tej samej platformie technologicznej). Efektem jest dokumentacja projektowa, która w kolejnych krokach zasila skille [business-xml](../business-xml/SKILL.md) (model danych), [form-xml](../form-xml/SKILL.md) (formularze) i [programming](../programming/SKILL.md) (logika).

Ten plik jest mapą procesu — po kroku wstępnym (Etap 0) następują trzy etapy o rosnącym poziomie szczegółowości. Szczegółowe specyfikacje sekcji każdego etapu są w plikach `references/`, które czytasz dopiero, gdy dochodzisz do danego etapu.

| Etap | Zakres | Odbiorca | Szczegóły |
|------|--------|----------|-----------|
| **0. Przygotowanie** | nazwa firmy, repozytorium Git | — (krok wstępny) | `references/etap-0-przygotowanie.md` |
| **1. Wizja i kontekst biznesowy** | co, dla kogo, dlaczego | decydenci, marketing, sprzedaż | `references/etap-1-wizja.md` |
| **2. Architektura modułu** | jak — role, dane, UI, integracje | zespół projektowy | `references/etap-2-architektura.md` |
| **3. Specyfikacja szczegółowa** | szczegóły implementacyjne obiektów | zespół implementacyjny i AI | `references/etap-3-specyfikacja.md` |

Na końcu, po zamknięciu etapów, generujesz dokument **TODO** z kolejnymi krokami implementacji.

Etapy 1–3 osadzają plan w istniejącym modelu danych przez **inwentaryzację** narzędziami `scan-modules` i `scan-folders` — wymagania środowiska, uruchomienie i użycie planistyczne opisuje `references/dane-referencyjne.md`. Przeczytaj go przy pierwszym sięgnięciu po inwentaryzację (najpóźniej w sekcji 1.5 Etapu 1); tam też jest reguła postępowania, gdy środowiska brak.

## Jak prowadzić rozmowę

Proces jest interaktywny. **Nie generuj całego dokumentu naraz** — pracuj etap po etapie:

0. **Zacznij od Etapu 0** — krok wstępny niezależny od treści modułu (`references/etap-0-przygotowanie.md`): ustal nazwę firmy i zadbaj o repozytorium Git, zanim przejdziesz do Etapu 1.
1. **Wczytaj etap** — gdy zaczynasz Etap N, przeczytaj `references/etap-N-*.md`. Plik zawiera pytania do zadania oraz szczegółową specyfikację sekcji tego etapu.
2. **Zbierz informacje** — zacznij od ogólnej idei modułu (co chce osiągnąć, dla kogo, jaki problem rozwiązuje), potem doprecyzowuj pytaniami z pliku etapu.
3. **Opracuj etap** — na podstawie odpowiedzi wygeneruj dokument etapu. Etap jest opracowany dopiero, gdy **każda sekcja z pliku etapu jest wypełniona albo jawnie odnotowana jako otwarta kwestia** — gdy czegoś brakuje, zadaj konkretne pytanie zamiast zgadywać.
4. **Poczekaj na zatwierdzenie** — przedstaw dokument i poczekaj na akceptację/uwagi. Nie przechodź dalej bez wyraźnej zgody.
5. **Iteruj i przejdź dalej** — po poprawkach i akceptacji przejdź do kolejnego etapu.

Prowadź rozmowę po polsku. Zadawaj pytania grupami tematycznymi (2–4 na raz) — nie zasypuj użytkownika listą 15 pytań naraz — i dostosowuj kolejne pytania do odpowiedzi.

## Zapis dokumentów

Każdy etap zapisz jako osobny plik Markdown w katalogu roboczym projektu (domyślnie podkatalog nazwany po module, np. `plan-<nazwa-modułu>/`), chyba że użytkownik wskaże inne miejsce:

- `etap-1-wizja.md`, `etap-2-architektura.md`, `etap-3-specyfikacja.md`
- `otwarte-kwestie.md` — prowadzona przez cały proces
- `todo.md` — na końcu

Przed utworzeniem katalogu upewnij się z użytkownikiem co do nazwy modułu i lokalizacji.

## Otwarte kwestie

Otwarte kwestie to **jedno wspólne miejsce, w którym zbierane są wszystkie decyzje projektowe pozostające do podjęcia** — użytkownik ma w każdym momencie jasny obraz tego, co blokuje przejście dalej. Prowadź tę listę przez cały proces.

### Co trafia na listę

Dopisuj nową kwestię zawsze, gdy:
- pojawia się pytanie projektowe, na które nie znasz odpowiedzi — **zamiast zgadywać, zapisz je jako otwartą kwestię**,
- istnieje kilka alternatyw i wybór należy do użytkownika (np. wariant struktury danych, sposób integracji),
- brakuje informacji od osoby trzeciej (klient, inny zespół, dział prawny, licencjonowanie),
- decyzję świadomie odkładasz „na później", aby nie blokować bieżącego etapu,
- użytkownik mówi „zastanowię się", „dopytam", „nie wiem jeszcze" — to sygnał do założenia wpisu.

Nie zostawiaj nierozstrzygniętych założeń ukrytych w treści dokumentu — każde takie miejsce powinno mieć odpowiadający wpis na liście.

### Struktura tabeli

| Nr | Etap | Obszar | Kwestia | Wpływ | Blokująca | Status | Decyzja i uzasadnienie | Data |
|----|------|--------|---------|-------|-----------|--------|------------------------|------|
| 1 | 2 | Dane | [Opis problemu do rozstrzygnięcia] | Wysoki/Średni/Niski | Tak/Nie | Otwarta / W trakcie / Zamknięta | [Podjęta decyzja + dlaczego] | RRRR-MM-DD |

- **Obszar** — czego dotyczy (dane, UI, integracje, uprawnienia, wydajność, licencje, proces…).
- **Wpływ** — jak duże są konsekwencje decyzji dla projektu.
- **Blokująca** — czy kwestia uniemożliwia zatwierdzenie etapu lub rozpoczęcie implementacji.
- **Status** — cykl życia: *Otwarta* → *W trakcie* → *Zamknięta*.
- **Decyzja i uzasadnienie** — przy zamknięciu wpisz nie tylko *co* postanowiono, ale i *dlaczego*. Nigdy nie usuwaj zamkniętych kwestii — stanowią historię decyzji.

### Zasady prowadzenia

1. **Numeracja jest stała** — raz nadany numer nie zmienia się; zamknięte pozycje zostają na liście.
2. **Aktualizuj na bieżąco** — gdy zapadnie decyzja, od razu zmień status na *Zamknięta* i uzupełnij kolumnę decyzji.
3. **Prezentuj na końcu każdego etapu** — pokaż pełną listę i wyraźnie wskaż kwestie otwarte oraz **blokujące**.
4. **Bramka jakości** — nie przechodź do kolejnego etapu z otwartymi kwestiami *blokującymi* dla tego etapu. Kwestie nieblokujące można przenieść dalej, ale muszą pozostać widoczne.
5. **Powiązanie z TODO** — „Zamknięcie otwartych kwestii" jest pozycją TODO; do implementacji nie wchodzimy z otwartymi kwestiami blokującymi.

## Lokalizacja projektów kodu (etap implementacji)

Dokumenty planistyczne (etapy, otwarte kwestie, TODO) trzymasz w podkatalogu planu — patrz „Zapis dokumentów". Natomiast gdy proces dojdzie do **budowania folderów projektów** (rusztowanie solucji dodatku — patrz [new-addon-cli.md](../programming/references/new-addon-cli.md)), twórz je **bezpośrednio w bieżącym katalogu roboczym**: plik solucji (`.sln`) oraz projekty (`Firma.NazwaModulu`, `Firma.NazwaModulu.UI`, `Firma.NazwaModulu.Tests`) mają leżeć w katalogu, w którym pracujesz, a nie w zagnieżdżonym podfolderze. Dzięki temu repozytorium Git założone w Etapie 0 obejmuje solucję od razu. Nazwy projektów budujesz z ustalonej w Etapie 0 nazwy firmy.

## Dokument TODO

Po zamknięciu wszystkich etapów wygeneruj dokument TODO z kolejnymi krokami:

### Uzupełnienie planu
- [ ] Weryfikacja i zatwierdzenie Etapu 1 przez interesariuszy
- [ ] Weryfikacja i zatwierdzenie Etapu 2 przez zespół projektowy
- [ ] Uzupełnienie specyfikacji szczegółowej (Etap 3) dla wszystkich obiektów
- [ ] Zamknięcie otwartych kwestii

### Implementacja
- [ ] Rusztowanie solucji w bieżącym katalogu (projekty `Firma.NazwaModulu*`, → [new-addon-cli.md](../programming/references/new-addon-cli.md))
- [ ] Model danych — tabele, pola, relacje
- [ ] Plik business.xml (→ skill [business-xml](../business-xml/SKILL.md))
- [ ] Struktura menu, listy, widoki
- [ ] Formularze i zakładki (→ skill [form-xml](../form-xml/SKILL.md))
- [ ] Konfiguracja — słowniki, definicje, ustawienia
- [ ] Dane konfiguracyjne inicjujące bazę (sekcja 3.14) — pliki `*.dbinit.xml` w projekcie jako EmbeddedResource; budowa pliku → [import-export-xml](../config/references/import-export-xml.md), osadzenie → [new-addon-cli](../programming/references/new-addon-cli.md), test wczytania przez `dbmgr importxml` → [tools](../tools/SKILL.md)
- [ ] Weryfikatory — walidacja danych wprowadzanych przez operatora (→ [verifiers.md](../programming/references/verifiers.md))
- [ ] Workery i czynności (→ [worker-extender.md](../programming/references/worker-extender.md))
- [ ] Algorytmy w transakcji serwerowej — logika zależna od równoległej pracy stanowisk (→ [events.md](../programming/references/events.md))
- [ ] Raporty i wydruki
- [ ] Procesy Workflow
- [ ] Wskaźniki i wykresy BI
- [ ] Uprawnienia i role
- [ ] Integracje z innymi systemami
- [ ] Baza Demo i dane demonstracyjne
- [ ] **Testy integracyjne** — dla każdego workera, każdego algorytmu obiektu biznesowego, każdego weryfikatora i każdej logiki w transakcji serwerowej (→ [integration-tests.md](../programming/references/integration-tests.md))
- [ ] Dokumentacja użytkownika i techniczna

## Powiązanie z innymi skillami

Już podczas planowania korzystasz z narzędzi inwentaryzacyjnych: [scan-modules](../programming/references/scan-modules.md) i [scan-props](../programming/references/scan-props.md) oraz [scan-folders](../config/references/scan-folders.md) — łącznie odwzorowują istniejący model danych i menu platformy (patrz `references/dane-referencyjne.md`).

Po zatwierdzeniu planu projektu:
1. **[business-xml](../business-xml/SKILL.md)** — generowanie pliku business.xml na podstawie modelu danych z Etapu 3 (sekcje 3.1–3.3).
2. **[form-xml](../form-xml/SKILL.md)** — generowanie formularzy i widoków UI na podstawie sekcji 3.4 i 3.5.
3. **[programming](../programming/SKILL.md)** — implementacja logiki biznesowej i testów; dokumenty właściwe dla poszczególnych obszarów Etapu 3 wskazują strzałki (→) przy pozycjach dokumentu TODO powyżej.
