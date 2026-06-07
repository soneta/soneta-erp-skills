# KADRY02 — Etat — zatrudnienie etatowe

> Wspólne fakty o typie, podstawowe typy i szablon wzorca: [../kadry.md](../kadry.md).

### KADRY-B1 — Definiowanie etatu (umowa o pracę) (★)

**Cel:** ustalić warunki zatrudnienia etatowego pracownika — rodzaj umowy o pracę, okres, daty
zawarcia/rozpoczęcia pracy, stanowisko, jednostkę organizacyjną oraz stawkę zaszeregowania
(wymiar etatu, rodzaj/typ stawki, kwota). Warunki etatu są **historyczne**: siedzą w polu
`Etat` konkretnego zapisu `PracHistoria`. Etat ustawiamy albo na świeżo utworzonym pracowniku
(`pracownik.Last.Etat`, patrz KADRY-A1), albo na nowym zapisie historii „od daty" (patrz KADRY-A14).

**Gdzie leżą pola — `PracHistoria.Etat: Soneta.Kadry.Etat` (subrow zapisu historii):**

| Dana | Pole / typ | Uwaga |
|---|---|---|
| Rodzaj umowy o pracę | `Etat.TypUmowy: Soneta.Kadry.TypUmowyOPrace` | enum: `NaCzasNieokreślony`, `NaOkresPróbny`, `NaCzasOkreślony`, `NaOkresZastępstwa`, `DoDniaPorodu`, `NaCzasWykonywniaPracy`, … (`Brak = 0` = nie dotyczy) |
| Okres etatu (od–do) | `Etat.Okres: Soneta.Types.FromTo` | okres obowiązywania warunków zatrudnienia |
| Data zawarcia umowy | `Etat.DataZawarcia: Soneta.Types.Date` | data podpisania umowy |
| Data rozpoczęcia pracy | `Etat.DataRozpPracy: Soneta.Types.Date` | data faktycznego rozpoczęcia |
| Stanowisko (opis tekstowy) | `Etat.Stanowisko: string` | **wymagane dla etatu** (weryfikator przy `Save()`) |
| Jednostka organizacyjna (wydział) | `Etat.Wydzial: Soneta.Kadry.Wydzial` | **wymagane dla etatu**; pobierz istniejący wydział, korzeń struktury: `session.GetKadry().Wydzialy.Firma` |
| Oddział firmy | `Etat.Oddzial: Soneta.Core.OddzialFirmy` | opcjonalny oddział |
| Miejsce wykonywania pracy | `Etat.MiejscePracy: string` | tekst |
| Podstawa stosunku pracy | `Etat.Podstawa: Soneta.Kadry.StosPracyNaPodstawie` | enum |

**Stawka — subrow `Etat.Zaszeregowanie: Soneta.Kadry.Zaszeregowanie`:**

| Dana | Pole / typ | Uwaga |
|---|---|---|
| Rodzaj stawki | `Zaszeregowanie.RodzajStawki: Soneta.Kadry.RodzajStawkiZaszeregowania` | enum: `Godzinowa = 0`, `Miesieczna = 1`, `DochodDeklarowany = 2` |
| Typ stawki | `Zaszeregowanie.TypStawki: Soneta.Kadry.TypStawkiZaszeregowania` | enum: `Dowolna = 0`, `Minimalna = 1`, `ZZakresu = 2`, `WgWskaźnika = 3`, `Nieokreślona = 10` |
| Wymiar etatu (ułamek) | `Zaszeregowanie.Wymiar: Soneta.Types.Fraction` | `Fraction.One` = pełny etat; `new Fraction(1, 2)` = ½ etatu |
| Kwota stawki | `Zaszeregowanie.Stawka: Soneta.Types.Currency` | kwota brutto (miesięczna lub godzinowa wg `RodzajStawki`) |
| Grupa zaszeregowania | `Zaszeregowanie.Grupa: Soneta.Kadry.GrupaZaszeregowania` | rekord słownika (opcjonalny) |
| Definicja elementu wynagrodzenia | `Zaszeregowanie.Element: Soneta.Place.DefinicjaElementu` | element płacowy wiązany ze stawką (opcjonalny) |

