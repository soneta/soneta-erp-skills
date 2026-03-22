# enova365 - Szczegółowy Przewodnik Projektowy UI

Dokument opisuje zasady budowania interfejsów użytkownika wizualnie spójnych z aplikacją enova365 ERP firmy Soneta. Opracowany na podstawie analizy screenshotów i plików HTML aplikacji.

---

## 1. MOTYWY KOLORYSTYCZNE

Aplikacja posiada dwa pełne motywy: jasny (white) i ciemny (black). Oba używają tego samego systemu zmiennych CSS (~830 tokenów).

### 1.1 Kolor wiodący (Brand)

Kolory brand pochodzą z pliku palety `PaletteBase.css` (katalog `wzorce/css/standard/`).

| Element | Wartość | Zmienna CSS |
|---------|---------|-------------|
| **Główny zielony (primary/header)** | `#016E46` (szmaragdowy) | `--brand_500` |
| **Brand ciemny** | `#015436` | `--brand_600` |
| **Brand najciemniejszy** | `#01422A` | `--brand_700` |
| **Brand jasny** | `#02BB77` | `--brand_300` |
| **Brand najjaśniejszy** | `#02E391` | `--brand_200` |
| **Akcent cyjan** | `#31e2b8` (turkusowy - logo) | — |
| **Akcent błękitny** | `#6acbf3` (jasny niebieski - logo) | — |

### 1.2 Motyw jasny (White)

| Przeznaczenie | Kolor | Opis |
|---------------|-------|------|
| **Tło główne (content area)** | `rgb(230, 231, 231)` | Jasnoszare — `--theme_bg`, wydziela białe elementy UI |
| **Tło nagłówka (header bar)** | `#016E46` | Szmaragdowy — `--brand_500` |
| **Tło sidebara** | `#003320` | Bardzo ciemna zieleń — `RGBA(0, 51, 32)`, **ciemniejszy niż header** |
| **Sidebar hover** | `#004626` | `RGBA(0, 70, 38)` — `--nav_tree_item_hover` |
| **Sidebar selected** | `#005830` | `RGBA(0, 88, 48)` — `--nav_tree_item_selected` |
| **Tekst sidebara** | `#FFFFFF` | Biały tekst na ciemnozielonym tle sidebara |
| **Tło surface/karty** | `#FFFFFF` | Białe karty na jasnym tle |
| **Tło sekcji** | `#F5F5F5` ~ `#FAFAFA` | Bardzo jasnoszare |
| **Tekst podstawowy** | `#212121` ~ `#333333` | Ciemnoszary/prawie czarny |
| **Tekst drugorzędny** | `#757575` ~ `#A3A3A3` | Średni szary |
| **Tekst etykiet** | `#616161` | Szary na etykietach pól |
| **Tekst na ciemnym tle** | `#FFFFFF` | Biały na zielonym headerze |
| **Obramowanie inputów** | `#BDBDBD` ~ `#E0E0E0` | Jasnoszare |
| **Obramowanie focus** | `#016E46` | Zielony border na focus |
| **Tło wiersza zaznaczonego** | `rgba(46, 125, 50, 0.08)` | Bardzo lekki zielony tint |
| **Tło wiersza hover** | `rgba(0, 0, 0, 0.04)` | Prawie przezroczysty szary |
| **Separator/divider** | `#E0E0E0` | Jasnoszara linia |
| **Tło inputu disabled** | `#F5F5F5` | Jasnoszary |

### 1.3 Motyw ciemny (Black)

| Przeznaczenie | Kolor | Opis |
|---------------|-------|------|
| **Tło główne** | `#1E1E1E` ~ `#212121` | Ciemny grafitowy |
| **Tło nagłówka (header bar)** | `#1B5E20` ~ `#0D3B14` | Ciemnozielony (ciemniejszy niż w jasnym) |
| **Tło sidebara** | `#1A1A1A` ~ `#1E1E1E` | Ciemny — w motywie ciemnym sidebar nie jest zielony, lecz ciemny grafitowy |
| **Tło surface/karty** | `#2A2A2A` ~ `#303030` | Ciemny grafitowy |
| **Tło sekcji** | `#252525` ~ `#2C2C2C` | Nieco jaśniejszy ciemny |
| **Tekst podstawowy** | `#E0E0E0` ~ `#F0F0F0` | Jasnoszary |
| **Tekst drugorzędny** | `#A3A3A3` ~ `#9E9E9E` | Średni szary |
| **Tekst etykiet** | `#BDBDBD` | Jasnoszary |
| **Obramowanie inputów** | `#424242` ~ `#555555` | Ciemnoszare |
| **Obramowanie focus** | `#016E46` | Zielony (taki sam jak w jasnym) |
| **Tło wiersza zaznaczonego** | `rgba(46, 125, 50, 0.15)` | Lekki zielony tint na ciemnym |
| **Tło wiersza hover** | `rgba(255, 255, 255, 0.05)` | Prawie przezroczysty biały |
| **Separator/divider** | `#424242` | Ciemnoszara linia |
| **Tło inputu** | `#2A2A2A` ~ `#333333` | Ciemny |
| **Tło overlay/modal** | `rgba(0, 0, 0, 0.5)` ~ `rgba(0, 0, 0, 0.7)` | Półprzezroczysty czarny |

### 1.4 Kolory semantyczne (wspólne dla obu motywów)

