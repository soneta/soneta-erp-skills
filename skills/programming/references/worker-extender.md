# Obiekty Worker i Extender

Obiekty `Worker` i `Extender` rozszerzają model danych o dodatkową logikę UI:
properties wyliczane, akcje w menu Czynności, dodatkowe pola na formularzu.
Oba korzystają z [Context](context.md) do pobierania parametrów.

> Składnię wyrażeń bindujących po stronie form.xml (`{Workers.Alias.Pole}`, `{new Extender.Pole}`)
> oraz element `Command` wywołujący akcję opisuje skill `/soneta:form-xml`.

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

### Worker rozszerzający obiekt obcy (spoza dodatku)

To **kanoniczny sposób dołożenia property lub kolekcji do obiektu biznesowego platformy Soneta,
którego kodu nie da się zmodyfikować** (np. `Kontrahent`, `Towar`, `DokumentHandlowy`). Zamiast
edytować obcą klasę, rejestrujesz worker na jej typie i przez `[Context]` sięgasz do własnych
tabel z dodatku — property/kolekcje workera stają się dostępne w bindowaniu form.xml oraz w kodzie.

```csharp
[assembly: Worker<KontrahentOcenaWorker, Kontrahent>]

public class KontrahentOcenaWorker
{
    // Obiekt obcy, który "rozszerzamy" — pobierany z kontekstu.
    [Context]
    public Kontrahent Kontrahent { get; set; }

    // Kolekcja z dodatku, powiązana z obcym obiektem przez klucz (SubTable filtrowana kluczem).
    public SubTable<OcenaKontrahenta> Oceny =>
        Kontrahent.Session.GetOcenaKontrahenta().OcenyKontrah.WgKontrahent[Kontrahent];

    [Caption("Liczba ocen")]
    public int LiczbaOcen => Oceny.Count;
}
```

* **Property i kolekcje dodatku wystawiasz z workera**, a nie z obcej klasy — dodatek nie modyfikuje
  modułu bazowego i pozostaje odinstalowywalny.
* Powiązanie realizuj **kluczem** (`WgKontrahent[Kontrahent]`) po polu wskazującym obcy obiekt w Twojej
  tabeli — pełne filtrowanie serwerowe opisuje [rowcondition.md](rowcondition.md).
* Sesję bierz z obiektu obcego (`Kontrahent.Session`) — nie trzymaj własnej referencji do sesji.
* W bindowaniu form.xml odwołujesz się jak do każdego workera: `{Workers.KontrahentOcena.LiczbaOcen}`.

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
* Akcja workera pojawia się **w dwóch miejscach naraz**: w menu Czynności listy/formularza oraz
  jako `Command` możliwy do umieszczenia na formularzu (element `Command` — skill `/soneta:form-xml`).
