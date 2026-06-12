# Skill programowania Soneta — przygotowanie listy zadań domeny (wejście do dokumentacji)

## Parametry działania

> **Wszystkie parametry uzupełnij PRZED rozpoczęciem pracy.** Reszta promptu odwołuje się tylko
> do nich — nie wpisuj nazw domeny/obiektów na sztywno w treści zadania. Te same parametry
> (Domena, Główny obiekt, Prefiks, Zakres) muszą zgadzać się z promptem „Code-Snippets".

| Parametr | Wartość (uzupełnij)                                                                                                                                              | Opis |
|---|------------------------------------------------------------------------------------------------------------------------------------------------------------------|---|
| **Domena** | Workflow                                                                                                                                                         |
| **Główny obiekt biznesowy** | `Soneta.Business.Db.Task`, `Soneta.Workflow.WFWorkflow`                                                                                                          | Klasa `Row`/`GuidedRow` będąca rdzeniem listy. |
| **Prefiks kodów receptur** | `WORKFLOW`                                                                                                                                                       | Prefiks używany później w etapie 2 (`KADRY-A1`). Litera sekcji listy = litera receptury. |
| **Zakres** | Workflow, procesy, zadania                                                                                                                                       | Granice tematyczne — co wchodzi, a co nie. |
| **Plik wyjściowy** | `lista-zadan-workflow.md`                                                                                                                                        | Tu zapisz gotową listę (wejście do promptu „Code-Snippets"). |
| **Główne źródła kodu** | `@Soneta.Workflow/`, `@Soneta.Workflow.UI/`, `@Soneta.Workflow.Test/`, `@Soneta.Zadania/`, `@Soneta.Zadania.UI/`, `@Soneta.Zadania.Test/`, `@Soneta.Business/Db` | Miejsca pierwszego wyboru przy analizie. |
| **Zadania obowiązkowe** | (wklej poniżej)                                                                                                                                                  | Lista pozycji, które MUSZĄ się znaleźć — punkt wyjścia do rozszerzenia. |

## Zadanie do zrobienia

Przygotuj **listę zadań** dla domeny (parametr) — uporządkowany katalog procedur i operacji, jakie
można wykonać na **głównym obiekcie biznesowym** i obiektach powiązanych w ramach **zakresu**. Lista
będzie elementem skilla `/soneta-programming` i **wejściem do promptu „Code-Snippets"**, który na jej
podstawie wygeneruje receptury z kodem i testami.

**Na tym etapie NIE piszesz kodu i NIE projektujesz snippetów.** Opisujesz wyłącznie *co* można zrobić
(funkcjonalność z perspektywy biznesowej), nie *jak* to zakodować. Pola, kolekcje i workery wskazuj
tylko jako orientacyjny trop dla etapu 2, nie jako gotowe API.

## Skąd czerpać pozycje listy

1. **Formularze i listy w programie** — przejrzyj listy dokumentów/obiektów domeny, otwórz formularze,
   przejrzyj zakładki i czynności (menu „Czynności"). Każda zakładka i czynność to kandydat na pozycję.
2. **Workery** — akcje w programie są zaimplementowane przez Workery; ich tytuły to dobre nazwy zadań
   (pomocniczo: `/soneta-programming/scripts/scan-workers.csx`).
3. **Istniejące listy zadań** — `domeny/kadry.md`, `domeny/handel.md` (mapy receptur) jako wzorzec
   zakresu i granulacji.
4. **Wiedza biznesowa** — typowe procesy domeny, nawet jeśli nieoczywiste w UI.

## Forma wyjścia (struktura listy)

Lista jest **pogrupowana w sekcje literowane** (A, B, C…) — **każda sekcja stanie się rozdziałem**
w etapie 2, a jej litera to litera kodu receptury (`<PREFIKS>-A1`, `<PREFIKS>-A2`, …). Grupuj zadania
tematycznie (np. dane kadrowe, etat, nieobecności, umowy, czas pracy, wypłaty), w kolejności od
fundamentów do operacji złożonych.

Dla każdej sekcji podaj nagłówek `## A. <tytuł sekcji>`, a pod nim numerowane pozycje. Każda pozycja:

- **Tytuł zadania** — zwięzły, czasownikowy (np. „Zatrudnienie nowego pracownika").
- **Obiekt(y)** — główny obiekt i obiekty powiązane, których zadanie dotyczy.
- **Trop** — orientacyjnie: zakładka formularza / tytuł workera / kolekcja (bez kodu, jedno zdanie).
- **Warianty** — jeśli zadanie ma odmiany (np. umowa zlecenie / o dzieło), wypunktuj je.

Zakończ listę sekcją **„Operacje przekrojowe"** — typowe czynności techniczne powtarzające się w kodzie
domeny (np. otwarcie sesji i transakcja, dodanie wiersza do `SubTable`, wywołanie workera, blokada
optymistyczna, filtrowanie listy `RowCondition`, wydruk/PDF). To zasili „Pułapki" i odsyłacze etapu 2.

## Zadania obowiązkowe (punkt wyjścia)

Poniższe pozycje MUSZĄ trafić na listę; rozmieść je we właściwych sekcjach i **uzupełnij o brakujące**:

* tworzenie nowego zadania.
* tworzenie nowej definicji procesu workflow.
* tworzenie węzłów w konkretnym procesie workflow.
* tworzenie tranzycji pomiędzy węzłami procesu workflow.
* odczytanie aktualnych zadań dla operatora.
* odczytanie szczegółów zadania dla operatora, na przykład jakie operacje może wykonać.
* wykonanie konkretnego zadania.
* znajdowanie dokumentu powiązanego do zadania.

## Weryfikacja końcowa

* Każde zadanie obowiązkowe jest na liście, przypisane do sekcji.
* Lista jest pogrupowana w sekcje literowane, gotowe na numerację rozdziałów i kody `<PREFIKS>-Xn`.
* Każda pozycja ma tytuł, obiekt(y) i trop; warianty tam, gdzie występują.
* Jest sekcja „Operacje przekrojowe".
* Brak kodu i snippetów — sama lista *co*, nie *jak*.
* Zakres pokrywa to, co widać na formularzach/listach i w workerach domeny; luki uzupełnione wiedzą biznesową.
* Pozycje sformułowane neutralnie domenowo (działają dla innej domeny po zmianie parametrów).