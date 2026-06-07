# KADRY08 — Płace — naliczanie wypłat

> Wspólne fakty o typie, podstawowe typy i szablon wzorca: [../kadry.md](../kadry.md).

> **Model danych.** `Wyplata` (`Soneta.Place.Wyplata`) jest klasą **abstrakcyjną**, root `GuidedRow`,
> tabela `Wyplaty`. Konkretne typy: `WyplataEtat` (etat), `WyplataUmowa` (umowy), `WyplataInne`
> (pozostałe). Każda wypłata należy do jednej **listy płac** (`ListaPlac`, tabela `ListyPlac`) i do
> jednego pracownika. Składniki wynagrodzenia to **elementy** (`WypElement`, tabela `WypElementy`,
> root guided) w kolekcji `Wyplata.Elementy: SubTable<WypElement>`.
>
> **Naliczanie** realizuje publiczny worker `Soneta.Place.NaliczanieSeryjne` (klasa abstrakcyjna
> `partial`) z zagnieżdżonymi klasami:
> - parametry: `NaliczanieSeryjne.Params` (bazowa), `NaliczanieSeryjne.PracownikParams : Params`
>   (etat + pozostałe), `NaliczanieSeryjne.UmowaParams : Params` (umowy);
> - wykonawcy: `NaliczanieSeryjne.Pracownika : NaliczanieSeryjne` (wypłaty pracownika),
>   `NaliczanieSeryjne.Umowy : NaliczanieSeryjne` (wypłaty z umów).
>
> Wynik to obiekt `Soneta.Place.NaliczanieWypłat` z kolekcją `WszystkieWypłaty: IList` (elementy są
> typu `Wyplata`). **Naliczanie samo zatwierdza zmiany w sesji** (`Nalicz()` wewnętrznie otwiera i
> commituje transakcję edycyjną na sesji pracownika) — utrwalenie w bazie wymaga osobnego
> `session.Save()`.

### KADRY-H1 — Naliczanie wypłat etatowych (★)

**Cel:** naliczyć wypłatę etatową (wynagrodzenie zasadnicze etatu + dodatki/potrącenia) dla jednego
pracownika za wskazany okres rozliczeniowy.

**Klasy, pola i typy:**

| Element | Typ / sygnatura | Uwaga |
|---|---|---|
| Parametry | `new NaliczanieSeryjne.PracownikParams(Context)` | ctor przyjmuje `Context` (sesja operacyjna) |
| Data wypłaty | `PracownikParams.DataWypłaty: Date` | ustawienie **automatycznie** wylicza `Okres` (z konfiguracji listy) i `MiesiącDeklaracji` |
| Data listy | `PracownikParams.DataListy: Date` | data dokumentu listy płac |
| Okres naliczania | `PracownikParams.Okres: FromTo` | zwykle wyliczony z `DataWypłaty`; można nadpisać |
| Typ naliczenia | `PracownikParams.Naliczanie: TypNaliczenia` | `PłatnaZGóry`/`PłatnaZDołu`; **domyślnie `PłatnaZDołu`** — patrz Pułapki (licencja) |
| Filtr typu wypłaty | `PracownikParams.TypWypłaty: TypWyplaty` | `Wszystkie`/`Etat`/`Umowa`/`Inne` — dla etatu `Etat` lub `Wszystkie` |
| Wykonawca | `new NaliczanieSeryjne.Pracownika(PracownikParams)` | |
| Pracownik | `NaliczanieSeryjne.Pracownika.Pracownik: Pracownik` | komu naliczamy (z tej samej sesji co `Context`) |
| Uruchomienie | `NaliczanieSeryjne.Pracownika.Nalicz(): NaliczanieWypłat` | nalicza i zatwierdza w sesji |
| Wynik | `NaliczanieWypłat.WszystkieWypłaty: IList` (elementy `Wyplata`) | naliczone wypłaty |
| Błędy naliczania | `NaliczanieWypłat.Nienaliczeni: IEnumerable<BłądNaliczaniaWynagrodzenia>` | pracownicy, dla których się nie udało |

`TypNaliczenia` (`Soneta.Place`): `PłatnaZGóry = 1`, `PłatnaZDołu = 2`.
`TypWyplaty` (`Soneta.Place`): `Wszystkie = 0`, `Etat = 1`, `Umowa = 2`, `Inne = 3`.

**Snippet:**

```csharp
var place = session.GetPlace();
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];

// Parametry naliczania — Context z tej samej sesji co pracownik:
var pars = new NaliczanieSeryjne.PracownikParams(context);
pars.DataWypłaty = new Date(2024, 6, 28);    // ustawia Okres i MiesiącDeklaracji automatycznie
pars.DataListy   = pars.DataWypłaty;
// pars.Naliczanie pozostaje domyślnie PłatnaZDołu (nie ustawiamy — patrz Pułapki)
pars.TypWypłaty  = TypWyplaty.Etat;          // tylko wypłaty etatowe

var naliczanie = new NaliczanieSeryjne.Pracownika(pars) { Pracownik = pracownik };
NaliczanieWypłat wynik = naliczanie.Nalicz();   // nalicza + commit w sesji

foreach (Wyplata w in wynik.WszystkieWypłaty)
{
    // w.Pracownik, w.ListaPlac, w.Data, w.MiesiacDeklaracji, w.Wartosc (Currency, do wypłaty)
}

session.Save();   // utrwalenie w bazie (opcjonalne — bez tego zmiany żyją tylko w sesji)
```

**Pułapki:**
- **`Context` musi pochodzić z tej samej sesji co pracownik.** `PracownikParams(Context)` wiąże się z
  `Context.Session`; pracownik pobrany z innej sesji spowoduje niespójność.
- **Nie ustawiaj `Naliczanie` jawnie, jeśli nie masz pewności co do licencji.** Setter
  `Params.Naliczanie` rzuca wyjątek, gdy licencja nie jest „PL Złoty/Platynowy" — getter wtedy i tak
  zwraca `PłatnaZDołu`. Pozostawienie wartości domyślnej (`PłatnaZDołu`) jest bezpieczne.
- `Nalicz()` **otwiera własną transakcję** na sesji pracownika i commituje ją — **nie owijaj** wywołania
  w dodatkowy `session.Logout(true)`. Po naliczeniu zmiany są w sesji; do bazy idą dopiero w `Save()`.
