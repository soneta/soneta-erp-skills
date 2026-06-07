# KADRY10 — Deklaracje (ZUS, PIT, PFRON, PPK)

> Wspólne fakty o typie, podstawowe typy i szablon wzorca: [../kadry.md](../kadry.md).

> **Moduł.** `Soneta.Deklaracje.DeklaracjeModule` — dostęp z sesji przez `session.GetDeklaracje()`.
> Wszystkie deklaracje (ZUS, PIT, PFRON, PPK) to wiersze tabeli `Deklaracje`, dziedziczące po
> abstrakcyjnej klasie root `Soneta.Deklaracje.Deklaracja` (`GuidedRow`, implementuje m.in.
> `IDeklaracja`, `IDokumentPlatny`, `IDokumentKsiegowalny`). Konkretne typy żyją w podprzestrzeniach:
> `Soneta.Deklaracje.ZUS.*`, `Soneta.Deklaracje.PIT.*`, `Soneta.Deklaracje.PFRON.*`,
> `Soneta.Deklaracje.PPK.*`.
>
> **Rozróżnienie kluczowe dla testów — NALICZENIE/UTWORZENIE vs E-WYSYŁKA.**
> - **Naliczenie/utworzenie deklaracji** (workery `*Worker` z akcjami „Przygotuj…/Nalicz…/Przelicz”,
>   operacje PPK) tworzy **wiersze w bazie** — to operacja lokalna, w zasadzie testowalna na Demo,
>   ale **wymaga `Context`** (i dla ZUS zwykle obiektu `KEDU`). Workery nie mają konstruktorów
>   bezparametrowych dających pełny kontrakt — `Params` budujemy z `Context`/`Session`.
> - **E-wysyłka** to osobne typy: `EDeklaracja` (tabela `EDeklaracje` — XML, podpis, UPO) oraz
>   `ETransmisja` (tabela `ETransmisje` — pojedyncze transmisje do bramki). Eksport KEDU/PUE realizują
>   workery `Soneta.Deklaracje.UI.KeduEksportForm.EksportWorker` (akcje „Eksport KEDU”, „Pobierz KEDU”)
>   i `Soneta.Deklaracje.UI.PUEEksportForm.EksportWorker` (akcja „Eksport PUE (RUD)”), a uruchomienie
>   Programu Płatnika — `Soneta.Deklaracje.ZUS.DeklaracjaZUS.UruchomPPWorker` (akcja
>   „Uruchom 'Program Płatnika'”). **To operacje sieciowe/plikowe/zewnętrzne — NIE do testu** (nawet
>   utworzenie `EDeklaracja` wymaga podpisu i bramki ZUS/US).
>
> **`KEDU` (`Soneta.Deklaracje.ZUS.KEDU`)** — „zestaw deklaracji”: kontener (komplet dokumentów ZUS),
> do którego workery zgłoszeniowe i rozliczeniowe dopinają wygenerowane bloki. Praktycznie każdy worker
> ZUS przyjmuje `Kedu` w swoich `Params`; bez przekazanego `KEDU` generowanie deklaracji ZUS nie ma
> gdzie zapisać wyniku. KEDU nie jest tworzony „w locie” w sposób trywialny — jest częścią mechanizmu
> deklaracji rozliczeniowych ZUS i jego zbudowanie wymaga środowiska modułu Deklaracje (`Context`).

---

### KADRY-J1 — Zgłoszenia ZUS (ZUA/ZZA, ZCNA, ZWUA)

**Cel:** zgłosić/wyrejestrować pracownika i jego umowy w ZUS oraz zgłosić członków rodziny do
ubezpieczenia zdrowotnego. Typy zgłoszeń to wiersze deklaracji: `ZUA` (społeczne + zdrowotne),
`ZZA` (tylko zdrowotne), `ZCNA` (rodzina), `ZWUA` (wyrejestrowanie), `ZIUA` (zmiana danych
identyfikacyjnych), `ZCZA` (zmiana danych członka rodziny) — wszystkie w `Soneta.Deklaracje.ZUS`.

