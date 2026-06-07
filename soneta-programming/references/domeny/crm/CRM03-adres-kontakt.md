# CRM03 — Adres, kontakt, osoby

> Wspólne fakty o typie, podstawowe typy i szablon wzorca: [../crm.md](../crm.md).

### CRM-W6 — Adres

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

### CRM-W7 — Dane kontaktowe i adresy WWW

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

### CRM-W8 — Osoby kontaktowe

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

