---
name: soneta-mcp-ui-guide
description: >-
  Obsługa programów Soneta (enova365, Triva) przez narzędzia MCP soneta_ui. Nawigacja po modułach,
  przeglądanie list, otwieranie formularzy, edycja danych, dodawanie obiektów. Używaj tego skilla
  ZAWSZE gdy użytkownik: (1) prosi o odczytanie, wyświetlenie lub wyszukanie danych w enova365/Triva
  (np. kontrahenci, faktury, pracownicy, towary, stany magazynowe); (2) chce dodać, edytować lub
  przeglądać obiekty w programie Soneta; (3) pyta o nawigację w modułach enova365 (Handel, Kadry,
  Księgowość, CRM, itp.); (4) wspomina 'otwórz w enova', 'pokaż listę', 'znajdź kontrahenta',
  'dodaj fakturę', 'sprawdź stan magazynu', 'pokaż pracowników'; (5) chce wykonać operację
  na danych ERP przez MCP.
version: "1.0"
---

# Obsługa programów Soneta (enova365 / Triva) przez MCP

Skill umożliwia pełną interakcję z działającą instancją enova365 lub Triva poprzez narzędzia MCP `soneta_ui`. Pozwala przeglądać dane, nawigować po modułach, edytować formularze i dodawać nowe obiekty.

## 1. Przepływ pracy (workflow MCP)

Narzędzia MCP `soneta_ui` są **zależne od siebie** — muszą być wywoływane w określonej sekwencji:

```
get_modules → get_folders → navigate_to_folder → retrieve_list → focus_row / open_form → switch_form_page → update_field_value
                                                                   ↓
                                                              add_object → update_field_value
```

### Zasady sekwencji

- `retrieve_list` wymaga wcześniejszego `navigate_to_folder`
- `open_form(objectID)` wymaga `objectID` z odpowiedzi `retrieve_list`
- `switch_form_page(pageID)` wymaga `pageID` z odpowiedzi `open_form`
- `update_field_value` działa tylko na polach oznaczonych jako `edytowany`
- `add_object` tworzy nowy obiekt w aktualnie nawigowanym folderze
- `focus_row(objectID)` zaznacza wiersz na liście bez otwierania formularza

### Narzędzia eksploracyjne (niezależne)

- `get_modules` — lista modułów (punkt startowy)
- `get_folders(programFolder)` — podfoldery danego modułu/folderu

## 2. Formaty danych (CultureInfo.InvariantCulture)

Wszystkie wartości przesyłane z/do programu muszą używać formatu InvariantCulture:

| Typ | Format | Przykład |
|-----|--------|----------|
| Date | `yyyy-MM-dd` | `2026-03-25` |
| FromTo | `yyyy-MM-dd..yyyy-MM-dd` | `2026-01-01..2026-03-31` |
| Bool | `Tak` / `Nie` | `Tak` |
| Number | kropka dziesiętna | `1234.56` |
| Filter | `["filterID=Wartość"]` | `["_Status=Aktywny", "_DataListy=2026-01-01.."]` |

## 3. Typowe scenariusze krok po kroku

### Scenariusz A — Odczyt danych z listy

```
1. navigate_to_folder(programFolder)     — otwórz folder
2. retrieve_list(filters?, pageNumber?)  — pobierz dane (opcjonalnie z filtrami)
3. Prezentuj wyniki użytkownikowi
```

### Scenariusz B — Podgląd szczegółów obiektu

```
1. navigate_to_folder(programFolder)     — otwórz folder
2. retrieve_list()                       — pobierz listę, znajdź objectID
3. open_form(objectID)                   — otwórz formularz
4. switch_form_page(pageID)              — przełącz zakładkę jeśli potrzeba
```

### Scenariusz C — Edycja istniejącego obiektu

```
1. navigate_to_folder(programFolder)     — otwórz folder
2. retrieve_list()                       — znajdź obiekt
3. open_form(objectID)                   — otwórz formularz
4. switch_form_page(pageID)              — przejdź do zakładki z polem (jeśli trzeba)
5. update_field_value(["fieldID=value"]) — zmień wartość pola oznaczonego 'edytowany'
```

### Scenariusz D — Dodawanie nowego obiektu

```
1. navigate_to_folder(programFolder)     — otwórz odpowiedni folder
2. add_object()                          — utwórz nowy obiekt
3. update_field_value(["fieldID=value"]) — wypełnij wymagane pola
```