**Pułapki:**
- **Kolejność ma znaczenie — `Etat.Okres` ustaw jako PIERWSZE.** Na świeżo utworzonym
  pracowniku (lub świeżym zapisie historii) **cały subrow `Etat` jest w trybie tylko-do-odczytu**,
  dopóki nie ustawisz `Etat.Okres` (zakres zatrudnienia). Próba ustawienia `Etat.TypUmowy`,
  `Etat.Podstawa` czy `Zaszeregowanie.RodzajStawki`/`Wymiar` przed `Etat.Okres` rzuca
  `Soneta.Business.ColReadOnlyException` (np. „'Etat.Typ umowy' — pole w trybie 'tylko do odczytu'").
  Po ustawieniu `Etat.Okres` pozostałe pola (w tym `Zaszeregowanie.Wymiar`) są zapisywalne —
  kolejność wśród nich nie ma już znaczenia.
- **Pola wymagane dla etatu:** po ustawieniu `Etat.Okres` (pracownik staje się etatowy) `Save()`
  wymaga `Etat.Wydzial` **oraz** `Etat.Stanowisko` — bez nich zapis rzuca wyjątek weryfikatora.
- `Etat` to **subrow** zapisu `PracHistoria` — modyfikujesz jego pola (`Last.Etat.Okres = …`), nie
  przypisujesz całego obiektu `Etat`.
- `Zaszeregowanie` to z kolei subrow `Etat` — analogicznie modyfikujesz pola
  (`Last.Etat.Zaszeregowanie.Stawka = …`).
- `Etat.Wymiar` i `Etat.TypStawki` istnieją także na poziomie `Etat` (delegaty/odczyt) — **kanonicznie
  ustawiamy je na `Etat.Zaszeregowanie`** (`Zaszeregowanie.Wymiar`, `Zaszeregowanie.RodzajStawki`,
  `Zaszeregowanie.TypStawki`), bo to one są polami bazodanowymi tej struktury.
- `Etat.Wydzial` i `Etat.Oddzial` to **referencje** do istniejących rekordów — nie twórz „w locie";
  korzeń struktury organizacyjnej pobierzesz przez `session.GetKadry().Wydzialy.Firma`.
- Zmiana warunków etatu **od konkretnego dnia** to nowy zapis historii (`Historia.Update(date)` +
  `PracHistorie.AddRow`, patrz KADRY-A14), a nie nadpisanie bieżącego zapisu (to byłaby korekta całego okresu).
- `TypUmowyOPrace` to enum, nie string; `Okres`/`DataZawarcia`/`DataRozpPracy` to typy biznesowe
  `FromTo`/`Date`, nie `DateTime` (safe-code §10.1).

**Snippet:**

```csharp
var kadry = session.GetKadry();

using (var t = session.Logout(editMode: true))
{
    // Nowy pracownik (KADRY-A1) — AddRow tworzy pierwszy zapis historii (Last) + kalendarz.
    var pracownik = session.AddRow(new PracownikFirmy());
    pracownik.Kod = "555";
    pracownik.Last.Nazwisko = "Kowalska";
    pracownik.Last.Imie     = "Gabriela";

    // Warunki etatu — na Etat bieżącego (pierwszego) zapisu historii.
    // KLUCZOWE: Etat.Okres MUSI być pierwszy — odblokowuje (z trybu read-only) resztę pól Etat.
    var etat = pracownik.Last.Etat;
    etat.Okres         = new FromTo(new Date(2026, 1, 1), Date.MaxValue);   // 1) NAJPIERW okres
    etat.TypUmowy      = TypUmowyOPrace.NaCzasNieokreślony;
    etat.DataZawarcia  = new Date(2025, 12, 20);
    etat.DataRozpPracy = new Date(2026, 1, 1);
    etat.Stanowisko    = "Specjalista";                  // wymagane dla etatu
    etat.Wydzial       = kadry.Wydzialy.Firma;           // wymagane dla etatu (korzeń struktury)

    // Stawka zaszeregowania (po ustawieniu Etat.Okres pola są zapisywalne):
    var z = etat.Zaszeregowanie;
    z.RodzajStawki = RodzajStawkiZaszeregowania.Miesieczna;
    z.TypStawki    = TypStawkiZaszeregowania.Dowolna;
    z.Wymiar       = Fraction.One;                            // pełny etat
    z.Stawka       = (Currency)6000m;                         // kwota brutto miesięcznie

    t.Commit();
}
session.Save();
```

> **Zmiany warunków zatrudnienia (KADRY-B2–KADRY-B7).** Warunki zatrudnienia etatowego siedzą w polu `PracHistoria.Etat: Soneta.Kadry.Etat`
> (subrow zapisu historii). `Etat` jest **historyczny** wraz z całym `PracHistoria` — okres
> obowiązywania warunków trzyma `Etat.Okres: FromTo`, a okres zapisu historii `PracHistoria.Aktualnosc`.
> Zmiana warunków „od dnia" to **nowy zapis historii** (`Historia.Update(date)` + `PracHistorie.AddRow`,
> wzorzec z KADRY-A14) — modyfikacja bieżącego zapisu byłaby korektą całego jego okresu.
>
> **Bramka edycji etatu (KADRY-B1).** Na świeżym zapisie cały subrow `Etat` jest tylko-do-odczytu, dopóki
> nie ustawisz `Etat.Okres` — ustaw go **PIERWSZY**, inaczej dotknięcie `TypUmowy`/`Zaszeregowanie.*`
> rzuca `Soneta.Business.ColReadOnlyException`. Pola wymagane przy zapisie etatu: `Etat.Wydzial`
> **oraz** `Etat.Stanowisko`. Po `Update(date)` klon ma już ustawiony `Etat.Okres` (sklonowany ze
> starego zapisu) — zwykle nie trzeba go ustawiać ponownie, ale jeśli zmieniasz okres etatu, rób to
> jako pierwsze.

