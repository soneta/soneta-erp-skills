# Docker Compose — pełny przewodnik (ścieżka główna)

Postawienie Soneta (enova365 / Triva) jako stack `dbinit → server → web` przez
`docker compose`. Gotowe pliki: [../assets/docker-compose.yaml](../assets/docker-compose.yaml)
(SQL zewnętrzny) i [../assets/docker-compose.mssql.yaml](../assets/docker-compose.mssql.yaml)
(SQL w kontenerze). Składnię komend `dbmgr` → [tools](../../tools/SKILL.md).

## Minimalny cykl życia

```bash
docker compose up -d           # start; dbinit założy bazę, potem server, potem web
docker compose ps              # status usług
docker compose logs -f web     # logi (server / dbinit / web)
docker compose down            # zatrzymanie (dodaj -v aby usunąć wolumeny, np. mssql-data)
```

Web dostępny na `http://localhost:60000` (mapowanie `60000:8080`).

## Anatomia pliku

Trzy usługi na jednym obrazie serwera + obraz web:

- **`dbinit`** — jednorazowa inicjalizacja bazy. Reużywa `soneta/server.standard`
  (w obrazie jest `dbmgr.dll`), nadpisuje `entrypoint: ["dotnet","dbmgr.dll"]`, a w
  `command` woła `create …`. Oznaczona `x-init: true` (dla Container Desktop) — w Dockerze
  to pole jest ignorowane, więc kolejność wymusza `depends_on` (niżej).
- **`server`** — `entrypoint: ["dotnet","/app/server.dll"]`; połączenie do bazy przez
  zmienne `SONETA_Server__DbRegister__*`. Nasłuchuje w sieci projektu (domyślnie `:22000`).
- **`web`** — `entrypoint: ["dotnet","/app/web.dll"]`; `SONETA_ServerEndpoint` wskazuje
  serwer, `SONETA_URLS` ustawia nasłuch, `ports` publikuje na hoście.

### Zmienne środowiskowe

| Zmienna | Usługa | Znaczenie |
|---|---|---|
| `SONETA_Server__DbRegister__Name` | server | nazwa wpisu bazy w konfiguracji |
| `SONETA_Server__DbRegister__Server` | server | adres SQL, np. `host.docker.internal,1433` |
| `SONETA_Server__DbRegister__DatabaseName` | server | fizyczna nazwa bazy SQL |
| `SONETA_Server__DbRegister__User` / `__Password` | server | logowanie SQL |
| `SONETA_ServerEndpoint` | web | URL serwera, np. `http://soneta-server:22000` |
| `SONETA_URLS` | web | adres nasłuchu web, np. `http://*:8080` |

Podwójne podkreślenie `__` odwzorowuje zagnieżdżenie sekcji `appsettings.json`, a prefiks
`SONETA_` to **warstwa nadpisań** (silniejsza niż plik bazowy — wygodna w kontenerach).
Znaczenie kluczy, domyślne porty i warstwy nadpisań →
[config → appsettings.md](../../config/references/appsettings.md).

Ten stack działa w **trybie bezpośrednim** (web → serwer, bez routera/orkiestratora).
Serwer nasłuchuje domyślnie na `:22000` (`Server:Urls`), a `SONETA_ServerEndpoint` musi
wskazywać ten sam port — to jedna z „par, które muszą się zgadzać" (`Server:Urls` ↔
`ServerEndpoint`, patrz appsettings.md). Web publikujemy na `8080` w kontenerze (`SONETA_URLS`)
→ `60000` na hoście.

## Nazwy usług między kontenerami (ważne)

- **Docker** rozwiązuje w sieci projektu **nazwę usługi** oraz **`container_name`**.
- **Container Desktop** używa `container_name` (lub `<projekt>-<usługa>`) i wpisuje ją do
  `/etc/hosts`.

Dlatego w assetach ustawiamy jawne `container_name` (`soneta-server`, `soneta-web`, …) i
`web` odwołuje się do `http://soneta-server:22000` — jeden plik działa w obu środowiskach.

## Kolejność i gotowość

- `server` czeka na sukces inicjalizacji: `depends_on: { dbinit: { condition:
  service_completed_successfully } }`.
