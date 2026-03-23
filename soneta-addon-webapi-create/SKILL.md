---
name: nowy-dodatek-enova365
description: Tworzenie nowych dodatków WebAPI dla systemu enova365. Używaj tego skilla, gdy chcesz stworzyć nowy projekt rozszerzający funkcjonalność enova365 o nowe punkty końcowe DynamicAPI.
---

# Nowy Dodatek enova365

Ten skill pomaga w szybkim tworzeniu nowych projektów dodatków WebAPI dla systemu enova365, wykorzystując nowoczesne standardy (Soneta.Sdk, .NET 8).

## Przepływ pracy

1.  **Analiza wymagań**: Zidentyfikuj moduł enova365 (np. Handel, Kadry), który ma zostać rozszerzony.
2.  **Inicjalizacja środowiska**: Skopiuj `nuget-config-template.xml` jako `nuget.config` w katalogu głównym projektu.
3.  **Inicjalizacja projektu**: Utwórz folder projektu i plik `.csproj` na bazie szablonu.
4.  **Bootstrap (Kluczowy krok)**: Jeśli katalog `bin/Debug/` nie istnieje, **musisz najpierw skompilować minimalny projekt** (`dotnet build`), aby pobrać NuGet-y i wygenerować biblioteki DLL potrzebne do nauki i inspekcji.
5.  **Inspekcja Metadanych (Detective Work)**: Użyj narzędzi CLI do inspekcji DLLi, aby poznać poprawne nazwy tabel i klas (patrz sekcja poniżej).
6.  **Implementacja**: Stwórz interfejs i klasę API, używając pełnych nazw typów w atrybutach.
7.  **Weryfikacja**: Uruchom `dotnet build`.

## Inspekcja Metadanych (Detective Work)

Jeśli nie wiesz, jak nazywa się tabela w module (np. `Dokumenty` czy `DokHandlowe`) lub jakie metody i właściwości posiada dana klasa, skorzystaj z wygenerowanych plików metadanych w folderze `.gemini/skills/nowy-dodatek-enova365/metadata/`. Są one wyczyszczone z symboli kompilatora i znacznie czytelniejsze.

### 1. Korzystanie z plików metadanych
Użyj `grep` na plikach `.txt` wewnątrz skilla, aby szybko znaleźć definicję klasy:

```bash
# Szukanie klasy i jej członków (np. DokumentHandlowy)
grep -A 100 "Type: Soneta.Handel.DokumentHandlowy" .gemini/skills/nowy-dodatek-enova365/metadata/Soneta.Handel.txt

# Szukanie konkretnej metody w całej bibliotece
grep "Meth: .*Zatwierdz" .gemini/skills/nowy-dodatek-enova365/metadata/Soneta.Handel.txt
```

### 2. Generowanie nowych metadanych (ApiDumper)
Jeśli potrzebujesz informacji z biblioteki, której nie ma jeszcze w `metadata/` (np. `Soneta.KadryPlace.dll`), użyj przygotowanego narzędzia:

```bash
dotnet run --project .gemini/skills/nowy-dodatek-enova365/scripts/ApiDumper/ApiDumper.csproj -- bin/Debug/Soneta.KadryPlace.dll .gemini/skills/nowy-dodatek-enova365/metadata/Soneta.KadryPlace.txt
```

### 3. Bezpośrednia inspekcja DLL (Fallback)
Jeśli z jakiegoś powodu nie możesz użyć plików metadanych, użyj `strings` (pamiętaj o filtrach):

```bash
# Szukanie nazw tabel i właściwości w module
strings bin/Debug/Soneta.Handel.dll | grep -E "Module|Table|get_" | grep "Dok"
```

## Sprawdzone wzorce enova365

### 1. Pobieranie Modułów
Zawsze używaj `GetInstance` lub dostępu przez `Modules[]`:
```csharp
var hm = HandelModule.GetInstance(workSession);
// lub (jeśli GetInstance nie jest dostępne)
var hm = (HandelModule)workSession.Modules[typeof(HandelModule)];
```

### 2. Tabele i Rekordy (Moduł Handel)
| Cel | Klasa Rekordu | Tabela w `HandelModule` |
| :--- | :--- | :--- |
| **Dokument Handlowy** | `DokumentHandlowy` | `hm.DokHandlowe` |
| **Pozycja Dokumentu** | `PozycjaDokHandlowego` | `hm.PozycjeDokHan` |
| **Magazyn** | `Soneta.Magazyny.Magazyn` | `hm.Magazyny` |

### 3. Wyszukiwanie (LINQ vs Indexery)
Jeśli `WgKodu[]` nie działa, użyj bezpiecznego rzutowania na `IEnumerable`:
```csharp
using System.Collections;
// ...
var magazyn = ((IEnumerable)hm.Magazyny).Cast<Soneta.Magazyny.Magazyn>()
    .FirstOrDefault(m => m.Symbol == "KOD");
```

## Kluczowe atrybuty (Implementacja)

W pliku implementacji używaj **pełnych nazw typów** (razem z namespace) w atrybutach assembly:

```csharp
[assembly: Service(typeof(Namespace.Interfaces.IYourApi), typeof(Namespace.YourApi), ServiceScope.Session)]
[assembly: DynamicApiController(typeof(Namespace.Interfaces.IYourApi), typeof(Namespace.YourApi))]
```

## Praca z Sesją i Transakcją

```csharp
using (var workSession = _session.Login.CreateSession(false, false))
using (var transaction = workSession.Logout(true))
{
    // Logika...
    transaction.Commit();
}
```
