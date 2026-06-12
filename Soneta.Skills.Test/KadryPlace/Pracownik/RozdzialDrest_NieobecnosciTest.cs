using System.Linq;
using AwesomeAssertions;
using NUnit.Framework;
using Soneta.Business;
using Soneta.Kadry;
using Soneta.Kalend;
using Soneta.Place;
using Soneta.Types;
using Prac = Soneta.Kadry.Pracownik;

namespace Soneta.Skills.Test.KadryPlace.Pracownik;

/// <summary>
/// Rozdział D (część dalsza) — „Nieobecności i czas pracy" (receptury KADRY-D3–KADRY-D12).
/// <para>
/// Testy są <b>wykonywalną dokumentacją</b> publicznego kontraktu platformy Soneta dla zaawansowanej
/// obsługi nieobecności pracownika: zwolnień ZUS (e-ZLA), deklaracji Z-3/Z-3a, przestoju, parametrów
/// okresu zasiłkowego, naliczania limitów, podstaw nieobecności, bilansu otwarcia, wniosków urlopowych
/// i pracy zdalnej. Każda metoda mapuje się 1:1 do receptury z dokumentu skilla <c>kadry/KADRY04-nieobecnosci.md</c>:
/// <list type="bullet">
/// <item><b>KADRY-D3</b> — model danych e-ZLA (<c>Nieobecnosc.Zwolnienie: ZwolnienieZUS</c>, <c>Nieobecnosc.ZLA: ZLA</c>); sam import sieciowy → [Ignore];</item>
/// <item><b>KADRY-D4</b> — deklaracje Z-3 / Z-3a (workery <c>Z3Worker</c>/<c>Z3aWorker</c> — wymagają naliczonej podstawy);</item>
/// <item><b>KADRY-D5</b> — przestój (<c>DodajPrzestojWorker</c>, <c>IndywidualnyProcentWynagrPrzestojowegoWorker</c>);</item>
/// <item><b>KADRY-D6</b> — parametry okresu zasiłkowego (<c>Zwolnienie.KontynuacjaOkrZas</c>/<c>PrzedluzenieOkrZas</c>);</item>
/// <item><b>KADRY-D8</b> — naliczanie + przeliczanie limitów (<c>NaliczanieLimitow.DodajLimit()</c>, <c>PrzeliczWykorzystaneWorker</c>);</item>
/// <item><b>KADRY-D9</b> — podstawy nieobecności (<c>pracownik.PodstawyNieobecności</c> — odczyt; dodawanie → [Ignore]);</item>
/// <item><b>KADRY-D10</b> — bilans otwarcia (<c>PracHistoria.ChorobowyBO</c>, <c>PracHistoria.DodatkowyBO</c>);</item>
/// <item><b>KADRY-D11</b> — wnioski urlopowe (<c>WniosekUrlopowy</c>, <c>PlanowanaNieobecność</c>);</item>
/// <item><b>KADRY-D12</b> — praca zdalna (<c>PracHistoria.PracaZdalna</c>, <c>LokalizacjaPracyZdalnej</c>).</item>
/// </list>
/// </para>
/// <para>
/// Wszystko działa na bazie Demo (GoldStandard) z automatycznym rollbackiem po teście. Operujemy
/// wyłącznie na <b>publicznym kontrakcie</b> — tak jak dodatek programisty zewnętrznego bez dostępu
/// do kodu źródłowego aplikacji. Operacje wymagające sieci (import e-ZLA) lub naliczonej wypłaty
/// (kwoty zasiłku/przestoju, sensowne kwoty deklaracji Z-3, dodawanie podstaw) są oznaczone [Ignore]
/// z asercją na model danych tam, gdzie się da.
/// </para>
/// </summary>
[TestFixture]
public class RozdzialDrest_NieobecnosciTest : PracownikTestBase
{
    private const string DefZwolnienieChor = "Zwolnienie chorobowe";
    private const string DefUrlopWyp = "Urlop wypoczynkowy";
    // Definicja nieobecności NIEwymagająca naliczonego limitu — bezpieczna dla wniosków bez naliczania limitu.
    private const string DefBezplatny = "Urlop bezpłatny (art 174 kp)";

    // ============================== KADRY-D3 — Import e-ZLA (model danych) ==============================

