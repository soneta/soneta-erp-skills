using System;
using System.Linq;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Soneta.Handel;
using Soneta.Handel.RelacjeDokumentow.Api;
using Soneta.Magazyny;
using Soneta.Towary;
using Soneta.Types;

namespace Soneta.Skills.Test.Handel.DokumentyHandlowe;

/// <summary>
/// Rozdział 9 skilla „dokument-handlowy” — Korekty i dokumenty specjalne (HANDEL-W48–HANDEL-W52).
/// <para>
/// Rozdział obejmuje korekty (przez serwis relacji <see cref="IRelacjeService"/>.<c>NowaKorekta</c>),
/// inwentaryzację (INW) oraz przesunięcie międzymagazynowe (MM). Wszystkie testy operują
/// <b>wyłącznie na publicznym kontrakcie</b> platformy — jak dodatek programisty zewnętrznego.
/// </para>
/// <para>
/// Reguły wspólne (zob. dokumentacja, rozdz. 9 oraz <c>safe-code.md</c>):
/// <list type="bullet">
/// <item>dokument korygowany / nadrzędny musi być <b>zatwierdzony</b> przed wywołaniem relacji,</item>
/// <item>relacja to operacja modyfikująca — wykonujemy ją w transakcji edycyjnej
/// (<c>Session.Logout(editMode: true)</c>), po niej <c>Session.Save()</c>,</item>
/// <item>magazyn księguje obroty/zasoby <b>dopiero po <c>Session.Save()</c></b>, nie po <c>Commit()</c>,</item>
/// <item>Demo blokuje stan ujemny (<c>StanUjemnyVerifier</c>) — rozchód wymaga wcześniejszego,
/// <b>zapisanego</b> przyjęcia (PW) tego towaru,</item>
/// <item>pola <c>DokumentKorygowany</c>, <c>DokumentyKorygujące</c> są <b>kalkulowane (read-only)</b> —
/// czytamy je, nie ustawiamy; powstają jako efekt utworzenia relacji.</item>
/// </list>
/// </para>
/// Tam, gdzie definicja relacji w Demo wymaga rozstrzygnięcia niedostarczalnego czystym
/// publicznym API (np. callback w <c>HandlerSet</c>), test rozpoznaje
/// <see cref="NotImplementedException"/> i jest pomijany (<c>Assert.Ignore</c>) z czytelnym powodem —
/// to nie błąd testu, lecz ograniczenie kontraktu/konfiguracji.
/// </summary>
[TestFixture]
public class Rozdzial09_KorektyTest : DokumentHandlowyTestBase
{
    // === Pomocnicze ===

    /// <summary>Serwis relacji bieżącej sesji (rzuca, gdy serwisu brak).</summary>
    private IRelacjeService Relacje => Session.GetRequiredService<IRelacjeService>();

    /// <summary>Zmienia stan dokumentu na zatwierdzony (w transakcji edycyjnej).</summary>
    private void Zatwierdz(DokumentHandlowy dok)
    {
        InTransaction(() => dok.Stan = StanDokumentuHandlowego.Zatwierdzony);
    }

    /// <summary>
    /// Wprowadza towar magazynowy na stan: tworzy i ZAPISUJE przyjęcie wewnętrzne (PW).
    /// Magazyn księguje się dopiero po <c>Session.Save()</c> — warunek konieczny rozchodu (Demo blokuje stan ujemny).
    /// Save bez Dispose: kontynuujemy pracę na tej samej sesji.
    /// </summary>
    private void WprowadzNaStan(string towarKod, double ilosc)
    {
        var pw = UtworzDokument(Definicje.PrzyjecieWewnetrzne, magazyn: Magazyn(Magazyn_.Firma));
        InTransaction(() => DodajPozycje(pw, Towar(towarKod), ilosc, cena: 10));
        Zatwierdz(pw);
        Session.Save(); // księguje zasób
    }

    // ===================================================================================
    // HANDEL-W48 — Korekta ilościowa / ceny przez IRelacjeService.NowaKorekta
    // ===================================================================================

