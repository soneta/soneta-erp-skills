---
name: containers
description: >
  Uruchamianie i wdrażanie platformy Soneta (enova365, Triva) w kontenerach. Używaj gdy
  użytkownik: (1) stawia środowisko (server + web) na obrazach Soneta przez `docker compose`
  albo na Apple `container` / Container Desktop; (2) tworzy bazę danych w kontenerze (usługa
  init z `dbmgr create`, `--demo`, `--recreate`, licencja, konwersja); (3) wdraża na
  Kubernetes przez Helm (`helm repo add soneta`, `values.yaml`, `dblist`, `adminMode`); (4)
  wybiera wersję obrazów (tagi z Docker Hub `soneta/*` lub `registry.soneta.pl`), architekturę
  (alpine/arm64/amd64), logowanie do registry; (5) potrzebuje SQL Servera — zewnętrznego
  (`host.docker.internal` / `host.containers.internal`) albo jako kontener `mssql`; (6)
  rozwiązuje problemy startu stacku (kolejność, port zajęty, brak DNS między usługami w Apple
  container, zły host-alias). Słowa kluczowe: docker compose, docker-compose.yaml, apple
  container, Container Desktop, helm, kubernetes, obraz, tag, wersja, mssql, dbmgr, x-init,
  server.standard, web.standard.
---

# Soneta w kontenerach — uruchamianie i wdrażanie (enova365, Triva)

Skill dla **partnera lub zespołu Soneta**, który ma postawić i utrzymać środowisko Soneta w kontenerach
oraz założyć i zarządzać bazą — **bez dostępu do kodu programu**. Trzy ścieżki, wspólne
pojęcia. Składnię komend `dbmgr` opisuje [tools](../tools/SKILL.md) (nie duplikujemy jej tutaj).

## Którą ścieżką

| Ścieżka | Kiedy | Reference |
|---|---|---|
| **Docker Compose** | Domyślnie: lokalne środowisko test/demo, CI, jeden host. | [references/docker-compose.md](references/docker-compose.md) |
| **Apple `container` / Container Desktop** | macOS na Apple Silicon; wklejasz ten sam YAML do GUI. | [references/apple-container.md](references/apple-container.md) |
| **Helm / Kubernetes** | Wdrożenie na klaster (beta). | [references/helm-k8s.md](references/helm-k8s.md) |
| **Wybór wersji obrazów** | Zawsze — wspólne dla wszystkich ścieżek. | [references/obrazy-wersje.md](references/obrazy-wersje.md) |

## Wspólne pojęcia (dotyczą każdej ścieżki)

- **Obrazy:** `soneta/server.standard` (logika + serwer, ma w środku `dbmgr.dll`) oraz
  `soneta/web.standard` (aplikacja webowa). Rzadziej `web.api`, `web.wcf`.
- **Wersja = tag**, zawsze podawany jawnie (brak `latest`/`stable`). Docker Hub:
  `XXXX.X.X-alpine`; registry.soneta.pl (login): buildy alfa. Patrz `obrazy-wersje.md`.
- **SQL Server:** zewnętrzny (host lub osobny kontener) albo `mssql` w tym samym compose.
  Adres hosta: Docker → `host.docker.internal`, Apple container → `host.containers.internal`.
- **Baza powstaje raz** przez `dbmgr create <db> --mssql --sqlserver <host,port> --sqldb
  <db> --sqluser sa --sqlpwd <hasło> --demo gold --active --recreate` — jako usługa init
  reużywająca obrazu `server.standard`.
- **Licencja / dane:** `--demo silver|gold|platinum` (dane demo + licencja) lub `--licence`.

## Szybki start (Docker)

```bash
# 1. Skopiuj gotowy plik i podmień: tag wersji, hasło SA, host SQL.
cp assets/docker-compose.yaml ./docker-compose.yaml
# 2. Wystartuj (dbinit założy bazę, potem server, potem web):
docker compose up -d
# 3. Web: http://localhost:60000    Logi: docker compose logs -f
```

**Logowanie do web:** operatorem bazy demo (domyślny administrator). Hasło administratora
ustawisz przy tworzeniu (`dbmgr create --adminpwd`) albo zresetujesz później
(`dbmgr resetadminpwd`) — składnia w [tools](../tools/SKILL.md).

**macOS / Container Desktop:** wklej **plik z kroku 1** (SQL zewnętrzny) do okna „Uruchom
Docker Compose", zmieniając host-alias (`host.docker.internal` → `host.containers.internal`) —
patrz [references/apple-container.md](references/apple-container.md). Wariantu z kontenerem
SQL (`docker-compose.mssql.yaml`) tam **nie używaj** (`x-init` nie doczeka się SQL).

Samowystarczalny wariant z kontenerem SQL (tylko Docker): `assets/docker-compose.mssql.yaml`.

## Checklisty

**Przed startem:** silnik działa (`docker`/`container`) · wybrany tag wersji · dostępny
SQL + znane hasło SA · wolne porty (np. 60000, 1433).

**Po starcie:** `dbinit` zakończony sukcesem (baza założona) · `server` wstał · `web`
odpowiada na porcie · nałożona licencja / dane `--demo`.

**Gdy nie działa:** port zajęty (zmień mapowanie) · zły host-alias (Docker vs Apple) ·
w Apple container brak DNS między usługami → wpisy `/etc/hosts` (robi to Container
Desktop) · `x-init` nie może zależeć od zwykłej usługi · brak `build:` w Apple container.

## Powiązane skille

- **[tools](../tools/SKILL.md)** — pełna składnia `dbmgr` (create/convert/backup/restore/licence/
  extensions) oraz źródło baz `docker:` w `SonetaFrame`. Tu tylko wołamy `dbmgr` w kontenerze.
- **[config](../config/SKILL.md)** — znaczenie kluczy `appsettings.json`, domyślne porty i warstwy
  nadpisań przez zmienne `SONETA_...`
  ([appsettings.md](../config/references/appsettings.md)); import/eksport XML (role,
  uprawnienia, ustawienia) do bazy założonej w kontenerze.
- **[programming](../programming/SKILL.md)**, **[addon-planning](../addon-planning/SKILL.md)** — kod i planowanie dodatku, który
  potem uruchomisz w tym środowisku.
- **[erp](../erp/SKILL.md)** — mapa wszystkich skilli platformy.