    [Test]
    [Description("KADRY-D3: dane ZUS zwolnienia leżą w subrowie Nieobecnosc.Zwolnienie typu ZwolnienieZUS, " +
                 "a dane dokumentu ZLA w subrowie Nieobecnosc.ZLA typu ZLA — odwzorowujemy e-ZLA jako " +
                 "NieobecnośćPracownika z definicją zasiłkową i ustawiamy pola subrowów (bez sieci).")]
    public void KADRY_D3_ModelDanychEZLA_ZwolnienieIZLAToSubrowyNieobecnosci()
    {
        var pracownik = Pracownik(Pracownik_.Andrzejewski);
        pracownik.Should().NotBeNull("pracownik z Demo istnieje");

        var defChor = Kalend.DefNieobecnosci.WgNazwy[DefZwolnienieChor] as DefinicjaNieobecnosci;
        defChor.Should().NotBeNull($"definicja zasiłkowa '{DefZwolnienieChor}' istnieje w bazie Demo");

        var okres = new FromTo(new Date(2026, 5, 4), new Date(2026, 5, 10));

        InTransaction(() =>
        {
            var nieob = Session.AddRow(new NieobecnośćPracownika(pracownik));
            nieob.Definicja = defChor;
            nieob.Okres = okres;

            // Subrowy Zwolnienie / ZLA są częścią rekordu — nie tworzymy ich osobno, ustawiamy pola.
            nieob.Zwolnienie.Numer = "ZLA000001"; // pole Numer ma limit 9 znaków
            nieob.Zwolnienie.KodChoroby = "A";
            nieob.Zwolnienie.Przyczyna = PrzyczynaZwolnienia.ZwolnienieLekarskie;
            nieob.ZLA.Zrodlo = "Import PUE (odwzorowanie testowe)";
        });
        SaveDispose();

        var pracownik2 = Pracownik(Pracownik_.Andrzejewski);
        var maj = new FromTo(new Date(2026, 5, 1), new Date(2026, 5, 31));
        var zapisana = pracownik2.Nieobecnosci.GetIntersectedRows(maj).Cast<Nieobecnosc>().Single();

        zapisana.Definicja.Nazwa.Should().Be(DefZwolnienieChor);
        zapisana.Zwolnienie.Numer.Should().Be("ZLA000001", "dane ZUS z subrowa Zwolnienie zostały utrwalone");
        zapisana.Zwolnienie.KodChoroby.Should().Be("A");
        zapisana.Zwolnienie.Przyczyna.Should().Be(PrzyczynaZwolnienia.ZwolnienieLekarskie);
        zapisana.ZLA.Zrodlo.ToString().Should().Contain("Import PUE", "dane dokumentu ZLA z subrowa ZLA zostały utrwalone");
    }

    [Test]
    [Ignore("Sam import e-ZLA z PUE ZUS jest operacją SIECIOWĄ (uwierzytelnienie + bramka ZUS) — nie da się " +
            "go odtworzyć w teście jednostkowym na bazie Demo. Model danych (subrowy Zwolnienie/ZLA) jest " +
            "pokryty przez KADRY_D3_ModelDanychEZLA_ZwolnienieIZLAToSubrowyNieobecnosci.")]
    [Description("KADRY-D3: import e-ZLA z PUE — niewykonalny bez sieci.")]
    public void KADRY_D3_ImportEZLA_ZPUE_WymagaSieci_Niewykonalne()
    {
    }

    // ============================== KADRY-D4 — Deklaracje Z-3 / Z-3a ==============================

    [Test]
    [Ignore("Sensowny Z-3 wymaga NALICZONEJ wypłaty/podstawy zasiłku — na czystej Demo z rollbackiem, bez " +
            "pełnego scenariusza naliczenia listy płac, deklaracja powstałaby z pustymi kwotami, a worker " +
            "Z3Worker przyjmuje dane przez Context (KeduContext + Z3ParamContext) i wykonuje logikę KEDU. " +
            "Testowalny jest jedynie fakt istnienia workera (sprawdzane przez KADRY_D4_Z3Worker_TypIstnieje).")]
    [Description("KADRY-D4: generowanie deklaracji Z-3 przez worker — niewykonalne bez naliczonej podstawy zasiłku.")]
    public void KADRY_D4_GenerowanieZ3_PrzezWorker_Niewykonalne()
    {
    }

