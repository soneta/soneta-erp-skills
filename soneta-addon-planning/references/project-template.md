# Szablon planu projektu dodatku enova365

Poniżej znajduje się kompletny szablon dokumentu planu projektu. Sekcje oznaczone `[...]` należy wypełnić zgodnie z wymaganiami projektu.

---

# Plan projektu: [Nazwa dodatku]

**Wersja:** 1.0  
**Data:** [Data utworzenia]  
**Autor:** [Imię i nazwisko]

---

## 1. Założenia projektu

### 1.1. Cel biznesowy

[Krótki opis problemu biznesowego, który dodatek ma rozwiązać. 2-3 zdania.]

### 1.2. Zakres funkcjonalny

[Lista głównych funkcjonalności dodatku:]
- [Funkcjonalność 1]
- [Funkcjonalność 2]
- [Funkcjonalność 3]

### 1.3. Elementy konfigurowalne

Elementy, które będą dostosowywane podczas wdrożenia u klienta:

| Element | Opis | Sposób konfiguracji |
|---------|------|---------------------|
| [Nazwa elementu] | [Co konfigurujemy] | [Słownik / Parametr / Definicja] |

**Przykłady elementów konfigurowalnych:**
- Słowniki (statusy, typy, kategorie)
- Definicje dokumentów (numeracja, pola wymagane)
- Parametry algorytmów (stawki, progi, limity)
- Szablony wydruków
- Uprawnienia i role

### 1.4. Integracje z modułami enova365

| Moduł | Typ integracji | Opis |
|-------|----------------|------|
| [Nazwa modułu] | [Odczyt / Zapis / Relacja] | [Krótki opis] |

**Typowe integracje:**
- **Handel** - dokumenty handlowe, towary, kontrahenci
- **CRM** - kontrahenci, osoby kontaktowe
- **Kadry** - pracownicy, struktury organizacyjne
- **Księgowość** - ewidencje, rozrachunki

### 1.5. Ograniczenia i wymagania niefunkcjonalne

- [Ograniczenie 1]
- [Wymaganie wydajnościowe]
- [Wymaganie bezpieczeństwa]

---

## 2. Model danych

### 2.1. Tabele operacyjne

Tabele przechowujące dane wprowadzane podczas codziennej pracy.

#### [Nazwa tabeli 1] (guided="Root")

**Opis:** [Krótki opis przeznaczenia tabeli]

| Pole | Typ | Wymagane | Opis |
|------|-----|----------|------|
| [Nazwa pola] | [Typ danych] | [Tak/Nie] | [Krótki opis] |

**Klucze:**
- `Wg[NazwaPola]` - [opis przeznaczenia klucza]

**Relacje:**
- → [Tabela docelowa] (przez pole [Nazwa pola])

#### [Nazwa tabeli szczegółów] (relguided="inner")

**Opis:** [Pozycje/szczegóły dla tabeli głównej]

| Pole | Typ | Wymagane | Opis |
|------|-----|----------|------|
| [Tabela główna] | [Relacja] | Tak | Relacja do obiektu głównego |
| [Pole szczegółu] | [Typ] | [Tak/Nie] | [Opis] |

### 2.2. Tabele konfiguracyjne

Tabele zawierające dane konfiguracyjne tworzone podczas wdrożenia (config="true").

#### [Nazwa słownika] (config="true")

**Opis:** [Słownik/definicja dla...]

| Pole | Typ | Wymagane | Opis |
|------|-----|----------|------|
| Kod | string | Tak | Unikalny kod elementu |
| Nazwa | string | Tak | Wyświetlana nazwa |
| Blokada | boolean | Nie | Blokuje wyświetlanie na listach wyboru |

**Wartości początkowe:**
- [Wartość 1]
- [Wartość 2]

### 2.3. Diagram relacji

```
┌─────────────────┐     ┌─────────────────┐
│  [Tabela główna]│────<│ [Tabela szczeg.]│
│                 │     │                 │
│ - Pole1         │     │ - TabelaGłówna  │
│ - Pole2         │     │ - PoleSzczegółu │
└────────┬────────┘     └─────────────────┘
         │
         │ relacja
         ▼
┌─────────────────┐
│ [Tabela słown.] │
│   (config)      │
└─────────────────┘

Legenda:
────< relacja 1:N (inner)
────  relacja N:1 (lookup)
```

---

## 3. Struktura menu modułu

### 3.1. Menu główne modułu

