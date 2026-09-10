# Zasady projektowania okien (UX) i weryfikacja wizualna

Jak dobierać etykiety, gdzie je umieszczać i jak sprawdzić efekt na żywej aplikacji.
Składnia elementów: [../SKILL.md](../SKILL.md).

## Etykiety i opisy — formularze konfiguracyjne vs operacyjne

Długość etykiet (`CaptionHtml`) i obecność opisów dobieraj do **częstotliwości użycia** formularza:

- **Formularze konfiguracyjne** (definicje, ustawienia, parametry — odwiedzane **rzadko**): etykiety
  i pola opisowe powinny być **wyczerpujące i samotłumaczące**. Użytkownik wracający na taki formularz
  po tygodniach/miesiącach musi bez wahania zrozumieć, do czego służy każde pole — nie może polegać na
  pamięci. Preferuj pełne, jednoznaczne etykiety; tam, gdzie sens nie jest oczywisty, dodaj tekst
  pomocniczy/opis (np. rozbudowany `CaptionHtml`, etykieta wprowadzająca `Label`, opis w grupie).
  Priorytetem jest **zrozumiałość**, nie gęstość.

- **Formularze operacyjne** (dokumenty, kartoteki używane **codziennie**): etykiety powinny być
  **krótkie i zwięzłe**. Tu najważniejsza jest **przestrzeń na dane** — czytelność wprowadzanych
  wartości i to, by na ekranie mieściło się ich dużo (mniej przewijania i przełączania między
  zakładkami). Użytkownik zna te pola z codziennej pracy, więc rozwlekłe etykiety tylko zabierają
  miejsce. Priorytetem jest **zwięzłość i gęstość danych**.

## Pozycja etykiety względem pola — `LabelLeft` vs `LabelTop`

Na zakładkach formularzy obiektów **generalną zasadą jest umieszczanie etykiety po lewej stronie** pola
edycyjnego (`Class="LabelLeft"`). Jest to wartość **domyślna** dla formularzy — jeśli nie ustawisz nic
innego, obowiązuje układ z etykietą po lewej.

Wyjątki, w których etykieta trafia **na górę, nad pole edycyjne** (`Class="LabelTop"`):

- **Formularze aplikacji pulpitowej** uruchamianej dla operatorów pulpitowych — tu `LabelTop` jest
  wartością **domyślną**: etykiety umieszczamy nad polami edycyjnymi.
- **Pola filtrujące listy** (pasek parametrów `Flow Class="DataBar"`) — patrz
  [collections-grids.md](collections-grids.md).

## Wizualna weryfikacja formularza (buscall)

Po zdefiniowaniu lub zmianie `form.xml` (i przebudowaniu projektu `.UI`) **zweryfikuj wygląd wizualnie**
na żywej aplikacji — nie polegaj wyłącznie na poprawności XML. Otwórz formularz zdalnie i zrób zrzut
ekranu narzędziem `buscall` (`call navigate_to_folder`/`open_form` → `call take_screenshot`), a następnie
**obejrzyj PNG** i oceń estetykę i czytelność układu:

- **Odstępy etykieta–pole** nie są zbyt duże — łatwo dopasować etykietę do jej pola.
- **Etykiety są czytelne i w całości widoczne** — jest wystarczająco miejsca na tekst etykiety
  (nie jest ucięty ani zawinięty w nieczytelny sposób).
- **Pola są wyrównane** — ułożone równo jedno pod drugim, kolumny się zgadzają, całość wygląda schludnie.

Weryfikacja jest szczególnie ważna po zbudowaniu **układu wielokolumnowego** (`OuterWidth` —
patrz [../SKILL.md](../SKILL.md)), bo rozjechane kolumny widać dopiero na ekranie.

Pełna procedura sterowania aplikacją i robienia zrzutów (konfiguracja bazy, uruchamianie, `take_screenshot`):
narzędzie `buscall` — dokument [buscall-live-testing.md](../../programming/references/buscall-live-testing.md) (weryfikacja na żywej aplikacji); składnia metod w [buscall.md](../../tools/references/buscall.md).

## Powiązania

- [../SKILL.md](../SKILL.md) — układ wielokolumnowy (`OuterWidth`), kontenery, `Class`.
- [collections-grids.md](collections-grids.md) — pasek filtra listy i etykiety w `DataBar`.
- [examples.md](examples.md) — kompletne pliki formularzy.
- [buscall-live-testing.md](../../programming/references/buscall-live-testing.md); [buscall.md](../../tools/references/buscall.md).
