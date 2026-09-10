# Konfiguracja `appsettings.json` — orchestrator, serwer, web, webapi, webwcf

Przewodnik po ustawieniach uruchomieniowych platformy Soneta (enova365, Triva): porty
i adresy komponentów, kolejność nadpisywania konfiguracji oraz znaczenie poszczególnych
kluczy. Dotyczy instalacji, w których komponenty uruchamiane są jako procesy .NET
(orkiestrator + serwer + frontendy).

## Najważniejsze w skrócie

**Dwa pliki, pięć komponentów.** Konfiguracja nie jest osobna dla każdego komponentu —
jest skonsolidowana w dwóch plikach `appsettings.json`:

| Plik | Obsługuje komponenty | Przykładowe sekcje |
|---|---|---|
| **back-end** (część orkiestratora) | **orchestrator + server** (+ router, commhub, scheduler) | `Orchestrator`, `Server`, `Router`, `CommHub`, `Scheduler`, `Balancers`, `MemoCache`, `IdleMonitor` |
| **front-end** (część webowa) | **web + webapi + webwcf** | `Kestrel`, `FrontEnd`, `Web`, `WebApi`, `WebWcf` |

`webapi` i `webwcf` nie mają własnego pliku — współdzielą plik części webowej.

**Domyślne porty i pary adresów** (kanoniczne wartości bazowe):

| Komponent | Rola | Port domyślny | Łączy się z |
|---|---|---|---|
| orchestrator | nadzorca procesów | `6000` | steruje całością |
| server | silnik logiki biznesowej | `22000` | — |
| router | proxy / balancer obciążenia | `7000` | → dynamiczne instancje serwera |
| web | frontend przeglądarkowy | `5000` | → router **lub** serwer bezpośrednio |
| webapi | frontend REST/JSON | `9010` | jak web |
| webwcf | frontend SOAP/WCF | `9020` | jak web |
| scheduler | zadania w tle | `8000` | — |
| commhub | szyna komunikatów (tcp) | `4000` | wszyscy → tu |

**Trzy pary, które MUSZĄ się zgadzać** (zmieniasz jedną stronę → zmień drugą):

- `Server:Urls` ↔ `FrontEnd:ServerEndpoint`
- `Router:Urls` ↔ `Orchestrator:Process:Composition:FrontEnd:RouterEndpoint`
- `CommHub:Urls` ↔ `CommHubClient:EndPoints` (oraz `FrontEnd:CommHubClient:EndPoints`)

Zgadza się **port i schemat** (`http`/`https`) — **nie** forma hosta: `…:Urls` używa `+`
(nasłuch na wszystkich adresach po stronie usługi), a `…Endpoint`/`EndPoints` konkretnego
adresu klienta (`localhost`, nazwa hosta, IP).

**Zanim zmienisz cokolwiek — poznaj kolejność warstw** (patrz niżej): `appsettings.json`
w instalacji to tylko *baza*, którą nadpisują warstwy wyższe. Najczęstszy błąd: „zmieniłem
`appsettings.json`, a nic się nie zmieniło" — bo nadpisuje profil systemu, nakładka `-c`,
zmienna `SONETA_…` albo argument wiersza poleceń.

## Kolejność warstw konfiguracji (co wygrywa)

Wartości scalane są warstwami — od najsłabszej do najsilniejszej. Późniejsza warstwa
nadpisuje wcześniejszą klucz po kluczu:

1. **`appsettings.json` przy pliku wykonywalnym** komponentu — baza.
2. **Profil systemu** — `…/Soneta/config/appsettings.json` w danych aplikacji
   (kolejno: dane wspólne → dane użytkownika roaming → dane użytkownika lokalne). Tu robi się
   nadpisania wdrożeniowe bez ruszania plików w instalacji.
3. **Nakładka `-c` / `--config-file`** — dodatkowy plik JSON (zestaw gotowych scenariuszy,
   patrz niżej).
4. **Zmienne środowiskowe z prefiksem `SONETA_`** (separator `__`), np.
   `SONETA_WEB__SERVERENDPOINT=…`, `SONETA_DBNAME=…`. Wygodne w kontenerach i CI.
5. **Argumenty wiersza poleceń** — `--urls`, `-s`/`--server-endpoint`, `-d`/`--database`,
   `--standard`/`--premium`, `--ext`/`--extpath`. Najsilniejsze, per uruchomienie.

> **Zasada zmiany ustawienia lokalnego/tymczasowego:** nie edytuj bazowego `appsettings.json`
> — użyj warstwy wyższej (nakładka `-c`, zmienna `SONETA_…` albo argument `--urls`). Bazowy
> plik zostaje wtedy czysty i przenośny między środowiskami.