**Workery — poziom `Pracownicy` (klasy zagnieżdżone `Soneta.Deklaracje.ZUS.ZarejestrujPracownikówWorker`):**

| Worker (akcja) | `Params` (typ) | Pola `Params` | Metoda akcji |
|---|---|---|---|
| `ZarejestrujPracownikówWorker.Rejestracja` — „Deklaracje ZUS/Przygotuj ZUA i ZZA” | `ZarejestrujBaseWorker.ParamsKor` | `Okres: FromTo`, `DataDokumentu`/`DataWypełnienia: Date`, `Kedu: KEDU`, `KorektaZmiana: ZgloszenieZUS.KorektaZmiana`, `ZarejestrujRodzinę: bool` | `object ZarejestrujPracowników()` |
| `ZarejestrujPracownikówWorker.Rodzina` — „Deklaracje ZUS/Przygotuj ZCNA” | `ZarejestrujBaseWorker.Params` | `Okres`, `DataDokumentu`, `DataWypełnienia`, `Kedu` | `object ZarejestrujRodzinę()` |
| `ZarejestrujPracownikówWorker.Wyrejestrowanie` — „Deklaracje ZUS/Przygotuj ZWUA” | `Wyrejestrowanie.ParamsWR` | `Okres`, `DataDokumentu`, `DataWypełnienia`, `Kedu`, `RIA: bool`, `WyrejestrujRodzinę: bool` | `object WyrejestrujPracowników()` |
| `ZarejestrujPracownikówWorker.ZgloszenieUmow` — „Deklaracje ZUS/Przygotuj RUD” | `ZgloszenieUmow.UParams` | `Okres`, `DataWypełnienia`, `Kedu`, `Trwajace: bool` | `object ZgłośUmowy()` |

> Worker przyjmuje zaznaczone osoby przez `Pracownicy: Pracownik[]` (`[Context]`). Wszystkie `Params`
> mają ctor `(Context)`. Po akcji wynik (lista wygenerowanych deklaracji) odczytasz z bazowego
> `Deklaracje: View`, a `Save()` zatwierdza.

**Workery — poziom `Umowy` (zleceniobiorcy), `Soneta.Deklaracje.ZUS.ZarejestrujUmowyWorker`** —
opisane w **KADRY-G5** (`Rejestracja.ZarejestrujUmowy()` → ZUA/ZZA wg schematu `UmowaHistoria.Ubezpieczenia`,
`Wyrejestrowanie.WyrejestrujUmowy()` → ZWUA). `ParamsZ`/`ParamsW` mają ctor `(Context)`; pola
bazowe `Okres`/`DataDokumentu`/`DataWypełnienia`/`Kedu` + `ZarejestrujRodzinę`/`WyrejestrujRodzinę`.

**ZCNA na rodzinie (KADRY-A9).** Zgłoszenie członka rodziny do ubezpieczenia zdrowotnego startuje z danych
`CzlonekRodziny` (`Ubezpieczony = true`, `UbezpieczenieOkres`, `StPokrewienstwa` — patrz KADRY-A9), a samą
deklarację ZCNA generuje `ZarejestrujPracownikówWorker.Rodzina` (lub `Rejestracja` z
`Pars.ZarejestrujRodzinę = true`). Dla zleceniobiorcy analogicznie przez `ZarejestrujUmowyWorker`.

**Przerejestrowanie (KADRY-A19).** `Soneta.Deklaracje.UI.PrzerejestrowaniePracownikaWorker` (DataType
`PracHistoria`) oraz `Soneta.Deklaracje.UI.PrzerejestrowanieZleceniobiorcyWorker` (DataType
`UmowaHistoria`) — generują ZWUA+ZUA przy zmianie tytułu/wydziału. `Params` wymaga `KEDU` + `Context`.

