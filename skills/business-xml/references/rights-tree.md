# Drzewo praw do danych — pliki `*.rightstree.xml`

Plik `*.rightstree.xml` mówi platformie, **w którym miejscu drzewa uprawnień** pojawią się prawa
do tabel biblioteki. Bez wpisu tabela nie znika z systemu praw — ląduje w gałęzi
`Dodatki/<Moduł>`, czyli w koszu na wszystko, czego nie umieszczono świadomie.

## Reguła: do pliku trafiają wyłącznie korzenie drzewa praw

Prawa dziedziczą się po **relacji nadrzędnej praw**. Tabela, która taką relację ma, dostaje
miejsce w drzewie po obiekcie nadrzędnym i **nie ma własnego wpisu**. Wpis dostaje tylko tabela,
dla której takiej relacji nie da się wyznaczyć.

Relacja nadrzędna praw wybierana jest z relacji tabeli w **tej kolejności** (pierwsza pasująca
grupa wygrywa):

1. relacja z `relguided` (`inner` / `outer`),
2. relacja z `relright="Primary"`,
3. relacja z `reldefault="true"`,
4. relacja z `relright="true"` — **z pominięciem relacji wskazujących obiekt będący źródłem praw
   obiektowych** (tabela z `<interface>IRightsSource</interface>`).

| Relacje tabeli w `business.xml` | Wpis w rightstree | Dlaczego |
|---|---|---|
| brak relacji do innych obiektów | **TAK** | korzeń — nie ma od kogo dziedziczyć |
| `relguided="inner"` / `relguided="outer"` | NIE | dziecko obiektu nadrzędnego (`guided="Root"`) |
| `relright="Primary"` albo `relright="true"` do zwykłego obiektu | NIE | prawa z obiektu wskazanego tą relacją |
| `relright="true"` **do obiektu `IRightsSource`** | **TAK** | dostępem steruje prawo obiektowe; w drzewie struktury tabela zostaje korzeniem |
| `relright="other"` / `relright="config"` / `relright="false"` | **TAK** | to nie są relacje praw |
| tylko `reldefault="true"` | NIE | relacja domyślna pełni rolę nadrzędnej |

Praktyczny skrót: **`relguided` albo `relright="true"` do zwykłego obiektu = brak wpisu.**
Dwa przypadki łatwo przeoczyć: relacja `relright="true"` prowadząca do **źródła praw
obiektowych** oraz `relright="other"`/`"config"` — w obu tabela **pozostaje korzeniem** i wpis
jest potrzebny.

Gdy relacja `relguided` nie ma jednocześnie `relright="true"`, platforma i tak przyjmuje ją jako
relację praw, ale zapisuje ostrzeżenie do logu — docelowo obie deklaracje powinny występować razem.

## Minimalny przykład

Moduł `Serwis` z czterema tabelami:

| Tabela | Definicja | Korzeń? |
|---|---|---|
| `ZlecenieSerwisowe` | `guided="Root"`, bez relacji praw | tak |
| `PozycjaZlecenia` | relacja do zlecenia z `relguided="inner"` | nie — prawa ze zlecenia |
| `DefinicjaZlecenia` | `guided="Root" config="true"`, `<interface>IRightsSource</interface>` | tak |
| `RozliczenieZlecenia` | `guided="Root"`, relacja do definicji z `relright="true"` | tak — definicja jest źródłem praw obiektowych |

```xml
<?xml version="1.0" encoding="utf-8"?>
<RightTreePaths>
  <Module Name="Serwis" Path="Serwis/Zlecenia">
    <Type Name="Firma.Serwis.ZlecenieSerwisowe" Path="" />
    <Type Name="Firma.Serwis.RozliczenieZlecenia" Path="" />
    <Type Name="Firma.Serwis.DefinicjaZlecenia" Path="" />
    <!-- PozycjaZlecenia: relguided="inner" → prawa ze zlecenia, wpisu NIE MA -->
  </Module>
</RightTreePaths>
```

