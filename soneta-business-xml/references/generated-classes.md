# Klasy biznesowe obok business.xml

Plik `business.xml` to **połowa** definicji obiektu biznesowego. Druga połowa to **klasy C#**,
które programista tworzy równolegle. Generator (uruchamiany przy kompilacji) zamienia każdą
`<table>` na **techniczne abstrakcyjne klasy bazowe**, a programista dopisuje **klasy konkretne**
dziedziczące po nich służące do implementacji logiki biznesowej.

> **Zasada:** dla każdej `<table>` powstają dwie klasy pisane ręcznie — klasa **obiektu
> biznesowego** (pojedynczy wiersz) i klasa **tabeli** (cała kolekcja). Tworzymy je od razu
> przy definiowaniu tabeli w business.xml.

## Co generuje narzędzie, co pisze programista

Dla definicji:

```xml
<table name="Zgloszenie" tablename="Zgloszenia" guided="Root"
       caption="Zgłoszenie" tablecaption="Zgłoszenia"
       description="Zgłoszenie serwisowe od klienta. Rejestruje reklamacje, naprawy
                    i przeglądy wraz z opisem i datą przyjęcia.">
  <key name="WgNumeru" keyunique="true" keyprimary="true">
    <keycol name="Numer"/>
  </key>
  <col name="Numer" type="string" length="30" required="true" important="true"/>
  <col name="Opis" type="string" length="200"/>
</table>
```

| Warstwa | Klasa | Kto tworzy |
|---------|-------|------------|
| Techniczna klasa danych (rekord) | `<Moduł>Module.ZgloszenieRecord` | narzędzie (infrastruktura ORM, nie dotykamy) |
| Bazowa klasa wiersza | `<Moduł>Module.ZgloszenieRow` | narzędzie (z `name="Zgloszenie"`) |
| Bazowa klasa tabeli | `<Moduł>Module.ZgloszenieTable` | narzędzie |
| **Konkretna klasa obiektu biznesowego** | `class Zgloszenie : <Moduł>Module.ZgloszenieRow` | **programista** |
| **Konkretna klasa tabeli** | `class Zgloszenia : <Moduł>Module.ZgloszenieTable` | **programista** |

- Nazwa klasy obiektu biznesowego = `name` tabeli (l. poj.), nazwa klasy tabeli = `tablename` (l. mn.).
- `<Moduł>` to klasa modułu wynikająca z `name` modułu w nagłówku `business.xml`.
- Klasa `ZgloszenieRecord` to techniczna struktura danych ORM — programista **jej nie pisze**
  ani nie używa wprost.

```csharp
namespace Soneta.Serwis {

    public class Zgloszenie : SerwisModule.ZgloszenieRow {
        // logika biznesowa pojedynczego zgłoszenia
    }

    public class Zgloszenia : SerwisModule.ZgloszenieTable {
        // metody wyszukiwania, stałe, Params
    }
}
```

> Klasa tabeli **nie musi** być `partial` ani `sealed` — to zwykła klasa dziedzicząca po
> bazie. `partial` stosuje się tylko wtedy, gdy faktycznie dzielisz ją na kilka plików.

## Atrybut `description` tabeli — opis zastosowania

Tabela powinna mieć atrybut **`description`** — krótki, **dwu- trzyzdaniowy** opis tego,
po co tabela powstała. Służy do szybkiego zorientowania się w strukturze programu (również
przez modele językowe analizujące schemat).

```xml
<table name="Zgloszenie" tablename="Zgloszenia" ...
       description="Zgłoszenie serwisowe od klienta. Rejestruje reklamacje, naprawy
                    i przeglądy wraz z opisem i datą przyjęcia.">
```

- pisz o **przeznaczeniu** tabeli, nie o pojedynczych polach;
- dla tabel szczegółów warto zaznaczyć powiązanie z tabelą nadrzędną;
- `description` ≠ `caption`/`tablecaption` (te są etykietami UI, nie opisem zastosowania).

## Konstruktory klasy obiektu biznesowego

To, jakie konstruktory musi mieć klasa wiersza, **zależy od pól `readonly`** w `business.xml`.

### Tabela bez pól `readonly` → konstruktor domyślny

Gdy żadna kolumna nie jest `readonly`, wystarczy domyślny bezparametrowy konstruktor
(można pominąć — kompilator wygeneruje go automatycznie).

