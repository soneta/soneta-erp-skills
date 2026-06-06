using AwesomeAssertions;
using NUnit.Framework;
using Soneta.Handel;
using Soneta.Magazyny;

namespace Soneta.Skills.Test.Handel.DokumentyHandlowe;

/// <summary>
/// Rozdział 3 — Stany dokumentu i cykl życia (W12–W16).
/// <para>
/// Stanem dokumentu steruje jedno zapisywalne pole <c>dok.Stan</c>
/// (<see cref="StanDokumentuHandlowego"/>: <c>Bufor=0, Zatwierdzony=1, Zablokowany=2, Anulowany=3</c>).
/// Do asercji używamy skrótów kalkulowanych <c>dok.Bufor</c>/<c>dok.Zatwierdzony</c>/<c>dok.Anulowany</c>,
/// a nie porównywania enuma.
/// </para>
/// <para>
/// W bazie Demo działa <c>StanUjemnyVerifier</c> (blokada stanu ujemnego): zatwierdzenie rozchodu
/// wymaga wcześniej zapisanego przyjęcia tego towaru. Dlatego do prostych testów cyklu życia
/// używamy przychodu (PW), który niczego nie blokuje. Magazyn księguje się dopiero po
/// <c>Session.Save()</c>, nie po samym <c>Commit()</c>.
/// </para>
/// Cała klasa operuje wyłącznie na publicznym kontrakcie platformy (tak jak dodatek zewnętrzny).
/// </summary>
[TestFixture]
public class Rozdzial03_CyklZyciaTest : DokumentHandlowyTestBase
{
    // === Pomocnik lokalny: zatwierdzony przychód (PW) z jedną pozycją, zapisany trwale ===

    /// <summary>
    /// Tworzy przyjęcie wewnętrzne (PW) z pozycją towaru BIKINI, zatwierdza je i zapisuje.
    /// PW to przychód — nie podlega blokadzie stanu ujemnego, więc nadaje się do testów cyklu życia.
    /// Zwraca Guid zapisanego dokumentu (sesja zostaje zamknięta przez <see cref="SaveDispose"/>).
    /// </summary>
    private System.Guid UtworzZatwierdzonyPwIZapisz(double ilosc = 10, double cena = 5)
    {
        // 1. Dokument przychodowy + pozycja w jednej transakcji.
        var dok = UtworzDokument(Definicje.PrzyjecieWewnetrzne, magazyn: Magazyn(Magazyn_.Firma));
        InTransaction(() => DodajPozycje(dok, Towar(Towar_.Bikini), ilosc, cena));

        // 2. Zatwierdzenie: Bufor -> Zatwierdzony (osobna transakcja).
        InTransaction(() => dok.Stan = StanDokumentuHandlowego.Zatwierdzony);

        var guid = dok.Guid;
        // 3. Dopiero Save() księguje obroty/zasoby. SaveDispose zamyka okno edycji sesji.
        SaveDispose();
        return guid;
    }

    [Test]
    [Description("W12: zatwierdzenie przychodu (PW) zmienia stan na Zatwierdzony i tworzy zasoby po Save.")]
    public void W12_ZatwierdzeniePrzychodu_UstawiaStanIKsięgujeZasoby()
    {
        // Tworzymy PW z pozycją (przychód — bez ryzyka stanu ujemnego).
        var dok = UtworzDokument(Definicje.PrzyjecieWewnetrzne, magazyn: Magazyn(Magazyn_.Firma));
        InTransaction(() => DodajPozycje(dok, Towar(Towar_.Bikini), ilosc: 10, cena: 5));

        // Przed zatwierdzeniem dokument jest w buforze.
        dok.Bufor.Should().BeTrue();
        dok.Zatwierdzony.Should().BeFalse();

        // Zatwierdzenie: bufor -> zatwierdzony (czytamy pole kalkulowane, nie enum).
        InTransaction(() => dok.Stan = StanDokumentuHandlowego.Zatwierdzony);
        var guid = dok.Guid;
        // Dopiero Save() księguje obroty/zasoby/płatności.
        SaveDispose();

        // Odczyt na świeżej sesji po Guid (wzorzec zapis -> odczyt).
        var zapisany = Get<DokumentHandlowy>(guid);
        zapisany.Zatwierdzony.Should().BeTrue();
        zapisany.Bufor.Should().BeFalse();
        // Przychód utworzył zasoby magazynowe (widoczne po Save).
        zapisany.Zasoby.Count.Should().BeGreaterThan(0);
    }

