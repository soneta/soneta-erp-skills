# Bindowanie danych — powiązanie typu, kontekst, składnia wyrażeń

Jak formularz trafia do obiektu, jak zmienia się kontekst danych i jaka składnia obowiązuje
w `{...}`. Podstawy składni formularza: [../SKILL.md](../SKILL.md).

## Powiązanie typu z formularzem

1. **Przez nazwę pliku** — pierwszy człon nazwy pliku wyznacza typ okna. Może to być:
   - **klasa** — `Towar.Ogolne.pageform.xml` → kontekst klasy `Towar`;
   - **interfejs** — wspólna zakładka wielu tabel implementujących dany interfejs (relacje
     interfejsowe, np. `IKontrahent...`);
   - **klasa dziedzicząca (selektor)** — zakładka dla wariantu/podtypu obiektu;
   - **`Config.`** — okno konfiguracji (ustawienia modułów), np. `Config.DefDokHandlowych...` —
     pełny opis: [Strony okna Opcji](#strony-okna-opcji-konfiguracja) niżej.
2. **Przez atrybut DataType** — `<DataForm DataType="Soneta.Handel.Towar,Soneta.Handel">`;
   wiąże typ **jawnie**, gdy nazwa pliku jest niejednoznaczna (formularze parametrów workerów,
   konfiguracji, selektory) lub gdy typ jest w innej przestrzeni nazw.
3. **Przez rejestrację FolderViewAttribute** — dla viewform.xml

> Odczyt rzeczywistego powiązania (który człon/`DataType`, także zawężanie po namespace, gdy ta
> sama nazwa jest w wielu modułach) z zasobów DLL realizuje skaner **`scan-forms`**
> ([scan-forms.md](../../programming/references/scan-forms.md)).

## Wiele zakładek jednego okna — auto-składanie po nazwie pliku

Okno wielozakładkowe **nie** wymaga rejestracji zakładek w kodzie. Wystarczy dodać kolejny
plik `{Typ}.{NazwaZakładki}.pageform.xml` — system zbiera **wszystkie** pliki o tym samym
prefiksie typu i składa je w jedno okno (każdy plik = jedna `Page`). Aby dorzucić zakładkę do
istniejącego okna (np. okna narzędziowego/konfiguracyjnego sterowanego klasą `Foo`), dodaj
plik `Foo.Moja.pageform.xml` z `<Page CaptionHtml="Moja" DataContext="{DataSource}">` — pojawi
się automatycznie. `CaptionHtml` bez `/` → samodzielna zakładka; z `/` → hierarchia.

> **Osadzanie zasobu.** Pliki `*.pageform.xml` / `*.form.xml` / `*.viewform.xml` są dołączane
> do biblioteki jako zasób przez konwencję budowania projektu — zwykle **nie** trzeba dodawać
> ich ręcznie do pliku projektu. Po dodaniu pliku wystarczy zbudować projekt i zakładka jest
> dostępna. (Jeśli wyjątkowo nie zostanie znaleziona, dopiero wtedy rozważ jawne dołączenie
> zasobu w konfiguracji projektu.)

> **Zakładki i grupy = sekcje danych.** `Page` i `Group` wyznaczają logiczne **sekcje danych**
> do uzupełnienia, a kolejność pól odzwierciedla kolejność wprowadzania (i pośrednio wykonywanego
> kodu). Ma to znaczenie przy budowaniu danych **kodem** oraz przy **imporcie XML `business="true"`**
> (patrz skill [config](../../config/SKILL.md)). Gdy masz tylko skompilowane DLL (bez źródeł formularzy), zakładki,
> sekcje i rozwinięte ścieżki pól (łańcuch `DataContext`+`EditValue`, dołączane `Include`)
> odczytasz z zasobów osadzonych skanerem **[scan-forms](../../programming/references/scan-forms.md)**.

## Strony okna Opcji (konfiguracja)

Stronę w oknie Opcji (Ustawienia / Narzędzia → Opcje) dodaje plik **`Config.{Nazwa}.pageform.xml`**
w projekcie `.UI` — osadza się automatycznie, jak inne formy (zob. wyżej *Osadzanie zasobu*).

- `Page CaptionHtml="Ścieżka/Poddrzewo/Nazwa"` — człony rozdzielone `/` budują **hierarchię
  drzewa Opcji** (np. `CaptionHtml="Systemowe/Mój obszar/Definicje"` → gałąź Systemowe →
  Mój obszar → strona Definicje). To zastosowanie ogólnej reguły „`CaptionHtml` z `/` =
  hierarchia" specyficznie dla okna konfiguracji.
- Dane strony dostarcza **extender** ustawiany jako kontekst strony:
  `DataContext="{New MojConfigExtender}"` — klasa z property widoków list konfiguracyjnych
  (np. `public View Definicje => …CreateView()` na sesji konfiguracyjnej okna Opcji).
  Namespace klasy extendera wg katalogu pliku. Extendery i konwencje (`IsVisibleX()`,
  `GetListX()`) opisuje [worker-extender](../../programming/references/worker-extender.md).
- Typowa zawartość: `Group` + `Grid` bindowany do widoku z extendera + standardowe komendy
  wierszy (Dodaj/Otwórz/Usuń) — grid otwiera formularze obiektów (pageformy `{Typ}.{Zakładka}`).

```xml
<!-- Config.MojeDefinicje.pageform.xml (projekt .UI) -->
<Page CaptionHtml="Systemowe/Mój obszar/Definicje" DataContext="{New MojConfigExtender}">
  <Group CaptionHtml="Definicje">
    <Grid Width="*" Height="*" EditValue="{Definicje}">
      <Field CaptionHtml="Nazwa" Width="40" EditValue="{Nazwa}" />
      <Field CaptionHtml="Blokada" Width="10" EditValue="{Blokada}" />
    </Grid>
  </Group>
</Page>
```

Checklista strony Opcji:
- [ ] plik `Config.{Nazwa}.pageform.xml` w projekcie `.UI`
- [ ] `CaptionHtml` z pełną ścieżką drzewa Opcji (człony `/`)
- [ ] extender w `DataContext="{New …}"` dostarcza widoki list
- [ ] jeżeli obiekty konfiguracyjne są źródłami praw (`IRightsSource`) — prawa nadane przy
      tworzeniu bazy ([rights-source](../../programming/references/rights-source.md), [import-export-xml](../../config/references/import-export-xml.md))
- [ ] weryfikacja wizualna na żywo: `get_configuration_folders "regexFilter=<nazwa>"` →
      `navigate_to_folder` → `take_screenshot` (buscall — [tools](../../tools/SKILL.md))

## Zmiana kontekstu danych

- `DataContext="{Adres}"` — zmienia kontekst **aktualnego elementu i podrzędnych**
- `EditValue="{Pozycje}"` na **elemencie listowym** — zmienia kontekst **tylko podrzędnych**:
  pola/kolumny wewnątrz odnoszą się już do **elementu kolekcji** zwróconej przez to `EditValue`
  (inny obiekt niż kontekst rodzica), nie do bieżącego `DataSource`. Dotyczy to **wszystkich
  elementów listowych**, nie tylko `Grid`: `Grid`, `List`, `Scheduler`, `Gantt`,
  `GanttDiagram`, `KanbanDiagram`, `Pivot`, `Chart`, `Diagram`, `TreeDiagram`. Np. w
  `<Grid EditValue="{Pozycje}">` kolumna `<Field EditValue="{Cena}">` to `Pozycje` → element →
  `Cena`. Pełne ścieżki pól z rozwiniętym kontekstem (marker `[]` dla elementu kolekcji)
  wypisuje skaner **`scan-forms`** ([scan-forms.md](../../programming/references/scan-forms.md)).

**`{DataSource}` to obiekt sterujący oknem.** Zwykle jest to edytowany `Row`, ale równie dobrze
może być klasa sterująca oknem narzędziowym/diagnostycznym. Wtedy pola (także filtry w pasku
`Flow`) bindują się wprost do jej publicznych property przez dziedziczony `DataContext` strony,
bez osobnego obiektu kontekstu:
```xml
<Page CaptionHtml="..." DataContext="{DataSource}">
  <Flow Align="true">
    <Field CaptionHtml="Operator" Width="25" EditValue="{Operator}" />  <!-- property obiektu sterującego -->
    <Field CaptionHtml="Okres"    Width="22" EditValue="{Okres}" />
  </Flow>
  ...
</Page>
```

## Składnia wyrażeń `{...}`

| Składnia | Opis |
|----------|------|
| `{Właściwość}` | Publiczna właściwość w kontekście |
| `{Obiekt.Właściwość}` | Właściwość zagnieżdżona |
| `{Kolekcja[0].Właściwość}` | Element kolekcji po indeksie — [dynamiczne listy pól](dynamic-forms.md) |
| `{Obiekt+SubObiekt.Właściwość}` | Operator `+` — nawigacja przez powiązany obiekt ViewInfo |
| `{Workers.NazwaWorkera.Pole}` | Właściwość workera |
| `{new NazwaExtender.Pole}` | Właściwość extendera |
| `{Features.NazwaCechy}` | Cechy powiązane z Row |
| `{Historia.Current.Pole}` | Pole obiektu z historią (`IRowWithHistory`, np. pracownik) — `Current` to wiersz historii aktualny na datę z kontekstu; inne: `First`, `Today`, `Last` |
| `{Context.TypDanych.Pole}` | Wartość z kontekstu UI (`Soneta.Business.Context`) |
| `{Licence.HAN}` | Warunek licencji (używany w `Renderable`) |
| `{.}` | Aktualna wartość w kontekście elementu |

- Czym są **workery i extendery** (`{Workers.Alias.Pole}`, `{new Extender.Pole}`) — obiekty
  doczepiane do Row z dodatkowymi property — opisuje [worker-extender.md](../../programming/references/worker-extender.md).
- Mechanizm **cech** (`{Features.NazwaCechy}`, `VisibleFeatures` na `Grid`) opisuje [features.md](../../programming/references/features.md).
- Klasę parametrów stojącą za `{Context...}` (`Params : ContextBase`) opisują [contextbase.md](../../programming/references/contextbase.md) i [context.md](../../programming/references/context.md).

## Wyrażenia warunkowe (RowCondition)

```xml
Visibility="{?State=Added}"             <!-- równość -->
Visibility="{?!State=Added}"            <!-- negacja -->
Visibility="{?Typ=Towar or Typ=Usługa}" <!-- OR -->
Visibility="{?Aktywny and Widoczny}"    <!-- AND -->
```

> **Dwie strony tego samego pojęcia.** RowCondition w form.xml to tekstowe wyrażenie `{?...}`
> rozwiązywane po stronie UI; jego odpowiednikiem w kodzie biznesowym jest serwerowy warunek
> `Expression<Predicate<TRow>>`. Stronę kodu opisuje [rowcondition.md](../../programming/references/rowcondition.md) — tam te same warunki buduje się i komponuje w C#.

Ta sama składnia obowiązuje w `Appearance.Condition` (formatowanie warunkowe —
patrz [../SKILL.md](../SKILL.md)).

## Powiązania

- [../SKILL.md](../SKILL.md) — składnia formularza, `Field`, kontenery, `Visibility` vs `Renderable`.
- [collections-grids.md](collections-grids.md) — konteksty w elementach listowych, filtry list.
- [dynamic-forms.md](dynamic-forms.md) — bindowanie po indeksie, generowanie pól z kodu.
- [examples.md](examples.md) — kompletne pliki pokazujące bindowanie w praktyce.
- [scan-forms.md](../../programming/references/scan-forms.md), [viewinfo.md](../../programming/references/viewinfo.md), [rowcondition.md](../../programming/references/rowcondition.md), [contextbase.md](../../programming/references/contextbase.md),
  [worker-extender.md](../../programming/references/worker-extender.md), [features.md](../../programming/references/features.md).
