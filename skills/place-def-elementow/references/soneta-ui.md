# Konfiguracja elementu przez MCP soneta_ui lub buscall

Czytaj przy automatyzacji [definicji elementu wynagrodzenia](../SKILL.md) przez
połączenie MCP `soneta_ui` lub `buscall`. Poniższe nazwy to metody integracji Soneta, nie narzędzia
wbudowane w konkretne środowisko agenta. Przed wywołaniem sprawdź udostępnione narzędzia,
ich schematy argumentów i aktualne identyfikatory formularza; prefiks MCP może się różnić.
Jeśli korzystasz z `buscall`, składnię wywołania sprawdź w
[referencji buscall](../../tools/references/buscall.md).

Mapowanie kroków z głównego skilla (zapis poglądowy, nie gotowy skrypt):

```text
1. navigate_to_folder("Ustawienia/Kadry i płace/Płace/Elementy wynagrodzenia")
2. add_subobject(gridID="_New_CfgDefElementowExtender_DefElementow")
3. update_field_value — wypełnij zakładkę Ogólne (nazwa, skrót, naliczanie, lista płac)
4. switch_form_page("DefinicjaElementuAlgorytmPage") — skonfiguruj algorytm
5. [opcjonalnie] switch_form_page("DefinicjaElementuEdytorPage") — wpisz kod C#
6. switch_form_page("DefinicjaElementuDeklaracjePage") — skonfiguruj PIT/ZUS
7. switch_form_page("DefinicjaElementuNieobecnosciProPage") — skonfiguruj wliczanie do podstaw
8. accept_subform() — zatwierdź podformularz
9. save_form() — zapisz do bazy
```

## Kod w edytorze

Do `update_field_value` przekazuj `fieldsValues` jako tablicę ciągów `pole=wartość`.
Wartość `_Tekst` ma zawierać rzeczywiste znaki nowej linii. Metoda oddziela nazwę pola
od wartości po pierwszym `=`; konwersja pola kodu nie odkodowuje dodatkowych sekwencji ucieczki.

W obiekcie argumentów MCP zapisanym jako JSON używaj `\n` (jeden ukośnik w tekście JSON).
Parser JSON zamienia tę sekwencję na znak nowej linii:

```json
{"fieldsValues":["_Tekst=public void Nazwa_Param(...) {\n    ...\n}\n\npublic Currency Nazwa_Wylicz(...) {\n    return ...;\n}"]}
```

Przez `buscall call` przekaż tę samą tablicę jako wartość argumentu `fieldsValues`.
Przykład dla Bash (kod poglądowy, zastąp go właściwym algorytmem):

```bash
buscall --db Demo call update_field_value 'fieldsValues=["_Tekst=public void Nazwa_Param(...) {\n    ...\n}\n"]'
```

Nie zamieniaj `\n` na `\\n` w powyższych przykładach: po odczytaniu JSON druga postać
pozostawia w wartości dosłowny ukośnik i literę `n`. Jeśli budujesz argumenty w kodzie,
przekaż tekst wielowierszowy i użyj serializatora JSON. Przy dodatkowej warstwie kodowania
lub innej powłoce zachowaj ten sam wynik po odkodowaniu argumentów.

Po wpisaniu odczytaj pole edytora i porównaj z przygotowanym kodem. Separatory wierszy
powinny pozostać rzeczywistymi znakami nowej linii, a sekwencje ucieczki wewnątrz literałów
C# — zachować oryginalną postać. Potwierdź też zapis konfiguracji i wynik naliczenia
według głównego skilla.
