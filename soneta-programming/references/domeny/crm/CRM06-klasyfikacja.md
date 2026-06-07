# CRM06 — Klasyfikacja

> Wspólne fakty o typie, podstawowe typy i szablon wzorca: [../crm.md](../crm.md).

### CRM-W13 — Cechy, kategorie, branże, GUS

**Cel:** uzupełnić cechy definiowalne i klasyfikacje kontrahenta.

**Warianty:**

| Wariant | Kolekcja / mechanizm |
|---|---|
| Cecha definiowalna | `Features: FeatureCollection` (odczyt/zapis po nazwie definicji) |
| Kategorie | `Kategorie: SubTable<KategoriaKth>` (poj. lub worker `KontrahenciPrzypiszKategorieWorker`) |
| Branże | `Branze: SubTable<BranzaKth>` |
| PKD / dane GUS | worker `DaneZGusBirWorker` (online; pobiera też kody PKD) |

**Pola i typy:** `Features: Soneta.Business.FeatureCollection` (indeksator `Features["NazwaCechy"]`
zwraca/przyjmuje `object`), `Kategorie: SubTable<KategoriaKth>`, `Branze: SubTable<BranzaKth>`.
`KategoriaKth` tworzymy konstruktorem `new KategoriaKth(kontrahent, defKategorii)`.

**Snippet:**

```csharp
var crm = session.GetCRM();
var k = crm.Kontrahenci.WgKodu["FIRMA001"];

using (var t = session.Logout(editMode: true))
{
    // Cecha definiowalna — dostęp po nazwie definicji (cecha musi być wcześniej zdefiniowana):
    k.Features["Segment"] = "Premium";

    // Przypisanie do kategorii (defKat: DefKategKth z konfiguracji CRM, indeks WgNazwy):
    var defKat = crm.DefKategoriiKth.WgNazwy["VIP"];
    if (defKat != null && crm.KategorieKth.WgKontrahent[k, defKat] == null)
        crm.KategorieKth.AddRow(new KategoriaKth(k, defKat));
    t.Commit();
}
session.Save();

// Odczyt cechy:
object segment = k.Features["Segment"];
```

**Pułapki:**
- Cecha jest dostępna **po nazwie definicji**; odwołanie do niezdefiniowanej cechy rzuca wyjątek —
  upewnij się, że definicja istnieje (cechy vs pola natywne to dwie różne rzeczy).
- Przed dodaniem kategorii sprawdź duplikat: `crm.KategorieKth.WgKontrahent[k, defKat]`.
- Masowe przypisanie kategorii: worker `KontrahenciPrzypiszKategorieWorker` (`[Context] Kontrahent[]`
  + `Params.Kategoria`).
- Pobranie kodów PKD odbywa się **online** z GUS-BIR (worker `DaneZGusBirWorker`) — patrz CRM-W15.

---

