
# Uwagi ogólne do tworzenia dokuemntu planowania projektu modułu/dodatku

## Przeznaczenie dokumentu planu projektu
Planowanie projektów modułów fukcjonalności dla platformy enova365/Soneta Enterprise.
Przygotowuje opis funkcjonalności nowego modułu z punktu widzenia używania go do stworzenia kolejnych planów implementacji poszczególnych elementów programu Soneta.
Plan będzie wykorzystywany w kolejnych etapach do budowania:
* struktury bazy danych,
* listy dostępnych list i widoków,
* innych ważnych/kluczowych elementów interfejsu,
* formularzy,
* tworzenia raportów i wydruków,
* praw dostępu,
* ról operatorów,
* zaplanowania testów,
* budowy dokumentacji
* i innych tego typu elementów prohjektu.


## Różne tematy do ujęcia w planie
- ogarnąć temat licencji w szczególności jakie licencję wymagane są do działania kontrolingu
- opracować punkty ograniczające modułu. Ograniczenia mogą dotyczyć np. zakresu funkcjonalności, wymagań technicznych, integracji z innymi systemami, czy też ograniczeń wynikających z przepisów prawa.
- opracować zmiany w aktualnych strukturach programu enova które są potrzebne do realizacji tego modułu
- opracować tabele i dane które będą wykorzystywane przez moduł kontrolingu, opisać krótko jaki jest cel u tych danych, a które znajdują się w programie enowa
- Przygotować plan todo czyli kolejne elementy do realizacji po planowaniu
- plan powinien rozpoczynać się podstawową ideą którą chcemy osiągnąć w danym systemie danym module. w tym przypadku chodzi o to że podstawową rzeczą jest przygotowanie danych które dopiero później będą wykorzystywane do analizy za pomocą innych narzędzi
- określić jakiego typu dane będą potrzebne do wczytywania systemu BI ale również jakie dane będą dostarczane do systemu BI
- zdefiniować jakie procesy będą realizowane w ramach danego modułu. Te procesy będą wykorzystane później do stworzenia procesów workflow a także do przypisania ich do poszczególnych operatorów ról
- określić jakie rolę będą realizowane przez system czyli opisać rolę operatorów i zadania które w ramach tej roli będą realizowane
- określić foldery i elementy interfejs-owę które będą dostępne z punktu widzenia modułu do opisania są funkcjonalności, które są szczególnie ważne interfejs sowo żeby produkt odniósł sukces. W kontrolingu ważnym elementem będzie miejsce w którym będzie można budować gotowe zapytania i warunki na podobieństwo Arkusz Excel owy m na zasadzie and/or. Wskazać które elementy interfejsu są krytyczne albo są sednem danego modułu
- określić zakres konfiguracji czyli wskazać ustawienia które będą definiowane w procesie konfigurowania programu a także różnego rodzaju definicję które będą wykorzystywane jako podpowiedzi przetwarzaniu już danych operacyjnych



## Poziom szczegółowości
Plan projektu zawiera ogólne opisy elementów projektu. Nie zawiera szczególowych inforamcji, które będą uzupełniane w kolejnych etapach. Na tym etapie chodzi o określenie ogólnych założeń, zakresu funkcjonalności...

## ETAP 1: Określenie ogólnych założeń mmodułu oraz ogólny opis funkcjonalności

## Elementy planu projektu

### Cel biznesowy projektu
Krótka informacja cztery, pięć zdań o tym co jest celem projektu. Jaki jest jego kluczowy element. Najważniejsza rzecz, którą chcemy osiągnąć.

### Firmy docelowe
Ogólne określenie użytkownika końcowego, który będzie używał tego rowiązania. Firma mała, średnia, duża? Branża? Działalność?

### Korzyści dla klienta
Zdefiniowanie najważniejszych korzyści dla klienta, określenie problemów rozwiązywanych przez moduł. Informacja ma być wykorzystywana w działach marketingu i sprzedaży, a także jako wprowadzenie do spotkań w temacie modułu.

### Najważniejsze funkcjonalności
Krótka lista najważniejszych funkcjonalności modułu. Te funkcjonalności będą szczegółowo opisywane w kolejnych etapach, ale już na tym etapie warto wskazać te kluczowe, które są sednem modułu i które będą najbardziej istotne z punktu widzenia klienta. Wyszczególnione w krótkich punktach.

### Scenariusze użytkownika
Opis kilku kluczowych scenariuszy użytkownika, które pokazują jak użytkownik będzie korzystał z modułu. Scenariusze powinny być opisane w formie krok po kroku, pokazując jakie działania użytkownik będzie podejmował, ogólnie jakie dane będzie wprowadzał, jakie wyniki będzie otrzymywał. Scenariusze powinny ograniczyć się tylko do najważniejszych,kluczowych danych. Szczegóły będą uzupełniane w kolejnych etapach.