---

### KADRY-B2 — Zmiana warunków zatrudnienia (aneks)

**Cel:** zarejestrować aneks do umowy o pracę — zmianę warunków obowiązującą **od wskazanego dnia**
(np. zmiana stanowiska, miejsca pracy, wymiaru, jednostki organizacyjnej). Realizuje się przez
**nowy zapis historyczny** etatu „od daty", nie przez nadpisanie bieżącego.

**Pola `Etat` (subrow `PracHistoria.Etat: Soneta.Kadry.Etat`):**

| Dana | Pole / typ | Uwaga |
|---|---|---|
| Okres etatu (od–do) | `Etat.Okres: Soneta.Types.FromTo` | okres obowiązywania warunków; po `Update` zwykle już ustawiony |
| Rodzaj umowy o pracę | `Etat.TypUmowy: Soneta.Kadry.TypUmowyOPrace` | enum |
| Data zawarcia aneksu | `Etat.DataZawarcia: Soneta.Types.Date` | data podpisania |
| Stanowisko (opis) | `Etat.Stanowisko: string` | wymagane dla etatu |
| Jednostka organizacyjna | `Etat.Wydzial: Soneta.Kadry.Wydzial` | wymagane dla etatu; referencja (`session.GetKadry().Wydzialy.Firma`) |
| Oddział firmy | `Etat.Oddzial: Soneta.Core.OddzialFirmy` | opcjonalny |
| Miejsce wykonywania pracy | `Etat.MiejscePracy: string` | tekst |
| Podstawa stosunku pracy | `Etat.Podstawa: Soneta.Kadry.StosPracyNaPodstawie` | enum |
| Forma organizacji pracy | `Etat.FormaOrganizacjiPracy: Soneta.Kadry.FormaOrganizacjiPracy` | enum |
| Wymiar / stawka (na `Zaszeregowanie`) | `Etat.Zaszeregowanie.Wymiar: Fraction`, `Etat.Zaszeregowanie.Stawka: Currency` | patrz KADRY-B3 |

**Snippet:**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
var odDnia = new Date(2026, 7, 1);

using (var t = session.Logout(editMode: true))
{
    // Nowy zapis historii „od daty" — klonuje zapis aktualny na `odDnia`, skraca stary do dnia
    // poprzedniego i zwraca nowy klon (okres od `odDnia`). Klon MUSI trafić do tabeli zapisów.
    var nowy = (PracHistoria)pracownik.Historia.Update(odDnia);
    pracownik.Module.PracHistorie.AddRow(nowy);

    // Etat na klonie ma już Okres (sklonowany) — pola Etat są zapisywalne. Aneksowane warunki:
    var etat = nowy.Etat;
    etat.Stanowisko   = "Starszy specjalista";
    etat.MiejscePracy = "Oddział Kraków";
    etat.DataZawarcia = new Date(2026, 6, 20);
    etat.Wydzial      = session.GetKadry().Wydzialy.Firma;   // wymagane (referencja)

    t.Commit();
}
session.Save();
```

**Pułapki:**
- `Update(date)` + `PracHistorie.AddRow(nowy)` to **nierozłączna para** — sam `Update` zwraca odpięty
  klon; bez `AddRow` zmiana nie zostanie zapisana.
- `Update(date)` rzuca `HistorySubTable.DateDuplicateException`, gdy na `date` już zaczyna się zapis
  (`Aktualnosc.From == date`) — wtedy modyfikuj istniejący zapis (`pracownik[date]`).
- Nie ustawiaj `PracHistoria.Aktualnosc` ani (zwykle) `Etat.Okres` ręcznie — zarządza nimi historia.
  Jeśli aneks zmienia długość okresu etatu, ustaw `Etat.Okres` **przed** pozostałymi polami (bramka KADRY-B1).
- `Etat` to subrow — modyfikuj jego pola, nie przypisuj całego obiektu.

---

### KADRY-B3 — Przeszeregowanie (zmiana stawki / grupy zaszeregowania)

**Cel:** zmienić wynagrodzenie zasadnicze — stawkę i/lub grupę zaszeregowania, od wskazanego dnia.

**Pola `Etat.Zaszeregowanie: Soneta.Kadry.Zaszeregowanie` (subrow `Etat`):**

| Dana | Pole / typ | Uwaga |
|---|---|---|
| Rodzaj stawki | `Zaszeregowanie.RodzajStawki: Soneta.Kadry.RodzajStawkiZaszeregowania` | enum: `Godzinowa = 0`, `Miesieczna = 1`, `DochodDeklarowany = 2` |
| Typ stawki | `Zaszeregowanie.TypStawki: Soneta.Kadry.TypStawkiZaszeregowania` | enum: `Dowolna`, `Minimalna`, `ZZakresu`, `WgWskaźnika`, `Nieokreślona` |
| Kwota stawki | `Zaszeregowanie.Stawka: Soneta.Types.Currency` | brutto (miesięczna/godzinowa wg `RodzajStawki`) |
| Wymiar etatu | `Zaszeregowanie.Wymiar: Soneta.Types.Fraction` | `Fraction.One` = pełny etat |
| Grupa zaszeregowania | `Etat.Grupa: Soneta.Kadry.GrupaZaszeregowania` | **leży na `Etat`, nie na `Zaszeregowanie`**; referencja do słownika `session.GetKadry().GrupyZaszer` (opcjonalna) |
| Element wynagrodzenia | `Zaszeregowanie.Element: Soneta.Place.DefinicjaElementu` | element płacowy wiązany ze stawką (opcjonalny) |
| Wskaźnik (wg wskaźnika) | `Zaszeregowanie.WskaznikNazwa: string`, `Zaszeregowanie.WskaznikKrotnosc: double` | gdy `TypStawki = WgWskaźnika` |

**Snippet (bezpośrednia zmiana, „od daty"):**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
var odDnia = new Date(2026, 7, 1);

using (var t = session.Logout(editMode: true))
{
    var nowy = (PracHistoria)pracownik.Historia.Update(odDnia);
    pracownik.Module.PracHistorie.AddRow(nowy);

    var etat = nowy.Etat;                    // subrow zapisu; Okres ustawiony przez Update
    etat.Zaszeregowanie.Stawka = (Currency)7200m;   // podwyżka stawki zasadniczej
    // etat.Grupa = session.GetKadry().GrupyZaszer...  // ewentualna zmiana grupy (leży na Etat, nie na Zaszeregowanie)

    t.Commit();
}
session.Save();
```

