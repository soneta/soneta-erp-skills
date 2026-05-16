# Soneta / enova365 AI Skills

Zestaw skills dla asystentów AI (Claude, Cursor, Windsurf, itp.) wspierających programowanie, projektowanie i konfigurację platformy **enova365 / Soneta Enterprise / Triva**.

## Dostępne skille

### 0. soneta-erp (meta-skill)

Mapa i przewodnik po pozostałych skillach. Pomaga wybrać właściwy skill w zależności od warstwy zadania (dane, UI, logika, płace).

**Kiedy używać:** rozpoczynasz nowe zadanie dla enova365/Soneta/Triva i nie wiesz, który skill zastosować; zadanie obejmuje wiele warstw platformy i potrzebna jest koordynacja między skillami.

### 1. soneta-programming

Fundamentalne klasy ORM platformy enova365/Soneta Enterprise.

**Zakres:**
- Mapowanie obiektowo-relacyjne (`Row`, `Table`, `Module`)
- Zarządzanie sesją (`Session`) i transakcjami biznesowymi
- Logowanie i dostęp do bazy (`Login`, `Database`, `BusApplication`)
- Paczki danych (`Datapack`, `GuidedRow`) i synchronizacja
- Kontekst aplikacji (`Context`)

**Kiedy używać:** pytania o klasy logiki biznesowej, sesje, transakcje, hierarchię `Row` → `Table` → `Module`.

### 2. soneta-business-xml

Generator plików `business.xml` definiujących strukturę obiektów biznesowych.

**Zakres:**
- Definiowanie tabel i kolumn
- Typy danych (proste, relacyjne, złożone)
- Relacje między obiektami (1:N, N:1, polimorficzne)
- Klucze i indeksy
- Wzorce: słowniki, dokumenty z pozycjami, historia zmian

**Kiedy używać:** tworzenie nowego modułu biznesowego, definiowanie encji, generowanie plików `*.business.xml`.

### 3. soneta-form-xml

Tworzenie plików `form.xml` opisujących formularze i widoki UI platformy enova365.

**Zakres:**
- Formularze stron (`pageform.xml`), widoki list (`viewform.xml`), lookupy (`lookupform.xml`), gridy (`gridform.xml`)
- Elementy: `DataForm`, `Page`, `Group`, `Grid`, `Field`, `Row`, `Stack`, `Flow`, `Command`
- Atrybuty: `EditValue`, `DataContext`, `Visibility`, `RowCondition`, `Renderable`, `CaptionHtml`
- Warunkowe formatowanie, wiązanie danych, wzorce UI

**Kiedy używać:** tworzenie zakładek, widoków list, formularzy i lookupów dla enova365.

### 4. soneta-addon-planning

Planowanie projektów dodatków dla platformy enova365/Soneta Enterprise.

**Zakres:**
- Interaktywny proces planowania w 3 etapach (wizja, architektura, specyfikacja szczegółowa)
- Struktura danych (tabele, relacje)
- Elementy konfigurowalne, definicje list i menu
- Formularze, workery i raporty
- Dokumentacja implementacyjna z TODO

**Kiedy używać:** planowanie nowego modułu/dodatku, przygotowanie założeń projektu, specyfikacja funkcjonalna.

### 5. soneta-ui-style

System projektowy (design system) Soneta / enova365 do budowania aplikacji webowych.

**Zakres:**
- Palety kolorów (motywy jasny/ciemny) — szmaragdowy `#016E46` jako kolor główny
- Typografia (Roboto), layout, system odstępów (skala 4px)
- Komponenty UI: przyciski, formularze, tabele, sidebar, header, kafelki, modale
- Cienie, ikony SVG (~370 ikon liniowych), animacje, responsywność
- Enterprise minimalism, flat design z subtelnymi cieniami

**Kiedy używać:** projektowanie stron/aplikacji w stylu enova365, dashboardy, formularze, strony logowania, panele administracyjne.

### 6. soneta-place-def-elementow

Tworzenie i konfiguracja definicji elementów wynagrodzenia w enova365 (moduł Płace).

**Zakres:**
- Algorytmy naliczania: kreator, edytor C# (`_Param`, `_Wylicz`, `_Wartość1h`), algorytmy wbudowane
- 12 wzorców dla Dodatków, 5 dla Nieobecności, 5 dla Dodatków automatycznych (z analizy ~247 definicji)
- Receptury kodu C#: iterowanie po elementach, staż pracy, wymiar etatu, czas pracy, wskaźniki, cechy pracownika
- Konfiguracja zakładek: Ogólne, Deklaracje (PIT/ZUS), Nieobecności, Algorytm
- Metody sterujące naliczaniem (`_PodstawaUrlopu`, `_PodstawaZasiłku`)

**Kiedy używać:** tworzenie/modyfikacja definicji elementu wynagrodzenia, pisanie algorytmów płacowych (premia procentowa, dodatek stażowy, zasiłek chorobowy, ekwiwalent za urlop).

## Powiązania między skillami

Skille są zaprojektowane do współpracy:

1. **soneta-erp** → wskazuje właściwy skill dla danego zadania
2. **soneta-addon-planning** → planuje strukturę nowego dodatku
3. **soneta-business-xml** → definiuje obiekty biznesowe w XML
4. **soneta-programming** → pokazuje jak pracować z wygenerowanymi klasami C#
5. **soneta-form-xml** → tworzy formularze UI dla obiektów
6. **soneta-ui-style** → styluje interfejs webowy zgodnie z design systemem enova365
7. **soneta-place-def-elementow** → konfiguruje warstwę płacową (definicje elementów wynagrodzenia)

## Instalacja

### Claude Code

Skopiuj foldery skilli do `~/.claude/skills/`.

### Cursor / Windsurf / inne IDE

Dodaj zawartość skilli do kontekstu projektu lub rules.

## Licencja

MIT