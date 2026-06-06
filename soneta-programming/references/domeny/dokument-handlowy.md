# Dokument handlowy — receptury kodu biznesowego (Soneta / enova365)

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

## Spis treści

- [1. Fundamenty i identyfikacja](#1-fundamenty-i-identyfikacja)
  - [W1 — Dostęp do modułów handlowo-magazynowych i tabeli `DokHandlowe`](#w1--dostęp-do-modułów-handlowo-magazynowych-i-tabeli-dokhandlowe)
  - [W2 — Wybór definicji dokumentu (`DefDokHandlowego`) wg symbolu](#w2--wybór-definicji-dokumentu-defdokhandlowego-wg-symbolu)
  - [W3 — Rozpoznanie rodzaju dokumentu (faktura / magazynowy / zamówienie / korekta / zaliczka)](#w3--rozpoznanie-rodzaju-dokumentu-faktura--magazynowy--zamówienie--korekta--zaliczka)
- [2. Wystawianie dokumentów](#2-wystawianie-dokumentów)
  - [W4 — Faktura sprzedaży (FV)](#w4--faktura-sprzedaży-fv)
  - [W5 — Faktura zakupu (FZ)](#w5--faktura-zakupu-fz)
  - [W6 — Dokument magazynowy (PZ / WZ / RW / PW)](#w6--dokument-magazynowy-pz--wz--rw--pw)
  - [W7 — Zamówienie (ZO / ZD)](#w7--zamówienie-zo--zd)
  - [W8 — Dodawanie pozycji (towar, ilość, cena, rabat, jednostka)](#w8--dodawanie-pozycji-towar-ilość-cena-rabat-jednostka)
  - [W9 — Dokument w walucie obcej](#w9--dokument-w-walucie-obcej)
  - [W10 — Dokument z usługą (pozycja usługowa bez wpływu na magazyn)](#w10--dokument-z-usługą-pozycja-usługowa-bez-wpływu-na-magazyn)
  - [W11 — Odbiorca / płatnik inny niż kontrahent + miejsce dostawy](#w11--odbiorca--płatnik-inny-niż-kontrahent--miejsce-dostawy)
- [3. Stany dokumentu i cykl życia](#3-stany-dokumentu-i-cykl-życia)
  - [W12 — Zatwierdzenie dokumentu (bufor → zatwierdzony)](#w12--zatwierdzenie-dokumentu-bufor--zatwierdzony)
  - [W13 — Cofnięcie do bufora / odtwierdzenie](#w13--cofnięcie-do-bufora--odtwierdzenie)
  - [W14 — Anulowanie dokumentów](#w14--anulowanie-dokumentów)
  - [W15 — Naprawa i przeliczenie stanu dokumentu](#w15--naprawa-i-przeliczenie-stanu-dokumentu)
  - [W16 — Bezpieczne usunięcie dokumentu z bufora i obsługa zależności](#w16--bezpieczne-usunięcie-dokumentu-z-bufora-i-obsługa-zależności)
- [4. Relacje i generowanie dokumentów](#4-relacje-i-generowanie-dokumentów)
  - [W17 — Generowanie faktury z zamówienia (ZO → FV)](#w17--generowanie-faktury-z-zamówienia-zo--fv)
  - [W18 — Zbiorczy dokument magazynowy z wielu faktur (wiele FA → 1 WZ/PZ)](#w18--zbiorczy-dokument-magazynowy-z-wielu-faktur-wiele-fa--1-wzpz)
  - [W19 — Zbiorcza faktura z wielu dokumentów magazynowych (wiele WZ → 1 FA)](#w19--zbiorcza-faktura-z-wielu-dokumentów-magazynowych-wiele-wz--1-fa)
  - [W20 — Wyszukiwanie dokumentów powiązanych (odczyt pól kalkulowanych)](#w20--wyszukiwanie-dokumentów-powiązanych-odczyt-pól-kalkulowanych)
  - [W21 — Generowanie dokumentu magazynowego z faktury (FA → WZ pojedynczo)](#w21--generowanie-dokumentu-magazynowego-z-faktury-fa--wz-pojedynczo)
  - [W22 — Kopiowanie faktury klientowi (`KopiujKlientowiFaktureWorker`)](#w22--kopiowanie-faktury-klientowi-kopiujklientowifaktureworker)
  - [W23 — Ręczne wiązanie i rozwiązywanie powiązań](#w23--ręczne-wiązanie-i-rozwiązywanie-powiązań)
  - [W24 — Odczyt łańcucha powiązań i stan pokrycia zamówienia](#w24--odczyt-łańcucha-powiązań-i-stan-pokrycia-zamówienia)
- [5. Odczyt i wyszukiwanie](#5-odczyt-i-wyszukiwanie)
  - [W25 — Odczytanie pozycji dokumentu](#w25--odczytanie-pozycji-dokumentu)
  - [W26 — Odczytanie dokumentów dla kontrahenta](#w26--odczytanie-dokumentów-dla-kontrahenta)
  - [W27 — Ostatnie pozycje dokumentów dla wskazanego towaru](#w27--ostatnie-pozycje-dokumentów-dla-wskazanego-towaru)
  - [W28 — Wyszukiwanie dokumentów wg okresu, definicji, stanu, serii](#w28--wyszukiwanie-dokumentów-wg-okresu-definicji-stanu-serii)
  - [W29 — Odczyt dokumentu wg numeru lub Guid](#w29--odczyt-dokumentu-wg-numeru-lub-guid)
  - [W30 — Korekty dokumentu i dokument korygowany](#w30--korekty-dokumentu-i-dokument-korygowany)
- [6. Magazyn, zasoby, partie, obroty](#6-magazyn-zasoby-partie-obroty)
  - [W31 — Przeglądanie zasobów utworzonych przez dokument przychodowy (`dok.Zasoby`)](#w31--przeglądanie-zasobów-utworzonych-przez-dokument-przychodowy-dokzasoby)
  - [W32 — Przetwarzanie obrotów faktury sprzedaży i dokumentu rozchodowego (`dok.Obroty`, `dok.ObrotyWszystkie`)](#w32--przetwarzanie-obrotów-faktury-sprzedaży-i-dokumentu-rozchodowego-dokobroty-dokobrotywszystkie)
  - [W33 — Odczyt stanu magazynowego towaru (magazyn / data) — `mag.Zasoby` z filtrem](#w33--odczyt-stanu-magazynowego-towaru-magazyn--data--magzasoby-z-filtrem)
  - [W34 — Wyszukiwanie partii magazynowych (`GrupaDostaw`) według cech](#w34--wyszukiwanie-partii-magazynowych-grupadostaw-według-cech)
  - [W35 — Dokument rozchodowy ze wskazaniem JEDNEJ partii](#w35--dokument-rozchodowy-ze-wskazaniem-jednej-partii)
  - [W36 — Dokument rozchodowy ze wskazaniem WIELU partii](#w36--dokument-rozchodowy-ze-wskazaniem-wielu-partii)
  - [W37 — Dokument przyjęcia (PW/PZ) z numerem serii — zapis numeru serii jako cecha](#w37--dokument-przyjęcia-pwpz-z-numerem-serii--zapis-numeru-serii-jako-cecha)
  - [W38 — Odczyt rozchodu zasobów: powiązanie pozycji rozchodu z partią pierwotną / przyjęciem](#w38--odczyt-rozchodu-zasobów-powiązanie-pozycji-rozchodu-z-partią-pierwotną--przyjęciem)
  - [W39 — Odczyt okresów magazynowych i kontekstu wyceny (FIFO/LIFO/wg dostaw)](#w39--odczyt-okresów-magazynowych-i-kontekstu-wyceny-fifolifowg-dostaw)
- [7. Cechy (Features)](#7-cechy-features)
  - [W40 — Przenoszenie cech z partii (dostawy) / towaru na pozycję dokumentu](#w40--przenoszenie-cech-z-partii-dostawy--towaru-na-pozycję-dokumentu)
  - [W41 — Odczyt i zapis cech dokumentu / pozycji (`Features`)](#w41--odczyt-i-zapis-cech-dokumentu--pozycji-features)
  - [W42 — Filtrowanie / wyszukiwanie dokumentów i partii po wartości cechy (serwerowo)](#w42--filtrowanie--wyszukiwanie-dokumentów-i-partii-po-wartości-cechy-serwerowo)
- [8. VAT, wartości i waluty](#8-vat-wartości-i-waluty)
  - [W43 — Odczytanie tabeli VAT (`SumyVAT`)](#w43--odczytanie-tabeli-vat-sumyvat)
  - [W44 — Odczyt podsumowań wartości dokumentu](#w44--odczyt-podsumowań-wartości-dokumentu)
  - [W45 — Ręczna korekta tabeli VAT (`KorektaVAT`)](#w45--ręczna-korekta-tabeli-vat-korektavat)
  - [W46 — Sposób liczenia VAT (`LiczonaOd`) i przeliczenie procedur VAT](#w46--sposób-liczenia-vat-liczonaod-i-przeliczenie-procedur-vat)
  - [W47 — Zmiana waluty dokumentu i cen](#w47--zmiana-waluty-dokumentu-i-cen)
- [9. Korekty i dokumenty specjalne](#9-korekty-i-dokumenty-specjalne)
  - [W48 — Korekta ilościowa i korekta ceny](#w48--korekta-ilościowa-i-korekta-ceny)
  - [W49 — Korekta wartości przyjęcia magazynowego](#w49--korekta-wartości-przyjęcia-magazynowego)
  - [W50 — Dokument inwentaryzacji (INW)](#w50--dokument-inwentaryzacji-inw)
  - [W51 — Faktura zaliczkowa i jej rozliczenie dokumentem końcowym](#w51--faktura-zaliczkowa-i-jej-rozliczenie-dokumentem-końcowym)
  - [W52 — Przesunięcie międzymagazynowe (MM)](#w52--przesunięcie-międzymagazynowe-mm)
- [10. Operacje zbiorcze (batch)](#10-operacje-zbiorcze-batch)
  - [W53 — Ewidencjonowanie / eksport do księgowości wielu dokumentów](#w53--ewidencjonowanie--eksport-do-księgowości-wielu-dokumentów)
  - [W54 — Hurtowe zatwierdzanie / generowanie dokumentów dla zaznaczonego zbioru](#w54--hurtowe-zatwierdzanie--generowanie-dokumentów-dla-zaznaczonego-zbioru)
  - [W55 — Wydajne przetwarzanie wielu dokumentów w jednej sesji (paczki)](#w55--wydajne-przetwarzanie-wielu-dokumentów-w-jednej-sesji-paczki)
- [11. Operacje pomocnicze (przekrojowe)](#11-operacje-pomocnicze-przekrojowe)
  - [W56 — Bezpieczne pobranie / utworzenie kontrahenta i towaru pozycji](#w56--bezpieczne-pobranie--utworzenie-kontrahenta-i-towaru-pozycji)
  - [W57 — Przeliczanie jednostek miary towaru przy dodawaniu pozycji](#w57--przeliczanie-jednostek-miary-towaru-przy-dodawaniu-pozycji)
  - [W58 — Walidacja przed zatwierdzeniem (kompletność, zasób, limit kredytowy)](#w58--walidacja-przed-zatwierdzeniem-kompletność-zasób-limit-kredytowy)
  - [W59 — Obsługa błędów i blokada optymistyczna (kolizje `Save`, ponowienie)](#w59--obsługa-błędów-i-blokada-optymistyczna-kolizje-save-ponowienie)
  - [W60 — Odczyt metadanych dokumentu (`ChangeInfos` — kto/kiedy założył i zmienił)](#w60--odczyt-metadanych-dokumentu-changeinfos--ktokiedy-założył-i-zmienił)
  - [W61 — Praca z definicjami i numeracją (seria, wymuszenie numeru, bufor `Numer`)](#w61--praca-z-definicjami-i-numeracją-seria-wymuszenie-numeru-bufor-numer)
- [12. Wydruki i raporty](#12-wydruki-i-raporty)
  - [W62 — Wydruk faktury do PDF / na drukarkę](#w62--wydruk-faktury-do-pdf--na-drukarkę)
  - [W63 — Wydruk dokumentu magazynowego (PZ/WZ/MM)](#w63--wydruk-dokumentu-magazynowego-pzwzmm)
  - [W64 — Raport dobowy i okresowy (zestawienie za dzień / okres)](#w64--raport-dobowy-i-okresowy-zestawienie-za-dzień--okres)
  - [W65 — Wydruk zbiorczy dla zaznaczonego zbioru dokumentów](#w65--wydruk-zbiorczy-dla-zaznaczonego-zbioru-dokumentów)
  - [W66 — Zapis wydruku do strumienia/pliku (integracja, e-mail)](#w66--zapis-wydruku-do-strumieniapliku-integracja-e-mail)
- [13. Tematy specjalistyczne (KSeF, fiskalizacja, kompletacja, Intrastat)](#13-tematy-specjalistyczne-ksef-fiskalizacja-kompletacja-intrastat)
  - [W67 — Wysłanie faktury do KSeF (pojedynczo i zbiorczo)](#w67--wysłanie-faktury-do-ksef-pojedynczo-i-zbiorczo)
  - [W68 — Sprawdzenie statusu KSeF i odczyt numeru KSeF](#w68--sprawdzenie-statusu-ksef-i-odczyt-numeru-ksef)
  - [W69 — UPO, numer KSeF z duplikatu, walidacja struktury XML](#w69--upo-numer-ksef-z-duplikatu-walidacja-struktury-xml)
  - [W70 — Import faktur z KSeF (dokumenty zakupu)](#w70--import-faktur-z-ksef-dokumenty-zakupu)
  - [W71 — Fiskalizacja dokumentu (paragon fiskalny)](#w71--fiskalizacja-dokumentu-paragon-fiskalny)
  - [W72 — E-paragon (e-mail) i ponowny wydruk paragonu](#w72--e-paragon-e-mail-i-ponowny-wydruk-paragonu)
  - [W73 — Dokument kompletacji (złożenie / rozłożenie kompletu)](#w73--dokument-kompletacji-złożenie--rozłożenie-kompletu)
  - [W74 — Intrastat (dane statystyczne i wyszukanie dokumentów do deklaracji)](#w74--intrastat-dane-statystyczne-i-wyszukanie-dokumentów-do-deklaracji)
- [14. Płatności dokumentu handlowego](#14-płatności-dokumentu-handlowego)
  - [W75 — Przeglądanie płatności dokumentu](#w75--przeglądanie-płatności-dokumentu)
  - [W76 — Rozbicie płatności na raty](#w76--rozbicie-płatności-na-raty)
  - [W77 — Ręczne dodanie / edycja pojedynczej płatności](#w77--ręczne-dodanie--edycja-pojedynczej-płatności)
  - [W78 — Warunki płatności z kontrahenta i ich przeliczenie na dokumencie](#w78--warunki-płatności-z-kontrahenta-i-ich-przeliczenie-na-dokumencie)
  - [W79 — Zmiana płatnika (inny niż kontrahent)](#w79--zmiana-płatnika-inny-niż-kontrahent)
  - [W80 — Odczyt stanu rozliczenia płatności](#w80--odczyt-stanu-rozliczenia-płatności)
  - [W81 — Płatności w walucie obcej (kwota w walucie vs PLN, kurs)](#w81--płatności-w-walucie-obcej-kwota-w-walucie-vs-pln-kurs)
  - [W82 — Powiązanie płatności z terminem i rabatem za wcześniejszą zapłatę](#w82--powiązanie-płatności-z-terminem-i-rabatem-za-wcześniejszą-zapłatę)

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

Każdy wzorzec (`Wn`) ma stałą strukturę:

- **Cel** — co robi i kiedy go użyć.
- **Warianty** — tabela odmian przypadku.
- **Pola i typy** — realne właściwości/kolekcje i ich typy.
- **Snippet** — kod C# 10 na publicznym kontrakcie.
- **Pułapki** — typowe błędy i zasady safe-code.

---


## 1. Fundamenty i identyfikacja

> Rozdział opisuje, jak z poziomu sesji dotrzeć do modułów handlowo-magazynowych, jak poprawnie
> wskazać **definicję dokumentu** (`DefDokHandlowego`) zanim utworzysz dokument, oraz jak na podstawie
> definicji i flag dokumentu **rozpoznać jego rodzaj** (faktura / magazynowy / zamówienie / korekta /
> zaliczka). Cały kod jest zgodny z **C# 10** i operuje wyłącznie na **publicznym kontrakcie**
> platformy. Fundamenty wspólne (sesja, transakcja `session.Logout(true)` + `Commit`/`CommitUI`,
> blokada optymistyczna, praca z `SubTable`) opisują [`safe-code.md`](../safe-code.md),
> [`session-login.md`](../session-login.md) oraz [`worker-extender.md`](../worker-extender.md) — tutaj
> się do nich odwołujemy, nie powtarzamy ich.

### W1 — Dostęp do modułów handlowo-magazynowych i tabeli `DokHandlowe`

**Cel:** z obiektu `Session` (lub dowolnego `ISessionable` — `Row`, `Table`, `Context`) dotrzeć do
modułów, na których opiera się logika handlu i magazynu, oraz do tabeli dokumentów `DokHandlowe`.
To punkt wejścia każdego scenariusza w tym dokumencie.

**Warianty:**

| Wariant | Wywołanie (extension method na `Session`) | Co udostępnia |
|---|---|---|
| Moduł handlowy | `session.GetHandel()` → `HandelModule` | `.DokHandlowe` (tabela dokumentów), `.DefDokHandlowych` (definicje) |
| Moduł magazynowy | `session.GetMagazyny()` → `MagazynyModule` | `.Magazyny`, `.Zasoby`, `.Obroty`, `.GrupyDostaw` (partie), `.OkresyMag` |
| Moduł towarów | `session.GetTowary()` → `TowaryModule` | `.Towary`, `.Jednostki` |
| Moduł CRM | `session.GetCRM()` → `CRMModule` | `.Kontrahenci` |
| Moduł kasowy | `session.GetKasa()` → `KasaModule` | formy płatności, rozrachunki (dot. płatności dokumentu) |
| Waluty | `Soneta.Waluty.WalutyModule.GetInstance(session)` | `.Waluty`, `.TabeleKursowe` |

**Pola i typy:** `HandelModule.DokHandlowe: DokHandlowe` (tabela `DokumentHandlowy`),
`HandelModule.DefDokHandlowych` (tabela `DefDokHandlowego`),
`MagazynyModule.Magazyny`, `TowaryModule.Towary`, `CRMModule.Kontrahenci`. Wszystkie moduły
implementują `ISessionable` i mają property `.Session`.

**Snippet:**

```csharp
// Punkt wejścia — z sesji pobieramy moduły handlowo-magazynowe:
var handel    = session.GetHandel();      // HandelModule
var magazyny  = session.GetMagazyny();    // MagazynyModule
var towary    = session.GetTowary();      // TowaryModule
var crm       = session.GetCRM();         // CRMModule

// Tabela dokumentów handlowych (operacyjna, guided):
var dokumenty = handel.DokHandlowe;

// Iteracja po dokumentach — ZAWSZE zawężaj zakres (data/definicja/kontrahent),
// to tabela operacyjna rosnąca z biznesem. Filtr aplikujemy na indeksie (warunek serwerowy):
var od = Date.Today.AddMonths(-1);
foreach (DokumentHandlowy d in handel.DokHandlowe.WgDaty[(DokumentHandlowy x) => x.Data >= od])
{
    // d.* — Numer, Data, Definicja, Kontrahent, Suma, Stan ...
}

// Z dowolnego ISessionable można zejść do modułu również metodą GetInstance:
var hm = Soneta.Handel.HandelModule.GetInstance(jakisRow);   // gdy nie mamy zmiennej Session
```

**Pułapki:**
- Moduł i tabela są **single-threaded** — nie współdziel ich między wątkami; pobieraj je z sesji
  bieżącego wątku (thread-safety w SKILL.md).
- `session.GetWaluty()` jest **internal** — z dodatku zewnętrznego użyj
  `Soneta.Waluty.WalutyModule.GetInstance(session)`.
- **Nie ładuj całej tabeli `DokHandlowe`** do pamięci z `if`-em w pętli. Filtruj serwerowo —
  warunek aplikuj na indeksie tabeli (np. `WgDaty[(DokumentHandlowy x) => …]`), żeby wykonał się
  po stronie SQL (safe-code §6). W warunku `RowCondition` używaj **tylko pól bazodanowych** — pola
  kalkulowane rzucą `LinqConditionException`.
- Pobranie modułu nie tworzy ani nie modyfikuje danych — modyfikacje zawsze w transakcji
  (`session.Logout(true)` + `Commit`/`CommitUI`, potem `Save`).

### W2 — Wybór definicji dokumentu (`DefDokHandlowego`) wg symbolu

**Cel:** zanim utworzysz dokument, musisz wskazać jego **definicję** — to ona określa typ dokumentu
(sprzedaż, zakup, magazynowy, zamówienie…), numerację, zachowanie magazynu i płatności. Definicja
jest **pierwszym** ustawianym polem nowego dokumentu (`dok.Definicja = …`), zanim ustawisz magazyn,
kontrahenta czy pozycje.

**Warianty:**

| Wariant | Klucz / mechanizm | Uwaga |
|---|---|---|
| Po symbolu | `DefDokHandlowych.WgSymbolu["FV"]` | indeks **unikalny** — zwraca pojedynczy rekord lub `null` |
| Filtr po kategorii (typie) | `DefDokHandlowych.WgKategorii[KategoriaHandlowa.Sprzedaż]` | zbiór wszystkich definicji danej kategorii |
| Po symbolu w obrębie kategorii | warunek serwerowy na `WgSymbolu` + sprawdzenie `Kategoria` | gdy w bazie istnieje kilka wariantów sprzedaży |
| Walidacja istnienia | `WgSymbolu[symbol] != null` | brak definicji = nie da się utworzyć dokumentu |

Typowe symbole w bazie Demo: **FV** (faktura sprzedaży), **FZ** (faktura zakupu), **PAR** (paragon),
**PZ**/**PW** (przyjęcia magazynowe), **WZ**/**RW** (rozchody magazynowe), **ZO** (zamówienie
odbiorcy), **ZD** (zamówienie do dostawcy), **MM** (przesunięcie międzymagazynowe),
**INW** (inwentaryzacja), **KS** (korekta sprzedaży). Symbole zależą od konfiguracji konkretnej bazy —
nie zakładaj ich „na sztywno", weryfikuj `!= null`.

**Pola i typy:** `DefDokHandlowego.Symbol: string` (maks. 12 znaków, unikalny),
`DefDokHandlowego.Kategoria: Soneta.Handel.KategoriaHandlowa`. Indeks `WgSymbolu` jest unikalny
(zwraca pojedynczy rekord), `WgKategorii` grupuje definicje po kategorii.

**Snippet:**

```csharp
var handel = session.GetHandel();

// 1. Po symbolu — klucz unikalny: pojedynczy rekord albo null
DefDokHandlowego defFV = handel.DefDokHandlowych.WgSymbolu["FV"];
if (defFV == null)
    throw new BusException("Brak definicji dokumentu o symbolu FV w tej bazie.".Translate());

// 2. Wszystkie definicje danej kategorii (np. wszystkie definicje sprzedaży):
foreach (DefDokHandlowego d in handel.DefDokHandlowych.WgKategorii[KategoriaHandlowa.Sprzedaż])
{
    // d.Symbol, d.Kategoria ...
}

// 3. Użycie definicji przy tworzeniu dokumentu — Definicja USTAWIANA PIERWSZA:
using (var t = session.Logout(editMode: true))
{
    var dok = new DokumentHandlowy();
    session.AddRow(dok);                                       // AddRow przed ustawianiem pól
    dok.Definicja = handel.DefDokHandlowych.WgSymbolu["PW"];   // definicja jako pierwsze pole
    // dok.Magazyn / dok.Kontrahent ustawiamy dopiero PO definicji (gdy definicja ich wymaga)
    t.Commit();                                                // CommitUI() w workerze/extenderze
}
session.Save();
```

**Pułapki:**
- `WgSymbolu[...]` zwraca **pojedynczy** rekord (klucz unikalny) i może być `null` — zawsze sprawdź
  przed użyciem. `WgKategorii[...]` zwraca **zbiór** — iteruj lub `.FirstOrDefault()`.
- **Definicja musi być ustawiona jako pierwsze pole** dokumentu — od niej zależy widoczność i
  wymagalność pozostałych pól (magazyn, kontrahent, numeracja). Ustawienie magazynu/kontrahenta
  przed definicją jest błędem.
- Symbole **nie są gwarantowane** — zależą od konfiguracji bazy klienta. Nie polegaj na obecności
  „FV"/„WZ"; pobierz definicję i sprawdź `!= null`, a w razie potrzeby filtruj po `Kategoria`.
- `DefDokHandlowego` to dane **konfiguracyjne** (`GuidedRow`) — odczytuj je, nie twórz „w locie" w
  kodzie operacyjnym.

### W3 — Rozpoznanie rodzaju dokumentu (faktura / magazynowy / zamówienie / korekta / zaliczka)

**Cel:** ustalić, „czym jest" dany dokument — fakturą, dokumentem magazynowym, zamówieniem, korektą
czy dokumentem zaliczkowym — by rozgałęzić logikę (np. inaczej traktować rozchód magazynowy niż
zamówienie). Rozpoznanie opiera się na **kategorii definicji** (`Definicja.Kategoria`) oraz na
gotowych flagach dokumentu (`Korekta`, `JestDokZaliczkowy()`).

**Warianty:**

| Co rozpoznajemy | Mechanizm (publiczny kontrakt) | Wartości / zakres `KategoriaHandlowa` |
|---|---|---|
| Faktura/handlowy (sprzedaż, zakup, korekty, f. wewnętrzna) | `Definicja.Kategoria` w zakresie handlowym | `Sprzedaż=2`, `KorektaSprzedaży=3`, `Zakup=4`, `KorektaZakupu=5`, `FakturaWewnętrzna=6` (zakres `HandelPierwszy=1 … HandelOstatni=100`) |
| Magazynowy (PW/PZ/WZ/RW/MM/INW…) | `Definicja.Kategoria` w zakresie magazynowym | `PrzyjęcieMagazynowe=102`, `WydanieMagazynowe=104`, `PrzesunięcieMagazynowe=106`, `Inwentaryzacja=107` … (zakres `MagazynPierwszy=101 … MagazynOstatni=200`) |
| Zamówienie (ZO/ZD/wewn.) | `Definicja.Kategoria` | `ZamówienieOdbiorcy=302`, `ZamówienieDostawcy=303`, `ZamówienieWewnętrzne=312` |
| Korekta | flaga `dok.Korekta` **lub** kategoria typu `Korekta*` | `dok.Korekta == true`; kategorie: `KorektaSprzedaży`, `KorektaZakupu`, `KorektaPrzyjęciaMagazynowego`, `KorektaWydaniaMagazynowego` … |
| Dokument zaliczkowy | metoda `dok.JestDokZaliczkowy()` / `dok.JestDokZaliczkowy(out bool korekta)` | `true` = zaliczkowy; `out korekta` = korekta zaliczki |

**Pola i typy:**
- `DokumentHandlowy.Definicja: Soneta.Handel.DefDokHandlowego` — definicja dokumentu.
- `DefDokHandlowego.Kategoria: Soneta.Handel.KategoriaHandlowa` — **kluczowy** wyznacznik rodzaju.
- `DokumentHandlowy.Korekta: bool` (kalkulowane, read-only) — czy dokument jest korektą.
- `DokumentHandlowy.JestDokZaliczkowy(): bool` oraz `JestDokZaliczkowy(out bool korekta): bool` —
  rozpoznanie zaliczki (drugi przeciążony wariant zwraca też, czy to korekta zaliczki).
- `DefDokHandlowego.Symbol: string` — symbol (do logów / komunikatów).

Enum `Soneta.Handel.KategoriaHandlowa` (wartości publiczne) ma czytelne **markery zakresów**:
`HandelPierwszy=1`/`HandelOstatni=100`, `MagazynPierwszy=101`/`MagazynOstatni=200`,
`PozostałePierwszy=301`/`PozostałeOstatni=400`. Pozwalają one rozpoznać „grupę" dokumentu zakresem,
bez wyliczania wszystkich symboli.

**Snippet:**

```csharp
// Rozpoznanie rodzaju dokumentu na podstawie kategorii jego definicji + flag dokumentu.
// KategoriaHandlowa to enum — markery zakresów (HandelPierwszy/Ostatni, MagazynPierwszy/Ostatni)
// pozwalają klasyfikować grupę dokumentu bez wymieniania wszystkich symboli.
static string RozpoznajRodzaj(DokumentHandlowy dok)
{
    KategoriaHandlowa kat = dok.Definicja.Kategoria;

    // Zaliczka i korekta mają dedykowane, jednoznaczne testy — sprawdzamy je najpierw:
    if (dok.JestDokZaliczkowy(out bool korektaZaliczki))
        return korektaZaliczki ? "Korekta zaliczki" : "Dokument zaliczkowy";

    if (dok.Korekta)
        return "Korekta";

    // Klasyfikacja grupy po zakresie wartości enuma (markery są publiczne):
    return kat switch
    {
        >= KategoriaHandlowa.HandelPierwszy  and <= KategoriaHandlowa.HandelOstatni  => "Faktura / dokument handlowy",
        >= KategoriaHandlowa.MagazynPierwszy and <= KategoriaHandlowa.MagazynOstatni => "Dokument magazynowy",
        KategoriaHandlowa.ZamówienieOdbiorcy
            or KategoriaHandlowa.ZamówienieDostawcy
            or KategoriaHandlowa.ZamówienieWewnętrzne                                => "Zamówienie",
        _ => "Inny"
    };
}

// Przykład użycia — rozgałęzienie logiki po rodzaju:
DokumentHandlowy dok = session.GetHandel().DokHandlowe.WgDaty[
    (DokumentHandlowy d) => d.Data == Date.Today].FirstOrDefault();

if (dok != null && dok.Definicja.Kategoria == KategoriaHandlowa.WydanieMagazynowe)
{
    // ... logika dotycząca rozchodu magazynowego
}
```

**Pułapki:**
- **Rodzaj wynika z definicji, nie z symbolu.** Symbol (np. „FV") jest dowolny i zależny od bazy —
  rozpoznawaj po `Definicja.Kategoria`, a nie po porównaniu `Symbol == "FV"`.
- Pomocnicze metody rozszerzające na enumie (`JestHandlowa`, `JestMagazynowa`, `JestZamowienie`)
  są **`internal`** — z dodatku zewnętrznego ich nie wywołasz. Klasyfikuj **zakresami markerów**
  (`>= HandelPierwszy and <= HandelOstatni` itd.) lub porównaniem do konkretnych wartości — tak jak
  w snippetcie.
- Wartości `*Pierwszy`/`*Ostatni` są oznaczone `[Hidden]` (nie pokazują się w UI), ale to **publiczne**
  stałe enuma — wolno ich użyć w kodzie jako granic zakresu.
- `Korekta` i wyniki `JestDokZaliczkowy()` są **kalkulowane (read-only)** — służą tylko do odczytu;
  nie próbuj ich ustawiać. Korektę tworzy się przez relacje dokumentów (`IRelacjeService.NowaKorekta`),
  a nie przez przestawienie flagi.
- Sprawdzaj zaliczkę/korektę **przed** klasyfikacją zakresową: korekta sprzedaży nadal mieści się w
  zakresie handlowym, a zaliczka bywa fakturą — dedykowane testy (`JestDokZaliczkowy`, `Korekta`)
  są bardziej szczegółowe i powinny mieć pierwszeństwo.
- `dok.Definicja` może w teorii być `null` na świeżo utworzonym, jeszcze nieskonfigurowanym
  dokumencie — przy klasyfikacji dokumentów „w trakcie tworzenia" zabezpiecz dostęp do `Kategoria`.

---

## 2. Wystawianie dokumentów

Rozdział pokazuje, jak **utworzyć dokument handlowy od zera** w różnych wariantach (faktura
sprzedaży, faktura zakupu, dokument magazynowy, zamówienie, dokument walutowy, dokument z usługą)
oraz jak **dodawać i parametryzować pozycje**. Wszystkie wzorce operują na publicznym kontrakcie
platformy: tabela `DokHandlowe` (`session.GetHandel().DokHandlowe`), definicje
`DefDokHandlowych.WgSymbolu[...]`, pozycje `PozycjaDokHandlowego`.

> **Kolejność ustawiania pól jest istotna.** Najpierw `AddRow(dok)`, potem `Definicja` (inicjuje
> kategorię, kierunek magazynu, sposób liczenia VAT, walutę płatności), następnie `Magazyn`,
> `Kontrahent`, daty. Na pozycji najpierw `Towar` (inicjuje jednostkę, stawkę VAT, cenę i rabat),
> dopiero potem `Ilosc`, `Cena`, `Rabat`. Cała operacja w jednej transakcji
> `session.Logout(editMode: true)` zakończonej `Commit()` (kod biznesowy) / `CommitUI()`
> (worker/extender), a po niej `session.Save()` — dopiero `Save()` księguje obroty magazynowe i
> wykrywa konflikty.

---

### W4 — Faktura sprzedaży (FV)

**Cel:** wystawić fakturę sprzedaży: dokument rozchodowy z kontrahentem-nabywcą, pozycjami
towarowymi, automatycznie wyliczoną tabelą VAT i płatnością.

**Warianty:**

| Wariant | Charakterystyka | Pola krytyczne |
|---|---|---|
| FV krajowa od netto | standardowa sprzedaż | `Definicja=FV`, `LiczonaOd=Netto`, `Kontrahent` krajowy |
| FV liczona od brutto | sprzedaż detaliczna / paragonowa | `LiczonaOd=Brutto` |
| FV z rabatem nagłówkowym | rabat przepisywany na pozycje | `Rabat: Percent` na dokumencie |
| FV dla odbiorcy unijnego | WDT — stawka 0% | kontrahent `RodzajPodmiotu=Unijny`, stawka z karty/UE (W11) |
| FV walutowa | sprzedaż w EUR/USD | patrz **W9** |

**Pola i typy:** `Definicja: DefDokHandlowego` (`DefDokHandlowych.WgSymbolu["FV"]`),
`Magazyn: Magazyn`, `Kontrahent: Kontrahent`, `Data: Date` (data wystawienia),
`DataOperacji: Date` (faktyczna data sprzedaży), `LiczonaOd: SposobLiczeniaVAT` (`Netto`/`Brutto`),
`Rabat: Percent`. Wartości wyliczane: `Suma: BruttoNetto`, `SumyVAT: SubTable<SumaVAT>`,
`Platnosci: SubTable<Soneta.Kasa.Platnosc>` (powstaje automatycznie wg formy/terminu kontrahenta).

**Snippet:**

```csharp
var handel = session.GetHandel();
var magazyny = session.GetMagazyny();
var crm = session.GetCRM();

using (var t = session.Logout(editMode: true))
{
    var fv = new DokumentHandlowy();
    session.AddRow(fv);                                              // AddRow PRZED ustawianiem pól

    fv.Definicja = handel.DefDokHandlowych.WgSymbolu["FV"];          // definicja PIERWSZA
    fv.Magazyn = magazyny.Magazyny.WgSymbol["F"];
    fv.Kontrahent = crm.Kontrahenci.WgKodu["Abc"];                   // nabywca
    fv.Data = Date.Today;                                            // data wystawienia
    fv.DataOperacji = Date.Today;                                    // faktyczna data sprzedaży
    fv.LiczonaOd = SposobLiczeniaVAT.Netto;                          // VAT liczony od netto

    // Pozycja towarowa (szczegóły w W8):
    var poz = new PozycjaDokHandlowego(fv);
    session.AddRow(poz);
    poz.Towar = session.GetTowary().Towary.WgKodu["BIKINI"];         // Towar PIERWSZY
    poz.Ilosc = new Quantity(2, poz.Ilosc.Symbol);
    poz.Cena  = new DoubleCy(50m, poz.Cena.Symbol);

    t.Commit();                                                      // CommitUI() w workerze/extenderze
}
session.Save();                                                     // tu księgują się obroty i VAT

// Odczyt wyliczonej tabeli VAT i wartości:
foreach (SumaVAT v in fv.SumyVAT) { /* v.Stawka, v.Suma (Netto/VAT/Brutto) */ }
BruttoNetto suma = fv.Suma;
```

**Pułapki:**
- **Demo blokuje stan ujemny** (`StanUjemnyVerifier`): FV (rozchód) wymaga wcześniejszego
  **zapisanego** przyjęcia (PW/PZ) tego towaru. Samo `CommitUI` nie księguje obrotów — magazyn
  aktualizuje się dopiero po `Session.Save()` dokumentu przychodowego.
- `SumyVAT`, `Suma`, `Platnosci` są **wyliczane** z pozycji i parametrów dokumentu — nie ustawiaj
  ich ręcznie (ręczna korekta tabeli VAT to osobny mechanizm: `KorektaVAT=true`).
- `LiczonaOd` ustaw przed pozycjami — zmiana po wprowadzeniu pozycji wymusza przeliczenie cen
  netto↔brutto.
- Stawka VAT pozycji jest inicjowana z karty towaru — nie ustawiaj jej „z palca", jeśli nie musisz
  jej nadpisać.

---

### W5 — Faktura zakupu (FZ)

**Cel:** wprowadzić fakturę zakupu otrzymaną od dostawcy: dokument przychodowy z numerem obcym
dostawcy oraz datami zakupu i wystawienia dokumentu obcego.

**Warianty:**

| Wariant | Charakterystyka | Pola krytyczne |
|---|---|---|
| FZ krajowa | zakup od dostawcy PL | `Definicja=FZ`, `Obcy.Numer`, `DataOperacji` (data zakupu) |
| FZ z dostawą magazynową | zakup z przyjęciem na magazyn | `Magazyn`, kierunek przychodowy z definicji |
| FZ od dostawcy unijnego (WNT) | nabycie wewnątrzwspólnotowe | kontrahent `RodzajPodmiotu=Unijny` |
| FZ walutowa | zakup w walucie obcej | patrz **W9** |

**Pola i typy:** `Definicja=DefDokHandlowych.WgSymbolu["FZ"]`, `Kontrahent` = dostawca,
`Obcy: DokumentObcy` (subrow): `Obcy.Numer: string` (numer obcy nadany przez dostawcę),
`Obcy.DataOtrzymania: Date` (data dokumentu obcego). `Data: Date` (data wystawienia w naszym
systemie), `DataOperacji: Date` (faktyczna data zakupu).

**Snippet:**

```csharp
var handel = session.GetHandel();

using (var t = session.Logout(editMode: true))
{
    var fz = new DokumentHandlowy();
    session.AddRow(fz);

    fz.Definicja = handel.DefDokHandlowych.WgSymbolu["FZ"];
    fz.Magazyn = session.GetMagazyny().Magazyny.WgSymbol["F"];
    fz.Kontrahent = session.GetCRM().Kontrahenci.WgKodu["ZEFIR"];   // dostawca
    fz.Data = Date.Today;                                           // data wystawienia u nas
    fz.DataOperacji = Date.Today.AddDays(-2);                       // faktyczna data zakupu

    // Numer i data dokumentu obcego (od dostawcy):
    fz.Obcy.Numer = "FV/2026/06/123";                              // numer obcy
    fz.Obcy.DataOtrzymania = Date.Today.AddDays(-2);              // data dokumentu obcego

    var poz = new PozycjaDokHandlowego(fz);
    session.AddRow(poz);
    poz.Towar = session.GetTowary().Towary.WgKodu["BIKINI"];
    poz.Ilosc = new Quantity(10, poz.Ilosc.Symbol);
    poz.Cena  = new DoubleCy(30m, poz.Cena.Symbol);

    t.Commit();
}
session.Save();
```

**Pułapki:**
- `Obcy` to subrow (pole złożone) — nie da się przypisać `fz.Obcy = …`; ustawiaj jego pola
  (`fz.Obcy.Numer`, `fz.Obcy.DataOtrzymania`).
- Rozróżniaj trzy daty: `Data` (wystawienia u nas), `DataOperacji` (faktyczna data
  zakupu/sprzedaży, decyduje o okresie magazynowym), `Obcy.DataOtrzymania` (data na dokumencie
  obcym). To trzy różne pola — nie myl ich.
- FZ z przyjęciem na magazyn księguje **przychód** → po `Save()` powstają zasoby (`dok.Zasoby`).
- Indeks `WgKontrahentaObcy` (Kontrahent + numer obcy) pozwala wykryć duplikat faktury od tego
  samego dostawcy — sprawdzaj przed dodaniem.

---

### W6 — Dokument magazynowy (PZ / WZ / RW / PW)

**Cel:** wystawić czysto magazynowy dokument wpływający na stan magazynu, bez części handlowej
(VAT/płatności) lub z minimalną.

**Warianty:**

| Wariant | Symbol | Kierunek | Zastosowanie |
|---|---|---|---|
| Przyjęcie zewnętrzne | `PZ` | przychód | przyjęcie od dostawcy |
| Przyjęcie wewnętrzne | `PW` | przychód | przyjęcie z produkcji / bilans otwarcia |
| Wydanie zewnętrzne | `WZ` | rozchód | wydanie odbiorcy |
| Rozchód wewnętrzny | `RW` | rozchód | zużycie wewnętrzne |

**Pola i typy:** `Definicja=DefDokHandlowych.WgSymbolu["PW"]` (itd.), `Magazyn: Magazyn` (wymagany),
`Kontrahent` (gdy dotyczy — PZ/WZ tak, RW/PW zwykle nie), `Data`, `DataOperacji`. Kierunek
magazynu (`KierunekMagazynu: KierunekPartii` — `Przychód=1`, `Rozchód=-1`) jest ustawiany z
definicji (`readonly="set"`). Wynik: `dok.Zasoby` (przy przychodzie), `dok.Obroty`.

**Snippet:**

```csharp
var handel = session.GetHandel();

// Przyjęcie wewnętrzne PW (przychód — buduje stan magazynu pod późniejsze rozchody):
using (var t = session.Logout(editMode: true))
{
    var pw = new DokumentHandlowy();
    session.AddRow(pw);
    pw.Definicja = handel.DefDokHandlowych.WgSymbolu["PW"];          // kierunek z definicji
    pw.Magazyn = session.GetMagazyny().Magazyny.WgSymbol["F"];
    pw.Data = Date.Today;
    pw.DataOperacji = Date.Today;

    var poz = new PozycjaDokHandlowego(pw);
    session.AddRow(poz);
    poz.Towar = session.GetTowary().Towary.WgKodu["BIKINI"];
    poz.Ilosc = new Quantity(100, poz.Ilosc.Symbol);
    poz.Cena  = new DoubleCy(25m, poz.Cena.Symbol);

    t.Commit();
}
session.Save();                                                     // dopiero teraz powstają zasoby

// Stan magazynowy po przyjęciu:
foreach (var z in pw.Zasoby) { /* z.* — partia, ilość, magazyn */ }
```

**Pułapki:**
- `Magazyn` jest **wymagany** (`required`) dla dokumentów magazynowych — bez niego `Save()` rzuci
  `RowException`.
- `KierunekMagazynu`/`TypPartii` są `readonly="set"` — wynikają z definicji, nie ustawiaj ich
  ręcznie.
- Rozchód (WZ/RW) na bazie Demo wymaga wcześniejszego **zapisanego** przychodu (PW/PZ) — inaczej
  `StanUjemnyVerifier` zablokuje `Save()`.
- Obroty/zasoby księgują się **po `Session.Save()`**, nie po `Commit()`/`CommitUI()`. Aby je
  odczytać, zapisz dokument i odśwież.

---

### W7 — Zamówienie (ZO / ZD)

**Cel:** wystawić zamówienie od odbiorcy (ZO) lub zamówienie do dostawcy (ZD). Zamówienie nie
wpływa na stan magazynowy (może tworzyć rezerwacje), jest dokumentem nadrzędnym dla realizacji
(FV/WZ — patrz rozdział o relacjach).

**Warianty:**

| Wariant | Symbol | Strona | Realizacja |
|---|---|---|---|
| Zamówienie odbiorcy | `ZO` | klient zamawia u nas | → FV / WZ przez `IRelacjeService` |
| Zamówienie do dostawcy | `ZD` | my zamawiamy u dostawcy | → FZ / PZ przez `IRelacjeService` |
| ZO z rezerwacją | `ZO` | jw. | rezerwacja zasobu (`dok.Rezerwacja`) |
| ZO z terminem dostawy | `ZO` | jw. | `Dostawa.Termin` |

**Pola i typy:** `Definicja=DefDokHandlowych.WgSymbolu["ZO"]` / `["ZD"]`, `Kontrahent`,
`Magazyn`, `Data`, `DataOperacji`, `Dostawa: DokumentDostawa` (subrow): `Dostawa.Termin: Date`
(termin realizacji), `Dostawa.Sposob: string`. Powiązanie z realizacją: `dok.Rezerwacja`,
generowanie dokumentu podrzędnego przez `IRelacjeService.NowyPodrzednyIndywidualny(...)`.

**Snippet:**

```csharp
var handel = session.GetHandel();

using (var t = session.Logout(editMode: true))
{
    var zo = new DokumentHandlowy();
    session.AddRow(zo);
    zo.Definicja = handel.DefDokHandlowych.WgSymbolu["ZO"];         // zamówienie odbiorcy
    zo.Magazyn = session.GetMagazyny().Magazyny.WgSymbol["F"];
    zo.Kontrahent = session.GetCRM().Kontrahenci.WgKodu["Abc"];     // zamawiający odbiorca
    zo.Data = Date.Today;
    zo.DataOperacji = Date.Today;
    zo.Dostawa.Termin = Date.Today.AddDays(7);                      // oczekiwany termin dostawy

    var poz = new PozycjaDokHandlowego(zo);
    session.AddRow(poz);
    poz.Towar = session.GetTowary().Towary.WgKodu["BIKINI"];
    poz.Ilosc = new Quantity(5, poz.Ilosc.Symbol);
    poz.Cena  = new DoubleCy(50m, poz.Cena.Symbol);

    t.Commit();
}
session.Save();
```

**Pułapki:**
- Zamówienie **nie buduje stanu magazynu** — to dokument planistyczny. Realizację (FV/WZ z ZO,
  FZ/PZ z ZD) tworzysz przez `IRelacjeService` (rozdział o relacjach) — dokument nadrzędny musi
  być wtedy **zatwierdzony**.
- `Dostawa` to subrow — ustawiaj pola (`zo.Dostawa.Termin`), nie przypisuj całego obiektu.
- Rezerwacja ilościowa zamówienia jest zarządzana wewnętrznym workerem
  (`ZmienRezerwacjeIlosciowaWorker` — **internal**, niedostępny z dodatku); z poziomu publicznego
  odczytuj `zo.Rezerwacja`, a rezerwacje steruj przez definicję dokumentu i relacje.

---

### W8 — Dodawanie pozycji (towar, ilość, cena, rabat, jednostka)

**Cel:** dodać pozycję towarową do dokumentu — z automatycznym pobraniem ceny/rabatu z cennika lub
z ręcznym nadpisaniem.

**Warianty:**

| Wariant | Operacja | Pole |
|---|---|---|
| Pozycja z automatyczną ceną | cena i rabat pobrane z cennika/karty | tylko `Towar` + `Ilosc` |
| Ręczna cena | nadpisanie ceny | `Cena: DoubleCy` (ustawia `KorektaCeny=true`) |
| Ręczny rabat | nadpisanie rabatu | `Rabat: Percent` (ustawia `KorektaRabatu=true`) |
| Inna jednostka | sprzedaż w jednostce zbiorczej | `Ilosc` z symbolem jednostki towaru |
| Pozycja bez rabatu | wyłączenie rabatu | `BezRabatu=true` |
| Ręczna wartość | korekta wartości pozycji | `WartoscCy: Currency` |

**Pola i typy (`PozycjaDokHandlowego`):** `Towar: Towar` (ustaw pierwszy — inicjuje jednostkę,
stawkę VAT, cenę i rabat), `Ilosc: Quantity` (ilość + symbol jednostki), `Cena: DoubleCy` (cena
+ symbol waluty; netto lub brutto wg `Dokument.LiczonaOd`), `Rabat: Percent`,
`RabatCeny: DoubleCy` (rabat kwotowy), `WartoscCy: Currency` (wartość pozycji),
`DefinicjaStawki: DefinicjaStawkiVat` (stawka VAT). Flagi nadpisań: `KorektaCeny: bool`,
`KorektaRabatu: bool`, `BezRabatu: bool`.

**Snippet:**

```csharp
var towary = session.GetTowary();

using (var t = session.Logout(editMode: true))
{
    // Wariant A — cena i rabat pobrane automatycznie z cennika/karty towaru:
    var poz1 = new PozycjaDokHandlowego(dok);
    session.AddRow(poz1);
    poz1.Towar = towary.Towary.WgKodu["BIKINI"];                    // ustawia jednostkę, cenę, rabat, stawkę VAT
    poz1.Ilosc = new Quantity(3, poz1.Ilosc.Symbol);               // symbol jednostki z towaru
    // Cena i rabat zostają takie, jakie zaproponował cennik.

    // Wariant B — ręczne nadpisanie ceny i rabatu:
    var poz2 = new PozycjaDokHandlowego(dok);
    session.AddRow(poz2);
    poz2.Towar = towary.Towary.WgKodu["BIKINI"];
    poz2.Ilosc = new Quantity(10, poz2.Ilosc.Symbol);
    poz2.Cena  = new DoubleCy(48m, poz2.Cena.Symbol);              // nadpisanie ceny → KorektaCeny=true
    poz2.Rabat = new Percent(0.1);                                 // 10% rabatu → KorektaRabatu=true

    t.Commit();
}
session.Save();
```

**Pułapki:**
- **`Towar` ustawiaj jako pierwszy.** Dopiero on inicjuje symbol jednostki na `Ilosc`/`Cena`, stawkę
  VAT i proponowaną cenę/rabat. Ustawienie `Ilosc`/`Cena` przed `Towar` operowałoby na pustych
  symbolach.
- `Ilosc` to `Quantity` (wartość + symbol jednostki), `Cena` to `DoubleCy` (wartość + symbol
  waluty) — twórz je z symbolem już ustawionym na pozycji: `new Quantity(n, poz.Ilosc.Symbol)`,
  `new DoubleCy(c, poz.Cena.Symbol)`. Nie wstawiaj „gołego" `decimal`.
- Ręczne ustawienie `Cena`/`Rabat` zapala flagi `KorektaCeny`/`KorektaRabatu` — od tej chwili
  platforma **nie przeliczy** już automatycznie tej wartości (np. po zmianie kontrahenta/ilości).
- `Cena` jest netto albo brutto zależnie od `Dokument.LiczonaOd` — interpretuj ją spójnie z
  dokumentem.
- Konstruktor pozycji wymaga dokumentu: `new PozycjaDokHandlowego(dok)`, a po nim `session.AddRow(poz)`.

---

### W9 — Dokument w walucie obcej

**Cel:** wystawić dokument rozliczany w walucie obcej (EUR/USD): wskazać walutę płatności, tabelę
kursową, datę kursu oraz — w razie potrzeby — wpisać kurs ręcznie.

**Warianty:**

| Wariant | Mechanizm | Pola |
|---|---|---|
| Kurs z tabeli na datę | kurs pobierany z `TabelaKursowa` | `TabelaKursowa`, `DataKursu` |
| Kurs ręczny | użytkownik podaje kurs | `KursWaluty: double` |
| Zmiana waluty istniejącego dokumentu | przeliczenie dokumentu i cen | akcja „Zmień walutę dokumentu i cen..." (worker) |
| Waluta na pozycji | cena w walucie | `poz.Cena: DoubleCy` z symbolem waluty |

**Pola i typy:** `TabelaKursowa: TabelaKursowa` (wymagana — `WalutyModule.GetInstance(session).TabeleKursowe`),
`DataKursu: Date`, `KursWaluty: double`, `BruttoCy: Currency` (kwota płatności w walucie).
Waluta płatności wynika z definicji (`DefDokHandlowego.WalutaPlatnosci`). Zmianę waluty
istniejącego dokumentu realizuje akcja menu Czynności sterowana klasą parametrów
`DokumentHandlowyZmianaWalutyWorkerParams` (publiczna): `Waluta`, `WalutaBazowa` (read-only),
`TabelaKursowa`, `Data`, `KursWaluty`, `ZmienCeny: bool`.

**Snippet:**

```csharp
using Microsoft.Extensions.DependencyInjection;   // dla GetRequiredService, jeśli potrzebne
using Soneta.Waluty;

var wm = WalutyModule.GetInstance(session);        // session.GetWaluty() jest internal
var eur = wm.Waluty.WgSymbolu["EUR"];
var tabela = wm.TabeleKursowe.NBP;                 // np. tabela NBP

// Zmiana waluty istniejącego (buforowego) dokumentu na EUR z ręcznym kursem.
// Worker uruchamiany jest jak akcja menu Czynności — parametry przekazujemy przez Context:
var paramy = new DokumentHandlowyZmianaWalutyWorkerParams(context, dok)
{
    Waluta = eur,
    TabelaKursowa = tabela,
    KursWaluty = 4.3344,        // kurs ręczny; przy zmianie tabeli/daty platforma proponuje kurs sama
    ZmienCeny = true,           // przelicz także ceny pozycji
};
context.Set(paramy);
// akcja „Zmień walutę dokumentu i cen..." (ZmienWalute) wykonuje przeliczenie w transakcji UI

// Dokument walutowy „od zera": ustaw tabelę i datę kursu przed pozycjami:
using (var t = session.Logout(editMode: true))
{
    dok.TabelaKursowa = tabela;
    dok.DataKursu = Date.Today;
    // dok.KursWaluty = 4.3344;   // tylko gdy chcesz wymusić kurs ręczny
    t.Commit();
}
session.Save();
```

**Pułapki:**
- **Brak kursu na datę = wyjątek.** Jeśli w bazie nie ma kursu danej waluty na `DataKursu`, operacja
  rzuca `KursWalutyNotFoundException`. Na bazie Demo nie ma kursu EUR „na dziś" — albo dodaj kurs do
  tabeli kursowej, albo wpisz kurs ręcznie (`KursWaluty`).
- `TabelaKursowa` jest **wymagana** dla dokumentu walutowego.
- `session.GetWaluty()` jest **internal** — używaj `WalutyModule.GetInstance(session)`.
- Worker `DokumentHandlowyZmianaWalutyWorker` jest klasą **internal** — z dodatku nie tworzysz jej
  instancji bezpośrednio; uruchamiasz akcję przez framework Czynności, przekazując publiczną klasę
  `DokumentHandlowyZmianaWalutyWorkerParams` przez `Context`.
- Zmiana waluty dokumentu jest możliwa tylko w **buforze** (`dok.Bufor == true`).

---

### W10 — Dokument z usługą (pozycja usługowa bez wpływu na magazyn)

**Cel:** dodać do dokumentu pozycję usługową (np. „MONTAZ", „TRANSPORT") — towar typu usługa nie
ma wpływu na stan magazynu, ale uczestniczy w wartości i tabeli VAT.

**Warianty:**

| Wariant | Charakterystyka |
|---|---|
| FV tylko z usługą | faktura za samą usługę (np. montaż) — brak obrotu magazynowego |
| FV mieszana | towar magazynowy + pozycja usługowa na jednym dokumencie |
| Usługa rozliczana ilościowo | usługa w jednostce (np. „TRANSPORT" w km) |

**Pola i typy:** identyczne jak w W8 (`Towar`, `Ilosc`, `Cena`, `Rabat`, `DefinicjaStawki`).
Różnica jest w **karcie towaru**: towar usługowy nie generuje obrotu magazynowego —
`poz.IloscMagazynu` pozostaje zerowa, `dok.Zasoby`/`dok.Obroty` nie powstają dla tej pozycji.

**Snippet:**

```csharp
var handel = session.GetHandel();
var towary = session.GetTowary();

using (var t = session.Logout(editMode: true))
{
    var fv = new DokumentHandlowy();
    session.AddRow(fv);
    fv.Definicja = handel.DefDokHandlowych.WgSymbolu["FV"];
    fv.Magazyn = session.GetMagazyny().Magazyny.WgSymbol["F"];
    fv.Kontrahent = session.GetCRM().Kontrahenci.WgKodu["Abc"];
    fv.Data = Date.Today;
    fv.DataOperacji = Date.Today;

    // Pozycja usługowa — towar "MONTAZ" jest usługą (BEZ wpływu na magazyn):
    var poz = new PozycjaDokHandlowego(fv);
    session.AddRow(poz);
    poz.Towar = towary.Towary.WgKodu["MONTAZ"];                     // usługa
    poz.Ilosc = new Quantity(1, poz.Ilosc.Symbol);
    poz.Cena  = new DoubleCy(200m, poz.Cena.Symbol);

    t.Commit();
}
session.Save();
```

**Pułapki:**
- O tym, czy pozycja wpływa na magazyn, decyduje **typ towaru** (usługa vs towar magazynowy), a nie
  pole na pozycji. Dla usługi `StanUjemnyVerifier` nie blokuje wystawienia rozchodu — usługa nie
  pobiera ze stanu.
- Faktura zawierająca **wyłącznie** usługi nie tworzy obrotów magazynowych, ale nadal liczy tabelę
  VAT i płatność.
- Usługa też ma jednostkę (np. „TRANSPORT" w km) — `Ilosc` używa symbolu jednostki z karty towaru.

---

### W11 — Odbiorca / płatnik inny niż kontrahent + miejsce dostawy

**Cel:** wystawić dokument, na którym **nabywca** (`Kontrahent`) różni się od **odbiorcy** towaru
(`Odbiorca`), wskazać miejsce dostawy oraz — gdy płatnikiem jest inny podmiot — rozliczyć
płatność na płatnika.

**Warianty:**

| Wariant | Pole / mechanizm |
|---|---|
| Inny odbiorca towaru | `Odbiorca: Kontrahent` |
| Miejsce dostawy odbiorcy | `OdbiorcaMiejsceDostawy: Lokalizacja` |
| Osoba odbierająca | `OsobaKontrahenta: KontaktOsoba`, `Osoba: string` (podpisujący) |
| Adres / parametry przesyłki | subrow `Dostawa` (`Dostawa.Termin`, `Dostawa.Sposob`, `Dostawa.Odpowiedzialny`) |
| Inny płatnik | `dok.InnyPłatnik` (kalkulowane — wynika z relacji podmiotów / płatności) |

**Pola i typy:** `Kontrahent: Kontrahent` (nabywca — strona transakcji/VAT),
`Odbiorca: Kontrahent` (odbiorca towaru — dane dostawy), `OdbiorcaMiejsceDostawy: Lokalizacja`
(miejsce docelowe dostawy), `OsobaKontrahenta: KontaktOsoba`, `Osoba: string`.
`InnyPłatnik` jest **kalkulowane (read-only)** — płatnika ustawia się przez relacje podmiotów
(płatnik podmiotu) lub przez płatność, nie przez bezpośrednie przypisanie na dokumencie.

**Snippet:**

```csharp
var crm = session.GetCRM();

using (var t = session.Logout(editMode: true))
{
    // dok utworzony jak w W4; Kontrahent = nabywca (np. centrala):
    dok.Kontrahent = crm.Kontrahenci.WgKodu["Abc"];                 // nabywca / strona VAT
    dok.Odbiorca   = crm.Kontrahenci.WgKodu["ZEFIR"];               // odbiorca towaru (inny podmiot)

    // Miejsce dostawy odbiorcy (lokalizacja zdefiniowana u odbiorcy):
    // dok.OdbiorcaMiejsceDostawy = ... // rekord Lokalizacja powiązany z odbiorcą

    dok.Osoba = "Jan Kowalski";                                     // osoba podpisująca po stronie kontrahenta

    // Parametry dostawy (subrow):
    dok.Dostawa.Termin = Date.Today.AddDays(3);
    dok.Dostawa.Sposob = "Kurier";

    t.Commit();
}
session.Save();

// Odczyt płatnika (kalkulowane):
bool jestInnyPlatnik = dok.InnyPłatnik;
```

**Pułapki:**
- `Kontrahent` to **nabywca** (strona transakcji i VAT), `Odbiorca` to fizyczny odbiorca towaru —
  to dwa różne pola, oba typu `Kontrahent`. Faktura wystawiana jest na `Kontrahent`, dostawa idzie
  do `Odbiorca`.
- `InnyPłatnik` jest **kalkulowane** — nie przypisuj go ręcznie. Innego płatnika ustala się przez
  relacje podmiotów (płatnik nadrzędny) lub przez konfigurację płatności dokumentu.
- `OdbiorcaMiejsceDostawy` to referencja do rekordu `Lokalizacja` (zwykle zdefiniowanego u
  odbiorcy) — pobierz istniejącą lokalizację, nie twórz „w locie".
- `Dostawa` to subrow — ustawiaj jego pola, nie przypisuj całego obiektu.
- Zmiana płatnika rozkłada się na płatności; do podziału płatności na raty/płatników służy publiczny
  worker `PodzialPlatnosciWorker`.

---

## 3. Stany dokumentu i cykl życia

Stan dokumentu handlowego steruje całym jego cyklem życia: od bufora (rekord roboczy, swobodnie
edytowalny i usuwalny), przez zatwierdzenie (księgowanie obrotów magazynowych, generowanie
płatności, blokada większości pól), aż po anulowanie. Stanem steruje **jedno zapisywalne pole**
`dok.Stan`, a dodatkowe operacje serwisowe (naprawa, przeliczenie) wykonują publiczne workery.

> **Fundamenty** (sesja, transakcja edycyjna `session.Logout(editMode: true)`, `Commit`/`CommitUI`,
> blokada optymistyczna w `Save()`) opisuje [`safe-code.md`](../safe-code.md) — tu się do nich
> odwołujemy, nie powtarzamy. Cały kod jest zgodny z **C# 10** i operuje wyłącznie na **publicznym
> kontrakcie** platformy.

**Fakty o stanie (zweryfikowane):**

- **Pole sterujące:** `dok.Stan: Soneta.Handel.StanDokumentuHandlowego` (zapisywalne w transakcji).
- **Enum `StanDokumentuHandlowego`:** `Bufor=0`, `Zatwierdzony=1`, `Zablokowany=2`, `Anulowany=3`.
  Wartość `Zablokowany` ustawia **platforma** (np. po zaksięgowaniu w ewidencji) — nie ustawiaj jej z
  dodatku „z palca".
- **Skróty kalkulowane (tylko do odczytu, `bool`):** `dok.Bufor`, `dok.Zatwierdzony`, `dok.Anulowany`.
- **Usunięcie z bufora:** `dok.Delete()` w transakcji (tylko gdy brak zależności).
- **Workery publiczne (cykl życia / naprawa):** `Soneta.Handel.PoprawaStanuDokumentuWorker`,
  `Soneta.Magazyny.PrzeliczenieStanuWorker`.

---

### W12 — Zatwierdzenie dokumentu (bufor → zatwierdzony)

**Cel:** przeprowadzić dokument z bufora do stanu zatwierdzonego. Dopiero zatwierdzenie + `Save()`
księguje obroty magazynowe, tworzy zasoby/partie, generuje płatności i czyni dokument nadrzędnym dla
relacji (np. ZO→FV, FA→WZ — patrz rozdział o relacjach).

**Warianty:**

| Wariant | Operacja | Uwaga |
|---|---|---|
| Zatwierdzenie pojedyncze | `dok.Stan = StanDokumentuHandlowego.Zatwierdzony` | w transakcji + `Save()` |
| Zatwierdzenie zbiorcze | worker `EwidencjonowanieZbiorczeWorker` (`[Context] DokumentHandlowy[]`) | wiele dokumentów naraz |
| Sprawdzenie stanu | `dok.Zatwierdzony` / `dok.Bufor` (kalkulowane `bool`) | bez porównywania enuma |
| Stan `Zablokowany` | ustawiany przez platformę (księgowanie ewidencji) | nie ustawiaj ręcznie |

**Pola i typy:** `dok.Stan: StanDokumentuHandlowego` (zapisywalne), `dok.Bufor/Zatwierdzony/Anulowany:
bool` (kalkulowane). Wartości magazynowe widoczne **po** `Save()`: `dok.Zasoby`, `dok.Obroty`,
`dok.SumyVAT`.

**Snippet:**

```csharp
var hm = session.GetHandel();
var dok = hm.DokHandlowe.WgDaty[/* ... */];  // odczytany dokument w buforze

using (var t = session.Logout(editMode: true))
{
    dok.Stan = StanDokumentuHandlowego.Zatwierdzony;  // bufor -> zatwierdzony
    t.Commit();                                        // CommitUI() w workerze/extenderze
}
session.Save();   // DOPIERO TERAZ księgowane są obroty/zasoby/płatności

// Sprawdzenie po zapisie — czytaj pola kalkulowane, nie porównuj enuma:
if (dok.Zatwierdzony)
{
    foreach (var z in dok.Zasoby) { /* zasoby utworzone przez dokument przychodowy */ }
}
```

**Pułapki:**

- **Magazyn księguje się dopiero po `Save()`** — samo `Commit()`/`CommitUI()` nie tworzy obrotów ani
  zasobów. Jeśli baza blokuje stan ujemny (weryfikator `StanUjemnyVerifier`, jak w bazie Demo),
  rozchód (FV/WZ/RW) wymaga **wcześniej zapisanego** przyjęcia (PW/PZ) tego towaru — inaczej `Save()`
  rzuci wyjątek.
- Zatwierdzenie uruchamia walidatory dokumentu (kompletność pozycji, magazyn, kontrahent, tabela
  VAT). Błędy wychodzą w `Commit()`/`Save()` jako `RowException` — nie połykaj ich (safe-code §4).
- W workerze/extenderze użyj `t.CommitUI()` zamiast `t.Commit()`
  ([`worker-extender.md`](../worker-extender.md)).
- Po `Save()` w środku jednej sesji zamyka się okno edycji; kolejna edycja na **tym samym** obiekcie
  bez ponownego `Logout` rzuci `AccessWriteDenied`. Wzorzec: zapis → odczyt na świeżej sesji.
- Nie ustawiaj `Stan = Zablokowany` z dodatku — to stan wewnętrzny platformy (np. po zaksięgowaniu w
  ewidencji).

---

### W13 — Cofnięcie do bufora / odtwierdzenie

**Cel:** wycofać zatwierdzony dokument z powrotem do bufora, aby go poprawić. Operacja odksięgowuje
to, co zatwierdzenie zaksięgowało (obroty, płatności), więc jest dozwolona **tylko** gdy nie ma
zależności blokujących (zamknięty okres magazynowy/VAT, zaksięgowanie w ewidencji, dokumenty
podrzędne).

**Warianty:**

| Wariant | Operacja | Warunek dozwolenia |
|---|---|---|
| Cofnięcie do bufora | `dok.Stan = StanDokumentuHandlowego.Bufor` | okres otwarty, brak podrzędnych, nie zaksięgowany |
| Dokument zablokowany | najpierw zdjąć blokadę po stronie ewidencji/księgowości | `dok.Stan == Zablokowany` blokuje cofnięcie |
| Z dokumentami podrzędnymi | najpierw usuń/rozłącz podrzędne (relacje) | patrz rozdział o relacjach i W16 |

**Pola i typy:** `dok.Stan: StanDokumentuHandlowego`, `dok.Zatwierdzony/Bufor: bool` (kalkulowane),
`dok.DokumentyMagazynowe`, `dok.DokumentyHandlowe`, `dok.DokumentyKorygujące` (kalkulowane — do
sprawdzenia zależności przed cofnięciem).

**Snippet:**

```csharp
var dok = session.GetHandel().DokHandlowe.WgDaty[/* ... */];

if (!dok.Zatwierdzony) return;                  // już w buforze / anulowany — nic do zrobienia

// Cofnięcie jest zablokowane, gdy istnieją dokumenty podrzędne (korekty, magazynowe):
bool maZaleznosci = dok.DokumentyKorygujące.Any() || dok.DokumentyMagazynowe.Length > 0;
if (maZaleznosci)
    throw new BusException(
        "Nie można cofnąć dokumentu do bufora — istnieją powiązane dokumenty.".Translate());

using (var t = session.Logout(editMode: true))
{
    dok.Stan = StanDokumentuHandlowego.Bufor;   // odtwierdzenie: zatwierdzony -> bufor
    t.Commit();                                  // CommitUI() w workerze/extenderze
}
session.Save();   // tu odksięgowanie obrotów/płatności i wykrycie konfliktów
```

**Pułapki:**

- Cofnięcie dokumentu w **zamkniętym okresie** magazynowym/VAT albo zaksięgowanego w ewidencji
  zakończy się wyjątkiem w `Commit()`/`Save()`. Sprawdź stan otwarcia okresu zanim spróbujesz.
- Dokument w stanie `Zablokowany` nie cofniesz przez `dok.Stan = Bufor` — blokada wynika z innego
  modułu (np. ewidencja zaksięgowana). Do diagnozy/naprawy rozbieżności stanu dokument↔ewidencja służy
  `PoprawaStanuDokumentuWorker` (W15).
- Jeśli istnieją dokumenty podrzędne (korekty, powiązane magazynowe), cofnięcie się nie powiedzie —
  najpierw rozwiąż powiązania (rozdział o relacjach), patrz też W16.
- To **nie** to samo co anulowanie (W14): cofnięcie wraca do edytowalnego bufora, anulowanie zamyka
  dokument w stanie nieodwracalnym.

---

### W14 — Anulowanie dokumentów

**Cel:** unieważnić dokument, który nie powinien już brać udziału w obrocie (np. wystawiony omyłkowo),
zachowując go w bazie dla ciągłości numeracji i audytu. Anulowanie odksięgowuje skutki magazynowe i
finansowe, ale rekord pozostaje (w przeciwieństwie do `Delete()`).

**Warianty:**

| Wariant | Operacja | Uwaga |
|---|---|---|
| Anulowanie z bufora | `dok.Stan = StanDokumentuHandlowego.Anulowany` | bufor → anulowany |
| Anulowanie zatwierdzonego | `dok.Stan = StanDokumentuHandlowego.Anulowany` | odksięgowuje obroty/płatności; tylko gdy okres otwarty |
| Sprawdzenie | `dok.Anulowany` (kalkulowane `bool`) | bez porównywania enuma |

**Pola i typy:** `dok.Stan: StanDokumentuHandlowego`, `dok.Anulowany: bool` (kalkulowane).

**Snippet:**

```csharp
var dok = session.GetHandel().DokHandlowe.WgDaty[/* ... */];

if (dok.Anulowany) return;                      // już anulowany

using (var t = session.Logout(editMode: true))
{
    dok.Stan = StanDokumentuHandlowego.Anulowany;   // bufor lub zatwierdzony -> anulowany
    t.Commit();                                      // CommitUI() w workerze/extenderze
}
session.Save();

// Po anulowaniu dokument pozostaje w bazie (numeracja zachowana), ale nie wpływa na stany:
bool wycofany = dok.Anulowany;
```

**Pułapki:**

- Anulowanie zatwierdzonego dokumentu odksięgowuje jego skutki — w **zamkniętym okresie** albo gdy
  istnieją dokumenty podrzędne kończy się wyjątkiem. Najpierw rozwiąż zależności (jak w W13).
- Anulowanie jest **nieodwracalne** — nie ma przejścia `Anulowany → Bufor` na poziomie pola `Stan`.
  Gdy chcesz tylko poprawić dokument, użyj cofnięcia do bufora (W13).
- Anulowany dokument zwykle nie powinien być źródłem relacji ani korekt — generowanie podrzędnych z
  anulowanego nadrzędnego zostanie odrzucone.
- Do trwałego usunięcia rekordu (gdy dozwolone) służy `Delete()` (W16), a nie anulowanie —
  anulowanie zachowuje rekord i numer.

---

### W15 — Naprawa i przeliczenie stanu dokumentu

**Cel:** naprawić rozbieżności między dokumentem a jego skutkami: stan dokumentu vs stan dokumentu
ewidencji (`PoprawaStanuDokumentuWorker`) oraz zgodność obrotów/zasobów magazynowych z pozycjami
(`PrzeliczenieStanuWorker`). To operacje serwisowe — uruchamiaj świadomie, nie w pętli zwykłej
logiki.

**Warianty:**

| Wariant | Worker (publiczny) | Akcja menu / wejście |
|---|---|---|
| Naprawa stanu dokumentu (synchron. z ewidencją) | `Soneta.Handel.PoprawaStanuDokumentuWorker` | „Narzędziowe/Naprawa stanu dokumentu"; `[Context] Dokument` |
| Sprawdzenie poprawności obrotów (bez zapisu) | `Soneta.Magazyny.PrzeliczenieStanuWorker`, `Opcje.SprawdzićPoprawność` | „Narzędziowe/Naliczenie obrotów towaru" |
| Ponowne pełne przeliczenie | `PrzeliczenieStanuWorker`, `Opcje.PonowniePrzeliczyć` | jw. (zapis w transakcji) |
| Poprawa tylko błędnych | `PrzeliczenieStanuWorker`, `Opcje.PoprawićTylkoBłędne` | jw. |
| Poprawa / sprawdzenie samych obrotów | `Opcje.PoprawićObroty` / `Opcje.SprawdzićObroty` | jw. |

**Pola i typy (publiczny kontrakt workerów):**

- `PoprawaStanuDokumentuWorker`: property `[Context] public DokumentHandlowy Dokument`; akcja
  `public void NaprawStan()`; predykat widoczności
  `public static bool IsVisibleNaprawStan(DokumentHandlowy dokument)`. Worker sam zarządza
  transakcją wewnątrz `NaprawStan()` (synchronizuje `dok.Stan` z dokumentem ewidencji, w razie potrzeby
  tworzy/kasuje ewidencję, może przestawić `Stan` na `Zablokowany`/`Zatwierdzony`).
- `PrzeliczenieStanuWorker`: enum `public enum Opcje { SprawdzićPoprawność, PoprawićTylkoBłędne,
  PrzeliczyćTylkoNiepoprawione, PonowniePrzeliczyć, PoprawićObroty, SprawdzićObroty }`; konstruktor
  publiczny `PrzeliczenieStanuWorker(Opcje wykonaj, bool wszystkieMagazyny, bool rozchód0, bool
  przywracajWartość)`; property `[Context]` `Dokument`, `Towar`, `Magazyny` (`Magazyn[]`); akcja
  `public void PrzeliczStan()`. Worker sam otwiera transakcje wewnątrz `PrzeliczStan()`.

**Snippet:**

```csharp
var dok = session.GetHandel().DokHandlowe.WgDaty[/* ... */];

// 1. Naprawa rozbieżności stanu dokumentu względem dokumentu ewidencji.
//    Worker sam prowadzi transakcje — ustaw tylko kontekst i wywołaj akcję.
var naprawa = new PoprawaStanuDokumentuWorker { Dokument = dok };
naprawa.NaprawStan();
session.Save();   // utrwalenie zmian dokonanych przez workera

// 2. Sprawdzenie poprawności obrotów dokumentu BEZ wprowadzania zmian (tryb diagnostyczny):
var sprawdz = new PrzeliczenieStanuWorker(
    PrzeliczenieStanuWorker.Opcje.SprawdzićPoprawność,
    wszystkieMagazyny: false, rozchód0: false, przywracajWartość: true) { Dokument = dok };
sprawdz.PrzeliczStan();   // tryb SprawdzićPoprawność nie commituje — tylko raportuje (Trace)

// 3. Pełne ponowne przeliczenie obrotów dokumentu (modyfikuje dane):
var przelicz = new PrzeliczenieStanuWorker(
    PrzeliczenieStanuWorker.Opcje.PoprawićTylkoBłędne,
    wszystkieMagazyny: false, rozchód0: false, przywracajWartość: true) { Dokument = dok };
przelicz.PrzeliczStan();
session.Save();
```

**Pułapki:**

- Oba workery **same zarządzają transakcjami** wewnątrz swoich akcji (`NaprawStan`/`PrzeliczStan`).
  Nie owijaj wywołania własnym `session.Logout(true)` — wystarczy `session.Save()` po akcji, by
  utrwalić zmiany.
- W realnej aplikacji akcje są rejestrowane z `Mode = ActionMode.IsolatedSession | Progress`, czyli
  uruchamiają się w **izolowanej sesji**. Przy programowym wywołaniu działasz na bieżącej sesji —
  upewnij się, że nie koliduje to z innymi otwartymi transakcjami.
- `Opcje.SprawdzićPoprawność` to tryb **tylko diagnostyczny** — nie zmienia danych, raportuje przez
  `Trace`. Do faktycznej naprawy użyj `PoprawićTylkoBłędne`/`PonowniePrzeliczyć`.
- `PrzeliczenieStanuWorker` rzuca `RowException`, gdy napotka obrót w **zamkniętym okresie**
  magazynowym albo dokument korygowany w buforze („Dokument korygowany … w buforze. Należy go
  zatwierdzić.") — obsłuż te przypadki, nie wywołuj przeliczenia na ślepo.
- `PoprawaStanuDokumentuWorker.IsVisibleNaprawStan` zwraca `false` dla dokumentów z obsługą
  technologii produkcji i magazynu pozabilansowego — to sygnał, że dla takich dokumentów naprawa nie
  ma zastosowania.
- To są narzędzia serwisowe — nie używaj ich jako rutynowego elementu logiki tworzenia dokumentów.

---

### W16 — Bezpieczne usunięcie dokumentu z bufora i obsługa zależności

**Cel:** trwale usunąć dokument z bazy (`Delete()`), gdy jest błędny i jeszcze niepowiązany. Usuwanie
jest dozwolone **wyłącznie w buforze** i tylko gdy nie istnieją zależności (rezerwacje, dokumenty
magazynowe/handlowe powiązane, korekty). W przeciwnym razie świadomie odmów (lub anuluj — W14).

**Warianty:**

| Wariant | Sytuacja | Zalecenie |
|---|---|---|
| Usunięcie czyste | bufor, brak powiązań i rezerwacji | dozwolone (`dok.Delete()`) |
| Dokument zatwierdzony | poza buforem | najpierw cofnij do bufora (W13) lub anuluj (W14) |
| Z rezerwacją | `dok.Rezerwacja != null` | usuń/zwolnij rezerwację najpierw (relacje) |
| Z dokumentami powiązanymi | `DokumentyMagazynowe`/`DokumentyHandlowe`/korekty niepuste | rozłącz/usuń podrzędne lub anuluj |

**Pola i typy (do oceny zależności — kalkulowane, tylko odczyt):** `dok.Bufor: bool`,
`dok.Rezerwacja`, `dok.DokumentyMagazynowe: DokumentHandlowy[]`, `dok.DokumentyHandlowe:
DokumentHandlowy[]`, `dok.DokumentyKorygujące: IEnumerable`, `dok.DokumentKorygowany`,
`dok.DokumentyZaliczkowe`.

**Snippet:**

```csharp
var dok = session.GetHandel().DokHandlowe.WgDaty[/* ... */];

// 1. Usuwać można tylko z bufora:
if (!dok.Bufor)
    throw new BusException(
        "Usunąć można tylko dokument w buforze. Cofnij do bufora lub anuluj.".Translate());

// 2. Zależności blokujące usunięcie (rezerwacja, powiązane, korekty):
bool maZaleznosci =
    dok.Rezerwacja != null ||
    dok.DokumentyMagazynowe.Length > 0 ||
    dok.DokumentyHandlowe.Length > 0 ||
    dok.DokumentyKorygujące.Any();

if (maZaleznosci)
    throw new BusException(
        "Nie można usunąć dokumentu — istnieją powiązania (rezerwacja/dokumenty/korekty).".Translate());

using (var t = session.Logout(editMode: true))
{
    dok.Delete();        // twarde usunięcie — tylko gdy bufor i brak zależności
    t.Commit();          // CommitUI() w workerze/extenderze
}
session.Save();          // integralność weryfikowana także tutaj
```

**Pułapki:**

- Sprawdzaj zależności **przed** `Delete()`. Próba usunięcia powiązanego dokumentu i tak zostanie
  odrzucona przez integralność (wyjątek w `Save()`), ale lepiej zdecydować świadomie i zwrócić czytelny
  komunikat.
- Usunięcie usuwa też **pozycje** dokumentu — wykonuj je jedną transakcją; nie kasuj pozycji „ręcznie"
  przed `dok.Delete()`, jeśli i tak usuwasz cały dokument.
- Gdy dokument jest **zatwierdzony**, najpierw cofnij go do bufora (W13). Jeśli cofnięcie jest
  zablokowane (okres zamknięty, podrzędne), rozważ **anulowanie** (W14) zamiast usuwania — anulowanie
  zachowuje numer i ścieżkę audytu.
- Rezerwacje rozwiązuje logika relacji/magazynu (workery rezerwacji są **internal** — z dodatku
  operuj przez publiczne API relacji oraz pola `dok.Rezerwacja`), nie kasuj rekordów rezerwacji
  bezpośrednio z dodatku.
- `Delete()` na dokumencie poza buforem (zatwierdzony/zablokowany/anulowany) jest zabronione — nie
  obchodź tego przez bezpośrednie operacje na tabeli.

---

## 4. Relacje i generowanie dokumentów

Rozdział opisuje **publiczny tor przekształceń dokumentów handlowych**: generowanie dokumentów
podrzędnych z nadrzędnych (zamówienie → faktura → dokument magazynowy), wiązanie i rozwiązywanie
powiązań oraz odczyt łańcucha relacji i stanu pokrycia zamówienia.

> **Punkt wejścia — `IRelacjeService`.** Cała logika relacji handlowych jest udostępniona dodatkom
> zewnętrznym **wyłącznie** przez serwis `Soneta.Handel.RelacjeDokumentow.Api.IRelacjeService`
> (scope: `Session`). Workery wykonawcze (`PowiazDokumentyWorker`, `UsunPowiazanieDokumentowWorker`,
> akcje menu „Relacje”) są **internal** — nie instancjonuj ich z dodatku. Pobranie serwisu:
>
> ```csharp
> using Microsoft.Extensions.DependencyInjection;            // GetRequiredService
> using Soneta.Handel.RelacjeDokumentow.Api;                 // IRelacjeService, HandlerSet
>
> var rel = session.GetRequiredService<IRelacjeService>();   // rzuca, gdy serwisu brak
> // albo: var rel = session.GetService<IRelacjeService>();  // zwraca null, gdy brak
> ```
>
> **Reguły wspólne dla całego rozdziału:**
> - Dokumenty **nadrzędne muszą być zatwierdzone** (`dok.Stan = StanDokumentuHandlowego.Zatwierdzony`)
>   — z bufora relacja nie powstanie.
> - Wywołanie metody serwisu (`NowyPodrzedny*`, `Dolacz*`) jest operacją modyfikującą — musi działać
>   **w otwartej transakcji edycyjnej** (`session.Logout(editMode: true)`), a po zamknięciu transakcji
>   zatwierdź zmiany przez `session.Save()`.
> - Wynik to `DokumentHandlowy[]` — tablica utworzonych/dołączonych dokumentów podrzędnych.
> - `Context` (zaznaczenie / parametry UI) i `HandlerSet` (callbacki rozstrzygające) są **opcjonalne**.
>   Jeśli definicja relacji wymaga rozstrzygnięcia (np. wyboru dostaw, magazynu, pozycji) i **nie
>   dostarczysz odpowiedniego callbacka**, platforma rzuci `NotImplementedException`.

### HandlerSet — callbacki rozstrzygające

`HandlerSet` to zbiór delegatów wołanych przez silnik relacji, gdy przekształcenie wymaga decyzji,
którą w UI podejmuje użytkownik. W trybie programowym (dodatek, test, worker bez UI) musisz je
dostarczyć sam — inaczej `NotImplementedException`. Najważniejsze:

| Callback | Typ | Kiedy potrzebny |
|---|---|---|
| `WybierzMagazynCallback` | `Func<Context, Magazyn>` | definicja relacji ma `WyborPozycji = WybórMagazynu` — wskaż magazyn docelowy |
| `WybierzMagazynDocelowyCallback` | `Func<DokumentDocelowy, Magazyn>` | wybór magazynu dla dokumentu docelowego (domyślnie `d.MagazynDo`) |
| `WybierzPozycjeCallback` | `Action<DokumentDocelowy>` | definicja ma `WyborPozycji = WybórPozycji` — zaznacz pozycje (domyślnie `PrzeliczPozycje()`) |
| `WybierzDostawyCallback` | `Action<DostawaWorker>` | wskazanie partii/dostaw przy rozchodzie (gdy `WskazaniePartii` wymuszone) |
| `WybierzDokumentyZaliczkoweCallback` | `Action<DokumentDocelowy>` | faktura z zaliczkami |
| `UstawParametryFakturowania` | `Action<DefRelacjiCyklicznaFakturowanieParams>` | fakturowanie cykliczne |

Domyślnie `WybierzPozycjeCallback` przepisuje wszystkie pozycje (`PrzeliczPozycje()`). Callbacki bez
sensownej wartości domyślnej (`WybierzMagazynCallback`, `WybierzDostawyCallback`,
`WybierzDokumentyZaliczkoweCallback`) rzucają `NotImplementedException`, dopóki ich nie nadpiszesz.

---

### W17 — Generowanie faktury z zamówienia (ZO → FV)

**Cel:** z zatwierdzonego zamówienia (odbiorcy `ZO` lub do dostawcy `ZD`) wygenerować pojedynczy
dokument podrzędny o wskazanym symbolu (np. fakturę `FV`). Relacja **jeden nadrzędny → jeden
podrzędny** (indywidualna).

**Warianty:**

| Wariant | Wejście | Symbol podrzędnego | Uwaga |
|---|---|---|---|
| ZO → FV | jedno zamówienie odbiorcy | `"FV"` | klasyczna realizacja sprzedaży |
| ZD → ZK (FZ) | zamówienie do dostawcy | `"ZK"` / `"FZ"` | zakup; może wymagać `WybierzMagazynCallback` |
| FA → WZ pojedynczo | jedna faktura | `"WZ"` | wydanie magazynowe do faktury (patrz W21) |
| Wszystkie pozycje | bez `HandlerSet` lub `WybierzPozycjeCallback` = przepisz wszystko | — | gdy definicja relacji ma `BrakOkna` |
| Wybrane pozycje | `WybierzPozycjeCallback` zaznacza podzbiór | — | gdy definicja ma `WybórPozycji` |

**Pola i typy:**
`IRelacjeService.NowyPodrzednyIndywidualny(DokumentHandlowy[] nadrzedne, string symbolPodrzednego,
Context context = null, HandlerSet handlers = null) → DokumentHandlowy[]`.
Wynik ma `Length == nadrzedne.Length` (każdy nadrzędny dostaje własny podrzędny).
Pozycja podrzędnego: `poz.Dostawa` (wskazana partia/dostawa, gdy dotyczy).

**Snippet:**

```csharp
using Microsoft.Extensions.DependencyInjection;
using Soneta.Handel;
using Soneta.Handel.RelacjeDokumentow.Api;

var rel = session.GetRequiredService<IRelacjeService>();

// zamowienie jest już zatwierdzone (StanDokumentuHandlowego.Zatwierdzony)
DokumentHandlowy[] faktury;
using (var t = session.Logout(editMode: true))
{
    faktury = rel.NowyPodrzednyIndywidualny(
        new[] { zamowienie },
        "FV");                                  // bez HandlerSet — gdy relacja nie wymaga rozstrzygnięć
    t.Commit();                                 // CommitUI() w workerze/extenderze
}
session.Save();

DokumentHandlowy faktura = faktury[0];          // jeden nadrzędny → jeden podrzędny
```

Wariant z wyborem pozycji (przepisz tylko pozycje danego towaru):

```csharp
using (var t = session.Logout(editMode: true))
{
    var wynik = rel.NowyPodrzednyIndywidualny(
        new[] { zamowienie }, "FV",
        handlers: new HandlerSet
        {
            WybierzPozycjeCallback = docelowy =>
            {
                // docelowy: DokumentDocelowy — zaznacz pozycje do przeniesienia
                docelowy.PrzeliczPozycje();      // domyślnie: wszystkie
            }
        });
    t.Commit();
}
session.Save();
```

**Pułapki:**
- Dokument nadrzędny **musi być zatwierdzony** — z bufora `NowyPodrzedny*` nie zadziała.
- Gdy definicja relacji wymaga rozstrzygnięcia (magazyn, dostawy, pozycje), a `HandlerSet` go nie
  dostarcza → `NotImplementedException`. Zacznij od wywołania bez `HandlerSet`; jeśli rzuca, dodaj
  konkretny callback (patrz tabela powyżej).
- Symbol podrzędnego musi odpowiadać **istniejącej definicji relacji** wychodzącej z definicji
  nadrzędnego (konfiguracja `DefRelacji` na `DefDokHandlowego`). Brak pasującej relacji → pusty wynik
  lub wyjątek.
- Cała operacja w **jednej** transakcji + `Save()`. Mieszane sesje rekordów → użyj `session.Get(...)`.

---

### W18 — Zbiorczy dokument magazynowy z wielu faktur (wiele FA → 1 WZ/PZ)

**Cel:** z wielu zatwierdzonych faktur utworzyć **jeden** zbiorczy dokument podrzędny (np. jeden
dokument magazynowy `WZ`/`PZ` zbierający pozycje wszystkich faktur). Relacja **wiele nadrzędnych →
jeden podrzędny** (zbiorcza).

**Warianty:**

| Wariant | Wejście | Symbol | Wynik |
|---|---|---|---|
| Wiele FA → 1 WZ | tablica faktur sprzedaży | `"WZ"` | 1 wydanie zbiorcze |
| Wiele FZ → 1 PZ | tablica faktur zakupu | `"PZ"` | 1 przyjęcie zbiorcze |
| Wiele ZO → 1 FV | zbiorcza faktura z zamówień | `"FV"` | 1 faktura zbiorcza |

**Pola i typy:**
`IRelacjeService.NowyPodrzednyZbiorczy(DokumentHandlowy[] nadrzedne, string symbolPodrzednego,
Context context = null, HandlerSet handlers = null) → DokumentHandlowy[]`.
W przeciwieństwie do W17 zwraca zwykle tablicę **jednoelementową** (jeden dokument zbiorczy).

**Snippet:**

```csharp
var rel = session.GetRequiredService<IRelacjeService>();

// faktury: DokumentHandlowy[] — wszystkie zatwierdzone, zgodne (ten sam kontrahent/magazyn wg konfiguracji)
DokumentHandlowy wz;
using (var t = session.Logout(editMode: true))
{
    var wynik = rel.NowyPodrzednyZbiorczy(faktury, "WZ");
    wz = wynik[0];                              // jeden zbiorczy dokument magazynowy
    t.Commit();
}
session.Save();
```

**Pułapki:**
- Dokumenty zbiorcze powstają tylko z dokumentów **zgodnych** (wymóg ten sam kontrahent / magazyn /
  waluta — zależnie od definicji relacji zbiorczej). Niezgodne wejście → wyjątek lub pominięcie.
- Wszystkie nadrzędne muszą być **zatwierdzone**.
- Tak jak w W17 — brak wymaganego callbacka w `HandlerSet` → `NotImplementedException`.
- Nie zakładaj `Length == nadrzedne.Length` — tu wynik jest **agregatem** (zwykle 1 dokument).

---

### W19 — Zbiorcza faktura z wielu dokumentów magazynowych (wiele WZ → 1 FA)

**Cel:** „odwrotny” kierunek W18 — z wielu zatwierdzonych dokumentów magazynowych (np. `WZ`)
utworzyć **jedną** zbiorczą fakturę sprzedaży.

**Warianty:**

| Wariant | Wejście | Symbol | Uwaga |
|---|---|---|---|
| Wiele WZ → 1 FV | wydania magazynowe | `"FV"` | fakturowanie zbiorcze rozchodów |
| Wiele PZ → 1 FZ | przyjęcia magazynowe | `"FZ"` | zbiorczy zakup |

**Pola i typy:** ta sama metoda `NowyPodrzednyZbiorczy(...)` co w W18 — różni się tylko kierunkiem
(nadrzędne = dokumenty magazynowe, symbol podrzędnego = faktura).

**Snippet:**

```csharp
var rel = session.GetRequiredService<IRelacjeService>();

// wydania: DokumentHandlowy[] — zatwierdzone WZ tego samego kontrahenta
DokumentHandlowy fakturaZbiorcza;
using (var t = session.Logout(editMode: true))
{
    fakturaZbiorcza = rel.NowyPodrzednyZbiorczy(wydania, "FV")[0];
    t.Commit();
}
session.Save();
```

**Pułapki:**
- Kierunek relacji (magazynowy → handlowy) musi być skonfigurowany jako `DefRelacji` na definicji
  dokumentu magazynowego. Brak relacji → pusty wynik.
- Dokumenty magazynowe muszą być **zatwierdzone** i zgodne (kontrahent / waluta).
- Walidator stanu ujemnego nie dotyczy tej operacji (rozchód już się dokonał na WZ), ale faktura
  przejmie wartości z dokumentów źródłowych — nie modyfikuj pozycji ręcznie po przekształceniu, jeśli
  ma zachować zgodność z magazynem.

---

### W20 — Wyszukiwanie dokumentów powiązanych (odczyt pól kalkulowanych)

**Cel:** odczytać dokumenty powiązane bez ręcznego przeszukiwania relacji — przez pola kalkulowane na
`DokumentHandlowy`. Działa w obie strony: dla faktury → jej dokumenty magazynowe, dla magazynowego →
jego faktury.

**Warianty:**

| Wariant | Pole kalkulowane | Typ | Zwraca |
|---|---|---|---|
| Magazynowe dla faktury | `dok.DokumentyMagazynowe` | `DokumentHandlowy[]` | WZ/PZ powiązane z fakturą |
| Główny dok. magazynowy | `dok.DokumentMagazynowyGłówny` | `DokumentHandlowy` | pierwszy/główny magazynowy |
| Faktury dla magazynowego | `dok.DokumentyHandlowe` | `DokumentHandlowy[]` | faktury powiązane z WZ/PZ/ZO/ofertą |

**Pola i typy:** wszystkie trzy to **właściwości kalkulowane (read-only)** na `DokumentHandlowy`.
`DokumentyMagazynowe` dla dokumentu, który **sam jest magazynowy** (`TypPartii.Magazynowy` itd.),
zwraca `{ this }`. Analogicznie `DokumentyHandlowe` dla samego dokumentu handlowego zwraca `{ this }`.

**Snippet:**

```csharp
// 1. Dla faktury — jej dokumenty magazynowe (wydania/przyjęcia)
foreach (DokumentHandlowy mag in faktura.DokumentyMagazynowe)
{
    // mag.Numer, mag.Magazyn, mag.Pozycje ...
}

// główny dokument magazynowy (gdy potrzebny jeden)
DokumentHandlowy glowny = faktura.DokumentMagazynowyGłówny;

// 2. Dla dokumentu magazynowego — faktury, które go „obsługują”
foreach (DokumentHandlowy fa in wz.DokumentyHandlowe)
{
    // fa.Numer, fa.Suma ...
}
```

**Pułapki:**
- To pola **kalkulowane** — czytaj, nie ustawiaj. Każde odwołanie uruchamia wyszukiwanie po relacjach,
  więc **nie wołaj ich w pętli** dla tysięcy rekordów — buforuj wynik w zmiennej lokalnej.
- Zwracają **tablicę** (może być pusta), nie `null` — bezpiecznie iterować, ale sprawdzaj `.Length`
  przed `[0]`.
- Pola respektują **prawa dostępu** — dokumenty bez prawa odczytu są pomijane (wynik może być węższy
  niż faktyczny łańcuch relacji).

---

### W21 — Generowanie dokumentu magazynowego z faktury (FA → WZ pojedynczo)

**Cel:** do pojedynczej zatwierdzonej faktury wygenerować odpowiadający dokument magazynowy
(np. wydanie `WZ`). To wariant indywidualny (W17), tylko z innym symbolem docelowym.

**Warianty:**

| Wariant | Wejście | Symbol | Uwaga |
|---|---|---|---|
| FV → WZ | faktura sprzedaży | `"WZ"` | wydanie z magazynu |
| FZ → PZ | faktura zakupu | `"PZ"` | przyjęcie do magazynu |
| Z wyborem partii | + `WybierzDostawyCallback` | — | gdy `WskazaniePartii` wymuszone na definicji WZ |

**Pola i typy:** `IRelacjeService.NowyPodrzednyIndywidualny(...)` — jak W17. Pozycje magazynowe mają
`poz.Dostawa` (wskazana partia/dostawa).

**Snippet (z wyborem partii — wymusza `HandlerSet`):**

```csharp
using Soneta.Magazyny;

var rel = session.GetRequiredService<IRelacjeService>();

DokumentHandlowy wz;
using (var t = session.Logout(editMode: true))
{
    var wynik = rel.NowyPodrzednyIndywidualny(
        new[] { faktura }, "WZ",
        handlers: new HandlerSet
        {
            WybierzDostawyCallback = dostawaWorker =>
            {
                // dla każdej pozycji wskaż pobierane zasoby/partie
                foreach (var poz in dostawaWorker.GetListPozycja())
                {
                    dostawaWorker.Pozycja = poz;
                    foreach (Zasob z in dostawaWorker.Zasoby.Cast<Zasob>())
                    {
                        using var tz = z.Session.Logout(editMode: true);
                        // ... oznacz zasób jako pobrany (Pobrano = true)
                        tz.Commit();
                    }
                }
            }
        });
    wz = wynik[0];
    t.Commit();
}
session.Save();
```

**Pułapki:**
- Gdy definicja `WZ` ma `WskazaniePartii = WymuszonyDodawanie`, **musisz** dostarczyć
  `WybierzDostawyCallback` — inaczej `NotImplementedException`.
- Rozchód wymaga wcześniejszego **zapisanego** przyjęcia towaru (`StanUjemnyVerifier` w Demo). Magazyn
  księguje się dopiero po `Session.Save()` — samo `Commit`/`CommitUI` nie tworzy obrotów/zasobów.
- Po wygenerowaniu WZ odczytaj go zwrotnie przez `faktura.DokumentyMagazynowe` (W20).

---

### W22 — Kopiowanie faktury klientowi (`KopiujKlientowiFaktureWorker`)

**Cel:** skopiować zatwierdzone faktury sprzedaży klienta jako dokumenty zakupu **do bazy klienta**
(scenariusz biura rachunkowego pracującego na wielu bazach). Worker **publiczny**.

**Dostępność:** `Soneta.EI.KopiujKlientowiFaktureWorker` jest **public** (rejestracja
`[assembly: Worker(typeof(KopiujKlientowiFaktureWorker), typeof(DokHandlowe))]`). Akcja menu
„Kopiuj klientowi...”. **Widoczna tylko** gdy bieżąca baza jest *master* w konfiguracji „Praca na
wielu bazach” **i** licencja to `Biuro Rachunkowe` (`IsVisibleKopiuj`). Bez tej konfiguracji
nie zadziała (nie znajdzie bazy klienta).

**Pola i typy:**
- `[Context] DokumentHandlowy[] Dokumenty` — kopiowane faktury (brane są tylko `Zatwierdzony`).
- `[Context] Params Prms` — parametry; `Params : ContextBase`:
  - `DefinicjaDokumentu Definicja` — definicja dokumentu zakupu w bazie klienta (lista z
    `DefDokumentow.WgTypu[TypDokumentu.ZakupEwidencja]`);
  - `bool PrzygotujPrzelewy` (domyślnie `true`) — czy generować przelewy dla zobowiązań.
- `object Kopiuj()` — akcja `[Action("Kopiuj klientowi...", Mode = SingleSession | Progress)]`;
  zwraca komunikat tekstowy, szczegóły pisze do logu.

**Snippet (programowe użycie workera z `Params`):**

```csharp
using Soneta.EI;

// dokumenty: zaznaczone faktury sprzedaży (worker bierze tylko zatwierdzone)
var prms = new KopiujKlientowiFaktureWorker.Params(context)
{
    Definicja = /* DefinicjaDokumentu zakupu */,
    PrzygotujPrzelewy = true,
};

var worker = new KopiujKlientowiFaktureWorker
{
    Dokumenty = dokumenty,
    Prms = prms,
};

object komunikat = worker.Kopiuj();   // tworzy dokumenty w bazie klienta; Save robi worker wewnętrznie
```

**Pułapki:**
- Worker działa **na wielu bazach** (`DBItemContext`) — sam otwiera/zamyka transakcje i `Save()`
  w bazie klienta. Nie opakowuj wywołania w zewnętrzną transakcję na bazie master.
- Kopiowane są **tylko faktury zatwierdzone**; dokumenty z zobowiązaniem (nie należnością) są
  **pomijane** (zakup wymaga należności po stronie sprzedaży).
- W bazie klienta tworzony jest automatycznie kontrahent „biuro” (wg NIP z pieczątki firmy), jeśli go
  brak. Brakujący sposób zapłaty w bazie klienta → dokument pominięty (log).
- Wymaga licencji `Biuro Rachunkowe` i roli master — w innym układzie akcja jest niewidoczna.
- Do zwykłego „kopiuj dokument w tej samej bazie” ten worker **nie służy** — to specjalizowany scenariusz
  wielobazowy.

---

### W23 — Ręczne wiązanie i rozwiązywanie powiązań

**Cel:** **dołączyć** istniejący dokument do innego jako podrzędny/nadrzędny (bez generowania nowego)
oraz rozwiązać błędnie utworzone powiązanie. Tor publiczny = `IRelacjeService.Dolacz*`.

> **Uwaga o dostępności:** workery wykonawcze `PowiazDokumentyWorker` i
> `UsunPowiazanieDokumentowWorker` są **internal** — nie używaj ich z dodatku. Wiązanie realizuj przez
> `IRelacjeService.DolaczPodrzednyIndywidualny` / `DolaczNadrzedny`. **Programowego, publicznego API do
> *rozwiązywania* powiązań brak** — rozwiązywanie powiązań jest dostępne tylko interaktywnie (menu
> „Relacje” w aplikacji), bo odpowiedni worker jest internal. To ograniczenie publicznego kontraktu.

**Warianty:**

| Wariant | Metoda | `relationName` |
|---|---|---|
| Dołącz podrzędny do nadrzędnego | `DolaczPodrzednyIndywidualny(documents, relationName)` | nazwa definicji relacji wychodzącej (np. `"Faktura"`) |
| Dołącz dokument do nadrzędnego | `DolaczNadrzedny(documents, relationName)` | nazwa relacji od strony nadrzędnego (np. `"Zamówienie"`) |
| Rozwiązanie powiązania | — | **tylko interaktywnie** (worker internal) |

**Pola i typy:**
```csharp
DokumentHandlowy[] DolaczPodrzednyIndywidualny(
    DokumentHandlowy[] documents, string relationName,
    Context context = null, HandlerSet handlers = null);
DokumentHandlowy[] DolaczNadrzedny(
    DokumentHandlowy[] documents, string relationName,
    Context context = null, HandlerSet handlers = null);
```
`relationName` to **nazwa definicji relacji** (`DefRelacji`), nie symbol dokumentu — np. `"Zamówienie"`,
`"Faktura"`, `"Korekta wydania magazynowego 2"`.

**Snippet:**

```csharp
var rel = session.GetRequiredService<IRelacjeService>();

// Dołącz fakturę do istniejącego zamówienia jako nadrzędnego (relacja "Zamówienie")
using (var t = session.Logout(editMode: true))
{
    var powiazane = rel.DolaczNadrzedny(new[] { faktura }, "Zamówienie");
    t.Commit();
}
session.Save();
```

**Pułapki:**
- `relationName` musi dokładnie pasować do **nazwy `DefRelacji`** skonfigurowanej w bazie (wielkość
  liter / spacje istotne) — niepasująca nazwa daje pusty/`null` wynik w tablicy.
- `Dolacz*` przetwarza dokumenty **pojedynczo** (`Array.ConvertAll`) — wynik na pozycji `i` może być
  `null`, jeśli dołączenie konkretnego dokumentu się nie powiodło. Sprawdzaj elementy wyniku.
- Dokumenty muszą być **zatwierdzone** i wzajemnie zgodne (kontrahent / pozycje).
- **Rozwiązywanie** powiązań programowo z dodatku **niedostępne** — zaplanuj operację jako działanie
  użytkownika w aplikacji (menu „Relacje”).

---

### W24 — Odczyt łańcucha powiązań i stan pokrycia zamówienia

**Cel:** prześledzić łańcuch relacji (oferta → zamówienie → faktura → dokument magazynowy) oraz
odczytać **stan pokrycia/realizacji zamówienia** (czy zamówienie zostało zrealizowane fakturami).

**Warianty:**

| Wariant | Mechanizm | Typ wyniku |
|---|---|---|
| W górę łańcucha (faktury dla magazynowego/zamówienia) | `dok.DokumentyHandlowe` (W20) | `DokumentHandlowy[]` |
| W dół łańcucha (magazynowe dla faktury) | `dok.DokumentyMagazynowe` (W20) | `DokumentHandlowy[]` |
| Stan pokrycia zamówienia (odczyt) | `StanPokryciaZamówieniaWorker.StanPokrycia` | enum `StanPokryciaZamówienia` |

**Pola i typy:**
- Odczyt stanu pokrycia: worker **public** `Soneta.Handel.StanPokryciaZamówieniaWorker`
  (`[Context] DokumentHandlowy Dokument`) → property `StanPokrycia : StanPokryciaZamówienia`.
- Enum `Soneta.Handel.StanPokryciaZamówienia`: `Brak = 0`, `Częściowe = 1`, `Pełne = 2`,
  `NiePodlega = 3`, `Niezweryfikowane = 4`.
- **Ważne:** worker tylko **odczytuje** wcześniej wyliczony stan (z cache na `Login`). Samo
  przeliczenie uruchamia akcja menu „Sprawdź pokrycie” (`StanPokryciaZamowienWorker`, `[HandelAction]`)
  — wywołuje ją użytkownik; dopóki nie zostanie odpalona, `StanPokrycia` zwraca `Niezweryfikowane`.

**Snippet:**

```csharp
using Soneta.Handel;

// Odczyt stanu pokrycia pojedynczego zamówienia (po wcześniejszym „Sprawdź pokrycie”):
var w = new StanPokryciaZamówieniaWorker { Dokument = zamowienie };
StanPokryciaZamówienia stan = w.StanPokrycia;

bool zrealizowane = stan == StanPokryciaZamówienia.Pełne;

// Łańcuch relacji w dół: zamówienie -> faktury -> ich dokumenty magazynowe
foreach (DokumentHandlowy fa in zamowienie.DokumentyHandlowe)        // faktury zamówienia
    foreach (DokumentHandlowy mag in fa.DokumentyMagazynowe)         // wydania faktury
    {
        // mag.Numer, mag.Magazyn ...
    }
```

**Pułapki:**
- `StanPokryciaZamówieniaWorker.StanPokrycia` zwraca `Niezweryfikowane`, dopóki w sesji/loginie nie
  wykonano przeliczenia (akcja „Sprawdź pokrycie”). **Programowego, publicznego wyzwalacza
  przeliczenia brak** — `StanPokryciaZamówień.Przelicz()` jest wywoływane przez internal akcję menu.
  Z dodatku traktuj `StanPokrycia` jako **odczyt** stanu policzonego interaktywnie.
- Pola `DokumentyHandlowe`/`DokumentyMagazynowe` respektują prawa dostępu i są kalkulowane — buforuj
  wynik, nie wołaj w gęstych pętlach (W20).
- Stan `NiePodlega` oznacza dokument, którego pokrycie nie dotyczy (np. nie jest zamówieniem) —
  rozróżniaj go od `Brak` (zamówienie bez realizacji).

---

> **Powiązane sekcje:** tworzenie/stan dokumentu (sekcja 1–2), korekty (`IRelacjeService.NowaKorekta`,
> `NowaKorektaZbiorcza` — analogiczne do W17/W18, symbol korekty opcjonalny), magazyn i partie
> (`dok.Zasoby`, `dok.Obroty`, `GrupaDostaw`).

---

## 5. Odczyt i wyszukiwanie

Odczyt dokumentów handlowych prawie zawsze sprowadza się do **filtrowania serwerowego**: warunek
budujesz wyrażeniem LINQ i aplikujesz na **kluczu** tabeli (`DokHandlowe.WgXxx[dok => …]`) albo na
**kolekcji podrzędnej** (`towar.Pozycje[…]`, `dok.Pozycje[…]`). Z bazy do pamięci trafiają wtedy
wyłącznie pasujące wiersze. `DokHandlowe` to duża tabela **operacyjna** (`guided="Exported"`) —
nigdy nie iteruj jej w całości z `if` w pamięci; zawsze zawężaj zakres (okres, kontrahent, definicja)
przez SQL i — przy analizach poprzecznych — ogranicz przedział czasowy.

> **Fundamenty** (sesja, transakcja, blokada optymistyczna) opisuje [`safe-code.md`](../safe-code.md),
> a mechanikę warunków serwerowych [`rowcondition.md`](../rowcondition.md) — tu się do nich
> odwołujemy, nie powtarzamy. Cały kod jest zgodny z **C# 10** i operuje wyłącznie na **publicznym
> kontrakcie** platformy. W wyrażeniu LINQ wolno użyć **tylko pól bazodanowych**; pole kalkulowane
> rzuci `LinqConditionException`.

**Fakty o odczycie (zweryfikowane na tabeli `DokHandlowe` i `PozycjeDokHan`):**

- **Klucze tabeli `DokHandlowe`** (do filtrowania serwerowego i sortowania): `WgDaty`
  (`Data`, `Czas`), `WgMagazynuNumer` (`Magazyn`, `Numer.Pelny`), `WgMagazynuObcy`
  (`Magazyn`, `Obcy.Numer`), `WgKontrahentaObcy` (`Kontrahent`, `Obcy.Numer`, `Kategoria`),
  `WgOkresIntrastat`, oraz `PrimaryKey`. **Nie ma** „gołego" klucza `WgKontrahenta` ani `WgNumeru` —
  filtruj wyrażeniem na dowolnym z powyższych kluczy (sortowanie bierze się z wybranego klucza).
- **Indeksator po Guid:** `hm.DokHandlowe[guid]` (zwraca `DokumentHandlowy`; **rzuca `RowNotFoundException`** dla nieznanego Guid).
- **Pozycje dokumentu:** `dok.Pozycje` — `LpSubTable<PozycjaDokHandlowego>` (sortowane po `Lp`).
- **Pozycje danego towaru (historia obrotu):** `towar.Pozycje` — `SubTable<PozycjaDokHandlowego>`
  (klucz `WgTowar`). Klucze na `PozycjeDokHan`: `WgDaty` (`Data`), `WgKierunek`
  (`Towar`, `KierunekMagazynu`, `Data`, `Czas`), `WgTowarDokumentu` (`Towar`, `Dokument`).
- **Numer dokumentu:** pole `dok.Numer: NumerDokumentu`. Pełny numer do **odczytu** to
  `dok.Numer.NumerPelny` (kalkulowane). W warunku serwerowym używaj pola bazodanowego `Numer.Pelny`
  (np. `dok => dok.Numer.Pelny == "FV 1/2026"`).
- **Korekty:** `dok.DokumentKorygowany` (dokument korygowany przez tę korektę),
  `dok.DokumentyKorygujące` (`IEnumerable<DokumentHandlowy>` — łańcuch korekt tego dokumentu),
  `dok.Korekta: bool` (pole bazodanowe — czy dokument jest korektą). Wszystkie powiązania korekt to
  pola **kalkulowane** (oprócz `Korekta`).
- **Kolekcje na `Kontrahent` (z modułu CRM):** `k.DokumentyHandlowe` i `k.DokumentyHandloweOdbiorcy`
  to **nietypowane** `SubTable` (CRM nie referuje Handlu). Iteracja działa, ale typowane filtrowanie
  serwerowe rób od strony Handlu: `hm.DokHandlowe.WgKontrahentaObcy[dok => dok.Kontrahent == k]`.

---

### W25 — Odczytanie pozycji dokumentu

**Cel:** przejść po pozycjach (towar, ilość, cena, rabat, wartość) wczytanego dokumentu — np. do
wydruku, eksportu czy przeliczeń własnych.

**Warianty:**

| Wariant | Źródło / operacja |
|---|---|
| Wszystkie pozycje wg Lp | `dok.Pozycje` (`LpSubTable`, sortowane po `Lp`) |
| Tylko pozycje danego towaru | `dok.Pozycje[(PozycjaDokHandlowego p) => p.Towar == towar]` |
| Pozycje o niezerowej ilości | warunek serwerowy na `p.Ilosc.Value` |
| Wartości pozycji | `p.WartoscCy`, `p.Suma` (`BruttoNetto`: `NettoCy`/`VATCy`/`BruttoCy`) |

**Pola i typy (`PozycjaDokHandlowego`):** `Towar: Towar`, `Ilosc: Quantity`
(`.Value`, `.Symbol`), `Cena: DoubleCy`, `Rabat: Percent`, `WartoscCy: Currency`,
`Suma: BruttoNetto` (`NettoCy`, `VATCy`, `BruttoCy` — typ `Currency`; `Netto`/`VAT`/`Brutto` — `decimal`),
`Lp: int`, `Stawka: StawkaVat`, `Opis: string`.

**Snippet:**

```csharp
var hm = session.GetHandel();
var dok = hm.DokHandlowe[guid];                 // dokument wczytany po Guid (W29)
if (dok == null) return;

// Iteracja po pozycjach (LpSubTable jest już posortowana po Lp):
foreach (PozycjaDokHandlowego p in dok.Pozycje)
{
    string towar  = p.Towar?.Kod;
    Quantity ilosc = p.Ilosc;                   // p.Ilosc.Value + p.Ilosc.Symbol (jednostka)
    DoubleCy cena = p.Cena;
    Percent rabat = p.Rabat;
    Currency netto  = p.Suma.NettoCy;           // wartość netto pozycji w PLN
    Currency brutto = p.Suma.BruttoCy;
    Currency wartosc = p.WartoscCy;             // wartość pozycji w walucie ceny
}

// Tylko pozycje wybranego towaru — filtr serwerowy na kolekcji:
var towar = session.GetTowary().Towary.WgKodu["BIKINI"];
foreach (PozycjaDokHandlowego p in dok.Pozycje[(PozycjaDokHandlowego p) => p.Towar == towar])
{
    // ...
}
```

**Pułapki:**
- `Ilosc` to `Quantity`, a `Cena`/`WartoscCy` to `DoubleCy`/`Currency` (kwota + waluta), **nie**
  `decimal`/`double` (safe-code §10). Składowe: `p.Ilosc.Value`, `p.Ilosc.Symbol`.
- Do filtrowania pozycji **na jednym dokumencie** możesz iterować `dok.Pozycje` (to mała kolekcja),
  ale i tak preferuj warunek `dok.Pozycje[p => …]` — wykona się serwerowo.
- `p.Suma`/`p.WartoscCy` są przeliczane przez platformę — czytaj je, nie wyliczaj „ręcznie".
- `p.Towar` bywa `null` dla pozycji nietowarowych (opis/koszt) — zabezpiecz dostęp (`?.`).

---

### W26 — Odczytanie dokumentów dla kontrahenta

**Cel:** pobrać dokumenty wystawione na danego kontrahenta — jako nabywcę (`Kontrahent`) lub jako
odbiorcę (`Odbiorca`).

**Warianty:**

| Wariant | Źródło | Typ |
|---|---|---|
| Kontrahent jako nabywca (kolekcja CRM) | `k.DokumentyHandlowe` | nietypowany `SubTable` |
| Odbiorca (kolekcja CRM) | `k.DokumentyHandloweOdbiorcy` | nietypowany `SubTable` |
| Filtr typowany od strony Handlu | `hm.DokHandlowe.WgKontrahentaObcy[dok => dok.Kontrahent == k]` | `SubTable<DokumentHandlowy>` |
| Zawężenie okresem | dołóż `&& dok.Data >= od` w warunku | — |

**Pola i typy:** `dok.Kontrahent: Kontrahent`, `dok.Odbiorca: Kontrahent` (oba bazodanowe).
`Kontrahent.DokumentyHandlowe` / `DokumentyHandloweOdbiorcy` to kolekcje `SubTable` na kontrahencie
(zawężone już do jednego kontrahenta).

**Snippet:**

```csharp
var hm = session.GetHandel();
var k = session.GetCRM().Kontrahenci.WgKodu["Abc"];
if (k == null) return;

// Wariant A — kolekcja na kontrahencie (nietypowana, ale wygodna do prostego przejścia):
foreach (DokumentHandlowy dok in k.DokumentyHandlowe)
{
    // dok.Numer.NumerPelny, dok.Data, dok.Suma ...
}

// Wariant B — typowany filtr serwerowy od strony Handlu + zawężenie okresem
// (klucz WgKontrahentaObcy nadaje sortowanie wg kontrahenta):
var od = Date.Today.AddMonths(-3);
foreach (DokumentHandlowy dok in hm.DokHandlowe.WgKontrahentaObcy[
             (DokumentHandlowy dok) => dok.Kontrahent == k && dok.Data >= od])
{
    // tylko dokumenty kontrahenta z ostatnich 3 miesięcy
}

// Dokumenty, w których kontrahent jest ODBIORCĄ:
foreach (DokumentHandlowy dok in hm.DokHandlowe[
             (DokumentHandlowy dok) => dok.Odbiorca == k])
{
    // ...
}
```

**Pułapki:**
- `k.DokumentyHandlowe` jest **nietypowane** (`SubTable`, nie `SubTable<DokumentHandlowy>`) — pętla
  `foreach (DokumentHandlowy …)` działa, ale do filtrowania wyrażeniem LINQ użyj kolekcji od strony
  Handlu (`hm.DokHandlowe.WgXxx[…]`), gdzie typ wiersza jest znany kompilatorowi.
- `Kontrahent` i `Odbiorca` to **dwa różne pola** — wybierz świadomie (nabywca ≠ odbiorca towaru).
- To dane operacyjne — przy szerokich analizach **zawężaj okres** (`dok.Data >= od`), nie ładuj całej
  historii (safe-code §6.3).
- Porównuj po referencji rekordu (`dok.Kontrahent == k`), a nie po `Kod` — referencja generuje
  szybkie `JOIN` po `ID`.

---

### W27 — Ostatnie pozycje dokumentów dla wskazanego towaru

**Cel:** prześledzić historię obrotu danym towarem — pozycje dokumentów, w których towar wystąpił
(np. ostatnie zakupy/sprzedaże, kierunek magazynowy, ceny historyczne).

**Warianty:**

| Wariant | Źródło / warunek |
|---|---|
| Wszystkie pozycje towaru | `towar.Pozycje` (klucz `WgTowar`) |
| Tylko rozchody / przychody | filtr na `p.KierunekMagazynu` (`KierunekPartii`) |
| Z zakresu dat | `towar.Pozycje[p => p.Data >= od]` |
| Tylko z dokumentów zatwierdzonych | warunek przez referencję: `p.Dokument.Stan == StanDokumentuHandlowego.Zatwierdzony` |
| Ostatnie N po dacie | sortuj kluczem `WgKierunek`/`WgDaty` i ogranicz w pamięci po zawężeniu |

**Pola i typy (`PozycjaDokHandlowego`):** `Towar: Towar`, `Dokument: DokumentHandlowy`,
`Data: Date`, `Czas: Time`, `KierunekMagazynu: Soneta.Magazyny.KierunekPartii`
(`Rozchód=-1`, `Brak=0`, `Przychód=1`), `Cena: DoubleCy`, `Ilosc: Quantity`. Kolekcja
`towar.Pozycje: SubTable<PozycjaDokHandlowego>`.

**Snippet:**

```csharp
var towar = session.GetTowary().Towary.WgKodu["BIKINI"];
if (towar == null) return;

// Pozycje towaru z ostatnich 6 miesięcy — filtr serwerowy na kolekcji towaru:
var od = Date.Today.AddMonths(-6);
foreach (PozycjaDokHandlowego p in towar.Pozycje[(PozycjaDokHandlowego p) => p.Data >= od])
{
    DokumentHandlowy dok = p.Dokument;          // dokument macierzysty pozycji
    string numer = dok.Numer.NumerPelny;
    // p.KierunekMagazynu, p.Ilosc, p.Cena, p.Data ...
}

// Tylko rozchody (sprzedaż/wydania) danego towaru z dokumentów zatwierdzonych:
foreach (PozycjaDokHandlowego p in towar.Pozycje[(PozycjaDokHandlowego p) =>
             p.KierunekMagazynu == KierunekPartii.Rozchód
             && p.Dokument.Stan == StanDokumentuHandlowego.Zatwierdzony
             && p.Data >= od])
{
    // historia rozchodów towaru
}
```

**Pułapki:**
- Filtruj na `towar.Pozycje[…]` (kolekcja zawężona do jednego towaru), nie iteruj globalnie
  `PozycjeDokHan` — to jedna z największych tabel operacyjnych (safe-code §6.3).
- Warunek przez referencję (`p.Dokument.Stan == …`) jest dozwolony — `Stan` jest polem
  bazodanowym i wygeneruje `JOIN`. Nie używaj w warunku pól kalkulowanych dokumentu
  (np. `p.Dokument.Zatwierdzony` rzuci `LinqConditionException`).
- „Ostatnie N" realizuj przez sortowanie kluczem (`WgKierunek`/`WgDaty`) **po** zawężeniu okresem;
  nie pobieraj całości po to, by wziąć kilka rekordów.
- `KierunekPartii` żyje w `Soneta.Magazyny` — wymagana referencja do modułu Magazyny.

---

### W28 — Wyszukiwanie dokumentów wg okresu, definicji, stanu, serii

**Cel:** odfiltrować dokumenty po kryteriach nagłówkowych (data, definicja, stan, magazyn, seria)
serwerowo, bez obiektów warstwy UI (`View`).

**Warianty:**

| Wariant | Warunek (pole bazodanowe) |
|---|---|
| Okres dat | `dok.Data >= od && dok.Data <= do` |
| Konkretna definicja (symbol) | `dok.Definicja == def` (rekord z `DefDokHandlowych.WgSymbolu[...]`) |
| Stan dokumentu | `dok.Stan == StanDokumentuHandlowego.Zatwierdzony` |
| Magazyn | `dok.Magazyn == mag` |
| Seria | `dok.Seria == "A"` |
| Wiele kryteriów | koniunkcja `&&` / alternatywa `||` w jednym wyrażeniu |

**Pola i typy:** `dok.Data: Date`, `dok.Definicja: DefDokHandlowego`,
`dok.Stan: StanDokumentuHandlowego`, `dok.Magazyn: Magazyn`, `dok.Seria: string`,
`dok.Kategoria: KategoriaHandlowa`. Klucz `WgDaty` daje sortowanie po dacie.

**Snippet:**

```csharp
var hm = session.GetHandel();

var def  = hm.DefDokHandlowych.WgSymbolu["FV"];                 // definicja faktury sprzedaży
var mag  = session.GetMagazyny().Magazyny.WgSymbol["F"];
var od   = new Date(2026, 1, 1);
var doDt = new Date(2026, 3, 31);

// Zatwierdzone faktury FV z I kwartału na magazynie F — jeden warunek serwerowy.
// Klucz WgDaty nadaje sortowanie po Data, Czas:
foreach (DokumentHandlowy dok in hm.DokHandlowe.WgDaty[(DokumentHandlowy dok) =>
             dok.Definicja == def
             && dok.Magazyn == mag
             && dok.Stan == StanDokumentuHandlowego.Zatwierdzony
             && dok.Data >= od && dok.Data <= doDt])
{
    // dok.Numer.NumerPelny, dok.Suma, dok.Kontrahent ...
}

// Wariant: warunek jako wartość przekazywana dalej (np. do metody):
var cond = RowCondition.FromExpression<DokumentHandlowy>(
    dok => dok.Definicja == def && dok.Seria == "A");
foreach (DokumentHandlowy dok in hm.DokHandlowe.WgDaty[cond]) { /* ... */ }
```

**Pułapki:**
- **Nie używaj `View`** w kodzie biznesowym (to obiekt UI) — filtruj `SubTable[expression]` lub
  `RowCondition.FromExpression` ([`rowcondition.md`](../rowcondition.md)).
- Porównuj definicję/magazyn po **rekordzie** (`dok.Definicja == def`), nie po stringu symbolu —
  rekord pobierz raz przez `WgSymbolu[...]`/`WgSymbol[...]` poza pętlą.
- Stan porównuj enumem (`dok.Stan == StanDokumentuHandlowego.Zatwierdzony`); skróty `dok.Zatwierdzony`
  są kalkulowane i **nie wolno** ich użyć w warunku LINQ.
- Wybór klucza (`WgDaty`, `WgMagazynuNumer`, `WgKontrahentaObcy`) decyduje tylko o **sortowaniu** —
  warunek i tak trafia do `WHERE`. Dla dużych zbiorów dobierz klucz pasujący do oczekiwanej kolejności.

---

### W29 — Odczyt dokumentu wg numeru lub Guid

**Cel:** odnaleźć pojedynczy dokument po jego pełnym numerze (`Numer.Pelny`) albo po globalnym
identyfikatorze `Guid` (np. zapisanym wcześniej w innym systemie / w teście).

**Warianty:**

| Wariant | Mechanizm | Zwraca |
|---|---|---|
| Po Guid | `hm.DokHandlowe[guid]` (indeksator `GuidedTable`) | `DokumentHandlowy`; **rzuca `RowNotFoundException`**, gdy brak |
| Po pełnym numerze | filtr serwerowy `dok => dok.Numer.Pelny == numer` | zbiór (bierz `.FirstOrDefault()`) |
| Po numerze w obrębie magazynu | klucz `WgMagazynuNumer` (`Magazyn` + `Numer.Pelny`) | precyzyjniej (numer bywa unikalny per magazyn) |
| Po numerze obcym | klucz `WgMagazynuObcy` / pole `dok.Obcy.Numer` | dokument z numerem dostawcy |

**Pola i typy:** `dok.Numer: NumerDokumentu` (odczyt pełnego numeru: `dok.Numer.NumerPelny`;
pole bazodanowe w warunku: `Numer.Pelny`), `dok.Guid: Guid` (z `GuidedRow`),
`dok.Obcy.Numer: string` (numer dokumentu obcego).

**Snippet:**

```csharp
var hm = session.GetHandel();

// 1. Po Guid — najpewniejszy, jednoznaczny dostęp. UWAGA: indeksator GuidedTable RZUCA
//    RowNotFoundException dla nieznanego Guid (nie zwraca null) — obuduj try/catch, gdy brak pewności:
DokumentHandlowy poGuid;
try { poGuid = hm.DokHandlowe[guid]; }
catch (Soneta.Business.RowNotFoundException) { poGuid = null; }

// 2. Po pełnym numerze — warunek serwerowy na polu bazodanowym Numer.Pelny.
//    Numer może się powtarzać między magazynami, więc bierzemy pierwszy / iterujemy:
DokumentHandlowy poNumerze = hm.DokHandlowe.WgMagazynuNumer[
    (DokumentHandlowy dok) => dok.Numer.Pelny == "FV 1/2026"].FirstOrDefault();

// 3. Po numerze w obrębie magazynu (precyzyjniej — numeracja zwykle per magazyn):
var mag = session.GetMagazyny().Magazyny.WgSymbol["F"];
DokumentHandlowy wMagazynie = hm.DokHandlowe.WgMagazynuNumer[(DokumentHandlowy dok) =>
    dok.Magazyn == mag && dok.Numer.Pelny == "FV 1/2026"].FirstOrDefault();

if (poGuid != null)
{
    string pelny = poGuid.Numer.NumerPelny;     // odczyt pełnego numeru (kalkulowane)
}
```

**Pułapki:**
- W warunku LINQ używaj pola bazodanowego `Numer.Pelny`; do **odczytu** sformatowanego numeru służy
  kalkulowane `dok.Numer.NumerPelny` — w wyrażeniu serwerowym rzuciłoby `LinqConditionException`.
- Pełny numer **nie jest** globalnie unikalny (numeracja bywa per magazyn/seria/rok) — dlatego filtr
  zwraca zbiór; bierz `.FirstOrDefault()` albo dołóż `dok.Magazyn == mag`.
- Indeksator `hm.DokHandlowe[guid]` to dostęp po `Guid` (z `GuidedTable`) — dla nieznanego `Guid`
  **rzuca `Soneta.Business.RowNotFoundException`** (NIE zwraca `null`). Gdy brak pewności istnienia,
  obuduj go `try/catch`. Nie myl z dostępem po `ID` (klucz wewnętrzny tabeli).
- Numer obcy (dostawcy) jest w `dok.Obcy.Numer` — to inne pole niż własny `Numer`.

---

### W30 — Korekty dokumentu i dokument korygowany

**Cel:** dla danego dokumentu ustalić jego korekty (dokumenty korygujące) oraz — dla korekty —
dokument, który koryguje.

**Warianty:**

| Wariant | Pole / kierunek | Typ |
|---|---|---|
| Dokument korygowany przez tę korektę | `korekta.DokumentKorygowany` | `DokumentHandlowy` (lub `null`) |
| Wszystkie korekty danego dokumentu | `dok.DokumentyKorygujące` | `IEnumerable<DokumentHandlowy>` (łańcuch) |
| Najbliższa korekta | `dok.DokumentKorygujący` | `DokumentHandlowy` (lub `null`) |
| Ostatnia korekta w łańcuchu | `dok.DokumentKorygującyOstatni` | `DokumentHandlowy` |
| Czy dokument jest korektą | `dok.Korekta` | `bool` (pole bazodanowe) |
| Serwerowy filtr korekt | `hm.DokHandlowe[d => d.Korekta]` | `SubTable<DokumentHandlowy>` |

**Pola i typy:** `dok.Korekta: bool` (bazodanowe — czy dokument jest korektą),
`dok.DokumentKorygowany: DokumentHandlowy`, `dok.DokumentyKorygujące: IEnumerable<DokumentHandlowy>`,
`dok.DokumentKorygujący`/`DokumentKorygującyOstatni: DokumentHandlowy`,
`dok.DokumentyKorygowane: IEnumerable<DokumentHandlowy>` (cały łańcuch korygowanych) —
wszystkie powiązania **kalkulowane** (tylko do odczytu; korekty zakładaj przez `IRelacjeService`).

**Snippet:**

```csharp
var hm = session.GetHandel();
var dok = hm.DokHandlowe[guid];
if (dok == null) return;

// Korekty tego dokumentu (łańcuch korekt — kolejne korekty korekt):
foreach (DokumentHandlowy korekta in dok.DokumentyKorygujące)
{
    string nr = korekta.Numer.NumerPelny;
    DokumentHandlowy korygowany = korekta.DokumentKorygowany;   // wskazuje z powrotem na dok
}

// Gdy mamy w ręku korektę — odczyt dokumentu korygowanego:
if (dok.Korekta)
{
    DokumentHandlowy zrodlo = dok.DokumentKorygowany;           // dokument pierwotny
}

// Serwerowe wyszukanie samych korekt w okresie (pole Korekta jest bazodanowe):
var od = Date.Today.AddMonths(-1);
foreach (DokumentHandlowy k in hm.DokHandlowe.WgDaty[(DokumentHandlowy d) =>
             d.Korekta && d.Data >= od])
{
    // d.DokumentKorygowany — dokument, którego dotyczy korekta
}
```

**Pułapki:**
- `DokumentKorygowany`/`DokumentyKorygujące`/`DokumentKorygujący` są **kalkulowane** (liczone z
  relacji handlowych) — tylko do odczytu. Tworzenie korekt realizuje `IRelacjeService.NowaKorekta(...)`
  (rozdział o relacjach), nie przypisywanie tych pól.
- W warunku serwerowym wolno użyć tylko pola **`Korekta`** (bazodanowe). Pola powiązań korekt są
  kalkulowane → w LINQ rzucą `LinqConditionException`.
- `DokumentKorygowany` zwraca `null`, gdy dokument **nie** jest korektą (`Korekta == false`) — zawsze
  sprawdź `dok.Korekta` albo `!= null` przed użyciem.
- `DokumentyKorygujące` to **łańcuch** (korekta korekty korekty…), a nie pojedynczy element — gdy
  potrzebujesz tylko najbliższej, użyj `DokumentKorygujący`; gdy ostatniej — `DokumentKorygującyOstatni`.

---

## 6. Magazyn, zasoby, partie, obroty

> Sekcja opisuje **odczyt** efektów magazynowych dokumentu (zasoby, obroty) oraz
> **sterowanie** rozchodem przez wskazanie partii (`GrupaDostaw`) i kontekst wyceny
> (FIFO/LIFO/wg dostaw). Cały kod operuje wyłącznie na **publicznym kontrakcie**
> platformy i jest zgodny z C# 10.
>
> **Klucz do zrozumienia całej sekcji:** magazyn księguje obroty i zasoby **dopiero po
> `Session.Save()`** dokumentu. Samo `Commit()`/`CommitUI()` w transakcji nie nalicza
> stanów. W bazie Demo działa `StanUjemnyVerifier` — **rozchód** (FV/WZ/RW) wymaga
> wcześniejszego **zapisanego** przyjęcia (PW/PZ) tego towaru; w przeciwnym razie zapis
> rozchodu zostanie odrzucony.
>
> **Słowniczek typów (moduł `Soneta.Magazyny`):**
> - `Zasob` (tabela `Zasoby`) — stan towaru: ilość na partii w danym magazynie i okresie.
> - `Obrot` (tabela `Obroty`) — pojedynczy ruch (przychód lub rozchód) wiążący partie.
> - `GrupaDostaw` (tabela `GrupyDostaw`, namespace `Soneta.Magazyny.Dostawy`) — **partia**
>   towaru (identyfikowana `Numer` + `Towar`).
> - `OkresMagazynowy` (tabela `OkresyMag`) — przedział czasu, w którym ewidencjonowane są
>   obroty/zasoby; po zamknięciu blokuje modyfikacje.
> - `PartiaTowaru` — **subrow** (nie tabela) opisujący stronę partii w `Obrot`/`Zasob`:
>   `Dokument`, `PozycjaIdent`, `PartiaTowaru: GrupaDostaw`, `KontrahentPartii`, `Data`, `Czas`, `Typ`, `Wartosc`.
> - Enum `KierunekPartii`: `Rozchód=-1`, `Brak=0`, `Przychód=1`.
> - Enum `Magazyn.Algorytm` (`AlgorytmMagazynowy`): `FIFO=0`, `LIFO=1`, `NieLiczyćStanów=2`,
>   `WgDostawy=3`, `WgDostawyPrzyZatwierdzaniu=10`, `OdNajdroższych=4`, `OdNajtańszych=5`,
>   `WgCechyPozycji=6/7`, `WgCechyDokumentu=8/9`.
>
> Dostęp do modułu: `var mag = session.GetMagazyny();` → `mag.Zasoby`, `mag.Obroty`,
> `mag.GrupyDostaw`, `mag.OkresyMag`, `mag.Magazyny`.

---

### W31 — Przeglądanie zasobów utworzonych przez dokument przychodowy (`dok.Zasoby`)

**Cel:** po zapisaniu dokumentu przychodowego (PW/PZ/FZ) odczytać zasoby magazynowe,
które ten dokument wprowadził na stan — np. żeby zweryfikować ilości albo powiązać je z
partią.

**Warianty:**

| Wariant | Źródło | Uwaga |
|---|---|---|
| Zasoby utworzone bezpośrednio przez dokument | `dok.Zasoby` (`SubTable<Zasob>`) | filtr po `Partia.Dokument == dok` |
| Zasoby łącznie z dokumentami zależnymi | `dok.ZasobyWszystkie` (`ListWithView`) | obejmuje powiązane dok. magazynowe |
| Iteracja po module | `mag.Zasoby.WgTowar[towar, okres, magazyn]` | gdy nie mamy uchwytu do dokumentu |

**Pola i typy:** `dok.Zasoby: SubTable` (elementy `Soneta.Magazyny.Zasob`). `Zasob`:
`Ilosc: Quantity`, `IloscRezerwowana: Quantity`, `Kierunek: KierunekPartii`,
`Magazyn: Magazyn`, `Towar: Towar`, `Okres: OkresMagazynowy`, `Partia: PartiaTowaru` (subrow),
`PartiaPierwotna: PartiaTowaru`.

**Snippet:**

```csharp
// dok — zapisany dokument przychodowy (PW/PZ/FZ), po session.Save()
var mag = session.GetMagazyny();

foreach (Zasob z in dok.Zasoby)
{
    // strona partii zasobu: skąd pochodzi (dokument, pozycja, numer partii)
    GrupaDostaw partia = z.Partia.PartiaTowaru;   // rekord partii (może być null dla prostej ewidencji)
    Console.WriteLine(
        $"{z.Towar.Kod}  mag={z.Magazyn.Symbol}  kierunek={z.Kierunek}  " +
        $"ilość={z.Ilosc}  partia={partia?.Numer}");
}
```

**Pułapki:**
- `dok.Zasoby` jest **puste, dopóki nie wykonasz `session.Save()`** — przed zapisem magazyn
  nie zaksięgował zasobów (sam `Commit`/`CommitUI` nie wystarcza).
- Wzorzec testowy: zapis dokumentu → `SaveDispose()` → odczyt na świeżej sesji po `Guid`,
  bo po `Save()` w środku testu okno edycji się zamyka.
- Zasób przychodowy ma `Kierunek == KierunekPartii.Przychód`. Zasób rozchodowy na stanie
  ujemnym ma `Kierunek == KierunekPartii.Rozchód` — nie myl ich przy sumowaniu stanu.
- Nie modyfikuj `Zasob`/`Obrot` ręcznie — to tabele wyliczane przez moduł magazynowy.

---

### W32 — Przetwarzanie obrotów faktury sprzedaży i dokumentu rozchodowego (`dok.Obroty`, `dok.ObrotyWszystkie`)

**Cel:** odczytać obroty magazynowe (ruchy) wygenerowane przez dokument — rozchód
(FV/WZ/RW) lub przychód — w tym obroty z dokumentów zależnych.

**Warianty:**

| Wariant | Property | Co zwraca |
|---|---|---|
| Obroty związane bezpośrednio z dokumentem | `dok.Obroty` (`SubTable`) | dla przychodu: po stronie przychodowej; dla rozchodu: po stronie rozchodowej |
| Wszystkie obroty (z dok. zależnymi, bez storna zasobu) | `dok.ObrotyWszystkie` (`ListWithView`) | obroty wszystkich powiązanych dok. magazynowych |
| Obroty wszystkich pozycji | `dok.ObrotyWszystkiePozycji` (`ListWithView`) | po pozycjach (z pozycjami zależnymi) |
| Z korektami, wg partii pierwotnej | `dok.ObrotyWszystkieWgPartiiPierwotnej` (`ListWithView`) | uwzględnia dok. korygujące |

**Pola i typy:** `Obrot`: `Ilosc: Quantity`, `Towar: Towar`, `Magazyn: Magazyn`,
`Okres: OkresMagazynowy`, `Data: Date`, `Czas: Time`, `Korekta: KorektaObrotu`,
`Stornowany: Obrot`, `Przychod: PartiaTowaru`, `Rozchod: PartiaTowaru`,
`PrzychodPierwotny: PartiaTowaru`.

**Snippet:**

```csharp
// dok — zapisana faktura sprzedaży / dokument rozchodowy (po session.Save())
// 1) Obroty samego dokumentu (strona dobrana automatycznie wg kierunku magazynu):
foreach (Obrot o in dok.Obroty)
{
    // Przychod/Rozchod to subrow PartiaTowaru — wskazuje partię i dokument źródłowy
    GrupaDostaw partiaRozchodu = o.Rozchod.PartiaTowaru;     // z której partii zszedł towar
    GrupaDostaw partiaPrzychodu = o.Przychod.PartiaTowaru;   // partia przychodowa (źródło)
    Console.WriteLine($"{o.Towar.Kod}  ilość={o.Ilosc}  z partii={partiaPrzychodu?.Numer}");
}

// 2) Wszystkie obroty łącznie z dokumentami magazynowymi powiązanymi z fakturą:
foreach (Obrot o in dok.ObrotyWszystkie.Cast<Obrot>())
{
    if (o.Korekta == KorektaObrotu.StornoZasobu) continue;   // ObrotyWszystkie już to pomija
    // ... agregacja ilości/wartości
}
```

**Pułapki:**
- `dok.Obroty` automatycznie dobiera stronę (przychodowa vs rozchodowa) na podstawie
  kierunku magazynowego dokumentu — nie filtruj jej ręcznie po kierunku.
- `ObrotyWszystkie`/`ObrotyWszystkiePozycji`/`ObrotyWszystkieWgPartiiPierwotnej` zwracają
  `ListWithView` — iteruj przez `.Cast<Obrot>()`. Pomijają już obroty `StornoZasobu`.
- Obroty pojawiają się **po `Session.Save()`** dokumentu, nie po `Commit()`.
- `Przychod`/`Rozchod`/`PrzychodPierwotny` to **subrow `PartiaTowaru`**, nie rekord partii —
  do rekordu `GrupaDostaw` sięgaj przez `.PartiaTowaru`, do dokumentu źródłowego przez
  `.Dokument`, do pozycji przez `.PozycjaIdent`.

---

### W33 — Odczyt stanu magazynowego towaru (magazyn / data) — `mag.Zasoby` z filtrem

**Cel:** wyliczyć aktualny stan towaru w danym magazynie (i ewentualnie okresie), bez
otwierania konkretnego dokumentu — np. do walidacji dostępności przed rozchodem.

**Warianty:**

| Wariant | Indeks | Sygnatura |
|---|---|---|
| Stan towaru w magazynie | `mag.Zasoby.WgTowar[towar, okres, magazyn]` | zawęź serwerowo do magazynu i okresu |
| Stan towaru we wszystkich okresach/magazynach | `mag.Zasoby.WgTowar[towar]` | szersze — sumuj ostrożnie |
| Zasoby konkretnej partii | `mag.Zasoby.WgPartiaTowaruMagazyn[partia, magazyn, towar]` | gdy znamy `GrupaDostaw` |
| Zasoby magazynu w okresie | `mag.Zasoby.WgMagazyn[magazyn, okres]` | przegląd całego magazynu |

**Pola i typy:** `mag.Zasoby: Zasoby` (tabela). Indeksy zwracają `SubTable<Zasob>`.
`OkresMagazynowy` z `mag.OkresyMag` (patrz W39). Ilości to `Quantity`.

**Snippet:**

```csharp
var mag = session.GetMagazyny();
var towar = session.GetTowary().Towary.WgKodu["BIKINI"];
var magazyn = mag.Magazyny.WgSymbol["F"];
var okres = mag.OkresyMag.WgOkres[Date.Today];   // okres obejmujący dzień (patrz W39)

// Stan = suma ilości zasobów przychodowych pomniejszona o rozchodowe (stan ujemny)
Quantity stan = new(0, towar.JednostkaMag.Symbol);
foreach (Zasob z in mag.Zasoby.WgTowar[towar, okres, magazyn])
{
    if (z.Kierunek == KierunekPartii.Przychód)
        stan += z.Ilosc;
    else if (z.Kierunek == KierunekPartii.Rozchód)
        stan -= z.Ilosc;
}
```

**Pułapki:**
- **Nie ładuj całej tabeli `Zasoby` do pamięci** — zawsze zawężaj indeksem
  (`WgTowar[...]`, `WgMagazyn[...]`, `WgPartiaTowaruMagazyn[...]`). Patrz `safe-code.md` §6.
- Ilości są typu `Quantity` (ilość + jednostka), nie `double` — operuj na `Quantity` i
  pilnuj zgodności jednostek (`z.Ilosc.Symbol`).
- Stan „na dzień" zależy od okresu magazynowego — dla daty historycznej wybierz właściwy
  `OkresMagazynowy`, nie zawsze bieżący.
- Towary **bez magazynu** (np. usługi „MONTAZ", „TRANSPORT" w Demo) nie mają zasobów —
  zapytanie zwróci pustą kolekcję.
- W bazie Demo stan ujemny jest blokowany przy zapisie rozchodu — odczyt stanu służy do
  wcześniejszej walidacji, ale ostateczną kontrolę i tak wykona `Session.Save()`.

---

### W34 — Wyszukiwanie partii magazynowych (`GrupaDostaw`) według cech

**Cel:** odnaleźć partię (`GrupaDostaw`) po numerze, towarze lub cesze (np. numer serii,
data ważności zapisana jako cecha), zanim wskażemy ją przy rozchodzie.

**Warianty:**

| Wariant | Klucz / mechanizm | Uwaga |
|---|---|---|
| Po numerze + towarze | `mag.GrupyDostaw.WgNumer[numer, towar]` | klucz unikalny — pojedynczy rekord lub null |
| Po numerze (zbiór) | `mag.GrupyDostaw.WgNumer[numer]` | zwraca `SubTable<GrupaDostaw>` |
| Wszystkie partie towaru | `mag.GrupyDostaw.WgTowar[towar]` | partie danego towaru |
| Po dacie | `mag.GrupyDostaw.WgData[data]` | indeks po `Data` |
| Po cesze | `partie[(GrupaDostaw g) => warunek]` na indeksie | cecha musi być zdefiniowana |

**Pola i typy:** `GrupaDostaw`: `Numer: string` (`public virtual`, czasem nadawany
automatycznie), `Towar: Towar`, `Data: Date`, `Blokada: bool`,
`Features: FeatureCollection`, `KodKreskowy: string`. Klucz `WgNumer` = (`Numer`, `Towar`).

**Snippet:**

```csharp
var mag = session.GetMagazyny();
var towar = session.GetTowary().Towary.WgKodu["BIKINI"];

// 1) Partia po numerze i towarze — klucz unikalny:
GrupaDostaw partia = mag.GrupyDostaw.WgNumer["LOT-2026-001", towar];

// 2) Wszystkie niezablokowane partie towaru — filtr serwerowy na indeksie:
foreach (GrupaDostaw g in mag.GrupyDostaw.WgTowar[(GrupaDostaw g) => !g.Blokada])
{
    // odczyt cechy zapisanej na partii (np. numer serii / data ważności):
    object seria = g.Features["NumerSerii"];   // cecha musi być wcześniej zdefiniowana
}

// 3) Filtr po dacie powstania partii:
foreach (GrupaDostaw g in mag.GrupyDostaw.WgData[Date.Today]) { /* ... */ }
```

**Pułapki:**
- `WgNumer[numer, towar]` zwraca **pojedynczy** rekord (może być `null`); `WgNumer[numer]`
  i `WgTowar[towar]` zwracają **zbiór** (`SubTable`).
- W `RowCondition` używaj tylko **pól bazodanowych** (`Numer`, `Towar`, `Data`, `Blokada`).
  Pola kalkulowane (np. `KodKreskowy`) i wartości cech rzucą `LinqConditionException` —
  cechę filtruj dopiero po materializacji albo przez dedykowany warunek na cesze.
- Cecha (`Features["…"]`) wymaga wcześniej zdefiniowanej definicji cechy — odwołanie do
  niezdefiniowanej cechy rzuca wyjątek (patrz `features.md`).
- `Numer` partii bywa **nadawany automatycznie** (autonumerowanie wg karty towaru lub wg
  cechy) — nie zakładaj, że zawsze ustawisz go ręcznie.

---

### W35 — Dokument rozchodowy ze wskazaniem JEDNEJ partii

**Cel:** wystawić rozchód (WZ/RW/FV), w którym pozycja schodzi z **konkretnej, wskazanej
partii** — a nie z partii wybranej automatycznie przez algorytm magazynu.

**Warianty:**

| Wariant | Mechanizm | Uwaga |
|---|---|---|
| Wskazanie partii przez pozycję dostawy | `poz.Dostawa = pozycjaPrzyjęcia` | `Dostawa: PozycjaDokHandlowego` (pozycja PW/PZ) |
| Wskazanie partii pierwotnej | `poz.DostawaPierwotna` | dla łańcucha korekt |
| Tryb wskazania na definicji | `DefDokHandlowego.WskazaniePartii` | `WyborPartiiOpcje` (Dozwolony/Wymuszony…) |
| Identyfikacja przez cechę | gdy magazyn `WgCechyPozycji` | partia wybierana wg cechy pozycji (W37, W39) |

**Pola i typy:** `poz.Dostawa: PozycjaDokHandlowego` (kategoria „Magazyn", opis „Pozycja
dostawy dla danego rozchodu magazynowego"). Tryb sterowany przez
`DefDokHandlowego.WskazaniePartii: WyborPartiiOpcje` (`Zabroniony=0`, `Dozwolony=1`,
`Automatyczny=2`, `Wymuszony=4`, `WymuszonyDodawanie`, `WymuszonyZatwierdzanie`,
`WgTowaru=8`).

**Snippet:**

```csharp
var mag = session.GetMagazyny();
var towar = session.GetTowary().Towary.WgKodu["BIKINI"];
var magazyn = mag.Magazyny.WgSymbol["F"];

// WARUNEK WSTĘPNY: istnieje ZAPISANE przyjęcie (PW/PZ) tego towaru (Demo blokuje stan ujemny).
// Znajdź pozycję przyjęcia odpowiadającą partii, z której chcemy zejść:
GrupaDostaw partia = mag.GrupyDostaw.WgNumer["LOT-2026-001", towar];
Obrot przychod = mag.Obroty.WgPrzychodPartiaTowaruMagazyn[partia, magazyn, towar]
                    .Cast<Obrot>().FirstOrDefault();
PozycjaDokHandlowego pozycjaPrzyjecia = przychod?.Przychod.Dokument?
    .Pozycje.Cast<PozycjaDokHandlowego>()
    .FirstOrDefault(p => p.Towar == towar);

using (var t = session.Logout(editMode: true))
{
    var dok = new DokumentHandlowy();
    session.AddRow(dok);
    dok.Definicja = session.GetHandel().DefDokHandlowych.WgSymbolu["WZ"];
    dok.Magazyn = magazyn;

    var poz = new PozycjaDokHandlowego(dok);
    session.AddRow(poz);
    poz.Towar = towar;                                     // USTAW PIERWSZY
    poz.Ilosc = new Quantity(2, poz.Ilosc.Symbol);
    poz.Dostawa = pozycjaPrzyjecia;                        // WSKAZANIE JEDNEJ partii (dostawy)
    t.Commit();                                            // CommitUI() w workerze/extenderze
}
session.Save();                                            // tu nalicza się obrót/zasób rozchodowy
```

**Pułapki:**
- Wskazanie partii działa tylko, gdy definicja dokumentu na to pozwala
  (`WskazaniePartii != Zabroniony`). Przy `Zabroniony` partia jest dobierana wyłącznie
  algorytmem magazynu — ustawienie `poz.Dostawa` zostanie zignorowane lub odrzucone.
- `poz.Dostawa` to **pozycja dokumentu przyjęcia** (`PozycjaDokHandlowego`), a nie rekord
  `GrupaDostaw`. Partię `GrupaDostaw` mapujesz na pozycję przyjęcia przez obrót przychodowy
  (`Obrot.Przychod.Dokument` + `PozycjaIdent`) — jak w snippetcie.
- Demo blokuje stan ujemny: bez **zapisanego** przyjęcia tej partii `Session.Save()`
  rozchodu rzuci wyjątek (`StanUjemnyVerifier`).
- Pozycje obu dokumentów muszą być w **tej samej sesji** — nie mieszaj rekordów z różnych
  sesji (`session.Get(...)`).
- Ustaw `poz.Dostawa` **przed** `Commit()`; właściwy obrót zostaje naliczony dopiero w
  `Save()`.

---

### W36 — Dokument rozchodowy ze wskazaniem WIELU partii

**Cel:** wystawić rozchód, którego ilość pochodzi z **kilku różnych partii** (np. 10 szt:
6 z LOT-A, 4 z LOT-B) — każda partia jako osobna pozycja rozchodu wskazująca swoją dostawę.

**Warianty:**

| Wariant | Mechanizm | Uwaga |
|---|---|---|
| Pozycja per partia | po jednej `PozycjaDokHandlowego` na każdą wskazaną dostawę | najprostszy, czytelny |
| Wybór przez worker dostaw | `IRelacjeService` + `HandlerSet.WybierzDostawyCallback` | dla relacji nadrzędny→podrzędny |
| Automatyczny rozdział wg algorytmu | `WskazaniePartii = Automatyczny` | platforma sama dzieli na partie |

**Pola i typy:** jak W35 — wiele pozycji, każda z własnym `poz.Dostawa` i `poz.Ilosc`.
Przy generowaniu z dokumentu nadrzędnego: `IRelacjeService.NowyPodrzednyIndywidualny(...)`
z `HandlerSet { WybierzDostawyCallback = ... }` (namespace
`Soneta.Handel.RelacjeDokumentow.Api`, wymaga `using Microsoft.Extensions.DependencyInjection;`).

**Snippet:**

```csharp
var mag = session.GetMagazyny();
var towar = session.GetTowary().Towary.WgKodu["BIKINI"];
var magazyn = mag.Magazyny.WgSymbol["F"];

// Mapowanie: numer partii -> ilość do zejścia
var rozdzial = new (string numer, double ilosc)[] { ("LOT-A", 6), ("LOT-B", 4) };

using (var t = session.Logout(editMode: true))
{
    var dok = new DokumentHandlowy();
    session.AddRow(dok);
    dok.Definicja = session.GetHandel().DefDokHandlowych.WgSymbolu["WZ"];
    dok.Magazyn = magazyn;

    foreach (var (numer, ilosc) in rozdzial)
    {
        GrupaDostaw partia = mag.GrupyDostaw.WgNumer[numer, towar];
        Obrot przychod = mag.Obroty.WgPrzychodPartiaTowaruMagazyn[partia, magazyn, towar]
                            .Cast<Obrot>().FirstOrDefault();
        PozycjaDokHandlowego dostawa = przychod?.Przychod.Dokument?
            .Pozycje.Cast<PozycjaDokHandlowego>().FirstOrDefault(p => p.Towar == towar);

        var poz = new PozycjaDokHandlowego(dok);
        session.AddRow(poz);
        poz.Towar = towar;
        poz.Ilosc = new Quantity(ilosc, poz.Ilosc.Symbol);
        poz.Dostawa = dostawa;                 // każda pozycja wskazuje INNĄ partię
    }
    t.Commit();
}
session.Save();
```

**Pułapki:**
- Każda wskazana partia = **osobna pozycja** rozchodu. Nie da się jedną pozycją wskazać
  dwóch różnych partii — `poz.Dostawa` to pojedyncza referencja.
- Suma ilości wskazanych partii musi mieścić się w zapisanym stanie każdej partii
  (Demo blokuje stan ujemny per partia).
- Przy generowaniu z dokumentu nadrzędnego (ZO→FV) wybór wielu dostaw realizuje
  `HandlerSet.WybierzDostawyCallback` — brak implementacji callbacku przy
  `WyborPozycjiDlaRelacji != BrakOkna` skutkuje `NotImplementedException`.
- Wszystkie pozycje w jednej transakcji edycyjnej, zapis raz przez `Session.Save()`.

---

### W37 — Dokument przyjęcia (PW/PZ) z numerem serii — zapis numeru serii jako cecha

**Cel:** zarejestrować przyjęcie towaru i zapisać **numer serii / partii**. Jeśli nie ma
dedykowanego pola na serię, numer przenosimy jako **cechę** (`Features`) pozycji/dokumentu,
skąd platforma przenosi go na partię (`GrupaDostaw`) i obrót.

**Warianty:**

| Wariant | Mechanizm | Uwaga |
|---|---|---|
| Numer partii wprost | `GrupaDostaw.Numer` | gdy partia jest tworzona/wskazywana jawnie |
| Numer serii jako cecha pozycji | `poz.Features["NumerSerii"] = "..."` | przenoszony na partię/obrót |
| Autonumerowanie wg cechy | `WyborPartiiAutonumerowanie.WgCechy` | numer partii brany z cechy |
| Data ważności jako cecha | `poz.Features["DataWaznosci"] = date` | analogicznie do serii |

**Pola i typy:** `dok.Features["…"]` i `poz.Features["…"]`
(`FeatureCollection`, indeksator po nazwie definicji cechy, zwraca/przyjmuje `object`).
`GrupaDostaw.Numer: string`. Tryb numeracji partii:
`WyborPartiiAutonumerowanie` (`Brak=0`, `Standardowe=1`, `WgCechy=2`).

**Snippet:**

```csharp
var mag = session.GetMagazyny();
var towar = session.GetTowary().Towary.WgKodu["BIKINI"];

using (var t = session.Logout(editMode: true))
{
    var dok = new DokumentHandlowy();
    session.AddRow(dok);
    dok.Definicja = session.GetHandel().DefDokHandlowych.WgSymbolu["PW"];   // przyjęcie
    dok.Magazyn = mag.Magazyny.WgSymbol["F"];

    var poz = new PozycjaDokHandlowego(dok);
    session.AddRow(poz);
    poz.Towar = towar;
    poz.Ilosc = new Quantity(10, poz.Ilosc.Symbol);
    poz.Cena = new DoubleCy(5m, poz.Cena.Symbol);

    // Numer serii jako cecha pozycji — przeniesiony na partię/obrót po Save:
    poz.Features["NumerSerii"] = "LOT-2026-001";    // definicja cechy musi istnieć
    t.Commit();
}
session.Save();

// Po zapisie partia jest dostępna w GrupyDostaw; numer serii odczytasz z cechy partii:
GrupaDostaw partia = mag.GrupyDostaw.WgTowar[towar].Cast<GrupaDostaw>()
    .FirstOrDefault(g => Equals(g.Features["NumerSerii"], "LOT-2026-001"));
```

**Pułapki:**
- Cecha musi być **wcześniej zdefiniowana** (`FeatureSetDefinition`) i — by przenosiła się
  na partię — odpowiednio skonfigurowana w module magazynowym. Odwołanie do niezdefiniowanej
  cechy rzuca wyjątek.
- Partia powstaje dopiero **po `Session.Save()`** przyjęcia — przed zapisem
  `mag.GrupyDostaw` jej nie zawiera.
- Gdy magazyn ma autonumerowanie `WgCechy`, `GrupaDostaw.Numer` jest **wyliczany z cechy** —
  nie ustawiaj go ręcznie sprzecznie z cechą.
- Filtr partii po wartości cechy rób **po materializacji** (jak w snippetcie) — wartości
  cech nie są polami bazodanowymi, więc nie wejdą do `RowCondition`.

---

### W38 — Odczyt rozchodu zasobów: powiązanie pozycji rozchodu z partią pierwotną / przyjęciem

**Cel:** dla pozycji/obrotu rozchodowego ustalić, **z której partii (i którego przyjęcia)**
zszedł towar — np. do raportu pochodzenia (traceability) lub rozliczenia kosztu.

**Warianty:**

| Wariant | Źródło | Co zwraca |
|---|---|---|
| Partia rozchodu | `obrot.Rozchod.PartiaTowaru` | `GrupaDostaw` strony rozchodowej |
| Partia przychodowa (źródłowa) | `obrot.Przychod.PartiaTowaru` | partia, z której zszedł towar |
| Partia pierwotna | `obrot.PrzychodPierwotny.PartiaTowaru` | pierwotne przyjęcie (przed korektami) |
| Dokument/pozycja źródłowa | `obrot.Przychod.Dokument`, `.PozycjaIdent` | przyjęcie i jego pozycja |
| Dostawa na pozycji rozchodu | `poz.Dostawa`, `poz.DostawaPierwotna` | pozycja przyjęcia powiązana z rozchodem |

**Pola i typy:** subrow `PartiaTowaru` na `Obrot`/`Zasob`:
`Dokument: DokumentHandlowy`, `PozycjaIdent: int`, `PartiaTowaru: GrupaDostaw`,
`KontrahentPartii: Kontrahent`, `Data: Date`, `Czas: Time`, `Typ: TypPartii`,
`Wartosc: decimal`. Na pozycji: `poz.Dostawa: PozycjaDokHandlowego`,
`poz.DostawaPierwotna: PozycjaDokHandlowego`.

**Snippet:**

```csharp
// dok — zapisany dokument rozchodowy (FV/WZ/RW)
foreach (Obrot o in dok.Obroty)
{
    // Strona rozchodowa = partia, z której zeszła ilość:
    GrupaDostaw partiaRozchodu = o.Rozchod.PartiaTowaru;

    // Strona przychodowa = przyjęcie, z którego pochodzi towar (pochodzenie):
    DokumentHandlowy przyjecie = o.Przychod.Dokument;
    GrupaDostaw partiaZrodlowa = o.Przychod.PartiaTowaru;

    // Pierwotne przyjęcie (przed łańcuchem korekt):
    GrupaDostaw partiaPierwotna = o.PrzychodPierwotny.PartiaTowaru;

    Console.WriteLine(
        $"{o.Towar.Kod}  ilość={o.Ilosc}  z przyjęcia={przyjecie?.Numer}  " +
        $"partia={partiaZrodlowa?.Numer}  kontrahent={o.Przychod.KontrahentPartii?.Kod}");
}

// Powiązanie na poziomie pozycji rozchodu:
foreach (PozycjaDokHandlowego poz in dok.Pozycje)
{
    PozycjaDokHandlowego pozycjaPrzyjecia = poz.Dostawa;   // pozycja PW/PZ
}
```

**Pułapki:**
- Rozróżniaj `Przychod` (źródło, czyli przyjęcie), `Rozchod` (bieżący rozchód) i
  `PrzychodPierwotny` (źródło sprzed korekt). Do raportu pochodzenia używaj `Przychod`/
  `PrzychodPierwotny`.
- `obrot.Przychod`/`Rozchod` to **subrow `PartiaTowaru`** — nie jest `null` jako struktura,
  ale jego pola (np. `PartiaTowaru`, `Dokument`) mogą być puste dla prostej ewidencji bez
  partii. Zabezpiecz odczyt `?.`.
- Jedna pozycja rozchodu może wygenerować **wiele obrotów** (gdy zeszła z kilku przychodów,
  np. FIFO) — iteruj po obrotach, nie zakładaj relacji 1:1 pozycja↔partia.
- Odczyt sensowny dopiero **po `Session.Save()`** dokumentu (przed zapisem brak obrotów).

---

### W39 — Odczyt okresów magazynowych i kontekstu wyceny (FIFO/LIFO/wg dostaw)

**Cel:** ustalić aktywny okres magazynowy dla daty oraz dowiedzieć się, jakim algorytmem
magazyn wycenia rozchód (co decyduje o wyborze partii, gdy nie wskazujemy jej ręcznie).

**Warianty:**

| Wariant | Źródło | Uwaga |
|---|---|---|
| Okres dla daty | `mag.OkresyMag.WgOkres[data]` | klucz po `Okres.To` |
| Czy okres zamknięty | `okres.Zamkniety: bool` | zamknięcie blokuje modyfikacje |
| Algorytm rozchodu magazynu | `magazyn.Algorytm: AlgorytmMagazynowy` | FIFO/LIFO/wg dostaw/wg cechy |
| Cecha algorytmu (wg cechy) | `magazyn.CechaAlgorytmu: string` | nazwa cechy pozycji/dokumentu |

**Pola i typy:** `OkresMagazynowy`: `Okres: FromTo`, `Zamkniety: bool`. Tabela `OkresyMag`,
indeks `WgOkres` (po `Okres.To`). `Magazyn.Algorytm: AlgorytmMagazynowy` (`FIFO=0`,
`LIFO=1`, `NieLiczyćStanów=2`, `WgDostawy=3`, `WgDostawyPrzyZatwierdzaniu=10`,
`OdNajdroższych=4`, `OdNajtańszych=5`, `WgCechyPozycji=6/7`, `WgCechyDokumentu=8/9`),
`Magazyn.CechaAlgorytmu: string`.

**Snippet:**

```csharp
var mag = session.GetMagazyny();
var magazyn = mag.Magazyny.WgSymbol["F"];

// Okres magazynowy obejmujący wskazaną datę:
OkresMagazynowy okres = mag.OkresyMag.WgOkres[Date.Today];
bool zamkniety = okres != null && okres.Zamkniety;

// Kontekst wyceny rozchodu (jak magazyn dobiera partie automatycznie):
AlgorytmMagazynowy algorytm = magazyn.Algorytm;
bool rozchodWgCechy =
    algorytm is AlgorytmMagazynowy.WgCechyPozycji or AlgorytmMagazynowy.WgCechyPozycjiMalejąco
            or AlgorytmMagazynowy.WgCechyDokumentu or AlgorytmMagazynowy.WgCechyDokumentuMalejąco;

string cechaWyceny = rozchodWgCechy ? magazyn.CechaAlgorytmu : null;

string opisWyceny = algorytm switch
{
    AlgorytmMagazynowy.FIFO => "rozchód od najstarszych dostaw",
    AlgorytmMagazynowy.LIFO => "rozchód od najnowszych dostaw",
    AlgorytmMagazynowy.WgDostawy => "rozchód wg wskazanej dostawy (partii)",
    _ => algorytm.ToString()
};
```

**Pułapki:**
- Gdy magazyn liczy `WgDostawy` (wskazanie partii) lub `WgCechy*`, automatyczny dobór partii
  zależy od `poz.Dostawa` (W35/W36) lub cechy (`CechaAlgorytmu`) — bez nich rozchód nie
  zostanie poprawnie rozliczony.
- `NieLiczyćStanów` oznacza, że magazyn **nie prowadzi zasobów** — `dok.Zasoby` pozostanie
  puste, a kontroli stanu ujemnego nie ma.
- Modyfikacja dokumentów w **zamkniętym** okresie (`okres.Zamkniety == true`) zostanie
  odrzucona — sprawdź to przed edycją wstecz.
- `OkresMagazynowy` to dane konfiguracyjne (`config="true"`, `guided`) — nie twórz okresów
  „w locie" w kodzie operacyjnym; korzystaj z istniejących.

---

## 7. Cechy (Features)

Cechy (Features) to dodatkowe, definiowalne informacje przypisane do `Row` — tu: do dokumentu
(`DokumentHandlowy`) i pozycji (`PozycjaDokHandlowego`). Definicje cech (`FeatureDefinition`) tworzy
się we wdrożeniu (bez konwersji bazy); cecha jest adresowana **po nazwie definicji**. Dostęp daje
property `Features` (`Soneta.Business.FeatureCollection`) oraz nietypowany indeksator `Row["Nazwa"]`.
Fundamenty cech opisuje `references/features.md` — tu pokazujemy ich użycie na dokumencie handlowym.

> Cechy są częścią publicznego kontraktu. **Samo przenoszenie cech** (z partii / z dokumentu
> nadrzędnego) jest sterowane **konfiguracją definicji dokumentu/relacji**, a nie wywoływane
> imperatywnie z dodatku — patrz W40.

---

### W40 — Przenoszenie cech z partii (dostawy) / towaru na pozycję dokumentu

**Cel:** sprawić, by przy rozchodzie magazynowym cechy zapisane na partii (dostawie) trafiły na
pozycję dokumentu rozchodowego, a przy przekształceniach w relacjach — by cechy dokumentu/pozycji
nadrzędnej zostały skopiowane na dokument podrzędny. To mechanizm **konfiguracyjny**: ustawiasz flagi
na `DefDokHandlowego` / definicji relacji, platforma kopiuje cechy automatycznie podczas operacji.

**Warianty:**

| Wariant | Gdzie ustawić | Pole / mechanizm |
|---|---|---|
| Partia (dostawa) → pozycja rozchodu | definicja dokumentu rozchodowego (WZ/RW/FV) | `DefDokHandlowego.KopiujCechyDostawy: bool` |
| Dokument nadrzędny → podrzędny (cechy nagłówka) | definicja relacji | `KopiujCechyDokumentu: bool` |
| Dokument nadrzędny → podrzędny (cechy pozycji) | definicja relacji | `KopiujCechyPozycji: bool` |
| Wybrane cechy + synchronizacja zwrotna | definicja relacji | konfiguracja „kopiuj cechy" z listą definicji + flagą synchronizacji |
| Ręczne dopisanie cechy na pozycji | kod dodatku | `poz["Nazwa"] = wartość` w transakcji (W41) |

**Pola i typy:**
- `DefDokHandlowego.KopiujCechyDostawy: bool` — „Kopiuj cechy z dostawy"; włącza przeniesienie cech
  partii na pozycję dokumentu **rozchodowego** przy wskazaniu zasobu / księgowaniu rozchodu.
- Na definicji relacji: `KopiujCechyDokumentu: bool`, `KopiujCechyPozycji: bool` — wymuszają
  kopiowanie cech (nagłówka / pozycji) z dokumentu nadrzędnego na podrzędny.
- `poz.Features` / `poz["Nazwa"]` — odczyt/zapis cechy pozycji (typ `FeatureCollection` / `object`).
- Warunkiem działania jest istnienie **tej samej definicji cechy** zarejestrowanej dla obu tabel
  (`PozycjeDokHan`, ewentualnie partia/towar) — kopiowane są cechy o zgodnej nazwie.

**Snippet:**

```csharp
// Włączenie przenoszenia cech z dostawy na pozycję rozchodu — konfiguracja definicji WZ.
// (jednorazowo, na etapie wdrożenia; wykonywane w sesji KONFIGURACYJNEJ)
var handel = session.GetHandel();
var defWZ = handel.DefDokHandlowych.WgSymbolu["WZ"];

using (var t = session.Logout(editMode: true))
{
    defWZ.KopiujCechyDostawy = true;   // cechy partii trafią na pozycję dokumentu rozchodowego
    t.Commit();
}
session.Save();

// Po włączeniu flagi: tworzysz przyjęcie z cechą partii, a przy rozchodzie (wskazanie zasobu)
// cecha jest kopiowana na pozycję automatycznie — nie kopiujesz jej w kodzie.
// Przyjęcie (PW/PZ) — cecha "NrSerii" zapisana na pozycji = cecha dostawy/partii:
using (var t = session.Logout(editMode: true))
{
    var pw = new DokumentHandlowy();
    session.AddRow(pw);
    pw.Definicja = handel.DefDokHandlowych.WgSymbolu["PW"];
    pw.Magazyn = session.GetMagazyny().Magazyny.WgSymbol["F"];

    var poz = new PozycjaDokHandlowego(pw);
    session.AddRow(poz);
    poz.Towar = session.GetTowary().Towary.WgKodu["BIKINI"];
    poz.Ilosc = new Quantity(10, poz.Ilosc.Symbol);
    poz.Cena  = new DoubleCy(5m, poz.Cena.Symbol);
    poz["NrSerii"] = "S-2026-001";    // cecha partii (definicja "NrSerii" dla PozycjeDokHan)

    pw.Stan = StanDokumentuHandlowego.Zatwierdzony;
    t.Commit();
}
session.Save();                        // dopiero teraz powstaje zasób/partia z cechą

// Rozchód WZ ze wskazaniem partii — cecha "NrSerii" pojawi się na pozycji WZ
// dzięki KopiujCechyDostawy = true (kopiowane przez platformę przy księgowaniu rozchodu).
```

**Pułapki:**
- Przeniesienie cech z dostawy to **konfiguracja**, nie API: bez `KopiujCechyDostawy = true` na
  definicji dokumentu rozchodowego nic się nie skopiuje — nie próbuj „przepisywać" cech partii
  imperatywnie z dodatku.
- Kopiowane są cechy o **tej samej nazwie definicji** zarejestrowane dla pozycji; definicja cechy
  musi istnieć przed użyciem (inaczej `poz["Nazwa"] = …` rzuci wyjątek — patrz W41).
- Cecha partii „materializuje się" dopiero po `Session.Save()` dokumentu przychodowego (to wtedy
  powstaje zasób/obrót). Wskazanie partii przy rozchodzie i kopiowanie cechy działa na **zapisanych**
  zasobach (Demo blokuje stan ujemny — rozchód wymaga wcześniejszego zapisanego przyjęcia).
- Kopiowanie nadrzędny→podrzędny w relacjach (`KopiujCechyDokumentu`/`KopiujCechyPozycji`) ustawia
  się na **definicji relacji**, nie na definicji dokumentu; faktyczne tworzenie podrzędnego rób przez
  `IRelacjeService` (sekcja relacji), a cechy dojdą same.
- Konfigurację definicji rób w sesji **konfiguracyjnej** (`config: true`) — to dane konfiguracyjne,
  nie operacyjne (`safe-code.md`).

---

### W41 — Odczyt i zapis cech dokumentu / pozycji (`Features`)

**Cel:** odczytać i ustawić wartości cech na dokumencie handlowym i jego pozycjach — zarówno
nietypowano (po nazwie definicji), jak i typowano (gettery `FeatureCollection`).

**Warianty:**

| Wariant | Dostęp | Zwraca / przyjmuje |
|---|---|---|
| Odczyt nietypowany | `dok["Nazwa"]`, `poz["Nazwa"]` | `object` (`null`, gdy brak wartości) |
| Odczyt typowany | `dok.Features.GetString/GetInt/GetDecimal/GetDate/GetBool/GetCurrency/GetDoubleCy/GetPercent/GetAmount(...)` | konkretny typ Soneta |
| Zapis (dowolny typ) | `dok["Nazwa"] = wartość` w transakcji | — |
| Sprawdzenie istnienia | `dok.Features.Exists("Nazwa")` | `bool` |
| Usunięcie wartości | `dok.Features.Remove("Nazwa")` w transakcji | — |
| Kopiowanie całego zestawu | `źródło.Features.CopyTo(cel.Features)` | — |
| Lista definicji | `dok.Features.Definitions` | `FeatureDefinitions` |

**Pola i typy:**
- `DokumentHandlowy.Features: Soneta.Business.FeatureCollection`,
  `PozycjaDokHandlowego.Features: Soneta.Business.FeatureCollection`.
- Indeksator nietypowany: `object this[string name]` na `Row` (`dok["Nazwa"]`) — równoważny
  `dok.Features["Nazwa"]`.
- Gettery typowane (wybór): `GetString`, `GetInt`, `GetBool`, `GetDecimal`, `GetDouble`, `GetDate`,
  `GetTime`, `GetFromTo`, `GetFraction`, `GetPercent`, `GetCurrency`, `GetDoubleCy`,
  `GetDictionaryItem`, `GetRow`, `GetHistory`, `GetArray`.
- Pomocnicze: `Exists(string)`, `Remove(string)`, `IsChanged`, `Definitions`.

**Snippet:**

```csharp
var handel = session.GetHandel();
var dok = handel.DokHandlowe.WgDaty[...];     // lub Get<DokumentHandlowy>(guid) w testach

// --- Odczyt nietypowany (object; null gdy brak wartości) ---
object centrum = dok["CentrumKosztow"];
if (centrum == null) { /* cecha bez wartości na tym dokumencie */ }

// --- Odczyt typowany przez Features ---
string opis    = dok.Features.GetString("OpisDodatkowy");
Date   dostawa = dok.Features.GetDate("DataDostawy");
bool   pilne   = dok.Features.GetBool("Pilne");

// pozycja:
PozycjaDokHandlowego poz = dok.Pozycje.Cast<PozycjaDokHandlowego>().First();
string nrSerii = poz.Features.GetString("NrSerii");

// --- Zapis cech: wymaga transakcji edycyjnej (jak każda modyfikacja Row) ---
using (var t = session.Logout(editMode: true))
{
    dok["OpisDodatkowy"] = "Pilna realizacja";   // String
    dok["Pilne"]         = true;                  // Bool
    dok["DataDostawy"]   = Date.Today.AddDays(3); // Date
    poz["NrSerii"]       = "S-2026-001";          // String na pozycji
    t.Commit();                                   // CommitUI() w workerze/extenderze
}
session.Save();

// Istnienie / usunięcie wartości:
bool ma = dok.Features.Exists("OpisDodatkowy");
using (var t = session.Logout(editMode: true))
{
    dok.Features.Remove("OpisDodatkowy");
    t.Commit();
}
session.Save();
```

**Pułapki:**
- Cecha musi mieć **wcześniej utworzoną definicję** (`FeatureDefinition`) zarejestrowaną dla
  właściwej tabeli (`DokHandlowe` dla dokumentu, `PozycjeDokHan` dla pozycji). Odwołanie do
  niezdefiniowanej cechy rzuca wyjątek — to nie to samo co pole natywne.
- Każdy **zapis** cechy to modyfikacja `Row` → musi być w transakcji (`session.Logout(true)` +
  `Commit`/`CommitUI`), potem `Save`. Odczyt transakcji nie wymaga.
- Indeksator nietypowany zwraca `object`; dla wartości pieniężnych/ilościowych zapisuj właściwy typ
  Soneta (`Currency`, `DoubleCy`, `Amount`, `Percent`, `Date`), nie surowy `decimal`/`double`/`string`.
- Cechy **algorytmiczne**: przypisanie wartości uruchamia algorytm definicji — efekty uboczne; część
  cech bywa read-only (`IsReadOnly(fd)` / tryb `SpecialEdit`) i edycja rzuci `AccessDeniedException`.
- W form.xml cechę adresuje się ścieżką `Features.Nazwa` (np. `{Features.NrSerii}`), także przez
  relację (`{Kontrahent.Features.Segment}`).
- `dok.Pozycje` to kolekcja pozycji dokumentu — iteruj po niej, nie ładuj całej tabeli
  `PozycjeDokHan`.

---

### W42 — Filtrowanie / wyszukiwanie dokumentów i partii po wartości cechy (serwerowo)

**Cel:** znaleźć dokumenty, pozycje, towary lub partie spełniające warunek na wartości cechy — z
filtrowaniem wykonywanym **po stronie SQL**, bez ładowania całej tabeli do pamięci.

**Warianty:**

| Wariant | Konstrukcja warunku | Uwaga |
|---|---|---|
| Równość wartości cechy | `new FieldCondition.Equal("Features.Nazwa", wartość)` | string-path, bo `Features.X` nie jest typowaną property |
| Większy / mniejszy | `FieldCondition.GreaterEqual / LessEqual("Features.Nazwa", v)` | dla cech liczbowych/dat |
| Łączenie warunków | `new RowCondition.And(...)` / `RowCondition.Or(...)` | składanie warunków serwerowych |
| Na indeksie tabeli | `tabela.WgKlucz[condition]` | filtr aplikowany na indeksie (SQL) |
| Na kolekcji `SubTable` | `dok.Pozycje[condition]` | filtr na pozycjach dokumentu |
| W widoku (UI) | `view.Condition &= new FieldCondition.Equal("Features.Nazwa", v)` | tylko kod UI / ViewInfo |

**Pola i typy:**
- `Soneta.Business.FieldCondition.Equal/GreaterEqual/LessEqual/...(string path, object value)` —
  ścieżka cechy to literał `"Features.NazwaDefinicji"`.
- `Soneta.Business.RowCondition.And` / `RowCondition.Or` — kompozycja warunków.
- Indeksy do filtrowania: `handel.DokHandlowe.WgDaty[condition]` (dokumenty),
  `towary.Towary.WgKodu[condition]` (towary), `magazyny.GrupyDostaw[...]` (partie).

**Snippet:**

```csharp
// 1) Towary po wartości cechy "Dystrybutor" = "Abc" (filtr serwerowy na indeksie)
var towary = session.GetTowary().Towary;
foreach (Towar t in towary.WgKodu[new FieldCondition.Equal("Features.Dystrybutor", "Abc")])
{
    // ... tylko towary o tej cesze; SQL filtruje po DataKey cechy
}

// 2) Dokumenty handlowe oznaczone cechą "Pilne" = true
var handel = session.GetHandel();
foreach (DokumentHandlowy d in
         handel.DokHandlowe.WgDaty[new FieldCondition.Equal("Features.Pilne", true)])
{
    // ...
}

// 3) Złożony warunek: cecha LUB cecha (OR) — wszystkie indeksowane serwerowo
var orWarunek = new RowCondition.Or(
    new FieldCondition.Equal("Features.Dystrybutor", "Abc"),
    new FieldCondition.Equal("Features.Dystrybutor", "Cba"));
var wybrane = towary.WgKodu[orWarunek].ToArray();

// 4) Filtr po cesze + zakres (np. cecha-data dostawy >= dziś) na dokumentach
var pilneNaDzis = new RowCondition.And(
    new FieldCondition.Equal("Features.Pilne", true),
    new FieldCondition.GreaterEqual("Features.DataDostawy", Date.Today));
foreach (DokumentHandlowy d in handel.DokHandlowe.WgDaty[pilneNaDzis]) { /* ... */ }

// 5) Pozycje konkretnego dokumentu po cesze (filtr na kolekcji SubTable)
foreach (PozycjaDokHandlowego p in
         dok.Pozycje[new FieldCondition.Equal("Features.NrSerii", "S-2026-001")])
{
    // ...
}
```

**Pułapki:**
- Cechy adresuj **string-pathem** `"Features.Nazwa"` w `FieldCondition` — `Features.X` nie jest
  typowaną property `Row`, więc nie da się jej użyć w wyrażeniu LINQ (`(Row r) => r.Features…`).
- Warunek aplikuj **na indeksie** (`WgKodu[...]`, `WgDaty[...]`) lub na kolekcji `SubTable`
  (`dok.Pozycje[...]`) — to wykonuje filtr w SQL. Nie iteruj całej tabeli z `if` w pamięci
  (`safe-code.md` §6).
- Wyszukiwanie korzysta z indeksowanego pola `DataKey` cechy; wartość w warunku podawaj w typie
  zgodnym z typem cechy (np. `bool` dla cechy Bool, `Date` dla cechy Date) — wartości są zapisane w
  ustalonym formacie tekstowym (patrz tabela typów w `references/features.md`).
- `view.Condition &= …` to mechanizm **UI** (ViewInfo/folder); w kodzie biznesowym używaj
  `SubTable[condition]`, nie obiektu `View`.
- `DokHandlowe` to tabela operacyjna guided — przy szerokich przekrojach dodatkowo zawężaj zakres
  czasowy (data dokumentu), nie tylko warunek na cesze.

---

## 8. VAT, wartości i waluty

Rozdział opisuje publiczny kontrakt dokumentu handlowego w zakresie tabeli VAT, podsumowań
wartości, ręcznej korekty VAT, sposobu liczenia VAT oraz zmiany waluty dokumentu i cen. Cały kod
jest zgodny z **C# 10** i operuje wyłącznie na **publicznych** typach i workerach platformy.

> **Wartości pieniężne** na pozycjach tabeli VAT i podsumowaniach mają dwie reprezentacje:
> `BruttoNetto` — kwoty w walucie systemowej jako `decimal` (`Netto`, `VAT`, `Brutto`); `BruttoNettoCy`
> — kwoty w walucie dokumentu jako `Currency` (`NettoCy`, `VATCy`, `BruttoCy`). Nie operuj na
> niezaokrąglonych `decimal` — platforma weryfikuje zaokrąglenie (safe-code §10).

---

### W43 — Odczytanie tabeli VAT (`SumyVAT`)

**Cel:** odczytać rozbicie wartości dokumentu na stawki VAT (netto / VAT / brutto wg stawki) — np.
do wydruku, eksportu lub kontroli sumy podatku.

**Warianty:**

| Wariant | Źródło | Uwaga |
|---|---|---|
| Tabela VAT dokumentu | `dok.SumyVAT` (`SubTable<SumaVAT>`) | po jednej pozycji na stawkę |
| Kwoty w walucie systemowej | `suma.Suma` (`BruttoNetto`) | `Netto`/`VAT`/`Brutto` jako `decimal` |
| Kwoty w walucie dokumentu | `suma.SumaCy` (`BruttoNettoCy`) | `NettoCy`/`VATCy`/`BruttoCy` jako `Currency` |
| Procent / opis stawki | `suma.Stawka`, `suma.DefinicjaStawki` | `StawkaVat.Procent: Percent` |
| Sumy z dokumentów nadrzędnych | `dok.NadrzędneSumyVAT` (`IList`) | scalone stawki nadrzędnych |

**Pola i typy:** `dok.SumyVAT: SubTable<SumaVAT>`. `SumaVAT` udostępnia: `DefinicjaStawki:
DefinicjaStawkiVat`, `Stawka: StawkaVat` (`Stawka.Procent: Percent`), `Suma: BruttoNetto`
(`Netto`, `VAT`, `Brutto` — `decimal`), `SumaCy: BruttoNettoCy` (`NettoCy`, `VATCy`, `BruttoCy` —
`Currency`), `Dokument: DokumentHandlowy`.

**Snippet:**

```csharp
var dok = session.GetHandel().DokHandlowe.WgDaty[...];   // lub po Guid

// Iteracja po tabeli VAT — jedna pozycja (SumaVAT) na każdą stawkę dokumentu:
foreach (SumaVAT s in dok.SumyVAT)
{
    Percent stawka = s.Stawka.Procent;       // np. 23%
    decimal netto  = s.Suma.Netto;           // kwota netto w walucie systemowej
    decimal vat    = s.Suma.VAT;             // kwota podatku VAT
    decimal brutto = s.Suma.Brutto;          // kwota brutto

    // Kwoty w walucie dokumentu (Currency = wartość + symbol waluty):
    Currency vatCy = s.SumaCy.VATCy;

    Console.WriteLine($"{stawka}: netto={netto} VAT={vat} brutto={brutto}");
}

// Łączna kwota VAT dokumentu z tabeli VAT:
decimal vatRazem = dok.SumyVAT.Sum(s => s.Suma.VAT);
```

**Pułapki:**
- `dok.SumyVAT` to `SubTable<SumaVAT>` — kolekcja serwerowa; iteruj po niej, nie materializuj do listy,
  jeśli wystarczy przebieg jednorazowy. Tabela VAT jest mała (kilka stawek), więc `.Sum(...)` jest
  akceptowalne.
- Rozróżniaj `Suma` (`BruttoNetto`, `decimal` w walucie systemowej) od `SumaCy` (`BruttoNettoCy`,
  `Currency` w walucie dokumentu). Dla dokumentu walutowego do prezentacji używaj `SumaCy`.
- `Stawka` to `StawkaVat` (typ stawki), `Procent` zwraca `Percent` — nie myl z `decimal`.
- Tabela VAT jest **wyliczana z pozycji** dokumentu (chyba że włączono `KorektaVAT` — patrz W45). Nie
  modyfikuj jej, gdy chcesz tylko odczytać wartości.

---

### W44 — Odczyt podsumowań wartości dokumentu

**Cel:** odczytać zsumowane wartości netto / VAT / brutto całego dokumentu oraz proponowany rabat —
bez ręcznego sumowania pozycji.

**Warianty:**

| Wariant | Pole | Typ | Uwaga |
|---|---|---|---|
| Podsumowanie dokumentu | `dok.Suma` | `BruttoNetto` | `Netto`/`VAT`/`Brutto` (`decimal`, waluta systemowa) |
| Wartość brutto w walucie | `dok.BruttoCy` | `Currency` | brutto w walucie dokumentu |
| Suma wyliczona z pozycji | `dok.SumaPozycji` | `BruttoNettoPozycji` | `Netto`/`VAT`/`Brutto` (read-only) |
| Suma pozycji tow./prod. | `dok.SumaPozycjiTowProd` | `BruttoNettoPozycji` | tylko towary i produkty |
| Proponowany rabat | `dok.Rabat` | `Percent` | przepisywany do pozycji |

**Pola i typy:** `dok.Suma: BruttoNetto` (podsumowana wartość dokumentu), `dok.BruttoCy: Currency`,
`dok.SumaPozycji: BruttoNettoPozycji` (`Netto`/`VAT`/`Brutto` — `decimal`, **tylko do odczytu**,
liczone na bieżąco z pozycji), `dok.Rabat: Percent`.

**Snippet:**

```csharp
var dok = session.GetHandel().DokHandlowe.WgDaty[...];

// Podsumowanie całego dokumentu (waluta systemowa):
decimal netto  = dok.Suma.Netto;
decimal vat    = dok.Suma.VAT;
decimal brutto = dok.Suma.Brutto;

// Brutto w walucie dokumentu (dla dokumentów walutowych):
Currency bruttoCy = dok.BruttoCy;

// Suma wyliczana z pozycji (przydatne do kontroli spójności z dok.Suma):
var sp = dok.SumaPozycji;
Console.WriteLine($"Pozycje: netto={sp.Netto} VAT={sp.VAT} brutto={sp.Brutto}");

// Proponowany rabat dokumentu (przepisywany do nowych pozycji):
Percent rabat = dok.Rabat;
```

**Pułapki:**
- `dok.Suma` to **stan zapisany** podsumowania, a `dok.SumaPozycji` jest **wyliczane na bieżąco**
  z pozycji za każdym odczytem. Dla dokumentu w buforze, przed ponownym przeliczeniem, mogą się
  chwilowo różnić.
- `SumaPozycji`/`SumaPozycjiTowProd` zwracają `BruttoNettoPozycji` — typ **tylko do odczytu** (brak
  setterów); nie próbuj przez nie modyfikować wartości.
- `dok.Rabat` to `Percent` — proponowany rabat dokumentu, przepisywany do nowo dodawanych pozycji;
  ustawienie nie przelicza wstecznie pozycji już istniejących.
- Wartości brutto/netto na poziomie dokumentu zależą od `LiczonaOd` (W46) i ewentualnej korekty
  tabeli VAT (`KorektaVAT`, W45).

---

### W45 — Ręczna korekta tabeli VAT (`KorektaVAT`)

**Cel:** ręcznie skorygować kwoty w tabeli VAT (gdy wyliczenie z pozycji nie odpowiada wartości
docelowej — np. zaokrąglenia faktury źródłowej), włączając flagę `KorektaVAT` i edytując wiersze
`SumyVAT`.

**Warianty:**

| Wariant | Operacja |
|---|---|
| Włączenie trybu korekty | `dok.KorektaVAT = true` |
| Ręczna zmiana kwoty stawki | edycja `suma.Suma.Netto` / `.VAT` / `.Brutto` na wierszu `SumaVAT` |
| Dostępność korekty | `dok.IsReadOnlyKorektaVAT()`, `dok.IsReadOnlySumyVAT()` (sterowanie UI) |
| Powrót do automatu | `dok.KorektaVAT = false` (tabela liczona ponownie z pozycji) |

**Pola i typy:** `dok.KorektaVAT: bool` (czy sumy VAT zmieniono ręcznie i nie zależą od pozycji),
`SumaVAT.Suma: BruttoNetto` (`Netto`/`VAT`/`Brutto` — `decimal`). Wiersze tabeli VAT są edytowalne
**tylko gdy** `KorektaVAT == true` (`SumaVAT.IsReadOnly()` zwraca `true` przy wyłączonej fladze).

> **Worker `KorektaTabeliVATWorker` jest `internal`** — nie da się go zainstancjonować z dodatku
> zewnętrznego. Publiczny tor korekty prowadzi przez flagę `dok.KorektaVAT` i bezpośrednią edycję
> pól wierszy `dok.SumyVAT`.

**Snippet:**

```csharp
var dok = session.GetHandel().DokHandlowe.WgDaty[...];

using (var t = session.Logout(editMode: true))    // CommitUI() w workerze/extenderze
{
    // 1. Włącz ręczną korektę — odblokowuje edycję wierszy tabeli VAT:
    dok.KorektaVAT = true;

    // 2. Skoryguj kwoty na wybranej stawce (np. wyrównanie groszowe na 23%):
    foreach (SumaVAT s in dok.SumyVAT)
    {
        if (s.Stawka.Procent == new Percent(0.23))
        {
            s.Suma.VAT    = 230.01m;       // wartości MUSZĄ być zaokrąglone do grosza
            s.Suma.Brutto = 1230.01m;
        }
    }
    t.Commit();
}
session.Save();
```

**Pułapki:**
- Edycja wierszy `SumyVAT` bez `dok.KorektaVAT = true` zostanie zablokowana — `SumaVAT` jest wtedy
  read-only (sumy zależą od pozycji).
- Przypisywane kwoty muszą być **zaokrąglone do grosza** — w trybie DEBUG ustawienie
  niezaokrąglonej wartości `Netto`/`VAT`/`Brutto` rzuca `ArgumentException`. Zaokrąglaj wejście
  (`Soneta.Tools.Math.RoundCy(...)`).
- `KorektaVAT` jest dostępna tylko, gdy definicja dokumentu na to pozwala
  (`Definicja.SumyVAT` w trybie korekty) — sprawdzaj `dok.IsReadOnlyKorektaVAT()` zanim ustawisz
  flagę z poziomu UI.
- Po włączeniu korekty tabela VAT **przestaje** śledzić zmiany pozycji. Wyłączenie
  (`KorektaVAT = false`) przywraca wyliczanie z pozycji i nadpisuje ręczne kwoty.
- `DefinicjaStawki` na wierszu `SumaVAT` można zmieniać tylko przy włączonej korekcie
  (`IsReadOnlyDefinicjaStawki()` zależy od `KorektaVAT`).

---

### W46 — Sposób liczenia VAT (`LiczonaOd`) i przeliczenie procedur VAT

**Cel:** ustawić, czy dokument jest liczony od netto czy od brutto (`LiczonaOd`), oraz przeliczyć
procedury VAT (JPK) na dokumencie zatwierdzonym/zaksięgowanym przy użyciu publicznego workera.

**Warianty:**

| Wariant | Mechanizm |
|---|---|
| Liczenie od netto | `dok.LiczonaOd = SposobLiczeniaVAT.OdNetto` |
| Liczenie od brutto | `dok.LiczonaOd = SposobLiczeniaVAT.OdBrutto` |
| Od brutto minus netto | `dok.LiczonaOd = SposobLiczeniaVAT.OdBruttoMinusNetto` |
| Wg ustawień kontrahenta | `dok.LiczonaOd = SposobLiczeniaVAT.ZależyOdKontrahenta` |
| Przeliczenie procedur VAT | worker `PrzeliczProceduryVATWorker` (publiczny) |

**Pola i typy:** `dok.LiczonaOd: SposobLiczeniaVAT` — enum `Soneta.Handel.SposobLiczeniaVAT`:
`OdNetto=1`, `OdBrutto=2`, `OdBruttoMinusNetto=3`, `ZależyOdKontrahenta=4` (wartość `0` jest
niedozwolona — rzuca `RequiredException`). Worker `PrzeliczProceduryVATWorker` ma publiczną klasę
parametrów `PrzeliczProceduryVATParams : ContextBase` (`Zatwierdzone: bool = true`,
`Zaksiegowane: bool = false`) oraz właściwości `[Context]`: `Dokument: DokumentHandlowy`,
`Params: PrzeliczProceduryVATParams`.

**Snippet:**

```csharp
var dok = session.GetHandel().DokHandlowe.WgDaty[...];

// 1. Zmiana sposobu liczenia VAT (dokument w buforze):
using (var t = session.Logout(editMode: true))
{
    dok.LiczonaOd = SposobLiczeniaVAT.OdBrutto;   // 0 jest niedozwolone
    t.Commit();
}
session.Save();

// 2. Przeliczenie procedur VAT (JPK) workerem publicznym.
//    Worker działa tylko dla dokumentu zatwierdzonego (Params.Zatwierdzone)
//    lub zablokowanego/zaksięgowanego (Params.Zaksiegowane):
var p = new PrzeliczProceduryVATWorker.PrzeliczProceduryVATParams(context)
{
    Zatwierdzone = true,
    Zaksiegowane = false,
};
var worker = new PrzeliczProceduryVATWorker
{
    Dokument = dok,
    Params   = p,
};
worker.PrzeliczProceduryVAT();    // sam otwiera transakcję i Commit
session.Save();
```

**Pułapki:**
- `LiczonaOd` nie przyjmuje wartości `0` (`RequiredException`). Zawsze ustaw konkretny wariant enuma.
- Zmiana `LiczonaOd` na dokumencie z pozycjami wpływa na sposób przeliczenia netto↔brutto pozycji
  i tabeli VAT — rób to przed wprowadzeniem cen lub świadomie po przeliczeniu.
- `PrzeliczProceduryVATWorker.PrzeliczProceduryVAT()` **nic nie zrobi**, jeśli dokument jest w
  buforze albo stan nie pasuje do flag `Params` (`Zatwierdzone`/`Zaksiegowane`). Worker sam otwiera
  transakcję (`Logout(true)` + `Commit`) — nie owijaj go w dodatkową transakcję edycyjną.
- Worker jest widoczny tylko, gdy definicja liczy sumy VAT i ma definicję ewidencji
  (`IsVisiblePrzeliczProceduryVAT`); z poziomu kodu i tak sprawdź stan dokumentu przed wywołaniem.
- `PrzeliczProceduryVATParams` dziedziczy po `ContextBase` — przy ręcznym tworzeniu przekaż `Context`
  do konstruktora.

---

### W47 — Zmiana waluty dokumentu i cen

**Cel:** zmienić walutę dokumentu handlowego (i opcjonalnie przeliczyć ceny pozycji) — np. wystawić
fakturę w EUR zamiast PLN, z kursem z wybranej tabeli kursowej.

**Warianty:**

| Wariant | Mechanizm |
|---|---|
| Zmiana waluty z przeliczeniem cen | parametry `DokumentHandlowyZmianaWalutyWorkerParams` + akcja „Zmień walutę dokumentu i cen..." |
| Zmiana waluty bez cen | te same parametry z `ZmienCeny = false` |
| Ręczne ustawienie waluty/kursu | `dok.TabelaKursowa`, `dok.KursWaluty`, `dok.DataOgłoszeniaKursu`, `dok.BruttoCy` |

**Pola i typy:** klasa parametrów (publiczna) `DokumentHandlowyZmianaWalutyWorkerParams :
PozycjaDokHandlowegoZmianaWalutyCenyWorkerParams` (ctor `(Context, [Context] DokumentHandlowy)`)
udostępnia: `Waluta: Waluta` („na walutę"), `WalutaBazowa: Waluta` (read-only, „z waluty"),
`TabelaKursowa: TabelaKursowa`, `Data: Date`, `KursWaluty: double`, `ZmienCeny: bool`. Pola
dokumentu: `dok.TabelaKursowa: TabelaKursowa`, `dok.KursWaluty: double`, `dok.BruttoCy: Currency`.
Moduł walut (jest `internal` jako extension): `Soneta.Waluty.WalutyModule.GetInstance(session)` →
`.Waluty.WgSymbolu["EUR"]`, `.TabeleKursowe`.

> **Worker `DokumentHandlowyZmianaWalutyWorker` jest `internal`** — nie da się go zainstancjonować
> bezpośrednio z dodatku zewnętrznego. Jest jednak zarejestrowany jako akcja menu Czynności („Zmień
> walutę dokumentu i cen...", `Shift+F11`) i przyjmuje publiczne parametry
> `DokumentHandlowyZmianaWalutyWorkerParams`. Z poziomu kodu dodatku zewnętrznego dostępne tory to:
> (1) uruchomienie akcji przez mechanizm Czynności z przygotowanym `Context`, albo (2) bezpośrednie
> ustawienie pól waluty/kursu na dokumencie i pozycjach.

**Snippet:**

```csharp
using Microsoft.Extensions.DependencyInjection;   // jeśli korzystasz z serwisów
using Soneta.Waluty;

var dok = session.GetHandel().DokHandlowe.WgDaty[...];

// --- Tor 1: przygotowanie parametrów workera (do uruchomienia przez akcję Czynności) ---
// Worker jest internal — z dodatku przygotowujemy publiczne Params i uruchamiamy akcję
// przez mechanizm menu Czynności (Context z zaznaczonym dokumentem).
var wm = WalutyModule.GetInstance(session);
var p = new DokumentHandlowyZmianaWalutyWorkerParams(context, dok)
{
    Waluta        = wm.Waluty.WgSymbolu["EUR"],   // waluta docelowa
    TabelaKursowa = wm.TabeleKursowe.NBP,
    Data          = Date.Today,
    ZmienCeny     = true,                          // przelicz też ceny pozycji
};
// KursWaluty wylicza się automatycznie po ustawieniu Waluta/TabelaKursowa/Data;
// w razie potrzeby można nadpisać: p.KursWaluty = 4.30;

// --- Tor 2: ręczne ustawienie waluty i kursu na dokumencie (bez workera) ---
using (var t = session.Logout(editMode: true))
{
    dok.TabelaKursowa = wm.TabeleKursowe.NBP;
    dok.KursWaluty    = 4.30;
    // dok.BruttoCy = new Currency(..., "EUR");   // kwoty w walucie dokumentu
    t.Commit();
}
session.Save();
```

**Pułapki:**
- Worker `DokumentHandlowyZmianaWalutyWorker` jest `internal` — **nie** wywołasz `new ...Worker(...)`
  ani `.ZmienWalute()` z dodatku zewnętrznego. Używaj publicznych `Params` + akcji Czynności lub
  bezpośredniej edycji pól dokumentu.
- `session.GetWaluty()` jest **internal** — moduł walut pobieraj przez
  `WalutyModule.GetInstance(session)` (namespace `Soneta.Waluty`).
- Jeśli w bazie **brak kursu** na żądaną datę (np. Demo nie ma kursu EUR „na dziś"), platforma rzuci
  `KursWalutyNotFoundException`. `KursWaluty` w parametrach wylicza się automatycznie tylko, gdy kurs
  istnieje; w przeciwnym razie ustaw `KursWaluty` ręcznie.
- Zmiana waluty ma sens tylko dla dokumentu w **buforze** (`IsVisibleZmienWalute` wymaga
  `dok.Bufor`); dla dokumentu zatwierdzonego operacja jest niedostępna.
- `WalutaBazowa` jest read-only — wyznaczana z bieżącej waluty dokumentu (`dok.BruttoCy.Symbol`).
  Ustawiasz tylko `Waluta` (docelową).
- Kwoty pieniężne to `Currency` (wartość + symbol), nie `decimal`/`double`. Sam `KursWaluty` jest
  `double`.

---

---

## 9. Korekty i dokumenty specjalne

Rozdział obejmuje korekty (ilościowe, ceny, wartości przyjęcia) oraz dokumenty „specjalne": inwentaryzację (INW), fakturę zaliczkową wraz z jej rozliczeniem oraz przesunięcie międzymagazynowe (MM). Wszystkie wzorce operują **wyłącznie na publicznym kontrakcie** platformy. Kluczowym narzędziem jest serwis relacji `IRelacjeService` (namespace `Soneta.Handel.RelacjeDokumentow.Api`), opisany w rozdziale o relacjach — tutaj koncentrujemy się na metodzie `NowaKorekta` oraz na specyfice każdego typu dokumentu.

> **Wspólne reguły** (powtórzone z fundamentów, [`safe-code.md`](../safe-code.md)):
> - Dostęp do serwisu: `var rel = session.GetRequiredService<IRelacjeService>();` (wymaga `using Microsoft.Extensions.DependencyInjection;`).
> - Dokument **nadrzędny / korygowany musi być zatwierdzony** (`StanDokumentuHandlowego.Zatwierdzony`) przed wywołaniem relacji.
> - Każda modyfikacja w transakcji (`session.Logout(editMode: true)` + `Commit()` / `CommitUI()` w workerze), potem `session.Save()`. Magazyn księguje się dopiero po `Save()`.
> - Pola `DokumentKorygowany`, `DokumentyKorygujące`, `DokumentyZaliczkowe` są **kalkulowane (read-only)** — nie ustawiaj ich ręcznie; powstają jako efekt utworzenia relacji.

---

### W48 — Korekta ilościowa i korekta ceny

**Cel:** utworzyć dokument korygujący do zatwierdzonej faktury / dokumentu magazynowego (zmiana ilości, ceny, rabatu lub VAT) i zapisać poprawione wartości na pozycjach korekty.

**Warianty:**

| Wariant | Wywołanie | Uwaga |
|---|---|---|
| Korekta pojedynczego dokumentu | `NowaKorekta(new[]{ dok }, symbolKorekty)` | zwraca tablicę korekt (zwykle 1 element) |
| Korekta zbiorcza (wiele dok. → jedna) | `NowaKorektaZbiorcza(korygowane, symbolKorekty)` | grupuje korygowane dokumenty |
| Domyślny symbol korekty | `NowaKorekta(new[]{ dok })` (bez symbolu) | platforma dobiera definicję korekty wg definicji korygowanego |
| Korekta ilościowa | po utworzeniu: zmiana `poz.Ilosc` na pozycji korekty | różnica ilości |
| Korekta ceny / rabatu | zmiana `poz.Cena` / `poz.Rabat` | różnica wartości |
| Korekta „do zera" (zwrot całości) | ustaw `poz.Ilosc = Quantity.Zero` (w jednostce pozycji) | pełny storno |

**Pola i typy:**
- `IRelacjeService.NowaKorekta(DokumentHandlowy[] korygowane, string symbolKorekty = null, Context context = null, HandlerSet handlers = null): DokumentHandlowy[]`.
- `IRelacjeService.NowaKorektaZbiorcza(DokumentHandlowy[] korygowane, string symbolKorekty = null, …): DokumentHandlowy[]`.
- Na pozycji korekty: `PozycjaDokHandlowego.Ilosc: Quantity`, `Cena: DoubleCy`, `Rabat: Percent`, `PozycjaKorygowana` (powiązanie z pozycją oryginału, read-only).
- Odczyt powiązań: `dok.DokumentyKorygujące` (kolekcja korekt), `korekta.DokumentKorygowany` (oryginał).

**Snippet:**

```csharp
using Microsoft.Extensions.DependencyInjection;
using Soneta.Handel;
using Soneta.Handel.RelacjeDokumentow.Api;
using Soneta.Types;

// 1. Oryginał musi być zatwierdzony:
using (var t = session.Logout(editMode: true)) {
    faktura.Stan = StanDokumentuHandlowego.Zatwierdzony;
    t.Commit();
}
session.Save();

// 2. Utworzenie korekty przez serwis relacji:
var rel = session.GetRequiredService<IRelacjeService>();

DokumentHandlowy korekta;
using (var t = session.Logout(editMode: true)) {
    korekta = rel.NowaKorekta(new[] { faktura }, "KWN")[0];   // symbol definicji korekty

    // 3. Korekta ilościowa: zmiana ilości na pozycji korekty
    //    (pozycje korekty są wstępnie zainicjowane wartościami oryginału)
    var poz = korekta.Pozycje.First();
    poz.Ilosc = new Quantity(8, poz.Ilosc.Symbol);   // było 10 -> korygujemy do 8

    // 4. Korekta ceny / rabatu — alternatywnie:
    // poz.Cena  = new DoubleCy(4.5m, poz.Cena.Symbol);
    // poz.Rabat = new Percent(0.15);

    t.Commit();
}
session.Save();

// Odczyt powiązania:
DokumentHandlowy oryginal = korekta.DokumentKorygowany;
```

**Pułapki:**
- `NowaKorekta` zwraca **tablicę** `DokumentHandlowy[]` — dla jednego dokumentu bierz `[0]` / `.Single()`.
- Korygowany dokument musi być **zatwierdzony**; korekta do dokumentu w buforze nie powstanie.
- Pozycje korekty są inicjowane wartościami oryginału — modyfikujesz je „do wartości docelowej", a system sam policzy różnicę. Nie wpisuj różnicy „z palca".
- `symbolKorekty` to symbol **definicji korekty** (np. „KWN", „KS"), a nie symbol korygowanej faktury. Definicja korekty musi istnieć i być odblokowana.
- Całą sekwencję (utworzenie + edycja pozycji) wykonuj w **jednej transakcji**, dopiero potem `Save()`.
- Symbol jednostki na `Ilosc` musi pochodzić z istniejącej pozycji (`poz.Ilosc.Symbol`) — nie twórz `Quantity` z gołą liczbą.

---

### W49 — Korekta wartości przyjęcia magazynowego

**Cel:** skorygować ilość/wartość przyjęcia magazynowego (PZ/PW) tak, aby poprawić zaksięgowane obroty i partie dostaw.

**Warianty:**

| Wariant | Mechanizm publiczny |
|---|---|
| Korekta przyjęcia ilościowa | `IRelacjeService.NowaKorekta(new[]{ przyjecie }, …)` + korekta `Ilosc` na pozycji |
| Korekta wartości (ceny) przyjęcia | jw., zmiana `Cena` na pozycji korekty |
| Korekta wskazanej dostawy / partii | korekta z odwołaniem do partii — `Soneta.Magazyny.GrupaDostaw` |

**Pola i typy:** te same co W48 — `IRelacjeService.NowaKorekta(...)`, `PozycjaDokHandlowego.Ilosc/Cena`, `PozycjaKorygowana`.

**Snippet:**

```csharp
var rel = session.GetRequiredService<IRelacjeService>();

DokumentHandlowy korektaPrzyjecia;
using (var t = session.Logout(editMode: true)) {
    // przyjecie = zatwierdzony dokument PZ/PW
    korektaPrzyjecia = rel.NowaKorekta(new[] { przyjecie })[0];

    var poz = korektaPrzyjecia.Pozycje.First();
    poz.Ilosc = new Quantity(9, poz.Ilosc.Symbol);   // przyjęto 10, korygujemy stan do 9

    t.Commit();
}
session.Save();   // tu księgują się skorygowane obroty/partie
```

**Pułapki:**
- **Dedykowany worker `UtworzKorektePrzyjeciaWorker` jest `internal`** — nie da się go zainstancjonować z dodatku zewnętrznego. Publiczny tor to **`IRelacjeService.NowaKorekta`** (wewnętrznie worker robi dokładnie to samo: `NowaKorekta` + dostosowanie `Pozycje[].Ilosc` z uwzględnieniem obrotów/storn).
- Korekta przyjęcia działa na zaksięgowanych obrotach i partiach — różnicowe wyliczenia ilości względem obrotów (`MagazynyModule.Obroty`) i storn wykonuje platforma. Z poziomu publicznego kontraktu ustaw docelową `Ilosc`/`Cena` na pozycji korekty.
- Magazyn (zasoby/obroty) aktualizuje się dopiero po `session.Save()`, nie po `Commit()`.
- Jeśli przyjęcie wskazywało partię/dostawę, korekta musi odnosić się do tej samej dostawy — przy złożonych scenariuszach (rozchody z tej partii, przesunięcia) korektę realizuj na pełnej, zalogowanej sesji aplikacyjnej.

---

### W50 — Dokument inwentaryzacji (INW)

**Cel:** utworzyć dokument spisu z natury (INW), na którym wprowadza się stany rzeczywiste; system wylicza różnice (nadwyżka / strata) względem stanu ewidencyjnego i generuje dokumenty korygujące stan.

**Warianty:**

| Wariant | Charakterystyka |
|---|---|
| Spis z natury | pozycje = stan rzeczywisty zliczony fizycznie |
| Stan początkowy / bilans otwarcia | INW jako dokument ustalający stany na start |
| Nadwyżka | stan rzeczywisty > ewidencyjny → relacja `InwentaryzacjaNadwyżka` |
| Strata / niedobór | stan rzeczywisty < ewidencyjny → relacja `InwentaryzacjaStrata` |
| Inwentaryzacja wg partii / wskazania dostawy | spis z dokładnością do partii (`GrupaDostaw`) |

**Pola i typy:**
- Definicja: `session.GetHandel().DefDokHandlowych.WgSymbolu["INW"]`.
- `DokumentHandlowy.Magazyn` (`Soneta.Magazyny.Magazyn`) — inwentaryzowany magazyn (wymagany).
- `PozycjaDokHandlowego.Ilosc: Quantity` — stan rzeczywisty.
- Dokumenty różnic (odczyt): `dok.Podrzędne[...]` / relacje inwentaryzacyjne; różnica wartości dostępna na dokumencie różnicy (np. `Ewidencja.Wartosc`).

**Snippet:**

```csharp
var hm = session.GetHandel();
var magazyny = session.GetMagazyny();
var towary = session.GetTowary();

DokumentHandlowy inw;
using (var t = session.Logout(editMode: true)) {
    inw = new DokumentHandlowy();
    session.AddRow(inw);
    inw.Definicja = hm.DefDokHandlowych.WgSymbolu["INW"];   // definicja PIERWSZA
    inw.Magazyn = magazyny.Magazyny.WgSymbol["F"];          // inwentaryzowany magazyn

    // Pozycja = stan rzeczywisty zliczony fizycznie:
    var poz = new PozycjaDokHandlowego(inw);
    session.AddRow(poz);
    poz.Towar = towary.Towary.WgKodu["BIKINI"];             // Towar PIERWSZY (inicjuje jednostkę)
    poz.Ilosc = new Quantity(9, poz.Ilosc.Symbol);          // ewidencyjnie 10 -> spis 9

    inw.Stan = StanDokumentuHandlowego.Zatwierdzony;        // zatwierdzenie wylicza różnice
    t.Commit();
}
session.Save();   // tu powstają dokumenty różnic i korekta stanu
```

**Pułapki:**
- INW wymaga **wskazanego magazynu**; bez niego nie da się policzyć różnic.
- Różnice (nadwyżka/strata) i ich zaksięgowanie powstają przy **zatwierdzeniu + Save**, nie wcześniej. Dokumenty różnic to obiekty podrzędne — czytaj je przez kolekcje relacji, nie twórz ręcznie.
- Inwentaryzacja wg partii wymaga wskazania dostawy/partii (`Soneta.Magazyny.GrupaDostaw`) — bez tego spis odnosi się do stanu zbiorczego.
- W bazie Demo obowiązuje blokada stanu ujemnego (`StanUjemnyVerifier`) — żeby spis miał sens, towar musi mieć wcześniejsze, **zapisane** przyjęcie (PW/PZ).
- Nie modyfikuj wartości na dokumentach różnic ręcznie — to wynik wyliczeń platformy.

---

### W51 — Faktura zaliczkowa i jej rozliczenie dokumentem końcowym

**Cel:** wystawić fakturę zaliczkową (FZAL) na poczet przyszłej dostawy, a następnie rozliczyć ją dokumentem końcowym (FV), tak by wartość końcowej została pomniejszona o wpłaconą zaliczkę.

**Warianty:**

| Wariant | Mechanizm |
|---|---|
| Utworzenie zaliczkowej z zamówienia | `NowyPodrzednyIndywidualny(new[]{ zamowienie }, "FZAL")` |
| Rozliczenie zaliczki na dokumencie końcowym | `NowyPodrzednyIndywidualny(new[]{ zaliczkowa }, "FV", handlers: …)` |
| Przenoszenie zaliczki **na pozycje** | callback `WybierzDokumentyZaliczkoweCallback` + `DokumentHandlowyRealizacjaZaliczkiWorker` |
| Przenoszenie zaliczki **wg stawki VAT** | callback `WybierzZaliczkiWgStawkiVatCallback` |
| Wiele zaliczek do jednej końcowej | dodaj wszystkie w callbacku (`Wybrany = true` dla każdej) |

**Pola i typy:**
- `IRelacjeService.NowyPodrzednyIndywidualny(DokumentHandlowy[] nadrzedne, string symbolPodrzednego, Context context = null, HandlerSet handlers = null): DokumentHandlowy[]`.
- `HandlerSet.WybierzDokumentyZaliczkoweCallback: Action<DokumentDocelowy>` — wskazanie zaliczek (tor „na pozycje").
- `HandlerSet.WybierzZaliczkiWgStawkiVatCallback: Action<DokumentDocelowy>` — tor „wg stawki VAT".
- Worker publiczny do wskazania zaliczki: `DokumentHandlowyRealizacjaZaliczkiWorker` z property `[Context] Dokument: DokumentHandlowy`, `[Context] Docelowy: DokumentDocelowy`, `Wybrany: bool`.
- Odczyt: `dok.DokumentyZaliczkowe` (kalkulowane) — zaliczki powiązane z końcowym; `dok.SumyVAT: SubTable<SumaVAT>`; `dok.BruttoCy`.

**Snippet:**

```csharp
using Microsoft.Extensions.DependencyInjection;
using Soneta.Handel;
using Soneta.Handel.RelacjeDokumentow.Api;

var rel = session.GetRequiredService<IRelacjeService>();

// zaliczkowa = zatwierdzona faktura zaliczkowa (FZAL).
// Rozliczamy ją dokumentem końcowym FV — callback wskazuje, które zaliczki przenieść:
DokumentHandlowy[] koncowy;
using (var t = session.Logout(editMode: true)) {
    koncowy = rel.NowyPodrzednyIndywidualny(
        new[] { zaliczkowa },
        "FV",
        handlers: new HandlerSet {
            WybierzDokumentyZaliczkoweCallback = WybierzZaliczki
        });
    t.Commit();
}
session.Save();

// koncowy[0].BruttoCy == 0, jeśli zaliczka pokryła całość

// Callback: zaznacza wszystkie dokumenty zaliczkowe powiązane z dokumentem docelowym.
static void WybierzZaliczki(DokumentDocelowy target) {
    var w = new DokumentHandlowyRealizacjaZaliczkiWorker { Docelowy = target };
    foreach (var d in target.DokumentyZaliczkowe.Cast<DokumentHandlowy>()) {
        w.Dokument = d;
        w.Wybrany = true;     // przenosi zaliczkę na dokument końcowy
    }
}
```

**Pułapki:**
- Bez dostarczenia odpowiedniego callbacka (`WybierzDokumentyZaliczkoweCallback` / `WybierzZaliczkiWgStawkiVatCallback`) domyślne handlery rzucają `NotImplementedException` — **musisz** wskazać tryb przenoszenia zaliczki zgodny z konfiguracją definicji końcowej (`SposobPrzenoszeniaZaliczki`: `NaPozycje` vs `NaDokument`).
- Tryb przenoszenia (na pozycje / wg stawki VAT) jest **cechą definicji** dokumentu końcowego — użyj callbacka pasującego do konfiguracji, inaczej rozliczenie nie zadziała.
- Worker rozliczenia (`RealizacjaZaliczkiWorker`, edytor kwot wg stawki) jest `internal` — z dodatku używaj publicznego `DokumentHandlowyRealizacjaZaliczkiWorker` (wskazanie dokumentów) wewnątrz callbacka.
- Faktura zaliczkowa musi być **zatwierdzona** przed rozliczeniem; `DokumentyZaliczkowe` to pole **kalkulowane** — nie ustawiasz go, czytasz.
- Tabela VAT dokumentu zaliczkowego jest przeliczana proporcjonalnie do wpłaconej zaliczki (logika `DokumentZaliczkowyWorker`) — nie modyfikuj `SumyVAT` ręcznie.

---

### W52 — Przesunięcie międzymagazynowe (MM)

**Cel:** przesunąć zasób z jednego magazynu do drugiego dokumentem MM — rozchód z magazynu źródłowego i przychód do magazynu docelowego w jednej operacji.

**Warianty:**

| Wariant | Mechanizm |
|---|---|
| Przesunięcie w obrębie firmy | MM z `MagazynZ` (źródło) i `MagazynDo` (cel) |
| Wskazanie partii / dostawy przy rozchodzie | pozycja z odwołaniem do `GrupaDostaw` |
| Korekta przesunięcia | `IRelacjeService.NowaKorekta(new[]{ mm }, …)` |

**Pola i typy:**
- Definicja: `session.GetHandel().DefDokHandlowych.WgSymbolu["MM"]`.
- `DokumentHandlowy.MagazynZ: Soneta.Magazyny.Magazyn` — magazyn źródłowy (rozchód).
- `DokumentHandlowy.MagazynDo: Soneta.Magazyny.Magazyn` — magazyn docelowy (**kalkulowane**: ustawia magazyn na podrzędnym dokumencie przesunięcia `Podrzędne[TypRelacjiHandlowej.PrzesunięcieDo]`; wymaga, by dokument przesunięcia już istniał — ustawiaj po `Definicja`).
- `PozycjaDokHandlowego.Towar`, `Ilosc: Quantity`.

**Snippet:**

```csharp
var hm = session.GetHandel();
var magazyny = session.GetMagazyny();
var towary = session.GetTowary();

DokumentHandlowy mm;
using (var t = session.Logout(editMode: true)) {
    mm = new DokumentHandlowy();
    session.AddRow(mm);
    mm.Definicja = hm.DefDokHandlowych.WgSymbolu["MM"];     // definicja PIERWSZA

    mm.MagazynZ  = magazyny.Magazyny.WgSymbol["F"];          // magazyn źródłowy
    mm.MagazynDo = magazyny.Magazyny.WgNazwa["Magazyn 2"];   // magazyn docelowy (po ustawieniu definicji)

    var poz = new PozycjaDokHandlowego(mm);
    session.AddRow(poz);
    poz.Towar = towary.Towary.WgKodu["BIKINI"];             // Towar PIERWSZY
    poz.Ilosc = new Quantity(5, poz.Ilosc.Symbol);

    mm.Stan = StanDokumentuHandlowego.Zatwierdzony;
    t.Commit();
}
session.Save();   // tu księguje się rozchód ze źródła i przychód do celu
```

**Pułapki:**
- `MagazynDo` jest **polem kalkulowanym** delegującym do podrzędnego dokumentu przesunięcia — ustaw je **po** `Definicja` (a najlepiej przed dodaniem pozycji), bo `IsReadOnlyMagazynDo()` blokuje zmianę magazynu, gdy istnieją już pozycje.
- `MagazynZ` i `MagazynDo` **muszą być różne** i oba dostępne (prawa do magazynów / przypisanie definicji do magazynu wg konfiguracji `Ogólne.PrzypisanieDefinicjiDoMagazynu`).
- Rozchód MM podlega blokadzie stanu ujemnego (Demo: `StanUjemnyVerifier`) — magazyn źródłowy musi mieć **zapisany** zasób przesuwanego towaru.
- Obroty (rozchód + przychód) księgują się po `session.Save()`, nie po `Commit()`.
- Korektę przesunięcia wykonuj przez `IRelacjeService.NowaKorekta` (jak w W48/W49); ręczna korekta partii przy MM jest złożona i wymaga pełnej sesji aplikacyjnej.

---

## 10. Operacje zbiorcze (batch)

Operacje na zbiorze dokumentów (ewidencjonowanie do księgowości, hurtowe zatwierdzanie,
generowanie dokumentów podrzędnych) wykonujemy efektywnie i bezpiecznie: filtr **serwerowy**
zamiast pełnego skanu tabeli, **krótkie transakcje** (paczki), świadoma obsługa **blokady
optymistycznej** w `Save()`. Tabela `DokHandlowe` jest operacyjna (guided) — pełny skan bez
zakresu czasowego jest zabroniony (`safe-code.md` §6.3). Duże pętle dziel na paczki, by nie
trzymać długiej transakcji edycyjnej (§13.1).

### W53 — Ewidencjonowanie / eksport do księgowości wielu dokumentów

**Cel:** zbiorczo zaewidencjonować (zaksięgować do ewidencji księgowej) wiele dokumentów
handlowych z danego okresu — np. raport fiskalny zbiorczy z paragonów lub korekt paragonów.
Realizuje to publiczny worker `EwidencjonowanieZbiorczeWorker`, który sam grupuje dokumenty
(po drukarce / oddziale / rodzaju podmiotu) i tworzy zbiorcze dokumenty ewidencji `DokEwidencji`.

**Warianty:**

| Wariant | Ustawienie `Params` |
|---|---|
| Raport fiskalny z paragonów | `RaportDla = RaportDla.Paragonów` |
| Raport dla korekt paragonów | `RaportDla = RaportDla.KorektParagonów` |
| Zawężenie do jednej drukarki | `SymbolKasy = "D1"` (puste = wszystkie z niepustym symbolem kasy) |
| Wskazanie definicji ewidencji | `Definicja` (typ `SprzedażZbiorczaEwidencja`) — gdy chcemy inną niż domyślna |
| Filtr po dacie wystawienia | `ZaOkres: FromTo` |
| Filtr po dacie dostawy / zaliczki | `OkresDostawyZaliczki: FromTo` |
| Wielooddziałowość | `Oddzial: OddzialFirmy` (gdy włączona w konfiguracji) |

**Pola i typy:**
- Worker: `Soneta.Handel.EwidencjonowanieZbiorczeWorker` (**public**), metoda publiczna
  `void Ewidencjonuj()`, property `[Context] Params Param`.
- `EwidencjonowanieZbiorczeWorker.Params(Context cx)` — konstruktor z `Context`. Pola:
  `ZaOkres: FromTo`, `OkresDostawyZaliczki: FromTo`, `RaportDla: RaportDla`,
  `SymbolKasy: string`, `Definicja: Soneta.Core.DefinicjaDokumentu`, `Oddzial: OddzialFirmy`.
- `EwidencjonowanieZbiorczeWorker.RaportDla` (enum): `Paragonów`, `KorektParagonów`.
- Worker przetwarza tylko dokumenty w stanie `Zatwierdzony` / `Zablokowany`; pomija już
  zaewidencjonowane (`EwidencjaZbiorcza != null`).

**Snippet:**

```csharp
// Worker SAM otwiera transakcję edycyjną i robi CommitUI() w środku — NIE owijaj go
// w session.Logout(true). Wystarczy go skonfigurować, wywołać i zapisać.
var worker = new EwidencjonowanieZbiorczeWorker
{
    Param = new EwidencjonowanieZbiorczeWorker.Params(context)
    {
        RaportDla = EwidencjonowanieZbiorczeWorker.RaportDla.Paragonów,
        ZaOkres = new FromTo(new Date(2026, 6, 1), new Date(2026, 6, 30)),  // data wystawienia
        OkresDostawyZaliczki = FromTo.All,                                  // bez filtra dostawy
        SymbolKasy = "D1",                                                  // jedna drukarka
        Definicja = CoreModule.GetInstance(session).DefDokumentow.WgSymbolu["SPZE"],
    }
};

worker.Ewidencjonuj();   // tworzy zbiorcze DokEwidencji w transakcji wewnętrznej (CommitUI)
session.Save();          // dopiero teraz zapis do bazy — tu wykrywane konflikty optymistyczne
```

**Pułapki:**
- `Ewidencjonuj()` **samodzielnie** otwiera `Session.Logout(true)` i kończy `CommitUI()`. Nie
  wywołuj go we własnej transakcji edycyjnej (zagnieżdżenie/podwójny commit). Po nim wykonaj
  `session.Save()` (w testach `SaveDispose()`).
- `Param` ustaw **przed** `Ewidencjonuj()` — jest to property `[Context]`; bez niej worker
  rzuci `NullReferenceException`.
- `Date` i `FromTo` to typy biznesowe — używaj `Date`/`Date.Today`, nie `DateTime`
  (`safe-code.md` §10). `FromTo.All` = bez ograniczenia, `FromTo.Empty` worker zamienia na `All`.
- `Definicja` to rekord konfiguracyjny — pobierz istniejący (`DefDokumentow.WgTypu[...]` /
  `WgSymbolu[...]`), nie twórz „w locie". Gdy `Definicja == null`, worker użyje domyślnej.
- Worker działa na danych z `ZaOkres` (data wystawienia) — zawsze podaj zakres, nie zostawiaj
  pełnego skanu całej historii.
- Konflikt edycji (ktoś zapisał ten sam dokument) wybuchnie w `session.Save()` jako
  `RowConflictException` — obsłuż go (refresh + retry lub eskalacja), nie połykaj (§4).

### W54 — Hurtowe zatwierdzanie / generowanie dokumentów dla zaznaczonego zbioru

**Cel:** wykonać operację cyklu życia (zatwierdzenie, cofnięcie do bufora, anulowanie) na
**wielu** dokumentach naraz, albo wygenerować dla zaznaczonego zbioru dokumenty podrzędne
(np. wiele zamówień → faktury, wiele faktur → jeden zbiorczy WZ) za pomocą `IRelacjeService`,
który przyjmuje **tablicę** dokumentów.

**Warianty:**

| Wariant | Mechanizm |
|---|---|
| Hurtowe zatwierdzanie | pętla po zbiorze, `dok.Stan = StanDokumentuHandlowego.Zatwierdzony`, jedna (krótka) transakcja |
| Hurtowe cofnięcie do bufora / anulowanie | `dok.Stan = StanDokumentuHandlowego.Bufor` / `.Anulowany` |
| Indywidualne generowanie podrzędnych | `IRelacjeService.NowyPodrzednyIndywidualny(DokumentHandlowy[], symbol)` — N nadrzędnych → N podrzędnych |
| Zbiorcze generowanie podrzędnego | `IRelacjeService.NowyPodrzednyZbiorczy(DokumentHandlowy[], symbol)` — wiele FA → 1 WZ |
| Zbiorcza korekta | `IRelacjeService.NowaKorektaZbiorcza(DokumentHandlowy[])` |
| Dołączenie nadrzędnego / podrzędnego | `DolaczNadrzedny`, `DolaczPodrzednyIndywidualny` |

**Pola i typy:**
- `dok.Stan: Soneta.Handel.StanDokumentuHandlowego` (`Bufor=0`, `Zatwierdzony=1`,
  `Zablokowany=2`, `Anulowany=3`). Skróty read-only: `dok.Bufor`, `dok.Zatwierdzony`,
  `dok.Anulowany`.
- `IRelacjeService` (namespace `Soneta.Handel.RelacjeDokumentow.Api`): metody przyjmują
  `DokumentHandlowy[]` i zwracają `DokumentHandlowy[]`. Dokumenty nadrzędne muszą być
  **zatwierdzone**. Dostęp: `session.GetRequiredService<IRelacjeService>()`
  (`using Microsoft.Extensions.DependencyInjection;`).

**Snippet:**

```csharp
var hm = session.GetHandel();
var fv = hm.DefDokHandlowych.WgSymbolu["FV"];
var od = new Date(2026, 6, 1);

// (1) Hurtowe zatwierdzanie zamówień z czerwca — filtr SERWEROWY + krótka transakcja
using (var t = session.Logout(editMode: true))
{
    foreach (DokumentHandlowy d in hm.DokHandlowe[(DokumentHandlowy d) =>
                 d.Data >= od && d.Definicja == fv && d.Stan == StanDokumentuHandlowego.Bufor])
    {
        d.Stan = StanDokumentuHandlowego.Zatwierdzony;   // pętla po Stan na zaznaczonym zbiorze
    }
    t.Commit();   // CommitUI() w workerze/extenderze
}
session.Save();

// (2) Wygenerowanie faktur dla zaznaczonych (zatwierdzonych) zamówień — IRelacjeService na tablicy
var rel = session.GetRequiredService<IRelacjeService>();
DokumentHandlowy[] zamowienia = /* zaznaczone, zatwierdzone ZO */;
using (var t = session.Logout(editMode: true))
{
    DokumentHandlowy[] faktury = rel.NowyPodrzednyIndywidualny(zamowienia, "FV");
    t.Commit();
}
session.Save();
```

**Pułapki:**
- `IRelacjeService` wymaga, by dokumenty nadrzędne były **zatwierdzone** — najpierw zatwierdź
  (wariant 1), potem generuj podrzędne.
- Operacje masowe wykonuj w jednej transakcji **tylko gdy zbiór jest mały**; dla dużych dziel na
  paczki (W55) — długa transakcja blokuje innych i zwiększa ryzyko konfliktu (§13.1).
- Zmiana `Stan` musi być w transakcji (`session.Logout(true)`); w workerze/extenderze
  `t.CommitUI()` zamiast `t.Commit()`.
- Nie iteruj całej tabeli `DokHandlowe` z `if` w pamięci — filtr serwerowy z zakresem czasowym
  (§6.1, §6.3). Zaznaczony w UI zbiór masz w `context` jako `DokumentHandlowy[]`.
- `Save()` po operacji relacji może rzucić `RowConflictException` (optimistic lock) — obsłuż (§4).

### W55 — Wydajne przetwarzanie wielu dokumentów w jednej sesji (paczki)

**Cel:** przetworzyć duży zbiór dokumentów (tysiące) w jednej sesji bez blokowania innych
użytkowników i bez ryzyka, że pojedynczy konflikt unieważni całą operację — przez podział na
**paczki** (krótkie transakcje, okresowy `Save()`).

**Warianty:**

| Wariant | Technika |
|---|---|
| Filtr serwerowy z zakresem czasowym | `hm.DokHandlowe[(DokumentHandlowy d) => d.Data >= od && d.Data <= doD && …]` |
| Paczki o stałym rozmiarze | licznik w pętli + `Commit()` / `Save()` co N rekordów |
| Izolacja konfliktu paczki | `try/catch (RowConflictException)` wokół `Save()` paczki, retry/log paczki |
| Tylko odczyt (raport) | `login.CreateSession(readOnly: true, …)` — bez transakcji edycyjnej |

**Pola i typy:** `Soneta.Types.Date` (zakres), `StanDokumentuHandlowego`, `RowConflictException`
(`session.Save()`), `IDisposable` na sesji i transakcji.

**Snippet:**

```csharp
const int rozmiarPaczki = 200;       // przetwarzaj po 200 dokumentów na transakcję
var hm = session.GetHandel();
var od  = new Date(2026, 1, 1);
var doD = Date.Today;

// Materializujemy KLUCZE/ID po stronie serwera (filtr), nie całe rekordy w pamięci wszystkie naraz.
// Iterujemy serwerowy zbiór i commitujemy paczkami — krótka transakcja na każdą paczkę.
int licznik = 0;
ITransaction t = session.Logout(editMode: true);
try
{
    foreach (DokumentHandlowy d in hm.DokHandlowe[(DokumentHandlowy d) =>
                 d.Data >= od && d.Data <= doD && d.Stan == StanDokumentuHandlowego.Bufor])
    {
        d.Stan = StanDokumentuHandlowego.Zatwierdzony;

        if (++licznik % rozmiarPaczki == 0)
        {
            t.Commit();
            t.Dispose();
            session.Save();                  // zamknięcie paczki — krótka transakcja
            t = session.Logout(editMode: true);
        }
    }
    t.Commit();
}
finally
{
    t.Dispose();
}
session.Save();                              // ostatnia (niepełna) paczka
```

**Pułapki:**
- **Krótka transakcja** to bezpieczeństwo, nie tylko wydajność — operacja > ~30 s powinna iść
  paczkami (§13.1). Jedna gigantyczna transakcja blokuje innych i zwiększa szansę konfliktu.
- Filtruj **serwerowo** (`SubTable[condition]`), z zakresem czasowym dla tabeli operacyjnej
  guided (`DokHandlowe`) — nigdy pełny skan (§6.1, §6.3). Nie używaj `.ToList().Where(...)`
  (§13.2).
- Po `session.Save()` w środku pętli okno edycji jest zamknięte — kolejną edycję otwórz **nową**
  transakcją (`session.Logout(true)`), inaczej `AccessWriteDenied`. (W testach wzorzec to
  `Save()` → `SaveDispose()` → odczyt na świeżej sesji po `Guid`.)
- Obsłuż `RowConflictException` per paczka (refresh + retry lub log i kontynuacja), nie łap
  `Exception` ogólnie (§4, §9.1). Połknięty wyjątek z `Save()` = utrata danych.
- Nie współdziel `Session`/`Row` między wątkami — równoległe przetwarzanie wymaga osobnej sesji
  na wątek (§3.1).
- Sesja zawsze w `using`/`try-finally` z `Dispose()` (§1.1); transakcja bez `Commit()` =
  automatyczny rollback.

---

> Powiązane: rozdz. 5 (cykl życia / `Stan`), rozdz. 8 (relacje, `IRelacjeService`),
> `safe-code.md` §4 (optimistic lock), §6 (filtr serwerowy), §13 (paczki),
> `rowcondition.md` (serwerowy LINQ).

---

## 11. Operacje pomocnicze (przekrojowe)

Rozdział zbiera wzorce „okołodokumentowe": bezpieczne pozyskanie kontrahenta i towaru do pozycji,
przeliczanie jednostek, walidację przed zatwierdzeniem, obsługę błędów i blokady optymistycznej,
odczyt metadanych (`ChangeInfos`) oraz pracę z definicjami i numeracją dokumentu. Fundamenty (sesja,
transakcja, `Save`, blokada optymistyczna) opisuje [`safe-code.md`](../safe-code.md) i
[`session-login.md`](../session-login.md) — tutaj się do nich odwołujemy.

> Cały kod jest zgodny z C# 10 (target-typed `new`, `var`, file-scoped namespace, wyrażenia `switch`,
> nazwane parametry `bool`) i operuje **wyłącznie na publicznym kontrakcie** platformy.

---

### W56 — Bezpieczne pobranie / utworzenie kontrahenta i towaru pozycji

**Cel:** przed dodaniem pozycji lub ustawieniem nabywcy bezpiecznie zlokalizować istniejący rekord
(kontrahent, towar), a gdy go brak — świadomie utworzyć nowy albo użyć kontrahenta jednorazowego
(systemowego rekordu „incydentalnego"). Chroni przed `NullReferenceException` w trakcie transakcji.

**Warianty:**

| Wariant | Mechanizm | Uwaga |
|---|---|---|
| Kontrahent po kodzie | `crm.Kontrahenci.WgKodu["Abc"]` | klucz unikalny, może być `null` |
| Kontrahent po NIP (dedup) | `crm.Kontrahenci.WgNIP[(Kontrahent k)=>k.NIP==nip]` | filtr serwerowy, normalizuj `Nip.Flat` |
| Kontrahent jednorazowy / incydentalny | `Kontrahent.INCYDENTALNY` (stała `Guid`), `k.JestIncydentalny` | rekord systemowy — dane nabywcy zapisz na dokumencie |
| Utworzenie nowego kontrahenta | `new Kontrahent()` + `AddRow` | patrz W3 w `kontrahent.md` |
| Towar po kodzie | `tm.Towary.WgKodu["BIKINI"]` | klucz unikalny, może być `null` |
| Brak towaru | przerwij operację (`BusException`) | nie twórz towaru „w locie" w trakcie wystawiania |

**Pola i typy:** `crm.Kontrahenci.WgKodu: GuidedTable` (indeks po `Kod`), `Kontrahent.JestIncydentalny:
bool` (kalkulowane), `Kontrahent.INCYDENTALNY: System.Guid` (stała), `tm.Towary.WgKodu` (indeks po
`Kod`), `dok.Kontrahent: Kontrahent`. Dostęp do kontrahenta incydentalnego po `Guid`:
`crm.Kontrahenci[Kontrahent.INCYDENTALNY]` (indeksator `GuidedTable` po `Guid`).

**Snippet:**

```csharp
var crm = session.GetCRM();
var tm  = session.GetTowary();

// 1. Kontrahent po kodzie — może nie istnieć
Kontrahent kontrahent = crm.Kontrahenci.WgKodu["Abc"];

// 2. Gdy brak po kodzie — dedup po NIP, zanim ewentualnie utworzymy nowego
if (kontrahent == null && !string.IsNullOrEmpty(nip))
{
    var flat = Nip.Flat(nip);                         // normalizacja przed porównaniem
    kontrahent = crm.Kontrahenci.WgNIP[(Kontrahent k) => k.NIP == flat].FirstOrDefault();
}

// 3. Sprzedaż jednorazowa (klient detaliczny bez kartoteki) — kontrahent incydentalny
if (kontrahent == null)
    kontrahent = crm.Kontrahenci[Kontrahent.INCYDENTALNY];   // systemowy rekord „incydentalny"

// 4. Towar pozycji — gdy brak, przerywamy świadomie (nie wystawiamy „pustej" pozycji)
Towar towar = tm.Towary.WgKodu["BIKINI"];
if (towar == null)
    throw new BusException("Brak towaru o kodzie BIKINI.".Translate());

using (var t = session.Logout(editMode: true))
{
    dok.Kontrahent = kontrahent;                      // gdy definicja wymaga nabywcy
    t.Commit();                                       // CommitUI() w workerze/extenderze
}
session.Save();
```

**Pułapki:**
- `WgKodu[...]` zwraca **jeden** rekord lub `null` (klucz unikalny). `WgNIP[condition]` /
  `WgNazwy[...]` zwracają **zbiór** — użyj `.FirstOrDefault()`. Nie iteruj całej tabeli `Kontrahenci`
  / `Towary` w pamięci — to kartoteki; filtruj serwerowo (`SubTable[condition]`, `safe-code.md` §6).
- **Kontrahenta incydentalnego nie wolno ustawić na każdym typie dokumentu** — na fakturze sprzedaży
  (np. `FV`) przypisanie `dok.Kontrahent = crm.Kontrahenci[Kontrahent.INCYDENTALNY]` rzuca
  `ArgumentException` („Nie można ustawiać kontrahenta incydentalnego w dokumentach typu 'FV'"). Rekord
  incydentalny jest przeznaczony do sprzedaży detalicznej (np. paragon) — na fakturze podaj realnego nabywcę.
- Kontrahenta jednorazowego pobieraj jako rekord **incydentalny** (`Kontrahent.INCYDENTALNY`) — nie
  twórz za każdym razem nowego rekordu w kartotece. Rekordu incydentalnego nie modyfikuj
  (`JestIncydentalny == true`); dane konkretnego nabywcy (nazwa, NIP, adres) zapisz na samym
  dokumencie / w jego polach adresowych, nie na rekordzie kontrahenta.
- Nie twórz towaru „w locie" przy wystawianiu dokumentu — brak towaru to błąd danych, nie sytuacja do
  cichego uzupełnienia. Towar musi mieć ustawioną jednostkę (W57).
- W `RowCondition` używaj tylko pól bazodanowych. `JestIncydentalny`, `NazwaFormatowana` itp. są
  kalkulowane → w wyrażeniu LINQ rzucą `LinqConditionException`.

---

### W57 — Przeliczanie jednostek miary towaru przy dodawaniu pozycji

**Cel:** dodać pozycję w jednostce pomocniczej (np. opakowanie zbiorcze, „km", „kg") i poprawnie
przeliczyć ją na jednostkę podstawową towaru, korzystając z przeliczników zdefiniowanych dla towaru.

**Warianty:**

| Wariant | Mechanizm | Uwaga |
|---|---|---|
| Pozycja w jednostce podstawowej | `poz.Ilosc = new Quantity(n, poz.Ilosc.Symbol)` | symbol z pozycji po ustawieniu `Towar` |
| Pozycja w jednostce pomocniczej | `new Quantity(n, "OPAK")` | symbol jednostki pomocniczej |
| Jawne przeliczenie ilości | `towar.PrzeliczJednostkę(jednostka, qty, throwError)` | zwraca `Quantity` w jednostce docelowej |
| Jednostka podstawowa towaru | `towar.Jednostka: Jednostka` | jednostka, w której prowadzony jest magazyn |
| Jednostka uzupełniająca (Intrastat/CN) | `towar.JednostkaUzupelniajaca: Jednostka` | wymaga zdefiniowanego przelicznika |
| Brak przelicznika | `throwError: true` → wyjątek | brak przelicznika = niejednoznaczne przeliczenie |

**Pola i typy:** `Towar.Jednostka: Soneta.Handel.Jednostka`, `Towar.JednostkaUzupelniajaca:
Jednostka`, `Towar.PrzeliczJednostkę(Jednostka jednostka, Quantity qty, bool throwError): Quantity`,
`tm.Jednostki` (tabela jednostek, indeks `WgKodu`). `Quantity` (`Soneta.Types`) = wartość + symbol
jednostki; `poz.Ilosc.Symbol` po ustawieniu `poz.Towar` przyjmuje symbol jednostki podstawowej.

**Snippet:**

```csharp
var tm = session.GetTowary();
var towar = tm.Towary.WgKodu["TRANSPORT"];           // towar prowadzony np. w „km"

using (var t = session.Logout(editMode: true))
{
    var poz = new PozycjaDokHandlowego(dok);         // ctor wymaga dokumentu
    session.AddRow(poz);
    poz.Towar = towar;                               // USTAW PIERWSZY — inicjuje jednostkę na Ilosc/Cena

    // Wariant A: ilość w jednostce podstawowej towaru (symbol z pozycji)
    poz.Ilosc = new Quantity(10, poz.Ilosc.Symbol);

    // Wariant B: ilość podana w jednostce pomocniczej i przeliczona na podstawową
    var jednPom = tm.Jednostki.WgKodu["OPAK"];       // jednostka pomocnicza
    var iloscPom = new Quantity(3, jednPom.Kod);
    // throwError: true — brak przelicznika OPAK→podstawowa zgłosi wyjątek zamiast cichego błędu
    Quantity iloscPodstawowa = towar.PrzeliczJednostkę(towar.Jednostka, iloscPom, throwError: true);
    poz.Ilosc = iloscPodstawowa;

    t.Commit();
}
session.Save();
```

**Pułapki:**
- `poz.Towar` ustaw **przed** `Ilosc`/`Cena` — to on inicjuje symbol jednostki na pozycji. Konstrukcja
  `new Quantity(n, poz.Ilosc.Symbol)` gwarantuje zgodny symbol; podanie surowego symbolu spoza
  jednostek towaru daje przeliczenie tylko przy istniejącym przeliczniku.
- `PrzeliczJednostkę(..., throwError: true)` rzuci wyjątek, gdy **brak przelicznika** między
  jednostkami — to świadomy wybór: lepszy twardy błąd niż cicha, niepoprawna ilość. Dla `false`
  zwraca ilość bez przeliczenia (ryzykowne).
- `Quantity` to typ wartość+symbol (nie `double`). Nie mieszaj `Quantity` o różnych symbolach w
  arytmetyce — najpierw sprowadź do jednej jednostki przez `PrzeliczJednostkę`.
- `JednostkaUzupelniajaca` (CN/Intrastat) wymaga przelicznika z jednostki podstawowej; jego brak
  zgłaszany jest przy wyliczeniach Intrastat — zdefiniuj przelicznik na towarze.
- Przeliczniki to dane konfiguracyjne towaru — nie twórz ich „w locie" w trakcie wystawiania
  dokumentu; brak przelicznika to sygnał błędu konfiguracji, nie do obejścia w kodzie pozycji.

---

### W58 — Walidacja przed zatwierdzeniem (kompletność, zasób, limit kredytowy)

**Cel:** przed zmianą stanu na `Zatwierdzony` sprawdzić kompletność danych (kontrahent, pozycje),
dostępność zasobu magazynowego oraz przygotować się na automatyczną kontrolę limitu kredytowego
nabywcy. Pozwala zgłosić czytelny błąd zamiast łapać wyjątek głęboko w `Save()`.

**Warianty:**

| Wariant | Sprawdzenie (publiczny kontrakt) | Egzekwowanie |
|---|---|---|
| Kompletność danych | `dok.Kontrahent != null`, `!dok.Pozycje.IsEmpty` | własna walidacja przed `Stan` |
| Dostępność zasobu (stan ujemny) | przyjęcie (PW/PZ) zapisane przed rozchodem | weryfikator Demo `StanUjemnyVerifier` — wyjątek w `Save()` |
| Limit kredytowy nabywcy | `dok.Kontrahent.LimitKredytu`, `KontrolaAktywna`, `TypLimituKredytowego` | platforma kontroluje **automatycznie** przy zatwierdzeniu |
| Termin / forma płatności | `dok.Platnosci` (W z sekcji N) | wynika z definicji i kontrahenta |

**Pola i typy:** `dok.Pozycje: SubTable<PozycjaDokHandlowego>` (`.IsEmpty: bool`), `dok.Kontrahent:
Kontrahent`, `dok.Stan: StanDokumentuHandlowego`. Po stronie kontrahenta (odczyt):
`Kontrahent.LimitKredytu: Currency`, `Kontrahent.TypLimituKredytowego`, `Kontrahent.KontrolaAktywna:
bool` (kalkulowane) — patrz W9 w `kontrahent.md`.

**Snippet:**

```csharp
// Walidacja PRZED próbą zmiany stanu — czytelny błąd zamiast wyjątku z głębi Save()
if (dok.Kontrahent == null)
    throw new RowException(dok, "Dokument nie ma nabywcy.".Translate());
if (dok.Pozycje.IsEmpty)
    throw new RowException(dok, "Dokument nie ma pozycji.".Translate());

// Informacyjnie: czy nabywca ma aktywną kontrolę kredytową (odczyt pól kalkulowanych)
if (dok.Kontrahent.KontrolaAktywna)
{
    // limit jest egzekwowany automatycznie przy zatwierdzeniu — patrz pułapki
}

using (var t = session.Logout(editMode: true))
{
    dok.Stan = StanDokumentuHandlowego.Zatwierdzony;     // tu uruchamia się kontrola limitu/zasobu
    t.Commit();
}
session.Save();   // brak zasobu (StanUjemnyVerifier) / przekroczony limit → wyjątek właśnie tutaj
```

**Pułapki:**
- **Kontrola limitu kredytowego jest wewnętrzna i automatyczna** — uruchamia się przy zatwierdzaniu
  dokumentu rozchodowego, gdy definicja ma ustawione „zachowanie po przekroczeniu limitu". Z dodatku
  zewnętrznego **nie wywołujesz jej ręcznie** (logika `LimitKredytowyDokumentu` jest `internal`) —
  czytasz pola kontrahenta (`LimitKredytu`, `KontrolaAktywna`) i obsługujesz `InvalidOperationException`
  zgłaszany przez platformę przy zatwierdzaniu.
- W bazie Demo `StanUjemnyVerifier` blokuje rozchód bez wcześniejszego **zapisanego** przyjęcia.
  Samo `CommitUI` nie księguje zasobów — magazyn księguje się dopiero po `Session.Save()`, więc błąd
  pojawia się w `Save()`, nie w transakcji.
- `IsEmpty` na kolekcji `SubTable` to **właściwość** (serwerowy `exists`, bez nawiasów) — nie
  materializuj `Pozycje.ToList().Count`.
- Walidację własną rzucaj jako `RowException(dok, "…".Translate())` **przed** `Commit()`. Wyjątek po
  `Commit()` nie wycofa zmiany z sesji (safe-code §5.1).

---

### W59 — Obsługa błędów i blokada optymistyczna (kolizje `Save`, ponowienie)

**Cel:** poprawnie obsłużyć wyjątki zgłaszane przez `Session.Save()` — w szczególności konflikt
optymistyczny (ktoś inny zapisał ten sam rekord) — zamiast je „połykać"; w razie konfliktu odświeżyć
dane i ponowić operację.

**Warianty:**

| Wariant | Wyjątek | Reakcja |
|---|---|---|
| Konflikt optymistyczny | `RowConflictException` | świeża sesja → ponów operację (retry) |
| Naruszenie integralności / unikalności | `RowException` (z `InnerException`) | komunikat dla użytkownika, bez retry |
| Walidacja biznesowa | `RowException` / `BusException` | zgłoś użytkownikowi, popraw dane |
| Brak praw / okno edycji zamknięte | `AccessWriteDenied` | edytuj na świeżej, zalogowanej sesji |

**Pola i typy:** `Session.Save()`, `Session.Logout(editMode: true)`, wyjątki z `Soneta.Business`
(`RowConflictException`, `RowException`, `BusException`, `AccessWriteDenied`). Po `Save()` w środku
operacji okno edycji bywa zamknięte — kolejna edycja na tej samej sesji rzuci `AccessWriteDenied`.

**Snippet:**

```csharp
// Ponowienie przy konflikcie optymistycznym (retry na świeżych danych)
const int maxProb = 3;
for (int proba = 1; ; proba++)
{
    var dok = session.GetHandel().DokHandlowe[guidDokumentu];   // świeży odczyt po Guid
    try
    {
        using (var t = session.Logout(editMode: true))
        {
            dok.Stan = StanDokumentuHandlowego.Zatwierdzony;
            t.Commit();
        }
        session.Save();
        break;                                                  // sukces
    }
    catch (RowConflictException) when (proba < maxProb)
    {
        // ktoś zapisał rekord równolegle — odśwież i spróbuj ponownie
        session = session.Login.CreateSession(readOnly: false, config: false, name: "Retry");
    }
    catch (RowException ex)
    {
        // naruszenie integralności / unikalności / walidacja — bez retry
        throw new BusException($"Nie udało się zapisać dokumentu: {ex.Message}".Translate(), ex);
    }
}
```

**Pułapki:**
- Konflikt optymistyczny ujawnia się **dopiero w `Save()`** (nie w `Commit`). Nie połykaj
  `RowConflictException` — albo ponów na świeżych danych, albo eskaluj (safe-code §4).
- Retry rób na **świeżym odczycie** rekordu (po `Guid`) w nowej/odświeżonej sesji — ponowne
  zapisanie tej samej, „starej" instancji odtworzy konflikt.
- Po `Save()` wewnątrz dłuższej operacji okno edycji jest zamknięte → następna edycja na tej samej
  sesji rzuci `AccessWriteDenied`. Wzorzec: zapis → świeża sesja → odczyt po `Guid` → kolejna edycja.
- Nie używaj `catch (Exception)` bez ponownego rzutu — zgubisz informację o przyczynie. Ogranicz
  retry liczbą prób, by nie zapętlić przy trwałym konflikcie.

---

### W60 — Odczyt metadanych dokumentu (`ChangeInfos` — kto/kiedy założył i zmienił)

**Cel:** odczytać informacje audytowe rekordu dokumentu: kto i kiedy go założył oraz kto ostatnio go
zmodyfikował. Dane pochodzą z tabeli `ChangeInfos` i są dostępne przez kalkulowane właściwości
`GuidedRow` (dokument jest `GuidedRow`).

**Warianty:**

| Wariant | Właściwość (kalkulowana) | Zawartość |
|---|---|---|
| Kto/kiedy założył | `dok.FirstChangeInfo: ChangeInfo` | operator i czas utworzenia |
| Kto/kiedy ostatnio zmienił | `dok.LastChangeInfo: ChangeInfo` | operator i czas ostatniej zmiany |
| Pełna historia zmian | `session.GetBusiness().ChangeInfos[dok]` | kolekcja wpisów (`SubTable`) |
| Wyłączenie zapisu historii dla rekordu | `dok.SetChangeInfo(false)` | wyłącza rejestrację `ChangeInfo` dla tego wiersza |

**Pola i typy:** `GuidedRow.FirstChangeInfo: Soneta.Business.ChangeInfo` (Caption „Założył"),
`GuidedRow.LastChangeInfo: ChangeInfo` (Caption „Ostatnia zmiana"). `ChangeInfo` udostępnia m.in.
`Operator` (rekord operatora), `Time`/`Godzina` (czas) oraz `Type: ChangeInfoType`. Kolekcja:
`session.GetBusiness().ChangeInfos[row]`.

**Snippet:**

```csharp
var dok = session.GetHandel().DokHandlowe[guidDokumentu];

// Kto i kiedy założył dokument (najwcześniejszy wpis ChangeInfos)
ChangeInfo zalozyl = dok.FirstChangeInfo;
if (zalozyl != null)
{
    Operator ktoZalozyl = zalozyl.Operator;     // rekord operatora
    // zalozyl.Time / zalozyl.Godzina — czas utworzenia
}

// Kto ostatnio zmodyfikował
ChangeInfo ostatnia = dok.LastChangeInfo;
if (ostatnia != null)
{
    Operator ktoZmienil = ostatnia.Operator;
}

// Pełna historia zmian rekordu
foreach (ChangeInfo ci in session.GetBusiness().ChangeInfos[dok])
{
    // ci.Operator, ci.Time, ci.Type (ChangeInfoType: Added / Modified / Deleted ...)
}
```

**Pułapki:**
- `FirstChangeInfo` / `LastChangeInfo` są **kalkulowane** (zapytania `select top 1 ... from
  ChangeInfos`) — tylko do odczytu, nie ustawiaj. Mogą zwrócić `null`, gdy historia rekordu jest
  pusta (np. import bez rejestracji `ChangeInfo`) — zawsze sprawdź `!= null`.
- Rejestracja `ChangeInfo` zależy od konfiguracji (`ChangeInfoMode` per tabela). Jeśli historia jest
  wyłączona, właściwości mogą być puste — nie zakładaj, że audyt jest zawsze włączony.
- Każdy odczyt `FirstChangeInfo`/`LastChangeInfo` to osobne zapytanie SQL — przy przeglądaniu wielu
  dokumentów nie wywołuj ich w pętli po całej tabeli; ogranicz zakres (safe-code §6).
- Nie loguj danych operatora w sposób ujawniający wrażliwe informacje (safe-code §12).

---

### W61 — Praca z definicjami i numeracją (seria, wymuszenie numeru, bufor `Numer`)

**Cel:** rozpoznać definicję dokumentu i jej schemat numeracji, ustawić/odczytać serię, w razie
potrzeby wymusić konkretny numer, oraz zrozumieć relację między buforem a numerem końcowym
(dokument w buforze ma numer „BUFOR", numer właściwy nadawany jest przy zatwierdzeniu).

**Warianty:**

| Wariant | Mechanizm (publiczny) | Uwaga |
|---|---|---|
| Pobranie definicji | `session.GetHandel().DefDokHandlowych.WgSymbolu["FV"]` | symbol z bazy Demo |
| Ustawienie definicji na dokumencie | `dok.Definicja = def` | ustaw **pierwszą**, przed innymi polami |
| Rozpoznanie / ustawienie serii | `dok.Seria`, `dok.GetListSeria()` | seria tylko gdy numeracja ma komponent „Seria" |
| Numer w buforze | `dok.BuforNumer` → `"BUFOR"`, `dok.Numer.NumerPelny` | numer właściwy nadawany przy zatwierdzeniu |
| Wymuszenie numeru | `dok.Numer.NumerPelny = "..."` | tylko gdy definicja na to pozwala |
| Pełny numer (do odczytu) | `dok.Numer.NumerPelny`, `dok.NumerPelnyZapisany` | string z serią i numerem |

**Pola i typy:** `dok.Definicja: Soneta.Handel.DefDokHandlowego`, `dok.Seria: string`,
`dok.GetListSeria(): string[]`, `dok.Numer: Soneta.Core.NumerDokumentu` (bufor numeracji:
`NumerPelny: string`, `PrzeliczSymbol(string component)`), `dok.NumerPelnyZapisany: string`,
`dok.BuforNumer: string` (kalkulowane → `"BUFOR"` w buforze), `dok.Bufor: bool` (kalkulowane).

**Snippet:**

```csharp
var hm = session.GetHandel();

using (var t = session.Logout(editMode: true))
{
    var dok = new DokumentHandlowy();
    session.AddRow(dok);
    dok.Definicja = hm.DefDokHandlowych.WgSymbolu["FV"];   // definicja PIERWSZA — niesie schemat numeracji
    dok.Kontrahent = session.GetCRM().Kontrahenci.WgKodu["Abc"];

    // Seria — tylko gdy schemat numeracji definicji ma komponent „Seria"
    string[] dostepneSerie = dok.GetListSeria();
    if (dostepneSerie.Length > 0)
        dok.Seria = dostepneSerie[0];                      // ustawienie serii przelicza numer

    t.Commit();
}
session.Save();

// Odczyt numeru: w buforze numer właściwy nie jest jeszcze nadany
bool wBuforze = dok.Bufor;          // true → BuforNumer == "BUFOR"
string numer  = dok.Numer.NumerPelny;   // pełny numer (z serią), nadany przy zatwierdzeniu

// Zatwierdzenie nadaje numer właściwy
using (var t = session.Logout(editMode: true))
{
    dok.Stan = StanDokumentuHandlowego.Zatwierdzony;
    t.Commit();
}
session.Save();
```

**Pułapki:**
- `Definicja` ustaw **jako pierwszą** — to ona określa wymagane pola (magazyn, kontrahent) oraz
  schemat numeracji (`Numeracja`). Zmiana definicji po wypełnieniu dokumentu jest ograniczona
  (`IsReadOnlyDefinicja()`).
- `Seria` można ustawić **tylko**, gdy numeracja definicji ma komponent „Seria" — w przeciwnym razie
  setter rzuci `RowException` („SeriesDeniedErr"). Sprawdź przez `GetListSeria()` (zwraca dozwolone
  wartości; przy słowniku serii — tylko wartości ze słownika).
- Numer właściwy nadawany jest **przy zatwierdzeniu**; dokument w buforze ma `BuforNumer == "BUFOR"`,
  a `Numer.NumerPelny` zawiera znacznik „/BUFOR". Nie traktuj numeru z bufora jako ostatecznego.
- Wymuszenie numeru przez `dok.Numer.NumerPelny = "..."` działa tylko w granicach dozwolonych przez
  definicję (`IsReadOnlyNumerPelny()`); kolizja z istniejącym numerem ujawni się jako `RowException`
  z `DuplicateKeyException` w `Save()`.
- `Numer` to obiekt `NumerDokumentu` (bufor numeracji), nie zwykły string — pełny numer czytaj przez
  `Numer.NumerPelny` lub `NumerPelnyZapisany`, nie składaj go ręcznie z serii i liczby.

---

---

## 12. Wydruki i raporty

Wydruk dokumentu handlowego (faktura, dokument magazynowy, paragon) oraz raporty
i zestawienia tworzy się przez **serwis `IReportService`** z modułu `Soneta.Business.UI`.
Serwis bierze wzorzec wydruku (`*.repx` / `*.aspx` / `*.dotx`), kontekst z danymi
(rekord, zaznaczenie, parametry) i zwraca **gotowy dokument jako strumień** (`Stream`) —
bez udziału interfejsu użytkownika. To jest jedyny mechanizm, którego dodatek zewnętrzny
powinien używać do programowego generowania wydruków (export do PDF, wysyłka e-mail,
archiwizacja). Klasa `ReportResult` opisuje *co* i *jak* wydrukować.

> **Dostęp do serwisu (publiczny kontrakt):**
> ```csharp
> using Microsoft.Extensions.DependencyInjection;   // GetRequiredService
> using Soneta.Business.UI;                          // IReportService, ReportResult, ReportFormats, ReportTargets
>
> var raporty = session.GetRequiredService<IReportService>();
> ```

**Metody `IReportService` (publiczne):**

| Metoda | Zwraca | Zastosowanie |
|---|---|---|
| `Stream GenerateReport(ReportResult rr)` | strumień (PDF/XLSX/PNG/…) | generowanie wydruku binarnego do strumienia/pliku/e-maila |
| `string GenerateReportStr(ReportResult rr)` | string | wydruk tekstowy (`HTML`, `TXT`) |
| `void PrintReport(ReportResult rr, bool archive = false, string archivePath = "")` | — | wydruk **na drukarkę** (sprzęt), opcjonalna archiwizacja na dysk |
| `Type[] GetParameterTypes(string templateFileName, Context context)` | typy parametrów | sprawdzenie, jakich obiektów parametrów wymaga wzorzec |

**Pola `ReportResult` (publiczne, najważniejsze):**

| Pole | Typ | Znaczenie |
|---|---|---|
| `TemplateFileName` | `string` | nazwa wzorca (np. `"Sprzedaz.repx"`, `"Zakup.repx"`). Ustawienie go włącza tryb automatyczny (bez UI). |
| `DataType` | `Type` | typ danych branych z kontekstu: `typeof(DokumentHandlowy)` (jeden), `typeof(DokumentHandlowy[])` (zaznaczone), `typeof(DokHandlowe)` (cały widok). |
| `Context` | `Context` | kontekst z rekordem(-ami) i parametrami wydruku (`Context.Set(...)`). |
| `OutputFormat` | `ReportFormats` | `PDF`, `XLSX`, `XLS`, `CSV`, `DOCX`, `TXT`, `HTML`, `MHT`, `PNG`. Domyślnie `HTML`. |
| `Target` | `ReportTargets` | cel: `File`, `Printer`, `PrinterService`, `Preview`, `Attachment`, `Email`, `ShareDocument`, `OpenApplication`. Domyślnie `File`. |
| `AskForParameters` | `bool` | `false` = brak okien z pytaniem o parametry (tryb wsadowy). |
| `PrinterName` | `string` | nazwa drukarki dla `Target = Printer`. |
| `Encrypt` | `string` | hasło szyfrujące PDF. |
| `Sign`, `VisibleSignature` | `bool` | podpis certyfikatem (tylko tryb interaktywny okienkowy). |
| `OutputHandler` | `Func<Stream,object>` | własna obsługa gotowego strumienia (tryb wzorca; **nieobsługiwane przez `IReportService`** — patrz W66). |
| `ReportName` | `string` | nazwa wydruku z menu (tryb interaktywny; **wyklucza się** z `TemplateFileName`/`IReportService`). |

> **Reguła spójności (`CheckConsistency`):** `IReportService` wymaga ustawionego
> `TemplateFileName` i **nie** akceptuje `OutputHandler` ani `ReportName`. `ReportName`
> i `TemplateFileName` wzajemnie się wykluczają. Naruszenie → `ArgumentException`.

---

### W62 — Wydruk faktury do PDF / na drukarkę

**Cel:** wygenerować wydruk pojedynczego dokumentu handlowego (faktura sprzedaży FV,
faktura zakupu FZ, paragon) do strumienia PDF albo wysłać go na drukarkę.

**Warianty:**

| Wariant | Ustawienie | Uwaga |
|---|---|---|
| Faktura sprzedaży → PDF | `TemplateFileName = "Sprzedaz.repx"`, `OutputFormat = PDF` | strumień `%PDF…` |
| Faktura zakupu → PDF | `TemplateFileName = "Zakup.repx"` | analogicznie |
| Wydruk HTML / TXT | `OutputFormat = HTML` / `TXT` | użyj `GenerateReportStr` lub `GenerateReport` |
| Duplikat / oryginał | parametr `ParametryWydrukuDokumentu { Duplikat = … }` w kontekście | parametr wzorca |
| Na drukarkę (sprzęt) | `Target = Printer`, `PrintReport(rr)` | wymaga drukarki — patrz „Pułapki” |
| PDF szyfrowany | `Encrypt = "hasło"` | hasło otwarcia pliku |

**Pola i typy:** `IReportService.GenerateReport(ReportResult) : Stream`,
`ReportResult.TemplateFileName : string`, `ReportResult.DataType : Type`,
`ReportResult.OutputFormat : ReportFormats`, `ReportResult.Context : Context`,
`ParametryWydrukuDokumentu : ContextBase` (parametry wzorca dokumentu, m.in. `Duplikat : bool`).

**Snippet:**

```csharp
using Microsoft.Extensions.DependencyInjection;
using Soneta.Business.UI;
using Soneta.Handel;

// 'dok' to zatwierdzona faktura sprzedaży (FV). 'session' — bieżąca sesja.
var raporty = session.GetRequiredService<IReportService>();

// 1. Kontekst: pojedynczy dokument + jego elementy + parametry wzorca.
var context = new Context(session);
context.Set(dok);
context.Set(dok.Definicja);
context.Set(dok.Kontrahent);
context.Set(new DokumentHandlowy[] { dok });           // wymagane przez niektóre wzorce
context.Set(new ParametryWydrukuDokumentu(context) { Duplikat = false });

// 2. Opis wydruku — tryb automatyczny (TemplateFileName) → bez UI.
var rr = new ReportResult {
    TemplateFileName = "Sprzedaz.repx",                // "Zakup.repx" dla faktury zakupu
    DataType = typeof(DokumentHandlowy),               // wydruk dla pojedynczego dokumentu
    Context = context,
    OutputFormat = ReportFormats.PDF,
    AskForParameters = false                           // tryb wsadowy — nie pytaj o parametry
};

// 3. Generowanie do strumienia i zapis do pliku.
using (Stream pdf = raporty.GenerateReport(rr))
using (var plik = new FileStream(@"C:\Temp\FV.pdf", FileMode.Create, FileAccess.Write))
    pdf.CopyTo(plik);
```

**Pułapki:**
- `GenerateReport` zwraca **`Stream`** dla formatów binarnych (PDF, XLSX, PNG). Dla
  `HTML`/`TXT` użyj `GenerateReportStr` (zwraca `string`). Zwrócony strumień **opakuj w `using`**.
- Kontekst musi zawierać wszystko, czego wymaga wzorzec: rekord (`Context.Set(dok)`),
  tablicę zaznaczeń **i** instancję parametrów (`ParametryWydrukuDokumentu`). Brak parametru
  + `AskForParameters = true` w trybie wsadowym zawiesi się na oczekiwaniu na UI — w kodzie
  bez interfejsu zawsze ustaw `AskForParameters = false`.
- Wydruk faktury powinien dotyczyć dokumentu **zatwierdzonego** (`Stan == Zatwierdzony`) —
  dokument w buforze nie ma jeszcze nadanego numeru pełnego.
- Sprawdzenie poprawności PDF w teście: pierwsze 4 znaki strumienia to `"%PDF"`;
  HTML zaczyna się od `"<!DOCTYPE html"`.
- **Druk na fizyczną drukarkę** (`PrintReport`, `Target = Printer`) wymaga sprzętu i
  sterownika — **nie da się tego przetestować jednostkowo**. W testach i integracjach
  używaj ścieżki `GenerateReport` → strumień/PDF.

---

### W63 — Wydruk dokumentu magazynowego (PZ/WZ/MM)

**Cel:** wydrukować dokument magazynowy (PZ, WZ, MM, RW, PW) — identyczny mechanizm jak
dla faktury, różni się tylko wzorcem dobranym do rodzaju dokumentu (wg jego definicji).

**Warianty:**

| Wariant | Wzorzec / `DataType` |
|---|---|
| Przyjęcie / wydanie magazynowe | wzorzec magazynowy (`*.repx`), `DataType = typeof(DokumentHandlowy)` |
| Przesunięcie MM | wzorzec MM |
| Wydruk wg definicji dokumentu | wzorzec domyślny przypisany do `dok.Definicja` |

**Pola i typy:** jak w W62 — `IReportService.GenerateReport`, `ReportResult.TemplateFileName`,
`DokumentHandlowy.Definicja` (decyduje o domyślnym wzorcu).

**Snippet:**

```csharp
using Microsoft.Extensions.DependencyInjection;
using Soneta.Business.UI;
using Soneta.Handel;

// 'wz' — zatwierdzony dokument WZ (rozchód magazynowy).
var raporty = session.GetRequiredService<IReportService>();

var context = new Context(session);
context.Set(wz);
context.Set(wz.Definicja);
context.Set(wz.Magazyn);
context.Set(new DokumentHandlowy[] { wz });
context.Set(new ParametryWydrukuDokumentu(context) { Duplikat = false });

var rr = new ReportResult {
    TemplateFileName = "WydanieZewnetrzne.repx",   // wzorzec właściwy dla danego rodzaju dokumentu
    DataType = typeof(DokumentHandlowy),
    Context = context,
    OutputFormat = ReportFormats.PDF,
    AskForParameters = false
};

using (Stream pdf = raporty.GenerateReport(rr)) {
    // pdf → plik / e-mail / archiwum
}
```

**Pułapki:**
- Dokument magazynowy i faktura to ten sam typ `DokumentHandlowy` — różni je **definicja**
  (`dok.Definicja`) i przypisany wzorzec. Dobierz `TemplateFileName` zgodny z rodzajem
  dokumentu; nie drukuj WZ wzorcem faktury sprzedaży.
- Dla dokumentów magazynowych ustaw w kontekście `dok.Magazyn` (część wzorców go wymaga).
- Nazwy wzorców są elementem konfiguracji wdrożenia (lista wydruków zarejestrowanych dla typu).
  Listę typów parametrów, których wymaga konkretny wzorzec, sprawdzisz przez
  `GetParameterTypes(templateFileName, context)` przed wywołaniem `GenerateReport`.

---

### W64 — Raport dobowy i okresowy (zestawienie za dzień / okres)

**Cel:** wygenerować zestawienie/rejestr dokumentów za **wskazany dzień** (raport dobowy)
lub **wskazany okres** (raport okresowy). Dwie odrębne ścieżki:
1. **Zestawienie/raport bazodanowy** — przez `IReportService` z wzorcem zestawienia i
   parametrem okresu (analizowalny, zapisywalny do PDF/XLSX) — **ścieżka testowalna**.
2. **Raport fiskalny drukarki** (`RaportDobowy`/`RaportOkresowy`) — wydruk na **drukarce
   fiskalnej** przez `IFiscalPrinterAPI` — wymaga sprzętu, **nietestowalny jednostkowo**.

**Warianty:**

| Wariant | Mechanizm | Parametr okresu |
|---|---|---|
| Zestawienie sprzedaży za dzień → PDF | `IReportService` + wzorzec zestawienia, `DataType = typeof(DokHandlowe)` | `FromTo(dzień, dzień)` w parametrach wzorca |
| Zestawienie za okres → PDF/XLSX | jw. | `FromTo(od, do)` |
| Fiskalny raport dobowy (sprzęt) | `IFiscalPrinterAPI.DrukujRaport(nazwaDrukarki)` | dzień bieżący |
| Fiskalny raport okresowy (sprzęt) | `IFiscalPrinterAPI.DrukujRaportOkresowy(nazwaDrukarki, RaportOkresowyParams)` | `RaportOkresowyParams.RaportZaOkres : FromTo` |

**Pola i typy:**
`Soneta.Fiskal.IFiscalPrinterAPI` (publiczny): `DrukujRaport(string nazwaDrukarki)`,
`DrukujRaportOkresowy(string nazwaDrukarki, RaportOkresowyParams pars)`,
`Fiskalizuj(DokumentHandlowy dok, string nazwaDrukarki)`.
`Soneta.Fiskal.RaportOkresowyParams : ContextBase` — `RaportZaOkres : FromTo` (`[Required]`),
inicjalizowany na dzień bieżący; ctor `RaportOkresowyParams(Context)`.

**Snippet:**

```csharp
using Microsoft.Extensions.DependencyInjection;
using Soneta.Business.UI;
using Soneta.Types;          // FromTo, Date

// --- Ścieżka 1: zestawienie bazodanowe za wskazany dzień → PDF (testowalne) ---
var raporty = session.GetRequiredService<IReportService>();

var dzien = Date.Today;
var context = new Context(session);
context.Set(new FromTo(dzien, dzien));               // parametr okresu wzorca zestawienia

var rr = new ReportResult {
    TemplateFileName = "ZestawienieSprzedazy.repx",  // wzorzec rejestru/zestawienia
    DataType = typeof(Soneta.Handel.DokHandlowe),    // wydruk dla zbioru dokumentów z widoku
    Context = context,
    OutputFormat = ReportFormats.PDF,
    AskForParameters = false
};

using (Stream pdf = raporty.GenerateReport(rr)) {
    // zapis / wysyłka
}

// --- Ścieżka 2: fiskalny raport okresowy (WYMAGA DRUKARKI FISKALNEJ) ---
// var fiskal = session.GetRequiredService<Soneta.Fiskal.IFiscalPrinterAPI>();
// var pars = new Soneta.Fiskal.RaportOkresowyParams(context) {
//     RaportZaOkres = new FromTo(new Date(2026, 6, 1), new Date(2026, 6, 30))
// };
// fiskal.DrukujRaportOkresowy("Posnet Thermal", pars);   // druk na sprzęcie
```

**Pułapki:**
- Rozróżnij dwie rzeczy o podobnej nazwie: **raport dobowy/okresowy drukarki fiskalnej**
  (`IFiscalPrinterAPI`, rozliczenie utargu na sprzęcie) vs. **bazodanowe zestawienie/rejestr**
  za dzień/okres (`IReportService` + wzorzec). Dodatek raportujący zwykle chce ścieżki 2.
- `RaportOkresowyParams.RaportZaOkres` jest `[Required]`; pusty `FromTo` resetuje się do dnia
  bieżącego, a otwarty zakres (`From == MinValue`/`To == MaxValue`) zwija się do jednego dnia.
- **Fiskalny raport (`DrukujRaport*`) wymaga podłączonej drukarki fiskalnej** — operacja
  sprzętowa, **nie do testów jednostkowych**. Testuj wyłącznie ustawienie `RaportOkresowyParams`
  i ścieżkę bazodanową `GenerateReport`.

---

### W65 — Wydruk zbiorczy dla zaznaczonego zbioru dokumentów

**Cel:** wygenerować jeden wydruk obejmujący wiele dokumentów naraz (np. seria faktur z
zaznaczenia listy) zamiast drukować każdy osobno.

**Warianty:**

| Wariant | `DataType` | Kontekst |
|---|---|---|
| Zaznaczone rekordy | `typeof(DokumentHandlowy[])` | `context.Set(tablica)` zaznaczonych dokumentów |
| Wszystkie z widoku | `typeof(DokHandlowe)` | rekordy dostarcza `View`/`ViewInfo` |
| Pojedynczy | `typeof(DokumentHandlowy)` | jeden rekord (W62) |

> `DataType` decyduje, które rekordy trafiają na wydruk: `typeof(T)` — jeden obiekt,
> `typeof(T[])` — zaznaczone, `typeof(Tabela)` — wszystkie z widoku.

**Pola i typy:** `ReportResult.DataType : Type`, `ReportResult.Rows : IEnumerable`
(jawne wskazanie rekordów do wydruku), `Context.Set(DokumentHandlowy[])`.

**Snippet:**

```csharp
using Microsoft.Extensions.DependencyInjection;
using Soneta.Business.UI;
using Soneta.Handel;

// 'zaznaczone' — tablica zatwierdzonych dokumentów do wydruku zbiorczego.
DokumentHandlowy[] zaznaczone = /* ... */;

var raporty = session.GetRequiredService<IReportService>();

var context = new Context(session);
context.Set(zaznaczone);                       // zbiór rekordów do wydruku

var rr = new ReportResult {
    TemplateFileName = "Sprzedaz.repx",
    DataType = typeof(DokumentHandlowy[]),     // wydruk dla ZAZNACZONYCH rekordów
    Rows = zaznaczone,                         // jawne wskazanie zbioru (opcjonalne)
    Context = context,
    OutputFormat = ReportFormats.PDF,
    AskForParameters = false
};

using (Stream pdf = raporty.GenerateReport(rr)) {
    // jeden strumień PDF z wieloma dokumentami
}
```

**Pułapki:**
- Kluczowa różnica vs W62 to **`DataType = typeof(DokumentHandlowy[])`** — typ tablicowy
  przełącza wzorzec w tryb wielu rekordów. Z `typeof(DokumentHandlowy)` wydrukuje się tylko
  pierwszy/bieżący dokument.
- `Rows` (`IEnumerable`) pozwala jawnie podać zbiór; pole **nie działa dla wydruków z menu**
  (tylko dla automatycznego trybu z `TemplateFileName`).
- Do wydruków masowych ustaw `AskForParameters = false` — inaczej każdy dokument mógłby
  wywołać okno parametrów.
- Wszystkie dokumenty w zbiorze powinny pasować do jednego wzorca (ten sam rodzaj/definicja).

---

### W66 — Zapis wydruku do strumienia/pliku (integracja, e-mail)

**Cel:** uzyskać wydruk jako strumień bajtów, bez drukowania — do zapisania w pliku,
dołączenia jako załącznik do e-maila, archiwizacji lub przesłania do zewnętrznego systemu.

**Warianty:**

| Wariant | Mechanizm |
|---|---|
| Do pliku / strumienia | `GenerateReport` → `Stream` → `FileStream`/`MemoryStream` |
| Wydruk tekstowy (HTML/TXT) | `GenerateReportStr` → `string` |
| Załącznik e-mail | `Target = ReportTargets.Email` lub strumień z `GenerateReport` jako załącznik |
| Z archiwizacją na druk | `PrintReport(rr, archive: true, archivePath: @"C:\Archiwum")` |
| Własna obsługa strumienia (tryb wzorca, **nie** `IReportService`) | `ReportResult.OutputHandler` jako rezultat operacji |

**Pola i typy:** `IReportService.GenerateReport(ReportResult) : Stream`,
`IReportService.GenerateReportStr(ReportResult) : string`,
`ReportResult.OutputFormat : ReportFormats`, `ReportResult.Target : ReportTargets`,
`ReportResult.Encrypt : string` (hasło PDF),
`ReportResult.OutputHandler : Func<Stream, object>` (tylko rezultat operacji UI).

**Snippet:**

```csharp
using Microsoft.Extensions.DependencyInjection;
using Soneta.Business.UI;
using Soneta.Handel;

var raporty = session.GetRequiredService<IReportService>();

var context = new Context(session);
context.Set(dok);
context.Set(new DokumentHandlowy[] { dok });
context.Set(new ParametryWydrukuDokumentu(context) { Duplikat = false });

var rr = new ReportResult {
    TemplateFileName = "Sprzedaz.repx",
    DataType = typeof(DokumentHandlowy),
    Context = context,
    OutputFormat = ReportFormats.PDF,
    Encrypt = "tajne-haslo",            // (opcjonalnie) PDF chroniony hasłem
    AskForParameters = false
};

// 1. Do pamięci — np. bajty do wysyłki e-mailem przez własny mechanizm:
byte[] pdfBytes;
using (Stream src = raporty.GenerateReport(rr))
using (var ms = new MemoryStream()) {
    src.CopyTo(ms);
    pdfBytes = ms.ToArray();
}
// pdfBytes → załącznik wiadomości, REST API, repozytorium dokumentów...

// 2. Wariant: niech mechanizm sam wyśle e-mail (rezultat operacji w workerze UI):
//    rr.Target = ReportTargets.Email;   // wymaga konfiguracji konta pocztowego i szablonu
```

**Pułapki:**
- `GenerateReport` to właściwa droga dla integracji — zwraca strumień, którym dysponujesz
  dowolnie (plik, e-mail, sieć). **Zawsze `using`** na zwróconym strumieniu (PDF i inne
  formaty binarne).
- `OutputHandler` **nie jest obsługiwany przez `IReportService`** (`CheckConsistency` rzuci
  `ArgumentException`). Służy jako rezultat operacji w trybie wzorca (worker/Command z UI),
  nie do wsadowego generowania w czystym kodzie biznesowym.
- `Target = Email`/`Attachment` to ścieżki integrujące się z modułem pocztowym (konto
  `KontoPocztowe`, szablon `SzablonEmail`) — wymagają pełnej, skonfigurowanej sesji
  aplikacyjnej; w czystym kodzie integracyjnym prościej pobrać strumień z `GenerateReport`
  i wysłać go własnym kanałem.
- Format dobieraj świadomie: `PDF`/`XLSX`/`PNG` → `GenerateReport` (`Stream`);
  `HTML`/`TXT` → `GenerateReportStr` (`string`).
- Szyfrowanie (`Encrypt`) i podpis (`Sign`) dotyczą PDF; podpis certyfikatem działa tylko
  w trybie interaktywnym okienkowym (wymaga okna certyfikatu).

---

> **Co jest testowalne, a co nie (sekcja 12):**
> - **Testowalne:** generowanie wydruku do strumienia/PDF/HTML/TXT przez
>   `IReportService.GenerateReport`/`GenerateReportStr` (W62, W63, W64-ścieżka bazodanowa,
>   W65, W66). Asercja: PDF zaczyna się od `"%PDF"`, HTML od `"<!DOCTYPE html"`.
> - **Nietestowalne jednostkowo (wymaga sprzętu):** druk na fizyczną drukarkę
>   (`PrintReport`, `Target = Printer`) oraz fiskalny raport dobowy/okresowy drukarki
>   (`IFiscalPrinterAPI.DrukujRaport`/`DrukujRaportOkresowy`, `Fiskalizuj`). Dla nich
>   testuj tylko poprawne ustawienie `ReportResult`/`RaportOkresowyParams`, bez faktycznego
>   druku.

---

## 13. Tematy specjalistyczne (KSeF, fiskalizacja, kompletacja, Intrastat)

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

### W67 — Wysłanie faktury do KSeF (pojedynczo i zbiorczo)

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
  `dok.ImportExportKSeF.XmlValidated == ThreeStateBoolean.True` (czyli wcześniej wykonaną walidację struktury — W69).

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
  rzuci wyjątek „nie posiada zweryfikowanego pliku XML". Najpierw wykonaj W69 (Sprawdź strukturę pliku) lub
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

### W68 — Sprawdzenie statusu KSeF i odczyt numeru KSeF

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

### W69 — UPO, numer KSeF z duplikatu, walidacja struktury XML

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

### W70 — Import faktur z KSeF (dokumenty zakupu)

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

### W71 — Fiskalizacja dokumentu (paragon fiskalny)

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

### W72 — E-paragon (e-mail) i ponowny wydruk paragonu

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

### W73 — Dokument kompletacji (złożenie / rozłożenie kompletu)

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

### W74 — Intrastat (dane statystyczne i wyszukanie dokumentów do deklaracji)

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

## 14. Płatności dokumentu handlowego

Płatności (należności i zobowiązania) powstają automatycznie z dokumentu handlowego płatnego (np. FV, FZ)
i opisują kwoty do uregulowania: termin, sposób zapłaty, ewidencję środków pieniężnych (ŚP) oraz stan
rozliczenia z zapłatami. Z poziomu dokumentu dostęp do nich daje kolekcja `dok.Platnosci`
(`SubTable<Soneta.Kasa.Platnosc>`). Pojedyncza płatność to obiekt `Soneta.Kasa.Platnosc` — w praktyce jedna
z dwóch klas konkretnych: `Naleznosc` (kierunek `Przychod`, sprzedaż) lub `Zobowiazanie` (kierunek
`Rozchod`, zakup). Wymagana referencja do `Soneta.Kasa`.

> **Pojęcia.** Kwota płatności (`Kwota: Currency`) jest w walucie dokumentu; `KwotaKsiegi: Currency` to jej
> przeliczenie na PLN po `Kurs`. Stan uregulowania to `StanRozliczenia` (+ `KwotaRozliczona`,
> `DoRozliczenia`). Płatności są edytowalne wyłącznie, gdy dokument (i sama płatność) są w **buforze** —
> po zatwierdzeniu pola płatności stają się tylko do odczytu.

---

### W75 — Przeglądanie płatności dokumentu

**Cel:** odczytać płatności wystawione z dokumentu — kwotę, walutę, sposób zapłaty, termin oraz stan
rozliczenia — bez modyfikacji.

**Warianty:**

| Wariant | Źródło / pole |
|---|---|
| Lista płatności dokumentu | `dok.Platnosci` (`SubTable<Platnosc>`) |
| Kwota i waluta | `p.Kwota: Currency` (`.Value`, `.Symbol`) |
| Sposób zapłaty | `p.SposobZaplaty: Soneta.Kasa.SposobZaplaty` (`.Nazwa`, `.Typ`, `.MPP`) |
| Termin płatności | `p.Termin: Date`, `p.TerminDni: int` (dni od daty odniesienia) |
| Stan rozliczenia | `p.StanRozliczenia`, `p.Rozliczono: bool`, `p.KwotaRozliczona`, `p.DoRozliczenia` |
| Kwota nierozliczona po terminie | `p.DoRozliczenia` + warunek `p.Termin < Date.Today` |
| Należność / zobowiązanie | `p.Kierunek`, `p.CzyNaleznosc: bool`, `p.CzyZobowiazanie: bool` |

**Pola i typy:** `Platnosc.Kwota: Soneta.Types.Currency`, `KwotaKsiegi: Currency` (PLN),
`SposobZaplaty: Soneta.Kasa.SposobZaplaty`, `Termin: Soneta.Types.Date`, `TerminDni: int`,
`StanRozliczenia: Soneta.Kasa.StanRozliczenia` (`Nierozliczony=0`, `Czesciowo=1`, `Calkowicie=2`,
`NiePodlega=3`), `Rozliczono: bool`, `KwotaRozliczona: Currency`, `DoRozliczenia: Currency`,
`Kierunek: Soneta.Kasa.KierunekPlatnosci`, `EwidencjaSP: Soneta.Kasa.EwidencjaSP`.

**Snippet:**

```csharp
var hm = session.GetHandel();
var dok = hm.DokHandlowe.WgDaty[...];        // lub inny lookup dokumentu

foreach (Platnosc p in dok.Platnosci)
{
    Currency kwota   = p.Kwota;                // w walucie dokumentu
    string waluta    = p.Kwota.Symbol;         // np. "PLN", "EUR"
    string sposob    = p.SposobZaplaty.Nazwa;  // np. "Przelew", "Gotówka"
    Date termin      = p.Termin;
    StanRozliczenia stan = p.StanRozliczenia;

    // Kwota pozostała do zapłaty i to, co już przeterminowane:
    Currency doZaplaty = p.DoRozliczenia;
    bool poTerminie    = !p.Rozliczono && p.Termin < Date.Today && p.DoRozliczenia > Currency.Zero;
}
```

**Pułapki:**
- `dok.Platnosci` to `SubTable` — iteruj serwerowo, nie materializuj do `List` tylko po to, by policzyć
  elementy (`IsEmpty`/`Count` są dostępne na kolekcji). Patrz [`rowcondition.md`](references/rowcondition.md).
- `StanRozliczenia.NiePodlega` oznacza płatność **nierozliczaną** (`p.Rozliczana == false`) — nie myl jej
  z `Nierozliczony` (rozliczana, ale jeszcze niezapłacona).
- `Kwota` jest w walucie dokumentu; do raportu w PLN użyj `KwotaKsiegi` (W81), nie mnóż „ręcznie".
- „Po terminie" liczysz z `Termin` i `DoRozliczenia` względem `Date.Today` — w samej płatności nie ma
  gotowego pola „kwota po terminie".

---

### W76 — Rozbicie płatności na raty

**Cel:** zamienić pojedynczą płatność dokumentu na zestaw rat (cyklicznych miesięcznych) albo na rozbicie
netto + VAT, przy użyciu publicznego workera `PodzialPlatnosciWorker`.

**Warianty:**

| Wariant | Ustawienie `WParams` |
|---|---|
| Raty miesięczne wg liczby rat | `Metoda = WOptions.Raty`, `IlośćRat = n` |
| Raty miesięczne wg kwoty raty | `Metoda = WOptions.Raty`, `Kwota = kwotaRaty` (worker wyliczy liczbę rat) |
| Rozbicie netto + VAT (MPP) | `Metoda = WOptions.NettoPlusVat` |

**Pola i typy:** worker `Soneta.Handel.PodzialPlatnosci.PodzialPlatnosciWorker`, parametry
`Soneta.Handel.PodzialPlatnosci.WParams : ContextBase` (inicjowane z `Context` zawierającego
`DokumentHandlowy`): `Metoda: WOptions` (`NettoPlusVat=0x1`, `Raty=0x2`), `IlośćRat: int`,
`Kwota: Currency` (kwota pojedynczej raty), `TerminPierwszejWpłaty: Date` (read-only — z warunków
płatności), `Cykl: WOptions` (`Miesięczny`). Akcja: `PodzielPlatnosci([Context] DokumentHandlowy)`.

**Snippet:**

```csharp
// Worker działa na dokumencie w BUFORZE z kierunkiem płatności (FV/FZ).
// Parametry tworzymy przez Context (wzorzec worker-z-Params), patrz worker-extender.md.
var context = new Context(session);
context.Set(dok);                              // DokumentHandlowy w kontekście

var wp = new PodzialPlatnosci.WParams(context)
{
    Metoda  = PodzialPlatnosci.WOptions.Raty,
    IlośćRat = 3,                              // 3 równe raty miesięczne
};

var worker = new PodzialPlatnosci.PodzialPlatnosciWorker(wp);
worker.PodzielPlatnosci(dok);                  // sam otwiera transakcję i robi CommitUI

session.Save();
```

**Pułapki:**
- Akcja jest dostępna tylko gdy `dok.Bufor == true` i `dok.Definicja.KierunekPlatnosci != Brak`
  (`IsVisiblePodzielPlatnosci`) — na zatwierdzonym dokumencie się nie wykona.
- `PodzielPlatnosci` **sam otwiera transakcję** (`Session.Logout(true)` + `CommitUI`) i **usuwa**
  istniejące płatności dokumentu, zastępując je wyliczonymi ratami/podziałem. Nie zawijaj go w drugą
  transakcję edycyjną; po nim wywołaj `session.Save()`.
- W trybie `Raty` ustawienie `Kwota` przelicza `IlośćRat` (i odwrotnie) — ustaw jedno z dwóch.
- Ostatnia rata przejmuje resztę z zaokrągleń (kwoty rat sumują się do `BruttoCy` dokumentu) — nie zakładaj
  równego podziału co do grosza.

---

### W77 — Ręczne dodanie / edycja pojedynczej płatności

**Cel:** ręcznie ułożyć płatności dokumentu — np. część gotówką, resztę przelewem — ustawiając sposób
zapłaty, ewidencję ŚP, termin i kwotę.

**Warianty:**

| Wariant | Operacja |
|---|---|
| Dodanie należności (sprzedaż) | `new Naleznosc(dok)` + `AddRow` |
| Dodanie zobowiązania (zakup) | `new Zobowiazanie(dok)` + `AddRow` |
| Edycja istniejącej | zmiana pól na elemencie `dok.Platnosci` |
| Częściowo gotówka + przelew | dwie płatności o różnym `SposobZaplaty`, suma `Kwota` = wartość dokumentu |

**Pola i typy:** konstruktory `Naleznosc(IDokumentPlatny)`, `Zobowiazanie(IDokumentPlatny)` (publiczne).
Tabela płatności: `KasaModule.GetInstance(session).Platnosci`. Pola zapisywalne:
`SposobZaplaty: SposobZaplaty`, `EwidencjaSP: EwidencjaSP`, `Termin: Date` (lub `TerminDni: int`),
`Kwota: Currency`, `KwotaMPP: Currency`, `Rachunek: RachunekBankowyPodmiotu`, `Priorytet: int`.

**Snippet:**

```csharp
var kasa = KasaModule.GetInstance(session);
var spZaplaty = kasa.SposobyZaplaty;

using (var t = session.Logout(editMode: true))   // dokument MUSI być w buforze
{
    // 1) część gotówką
    var gotowka = new Naleznosc(dok);             // sprzedaż -> Naleznosc; zakup -> Zobowiazanie
    kasa.Platnosci.AddRow(gotowka);
    gotowka.SposobZaplaty = spZaplaty.Gotówka;
    gotowka.Kwota  = new Currency(300m, "PLN");
    gotowka.Termin = dok.DataDokumentu;           // gotówka -> termin = data dokumentu

    // 2) reszta przelewem
    var przelew = new Naleznosc(dok);
    kasa.Platnosci.AddRow(przelew);
    przelew.SposobZaplaty = spZaplaty.WgNazwy["Przelew"];
    przelew.Kwota  = new Currency(dok.BruttoCy.Value - 300m, "PLN");
    przelew.TerminDni = 14;                        // 14 dni od daty odniesienia
    // przelew.Rachunek = ...                       // dla przelewu wskaż rachunek podmiotu

    t.Commit();                                     // CommitUI() w workerze/extenderze
}
session.Save();
```

**Pułapki:**
- Płatność można dodać **tylko do dokumentu w buforze** — `OnAdded` rzuca wyjątek
  („Nie można dodawać płatności do zatwierdzonego dokumentu"). `Platnosc.Bufor`/`IsReadOnly` chronią
  edycję po zatwierdzeniu.
- Dobierz klasę do kierunku dokumentu: sprzedaż (`KierunekPlatnosci.Przychod`) → `Naleznosc`, zakup
  (`Rozchod`) → `Zobowiazanie`. Zła klasa = niespójny kierunek.
- `Kwota` to `Currency` — twórz `new Currency(wartość, symbolWaluty)`; symbol musi być zgodny z walutą
  dokumentu/ewidencji (weryfikator ostrzega o niezgodności).
- Dla sposobu zapłaty typu „przelew" wymagany jest `Rachunek` (weryfikator-ostrzeżenie). Ustaw rachunek
  należący do podmiotu płatności (twardy weryfikator `RachunekPodmiotuVerifier`).
- `SposobZaplaty` pobieraj z tabeli (`kasa.SposobyZaplaty.Gotówka`, `...WgNazwy["Przelew"]`) — to rekord
  konfiguracyjny, nie ustawiaj „z palca".

---

### W78 — Warunki płatności z kontrahenta i ich przeliczenie na dokumencie

**Cel:** odczytać/ustawić warunki płatności dokumentu (sposób, termin w dniach, ewidencja ŚP) spójnie
z domyślnymi warunkami kontrahenta, przez publiczny `WarunkiPłatnościWorker`.

**Warianty:**

| Wariant | Mechanizm |
|---|---|
| Domyślne warunki z kontrahenta | `Kontrahent.SposobZaplaty`, `Kontrahent.Termin` (W9) — inicjują płatność |
| Odczyt warunków dokumentu | `WarunkiPłatnościWorker`: `Sposób`, `TerminDni`, `Termin`, `EwidencjaSP`, `Kwota`, `Raty` |
| Zmiana terminu (w dniach) | `worker.TerminDni = n` lub `worker.Termin = data` |
| Zmiana sposobu zapłaty | `worker.Sposób = ...` (przelicza też ewidencję ŚP) |
| Bezpośrednio na płatności | `p.TerminDni`, `p.Termin`, `p.SposobZaplaty`, `p.EwidencjaSP` |

**Pola i typy:** worker `Soneta.Kasa.WarunkiPłatnościWorker` (publiczny, zarejestrowany dla
`IDokumentPlatny`): `[Context] Dokument: IDokumentPlatny`, `TerminDni: int`, `Termin: Date`,
`Sposób: SposobZaplaty`, `EwidencjaSP: EwidencjaSP`, `Kwota: Currency` (read-only), `Raty: int`
(liczba płatności). Operuje na **pierwszej** płatności dokumentu. Na kontrahencie:
`Kontrahent.SposobZaplaty: FormaPlatnosci`, `Kontrahent.Termin: int` (patrz kontrahent W9).

**Snippet:**

```csharp
// Warunki płatności kontrahenta są przenoszone na płatność przy jej tworzeniu/zmianie podmiotu.
// Do odczytu/zmiany "zbiorczej" warunków dokumentu służy WarunkiPłatnościWorker:
var context = new Context(session);
context.Set(dok);                                  // dok : IDokumentPlatny (DokumentHandlowy)

var warunki = new WarunkiPłatnościWorker { Dokument = dok };

int dni        = warunki.TerminDni;                // termin liczony w dniach
SposobZaplaty sp = warunki.Sposób;
int liczbaRat  = warunki.Raty;

using (var t = session.Logout(editMode: true))     // dokument w buforze
{
    if (!warunki.IsReadOnlyTerminDni())
        warunki.TerminDni = 21;                    // przelicza Termin na pierwszej płatności
    if (!warunki.IsReadOnlySposób())
        warunki.Sposób = session.GetKasa().SposobyZaplaty.WgNazwy["Przelew"];
    t.Commit();
}
session.Save();
```

**Pułapki:**
- `WarunkiPłatnościWorker` działa na **pierwszej** płatności i tylko gdy `Raty <= 1` (jedna płatność);
  przy wielu płatnościach (`Raty > 1`) pola są read-only (`IsReadOnly...` zwracają `true`) — wtedy edytuj
  poszczególne płatności bezpośrednio (W77) albo użyj podziału (W76).
- `TerminDni` to dni od **daty odniesienia** (`TerminLiczonyOd`/data dokumentu), nie data bezwzględna —
  ustawienie `TerminDni` przelicza `Termin`.
- Edycja terminu może być zablokowana polityką (`IEdycjaTerminuPlatnosci`) — zawsze sprawdzaj
  `IsReadOnlyTermin()`/`IsReadOnlyTerminDni()` przed zapisem.
- Zmiana `Sposób` przelicza ewidencję ŚP (subewidencję) — nie ustawiaj `EwidencjaSP` „obok", licz na
  spójność workera.

---

### W79 — Zmiana płatnika (inny niż kontrahent)

**Cel:** ustawić na płatności podmiot inny niż kontrahent dokumentu (np. płatnik trzeci) i wykryć tę
sytuację z poziomu dokumentu.

**Warianty:**

| Wariant | Mechanizm |
|---|---|
| Zmiana płatnika płatności | `p.Podmiot = innyPodmiot` (`IPodmiotKasowy`) |
| Wykrycie „innego płatnika" | `dok.InnyPłatnik: bool` (read-only — `true`, gdy jakaś płatność ma `Podmiot != Kontrahent`) |
| Płatnik domyślny kontrahenta | `Kontrahent.Platnik: IPodmiotKasowy` (kalkulowane — nadrzędny z relacji) |

**Pola i typy:** `Platnosc.Podmiot: Soneta.Kasa.IPodmiotKasowy` (zapisywalne),
`DokumentHandlowy.InnyPłatnik: bool` (**kalkulowane, read-only**),
`IsReadOnlyPodmiot()`. `Kontrahent` implementuje `IPodmiotKasowy`.

**Snippet:**

```csharp
// "Inny płatnik" ustawiamy na poziomie POJEDYNCZEJ płatności — pole Podmiot:
IPodmiotKasowy platnik = session.GetCRM().Kontrahenci.WgKodu["PLATNIK"];

using (var t = session.Logout(editMode: true))     // dokument w buforze
{
    foreach (Platnosc p in dok.Platnosci)
        if (!p.IsReadOnlyPodmiot())
            p.Podmiot = platnik;                    // rozrachunek przejdzie na nowy podmiot
    t.Commit();
}
session.Save();

// Odczyt: czy dokument ma płatnika innego niż kontrahent:
bool inny = dok.InnyPłatnik;                        // kalkulowane, tylko do odczytu
```

**Pułapki:**
- `dok.InnyPłatnik` jest **wyłącznie do odczytu** — to flaga wyliczana z porównania `p.Podmiot` z
  `dok.Kontrahent`. Aby „zmienić płatnika", ustaw `Platnosc.Podmiot`, nie próbuj przypisać `InnyPłatnik`.
- `Podmiot` jest read-only, gdy płatność jest częściowo rozliczona (`KwotaRozliczona != 0`) — sprawdzaj
  `IsReadOnlyPodmiot()`.
- Zmiana podmiotu przenosi rozrachunek na nowy podmiot i może podmienić zablokowany podmiot na jego
  zamiennik (wbudowana logika) — odczytaj `p.Podmiot` po zmianie, nie zakładaj wartości wejściowej.
- `Rachunek` musi należeć do nowego `Podmiot` (twardy weryfikator) — po zmianie płatnika zweryfikuj/wyczyść
  rachunek.

---

### W80 — Odczyt stanu rozliczenia płatności

**Cel:** ustalić, czy płatność jest rozliczona w całości, częściowo czy nierozliczona, oraz dotrzeć do
powiązanych rozliczeń (zapłat).

**Warianty:**

| Wariant | Pole / kolekcja |
|---|---|
| Stan zbiorczy | `p.StanRozliczenia` (`Nierozliczony`/`Czesciowo`/`Calkowicie`/`NiePodlega`) |
| Rozliczono całkowicie? | `p.Rozliczono: bool`, `p.Zrealizowane: bool` |
| Kwoty | `p.KwotaRozliczona`, `p.DoRozliczenia` |
| Data rozliczenia | `p.DataRozliczenia: Date` (`Date.MaxValue` = nierozliczona) |
| Rozliczono na dzień | `p.RozliczonoDoDnia(Date data)` |
| Powiązane rozliczenia/transakcje | `p.Dokumenty`, `p.Zaplaty` (kolekcje `RozliczenieSP`) |
| Czy podlega rozliczeniu | `p.Rozliczana: bool` |

**Pola i typy:** `StanRozliczenia: Soneta.Kasa.StanRozliczenia`, `Rozliczono: bool`, `Zrealizowane: bool`,
`KwotaRozliczona/DoRozliczenia: Currency`, `DataRozliczenia: Date`, `Rozliczana: bool`,
`Dokumenty`/`Zaplaty` (rozliczenia typu `Soneta.Kasa.RozliczenieSP`),
metoda `RozliczonoDoDnia(Date, bool wgDatyKsi = false): Currency`.

**Snippet:**

```csharp
foreach (Platnosc p in dok.Platnosci)
{
    switch (p.StanRozliczenia)
    {
        case StanRozliczenia.Calkowicie: /* zapłacona w całości */ break;
        case StanRozliczenia.Czesciowo:  /* część zapłacona: p.DoRozliczenia > 0 */ break;
        case StanRozliczenia.Nierozliczony: /* brak zapłat */ break;
        case StanRozliczenia.NiePodlega:    /* płatność nierozliczana */ break;
    }

    Currency zaplaconoDoDzis = p.RozliczonoDoDnia(Date.Today);

    // Powiązane rozliczenia (transakcje zapłaty):
    foreach (RozliczenieSP r in p.Zaplaty) { /* r.Data, r.KwotaDokumentu, ... */ }
    foreach (RozliczenieSP r in p.Dokumenty) { /* r.Data, r.KwotaZaplaty, ... */ }
}
```

**Pułapki:**
- `StanRozliczenia` jest kalkulowane z `KwotaRozliczona`/`Kwota` — nie ustawiaj go; rozliczenia powstają
  przez operacje kasowe/rozliczeniowe, nie przez bezpośredni zapis na płatności.
- `DataRozliczenia == Date.MaxValue` oznacza „nierozliczona" — nie traktuj `MaxValue` jako realnej daty.
- Rozliczenia są rozdzielone na dwie kolekcje (`Dokumenty` i `Zaplaty`) zależnie od strony powiązania —
  do pełnego obrazu przejrzyj obie.
- Dla płatności `Rozliczana == false` (`NiePodlega`) `DoRozliczenia` wynosi zero — nie analizuj jej jak
  zaległości.

---

### W81 — Płatności w walucie obcej (kwota w walucie vs PLN, kurs)

**Cel:** poprawnie odczytać/ustawić płatność walutową — kwotę w walucie obcej, jej przeliczenie na PLN
oraz kurs i tabelę kursową.

**Warianty:**

| Wariant | Pole |
|---|---|
| Kwota w walucie dokumentu | `p.Kwota: Currency` (symbol = waluta, np. „EUR") |
| Kwota w PLN (księgowa) | `p.KwotaKsiegi: Currency` |
| Kurs i tabela | `p.Kurs: double`, `p.TabelaKursowa: TabelaKursowa` |
| Interfejs walutowy | `IRowWithKurs`: `KwotaWaluty` (= `Kwota`), `KwotaPLN` (= `KwotaKsiegi`) |
| Słownie | `p.Słownie: string` |

**Pola i typy:** `Kwota: Currency` (waluta dokumentu), `KwotaKsiegi: Currency` (PLN),
`Kurs: double`, `TabelaKursowa: Soneta.Waluty.TabelaKursowa`. `Platnosc` implementuje
`Soneta.Waluty.IRowWithKurs` (`KwotaWaluty`, `KwotaPLN`).

**Snippet:**

```csharp
foreach (Platnosc p in dok.Platnosci)
{
    if (p.Kwota.Symbol != Currency.SystemSymbol)   // płatność walutowa (np. "EUR")
    {
        Currency wWalucie = p.Kwota;               // np. 1000 EUR
        Currency wPln     = p.KwotaKsiegi;         // przeliczenie na PLN
        double kurs       = p.Kurs;                // kurs zastosowany
        TabelaKursowa tab = p.TabelaKursowa;       // tabela kursów (lub null)
    }
}

// Ustawienie kursu ręcznie (gdy dokument/ewidencja walutowa, w buforze):
using (var t = session.Logout(editMode: true))
{
    foreach (Platnosc p in dok.Platnosci)
        if (p.Kwota.Symbol != Currency.SystemSymbol && !p.IsReadOnlyTabelaKursowa())
            p.TabelaKursowa = session.GetKasa().EwidencjeSP /* ... */ ?.TabelaKursowa;
    t.Commit();
}
session.Save();
```

**Pułapki:**
- Dla płatności w PLN `Kurs == 1.0` i `TabelaKursowa == null` — przeliczeniem zajmuj się tylko, gdy
  `Kwota.Symbol != Currency.SystemSymbol`.
- `KwotaKsiegi` wylicza się z `Kwota * Kurs`; jeśli ustawisz tabelę bez kursu na datę dokumentu, kurs może
  pozostać `0.0` (brak kursu) — wtedy `KwotaKsiegi` będzie zerowa. Upewnij się, że tabela kursowa ma kurs
  na `DataDokumentu` (w bazie Demo brak kursów „na dziś" → operacja walutowa rzuca
  `KursWalutyNotFoundException`, por. rozdz. o walutach).
- Kwota płatności walutowej musi mieć symbol zgodny z walutą dokumentu/ewidencji ŚP — weryfikator ostrzega
  o niezgodności symboli.
- Sumę płatności w PLN czytaj z `KwotaKsiegi` (lub `IRowWithKurs.KwotaPLN`), nie przeliczaj `Kwota` własnym
  kursem.

---

### W82 — Powiązanie płatności z terminem i rabatem za wcześniejszą zapłatę

**Cel:** obsłużyć rabat za wcześniejszą zapłatę (skonto) — wskazać termin uprawniający do rabatu i odczytać
jego wpływ na warunki płatności dokumentu.

**Warianty:**

| Wariant | Mechanizm |
|---|---|
| Ustawienie terminu rabatu na dokumencie | `dok.RabatZaTerminPlatnosci.Termin = data` |
| Odczyt naliczonego rabatu | `dok.RabatZaTerminPlatnosci.Rabat: Percent` |
| Rodzaj rabatu | `dok.RabatZaTerminPlatnosci.Rodzaj: RodzajRabatuZaTerminPlatnosci` |
| Termin samej płatności | `p.Termin`, `p.TerminDni` (W77/W78) |
| Parametry rabatu na kontrahencie | `Kontrahent.RodzajRabatuZaTerminPlatnosci`, `TrybRabatu...`, `IloscDniDlaRabatu`, `WartoscRabatuZaKazdyDzien` |

**Pola i typy:** `DokumentHandlowy.RabatZaTerminPlatnosci: Soneta.Handel.RabatZaTerminPlatnosci`
(subrow) z polami `Termin: Date` (zapisywalne — termin uprawniający do rabatu), `Rabat: Percent`
(wyliczane), `Rodzaj: RodzajRabatuZaTerminPlatnosci`. Na płatności: `Termin: Date`,
`TerminDni: int`, `TerminLiczonyOd: Date` (data odniesienia, read-only).

**Snippet:**

```csharp
using (var t = session.Logout(editMode: true))     // dokument w buforze, z kontrahentem
{
    // Termin uprawniający do rabatu za wcześniejszą zapłatę (skonto):
    if (!dok.RabatZaTerminPlatnosci.IsReadOnlyTermin())
        dok.RabatZaTerminPlatnosci.Termin = dok.DataDokumentu.AddDays(7);
    t.Commit();
}
session.Save();

// Odczyt naliczonego rabatu (zależny od parametrów rabatu kontrahenta):
Percent rabat = dok.RabatZaTerminPlatnosci.Rabat;
Date terminRabatu = dok.RabatZaTerminPlatnosci.Termin;
```

**Pułapki:**
- `RabatZaTerminPlatnosci.Rabat` jest **wyliczany** z parametrów kontrahenta (tryb: progresywny /
  podstawowy / progowy) i różnicy dni między `Termin` rabatu a terminem płatności — nie ustawiaj go wprost.
- Ustawienie `Termin` < `Date.Today` zeruje rabat i czyści termin — przekazuj datę przyszłą.
- Termin rabatu można ustawić tylko, gdy **wszystkie** płatności dokumentu mają ten sam termin
  (`Dokument.Platnosci` zgrupowane po `Termin` → jedna grupa); w przeciwnym razie rzuca `RowException`.
- Edycja może być zablokowana polityką `IEdycjaTerminuPlatnosci` — sprawdzaj `IsReadOnlyTermin()`.
- Naliczenie rabatu wymaga skonfigurowanych parametrów na kontrahencie
  (`RodzajRabatuZaTerminPlatnosci`, `Tryb...`, progi/wartości) — bez nich `Rabat` pozostanie `Percent.Zero`.

---

## Powiązane dokumenty

- [`safe-code.md`](../safe-code.md) — sesja, transakcje, blokada optymistyczna, zasady bezpiecznego kodu.
- [`session-login.md`](../session-login.md) — `Session`, `Login`, `Database`.
- [`worker-extender.md`](../worker-extender.md) — workery, akcje menu Czynności, bindowanie.
- [`rowcondition.md`](../rowcondition.md) — serwerowy LINQ, `RowCondition`, `SubTable[condition]`.
- [`features.md`](../features.md) — cechy (`Features`), typy, dostęp typowany/nietypowany.
- [`datapack-guidedrow.md`](../datapack-guidedrow.md) — eksport/import, `GuidedRow`.
- [`kontrahent.md`](kontrahent.md) — receptury dla `Kontrahent` (nabywca/odbiorca/płatnik dokumentu).
- [`scan-props.md`](../scan-props.md) / [`scan-workers.md`](../scan-workers.md) — inwentaryzacja pól i workerów.