## Model konfiguracji

### Grupa `FrontEnd` vs sekcje `Web` / `WebApi` / `WebWcf`

Każdy komponent czyta klucz wg kolejności: **sekcja komponentu → sekcja grupy → korzeń**.

- Grupa **`FrontEnd`** = `web` + `webapi` + `webwcf`. Klucze pod `FrontEnd:` (np.
  `FrontEnd:ServerEndpoint`, `FrontEnd:ServerGateway`, `FrontEnd:CommHubClient`,
  `FrontEnd:Hsts`) są **wspólne** dla wszystkich trzech frontendów.
- Klucze pod `Web:` / `WebApi:` / `WebWcf:` są **specyficzne** dla danego frontendu
  i nadpisują to, co ustawiono w `FrontEnd:` lub w korzeniu.
- Po stronie back-endu analogicznie: `Server:`, `Router:` to sekcje komponentów, a ustawienia
  wspólne (np. `Logging`, `Telemetry`, `TokenOptions`) leżą w korzeniu.

### Dwa tryby połączenia frontend → back-end

O trybie decyduje obecność klucza `RouterEndpoint`:

| Tryb | Warunek | Jak frontend łączy się z serwerem |
|---|---|---|
| **bezpośredni** | brak `RouterEndpoint` | frontend → wprost `FrontEnd:ServerEndpoint` (domyślnie serwer `:22000`) |
| **przez router** | `RouterEndpoint` ustawiony | frontend → router (`:7000`) → router kieruje ruch do dynamicznych instancji serwera (porty przydziela orkiestrator) |

Samodzielna część webowa działa w trybie bezpośrednim. Uruchomienie złożone (orkiestrator
zestawia całość) ustawia `RouterEndpoint` i włącza tryb przez router.

## Gotowe scenariusze — nakładki `-c`

Zestaw plików nakładkowych do włączania przez `-c`. Zmieniają spójnie kilka kluczy naraz:

| Nakładka | Efekt |
|---|---|
| `use-ssl` | Pary portów `HTTPS;HTTP` dla wszystkich komponentów + przełączenie adresów na `https` |
| `no-router` | Wyłącza router i czyści `RouterEndpoint` → tryb bezpośredni |
| `use-webapi` | Włącza `webapi` i `webwcf` w kompozycji orkiestratora (domyślnie wyłączone) |
| `use-legacy_controllers` | Włącza starsze kontrolery API (`MethodInvoker`, `Token`) |
| `use-balancer-logins` | Router balansuje wg liczby logowań + progi wydajności (CPU/RAM/uptime) |
| `use-static-routing` | Statyczne przypięcie wskazanej bazy do konkretnej instancji serwera |
| `with-commhubauth` | Uwierzytelnienie szyny CommHub kluczem API |

## Słownik parametrów — część back-end

### `Orchestrator:Process` — kompozycja i uruchamianie komponentów
| Klucz | Znaczenie |
|---|---|
| `Urls` (`http://127.0.0.1:6000`) | Adres nasłuchu orkiestratora. |
| `ForkDetached` (`false`) | Pozostawia procesy potomne po zamknięciu orkiestratora. |
| `Composition.Router.Enabled` (`true`) / `.InProcess` (`false`) | Czy router działa i czy w procesie orkiestratora. |
| `Composition.Server.Enabled` (`true`) / `.MaxDynamicRuns` (`100`) | Uruchamianie serwera; limit dynamicznych instancji (= zajmowanych portów). |
| `Composition.Web/WebApi/WebWcf/Scheduler.Enabled` | Które frontendy/scheduler startują. Domyślnie web = `true`, reszta = `false`. |
| `Composition.CommHub.InProcess` (`false`) | Szyna w procesie orkiestratora czy osobno. |
| `Composition.FrontEnd.RouterEndpoint` (`http://localhost:7000`) | **Przełącznik trybu**: ustawiony → frontendy przez router. Para z `Router:Urls`. |
| `Composition.Dashboard.Enabled` (`false`) | Panel telemetrii (Aspire Dashboard): `Overrides.Urls` (`:3000`), OTLP gRPC (`:4317`). |

### `Router`
| Klucz | Znaczenie |
|---|---|
| `Urls` (`http://+:7000`) | Adres nasłuchu routera. Para z `RouterEndpoint`. |
| `Balancer` (`Database`) | Strategia routingu: `Database` (wg przypisania bazy) lub `Logins` (pojemnościowy). |

