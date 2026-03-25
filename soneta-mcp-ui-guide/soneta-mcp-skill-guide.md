# Skill zarządzania programem Soneta (enova365) przez MCP

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


## Foldery w programie

| Intencja użytkownika | programFolder |
|---|---|
| terminarz, zadania do wykonania | `Terminarz` |
| zarządzanie dokumentami, DMS | `DMS` |
| procesy workflow | `Workflow` |

## 4. Typowe scenariusze krok po kroku

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

## 5. Zasady bezpieczeństwa


- **Edytuj tylko pola** oznaczone jako `edytowany` — nie próbuj wymuszać zmian na polach tylko do odczytu
- **Nie zakładaj filterID filtrów** — odczytaj je z odpowiedzi `retrieve_list`
- **Nie zakładaj fieldID zmienianych pól** — odczytaj je z odpowiedzi `open_form`, `switch_form_page`, `add_object`
- **Edytuj tylko pola na aktualnej zakładce** — odczytaj je z odpowiedzi `open_form`, `switch_form_page`, `add_object`. Żeby zmienić pole na innej zakładce użyj `switch_form_page`
- **Stronicowanie** — używaj `pageNumber` do iteracji po dużych listach, nie próbuj pobrać wszystkiego naraz

## 6. Obsługa błędów

| Problem | Rozwiązanie |
|---|---|
| `navigate_to_folder` zwraca błąd | Sprawdź ścieżkę przez `get_folders` — ścieżka może być nieprawidłowa |
| Lista jest pusta | Zasugeruj zmianę lub usunięcie filtrów |
| Pole nie jest `edytowany` | Poinformuj użytkownika — pole jest tylko do odczytu |
| Brak `objectID` | Najpierw wykonaj `retrieve_list` aby uzyskać identyfikatory |
| Brak `pageID` | Najpierw wykonaj `open_form` aby uzyskać listę zakładek |
| Nieznany folder | Użyj `get_modules` → `get_folders` do eksploracji struktury |

## 7. Parametr navigate_to_folder — newTab

- `newTab: true` — otwiera folder w nowej karcie (nie zamyka aktualnego widoku)
- `newTab: false` (domyślnie) — zastępuje aktualny widok
- Używaj `newTab: true` gdy użytkownik chce porównać dane z dwóch folderów