```csharp
public class Zgloszenie : SerwisModule.ZgloszenieRow {
    // brak pól readonly — konstruktor domyślny wystarcza
}
```

### Tabela z polami `readonly` → konstruktor inicjujący + konstruktor ORM

Pole `readonly` nie ma settera — jego wartość trzeba ustawić **w konstruktorze**.
Bazowa klasa wiersza udostępnia wtedy konstruktor przyjmujący wartości takich pól. Klasa
konkretna musi dostarczyć **dwa** konstruktory:

1. konstruktor **inicjujący pola readonly** (wywołuje konstruktor bazy),
2. konstruktor `(RowCreator creator)` — **wymagany przez ORM** do materializacji obiektu
   odczytywanego z bazy (konstruktor infrastrukturalny).

```xml
<col name="DataUtworzenia" type="datetime" readonly="true" required="true"/>
```

```csharp
public class Zgloszenie : SerwisModule.ZgloszenieRow {

    // 1) inicjuje pole readonly przy tworzeniu nowego obiektu
    public Zgloszenie(DateTime dataUtworzenia) : base(dataUtworzenia) { }

    // 2) wymagany przez ORM — odczyt obiektu z bazy
    public Zgloszenie(RowCreator creator) : base(creator) { }
}
```

> **Reguła kciuka:** są pola `readonly` (w szczególności **selector**, patrz niżej) →
> musisz napisać konstruktor `(RowCreator creator)`. Brak pól `readonly` → nie musisz.

> Pełną dokumentację klas Row/Table i wzorca selektora (`[BusinessRow]`, `[NewRow]`,
> konstruktor `RowCreator`, pola readonly) zawiera skill `/soneta-programming` (row-types.md).

## Tabela z selector'em — wiele typów obiektów w jednej tabeli

**Selector** (`selector="true"`) to kolumna decydująca, **który typ obiektu** reprezentuje
wiersz. Jedna tabela przechowuje wtedy różne warianty obiektu, a ORM dla każdego wiersza
buduje właściwą klasę. Selector jest polem typu **`int`** — najczęściej zadeklarowanym jako
**enum** (bardziej opisowy, zalecany), choć dopuszczalny jest też zwykły `int`. Kolumnę
oznacza się `selector="true"`, zawsze `readonly` i `required`.

### Krok 1 — kolumna-selector i enum w business.xml

```xml
<enum name="TypZgloszenia"/>

<table name="Zgloszenie" tablename="Zgloszenia" guided="Root"
       caption="Zgłoszenie" tablecaption="Zgłoszenia"
       description="Zgłoszenie serwisowe. Jedna tabela przechowuje reklamacje, naprawy
                    i przeglądy, rozróżniane polem Typ.">
  <key name="WgNumeru" keyunique="true" keyprimary="true">
    <keycol name="Numer"/>
  </key>

  <col name="Typ" type="TypZgloszenia" selector="true"
       readonly="true" required="true"
       caption="Typ" description="Rodzaj zgłoszenia."/>

  <col name="Numer" type="string" length="30" required="true" important="true"/>
  <col name="Opis" type="string" length="200"/>
</table>
```

### Krok 2 — enum z jawnymi wartościami liczbowymi

Enum użyty w kolumnie ma w C# **explicite przypisane numery**. Wartość selector'a trafia do
bazy i do rejestracji typów (krok 3), więc musi być stabilna. Nowe pozycje **dopisuje się
na końcu** — nigdy nie zmienia ani nie przenumerowuje istniejących.

```csharp
public enum TypZgloszenia {
    Reklamacja = 1,
    Naprawa    = 2,
    Przeglad   = 3,
}
```

### Krok 3 — abstrakcyjna klasa bazowa, podtypy i rejestracja

- klasa obiektu biznesowego jest **`abstract`**, a jej konstruktor przyjmuje wartość selector'a;
- każdy wariant to **podtyp** z konstruktorem oznaczonym `[DefaultConstructor]` (ustawia swoją
  wartość) oraz konstruktorem `(RowCreator)` dla ORM;
- każdy podtyp rejestrujemy atrybutem **assembly-level** `[BusinessRow]`, wiążącym klasę
  z wartością selector'a.

