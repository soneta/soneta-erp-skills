# Źródła praw (`IRightsSource`)

Obiekt (zwykle **konfiguracyjny**, np. magazyn, rejestr, definicja) może być **źródłem praw**:
operatorowi/roli przypisuje się uprawnienia do tego obiektu, co steruje dostępem do **danych
operacyjnych referujących** do niego — a nie tylko do samego obiektu konfiguracyjnego. Przykład:
operator z prawem do danego magazynu widzi tylko dokumenty przypisane do tego magazynu, a do
dokumentów z innych magazynów dostępu nie ma.

- Włączenie: w `business.xml` dodaj do tabeli `<interface>IRightsSource</interface>`. Od tego momentu
  system sam dba o widoczność obiektów i propagację praw. (`IRightsSourceEx` dokłada pod-kategorię,
  `IsRightsSourceEnable()`, `IsRightsSourceVisible()`.)
- Odczyt uprawnień: `Row.AccessRight`, `Table.AccessRight`, `Login.GetObjectRight(source)` →
  `AccessRights` (`Granted`/`Denied`/…).
- **Listy/`View` automatycznie filtrują** dane po prawach — pokazują tylko rekordy ze źródeł, do
  których operator ma dostęp. Na formularzu definicji pojawia się zakładka z przypisaniami uprawnień.
- W **kodzie biznesowym nie sprawdzaj** `AccessRight` warunkami — system egzekwuje prawa sam
  (patrz [safe-code.md](safe-code.md) §7.2).