| Przeznaczenie | Kolor | Opis |
|---------------|-------|------|
| **Success / Primary action** | `#2E7D32` | Zielony - przyciski "Zapisz" |
| **Danger / Zamknij** | `#C62828` ~ `#D32F2F` | Czerwony - przyciski "Zamknij", "X" |
| **Warning** | `#F57C00` ~ `rgb(192, 64, 0)` | Pomarańczowy |
| **Info** | `#1976D2` | Niebieski |
| **Link** | `#016E46` | Ciemnozielony - linki "Nie pamiętasz hasła?" |
| **Disabled text** | `#A3A3A3` | Szary z opacity |
| **Error border/shadow** | Czerwony z shadow | Walidacja |
| **Warning border/shadow** | Pomarańczowy z shadow | Ostrzeżenia |

### 1.5 Kolory ikon modułów (menu główne)

Na ekranie menu głównego widoczne są kolorowe ikony modułów w kołach:

| Moduł | Kolor ikony |
|-------|-------------|
| Terminarz | Zielony |
| Ewidencja dokumentów | Niebieski |
| Ewidencja Środków Pieniężnych | Cyjan |
| Kadry i płace | Pomarańczowy |
| Księgowość | Fioletowy |
| Księga inwentarzowa | Oliwkowy |
| Ewidencja pojazdów | Czerwony |
| Handel | Żółty |
| Produkcja zaawansowana | Magenta |
| ServiceDesk | Różowy |
| Business Intelligence | Złoty |
| CRM | Zielonkawy |

---

## 2. TYPOGRAFIA

### 2.1 Rodziny czcionek

| Przeznaczenie | Czcionka | Fallback |
|---------------|----------|----------|
| **Główna (primaryFont)** | `"SourceSans"` | `Arial, sans-serif` |
| **Nagłówki/pogrubienia** | `"SourceSansSemiBold"` | `Arial, sans-serif` |
| **Alternatywna** | `"Roboto"` | `sans-serif` |
| **Branding/logo** | `"Lato"` | `sans-serif` |
| **Monospace (kod)** | `Consolas` | `"Courier New", monospace` |

### 2.2 Rozmiary czcionek

| Przeznaczenie | Rozmiar |
|---------------|---------|
| **Drobny tekst (captions)** | `10px` – `11px` |
| **Etykiety pól formularza** | `12px` |
| **Tekst w inputach** | `13px` – `14px` |
| **Tekst podstawowy (body)** | `14px` |
| **Nazwy zakładek/tabs** | `13px` – `14px` |
| **Tytuł formularza** | `16px` – `18px` |
| **Nagłówki sekcji** | `14px` – `16px` (z font-weight: 600) |
| **Nazwa modułu (menu)** | `16px` – `18px` |
| **Opis modułu (menu)** | `12px` – `13px` |
| **Duże nagłówki** | `20px` – `24px` |
| **Logo "enova365"** | `24px` – `32px` |
| **Strona logowania – hasło** | `24px` – `32px` |
| **Ekran logowania – "System ERP"** | `22px` – `26px` |

### 2.3 Grubości czcionek

| Styl | Wartość |
|------|---------|
| **Light** | `300` |
| **Regular** | `400` (domyślna) |
| **Medium** | `500` |
| **Semi-bold** | `600` (nagłówki sekcji, aktywne elementy menu) |
| **Bold** | `bold` / `700` (tytuły, pogrubienia) |

### 2.4 Odstępy między literami

| Kontekst | Wartość |
|----------|---------|
| **Normalny tekst** | `0` ~ `0.1px` |
| **Etykiety** | `0.15px` ~ `0.25px` |
| **Przyciski uppercase** | `0.6px` ~ `2px` |

### 2.5 Wysokość linii

| Kontekst | Wartość |
|----------|---------|
| **Drobny tekst** | `14px` – `16px` |
| **Tekst podstawowy** | `18px` – `20px` |
| **Wiersze tabeli** | `20px` – `24px` |
| **Inputy** | `24px` – `26px` |
| **Nagłówki** | `28px` – `32px` |
| **Duże elementy** | `48px` |

---

## 3. UKŁAD (LAYOUT)

### 3.1 Struktura główna

```
┌──────────────────────────────────────────────────────┐
│                   HEADER BAR (~48-56px)               │
│  [Logo] [Nawigacja]  [Play][Czas]  [Ikony] [Avatar]  │
├──────────┬───────────────────────────────────────────┤
│          │  BREADCRUMB / PATH BAR (~32px)             │
│          ├───────────────────────────────────────────┤
│ SIDEBAR  │  TITLE BAR + TABS / TOOLBAR (~44-48px)    │
│ (250px)  ├───────────────────────────────────────────┤
│          │                                           │
│ Drzewo   │            CONTENT AREA                   │
│ nawigacji│  (Formularz / Lista / Dashboard)           │
│          │                                           │
│          │                                           │
├──────────┴───────────────────────────────────────────┤
│                   STATUS BAR (opcjonalnie)            │
└──────────────────────────────────────────────────────┘
```

### 3.2 Wymiary główne

| Element | Wymiar |
|---------|--------|
| **Header bar height** | `48px` – `56px` |
| **Sidebar width (rozwinięty)** | `240px` – `260px` |
| **Sidebar width (zwinięty)** | `48px` – `56px` |
| **Breadcrumb bar height** | `28px` – `32px` |
| **Toolbar / tab bar height** | `40px` – `48px` |
| **Status bar height** | `24px` – `28px` |
| **Minimalna szerokość okna** | `1024px` |

