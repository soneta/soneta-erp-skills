# Zasady tworzenia skilli Soneta

Skille w tym repozytorium znajdują się w `skills/`. Instalację opisuje [README.md](README.md).

- Soneta to nazwa firmy i platformy. Skille dotyczą obu produktów: enova365 i Triva.
- Użytkownicy skilli mogą nie mieć dostępu do kodu źródłowego Soneta. Opieraj instrukcje
  na udostępnionych referencjach, publicznych interfejsach, bibliotekach i narzędziach.
- Pisz zwięźle, zachowując szczegóły potrzebne do wykonania zadania. Najważniejsze reguły
  i odnośniki umieszczaj na początku. Szczegóły dotyczące wybranych zadań przenoś do referencji.
- Przykłady kodu powinny być ogólne, bez odwołań do kodu źródłowego programu Soneta.
  Stosuj C# 14 i .NET 10, z uwzględnieniem wersji platformy opisanej w danym materiale.
- Dodając materiał, umieść odnośnik do niego w odpowiednim `SKILL.md` lub indeksie
  i powiąż go z dokumentami opisującymi ten sam temat.
- W checklistach zapisuj warunki poprawności, które agent ma sprawdzić.
- W polskiej odmianie nazw używaj apostrofu po niemym `e` (np. `iPhone'a`, `software'u`)
  oraz łącznika przy skrótowcach (np. `SMS-a`, `URL-a`).

## Przenośność

- Utrzymuj jedną wersję każdego skilla w `skills/<nazwa>/`. Pole `name` w `SKILL.md` musi
  odpowiadać nazwie katalogu, a `description` opisywać zastosowanie niezależnie od asystenta.
- Między skillami i dokumentami używaj względnych linków Markdown do rzeczywistych plików,
  np. z `skills/erp/SKILL.md` do `../programming/SKILL.md`. Składnię aktywacji i prefiksy
  pluginów opisuj w instrukcji instalacji, nie jako wymagany sposób czytania referencji.
- Opisuj potrzebną operację: odczyt, wyszukanie, uruchomienie polecenia. Dobór narzędzia
  pozostaw agentowi. Zachowuj nazwy i kontrakty narzędzi Soneta. Podawaj, kiedy wymagają
  terminala, binariów lub połączenia z aplikacją.
- Ścieżki do zasobów ustalaj względem pliku skilla lub referencji. Oddzielaj katalog zasobów
  od projektu użytkownika i miejsca zapisu wyników. Nie zakładaj stałej ścieżki cache'u
  pluginu ani konkretnej powłoki.
- Instrukcje dla opcjonalnego połączenia, np. MCP, umieszczaj przy zadaniach, które z niego
  korzystają. Opisz też, jak przygotować kod lub instrukcje bez połączenia oraz wskazać,
  czego agent nie wykonał i nie zweryfikował.
- Zachowaj manifesty w `.claude-plugin/`, aby nadal udostępniać plugin dla Claude Code.
  Ta sama treść skilli ma służyć także innym asystentom.

## Weryfikacja

Przed zakończeniem:

- Sprawdź frontmatter zmienionych skilli (`name`, `description`, zgodność nazwy z katalogiem)
  oraz to, czy zmienione odnośniki prowadzą do właściwych plików. Przejrzyj diff.
- Sprawdź przejście z `erp` do właściwego skilla zarówno przez mechanizm ładowania skilli,
  jak i przez zwykły odczyt plików.
- Uruchom zmienione skrypty w wymaganym środowisku.
- Zmiany manifestów, sposobu instalacji lub ładowania skilli sprawdzaj w środowisku,
  którego dotyczą. Wskaż, czego nie udało się zweryfikować.
