using System.Linq;
using AwesomeAssertions;
using NUnit.Framework;
using Soneta.Business;
using Soneta.Kadry;
using Soneta.Kalend;
using Soneta.Types;
using Prac = Soneta.Kadry.Pracownik;

namespace Soneta.Skills.Test.KadryPlace.Pracownik;

/// <summary>
/// Rozdział D — „Nieobecności i czas pracy" (receptury KADRY-D1, KADRY-D2, KADRY-D7).
/// <para>
/// Testy są <b>wykonywalną dokumentacją</b> publicznego kontraktu platformy Soneta dla obsługi
/// nieobecności pracownika oraz limitów urlopowych. Każda metoda mapuje się 1:1 do receptury
/// z dokumentu skilla <c>kadry/KADRY04-nieobecnosci.md</c>:
/// <list type="bullet">
/// <item><b>KADRY-D1</b> — wprowadzanie nieobecności (<c>NieobecnośćPracownika</c>, kolekcja <c>Nieobecnosci</c>);</item>
/// <item><b>KADRY-D2</b> — korygowanie nieobecności (zmiana okresu/typu, rekord <c>KorektaNieobecności</c>);</item>
/// <item><b>KADRY-D7</b> — analiza limitów urlopowych (naliczenie <c>NaliczanieLimitow.DodajLimit()</c> + odczyt z <c>pracownik.Limity</c>).</item>
/// </list>
/// </para>
/// <para>
/// Wszystko działa na bazie Demo (GoldStandard) z automatycznym rollbackiem po teście. Operujemy
/// wyłącznie na <b>publicznym kontrakcie</b> — tak jak dodatek programisty zewnętrznego bez dostępu
/// do kodu źródłowego aplikacji.
/// </para>
/// <para>
/// <b>Uwaga praktyczna (odkryta w trakcie testów):</b> ustawienie <c>Okres</c> na nieobecności typu
/// „urlop wypoczynkowy" wyzwala synchroniczne przeliczenie limitu i — gdy pracownik nie ma jeszcze
/// naliczonego limitu na ten dzień — rzuca <c>LimitNotFoundException</c>. Dlatego dla scenariuszy KADRY-D1/KADRY-D2
/// (czysta obsługa rekordu nieobecności) używamy typu nieobecności <b>niewymagającego limitu</b>
/// („Urlop bezpłatny (art 174 kp)"), a urlop wypoczynkowy testujemy dopiero po naliczeniu limitu (KADRY-D7).
/// </para>
/// </summary>
[TestFixture]
public class RozdzialD_NieobecnosciTest : PracownikTestBase
{
    // Typ nieobecności NIEwymagający naliczonego limitu — bezpieczny do scenariuszy obsługi rekordu.
    private const string DefBezplatny = "Urlop bezpłatny (art 174 kp)";
    private const string DefBezplatny2 = "Urlop bezpłatny (kod 350)";
    private const string DefUrlopWyp = "Urlop wypoczynkowy";

    // ============================== KADRY-D1 — Wprowadzanie nieobecności ==============================

    [Test]
    [Description("KADRY-D1: Nieobecnosc jest typem ABSTRAKCYJNYM; konkretnym typem nieobecności pracownika " +
                 "jest NieobecnośćPracownika (dziedziczy po Nieobecnosc) z ctorem (Pracownik).")]
    public void KADRY_D1_NieobecnoscPracownika_JestKonkretnymTypemNieobecnosci()
    {
        // Dokumentujemy regułę z receptury: new Nieobecnosc() jest niemożliwe (typ abstrakcyjny),
        // więc używamy NieobecnośćPracownika. Sprawdzamy relację dziedziczenia bez instancjonowania abstrakta.
        typeof(Nieobecnosc).IsAbstract.Should().BeTrue("Nieobecnosc jest klasą abstrakcyjną");
        typeof(Nieobecnosc).IsAssignableFrom(typeof(NieobecnośćPracownika))
            .Should().BeTrue("NieobecnośćPracownika jest konkretnym typem nieobecności pracownika");
    }

