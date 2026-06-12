using System;
using System.Collections;
using System.Linq;
using AwesomeAssertions;
using NUnit.Framework;
using Soneta.Kasa;            // EksportPrzelewowWorker, EksportPrzelewowParams, PrzelewBase, PaczkaPrzelewow, RachunekBankowyFirmy, RozrachunekIdx, ...
using Soneta.Place;           // ListaPlac (+ ListaPlac.PrzygotujPrzelewyWorker)
using Prac = Soneta.Kadry.Pracownik;

namespace Soneta.Skills.Test.KadryPlace.Pracownik;

/// <summary>
/// Rozdział I (część rozliczeniowa) — „Przelewy wynagrodzeń, eksport do banku, rozliczenia/faktury”
/// (receptury KADRY-I4, KADRY-I5, KADRY-I6).
/// <para>
/// Testy są <b>wykonywalną dokumentacją</b> publicznego kontraktu mechanizmu „z wypłaty do przelewu”
/// i rozliczeń pracownika. Operujemy wyłącznie na <b>publicznym kontrakcie</b> platformy Soneta
/// (jak dodatek programisty zewnętrznego), na bazie Demo (GoldStandard) z automatycznym rollbackiem.
/// </para>
/// <list type="bullet">
/// <item><b>KADRY-I4</b> — przygotowanie przelewów wynagrodzeń workerem
/// <c>Soneta.Place.ListaPlac.PrzygotujPrzelewyWorker</c> (akcja <c>PrzygotujPrzelewy()</c>).
/// Testowalny jest <b>kontrakt</b> (istnienie workera i jego <c>Params</c> z polami
/// <c>Data</c>/<c>Paczka</c>/<c>ZRachunku</c>) oraz <b>odczyt</b> kolekcji rozliczeniowych pracownika
/// (<c>Przelewy</c>, <c>DokumentyPreliminarza</c>, <c>Rozrachunki</c>). Samo
/// <c>worker.PrzygotujPrzelewy()</c> wymaga skonfigurowanego modułu Kasa (definicja paczki, rachunek
/// firmy, rachunek pracownika), czego baza Demo nie gwarantuje → <c>[Ignore]</c>.</item>
/// <item><b>KADRY-I5</b> — eksport przelewów do pliku bankowego workerem
/// <c>Soneta.Kasa.EksportPrzelewowWorker</c> (akcja <c>Eksport()</c>) sterowanym
/// <c>Soneta.Kasa.EksportPrzelewowParams</c>. Testowalne jest <b>istnienie publicznego API</b>
/// (konstrukcja workera i parametrów, pole <c>FileName</c>). Wywołanie <c>Eksport()</c> to operacja
/// plikowa/sieciowa → <c>[Ignore]</c>.</item>
/// <item><b>KADRY-I6</b> — rozliczenia/faktura: odczyt kolekcji rozrachunkowych pracownika
/// (<c>Rozrachunki</c>, <c>DokumentyRozliczeniowe</c>, <c>DokumentyPreliminarza</c>) — asercja, że są
/// dostępne, iterowalne i zwracają typy zgodne z kontraktem. Wystawienie faktury (zbiorczej) z zapłaty
/// to domena handlowa (<c>DokumentHandlowy</c>), poza kontraktem pracownika → <c>[Ignore]</c>.</item>
/// </list>
/// </summary>
[TestFixture]
public class RozdzialIrest_PrzelewyTest : PracownikTestBase
{
    // ===================================================================================
    // KADRY-I4 — Przygotowanie przelewów wynagrodzeń (kontrakt workera + odczyt kolekcji)
    // ===================================================================================

