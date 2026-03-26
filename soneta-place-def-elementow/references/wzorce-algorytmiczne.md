# Wzorce algorytmiczne — wnioski z analizy ~247 rzeczywistych definicji

Znajomość tych wzorców pozwala szybko tworzyć nowe definicje przez analogię. Każdy wzorzec opisuje źródło podstawy, formułę obliczeniową i przykładowe definicje.

## Wzorce dla Dodatków (WypElementDodatek)

### Wzorzec A — Kwota z historii (~10 definicji)

- **Źródło podstawy:** `Element.DodHistoria.Podstawa`
- **Formuła _Wylicz:** `Składnik.Podstawa1`
- **Przykłady:** Dodatek funkcyjny, Premia, Składka PZU, Zasiłek rodzinny

### Wzorzec B — Kwota z konfiguracji (~9 definicji)

- **Źródło podstawy:** `module.Config.Zasiłki.*[Element.Okres.To]`
- **Formuła _Wylicz:** `Składnik.Podstawa1`
- **Przykłady:** Zas.pielęgnacyjny, Zas.pogrzebowy, Zas.porodowy

### Wzorzec C — Kwota z definicji (~3 definicje)

- **Źródło podstawy:** `Element.Definicja.Algorytm.KreatorAlgorytmu.Podstawa`
- **Formuła _Wylicz:** `Składnik.Podstawa1`
- **Przykłady:** Korekta składek ZUS, Korekta zaliczki podatku

### Wzorzec D — ZasadniczeNominalne + Procent/Staż (~2 definicje)

- **Źródło podstawy:** `ZasadniczeNominalne(Element.Okres.To)`
- **Formuła _Wylicz:** `Podstawa1 * Procent`
- **Przykłady:** Premia procentowa, Dodatek stażowy

### Wzorzec E — NaliczanieEkwiwalent (~4 definicje)

- **Źródło podstawy:** `NaliczanieEkwiwalent(Element, Składnik).NaliczPodstawy()`
- **Formuła _Wylicz:** `Podstawa1 * Dni + Podstawa2 * Czas`
- **Przykłady:** Ekwiwalent za urlop, Odprawa emerytalna, Odprawa, Odszkodowanie

### Wzorzec F — OdsetkiZUS (~14 definicji)

- **Źródło podstawy:** `OdsetkiZUS_ParamInt(Element, Składnik)`
- **Formuła _Wylicz:** `Składnik.Podstawa1`
- **Przykłady:** Odsetki wynagr.chorobowe, Odsetki zas.macierzyński

### Wzorzec G — Ryczałt samochodowy (~10 definicji)

- **Źródło podstawy:** Stawka z `SamochodowkaModule.Config.StawkiZaKm.*`
- **Formuła _Wylicz:** `Podstawa1 * Współczynnik * (1 - Dni/22)`
- **Przykłady:** Ryczałt za paliwo, Ryczałt użyt.sam.służb.

### Wzorzec H — Praca zdalna (~3 definicje)

- **Źródło podstawy:** `Element.Module.Config.PracaZdalna.Stawka*`
- **Formuła _Wylicz:** `Podstawa1 * Czas` lub `Podstawa1 * Dni`
- **Przykłady:** Ekwiwalent/ryczałt za pracę zdalną

### Wzorzec I — Dodatki rodzinne (~5 definicji)

- **Źródło podstawy:** `module.Config.Zasiłki.DodRodzinny*` (zależne od wieku/miesiąca)
- **Formuła _Wylicz:** `Składnik.Podstawa1`
- **Przykłady:** Dod. kształcenie, nauka, rok szkolny, urodzenie dziecka

### Wzorzec J — Wyrównanie macierzyńskiego (1 definicja)

- **Źródło podstawy:** `PodwyższenieMacierzyńskiego(Element, Składnik).NaliczPodstawy()`
- **Formuła _Wylicz:** `Max(podstawa - zasiłekNetto, 0)`
- **Przykłady:** Wyrównanie zas. macierzyńskiego

### Wzorzec K — Świadczenie z limitem (1 definicja)

- **Źródło podstawy:** `Element.DodHistoria.Podstawa` + kontrola limitu rocznego
- **Formuła _Wylicz:** Z uwzględnieniem limitu podatkowego
- **Przykłady:** Świadczenie socjalne (z limitem, dodatek)

### Wzorzec L — EdytorAlgorytmu custom (1 definicja)

- **Źródło podstawy:** `ZasadniczeNominalne(Element.Okres.To)`
- **Formuła _Wylicz:** `Podstawa1 - (Podstawa1 * Dni / 28)`
- **Przykłady:** Potrącenie za nieobecność 1/28

