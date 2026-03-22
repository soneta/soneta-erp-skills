---
name: soneta-ui-style
description: "System projektowy (design system) Soneta / enova365 do budowania aplikacji webowych i stron internetowych wizualnie spójnych z enova365 ERP. Zawiera pełne specyfikacje: palety kolorów (motywy jasny/ciemny), typografię, layout, komponenty UI (przyciski, formularze, tabele, sidebar, header, kafelki, modale), system odstępów, cienie, ikony SVG, animacje i responsywność. Używaj tego skilla ZAWSZE gdy użytkownik: (1) prosi o zaprojektowanie, zbudowanie lub ostylowanie strony/aplikacji webowej w stylu Soneta lub enova365; (2) wspomina 'styl enova', 'styl Soneta', 'design system enova365', 'UI enova'; (3) chce stworzyć dashboard, formularz, stronę logowania lub panel administracyjny w estetyce enova365; (4) prosi o landing page, stronę firmową lub aplikację z zielonym motywem enterprise w stylu Soneta; (5) pyta o kolory, czcionki, komponenty lub wzorce UI platformy enova365/Soneta. Skill działa z każdym stosem technologicznym (HTML/CSS, React, Vue, Tailwind, itp.)."
---

# Soneta / enova365 — Design System

Ten skill opisuje reguły projektowania interfejsów użytkownika wizualnie spójnych z aplikacją enova365 ERP firmy Soneta. Stosuj te reguły budując strony internetowe, dashboardy, formularze i aplikacje webowe w estetyce Soneta.

## Zasady przewodnie

1. **Szmaragdowa zieleń jako kolor wiodący** — `#016E46` dominuje w headerze, sidebarze (motyw jasny), przyciskach akcji, stanach focus i linkach
2. **Enterprise minimalizm** — czysty, profesjonalny interfejs bez ozdobników, nacisk na czytelność danych
3. **Wysoka gęstość informacji** — kompaktowe wiersze (`30-36px`), pola blisko siebie, minimalne marginesy
4. **Płaski design z subtelnymi cieniami** — brak skeuomorfizmu, delikatne bordery i cienie
5. **Hierarchia przez wagę czcionki, nie rozmiar** — różnice wynikają z `font-weight` (400 vs 600), a nie drastycznych zmian rozmiaru
6. **Dwa motywy: jasny i ciemny** — oba używają tego samego systemu zmiennych CSS
7. **Spójność stanów** — każdy komponent definiuje 6 stanów: default, hover, focus, pressed, selected, disabled

---

## 1. Kolory

### 1.1 Brand (wspólne)

| Token | Wartość | Użycie |
|-------|---------|--------|
| `--brand_500` | `#016E46` | Kolor główny — header, focus, linki |
| `--brand_600` | `#015436` | Brand ciemny |
| `--brand_700` | `#01422A` | Brand najciemniejszy |
| `--brand_300` | `#3A8E6F` | Brand jasny |
| `--brand_200` | `#02E391` | Brand najjaśniejszy |
| Akcent cyjan | `#31e2b8` | Logo — turkusowy |
| Akcent błękitny | `#6acbf3` | Logo — jasny niebieski |

### 1.2 Motyw jasny (White)

| Przeznaczenie | Kolor |
|---------------|-------|
| Tło główne (content area) | `rgb(230, 231, 231)` — jasnoszare, wydziela białe karty |
| Tło headera | `#016E46` — szmaragdowy |
| Tło sidebara | `#003320` — bardzo ciemna zieleń, **ciemniejszy niż header** |
| Sidebar hover | `rgba(0, 70, 38)` |
| Sidebar selected | `rgba(0, 88, 48)` |
| Tekst sidebara | `#FFFFFF` |
| Tło surface/karty | `#FFFFFF` |
| Tło sekcji | `#F5F5F5` – `#FAFAFA` |
| Tekst podstawowy | `#212121` – `#333333` |
| Tekst drugorzędny | `#757575` – `#A3A3A3` |
| Tekst etykiet | `#616161` |
| Obramowanie inputów | `#BDBDBD` – `#E0E0E0` |
| Obramowanie focus | `#016E46` |
| Wiersz zaznaczony | `rgba(46, 125, 50, 0.08)` |
| Wiersz hover | `rgba(0, 0, 0, 0.04)` |
| Separator | `#E0E0E0` |

