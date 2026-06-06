using System;
using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using NUnit.Framework;
using Soneta.Handel;

namespace Soneta.Skills.Test.Handel.DokumentyHandlowe;

/// <summary>
/// Rozdział 10 — Operacje zbiorcze (batch), wzorce W53–W55.
/// <para>
/// Operacje na zbiorze dokumentów wykonujemy bezpiecznie i wydajnie: filtr <b>serwerowy</b>
/// (a nie pełny skan tabeli operacyjnej <c>DokHandlowe</c>), <b>krótkie transakcje</b>
/// (paczki) oraz świadoma obsługa zapisu (<c>Save()</c>, gdzie wykrywane są konflikty
/// optymistyczne). W testach krótka transakcja = <c>InTransaction(...)</c>, a zamknięcie
/// paczki = <c>SaveDispose()</c> (Save + zamknięcie okna edycji sesji).
/// </para>
/// <para>
/// W bazie Demo działa <c>StanUjemnyVerifier</c> (blokada stanu ujemnego), więc do operacji
/// zbiorczych używamy przychodów (PW) — nie podlegają tej blokadzie i nie wymagają
/// wcześniejszego zapasu towaru. Magazyn księguje się dopiero po <c>Session.Save()</c>.
/// </para>
/// Cała klasa operuje wyłącznie na publicznym kontrakcie platformy (jak dodatek zewnętrzny).
/// </summary>
[TestFixture]
public class Rozdzial10_BatchTest : DokumentHandlowyTestBase
{
    // === Pomocnik lokalny: kilka przyjęć (PW) w buforze, zapisanych trwale ===

    /// <summary>
    /// Tworzy <paramref name="ile"/> dokumentów przyjęcia wewnętrznego (PW) z jedną pozycją
    /// BIKINI, pozostawia je w buforze i zapisuje trwale. Zwraca listę Guidów (sesja zostaje
    /// zamknięta przez <see cref="SaveDispose"/>, więc dalej pracujemy przez odczyt po Guid).
    /// PW to przychód — bez ryzyka blokady stanu ujemnego, idealny do testów wsadowych.
    /// </summary>
    private List<Guid> UtworzPwWBuforzeIZapisz(int ile, double ilosc = 10, double cena = 5)
    {
        var guidy = new List<Guid>(ile);
        for (int i = 0; i < ile; i++)
        {
            // Każdy dokument tworzymy przez bazowy helper (AddRow -> Definicja -> Magazyn).
            var dok = UtworzDokument(Definicje.PrzyjecieWewnetrzne, magazyn: Magazyn(Magazyn_.Firma));
            InTransaction(() => DodajPozycje(dok, Towar(Towar_.Bikini), ilosc, cena));
            guidy.Add(dok.Guid);
        }
        // Jeden wspólny Save dla wszystkich utworzonych dokumentów.
        SaveDispose();
        return guidy;
    }

    // === W54 — Hurtowe zatwierdzanie wielu dokumentów w jednej transakcji ===

    [Test]
    [Description("W54: hurtowe zatwierdzanie — kilka PW w buforze zatwierdzonych pętlą po Stan w jednej transakcji; po Save wszystkie są Zatwierdzone.")]
    public void W54_HurtoweZatwierdzanie_WszystkieDokumentyZatwierdzone()
    {
        // 1. Przygotowanie: 3 dokumenty PW w buforze, zapisane trwale.
        var guidy = UtworzPwWBuforzeIZapisz(ile: 3);

        // Wczytujemy je na świeżej sesji i potwierdzamy stan wyjściowy = Bufor.
        var dokumenty = guidy.Select(g => Get<DokumentHandlowy>(g)).ToArray();
        dokumenty.Should().OnlyContain(d => d.Bufor);

        // 2. Hurtowe zatwierdzenie: jedna (krótka) transakcja, pętla po zbiorze i zmiana Stan.
        //    W teście InTransaction odpowiada wzorcowi session.Logout(true) + Commit z dokumentu.
        InTransaction(() =>
        {
            foreach (var d in dokumenty)
                d.Stan = StanDokumentuHandlowego.Zatwierdzony;
        });
        SaveDispose();

        // 3. Asercja: po Save wszystkie dokumenty są zatwierdzone (czytamy pola kalkulowane).
        foreach (var g in guidy)
        {
            var zapisany = Get<DokumentHandlowy>(g);
            zapisany.Zatwierdzony.Should().BeTrue();
            zapisany.Bufor.Should().BeFalse();
        }
    }

