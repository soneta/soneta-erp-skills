---
name: tools
description: >
  Narzędzia deweloperskie wiersza poleceń (CLI) platformy Soneta (enova365, Triva) — dla partnerów
  i zespołu Soneta (repozytorium źródłowe programu). Używaj gdy użytkownik: (1)
  zarządza bazami przez `dbmgr` — tworzy, rejestruje, konwertuje, backup/restore, licencje,
  rozszerzenia, analiza, kompilacja; (2) przygotowuje bazę testową/demo z CLI albo automatyzuje
  operacje na bazach w skrypcie/CI; (3) testuje działającą aplikację przez `buscall` — zdalnie
  steruje programem (nawigacja, formularze, gridy, edycja) i robi zrzuty ekranu; (4) uruchamia
  ramkę `SonetaFrame` (`SonetaFrameNew`), konfiguruje źródła baz (`demo:`, `http`, `docker:`,
  `process:`, `orchestrator:`) albo pyta o `Settings_Product.json`; (5) pyta o składnię, komendy
  lub opcje `dbmgr`, `buscall`, `callmcp`, `SonetaFrame`; (6) wspomina „zarządzanie bazą enova",
  „baza demo", „konwersja bazy", „testowanie na żywej aplikacji", „ramka Soneta". Sięgnij też, gdy
  inny skill potrzebuje operacji na bazie lub weryfikacji zmian na uruchomionej aplikacji.
---

# Soneta Tools — narzędzia deweloperskie CLI

Skill dokumentuje **narzędzia wiersza poleceń** stosowane w Soneta do programowania
i zarządzania platformą (enova365, Triva). Każde narzędzie ma własny, dokładny
reference oparty na rzeczywistym interfejsie (`--help` + kod źródłowy).

## Mapa skilla — które narzędzie do czego

Każde narzędzie ma **referencję funkcji/parametrów** oraz osobny dokument o **konkretnym zastosowaniu**.

| Narzędzie | Dokument | Rodzaj | Co zawiera |
|---|---|---|---|
| **`dbmgr`** | [references/dbmgr.md](references/dbmgr.md) | referencja | Zarządzanie **bazami danych** z CLI: tworzenie/rejestracja/kasowanie, konwersja, backup/restore (`.bac`/`.zip`), licencje i klucz wirtualny, rozszerzenia, import XML, analiza, kompilacja. Wszystkie komendy i opcje. |
| **`dbmgr`** | [references/dbmgr-cli-menu.md](references/dbmgr-cli-menu.md) + [assets/dbmgr-menu.cs](assets/dbmgr-menu.cs) | zastosowanie | Wzorzec + gotowy szablon: owinięcie `dbmgr` w interaktywne menu CLI (Spectre.Console) na jednoplikowej aplikacji C# — wybór bazy z listy, tryby środowisk, gotowe akcje. |
| **`buscall`** | [references/buscall.md](references/buscall.md) | referencja | Zdalne wywoływanie metod aplikacji: tryby `call`/`callmcp`, składnia argumentów `klucz=wartość`, odkrywanie metod (`methods.list`), katalog metod Bundle (nawigacja, formularze, gridy, zrzuty), kody wyjścia, pułapki trybu `mcp` (otwarty STDIN, współbieżność, cache listy metod). |
| **`SonetaFrame`** | [references/sonetaframe.md](references/sonetaframe.md) | referencja | Aplikacja ramki (`SonetaFrameNew`) hostująca webową wersję programu: uruchamianie (`dotnet SonetaFrameNew.dll`), parametry `-s`/`--start`, `-c`/`--connection`, plik konfiguracyjny (`Settings_<Product>.json`) i pełna składnia **źródeł baz danych** (`demo:`, `http(s):`, `docker:`, `process:`, `orchestrator:` + modyfikatory), logi ramki i `buscall` (`Soneta.Frame/Logs/`). |