### 1.3 Motyw ciemny (Black)

| Przeznaczenie | Kolor |
|---------------|-------|
| Tło główne | `#1E1E1E` – `#212121` |
| Tło headera | `#1B5E20` – `#0D3B14` — ciemnozielony |
| Tło sidebara | `#262626` — **grafitowy, nie zielony** |
| Tło surface/karty | `#2A2A2A` – `#303030` |
| Tekst podstawowy | `#E0E0E0` – `#F0F0F0` |
| Tekst drugorzędny | `#A3A3A3` |
| Tekst etykiet | `#BDBDBD` |
| Obramowanie inputów | `#424242` – `#555555` |
| Obramowanie focus | `#016E46` — identyczny jak w jasnym |
| Wiersz zaznaczony | `rgba(46, 125, 50, 0.15)` |
| Wiersz hover | `rgba(255, 255, 255, 0.05)` |
| Separator | `#424242` |
| Overlay modal | `rgba(0, 0, 0, 0.7)` |

### 1.4 Kolory semantyczne

| Przeznaczenie | Kolor |
|---------------|-------|
| Success / Primary action | `#2E7D32` — przyciski "Zapisz" |
| Danger | `#C62828` – `#D32F2F` — przyciski "Zamknij" |
| Warning | `#F57C00` |
| Info | `#1976D2` |
| Link | `#016E46` |
| Disabled | `#A3A3A3` z opacity |

### 1.5 Paleta neutralna (z PaletteBase.css)

Wartości w formacie RGB (bez `rgb()`):

| Token | Wartość | HEX |
|-------|---------|-----|
| `--neutral_0` | `255, 255, 255` | `#FFFFFF` |
| `--neutral_100` | `245, 245, 245` | `#F5F5F5` |
| `--neutral_200` | `229, 229, 229` | `#E5E5E5` |
| `--neutral_300` | `212, 212, 212` | `#D4D4D4` |
| `--neutral_500` | `109, 115, 113` | `#6D7371` |
| `--neutral_700` | `77, 81, 80` | `#4D5150` |
| `--neutral_800` | `38, 38, 38` | `#262626` |
| `--neutral_850` | `33, 33, 33` | `#212121` |
| `--neutral_900` | `23, 23, 23` | `#171717` |

Semantyczne: `--red_600: #DC2626`, `--green_400: #10C200`, `--blue_500: #3B82F6`, `--orange_500: #F97316`, `--yellow_400: #F3D930`, `--emerald_400: #139041`.

---

## 2. Typografia

### Czcionki

| Przeznaczenie | Czcionka | Fallback |
|---------------|----------|----------|
| Główna (`--primaryFont`) | `"FiraSans"` | `Arial, sans-serif` |
| Alternatywna (`--tertiaryFont`) | `"ROBOTO"` | `sans-serif` |
| Nagłówki | `"SourceSansSemiBold"` | `Arial, sans-serif` |
| Branding/logo | `"Lato"` | `sans-serif` |
| Monospace | `Consolas` | `"Courier New", monospace` |

Gdy nie możesz załadować czcionek Soneta, użyj `"Roboto", "Segoe UI", Arial, sans-serif` jako bezpiecznego zamiennika.

### Rozmiary

| Przeznaczenie | Rozmiar |
|---------------|---------|
| Captions | `10px` – `11px` |
| Etykiety pól | `12px` (`--font_size_label: 9pt`) |
| Tekst w inputach | `13px` – `14px` |
| Body (podstawowy) | `14px` |
| Nagłówki sekcji | `14px` – `16px` + `font-weight: 600` |
| Tytuł formularza | `16px` – `18px` |
| Duże nagłówki | `20px` – `24px` |
| Logo / ekran logowania | `24px` – `32px` |

### Grubości

| Styl | Wartość | Użycie |
|------|---------|--------|
| Light | `300` | Delikatne teksty |
| Regular | `400` | Domyślna |
| Medium | `500` | Przyciski |
| Semi-bold | `600` | Nagłówki sekcji, aktywne menu |
| Bold | `700` | Tytuły |

### Wysokość linii

