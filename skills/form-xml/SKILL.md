---
name: form-xml
description: "Specjalistyczna wiedza o WŁASNOŚCIOWYM formacie plików form.xml platformy Soneta (enova365) — bez tego skilla Claude generuje błędne XML z nieistniejącymi elementami. Dotyczy dodatków partnerów i kodu samej platformy (repozytorium źródłowe Soneta, moduły standardowe). ZAWSZE używaj tego skilla gdy użytkownik: (1) prosi o utworzenie lub modyfikację pliku pageform.xml, viewform.xml, form.xml, lookupform.xml lub gridform.xml dla platformy Soneta (enova365); (2) pyta o elementy DataForm, Page, Group, Grid, Field, Row, Stack, Flow, Command, Include, Appearance, GroupBy w Soneta; (3) pyta o składnię EditValue, DataContext, Visibility, RowCondition, Renderable, CaptionHtml, Footer, Class lub układ UI formularzy Soneta; (4) pokazuje istniejący plik form.xml/pageform.xml/viewform.xml i pyta o jego strukturę lub chce go rozszerzyć; (5) pyta o warunkową widoczność, formatowanie warunkowe (Appearance), bindowanie danych lub wzorce UI w Soneta/enova365."
---

# Soneta Form XML - Formularze UI

## Mapa skilla

Ten plik zawiera **rdzeń składni**: strukturę dokumentu, kontenery, `Field`, `Class`, `Appearance`.
Tematy rozwinięte są w `references/`:

| Zagadnienie | Dokument |
|---|---|
| Listy i kolekcje — `Grid`, kolumny, `ViewInfo`, multi-select, pasek filtra `DataBar` | [references/collections-grids.md](references/collections-grids.md) |
| Bindowanie — powiązanie pliku z typem, zakładki wielookienne, `DataContext`, składnia `{...}`, RowCondition | [references/binding.md](references/binding.md) |
| Kompletne przykłady pageform/viewform/form | [references/examples.md](references/examples.md) |
| Projektowanie okien (UX), pozycja etykiet, weryfikacja wizualna zrzutem ekranu | [references/ux-design.md](references/ux-design.md) |
| Formularze dynamiczne — `{Kolekcja[i].Pole}`, `<Template RenderMethodName>` | [references/dynamic-forms.md](references/dynamic-forms.md) |
| Pełna specyfikacja elementów i wartości `Class` | [references/ELEMENTS.md](references/ELEMENTS.md) |
| Schemat XSD | [references/Form.xsd](references/Form.xsd) |

## Lokalizacja plików — biblioteka `.UI`

Wszystkie definicje interfejsu użytkownika (`pageform.xml`, `viewform.xml`, `gridform.xml`,
`lookupform.xml`, `form.xml`, a także ViewInfo i extendery UI) umieszcza się w **bibliotece UI**
odpowiadającej modułowi biznesowemu — o nazwie modułu z sufiksem **`.UI`**:

| Moduł biznesowy | Biblioteka UI |
|---|---|
| `Soneta.Handel` | `Soneta.Handel.UI` |
| `Soneta.Business` | `Soneta.Business.UI` |
| `Soneta.CRM` | `Soneta.CRM.UI` |

Cel: **rozdzielenie logiki biznesowej od prezentacji**. Moduł biznesowy nie ma (i nie może mieć)
referencji do warstwy UI ani do innych modułów biznesowych; biblioteka `.UI` **może** referować
wiele modułów biznesowych, dzięki czemu formularz sięga po typy z różnych obszarów. Dlatego
formularze obiektów z `Soneta.X` zapisuj w projekcie `Soneta.X.UI`, nie obok klas biznesowych.

## Typy plików formularzy

