# Rejestr konfiguracji (ConfigReg)

Rejestr konfiguracji to **zrzut całej konfiguracji bazy danych do jednego drzewa**, które można
zapisać do pliku `*.reg.json`, porównać z inną bazą, scalić i wgrać z powrotem. Służy do
wersjonowania ustawień, śledzenia zmian i przenoszenia konfiguracji między bazami
(testowa → operacyjna, wzorcowa → wdrożeniowa). W programie: **Narzędzia → Zarządzanie
konfiguracją**; zapisane rejestry widać na wbudowanej liście rejestrów.

## Rejestr czy XML — który mechanizm wybrać

Platforma ma **dwa niezależne** mechanizmy przenoszenia ustawień. Mają rozłączne formaty plików
i nie da się ich mieszać.

| | Rejestr konfiguracji | Import/eksport XML |
|---|---|---|
| Zakres | stan konfiguracji **jako całość** albo wybrane gałęzie | **wskazane** rekordy i ich powiązania |
| Plik | `*.reg.json` | `<session>` / `*.dbinit.xml` |
| Wykrywanie różnic | tak — porównanie bazy ze zrzutem | nie |
| Scalanie i usuwanie | tak, z kontrolą stanu każdego węzła | nie, tylko dodawanie i aktualizacja |
| Typowe użycie | wdrożenie, audyt zmian, wersjonowanie ustawień w repozytorium | dane demo, paczka startowa, punktowa migracja rekordów |
| Opis | ten dokument | [import-export-xml.md](import-export-xml.md) |

Kryterium wyboru: **czy interesuje Cię różnica?** Jeśli chcesz wiedzieć, co zmieniło się od
poprzedniego zrzutu, albo doprowadzić bazę do zadanego stanu — rejestr. Jeśli chcesz po prostu
wstawić zestaw rekordów — XML.

## Pięć operacji

