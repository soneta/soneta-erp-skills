# Klasa Context (kontekst)

Dokumentacja klasy Context odpowiedzialnej za komunikację między warstwą logiki biznesowej a interfejsem graficznym.

## Czym jest Context?

Context to **kontener par klucz-wartość**, gdzie:
- **Klucz** = typ (Type)
- **Wartość** = obiekt tego typu (object)

Context jest stale aktualizowany podczas pracy z programem i przechowuje informacje o aktualnym stanie interfejsu.

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

## Klasa parametrów (np filtrów) - dziedziczy z `ContextBase`

Klasy parametrów filtrów dziedziczą z `ContextBase`

```csharp
// Definicja klasy parametrów (w ViewInfo)
public class TowaryParams(Context context) : ContextBase(context)
{
    [Translate]
    public Magazyn? Magazyn
    {
        get => Context.GetOrDefault<Magazyn>(); 
        set => Context.Set(value);
    }
    
    [Translate]
    public TypTowaru Typ {
        get => Context.GetOrDefault<TypTowaru>(); 
        set => Context.Set(value);
    }

    [Accessor(AutoChange = true)]
    [Caption("Szukaj")]
    public string SearchString { get; set; }
}
```

* Każde property klasy parametrów dziedziczy z `ContextBase` wymaga kontroli tłumaczenia za pomocą jednego z 
  atrybutu: `[Translate]`, `[TranslateIgnore]`, `[Caption("Tytuł")]`
* Bindowanie we viewform.xml wewnątrz `FilterPanel` nie wymaga użycia `Context.` (np `TowaryParams`), ponieważ 
  `Context` jest dostępne bezpośrednio wewnątrz `FilterPanel`
* Bindowanie w pageform.xml wymaga użycia `Context.` (np `Context.TowaryParams`)
* Gdy property nie używa context -> stosuj `[Accessor(AutoChange = true)]` - zamiennie w kodzie set property można 
  również użyć `Session.InvokeChanged()` lub `Context.InvokeChanged()`

```xml
<!-- Bindowanie w viewform.xml -->
<Flow Name="FilterPanel">
    <Field CaptionHtml="Magazyn" EditValue="{TowaryParams.Magazyn}"/>
    <Field CaptionHtml="Typ" EditValue="{TowaryParams.Typ}"/>
    <Field CaptionHtml="Szukaj" EditValue="{TowaryParams.SearchString}"/>
</Flow>
```

```xml
<!-- Bindowanie w pageform.xml -->
<Page>
    <Group CaptionHtml="Parametry">
        <Field CaptionHtml="Magazyn" EditValue="{Context.TowaryParams.Magazyn}"/>
        <Field CaptionHtml="Typ" EditValue="{Context.TowaryParams.Typ}"/>
        <Field CaptionHtml="Szukaj" EditValue="{Context.TowaryParams.SearchString}"/>
    </Group>
</Page>
```

Wartości filtrów są dostępne przez context do:
- Widoków (filtrowanie danych)
- Obiektów worker (właściwości wyliczane, akcje)
- Obiektów extender (właściwości wyliczane)
- Tworzenia obiektów

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
3. **Dziedzicz z ContextBase** dla własnych klas parametrów i pamiętaj o konstruktorze `(Context context)`
4. **Stosuj `[Accessor(AutoChange = true)]`** lub `InvokeChanged()` dla powiadamiania UI o zmianach jeżeli wartość 
   property nie jest przechowywana w context
5. **Używaj `[Translate]`, `[TranslateIgnore]`, `[Caption("Tytuł")]`** dla property klas parametrów (ContextBase)
6. **Worker / Extender** - rozszerzanie modelu o logikę UI, patrz [worker-extender.md](worker-extender.md)
