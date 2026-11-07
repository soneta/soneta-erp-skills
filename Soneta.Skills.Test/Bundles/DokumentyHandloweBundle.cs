#nullable enable

using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Soneta.Business;
using Soneta.Business.App;
using Soneta.Business.Bundles;
using Soneta.Business.Licence;
using Soneta.CRM;
using Soneta.Handel;
using Soneta.Magazyny;
using Soneta.Skills.Test.Bundles;
using Soneta.Tools;
using Soneta.Towary;
using Soneta.Types;

[assembly: Bundle<DokumentyHandloweBundle>(
    Caption = "Dokumenty handlowe (przykład skilla)",
    Contexts = [LicencjeModułu.HAN])]

namespace Soneta.Skills.Test.Bundles;

internal sealed class DokumentyHandloweBundle(Login login, IBundleSessionContext bundleContext) {

    private const int MaxScan = 500;

    private static readonly JsonSerializerOptions ArgOptions = new() { PropertyNameCaseInsensitive = true };

    [BundleSetting, Caption("Zapis dokumentów")]
    [Description("Agent może tworzyć i zmieniać dokumenty w buforze. Bez tego parametru metoda zapisu jest niedostępna.")]
    public bool AllowsWrite { get; set; }

    [BundleSetting, Caption("Limit wartości brutto")]
    [Description("Najwyższa wartość brutto dokumentu zapisywanego przez agenta. 0 oznacza brak limitu.")]
    public decimal MaxBrutto { get; set; } = 10_000m;

    public bool IsVisible_save_dokument_handlowy() => AllowsWrite;

    [BundleMethod(Title = "Wyszukaj dokumenty handlowe", ReadOnlyHint = true, IdempotentHint = true)]
    [Description("""
        Wyszukuje dokumenty handlowe. Zwraca `documentsCsv` z nagłówkiem
        `id|numer|data|kontrahent|brutto|stan` oraz `count` i `truncated`.
        `id` przekaż do `get_dokument_handlowy` albo `save_dokument_handlowy`.
        """)]
    public SearchResponse search_dokumenty_handlowe(
        [Description("Symbol definicji dokumentu, np. `FV`, `ZO`. Puste = wszystkie.")] string? definicja = null,
        [Description("Kod kontrahenta albo jego `id`.")] string? kontrahent = null,
        [Description("Okres daty wystawienia `rrrr-mm-dd..rrrr-mm-dd`. Puste = bez ograniczenia.")] string? okres = null,
        [Description("Maksymalna liczba dokumentów (domyślnie 15; 0 = bez ograniczenia).")] int limit = 15) {

        login.CurrentRole.BundleOperationalData.CheckReadOnly();

        using var session = login.CreateSession(readOnly: true, config: false, nameof(DokumentyHandloweBundle));
        var hm = session.GetHandel();

        var cond = RowCondition.Empty;
        if (!okres.IsNullOrEmpty()) {
            var zakres = FromTo.Parse(okres, CultureInfo.InvariantCulture);
            cond &= RowCondition.FromExpression<DokumentHandlowy>(d => d.Data >= zakres.From && d.Data <= zakres.To);
        }
        if (!definicja.IsNullOrEmpty()) {
            var def = FindDefinicja(hm, definicja);
            cond &= RowCondition.FromExpression<DokumentHandlowy>(d => d.Definicja == def);
        }
        if (!kontrahent.IsNullOrEmpty()) {
            var k = FindKontrahent(session, kontrahent);
            cond &= RowCondition.FromExpression<DokumentHandlowy>(d => d.Kontrahent == k);
        }

        var rows = hm.DokHandlowe.WgDaty[cond].Cast<DokumentHandlowy>().Take(MaxScan + 1).ToArray();
        var (items, count, truncated) = BundleLimit.Apply(rows, limit);

        return new SearchResponse(items.Select(ToInfo).ToArray(), count, truncated) {
            Warning = rows.Length > MaxScan ? "Ponad {0} dokumentów — zawęź okres lub kontrahenta.".TranslateFormat(MaxScan) : null,
        };
    }

    [BundleMethod(Title = "Odczytaj dokument handlowy", ReadOnlyHint = true, IdempotentHint = true)]
    [Description("""
        Zwraca nagłówek dokumentu i jego pozycje (`pozycjeCsv` z nagłówkiem
        `lp|towar|nazwa|ilosc|jednostka|cena|netto`).
        """)]
    public DokumentResponse get_dokument_handlowy(
        [Required, Description("`id` dokumentu z `search_dokumenty_handlowe`.")] string id) {

        login.CurrentRole.BundleOperationalData.CheckReadOnly();

        using var session = login.CreateSession(readOnly: true, config: false, nameof(DokumentyHandloweBundle));
        return ToResponse(FindDokument(session, id));
    }

