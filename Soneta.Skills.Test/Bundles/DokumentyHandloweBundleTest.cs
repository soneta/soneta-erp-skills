#nullable enable

using System;
using System.Linq;
using System.Text.Json;
using AwesomeAssertions;
using NUnit.Framework;
using Soneta.Business;
using Soneta.Business.App;
using Soneta.Business.Bundles;
using Soneta.Business.Db;
using Soneta.Handel;
using Soneta.Test;

namespace Soneta.Skills.Test.Bundles;

/// <summary>
/// Przykład pakietu funkcji MCP z rozdziału skilla „Pakiety funkcji (bundle) dla agentów AI”:
/// wyszukiwanie, odczyt i zapis dokumentu handlowego z pozycjami, limit kwoty z parametru pakietu,
/// próba bez zapisu i wpis w dzienniku zmian.
/// </summary>
[TestFixture]
public class DokumentyHandloweBundleTest : TestBase {

    private static readonly Guid TestAgentGuid = new("5a1e5a1e-0000-0000-0000-000000000001");

    private DokumentyHandloweBundle CreateBundle(bool allowsWrite = true, decimal maxBrutto = 10_000m) =>
        new(Session.Login, new FakeBundleContext(Session.Login)) { AllowsWrite = allowsWrite, MaxBrutto = maxBrutto };

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Test]
    [Description("Zapis jest ukryty bramką IsVisible_, dopóki operator nie włączy parametru AllowsWrite")]
    public void Bundle_Should_HideSave_When_WriteNotAllowed() {
        CreateBundle(allowsWrite: false).IsVisible_save_dokument_handlowy().Should().BeFalse();
        CreateBundle(allowsWrite: true).IsVisible_save_dokument_handlowy().Should().BeTrue();
    }

    [Test]
    [Description("Nowa FV z pozycjami powstaje w buforze; odczyt zwraca pozycje i identyfikator sesyjny")]
    public void Bundle_Should_CreateDocumentInBuffer_When_SaveWithPositions() {
        var bundle = CreateBundle();

        var saved = bundle.save_dokument_handlowy(definicja: "FV", kontrahent: "Abc",
            pozycje: [Json("""{"towar":"MONTAZ","ilosc":2,"cena":50}"""), Json("""{"towar":"MONTAZ","ilosc":1,"cena":10}""")]);

        saved.Saved.Should().BeTrue();
        saved.Dokument.Id.Should().NotBeNullOrEmpty();
        saved.Dokument.Stan.Should().Be(nameof(StanDokumentuHandlowego.Bufor));
        saved.Dokument.Pozycje.Should().HaveCount(2);
        saved.Dokument.Data.Should().MatchRegex(@"^\d{4}-\d{2}-\d{2}$");

        var read = bundle.get_dokument_handlowy(saved.Dokument.Id!);
        read.Pozycje.Select(p => p.Ilosc).Should().Equal(2, 1);
        read.Brutto.Should().Be(saved.Dokument.Brutto);

        var json = JsonSerializer.Serialize(read);
        json.Should().Contain("\"PozycjeCsv\"");
    }

    [Test]
    [Description("Zmiana dokumentu: dodanie pozycji i usunięcie istniejącej po lp")]
    public void Bundle_Should_UpdatePositions_When_SaveWithId() {
        var bundle = CreateBundle();
        var created = bundle.save_dokument_handlowy(definicja: "FV", kontrahent: "Abc",
            pozycje: [Json("""{"towar":"MONTAZ","ilosc":1,"cena":20}""")]);

        var updated = bundle.save_dokument_handlowy(id: created.Dokument.Id,
            pozycje: [Json("""{"towar":"MONTAZ","ilosc":3,"cena":30}"""), Json("""{"lp":1,"usun":true}""")]);

        updated.Dokument.Pozycje.Should().ContainSingle().Which.Ilosc.Should().Be(3);
    }

    [Test]
    [Description("Przekroczenie limitu brutto z parametru pakietu wycofuje zapis")]
    public void Bundle_Should_RejectSave_When_GrossAboveLimit() {
        var bundle = CreateBundle(maxBrutto: 100m);

        var act = () => bundle.save_dokument_handlowy(definicja: "FV", kontrahent: "Abc",
            pozycje: [Json("""{"towar":"MONTAZ","ilosc":10,"cena":50}""")]);

        act.Should().Throw<InvalidOperationException>().WithMessage("*przekracza limit*");
    }

    [Test]
    [Description("testing=true wylicza wartości bez zapisu")]
    public void Bundle_Should_NotSave_When_Testing() {
        var bundle = CreateBundle();

        var dry = bundle.save_dokument_handlowy(definicja: "FV", kontrahent: "Abc",
            pozycje: [Json("""{"towar":"MONTAZ","ilosc":1,"cena":40}""")], testing: true);

        dry.Saved.Should().BeFalse();
        dry.Dokument.Id.Should().BeNull();
        dry.Dokument.Brutto.Should().BePositive();
    }

    [Test]
    [Description("Zapis przez agenta zostawia wpis ChangeInfoType.Bundle z identyfikatorem agenta")]
    public void Bundle_Should_LogChangeInfo_When_Saved() {
        var bundle = CreateBundle();
        var saved = bundle.save_dokument_handlowy(definicja: "FV", kontrahent: "Abc",
            pozycje: [Json("""{"towar":"MONTAZ","ilosc":1,"cena":15}""")]);

        var (table, rowId) = Session.Login.ObjectIds().ResolveObject(saved.Dokument.Id);
        using var session = Session.Login.CreateSession(readOnly: true, config: false);
        var dok = (DokumentHandlowy)session.Tables[table].Rows.FindByID(rowId, false)!;

        var guid = dok.Guid;
        session.GetBusiness().ChangeInfos.ByTime[(ChangeInfo ci) => ci.SourceGuid == guid && ci.Type == ChangeInfoType.Bundle]
            .Cast<ChangeInfo>()
            .Should().Contain(ci => ci.Info.Contains(TestAgentGuid.ToString()));
    }

    [Test]
    [Description("Wyszukiwanie po definicji i kontrahencie zwraca identyfikatory i nagłówek CSV")]
    public void Bundle_Should_FindDocuments_When_SearchByDefinitionAndContractor() {
        var bundle = CreateBundle();
        var saved = bundle.save_dokument_handlowy(definicja: "FV", kontrahent: "Abc",
            pozycje: [Json("""{"towar":"MONTAZ","ilosc":1,"cena":12}""")]);

        var found = bundle.search_dokumenty_handlowe(definicja: "FV", kontrahent: "Abc", limit: 0);

        found.Documents.Select(d => d.Id).Should().Contain(saved.Dokument.Id);
        JsonSerializer.Serialize(found).Should().Contain("\"DocumentsCsv\"");
    }

    [Test]
    [Description("Poza zakresem ograniczonym (ProtectedState.IsRestricted == false) wartości wracają jawnie")]
    public void Bundle_Should_NotMask_When_NotRestricted() {
        var bundle = CreateBundle();
        var saved = bundle.save_dokument_handlowy(definicja: "FV", kontrahent: "Abc",
            pozycje: [Json("""{"towar":"MONTAZ","ilosc":1,"cena":11}""")]);

        ProtectedState.IsRestricted.Should().BeFalse();
        saved.Dokument.Kontrahent.Should().NotBeNullOrEmpty().And.NotBe("***");
    }

    private sealed class FakeBundleContext(Login login) : IBundleSessionContext {
        public object Owner => login;
        public Context Context => throw new NotSupportedException();
        public Guid AgentGuid => TestAgentGuid;
        public object? GetBundle(Type bundleType) => null;
    }
}
