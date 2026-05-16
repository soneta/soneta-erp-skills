# Datapack i GuidedRow - Paczki danych

Dokumentacja mechanizmu paczek danych (Datapack) służącego do grupowania powiązanych obiektów i ich synchronizacji między bazami danych.

## Czym jest Datapack?

**Datapack** to struktura obiektów różnych typów powiązanych relacjami, które tworzą logiczną całość.

### Przykład: Dokument handlowy

```
DokumentHandlowy (Root)
 ├── PozycjaDokHandlowego[] (Child)
 ├── SumaVAT[] (Child)
 └── Platnosc[] (Child)
```

Faktura bez pozycji nie jest kompletną fakturą - wszystkie te obiekty stanowią jedną paczkę danych.

## Hierarchia klas Row i Table

Hierarchię ogólną opisuje [SKILL.md](../SKILL.md#hierarchia-głównych-klas). W skrócie: `Row → GuidedRow → ExportedRow` (po stronie wierszy) oraz `Table → GuidedTable → ExportedTable` (po stronie tabel). Szczegóły właściwości - sekcje poniżej.

## Atrybut guided w business.xml

Atrybut `guided` w definicji tabeli określa rolę obiektu w strukturze Datapack.

| Wartość | Klasa bazowa | Opis |
|---------|--------------|------|
| `Root` | `GuidedRow` | Główny element paczki danych. Posiada GUID. |
| `Exported` | `ExportedRow` | Jak Root + flaga Exported (czy wyeksportowany) |
| `Child` | `Row` | Element podrzędny w paczce. Musi mieć relację `relguided`. |
| `None` | `Row` | Nie uczestniczy w mechanizmie Datapack |

**Domyślnie:** `guided="Child"`

### Przykłady definicji

```xml
<!-- ROOT - główna kartoteka towaru -->
<table name="Towar" tablename="Towary" guided="Root">
  ...
</table>

<!-- EXPORTED - dokument do synchronizacji -->
<table name="DokumentHandlowy" tablename="DokHandlowe" guided="Exported">
  ...
</table>

<!-- CHILD - pozycje dokumentu (domyślnie) -->
<table name="PozycjaDokHandlowego" tablename="PozycjeDokHan">
  <col name="Dokument" type="DokumentHandlowy" 
       relguided="inner" delete="cascade"
       children="Pozycje"/>
  ...
</table>
```

## Atrybut relguided

Określa relację obiektu Child do obiektu Root w strukturze Datapack.

| Wartość | Opis |
|---------|------|
| `inner` | Obiekt podrzędny zagnieżdżony wewnątrz roota w XML |
| `outer` | Obiekt podrzędny poza rootem w XML (ale powiązany) |
| (puste) | Relacja nie jest częścią Datapack |

### Kiedy używać relguided

```xml
<!-- Pozycja dokumentu - CZĘŚĆ datapacka dokumentu -->
<col name="Dokument" type="DokumentHandlowy" 
     relguided="inner"           <!-- ← powiązanie w Datapack -->
     delete="cascade"
     children="Pozycje"/>

<!-- Towar na pozycji - NIE jest częścią datapacka dokumentu -->
<col name="Towar" type="Towar"   <!-- ← brak relguided -->
     children="Pozycje"/>
```

**Reguły:**
- Tabela Child może mieć **tylko jedną** kolumnę z `relguided`
- Tabela Root **nie posiada** relacji `relguided` (jest korzeniem paczki)

## GuidedRow - szczegóły

### Właściwości

| Właściwość | Typ | Opis |
|------------|-----|------|
| `Guid` | `System.Guid` | Globalnie unikalny identyfikator |
| `Attachments` | `AttachmentCollection` | Kolekcja załączników |
| `DefaultImage` | `Attachment` | Domyślne zdjęcie obiektu |
| `Note` | `string` | Notatka tekstowa |
| `FirstChangeInfo` | `ChangeInfo` | Pierwsza zmiana w historii |
| `LastChangeInfo` | `ChangeInfo` | Ostatnia zmiana w historii |
| `IsAdded` | `bool` | Czy nowo dodany |
| `IsModified` | `bool` | Czy zmodyfikowany |
| `IsDeleted` | `bool` | Czy skasowany |

### Załączniki

Załączniki można podpinać **tylko** do obiektów wywodzących się z `GuidedRow`.

```csharp
GuidedRow row = ...;

// Dostęp do załączników
foreach (Attachment att in row.Attachments)
{
    Console.WriteLine(att.Nazwa);
}

// Domyślne zdjęcie
Attachment img = row.DefaultImage;
```

### Dodawanie załącznika z transakcją

```csharp
public void DodajZalacznik(Login login, Towar towar, byte[] plik, string nazwa)
{
    using (var session = login.CreateSession(false, false, "DodawanieZalacznika"))
    {
        // Doczytaj towar w bieżącej sesji (mieszanie obiektów z różnych sesji jest błędem)
        var towarInSession = session.Get(towar);

        using (var transaction = session.Logout(editMode: true))
        {
            var attachment = new Attachment(towarInSession, AttachmentType.Attachments);
            towarInSession.Module.Business.Attachments.AddRow(attachment);

            attachment.Name = nazwa;
            attachment.RawData = plik;

            transaction.Commit();
        }
        session.Save();
    }
}
```

## ExportedRow

Rozszerza `GuidedRow` o flagę `Exported`.

### Użycie

Obiekty, które po eksporcie do systemów zewnętrznych **nie powinny być modyfikowane**.

**Przykład:** Przelew wyeksportowany do systemu bankowego - po zaksięgowaniu nie powinien być zmieniany w ERP.

```xml
<table name="PrzelewBase" tablename="Przelewy" guided="Exported">
  ...
</table>
```

### Właściwość

| Właściwość | Typ | Opis |
|------------|-----|------|
| `Exported` | `bool` | `true` = obiekt wyeksportowany, nie modyfikować |

## Historia zmian (ChangeInfos)

System loguje zmiany na obiektach `GuidedRow`.

### Tabela ChangeInfos

| Kolumna | Opis |
|---------|------|
| `SourceTable` | Nazwa tabeli źródłowej |
| `SourceGuid` | GUID zmodyfikowanego obiektu |
| `Time` | Data i czas zmiany |
| `Operator` | Kto dokonał zmiany |

### Dostęp do historii

```csharp
var bm = session.GetBusiness();

// Iteracja po historii zmian kontrahenta
Kontrahent knt = ...;
foreach (ChangeInfo ci in bm.ChangeInfos[knt])
{
    Console.WriteLine($"{ci.Time}: {ci.Operator}");
}

// Skróty na obiekcie
Console.WriteLine($"Utworzono: {knt.FirstChangeInfo?.Time}");
Console.WriteLine($"Ostatnia zmiana: {knt.LastChangeInfo?.Time}");
```

**Uwaga:** Zmiany na obiektach Child są rejestrowane na poziomie Root.

## Blokady

Modyfikacja **dowolnego** obiektu w strukturze Datapack zakłada blokadę na poziomie **Root**.

```
DokumentHandlowy ← BLOKADA
 ├── PozycjaDokHandlowego (modyfikacja tutaj)
 └── SumaVAT
```

Nie można równolegle edytować obiektów należących do jednego Datapacka.

## GuidedTable

Odpowiednik `GuidedRow` dla tabel - dodaje indeksator po GUID.

```csharp
GuidedTable<Towar> towary = ...;

// Pobranie obiektu po GUID
Guid guid = new Guid("65336878-70cf-4e64-bd72-b742cd26a657");
Towar towar = towary[guid];
```

## Wzorce użycia

### Definiowanie struktury Datapack

```xml
<!-- 1. Root - główny obiekt -->
<table name="Zamowienie" tablename="Zamowienia" guided="Root">
  <key name="WgNumeru" keyprimary="true" keyunique="true">
    <keycol name="Numer"/>
  </key>
  <col name="Numer" type="string" length="30" required="true"/>
  <col name="Data" type="date" required="true"/>
  <col name="Kontrahent" type="Kontrahent" required="true"/>
</table>

<!-- 2. Child - pozycje zamówienia -->
<table name="PozycjaZamowienia" tablename="PozycjeZamowien">
  <!-- Jedna relacja relguided="inner" -->
  <col name="Zamowienie" type="Zamowienie"
       required="true" readonly="true"
       relguided="inner" delete="cascade"
       children="Pozycje" keyprimary="true"/>
  <col name="Lp" type="int" required="true"/>
  <col name="Towar" type="Towar" required="true"/>
  <col name="Ilosc" type="double" required="true"/>
</table>
```

## Podsumowanie atrybutów

| Atrybut | Gdzie | Wartości | Opis |
|---------|-------|----------|------|
| `guided` | `<table>` | Root, Exported, Child, None | Rola w Datapack |
| `relguided` | `<col>` | inner, outer, (puste) | Relacja Child→Root |
| `delete` | `<col>` | cascade, restrict, setnull | Akcja przy usuwaniu roota |