### 3.3 System layoutu

- **Flexbox** jest dominującym modelem layoutu
- `display: flex` z `flex-direction: row` dla głównego podziału (sidebar + content)
- `display: flex` z `flex-direction: column` dla wertykalnego stackowania sekcji
- **Grid** używany sporadycznie, głównie w dashboardzie modułów (siatka kafelków)
- `justify-content: space-between` dla rozmieszczania elementów w toolbarze
- `align-items: center` praktycznie wszędzie do verticalnego centrowania

---

## 4. KOMPONENTY UI

### 4.1 Przyciski (Buttons)

#### Warianty

| Wariant | Tło | Tekst | Obramowanie | Użycie |
|---------|-----|-------|-------------|--------|
| **Primary** | `#2E7D32` (zielony) | `#FFFFFF` | brak | "Zapisz", "Zaloguj" |
| **Secondary** | przezroczyste | ciemny/jasny tekst | `1px solid` szary | "Wersja mobilna", "Czynności" |
| **Tertiary** | przezroczyste | ciemny/jasny tekst | brak | Ikony w toolbarze |
| **Danger** | `#C62828` (czerwony) | `#FFFFFF` | brak | "Zamknij" |
| **Important** | zielony z wyróżnieniem | `#FFFFFF` | brak | Kluczowe akcje |

#### Rozmiary i kształt

| Właściwość | Wartość |
|------------|---------|
| **Wysokość przycisku** | `32px` – `38px` (standard), `40px` – `44px` (duży) |
| **Padding** | `6px 12px` (mały), `8px 16px` (standard), `10px 20px` (duży) |
| **Border-radius** | `4px` – `6px` (prostokątne z lekkim zaokrągleniem) |
| **Font-size** | `13px` – `14px` |
| **Font-weight** | `500` – `600` |
| **Min-width** | brak (dopasowanie do treści) |
| **Gap (ikona + tekst)** | `6px` – `8px` |

#### Stany

| Stan | Zmiana wizualna |
|------|----------------|
| **Hover** | Lekkie rozjaśnienie tła, zmiana kursora na pointer |
| **Focus** | `box-shadow: inset 0 0 3px 1px #016E46` lub `outline: 2px solid #016E46` |
| **Pressed/Active** | Lekkie przyciemnienie tła |
| **Disabled** | Opacity `0.5` – `0.6`, brak interakcji, `cursor: default` |
| **Selected** | Wyraźniejsze tło, pogrubiony tekst |

#### Przycisk "Zapisz" (przykład Primary)

```css
.btn-primary {
  background-color: #2E7D32;
  color: #FFFFFF;
  border: none;
  border-radius: 4px;
  padding: 6px 16px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
}
```

#### Przycisk "Zamknij" (przykład Danger)

```css
.btn-danger {
  background-color: #C62828;
  color: #FFFFFF;
  border: none;
  border-radius: 4px;
  padding: 6px 12px;
  font-size: 14px;
  font-weight: 500;
  display: flex;
  align-items: center;
  gap: 4px;
}
```

### 4.2 Pola formularza (Inputs)

#### Tekst / Text Input

| Właściwość | Motyw jasny | Motyw ciemny |
|------------|-------------|--------------|
| **Tło** | `#FFFFFF` | `#2A2A2A` |
| **Tekst** | `#212121` | `#E0E0E0` |
| **Obramowanie** | `1px solid #BDBDBD` | `1px solid #555555` |
| **Border-radius** | `4px` | `4px` |
| **Wysokość** | `30px` – `32px` | `30px` – `32px` |
| **Padding** | `4px 8px` | `4px 8px` |
| **Font-size** | `13px` – `14px` | `13px` – `14px` |

#### Stany inputów

| Stan | Opis |
|------|------|
| **Focus** | Border zmienia się na `#016E46` (zielony), opcjonalnie `box-shadow: inset 0 0 3px 1px #016E46` |
| **Disabled** | Tło `#F5F5F5` (jasny) / `#1A1A1A` (ciemny), tekst przyszarzony |
| **Error** | Czerwony shadow/border |
| **Warning** | Pomarańczowy shadow/border |
| **Read-only** | Bez border-bottom, wygląda jak tekst |

#### Etykiety (Labels)

| Właściwość | Wartość |
|------------|---------|
| **Pozycja** | Nad inputem lub po lewej stronie (inline) |
| **Font-size** | `12px` |
| **Color** | Szary (`#757575` jasny / `#BDBDBD` ciemny) |
| **Font-weight** | `400` (regular) |
| **Margin-bottom** | `2px` – `4px` (gdy nad inputem) |

#### Układ pól w formularzu

- Pola ułożone w siatce 2-4 kolumn
- Etykieta po lewej, input po prawej (inline layout) - dominujący wzorzec
- Etykieta ma stałą szerokość (~100-140px)
- Input rozciąga się na dostępną przestrzeń (`flex: 1`)
- Odstęp między wierszami: `8px` – `12px`
- Sekcje oddzielone nagłówkami z większym marginesem górnym (`16px` – `24px`)

### 4.3 Checkbox