---

## Wzorce dla Nieobecności (WypElementNieobecnosc)

### Wzorzec A — PodstawaZasiłku (~51 definicji)

- **Klasa pomocnicza:** `new PodstawaZasiłku(Element)`
- **Formuła _Wylicz:** `WyliczPodstawęZasiłkuZaDzień(...) * Dni`
- **Przykłady:** Zas.chorobowy, Zas.macierzyński, Zas.opiekuńczy, Wynagr.chorobowe, Świad.rehabilitacyjne

### Wzorzec B — NaliczanieOkolicznosciowy (~15 definicji)

- **Klasa pomocnicza:** `new NaliczanieOkolicznosciowy(Element, Składnik).NaliczPodstawy()`
- **Formuła _Wylicz:** `Podstawa1 * Czas + Podstawa2 * Dni`
- **Przykłady:** Wynagr.urlop okolicznościowy, Wynagr.delegacja, Wynagr.badania lekarskie, Wynagr.urlop na poszukiwanie pracy

### Wzorzec C — JakUrlopWypoczynkowy (1 definicja)

- **Klasa pomocnicza:** Wbudowany algorytm
- **Formuła _Wylicz:** Wbudowany
- **Przykłady:** Wynagr.urlop wypoczynkowy

### Wzorzec D — Kreator z Kwotą (1 definicja)

- **Klasa pomocnicza:** `Element.Definicja.Algorytm.KreatorAlgorytmu.Podstawa`
- **Formuła _Wylicz:** `Składnik.Podstawa1.Ceiling(1)`
- **Przykłady:** Urlop wychowawczy

### Wzorzec E — WartośćZasiłku (~3 definicje)

- **Klasa pomocnicza:** `WartośćZasiłku(Element, Składnik, kwota)`
- **Formuła _Wylicz:** `Składnik.Podstawa1.Ceiling(1)`
- **Przykłady:** Zas.wychowawczy (1,2 dziecko / 3... dziecko / osoba samotna)

---

## Wzorce dla Dodatków automatycznych (WypElementDodatekAutomatyczny)

### Wzorzec A — Składki budżetowe (1 definicja)

- **Opis:** `PodstawaSkładekZUSWłaścicielaWakacjeSkładkowe(Element)`
- **Przykłady:** Dochód deklarowany - składki budżetowe

### Wzorzec B — Dofinansowanie (1 definicja)

- **Opis:** Kwota z `Pracownik.BadaniaLekarskie` (LINQ)
- **Przykłady:** Dofinansowanie okulary lub soczewki

### Wzorzec C — Potrącenie OPP (1 definicja)

- **Opis:** `PodstawyPotrąceniaOPP(Element, Składnik)` + metody Odbiorca/Rachunek
- **Przykłady:** Potrącenie OPP

### Wzorzec D — Przychód PPK (~4 definicje)

- **Opis:** `TymczasowoNaliczPodatki` + iteracja elementów wypłaty
- **Przykłady:** Przychód od skł. pracod. PPK (etat/umowa/um.poz/RSP)

### Wzorzec E — Wynagrodzenie postojowe (2 definicje)

- **Opis:** Stawka 1h vs najniższe wynagrodzenie, czas ze strefy przestoju
- **Przykłady:** Wynagrodzenie postojowe (ekonomiczne/KP)

---

## Statystyki definicji w systemie

| Rodzaj | Aktywne + Zablokowane |
|---|---|
| Dodatek | 51 |
| Nieobecność | 70 |
| Umowa | 43 |
| Pożyczka spłata | 10 |
| Przychód PPK | 9 |
| Dodatek automatyczny | 9 |
| Fund poż wycofanie | 6 |
| Fund poż wpisowe | 5 |
| Zbieg pracy i rodzicielstwa | 5 |
| Zajęcie komornicze | 4 |
| Zwrot nadpłaty PPK | 4 |
| Etat | 3 |
| Wyrównanie do minimalnej | 3 |
| Umowa rozliczenie | 3 |
| Nocne | 2 |
| Świadczenie | 2 |
| Odchyłki | 2 |
| Zaliczka | 2 |
| Zaliczka zwrot | 2 |
| Pożyczka | 2 |
| Fund poż składka | 2 |
| Nadgodziny I/II/św | 1+1+1 |
| Akord | 1 |
| Nagroda | 1 |
| Kara | 1 |
| Kurs | 1 |
| Zajęcie komornicze rozlicz depozytu | 1 |
| Zajęcie komornicze zwrot nadpłaty | 1 |
| **Łącznie** | **~247** |
