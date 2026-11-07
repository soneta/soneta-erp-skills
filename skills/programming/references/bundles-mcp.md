# Pakiety funkcji (bundle) — metody MCP dla agentów AI

**Pakiet funkcji** (bundle) to klasa, której metody program udostępnia agentom AI jako
**narzędzia MCP**. Agent (serwerowy czat w programie albo agent lokalny, np. Claude Code) widzi
listę metod z opisami, wywołuje je z argumentami JSON i dostaje wynik JSON. Dodatek może
zarejestrować własne pakiety obok standardowych.

Dokument opisuje **pakiety systemowe**: metody działające na danych przez ORM (`Login` →
`Session` → `Row`), bez okien programu. Pakiety sterujące oknami i kontrolkami (nawigacja,
formularze, gridy) należą do platformy i nie są tu opisane.

## Najważniejsze zasady

- **Rejestracja:** `[assembly: Bundle<MojPakiet>(Caption = "…")]`. Metoda trafia do agenta, gdy
  jest publiczna i ma **`[BundleMethod]` oraz `[Description]`**. Bez opisu metoda jest pomijana.
- **Nazwa metody = nazwa narzędzia MCP**, unikalna w całym programie, także między dodatkami.
  Duplikat psuje rejestr wszystkich pakietów. Konwencja: `snake_case`, `czasownik_rzeczownik`.
- **Parametry tylko typów schematu:** `string`, `string[]`, `int`, `int[]`, `bool`, `enum`,
  `JsonElement[]` (i ich `Nullable`). Struktury złożone przyjmuj jako `JsonElement[]`.
- **Wynik:** rekord DTO serializowany do JSON (camelCase, bez pól `null`). Obiekt wskazuj
  identyfikatorem sesyjnym `login.ObjectIds().Mark(row)` (np. `dh7`), nigdy `Row.ID`.
- **Typy platformy** (`Date`, `FromTo`, `Time`, `Percent`, `Currency`, `Quantity`…) zamieniaj na
  tekst i parsuj zawsze z `CultureInfo.InvariantCulture` — nigdy samym `ToString()`.
- **Każde wywołanie = nowa instancja pakietu i własna sesja** (`using`). DTO buduj przed
  zamknięciem sesji. Zapis: `Logout(true)` → `Commit()` → `Save()`.
- **Prawa na wejściu metody:** `login.CurrentRole.BundleOperationalData` (albo
  `BundleConfigurationData`) i `.CheckReadOnly()` dla odczytu, `.CheckGranted()` dla zapisu.
- **Zapis wyłączony domyślnie:** parametr pakietu `[BundleSetting] bool AllowsWrite` + bramka
  `IsVisible_<metoda>()`. Zakres zapisu ograniczaj parametrami, np. limitem kwoty dokumentu.
- **Human in the loop:** platforma nie pyta operatora o zgodę na każde wywołanie. Zapisuj dane
  w stanie roboczym (bufor), który zatwierdza człowiek. Daj tryb próbny `testing`.
- **Operacje krytyczne loguj** w `session.ChangeInfos` typem `ChangeInfoType.Bundle`, z
  identyfikatorem agenta (`IBundleSessionContext.AgentGuid`).
- **Dane chronione (RODO):** getter pola wrażliwego sprawdza prawo i woła `ProtectedState.Mark()`.
  Agent dostawcy niezaufanego albo operator bez prawa „Dane chronione” dostaje `***`. Własny
  tekst wyniku licz przez `ProtectedState.ReadText(…)`.
- **Komunikaty błędów** pisz dla modelu: co poprawić i którą metodę wywołać. Bez danych osobowych.

## Spis treści