### `Server`
| Klucz | Znaczenie |
|---|---|
| `Urls` (`http://+:22000`) | Adres nasłuchu serwera. Para z `FrontEnd:ServerEndpoint`. |
| `GrpcServiceOptions.MaxReceiveMessageSize` (`31457280` = 30 MB) | Limit rozmiaru komunikatu między frontendem a serwerem. |
| `ApplicationOptions.SessionLiveTimeMinutes` (`20`) | Czas życia sesji serwerowej. |
| `ApplicationOptions.SessionObjectsLimit` (`40`) | Limit obiektów utrzymywanych w sesji. |
| `ApplicationOptions.SecureUrlKey` | Klucz do podpisywania bezpiecznych adresów URL. |
| `ApplicationOptions.Eventlog` (`false`) | Logowanie do dziennika zdarzeń systemu Windows. |

### `CommHub` / `CommHubClient` — szyna komunikatów
| Klucz | Znaczenie |
|---|---|
| `CommHub.Urls` (`tcp://+:4000`) / `CommHub.Ssl` (`null` = auto) | Adres nasłuchu szyny i wymuszenie SSL. |
| `CommHubClient.EndPoints` (`["127.0.0.1:4000"]`) | Adres szyny po stronie klienta. Para z `CommHub:Urls`. |

### `Balancers` — parametry strategii routingu
| Klucz | Znaczenie |
|---|---|
| `Logins.Capacity` (`5`) | Pojemność pojedynczej instancji serwera. |
| `Logins.ServerLeaseDuration` (`30` s) | Czas dzierżawy serwera w oczekiwaniu na logowanie. |
| `Logins.Strategy` (`MostLogins`) | Wybór serwera: `LeastLogins` / `MostLogins`. |

### `IdleMonitor` — automatyczne zamykanie bezczynnych procesów
| Klucz | Znaczenie |
|---|---|
| `BusyTime` (`7:00-18:00`) | Okno godzin wykluczone z zamykania procesu. |
| `Due` (`600` s) / `Interval` (`300` s) / `Timeout` (`3600` s) | Start śledzenia / odstęp prób / bezczynność konieczna do zamknięcia. |

### `MemoCache` — pamięć podręczna pól „memo"
Pola „memo" to długie pola tekstowe / notatki rekordów; pamięć podręczna ogranicza ich odczyty
z bazy.
| Klucz | Znaczenie |
|---|---|
| `Type` (`PersistentMemory`) | `None` (bez cache — **całkowite wyłączenie**), `PersistentMemory` (w pamięci + zapis do pliku), `FileSystem` (pliki bez pamięci procesu). |
| `PersistentMemory.MaxSizeMB` (`64`) / `.CleanupThresholdPercent` (`80`) | Rozmiar maksymalny / ile procent zostaje po czyszczeniu. |
| `PersistentMemory.LifetimeDays` (`31`) / `.AutoSaveIntervalMinutes` (`0`) | Czas życia wpisu / cykliczny zapis (`0` = wyłączony). |

## Słownik parametrów — część front-end

### `Kestrel` / `Form` — limity żądań
| Klucz | Znaczenie |
|---|---|
| `Kestrel.Limits.MaxRequestBodySize` (`31457280` = 30 MB) | Limit rozmiaru żądania HTTP. |
| `Form.MultipartBodyLengthLimit` / `Form.ValueCountLimit` (`10`) | Limity formularzy multipart. |

### `FrontEnd:` — wspólne dla web + webapi + webwcf
| Klucz | Znaczenie |
|---|---|
| `ServerEndpoint` (`http://localhost:22000`) | Adres serwera w trybie bezpośrednim. Para z `Server:Urls`. |
| `CommHubClient.EndPoints` (`["127.0.0.1:4000"]`) | Adres szyny CommHub dla frontendów. |
| `ServerGateway.ServerRequestTimeout` (`60000` ms) | Limit oczekiwania na serwer. |
| `ServerGateway.RetryPolicy.MaxRetryAttempts` (`3`) / `.BackoffType` (`Constant`) | Ponawianie żądań do serwera (algorytm: `Constant`/`Linear`/`Exponential`). |
| `ServerGateway.Timeout` (`null`) | Górny limit czasu odpowiedzi (null = wartość domyślna). |
| `Hsts.MaxAge` (`365`) / `IncludeSubDomains` / `Preload` | Nagłówek HSTS. |
| `ForwardedHeaders.*` | Praca za reverse-proxy: `X-Forwarded-*`, `ForwardLimit`, `KnownProxies`. |