Body: `18-20px`, wiersze tabeli: `20-24px`, inputy: `24-26px`, nagłówki: `28-32px`.

### Letter-spacing

Normalny tekst: `0–0.1px`, etykiety: `0.15–0.25px`, przyciski uppercase: `0.6–2px`.

---

## 3. Layout

### Struktura główna

```
┌──────────────────────────────────────────────────────┐
│                   HEADER BAR (~48-56px)               │
│  [Logo] [Search]           [Ikony] [Avatar]          │
├──────────┬───────────────────────────────────────────┤
│          │  BREADCRUMB (~32px)                        │
│          ├───────────────────────────────────────────┤
│ SIDEBAR  │  TOOLBAR + TABS (~44-48px)                │
│ (250px)  ├───────────────────────────────────────────┤
│          │                                           │
│ Drzewo   │            CONTENT AREA                   │
│ nawigacji│  (Formularz / Lista / Dashboard)           │
│          │                                           │
├──────────┴───────────────────────────────────────────┤
│                   STATUS BAR (opcjonalnie)            │
└──────────────────────────────────────────────────────┘
```

### Wymiary

| Element | Wymiar |
|---------|--------|
| Header bar | `48px` – `56px` |
| Sidebar rozwinięty | `240px` – `260px` |
| Sidebar zwinięty | `48px` – `56px` |
| Breadcrumb | `28px` – `32px` |
| Toolbar | `40px` – `48px` |
| Status bar | `24px` – `28px` |
| Min width | `1024px` |

### System layoutu

- **Flexbox** dominuje — `flex-direction: row` dla sidebar+content, `column` dla sekcji
- **Grid** sporadycznie — dashboard kafelków
- `justify-content: space-between` w toolbarze
- `align-items: center` praktycznie wszędzie

---

## 4. Komponenty UI

### 4.1 Przyciski

| Wariant | Tło | Tekst | Border | Użycie |
|---------|-----|-------|--------|--------|
| **Primary** | `#2E7D32` | `#FFF` | brak | "Zapisz", "Zaloguj" |
| **Secondary** | przezroczyste | inherit | `1px solid` szary | "Czynności" |
| **Tertiary** | przezroczyste | inherit | brak | Ikony toolbar |
| **Danger** | `#C62828` | `#FFF` | brak | "Zamknij" |

Wymiary: height `32-38px` (standard) / `40-44px` (duży), padding `6px 12px` / `8px 16px` / `10px 20px`, border-radius `4-6px`, font `13-14px` weight `500-600`, gap ikona+tekst `6-8px`.

Stany: hover — lekkie rozjaśnienie; focus — `box-shadow: inset 0 0 3px 1px #016E46`; pressed — przyciemnienie; disabled — opacity `0.5-0.6`.

```css
/* Przykład: przycisk Primary */
.btn-primary {
  background-color: #2E7D32;
  color: #FFFFFF;
  border: none;
  border-radius: 4px;
  padding: 6px 16px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: background-color 0.2s ease;
}
.btn-primary:hover { filter: brightness(1.1); }
.btn-primary:focus { box-shadow: inset 0 0 3px 1px #016E46; }
.btn-primary:disabled { opacity: 0.5; cursor: default; }
```

### 4.2 Pola formularza

| Właściwość | Jasny | Ciemny |
|------------|-------|--------|
| Tło | `#FFFFFF` | `#2A2A2A` |
| Tekst | `#212121` | `#E0E0E0` |
| Border | `1px solid #BDBDBD` | `1px solid #555555` |
| Border-radius | `4px` | `4px` |
| Wysokość | `30-32px` | `30-32px` |
| Padding | `4px 8px` | `4px 8px` |
| Font-size | `13-14px` | `13-14px` |

Focus: border `#016E46` + opcjonalnie `box-shadow: inset 0 0 3px 1px #016E46`. Error: czerwony shadow. Warning: pomarańczowy shadow. Disabled: tło `#F5F5F5` / `#1A1A1A`.

**Etykiety**: font `12px`, kolor `#757575` (jasny) / `#BDBDBD` (ciemny), weight `400`.

**Układ formularza**: siatka 2-4 kolumn, etykieta po lewej (~100-140px stała szerokość), input `flex: 1`, gap między wierszami `8-12px`, sekcje oddzielone nagłówkami z marginesem `16-24px`.

