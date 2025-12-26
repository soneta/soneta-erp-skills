# Katalog modułów Soneta

Katalog modułów dla platform Soneta (enova365, Soneta Enterprise).

## Spis treści

1. [Architektura: Konfiguracja vs Operacje](#architektura-konfiguracja-vs-operacje)
2. [Lista modułów](#lista-modułów)
3. [Najważniejsze typy do relacji](#najważniejsze-typy-do-relacji)
4. [Popularne Subrow](#popularne-subrow)
5. [Interfejsy](#interfejsy)
6. [Moduły szczegółowo](#moduły-szczegółowo)

---

## Architektura: Konfiguracja vs Operacje

enova365 stosuje wzorzec **Definicja → Operacja**, gdzie:

- **Tabele konfiguracyjne (`config="true"`)** - definiują zachowanie systemu
  - Tworzone podczas wdrożenia
  - Określają algorytmy, nazwy, numerację, sposób przetwarzania
  - Często mają prefiks/sufiks: `Definicja`, `Def`, `Config`
  
- **Tabele operacyjne** - przechowują dane transakcyjne
  - Tworzone podczas codziennej pracy
  - Ich zachowanie określa powiązana definicja

### Przykłady par Definicja → Operacja

| Definicja (config) | Operacja | Opis |
|-------------------|----------|------|
| `DefinicjaDokumentu` | `DokEwidencja` | Definiuje typ dokumentu, numerację, zachowanie |
| `DefDokHandlowego` | `DokumentHandlowy` | Definiuje typ dokumentu handlowego |
| `DefinicjaElementu` | `WypElement` | Definiuje element wypłaty, algorytm naliczania |
| `DefinicjaListyPlac` | `ListaPlac` | Definiuje typ listy płac |
| `DefinicjaNieobecnosci` | nieobecności w kalendarzu | Definiuje typ nieobecności |
| `DefinicjaSzkolenia` | `RealizacjaSzkolenia` | Definiuje typ szkolenia |
| `DefDokHandlowego` | `DokumentHandlowy` | Definiuje fakturę, WZ, PZ, itd. |

---

## Lista modułów

| Moduł | Namespace | Opis |
|-------|-----------|------|
| Business | Soneta.Business.Db | Definicje systemowe, operatorzy, uprawnienia |
| Core | Soneta.Core | Definicje dokumentów, stawki VAT, oddziały, adresy |
| CRM | Soneta.CRM | Kontrahenci, banki, urzędy, kontakty |
| Kadry | Soneta.Kadry | Pracownicy, umowy, wydziały |
| Kalend | Soneta.Kalend | Kalendarze, grafiki pracy, RCP, nieobecności |
| Place | Soneta.Place | Listy płac, wypłaty, elementy wynagrodzenia |
| Handel | Soneta.Handel | Dokumenty handlowe, definicje dokumentów |
| Towary | Soneta.Towary | Towary, jednostki, ceny, kody CN |
| Magazyny | Soneta.Magazyny | Magazyny, okresy magazynowe |
| Kasa | Soneta.Kasa | Płatności, rachunki bankowe, formy płatności |
| Ksiega | Soneta.Ksiega | Konta, dekrety, schematy księgowe |
| SrodkiTrwale | Soneta.SrodkiTrwale | Środki trwałe, amortyzacja |
| Waluty | Soneta.Waluty | Waluty, tabele kursowe |
| Deklaracje | Soneta.Deklaracje | Deklaracje podatkowe |
| EwidencjaVat | Soneta.EwidencjaVat | Ewidencja VAT |
| Delegacje | Soneta.Delegacje | Delegacje krajowe i zagraniczne |
| HR | Soneta.HR | Oceny, rekrutacja, szkolenia, uprawnienia |
| HR2 | Soneta.HR2 | Opisy stanowisk, kompetencje, cele |
| Oceny | Soneta.Oceny | System ocen pracowniczych |
| Produkcja | Soneta.Produkcja | Technologie, operacje produkcyjne |
| ProdukcjaPro | Soneta.ProdukcjaPro | Zaawansowana produkcja, zlecenia |
| Zadania | Soneta.Zadania | Projekty, zadania, kampanie, budżety |
| Windykacja | Soneta.Windykacja | Sprawy windykacyjne |
| Support | Soneta.Support | Zgłoszenia serwisowe (tickety) |
| Samochodowka | Soneta.Samochodowka | Ewidencja przejazdów, pojazdy |
| Vehicles | Soneta.Vehicles | Flota pojazdów, rezerwacje |
| RealEstate | Soneta.RealEstate | Nieruchomości, stanowiska pracy |
| RMK | Soneta.RMK | Rozliczenia międzyokresowe kosztów |
| BI | Soneta.BI | Business Intelligence, dashboardy |

---

## Najważniejsze typy do relacji

### Podmioty i kontrahenci (CRM)

```xml
<using>Soneta.CRM</using>

<col name="Kontrahent" type="Kontrahent" relname="..."/>
<col name="Bank" type="Bank" relname="..."/>
<col name="UrzadSkarbowy" type="UrzadSkarbowy" relname="..."/>
```

### Pracownicy i kadry (Kadry)

```xml
<using>Soneta.Kadry</using>

<col name="Pracownik" type="Pracownik" relname="..."/>
<col name="Wydzial" type="Wydzial" relname="..."/>
<col name="Umowa" type="Umowa" relname="..."/>
```

### Towary i magazyny (Towary, Magazyny)

```xml
<using>Soneta.Towary</using>
<using>Soneta.Magazyny</using>

<col name="Towar" type="Towar" relname="..."/>
<col name="Jednostka" type="Jednostka" relname="..."/>
<col name="Magazyn" type="Magazyn" relname="..."/>
```

### Dokumenty handlowe (Handel)

```xml
<using>Soneta.Handel</using>

<col name="DokumentHandlowy" type="DokumentHandlowy" relname="..."/>
<col name="Definicja" type="DefDokHandlowego" relname="..."/>
```

### Finanse i płatności (Kasa, Waluty)

```xml
<using>Soneta.Kasa</using>
<using>Soneta.Waluty</using>

<col name="Waluta" type="Waluta" relname="..."/>
<col name="FormaPlatnosci" type="FormaPlatnosci" relname="..."/>
<col name="Platnosc" type="Platnosc" relname="..."/>
```

### Definicje i konfiguracja (Core)

```xml
<using>Soneta.Core</using>

<col name="Definicja" type="DefinicjaDokumentu" relname="..."/>
<col name="StawkaVat" type="DefinicjaStawkiVat" relname="..."/>
<col name="Oddzial" type="OddzialFirmy" relname="..."/>
<col name="Kraj" type="KrajTbl" relname="..."/>
```

---

## Popularne Subrow

### Adres (Core)

```xml
<using>Soneta.Core</using>
<col name="Adres" type="Adres"/>
```

Zawiera: KodPocztowy, Miejscowosc, Ulica, NrDomu, NrLokalu, Poczta, Wojewodztwo

### Osoba (Core)

```xml
<col name="OsobaKontaktowa" type="Osoba"/>
```

Zawiera: Imie, Nazwisko, DrugieImie, PESEL, DataUrodzenia

### Kontakt (Core)

```xml
<col name="DaneKontaktowe" type="Kontakt"/>
```

Zawiera: Telefon, Fax, Email, WWW

### NumerDokumentu (Core)

```xml
<col name="Numer" type="NumerDokumentu"/>
```

Zawiera: Symbol, Numer, Pelny

### FromTo (wbudowany)

```xml
<col name="Okres" type="FromTo"/>
```

Zawiera: From (data od), To (data do)

### BruttoNetto, BruttoNettoCy (Handel)

```xml
<using>Soneta.Handel</using>
<col name="Wartosc" type="BruttoNetto"/>
<col name="WartoscWalutowa" type="BruttoNettoCy"/>
```

---

## Interfejsy

| Interface | Moduł | Opis |
|-----------|-------|------|
| `IKontrahent` | Core | Kontrahent (relacja polimorficzna) |
| `IPodmiotKasowy` | Kasa | Podmiot kasowy |
| `IRightsSource` | Business | Źródło uprawnień |
| `IGuidedRow` | Business | Wiersz z nawigacją |
| `IDokument` | Core | Dokument |
| `IDokumentKasowy` | Core | Dokument kasowy |
| `IDokumentPlatny` | Core | Dokument płatny |
| `IDokumentKsiegowalny` | Core | Dokument księgowalny |

---

## Moduły szczegółowo

### Core (Soneta.Core)

**Tabele konfiguracyjne (config=true):**
DefinicjaDokumentu, DefinicjaDokumentuOA, CentrumKosztow, ZrodloFinansowania, RodzajZrodla, DefinicjaPodzielnikaKosztow, DefinicjaStawkiVat, DefinicjaStawkiAkcyzy, OddzialFirmy, KrajTbl, IsoProcedura, StrukturaOrganizacyjna, DefinicjaElementuStrukturyOrganizacyjnej, DefTeczki, RodzajKontaktu, DomyslnyCel, Slownik, SlownikZewn, SystemZewn, ScheduleDefs, ProceduryVAT, WarningDefs, KrajSME

**Tabele eksportowalne (guided=Exported):**
DokEwidencja

**Tabele operacyjne (guided=Root):**
HistoriaDanychOddziału, HistoriaDanychFirmyBase, ElementStrukturyOrganizacyjnej, Teczka, Discussion, Konwersacja, Aktualnosci

**Subrow:**
DefinicjaCyklu, DefinicjaNumeracji, NumerDokumentu, Adres, AdresRozszerzony, Kontakt, Osoba, StawkaVat

---

### CRM (Soneta.CRM)

**Tabele konfiguracyjne (config=true):**
DefKategKth, FormaPrawna, RoleOpiekun, AuthProvider, AuthAzureConfig, SzablonSms, ZrodloKontaktu, Branza, DefTransakcji, StanyTransakcji, DefLeadow, StanyLeada, PriorytetyLeadow, PriorytetyTran

**Tabele operacyjne (guided=Root):**
Kontrahent, Bank, UrzadSkarbowy, UrzadCelny, OddziałZUS, InstytucjaFinansowaPPK, Lokalizacja, OsobaKontrahent, WizytowkaFirmy, KontoPocztowe, WiadomoscEmail, SzablonEmail, Transakcja, Lead, Opiekun

**Subrow:**
PowiazaniePodmiotu

---

### Kadry (Soneta.Kadry)

**Tabele konfiguracyjne (config=true):**
DefPodstawyStazu, DefinicjaWydziału, Wydzial, GrupaZaszeregowania, TytulUbezpieczenia4, KodPracyWSzególnychWarunkachCharakterze, ZestawDodatków, DefinicjaAkordu, DefinicjaOświadczenia, DefinicjaBadaniaLekarskiego, DefSzkolenBHP, DefJezykowObcych, DefFundPozycz, KatCzynnSzkod, DefCzynnSzkod, KatZglSygnal

**Tabele operacyjne (guided=Root):**
Pracownik, Umowa, Dodatek, CzlonekRodziny, Akord, WniosekUrlopowy, OświadczeniePracownika, NagrodaKara, Pozyczka, ZgodaNaEdycję, KorektaZajęciaKomorniczego, UmowyZewnetrzne, CzynnSzkodPrac

**Subrow:**
Zaszeregowanie, Etat, StazPracyPracownika, Urodzony, DokumentOsoby, Obywatelstwo, StopienNiepelnosp, DaneZUS, DanePFRON, Ubezpieczenia, UmowaOPrace, ZawarcieUmowy, RozwiazanieUmowy, StatystykaGUS, PodatkiInfo, StanRodzinny

---

### Kalend (Soneta.Kalend)

**Tabele konfiguracyjne (config=true):**
CzytnikRCP, DefinicjaLimitu, DefinicjaStrefy, DefinicjaDnia, DefinicjaNieobecnosci, DefinicjaGrafikaPracy, DefinicjaZestawieniaCzasu, DefAlgorytmRCP, DefinicjaRozliczeniaCzasuPracy, DefinicjaRodzajuPracyZdalnej, DefinicjaZdarzeniaRCP, DefinicjaAktualizacjiKalendarza, DefWeryfKalend

**Tabele operacyjne (guided=Root):**
KalendarzBase, GrafikPracy, RozliczenieCzasuPracy, ElementRozliczeniaCzasuPracy, WniosekPracyZdalnej, DokumentAktualizacjiKalendarza, ObiektDoPlanowania

**Subrow:**
CzasPracy, Nadgodziny, Nocne, ZwolnienieZUS, UrlopMacierzyński, UrlopWychowawczy, UrlopWypoczynkowy, ZLA

---

### Place (Soneta.Place)

**Tabele konfiguracyjne (config=true):**
DefinicjaListyPlac, DefinicjaElementu, PozycjaPIT, KodRSA, DefinicjaElementuRozliczenia

**Tabele operacyjne (guided=Root):**
ListaPlac, Wyplata, WypElement, Zaliczka, KosztAutorski, ZaniechaniePodatkowe, OświadczenieZusOpieka, DodatekAutomatyczny, DefinicjaPlanowanejListyPłac, PlanowanaListaPłac, ZasiłekInnyPłatnik, DokumentRozliczeniaKontrahenta, DokumentRozliczeniaPracownika

**Subrow:**
KorektaDefElementu, CzasDefElementu, WspolczynnikDefElementu, KreatorAlgorytmu, AlgorytmDefElementu, KosztyUzyskaniaPrzychodu, ZaliczkaPodatku, UlgaPodatkowa, UbezpieczenieSpoleczne, UbezpieczenieZdrowotne, NarzutyNaWynagrodzenie, PodstawaUrlopu, PodstawaZasilkow, Zaokraglenie

---

### Handel (Soneta.Handel)

**Tabele konfiguracyjne (config=true):**
DefDokHandlowego, DefUrzadzeniaUz, DrukarkaFiskalna

**Tabele eksportowalne (guided=Exported):**
DokumentHandlowy, UrzadzenieUz

**Tabele operacyjne (guided=Root):**
PrzesylkaSpedyt, PaczkaWzorcowa

**Subrow:**
Realizacja, BruttoNetto, BruttoNettoCy, MozliwosciEdycji, Wersjonowanie, Przesylka, PunktOdbioru, EDokument, KreatorDokumentu, DokumentObcy, Wymiary

---

### Towary (Soneta.Towary)

**Tabele konfiguracyjne (config=true):**
Jednostka, Przelicznik, DefinicjaCeny

**Tabele operacyjne (guided=Root):**
Towar, KodCN, KodBDO, KodCPV, KodSUP, SchemOpakowan, ElementKompletu, CenaGrupowa, PrzecenaOkresowa

**Subrow:**
GrupyDostawOpcje, AlgorytmCeny, AlgorytmRabatu, ProduktInfo, WspolczynnikCeny

---

### Magazyny (Soneta.Magazyny)

**Tabele konfiguracyjne (config=true):**
Magazyn, OkresMagazynowy

**Subrow:**
PartiaTowaru, ParametryRezerwacji

---

### Kasa (Soneta.Kasa)

**Tabele konfiguracyjne (config=true):**
FormatWymianyElektronicznej, SposobZaplaty, FormaPlatnosci, DefinicjaPaczkiPrzelewu, TypIdenPodPrzel, EwidencjaSP, OkresMW, SerwisBankowy

**Tabele eksportowalne (guided=Exported):**
Platnosc, DokKasowyBase

**Tabele operacyjne (guided=Root):**
RachunekBankowyPodmiotu, Zaplata, RaportESP, RozliczenieSP, DokRozliczBase, PreliminarzDokument

**Subrow:**
NumerRachunku, RachunekBankowy, KartaPlatnicza, ElixirInfo, OdsetkiKarne

---

### Ksiega (Soneta.Ksiega)

**Tabele konfiguracyjne (config=true):**
OkresObrachunkowy, DefinicjaSlownika, SchematKsiegowy, RelacjaOpisAnal, ZestawienieKS, MatrycaBase, DefinicjaKregu, ZnacznikKonta, GrupaKont, SchematPodz

**Tabele operacyjne (guided=Root):**
DekretBase, KontoBase, ElemSlownika, WynikZestKS, SprawozdanieKS, ZleDlugiDokument

**Subrow:**
KPiR, DefinicjaZakresu

---

### Waluty (Soneta.Waluty)

**Tabele konfiguracyjne (config=true):**
Waluta, TabelaKursowa

**Subrow:**
Euro

---

### SrodkiTrwale (Soneta.SrodkiTrwale)

**Tabele konfiguracyjne (config=true):**
RodzajST, MiejsceUzytkowania, KategoriaST, KategoriaZapotrzebowania, TytulDokumentuST

**Tabele operacyjne (guided=Root):**
SrodekTrwalyBase, DokumentST, DokumentUL, ZestawST, LokalizacjaNier, RodzajPO, Wyposazenie

**Subrow:**
Sezonowosc, ParametryAmortyzacji

---

### HR (Soneta.HR)

**Tabele konfiguracyjne (config=true):**
DefElementuOcenyPracownika, WzorOcenyPracownika, DefinicjaStanowiska, DefinicjaFunkcji, DefinicjaEtapuRekrutacji, KategoriaUprawnienia, KategoriaSzkolenia, EtapRealizacjiSzkolenia, GrupaStanow

**Tabele operacyjne (guided=Root):**
OcenaPracownika, Rekrutacja, EtapRekrutacji, Wyszukanie, DefinicjaUprawnienia, UprawnieniePracownika, DefinicjaSzkolenia, DostawcaSzkoleń, OfertaSzkolenia, BudżetSzkoleń, WniosekOSzkolenie, RealizacjaSzkolenia

---

### Oceny (Soneta.Oceny)

**Tabele konfiguracyjne (config=true):**
DefinicjaSekcjiDokumentu, ZakresWartości, SkalaOcen, ElementSkaliOcen, KategoriaElementuOceny, DefinicjaElementuOceny, DefinicjaArkuszaOceny, DefinicjaOceny

**Tabele operacyjne (guided=Root):**
OcenaRealizacja, OcenaArkusz, OcenaPowiaz

**Subrow:**
MiaraElementuOceny, WartośćElementuOceny

---

### Zadania (Soneta.Zadania)

**Tabele konfiguracyjne (config=true):**
DefKampania, DefProjektu, StanProjektu, DefZadania, StanZadania, PriorytetZadania, DefBudgetAspect, DefBudgetCategory, DefBudgetCategoryRelation, DefBudget, BudgetPeriod, DefPlanVersion, DefsKoresp, StanyKoresp, TypyUrzadzen, KategorieAkt, Zespoly

**Tabele operacyjne (guided=Root):**
Projekt, Zadanie, Kampania, WersjaPlanu, Urzadzenie, PlanowanyPrzeglad, Korespondencja, BudzetyProjektu, GoogleCalendars, ZadaniaDnia

**Subrow:**
Budzet

---

### Produkcja (Soneta.Produkcja)

**Tabele konfiguracyjne (config=true):**
ProdProdukt, ProdSlownik

**Tabele operacyjne (guided=Root):**
Technologia, Operacja, PozycjaTechn, KosztTechn, CzasTechn, ProdZasob, ProdOsoba, ProdAwaria, ProdMeldunekBraku

---

### ProdukcjaPro (Soneta.ProdukcjaPro)

**Tabele konfiguracyjne (config=true):**
ProWydzial, ProUzytkownikPaneluMeldunkowego

**Tabele operacyjne (guided=Root):**
ProTechnologia, ProZlecenie, ProMeldunek, ProZasob, ProOsoba, ProStawka, ProDefinicjaMeldunku, ProKompetencja, ProDefinicjaOperacji, ProZestawienieMaterialow

---

### Support (Soneta.Support)

**Tabele konfiguracyjne (config=true):**
TicketDefinition, Priority, State, Product, ProductVersion, OperatorToTeam, SLACalendar

**Tabele operacyjne (guided=Root):**
Ticket, Team, RelationToDoc, SLADocument, TicketFollower

---

### Vehicles (Soneta.Vehicles)

**Tabele konfiguracyjne (config=true):**
VehicleType, VehicleState, ReservationDef, ReservationState

**Tabele operacyjne (guided=Root):**
VehicleDetails, VehicleEvent, VehicleReading, Reservation, Fine, Insurance, DamageEvent, TechInspection, VehicleHis

---

### Windykacja (Soneta.Windykacja)

**Tabele konfiguracyjne (config=true):**
EtapDefinicjiWindykacji, DefinicjaSprawyWindykacyjnej, StanWindykacji

**Tabele operacyjne (guided=Root):**
SprawaWindykacyjna

**Subrow:**
WindykacjaInfo

---

### RealEstate (Soneta.RealEstate)

**Tabele konfiguracyjne (config=true):**
TypNieruchomosc, StanNieruchomosci, CelRezerwacji, DefinicjaAlgorytmuUslugi, DefinicjaRozliczeniaMediow

**Tabele operacyjne (guided=Root):**
Nieruchomosc, NieruchomoscHis, NieruchomoscZdarzenie, StanowiskoPracy, RezerwacjaStanowiskaPracy, UslugaNieruch, RozliczenieMediow

**Subrow:**
AlgorytmNieruchomosci, AlgorytmUslugi

---

### Delegacje (Soneta.Delegacje)

**Tabele konfiguracyjne (config=true):**
KrajDelegacji, StawkiDelegacji

**Tabele operacyjne (guided=Root):**
Delegacja, KosztDelegacji

---

### Samochodowka (Soneta.Samochodowka)

**Tabele konfiguracyjne (config=true):**
EkoRodzajPaliwa, EkoRodzajSilnika

**Tabele operacyjne (guided=Root):**
Pojazd, DefinicjaTrasy, Przejazd, KosztEP, RozliczenieEP

**Subrow:**
Trasa

---

### RMK (Soneta.RMK)

**Tabele operacyjne (guided=Root):**
KosztRMK, DokumentRMK

---

### BI (Soneta.BI)

**Tabele konfiguracyjne (config=true):**
DataSource, DataModel, ModelGroupBy, ModelOrderBy, TimeSpanDefinition, TimeSpanSet, TimeSpanItem, DashboardItemLocation, DashboardViewLocation, DashboardItemDefinition, ChartParam, DataSetDefinition, ColumnDefinition, SerializationDefinition, AnalysisArea

**Subrow:**
FieldProxy, TableProxy

---

### Business (Soneta.Business.Db)

**Tabele konfiguracyjne (config=true):**
CfgNode, Operator, Entitle, DBItem, DBGroup, WizardDefinition, WizardStepDefinition, UserGroup, SysNotification, DashboardArea, DashboardView, SystemRole, RoleCategory, RuntimeSolution, RuntimeProject, PivotViews, NotifiCategories

**Tabele operacyjne (guided=Root):**
FeatureDefinition, FeatureTransferDefinition, FeatureSetDefinition, DictionaryItem, Task, SystemFiles, AppTokens

**Subrow:**
RuntimeDefinitionInfo, RuntimeFields, AlgorithmColumn
