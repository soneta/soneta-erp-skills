# ViewInfo — definicja widoku listy (folder)

`ViewInfo` to klasa konfiguracyjna folderu/listy widocznego w drzewku Soneta. Definiuje co i jak jest w nim wyświetlane: który zasób UI (`viewform.xml`) załadować, jaką tabelę i z jakim filtrem otworzyć, jakie parametry filtru udostępnić użytkownikowi oraz jakie reguły dostępu/widoczności zastosować.

ViewInfo to **kod UI** (patrz "Kod biznesowy vs UI" w głównym `SKILL.md`).

Skill `soneta-form-xml` opisuje pełną składnię plików `viewform.xml`/`pageform.xml` (elementy `DataForm`, `Flow`, 
`Grid`, `Field`, `Appearance`, `GroupBy`, atrybuty `EditValue`, `Visibility`, `IsReadOnly`, `Condition` itd.). Sięgaj do niego za każdym razem, gdy edytujesz lub generujesz XML formularza — bez tej wiedzy łatwo wygenerować nieprawidłowe znaczniki.

## Spis treści

- [Rejestracja folderu — atrybut `FolderView`](#rejestracja-folderu--atrybut-folderview)
- [Anatomia klasy ViewInfo](#anatomia-klasy-viewinfo)
- [Eventy — gdzie zaszywać logikę](#eventy--gdzie-zaszywać-logikę)
  - [Kanoniczna para `InitContext` + `CreateView`](#kanoniczna-para-initcontext--createview)
- [Klasa parametrów (`Params` / `WParams` / lub inna nazwa)](#klasa-parametrów-params--wparams--lub-inna-nazwa)
- [Filtrowanie View — paleta narzędzi](#filtrowanie-view--paleta-narzędzi)
  - [Filtrowanie po cechach (Features)](#filtrowanie-po-cechach-features)
- [Powiązanie z `viewform.xml`](#powiązanie-z-viewformxml)
- [Pełny przykład minimalny](#pełny-przykład-minimalny)
- [Pułapki i dobre praktyki](#pułapki-i-dobre-praktyki)

---

## Rejestracja folderu — atrybut `FolderView`

Klasa `ViewInfo` jest podpinana do drzewka folderów przez atrybut assemblowy `[assembly: FolderView(...)]`. To 
jedyne miejsce, gdzie deklarujesz ścieżkę w menu, ikonę i typ ViewInfo:

```csharp
[assembly: FolderView("CRM/Kontrahenci", 
    TableName = "Kontrahent",
    Priority = 10,
    ViewType = typeof(Soneta.CRM.UI.KontrahenciViewInfo),
    Description = "Kartoteka kontrahentów z danymi handlowymi i kontaktowymi.",
    IconName = "dom_osoba")]
```

Najważniejsze parametry:

| Parametr | Znaczenie |
|---|---|
| `path` (pierwszy arg) | Ścieżka w drzewku — segmenty oddzielone `/`. Liście są folderami klikalnymi. |
| `TableName` | Nazwa tabeli ORM, do której domyślnie odnosi się folder. |
| `ViewType` | Typ klasy `ViewInfo` ładowanej dla tego folderu. |
| `Priority` | Kolejność w obrębie rodzica (niższe wyświetlane wcześniej). |
| `GroupIndex` | Grupa wizualna w obrębie poziomu. |
| `IconName` | Nazwa zasobu ikony. |
| `Description` | Tooltip / opis folderu. |

Najważniejsze foldery `path` programu Soneta można znaleźć w skill /soneta-mcp-ui/common-folders.md. 

---

## Anatomia klasy ViewInfo

ViewInfo można używać do definiowania:

* widoku danych w folderach programu
* list na formularzach, gdy lista ma rozbudowaną logikę obsługi i filtrowania

ViewInfo to `System.ComponentModel.Component` — działa we wzorcu inicjalizacji w konstruktorze. Minimalny szkielet:

```csharp
public class FakturyViewInfo : ViewInfo {

    public FakturyViewInfo() {
        ResourceName = "Faktury";
        AllowNewInPlace = false;

        CreateView  += FakturyViewInfo_CreateView;
        InitContext += FakturyViewInfo_InitContext;
    }

    // ... handlery eventów, klasa Params, metody pomocnicze ...
}
```

Właściwości najczęściej ustawiane w konstruktorze:

| Property | Znaczenie                                                                                                                                                                   |
|---|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `ResourceName` | Identyfikator zasobu UI — z niego wyliczana jest nazwa pliku `*.viewform.xml`. Domyślnie używana jest nazwa tabeli z `FolderView`. Można podać kilka zasobów oddzielonych przecinkami (próbowane po kolei). |
| `NewRowTable` | Nazwa tabeli, do której trafiają nowo dodawane wiersze (komenda *Dodaj*).                                                                                                   |
| `NewRows` | Tablica `NewRowAttribute[]` — wiele wariantów nowego wiersza (rozwijane menu *Dodaj*).                                                                                      |
| `AllowNewInPlace` | `true` (domyślnie) — pozwala dodawać wiersze wprost w gridzie, w przypadku użycia ViewInfo na formularzach. Ustaw `false`, gdy wymagane jest otwarcie formularza.           |
| `ReadOnly` | Wymusza `view.AllowNew = view.AllowRemove = false` w `CreateView`. Używane tylko na ViewInfo na formularzach.                                                               |
| `GhostField` | Nazwa "kolumny widmowej" — wykorzysytywane gdy lista zawiera inne obiekty niż otwierane na formularzu z listy.                                                              |
| `OpeningPageName` | Nazwa pliku `*.pageform.xml` otwieranego po kliknięciu wiersza (zastępuje przestarzałe `OpeningPageType`).                                                                  |
| `MakeConfigSession` | `ThreeStateBoolean.Default/Tak/Nie` — czy sesja folderu ma być konfiguracyjna. Używane tylko na ViewInfo na formularzach.                                                                                             |

`ResourceName = "Faktury"` → program szuka pliku `Faktury.viewform.xml` w lokalizacjach zasobów modułu.

---

## Eventy — gdzie zaszywać logikę

Logikę ViewInfo wprowadzasz przez handlery eventów dziedziczonych z `Soneta.Business.ViewInfo`. Podpinaj je w konstruktorze. Najczęściej używane:

| Event | Sygnatura argumentów | Po co                                                                                                                                                                                                                                   |
|---|---|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `CreateView` | `CreateViewEventArgs` (`View`, `DataSource`, `Session`, `Context`) | Budowa głównego `View`: filtry, `Condition`, `AddExpression`, `FilterCondition`, `AllowNew/Update/Remove`.                                                                                                                              |
| `InitContext` | `ContextEventArgs` (`Context`, `FolderView`) | Wstrzyknięcie do kontekstu obiektu `Params`/`WParams` lub innych oraz helperów. Wywoływane **przed** `CreateView`.                                                                                                                      |
| `InitAllRowsContext` | jw. | Wariant "Wszystkie wiersze" — wyzeruj filtry do wartości "Razem"/"Wszystkie". Używany do mechanizmów globalnego wyszukiwania. Wtedy zbudowany View użyty jest przez "Global Search" programu i musi zwrócić wszystkie dostępne obiekty. |
| `UpdateDataSource` | `UpdateDataSourceEventArgs` | Reakcja na zmianę źródła. Rzadko nadpisywane.                                                                                                                                                                                           |
| `Action` | `ActionEventArgs` | Obsługa akcji wywołanej z toolbara/grida "Dodaj", "Edytuj", "Aktualizuj", "Usuń".                                                                                                                                                       |
| `CanDeleteRow` | `CanDeleteRowEventArgs` | Walidacja przed kasowaniem (zwrócenie błędu blokuje delete).                                                                                                                                                                            |
| `SettingFocusedData` | `SettingFocusedDataEventArgs` | Ustalający wiersz w liście odpowiadający obiektowi na formularzu (może być inny).                                                                                                                                                       |

### Kanoniczna para `InitContext` + `CreateView`

```csharp
public FakturyViewInfo() {
    NewRowTable = "Faktura";
    ResourceName = "Faktury";

    InitContext += FakturyViewInfo_InitContext;
    CreateView  += FakturyViewInfo_CreateView;
}

private void FakturyViewInfo_InitContext(object sender, ContextEventArgs args) {
    args.Context.Set(new Params(args.Context));
}

private void FakturyViewInfo_CreateView(object sender, CreateViewEventArgs args) {
    var pars = args.Context.GetRequired<Params>();

    var view = args.Session.GetHandel().Faktury.WgNumeru.CreateView();
    view.AddExpression<Faktura>(f => f.Data >= pars.OdDaty && f.Data <= pars.DoDaty);

    if (pars.Filtr == FiltrFaktur.TylkoNiezatwierdzone)
        view.AddExpression<Faktura>(f => f.Stan == StanDokumentu.Bufor);

    args.DataSource = view;
}
```

W `InitContext` ustawiasz Params **w kontekście** (`args.Context.Set(...)`), żeby:
1. Pola filtru w `viewform.xml` (`EditValue="{FakturyViewInfo+Params.OdDaty}"`) mogły je odczytać.
2. `CreateView` mógł je odczytać przez `args.Context.GetRequired<Params>()`.

---

## Klasa parametrów (`Params` / `WParams` / lub inna nazwa)

`Params` to klasa publiczna (może być zagnieżdżona, ale trzeba uważać na identyfikator) dziedzicząca z `ContextBase` 
(lub z `Params` innego ViewInfo). 
Trzyma stan filtrów widoku — wartości pól z `FilterPanel`. Szczegóły dotyczące `ContextBase`, `InvokeChanged`, 
persistencji i kontekstu — patrz [contextbase.md](contextbase.md).

```csharp
public class Params : ContextBase {

    protected string key = "Handel.Faktury";

    public Params(Context context) : base(context) {
        Load();
    }

    public void Load() {
        filtr = LoadProperty(nameof(Filtr), key, FiltrFaktur.Wszystkie);
        osDaty = LoadProperty(nameof(OdDaty), key, Date.Today.GetFirstOfMonth());
    }

    private FiltrFaktur filtr;
    
    [Caption("Filtr")]
    public FiltrFaktur Filtr {
        get => filtr;
        set {
            filtr = value;
            Session.InvokeChanged();
            SaveProperty(nameof(Filtr), key);
        }
    }

    public object GetListFiltr() => Enum.GetValues(typeof(FiltrFaktur));
    
    private Date odDaty;
    
    [Caption("Od daty")]
    public Date OdDaty {
        get => odDaty;
        set {
            odDaty = value;
            Session.InvokeChanged();
            SaveProperty(nameof(OdDaty), key);
        }
    }

    public bool IsReadOnlyOdDaty() => filtr == FiltrFaktur.Wszystkie;
}
```

Konwencje:
- Każda property z `[Caption]` (lokalizowalna nazwa pola w UI).
- Setter wywołuje `Session.InvokeChanged()` — wymusza refresh View.
- `SaveProperty/LoadProperty` z kluczem zapisuje wartość w konfiguracji loginu (persystencja między sesjami).
- Metody `GetList<X>()` (opcjonalne) zwracają listę dozwolonych wartości (enum, `View`, `LookupInfo.Item`, tablica).
- Metody `IsReadOnly<X>()` / `IsVisible<X>()` (opcjonalne) sterują dostępnością/widocznością pola — bindowane z `viewform.xml`.

**Współdzielenie state przez Context**: jeśli kilka folderów ma operować na tej samej dacie / liście płac, można trzymać wartość w `Context[typeof(Date)]` / `Context[typeof(ListaPlac)]` zamiast w polu klasy — wtedy wszystkie ViewInfo widzące ten sam Context dzielą wartość.

---

## Filtrowanie View — paleta narzędzi

W handlerze `CreateView` masz do dyspozycji kilka mechanizmów. Wybieraj najprostszy, który załatwia sprawę:

```csharp
// 1) AddExpression — najczytelniejszy, LINQ tłumaczony na SQL
view.AddExpression<Faktura>(f => f.Data >= pars.OdDaty && f.Status == StatusDok.Zatwierdzony);

// 2) Condition + FieldCondition — proste porównania
view.Condition &= new FieldCondition.Equal("Magazyn", pars.Magazyn);

// 3) RowCondition.Exists — EXISTS subquery (relacje 1:N filtrowane przez dziecko)
view.Condition &= new RowCondition.Exists(
    "OsobyKontrahenci", "Kontrahent",
    new FieldCondition.Equal("OsobaKontaktowa", pars.Osoba));

// 4) FilterCondition — walidacja per-wiersz (gdy logika nie da się zSQL-ować, np. uprawnienia)
view.FilterCondition += (s, e) => {
    if (e.Row is Faktura f)
        e.Accepted &= f.GetObjectRight() != AccessRights.Denied;
};

// 5) Zablokowanie operacji - lista na formularzu
if (pars.TylkoPodgląd) {
    view.AllowNew = false;
    view.AllowUpdate = false;
    view.AllowRemove = false;
}
```

Reguły wyboru:
- **`AddExpression`** — gdy filtr da się wyrazić jako LINQ na właściwościach kolumn. Preferowany.
- **`Condition &=`** — gdy potrzebujesz dołożyć `RowCondition` bezpośrednio (`Exists`, `Or`, gotowy obiekt) **oraz gdy filtrujesz po cechach (`Features.X`) — LINQ tego nie obsługuje**.
- **`FilterCondition`** — tylko jako ostatnia deska ratunku. Działa po stronie klienta, nie da się przez nią paginować/sortować po SQL-u.

### Filtrowanie po cechach (Features)

Cechy są dynamicznymi polami trzymanymi w osobnej tabeli — **nie są typowanymi properties klasy Row**. Dla cech używaj `FieldCondition` ze string-path `"Features.NazwaCechy"`:

```csharp
// Zwykłe pola Towaru — LINQ (preferowane, walidowane przy kompilacji)
view.AddExpression<Towar>(t => !t.Blokada && t.Typ == TypTowaru.Towar);

// Cechy — FieldCondition ze string-path (jedyna droga)
view.Condition &= new FieldCondition.Equal("Features.GrupaTowaru", "Telewizor");
view.Condition &= new FieldCondition.Equal("Features.Marka", "Samsung");
view.Condition &= new FieldCondition.GreaterEqual("Features.Przekatna", 50);
view.Condition &= new FieldCondition.LessEqual("Features.Przekatna", 75);
```

Path `"Features.NazwaCechy"` działa też w `LikeConditionProvider`, `OrderBy` widoku i bindingach `viewform.xml` (`EditValue="{Features.Marka}"`). Pełen opis cech (typy, składowanie, dostęp programowy) — patrz [features.md](features.md).

Pełen opis `RowCondition` (rodzaje, `Exists`, `Or`/`And`, użycie w `SubTable`) — patrz [rowcondition.md](rowcondition.md).

---

## Powiązanie z `viewform.xml`

`ResourceName` → program wyszuka plik `<ResourceName>.viewform.xml`. Pola formularza bindują się do właściwości 
ViewInfo i jego `Params` przez składnię `{NazwaViewInfo+Params.Nazwa}`:

```xml
<DataForm xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
          xmlns="http://www.enova.pl/schema/form.xsd"
          xsi:schemaLocation="http://www.enova.pl/schema/form.xsd https://www.enova.pl/schema/form.xsd">
  
  <Flow Name="FilterPanel">
    <Field EditValue="{FakturyViewInfo+Params.OdDaty}"
           CaptionHtml="Od daty"
           Width="20"/>
    <Field EditValue="{FakturyViewInfo+Params.Filtr}"
           CaptionHtml="Filtr"
           Width="20"/>
  </Flow>

  <Grid Name="List" OrderBy="Data">
    <Field EditValue="{Numer}" CaptionHtml="Numer" Width="20"/>
    <Field EditValue="{Data}" CaptionHtml="Data" Width="12"/>
    <Field EditValue="{Kontrahent}" CaptionHtml="Kontrahent" Width="35"/>
    <Field EditValue="{WartoscBrutto}" CaptionHtml="Brutto" Width="13" Footer="Sum"/>
  </Grid>
</DataForm>
```

Reguły bindowania:
- `EditValue="{NazwaProperty}"` w Grid — pole z aktualnego wiersza.
- `EditValue="{NazwaViewInfo+Params.NazwaProperty}"` w FilterPanel — właściwość z `Params` (klasa zagnieżdżona).
- `Visibility="{NazwaViewInfo+Params.IsVisibleXxx()}"`, `IsReadOnly="{NazwaViewInfo+Params.IsReadOnlyXxx()}"` — wywołanie metody (z `()`).
- `Appearance` z `Condition="{?[FlagaBlokady] = True}"` — formatowanie warunkowe wiersza.
- `Footer="Sum"` - kolumny liczbowe mogą mieć sumę w stopce.

Pełna gramatyka `viewform.xml` (DataForm, Page, Group, Grid, Field, Stack, Flow, Command, Appearance, GroupBy, Renderable, CaptionHtml, Footer, Class) — używaj skilla **`/soneta-form-xml`**. Bez niego wygenerowany XML łatwo zawiera nieistniejące elementy.

---

## Pełny przykład minimalny

Klasa C#:

```csharp
using System;
using Soneta.Business;
using Soneta.Business.UI;
using Soneta.Handel;
using Soneta.Types;

[assembly: FolderView("Handel/Faktury własne", TableName = "Faktura",
    Priority = 50, GroupIndex = 1, IconName = "dokument_wy",
    ViewType = typeof(Soneta.Handel.UI.FakturyViewInfo))]

namespace Soneta.Handel.UI;

public class FakturyViewInfo : ViewInfo {

    public FakturyViewInfo() {
        NewRowTable = "Faktura";
        ResourceName = "Faktury";
        AllowNewInPlace = false;

        InitContext += FakturyViewInfo_InitContext;
        CreateView  += FakturyViewInfo_CreateView;
    }

    private void FakturyViewInfo_InitContext(object sender, ContextEventArgs args) {
        args.Context.Set(new Params(args.Context));
    }

    private void FakturyViewInfo_CreateView(object sender, CreateViewEventArgs args) {
        var pars = args.Context.GetRequired<Params>();

        var view = args.Session.GetHandel().Faktury.WgNumeru.CreateView();
        view.AddExpression<Faktura>(f => f.Data >= pars.OdDaty && f.Data <= pars.DoDaty);
        if (pars.Filtr == FiltrFaktur.TylkoNiezatwierdzone)
            view.AddExpression<Faktura>(f => f.Stan == StanDokumentu.Bufor);

        args.DataSource = view;
    }

    public enum FiltrFaktur { Wszystkie, TylkoZatwierdzone, TylkoNiezatwierdzone }

    public class Params : ContextBase {
        private const string key = "Handel.Faktury";

        public Params(Context context) : base(context) {
            odDaty = LoadProperty(nameof(OdDaty), key, Date.Today.GetFirstOfMonth());
            doDaty = LoadProperty(nameof(DoDaty), key, Date.Today);
            filtr  = LoadProperty(nameof(Filtr), key, FiltrFaktur.Wszystkie);
        }

        private Date odDaty;
        
        [Caption("Od daty")]
        public Date OdDaty {
            get => odDaty;
            set { odDaty = value; Session.InvokeChanged(); SaveProperty(nameof(OdDaty), key); }
        }

        private Date doDaty;
        
        [Caption("Do daty")]
        public Date DoDaty {
            get => doDaty;
            set { doDaty = value; Session.InvokeChanged(); SaveProperty(nameof(DoDaty), key); }
        }

        private FiltrFaktur filtr;
        
        [Caption("Filtr")]
        public FiltrFaktur Filtr {
            get => filtr;
            set { filtr = value; Session.InvokeChanged(); SaveProperty(nameof(Filtr), key); }
        }
    }
}
```

Towarzyszący `Faktury.viewform.xml`:

```xml
<DataForm xmlns="...">
  <Flow Name="FilterPanel">
    <Field EditValue="{FakturyViewInfo+Params.OdDaty}" CaptionHtml="Od"     Width="15"/>
    <Field EditValue="{FakturyViewInfo+Params.DoDaty}" CaptionHtml="Do"     Width="15"/>
    <Field EditValue="{FakturyViewInfo+Params.Filtr}"  CaptionHtml="Filtr"  Width="25"/>
  </Flow>

  <Grid Name="List" OrderBy="Numer">
    <Field EditValue="{Numer}"        CaptionHtml="Numer"      Width="20"/>
    <Field EditValue="{Data}"         CaptionHtml="Data"       Width="12"/>
    <Field EditValue="{Kontrahent}"   CaptionHtml="Kontrahent" Width="35"/>
    <Field EditValue="{WartoscBrutto}" CaptionHtml="Brutto"    Width="13" Footer="Sum"/>
  </Grid>
</DataForm>
```

---

## Pułapki i dobre praktyki

- **`AllowNew`/`AllowUpdate`/`AllowRemove`** ustawiaj w `CreateView`, nie w konstruktorze — operują na świeżym `View` tworzonym przy każdym wejściu do folderu.
- **`InitContext` musi poprzedzać `CreateView`** w łańcuchu zależności: w `InitContext` wsadzasz `Params` do 
  kontekstu, w `CreateView` je odczytujesz przez `args.Context.GetRequired<Params>()`. Nie próbuj odwrotnej kolejności.
- **`Session.InvokeChanged()` w setterach Params** jest obowiązkowe — bez niego zmiana pola filtru nie odświeży listy.
- **`SaveProperty`/`LoadProperty`** używają klucza per-ViewInfo (`"CRM.Kontrahenci"`, `"Handel.Faktury"`). Trzymaj go w stałej, żeby zmiana nazwy nie skasowała zapisanych preferencji.
- **`static` pola jako pamięć ostatniej wartości** między sesjami — tylko gdy świadomie godzisz się na współdzielenie między równoległymi loginami (`BusApplication` jest multithreaded — patrz "Thread-safety" w głównym `SKILL.md`). Domyślnie preferuj `SaveProperty`/`LoadProperty`.
- **Nazewnictwo**: nazwa klasy `<NazwaTabeli>ViewInfo` (l.mn. dla list — `KontrahenciViewInfo`, `FakturyViewInfo`), 
  namespace zgodny z modułem (`Soneta.Handel.UI`). Identyfikatory domenowe po polsku, systemowe po angielsku — patrz 
  "Konwencje nazewnicze" w głównym `SKILL.md`.
- **`FilterCondition` to ostatnia deska ratunku** — działa po wczytaniu wierszy, ogranicza wydajność. Jeśli się da, przepisz na `AddExpression` lub `RowCondition.Exists`.
- **Kod biznesowy vs UI**: handlery `CreateView`/`InitContext` to UI. Nie wołaj z nich workera biznesowego, jeśli ten ma działać poza interfejsem — wydziel logikę do zwykłej metody operującej na `SubTable`/`Session`.
