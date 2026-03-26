# Algorytmy naliczania — kluczowa konfiguracja

## Typy algorytmów

| Typ | Opis |
|---|---|
| **Kreator algorytmu** | Konfigurator wizualny — definiujesz podstawę, mnożnik, korekty. System generuje kod C#. |
| **Edytor algorytmu** | Ręczny kod C# w edytorze. Pełna kontrola nad logiką. |
| **Klasa algorytmu** | Wskazanie klasy C# z zewnętrznego assembly. |
| **Za okres nieobecności** | Wbudowany algorytm dla nieobecności. |
| **Jak ekwiwalent za urlop** | Wbudowany algorytm ekwiwalentu. |
| **Jak urlop wypoczynkowy** | Wbudowany algorytm urlopu wypoczynkowego. |
| **Jak urlop okolicznościowy** | Wbudowany algorytm urlopu okolicznościowego. |

## Kreator algorytmu — parametry

### Podstawa

| Typ podstawy | Opis | Przykład użycia |
|---|---|---|
| `Kwota` | Stała kwota z parametru elementu | Premia kwotowa |
| `Wyrażenie` | Wyrażenie C# jako podstawa | Wynagrodzenie zasadnicze |
| `Kod metody` | Wywołanie metody C# | Zaawansowane obliczenia |
| `ZasadniczeNominalne` | Nominalne wynagrodzenie zasadnicze | Dodatek stażowy, premia procentowa |
| `ZasadniczeRzeczywiste` | Rzeczywiste wynagrodzenie zasadnicze | — |
| `Brutto` | Kwota brutto wypłaty | — |
| `BruttoBezZasiłków` | Brutto bez zasiłków | — |
| `StawkaZaszeregowania1h` | Stawka godzinowa | Nadgodziny |
| `PodstawaUrlopuWypoczynkowego1h` | Podstawa urlopu za 1h | — |
| `PodstawaEkwiwalentu1h` | Podstawa ekwiwalentu za 1h | — |
| `NajniższeWynagrodzenie1h` | Minimalna stawka za 1h | — |

### Pomniejszenie za okres niezatrudnienia

| Wartość | Opis |
|---|---|
| `NiePomniejszany` | Bez pomniejszenia |
| `Proporcjonalnie` | Proporcjonalnie do dni zatrudnienia |
| `JednaTrzydzista` | 1/30 za każdy dzień niezatrudnienia |
| `DniKalendarzowe` | Wg dni kalendarzowych |

### Mnożnik

| Typ | Opis |
|---|---|
| `BezWspółczynnika` | Bez mnożnika |
| `Procent` | Procent z parametru elementu |
| `Ułamek` | Ułamek (np. 1/3) |
| `Współczynnik` | Stały współczynnik |
| `ZależnyOdStażuPracy` | Tabela procentowa wg lat stażu |

### Korekta za nieobecności

Dla każdego typu nieobecności oddzielnie:

| Wartość | Opis |
|---|---|
| `NiePomniejsza` | Brak pomniejszenia |
| `Proporcjonalnie` | Proporcjonalnie do godzin nieobecności |
| `JednaTrzydziesta` | 1/30 za każdy dzień nieobecności |
| `JakZasadnicze` | Tak samo jak wynagrodzenie zasadnicze |
| `ZaKażdyDzień` | Za każdy dzień kalendarzowy |
| `Algorytm` | Własny algorytm pomniejszenia |

Typy nieobecności: nieusprawiedliwione, niepłatne, płatne (urlopy), wychowawcze, macierzyńskie, rehabilitacyjne, opiekuńcze, chorobowe.

### Zależność od czasu pracy

| Wartość | Opis |
|---|---|
| `NieZależyOdCzasu` | Bez zależności od czasu |
| `Godzinę` | Wartość za godzinę × czas pracy |
| `Miesięcznie` | Wartość miesięczna z korektami za nieobecności |

## Edytor algorytmu — struktura kodu C#

Algorytm edytora wymaga **dwóch metod publicznych**:

```csharp
// Metoda _Param — przygotowanie danych do obliczeń
public void {Identyfikator}_Param({KlasaElementu} Element, WypSkladnik Składnik)
{
    // Ustaw podstawy, czas, dni, procent
}

// Metoda _Wylicz — obliczenie końcowej wartości
public Currency {Identyfikator}_Wylicz({KlasaElementu} Element, WypSkladnik Składnik)
{
    // Zwróć obliczoną wartość elementu
    return ...;
}
```

