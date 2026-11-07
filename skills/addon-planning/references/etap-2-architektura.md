# ETAP 2 — Architektura modułu

Cel: określić **jak** moduł będzie zbudowany — role, dane, interfejs, integracje. Dokument dla zespołu projektowego. Nadal bez szczegółowej zawartości obiektów (to Etap 3) — tu chodzi o architekturę i główne struktury.

## Pytania do zadania użytkownikowi

**Pierwsza tura — role i konfiguracja:**
- Jakie role użytkowników będą korzystać z modułu? Jakie zadania realizują?
- Jakie elementy powinny być konfigurowalne przez klienta?

**Druga tura — dane i interfejs:**
- Jakie są główne obiekty danych? (dokumenty, kartoteki, słowniki)
  *(zanim zaproponujesz strukturę — najpierw inwentaryzacja: patrz twarda kolejność w sekcji 2.3)*
- Jak powinna wyglądać struktura menu?

**Trzecia tura — integracje i przyszłość:**
- Jakie dane z istniejących modułów Soneta będą wykorzystywane? *(jeśli jeszcze nie zeskanowano — zeskanuj moduły narzędziem `scan-modules` — patrz `dane-referencyjne.md`)*
- Czy moduł integruje się z systemami zewnętrznymi?
- Czy klient ma dane do migracji?
- Jeśli moduł obsługują agenci AI (1.13): które obszary udostępnić, jak kontrolować zapis, które dane są chronione? (→ 2.10)

## Sekcje dokumentu Etapu 2

### 2.1. Role użytkowników
Lista operatorów, ich zadania, procesy w których uczestniczą.

### 2.2. Zakres konfiguracji modułu
Elementy konfigurowalne: definicje dokumentów, słowniki, ustawienia, opcje programu dostępne tylko dla pewnych grup klientów. Zarówno konfiguracja wstępna, jak i definicje używane jako podpowiedzi przy przetwarzaniu danych operacyjnych.

### 2.3. Kluczowe struktury danych
Najważniejsze struktury danych — dokumenty, kartoteki. Bez szczegółowej zawartości (to Etap 3). Ogólny diagram relacji między głównymi obiektami.

**Twarda kolejność: inwentaryzacja PRZED decyzjami o strukturach.** Nie projektuj żadnej nowej
tabeli ani kartoteki, dopóki nie wykonasz inwentaryzacji (`scan-modules`, `scan-folders`) — skan
potrafi ujawnić istniejący moduł platformy pokrywający część potrzeb. Decyzję „budować własne
vs użyć istniejącego" podejmuj dopiero **po** inwentaryzacji i odnotuj ją (z uzasadnieniem)
w liście otwartych kwestii.

Na podstawie inwentaryzacji modułów (`scan-modules`, patrz `dane-referencyjne.md`) sprawdź:
- czy potrzebne struktury już istnieją w platformie (unikanie duplikacji),
- jakie tabele nadrzędne (`Guided = root`) mogą być wykorzystane,
- jakie wzorce projektowe stosują istniejące moduły (podział konfiguracyjne/operacyjne wg kolumny `Konfig`, definicje dokumentów, korzenie `Guided` i szczegóły w relacji do nich, datapacki).

### 2.4. Struktura menu i elementy interfejsu
Foldery, hierarchia list w menu głównym, grupowanie funkcjonalne. Wyróżnij elementy interfejsu kluczowe dla sukcesu produktu (np. w kontrolingu — miejsce do budowania zapytań i warunków na wzór arkusza, AND/OR).

### 2.5. Relacje z modułami platformy Soneta
Na podstawie inwentaryzacji modułów (`scan-modules`, patrz `dane-referencyjne.md`):
- wskaż konkretne moduły, z którymi nowy moduł współpracuje (np. Handel, Kadry, Ksiega, CRM),
- wymień konkretne `RowType`/`TableType`, do których się odwołuje (relacje lookup/inner),
- określ, czy realizacja wymaga rozszerzenia istniejących tabel (nowe kolumny lub relacje do już istniejących obiektów).

### 2.6. Relacje z innymi systemami
Integracje z systemami zewnętrznymi, wymiana danych, procesy integracyjne. Jakie dane zasilają system BI i jakie dane BI zwraca do modułu.

### 2.7. Migracja danych
Dane do importu (kartoteki, dokumenty historyczne, salda), źródła (inne ERP, Excel, bazy), wymagania dotyczące zachowania historii.

### 2.8. Wydajność i skalowalność
- **Wolumeny danych** — przewidywana liczba rekordów w kluczowych tabelach (bieżąca i docelowa), częstotliwość zapisu i odczytu.
- **Optymalizacja dostępu** — tabele i operacje wymagające uwagi (dedykowane indeksy, widoki, denormalizacja, cache).
- **Przetwarzanie wsadowe vs online** — co musi działać w czasie rzeczywistym, a co w tle/wsadowo (przeliczenia, raporty, synchronizacja).
- **Skalowalność** — zachowanie przy rosnącej liczbie użytkowników i rekordów, potencjalne wąskie gardła i sposoby ich unikania.

### 2.9. Kierunki rozwoju
Przyszłe funkcjonalności poza zakresem bieżącej wersji (o ile taki rozwój jest planowany).

### 2.10. Udostępnienie modułu agentom AI (pakiety funkcji)
Wypełnij, gdy sekcja 1.13 przewiduje agentów AI; inaczej odnotuj „nie dotyczy”. Agent korzysta
z metod **pakietów funkcji** (bundle) — mechanizm opisuje skill `/soneta:programming`
(`bundles-mcp.md`).

- **Co daje standard** — standardowe pakiety platformy już czytają listy i pola obiektów,
  wyszukują obiekty, importują i eksportują dane. Własny pakiet planuj tylko dla operacji
  dziedzinowych: zapisu złożonych obiektów (dokument z pozycjami), obliczeń modułu, zwięzłego
  odczytu dla agenta.
- **Podział na pakiety** — jeden pakiet = spójny obszar (np. „Odczyt umów”, „Rejestracja umów”).
  Każdy pakiet ma własne prawo w rolach i własną licencję; operator dodaje pakiety do agenta
  osobno. Zapis oddziel od odczytu, jeśli różne role mają dostawać różne zestawy.
- **Model kontroli zapisu (human in the loop)** — dla każdego scenariusza zapisu z 1.13: stan
  roboczy zatwierdzany przez operatora, tryb próbny bez zapisu, parametry operatora (zapis
  wyłączony domyślnie, limity kwot i liczby obiektów).
- **Dane chronione** — które obiekty i pola modułu są danymi chronionymi (prawo proste); agent
  bez zgody dostaje je zamaskowane.
- **Audyt** — które operacje agenta trafiają do dziennika zmian.
- **Licencje** — licencja modułu AI oraz licencja modułu dziedzinowego, od której zależy pakiet.
