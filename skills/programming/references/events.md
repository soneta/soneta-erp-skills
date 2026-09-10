# Eventy (zdarzenia sesji) — odraczanie i serwerowe wykonanie logiki

Event to **metoda logiki biznesowej odłożona do momentu, gdy jej wynik jest naprawdę potrzebny**.
Zamiast wykonywać ciężki algorytm po każdej zmianie property, rejestrujesz w sesji *potrzebę* jego
uruchomienia; faktyczne wykonanie następuje raz — przy odczycie wyniku, przy zatwierdzeniu edycji z
UI lub przy zapisie sesji. Dzięki temu algorytm zależny od wielu zmian wykonuje się jednokrotnie
(złożoność często spada z `O(n²)` do `O(n)`), a logika krytyczna dla spójności danych może
zostać wykonana wewnątrz transakcji bazodanowej.

## Najważniejsze zasady (must-know)

1. **Dwa rodzaje eventów, różny cel:**
   - **Sesyjne** (`Session.Events`) — odraczają **ciężki algorytm** wyzwalany przez wiele różnych
     zmian (np. przeliczenie sumy pozycji dokumentu). Wykonują się „leniwie", gdy wynik jest
     potrzebny. Mogą trwać dłużej.
   - **Serwerowe** (`Session.ServerEvents`) — wykonują logikę **wewnątrz transakcji bazodanowej**
     podczas `Session.Save()`. Służą danym zależnym od zmian robionych równolegle na innych
     stanowiskach (np. ciągła numeracja dokumentów). **Muszą być szybkie** — trzymają transakcję
     serwerową, więc długie obliczenia zwiększają ryzyko kolizji i spowalniają cały system.
2. **Event to metoda o sygnaturze `BusEventHandler`** (`void M(BusEventArgs args)`), najczęściej
   metoda instancji obiektu dziedziczącego po `Row` lub `Table`. Rejestracja = dodanie delegata do
   kolekcji: `Session.Events.Add(handler[, args])` lub `Session.ServerEvents.Add(handler[, args])`.
3. **Rejestracja jest idempotentna (dedup).** Wielokrotne dodanie tego samego delegata (+ równych
   argumentów) tworzy **jeden** wpis. O tożsamości decyduje trójka: *nazwa metody + obiekt docelowy
   (`Target`) + argumenty (`Args.Equals`/`GetHashCode`)*. Dlatego własna klasa argumentów **musi**
   nadpisywać `Equals` i `GetHashCode`.
4. **Rejestruj event tam, gdzie zachodzi zmiana** uzasadniająca przeliczenie — w setterze property
   lub w metodzie, która zmienia dane. Rejestracja odbywa się wewnątrz transakcji edycyjnej
   (`Session.Logout(editMode: true)`), bo każda zmiana danych i tak jej wymaga.
5. **Przed odczytem wartości liczonej przez event sesyjny wymuś jego wykonanie:**
   `Session.Events.Invoke(handler[, args])` — odpali konkretny event, jeśli czeka w kolejce (inaczej
   no-op). Bez tego świeżo zarejestrowany event nie wykonał się jeszcze i odczytasz nieaktualną wartość.
6. **Kiedy odpalają się wszystkie zarejestrowane eventy:**
   - **sesyjne** — przy `ITransaction.CommitUI()` (zatwierdzenie edycji z UI), ręcznie przez
     `Session.Events.Invoke()`, oraz **zawsze na początku `Session.Save()`**;
   - **serwerowe** — w transakcji serwerowej wewnątrz `Session.Save()` (nie da się ich odpalić poza
     zapisem).
   > **Pułapka:** `Commit()` (kod biznesowy) **nie** odpala eventów sesyjnych — robi to dopiero
   > `CommitUI()` albo `Save()`, albo ręczny `Invoke`. W czystym kodzie biznesowym polegaj na
   > `Invoke(handler)` przed odczytem lub na tym, że `Save()` je zdrenuje.
7. **Eventy są w pełni transakcyjne.** Rejestracja/wyrejestrowanie uczestniczy w bieżącej transakcji
   sesyjnej i jest **automatycznie wycofywane** przy rollbacku (`Session.Logout()` bez `Commit`).
