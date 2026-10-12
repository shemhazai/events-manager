namespace EventsManager.Api;

/// <summary>
/// Catalogue des catégories de problème : pour un code de statut, le slug relatif qui sert de
/// discriminant machine au client et le titre français stable qui l'accompagne.
/// Référentiel : docs/api-conventions.md, section 4.7.
/// Un endpoint qui veut un slug plus fin pose <c>type</c> et <c>title</c> lui-même ; le
/// customizer de Program.cs ne les écrase pas.
/// </summary>
internal static class ProblemCatalogue
{
    private static readonly Dictionary<int, (string Type, string Title)> ByStatus = new()
    {
        [StatusCodes.Status400BadRequest] = ("/problems/validation", "Requête invalide"),
        [StatusCodes.Status404NotFound] = ("/problems/not-found", "Ressource introuvable"),
        [StatusCodes.Status405MethodNotAllowed] = ("/problems/method-not-allowed", "Méthode HTTP non autorisée"),
        [StatusCodes.Status409Conflict] = ("/problems/conflict", "Conflit avec l'état de la ressource"),
        [StatusCodes.Status415UnsupportedMediaType] = ("/problems/unsupported-media-type", "Type de média non supporté"),
        [StatusCodes.Status500InternalServerError] = ("/problems/internal-error", "Erreur interne"),
    };

    private static readonly (string Type, string Title) ClientFallback =
        ("/problems/client-error", "Requête refusée");

    private static readonly (string Type, string Title) ServerFallback =
        ("/problems/internal-error", "Erreur interne");

    /// <summary>
    /// Rend la catégorie d'un code de statut. Un code hors table retombe sur la catégorie
    /// générique de sa classe : jamais de titre anglais du framework en réponse (section 4.4).
    /// </summary>
    internal static (string Type, string Title) For(int status)
    {
        if (ByStatus.TryGetValue(status, out var category))
        {
            return category;
        }

        return status >= StatusCodes.Status500InternalServerError ? ServerFallback : ClientFallback;
    }
}
