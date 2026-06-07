# CRM07 — Powiązania

> Wspólne fakty o typie, podstawowe typy i szablon wzorca: [../crm.md](../crm.md).

### CRM-W14 — Powiązania i opiekunowie

**Cel:** zarządzać opiekunami i relacjami między kontrahentami.

**Warianty:**

| Wariant | Operacja / worker |
|---|---|
| Opiekun (dodanie / główny) | `Opiekunowie: SubTable<Opiekun>`, worker `UstawOpiekunaGlownegoWorker` |
| Sprawdzenie opieki na dzień | metody `JestOpiekunemNaDzis(...)`, `OpiekunowieWOkresie(...)` |
| Podmiot nadrzędny / podrzędny | workery `NowyPodmiotNadrzednyWorker`, `NowyPodmiotPodrzednyWorker` |
| Relacje podmiotów | `Podrzedni: SubTable<RelacjaPodmiotu>`, `PodmiotNadrzedny: IPodmiot` |
| Połącz / rozłącz | workery `PolaczKontrahentowWorker`, `RozlaczKontrahentaWorker` |

**Pola i typy (`Opiekun`):** `Kontrahent: Kontrahent`, `Operator: Operator`, `Typ: TypOpiekuna`
(`Glówny=0`, `Zastępca=1`), `Rola: RolaOpiekun`, `OddzialFirmy`, `DataOd: Date`, `DataDo: Date`,
`Aktywny: bool`.

**Snippet:**

```csharp
var crm = session.GetCRM();
var k = crm.Kontrahenci.WgKodu["FIRMA001"];

using (var t = session.Logout(editMode: true))
{
    var op = new Opiekun();
    crm.Opiekunowie.AddRow(op);
    op.Kontrahent = k;
    op.Operator = oper;                        // Operator pobrany z modułu Business
    op.Typ = TypOpiekuna.Glówny;
    op.DataOd = Date.Today;
    op.DataDo = Date.MaxValue;
    op.Aktywny = true;
    t.Commit();
}
session.Save();

// Odczyt relacji podmiotów:
foreach (RelacjaPodmiotu r in k.Podrzedni)
{
    // r.Nadrzedny, r.PowiazaniePodmiotu.Rola, r.PowiazaniePodmiotu.RodzajPowiazania
}
IPodmiot nadrzedny = k.PodmiotNadrzedny;
```

**Pułapki:**
- `Opiekun.Operator` to rekord operatora (dane konfiguracyjne) — w kodzie biznesowym pobieraj go
  spójnie z bieżącą sesją; nie mieszaj rekordów z różnych sesji (safe-code §2.1, użyj `session.Get(...)`).
- Do sprawdzania opieki „na dziś"/„w okresie" używaj metod publicznych `JestOpiekunemNaDzis`,
  `OpiekunowieWOkresie` zamiast ręcznego filtrowania dat.
- Relacje podmiotów (nadrzędny/podrzędny, płatnik/odbiorca) zakładaj workerami
  `NowyPodmiotNadrzednyWorker`/`NowyPodmiotPodrzednyWorker` — mają walidatory spójności.

---

