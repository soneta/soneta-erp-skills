# Dodatkowe elementy projektu


## Wyszczególnienie użycia istniejących tabel z systemu platformy Soneta (enova365, Triva) które będą wykorzystywane przez moduł.
Lista wszystkich tabel z systemu platformy Soneta podzielonych na moduły znajduje się w załączniku ExistingTables.md.
Jeżeli moduł budowany jest przez zespół firmy Soneta, to należy określić ewentualne zmiany w tabelach z systemu platformy Soneta.

### 3. Struktura menu modułu
- Hierarchia list w menu głównym
- Grupowanie funkcjonalne

### 4. Definicje list
Dla każdej listy:
- Filtry (pola filtrujące, filtry predefiniowane)
- Kolumny (kolejność, szerokość, formatowanie)
- Czynności (workery) - nazwa i krótki opis
- Raporty/wydruki - nazwa i krótki opis

### 5. Definicje formularzy
Dla każdego formularza obiektu:
- Zakładki (grupowanie logiczne)
- Pola na zakładkach (pogrupowane)
- Listy szczegółów (sublists) z kolumnami
- Czynności (workery) dostępne z formularza
- Raporty/wydruki dostępne z formularza

### 6. Słowniki i konfiguracja
- Tabele słownikowe (config=true)
- Wartości domyślne
- Parametry konfiguracyjne modułu

### 7. Uprawnienia
- Role użytkowników
- Prawa dostępu do obiektów i funkcji




## Format dokumentu planu

Plan projektu generowany jest jako dokument Markdown z następującą strukturą:

```markdown
# Plan projektu: [Nazwa dodatku]

## 1. Założenia projektu
### 1.1. Cel biznesowy
### 1.2. Zakres funkcjonalny
### 1.3. Elementy konfigurowalne
### 1.4. Integracje
### 1.5. Ograniczenia

## 2. Model danych
### 2.1. Tabele operacyjne
### 2.2. Tabele konfiguracyjne
### 2.3. Diagram relacji

## 3. Struktura menu
### 3.1. Menu główne modułu

## 4. Listy
### 4.1. [Nazwa listy]
#### Filtry
#### Kolumny
#### Czynności
#### Raporty

## 5. Formularze
### 5.1. [Nazwa formularza]
#### Zakładki i pola
#### Listy szczegółów
#### Czynności
#### Raporty

## 6. Słowniki i konfiguracja

## 7. Uprawnienia
```

**Szczegóły doprecyzowywane w kolejnych etapach:**
- Dokładne atrybuty kolumn (długość, walidacje)
- Implementacja workerów (algorytmy, kroki)
- Szablony raportów (układ, pola)
- Warunki filtrów (wyrażenia, wartości domyślne)

## Powiązanie z innymi skillami

Po zatwierdzeniu planu projektu:
1. **soneta-business-xml** - generowanie pliku business.xml na podstawie modelu danych
2. **soneta-programming** - implementacja workerów i logiki biznesowej

## Szczegółowa dokumentacja

- **[references/project-template.md](references/project-template.md)** - pełny szablon dokumentu planu projektu
- **[references/checklist.md](references/checklist.md)** - lista kontrolna kompletności planu
