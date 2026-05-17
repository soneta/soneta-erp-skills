# RowCondition - serwerowe filtrowanie danych

`RowCondition` to mechanizm tworzenia warunków filtrujących wczytywane wiersze **na poziomie serwera SQL**. Warunki są tłumaczone na klauzulę `WHERE` zapytania SQL, dzięki czemu z bazy do pamięci aplikacji trafiają wyłącznie wiersze spełniające kryteria. Jest to **podstawowy sposób efektywnego odczytu danych** w logice biznesowej.

Najwygodniejsze API to budowa warunku z wyrażeń LINQ (`Expression<Predicate<TRow>>`) przez `RowCondition.FromExpression(...)` oraz aplikowanie wyrażeń bezpośrednio do `SubTable` i `View` przez indeksator i `AddExpression(...)`.

## Najważniejsze zasady

* W wyrażeniu LINQ można odwoływać się **wyłącznie do pól bazodanowych** (kolumn tabeli, pól złożonych, kolekcji powiązanych, cech). Próba użycia pola niebazodanowego rzuca `LinqConditionException`.
* Po lewej i prawej stronie operatora porównania może wystąpić wyrażenie liczone po stronie klienta lub pole bazodanowe - kolejność jest dowolna (`row.Prop == wyr` lub `wyr == row.Prop`).
* Po stronie klienta można używać dowolnych pól, właściwości, wywołań metod, wyrażeń arytmetycznych - są one zewaluowane przed wysłaniem do SQL i wstawione jako stała.
* Wszystkie porównania tekstowe są **case-insensitive**.
* Wyrażenie LINQ jest tłumaczone na SQL, ale **kompilator C# wymusza poprawność typów** - błędy wykrywane są na etapie budowy projektu, a nie wykonania.

## Wzorce użycia w kodzie

### 1. Indeksator `SubTable[expression]` - logika biznesowa

Najwygodniejszy sposób odczytu odfiltrowanych danych w kodzie biznesowym. Zwraca nowy `SubTable` zawierający tylko pasujące wiersze:

```csharp
var st = Session.GetHandel().DokHandlowe.WgMagazyn[
    dok => dok.Kontrahent.Kod == "ABC"
];

foreach (DokumentHandlowy dok in st) {
    // ... operacje na odfiltrowanych dokumentach
}
```

Generowane SQL łączy tabele referencyjne automatycznie (`LEFT OUTER JOIN`) i osadza warunek w `WHERE`:

```sql
select * from DokHandlowe t0
left outer join Kontrahenci t1 on t0.Kontrahent = t1.ID
where t1.[Kod] = @?
order by t0.[Magazyn], t0.[Data], t0.[Czas], t0.[ID]
```

Sortowanie pochodzi z klucza wybranego indeksatorem (tu: `WgMagazyn`).

### 2. `View.AddExpression(...)` - listy w UI

`View` to obiekt warstwy UI używany do prezentacji list. Można na niego nakładać kolejne warunki, które są łączone operatorem `AND`:

```csharp
View view = Session.GetTowary().Towary.PrimaryKey.CreateView();

// Generyczna wersja - jawny typ wiersza
view.AddExpression<Towar>(t => LinqConditionMethods.Like(t.Kod, "B*"));

// Wersja z jawnym castem w wyrażeniu - przydatna dla cech (indeksatora `row["..."]`)
view.AddExpression((Towar t) => t["Asortyment"] == null);
```

`AddExpression` można wywoływać wielokrotnie - każdy warunek dokłada koniunkcję do zapytania.

### 3. `Query.Table.AddExpression(...)` - zapytania niskopoziomowe

Stosowane w niskopoziomowym budowaniu zapytań (`Query`). API identyczne jak dla `View`.

### 4. `RowCondition.FromExpression(...)` - jawne budowanie warunku

Gdy warunek ma być przekazywany jako wartość (parametr, zwracany z metody), buduje się go jawnie:

```csharp
var cond = RowCondition.FromExpression<Towar>(
    t => t.Dostawca.Kod == "ABC" && t.Typ == TypTowaru.Towar
);
```

Powstały obiekt można potem podać do `SubTable`, `View`, `Query`. Tłumaczenie na string SQL-like uzyskuje się przez `cond.ToString()` - przydatne w testach jednostkowych.

## Zakres możliwych wyrażeń

Poniższe sekcje opisują, co można umieścić wewnątrz `Expression<Predicate<TRow>>`. Każda pozycja ma przykład C# oraz - tam gdzie to istotne - postać po `ToString()` (zgodną z konwencją trzymaną w testach).

### Odwołania do pól

