# Klasa Context (kontekst)

Dokumentacja klasy Context odpowiedzialnej za komunikację między warstwą logiki biznesowej a interfejsem graficznym.

## Czym jest Context?

Context to **kontener par klucz-wartość**, gdzie:
- **Klucz** = typ (Type)
- **Wartość** = obiekt tego typu (object)

Context jest stale aktualizowany podczas pracy z programem i przechowuje informacje o aktualnym stanie interfejsu.

## Context jest zawsze powiązany z sesją

Każdy kontekst jest **związany z konkretną sesją** (`Session`) - jest od niej zależny i udostępnia ją przez
`Context.Session` (implementuje `ISessionable`). To powiązanie pociąga za sobą ważną regułę:

> **Obiekty umieszczane w kontekście muszą pochodzić z tej samej sesji co sam kontekst.**
> Nie wolno mieszać w jednym kontekście obiektów z różnych sesji.

Wynika to z faktu, że obiekty biznesowe (`Row` i pochodne) są single-threaded i żyją w obrębie jednej sesji -
ten sam rekord wczytany w dwóch sesjach to dwa różne obiekty. Gdy potrzebujesz operować na obiektach
z innej sesji, najpierw przenieś je do właściwej sesji (np. `session.Get(obiekt)`) albo przenieś cały
kontekst do tej sesji - patrz klonowanie poniżej.

## Klonowanie kontekstu do innej sesji

Kontekst można przenieść do wskazanej sesji - tworzony jest wtedy kontekst związany z tą sesją,
do którego kopiowane są wartości z kontekstu bazowego.

```csharp
// Zawsze tworzy NOWY obiekt kontekstu związany z podaną sesją
// i kopiuje do niego wartości z kontekstu bazowego.
Context kopia = context.Clone(session);

// Zwraca kontekst w podanej sesji. Jeśli sesja kontekstu pokrywa się
// z podaną sesją, zwracany jest TEN SAM obiekt (bez kopiowania);
// w przeciwnym razie zachowuje się jak Clone.
Context znormalizowany = context.Normalize(session);
```

Różnica jest istotna wydajnościowo i semantycznie:
- `Clone(session)` - **zawsze** nowy obiekt (nawet gdy sesja się zgadza),
- `Normalize(session)` - nowy obiekt **tylko gdy** trzeba zmienić sesję; gdy sesja już pasuje, oddaje
  oryginał. Używaj `Normalize`, gdy zależy Ci tylko na tym, by kontekst był „w danej sesji", a nie
  na tym, by koniecznie powstała kopia.

## Tworzenie nowego kontekstu od zera

Gdy potrzebujesz kontekstu, którego nie dostajesz z UI (np. w kodzie biznesowym wywołującym worker),
masz trzy sposoby utworzenia go od podstaw.

### 1. Sklonowanie pustego kontekstu w sesji

```csharp
Context cx = Context.Empty.Clone(session);
```

`Context.Empty` to współdzielony, pusty kontekst-szablon; jego sklonowanie daje świeży, pusty kontekst
związany z podaną sesją.

### 2. Pusty kontekst powiązany z sesją

```csharp
Context cx = session.GetEmptyContext();
```

`GetEmptyContext()` zwraca pusty kontekst powiązany z daną sesją. **Uwaga:** metoda zwraca wciąż
**ten sam obiekt** - może on być już wypełniony wartościami z wcześniejszych wywołań `GetEmptyContext`
na tej sesji. Jeśli potrzebujesz gwarancji świeżego, czystego kontekstu, użyj sposobu 1
(`Context.Empty.Clone(session)`).

### 3. Utworzenie kontekstu wraz z nową sesją (`Login.CreateEmptyContext`)

```csharp
// sessionReadOnly: true = sesja tylko do odczytu, false = sesja edycyjna
// sessionName: nazwa tworzonej sesji
Context cx = login.CreateEmptyContext(sessionReadOnly: true, sessionName: "MójKontekst");
try
{
    // ... praca z kontekstem i jego sesją cx.Session ...
}
finally
{
    cx.Session.Dispose();   // pamiętaj o zwolnieniu utworzonej sesji
}
```

