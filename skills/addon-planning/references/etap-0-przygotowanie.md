# ETAP 0 — Przygotowanie

Cel: ustalić dwie rzeczy **niezależne od treści modułu**, zanim przejdziesz do Etapu 1 (Wizja). To krok wstępny — nie powstaje z niego osobny dokument planistyczny, ale jego ustalenia (nazwa firmy) i działania (repozytorium Git) warunkują cały dalszy proces.

## Nazwa firmy tworzącej dodatek

Dodatek może budować **dowolna firma**, nie tylko dostawca platformy. **Zapytaj o nazwę firmy (producenta dodatku) już na starcie** — przewija się w wielu miejscach:

- przedrostek przestrzeni nazw (`Firma.NazwaModulu`),
- nazwy projektów (`Firma.NazwaModulu`, `Firma.NazwaModulu.UI`, `Firma.NazwaModulu.Tests`),
- identyfikator w Nayspace,
- metryki paczek.

Zapisz ją i używaj **konsekwentnie** w dokumentach etapów oraz przy budowaniu solucji — zamiast wpisywać na sztywno konkretną firmę. Przedrostek `Soneta.*` jest zarezerwowany dla modułów samego dostawcy platformy; dodatek innej firmy używa własnego przedrostka.

## Repozytorium Git

Sprawdź, czy bieżący katalog roboczy jest pod kontrolą wersji:

```bash
git rev-parse --is-inside-work-tree
```

Jeśli **Git jest zainstalowany**, a katalog **nie jest** repozytorium (ani Git, ani innym systemem kontroli wersji), **utwórz repozytorium** (`git init`) i zainicjuj `.gitignore` ze standardowymi wykluczeniami projektów C#:

```gitignore
.idea/
.run/
.claude/
bin/
obj/
*.sln.DotSettings.user
.vs/
*.user
*.suo
*.userprefs
LocalSettings.json
*.log
*.db
TestResults/
.vscode/
```

Analogiczne katalogi innych asystentów (np. `.codex/`) dopisz według używanego narzędzia.
Współdzielone instrukcje (`CLAUDE.md`, `AGENTS.md`) i skille wersjonowane w repozytorium
zostaw poza wykluczeniami — wykluczasz lokalne ustawienia asystenta, nie ustalenia zespołu.

Gdy Git nie jest zainstalowany albo katalog jest już repozytorium — pomiń ten krok (istniejącego repo nie nadpisuj).

> Repozytorium założone na tym etapie obejmie później solucję dodatku budowaną **bezpośrednio w bieżącym katalogu roboczym** — patrz sekcja „Lokalizacja projektów kodu" w `SKILL.md`.