* Metoda Action (w przykładzie metoda `SendEmails`) obiektu worker zwraca [action result](./action-result.md)
* Metoda `bool IsVisibleXxx()` (np `bool IsVisibleSendEmails()`) jest opcjonalna i kontroluje widoczność w menu
* Metoda `bool IsEnabledXxx()` (np `bool IsEnabledSendEmails()`) jest opcjonalna i kontroluje aktywność pozycji w menu
* Metoda `string GetNameXxx()` (np `string GetNameSendEmails()`) jest opcjonalna i kontroluje tytuł pozycji w menu
* Metoda `bool IsCheckedXxx()` (np `bool IsCheckedSendEmails()`) jest opcjonalna i kontroluje zaznaczenie pozycji w menu
* Warunki zależne od listy/formularza, zakładki, liczby zaznaczonych wierszy i serwisów DI —
  [Warunki czynności](#warunki-czynności--isvisible--isenabled). **W kliencie web `IsEnabledXxx() == false`
  ukrywa czynność** tak samo jak `IsVisibleXxx()`.

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

### Czynności dynamiczne — `GetActions`

Obok statycznych czynności `[Action("Tytuł")]` istnieje konwencja czynności **dynamicznych** —
gdy lista pozycji menu zależy od danych (np. jedna pozycja na każdą definicję z konfiguracji).
Na klasie workera deklaruje się publiczną **statyczną** metodę `GetActions`, wywoływaną przy
budowie menu:

```csharp
[assembly: Worker(typeof(DefinicjeWorker), typeof(GuidedRow))]

public class DefinicjeWorker
{
    public static IEnumerable GetActions(Session session, Context context)
    {
        // Zawężenie do właściwego kontekstu — rejestracja na GuidedRow oferuje
        // czynność na wszystkich obiektach głównych.
        if (context[typeof(GuidedRow), false] is not GuidedRow row)
            yield break;

        foreach (Definicja definicja in session.GetMojModul().Definicje) {
            // Prawo przed kolumnami — patrz rights-source.md.
            if (definicja.AccessRight == AccessRights.Denied)
                continue;
            yield return new DefinicjaAction(definicja);
        }
    }
}
```

`GetActions` zwraca instancje własnej klasy dziedziczącej po `Soneta.Business.Action`
z nadpisaniami:

- `Name` — pełna ścieżka menu rozdzielana `/` (np. `"/Czynności/Grupa/" + nazwa`; człony
  tłumaczone, np. `TranslatePath()`),
- `Mode`, `Target` — np. `ActionMode.SingleSession`, `ActionTarget.Menu`,
- `object Invoke(object instance, Context context)` — wykonanie; zwrócony obiekt przechodzi
  standardową obsługę rezultatów (patrz [action-result.md](./action-result.md)).

Zasady:

* Rejestracja jak zwykłego workera; rejestracja na `GuidedRow` oferuje czynność na wszystkich
  obiektach głównych — zawężenie robi się warunkami w `GetActions` (jak w przykładzie).
* `GetActions` uczestniczy w budowie menu **każdego okna** — musi być tanie i odporne; niezłapany
  wyjątek objawia się dialogiem błędu w całej aplikacji.
* Przy definicjach będących źródłami praw sprawdzaj `AccessRight` **przed** odczytem kolumn
  wiersza — szczegóły i pułapki: [rights-source.md](./rights-source.md).
* Skaner [scan-workers.md](./scan-workers.md) **nie wykrywa** czynności dynamicznych — inwentaryzuje
  tylko metody z atrybutem `[Action]`.

### Gdzie akcja się pojawia — formularz czy lista

O miejscu prezentacji akcji (statycznej i dynamicznej) decydują flagi `ActionMode` i `ActionTarget`:

| Flaga | Znaczenie |
|---|---|
| `ActionMode.OnlyForm` | tylko na formularzu obiektu |
| `ActionMode.OnlyTable` | tylko na liście |
| `ActionMode.OnlyListOnForm` | tylko na liście osadzonej w formularzu |
| brak flag `Only*` | wszędzie, gdzie worker pasuje typem danych |
| `ActionMode.OnlyWebForms` / `OnlyWinForms` | tylko w kliencie web / tylko w wersji okienkowej; łączy się z innymi flagami (`SingleSession \| OnlyWebForms`), działa też dla czynności z `GetActions` (nadpisany `Mode`). Widoczność zależna od czegoś więcej (licencja, prawa, typ urządzenia) → `IsVisible…` |
| `ActionTarget.Menu` + `LocalMenu` | pozycja w menu „Czynności” i w menu kontekstowym wiersza |
| `ActionTarget.ToolbarWithText` | osobny przycisk z podpisem w pasku narzędzi formularza lub listy; w pasku **listy** renderuje się tylko przy `ActionMode.SingleSession` lub `IsolatedSession` |

Rozpoznanie „formularz czy lista” w `GetActions` opiera się na zawartości kontekstu
(szczegóły w [context.md](./context.md#zawartość-context)):

- **formularz** wstawia `CurrentObject` (obiekt formularza) oraz sam wiersz pod jego typem;
- **lista** nie ma `CurrentObject`; ma wiersz bieżący pod jego konkretnym typem, zaznaczenie jako
  **tablicę typowaną** (np. `Kontrahent[]`, odczytywalną też jako `GuidedRow[]` / `Row[]`) i `View`.

```csharp
public static IEnumerable GetActions(Session session, Context context)
{
    var current = context.GetOrDefault<CurrentObject>()?.Value as GuidedRow;
    var selected = context.GetOrDefault<GuidedRow[]>();
    bool isList = current == null && selected?.Length > 0;
    var row = current ?? selected?[0];
    if (row == null) yield break;

    // Czynność „tylko dla jednego zapisu”: przy zaznaczeniu wielu wierszy nie oferuj jej.
    if (isList && selected!.Length != 1) yield break;

    yield return new MojaAkcja(row) {
        IsList = isList   // → Mode: OnlyTable zamiast OnlyForm, Target: Menu | LocalMenu
    };
}
```

Zasady wykonania na liście:

* Uruchomienie z menu zaznaczenia jest **for-each** — silnik iteruje zaznaczone wiersze i przed
  każdym `Invoke` wstawia bieżący wiersz do kontekstu (`context.Set(row)`). Akcja, która ma działać
  raz dla całego zaznaczenia, przyjmuje tablicę (przykład „grupowo na liście kontrahentów” wyżej);
  akcja „tylko dla jednego zapisu” ukrywa się w `GetActions`, gdy tablica ma więcej niż jeden element.
* Parametry `GetActions` są wiązane najpierw z **bieżącej wartości** przekazanej przez UI (wiersz,
  tablica zaznaczenia lub `View` — gdy typ parametru jest z niej przypisywalny), potem z `Context`.
  Brak dopasowania oznacza brak akcji, bez wyjątku — przy nietypowych sygnaturach sprawdź menu
  na obu rodzajach okien.

### Źródła menu Czynności i paska narzędzi

**Lista (folder):**

- Menu Czynności ma dwa źródła: **tabelę** (lista/widok) i **wiersz** z fokusem. Bez fokusu (np. pusta
  lista po usunięciu ostatniego wiersza) akcje workerów zarejestrowanych dla typu wiersza nie są
  pokazywane, a ich `GetActions` nie jest wołane. Workery tabeli są wołane zawsze.
- Wniosek: `GetActions(Context, TWiersz row)` workera wiersza dostaje w menu listy `row != null`.
  Czynność, która ma działać także na pustej liście, rejestruj dla tabeli.
- Pasek listy zawiera akcje tabeli; akcje wiersza trafiają na niego tylko w trybie
  `ActionMode.IsolatedSession` / `SingleSession`.

**Formularz (klient web):**

- Menu Czynności zbiera akcje obiektu formularza oraz bieżącego wiersza każdego grida na aktywnej
  zakładce (gdy grid nie ma `ActionsMode="Form"` — `/soneta:form-xml`). Akcje wierszy tworzą osobną grupę.
- Na pasek narzędzi (`ActionTarget.Toolbar` / `ToolbarWithText`) i do skrótów klawiszowych trafiają
  tylko akcje obiektu formularza. Wyjątek: czynność dodana przez użytkownika do ulubionych.
- Pułapka: worker zarejestrowany dla typu obiektu i dla typu wierszy jego grida (np. dokument
  i pozycje będące dokumentami) daje w menu dwie pozycje o tej samej nazwie. Ogranicz widoczność
  w `IsVisible…` (dane kontekstu = obiekt formularza) albo ustaw na gridzie `ActionsMode="Form"`.

**Weryfikacja:** na liście — fokus po usunięciu wiersza (sąsiedni wiersz albo brak) i zawartość
menu (w danych listy pomijaj wiersz sumy, identyfikator zaczyna się od `$`); na formularzu z gridem —
przycisk akcji raz na pasku, w menu w oczekiwanych grupach.

### Warunki czynności — `IsVisible…` / `IsEnabled…`

**Najważniejsze:**

- **Web: niedostępna = niewidoczna.** Czynność z `IsEnabled…` = `false` znika z menu Czynności
  i z paska (`ActionTarget.Toolbar`/`ToolbarWithText`) — bez „wyszarzonego" przycisku. Para
  wykluczających się czynności pokazuje zawsze tylko jedną; po wykonaniu pasek przerysowuje się
  i pokazuje drugą. Czynność ma być widoczna, ale nieaktywna? W web niemożliwe — przenieś warunek
  do komunikatu w metodzie czynności.
- **Przeliczanie po każdej zmianie.** Menu i pasek przeliczają się przy budowie każdej odpowiedzi
  okna: po zmianie w sesji, zmianie zakładki, zmianie wiersza bieżącego **i** liczby zaznaczonych
  (także bez zmiany wiersza bieżącego). Warunek ma być tani (bez SQL w pętli) i bez skutków ubocznych.
- **Prawo przed kolumnami.** Predykat czytający kolumny wiersza, który może być niedostępny (źródło
  praw lub wiersz dziedziczący prawo), najpierw sprawdza `row.AccessRight != AccessRights.Denied` —
  inaczej `AccessDeniedException` przerywa budowanie menu zamiast ukryć pozycję
  ([rights-source.md](./rights-source.md)).

```csharp
public static bool IsEnabledZatwierdz(MojDokument dok) => dok.Bufor;
public static bool IsEnabledOdtwierdz(MojDokument dok) => !dok.Bufor;
```

**Lista czy formularz w statycznej `[Action]`.** Property
`[Context(Required = false)] public Kontrahent[] Zaznaczeni { get; set; }` dostaje tablicę zaznaczenia
tylko na liście (na formularzu zostaje `null`); `Required = false` zapobiega otwarciu okna
parametrów, gdy typu brak w kontekście. Warunek czynności „tylko dla jednego zapisu":
`IsVisibleXxx() => Zaznaczeni is null || Zaznaczeni.Length <= 1`.

**Liczba zaznaczonych na liście.** Zaznaczenie jest w kontekście jako tablica typu wiersza lub
interfejsu, który typ implementuje ([context.md](context.md#kolekcje-zaznaczonych-obiektów)):

```csharp
public static bool IsVisibleWyslij(Context cx) =>
    !(cx.Get(out IMojHost[] zaznaczone) && zaznaczone.Length > 1);

public static bool IsVisibleWyslijZbiorczo(Context cx) =>
    cx.Get(out IMojHost[] zaznaczone) && zaznaczone.Length > 1;
```

**Czynność zależna od zakładki formularza** — `UILocation.PageXmlName`:

```csharp
public bool IsVisibleMojaCzynnosc(Context cx) =>
    cx.Get(out UILocation uil) && uil.PageXmlName == "MojaZakladka";
```

- `PageXmlName` = część nazwy pliku po typie (`Typ.MojaZakladka.pageform.xml` → `MojaZakladka`),
  nie `Name` elementu `Page`.
- Formularz otwiera się na **pierwszej widocznej** zakładce — czynność innej zakładki pojawia się
  (pasek i menu) dopiero po przejściu na nią, także w testach.

**Parametr serwisu.** Metoda warunku może przyjmować obok `Context` interfejs serwisu — platforma
rozwiązuje go z kontenera DI sesji, tak jak parametry metody czynności ([services.md](services.md)):

```csharp
public static bool IsVisibleMojaCzynnosc(Context cx, IMojSerwis serwis) => serwis.CzyDostepne(cx);
```

Nie trzymaj referencji do serwisu poza wywołaniem — zapis z zamknięciem jednego okna zwalnia
sesję i jej kontener DI, a inne okno tej sesji może jeszcze budować odpowiedź.

Checklista:
- [ ] Nie projektuj UI ani testu oczekującego obu przycisków pary wykluczających się czynności.
- [ ] Warunek z `Get(out T[])` obsługuje jawnie brak tablicy (formularz).
- [ ] Nazwa w warunku zakładki = nazwa z pliku `pageform.xml`; scenariusz testu przechodzi na zakładkę przed sprawdzeniem czynności.
- [ ] Parametr serwisu to interfejs zarejestrowany w DI (inaczej nie zostanie rozwiązany).
- [ ] Warunek czysty i szybki — zależy tylko od wiersza/kontekstu.
- [ ] Scenariusz listy: zaznacz 1 → sprawdź; dołóż drugi bez zmiany wiersza bieżącego → sprawdź.

## Obiekty Extender

Pozwalają na bindowanie logiki interface-owej do formularzy. Można bindować methods i properties z obiektu extender.
Extender bywa też **kontekstem całej strony** (`DataContext="{New MojExtender}"`) — m.in. stron
okna Opcji (`Config.*.pageform.xml`), gdzie dostarcza widoki list konfiguracyjnych; składnię
opisuje `/soneta:form-xml` (binding, *Strony okna Opcji*).

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

## Częste pułapki (namespace'y i typy)

* **`[Caption]` jest w `Soneta.Types`** — wymaga `using Soneta.Types;`, nie `Soneta.Business`.
* **Daty: `Soneta.Types.Date`** (`Date.Today`), nie `DateTime.Now`/`DateTime.Today` — poza
  szczególnymi potrzebami (znaczniki czasu z godziną).
* **Bieżący zalogowany operator: `session.AuthorizationInfo.Operator`** (typ
  `Soneta.Business.App.Operator`), **NIE** `Login.Operator`. Nadaje się np. do ustawienia relacji
  „autor" na tworzonym obiekcie. Szczegóły: [session-login.md](session-login.md#dostęp-do-informacji-o-operatorze).

## Dobre praktyki

1. **Używaj [Context]** w obiektach worker i extender dla parametrów inicjowanych z context
2. **Dziedzicz z ContextBase** dla własnych klas parametrów (patrz [contextbase.md](contextbase.md))
3. **Metody Action zwracają [action result](./action-result.md)** - nie wywołuj UI bezpośrednio;
   akcja zwracająca `Row` otwiera jego formularz (wzorzec „szkicu" — [action-result.md](action-result.md#zwrócenie-row-lub-dowolnego-object))
4. **`CommitUI()` zamiast `Commit()`** - w workerach/extenderach uruchamianych z UI używaj `CommitUI()`