### Scenariusz E — Przeglądanie dużej listy (stronicowanie)

```
1. navigate_to_folder(programFolder)
2. retrieve_list(pageNumber=0)           — pierwsza strona
3. retrieve_list(pageNumber=1)           — kolejna strona
4. ... kontynuuj aż dane się wyczerpią
```

### Scenariusz F — Nawigacja eksploracyjna (nieznany folder)

```
1. get_modules()                         — lista modułów
2. get_folders(moduł)                    — foldery w module
3. get_folders(moduł/podfolder)          — dalsze podfoldery (jeśli type=folders)
4. navigate_to_folder(znaleziony_folder) — otwórz właściwy folder
```

## 4. Zasady bezpieczeństwa

- **Edytuj tylko pola** oznaczone jako `edytowany` — nie próbuj wymuszać zmian na polach tylko do odczytu
- **Nie zakładaj filterID filtrów** — odczytaj je z odpowiedzi `retrieve_list`
- **Nie zakładaj fieldID zmienianych pól** — odczytaj je z odpowiedzi `open_form`, `switch_form_page`, `add_object`
- **Edytuj tylko pola na aktualnej zakładce** — żeby zmienić pole na innej zakładce użyj `switch_form_page`
- **Stronicowanie** — używaj `pageNumber` do iteracji po dużych listach, nie próbuj pobrać wszystkiego naraz

## 5. Obsługa błędów

| Problem | Rozwiązanie |
|---|---|
| `navigate_to_folder` zwraca błąd | Sprawdź ścieżkę przez `get_folders` — ścieżka może być nieprawidłowa |
| Lista jest pusta | Zasugeruj zmianę lub usunięcie filtrów |
| Pole nie jest `edytowany` | Poinformuj użytkownika — pole jest tylko do odczytu |
| Brak `objectID` | Najpierw wykonaj `retrieve_list` aby uzyskać identyfikatory |
| Brak `pageID` | Najpierw wykonaj `open_form` aby uzyskać listę zakładek |
| Nieznany folder | Użyj `get_modules` → `get_folders` do eksploracji struktury |

## 6. Parametr navigate_to_folder — newTab

- `newTab: true` — otwiera folder w nowej karcie (nie zamyka aktualnego widoku)
- `newTab: false` (domyślnie) — zastępuje aktualny widok
- Używaj `newTab: true` gdy użytkownik chce porównać dane z dwóch folderów

## 7. Najczęściej używane foldery programu

Poniższa lista zawiera ścieżki folderów (`programFolder`). Używaj ich z `navigate_to_folder`, `retrieve_list`, `open_form`.

### Handel

| programFolder | Opis |
|---|---|
| `Handel/Kartoteki/Towary i usługi` | Kartoteka towarów i usług |
| `Handel/Sprzedaż/Faktury sprzedaży` | Faktury sprzedaży, paragony, korekty |
| `Handel/Sprzedaż/Zamówienia od odbiorców` | Zamówienia złożone przez odbiorców |
| `Handel/Sprzedaż/Oferty do odbiorców` | Dokumenty ofert handlowych |
| `Handel/Zakup/Faktury zakupu` | Faktury zakupu oraz korekty |
| `Handel/Zakup/Zamówienia do dostawców` | Zamówienia złożone u dostawców |
| `Handel/Zakup/Faktury wewnętrzne` | Faktury wewnętrzne z wewnątrzunijnych faktur zakupu |
| `Handel/Magazyn/Dokumenty razem` | Wszystkie dokumenty magazynowe (PZ, WZ, PW, RW, MM) |
| `Handel/Magazyn/Stany magazynowe` | Stany towarów w magazynie |
| `Handel/Magazyn/Zasoby` | Stany i partie zasobów magazynowych |
| `Handel/Magazyn/Dokumenty wg kategorii/Przyjęcia magazynowe` | Dokumenty PZ oraz ich korekty |
| `Handel/Magazyn/Dokumenty wg kategorii/Wydania magazynowe` | Dokumenty WZ oraz ich korekty |
| `Handel/Magazyn/Dokumenty wg kategorii/Przesunięcia magazynowe` | Dokumenty przesunięć MM |
| `Handel/Magazyn/Dokumenty wg kategorii/Inwentaryzacja` | Inwentaryzacja, spis z natury |
| `Handel/Magazyn/Okresy magazynowe` | Okresy magazynowe, zasoby, obroty |
| `Handel/Dokumenty razem i pozostałe/Wszystkie dokumenty` | Wszystkie dokumenty handlowe razem |
| `Handel/Dokumenty razem i pozostałe/Pozycje dokumentów` | Zestawienie pozycji ze wszystkich dokumentów |
| `Handel/Sprzedaż/Ceny i rabaty/Cennik kontrahenta` | Cennik towarów dla kontrahenta |
| `Handel/KSeF/Wysyłanie` | Wysyłanie dokumentów do KSeF |
| `Handel/KSeF/Pobrane` | Pliki faktur pobranych z KSeF |
| `Handel/Zestawienia/Obroty wg towarów` | Obroty magazynowe wg towarów |
| `Handel/Zestawienia/Obroty wg kontrahentów` | Obroty magazynowe wg kontrahentów |
| `Handel/Zestawienia/Sprzedaż i zakupy wg towarów` | Sprzedaż i zakupy wg towarów |
| `Handel/Zestawienia/Sprzedaż i zakupy wg kontrahentów` | Sprzedaż i zakupy wg kontrahentów |