### `Web:` — specyficzne dla web
| Klucz | Znaczenie |
|---|---|
| `Urls` (`http://+:5000`) | Adres nasłuchu web. |
| `Authentication.Adfs` / `.Forms` / `.MobileBiometric` | Metody logowania: ADFS, formularz, biometria mobilna. |
| `Antiforgery.{CookieName,HeaderName,MetaName}` | Nazwy tokenów zabezpieczenia XSRF. |
| `Customization.Themes` (`Base`, `Dark`) / `CustomIncludes` / `LogoSettings` | Motywy, wstrzykiwanie własnego CSS/HTML, branding (logo). |
| `IpFilters.{Logins,Roles,UserTypes}` | Białe listy adresów IP per tożsamość (`*` = dowolny). |
| `Settings.ContentSecurityPolicy` / `.PermissionsPolicy` | Nagłówki bezpieczeństwa. |
| `SessionManagement.DatabaseWhiteList` / `.DatabaseBlackList` | Wyrażenia regularne baz dozwolonych/zabronionych do logowania (tryb SingleDB przez `"\\*"`). |

### `WebApi:` — specyficzne dla webapi
| Klucz | Znaczenie |
|---|---|
| `Urls` (`http://+:9010`) | Adres nasłuchu (dokumentacja pod `/api/docs`). |
| `LegacyControllers.{MethodInvoker,Token}` (`false`) | Starsze kontrolery (włącza nakładka `use-legacy_controllers`). |
| `Invokers.Invoker1.{InvokerName,DbName,UserName,Password}` | Predefiniowane konto serwisowe API. |
| `Cors.*` / `Settings.SendTimeout` (`60`) | Reguły CORS / limit czasu. |

### `WebWcf:` — specyficzne dla webwcf
| Klucz | Znaczenie |
|---|---|
| `Urls` (`http://+:9020`) | Adres nasłuchu (opis usługi pod `/?wsdl`). |
| `ServiceBinding.MethodInvokerService.MaxReceivedMessageSize` (`65536`) | Limit rozmiaru komunikatu WCF. |
| `ServiceBehavior.TokenService.IncludeExceptionDetailInFaults` (`false`) | Czy zwracać szczegóły wyjątków w komunikacie SOAP Fault. |

## Wspólne dla obu plików (korzeń)

| Sekcja | Znaczenie |
|---|---|
| `Logging` | Poziomy (`LogLevel`) + `Soneta` (`Enabled`, `LogFolder`, `FileName`, `RollingInterval`, `Sinks` = `Console`/`File`; `null`/`[]` wyłącza logowanie). |
| `Telemetry` | OpenTelemetry: `EnableOTLPExporter`, `Collect{Logs,Metrics,Traces}`, źródła metryk/śladów. Endpoint kolektora w `OTEL_EXPORTER_OTLP_ENDPOINT`. |
| `TokenOptions.ClockSkew` (`60` s) | Tolerancja rozjazdu zegara przy walidacji tokenów (back-end ma dodatkowo `Impersonate`/`Recovery`). |
| `Fido2` / `Totp` (back-end) | Uwierzytelnianie WebAuthn (FIDO2) i TOTP (2FA): domeny, źródła, tolerancje czasu. |

## Checklista — zmiana portu lub adresu

- [ ] Zidentyfikuj plik (back-end czy front-end) i sekcję komponentu.
- [ ] Zmień `…:Urls` **oraz** sprzężony `…Endpoint`/`EndPoints` (patrz trzy pary) — zgodny port i schemat.
- [ ] Jeśli używasz routera, sprawdź parę `Router:Urls` ↔ `RouterEndpoint`.
- [ ] Upewnij się, że nowy port jest wolny (żaden inny komponent go nie zajmuje).
- [ ] Sprawdź, czy zmiany nie nadpisuje warstwa wyższa (profil systemu, `-c`, `SONETA_…`, `--urls`).
- [ ] Dla zmiany tymczasowej użyj warstwy wyższej zamiast edytować bazowy `appsettings.json`.

## Powiązania

- [tools](../../tools/SKILL.md) — uruchamianie i zarządzanie bazami z CLI
  (`dbmgr`), ramka hostująca aplikację (`SonetaFrame`), test na żywej aplikacji (`buscall`).
- [import-export-xml](import-export-xml.md) — przenoszenie danych i ustawień biznesowych
  między bazami (komplementarne do konfiguracji uruchomieniowej opisanej tu).
- [containers](../../containers/SKILL.md) — uruchamianie komponentów w kontenerach
  (docker compose, Apple container, Helm); tam klucze z tego dokumentu ustawia się jako zmienne
  `SONETA_…` (np. `SONETA_Server__DbRegister__*`, `SONETA_ServerEndpoint`, `SONETA_URLS`).
- [erp](../../erp/SKILL.md) — mapa wyboru skilla.