```csharp
// Pole proste
t => t.Kod == "ABC"

// Pole referencyjne (JOIN po referencji)
t => t.Dostawca.OddzialFirmy.Nazwa == "Nazwa"
//  -> [Dostawca.OddzialFirmy.Nazwa]='Nazwa'

// Łańcuch referencji z polem złożonym
t => t.Dostawca.Kontakt.EMAIL == "a@a.com"
//  -> [Dostawca.Kontakt.EMAIL]='a@a.com'

// Pole prywatne / protected - dostęp przez indeksator z castem
op => (string)op["Password", RowVersion.Original] == "ABC"
//  -> Password='ABC'

// Cecha (feature) - indeksator po nazwie cechy, prawa strona jako (object)
op => op["NAME"] == (object)"VALUE"
//  -> [Features.NAME]='VALUE'

// Cecha na obiekcie referencyjnym
t => t.Dostawca["NAME"] != (object)"VALUE"
//  -> [Dostawca.Features.NAME]<>'VALUE'

// Porównanie dwóch kolumn tego samego wiersza
op => op.Name == op.FullName
//  -> [Name]=[FullName]
```

### Wartości po stronie klienta

Można odwoływać się do pól/zmiennych/metod zdefiniowanych po stronie kodu - są one liczone raz, przed wysłaniem do SQL, i osadzane jako stała:

```csharp
private readonly string FieldString = "ABC";
private string PropertyString => "ABC";

op => op.Name == FieldString                       // Name='ABC'
op => op.Name == PropertyString                    // Name='ABC'
op => op.Name == PropertyString.Substring(1, 2)    // Name='BC'
```

### Typy proste, enum, int

```csharp
op => op.ID == 123       // ID='123'
op => op.ID != 123
op => op.ID >  123
op => op.ID >= 123
op => op.ID <  123
op => op.ID <= 123

// Kolejność może być odwrócona
op => 123 > op.ID        // ID<'123'

// Enum
t => t.Typ == TypTowaru.Receptura    // Typ=Receptura
```

### Bool

```csharp
op => op.Locked          // Locked
op => !op.Locked         // NOT Locked
```

### String

Wszystkie porównania są case-insensitive.

```csharp
op => op.Name == "ABC"               // Name='ABC'
op => "ABC" != op.Name               // Name<>'ABC'

// Większość/mniejszość przez CompareTo - na obiekcie lub statycznie:
op => op.Name.CompareTo("ABC") > 0          // Name>'ABC'
op => op.Name.CompareTo("ABC") >= 0
op => op.Name.CompareTo("ABC") <  0
op => op.Name.CompareTo("ABC") <= 0
op => 0 > op.Name.CompareTo("ABC")          // Name<'ABC' (kolejność odwrócona)
op => string.Compare(op.Name, "ABC") > 0    // Name>'ABC'
op => 0 < string.Compare(op.Name, "ABC")    // Name>'ABC'
op => 0 < string.Compare("ABC", op.Name)    // Name<'ABC'

// Dopasowanie podciągu / prefiksu / sufiksu - generuje SQL LIKE z escapowaniem znaków '*' i '%'
op => op.Name.Contains("A*B%C")             // Name like '%A[*]B[%]C%'
op => op.Name.StartsWith("A*B%C")           // Name like 'A[*]B[%]C%'
op => op.Name.EndsWith("ABC")               // Name like '%ABC'

// Pełna maska SQL LIKE (znaki '%' i '_' traktowane jako wildcardy)
op => LinqConditionMethods.Like(op.Name, "AB%C")    // Name like 'AB%C'
```

### Null / not null

```csharp
op => op.Name == null     // Name Is Null
op => op.Name != null     // Name Is Not Null
```

### Referencje

```csharp
t => t.Dostawca == null
t => t.Dostawca != null

// Weryfikacja typu referencji (sprawdzenie tabeli docelowej polimorficznej referencji)
t => t.Jednostka is Jednostka     // Jednostka typeof Jednostki

// Warunek na obiekcie wskazywanym przez referencję - alternatywa do "t.Ref.Pole == ..."
t => LinqConditionMethods.Join(t.Dostawca, k => k.Kod == "ABC")
//  -> join Dostawca where (Kod='ABC')
```

### Operator IN - przynależność do zbioru

`LinqConditionMethods.In` zastępuje rozpisany ciąg `pole == v1 || pole == v2 || ...`. Argumenty można podać jako `params` lub jako tablicę. Działa dla różnych typów - intów, dat, procentów, stringów, referencji:

```csharp
// Stringi (params)
t => LinqConditionMethods.In(t.Kod, new[] { "BIKINI", "XYZ" })

// Daty
d => LinqConditionMethods.In(d.Data, Date.Today.PrevDay, Date.Today, Date.Today.NextDay)

// Inty - params
d => LinqConditionMethods.In(d.ID, 1, 2)

// Inty - tablica
d => LinqConditionMethods.In(d.ID, new[] { 1, 2 })

// Procenty
d => LinqConditionMethods.In(d.Rabat, Percent.Parse("10%"), Percent.Parse("20%"))

// Referencje
t => LinqConditionMethods.In(t.Dostawca, kontra1, kontra2)
t => LinqConditionMethods.In(t.Dostawca, kontraArr)
```

