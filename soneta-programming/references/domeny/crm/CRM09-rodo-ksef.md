# CRM09 — RODO/GIODO i KSeF

> Wspólne fakty o typie, podstawowe typy i szablon wzorca: [../crm.md](../crm.md).

### CRM-W16 — RODO / GIODO

**Cel:** obsłużyć zgody i wymianę danych osobowych kontrahenta.

**Warianty:**

| Wariant | Mechanizm / worker | Kolekcja |
|---|---|---|
| Oświadczenia | `KontrahentDodajOswiadczeniaWorker` | `GIODOOświadczenia: SubTable<GIODOOświadczenie>` |
| Pozyskanie danych | `KontrahentDodajPozyskanieDanychWorker` | `GIODOUdostępnienia` |
| Udostępnienie danych | `KontrahentDodajUdostepnienieDanychWorker` | `GIODOUdostępnienia` |
| Powierzenie danych | `KontrahentDodajPowierzenieDanychWorker` | `GIODOUdostępnienia` |
| Potwierdzenia zgodności | — | `PotwierdzeniaGIODO: SubTable<GIODOZgodny>` |

**Pola i typy:** `GIODOOświadczenia: SubTable<GIODOOświadczenie>`,
`GIODOUdostępnienia: SubTable<GIODOWymianaDanych>`, `PotwierdzeniaGIODO: SubTable<GIODOZgodny>`,
`ZgodnoscGIODOPotwierdzona: bool` (kalkulowane).

**Snippet:**

```csharp
var k = session.GetCRM().Kontrahenci.WgKodu["FIRMA001"];

// Odczyt oświadczeń RODO kontrahenta:
foreach (GIODOOświadczenie o in k.GIODOOświadczenia)
{
    // o.* — definicja oświadczenia, okres obowiązywania, status zgody
}

// Dodawanie oświadczeń realizują workery RODO (dziedziczą po bazowych z Soneta.Core):
// new KontrahentDodajOswiadczeniaWorker(...).DodajOświadczenia();
```

**Pułapki:**
- Obowiązywanie zgody jest „na dzień" — czytaj okresy z rekordów `GIODOOświadczenie`, nie zakładaj
  bezterminowości.
- Dane osobowe (PESEL, e-mail osób) są wrażliwe — nie loguj ich (safe-code §12).
- Workery RODO mają tryb `ConfirmSave` i wymagają praw do obszaru GIODO.

### CRM-W17 — KSeF

**Cel:** ustawić parametry KSeF kontrahenta.

**Warianty:**

| Wariant | Pole |
|---|---|
| Szablon pól opcjonalnych | `DomyslnySzablonPolOpcjonalnychKSeF: Soneta.Core.KSeFSzablonPolOpcjonalnych` |
| Sposób wysyłki ceny | `KSeFSposobObslugiWysylkiCeny: Soneta.Core.SposobObslugiWysylkiCenyDoKSeF` |
| Powiązanie z e-fakturą | `EFaktura`, `EFakturaOkres` (patrz CRM-W7) |

**Pola i typy:** jak w tabeli powyżej (oba pola bazodanowe, zapisywalne).

**Snippet:**

```csharp
var k = session.GetCRM().Kontrahenci.WgKodu["FIRMA001"];

using (var t = session.Logout(editMode: true))
{
    k.KSeFSposobObslugiWysylkiCeny = SposobObslugiWysylkiCenyDoKSeF.CenaPoRabacie;
    // k.DomyslnySzablonPolOpcjonalnychKSeF = ... // rekord szablonu z konfiguracji
    t.Commit();
}
session.Save();
```

**Pułapki:**
- `DomyslnySzablonPolOpcjonalnychKSeF` to referencja do rekordu konfiguracyjnego — pobierz istniejący
  szablon, nie twórz „w locie".
- Konfiguracja KSeF współgra z `EFaktura` (CRM-W7) — ustawiaj je spójnie.

---