| Właściwość | Wartość |
|------------|---------|
| **Rozmiar** | `16px` × `16px` ~ `18px` × `18px` |
| **Border-radius** | `2px` – `3px` |
| **Border** | `1px` – `2px solid` szary |
| **Checked background** | Zielony (`#2E7D32`) |
| **Checked checkmark** | Biały |
| **Disabled** | Przyszarzony z zmniejszoną opacity |

### 4.4 Tabele / Listy

#### Nagłówek tabeli

| Właściwość | Motyw jasny | Motyw ciemny |
|------------|-------------|--------------|
| **Tło** | `#F5F5F5` ~ `#EEEEEE` | `#2A2A2A` ~ `#333333` |
| **Tekst** | `#616161` | `#BDBDBD` |
| **Font-size** | `12px` – `13px` |
| **Font-weight** | `600` |
| **Padding** | `8px 12px` |
| **Border-bottom** | `1px solid #E0E0E0` / `#424242` |

#### Wiersze tabeli

| Właściwość | Motyw jasny | Motyw ciemny |
|------------|-------------|--------------|
| **Tło parzyste** | `#FFFFFF` | `#1E1E1E` |
| **Tło nieparzyste** | `#FAFAFA` ~ `#F5F5F5` | `#252525` ~ `#2A2A2A` |
| **Wysokość wiersza** | `30px` – `36px` |
| **Padding komórki** | `4px 12px` ~ `6px 12px` |
| **Font-size** | `13px` – `14px` |
| **Border-bottom** | `1px solid #F0F0F0` / `#333333` |

#### Stany wierszy

| Stan | Motyw jasny | Motyw ciemny |
|------|-------------|--------------|
| **Hover** | `rgba(0,0,0,0.04)` | `rgba(255,255,255,0.05)` |
| **Selected** | Lekki zielony tint `rgba(46,125,50,0.08)` | `rgba(46,125,50,0.15)` |
| **Active/focused** | Wyraźniejszy zielony tint | Wyraźniejszy zielony tint |

### 4.5 Zakładki (Tabs)

Na podstawie screenshotów widoczne są dwa style zakładek:

#### Tabs w toolbarze (styl "pill/outline")

| Właściwość | Wartość |
|------------|---------|
| **Styl** | Obramowane prostokąty (outline) |
| **Padding** | `6px 12px` |
| **Border-radius** | `4px` |
| **Border** | `1px solid` szary |
| **Font-size** | `13px` |
| **Aktywna zakładka** | Zielone tło lub wyraźny zielony border |
| **Gap między tabami** | `4px` – `6px` |

#### Tabs w dokumencie (styl "page tabs")

| Właściwość | Wartość |
|------------|---------|
| **Styl** | Zakładki z górnym paskiem jako sekcje |
| **Gap** | `0px` (przylegające) |
| **Aktywna** | Wyróżnione tło, pogrubiony tekst |

### 4.6 Nagłówek aplikacji (Header Bar)

| Właściwość | Wartość |
|------------|---------|
| **Tło** | Szmaragdowy (`#016E46` — `--brand_500`) |
| **Wysokość** | `48px` – `56px` |
| **Logo** | Po lewej stronie headera. Używać pliku `logo_standard.svg` (patrz sekcja 8.5) osadzonego inline jako `<svg>` z `fill="#FFFFFF"` na obu elementach `<path>`, `viewBox="0 0 200 33.5"`, wysokość `22px`, szerokość `auto`. Logo zawiera pełny napis „enova365" z ikoną play (kółko z trójkątem) zastępującą literę „o". **Nie odtwarzać logo z osobnych elementów (tekst + ikona)** — zawsze używać oficjalnego pliku SVG |
| **Pole wyszukiwania** | Umieszczone w headerze po prawej od logo, ale z **jasnym/białym tłem** i ciemnym tekstem (nie przezroczyste/ciemne). Ikona lupy po lewej stronie inputa. Zaokrąglone rogi (`border-radius: 4px`) |
| **Ikony** | Białe, liniowe SVG, `20px` – `24px` |
| **Avatar** | Kółko `32px` – `36px`, zielone tło z białym inicjałem |
| **Padding** | `0 16px` |
| **Shadow** | Subtelny shadow na dole |
| **Elementy środkowe** | Play/timer, zegar (format `00:00`) |
| **Elementy prawe** | Ikona lupy (szukaj), ikona ustawień (trybik), ikona dzwonka (powiadomienia), avatar z nazwą użytkownika i rolą |

### 4.7 Sidebar / Panel nawigacji

**WAŻNE:** W motywie jasnym sidebar ma **bardzo ciemne zielone tło** (`#003320`) z **białym tekstem** — jest **ciemniejszy niż header** (`#016E46`). Sidebar jest wizualnie "pływającym panelem" — posiada **zaokrąglone rogi** i **marginesy** oddzielające go od headera i content area.

#### Kształt i pozycjonowanie sidebara

| Właściwość | Wartość |
|------------|---------|
| **Border-radius** | `8px` ~ `12px` (zaokrąglone narożniki panelu) |
| **Margin** | `4px` ~ `6px` od góry, lewej i dołu — tworzy widoczną szczelinę wokół panelu |
| **Tło za sidebarem** | Takie samo jak content area (białe / ciemne) — widoczne w szczelinach marginesów |

#### Struktura sidebara