### 4.3 Checkbox

Rozmiar `16-18px`, border-radius `2-3px`, checked: zielone tło `#2E7D32` z białym checkmarkiem.

### 4.4 Tabele / Listy

**Nagłówek**: tło `#F5F5F5` (jasny) / `#2A2A2A` (ciemny), tekst `#616161` / `#BDBDBD`, font `12-13px` weight `600`, padding `8px 12px`.

**Wiersze**: tło naprzemienne `#FFF` / `#FAFAFA` (jasny), `#1E1E1E` / `#252525` (ciemny), height `30-36px`, padding `4-6px 12px`, font `13-14px`. Hover: `rgba(0,0,0,0.04)` / `rgba(255,255,255,0.05)`. Selected: zielony tint `rgba(46,125,50,0.08)` / `0.15`.

### 4.5 Zakładki (Tabs)

Styl **outline**: padding `6px 12px`, border `1px solid szary`, border-radius `4px`, font `13px`, aktywna — zielone tło lub zielony border, gap `4-6px`.

Styl **page tabs**: przylegające (`gap: 0`), aktywna — wyróżnione tło + pogrubiony tekst.

### 4.6 Header Bar

- Tło: `#016E46` (jasny) / `#1B5E20` (ciemny)
- Wysokość: `48-56px`, padding: `0 16px`
- Logo: ZAWSZE osadzać poniższe oficjalne SVG inline (nie wymyślać własnego logo!):

