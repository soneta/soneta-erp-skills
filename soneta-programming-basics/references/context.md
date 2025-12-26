# Klasa Context

Dokumentacja klasy Context odpowiedzialnej za komunikację między warstwą logiki biznesowej a interfejsem graficznym.

## Czym jest Context?

Context to **kontener par klucz-wartość**, gdzie:
- **Klucz** = typ (Type)
- **Wartość** = obiekt tego typu (object)

Kontekst jest stale aktualizowany podczas pracy z programem i przechowuje informacje o aktualnym stanie interfejsu.

## Zawartość kontekstu

Przykładowa zawartość przy otwartej liście kontrahentów:

| Typ | Opis |
|-----|------|
| `SelectedCounter` | Liczba zaznaczonych wierszy na gridzie |
| `Kontrahent[]` | Kolekcja zaznaczonych kontrahentów |
| `UILocation` | Aktywny element interfejsu |
| `INavigatorContext` | Kontekst grida (zaznaczenia, focus) |
| `View` | Źródło danych grida |
| `Params` | Klasa parametrów filtrów |
| `LicencjaProgramu` | Informacje o licencji |
| `Login` | Zalogowany użytkownik |
| `MsSqlDatabase` | Baza danych |

## Odczyt z kontekstu

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

### Przez indeksator (rzuca wyjątek gdy brak)

```csharp
public void Action(Context cx)
{
    // Rzuca wyjątek gdy brak obiektu w kontekście
    Kontrahent knt = (Kontrahent)cx[typeof(Kontrahent)];
    
    // Bez wyjątku - drugi parametr
    Kontrahent knt2 = (Kontrahent)cx[typeof(Kontrahent), false];
}
```

### Przez metodę Get<T> (bezpieczna)

```csharp
public void Action(Context cx)
{
    // Zwraca true jeśli znaleziono, false jeśli nie
    if (cx.Get(out DokumentHandlowy dokument))
    {
        // dokument znaleziony
    }
    else
    {
        // dokument nie znaleziony (dokument = null)
    }
}
```

## Zapis do kontekstu

```csharp
public void Action(Context cx)
{
    // Przez indeksator
    Kontrahent knt = ...;
    cx[typeof(Kontrahent)] = knt;
    
    // Przez metodę Set
    DokumentHandlowy dok = ...;
    cx.Set(dok);
}
```

## Zastosowania

### 1. Filtry na listach głównych

Klasy parametrów filtrów dziedziczą z `ContextBase` i są automatycznie w kontekście.

```csharp
// Definicja klasy parametrów (w ViewInfo)
public class TowaryParams(Context context) : ContextBase(context)
{
    public Magazyn? Magazyn
    {
        get => Context.GetOrDefault<Magazyn>(); 
        set => Context.Set(value);
    }
    
    public TypTowaru Typ {
        get => Context.GetOrDefault<TypTowaru>(); 
        set => Context.Set(value);
    }

    [Accessor(AutoChange = true)]
    public string SearchString { get; set; }
}
```

```xml
<!-- Bindowanie w viewform.xml -->
<Field CaptionHtml="Magazyn" EditValue="{TowaryParams.Magazyn}"/>
<Field CaptionHtml="Typ" EditValue="{TowaryParams.Typ}"/>
<Field CaptionHtml="Szukaj" EditValue="{TowaryParams.SearchString}"/>
```

Wartości filtrów są dostępne przez kontekst dla:
- Widoków (filtrowanie danych)
- Workerów (właściwości wyliczane)
- Wydruków

### 2. Wartości domyślne nowych obiektów

Kontekst używany do inicjalizacji nowych obiektów wartościami z filtrów. Uzupełniane są właściwości zaznaczone atrybutem `[Context]`, który oznacza próbę odczytania wartości do ustawienia property z kontekstu.

```
Lista faktur:
  Filtr Magazyn: "Sklep"
  Filtr Kontrahent: "Drynda"
        ↓
Nowy dokument:
  Magazyn: "Sklep" (z kontekstu - property z [Context])
  Kontrahent: "Drynda" (z kontekstu - property z [Context])
```

### 3. Wydruki

Wydruki mają dostęp do obiektów z kontekstu jako źródła danych.

```csharp
// W kodzie wydruku
Context cx = ...;
if (cx.Get(out Kontrahent[] kontrahenci))
{
    // kontrahenci[] = zaznaczone na liście
}
```

### 4. Workery - atrybut [Context]