```csharp
[assembly: BusinessRow(typeof(Zgloszenie.ReklamacjaType), TypZgloszenia.Reklamacja)]
[assembly: BusinessRow(typeof(Zgloszenie.NaprawaType),    TypZgloszenia.Naprawa)]
[assembly: BusinessRow(typeof(Zgloszenie.PrzegladType),   TypZgloszenia.Przeglad)]

namespace Soneta.Serwis {

    public abstract class Zgloszenie : SerwisModule.ZgloszenieRow {

        protected Zgloszenie(TypZgloszenia typ) : base(typ) { }
        protected Zgloszenie(RowCreator creator) : base(creator) { }

        public class ReklamacjaType : Zgloszenie {
            [DefaultConstructor] public ReklamacjaType() : base(TypZgloszenia.Reklamacja) { }
            public ReklamacjaType(RowCreator creator) : base(creator) { }
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
}
```

Przy odczycie wiersza ORM patrzy na wartość kolumny-selector'a i buduje instancję klasy
zarejestrowanej dla tej wartości. Każdy podtyp może mieć własną logikę (metody, walidacje).

> **`[DefaultConstructor]`** wskazuje, **który konstruktor ma wykorzystać interfejs
> użytkownika** przy tworzeniu obiektu, gdy klasa ma więcej niż jeden konstruktor. Konstruktory
> infrastrukturalne `(RowCreator)` nie są w tej analizie uwzględniane — atrybut stawiamy więc
> na konstruktorze „biznesowym" (bezparametrowym / ustawiającym wartość selector'a).

> **Rozszerzalność:** dodatek może dorejestrować **własny** podtyp kolejnym
> `[assembly: BusinessRow(typeof(MojNowyTyp), TypZgloszenia.Cos)]` — bez zmian w module bazowym.

## Pozycje menu „Nowy..." — `[NewRow]`

Atrybut **assembly-level** `[NewRow]` decyduje, które typy operator może **dodać z interfejsu**
(pozycje rozwijanego przycisku „Nowy" nad listą). Dla tabeli z selector'em deklarujemy
**po jednym `[NewRow]` na każdy podtyp** dostępny w menu.

```csharp
[assembly: NewRow("Reklamacja", typeof(Zgloszenie.ReklamacjaType))]
[assembly: NewRow("Naprawa",    typeof(Zgloszenie.NaprawaType))]
[assembly: NewRow("Przegląd",   typeof(Zgloszenie.PrzegladType))]
```

- pierwszy argument to etykieta pozycji w menu (gdy pominięty — bierze `[Caption]` klasy lub jej nazwę);
- `Default = true` — typ tworzony automatycznie po klawiszu `INSERT` (bez rozwijania menu);
- `Shortcut = 'R'` — klawisz skrótu tworzący dany typ;
- klasa wskazana w `[NewRow]` musi mieć jeden konstruktor (nie `RowCreator`) lub wiele, gdzie jeden jest oznaczony `[DefaultConstructor]`.

> **Brak `[NewRow]` = brak możliwości dodania z UI.** Taki obiekt powstaje **wyłącznie
> programowo z kodu**. To naturalny podział: dane konfiguracyjne (operator dodaje ręcznie)
> mają `[NewRow]`; dane operacyjne tworzone przez logikę programu — celowo go nie mają.

## Podsumowanie — co utworzyć przy każdej tabeli

1. `description` tabeli — 2–3 zdania o jej przeznaczeniu.
2. Klasa **obiektu biznesowego** (`X : <Moduł>Module.XRow`) i klasa **tabeli**
   (`Xs : <Moduł>Module.XTable`).
3. Jeśli są pola `readonly` → konstruktor inicjujący + `(RowCreator creator)`.
4. Jeśli jest **selector**: enum (lub `int`) z jawnymi wartościami, `abstract` baza, podtypy
   z `[DefaultConstructor]`, rejestracja `[assembly: BusinessRow]`.
5. `[assembly: NewRow(...)]` dla typów dostępnych w menu „Nowy" (pomiń, by zablokować dodawanie z UI).

> Perspektywę kodu/runtime tego samego mechanizmu (jak ORM materializuje obiekty, wzorce
> użycia w logice biznesowej) opisuje skill `/soneta-programming` (artykuł row-types.md).
