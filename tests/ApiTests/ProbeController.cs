using EventsManager.Application.Events;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EventsManager.ApiTests;

/// <summary>
/// Contrôleur de sonde, présent uniquement dans l'assembly de test : l'unité s'interdit tout
/// endpoint de production (c'est la 10a), mais les réglages du pipeline ne se prouvent que sur
/// une réponse HTTP réelle. Le préfixe « __probe » est volontairement hors convention d'URL
/// (section 1.2) pour qu'il ne puisse jamais être confondu avec une ressource de l'API.
/// </summary>
[ApiController]
[Route("__probe")]
public sealed class ProbeController : ControllerBase
{
    internal static readonly Guid PayloadId = Guid.Parse("0199c0de-0000-7000-8000-00000000cafe");

    [HttpGet("payload")]
    public ActionResult<object> Payload()
    {
        return Ok(new ProbePayload(
            PayloadId,
            "Dupont",
            new DateOnly(2026, 9, 12),
            Note: null,
            Tags: []));
    }

    [HttpGet("event-dto")]
    public ActionResult<EventDto> Event()
    {
        return Ok(new EventDto(PayloadId, "Souper", new DateOnly(2026, 9, 12)));
    }

    [HttpGet("mvc-problem")]
    public ActionResult MvcProblem()
    {
        // Chemin MVC : ControllerBase.Problem passe par ProblemDetailsFactory, pas par
        // IProblemDetailsService. On vérifie ici que le customizer gouverne quand même ce
        // chemin — sans quoi tout endpoint de la 10a pourrait publier un type absolu.
        return Problem(
            type: "https://tools.ietf.org/html/rfc9110#section-15.5.5",
            title: "Not Found",
            statusCode: StatusCodes.Status404NotFound);
    }

    [HttpGet("slug-sans-titre")]
    public ActionResult SlugSansTitre()
    {
        // Slug fin posé sans titre : le catalogue doit rattraper (section 4.4).
        return Problem(type: "/problems/name-taken", statusCode: StatusCodes.Status409Conflict);
    }

    [HttpPost("echo")]
    public ActionResult<ProbeInput> Echo([FromBody] ProbeInput input)
    {
        return Ok(input);
    }

    [HttpGet("validation-problem")]
    public ActionResult SondeValidation()
    {
        // Dictionnaire champ -> messages : c'est le seul endroit où DictionaryKeyPolicy
        // s'applique réellement (sections 2.1 et 4, membre errors).
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["OrderWindowStartDate"] = ["La date de début de fenêtre est obligatoire."],
        };

        return ValidationProblem(new ValidationProblemDetails(errors));
    }

    [HttpGet("boom")]
    public ActionResult Boom()
    {
        throw new InvalidOperationException("Secret de fabrication qui ne doit jamais atteindre le client.");
    }
}