**Snippet (przygotowanie ZUA/ZZA dla zaznaczonych pracowników):**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];

var pars = new Soneta.Deklaracje.ZUS.ZarejestrujBaseWorker.ParamsKor(context)
{
    Okres           = new FromTo(new Date(2026, 1, 1), Date.MaxValue),
    DataDokumentu   = new Date(2026, 1, 1),
    DataWypełnienia = Date.Today,
    Kedu            = kedu,              // KEDU z modułu Deklaracje (Context)
    ZarejestrujRodzinę = false,
};
var rejestracja = new Soneta.Deklaracje.ZUS.ZarejestrujPracownikówWorker.Rejestracja
{
    Pracownicy = new[] { pracownik },
    Pars       = pars,
};
rejestracja.ZarejestrujPracowników();   // tworzy ZUA/ZZA (i ZCNA, gdy ZarejestrujRodzinę)
session.Save();
```

**Pułapki:**
- Typ zgłoszenia (ZUA vs ZZA) wynika ze schematu ubezpieczeń (`Etat.Ubezpieczenia` / `UmowaHistoria.Ubezpieczenia`,
  KADRY-A7/KADRY-G5) — nie z parametru workera. Ustaw `Tyub4` i flagi `Spoleczne`/`Zdrowotne` przed zgłoszeniem.
- Każdy `Params` wymaga `Context` (ctor `(Context)`) i pola `Kedu` — bez `KEDU` deklaracja nie ma
  kontenera docelowego. Operacja jest **lokalna** (zapis wiersza), ale niewykonalna bez `Context`/`KEDU`.
- `ZWUA` z `RIA = true` powiązany jest z mechanizmem RIA (KADRY-J2).
- Workery zgłoszeniowe na `Pracownicy` dotyczą etatowych; na `Umowy` — zleceniobiorców (KADRY-G5).

---

### KADRY-J2 — Deklaracje rozliczeniowe ZUS (DRA, RIA, IMIR, RUD, IWA; KEDU)

**Cel:** naliczyć/utworzyć deklaracje rozliczeniowe i informacyjne ZUS. Typy (`Soneta.Deklaracje.ZUS`,
wiersze tabeli `Deklaracje`): `DRA` (deklaracja rozliczeniowa z załącznikami RCA/RSA/RZA; ctor `(KEDU)`),
`RIA` (informacja roczna / raport po ustaniu zatrudnienia; ctor `(Pracownik, KEDU)`), `RMUA` —
informacja miesięczna dla ubezpieczonego, potocznie **IMIR** (ctor `(Pracownik, RMUA.TypOkresuDeklaracji)`;
**brak osobnego typu `IMIR` w CLR — to `RMUA`**), `RUD` (zgłoszenie umowy o dzieło), `IWA` (informacja o wypadkach/składce wypadkowej),
`OSW` (oświadczenie), `Z3`/`Z3a` (zaświadczenia płatnika ERP-7 — patrz niżej), `KEDU` (zestaw).

**Naliczanie seryjne — poziom `Pracownicy`:**

| Worker (akcja) | `Params` (typ) | Pola `Params` | Metoda |
|---|---|---|---|
| `Soneta.Deklaracje.ZUS.NaliczanieSeryjneRIAWorker` — „Deklaracje ZUS/Przygotuj RIA” | `…RIAWorker.Params` | `DataDokumentu`/`DataWypełnienia: Date`, `Kedu: KEDU`, `Wydział: Wydzial`, `Wszystkie: bool`, `Zerowa: bool` | `object NaliczRMUA(Context)` |
| `Soneta.Deklaracje.ZUS.NaliczanieSeryjneRMUAWorker` — „Deklaracje ZUS/Przygotuj IMIR” | `…RMUAWorker.Params` | `DataDokumentu`/`DataWypełnienia: Date`, `Miesiac: YearMonth`, `Rok: int`, `TypOkresu: RMUA.TypOkresuDeklaracji`, `Oskladkowani: bool`, `Wydział`, `Wszystkie` | `object NaliczRMUA(Context)` |

> Oba workery mają **ctor bezparametrowy**, przyjmują `Pracownicy: Pracownik[]` (`[Context]`) i mają w props
> `Context`, `Kedu`, `Deklaracje: View`. Metoda akcji `NaliczRMUA(Context)` (ta sama nazwa dla RIA i RMUA).
> `Params` są property `Pars` (setter); na workerze `RMUAWorker` pola `Params` są też wystawione bezpośrednio jako property.

**Przeliczenie pojedynczej deklaracji — `Soneta.Deklaracje.DeklaracjaWorker`** (DataType `Deklaracja`,
więc działa dla **dowolnej** deklaracji ZUS/PIT/PFRON): akcja **„Przelicz”** → `void Przelicz()`;
parametr `Deklaracja: Soneta.Deklaracje.Deklaracja` (`[Context]`).

**RUD** generuje `ZarejestrujPracownikówWorker.ZgloszenieUmow` (KADRY-J1) lub jest dostępna na liście umów.
**DRA z załącznikami** to root `DeklaracjaZUS`; nalicza się przez mechanizm KEDU + `Przelicz`.

**E-wysyłka (NIE testować):** eksport KEDU — `KeduEksportForm.EksportWorker` („Eksport KEDU”,
„Pobierz KEDU”); eksport PUE/RUD — `PUEEksportForm.EksportWorker` („Eksport PUE (RUD)”); Program
Płatnika — `DeklaracjaZUS.UruchomPPWorker`.

**Pułapki:**
- `KEDU` jest osią całego rozliczenia ZUS — wszystkie workery rozliczeniowe wpisują wynik do
  przekazanego `Kedu`. Bez modułu Deklaracje (`Context`) i `KEDU` operacji nie złożysz.
- `DeklaracjaWorker.Przelicz()` przelicza **istniejący** wiersz deklaracji — najpierw musi powstać
  (np. z naliczania seryjnego), więc to nie jest „utworzenie od zera”.

---

### KADRY-J3 — Deklaracje PIT (PIT-11, PIT-4R, PIT-8AR, PIT-R, IFT-1/IFT-1R, PIT-8C)

**Cel:** naliczyć imienne i zbiorcze deklaracje podatkowe. Typy (`Soneta.Deklaracje.PIT`, wiersze
tabeli `Deklaracje`): `PIT11`, `PIT4`/PIT-4R (rozliczeniowa zaliczek), `PIT8A`/PIT-8AR (zryczałtowany),
`PITR` (PIT-R), `IFT1`/`IFT1R`, `PIT8C`, `PIT40`, plus `ZbiorczaPIT`/`IEDeklaracjaZbiorczaItem`
(deklaracje zbiorcze).

**Naliczanie seryjne — poziom `Pracownicy` (klasy zagnieżdżone `Soneta.Deklaracje.PIT.NaliczanieSeryjne`):**

| Worker (akcja) | Ctor | `Params` — pola | Metoda |
|---|---|---|---|
| `NaliczanieSeryjne.PIT_11Worker` — „Deklaracje PIT/Nalicz PIT 11” | `(Session session)` | `Okres: FromTo`, `Data: Date`, `Naliczaj: NaliczanieDeklaracje`, `BezPotwierdzenia: bool`, dane podpisującego (`Imię`/`Nazwisko`/`Stanowisko` + `…Odp`), `TreśćUzasadnienia: string` | `object Nalicz_PIT_11()` |
| `NaliczanieSeryjne.PIT_RWorker` — „Deklaracje PIT/Nalicz PIT R” | `(Session)` | jw. (`Params`) | `Nalicz…()` |
| `NaliczanieSeryjne.PIT_8CWorker` — „Deklaracje PIT/Nalicz PIT 8C” | `(Session)` | jw. | `Nalicz…()` |
| `NaliczanieSeryjne.IFT_1Worker` / `IFT_1RWorker` — „Deklaracje PIT/Nalicz IFT-1 / IFT-1R” | `(Session)` | jw. | `Nalicz…()` |

> `Params` mają ctor `(Context)`; worker `PIT_11Worker` dodatkowo ma ctor `(Session)`. Zaznaczeni
> pracownicy przez `[Context]`.

**Deklaracje płatnika (PIT-4R/PIT-8AR)** są zbiorcze na poziomie podmiotu/oddziału (`PIT4`/`PIT8A`,
`ZbiorczaPIT`) — tworzone/dodawane workerami zbiorczymi (`DodajDoZbiorczejPITWorker`,
`WybierzDeklaracjeDoZbiorczejPITWorker`) i przeliczane `DeklaracjaWorker.Przelicz()` (KADRY-J2) lub
dedykowanymi `…PrzeliczWorker` (np. `PITR.PrzeliczWorker`, `PIT8S.PrzeliczWorker`).

**Snippet (naliczenie PIT-11 dla zaznaczonych pracowników):**

```csharp
var pracownicy = new[] { session.GetKadry().Pracownicy.WgKodu["006"] };