Workery mogą pobierać parametry z kontekstu automatycznie.

```csharp
public class MojWorker
{
    [Context]  // Pobierane z kontekstu
    public Magazyn Magazyn { get; set; }
    
    [Context]  // Jeśli brak w kontekście - okno parametrów
    public Kontrahent Kontrahent { get; set; }
}
```

### 5. Właściwości wyliczane zależne od filtrów

```csharp
// Worker wyliczający stan magazynowy
public class StanMagazynu : IWorker
{
    public object Compute(Context cx, object source)
    {
        Towar towar = source as Towar;
        
        // Pobranie magazynu z kontekstu (z filtra)
        if (cx.Get(out Magazyn magazyn))
        {
            return towar.GetStan(magazyn);
        }
        return towar.GetStanCalkowity();
    }
}
```

## Klasa ContextBase

Bazowa klasa dla obiektów automatycznie umieszczanych w kontekście.

```csharp
public class MojaKlasaParametrow(Context context) : ContextBase(context)
{
    [Accessor(AutoChange = true)]
    public Date DataOd { get; set; }

    [Accessor(AutoChange = true)]
    public Date DataDo { get; set; }

    public Kontrahent Kontrahent { 
        get => Context.GetOrDefault<Kontrahent>();
        set => Context.Set(value); 
    }
}
```

Obiekty dziedziczące z `ContextBase` i nie tylko:
- Są automatycznie dodawane do kontekstu
- Wywoływane jest zdarzenie `OnChanged` przy zmianie właściwości
- Obsługują bindowanie do kontrolek UI
- Właściwości połączone z UI (formularze, parametry) mogą być zadeklarowane z `[Accessor(AutoChange = true)]`, dzięki czemu Accessor automatycznie uruchomi mechanizm powiadamiania o zmianach i nie będzie konieczne wywołanie `Session.InvokeChanged()` lub `Context.InvokeChanged()`

## Interfejs INavigatorContext

Dostępny w kontekście gdy aktywna jest lista (grid).

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

W kontekście znajdują się tablice zaznaczonych obiektów.

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
    // Dostęp do sesji przez kontekst
    Session session = cx.Session;
    
    // Dostęp do modułu przez sesję
    var tm = session.GetTowary();
}
```

## Pełny przykład - Worker z kontekstem

```csharp
[assembly: Worker<TowarExtender, Towar>]

namespace Soneta.Towary;

public class TowarExtenderParams(Context context) : ContextBase(context)
{
    [Accessor(AutoChange = true)]
    [Caption("Magazyn filtrowania")]
    public Magazyn MagazynFiltra { get; set; }
}

public class TowarExtender
{
    [Context]
    public TowarExtenderParams Params { get; set; }

    [Context]
    public Towar Towar { get; set; }

    public decimal StanWMagazynie => 
        Params.MagazynFiltra != null
            ? PoliczStanMagazynu(Towar, Params.MagazynFiltra) 
            : PoliczStanMagazynu(Towar);

    private decimal PoliczStanMagazynu(Towar towar, Magazyn magazyn)
    {
        // Wyliczyć stan we wskazanym magazynie
        return 0;
    }

    private decimal PoliczStanMagazynu(Towar towar)
    {
        // Wyliczyć stan w całej firmie
        return 0;
    }
}
```

## Dobre praktyki

1. **Używaj Get<T>, GetOrDefault<T>, GetRequired<T>** zamiast indeksatora - bezpieczniejsze
2. **Sprawdzaj obecność** obiektów w kontekście przed użyciem
3. **Dziedzicz z ContextBase** dla własnych klas parametrów i pamiętaj o konstruktorze `(Context context)`
4. **Używaj [Context]** w workerach dla parametrów z kontekstu
5. **Stosuj `[Accessor(AutoChange = true)]`** lub `InvokeChanged()` dla powiadamiania UI o zmianach

## Typowe typy w kontekście

| Typ | Kiedy dostępny |
|-----|----------------|
| `Login` | Zawsze po zalogowaniu |
| `Database` | Zawsze po zalogowaniu |
| `LicencjaProgramu` | Zawsze po zalogowaniu |
| `Session` | Gdy aktywny widok z danymi |
| `View` | Gdy aktywna lista |
| `INavigatorContext` | Gdy aktywna lista |
| `[Obiekt][]` | Zaznaczenia na liście |
| `[ViewInfo]+Params` | Klasa parametrów widoku |
| `UILocation` | Lokalizacja w UI |
