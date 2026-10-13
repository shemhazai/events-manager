using EventsManager.Application.Events;
using Microsoft.AspNetCore.Mvc;

namespace EventsManager.Api.Events;

/// <summary>
/// Surface HTTP de la ressource « évènement ». Référentiel : docs/api-conventions.md.
/// </summary>
[ApiController]
[Route("api/events")]
public sealed class EventsController(
    GetCurrentEventQueryHandler getCurrentEventQueryHandler,
    ILogger<EventsController> logger) : ControllerBase
{
    private readonly GetCurrentEventQueryHandler _getCurrentEventQueryHandler = getCurrentEventQueryHandler;
    private readonly ILogger<EventsController> _logger = logger;

    /// <summary>
    /// Rend l'évènement dont la fenêtre de commande est ouverte aujourd'hui. Ressource
    /// singleton : le corps est l'évènement lui-même, jamais un tableau ni l'objet de page de
    /// la section 6, qui ne concerne que les collections.
    /// <para>
    /// Le <c>404</c> est un <b>état normal</b> : il pilote la page « aucun évènement à venir »
    /// du front public. Il ne se journalise donc pas, à la différence du <c>500</c>.
    /// </para>
    /// </summary>
    [HttpGet("current")]
    [ProducesResponseType<CurrentEventDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<IActionResult> GetCurrent(CancellationToken cancellationToken)
    {
        var result = await _getCurrentEventQueryHandler.HandleAsync(new GetCurrentEventQuery(), cancellationToken);

        switch (result)
        {
            case GetCurrentEventResult.Found found:
                return Ok(found.Event);

            case GetCurrentEventResult.None:
                // Aucun slug fin : le front sait quelle route il a appelée, un discriminant
                // dédié n'apporterait aucune information nouvelle (section 4.2). Le type et le
                // titre viennent du catalogue, par le customizer de Program.cs.
                return Problem(statusCode: StatusCodes.Status404NotFound);

            case GetCurrentEventResult.Ambiguous ambiguous:
                return AmbiguityDetected(ambiguous);

            default:
                // GetCurrentEventResult est une hiérarchie fermée : ce bras est inatteignable,
                // et le resterait même si un quatrième cas y était ajouté sans être traité ici
                // — auquel cas c'est ce throw qui le dirait, plutôt qu'un silence.
                throw new InvalidOperationException(
                    $"Issue de lecture non traitée : {result.GetType().Name}.");
        }
    }

    /// <summary>
    /// Invariant de non-chevauchement des fenêtres violé en base : la lecture ne choisit pas,
    /// elle échoue. Le <c>500</c> est <b>posé délibérément</b> (section 3.6), il ne vient pas
    /// d'une exception subie — d'où la journalisation explicite, qui est la seule trace du
    /// diagnostic : la réponse, elle, n'en dit rien (sections 4.5 et 4.6).
    /// </summary>
    private IActionResult AmbiguityDetected(GetCurrentEventResult.Ambiguous ambiguous)
    {
        // « Au moins », et non le décompte exact : la lecture plafonne volontairement sa
        // recherche de candidates, puisqu'elle n'a besoin que de distinguer 0, 1 et « plus d'un ».
        // Les identifiants listés sont donc ceux qu'elle a vus, pas nécessairement tous ceux qui
        // sont en faute — l'écrire autrement laisserait croire l'incohérence circonscrite à cette
        // paire.
        _logger.LogError(
            "Invariant de non-chevauchement des fenêtres de commande violé : au moins {CandidateCount} évènements ont leur fenêtre ouverte aujourd'hui, dont ({CandidateIds}). Aucun évènement courant n'est rendu.",
            ambiguous.CandidateIds.Count,
            string.Join(", ", ambiguous.CandidateIds));

        return Problem(statusCode: StatusCodes.Status500InternalServerError);
    }
}
