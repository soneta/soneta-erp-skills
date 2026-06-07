# KADRY11 — Ewidencje pracownicze

> Wspólne fakty o typie, podstawowe typy i szablon wzorca: [../kadry.md](../kadry.md).

> **Wzorzec wspólny.** Wszystkie ewidencje pracownicze to **kolekcje `SubTable` na rootcie
> `Pracownik`** (nie na `PracHistoria`). Każdy element jest osobnym `GuidedRow` (child pracownika)
> z polem `Pracownik: Soneta.Kadry.Pracownik` ustawianym automatycznie przez konstruktor
> `new Xxx(pracownik)`. Schemat dodania jest jednolity:
>
> ```csharp
> using (var t = session.Logout(editMode: true)) {
>     var wpis = session.AddRow(new Xxx(pracownik));   // ctor wiąże wpis z pracownikiem
>     // ... ustaw pola ...
>     t.Commit();                                       // Commit() w kodzie biznesowym
> }
> session.Save();
> ```
>
> `session.AddRow(new Xxx(pracownik))` i `pracownik.Kolekcja.AddRow(new Xxx(pracownik))` są
> równoważne — wpis trafia do tej samej tabeli i do `SubTable` pracownika. Większość typów wymaga
> wskazania **definicji** (rekord słownikowy, tabela konfiguracyjna) — definicję pobierasz przez
> `WgNazwy[...]` z odpowiedniego modułu, **nie** tworzysz jej w teście operacyjnym.

| Ewidencja | Kolekcja na `Pracownik` | Typ elementu | Tabela |
|---|---|---|---|
| KADRY-K1 Badania lekarskie | `BadaniaLekarskie: SubTable<BadanieLekarskie>` | `Soneta.Kadry.BadanieLekarskie` | `BadaniaLekarskie` |
| KADRY-K2 Szkolenia BHP | `SzkoleniaBHP: SubTable<SzkolenieBHP>` | `Soneta.Kadry.SzkolenieBHP` | `SzkoleniaBHP` |
| KADRY-K3 Wnioski o szkolenia | `WnioskiOSzkolenia: SubTable<WniosekOSzkolenie>` | `Soneta.HR.WniosekOSzkolenie` | `WnioskiOSzkol` |
| KADRY-K3 Ukończone szkolenia | `UkończoneSzkolenia: SubTable<UkończoneSzkolenie>` | `Soneta.HR.UkończoneSzkolenie` | `UkonczSzkolenia` |
| KADRY-K3 Uprawnienia | `Uprawnienia: SubTable<UprawnieniePracownika>` | `Soneta.HR.UprawnieniePracownika` | `UprawnieniaPrac` |
| KADRY-K4 Nagrody i kary | `NagrodyKary: SubTable<NagrodaKara>` | `Nagroda` / `Kara` (`NagrodaKara` abstr.) | `NagrodyKary` |
| KADRY-K4 Oświadczenia | `Oświadczenia: SubTable<OświadczeniePracownika>` | `Soneta.Kadry.OświadczeniePracownika` | `OswiadczeniaPrac` |
| KADRY-K5 Wypadki przy pracy | `Wypadki: SubTable<Wypadek>` | `Soneta.Kadry.Wypadek` | `Wypadki` |

---

### KADRY-K1 — Badania lekarskie

**Cel:** zarejestrować badanie lekarskie pracownika (wstępne/okresowe/kontrolne) wraz z terminami
ważności i datą następnego badania; ewentualnie wykonać operację seryjną dla grupy osób.

**Mechanizm:** `BadanieLekarskie` ma publiczny konstruktor `BadanieLekarskie(Pracownik pracownik)`.
Wpis wymaga `Definicja: DefinicjaBadaniaLekarskiego` (słownik, tabela konfiguracyjna `DefBadanLek`,
pobierana przez `WgNazwy[...]`). Jeśli definicja jest **cykliczna** (`Definicja.Cykliczne == true`,
ma `NastepneDefinicja`/`NastepneTermin`), platforma wylicza termin kolejnego badania —
udostępniony jako wyliczane `NastępneTermin`/`NastępneDefinicja`.

**Pola i typy (rekord `BadanieLekarskie`):**

| Pole | Typ | Uwaga |
|---|---|---|
| `Definicja` | `Soneta.Kadry.DefinicjaBadaniaLekarskiego` | wymagana; słownik `DefBadanLek` |
| `Data` | `Soneta.Types.Date` | data wykonania badania |
| `Termin` | `Soneta.Types.Date` | termin badania — **read-only** (wyliczany z `Data`+definicji); ustawienie rzuca `ColReadOnlyException` |
| `WazneDo` | `Soneta.Types.Date` | „Ważne do" — koniec ważności (ustawialny) |
| `PracaWOkularach` | `bool` | adnotacja medyczna |
| `KwotaDofinansowania` | `decimal`, `DataDofinansowania: Date` | dofinansowanie badania |
| `Opis` | `Soneta.Business.MemoText` | opis/uwagi |
| `Anulowany` | `bool` | flaga anulowania |
| `Pracownik` | `Soneta.Kadry.Pracownik` | ustawiany przez ctor |
| `NastępneTermin`, `NastępneDefinicja`, `Następne` | (wyliczane) | termin/def./wpis następnego badania |

