# Kontrahent — receptury kodu biznesowego (Soneta / enova365)

Zbiór gotowych wzorców kodu dla obiektu biznesowego **`Soneta.CRM.Kontrahent`** (tabela `Kontrahenci`).
Dokument jest częścią skilla `soneta-programming`. Celem jest, aby agent pisał **bezbłędny kod
biznesowy** operujący na kontrahencie — trafiający w realne pola, kolekcje i workery platformy.

> Format **zwarty**: każdy wzorzec opisuje ogólny przypadek + tabelę wariantów, zamiast wielu wąskich
> pozycji. Fundamenty (sesja, transakcja, blokada optymistyczna, praca z `SubTable`, obsługa błędów)
> są opisane w [`safe-code.md`](../safe-code.md), [`session-login.md`](../session-login.md) oraz
> [`worker-extender.md`](../worker-extender.md) — tutaj się do nich odwołujemy, nie powtarzamy ich.
>
> **Cały kod w tym dokumencie jest zgodny z C# 10** (target-typed `new`, `var`, wyrażenia `switch`,
> nazwane parametry `bool`). Snippety operują wyłącznie na **publicznym kontrakcie** platformy — nie
> ma odwołań do prywatnych klas ani kodu źródłowego aplikacji.

## Fakty o typie (zweryfikowane skanem DLL — `scan-props.csx`)

- **Klasa biznesowa:** `Soneta.CRM.Kontrahent` — `GuidedRow` (root), tabela `Soneta.CRM.Kontrahenci`.
- **Implementuje:** `IPodmiot`, `IKontrahent`, `IPodmiotKasowy`, `IElementSlownika`, `IAdresHost`,
  `IKodowany`, `IAdresyWWWHost`, `IDaneKontaktoweHost`, `IEmailElement`, `IRegonHost`,
  `IGIODOZgodnyHost`, `IGIODOWymianaDanychHost`, `IGIODOOświadczenieHost`.