8. **Do eventu per-wiersz użyj gotowej klasy `RowEventArgs<TRow>`** — przechowuje `ID` wiersza (nie
   referencję), ma poprawne `Equals`/`GetHashCode` i zwraca żywy wiersz przez `GetRow(...)`. Sam
   pisz `BusEventArgs` tylko dla nietypowych parametrów.

## Minimalny event sesyjny + rejestracja i odczyt (skrót)

```csharp
public partial class Dokument {

    Currency suma;

    // 1) EVENT — ciężkie przeliczenie sumy pozycji (uruchamiane leniwie)
    void PrzeliczSume(BusEventArgs args) {
        suma = Pozycje.Aggregate(Currency.Zero, (s, p) => s + p.Wartosc);
    }

    // 2) REJESTRACJA — wołana przy KAŻDEJ zmianie pozycji; dedup zostawia jeden wpis
    internal void ZarejestrujPrzeliczenieSumy()
        => Session.Events.Add(PrzeliczSume);        // Target = ten wiersz, Args = Empty

    // 3) ODCZYT — wymuś event, jeśli czeka w kolejce, potem zwróć wynik
    public Currency Suma {
        get {
            Session.Events.Invoke(PrzeliczSume);    // odpali PrzeliczSume, jeśli zarejestrowany
            return suma;
        }
    }
}
```

Setter pozycji tylko **rejestruje potrzebę** przeliczenia (tanio, `O(1)`), zamiast przeliczać sumę
za każdym razem:

```csharp
public Currency Wartosc {
    get => record.Wartosc;
    set {
        record.Wartosc = value;
        Dokument?.ZarejestrujPrzeliczenieSumy();    // n zmian → jedna rejestracja → jedno przeliczenie
    }
}
```

### Zabezpieczenia handlera przeliczającego (potwierdzone w praktyce)

Handler „przelicz przy zapisie" (drenowany przy `CommitUI()`/`Save()`), który **edytuje wiersze
podrzędne**, może przez ich settery ponownie uzbrajać samego siebie. Dwa guardy:

1. **Guard reentrancji** — pole `bool przeliczanie` blokuje wejście handlera w trakcie własnego
   wykonania.
2. **Guardy zmiany** — przypisuj tylko przy faktycznej różnicy (`if (pole != nowa) pole = nowa;`).
   Ograniczają zbędne edycje i pętle zdarzeń oraz zapewniają zbieżność w ≤2 przebiegach.

```csharp
bool przeliczanie;

void PrzeliczSume(BusEventArgs args) {
    if (przeliczanie) return;              // guard reentrancji
    przeliczanie = true;
    try {
        var nowa = Pozycje.Aggregate(Currency.Zero, (s, p) => s + p.Wartosc);
        if (suma != nowa) suma = nowa;     // guard zmiany — brak edycji bez różnicy
    }
    finally { przeliczanie = false; }
}
```

## Sesyjne vs serwerowe — porównanie

| Cecha | Eventy sesyjne (`Session.Events`) | Eventy serwerowe (`Session.ServerEvents`) |
|-------|-----------------------------------|-------------------------------------------|
| **Cel** | odroczenie ciężkiego algorytmu do momentu potrzeby | logika w transakcji bazodanowej, zależna od równoległych zmian |
| **Kiedy się wykonują** | `CommitUI()`, ręczny `Invoke()`, początek `Save()` | transakcja serwerowa wewnątrz `Save()` |
| **Ręczne odpalenie** | tak — `Invoke()` / `Invoke(handler[, args])` | nie — poza zapisem `Invoke` rzuca `ExpectedSaveException` |
| **Czas wykonania** | może być dłuższy | **musi być krótki** (trzyma transakcję serwerową) |
| **Typowy przykład** | suma pozycji, przeliczenia zależne od wielu pól | ciągła numeracja dokumentów, rezerwacja numeru/zasobu |
| **Odczyt wyniku przed Save** | `Invoke(handler)` wymusza obliczenie | niedostępny przed zapisem (liczone w trakcie `Save`) |

Podczas `Save()` po każdym evencie serwerowym platforma dodatkowo drenuje eventy sesyjne — zmiany
wprowadzone przez event serwerowy natychmiast wyzwalają zależne od nich przeliczenia sesyjne.

## Jak działa dedup (klucz unikalności)

