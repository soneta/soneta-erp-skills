# Obiekty Worker i Extender

Obiekty `Worker` i `Extender` rozszerzają model danych o dodatkową logikę UI:
properties wyliczane, akcje w menu Czynności, dodatkowe pola na formularzu.
Oba korzystają z [Context](context.md) do pobierania parametrów.

## Obiekty Worker

Worker dorzuca do obiektu danych dodatkowe properties wyliczane (do użycia w bindowaniu) oraz pozycje w menu Czynności.
Worker można też **utworzyć i wywołać ręcznie z kodu** — wystarczy zainstancjonować klasę, ustawić jej pola/properties
i wywoływać metody (patrz [Programowe użycie workera](#programowe-użycie-workera)).

* Przypisuj worker do konkretnego obiektu danych — worker zawsze działa w kontekście jednego typu.
* Dodawaj do nazwy klasy sufiks `Worker` (np. `WyliczenieStanMagazynuWorker`).
* Wybieraj nazwę klasy opisującą działanie, nie technikę.
* Inicjuj parametry z kontekstu przez `[Context]` lub przez konstruktor (jego parametry również pobierane są z `Context`).
* Rejestruj przez generyczny atrybut `[assembly: Worker<WorkerType, DataType>]` — to wersja zalecana.

### Rejestracja worker

```csharp
// Rejestracja zalecana atrybutem generic
[assembly: Worker<NazwaKlasyWorker, DataType>]
```

```csharp
// Niezalecana rejestracja atrybutem z parametrami
[assembly: Worker(typeof(NazwaKlasyWorker), typeof(DataType))]
```

#### Opcjonalny alias `name`

Atrybut `Worker` przyjmuje dodatkowy, opcjonalny parametr `name` — alternatywną nazwę używaną
przy bindowaniu w `form.xml` (`{Workers.<name>.<Property>}`). Standardowo aliasem jest nazwa klasy
workera **bez sufiksu `Worker`** (`WyliczenieStanMagazynuWorker` → `WyliczenieStanMagazynu`).
Parametr `name` ma sens tylko wtedy, gdy chcesz zbindować worker pod inną nazwą niż domyślna —
np. dla zachowania kompatybilności po refaktoringu klasy.

```csharp
[assembly: Worker<NowyWyliczStanuWorker, Towar>("WyliczenieStanMagazynu")]
// W form.xml dalej używamy starego aliasu:
//     EditValue="{Workers.WyliczenieStanMagazynu.StanMagazynu}"
```

### Deklaracja klasy worker

```csharp

[assembly: Worker<WyliczenieStanMagazynuWorker, Towar>]

// Worker wyliczający stan magazynowy
public class WyliczenieStanMagazynuWorker
{
    [Context] 
    public Magazyn Magazyn { get; set; }
    
    [Context] 
    public Towar Towar { get; set; }
    
    public decimal StanMagazynu =>
        Magazyn != null
            ? Towar.GetStan(Magazyn)
            : Towar.GetStanCalkowity();
}
```

Można stosować publiczne metod kontrolujące zachowanie property w edytorze:
* `bool IsVisibleXxx()` - widoczność pola
* `bool IsReadOnlyXxx()` - disable pola
* `object GetListXxx()` - szczegóły edycji

### Bindowanie na UI form.xml (liście)

Bindowanie wg schematu: `{Workers.<NazwaTypuBezSufiksWorker>.NazwaProperty}`
* Za początku zawsze `Workers.`
* Nazwa typu bez sufiksu `Worker` z nazwy klasy worker (tutaj `WyliczenieStanMagazynu`)

```xml
<Grid Name="List">
    <Field CaptionHtml="Kod" Width="17" EditValue="{Kod}" />
    <Field CaptionHtml="Nazwa" Width="30" EditValue="{Nazwa}" />
    <Field CaptionHtml="Stan magazynu" Width="17" EditValue="{Workers.WyliczenieStanMagazynu.StanMagazynu}" />
</Grid>
```

### Worker dodający pozycje do menu Czynności w UI

Worker udostępnia metodę w menu Czynności za pomocą atrybutu `[Action("Tytuł")]`.

* Jeden worker może udostępniać wiele pozycji (metod) w menu Czynności.
* Metoda Action (w przykładzie metoda `SendEmails`) obiektu worker zwraca [action result](./action-result.md)
* Metoda `bool IsVisibleXxx()` (np `bool IsVisibleSendEmails()`) jest opcjonalna i kontroluje widoczność w menu
* Metoda `bool IsEnabledXxx()` (np `bool IsEnabledSendEmails()`) jest opcjonalna i kontroluje aktywność pozycji w menu
* Metoda `string GetNameXxx()` (np `string GetNameSendEmails()`) jest opcjonalna i kontroluje tytuł pozycji w menu
* Metoda `bool IsCheckedXxx()` (np `bool IsCheckedSendEmails()`) jest opcjonalna i kontroluje zaznaczenie pozycji w menu

#### Przykład akcji wykonywanej grupowo na liście kontrahentów

```csharp
[assembly: Worker<SendEmailsForKontrahentWorker, Kontrahent>]

public class SendEmailsForKontrahentWorker
{
    [Context]
    public Kontrahent[] Kontrahenci { get; set; }

    [Action("Wyślij email")]
    public object SendEmails()
    {
        int counter = 0;
        foreach (var k in Kontrahenci)
        {
            if (!k.Email.IsNullOrEmpty())
            {
                WyslijEmail(k.Email);
                ++counter;
            }
        }
        
        return "Wysłano {0} emaili.".TranslateFormat(counter);
    }
    
    public bool IsVisibleSendEmails() => Kontrahenci?.Length>0;
    public bool IsEnabledSendEmails() => Kontrahenci.All(k => k.Email!="");

    private void WyslijEmail(string email) { /* ... */ }
}
```

#### Przykład akcji wykonywanej pojedynczo na towarze

```csharp
[assembly: Worker<KodDuzymiLiteramiWorker, Towar>]

public class KodDuzymiLiteramiWorker
{
    [Context]
    public Towar Towar { get; set; }

    [Action("Kod towaru dużymi literami")]
    public void MakeUpperName()
    {
        Towar.Nazwa = Towar.Nazwa.ToUpper();
    }

    public bool IsVisibleMakeUpperName() => Towar != null;
    public bool IsEnabledMakeUpperName() => !Towar.Nazwa.IsNullOrEmpty();
}
```

#### Przykład akcji otwierającej formularz kontrahenta dla dokumentu

```csharp
[assembly: Worker<PokazKontrahentaDokumentuWorker, DokumentHandlowy>]

public class PokazKontrahentaDokumentuWorker
{
    [Context]
    public DokumentHandlowy Dokument { get; set; }

    [Action("Kontrahent dokumentu")]
    public Kontrahent Pokaz()
    {
        return Dokument.Kontrahent;
    }
    
    public bool IsEnabledPokaz() => Dokument.Kontrahent != null;
    
    public string GetNamePokaz() => "Pokaż kontrahenta: {0}".TranslateFormat(Dokument.Kontrahent?.Nazwa);
}
```

## Obiekty Extender

Pozwalają na bindowanie logiki interface-owej do formularzy. Można bindować methods i properties z obiektu extender.

* Extender nie jest przypisany do danych
* W nazwie klasy powinno się stosować sufiks `Extender`
* Może być inicjowany z context za pomocą `[Context]`
* Rejestracja za pomocą atrybutu assembly z jednym parametrem `[Worker<ExtenderType>]` - zalecana wersja generic

Można stosować publiczne metod kontrolujące zachowanie property w edytorze:
* `bool IsVisibleXxx()` - widoczność pola
* `bool IsReadOnlyXxx()` - disable pola
* `object GetListXxx()` - szczegóły edycji

### Rejestracja extender

```csharp
// Rejestracja zalecana atrybutem generic
[assembly: Worker<NazwaKlasyExtender>]
```

```csharp
// Niezalecana rejestracja atrybutem z parametrami
[assembly: Worker(typeof(NazwaKlasyExtender))]
```

### Deklaracja klasy extender

```csharp
[assembly: Worker<UpperNazwaExtender>]

// Extender pokazujący nazwę dużymi literami
public class UpperNazwaExtender
{
    [Context] 
    public Towar Towar { get; set; }
    
    public string UpperNazwa 
    {
        get => Towar.Nazwa.ToUpper();
        set => Towar.Nazwa = value.ToUpper();
    }
    
    public bool IsReadOnlyUpperNazwa() => string.IsNullOrEmpty(Towar.Nazwa);
    
    public string PokazNazwe() => "Oryginalna nazwa towaru: {0}".TranslateFormat(Towar.Nazwa); 
}
```

### Bindowanie na UI pageform.xml (formularz)

Bindowanie wg schematu: `{new <NazwaTypuZSufixExtender>.NazwaProperty}`
* Za początku zawsze `new `
* Nazwa typu z sufiksem `Extender` z nazwy klasy extender (tutaj `UpperNazwaExtender`)
* Podobnie do property, możemy bindować metody: `{new <NazwaTypuZSufixExtender>.NazwaMetody()}`

```xml
<Page CaptionHtml="Nazwa zakładki">
    <Group CaptionHtml="Identyfikacja towaru">
        <Field CaptionHtml="Kod" Width="17" EditValue="{Kod}" />
        <Field CaptionHtml="Nazwa" Width="30" EditValue="{new UpperNazwaExtender.UpperNazwa}" />
        <Command CaptionHtml="Oryginalna nazwa" DataContext="{new UpperNazwaExtender}" MethodName="PokazNazwe" />
    </Group>
</Page>
```

## Pobieranie parametrów z context - atrybut [Context]

Worker (i extender) może pobierać parametry z context automatycznie.

```csharp
public class MojWorker
{
    [Context]  // Pobierane z context
    public Magazyn Magazyn { get; set; }
    
    [Context]  // Jeśli brak w context - okno parametrów
    public Kontrahent Kontrahent { get; set; }
}
```

## Pełny przykład - Worker z context

```csharp
[assembly: Worker<Soneta.Towary.StanTowaruWorker, Towar>]

namespace Soneta.Towary;

public class TowarExtenderParams(Context context) : ContextBase(context)
{
    [Accessor(AutoChange = true)]
    [Caption("Magazyn filtrowania")]
    public Magazyn MagazynFiltra { get; set; }
}

public class StanTowaruWorker
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

## Konstruktor inicjowany z Context

Jeśli klasa workera (lub extendera) ma **konstruktor publiczny z parametrami**, jego parametry są
inicjowane z `Context` po typie — analogicznie jak property z atrybutem `[Context]`. Pozwala to
trzymać pola jako `readonly` i wymusza komplet zależności w momencie tworzenia obiektu.

```csharp
[assembly: Worker<WyliczenieStanMagazynuWorker, Towar>]

public class WyliczenieStanMagazynuWorker
{
    private readonly Towar towar;
    private readonly Magazyn magazyn;

    // Parametry konstruktora są pobierane z Context (po typie) w momencie tworzenia workera.
    public WyliczenieStanMagazynuWorker(Towar towar, Magazyn magazyn)
    {
        this.towar = towar;
        this.magazyn = magazyn;
    }

    public decimal StanMagazynu =>
        magazyn != null ? towar.GetStan(magazyn) : towar.GetStanCalkowity();
}
```

Reguły:
* Jeśli jest więcej niż jeden konstruktor publiczny, platforma wybiera ten, dla którego potrafi
  rozwiązać komplet parametrów z `Context`.
* Konstruktor i property z `[Context]` można łączyć w jednej klasie.
* Brak wymaganej zależności w `Context` skutkuje błędem / oknem parametrów (analogicznie jak
  brakujące `[Context]`).

## Programowe użycie workera

Workera można utworzyć i wywołać bez pośrednictwa UI — ręcznie z kodu biznesowego. Wystarczy
zainstancjonować klasę, ustawić pola/properties (lub przekazać je przez konstruktor) i wywołać
metody.

```csharp
using (var session = login.CreateSession(readOnly: true, config: false, name: "PoliczStan"))
{
    var towar = session.GetTowary().Towary.WgKodu["NOWY001"];
    var magazyn = session.GetMagazyny().Magazyny.WgKodu["MAG-A"];

    var worker = new WyliczenieStanMagazynuWorker
    {
        Towar = towar,
        Magazyn = magazyn,
    };

    decimal stan = worker.StanMagazynu;
}
```

Kiedy worker wymaga konstruktora — przekaż zależności jako parametry konstruktora zamiast property:

```csharp
var worker = new WyliczenieStanMagazynuWorker(towar, magazyn);
decimal stan = worker.StanMagazynu;
```

Taki sposób użycia jest przydatny w testach jednostkowych, w workerach wywoływanych z innych
workerów oraz w kodzie biznesowym, który chce skorzystać z logiki zamkniętej w workerze bez
przechodzenia przez UI.

## Dobre praktyki

1. **Używaj [Context]** w obiektach worker i extender dla parametrów inicjowanych z context
2. **Dziedzicz z ContextBase** dla własnych klas parametrów (patrz [contextbase.md](contextbase.md))
3. **Metody Action zwracają [action result](./action-result.md)** - nie wywołuj UI bezpośrednio
4. **`CommitUI()` zamiast `Commit()`** - w workerach/extenderach uruchamianych z UI używaj `CommitUI()`