    [BundleMethod(Title = "Zapisz dokument handlowy w buforze", DestructiveHint = true)]
    [Description("""
        Tworzy dokument handlowy (bez `id`) albo zmienia dokument w buforze (z `id`).
        Dokument zawsze zostaje w buforze — zatwierdza go operator.
        `pozycje` to tablica obiektów, np.
        `[{"towar":"BIKINI","ilosc":2,"cena":49.90},{"lp":1,"usun":true}]`:
        - `lp` — pomiń dla nowej pozycji; podaj, by zmienić albo usunąć (`usun: true`) istniejącą,
        - `towar` — kod towaru albo jego `id`; wymagany w nowej pozycji,
        - `ilosc`, `cena` — opcjonalne; brak = wartości z karty towaru.
        Dokument o wartości brutto ponad limit operatora nie zostanie zapisany.
        `testing: true` sprawdza dane i wylicza wartości bez zapisu.
        Zwraca dokument w postaci jak `get_dokument_handlowy` oraz `saved`.
        """)]
    public SaveResponse save_dokument_handlowy(
        [Description("`id` dokumentu do zmiany. Puste = nowy dokument.")] string? id = null,
        [Description("Symbol definicji nowego dokumentu, np. `FV`, `ZO`.")] string? definicja = null,
        [Description("Kod kontrahenta albo jego `id`.")] string? kontrahent = null,
        [Description("Pozycje do dodania, zmiany lub usunięcia — format w opisie metody.")] JsonElement[]? pozycje = null,
        [Description("`true` = próba bez zapisu.")] bool testing = false) {

        login.CurrentRole.BundleOperationalData.CheckGranted();
        var args = (pozycje ?? []).Select(ReadPozycja).ToArray();

        using var session = login.CreateSession(readOnly: false, config: false, nameof(DokumentyHandloweBundle));
        DokumentHandlowy dok;

        using (var t = session.Logout(editMode: true)) {
            dok = id.IsNullOrEmpty() ? CreateDokument(session, definicja) : FindDokument(session, id);
            if (!dok.Bufor)
                throw new InvalidOperationException(
                    "Dokument {0} nie jest w buforze — agent zmienia tylko dokumenty w buforze.".TranslateFormat(dok.Numer.NumerPelny));

            if (!kontrahent.IsNullOrEmpty()) dok.Kontrahent = FindKontrahent(session, kontrahent);
            foreach (var arg in args) ApplyPozycja(session, dok, arg);

            if (MaxBrutto > 0 && dok.Suma.Brutto > MaxBrutto)
                throw new InvalidOperationException(
                    "Wartość brutto {0} przekracza limit {1} ustawiony przez operatora. Dokument nie został zapisany."
                        .TranslateFormat(dok.Suma.Brutto, MaxBrutto));

            if (testing) return new SaveResponse(ToResponse(dok, withId: false), Saved: false);

            session.ChangeInfos.Add(dok, ChangeInfoType.Bundle,
                "Agent AI {0}: save_dokument_handlowy".TranslateFormat(bundleContext.AgentGuid),
                JsonSerializer.Serialize(new { id, definicja, kontrahent, pozycje }));
            t.Commit();
        }
        session.Save();
        return new SaveResponse(ToResponse(dok), Saved: true);
    }

