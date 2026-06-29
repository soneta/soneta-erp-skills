# Kontrahent — receptury kodu biznesowego (platforma Soneta)

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

Każdy wzorzec (`CRM-Wn`) ma stałą strukturę:

- **Cel** — co robi i kiedy go użyć.
- **Warianty** — tabela odmian przypadku.
- **Pola i typy** — realne właściwości/kolekcje i ich typy.
- **Snippet** — kod C# 10.
- **Pułapki** — typowe błędy i zasady safe-code.

---


## Mapa receptur

| Rozdział | Plik | Receptury |
|---|---|---|
| CRM01 — Wyszukiwanie i identyfikacja | [crm/CRM01-wyszukiwanie.md](crm/CRM01-wyszukiwanie.md) | CRM-W1–W2 |
| CRM02 — Tworzenie, modyfikacja, usuwanie | [crm/CRM02-tworzenie.md](crm/CRM02-tworzenie.md) | CRM-W3–W5 |
| CRM03 — Adres, kontakt, osoby | [crm/CRM03-adres-kontakt.md](crm/CRM03-adres-kontakt.md) | CRM-W6–W8 |
| CRM04 — Warunki handlowe i finanse | [crm/CRM04-warunki-finanse.md](crm/CRM04-warunki-finanse.md) | CRM-W9–W11 |
| CRM05 — Sprzedaż i dokumenty | [crm/CRM05-sprzedaz.md](crm/CRM05-sprzedaz.md) | CRM-W12 |
| CRM06 — Klasyfikacja | [crm/CRM06-klasyfikacja.md](crm/CRM06-klasyfikacja.md) | CRM-W13 |
| CRM07 — Powiązania | [crm/CRM07-powiazania.md](crm/CRM07-powiazania.md) | CRM-W14 |
| CRM08 — Weryfikacja statusu | [crm/CRM08-weryfikacja-statusu.md](crm/CRM08-weryfikacja-statusu.md) | CRM-W15 |
| CRM09 — RODO/GIODO i KSeF | [crm/CRM09-rodo-ksef.md](crm/CRM09-rodo-ksef.md) | CRM-W16–W17 |
| CRM10 — Operacje masowe | [crm/CRM10-operacje-masowe.md](crm/CRM10-operacje-masowe.md) | CRM-W18 |

## Powiązane dokumenty

- [`safe-code.md`](../safe-code.md) — sesja, transakcje, blokada optymistyczna, zasady bezpiecznego kodu.
- [`session-login.md`](../session-login.md) — `Session`, `Login`, `Database`.
- [`worker-extender.md`](../worker-extender.md) — workery, akcje menu Czynności, bindowanie.
- [`rowcondition.md`](../rowcondition.md) — serwerowy LINQ, `RowCondition`, `SubTable[condition]`.
- [`datapack-guidedrow.md`](../datapack-guidedrow.md) — eksport/import, `GuidedRow`.
- [`scan-props.md`](../scan-props.md) / [`scan-workers.md`](../scan-workers.md) — inwentaryzacja pól i workerów.
