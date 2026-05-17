# Klasa ContextBase - klasy parametrów

Dokumentacja klas parametrów (filtrów widoków, parametrów raportów, parametrów akcji)
dziedziczących z `ContextBase`. Klasy te są **mostem między UI a logiką** - przechowują
wartości wybrane przez użytkownika i udostępniają je do widoków, workerów, extenderów
oraz przy tworzeniu obiektów.

Pełna dokumentacja klasy `Context` (kontener kluczy/wartości) - patrz [context.md](context.md).

## Definicja klasy parametrów

Klasy parametrów filtrów dziedziczą z `ContextBase` i mają konstruktor przyjmujący `Context`:

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

## Trwałość wartości - LoadProperty / SaveProperty

Klasa `Params` jest tworzona od nowa przy każdym otwarciu widoku (każde wywołanie `InitContext`).
Aby wartości filtrów (okres, kategoria, zaznaczone opcje) **przeżyły między otwarciami**, zapisuje się
je do persystentnego storage operatora przez metody `LoadProperty` / `SaveProperty` klasy `ContextBase`.

### Sygnatury

```csharp
public void SaveProperty(string propertyName, string category = null);

public object LoadProperty(string propertyName, string category = null);

public T LoadProperty<T>(string propertyName, string category = null, T def = default);

// Wariant z out i wartością domyślną - dla typów wartościowych
public bool LoadProperty<T>(string propertyName, out T result,
                            string category = null, T def = default) where T : struct;
```

- `propertyName` - nazwa property w obecnej klasie (najczęściej `nameof(X)`); silnik czyta wartość przez
  refleksję przy zapisie i ustawia ją przy odczycie.
- `category` - klucz przestrzeni nazw w storage, zwykle stała typu `"CRM.Kontrahenci"`,
  `"Płace.ListyPłac"`. Pozwala oddzielić ustawienia tego samego property w różnych widokach.

### Wzorzec konstruktora i pól

Każde "trwałe" property ma trzy elementy: prywatne pole, property z setterem zapisującym
oraz wywołanie `LoadProperty` w `Load()`:

```csharp
public class Params : ContextBase
{
    static readonly string key = "Modul.Widok";

    public Params(Context context) : base(context)
    {
        Load();
    }

    FiltrKontrahentów filtr;
    [Caption("Kontrahenci")]
    public FiltrKontrahentów Filtr
    {
        get => filtr;
        set
        {
            filtr = value;
            Context.InvokeChanged();             // powiadom UI
            SaveProperty(nameof(Filtr), key);    // zapisz do storage
        }
    }

    private void Load()
    {
        // wariant z rzutowaniem object -> enum
        var v = LoadProperty(nameof(Filtr), key, FiltrKontrahentów.Aktywni);
        filtr = v == null ? FiltrKontrahentów.Aktywni : (FiltrKontrahentów)v;

        // wariant generyczny z wartością domyślną - preferowany
        status = LoadProperty(nameof(Status), key, StatusPodmiotuFiltr.Wszystkie);
    }
}
```

Setter property powinien:
1. zapisać wartość do pola,
2. powiadomić UI o zmianie (`Context.InvokeChanged()` lub `Session.InvokeChanged()`),
3. zapisać wartość przez `SaveProperty(nameof(X), key)`.

### Wartości przechowywane w Context (nie w polu)

Gdy wartość property żyje w `Context` (np. `KontaktOsoba`, `OddzialFirmy`, `Date`,
`DefinicjaListyPlac`), używa się helpera `SetContext(Type, object)` do odczytania zapisanej wartości
i wstawienia jej z powrotem do context:

```csharp
public KontaktOsoba Osoba
{
    get => Context.GetOrDefault<KontaktOsoba>();
    set
    {
        Context.Set(value);
        SaveProperty(nameof(Osoba), key);
    }
}

private void Load()
{
    SetContext(typeof(KontaktOsoba), LoadProperty(nameof(Osoba), key));
    SetContext(typeof(OddzialFirmy),  LoadProperty(nameof(OddzialFirmy), key));
}
```

`SetContext` ustawia wartość do context tylko jeżeli jest różna od `null` lub gdy klucz jeszcze nie
istnieje - dzięki czemu nie nadpisuje wartości, którą wcześniej ustawił inny widok.

