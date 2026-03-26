# API algorytmów — pola, metody, klasy, typy danych

## Pola WypSkladnik

### Podstawowe pola

| Pole | Typ | Opis |
|---|---|---|
| `Składnik.Podstawa1` | `DoubleCy` | Główna podstawa obliczenia |
| `Składnik.Podstawa2` | `DoubleCy` | Dodatkowa podstawa |
| `Składnik.Czas` | `Time` | Czas pracy (godziny) |
| `Składnik.Dni` | `int` | Liczba dni |
| `Składnik.Procent` | `Percent` | Procent (mnożnik) |
| `Składnik.Okres` | `FromTo` | Okres naliczania składnika |

### Dodatkowe pola (odkryte w rzeczywistych algorytmach)

| Pole | Typ | Opis | Przykład użycia |
|---|---|---|---|
| `Składnik.Podstawa3` | `DoubleCy` | Trzecia podstawa obliczenia | Wyrównanie macierzyńskiego (podstawa dzienna) |
| `Składnik.Wspolczynnik` | `double` | Współczynnik mnożnikowy | Ryczałt samochodowy (liczba km) |
| `Składnik.Ilosc` | `Fraction` | Ilość (wymiar etatu przy zbiegu) | `WymiarEtatuZasiłekPrzyZbiegu(Element)` |
| `Składnik.DataKursu` | `Date` | Data kursu waluty | Przeliczanie walut w algorytmie postojowym |
| `Składnik.Ulamek` | `Fraction` | Ułamek (wymiar etatu) | Mnożenie przez wymiar etatu |

## Metody pomocnicze

### Podstawowe metody

| Metoda | Zwraca | Opis |
|---|---|---|
| `ZasadniczeNominalne(Date)` | `DoubleCy` | Nominalne wynagrodzenie zasadnicze na datę |
| `StawkaZaszeregowania1h(Date)` | `DoubleCy` | Stawka zaszeregowania za 1 godzinę |
| `StawkaZaszeregowaniaNorm1h(Date)` | `DoubleCy` | Stawka zaszereg. normatywna za 1h |
| `Element.Pracownik.Czasy.Norma(FromTo)` | `CzasDni` | Norma czasu pracy (Czas + Dni) |
| `Element.Pracownik.Czasy.Nieobecnosci(FromTo)` | `CzasDni` | Czas nieobecności (Czas + Dni) |
| `Element.Pracownik.StażPracy(Date, PodstawaStazu)` | obiekt z `.Lata` | Staż pracy |
| `Element.Okres` | `FromTo` | Okres elementu (.From, .To) |
| `Element.DodHistoria.Procent` | `Percent` | Procent z parametru dodatku |
| `Element.DodHistoria.Kwota` | `Currency` | Kwota z parametru dodatku |
| `Element.DodHistoria.Podstawa` | `DoubleCy` | Podstawa z parametru dodatku (częściej używana niż .Kwota) |
| `PełnyOkresNaliczania(Element, Składnik)` | `FromTo` | Pełny okres naliczania |
| `NadgodzinyDobaOkres(Element, Składnik)` | `ZestawienieNadgodzin` | Zestawienie nadgodzin (.N50, .N100) |
| `SumaElementow(Element, okres, WypElement.Zasadnicze)` | `DoubleCy` | Suma elementów zasadniczych |

### Dodatkowe metody pomocnicze (odkryte w rzeczywistych algorytmach)