Sidebar składa się z trzech sekcji (od góry):
1. **Pasek sterowania** — przyciski "Zwiń" (strzałka w lewo) i "Odepnij" na tle identycznym z headerem
2. **Pole wyszukiwania** — input "Wyszukaj w menu" z ikoną lupy, białe tło, pełna szerokość
3. **Drzewo nawigacji** — lista pozycji z chevronami do rozwijania podpoziomów

| Właściwość | Motyw jasny | Motyw ciemny |
|------------|-------------|--------------|
| **Tło całego sidebara** | `#003320` (bardzo ciemna zieleń, ciemniejszy niż header) | `#262626` (ciemny grafitowy) |
| **Szerokość** | `240px` – `260px` | `240px` – `260px` |
| **Tekst menu** | `#FFFFFF` (biały), `13px` – `14px` | `#E0E0E0` (jasnoszary), `13px` – `14px` |
| **Pole wyszukiwania** | Białe tło (`#FFFFFF`), ciemny tekst, placeholder szary | Ciemne tło (`#2A2A2A`), jasny tekst |
| **Aktywny element** | Pogrubiony biały tekst, subtelne jaśniejsze tło (`rgba(255,255,255,0.12)`) | Pogrubiony, zielony tekst |
| **Hover** | `rgba(255,255,255,0.08)` — lekkie rozjaśnienie | `rgba(255,255,255,0.05)` |
| **Ikona rozwinięcia** | Chevron `>` / `v`, biały, `12px` | Chevron `>` / `v`, szary, `12px` |
| **Ikony pozycji menu** | Białe liniowe SVG, `16px` – `18px` | Szare liniowe SVG, `16px` – `18px` |
| **Padding elementu** | `8px 16px` | `8px 16px` |
| **Wcięcie podpoziomów** | +`16px` – `20px` na poziom | +`16px` – `20px` na poziom |
| **Border-right** | Brak (sidebar jest wizualnie oddzielony marginesem i zaokrągleniem) | Brak |
| **Border-radius panelu** | `8px` ~ `12px` | `8px` ~ `12px` |
| **Margin panelu** | `4px` ~ `6px` (góra, lewo, dół) | `4px` ~ `6px` (góra, lewo, dół) |
| **Przyciski "Zwiń"/"Odepnij"** | Białe ikony/tekst na zielonym tle, małe `12px` – `13px` | Szare ikony/tekst |

### 4.8 Breadcrumb / Ścieżka

Breadcrumb wyświetla się w górnej części content area, bezpośrednio pod paskiem zakładek dokumentów. Zawiera ikonę domu (SVG `dom.svg`) jako link do strony głównej, po której następują segmenty ścieżki oddzielone separatorem `>`.

| Właściwość | Wartość |
|------------|---------|
| **Tło** | Jak content area (`#FFFFFF` / `#1E1E1E`) |
| **Wysokość** | `28px` – `32px` |
| **Font-size** | `12px` |
| **Kolor tekstu segmentów** | Szary (`#757575`), każdy segment jest linkiem |
| **Kolor aktywnego (ostatni)** | Ciemniejszy tekst (`#212121`) — nie jest linkiem |
| **Separator** | `>` (znak większości), kolor szary `#BDBDBD`, z `margin: 0 4px` |
| **Ikona Home** | Liniowa ikona SVG domku (`dom.svg`), `14px` – `16px`, kolor szary |
| **Padding** | `4px 16px` |
| **Przykład** | `🏠 > Kadry i płace > Kadry > Pracownicy` |

### 4.9 Modal / Dialog

| Właściwość | Motyw jasny | Motyw ciemny |
|------------|-------------|--------------|
| **Overlay** | `rgba(0,0,0,0.5)` | `rgba(0,0,0,0.7)` |
| **Tło modala** | `#FFFFFF` | `#2A2A2A` |
| **Border-radius** | `8px` – `12px` |
| **Shadow** | `0 4px 20px rgba(0,0,0,0.25)` | `0 4px 20px rgba(0,0,0,0.5)` |
| **Padding** | `20px` – `25px` |
| **Nagłówek** | Osobne tło, border-bottom |
| **Szerokość** | `400px` – `600px` (zależnie od treści) |

### 4.10 Menu kontekstowe / Dropdown

Na screenshotach widoczne jest menu czynności z dwoma kolumnami:

| Właściwość | Motyw jasny | Motyw ciemny |
|------------|-------------|--------------|
| **Tło** | `#FFFFFF` | `#2A2A2A` ~ `#1E1E1E` |
| **Border** | `1px solid #E0E0E0` | `1px solid #424242` |
| **Border-radius** | `8px` |
| **Shadow** | `0 4px 12px rgba(0,0,0,0.15)` | `0 4px 12px rgba(0,0,0,0.4)` |
| **Padding wewnętrzny** | `8px 0` |
| **Padding elementu** | `8px 16px` |
| **Font-size elementu** | `13px` – `14px` |
| **Hover elementu** | Lekkie tło |
| **Układ** | Dwie kolumny w dużym menu |
| **Pole wyszukiwania** | Na górze menu, z ikoną lupy |
| **Ikony elementów** | Kolorowe ikony `16px` – `20px` po lewej |

### 4.11 Ekran logowania (Login)