Efekt w drzewie uprawnień: `Program/Serwis/Zlecenia/ZlecenieSerwisowe`,
`Program/Serwis/Zlecenia/RozliczenieZlecenia` oraz — bo tabela jest konfiguracyjna —
`Konfiguracja/Serwis/Zlecenia/DefinicjaZlecenia`.

## Struktura pliku

```
RightTreePaths
└── Module (grupa porządkowa, jedna lub wiele)
    └── Type (jeden wpis na korzeń drzewa praw)
```

### `<Module>`

| Atrybut | Znaczenie |
|---|---|
| `Name` | Nazwa modułu — etykieta porządkowa. Ta sama nazwa może wystąpić w kilku grupach, gdy tabele modułu rozchodzą się po różnych ścieżkach. |
| `Path` | Domyślna ścieżka dla typów w grupie. Segmenty rozdziela `/`, każdy tworzy poziom drzewa. |

### `<Type>`

| Atrybut | Znaczenie |
|---|---|
| `Name` | **Pełna** nazwa typu wiersza: `Przestrzeń.Klasa`. Typ zagnieżdżony zapisuje się z plusem: `Przestrzeń.Klasa+Podtyp`. |
| `Path` | **Obowiązkowy.** Pusty (`Path=""`) = weź ścieżkę z `Module`. Niepusty = własna ścieżka zamiast modułowej. |
| `Config` | `"true"` wymusza gałąź „Konfiguracja" dla tabeli, która nie jest konfiguracyjna. Tabela `config="true"` trafia tam bez tego atrybutu. |

Wpis odnosi się do **klasy wiersza**, nie do nazwy tabeli w bazie ani nazwy w `business.xml` —
przy zmianie przestrzeni nazw albo nazwy klasy wpis trzeba poprawić.

## Gałęzie drzewa i prefiksy

Ścieżki z pliku są doklejane do gałęzi, którą platforma wybiera sama. **Prefiksu nigdy nie
wpisuje się w `Path`.**

| Gałąź | Kiedy |
|---|---|
| `Program` | zwykła tabela operacyjna |
| `Konfiguracja` | tabela `config="true"` albo wpis z `Config="true"` |
| `Wielokrotne` | typy udostępniane przez relacje interfejsowe i wyjątki praw |
| `Dodatki/<Moduł>` | **brak wpisu** dla korzenia (lub brak całego pliku) |

Gałąź `Dodatki` jest akceptowalna dla prostego dodatku, który nie potrzebuje własnego miejsca
w drzewie. Dla modułu z kilkoma tabelami warto podać ścieżkę — inaczej operator konfigurujący
role szuka uprawnień w innym miejscu niż reszta funkcji.

## Osadzanie w bibliotece

Plik musi być **zasobem osadzonym** (`EmbeddedResource`), a jego nazwa wynika z nazwy biblioteki:
odcinany jest pierwszy segment nazwy, reszta plus `.rightstree.xml`.

| Biblioteka | Nazwa pliku |
|---|---|
| `Firma.Serwis` | `Serwis.rightstree.xml` |
| `Firma.Web.Business` | `Web.Business.rightstree.xml` |
| `Serwis` (bez kropki) | `Serwis.rightstree.xml` |

Dopasowanie idzie po końcówce nazwy zasobu, więc plik może leżeć w podkatalogu projektu.

**Soneta SDK** dołącza wszystkie `*.rightstree.xml` automatycznie — w projekcie dodatku nie trzeba
nic dopisywać. W projekcie na czystym `Microsoft.NET.Sdk` dodaj regułę ręcznie:

```xml
<ItemGroup>
  <None Remove="**\*.rightstree.xml" />
  <EmbeddedResource Include="**\*.rightstree.xml" />
</ItemGroup>
```

## Synchronizacja z `business.xml`

Plik nie aktualizuje się sam — to drugi krok każdej zmiany struktury danych.