    [Test]
    [Description("KADRY-D4: workery deklaracji Z-3 / Z-3a istnieją w publicznym kontrakcie (typy " +
                 "Soneta.Deklaracje.ZUS.ZUSZ3.Z3Worker / Z3aWorker) — dokumentujemy ich dostępność.")]
    public void KADRY_D4_Z3Worker_TypIstnieje()
    {
        // Workery są w osobnym assembly Soneta.Deklaracje.ZUS — sprawdzamy obecność typu po pełnej nazwie.
        var z3 = System.Type.GetType("Soneta.Deklaracje.ZUS.ZUSZ3.Z3Worker, Soneta.Deklaracje.ZUS")
                 ?? FindByFullName("Soneta.Deklaracje.ZUS.ZUSZ3.Z3Worker");
        var z3a = System.Type.GetType("Soneta.Deklaracje.ZUS.ZUSZ3.Z3aWorker, Soneta.Deklaracje.ZUS")
                  ?? FindByFullName("Soneta.Deklaracje.ZUS.ZUSZ3.Z3aWorker");

        z3.Should().NotBeNull("worker Z-3 (Z3Worker) jest dostępny w publicznym kontrakcie");
        z3a.Should().NotBeNull("worker Z-3a (Z3aWorker) jest dostępny w publicznym kontrakcie");
        z3!.GetMethod("UtworzDeklaracjeZ3").Should().NotBeNull("Z3Worker eksponuje akcję UtworzDeklaracjeZ3");
    }