**Worker platformy (alternatywa seryjna):** przeszeregowania realizuje moduł
`Soneta.Przeszeregowania` — dokument `Soneta.Kadry.Przeszeregowanie` z elementami
`ElementPrzeszeregowania` (m.in. `Soneta.Kadry.ZmianaStawki`), wykonywany czynnością
`ZmianaStawkiWorker` (zmiana kwoty / procentowa / grupa) dla zaznaczonej grupy pracowników. Element
`ZmianaStawki` ma pola `Grupa: GrupaZaszeregowania`, `Kwota: Currency` i zapisuje wynik do
`Etat.Zaszeregowanie` właściwego zapisu historii.

**Pułapki:**
- `Wymiar`/`Stawka`/`RodzajStawki` na **świeżym** zapisie są zapisywalne dopiero po `Etat.Okres`
  (bramka KADRY-B1); po `Update` okres jest już sklonowany, więc pola są zapisywalne.
- `Stawka` to `Currency` (nie `decimal`), `Wymiar` to `Fraction` (nie `double`) — safe-code §10.1.
- `Etat.Grupa`/`Zaszeregowanie.Element` to **referencje** do istniejących rekordów — nie twórz „w locie".
  **Uwaga:** `Grupa` jest polem `Etat` (pobierasz ze słownika `session.GetKadry().GrupyZaszer`), a **nie**
  polem `Zaszeregowanie` — `Zaszeregowanie` nie ma property `Grupa`.
- Kanonicznie ustawiasz pola stawki na `Etat.Zaszeregowanie` (pola bazodanowe), nie na delegatach
  `Etat.Wymiar`/`Etat.TypStawki`.

---

### KADRY-B4 — Rozwiązanie / wygaśnięcie umowy o pracę

**Cel:** zakończyć stosunek pracy z dniem rozwiązania — ustawić datę końca okresu etatu, dane
wypowiedzenia oraz przyczynę/kod rozwiązania (na potrzeby świadectwa pracy i deklaracji ZUS).