| Operacja | Co robi | Zapisuje do bazy |
|---|---|---|
| **Eksport całej bazy** | zrzuca cały rejestr do pliku `*.reg.json` | nie |
| **Eksport wybranych elementów** | zrzuca zaznaczone węzły; opcjonalnie dociąga wiersze powiązane („z powiązanymi", także w wariancie jednopoziomowym) | nie |
| **Zmiany w stosunku do wskazanego rejestru** | porównuje stan bazy ze zrzutem i pokazuje różnice | **nigdy** |
| **Scalenie** | nanosi zawartość pliku na stan bazy i pokazuje, co zostanie zapisane | tak, po zatwierdzeniu |
| **Podgląd drzewa** | pokazuje, co w ogóle wchodzi do rejestru i pod jaką ścieżką | nie |

Porównanie i podgląd są bezpieczne na bazie produkcyjnej. **Jedyną operacją zapisującą jest
scalenie.**

**Porównanie dwóch baz robi się dwuetapowo:** najpierw eksport bazy odniesienia do pliku, potem
porównanie drugiej bazy z tym plikiem. Znaczniki wyniku czyta się z perspektywy bazy
porównywanej: `[+]` jest tylko w niej, `[-]` jest tylko w pliku, `[*]` jest w obu, ale inaczej.

## Co wchodzi do rejestru

Rejestr obejmuje **tabele konfiguracyjne z rekordami identyfikowanymi GUID-em** oraz, obok nich,
trzy gałęzie szczególne:

- **konfigurację modułów** — węzły ustawień wraz z ich atrybutami,
- **magazyn plików** (definicje wydruków, szablony, pliki pomocnicze) wraz ze strukturą folderów,
- **dodatki** zarejestrowane w bazie.

Poza rejestrem zostają: dane transakcyjne, tabele oznaczone przez producenta jako wyłączone
oraz **pojedyncze pola wrażliwe i lokalne** — hasła, historia haseł, hasze licencji,
identyfikatory instalacji. Tabele bez ani jednego rekordu w ogóle nie pojawiają się w drzewie.

> **Uwaga przy eksporcie całej bazy.** Plik zawiera wszystko, co jest w rejestrze. Zanim
> wrzucisz go do repozytorium albo wyślesz poza firmę, przejrzyj go pod kątem danych
> wrażliwych — wycinane są wyłącznie pola wskazane przez producenta jako pomijane.

## Format pliku `*.reg.json`

Zwykły JSON; rozszerzenie `*.reg.json` służy tylko do filtrowania w oknie wyboru pliku.

### Koperta

```json
{
  "Version": 1,
  "Register": { "…": "…" }
}
```

`Version` zapisywane jest zawsze jako `1` i przy odczycie nie jest sprawdzane. `Register`
to korzeń drzewa; pod nim pojawiają się kolejno moduły.

### Sigile

Znaczniki sterujące zaczynają się od `$`, `#` lub `@`, żeby nie kolidować z nazwami pól.

| Sigil | Gdzie | Znaczenie |
|---|---|---|
| `#Nazwa` | klucz właściwości | Pole tożsamości. Po tych polach dopasowywane są wiersze przy porównywaniu i scalaniu. |
| `@Nazwa` | klucz właściwości | Atrybut węzła konfiguracji lub dodatku. |
| `"$": "added\|modified\|deleted"` | właściwość obiektu | Stan węzła. Brak = bez zmian. |
| `{"$v": …, "$": "…"}` | opakowanie wartości | Wartość, która poza treścią niesie stan. Bez stanu zapisuje się „gołą" wartością. |
| `"$strict"` | pierwszy element tablicy | Kolekcja określa stan docelowy **w całości** — patrz niżej. |
| `"$added"` / `"$modified"` / `"$deleted"` | element tablicy | Stan samej kolekcji. |
| `{"$blob": "nazwa1.txt"}` | wartość | Treść wyniesiona poza JSON — duże teksty i binaria. |

Poza JSON wynoszone są automatycznie: dłuższe pola tekstowe typu memo, teksty z co najmniej
dwoma znakami nowej linii oraz zawartość binarna od 16 bajtów. Plik ze zrzutem musi wtedy
podróżować **razem z plikami towarzyszącymi** — brak któregokolwiek z nich to błąd odczytu.

### Jak węzły odwzorowują się na JSON

| Węzeł | Postać w JSON-ie |
|---|---|
| Pojedyncza wartość | goła wartość albo `{"$v": …, "$": "…"}`, gdy niesie stan |
| Obiekt (moduł, wiersz, węzeł konfiguracji) | `{ … }`; najpierw pola `#klucz`, potem reszta |
| Kolekcja (tabela, lista podrzędna) | tablica `[ … ]`, poprzedzona `"$strict"` i/lub stanem |

Wiersze kolekcji nie mają własnej nazwy — występują wyłącznie jako elementy tablic.

### Wartości

| Typ | Zapis |
|---|---|
| Liczby | zapis liczbowy; typ docelowy (całkowita, zmiennoprzecinkowa, lista wyboru) ustala się dopiero przy scalaniu |
| Wartość logiczna | `true` / `false` |
| Lista wyboru | nazwa jako tekst; przy odczycie akceptowana jest też liczba |
| GUID | tekst |
| Odwołanie do innego wiersza | `Tabela:guid`, `Tabela:id`, albo samo `guid`/`id`, gdy typ kolumny jednoznacznie wskazuje tabelę |
| Dane binarne | Base64 albo `$blob` |
| Data, czas, miesiąc roku | tekst w formacie typu; wartość pusta odpowiada wartości minimalnej |

Polskie znaki i cudzysłowy w treściach XML zapisywane są bez ucieczek, więc plik pozostaje
czytelny w przeglądzie różnic.

### Ścieżki węzłów

Ścieżka to segmenty rozdzielone `/` — używa się jej przy podglądzie drzewa i przy wskazywaniu
gałęzi do eksportu.

| Zapis | Znaczenie |
|---|---|
| `/` | korzeń drzewa |
| `Business/FeatureDefs` | zejście po nazwach |
| `Name=CECHA` | element kolekcji, którego pole `Name` ma wartość `CECHA` |
| `TableName=Towary&Name=Dwa` | dopasowanie po wielu polach naraz |
| `#0` | element kolekcji po numerze porządkowym |
| `Guid=00000003-aaaa-…` | typowe wskazanie wiersza guidowanego |

Przykłady: `Business/FeatureDefs/Name=CECHA/Category`, `Business/Config/mail/@EMailAddress`,
`Core/OddzialyFirmy/Guid=00000011-…/Pododdzialy`, `Storage/reports/…/dokumenty.xml`.

W nazwach węzłów znaki `\`, `&` i `=` są poprzedzane ukośnikiem odwrotnym, a `/` zamieniany
na `-`. Dzięki temu nazwa pliku albo pola z ukośnikiem nie rozbija ścieżki — ale też nie
odnajdziesz jej po oryginalnym brzmieniu. Pliki magazynu niosą kontekst po przecinku:
`Storage/reports/…/dokumenty z pozycjami.xml,00000000-0015-0001-0001-000000000000`.

## `$strict` — kolekcja, która kasuje

Domyślnie tablica jest **przyrostowa**: potrafi wyłącznie dodawać i aktualizować wiersze,
nigdy nie usuwa. To bezpieczny tryb i mają go węzły tabel.

Kolekcja oznaczona `"$strict"` działa odwrotnie — **określa stan docelowy w całości**. Wiersze
nieobecne w pliku zostaną w bazie docelowej **usunięte**. Tak powstają domyślnie listy podrzędne
(pozycje wewnątrz wiersza nadrzędnego).

Dwie konsekwencje praktyczne:

- Zrzut **wybranej gałęzi** jest opakowywany w sztuczną ścieżkę od korzenia, a kolekcje `$strict`
  degradują się przy tym do przyrostowych. Taki plik **nie usunie** wierszy w bazie docelowej —
  nawet jeśli ich nie zawiera.
- Zapis jest odrzucany, gdy po scaleniu kolekcja `$strict` zawiera wiersze w stanie innym niż
  „dodany" albo „bez zmian". To zabezpieczenie, nie usterka — przeczytaj, których wierszy dotyczy.

## Dlaczego wiersz nie został dopasowany

Najczęstsza pułapka przy porównywaniu dwóch baz: ten sam logicznie wiersz pokazuje się
jednocześnie jako **usunięty i dodany**. Znaczy to, że mechanizm nie rozpoznał go jako tego
samego rekordu, więc skasuje go i wstawi na nowo — **z nowym GUID-em** i ryzykiem naruszenia
klucza unikalnego.

Tożsamość ustalana jest kolejno: po nazwie (dla węzłów nazwanych) → po wszystkich polach `#klucz`
→ po GUID-zie → po zgodności wszystkich wartości. Typowe przyczyny rozjazdu:

- bazy powstały niezależnie, więc ten sam wpis ma w każdej inny GUID,
- pole tożsamości jest **odwołaniem** do innego wiersza, który sam ma inny GUID w każdej bazie,
- wpis został ręcznie usunięty i wprowadzony ponownie w jednej z baz.

Rozwiązanie jest zawsze po stronie danych, nie pliku: doprowadź wiersze do wspólnej tożsamości
(np. przenosząc wzorzec jednym eksportem do obu baz), zamiast poprawiać zrzut ręcznie.

## Czego mechanizm jeszcze nie robi

- **Rozstrzyganie konfliktów nie jest dokończone.** Formularz pokazuje filtr „Tylko konflikty"
  i pole rozstrzygnięcia, ale logika ich rozwiązywania nie działa. Nie planuj wdrożenia
  opartego o ten krok.
- **Nie ma operacji odwrotnej do scalenia.** Wycofanie zmian to odtworzenie kopii bazy.

## Checklista — przeniesienie ustawień między bazami

- [ ] Obie bazy są w **tej samej wersji** logiki biznesowej.
- [ ] Ustalona ścieżka gałęzi przez podgląd drzewa — zanim wskażesz ją do eksportu.
- [ ] Eksport z bazy wzorcowej: cała baza albo wybrane elementy, w razie potrzeby „z powiązanymi".
- [ ] Plik przejrzany pod kątem danych wrażliwych, jeśli ma opuścić firmę lub trafić do repozytorium.
- [ ] Pliki towarzyszące (wyniesione treści `$blob`) przeniesione **razem** ze zrzutem.
- [ ] Porównanie bazy docelowej ze zrzutem — zanim cokolwiek scalisz.

## Checklista — przed scaleniem do bazy operacyjnej

- [ ] Wykonana kopia bezpieczeństwa bazy docelowej — scalenie omija część walidacji biznesowej
      i nie ma operacji odwrotnej.
- [ ] Scalenie uruchomione najpierw **bez zapisu** i przeczytane wyniki, nie tylko liczby.
- [ ] Zero ostrzeżeń blokujących; jeśli są — przyczyna zrozumiana, a nie obejściem wymuszona.
- [ ] Ostrzeżenia o kolekcjach `$strict` przejrzane pod kątem tego, **co zostanie usunięte**.
- [ ] Lista zmian obejrzana w całości, a nie w domyślnym przycięciu.
- [ ] Brak par „usunięty i dodany" dla tego samego wpisu — patrz „Dlaczego wiersz nie został
      dopasowany".

## Powiązania

- [import-export-xml.md](import-export-xml.md) — drugi mechanizm przenoszenia ustawień
  (`<session>`, `*.dbinit.xml`, eksport rekordów powiązanych).
- [tools](../../tools/SKILL.md) — operacje na bazach z wiersza poleceń: kopia
  bezpieczeństwa przed scaleniem i sprawdzenie wersji bazy z checklist powyżej, a także
  weryfikacja efektów na uruchomionej aplikacji.
- [programming](../../programming/SKILL.md) — rekordy guidowane i paczki danych,
  czyli warstwa, na której opiera się tożsamość wierszy opisana w „Dlaczego wiersz nie został
  dopasowany".
- [business-xml](../../business-xml/SKILL.md) — definicja tabel i kolumn, w tym
  oznaczanie tabeli jako konfiguracyjnej, co decyduje o jej obecności w rejestrze.
- [erp](../../erp/SKILL.md) — mapa wyboru skilla.
