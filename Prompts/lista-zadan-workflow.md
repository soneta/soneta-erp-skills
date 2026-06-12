# Lista zadań domeny Workflow (Soneta / enova365)

Uporządkowany katalog procedur i operacji, jakie można wykonać na obiektach domeny **Workflow**
i powiązanych z nią **zadaniach** (`Task`). Lista jest **wejściem do promptu „Code-Snippets"** —
opisuje *co* można zrobić (perspektywa biznesowa), a nie *jak* to zakodować. Pola, kolekcje i workery
są podane wyłącznie jako **orientacyjny trop** dla etapu 2.

- **Domena:** Workflow
- **Główne obiekty biznesowe:** `Soneta.Business.Db.Task` (tabela `Tasks`, caption „Zadanie"),
  `Soneta.Workflow.WFWorkflow` (tabela `WFWorkflows`, caption „Proces").
- **Prefiks kodów receptur:** `WORKFLOW` (litera sekcji = litera receptury, np. `WORKFLOW-A1`).
- **Zakres:** workflow, definicje procesów, węzły, tranzycje, procesy (instancje), zadania operatora.
- **Główne źródła kodu:** `Soneta.Workflow/`, `Soneta.Workflow.UI/`, `Soneta.Workflow.Test/`,
  `Soneta.Zadania/`, `Soneta.Zadania.UI/`, `Soneta.Zadania.Test/`, `Soneta.Business/Db`.

## Mapa pojęć domeny (zweryfikowana w business.xml)

| Pojęcie biznesowe | Klasa `Row` | Tabela | Rodzaj danych | Moduł |
|---|---|---|---|---|
| Definicja procesu (szablon) | `WFDefinition` | `WFDefs` | konfiguracja | `WorkflowModule` |
| Węzeł procesu = definicja zadania | `TaskDefinition` | `TaskDefs` | konfiguracja | `Soneta.Business.Db` |
| Tranzycja (przejście między węzłami) | `WFTransition` | `WFTransitions` | konfiguracja | `WorkflowModule` |
| Wzorzec tranzycji | `WFTransitionDefinition` | `WFTransitionDefs` | konfiguracja | `WorkflowModule` |
| Schemat generatora obiektów | `OGSchema` | `OGSchemas` | konfiguracja | `WorkflowModule` |
| Rola procesowa (tor diagramu) | `WFProcessRole` | `WFProcessRoles` | konfiguracja | `WorkflowModule` |
| Proces (instancja uruchomiona) | `WFWorkflow` | `WFWorkflows` | operacyjne | `WorkflowModule` |
| Zadanie (egzemplarz węzła) | `Task` | `Tasks` | operacyjne | `Soneta.Business.Db` |
| Zadanie CRM / aktywność | `Zadanie` | `Zadania` | operacyjne | `ZadaniaModule` |
| Sprawa (DMS) | `Matter` | `Matters` | operacyjne | `DmsModule` |

> **Dostęp z sesji:** `session.GetWorkflow()` (`WorkflowModule`), `session.GetBusiness()`
> (tabela `Tasks`, `TaskDefs`), `session.GetZadania()` (`ZadaniaModule`).
> **Uwaga terminologiczna:** „węzeł" procesu workflow jest reprezentowany przez `TaskDefinition`
> (definicja zadania), a uruchomiony węzeł — przez `Task`. Silnik (`StandardEngine`,
> `TaskCalculatorForWorkflow`) tworzy egzemplarze `Task` na podstawie `TaskDefinition`.

---

## A. Definicja procesu workflow (projektowanie)

1. **Utworzenie nowej definicji procesu workflow** *(obowiązkowe)*
   - **Obiekt(y):** `WFDefinition` (`WFDefs`).
   - **Trop:** kolekcja `WorkflowModule.WFDefs`; pola `Symbol`, `Name`, `DefinitionType`
     (Standard / Engine), `Numerator`, `SingleWorkflowInstance`, `IsDeployed`.
   - **Warianty:** definicja standardowa (modelowana na diagramie) / definicja typu Engine (kodowa).

2. **Konfiguracja trybu i sposobu edycji definicji**
   - **Obiekt(y):** `WFDefinition`.
   - **Trop:** pola `EditType`, `IsDiagramEditedInHtml`; worker `ChangeWFDefinitionModelingTypeWorker`
     („zmiana trybu edycji Standard/Engine").

3. **Definiowanie ról procesowych (torów diagramu)**
   - **Obiekt(y):** `WFProcessRole` (`WFProcessRoles`), powiązane z `WFDefinition`.
   - **Trop:** rola procesowa jako nowy tor na edytorze; przypisanie operatora/roli/węzła org.

4. **Projektowanie diagramu procesu**
   - **Obiekt(y):** `WFDefinition` (pole `SerializedDiagram`).
   - **Trop:** worker `DiagramDesignerProgressWorker`; folder `Soneta.Workflow/Diagram/`.

5. **Kopiowanie definicji procesu**
   - **Obiekt(y):** `WFDefinition`.
   - **Trop:** worker `WFDefinitionCopyWorker`.

6. **Import / eksport definicji procesu**
   - **Obiekt(y):** `WFDefinition` (+ węzły, tranzycje, schematy).
   - **Trop:** workery `WorkflowImportWorker`, `WorkflowExportWorker`, `SerializeWFDefinitionWorker`.
   - **Warianty:** eksport do pliku / import z pliku / serializacja całego procesu.

7. **Wdrożenie definicji (przełączenie z modelowania na produkcję)**
   - **Obiekt(y):** `WFDefinition` (pole `IsDeployed`).
   - **Trop:** flaga trybu wdrożenia vs. modelowania.

## B. Węzły procesu (definicje zadań)

1. **Utworzenie węzła w konkretnym procesie workflow** *(obowiązkowe)*
   - **Obiekt(y):** `TaskDefinition` (`TaskDefs`), powiązany z `WFDefinition`.
   - **Trop:** kolekcja definicji zadań procesu; pola `Name`, `IsStart` (węzeł startowy),
     `OverdueHandling`, `MultiTaskType`, `EndType` (None/Task/Workflow).
   - **Warianty:** węzeł startowy / pośredni / końcowy; węzeł jedno- lub wielozadaniowy.

2. **Konfiguracja węzła startowego procesu**
   - **Obiekt(y):** `TaskDefinition` (`IsStart`).
   - **Trop:** punkt startowy procesu — `WFDefinition.GetStartPoint()`.

3. **Określenie wykonawcy węzła (operator / rola / węzeł organizacyjny)**
   - **Obiekt(y):** `TaskDefinition`, `WFProcessRole`.
   - **Trop:** przypisanie operatora, roli procesowej lub elementu struktury organizacyjnej do węzła
     (odzwierciedlone na egzemplarzu jako `Task.OperatorRoleType`).

4. **Konfiguracja źródła danych węzła i obiektu nadrzędnego**
   - **Obiekt(y):** `TaskDefinition`, `WfTaskSource`.
   - **Trop:** mapowanie obiektu biznesowego do zadania; executor `WeTaskSourceExecutor`.

5. **Podpięcie kreatora (wizard) do węzła**
   - **Obiekt(y):** `TaskDefinition`, `AdditionalWizardsRef`.
   - **Trop:** workery `CreateSimpleWizardWorker`, `OpenWizardOnTaskWorker`.

6. **Kopiowanie definicji zadania (węzła)**
   - **Obiekt(y):** `TaskDefinition`.
   - **Trop:** worker `CopyTaskDefinitionWorker`.

7. **Obsługa terminów i przeterminowania węzła**
   - **Obiekt(y):** `TaskDefinition` (`OverdueHandling`).
   - **Trop:** reakcja na przekroczenie terminu zadania.

## C. Tranzycje i przepływ procesu

1. **Utworzenie tranzycji pomiędzy węzłami procesu** *(obowiązkowe)*
   - **Obiekt(y):** `WFTransition` (`WFTransitions`); węzły `TaskDefinition` jako `Source` / `Target`.
   - **Trop:** kolekcja `WorkflowModule.WFTransitions`; pola `Name`, `LP`, `WfDefinition`,
     `Source`, `Target`.
   - **Warianty:** tranzycja zwykła / domyślna (`IsDefaultTransition`) / nie kończąca zadania
     (`WeakTransition`) / wielowariantowa (`VariantTypeName`).

2. **Definiowanie warunku realizacji tranzycji (algorytm)**
   - **Obiekt(y):** `WFTransition`.
   - **Trop:** pola `Statement` (kod kalkulatora realizacji), `CheckCode`, `ActionCode`.

3. **Tranzycja jako decyzja użytkownika**
   - **Obiekt(y):** `WFTransition` (`IsUserDecision`).
   - **Trop:** decyzja prezentowana operatorowi; pola `QuickAccess`, `Icon`, `ForeColor`.

4. **Definiowanie wariantów tranzycji**
   - **Obiekt(y):** `WFTransition` (`VariantTypeName`), `UserDecision.Variant`.
   - **Trop:** `WFTransitionCalculator.GetVariants(task)`.

5. **Konfiguracja schematu generatora obiektów (mapowanie danych)**
   - **Obiekt(y):** `OGSchema` (`OGSchemas`), powiązany z `WFTransition`.
   - **Trop:** pola `SourceType`, `TargetType`, `XML` (mapowanie pól), `CopyAttachments`, `CopyNotes`.
   - **Warianty:** generowanie nowego dokumentu z danych źródłowych / kopiowanie załączników i notatek.

6. **Użycie wzorca tranzycji (reużywalna definicja)**
   - **Obiekt(y):** `WFTransitionDefinition` (`WFTransitionDefs`).
   - **Trop:** podpięcie wzorca do `WFTransition`.

7. **Obliczenie dostępnych tranzycji dla zadania**
   - **Obiekt(y):** `Task`, `WFTransition`, `WFTransitionCalculator`.
   - **Trop:** worker `TransitionsForTasksWorker`; metody `IsVisible`, `IsReadOnly`, `IsRealized`.

## D. Proces workflow — uruchamianie i cykl życia

1. **Uruchomienie procesu workflow (utworzenie instancji)**
   - **Obiekt(y):** `WFWorkflow` (`WFWorkflows`), `WFDefinition`, `TaskDefinition`.
   - **Trop:** `WFDefinition.GetStartPoint().StartProcess(...)` / `StartProcessUI(...)`; worker/kafelek
     `ProcessWorkflowDashboardWorker` („start procesu na pulpicie").
   - **Warianty:** start manualny (menu/panel) / automatyczny (`AutoStart` na `OnAdded`) /
     harmonogram (`IScheduleAutoJob.AutoAction`) / podproces z zadania.

2. **Start procesu z poziomu dokumentu / obiektu biznesowego**
   - **Obiekt(y):** `WFWorkflow`, dowolny `GuidedRow`.
   - **Trop:** worker `GuidedRowProcessStartWorker`; powiązanie przez `WFWorkflow.ManagingRow`.

3. **Powiązanie procesu z dokumentem zarządzającym**
   - **Obiekt(y):** `WFWorkflow` (`ManagingRow : IManagingRow`).
   - **Trop:** wiersz zarządzający procesem (np. dokument handlowy, kontrahent).

4. **Uruchomienie podprocesu z zadania**
   - **Obiekt(y):** `Task`, `WFWorkflow`.
   - **Trop:** `TaskDefinition.StartSubprocess(mainTask, context)`; flaga węzła `IsSubprocess`.

5. **Odczyt stanu i postępu procesu**
   - **Obiekt(y):** `WFWorkflow`.
   - **Trop:** pola `IsClosed`, `DateFrom`/`DateTo`, kolekcje `AllTasks`, `LiveTasks`, `ActiveTasks`;
     worker `WFWorkflowStageWorker` („etap procesu i odpowiedzialny").

6. **Zamknięcie / zakończenie procesu**
   - **Obiekt(y):** `WFWorkflow`.
   - **Trop:** `TryFinishProcess()`, `Process.Terminate()`; brak dostępnych tranzycji lub węzeł końcowy.

7. **Anulowanie / usunięcie procesów i ich zadań**
   - **Obiekt(y):** `WFWorkflow`, `Task`.
   - **Trop:** `WFWorkflow.DeleteProcessesWorker`; prawo proste `IsSimpleRightDeleteWorkflowTasksGranted`.

8. **Seryjne uruchomienie procesu dla wielu obiektów**
   - **Obiekt(y):** `WFWorkflow`, `GuidedRow` (zaznaczone wiersze).
   - **Trop:** worker `GuidedTableProcessStartWorker`; filtr `Definition.IncompatibleWithSerialOperations`.

## E. Zadania operatora — odczyt i przegląd

1. **Odczytanie aktualnych zadań dla operatora** *(obowiązkowe)*
   - **Obiekt(y):** `Task` (`Tasks`).
   - **Trop:** `Tasks.IsLoggedUserTask(task)`; klucze `WgTaskUser[taskUser]`, `WgParent`,
     `WgDefinition`; filtr `Progress == Active`, `Notification <= now`, `ValidFrom <= today`.
   - **Warianty:** zadania przypisane do operatora / roli (`RoleGuid`) / węzła org. (`Node`) /
     użytkownika sieciowego (`TaskUser`).

2. **Przegląd zadań na panelu / pulpicie Workflow**
   - **Obiekt(y):** `Task`, `WFWorkflow`.
   - **Trop:** `WorkflowPanelViewInfo`, `WorkflowPanelExtender`, `WorkflowPanelMobile`;
     enum `NotificationsOptionsEnum` (Aktywne/Wszystkie).

3. **Filtrowanie listy zadań workflow**
   - **Obiekt(y):** `Task`.
   - **Trop:** `TasksViewInfo`; widok na `BusinessModule.Tasks` z warunkami workflow (`RowCondition`).

4. **Odczyt zadań powiązanych z konkretnym obiektem (rodzicem)**
   - **Obiekt(y):** `Task`, `IGuidedRow`.
   - **Trop:** `Tasks.WgParent[guidedRow]`.

5. **Obsługa przypomnień i powiadomień o zadaniach**
   - **Obiekt(y):** `Task`.
   - **Trop:** pola `Notification`, `NotificationDate`/`NotificationTime`, `IsNotification`;
     metoda `Tasks.GetRemainders()`.

## F. Realizacja zadania workflow

1. **Odczytanie szczegółów zadania — dostępne operacje** *(obowiązkowe)*
   - **Obiekt(y):** `Task`, `UserDecision`, `WFTransition`.
   - **Trop:** `Task.GetUserDecisions(withNoDecision, onlyAvailable)` → kolekcja `UserDecision`
     (`Caption`, `IsEnabled`, `IsVisible`); `Task.GetActionMethods(context)` (czynności menu).
   - **Warianty:** decyzje użytkownika (tranzycje) / akcje (czynności) / warianty decyzji.

2. **Wykonanie konkretnego zadania (podjęcie decyzji)** *(obowiązkowe)*
   - **Obiekt(y):** `Task`, `WFTransition`, `WFWorkflow`.
   - **Trop:** `Task.SetUserDecision(userDecision, context)`; worker `TransitionsForTasksWorker`
     (akcja „Podejmij decyzję"); `Task.Recalculate()` → silnik tworzy zadania następnych węzłów.
   - **Warianty:** decyzja jednoznaczna / z wyborem wariantu / z parametrami (QueryContext).

3. **Zmiana stanu zadania (postępu)**
   - **Obiekt(y):** `Task` (`Progress`).
   - **Trop:** `Task.SetProgress(value)`; stany `Active`, `Realized`, `NonActive`, `Waiting`, `Aborted`.

4. **Przypisanie zadania sobie**
   - **Obiekt(y):** `Task`.
   - **Trop:** worker `AssignForYourselfWorker`.

5. **Przejęcie zadania od innego operatora**
   - **Obiekt(y):** `Task` (`TakeOverOperator`, `TakeOverITaskUser`).
   - **Trop:** worker `TakeOverTaskWorker`.

6. **Odrzucenie zadania**
   - **Obiekt(y):** `Task`.
   - **Trop:** worker `RejectTaskWorker`.

7. **Potwierdzenie powiadomienia / zadania informacyjnego**
   - **Obiekt(y):** `Task`.
   - **Trop:** worker `ConfirmTaskWorker`.

8. **Uruchomienie kreatora przypisanego do zadania**
   - **Obiekt(y):** `Task`.
   - **Trop:** worker `OpenWizardOnTaskWorker`.

9. **Seryjne podejmowanie decyzji na wielu zadaniach**
   - **Obiekt(y):** `Task`, `WFTransition`, `UserDecision`.
   - **Trop:** `MultiSourceDecisionManager`; filtr `Transition.IncompatibleWithSerialOperations`.
   - **Warianty:** ta sama decyzja dla wielu zadań / wiele źródeł danych na jednym zadaniu.

## G. Powiązania zadania z dokumentami i obiektami

1. **Znajdowanie dokumentu powiązanego z zadaniem** *(obowiązkowe)*
   - **Obiekt(y):** `Task`, `IGuidedRow`, `TaskLinkedObj`.
   - **Trop:** pole `Task.Parent` (główny obiekt), kolekcja `Task.LinkedObjects`
     (`TaskLinkedObj.LinkedObject`, `IsParent`).
   - **Warianty:** obiekt główny (Parent) / obiekty dodatkowo powiązane (LinkedObjects).

2. **Nawigacja z zadania do procesu i jego definicji**
   - **Obiekt(y):** `Task`, `WFWorkflow`, `TaskDefinition`, `WFDefinition`.
   - **Trop:** `Task.WFWorkflow`, `Task.Definition`, `Task.Definition.WFDefinition`;
     worker `TaskGoToWorkflowWorker`.

3. **Aktualizacja powiązanych obiektów zadania**
   - **Obiekt(y):** `Task`, `TaskLinkedObj`.
   - **Trop:** `Task.UpdateLinkedObjects(objs)` (wywoływane przez kalkulator).

4. **Odczyt informacji o workflow dla dowolnego wiersza**
   - **Obiekt(y):** dowolny `GuidedRow`, `WFWorkflow`.
   - **Trop:** worker `RowWorkflowInfoWorker`.

## H. Zadania CRM (aktywności) — wątek niezależny od workflow

1. **Utworzenie nowego zadania** *(obowiązkowe)*
   - **Obiekt(y):** `Zadanie` (`Zadania`), `DefZadania`.
   - **Trop:** kolekcja `ZadaniaModule.Zadania`; pola `Nazwa`, `Definicja`, `Prowadzacy`,
     `Wykonujacy`, `DataOd`/`DataDo`, `StanZadania`, `Priorytet`, `Kontrahent`.
   - **Warianty:** zadanie / zdarzenie / zlecenie / rezerwacja zasobu; jednorazowe / cykliczne.

2. **Zmiana stanu zadania CRM**
   - **Obiekt(y):** `Zadanie` (`StanZadania`).
   - **Trop:** worker `ZadaniaZmienStanWorker`; konfiguracja stanów `StanyZadania`, dozwolone
     przejścia `AvaliableState`; enum `TaskStateType` (Open/Completed/Rejected).

3. **Tworzenie zadań cyklicznych / kopiowanie zadań**
   - **Obiekt(y):** `Zadanie`.
   - **Trop:** workery `ZadanieUtworzZadaniaWorker`, `CopyingActivitiesWorker`.

4. **Budowanie hierarchii podzadań**
   - **Obiekt(y):** `Zadanie` (`Nadrzedne`, `TypNadrzednego`).
   - **Trop:** workery `ZadanieDolaczZadaniePodrzedneWorker`, `TaskHierarchyWorker`.

5. **Powiązanie zadania z kontrahentem / projektem / kampanią**
   - **Obiekt(y):** `Zadanie`, `Kontrahent`, `Projekt`, `Kampania`.
   - **Trop:** workery `KontrahentDodajAktywnoscWorker`, `ProjektDolaczZadanieWorker`;
     kolekcje `Kontrahent.Zadania`, `Projekt.Zadania`.

6. **Rejestracja czasu pracy nad zadaniem**
   - **Obiekt(y):** `Zadanie` (`ITimeTrack`, `CzasWykonania`).
   - **Trop:** workery `ZadaniaStoperWorker`, `MyActivityAddTimeTrackWorker`.

7. **Powiązanie dokumentów z zadaniem CRM**
   - **Obiekt(y):** `Zadanie` (`IDocumentHostCRM`), `DokumentCRM`.
   - **Trop:** kolekcja `DokPowiazane`; worker `ZadaniePokazDokumentCRMWorker`.

## I. Powiadomienia, triggery i automatyzacja procesu

1. **Wysyłanie powiadomienia systemowego z węzła procesu**
   - **Obiekt(y):** `WFWorkflow`, `Task`, definicja powiadomienia węzła.
   - **Trop:** executor `WeSysNotificationExecutor`; kalkulator `WfAdvSysNotificationCalculator`;
     zdarzenia `When` / `Recipients` powiadomienia.
   - **Warianty:** powiadomienie systemowe / e-mail (`WeEmailNotification`) / SMS (`WeSmsNotification`) /
     przez plugin (`WePlugInNotification`).

2. **Definiowanie triggera (wyzwalacza) procesu**
   - **Obiekt(y):** `WFDefinition`, `TaskDefinition`, `GuidedRow` źródłowy.
   - **Trop:** executor `WeTriggerExecutor`; `WFAdvTaskTriggerCalculator`, `WFAdvTaskTriggerExtender`.

3. **Automatyczny start procesu na zdarzenie**
   - **Obiekt(y):** `WFWorkflow`, `WFDefinition`.
   - **Trop:** `WeStartAuto` (start na `OnAdded` źródła); flaga `AutoStart` definicji.

4. **Automatyczna tranzycja (bez decyzji użytkownika)**
   - **Obiekt(y):** `WFTransition`, `Task`.
   - **Trop:** `WeAutomaticTransition`; tranzycja realizowana warunkiem `Statement` bez udziału operatora.

5. **Uruchamianie procesu z harmonogramu**
   - **Obiekt(y):** `WFWorkflow`, `WFDefinition`.
   - **Trop:** `IScheduleAutoJob.AutoAction` (zadanie cykliczne harmonogramu).

## J. Sprawy (DMS / Matters) i rejestr

1. **Założenie sprawy i powiązanie z procesem workflow**
   - **Obiekt(y):** `Matter` (`DmsModule`), `WFWorkflow`.
   - **Trop:** `MatterCaseWorker`; definicja sprawy (`MatterDefinition`); `session.GetDms()`.
   - **Warianty:** sprawa zakładana ręcznie / generowana z procesu / z dokumentu rejestru.

2. **Rejestracja dokumentu w sprawie / rejestrze**
   - **Obiekt(y):** `Matter`, `BasicDocument`, dokument rejestru.
   - **Trop:** rejestr ujednolicony; `UnifiedRegisterClassVerifier`.

3. **Przegląd spraw na liście DMS**
   - **Obiekt(y):** `Matter`.
   - **Trop:** `MattersViewInfo` (FolderView „DMS/Sprawy").

4. **Śledzenie zmian i historii sprawy**
   - **Obiekt(y):** `Matter`.
   - **Trop:** `MatterCaseChangeInfo` (rejestr zmian sprawy).

---

## Operacje przekrojowe

Techniczne czynności powtarzające się w kodzie domeny — zasilają „Pułapki" i odsyłacze etapu 2:

- **Otwarcie sesji i transakcja biznesowa** — `login.CreateSession(...)`, `session.Logout(editMode:true)`,
  `Commit()` / `CommitUI()`, `session.Save()` (blokada optymistyczna). Patrz `session-login.md`.
- **Praca z konfiguracją vs. dane operacyjne** — definicje (`WFDefinition`, `TaskDefinition`,
  `WFTransition`) to dane `config="true"`; procesy i zadania (`WFWorkflow`, `Task`) to dane operacyjne.
- **Dodanie wiersza do kolekcji `SubTable`** — np. tranzycji do procesu, zadania do procesu.
- **Filtrowanie list serwerowo (`RowCondition`)** — zadania operatora, procesy aktywne; zamiast `View`
  używaj `SubTable[condition]` w kodzie biznesowym. Patrz `rowcondition.md`.
- **Dostęp przez klucze (`Wg…`)** — `Tasks.WgParent`, `Tasks.WgTaskUser`, `Tasks.WgDefinition`,
  `WFWorkflows.WgOperator`.
- **Wywołanie workera / akcji menu Czynności** — start procesu, podjęcie decyzji, przejęcie zadania.
  Patrz `worker-extender.md`, `action-result.md`.
- **Wymuszenie przeliczenia stanu zadania** — `Task.Recalculate()` (silnik `TaskCalculatorForWorkflow`).
- **Algorytmy w definicjach** — pola `Statement`, `CheckCode`, `ActionCode` (`AlgorithmColumn`).
- **Wykrywanie tożsamości tabeli między sesjami** — porównanie `Row.Table.TableInfo`.
- **Tłumaczenia i logowanie** — `Translate`, `ILogger`. Patrz `translations-logging.md`.

## Weryfikacja końcowa

- ✅ Wszystkie zadania obowiązkowe na liście i przypisane do sekcji: tworzenie zadania (H1),
  tworzenie definicji procesu (A1), tworzenie węzłów (B1), tworzenie tranzycji (C1), odczyt aktualnych
  zadań operatora (E1), odczyt szczegółów/operacji zadania (F1), wykonanie zadania (F2),
  znajdowanie dokumentu powiązanego (G1).
- ✅ Sekcje literowane A–J, gotowe na numerację `WORKFLOW-Xn` (I — powiadomienia/triggery,
  J — sprawy DMS; operacje seryjne w D8/F9).
- ✅ Każda pozycja ma tytuł, obiekt(y) i trop; warianty tam, gdzie występują.
- ✅ Jest sekcja „Operacje przekrojowe".
- ✅ Brak kodu i snippetów — sama lista *co*, nie *jak*.