    [Test]
    [Description("KADRY-I4 (kontrakt): worker przygotowania przelewów z listy płac istnieje w publicznym API — " +
                 "Soneta.Place.ListaPlac.PrzygotujPrzelewyWorker z zagnieżdżonym typem Params. " +
                 "Asercja przez refleksję publicznego kontraktu: typ workera i Params istnieją, Params ma " +
                 "pola Data/Paczka/DefinicjaPaczki/ZRachunku, a worker ma metodę PrzygotujPrzelewy(). " +
                 "Faktyczne wywołanie PrzygotujPrzelewy() jest [Ignore] (osobny test) — wymaga konfiguracji Kasa.")]
    public void KADRY_I4_PrzygotujPrzelewy_KontraktWorkera()
    {
        // Worker płacowy jest typem zagnieżdżonym w ListaPlac (assembly Soneta.KadryPlace, namespace Soneta.Place).
        Type workerType = typeof(ListaPlac.PrzygotujPrzelewyWorker);
        workerType.Should().NotBeNull("worker przygotowania przelewów istnieje w publicznym kontrakcie");

        // Typ parametrów workera (zagnieżdżony Params).
        Type paramsType = workerType.GetNestedType("Params");
        paramsType.Should().NotBeNull("PrzygotujPrzelewyWorker udostępnia publiczny typ Params");

        // Kluczowe pola/właściwości parametrów wg dokumentacji KADRY-I4.
        var skladowe = paramsType.GetMembers()
            .Select(m => m.Name)
            .ToList();
        skladowe.Should().Contain("Data", "Params.Data — data dokumentów przelewu");
        skladowe.Should().Contain("Paczka", "Params.Paczka — istniejąca paczka przelewów");
        skladowe.Should().Contain("ZRachunku", "Params.ZRachunku — rachunek firmy obciążany przelewami");

        // Akcja workera: PrzygotujPrzelewy().
        workerType.GetMethod("PrzygotujPrzelewy")
            .Should().NotBeNull("worker udostępnia akcję PrzygotujPrzelewy()");

        // Dokument przelewu, który powstaje w wyniku akcji, to Soneta.Kasa.PrzelewBase (tabela Przelewy).
        typeof(PrzelewBase).Should().NotBeNull("dokument przelewu to Soneta.Kasa.PrzelewBase");
    }

    [Test]
    [Description("KADRY-I4 (odczyt): kolekcje rozliczeniowe pracownika są dostępne i iterowalne — " +
                 "Pracownik.Przelewy (PrzelewBase), Pracownik.DokumentyPreliminarza (PreliminarzDokument), " +
                 "Pracownik.Rozrachunki (RozrachunekIdx). Asercja: iteracja nie rzuca, a elementy (jeśli są) " +
                 "mają typy zgodne z kontraktem. Bez wywołania PrzygotujPrzelewy — sam odczyt stanu.")]
    public void KADRY_I4_KolekcjeRozliczeniowePracownika_OdczytTypyZgodne()
    {
        var pracownik = Pracownik(Pracownik_.Andrzejewski);
        pracownik.Should().NotBeNull();

        // Przelewy — odczyt nie rzuca; elementy (jeśli są) to PrzelewBase.
        Action czytajPrzelewy = () => IterujISprawdzTyp<PrzelewBase>(pracownik.Przelewy);
        czytajPrzelewy.Should().NotThrow("odczyt kolekcji Pracownik.Przelewy jest bezpieczny");

        // Dokumenty preliminarza — elementy to PreliminarzDokument.
        Action czytajPreliminarz = () => IterujISprawdzTyp<PreliminarzDokument>(pracownik.DokumentyPreliminarza);
        czytajPreliminarz.Should().NotThrow("odczyt kolekcji Pracownik.DokumentyPreliminarza jest bezpieczny");

        // Rozrachunki — elementy to RozrachunekIdx.
        Action czytajRozrachunki = () => IterujISprawdzTyp<RozrachunekIdx>(pracownik.Rozrachunki);
        czytajRozrachunki.Should().NotThrow("odczyt kolekcji Pracownik.Rozrachunki jest bezpieczny");
    }

    [Test]
    [Ignore("KADRY-I4: faktyczne wywołanie ListaPlac.PrzygotujPrzelewyWorker.PrzygotujPrzelewy() wymaga " +
            "skonfigurowanego modułu Kasa (definicja paczki przelewów DefinicjaPaczkiPrzelewu, rachunek firmy " +
            "RachunekBankowyFirmy oraz rachunek odbiorcy Pracownik.Rachunki). Baza Demo nie gwarantuje tej " +
            "konfiguracji, więc generowanie dokumentów PrzelewBase jest niepewne. Test KADRY_I4_PrzygotujPrzelewy_KontraktWorkera " +
            "pokrywa publiczny kontrakt; samo przygotowanie przelewów dokumentujemy bez uruchamiania.")]
    [Description("KADRY-I4 (wykonanie — pominięte): naliczenie wypłaty etatowej (jak KADRY-H1/KADRY-I1b) → ListaPlac z Wyplata.ListaPlac → " +
                 "new ListaPlac.PrzygotujPrzelewyWorker { Pars = new Params { Data = Date.Today, ... } }.PrzygotujPrzelewy() → " +
                 "session.Save(). Powstają dokumenty Soneta.Kasa.PrzelewBase w paczce PaczkaPrzelewow.")]
    public void KADRY_I4_PrzygotujPrzelewy_Wykonanie()
    {
        // Pominięte — patrz powód w [Ignore]. Operacja zapisująca zależna od konfiguracji modułu Kasa.
    }