    [Test]
    [Description("KADRY-D1: nieobecność tworzymy NieobecnośćPracownika(pracownik) (ctor wiąże z pracownikiem) " +
                 "+ AddRow; ustawiamy Definicja (słownik DefNieobecnosci) i Okres (FromTo); zapis przez Save().")]
    public void KADRY_D1_WprowadzenieNieobecnosci_TworzyRekordWKolekcjiNieobecnosci()
    {
        var pracownik = Pracownik(Pracownik_.Andrzejewski);
        pracownik.Should().NotBeNull("pracownik z Demo istnieje");

        var def = Kalend.DefNieobecnosci.WgNazwy[DefBezplatny] as DefinicjaNieobecnosci;
        def.Should().NotBeNull($"definicja '{DefBezplatny}' istnieje w bazie Demo");

        var okres = new FromTo(new Date(2026, 7, 6), new Date(2026, 7, 10));

        InTransaction(() =>
        {
            // Typ konkretny; ctor NieobecnośćPracownika(pracownik) wiąże nieobecność z pracownikiem.
            var nieobecnosc = Session.AddRow(new NieobecnośćPracownika(pracownik));
            nieobecnosc.Definicja = def;   // rodzaj nieobecności (wymagany)
            nieobecnosc.Okres = okres;     // zakres dat „od–do"

            // Relacja Pracownik jest ustawiana przez ctor i jest tylko do odczytu.
            nieobecnosc.Pracownik.Should().BeSameAs(pracownik, "ctor wiąże nieobecność z pracownikiem");
        });
        SaveDispose();

        // Odczyt: nieobecność przecinająca lipiec 2026 została zapisana w kolekcji pracownika.
        var lipiec = new FromTo(new Date(2026, 7, 1), new Date(2026, 7, 31));
        var pracownik2 = Pracownik(Pracownik_.Andrzejewski);
        var nieobecnosci = pracownik2.Nieobecnosci.GetIntersectedRows(lipiec).Cast<Nieobecnosc>().ToList();

        nieobecnosci.Should().ContainSingle("dodaliśmy jedną nieobecność w lipcu 2026")
            .Which.Definicja.Nazwa.Should().Be(DefBezplatny);
        var zapisana = nieobecnosci[0];
        zapisana.Okres.From.Should().Be(okres.From);
        zapisana.Okres.To.Should().Be(okres.To);
    }

    [Test]
    [Description("KADRY-D1 (odczyt): pracownik.Nieobecnosci.GetIntersectedRows(FromTo) zwraca nieobecności " +
                 "przecinające zadany przedział; poza przedziałem nieobecność nie jest zwracana.")]
    public void KADRY_D1_GetIntersectedRows_FiltrujePoPrzecieciuOkresu()
    {
        var pracownik = Pracownik(Pracownik_.Bednarek);
        var def = Kalend.DefNieobecnosci.WgNazwy[DefBezplatny] as DefinicjaNieobecnosci;

        InTransaction(() =>
        {
            var n = Session.AddRow(new NieobecnośćPracownika(pracownik));
            n.Definicja = def;
            n.Okres = new FromTo(new Date(2026, 8, 3), new Date(2026, 8, 7));   // sierpień
        });
        SaveDispose();

        var pracownik2 = Pracownik(Pracownik_.Bednarek);

        // Przedział przecinający się z nieobecnością → znajduje rekord.
        var sierpien = new FromTo(new Date(2026, 8, 1), new Date(2026, 8, 31));
        pracownik2.Nieobecnosci.GetIntersectedRows(sierpien).Cast<Nieobecnosc>()
            .Should().ContainSingle("nieobecność przecina sierpień 2026");

        // Przedział rozłączny (wrzesień) → brak rekordu.
        var wrzesien = new FromTo(new Date(2026, 9, 1), new Date(2026, 9, 30));
        pracownik2.Nieobecnosci.GetIntersectedRows(wrzesien).Cast<Nieobecnosc>()
            .Should().BeEmpty("nieobecność nie przecina się z wrześniem 2026");
    }

    // ============================== KADRY-D2 — Korygowanie nieobecności ==============================

