---
name: soneta-erp
description: >
  Mapa i przewodnik po wyspecjalizowanych skillach do pracy z platformą Soneta ERP
  (enova365, Soneta Enterprise, Triva). Pomaga wybrać właściwy skill w zależności od
  zadania: programowanie ORM (soneta-programming), planowanie dodatków
  (soneta-addon-planning), definicje obiektów biznesowych (soneta-business-xml),
  formularze UI (soneta-form-xml), definicje elementów wynagrodzenia
  (soneta-place-def-elementow). Używaj tego skilla ZAWSZE gdy użytkownik: (1) rozpoczyna
  nowe zadanie związane z enova365/Soneta/Triva i nie jest jasne, który wyspecjalizowany
  skill zastosować; (2) pyta ogólnie o tworzenie dodatków, modułów lub rozszerzeń dla
  Soneta ERP; (3) wspomina o platformie enova, Soneta Enterprise, Triva bez sprecyzowania
  warstwy (dane, UI, logika, płace); (4) chce poznać dostępne narzędzia/skille do pracy
  z ekosystemem Soneta; (5) realizuje zadanie obejmujące wiele warstw platformy
  (np. nowy moduł z bazą danych, formularzami i logiką) i potrzebuje koordynacji między
  skillami.
---

# Mapa skills podczas pracy z Soneta ERP (enova/Triva)

* `/soneta-programming` - Fundamentalne klasy ORM platformy enova365/Soneta Enterprise. Obejmuje mapowanie 
obiektowo-relacyjne (Row, Table, Module), zarządzanie sesją (Session), logowanie (Login, Database, BusApplication), 
paczki danych (Datapack, GuidedRow) oraz kontekst (Context). Używaj gdy użytkownik pyta o podstawowe klasy logiki 
biznesowej, strukturę obiektów ORM, sesje i transakcje, hierarchię klas Row/Table/Module, mechanizm Datapack i 
synchronizację danych, lub kontekst aplikacji enova365, Context 
* `/soneta-addon-planning` - Planowanie projektów dodatków dla platformy enova365/Soneta Enterprise. Tworzy
  kompletną dokumentację projektową obejmującą: strukturę danych (tabele, relacje),
  elementy konfigurowalne, definicje list i menu, formularze, workery i raporty.
  Używaj gdy użytkownik prosi o zaplanowanie nowego modułu/dodatku enova365,
  przygotowanie założeń projektu, stworzenie specyfikacji funkcjonalnej dodatku,
  lub zdefiniowanie struktury danych i interfejsu użytkownika dla nowego modułu.
*  `/soneta-business-xml` - Generator plików business.xml dla platform Soneta (enova365, Soneta Enterprise).
   Tworzy definicje obiektów biznesowych (tabel, kolumn, relacji, indeksów) zgodne
   ze schematem XSD. Używaj gdy użytkownik prosi o stworzenie nowego modułu biznesowego,
   zdefiniowanie obiektów lub encji do przechowywania w bazie danych, utworzenie relacji
   między obiektami, lub generowanie plików business.xml dla enova365/Soneta Enterprise.
* `/soneta-form-xml` - XML z nieistniejącymi elementami. ZAWSZE używaj tego skilla gdy użytkownik: (1) prosi o utworzenie lub modyfikację pliku pageform.xml, viewform.xml, form.xml, lookupform.xml lub gridform.xml dla enova365/Soneta; (2) pyta o elementy DataForm, Page, Group, Grid, Field, Row, Stack, Flow, Command, Include, Appearance, GroupBy w enova365; (3) pyta o składnię EditValue, DataContext, Visibility, RowCondition, Renderable, CaptionHtml, Footer, Class lub układ UI formularzy enova365; (4) pokazuje istniejący plik form.xml/pageform.xml/viewform.xml i pyta o jego strukturę lub chce go rozszerzyć; (5) pyta o warunkową widoczność, formatowanie warunkowe (Appearance), bindowanie danych lub wzorce UI w Soneta/enova365.
