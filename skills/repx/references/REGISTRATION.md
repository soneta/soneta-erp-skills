# Rejestracja, kod-behind, parametry, style

Referencja uzupełniająca [../SKILL.md](../SKILL.md). Jak sprawić, by plik `.repx` stał się
działającym wydrukiem w platformie Soneta (enova365, Triva).

## Sam plik .repx nie wystarczy

Aby wydruk pojawił się na liście wydruków obiektu, potrzebne jest **powiązanie z typem
listy/obiektu**. Są dwie drogi:

1. **Rejestracja w kodzie dodatku** — atrybut `[assembly: DxReport(...)]` (dla programisty).
2. **Wzorzec użytkownika** — dodanie raportu w edytorze wydruków w programie; zapisywany w bazie
   w folderze „Wzorce użytkownika" (dla wdrożeniowca bez dostępu do kodu). Taki wzorzec można też
   przenosić między bazami przez import/eksport XML — zob. **[config](../../config/SKILL.md)**.

## Rejestracja atrybutem `[assembly: DxReport(...)]`

Umieszczana w projekcie dodatku (np. w pliku `Properties/AssemblyInfo.RepxReport.cs`). Wiąże plik
`.repx` z typem ORM i miejscem w menu:

```csharp
// wydruk listy towarów, wariant widoku "CennikTowarow"
[assembly: DxReport(
    typeof(Towary),          // TYP LISTY/OBIEKTU — decyduje, gdzie wydruk się pojawia
    "Cennik",                // nazwa w menu wydruków ("/" tworzy podmenu)
    "Cennik.repx",           // nazwa pliku .repx (zasób osadzony)
    "CennikTowarow",         // ViewValue — wariant widoku/listy (opcjonalnie, wiele)
    Priority = 0)]

// wydruk pojedynczego rekordu, tylko z formularza
[assembly: DxReport(
    typeof(Towar),
    "Obroty towaru", "ObrotyTowaru.repx",
    OnlyForm = true,
    Priority = 1)]

// podmenu + wiele wariantów widoku
[assembly: DxReport(
    typeof(DokHandlowe),
    "Stany magazynowe/Według towarów", "StanyMagazynoweWgTowarow.repx",
    "StanMagazynu",
    Priority = 0)]
```

| Element | Znaczenie |
|---|---|
| `typeof(...)` (1. arg) | **Typ powiązania.** Typ listy (np. `Towary`, `Kontrahenci`) albo pojedynczy rekord (`Towar`). Program buduje menu wydruków, zbierając atrybuty pasujące do typu bieżącej listy. |
| nazwa (2. arg) | Etykieta w menu; `/` tworzy podmenu (`"Obroty/Według dni"`). |
| plik (3. arg) | Nazwa pliku `.repx`. |
| warianty widoku (params) | Nazwane `ViewValue`, na których wydruk jest dostępny. Można podać kilka; obok nazw można użyć wartości enum kategorii (np. kategoria dokumentu). |
| `Priority` | Kolejność na liście wydruków. |
| `OnlyForm` / `OnlyTable` | Tylko formularz / tylko lista. |
| `Culture` | Ograniczenie językowe: `DxReportAttribute.CultureOnlyPL` (tylko PL), `DxReportAttribute.CultureAsk` (pytaj o język). |
| `Contexts` | Ograniczenie licencyjne/kontekstowe (np. moduł licencji). |
| `VisibleInAspxMode` | Widoczność w trybie web. |

Warstwa ORM (typy list, workery liczące dane) → **[programming](../../programming/SKILL.md)**.

## Osadzanie plików — automatyczne przez SDK

Plik `.repx` (oraz `.repx.cs`, grafiki, `.repss`) **wystarczy umieścić w podkatalogu `Repx/`**
projektu dodatku typu `*.Reports`. Soneta SDK osadza je automatycznie jako `EmbeddedResource` —
**nie dodawaj wpisów `EmbeddedResource` do `.csproj` ręcznie**. Grafiki wstawiane do raportu
(logo, wzory deklaracji) też muszą leżeć w `Repx/`. Struktura szkieletu dodatku i CLI → **[programming](../../programming/SKILL.md)**.

```
MojDodatek.Reports/
├── Repx/
│   ├── MojWydruk.repx          # szablon (XML)
│   ├── MojWydruk.repx.cs       # opcjonalny kod-behind (ReportSnippet)
│   └── logo.png                # grafiki też tu
├── RepxParams/
│   ├── MojWydrukParams.cs      # opcjonalne parametry (ContextBase)
│   └── MojWydrukParams.pageform.xml
└── Properties/AssemblyInfo.RepxReport.cs   # rejestracja [assembly: DxReport(...)]
```

## Dwa modele: czysty XtraReport + snippet vs klasa raportu