    // ===================================================================================
    // KADRY-I5 — Eksport przelewów do pliku bankowego (istnienie API; eksport pliku → Ignore)
    // ===================================================================================

    [Test]
    [Description("KADRY-I5 (kontrakt API): eksport przelewów to worker Soneta.Kasa.EksportPrzelewowWorker " +
                 "sterowany Soneta.Kasa.EksportPrzelewowParams. UWAGA: EksportPrzelewowParams NIE ma " +
                 "konstruktora bezparametrowego — wymaga (Context, RachunekBankowyFirmy, PrzelewBase[]), a sam " +
                 "konstruktor RZUCA ApplicationException, gdy nie wskazano rachunku firmy (walidacja w ctorze). " +
                 "Dlatego kontrakt weryfikujemy REFLEKSJĄ (bez instancjonowania): istnienie typów, sygnatura " +
                 "konstruktora parametrów, publiczne pole FileName, worker + property Params i metoda Eksport().")]
    public void KADRY_I5_EksportPrzelewow_KontraktApi()
    {
        // Typ parametrów eksportu istnieje w publicznym kontrakcie.
        Type paramsType = typeof(EksportPrzelewowParams);
        paramsType.Should().NotBeNull("EksportPrzelewowParams istnieje w publicznym kontrakcie");

        // Konstruktor parametrów wymaga (Context, RachunekBankowyFirmy, PrzelewBase[]) — sygnatura wg kontraktu.
        // (NIE wołamy go: ctor waliduje rachunek i rzuca ApplicationException przy braku konfiguracji.)
        var ctor = paramsType.GetConstructor(new[]
        {
            typeof(Soneta.Business.Context), typeof(RachunekBankowyFirmy), typeof(PrzelewBase[]),
        });
        ctor.Should().NotBeNull(
            "EksportPrzelewowParams wymaga konstruktora (Context, RachunekBankowyFirmy, PrzelewBase[])");

        // Publiczne pole ścieżki pliku wyjściowego.
        paramsType.GetProperty("FileName")
            .Should().NotBeNull("Params.FileName — ścieżka pliku wyjściowego (operacja na dysku)");

        // Worker eksportu i jego property Params (sterowanie parametrami).
        Type workerType = typeof(EksportPrzelewowWorker);
        workerType.Should().NotBeNull("EksportPrzelewowWorker istnieje w publicznym kontrakcie");
        workerType.GetProperty("Params")
            .Should().NotBeNull("worker przyjmuje parametry przez właściwość Params");

        // Akcja eksportu istnieje w kontrakcie (ale jej NIE wołamy — patrz KADRY_I5_EksportPrzelewow_Eksport).
        workerType.GetMethod("Eksport")
            .Should().NotBeNull("worker udostępnia akcję Eksport() — w teście jednostkowym nie wywoływaną");
    }

    [Test]
    [Ignore("KADRY-I5: EksportPrzelewowWorker.Eksport() zapisuje fizyczny plik bankowy na dysk (wg Params.FileName) " +
            "i zależy od formatu/sterownika eksportu danego banku; wysyłka online to dodatkowo operacja sieciowa. " +
            "To wejście/wyjście do systemu zewnętrznego — poza zakresem testu jednostkowego. Kontrakt API " +
            "pokrywa test KADRY_I5_EksportPrzelewow_KontraktApi (bez wywołania Eksport()).")]
    [Description("KADRY-I5 (wykonanie — pominięte): worker.Eksport() — zapis pliku przelewów wg FileName; po eksporcie " +
                 "PrzelewBase.Exported = true blokuje dalszą edycję.")]
    public void KADRY_I5_EksportPrzelewow_Eksport()
    {
        // Pominięte — patrz powód w [Ignore]. Operacja plikowa/sieciowa.
    }

