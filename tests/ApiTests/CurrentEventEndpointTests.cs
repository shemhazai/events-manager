using System.Net;
using System.Text.Json;
using EventsManager.Domain.Events;
using Microsoft.Extensions.Logging;

namespace EventsManager.ApiTests;

/// <summary>
/// Surface HTTP de GET /api/events/current sur le pipeline réel : EV-009 à EV-019 de
/// docs/acceptance/event.md, et la contrepartie de journalisation de la section 3.6 de
/// docs/api-conventions.md.
/// <para>
/// « Aujourd'hui » vaut le 2026-06-15 pour cet hôte de test (<see cref="CurrentEventApiFactory"/>), la date de
/// référence de tous les exemples du fichier de critères ; les fenêtres sont celles qu'il donne
/// littéralement. Le port de persistance est un double alimenté avant l'appel : ce qui est
/// vérifié ici est le contrat de la réponse, pas la traduction SQL du prédicat, prouvée par les
/// tests d'intégration.
/// </para>
/// </summary>
public class CurrentEventEndpointTests(CurrentEventApiFactory factory) : IClassFixture<CurrentEventApiFactory>
{
    private const string CurrentEventPath = "/api/events/current";
    private const string Name = "Souper";
    private const string OtherName = "Barbecue";
    private const string ProblemJson = "application/problem+json";
    private const string WindowStartDateProperty = "order_window_start_date";

    private static readonly DateOnly EventDate = new(2026, 9, 12);

    /// <summary>
    /// « Aujourd'hui » au sens d'EV-D2 au moment de construire les évènements du test, pris avant
    /// toutes les fenêtres des critères : seule la géométrie de la fenêtre est en jeu, jamais la
    /// borne de création.
    /// </summary>
    private static readonly DateOnly CreationToday = new(2026, 1, 1);

    private readonly CurrentEventApiFactory _factory = factory;

    [Fact]
    public async Task Une_Seule_Candidate_Rend_200_Et_Un_Objet_Json_Singleton()
    {
        // EV-009 : exactement une candidate — 200 et un objet JSON décrivant cet évènement.
        // Forme singleton : jamais un tableau (section 2.6), jamais l'objet de page de la
        // section 6, qui ne concerne que les collections.
        var (status, contentType, body) = await GetCurrentAsync(Souper());

        status.Should().Be(HttpStatusCode.OK);
        contentType.Should().Be("application/json");
        body.TrimStart().Should().StartWith("{");

        using var document = JsonDocument.Parse(body);
        document.RootElement.ValueKind.Should().Be(JsonValueKind.Object);

        var properties = document.RootElement.EnumerateObject().Select(property => property.Name).ToArray();
        properties.Should().NotContain("items");
        properties.Should().NotContain("self");
        properties.Should().NotContain("next");
        properties.Should().NotContain("prev");
    }

    [Fact]
    public async Task Corps_Porte_Exactement_Les_Quatre_Champs_Du_Critere()
    {
        // EV-010 : contenu exposé, exactement quatre champs — id, name, date,
        // order_window_end_date. Pour « Souper » au 2026-09-12 avec la fenêtre 2026-06-01 vers
        // 2026-06-20, lu le 2026-06-15. Propriétés en snake_case, dates en ISO 8601
        // (sections 2.1 et 2.5).
        var souper = Souper();

        var (_, _, body) = await GetCurrentAsync(souper);

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        root.EnumerateObject().Select(property => property.Name).Should()
            .BeEquivalentTo("id", "name", "date", "order_window_end_date");
        root.GetProperty("id").GetGuid().Should().Be(souper.Id.Value);
        root.GetProperty("name").GetString().Should().Be(Name);
        root.GetProperty("date").GetString().Should().Be("2026-09-12");
        root.GetProperty("order_window_end_date").GetString().Should().Be("2026-06-20");
        body.Should().NotContain("orderWindowEndDate");
    }

    [Fact]
    public async Task Corps_N_Expose_Pas_Le_Debut_De_Fenetre()
    {
        // EV-011 : le début de fenêtre n'est pas exposé. Un corps qui porterait un
        // order_window_start_date est un défaut. Vérifié aussi sur la valeur du début
        // (2026-06-01) : la fuite ne serait pas moins réelle sous un autre nom de champ.
        var (_, _, body) = await GetCurrentAsync(Souper());

        using var document = JsonDocument.Parse(body);

        document.RootElement.TryGetProperty(WindowStartDateProperty, out _).Should().BeFalse();
        body.Should().NotContain(WindowStartDateProperty);
        body.Should().NotContain("2026-06-01");
    }

    [Fact]
    public async Task Corps_Porte_L_Identifiant_De_L_Evenement()
    {
        // EV-012 : l'id est exposé — il est requis pour poster une commande (OR-C1), sans lui le
        // front public ne peut rien faire de la réponse.
        var souper = Souper();

        var (_, _, body) = await GetCurrentAsync(souper);

        using var document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("id").GetGuid().Should().Be(souper.Id.Value);
    }