| Element | Opis |
|---------|------|
| **Układ** | Dwuczęściowy: lewo - zdjęcie/branding, prawo - formularz |
| **Podział** | ~50/50 lub 55/45 |
| **Lewa strona** | Zdjęcie stockowe z logo enova365 nałożonym |
| **Prawa strona** | Formularz logowania, wycentrowany w pionie |
| **Logo** | "en**>**va 365" z kolorowymi akcentami |
| **Hasło brandingowe** | "System ERP dla ambitnego biznesu", `22px`, kursywa |
| **Input Login** | Z etykietą, border `1px`, focus zielony |
| **Input Hasło** | Z etykietą, border `1px` |
| **Checkbox "Zapamiętaj"** | Standardowy checkbox |
| **Link "Nie pamiętasz hasła?"** | Zielony tekst, `#016E46` |
| **Przycisk "Zaloguj"** | Zielony, pełna szerokość, `height: 40px`, `border-radius: 4px` |
| **Przycisk "Wersja mobilna"** | Outline, pełna szerokość |
| **"Baza danych"** | Mała etykieta + wartość |
| **Footer** | "by Soneta ®" w prawym dolnym rogu |

### 4.12 Dashboard / Menu główne (Folder) — Kafelki (Tiles)

Parametry kafelków pochodzą z `TileItem.module.scss`, `TileGroup.module.scss` i `PaletteBase.css`.

**Tło content area (za kafelkami):** `rgb(230, 231, 231)` — jasnoszare (`--theme_bg`), wydziela białe kafelki z interfejsu.

#### Kafelek (Tile Item)

| Właściwość | Wartość | Źródło |
|------------|---------|--------|
| **Szerokość** | `352px` | `TileItem` |
| **Min-height** | `80px` | `TileItem` |
| **Border-radius** | `12px` | `TileItem` |
| **Tło (jasny)** | `#FFFFFF` | `--tile_item_bg: --neutral_0` |
| **Tło (ciemny)** | `#2A2A2A` – `#333333` | `--neutral_850` |
| **Border-left** | `4px solid [kolor_modułu]` | Kolorowy akcent po lewej stronie — kolor odpowiada ikonowi modułu |
| **Box-shadow** | `0px 0px 10px 1px rgba(229,229,229)` | `--tile_shadow` |
| **Padding** | `10px 16px` | `TileItem .t-content` |
| **Gap (ikona↔tekst)** | `10px` | `TileItem .t-content` |

#### Ikona w kafelku

| Właściwość | Wartość |
|------------|---------|
| **Rozmiar** | `20px` × `20px` — SVG liniowe |
| **Kolor** | Taki sam jak `border-left` — kolor modułu (np. zielony, niebieski, pomarańczowy) |
| **Pozycja** | Po lewej stronie, vertically centered |

#### Typografia kafelka

| Element | Font | Rozmiar | Waga | Kolor |
|---------|------|---------|------|-------|
| **Tytuł modułu** | `Roboto` (`--tertiaryFont`) | `16px` | `600` (`--tile_fw`) | `#000000` (czarny, `--pureBlack`) |
| **Opis modułu** | `Roboto` (`--tertiaryFont`) | `14px` | `400` | `rgb(68, 70, 69)` (`--tile_desc_fg`) |
| **Line-height tytułu** | — | `28px` | — | — |
| **Line-height opisu** | — | `16px` | — | — |

#### Layout kafelków

| Właściwość | Wartość | Źródło |
|------------|---------|--------|
| **Układ** | Flex-wrap, wyrównanie do lewej | `TileGroup .tile-content` |
| **Gap między kafelkami** | `8px` | `TileMenu .main-menu`, `TileGroup .tile-content` |
| **Padding grupy** | `30px 10px 0 4px` (pierwszy: `10px 10px 0 4px`) | `TileGroup` |
| **Tytuł grupy** | `16px`, `font-weight: 500`, uppercase | `TileGroup .title-group` |

#### Stany kafelka

| Stan | Efekt |
|------|-------|
| **Hover** | Tło zmienia się na `#F5F5F5` (`--tile_group_hover_bg`) |
| **Active** | Brak shadow, tło `--tile_active_bg` |
| **Focus** | Outline `2px` z offset `2px` |

### 4.13 Toolbar / Pasek narzędzi

| Właściwość | Wartość |
|------------|---------|
| **Wysokość** | `40px` – `48px` |
| **Tło** | Takie samo jak content area |
| **Border-bottom** | `1px solid` separator |
| **Układ** | Flex row, `align-items: center` |
| **Gap** | `4px` – `8px` |
| **Padding** | `4px 16px` |
| **Przyciski w toolbarze** | Styl outline/tertiary, `height: 30px` – `32px` |
| **Filtry** | Zielone tło ("Rozwiń filtry"), `border-radius: 16px` – `20px` (pill) |

### 4.14 Scrollbar (niestandardowy)

| Właściwość | Motyw jasny | Motyw ciemny |
|------------|-------------|--------------|
| **Track** | Przezroczyste lub jasnoszare | Przezroczyste lub ciemnoszare |
| **Thumb** | `#BDBDBD` | `#555555` |
| **Thumb hover** | `#999999` | `#777777` |
| **Szerokość** | `8px` – `10px` |
| **Border-radius thumb** | `4px` – `5px` |

---

## 5. SPACING SYSTEM (SYSTEM ODSTĘPÓW)

Aplikacja używa skali 4px:

| Token | Wartość | Użycie |
|-------|---------|--------|
| **xs** | `2px` | Mikro-odstępy, gap między ikoną a tekstem |
| **sm** | `4px` | Padding wewnętrzny małych elementów |
| **md** | `8px` | Standardowy padding, gap w flexie |
| **lg** | `12px` | Padding komórek tabeli, gap przycisków |
| **xl** | `16px` | Padding sekcji, margin między grupami |
| **2xl** | `20px` | Padding kart/modali |
| **3xl** | `24px` | Margin między sekcjami formularza |
| **4xl** | `32px` | Duże odstępy strukturalne |
| **5xl** | `40px` | Bardzo duże odstępy |

---

## 6. BORDER-RADIUS (ZAOKRĄGLENIA)

| Element | Wartość |
|---------|---------|
| **Inputy** | `4px` |
| **Przyciski** | `4px` – `6px` |
| **Karty/kafelki** | `8px` – `12px` |
| **Modals** | `8px` – `12px` |
| **Pill/badge** | `16px` – `20px` |
| **Avatary** | `50%` (koła) |
| **Tooltipy** | `4px` – `6px` |
| **Tabs** | `4px` (góra) lub `0` |

---

## 7. CIENIE (SHADOWS)

| Kontekst | Wartość |
|----------|---------|
| **Subtelny (karty)** | `0 1px 3px rgba(0,0,0,0.08)` |
| **Standardowy (dropdown)** | `0 2px 8px rgba(0,0,0,0.15)` |
| **Wyraźny (modal)** | `0 4px 20px rgba(0,0,0,0.25)` |
| **Focus inset** | `inset 0 0 3px 1px #016E46` |
| **Header shadow** | `0 2px 4px rgba(0,0,0,0.1)` |
| **Ciemny motyw** | Intensywniejsze wartości alpha (×1.5 – ×2) |

---

## 8. IKONY

### 8.1 System ikon — liniowe SVG

Aplikacja enova365 używa **własnego zestawu ikon SVG w stylu liniowym (outline/stroke)**. Ikony NIE są wypełnione (filled) — składają się z konturów i linii. Każda ikona jest zdefiniowana jako plik `.svg` z elementem `<path>` i `viewBox="0 0 160 160"`.

**Kluczowe cechy stylu ikon:**
- **Liniowe / outline** — ikony rysowane konturem, nie wypełnieniem
- **Jednolity kolor** — fill na elemencie `<path>`, kolor zmieniany przez CSS (`fill: currentColor` lub bezpośrednie nadpisanie `fill`)
- **ViewBox 160×160** — standardowy viewBox, ikony skalowane do wymaganego rozmiaru
- **Brak stroke** — mimo liniowego wyglądu, ikony używają `fill` na ścieżkach o cienkich kształtach (nie `stroke`)

### 8.2 Rozmiary i kolory

| Właściwość | Wartość |
|------------|---------|
| **Rozmiar mały** | `14px` – `16px` (inline z tekstem, sidebar) |
| **Rozmiar standardowy** | `18px` – `20px` (toolbary, menu, breadcrumb) |
| **Rozmiar duży** | `24px` – `32px` (nagłówki, dashboard kafelki) |
| **Kolor (jasny motyw, content)** | `#616161` (szary), kontekstowy kolor semantyczny |
| **Kolor (ciemny motyw)** | `#BDBDBD` (jasnoszary) |
| **Kolor na zielonym tle (header, sidebar jasny)** | `#FFFFFF` (biały) |
| **Kolor aktywny** | `#2E7D32` (zielony) |

### 8.3 Najważniejsze ikony i ich pliki SVG

| Nazwa pliku | Zastosowanie |
|-------------|-------------|
| `logo.svg` | Ikona play w kole (sam symbol, biały fill) — używany w headerze jako element dekoracyjny |

### 8.5 Pliki logo w katalogu `SvgResources/Standard/`

Gotowe, kompletne wersje logotypu enova365 do osadzenia w interfejsie:

| Nazwa pliku | Opis | ViewBox |
|-------------|------|---------|
| `logo_standard.svg` | **Pełne logo tekstowe** "enova365" — tekst + ikona play (zastępuje "o"). Dwie ścieżki: jedna dla symbolu play z cyframi "365", druga dla liter "en_va". Kolor domyślny czarny, na ciemnym tle nadać `fill: #FFFFFF` | `0 0 200 33.5` |
| `logo_full_standard.svg` | Logo z dopiskiem "business" poniżej — pełna wersja brandingowa z podtytułem | `0 0 200 44` |
| `logo_icon_standard.svg` | Sam symbol — kółko z trójkątem play (ikona aplikacji) | `0 0 160 160` |
| `ustawienia_logo.svg` | Logo w kontekście ustawień | — |