    private static PozycjaArg ReadPozycja(JsonElement element) {
        if (element.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Pozycja musi być obiektem JSON, np. {\"towar\":\"BIKINI\",\"ilosc\":2}.".Translate());

        var arg = element.Deserialize<PozycjaArg>(ArgOptions)!;
        if (arg.Lp == null && arg.Towar.IsNullOrEmpty())
            throw new ArgumentException("Nowa pozycja wymaga `towar`.".Translate());
        return arg;
    }

    private void ApplyPozycja(Session session, DokumentHandlowy dok, PozycjaArg arg) {
        PozycjaDokHandlowego poz;
        if (arg.Lp is int lp) {
            poz = dok.Pozycje.FirstOrDefault(p => p.Lp == lp)
                  ?? throw new ArgumentException("Dokument nie ma pozycji lp={0}.".TranslateFormat(lp));
            if (arg.Usun) {
                poz.Delete();
                return;
            }
            if (!arg.Towar.IsNullOrEmpty()) poz.Towar = FindTowar(session, arg.Towar);
        }
        else {
            poz = new PozycjaDokHandlowego(dok);
            session.AddRow(poz);
            poz.Towar = FindTowar(session, arg.Towar!);
        }

        if (arg.Ilosc is double ilosc) poz.Ilosc = new Quantity(ilosc, poz.Ilosc.Symbol);
        if (arg.Cena is decimal cena) poz.Cena = new DoubleCy(cena, poz.Cena.Symbol);
    }

    private static DokumentHandlowy CreateDokument(Session session, string? definicja) {
        if (definicja.IsNullOrEmpty())
            throw new ArgumentException("Nowy dokument wymaga `definicja`.".Translate());

        var dok = new DokumentHandlowy();
        session.AddRow(dok);
        dok.Definicja = FindDefinicja(session.GetHandel(), definicja);
        dok.Magazyn ??= session.GetMagazyny().Magazyny.Firma;
        return dok;
    }

    private static DefDokHandlowego FindDefinicja(HandelModule hm, string symbol) =>
        hm.DefDokHandlowych.WgSymbolu[symbol]
        ?? throw new ArgumentException("Nie ma definicji dokumentu '{0}'.".TranslateFormat(symbol));

    private DokumentHandlowy FindDokument(Session session, string id) {
        var (table, rowId) = login.ObjectIds().ResolveObject(id);
        return session.Tables[table].Rows.FindByID(rowId, false) as DokumentHandlowy
               ?? throw new ArgumentException("`{0}` nie wskazuje dokumentu handlowego.".TranslateFormat(id));
    }

    private Kontrahent FindKontrahent(Session session, string value) =>
        FindRow(session, value, session.GetCRM().Kontrahenci.WgKodu[value]);

    private Towar FindTowar(Session session, string value) =>
        FindRow(session, value, session.GetTowary().Towary.WgKodu[value]);

    private T FindRow<T>(Session session, string value, T? byCode) where T : Row {
        if (login.ObjectIds().TryResolve(value, out var table, out int rowId))
            return session.Tables[table!].Rows.FindByID(rowId, false) as T
                   ?? throw new ArgumentException("`{0}` nie wskazuje obiektu typu {1}.".TranslateFormat(value, typeof(T).Name));
        return byCode ?? throw new ArgumentException("Nie znaleziono '{0}'.".TranslateFormat(value));
    }

    private DokumentInfo ToInfo(DokumentHandlowy d) => new(
        login.ObjectIds().Mark(d)!, d.Numer.NumerPelny, d.Data.ToString(null, CultureInfo.InvariantCulture),
        ProtectedState.ReadText(() => d.Kontrahent?.Nazwa), d.Suma.Brutto, d.Stan.ToString());

    private DokumentResponse ToResponse(DokumentHandlowy d, bool withId = true) => new(
        withId ? login.ObjectIds().Mark(d) : null,
        d.Numer.NumerPelny, d.Definicja.Symbol, d.Data.ToString(null, CultureInfo.InvariantCulture),
        ProtectedState.ReadText(() => d.Kontrahent?.Nazwa), d.Stan.ToString(), d.Suma.Netto, d.Suma.Brutto,
        d.Pozycje.Select(p => new PozycjaInfo(
            p.Lp, p.Towar.Kod, p.Towar.Nazwa, p.Ilosc.Value, p.Ilosc.Symbol, p.Cena.Value, p.Suma.Netto)).ToArray());

    private sealed record PozycjaArg(int? Lp, string? Towar, double? Ilosc, decimal? Cena, bool Usun = false);
}

[BundleCsv(nameof(Documents))]
internal sealed record SearchResponse(DokumentInfo[] Documents, int Count, bool Truncated) {
    public string? Warning { get; init; }
}

internal sealed record DokumentInfo(string Id, string Numer, string Data, string? Kontrahent, decimal Brutto, string Stan);

[BundleCsv(nameof(Pozycje))]
internal sealed record DokumentResponse(
    string? Id, string Numer, string Definicja, string Data, string? Kontrahent, string Stan,
    decimal Netto, decimal Brutto, PozycjaInfo[] Pozycje);

internal sealed record PozycjaInfo(int Lp, string Towar, string Nazwa, double Ilosc, string Jednostka, double Cena, decimal Netto);

internal sealed record SaveResponse(DokumentResponse Dokument, bool Saved);
