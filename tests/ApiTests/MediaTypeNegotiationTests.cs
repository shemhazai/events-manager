using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace EventsManager.ApiTests;

/// <summary>
/// Preuve que les types de médias négociables sont bien réduits à application/json, en requête
/// comme en réponse (docs/api-conventions.md, sections 5.1 et 9.3). AddControllers installe par
/// défaut des formateurs plus larges ; Program.cs les retire, et c'est un réglage de
/// négociation que seule une requête réelle peut vérifier.
/// </summary>
public class MediaTypeNegotiationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory = factory;

    [Fact]
    public async Task Succes_Sort_En_Application_Json()
    {
        using var response = await GetAsync("/__probe/payload", accept: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task Accept_Text_Json_Ne_Produit_Plus_Une_Reponse_Text_Json()
    {
        // text/json était annoncé par SystemTextJsonOutputFormatter : un client qui le demandait
        // recevait un succès étiqueté avec. ReturnHttpNotAcceptable restant à false, la requête
        // n'est pas rejetée — elle retombe sur application/json, ce qui est le comportement
        // voulu et ce que l'absence de ligne 406 en section 4.7 suppose.
        using var response = await GetAsync("/__probe/payload", "text/json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task Chaine_Rendue_Telle_Quelle_Ne_Sort_Plus_En_Text_Plain()
    {
        // StringOutputFormatter retiré : une chaîne est désormais sérialisée en JSON comme
        // n'importe quelle autre valeur, au lieu d'être rendue en texte brut.
        using var response = await GetAsync("/__probe/chaine-nue", accept: null);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        body.Should().Be("\"bonjour\"");
    }

    [Fact]
    public async Task Accept_Text_Plain_Ne_Produit_Plus_Une_Reponse_Text_Plain()
    {
        using var response = await GetAsync("/__probe/chaine-nue", "text/plain");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task Requete_En_Text_Json_Est_Refusee()
    {
        // Section 5.1 porte aussi sur les requêtes : text/json a été retiré des formateurs
        // d'entrée, un corps étiqueté ainsi est donc un type de média non supporté.
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/__probe/echo", UriKind.Relative))
        {
            Content = new StringContent("""{"label":"Tarte"}""", Encoding.UTF8, "text/json"),
        };

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Requete_En_Application_Json_Reste_Acceptee()
    {
        // Garde-fou du test précédent : si le formateur d'entrée avait été retiré en entier,
        // celui-ci passerait au rouge au lieu de laisser croire que tout va bien.
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/__probe/echo", UriKind.Relative))
        {
            Content = new StringContent("""{"label":"Tarte"}""", Encoding.UTF8, "application/json"),
        };

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    private async Task<HttpResponseMessage> GetAsync(string path, string? accept)
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.Relative));

        if (accept is not null)
        {
            request.Headers.Accept.Clear();
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