    [Test]
    [Description("KADRY-D2 (wariant A): okres nieobecności jest polem zapisywalnym — na istniejącym rekordzie " +
                 "można zmienić Okres (np. wydłużyć nieobecność) i utrwalić zmianę przez Save().")]
    public void KADRY_D2_ModyfikacjaOkresu_ZmianaIstniejacegoRekordu()
    {
        var pracownik = Pracownik(Pracownik_.Bujak);
        var def = Kalend.DefNieobecnosci.WgNazwy[DefBezplatny] as DefinicjaNieobecnosci;

        // Najpierw wprowadzamy nieobecność (stan „przed korektą").
        var okresStary = new FromTo(new Date(2026, 3, 2), new Date(2026, 3, 6));
        InTransaction(() =>
        {
            var n = Session.AddRow(new NieobecnośćPracownika(pracownik));
            n.Definicja = def;
            n.Okres = okresStary;
        });
        SaveDispose();

        // Korekta wariant A: odszukujemy istniejący rekord i wydłużamy jego okres.
        var okresNowy = new FromTo(new Date(2026, 3, 2), new Date(2026, 3, 11));
        InTransaction(() =>
        {
            var pracownikE = Pracownik(Pracownik_.Bujak);
            var nieobecnosc = (Nieobecnosc)pracownikE.Nieobecnosci.GetIntersectedRows(okresStary)[0];
            nieobecnosc.Okres = okresNowy;   // Okres jest polem zapisywalnym
        });
        SaveDispose();

        // Po korekcie istnieje jeden rekord z wydłużonym okresem.
        var pracownik2 = Pracownik(Pracownik_.Bujak);
        var marzec = new FromTo(new Date(2026, 3, 1), new Date(2026, 3, 31));
        var wynik = pracownik2.Nieobecnosci.GetIntersectedRows(marzec).Cast<Nieobecnosc>().ToList();
        wynik.Should().ContainSingle("modyfikacja okresu nie tworzy nowego rekordu");
        wynik[0].Okres.To.Should().Be(okresNowy.To, "okres został wydłużony do 2026-03-11");
    }

    [Test]
    [Description("KADRY-D2 (wariant A): zmiana typu nieobecności — pole Definicja jest zapisywalne, " +
                 "można podmienić rodzaj nieobecności na istniejącym rekordzie.")]
    public void KADRY_D2_ZmianaDefinicji_PodmieniaTypNieobecnosci()
    {
        var pracownik = Pracownik(Pracownik_.Strzelecki);
        var def1 = Kalend.DefNieobecnosci.WgNazwy[DefBezplatny] as DefinicjaNieobecnosci;
        var def2 = Kalend.DefNieobecnosci.WgNazwy[DefBezplatny2] as DefinicjaNieobecnosci;
        def2.Should().NotBeNull($"definicja '{DefBezplatny2}' istnieje w bazie Demo");

        var okres = new FromTo(new Date(2026, 4, 6), new Date(2026, 4, 10));
        InTransaction(() =>
        {
            var n = Session.AddRow(new NieobecnośćPracownika(pracownik));
            n.Definicja = def1;
            n.Okres = okres;
        });
        SaveDispose();

        // Korekta typu: podmiana definicji na inny rodzaj nieobecności bezpłatnej.
        InTransaction(() =>
        {
            var pracownikE = Pracownik(Pracownik_.Strzelecki);
            var def2e = Kalend.DefNieobecnosci.WgNazwy[DefBezplatny2] as DefinicjaNieobecnosci;
            var nieobecnosc = (Nieobecnosc)pracownikE.Nieobecnosci.GetIntersectedRows(okres)[0];
            nieobecnosc.Definicja = def2e;
        });
        SaveDispose();

        var pracownik2 = Pracownik(Pracownik_.Strzelecki);
        var wynik = pracownik2.Nieobecnosci.GetIntersectedRows(okres).Cast<Nieobecnosc>().Single();
        wynik.Definicja.Nazwa.Should().Be(DefBezplatny2, "typ nieobecności został zmieniony");
    }