- [1. Rejestracja pakietu](#1-rejestracja-pakietu)
- [2. Deklaracja metody](#2-deklaracja-metody)
- [3. Parametry wejściowe](#3-parametry-wejściowe)
- [4. Wynik metody](#4-wynik-metody)
- [5. Sesja, odczyt i zapis](#5-sesja-odczyt-i-zapis)
- [6. Prawa `BundleRights`](#6-prawa-bundlerights)
- [7. Parametry pakietu i kontrola zapisu](#7-parametry-pakietu-i-kontrola-zapisu)
- [8. Human in the loop](#8-human-in-the-loop)
- [9. Dziennik zmian](#9-dziennik-zmian)
- [10. Dane chronione, RODO i zaufani dostawcy AI](#10-dane-chronione-rodo-i-zaufani-dostawcy-ai)
- [11. Przykład: dokumenty handlowe](#11-przykład-dokumenty-handlowe)
- [12. Testowanie](#12-testowanie)
- [13. Checklista](#13-checklista)

---

## 1. Rejestracja pakietu

```csharp
using Soneta.Business.Bundles;
using Soneta.Business.Licence;
using MojaFirma.Agent;              // atrybut stoi przed namespace — potrzebny using klasy pakietu

[assembly: Bundle<DokumentyHandloweBundle>(
    Caption = "Dokumenty handlowe",
    Contexts = [LicencjeModułu.HAN])]

namespace MojaFirma.Agent;

internal sealed class DokumentyHandloweBundle(Login login, IBundleSessionContext bundleContext) {
    // metody [BundleMethod]
}
```

| Właściwość atrybutu | Znaczenie |
|---|---|
| `Caption` | Nazwa pakietu dla operatora (definicja agenta, role). |
| `Contexts` | Licencje modułu wymagane do użycia pakietu, np. `[LicencjeModułu.HAN]`. Pakiet bez licencji jest niedostępny dla operatora typu WebUser (pulpity). |
| `Target` | Domyślnie `BundleTargets.System` — właściwe dla dodatków. `UI`/`Frame` dotyczą okien i aplikacji klienckiej. |
| `IsRightsSource` | Domyślnie `true`: pakiet dostaje własne prawo w rolach (węzeł „Bundle / Pakiety funkcji”). `false` = dziedziczy prawo węzła Bundle. |
| `Priority` | Kolejność pakietów na liście narzędzi (domyślnie 100). |

**Klucz prawa pakietu to pełna nazwa typu klasy.** Zmiana nazwy klasy albo przestrzeni nazw
gubi prawa nadane w rolach i narzędzia zapisane w definicjach agentów.

**Konstruktor.** Platforma tworzy instancję **osobno dla każdego wywołania** i wstrzykuje
argumenty z kontekstu wywołania (jak przy workerach, zob. [context.md](context.md)):

| Argument | Zawartość |
|---|---|
| `Login` | Zalogowanie operatora, w imieniu którego działa agent. Źródło sesji. |
| `IBundleSessionContext` | `AgentGuid` (agent wywołujący), `Owner` (okno albo `Login`), `Context`, `GetBundle<T>()` — instancja innego pakietu z parametrami tego agenta (`null`, gdy agent go nie ma). |

Klasa może być `internal` — platforma odczytuje ją refleksją.

## 2. Deklaracja metody

```csharp
[BundleMethod(Title = "Wyszukaj dokumenty handlowe", ReadOnlyHint = true, IdempotentHint = true)]
[Description("""
    Wyszukuje dokumenty handlowe. Zwraca `documentsCsv` z nagłówkiem
    `id|numer|data|kontrahent|brutto|stan` oraz `count` i `truncated`.
    `id` przekaż do `get_dokument_handlowy` albo `save_dokument_handlowy`.
    """)]
public SearchResponse search_dokumenty_handlowe(
    [Description("Symbol definicji dokumentu, np. `FV`, `ZO`. Puste = wszystkie.")] string? definicja = null,
    [Description("Maksymalna liczba dokumentów (domyślnie 15; 0 = bez ograniczenia).")] int limit = 15) { … }
```

**Atrybuty:**

| Atrybut | Przestrzeń | Rola |
|---|---|---|
| `[BundleMethod]` | `Soneta.Business.Bundles` | Oznacza metodę pakietu; niesie tytuł i podpowiedzi MCP. |
| `[Description]` | `System.ComponentModel` | Opis metody i parametru dla modelu. **Bez opisu metody narzędzie nie powstaje.** |
| `[Required]` | `Soneta.Business` | Parametr wymagany. Nie `System.ComponentModel.DataAnnotations.Required` — tamten nie działa. |

**Właściwości `[BundleMethod]`** trafiają do adnotacji narzędzia MCP (`ToolAnnotations`). Klient
MCP może na ich podstawie pytać użytkownika o zgodę. Program ich nie egzekwuje — ustawiaj je zgodnie
z prawdą.

| Właściwość | Kiedy `true` |
|---|---|
| `Title` | Krótki tytuł po polsku (lista narzędzi, definicja agenta). |
| `ReadOnlyHint` | Metoda tylko czyta. |
| `IdempotentHint` | Powtórzenie z tymi samymi argumentami daje ten sam efekt. Listy stronicowane — `false`. |
| `DestructiveHint` | Metoda zmienia albo usuwa dane. |
| `OpenWorldHint` | Metoda sięga poza program (zewnętrzne usługi). |
| `LongRunning` | Metoda czeka na reakcję operatora w oknie programu (mechanizm pakietów okien). Pakiet systemowy odpowiada od razu — zostaw `false`. |

**Opis metody to instrukcja dla modelu.** Pisz w nim: co robi metoda, kształt wyniku (nazwy pól,
nagłówek CSV), skąd wziąć argumenty (którą metodą) i ograniczenia (bufor, limit). Odwołuj się do
innych narzędzi w backtickach. Opis parametru podaje format i przykład.

**Konwencje nazw:** metoda `snake_case` (`search_…`, `get_…`, `save_…`, `delete_…`), parametry
camelCase. Unikalność w programie zapewnij prefiksem domeny lub dodatku. Wartości domyślne C#
oznaczają parametr opcjonalny.

## 3. Parametry wejściowe

### Typy proste

| Typ C# | Typ w schemacie | Uwagi |
|---|---|---|
| `string`, `string?` | `string` | Liczba w JSON też przejdzie (np. identyfikator). |
| `int`, `int?` | `integer` | Przyjmuje też tekst z liczbą. |
| `bool`, `bool?` | `boolean` | |
| `enum` | `string` | Nazwa wartości bez rozróżniania wielkości liter. Błąd zwraca listę dozwolonych. Wartości wymień w `[Description]`. |
| `string[]`, `int[]` | `array` | |
| `JsonElement[]` | `array` (elementy dowolne) | **Struktury złożone** — patrz niżej. |

Inne typy (`decimal`, `double`, `DateTime`, `Guid`, pojedynczy `JsonElement`) nie mają odwzorowania
w schemacie narzędzia. Kwotę i datę przyjmuj jako `string` i parsuj **zawsze** kulturą niezmienną
(`Date.Parse(s, CultureInfo.InvariantCulture)`, `FromTo.Parse("2026-01-01..2026-06-30", …)`) albo
umieść w obiekcie `JsonElement`.

### Parametry złożone — `JsonElement[]`

Tablica obiektów JSON to sposób na dane zagnieżdżone, np. pozycje dokumentu. Kształt obiektu
opisz w `[Description]` metody z przykładem. Każdy element zdeserializuj do prywatnego rekordu
i zwaliduj **przed** otwarciem sesji:

```csharp
private static readonly JsonSerializerOptions ArgOptions = new() { PropertyNameCaseInsensitive = true };

private sealed record PozycjaArg(int? Lp, string? Towar, double? Ilosc, decimal? Cena, bool Usun = false);

private static PozycjaArg ReadPozycja(JsonElement element) {
    if (element.ValueKind != JsonValueKind.Object)
        throw new ArgumentException("Pozycja musi być obiektem JSON, np. {\"towar\":\"BIKINI\",\"ilosc\":2}.".Translate());

    var arg = element.Deserialize<PozycjaArg>(ArgOptions)!;
    if (arg.Lp == null && arg.Towar.IsNullOrEmpty())
        throw new ArgumentException("Nowa pozycja wymaga `towar`.".Translate());
    return arg;
}
```

Wywołanie przez agenta:

```json
{
  "definicja": "FV",
  "kontrahent": "Abc",
  "pozycje": [
    { "towar": "BIKINI", "ilosc": 2, "cena": 49.90 },
    { "lp": 1, "usun": true }
  ]
}
```

Wzorzec „operacja na kolekcji”: element bez klucza (`lp`) dodaje pozycję, z kluczem — zmienia,
z kluczem i `usun: true` — usuwa. Jedna metoda obsługuje wtedy tworzenie i aktualizację.

### Parametry `klucz=wartość` — `string[]`

Prostsze struktury (filtry, agregaty) przyjmuj jako `string[]` wpisów `"pole=wartość"`, np.
`["Kod=*ABC", "Data=2026-01-01..2026-06-30"]`. Rozdziel na pierwszym `=`.

### Wskazanie obiektu — identyfikator sesyjny

Obiekty przekazuje się identyfikatorem otrzymanym wcześniej w wyniku (np. `dh7`: alias tabeli +
licznik). Rejestr identyfikatorów (`login.ObjectIds()`, usługa zakresu `Login`) jest strażnikiem:
rozwiąże tylko obiekt, który agent już dostał.

```csharp
// identyfikator wymagany — tabela wynika z identyfikatora
var (table, rowId) = login.ObjectIds().ResolveObject(id);
var dok = session.Tables[table].Rows.FindByID(rowId, false) as DokumentHandlowy
          ?? throw new ArgumentException("`{0}` nie wskazuje dokumentu handlowego.".TranslateFormat(id));

// identyfikator albo kod — TryResolve nie rzuca wyjątkiem
if (login.ObjectIds().TryResolve(value, out var t, out int rid))
    return session.Tables[t!].Rows.FindByID(rid, false) as Towar;
return session.GetTowary().Towary.WgKodu[value];
```

`ResolveObjects(string[] ids)` rozwiązuje wiele identyfikatorów jednej tabeli (powtórzenia
pomija, mieszanie tabel odrzuca). Identyfikatory tracą ważność z wylogowaniem.

### Pliki

Treść binarną przyjmuj jako `string` Base64 (`contentBase64`). Aplikacja kliencka może sama wczytać
plik wskazany parametrem `…FileName` i wypełnić nim parametr treści — serwer nie sięga do plików.

## 4. Wynik metody

Metoda zwraca rekord DTO. Platforma serializuje go do JSON z nazwami **camelCase** i **pomija pola
`null`** (puste ostrzeżenia ustaw na `null`, nie na pustą tablicę).

```csharp
[BundleCsv(nameof(Documents))]
internal sealed record SearchResponse(DokumentInfo[] Documents, int Count, bool Truncated) {
    public string? Warning { get; init; }
}

internal sealed record DokumentInfo(string Id, string Numer, string Data, string? Kontrahent, decimal Brutto, string Stan);
```

| Element | Zasada |
|---|---|
| `[BundleCsv(nameof(Tablica))]` na typie | Tablica obiektów idzie jako jeden tekst CSV: pole `<nazwa>Csv`, separator `\|`, nagłówek z nazw właściwości elementu. Oszczędza tokeny. Nagłówek opisz w `[Description]` metody. |
| `BundleLimit.Apply(items, limit)` | Zwraca `(Items, Count, Truncated)`. Konwencja: parametr `limit` (domyślnie 15, `0` = bez ograniczenia), pola wyniku `count` i `truncated`. |
| Twardy limit odczytu | Z tabel operacyjnych czytaj najwyżej N+1 wierszy (np. 500) i dodaj ostrzeżenie „zawęź okres”. Nie iteruj całej tabeli. |
| Identyfikatory | `login.ObjectIds().Mark(row)` — rejestruje obiekt i zwraca `id`. Po `Save()` nowego wiersza (prawdziwe `ID`). |
| Typy platformy | Tekst w kulturze niezmiennej: `data.ToString(null, CultureInfo.InvariantCulture)` → `2026-11-07`; tak samo `FromTo`, `Time`, `Percent`, `Currency`. `ToString()` bez kultury zależy od języka operatora i serwera — model dostaje raz `07.11.2026`, raz `11/7/2026`. |
| Enumy | Nazwa wartości: `Stan.ToString()`. |
| Ostrzeżenia | Pole `Warning`/`Warnings` na problemy niekrytyczne. |

**Błędy** zgłaszaj wyjątkiem. Agent dostaje `{ "error": "<komunikat>" }`.

| Wyjątek | Kiedy |
|---|---|
| `ArgumentException` | Złe argumenty (format, brak obiektu). |
| `InvalidOperationException` | Operacja niedozwolona w tym stanie (dokument zatwierdzony, limit przekroczony). |
| `AccessException` | Brak prawa (rzuca `CheckReadOnly`/`CheckGranted`). |

Komunikat pisz dla modelu: co poprawić, skąd wziąć wartość, np. „Podaj `id` zwrócone przez
`search_dokumenty_handlowe`”. Nie wstawiaj do komunikatu `row.ToString()` ani danych osobowych.

## 5. Sesja, odczyt i zapis

Pakiet nie dostaje sesji — tworzy ją z `Login` na czas jednego wywołania:

```csharp
// odczyt
using var session = login.CreateSession(readOnly: true, config: false, nameof(DokumentyHandloweBundle));
return ToResponse(FindDokument(session, id));      // DTO zbudowane przed Dispose sesji

// zapis — jedno wywołanie = jedna transakcja
using var session = login.CreateSession(readOnly: false, config: false, nameof(DokumentyHandloweBundle));
using (var t = session.Logout(editMode: true)) {
    // zmiany, walidacja, limity
    if (testing) return Preview(dok);              // wyjście bez Commit = rollback
    session.ChangeInfos.Add(dok, ChangeInfoType.Bundle, info, data);
    t.Commit();
}
session.Save();                                    // weryfikatory, numeracja, konflikty
```

- `config: true` dla tabel konfiguracyjnych (`tableInfo.IsConfig`).
- Wyjątek przed `Commit()` wycofuje wszystkie zmiany wywołania. Limity i walidację sprawdzaj
  przed `Commit()`.
- Weryfikatory (`Save()` z błędem) zwracają agentowi komunikat jak każdy wyjątek
  ([verifiers.md](verifiers.md)).
- `Save()` uruchamia pełną logikę zapisu — także w buforze. Przykład: pozycja faktury w buforze
  rezerwuje towar, więc brak stanu kończy się błędem weryfikatora stanu ujemnego.
- Pozostałe zasady sesji: [session-login.md](session-login.md), [safe-code.md](safe-code.md).

## 6. Prawa `BundleRights`

Agent działa w imieniu operatora, więc obowiązują wszystkie jego prawa do danych. Dodatkowo
węzeł praw **Bundle** zawiera prawa proste (`SimpleRight`, zob.
[rights-source.md](rights-source.md#prawa-proste-simpleright)):

| Stała | Prawo w roli | Znaczenie |
|---|---|---|
| `BundleRights.LocalAgent` | „Lokalny agent AI” | Obsługa programu przez agentów lokalnych (sprawdza platforma). |
| `BundleRights.ConfigurationData` | „Dane konfiguracyjne” | Dostęp pakietów do danych konfiguracyjnych. |
| `BundleRights.OperationalData` | „Dane operacyjne” | Dostęp pakietów do danych operacyjnych. |
| `BundleRights.ProtectedData` | „Dane chronione” | Agent zaufanego dostawcy czyta dane chronione jawnie ([§10](#10-dane-chronione-rodo-i-zaufani-dostawcy-ai)). |

Oraz prawo do każdego pakietu (węzeł „Pakiety funkcji”, gdy `IsRightsSource = true`) — platforma
sprawdza je razem z licencją przed wywołaniem.

Rozszerzenia `Role` i `AccessRights`:

```csharp
var right = tableInfo.IsConfig
    ? login.CurrentRole.BundleConfigurationData
    : login.CurrentRole.BundleOperationalData;

right.CheckReadOnly();   // odczyt: AccessException przy Denied
right.CheckGranted();    // zapis: AccessException, gdy prawo nie jest pełne
```

Sprawdzaj prawo **na początku metody, przed utworzeniem sesji** — w każdej metodzie, bo agent może
wywołać metodę z pominięciem listy narzędzi.

**Licencja AI.** Agenci wymagają licencji modułu AI (`AiAgents.IsLicensed(login)`, enova365, poza
wersją okienkową). Licencję modułu dziedzinowego wskazuje `Contexts` atrybutu pakietu.

## 7. Parametry pakietu i kontrola zapisu

Parametr pakietu (`[BundleSetting]`) ustawia **operator** w definicji agenta (zakładka „Parametry”
narzędzia pakietu). Argumentami metod steruje model, parametrami — człowiek. Parametry przycinają
możliwości pakietu albo je rozszerzają.

```csharp
[BundleSetting, Caption("Zapis dokumentów")]
[Description("Agent może tworzyć i zmieniać dokumenty w buforze. Bez tego parametru metoda zapisu jest niedostępna.")]
public bool AllowsWrite { get; set; }

[BundleSetting, Caption("Limit wartości brutto")]
[Description("Najwyższa wartość brutto dokumentu zapisywanego przez agenta. 0 oznacza brak limitu.")]
public decimal MaxBrutto { get; set; } = 10_000m;

public bool IsVisible_save_dokument_handlowy() => AllowsWrite;   // bramka metody save_dokument_handlowy
```

**Parametr:**
- publiczna property z getterem i setterem; typ: `bool`, `int`, `long`, `decimal`, `double`,
  `string`, `DateTime`, `Guid`, enum, tablice tych typów (inny typ jest pomijany);
- `[Caption]` (`Soneta.Types`) — etykieta pola, `[Description]` — co traci agent bez parametru;
- wartość domyślna z inicjatora = ustawienie bezpieczne (zapis wyłączony, limit ustawiony);
- getter/setter nie sięga po argumenty konstruktora — formularz parametrów tworzy instancję bez
  wstrzykiwania zależności;
- zapisywane są tylko wartości różne od domyślnych (JSON w narzędziu agenta). **Zmiana nazwy
  property gubi zapisaną wartość.**

**Bramka `IsVisible_<nazwa metody>()`** — publiczna, bezparametrowa, zwraca `bool`. Ukrywa metodę
na liście narzędzi i blokuje jej wywołanie. Czyta wyłącznie parametry pakietu i stan statyczny —
przy budowie listy działa na instancji bez `Login`.

**Wzorce kontroli zapisu:**

| Wzorzec | Realizacja |
|---|---|
| Zapis wyłączony domyślnie | `bool AllowsWrite` + bramka metody zapisu. |
| Limit kwoty | `decimal MaxBrutto`; po przeliczeniu dokumentu, przed `Commit()`: przekroczenie → wyjątek, rollback. |
| Limit liczby obiektów | `int MaxPozycji`, `int MaxDokumentow` na wywołanie. |
| Tylko stan roboczy | Agent tworzy i zmienia dokumenty w buforze; zatwierdzenie zostaje dla operatora ([§8](#8-human-in-the-loop)). |
| Zawężenie zakresu | `string[] Definicje` — dozwolone symbole definicji, magazyny itp. |
| Osobne uprawnienie operacji | Osobne parametry dla eksportu, importu, usuwania. |

Operator dodatkowo zawęża listę metod pakietu w definicji agenta (dozwolone metody).

## 8. Human in the loop

**Platforma nie zatwierdza wywołań.** Czat serwerowy wykonuje każdą dozwoloną metodę od razu.
Agent lokalny może pytać użytkownika o zgodę na podstawie podpowiedzi `DestructiveHint`/
`ReadOnlyHint`, ale to ustawienie klienta MCP, nie programu. Kontrolę człowieka projektuje pakiet:

1. **Stan roboczy + zatwierdzenie przez operatora** (najmocniejszy). Agent zapisuje dokument
   w buforze; operator przegląda i zatwierdza go w programie. Metoda odrzuca zmianę dokumentu
   zatwierdzonego. Nie dawaj agentowi metody zatwierdzania, chyba że chroni ją osobny parametr.
2. **Tryb próbny `testing`.** Metoda wykonuje całą operację i zwraca wynik (wartości, numery,
   błędy weryfikacji) bez `Commit()`. Agent pokazuje propozycję użytkownikowi i zapisuje po jego
   akceptacji w rozmowie.
3. **Parametry operatora** ([§7](#7-parametry-pakietu-i-kontrola-zapisu)): zapis wyłączony
   domyślnie, limity kwot i liczby obiektów. Powyżej limitu metoda odmawia — operację wykonuje
   człowiek.
4. **Ślad w dzienniku zmian** ([§9](#9-dziennik-zmian)) — kontrola po fakcie.

Platformowe pakiety okien mają własne potwierdzanie (np. zapis formularza czeka na decyzję
operatora w oknie programu; parametr „Samodzielny zapis formularza” je wyłącza). Ten mechanizm
nie jest API dla pakietów systemowych.

## 9. Dziennik zmian

Zmiana danych wykonana przez agenta zapisuje się na operatora, bez znacznika AI. Wywołania pakietów
systemowych przez czat serwerowy nie trafiają do dziennika zmian automatycznie. **Operację
krytyczną (zapis, usunięcie, zmiana stanu, import) pakiet loguje sam**, w tej samej transakcji:

```csharp
session.ChangeInfos.Add(dok, ChangeInfoType.Bundle,
    "Agent AI {0}: save_dokument_handlowy".TranslateFormat(bundleContext.AgentGuid),   // Info ≤ 255 znaków
    JsonSerializer.Serialize(new { id, definicja, kontrahent, pozycje }));             // Data — memo
```

- `ChangeInfoType.Bundle` — typ wpisu dla operacji pakietów funkcji.
- `Info`: identyfikator agenta i nazwa metody (pole obcinane do 255 znaków).
- `Data`: argumenty wywołania (JSON). Nie wpisuj danych chronionych, których operacja nie wymaga.
- Wpis trafia do bazy przy `Save()`; rollback usuwa go razem ze zmianą.
- Szczegóły dziennika: [changeinfos.md](changeinfos.md).

## 10. Dane chronione, RODO i zaufani dostawcy AI

**Dane chronione** to wartości objęte prawem prostym: dane osobowe (imię, nazwisko, PESEL, data
i miejsce urodzenia, adresy), wynagrodzenia, zaszeregowanie. Model AI działa u dostawcy zewnętrznego,
więc ich przekazanie wymaga zgody administratora danych.

**Reguła:** agent czyta dane chronione jawnie tylko wtedy, gdy **dostawca AI jest zaufany**
(pole „Zaufany” w konfiguracji dostawcy AI; jego edycja wymaga osobnego prawa) **i** rola
operatora ma prawo **Bundle / Dane chronione** = pełne. W pozostałych przypadkach agent dostaje
`***`, a wynik oznacza pole jako zaciemnione. Zasada dotyczy też agentów lokalnych — o zaufaniu
decyduje rekord dostawcy, którego używa agent. W nowej bazie standardowi dostawcy nie są zaufani.

### Oznaczanie pola chronionego w dodatku

Getter pola wrażliwego najpierw sprawdza prawo (brak → `AccessDeniedException`), potem woła
`ProtectedState.Mark()`:

```csharp
public new string NumerDowodu {
    get {
        if (Session.Login.CurrentRole?[typeof(MojeRights), MojeRights.DaneOsobowe] == AccessRights.Denied)
            throw new AccessDeniedException(…);
        ProtectedState.Mark();          // odczyt danej chronionej — maskowana dla agenta bez zgody
        return base.NumerDowodu;
    }
}
```

`Mark()` nie blokuje odczytu (blokuje prawo). Zgłasza tylko, że obliczona wartość jest wrażliwa.
Standardowe pakiety odczytu (listy, wartości pól, wyszukiwanie) maskują taką wartość
automatycznie — także pola dodatku i wartości kalkulowane, które pole chronione czytają.

### Maskowanie w wyniku własnego pakietu

Każdą wartość, która może zawierać dane chronione (także `row.ToString()`, nazwy, opisy), oblicz
przez `ProtectedState.ReadText` (tekst) albo `ProtectedState.Read` (dowolna wartość):

```csharp
// tekst do DTO — "***", gdy obliczenie odczytało daną chronioną
Kontrahent: ProtectedState.ReadText(() => d.Kontrahent?.Nazwa)

// dowolna wartość + rozpoznanie zaciemnienia (np. flaga w wyniku)
object? value = ProtectedState.Read(() => umowa.Wynagrodzenie);   // pole chronione dodatku
bool zaciemniona = ReferenceEquals(value, ProtectedState.ObfuscatedValue);
```

| Element `ProtectedState` | Znaczenie |
|---|---|
| `ReadText(Func<string?>)` | Tekst albo w całości `Mask`. Najczęstszy przypadek w DTO. |
| `Read(Func<object?>)` | Oblicza wartość. W zakresie ograniczonym, gdy obliczenie odczytało daną chronioną (`Mark()`), zwraca `ObfuscatedValue`. |
| `ObfuscatedValue` | Wartość zaciemniona; `ToString()` = `Mask`. |
| `Mask` | `"***"`. |
| `IsRestricted` | `true` w wywołaniu agenta bez zgody na dane chronione. Poza wywołaniem agenta (czynność operatora, test) `false` — `Read` zwraca wartość jawną. |
| `StartListening()` / `Scope.WasRead` | Nasłuch odczytów — do własnych scenariuszy, gdy `Read` nie wystarcza. |

`Read` nie zastępuje prawa: brak prawa prostego nadal kończy się wyjątkiem w getterze.

> **Uwaga — cache odczytu wyłącza maskowanie.** Mechanizm wykrywa daną chronioną tylko wtedy, gdy
> getter z `Mark()` **wykona się w trakcie** `ReadText`/`Read`. Wartość wzięta z cache (pole, słownik, `Lazy`,
> wynik policzony wcześniej) nie zgłasza odczytu i trafia do agenta jawnie.
>
> - **Licz wartość wewnątrz lambdy.** `var n = d.Kontrahent?.Nazwa; ProtectedState.ReadText(() => n)`
>   nie maskuje — getter wykonał się przed `ReadText`.
> - **Property z własnym cache**, które wylicza wynik z danych chronionych (np.
>   `opis ??= $"{Imie} {Nazwisko}"`), woła `ProtectedState.Mark()` także przy trafieniu w cache —
>   albo nie cache'uje.
> - **Cache pakietu współdzielony między wywołaniami** (statyczny, na `Login`, na liście) nie
>   przechowuje wartości gotowych do wysłania. Wartość jawna z wywołania agenta zaufanego,
>   podana później agentowi niezaufanemu, to wyciek danych; wartość `***` w odwrotnej sytuacji —
>   błąd. Cache'uj obiekty (`Row`, identyfikatory) i licz wartości przez `Read` przy każdym
>   wywołaniu — albo trzymaj w cache znacznik „chroniona” razem z wartością.
> - **Wartości policzone przez warstwę prezentacji** (accessory pól formularza i listy) są
>   cache'owane. Platforma odświeża je w wywołaniu agenta; kod pakietu czyta dane z obiektów
>   biznesowych, nie z cache prezentacji.

### RODO — zasady dla pakietu

- **Minimalizacja:** zwracaj tylko pola potrzebne do zadania; domyślny `limit` mały (15).
- **Komunikaty błędów** bez danych osobowych i bez `row.ToString()` obiektów osób.
- **Dziennik zmian:** w `Data` nie zapisuj danych chronionych, których operacja nie wymaga.
- **Nie omijaj praw:** nie czytaj pól chronionych z pominięciem getterów (np. bezpośrednio z SQL).
- Pole „Zaufany” dostawcy i prawo „Dane chronione” ustawia administrator po ocenie dostawcy.

## 11. Przykład: dokumenty handlowe

Kompletny pakiet (wyszukiwanie, odczyt dokumentu z pozycjami, zapis z pozycjami jako
`JsonElement[]`, limit brutto, bufor, tryb próbny, dziennik zmian, dane chronione) — sprawdzony
testem na bazie Demo: [bundles-mcp-przyklad.md](bundles-mcp-przyklad.md).

## 12. Testowanie

Metody pakietu to zwykłe metody C# — test integracyjny ([integration-tests.md](integration-tests.md))
tworzy instancję z `Login` sesji testowej i atrapą `IBundleSessionContext`, ustawia parametry
inicjatorem i woła metody wprost:

```csharp
private sealed class FakeBundleContext(Login login) : IBundleSessionContext {
    public object Owner => login;
    public Context Context => throw new NotSupportedException();
    public Guid AgentGuid => TestAgentGuid;
    public object? GetBundle(Type bundleType) => null;
}

var bundle = new DokumentyHandloweBundle(Session.Login, new FakeBundleContext(Session.Login)) {
    AllowsWrite = true, MaxBrutto = 100m,
};
var pozycja = JsonDocument.Parse("""{"towar":"BIKINI","ilosc":10,"cena":50}""").RootElement.Clone();

var act = () => bundle.save_dokument_handlowy(definicja: "FV", kontrahent: "Abc", pozycje: [pozycja]);
act.Should().Throw<InvalidOperationException>().WithMessage("*przekracza limit*");
```

Sprawdź testami: bramki (`IsVisible_…` przy parametrach domyślnych), limity, tryb `testing`
(brak zapisu), wpis `ChangeInfoType.Bundle`, odrzucenie dokumentu zatwierdzonego, odczyt nowego
`id`. Na żywej aplikacji listę i wywołania metod sprawdzisz klientem MCP `callmcp`
(`methods.list`) — skill `/soneta:tools`, [buscall-live-testing.md](buscall-live-testing.md).

## 13. Checklista

**Rejestracja i kontrakt**
- [ ] `[assembly: Bundle<T>(Caption = …)]`, `Contexts` z licencją modułu, `using` przestrzeni klasy.
- [ ] Każda metoda: `[BundleMethod(Title, hinty zgodne z działaniem)]` + `[Description]`.
- [ ] Nazwa metody unikalna w programie (`snake_case`, prefiks domeny).
- [ ] Parametry tylko typów schematu; złożone jako `JsonElement[]` z przykładem w opisie.
- [ ] `[Required]` z `Soneta.Business` dla parametrów bez wartości domyślnej.
- [ ] Opis metody podaje kształt wyniku, nagłówki CSV i metody, z których pochodzą argumenty.

**Wynik**
- [ ] DTO jako rekordy; tablice wierszy `[BundleCsv]`; `limit` + `count`/`truncated`.
- [ ] Obiekty jako `login.ObjectIds().Mark(row)`; wejście przez `ResolveObject`/`TryResolve`.
- [ ] `Date`, `FromTo`, `Time`, `Percent`, `Currency` — `ToString`/`Parse` z `CultureInfo.InvariantCulture`.
- [ ] Twardy limit odczytu z tabel operacyjnych; filtr serwerowy (`RowCondition`).
- [ ] Błędy wyjątkiem z komunikatem dla modelu, bez danych osobowych.

**Bezpieczeństwo zapisu**
- [ ] Prawo `BundleOperationalData`/`BundleConfigurationData` sprawdzone na początku każdej metody.
- [ ] Sesja w `using`; zapis `Logout` → walidacja i limity → `Commit` → `Save`.
- [ ] Zapis wyłączony domyślnie (`[BundleSetting]` + `IsVisible_…`).
- [ ] Limity zakresu zapisu (kwota, liczba obiektów, dozwolone definicje) z parametrów pakietu.
- [ ] Agent zapisuje stan roboczy; zatwierdzenie zostaje dla operatora.
- [ ] Tryb `testing` dla metod zapisu.
- [ ] Wpis `ChangeInfoType.Bundle` z `AgentGuid` dla każdej operacji krytycznej.

**Dane chronione**
- [ ] Gettery pól wrażliwych dodatku: prawo → `ProtectedState.Mark()`.
- [ ] Wartości opisowe w wyniku (nazwy, `ToString()`) liczone przez `ProtectedState.ReadText(…)`.
- [ ] Wartość liczona wewnątrz lambdy `ReadText`/`Read`; property z cache woła `Mark()` przy trafieniu; cache pakietu bez gotowych wartości.
- [ ] Minimalny zakres danych w wyniku i w `Data` dziennika zmian.

## Powiązane

- [worker-extender.md](worker-extender.md) — czynności w menu (alternatywa dla operatora).
- [action-result.md](action-result.md) — `PromptResult`: czynność zlecająca zadanie agentowi AI.
- [rights-source.md](rights-source.md) — prawa proste `SimpleRight`, źródła praw.
- [changeinfos.md](changeinfos.md) — dziennik zmian.
- [assembly-attributes.md](assembly-attributes.md) — odczyt atrybutów `[Bundle]` z załadowanych modułów.
- `/soneta:config` → `references/mcp-import-export.md` — standardowy pakiet importu i eksportu (tryb `testing`).
- `/soneta:tools` — `callmcp`, `buscall`: wywoływanie metod pakietów z wiersza poleceń.
- `/soneta:addon-planning` — planowanie pakietów w projekcie dodatku: sekcje 1.13, 2.10, 3.17.