- **Pola:** 75 bazodanowych + 142 kalkulowane.
- **Moduł:** `Soneta.CRM.CRMModule`, dostęp `session.GetCRM()`. Tabela: `crm.Kontrahenci`.
- **Kluczowe pola bazodanowe (zapisywalne):** `Kod: string`, `Nazwa: string`, `NIP: string`,
  `EuVAT: string`, `PESEL: string`, `REGON: string`, `KRS: string`,
  `StatusPodmiotu: Soneta.Core.StatusPodmiotu`, `RodzajPodmiotu: Soneta.Core.RodzajPodmiotu`
  (= „Rodzaj VAT dla sprzedaży"), `RodzajPodmiotuZakup: Soneta.Core.RodzajPodmiotu`,
  `PodatnikVAT: bool`, `VATLiczonyOd: Soneta.CRM.VatKontahentaLiczonyOd`,
  `FormaPrawna: Soneta.CRM.FormaPrawna`, `Waluta: Soneta.Waluty.Waluta`,
  `SposobZaplaty: Soneta.Kasa.FormaPlatnosci`, `Termin: int`, `TerminPlanowany: int`,
  `LimitKredytu: Currency`, `TypLimituKredytowego: Soneta.CRM.TypLimituKredytowego`,
  `KontrolaKwota: Currency`, `KontrolaDni: int`, `TypPrzeterminowania: Soneta.CRM.TypLimituKredytowego`,
  `Blokada: bool`, `BlokadaSprzedazy: bool`, `Zamiennik: Kontrahent`,
  `EFaktura: Soneta.Core.EFaktura`, `EFakturaOkres: FromTo`,
  `NieWindykowac: bool`, `DefinicjaSprawyWindykacyjnej`, `OddzialFirmy`, `Region`, `Rabat: Percent`,
  `DomyslnySzablonPolOpcjonalnychKSeF`, `KSeFSposobObslugiWysylkiCeny`.
- **Pola złożone:** `Adres: Soneta.Core.Adres`, `AdresDoKorespondencji: Soneta.Core.Adres`,
  `Kontakt: Soneta.Core.Kontakt` (`Kontakt.EMAIL`, `Kontakt.TelefonKomorkowy`, `Kontakt.WWW`,
  `Kontakt.SkrytkaPocztowa`, `Kontakt.Skype`), `OdsKarne: Soneta.Kasa.OdsetkiKarne`.
- **Pola kalkulowane (tylko do odczytu):** `Nazwa` jest zapisywalna, ale `NazwaFormatowana`,
  `NazwaPierwszaLinia`, `KodKraju`, `JestIncydentalny`, `IsStandard`, `DomyslnyRachunek`,
  `Platnik`, `LimitNieograniczony`, `PrzeterminowanieNieograniczone`, `KontrolaAktywna`,
  `AktualnyStatusVAT`, `AktualnyStatusVATMF`, `AktualnyStatusVATVies` — **nie ustawiaj** ich
  bezpośrednio.
- **Kolekcje (`SubTable`):** `Osoby` (`SubTable<KontaktOsoba>`), `Kontakty` (`SubTable<DaneKontaktowe>`),
  `AdresyWWW` (`SubTable<AdresWWW>`), `Kategorie` (`SubTable<KategoriaKth>`),
  `Branze` (`SubTable<BranzaKth>`), `Opiekunowie` (`SubTable<Opiekun>`),
  `Rachunki` (`SubTable<Soneta.Kasa.RachunekBankowyPodmiotu>`),
  `Rozrachunki` (`SubTable<Soneta.Kasa.RozrachunekIdx>`), `Podrzedni` (`SubTable<RelacjaPodmiotu>`),
  `StatusyVAT` (`SubTable<StatusVAT>`), `KodyKreskowe` (`SubTable<KodKreskowy>`),
  `GIODOOświadczenia` (`SubTable<GIODOOświadczenie>`), `GIODOUdostępnienia` (`SubTable<GIODOWymianaDanych>`),
  `PotwierdzeniaGIODO` (`SubTable<GIODOZgodny>`).
- **Cechy:** `Features: Soneta.Business.FeatureCollection` (indeksator po nazwie definicji cechy).

## Szablon wzorca

Każdy wzorzec (`Wn`) ma stałą strukturę:

- **Cel** — co robi i kiedy go użyć.
- **Warianty** — tabela odmian przypadku.
- **Pola i typy** — realne właściwości/kolekcje i ich typy.
- **Snippet** — kod C# 10.
- **Pułapki** — typowe błędy i zasady safe-code.

---

## 1. Wyszukiwanie i identyfikacja

### W1 — Wyszukiwanie kontrahenta

**Cel:** odnaleźć istniejącego kontrahenta po wybranym kluczu, zanim zaczniemy go modyfikować lub
zanim utworzymy nowy rekord.

**Warianty:**

| Wariant | Klucz | Uwaga |
|---|---|---|
| Po kodzie | `Kod` | indeks `WgKodu`, klucz unikalny — zwraca pojedynczy rekord |
| Po nazwie / fragmencie | `Nazwa` | indeks `WgNazwy` (nieunikalny) lub `SubTable[pattern]` |
| Po NIP / EU VAT | `NIP`, `EuVAT` | normalizacja: `Nip.Flat` / `EuVat.Flat` przed porównaniem |
| Po adresie | `Adres.*` | miejscowość, kod pocztowy, ulica |
| Po PESEL / REGON / KRS | `PESEL`, `REGON`, `KRS` | osoby fizyczne / podmioty |
| Dedup przed dodaniem | `NIP` | sprawdzenie, czy podmiot już istnieje |
| Kontrahent incydentalny | `JestIncydentalny` | systemowy rekord (`Kontrahent.INCYDENTALNY`) |

**Pola i typy:** `Kod: string`, `NIP: string`, `EuVAT: string`, `Nazwa: string`,
`Adres: Soneta.Core.Adres`, `PESEL/REGON/KRS: string`, `JestIncydentalny: bool`.

**Snippet:**

```csharp
var crm = session.GetCRM();

// 1. Po kodzie — klucz unikalny, zwraca pojedynczy rekord lub null
Kontrahent poKodzie = crm.Kontrahenci.WgKodu["ABC"];

// 2. Po nazwie — indeks nieunikalny, zwraca zbiór; bierzemy pierwszy
Kontrahent poNazwie = crm.Kontrahenci.WgNazwy["Firma XYZ"].FirstOrDefault();

// 3. Po NIP — filtr serwerowy; warunek aplikujemy na indeksie. Porównania tekstowe są case-insensitive
var nip = Nip.Flat("123-456-32-18");           // usuwa myślniki
Kontrahent poNip = crm.Kontrahenci.WgNIP[(Kontrahent k) => k.NIP == nip].FirstOrDefault();

// 4. Po fragmencie nazwy / mieście — serwerowy LIKE (warunek na indeksie WgNazwy)
foreach (Kontrahent k in crm.Kontrahenci.WgNazwy[(Kontrahent k) =>
             k.Nazwa.Contains("bud") && k.Adres.Miejscowosc == "Kraków"])
{
    // ...
}

// 5. Dedup przed dodaniem nowego kontrahenta
bool juzIstnieje = crm.Kontrahenci.WgNIP[(Kontrahent k) => k.NIP == nip].Any();
```

**Pułapki:**
- `WgKodu[...]` zwraca pojedynczy rekord (klucz unikalny) — może być `null`. `WgNazwy[...]` zwraca
  **zbiór** (klucz nieunikalny), trzeba `.FirstOrDefault()`/iterację.
- **Nie iteruj całej tabeli** `Kontrahenci` z `if` w pamięci — to tabela kartotekowa (rośnie z
  biznesem). Filtruj przez warunek aplikowany **na indeksie**, np.
  `crm.Kontrahenci.WgKodu[(Kontrahent k) => …]` (warunek wykonywany przez SQL). Indeksator samej
  tabeli (`crm.Kontrahenci[…]`) służy do dostępu po `ID`/kluczu, nie przyjmuje wyrażenia LINQ.
  Patrz [`rowcondition.md`](../rowcondition.md) i [`safe-code.md`](../safe-code.md) §6.
- W `RowCondition` (wyrażeniu LINQ) wolno użyć **tylko pól bazodanowych**. `NazwaFormatowana`,
  `KodKraju`, `Platnik` są kalkulowane → rzucą `LinqConditionException`.
- Porównania tekstowe w warunku są **case-insensitive** — nie dubluj `ToLower()`.
- Przed porównaniem NIP/EU VAT normalizuj wejście (`Nip.Flat`, `EuVat.Flat`), bo w bazie bywają
  formaty z myślnikami i bez.

### W2 — Walidacja NIP / REGON / EU VAT

**Cel:** sprawdzić poprawność NIP/REGON (suma kontrolna) i EU VAT (format/kraj) przed zapisem,
niezależnie od weryfikacji online (W15).

**Warianty:**

| Wariant | Wejście | Metoda publiczna |
|---|---|---|
| NIP krajowy | 10 cyfr lub `DDD-DDD-DD-DD` | `Soneta.Core.Nip.Test(string)` |
| REGON 9/14 | 9 lub 14 cyfr | `Soneta.Core.Regon.Test(string)` |
| EU VAT | prefiks kraju + numer | `Soneta.Core.EuVat.Test(string, ISessionable)` |
| Normalizacja | usunięcie myślników/spacji | `Nip.Flat`, `Nip.Format`, `EuVat.Flat` |
| Rozbicie EU VAT | kraj + numer | `EuVat.Split(value, out country, out nip)` |

**Pola i typy:** `NIP: string`, `REGON: string`, `EuVAT: string`. Walidatory są **statyczne**;
`EuVat.Test` wymaga `ISessionable` (sprawdza listę krajów UE w bazie).

**Snippet:**

```csharp
// Walidatory rzucają NullReferenceException dla null — najpierw odsiej puste wejście.
if (!nip.IsNullOrEmpty() && Nip.Test(nip)) { /* NIP poprawny */ }
if (!regon.IsNullOrEmpty() && Regon.Test(regon)) { /* REGON poprawny */ }
if (!euVat.IsNullOrEmpty() && EuVat.Test(euVat, session)) { /* EU VAT poprawny */ }

// Rozbicie EU VAT "PL1234563218" -> kraj "PL", numer "1234563218"
EuVat.Split(euVat, out string kodKraju, out string numer);

// Walidacja w event-handlerze zapisu (rzut PRZED Commit/Save):
if (!kontrahent.NIP.IsNullOrEmpty() && !Nip.Test(kontrahent.NIP))
    throw new RowException(kontrahent, "Nieprawidłowy NIP".Translate(), nameof(kontrahent.NIP));
```

**Pułapki:**
- `Nip.Test`, `Regon.Test`, `EuVat.Test` **rzucają `NullReferenceException` dla `null`** (odwołują się
  do `.Length`). Zawsze najpierw sprawdź `IsNullOrEmpty`.
- To walidacja **formatu/sumy kontrolnej**, a nie weryfikacja w MF/VIES — patrz W15.
- Komunikaty walidacyjne rzucaj jako `RowException(row, "…".Translate(), nameof(Pole))` **przed**
  `Commit()` (safe-code §5.1). Wyjątek po `Commit()` nie wycofa zmiany z sesji.
- Ustawienie `NIP`/`EuVAT` na samym `Kontrahent` uruchamia wbudowaną synchronizację (NIP↔EuVAT,
  auto-zmiana `RodzajPodmiotu`) — własna walidacja jest dodatkiem, nie zastępstwem.

---

## 2. Tworzenie, modyfikacja, usuwanie

### W3 — Tworzenie kontrahenta

**Cel:** utworzyć nowy rekord kontrahenta z poprawnym minimalnym zestawem pól i wartościami domyślnymi.

**Warianty:**

| Wariant | Charakterystyka | Pola krytyczne |
|---|---|---|
| Podmiot gospodarczy krajowy | firma w PL | `StatusPodmiotu=PodmiotGospodarczy`, `RodzajPodmiotu=Krajowy`, `NIP` |
| Unijny / zagraniczny | sprzedaż wewn.-unijna / eksport | `EuVAT`, `RodzajPodmiotu=Unijny/Eksportowy` |
| Osoba fizyczna / finalny | konsument | `StatusPodmiotu=Finalny`, `PESEL` |

**Pola i typy:** `Kod: string`, `Nazwa: string`, `StatusPodmiotu: Soneta.Core.StatusPodmiotu`
(`PodmiotGospodarczy=0`, `Finalny=1`), `RodzajPodmiotu: Soneta.Core.RodzajPodmiotu`
(`Krajowy=0`, `Eksportowy=1`, `EksportowyPodróżny=2`, `Unijny=3`, `UnijnyTrójstronny=4`, `BezVAT=5`),
`PodatnikVAT: bool`, `FormaPrawna: Soneta.CRM.FormaPrawna`.

**Nadawanie kodu / numeracji:** `Kod` jest polem tekstowym ustawianym jawnie. Może być wymagana jego
unikalność (zależnie od konfiguracji modułu CRM); w razie kolizji `Save()` zgłosi `RowException` z
`DuplicateKeyException` w `InnerException`.

**Snippet:**

```csharp
var crm = session.GetCRM();

using (var t = session.Logout(editMode: true))
{
    var k = new Kontrahent();
    crm.Kontrahenci.AddRow(k);                 // najpierw dodaj do tabeli, potem ustawiaj pola

    k.Kod = "FIRMA001";
    k.Nazwa = "Firma XYZ Sp. z o.o.";
    k.StatusPodmiotu = StatusPodmiotu.PodmiotGospodarczy;
    k.RodzajPodmiotu = RodzajPodmiotu.Krajowy;
    k.PodatnikVAT = true;
    k.NIP = "1234563218";                      // ustawienie NIP synchronizuje EuVAT

    t.Commit();                                // Commit() w kodzie biznesowym
}
session.Save();                                // zapis do bazy; tu wykryte konflikty/duplikaty
```

**Pułapki:**
- Tworzenie **wyłącznie w transakcji** (`session.Logout(editMode: true)`). `AddRow` przed
  ustawianiem pól.
- W workerze/extenderze (uruchamianym z UI) używaj `t.CommitUI()` zamiast `t.Commit()`
  (safe-code, [`worker-extender.md`](../worker-extender.md)).
- `Nazwa` jest zapisywalna; `NazwaFormatowana`/`NazwaPierwszaLinia` są kalkulowane — nie ustawiaj.
- Dla podmiotu unijnego ustaw `EuVAT` (z prefiksem kraju) — platforma sama dostosuje `RodzajPodmiotu`.
- Brak `Commit()` = automatyczny rollback przy `Dispose()`.

### W4 — Modyfikacja i statusy

**Cel:** zmienić dane istniejącego kontrahenta lub jego status dostępności/handlowy.

**Warianty:**

| Wariant | Pole / operacja |
|---|---|
| Edycja danych identyfikacyjnych | `Kod`, `Nazwa`, `NIP`, … (blokada optymistyczna) |
| Ukrycie na listach | `Blokada: bool` |
| Blokada sprzedaży | `BlokadaSprzedazy: bool` |
| Zmiana formy prawnej | `FormaPrawna` (poj. lub masowo: worker `ZmienFormePrawnaKontrahentowWorker`) |
| Zastąpienie (zamiennik) | `Zamiennik: Kontrahent` (ustawia automatycznie `Blokada=true`) |
| Kopiowanie kontrahenta | worker `Soneta.CRM.KopiujKontrahentaWorker` (akcja „Kopiuj kontrahenta...") |

**Pola i typy:** `Blokada: bool`, `BlokadaSprzedazy: bool`, `FormaPrawna: Soneta.CRM.FormaPrawna`,
`Zamiennik: Soneta.CRM.Kontrahent`.

**Snippet:**

```csharp
var crm = session.GetCRM();
var k = crm.Kontrahenci.WgKodu["FIRMA001"];
if (k == null) return;

using (var t = session.Logout(editMode: true))
{
    k.Nazwa = "Firma XYZ S.A.";
    k.BlokadaSprzedazy = true;                 // zakaz wystawiania dokumentów rozchodu
    k.Blokada = true;                          // ukrycie na listach
    t.Commit();
}
session.Save();

// Kopiowanie kontrahenta — programowe użycie workera (bez UI):
var kopiarka = new KopiujKontrahentaWorker { Kontrahent = k };
using (var t = session.Logout(editMode: true))
{
    Kontrahent nowy = kopiarka.Kopiuj();
    t.Commit();
}
session.Save();
```

**Pułapki:**
- **Blokada optymistyczna**: konflikt edycji (ktoś inny zapisał ten rekord) wybucha w `session.Save()`
  jako `RowConflictException` — obsłuż go (refresh + retry lub eskalacja), nie połykaj (safe-code §4).
- Nie nadpisuj `Kod` rekordów standardowych (`IsStandard == true`) ani incydentalnego
  (`JestIncydentalny == true`).
- `Zamiennik` ma efekt uboczny — ustawienie zamiennika włącza `Blokada=true`. Do rozwiązania
  „aktualnego" kontrahenta służy `Kontrahent.Coalesce(k)` (zwraca zamiennika albo sam rekord).
- Worker `KopiujKontrahentaWorker` ma property `[Context] Kontrahent` — przy ręcznym użyciu ustaw ją
  przed wywołaniem `Kopiuj()`; operacja musi być w transakcji.

### W5 — Bezpieczne usuwanie

**Cel:** usunąć kontrahenta albo świadomie odmówić usunięcia, gdy istnieją powiązania.

**Warianty:**

| Wariant | Sytuacja | Zalecenie |
|---|---|---|
| Usunięcie czyste | brak dokumentów/rozrachunków/zadań/zdarzeń | dozwolone (`DeleteRow`) |
| Usunięcie zablokowane | są dokumenty/rozrachunki/zapisy | zamiast usuwać → `Blokada=true` |
| Kontrahent systemowy | `IsStandard` / `JestIncydentalny` | nie usuwać |

**Pola i typy:** `DokumentyHandlowe`, `Rozrachunki`, `Zadania`, `Zdarzenia` (kolekcje `SubTable`),
`IsStandard: bool`, `JestIncydentalny: bool`, `Blokada: bool`.

**Snippet:**

```csharp
var crm = session.GetCRM();
var k = crm.Kontrahenci.WgKodu["FIRMA001"];
if (k == null) return;

if (k.IsStandard || k.JestIncydentalny)
    throw new BusException("Nie można usunąć kontrahenta systemowego.".Translate());

bool maPowiazania = !k.DokumentyHandlowe.IsEmpty || !k.Rozrachunki.IsEmpty
                    || !k.Zadania.IsEmpty || !k.Zdarzenia.IsEmpty;

using (var t = session.Logout(editMode: true))
{
    if (maPowiazania)
        k.Blokada = true;                      // miękkie wycofanie zamiast usunięcia
    else
        k.Delete();                            // twarde usunięcie tylko gdy brak powiązań
    t.Commit();
}
session.Save();
```

**Pułapki:**
- Sprawdź powiązania **przed** `DeleteRow()`. Próba usunięcia powiązanego rekordu i tak zostanie
  odrzucona przez integralność (wyjątek w `Save()`), ale lepiej zdecydować świadomie.
- Preferuj `Blokada=true` (kontrahent znika z list, dane pozostają) zamiast kasowania, gdy są
  powiązania historyczne.
- `IsEmpty`/`Any` na kolekcji `SubTable` to **właściwości** (test serwerowy `exists …`, bez
  nawiasów) — nie materializuj kolekcji do pamięci (`.ToList().Count`).

---

## 3. Adres, kontakt, osoby

### W6 — Adres

**Cel:** wprowadzić lub zaktualizować adres kontrahenta.

**Warianty:**

| Wariant | Pole |
|---|---|
| Adres główny | `Adres: Soneta.Core.Adres` |
| Adres do korespondencji | `AdresDoKorespondencji: Soneta.Core.Adres` |
| Telefon / faks na adresie | `Adres.Telefon`, `Adres.Faks` |
| Dane rozszerzone / nietypowa lokalizacja / GLN | `Adres.NietypowaLokalizacja`, `Adres.GLN` |

**Pola i typy (`Soneta.Core.Adres`):** `Ulica: string`, `NrDomu: string`, `NrLokalu: string`,
`KodPocztowy: int`, `KodPocztowyS: string` (sformatowany, np. `"31-000"`), `Poczta: string`,
`Miejscowosc: string`, `Gmina: string`, `Powiat: string`, `Wojewodztwo: Soneta.Core.Wojewodztwa`
(enum), `Kraj: string`, `KodKraju: string`, `GLN: string`, `Telefon: string`, `Faks: string`,
`ZagranicznyKodPocztowy: string`.

**Snippet:**

```csharp
var k = session.GetCRM().Kontrahenci.WgKodu["FIRMA001"];

using (var t = session.Logout(editMode: true))
{
    var a = k.Adres;                           // property zwraca obiekt adresu — edytujemy jego pola
    a.Ulica = "Wadowicka";
    a.NrDomu = "8A";
    a.NrLokalu = "2";
    a.KodPocztowyS = "30-415";                 // string z myślnikiem; pole int KodPocztowy = 30415
    a.Miejscowosc = "Kraków";
    a.Poczta = "Kraków";
    a.Wojewodztwo = Wojewodztwa.małopolskie;
    a.Kraj = "Polska";
    a.Telefon = "+48 12 345 67 89";

    // Adres do korespondencji (gdy różny od głównego)
    k.AdresDoKorespondencji.Ulica = "Skrytka pocztowa 15";
    t.Commit();
}
session.Save();
```

**Pułapki:**
- `Adres` to property **kalkulowana zwracająca obiekt złożony** — nie da się przypisać `k.Adres = …`;
  modyfikuj jego pola.
- `KodPocztowy` jest typu **`int`** (np. `30415`). Do wartości z myślnikiem używaj `KodPocztowyS`
  (string), które samo rozkłada/składa kod.
- `Wojewodztwo` to **enum** `Soneta.Core.Wojewodztwa`, nie string.
- `KodKraju` adresu bywa kalkulowane z `Kraj` — ustawiaj `Kraj`/`KodKraju` spójnie.

### W7 — Dane kontaktowe i adresy WWW

**Cel:** odczytać i zapisać kanały kontaktu (e-mail, telefon, faks, WWW) z oznaczeniem domyślnego.

**Warianty:**

| Wariant | Kolekcja / pole |
|---|---|
| Odczyt domyślnego e-maila/telefonu/WWW | `Kontakt.EMAIL`, `Kontakt.TelefonKomorkowy`, `Kontakt.WWW` |
| Dodanie kanału kontaktu | `Kontakty: SubTable<DaneKontaktowe>` (`Rodzaj`, `Kontakt`, `Domyslny`) |
| Adresy WWW | `AdresyWWW: SubTable<AdresWWW>` (`Adres`, `Domyslny`) |
| e-faktura | `EFaktura: Soneta.Core.EFaktura`, `EFakturaOkres: FromTo` |

**Pola i typy:** `Kontakt: Soneta.Core.Kontakt` (zsumowany „domyślny" kontakt — `EMAIL`,
`TelefonKomorkowy`, `WWW`, `SkrytkaPocztowa`, `Skype`). `DaneKontaktowe`: `Host: IDaneKontaktoweHost`,
`Rodzaj: RodzajKontaktu`, `Kontakt: string`, `Domyslny: bool`. `AdresWWW`: `Adres: string`,
`Domyslny: bool`.

**Snippet:**

```csharp
var k = session.GetCRM().Kontrahenci.WgKodu["FIRMA001"];

// Odczyt domyślnych kanałów (do automatyzacji wysyłek):
string email = k.Kontakt.EMAIL;
string tel   = k.Kontakt.TelefonKomorkowy;
string www   = k.Kontakt.WWW;

// Dodanie nowego kanału e-mail i oznaczenie go jako domyślny:
var rodzajEmail = session.GetCore().RodzajeKontaktow[RodzajeKontaktow.AdresEmail];
using (var t = session.Logout(editMode: true))
{
    var dk = new DaneKontaktowe { Host = k };  // Host = kontrahent (IDaneKontaktoweHost)
    session.AddRow(dk);
    dk.Rodzaj = rodzajEmail;
    dk.Kontakt = "kontakt@firma-xyz.pl";
    dk.Domyslny = true;

    // Dodanie adresu WWW:
    var strona = new AdresWWW(k);              // ctor przyjmuje IAdresyWWWHost
    session.AddRow(strona);
    strona.Adres = "https://www.firma-xyz.pl";
    strona.Domyslny = true;
    t.Commit();
}
session.Save();
```

**Pułapki:**
- `DaneKontaktowe.Rodzaj` to rekord słownika `RodzajKontaktu` — pobierz go po stałej Guid przez
  `session.GetCore().RodzajeKontaktow[RodzajeKontaktow.AdresEmail]` (analogicznie `TelefonKomórkowy`,
  `TelefonStacjonarny`, `Faks`, `Skype`).
- Tylko **jeden** kontakt domyślny w obrębie rodzaju — ustawienie `Domyslny=true` na nowym zwykle
  zdejmuje flagę z poprzedniego.
- `k.Kontakt.*` to **zagregowany** widok domyślnych kontaktów (do odczytu w automatyzacji). Pełna
  lista kanałów jest w kolekcji `k.Kontakty`.
- `AdresWWW` tworzymy konstruktorem z hostem (`new AdresWWW(k)`); pole adresu URL nazywa się `Adres`
  (nie `Url`).

### W8 — Osoby kontaktowe

**Cel:** zarządzać osobami kontaktowymi przypisanymi do kontrahenta.

**Warianty:**

| Wariant | Operacja |
|---|---|
| Odczyt listy | `Osoby: SubTable<KontaktOsoba>` (`Imie`, `Nazwisko`, `Stanowisko`, `EMAIL`, `Nieaktualny`) |
| Dodanie osoby | nowy `KontaktOsoba`, ustaw `Kontrahent` |
| Edycja osoby | zmiana pól |
| Oznaczenie nieaktualnej | flaga `Nieaktualny` (zamiast usuwania) |
| Dołącz / odłącz istniejącą | workery `DolaczOsobeKontrahentaWorker`, `RozlaczKontrahentaWorker` |

**Pola i typy (`KontaktOsoba`):** `Imie: string`, `Nazwisko: string`, `Stanowisko: string`,
`EMAIL: string`, `Nieaktualny: bool`, `Kontrahent: IKontrahent` (powiązanie).

**Snippet:**

```csharp
var k = session.GetCRM().Kontrahenci.WgKodu["FIRMA001"];

// Odczyt aktualnych osób:
foreach (KontaktOsoba os in k.Osoby[(KontaktOsoba o) => !o.Nieaktualny])
    Console.WriteLine($"{os.Imie} {os.Nazwisko} — {os.Stanowisko}");

// Dodanie osoby kontaktowej:
using (var t = session.Logout(editMode: true))
{
    var os = new KontaktOsoba();
    session.AddRow(os);
    os.Kontrahent = k;                         // powiązanie z kontrahentem
    os.Imie = "Anna";
    os.Nazwisko = "Nowak";
    os.Stanowisko = "Kierownik zakupów";
    os.EMAIL = "a.nowak@firma-xyz.pl";
    t.Commit();
}
session.Save();
```

**Pułapki:**
- Powiązanie osoby z kontrahentem ustawiamy przez `os.Kontrahent = k` (pod spodem powstaje rekord
  relacji w `OsobyKontaktowe`); osoba pojawia się wtedy w `k.Osoby`.
- **Nie usuwaj** osób, których dotyczyła historia kontaktu — oznaczaj `Nieaktualny=true`. Uwaga:
  ustawienie `Nieaktualny` ma efekty uboczne (kaskada na powiązania, integracja z kontem webowym) —
  rób to tylko w pełnej, zalogowanej sesji aplikacyjnej.
- Filtruj aktualne/nieaktualne serwerowo: `k.Osoby[(KontaktOsoba o) => !o.Nieaktualny]`.

---

## 4. Warunki handlowe i finanse

### W9 — Warunki płatności i limity kredytowe

**Cel:** ustawić warunki płatności i parametry kontroli kredytu kupieckiego.

**Warianty:**

| Wariant | Pola |
|---|---|
| Warunki płatności | `SposobZaplaty: FormaPlatnosci`, `Termin: int`, `TerminPlanowany: int`, `Waluta` |
| Limit kredytu | `TypLimituKredytowego`, `LimitKredytu: Currency` |
| Kontrola przeterminowania | `TypPrzeterminowania`, `KontrolaKwota: Currency`, `KontrolaDni: int` |
| Odczyt stanu (kalkulowane) | `LimitNieograniczony`, `PrzeterminowanieNieograniczone`, `KontrolaAktywna` |
| e-faktura | `EFaktura: Soneta.Core.EFaktura`, `EFakturaOkres: FromTo` |
| Odsetki / windykacja | `OdsKarne` (złożone), `NieWindykowac`, `DefinicjaSprawyWindykacyjnej` |
| Rachunki bankowe | `Rachunki: SubTable<RachunekBankowyPodmiotu>`, `DomyslnyRachunek` (kalkulowane) |

**Pola i typy:** `SposobZaplaty: Soneta.Kasa.FormaPlatnosci`, `Termin: int`,
`LimitKredytu: Soneta.Types.Currency`, `TypLimituKredytowego: Soneta.CRM.TypLimituKredytowego`
(`Kwota=0`, `Nieograniczony=1`), `KontrolaKwota: Currency`, `KontrolaDni: int`,
`TypPrzeterminowania: Soneta.CRM.TypLimituKredytowego`.

**Snippet:**

```csharp
var crm = session.GetCRM();
var k = crm.Kontrahenci.WgKodu["FIRMA001"];

using (var t = session.Logout(editMode: true))
{
    // Warunki płatności:
    k.SposobZaplaty = session.GetKasa().FormyPlatnosci[FormaPlatnosci.Przelew];
    k.Termin = 14;                             // dni

    // Limit kredytu kupieckiego:
    k.TypLimituKredytowego = TypLimituKredytowego.Kwota;
    k.LimitKredytu = new Currency(50000m, "PLN");   // kwota + symbol waluty

    // Kontrola przeterminowania:
    k.TypPrzeterminowania = TypLimituKredytowego.Kwota;
    k.KontrolaKwota = new Currency(5000m, "PLN");
    k.KontrolaDni = 7;
    t.Commit();
}
session.Save();

// Odczyt pól kalkulowanych (tylko do odczytu):
bool bezLimitu = k.LimitNieograniczony;
RachunekBankowyPodmiotu domyslny = k.DomyslnyRachunek;
```

**Pułapki:**
- Kwoty to **`Currency`** (kwota + waluta), nie `decimal`/`double` (safe-code §10). Twórz
  `new Currency(kwota, waluta)`.
- `LimitNieograniczony`, `PrzeterminowanieNieograniczone`, `KontrolaAktywna`, `DomyslnyRachunek` są
  **kalkulowane** — tylko do odczytu.
- `SposobZaplaty` to rekord `FormaPlatnosci` — pobierz go z `session.GetKasa().FormyPlatnosci[…]`
  (np. stała `FormaPlatnosci.Przelew`), nie ustawiaj „z palca".
- Ustawienie `TypLimituKredytowego = Nieograniczony` czyni `LimitKredytu` polem nieaktywnym (w UI
  read-only) — ustawiaj kwotę tylko dla typu `Kwota`.

### W10 — Konto księgowe / rozrachunkowe

**Cel:** odczytać/ustawić powiązanie kontrahenta z rozliczeniami (kontrahent jako `IPodmiotKasowy`).

**Warianty:**

| Wariant | Mechanizm |
|---|---|
| Kontrahent jako podmiot kasowy | rzutowanie/użycie przez interfejs `Soneta.Kasa.IPodmiotKasowy` |
| Domyślny płatnik | `Platnik: IPodmiotKasowy` (kalkulowane — nadrzędny z relacji lub sam podmiot) |
| Rachunki podmiotu | `Rachunki: SubTable<RachunekBankowyPodmiotu>` |

**Pola i typy:** `Platnik: Soneta.Kasa.IPodmiotKasowy` (kalkulowane), `Rachunki`,
`DomyslnyRachunek` (kalkulowane). `Kontrahent` implementuje `IPodmiotKasowy`.

**Snippet:**

```csharp
var k = session.GetCRM().Kontrahenci.WgKodu["FIRMA001"];

// Kontrahent jest podmiotem kasowym — można go podać tam, gdzie wymagany jest IPodmiotKasowy:
IPodmiotKasowy podmiot = k;

// Domyślny płatnik (gdy kontrahent jest podrzędny, zwraca nadrzędnego z relacji):
IPodmiotKasowy platnik = k.Platnik;
```

**Pułapki:**
- `Platnik` jest **kalkulowany** (zależny od relacji podmiotów, W14) — nie zapisuj go bezpośrednio.
- Konta księgowe rozrachunkowe należą do modułu księgowego; z poziomu kontrahenta operuj przez
  interfejs `IPodmiotKasowy` i kolekcje rozrachunków (W11), nie przez prywatne pola księgowe.

### W11 — Rozrachunki i płatności

**Cel:** odczytać należności/zobowiązania i ostatnie płatności kontrahenta.

**Warianty:**

| Wariant | Źródło |
|---|---|
| Należności i zobowiązania | `Rozrachunki: SubTable<Soneta.Kasa.RozrachunekIdx>` |
| Płatności / zapłaty | `Platnosci: SubTable<Platnosc>`, `Zaplaty: SubTable<Zaplata>` |
| Dokumenty rozliczeniowe | `DokumentyRozliczeniowe: SubTable<DokRozliczBase>` |
| Przelewy | `Przelewy: SubTable<PrzelewBase>` |

**Pola i typy:** wszystkie powyższe to kolekcje `SubTable` na `Kontrahent`. `RozrachunekIdx` ma
m.in. pola kwotowe i datę rozrachunku.

**Snippet:**

```csharp
var k = session.GetCRM().Kontrahenci.WgKodu["FIRMA001"];

// Rozrachunki nierozliczone — filtr serwerowy po kolekcji:
foreach (RozrachunekIdx r in k.Rozrachunki)
{
    // r.* — kwota, waluta, data, kierunek (należność/zobowiązanie)
}

// Ostatnie zapłaty (zawężaj zakresem czasu — to dane operacyjne!):
var od = Date.Today.AddMonths(-3);
foreach (Zaplata z in k.Zaplaty)
{
    // ...
}
```

**Pułapki:**
- Rozrachunki to dane **wyliczane/operacyjne** — przy szerszych analizach **zawężaj zakres czasowy**
  i nie ładuj całej historii (safe-code §6.3).
- Saldo/przeterminowanie na dany dzień to wynik wyliczeń — czytaj przez dedykowane pola/kolekcje,
  nie sumuj „ręcznie" całej tabeli.
- `RozrachunekIdx` / `Platnosc` / `Zaplata` żyją w module `Soneta.Kasa` — wymagana referencja do
  `Soneta.Kasa`.

---

## 5. Sprzedaż i dokumenty

### W12 — Dokumenty i dane sprzedażowe

**Cel:** odczytać dokumenty handlowe kontrahenta oraz (opcjonalnie) utworzyć dokument.

**Warianty:**

| Wariant | Źródło / worker |
|---|---|
| Dokumenty, w których kontrahent jest nabywcą | `DokumentyHandlowe: SubTable` |
| Dokumenty, w których jest odbiorcą | `DokumentyHandloweOdbiorcy: SubTable` |
| Dokumenty ewidencji | `DokumentyEwidencji: SubTable<DokEwidencji>` |
| Utworzenie dokumentu | przez moduł `Handel` (definicja dokumentu + ustawienie `Kontrahent`) |

**Pola i typy:** `DokumentyHandlowe`, `DokumentyHandloweOdbiorcy`, `DokumentyEwidencji` — kolekcje
`SubTable` na `Kontrahent`.

**Snippet:**

```csharp
var k = session.GetCRM().Kontrahenci.WgKodu["FIRMA001"];

// Ostatnie dokumenty handlowe kontrahenta jako nabywcy:
foreach (var d in k.DokumentyHandlowe)
{
    // d.* — numer, data, wartości
}
```

**Pułapki:**
- Tworzenie dokumentu handlowego realizuje moduł `Handel` (definicja `DefDokHandlowych`,
  `new DokumentHandlowy`, ustawienie `Kontrahent`) — to osobny obszar; z poziomu kontrahenta
  korzystaj z jego kolekcji do odczytu.
- `DokHandlowe` to tabela **operacyjna guided** — przy iteracji poprzecznej zawężaj zakres czasowy
  (safe-code §6.3). Kolekcja `k.DokumentyHandlowe` jest już zawężona do jednego kontrahenta.

---

## 6. Klasyfikacja

### W13 — Cechy, kategorie, branże, GUS

**Cel:** uzupełnić cechy definiowalne i klasyfikacje kontrahenta.

**Warianty:**

| Wariant | Kolekcja / mechanizm |
|---|---|
| Cecha definiowalna | `Features: FeatureCollection` (odczyt/zapis po nazwie definicji) |
| Kategorie | `Kategorie: SubTable<KategoriaKth>` (poj. lub worker `KontrahenciPrzypiszKategorieWorker`) |
| Branże | `Branze: SubTable<BranzaKth>` |
| PKD / dane GUS | worker `DaneZGusBirWorker` (online; pobiera też kody PKD) |

**Pola i typy:** `Features: Soneta.Business.FeatureCollection` (indeksator `Features["NazwaCechy"]`
zwraca/przyjmuje `object`), `Kategorie: SubTable<KategoriaKth>`, `Branze: SubTable<BranzaKth>`.
`KategoriaKth` tworzymy konstruktorem `new KategoriaKth(kontrahent, defKategorii)`.

**Snippet:**

```csharp
var crm = session.GetCRM();
var k = crm.Kontrahenci.WgKodu["FIRMA001"];

using (var t = session.Logout(editMode: true))
{
    // Cecha definiowalna — dostęp po nazwie definicji (cecha musi być wcześniej zdefiniowana):
    k.Features["Segment"] = "Premium";

    // Przypisanie do kategorii (defKat: DefKategKth z konfiguracji CRM, indeks WgNazwy):
    var defKat = crm.DefKategoriiKth.WgNazwy["VIP"];
    if (defKat != null && crm.KategorieKth.WgKontrahent[k, defKat] == null)
        crm.KategorieKth.AddRow(new KategoriaKth(k, defKat));
    t.Commit();
}
session.Save();

// Odczyt cechy:
object segment = k.Features["Segment"];
```

**Pułapki:**
- Cecha jest dostępna **po nazwie definicji**; odwołanie do niezdefiniowanej cechy rzuca wyjątek —
  upewnij się, że definicja istnieje (cechy vs pola natywne to dwie różne rzeczy).
- Przed dodaniem kategorii sprawdź duplikat: `crm.KategorieKth.WgKontrahent[k, defKat]`.
- Masowe przypisanie kategorii: worker `KontrahenciPrzypiszKategorieWorker` (`[Context] Kontrahent[]`
  + `Params.Kategoria`).
- Pobranie kodów PKD odbywa się **online** z GUS-BIR (worker `DaneZGusBirWorker`) — patrz W15.

---

## 7. Powiązania

### W14 — Powiązania i opiekunowie

**Cel:** zarządzać opiekunami i relacjami między kontrahentami.

**Warianty:**

| Wariant | Operacja / worker |
|---|---|
| Opiekun (dodanie / główny) | `Opiekunowie: SubTable<Opiekun>`, worker `UstawOpiekunaGlownegoWorker` |
| Sprawdzenie opieki na dzień | metody `JestOpiekunemNaDzis(...)`, `OpiekunowieWOkresie(...)` |
| Podmiot nadrzędny / podrzędny | workery `NowyPodmiotNadrzednyWorker`, `NowyPodmiotPodrzednyWorker` |
| Relacje podmiotów | `Podrzedni: SubTable<RelacjaPodmiotu>`, `PodmiotNadrzedny: IPodmiot` |
| Połącz / rozłącz | workery `PolaczKontrahentowWorker`, `RozlaczKontrahentaWorker` |

**Pola i typy (`Opiekun`):** `Kontrahent: Kontrahent`, `Operator: Operator`, `Typ: TypOpiekuna`
(`Glówny=0`, `Zastępca=1`), `Rola: RolaOpiekun`, `OddzialFirmy`, `DataOd: Date`, `DataDo: Date`,
`Aktywny: bool`.

**Snippet:**

```csharp
var crm = session.GetCRM();
var k = crm.Kontrahenci.WgKodu["FIRMA001"];

using (var t = session.Logout(editMode: true))
{
    var op = new Opiekun();
    crm.Opiekunowie.AddRow(op);
    op.Kontrahent = k;
    op.Operator = oper;                        // Operator pobrany z modułu Business
    op.Typ = TypOpiekuna.Glówny;
    op.DataOd = Date.Today;
    op.DataDo = Date.MaxValue;
    op.Aktywny = true;
    t.Commit();
}
session.Save();

// Odczyt relacji podmiotów:
foreach (RelacjaPodmiotu r in k.Podrzedni)
{
    // r.Nadrzedny, r.PowiazaniePodmiotu.Rola, r.PowiazaniePodmiotu.RodzajPowiazania
}
IPodmiot nadrzedny = k.PodmiotNadrzedny;
```

**Pułapki:**
- `Opiekun.Operator` to rekord operatora (dane konfiguracyjne) — w kodzie biznesowym pobieraj go
  spójnie z bieżącą sesją; nie mieszaj rekordów z różnych sesji (safe-code §2.1, użyj `session.Get(...)`).
- Do sprawdzania opieki „na dziś"/„w okresie" używaj metod publicznych `JestOpiekunemNaDzis`,
  `OpiekunowieWOkresie` zamiast ręcznego filtrowania dat.
- Relacje podmiotów (nadrzędny/podrzędny, płatnik/odbiorca) zakładaj workerami
  `NowyPodmiotNadrzednyWorker`/`NowyPodmiotPodrzednyWorker` — mają walidatory spójności.

---

## 8. Weryfikacja statusu

### W15 — Weryfikacja VAT (GUS / MF / VIES)

**Cel:** zweryfikować dane i status podatnika w rejestrach zewnętrznych. **Wszystkie operacje są
online** (wymagają połączenia i bywają limitowane).

**Warianty:**

| Wariant | Worker (jednostkowo / masowo) | Wynik na kontrahencie |
|---|---|---|
| Dane z GUS-BIR (też PKD) | `DaneZGusBirWorker` / `DaneZGusBirMultipleWorker` | nazwa, adres, REGON, KRS, PKD |
| Status MF / biała lista | `DaneZMfWorker`, `KontrahentBialaListaWorker` / `KontrahenciBialaListaWorker` | `AktualnyStatusVATMF` |
| Status VIES | `DataFromViesWorker` / `KontrahenciDaneZViesWorker` | `AktualnyStatusVATVies` |
| Historia statusu VAT | kolekcja `StatusyVAT: SubTable<StatusVAT>` | — |

**Pola i typy (odczyt wyniku):** `AktualnyStatusVAT`, `AktualnyStatusVATMF`, `AktualnyStatusVATVies`
(typ `Soneta.CRM.StatusNumeruVAT`, kalkulowane), `AktStatusVATData/DataMF/DataVIES: DateTime?`,
`StatusyVAT: SubTable<StatusVAT>`.

**Snippet:**

```csharp
var k = session.GetCRM().Kontrahenci.WgKodu["FIRMA001"];

// Odczyt ostatnio zapisanych statusów (offline — bez sieci):
StatusNumeruVAT statusMF   = k.AktualnyStatusVATMF;
StatusNumeruVAT statusVies = k.AktualnyStatusVATVies;
DateTime? dataMF           = k.AktStatusVATDataMF;

// Historia statusów:
foreach (StatusVAT s in k.StatusyVAT) { /* ... */ }

// Weryfikacja online — przez worker (przykład: status MF):
// var w = new DaneZMfWorker { Kontrahent = k, Context = context };
// w.DaneZMf();   // WYMAGA SIECI — obuduj obsługą braku połączenia/limitów
```

**Pułapki:**
- Operacje GUS/MF/VIES **wymagają sieci** — obuduj je obsługą błędów połączenia i limitów; **nie
  testuj ich w testach jednostkowych** (zależność od usług zewnętrznych).
- Status VAT z rejestru to dane „na dzień" — zapisuj datę weryfikacji (`AktStatusVATData*`).
- W kodzie offline czytaj wyłącznie pola kalkulowane (`AktualnyStatusVAT*`) i historię `StatusyVAT`.
- Nie loguj nadmiarowo numerów NIP/PESEL (safe-code §12).

---

## 9. RODO/GIODO i KSeF

### W16 — RODO / GIODO

**Cel:** obsłużyć zgody i wymianę danych osobowych kontrahenta.

**Warianty:**

| Wariant | Mechanizm / worker | Kolekcja |
|---|---|---|
| Oświadczenia | `KontrahentDodajOswiadczeniaWorker` | `GIODOOświadczenia: SubTable<GIODOOświadczenie>` |
| Pozyskanie danych | `KontrahentDodajPozyskanieDanychWorker` | `GIODOUdostępnienia` |
| Udostępnienie danych | `KontrahentDodajUdostepnienieDanychWorker` | `GIODOUdostępnienia` |
| Powierzenie danych | `KontrahentDodajPowierzenieDanychWorker` | `GIODOUdostępnienia` |
| Potwierdzenia zgodności | — | `PotwierdzeniaGIODO: SubTable<GIODOZgodny>` |

**Pola i typy:** `GIODOOświadczenia: SubTable<GIODOOświadczenie>`,
`GIODOUdostępnienia: SubTable<GIODOWymianaDanych>`, `PotwierdzeniaGIODO: SubTable<GIODOZgodny>`,
`ZgodnoscGIODOPotwierdzona: bool` (kalkulowane).

**Snippet:**

```csharp
var k = session.GetCRM().Kontrahenci.WgKodu["FIRMA001"];

// Odczyt oświadczeń RODO kontrahenta:
foreach (GIODOOświadczenie o in k.GIODOOświadczenia)
{
    // o.* — definicja oświadczenia, okres obowiązywania, status zgody
}

// Dodawanie oświadczeń realizują workery RODO (dziedziczą po bazowych z Soneta.Core):
// new KontrahentDodajOswiadczeniaWorker(...).DodajOświadczenia();
```

**Pułapki:**
- Obowiązywanie zgody jest „na dzień" — czytaj okresy z rekordów `GIODOOświadczenie`, nie zakładaj
  bezterminowości.
- Dane osobowe (PESEL, e-mail osób) są wrażliwe — nie loguj ich (safe-code §12).
- Workery RODO mają tryb `ConfirmSave` i wymagają praw do obszaru GIODO.

### W17 — KSeF

**Cel:** ustawić parametry KSeF kontrahenta.

**Warianty:**

| Wariant | Pole |
|---|---|
| Szablon pól opcjonalnych | `DomyslnySzablonPolOpcjonalnychKSeF: Soneta.Core.KSeFSzablonPolOpcjonalnych` |
| Sposób wysyłki ceny | `KSeFSposobObslugiWysylkiCeny: Soneta.Core.SposobObslugiWysylkiCenyDoKSeF` |
| Powiązanie z e-fakturą | `EFaktura`, `EFakturaOkres` (patrz W7) |

**Pola i typy:** jak w tabeli powyżej (oba pola bazodanowe, zapisywalne).

**Snippet:**

```csharp
var k = session.GetCRM().Kontrahenci.WgKodu["FIRMA001"];

using (var t = session.Logout(editMode: true))
{
    k.KSeFSposobObslugiWysylkiCeny = SposobObslugiWysylkiCenyDoKSeF.CenaPoRabacie;
    // k.DomyslnySzablonPolOpcjonalnychKSeF = ... // rekord szablonu z konfiguracji
    t.Commit();
}
session.Save();
```

**Pułapki:**
- `DomyslnySzablonPolOpcjonalnychKSeF` to referencja do rekordu konfiguracyjnego — pobierz istniejący
  szablon, nie twórz „w locie".
- Konfiguracja KSeF współgra z `EFaktura` (W7) — ustawiaj je spójnie.

---

## 10. Operacje masowe

### W18 — Operacje na zbiorze kontrahentów

**Cel:** wykonać operację na wielu kontrahentach efektywnie i bezpiecznie.

**Warianty:**

| Wariant | Mechanizm |
|---|---|
| Iteracja z warunkiem | serwerowy LINQ `crm.Kontrahenci[(Kontrahent k) => …]` (patrz [`rowcondition.md`](../rowcondition.md)) |
| Masowa aktualizacja | jedna transakcja, paczki (patrz [`safe-code.md`](../safe-code.md)) |
| Masowa zmiana formy prawnej | worker `ZmienFormePrawnaKontrahentowWorker` |
| Masowe przypisanie kategorii | worker `KontrahenciPrzypiszKategorieWorker` |
| Masowa weryfikacja VAT/VIES (online) | `KontrahenciBialaListaWorker`, `KontrahenciDaneZMfWorker`, `KontrahenciDaneZViesWorker` |
| Eksport / import | datapack / business.xml (patrz [`datapack-guidedrow.md`](../datapack-guidedrow.md)) |

**Snippet:**

```csharp
var crm = session.GetCRM();

// Masowa zmiana: ustaw blokadę sprzedaży dla kontrahentów bez NIP — filtr serwerowy + 1 transakcja
using (var t = session.Logout(editMode: true))
{
    foreach (Kontrahent k in crm.Kontrahenci.WgKodu[(Kontrahent k) =>
                 k.NIP == null && k.StatusPodmiotu == StatusPodmiotu.PodmiotGospodarczy])
    {
        k.BlokadaSprzedazy = true;
    }
    t.Commit();
}
session.Save();
```

**Pułapki:**
- **Nie ładuj całej tabeli** do pamięci — filtr serwerowy (`SubTable[condition]`).
- Duże operacje dziel na **paczki** (krótkie transakcje), by nie blokować innych użytkowników i nie
  zwiększać ryzyka konfliktu optymistycznego (safe-code §13.1).
- Workery masowe (`*Worker` na typie `Kontrahenci`) mają property `[Context] Kontrahent[]` —
  przy użyciu programowym ustaw tablicę zaznaczonych rekordów.

---

## Powiązane dokumenty

- [`safe-code.md`](../safe-code.md) — sesja, transakcje, blokada optymistyczna, zasady bezpiecznego kodu.
- [`session-login.md`](../session-login.md) — `Session`, `Login`, `Database`.
- [`worker-extender.md`](../worker-extender.md) — workery, akcje menu Czynności, bindowanie.
- [`rowcondition.md`](../rowcondition.md) — serwerowy LINQ, `RowCondition`, `SubTable[condition]`.
- [`datapack-guidedrow.md`](../datapack-guidedrow.md) — eksport/import, `GuidedRow`.
- [`scan-props.md`](../scan-props.md) / [`scan-workers.md`](../scan-workers.md) — inwentaryzacja pól i workerów.