`Login.CreateEmptyContext(bool? sessionReadOnly, string sessionName)` (jest też przeciążenie
`CreateEmptyContext(bool? sessionReadOnly)`) tworzy nowy, pusty kontekst i przypisuje go do
**nowo utworzonej sesji** (tylko do odczytu albo edycyjnej, zależnie od `sessionReadOnly`). Ponieważ ta
metoda tworzy sesję, **odpowiadasz za jej zwolnienie** - nie zapomnij o `Dispose` na `cx.Session`
(najlepiej w `try/finally` lub przez `using`).

## Zawartość context

Przykładowa zawartość przy otwartej liście kontrahentów:

| Typ | Opis                                   |
|-----|----------------------------------------|
| `SelectedCounter` | Liczba zaznaczonych wierszy na gridzie |
| `Kontrahent[]` | Kolekcja zaznaczonych kontrahentów     |
| `UILocation` | Aktywny element interfejsu             |
| `INavigatorContext` | Context grida (zaznaczenia, focus)     |
| `View` | Źródło danych grida                    |
| `Params` | Klasa parametrów filtrów               |
| `Session` | Gdy aktywny widok z danymi |
| `Login` | Zalogowany użytkownik                  |
| `[ViewInfo]+Params` | Klasa parametrów widoku |

## Odczyt z context

### Metody GetOrDefault i GetRequired (zalecane)

```csharp
public void Action(Context context)
{
    // Zwraca obiekt lub null, gdy brak obiektu
    Kontrahent? knt = context.GetOrDefault<Kontrahent>();

    // Zwraca obiekt lub wyjątek, gdy brak obiektu
    Kontrahent knt2 = context.GetRequired<Kontrahent>();
}
```

### Przez indeksator - gdy typ określony wartością `Type`

```csharp
public void Action(Context cx)
{
    // Rzuca wyjątek gdy brak obiektu w context
    Kontrahent knt = (Kontrahent)cx[typeof(Kontrahent)];
    
    // Bez wyjątku - drugi parametr
    Kontrahent? knt2 = (Kontrahent?)cx[typeof(Kontrahent), false];
}
```

### Przez metodę Get<T> (bezpieczna)

```csharp
public void Action(Context cx)
{
    // Zwraca true jeśli znaleziono, false jeśli nie
    if (cx.Get(out DokumentHandlowy? dokument))
    {
        // dokument znaleziony
    }
    else
    {
        // dokument nie znaleziony (dokument = null)
    }
}
```

## Ustawienie wartości do context

```csharp
public void Action(Context cx)
{
    // Przez indeksator z określeniem typu 
    Kontrahent knt = ...;
    cx[typeof(Kontrahent)] = knt;
    
    // Przez metodę Set
    DokumentHandlowy dok = ...;
    cx.Set(dok);
}
```

## Klasy parametrów (ContextBase)

Klasy parametrów filtrów widoków, parametrów raportów i akcji dziedziczą z `ContextBase` -
są mostem między UI a logiką (właściwości czytane/zapisywane przez context, trwałość
przez `LoadProperty` / `SaveProperty`, powiadamianie UI przez `InvokeChanged`).

Pełna dokumentacja - patrz [contextbase.md](contextbase.md).

## Tworzenie obiektów

Context może tworzyć obiekty worker, extender, i inne obiekty, które mogą być używane w aplikacji.
Do utworzenia obiektu użyj metody `Context.CreateObject<ObjectType>()`.

* Parametry konstruktora są wypełniane automatycznie przez context, jeśli są dostępne w kontekście.
* Property oznaczone przez `[Context]` wypełniane są automatycznie przez context wg typu kontekstu.
* Atrybut `[Context(Required=false)]` oznacza, że property nie jest wymagana do tworzenia obiektu.
* Atrybut `[Context<ParamType>("propertyName")]` lub `[Context(typeof(ParamType), "propertyName")]` pozwala na 
  wypełnienie property z obiektu parametru ze wskazanego property.