Kolekcja identyfikuje event trójką **(nazwa metody, obiekt `Target` delegata, argumenty)**:

- Ten sam delegat rejestrowany wielokrotnie (nawet tworzony na nowo jako `obiekt.Metoda`) →
  **jeden** wpis, bo `Target` i nazwa metody są równe.
- **Różne argumenty** (wg `Equals`) → **osobne** wpisy. To pozwala zarejestrować ten sam handler dla
  wielu wierszy — rozróżnia je klasa argumentów (`RowEventArgs<TRow>` po `ID` wiersza).

Stąd wymóg poprawnych `Equals`/`GetHashCode` w klasie argumentów: bez nich dedup i odczyt przez
`Invoke(handler, args)` nie zadziałają (nie odnajdą właściwego wpisu).

> **Rejestracja handlera będącego metodą `Row`** przełącza ten wiersz w tryb edycji (`SetEdit`),
> więc wiersz musi być „żywy" i musisz być w transakcji edycyjnej. Handler na `Table`/`Module`
> (singleton w sesji) nie wymusza edycji wiersza — użyj go z `RowEventArgs<TRow>`, gdy nie chcesz
> uzależniać rejestracji od stanu edycji konkretnego wiersza.

## Klasy argumentów (BusEventArgs)

Argumenty przenoszą parametry uruchomienia eventu **i** wyznaczają jego unikalność. Hierarchia:

| Klasa | Kiedy używać | Co dostarcza |
|-------|--------------|--------------|
| `BusEventArgs.Empty` | event bez parametrów, jeden na `(metoda, Target)` | wspólny pusty singleton; `Add(handler)` używa go domyślnie |
| `RowEventArgs<TRow>` | event dotyczący **konkretnego wiersza** | trzyma `ID` wiersza; `GetRow(ISessionable)` zwraca żywy wiersz; gotowe `Equals`/`GetHashCode` po `ID` |
| `SessionBusEventArgs` | event dotyczący **całej sesji**, nie pojedynczego wiersza | trzyma `Session`; równość po sesji |
| własna klasa : `BusEventArgs` | nietypowe parametry (np. okres, tryb) | **sam** nadpisz `Equals` + `GetHashCode` |

**Wzorzec per-wiersz z `RowEventArgs<TRow>`** — handler na tabeli, wiersz rozpoznawany po argumentach.
W handlerze **nie przechowuj referencji do wiersza** — pobierz go z argumentów przez `GetRow`, aby
pracować na żywym wierszu w bieżącej transakcji:

```csharp
public partial class Dokumenty {   // klasa tabeli

    // Event na tabeli — wiersz przekazany w argumentach
    void PrzeliczSume(BusEventArgs args) {
        Dokument dok = ((RowEventArgs<Dokument>)args).GetRow(this);   // żywy wiersz z bieżącej sesji
        dok.PrzeliczSumeWewn();
    }

    internal void ZarejestrujPrzeliczenie(Dokument dok)
        => Session.Events.Add(PrzeliczSume, new RowEventArgs<Dokument>(dok));   // dedup po ID wiersza
}
```

Odczyt wymusza event **z równymi argumentami** (równość po `ID`, więc wystarczy nowa instancja
`RowEventArgs` na ten sam wiersz):

```csharp
Session.Events.Invoke(tabela.PrzeliczSume, new RowEventArgs<Dokument>(dok));
```

**Własna klasa argumentów** — pamiętaj o `Equals`/`GetHashCode` (bez nich brak dedup):

```csharp
public sealed class OkresEventArgs : BusEventArgs {
    public Date Od { get; }
    public Date Do { get; }
    public OkresEventArgs(Date od, Date @do) { Od = od; Do = @do; }

    public override bool Equals(object o)
        => o is OkresEventArgs a && a.Od == Od && a.Do == Do;
    public override int GetHashCode() => Od.GetHashCode() ^ Do.GetHashCode();
}
```

> Klasa argumentów przenoszona między sesjami wymaga nadpisania `CloneToSession` (domyślnie rzuca
> wyjątek). W typowej pracy w jednej sesji nie jest to potrzebne — `RowEventArgs<TRow>` i tak
> operuje na `ID`, więc odtwarza wiersz w docelowej sesji przez `GetRow`.

## Delegaty