    [Test]
    [Description("W54: hurtowe cofnięcie do bufora — kilka zatwierdzonych PW cofniętych jedną pętlą po Stan; po Save wszystkie wracają do bufora.")]
    public void W54_HurtoweCofniecieDoBufora_WszystkieWBuforze()
    {
        // 1. Najpierw zatwierdzamy kilka PW (stan wyjściowy do cofnięcia).
        var guidy = UtworzPwWBuforzeIZapisz(ile: 2);
        var zatwierdzone = guidy.Select(g => Get<DokumentHandlowy>(g)).ToArray();
        InTransaction(() =>
        {
            foreach (var d in zatwierdzone)
                d.Stan = StanDokumentuHandlowego.Zatwierdzony;
        });
        SaveDispose();
        guidy.Select(g => Get<DokumentHandlowy>(g)).Should().OnlyContain(d => d.Zatwierdzony);

        // 2. Hurtowe cofnięcie: zatwierdzony -> bufor (odksięgowanie przy Save) w jednej transakcji.
        var doCofniecia = guidy.Select(g => Get<DokumentHandlowy>(g)).ToArray();
        InTransaction(() =>
        {
            foreach (var d in doCofniecia)
                d.Stan = StanDokumentuHandlowego.Bufor;
        });
        SaveDispose();

        // 3. Asercja: wszystkie z powrotem w buforze.
        guidy.Select(g => Get<DokumentHandlowy>(g))
            .Should().OnlyContain(d => d.Bufor && !d.Zatwierdzony);
    }

    // === W55 — Wydajne przetwarzanie w paczkach (krótkie transakcje, okresowy Save) ===

    [Test]
    [Description("W55: przetwarzanie w paczkach — kilka dokumentów dzielonych na małe transakcje z okresowym Save; po przetworzeniu wszystkie poprawnie zatwierdzone.")]
    public void W55_PrzetwarzanieWPaczkach_WszystkieDokumentyPrzetworzone()
    {
        // 1. Większy (na potrzeby testu kilkuelementowy) zbiór PW w buforze.
        const int ileDokumentow = 5;
        var guidy = UtworzPwWBuforzeIZapisz(ile: ileDokumentow);

        // 2. Wzorzec paczkowy: małe paczki + Save po każdej paczce (krótka transakcja).
        //    W produkcyjnym kodzie rozmiar paczki to ~200; w teście używamy 2, by faktycznie
        //    domknąć więcej niż jedną paczkę i pokazać wzorzec "Save -> nowa sesja po Guid".
        //    Po SaveDispose okno edycji jest zamknięte, więc kolejną paczkę edytujemy na
        //    świeżej sesji (odczyt po Guid) — odpowiednik nowej session.Logout(true).
        const int rozmiarPaczki = 2;
        int przetworzone = 0;

        // Iterujemy serwerowo wyłonione dokumenty (tu: po znanych Guidach) paczkami.
        foreach (var paczka in guidy.Chunk(rozmiarPaczki))
        {
            // Każda paczka = osobna krótka transakcja na świeżej sesji.
            var dokumentyPaczki = paczka.Select(g => Get<DokumentHandlowy>(g)).ToArray();
            InTransaction(() =>
            {
                foreach (var d in dokumentyPaczki)
                {
                    d.Stan = StanDokumentuHandlowego.Zatwierdzony;
                    przetworzone++;
                }
            });
            // Okresowy Save zamyka paczkę (krótka transakcja); kolejna paczka -> nowa sesja.
            SaveDispose();
        }

        // 3. Asercja poprawności: liczba przetworzonych = liczba dokumentów,
        //    a każdy dokument jest trwale zatwierdzony.
        przetworzone.Should().Be(ileDokumentow);
        foreach (var g in guidy)
            Get<DokumentHandlowy>(g).Zatwierdzony.Should().BeTrue();
    }