| Metoda | Zwraca | Opis |
|---|---|---|
| `WyliczPodstawęZasiłkuZaDzień(Element, Składnik)` | `DoubleCy` | Podstawa zasiłku za 1 dzień (po waloryzacji, ograniczeniach) |
| `OgraniczeniePodstawyZasiłkuDoKorygowanego(Element, Składnik, wartość)` | `Currency` | Ograniczenie wartości zasiłku do kwoty korygowanego |
| `WymiarEtatuZasiłekPrzyZbiegu(Element)` | `Fraction` | Wymiar etatu dla zasiłku przy zbiegu tytułów |
| `WnagrodzenieZaNadgodziny(Element)` | `DoubleCy` | Wynagrodzenie za nadgodziny do podstawy dopłaty |
| `NajniższeWynagrodzenie1h(Date)` | `DoubleCy` | Minimalne wynagrodzenie za 1h |
| `PrzeliczDniNaGodziny(Date, int)` | `Time` | Przeliczenie dni na godziny wg normy |
| `OdsetkiZUS_ParamInt(Element, Składnik)` | `void` | Naliczenie odsetek od zasiłków ZUS |
| `PodstawyPotrąceniaOPP(Element, Składnik)` | `void` | Naliczenie podstawy potrącenia OPP |
| `WartośćPotrąceniaOPP(Element, Składnik)` | `Currency` | Wartość potrącenia OPP |
| `PodstawaSkładekZUSWłaścicielaWakacjeSkładkowe(Element)` | `DoubleCy` | Podstawa składek z uwzgl. wakacji składkowych |
| `WartośćZasiłku(Element, Składnik, kwota)` | `DoubleCy` | Wartość zasiłku proporcjonalna do okresu |
| `NettoBrutto(Element, wartość)` | `DoubleCy` | Przeliczenie netto na brutto |
| `SpłataZaliczkiNieopodatkowanej(Element, Składnik)` | `DoubleCy` | Spłata zaliczki nieopodatkowanej |
| `WartośćWskaźnika(string nazwa, Date data)` | `DoubleCy` | Wartość wskaźnika |
| `WartośćWskaźnikaObj(string nazwa, Date data)` | `object` | Wartość wskaźnika jako obiekt (do castowania) |

## Klasy i obiekty dostępne w algorytmach

| Klasa / Obiekt | Opis | Przykład dostępu |
|---|---|---|
| `PodstawaZasiłku` | Wylicza podstawę zasiłku chorobowego/macierzyńskiego — `Podstawa`, `Procent` | `new PodstawaZasiłku(Element)` |
| `NaliczanieOkolicznosciowy` | Nalicza wynagrodzenie za urlopy okolicznościowe, delegacje itp. | `new NaliczanieOkolicznosciowy(Element, Składnik).NaliczPodstawy()` |
| `NaliczanieEkwiwalent` | Nalicza podstawę ekwiwalentu za urlop | `new NaliczanieEkwiwalent(Element, Składnik).NaliczPodstawy()` |
| `NaliczanieWypoczynkowy` | Nalicza podstawę urlopu wypoczynkowego (używane w nadgodzinach akordowych) | `new NaliczanieWypoczynkowy(Element, Składnik).NaliczPodstawy()` |
| `PodwyższenieMacierzyńskiego` | Nalicza wyrównanie zasiłku macierzyńskiego | `new PodwyższenieMacierzyńskiego(Element, Składnik).NaliczPodstawy()` |
| `JakEkwiwalentZaUrlop` | Klasa statyczna z metodami kontroli ekwiwalentu | `JakEkwiwalentZaUrlop.KontrolaPrzeliczeniaLimitu(...)` |
| `PlaceModule` | Moduł płac — dostęp do konfiguracji | `Element.Table.Module` lub `Element.Module` |
| `KalendModule` | Moduł kalendarza — strefy, limity nieobecności | `KalendModule.GetInstance(Element)` |
| `SamochodowkaModule` | Moduł samochodówki — stawki za km | `SamochodowkaModule.GetInstance(Element)` |
| `KadryModule` | Moduł kadr — podstawy stażu | `KadryModule.GetInstance(Element)` |
| `DefinicjaStrefy` | Definicja strefy czasu pracy | `KalendModule.GetInstance(Element).DefinicjeStref.WgNazwy["Praca zdalna"]` |
| `DefinicjaStrefy.PrzestójEkonomiczny` | Predefiniowana strefa przestoju ekonomicznego | `Kalend.DefinicjeStref[DefinicjaStrefy.PrzestójEkonomiczny]` |
| `DefinicjaStrefy.PrzestójKP` | Predefiniowana strefa przestoju KP | `Kalend.DefinicjeStref[DefinicjaStrefy.PrzestójKP]` |
| `LimitNieobecnosci` | Limit urlopowy pracownika | `KalendModule.GetInstance(Element).LimNieobecnosci.WgPracownik[...]` |
| `DefinicjaLimitu` | Definicja limitu (np. urlop wypoczynkowy) | `KalendModule.GetInstance(Element).DefinicjeLimitow.UrlopWypoczynkowy` |
| `CzlonekRodziny` | Członek rodziny pracownika | `Element.Dodatek.Rodzina` |