> **Uwaga:** wizualną **weryfikację kodu na żywej aplikacji** przez `buscall` (baza startująca z Twojego kodu,
> przeładowanie DLL, pułapki osieroconych procesów/portów, zrzuty ekranu do oceny wyglądu formularzy) opisuje
> dokument [buscall-live-testing.md](../programming/references/buscall-live-testing.md).

> **Żywa aplikacja z WŁASNYM dodatkiem** — kompletny przepis w trzech krokach: (1) per-bazowy
> `serversettings.json` (`Ext` + `Server.DbRegister`) i utworzenie bazy z tabelami dodatku —
> [references/dbmgr.md](references/dbmgr.md), sekcja „Baza z własnym dodatkiem"; (2) wpięcie tej samej
> konfiguracji w `Sources` ramki przez `config-file=` — [references/sonetaframe.md](references/sonetaframe.md);
> (3) sterowanie i zrzuty ekranu — [references/buscall.md](references/buscall.md), „Typowy przepływ".

## Wspólny kontekst

- **Dostęp do narzędzi:** polecenia uruchamiaj przez terminal dostępny w danym środowisku
  agenta. MCP jest opcjonalnym sposobem połączenia, a `callmcp` trybem narzędzia `buscall`;
  dostępne nazwy i parametry sprawdzaj w kontrakcie połączenia lub przez `methods.list`.
  Gdy brakuje terminala, binariów lub połączenia z aplikacją, przygotuj polecenia i wskaż,
  które kroki pozostają niewykonane.
- **Binaria** znajdują się w katalogu wyjściowym buildu (`bin/Debug`) odpowiedniego projektu.
  Dokładna ścieżka zależy od Twojego układu repozytoriów — w przykładach piszemy krótko
  `dbmgr` / `buscall`, zakładając alias, wpis w `PATH` albo uruchamianie z katalogu buildu:
  - `dbmgr` → `dbmgr.exe` (Windows) lub `dotnet dbmgr.dll` (cross-platform),
  - `buscall` → `buscall` lub `dotnet BusCall.dll` (katalog buildu projektu BusCall),
  - `SonetaFrame` → `dotnet SonetaFrameNew.dll` (aplikacja ramki).
- **Nazwa bazy** w obu narzędziach (`--db <Baza>` w `buscall`, `<database_name>` w `dbmgr`)
  odwołuje się do **wpisu w konfiguracji** (połączenie zdefiniowane w `SonetaFrame` /
  plik `Settings_<Product>.json`), a nie bezpośrednio do fizycznej nazwy bazy SQL.
  Szczegóły w referencjach.
- **Platforma .NET 10**, narzędzia działają cross-platform (macOS/Windows/Linux).

## Jak wybrać

- Operujesz **na strukturze/danych bazy** (utwórz, skonwertuj, backup, licencja, extension)
  → **`dbmgr`** ([references/dbmgr.md](references/dbmgr.md)).
- **Sterujesz uruchomioną aplikacją** (nawigacja, formularze, zrzuty ekranu)
  → **`buscall`** ([references/buscall.md](references/buscall.md)).
- **Uruchamiasz samą aplikację**, konfigurujesz połączenia/źródła baz danych albo pytasz
  o plik ustawień ramki → **`SonetaFrame`** ([references/sonetaframe.md](references/sonetaframe.md)).

## Powiązane skille

- **[programming](../programming/SKILL.md)** — warstwa ORM i kod biznesowy; `buscall` służy do weryfikacji
  napisanego tam kodu na żywej aplikacji.
- **[containers](../containers/SKILL.md)** — uruchamianie i wdrażanie produktu w kontenerach (docker compose,
  Apple container / Container Desktop, Helm). Tu wołasz `dbmgr` **w kontenerze** (obraz
  `server.standard` ma `dbmgr.dll`) — składnię komend bierzesz z tego skilla, orkiestrację
  z [containers](../containers/SKILL.md).
- **[erp](../erp/SKILL.md)** — meta-skill z mapą wszystkich skilli platformy Soneta.
