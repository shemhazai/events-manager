using EventsManager.Api;

namespace EventsManager.ApiTests;

/// <summary>
/// Le catalogue est la seule source des couples (type, titre) servis au client. Ses entrées
/// atteignables par HTTP sont pincées dans <see cref="ProblemDetailsTests"/> ; ici on pince la
/// table elle-même, y compris les deux replis qu'aucune requête ne produit aujourd'hui mais
/// qui existent précisément pour qu'un code imprévu ne sorte jamais avec le titre anglais du
/// framework (docs/api-conventions.md, section 4.7).
/// </summary>
public class ProblemCatalogueTests
{
    [Theory]
    [InlineData(400, "/problems/validation", "Requête invalide")]
    [InlineData(404, "/problems/not-found", "Ressource introuvable")]
    [InlineData(405, "/problems/method-not-allowed", "Méthode HTTP non autorisée")]
    [InlineData(409, "/problems/conflict", "Conflit avec l'état de la ressource")]
    [InlineData(415, "/problems/unsupported-media-type", "Type de média non supporté")]
    [InlineData(500, "/problems/internal-error", "Erreur interne")]
    public void For_Rend_Le_Couple_Du_Referentiel(int status, string type, string title)
    {
        ProblemCatalogue.For(status).Should().Be((type, title));
    }

    [Theory]
    [InlineData(402)]
    [InlineData(418)]
    [InlineData(451)]
    public void Code_Client_Hors_Table_Retombe_Sur_Le_Repli_Client(int status)
    {
        ProblemCatalogue.For(status).Should().Be(("/problems/client-error", "Requête refusée"));
    }

    [Theory]
    [InlineData(501)]
    [InlineData(503)]
    public void Code_Serveur_Hors_Table_Retombe_Sur_Le_Repli_Serveur(int status)
    {
        ProblemCatalogue.For(status).Should().Be(("/problems/internal-error", "Erreur interne"));
    }

    [Fact]
    public void Tout_Type_Est_Une_Uri_Relative()
    {
        // Section 4.1 : le discriminant machine ne dépend pas de l'hôte de déploiement.
        int[] status = [400, 404, 405, 409, 415, 500, 418, 503];

        foreach (var code in status)
        {
            ProblemCatalogue.For(code).Type.Should().StartWith("/problems/");
        }
    }
}
