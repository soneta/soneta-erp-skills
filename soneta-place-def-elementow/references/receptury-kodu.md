# Receptury kodu — typowe konstrukcje w algorytmach C#

Gotowe fragmenty kodu C# do użycia w edytorze algorytmu. Każda receptura rozwiązuje konkretny problem obliczeniowy.

## Spis treści

1. [Iterowanie po elementach wypłaty](#1-iterowanie-po-elementach-wypłaty)
2. [Wynagrodzenie zasadnicze](#2-wynagrodzenie-zasadnicze)
3. [Nieobecności](#3-nieobecności)
4. [Wymiar etatu](#4-wymiar-etatu)
5. [Czas pracy i kalendarz](#5-czas-pracy-i-kalendarz)
6. [Okresy i daty](#6-okresy-i-daty)
7. [Staż pracy](#7-staż-pracy)
8. [Cechy pracownika (Features)](#8-cechy-pracownika-features)
9. [Wskaźniki](#9-wskaźniki)
10. [Parametry dodatku pracownika (DodHistoria)](#10-parametry-dodatku-pracownika-dodhistoria)
11. [Zaokrąglenia](#11-zaokrąglenia)
12. [Dzielenie z zachowaniem waluty](#12-dzielenie-z-zachowaniem-waluty)
13. [Mnożenie procentu](#13-mnożenie-procentu)
14. [Podatki — mnożnik kosztów i ulgi](#14-podatki)
15. [Przeliczenie Netto → Brutto](#15-przeliczenie-netto-brutto)
16. [Naliczanie urlopów i ekwiwalentów](#16-naliczanie-urlopów-i-ekwiwalentów)
17. [Porównywanie wartości (Max, Min)](#17-porównywanie-wartości)
18. [Porównywanie fragmentu nazwy](#18-porównywanie-fragmentu-nazwy)
19. [Suma czasu pracy w wydziale](#19-suma-czasu-pracy-w-wydziale)
20. [Stawka za km (moduł samochodówki)](#20-stawka-za-km)
21. [Pomniejszenie akordowe](#21-pomniejszenie-akordowe)
22. [Debugowanie i logowanie](#22-debugowanie-i-logowanie)
23. [Sprawdzenie czy pracownik ma akord](#23-sprawdzenie-akordu)
24. [Zaliczka a spłata zaliczki — wydział](#24-zaliczka-wydział)

---

## 1. Iterowanie po elementach wypłaty

### Odczytanie wartości z innego elementu (wg nazwy)

```csharp
Składnik.Procent = Percent.Zero;
foreach (WypElement e in Element.Elementy[Element.Okres])
    if (e.SkładnikGłówny != null)
        if (e.Definicja.Nazwa == "Premia procentowa")
            Składnik.Procent = e.SkładnikGłówny.Procent;
```

### Odczytanie wartości z innego elementu (wg Guid definicji)

```csharp
foreach (WypElement e in Element.Elementy[Element.Okres])
    if (e.Definicja.Guid == new Guid("xxxx-xxxxxx-xxxxxx-xxxx"))
        // operacja na elemencie
```

### Suma elementów o wskazanej nazwie

```csharp
decimal Funkcyjny = 0m;
foreach (WypElement e in Element.Elementy[Element.Okres])
    if (e.Definicja.Nazwa == "Dodatek funkcyjny")
        Funkcyjny += e.Wartosc;
```

### Suma elementów wchodzących do podstawy urlopu

```csharp
decimal Suma = 0m;
foreach (WypElement e in Element.Elementy[Element.Okres])
    if (e.Definicja.Nieobecnosci.Urlop.Typ != 0)
        Suma += e.Wartosc;
```

### Suma elementów o określonym źródle (RodzajŹródłaWypłaty)

```csharp
decimal akt = 0m;
foreach (WypElement e in Element.Elementy[Składnik.Okres])
    if (e.RodzajZrodla == RodzajŹródłaWypłaty.Umowa)
        akt += e.Wartosc;
```

### Suma elementów stanowiących podstawę składek ZUS

```csharp
decimal podstawa = 0m;
foreach (WypElement e in Element.Elementy[Element.Okres])
    if (e.Definicja.Deklaracje.Spoleczne.Typ != TypUbezpieczeniaSpolecznego.NieNaliczać)
        podstawa += e.Wartosc;
```

### Suma elementów wg daty wypłaty (np. do 13-tki)

```csharp
FromTo okres = new Okres(Element.Okres).Rok(0);
Soneta.Business.SubTable st = Płace.WypElementy.WgDaty[Pracownik];
st = new Soneta.Business.SubTable(st, okres);

foreach (WypElement e in st)
    if (e is WypElementEtat)
        Składnik.Podstawa1 += e.Wartosc;
    else if (e.Definicja.Features.GetBool("Podstawa13"))
        Składnik.Podstawa2 += e.Wartosc;
```

### Suma wynagrodzeń akordowych

```csharp
decimal WynagrAkordowe = 0m;
foreach (WypElement e in Element.Elementy[Składnik.Okres])
    if (e is WypElementAkord)
        WynagrAkordowe += e.Wartosc;
```

### Konstrukcja switch/case po nazwach elementów

```csharp
decimal Suma = 0m;
foreach (WypElement e in Element.Elementy[Element.Okres])
    switch (e.Definicja.Nazwa) {
        case "Dodatek funkcyjny":
            Suma += e.Podatki.ZalFIS;
            break;
        case "Premia":
        case "Dodatek stażowy":
            Suma += e.Wartosc;
            // Suma += e.Podatki.KosztyZUS;
            // Suma += e.Netto;
            // Suma += e.DoWypłaty;
            break;
    }
```

---

## 2. Wynagrodzenie zasadnicze

### Wynagrodzenie zasadnicze nominalne (cięte okresami aktualizacji)

```csharp
Time NormaOkres = Czasy.Norma(Składnik.Okres).Czas;
Time NormaMies = Czasy.Norma(Składnik.Okres.FullMonth).Czas;

DoubleCy Podstawa = ZasadniczeNominalne(Element.Okres.To);
Składnik.Podstawa1 = Podstawa * NormaOkres / NormaMies;
```

### Suma elementów zasadniczych (dwie metody)

```csharp
// Metoda 1: SumaElementow
Składnik.Podstawa1 = SumaElementow(Element, Składnik.Okres, WypElement.Zasadnicze);

// Metoda 2: ZasadniczeNominalne
Składnik.Podstawa1 = ZasadniczeNominalne(Element.Okres.To);
```

### Zależność od rodzaju stawki zaszeregowania

```csharp
if (Element.PracHistoria.Etat.Zaszeregowanie.RodzajStawki == RodzajStawkiZaszeregowania.Miesieczna) {
    Składnik.Podstawa1 = Element.DodHistoria.Podstawa;
} else {
    Składnik.Podstawa1 = SumaElementow(Element, Składnik.Okres, WypElement.Zasadnicze);
    Składnik.Procent = Element.DodHistoria.Procent;
}
```

---

## 3. Nieobecności

### Liczba dni nieobecności o wskazanej nazwie

```csharp
int DniNie = 0;
foreach (OkresNieobecności n in Pracownik.Czasy.Nieobecnosci(Element.Okres, true))
    if (n.Definicja.Nazwa == "Urlop opiekuńczy (art 188 kp)")
        DniNie += n.Norma().Dni;

// Kalendarzowa liczba dni nieobecności:
//     DniNie += n.Okres.Days;
```

### Liczba dni nieobecności wg typu

```csharp
Składnik.Dni = 0;
foreach (OkresNieobecności n in Pracownik.Czasy.Nieobecnosci(Element.Okres, true))
    if (n.Definicja.Typ != TypNieobecnosci.UsprawiedliwionaPłatna)
        Składnik.Dni += n.Norma().Dni;
```

### Odwołanie do nieobecności z elementu wynagrodzenia

```csharp
foreach (WypElement e in Pracownik.Elementy[Element.Okres]) {
    var en = e as WypElementNieobecność;
    Nieobecnosc n = en == null ? null : en.Nieobecność;

    if (e.Definicja.Nazwa == "Wynagr.urlop wypoczynkowy")
        Msg(e.Okres + " / " + e.SkładnikGłówny.Podstawa1 + " / " + n.Okres);
}
```

### Pomniejszenie wynagrodzenia zasadniczego o daną nieobecność

```csharp
decimal Pomniejszenia = 0;
foreach (WypElement element in Element.Elementy[Składnik.Okres.FullMonth, WypElement.Zasadnicze])
    foreach (WypSkladnik składnik in element.Skladniki) {
        WypSkladnikPomniejszenie pomniejszenie = składnik as WypSkladnikPomniejszenie;
        Nieobecnosc nieobecność = pomniejszenie == null ? null : pomniejszenie.Nieobecnosc;
        if (nieobecność == Element.Nieobecność)
            Pomniejszenia += składnik.Wartosc;
    }
Składnik.Podstawa1 = -Pomniejszenia;
```

### Pomniejszenia za nieobecności płatne (z elementu zasadniczego)

```csharp
decimal pomniejszenia = 0;
foreach (WypElement element in Element.Elementy[Składnik.Okres, WypElement.Zasadnicze])
    foreach (WypSkladnik składnik in element.Skladniki) {
        WypSkladnikPomniejszenie pomniejszenie = składnik as WypSkladnikPomniejszenie;
        Nieobecnosc nieobecność = pomniejszenie == null ? null : pomniejszenie.Nieobecnosc;
        if (nieobecność != null && nieobecność.Definicja.Typ == TypNieobecnosci.UsprawiedliwionaPłatna)
            pomniejszenia += składnik.Wartosc;
    }
Składnik.Podstawa1 = pomniejszenia;
```

### Sumowanie pomniejszeń z konkretnego elementu wg typu nieobecności

```csharp
decimal podstawa = 0;
foreach (WypElement e in Element.Elementy[Element.Okres])
    if (e.Definicja.Nazwa == "Dodatek stażowy")
        foreach (WypSkladnik skl in e.Skladniki) {
            Nieobecnosc n = skl.Nieobecnosc;
            if (n != null && n.Definicja.Typ == TypNieobecnosci.NieobecnośćZUS)
                podstawa -= skl.Wartosc;
        }
Składnik.Podstawa1 = podstawa;
```

---

## 4. Wymiar etatu

### Przeliczenie przez wymiar etatu (Num/Den)

```csharp
Składnik.Podstawa1 = ZasadniczeNominalne(Element.Okres.To);
// Dzielenie: pełny etat → niepełny (np. 1/2 etatu: Den=2, Num=1)
Składnik.Podstawa1 *= Element.PracHistoria.Etat.Wymiar.Den;
Składnik.Podstawa1 /= Element.PracHistoria.Etat.Wymiar.Num;
```

### Mnożenie przez wymiar etatu (jako ułamek)

```csharp
Składnik.Ulamek = Element.PracHistoria.Etat.Wymiar;
Składnik.Podstawa1 *= Składnik.Ulamek;
```

### Odczytanie historycznego wymiaru etatu

```csharp
int Mianownik = Element.Pracownik[Element.Data].Etat.Wymiar.Den;
int Licznik = Element.Pracownik[Element.Data].Etat.Wymiar.Num;
```

---

## 5. Czas pracy i kalendarz

### Norma czasu pracy w niedziele

```csharp
KalkulatorPlanu Kalk = Czasy.KalkPlanu;
Kalk.LoadOkres(Składnik.Okres);
Time Norma = Time.Zero;
foreach (Date Data in Składnik.Okres)
    if (Data.DayOfWeek == DayOfWeek.Sunday) {
        Dzien DzieńPlanu = Kalk[Data];
        Norma += DzieńPlanu.Czas;
    }
```

### Czas pracy w soboty, święta i w nocy

```csharp
// Czas w święta lub dni wolne
CzasDni cd = Element.Pracownik.Czasy.KalkPracy.Praca(Składnik.Okres, Dzien.Świąteczny);
CzasDni cd = Element.Pracownik.Czasy.KalkPracy.Praca(Składnik.Okres, Dzien.Wolny);
Składnik.Czas = cd.Czas;
Składnik.Dni = cd.Dni;

// Czas pracy w soboty z nocnymi
Time CzasPraca = Time.Zero;
Time CzasPracaNoc = Time.Zero;
KalkulatorPracy Kalk = Czasy.KalkPracy;
Kalk.LoadOkres(Składnik.Okres);
foreach (Date Data in Składnik.Okres)
    if (Data.DayOfWeek == DayOfWeek.Saturday) {
        Dzien DzieńPracy = Kalk[Data];
        CzasPraca += DzieńPracy.Czas;
        FromTo Okres = new FromTo(Data, Data);
        CzasPracaNoc = Element.Pracownik.Czasy.Nocne(Okres);
    }
```

### Odwołanie do strefy czasu pracy

```csharp
DefinicjaStrefy defStrefy = Kalend.DefinicjeStref.WgNazwy["Badania lekarskie"];
CzasDni cd = Pracownik.Czasy.KalkPracy.Praca(Element.Okres, defStrefy);
Time CzasBadania = cd.Czas;
int DniBadania = cd.Dni;
```

### Odwołanie do standardowego kalendarza

```csharp
// Standardowy kalendarz
Kalendarz Std = Kalend.Kalendarze.Standard;
KalkulatorKalendarza KK = new KalkulatorKalendarza(Std);
Time CzasNorma = KK.Norma(Składnik.Okres, null).Czas;

// Kalendarz wg nazwy
Kalendarz Std = (Kalendarz)Kalend.Kalendarze.WgNazwy[TypKalendarza.Kalendarz, "Standard"];

// Kalendarz pracownika
Kalendarz Std = Element.PracHistoria.Etat.Kalendarz;
```

### Odchyłki czasu pracy

```csharp
Odchylka odchylka = Element.Pracownik.Czasy.KalkPracy.Odchylki(Składnik.Okres);
Składnik.Czas = odchylka.Plus - odchylka.Minus;
// lub tylko akordy:
Składnik.Czas = odchylka.Akordy;
```

### Dzielenie przez czas

```csharp
Time CzasPraca = Pracownik.Czasy.Praca(Element.Okres).Czas;
double Norma = 168.0;
Percent Prct = new Percent((decimal)(-(1 - CzasPraca.TotalHours / Norma)));
```

---

## 6. Okresy i daty

### Tworzenie dat i okresów

```csharp
int Rok = Element.Okres.To.Year;
Date Data = new Date(Rok, 01, 02);

// Okres ręczny
FromTo Okres = new FromTo(new Date(Rok, 1, 1), new Date(Rok, 1, 31));

// Okres od początku roku do miesiąca poprzedzającego
int Miesiac = Element.Okres.From.Month;
if (Miesiac > 1)
    Okres = new FromTo(new Date(Rok, 1, 1), new Date(Rok, Miesiac, 1) - 1);
```

### Operacje na YearMonth

```csharp
YearMonth Aktualny = Składnik.Okres.ToYearMonth();
// Okres 3 miesięcy wstecz
FromTo Okres3Mies = new FromTo(Aktualny.AddMonths(-3).FirstDay, Aktualny.AddMonths(-1).LastDay);
```

### Liczba dni kalendarzowych

```csharp
int dniKalendarzowe = new YearMonth(Element.Okres.To).Days;
// lub:
int dniKalendarzowe = Element.Okres.FullMonth.Days;

// Dni zatrudnienia w okresie
int DniZatrKalend = (Element.Okres * Element.PracHistoria.Etat.OkresZatrudnienia).Days;
```

### Pętla po miesiącach (dodatki okresowe)

```csharp
int LiczMies = 0;
DoubleCy Suma = 0.0m;
Periods ps = Periods.New(Element.Okres).BreakByMonth();
foreach (FromTo OkresMsc in ps) {
    if (Czasy.Praca(OkresMsc).Dni > 0) {
        Suma += WartośćWskaźnika("Ekwiwalent za pranie", OkresMsc.To);
        ++LiczMies;
    }
}
Składnik.Podstawa1 = Suma;
Składnik.Dni = LiczMies;
```

### Liczba miesięcy w okresie

```csharp
Periods ps = Periods.New(Element.Okres).BreakByMonth();
Składnik.Ilosc = ps.Count;
```

### Okres ograniczony okresem przyznania dodatku

```csharp
Time CzasNormaOkres = Czasy.Norma(Element.DodHistoria.Okres * Element.Okres).Czas;
```

### Pełny miesiąc kalendarzowy z daty

```csharp
Date dataStażu = Element.Okres.FullMonth.To;
```

---

## 7. Staż pracy

### Staż wg różnych podstaw

```csharp
Date DataStażu = Element.Okres.From;

// Staż razem (wg zdefiniowanej podstawy)
DefPodstawyStazu podstawaStażu
    = KadryModule.GetInstance(Element).DefPodstawStazu.WgNazwy["Zatrudnienie poza firmą"];
int StażRazem = Element.Pracownik.StażPracy(DataStażu, podstawaStażu).Lata;

// Staż w firmie
int StażFirma = Pracownik.StażPracy(DataStażu).Lata;

// Staż poza firmą (zatrudnienie)
int StażPoza = Pracownik.StażPracy(Kadry.DefPodstawStazu.Zatrudnienie).Lata;
```

### Wyliczanie stażu z dowolnego okresu

```csharp
StazPracy Staz = new StazPracy(Okres);
int LataStaz = Staz.Normalizuj().Lata;
```

---

## 8. Cechy pracownika (Features)

### Odczyt cechy historycznej

```csharp
Date Data = Element.Okres.From;
Time CzasNoc = (Time)Element.Pracownik.Features["Czas pracy w nocy", Data];
Decimal Skladnik = (Decimal)Element.Pracownik.Features["Składnik", Data];
```

### Odczyt cechy niehistorycznej (typowanej)

```csharp
Składnik.Procent = Element.Pracownik.Features.GetPercent("Procent dodatku za noce");
Składnik.Czas = Element.Pracownik.Features.GetTime("Czas pracy w nocy");
```

### Sumowanie wartości cechy historycznej w okresie

```csharp
Currency Suma = 0;
Soneta.Business.HistoryFeature Premie = (Soneta.Business.HistoryFeature)Pracownik.Features["Premia"];
foreach (Date Data in Premie.Dates)
    if (Okres.Contains(Data)) {
        Currency Premia = Premie.GetCurrency(Data);
        // lub: Decimal Premia = Premie.GetDecimal(Data);
        Suma += Premia;
    }
```

---

## 9. Wskaźniki

### Odczytanie wartości wskaźnika

```csharp
DoubleCy wartość = WartośćWskaźnika("Ekwiwalent za pranie", Element.Okres.To);
```

### Odczytanie wskaźnika jako obiektu (do dzielenia, castowania)

```csharp
Składnik.Ilosc = (double)WartośćWskaźnikaObj("Współczynnik nagrody", Składnik.Okres.To);
```

### Sklejanie nazwy wskaźnika z danymi pracownika

```csharp
DoubleCy Obrót = WartośćWskaźnika("Obrót " + Wydział.Nazwa, Element.Okres.To);
// lub ze stanowiskiem:
DoubleCy Podstawa = WartośćWskaźnika(
    "Narzędzia /" + Element.Pracownik[OkresMsc.To].Etat.Stanowisko.ToLower(),
    OkresMsc.To);
```

---

## 10. Parametry dodatku pracownika (DodHistoria)

### Odczytanie kwoty i procentu z dodatku

```csharp
Składnik.Podstawa1 = Element.DodHistoria.Podstawa;
Składnik.Procent = Element.DodHistoria.Procent;
```

### Iterowanie po historii dodatku

```csharp
// Wariant 1: po dodatkach pracownika
Składnik.Podstawa1 = 0.0m;
foreach (Dodatek d in Pracownik.Dodatki) {
    DodHistoria dh = d[Element.Okres.From];
    if (dh.Element.Nazwa == "Premia") {
        Składnik.Podstawa1 = dh.Podstawa;
        break;
    }
}

// Wariant 2: po historii jednego dodatku (sumowanie w okresie)
YearMonth Aktualny = Składnik.Okres.ToYearMonth();
FromTo okres = new FromTo(Aktualny.AddMonths(-2).FirstDay, Aktualny.AddMonths(0).LastDay);
Currency suma = 0;
foreach (DodHistoria dh in Element.Dodatek.Historia)
    if (okres.Contains(dh.Aktualnosc.From))
        suma += dh.Podstawa;
```

---

## 11. Zaokrąglenia

```csharp
// Zaokrąglenie DoubleCy do 1 miejsca po przecinku
DoubleCy TmpCy = Składnik.Podstawa1;
Składnik.Podstawa1 = new DoubleCy(Soneta.Tools.Math.Round(TmpCy.Value, 1), TmpCy.Symbol);

// Zaokrąglenie wyniku (Currency) do pełnych złotych
return Składnik.Podstawa1.Round(0);

// Zaokrąglenie w górę
return Składnik.Podstawa1.Ceiling(1);
```

---

## 12. Dzielenie z zachowaniem waluty

```csharp
Składnik.Ilosc = (double)WartośćWskaźnikaObj("Współczynnik nagrody", Składnik.Okres.To);
DoubleCy w = Składnik.Podstawa1 * Składnik.Dni * Składnik.Procent;
return new DoubleCy(w.Value / Składnik.Ilosc, w.Symbol);
```

---

## 13. Mnożenie procentu

```csharp
Percent p = (Percent)WartośćWskaźnikaObj("Pomniejszenie premii", okres.To);
p = new Percent(((decimal)p) * Składnik.Dni);
```

---

## 14. Podatki

```csharp
Decimal KosztyMnoznik = Element.Pracownik[Element.Data].Podatki.KosztyMnoznik;
Decimal UlgaMnoznik = Element.Pracownik[Element.Data].Podatki.UlgaMnoznik;
```

---

## 15. Przeliczenie Netto → Brutto

```csharp
Składnik.Podstawa1 = Element.DodHistoria.Podstawa;
Składnik.Podstawa2 = NettoBrutto(Element, Składnik.Podstawa1);

// lub z wartości decimal:
decimal netto = (decimal)Składnik.Podstawa1.Value;
decimal brutto = NettoBrutto(Element, netto).Value;
```

---

## 16. Naliczanie urlopów i ekwiwalentów

### Urlop wypoczynkowy i okolicznościowy

```csharp
new NaliczanieWypoczynkowy(Element, Składnik).NaliczPodstawy();
// lub:
new NaliczanieOkolicznosciowy(Element, Składnik).NaliczPodstawy();

return (Currency)(Składnik.Podstawa1 * Składnik.Czas)
     + (Currency)(Składnik.Podstawa2 * Składnik.Dni);
```

### Ekwiwalent z parametrami niestandardowymi

```csharp
NaliczanieEkwiwalent Naliczanie = new NaliczanieEkwiwalent(Element, Składnik);
Naliczanie.ŚredniaNormaMiesięczna = 21.0;
Naliczanie.OkresPodstawy = 3;
Naliczanie.WgWymiaruEtatu = false;
Naliczanie.NaliczPodstawy();

return (Currency)(Składnik.Podstawa1 * Składnik.Czas)
     + (Currency)(Składnik.Podstawa2 * Składnik.Dni);
```

---

## 17. Porównywanie wartości

```csharp
Składnik.Podstawa1 = Currency.Max(Składnik.Podstawa1, Składnik.Podstawa2);
Składnik.Podstawa1 = Currency.Min(Składnik.Podstawa1, Składnik.Podstawa2);
```

---

## 18. Porównywanie fragmentu nazwy

```csharp
DoubleCy Podstawa = WartośćWskaźnika("Bony podarunkowe", Element.Okres.To);
if (Element.PracHistoria.Etat.Stanowisko.Length >= 9
    && Element.PracHistoria.Etat.Stanowisko.Substring(0, 9) == "Kierownik")
    Podstawa = WartośćWskaźnika("Bony podarunkowe kier", Element.Okres.To);
```

---

## 19. Suma czasu pracy w wydziale

```csharp
Wydzial Wydział = Element.PracHistoria.Etat.Wydzial;
Time CzasPracaWydz = Time.Zero;

foreach (Pracownik p in Kadry.Pracownicy) {
    Periods ps = Periods.Empty;
    foreach (PracHistoria ph in p.Historia.GetIntersectedRows(Składnik.Okres))
        if (ph.Etat.Wydzial == Wydział)
            ps += ph.Etat.EfektywnyOkres;
    ps = ps.ToFlat() * Składnik.Okres;
    CzasPracaWydz += p.Czasy.KalkPracy.Praca(ps).Czas;
}
Składnik.Czas = CzasPracaWydz;
```

---

## 20. Stawka za km

```csharp
Soneta.Samochodowka.SamochodowkaModule sm
    = Soneta.Samochodowka.SamochodowkaModule.GetInstance(Element);
double v = sm.Config.StawkiZaKm.SamochodPonad900[Element.Okres.From];
return new DoubleCy(v);
```

---

## 21. Pomniejszenie akordowe

```csharp
Składnik.Podstawa1 = 0.0m;
foreach (WypElement e in Element.Elementy[Element.Okres])
    if (e.RodzajZrodla == RodzajŹródłaWypłaty.Etat)
        foreach (WypSkladnik s in e.Skladniki)
            if (s is WypSkladnikOdchyłka.AkordMinus)
                Składnik.Podstawa1 -= s.Wartosc;
```

---

## 22. Debugowanie i logowanie

### Komunikaty Msg (widoczne w obliczeniach elementu)

```csharp
Msg("Premia za 1 sztukę: " + PdstPremii);
Msg("Liczba sztuk: " + LiczbaSztuk);
```

### Zapis obliczeń (widoczny w formularzu elementu)

```csharp
Składnik.Podstawa1 = 150;
Element.ZapisObliczen = "Podstawa: " + Składnik.Podstawa1;
```

### Logi systemowe

```csharp
Soneta.Business.Log log = new Soneta.Business.Log("Log do premii");
log.WriteLine("Naliczanie premii uznaniowej dla: " + Pracownik);
log.WriteLine(e.Nazwa + ": " + e.Wartosc);
```

---

## 23. Sprawdzenie akordu

```csharp
var jestAkord = !Pracownik.Akordy.IsEmpty;
```

---

## 24. Zaliczka wydział

```csharp
var wydzialZaliczka = Element.Zaliczka.Realizacja.Wydzial;
var wydzialSplata = Element.Wydzial;

if (wydzialSplata == wydzialZaliczka)
    Składnik.Podstawa1 = SpłataZaliczkiNieopodatkowanej(Element, Składnik);
```