    [Test]
    [Description("KADRY-D2 (wariant C): korektę dodajemy konstruktorem KorektaNieobecności(nieobecność) — " +
                 "rekord korygujący o okresie ZAWARTYM w okresie korygowanym; po zapisie nieobecność " +
                 "pierwotna zostaje oznaczona flagą Korygowana=true.")]
    public void KADRY_D2_KorektaNieobecnosci_OznaczaNieobecnoscJakoKorygowana()
    {
        var pracownik = Pracownik(Pracownik_.Andrzejewski);
        var def = Kalend.DefNieobecnosci.WgNazwy[DefBezplatny] as DefinicjaNieobecnosci;

        var okresPierwotny = new FromTo(new Date(2026, 5, 4), new Date(2026, 5, 8));
        // Stan „przed korektą": nieobecność nie jest korygowana.
        InTransaction(() =>
        {
            var n = Session.AddRow(new NieobecnośćPracownika(pracownik));
            n.Definicja = def;
            n.Okres = okresPierwotny;
            n.Korygowana.Should().BeFalse("świeża nieobecność nie jest jeszcze korygowana");
        });
        SaveDispose();

        // Wariant C: rekord korekty dotyczy NieobecnośćPracownika (ctor przyjmuje korygowaną nieobecność).
        // UWAGA: okres korekty jest OGRANICZONY do okresu nieobecności korygowanej (KorygowanyOkresException
        // przy próbie wyjścia poza), dlatego okres korekty musi być PODZBIOREM okresu pierwotnego.
        var okresKorekty = new FromTo(new Date(2026, 5, 4), new Date(2026, 5, 6));
        InTransaction(() =>
        {
            var pracownikE = Pracownik(Pracownik_.Andrzejewski);
            var nPrac = (NieobecnośćPracownika)pracownikE.Nieobecnosci.GetIntersectedRows(okresPierwotny)[0];

            var korekta = Session.AddRow(new KorektaNieobecności(nPrac));
            korekta.Definicja = nPrac.Definicja;
            korekta.Okres = okresKorekty;

            // KorektaNieobecności dziedziczy po Nieobecnosc.
            (korekta is Nieobecnosc).Should().BeTrue("KorektaNieobecności jest rodzajem Nieobecnosc");
        });
        SaveDispose();

        // Po korekcie nieobecność pierwotna istnieje i jest oznaczona jako korygowana.
        // (Dla nieobecności bez wyliczeń płacowych — jak urlop bezpłatny — sam rekord korekty nie tworzy
        //  drugiego, samodzielnego wpisu w kolekcji Nieobecnosci; obserwowalnym efektem jest flaga Korygowana.)
        var pracownik2 = Pracownik(Pracownik_.Andrzejewski);
        var maj = new FromTo(new Date(2026, 5, 1), new Date(2026, 5, 31));
        var rekordy = pracownik2.Nieobecnosci.GetIntersectedRows(maj).Cast<Nieobecnosc>().ToList();
        rekordy.Should().ContainSingle("nieobecność pierwotna nadal istnieje w kolekcji")
            .Which.Korygowana.Should().BeTrue("po dodaniu korekty nieobecność jest oznaczona jako korygowana");
    }

    [Test]
    [Ignore("Worker UstalPonowniePodstawęNaliczaniaWorker (KADRY-D2 wariant B) jest aktywny tylko dla zwolnień " +
            "ZUS / urlopów macierzyńskich (IsEnabledPonownieUstalPodstawę), a FAKTYCZNE przeliczenie kwot " +
            "zasiłku następuje dopiero przy ponownym naliczeniu wypłaty (mechanizm PodstawaZasilku). Na bazie " +
            "Demo z rollbackiem, bez pełnego scenariusza naliczenia listy płac, nie da się sensownie zweryfikować " +
            "efektu workera. LUKA w kadry/KADRY04-nieobecnosci.md KADRY-D2: dokument nie podaje minimalnego, wykonalnego scenariusza " +
            "naliczenia wypłaty pozwalającego zweryfikować przeliczenie podstawy.")]
    [Description("KADRY-D2 (wariant B): czynność 'Ustal ponownie podstawę naliczania' przez worker — " +
                 "niewykonalna na samej korekcie rekordu bez naliczonej wypłaty.")]
    public void KADRY_D2_PonowneUstaleniePodstawy_PrzezWorker_Niewykonalne()
    {
        // Pozostawione jako [Ignore] — patrz uzasadnienie w atrybucie.
    }

    // ============================== KADRY-D7 — Analiza limitów urlopowych ==============================

    [Test]
    [Description("KADRY-D7: limit urlopowy NIE jest tworzony ręcznie — najpierw naliczamy go " +
                 "NaliczanieLimitow.DodajLimit(), potem odczytujemy z pracownik.Limity; arytmetyka " +
                 "Wykorzystane == Razem - Pozostalo jest spójna.")]
    public void KADRY_D7_NaliczenieLimitu_TworzyLimitDoOdczytu()
    {
        var pracownik = Pracownik(Pracownik_.Andrzejewski);
        var defLimit = Kalend.DefinicjeLimitow.WgNazwy[DefUrlopWyp] as DefinicjaLimitu;
        defLimit.Should().NotBeNull($"definicja limitu '{DefUrlopWyp}' istnieje w bazie Demo");

        var rok = FromTo.Year(new Date(2026, 1, 1));

        InTransaction(() =>
        {
            // NaliczanieLimitow: publiczny bezparametrowy ctor; Params(Context) z bieżącej sesji testu.
            var naliczanie = new NaliczanieLimitow
            {
                Pars = new NaliczanieLimitow.Params(Context)
                {
                    Definicja = defLimit,
                    Okres = rok,
                    KopiujKorekty = true
                },
                Pracownicy = new[] { pracownik }
            };
            naliczanie.DodajLimit();   // tworzy/aktualizuje rekordy LimitNieobecnosci
        });
        SaveDispose();

        // Odczyt limitu — filtr serwerowy po kolekcji child pracownika TYLKO po Definicja
        // (porównanie FromTo == FromTo nie jest tłumaczone na zapytanie serwerowe — okres filtrujemy w pamięci).
        var pracownik2 = Pracownik(Pracownik_.Andrzejewski);
        var defLimit2 = Kalend.DefinicjeLimitow.WgNazwy[DefUrlopWyp] as DefinicjaLimitu;
        var lim = pracownik2.Limity[(LimitNieobecnosci l) => l.Definicja == defLimit2]
            .Cast<LimitNieobecnosci>()
            .FirstOrDefault(l => l.Okres.From == rok.From);

        lim.Should().NotBeNull("naliczenie utworzyło limit urlopu wypoczynkowego na 2026");
        // „Przysługujący" to Razem (limit kodeksowy + przeniesienia + zmiany), wykorzystany = Razem - Pozostalo.
        // Uwaga: dla syntetycznych pracowników Demo Razem bywa 0 (brak danych stażu/urodzenia napędzających 20/26 dni),
        // dlatego sprawdzamy spójność arytmetyki, a nie konkretną dodatnią wartość.
        (lim!.Razem - lim.Pozostalo).Should().Be(lim.Wykorzystane,
            "wykorzystany = przysługujący - pozostały (== pole Wykorzystane)");
        lim.Razem.Should().BeGreaterThanOrEqualTo(0, "przysługujący limit nie jest ujemny");
    }