### Proesy biznesowe
Procesy biznesowe, które będą realizowane w całoci przez moduł, ale także inne procesy biznesowe, w których części realizacji będzie wykorzystana funkcjonalność modułu, włącznie z pozostałymi elementami programu.


## ETAP 2: Ogólne określenie szczegółowych elementów projektu

### Role użytkowników. 
Lista operatorów, którzy będą korzystać z modułu? Jakie zadanie będzie realizować. W jakich procesach będą uczestniczyć? 

### Zakres konfiguracji modułu
Opisy elementów systemu, które będą konfigurowane. Ogólny opis zakresu konfiguracji, pozwalające na dostosowanie modułu do różnych potrzeb różnych klientów. Określenie ewentualnych definicji dokumentów, słowników, konfigurowanych ustawień, opcji programu dostępnych tylko dla pewnych grup klientów.

### Kluczowe struktury danych
Określenie najważniejszych struktur danych, które będą podstawą modułu. Podstawowe listy dokumentów, kartoteki. Bez opis szczegółowej zawartości, która będzie okreslona w kolejnym etapie.

## Kierunki rozwoju modułu w przyszłości o ile taki rozwój jest planowany.
Okreslenie kierunków rozwoju modułu w przyszłości, jakie funkcjonalności mogą być dodane w kolejnych etapach, które nie powinny być rozwijane w tej wersji rozwiązania.

## Relacje z innymi systemami
Określenie relacji z innymi systemami, które będą wykorzystywane przez moduł, ale także innych systemów, które będą integrowane z modułem. Określenie jakie dane będą wymieniane między modułem a innymi systemami, jakie procesy będą realizowane w ramach tej integracji.


## ETAP 2: Uszczegółowienie elementów projektu

### Dane operacyjne
Rozwiniecie struktu danych operacyjnych. Wskazanie ich dodatkowych cech, jak hostoryczność danych, wykorzystanie numeracji dokumentów. Wyszczególnienie pól danych. Określenie list szczegółowych w ramach obiektu. Określenie relacji do innych danych modułu, a także innych danych programu Soneta (poza projektowanych modułem).
Określenie części konfiguracyjnej, jak typy definicje, słowniki, itp. powiązanych z omawianym obiektem.
Przykład: Dokument jest numerowany, określa datę wprowadzenia, zatwierdzenia, stany (bufor, zatwierdzony, odrzucony), jest powiązany z pracownikiem.
Przypisany do definicji dokumentu. okreslającej zasady numeracji, tytuł dokuemntu, warunki akceptacji, itp.

### Relacje do danych programu
Określenie relacji do danych programu Soneta z poza modułu, które będą integrowane z modułem. Określenie jakie dane będą wymieniane między modułem, a innymi danymi programu Soneta.

### Podstawowe listy modułu
Wyszczególnienie głównych list obiektów modułu.
Wyspecifikowanie podstawowych kolumn listy, oraz kolumn opcjonalnych dostępnych dopiera w opcjach konfiguracyjnych.
Określenie podstawowych pól filtrtujących dane, podstawowych filtrów oraz dodatkowych (po rozwinięciu).

### Formularze
Wyszczególnienie podstawowych formularzy obiektów dostępnych z list. Opisanie ich zawartości, pól znajdujących się w obiekcie oraz list szczegółowych. Pogrupowanie danych w zakłądki oraz grupy na zakładkach.

## Przygotowanie dokumentu TODO
Dokument powinien określać kolejne kroki w projekcie i procesie implementacji. Powinien to być osobny dokument, wykorzystywany przez zespół implementacyjny, oraz AI do realizacji kolejnych kroków tworzenia modułu, ale także przez inne działy firmy Soneta, jak marketing, sprzedaż, czy dział wsparcia. Dokument powinien być aktualizowany w trakcie realizacji projektu, a także po jego zakończeniu, w celu określenia kolejnych kroków rozwoju modułu.

### Punkty do dokumentu TODO:
- [ ] Określenie modelu danych projektu, takich jak tabele, pola, relacje, itp.
- [ ] Zbudownie na podstawie modelu pliku business.xml
- [ ] Struktura menu modułu, listy, widoki
- [ ] Wyglądy formularzy, ich zakładek
- [ ] Zakładki konfiguracji, takich jak słowniki, definicje, ustawienia
- [ ] Workery, menu "Czynności" dostępne na listach i formularzach
- [ ] Raporty i wydruki dostępne na listach i formularzach
- [ ] Procesy Workflow w których uczestniczy moduł i realizowane w module
- [ ] Wskaźniki, listy, agregaty, wykresy BI
- [ ] Uprawnienia i role użytkowników
- [ ] Punkty styku/integracji z innymi systemami
- [ ] Uzupełnienie bazy Demo, i inne bazy demostracyjne
- [ ] Testy integracyjne, interface-owych
- [ ] Określenie struktury dokumentacji użytkownika i technicznej