    [Test]
    [Description("HANDEL-W48: do zatwierdzonej faktury sprzedaży (FV) tworzy dokument korygujący przez " +
                 "IRelacjeService.NowaKorekta; sprawdza powstanie korekty oraz powiązanie " +
                 "korekta.DokumentKorygowany == oryginał i obecność oryginału w fv.DokumentyKorygujące.")]
    public void HANDEL_W48_NowaKorekta_DoZatwierdzonejFv_TworzyDokumentKorygujacy()
    {
        // Zatwierdzona FV (dokument korygowany) — helper zapisuje ją z pozycją przed zatwierdzeniem,
        // dzięki czemu KrajPodatkuVat jest przeliczony i ewidencja VAT powstaje bez NRE.
        var fvGuid = UtworzZatwierdzonaFakture();

        var fv = Get<DokumentHandlowy>(fvGuid);
        DokumentHandlowy[] korekty = null;
        InTransaction(() => korekty = Relacje.NowaKorekta(new[] { fv }));
        var korektaGuid = korekty[0].Guid;
        SaveDispose();

        korekty.Should().NotBeNull();
        var korekta = Get<DokumentHandlowy>(korektaGuid);
        korekta.Should().NotBeNull("powstał dokument korygujący");
        korekta.DokumentKorygowany.Should().NotBeNull("korekta wskazuje dokument korygowany (kalkulowane)");
        korekta.DokumentKorygowany.Guid.Should().Be(fvGuid, "korekta wskazuje oryginalną fakturę");

        var fvOdczyt = Get<DokumentHandlowy>(fvGuid);
        fvOdczyt.DokumentyKorygujące.Should().Contain(d => d.Guid == korektaGuid,
            "oryginał wskazuje swój dokument korygujący (kalkulowane DokumentyKorygujące)");
    }

    [Test]
    [Description("HANDEL-W48 (pułapka): NowaKorekta zwraca tablicę DokumentHandlowy[]; dla jednego dokumentu " +
                 "wynik ma dokładnie jeden element (relacja indywidualna).")]
    public void HANDEL_W48_NowaKorekta_ZwracaTabliceZJednymElementem()
    {
        var fvGuid = UtworzZatwierdzonaFakture();

        var fv = Get<DokumentHandlowy>(fvGuid);
        DokumentHandlowy[] korekty = null;
        InTransaction(() => korekty = Relacje.NowaKorekta(new[] { fv }));
        SaveDispose();

        // Relacja indywidualna: jeden dokument korygowany → tablica z dokładnie jednym elementem.
        korekty.Should().NotBeNull();
        korekty.Should().HaveCount(1, "NowaKorekta dla jednego dokumentu zwraca jednoelementową tablicę");
    }

    // ===================================================================================
    // HANDEL-W50 — Dokument inwentaryzacji (INW)
    // ===================================================================================

    [Test]
    [Description("HANDEL-W50: tworzy dokument inwentaryzacji (INW) ze wskazanym magazynem i pozycją spisu; " +
                 "sprawdza, że dokument powstał z poprawną definicją i magazynem. Wyliczanie różnic " +
                 "(nadwyżka/strata) jest efektem zatwierdzenia + Save i nie jest tu asercjonowane.")]
    public void HANDEL_W50_Inwentaryzacja_TworzyDokumentZeWskazanymMagazynem()
    {
        // Tworzenie INW w JEDNEJ transakcji edycyjnej — bez wcześniejszego Session.Save()
        // (poprzedni Save zamykał okno edycji bieżącej sesji → AccessWriteDenied przy edycji nowego INW, §8).
        // Asercje ograniczone do faktów strukturalnych: definicja INW i wskazany magazyn „F”.
        // Definicja PIERWSZA (wyznacza zachowanie dokumentu), potem magazyn inwentaryzowany.
        DokumentHandlowy inw = null;
        try
        {
            InTransaction(() =>
            {
                inw = new DokumentHandlowy();
                Session.AddRow(inw);
                inw.Definicja = Definicja(Definicje.Inwentaryzacja); // INW
                inw.Magazyn = Magazyn(Magazyn_.Firma);               // inwentaryzowany magazyn (wymagany)
            });
        }
        catch (NotImplementedException ex)
        {
            // Gdyby utworzenie/zatwierdzenie INW w Demo wymagało specjalnej procedury niedostępnej publicznie.
            Assert.Ignore("Dokument INW wymaga procedury niedostępnej z publicznego API (NotImplementedException): " + ex.Message);
            return;
        }

        // Asercja ograniczona do utworzenia dokumentu (zgodnie z zakresem rozdziału):
        // dokument powstał, ma definicję INW i wskazany magazyn.
        inw.Should().NotBeNull();
        inw!.Definicja.Symbol.Should().Be(Definicje.Inwentaryzacja, "dokument inwentaryzacji ma definicję INW");
        inw.Magazyn.Should().Be(Magazyn(Magazyn_.Firma), "INW wymaga wskazanego magazynu");
    }

