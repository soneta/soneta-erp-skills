# Źródła praw (`IRightsSource`)

> **Dwa różne mechanizmy — nie myl ich.** Ten dokument opisuje **prawa obiektowe**: dostęp do
> danych operacyjnych sterowany wskazanym obiektem (magazyn, rejestr, definicja). Osobną sprawą
> jest **miejsce tabeli w drzewie uprawnień** — decyduje o nim plik `*.rightstree.xml`
> towarzyszący `business.xml` (skill `/soneta:business-xml`, `references/rights-tree.md`).
> Każda nowa tabela wymaga tam wpisu, o ile nie dziedziczy praw przez relację
> `relright="true"`/`relguided`.
>
> Trzeci mechanizm — **prawa proste (funkcyjne)** nadawane w rolach, bez rekordu źródła —
> opisuje sekcja [Prawa proste `SimpleRight`](#prawa-proste-simpleright).

Obiekt (zwykle **konfiguracyjny**, np. magazyn, rejestr, definicja) może być **źródłem praw**:
operatorowi/roli przypisuje się uprawnienia do tego obiektu, co steruje dostępem do **danych
operacyjnych referujących** do niego — a nie tylko do samego obiektu konfiguracyjnego. Przykład:
operator z prawem do danego magazynu widzi tylko dokumenty przypisane do tego magazynu, a do
dokumentów z innych magazynów dostępu nie ma.

**Najważniejsze reguły:**

- **Nowe źródło praw jest domyślnie DENIED.** Funkcja oparta o taki rekord jest martwa do czasu
  nadania prawa (rola w UI, rekord `Right` w kodzie lub `<Right>` w dbinit — patrz
  [Nadawanie praw](#nadawanie-praw)). Projektując moduł ze źródłem praw, **zaplanuj nadanie praw
  przy tworzeniu bazy** (rekordy standardowe/demo) — inaczej funkcja nie działa out-of-the-box,
  a objawy bywają mylące (patrz niżej).
- **Odczyt jakiejkolwiek kolumny wiersza, do którego operator ma prawo Denied, rzuca
  `AccessDeniedException`** („Brak praw dostępu do danych."). Dotyczy wierszy `IRightsSource`
  oraz wierszy dziedziczących prawa przez `relright`. Sam odczyt `row.AccessRight` /
  `login.GetObjectRight(row)` jest **bezpieczny** — nie czyta rekordu.
- W **kodzie biznesowym nie sprawdzaj** `AccessRight` warunkami — system egzekwuje prawa sam
  (patrz [safe-code.md](safe-code.md) §7.2). Wyjątkiem jest **kod infrastrukturalny** enumerujący
  źródła praw (sekcja niżej) — to odpowiednik automatycznego filtrowania list, nie logika biznesowa.

## Podstawy

- Włączenie: w `business.xml` dodaj do tabeli `<interface>IRightsSource</interface>` (deklarację
  `<interface>` opisuje skill `/soneta:business-xml`). Od tego momentu
  system sam dba o widoczność obiektów i propagację praw. (`IRightsSourceEx` dokłada pod-kategorię,
  `IsRightsSourceEnable()`, `IsRightsSourceVisible()`.)
- Odczyt uprawnień: `Row.AccessRight`, `Table.AccessRight`, `Login.GetObjectRight(source)` →
  `AccessRights` (`Granted`/`Denied`/…).
- **Listy/`View` automatycznie filtrują** dane po prawach — pokazują tylko rekordy ze źródeł, do
  których operator ma dostęp. Na formularzu definicji pojawia się zakładka z przypisaniami uprawnień.

## Kod infrastrukturalny enumerujący źródła praw

Kod, który **enumeruje definicje** będące źródłami praw (budowa pozycji menu, wybór definicji do
zaoferowania operatorowi), musi sam pominąć wiersze bez prawa — zanim dotknie ich kolumn:

```csharp
foreach (Definicja definicja in module.Definicje) {
    // Prawo przed kolumnami — odczyt kolumny wiersza bez prawa rzuca AccessDeniedException.
    if (definicja.AccessRight == AccessRights.Denied)
        continue;
    if (definicja.Blokada || !definicja.WidocznaWMenu)
        continue;
    yield return new DefinicjaAction(definicja);
}
```

- Sprawdzenie `AccessRight` wykonuj **przed** odczytem pierwszej kolumny wiersza.
- **Komunikaty odmowy nie mogą czytać kolumn wiersza** (np. jego nazwy) — same rzucą
  `AccessDeniedException`.
- Taki kod bywa wywoływany przy każdym otwarciu okna (np. z `GetActions` —
  [worker-extender.md](worker-extender.md#czynności-dynamiczne--getactions)) — niezłapany wyjątek
  z odczytu kolumny objawia się dialogiem błędu w całej aplikacji, a przyczyną pierwotną jest
  zwykle **brak nadanych praw**, nie sam kod.

## Nadawanie praw

- **UI** — rola operatora (zakładka przypisań na formularzu definicji).
- **Kod** — rekord `Right` wiążący uprawnienie (`Entitle`) ze źródłem (`Source`); wzorzec dla
  testów integracyjnych: [integration-tests.md](integration-tests.md#testy-praw-obiektowych-irightssource).
- **dbinit/demo** — element `<Right>` w pliku importu; format i stały guid Entitle administratora
  opisuje `/soneta:config` (import-xml, sekcja o nadawaniu praw obiektowych).

**Cache ról a transakcja:** prawa nadane w tej samej transakcji **nie odświeżają cache ról**
zalogowanego loginu — kod, który tworzy definicję, nadaje prawo i natychmiast je egzekwuje
w jednym zapisie, zobaczy Denied. Nadanie praw i ich egzekwowanie rozdziel zapisem sesji
(konsekwencje dla testów — [integration-tests.md](integration-tests.md#testy-praw-obiektowych-irightssource)).

## Prawa proste `SimpleRight`

Prawo funkcyjne („wolno edytować cudze oceny") bez własnej tabeli. Deklaracja atrybutem assembly
przy typie wiersza; nazwa prawa jest jednocześnie etykietą w drzewie uprawnień roli:

```csharp
[assembly: SimpleRight(typeof(Ocena), Oceny.SimpleRightZarzadzanie,
    Description = "Zezwala na edycję ocen innych autorów.")]

public class Oceny : OcenyModule.OcenaTable {
    public const string SimpleRightZarzadzanie = "Zarządzanie ocenami";

    public static bool IsZarzadzanieGranted(Session session) =>
        session.Login.CurrentRole?[typeof(Ocena), SimpleRightZarzadzanie] == AccessRights.Granted;
}
```

- Typowe miejsca sprawdzenia: `OnDeleting` (rzuć `RowException`, gdy brak prawa), `CalcReadOnly`
  (wiersz tylko do odczytu dla operatora bez prawa), warunek `IsEnabled…`/`IsVisible…` czynności.
- `CustomType = "Worker"` grupuje prawo w drzewie uprawnień pod czynnościami.
- To świadoma reguła funkcyjna, nie ręczne powielanie praw do tabel — zakaz warunków na
  `AccessRight` z [safe-code.md](safe-code.md) §7.2 jej nie dotyczy.
- **Null-safe:** sesja bez operatora (import `dbmgr`, dane demo, zadania systemowe) ma
  `Login.CurrentRole == null` i `AuthorizationInfo.Operator == null`. Kod praw w `CalcReadOnly`/
  `OnDeleting` bez `?.` rzuca `NullReferenceException`. Brak operatora traktuj jako kontekst
  systemowy (pełny dostęp) — szczegóły importu: `/soneta:config` → `references/demo-data.md`.

## Checklista — moduł ze źródłem praw

- [ ] Prawa dla ról nadane przy tworzeniu bazy (dbinit/demo — `/soneta:config`), nie tylko ręcznie w UI.
- [ ] Kod enumerujący źródła praw sprawdza `AccessRight` przed odczytem kolumn.
- [ ] Komunikaty o braku prawa nie czytają kolumn wiersza.
- [ ] Logika biznesowa bez warunków na `AccessRight` ([safe-code.md](safe-code.md) §7.2).
- [ ] Testy praw wg wzorca z [integration-tests.md](integration-tests.md#testy-praw-obiektowych-irightssource).
- [ ] Prawa proste: stała z nazwą prawa + `[assembly: SimpleRight]`; sprawdzenie null-safe (`CurrentRole?[…]`).