```
[Nazwa modułu]
├── [Grupa 1]
│   ├── [Lista 1.1] - [krótki opis]
│   └── [Lista 1.2] - [krótki opis]
├── [Grupa 2]
│   ├── [Lista 2.1] - [krótki opis]
│   └── [Lista 2.2] - [krótki opis]
└── Konfiguracja
    ├── [Słownik 1]
    └── [Słownik 2]
```

---

## 4. Listy

### 4.1. [Nazwa listy]

**Tabela źródłowa:** [Nazwa tabeli]  
**Przeznaczenie:** [Krótki opis co lista pokazuje i dla kogo]

#### Filtry

| Filtr | Typ | Opis |
|-------|-----|------|
| [Nazwa filtra] | [Pole / Predefiniowany / Zakres dat] | [Opis działania] |

**Filtry predefiniowane:**
- [Nazwa filtra] - [Warunek filtrowania]

#### Kolumny

| Kolumna | Źródło | Opis |
|---------|--------|------|
| [Nagłówek kolumny] | [Nazwa pola lub wyrażenie] | [Opis zawartości] |

#### Czynności (Workery)

| Czynność | Opis |
|----------|------|
| [Nazwa czynności] | [Krótki opis działania - 1-2 zdania] |

#### Raporty i wydruki

| Raport | Opis |
|--------|------|
| [Nazwa raportu] | [Krótki opis zawartości - 1-2 zdania] |

---

## 5. Formularze

### 5.1. Formularz: [Nazwa obiektu]

**Tabela:** [Nazwa tabeli]  
**Przeznaczenie:** [Edycja/Podgląd obiektu typu...]

#### Zakładki i pola

##### Zakładka: [Nazwa zakładki 1]

**Przeznaczenie:** [Co zawiera ta zakładka]

| Grupa | Pola |
|-------|------|
| [Nazwa grupy] | [Pole1], [Pole2], [Pole3] |
| [Nazwa grupy 2] | [Pole4], [Pole5] |

##### Zakładka: [Nazwa zakładki 2]

**Przeznaczenie:** [Co zawiera ta zakładka]

| Grupa | Pola |
|-------|------|
| [Nazwa grupy] | [Pole1], [Pole2] |

#### Listy szczegółów (Sublists)

##### Lista: [Nazwa listy szczegółów]

**Tabela:** [Tabela szczegółów]  
**Relacja:** [Pole relacji do obiektu głównego]

| Kolumna | Opis |
|---------|------|
| [Nazwa kolumny] | [Opis] |

**Filtry listy szczegółów:**
- [Nazwa filtra] - [Opis]

#### Czynności (Workery)

| Czynność | Kontekst | Opis |
|----------|----------|------|
| [Nazwa] | [Pojedynczy obiekt / Zaznaczone / Bez kontekstu] | [Opis] |

#### Raporty i wydruki

| Raport | Kontekst | Opis |
|--------|----------|------|
| [Nazwa] | [Pojedynczy / Lista] | [Opis] |

---

## 6. Słowniki i konfiguracja

### 6.1. Słowniki

| Słownik | Przeznaczenie | Pola konfiguracyjne |
|---------|---------------|---------------------|
| [Nazwa] | [Do czego służy] | [Jakie dodatkowe pola poza Kod/Nazwa] |

### 6.2. Parametry konfiguracyjne modułu

| Parametr | Typ | Domyślnie | Opis |
|----------|-----|-----------|------|
| [Nazwa parametru] | [Typ] | [Wartość] | [Co parametr kontroluje] |

### 6.3. Definicje obiektów

| Definicja | Przeznaczenie | Elementy definiowalene |
|-----------|---------------|------------------------|
| [Nazwa] | [Typ obiektów, które definiuje] | [Co można skonfigurować w definicji] |

---

## 7. Uprawnienia

### 7.1. Role użytkowników

| Rola | Opis | Typowe uprawnienia |
|------|------|-------------------|
| [Nazwa roli] | [Kto to jest] | [Ogólny zakres uprawnień] |

### 7.2. Prawa dostępu

| Obiekt/Funkcja | [Rola 1] | [Rola 2] | [Rola 3] |
|----------------|----------|----------|----------|
| [Lista/Formularz/Worker] | [Pełny/Odczyt/Brak] | [...] | [...] |

---

## 8. Załączniki

### 8.1. Słownik terminów

| Termin | Definicja |
|--------|-----------|
| [Termin biznesowy] | [Wyjaśnienie w kontekście dodatku] |

### 8.2. Otwarte kwestie

| Nr | Kwestia | Status | Decyzja |
|----|---------|--------|---------|
| 1 | [Opis problemu do rozstrzygnięcia] | [Otwarta/Zamknięta] | [Podjęta decyzja] |

---

**Koniec dokumentu**
