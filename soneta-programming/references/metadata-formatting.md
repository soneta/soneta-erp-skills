# Notacja klamrowa i metadane modeli

## Notacja klamrowa (`AccessorFormatter`)

Tekst z **wstawkami danych** (captiony, szablony, opisy, treści promptów) komponuje się notacją
klamrową obsługiwaną przez `AccessorFormatter`. Wartości pól pobierane są przez `Accessor` obiektu
kontekstu — bez ręcznego sklejania stringów.

- `{ścieżka}` — wartość accessora, np. `{Kontrahent.Nazwa}`,
- `{ścieżka:format}` — z formatem po dwukropku, np. `{Data:yyyy-MM-dd}`,
- `{{` / `}}` — literalna klamra (escape).

```csharp
var f = new AccessorFormatter { UseHtmlEncoding = false };
f.Compile("Faktura dla {Kontrahent.Nazwa} z dnia {Data:yyyy-MM-dd}", accessor);
string text = f.ToString();
```

Bez accessora można podać własne źródło wartości przez `GetValueHandler`. Obsługuje też pola
Html/Markdown (kodowanie wyniku). Tej samej notacji używaj zamiast definiowania osobnych
„parametrów" tekstu.

## Metadane modułów, tabel, kluczy, pól

Dostęp do metadanych obiektów biznesowych jest dostępny przez metody `static` klasy `ApplicationInfo`.

- Odczyt informacji o tabeli: `TableInfo info = ApplicationInfo.GetTableInfo(nazwaTabeli)`. Istnieje tylko jedna referencja obiektu `TableInfo` dla tabeli - można używać `ReferenceEquals`, `Dictionary`, itp.
- Wszystkie tabele: `ApplicationInfo.GetTablesInfo()`.
- Tabele dla modułu: `ApplicationInfo.GetModuleInfo(moduleName).TableInfos`.

### Wykorzystuj `TableInfo` do weryfikacji tabeli

```csharp
Row row1 = ...;
Row row2 = ...;
if (row1.Table.TableInfo == row2.Table.TableInfo) {
    // Ta sama tabela, nawet gdy różne sesje
}
```