    [Fact]
    public async Task Aucune_Candidate_Rend_404_Et_Le_Problem_Json_Du_Catalogue()
    {
        // EV-014 : aucune candidate — 404 et Problem JSON, type /problems/not-found, titre
        // « Ressource introuvable » (section 4.7). Aucun slug fin n'est créé : le front sait déjà
        // quelle route il a appelée.
        var (status, contentType, body) = await GetCurrentAsync();

        status.Should().Be(HttpStatusCode.NotFound);
        contentType.Should().Be(ProblemJson);

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        root.GetProperty("type").GetString().Should().Be("/problems/not-found");
        root.GetProperty("title").GetString().Should().Be("Ressource introuvable");
        root.GetProperty("status").GetInt32().Should().Be(404);
        root.GetProperty("instance").GetString().Should().Be(CurrentEventPath);
        root.GetProperty("trace_id").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Base_Sans_Aucun_Evenement_Rend_404()
    {
        // EV-015, première des quatre situations qui rendent le même 404.
        var response = await GetCurrentAsync();

        AssertNotFoundWithBody(response);
    }

    [Fact]
    public async Task Un_Seul_Evenement_Sans_Fenetre_Rend_404()
    {
        // EV-015, deuxième situation : un seul évènement, sans fenêtre (EV-W5).
        var response = await GetCurrentAsync(Event.Create(Name, EventDate, CreationToday));

        AssertNotFoundWithBody(response);
    }

    [Fact]
    public async Task Un_Seul_Evenement_Dont_La_Fenetre_N_Est_Pas_Encore_Ouverte_Rend_404()
    {
        // EV-015, troisième situation : fenêtre 2026-06-16 vers 2026-06-20, pas encore ouverte.
        var response = await GetCurrentAsync(WithWindow(new DateOnly(2026, 6, 16), new DateOnly(2026, 6, 20)));

        AssertNotFoundWithBody(response);
    }

    [Fact]
    public async Task Un_Seul_Evenement_Dont_La_Fenetre_Est_Deja_Fermee_Rend_404()
    {
        // EV-015, quatrième situation : fenêtre 2026-06-01 vers 2026-06-14, déjà fermée.
        var response = await GetCurrentAsync(WithWindow(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 14)));

        AssertNotFoundWithBody(response);
    }

    [Fact]
    public async Task Le_404_D_Etat_Normal_Ne_Se_Journalise_Pas_Comme_Une_Erreur()
    {
        // EV-016 : ce 404 est un état normal et attendu, pas une anomalie — il pilote la page
        // « aucun évènement à venir ». Côté serveur il ne se journalise pas comme une erreur,
        // ce qui le distingue du 404 d'un identifiant inconnu (EV-R1).
        var (status, _, _) = await GetCurrentAsync();

        status.Should().Be(HttpStatusCode.NotFound);
        _factory.Logs.Entries.Where(entry => entry.Level >= LogLevel.Error).Should().BeEmpty();
    }

