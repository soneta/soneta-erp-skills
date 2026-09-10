---
name: erp
description: >
  Mapa i przewodnik po skillach platformy Soneta (enova365, Triva): programming (ORM, kod
  biznesowy), addon-planning, business-xml, form-xml, repx (wydruki
  .repx), place-def-elementow, config (import/eksport XML, scan-folders,
  appsettings.json, rejestr konfiguracji *.reg.json), tools (dbmgr, buscall, SonetaFrame),
  containers (docker compose, Apple container, Helm/Kubernetes). Używaj gdy użytkownik: (1)
  rozpoczyna zadanie dla platformy Soneta i nie wiadomo, który skill wybrać; (2) pyta ogólnie o
  dodatki, moduły lub rozszerzenia Soneta ERP; (3) wspomina enova, Soneta Enterprise, Triva bez
  sprecyzowania warstwy (dane, UI, logika, płace); (4) chce poznać dostępne skille; (5) realizuje
  zadanie obejmujące wiele warstw platformy; (6) pracuje nad kodem samej platformy w repozytorium
  źródłowym Soneta (`Soneta.*`, moduły standardowe) — skille opisują publiczną
  bibliotekę platformy i obowiązują tak samo dla dodatków partnerów i kodu zespołu Soneta.
---

# Mapa skills podczas pracy z platformą Soneta (enova365, Triva)

> **Zakres stosowania.** Skille Soneta dokumentują publiczną bibliotekę platformy (ORM, kod
> biznesowy, formularze, wydruki, narzędzia). Stosuj je **zawsze**, gdy powstaje kod na platformie —
> niezależnie od tego, czy jest to dodatek partnera, czy kod modułów standardowych pisany przez zespół
> Soneta w repozytorium źródłowym programu. Te same wzorce, checklisty i zasady bezpiecznego kodu
> (safe-code) obowiązują w obu przypadkach.

* [programming](../programming/SKILL.md) - Fundamentalne klasy ORM platformy Soneta. Obejmuje mapowanie
obiektowo-relacyjne (Row, Table, Module), zarządzanie sesją (Session), logowanie (Login, Database, BusApplication), 
paczki danych (Datapack, GuidedRow) oraz kontekst (Context). Używaj gdy użytkownik pyta o podstawowe klasy logiki 
biznesowej, strukturę obiektów ORM, sesje i transakcje, hierarchię klas Row/Table/Module, mechanizm Datapack i 
synchronizację danych, lub kontekst aplikacji Soneta, Context 
* [addon-planning](../addon-planning/SKILL.md) - Planowanie projektów dodatków dla platformy Soneta. Tworzy
  kompletną dokumentację projektową obejmującą: strukturę danych (tabele, relacje),
  elementy konfigurowalne, definicje list i menu, formularze, workery i raporty.
  Używaj gdy użytkownik prosi o zaplanowanie nowego modułu/dodatku Soneta,
  przygotowanie założeń projektu, stworzenie specyfikacji funkcjonalnej dodatku,
  lub zdefiniowanie struktury danych i interfejsu użytkownika dla nowego modułu.
*  [business-xml](../business-xml/SKILL.md) - Generator plików business.xml dla platformy Soneta.
   Tworzy definicje obiektów biznesowych (tabel, kolumn, relacji, indeksów) zgodne
   ze schematem XSD. Używaj gdy użytkownik prosi o stworzenie nowego modułu biznesowego,
   zdefiniowanie obiektów lub encji do przechowywania w bazie danych, utworzenie relacji
   między obiektami, lub generowanie plików business.xml dla platformy Soneta.
* [config](../config/SKILL.md) - Narzędzia i mechanizmy związane z konfiguracją systemu i funkcjami
  domenowymi platformy (zawartość rozwijana). Obecnie: (A) **import/eksport danych i ustawień
  konfiguracyjnych przez pliki XML** — struktura pliku `<session>`, import według rekordów
  (pliki `*.dbinit.xml`, baza demo), import przez logikę biznesową (`business="true"`),
  eksport datapacku, identyfikacja po GUID, formaty wartości; (B) `scan-folders` —
  inwentaryzacja **folderów statycznych menu** (`[assembly: FolderView]`) z bibliotek DLL
  (drzewo pozycji menu, listy, formularze, powiązanie z tabelą/`ViewInfo`); (C) **konfiguracja
  uruchomieniowa `appsettings.json`** — porty i adresy komponentów (orchestrator, server, web,
  webapi, webwcf, router, commhub), sprzężone pary adresów, kolejność warstw nadpisań
  (profil systemu, `-c`, `SONETA_`, argumenty CLI), znaczenie kluczy; (D) **rejestr
  konfiguracji (ConfigReg)**, czyli menu „Zarządzanie konfiguracją" — zrzut całej konfiguracji
  bazy do pliku `*.reg.json`, porównanie dwóch baz, scalanie paczek ustawień, sigile formatu
  (`$strict`, `$v`, `#klucz`, `@atrybut`, `$blob`) i ścieżki węzłów. Używaj gdy użytkownik
  buduje/analizuje XML importu danych Soneta, przenosi ustawienia między bazami, eksportuje
  rekordy do XML, mapuje strukturę menu dodatku, konfiguruje porty/adresy komponentów i pyta,
  co robi klucz w `appsettings.json`, albo pyta o rejestr konfiguracji, buduje/debuguje plik
  `*.reg.json` lub diagnozuje niedopasowany wiersz przy scalaniu. Odróżnij oba mechanizmy
  przenoszenia ustawień: XML (`<session>`, `dbinit.xml`) przenosi wskazane rekordy i nie
  wykrywa różnic, rejestr zdejmuje stan konfiguracji jako całość. Warstwa kodu importu
  (`SessionReader`/`SessionWriter`) i ORM → [programming](../programming/SKILL.md); operacje na bazie z CLI →
  [tools](../tools/SKILL.md).
