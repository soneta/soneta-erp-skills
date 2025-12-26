# Soneta ORM AI Skills

Zestaw skills dla asystentów AI (Claude, Cursor, Windsurf, itp.) wspierających programowanie z ORM platformy **enova365/Soneta Enterprise**.

## 🎯 Cel projektu

Skille zawierają dokumentację i wzorce programistyczne, które pozwalają asystentom AI efektywnie pomagać w tworzeniu dodatków dla systemu enova365. Dzięki nim AI rozumie specyfikę platformy Soneta: mapowanie obiektowo-relacyjne, strukturę modułów, zarządzanie sesjami i transakcjami.

## 📦 Dostępne skille

### 1. soneta-programming-basics

Fundamentalne klasy ORM platformy enova365/Soneta Enterprise.

**Zakres:**
- Mapowanie obiektowo-relacyjne (`Row`, `Table`, `Module`)
- Zarządzanie sesją (`Session`) i transakcjami biznesowymi
- Logowanie i dostęp do bazy (`Login`, `Database`, `BusApplication`)
- Paczki danych (`Datapack`, `GuidedRow`) i synchronizacja
- Kontekst aplikacji (`Context`)

**Kiedy używać:**
- Pytania o podstawowe klasy logiki biznesowej
- Praca z sesjami i transakcjami
- Tworzenie, modyfikacja i usuwanie obiektów
- Zrozumienie hierarchii `Row` → `Table` → `Module`

### 2. enova365-business-xml

Generator plików `business.xml` definiujących strukturę obiektów biznesowych.

**Zakres:**
- Definiowanie tabel i kolumn
- Typy danych (proste, relacyjne, złożone)
- Relacje między obiektami (1:N, N:1, polimorficzne)
- Klucze i indeksy
- Wzorce: słowniki, dokumenty z pozycjami, historia zmian

**Kiedy używać:**
- Tworzenie nowego modułu biznesowego
- Definiowanie encji do przechowywania w bazie
- Tworzenie relacji między obiektami
- Generowanie plików `*.business.xml`

## 🚀 Instalacja

### Claude Desktop / Claude.ai

Skopiuj folder ze skillem do katalogu skills w konfiguracji Claude.

### Cursor / Windsurf / inne IDE

Dodaj zawartość skilli do kontekstu projektu lub rules.

## 📁 Struktura repozytorium

```
soneta-ai-skills/
├── README.md
├── soneta-programming-basics/
│   ├── SKILL.md                    # Główna dokumentacja
│   └── references/
│       ├── session-login.md        # BusApplication, Database, Login, Session
│       ├── datapack-guidedrow.md   # Paczki danych, GuidedRow, synchronizacja
│       ├── context.md              # Klasa Context, komunikacja UI ↔ logika
│       └── examples.md             # Przykłady kodu i wzorce użycia
│
└── enova365-business-xml/
    ├── SKILL.md                    # Główna dokumentacja
    ├── assets/
    │   └── business_struct.xsd     # Schemat XSD dla walidacji
    └── references/
        ├── modules-catalog.md      # Katalog modułów enova365
        ├── table-reference.md      # Dokumentacja atrybutów table i col
        ├── relations-guide.md      # Tworzenie relacji między obiektami
        └── examples.md             # Przykłady z rzeczywistych modułów
```

## 💡 Przykłady użycia

### Tworzenie nowego obiektu

```csharp
using (var session = login.CreateSession(false, false, "Dodawanie"))
{
    var tm = session.GetTowary();
    
    using (var transaction = session.Logout(editMode: true))
    {
        var towar = new Towar();
        tm.Towary.AddRow(towar);
        towar.Kod = "NOWY001";
        towar.Nazwa = "Nowy towar";
        transaction.Commit();
    }
    
    session.Save();
}
```

### Definicja obiektu w business.xml

```xml
<table name="Towar" tablename="Towary" guided="Root" 
       caption="Towar" tablecaption="Towary">
  <key name="WgKodu" keyunique="true" keyprimary="true">
    <keycol name="Kod"/>
  </key>
  <col name="Kod" type="string" length="100" required="true"/>
  <col name="Nazwa" type="string" length="200" required="true"/>
  <col name="Cena" type="currency"/>
</table>
```

## 🔗 Powiązania między skillami

Skille są zaprojektowane do współpracy:

1. **enova365-business-xml** → definiuje strukturę obiektów
2. **soneta-programming-basics** → pokazuje jak pracować z wygenerowanymi klasami

Generator `BusinessGenerator` automatycznie tworzy klasy C# z plików `*.business.xml` podczas kompilacji projektu.

## 📝 Konwencje nazewnicze

| Element | Konwencja | Przykład |
|---------|-----------|----------|
| Klasa wiersza | PascalCase, l.poj. | `Towar`, `Kontrahent` |
| Klasa tabeli | PascalCase, l.mn. | `Towary`, `Kontrahenci` |
| Klasa modułu | Nazwa + Module | `TowaryModule` |
| Klucz | Wg + nazwa kolumny | `WgKodu`, `WgNazwy` |
| Namespace | Soneta.NazwaModułu | `Soneta.Towary` |

Obiekty biznesowe (domenowe) nazywane są po polsku: `Towar`, `Faktura`, `Kontrahent`.
Obiekty systemowe (techniczne) po angielsku: `Session`, `Context`, `Row`.

## 🤝 Współpraca

Zapraszamy do zgłaszania issues i pull requestów. Szczególnie cenne są:
- Nowe przykłady kodu
- Poprawki w dokumentacji
- Dodatkowe wzorce programistyczne

## 📄 Licencja

MIT

## 🔗 Linki

- [Dokumentacja enova365](https://www.enova.pl/)
- [Soneta Developer Portal](https://developer.enova365.pl/)