| Typ pliku | Wzorzec nazwy | Przeznaczenie |
|-----------|---------------|---------------|
| **pageform.xml** | `{DataType}.{PageName}.pageform.xml` | Zakładka formularza edycji obiektu |
| **Config.*.pageform.xml** | `Config.{Nazwa}.pageform.xml` | Strona okna Opcji (Ustawienia); `CaptionHtml` z `/` = hierarchia drzewa Opcji — [references/binding.md](references/binding.md#strony-okna-opcji-konfiguracja) |
| **viewform.xml** | `{NazwaWidoku}.viewform.xml` | Widok listy zarejestrowanej jako folder (listy główne) |
| **gridform.xml** | `{IdentyfikatorListy}.gridform.xml` | Indywidualne ustawienia listy na formularzu |
| **lookupform.xml** | `{NazwaPodpowiedzi}.lookupform.xml` | Lista wyboru (lookup) |
| **form.xml** | `{Nazwa}.form.xml` | Współdzielony fragment UI (include) |

**Przykłady pageform.xml:** `Towar.Ogolne.pageform.xml`, `Kontrahent.Adresy.pageform.xml`

Jak plik wiąże się z typem obiektu (nazwa pliku, `DataType`, `FolderViewAttribute`) i jak system
składa wiele zakładek w jedno okno — [references/binding.md](references/binding.md).

## Struktura dokumentu

Każdy plik formularza zaczyna się od deklaracji XML i elementu `DataForm`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<DataForm xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
          xmlns:xsd="http://www.w3.org/2001/XMLSchema"
          xmlns="http://www.enova.pl/schema/form.xsd"
          xsi:schemaLocation="http://www.enova.pl/schema/ https://www.enova.pl/schema/form.xsd">
  <!-- zawartość -->
</DataForm>
```

Kompletne pliki do skopiowania: [references/examples.md](references/examples.md).

### Atrybuty DataForm

| Atrybut | Opis |
|---------|------|
| `Priority` | Kolejność zakładek (domyślnie 100, niższa = wcześniej) |
| `RightName` | Opcjonalny. Nazwa uprawnienia do zakładki |
| `Contexts` | Warunki licencyjne, np. `"Licence.HAN \| Licence.FA"` |
| `ViewType` | Typ widoku: `None`, `Dialog`, `Form`, `Folder` |
| `Mode` | Tryb: `None`, `Form`, `Folder`, `Wizard`, `Modal`, `Popup`, `Frame` |
| `DataType` | Opcjonalny. Pełna nazwa typu gdy nie wynika z nazwy pliku |

## Wspólne atrybuty elementów

| Atrybut | Opis |
|---------|------|
| `Name` | Identyfikator elementu (dostępny w kodzie C#) |
| `Class` | Klasy stylów (lista wartości oddzielonych spacją) |
| `DataContext` | Zmienia kontekst danych dla elementu i elementów podrzędnych |
| `Visibility` | Warunek widoczności (bindowalne): `true`/`false`/`{wyrażenie}` |
| `Renderable` | Czy element w ogóle powstaje — liczone **raz**, przy budowie układu (patrz niżej) |
| `Width` | Szerokość **samego pola edycyjnego** w znakach lub px; `"*"` = wypełnij |
| `LabelWidth` | Szerokość samej etykiety |
| `OuterWidth` | Szerokość **etykiety razem z polem** — tym wyrównuje się kolumny |
| `Height` | Wysokość w wierszach lub px; `"*"` = wypełnij |

**`Renderable` vs `Visibility`.** `Renderable` jest liczone raz, przy budowie układu formularza, a
zbudowany układ jest cache'owany (odbudowuje się m.in. po zapisie konfiguracji, przy zmianie języka
i osobno dla klienta web i mobile). Element z `Renderable=false` zostaje **usunięty z drzewa**,
więc nie da się go potem pokazać — dlatego `Renderable` nadaje się wyłącznie do warunków
licencyjnych i środowiskowych, nigdy do warunków zależnych od danych. Do tych drugich służy
`Visibility`, liczone przy każdym przeliczeniu formularza.

### Atrybut CaptionHtml

- Tekst etykiety (w formacie HTML)
- Wyrażenia bindowane: `{Właściwość}` → wartość automatycznie kodowana do HTML
- Wyrażenia HTML: `{WłaściwośćHtml}` → sufiks `Html` wyłącza kodowanie
- Podwójne klamry dla literałów: `{{` → `{`
- Alternatywa: `CaptionMarkdown` dla Markdown

**Specjalne przypadki w `Field`:**
- Brak atrybutu → automatyczna etykieta wyliczana na podstawie danych
- `CaptionHtml=" "` (spacja) → pusta etykieta (miejsce zachowane)
- `CaptionHtml=""` (pusty) → brak etykiety i miejsca

## Elementy kontenerowe

### Page - Zakładka

```xml
<Page CaptionHtml="Ogólne" DataContext="{DataSource}">
  <!-- zawartość zakładki -->
</Page>
```

| Atrybut | Opis |
|---------|------|
| `CaptionHtml` | Tytuł; może zawierać `/` do grupowania (np. `"Dokumenty/Faktury"`) |
| `DataContext` | Źródło danych; `{DataSource}` = obiekt edytowany |
| `Visibility` | Wyrażenie warunkowe widoczności (bindowalne) |
| `Renderable` | Liczone raz przy budowie układu — tylko dla warunków licencji/środowiska |
| `Key` | Skrót klawiaturowy |

### Group - Grupa pól

```xml
<Group CaptionHtml="Dane podstawowe" LabelWidth="20">
  <Field CaptionHtml="Kod" Width="20" EditValue="{Kod}" />
  <Field CaptionHtml="Nazwa" Width="*" EditValue="{Nazwa}" />
</Group>
```

`LabelWidth` ustawia szerokość etykiet dla wszystkich pól w grupie.

### Stack, Row, Flow - Układ elementów

```xml
<!-- Stack: układ pionowy (elementy jeden pod drugim) -->
<Stack LabelWidth="15">
  <Field CaptionHtml="Pole 1" EditValue="{Pole1}" />
</Stack>

<!-- Row: układ poziomy -->
<Row>
  <Field CaptionHtml="Kod" Width="20" EditValue="{Kod}" />
  <Gap Width="*" />  <!-- przesuwa kolejne elementy do prawej -->
  <Field CaptionHtml="Status" Width="15" EditValue="{Status}" />
</Row>

<!-- Flow: elementy od lewej do prawej z zawijaniem -->
<Flow Align="true">
  <Field CaptionHtml="Data od" Width="15" EditValue="{DataOd}" />
  <Field CaptionHtml="Data do" Width="15" EditValue="{DataDo}" />
</Flow>
```

### ★ Układ wielokolumnowy — szerokości ustawiaj przez `OuterWidth`

To najczęstsze źródło rozjeżdżonych formularzy. Zapamiętaj różnicę:

| Atrybut | Co obejmuje |
|---------|-------------|
| `Width` | **samo pole edycyjne**, bez etykiety |
| `LabelWidth` | samą etykietę |
| `OuterWidth` | **etykietę razem z polem** — czyli całą kolumnę |

W układzie wielokolumnowym o wyrównaniu kolumn decyduje `OuterWidth`. Gdy ustawisz tylko `Width`,
kolumny rozjadą się przy każdej różnicy w długości etykiet — bo etykieta dokłada się do szerokości
poza tym, co zadeklarowałeś.

Są dwa poprawne układy wielokolumnowe; oba wymagają **określonych szerokości kolumn**:

```xml
<!-- Kolumny jako Stack w Row — dla kolumn z wieloma polami -->
<Row>
  <Stack OuterWidth="40">
    <Field CaptionHtml="Sposób dostawy" OuterWidth="40" EditValue="{SposobDostawy}" />
    <Field CaptionHtml="Termin dostawy" OuterWidth="40" EditValue="{TerminDostawy}" />
  </Stack>
  <Gap Width="5" />
  <Stack OuterWidth="40">
    <Field CaptionHtml="Sposób zapłaty" OuterWidth="40" EditValue="{SposobZaplaty}" />
    <Field CaptionHtml="Termin zapłaty" OuterWidth="40" EditValue="{TerminZaplaty}" />
  </Stack>
</Row>

<!-- Field wprost w Row — dla pojedynczych wierszy z kilkoma polami -->
<Row>
  <Field CaptionHtml="Kod"  OuterWidth="30" EditValue="{Kod}" />
  <Field CaptionHtml="Data" OuterWidth="26" EditValue="{Data}" />
</Row>
```

> **⚠️ Czego unikać:** `<Stack>` **bez zadeklarowanej szerokości** z polami `Width="*"` w środku.
> Taki układ renderuje „kolumny" jedna na drugiej — etykiety i pola się nakładają. Problemem nie
> jest `Row` + `Stack` (to kanoniczny wzorzec dwukolumnowy), tylko brak szerokości kolumny.

Po zbudowaniu układu wielokolumnowego **zawsze zweryfikuj go wizualnie** zrzutem ekranu —
[references/ux-design.md](references/ux-design.md).

### Zasada budowania zakładki

```xml
<Page CaptionHtml="Ogólne" DataContext="{DataSource}">
  <Group CaptionHtml="Dane podstawowe">
    <!-- pola pionowo -->
    <Field CaptionHtml="Kod" Width="20" EditValue="{Kod}" />
    <!-- lub wielokolumnowo — kolumny z OuterWidth (patrz sekcja o układzie wielokolumnowym): -->
    <Row>
      <Field CaptionHtml="Data" OuterWidth="26" EditValue="{Data}" />
      <Field CaptionHtml="Status" OuterWidth="28" EditValue="{Status}" />
    </Row>
  </Group>
  <Group CaptionHtml="Pozycje">
    <Grid Width="*" Height="*" EditValue="{Pozycje}" IsToolbarVisible="true">
      <Field CaptionHtml="Nazwa" Width="30" EditValue="{Nazwa}" />
    </Grid>
  </Group>
</Page>
```

> **Szerokość kolumn w `Grid`.** `Width="*"` działa na **kontenerach** (`Grid`, `Group`, `Stack`),
> ale **kolumny listy** (`Field` wprost w `Grid`) muszą mieć **stałą** szerokość w znakach —
> wartość nieliczbowa jest po cichu ignorowana. Szczegóły i pozostałe atrybuty listy:
> [references/collections-grids.md](references/collections-grids.md).

## Elementy pól i kontrolek

### Field - Pole edycyjne

Jest generowany **dynamicznie** — typ właściwości decyduje o kontrolce (int → liczba, bool → checkbox, typ Sonety → lookup).

```xml
<Field CaptionHtml="Nazwa pola" Width="20" EditValue="{Właściwość}"
       Important="true" IsReadOnly="{Warunek}" />
```

| Atrybut | Opis |
|---------|------|
| `EditValue` | **Wymagany**. Binding do właściwości: `{Właściwość}` lub `{new Ext.Właściwość}` |
| `CaptionHtml` | Etykieta pola |
| `Width` | Szerokość samego pola edycyjnego (`*` = wypełnij) |
| `OuterWidth` | Szerokość etykiety razem z polem — używaj do wyrównywania kolumn |
| `Height` | Wysokość — **niepusta wartość włącza tryb wieloliniowy**, patrz niżej |
| `Important` | `true` — pole oznaczone jako ważne (wyróżnione w widoku) |
| `IsReadOnly` | Warunek tylko do odczytu (bindowalne) — **zwykle zbędny**, patrz niżej |
| `Format` | Formatowanie w standardzie .NET: `N2`, `d`, `C` |
| `Footer` | Agregacja w stopce listy: `Sum`, `Count`, `Average`, `Min`, `Max` |
| `CheckedValue` | Wartość dla RadioButton — **zawsze przełącza edytor na radio**, patrz niżej |
| `Class` | Klasy stylów |

**Kiedy NIE dodawać `IsReadOnly`.** Tryb tylko-do-odczytu jest wyliczany automatycznie — nie
dokładaj `IsReadOnly="true"`, gdy pole i tak ma być nieedytowalne z jednego z poniższych powodów:
- **property bez settera** (tylko `get`) — np. pole `readonly`/selektor z definicji danych lub
  property kalkulowana — jest read-only z definicji (definicję pól tabeli opisuje [table-reference.md](../business-xml/references/table-reference.md));
- **prawa do obiektu biznesowego** — brak prawa zapisu blokuje edycję automatycznie;
- **metoda `IsReadOnlyX()`** obok property `X` w klasie biznesowej — zwraca warunek, czy edytor ma
  być zablokowany (to preferowany sposób sterowania read-only, bo logika zostaje przy danych).

`IsReadOnly` na `Field` stosuj **tylko** gdy chcesz **nadpisać** ten standardowy mechanizm
(np. zablokować w UI property, która ma setter i nie jest objęta `IsReadOnlyX()`).

### ★ Metody sterujące obok property — konwencja nazewnicza

`IsReadOnlyX()` to jedna z rodziny metod, których silnik szuka **po nazwie**, obok property `X`.
Wszystkie są bezparametrowe i publiczne:

| Metoda | Do czego |
|--------|----------|
| `IsReadOnly<X>()` | blokada edycji pola |
| `IsReadOnly()` (bez nazwy pola) | blokada edycji całego obiektu |
| `IsVisible<X>()` | **widoczność pola** |
| `GetList<X>()` | lista dozwolonych wartości (combo, lookup) |
| `GetAppearance<X>()` | formatowanie warunkowe wyliczane w kodzie |
| `IsRequired<X>()` | pole wymagane |
| `GetLocalized<X>()` | wartość zlokalizowana |

Zaletą tych metod jest to, że logika zostaje przy danych i działa w każdym formularzu, który
pokazuje to pole — nie trzeba jej powtarzać w XML.

> **⚠️ Pułapka:** `IsVisible<X>()` działa **tylko wtedy, gdy element nie ma atrybutu `Visibility`
> w XML**. Atrybut ma pierwszeństwo i po cichu wyłącza konwencję — pole będzie widoczne mimo
> metody zwracającej `false`. Wybierz jedno: albo metodę przy property, albo `Visibility` w XML.

### ★ `[Accessor(AutoChange = true)]` dla property spoza rekordu

Gdy `EditValue` wskazuje property pomocniczą, której ustawienie **nie zmienia obiektu sesyjnego**
(property na klasie parametrów, extenderze albo obiekcie sterującym oknem), oznacz ją atrybutem
`[Accessor(AutoChange = true)]`. Bez tego formularz nie odświeży pól zależnych po zmianie wartości
— zmiana „nie zostanie zauważona", bo nie przeszła przez sesję.

```csharp
[Accessor(AutoChange = true)]
public string WybranaOpcja { get; set; }
```

Alternatywa dla bardziej złożonych przypadków: `Session.InvokeChanged()` / `Context.InvokeChanged()`
w setterze. Szczegóły — [contextbase.md](../programming/references/contextbase.md), [viewinfo.md](../programming/references/viewinfo.md).

### RadioButton, przełącznik i lista wielokrotnego wyboru

**RadioButton** — pola z tym samym `EditValue` i różnymi `CheckedValue`:
```xml
<Field Width="15" CaptionHtml="Towar" EditValue="{Typ}" CheckedValue="Towar" />
<Field Width="15" CaptionHtml="Usługa" EditValue="{Typ}" CheckedValue="Usługa" />
```

Typ property po drugiej stronie może być:
- **`string`** — porównanie tekstowe, jak wyżej;
- **`enum`** — `CheckedValue` to nazwa wartości (`CheckedValue="Towar"`) albo jej numer
  (`CheckedValue="2"`);
- **`bool`** — `CheckedValue="true"` / `"false"` (akceptowane też `"1"` / `"0"`); tak buduje się
  parę radiów „Tak/Nie" nad jednym polem logicznym.

> **⚠️ `CheckedValue` nigdy nie daje checkboxa.** Samo jego ustawienie przełącza edytor na radio,
> a razem z `Class="CheckButtonEdit"` — na przycisk-przełącznik. **Nie da się nim zbudować listy
> wielokrotnego wyboru.** Do tego służą wzorce niżej.

**Lista wielokrotnego wyboru** — trzy drogi, zależnie od długości listy:

1. **Pojedyncze checkboxy** — pole `bool` na elemencie kolekcji, bindowane indeksem, **bez**
   `CheckedValue`. Dla krótkiej listy o znanej długości:
   ```xml
   <Field CaptionHtml="{Opcje[0].Nazwa}" EditValue="{Opcje[0].Zaznaczona}" />
   <Field CaptionHtml="{Opcje[1].Nazwa}" EditValue="{Opcje[1].Zaznaczona}" />
   ```
   Składnię indeksowania opisuje [references/dynamic-forms.md](references/dynamic-forms.md).
2. **Grid z kolumną `bool`** i `EditInPlace="true"` — dla długiej listy, bo dochodzi sortowanie i
   filtrowanie. Kolekcja nie musi składać się z obiektów biznesowych; wystarczą zwykłe obiekty
   z property `bool`:
   ```xml
   <Grid Width="*" Height="*" EditValue="{Opcje}" EditInPlace="true"
         NewButton="None" EditButton="None" RemoveButton="None">
     <Field CaptionHtml="Nazwa"   Width="40" EditValue="{Nazwa}" IsReadOnly="true" />
     <Field CaptionHtml="Wybrana" Width="10" EditValue="{Zaznaczona}" />
   </Grid>
   ```
3. **`SelectedValue`** — zaznaczanie wierszy gridu ([references/collections-grids.md](references/collections-grids.md)).
   Najmniej kodu, ale zaznaczenie gubi się przy przeładowaniu listy i trudniej ustawić je z kodu
   niż zwykłe pole `bool`.

**Pole wieloliniowe (memo).** Wieloliniowy edytor tekstu to zwykły `Field` z `Height="N"`
(liczba wierszy). **Tryb wieloliniowy włącza sam fakt podania `Height`** — dowolna niepusta
wartość (`"4"`, `"250px"`, `"*"`) wystarczy; `Width` nie ma z tym nic wspólnego, choć zwykle daje
się `Width="*"`, żeby pole zajęło całą szerokość. Działa dla property typu `string`; jeśli
property ma własny typ edytora (przez `Class` albo atrybut edytora w kodzie), on ma pierwszeństwo.
Aby zrobić panel podglądu tylko-do-odczytu (np. tekst zbudowany w kodzie), dodaj
`IsReadOnly="true"` albo zwiąż go z property bez settera — nie potrzeba `Class` ani specjalnego
edytora:
```xml
<Field CaptionHtml="Podgląd" Width="*" Height="8" IsReadOnly="true" EditValue="{TekstPodgladu}" />
```

### Label, Gap, Command, Include

```xml
<Label CaptionHtml="Tekst informacyjny" Width="30" />
<Gap Width="*" />   <!-- wypełniacz; pusty na końcu wiersza nic nie zmienia -->

<Command CaptionHtml="Zapisz" MethodName="Zapisz"
         DataContext="{new MojExtender}" Visibility="{IsVisible}"
         CommandStyle="Important" Key="F5" />

<!-- Dołączenie pliku form.xml -->
<Include Source="Adres.form.xml" DataContext="{Adres}" />
<!-- Dynamiczny element z kodu C# -->
<Include Source="{DynamicznyFormularz}" />
```

`Command` wywołuje metodę (`MethodName`) lub otwiera obiekt (`OpenMethodName`) z kontekstu —
zwykle z extendera/workera. Co taka akcja zwraca (action result: zamknięcie okna, otwarcie
formularza, komunikat) opisują [action-result.md](../programming/references/action-result.md) i [worker-extender.md](../programming/references/worker-extender.md).

### Grid — listy i kolekcje

Element listowy zasilany kolekcją lub `ViewInfo`:

```xml
<Grid Width="*" Height="*" EditValue="{Pozycje}" IsToolbarVisible="true">
  <Field CaptionHtml="Kod" Width="15" EditValue="{Kod}" />
  <Field CaptionHtml="Ilość" Width="10" EditValue="{Ilosc}" Footer="Sum" />
</Grid>
```

Komplet atrybutów, edycja w miejscu, `SelectedValue`/`FocusedValue`, listy sterowane kodem
(`ViewInfo`) i pasek filtra `Flow Class="DataBar"` —
[references/collections-grids.md](references/collections-grids.md).

## Atrybut Class — najważniejsze wartości

**Style etykiet:** `BoldLabel`, `CenterLabel`, `RightLabel`, `WarningLabel`, `InfoLabel`, `NoColonLabel`

**Style czcionek:** `BoldFont`, `LargeFont`, `GreenFont`, `RedFont`

**Typy edytorów:** `PasswordEdit`, `RichEdit`, `ImageEdit`, `HyperlinkEdit`, `EmailEdit`, `PhoneEdit`, `ColorEdit`, `ProgressEdit`, `RatingEdit`, `FileEdit`

**Zachowania kontenerów:** `Collapsable`, `Expandable`, `Expanded`, `Scrollable`, `FirstResponder`

**Przyciski:** `MainCommand`, `SplitCommand`, `CommandText`, `CommandIco`, `CommandIcoText`

**Wyrównanie:** `LeftAlign`, `RightAlign`, `TextRight`

**Pozycjonowanie:** `GroupItem` — element na poziomie nagłówka Group (zwykle po prawej)

**Etykiety:** `LabelLeft` (domyślne na formularzach), `LabelTop` — patrz
[references/ux-design.md](references/ux-design.md)

> Pełna lista Class: [references/ELEMENTS.md](references/ELEMENTS.md)

## Appearance - Warunkowe formatowanie

```xml
<Field EditValue="{Saldo}">
  <Appearance Condition="{?Saldo&lt;0}" ForeColor="Red" FontBold="true" />
  <Appearance Condition="{?Saldo&gt;1000}" BackColor="LightGreen" />
</Field>
```

Składnia `Condition` może używać nawiasów kwadratowych gdy nazwa pola zawiera spacje lub operator porównania wymaga jawnego typu:
```xml
<Appearance Condition="{?[Typ] = 'usługa'}" ForeColor="#800080" />
```

Warunek `Appearance.Condition` to ta sama składnia co RowCondition w `Visibility`
([references/binding.md](references/binding.md)) — jego odpowiednikiem po stronie kodu jest
`Expression<Predicate<TRow>>`; patrz [rowcondition.md](../programming/references/rowcondition.md).

## Powiązane skille — gdzie szukać strony kodu

Form.xml opisuje **prezentację**; logikę i dane opisują skille obok. Mapa pojęcie → artykuł:

| Pojęcie w form.xml | Strona kodu / danych |
|---|---|
| `Visibility="{?...}"`, `Appearance.Condition` | [rowcondition.md](../programming/references/rowcondition.md) (`Expression<Predicate<TRow>>`) |
| `Grid EditValue="{...View}"` (ViewInfo) | [viewinfo.md](../programming/references/viewinfo.md) |
| `Flow Class="DataBar"`, `{XParams.Pole}` | [contextbase.md](../programming/references/contextbase.md), [context.md](../programming/references/context.md) |
| `{Features.NazwaCechy}`, `VisibleFeatures` | [features.md](../programming/references/features.md) |
| `{Workers.Alias.Pole}`, `{new Extender.Pole}` | [worker-extender.md](../programming/references/worker-extender.md) |
| `Command MethodName`/`OpenMethodName` | [action-result.md](../programming/references/action-result.md), [worker-extender.md](../programming/references/worker-extender.md) |
| pole `readonly`/selektor, definicja pól | [table-reference.md](../business-xml/references/table-reference.md) |
| odczyt zakładek i pól z DLL (bez źródeł) | [scan-forms.md](../programming/references/scan-forms.md) |
| wizualna weryfikacja wyglądu formularza na żywo (zrzut ekranu) | [buscall-live-testing.md](../programming/references/buscall-live-testing.md) (składnia `buscall` w [tools](../tools/SKILL.md)) |