    [Test]
    [Description("W13: cofnięcie zatwierdzonego dokumentu bez zależności z powrotem do bufora.")]
    public void W13_CofniecieDoBufora_PrzywracaStanBufor()
    {
        // Zatwierdzony PW bez dokumentów podrzędnych.
        var guid = UtworzZatwierdzonyPwIZapisz();

        // Re-get na świeżej sesji (po SaveDispose nie wolno edytować obiektu z poprzedniej sesji — §8).
        var dok = Get<DokumentHandlowy>(guid);
        dok.Zatwierdzony.Should().BeTrue();

        // Cofnięcie: zatwierdzony -> bufor (odksięgowanie przy Save).
        InTransaction(() => dok.Stan = StanDokumentuHandlowego.Bufor);
        SaveDispose();

        var poCofnieciu = Get<DokumentHandlowy>(guid);
        poCofnieciu.Bufor.Should().BeTrue();
        poCofnieciu.Zatwierdzony.Should().BeFalse();
    }

    [Test]
    [Description("W14: anulowanie dokumentu w buforze ustawia stan Anulowany, rekord pozostaje w bazie.")]
    public void W14_AnulowanieZBufora_UstawiaStanAnulowany()
    {
        // PW w buforze (anulowanie z bufora nie wymaga odksięgowania).
        var dok = UtworzDokument(Definicje.PrzyjecieWewnetrzne, magazyn: Magazyn(Magazyn_.Firma));
        InTransaction(() => DodajPozycje(dok, Towar(Towar_.Bikini), ilosc: 10, cena: 5));
        dok.Bufor.Should().BeTrue();

        // Anulowanie: bufor -> anulowany.
        InTransaction(() => dok.Stan = StanDokumentuHandlowego.Anulowany);
        var guid = dok.Guid;
        SaveDispose();

        // Po anulowaniu rekord nadal istnieje (w przeciwieństwie do Delete) i jest oznaczony jako anulowany.
        var zapisany = Get<DokumentHandlowy>(guid);
        zapisany.Should().NotBeNull();
        zapisany.Anulowany.Should().BeTrue();
        zapisany.Bufor.Should().BeFalse();
    }

    [Test]
    [Description("W14: anulowanie zatwierdzonego przychodu odksięgowuje zasoby, rekord zostaje.")]
    public void W14_AnulowanieZatwierdzonego_OdksięgowujeIRekordZostaje()
    {
        // Zatwierdzony PW (utworzył zasoby).
        var guid = UtworzZatwierdzonyPwIZapisz();

        var dok = Get<DokumentHandlowy>(guid);
        dok.Zatwierdzony.Should().BeTrue();

        // Anulowanie zatwierdzonego: odksięgowanie skutków magazynowych przy Save.
        InTransaction(() => dok.Stan = StanDokumentuHandlowego.Anulowany);
        SaveDispose();

        var zapisany = Get<DokumentHandlowy>(guid);
        // Rekord zachowany (numeracja/audyt), oznaczony jako anulowany.
        zapisany.Should().NotBeNull();
        zapisany.Anulowany.Should().BeTrue();
        // Anulowanie odksięgowało zasoby utworzone przez przychód.
        zapisany.Zasoby.Count.Should().Be(0);
    }