var worker = new Soneta.Deklaracje.PIT.NaliczanieSeryjne.PIT_11Worker(session)
{
    Pracownicy = pracownicy,
};
worker.Pars.Okres = FromTo.Year(2025);     // rok podatkowy
worker.Pars.Data  = Date.Today;
worker.Nalicz_PIT_11();                     // tworzy wiersze PIT11 w tabeli Deklaracje
session.Save();
```

**Pułapki:**
- Naliczenie PIT bazuje na naliczonych wypłatach (H) i bilansach otwarcia PIT (KADRY-J6) — bez danych
  źródłowych deklaracja będzie zerowa.
- Sygnatury `Params` PIT mają ctor `(Context)`; `PIT_11Worker` ma też ctor `(Session)` — w teście
  użyj `(session)` + ustaw `Pracownicy`/`Pars`.
- **E-wysyłka PIT to `EDeklaracja`/`ETransmisja` (bramka MF) — NIE testować.** Samo naliczenie
  wiersza PIT jest lokalne (zapis do bazy).

---

### KADRY-J4 — Deklaracje PFRON (Wn-D, INF-2, DEK-R, INF-D-P)

**Cel:** utworzyć/naliczyć deklaracje PFRON. Typy (`Soneta.Deklaracje.PFRON`, wiersze tabeli
`Deklaracje`): `WN_D` (Wn-D — wniosek o dofinansowanie), `WN_U` (Wn-U), `INF_D`/`INF_D_P`
(informacje o pracownikach niepełnosprawnych — załączniki do Wn-D), `INF_2` (informacja roczna),
`DEK_R` (deklaracja roczna wpłat).

**Workery:**
- `Soneta.Deklaracje.DeklaracjaWorker` — akcja **„Przelicz”** (`Przelicz()`) dla każdego z typów PFRON
  (są DataType `Deklaracja`).
- `Soneta.Deklaracje.PFRON.INF_D.InfoWorker`, `…INF_D_P.InfoWorker` — properties informacyjne (UI).
- **E-wysyłka SOD (NIE testować):** `Soneta.Deklaracje.UI.SODEksportForm.EksportWorker` (DataType
  `WN_D`/`WN_U`/`INF_D`) — eksport do systemu SODiR.

**Dane źródłowe** PFRON pochodzą z `PracHistoria.PFRON` (KADRY-A13: stopień niepełnosprawności, efekt
zachęty, schorzenia SOD) — bez nich deklaracja będzie pusta.

**Pułapki:**
- PFRON nie ma dedykowanego „NaliczanieSeryjne” na `Pracownicy` — deklarację (`WN_D` itd.) tworzy się
  w module Deklaracje, a przelicza `DeklaracjaWorker.Przelicz()`. Tworzenie/edycja wymaga `Context`.
- Konfiguracja procentów/odpisu PFRON to workery na `OddzialFirmy`
  (`Soneta.Deklaracje.Config.*PFRON*Worker`) — to dane konfiguracyjne, nie deklaracje.

---

### KADRY-J5 — Operacje PPK

**Cel:** obsłużyć cykl życia uczestnictwa w PPK — kwalifikacja/auto-zapis, rejestracja uczestnika,
rezygnacja, wznowienie, zmiana danych, zakończenie zatrudnienia, dokumenty i rozliczenie składek.
Typy dokumentów PPK (`Soneta.Deklaracje.PPK`, wiersze tabeli `Deklaracje`): `RejestracjaUczestnikaPPK`,
`DeklaracjaUczestnikaPPK`, `ZmianaDanychIdentyfikacyjnychUczestnikaPPK`,
`ZmianaDanychKontaktowychUczestnikaPPK`, `ZakończenieZatrudnieniaUczestnikaPPK`, `TransferPPK`,
`WypłataTransferowaPPK`, `WypłataŚrodkówPrzezUczestnikaPPK`, `ZwrotŚrodkówPPK`, `RozliczenieSkładekPPK`,
`RozliczenieNadpłatPPK`, `ZwrotNadpłatyPPK`, `NadanieUczestnikowiNumeruPPK`,
`DokumentyPracodawcyPPK`, `DokumentyInstytucjiFinansowejPPK`.

**Workery operacji PPK — poziom `Pracownicy` (zagnieżdżone `Soneta.Deklaracje.PPK.DeklaracjePPKPracownikówWorker`),
wspólny `Params = DeklaracjePPKBaseWorker.Params` (`Okres: FromTo`, `DokumentPPK: DokumentyPracodawcyPPK`):**

| Worker (akcja) | Metoda |
|---|---|
| `…Worker.Rejestracja` — „Operacje PPK/Rejestracja uczestnika” | `object RejestracjaPracownikow()` |
| `…Worker.Rezygnacja` — „Operacje PPK/Rezygnacja uczestnika” | `object RezygnacjaPracownikow()` |
| `…Worker.Wznowienie` — „Operacje PPK/Automatyczne wznowienie uczestnictwa” | `object WznowieniePracownikow()` |
| `…Worker.ZakończenieZatrudnienia` — „Operacje PPK/Zakończenie zatrudnienia uczestnika” | `object ZakończenieZatrudnieniaPracownikow()` |
| `…Worker.ZmianaDanychIdentyfikacyjnych` — „Operacje PPK/Zmiana danych identyfikacyjnych” | `object ZmianaDanychIdentyfikacyjnychPracownikow()` |

> Przystąpienie/auto-zapis i zmiana procentu składki realizowane są na poziomie **pracownika**
> (dane PPK pracownika), nie tymi workerami zbiorczymi.

**Workery na pracowniku (kwalifikacja PPK) — `Soneta.Kadry.Pracownik`:**

| Worker | Ctor | Wybrane pola/props |
|---|---|---|
| `Pracownik.PPKWorker` (alias `PPK`) | `(Context context)` | `Data: Date`, `Idx: Pracownik`; `Kwalifikacja: PPKWorker.RodzajZgłoszenia`, `DataKwalifikacji[/Min/Max]: Date`, `Kwalifikacja[Min/Max]` |
| `Pracownik.AutoZapisPPKWorker` (alias `AutoZapisPPK`) | `(Context context)` | `Data: Date`, `Pracownik: Pracownik`; `Kwalifikacja: AutoZapisPPKWorker.CzyAutoZapisPPK` |

> Te workery służą do **odczytu kwalifikacji** (czy/kiedy pracownik podlega przystąpieniu lub
> auto-zapisowi do PPK na dany dzień) — mają ctor `(Context)`.

**Przeliczanie/rozliczenie PPK:**
- `Soneta.Deklaracje.PPK.PrzeliczPPKWorker` (DataType m.in. `RozliczenieNadpłatPPK`,
  `WypłataTransferowaPPK`, `WypłataŚrodkówPrzezUczestnikaPPK`, `ZwrotŚrodkówPPK`,
  `NadanieUczestnikowiNumeruPPK`) — przelicza dokument rozliczeniowy PPK.
- `Soneta.Deklaracje.PPK.NadanieNumeruPPKWorker` (DataType `NadanieUczestnikowiNumeruPPK`).
- `RozliczenieSkładekPPK` / `RejestracjaUczestnikaPPK` / `DeklaracjaUczestnikaPPK` przeliczane przez
  `DeklaracjaWorker.Przelicz()` (KADRY-J2, DataType `Deklaracja`).

**E-wysyłka / import-eksport PPK (NIE testować):**
- `Soneta.Deklaracje.PPK.DokumentyPPKEksportWorker` (DataType `DokumentyPracodawcyPPK`,
  `DokumentyInstytucjiFinansowejPPK`) — eksport do instytucji finansowej.
- `Soneta.Deklaracje.PPK.DokumentyPPKImportWorker` (DataType `DokumentyInstytucjiFinansowejPPK`) —
  import zwrotny.

**Snippet (rejestracja uczestnika PPK dla zaznaczonych):**

```csharp
var pracownicy = new[] { session.GetKadry().Pracownicy.WgKodu["006"] };