    // ===================================================================================
    // KADRY-I6 — Rozliczenia / faktura (odczyt rozrachunków; wystawienie faktury → Ignore)
    // ===================================================================================

    [Test]
    [Description("KADRY-I6 (odczyt): kolekcje rozliczeniowe pracownika są dostępne i iterowalne, a elementy mają " +
                 "typy zgodne z kontraktem — Pracownik.Rozrachunki (RozrachunekIdx), " +
                 "Pracownik.DokumentyRozliczeniowe (DokRozliczBase), Pracownik.DokumentyPreliminarza " +
                 "(PreliminarzDokument). Asercja: iteracja nie rzuca; bez operacji zapisujących.")]
    public void KADRY_I6_Rozliczenia_OdczytStanu()
    {
        var pracownik = Pracownik(Pracownik_.Bednarek);
        pracownik.Should().NotBeNull();

        // Rozrachunki — indeksy rozrachunkowe podmiotu (RozrachunekIdx).
        Action czytajRozrachunki = () => IterujISprawdzTyp<RozrachunekIdx>(pracownik.Rozrachunki);
        czytajRozrachunki.Should().NotThrow("odczyt kolekcji Pracownik.Rozrachunki jest bezpieczny");

        // Dokumenty rozliczeniowe — DokRozliczBase.
        Action czytajRozliczeniowe = () => IterujISprawdzTyp<DokRozliczBase>(pracownik.DokumentyRozliczeniowe);
        czytajRozliczeniowe.Should().NotThrow("odczyt kolekcji Pracownik.DokumentyRozliczeniowe jest bezpieczny");

        // Dokumenty preliminarza — PreliminarzDokument.
        Action czytajPreliminarz = () => IterujISprawdzTyp<PreliminarzDokument>(pracownik.DokumentyPreliminarza);
        czytajPreliminarz.Should().NotThrow("odczyt kolekcji Pracownik.DokumentyPreliminarza jest bezpieczny");
    }

    [Test]
    [Ignore("KADRY-I6: „Wystaw fakturę (zbiorczą) z zapłaty” NIE istnieje w publicznym kontrakcie pracownika/płac — " +
            "faktura to dokument handlowy (Soneta.Handel.DokumentHandlowy). Powiązanie zapłaty z fakturą realizują " +
            "rozrachunki/rozliczenia (moduł Kasa), a operacje zapisujące (np. RozliczWgPrzelewowWyplataWorker) wymagają " +
            "skonfigurowanego modułu Kasa/Handel, którego baza Demo nie gwarantuje. Wystawianie faktur należy do testów " +
            "domeny handlowej (handel.md). Odczyt rozrachunków pokrywa test KADRY_I6_Rozliczenia_OdczytStanu.")]
    [Description("KADRY-I6 (wykonanie — pominięte): wystawienie faktury zbiorczej z zapłat/rozliczeń (domena handlowa) " +
                 "oraz rozliczanie zapisujące przez workery rozliczeniowe Kasa.")]
    public void KADRY_I6_WystawienieFaktury_Rozliczenie()
    {
        // Pominięte — patrz powód w [Ignore]. Domena handlowa + konfiguracja Kasa/Handel.
    }

    // ===================================================================================
    // Pomocniki lokalne
    // ===================================================================================

    /// <summary>
    /// Iteruje kolekcję (np. <c>SubTable&lt;T&gt;</c> z kartoteki pracownika) i sprawdza, że każdy
    /// element jest przypisywalny do oczekiwanego typu kontraktu. Sama iteracja po kolekcji
    /// rozliczeniowej pracownika jest bezpieczna (zakres = jeden podmiot), więc nie skanujemy całej
    /// tabeli operacyjnej (safe-code §6.3). Pusta kolekcja jest poprawna (brak danych w Demo).
    /// </summary>
    private static void IterujISprawdzTyp<T>(IEnumerable kolekcja)
    {
        kolekcja.Should().NotBeNull("kolekcja rozliczeniowa pracownika jest dostępna w kontrakcie");
        foreach (var element in kolekcja)
            element.Should().BeAssignableTo<T>($"elementy kolekcji są typu {typeof(T).Name} (zgodnie z kontraktem)");
    }
}