    [Test]
    [Description("W55: filtr serwerowy z zakresem czasowym — wsadowo zatwierdzamy tylko PW z dzisiejszą datą i w buforze; wzorzec SubTable[condition] zamiast pełnego skanu.")]
    public void W55_FiltrSerwerowyZakresCzasowy_PrzetwarzaTylkoWybranePaczki()
    {
        // 1. Tworzymy kilka PW w buforze (data = dziś, nadana domyślnie przez definicję).
        const int ileDokumentow = 4;
        var guidy = UtworzPwWBuforzeIZapisz(ile: ileDokumentow);
        var oczekiwane = new HashSet<Guid>(guidy);

        // 2. Filtr SERWEROWY z zakresem czasowym na tabeli operacyjnej DokHandlowe —
        //    NIE iterujemy całej tabeli z if-em w pamięci. Zawężamy do PW w buforze z dzisiaj.
        var fv = Definicja(Definicje.PrzyjecieWewnetrzne);
        var od = Soneta.Types.Date.Today;

        // Materializujemy zbiór do paczkowego przetwarzania (w produkcji iterujemy strumieniowo).
        var doPrzetworzenia = Handel.DokHandlowe.WgDaty[(DokumentHandlowy d) =>
                d.Data >= od && d.Definicja == fv && d.Stan == StanDokumentuHandlowego.Bufor]
            .Cast<DokumentHandlowy>()
            .Where(d => oczekiwane.Contains(d.Guid))   // zawężenie tylko do dokumentów tego testu
            .Select(d => d.Guid)
            .ToList();

        // Filtr serwerowy odnalazł wszystkie utworzone dokumenty tego testu.
        doPrzetworzenia.Should().HaveCount(ileDokumentow);

        // 3. Przetwarzanie paczkami (krótkie transakcje) na wyłonionym zbiorze.
        const int rozmiarPaczki = 2;
        foreach (var paczka in doPrzetworzenia.Chunk(rozmiarPaczki))
        {
            var dokumentyPaczki = paczka.Select(g => Get<DokumentHandlowy>(g)).ToArray();
            InTransaction(() =>
            {
                foreach (var d in dokumentyPaczki)
                    d.Stan = StanDokumentuHandlowego.Zatwierdzony;
            });
            SaveDispose();
        }

        // 4. Asercja: wszystkie wyłonione filtrem dokumenty zostały zatwierdzone.
        foreach (var g in doPrzetworzenia)
            Get<DokumentHandlowy>(g).Zatwierdzony.Should().BeTrue();
    }

    // === W53 — Ewidencjonowanie zbiorcze (EwidencjonowanieZbiorczeWorker) ===

    [Test]
    [Description("W53: ewidencjonowanie zbiorcze (EwidencjonowanieZbiorczeWorker) — pomijane: wymaga konfiguracji księgowej/ewidencji niedostępnej wprost w bazie Demo.")]
    public void W53_EwidencjonowanieZbiorcze_PominietePoniewazWymagaKonfiguracjiKsiegowej()
    {
        // SKIP: pełny tor ewidencjonowania zbiorczego wymaga skonfigurowanej ewidencji
        // księgowej (definicja dokumentu ewidencji typu SprzedażZbiorczaEwidencja) oraz
        // dokumentów źródłowych z niepustym symbolem kasy/drukarki fiskalnej. W bazie Demo
        // nie jest to dostępne wprost, więc tworzenie zbiorczych DokEwidencji nie zadziała
        // w sposób powtarzalny. Opisujemy tu jedynie PUBLICZNY tor wywołania:
        //
        //   var worker = new EwidencjonowanieZbiorczeWorker
        //   {
        //       Param = new EwidencjonowanieZbiorczeWorker.Params(context)
        //       {
        //           RaportDla = EwidencjonowanieZbiorczeWorker.RaportDla.Paragonów, // lub KorektParagonów
        //           ZaOkres = new FromTo(new Date(2026, 6, 1), new Date(2026, 6, 30)), // data wystawienia
        //           OkresDostawyZaliczki = FromTo.All,    // bez filtra dostawy/zaliczki
        //           SymbolKasy = "D1",                    // jedna drukarka; puste = wszystkie z symbolem kasy
        //           Definicja = CoreModule.GetInstance(session).DefDokumentow.WgSymbolu["SPZE"], // opcjonalnie
        //       }
        //   };
        //   worker.Ewidencjonuj();   // worker SAM otwiera transakcję i robi CommitUI() w środku
        //   session.Save();          // dopiero teraz zapis do bazy (tu wykrywane konflikty optymistyczne)
        //
        // Uwagi (pułapki):
        //  - NIE owijaj Ewidencjonuj() we własną transakcję edycyjną (worker robi Session.Logout(true)
        //    + CommitUI() wewnętrznie); zagnieżdżenie = podwójny commit.
        //  - Param to property [Context] — ustaw PRZED Ewidencjonuj(), inaczej NullReferenceException.
        //  - Worker przetwarza tylko dokumenty Zatwierdzone/Zablokowane i pomija już
        //    zaewidencjonowane (EwidencjaZbiorcza != null).
        //  - Definicja to rekord konfiguracyjny — pobierz istniejący (WgSymbolu/WgTypu), nie twórz "w locie".
        Assert.Ignore("W53: ewidencjonowanie zbiorcze wymaga konfiguracji ewidencji księgowej/kasy " +
                      "niedostępnej wprost w bazie Demo. Publiczny tor (Ewidencjonuj() + Params) opisany w komentarzu.");
    }
}
