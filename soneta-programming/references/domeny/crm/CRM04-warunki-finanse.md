# CRM04 — Warunki handlowe i finanse

> Wspólne fakty o typie, podstawowe typy i szablon wzorca: [../crm.md](../crm.md).

### CRM-W9 — Warunki płatności i limity kredytowe

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

### CRM-W10 — Konto księgowe / rozrachunkowe

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
- `Platnik` jest **kalkulowany** (zależny od relacji podmiotów, CRM-W14) — nie zapisuj go bezpośrednio.
- Konta księgowe rozrachunkowe należą do modułu księgowego; z poziomu kontrahenta operuj przez
  interfejs `IPodmiotKasowy` i kolekcje rozrachunków (CRM-W11), nie przez prywatne pola księgowe.

### CRM-W11 — Rozrachunki i płatności

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

