# Dodatkowe sygnatury metod sterujących naliczaniem

Oprócz standardowych metod `_Param`, `_Wylicz`, `_Wartość1h`, `_Odbiorca`, `_RachunekOdbiorcy` i `_CięcieOkresu`, algorytm edytora może definiować metody wpływające na podstawy urlopów i zasiłków.

## _PodstawaUrlopu — sterowanie podstawą urlopu

Metoda wywoływana przez system przy naliczaniu urlopu wypoczynkowego, gdy element powinien być wliczany do podstawy urlopu w niestandardowy sposób:

```csharp
public decimal {Identyfikator}_PodstawaUrlopu(WypElement Urlop, WypElement Element) {
    var wymiar = Element.PracHistoria.Etat.Zaszeregowanie.Wymiar;
    var mies = new YearMonth(Element.Okres.To);
    var normamies = Czasy.KalkPlanu.NormaWym(wymiar, mies).Czas.TotalMinutes;
    var norma = Czasy.KalkPlanu.NormaWym(wymiar, Element.Okres).Czas.TotalMinutes;

    decimal podstawa = (decimal)ZasadniczeNominalne(Element.Okres.To).Value;
    Percent proc = Element.SkładnikGłówny.Procent;

    return podstawa * proc * norma / normamies;
}
```

## _PodstawaZasiłku — sterowanie podstawą zasiłków ZUS

Metoda wywoływana przy naliczaniu zasiłków chorobowych, macierzyńskich itp., gdy element powinien być wliczany do podstawy zasiłku w niestandardowy sposób:

### Wariant prosty — stała kwota

```csharp
public void {Identyfikator}_PodstawaZasiłku(WypElement element, PodstawaZasiłkuArgs args) {
    args.KwotaPodstawy = 1000;
    args.SposóbWliczenia = ElementPodstawyZasilku.SposóbWliczenia.BezDopełniania;
}
```

### Wariant złożony — z obliczeniem

```csharp
public void {Identyfikator}_PodstawaZasiłku(WypElement element, PodstawaZasiłkuArgs args) {
    DoubleCy podstawa = ZasadniczeNominalne(element.Okres.To);
    foreach (WypElement e in element.Elementy[element.Okres])
        if (e.Definicja.Nazwa == "Przestój.")
            podstawa -= e.Wartosc;

    podstawa *= 0.8629;
    args.KwotaPodstawy = (decimal)podstawa.Value;
    args.SposóbWliczenia = ElementPodstawyZasilku.SposóbWliczenia.BezDopełniania;
}
```

## Klasa PodstawaZasiłkuArgs

| Pole | Typ | Opis |
|---|---|---|
| `args.KwotaPodstawy` | `decimal` | Kwota wliczana do podstawy zasiłku |
| `args.SposóbWliczenia` | `ElementPodstawyZasilku.SposóbWliczenia` | Sposób wliczenia (`BezDopełniania` i inne) |