- `web` startuje po `server` (`depends_on: [server]`). `depends_on` pilnuje **startu**,
  nie „gotowości" — web ponawia łączenie do serwera, więc krótkie wyprzedzenie jest OK.
- Dla kontenera SQL używaj `healthcheck` + `condition: service_healthy` (patrz plik `.mssql`).

## SQL: zewnętrzny vs kontener

- **Zewnętrzny (host):** `--sqlserver "host.docker.internal,1433"` i ten sam adres w
  `SONETA_Server__DbRegister__Server`. Na Linuksie dodaj usłudze
  `extra_hosts: ["host.docker.internal:host-gateway"]`.
- **Kontener `mssql`:** plik `docker-compose.mssql.yaml` — łączysz się po `container_name`
  serwera SQL (`soneta-mssql,1433`); `dbinit` czeka na `healthcheck`. Wariant dockerowy
  (w Container Desktop `x-init` nie może zależeć od zwykłej usługi — patrz `apple-container.md`).

## Zarządzanie istniejącą bazą (dbmgr w kontenerze)

`run --rm dbinit <komenda>` uruchamia obraz `server.standard` z `entrypoint dbmgr.dll`
i przekazuje `<komenda>` jako argumenty. **Warunek:** usługa `dbinit` w assetach ma blok
`SONETA_Server__DbRegister__*` — dzięki temu `dbmgr` w świeżym kontenerze zna wpis bazy
`sttest` (bez tego `convert`/`backup` nie znajdą połączenia).

```bash
docker compose run --rm dbinit list             # szybka diagnoza: czy baza jest widoczna
docker compose run --rm dbinit convert sttest   # konwersja po zmianie wersji obrazu
```

**Pliki wejściowe/wyjściowe montuj wolumenem** — kontener `--rm` nie zachowuje danych:

```bash
# tekstowy backup po stronie klienta (.zip — przenośny):
docker compose run --rm -v "$(pwd)":/work dbinit backuptxt sttest /work/sttest.zip
# licencja z pliku na hoście:
docker compose run --rm -v "$(pwd)":/work dbinit licence sttest /work/licence.xml
```

> Binarny `backup` (`.bac`) zapisuje **serwer SQL** po swojej stronie, nie klient — do
> przenośnych kopii używaj `backuptxt` (`.zip`). Pełna składnia komend `dbmgr` (convert,
> backup/restore, licence, extensions) → [tools](../../tools/SKILL.md). Import XML (role, ustawienia) → [config](../../config/SKILL.md).

## Opcjonalnie: parametryzacja przez `.env` (tylko Docker)

Docker interpoluje `${...}`; Container Desktop **nie**. Jeśli zostajesz przy Dockerze,
możesz sparametryzować plik:

```yaml
image: soneta/server.standard:${SONETA_VERSION:-2606.0.1-alpine}
command: create ${DB_NAME:-sttest} --mssql --sqlserver "${SQL_HOST:-host.docker.internal},1433" ...
```

```dotenv
# .env
SONETA_VERSION=2606.0.1-alpine
DB_NAME=sttest
SQL_HOST=host.docker.internal
SA_PASSWORD=enova.123456
```

Do wklejenia w Container Desktop użyj wersji z wartościami wprost.

## Checklista

- [ ] tag wersji podmieniony w `dbinit`, `server`, `web` (ten sam)
- [ ] hasło SA zmienione poza środowiskiem dev
- [ ] host SQL zgodny ze środowiskiem (`host.docker.internal` vs kontener)
- [ ] port web wolny (domyślnie 60000)
- [ ] `dbinit` zakończył się sukcesem (`docker compose logs dbinit`)
- [ ] po pierwszym udanym starcie **usuń `--recreate`** z `dbinit` (inaczej każdy re-`up`
      zdropuje bazę) albo startuj tylko `docker compose up -d server web`

Powiązane: [apple-container.md](apple-container.md) · [helm-k8s.md](helm-k8s.md) ·
[obrazy-wersje.md](obrazy-wersje.md) · [tools](../../tools/SKILL.md) (dbmgr) · [config](../../config/SKILL.md) (XML, appsettings).