```html
<svg viewBox="0 0 200 33.5" height="22" fill="#FFFFFF" xmlns="http://www.w3.org/2000/svg">
  <path d="M69.7,0.1C60.5,0.1,53,7.6,53,16.7c0,9.2,7.5,16.7,16.7,16.7c9.2,0,16.7-7.5,16.7-16.7C86.4,7.6,78.9,0.1,69.7,0.1z M153.2,4.7c-2.8,0-5.4,0.9-6.7,1.8l1,3.2c1-0.6,2.9-1.5,4.8-1.5c2.6,0,3.7,1.3,3.7,3c0,2.3-2.6,3.2-4.6,3.2h-2v3.2h2c2.7,0,5.3,1.2,5.3,4c0,1.8-1.3,3.7-4.7,3.7c-2.2,0-4.4-0.9-5.3-1.4l-1,3.4c1.3,0.8,3.7,1.6,6.6,1.6c5.8,0,9.1-3.1,9.1-7.1c0-3.2-2.3-5.3-5.2-5.8v-0.1c2.9-1,4.3-3,4.3-5.5C160.5,7.3,158,4.7,153.2,4.7L153.2,4.7z M178.3,4.7c-0.4,0-0.8,0-1.3,0.1c-3.3,0.3-6.2,1.4-8.3,3.5c-2.4,2.3-4.1,5.9-4.1,10.4c0,5.8,3.1,10.1,8.8,10.1c5,0,8.3-3.9,8.3-8.4c0-4.8-3.1-7.6-7.2-7.6c-2.3,0-4.1,0.9-5.3,2.3h-0.1c0.6-3.2,2.9-6.2,7.9-6.8c0.9-0.1,1.6-0.1,2.2-0.1V4.7C179,4.7,178.7,4.7,178.3,4.7L178.3,4.7z M187.1,5.1l-1.5,11.8c0.9-0.1,1.9-0.2,3.2-0.2c4.7,0,6.6,1.8,6.6,4.5c0,2.8-2.5,4.3-5.1,4.3c-2.1,0-4.1-0.7-5.2-1.2l-0.9,3.4c1.2,0.7,3.5,1.4,6.2,1.4c5.7,0,9.4-3.6,9.4-8.1c0-2.8-1.3-4.8-3.1-5.9c-1.5-1-3.6-1.5-5.7-1.5c-0.7,0-1.2,0-1.7,0.1l0.7-4.7h9V5.1C199.3,5.1,187.1,5.1,187.1,5.1z M65.3,7.1c0.3,0,0.8,0.2,1.4,0.5l12.7,7.7c0.5,0.3,0.9,0.9,0.9,1.5c0,0.6-0.4,1.2-0.9,1.6l-13.1,7.9c-0.3,0.2-0.6,0.2-0.9,0.2c-1,0-1.8-0.8-1.8-1.8V8.9C63.5,7.9,64.4,7.1,65.3,7.1z M173.2,16.1c2.6,0,4.1,1.9,4.1,4.6c0,2.8-1.5,4.8-3.7,4.8c-2.9,0-4.4-2.5-4.4-5.6c0-0.6,0.1-1,0.3-1.4C170,17.1,171.5,16.1,173.2,16.1z"/>
  <path d="M93.1,4.3c-0.2,0-0.3,0.1-0.5,0.1l-0.4,0.2c-0.7,0.4-1.1,1.3-0.8,2.1l9,21.1c0.5,0.6,1.6,1,2.8,1c1.2,0,2.3-0.4,2.8-1.1l8.5-21.3c0.1-0.8-0.3-1.7-1.1-2l-0.3-0.1c-0.1,0-0.2-0.1-0.3-0.1h-0.5c-0.5,0.1-1,0.3-1.3,0.8l-7.8,20.1L95,5.2c-0.3-0.5-0.8-0.9-1.4-0.9C93.6,4.3,93.1,4.3,93.1,4.3z M9.1,4.4C3,4.4,0,7.8,0,14.9v3c0,7.5,5.1,10.5,11.2,10.5h4.7c0.9-0.1,1.5-0.7,1.7-1.6v-0.5c-0.1-0.8-0.8-1.5-1.6-1.6h-4.8c-2.7,0-7.3,0-7.4-6h12.4c1,0,1.9-0.8,1.9-1.9v-1.8C18,7.7,15.2,4.4,9.1,4.4z M28.9,4.4c-1,0-1.9,0.8-1.9,1.9v20.3c0,0.9,0.7,1.6,1.6,1.8h0.5c0.8-0.1,1.5-0.8,1.6-1.6V8.1h3.8c3.2,0,7.5,0.4,7.5,6.8v12c0.2,0.8,0.9,1.4,1.7,1.4h0.3c0.9,0,1.6-0.7,1.7-1.5V14.9c0-7.4-5-10.5-11.3-10.5C34.6,4.4,28.9,4.4,28.9,4.4z M121.1,4.4c-0.9,0.1-1.6,0.9-1.6,1.8v0.1c0,0.9,0.7,1.7,1.6,1.8h6.4c2.9,0,4.8,0.4,5.2,5.2H128c-2.2,0-8.9,0-8.9,7.4c0,5.6,3.9,7.6,7.3,7.6h8.2c1,0,1.9-0.8,1.9-1.9V14.9c0-7.4-2.7-10.5-8.9-10.5L121.1,4.4L121.1,4.4z M9.1,8.1c2.8,0,5.2,0.5,5.2,6.7H3.7C3.7,8.6,6.3,8.1,9.1,8.1z M128,17h4.7v7.6h-6.4c-1.6,0-3.6-0.7-3.6-3.9C122.8,18.2,123.5,17,128,17z"/>
</svg>
```
- Search: **białe tło**, ciemny tekst, ikona lupy, border-radius `4px`
- Ikony: białe SVG `20-24px`
- Avatar: kółko `32-36px`, zielone tło + biały inicjał
- Shadow: `0 2px 4px rgba(0,0,0,0.1)` na dole

### 4.7 Sidebar

**Motyw jasny**: tło `#003320` (ciemniejsza zieleń niż header), biały tekst, białe ikony SVG `16-18px`. **Motyw ciemny**: tło `#262626` (grafitowy), jasnoszary tekst.

Kształt: border-radius `8-12px`, margin `4-6px` (góra/lewo/dół) — pływający panel z zaokrąglonymi rogami.

Struktura: (1) pasek sterowania "Zwiń"/"Odepnij", (2) pole wyszukiwania, (3) drzewo nawigacji z chevronami.

Hover: `rgba(255,255,255,0.08)` (jasny) / `0.05` (ciemny). Aktywny: `rgba(255,255,255,0.12)` + pogrubienie. Padding elementu: `8px 16px`, wcięcie podpoziomów: `+16-20px`.

### 4.8 Breadcrumb