```csharp
public delegate void BusEventHandler(BusEventArgs args);            // handler rejestrowany w kolekcji
public delegate void BusEventHandler<T>(T args) where T : BusEventArgs;  // wariant typowany
```

Metody `Add`/`Invoke` przyjmują **nietypowy** `BusEventHandler`. W praktyce handler deklarujesz jako
`void M(BusEventArgs args)` i **rzutujesz** `args` na konkretny typ w środku (jak wyżej z
`RowEventArgs<Dokument>`).

## Rejestracja, odczyt i usuwanie

```csharp
// Rejestracja
Session.Events.Add(handler);                       // Args = Empty, priorytet 100
Session.Events.Add(handler, args);                 // z argumentami
Session.Events.Add(handler, args, priority);       // z priorytetem (niższy = wcześniej; domyślnie 100)

// Wymuszenie wykonania
Session.Events.Invoke();                           // odpala WSZYSTKIE zarejestrowane (drenaż do pustej kolekcji)
Session.Events.Invoke(handler[, args]);            // odpala JEDEN konkretny event, jeśli czeka; potem go usuwa
Session.Events.InvokeInTransaction(handler[, args]);  // jw., ale sam otwiera Logout(true)+Commit

// Zapytania i czyszczenie
Session.Events.Contains(handler[, args]);          // czy zarejestrowany
Session.Events.Contains(obiekt);                   // czy jest event powiązany z obiektem
Session.Events.Remove(handler[, args]);            // wyrejestruj konkretny
Session.Events.Remove(obiekt);                     // wyrejestruj wszystkie eventy tego obiektu
Session.Events.IsEmpty;                            // czy kolekcja pusta
```

- **`Invoke()`** (bez argumentów) opróżnia kolekcję do końca — event może w trakcie zarejestrować
  kolejne, one też zostaną wykonane (punkt stały). To wariant wołany przez `CommitUI()` i `Save()`.
- **`Invoke(handler[, args])`** to typowy sposób odczytu wartości liczonej leniwie: wymusza tylko ten
  jeden event tuż przed odczytem.
- **`InvokeInTransaction(...)`** użyj, gdy wywołujesz odczyt spoza otwartej transakcji edycyjnej, a
  event modyfikuje dane — sam owinie wykonanie w `Session.Logout(true)` + `Commit`.
- Ponowna rejestracja eventu **w trakcie jego własnego wykonania** (w tej samej transakcji) jest
  pomijana — nie wpadniesz w nieskończoną pętlę rejestracji z wnętrza handlera.

## Priorytety

`Add(handler, args, priority)` — **niższy priorytet wykonuje się wcześniej** (kolejka rosnąca,
domyślnie `100`). Stosuj, gdy jeden event zależy od wyniku innego (np. najpierw przelicz pozycje,
potem sumy nagłówka).

## Transakcyjność

Rejestracja eventów jest **w pełni transakcyjna** — uczestniczy w tej samej transakcji biznesowej
(`ITransaction`), co zmiany danych:

- **Rollback** (Dispose transakcji bez `Commit`/`CommitUI`) cofa również `Add`/`Remove` — kolekcja
  eventów wraca do stanu sprzed transakcji. Jeśli setter zarejestrował event, a edycję wycofano,
  event **nie zostaje** w sesji.
- **Commit/CommitUI** utrwala rejestrację; przy transakcjach zagnieżdżonych stan przenosi się do
  transakcji nadrzędnej.
- Eventy wierszy, które stały się `Detached` (odłączone/usunięte), są automatycznie sprzątane.