### Kontrahenci i urzędy

| programFolder | Opis |
|---|---|
| `Kontrahenci i urzędy/Kontrahenci` | Kartoteka kontrahentów |
| `Kontrahenci i urzędy/Osoby` | Osoby związane z kontrahentami |
| `Kontrahenci i urzędy/Kontrahenci wg opiekuna` | Kontrahenci wg opiekunów handlowych |
| `Kontrahenci i urzędy/Banki` | Słownik banków |
| `Kontrahenci i urzędy/Urzędy skarbowe` | Słownik urzędów skarbowych |

### Ewidencja Środków Pieniężnych

| programFolder | Opis |
|---|---|
| `Ewidencja Środków Pieniężnych/Raporty` | Raporty zmian w ewidencjach |
| `Ewidencja Środków Pieniężnych/Dokumenty kasowe` | Wpłaty i wypłaty do kasy |
| `Ewidencja Środków Pieniężnych/Przelewy` | Przelewy bankowe |
| `Ewidencja Środków Pieniężnych/Paczki przelewów` | Paczki przelewów do eksportu |
| `Ewidencja Środków Pieniężnych/Rozrachunki wg dokumentów` | Rozrachunki wg dokumentów |
| `Ewidencja Środków Pieniężnych/Rozrachunki wg kontrahentów` | Rozrachunki wg kontrahentów |
| `Ewidencja Środków Pieniężnych/Zobowiązania i należności` | Zobowiązania i należności |
| `Ewidencja Środków Pieniężnych/Wpłaty i wypłaty` | Wpłaty i wypłaty |
| `Ewidencja Środków Pieniężnych/Dokumenty rozliczeniowe/Kompensaty` | Kompensaty |
| `Ewidencja Środków Pieniężnych/Dokumenty rozliczeniowe/Noty odsetkowe` | Noty odsetkowe |
| `Ewidencja Środków Pieniężnych/Dokumenty rozliczeniowe/Wezwania do zapłaty` | Wezwania do zapłaty |
| `Ewidencja Środków Pieniężnych/Dokumenty rozliczeniowe/Rozliczenie delegacji` | Rozliczenie delegacji |
| `Ewidencja Środków Pieniężnych/Sprawy windykacyjne` | Sprawy windykacyjne |

### Księgowość

| programFolder | Opis |
|---|---|
| `Księgowość/Obroty i salda` | Zestawienie zapisów na kontach |
| `Księgowość/Dziennik/Dekrety` | Dziennik wg dekretów |
| `Księgowość/Dziennik/Zapisy` | Dziennik wg zapisów |
| `Księgowość/Plan kont` | Plan kont księgowych |
| `Księgowość/Rozrachunki wg dokumentów` | Rozrachunki wg dokumentów |
| `Księgowość/Rozrachunki wg kontrahentów` | Rozrachunki wg kontrahentów |
| `Księgowość/Rozliczenia księgowe` | Rozliczenia na kontach |
| `Księgowość/Definicje zestawień księgowych` | Bilans, rachunek wyników |
| `Księgowość/Wyniki zestawień` | Wyniki zestawień księgowych |
| `Księgowość/Sprawozdania finansowe` | Sprawozdania finansowe |
| `Księgowość/Deklaracje/CIT-8` | Deklaracja CIT-8 |
| `Księgowość/Deklaracje/Zaliczka PIT skala` | Zaliczka PIT — skala |
| `Księgowość/RMK/Dokumenty RMK` | Rozliczenia międzyokresowe kosztów |
| `Księgowość/Złe długi` | Dokumenty złych długów |

