# Dokument handlowy — receptury kodu biznesowego (platforma Soneta)

Zbiór gotowych wzorców kodu dla obiektu biznesowego **`Soneta.Handel.DokumentHandlowy`**
(tabela `DokHandlowe`, moduł `HandelModule`). Dokument jest częścią skilla `soneta-programming`.
Celem jest, aby agent pisał **bezbłędny kod biznesowy** operujący na dokumencie handlowym — fakturach,
dokumentach magazynowych, zamówieniach, ofertach i korektach — trafiający w realne pola, kolekcje i workery
platformy.

> Format **zwarty**: każdy wzorzec opisuje ogólny przypadek + tabelę wariantów, zamiast wielu wąskich
> pozycji. Fundamenty (sesja, transakcja, blokada optymistyczna, praca z `SubTable`, obsługa błędów)
> są opisane w [`safe-code.md`](../safe-code.md), [`session-login.md`](../session-login.md) oraz
> [`worker-extender.md`](../worker-extender.md) — tutaj się do nich odwołujemy, nie powtarzamy ich.
>
> **Cały kod w tym dokumencie jest zgodny z C# 10** (target-typed `new`, `var`, wyrażenia `switch`,
> nazwane parametry `bool`). Snippety operują wyłącznie na **publicznym kontrakcie** platformy — nie
> ma odwołań do prywatnych klas ani kodu źródłowego aplikacji.

## Fakty o typie (zweryfikowane skanem DLL — `scan-props.csx` / `scan-workers.csx`)