- `WszystkieWypłaty` to `IList` nietypowana — iteruj jako `foreach (Wyplata w in ...)`.
- Pracownik w archiwum (`Pracownik.ArchiwumInfo == InformacjeOArchiwum.WArchiwum`) jest pomijany —
  `WszystkieWypłaty` będzie puste, bez wyjątku.
- Naliczanie to operacja na danych operacyjnych — sprawdź `wynik.Nienaliczeni` zamiast łapać ogólny
  wyjątek; przy `KontynacjaNaliczenia` (tryb seryjny) błędy lądują tam, a nie w `throw`.

### KADRY-H2 — Naliczanie wypłat z umów (★)

**Cel:** naliczyć wypłatę z konkretnej umowy cywilnoprawnej (`Soneta.Kadry.Umowa`).

**Klasy, pola i typy:**

| Element | Typ / sygnatura | Uwaga |
|---|---|---|
| Parametry | `new NaliczanieSeryjne.UmowaParams(Context)` | jak `PracownikParams`, ale `Naliczanie` jest na sztywno `PłatnaZDołu` (setter rzuca `NotSupportedException`) |
| Data wypłaty / listy / okres | `UmowaParams.DataWypłaty`, `.DataListy`, `.Okres` | jak w KADRY-H1 |
| Wykonawca | `new NaliczanieSeryjne.Umowy(UmowaParams)` | w ctorze ustawia `TypWypłaty = Umowa` |
| Umowa | `NaliczanieSeryjne.Umowy.Umowa: Umowa` | ustawienie umowy ustawia też `Pracownik` z `umowa.Pracownik` |
| Uruchomienie | `NaliczanieSeryjne.Umowy.Nalicz(): NaliczanieWypłat` | |

**Snippet:**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
Umowa umowa = pracownik.Umowy.Cast<Umowa>().First();   // przykładowa umowa pracownika

var pars = new NaliczanieSeryjne.UmowaParams(context);
pars.DataWypłaty = new Date(2024, 6, 28);
pars.DataListy   = pars.DataWypłaty;

var naliczanie = new NaliczanieSeryjne.Umowy(pars) { Umowa = umowa };
NaliczanieWypłat wynik = naliczanie.Nalicz();

foreach (Wyplata w in wynik.WszystkieWypłaty)
{
    // w.Typ == TypWyplaty.Umowa; w.Wartosc; w.Elementy
}
session.Save();
```

**Pułapki:**
- **Nie ustawiaj `UmowaParams.Naliczanie`** — setter rzuca `NotSupportedException` (umowy zawsze
  „płatne z dołu").
- Ustawienie `Umowy.Umowa` nadpisuje `Pracownik` na właściciela umowy — nie ustawiaj `Pracownik` ręcznie.
- Pozostałe pułapki jak w KADRY-H1 (Context z tej samej sesji, własna transakcja w `Nalicz()`, `Save()`).

### KADRY-H3 — Naliczanie pozostałych wypłat (★)

**Cel:** naliczyć wypłaty „pozostałe" — pojedynczy dodatek/potrącenie (np. premia, zasiłek
jednorazowy) poza zasadniczym wynagrodzeniem etatu, bądź wypłaty typu `Inne`.

**Mechanizm:** używamy tego samego wykonawcy co KADRY-H1 — `NaliczanieSeryjne.Pracownika` — sterując
zakresem przez `PracownikParams`:
- `PracownikParams.TypWypłaty = TypWyplaty.Inne` — naliczanie tylko składników typu „inne",
- `PracownikParams.Dodatek: DefinicjaElementu` — **zawężenie do jednej definicji** dodatku/potrącenia
  (naliczany jest tylko wskazany składnik).

**Pola i typy:**

| Element | Typ / sygnatura | Uwaga |
|---|---|---|
| Filtr typu | `PracownikParams.TypWypłaty: TypWyplaty` | `Inne` — pozostałe; `Wszystkie` — łącznie z etatem |
| Pojedynczy składnik | `PracownikParams.Dodatek: DefinicjaElementu` | definicja konkretnego dodatku/potrącenia; `null` = bez zawężenia |

**Snippet:**

```csharp
var place = session.GetPlace();
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];

// Definicja konkretnego dodatku/potrącenia (rekord konfiguracyjny):
DefinicjaElementu defDodatku = place.DefElementow.WgKodu["PREMIA"];   // przykładowy kod

var pars = new NaliczanieSeryjne.PracownikParams(context);
pars.DataWypłaty = new Date(2024, 6, 28);
pars.DataListy   = pars.DataWypłaty;
pars.TypWypłaty  = TypWyplaty.Inne;     // pozostałe wypłaty
pars.Dodatek     = defDodatku;          // tylko ten składnik

var naliczanie = new NaliczanieSeryjne.Pracownika(pars) { Pracownik = pracownik };
NaliczanieWypłat wynik = naliczanie.Nalicz();