- **Model produkcyjny (zalecany): czysty `XtraReport` + snippet.** W `.repx` `ControlType` =
  `DevExpress.XtraReports.UI.XtraReport, DevExpress.XtraReports.v20.2`. Logika (obliczenia,
  warunkowe formatowanie zbyt złożone na wyrażenia) mieszka w osobnej klasie w pliku
  `MojWydruk.repx.cs` dziedziczącej po `ReportSnippet`. Snippet podpięty jest w `.repx` jako
  komponent `ReportSnippetComponent` w `ComponentStorage`:

  ```xml
  <Item4 Ref="33" ObjectType="Soneta.Business.UI.DxReports.ReportSnippetComponent,Soneta.Business.UI.DxReports"
         Name="Snippet" SnippetTypeName="MojDodatek.Reports.MojWydrukSnippet,MojDodatek.Reports" />
  ```

  Kod-behind wiąże się z kontrolkami/zdarzeniami szablonu **po nazwie** atrybutem `[DxBind]`:

  ```csharp
  public class MojWydrukSnippet : ReportSnippet {
      [DxBind] private XRTableCell MarzaCol;                       // pole = kontrolka z .repx
      [DxBind(Name = "Report")] private void XtraReport_BeforePrint(object s, CancelEventArgs e) { ... }
      [DxBind] private void MarzaCol_BeforePrint(object s, CancelEventArgs e) { ... }
  }
  ```

  Pełny opis klasy `ReportSnippet`, atrybutu `[DxBind]`, zdarzeń, parametrów i dostarczania danych
  → **[SNIPPET.md](SNIPPET.md)**. Wzorce samego kodu (sesja, ORM, workery) → **[programming](../../programming/SKILL.md)**.

  > **⚠️ Licencja DevExpress:** `ReportSnippet`/`[DxBind]` używają typów DevExpress — w kodzie dodatku
  > wymaga to własnej licencji DevExpress (licencja Soneta na projektowanie raportów jej nie zastępuje).
  > Bez licencji użyj generycznego `Snippet` + `[Bind]` — szczegóły w [SNIPPET.md](SNIPPET.md).

- **Model „klasa raportu": dedykowana podklasa `XtraReport`.** `ControlType` wskazuje własną
  klasę (`NazwaRaportu, Assembly`), a cała logika jest w niej. Używany głównie w testach; w
  produkcji preferuj model ze snippetem (rozdziela layout od kodu, snippet bywa współdzielony).

## Parametry wydruku

Raporty Soneta **nie** używają natywnych `<Parameters>` DevExpress. Parametry pytane od
użytkownika przed wydrukiem definiuje się jako **klasę kontekstu** + **formularz**:

- `RepxParams/MojWydrukParams.cs` — klasa dziedzicząca po `ContextBase` z właściwościami-parametrami.
- `RepxParams/MojWydrukParams.pageform.xml` — formularz parametrów (składnia form.xml →
  **[form-xml](../../form-xml/SKILL.md)**).
- W `.repx`: `BusinessDataSource DataKind="Context"` + `DataMember` nawigujący do właściwości/
  kolekcji klasy parametrów (np. `MojWydrukSnippet+ParamClass.MyDataSource`). Zob. [DATA.md](DATA.md).

## Style — arkusze `.repss`

Formatowanie jest centralne: nazwane style trzymane są w arkuszach DevExpress `.repss`, a raport
wybiera arkusz przez `StylesSource` na `BusinessContext`:

```xml
<Item1 Ref="49" ObjectType="...Components.BusinessContext,..." Name="BusinessContext"
       StylesSource="standardowy" />
```

- W kontrolkach odwołuj się do stylu po nazwie (`StyleName="StandardowyStyl"`,
  `"NaglowekTytulStyl"`, `"ListaStylAutomatyczny"`) zamiast duplikować `Font`/`BackColor`.
- `StylePriority Use*="false"` na kontrolce = „bierz tę właściwość ze stylu".
- Standardowe arkusze, nagłówki i stopki są zarządzane centralnie w bazie (foldery konfiguracji
  wydruków: arkusze stylów, nagłówki, stopki, podraporty, wzorce użytkownika). Nagłówki/stopki
  wstawia się kontrolkami `Header`/`Footer` przez `ReportSourceName` → [CONTROLS.md](CONTROLS.md).

Pełna anatomia pliku `.repss` (składnia, właściwości stylów, katalog nazwanych stylów) →
[STYLES.md](STYLES.md). Budowa samych plików nagłówka/stopki/podraportu oraz mechanizm wiązania po
nazwie logicznej → [SUBREPORTS.md](SUBREPORTS.md).

## Wersje DevExpress

- **Runtime programu: DevExpress 25.2.** Edytując `.repx` narzędziem DevExpress używaj wersji
  zgodnej z runtime.
- **`SerializerVersion="20.2.7.0"` / `Version="20.2"` w pliku** to metadana serializera, nie
  wersja runtime. Istniejące pliki z `20.2` działają poprawnie (loader podnosi format
  automatycznie) — **nie „przewersjonowuj" ich ręcznie** bez potrzeby. Nowe pliki twórz spójnie
  z istniejącymi w danym projekcie.

## Testowanie i generowanie z CLI

Wygenerowanie wydruku do PDF/innego formatu z wiersza poleceń oraz testy na żywej aplikacji →
**[tools](../../tools/SKILL.md)**.