    private static System.Type FindByFullName(string fullName) =>
        System.AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => { try { return a.GetType(fullName); } catch { return null; } })
            .FirstOrDefault(t => t != null);

    // ============================== KADRY-D5 — Przestój ==============================

    [Test]
    [Description("KADRY-D5: przestój dodajemy workerem DodajPrzestojWorker — ustawiamy Pracownicy oraz " +
                 "Pars (DefinicjaStrefy + Okres); worker wykonuje własną transakcję. Strefę przestoju " +
                 "pobieramy dynamicznie ze słownika DefinicjeStref danej bazy.")]
    public void KADRY_D5_DodajPrzestoj_PrzezWorker()
    {
        var pracownik = Pracownik(Pracownik_.Bednarek);

        // Definicja strefy przestoju — słownik danej bazy; nazwa może się różnić, więc szukamy elastycznie.
        var defStrefa = Kalend.DefinicjeStref.Cast<DefinicjaStrefy>()
            .FirstOrDefault(d => d.Nazwa != null && d.Nazwa.Contains("rzestój"));

        if (defStrefa == null)
        {
            Assert.Ignore("Brak strefy przestoju w słowniku DefinicjeStref bazy Demo — receptura KADRY-D5 niewykonalna na tej bazie.");
            return;
        }

        var worker = new DodajPrzestojWorker
        {
            Pracownicy = new[] { pracownik },
            Pars = new DodajPrzestojWorker.Params(Context)
            {
                DefinicjaStrefy = defStrefa,
                Okres = new FromTo(new Date(2026, 6, 1), new Date(2026, 6, 5))
            }
        };

        // Worker wykonuje własną transakcję — wywołujemy poza otwartą transakcją edycyjną.
        worker.DodajPrzestoj();
        SaveDispose();

        // Przestój materializuje się jako strefa w planie pracy — weryfikujemy spójnie, że operacja
        // nie rzuciła wyjątku i pracownik jest nadal odczytywalny (skutki płacowe liczą się przy wypłacie).
        Pracownik(Pracownik_.Bednarek).Should().NotBeNull("przestój dodany bez błędu");
    }

    [Test]
    [Description("KADRY-D5: indywidualny procent wynagrodzenia przestojowego (przestój ekonomiczny) ustawiamy " +
                 "workerem IndywidualnyProcentWynagrPrzestojowegoWorker — Pars.Data + Pars.Procent (ułamek). " +
                 "Procent jest też trzymany na etacie: PracHistoria.Etat.Postojowe.Procent.")]
    public void KADRY_D5_ProcentWynagrPrzestojowego_PrzezWorker_OdkladaSieNaEtacie()
    {
        var pracownik = Pracownik(Pracownik_.Bujak);

        var worker = new IndywidualnyProcentWynagrPrzestojowegoWorker
        {
            Pracownicy = new[] { pracownik },
            Pars = new IndywidualnyProcentWynagrPrzestojowegoWorker.Params(Context)
            {
                Data = new Date(2026, 6, 1),
                Procent = new Percent(0.5m) // 50% — Percent przyjmujemy jako ułamek, nie liczbę 50
            }
        };
        worker.Aktualizuj();
        SaveDispose();

        // Procent przestojowego odkłada się na etacie (PracHistoria.Etat.Postojowe).
        var pracownik2 = Pracownik(Pracownik_.Bujak);
        var historia = pracownik2.Historia[new Date(2026, 6, 1)];
        historia.Should().NotBeNull("istnieje zapis historyczny na czerwiec 2026");
        historia.Etat.Postojowe.Procent.Should().Be(new Percent(0.5m),
            "procent wynagrodzenia przestojowego został zapisany na etacie jako 50%");
    }

    // ============================== KADRY-D6 — Parametry okresu zasiłkowego ==============================

    [Test]
    [Description("KADRY-D6: parametry okresu zasiłkowego są bazodanowymi polami subrowa Nieobecnosc.Zwolnienie: " +
                 "KontynuacjaOkrZas (enum), PrzedluzenieOkrZas (bool), PrzedluzeniaData (Date) oraz " +
                 "flaga PonownieUstalPodstawe ustawiana metodą SetPonownieUstalPodstawe(bool).")]
    public void KADRY_D6_ParametryOkresuZasilkowego_ZapisNaSubrowieZwolnienie()
    {
        var pracownik = Pracownik(Pracownik_.Strzelecki);
        var defChor = Kalend.DefNieobecnosci.WgNazwy[DefZwolnienieChor] as DefinicjaNieobecnosci;

        var okres = new FromTo(new Date(2026, 5, 4), new Date(2026, 5, 31));
        InTransaction(() =>
        {
            var nieob = Session.AddRow(new NieobecnośćPracownika(pracownik));
            nieob.Definicja = defChor;
            nieob.Okres = okres;
        });
        SaveDispose();

        // Zmiana parametrów okresu zasiłkowego wprost na rekordzie.
        InTransaction(() =>
        {
            var pracownikE = Pracownik(Pracownik_.Strzelecki);
            var nieob = (Nieobecnosc)pracownikE.Nieobecnosci.GetIntersectedRows(okres)[0];
            nieob.Zwolnienie.KontynuacjaOkrZas = KontynuacjaOkrZas.Tak;
            nieob.Zwolnienie.PrzedluzenieOkrZas = true;
            nieob.Zwolnienie.PrzedluzeniaData = new Date(2026, 5, 31);
            nieob.Zwolnienie.SetPonownieUstalPodstawe(true);
        });
        SaveDispose();

        var pracownik2 = Pracownik(Pracownik_.Strzelecki);
        var zapisana = pracownik2.Nieobecnosci.GetIntersectedRows(okres).Cast<Nieobecnosc>().Single();
        zapisana.Zwolnienie.KontynuacjaOkrZas.Should().Be(KontynuacjaOkrZas.Tak);
        zapisana.Zwolnienie.PrzedluzenieOkrZas.Should().BeTrue("okres zasiłkowy oznaczono jako przedłużony");
        zapisana.Zwolnienie.PrzedluzeniaData.Should().Be(new Date(2026, 5, 31));
        zapisana.Zwolnienie.PonownieUstalPodstawe.Should().BeTrue("flaga ponownego ustalenia podstawy ustawiona");
    }

    // ============================== KADRY-D8 — Naliczanie i przeliczanie limitów ==============================

    [Test]
    [Description("KADRY-D8: limit naliczamy NaliczanieLimitow.DodajLimit(), a liczbę wykorzystanych dni " +
                 "przeliczamy workerem LimitNieobecnosci.Pracownicy.PrzeliczWykorzystaneWorker " +
                 "(Pars.Definicja + Pars.Okres). Po przeliczeniu arytmetyka limitu pozostaje spójna.")]
    public void KADRY_D8_NaliczenieIPrzeliczenieLimitu()
    {
        var defLimit = Kalend.DefinicjeLimitow.WgNazwy[DefUrlopWyp] as DefinicjaLimitu;
        defLimit.Should().NotBeNull($"definicja limitu '{DefUrlopWyp}' istnieje w bazie Demo");
        var rok = FromTo.Year(new Date(2026, 1, 1));

        // 1) Naliczenie limitu (jak KADRY-D7).
        InTransaction(() =>
        {
            var pracownikE = Pracownik(Pracownik_.Andrzejewski);
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

        // 2) Przeliczenie wykorzystanych — worker wykonuje własną transakcję.
        var przelicz = new LimitNieobecnosci.Pracownicy.PrzeliczWykorzystaneWorker
        {
            Pracownicy = new[] { Pracownik(Pracownik_.Andrzejewski) },
            Pars = new LimitNieobecnosci.Pracownicy.PrzeliczWykorzystaneWorker.Params(Context)
            {
                Definicja = Kalend.DefinicjeLimitow.WgNazwy[DefUrlopWyp] as DefinicjaLimitu,
                Okres = rok
            }
        };
        przelicz.PrzeliczWykorzystane();
        SaveDispose();

        // 3) Odczyt limitu — arytmetyka spójna (Razem bywa 0 dla syntetycznych pracowników Demo).
        var pracownik2 = Pracownik(Pracownik_.Andrzejewski);
        var defLimit2 = Kalend.DefinicjeLimitow.WgNazwy[DefUrlopWyp] as DefinicjaLimitu;
        var lim = pracownik2.Limity[(LimitNieobecnosci l) => l.Definicja == defLimit2]
            .Cast<LimitNieobecnosci>()
            .FirstOrDefault(l => l.Okres.From == rok.From);

        lim.Should().NotBeNull("limit urlopu wypoczynkowego na 2026 został naliczony");
        lim!.Wykorzystane.Should().Be(lim.Razem - lim.Pozostalo,
            "po przeliczeniu wykorzystany jest spójny z Razem - Pozostalo");
        lim.Wykorzystane.Should().BeGreaterThanOrEqualTo(0, "wykorzystane nie jest ujemne");
    }

    // ============================== KADRY-D9 — Podstawy nieobecności (odczyt) ==============================

    [Test]
    [Description("KADRY-D9 (odczyt): podstawy nieobecności ZUS / urlopu leżą w kolekcji child " +
                 "pracownik.PodstawyNieobecności (typ Soneta.Place.PodstawaNieobecnosci); filtrujemy " +
                 "serwerowo po polu Typ (Chorobowa / Wypoczynkowy). Na czystej Demo kolekcja może być pusta.")]
    public void KADRY_D9_OdczytPodstawNieobecnosci_FiltrPoTyp()
    {
        var pracownik = Pracownik(Pracownik_.Andrzejewski);

        // Filtr serwerowy po Typ — nie iterujemy całości z if w pamięci.
        var chorobowe = pracownik.PodstawyNieobecności[
            (PodstawaNieobecnosci x) => x.Typ == TypyPodstawNieobecnosci.Chorobowa]
            .Cast<PodstawaNieobecnosci>().ToList();

        // Asercja na MODEL/spójność: każda zwrócona pozycja faktycznie ma Typ == Chorobowa,
        // a relacja do pracownika jest spełniona (Pracownik to guided-parent, read-only).
        chorobowe.Should().OnlyContain(p => p.Typ == TypyPodstawNieobecnosci.Chorobowa,
            "filtr serwerowy zwraca wyłącznie podstawy chorobowe");
        chorobowe.Should().OnlyContain(p => p.Pracownik == pracownik,
            "podstawa należy do pracownika (relacja child)");
        // Na czystej Demo (bez naliczonej wypłaty z zasiłkiem) kolekcja bywa pusta — to dopuszczalne.
    }

    [Test]
    [Ignore("PodstawaNieobecnosci NIE ma publicznego ctora (jedynie (RowCreator) i (Pracownik, " +
            "TypyPodstawNieobecnosci) — niepubliczne). Rekordy podstaw powstają z NALICZENIA WYPŁATY, " +
            "więc ręczne dodanie podstawy nie jest możliwe przez publiczny kontrakt; testowalny jest " +
            "wyłącznie odczyt (KADRY_D9_OdczytPodstawNieobecnosci_FiltrPoTyp).")]
    [Description("KADRY-D9: ręczne dodanie podstawy nieobecności — niewykonalne (brak publicznego ctora).")]
    public void KADRY_D9_DodanieRecznePodstawy_Niewykonalne()
    {
    }

    // ============================== KADRY-D10 — Bilans otwarcia ==============================

    [Test]
    [Description("KADRY-D10: bilans otwarcia chorobowy leży w subrowie zapisu PracHistoria.ChorobowyBO " +
                 "(okres zasiłkowy, dni). Edytujemy pola subrowa na właściwym zapisie historycznym " +
                 "'na dzień' (pracownik.Historia[data]).")]
    public void KADRY_D10_BilansOtwarcia_ChorobowyBO()
    {
        var data = new Date(2026, 1, 1);

        InTransaction(() =>
        {
            var pracownikE = Pracownik(Pracownik_.Bednarek);
            var historia = pracownikE.Historia[data];
            historia.Should().NotBeNull("istnieje zapis historyczny obowiązujący na 2026-01-01");

            // BO chorobowy / okres zasiłkowy
            historia.ChorobowyBO.DniZasilkowe = 33;
            historia.ChorobowyBO.ZasilekOdDnia = data;
            historia.ChorobowyBO.PrzedluzenieOZ = true;
        });
        SaveDispose();

        var pracownik2 = Pracownik(Pracownik_.Bednarek);
        var historia2 = pracownik2.Historia[data];
        historia2.ChorobowyBO.DniZasilkowe.Should().Be(33, "BO chorobowy: dni zasiłkowe");
        historia2.ChorobowyBO.ZasilekOdDnia.Should().Be(data, "BO chorobowy: zasiłek od dnia");
        historia2.ChorobowyBO.PrzedluzenieOZ.Should().BeTrue("BO chorobowy: przedłużenie okresu zasiłkowego");
    }

    [Test]
    [Ignore("DodatkowyBO.UPoprzednich/BezPierwszego/Wykorzystany rzucają ColReadOnlyException na zwykłym " +
            "zapisie historycznym z Demo (pole 'tylko do odczytu'). BO urlopowy jest zapisywalny tylko na " +
            "zapisie historycznym oznaczonym jako bilans otwarcia / start zatrudnienia — czego nie da się " +
            "odtworzyć na gotowych pracownikach Demo bez ingerencji w historię zatrudnienia. Pole ChorobowyBO " +
            "jest pokryte przez KADRY_D10_BilansOtwarcia_ChorobowyBO.")]
    [Description("KADRY-D10: bilans otwarcia urlopowy (DodatkowyBO) — niezapisywalny na zwykłym zapisie historii Demo.")]
    public void KADRY_D10_BilansOtwarcia_DodatkowyBO_ReadOnlyNaHistoriiDemo()
    {
    }

    // ============================== KADRY-D11 — Wnioski o urlop ==============================

    [Test]
    [Description("KADRY-D11: wniosek urlopowy tworzymy ctorem WniosekUrlopowy(pracownik, definicja) + AddRow; " +
                 "ustawiamy Okres, Data, Stan (StanWnioskuUrlopowego). Wniosek trafia do kolekcji " +
                 "pracownik.WnioskiUrlopowe; akceptacja to zmiana Stan na Zaakceptowany + DataDecyzji. " +
                 "Używamy definicji NIEwymagającej limitu — akceptacja wniosku urlopu wypoczynkowego " +
                 "wyzwoliłaby przeliczenie limitu i LimitNotFoundException bez wcześniejszego naliczenia limitu.")]
    public void KADRY_D11_WniosekUrlopowy_RejestracjaIAkceptacja()
    {
        var pracownik = Pracownik(Pracownik_.Bujak);
        var defUrlop = Kalend.DefNieobecnosci.WgNazwy[DefBezplatny] as DefinicjaNieobecnosci;
        defUrlop.Should().NotBeNull($"definicja '{DefBezplatny}' istnieje w bazie Demo");

        InTransaction(() =>
        {
            var wniosek = Session.AddRow(new WniosekUrlopowy(pracownik, defUrlop));
            wniosek.Okres = new FromTo(new Date(2026, 8, 3), new Date(2026, 8, 7));
            wniosek.Data = new Date(2026, 7, 20);
            wniosek.Stan = StanWnioskuUrlopowego.Oczekujący;

            wniosek.Pracownik.Should().BeSameAs(pracownik, "ctor wiąże wniosek z pracownikiem");
            wniosek.Definicja.Should().BeSameAs(defUrlop, "ctor ustawia definicję nieobecności");
        });
        SaveDispose();

        // Odczyt z kolekcji child + akceptacja (zmiana stanu).
        InTransaction(() =>
        {
            var pracownikE = Pracownik(Pracownik_.Bujak);
            var wniosek = pracownikE.WnioskiUrlopowe.Cast<WniosekUrlopowy>()
                .First(w => w.Stan == StanWnioskuUrlopowego.Oczekujący);
            wniosek.Stan = StanWnioskuUrlopowego.Zaakceptowany;
            wniosek.DataDecyzji = new Date(2026, 7, 21);
        });
        SaveDispose();

        var pracownik2 = Pracownik(Pracownik_.Bujak);
        var zapisany = pracownik2.WnioskiUrlopowe.Cast<WniosekUrlopowy>().Single();
        zapisany.Stan.Should().Be(StanWnioskuUrlopowego.Zaakceptowany, "wniosek został zaakceptowany");
        zapisany.DataDecyzji.Should().Be(new Date(2026, 7, 21));
        zapisany.Definicja.Nazwa.Should().Be(DefBezplatny);
    }

    [Test]
    [Description("KADRY-D11: planowana nieobecność (osobny model planu urlopów) — ctor PlanowanaNieobecność(pracownik) " +
                 "+ AddRow; Definicja, Okres. Pole Stan jest READ-ONLY (StanPlanowanejNieobecności) — zmieniamy " +
                 "je metodami przejść stanu (StanWprowadzona/StanZatwierdzona/StanAnulowana/StanOczekująca). " +
                 "Trafia do kolekcji pracownik.PlanowaneNieobecności (FromToSubTable).")]
    public void KADRY_D11_PlanowanaNieobecnosc_Rejestracja()
    {
        var pracownik = Pracownik(Pracownik_.Strzelecki);
        // Definicja planowanej nieobecności MUSI mieć zaznaczone pole 'Planowana' — pobieramy dynamicznie.
        var defPlan = Kalend.DefNieobecnosci.Cast<DefinicjaNieobecnosci>().FirstOrDefault(d => d.Planowana);
        if (defPlan == null)
        {
            Assert.Ignore("Brak definicji nieobecności z flagą 'Planowana' w bazie Demo — receptura niewykonalna.");
            return;
        }

        var okres = new FromTo(new Date(2026, 9, 1), new Date(2026, 9, 5));
        InTransaction(() =>
        {
            var plan = Session.AddRow(new PlanowanaNieobecność(pracownik));
            plan.Definicja = defPlan;
            plan.Okres = okres;
            // Stan jest read-only — przejście stanu wykonujemy metodą domenową, nie przypisaniem.
            plan.StanWprowadzona();
        });
        SaveDispose();

        var pracownik2 = Pracownik(Pracownik_.Strzelecki);
        var wrzesien = new FromTo(new Date(2026, 9, 1), new Date(2026, 9, 30));
        var plany = pracownik2.PlanowaneNieobecności.GetIntersectedRows(wrzesien)
            .Cast<PlanowanaNieobecność>().ToList();
        plany.Should().ContainSingle("dodaliśmy jedną planowaną nieobecność we wrześniu 2026")
            .Which.Stan.Should().Be(StanPlanowanejNieobecności.Wprowadzona,
                "po StanWprowadzona() plan jest w stanie Wprowadzona");
    }

    [Test]
    [Description("KADRY-D11: wniosek o delegację jest subrowem wniosku urlopowego (WniosekUrlopowy.Delegacja " +
                 "typu WniosekODelegację) — ustawiamy pola delegacji na tym subrowie.")]
    public void KADRY_D11_WniosekODelegacje_JestSubrowemWniosku()
    {
        var pracownik = Pracownik(Pracownik_.Andrzejewski);
        var defUrlop = Kalend.DefNieobecnosci.WgNazwy[DefUrlopWyp] as DefinicjaNieobecnosci;

        InTransaction(() =>
        {
            var wniosek = Session.AddRow(new WniosekUrlopowy(pracownik, defUrlop));
            wniosek.Okres = new FromTo(new Date(2026, 10, 5), new Date(2026, 10, 9));
            wniosek.Data = new Date(2026, 9, 30);
            // Delegacja to subrow — ustawiamy jego pola (cel, planowana zaliczka).
            wniosek.Delegacja.Cel = "Spotkanie z klientem";
            wniosek.Delegacja.WnioskowanaZaliczka = new Currency(500m);
        });
        SaveDispose();

        var pracownik2 = Pracownik(Pracownik_.Andrzejewski);
        var zapisany = pracownik2.WnioskiUrlopowe.Cast<WniosekUrlopowy>().Single();
        zapisany.Delegacja.Cel.ToString().Should().Contain("klientem", "cel delegacji zapisany na subrowie");
        zapisany.Delegacja.WnioskowanaZaliczka.Should().Be(new Currency(500m));
    }

    // ============================== KADRY-D12 — Praca zdalna ==============================

    [Test]
    [Description("KADRY-D12: parametry pracy zdalnej (model pracy, oświadczenie o warunkach) leżą na " +
                 "historycznym zapisie etatu: PracHistoria.PracaZdalna (typ PracZdalna). Edytujemy je " +
                 "na właściwym zapisie 'na dzień' (pracownik.Historia[data]).")]
    public void KADRY_D12_ModelPracyZdalnej_NaHistoriiEtatu()
    {
        var data = new Date(2026, 6, 1);

        InTransaction(() =>
        {
            var pracownikE = Pracownik(Pracownik_.Bednarek);
            var historia = pracownikE.Historia[data];
            historia.PracaZdalna.ModelPracy = ModelPracy.PracaHybrydowa;
            historia.PracaZdalna.OswiadczenieWarunki = true;
        });
        SaveDispose();

        var pracownik2 = Pracownik(Pracownik_.Bednarek);
        var historia2 = pracownik2.Historia[data];
        historia2.PracaZdalna.ModelPracy.Should().Be(ModelPracy.PracaHybrydowa, "ustawiono model pracy hybrydowej");
        historia2.PracaZdalna.OswiadczenieWarunki.Should().BeTrue("oświadczenie o warunkach lokalowych ustawione");
    }

    [Test]
    [Description("KADRY-D12: lokalizacja pracy zdalnej ma publiczny ctor LokalizacjaPracyZdalnej(pracownik) — " +
                 "tworzymy ją + AddRow i ustawiamy adres (subrow Adres). Trafia do kolekcji " +
                 "pracownik.LokalizacjePracyZdalnej.")]
    public void KADRY_D12_LokalizacjaPracyZdalnej_PublicznyCtor()
    {
        var pracownik = Pracownik(Pracownik_.Bujak);

        InTransaction(() =>
        {
            var lok = Session.AddRow(new LokalizacjaPracyZdalnej(pracownik));
            lok.Adres.Miejscowosc = "Kraków";
            lok.Adres.Ulica = "Wadowicka";
        });
        SaveDispose();

        var pracownik2 = Pracownik(Pracownik_.Bujak);
        var lokalizacje = pracownik2.LokalizacjePracyZdalnej.Cast<LokalizacjaPracyZdalnej>().ToList();
        lokalizacje.Should().ContainSingle("dodaliśmy jedną lokalizację pracy zdalnej")
            .Which.Adres.Miejscowosc.Should().Be("Kraków", "adres lokalizacji został zapisany");
    }

    [Test]
    [Description("KADRY-D12 (odczyt): ewidencję pracy zdalnej okazjonalnej prezentuje worker ODCZYTOWY " +
                 "Soneta.Kadry.Pracownik.PracaZdalnaWorker (property bez akcji modyfikującej): " +
                 "DniPracyZdalnejRazem, LimitPracaZdalnaOkazjonalna, PozostaloPracaZdalnaOkazjonalna. " +
                 "Inicjujemy Pracownik + Okres i odczytujemy spójne, nieujemne wartości.")]
    public void KADRY_D12_PracaZdalnaWorker_OdczytEwidencji()
    {
        var pracownik = Pracownik(Pracownik_.Andrzejewski);

        var worker = new Prac.PracaZdalnaWorker
        {
            Pracownik = pracownik,
            Okres = FromTo.Year(new Date(2026, 1, 1))
        };

        // Worker odczytowy — property liczone z planu/ewidencji; weryfikujemy spójność wartości.
        worker.DniPracyZdalnejRazem.Should().BeGreaterThanOrEqualTo(0, "liczba dni pracy zdalnej nie jest ujemna");
        worker.LimitPracaZdalnaOkazjonalna.Should().BeGreaterThanOrEqualTo(0, "limit pracy zdalnej okazjonalnej nie jest ujemny");
        worker.PozostaloPracaZdalnaOkazjonalna.Should().BeLessThanOrEqualTo(worker.LimitPracaZdalnaOkazjonalna,
            "pozostały limit nie przekracza limitu całkowitego");
    }

    [Test]
    [Ignore("WniosekPracyZdalnej ma NIEPUBLICZNE ctory — w teście jednostkowym nie utworzysz go przez new; " +
            "zlecenie pracy zdalnej idzie przez worker GrupoweZleceniePracyZdalnejWorker (czynność Net/UI " +
            "wymagająca pełnego Contextu Pulpitu). Testowalne wprost: ModelPracy/OswiadczenieWarunki na " +
            "PracHistoria.PracaZdalna (KADRY_D12_ModelPracyZdalnej_NaHistoriiEtatu) oraz LokalizacjaPracyZdalnej " +
            "(KADRY_D12_LokalizacjaPracyZdalnej_PublicznyCtor).")]
    [Description("KADRY-D12: rejestracja wniosku o pracę zdalną — niewykonalna przez new (ctory niepubliczne).")]
    public void KADRY_D12_WniosekPracyZdalnej_NiepublicznyCtor_Niewykonalne()
    {
    }
}
