# HANDEL13 — Tematy specjalistyczne (KSeF, fiskalizacja, kompletacja, Intrastat)

> Wspólne fakty o typie, podstawowe typy i szablon wzorca: [../handel.md](../handel.md).

> Rozdział obejmuje obszary, które łączą dokument handlowy z systemami zewnętrznymi (KSeF), urządzeniami
> (drukarka fiskalna) oraz specjalistyczną logiką magazynową (kompletacja) i sprawozdawczą (Intrastat).
>
> **Ważne — co jest, a co nie jest testowalne jednostkowo.** Część operacji wymaga **sieci** (komunikacja
> z bramką KSeF, wysyłka e-mail e-paragonu) albo **sprzętu** (drukarka fiskalna). Tych fragmentów **nie**
> da się odtworzyć w teście jednostkowym — testuj wyłącznie **ustawienie pól i parametrów** oraz **strukturę**
> (np. `XmlValidated`, parametry workera, pola `KSeFKomunikat`). Każdy wzorzec poniżej oznacza, która część
> jest „offline" (testowalna), a która „online/sprzętowa" (NIE testuj — patrz `dh-facts.md`, „Reguły testów").
>
> Wszystkie workery wymienione w tym rozdziale są **publiczne** i mogą być wywołane z dodatku zewnętrznego.
> Operacje modyfikujące dokument wykonuj w transakcji (`session.Logout(true)` + `Commit`/`CommitUI`), potem
> `session.Save()`. Kod zgodny z C# 10.

---

### HANDEL-W67 — Wysłanie faktury do KSeF (pojedynczo i zbiorczo)

**Cel:** wysłać zatwierdzony dokument sprzedaży do Krajowego Systemu e-Faktur — pojedynczo
(`KSeFWyslijWorker`) albo wsadowo dla wielu dokumentów naraz (`KSeFWysylkaWsadowaWorker`). Sama wysyłka
to operacja **online** (NIE testuj); offline (testowalne) jest przygotowanie dokumentu: wygenerowanie XML,
walidacja struktury i ustawienie parametrów autoryzacji.

**Warianty:**

| Wariant | Worker / akcja | Uwaga |
|---|---|---|
| Wysyłka pojedyncza | `KSeFWyslijWorker.Wyslij(dok)` (akcja „KSeF/Wyślij") | dla jednego dokumentu |
| Wysyłka zbiorcza | `KSeFWysylkaWsadowaWorker.WyslijZbiorczo()` (akcja „KSeF/Wyślij zbiorczo") | `Dokumenty[]`, generuje XML brakującym, pomija zaimportowane/odrzucone |
| Faktura offline (awaria/tryb 24h) | wysyłka z `KSeFKomunikat.Offline == true` | używa tokenu i kontekstu zapisanych na komunikacie |
| Data wystawienia ≠ dziś | weryfikator `KSeFWyslijWorker.Weryfikator(dok)` | rzuca wyjątek wg konfiguracji i uprawnień (data przyszła/przeszła) |

**Pola i typy:**
- Parametry: `KSeFWyslijParams : ContextBase` — `SystemZewn: SystemZewnPlatformaEDI` (`[Required]`),
  `Token: SysZewToken` (`[Required]`, „Sposób autoryzacji"), `KontekstAutentykacjiKSeF` („Kontekst autoryzacji").
  Listy: `GetListSystemZewn()`, `GetListToken()`, `GetListKontekstAutentykacjiKSeF()`.
- `KSeFWyslijWorker`: `[Context] Dokument: DokumentHandlowy`, `[Context] Parametry: KSeFWyslijParams`,
  `[Context] Context: Context`. Akcja `object Wyslij(DokumentHandlowy dok)`.
- `KSeFWysylkaWsadowaWorker`: `[Context] Parametry: KsefEksportIWyslijParams`,
  `[Context(Required=false)] Dokumenty: DokumentHandlowy[]`, `[Context(Required=false)] Dokument: DokumentHandlowy`,
  `[Context] Context: Context`.
- Warunek wstępny (sprawdzany przez `WeryfikatorPolaXmlValidated`): każdy dokument musi mieć
  `dok.ImportExportKSeF.XmlValidated == ThreeStateBoolean.True` (czyli wcześniej wykonaną walidację struktury — HANDEL-W69).

**Snippet:**

```csharp
using Microsoft.Extensions.DependencyInjection;
using Soneta.KSeF.Workers;

var hm = session.GetHandel();
var dok = hm.DokHandlowe.WgNumer[...];          // zatwierdzona faktura sprzedaży

// 1) Walidacja daty wystawienia (offline, testowalne) — rzuca wyjątek dla daty != dziś
//    wg konfiguracji KSeF i uprawnień operatora:
KSeFWyslijWorker.Weryfikator(dok);

// 2) Przygotowanie parametrów autoryzacji (offline). System i token wybierane z list:
var ctx = session.GetEmptyContext();
ctx.TryAdd(() => dok);
var parametry = new KSeFWyslijParams(ctx);       // konstruktor sam wybiera domyślny system/token
// parametry.SystemZewn / parametry.Token można ustawić jawnie z GetListSystemZewn()/GetListToken()

// 3) Wysyłka pojedyncza — OPERACJA ONLINE (NIE testuj jednostkowo):
var worker = new KSeFWyslijWorker { Dokument = dok, Parametry = parametry, Context = ctx };
object wynik = worker.Wyslij(dok);               // wewnątrz: SesjaWysylki + WyslijDokument, zapis KSeFKomunikat

// Wysyłka zbiorcza — ONLINE; Dokument musi być pierwszym elementem tablicy Dokumenty:
DokumentHandlowy[] doks = hm.DokHandlowe.WgNumer[...].ToArray();
var workerZb = new KSeFWysylkaWsadowaWorker { Dokument = doks[0], Dokumenty = doks, Context = ctx, Parametry = paramsZb };
workerZb.WyslijZbiorczo();
```

**Pułapki:**
- **Tylko dokumenty zatwierdzone** podlegają wysyłce (`IsVisible*` wymagają `dok.Zatwierdzony`). Bufor i
  dokument anulowany nie są wysyłane.
- Przed wysyłką dokument **musi mieć zwalidowany XML** (`XmlValidated == True`) — inaczej `WeryfikatorPolaXmlValidated`
  rzuci wyjątek „nie posiada zweryfikowanego pliku XML". Najpierw wykonaj HANDEL-W69 (Sprawdź strukturę pliku) lub
  wygeneruj XML (wysyłka zbiorcza robi to automatycznie dla statusu `Brak`).
- Wysyłka zbiorcza **pomija** dokumenty: zaimportowane z KSeF (`ImportExportKSeF.Rodzaj == Import`), o
  nieprawidłowym/niezweryfikowanym XML, wygenerowane inną definicją niż w parametrach, w trybie offline z
  innym tokenem — wszystkie pominięcia trafiają do logu „KSeF".
- Cała komunikacja z bramką (`IKSeFAPIv2Service`/`IKSeFAPIService`) **wymaga sieci** → **NIE testuj
  jednostkowo**. W teście weryfikuj jedynie: utworzenie `KSeFWyslijParams`, dobór systemu/tokenu z list,
  `Weryfikator` oraz że XML jest zwalidowany.
- Po wysyłce na dokumencie zatwierdzonym ustawiana jest flaga `Session.SaveImmediatelyIfPossible = true`
  (natychmiastowy zapis komunikatu KSeF).

---

### HANDEL-W68 — Sprawdzenie statusu KSeF i odczyt numeru KSeF

**Cel:** po wysyłce sprawdzić w bramce, czy dokument został przyjęty, i pobrać nadany **numer KSeF**
(`KSeFSprawdzStatusWorker`). Sprawdzenie statusu to operacja **online** (NIE testuj); odczyt już zapisanego
statusu/numeru jest **offline** (testowalne — pola kalkulowane na dokumencie).

**Warianty:**

| Wariant | Mechanizm |
|---|---|
| Sprawdzenie statusu po sesji wysyłki | `KSeFSprawdzStatusWorker.SprawdzStatus(dok)` (akcja „KSeF/Sprawdź status") — ONLINE |
| Odczyt aktualnego statusu | `dok.StatusKSeF: KSeFState` — offline, kalkulowane |
| Odczyt numeru KSeF / nr referencyjnych | `dok.KSeFKomunikat.NumerDokumentuKSeF` itd. — offline |
| Czy dokument w ogóle podlega KSeF | `dok.PodlegaKSeF`, `dok.PosiadaKSeF` — offline |

**Pola i typy:**
- `dok.StatusKSeF: Soneta.Core.KSeF.KSeFState` (kalkulowane). Wartości:
  `NieDotyczy=1, Brak=2, DoWyslania=4, Wyslany=8, Przyjety=16, Odrzucony=32` (oraz `Robocze=14`, `Razem=31`
  zachowane dla kompatybilności). Status wyliczany z zawartości `KSeFKomunikat` i stanu dokumentu (Bufor/Anulowany ⇒ `NieDotyczy`).
- `dok.KSeFKomunikat` (rekord `KSeFKomunikat`): `NumerDokumentuKSeF: string` (numer nadany przez KSeF —
  ustawiony ⇒ status `Przyjety`), `NumerReferencyjnyKSeF: string`, `NumerReferencyjnySesjiKSeF: string`,
  `OpisBledu: string` (niepusty ⇒ status `Odrzucony`), `Offline: bool`, `TokenKSeF: SysZewToken`,
  `DataPrzeslaniaKSeF`, `DataPrzyjeciaKSeF`.
- `dok.PosiadaKSeF: bool` (ma plik `ImportExportKSeF`), `dok.PodlegaKSeF: bool`, `dok.QRCodeLink: string`.

**Snippet:**

```csharp
// Sprawdzenie statusu w bramce — OPERACJA ONLINE (NIE testuj jednostkowo):
var worker = new KSeFSprawdzStatusWorker();
MessageBoxInformation wynik = worker.SprawdzStatus(dok);   // pobiera status z sesji wysyłki

// Odczyt zapisanego statusu i numeru — OFFLINE, w pełni testowalne:
KSeFState status = dok.StatusKSeF;
if (status == KSeFState.Przyjety)
{
    string numerKSeF = dok.KSeFKomunikat.NumerDokumentuKSeF;     // numer nadany przez KSeF
    string nrSesji   = dok.KSeFKomunikat.NumerReferencyjnySesjiKSeF;
}
else if (status == KSeFState.Odrzucony)
{
    string blad = dok.KSeFKomunikat.OpisBledu;                  // przyczyna odrzucenia
}
```

**Pułapki:**
- `StatusKSeF` jest **kalkulowane** — nie da się go ustawić; zmienia się przez sam `KSeFKomunikat`.
- Sprawdzenie statusu działa tylko, gdy `dok.StatusKSeF != Przyjety` i istnieje `KSeFKomunikat` z numerem
  referencyjnym sesji; dokument w stanie `DoWyslania` nie ma jeszcze czego sprawdzać.
- Worker odczytuje status **wszystkich** dokumentów z tej samej sesji wysyłki (`NumerReferencyjnySesjiKSeF`)
  i każdemu z nich uzupełnia numer KSeF — to operacja zbiorcza po stronie bramki.
- Wywołanie `IKSeFAPIv2Service.SprawdzStatusDokumentowZSesji` **wymaga sieci** → **NIE testuj jednostkowo**.
  W teście weryfikuj jedynie wyliczanie `StatusKSeF` z różnych ustawień `KSeFKomunikat`.

---

### HANDEL-W69 — UPO, numer KSeF z duplikatu, walidacja struktury XML

**Cel:** trzy operacje pomocnicze KSeF: pobranie **UPO** (urzędowego poświadczenia odbioru) dla przyjętej
faktury, **odzyskanie numeru KSeF z duplikatu** (gdy bramka odrzuciła dokument kodem 440 = duplikat) oraz
**walidacja struktury XML** względem schematu (XSD). Walidacja XML jest **offline** (testowalna); pobranie
UPO jest **online** (NIE testuj). Pobranie numeru z duplikatu jest **offline** (parsuje istniejący `OpisBledu`).

**Warianty:**

| Wariant | Worker / akcja | Online? |
|---|---|---|
| Walidacja struktury XML | `KSeFSprawdzXMLWorker.Check()` (akcja „KSeF/Sprawdź strukturę pliku") | OFFLINE (lokalny XSD) |
| Pobranie UPO dla dokumentu | `KSeFSprawdzUPODokumentuWorker.SprawdzUPO()` (akcja „KSeF/Sprawdź UPO...") | ONLINE |
| Numer KSeF z duplikatu (błąd 440) | `PobierzNumerKSeFZDuplikatuWorker.PobierzNumerDokumentuKSeF(dok)` | OFFLINE (parsuje `OpisBledu`) |

**Pola i typy:**
- `KSeFSprawdzXMLWorker`: `[Context] Dokument: DokumentHandlowy`, metoda `void Check()`. Ustawia
  `dok.ImportExportKSeF.XmlValidated: ThreeStateBoolean` (`True`/`False`). Walidator publiczny:
  `KSeFSchemaVerifier.Verify(DokumentHandlowy dok)` (rzuca wyjątek przy niezgodności ze schematem).
- `KSeFSprawdzUPODokumentuWorker`: `[Context] Dokument`, `void SprawdzUPO()`. Wymaga
  `dok.StatusKSeF == Przyjety` i tokenu w wersji API v2 (`KSeFKomunikat.TokenKSeF.KSeFAPIv2`), inaczej rzuca
  `RowException`. Zapisuje rekord `KSeFUPO` i daty `DataPrzeslaniaKSeF`/`DataPrzyjeciaKSeF`.
- `PobierzNumerKSeFZDuplikatuWorker`: akcja `void PobierzNumerDokumentuKSeF(DokumentHandlowy dok)`.
  Aktywna, gdy `dok.KSeFKomunikat.OpisBledu` zawiera „440"; z opisu wyłuskuje numer dokumentu i sesji,
  ustawia `NumerDokumentuKSeF` / `NumerReferencyjnySesjiKSeF` (status przechodzi na `Przyjety`).

**Snippet:**

```csharp
// 1) Walidacja struktury XML — OFFLINE (lokalny XSD), w pełni testowalne:
var xmlWorker = new KSeFSprawdzXMLWorker { Dokument = dok };
xmlWorker.Check();
bool poprawny = dok.ImportExportKSeF.XmlValidated == ThreeStateBoolean.True;

// Alternatywnie sam weryfikator (rzuca wyjątek przy błędzie struktury):
KSeFSchemaVerifier.Verify(dok);

// 2) Numer KSeF z duplikatu — OFFLINE (parsuje OpisBledu z błędu 440):
var dupWorker = new PobierzNumerKSeFZDuplikatuWorker();
dupWorker.PobierzNumerDokumentuKSeF(dok);          // ustawia NumerDokumentuKSeF, jeśli OpisBledu zawiera "440"

// 3) Pobranie UPO — OPERACJA ONLINE (NIE testuj jednostkowo):
var upoWorker = new KSeFSprawdzUPODokumentuWorker { Dokument = dok };
upoWorker.SprawdzUPO();                              // wymaga StatusKSeF == Przyjety oraz API v2
```

**Pułapki:**
- `Check()` opiera się o **lokalny XSD** (`ImportExportKSeF.DefinicjaXmlNag.LocalXSD`) — nie potrzebuje
  sieci, dlatego jest **testowalny**. Wymaga jednak wcześniej wygenerowanego XML (`ImportExportKSeF.Xml`
  niepusty — `IsEnabledCheck`).
- `SprawdzUPO()` rzuca `RowException`, gdy dokument nie jest `Przyjety` albo nie był wysłany w API v2 —
  obsłuż to przed wywołaniem. Samo pobranie UPO **wymaga sieci** → NIE testuj.
- `PobierzNumerDokumentuKSeF` ustawia w `OpisBledu` znacznik `DokumentHandlowy.PobranoNumerKSeFZDuplikatuOpis`
  (link QR z duplikatu może nie działać) — to celowy efekt uboczny, nie błąd.
- Walidacja statusu „440 = duplikat" działa wyłącznie na tekście `OpisBledu` — jeśli opis nie zawiera „440",
  worker nic nie robi.

---

### HANDEL-W70 — Import faktur z KSeF (dokumenty zakupu)

**Cel:** pobrać z KSeF faktury zakupowe (oraz sprzedażowe), zapisać je jako pliki KSeF (`KSeFPlik`) w bazie,
a następnie utworzyć z nich dokumenty zakupu. **Cały proces pobierania jest online** (komunikacja z bramką)
i operuje na rekordach **konfiguracyjno-systemowych** (`KSeFZapytanieOFa`, `KSeFPlik`), a tworzenie
dokumentów zakupu z plików KSeF realizowane jest w module księgowym — **NIE testuj jednostkowo** części
sieciowej.

**Warianty:**

| Wariant | Mechanizm |
|---|---|
| Zapytanie o faktury za okres | rekord `KSeFZapytanieOFa` + parametry `ParametryPobieraniaFakturKSeF` (`DataOd`, `DataDo`, `PodmiotTworzeniaZapytaniaKSeF`) |
| Pobranie paczek wyników | `KSeFDownloadPartWorker.Pobierz()` (akcja „Pobierz pakiety") — ONLINE; tworzy `KSeFPlik` |
| Kwalifikacja kierunku (zakup/sprzedaż) | wg porównania NIP z pieczątki firmy z NIP-em Podmiot1 w XML |
| Utworzenie dokumentu zakupu | z `KSeFPlik` (import XML do dokumentu) — obszar księgowy |

**Pola i typy:**
- `KSeFDownloadPartWorker`: `[Context] KSeFZapytanieOFa: KSeFZapytanieOFa`, akcja `object Pobierz()`.
  Pobiera tylko, gdy `KSeFZapytanieOFa.StatusZapytania == StatusZapytania.Przetworzono` i nie pobrano
  jeszcze wszystkich paczek (`PobraneWszystkie`).
- `ParametryPobieraniaFakturKSeF`: `DataOd: DateTimeOffset`, `DataDo: DateTimeOffset`,
  `PodmiotTworzeniaZapytaniaKSeF`, `PobieranieSamofakturowania`.
- Wynik: rekordy `KSeFPlik` (z `RodzajDokumentuKSeFZapytanieOFa`: `Sprzedaz`/`Zakup`/`Razem`) tworzone przez
  `KSeFPlik.CreateKSefPlik(...)`. Formularz `FA_RR` jest pomijany.

**Snippet:**

```csharp
// Pobranie paczek z wynikami zapytania — OPERACJA ONLINE (NIE testuj jednostkowo):
var worker = new KSeFDownloadPartWorker { KSeFZapytanieOFa = zapytanie };
object wynik = worker.Pobierz();      // tworzy rekordy KSeFPlik dla faktur z bramki

// Po pobraniu pliki KSeF są dostępne w module Core (KSeFPliki) i mogą zostać
// zaimportowane jako dokumenty zakupu (obszar księgowy). Kierunek (Zakup/Sprzedaz)
// kwalifikowany jest automatycznie przez porównanie NIP-u z pieczątki firmy z NIP-em
// nadawcy (Podmiot1) w pliku XML.
```

**Pułapki:**
- Pobranie paczek **wymaga sieci** (`IKSeFAPIv2Service.PobierzFakturyZPaczek`) → **NIE testuj jednostkowo**.
- Import opiera się o rekordy `KSeFZapytanieOFa`/`KSeFPlik`, a nie bezpośrednio o `DokumentHandlowy` —
  dokument zakupu powstaje dopiero w kolejnym kroku (import XML), poza zakresem prostego workera na dokumencie.
- Pliki o tym samym numerze KSeF są **pomijane** (deduplikacja po numerze), tak samo formularz `FA_RR`.
- Z poziomu dodatku zewnętrznego operuj na publicznych `ParametryPobieraniaFakturKSeF` i statusie zapytania;
  testuj wyłącznie logikę przygotowania parametrów (okres, podmiot), nie samo pobieranie.

---

### HANDEL-W71 — Fiskalizacja dokumentu (paragon fiskalny)

**Cel:** oznaczyć / wydrukować dokument sprzedaży jako paragon na drukarce fiskalnej. **Wydruk na drukarce
to operacja sprzętowa** (NIE testuj). Worker `FiskalizacjaDokumentuWorker` ma jednak rolę „oznacz jako
zafiskalizowane" — ustawienie `SymbolKasy` na zatwierdzonym dokumencie jest **offline** (testowalne).

**Warianty:**

| Wariant | Mechanizm | Sprzęt? |
|---|---|---|
| Oznacz jako zafiskalizowane | `FiskalizacjaDokumentuWorker.Execute()` (akcja „Narzędziowe/Oznacz jako zafiskalizowane") | NIE (tylko ustawia `SymbolKasy`) |
| Symbol drukarki tekstowo | `ParametryFiskalizacjiDokumentu.SymbolKasy: string` (max 12) | — |
| Symbol drukarki z listy (z bazy) | `ParametryFiskalizacjiDokumentu.SymbolKasyEnum` + `GetListSymbolKasyEnum()` | — |
| Faktyczny wydruk fiskalny | `Fiscalizer` (klasa fiskalizatora) | TAK |

**Pola i typy:**
- `FiskalizacjaDokumentuWorker`: `[Context] Dokument: DokumentHandlowy`,
  `[Context] Parametry: ParametryFiskalizacjiDokumentu`, metoda `void Execute()`.
- `ParametryFiskalizacjiDokumentu : ContextBase` — `SymbolKasy: string` (`[MaxLength(12)]`, „Symbol drukarki"),
  `SymbolKasyEnum: string` (combo, gdy dane drukarki w bazie), `GetListSymbolKasyEnum(): List<string>`.
- Pola dokumentu: `dok.SymbolKasy: string` (ustawiane przez `UstawSymbolKasy`), `dok.Kategoria: KategoriaHandlowa`.
- `IsVisibleExecute`: tylko `Sprzedaż`/`KorektaSprzedaży`. `IsEnabledExecute`: dokument **zatwierdzony**
  i z **pustym** `SymbolKasy`.

**Snippet:**

```csharp
// Oznaczenie dokumentu jako zafiskalizowanego (OFFLINE — ustawia tylko SymbolKasy):
var ctx = session.GetEmptyContext();
ctx.TryAdd(() => dok);
var parametry = new FiskalizacjaDokumentuWorker.ParametryFiskalizacjiDokumentu(ctx)
{
    SymbolKasy = "DRUK1"        // symbol drukarki, max 12 znaków
};

var worker = new FiskalizacjaDokumentuWorker { Dokument = dok, Parametry = parametry };
worker.Execute();               // wykona się tylko gdy dok zatwierdzony i SymbolKasy pusty

// Po operacji:
string symbol = dok.SymbolKasy; // "DRUK1"

// Faktyczny wydruk na drukarce fiskalnej — OPERACJA SPRZĘTOWA (NIE testuj):
// var fiscalizer = new Fiscalizer(dok);
// fiscalizer.Fiscalize(false);
```

**Pułapki:**
- `Execute()` z `FiskalizacjaDokumentuWorker` **nie drukuje** — jedynie ustawia `SymbolKasy` i dopisuje
  informację o fiskalizacji do zmian zapisu. Faktyczny wydruk realizuje klasa `Fiscalizer` (sprzęt) → NIE testuj.
- Operacja działa wyłącznie dla dokumentów **zatwierdzonych** o pustym `SymbolKasy` (`IsEnabledExecute`) i
  kategorii `Sprzedaż`/`KorektaSprzedaży` (`IsVisibleExecute`).
- `SymbolKasy` jest przycinany (`Trim`) i ograniczony do 12 znaków; wybór z listy (`SymbolKasyEnum`)
  dostępny tylko, gdy konfiguracja trzyma dane drukarek w bazie (`Config.DrukarkaFiskalna.DaneDrukarkiZapisywaneWBazie`).
- W teście weryfikuj jedynie ustawienie `dok.SymbolKasy` i warunki `IsEnabled/IsVisible` — nie symuluj wydruku.

---

### HANDEL-W72 — E-paragon (e-mail) i ponowny wydruk paragonu

**Cel:** obsłużyć **e-paragon** (paragon w formie elektronicznej wysyłany e-mailem) oraz **ponowny wydruk**
paragonu na drukarce fiskalnej. Ustawienie pól e-paragonu (`EParagon`, adres e-mail) jest **offline**
(testowalne); wysyłka e-mail i wydruk na drukarce są **online/sprzętowe** (NIE testuj).

**Warianty:**

| Wariant | Mechanizm | Online/sprzęt? |
|---|---|---|
| Oznaczenie dokumentu jako e-paragon | `dok.EParagon: bool`, `dok.EParagonAdresEmail: string` | NIE (pola) |
| Polityka e-paragonu na definicji | `Definicja.OznaczJakoEParagon: OznaczJakoEParagon` | NIE |
| Odczyt danych wysłanego e-paragonu | `dok.DaneEParagonu: DaneEParagonu`, `dok.OtworzUrlEParagonu()` | NIE (odczyt) |
| Ponowny wydruk paragonu | `PonownyWydrukParagonuWorker.Drukuj()` (akcja „Narzędziowe/Wydrukuj ponownie...") | TAK (sprzęt) |

**Pola i typy:**
- `dok.EParagon: bool` — czy dokument jest e-paragonem; ustawienie `EParagonAdresEmail` automatycznie ustawia
  `EParagon` (poza polityką `OznaczJakoEParagon.Zawsze`).
- `dok.EParagonAdresEmail: string` — adres e-mail odbiorcy e-paragonu (walidowany; przy `EParagon==true`
  nie może być pusty).
- `Definicja.OznaczJakoEParagon: Soneta.Handel.OznaczJakoEParagon` — `Nigdy=0, Zawsze=1, WgKontrahenta=2`.
- `dok.DaneEParagonu: DaneEParagonu`, `dok.OtworzUrlEParagonu(): HyperlinkResult`.
- `PonownyWydrukParagonuWorker`: `[Context] Paragon: DokumentHandlowy`, akcja `object Drukuj()`.
  `IsVisibleDrukuj`: definicja `Fiskalizowany`, dokument zatwierdzony, niepusty `SymbolKasy`.

**Snippet:**

```csharp
// Oznaczenie dokumentu jako e-paragon i ustawienie adresu e-mail (OFFLINE — testowalne):
using (var t = session.Logout(true))
{
    dok.EParagonAdresEmail = "klient@example.com";   // ustawia też EParagon = true
    t.Commit();
}
session.Save();

bool jestEParagonem = dok.EParagon;                  // true

// Odczyt danych wysłanego e-paragonu (offline):
DaneEParagonu dane = dok.DaneEParagonu;

// Ponowny wydruk na drukarce fiskalnej — OPERACJA SPRZĘTOWA (NIE testuj jednostkowo):
var worker = new PonownyWydrukParagonuWorker { Paragon = dok };
object wynik = worker.Drukuj();   // pyta o potwierdzenie, następnie Fiscalizer.Fiscalize(false)
```

**Pułapki:**
- Ustawienie `EParagonAdresEmail` ma efekt uboczny: dla polityki innej niż `Zawsze` automatycznie ustawia
  `EParagon = !string.IsNullOrWhiteSpace(value)`. Przy `EParagon==true` pusty adres e-mail nie przejdzie
  walidacji (`EParagonVerifier`/`EParagonEmailVerifier`).
- **Sama wysyłka e-paragonu e-mailem wymaga sieci**, a ponowny wydruk — drukarki fiskalnej → **NIE testuj
  jednostkowo**. Testuj jedynie ustawienie `EParagon`/`EParagonAdresEmail` i wyliczanie polityki `OznaczJakoEParagon`.
- `PonownyWydrukParagonuWorker.Drukuj()` wyświetla pytanie „czy wysłać ponownie" (`MessageBoxInformation`) —
  faktyczny wydruk dzieje się w handlerze `YesHandler` przez `Fiscalizer`.
- Ponowny wydruk dostępny tylko dla dokumentu z definicji **fiskalizowanej**, zatwierdzonego, z niepustym
  `SymbolKasy` (czyli już raz zafiskalizowanego).

---

### HANDEL-W73 — Dokument kompletacji (złożenie / rozłożenie kompletu)

**Cel:** obsłużyć kompletację „w locie" — rozbicie pozycji-kompletu na składniki (rozchód składników,
przychód wyrobu) wg kartoteki kompletacji. Worker `DokumentKompletacjaWorker` udostępnia przeliczenie
pozycji wg kartoteki, wycofujące ręczne zmiany użytkownika. To operacja **w pełni lokalna** (offline) —
testowalna, choć wymaga poprawnie skonfigurowanej definicji kompletacji i magazynu.

**Warianty:**

| Wariant | Mechanizm |
|---|---|
| Przelicz składniki/produkty wg kartoteki | `DokumentKompletacjaWorker.PrzeliczWgKartoteki(dok)` (akcja w menu Czynności) |
| Definicja dokumentu kompletacji | `Definicja.SposobEdycjiKompletacji: SposobEdycjiKompletacji` (≠ `None`) |
| Powiązanie składniki ↔ wyrób | dokumenty kompletacji rozchodu/przychodu z `DefDokHandlowych` |
| Powiązanie z obrotami | obroty rozchodowe składników i przychodowy wyrobu po `Save` |

**Pola i typy:**
- `DokumentKompletacjaWorker`: akcja `void PrzeliczWgKartoteki(DokumentHandlowy dokument)`. Wycofuje
  relacje podrzędne pozycji (`pozycja.PodrzędneRelacje`) i przelicza kompletację wg kartoteki.
- `dok.Definicja.SposobEdycjiKompletacji: Soneta.Handel.SposobEdycjiKompletacji` — gdy `None`, akcja
  niewidoczna (`IsVisiblePrzeliczWgKartoteki`).
- Definicje kompletacji w module: `hm.DefDokHandlowych.Kompletacja`, `.KompletacjaRozchód`,
  `.KompletacjaPrzychód` (typu `DefDokHandlowego`).
- Powiązania składników/wyrobu: pozycje dokumentu (`dok.Pozycje`) i ich relacje
  (`PozycjaDokHandlowego.PodrzędneRelacje` typu `PozycjaRelacjiHandlowej`).

**Snippet:**

```csharp
using Soneta.Handel.Kompletacje;

// Dokument kompletacji „w locie" — definicja musi mieć SposobEdycjiKompletacji != None:
var dok = hm.DokHandlowe.WgNumer[...];

// Przeliczenie składników i wyrobu wg kartoteki kompletacji (OFFLINE, w transakcji wew. workera):
var worker = new DokumentKompletacjaWorker();
worker.PrzeliczWgKartoteki(dok);   // wycofuje zmiany użytkownika, odtwarza komplet z kartoteki
session.Save();                    // obroty składników (rozchód) i wyrobu (przychód) księgowane przy Save

// Sprawdzenie, czy dokument w ogóle obsługuje kompletację:
bool kompletacja = dok.Definicja.SposobEdycjiKompletacji != SposobEdycjiKompletacji.None;
```

**Pułapki:**
- `PrzeliczWgKartoteki` **kasuje ręczne zmiany użytkownika** w kompletacji i odtwarza komplet z kartoteki —
  to operacja jednokierunkowa, nie „aktualizacja przyrostowa".
- Worker steruje wewnętrzną flagą `dok.BezKopiowania` (włącza/wyłącza w `try/finally`) — nie ustawiaj jej
  samodzielnie obok wywołania workera.
- Akcja jest niewidoczna dla `SposobEdycjiKompletacji == None` oraz dla dokumentu w stanie `Detached`/`Deleted`
  (`IsVisiblePrzeliczWgKartoteki`).
- Obroty magazynowe (rozchód składników, przychód wyrobu) powstają dopiero po `Session.Save()` — w teście
  zastosuj wzorzec „zapis → `SaveDispose()` → odczyt na świeżej sesji" i pamiętaj o blokadzie stanu ujemnego
  w bazie Demo (składniki muszą mieć wcześniejszy zapisany przychód).

---

### HANDEL-W74 — Intrastat (dane statystyczne i wyszukanie dokumentów do deklaracji)

**Cel:** uzupełnić na pozycjach dokumentu dane potrzebne do deklaracji Intrastat (kod CN, masa, kraj
pochodzenia, ilość w jednostkach uzupełniających) za pomocą `DokumentHandlowyZmienIntrastatWorker`, oraz
wyszukać dokumenty kwalifikujące się do deklaracji przywozu/wywozu za okres. Operacja jest **w pełni lokalna**
(offline) — testowalna.

**Warianty:**

| Wariant | Mechanizm |
|---|---|
| Aktualizacja danych Intrastat na pozycjach | `DokumentHandlowyZmienIntrastatWorker.Update()` (akcja „Aktualizuj dane dla Intrastatu ...") |
| Wybór aktualizowanych danych | `DokumentHandlowyZmienIntrastatParams`: `KodCN`, `Masa`, `Kraj`, `Przelicznik` (bool) |
| Rodzaj Intrastat na definicji | `Definicja.Intrastat: RodzajIntrastat` (`NieUwzględniaj`/`Przywóz`/`Wywóz`/`PrzywózWPodrzędnym`) |
| Typ deklaracji (przywóz/wywóz) | `TypDeklaracji.IntrastatPrzywóz` / `IntrastatWywóz` |
| Okres dokumentu do deklaracji | `dok.OkresIntrastat` (miesiąc deklaracji) |

**Pola i typy:**
- `DokumentHandlowyZmienIntrastatWorker`: konstruktor `(DokumentHandlowyZmienIntrastatParams @params)`
  z `[Context]`; `[Context] Dokument: DokumentHandlowy`, `[Context(Required=false)] Dokumenty: DokumentHandlowy[]`,
  `Params` (read-only). Akcja `object Update()`.
- `DokumentHandlowyZmienIntrastatParams : ContextBase` — `KodCN: bool` („Kod CN"), `Masa: bool` („Masa"),
  `Kraj: bool` („Kraj pochodzenia"), `Przelicznik: bool` („Ilość w jedn. uzupełn.").
- `dok.Definicja.Intrastat: Soneta.Magazyny.RodzajIntrastat`. `dok.KierunekMagazynu: Soneta.Magazyny.KierunekPartii`
  (kraj pochodzenia aktualizowany tylko, gdy `KierunekMagazynu != Brak`).
- Wykonawcza metoda dokumentu: `dok.UaktualnijIntrastat(bool kodCN, bool masa, bool kraj, bool przelicznik): int`
  (zwraca liczbę zaktualizowanych pozycji).

**Snippet:**

```csharp
using Soneta.Deklaracje.UE;
using static Soneta.Deklaracje.UE.DokumentHandlowyZmienIntrastatWorker;

// Aktualizacja danych Intrastat na pozycjach dokumentu (OFFLINE — testowalne):
var ctx = session.GetEmptyContext();
ctx.TryAdd(() => dok);
var parametry = new DokumentHandlowyZmienIntrastatParams(ctx)
{
    KodCN = true,        // przepisz kod CN z kartoteki towaru
    Masa = true,         // przelicz masę pozycji
    Kraj = true,         // ustaw kraj pochodzenia
    Przelicznik = true   // ilość w jednostce uzupełniającej
};

var worker = new DokumentHandlowyZmienIntrastatWorker(parametry) { Dokument = dok };
worker.Update();         // aktualizuje pozycje; pomija dokumenty z Definicja.Intrastat == NieUwzględniaj
session.Save();

// Wyszukanie dokumentów do deklaracji za okres — filtr serwerowy po rodzaju Intrastatu i okresie:
var hm = session.GetHandel();
var okres = new FromTo(Date.Today.FirstDayMonth(), Date.Today.LastDayMonth());
foreach (DokumentHandlowy d in hm.DokHandlowe.WgNumer[(DokumentHandlowy d) =>
             d.OkresIntrastat >= okres.From && d.OkresIntrastat <= okres.To])
{
    bool przywoz = d.Definicja.Intrastat == RodzajIntrastat.Przywóz
                   || d.Definicja.Intrastat == RodzajIntrastat.PrzywózWPodrzędnym;
    // przywoz == true ⇒ TypDeklaracji.IntrastatPrzywóz, w przeciwnym razie IntrastatWywóz
}
```

**Pułapki:**
- `Update()` rzuca `ApplicationException`, gdy dokument zawiera **koszty dodatkowe z podziałem wg masy**
  (`PodzialKosztuDodatkowego == Masa`) a zaznaczono aktualizację masy — nie da się wtedy przeliczyć masy.
- Dokumenty z `Definicja.Intrastat == RodzajIntrastat.NieUwzględniaj` są **pomijane** (akcja niewidoczna —
  `IsVisibleUpdate`).
- Kraj pochodzenia aktualizowany jest tylko, gdy `dok.KierunekMagazynu != KierunekPartii.Brak` — sam parametr
  `Kraj=true` nie wystarczy dla dokumentu bez ruchu magazynowego.
- Jeśli istnieje już **zatwierdzona deklaracja** Intrastat za dany okres (`OkresIntrastat.LastDayMonth()`),
  worker dopisze do logu ostrzeżenie, że dane nie zmienią się w zatwierdzonej deklaracji (trzeba wygenerować
  korektę) — aktualizacja na dokumencie i tak się wykona.
- Wyszukiwanie dokumentów do deklaracji filtruj **serwerowo** po `OkresIntrastat` i rodzaju Intrastatu z
  definicji — nie ładuj całej tabeli `DokHandlowe` do pamięci (safe-code §6).

---

