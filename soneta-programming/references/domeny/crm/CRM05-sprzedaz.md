# CRM05 — Sprzedaż i dokumenty

> Wspólne fakty o typie, podstawowe typy i szablon wzorca: [../crm.md](../crm.md).

### CRM-W12 — Dokumenty i dane sprzedażowe

**Cel:** odczytać dokumenty handlowe kontrahenta oraz (opcjonalnie) utworzyć dokument.

**Warianty:**

| Wariant | Źródło / worker |
|---|---|
| Dokumenty, w których kontrahent jest nabywcą | `DokumentyHandlowe: SubTable` |
| Dokumenty, w których jest odbiorcą | `DokumentyHandloweOdbiorcy: SubTable` |
| Dokumenty ewidencji | `DokumentyEwidencji: SubTable<DokEwidencji>` |
| Utworzenie dokumentu | przez moduł `Handel` (definicja dokumentu + ustawienie `Kontrahent`) |

**Pola i typy:** `DokumentyHandlowe`, `DokumentyHandloweOdbiorcy`, `DokumentyEwidencji` — kolekcje
`SubTable` na `Kontrahent`.

**Snippet:**

```csharp
var k = session.GetCRM().Kontrahenci.WgKodu["FIRMA001"];

// Ostatnie dokumenty handlowe kontrahenta jako nabywcy:
foreach (var d in k.DokumentyHandlowe)
{
    // d.* — numer, data, wartości
}
```

**Pułapki:**
- Tworzenie dokumentu handlowego realizuje moduł `Handel` (definicja `DefDokHandlowych`,
  `new DokumentHandlowy`, ustawienie `Kontrahent`) — to osobny obszar; z poziomu kontrahenta
  korzystaj z jego kolekcji do odczytu.
- `DokHandlowe` to tabela **operacyjna guided** — przy iteracji poprzecznej zawężaj zakres czasowy
  (safe-code §6.3). Kolekcja `k.DokumentyHandlowe` jest już zawężona do jednego kontrahenta.

---

