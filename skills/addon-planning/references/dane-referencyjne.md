# Dane referencyjne — inwentaryzacja modułów, tabel i folderów menu

Aby osadzić plan w istniejącym modelu danych platformy Soneta, **zinwentaryzuj strukturę na żywo z bibliotek** — nie używaj statycznych snapshotów (starzeją się). Narzędzia `scan-modules` i `scan-props` pochodzą ze skilla [programming](../../programming/SKILL.md), a `scan-folders` ze skilla [config](../../config/SKILL.md) — tam pełna semantyka wyników i opcji; tutaj tylko użycie planistyczne.

## Wymagania środowiska

- skompilowane biblioteki platformy Soneta — katalog z plikami `*.dll` (np. `bin/Debug` instalacji deweloperskiej lub katalog binariów zainstalowanego programu; jeśli nie znasz ścieżki, zapytaj użytkownika lub potraktuj jako otwartą kwestię),
- .NET SDK 10 oraz `dotnet-script` (`dotnet tool install -g dotnet-script`).

> **Ścieżki skryptów** poniżej są względne wobec katalogu tego dokumentu
> (`skills/addon-planning/references/` w repozytorium) i wskazują skrypty
> sąsiednich skilli — `../../programming/scripts/` i `../../config/scripts/`.

Gdy środowiska brak (np. planowanie koncepcyjne bez dostępu do buildu) — **nie zgaduj istniejących struktur**; zapisz inwentaryzację jako otwartą kwestię **blokującą** dla Etapu 2 i kontynuuj Etap 1.

## Kiedy w procesie

- **Etap 1, sekcja 1.5** — weryfikacja, w jakim zakresie standard platformy pokrywa zamierzony zakres modułu. Tu zwykle wygodniejszy jest `scan-folders`, bo funkcjonalność biznesową łatwiej dopasować do pozycji menu niż do surowej tabeli.
- **Etap 2 (sekcje 2.3, 2.5) i Etap 3 (sekcje 3.1–3.3)** — najintensywniejsze użycie `scan-modules`: konkretne moduły, `RowType`/`TableType`, wzorce projektowe.
- Gdy użytkownik wspomni o integracji z istniejącymi danymi (pracownicy, kontrahenci, towary) — zeskanuj strukturę i wskaż konkretne `RowType`/`TableType` po nazwach.
- Do drążenia pól wybranego rekordu użyj `scan-props`.

## `scan-modules` — perspektywa danych

Czyta metadane skompilowanych DLL-ek przez Roslyn, więc odzwierciedla dokładnie tę wersję platformy i dodatków, z którą pracuje użytkownik.

```bash
dotnet script ../../programming/scripts/scan-modules.csx -- <KatalogDll> > modules.md
```

Wynik zapisz do pliku roboczego (`modules.md`) i **czytaj selektywnie** — jest duży (kilkadziesiąt modułów, >1000 tabel). Namierzaj moduły przez `grep -n '^## ' modules.md`, potem czytaj tylko istotne sekcje.

Użycie kolumn wyniku w planie (pełna semantyka wartości: [scan-modules.md](../../programming/references/scan-modules.md)):

| Kolumna | Użycie w planie |
|---------|-----------------|
| `RowType` | obiekt biznesowy, do którego odwołujesz się w kodzie i relacjach |
| `TableType` | fizyczna tabela w bazie (`Session.Tables.*`) |
| `Guided` | wzorzec relacji nadrzędny-szczegółowy (inner): `root` = korzeń drzewa, `child: Pole→TypRow` = tabela podrzędna |
| `Konfig` | podział konfiguracyjne/operacyjne — stosuj ten sam w nowym module |
| `Interfaces` | relacje interfejsowe — punkty podpięcia do istniejących tabel |
| `Tytuł` / `Opis` | zrozumienie, czy struktura pokrywa potrzebę |

Korzystaj z inwentaryzacji, aby:
- sprawdzić, czy potrzebne struktury już istnieją (unikanie duplikacji),
- wskazać konkretne `RowType`/`TableType`, do których nowy moduł będzie się odwoływać,
- rozpoznać wzorce projektowe (podział konfiguracyjne/operacyjne, korzenie `Guided` i datapacki, relacje interfejsowe),
- zidentyfikować moduły współpracujące.

## `scan-folders` — perspektywa funkcjonalno-użytkowa

Komplementarnie do `scan-modules` (perspektywa danych) buduje drzewo folderów statycznych menu (`[assembly: FolderView]`): jakie listy i formularze program faktycznie udostępnia użytkownikowi i którą tabelą/`ViewInfo` stoi dana pozycja. Odpowiada na pytanie „czy użytkownik już dziś klika tę funkcję w standardzie?".

```bash
dotnet script ../../config/scripts/scan-folders.csx -- <KatalogDll> [<PrefiksSciezki>] [--flat] > folders.md
```

Pełne drzewo to >1000 węzłów — filtruj prefiksem ścieżki (np. `Handel`) i ewentualnie `--flat` do grepowania.