### Dostęp do konfiguracji

| Ścieżka | Opis | Przykład |
|---|---|---|
| `module.Config.Zasiłki.*[Date]` | Kwoty zasiłków indeksowane datą | `module.Config.Zasiłki.Pielęgnacyjny[Element.Okres.To]` |
| `module.Config.PracaZdalna.*[Date]` | Stawki pracy zdalnej | `Element.Module.Config.PracaZdalna.StawkaGodzinowa[Element.Okres.To]` |
| `Płace.Config.Etat.*` | Konfiguracja etatowa (nadgodziny akordowe) | `Płace.Config.Etat.NadgodzinyAkordowe60` |
| `Płace.Config.Ogólne.*` | Konfiguracja ogólna płac | `Płace.Config.Ogólne.PrzestójWPodstawieMinimalnejPłacy` |
| `SamochodowkaModule.Config.StawkiZaKm.*[Date]` | Stawki za km | `sm.Config.StawkiZaKm.SamochodDo900[Element.Okres.To]` |

## Typy danych

| Typ | Opis |
|---|---|
| `Currency` | Wartość pieniężna (z walutą) — typ zwracany przez _Wylicz |
| `DoubleCy` | Wartość zmiennoprzecinkowa z walutą — do obliczeń pośrednich |
| `Time` | Czas (godziny:minuty) |
| `Date` | Data |
| `FromTo` | Zakres dat (From..To) |
| `Percent` | Wartość procentowa |
| `Fraction` | Ułamek (np. wymiar etatu), z polami `.Num` i `.Den` |
| `CzasDni` | Struktura z polami .Czas (Time) i .Dni (int) |
| `YearMonth` | Rok + miesiąc, z właściwością .Days |

## Operacje na typach danych

| Operacja | Składnia | Opis |
|---|---|---|
| Zaokrąglenie w górę | `wartość.Ceiling(1)` | Zaokrąglenie Currency/DoubleCy w górę do 1 miejsca |
| Maximum z dwóch wartości | `DoubleCy.Max(a, b)` | Większa z dwóch wartości DoubleCy |
| Stała zero | `DoubleCy.Zero` | Wartość zerowa DoubleCy |
| Czas zero | `Time.Zero` | Czas zerowy |
| Czas pusty | `Time.Empty` | Czas niezainicjalizowany |
| Sprawdzenie czasu | `Time.ZeroOrEmpty(czas)` | Czy czas jest zerowy lub pusty |
| Maksimum czasu | `Time.Max(a, b)` | Większy z dwóch czasów |
| Rzutowanie | `(Currency)doubleCy` | Konwersja DoubleCy → Currency |
| Rzutowanie | `(decimal)doubleCy.Value` | Konwersja DoubleCy → decimal |
| Pełny miesiąc | `Składnik.Okres.FullMonth` | Rozszerzenie okresu do pełnego miesiąca |
| Dni w miesiącu | `FromTo.Month(date).Days` | Liczba dni w miesiącu zawierającym datę |
| Pierwszy dzień | `date.FirstDayMonth()` | Pierwszy dzień miesiąca |
| Wiek | `new FromTo(from, to).Age` | Obliczenie pełnych lat między datami |
| Przeliczenie waluty | `Element.PrzeliczNaPln(doubleCy)` | Przeliczenie na PLN |
| Tabela kursowa | `Pracownik.GetTabelaKursowa(date)` | Pobranie tabeli kursowej |
| Przeliczenie walut | `tabela.Przelicz(wartość, date, symbol)` | Przeliczenie między walutami |
| Max/Min Currency | `Currency.Max(a, b)` / `Currency.Min(a, b)` | Porównanie wartości Currency |
| Zaokrąglenie | `wartość.Round(0)` | Zaokrąglenie do pełnych złotych |
| Zaokrąglenie DoubleCy | `new DoubleCy(Soneta.Tools.Math.Round(val.Value, 1), val.Symbol)` | Zaokrąglenie DoubleCy do 1 miejsca |

