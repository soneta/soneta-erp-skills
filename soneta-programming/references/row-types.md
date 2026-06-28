# Typy wierszy: klasy Row/Table, konstruktory, selector

Ten dokument opisuje, jak programista **implementuje klasy** dla tabeli zdefiniowanej w
`business.xml` — od strony kodu i działania ORM. Definiowanie samego pliku XML opisuje skill
**soneta-business-xml** (tam też pełny wzorzec „XML ↔ klasy").

## Klasy obiektu biznesowego i tabeli (poziom 3)

Dla każdej tabeli generator tworzy techniczne klasy bazowe, a programista pisze dwie klasy
konkretne (poziom 3 — patrz „3 poziomy logiki" w SKILL.md):

| Rola | Klasa | Dziedziczy po |
|------|-------|---------------|
| Obiekt biznesowy (wiersz) | `Zgloszenie` | `SerwisModule.ZgloszenieRow` |
| Tabela (kolekcja) | `Zgloszenia` | `SerwisModule.ZgloszenieTable` |

```csharp
public class Zgloszenie : SerwisModule.ZgloszenieRow {
    // logika pojedynczego obiektu (właściwości kalkulowane, walidacje, metody)
}

public class Zgloszenia : SerwisModule.ZgloszenieTable {
    // metody wyszukiwania, stałe Guid, klasa Params
}
```

- Nazwa klasy obiektu biznesowego = `name` tabeli (l. poj.), klasy tabeli = `tablename` (l. mn.).
- Klasa tabeli **nie musi** być `partial` ani `sealed` — `partial` stosuje się tylko, gdy
  faktycznie dzielisz ją na kilka plików.
- ORM generuje też techniczną klasę danych `<Moduł>Module.ZgloszenieRecord` (rekord surowych
  danych) — programista **jej nie pisze** ani nie używa wprost. Dlatego klasę pisaną ręcznie
  nazywamy **obiektem biznesowym** / **wierszem**, a nie „rekordem".

## Konstruktory klasy obiektu biznesowego

ORM tworzy obiekty na dwa sposoby: **materializując** je z bazy oraz przy **tworzeniu nowego**
wiersza w kodzie. To, jakich konstruktorów wymaga klasa, zależy od pól `readonly`.

### Brak pól `readonly` → konstruktor domyślny

```csharp
public class Zgloszenie : SerwisModule.ZgloszenieRow {
    // wystarczy konstruktor domyślny (niejawny)
}
```

Obiekt tworzymy zwykłym `new` i dodajemy do tabeli:

```csharp
using (var tr = session.Logout(editMode: true)) {
    var z = new Zgloszenie();
    session.GetSerwis().Zgloszenia.AddRow(z);
    z.Numer = "Z/2026/001";
    tr.Commit();
}
```

### Pola `readonly` → konstruktor inicjujący + `(RowCreator)`

Pole `readonly` nie ma settera — wartość ustawia się **w konstruktorze**. Bazowa klasa wiersza
udostępnia konstruktor przyjmujący te wartości; klasa konkretna potrzebuje **dwóch**
konstruktorów:

```csharp
public class Zgloszenie : SerwisModule.ZgloszenieRow {

    // tworzenie nowego obiektu — inicjuje pole readonly
    public Zgloszenie(DateTime dataUtworzenia) : base(dataUtworzenia) { }

    // materializacja obiektu z bazy — wymagany przez ORM (konstruktor infrastrukturalny)
    public Zgloszenie(RowCreator creator) : base(creator) { }
}
```

Konstruktor `(RowCreator creator)` jest wywoływany przez ORM przy odczycie wiersza — **nie
wywołujemy go z własnego kodu**. Brak tego konstruktora przy polach readonly = błąd
materializacji.

## Selector — wiele typów obiektów w jednej tabeli

Gdy kolumna ma `selector="true"` w `business.xml`, jedna tabela przechowuje **różne typy
obiektów**. ORM przy odczycie patrzy na wartość selector'a i buduje instancję właściwej klasy.
Selector jest polem typu `int` — najczęściej zadeklarowanym jako **enum** (bardziej opisowy,
zalecany), choć dopuszczalny jest też zwykły `int`.

Wzorzec po stronie kodu:

```csharp
// enum z jawnymi wartościami — wartość trafia do bazy i do rejestracji
public enum TypZgloszenia {
    Reklamacja = 1,
    Naprawa    = 2,
    Przeglad   = 3,
}

// rejestracja podtypów (assembly-level): klasa <-> wartość selector'a
[assembly: BusinessRow(typeof(Zgloszenie.ReklamacjaType), TypZgloszenia.Reklamacja)]
[assembly: BusinessRow(typeof(Zgloszenie.NaprawaType),    TypZgloszenia.Naprawa)]
[assembly: BusinessRow(typeof(Zgloszenie.PrzegladType),   TypZgloszenia.Przeglad)]

public abstract class Zgloszenie : SerwisModule.ZgloszenieRow {

    protected Zgloszenie(TypZgloszenia typ) : base(typ) { }
    protected Zgloszenie(RowCreator creator) : base(creator) { }

    public class ReklamacjaType : Zgloszenie {
        [DefaultConstructor] public ReklamacjaType() : base(TypZgloszenia.Reklamacja) { }
        public ReklamacjaType(RowCreator creator) : base(creator) { }
        // logika specyficzna dla reklamacji
    }

    public class NaprawaType : Zgloszenie {
        [DefaultConstructor] public NaprawaType() : base(TypZgloszenia.Naprawa) { }
        public NaprawaType(RowCreator creator) : base(creator) { }
    }

    public class PrzegladType : Zgloszenie {
        [DefaultConstructor] public PrzegladType() : base(TypZgloszenia.Przeglad) { }
        public PrzegladType(RowCreator creator) : base(creator) { }
    }
}
```

Reguły:
- klasa bazowa obiektu jest **`abstract`** (selector jest `readonly` → konstruktor `(RowCreator)`);
- każdy podtyp ma konstruktor `[DefaultConstructor]` ustawiający swoją wartość selector'a oraz
  konstruktor `(RowCreator)`;
- `[assembly: BusinessRow(typeof(Podtyp), wartość)]` wiąże klasę z wartością — to **discovery**:
  ORM zbiera rejestracje ze wszystkich assembly, więc **dodatek może dorejestrować własny
  podtyp** bez modyfikacji modułu bazowego.

### Atrybut `[DefaultConstructor]`

Wskazuje, **który konstruktor ma wykorzystać interfejs użytkownika** przy tworzeniu obiektu.
Klasa może mieć wiele konstruktorów — bez tego atrybutu UI nie wiedziałby, którego użyć.
W tej analizie **nie są uwzględniane konstruktory infrastrukturalne** `(RowCreator)`, więc
atrybut stawiamy na konstruktorze „biznesowym" (bezparametrowym lub ustawiającym wartość
selector'a).

### Rozróżnianie podtypów przy odczycie

```csharp
foreach (Zgloszenie z in session.GetSerwis().Zgloszenia.WgNumeru) {
    if (z is Zgloszenie.ReklamacjaType reklamacja) { /* ... */ }
}
```

### Jawne wartości liczbowe enum'ów

Każdy enum zapisywany w kolumnie (w szczególności selector) musi mieć **explicite przypisane
numery**. Wartość trafia do bazy i do `[BusinessRow]`, więc nowe pozycje **dopisuje się na
końcu** — bez zmiany ani przenumerowania istniejących, by nie rozjechać zapisanych danych.

## `[NewRow]` — pozycje menu „Nowy..."

Atrybut **assembly-level** `[NewRow]` określa, które typy operator może **dodać z interfejsu**
(pozycje rozwijanego przycisku „Nowy" nad listą). Dla tabeli z selector'em — po jednym na
podtyp dostępny w menu:

```csharp
[assembly: NewRow("Reklamacja", typeof(Zgloszenie.ReklamacjaType))]
[assembly: NewRow("Naprawa",    typeof(Zgloszenie.NaprawaType), Default = true)]
[assembly: NewRow("Przegląd",   typeof(Zgloszenie.PrzegladType), Shortcut = 'P')]
```

- klasa wskazana w `[NewRow]` **musi mieć bezparametrowy konstruktor** (oznaczony `[DefaultConstructor]`);
- `Default = true` — typ tworzony automatycznie po `INSERT`; `Shortcut` — klawisz skrótu;
- etykieta (pierwszy argument) domyślnie z `[Caption]` klasy lub jej nazwy.

> **Brak `[NewRow]` = brak możliwości dodania z UI.** Taki obiekt powstaje **wyłącznie
> programowo z kodu**. To naturalny podział: dane **konfiguracyjne** (operator dodaje ręcznie)
> mają `[NewRow]`; dane **operacyjne** tworzone przez logikę/workery programu — celowo go nie
> mają, co zabezpiecza je przed ręcznym dodawaniem.

## Odczyt atrybutów assembly-level

Rejestracje `[BusinessRow]` / `[NewRow]` (i inne atrybuty assembly-level) odczytuje się gotową,
cache'owaną metodą `AssemblyAttributes.GetCustom<T>()` — np. zbudowanie mapy `selector → typ`.
Pełny opis (cache, sortowanie wg `Priority`, `Find`, iteracja po assembly, analiza DLL):
[assembly-attributes.md](assembly-attributes.md).

## Skrót — co zaimplementować przy tabeli

1. Klasa obiektu biznesowego (`X : <Moduł>Module.XRow`) i klasa tabeli (`Xs : <Moduł>Module.XTable`).
2. Pola `readonly` → konstruktor inicjujący + `(RowCreator creator)`.
3. Selector → enum (lub `int`) z jawnymi numerami, `abstract` baza, podtypy z `[DefaultConstructor]`,
   rejestracja `[assembly: BusinessRow]`.
4. `[assembly: NewRow]` dla typów dostępnych w menu „Nowy" (pomiń, aby blokować dodawanie z UI).