| Zmiana w `business.xml` | Co zrobić w rightstree |
|---|---|
| nowa tabela bez relacji praw | dodaj `<Type>` |
| nowa tabela z relacją nadrzędną praw | nic — dziedziczy miejsce po obiekcie nadrzędnym |
| usunięcie tabeli | usuń `<Type>` |
| zmiana nazwy klasy lub przestrzeni nazw | popraw `Name` we wpisie |
| dodanie relacji nadrzędnej praw do istniejącej tabeli | **usuń** jej wpis — przestała być korzeniem |
| usunięcie relacji praw z tabeli | **dodaj** wpis — stała się korzeniem |
| dodanie `IRightsSource` do obiektu wskazywanego relacją `relright="true"` | **dodaj** wpisy tabelom, które przez tę relację dziedziczyły miejsce — przestały być dziećmi |
| dodanie/usunięcie `config="true"` | sprawdź gałąź (Konfiguracja vs Program), ewentualnie `Config` |

## Objawy błędów

| Objaw | Przyczyna | Naprawa |
|---|---|---|
| Prawa do nowej tabeli w gałęzi `Dodatki/<Moduł>` | brak wpisu dla korzenia | dodaj `<Type>` z właściwym `Path` |
| Wszystkie tabele biblioteki w `Dodatki` | brak pliku, zła nazwa pliku albo plik nie jest zasobem osadzonym | popraw nazwę wg konwencji i regułę `EmbeddedResource` |
| Wpis jest, a tabela dalej nie w swojej gałęzi | literówka w `Name` albo typ nie jest korzeniem (ma relację praw) | porównaj pełną nazwę klasy; sprawdź relacje tabeli |
| Błąd budowy drzewa praw przy starcie | ten sam typ wpisany w dwóch plikach | zostaw jeden wpis |
| Błąd odczytu pliku | `<Type>` bez atrybutu `Path` | dodaj `Path=""` |
| Wpis istnieje, ale nic nie robi | typ nie jest korzeniem (ma relację nadrzędną praw) | usuń martwy wpis, żeby nie mylił |
| Prawa do tabeli w `Dodatki` mimo relacji `relright="true"` | relacja wskazuje obiekt `IRightsSource` — jest pomijana, tabela pozostaje korzeniem | dodaj wpis `<Type>` |

## Checklista — po każdej zmianie w `business.xml`

- [ ] Każda nowa tabela bez relacji nadrzędnej praw ma wpis `<Type>` — w tym tabela, której
      jedyna relacja `relright="true"` wskazuje obiekt `IRightsSource`.
- [ ] Żadna tabela z relacją nadrzędną praw (`relguided`, `relright="Primary"`, `relright="true"`
      do zwykłego obiektu, `reldefault="true"`) nie ma wpisu — wpisy martwe usuwamy.
- [ ] `Name` to pełna nazwa klasy wiersza, typy zagnieżdżone z `+`.
- [ ] Każdy `<Type>` ma atrybut `Path` (choćby pusty).
- [ ] `Path` bez prefiksu `Program`/`Konfiguracja`/`Wielokrotne`.
- [ ] Usunięte tabele nie zostawiły wpisów.
- [ ] Nazwa pliku zgodna z nazwą biblioteki; plik dołączony jako `EmbeddedResource`.
- [ ] Tabele konfiguracyjne trafiają do gałęzi Konfiguracja (z `config="true"` lub `Config="true"`).

## Powiązane dokumenty

- [relations-guide.md](relations-guide.md) — relacje `relright` i `relguided`, od których zależy,
  czy tabela jest korzeniem.
- [table-reference.md](table-reference.md) — atrybuty `guided` i `config` tabeli.
- [rights-source.md](../../programming/references/rights-source.md) — **prawa obiektowe** (`IRightsSource`):
  inny mechanizm, sterujący dostępem do danych przez wskazany obiekt konfiguracyjny; drzewo praw
  opisane tutaj decyduje tylko o miejscu tabeli w konfiguracji uprawnień.
- [addon-planning](../../addon-planning/SKILL.md) — planowanie uprawnień na etapie architektury dodatku.
