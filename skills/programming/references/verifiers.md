# Weryfikatory - walidacja spójności danych biznesowych

Weryfikator to reguła sprawdzająca poprawność (spójność) danych obiektu biznesowego za pomocą
algorytmu. Sprawdza wartości zapisane w wierszu (Row) i — jeśli dane naruszają regułę — zgłasza
komunikat o określonym **poziomie ważności** (błąd / ostrzeżenie / informacja).

## Najważniejsze zasady (must-know)

1. **Poziom `Error` blokuje zapis.** Przed zapisem sesji do bazy (`session.Save()`) wszystkie
   weryfikatory typu `Error` muszą przejść poprawnie. Sesji z niespełnionym weryfikatorem-błędem
   **nie da się zapisać** — walidacja rzuca wyjątek i przerywa `Save()`.
2. **Poziom `Warning` nie blokuje zapisu.** Ostrzeżenia (najczęściej żółte komunikaty) użytkownik
   może zignorować. **W sesji bez interfejsu użytkownika ostrzeżenia nie są w ogóle sprawdzane** —
   sprawdzane są wtedy wyłącznie błędy.
3. **Poziom `Information` niczego nie wymusza** — to czysty komunikat.
4. **Weryfikator uzbraja się na zmianę pól-źródeł.** Źródła to pola/obiekty, których zmiana ma
   uruchomić sprawdzenie. Po każdej zmianie źródła dodajesz weryfikator do sesji
   (`Session.Verifiers.Add(...)`). Gdy warunek jest spełniony, weryfikator sam znika z kolekcji;
   następna zmiana źródła uzbraja go ponownie.
5. **Dodanie jest idempotentne.** Powtórne `Add(...)` tego samego weryfikatora nie tworzy duplikatu
   (decyduje `Equals`/`GetHashCode`) — w kolekcji zostaje jedna instancja.
