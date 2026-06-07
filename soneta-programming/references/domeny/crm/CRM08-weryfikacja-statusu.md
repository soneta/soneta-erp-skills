# CRM08 — Weryfikacja statusu

> Wspólne fakty o typie, podstawowe typy i szablon wzorca: [../crm.md](../crm.md).

### CRM-W15 — Weryfikacja VAT (GUS / MF / VIES)

**Cel:** zweryfikować dane i status podatnika w rejestrach zewnętrznych. **Wszystkie operacje są
online** (wymagają połączenia i bywają limitowane).

**Warianty:**

| Wariant | Worker (jednostkowo / masowo) | Wynik na kontrahencie |
|---|---|---|
| Dane z GUS-BIR (też PKD) | `DaneZGusBirWorker` / `DaneZGusBirMultipleWorker` | nazwa, adres, REGON, KRS, PKD |
| Status MF / biała lista | `DaneZMfWorker`, `KontrahentBialaListaWorker` / `KontrahenciBialaListaWorker` | `AktualnyStatusVATMF` |
| Status VIES | `DataFromViesWorker` / `KontrahenciDaneZViesWorker` | `AktualnyStatusVATVies` |
| Historia statusu VAT | kolekcja `StatusyVAT: SubTable<StatusVAT>` | — |

**Pola i typy (odczyt wyniku):** `AktualnyStatusVAT`, `AktualnyStatusVATMF`, `AktualnyStatusVATVies`
(typ `Soneta.CRM.StatusNumeruVAT`, kalkulowane), `AktStatusVATData/DataMF/DataVIES: DateTime?`,
`StatusyVAT: SubTable<StatusVAT>`.

**Snippet:**

```csharp
var k = session.GetCRM().Kontrahenci.WgKodu["FIRMA001"];

// Odczyt ostatnio zapisanych statusów (offline — bez sieci):
StatusNumeruVAT statusMF   = k.AktualnyStatusVATMF;
StatusNumeruVAT statusVies = k.AktualnyStatusVATVies;
DateTime? dataMF           = k.AktStatusVATDataMF;

// Historia statusów:
foreach (StatusVAT s in k.StatusyVAT) { /* ... */ }

// Weryfikacja online — przez worker (przykład: status MF):
// var w = new DaneZMfWorker { Kontrahent = k, Context = context };
// w.DaneZMf();   // WYMAGA SIECI — obuduj obsługą braku połączenia/limitów
```

**Pułapki:**
- Operacje GUS/MF/VIES **wymagają sieci** — obuduj je obsługą błędów połączenia i limitów; **nie
  testuj ich w testach jednostkowych** (zależność od usług zewnętrznych).
- Status VAT z rejestru to dane „na dzień" — zapisuj datę weryfikacji (`AktStatusVATData*`).
- W kodzie offline czytaj wyłącznie pola kalkulowane (`AktualnyStatusVAT*`) i historię `StatusyVAT`.
- Nie loguj nadmiarowo numerów NIP/PESEL (safe-code §12).

---

