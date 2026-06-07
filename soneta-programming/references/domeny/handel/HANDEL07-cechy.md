# HANDEL07 — Cechy (Features)

> Wspólne fakty o typie, podstawowe typy i szablon wzorca: [../handel.md](../handel.md).

Cechy (Features) to dodatkowe, definiowalne informacje przypisane do `Row` — tu: do dokumentu
(`DokumentHandlowy`) i pozycji (`PozycjaDokHandlowego`). Definicje cech (`FeatureDefinition`) tworzy
się we wdrożeniu (bez konwersji bazy); cecha jest adresowana **po nazwie definicji**. Dostęp daje
property `Features` (`Soneta.Business.FeatureCollection`) oraz nietypowany indeksator `Row["Nazwa"]`.
Fundamenty cech opisuje `references/features.md` — tu pokazujemy ich użycie na dokumencie handlowym.

> Cechy są częścią publicznego kontraktu. **Samo przenoszenie cech** (z partii / z dokumentu
> nadrzędnego) jest sterowane **konfiguracją definicji dokumentu/relacji**, a nie wywoływane
> imperatywnie z dodatku — patrz HANDEL-W40.

---

### HANDEL-W40 — Przenoszenie cech z partii (dostawy) / towaru na pozycję dokumentu

**Cel:** sprawić, by przy rozchodzie magazynowym cechy zapisane na partii (dostawie) trafiły na
pozycję dokumentu rozchodowego, a przy przekształceniach w relacjach — by cechy dokumentu/pozycji
nadrzędnej zostały skopiowane na dokument podrzędny. To mechanizm **konfiguracyjny**: ustawiasz flagi
na `DefDokHandlowego` / definicji relacji, platforma kopiuje cechy automatycznie podczas operacji.

**Warianty:**

| Wariant | Gdzie ustawić | Pole / mechanizm |
|---|---|---|
| Partia (dostawa) → pozycja rozchodu | definicja dokumentu rozchodowego (WZ/RW/FV) | `DefDokHandlowego.KopiujCechyDostawy: bool` |
| Dokument nadrzędny → podrzędny (cechy nagłówka) | definicja relacji | `KopiujCechyDokumentu: bool` |
| Dokument nadrzędny → podrzędny (cechy pozycji) | definicja relacji | `KopiujCechyPozycji: bool` |
| Wybrane cechy + synchronizacja zwrotna | definicja relacji | konfiguracja „kopiuj cechy" z listą definicji + flagą synchronizacji |
| Ręczne dopisanie cechy na pozycji | kod dodatku | `poz["Nazwa"] = wartość` w transakcji (HANDEL-W41) |

**Pola i typy:**
- `DefDokHandlowego.KopiujCechyDostawy: bool` — „Kopiuj cechy z dostawy"; włącza przeniesienie cech
  partii na pozycję dokumentu **rozchodowego** przy wskazaniu zasobu / księgowaniu rozchodu.
- Na definicji relacji: `KopiujCechyDokumentu: bool`, `KopiujCechyPozycji: bool` — wymuszają
  kopiowanie cech (nagłówka / pozycji) z dokumentu nadrzędnego na podrzędny.
- `poz.Features` / `poz["Nazwa"]` — odczyt/zapis cechy pozycji (typ `FeatureCollection` / `object`).
- Warunkiem działania jest istnienie **tej samej definicji cechy** zarejestrowanej dla obu tabel
  (`PozycjeDokHan`, ewentualnie partia/towar) — kopiowane są cechy o zgodnej nazwie.

**Snippet:**

