# CRM10 — Operacje masowe

> Wspólne fakty o typie, podstawowe typy i szablon wzorca: [../crm.md](../crm.md).

### CRM-W18 — Operacje na zbiorze kontrahentów

**Cel:** wykonać operację na wielu kontrahentach efektywnie i bezpiecznie.

**Warianty:**

| Wariant | Mechanizm |
|---|---|
| Iteracja z warunkiem | serwerowy LINQ `crm.Kontrahenci[(Kontrahent k) => …]` (patrz [`rowcondition.md`](../../rowcondition.md)) |
| Masowa aktualizacja | jedna transakcja, paczki (patrz [`safe-code.md`](../../safe-code.md)) |
| Masowa zmiana formy prawnej | worker `ZmienFormePrawnaKontrahentowWorker` |
| Masowe przypisanie kategorii | worker `KontrahenciPrzypiszKategorieWorker` |
| Masowa weryfikacja VAT/VIES (online) | `KontrahenciBialaListaWorker`, `KontrahenciDaneZMfWorker`, `KontrahenciDaneZViesWorker` |
| Eksport / import | datapack / business.xml (patrz [`datapack-guidedrow.md`](../../datapack-guidedrow.md)) |

**Snippet:**

```csharp
var crm = session.GetCRM();

// Masowa zmiana: ustaw blokadę sprzedaży dla kontrahentów bez NIP — filtr serwerowy + 1 transakcja
using (var t = session.Logout(editMode: true))
{
    foreach (Kontrahent k in crm.Kontrahenci.WgKodu[(Kontrahent k) =>
                 k.NIP == null && k.StatusPodmiotu == StatusPodmiotu.PodmiotGospodarczy])
    {
        k.BlokadaSprzedazy = true;
    }
    t.Commit();
}
session.Save();
```

**Pułapki:**
- **Nie ładuj całej tabeli** do pamięci — filtr serwerowy (`SubTable[condition]`).
- Duże operacje dziel na **paczki** (krótkie transakcje), by nie blokować innych użytkowników i nie
  zwiększać ryzyka konfliktu optymistycznego (safe-code §13.1).
- Workery masowe (`*Worker` na typie `Kontrahenci`) mają property `[Context] Kontrahent[]` —
  przy użyciu programowym ustaw tablicę zaznaczonych rekordów.

---

