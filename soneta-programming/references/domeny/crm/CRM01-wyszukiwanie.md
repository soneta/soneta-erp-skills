# CRM01 — Wyszukiwanie i identyfikacja

> Wspólne fakty o typie, podstawowe typy i szablon wzorca: [../crm.md](../crm.md).

### CRM-W1 — Wyszukiwanie kontrahenta

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

### CRM-W2 — Walidacja NIP / REGON / EU VAT

**Cel:** sprawdzić poprawność NIP/REGON (suma kontrolna) i EU VAT (format/kraj) przed zapisem,
niezależnie od weryfikacji online (CRM-W15).

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
- To walidacja **formatu/sumy kontrolnej**, a nie weryfikacja w MF/VIES — patrz CRM-W15.
- Komunikaty walidacyjne rzucaj jako `RowException(row, "…".Translate(), nameof(Pole))` **przed**
  `Commit()` (safe-code §5.1). Wyjątek po `Commit()` nie wycofa zmiany z sesji.
- Ustawienie `NIP`/`EuVAT` na samym `Kontrahent` uruchamia wbudowaną synchronizację (NIP↔EuVAT,
  auto-zmiana `RodzajPodmiotu`) — własna walidacja jest dodatkiem, nie zastępstwem.

---

