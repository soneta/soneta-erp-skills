# Katalog wzorców plików importu XML — na podstawie plików standardowych platformy

Platforma Soneta inicjuje i konwertuje bazę setkami plików `*.dbinit.xml` osadzonych
w bibliotekach. To ten sam mechanizm i ta sama składnia, co zwykły import XML (`<session>`),
więc pliki standardowe są **najlepszym wzornikiem** budowy własnych plików importu — dla dodatku
partnera (własny `*.dbinit.xml`) i dla jednorazowego wczytania danych (`dbmgr importxml`).
Ten dokument zbiera z nich powtarzalne wzorce i gotowe szkielety dla najczęstszych obiektów.
Składnię i atrybuty opisuje [import-export-xml.md](import-export-xml.md); tu są **przykłady**.

**Nazwy pól w przykładach są ilustracyjne** (stan na wersję, z której je wzięto). Przed użyciem
zawsze zweryfikuj strukturę obiektu skanem [scan-props](../../programming/references/scan-props.md), to obowiązek
agenta (zob. *Zasada nadrzędna* w [import-export-xml.md](import-export-xml.md)).

## Szybki wybór wzorca

| Chcę wczytać… | Wzorzec | Przykład |
|---|---|---|
| Prosty słownik (kod, nazwa, kilka pól) | rekord guidowany ze strukturalnym GUID-em | [Słownik](#słownik-prosty) |
| Element słownika kategoryzowanego | `SlownikElem` z `Kategoria` | [SlownikElem](#element-słownika-kategoryzowanego) |
| Słownik użytkownika z pozycjami | definicja + elementy odwołujące się przez `id` | [DefinicjaSlownika](#słownik-definiowany-z-elementami) |
| Rekord istniejący już w bazie (np. utworzony przez SQL), któremu trzeba nadać stały GUID | `key="Kod=…"` + `guid` | [Jednostka](#przypięcie-guid-a-do-istniejącego-rekordu-key) |
| Definicję dokumentu (kadrową, księgową, handlową) | rekord z subrow `Numeracja` | [DefinicjaDokumentu](#definicja-dokumentu-z-numeracją) |
| Poprawkę jednego pola w istniejącej definicji przy podniesieniu wersji | „patch”: ten sam `guid`, nowy `dbversion`, tylko zmienione pola, zwykle `updateonly` | [Patch](#patch-rekordu-w-kolejnej-wersji) |
| Definicję zadania / zdarzenie terminarza z kodem C# | `DefZadania`, `TaskDefinition` z `<Code>` | [Zadania](#definicja-zadania-i-zdarzenie-terminarza-z-kodem) |
| Cechę (pole dodatkowe) do tabeli | `FeatureDefinition` | [Cecha](#cecha-featuredefinition) |
| Szablon e-mail / SMS | `SzablonEmail`, `SzablonSms` z placeholderami | [Szablony](#szablon-e-mail-i-sms) |
| Ustawienie konfiguracji | `CfgNode` + `CfgAttribute` (dwa warianty) | [Konfiguracja](#ustawienie-konfiguracji-cfgnode) |
| Rolę systemową i prawa do obiektów | `SystemRole` + `RoleText`, rekordy `Right` | [Role i prawa](#rola-systemowa-i-prawa-do-obiektów) |
| Miejsce na kod klienta w projekcie runtime | `RuntimeProject` + `CodeFile insertonly` | [Kod runtime](#projekt-runtime-i-plik-kodu-insertonly) |
| Kokpit z kafelkami | `DashboardView` + kolekcja `Tiles` | [Kokpit](#kokpit-z-kafelkami) |
| Definicję formularza dodatkowego (tuple) z polami | `DbTupleDefinition` + `RuntimeFieldDefinition` z referencją `Tabela:GUID` | [Tuple](#definicja-tuple-z-polami-referencja-tabelaguid) |
| Definicję elementu wynagrodzenia (kreator lub kod C#) | `DefinicjaElementu` z subrow `Algorytm`, `Deklaracje`, `Nieobecnosci` | [Element wynagrodzenia](#definicja-elementu-wynagrodzenia) |

Gotowe pliki w katalogu `examples/`:
[dbinit-slownik-i-poprawki.dbinit.xml](../examples/dbinit-slownik-i-poprawki.dbinit.xml)
(nagłówek dbinit, słownik, `key`, patch, `updateonly`),
[import-rola-i-prawa.xml](../examples/import-rola-i-prawa.xml) (rola cząstkowa + prawa do obiektów),
[import-cecha-i-szablon-email.xml](../examples/import-cecha-i-szablon-email.xml) (cecha + szablon),
[import-definicja-elementu-wynagrodzenia.xml](../examples/import-definicja-elementu-wynagrodzenia.xml)
(element płacowy: kreator algorytmu i edytor z kodem C#).
Pliki złożone z wzorców standardowych; **nie były wczytywane próbnie** — nazwy pól potwierdź skanem.

## Wzorce ogólne wyniesione z plików standardowych

### Nagłówek pliku

```xml
<?xml version="1.0" encoding="utf-8"?>
<session xmlns="http://www.soneta.pl/schema/business" versionName="soneta" priority="200">
```

- **Zawsze `xmlns`.** Około połowa plików historycznych go nie ma i działa przez tryb zgodności,
  ale nowe pliki pisz z przestrzenią nazw. Literówka w nazwie atrybutu (`mlns`) nie zgłasza
  błędu — atrybut jest po cichu ignorowany.
- **`versionName`**: `soneta` dla obiektów biznesowych; `system` dla obiektów infrastruktury
  (projekty runtime, szablony powiadomień, kokpity, definicje zadań systemowych). Własny dodatek
  używa **własnej** nazwy wersjonowania (zob. sekcję dbinit w [import-export-xml.md](import-export-xml.md)).
- **`priority`** — piętra stosowane w plikach standardowych: 1–10 projekty runtime i jednostki,
  40–100 słowniki podstawowe (muszą poprzedzić dane, które się do nich odwołują), 200–400
  definicje, 1000–4000 role i prawa, bardzo wysokie (np. 9000+) — poprawki „na końcu”.
- Kodowanie: UTF-8. Część plików standardowych jest w UTF-16 z deklaracją `encoding="Unicode"` —
  czytnik platformy to akceptuje, ale zewnętrzne narzędzia XML nie; nie powielaj tego.

### Strukturalny GUID rekordów standardowych

Rekordy standardowe używają GUID-ów „mówiących”: `00000000-MMMM-OOOO-NNNN-000000000000`, gdzie
`MMMM` = moduł, `OOOO` = rodzaj obiektu, `NNNN` = numer. Dla własnego dodatku przyjmij **własny,
stały prefiks** (np. losowo wygenerowany pierwszy segment) i numeruj rekordy w ostatnich
segmentach — łatwo je odróżnić od standardowych, a plik pozostaje przenośny między bazami.
Nigdy nie generuj GUID-ów losowo przy każdym wydaniu pliku.

### Trzy sposoby wskazania innego rekordu w treści pola

```xml
<Wydzial>00000000-0005-0001-0001-000000000000</Wydzial>            <!-- sam GUID w tabeli docelowej -->
<Definition>DbTupleDefinition:00000000-0005-0013-0204-000000000000</Definition>  <!-- Tabela:GUID -->
<Definicja>DefinicjaSlownika_1</Definicja>                          <!-- id rekordu z TEGO pliku -->
```

Kolejność rozstrzygania: najpierw `id` z bieżącego pliku, potem `Tabela:GUID`, potem sam GUID.
Forma `Tabela:GUID` jest konieczna, gdy pole może wskazywać rekordy z różnych tabel
(np. `Entitle`, `Source` w prawach, `Definition` w polach tuple). Odwołanie przez numeryczny
`#ID` jest wspierane, ale w plikach standardowych nieużywane — nie jest przenośne.

### Puste elementy są znaczące

`<Opis />` **ustawia pustą wartość** (dla referencji — `null`), a nie „pomija pole”. W patchu
rekordu podawaj wyłącznie pola, które zmieniasz; nie kopiuj pustych elementów z eksportu.

### Patch rekordu w kolejnej wersji

Ten sam `guid`, nowy `dbversion`, unikalny `id` (sufiks wersji), tylko zmienione pola:

```xml
<DefinicjaNieobecnosci id="DefinicjaNieobecnosci_721" guid="00000000-0006-0005-0016-000000000000" dbversion="100000">
  <Nazwa>Delegacja służbowa</Nazwa>
  <Typ>UsprawiedliwionaPłatna</Typ>
  <!-- … komplet pól przy pierwszym wydaniu … -->
</DefinicjaNieobecnosci>
<DefinicjaNieobecnosci id="DefinicjaNieobecnosci_721_100500" guid="00000000-0006-0005-0016-000000000000" dbversion="100500">
  <WniosekUrlopowy>true</WniosekUrlopowy>
</DefinicjaNieobecnosci>
```

- `id` musi być unikalne w pliku — duplikat kończy import błędem; stąd sufiks wersji w `id`.
- Dodaj `updateonly="true"`, gdy rekord mógł zostać przez klienta usunięty — bez tego patch
  utworzyłby go na nowo z samym zmienionym polem.
- Wartość `updateonly` musi być dokładnie `true` — wpis ze spacją (`" true"`) jest ignorowany.
- Konwencja plików: bazowy `Obiekt.dbinit.xml` + przyrostowe `Obiekt_RRMMDDPP.dbinit.xml`
  (8 cyfr = wersja programu, od której zmiana obowiązuje; nie może przekraczać wersji bieżącej).
- Rekordy bez `dbversion` w pliku dbinit są pomijane; `dbversion` postawiony na `<session>`
  **nie jest czytany** — musi być na każdym rekordzie głównym.

### Wartości specjalne

| Typ | Zapis |
|---|---|
| bool | `True` / `False` |
| enum | nazwa symbolu, także z polskimi znakami: `PłatnaZDołu` |
| enum flagowy | wartości po przecinku: `Brak, Przychod` |
| Date graniczna | `(max)`, `(min)` |
| FromTo | `(wszystko)`, `2026-01-01...`, `2026-01-01...2026-12-31` |
| Guid pusty | `00000000-0000-0000-0000-000000000000` |
| procent | `0.00%` |
| czas | `0:00` |
| kolor | `#05DF72` |
| wzór numeracji | mini-język: `Definicja.Symbol/Seria/*:6/Data.Year:2` (`*` = licznik, `:n` = liczba cyfr) |

### Kod C#, XML i HTML w treści pola

Kod wpisuje się wprost w element, z encjami XML (`&amp;&amp;`, `&lt;`, `&gt;`) albo w `<![CDATA[ … ]]>`.
Białe znaki są znaczące — wcięcie XML trafia do bazy razem z kodem. Osadzony XML (formularz,
treść roli) i HTML (szablon e-mail) zapisuje się **zaescape'owany** w treści elementu. Binaria
(ikony) — Base64 w treści elementu.

## Katalog przykładów

### Słownik prosty

```xml
<FormaPrawna id="FormaPrawna_3" guid="00000000-0009-0013-0003-000000000000" dbversion="140300">
  <Kod>SOO</Kod>
  <Nazwa>Spółka z o.o.</Nazwa>
  <StatusPodmiotu>PodmiotGospodarczy</StatusPodmiotu>
  <Domyslna>false</Domyslna>
</FormaPrawna>
```

Analogicznie: branże, źródła kontaktu, jednostki miary, kody (PKD, CPV), rodzaje środków
trwałych — jeden element na pozycję, kompletne pola, stały GUID.

### Element słownika kategoryzowanego

Wspólna tabela słowników z polem kategorii i selektorem:

```xml
<SlownikElem guid="00000000-9003-0019-0001-000000000000" dbversion="25060000">
  <Selektor>Standard</Selektor>
  <Kategoria>Kasa.SymFormUS</Kategoria>
  <Nazwa>ALK-1</Nazwa>
</SlownikElem>
```

### Słownik definiowany z elementami

Elementy wskazują definicję przez jej `id` z tego samego pliku; sama definicja ma GUID,
elementy — nie muszą (identyfikuje je definicja + symbol):

```xml
<DefinicjaSlownika id="DefinicjaSlownika_KatArch" guid="00000000-0016-0002-0000-000000000000" dbversion="100800">
  <Nazwa>Kategoria archiwalna</Nazwa>
</DefinicjaSlownika>
<ElemSlownika id="ElemSlownika_KatArch_A" dbversion="100800">
  <Definicja>DefinicjaSlownika_KatArch</Definicja>
  <Symbol>A</Symbol>
  <Nazwa>A</Nazwa>
  <Zablokowany>False</Zablokowany>
</ElemSlownika>
```

### Przypięcie GUID-a do istniejącego rekordu (`key`)

`key` znajduje rekord po kluczu naturalnym; jeśli istnieje — **nadpisuje mu GUID** z atrybutu,
jeśli nie — tworzy. Typowe, gdy rekordy powstały wcześniej (SQL, ręcznie), a od danej wersji
mają być standardowe:

```xml
<Jednostka guid="00000000-0011-0007-0003-000000000000" key="Kod=l" dbversion="14">
  <Kod>l</Kod>
  <Opis>litr - jednostka objętości</Opis>
  <Precyzja>1</Precyzja>
  <Typ>Objętość</Typ>
</Jednostka>
```

Różnica wobec `where`: `where` wymaga istnienia rekordu (brak → błąd „Nieznaleziony zapis”).
Oba przyjmują **jeden** warunek `Pole=wartość`; wiele trafień → błąd „Znaleziono wiele zapisów”.

### Definicja dokumentu z numeracją

Subrow `Numeracja` zapisuje się jako zagnieżdżony element bez atrybutów:

```xml
<DefinicjaDokumentu id="DefinicjaDokumentu_UMW" guid="00000000-0005-0004-0001-000000000000" dbversion="100000">
  <Symbol>UMW</Symbol>
  <Typ>Umowa</Typ>
  <Nazwa>Umowa-Zlecenie</Nazwa>
  <Domyslna>True</Domyslna>
  <Blokada>False</Blokada>
  <Numeracja>
    <Wzor>Definicja.Symbol/Wydzial.Symbol/Seria/Data.Year:4/Data.Month:2/*:4</Wzor>
  </Numeracja>
</DefinicjaDokumentu>
```

Definicja dokumentu handlowego ma ten sam kształt, tylko kilkadziesiąt pól więcej (kategoria,
kierunek magazynu i płatności, definicja ewidencji, waluta, precyzja). Pola unikalne oznacza się
`duplicate="number"` — przy kolizji z istniejącym rekordem klienta symbol/nazwa dostaje
przyrostek zamiast błędu:

```xml
<DefDokHandlowego guid="00000000-0011-0002-0093-000000000000" dbversion="22120101">
  <Symbol duplicate="number">SGV</Symbol>
  <Nazwa duplicate="number">Sprzedaż w grupie VAT</Nazwa>
  <Kategoria>Sprzedaż</Kategoria>
  <Numeracja><Wzor>Definicja.Symbol/Seria/*:6/Data.Year:2</Wzor></Numeracja>
  <WalutaPlatnosci>00000000-0004-0001-0001-000000000000</WalutaPlatnosci>
  <!-- … pozostałe pola wg scan-props … -->
</DefDokHandlowego>
```

Gdy unikalność obejmuje dwa pola, wskaż drugie przez `duplicateKeyField`:
`<Nazwa duplicate="number" duplicateKeyField="NazwaTabeli">…</Nazwa>`.

### Definicja zadania i zdarzenie terminarza z kodem

```xml
<DefZadania id="DefZadania_AW" guid="00000000-0009-0007-0013-000000000000" dbversion="22100000">
  <Symbol>AW</Symbol>
  <Rodzaj>Incydentalne</Rodzaj>
  <Nazwa>Awaria</Nazwa>
  <Domyslna>True</Domyslna>
  <Formularz>Pełny</Formularz>
  <Numeracja>
    <Wzor>Definicja.Symbol/Data.Year:4/Data.Month:2/*</Wzor>
    <PodczasEdycji>False</PodczasEdycji>
  </Numeracja>
</DefZadania>
```

Definicja zdarzenia terminarza (`TaskDefinition`) niesie kod C# kalkulatora w polu `Code`;
patch kodu w kolejnej wersji to rekord z `updateonly="true"` i samym `<Code>`:

```xml
<TaskDefinition guid="00000000-0015-0003-0002-000000000000" dbversion="26060000" updateonly="true">
  <Code>
public class Task_Zadanie : TaskCalculatorZadania {
    public override bool IsEnable() {
        if (Row.Rodzaj == RodzajZadania.Zadanie &amp;&amp; Row.Aktywny) { Name = Row.Nazwa; return true; }
        return false;
    }
}
  </Code>
</TaskDefinition>
```

### Cecha (`FeatureDefinition`)

```xml
<FeatureDefinition guid="50839d58-66fb-49ee-992f-4ac4e4123d0d" dbversion="21100000">
  <TableName>Tickets</TableName>
  <Name>Tags</Name>
  <TypeNumber>Array</TypeNumber>
  <ReadOnlyMode>Standard</ReadOnlyMode>
  <Algorithm>DB</Algorithm>
  <ValueRequiredMode>NonRequired</ValueRequiredMode>
  <Group>False</Group>
  <History>False</History>
  <StrictDictionary>False</StrictDictionary>
  <Dictionary>Tags</Dictionary>
</FeatureDefinition>
```

`TableName` to nazwa tabeli (l.mn.), nie obiektu. Cecha algorytmiczna trzyma kod w `Code`.

### Szablon e-mail i SMS

HTML zaescape'owany, placeholdery `{Obiekt.Pole}` rozwijane w kontekście obiektu:

```xml
<SzablonEmail id="SzablonEmail_Rez" guid="00000000-0016-0006-0009-000000000000" dbversion="23040000">
  <Nazwa>Rezerwacja stanowiska pracy</Nazwa>
  <Blokada>False</Blokada>
  <DO>{Osoba.EMAIL}</DO>
  <Temat>Rezerwacja stanowiska pracy w okresie: {Okres}</Temat>
  <Tresc>&lt;p&gt;Zarezerwowano stanowisko: &lt;strong&gt;{StanowiskoPracy.Nazwa}&lt;/strong&gt;&lt;/p&gt;</Tresc>
</SzablonEmail>
<SzablonSms guid="00000000-0016-0006-0002-000000000000" dbversion="120300">
  <Nazwa>SMS podstawowy</Nazwa>
  <Tresc>Dzień dobry,
{SysNotificationContent.Body}</Tresc>
</SzablonSms>
```

### Ustawienie konfiguracji (`CfgNode`)

**Wariant A — zmiana jednego atrybutu istniejącego węzła** (`addnew` na kolekcji chroni pozostałe
atrybuty, `where` znajduje atrybut po nazwie):

```xml
<CfgNode guid="00000000-9014-0015-0000-000000000000" dbversion="150000">
  <Attributes addnew="true">
    <CfgAttribute where="Name=DisableMouseWheelComboBox">
      <StrValue>True</StrValue>
    </CfgAttribute>
  </Attributes>
</CfgNode>
```

**Wariant B — nowy węzeł liścia z atrybutami** (atrybut wskazuje węzeł-rodzica przez `id`):

```xml
<CfgNode id="CfgNode_Kasa" guid="00000000-9005-0000-0013-000000000000" dbversion="100000">
  <Parent>00000000-9005-0000-0011-000000000000</Parent>
  <Name>00000000-0015-0001-0001-000000000000</Name>
  <Type>Leaf</Type>
  <Attributes>
    <CfgAttribute id="CfgAttribute_Kasa_1">
      <Node>CfgNode_Kasa</Node>
      <Name>Domyślna kasa</Name>
      <Type>_string</Type>
      <StrValue>00000000-0003-0002-0001-000000000000</StrValue>
    </CfgAttribute>
  </Attributes>
</CfgNode>
```

Alternatywą dla przenoszenia całej konfiguracji jest rejestr konfiguracji
([config-reg.md](config-reg.md)); XML nadaje się do **wskazanych** ustawień.

### Rola systemowa i prawa do obiektów

Rola: rekord `SystemRole` z drzewem praw w polu `RoleText` (zaescape'owany XML `<Role>`,
`AccessRight` = `Granted` / `Denied` / `ReadOnly`). `Partial="True"` oznacza rolę cząstkową
(nakładkę), `Destiny` — przeznaczenie (`Forms`, `WebUser`, `Neutral`):

```xml
<SystemRole id="SystemRole_101" guid="00000000-0015-0002-0101-000000000000" dbversion="21060000">
  <Name>Towary i usługi - dodawanie, edycja</Name>
  <Locked>False</Locked>
  <Partial>True</Partial>
  <IsSystem>False</IsSystem>
  <Destiny>Forms</Destiny>
  <RoleText>&lt;?xml version="1.0" encoding="utf-8"?&gt;
&lt;Role Guid="00000000-0015-0002-0101-000000000000" Name="Towary i usługi - dodawanie, edycja" Mode="Advanced"&gt;
  &lt;Right Name="Rights"&gt;&lt;Right Name="Program"&gt;&lt;Right Name="Handel"&gt;&lt;Right Name="Towary"&gt;
    &lt;Right Name="Towar" AccessRight="Granted"&gt;
      &lt;Right Name="Reports" AccessRight="Denied" /&gt;
    &lt;/Right&gt;
  &lt;/Right&gt;&lt;/Right&gt;&lt;/Right&gt;&lt;/Right&gt;
&lt;/Role&gt;</RoleText>
  <Rights />
  <Powiazane />
</SystemRole>
```

Prawo do konkretnego obiektu (definicji dokumentu, ewidencji, oddziału…) — rekord `Right`
bez własnego GUID-a; `Entitle` wskazuje uprawnionego (`Entitle:GUID` — rola lub operator),
`Source` wskazuje obiekt w formie `Tabela:GUID`. Prawo tylko do odczytu: `ReadOnlyRight`.

```xml
<Right dbversion="24040000">
  <Entitle>Entitle:00000000-0015-0001-0001-000000000000</Entitle>
  <Source>DefinicjaRozliczeniaMediow:00000000-0009-1017-0003-000000000000</Source>
</Right>
<Right dbversion="24040000">
  <Entitle>Entitle:00000000-0015-0001-0100-000000000005</Entitle>
  <Source>EwidencjaSP:00000000-0003-0002-0001-000000000000</Source>
  <ReadOnlyRight>True</ReadOnlyRight>
</Right>
```

GUID `00000000-0015-0001-0001-000000000000` to standardowa rola administratora — nowe obiekty
będące źródłem praw są domyślnie niedostępne, więc plik definicji zwykle kończy się właśnie
takim rekordem (kontekst: [rights-source](../../programming/references/rights-source.md)). Pełny plik:
[import-rola-i-prawa.xml](../examples/import-rola-i-prawa.xml).

### Projekt runtime i plik kodu (`insertonly`)

`insertonly="true"` tworzy rekord tylko, gdy go nie ma — aktualizacja programu nie nadpisze
kodu wpisanego przez klienta. Pusty `<Text></Text>` to celowe „miejsce na kod”:

```xml
<session xmlns="http://www.soneta.pl/schema/business" versionName="system" priority="2">
  <RuntimeProject guid="00000000-0009-00ff-0001-000000000000" dbversion="22060000">
    <Solution>00000000-0015-0010-0001-000000000000</Solution>
    <Name>CRM</Name>
    <ProjectNamespace>Soneta.Runtime.Standard.CRM</ProjectNamespace>
  </RuntimeProject>
  <CodeFile guid="00000000-0009-0012-0010-000000000000" dbversion="26060102" insertonly="true">
    <Name>Algorytm kodu kontrahenta</Name>
    <RuntimeInfo>
      <Project>00000000-0009-00ff-0001-000000000000</Project>
      <Identifier>AlgorytmKoduKontrahenta</Identifier>
      <FileName>AlgorytmKoduKontrahenta</FileName>
    </RuntimeInfo>
    <Text></Text>
  </CodeFile>
</session>
```

### Kokpit z kafelkami

Tabela z selektorem — podtyp przez `class`; kafelki jako kolekcja `Tiles` z elementami bez
GUID-ów, wskazującymi rodzica przez `id`:

```xml
<DashboardView id="DashboardView_17" guid="00000000-0015-0009-0017-000000000000"
               class="Soneta.Business.Db.DashboardView+CockpitType,Soneta.Business" dbversion="26040000">
  <ViewType>Cockpit</ViewType>
  <Name>Startowy Dostawcy</Name>
  <DashboardArea>00000000-0015-0008-0002-000000000000</DashboardArea>
  <Destiny>WebUser</Destiny>
  <LayoutMode>Browser</LayoutMode>
  <Tiles>
    <DashboardViewTile id="DashboardViewTile_151">
      <Dashboard>DashboardView_17</Dashboard>
      <Definition>Table</Definition>
      <TileGuid>714b47e5-d96d-4a25-970a-1f1e71a7cff6</TileGuid>
      <Name>Należności i zobowiązania</Name>
      <Column>16</Column><Row>0</Row><Width>19</Width><Height>16</Height>
      <FolderPath>PulpitKontrahenta/Rozliczenia/NaleznosciIZobowiazania</FolderPath>
    </DashboardViewTile>
  </Tiles>
</DashboardView>
```

`class` = pełna nazwa typu z assembly; typ zagnieżdżony zapisuje się z `+`.

### Definicja tuple z polami (referencja `Tabela:GUID`)

```xml
<DbTupleDefinition guid="00000000-0005-0013-0204-000000000000" dbversion="25120404">
  <NazwaTabeli>Pracownicy</NazwaTabeli>
  <Nazwa duplicate="number" duplicateKeyField="NazwaTabeli">Wniosek o zmianę rezerwacji</Nazwa>
  <Symbol>WZR</Symbol>
  <Seria>False</Seria>
</DbTupleDefinition>
<RuntimeFieldDefinition guid="00000000-0005-0013-0204-000000000002"
                        class="Soneta.Core.DbTuples.DbTupleFieldDefinition,Soneta.Core" dbversion="25120404">
  <Type>DbTuple</Type>
  <Definition>DbTupleDefinition:00000000-0005-0013-0204-000000000000</Definition>
  <Lp>2</Lp>
  <Name>OriginalPeriod</Name>
  <FieldType>FromTo</FieldType>
</RuntimeFieldDefinition>
```

### Definicja elementu wynagrodzenia

Rekord bardzo rozbudowany; zakładki formularza odpowiadają subrow-om: `OkresNaliczania`,
`Algorytm` (→ `KreatorAlgorytmu` z `PodstawaTyp`/`Wspolczynnik`/`Czas`/`Korekta` albo kod C#
w polu `Tekst`), `Deklaracje` (PIT/ZUS), `Nieobecnosci`, `Zaokraglenie`. Standardowe elementy
stosują `Nazwa duplicate="number"`, `DoDnia>(max)`, `Podstawa>0.00 PLN`, `Ulamek>1/1`, enumy
z polskimi znakami. Szkielet kreatora (kwota z parametru dodatku):

```xml
<DefinicjaElementu id="DefinicjaElementu_Premia" guid="7a1d0c00-0007-0002-0001-000000000000">
  <RodzajZrodla>Dodatek</RodzajZrodla>
  <Nazwa duplicate="number">Premia uznaniowa</Nazwa>
  <Skrot>Prem.uzn.</Skrot>
  <DoDnia>(max)</DoDnia>
  <Zatrudnienie>Etat</Zatrudnienie>
  <OkresNaliczania><Naliczanie>PłatnaZDołu</Naliczanie><Typ>CoNMiesięcy</Typ><Ilosc>1</Ilosc></OkresNaliczania>
  <Algorytm>
    <Typ>KreatorAlgorytmu</Typ>
    <KreatorAlgorytmu>
      <PodstawaTyp>Kwota</PodstawaTyp>
      <PodstawaZa>NieZależyOdCzasu</PodstawaZa>
      <Wspolczynnik><Typ>BezWspółczynnika</Typ></Wspolczynnik>
      <Czas><Typ>NieUwzględniaj</Typ></Czas>
    </KreatorAlgorytmu>
    <Podstawa>0.00 PLN</Podstawa>
  </Algorytm>
  <Deklaracje> <!-- PozycjaPIT, Koszty, Zaliczka, Ulga, Spoleczne, Zdrowotne --> </Deklaracje>
  <Nieobecnosci> <!-- Urlop, Ekwiwalent, ZasilkiPracownicy: Wliczać/NieWliczać --> </Nieobecnosci>
</DefinicjaElementu>
```

Wariant z edytorem: `<Algorytm><Typ>EdytorAlgorytmu</Typ>…</Algorytm>` i kod w `<Tekst>` z metodami
`%NAZWA%_Param` / `%NAZWA%_Wylicz` (symbole podstawia platforma; encje XML dla `<`, `&`).
Pełny plik z obydwoma wariantami i opisem pól:
[import-definicja-elementu-wynagrodzenia.xml](../examples/import-definicja-elementu-wynagrodzenia.xml).
Znaczenie parametrów algorytmu i API kodu: [place-def-elementow](../../place-def-elementow/SKILL.md). Gdy trzeba odtworzyć
element o nietypowej konfiguracji, wyeksportuj istniejący i zmodyfikuj.

## Pułapki zauważone w plikach standardowych

- **Ten sam GUID wiele razy w pliku** jest poprawny (patche), ale każde wystąpienie musi mieć
  inne `id`.
- **Komentarze XML** są dozwolone między rekordami i między polami, ale **nie wewnątrz elementów
  kolekcji** między rekordami podrzędnymi (czytnik oczekuje tam wyłącznie elementów).
- **`deleted="True"`, `date`, `relationsimportmode`** są wspierane, lecz w plikach dbinit
  praktycznie nieużywane — należą do importu operacyjnego i eksportu.
- **`business="true"` nie występuje w żadnym pliku dbinit** — inicjowanie i konwersja bazy to
  zawsze import według rekordów; tryb biznesowy jest dla dokumentów operacyjnych.
- **Wydruki `.repx` nie są dystrybuowane przez XML importu** — element `<Report>` w plikach to
  pole definicji, nie rekord. Rejestrację wydruków opisuje [repx](../../repx/SKILL.md).
- Rekordy podrzędne często powtarzają wskaźnik na rodzica (`<Dashboard>DashboardView_17</Dashboard>`)
  — to nadmiarowe, ale spójne z eksportem; nie jest wymagane.

## Checklista własnego pliku wzorowanego na standardowych

- [ ] Nagłówek: `xmlns`, `versionName` (własna nazwa dla dodatku), `priority` dobrane tak,
      by słowniki poprzedzały odwołujące się do nich definicje.
- [ ] Każdy rekord główny: stały `guid` (własny prefiks), unikalny `id`, `dbversion`.
- [ ] Referencje do obiektów standardowych przez GUID (lub `Tabela:GUID`), do własnych
      rekordów z pliku przez `id`; bez `#ID`.
- [ ] Pola unikalne z `duplicate="number"` (+ `duplicateKeyField`, gdy klucz jest złożony).
- [ ] Poprawki wersji jako osobne rekordy patch (`guid` ten sam, nowy `dbversion`, tylko zmienione
      pola, `updateonly` gdy rekord mógł zniknąć); żadnych pustych elementów kopiowanych z eksportu.
- [ ] Miejsca na kod klienta z `insertonly="true"`.
- [ ] Nowe źródła praw domknięte rekordem `Right` dla roli administratora.
- [ ] Nazwy pól potwierdzone skanem `scan-props`; test na kopii bazy tylko na żądanie użytkownika
      (checklisty w [import-export-xml.md](import-export-xml.md)).

## Powiązania

- [import-export-xml.md](import-export-xml.md) — składnia, atrybuty, tryby, dbinit, testowanie.
- [demo-data.md](demo-data.md) — pliki demo: ten sam format, inna kolejność i cel.
- [config-reg.md](config-reg.md) — przenoszenie całej konfiguracji zamiast wskazanych rekordów.
- [programming](../../programming/SKILL.md): [rights-source](../../programming/references/rights-source.md),
  [datapack-guidedrow](../../programming/references/datapack-guidedrow.md),
  [scan-props](../../programming/references/scan-props.md), osadzanie dbinit jako EmbeddedResource.
- [place-def-elementow](../../place-def-elementow/SKILL.md): definicje elementów wynagrodzenia.
- [repx](../../repx/SKILL.md): rejestracja wydruków.
- [tools](../../tools/SKILL.md): `dbmgr importxml`.