Wysokość `28-32px`, font `12px`, kolor `#757575` (linki), ostatni segment `#212121` (nie-link), separator `>` kolor `#BDBDBD` z `margin: 0 4px`, ikona Home SVG `14-16px`, padding `4px 16px`.

### 4.9 Modal / Dialog

| Właściwość | Jasny | Ciemny |
|------------|-------|--------|
| Overlay | `rgba(0,0,0,0.5)` | `rgba(0,0,0,0.7)` |
| Tło | `#FFFFFF` | `#2A2A2A` |
| Border-radius | `8-12px` | `8-12px` |
| Shadow | `0 4px 20px rgba(0,0,0,0.25)` | `…0.5)` |
| Padding | `20-25px` | `20-25px` |
| Szerokość | `400-600px` | `400-600px` |

### 4.10 Dropdown / Menu kontekstowe

Tło białe/ciemne, border `1px solid #E0E0E0` / `#424242`, border-radius `8px`, shadow `0 4px 12px rgba(0,0,0,0.15)` / `0.4`, padding elementu `8px 16px`, font `13-14px`, ikony kolorowe `16-20px` po lewej.

### 4.11 Ekran logowania

Dwuczęściowy layout ~50/50: lewa — zdjęcie/branding z logo, prawa — formularz logowania wycentrowany w pionie. Logo "enova365" z akcentami kolorystycznymi. Hasło: "System ERP dla ambitnego biznesu" `22px` kursywa. Przycisk "Zaloguj": zielony, pełna szerokość, `40px` height. Footer: "by Soneta &reg;".

### 4.12 Dashboard — Kafelki (Tiles)

Tło content area: `rgb(230, 231, 231)` (jasny) — wydziela białe kafelki.

**Kafelek**: width `352px`, min-height `80px`, border-radius `12px`, tło białe/ciemne, **border-left `4px solid [kolor_modułu]`** — kolorowy akcent, shadow `0 0 10px 1px rgba(229,229,229)`, padding `10px 16px`.

**Układ wewnętrzny kafelka**: `display: flex; align-items: center; gap: 10px` — ikona po lewej, tekst (tytuł + opis) po prawej. Ikona jest wycentrowana w pionie względem bloku tekstowego.

**Ikona w kafelku**: rozmiar `20px` × `20px`, kolor = kolor modułu (taki sam jak border-left). Ikona to czyste SVG liniowe (outline) — same kontury, **bez koła/okręgu za ikoną**, bez żadnego tła pod ikoną. Ikona renderowana bezpośrednio na białym tle kafelka.

Katalog `SvgResources/` tego skilla zawiera ~370 gotowych ikon SVG enova365. Każda ikona ma `viewBox="0 0 160 160"` i używa `fill` na elementach `<path>` (cienkie kształty dające efekt liniowy). Aby użyć ikony w kafelku, skopiuj zawartość pliku SVG, ustaw `width="20" height="20"` i nadaj kolor modułu przez atrybut `fill` na elemencie `<svg>` lub `<path>`.

**Mapowanie modułów na pliki ikon SVG** (katalog `SvgResources/`):

| Moduł | Plik ikony | Kolor |
|-------|-----------|-------|
| Terminarz | `kalendarz.svg` | Zielony `#2E7D32` |
| Kadry i płace | `osoba.svg` | Pomarańczowy `#F57C00` |
| Księgowość | `kalkulator.svg` | Fioletowy `#7B1FA2` |
| Handel | `towar.svg` | Żółty `#F9A825` |
| CRM | `crm.svg` | Zielonkawy `#2E7D32` |
| Produkcja | `fabryka.svg` | Magenta `#C2185B` |
| Ewidencja dokumentów | `dokument.svg` | Niebieski `#1976D2` |
| Ewidencja Środków Pieniężnych | `monety.svg` | Cyjan `#00838F` |
| Księga inwentarzowa | `portfel.svg` | Oliwkowy `#827717` |
| Ewidencja pojazdów | `samochod.svg` | Czerwony `#C62828` |
| Business Intelligence | `wykres.svg` | Złoty `#F9A825` |