```csharp
// Włączenie przenoszenia cech z dostawy na pozycję rozchodu — konfiguracja definicji WZ.
// (jednorazowo, na etapie wdrożenia; wykonywane w sesji KONFIGURACYJNEJ)
var handel = session.GetHandel();
var defWZ = handel.DefDokHandlowych.WgSymbolu["WZ"];

using (var t = session.Logout(editMode: true))
{
    defWZ.KopiujCechyDostawy = true;   // cechy partii trafią na pozycję dokumentu rozchodowego
    t.Commit();
}
session.Save();

// Po włączeniu flagi: tworzysz przyjęcie z cechą partii, a przy rozchodzie (wskazanie zasobu)
// cecha jest kopiowana na pozycję automatycznie — nie kopiujesz jej w kodzie.
// Przyjęcie (PW/PZ) — cecha "NrSerii" zapisana na pozycji = cecha dostawy/partii:
using (var t = session.Logout(editMode: true))
{
    var pw = new DokumentHandlowy();
    session.AddRow(pw);
    pw.Definicja = handel.DefDokHandlowych.WgSymbolu["PW"];
    pw.Magazyn = session.GetMagazyny().Magazyny.WgSymbol["F"];

    var poz = new PozycjaDokHandlowego(pw);
    session.AddRow(poz);
    poz.Towar = session.GetTowary().Towary.WgKodu["BIKINI"];
    poz.Ilosc = new Quantity(10, poz.Ilosc.Symbol);
    poz.Cena  = new DoubleCy(5m, poz.Cena.Symbol);
    poz["NrSerii"] = "S-2026-001";    // cecha partii (definicja "NrSerii" dla PozycjeDokHan)

    pw.Stan = StanDokumentuHandlowego.Zatwierdzony;
    t.Commit();
}
session.Save();                        // dopiero teraz powstaje zasób/partia z cechą

// Rozchód WZ ze wskazaniem partii — cecha "NrSerii" pojawi się na pozycji WZ
// dzięki KopiujCechyDostawy = true (kopiowane przez platformę przy księgowaniu rozchodu).
```

**Pułapki:**
- Przeniesienie cech z dostawy to **konfiguracja**, nie API: bez `KopiujCechyDostawy = true` na
  definicji dokumentu rozchodowego nic się nie skopiuje — nie próbuj „przepisywać" cech partii
  imperatywnie z dodatku.
- Kopiowane są cechy o **tej samej nazwie definicji** zarejestrowane dla pozycji; definicja cechy
  musi istnieć przed użyciem (inaczej `poz["Nazwa"] = …` rzuci wyjątek — patrz HANDEL-W41).
- Cecha partii „materializuje się" dopiero po `Session.Save()` dokumentu przychodowego (to wtedy
  powstaje zasób/obrót). Wskazanie partii przy rozchodzie i kopiowanie cechy działa na **zapisanych**
  zasobach (Demo blokuje stan ujemny — rozchód wymaga wcześniejszego zapisanego przyjęcia).
- Kopiowanie nadrzędny→podrzędny w relacjach (`KopiujCechyDokumentu`/`KopiujCechyPozycji`) ustawia
  się na **definicji relacji**, nie na definicji dokumentu; faktyczne tworzenie podrzędnego rób przez
  `IRelacjeService` (sekcja relacji), a cechy dojdą same.
- Konfigurację definicji rób w sesji **konfiguracyjnej** (`config: true`) — to dane konfiguracyjne,
  nie operacyjne (`safe-code.md`).

---

### HANDEL-W41 — Odczyt i zapis cech dokumentu / pozycji (`Features`)

**Cel:** odczytać i ustawić wartości cech na dokumencie handlowym i jego pozycjach — zarówno
nietypowano (po nazwie definicji), jak i typowano (gettery `FeatureCollection`).

**Warianty:**

| Wariant | Dostęp | Zwraca / przyjmuje |
|---|---|---|
| Odczyt nietypowany | `dok["Nazwa"]`, `poz["Nazwa"]` | `object` (`null`, gdy brak wartości) |
| Odczyt typowany | `dok.Features.GetString/GetInt/GetDecimal/GetDate/GetBool/GetCurrency/GetDoubleCy/GetPercent/GetAmount(...)` | konkretny typ Soneta |
| Zapis (dowolny typ) | `dok["Nazwa"] = wartość` w transakcji | — |
| Sprawdzenie istnienia | `dok.Features.Exists("Nazwa")` | `bool` |
| Usunięcie wartości | `dok.Features.Remove("Nazwa")` w transakcji | — |
| Kopiowanie całego zestawu | `źródło.Features.CopyTo(cel.Features)` | — |
| Lista definicji | `dok.Features.Definitions` | `FeatureDefinitions` |

**Pola i typy:**
- `DokumentHandlowy.Features: Soneta.Business.FeatureCollection`,
  `PozycjaDokHandlowego.Features: Soneta.Business.FeatureCollection`.