### Operatory logiczne i wyrażenia złożone

```csharp
// AND, OR, NOT
op => op.Locked && op.ID > 234           // Locked and ID>'234'
op => !op.Locked || op.Name == "ABC"     // NOT Locked or Name='ABC'

// Operator warunkowy ?: - rozwijany do dwóch wykluczających się gałęzi
op => op.Locked ? op.Name == "ABC" : op.FullName == "DEF"
//  -> Locked and Name='ABC' or NOT Locked and FullName='DEF'

// Nawiasy - normalne nawiasy C# sterują grupowaniem
t => (t.Typ == TypTowaru.Towar || t.Typ == TypTowaru.Usluga) && t.Dostawca != null
```

### Pola złożone (Quantity, Currency, FromTo)

Można porównywać całe pole złożone albo pojedyncze elementy (`.Value`, `.Symbol`, `.From`, `.To`, ...):

```csharp
// Porównanie całego Quantity
t => t.MasaNetto == new Quantity(123, "kg")     // MasaNetto='123 kg'

// Składowe Quantity
t => t.MasaNetto.Value  == 123                  // [MasaNetto.Value]='123'
t => t.MasaNetto.Symbol == "szt"                // [MasaNetto.Symbol]='szt'

// FromTo - cały zakres
e => e.Okres == new YearMonth(2020, 2).ToFromTo()     // Okres='...'

// FromTo - składowa
e => e.Okres.From == new Date(2020, 2, 2)             // [Okres.From]='...'

// FromTo - przynależność daty do zakresu
e => e.Okres.Contains(new Date(2020, 2, 2))
//  -> [Okres.From]<='...' and [Okres.To]>='...'

// FromTo - przynależność zakresu do zakresu
e => e.Okres.Contains(new YearMonth(2020, 2).ToFromTo())

// FromTo - przecięcie zakresów (część wspólna niepusta)
e => e.Okres.IsIntersected(new YearMonth(2020, 2).ToFromTo())
```

### Kolekcje powiązane (podlisty)

Dla pól reprezentujących powiązaną podlistę:

```csharp
// Podlista pusta / niepusta
t => t.AdresyWWW.IsEmpty                                // NOT exists AdresWWW.Zapis
t => t.AdresyWWW.Any                                    // exists AdresWWW.Zapis
t => t.AdresyWWW.Any()                                  // exists AdresWWW.Zapis

// Egzystencjalny - istnieje przynajmniej jeden element spełniający warunek
t => t.AdresyWWW.Any(adr => adr.Domyslny)
//  -> exists AdresWWW.Zapis where (Domyslny)

// Egzystencjalny przez referencję
t => t.Dostawca.AdresyWWW.Any(adr => adr.Domyslny)
//  -> exists AdresWWW.Zapis=Dostawca where (Domyslny)

// Uniwersalny - wszystkie elementy spełniają warunek (równoważne: NOT exists element spełniający NEG)
t => t.AdresyWWW.All(adr => adr.Domyslny)
//  -> NOT exists AdresWWW.Zapis where (NOT Domyslny)

t => t.Dostawca.AdresyWWW.All(adr => adr.Domyslny)
//  -> NOT exists AdresWWW.Zapis=Dostawca where (NOT Domyslny)
```

## Ograniczenia - co się nie skompiluje do SQL

* **Pola niebazodanowe** - próba użycia właściwości obliczanej w C# (np. `op.NewPassword`) rzuca `LinqConditionException`.
* **Referencje przez pole nie-bazodanowe** - np. `t.Dostawca.Adres.Faks` gdy `Adres` nie jest fizyczną referencją w bazie - również `LinqConditionException`.
* W praktyce: jeśli pole jest deklarowane jako property w `business.xml`, jest bazodanowe; jeśli jest dopisywane jako property w klasie partial - nie jest.

## Kiedy używać czego

| Cel | API |
|---|---|
| Odczyt odfiltrowanego zbioru danych w logice biznesowej | `SubTable[t => ...]` |
| Filtrowanie listy w UI | `View.AddExpression(...)` |
| Przekazywanie warunku jako wartości / kompozycja warunków | `RowCondition.FromExpression(...)` |
| Niskopoziomowe zapytanie (Query) | `Query.Table.AddExpression(...)` |

**Kod biznesowy nie powinien używać `View`** (to obiekt UI). Kod biznesowy filtruj przez `SubTable[expression]` lub 
`RowCondition.FromExpression`.