### Ewidencja dokumentów i VAT

| programFolder | Opis |
|---|---|
| `Ewidencja dokumentów/Dokumenty` | Ewidencja dokumentów |
| `Ewidencja dokumentów/Ewidencja dokumentów VAT` | Dokumenty VAT |
| `Ewidencja dokumentów/Rejestr VAT` | Rejestr VAT |
| `Ewidencja dokumentów/Deklaracja VAT-7` | Deklaracja VAT-7 |
| `Ewidencja dokumentów/Deklaracja VAT-UE` | Deklaracja VAT-UE |
| `Ewidencja dokumentów/Jednolite pliki kontrolne` | JPK |
| `Ewidencja dokumentów/eDeklaracje` | Deklaracje elektroniczne |
| `Ewidencja dokumentów/Noty korygujące` | Noty korygujące |
| `Ewidencja dokumentów/Matryce dokumentów` | Szablony dokumentów |

### Kadry i płace

| programFolder | Opis |
|---|---|
| `Kadry i płace/Kadry/Pracownicy` | Kartoteka pracowników |
| `Kadry i płace/Kadry/Zleceniobiorcy` | Kartoteka zleceniobiorców |
| `Kadry i płace/Kadry/Wszyscy` | Wszyscy zatrudnieni |
| `Kadry i płace/Kadry/Umowy` | Umowy cywilnoprawne |
| `Kadry i płace/Kadry/Czas pracy/Nieobecności` | Nieobecności |
| `Kadry i płace/Kadry/Czas pracy/Limity nieobecności` | Limity urlopowe |
| `Kadry i płace/Kadry/Czas pracy/Wnioski o urlopy, delegacje` | Wnioski urlopowe |
| `Kadry i płace/Kadry/Ewidencje/Umowy o pracę` | Umowy o pracę |
| `Kadry i płace/Kadry/Ewidencje/Badania lekarskie` | Badania lekarskie |
| `Kadry i płace/Kadry/Ewidencje/Szkolenia BHP` | Szkolenia BHP |
| `Kadry i płace/Płace/Listy płac` | Listy płac |
| `Kadry i płace/Płace/Wypłaty` | Wypłaty |
| `Kadry i płace/Płace/Elementy wypłaty` | Składniki wypłat |
| `Kadry i płace/Deklaracje ZUS/DRA` | Deklaracje DRA |
| `Kadry i płace/Deklaracje ZUS/Zgłoszeniowe` | Deklaracje zgłoszeniowe |
| `Kadry i płace/Deklaracje PIT/PIT-11` | Deklaracja PIT-11 |
| `Kadry i płace/Deklaracje PIT/PIT-4R` | Deklaracja PIT-4R |
| `Kadry i płace/Dokumenty PPK/Rozliczenie składek` | Rozliczenie składek PPK |

### Księga inwentarzowa

| programFolder | Opis |
|---|---|
| `Księga inwentarzowa/Ewidencja środków trwałych` | Środki trwałe |
| `Księga inwentarzowa/Dokumenty środków trwałych` | Dokumenty środków trwałych |

### CRM

| programFolder | Opis |
|---|---|
| `CRM/Kontrahenci` | Kartoteka kontrahentów CRM |
| `CRM/Zadania` | Zadania CRM |
| `CRM/Moje zadania` | Zadania bieżącego operatora |
| `CRM/Zdarzenia` | Rejestr zdarzeń CRM |
| `CRM/Aktywności` | Aktywności: rozmowy, spotkania, notatki |
| `CRM/Leady` | Leady sprzedażowe |
| `CRM/Transakcje` | Transakcje sprzedażowe |
| `CRM/Projekty` | Projekty CRM |
| `CRM/Kampanie` | Kampanie marketingowe |
| `CRM/Kanban` | Tablica Kanban |
| `CRM/Kalendarz CRM` | Kalendarz z zadaniami i zdarzeniami |

### Pozostałe moduły

| programFolder | Opis |
|---|---|
| `Terminarz` | Zadania do wykonania |
| `DMS` | Zarządzanie dokumentami |
| `Workflow` | Procesy workflow |
