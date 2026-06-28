# AssemblyAttributes — odczyt atrybutów z załadowanych modułów

`AssemblyAttributes` (namespace `Soneta.Tools`) to centralne narzędzie do **odczytu atrybutów
z załadowanych assembly** w czasie działania programu. Używa się go wszędzie tam, gdzie trzeba
odkryć rejestracje rozsiane po modułach i dodatkach: `[BusinessRow]`, `[NewRow]`, `[Worker<…>]`,
`[FolderView]`, `[McpServer]` i własne atrybuty rejestrujące. Mechanizm jest **cache'owany**, więc
można go wołać na bieżąco.

> Różnica względem skryptów `scan-*` (patrz `scan-modules.md` itd.): `scan-*` czytają **metadane
> z plików DLL** (Roslyn, bez ładowania do CLR) — do analizy offline. `AssemblyAttributes` działa
> na **assembly już załadowanych** do bieżącego procesu (runtime).

## Atrybuty assembly-level (`GetCustom`)

Większość rejestracji w Soneta to atrybuty **assembly-level** dziedziczące po `AssemblyAttribute`
(np. `[assembly: BusinessRow(...)]`). Odczyt:

```csharp
// wszystkie atrybuty typu T (i pochodnych) ze wszystkich załadowanych assembly
McpServerAttribute[] servers = AssemblyAttributes.GetCustom<McpServerAttribute>();

// z filtrem
var chat = AssemblyAttributes.GetCustom<McpServerAttribute>(a => a.Target == McpServerTargets.Chat);
```

- **Cache per typ atrybutu** — pierwszy odczyt skanuje assembly (równolegle), kolejne wracają
  z cache. Własny cache jest zbędny.
- Dla atrybutów dziedziczących po **`PriorityAttribute`** (m.in. `[NewRow]`, `[McpServer]`) wynik
  jest **posortowany wg `Priority`** (mniejsza wartość = wyżej).
- Każdy `AssemblyAttribute` niesie `attr.Assembly` — assembly (dodatek), z którego pochodzi rejestracja.
- Zbierane są wyłącznie atrybuty z namespace `Soneta.*`.
- Po przeładowaniu zestawu bibliotek: `AssemblyAttributes.ResetAttributesCache()`.

> **Kiedy wołać:** dopiero po załadowaniu wszystkich bibliotek biznesowych. W trybie DEBUG zbyt
> wczesny odczyt (przed `Soneta.Business`) rzuca wyjątek.

### Kolejność wyników a `PriorityAttribute`

Wiele atrybutów rejestrujących dziedziczy po **`PriorityAttribute`** (m.in. `[NewRow]`, `[McpServer]`).
Gdy `T` jest takim typem, `GetCustom<T>()` zwraca wynik **posortowany rosnąco wg `Priority`** —
**mniejsza wartość = wcześniej**. To często **steruje kolejnością przetwarzania** (np. kolejnością
pozycji w menu, kolejnością uruchamiania rejestracji).

- Pole `Priority` (domyślnie `PriorityAttribute.DefaultPriorityValue` = **100**) ustawia się jako
  named argument atrybutu:
  ```csharp
  [assembly: NewRow("Reklamacja", typeof(Zgloszenie.ReklamacjaType), Priority = 50)]  // wcześniej niż domyślne 100
  ```
- Przy równym `Priority` kolejność rozstrzyga pełna nazwa typu atrybutu (stabilność).
- Własny atrybut rejestrujący, dla którego kolejność ma znaczenie, **wyprowadź z `PriorityAttribute`** —
  wtedy zyskuje sterowanie kolejnością „za darmo".

## Atrybuty zapięte do konkretnego typu (`Find`)

Do odczytu atrybutu **z konkretnej klasy** (nie assembly-level):

```csharp
Attribute attr = AssemblyAttributes.Find(typeof(MojaKlasa), typeof(MojAtrybut));
```

## Iteracja po załadowanych assembly

```csharp
foreach (Assembly asm in AssemblyAttributes.GetBusinessAssemblies()) { ... } // tylko zależne od Soneta.Types
foreach (Assembly asm in AssemblyAttributes.GetAssemblies())          { ... } // wszystkie (bez dynamicznych)
```

- `GetBusinessAssemblies()` — assembly biznesowe (referujące `Soneta.Types`); zwykle to one są celem analizy.
- `GetAssemblies()` — wszystkie załadowane z dysku (pomija dynamiczne i obejście duplikatów `*.Test`).
- `GetFromAssemblies()` — surowa lista wszystkich zebranych atrybutów `Soneta.*` (rzadziej potrzebna).

## Typowe zastosowania

- **Discovery podtypów selektora** — zebranie `[BusinessRow]` i zbudowanie mapy `selector → typ`
  (patrz [row-types.md](row-types.md)).
- **Pozycje menu „Nowy"** — `[NewRow]` posortowane wg `Priority`.
- **Pakiety funkcji MCP** — `[McpServer]` z `Target == Chat` (patrz projekt modułu Agents).
- **Inwentaryzacja dodatków** — które rejestracje pochodzą z którego assembly (`attr.Assembly`).
