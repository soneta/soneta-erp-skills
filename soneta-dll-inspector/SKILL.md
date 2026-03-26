---
name: soneta-dll-inspector
description: "Inspekcja publicznego API bibliotek Sonety z DLL-ek. Używaj tego skilla gdy AI potrzebuje poznać nazwy klas, metod, właściwości z dowolnej biblioteki enova365/Soneta — np. przed implementacją dodatku WebAPI, formularza UI, czy modelu danych. Skill nie trzyma predefiniowanych danych — generuje metadane on-demand z DLL-ek projektu użytkownika."
---

# Inspekcja API bibliotek Soneta

## Kiedy używać

Gdy AI potrzebuje poznać publiczne API biblioteki Sonety:
- Jakie klasy/interfejsy są w danym module?
- Jakie właściwości i metody ma konkretna klasa?
- Jak się nazywa metoda do konkretnej operacji?

## Wymaganie wstępne

Projekt użytkownika musi być zbudowany żeby DLL-ki były dostępne:

```bash
dotnet build
```

DLL-ki Sonety pojawią się w `bin/Debug/` projektu (np. `Soneta.Handel.dll`, `Soneta.Business.dll`).

## Jak uruchomić ApiDumper

```bash
# Ścieżka do ApiDumper relatywna do katalogu tego skilla
dotnet run --project <ścieżka-do-skilla>/scripts/ApiDumper/ApiDumper.csproj -- <argumenty>
```

Jeśli ApiDumper nie był wcześniej budowany, `dotnet run` zbuduje go automatycznie.

## Tryby pracy

### 1. Lista typów (domyślny)

Zwraca kompaktową listę wszystkich publicznych typów z DLL:

```bash
dotnet run --project .../ApiDumper.csproj -- bin/Debug/Soneta.Handel.dll
```

Output:
```
Public types (245):

  class            Soneta.Handel.DokumentHandlowy
  class            Soneta.Handel.PozycjaDokHandlowego
  interface        Soneta.Handel.IDokumentHandlowy
  enum             Soneta.Handel.StanDokumentuHandlowego
  ...
```

Użyj tego trybu na początek, żeby zobaczyć co jest dostępne.

### 2. Szczegóły typu (`--type`)

Zwraca właściwości i metody konkretnej klasy (bez szumu z System.Object):

```bash
dotnet run --project .../ApiDumper.csproj -- bin/Debug/Soneta.Handel.dll --type DokumentHandlowy
```

Output:
```
Type: Soneta.Handel.DokumentHandlowy
  Base: Soneta.Handel.DokumentHandlowyRow

  Properties:
    Kontrahent Kontrahent { get/set }
    Decimal Netto { get }
    StanDokumentuHandlowego Stan { get }
    ...

  Methods:
    Void Zatwierdz()
    Void Anuluj()
    ...
```

Szuka po krótkiej nazwie (`DokumentHandlowy`) lub pełnej (`Soneta.Handel.DokumentHandlowy`).

### 3. Wyszukiwanie (`--search`)

Szuka typów i członków pasujących do frazy:

```bash
dotnet run --project .../ApiDumper.csproj -- bin/Debug/Soneta.Handel.dll --search Zatwierdz
```

Output:
```
Type: Soneta.Handel.DokumentHandlowy
  Meth: Void Zatwierdz()
  Meth: Void ZatwierdzDoWysylki()

Type: Soneta.Handel.ZamowienieHandlowe
  Meth: Void Zatwierdz()
```

## Standardowe biblioteki Soneta

| DLL | Zawartość |
|-----|-----------|
| `Soneta.Business.dll` | Bazowe klasy ORM (Session, Module, Table, Row) |
| `Soneta.Handel.dll` | Dokumenty handlowe, pozycje, magazyny |
| `Soneta.CRM.dll` | Kontrahenci, kontakty |
| `Soneta.Kasa.dll` | Raporty kasowe, rozrachunki |
| `Soneta.KadryPlace.dll` | Kadry i płace |
| `Soneta.Core.dll` | Konfiguracja, użytkownicy, uprawnienia |
| `Soneta.Data.dll` | Warstwa dostępu do danych |
| `Soneta.Types.dll` | Typy bazowe, DynamicApi atrybuty |

## Workflow

1. Zbuduj projekt: `dotnet build`
2. Lista typów: `ApiDumper bin/Debug/Soneta.Handel.dll` — zorientuj się co jest
3. Drill-down: `ApiDumper ... --type DokumentHandlowy` — zbadaj konkretną klasę
4. Szukaj: `ApiDumper ... --search Zatwierdz` — znajdź metody po nazwie