**Manager:** `pracownik.Badania: Pracownik.BadaniaLekarskieManager` — pomocnik tylko do odczytu;
`pracownik.Badania.ZNajkrótszymTerminem(definicja = null): BadanieLekarskie` zwraca badanie z
najbliższym terminem wygaśnięcia (do raportów „badania okresowe do wykonania").

**Snippet:**

```csharp
var kadry = session.GetKadry();
var pracownik = kadry.Pracownicy.WgKodu["006"];
var definicja = kadry.DefBadanLek.WgNazwy["Wstępne"];     // słownik konfiguracyjny

using (var t = session.Logout(editMode: true))
{
    var badanie = session.AddRow(new BadanieLekarskie(pracownik));  // ctor wiąże z pracownikiem
    badanie.Definicja = definicja;
    badanie.Data      = Date.Today;
    // UWAGA: badanie.Termin jest read-only (wyliczany) — NIE ustawiaj go ręcznie.
    badanie.WazneDo   = new Date(Date.Today.Year + 2, Date.Today.Month, Date.Today.Day);

    t.Commit();
}
session.Save();
```

**Operacja seryjna (grupa pracowników):** w warstwie UI istnieje worker
`DodajBadaniaLekarskieWorker` (warianty `ZListyBadań`, `ZListyPracowników`) z akcją menu
„Operacje seryjne/Dodaj badania lekarskie" — iteruje po wybranych pracownikach i dla każdego robi
`new BadanieLekarskie(pracownik)` + `BadaniaLekarskie.AddRow(...)`. W kodzie biznesowym
seryjność realizujesz tą samą pętlą `foreach (var p in wybrani) { … AddRow … }` w jednej transakcji.

**Pułapki:**
- `Definicja` jest **wymagana** — bez niej `Save()` rzuci `RowException`.
- `Data`/`WazneDo` to `Soneta.Types.Date`, nie `DateTime`. `Termin` jest **read-only** (wyliczany) —
  próba ustawienia rzuca `ColReadOnlyException`. Reguła w weryfikatorach: `WazneDo` nie może być
  wcześniejsze niż `Termin`; termin następnego badania musi być **późniejszy** niż termin badania
  bieżącego — naruszenie wybucha jako `RowException` przy zapisie.
- `pracownik.Badania` to manager (odczyt), a kolekcją CRUD jest `pracownik.BadaniaLekarskie`
  (`SubTable<BadanieLekarskie>`). Nie myl tych dwóch.

---

### KADRY-K2 — Szkolenia BHP

**Cel:** zarejestrować odbyte szkolenie BHP (wstępne/okresowe) z terminem ważności i datą szkolenia
następnego (analogicznie do badań lekarskich).

**Mechanizm:** konstruktor `SzkolenieBHP(Pracownik pracownik)`; kolekcja `pracownik.SzkoleniaBHP`.
Wymagana `Definicja: DefinicjaSzkoleniaBHP` (słownik konfiguracyjny `DefSzkolenBHP`, `WgNazwy[...]`).
Cykliczność (`Definicja.Cykliczne`) wylicza `NastępneTermin`/`NastępneDefinicja`.

**Pola i typy (rekord `SzkolenieBHP`):**

| Pole | Typ | Uwaga |
|---|---|---|
| `Definicja` | `Soneta.Kadry.DefinicjaSzkoleniaBHP` | wymagana; słownik `DefSzkolenBHP` |
| `Data` | `Soneta.Types.Date` | data szkolenia |
| `Termin` | `Soneta.Types.Date` | termin — **read-only** (wyliczany); ustawienie rzuca `ColReadOnlyException` |
| `WażneDo` | `Soneta.Types.Date` | koniec ważności (wyliczane) |
| `Zakres` | `string` | zakres szkolenia |
| `Osoba` | `string` | prowadzący / osoba szkoląca |
| `Opis` | `Soneta.Business.MemoText` | uwagi |
| `Anulowany` | `bool` | flaga anulowania |
| `Pracownik` | `Soneta.Kadry.Pracownik` | ustawiany przez ctor |
| `NastępneTermin`, `NastępneDefinicja`, `Następne` | (wyliczane) | następne szkolenie |

**Snippet:**

```csharp
var kadry = session.GetKadry();
var pracownik = kadry.Pracownicy.WgKodu["007"];
var definicja = kadry.DefSzkolenBHP.WgNazwy["Wstępne"];

using (var t = session.Logout(editMode: true))
{
    var szkolenie = session.AddRow(new SzkolenieBHP(pracownik));
    szkolenie.Definicja = definicja;
    szkolenie.Data      = Date.Today;
    // UWAGA: szkolenie.Termin jest read-only (wyliczany) — NIE ustawiaj go ręcznie.
    szkolenie.Zakres    = "Instruktaż ogólny";

    t.Commit();
}
session.Save();
```

**Operacja seryjna:** UI udostępnia `DodajSzkolenieBHPWorker` (akcja menu, lista pracowników) —
w kodzie biznesowym pętla `foreach` + `new SzkolenieBHP(p)` + `AddRow` w jednej transakcji.

**Pułapki:**
- `Definicja` wymagana (jak w KADRY-K1).
- Uwaga na pisownię: pole nazywa się `WażneDo` (z „ż"), a w `BadanieLekarskie` — `WazneDo` (bez).
- `Termin` jest **read-only** (wyliczany) — ustawienie rzuca `ColReadOnlyException`.
- `Termin` następnego szkolenia musi być późniejszy niż bieżący — inaczej `RowException`.

---

### KADRY-K3 — Szkolenia i uprawnienia (moduł HR/HR2)

**Cel:** obsłużyć cykl rozwoju kompetencji: **wniosek o szkolenie** → **ukończone szkolenie** →
**uprawnienie/certyfikat**, wraz z kosztem i budżetem szkoleń. Typy leżą w module `Soneta.HR`
(`session.GetHR()`).

**KADRY-K3a — Wniosek o szkolenie** — `WniosekOSzkolenie([Required] Pracownik pracownik)`; kolekcja
`pracownik.WnioskiOSzkolenia`. Pola:

| Pole | Typ | Uwaga |
|---|---|---|
| `Definicja` | `Soneta.HR.DefinicjaSzkolenia` | rodzaj szkolenia (słownik HR) |
| `Etap` | `Soneta.HR.EtapRealizacjiSzkolenia` | np. „Wniosek zatwierdzony" (`hr.EtapRealizSzkol.WgNazwy[...]`) |
| `Realizacja` | `Soneta.HR.RealizacjaSzkolenia` | konkretna realizacja |
| `Budzet` | `Soneta.HR.BudżetSzkoleń` | budżet, z którego finansowane |
| `Koszt` | `Soneta.Types.Currency` | koszt szkolenia |
| `DataZgloszenia`, `Termin`, `DataAnulowania` | `Soneta.Types.Date` | daty cyklu wniosku |
| `Kierownik` | `Soneta.Kadry.Pracownik` | akceptujący |
| `SkierowanyPrzezZaklad` | `bool` | skierowanie pracodawcy |
| `Ocena` | `string`, `Opis: MemoText` | ocena/uwagi |

**KADRY-K3b — Ukończone szkolenie** — dwa ctory: `UkończoneSzkolenie([Required] Pracownik pracownik)`
oraz `UkończoneSzkolenie(WniosekOSzkolenie wniosek)` (przepina pracownika z wniosku). Kolekcja
`pracownik.UkończoneSzkolenia`. Pola: `Nazwa: string`, `Okres: FromTo`, `Ocena: string`,
`Opis: MemoText`, `Wniosek: WniosekOSzkolenie` (powiązanie).

**KADRY-K3c — Uprawnienie / certyfikat** — `UprawnieniePracownika([Required] Pracownik pracownik)`;
kolekcja `pracownik.Uprawnienia`. Pola:

| Pole | Typ | Uwaga |
|---|---|---|
| `Definicja` | `Soneta.HR.DefinicjaUprawnienia` | rodzaj uprawnienia |
| `Numer` | `string` | numer uprawnienia/certyfikatu |
| `DataUzyskania`, `DataUtraty`, `TerminWaznosci` | `Soneta.Types.Date` | daty ważności |
| `Okres` | `Soneta.Types.FromTo` | okres obowiązywania |
| `WydanePrzez` | `string` | organ wydający |
| `Zrodlo` | `Soneta.HR.IŹródłoUzyskaniaUprawnienia` | źródło (np. ukończone szkolenie) |

**Snippet (wniosek → koszt z budżetu):**

```csharp
var hr = session.GetHR();
var pracownik = session.GetKadry().Pracownicy.WgKodu["008"];

using (var t = session.Logout(editMode: true))
{
    var wniosek = session.AddRow(new WniosekOSzkolenie(pracownik));
    wniosek.Definicja     = hr.DefinicjeSzkolen.WgNazwy["Kurs zawodowy"];
    wniosek.Etap          = hr.EtapRealizSzkol.WgNazwy["Wniosek zatwierdzony"];
    wniosek.DataZgloszenia = Date.Today;
    wniosek.Koszt          = new Currency(1500m);

    t.Commit();
}
session.Save();
```

**Pułapki:**
- Typy KADRY-K3 są w `Soneta.HR` (`session.GetHR()`), nie w `Soneta.Kadry`.
- `Etap`/`Definicja` to wpisy słownikowe HR — pobieraj `WgNazwy[...]`, nie twórz w teście.
- `Koszt`/`Budżet` używają `Soneta.Types.Currency` (waluta), nie `decimal`.

---

### KADRY-K4 — Nagrody i kary; oświadczenia (PIT-2, RODO, zgody)

**KADRY-K4a — Nagrody i kary.** Klasa bazowa `Soneta.Kadry.NagrodaKara` jest **abstrakcyjna** — używaj
konkretnych podtypów: `Soneta.Kadry.Nagroda(Pracownik)` i `Soneta.Kadry.Kara(Pracownik)`. Oba ctory
delegują do `NagrodaKara(pracownik, TypNagrodyKary)` ustawiając `Typ` na `Nagroda`/`Kara`. Kolekcja
`pracownik.NagrodyKary: SubTable<NagrodaKara>`. Pola:

| Pole | Typ | Uwaga |
|---|---|---|
| `Definicja` | `Soneta.Kadry.DefinicjaNagrodyKary` | słownik `DefNagrodKar`; ma własne pole `Typ` (Nagroda/Kara) — musi zgadzać się z podtypem wpisu, inaczej `set_Definicja` rzuca `ArgumentException`; może nieść `Element`/`Kwota` |
| `Typ` | `Soneta.Kadry.TypNagrodyKary` | `Nagroda`/`Kara` (ustawia ctor podtypu) |
| `Data` | `Soneta.Types.Date` | data nadania |
| `DataAnulowania` | `Soneta.Types.Date` | anulowanie |
| `Rozliczenie` | `Soneta.Kadry.RozliczenieSwiadczenia` (subrow) | `Rozliczenie.Kwota: Currency`, `Rozliczenie.Element: DefinicjaElementu`, `Rozliczenie.Okres: FromTo` — powiązanie z wypłatą |
| `Opis` | `Soneta.Business.MemoText` | treść nagrody/kary |
| `Pracownik` | `Soneta.Kadry.Pracownik` | ustawiany przez ctor |

**KADRY-K4b — Oświadczenia (PIT-2, RODO, zgody).** `Soneta.Kadry.OświadczeniePracownika` — trzy ctory:
`OświadczeniePracownika([Required] Pracownik pracownik, [Required] DefinicjaOświadczenia definicja)`,
wariant z `Date dataZłożenia`, oraz `(RowCreator)`. Kolekcja `pracownik.Oświadczenia`. Rodzaj
oświadczenia (PIT-2, zgoda RODO, zgoda na e-doręczenia itp.) określa `Definicja` (słownik
konfiguracyjny `DefOswiadczen`). Pola:

| Pole | Typ | Uwaga |
|---|---|---|
| `Definicja` | `Soneta.Kadry.DefinicjaOświadczenia` | wymagana w ctorze; słownik `DefOswiadczen` |
| `DataZlozenia` | `Soneta.Types.Date` | data złożenia |
| `DataWycofania` | `Soneta.Types.Date` | data wycofania |
| `Okres` | `Soneta.Types.FromTo` | okres obowiązywania (z `Definicja.OkresWaznosci`/`OkresIlosc`) |
| `Tresc` | `Soneta.Business.MemoText` | treść |
| `TrescOswiadczenia` | `Soneta.Kadry.TreśćOświadczenia` | treść strukturalna |
| `Pracownik` | `Soneta.Kadry.Pracownik` | ustawiany przez ctor |

**Snippet (nagroda + oświadczenie PIT-2):**

```csharp
var kadry = session.GetKadry();
var pracownik = kadry.Pracownicy.WgKodu["009"];

using (var t = session.Logout(editMode: true))
{
    // nagroda — konkretny podtyp, NIE abstrakcyjna NagrodaKara
    var nagroda = session.AddRow(new Nagroda(pracownik));
    nagroda.Definicja = kadry.DefNagrodKar.WgNazwy["Nagroda uznaniowa"];
    nagroda.Data      = Date.Today;

    // oświadczenie — definicja jest wymagana w konstruktorze
    var defPit2 = kadry.DefOswiadczen.WgNazwy["PIT-2"];
    var oswiadczenie = session.AddRow(new OświadczeniePracownika(pracownik, defPit2, Date.Today));

    t.Commit();
}
session.Save();
```

**Pułapki:**
- **Nie** rób `new NagrodaKara(...)` — typ abstrakcyjny. Używaj `Nagroda`/`Kara`.
- `Definicja` musi mieć **`Typ` zgodny** z podtypem wpisu (`Nagroda` → def. o `Typ==Nagroda`, `Kara` →
  def. o `Typ==Kara`); przypisanie niezgodnej typem definicji rzuca `ArgumentException` w `set_Definicja`.
  Filtruj słownik: `DefNagrodKar.Cast<DefinicjaNagrodyKary>().FirstOrDefault(d => d.Typ == TypNagrodyKary.Nagroda)`.
- `OświadczeniePracownika` **nie ma** ctora samego `(Pracownik)` — definicja jest `[Required]`
  w konstruktorze; bez niej kod się nie skompiluje.
- `Rozliczenie.*` na nagrodzie/karze to subrow powiązania z wypłatą (`Currency`, `DefinicjaElementu`)
  — wypełniane przy rozliczaniu w płacach, nie przy samym wpisie.

---

### KADRY-K5 — Wypadki przy pracy

**Cel:** zarejestrować wypadek przy pracy wraz z dokumentacją powypadkową (protokół, decyzja,
okoliczności, skutki) i ewentualnym świadczeniem.

**Mechanizm:** `Soneta.Kadry.Wypadek(Pracownik pracownik)`; kolekcja `pracownik.Wypadki`. Wpis jest
numerowany (`Numer: Soneta.Core.NumerDokumentu`) i wymaga `Definicja: Soneta.Core.DefinicjaDokumentu`
(definicja dokumentu wypadku).

**Pola i typy (rekord `Wypadek`):**

| Pole | Typ | Uwaga |
|---|---|---|
| `Definicja` | `Soneta.Core.DefinicjaDokumentu` | definicja dokumentu (numeracja) |
| `Numer` | `Soneta.Core.NumerDokumentu` (subrow) | `Numer.Pelny`, `Numer.Symbol`, `Numer.Numer` |
| `Data` | `Soneta.Types.Date` | data wypadku |
| `Godzina` | `Soneta.Types.Time` | godzina wypadku |
| `DataZgloszenia` | `Soneta.Types.Date` | data zgłoszenia |
| `Miejsce` | `string` | miejsce wypadku |
| `Rodzaj` | `Soneta.Kadry.RodzajWypadku` | klasyfikacja wypadku |
| `PrzyPracy`, `Ciezki`, `Smiertelny`, `Niezdolnosc` | `bool` | kwalifikacja skutków |
| `Okolicznosci`, `Skutki`, `Odmowa` | `Soneta.Business.MemoText` | dokumentacja opisowa |
| `ProtokolNumer`, `ProtokolData` | `string` / `Date` | protokół powypadkowy |
| `DecyzjaNumer`, `DecyzjaData` | `string` / `Date` | decyzja |
| `PismoNumer`, `PismoData` | `string` / `Date` | pismo |
| `SKW` | `string` | statystyczna karta wypadku |
| `Kwota` | `decimal` | kwota świadczenia |
| `PracHistoria` | `Soneta.Kadry.PracHistoria` | (wyliczane) zapis kadrowy na datę |
| `Pracownik` | `Soneta.Kadry.Pracownik` | ustawiany przez ctor |

**Snippet:**

```csharp
var kadry = session.GetKadry();
var pracownik = kadry.Pracownicy.WgKodu["010"];

using (var t = session.Logout(editMode: true))
{
    var wypadek = session.AddRow(new Wypadek(pracownik));
    wypadek.Data          = Date.Today;
    wypadek.Godzina       = new Time(10, 30);
    wypadek.DataZgloszenia = Date.Today;
    wypadek.Miejsce       = "Hala produkcyjna";
    wypadek.PrzyPracy     = true;
    wypadek.Okolicznosci  = new MemoText("Poślizgnięcie na mokrej posadzce.");

    t.Commit();
}
session.Save();
```

**Pułapki:**
- `Numer` jest subrowem nadawanym wg `Definicja` (numeracja) — nie ustawiaj `Numer.Pelny` ręcznie,
  numer nadaje platforma; gdy `Definicja` ma własną numerację, podpięcie definicji wystarcza.
- `Godzina` to `Soneta.Types.Time`, `Data` to `Soneta.Types.Date` — nie `DateTime`.
- Pola opisowe (`Okolicznosci`, `Skutki`, `Odmowa`) to `MemoText`, nie `string`.

### KADRY-K6 — RODO/GIODO: oświadczenia, uprawnienia do przetwarzania, wymiana danych

**Cel:** ewidencjonować zgody/oświadczenia RODO pracownika, uprawnienia do przetwarzania danych
osobowych oraz fakty wymiany danych (pozyskanie / udostępnienie / powierzenie). Pracownik jest
hostem GIODO — implementuje `IGIODOOświadczenieHost`, `IGIODOUprawnienieHost`, `IGIODOWymianaDanychHost`,
`IGIODOZgodnyHost`. Zapis „teczki" personalnej do pliku jest operacją plikową (poza zakresem testów).

**Publiczny kontrakt — kolekcje na `Pracownik` (moduł `Soneta.Core`):**

| Kolekcja | Typ elementu | Zawartość |
|---|---|---|
| `GIODOOświadczenia` | `SubTable<Soneta.Core.GIODOOświadczenie>` | oświadczenia / zgody RODO |
| `GIODOUprawnienia` | `SubTable<Soneta.Core.GIODOUprawnienie>` | uprawnienia do przetwarzania danych |
| `GIODOUdostępnienia` | `SubTable<Soneta.Core.GIODOWymianaDanych>` | pozyskanie / udostępnienie / powierzenie danych |
| `PotwierdzeniaGIODO` | `SubTable<Soneta.Core.GIODOZgodny>` | potwierdzenia zgodności; `ZgodnoscGIODOPotwierdzona: bool` (kalkulowane) |

**`GIODOOświadczenie` (tabela `GIODOOswiadcz`, root) — pola bazodanowe:**

| Pole | Typ | Uwaga |
|---|---|---|
| `Host` | `IGIODOOświadczenieHost` | składający oświadczenie (= `Pracownik`) |
| `Definicja` | `Soneta.Core.GIODODefinicjaOświadczenia` | **referencja konfiguracyjna** (wymagana przez ctor) |
| `Data` | `Soneta.Types.Date` | data oświadczenia (zapisywalne) |
| `Okres` | `Soneta.Types.FromTo` | okres obowiązywania zgody — **read-only** (wyliczane z definicji) |
| `Rodzaj` | `Soneta.Core.RodzajeOświadczeńGIODO` | `Oświadczenie`, `UdzielenieZgody`, `WycofanieZgody` — **read-only** (wynika z definicji) |
| `Oswiadczenie` | `bool` | flaga oświadczenia |
| `Tresc` | `Soneta.Business.MemoText` | treść |
| `SposobPozyskania` | `string` | — |
| `DataWycofaniaZgody` | `Soneta.Types.Date` | — |
| `WycofanieZgody` | `GIODOOświadczenie` | powiązanie z zapisem wycofania |
| `Bufor` | `bool` | zatwierdzenie |

Ctor: `new GIODOOświadczenie(IGIODOOświadczenieHost host, GIODODefinicjaOświadczenia definicja)`.

**`GIODOUprawnienie` (tabela `GIODOUprawnienia`, root) — pola bazodanowe:**

| Pole | Typ | Uwaga |
|---|---|---|
| `Uprawniony` | `IGIODOUprawnienieHost` | = `Pracownik` |
| `Definicja` | `Soneta.Core.GIODODefinicjaUprawnienia` | **referencja konfiguracyjna** (wymagana przez ctor) |
| `Data`, `Przyznane`, `Odebrane` | `Soneta.Types.Date` | data zapisu / od kiedy przyznane / od kiedy odebrane |
| `Okres` | `Soneta.Types.FromTo` | okres przyznania |
| `Tresc` | `Soneta.Business.MemoText` | — |
| `WycofanieUprawnienia` | `GIODOUprawnienie` | powiązanie z wycofaniem |

Ctor: `new GIODOUprawnienie(IGIODOUprawnienieHost uprawniony, GIODODefinicjaUprawnienia definicja)`.

**`GIODOWymianaDanych` (tabela `GIODOWymDanych`, root) — pola bazodanowe:**

| Pole | Typ | Uwaga |
|---|---|---|
| `Host` | `IGIODOWymianaDanychHost` | = `Pracownik` |
| `Kierunek` | `Soneta.Core.GIODOKierunekWymianyDanych` | `Powierzenie`, `Pozyskanie`, `PowierzenieZbioru`, `PozyskanieZbioru`, `Udostępnienie` |
| `Podmiot` | `Soneta.Core.IKontrahent` | druga strona wymiany |
| `Data` | `Soneta.Types.Date` | data wymiany |
| `Zakres` | `Soneta.Business.MemoText` | zakres danych |
| `SposobPozyskania` | `string` | — |
| `PozyskaneOdOsoby`, `UdostepnioneOsobie`, `NaWniosekOsoby`, `TylkoDostep` | `bool` | flagi |
| `Definicja` | `Soneta.Core.DefinicjaDokumentu` | def. numeracji dokumentu |
| `ZbiorDanych` | `Soneta.Core.GIODO.GIODOZbiorDanych` | zbiór danych |

`GIODOWymianaDanych` **nie ma publicznego konstruktora** — rekordy tworzą wyłącznie workery poniżej
(zwracają konkretne podtypy `GIODOPozyskanieDanych` / `GIODOUdostępnienieDanych` / `GIODOPowierzenieDanych`).

**Workery RODO (jedyna droga dodania przez API; klasy zagnieżdżone w `Soneta.Kadry.Pracownicy`):**

| Worker | Metoda | Zwraca | Parametry (`Pars` / `Params`) |
|---|---|---|---|
| `Pracownicy.DodajOświadczeniaWorker` | `GIODOOświadczenie DodajOświadczenia()` | oświadczenie | `Pars`: `Definicja: GIODODefinicjaOświadczenia`, `Data`, `Oddział`, `SposobPozyskania`, `Zatwierdź: bool` |
| `Pracownicy.DodajUprawnieniaWorker` | `GIODOUprawnienie DodajUprawnienia()` | uprawnienie | `Pars`: `Definicja: GIODODefinicjaUprawnienia`, `Data`, `Przyznane`, `Odebrane`, `Oddział`, `Zatwierdź: bool` |
| `Pracownicy.DodajPozyskanieDanychWorker` | `GIODOPozyskanieDanych DodajPozyskanieDanych()` | wymiana (pozyskanie) | `Pars`: `Podmiot: IKontrahent`, `Data`, `Zakres: string`, `Oddział`, `SposobPozyskania`, `Zatwierdź: bool` |
| `Pracownicy.DodajUdostępnienieDanychWorker` | `GIODOUdostępnienieDanych DodajUdostępnienieDanych()` | wymiana (udostępnienie) | `Pars`: `Podmiot: IKontrahent`, `Data`, `Zakres: string`, `Oddział`, `Zatwierdź: bool` |
| `Pracownicy.DodajPowierzenieDanychWorker` | `GIODOPowierzenieDanych DodajPowierzenieDanych()` | wymiana (powierzenie) | `Pars` (analogicznie) |

Wszystkie workery RODO mają bezparametrowy ctor oraz property `Hosts: Pracownik[]` (`[Context]`, lista
pracowników, których dotyczy operacja) i `Session`.

**Zapis teczki personalnej do pliku — `Pracownik.ZapiszTeczkęDoPlikuWorker`** (akcja
„Teczka.../Zapisz teczkę do pliku", metoda `ZapiszTeczkeDoPliku()`, property `Param`) — to
**operacja plikowa** (serializacja dokumentacji do plików XML/katalogu na dysku). **Poza zakresem
testów jednostkowych → `[Ignore]`** (zależność od systemu plików).

**Snippet (dodanie oświadczenia GIODO workerem):**

```csharp
var kadry = session.GetKadry();
var pracownik = kadry.Pracownicy.WgKodu["006"];

// Definicja oświadczenia z konfiguracji (musi istnieć w bazie):
var defOswiadczenia = session.ExecuteConfig(s =>
    s.GetCore().GIODODefinicjeOświadczeń.WgNazwy["Zgoda na przetwarzanie danych"]);

using (var t = session.Logout(editMode: true))
{
    var worker = new Pracownik.Pracownicy.DodajOświadczeniaWorker { Hosts = new[] { pracownik } };
    worker.Pars.Definicja = session.Get(defOswiadczenia);
    worker.Pars.Data = Date.Today;
    worker.Pars.Zatwierdź = true;
    GIODOOświadczenie oswiadczenie = worker.DodajOświadczenia();
    t.CommitUI();
}
session.Save();

// Odczyt oświadczeń pracownika:
foreach (GIODOOświadczenie o in pracownik.GIODOOświadczenia)
{
    // o.Definicja, o.Okres, o.Rodzaj, o.Data
}
```

**Snippet (dodanie oświadczenia bez workera — bezpośrednim ctorem):**

```csharp
using (var t = session.Logout(editMode: true))
{
    var o = session.AddRow(new GIODOOświadczenie(pracownik, session.Get(defOswiadczenia)));
    // host i Definicja wynikają z ctora; Rodzaj/Okres są WYLICZANE (read-only) z definicji — nie ustawiaj ich.
    o.Data = Date.Today;
    o.SposobPozyskania = "Formularz papierowy";
    t.Commit();
}
session.Save();
```

**Pułapki:**
- `GIODOOświadczenie`/`GIODOUprawnienie` wymagają **referencji do definicji konfiguracyjnej**
  (`GIODODefinicjaOświadczenia` / `GIODODefinicjaUprawnienia`) — pobierz istniejący rekord
  (`ExecuteConfig`), nie twórz „w locie". Bez definicji w bazie scenariusz wymaga uprzedniej
  konfiguracji modułu RODO/GIODO.
- `GIODOWymianaDanych` **nie ma publicznego ctora** — dodawaj wyłącznie workerami
  `DodajPozyskanieDanychWorker` / `DodajUdostępnienieDanychWorker` / `DodajPowierzenieDanychWorker`.
- Workery RODO modyfikują dane i są uruchamiane „jak z UI" → transakcja edycyjna + `CommitUI()` +
  `Save()`. `Hosts`/`Podmiot` muszą pochodzić z bieżącej sesji (safe-code §2.1).
- Obowiązywanie zgody jest „na dzień" — czytaj `Okres`/`Data`, nie zakładaj bezterminowości.
- Dane wrażliwe (treść oświadczeń, podmioty) — nie loguj nadmiarowo (safe-code §12).
- Workery RODO wymagają praw do obszaru GIODO; w teście biznesowym egzekucji praw nie sprawdzamy
  (safe-code §7.2).

### KADRY-K7 — Struktura organizacyjna: przypisanie do wydziału/struktury, powiązania

**Cel:** przypisać pracownika do jednostki organizacyjnej (wydziału) oraz do elementów struktury
organizacyjnej (np. stanowiska w strukturze, relacje przełożony–podwładny). Wydział wynika z warunków
etatu (`Etat.Wydzial`, historyczne — patrz sekcja B), a powiązania ze strukturą trzyma osobna kolekcja.

**Publiczny kontrakt:**

| Składnik | Typ | Rodzaj | Uwaga |
|---|---|---|---|
| `Etat.Wydzial` | `Soneta.Kadry.Wydzial` | bazodanowe (na `PracHistoria.Etat`) | jednostka organizacyjna; korzeń: `session.GetKadry().Wydzialy.Firma` (zmiana „od daty" — KADRY-A14) |
| `PowiązaniaStrOrg` | `SubTable<Soneta.Core.PowiązanieStrukturyOrganizacyjnej>` | kolekcja na `Pracownik` | powiązania ze strukturą organizacyjną |
| `StrukturaOraganizacyjna` | `Pracownik.StrukturaOraganizacyjnaManager` | manager (read-only API) | nawigacja przełożeni/podwładni |
| Pracownik implementuje | `IŹródłoPowiązaniaStrukturyOrganizacyjnej` | interfejs | jest źródłem powiązań |

**`PowiązanieStrukturyOrganizacyjnej` (tabela `PowiazaniaStrOrg`, child przez `Zrodlo`) — pola:**

| Pole | Typ | Uwaga |
|---|---|---|
| `Zrodlo` | `IŹródłoPowiązaniaStrukturyOrganizacyjnej` | guided-parent (= `Pracownik`) |
| `Element` | `Soneta.Core.ElementStrukturyOrganizacyjnej` | referencja do **instancji** elementu struktury z tabeli `CoreModule.ElementyStrOrg` (NIE z `DefElStrukturOrg`, która trzyma `DefinicjaElementuStrukturyOrganizacyjnej`); `ElementStrukturyOrganizacyjnej` nie ma publicznego ctora — pobierz istniejący rekord |
| `Okres` | `Soneta.Types.FromTo` | okres obowiązywania powiązania (zapisywalne) |

Ctor: `new PowiązanieStrukturyOrganizacyjnej(ElementStrukturyOrganizacyjnej element, IŹródłoPowiązaniaStrukturyOrganizacyjnej zrodlo)`.

**Manager `StrukturaOraganizacyjnaManager` (tylko odczyt nawigacyjny):**

| Metoda / property | Sygnatura | Zwraca |
|---|---|---|
| `Przełożony(...)` | `Pracownik Przełożony(StrukturaOrganizacyjna, Date, bool, Func<...>)` | bezpośredni przełożony |
| `PrzełożonyWgPodległości(...)` | `Pracownik PrzełożonyWgPodległości(Date, bool, Func<...>)` | przełożony wg podległości |
| `Przełożeni(...)` | `IEnumerable<Pracownik> …` | przełożeni |
| `Podwładni(...)` | `IEnumerable<Pracownik> Podwładni(FromTo, bool, Func<...>)` | podwładni w okresie |
| `GetDomyślnyPrzełożony(naDzień[, bezpośredni, warunek])` | `Pracownik GetDomyślnyPrzełożony(Date, bool=…, Func=…)` | domyślny przełożony na dzień (property `DomyślnyPrzełożony` jest **przestarzała** — używaj metody) |

**Workery zmiany powiązań (klasy zagnieżdżone w `Soneta.Kadry.Pracownik`):**

| Worker | Akcja (menu) | Metoda | Parametry |
|---|---|---|---|
| `Pracownik.DodajPowiązanieStrukturyWorker` | „Struktura organizacyjna/Dodaj lub modyfikuj powiązanie…" | `object DodajPowiązanieStruktury()` | `Params: WybórElementuContext`, `Pracownicy: Pracownik[]` (`[Context]`) |
| `Pracownik.UsuńPowiązanieStrukturyWorker` | „Struktura organizacyjna/Usuń powiązanie…" | `void DodajPowiązanieStruktury()` | `Params: WybórElementuContext`, `Pracownicy: Pracownik[]` |

**Snippet (dodanie powiązania ze strukturą — bezpośrednim ctorem):**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];

// Instancja elementu struktury (musi istnieć w bazie — tabela ElementyStrOrg, NIE DefElStrukturOrg):
ElementStrukturyOrganizacyjnej element =
    session.GetCore().ElementyStrOrg.Cast<ElementStrukturyOrganizacyjnej>().FirstOrDefault();

using (var t = session.Logout(editMode: true))
{
    var p = session.AddRow(new PowiązanieStrukturyOrganizacyjnej(element, pracownik));
    p.Okres = new FromTo(Date.Today, Date.MaxValue);
    t.Commit();
}
session.Save();

// Odczyt nawigacyjny struktury:
Pracownik przelozony = pracownik.StrukturaOraganizacyjna.GetDomyślnyPrzełożony(Date.Today);
```

**Snippet (zmiana wydziału — nowy zapis „od daty", KADRY-A14):**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
var kadry = session.GetKadry();

using (var t = session.Logout(editMode: true))
{
    var ph = pracownik[Date.Today];           // zapis obowiązujący na dzień (KADRY-A15)
    ph.Etat.Wydzial = kadry.Wydzialy.Firma;   // referencja do istniejącego wydziału (korzeń struktury)
    t.Commit();
}
session.Save();
```

**Pułapki:**
- `Etat.Wydzial` jest **danymi historycznymi** (na `PracHistoria.Etat`) i jest **wymagany dla etatu** —
  zmieniaj nowym zapisem „od daty" (KADRY-A14), nie nadpisuj bieżącego (zmieniłoby cały okres wstecz).
- `PowiązanieStrukturyOrganizacyjnej.Element` to **referencja konfiguracyjna** — pobierz istniejący
  element struktury; bez zdefiniowanej struktury organizacyjnej scenariusz wymaga konfiguracji.
- `StrukturaOraganizacyjnaManager` jest **tylko do odczytu** — zmiany realizują workery
  `DodajPowiązanieStrukturyWorker` / `UsuńPowiązanieStrukturyWorker` lub bezpośredni zapis do
  `PowiązaniaStrOrg`.
- Workery struktury modyfikują dane „jak z UI" → transakcja + `CommitUI()` + `Save()`; rekordy z
  bieżącej sesji (safe-code §2.1).

### KADRY-K8 — Oceny okresowe: arkusze ocen, cele okresowe, karty kompetencji

**Cel:** prowadzić oceny okresowe pracownika (arkusz oceny z elementami), cele okresowe wraz z ich
realizacją, karty kompetencji i karty opisu stanowiska. Funkcjonalność należy do modułów **HR**
(`session.GetHR()`, `OcenyPracownikow`, `EtapyRekrutacji`) i **HR2** (`session.GetHR2()`,
`CeleOkresowePrac`, `KartyKompPrac`, `KartyOpStanowisk`). Pracownik implementuje `IOceniany`,
`IOceniający`, `IOdpowiedzialnyZaOcenę`, `IŹródłoKartyOpisuStanowiska`.

**Publiczny kontrakt — kolekcje na `Pracownik`:**

| Kolekcja | Typ elementu | Zawartość |
|---|---|---|
| `Oceny` | `SubTable<Soneta.HR.OcenaPracownika>` | arkusze ocen okresowych (root) |
| `ElementyOceny` | `SubTable<Soneta.HR.ElementOcenyPracownika>` | pojedyncze elementy/pozycje arkuszy ocen |
| `CeleOkresowe` | `SubTable<Soneta.HR2.CelOkresowyPracownika>` | cele okresowe |
| `KartyKompetencji` | `SubTable<Soneta.HR2.KartaKompetencjiPracownika>` | karty kompetencji |
| `KartyOpisuStanowiska` | `SubTable<Soneta.HR2.KartaOpisuStanowiskaBase>` | karty opisu stanowiska |
| `KartyRealizacjiCelu` | `SubTable<Soneta.HR2.KartaRealizacjiCelu>` | karty realizacji celów |
| `Oceniani` / `Oceniający` | `SubTable<Soneta.Oceny.OcenaOceniany/OcenaOceniający>` | role pracownika w ocenie |

**`OcenaPracownika` (tabela `OcenyPracownikow`, root; impl. `IOcenaPracownika`) — pola:**

| Pole | Typ | Uwaga |
|---|---|---|
| `Pracownik` | `Soneta.Kadry.Pracownik` | oceniany |
| `Nazwa` | `string` | nazwa arkusza |
| `Data`, `Termin` | `Soneta.Types.Date` | data oceny / termin |
| `Opis` | `Soneta.Business.MemoText` | — |
| `Anulowany` | `bool` | — |
| `ElementyOceny` | `SubTable<ElementOcenyPracownika>` | pozycje arkusza |

Ctor: `new OcenaPracownika(Pracownik pracownik)`.

**`ElementOcenyPracownika` (tabela `ElementyOcenPrac`, child przez `Ocena`) — pola:**

| Pole | Typ | Uwaga |
|---|---|---|
| `Ocena` | `IOcenaPracownika` | guided-parent (arkusz oceny lub etap rekrutacji) |
| `Pracownik` | `Soneta.Kadry.Pracownik` | — |
| `Definicja` | `Soneta.HR.DefElementuOcenyPracownika` | **referencja konfiguracyjna** (z tabeli `HRModule.DefElemOcenPrac`); zapisywalna i wymagana do zapisu |
| `Typ` | `Soneta.HR.TypyElementowOceny` | `Historyczny`, `Aktualny` — **read-only** (wynika z definicji) |
| `Data` | `Soneta.Types.Date` | **read-only** (wyliczane) |
| `Wartosc` | `decimal` | wartość liczbowa oceny (zapisywalna) |

Ctor: `new ElementOcenyPracownika(IOcenaPracownika ocena)`. Dodawaj przez `session.AddRow(new ElementOcenyPracownika(ocena))` (NIE `ocena.ElementyOceny.AddRow(...)` — `SubTable` nie udostępnia `AddRow`).

**`CelOkresowyPracownika` (tabela `CeleOkresowePrac`, root) — pola:**

| Pole | Typ | Uwaga |
|---|---|---|
| `Pracownik` | `Soneta.Kadry.Pracownik` | — |
| `Nazwa` | `string` | nazwa celu |
| `Data`, `Termin` | `Soneta.Types.Date` | — |
| `Opis` | `Soneta.Business.MemoText` | — |
| `Definicja` | `Soneta.Oceny.DefinicjaElementuOceny` | **referencja konfiguracyjna** (opcjonalna) |
| `Anulowany` | `bool` | — |
| `Realizacja` | `Soneta.HR2.RealizacjaCelu` | bieżąca realizacja (subrow) |
| `Realizacje` | `SubTable<Soneta.HR2.RealizacjaCelu>` | realizacje celu |

> `CelOkresowyPracownika` **nie ma pola `Wartosc`** — postęp/ocena celu jest reprezentowana przez `Realizacja`/`Realizacje` (`Soneta.HR2.RealizacjaCelu`). Pole `Wartosc` (typu decimal) ma natomiast `ElementOcenyPracownika`.

Ctor: `new CelOkresowyPracownika(Pracownik pracownik)`.

**`KartaOpisuStanowiskaBase` (tabela `KartyOpStanowisk`, root) — pola:**

| Pole | Typ | Uwaga |
|---|---|---|
| `Zrodlo` | `IŹródłoKartyOpisuStanowiska` | = `Pracownik` / `DefinicjaStanowiska` / wakat / oferta |
| `Typ` | `Soneta.HR2.TypyKartOpisuStanowiska` | `KartaOpisuStanowiska`, `OgłoszenieOPracę` |
| `Data` | `Soneta.Types.Date` | — |
| `Elementy` | `SubTable<ElementKartyOpisuStanowiska>` | elementy opisu |
| `Kompetencje` | `SubTable<KompetencjaKartyOpisuStanowiska>` | kompetencje |

`KartaOpisuStanowiskaBase` i `KartaKompetencjiPracownika` **nie mają publicznego ctora bezparametrowego**;
`KartaKompetencjiPracownika` ma ctor `(Pracownik pracownik, IŹródłoKartyCharakterystykiPracownika zrodlo)`.
Karty zwykle tworzone są workerami kopiującymi (`KopiujKartęOpisuStanowiskaWorker.KopiujZDefinicjiStanowiska()`,
`KopiujKartęKompetencjiWorker.KopiujZKOS()`/`KopiujZPoprzedniej()`).

**Workery oceniania (klasy w `Soneta.HR` / `Soneta.HR2`):**

| Worker | Metoda | Parametry |
|---|---|---|
| `Soneta.HR.OcenaPracownikowWorker` | `Oceń()` | `Pars`, `Idxs: Pracownik[]` (`[Context]`); ctor `(Context)` |
| `Soneta.HR.WzorOcenyPracownika.ZainicjujOcenęWorker` | `Zainicjuj()` | `Ocena: IOcenaPracownika`, `Pars`; ctor `(Session)` |
| `Soneta.HR2.KopiujKartęOpisuStanowiskaWorker` | `KopiujZDefinicjiStanowiska()`, `KopiujZPoprzedniej()` | `Karta: KartaOpisuStanowiskaBase` |
| `Soneta.HR2.KopiujKartęKompetencjiWorker` | `KopiujZKOS()`, `KopiujZPoprzedniej()` | `Karta: KartaKompetencjiPracownika` |

**Snippet (dodanie celu okresowego — wymaga definicji elementu oceny w bazie HR2):**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
var hr2 = session.GetHR2();

// Definicja elementu oceny z konfiguracji modułu Oceny (musi istnieć):
var defElementu = session.ExecuteConfig(s =>
    /* pobranie DefinicjaElementuOceny z modułu Oceny */ default);

using (var t = session.Logout(editMode: true))
{
    var cel = new CelOkresowyPracownika(pracownik);
    hr2.CeleOkresowePrac.AddRow(cel);
    cel.Nazwa = "Wdrożenie nowego modułu";
    cel.Data = Date.Today;
    cel.Termin = new Date(2026, 12, 31);
    cel.Definicja = session.Get(defElementu);
    t.Commit();
}
session.Save();

// Odczyt celów okresowych:
foreach (CelOkresowyPracownika c in pracownik.CeleOkresowe)
{
    // c.Nazwa, c.Termin, c.Wartosc.Punktacja
}
```

**Snippet (utworzenie arkusza oceny i dodanie elementu):**

```csharp
using (var t = session.Logout(editMode: true))
{
    var ocena = new OcenaPracownika(pracownik);
    session.GetHR().OcenyPracownikow.AddRow(ocena);
    ocena.Nazwa = "Ocena roczna 2026";
    ocena.Data = Date.Today;

    var el = session.AddRow(new ElementOcenyPracownika(ocena));  // ocena jako IOcenaPracownika
    el.Definicja = defElementu;   // wymagana (z HRModule.DefElemOcenPrac); Typ/Data są wyliczane (read-only)
    el.Wartosc = 4m;              // Wartosc to decimal
    t.Commit();
}
session.Save();
```

**Pułapki:**
- Cele/elementy ocen wymagają **referencji do definicji konfiguracyjnych** (`DefElementuOcenyPracownika`,
  `Soneta.Oceny.DefinicjaElementuOceny`) — bez nich scenariusz wymaga uprzedniej konfiguracji modułu
  Oceny/HR/HR2. W bazie Demo te definicje **mogą nie istnieć** — najpierw sprawdź dostępność.
- Karty opisu stanowiska / kompetencji nie mają prostego ctora — twórz je workerami kopiującymi
  (`KopiujKartę…Worker`) z definicji stanowiska lub poprzedniej karty.
- `ElementOcenyPracownika.Ocena` to `IOcenaPracownika` — może to być arkusz oceny **lub etap
  rekrutacji** (`EtapRekrutacji` także implementuje `IOcenaPracownika`, patrz KADRY-K9).
- `CelOkresowyPracownika` **nie ma pola `Wartosc`** — postęp/wynik celu reprezentują `Realizacja`/`Realizacje`
  (`RealizacjaCelu`). Liczbową wartość ma `ElementOcenyPracownika.Wartosc` (`decimal`).
- `ElementOcenyPracownika`: `Typ`/`Data` są **read-only** (wyliczane z definicji), a do tabeli dodawaj przez
  `session.AddRow(...)` — `SubTable<ElementOcenyPracownika>` nie ma metody `AddRow`.
- Workery oceniania uruchamiane „jak z UI" → transakcja + `CommitUI()` + `Save()`.

### KADRY-K9 — Rekrutacja: wakaty, ogłoszenia, aplikacje, etapy, stan zatrudnienia

**Cel:** prowadzić proces rekrutacji — wakaty (zapotrzebowanie), oferty/ogłoszenia o pracę, aplikacje
kandydatów oraz etapy rekrutacji z ocenami, aż do zatrudnienia kandydata. Funkcjonalność należy do
modułów **HR2** (`session.GetHR2()`, `RekrutAplikacje`, `RekrutWakaty`) i **HR**
(`session.GetHR()`, `Rekrutacje`, `EtapyRekrutacji`).

**Publiczny kontrakt — kolekcje na `Pracownik` (kandydat jest reprezentowany rekordem `Pracownik`):**

| Kolekcja | Typ elementu | Zawartość |
|---|---|---|
| `Aplikacje` | `SubTable<Soneta.HR2.RekrutacjaAplikacja>` | aplikacje kandydata |
| `Wakaty` | `SubTable<Soneta.HR2.RekrutacjaWakat>` | wakaty |
| `Rekrutacje` / `Kandydatury` | `SubTable<Soneta.HR.Rekrutacja>` | rekrutacje (kandydatury) |
| `EtapyRekrutacji` | `SubTable<Soneta.HR.EtapRekrutacji>` | etapy rekrutacji |

**`RekrutacjaAplikacja` (tabela `RekrutAplikacje`, root; impl. `IŹródłoRekrutacji`) — pola:**

| Pole | Typ | Uwaga |
|---|---|---|
| `Pracownik` | `Soneta.Kadry.Pracownik` | kandydat |
| `Stanowisko` | `Soneta.HR.DefinicjaStanowiska` | **referencja konfiguracyjna** stanowiska |
| `Wydzial` | `Soneta.Kadry.Wydzial` | jednostka organizacyjna |
| `Oferta` | `Soneta.HR2.OfertaPracy` | oferta, na podstawie której wpłynęła aplikacja |
| `Stan` | `Soneta.HR2.StanAplikacji` | `Wprowadzona`, `Zakończona`, `Anulowana` |
| `Data` | `Soneta.Types.Date` | data aplikacji |
| `PlanowanaDataZatrudnienia` | `Soneta.Types.Date` | — |

Ctor: `new RekrutacjaAplikacja(Pracownik pracownik, Soneta.HR.WydziałDefinicjiStanowiska stanowisko)`.
`WydziałDefinicjiStanowiska` jest w module **`Soneta.HR`** (NIE `Soneta.HR2`) i ma ctor
`new WydziałDefinicjiStanowiska(DefinicjaStanowiska definicjaStanowiska)` — definicję pobierz
z `session.GetHR().DefStanowisk`. `RekrutacjaAplikacja.Stanowisko` zwraca tę `DefinicjaStanowiska`.

**`EtapRekrutacji` (tabela `EtapyRekrutacji`, root; impl. `IOcenaPracownika`) — pola:**

| Pole | Typ | Uwaga |
|---|---|---|
| `Rekrutacja` | `Soneta.HR.Rekrutacja` | rekrutacja nadrzędna |
| `Definicja` | `Soneta.HR.DefinicjaEtapuRekrutacji` | **referencja konfiguracyjna** |
| `Lp` | `int` | numer etapu |
| `Data`, `Termin` | `Soneta.Types.Date` | — |
| `Odpowiedzialny` | `Soneta.Oceny.IOceniający` | osoba odpowiedzialna |
| `Opis` | `Soneta.Business.MemoText` | — |
| `ElementyOceny` | `SubTable<ElementOcenyPracownika>` | oceny etapu (etap jest `IOcenaPracownika`) |

Ctor: `new EtapRekrutacji(Rekrutacja rekrutacja)`.

**`Rekrutacja` (tabela; impl. `IOcenaPracownika`) — ctory:**
`new Rekrutacja(Pracownik pracownik)`, `new Rekrutacja(Pracownik pracownik, IŹródłoRekrutacji źródło)`.

**`RekrutacjaWakat` (tabela `RekrutWakaty`, root) — ctory:**
`new RekrutacjaWakat(WydziałDefinicjiStanowiska stanowisko)`,
`new RekrutacjaWakat(DefinicjaStanowiska definicjaStanowiska, Wydzial wydzial)`.

**`OfertaPracy` (tabela; ogłoszenie o pracę) — ctory:**
`new OfertaPracy(WydziałDefinicjiStanowiska stanowisko)`, `new OfertaPracy(RekrutacjaWakat wakat)`.

**Workery rekrutacji (klasy zagnieżdżone):**

| Worker | Metoda | Parametry |
|---|---|---|
| `Soneta.HR2.RekrutacjaAplikacja.NowaRekrutacjaWorker` | rozpoczęcie rekrutacji z aplikacji | `Aplikacje: RekrutacjaAplikacja[]` |
| `Soneta.HR2.RekrutacjaWakat.NowaRekrutacjaWorker` | rozpoczęcie rekrutacji z wakatu | `Wakat: RekrutacjaWakat`, `Pracownicy: Pracownik[]` |
| `Soneta.HR2.OfertaPracy.NowaRekrutacjaWorker` | rozpoczęcie rekrutacji z oferty | `Oferta: OfertaPracy`, `Pracownicy: Pracownik[]` |
| `Soneta.HR.OcenaKandydatowWorker` | `Oceń()` | `Pars`, `Elementy: Rekrutacja[]`; ctor `(Context)` |
| `Soneta.HR.Rekrutacja.ZatrudnijWorker` | `PracHistoria Zatrudnij()` | `Pars`, `Rekrutacja: Rekrutacja` — tworzy zatrudnienie (zapis historii) |

**Snippet (dodanie aplikacji kandydata — wymaga def. stanowiska w bazie):**

```csharp
var hr2 = session.GetHR2();
var kandydat = session.GetKadry().Pracownicy.WgKodu["006"];

// Definicja stanowiska z konfiguracji HR (musi istnieć w session.GetHR().DefStanowisk):
var defStanowiska = session.GetHR().DefStanowisk
    .Cast<Soneta.HR.DefinicjaStanowiska>().FirstOrDefault();
var wydzialDefStanowiska = new Soneta.HR.WydziałDefinicjiStanowiska(defStanowiska);

using (var t = session.Logout(editMode: true))
{
    var aplikacja = session.AddRow(new RekrutacjaAplikacja(kandydat, wydzialDefStanowiska));
    aplikacja.Data = Date.Today;
    aplikacja.Stan = StanAplikacji.Wprowadzona;
    t.Commit();
}
session.Save();

// Odczyt aplikacji kandydata:
foreach (RekrutacjaAplikacja a in kandydat.Aplikacje)
{
    // a.Stanowisko, a.Stan, a.Data, a.Oferta
}
```

**Pułapki:**
- Cały proces rekrutacji wymaga **konfiguracji HR/HR2** (`DefinicjaStanowiska`,
  `DefinicjaEtapuRekrutacji`, `WydziałDefinicjiStanowiska`). W bazie Demo te definicje **mogą nie
  istnieć** — przed scenariuszem sprawdź dostępność, inaczej `Save()` rzuci wyjątek weryfikatora.
- `RekrutacjaAplikacja` przyjmuje w ctorze `WydziałDefinicjiStanowiska`, nie samą `DefinicjaStanowiska`
  (wydział definicji powstaje z `new WydziałDefinicjiStanowiska(definicjaStanowiska)`).
- `EtapRekrutacji` i `Rekrutacja` implementują `IOcenaPracownika` — oceny etapów trzyma
  `EtapRekrutacji.ElementyOceny` (te same `ElementOcenyPracownika` co w KADRY-K8).
- `new Rekrutacja(pracownik)` ustawia pole `Pracownik` i dodaje rekord do **roota** `HRModule.Rekrutacje`
  (oraz `EtapRekrutacji` do `HRModule.EtapyRekrutacji`). Kolekcje na `Pracownik` (`Rekrutacje`/`Kandydatury`/
  `EtapyRekrutacji`) to `ChildTable` wiązane przez relacje — do weryfikacji w teście pewniejszy jest root
  `session.GetHR().Rekrutacje` niż `pracownik.Rekrutacje` (zależnie od relacji może być pusta dla samego `Pracownik`).
- Zatrudnienie kandydata realizuje `Rekrutacja.ZatrudnijWorker.Zatrudnij()` (zwraca `PracHistoria`) —
  spina rekrutację z zatrudnieniem (sekcja A). Worker modyfikuje dane → transakcja + `CommitUI()` + `Save()`.
- `Stan` aplikacji (`Wprowadzona`/`Zakończona`/`Anulowana`) steruje cyklem życia — nie usuwaj aplikacji
  z historią, oznaczaj `Anulowana`.
