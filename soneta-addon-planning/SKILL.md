---
name: soneta-addon-planning
description: >
  Planowanie projektów dodatków dla platformy enova365/Soneta Enterprise. Tworzy 
  kompletną dokumentację projektową obejmującą: strukturę danych (tabele, relacje), 
  elementy konfigurowalne, definicje list i menu, formularze, workery i raporty.
  Używaj gdy użytkownik prosi o zaplanowanie nowego modułu/dodatku enova365, 
  przygotowanie założeń projektu, stworzenie specyfikacji funkcjonalnej dodatku,
  lub zdefiniowanie struktury danych i interfejsu użytkownika dla nowego modułu.
---

# Soneta Addon Planning

Skill do tworzenia planów projektów dodatków dla platformy enova365. Plan projektu stanowi podstawę do dalszych prac implementacyjnych z wykorzystaniem skilli `enova365-business-xml` i `soneta-programming-basics`.

## Struktura planu projektu

Plan projektu dodatku enova365 składa się z następujących sekcji:

### 1. Założenia projektu
- Cel biznesowy dodatku
- Zakres funkcjonalny (co dodatek ma robić)
- Elementy konfigurowalne na etapie wdrożenia
- Integracje z istniejącymi modułami enova365
- Ograniczenia i wymagania niefunkcjonalne

### 2. Model danych
- Lista tabel z podziałem na operacyjne i konfiguracyjne
- Pola każdej tabeli (nazwa, typ, wymagalność, opis)
- Relacje między tabelami (diagram lub lista)
- Klucze i indeksy

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

## Workflow tworzenia planu

```
1. Zebranie wymagań
   ↓
2. Zdefiniowanie założeń i elementów konfigurowalnych
   ↓
3. Zaprojektowanie modelu danych
   ↓
4. Określenie struktury menu
   ↓
5. Zdefiniowanie list (filtry, kolumny, akcje)
   ↓
6. Zdefiniowanie formularzy (zakładki, pola, sublists)
   ↓
7. Określenie słowników i konfiguracji
   ↓
8. Zdefiniowanie uprawnień
```

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

## Konwencje nazewnicze

| Element | Konwencja | Przykład |
|---------|-----------|----------|
| Tabela operacyjna | PascalCase, l.poj. | `Zlecenie`, `PozycjaZlecenia` |
| Tabela konfiguracyjna | PascalCase, l.poj. | `DefinicjaZlecenia`, `StatusZlecenia` |
| Worker | PascalCase + Worker | `ZatwierdzZlecenieWorker` |
| Raport | PascalCase | `ZestawienieZlecen`, `KartaZlecenia` |
| Lista | l.mn. lub opis | `Zlecenia`, `ZleceniaDoRealizacji` |

## Poziom szczegółowości

Plan projektu zawiera **ogólne opisy** elementów:
- Nazwy i krótkie opisy (1-2 zdania)
- Typy danych bez szczegółów implementacyjnych
- Logiczne grupowanie bez dokładnych pozycji

**Szczegóły doprecyzowywane w kolejnych etapach:**
- Dokładne atrybuty kolumn (długość, walidacje)
- Implementacja workerów (algorytmy, kroki)
- Szablony raportów (układ, pola)
- Warunki filtrów (wyrażenia, wartości domyślne)

## Powiązanie z innymi skillami

Po zatwierdzeniu planu projektu:
1. **enova365-business-xml** - generowanie pliku business.xml na podstawie modelu danych
2. **soneta-programming-basics** - implementacja workerów i logiki biznesowej

## Szczegółowa dokumentacja

- **[references/project-template.md](references/project-template.md)** - pełny szablon dokumentu planu projektu
- **[references/checklist.md](references/checklist.md)** - lista kontrolna kompletności planu
