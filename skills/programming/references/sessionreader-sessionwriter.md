# SessionReader / SessionWriter — import i eksport XML z kodu

Programowa obsługa importu i eksportu danych XML platformy Soneta. **Strukturę i budowę
samego pliku XML** (tryby importu, identyfikacja rekordów, formaty wartości, atrybuty)
opisuje artykuł [import-export-xml](../../config/references/import-export-xml.md) — ten dokument dotyczy
wyłącznie warstwy kodu: klas `SessionReader` (import) i `SessionWriter` (eksport)
z przestrzeni `Soneta.Business`.

## Szybki start

```csharp
// IMPORT — do istniejącej sesji (bez Save; zapis kontroluje wywołujący)
using var xml = XmlReader.Create(stream);
var reader = new SessionReader(session);
reader.Read(xml);

// IMPORT — z Login (tworzy własne sesje i sam wykonuje Save)
var reader = new SessionReader(login);
reader.Read(xml);

// EKSPORT — rekord guidowany + datapack
using var xmlWriter = XmlWriter.Create(stream);
var writer = new SessionWriter();
writer.Add(kontrahent, ["Features"]);   // subexports: dodatkowe kolekcje/cechy
xmlWriter.WriteStartDocument();
writer.Write(xmlWriter);
```

## SessionReader (import)

### Konstruktory i cykl życia sesji

| Konstruktor | Zachowanie |
|---|---|
| `SessionReader(Session)` | import do przekazanej sesji; **bez** `Save()` — commit/zapis po stronie wywołującego |
| `SessionReader(Login)` | tworzy własną sesję (`CreateSessionX`), po wczytaniu wykonuje `Save()`; zdarzenie `SessionCreated` daje dostęp do tworzonych sesji |

`Read(XmlReader)` zwraca `true`, gdy import cokolwiek zmienił. Właściwość `Name` (domyślnie
`BaseURI` readera) trafia do komunikatów błędów. Podczas importu według rekordów sesja ma
ustawiane `InImport = true` — properties biznesowe są pomijane, a po zakończeniu wołane jest
`OnImported()` wierszy (szczegóły: [row-types.md](./row-types.md)).

### Obsługa błędów

- **Domyślnie** pierwszy błąd przerywa import wyjątkiem (najczęściej `InvalidXmlDataException`).
- `CollectExceptions = true` — każdy rekord główny wczytywany jest we własnej transakcji;
  błędy trafiają do listy (`GetThrowedExceptions()`), a import biegnie dalej.
- Zdarzenie `ReaderException` pozwala zareagować na błąd ustawienia właściwości/wiersza —
  odpowiedź przez `args.Response` (`ReaderResponse`):

| Odpowiedź | Skutek |
|---|---|
| `ThrowException` (domyślna) | rzuca wyjątek |
| `IgnoreProperty` | pomija pole |
| `IgnoreRow` | pomija cały wiersz |
| `Retry` (przez ustawienie `args.Value`) | ponawia z nową wartością |
| `RetryMapGuid` | jak `Retry` + trwały wpis mapowania GUID |
| `ReplaceData` | podmienia cały wczytywany wiersz na wskazany |

### Mapowanie GUID-ów (GuidMap)

Import może podmieniać GUID-y z pliku na GUID-y istniejące w bazie docelowej:

```csharp
var reader = new SessionReader(session) {
    GuidMapPolicy = SessionReader.GuidMapMode.All   // Rows | Values | Refs (domyślne)
};
reader.AddGuidMap(guidZPliku, guidWBazie, permanent: true);  // true = wpis trwały w bazie
```

- `AddGuidMap(from, to, permanent)` — `permanent: true` zapisuje mapowanie w tabeli
  `GuidMaps` (typ `SessionReader`), więc obowiązuje też w kolejnych importach; `false` —
  tylko w pamięci bieżącego importu.
- `GuidMapPolicy` wybiera, gdzie mapowanie działa: `Rows` (GUID-y rekordów głównych),
  `Refs` (pola referencyjne), `Values` (wartości, np. cechy referencyjne) — dowolne kombinacje.
- Inicjacja bazy z `*.dbinit.xml` używa `GuidMapMode.None` (GUID-y wczytywane dosłownie).
- Odpowiedź `RetryMapGuid` na `ReaderException` dokłada trwałe mapowanie w locie.

### Pozostałe ustawienia

- `RelationsImportMode` (`Replace`/`Update`) — domyślny tryb kolekcji, gdy plik nie określa
  `relationsimportmode`; obiekt może go też wymusić przez `Row.RelationsImportMode(...)`.
- `DbVersion` / `ProgramVersion` — sterują filtrem `dbversion` rekordów (mechanizm plików
  `*.dbinit.xml`); poza inicjacją bazy zostaw wartości domyślne (0 = bez filtrowania).

## SessionWriter (eksport)

Eksport działa według rekordów: `Add(GuidedRow)` dodaje rekord do kolejki, a `Write` zapisuje
go z całym datapackiem — relacje guidowane inner inline, outer jako kolejne elementy główne
([datapack-guidedrow.md](./datapack-guidedrow.md)).

```csharp
var writer = new SessionWriter {
    FromTo = FromTo.All,       // okres dla kolekcji datowanych
    BusinessMode = false       // standardowo eksport wg rekordów
};
writer.Add(dokument);                          // rekord + datapack
writer.Add(kontrahent, ["Features", "Rachunki"]); // + wskazane kolekcje/cechy (ścieżki z kropką)
writer.AddDeleted("Kontrahent", guid);         // znacznik <NazwaObiektu guid deleted="True"/>
bool cokolwiekZapisano = writer.Write(xmlWriter);
```