Przykład osadzenia ikony z pliku `kalendarz.svg` w kafelku:
```html
<svg width="20" height="20" viewBox="0 0 160 160" fill="#2E7D32" xmlns="http://www.w3.org/2000/svg">
  <path d="M150 15.002h-40.035V5.037c0-2.762-2.237-5-5-5s-5 2.238-5 5V15h-40V5.037c0-2.762-2.238-5-5-5s-5 2.238-5 5V15H10C4.478 15 0 19.477 0 25v125c0 5.522 4.478 10 10 10h140c5.522 0 10-4.478 10-10V25c0-5.52-4.478-9.998-10-9.998zM150 150H10V25h39.965v5.038c0 2.762 2.238 5 5 5s5-2.238 5-5v-5.036h40v5.038c0 2.763 2.237 5 5 5s5-2.237 5-5v-5.037H150V150zm-35-69.998h10c2.76 0 5-2.24 5-5v-10c0-2.76-2.24-5-5-5h-10c-2.76 0-5 2.24-5 5v10c0 2.76 2.24 5 5 5zM115 120h10a5 5 0 0 0 5-5v-10c0-2.76-2.24-5-5-5h-10c-2.76 0-5 2.24-5 5v10c0 2.765 2.24 5 5 5zm-30-20H75c-2.76 0-5 2.24-5 5v10a5 5 0 0 0 5 5h10a5 5 0 0 0 5-5v-10c0-2.757-2.24-5-5-5zm0-39.998H75c-2.76 0-5 2.24-5 5v10c0 2.76 2.24 5 5 5h10c2.76 0 5-2.24 5-5v-10a5 5 0 0 0-5-5zm-40 0H35c-2.76 0-5 2.24-5 5v10c0 2.76 2.24 5 5 5h10c2.76 0 5-2.24 5-5v-10a5 5 0 0 0-5-5zM45 100H35c-2.76 0-5 2.24-5 5v10a5 5 0 0 0 5 5h10a5 5 0 0 0 5-5v-10c0-2.757-2.24-5-5-5z"/>
</svg>
```

Jeśli nie masz dostępu do plików SVG, narysuj prostą ikonę liniową inline — cienkie ścieżki `<path>` z `fill="[kolor_modułu]"`, viewBox `0 0 160 160`, bez tła i bez koła.

**Tytuł**: Roboto `16px` weight `600`, line-height `28px`. **Opis**: Roboto `14px` weight `400`, kolor `rgb(68, 70, 69)`, line-height `16px`.

Layout kafelków: flex-wrap, gap `8px`, tytuł grupy: `16px` weight `500` uppercase.

Hover: tło `#F5F5F5`, active: brak shadow, focus: outline `2px`.

### 4.13 Toolbar

Wysokość `40-48px`, border-bottom separator, flex row `align-items: center`, gap `4-8px`, padding `4px 16px`. Przyciski outline/tertiary `30-32px` height. Filtry: zielone tło, pill shape `border-radius: 16-20px`.

### 4.14 Scrollbar

Track przezroczysty, thumb `#BDBDBD` (jasny) / `#555555` (ciemny), thumb hover `#999` / `#777`, width `8-10px`, border-radius `4-5px`.

---

## 5. System odstępów (skala 4px)

| Token | Wartość | Użycie |
|-------|---------|--------|
| xs | `2px` | Mikro-gap ikona↔tekst |
| sm | `4px` | Padding małych elementów |
| md | `8px` | Standardowy padding/gap |
| lg | `12px` | Padding komórek, gap przycisków |
| xl | `16px` | Padding sekcji, margin grup |
| 2xl | `20px` | Padding kart/modali |
| 3xl | `24px` | Margin między sekcjami |
| 4xl | `32px` | Duże odstępy |
| 5xl | `40px` | Bardzo duże odstępy |

---

## 6. Border-radius

| Element | Wartość |
|---------|---------|
| Inputy | `4px` |
| Przyciski | `4-6px` |
| Karty/kafelki | `8-12px` |
| Modale | `8-12px` |
| Pill/badge | `16-20px` |
| Avatary | `50%` |
| Tooltipy | `4-6px` |

---

## 7. Cienie