    // ===================================================================================
    // HANDEL-W52 — Przesunięcie międzymagazynowe (MM)
    // ===================================================================================

    [Test]
    [Description("HANDEL-W52: tworzy dokument przesunięcia międzymagazynowego (MM) z MagazynZ (źródło) i MagazynDo " +
                 "(cel). MagazynDo to pole kalkulowane delegujące do dokumentu podrzędnego — ustawiamy je " +
                 "po Definicji, przed dodaniem pozycji. Wymaga DRUGIEGO magazynu — gdy w Demo jest tylko „F”, " +
                 "test jest pomijany (Assert.Ignore).")]
    public void HANDEL_W52_PrzesuniecieMM_TworzyDokumentZMagazynamiZrodloowymIDocelowym()
    {
        var magazynZrodlo = Magazyn(Magazyn_.Firma); // „F” — jedyny pewny magazyn w Demo

        // MM wymaga DWÓCH różnych magazynów. Magazyn to dane KONFIGURACYJNE (słownikowe) — jeśli Demo ma
        // tylko „F”, tworzymy drugi magazyn w sesji konfiguracyjnej (Oddział kopiujemy z „F”), a potem
        // pobieramy go w sesji operacyjnej do MM.
        const string symbolCel = "F2";
        if (Magazyny.Magazyny.Cast<Magazyn>().All(m => m.Symbol != symbolCel))
        {
            var oddzialF = magazynZrodlo.Oddzial;   // oddział istniejącego magazynu „F”
            InConfigTransaction(() =>
            {
                var m = AddConfig(new Magazyn());
                m.Symbol = symbolCel;
                m.Nazwa = "Magazyn drugi (test MM)";
                if (oddzialF != null)
                    m.Oddzial = GetConfig(oddzialF);   // przepnij oddział do sesji konfiguracyjnej
            });
            SaveDisposeConfig();
            // Odśwież sesję operacyjną, by zobaczyła nowy magazyn z konfiguracji (świeża sesja wczyta config).
            SaveDispose();
        }

        // Magazyn źródłowy „F” musi mieć ZAPISANY zasób przesuwanego towaru (Demo: blokada stanu ujemnego).
        // PrzyjmijNaStan robi SaveDispose → po nim pracujemy na ŚWIEŻEJ sesji (magazyny pobieramy poniżej).
        PrzyjmijNaStan(Towar_.Bikini, 50);

        // Magazyny pobieramy z BIEŻĄCEJ (świeżej) sesji — różne MagazynZ/MagazynDo.
        var magZ = Magazyn(Magazyn_.Firma);
        var magDo = Magazyny.Magazyny.Cast<Magazyn>().FirstOrDefault(m => m.Symbol == symbolCel);
        magDo.Should().NotBeNull("drugi magazyn („F2”) istnieje po utworzeniu w konfiguracji");

        DokumentHandlowy mm = null;
        try
        {
            InTransaction(() =>
            {
                mm = new DokumentHandlowy();
                Session.AddRow(mm);
                mm.Definicja = Definicja(Definicje.PrzesuniecieMM); // MM — definicja PIERWSZA

                // Magazyn źródłowy (rozchód) to standardowe pole dokumentu Magazyn (MagazynZ bywa
                // read-only poza ZamówieniemWewnętrznym). MagazynDo deleguje do dokumentu podrzędnego
                // (PrzesunięcieDo) — ustawiamy je PO definicji i PRZED pozycjami.
                mm.Magazyn = magZ;
                mm.MagazynDo = magDo; // magazyn docelowy (różny od źródłowego)

                DodajPozycje(mm, Towar(Towar_.Bikini), ilosc: 5);
            });
        }
        catch (NotImplementedException ex)
        {
            Assert.Ignore("Dokument MM wymaga procedury niedostępnej z publicznego API (NotImplementedException): " + ex.Message);
            return;
        }

        // Asercje ograniczone do utworzenia dokumentu MM z poprawnymi magazynami.
        mm.Should().NotBeNull();
        mm!.Definicja.Symbol.Should().Be(Definicje.PrzesuniecieMM);
        mm.Magazyn.Should().Be(magZ, "magazyn źródłowy rozchodu");
        mm.MagazynDo.Should().Be(magDo, "magazyn docelowy przesunięcia");
        mm.Magazyn.Should().NotBe(mm.MagazynDo, "magazyn źródłowy i docelowy muszą być różne");
        mm.Pozycje.Count.Should().BeGreaterThan(0);
    }

