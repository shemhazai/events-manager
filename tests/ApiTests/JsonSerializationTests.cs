using System.Text.Json;

namespace EventsManager.ApiTests;

/// <summary>
/// Preuve des réglages de sérialisation de Program.cs sur une réponse HTTP réelle, et non sur
/// un JsonSerializerOptions reconstruit à côté — qui ne prouverait que lui-même.
/// Référentiel : docs/api-conventions.md, section 2.
/// </summary>
public class JsonSerializationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory = factory;

    [Fact]
    public async Task Proprietes_Sont_Serialisees_En_SnakeCase()
    {
        // Section 2.1 : snake_case partout, jamais camelCase.
        var json = await ReadRawAsync("/__probe/payload");

        json.Should().Contain("\"customer_name\":\"Dupont\"");
        json.Should().NotContain("customerName");
        json.Should().NotContain("CustomerName");
    }

    [Fact]
    public async Task Champ_Sans_Valeur_Est_Absent_Du_Json()
    {
        // Section 2.2 : un champ non renseigné est absent, jamais émis à null.
        var json = await ReadRawAsync("/__probe/payload");

        json.Should().NotContain("note");
        json.Should().NotContain("null");

        using var document = JsonDocument.Parse(json);
        document.RootElement.TryGetProperty("note", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Collection_Vide_Est_Serialisee_En_Tableau_Vide()
    {
        // Section 2.3 : une collection vide est [], jamais absente — la règle 2.2 ne s'y
        // applique pas.
        var json = await ReadRawAsync("/__probe/payload");

        json.Should().Contain("\"tags\":[]");
    }

    [Fact]
    public async Task DateOnly_Est_Serialisee_En_Iso_8601()
    {
        // Section 2.5 : "2026-09-12", sans heure et sans fuseau.
        var json = await ReadRawAsync("/__probe/payload");

        json.Should().Contain("\"event_date\":\"2026-09-12\"");
    }

    [Fact]
    public async Task Dto_De_L_Application_Respecte_La_Convention()
    {
        // Le réglage vaut pour les vrais contrats de la couche Application, pas seulement pour
        // la sonde : EventDto sort en id / name / date.
        var json = await ReadRawAsync("/__probe/event-dto");

        json.Should().Contain("\"id\":");
        json.Should().Contain("\"name\":\"Souper\"");
        json.Should().Contain("\"date\":\"2026-09-12\"");
    }

    [Fact]
    public async Task Reponse_De_Succes_Est_En_Application_Json()
    {
        // Section 5.1.
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/__probe/payload", UriKind.Relative), TestContext.Current.CancellationToken);

        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    private async Task<string> ReadRawAsync(string path)
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }
}
