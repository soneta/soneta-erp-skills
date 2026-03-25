# Soneta / enova365 AI Skills

Zestaw skills dla asystentów AI (Claude, Cursor, Windsurf, itp.) wspierających programowanie i projektowanie z platformą **enova365/Soneta Enterprise**.

## Dostępne skille

### 1. soneta-programming-basics

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

### 6. soneta-mcp-ui-guide

Obsługa programów Soneta (enova365, Triva) przez narzędzia MCP `soneta_ui`.

**Zakres:**
- Nawigacja po modułach i folderach programu (Handel, Kadry, Księgowość, CRM, itp.)
- Przeglądanie list z filtrowaniem i stronicowaniem
- Otwieranie formularzy i przełączanie zakładek
- Edycja danych na formularzach (pola oznaczone jako `edytowany`)
- Dodawanie nowych obiektów
- Mapa ~100 najczęściej używanych folderów programu

**Kiedy używać:** odczyt/edycja danych w enova365 lub Triva przez MCP — kontrahenci, faktury, pracownicy, towary, stany magazynowe, przelewy, deklaracje.

## Powiązania między skillami

Skille są zaprojektowane do współpracy:

1. **soneta-addon-planning** → planuje strukturę nowego dodatku
2. **soneta-business-xml** → definiuje obiekty biznesowe w XML
3. **soneta-programming-basics** → pokazuje jak pracować z wygenerowanymi klasami C#
4. **soneta-form-xml** → tworzy formularze UI dla obiektów
5. **soneta-ui-style** → styluje interfejs webowy zgodnie z design systemem enova365
6. **soneta-mcp-ui-guide** → obsługuje dane w działającej instancji enova365/Triva przez MCP

## Instalacja

### Claude Code

Skopiuj folder ze skillem do `~/.claude/skills/`.

### Cursor / Windsurf / inne IDE

Dodaj zawartość skilli do kontekstu projektu lub rules.

## Licencja

MIT