**Wzorzec (zgodny z czynnością „Zwolnij zaznaczonych pracowników"):**
1. skróć `Etat.Okres.To` do dnia rozwiązania (na bieżącym zapisie albo na nowym zapisie „od daty"),
2. ustaw dane wypowiedzenia (`Etat.OkresWypowiedzenia.*`) i przyczynę (`Etat.RozwiazanieUmowy.*`),
3. opcjonalnie oznacz `Etat.PracownikZwolniony = true` i wyrejestruj z ubezpieczeń.

**Pola — `Etat.OkresWypowiedzenia: Soneta.Kadry.OkresWypowiedzenia` (subrow):**

| Dana | Pole / typ | Uwaga |
|---|---|---|
| Data złożenia wypowiedzenia | `OkresWypowiedzenia.DataZlozenia: Soneta.Types.Date` | data wręczenia wypowiedzenia |
| Długość — dni / tygodnie / miesiące | `OkresWypowiedzenia.Dni: int`, `.Tygodnie: int`, `.Miesiace: int` | okres wypowiedzenia |
| Data upływu | `OkresWypowiedzenia.Uplywa: Soneta.Types.Date` | data upływu okresu wypowiedzenia |
| Skrócony | `OkresWypowiedzenia.Skrocony: bool` | skrócony okres wypowiedzenia |
| Zwolnienie z obowiązku pracy od | `OkresWypowiedzenia.ZwolnionyZObowiazkuPracyOd: Date` | |
| Data rozwiązania umowy (odczyt) | `OkresWypowiedzenia.DataRozwiązaniaUmowy: Date` | kalkulowane |

**Pola — `Etat.RozwiazanieUmowy: Soneta.Kadry.RozwiazanieUmowy` (subrow):**

| Dana | Pole / typ | Uwaga |
|---|---|---|
| Przyczyna rozwiązania | `RozwiazanieUmowy.PrzyczynaRozwUmowy: Soneta.Kadry.PrzyczynaRozwUmowy` | **referencja do słownika** `session.GetKadry().PrzyczRozwUmow` (indeks `WgNazwy` lub iteracja; **brak indeksera `WgKodu`**); rekord ma `Typ: TypPrzyczynyRozwUmowy` |
| Opis przyczyny | `RozwiazanieUmowy.PrzyczynaRozwUmowyOpis: string` | tekst |
| Podstawa prawna | `RozwiazanieUmowy.PodstawaPrawna: Soneta.Kadry.KodPodstawyPrawnejZwolnienia` | enum (tryb rozwiązania: za wypowiedzeniem, porozumienie, wygaśnięcie itd.) |
| Kod zwolnienia (ZUS) | `RozwiazanieUmowy.KodZwolnienia: Soneta.Kadry.KodZwolnienia` | enum (kod do ZUS RA/świadectwa) |
| Inicjatywa | `RozwiazanieUmowy.Inicjatywa: Soneta.Kadry.KodInicjatywyZwolnienia` | enum (pracodawca/pracownik) |
| Za odszkodowaniem | `RozwiazanieUmowy.ZaOdszkodowaniem: bool` | |
| Pracownik zwolniony (flaga) | `Etat.PracownikZwolniony: bool` | znacznik zakończenia |

**Snippet:**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
var dataRozwiazania = new Date(2026, 9, 30);

using (var t = session.Logout(editMode: true))
{
    var ph = pracownik.Last;
    var etat = ph.Etat;

    // 1) skrócenie okresu etatu do dnia rozwiązania
    etat.Okres = new FromTo(etat.Okres.From, dataRozwiazania);

    // 2) dane wypowiedzenia
    etat.OkresWypowiedzenia.DataZlozenia = new Date(2026, 8, 31);
    etat.OkresWypowiedzenia.Miesiace     = 1;

    // 3) przyczyna / tryb rozwiązania
    //    PrzyczynaRozwUmowy to rekord słownika — pobierz po nazwie (WgNazwy) albo iteracją (brak WgKodu):
    etat.RozwiazanieUmowy.PrzyczynaRozwUmowy = session.GetKadry().PrzyczRozwUmow.WgNazwy["Wypowiedzenie przez pracownika"]; // referencja
    //    PodstawaPrawna to enum kodów: NieDotyczy, _400.._463, _550 (kody GUS/ZUS) — wybierz właściwy kod:
    etat.RozwiazanieUmowy.PodstawaPrawna     = KodPodstawyPrawnejZwolnienia._400;
    etat.RozwiazanieUmowy.Inicjatywa         = KodInicjatywyZwolnienia.Pracownik;

    etat.PracownikZwolniony = true;          // znacznik zakończenia zatrudnienia

    t.Commit();
}
session.Save();
```

**Pułapki:**
- **Wygaśnięcie** vs **rozwiązanie** rozróżnia `PodstawaPrawna` (enum trybu) oraz `KodZwolnienia` —
  to one trafiają do świadectwa pracy i deklaracji ZUS.
- `PrzyczynaRozwUmowy` to **rekord słownika** (referencja), nie enum — pobierz istniejący wpis z
  `session.GetKadry().PrzyczRozwUmow` (indeks `WgNazwy` lub iteracja — **słownik nie ma indeksera
  `WgKodu`**). Pomyłka: `RozwiazanieUmowy.PrzyczynaRozwUmowy` (referencja) ≠
  `PrzyczynaRozwUmowy.Typ` (enum `TypPrzyczynyRozwUmowy` na rekordzie słownika).
- `KodPodstawyPrawnejZwolnienia` to enum kodów GUS/ZUS o wartościach `NieDotyczy`, `_400`..`_463`,
  `_550` (nazwy z prefiksem `_`) — **nie ma** wartości opisowych typu
  `RozwiazanieZaWypowiedzeniemPrzezPracownika`. `KodInicjatywyZwolnienia`: `NieDotyczy`, `Pracownik`,
  `Pracodawca`.
- Skrócenie `Etat.Okres.To` zmienia warunki w **całym** bieżącym okresie zapisu. Jeśli rozwiązanie
  ma obowiązywać od konkretnego dnia z zachowaniem poprzedniego okresu, użyj nowego zapisu
  (`Historia.Update(data)` + `PracHistorie.AddRow`), a zmiany rób na klonie.
- Wyrejestrowanie z ubezpieczeń (`IUbezpieczenie.Wyrejestrowany`/daty `Do`) to osobny krok — patrz KADRY-A7.
- `Okres`/`DataZlozenia`/`Uplywa` to `FromTo`/`Date`, nie `DateTime`.

---

### KADRY-B5 — Obniżenie / przywrócenie wymiaru etatu

**Cel:** czasowo obniżyć wymiar etatu i stawkę (operacje typu COVID / seryjne), a następnie
przywrócić warunki. Stan obniżenia jest **odczytowo** widoczny w subrowie
`Etat.ObnizenieEtatu: Soneta.Kadry.ObniżenieWymiaruEtatu` (delegat do zapisu historii etatu).

> **Ważne (zweryfikowane na DLL):** subrow `ObniżenieWymiaruEtatu` jest **w całości tylko-do-odczytu**
> — wszystkie jego property (`Wymiar`, `Stawka`, `RodzajStawki`, `TypStawki`, `Element`, `Kalendarz`,
> `Info`) mają `CanWrite == false`, a klasa **nie udostępnia publicznej metody `Save(...)`**.
> Z poziomu kodu biznesowego **nie da się** ustawić tych pól ani „utrwalić" obniżenia przez ten subrow.
> Pełny zapis stanu obniżenia (z metadanymi `ObniżenieWymiaruEtatuInfo`) realizują **workery platformy**.
> W zwykłym kodzie obniżenie sprowadzasz do ustawienia docelowego `Etat.Zaszeregowanie.Wymiar`
> (i ewentualnie `Stawka`) na nowym zapisie „od daty".

**Pola odczytowe — `Etat.ObnizenieEtatu: Soneta.Kadry.ObniżenieWymiaruEtatu` (subrow, read-only):**

| Dana | Pole / typ | Uwaga |
|---|---|---|
| Obniżony wymiar etatu | `ObnizenieEtatu.Wymiar: Soneta.Types.Fraction` | **read-only** |
| Obniżona stawka | `ObnizenieEtatu.Stawka: Soneta.Types.Currency` | **read-only** |
| Rodzaj / typ stawki | `ObnizenieEtatu.RodzajStawki`, `.TypStawki` | enumy, **read-only** |
| Kalendarz | `ObnizenieEtatu.Kalendarz: Soneta.Kalend.KalendarzBase` | referencja, **read-only** |
| Element wynagrodzenia | `ObnizenieEtatu.Element: Soneta.Place.DefinicjaElementu` | referencja, **read-only** |
| Zakres obniżenia (przełącznik) | `ObnizenieEtatu.Info: Soneta.Kadry.ObniżenieWymiaruEtatuInfo` | enum (`Brak`/`Wymiar`/`Stawka`/`Zaszeregowanie`/`Kalendarz`/…), **read-only** |

**Snippet (obniżenie wymiaru „od daty" w kodzie biznesowym):**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
var odDnia = new Date(2026, 7, 1);

using (var t = session.Logout(editMode: true))
{
    var ph = (PracHistoria)pracownik.Historia.Update(odDnia);
    pracownik.Module.PracHistorie.AddRow(ph);

    // Subrow ObnizenieEtatu jest read-only — NIE ustawiamy go bezpośrednio.
    // Docelowy wymiar po obniżeniu utrwalamy na Etat.Zaszeregowanie.Wymiar (pole zapisywalne):
    ph.Etat.Zaszeregowanie.Wymiar = new Fraction(4, 5);     // np. obniżenie do 4/5 etatu

    t.Commit();
}
session.Save();
```

**Workery platformy (seryjne, na zaznaczonych pracownikach, tabela `Pracownicy`):**
`ObniżWymiarEtatuWorker` (obniżenie: proporcjonalnie / do / o), `ZmianaStawkiZaszeregowaniaWorker`,
`ZmianaKalendarzaWorker`, `PrzywróćWarunkiZatrudnieniaWorker` (przywrócenie warunków sprzed
obniżenia). To one zakładają nowy zapis historii „od daty" i zapisują pełny stan obniżenia
(`ObniżenieWymiaruEtatuInfo`), którego nie da się ustawić przez publiczny kontrakt subrowa.

**Pułapki:**
- `Etat.ObnizenieEtatu` to **odczytowy delegat** do zapisu historii etatu — wszystkie property są
  read-only i klasa nie ma metody `Save(...)`. W kodzie biznesowym obniżenie wymiaru realizujesz
  ustawiając `Etat.Zaszeregowanie.Wymiar` (i `Stawka`) na nowym zapisie; pełny zapis stanu obniżenia
  z `ObniżenieWymiaruEtatuInfo` zostaw workerom platformy.
- Operacja jest „od daty" — zawsze przez nowy zapis (`Update` + `AddRow`); inaczej zmienisz wymiar
  wstecz w całym bieżącym okresie.
- Przywrócenie warunków to osobna operacja (`PrzywróćWarunkiZatrudnieniaWorker`) — nie polega na
  usuwaniu obniżenia, lecz na nowym zapisie z przywróconym wymiarem.

---

### KADRY-B6 — Podzielniki kosztów (rozdział kosztów wynagrodzenia)

**Cel:** rozdzielić koszty wynagrodzenia pracownika na wydziały/projekty/centra kosztów wg
współczynników. Struktura: `Pracownik` jako **źródło** podzielnika →
`pracownik.Podzielniki: SubTable<Soneta.Core.PodzielnikKosztow>` → każdy podzielnik ma **historię**
`PodzielnikKosztow.Historia: HistorySubTable<HistoriaPodzielnika>` → a zapis historii ma kolekcję
`HistoriaPodzielnika.Elementy: SubTable<Soneta.Core.ElementPodzielnika>` (poszczególne udziały).

> Uwaga: `Pracownik.ElementyPodzielnika: SubTable<ElementPodzielnika>` to widok zbiorczy elementów
> ze wszystkich podzielników pracownika (do odczytu). **Tworzysz** elementy na konkretnym zapisie
> `HistoriaPodzielnika`, nie przez tę kolekcję.

**Tworzenie obiektów (konstruktory + AddRow):**

| Obiekt | Konstruktor | Tabela / AddRow |
|---|---|---|
| Podzielnik | `new PodzielnikKosztow(pracownik)` (pracownik jako `IZrodloPodzielnikaKosztow`) | `session.GetCore().PodzielKosztow.AddRow(p)` |
| Zapis historii | `podzielnik.Historia.Update(odDnia)` | `session.GetCore().HistPodzielnikow.AddRow(h)` |
| Element udziału | `new ElementPodzielnika(historia)` | `session.GetCore().ElemPodzielnikow.AddRow(e)` |

**Pola — `PodzielnikKosztow`:** `Nazwa: string`, `Definicja: Soneta.Core.DefinicjaPodzielnikaKosztow`,
`Zrodlo: IZrodloPodzielnikaKosztow` (pracownik, ustawiany ctorem), `Last/Historia`.
**Pola — `HistoriaPodzielnika`:** `Aktualnosc: FromTo` (okres zapisu, zarządzany), `Podstawa: decimal`,
`Elementy: SubTable<ElementPodzielnika>`.
**Pola — `ElementPodzielnika`:** `ElementPodzialowy: Soneta.Core.IElementSlownika` (cel rozdziału —
m.in. `Wydzial`, `Projekt`, `CentrumKosztow`, `OddzialFirmy` — iface-ref), `Wspolczynnik: double`,
`Procent: Percent` (kalkulowany z współczynników).

**Snippet:**

```csharp
var core = session.GetCore();
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
var odDnia = new Date(2026, 1, 1);
var wydzialA = session.GetKadry().Wydzialy.Firma;   // referencja do celu rozdziału (IElementSlownika)

using (var t = session.Logout(editMode: true))
{
    var podzielnik = new PodzielnikKosztow(pracownik);     // ctor wiąże ze źródłem (pracownik)
    core.PodzielKosztow.AddRow(podzielnik);
    podzielnik.Nazwa     = "Rozdział kosztów";
    // podzielnik.Definicja = ...                          // referencja do definicji (opcjonalnie)

    // zapis historii „od daty" (klon + AddRow)
    var historia = podzielnik.Historia.Update(odDnia);
    core.HistPodzielnikow.AddRow(historia);

    // udział: cel rozdziału + współczynnik
    var element = new ElementPodzielnika(historia);
    core.ElemPodzielnikow.AddRow(element);
    element.ElementPodzialowy = wydzialA;
    element.Wspolczynnik      = 100d;                       // Procent wyliczany z współczynników

    t.Commit();
}
session.Save();

// Odczyt zbiorczy elementów podzielnika pracownika:
foreach (ElementPodzielnika e in pracownik.ElementyPodzielnika)
{
    IElementSlownika cel = e.ElementPodzialowy;   // np. Wydzial / Projekt
    double wsp = e.Wspolczynnik;
}
```

**Pułapki:**
- Trójpoziomowa struktura — `PodzielnikKosztow` (root, źródło = pracownik) → `HistoriaPodzielnika`
  (historia „od daty") → `ElementPodzielnika` (udziały). Każdy poziom: konstruktor **+** `AddRow` do
  właściwej tabeli `Core`. Sam konstruktor nie wystarczy.
- `Historia.Update(odDnia)` + `HistPodzielnikow.AddRow` — para jak w KADRY-A14; zmiana udziałów „od dnia"
  to nowy zapis historii (a wcześniej zwykle usunięcie/`Delete()` elementów starego zapisu przy
  aktualizacji tego samego okresu — patrz worker pracy zdalnej).
- `ElementPodzialowy` to **referencja interfejsowa** (`IElementSlownika`) — przypisz istniejący
  rekord (`Wydzial`, `Projekt`, `CentrumKosztow`, …), nie twórz „w locie".
- `Procent` jest kalkulowany z `Wspolczynnik` poszczególnych elementów — ustawiasz współczynniki,
  nie procenty.

---

### KADRY-B7 — Aktualizacja danych wg definicji stanowiska (matrycy)

**Cel:** powiązać etat z definicją stanowiska i przejąć z niej parametry (stawka/grupa/wymiar,
kalendarz, kod zawodu). Definicja stanowiska to **matryca** — wzorzec wartości dla etatu.

**Pole na etacie:** `Etat.Definicja: Soneta.HR.DefinicjaStanowiska` (referencja do słownika
konfiguracyjnego `session.GetHR().DefStanowisk`). Pokrewne: `Etat.DefinicjaFunkcji: DefinicjaFunkcji`.

**Pola `DefinicjaStanowiska` (matryca, do skopiowania na etat):**

| Dana | Pole / typ |
|---|---|
| Nazwa / stanowisko | `Nazwa: string`, `Stanowisko: string`, `StanowiskoPelne: string` |
| Funkcja | `Funkcja: string`, `DefinicjaFunkcji: Soneta.HR.DefinicjaFunkcji` |
| Zaszeregowanie (wzorzec) | `Zaszeregowanie: Soneta.Kadry.Zaszeregowanie` (`Stawka`, `Wymiar`, `RodzajStawki`, `Element`, `WskaznikNazwa/Krotnosc`) |
| Typ stawki / grupa | `TypStawki: TypStawkiZaszeregowania`, `Grupa: Soneta.Kadry.GrupaZaszeregowania` |
| Kalendarz | `Kalendarz: Soneta.Kalend.Kalendarz`, `NieNadpisujKalendarza: bool` |
| Kod zawodu / praca w szcz. warunkach | `KodWykonywanegoZawodu`, `KodPracyWSzczWarunkach`, `InterpretacjaKalendarza` |

**Snippet:**

```csharp
var pracownik = session.GetKadry().Pracownicy.WgKodu["006"];
var def = session.GetHR().DefStanowisk.WgNazwa["Specjalista ds. kadr"];   // matryca (referencja; klucz WgNazwa)
var odDnia = new Date(2026, 7, 1);

using (var t = session.Logout(editMode: true))
{
    var nowy = (PracHistoria)pracownik.Historia.Update(odDnia);
    pracownik.Module.PracHistorie.AddRow(nowy);

    var etat = nowy.Etat;
    etat.Definicja  = def;                       // powiązanie z definicją stanowiska
    etat.Stanowisko = def.Stanowisko;            // przeniesienie wartości z matrycy
    etat.Zaszeregowanie.Wymiar = def.Zaszeregowanie.Wymiar;
    etat.Zaszeregowanie.Stawka = def.Zaszeregowanie.Stawka;

    t.Commit();
}
session.Save();
```

**Pułapki:**
- `Etat.Definicja` to **referencja** do rekordu konfiguracyjnego `DefStanowisk` — pobierz istniejącą
  (`session.GetHR().DefStanowisk`), nie twórz „w locie". Indeks po nazwie to `WgNazwa`
  (**nie `WgNazwy`**); w bazie Demo słownik bywa pusty — zabezpiecz się na brak definicji.
- Definicja jest matrycą — przeniesienie wartości (stawka/wymiar/kalendarz) na etat zrób jawnie;
  samo wskazanie `Etat.Definicja` nie nadpisuje automatycznie wszystkich pól etatu w kodzie biznesowym.
- Dostępność definicji potrafi zależeć od konfiguracji (`DefinicjeStanowiskDlaWydziałów`) — definicja
  może być filtrowana po wydziale.
- Zmiana stanowiska „od dnia" to nowy zapis historii (KADRY-A14), nie nadpisanie bieżącego.