**Użycie w headerze:** Osadzić `logo_standard.svg` inline jako `<svg>` z `fill="#FFFFFF"` na obu `<path>`, wysokość `22px`, szerokość auto.
| `szukaj.svg` | Ikona lupy — wyszukiwanie |
| `dom.svg` | Ikona domu — breadcrumb, strona główna |
| `ustawienia.svg` | Ikona trybiku — ustawienia |
| `dzwonek.svg` | Ikona dzwonka — powiadomienia |
| `osoba.svg` | Ikona osoby — użytkownicy, avatar |
| `folder.svg` | Ikona folderu — nawigacja, moduły |
| `dokument.svg` | Ikona dokumentu — dokumenty handlowe |
| `kalendarz.svg` | Ikona kalendarza — terminarz |
| `filtr.svg` | Ikona filtra — filtry list |
| `dodaj.svg` | Ikona plus — dodawanie |
| `usun.svg` | Ikona kosza — usuwanie |
| `zapisz.svg` | Ikona dyskietki — zapisywanie |
| `drukarka.svg` | Ikona drukarki — drukowanie |
| `wykres.svg` | Ikona wykresu — raporty, BI |
| `anuluj.svg` | Ikona X — anulowanie, zamykanie |
| `rozwin.svg` | Ikona chevron — rozwijanie menu |
| `wstecz.svg` / `naprzod.svg` | Strzałki — nawigacja |
| `odswiez.svg` | Ikona odświeżania — przeładowanie |

### 8.4 Ikony modułów na dashboardzie

Na ekranie menu głównego ikony modułów wyświetlane są w **kolorowych kółkach**. Ikona SVG jest biała, tło kółka ma kolor przypisany do modułu (patrz sekcja 1.5). Kółka mają `border-radius: 50%`, rozmiar `32px` – `40px`.

---

## 9. ANIMACJE I PRZEJŚCIA

| Właściwość | Wartość |
|------------|---------|
| **Transition domyślny** | `all 0.2s ease` lub `0.15s` |
| **Hover transition** | `background-color 0.2s, color 0.2s` |
| **Opacity transition** | `opacity 0.3s ease` |
| **Sidebar collapse** | `width 0.3s ease` |
| **Skeleton loader** | Gradient shimmer `#f0f0f0` → `#e0e0e0` → `#f0f0f0` |
| **Spinner** | SVG z gradient stops, rotacja |

---

## 10. RESPONSYWNOŚĆ

| Breakpoint | Zachowanie |
|------------|------------|
| **Desktop (>1200px)** | Pełny layout: sidebar + content |
| **Tablet (~768-1200px)** | Sidebar zwinięty domyślnie, rozwijany overlay |
| **Mobile (<768px)** | "Wersja mobilna" - osobna aplikacja |

---

## 11. WZORCE NAZEWNICTWA CSS VARIABLES

System ~830 zmiennych CSS stosuje konwencję:

```
--{komponent}_{wariant}_{stan}_{właściwość}
```

Przykłady:
- `--command_primary_hover_bg` → przycisk primary, stan hover, tło
- `--nav_tree_item_selected` → drzewo nawigacji, element zaznaczony
- `--input_focused_border` → input, stan focus, border
- `--modal_bar_title_fg` → modal, pasek tytułowy, kolor tekstu

### Główne prefiksy komponentów:
| Prefiks | Komponent |
|---------|-----------|
| `command_` | Przyciski (primary, secondary, tertiary) |
| `input_` | Pola formularza |
| `nav_tree_` | Sidebar / drzewo nawigacji |
| `headerbar_` | Górny pasek |
| `modal_` | Okna dialogowe |
| `tooltip_` | Podpowiedzi |
| `popup_` | Menu kontekstowe / rozwijane |
| `checkbox_` | Checkboxy |
| `radio_` | Radio buttons |
| `dp_` | Date picker |
| `scroll_` | Paski przewijania |
| `spinner_` | Loadery / spinnery |
| `dashboard_` | Dashboard / kafelki modułów |
| `bookmark_` | Zakładki |
| `path_` | Breadcrumb |
| `label_` | Etykiety statusowe |
| `verifier_` | Komunikaty walidacji |
| `recorder_` | Nagrywanie makr |

---

## 12. PODSUMOWANIE KLUCZOWYCH ZASAD PROJEKTOWYCH

1. **Zielony jako kolor wiodący** — ciemna zieleń (`#1B5E20` – `#2E7D32`) dominuje w nagłówku, sidebarze, przyciskach akcji, stanach focus i linkach.

2. **Minimalizm i profesjonalizm** — interfejs jest utrzymany w stylu enterprise, bez ozdobników, z naciskiem na czytelność danych.

3. **Gęstość informacji** — formularze i listy są kompaktowe (wiersze `30-36px`), pola blisko siebie, minimalne marginesy.

4. **Dwukolorowy kontrast** — w obu motywach kontrast opiera się na jasne tło + ciemny tekst (lub odwrotnie), z zielenią jako jedynym akcentem kolorystycznym.

5. **Spójność stanów** — każdy komponent ma zdefiniowane 6 stanów: default, hover, focus, pressed, selected, disabled.

6. **Płaski design z subtelnymi cieniami** — brak skeuomorfizmu, minimalne cienie, płaskie tła z delikatnymi borderami.

7. **Hierarchia typograficzna przez wagę, nie rozmiar** — różnice między elementami wynikają bardziej z `font-weight` (400 vs 600) niż z drastycznych zmian rozmiaru.

8. **Sidebar jako główna nawigacja** — drzewo nawigacji po lewej stronie z możliwością zwijania, z wbudowanym wyszukiwaniem.

9. **Kontekstowe toolbary** — pasek narzędzi zmienia się w zależności od widoku (lista vs formularz), z przyciskami outline.

10. **System zmiennych CSS** — pełna temowalność przez ~830 CSS custom properties, umożliwiająca łatwe przełączanie motywów.