To ten sam model, co przy [weryfikatorach](verifiers.md#transakcyjność-rejestracji). Szczegóły
transakcji, `Commit`/`CommitUI` i rollbacku: [session-login.md](session-login.md).

## Przykład: event serwerowy — ciągła numeracja

Numer musi być ciągły mimo równoległej pracy wielu stanowisk, więc kolejny numer wolno wyliczyć
dopiero w **transakcji serwerowej** podczas `Save()` — inaczej dwa stanowiska mogłyby pobrać ten sam
numer. Handler musi być **krótki**:

```csharp
public partial class Dokument {

    // EVENT SERWEROWY — wykona się w transakcji serwerowej w trakcie Save()
    void NadajNumer(BusEventArgs args) {
        if (!Numer.IsNullOrEmpty()) return;             // idempotencja — mógł już dostać numer
        Numer = Definicja.PobierzKolejnyNumer();        // szybka rezerwacja pod blokadą serwerową
    }

    // REJESTRACJA — przy zatwierdzaniu dokumentu do numeracji
    internal void ZarejestrujNumeracje()
        => Session.ServerEvents.Add(NadajNumer, 1000);  // priorytet 1000 = późno, po innych eventach
}
```

Numeru nie odczytasz „na żądanie" przed zapisem — powstaje w trakcie `Save()`. To celowe: rezerwacja
numeru poza transakcją serwerową łamałaby ciągłość. Numeracja bywa **konfigurowalna** i dobrze ilustruje
wybór kolekcji: numer nadawany „podczas zapisu" → **serwerowo** (`ServerEvents`), numer „na bieżąco"
w trakcie edycji → **sesyjnie** (`Events`).

> **Kaskada i limit.** Podczas `Save()` platforma powtarza cykl *eventy serwerowe → eventy sesyjne →
> weryfikatory*, dopóki obie kolekcje nie będą puste — event może więc dołożyć kolejne. Zabezpieczeniem
> przed nieskończoną kaskadą jest limit iteracji (przekroczenie rzuca `RecursiveSaveException`), więc
> pilnuj, by eventy serwerowe się „zbiegały" (były idempotentne i nie rejestrowały się w kółko).

## Wybór: sesyjny czy serwerowy?

- Wynik potrzebny **w trakcie edycji / do odczytu w UI**, obliczenie może być cięższe, nie zależy od
  równoległych zmian innych stanowisk → **sesyjny**.
- Logika musi być spójna wobec **równoległych zmian z innych stanowisk** i wymaga atomowości w bazie
  (numeracja, rezerwacja zasobu, sekwencje) → **serwerowy**, a algorytm trzymaj krótki.

## Checklista eventu

- [ ] Wybrany właściwy rodzaj: sesyjny (`Session.Events`) do odraczania obliczeń / serwerowy
      (`Session.ServerEvents`) do logiki w transakcji bazodanowej.
- [ ] Handler ma sygnaturę `void M(BusEventArgs args)`; dla per-wiersz rzutuje `args` na
      `RowEventArgs<TRow>` i pobiera wiersz przez `GetRow` (bez trzymania referencji).
- [ ] Rejestracja (`Add`) w miejscu zmiany property/metody, wewnątrz transakcji edycyjnej.
- [ ] Własna klasa `BusEventArgs` ma nadpisane `Equals` **i** `GetHashCode` (inaczej brak dedup).
- [ ] Odczyt wartości liczonej przez event sesyjny poprzedzony `Invoke(handler[, args])` z **równymi**
      argumentami.
- [ ] Event serwerowy jest **krótki** i **idempotentny** (może zostać wykonany raz w transakcji Save).
- [ ] Nie polegasz na `Commit()` (biznesowym) do odpalenia eventów sesyjnych — używasz `Invoke`
      albo `Save()`/`CommitUI()`.
- [ ] Handler edytujący wiersze podrzędne ma **guard reentrancji** (`bool przeliczanie`) i **guardy
      zmiany** (`if (pole != nowa) pole = nowa;`) — zbieżność w ≤2 przebiegach.

## Powiązane materiały

- [session-login.md](session-login.md) — transakcje (`Logout`, `Commit`/`CommitUI`), `Save()`; to
  tam odpalają się eventy (CommitUI, początek Save) i tam działa ich transakcyjność.
- [verifiers.md](verifiers.md) — bliźniaczy mechanizm kolekcji sesyjnej: transakcyjność, dedup po
  `Equals`/`GetHashCode`, uzbrajanie na zmianę pól-źródeł.
- [row-types.md](row-types.md) — settery property, w których zwykle rejestrujesz eventy.
- [safe-code.md](safe-code.md) — zasady bezpiecznego kodu biznesowego (checklist do review).
- **Skill [form-xml](../../form-xml/SKILL.md)** — akcje UI (Command, edycja pól) kończą się `CommitUI()`, co wyzwala
  eventy sesyjne; tam opisana jest warstwa interfejsu wywołująca to zatwierdzenie.