Opcjonalna trzecia metoda (dla elementów Etat):
```csharp
// Metoda _Wartość1h — wartość za 1 godzinę (używana przez inne elementy)
public double {Identyfikator}_Wartość1h({KlasaElementu} Element)
{
    return ...;
}
```

### Klasy elementów wg rodzaju

| Rodzaj | Klasa w sygnaturze metody |
|---|---|
| Etat | `Soneta.Place.WypElementEtat` |
| Dodatek | `Soneta.Place.WypElementDodatek` |
| Dodatek automatyczny | `Soneta.Place.WypElementDodatekAutomatyczny` |
| Nieobecność | `Soneta.Place.WypElementNieobecnosc` |
| Nadgodziny I/II/św | `Soneta.Place.WypElementNadgodziny` |
| Nocne | `Soneta.Place.WypElementNocne` |
| Umowa | `Soneta.Place.WypElementUmowa` |
| Świadczenie | `Soneta.Place.WypElementSwiadczenie` |

**Uwaga:** Klasa `WypElementDodatekAutomatyczny` jest odrębna od `WypElementDodatek` — algorytmy dodatków automatycznych muszą używać tej klasy w sygnaturze.

## Przykłady kompletnych algorytmów C#

### Wynagrodzenie zasadnicze miesięczne (Etat)

Podstawa: wyrażenie `StawkaZaszeregowania1h * Norma.Czas`

```csharp
public void Wynagrodzenie_Zasadnicze_Mies__Param(
    Soneta.Place.WypElementEtat Element, WypSkladnik Składnik)
{
    DoubleCy podstawa1 = StawkaZaszeregowania1h(Element.Okres.To)
        * Pracownik.Czasy.Norma(Składnik.Okres).Czas;
    Składnik.Podstawa1 = podstawa1;

    CzasDni cd = Element.Pracownik.Czasy.Norma(Składnik.Okres);
    Składnik.Czas = cd.Czas;
    Składnik.Dni = cd.Dni;
}

public Currency Wynagrodzenie_Zasadnicze_Mies__Wylicz(
    Soneta.Place.WypElementEtat Element, WypSkladnik Składnik)
{
    return Składnik.Podstawa1;
}

public double Wynagrodzenie_Zasadnicze_Mies__Wartość1h(
    Soneta.Place.WypElementEtat Element)
{
    return Element.PrzeliczNaPln(StawkaZaszeregowania1h(Element.Okres.To)).Value;
}
```

### Dodatek stażowy (Dodatek — zależny od stażu pracy)

Podstawa: ZasadniczeNominalne, Mnożnik: ZależnyOdStażuPracy

```csharp
public void Dodatek_Stażowy_Param(
    Soneta.Place.WypElementDodatek Element, WypSkladnik Składnik)
{
    DoubleCy podstawa1 = ZasadniczeNominalne(Element.Okres.To);
    Składnik.Podstawa1 = podstawa1;

    WspolczynnikDefElementu współczynnik = Element.Definicja.Algorytm.KreatorAlgorytmu.Wspolczynnik;
    Date dataStażu = współczynnik.PracaWFirmie ? Element.Okres.To : Date.Empty;
    int lataStażu = Element.Pracownik.StażPracy(dataStażu, współczynnik.PodstawaStazu).Lata;
    Składnik.Procent = współczynnik[lataStażu];

    CzasDni cd = Element.Pracownik.Czasy.Norma(Składnik.Okres);
    Składnik.Czas = cd.Czas;
    Składnik.Dni = cd.Dni;
}

public Currency Dodatek_Stażowy_Wylicz(
    Soneta.Place.WypElementDodatek Element, WypSkladnik Składnik)
{
    return (Składnik.Podstawa1 * Składnik.Procent);
}
```

### Premia procentowa (Dodatek — procent od zasadniczego)

Podstawa: ZasadniczeNominalne, Mnożnik: Procent (z parametru dodatku)

```csharp
public void Premia_Procentowa_Param(
    Soneta.Place.WypElementDodatek Element, WypSkladnik Składnik)
{
    DoubleCy podstawa1 = ZasadniczeNominalne(Element.Okres.To);
    FromTo okres = PełnyOkresNaliczania(Element, Składnik);

    if (okres == Składnik.Okres)
        Składnik.Podstawa1 = podstawa1;
    else {
        // Przeliczenie proporcjonalne gdy okres składnika != pełny okres
        Time tokr = Czasy.KalkPlanu.NormaWym(Fraction.One, new Time(8,0), Składnik.Okres).Czas;
        Time tpełny = Czasy.KalkPlanu.NormaKodeksowaWym(Fraction.One, new Time(8,0), okres).Czas;
        if (tpełny == Time.Zero)
            Składnik.Podstawa1 = DoubleCy.Zero;
        else if (tokr > tpełny)
            Składnik.Podstawa1 = podstawa1;
        else
            Składnik.Podstawa1 = podstawa1 * tokr / tpełny;
    }

    Składnik.Procent = Element.DodHistoria.Procent;
    CzasDni cd = Element.Pracownik.Czasy.Norma(Składnik.Okres);
    Składnik.Czas = cd.Czas;
    Składnik.Dni = cd.Dni;
}

public Currency Premia_Procentowa_Wylicz(
    Soneta.Place.WypElementDodatek Element, WypSkladnik Składnik)
{
    return (Składnik.Podstawa1 * Składnik.Procent);
}
```

