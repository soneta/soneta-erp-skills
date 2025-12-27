# Lista kontrolna planu projektu dodatku enova365

Lista kontrolna do weryfikacji kompletności planu projektu przed rozpoczęciem implementacji.

## 1. Założenia projektu

- [ ] Zdefiniowany cel biznesowy (problem do rozwiązania)
- [ ] Określony zakres funkcjonalny (lista funkcji)
- [ ] Wymienione elementy konfigurowalne z opisem sposobu konfiguracji
- [ ] Zidentyfikowane integracje z istniejącymi modułami enova365
- [ ] Opisane ograniczenia i wymagania niefunkcjonalne

## 2. Model danych

### Tabele

- [ ] Każda tabela ma określone:
  - [ ] Nazwę (PascalCase, l.poj.)
  - [ ] Typ (operacyjna / konfiguracyjna)
  - [ ] Atrybut guided (Root / Exported / inner / brak)
  - [ ] Krótki opis przeznaczenia

### Pola

- [ ] Każde pole ma określone:
  - [ ] Nazwę (PascalCase)
  - [ ] Typ danych (string, int, date, relacja, enum itp.)
  - [ ] Wymagalność (Tak/Nie)
  - [ ] Krótki opis

### Relacje

- [ ] Wszystkie relacje między tabelami są opisane
- [ ] Tabele szczegółów mają wskazaną tabelę główną (relguided="inner")
- [ ] Relacje do modułów zewnętrznych są zidentyfikowane
- [ ] Istnieje diagram relacji lub lista powiązań

### Klucze

- [ ] Zdefiniowane klucze unikalne (keyprimary, keyunique)
- [ ] Określone klucze wyszukiwania (indeksy)

## 3. Struktura menu

- [ ] Zdefiniowana hierarchia menu modułu
- [ ] Listy pogrupowane logicznie
- [ ] Konfiguracja wydzielona do osobnej grupy

## 4. Listy

Dla każdej listy:

- [ ] Określona tabela źródłowa
- [ ] Opisane przeznaczenie listy
- [ ] Zdefiniowane filtry:
  - [ ] Filtry polowe (wyszukiwanie po polach)
  - [ ] Filtry predefiniowane (najczęstsze scenariusze)
  - [ ] Filtry zakresowe (daty, wartości)
- [ ] Określone kolumny z kolejnością
- [ ] Lista czynności (workerów) z krótkim opisem działania
- [ ] Lista raportów/wydruków z krótkim opisem zawartości

## 5. Formularze

Dla każdego formularza:

- [ ] Określona tabela źródłowa
- [ ] Zdefiniowane zakładki:
  - [ ] Nazwa zakładki
  - [ ] Przeznaczenie (co grupuje)
- [ ] Pola przypisane do zakładek i grup
- [ ] Zdefiniowane listy szczegółów (sublists):
  - [ ] Tabela szczegółów
  - [ ] Kolumny na liście
  - [ ] Ewentualne filtry
- [ ] Lista czynności z kontekstem (pojedynczy/zaznaczone/bez kontekstu)
- [ ] Lista raportów z kontekstem

## 6. Słowniki i konfiguracja

- [ ] Wszystkie tabele konfiguracyjne zidentyfikowane
- [ ] Określone wartości początkowe słowników
- [ ] Zdefiniowane parametry konfiguracyjne modułu
- [ ] Opisane definicje obiektów (jeśli występują)

## 7. Uprawnienia

- [ ] Zdefiniowane role użytkowników
- [ ] Przypisane prawa dostępu do:
  - [ ] List
  - [ ] Formularzy
  - [ ] Workerów
  - [ ] Raportów

## 8. Kompletność ogólna

- [ ] Wszystkie nazwy zgodne z konwencjami enova365
- [ ] Brak duplikatów nazw tabel/pól
- [ ] Relacje nie tworzą cykli (poza świadomymi wyjątkami)
- [ ] Każdy worker i raport ma krótki opis
- [ ] Otwarte kwestie są udokumentowane

## 9. Gotowość do implementacji

Po pozytywnej weryfikacji listy kontrolnej można przystąpić do:

1. **Generowania business.xml** (skill: enova365-business-xml)
   - Definicje tabel i relacji
   - Klucze i indeksy
   - Enumy i interfejsy

2. **Implementacji logiki** (skill: soneta-programming-basics)
   - Workery i ich algorytmy
   - Walidacje biznesowe
   - Raporty i wydruki

---

## Typowe braki do uzupełnienia

| Sekcja | Typowy brak | Jak uzupełnić |
|--------|-------------|---------------|
| Model danych | Brak tabeli historii | Dodać tabelę z okresami (FromTo) |
| Model danych | Brak tabeli notatek/załączników | Rozważyć użycie standardowego mechanizmu Attachments |
| Listy | Brak filtra po dacie | Dodać filtr zakresowy dla dat |
| Formularze | Brak zakładki "Historia" | Dodać zakładkę z ChangeInfos |
| Konfiguracja | Brak statusów | Dodać tabelę słownikową statusów |
| Uprawnienia | Brak roli administratora | Dodać rolę z pełnymi uprawnieniami |

## Pytania kontrolne

1. Czy użytkownik może wykonać wszystkie operacje biznesowe opisane w zakresie?
2. Czy dane wprowadzone przez użytkownika mogą być później wyszukane i zmodyfikowane?
3. Czy administrator może skonfigurować wszystkie elementy konfigurowalne?
4. Czy można wygenerować wszystkie potrzebne raporty?
5. Czy uprawnienia pozwalają na bezpieczne rozdzielenie obowiązków?