- Indeksator nietypowany: `object this[string name]` na `Row` (`dok["Nazwa"]`) — równoważny
  `dok.Features["Nazwa"]`.
- Gettery typowane (wybór): `GetString`, `GetInt`, `GetBool`, `GetDecimal`, `GetDouble`, `GetDate`,
  `GetTime`, `GetFromTo`, `GetFraction`, `GetPercent`, `GetCurrency`, `GetDoubleCy`,
  `GetDictionaryItem`, `GetRow`, `GetHistory`, `GetArray`.
- Pomocnicze: `Exists(string)`, `Remove(string)`, `IsChanged`, `Definitions`.

**Snippet:**

```csharp
var handel = session.GetHandel();
var dok = handel.DokHandlowe.WgDaty[...];     // lub Get<DokumentHandlowy>(guid) w testach

// --- Odczyt nietypowany (object; null gdy brak wartości) ---
object centrum = dok["CentrumKosztow"];
if (centrum == null) { /* cecha bez wartości na tym dokumencie */ }

// --- Odczyt typowany przez Features ---
string opis    = dok.Features.GetString("OpisDodatkowy");
Date   dostawa = dok.Features.GetDate("DataDostawy");
bool   pilne   = dok.Features.GetBool("Pilne");

// pozycja:
PozycjaDokHandlowego poz = dok.Pozycje.Cast<PozycjaDokHandlowego>().First();
string nrSerii = poz.Features.GetString("NrSerii");

// --- Zapis cech: wymaga transakcji edycyjnej (jak każda modyfikacja Row) ---
using (var t = session.Logout(editMode: true))
{
    dok["OpisDodatkowy"] = "Pilna realizacja";   // String
    dok["Pilne"]         = true;                  // Bool
    dok["DataDostawy"]   = Date.Today.AddDays(3); // Date
    poz["NrSerii"]       = "S-2026-001";          // String na pozycji
    t.Commit();                                   // CommitUI() w workerze/extenderze
}
session.Save();

// Istnienie / usunięcie wartości:
bool ma = dok.Features.Exists("OpisDodatkowy");
using (var t = session.Logout(editMode: true))
{
    dok.Features.Remove("OpisDodatkowy");
    t.Commit();
}
session.Save();
```

**Pułapki:**
- Cecha musi mieć **wcześniej utworzoną definicję** (`FeatureDefinition`) zarejestrowaną dla
  właściwej tabeli (`DokHandlowe` dla dokumentu, `PozycjeDokHan` dla pozycji). Odwołanie do
  niezdefiniowanej cechy rzuca wyjątek — to nie to samo co pole natywne.
- Każdy **zapis** cechy to modyfikacja `Row` → musi być w transakcji (`session.Logout(true)` +
  `Commit`/`CommitUI`), potem `Save`. Odczyt transakcji nie wymaga.
- Indeksator nietypowany zwraca `object`; dla wartości pieniężnych/ilościowych zapisuj właściwy typ
  Soneta (`Currency`, `DoubleCy`, `Amount`, `Percent`, `Date`), nie surowy `decimal`/`double`/`string`.
- Cechy **algorytmiczne**: przypisanie wartości uruchamia algorytm definicji — efekty uboczne; część
  cech bywa read-only (`IsReadOnly(fd)` / tryb `SpecialEdit`) i edycja rzuci `AccessDeniedException`.
- W form.xml cechę adresuje się ścieżką `Features.Nazwa` (np. `{Features.NrSerii}`), także przez
  relację (`{Kontrahent.Features.Segment}`).
- `dok.Pozycje` to kolekcja pozycji dokumentu — iteruj po niej, nie ładuj całej tabeli
  `PozycjeDokHan`.

---

### HANDEL-W42 — Filtrowanie / wyszukiwanie dokumentów i partii po wartości cechy (serwerowo)

**Cel:** znaleźć dokumenty, pozycje, towary lub partie spełniające warunek na wartości cechy — z
filtrowaniem wykonywanym **po stronie SQL**, bez ładowania całej tabeli do pamięci.

**Warianty:**

