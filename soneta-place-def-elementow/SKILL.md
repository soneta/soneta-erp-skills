---
name: soneta-place-def-elementow
description: Tworzenie i konfiguracja definicji elementów wynagrodzenia na platformie Soneta przez MCP soneta_ui. Algorytmy naliczania (kreator, edytor C#, wbudowane), wzorce algorytmiczne, receptury kodu C# dla elementów płacowych, konfiguracja PIT/ZUS/nieobecności. Używaj gdy użytkownik: (1) chce utworzyć/skonfigurować definicję elementu wynagrodzenia (zakładki Ogólne, Deklaracje, Nieobecności, Algorytm); (2) pyta o algorytm naliczania (kreator, edytor, kod C#); (3) potrzebuje kodu C# do edytora algorytmu (_Param, _Wylicz, _Wartość1h); (4) pyta o wzorce dla dodatków, nieobecności, zasiłków; (5) wspomina 'definicja elementu', 'element wynagrodzenia', 'algorytm płacowy', 'WypSkladnik', 'WypElement', 'premia procentowa', 'dodatek stażowy', 'zasiłek chorobowy', 'ekwiwalent za urlop'; (6) pisze recepturę kodu płacowego (staż pracy, wymiar etatu, czas pracy, wskaźniki, cechy pracownika).
---

# Definicje elementów wynagrodzenia — platforma Soneta (enova365, Triva)

Ten skill zawiera kompletną wiedzę o tworzeniu i konfigurowaniu definicji elementów wynagrodzenia na platformie Soneta. Wiedza jest podzielona na pliki referencyjne — czytaj odpowiedni plik w zależności od potrzeby.

## Spis treści referencji

| Plik | Kiedy czytać | Zawartość |
|---|---|---|
| `references/algorytmy-naliczania.md` | Gdy trzeba wybrać typ algorytmu, skonfigurować kreator lub napisać kod edytora | Typy algorytmów, parametry kreatora (podstawa, mnożnik, korekty), struktura kodu C# edytora, przykłady kompletnych algorytmów |
| `references/wzorce-algorytmiczne.md` | Gdy trzeba znaleźć najbliższy wzorzec dla nowego elementu | 12 wzorców dla Dodatków (A-L), 5 dla Nieobecności (A-E), 5 dla Dodatków automatycznych (A-E) — z analizy ~247 definicji |
| `references/receptury-kodu.md` | Gdy trzeba napisać konkretny fragment kodu C# | 24 kategorie gotowych fragmentów: iterowanie po elementach, wynagrodzenie zasadnicze, nieobecności, wymiar etatu, czas pracy, okresy, staż, cechy, wskaźniki, parametry dodatku, zaokrąglenia, netto→brutto, urlopy, debugowanie |
| `references/api-algorytmow.md` | Gdy potrzebna jest referencja API — pola, metody, klasy, typy | Pola WypSkladnik, metody pomocnicze, klasy naliczania, moduły, dostęp do konfiguracji, operacje na typach danych, sygnatury metod |
| `references/metody-sterujace-naliczaniem.md` | Gdy element musi wpływać na podstawy urlopów lub zasiłków | Metody _PodstawaUrlopu, _PodstawaZasiłku, klasa PodstawaZasiłkuArgs |

## Lokalizacja w programie

Folder konfiguracyjny: `Ustawienia/Kadry i płace/Płace/Elementy wynagrodzenia`

Formularz zawiera:
- **Listę definicji** (gridID: `_New_CfgDefElementowExtender_DefElementow`)
- **Filtry** pod listą:
  - `Rodzaj` — typ elementu (31 rodzajów)
  - `Zakres` — Standardowe / Definiowane / **Razem** (pokaż wszystkie)
  - `Stan` — Aktywne / Zablokowane / **Razem** (pokaż wszystkie)

Aby zobaczyć wszystkie definicje, ustaw Zakres=Razem i Stan=Razem.

## Rodzaje elementów wynagrodzenia

| Rodzaj | Klasa elementu | Typowe zastosowanie |
|---|---|---|
| **Etat** | `WypElementEtat` | Wynagrodzenie zasadnicze (mies./godz.), dochód deklarowany |
| **Dodatek** | `WypElementDodatek` | Premie, dodatki (funkcyjny, stażowy), ekwiwalenty, ryczałty, odprawy, potrącenia, korekty, odsetki |
| **Dodatek automatyczny** | `WypElementDodatekAutomatyczny` | Elementy naliczane automatycznie (PPK, potrącenie OPP, wynagrodzenie postojowe) |
| **Nieobecność** | `WypElementNieobecnosc` | Wynagrodzenie za urlopy, zasiłki (chorobowy, macierzyński, opiekuńczy), świadczenia rehabilitacyjne |
| **Nadgodziny I/II/św** | `WypElementNadgodziny` | Dopłata do nadgodzin 50%, 100%, za święta |
| **Nocne** | `WypElementNocne` | Dopłata za godziny nocne |
| **Akord** | `WypElementAkord` | Wynagrodzenie akordowe |
| **Nagroda** | `WypElementNagroda` | Nagroda pieniężna |
| **Kara** | `WypElementKara` | Kara pieniężna |
| **Umowa** | `WypElementUmowa` | Umowy cywilnoprawne, IFT-1, PIT-8A, PIT-R |
| **Umowa rozliczenie** | `WypElementUmowa` | Wyrównania umów zlecenia, kontraktów menedżerskich |
| **Świadczenie** | `WypElementSwiadczenie` | Świadczenia socjalne (z limitem i bez) |
| **Odchyłki** | `WypElementOdchylki` | Rozliczenie odchyłek czasu pracy, przerwy na karmienie |
| **Zaliczka / Zaliczka zwrot** | `WypElementZaliczka` | Zaliczki i ich spłaty |
| **Pożyczka / Pożyczka spłata** | `WypElementPozyczka` | Pożyczki KZP/ZFM, spłaty, umorzenia |
| **Fund poż składka/wpisowe/wycofanie** | — | Składki KZP/ZFM, wpisowe, wycofanie wkładu |
| **Wyrównanie do minimalnej** | `WypElementWyrownanie` | Wyrównanie do minimalnej płacy |
| **Zajęcie komornicze** | `WypElementZajecie` | Alimenty, zajęcia komornicze |
| **Przychód od składki pracodawcy PPK** | — | Przychód PPK, zwroty składek PPK |
| **Zwrot nadpłaty PPK** | — | Zwroty nadpłat PPK |
| **Zbieg pracy i rodzicielstwa** | — | Urlop wychowawczy/macierzyński przy zbiegu z pracą |

## Zakładki formularza definicji

### Ogólne (`DefinicjaElementuProPage`)

| Pole | Opis | Przykładowe wartości |
|---|---|---|
| **Nazwa** | Unikalna nazwa definicji | `Premia procentowa` |
| **Nazwa wyświetlana** | Nazwa wyświetlana operatorowi | `Premia procentowa` |
| **Skrót** | Krótki identyfikator | `Prm.procent` |
| **Kod** | Opcjonalny kod zewnętrzny | — |
| **Blokada** | Czy definicja zablokowana | Nie/Tak |
| **Naliczanie** | Płatna z góry / z dołu | `PłatnaZDołu` |
| **Typ okresu naliczania** | Jak często naliczać | Jednorazowa, Każdy okres, Co n miesięcy |
| **Przyrównuj do najniższego** | Czy wliczać do wyrównania | Tak/Nie |
| **Korygowany** | Czy generować korektę przy zmianie | Tak/Nie |
| **Generuj zerowy element** | Czy generować przy wartości 0 | Tak/Nie |
| **Definicja listy płac** | Do której listy płac | LPE (objectID=3) |
| **Rodzaj wypłaty** | Etat/Umowa/InnaWyplata | `Etat` |
| **Kolejność na listach płac** | Porządek sortowania | 0-99 |
| **Identyfikator** | Identyfikator C# (auto) | `Premia_Procentowa` |

### Deklaracje (`DefinicjaElementuDeklaracjePage`)

Konfiguracja podatków i składek ZUS:

| Sekcja | Kluczowe pola |
|---|---|
| **PIT** | Pozycja PIT, typ zaliczki (wg skali / procentowe), koszty uzyskania, ulga podatkowa |
| **ZUS** | Podstawa składek społecznych, składka zdrowotna, FP, FGŚP, kod RSA |
| **Priorytet** | Priorytet naliczania podatków i składek |

### Nieobecności (`DefinicjaElementuNieobecnosciProPage`)

Konfiguracja wliczania do podstaw urlopów i zasiłków:

| Sekcja | Opcje |
|---|---|
| **Wynagrodzenia za urlop** | NieWliczać, WliczaćAktualnąWartość, WliczaćPoPrzeliczeniu, WliczaćJakZasadnicze |
| **Wynagrodzenia za ekwiwalent** | j.w. + opcja dopełnienia |
| **Zasiłki (pracownicy)** | NieWliczać, WliczaćDla, DopełniaćWedługGodzinDla, DopełniaćJakZasadniczeDla |
| **Zasiłki (inni ubezpieczeni)** | j.w. |

### Algorytm/Ogólne (`DefinicjaElementuAlgorytmPage`)

Główna zakładka konfiguracji algorytmu — szczegóły w `references/algorytmy-naliczania.md`.

### Algorytm/Edytor (`DefinicjaElementuEdytorPage`)

Pole kodu C# (typ `code`) — edytor algorytmu. Aktywny gdy Algorytm = "Edytor algorytmu".

### Algorytm/Podgląd (`DefinicjaElementuPodgladPage`)

Podgląd wygenerowanego kodu C# (read-only).

## Tworzenie nowego elementu — krok po kroku przez MCP

```
1. navigate_to_folder("Ustawienia/Kadry i płace/Płace/Elementy wynagrodzenia")
2. add_subobject(gridID="_New_CfgDefElementowExtender_DefElementow")
3. update_field_value — wypełnij zakładkę Ogólne (nazwa, skrót, naliczanie, lista płac)
4. switch_form_page("DefinicjaElementuAlgorytmPage") — skonfiguruj algorytm
5. [opcjonalnie] switch_form_page("DefinicjaElementuEdytorPage") — wpisz kod C#
6. switch_form_page("DefinicjaElementuDeklaracjePage") — skonfiguruj PIT/ZUS
7. switch_form_page("DefinicjaElementuNieobecnosciProPage") — skonfiguruj wliczanie do podstaw
8. accept_subform() — zatwierdź podformularz
9. save_form() — zapisz do bazy
```

### Wybór algorytmu

| Scenariusz | Zalecany algorytm |
|---|---|
| Prosta kwota lub procent od zasadniczego | **Kreator algorytmu** — wystarczy ustawić Podstawę i Mnożnik |
| Złożona logika (warunki, nietypowe obliczenia) | **Edytor algorytmu** — własny kod C# |
| Naliczanie jak urlop/ekwiwalent | **Jak urlop wypoczynkowy** / **Jak ekwiwalent za urlop** |
| Naliczanie za okres nieobecności | **Za okres nieobecności** |

### Formatowanie kodu w edytorze (przez MCP)

Znaki nowej linii w kodzie C# przez MCP wstawiamy jako `\\n`:

```
update_field_value(["_Tekst=public void Nazwa_Param(...) {\\n    ...\\n}\\n\\npublic Currency Nazwa_Wylicz(...) {\\n    return ...;\\n}"])
```

## Wskazówki przy tworzeniu nowych algorytmów

1. **`Element` to parametr metody** — w kodzie algorytmu `Element` nie jest zmienną globalną ani polem klasy — jest **pierwszym parametrem** każdej metody (`_Param`, `_Wylicz`, `_Wartość1h` itd.). 
   Metoda musi zawsze zawierać te parametry.
   Typ tego parametru musi odpowiadać rodzajowi definiowanego elementu:
   - Etat → `Soneta.Place.WypElementEtat Element`
   - Dodatek → `Soneta.Place.WypElementDodatek Element`
   - Dodatek automatyczny → `Soneta.Place.WypElementDodatekAutomatyczny Element`
   - Nieobecność → `Soneta.Place.WypElementNieobecnosc Element`
   - Nadgodziny → `Soneta.Place.WypElementNadgodziny Element`
   - Umowa → `Soneta.Place.WypElementUmowa Element`

   Przykład: jeśli algorytm odwołuje się do `Element.DodHistoria.Podstawa`, a definicja jest rodzaju **Dodatek**, to sygnatura metody musi wyglądać:
   ```csharp
   public void MojDodatek_Param(WypElementDodatek Element, WypSkladnik Składnik) { ... }
   ```

2. **Wybierz wzorzec** — większość nowych elementów pasuje do jednego z istniejących wzorców. Przeczytaj `references/wzorce-algorytmiczne.md` i zacznij od skopiowania najbliższego wzorca.

3. **Źródło kwoty** — zdecyduj skąd pochodzi podstawa:
   - Kwota z parametrów pracownika → `Element.DodHistoria.Podstawa` (Wzorzec A)
   - Kwota z konfiguracji programu → `module.Config.Zasiłki.*[Date]` (Wzorzec B)
   - Kwota stała z definicji → `Element.Definicja.Algorytm.KreatorAlgorytmu.Podstawa` (Wzorzec C)
   - Procent od zasadniczego → `ZasadniczeNominalne(Date)` + `Element.DodHistoria.Procent` (Wzorzec D)

4. **Czas i dni** — prawie wszystkie algorytmy ustawiają czas i dni z normy:
   ```csharp
   CzasDni cd = Element.Pracownik.Czasy.Norma(Składnik.Okres);
   Składnik.Czas = cd.Czas;
   Składnik.Dni = cd.Dni;
   ```

5. **Pomniejszenie za nieobecności** — użyj kreatora algorytmu z konfiguracją korekt lub ręcznie: `Element.Pracownik.Czasy.Nieobecnosci(Składnik.Okres).Dni`

6. **Proporcjonalność do okresu** — gdy okres składnika jest krótszy niż pełny okres naliczania, przelicz proporcjonalnie (jak w Premii procentowej — Wzorzec D).

7. **Zasiłki** — zawsze używaj `new PodstawaZasiłku(Element)` i `WyliczPodstawęZasiłkuZaDzień(...)` — nie obliczaj podstawy zasiłku ręcznie.

8. **Urlopy okolicznościowe** — używaj `new NaliczanieOkolicznosciowy(Element, Składnik).NaliczPodstawy()` — automatycznie ustawi Podstawa1 (za godziny) i Podstawa2 (za dni).

9. **Ekwiwalenty i odprawy** — używaj `new NaliczanieEkwiwalent(Element, Składnik).NaliczPodstawy()`.

10. **Element.DodHistoria.Podstawa vs Element.DodHistoria.Kwota** — w rzeczywistych algorytmach kreatorowych kwota pobierana jest z `Element.DodHistoria.Podstawa` (nie `.Kwota`). Pole `.Kwota` występuje w dokumentacji, ale `.Podstawa` jest częściej stosowane w generowanym kodzie.

11. **Dodatkowe metody** — jeśli element ma odbiorców płatności (potrącenia, alimenty), zdefiniuj metody `_Odbiorca` i `_RachunekOdbiorcy`. Jeśli okres naliczania wymaga podziału, zdefiniuj `_CięcieOkresu`. Jeśli element wpływa na podstawy urlopów/zasiłków — przeczytaj `references/metody-sterujace-naliczaniem.md`.
