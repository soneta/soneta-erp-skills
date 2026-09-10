# Helm / Kubernetes — zwięźle (chart w fazie BETA)

Wdrożenie Soneta (enova365 / Triva) na klaster Kubernetes chartem `soneta/soneta`.
Chart jest **w fazie beta** — może się zmieniać. Pełne parametry: README charta w repo.
Gotowy przykład: [../assets/values.example.yaml](../assets/values.example.yaml).

## Instalacja

```bash
helm repo add soneta https://soneta.github.io/helm-charts
helm repo update
helm search repo soneta -l --devel          # dostępne wersje charta
helm upgrade sample soneta/soneta -f values.yaml --install
```

## Kluczowe wartości `values.yaml`

| Klucz | Znaczenie |
|---|---|
| `image.product` | wariant produktu (np. `standard`) |
| `image.tag` | **wersja obrazów** (patrz [obrazy-wersje.md](obrazy-wersje.md)) |
| `image.serverTagPostfix` / `webTagPostfix` | sufiks tagu, np. `-alpine` |
| `image.repository` | źródło obrazów (puste = domyślne `soneta/*` z Docker Hub) |
| `image.webapi` / `webwcf` / `scheduler` | włącz dodatkowe komponenty |
| `dblist` | **XML `DatabaseCollection`** z bazami (zewnętrzny MSSQL) |
| `appsettings.orchestrator.kubernetes.composition` | tryb wielobazowy: `router`/`web`/`webapi`/`server`/`commhub` |
| `imagePullSecrets` | sekret dostępu do prywatnego registry (`registry.soneta.pl`) |
| `resources.<komponent>` | limity/requesty CPU i pamięci per komponent |
| `ingress` | `enabled`, `class`, `host`, `tlsSecretName` |
| `envs.all` | wspólne zmienne środowiskowe (np. `TZ`) |
| `adminMode` | tryb serwisowy do operacji na bazie (patrz niżej) |

SQL Server jest **zewnętrzny** (chart go nie stawia) — konfigurujesz go w `dblist`.

## Tryb prosty vs orkiestracja

- **Prosty** (domyślny): jedna baza na instancję.
- **Orkiestracja**: wiele baz, elastyczne komponenty — włącz sekcję
  `appsettings.orchestrator.kubernetes.composition`.

## Tworzenie / konwersja bazy (adminMode)

Chart nie tworzy bazy automatycznie. Zrób to raz w trybie admin, potem wyłącz. Opcja
`--dbconfig` sprawia, że `dbmgr` **wygeneruje wpis bazy do pliku XML**, który wklejasz do `dblist`:

```bash
# 1. wystartuj z pustym dblist w trybie admin:
helm upgrade sample soneta/soneta -f values.yaml --install --set adminMode=true
# 2. w podzie admin (ten sam obraz ma dbmgr.dll) — utwórz bazę i wygeneruj config:
dotnet dbmgr.dll create sample --dbconfig ~/temp.xml --mssql --sqlserver "<host>,1433" \
  --sqldb sample --sqluser sa --sqlpwd "<hasło>" --demo gold --active
# 3. skopiuj zawartość ~/temp.xml do `dblist` w values.yaml, a potem wyłącz tryb admin:
helm upgrade sample soneta/soneta -f values.yaml --set adminMode=false
```

Pełna składnia `dbmgr` (`--dbconfig`, convert/backup/licence/extensions) → [tools](../../tools/SKILL.md).

## Checklista

- [ ] `helm repo add` + `repo update`
- [ ] `image.tag` + `*TagPostfix` ustawione na wybraną wersję
- [ ] `dblist` wskazuje działający, zewnętrzny SQL
- [ ] baza utworzona w `adminMode=true`, potem `adminMode=false`
- [ ] `ingress.host` i TLS skonfigurowane dla produkcji

Powiązane: [docker-compose.md](docker-compose.md) · [obrazy-wersje.md](obrazy-wersje.md) ·
[tools](../../tools/SKILL.md) (dbmgr) · [config](../../config/SKILL.md) (appsettings, import XML).
