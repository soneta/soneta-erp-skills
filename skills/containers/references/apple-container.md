# Apple `container` / Container Desktop (macOS) — zwięźle

Na macOS (Apple Silicon) zamiast Dockera używa się Apple `container`. Najwygodniej przez
**Container Desktop** — natywną aplikację, do której **wklejasz ten sam `docker-compose.yaml`**.
Ścieżka główna (Docker) i pliki: [docker-compose.md](docker-compose.md).

## Różnice wobec Dockera (istotne)

- **Brak natywnego `compose`** (`container` nie ma `docker compose up` ani `container compose`).
  Compose realizuje aplikacja Container Desktop, tłumacząc YAML na pojedyncze `container run`.
- **Host SQL:** `host.containers.internal` (nie `host.docker.internal`). Alias na gateway sieci
  podmienia **tylko Container Desktop** — przy ręcznym `container run` (Droga B) nie zadziała.
- **Nazwy usług między kontenerami:** Apple `container` **nie ma DNS nazw** kontenerów.
  Container Desktop obchodzi to, wpisując je do `/etc/hosts` (jako `container_name` lub
  `<projekt>-<usługa>`). Przy ręcznym `container run` **nazwy się nie rozwiążą** — trzeba użyć
  adresów IP lub własnych wpisów `/etc/hosts`.
- **Brak `build:`** — wymagany gotowy `image:` (obrazy z Docker Hub / registry.soneta.pl).
- **`x-init: true`** odpala się **pierwszy, sekwencyjnie, blokująco** (musi zwrócić 0) i
  **nie może zależeć** od zwykłej usługi. Stąd SQL w tym samym compose nie zadziała tu z
  `x-init` — SQL uruchom osobno (niżej), a bazę twórz na SQL zewnętrznym.
- Limity: `mem_limit` / `cpus` (`deploy.resources.limits` też jest respektowane). Ustaw
  `mem_limit` także serwerowi — domyślny przydział pamięci bywa za mały.
- Wymagania: macOS 26 (Tahoe), Apple Silicon, binarka `container` (`/usr/local/bin/container`
  lub `/opt/homebrew/bin/container`). Sama binarka działa też na macOS 15, ale **sieć
  kontener‑kontener** wymagana przez ten stack jest dopiero od macOS 26. Projekt CLI:
  `github.com/apple/container`.

## Droga A — Container Desktop (zalecana)

1. Otwórz okno **„Uruchom Docker Compose"**.
2. Wklej YAML z [../assets/docker-compose.yaml](../assets/docker-compose.yaml) (wariant z SQL
   zewnętrznym — **nie** `docker-compose.mssql.yaml`), zmieniając `host.docker.internal` →
   `host.containers.internal` (w `dbinit.command`, `dbinit.environment` i `server.environment`).
   **Zmiennych `${...}` NIE wpisuj** — Container Desktop ich nie interpoluje; podaj wartości wprost.
3. Uruchom. Kontenery pojawią się jako **rozwijana grupa** (dzięki etykietom
   `compose.project` / `compose.service`), z akcjami Uruchom/Zatrzymaj/Usuń całą grupę,
   podglądem logów, statystyk i wbudowanym terminalem (`container exec`).

`x-init: true` na `dbinit` sprawia, że baza tworzy się przed startem `server`/`web`. Wpisy
`/etc/hosts` (rozwiązywanie `soneta-server`) dokłada aplikacja automatycznie.

## Droga B — ręcznie `container run` (bez GUI)

Uwaga: tu **nie działają** aliasy hosta ani nazwy usług (patrz „Różnice"). Używaj IP.

```bash
container network create soneta-test
# SQL jako osobny kontener (obraz mssql jest amd64-only → --arch amd64 = emulacja Rosetta):
container run --detach --name soneta-mssql --network soneta-test --arch amd64 --publish 1433:1433 \
  --env ACCEPT_EULA=Y --env MSSQL_SA_PASSWORD="enova.123456" \
  mcr.microsoft.com/mssql/server:2022-latest
# ustal IP kontenera SQL (nazwa się nie rozwiąże):
SQL_IP=$(container inspect soneta-mssql --format '{{ .NetworkSettings.IPAddress }}')
# utworzenie bazy (dbmgr z obrazu serwera), po IP:
container run --name soneta-dbinit --network soneta-test \
  --entrypoint dotnet soneta/server.standard:2606.0.1-alpine \
  dbmgr.dll create sttest --mssql --sqlserver "$SQL_IP,1433" --sqldb sttest \
  --sqluser sa --sqlpwd "enova.123456" --demo gold --active --recreate
# server i web: po IP serwera (ustal jak wyżej) w SONETA_ServerEndpoint;
# dodaj --label compose.project=soneta-test dla grupowania w GUI.
```

Dla wygody rozważ Drogę A — Container Desktop sam wiąże nazwy przez `/etc/hosts`.

## Checklista

- [ ] binarka `container` zainstalowana i działa (`container --version`)
- [ ] host SQL = `host.containers.internal` (Droga A) lub IP (Droga B) — nie `host.docker.internal`
- [ ] wartości wprost (bez `${...}`)
- [ ] jawne `container_name` (rozwiązywanie nazw przez `/etc/hosts` — tylko w Container Desktop)
- [ ] jeśli SQL w kontenerze — uruchom go osobno przed `dbinit` (init nie zależy od usług);
      obraz mssql z `--arch amd64`
- [ ] `mem_limit` ustawiony także dla `server`

Powiązane: [docker-compose.md](docker-compose.md) · [obrazy-wersje.md](obrazy-wersje.md) ·
[tools](../../tools/SKILL.md) (dbmgr).