- **subexports** (`Add(row, string[])`) — nazwy kolekcji, relacji lub `Features` do dołączenia;
  zagnieżdżenia ścieżką z kropką (np. `"Pozycje.Zasoby"`); sama `"Features"` dołącza wszystkie
  cechy rekordu, `"Features.Nazwa"` — wybraną.
- `WriteWithLog(writer, name)` — jak `Write`, ale dodatkowo rejestruje operację w ChangeInfos
  (wg konfiguracji `ExportInfo`; zob. [changeinfos.md](./changeinfos.md)).
- Format wyniku: rekord = element o **nazwie obiektu biznesowego** (nazwa logiczna, l.poj. —
  nie tabela SQL) z atrybutami `id` (`Obiekt_ID`) i `guid`; referencje jako `Obiekt:GUID`
  lub identyfikator lokalny; wartości w kulturze invariant. Import rozwiązuje prefiks
  referencji zarówno po nazwie obiektu, jak i nazwie tabeli.

## Pliki `*.dbinit.xml` w projekcie dodatku (EmbeddedResource)

Pliki inicjujące bazę muszą być **osadzone w bibliotece** dodatku — platforma wyszukuje je
w zasobach assembly po rozszerzeniu `.dbinit.xml` i wczytuje przy tworzeniu bazy oraz przy
konwersji do nowszej wersji (kolejność wg `priority`, filtr rekordów wg `dbversion` —
struktura pliku: artykuł [import-export-xml](../../config/references/import-export-xml.md)).

- Projekt na **Soneta.Sdk** (zalecany — [new-addon-cli.md](./new-addon-cli.md)) osadza
  wszystkie pliki `**/*.dbinit.xml` jako `EmbeddedResource` **automatycznie** — wystarczy
  umieścić plik w projekcie (konwencja: podkatalog `DBInit/`).
- Projekt bez Soneta.Sdk wymaga wpisu ręcznego w `.csproj`:

```xml
<ItemGroup>
  <None Remove="DBInit\Slowniki.dbinit.xml" />
  <EmbeddedResource Include="DBInit\Slowniki.dbinit.xml" />
</ItemGroup>
```

Wczytanie przetestujesz bez instalowania dodatku: `dbmgr importxml <baza> <plik.xml>`
(→ [tools](../../tools/SKILL.md)) albo testem integracyjnym z `ImportBusinessXml` (niżej).

## Import w testach integracyjnych

`TestBase` udostępnia `ImportBusinessXml(nazwaZasobu)` — wczytuje plik osadzony jako
`EmbeddedResource` przez `SessionReader(Login)` i odświeża loginy. Typowe użycie: role,
definicje dokumentów, cechy i dane przygotowawcze testu. Szczegóły i konwencje:
[integration-tests.md](./integration-tests.md).

```csharp
public override void ClassSetup() {
    base.ClassSetup();
    ImportBusinessXml("DaneTestowe.xml");   // zasób osadzony w projekcie testowym
}
```

Wzorzec testu round-trip (eksport → import z mapowaniem GUID-ów):

```csharp
using var stream = new MemoryStream();
using (var xmlWriter = XmlWriter.Create(stream)) {
    var writer = new SessionWriter();
    writer.Add(kontrahent1, ["Features"]);
    xmlWriter.WriteStartDocument();
    writer.Write(xmlWriter);
}
stream.Seek(0, SeekOrigin.Begin);
using (var xmlReader = XmlReader.Create(stream)) {
    var reader = new SessionReader(Session);
    reader.AddGuidMap(guidStary, guidNowy, permanent: false);
    reader.Read(xmlReader);
}
```

## Checklista

- [ ] Właściwy konstruktor: `Session` (kontrola transakcji u wywołującego) vs `Login` (auto-Save).
- [ ] Import wielu niezależnych rekordów z tolerancją błędów → `CollectExceptions` + `GetThrowedExceptions()`.
- [ ] Przenoszenie między bazami z konfliktami GUID → `AddGuidMap` / `GuidMapPolicy`.
- [ ] Eksport z cechami/kolekcjami spoza datapacku → subexports w `Add`.
- [ ] Struktura pliku XML zgodna z artykułem [import-export-xml](../../config/references/import-export-xml.md).
- [ ] Weryfikacja: test integracyjny (`ImportBusinessXml`) lub żywa aplikacja ([tools](../../tools/SKILL.md), buscall).

## Powiązania

- Artykuł [import-export-xml](../../config/references/import-export-xml.md) — struktura i budowa pliku XML (tryby
  importu, identyfikacja, formaty wartości, atrybuty, pliki `*.dbinit.xml`).
- [datapack-guidedrow.md](./datapack-guidedrow.md) — rekordy guidowane i zakres datapacku.
- [row-types.md](./row-types.md) — `OnImporting`/`OnImported` w cyklu życia wiersza.
- [scan-props.md](./scan-props.md) — inwentaryzacja pól i właściwości do pliku importu.
- [integration-tests.md](./integration-tests.md) — `ImportBusinessXml` i testy na realnej bazie.
- [changeinfos.md](./changeinfos.md) — rejestrowanie operacji eksportu.