### Premia kwotowa (Dodatek — prosta kwota)

Podstawa: Kwota

```csharp
public void Premia_Param(
    Soneta.Place.WypElementDodatek Element, WypSkladnik Składnik)
{
    DoubleCy podstawa1 = Element.DodHistoria.Kwota;
    Składnik.Podstawa1 = podstawa1;

    CzasDni cd = Element.Pracownik.Czasy.Norma(Składnik.Okres);
    Składnik.Czas = cd.Czas;
    Składnik.Dni = cd.Dni;
}

public Currency Premia_Wylicz(
    Soneta.Place.WypElementDodatek Element, WypSkladnik Składnik)
{
    return Składnik.Podstawa1;
}
```

### Dopłata do nadgodzin 50% (Nadgodziny I)

Podstawa: StawkaZaszeregowania1h + wynagrodzenie za nadgodziny, Mnożnik: 50%

```csharp
public void Dopłata_Do_N_Godz_50__Param(
    Soneta.Place.WypElementNadgodziny Element, WypSkladnik Składnik)
{
    DoubleCy podstawa1;
    if (!Element.PracHistoria.Etat.WynagrodzenieAkordowe
        || !Płace.Config.Etat.NadgodzinyAkordowe60)
        podstawa1 = Element.PracHistoria.Etat.Kalendarz.Nadgodziny.PodstawaWgNormyKP
            ? StawkaZaszeregowaniaNorm1h(Element.Okres.To) + WnagrodzenieZaNadgodziny(Element)
            : StawkaZaszeregowania1h(Element.Okres.To) + WnagrodzenieZaNadgodziny(Element);
    else
        using (Soneta.Business.LogCatcher listener = new Soneta.Business.LogCatcher("Urlop")) {
            using (Pracownik.Session.Logout(true)) {
                var naliczanie = new NaliczanieWypoczynkowy(Element, Składnik);
                naliczanie.NaliczPodstawy();
                var procent = new Percent(0.6m);
                podstawa1 = Składnik.Podstawa1 * procent;
                Składnik.Podstawa1 = Składnik.Podstawa2 = DoubleCy.Zero;
            }
        }
    Składnik.Podstawa1 = podstawa1;

    Składnik.Procent = Element.Definicja.Algorytm.KreatorAlgorytmu.Wspolczynnik.Procent;
    ZestawienieNadgodzin zestNad = NadgodzinyDobaOkres(Element, Składnik);
    Składnik.Czas = zestNad.N50;
}

public Currency Dopłata_Do_N_Godz_50__Wylicz(
    Soneta.Place.WypElementNadgodziny Element, WypSkladnik Składnik)
{
    return ((Currency)(Składnik.Podstawa1 * Składnik.Procent) * Składnik.Czas);
}
```

### Potrącenie za nieobecność 1/28 (Dodatek — edytor algorytmu, niestandardowe)

Podstawa: ZasadniczeNominalne, pomniejszenie o 1/28 za każdy dzień nieobecności.

```csharp
public void Potrącenie_Za_Nieobecność_1_28_Param(
    Soneta.Place.WypElementDodatek Element, WypSkladnik Składnik)
{
    DoubleCy podstawa = ZasadniczeNominalne(Element.Okres.To);
    Składnik.Podstawa1 = podstawa;

    int dniNieobecnosci = Element.Pracownik.Czasy.Nieobecnosci(Składnik.Okres).Dni;
    Składnik.Dni = dniNieobecnosci;

    CzasDni cd = Element.Pracownik.Czasy.Norma(Składnik.Okres);
    Składnik.Czas = cd.Czas;
}

public Currency Potrącenie_Za_Nieobecność_1_28_Wylicz(
    Soneta.Place.WypElementDodatek Element, WypSkladnik Składnik)
{
    return Składnik.Podstawa1 - (Składnik.Podstawa1 * Składnik.Dni / 28);
}
```
