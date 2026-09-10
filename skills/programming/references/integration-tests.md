# Testy integracyjne — klasa bazowa `TestBase`

Testy logiki biznesowej Soneta pisze się na **prawdziwej bazie danych**, nie na mockach.
Klasa bazowa `Soneta.Test.TestBase` (projekt `Soneta.Test`, referencyjny przykład użycia:
`Soneta.Business.Test`) dostarcza gotowe, zainicjowane obiekty sesji podłączone do bazy
testowej i **automatycznie wycofuje wszystkie zmiany** po każdym teście. Test nie tworzy bazy,
nie otwiera loginu i nie sprząta po sobie — skupia się wyłącznie na scenariuszu biznesowym.

> ⚠ **Testujesz WŁASNY dodatek?** Bez dwóch kroków testy padną na starcie:
> **(1)** rejestracja assembly dodatku w `[SetUpFixture]` (inaczej `InvalidCastException`
> przy `session.GetMojDodatek()`), **(2)** rola z prawami do obiektów dodatku (inaczej
> `AccessWriteDeniedException`). Szczegóły → [Testy własnego dodatku](#testy-własnego-dodatku-testbase).

## Co robi TestBase za Ciebie

- **Zarządza bazą testową** — baza jest przygotowana raz (inicjalizator), test dostaje gotowe
  `Session` / `ConfigSession` / `Login` / `Database`.
- **Dwupoziomowa transakcja bazodanowa** (poziom klasy + poziom pojedynczego testu):
  - dane wpisane w `ClassSetup` żyją przez wszystkie testy klasy i są wycofywane po ostatnim,
  - dane wpisane w teście (lub `TestSetup`) są wycofywane po każdym pojedynczym teście.

  Sekwencja: `baza po inicjalizatorze → ClassSetup → [TestSetup → test → TestTearDown]* → ClassTearDown`,
  a rollback zdejmuje warstwy w odwrotnej kolejności. Dzięki temu testy są od siebie niezależne
  i powtarzalne bez ręcznego sprzątania.

## Minimalny test

```csharp
using AwesomeAssertions;
using NUnit.Framework;
using Soneta.Test;
using Soneta.Towary;

namespace Soneta.Business.Test;

public class TowarUpdateTest : TestBase {
    [Test]
    [Description("Weryfikuje, że zmiana nazwy towaru jest trwale zapisywana do bazy.")]
    public void Should_ZapisacNazweDoBazy_When_ZmienionoNazweTowaru() {
        // Arrange
        var towar = Session.GetTowary().Towary.WgKodu["ZES_Z190"];

        // Act
        InTransaction(() => towar.Nazwa = "Nowa nazwa");
        SaveDispose();                       // zapis do bazy + świeża sesja

        // Assert
        Session.GetTowary().Towary.WgKodu["ZES_Z190"].Nazwa
            .Should().Be("Nowa nazwa");
    }
}
```

Dziedziczenie po `TestBase` wystarcza — domyślnie test działa na bazie `nunit_default`
(`GoldStandardDatabaseInitializer`) w transakcji, która wycofa powyższą zmianę po zakończeniu.

> **Pisząc nowy test, stosuj zawsze (szczegóły w sekcjach poniżej):**
> nazwa `Prefiks_Should_…_When_…` · atrybut `[Description("po co ten test")]` · struktura **AAA** ·
> zmiana → `Commit` (przez `InTransaction`) → `SaveDispose` → asercja ze świeżej sesji.

## Nazewnictwo i grupowanie metod — `Should` / `When` + prefiks

Nazwę metody buduj wg wzorca **`Prefiks_Should_<oczekiwanyRezultat>_When_<warunek>`** — sama nazwa
opisuje zachowanie: co system *powinien* zrobić i *kiedy*, a prefiks grupuje testy wg obiektu/zagadnienia:

```csharp
public void Towar_Should_ZapisacNazweDoBazy_When_ZmienionoNazweTowaru() { ... }
public void Dokument_Should_RzucicWyjatek_When_ZatwierdzanoDokumentBezPozycji() { ... }
public void Cennik_Should_ZwrocicPusto_When_BrakTowarowSpelniajacychFiltr() { ... }
```

- Człon `When_` pomiń **tylko** przy scenariuszu bez warunku (np. `Towar_Should_MiecDomyslnaStawkeVat`).
- Nazwa **uzupełnia**, nie zastępuje atrybutu `[Description]`.
- Grupuj przede wszystkim **nazwą klasy** odpowiadającą obiektowi/tematowi (`TowarUpdateTest`,
  `DokumentHandlowyStanTest`); prefiks w nazwie metody stosuj przy wielu scenariuszach w jednej klasie:

```csharp
public class DokumentHandlowyStanTest : TestBase {
    public void Zatwierdzenie_Should_UtworzycZapisMagazynowy_When_DokumentMaPozycje() { ... }
    public void Zatwierdzenie_Should_RzucicWyjatek_When_BrakPozycji() { ... }
    public void Anulowanie_Should_ZdjacRezerwacje_When_DokumentBylZatwierdzony() { ... }
}
```

Prefiks (`Zatwierdzenie_`, `Anulowanie_`) porządkuje testy tematycznie i ułatwia filtrowanie
uruchomień (`--filter "...~Zatwierdzenie"`).

Staraj się używać tych samych prefiksów w obrębie jednej klasy — chyba że powstaje test, który
funkcjonalnie jest niezależny od pozostałych, wtedy możesz użyć różnych.

## Obowiązkowy opis testu — `[Description]`

Każdy test musi mieć atrybut `[Description("...")]` wyjaśniający, **po co** został utworzony —
jaki scenariusz lub regułę biznesową weryfikuje (a nie powtórkę nazwy metody):

```csharp
[Test]
[Description("Sprawdza, że nie można zatwierdzić dokumentu bez pozycji.")]
public void Should_RzucicWyjatek_When_ZatwierdzanoDokumentBezPozycji() { ... }
```

Opis pojawia się w raportach testów i ułatwia zrozumienie intencji przy regresjach.

## Struktura testu — zasada AAA (Arrange · Act · Assert)

Nowe testy buduj w trzech wyraźnie oddzielonych fazach — czytelnie rozdziel je pustą linią
lub komentarzem `// Arrange` / `// Act` / `// Assert`:

- **Arrange** — przygotowanie danych i stanu wejściowego (pobranie obiektów, ustawienie warunków).
- **Act** — pojedyncza testowana operacja (wywołanie metody, zmiana + `SaveDispose`).
- **Assert** — weryfikacja rezultatu (`.Should()...`).

Trzymaj jeden logiczny scenariusz na test i jedną akcję w fazie Act — dzięki temu przy błędzie
od razu wiadomo, co się zepsuło.

## Wybór bazy testowej — `[TestDatabase]`

Atrybut na klasie testowej wskazuje inicjalizator bazy. Domyślną wartością (dziedziczoną
z `TestBase`) jest baza złota `nunit_default`, więc bez atrybutu masz standardowe dane demo.

| Atrybut | Baza | Zawartość |
|---|---|---|
| brak / `[TestDatabase(typeof(GoldStandardDatabaseInitializer))]` | `nunit_default` | standardowe dane demo (złota licencja) |
| `[TestDatabase(typeof(StandardUIDatabaseInitializer))]` | `nunit_ui` | znacznie więcej rekordów (np. ~1000 towarów, dokumenty, kokpity) — do testów UI/wydajności |
| `[TestDatabase(typeof(PremiumUIDatabaseInitializer))]` | `nunit_premiumui` | baza premium dla programu Triva (`IsPremium = true`) |

Dodatkowe opcje atrybutu: `User` / `Password` (logowanie jako konkretny operator), np.
`[TestDatabase(typeof(StandardUIDatabaseInitializer), User = "k", Password = "k")]`.

`User` / `Password` są szczególnie istotne, gdy test ma zweryfikować zachowanie **dla operatora
pulpitowego** (np. Pulpit Kierownika, Pulpit Pracownika) — taki operator ma inne prawa dostępu
i ustawienia niż domyślny `Administrator`, więc widoczność folderów, dostępne dane i wynik operacji
mogą się różnić. Logując się jego kontem, testujesz system dokładnie w takim kontekście, w jakim
działa użytkownik pulpitu.

**Własny inicjalizator** — twórz go tylko w **szczególnych przypadkach**. Każda dodatkowa baza
testowa oznacza kolejną bazę do zbudowania i utrzymania na infrastrukturze, co znacząco spowalnia
późniejsze wykonywanie testów integracyjnych. Najpierw sprawdź, czy scenariusz da się zrealizować
na jednej z gotowych baz (`nunit_default` / `nunit_ui` / `nunit_premiumui`), wpisując potrzebne
dane w `ClassSetup`. Uzasadniony przypadek własnego inicjalizatora to m.in. **testy własnego
dodatku** (import roli z prawami do obiektów dodatku — sekcja
[Testy własnego dodatku](#testy-własnego-dodatku-testbase)). Jeśli własny inicjalizator jest
naprawdę konieczny — dziedzicz po `TestDatabaseInitializer` (lub po gotowym inicjalizatorze,
np. `GoldStandardDatabaseInitializer`), ustaw nazwę bazy atrybutem i wypełnij dane
w `Initialize()` (dostępne `Session`/`ConfigSession` + skróty `InSave`/`InConfigSave`):

```csharp
[TestDatabase(typeof(GoldStandardDatabaseInitializer), "nunit_mojmodul")]
public class MojModulDatabaseInitializer : TestDatabaseInitializer {
    protected override void Initialize() {
        InConfigSave(() => ConfigSession.AddRow(new FeatureDefinition("Towary") {
            Name = "Kwota", TypeNumber = FeatureTypeNumber.Decimal
        }));
    }
}
```

> **Uszkodzona baza testowa — dziwne błędy.** Czasami system nie wykryje poprawnie stanu bazy
> i nie przygotuje jej prawidłowo — testy zaczynają wtedy kończyć się niespodziewanymi, trudnymi
> do wyjaśnienia błędami (niezwiązanymi z testowaną logiką). Naprawa: **usuń odpowiednią bazę
> testową z serwera SQL** (np. `nunit_default`) — najprościej narzędziem `dbmgr`:
> `dbmgr drop nunit_default` (składnia i pozostałe komendy — skill **[tools](../../tools/SKILL.md)**). Przy kolejnym
> uruchomieniu `TestBase` automatycznie utworzy nową bazę i wypełni ją danymi z inicjalizatora.
> Uwaga: **odbudowa bazy testowej od zera jest długotrwała** i może chwilę potrwać — to normalne,
> po jednorazowym odtworzeniu kolejne uruchomienia testów są już szybkie.

## Testy własnego dodatku (TestBase)

Testy integracyjne modułu dodatkowego (np. `Soneta.MojDodatek` z `MojDodatekModule`) piszesz
na tym samym `TestBase`, ale host testowy nie wie nic o Twoim dodatku — trzeba go **zarejestrować**
i **nadać prawa**. Poniższe problemy występują zawsze; rozwiązania są potwierdzone w praktyce.

### ⚠ Rejestracja modułu dodatku — `[SetUpFixture]` + `Assembly.Load`

Host testowy enumeruje moduły z **załadowanych** assembly. Assembly dodatku nie jest jeszcze
załadowane w momencie budowania kolekcji modułów, więc `session.GetMojDodatek()` rozwiązuje się
na **błędny** moduł platformy. Objaw:

```
InvalidCastException: Unable to cast object of type 'Soneta.BI.BIModule'
to type 'Soneta.MojDodatek.MojDodatekModule'   (w Module.GetInstance / session.Modules[moduleInfo])
```

Rozwiązanie — osobna klasa `[SetUpFixture]` w przestrzeni nazw testów, która ładuje assembly
**zanim** wykona się setup `TestBase`:

```csharp
using System.Reflection;
using NUnit.Framework;

namespace Soneta.MojDodatek.Tests;

[SetUpFixture]
public class ModuleLoader {
    [OneTimeSetUp]
    public void LoadAddonAssembly() => Assembly.Load("Soneta.MojDodatek");
}
```

`[SetUpFixture]` uruchamia się raz, przed wszystkimi klasami testowymi w danej przestrzeni nazw.
**NIE działa** `Assembly.Load` w `ClassSetup()` (czyli w override `OneTimeSetUp` samego `TestBase`) —
to za późno, host zbudował już kolekcję modułów.

### ⚠ Prawa do nowych obiektów dodatku — rola „pełny dostęp"

Domyślny operator `TestBase` („Administrator") **nie ma prawa zapisu** do świeżo dodanych obiektów
dodatku — prawa nowego modułu nie są nadane w żadnej istniejącej roli. Objaw przy `Commit`/`Save`:

```
AccessWriteDeniedException   (ścieżka praw: Dodatki\MojDodatek\MojObiekt)
```

Rozwiązanie — **własny initializer bazy testowej** (to jeden z uzasadnionych przypadków, o których
mowa wyżej), który importuje rolę nadającą pełne prawa do gałęzi `Dodatki`:

```csharp
[TestDatabase(typeof(GoldStandardDatabaseInitializer), "nunit_addon")]
public class AddonDatabaseInitializer : GoldStandardDatabaseInitializer {
    protected override void Initialize() {
        base.Initialize();
        ImportBusinessXml("RolaPelnePrawa.xml");   // zasób osadzony (EmbeddedResource)
    }
}

[TestDatabase(typeof(AddonDatabaseInitializer))]
public class MojObiektTest : TestBase { ... }
```

Baza `nunit_addon` budowana jest raz (pierwsze uruchomienie ~2 min); kolejne uruchomienia są szybkie.

**Struktura pliku roli** (`RolaPelnePrawa.xml`, osadzony jako `<EmbeddedResource>` w projekcie
testowym) — to plik business.xml z obiektem `SystemRole`, którego pole `RoleText` zawiera
**zaescape'owany** XML definicji roli (`&lt;`/`&gt;`):

```xml
<session xmlns="http://www.soneta.pl/schema/business">
  <SystemRole id="SystemRole_5" guid="00000000-0015-0004-0003-000000000000">
    <RoleText>&lt;?xml version="1.0" encoding="utf-8"?&gt;
&lt;Role xmlns:xsi="..." xmlns:xsd="..." Guid="00000000-0015-0004-0003-000000000000"
      Name="Pełny dostęp do programu" Mode="Advanced"&gt;
  &lt;Right Name="Rights"&gt;
    &lt;Right Name="Wielokrotne"  AccessRight="Granted" /&gt;
    &lt;Right Name="Dodatki"      AccessRight="Granted" /&gt;   &lt;!-- klucz: prawa do obiektów dodatków --&gt;
    &lt;Right Name="Konfiguracja" AccessRight="Granted" /&gt;
    &lt;Right Name="Program"      AccessRight="Granted" /&gt;
  &lt;/Right&gt;
  &lt;Description&gt;Pełny dostęp do wszystkich funkcji i danych.&lt;/Description&gt;
&lt;/Role&gt;</RoleText>
  </SystemRole>
</session>
```

Najprościej: wyeksportuj z programu rolę systemową „Pełny dostęp" i użyj jej bez zmian — kluczowe
jest `Dodatki=Granted`. Plik **musi być w UTF-8** i mieć poprawnie zaescape'owaną treść `RoleText`;
uszkodzone kodowanie lub niedomknięte encje objawiają się błędem importu przy budowie bazy.

### Testy praw obiektowych (`IRightsSource`)

Gdy obiekt dodatku jest **źródłem praw** ([rights-source.md](rights-source.md)), nowy rekord jest
dla ról domyślnie **Denied** — test scenariusza „operator z prawem/bez prawa" musi jawnie nadać
prawo obiektowe operatorowi testowemu. Robi się to rekordem `Right` w sesji konfiguracyjnej;
uprawnienie operatora pobieraj przez `AuthorizationInfo` sesji (nie z `Login.Entitle` —
reguła z [session-login.md](session-login.md#dostęp-do-informacji-o-operatorze)):

```csharp
// session = sesja konfiguracyjna (Right/Entitle to dane konfiguracyjne)
var entitle = session.AuthorizationInfo.Operator.Entitles.GetFirst().Entitle;
session.AddRow(new Right(entitle, definicja, false));   // false = pełne prawo, true = tylko odczyt
```

**Kolejność ma znaczenie — cache ról.** Prawa nadane w tej samej transakcji nie odświeżają cache
ról zalogowanego loginu ([rights-source.md](rights-source.md#nadawanie-praw)). Jeśli funkcja jest
sterowana flagą (opcją konfiguracyjną):

1. Definicje i rekordy `Right` twórz przy **wyłączonej** fladze funkcji,
2. flagę włączaj **po zapisie konfiguracji** — dopiero wtedy egzekwowanie praw widzi nadane `Right`.

Włączenie flagi przed zapisem = fałszywy Denied (`AccessWriteDeniedException` przy `relright`).
Sprzątanie i przywracanie flagi wykonuj w `try/finally`, żeby nieudany test nie zostawił
konfiguracji w stanie włączonym.

### ⚠ Tabele `config="true"` są read-only w sesji operacyjnej

Słowniki konfiguracyjne dodatku (tabele z `config="true"` w business.xml) **nie dadzą się edytować**
w `Session`/`InTransaction` — próba kończy się `ReadOnlyException`. Rozróżnienie:

- dane **OPERACYJNE** → `Session` + `InTransaction`,
- dane **KONFIGURACYJNE** → `ConfigSession` + `InConfigTransaction` (lub `EditInConfigSession`).

```csharp
// ŹLE — ReadOnlyException:
InTransaction(() => Session.GetMojDodatek().Slowniki.AddRow(new SlownikDef { ... }));

// DOBRZE:
InConfigTransaction(() => ConfigSession.GetMojDodatek().Slowniki.AddRow(new SlownikDef { ... }));
```

**Odczyt** danych konfiguracyjnych z sesji operacyjnej oraz **przypisanie** config-relacji
w obiekcie operacyjnym działają normalnie — read-only dotyczy tylko zapisu samych wierszy
konfiguracyjnych. Pamiętaj też o zapisie `ConfigSession` przed użyciem danych w `Session`
(sekcja [Dostęp do danych](#dostęp-do-danych)).

### Projekt testowy dodatku i uruchamianie

- Projekt testowy potrzebuje `<ProjectReference>` do projektu logiki dodatku — daje typy
  w kompilacji i kopiuje assembly do outputu, dzięki czemu `Assembly.Load("Soneta.MojDodatek")`
  je znajdzie.
- Plik roli dodaj jako `<EmbeddedResource Include="RolaPelnePrawa.xml" />`.
- `dotnet test` na **Microsoft.Testing.Platform** wymaga `--project` ze ścieżką do `.csproj`
  (nie katalogu ani samej nazwy projektu):

```bash
dotnet test --project Soneta.MojDodatek.Tests/Soneta.MojDodatek.Tests.csproj \
    --filter "FullyQualifiedName~MojObiektTest"
```

- Pierwsze uruchomienie buduje bazę testową (długo — to normalne); kolejne są szybkie.

### Checklista — testy własnego dodatku

- [ ] `[SetUpFixture]` z `[OneTimeSetUp]` wołającym `Assembly.Load("<AssemblyDodatku>")`
      (nie w `ClassSetup` — za późno).
- [ ] Własny initializer (np. `AddonDatabaseInitializer`) z **własną nazwą bazy** (np. `nunit_addon`)
      i `ImportBusinessXml` roli „pełny dostęp" (`Dodatki=Granted`).
- [ ] `[TestDatabase(typeof(AddonDatabaseInitializer))]` na klasach testowych dodatku.
- [ ] Plik roli: UTF-8, `RoleText` poprawnie zaescape'owany, osadzony jako `EmbeddedResource`.
- [ ] Słowniki `config="true"` edytujesz przez `ConfigSession`/`InConfigTransaction`, nie `Session`.
- [ ] `<ProjectReference>` z projektu testów do projektu logiki dodatku.
- [ ] Uruchamianie: `dotnet test --project <ścieżka.csproj> --filter "FullyQualifiedName~<Klasa>"`.

## Cykl życia testu — metody wirtualne

Nadpisuj te metody zamiast atrybutów NUnit `[SetUp]`/`[OneTimeSetUp]` (są zajęte przez
mechanikę `TestBase`). **Zawsze wołaj `base`.**

| Metoda | Kiedy | Do czego |
|---|---|---|
| `ClassSetup()` | raz przed wszystkimi testami klasy | dane współdzielone przez testy (definicje cech, konfiguracja) |
| `ClassTearDown()` | raz po wszystkich testach klasy | rzadko potrzebne — rollback jest automatyczny |
| `TestSetup()` | przed każdym testem | inicjalizacja per-test |
| `TestTearDown()` | po każdym teście | rzadko potrzebne |

```csharp
public class FeaturesTest : TestBase {
    public override void ClassSetup() {
        base.ClassSetup();
        EditInConfigSession((ses, cx) => ses.AddRow(new FeatureDefinition("Kontrahenci") {
            Name = "Litera", TypeNumber = FeatureTypeNumber.Array
        }));
    }
}
```

Dane z `ClassSetup` widzą wszystkie testy klasy, a po ostatnim teście znikają razem z transakcją
poziomu klasy — nie zaśmiecają innych klas testowych.

## Transakcyjność — `EnableDbTransation`

Domyślnie każda zmiana jest wycofywana. Aby **wyłączyć** transakcję dla całej klasy (np. testy
współbieżności, które muszą realnie zapisać dane do bazy i widzieć je z wielu wątków):

```csharp
protected override bool EnableDbTransation => false;
```

Wtedy odpowiadasz sam za doprowadzenie bazy do stanu wyjściowego. Wyłączaj tylko gdy jest to
konieczne (testy wielowątkowe, testy blokad optimistic-lock między loginami).

## Dostęp do danych

| Składowa | Zwraca |
|---|---|
| `Session` | sesja edycyjna operacyjna, podpięta do bazy testowej |
| `ConfigSession` | sesja konfiguracyjna edycyjna |
| `Context` / `ConfigContext` | `Context` skojarzony z odpowiednią sesją |
| `Login` | login bazy testowej |
| `Database` | obiekt bazy (m.in. `StartSqlTraceInfo`) |

Sesje są gotowe do użycia — moduły pobieraj **wyłącznie metodami rozszerzającymi** `GetX()`
(generowanymi z `business.xml`, także dla modułów dodatków): `Session.GetTowary()`,
`Session.GetHandel()`, `ConfigSession.GetBusiness()` itd. — zasada
[safe-code.md §14.4](safe-code.md).

> `ConfigEditSession` jest redundantne względem `ConfigSession` (zwraca to samo) — **nie stosuj go**,
> używaj `ConfigSession`.

**Dane z sesji konfiguracyjnej w sesji operacyjnej.** Jeśli test wpisuje dane w `ConfigSession`,
a potem chce ich użyć w `Session` (operacyjnej), **najpierw zapisz sesję konfiguracyjną**
(`SaveDisposeConfig()` lub `EditInConfigSession`), a dopiero potem odwołaj się do sesji operacyjnej.
Odwołanie do `Session` przed zapisem `ConfigSession` sprawi, że sesja operacyjna nie zobaczy
jeszcze niezapisanych danych konfiguracyjnych.

## Modyfikacja danych

Każda zmiana musi być w transakcji biznesowej. `TestBase` opakowuje wzorzec `Logout → Commit`:

```csharp
InTransaction(() => towar.Nazwa = "X");        // Session,       Commit()
InUITransaction(() => dok.Stan = ...);         // Session,       CommitUI()  (kod UI: worker/extender)
InConfigTransaction(() => def.Nazwa = "Y");    // ConfigSession, Commit()
InUIConfigTransaction(() => ...);              // ConfigSession, CommitUI()
```

**Zapis do bazy + odświeżenie sesji** — po zapisie chcemy zwykle sprawdzić, że dane realnie trafiły
do bazy i wczytają się od nowa. `SaveDispose()` zapisuje sesję operacyjną, niszczy ją i podstawia
świeżą (już z zapisanymi danymi); `SaveDisposeConfig()` — analogicznie dla sesji konfiguracyjnej.
Wszystko nadal w transakcji bazodanowej, więc test i tak zostanie wycofany.

```csharp
InTransaction(() => towar.Nazwa = "X");
SaveDispose();                                 // teraz Session to nowa sesja
Session.GetTowary().Towary.WgKodu[kod].Nazwa.Should().Be("X");
```

`EditInConfigSession((ses, cx) => { ... })` — otwiera własną sesję konfiguracyjną, wykonuje edycję
w transakcji, robi `Commit` + `Save`. Wygodne w `ClassSetup` do wpisania danych konfiguracyjnych.

## Metody skrótowe — używaj krytycznie

`TestBase` powstawało przez lata i przez wielu autorów, więc część skrótów jest redundantna.
Preferuj czytelność i spójność z testami w sąsiedztwie. Skróty do pobierania/dodawania:

```csharp
Get<T>(id) / Get<T>(guid) / Get<T>(row)         // z sesji operacyjnej
GetConfig<T>(...)                               // z sesji konfiguracyjnej
Add<T>(row) / AddConfig<T>(row)                 // dodanie (samo otwiera transakcję, jeśli trzeba)
CreateContextRow<T>(row)                        // opakowanie w ContextBasedRow z Context
```

W praktyce najczęściej wystarcza jawne `Session.GetXxx()...` + `InTransaction` + `SaveDispose` —
jest czytelniejsze niż łańcuch skrótów.

`RowBuilder<TRow>` (fluent builder) — istnieje do zapisu przypominającego **skryptowanie**, a nie
logikę biznesową. Powoduje jednak, że testy są **trudne do debugowania** i słabo poddają się
późniejszej analizie narzędziami. **Ograniczaj jego stosowanie** — nie używaj go bez wyraźnej
prośby programisty; domyślnie buduj obiekty jawnie przez sesję i transakcje.

## Podmiana serwisów (DI) na potrzeby testu

Aby podstawić własną implementację interfejsu **tylko dla tej klasy testowej** (bez wpływu na inne
testy), nadpisz jedną z metod konfiguracyjnych — działają na odpowiednim `ServiceScope`:

| Metoda | Scope |
|---|---|
| `ConfigureDatabaseServices(IServiceCollection)` | `ServiceScope.Database` |
| `ConfigureLoginServices(IServiceCollection)` | `ServiceScope.Login` |
| `ConfigureSessionServices(IServiceCollection)` | `ServiceScope.Session` |

```csharp
public class LicenceTest : TestBase {
    protected override void ConfigureLoginServices(IServiceCollection services) =>
        services.ReplaceService<ILoginLicenceDataProviderInternal, ThisTestLicenceProvider>();

    [Test]
    public void PodmienionySerwisJestUzywany() =>
        Login.GetRequiredService<ILoginLicenceDataProviderInternal>()
            .Should().BeOfType<ThisTestLicenceProvider>();
}
```

`ReplaceService<TInterface, TImpl>()` podmienia domyślną rejestrację; można też `AddSingleton`,
`AddHttpClient` itd. Serwisy odczytujesz przez `Login.GetRequiredService<T>()` /
`Session.GetRequiredService<T>()`. Szczegóły scope'ów — [services.md](services.md).

## Import konfiguracji i definicji

- `ImportConfigFile(nazwaCel, nazwaZasobu, operatorCx, asmType, attrs)` — wgrywa plik konfiguracyjny
  (np. `*.viewform.xml`, folder, ustawienia) z **zasobu osadzonego** w assembly testowym. Domyślnie
  wycofywany po klasie; `SetConfigFilesPermanent()` zostawia go na stałe. `RemoveConfigFile(...)` usuwa.
- `ImportBusinessXml(nazwaZasobu)` — wczytuje dane XML (np. definicje dokumentów, cechy, dane
  przygotowawcze) przez `SessionReader(Login)`. Struktura pliku: artykuł [import-export-xml](../../config/references/import-export-xml.md); warstwa kodu: [sessionreader-sessionwriter.md](./sessionreader-sessionwriter.md).
- `RegisterDataForm(name, resName, asm)` — rejestruje formularz z zasobu (testy UI/DataForm).

## Asercje

Dopasuj bibliotekę do testów w okolicy. **Preferowana: `AwesomeAssertions`** (`using AwesomeAssertions;`,
składnia `.Should()`), np. `wartość.Should().Be(...)`, `kolekcja.Should().HaveCount(3)`,
`akcja.Should().Throw<BusException>()`. W starszych testach spotkasz NUnit `Assert.That(...)` —
w takim pliku zostań przy NUnit dla spójności.

**Rozszerzenia Soneta** (znacznie upraszczają asercje na tekstach wielowierszowych, zwłaszcza SQL):

| Asercja | Działanie |
|---|---|
| `str.Should().BeSameTexts(oczekiwany)` | równość ignorując białe znaki (i wielkość liter) |
| `str.Should().ContainSameText(fragment)` | zawieranie fragmentu ignorując białe znaki |
| `str.Should().BeSameTextsIgnoreNumbers(oczekiwany)` | jw., dodatkowo ignoruje liczby i `WITH(ROWLOCK)` |
| `obj.ShouldNotBeNull()` | asercja `!= null` **zwracająca** niepustą wartość (wygodne do dalszego łańcucha) |

```csharp
var towar = Session.GetTowary().Towary.WgKodu["ZES_Z190"].ShouldNotBeNull();
towar.Nazwa.Should().Be("Nowa nazwa");   // dalej pracujemy na wartości non-null
```

`BeSameTexts` / `BeSameTextsIgnoreNumbers` porównują bez białych znaków, więc oczekiwany tekst
możesz zapisać czytelnie w **raw string literal** (`""" … """`) bez martwienia się o wcięcia.

## Testy optymalizacji SQL — `SqlTraceInfo`

Gdy test ma pilnować, **jakie i ile** zapytań SQL generuje operacja (regresje wydajności), użyj
`Database.StartSqlTraceInfo()`. Obiekt zbiera zapytania od `Start` do `Dispose`:

```csharp
using var sql = Database.StartSqlTraceInfo();
view.ForceAllRows();

// BeSameTextsIgnoreNumbers ignoruje białe znaki i liczby — oczekiwany SQL zapisz czytelnie
sql.StatNoStrings.Should().BeSameTextsIgnoreNumbers("""
    1: select * from Towary
    """);
```

Przydatne właściwości: `AllLines` / `AllLinesClean` (wszystkie zapytania), `FirstLine`,
`Stat` / `StatNoStrings` (zliczone unikalne zapytania; `NoStrings` maskuje literały `'...'` → `?`
i liczby → `?`, żeby asercja była stabilna), `GetStatContainsSql(...)`. `Reset()` czyści licznik.
`StartSqlTraceInfoWithFullParamsLog()` loguje też wartości parametrów.

**Rozgrzej cache przed zbieraniem statystyki.** Sesja wczytuje sporo danych konfiguracyjnych
**raz na uruchomienie** — kolejne sesje korzystają już z cache. Jeśli więc mierzysz operację
„na zimno", w statystyce pojawią się jednorazowe zapytania konfiguracyjne, które zaburzają wynik
i sprawiają, że jest **niepowtarzalny** (zależny od kolejności testów). Dlatego wykonaj mierzoną
operację **raz przed** rozpoczęciem właściwego zbierania — dopiero potem startuj `SqlTraceInfo`:

```csharp
// Rozgrzewka — wypełnia cache konfiguracyjny (wynik ignorujemy)
view.ForceAllRows();

// Właściwy pomiar: liczy już tylko zapytania samej operacji
using var sql = Database.StartSqlTraceInfo();
view.ForceAllRows();
sql.StatNoStrings.Should().BeSameTextsIgnoreNumbers("""...""");
```

Alternatywnie rozgrzej cache w `ClassSetup`, jeśli dotyczy wszystkich testów w klasie.

## Atrybuty pomocnicze

- `[WorkItemIds(467895)]` na metodzie testowej — linkuje test do Work Itemów (Azure DevOps),
  relacja *tested by*. Stosuj przy testach pisanych pod konkretne zadanie/błąd.

## Uruchamianie

Projekt testowy używa NUnit. Uruchamiaj filtrem, nie całym projektem:

```bash
dotnet test Soneta.Business.Test --filter "FullyQualifiedName~TowarUpdateTest"
```

Zgodnie z konwencją: gdy pracujesz nad jedną klasą testową, uruchamiaj tylko ją (`--filter`),
a nie cały projekt.

Projekt na **Microsoft.Testing.Platform** (typowo projekt testowy dodatku) wymaga jawnego
`--project` ze ścieżką do pliku `.csproj` — nie zadziała katalog ani sama nazwa projektu:

```bash
dotnet test --project Soneta.MojDodatek.Tests/Soneta.MojDodatek.Tests.csproj \
    --filter "FullyQualifiedName~MojObiektTest"
```

## Checklist / pułapki

- Styl .NET 10: **file-scoped namespace** (bez klamer), **raw string literals** (`""" … """`) dla tekstów wielowierszowych/SQL.
- Nazwa metody wg wzorca `Should_<rezultat>_When_<warunek>`; grupy testów łącz wspólnym prefiksem.
- Każdy test ma `[Description("po co ten test")]` opisujący weryfikowany scenariusz/regułę.
- Buduj test wg **AAA** — oddzielone fazy Arrange · Act · Assert, jedna akcja w Act.
- Nadpisujesz `ClassSetup` / `TestSetup` / `ClassTearDown` / `TestTearDown` → **wołaj `base`**.
- Nie używaj atrybutów `[SetUp]` / `[OneTimeSetUp]` w klasie testowej — te haki należą do `TestBase`.
- Po edycji w sesji zawsze `Commit` (przez `InTransaction`/`InUITransaction`) — brak commitu = rollback.
- Chcesz sprawdzić trwały zapis? `SaveDispose()` / `SaveDisposeConfig()` i czytaj ze świeżej sesji.
- Nie sprzątaj ręcznie danych — transakcja `TestBase` wycofa je sama (chyba że `EnableDbTransation => false`).
- Dane globalne dla klasy wpisuj w `ClassSetup`, dane per-test w teście lub `TestSetup`.
- DI podmieniaj przez `ConfigureXxxServices` — nie modyfikuj rejestracji atrybutami assembly (wpłynęłoby na inne testy).
- Testujesz własny dodatek? Przejdź checklistę z sekcji
  [Testy własnego dodatku](#testy-własnego-dodatku-testbase) (`Assembly.Load` w `[SetUpFixture]`,
  rola z prawami `Dodatki`, `ConfigSession` dla tabel `config="true"`).