## Sygnatury metod algorytmu

### Metody obowiązkowe

```csharp
// _Param — przygotowanie danych
public void {Identyfikator}_Param({KlasaElementu} Element, WypSkladnik Składnik) { }

// _Wylicz — obliczenie wartości
public Currency {Identyfikator}_Wylicz({KlasaElementu} Element, WypSkladnik Składnik) { return ...; }
```

### Metody opcjonalne

```csharp
// _Wartość1h — wartość za 1 godzinę (dla elementów Etat)
public double {Identyfikator}_Wartość1h({KlasaElementu} Element) { return ...; }

// _Odbiorca — ustalenie odbiorcy płatności (np. dla potrąceń OPP, alimentów)
public IPodmiotKasowy {Identyfikator}_Odbiorca({KlasaElementu} Element)
{
    return Element.PracHistoria.OdpisOPP.Organizacja;
}

// _RachunekOdbiorcy — rachunek bankowy odbiorcy
public RachunekBankowyPodmiotu {Identyfikator}_RachunekOdbiorcy(
    {KlasaElementu} Element, IPodmiotKasowy odbiorca)
{
    return odbiorca == null ? null : odbiorca.DomyslnyRachunek;
}

// _CięcieOkresu — podział okresu naliczania na podokresy
public Periods {Identyfikator}_CięcieOkresu({KlasaElementu} Element, Periods periods)
{
    return ...; // zwraca podzielone okresy
}

// _Podstawa — dodatkowa metoda obliczania podstawy
public DoubleCy {Identyfikator}_Podstawa({KlasaElementu} Element, WypSkladnik Składnik)
{
    return ...; // obliczona podstawa
}
```

### Klasy elementów wg rodzaju (do użycia w sygnaturze)

| Rodzaj | Klasa |
|---|---|
| Etat | `Soneta.Place.WypElementEtat` |
| Dodatek | `Soneta.Place.WypElementDodatek` |
| Dodatek automatyczny | `Soneta.Place.WypElementDodatekAutomatyczny` |
| Nieobecność | `Soneta.Place.WypElementNieobecnosc` |
| Nadgodziny I/II/św | `Soneta.Place.WypElementNadgodziny` |
| Nocne | `Soneta.Place.WypElementNocne` |
| Umowa | `Soneta.Place.WypElementUmowa` |
| Świadczenie | `Soneta.Place.WypElementSwiadczenie` |

## Dostępne wartości enumeracji

### RodzajŹródłaWypłaty

`Etat`, `Nieobecność`, `Umowa`, `Akord`, `Storno`, `Dodatek`, `NadgodzinyI`, `NadgodzinyII`, `NadgodzinyŚw`, `Nocne`, `Kurs`, `Świadczenie`, `Nagroda`, `Kara`, `FundPożWpisowe`, `FundPożWycofanie`, `FundPożSkładka`, `Pożyczka`, `PożyczkaSpłata`, `Zaliczka`, `SpłataZaliczki`

### TypNieobecnosci

`UsprawiedliwionaPłatna`, `NieobecnośćZUS` i inne.

### PrzyczynaNieobecnosci

`UrlopWypoczynkowy`, `ZwolnienieChorobowe` i inne.

### RodzajStawkiZaszeregowania

`Miesieczna`, `Godzinowa`

### TypUbezpieczeniaSpolecznego

`NieNaliczać` i inne.
