# Wynagrodzenie urlopowe — skutki ustawień w praktyce

Jak ustawienia definicji elementów przekładają się na kwotę wynagrodzenia za urlop i ekwiwalentu.
Kod sterujący wliczaniem do podstawy (`_PodstawaUrlopu`): [metody-sterujace-naliczaniem.md](metody-sterujace-naliczaniem.md).

## Wzór

Wynagrodzenie za urlop wypoczynkowy (także na żądanie, dodatkowy, kombatancki, doktorancki, weterana):

```
(stawka części stałej + stawka części zmiennej) × godziny urlopu wg normy
```

Stawka godzinowa (`Podstawa1`) nie jest zaokrąglana — zaokrąglany jest wynik.

| Część | Jak liczona |
|---|---|
| **Stała** (pensja miesięczna) | kwota pomniejszenia pensji za urlop ÷ godziny pomniejszenia — pracownik dostaje dokładnie tyle, o ile pomniejszono pensję |
| **Zmienna** | suma składników z 3 miesięcy (12 dla definicji oznaczonych „znaczne wahania wysokości”) ÷ godziny faktycznie przepracowane |

- Miesiąc bez żadnego dnia pracy jest pomijany razem ze swoimi składnikami; miesiąc bez naliczonej
  wypłaty zatrzymuje cofanie.
- Urlop w pierwszym miesiącu pracy (brak miesięcy do średniej) liczony jest jak okolicznościowy —
  wyłącznie ze składników bieżącego miesiąca.

## Inne rodzaje urlopów

| Rodzaj | Podstawa |
|---|---|
| okolicznościowy, opieka art. 188, szkoleniowy, badania, delegacja, krwiodawstwo | tylko bieżący miesiąc |
| siła wyższa | 50% |
| urlop radcowski | 80% |

## Ustawienia definicji — skutki

- **„Wliczać jak zasadnicze”** na dodatku przejmuje ustawienia elementu zaszeregowania (dla pensji
  miesięcznej: „aktualna wartość”). O wliczeniu w miesiącu urlopu decyduje sposób pomniejszania
  elementu zasadniczego, nie dodatku.
- **„Po przeliczeniu — stawka zaszeregowania 1 h / najniższe 1 h”** przy pensji miesięcznej:
  stawka 1 h = kwota miesięczna ÷ norma danego miesiąca, więc przeliczenie zmienia składniki
  proporcją norm godzin miesięcy.
- **Ręczna podstawa urlopowa za miesiąc** zastępuje wypłaty z tego miesiąca. Kwota „w nominalnej”
  tylko oznacza miesiąc (liczony z 0); „w wypłaconej – znaczne” działa wyłącznie, gdy w bazie
  istnieje definicja oznaczona „znaczne wahania wysokości”.
- **Pracownik tymczasowy** (od VI 2017): stawka = wszystkie wliczane składniki z 3 ostatnich pełnych
  miesięcy (albo 90 dni) ÷ czas przepracowany; podstawa zaokrąglana do grosza.

## Ekwiwalent za urlop

```
za dzień   = (nominalne składniki stałe + średnia miesięczna zmiennych + okresowe w przeliczeniu na miesiąc)
             ÷ średnia norma miesięczna (konfiguracja historyczna, × wymiar etatu)
za godzinę = za dzień ÷ 8 h
```

Przy limicie urlopu prowadzonym w godzinach: wartość = stawka godzinowa × godziny.

## Checklista — weryfikacja kwoty urlopu

- [ ] Część stała równa pomniejszeniu pensji za ten sam okres.
- [ ] Okno części zmiennej: 3 albo 12 miesięcy (definicje „znaczne wahania wysokości”), pominięte miesiące bez pracy.
- [ ] Dodatki „wliczać jak zasadnicze” — sprawdzony sposób pomniejszania elementu zasadniczego.
- [ ] Ręczne podstawy urlopowe w oknie średniej — zastępują wypłaty miesiąca.
- [ ] Zaokrąglony tylko wynik, nie stawka godzinowa.