foreach (Wyplata w in wynik.WszystkieWypłaty)
{
    foreach (WypElement e in w.Elementy)
    {
        // e.Definicja, e.Nazwa, e.Wartosc (decimal), e.Okres
    }
}
session.Save();
```

**Pułapki:**
- `Dodatek` to rekord **konfiguracyjny** `DefinicjaElementu` — pobierz istniejącą definicję
  (np. przez klucz kodu w `place.DefElementow`), nie twórz „w locie".
- `TypWyplaty.Inne` i `TypWyplaty.Etat` są rozłączne — by naliczyć etat + dodatki łącznie użyj
  `Wszystkie`.
- Pozostałe pułapki jak w KADRY-H1.

### KADRY-H4 — Przeglądanie/odczyt wypłat za rok (★)

**Cel:** odczytać naliczone wypłaty pracownika za dany rok i zagregować wartości (suma do wypłaty,
brutto/netto/składki/podatek, sumy składników).

**Dostęp do wypłat (publiczny kontrakt):**

| Punkt wejścia | Typ | Uwaga |
|---|---|---|
| `pracownik.Wyplaty` | `SubTable<Wyplata>` | wszystkie wypłaty pracownika (klucz `WgPracownik`) |
| `session.GetPlace().Wyplaty.WgPracownik[pracownik]` | `SubTable<Wyplata>` | równoważnie z modułu |
| `session.GetPlace().Wyplaty.WgData[date]` | `SubTable<Wyplata>` | wypłaty z datą `date` |
| `listaPlac.Wyplaty` | `SubTable<Wyplata>` | wypłaty danej listy płac |

**Pola wypłaty (`Wyplata`) do odczytu:**

| Pole | Typ | Opis |
|---|---|---|
| `Pracownik` | `Pracownik` | właściciel |
| `ListaPlac` | `ListaPlac` | lista płac (`ListaPlac.Okres: FromTo`, `ListaPlac.DataWyplaty: Date`, `ListaPlac.Zatwierdzona: bool`) |
| `Data` | `Date` | data wypłaty (klucz `WgData`) |
| `MiesiacDeklaracji` | `YearMonth` | miesiąc rozliczenia PIT |
| `MiesiacZUS` | `YearMonth` | miesiąc rozliczenia ZUS |
| `Wartosc` | `Currency` | kwota **do wypłaty** (netto) w PLN |
| `Numer` | `NumerDokumentu` | numer dokumentu (`Numer.NumerPelny`) |
| `Typ` | `TypWyplaty` | etat / umowa / inne |
| `Bufor` | `bool` | wypłata w buforze (niezatwierdzona) |
| `Elementy` | `SubTable<WypElement>` | składniki wynagrodzenia |

**Kwoty na poziomie wypłaty (`Soneta.Place.Wyplata`, typ `Soneta.Types.Currency`):** `Wartosc`
(kwota **do wypłaty**, PLN), `WartoscCy` (w walucie listy), `DoWypłaty`, `Gotówka`, `Inne`.
Aby otrzymać `decimal`, użyj **`.Value`** (`w.Wartosc.Value`) — `Currency` nie ma jawnego rzutowania
na `decimal`.

> **Uwaga:** `Wyplata`/`WyplataEtat` **nie udostępnia** publicznych agregatów typu `Brutto`, `Netto`,
> `SkładkiZUS`, `Podatek` jako gotowych właściwości. Brutto/netto/składki/podatek **liczymy sumując
> składniki** z kolekcji `Wyplata.Elementy` (`WypElement.Wartosc`, `WypElement.Netto`, `WypElement.Podatki.*`).

**Składniki (`WypElement`) i ich struktura podatkowo-składkowa:**

| Pole | Typ | Opis |
|---|---|---|
| `Definicja` | `DefinicjaElementu` | definicja składnika |
| `Nazwa` | `string` | nazwa składnika |
| `Wartosc` | `decimal` | wartość składnika |
| `Okres` | `FromTo` | okres, za który naliczono |
| `Podatki` | `Podatki` (subrow) | struktura podatków/składek |
| `Podatki.PodstawaZUS` | `decimal` | podstawa ZUS |
| `Podatki.Emerytalna` / `Rentowa` / `Chorobowa` / `Wypadkowa` / `Zdrowotna` | `SkladkaZUS` (subrow) | każda z polami `Podstawa`, `Prac`, `Firma: decimal` |
| `Podatki.Koszty`, `Podatki.Ulga`, `Podatki.ZalFIS` | `decimal` | koszty, ulga, zaliczka PIT |

**Snippet:**

```csharp
var place = session.GetPlace();
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];

int rok = 2024;
var od = new Date(rok, 1, 1);
var doD = new Date(rok, 12, 31);

// Filtr serwerowy po dacie wypłaty (zakres roku) — bez pełnego skanu:
decimal sumaDoWypłaty = 0m;
decimal sumaBrutto    = 0m;

