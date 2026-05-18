# Cechy (Features) - dodatkowe informacje na obiektach biznesowych

Cechy to dodatkowe, dynamicznie definiowane informacje przypisane do obiektów biznesowych (Row). Każda tabela biznesowa ma własny zestaw cech, identyfikowanych po nazwie. Definicje przechowywane są w tabeli `FeatureDefs` (`FeatureDefinition`). Ich tworzenie i modyfikacja **nie wymaga konwersji bazy danych** — cechy mogą być definiowane podczas wdrożenia.

Cechy występują w dwóch wariantach:

- **Bazodanowe** — wartość przechowywana w tabeli `Features` powiązanej z daną tabelą biznesową.
- **Algorytmiczne** — wartość obliczana w locie przez algorytm; ustawienie cechy także wykonuje algorytm.

## Struktura tabeli Features

```
[Parent]      [int]           NOT NULL  -- ID Row obiektu biznesowego
[ParentType]  [nvarchar(16)]  NOT NULL  -- tabela, do której zarejestrowano cechę
[Name]        [nvarchar(30)]  NOT NULL  -- nazwa cechy
[Lp]          [int]           NOT NULL  -- 0 dla zwykłych cech; >0 dla historycznych / wielowartościowych
[DataKey]     [nvarchar(30)]  NULL      -- główne, indeksowane pole z wartością
[Data]        [nvarchar(3000)] NULL     -- duplikat DataKey (≤30 znaków) lub właściwa wartość (>30 znaków)
```

`Parent` + `ParentType` jednoznacznie identyfikują biznesowy Row, do którego należy cecha. `Lp` rozdziela kolejne wpisy cech historycznych lub wielowartościowych.

`DataKey` jest indeksowany — używaj go do wyszukiwań. Gdy zapis mieści się w 30 znakach, `Data` zawiera tę samą wartość; dla dłuższych — pełną wartość trzyma `Data`.

## Typy cech i ich reprezentacja fizyczna

Poniższa tabela obejmuje typy liczbowe, daty/czasu, walutowe i jednostkowe (`FeatureTypeNumber`).

| Caption | FeatureTypeNumber | Value | Soneta.Type | Przykład zapisu w DataKey/Data | Uwagi |
|---|---|---|---|---|---|
| Liczba całkowita | `Int` | 0 | `int` | `         44` | string 10-znakowy, wyrównany do prawej spacjami |
| Warunek | `Bool` | 1 | `bool` | `1` lub `0` | zapis jak `int` |
| Kwota | `Decimal` | 3 | `Decimal` | `          1.01000000` | 2 miejsca po przecinku w UI, fizycznie decimal 8 m.p., string 20 znaków, wyrównany do prawej |
| Liczba rzeczywista | `Double` | 4 | `Double` | `          1.01234568` | 8 miejsc po przecinku, string 20 znaków, wyrównany do prawej |
| Data | `Date` | 5 | `Date` | `2025-01-22` | format `rrrr-MM-dd` |
| Czas | `Time` | 6 | `Time` | `0000030:20` | format `hhhhhhh:mm` — 7 znaków totalHours + `:` + minuty |
| Okres dat | `FromTo` | 7 | `FromTo` | `2025-04-01...2025-04-30` | dwie daty `rrrr-MM-dd` rozdzielone `...` |
| Ułamek | `Fraction` | 8 | `Fraction` | `12345/6789` | dwa inty rozdzielone `/` |
| Kwota z walutą | `Currency` | 9 | `Currency` | `PLN           1.12000000` | 2 m.p. po zaokrągleniu, sztywny string 24-znakowy |
| Procent | `Percent` | 10 | `Percent` | `          0.20110000` | 20,11% zapisane jako `0.2011` |
| Liczba z walutą | `DoubleCy` | 11 | `DoubleCy` | `PLN           1.12345679` | 8 m.p., string 24-znakowy |
| Miesiąc w roku | `YearMonth` | 12 | `YearMonth` | `2025/02` | rok i miesiąc rozdzielone `/` |
| Czas z dokładnością do sekundy | `TimeSec` | 17 | `TimeSec` | `30:20:10` | format `hh:mm:ss` |
| Ilość | `Amount` | 18 | `Amount` | `szt         100.40000000` | jak `Currency`, ale jednostka opcjonalna |

## Dostęp do cechy w kodzie

### Dostęp nietypowany przez indeksator Row

```csharp
Row row = ...;
object v = row["NazwaCechy"];
if (v == null) {
    // cecha nie istnieje
}
```

### Dostęp typowany przez Row.Features

```csharp
Row fv = Session.GetHandel().DokHandlowe[123];

bool     b  = fv.Features.GetBool("CechaBool");
Currency c  = fv.Features.GetCurrency("CechaCurrency");
Date     dt = fv.Features.GetDate("CechaDate");
string   s  = fv.Features.GetString("CechaString");
int      i  = fv.Features.GetInt("CechaInt");
decimal  d  = fv.Features.GetDecimal("CechaDecimal");
```

### Ustawienie wartości

Tak jak każda modyfikacja Row, ustawienie cechy wymaga transakcji edycyjnej:

```csharp
using (var t = Session.Logout(editMode: true)) {
    fv["CechaString"] = "Nowa wartość cechy";
    fv["CechaInt"]    = 12345;
    t.Commit();
}
```

Dla cech algorytmicznych przypisanie wartości wywołuje algorytm zdefiniowany w `FeatureDefinition`.

## Bindowanie do cech w form.xml

W formularzach UI cechy adresuje się przez ścieżkę `Features.NazwaCechy` — bezpośrednio na bieżącym Row lub przez relację do innego obiektu:

```xml
<Field EditValue="{Features.CechaBool}"   CaptionHtml="Cecha Bool" />
<Field EditValue="{Features.CechaDate}"   CaptionHtml="Cecha Date" />
<Field EditValue="{Features.CechaString}" CaptionHtml="Cecha String" />
<Field EditValue="{Kontrahent.Features.CechaDecimal}" CaptionHtml="Cecha Decimal kontrahenta" />
```

## Filtrowanie View po cechach

`Features.X` nie jest typowaną property klasy Row. Dla filtrów po cechach używaj `FieldCondition` ze string-path `"Features.NazwaCechy"`:

```csharp
view.Condition &= new FieldCondition.Equal("Features.GrupaTowaru", "Telewizor");
view.Condition &= new FieldCondition.GreaterEqual("Features.Przekatna", 50);
view.Condition &= new FieldCondition.Equal("Features.SmartTV", true);
```