| Wariant | Konstrukcja warunku | Uwaga |
|---|---|---|
| Równość wartości cechy | `new FieldCondition.Equal("Features.Nazwa", wartość)` | string-path, bo `Features.X` nie jest typowaną property |
| Większy / mniejszy | `FieldCondition.GreaterEqual / LessEqual("Features.Nazwa", v)` | dla cech liczbowych/dat |
| Łączenie warunków | `new RowCondition.And(...)` / `RowCondition.Or(...)` | składanie warunków serwerowych |
| Na indeksie tabeli | `tabela.WgKlucz[condition]` | filtr aplikowany na indeksie (SQL) |
| Na kolekcji `SubTable` | `dok.Pozycje[condition]` | filtr na pozycjach dokumentu |
| W widoku (UI) | `view.Condition &= new FieldCondition.Equal("Features.Nazwa", v)` | tylko kod UI / ViewInfo |

**Pola i typy:**
- `Soneta.Business.FieldCondition.Equal/GreaterEqual/LessEqual/...(string path, object value)` —
  ścieżka cechy to literał `"Features.NazwaDefinicji"`.
- `Soneta.Business.RowCondition.And` / `RowCondition.Or` — kompozycja warunków.
- Indeksy do filtrowania: `handel.DokHandlowe.WgDaty[condition]` (dokumenty),
  `towary.Towary.WgKodu[condition]` (towary), `magazyny.GrupyDostaw[...]` (partie).

**Snippet:**

```csharp
// 1) Towary po wartości cechy "Dystrybutor" = "Abc" (filtr serwerowy na indeksie)
var towary = session.GetTowary().Towary;
foreach (Towar t in towary.WgKodu[new FieldCondition.Equal("Features.Dystrybutor", "Abc")])
{
    // ... tylko towary o tej cesze; SQL filtruje po DataKey cechy
}

// 2) Dokumenty handlowe oznaczone cechą "Pilne" = true
var handel = session.GetHandel();
foreach (DokumentHandlowy d in
         handel.DokHandlowe.WgDaty[new FieldCondition.Equal("Features.Pilne", true)])
{
    // ...
}

// 3) Złożony warunek: cecha LUB cecha (OR) — wszystkie indeksowane serwerowo
var orWarunek = new RowCondition.Or(
    new FieldCondition.Equal("Features.Dystrybutor", "Abc"),
    new FieldCondition.Equal("Features.Dystrybutor", "Cba"));
var wybrane = towary.WgKodu[orWarunek].ToArray();

// 4) Filtr po cesze + zakres (np. cecha-data dostawy >= dziś) na dokumentach
var pilneNaDzis = new RowCondition.And(
    new FieldCondition.Equal("Features.Pilne", true),
    new FieldCondition.GreaterEqual("Features.DataDostawy", Date.Today));
foreach (DokumentHandlowy d in handel.DokHandlowe.WgDaty[pilneNaDzis]) { /* ... */ }

// 5) Pozycje konkretnego dokumentu po cesze (filtr na kolekcji SubTable)
foreach (PozycjaDokHandlowego p in
         dok.Pozycje[new FieldCondition.Equal("Features.NrSerii", "S-2026-001")])
{
    // ...
}
```

**Pułapki:**
- Cechy adresuj **string-pathem** `"Features.Nazwa"` w `FieldCondition` — `Features.X` nie jest
  typowaną property `Row`, więc nie da się jej użyć w wyrażeniu LINQ (`(Row r) => r.Features…`).
- Warunek aplikuj **na indeksie** (`WgKodu[...]`, `WgDaty[...]`) lub na kolekcji `SubTable`
  (`dok.Pozycje[...]`) — to wykonuje filtr w SQL. Nie iteruj całej tabeli z `if` w pamięci
  (`safe-code.md` §6).
- Wyszukiwanie korzysta z indeksowanego pola `DataKey` cechy; wartość w warunku podawaj w typie
  zgodnym z typem cechy (np. `bool` dla cechy Bool, `Date` dla cechy Date) — wartości są zapisane w
  ustalonym formacie tekstowym (patrz tabela typów w `references/features.md`).
- `view.Condition &= …` to mechanizm **UI** (ViewInfo/folder); w kodzie biznesowym używaj
  `SubTable[condition]`, nie obiektu `View`.
- `DokHandlowe` to tabela operacyjna guided — przy szerokich przekrojach dodatkowo zawężaj zakres
  czasowy (data dokumentu), nie tylko warunek na cesze.

---