### Dziedziczenie - virtualne Load / Save

W rozbudowanej hierarchii (np. `WyplatyViewInfo.Params` -> `ListyPlacViewInfo.Params` -> `WdzParams`)
warto przeciążać `Load()` i `Save()` jako `virtual` / `override` i wywoływać `base`. Każda warstwa
zapisuje wyłącznie swoje properties pod własnym kluczem:

```csharp
public class Params : ListyPlacViewInfo.Params
{
    static readonly string key = "Płace.Wypłaty";

    public Params(Context context) : base(context) { }

    protected override void Load()
    {
        base.Load();
        SetContext(typeof(ListaPlac),  LoadProperty(nameof(ListaPlac), key));
        SetContext(typeof(Pracownik),  LoadProperty(nameof(Pracownik), key));
    }

    protected override void Save()
    {
        base.Save();
        SaveProperty(nameof(ListaPlac), key);
        SaveProperty(nameof(Pracownik), key);
    }
}
```

W tym wzorcu settery wywołują `Save()` zamiast pojedynczego `SaveProperty(...)` - dzięki temu
jeden zestaw zmian zapisuje całą warstwę naraz, a w klasach pochodnych nie trzeba duplikować
zapisów rodzica.

### Co warto zapisywać

- filtry tekstowe i enum-y wybierane przez użytkownika,
- okresy / daty filtrowania,
- aktualnie wybrane słowniki (kategorie, branże, role, listy płac, oddziały),
- ustawienia widoczności / trybu (np. `ObowiazywanieZgody`).

### Czego NIE zapisywać

- pól wyliczanych (`IsReadOnly...`, `IsVisible...`),
- wartości pochodzących z konfiguracji programu (czyta się je przy każdym `Load`),
- ulotnych pól typu `string Szukaj` (opcjonalnie - decyzja projektowa; w `KontrahenciViewInfo`
  jest zapisywane, w `WParams` z `KSeFiewInfo` nie).

## Powiadamianie UI o zmianach - `InvokeChanged` (nowy wzorzec)

Wcześniejszy kod używał `OnChanged(EventArgs.Empty)` z poziomu `ContextBase`:

```csharp
// PRZESTARZAŁE
set
{
    filtr = value;
    OnChanged(EventArgs.Empty);
    SaveProperty(nameof(Filtr), key);
}
```

**Zalecane** są nowsze formy:

```csharp
// preferowane - z Context
set
{
    filtr = value;
    Context.InvokeChanged();
    SaveProperty(nameof(Filtr), key);
}

// alternatywnie - z poziomu Session
set
{
    filtr = value;
    Session.InvokeChanged();
    SaveProperty(nameof(Filtr), key);
}
```

Dla properties **bez powiązania z polem trwałym** (czyli pole prywatne wewnątrz klasy, bez wpływu na
serializację) wystarczy atrybut `[Accessor(AutoChange = true)]` - silnik sam wywoła powiadomienie po
ustawieniu wartości:

```csharp
[Accessor(AutoChange = true)]
[Caption("Szukaj")]
public string SearchString { get; set; }
```

## Dobre praktyki

1. **Dziedzicz z ContextBase** dla własnych klas parametrów i pamiętaj o konstruktorze `(Context context)`.
2. **Stosuj `[Accessor(AutoChange = true)]`** lub `Context.InvokeChanged()` / `Session.InvokeChanged()`
   do powiadamiania UI. **Nie używaj** już `OnChanged(EventArgs.Empty)` - jest przestarzałe.
3. **Używaj `[Translate]`, `[TranslateIgnore]`, `[Caption("Tytuł")]`** dla property klas parametrów (ContextBase).
4. **Trwałość filtrów**: każdy zapisywany property w setterze woła `SaveProperty(nameof(X), key)`,
   a `Load()` (wywołane z konstruktora) odczytuje wartość przez `LoadProperty`. Dla wartości
   przechowywanych w context używaj `SetContext(typeof(T), LoadProperty(...))`.
5. **Stała `key`** - jedna na klasę, najczęściej `static readonly string key = "Modul.Widok"`,
   identyfikuje przestrzeń w storage operatora.
6. **W hierarchii** rób `protected override void Load()` / `Save()` i wołaj `base` - każda warstwa
   zapisuje własne properties pod własnym `key`.