| Kontekst | Wartość |
|----------|---------|
| Subtelny (karty) | `0 1px 3px rgba(0,0,0,0.08)` |
| Standardowy (dropdown) | `0 2px 8px rgba(0,0,0,0.15)` |
| Wyraźny (modal) | `0 4px 20px rgba(0,0,0,0.25)` |
| Focus inset | `inset 0 0 3px 1px #016E46` |
| Header | `0 2px 4px rgba(0,0,0,0.1)` |
| Ciemny motyw | Alpha ×1.5 – ×2 |

---

## 8. Ikony

System ikon enova365: **liniowe SVG (outline)**, viewBox `0 0 160 160`, kolor przez `fill` (nie stroke), `fill: currentColor` lub bezpośredni kolor.

| Rozmiar | Wartość | Użycie |
|---------|---------|--------|
| Mały | `14-16px` | Inline tekst, sidebar |
| Standardowy | `18-20px` | Toolbar, menu |
| Duży | `24-32px` | Nagłówki, dashboard |

Kolory: `#616161` (jasny content), `#BDBDBD` (ciemny), `#FFFFFF` (na zielonym tle), `#2E7D32` (aktywny).

Główne ikony: `szukaj.svg` (lupa), `dom.svg` (home), `ustawienia.svg` (trybik), `dzwonek.svg` (powiadomienia), `osoba.svg` (użytkownik), `folder.svg`, `dokument.svg`, `dodaj.svg` (+), `usun.svg` (kosz), `zapisz.svg` (dyskietka), `anuluj.svg` (X), `filtr.svg`.

**Logo**: Oficjalne SVG logo enova365 jest osadzone w sekcji 4.6 (Header Bar) powyżej. ZAWSZE kopiuj dokładnie ten kod SVG — nigdy nie wymyślaj własnej wersji logo ani nie próbuj odtwarzać go z tekstu + ikon. Na ciemnym tle użyj `fill="#FFFFFF"`, na jasnym `fill="#000000"`.

---

## 9. Animacje

| Właściwość | Wartość |
|------------|---------|
| Transition domyślny | `all 0.2s ease` |
| Hover | `background-color 0.2s, color 0.2s` |
| Opacity | `opacity 0.3s ease` |
| Sidebar collapse | `width 0.3s ease` |

---

## 10. Responsywność

| Breakpoint | Zachowanie |
|------------|------------|
| Desktop (>1200px) | Pełny layout: sidebar + content |
| Tablet (768–1200px) | Sidebar zwinięty, rozwijany overlay |
| Mobile (<768px) | Wersja mobilna — osobna aplikacja |

---

## 11. CSS Variables — konwencja nazewnictwa

Format: `--{komponent}_{wariant}_{stan}_{właściwość}`

Prefiksy: `command_` (przyciski), `input_` (formularze), `nav_tree_` (sidebar), `headerbar_`, `modal_`, `tooltip_`, `popup_`, `checkbox_`, `radio_`, `dp_` (date picker), `scroll_`, `spinner_`, `dashboard_`, `bookmark_` (zakładki), `path_` (breadcrumb), `label_`, `verifier_` (walidacja).

Przykłady: `--command_primary_hover_bg`, `--nav_tree_item_selected`, `--input_focused_border`, `--modal_bar_title_fg`.

---

## 12. Kolory ikon modułów (menu główne)

Na dashboardzie ikony w kolorowych kółkach (border-radius `50%`, `32-40px`):

| Moduł | Kolor |
|-------|-------|
| Terminarz | Zielony |
| Ewidencja dokumentów | Niebieski |
| Kadry i płace | Pomarańczowy |
| Księgowość | Fioletowy |
| Handel | Żółty |
| Produkcja | Magenta |
| CRM | Zielonkawy |

---

## Materiały referencyjne

Pełny przewodnik projektowy z większą ilością szczegółów: przeczytaj `enova365-design-guide.md` w katalogu tego skilla.

Pliki CSS z pełnymi paletami zmiennych:
- `wzorce/css/standard/PaletteBase.css` — paleta jasna (~830 zmiennych)
- `wzorce/css/standard/PaletteDark.css` — paleta ciemna


Ikony SVG: katalog `SvgResources/` zawiera ~370 ikon liniowych.

Pliki SCSS komponentów: katalog `wzorce/css/fullbrowser/` — style poszczególnych komponentów (TileItem, Login, HeaderBar, NavPanelTree, Grid, itd.).