6. **Cykl życia:** `Add` (uzbrojenie) → `Validate()` przy zapisie/akcji UI → gdy `IsValid()` zwróci
   `true`, weryfikator jest usuwany z kolekcji; gdy `false`, staje się aktywnym błędem/ostrzeżeniem.
   Weryfikator wiersza usuwanego (nie „live") kasuje się sam.
7. **Rejestracja jest transakcyjna.** Uzbrojenie i wyrejestrowanie weryfikatora uczestniczy w
   bieżącej transakcji biznesowej (`Session.Logout` / `ITransaction`): rollback cofa również zmiany
   w kolekcji weryfikatorów, a `Commit` je utrwala. Zob. [Transakcyjność rejestracji](#transakcyjność-rejestracji).

## Minimalny weryfikator + rejestracja (skrót)

```csharp
// 1. Klasa weryfikatora — pole 'Kod' nie może być puste (błąd blokujący zapis)
//    primary constructor przekazuje wiersz i nazwę pola-źródła do klasy bazowej
internal sealed class KodRequiredVerifier(Towar row) : ColVerifier<Towar>(row, nameof(Towar.Kod)) {
    protected override bool IsValid() => !Row.Kod.IsNullOrEmpty();   // warunek poprawności
    public override string Description => "Wartość pola 'Kod' jest wymagana".Translate();
    public override VerifierType Type => VerifierType.Error;         // blokuje Save()
}

// 2. Uzbrojenie na zmianę pola-źródła (w setterze property lub w workerze)
public string Kod {
    get => record.Kod;
    set {
        record.Kod = value;
        if (State != RowState.Detached)
            Session.Verifiers.Add(new KodRequiredVerifier(this));   // dedup: druga instancja nie wejdzie
    }
}
```

Rejestrację deklaratywną (`<verifier>` w business.xml) opisuje sekcja
[Uzbrajanie weryfikatorów](#uzbrajanie-weryfikatorów-arming).

## Poziomy ważności (VerifierType)

| Typ | Blokuje `Save()`? | Sprawdzany bez UI? | Prezentacja | Zastosowanie |
|-----|-------------------|--------------------|-------------|--------------|
| `Error` | **tak** — wyjątek przerywa zapis | **tak** | komunikat blokujący | naruszenie spójności, którego nie wolno utrwalić |
| `Warning` | nie — można zignorować | **nie** | zwykle żółty komunikat | dane podejrzane, ale dopuszczalne |
| `Information` | nie | nie | komunikat informacyjny | wskazówka dla operatora |

Typ zwraca property `Type` (domyślnie `Error`). Błędy są „najpilniej strzeżone": walidowane są
także przy zmianie **innego** pola i zawsze przy zatwierdzeniu wiersza — patrz `VerifierAction`.
Poziom może też być **konfigurowalny per wdrożenie** — wtedy `Type` zwraca wartość zależną od
ustawień (operator ustawia regułę jako błąd, ostrzeżenie lub ją wyłącza), a nie stałą.

> **Wiersz Detached (jeszcze nie dodany do tabeli):** jego weryfikatory-błędy traktowane są jak
> ostrzeżenia — nie blokują, bo obiekt nie trafia jeszcze do bazy.

## Kiedy weryfikator jest uruchamiany (VerifierAction)

`VerifierAction` opisuje zdarzenie, w reakcji na które rozważane jest sprawdzenie. Metoda
`IsAccept(data, property, action)` rozstrzyga, czy dany weryfikator jest istotny dla zdarzenia UI.
Domyślna logika bazowa (dla weryfikatorów, które nie zawężają pól):

| `VerifierAction` | Kiedy | Domyślnie istotny? |
|------------------|-------|--------------------|
| `ChangeField` | zmieniono pole powiązane z weryfikatorem | zawsze |
| `ChangeOtherField` | zmieniono **inne** pole | tylko gdy `Type == Error` |
| `AcceptRow` | zatwierdzenie/akceptacja całego wiersza (walidacja przed zapisem) | tylko gdy `Type != Information` |

Dlatego błędy są sprawdzane najszczelniej: reagują nawet na zmianę niepowiązanego pola i zawsze
przy zatwierdzeniu wiersza. Klasy `ColVerifier`/`MultiColVerifier` zawężają `IsAccept` do
konkretnych pól — patrz niżej.

## Hierarchia klas bazowych

```
Verifier (abstrakcyjna; dziedziczy po BusException, implementuje IVerifier)
 └── RowVerifier            – weryfikator wiersza (IRow); implementuje IExDescription
      └── RowVerifier<T>    – typowany po wierszu; Row zwraca T
      └── ColVerifier       – weryfikator jednego pola (IsAccept ograniczone do pola)
           └── ColVerifier<T>            – typowany po wierszu
                └── ImportantColumnVerifier<T> – gotowiec: ostrzega o zmianie „ważnego" pola
           └── RequiredVerifier          – gotowiec: pole wymagane (walidacja dopiero po AcceptRow)
      └── MultiColVerifier<T> – weryfikator zależny od kilku pól równocześnie
```

| Klasa | Co dostarcza | Co nadpisujesz | Typowy `Type` |
|-------|--------------|----------------|---------------|
| `Verifier` | baza; `Equals`/`GetHashCode` po typie | `IsValid`, `Description`, `Source`, `Type` | Error |
| `RowVerifier` / `RowVerifier<T>` | wiązanie z `Row`, `Source=Row`, `ExDescription` | `IsValid`, `Description`, `Type`, zwykle `IsAccept` | dowolny |
| `ColVerifier<T>` | `IsAccept` = „zmiana tego pola"; equals po polu | `IsValid`, `Description`, `Type` | dowolny |
| `MultiColVerifier<T>` | `IsAccept` = zmiana któregoś z listy pól | `IsValid`, `Description`, `Type` | dowolny |
| `ImportantColumnVerifier<T>` | pełny gotowiec — wykrywa zmianę wartości pola | (używasz wprost) | Warning |
| `RequiredVerifier` | „pole wymagane"; aktywuje się po `AcceptRow` | `IsValid` (i ew. `Type`) | Error |

**Zasada wyboru klasy bazowej:**
- jedno pole-źródło → `ColVerifier<T>` (nie musisz pisać `IsAccept`, podajesz nazwę pola w konstruktorze),
- kilka pól-źródeł → `MultiColVerifier<T>`,
- reguła obejmująca cały wiersz lub źródła spoza wiersza (np. pole pod-obiektu) → `RowVerifier<T>` z własnym `IsAccept`,
- „pole nie może być puste" → `RequiredVerifier`.

## Co nadpisujesz w nowej klasie weryfikatora

| Składowa | Rola | Uwagi                                                   |
|----------|------|---------------------------------------------------------|
| `bool IsValid()` | **właściwa reguła** — `true` = dane poprawne | wyjątek w środku zostaje opakowany w `RowException`     |
| `string Description` | komunikat dla operatora | zawsze przez `.Translate()` lub `.TranslateFormat()`    |
| `VerifierType Type` | poziom ważności | domyślnie `Error`; dla warningu nadpisz                 |
| `bool IsAccept(object data, string property, VerifierAction action)` | które zdarzenia dotyczą tego weryfikatora | w `ColVerifier`/`MultiColVerifier` już zaimplementowane |
| `object Source` | obiekt źródłowy | `RowVerifier` zwraca `Row` automatycznie                |

## Uzbrajanie weryfikatorów (arming)

Weryfikator zaczyna być pilnowany dopiero po dodaniu do `Session.Verifiers`. Są dwie drogi:

### 1. Deklaratywnie w business.xml (najczęstsza)

Weryfikator deklaruje się przy kolumnie-źródle. Framework generuje kod, który po zmianie tej
kolumny dodaje weryfikator do sesji:

```xml
<col name="PESEL" type="string" length="11">
  <verifier name="Pracownik.PeselVerifier"/>
  <verifier name="Pracownik.PeselUniqueVerifier" onadded="true"/>  <!-- tylko przy dodawaniu wiersza -->
</col>
```

`business.xml` podaje **tylko nazwę** klasy — implementację piszesz po stronie kodu (ten skill).
Składnia elementu `<verifier>` i atrybut `onadded`: patrz skill **[business-xml](../../business-xml/SKILL.md)**
(`references/table-reference.md` → „Element verifier"). Ten sam weryfikator można podpiąć pod
kilka kolumn — każda z nich staje się źródłem uzbrajającym.

### 2. Ręcznie w kodzie

Gdy źródłem jest coś innego niż pojedyncza kolumna (np. pole pod-obiektu, wynik operacji workera),
uzbrajasz weryfikator jawnie — w setterze property, w evencie zmiany lub w kodzie akcji:

```csharp
row.Session.Verifiers.Add(new OkresZatrudnieniaVerifier(row));
```

**Uzbrajaj po każdej zmianie źródła.** Powtórne `Add` nie duplikuje (dedup po `Equals`/`GetHashCode`),
a jeśli weryfikator był już zwalidowany, `Add` ustawia go ponownie jako „do sprawdzenia".

### Transakcyjność rejestracji

Rejestracja weryfikatorów jest **w pełni transakcyjna** — uczestniczy w tej samej transakcji
biznesowej (`ITransaction`), co zmiany danych, otwieranej przez `Session.Logout(editMode: true)`.
Transakcyjne są wszystkie operacje na kolekcji: `Add` (uzbrojenie), `Remove` (wyrejestrowanie) oraz
przejście weryfikatora między stanem „do sprawdzenia" a „zwalidowany".

Konsekwencje:

- **Rollback** (Dispose transakcji bez `Commit`/`CommitUI`) cofa nie tylko zmiany pól, ale też
  rejestrację weryfikatorów — kolekcja `Session.Verifiers` wraca do stanu sprzed transakcji. Jeśli
  więc setter uzbroił weryfikator, a edycja została wycofana, weryfikator **nie zostaje** w sesji.
- **Commit/CommitUI** utrwala rejestrację; przy transakcjach zagnieżdżonych zmiana rejestracji jest
  przenoszona do transakcji nadrzędnej (i finalnie zatwierdzana wraz z całą edycją).
- Dzięki temu uzbrajanie weryfikatora w setterze property jest spójne z transakcyjnym modelem edycji
  — stan kolekcji weryfikatorów zawsze odpowiada faktycznie zatwierdzonym danym.

```csharp
using (var tr = row.Session.Logout(editMode: true)) {
    row.Pole = nowaWartość;   // setter zmienia pole i uzbraja weryfikator (Session.Verifiers.Add)
    // brak Commit → Dispose wycofa i zmianę pola, i uzbrojenie weryfikatora
}
```

> **Uwaga:** transakcyjność dotyczy rejestracji wykonywanej **wewnątrz** transakcji edycyjnej — co
> jest normą, bo każda modyfikacja obiektu i tak wymaga `Session.Logout(editMode: true)`. Szczegóły
> transakcji, `Commit`/`CommitUI` i rollbacku: [session-login.md](session-login.md).

## Odczyt weryfikatorów przez UI (`IVerifiable.GetVerifiers()`)

To **nie jest** sposób rejestracji — uzbrajanie zawsze robi `Session.Verifiers.Add(...)`.
`IVerifiable.GetVerifiers()` mówi interfejsowi użytkownika, **jak dla danego obiektu odczytać jego
listę weryfikatorów**. Weryfikatory żyją w kolekcji sesji; formularz musi się do niej dostać, by
pokazać operatorowi błędy i ostrzeżenia dotyczące edytowanego obiektu.

Zwykły wiersz (Row) implementuje ten kontrakt „z pudełka" — deleguje odczyt do sesji, więc
formularz działa bez dodatkowego kodu. Problem dotyczy obiektów **nie-sesyjnych** edytowanych na
formularzu (np. obiekt parametrów/kontekstu workera dziedziczący po `ContextBase`): nie są wierszem
tabeli i formularzowi trudno sięgnąć z nich do kolekcji weryfikatorów w sesji. **Dlatego taki obiekt
powinien sam zaimplementować `GetVerifiers()`** — najczęściej delegując do sesji — aby dopięty do
niego formularz mógł zaprezentować zarejestrowane weryfikatory:

```csharp
IEnumerable IVerifiable.GetVerifiers() {
    if (Session is null) return Verifier.EmptyVerifiers;
    Accept();                                             // domknij bieżącą edycję pól
    Session.Verifiers.Add(new GrupaRequiredVerifier(this)); // upewnij się, że reguła jest uzbrojona
    return ((IVerifiable)Session).GetVerifiers();         // ← UI dostaje weryfikatory z sesji
}
```

Wywołanie `Add(...)` w tej metodzie to tylko dogodne domknięcie: gwarantuje, że przed odczytem
weryfikator jest w kolekcji. Sednem metody jest **zwrócenie** weryfikatorów sesji, żeby UI mogło je
pokazać. Weryfikator obiektu nie-wierszowego dziedziczy wprost po `Verifier` (nie po `RowVerifier`),
więc sam implementuje `Source`, `Equals`, `GetHashCode` i `IsAccept`.

## Tworzenie nowej klasy weryfikatora — krok po kroku

1. **Wybierz klasę bazową** wg liczby pól-źródeł (tabela wyżej).
2. **Konstruktor** (najczęściej primary constructor) przyjmuje wiersz; dla
   `ColVerifier<T>`/`MultiColVerifier<T>` przekaż też nazwę(-y) pól (`nameof(...)`) do konstruktora
   bazowego: `class KodVerifier(Towar row) : ColVerifier<Towar>(row, nameof(Towar.Kod))`.
3. **`IsValid()`** — zaimplementuj regułę. Zwracaj `true` również dla stanów „nie dotyczy"
   (np. brak wartości, którą reguła porównuje), aby nie generować fałszywych błędów.
4. **`Description`** — komunikat przez `.Translate()` lub `.TranslateFormat()`.
5. **`Type`** — nadpisz, jeśli ma to być `Warning`/`Information` (domyślnie `Error`).
6. **`IsAccept(...)`** — tylko dla `RowVerifier<T>`: wskaż `data == Row && property == "..."`
   dla każdego pola-źródła.
7. **Uzbrój** weryfikator (business.xml lub `Session.Verifiers.Add`).

## Przykłady

### RowVerifier<T> — reguła obejmująca cały wiersz (kilka źródeł, w tym pod-obiekty)

Gdy reguły nie da się sprowadzić do jednego pola — np. suma wartości pozycji dokumentu musi równać
się wartości nagłówka — użyj `RowVerifier<T>` i sam wskaż źródła w `IsAccept`. Tu źródłem jest pole
nagłówka **oraz** zmiana na dowolnej pozycji (pod-wierszu):

```csharp
internal sealed class SumaPozycjiVerifier(Dokument row) : RowVerifier<Dokument>(row) {
    public override VerifierType Type => VerifierType.Error;   // rozbieżność sum blokuje zapis
    public override string Description => "Suma wartości pozycji nie zgadza się z wartością dokumentu".Translate();

    protected override bool IsAccept(object data, string property, VerifierAction action)
        => (data == Row && property == nameof(Dokument.Wartosc))   // źródło: wartość nagłówka
        || (data is Pozycja pozycja && pozycja.Dokument == Row);   // źródło: dowolna pozycja dokumentu

    protected override bool IsValid()
        => Row.Pozycje.Aggregate(Currency.Zero, (suma, p) => suma + p.Wartosc) == Row.Wartosc;
}
```

Taki weryfikator uzbrajasz zarówno w setterze wartości nagłówka, jak i przy zmianie pozycji
(`Session.Verifiers.Add(new SumaPozycjiVerifier(dokument))`) — dedup zadba o jedną instancję.

### ColVerifier<T> — jedno pole, bez pisania IsAccept

`ColVerifier<T>` sam ogranicza sprawdzenie do wskazanego pola (podajesz je w konstruktorze),
więc nadpisujesz tylko `IsValid`/`Description`/`Type`:

```csharp
internal sealed class ElementGrupaVerifier(Zaszeregowanie row)
    : ColVerifier<Zaszeregowanie>(row, nameof(Zaszeregowanie.Element)) {   // źródło: pole Element

    protected override bool IsValid()
        => Row.Host is not Etat etat                                       // pattern matching zamiast 'as' + null-check
        || etat.Grupa is null
        || etat.Grupa.TypStawki == TypStawkiZaszeregowania.Nieokreślona
        || etat.Zaszeregowanie.Element == etat.DefinicjaStanowiskaHistoria?.Zaszeregowanie.Element;

    public override string Description => "Wartość pola 'Element' jest niezgodna z grupą zaszeregowania.".Translate();
    public override VerifierType Type => VerifierType.Warning;
}
```

### MultiColVerifier<T> — reguła zależna od kilku pól

Sprawdzany, gdy zmieni się którekolwiek z pól z listy. Tu: przynajmniej jedno z dwóch pól ma być
uzupełnione.

```csharp
internal sealed class KwotaLubKoncowkaVerifier(OdpisOPP row)
    : MultiColVerifier<OdpisOPP>(row, nameof(OdpisOPP.Kwota), nameof(OdpisOPP.Koncowka)) {   // dwa źródła

    protected override bool IsValid()
        => Row.Organizacja is null || Row.Kwota != Currency.Zero || Row.Koncowka != 0;

    public override string Description => "Jedno z pól 'Kwota', 'Końcówka' powinno zostać uzupełnione.".Translate();
    public override VerifierType Type => VerifierType.Warning;
}
```

### RequiredVerifier — pole wymagane

`RequiredVerifier` aktywuje się dopiero przy zatwierdzeniu wiersza (`AcceptRow`), więc nie krzyczy
o pustym polu w trakcie wypełniania formularza. Podajesz nazwę pola i warunek „jest wypełnione":

```csharp
internal sealed class OkresVerifier(IRow row) : RequiredVerifier(row, nameof(RegulaDostepnosci.Okres)) {
    new RegulaDostepnosci Row => (RegulaDostepnosci)base.Row;

    protected override bool IsValid() => Row is null || !Row.Okres.From.IsNull || !Row.IsCykl;
}
```

`RequiredVerifier` sam dobiera treść komunikatu („Wartość pola … jest wymagana" dla `Error`,
„Pole … nie powinno być puste" dla `Warning`).

### Przykład docelowy: PESEL a data urodzenia i płeć

Numer PESEL musi być zgodny z datą urodzenia i płcią. Każdy aspekt to osobny weryfikator typu
`Warning` (dane bywają niekompletne w trakcie wpisywania), a **klasę bazową dobieramy wg liczby
źródeł**: poprawność samego numeru zależy od **jednego** pola (`PESEL`) → `ColVerifier<T>`, natomiast
zgodność z datą urodzenia od **dwóch** pól (`PESEL`, `DataUrodzenia`) → `MultiColVerifier<T>`. Dzięki
temu nie piszemy `IsAccept` — pola-źródła podaje konstruktor, resztą zajmuje się klasa bazowa. Wariant
„PESEL a płeć" buduje się analogicznie (`MultiColVerifier<T>`, źródła `PESEL` i `Plec`):

```csharp
// Poprawność samego numeru PESEL — jedno źródło → ColVerifier<T>
public sealed class PeselVerifier(Pracownik row)
    : ColVerifier<Pracownik>(row, nameof(Pracownik.PESEL)) {

    protected override bool IsValid() => Row.PESEL.IsNullOrEmpty() || Pesel.Test(Row.PESEL);
    public override string Description => "Wprowadzono błędny numer PESEL".Translate();
    public override VerifierType Type => VerifierType.Warning;
}

// Zgodność PESEL-u z datą urodzenia — dwa źródła → MultiColVerifier<T>
public sealed class PeselDataVerifier(Pracownik row)
    : MultiColVerifier<Pracownik>(row, nameof(Pracownik.PESEL), nameof(Pracownik.DataUrodzenia)) {

    protected override bool IsValid() => !Pesel.Test(Row.PESEL) || Pesel.Test(Row.PESEL, Row.DataUrodzenia);
    public override string Description => "Numer PESEL nie jest zgodny z datą urodzenia".Translate();
    public override VerifierType Type => VerifierType.Warning;
}
```

Rejestracja obu weryfikatorów przy zmianie każdego ze źródeł (w setterach pól PESEL i DataUrodzenia):

```csharp
public string PESEL {
    get => record.PESEL;
    set {
        record.PESEL = value;
        if (State != RowState.Detached) {
            Session.Verifiers.Add(new PeselVerifier(this));
            Session.Verifiers.Add(new PeselDataVerifier(this));   // zależy też od PESEL
        }
    }
}

public Date DataUrodzenia {
    get => record.DataUrodzenia;
    set {
        record.DataUrodzenia = value;
        if (State != RowState.Detached)
            Session.Verifiers.Add(new PeselDataVerifier(this));   // to samo źródło z drugiej strony
    }
}
```

Ponieważ `Add` jest idempotentne, wielokrotne uzbrajanie tego samego weryfikatora (raz z settera
PESEL, raz z DataUrodzenia) jest bezpieczne — w kolekcji będzie jedna instancja `PeselDataVerifier`.

Ponieważ oba weryfikatory dziedziczą po `ColVerifier<T>`/`MultiColVerifier<T>`, nie piszą `IsAccept` —
klasa bazowa sama ogranicza sprawdzenie do pól podanych w konstruktorze (`nameof(...)`).

## Typy pomocnicze

| Typ | Rola |
|-----|------|
| `IVerifier` | kontrakt weryfikatora: `Description`, `Type`, `Validate()`, `TestAccept(...)`, `Source`, `Tested` |
| `IVerifiable` | kontrakt odczytu weryfikatorów przez UI (`GetVerifiers()`); implementują go obiekty nie-sesyjne edytowane na formularzu, by mógł on pokazać ich weryfikatory — patrz [sekcja wyżej](#odczyt-weryfikatorów-przez-ui-iverifiablegetverifiers) |
| `IExDescription` | rozszerzony opis techniczny (typ weryfikatora + typ wiersza); implementuje go `RowVerifier` |
| `VerifiersException` | zbiorczy wyjątek walidacji; `Verifiers[]` + flaga `Error` (czy wśród nich jest błąd) |
| `VerifierCollection` | `Session.Verifiers` — kolekcja z dedup (`Add`/`Remove`/`Contains`) i walidacją |

`Verifier` dziedziczy po `BusException` — niespełniony weryfikator może zostać
**rzucony** jako wyjątek (tak działa blokada zapisu przy błędzie).

## Checklista weryfikatora

- [ ] Klasa bazowa dobrana do liczby pól-źródeł (`ColVerifier<T>` / `MultiColVerifier<T>` / `RowVerifier<T>`).
- [ ] `IsValid()` zwraca `true` dla stanów „nie dotyczy" (brak danych do porównania) — brak fałszywych alarmów.
- [ ] `Type` ustawiony świadomie: `Error` tylko dla naruszeń, których nie wolno zapisać.
- [ ] Źródła (`IsAccept` lub nazwy pól w konstruktorze) pokrywają **wszystkie** pola wpływające na regułę.
- [ ] Weryfikator uzbrojony na **każde** źródło (business.xml `<verifier>` lub `Session.Verifiers.Add`).
- [ ] `Description` przez `.Translate()`.
- [ ] Uzbrajanie tylko dla wiersza nie-`Detached` (`State != RowState.Detached`), gdy robisz to ręcznie.
- [ ] Kod zapisu **nie połyka** `VerifiersException`/`BusException` z `Save()` — patrz [safe-code.md §5](safe-code.md) i [§9](safe-code.md).

## Pełny przykład — obiekt biznesowy z weryfikatorem od zera

Reguła: w kartotece kontrahenta pole `Email` jest wymagane, gdy zaznaczono `WysylkaEfaktury`.
Źródła: `Email` oraz `WysylkaEfaktury`.

```csharp
public partial class Kontrahent {

    // 1) Weryfikator — dwa pola-źródła, więc MultiColVerifier<T>
    internal sealed class EmailDlaEfakturyVerifier(Kontrahent row)
        : MultiColVerifier<Kontrahent>(row, nameof(Email), nameof(WysylkaEfaktury)) {

        protected override bool IsValid()
            => !Row.WysylkaEfaktury || !Row.Email.IsNullOrEmpty();

        public override string Description
            => "Przy wysyłce e-faktury pole 'Email' jest wymagane".Translate();

        public override VerifierType Type => VerifierType.Error;   // blokuje Save()
    }

    // 2) Uzbrojenie na zmianę każdego ze źródeł
    public string Email {
        get => record.Email;
        set {
            record.Email = value;
            ArmEfakturaVerifier();
        }
    }

    public bool WysylkaEfaktury {
        get => record.WysylkaEfaktury;
        set {
            record.WysylkaEfaktury = value;
            ArmEfakturaVerifier();
        }
    }

    void ArmEfakturaVerifier() {
        if (State != RowState.Detached)                             // nie uzbrajamy Detached
            Session.Verifiers.Add(new EmailDlaEfakturyVerifier(this));  // dedup: jedna instancja
    }
}
```

```csharp
// 3) Efekt przy zapisie
using (var tr = session.Logout(editMode: true)) {
    kontrahent.WysylkaEfaktury = true;   // uzbraja weryfikator (Email pusty → reguła niespełniona)
    tr.Commit();
}
session.Save();   // ← rzuca VerifiersException (Error): zapis zablokowany, dopóki Email pusty
```

Po uzupełnieniu `Email` kolejne uzbrojenie sprawi, że przy najbliższej walidacji `IsValid()` zwróci
`true`, weryfikator zniknie z kolekcji, a `Save()` przejdzie.

## Powiązane materiały

- [safe-code.md](safe-code.md) — §5 „Walidacja danych" i §9 „Obsługa wyjątków": nie połykaj `VerifiersException` z `Save()`.
- [session-login.md](session-login.md) — `Session.Save()` / transakcje; tu odpalana jest walidacja błędów przed zapisem.
- [events.md](events.md) — bliźniacza kolekcja sesyjna (`Session.Events`/`ServerEvents`): ten sam model transakcyjności i dedup po `Equals`/`GetHashCode`; do odraczania ciężkich przeliczeń zamiast liczenia ich przy każdej zmianie pola.
- [features.md](features.md) — cechy wymagane korzystają z tego samego mechanizmu (weryfikator „cecha wymagana").
- Deklaratywna rejestracja: element `<verifier>` przy kolumnie, atrybut `onadded` ([table-reference.md](../../business-xml/references/table-reference.md)).
- **Skill [form-xml](../../form-xml/SKILL.md)** — prezentacja komunikatów weryfikatorów w formularzu (błędy/ostrzeżenia przy polach).
