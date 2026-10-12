using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace EventsManager.ApiTests;

/// <summary>
/// Preuve du contrat d'erreur : les deux chemins qui produisent une erreur sans passer par un
/// endpoint — l'exception non gérée (UseExceptionHandler) et le statut nu du routage
/// (UseStatusCodePages) — sortent tous deux en Problem JSON conforme.
/// Référentiel : docs/api-conventions.md, section 4.
/// </summary>
public class ProblemDetailsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory = factory;

    [Fact]
    public async Task Exception_Non_Geree_Sort_En_Problem_Json()
    {
        var (status, contentType, json) = await GetAsync("/__probe/boom");

        status.Should().Be(HttpStatusCode.InternalServerError);
        contentType.Should().Be("application/problem+json");

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.GetProperty("type").GetString().Should().Be("/problems/internal-error");
        root.GetProperty("title").GetString().Should().Be("Erreur interne");
        root.GetProperty("status").GetInt32().Should().Be(500);
        root.GetProperty("instance").GetString().Should().Be("/__probe/boom");
        root.GetProperty("trace_id").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Statut_Nu_Du_Routage_Sort_En_Problem_Json()
    {
        // Aucun endpoint ne répond ici : le 404 est produit par le routage, sans corps. C'est
        // UseStatusCodePages qui doit le transformer en Problem JSON.
        var (status, contentType, json) = await GetAsync("/api/route-qui-n-existe-pas");

        status.Should().Be(HttpStatusCode.NotFound);
        contentType.Should().Be("application/problem+json");

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.GetProperty("type").GetString().Should().Be("/problems/not-found");
        root.GetProperty("title").GetString().Should().Be("Ressource introuvable");
        root.GetProperty("status").GetInt32().Should().Be(404);
        root.GetProperty("instance").GetString().Should().Be("/api/route-qui-n-existe-pas");
        root.GetProperty("trace_id").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Type_Est_Une_Uri_Relative()
    {
        // Section 4.1 : le discriminant machine ne dépend pas de l'hôte de déploiement, et ne
        // porte donc jamais l'URI RFC absolue posée par défaut par le framework.
        var (_, _, json) = await GetAsync("/__probe/boom");

        using var document = JsonDocument.Parse(json);
        var type = document.RootElement.GetProperty("type").GetString();

        type.Should().StartWith("/problems/");
        type.Should().NotContain("://");
    }

    [Fact]
    public async Task Cle_De_Correlation_Est_En_SnakeCase()
    {
        // Section 4.6 : la clé d'une extension est écrite verbatim, aucune policy de nommage ne
        // s'y applique — c'est donc un choix littéral, qui doit être vérifié comme tel.
        var (_, _, json) = await GetAsync("/__probe/boom");

        json.Should().Contain("\"trace_id\":");
        json.Should().NotContain("traceId");
    }

    [Fact]
    public async Task Reponse_D_Erreur_Ne_Fuit_Rien_De_L_Exception()
    {
        // Section 4.5 : ni message d'exception, ni type d'exception, ni trace d'appel.
        var (_, _, json) = await GetAsync("/__probe/boom");

        json.Should().NotContain("Secret de fabrication");
        json.Should().NotContain("InvalidOperationException");
        json.Should().NotContain("ProbeController");
        json.Should().NotContain("   at ");
    }

    [Fact]
    public async Task Chemin_Mvc_Est_Gouverne_Par_Le_Customizer()
    {
        // ControllerBase.Problem passe par ProblemDetailsFactory et non par IProblemDetailsService :
        // rien ne garantit a priori que le customizer s'y applique. S'il ne s'y appliquait pas, un
        // endpoint de la 10a publierait un type absolu et un titre anglais sans que rien ne le
        // signale. La sonde pose volontairement les deux défauts et vérifie qu'ils sont corrigés.
        var (status, contentType, json) = await GetAsync("/__probe/mvc-problem");

        status.Should().Be(HttpStatusCode.NotFound);
        contentType.Should().Be("application/problem+json");

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.GetProperty("type").GetString().Should().Be("/problems/not-found");
        root.GetProperty("title").GetString().Should().Be("Ressource introuvable");
        root.GetProperty("trace_id").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Slug_Fin_Sans_Titre_Retombe_Sur_Le_Catalogue()
    {
        // Section 4.7 exige que type et title soient posés ensemble. Rien ne l'impose au
        // compilateur : si l'appelant oublie le titre, c'est le catalogue qui le fournit,
        // et jamais le titre anglais du framework (section 4.4).
        var (status, _, json) = await GetAsync("/__probe/slug-sans-titre");

        status.Should().Be(HttpStatusCode.Conflict);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.GetProperty("type").GetString().Should().Be("/problems/name-taken");
        root.GetProperty("title").GetString().Should().Be("Conflit avec l'état de la ressource");
    }

    [Fact]
    public async Task Exception_Reste_En_Problem_Json_Meme_Si_Le_Client_Demande_Du_Html()
    {
        // Le contrat d'erreur ne se négocie pas avec le client sur le chemin exception :
        // mesuré le 2026-09-11, le writer répond en problem+json même sous Accept: text/html.
        // Le chemin du statut nu, lui, dégrade — écart mesuré et consigné en section 9.4.
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/__probe/boom", UriKind.Relative));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Methode_Non_Autorisee_Sort_En_Problem_Json()
    {
        // 405 produit par le routage, jamais par un endpoint (section 4.7).
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/__probe/payload", UriKind.Relative));

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("type").GetString().Should().Be("/problems/method-not-allowed");
        document.RootElement.GetProperty("title").GetString().Should().Be("Méthode HTTP non autorisée");
        document.RootElement.GetProperty("trace_id").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Type_De_Media_Non_Supporte_Sort_En_Problem_Json()
    {
        // 415 produit par la négociation de contenu, jamais par un endpoint (section 4.7).
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/__probe/echo", UriKind.Relative))
        {
            Content = new StringContent("bonjour", Encoding.UTF8, "text/plain"),
        };

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("type").GetString().Should().Be("/problems/unsupported-media-type");
        document.RootElement.GetProperty("title").GetString().Should().Be("Type de média non supporté");
    }

    [Fact]
    public async Task Cles_Du_Dictionnaire_Errors_Sont_En_SnakeCase()
    {
        // Seul endroit où DictionaryKeyPolicy s'applique réellement : les clés du membre
        // errors sont des noms de champs, donc soumises à la section 2.1 comme les autres.
        var (status, _, json) = await GetAsync("/__probe/validation-problem");

        status.Should().Be(HttpStatusCode.BadRequest);
        json.Should().Contain("order_window_start_date");
        json.Should().NotContain("OrderWindowStartDate");

        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("errors")
                .GetProperty("order_window_start_date")[0].GetString()
                .Should().Be("La date de début de fenêtre est obligatoire.");
    }

    [Fact]
    public async Task Statut_Nu_Degrade_Quand_Le_Client_Refuse_Le_Json()
    {
        // TEST DE CARACTÉRISATION. Il ne décrit pas le comportement voulu, il fige l'écart
        // mesuré et consigné en section 9.4 : sur le chemin du statut nu, le writer par défaut
        // décline quand l'Accept exclut le JSON, et UseStatusCodePages retombe sur du texte.
        // Le jour où cet écart est comblé, ce test passe au rouge — c'est le signal attendu,
        // il faudra alors mettre à jour la section 9.4 en même temps que ce test.
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/route-qui-n-existe-pas", UriKind.Relative));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().NotBe("application/problem+json");
    }

    private async Task<(HttpStatusCode Status, string? ContentType, string Json)> GetAsync(string path)
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        return (response.StatusCode, response.Content.Headers.ContentType?.MediaType, json);
    }
}