    [Test]
    [Description("KADRY-D7: wprowadzenie urlopu wypoczynkowego wymaga ISTNIEJĄCEGO limitu na ten dzień — ustawienie " +
                 "Okres na nieobecności urlopowej wyzwala przeliczenie limitu; po wcześniejszym naliczeniu " +
                 "limitu zapis przechodzi bez LimitNotFoundException, a limit jest odczytywalny.")]
    public void KADRY_D7_UrlopWypoczynkowy_WymagaNaliczonegoLimitu()
    {
        var defLimit = Kalend.DefinicjeLimitow.WgNazwy[DefUrlopWyp] as DefinicjaLimitu;
        var rok = FromTo.Year(new Date(2026, 1, 1));

        // 1) Najpierw nalicz limit za rok — to warunek konieczny dla urlopu wypoczynkowego.
        InTransaction(() =>
        {
            var pracownikE = Pracownik(Pracownik_.Bednarek);
            var naliczanie = new NaliczanieLimitow
            {
                Pars = new NaliczanieLimitow.Params(Context)
                {
                    Definicja = defLimit,
                    Okres = rok,
                    KopiujKorekty = true
                },
                Pracownicy = new[] { pracownikE }
            };
            naliczanie.DodajLimit();
        });
        SaveDispose();

        // 2) Dopiero teraz wprowadzenie urlopu wypoczynkowego nie rzuca LimitNotFoundException
        //    (definicje pobieramy ponownie w bieżącej sesji — po SaveDispose poprzednie są z innej sesji).
        InTransaction(() =>
        {
            var pracownikE = Pracownik(Pracownik_.Bednarek);
            var defUrlop = Kalend.DefNieobecnosci.WgNazwy[DefUrlopWyp] as DefinicjaNieobecnosci;
            var n = Session.AddRow(new NieobecnośćPracownika(pracownikE));
            n.Definicja = defUrlop;
            n.Okres = new FromTo(new Date(2026, 6, 1), new Date(2026, 6, 5));
        });
        SaveDispose();

        // 3) Odczyt: limit istnieje i jest spójny; nieobecność urlopowa została zapisana.
        var pracownik2 = Pracownik(Pracownik_.Bednarek);
        var defLimit2 = Kalend.DefinicjeLimitow.WgNazwy[DefUrlopWyp] as DefinicjaLimitu;
        var lim = pracownik2.Limity[(LimitNieobecnosci l) => l.Definicja == defLimit2]
            .Cast<LimitNieobecnosci>()
            .FirstOrDefault(l => l.Okres.From == rok.From);
        lim.Should().NotBeNull("limit urlopu wypoczynkowego za 2026 został naliczony");
        lim!.Wykorzystane.Should().Be(lim.Razem - lim.Pozostalo,
            "wykorzystany odczytany z pola jest spójny z Razem - Pozostalo");

        var czerwiec = new FromTo(new Date(2026, 6, 1), new Date(2026, 6, 30));
        pracownik2.Nieobecnosci.GetIntersectedRows(czerwiec).Cast<Nieobecnosc>()
            .Should().ContainSingle("urlop wypoczynkowy został zapisany po naliczeniu limitu");
    }
}