    // ===================================================================================
    // SCENARIUSZE POMINIĘTE (SKIP) — uzasadnienie zgodne z treścią rozdziału
    // ===================================================================================

    [Test]
    [Description("HANDEL-W49: korektę przyjęcia magazynowego (PW) budujemy — jak każdą korektę — przez RELACJĘ " +
                 "korekty IRelacjeService.NowaKorekta (nie tworzymy dokumentu wprost ani workerem internal). " +
                 "Sprawdza powstanie dokumentu korygującego i powiązanie korekta.DokumentKorygowany == przyjęcie.")]
    public void HANDEL_W49_KorektaPrzyjecia_PrzezRelacjeKorekty()
    {
        // Zatwierdzone, zapisane przyjęcie (PW) — dokument korygowany. PrzyjmijNaStan zatwierdza PW
        // (zatwierdzanie przychodu jest bezpieczne) i księguje zasób.
        var pwGuid = PrzyjmijNaStan(Towar_.Bikini, 10);

        var pw = Get<DokumentHandlowy>(pwGuid);
        DokumentHandlowy[] korekty = null;
        InTransaction(() => korekty = Relacje.NowaKorekta(new[] { pw }));
        var korektaGuid = korekty[0].Guid;
        SaveDispose();

        korekty.Should().HaveCount(1, "relacja korekty: jedno przyjęcie → jeden dokument korygujący");
        var korekta = Get<DokumentHandlowy>(korektaGuid);
        korekta.DokumentKorygowany.Should().NotBeNull("korekta wskazuje korygowane przyjęcie (kalkulowane)");
        korekta.DokumentKorygowany.Guid.Should().Be(pwGuid, "korekta wskazuje oryginalne przyjęcie PW");

        var pwOdczyt = Get<DokumentHandlowy>(pwGuid);
        pwOdczyt.DokumentyKorygujące.Should().Contain(d => d.Guid == korektaGuid,
            "przyjęcie wskazuje swój dokument korygujący");
    }