    [Fact]
    public async Task Deux_Candidates_Rendent_500_Et_Le_Problem_Json_Du_Catalogue()
    {
        // EV-017 : deux candidates — 500 et Problem JSON, type /problems/internal-error, titre
        // « Erreur interne » (section 4.7). Ce 500 est posé délibérément par le chemin de
        // lecture, il ne vient pas d'une exception subie.
        var (status, contentType, body) = await GetCurrentAsync(Barbecue(), Souper());

        status.Should().Be(HttpStatusCode.InternalServerError);
        contentType.Should().Be(ProblemJson);

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        root.GetProperty("type").GetString().Should().Be("/problems/internal-error");
        root.GetProperty("title").GetString().Should().Be("Erreur interne");
        root.GetProperty("status").GetInt32().Should().Be(500);
        root.GetProperty("instance").GetString().Should().Be(CurrentEventPath);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Deux_Candidates_Ne_Rendent_Aucun_Corps_D_Evenement(bool reversed)
    {
        // EV-018 (conséquence d'EV-017, à vérifier explicitement) : avec « Barbecue » au
        // 2026-07-04 (fenêtre 2026-06-01 vers 2026-06-20) et « Souper » au 2026-09-12 (fenêtre
        // 2026-06-10 vers 2026-08-31), tous deux ouverts le 2026-06-15, aucun corps d'évènement
        // ne sort — ni le premier par date d'évènement, ni le dernier, ni le plus récemment
        // inséré. D'où les deux ordres d'insertion.
        var barbecue = Barbecue();
        var souper = Souper(new DateOnly(2026, 6, 10), new DateOnly(2026, 8, 31));

        var (status, _, body) = reversed
            ? await GetCurrentAsync(souper, barbecue)
            : await GetCurrentAsync(barbecue, souper);

        status.Should().Be(HttpStatusCode.InternalServerError);

        using var document = JsonDocument.Parse(body);
        var properties = document.RootElement.EnumerateObject().Select(property => property.Name).ToArray();

        properties.Should().NotContain("id");
        properties.Should().NotContain("name");
        properties.Should().NotContain("date");
        properties.Should().NotContain("order_window_end_date");
        body.Should().NotContain(Name);
        body.Should().NotContain(OtherName);
        body.Should().NotContain(barbecue.Id.Value.ToString());
        body.Should().NotContain(souper.Id.Value.ToString());
        body.Should().NotContain("2026-07-04");
        body.Should().NotContain("2026-09-12");
    }

    [Fact]
    public async Task Le_500_Ne_Fuit_Ni_Exception_Ni_Trace_Et_Porte_Un_Trace_Id()
    {
        // EV-019 : le corps de ce 500 respecte les sections 4.5 et 4.6 comme tout autre — aucune
        // stack trace, aucun message d'exception brut, et un trace_id de corrélation. Le
        // diagnostic de l'incohérence vit dans les logs serveur, pas dans la réponse.
        var (_, _, body) = await GetCurrentAsync(Barbecue(), Souper());

        using var document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("trace_id").GetString().Should().NotBeNullOrWhiteSpace();

        body.Should().NotContain("Exception");
        body.Should().NotContain("   at ");
        body.Should().NotContain("EventsController");
        body.Should().NotContain("stackTrace");
        body.Should().NotContain("Invariant");
    }

    [Fact]
    public async Task Le_500_Delibere_Se_Journalise_Au_Niveau_Error_Avec_Les_Candidats()
    {
        // Contrepartie obligatoire de la section 3.6 : le corps ne dit rien de l'incohérence
        // (section 4.5), le diagnostic doit donc exister côté serveur — journalisation au niveau
        // Error, portant de quoi identifier les données en faute. Sans ce log, l'incident est
        // invisible.
        var barbecue = Barbecue();
        var souper = Souper();

        var (status, _, _) = await GetCurrentAsync(barbecue, souper);

        status.Should().Be(HttpStatusCode.InternalServerError);
        _factory.Logs.Entries.Should().Contain(entry =>
            entry.Level == LogLevel.Error
            && entry.Message.Contains(barbecue.Id.Value.ToString(), StringComparison.Ordinal)
            && entry.Message.Contains(souper.Id.Value.ToString(), StringComparison.Ordinal));
    }

    private static void AssertNotFoundWithBody((HttpStatusCode Status, string? ContentType, string Body) response)
    {
        // EV-015, conséquence d'EV-014 à vérifier explicitement : jamais un 200 au corps vide ou
        // nul, jamais un 204 — toujours un 404 porteur d'un Problem JSON.
        response.Status.Should().Be(HttpStatusCode.NotFound);
        response.Status.Should().NotBe(HttpStatusCode.OK);
        response.Status.Should().NotBe(HttpStatusCode.NoContent);
        response.ContentType.Should().Be(ProblemJson);
        response.Body.Should().NotBeNullOrWhiteSpace();

        using var document = JsonDocument.Parse(response.Body);
        document.RootElement.GetProperty("type").GetString().Should().Be("/problems/not-found");
    }

    private static Event WithWindow(DateOnly orderWindowStartDate, DateOnly orderWindowEndDate)
    {
        return Event.Create(Name, EventDate, CreationToday, orderWindowStartDate, orderWindowEndDate);
    }

    /// <summary>
    /// « Souper » au 2026-09-12 avec la fenêtre 2026-06-01 vers 2026-06-20, l'exemple d'EV-010.
    /// </summary>
    private static Event Souper()
    {
        return Souper(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 20));
    }

    private static Event Souper(DateOnly orderWindowStartDate, DateOnly orderWindowEndDate)
    {
        return Event.Create(Name, EventDate, CreationToday, orderWindowStartDate, orderWindowEndDate);
    }

    /// <summary>
    /// « Barbecue » au 2026-07-04 avec la fenêtre 2026-06-01 vers 2026-06-20, l'exemple
    /// d'EV-004 et d'EV-018.
    /// </summary>
    private static Event Barbecue()
    {
        return Event.Create(
            OtherName,
            new DateOnly(2026, 7, 4),
            CreationToday,
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 20));
    }

    private async Task<(HttpStatusCode Status, string? ContentType, string Body)> GetCurrentAsync(
        params Event[] events)
    {
        // Le client est créé d'abord : c'est lui qui démarre l'hôte, et les entrées de journal du
        // démarrage ne sont pas le sujet — d'où le Clear() juste avant la requête sous test.
        using var client = _factory.CreateClient();

        _factory.Repository.Events.Clear();
        _factory.Repository.Events.AddRange(events);
        _factory.Logs.Clear();

        using var response = await client.GetAsync(
            new Uri(CurrentEventPath, UriKind.Relative),
            TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        return (response.StatusCode, response.Content.Headers.ContentType?.MediaType, body);
    }
}
