# Safe Code - Zasady bezpiecznego kodu biznesowego Soneta

Lista zasad, które należy weryfikować **po każdym refaktoringu** oraz **przy każdym code review / PR** kodu biznesowego enova365 / Soneta Enterprise. Każda zasada jest sformułowana jako kontrolne pytanie — odpowiedź "nie" oznacza realne ryzyko (utrata danych, niespójność, race condition, błędna logika).

## Spis treści

- [1. Sesje i transakcje](#1-sesje-i-transakcje)
- [2. Mieszanie sesji i obiektów](#2-mieszanie-sesji-i-obiektów)
- [3. Thread-safety](#3-thread-safety)
- [4. Optimistic locking i konflikty](#4-optimistic-locking-i-konflikty)
- [5. Walidacja danych](#5-walidacja-danych)
- [6. Filtrowanie po stronie serwera](#6-filtrowanie-po-stronie-serwera)
- [7. Kod biznesowy vs UI](#7-kod-biznesowy-vs-ui)
- [8. ExecuteConfig - dane konfiguracyjne](#8-executeconfig---dane-konfiguracyjne)
- [9. Obsługa wyjątków i rollback](#9-obsługa-wyjątków-i-rollback)
- [10. Typy biznesowe](#10-typy-biznesowe)
- [11. Tłumaczenia i komunikaty](#11-tłumaczenia-i-komunikaty)
- [12. Logowanie](#12-logowanie)
- [13. Wydajność jako bezpieczeństwo](#13-wydajność-jako-bezpieczeństwo)
- [14. Czystość API publicznego](#14-czystość-api-publicznego)
- [15. Code review checklist (TL;DR)](#15-code-review-checklist-tldr)

---

## 1. Sesje i transakcje

### 1.1 Każda sesja w `using`

Sesja implementuje `IDisposable` — bez `using` zostanie ona w pamięci do GC, blokując zasoby i potencjalnie powodując nieskończony wzrost zużycia pamięci.

```csharp
// ŹLE
var session = login.CreateSession(readOnly: false, config: false, name: "Op");
// ... brak Dispose

// DOBRZE
using (var session = login.CreateSession(readOnly: false, config: false, name: "Op")) {
    // ...
}
```

---

## 2. Mieszanie sesji i obiektów

### 2.1 Nigdy nie używaj `Row` z innej sesji bezpośrednio

Każdy obiekt biznesowy żyje tylko w swojej sesji. Przekazanie go do innej sesji powoduje nieprzewidywalne zachowanie (stary stan, niespójne klucze).

```csharp
// ŹLE
faktura.Kontrahent = kontrahentZInnejSesji;

// DOBRZE
faktura.Kontrahent = session.Get(kontrahentZInnejSesji);
```

### 2.2 Nie zwracaj obiektów sesyjnych poza `using`

```csharp
// ŹLE - Towar po wyjściu z using jest "martwy"
public Towar PobierzTowar(Login login, string kod) {
    using (var session = login.CreateSession(true, false, "Pobierz")) {
        return session.GetTowary().Towary.WgKodu[kod];
    }
}
```

Zwracaj DTO / prymitywy, albo niech wywołujący kontroluje cykl życia sesji.

---

## 3. Thread-safety

### 3.1 Lista współdzielonych vs niewspółdzielonych

| Można używać jednocześnie w wielu wątkach | Nie wolno używać jednocześnie w wielu wątkach  |
|-------------------------------------------|------------------------------------------------|
| `BusApplication`, `Database`, `Login`     | `Session`, `Module`, `Table`, `Row`, `Context` |

### 3.2 Singleton / statyczne pola - bez sesyjnych referencji

Statyczne pole trzymające `Session`, `Row`, `Table` to klasyczna pułapka — pierwsza sesja "zawłaszcza" pole i kolejne wątki widzą cudze dane.

---

## 4. Optimistic locking i konflikty

### 4.1 Konflikty wykrywane w `Save()`

Soneta używa optimistic concurrency. Konflikt edycji (ktoś inny zapisał ten sam rekord) wybucha jako wyjątek z `session.Save()`. Jeśli kod łapie generyczny `Exception` i kontynuuje, użytkownik dostaje "zapisano" mimo że dane nie poszły do bazy.

```csharp
try {
    session.Save();
}
catch (RowConflictException ex) {
    // świadoma obsługa: refresh + powtórzenie lub eskalacja do użytkownika
}
```

### 4.2 Nie ignoruj wyjątku z `Save()`

`Save()` zgłaszający wyjątek = **nie zapisano**. Każdy taki przypadek musi mieć dedykowaną obsługę (retry, log, komunikat). Połknięcie wyjątku = utrata danych.

---

## 5. Walidacja danych

### 5.1 Walidacja przed `transaction.Commit()`

Wyjątek po `Commit()` ale przed `Save()` nie wycofuje zmian z bieżącej sesji — tylko z bazy. Następne `Save()` w tej samej sesji może je przepuścić niezauważenie. Walidacja musi rzucić wyjątek **przed** `Commit()` lub w event-handlerze `SavingRow`.

### 5.2 Komunikaty walidacyjne przez `Translate`

```csharp
throw new RowException(this, "Pole {0} jest wymagane".Translate(), nameof(Nazwa));
```

---

## 6. Filtrowanie po stronie serwera

### 6.1 Nie iteruj całej tabeli z `if` w pamięci

```csharp
// ŹLE - ściąga całą tabelę
foreach (Towar t in tm.Towary.WgKodu) {
    if (t.Aktywny && t.Cena > 100) { ... }
}

// DOBRZE - warunek wykonany przez serwer
foreach (Towar t in tm.Towary[(Towar t) => t.Aktywny && t.Cena > 100]) { ... }
```

Szczegóły wyrażeń `RowCondition` (LINQ-like, predykaty, kompozycja) — patrz [rowcondition.md](rowcondition.md).

### 6.2 Nie używaj `View` w kodzie biznesowym

`View` należy do warstwy UI. W kodzie biznesowym używaj `SubTable[condition]`. Szczegóły — patrz [viewinfo.md](viewinfo.md).

### 6.3 Nie wczytuj całych tabel kartotekowych ani operacyjnych

Tabele dzielą się na trzy kategorie pod kątem rozmiaru i bezpieczeństwa pełnego skanu:

| Kategoria | Przykłady | Pełny skan? |
|-----------|-----------|-------------|
| **Konfiguracyjne** (stałe, niewielkie) | `Jednostki`, `DefinicjeDokumentow`, `Stawki VAT` | **Dozwolony** |
| **Kartotekowe** (rosną z biznesem) | `Towary`, `Kontrahenci`, `Pracownicy` | **Zabroniony** bez filtra |
| **Operacyjne** (rosną w czasie, guided) | `DokHandlowe`, `Zapisy`, `WyplatyElementy` | **Zabroniony** — wymagany zakres czasowy |

**Reguła dla tabel operacyjnych guided** (zawierających daty): dostęp prawie zawsze musi być ograniczony do określonego okresu.

```csharp
// ŹLE - pobiera wszystkie dokumenty od początku istnienia bazy
foreach (DokumentHandlowy d in hm.DokHandlowe.WgNumeru) { ... }

// DOBRZE - zakres czasowy
var od = new Date(2026, 1, 1);
var doD = Date.Today;
foreach (DokumentHandlowy d in hm.DokHandlowe[(DokumentHandlowy d) => d.Data >= od && d.Data <= doD]) { ... }
```

**Reguła dla danych operacyjnych nie-guided** (pozycje, składniki, zapisy szczegółowe): używaj ich w zakresie obiektu guided root (np. pozycje konkretnego dokumentu, składniki konkretnej wypłaty). Jeśli musisz iterować poprzecznie, **dodaj filtr czasowy analogiczny do tabel guided**.

```csharp
// DOBRZE - w zakresie roota
foreach (PozycjaDokHandlowego p in faktura.Pozycje) { ... }

// ŹLE - iteracja po wszystkich pozycjach historycznych
foreach (PozycjaDokHandlowego p in hm.PozycjeDokHan.WgDokument) { ... }

// AKCEPTOWALNE - poprzeczna iteracja z filtrem czasowym (przez root)
var od = new Date(2026, 1, 1);
foreach (DokumentHandlowy d in hm.DokHandlowe.WgDaty[d => d.Data >= od]) {
    foreach (var p in d.Pozycje) { ... }
}
```

---

## 7. Kod biznesowy vs UI

### 7.1 Brak referencji do UI w kodzie biznesowym

Kod, który będzie wywoływany z workerów, schedulera, importu, API — **nie może** używać:
- `View`, `ViewInfo` (zamiast tego: `SubTable[condition]`)
- `IsReadOnlyXxx`, `IsVisibleXxx`, `IsEnabledXxx`, `GetListXxx`, `GetAppearanceXxx`
- okien dialogowych, `MessageBox`, `IUIServices`

### 7.2 Nie sprawdzaj `AccessRight` w logice biznesowej

```csharp
// ŹLE - logika biznesowa nie ma znać uprawnień
if (Table.AccessRight == AccessRights.Denied) return;
```

Prawa dostępu są warstwą UI / autoryzacji. Logika biznesowa zakłada poprawne wywołanie; egzekucja praw jest gdzie indziej.

---

## 8. ExecuteConfig - dane konfiguracyjne

### 8.1 Nie zwracaj obiektów sesyjnych z `ExecuteConfig`

`ExecuteConfig` korzysta z sesji współdzielonej między wątkami. Obiekt zwrócony na zewnątrz może być w międzyczasie unieważniony.

```csharp
// ŹLE
return login.ExecuteConfig(s => s.GetHandel().DefDokHandlowych.WgSymbolu["ZK"]);

// DOBRZE - zwracamy prymityw / kopię
return login.ExecuteConfig(s => s.GetHandel().DefDokHandlowych.WgSymbolu["ZK"]?.Kategoria);
```

### 8.2 Nie używaj `Session.ConfigSession` — używaj `Session.ExecuteConfig()`

`Session.ConfigSession` daje bezpośredni uchwyt do sesji konfiguracyjnej, którego cykl życia nie jest kontrolowany przez wywołującego — łatwo o wyciek obiektów sesyjnych poza wątek docelowy lub o korzystanie z nieaktualnego stanu.

```csharp
// ŹLE
var def = session.ConfigSession.GetHandel().DefDokHandlowych.WgSymbolu["ZK"];

// DOBRZE
var kategoria = session.ExecuteConfig(s =>
    s.GetHandel().DefDokHandlowych.WgSymbolu["ZK"]?.Kategoria);
```

`ExecuteConfig` ogranicza zasięg sesji konfiguracyjnej do bloku lambda i wymusza zwracanie prymitywów (patrz §8.1).

---

## 9. Obsługa wyjątków i rollback

### 9.1 Nie łap `Exception` bez konkretu

Generyczne `catch (Exception) { /* log */ }` ukrywa konflikty, błędy walidacji, brak praw, błędy I/O. Każdy z nich wymaga innej reakcji. Łap konkretne typy: `RowConflictException`, `RowException`, `BusException`, `LoginException`.

### 9.2 Nie rzucaj nieokreślonego `Exception`

`throw new Exception("...")` zmusza wywołującego do łapania `Exception` ogólnie, co (patrz §9.1) tłumi sygnały o realnej przyczynie błędu. Rzucaj **typowane** wyjątki, dobrane do warstwy:

| Sytuacja | Wyjątek |
|----------|---------|
| Naruszenie reguły biznesowej powiązane z rekordem | `RowException(row, "komunikat".Translate())` |
| Ogólny błąd logiki biznesowej (bez konkretnego wiersza) | `BusException` |
| Nieprawidłowy stan obiektu / nieprawidłowe wywołanie API | `InvalidOperationException` |
| Nieprawidłowy argument | `ArgumentException`, `ArgumentNullException`, `ArgumentOutOfRangeException` |
| Funkcja jeszcze nie obsłużona | `NotSupportedException`, `NotImplementedException` |

```csharp
// ŹLE
throw new Exception("Brak definicji dokumentu");

// DOBRZE - błąd biznesowy bez konkretnego wiersza
throw new BusException("Brak definicji dokumentu {0}".TranslateFormat(symbol));

// DOBRZE - błąd walidacyjny związany z wierszem
throw new RowException(faktura, "Pole {0} jest wymagane".Translate(), nameof(faktura.Kontrahent));

// DOBRZE - nieprawidłowy stan / niepoprawne wywołanie
throw new InvalidOperationException("Worker wymaga otwartej transakcji edycyjnej");
```

---

## 10. Typy biznesowe

### 10.1 Pieniądze, ilości, daty - dedykowane typy

| Pojęcie | Typ | NIE używaj |
|---------|-----|------------|
| Kwota walutowa | `Currency`, `DoubleCy` | `decimal`, `double` (gubi walutę) |
| Ilość z jednostką | `Quantity` | `decimal`, `int` (gubi jednostkę) |
| Data biznesowa | `Soneta.Types.Date` | `DateTime` (gubi semantykę "tylko data") |
| Procent | `Percent` | `double` |

### 10.2 `Date.Today` zamiast `DateTime.Today`

`Date.Today` honoruje datę biznesową aplikacji (np. zamknięty miesiąc). `DateTime.Today` zwraca datę systemową.

---

## 11. Tłumaczenia i komunikaty

### 11.1 Każdy łańcuch widoczny dla użytkownika przez `.Translate()`

```csharp
throw new RowException(this, "Pole jest wymagane".Translate());
throw new RowException(this, "Pole {0} jest wymagane".TranslateFormat(nameof(Nazwa)));
```

### 11.2 Stałe konfiguracyjne nie przez `Translate`

`Translate` jest dla tekstu UI / komunikatów. Klucze, symbole, kody nie powinny być tłumaczone — to dane techniczne.

### 11.3 `[TranslateIgnore]` dla pól nie do tłumaczenia

Szczegóły mechanizmów tłumaczeń — patrz [translations-logging.md](translations-logging.md).

---

## 12. Logowanie

### 12.1 Nie loguj danych wrażliwych (PII, hasła, tokeny)

PESEL, NIP w nadmiarze, hasła, klucze API nie trafiają do logów.

---

## 13. Wydajność jako bezpieczeństwo

Wolny kod = timeouty = nieskończone retry = blokady / data corruption.

### 13.1 Pętla po tabeli w transakcji edycyjnej - krótka

Długa transakcja blokuje innych użytkowników na poziomie optimistic-lock (większe szanse na konflikt). Operacja > 30 sekund powinna być dzielona na paczki.

### 13.2 Filtrowanie serwerowe zamiast `.ToList().Where(...)`

`ToList()` materializuje całą tabelę do pamięci. To anti-pattern w kodzie biznesowym.

---

## 14. Czystość API publicznego

### 14.1 Publiczne metody dokumentują kontrakt sesyjny

Metoda przyjmująca `Login` zarządza sesją sama. Metoda przyjmująca `Session` / `ISessionable` deleguje zarządzanie do wywołującego. **Nie mieszaj** — albo metoda otwiera własną sesję, albo dostaje ją z zewnątrz.

### 14.2 Brak `static` metod modyfikujących stan biznesowy

`static void DodajTowar(string kod)` → skąd `Login`? Globalne pole? Pułapka thread-safety. Zawsze przekazuj `Login` / `Session` jawnie.

### 14.3 Nazewnictwo zgodne z konwencją projektu

| Element | Konwencja |
|---------|-----------|
| Parametr `bool` | nazwany (np. `readOnly: true`) |
| Logika biznesowa | nazwy polskie (`Towar`, `Faktura`) |
| Klasy systemowe | nazwy angielskie (`Session`, `Module`) |

---

## 15. Code review checklist (TL;DR)

Do szybkiej weryfikacji PR-a / refaktoringu:

**Sesje i obiekty sesyjne**
- [ ] Każda sesja w `using` (§1.1)
- [ ] Obiekty z innych sesji przepuszczone przez `session.Get(...)` (§2.1)
- [ ] Brak zwracania obiektów sesyjnych poza `using` (§2.2)
- [ ] Brak współdzielenia `Session`/`Row`/`Table`/`Module`/`Context` między wątkami (§3.1)
- [ ] Brak statycznych pól trzymających obiekty sesyjne (§3.2)

**Konflikty i wyjątki**
- [ ] `RowConflictException` z `Save()` ma dedykowaną obsługę (§4.1)
- [ ] Brak ignorowania wyjątku z `Save()` (§4.2)
- [ ] Brak `catch (Exception)` bez konkretnego typu (§9.1)
- [ ] Brak `throw new Exception(...)` — używane typowane (`RowException`, `BusException`, `InvalidOperationException`, …) (§9.2)

**Walidacja**
- [ ] Walidacja rzuca wyjątek **przed** `Commit()` lub w `SavingRow` (§5.1)
- [ ] Komunikaty błędów przez `.Translate()` (§5.2, §11.1)

**Dane**
- [ ] Filtrowanie przez `SubTable[condition]`, nie pętle z `if` w pamięci (§6.1)
- [ ] Brak `View` w kodzie biznesowym (§6.2)
- [ ] Brak pełnego skanu tabel kartotekowych; tabele operacyjne guided z zakresem czasowym; nie-guided w zakresie roota (§6.3)
- [ ] Brak referencji do UI (`IsXxx`, `GetXxx`, `MessageBox`, `IUIServices`) w kodzie biznesowym (§7.1)
- [ ] Brak sprawdzania `AccessRight` w logice biznesowej (§7.2)
- [ ] Z `ExecuteConfig` wracają tylko prymitywy / kopie, nie obiekty sesyjne (§8.1)
- [ ] Brak `Session.ConfigSession` — dostęp do konfiguracji przez `Session.ExecuteConfig()` (§8.2)

**Typy**
- [ ] Pieniądze: `Currency`/`DoubleCy` z walutą (§10.1)
- [ ] Ilości: `Quantity` z jednostką (§10.1)
- [ ] Daty: `Soneta.Types.Date`, `Date.Today` (§10.1, §10.2)

**Tłumaczenia i logowanie**
- [ ] Łańcuchy widoczne dla użytkownika przez `.Translate()` (§11.1)
- [ ] Klucze / symbole / kody bez `Translate` (§11.2)
- [ ] Brak PII / haseł / tokenów w logach (§12.1)

**Wydajność**
- [ ] Krótkie transakcje edycyjne, duże operacje dzielone na paczki (§13.1)
- [ ] Brak `.ToList().Where(...)` w miejsce filtra serwerowego (§13.2)

**API i konwencje**
- [ ] Spójny kontrakt sesyjny — `Login` albo `Session`, nie mieszane (§14.1)
- [ ] Brak `static` metod modyfikujących stan biznesowy (§14.2)
- [ ] Parametry `bool` nazwane (§14.3)
- [ ] Nazewnictwo: logika biznesowa po polsku, klasy systemowe po angielsku (§14.3)