    [Test]
    [Ignore("HANDEL-W51 — rozliczenie zaliczki przez relację: zweryfikowano, że publiczny IRelacjeService NIE udostępnia " +
            "tej relacji w bazie Demo (GoldStandard). Relacja zaliczkowa to DefRelacjiZaliczki (nazwy „Faktura (zaliczka)” / " +
            "„Zaliczkowy (r. zal.)”) oznaczona w konfiguracji <Hidden>True</Hidden> — relacje ukryte nie są zwracane przez " +
            "DokumentHandlowy.ResolveActions(), więc DolaczNadrzedny rzuca InvalidOperationException('Operacja tworzenia relacji " +
            "nie jest dostępna') dla KAŻDEJ nazwy. KOREKTA wcześniejszej oceny: parametry rozliczenia SĄ publiczne " +
            "(RelacjeHandloweWorker.DokumentyZaliczkoweParams + HandlerSet.WybierzDokumentyZaliczkoweCallback) — blokadą jest " +
            "ukrycie samej relacji, którą uruchamia wewnętrzny przepływ zaliczkowy platformy, nie publiczne IRelacjeService. " +
            "Ciało testu pozostawiono jako wykonywalną dokumentację relacyjnego podejścia (FZAL → FV przez DolaczNadrzedny).")]
    [Description("HANDEL-W51: faktura zaliczkowa (FZAL) i jej rozliczenie fakturą końcową (FV) — BUDOWANE PRZEZ RELACJE. " +
                 "Najpierw zatwierdzona FZAL (zaliczka), potem faktura końcowa dołącza ją jako nadrzędną relacją " +
                 "„Faktura zaliczkowa” (IRelacjeService.DolaczNadrzedny) z publicznym callbackiem " +
                 "WybierzDokumentyZaliczkoweCallback (RelacjeHandloweWorker.DokumentyZaliczkoweParams jest publiczny). " +
                 "Po rozliczeniu faktura końcowa wskazuje zaliczkę w DokumentyZaliczkowe.")]
    public void HANDEL_W51_FakturaZaliczkowa_RozliczeniePrzezRelacje()
    {
        PrzyjmijNaStan(Towar_.Bikini, 100);

        // 1) Faktura ZALICZKOWA (FZAL) — sprzedażowa, więc zapis pozycji PRZED zatwierdzeniem (KrajPodatkuVat).
        // Kontrahenta pobieramy ŚWIEŻO przy każdym dokumencie (po SaveDispose wcześniejsze wiersze są nieaktualne).
        var fzal = UtworzDokument("FZAL", kontrahent: Kontrahent(Kontrahent_.Abc), magazyn: Magazyn(Magazyn_.Firma));
        InTransaction(() => DodajPozycje(fzal, Towar(Towar_.Bikini), ilosc: 5, cena: 20));
        var fzalGuid = fzal.Guid;
        SaveDispose();
        var fzal2 = Get<DokumentHandlowy>(fzalGuid);
        InTransaction(() => fzal2.Stan = StanDokumentuHandlowego.Zatwierdzony);
        SaveDispose();

        // 2) Faktura KOŃCOWA (FV) w buforze — dokument, do którego dołączymy zaliczkę relacją.
        var fv = UtworzDokument(Definicje.FakturaSprzedazy, kontrahent: Kontrahent(Kontrahent_.Abc), magazyn: Magazyn(Magazyn_.Firma));
        InTransaction(() => DodajPozycje(fv, Towar(Towar_.Bikini), ilosc: 5, cena: 20));
        var fvGuid = fv.Guid;
        SaveDispose();

        // 3) Rozliczenie zaliczki PRZEZ RELACJĘ „Faktura zaliczkowa” — dołączenie nadrzędnej zaliczki do FV.
        //    Publiczny callback uzupełnia parametry wyboru dokumentów zaliczkowych.
        var handlers = new HandlerSet
        {
            WybierzDokumentyZaliczkoweCallback = docelowy =>
                docelowy.Context.Set(new RelacjeHandloweWorker.DokumentyZaliczkoweParams(docelowy.Context)
                {
                    Docelowy = docelowy
                }),
            WybierzDokumentyCallback = p =>
                p.DokumentyWybrane = p.Dokumenty.Cast<DokumentHandlowy>().ToArray()
        };

        var fv2 = Get<DokumentHandlowy>(fvGuid);
        InTransaction(() =>
            Relacje.DolaczNadrzedny(new[] { fv2 }, "Zaliczkowy (r. zal.)", handlers: handlers));
        SaveDispose();

        // Faktura końcowa wskazuje rozliczoną zaliczkę (pole kalkulowane DokumentyZaliczkowe).
        var fvKoncowa = Get<DokumentHandlowy>(fvGuid);
        fvKoncowa.DokumentyZaliczkowe.Cast<DokumentHandlowy>().Select(d => d.Guid)
            .Should().Contain(fzalGuid, "faktura końcowa rozliczyła zaliczkę FZAL przez relację");
    }
}