* [tools](../tools/SKILL.md) - Narzędzia deweloperskie wiersza poleceń Soneta. `dbmgr` — zarządzanie
  bazami danych (tworzenie/rejestracja, konwersja, backup/restore, licencje, rozszerzenia,
  analiza, kompilacja); przygotowanie baz testowych/demo i automatyzacja w CI. `buscall` —
  testowanie na żywej aplikacji: zdalne sterowanie programem i zrzuty ekranu do analizy
  wizualnej. `SonetaFrame` / `SonetaFrameNew` (obie nazwy używane równolegle) — aplikacja ramki hostująca webową wersję
  programu: uruchamianie, parametry startowe, plik ustawień i źródła baz danych. Używaj gdy
  użytkownik zarządza bazą z CLI, tworzy bazę demo, robi backup/konwersję, uruchamia ramkę
  i konfiguruje połączenia do baz, albo weryfikuje zmiany na uruchomionej aplikacji.
* [repx](../repx/SKILL.md) - Wydruki DevExpress XtraReports (pliki `.repx` — serializowany XML) dla
  platformy Soneta. Struktura raportu (pasma, kontrolki `XRTable`/`XRLabel` i własne kontrolki
  Soneta `AmountLabel`/`Header`/`Footer`), źródło danych `BusinessDataSource` z `DataKind`
  (`CurrentList`/`Context`/`SingleRow`/`Session`), master-detail przez `DetailReportBand`,
  wiązania `ExpressionBindings`, podsumowania, grupowanie, formatowanie warunkowe oraz rejestracja
  `[assembly: DxReport(...)]`. Używaj gdy użytkownik tworzy/edytuje plik `.repx`, pyta o strukturę
  wydruku DevExpress w Soneta/enova365, źródło danych raportu lub jego rejestrację. Kod-behind
  wydruku (`ReportSnippet`, `[DxBind]`) i logika ORM licząca dane → [programming](../programming/SKILL.md); formularz
  parametrów wydruku → [form-xml](../form-xml/SKILL.md).
* [form-xml](../form-xml/SKILL.md) - XML z nieistniejącymi elementami. ZAWSZE używaj tego skilla gdy użytkownik: (1) prosi o utworzenie lub modyfikację pliku pageform.xml, viewform.xml, form.xml, lookupform.xml lub gridform.xml dla platformy Soneta (enova365); (2) pyta o elementy DataForm, Page, Group, Grid, Field, Row, Stack, Flow, Command, Include, Appearance, GroupBy w Soneta; (3) pyta o składnię EditValue, DataContext, Visibility, RowCondition, Renderable, CaptionHtml, Footer, Class lub układ UI formularzy Soneta; (4) pokazuje istniejący plik form.xml/pageform.xml/viewform.xml i pyta o jego strukturę lub chce go rozszerzyć; (5) pyta o warunkową widoczność, formatowanie warunkowe (Appearance), bindowanie danych lub wzorce UI w Soneta.
* [containers](../containers/SKILL.md) - Uruchamianie i wdrażanie platformy Soneta w kontenerach (bez odwołań do kodu programu,
  bez dostępu do kodu). Trzy ścieżki: **docker compose** (główna), **Apple `container` /
  Container Desktop** (macOS), **Helm / Kubernetes** (beta). Obejmuje: wybór wersji obrazów
  (`soneta/server.standard`, `web.standard` — Docker Hub lub `registry.soneta.pl`), tworzenie
  i zarządzanie bazą w kontenerze (usługa init z `dbmgr create`, `--demo`, `--recreate`),
  SQL zewnętrzny (`host.docker.internal` / `host.containers.internal`) lub kontener `mssql`.
  Używaj gdy użytkownik stawia środowisko test/demo na obrazach Soneta, pyta o
  `docker-compose.yaml`, Container Desktop, `helm install`, tag/wersję obrazu, albo o
  kolejność/host-alias/porty przy starcie stacku. Składnię samych komend `dbmgr` → [tools](../tools/SKILL.md).