var pars = new Soneta.Deklaracje.PPK.DeklaracjePPKBaseWorker.Params(context)
{
    Okres = FromTo.Year(2026),
    // DokumentPPK = … (DokumentyPracodawcyPPK z modułu Deklaracje)
};
var rej = new Soneta.Deklaracje.PPK.DeklaracjePPKPracownikówWorker.Rejestracja
{
    Pracownicy = pracownicy,
    Pars       = pars,
};
rej.RejestracjaPracownikow();   // tworzy dokumenty rejestracji uczestnika PPK
session.Save();
```

**Pułapki:**
- Zmiana procentu składki PPK / przystąpienie to dane **pracownika** (deklaracja uczestnika PPK,
  `DeklaracjaUczestnikaPPK`) — workery zbiorcze obejmują rejestrację, rezygnację, wznowienie, zmianę
  danych identyfikacyjnych i zakończenie zatrudnienia.
- `DeklaracjePPKBaseWorker.Params` ma ctor `(Context)`; operacja jest lokalna (tworzy wiersze
  dokumentów PPK), ale niewykonalna bez `Context` i zwykle `DokumentPPK`.
- `PPKWorker`/`AutoZapisPPKWorker` na pracowniku są **diagnostyczne** (kwalifikacja na dzień), nie
  tworzą dokumentów — i wymagają `Context`.

---

### KADRY-J6 — Bilanse otwarcia deklaracji (PIT, ZUS, ERP-7) przy wdrożeniu

**Cel:** wprowadzić dane historyczne sprzed startu systemu, potrzebne do poprawnego naliczenia
deklaracji w pierwszym okresie. Bilanse są **kolekcjami na pracowniku** (`SubTable`) — tworzy się je
i odczytuje czystym API biznesowym, **bez `Context`/`KEDU`/sieci**.

**Kolekcje na `Soneta.Kadry.Pracownik`:**

| Kolekcja | Typ | Przeznaczenie |
|---|---|---|
| `Pracownik.BilansyOtwarciaPIT` | `SubTable<Soneta.Place.BilansOtwarciaPIT>` | bilans otwarcia PIT (przychody/koszty/składki na start) |
| `Pracownik.WynagrodzeniaERP7` | `SubTable<Soneta.Kalend.WynagrodzenieERP7>` | wynagrodzenia do ERP-7 / Z-3 |
| `Pracownik.NieobecnosciERP7` | `SubTable<Soneta.Kalend.NieobecnoscERP7>` | nieobecności do ERP-7 / Z-3 |
| `Pracownik.DeklaracjePodmiotu` | `SubTable` | deklaracje powiązane z pracownikiem-podmiotem |

**Typ `Soneta.Place.BilansOtwarciaPIT`** (root `GuidedRow`, tabela `BilansyOtwPIT`) jest
**ABSTRAKCYJNY** — instancjonuje się jedną z konkretnych wersji odpowiadających wartościom enuma
`Soneta.Place.WersjaBilansuOtwarciaPIT` (`PIT11_11`, `PIT11_29`):
`Soneta.Place.BilansOtwarciaPIT_11` (Wersja = `PIT11_11`) lub `Soneta.Place.BilansOtwarciaPIT_29`
(Wersja = `PIT11_29`). Konkretne klasy mają publiczny ctor `(Pracownik pracownik)`; bazowy
`BilansOtwarciaPIT` ma ctor `(Pracownik, WersjaBilansuOtwarciaPIT)`, ale jest abstrakcyjny.
Property `Pracownik` i `Wersja` są **read-only** (ustawiane przez ctor; brak ctora bezparametrowego).
Pola bazodanowe m.in.: `Data: Date`, kwoty przychodów/kosztów/składek w rozbiciu
etat/umowa/macierzyński (`Przychod26ZwolEtat`, `Przychod26ZwolUmowa`, `PrzychodUlgaEtat`,
`PrzychodUlgaUmowa`, `Spoleczne`, `Spoleczne26`, `Zdrowotne9Procent`, `SkladkiCzlonkowskie` itd.)
oraz kolekcja `Elementy: SubTable<Soneta.Place.ElementBilansuOtwarciaPIT>`.

**ERP-7** (wcześniej druk ZUS Rp-7) opiera się na `WynagrodzeniaERP7`/`NieobecnosciERP7` pracownika
oraz zaświadczeniach `Soneta.Deklaracje.ZUS.Z3`/`Z3a` (workery `ZUSZ3.Z3Worker`/`Z3aWorker` na
`Nieobecnosc`) — sam druk Z-3/ERP-7 to generowanie dokumentu w module Deklaracje.

**Snippet (dodanie bilansu otwarcia PIT i odczyt):**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];

using (var t = session.Logout(editMode: true))
{
    // BilansOtwarciaPIT jest abstrakcyjny — tworzymy konkretną wersję (_29 => PIT11_29, _11 => PIT11_11).
    // Pracownik ustawia ctor (property read-only), więc NIE używamy inicjalizatora obiektu na Pracownik.
    var bo = session.AddRow(new Soneta.Place.BilansOtwarciaPIT_29(pracownik));
    bo.Data            = new Date(2026, 1, 1);
    bo.PrzychodUlgaEtat = 12000m;
    bo.Spoleczne        = 1645.20m;
    t.Commit();
}
session.Save();

// Odczyt bilansów otwarcia PIT pracownika (typ kolekcji: SubTable<BilansOtwarciaPIT>):
foreach (Soneta.Place.BilansOtwarciaPIT bo in pracownik.BilansyOtwarciaPIT)
{
    // bo.Data, bo.PrzychodUlgaEtat, bo.Spoleczne, bo.Wersja
}
```

**Pułapki:**
- `BilansOtwarciaPIT` ma kolekcję `Elementy` — niektóre kwoty są wyliczane z elementów; sprawdź na
  Demo, czy ustawiasz pola root, czy elementy.
- Bilanse są **danymi wdrożeniowymi** (jednorazowe na start) — nie myl z naliczonymi deklaracjami.
- ERP-7 (Z-3/Z-3a) wymaga modułu Deklaracje i `KEDU`/PUE do eksportu — samo wprowadzenie
  `WynagrodzeniaERP7`/`NieobecnosciERP7` jest lokalne, ale wygenerowanie druku — nie.