- **Klasa biznesowa:** `Soneta.Handel.DokumentHandlowy` — `GuidedRow` (root), tabela `Soneta.Handel.DokHandlowe`
  („Dokumenty handlowe").
- **Jeden typ — wiele rodzajów dokumentów.** Faktury (FV, FZ, PAR), dokumenty magazynowe (PZ, WZ, PW, RW, MM),
  zamówienia (ZO, ZD), oferty (OD, OO), korekty i inne — różni je wyłącznie **`Definicja`
  (`DefDokHandlowego`)**. To definicja wyznacza kierunek magazynu, numerację, sposób liczenia VAT itd.
- **Moduł:** `Soneta.Handel.HandelModule`, dostęp `session.GetHandel()`.
  Tabela dokumentów: `Handel.DokHandlowe`. Definicje: `Handel.DefDokHandlowych` (klucz `WgSymbolu["FV"]`).
- **Implementuje:** `IDokumentPlatny`, `IDokumentKsiegowalny`, `IDokumentKasowy`, `IDaneKontrahentaHost`,
  `IDokumentCRM`, `IKodowany`, `IExportImportXmlHost`, `IElementSlownika`, `IKomunikatEDIHost`,
  `IEmailElement`, `IProceduraVATHost`, `IZrodloOpisuAnalitycznego`.
- **Pola:** 128 bazodanowych + 388 kalkulowanych.

### Kluczowe pola bazodanowe (zapisywalne)

| Pole | Typ | Znaczenie |
|---|---|---|
| `Definicja` | `Soneta.Handel.DefDokHandlowego` | definicja dokumentu — wyznacza rodzaj/zachowanie (ustaw jako pierwszą) |
| `Kontrahent` | `Soneta.CRM.Kontrahent` | kontrahent (nabywca/dostawca) dokumentu |
| `Odbiorca` | `Soneta.CRM.Kontrahent` | odbiorca towarów (gdy inny niż kontrahent) |
| `Magazyn` | `Soneta.Magazyny.Magazyn` | magazyn, na który wpływa dokument |
| `Data` | `Soneta.Types.Date` | data wystawienia |
| `DataOperacji` | `Soneta.Types.Date` | faktyczna data sprzedaży/zakupu |
| `Numer` | `Soneta.Core.NumerDokumentu` | numeracja dokumentu (zob. wzorzec numeracji) |
| `Seria` | `string` | seria dokumentu |
| `Stan` | `Soneta.Handel.StanDokumentuHandlowego` | `Bufor=0`, `Zatwierdzony=1`, `Zablokowany=2`, `Anulowany=3` |
| `LiczonaOd` | `Soneta.Handel.SposobLiczeniaVAT` | liczenie wartości od netto/brutto |
| `KorektaVAT` | `bool` | sumy VAT zmienione ręcznie (niezależne od pozycji) |
| `Waluta` (przez `BruttoCy`) | `Soneta.Types.Currency` | kwota płatności w walucie |
| `TabelaKursowa` | `Soneta.Waluty.TabelaKursowa` | tabela kursów dla dokumentu walutowego |
| `RodzajTransakcji` | `Soneta.Handel.KodRodzajuTransakcji` | rodzaj transakcji Intrastat |
| `Opis` | `Soneta.Business.MemoText` | opis na wydruku |
| `Suma` | `Soneta.Handel.BruttoNetto` | podsumowana wartość dokumentu |

### Kluczowe kolekcje i właściwości kalkulowane (tylko do odczytu, o ile nie zaznaczono)

| Składowa | Typ | Znaczenie |
|---|---|---|
| `Pozycje` | `LpSubTable<PozycjaDokHandlowego>` | pozycje dokumentu |
| `SumyVAT` | `SubTable<SumaVAT>` | tabelka VAT (netto/VAT/brutto wg stawek) |
| `Platnosci` | `SubTable<Soneta.Kasa.Platnosc>` | płatności dokumentu |
| `Obroty` | `SubTable` | obroty magazynowe bezpośrednie dokumentu |
| `ObrotyWszystkie` | `ListWithView` | obroty łącznie z dokumentami zależnymi |
| `Zasoby` | `SubTable` | zasoby magazynowe utworzone przez dokument |
| `DokumentyMagazynowe` | `DokumentHandlowy[]` | dokumenty magazynowe powiązane z fakturą |
| `DokumentyHandlowe` | `DokumentHandlowy[]` | faktury powiązane z dokumentem magazynowym |
| `DokumentKorygowany` | `DokumentHandlowy` | dokument korygowany (kalkulowane — tworzy relacja/UI) |
| `DokumentyKorygujące` | `IEnumerable<DokumentHandlowy>` | korekty tego dokumentu |
| `DokumentyZaliczkowe` | `DokumentHandlowy[]` | nadrzędne dokumenty zaliczkowe |
| `Rezerwacja` | `DokumentHandlowy` | dokument rezerwacji towarów |
| `SumaPozycji` | `BruttoNettoPozycji` | wyliczona suma wartości pozycji |
| `Bufor` / `Zatwierdzony` / `Anulowany` | `bool` | skróty stanu (kalkulowane z `Stan`) |
| `Features` | `Soneta.Business.FeatureCollection` | cechy definiowalne dokumentu |

### Pozycja dokumentu — `Soneta.Handel.PozycjaDokHandlowego`

| Pole | Typ | Znaczenie |
|---|---|---|
| `Towar` | `Soneta.Towary.Towar` | towar pozycji (ustaw pierwszy — inicjuje jednostkę na `Ilosc`/`Cena`) |
| `Ilosc` | `Soneta.Towary.Quantity` | ilość; twórz `new Quantity(wartość, poz.Ilosc.Symbol)` |
| `Cena` | `Soneta.Types.DoubleCy` | cena (netto/brutto wg `LiczonaOd`); `new DoubleCy(wartość, poz.Cena.Symbol)` |
| `Rabat` | `Soneta.Types.Percent` | procent rabatu |
| `Features` | `FeatureCollection` | cechy pozycji (m.in. przeniesione z partii/towaru) |

Konstruktor pozycji wymaga dokumentu: `new PozycjaDokHandlowego(dokument)`.

## Podstawowe typy i obiekty pomocnicze

| Typ | Rola |
|---|---|
| `Soneta.Handel.HandelModule` | moduł Handel: `DokHandlowe`, `DefDokHandlowych` |
| `Soneta.Magazyny.MagazynyModule` | magazyny, zasoby, obroty, partie (`GrupaDostaw`) — `session.GetMagazyny()` |
| `Soneta.Towary.TowaryModule` | towary, jednostki, ceny — `session.GetTowary()` |
| `Soneta.CRM.CRMModule` | kontrahenci — `session.GetCRM()` |
| `Soneta.Handel.DefDokHandlowego` | definicja dokumentu (symbol, kierunek, numeracja, flagi) |
| `Soneta.Types.Quantity` | ilość z jednostką miary |
| `Soneta.Types.DoubleCy` | wartość zmiennoprzecinkowa z walutą (cena) |
| `Soneta.Types.Currency` | kwota z walutą (wartości, płatności) |
| `Soneta.Types.Percent` | procent (rabat, stawka) |
| `Soneta.Types.Date` | data biznesowa |
| `Soneta.Handel.StanDokumentuHandlowego` | stan cyklu życia dokumentu |

## Szablon wzorca

Każdy wzorzec (`HANDEL-Wn`) ma stałą strukturę:

- **Cel** — co robi i kiedy go użyć.
- **Warianty** — tabela odmian przypadku.
- **Pola i typy** — realne właściwości/kolekcje i ich typy.
- **Snippet** — kod C# 10 na publicznym kontrakcie.
- **Pułapki** — typowe błędy i zasady safe-code.

---



## Mapa receptur

| Rozdział | Plik | Receptury |
|---|---|---|
| HANDEL01 — Fundamenty i identyfikacja | [handel/HANDEL01-fundamenty.md](handel/HANDEL01-fundamenty.md) | HANDEL-W1–W3 |
| HANDEL02 — Wystawianie dokumentów | [handel/HANDEL02-wystawianie.md](handel/HANDEL02-wystawianie.md) | HANDEL-W4–W11 |
| HANDEL03 — Stany dokumentu i cykl życia | [handel/HANDEL03-cykl-zycia.md](handel/HANDEL03-cykl-zycia.md) | HANDEL-W12–W16 |
| HANDEL04 — Relacje i generowanie dokumentów | [handel/HANDEL04-relacje.md](handel/HANDEL04-relacje.md) | HANDEL-W17–W24 |
| HANDEL05 — Odczyt i wyszukiwanie | [handel/HANDEL05-odczyt.md](handel/HANDEL05-odczyt.md) | HANDEL-W25–W30 |
| HANDEL06 — Magazyn, zasoby, partie, obroty | [handel/HANDEL06-magazyn.md](handel/HANDEL06-magazyn.md) | HANDEL-W31–W39 |
| HANDEL07 — Cechy (Features) | [handel/HANDEL07-cechy.md](handel/HANDEL07-cechy.md) | HANDEL-W40–W42 |
| HANDEL08 — VAT, wartości i waluty | [handel/HANDEL08-vat-waluty.md](handel/HANDEL08-vat-waluty.md) | HANDEL-W43–W47 |
| HANDEL09 — Korekty i dokumenty specjalne | [handel/HANDEL09-korekty.md](handel/HANDEL09-korekty.md) | HANDEL-W48–W52 |
| HANDEL10 — Operacje zbiorcze (batch) | [handel/HANDEL10-batch.md](handel/HANDEL10-batch.md) | HANDEL-W53–W55 |
| HANDEL11 — Operacje pomocnicze (przekrojowe) | [handel/HANDEL11-pomocnicze.md](handel/HANDEL11-pomocnicze.md) | HANDEL-W56–W61 |
| HANDEL12 — Wydruki i raporty | [handel/HANDEL12-wydruki.md](handel/HANDEL12-wydruki.md) | HANDEL-W62–W66 |
| HANDEL13 — Tematy specjalistyczne (KSeF, fiskalizacja, kompletacja, Intrastat) | [handel/HANDEL13-specjalistyczne.md](handel/HANDEL13-specjalistyczne.md) | HANDEL-W67–W74 |
| HANDEL14 — Płatności dokumentu handlowego | [handel/HANDEL14-platnosci.md](handel/HANDEL14-platnosci.md) | HANDEL-W75–W82 |

## Powiązane dokumenty

- [`safe-code.md`](../safe-code.md) — sesja, transakcje, blokada optymistyczna, zasady bezpiecznego kodu.
- [`session-login.md`](../session-login.md) — `Session`, `Login`, `Database`.
- [`worker-extender.md`](../worker-extender.md) — workery, akcje menu Czynności, bindowanie.
- [`rowcondition.md`](../rowcondition.md) — serwerowy LINQ, `RowCondition`, `SubTable[condition]`.
- [`features.md`](../features.md) — cechy (`Features`), typy, dostęp typowany/nietypowany.
- [`datapack-guidedrow.md`](../datapack-guidedrow.md) — eksport/import, `GuidedRow`.
- [`crm.md`](crm.md) — receptury dla `Kontrahent` (nabywca/odbiorca/płatnik dokumentu).
- [`scan-props.md`](../scan-props.md) / [`scan-workers.md`](../scan-workers.md) — inwentaryzacja pól i workerów.