### Przykład tworzenia obiektu `Osoba` z parametrów

```csharp
public class OsobaParams(Context context) : ContextBase(context)
{
    [Translate]
    [Accessor(AutoChange = true)]     
    public string Nazwisko { get; set; }
    
    [Translate]
    [Accessor(AutoChange = true)]     
    public string Imie { get; set; }
    
    [Translate]
    public Operator Operator {
        get => Context.GetOrDefault<Operator>(); 
        set => Context.Set(value);
    }
}

// Operator na podstawie wartości Operator z context
public class Osoba(Operator oper) 
{
    // Inicjowane na podstawie Nazwisko z obiektu parametru
    [Context<OsobaParams>(nameof(OsobaParams.Nazwisko))]
    public string Nazwisko { get; set; }

    // Inicjowane na podstawie Imie z obiektu parametru
    [Context<OsobaParams>(nameof(OsobaParams.Imie))]
    public string Imie { get; set; }

    // Jeżeli brak obiektu Kontrahent w context to property jest ignorowane
    [Context(Required=false)]
    public Kontrahent Kontrahent { get; set; }
}

public class OsobaFactory 
{
    public static Osoba Create(Context context)
    {
        return context.CreateObject<Osoba>();
    }
}
```

## Obiekty Worker i Extender

Worker i Extender to dwa mechanizmy rozszerzania modelu o logikę UI - dodatkowe properties wyliczane, akcje w menu Czynności,
pola na formularzu. Oba pobierają parametry z context przez atrybut `[Context]`.

Pełna dokumentacja (rejestracja, deklaracja, bindowanie w form.xml, akcje, przykłady) - 
patrz [worker-extender.md](worker-extender.md).

## Interfejs INavigatorContext

Dostępny w context gdy aktywna jest lista (grid).

```csharp
public void Action(Context cx)
{
    if (cx.Get(out INavigatorContext nav))
    {
        // Wiersz z focusem
        object current = nav.Current;
        
        // Zaznaczone wiersze
        IEnumerable selected = nav.Selected;
        
        // Liczba zaznaczonych
        int count = nav.SelectedCount;
    }
}
```

## Kolekcje zaznaczonych obiektów

W context znajdują się tablice zaznaczonych obiektów.

```csharp
public void Action(Context cx)
{
    // Zaznaczeni kontrahenci
    if (cx.Get(out Kontrahent[] kontrahenci))
    {
        foreach (var k in kontrahenci)
        {
            // ...
        }
    }
    
    // Zaznaczone dokumenty
    if (cx.Get(out DokumentHandlowy[] dokumenty))
    {
        // ...
    }
}
```

## Context implementuje ISessionable

```csharp
public void Action(Context cx)
{
    // Dostęp do sesji przez context
    Session session = cx.Session;
    
    // Dostęp do modułu przez sesję
    var tm = session.GetTowary();
}
```

## Dobre praktyki

1. **Używaj Get<T>, GetOrDefault<T>, GetRequired<T>** zamiast indeksatora - bezpieczniejsze
2. **Sprawdzaj obecność** obiektów w context przed użyciem
3. **Nie mieszaj sesji** - obiekty w kontekście muszą pochodzić z tej samej sesji co kontekst;
   do przeniesienia kontekstu do innej sesji użyj `Clone(session)` / `Normalize(session)`
4. **Pamiętaj o Dispose sesji** utworzonej przez `Login.CreateEmptyContext(...)`
5. **Klasy parametrów (ContextBase)** - trwałość filtrów, `InvokeChanged`, dziedziczenie `Load`/`Save`,
   patrz [contextbase.md](contextbase.md)
6. **Worker / Extender** - rozszerzanie modelu o logikę UI, patrz [worker-extender.md](worker-extender.md)