    [Test]
    [Description("W16: usunięcie dokumentu w buforze bez zależności (Delete) trwale kasuje rekord.")]
    public void W16_UsuniecieZBufora_KasujeRekord()
    {
        // Dokument w buforze, bez powiązań i rezerwacji — usunięcie dozwolone.
        var dok = UtworzDokument(Definicje.PrzyjecieWewnetrzne, magazyn: Magazyn(Magazyn_.Firma));
        InTransaction(() => DodajPozycje(dok, Towar(Towar_.Bikini), ilosc: 10, cena: 5));
        var guid = dok.Guid;

        // Warunki bezpiecznego usunięcia: bufor.
        dok.Bufor.Should().BeTrue();

        // Twarde usunięcie (kasuje też pozycje) w tej samej sesji edycyjnej, bez wcześniejszego
        // SaveDispose — Delete musi nastąpić na żywym obiekcie, przed zapisem.
        InTransaction(() => dok.Delete());
        SaveDispose();

        // Po usunięciu indeksator po Guid rzuca RowNotFoundException dla nieistniejącego GUID (§5).
        Assert.Throws<Soneta.Business.RowNotFoundException>(() =>
        {
            var _ = Get<DokumentHandlowy>(guid);
        });
    }

    [Test]
    [Description("W16: anulowanie jako alternatywa dla usunięcia zatwierdzonego — rekord pozostaje.")]
    public void W16_ZatwierdzonyAnulowanyZamiastUsuniety_RekordZostaje()
    {
        // Zatwierdzonego dokumentu nie można usuwać przez Delete (tylko bufor) —
        // zalecaną ścieżką dla nieodwracalnego wycofania jest anulowanie (zachowuje numer i audyt).
        var guid = UtworzZatwierdzonyPwIZapisz();

        var dok = Get<DokumentHandlowy>(guid);
        // Poza buforem — Delete jest zabronione, więc anulujemy.
        dok.Bufor.Should().BeFalse();

        InTransaction(() => dok.Stan = StanDokumentuHandlowego.Anulowany);
        SaveDispose();

        // Rekord nadal w bazie, oznaczony jako anulowany.
        var zapisany = Get<DokumentHandlowy>(guid);
        zapisany.Should().NotBeNull();
        zapisany.Anulowany.Should().BeTrue();
    }

    [Test]
    [Description("W15: PoprawaStanuDokumentuWorker na poprawnym dokumencie nie zmienia jego stanu.")]
    public void W15_NaprawaStanu_NaPoprawnymDokumencie_ZachowujeStan()
    {
        // Zatwierdzony, spójny dokument — naprawa stanu nie powinna nic zepsuć.
        var guid = UtworzZatwierdzonyPwIZapisz();

        var dok = Get<DokumentHandlowy>(guid);
        dok.Zatwierdzony.Should().BeTrue();

        // Worker sam zarządza transakcją wewnątrz NaprawStan() — ustawiamy tylko kontekst.
        var naprawa = new PoprawaStanuDokumentuWorker { Dokument = dok };
        naprawa.NaprawStan();
        // Wystarczy Save() po akcji, by utrwalić ewentualne zmiany workera.
        SaveDispose();

        // Dokument poprawny — stan po naprawie pozostaje zatwierdzony.
        var poNaprawie = Get<DokumentHandlowy>(guid);
        poNaprawie.Zatwierdzony.Should().BeTrue();
    }

    [Test]
    [Description("W15: PrzeliczenieStanuWorker w trybie SprawdzićPoprawność (diagnostyka) nie zmienia danych.")]
    public void W15_SprawdzeniePoprawnosciObrotow_NieZmieniaStanu()
    {
        // Zatwierdzony przychód z poprawnymi obrotami.
        var guid = UtworzZatwierdzonyPwIZapisz();

        var dok = Get<DokumentHandlowy>(guid);
        var zasobyPrzed = dok.Zasoby.Count;

        // Tryb SprawdzićPoprawność tylko raportuje (Trace) — nie commituje zmian.
        // Worker sam otwiera transakcje wewnątrz PrzeliczStan(); nie owijamy go własnym Logout.
        var sprawdz = new PrzeliczenieStanuWorker(
            PrzeliczenieStanuWorker.Opcje.SprawdzićPoprawność,
            wszystkieMagazyny: false, rozchód0: false, przywracajWartość: true) { Dokument = dok };
        sprawdz.PrzeliczStan();

        // Tryb diagnostyczny nie modyfikuje danych — stan i zasoby bez zmian.
        dok.Zatwierdzony.Should().BeTrue();
        dok.Zasoby.Count.Should().Be(zasobyPrzed);
    }
}