foreach (Wyplata w in pracownik.Wyplaty[(Wyplata x) => x.Data >= od && x.Data <= doD])
{
    sumaDoWypłaty += w.Wartosc.Value;       // kwota do wypłaty (Currency -> decimal przez .Value)

    // brutto/składki/podatek liczymy z elementów (nie ma gotowych agregatów na wypłacie):
    foreach (WypElement e in w.Elementy)
    {
        sumaBrutto += e.Wartosc;            // WypElement.Wartosc to decimal
        decimal netto       = e.Netto;
        decimal podstawaZUS = e.Podatki.PodstawaZUS;
        decimal zaliczkaPit = e.Podatki.ZalFIS;
    }
}
```

**Pułapki:**
- `Wyplaty` to tabela **operacyjna guided** — zawsze ograniczaj zakresem czasowym (rok), nie iteruj
  całości (`safe-code §6.3`). Filtruj serwerowo przez `SubTable[condition]` po `Data`, nie w pamięci.
- `Wartosc` to `Currency` (kwota do wypłaty); konwersja na `decimal` przez `.Value`. Składnik
  `WypElement.Wartosc`/`WypElement.Netto` to już `decimal` — nie myl typów ani znaczeń.
- **Nie ma** gotowych właściwości agregujących (`Brutto`/`Netto`/`SkładkiZUS`/`Podatek`) na `Wyplata`
  ani `WyplataEtat` — sumuj składniki z `Wyplata.Elementy` (i ich `Podatki.*`).
- `SkladkaZUS` ma pola `Podstawa`, `Prac`, `Firma` (część pracownika i pracodawcy) oraz właściwość
  pomocniczą `Składka` (suma) — wybierz właściwą do potrzeb.
- Filtruj po `Data` (data wypłaty) lub `MiesiacDeklaracji`/`MiesiacZUS` zależnie od potrzeby
  raportowej — to różne pojęcia roku (rok wypłaty vs rok deklaracji).

### KADRY-H5 — Odczyt elementów wypłaty (brutto/składki/podatek/netto) (★)

**Cel:** odczytać składniki konkretnej **naliczonej** wypłaty (`Soneta.Place.Wyplata`) i wyliczyć
agregaty: brutto, składki ZUS (część pracownika i firmy), zaliczka PIT, netto.

**Model.** Składniki to `Wyplata.Elementy: SubTable<WypElement>` (`Soneta.Place.WypElement`, tabela
operacyjna guided `WypElementy`). `Wyplata` **nie** ma gotowych agregatów `Brutto`/`Netto`/`SkładkiZUS`/
`Podatek` — liczymy je z elementów albo przez worker `Wyplata.PITInfoWorker` (patrz niżej).

**Pola składnika `WypElement` (do odczytu):**

| Pole | Typ | Opis |
|---|---|---|
| `Definicja` | `DefinicjaElementu` | definicja składnika (konfiguracja) |
| `Nazwa` | `string` | nazwa składnika |
| `Wartosc` | `decimal` | wartość brutto składnika (kwota elementu) |
| `Netto` | `decimal` | wartość netto składnika |
| `DoWypłaty` | `decimal` | kwota do wypłaty z tego składnika |
| `Okres` | `FromTo` | okres, za który naliczono |
| `MiesiacDeklaracji` | `YearMonth` | miesiąc rozliczenia PIT |
| `MiesiacZUS` | `YearMonth` | miesiąc rozliczenia ZUS |
| `Podatki` | `Podatki` (subrow) | struktura podatkowo-składkowa |

**Subrow `WypElement.Podatki` (`Soneta.Place.Podatki`) — pola istotne:**

| Pole | Typ | Opis |
|---|---|---|
| `PodstawaZUS` | `decimal` | podstawa wymiaru składek ZUS |
| `Emerytalna` / `Rentowa` / `Chorobowa` / `Wypadkowa` / `Zdrowotna` | `SkladkaZUS` (subrow) | każda z polami `Podstawa`, `Prac`, `Firma: decimal` oraz wyliczanym `Składka` (suma) |
| `Koszty` | `decimal` | koszty uzyskania przychodu |
| `Ulga` | `decimal` | ulga podatkowa (kwota wolna) |
| `ZalFIS` | `decimal` | zaliczka na podatek dochodowy (fiskus) |
| `ZdrowotneDoOdliczenia` | `decimal` | składka zdrowotna do odliczenia |

Subrow `SkladkaZUS` (`Soneta.Place.SkladkaZUS`): `Podstawa` (podstawa), `Prac` (część pracownika,
`decimal`), `Firma` (część pracodawcy, `decimal`), wyliczane `Składka` (suma) i `JestMinus` (`bool`).

**Worker-agregator `Wyplata.PITInfoWorker`** (klasa publiczna, `[Context] Wypłata`) — udostępnia gotowe
sumy podatkowe dla wypłaty:

| Właściwość | Typ | Opis |
|---|---|---|
| `DoOpodatkowania` | `Currency` | suma elementów opodatkowanych (brutto opodatkowane) |
| `Nieopodatkowane` | `Currency` | suma elementów nieopodatkowanych |
| `Razem` | `decimal` | opodatkowane + nieopodatkowane (przychód razem) |
| `NettoRazem` | `decimal` | wynagrodzenie netto razem |
| `NettoOpodat` | `Currency` | netto opodatkowane |
| `SkładkiZUS` | `decimal` | suma składek ZUS pracownika |
| `SkładkaZdrow` | `decimal` | składka zdrowotna |
| `Koszty` | `decimal` | koszty uzyskania razem |
| `Ulga` | `decimal` | ulga podatkowa |
| `ZalFIS` | `decimal` | zaliczka PIT |
| `Dochód_Bez26` / `Dochód_26` | `decimal` | dochód (z podziałem na ulgę „do 26 lat") |

> `PITInfoWorker.Brutto` i `PITInfoWorker.Netto` są oznaczone `[Obsolete]` — używaj `DoOpodatkowania`,
> `Nieopodatkowane`, `Razem`, `NettoRazem`. Worker przyjmuje też kolekcję `Elementy: IEnumerable`
> (zamiast `Wypłata`) i `WykluczoneElementy: DefinicjaElementu[]`.

**Snippet (agregacja ręczna z elementów):**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];

decimal brutto = 0m, netto = 0m, zusPrac = 0m, zusFirma = 0m, zalPit = 0m;

// jedna konkretna wypłata pracownika (np. ostatnia z czerwca):
var od  = new Date(2024, 6, 1);
var doD = new Date(2024, 6, 30);
Wyplata wyplata = pracownik.Wyplaty[(Wyplata x) => x.Data >= od && x.Data <= doD].Cast<Wyplata>().First();

foreach (WypElement e in wyplata.Elementy)
{
    brutto += e.Wartosc;                 // WypElement.Wartosc to decimal
    netto  += e.Netto;
    zalPit += e.Podatki.ZalFIS;

    zusPrac  += e.Podatki.Emerytalna.Prac  + e.Podatki.Rentowa.Prac
              + e.Podatki.Chorobowa.Prac    + e.Podatki.Zdrowotna.Prac;
    zusFirma += e.Podatki.Emerytalna.Firma + e.Podatki.Rentowa.Firma
              + e.Podatki.Wypadkowa.Firma;
}

decimal doWyplaty = wyplata.Wartosc.Value;   // Currency -> decimal przez .Value
```

**Snippet (przez worker — gotowe agregaty):**

```csharp
var pit = new Wyplata.PITInfoWorker { Wypłata = wyplata };
decimal brutto  = pit.Razem;          // przychód razem
decimal nettoR  = pit.NettoRazem;
decimal zus     = pit.SkładkiZUS;
decimal zdrow   = pit.SkładkaZdrow;
decimal zaliczka = pit.ZalFIS;
```

**Pułapki:**
- `WypElement.Wartosc`/`Netto`/`DoWypłaty` to `decimal`; `Wyplata.Wartosc` (do wypłaty) to `Currency` —
  konwersja przez `.Value` (§10.1).
- `SkladkaZUS.Prac` to część pracownika, `SkladkaZUS.Firma` to część pracodawcy — wybierz właściwą
  zależnie od potrzeby (koszt pracownika vs koszt pracodawcy).
- `Wyplaty`/`WypElementy` to tabele operacyjne guided — pobieraj zakresem czasowym (§6.3), nie iteruj
  całości.
- Pomijaj elementy stornowane przy sumowaniu, jeśli liczysz stan bieżący — patrz `WypElement.RozliczenieStorna`
  (KADRY-H10); naliczona wypłata po korekcie zawiera zarówno element pierwotny (`Wystornowany`) jak i `Stornujący`.

---

### KADRY-H6 — Wypłata zaliczki / dołączenie zaliczki (★)

**Cel:** naliczyć i wypłacić zaliczkę (wypłata środków „na poczet" przyszłego wynagrodzenia), tworząc
dokument `Soneta.Place.Zaliczka` i element realizacji zaliczki na wypłacie.

**Model.** Zaliczka to rekord operacyjny `Soneta.Place.Zaliczka` (root guided, tabela `Zaliczki`,
`session.GetPlace().Zaliczki`), implementuje `IBazaZrodlaWyplaty` i `IPowiązanieWypłaty`. Element
realizujący zaliczkę to `WypElementZaliczka.Realizacja : WypElementZaliczka : WypElement`, spłata to
`WypElementZaliczka.Spłata`. Powiązanie elementu z zaliczką: `WypElement.BazaZrodla = Zaliczka`,
`RodzajŹródłaWypłaty.Zaliczka`.

**Ścieżka publiczna — worker `Soneta.Place.WypłaćZaliczkęWorker`** (na `Soneta.Kadry.Pracownicy`):

| Element | Typ / sygnatura | Uwaga |
|---|---|---|
| Parametry | `WypłaćZaliczkęWorker.ZalParams : WypElement.Params` | ctor `(Context)`; `Rodzaj == RodzajŹródłaWypłaty.Zaliczka` |
| Definicja | `ZalParams.Definicja: DefinicjaElementu` | definicja elementu zaliczki (z `WypElement.Params`); **musi mieć** `DefinicjaElementu.RodzajZrodla == RodzajŹródłaWypłaty.Zaliczka` — inaczej worker rzuca `WypElement.RodzajDefinicjiException` (np. „Korekta zaliczki podatku" ma `Dodatek`) |
| Data | `ZalParams.Data: Date` | data wypłaty zaliczki (wymagana) |
| Kwota | `ZalParams.Kwota: Currency` | kwota zaliczki (wymagana) |
| Pracownicy | `WypłaćZaliczkęWorker.Pracownicy: Pracownik[]` | dla kogo |
| Akcja | `[Action("Wypłać zaliczkę")] object WypłataZaliczki()` | tworzy `Zaliczka`, nalicza element realizacji |

**Stan zaliczki (`Zaliczka`):** `Wartosc: Currency`, `Splacono: Currency`, `Pozostaje: Currency`
(`= Wartosc - Splacono`), `Stan: StanZaliczki` (`NieSpłacona`/`CzęściowoSpłacona`/`CałkowicieSpłacona`),
`Realizacje: SubTable` (elementy realizacji), `Spłaty: SubTable<WypElement>` (elementy spłaty).

**Mechanizm naliczenia** (realizowany przez worker): dla każdego pracownika tworzony jest
`new Zaliczka(pracownik)`, dodawany przez `Zaliczki.AddRow(zaliczka)`, a następnie niskopoziomowy
obiekt `Soneta.Place.NaliczanieWypłat` z `NaliczŹródłoWypłaty = zaliczka` wykonuje `Nalicz()`.
Dołączenie/spłata zaliczki w kolejnej wypłacie etatowej dzieje się automatycznie podczas zwykłego
naliczania (KADRY-H1) — naliczanie wyszukuje niespłacone zaliczki pracownika i generuje element
`WypElementZaliczka.Spłata`.

**Snippet (uruchomienie workera zaliczki):**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
// definicję zaliczki rozpoznajemy po RodzajZrodla (nie po Kodzie/Nazwie — „Korekta zaliczki podatku"
// to RodzajZrodla.Dodatek, którego worker NIE przyjmie):
DefinicjaElementu defZaliczki = session.GetPlace().DefElementow.Cast<DefinicjaElementu>()
    .First(d => d.RodzajZrodla == RodzajŹródłaWypłaty.Zaliczka);

var pars = new WypłaćZaliczkęWorker.ZalParams(context) {
    Data  = new Date(2024, 6, 15),
    Kwota = new Currency(1000m, Currency.SystemSymbol),
};
pars.Definicja = defZaliczki;

var worker = new WypłaćZaliczkęWorker { Params = pars, Pracownicy = new[] { pracownik } };
object wynik = worker.WypłataZaliczki();   // tworzy Zaliczka + nalicza; otwiera własną transakcję
session.Save();
```

**Pułapki:**
- `ZalParams.Definicja` to **istniejąca** definicja elementu o `RodzajZrodla == RodzajŹródłaWypłaty.Zaliczka` —
  pobierz z `place.DefElementow` (filtruj po `RodzajZrodla`, nie po `Kod`/`Nazwa`), nie twórz w locie.
- Baza Demo może nie zawierać definicji o `RodzajZrodla == Zaliczka` — wtedy worker jest niewykonalny
  (w teście: `Assert.Ignore`).
- `Zaliczka.SetWartość(...)` jest `internal` — wartości nie ustawiaj ręcznie; przekaż `ZalParams.Kwota`
  do workera.
- `Zaliczka` nie kasuje się bezpośrednio, gdy ma realizacje/spłaty (`OnDeleting` rzuca `RowException`).
- Worker otwiera własną transakcję (`Session.Logout(true)` + `CommitUI`) — nie owijaj dodatkowo;
  utrwalenie w bazie przez `Save()`.

---

### KADRY-H7 — Korekta podatków i składek; „Przelicz składki ZUS i podatki" (★)

**Cel:** ponownie przeliczyć (skorygować) składki ZUS i zaliczki PIT na już naliczonych elementach
wypłat pracownika za dany miesiąc deklaracji — np. po zmianie progu, tytułu ubezpieczenia, korekcie
danych kadrowych.

**Worker `Soneta.Place.NaliczaniePodatkówMiesięcznie`** (na `Pracownik`/`PracHistoria`):

| Element | Typ / sygnatura | Uwaga |
|---|---|---|
| Miesiąc | publiczny ctor `(YearMonth miesiącDeklaracji)` (atrybut `[Context(typeof(MiesiącDeklaracji),"Miesiąc")]`) | przy ręcznym wywołaniu przekaż `YearMonth` (np. `pars.Miesiąc`); property odczytu `MiesiącDeklaracji: YearMonth` (get) |
| Klasa parametru | `Soneta.Place.MiesiącDeklaracji : ContextBase` | `MiesiącDeklaracji.Miesiąc: YearMonth` (domyślnie `YearMonth.Today`) |
| Pracownik | `NaliczaniePodatkówMiesięcznie.Pracownik: Pracownik` | `[Context]` |
| `NoTrace` | `bool` | wyłączenie śladu (logu) operacji |
| Akcja | `[Action("Przelicz składki ZUS i podatki")] void PrzeliczPodatki()` | przelicza elementy z danego miesiąca |

**Mechanizm:** worker iteruje elementy (`WypElementy.WgDaty`) wszystkich pracowników powiązanych
(`Pracownik.PracownicyPowiązani`) w okresie `MiesiącDeklaracji.ToFromTo()`, dla niezablokowanych
(`!element.Podatki.Korekta && element.Wyplata.Bufor`) wykonuje przeliczenie flag i naliczenie
podatków (`NaliczaniePodatków.NaliczRozrzuć()`). Wszystko w transakcji `Session.Logout(true)` +
`Commit()`.

**Snippet:**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];

var pars = new MiesiącDeklaracji(context) { Miesiąc = new YearMonth(2024, 6) };
var worker = new NaliczaniePodatkówMiesięcznie(pars.Miesiąc) { Pracownik = pracownik };
worker.PrzeliczPodatki();    // przelicza składki ZUS i zaliczki PIT za czerwiec 2024
session.Save();
```

**Pułapki:**
- Elementy z ręczną korektą podatków (`element.Podatki.Korekta == true`) oraz elementy z wypłat
  zatwierdzonych (`!Wyplata.Bufor`) są **pomijane** — przeliczane są tylko elementy z bufora.
- `MiesiącDeklaracji.Miesiąc` to `YearMonth` — to miesiąc deklaracji, nie data wypłaty.
- Worker przelicza także pracowników powiązanych (`PracownicyPowiązani`) — operacja może objąć więcej
  niż jedną kartotekę.

---

### KADRY-H8 — Rozliczenie pracownika; dochód / roczny dochód (★)

**Cel:** odczytać dochód z naliczonej wypłaty oraz (dla właścicieli) pobrać roczny dochód do rozliczeń;
opcjonalnie uruchomić rozliczenie pracownika.

**A. Dochód z wypłaty — `Wyplata.PITInfoWorker`** (publiczny, jak w KADRY-H5). Dochód podatkowy:

| Właściwość | Typ | Opis |
|---|---|---|
| `Dochód_Bez26` | `decimal` | dochód poza ulgą „do 26 lat" (`= przychód + przychód50 − koszty − koszty50`) |
| `Dochód_26` | `decimal` | dochód objęty ulgą „do 26 lat" |
| `DoOpodatkowania` | `Currency` | podstawa opodatkowania (brutto opodatkowane) |
| `Podstawa` | `decimal` | podstawa naliczenia zaliczki |
| `ZalFIS` | `decimal` | zaliczka PIT |

Dochód roczny pracownika sumuje się iterując wypłaty roku (KADRY-H4/KADRY-H5) i sumując `Dochód_Bez26 + Dochód_26`
(lub `DoOpodatkowania`) z `PITInfoWorker` każdej wypłaty.

**B. „Pobierz roczny dochód" — worker `Soneta.Kadry.PobierzDochodRocznyWorker`** (na `Pracownik`/
`PracHistoria`) — **tylko dla właściciela** (`Pracownik is Wlasciciel`):

| Element | Typ / sygnatura | Uwaga |
|---|---|---|
| Parametry | property `PobierzDochodRocznyWorker.Pars : PobierzDochodRocznyWorker.Params` | `Pars.Rok: int` (domyślnie rok ubiegły) |
| Pracownik | `PobierzDochodRocznyWorker.Pracownik: Pracownik` | `[Context]` |
| Akcja | `[Action("Pobierz roczny dochód")] void Pobierz()` | zapisuje `PrzychodRyczalt` (RoczDochSkala/RoczDochLiniowy/RoczDochRyczalt) za rok |

Korzysta z serwisu `IDochódWłaściciela.KwotaDochoduStraty(pracownik, YearMonth, FormaOpodatkowania)`.

**C. „Rozlicz pracownika" — worker `Soneta.Place.RozliczaniePracownikowWorker`** (na `Pracownik`) —
**tylko dla folderu pracowników zewnętrznych** (`KadryIPlace/Kadry/PracownicyZewnetrzni`):

| Element | Typ / sygnatura | Uwaga |
|---|---|---|
| Parametry | `RozliczeniePracownikowParams : RozliczanieUmowZewnetrznychParams` | `Okres: FromTo`, `Data: Date` |
| Akcja | `[Action("Rozlicz pracownika")] RozliczanieUmowZewnetrznych Rozlicz()` | rozlicza umowy zewnętrzne pracownika |

**Snippet (dochód roczny z wypłat):**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
var od = new Date(2024, 1, 1); var doD = new Date(2024, 12, 31);

decimal dochodRoczny = 0m;
foreach (Wyplata w in pracownik.Wyplaty[(Wyplata x) => x.Data >= od && x.Data <= doD])
{
    var pit = new Wyplata.PITInfoWorker { Wypłata = w };
    dochodRoczny += pit.Dochód_Bez26 + pit.Dochód_26;
}
```

**Pułapki:**
- `PobierzDochodRocznyWorker` działa wyłącznie dla `Wlasciciel` i form opodatkowania ogólnych/ryczałtu —
  dla zwykłego pracownika nie ma zastosowania (zwraca bez efektu).
- „Rozlicz pracownika" (`RozliczaniePracownikowWorker`) dotyczy **pracowników zewnętrznych** (umowy
  zewnętrzne), nie standardowego rozliczenia płacowego.
- Wewnętrzny `Wyplata.RozliczenieManager` (rozliczanie płatności/należności) jest **niepubliczny** —
  rozliczenie płatności inicjuje setter `Wyplata.Bufor` (zejście z bufora), nie wywołuj go bezpośrednio.

---

### KADRY-H9 — Kalkulator wynagrodzeń (brutto↔netto, koszt pracodawcy) (★)

**Cel:** wyliczyć netto z brutto (lub odwrotnie) oraz całkowity koszt pracodawcy.

**Brak dedykowanej publicznej klasy „kalkulatora wynagrodzeń"** w publicznym kontrakcie (patrz sekcja
„niewykonalne"). Wyliczenie realizujemy przez **naliczenie próbne** (KADRY-H1/KADRY-H3 — `NaliczanieSeryjne`) i
odczyt agregatów workera `Wyplata.PITInfoWorker` oraz `Wyplata.KosztyUzyskaniaPrzychoduWorker`.

**Koszt pracodawcy — `Wyplata.PITInfoWorker` + składki firmy z elementów:**
- brutto: `pit.Razem` / `pit.DoOpodatkowania`,
- netto: `pit.NettoRazem`,
- składki pracownika: `pit.SkładkiZUS`, `pit.SkładkaZdrow`,
- zaliczka PIT: `pit.ZalFIS`,
- składki firmy (narzuty pracodawcy): suma `WypElement.Podatki.{Emerytalna,Rentowa,Wypadkowa}.Firma`
  (plus FP/FGŚP/FEP) — patrz `WyplataSkładkiWorker` niżej.

**Agregator składek — `Soneta.Place.WyplataSkładkiWorker`** (publiczny, `[Context] Wypłata` lub
`Elementy: IEnumerable`): udostępnia `Razem: ZestawienieSkładek` z m.in.:

| Właściwość `ZestawienieSkładek` | Typ | Opis |
|---|---|---|
| `KosztyZUS` | `decimal` | składki ZUS pracownika (emer.+rent.+chor.+wyp., część `Prac`) |
| `FirmaZUS` | `decimal` | składki ZUS pracodawcy (część `Firma`) |
| `Narzuty` | `decimal` | narzuty pracodawcy (`FirmaZUS` + FP.Firma + FGSP.Firma + FEP.Firma) |
| `ZUS` | `decimal` | `KosztyZUS + FirmaZUS` |
| `Emerytalna`/`Rentowa`/… | `ISkładka` | pojedyncze składki (`Podstawa`/`Prac`/`Firma`/`Składka`) |

Koszt pracodawcy ≈ brutto (`pit.DoOpodatkowania`/`Razem`) + `skladki.Razem.Narzuty`.

**Snippet (kalkulacja przez naliczenie próbne):**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];

var pars = new NaliczanieSeryjne.PracownikParams(context);
pars.DataWypłaty = new Date(2024, 6, 28);
pars.DataListy   = pars.DataWypłaty;
pars.TypWypłaty  = TypWyplaty.Etat;

var nal = new NaliczanieSeryjne.Pracownika(pars) { Pracownik = pracownik };
NaliczanieWypłat wynik = nal.Nalicz();

Wyplata w = (Wyplata)wynik.WszystkieWypłaty[0];
var pit = new Wyplata.PITInfoWorker { Wypłata = w };
var skl = new WyplataSkładkiWorker { Wypłata = w };

decimal brutto       = pit.Razem;
decimal netto        = pit.NettoRazem;
decimal kosztPracod  = brutto + skl.Razem.Narzuty;   // brutto + narzuty pracodawcy
// (jeśli to tylko kalkulacja — nie wywołuj Save(), wynik istnieje w sesji)
```

**Pułapki:**
- Brak osobnego „kalkulatora" — wynik zawsze powstaje przez naliczenie i workery agregujące.
- Kalkulacja brutto↔netto zależy od pełnej konfiguracji pracownika (etat, ulgi, koszty, PPK) — nie ma
  bezstanowej funkcji „brutto→netto" w publicznym API.
- Jeśli naliczenie ma być tylko próbne, nie wywołuj `Save()` (zmiany zostaną w sesji i znikną z `Dispose`),
  albo wykonaj na osobnej sesji „brudnopisowej".

---

### KADRY-H10 — Stornowanie elementów wypłaty; obsługa elementów stornowanych (★)

**Cel:** zastornować (wycofać/skorygować) element już zatwierdzonej wypłaty i poprawnie odczytać stan
storna.

**Model.** Storno opisuje rekord `Soneta.Place.StornoElementu` (tabela `StornaElementow`). Element
ma stan `WypElement.StanStorna: StanStornaElementu` oraz dostęp do storna `WypElement.Storno: StornoElementu`.

Enum `Soneta.Place.StanStornaElementu`: `NieDotyczy=0`, `DoStornowania=1`, `Wystornowany=2`,
`Stornujący=3`, `WycofaneStorno=10` (tylko wyliczany).
Enum `Soneta.Place.RodzajStornaElementu`: `NieDotyczy=0`, `Anulowanie=1`, `Przeliczenie=2`.

**Pola `WypElement` związane ze storno:**

| Pole | Typ | Opis |
|---|---|---|
| `StanStorna` | `StanStornaElementu` | bieżący stan storna elementu |
| `StanStornaEx` | `StanStornaElementu` | jw. + `WycofaneStorno` gdy `Wystornowany` historyczny |
| `Storno` | `StornoElementu` | powiązany rekord storna (lub `null`) |
| `RozliczenieStorna` | `bool` | `true` gdy `Wystornowany` lub `Stornujący` (element nie liczy się do bieżącego stanu) |
| `Wystornowany` | `bool` | do elementu naliczono element stornujący |
| `Stornowane` / `Stornujące` | `SubTable<StornoElementu>` | relacje storn |
| `Korekta` | `bool` | element zmodyfikowany ręcznie przez operatora |
| `UtwórzStorno()` | `WypElement` | (wirtualna) tworzy element stornujący danego typu |

**Workery oznaczania (publiczne, na `WypElement` / `Wyplata`):**
- `StornoElementu.ElementDoPrzeliczeniaWorker` (na `WypElement`):
  - `[Action("Oznacz element do przeliczenia")] ZaznaczElementDoPrzeliczenia()` — `RodzajStornaElementu.Przeliczenie`,
  - `[Action("Oznacz element do anulowania")] ZaznaczElementDoAnulowania()` — `RodzajStornaElementu.Anulowanie`,
  - `[Action("Wycofaj oznaczenie anulowania lub przeliczenia")] WycofajZaznaczenie()` — kasuje `Storno`.
- `StornoElementu.WypłataDoPrzeliczeniaWorker` (na `Wyplata`):
  - `ZaznaczElementyDoPrzeliczenia()` / `WycofajZaznaczenie()` — dla wszystkich elementów wypłaty.
- `StornoElementu.ListaPłacDoPrzeliczeniaWorker` (na `ListaPlac`, z `Params.Definicja` / `WszystkieElementy`).

**Mechanizm.** Oznaczenie tworzy `StornoElementu` i ustawia element na `DoStornowania`. Właściwe
wytworzenie elementu stornującego (`UtwórzStornujący()`, stan `Wystornowany` na pierwotnym +
`Stornujący` na nowym) następuje przy ponownym naliczeniu wypłaty (KADRY-H1) lub przeliczeniu. Wymagane:
wypłata zatwierdzona (`Wyplata.Zatwierdzona`) i element w stanie `NieDotyczy`.

**Snippet (oznaczenie do anulowania + przeliczenie):**

```csharp
Wyplata w = ...; // zatwierdzona wypłata pracownika 006
WypElement element = w.Elementy.Cast<WypElement>().First(e => e.Definicja.Kod == "PREMIA");

// oznacz element do anulowania:
var worker = new StornoElementu.ElementDoPrzeliczeniaWorker { Element = element };
worker.ZaznaczElementDoAnulowania();   // otwiera własną transakcję
// element.StanStorna == StanStornaElementu.DoStornowania, element.Storno.RodzajStorna == Anulowanie

// ponowne naliczenie wypłaty (KADRY-H1) wygeneruje element stornujący:
var pars = new NaliczanieSeryjne.PracownikParams(context) { DataWypłaty = w.Data, DataListy = w.Data };
new NaliczanieSeryjne.Pracownika(pars) { Pracownik = w.Pracownik }.Nalicz();
session.Save();
```

**Odczyt elementów stornowanych:**

```csharp
foreach (WypElement e in w.Elementy)
{
    if (e.RozliczenieStorna) continue;   // pomiń wystornowane i stornujące przy sumowaniu stanu bieżącego
    // ... e to element „żywy"
}
```

**Pułapki:**
- Oznaczać można tylko elementy wypłaty **zatwierdzonej** i w stanie `NieDotyczy` (`IsEnabled...` to
  egzekwuje); na buforze storno nie ma sensu.
- Storno samo w sobie tylko **oznacza** (`DoStornowania`) — wystornowanie (`Wystornowany`/`Stornujący`)
  powstaje dopiero przy ponownym naliczeniu/przeliczeniu.
- Przy sumowaniu kwot bieżących pomijaj `RozliczenieStorna == true`, inaczej policzysz element pierwotny
  i jego storno podwójnie.
- Nie można przenieść do bufora wypłaty z elementami `DoStornowania`/`Wystornowany` (rzuca `RowException`
  — patrz KADRY-H11).

---

### KADRY-H11 — Anulowanie/usunięcie naliczonej wypłaty (bufor, ponowne naliczenie) (★)

**Cel:** „cofnąć" naliczoną i zatwierdzoną wypłatę do edycji (bufor) lub usunąć, by naliczyć ponownie.

**Model.** Wypłata ma flagi `Wyplata.Bufor: bool` (niezatwierdzona/edytowalna) oraz `Wyplata.Zatwierdzona: bool`
(odwrotność `Bufor`). Zejście z bufora = zatwierdzenie; powrót do bufora = otwarcie do edycji.

**Workery (publiczne, na `Wyplata`):**

| Worker / akcja | Sygnatura | Efekt |
|---|---|---|
| `Wyplata.ZatwierdźWorker` | property `Lista: Wyplata`; `[Action("Zatwierdź wypłatę")] void Zatwierdź()` | `Zatwierdzona = true` (zejście z bufora) |
| `Wyplata.OtwórzWorker` | property `Wypłata: Wyplata`; `[Action("Przenieś do bufora")] void Otwórz()` | `Zatwierdzona = false` (powrót do bufora) |

Obie akcje działają w transakcji `Session.Logout(true)` + `Commit()`. **Uwaga na nazwy property:**
worker zatwierdzania przypina wypłatę przez `ZatwierdźWorker.Lista`, a otwierania — przez
`OtwórzWorker.Wypłata`. `IsEnabled...` wymaga `Wyplata.CanBufor` — ale `CanBufor` jest **`protected`**
(niedostępny z dodatku); stan czytaj przez publiczne `Wyplata.Bufor` / `Wyplata.Zatwierdzona`.

**Bezpośrednia flaga `Wyplata.Bufor`:**
- setter `Bufor` rzuca `ColReadOnlyException`, gdy `!CanBufor`;
- zejście z bufora (`Bufor=false`) wyzwala rozliczenie płatności (wewnętrzny `RozliczenieManager`);
- `IsReadOnlyBufor()` true gdy brak praw / `!CanBufor` / wyłączone „ZatwierdzanieFlagą" / lista zatwierdzona.

**Usunięcie / ponowne naliczenie.** Aby przeliczyć od nowa: przenieś wypłatę do bufora
(`OtwórzWorker.Otwórz()`), a następnie wykonaj ponowne naliczenie (KADRY-H1 — `NaliczanieSeryjne`), które
nadpisze elementy. Usunięcie samej wypłaty realizuje standardowe `Row.Delete()` w transakcji (gdy
dozwolone — wypłata w buforze, bez powiązań blokujących).

**Snippet (powrót do bufora + ponowne naliczenie):**

```csharp
Wyplata w = ...; // zatwierdzona wypłata pracownika 006

// 1) przenieś do bufora:
new Wyplata.OtwórzWorker { Wypłata = w }.Otwórz();   // Zatwierdzona = false

// 2) ponowne naliczenie (KADRY-H1):
var pars = new NaliczanieSeryjne.PracownikParams(context) {
    DataWypłaty = w.Data, DataListy = w.Data, TypWypłaty = TypWyplaty.Etat,
};
new NaliczanieSeryjne.Pracownika(pars) { Pracownik = w.Pracownik }.Nalicz();
session.Save();
```

**Snippet (usunięcie wypłaty z bufora):**

```csharp
using (ITransaction t = session.Logout(true)) {
    w.Bufor = true;     // upewnij się, że w buforze (lub OtwórzWorker)
    w.Delete();
    t.Commit();
}
session.Save();
```

**Pułapki:**
- `Otwórz()` rzuca `RowException`, gdy wypłata nie jest zatwierdzona; `Zatwierdź()` — gdy już
  zatwierdzona. Sprawdzaj `IsEnabled...` / stan przed wywołaniem.
- `UpdateBufor()` rzuca `RowException`, gdy na wypłacie są elementy `DoStornowania`/`Wystornowany` —
  najpierw wycofaj oznaczenia storna (KADRY-H10) lub dokończ przeliczenie.
- Zejście z bufora wykonuje rozliczenie płatności i kopiowanie kursu — nie traktuj go jak zwykłej
  zmiany pola.
- Operacje płacowe to dane operacyjne — łap `RowException`/`RowConflictException` z `Save()` (§4, §9),
  nie ogólny `Exception`.

