namespace EventsManager.Application.Events;

/// <summary>
/// Use case : lire l'évènement dont la fenêtre de commande est ouverte aujourd'hui.
/// Le handler décide des trois issues métier ; leur traduction en codes de statut appartient
/// à la couche au-dessus. En particulier il <b>refuse de choisir</b> quand plusieurs
/// évènements sont candidats : c'est ce refus qui est l'issue, pas un défaut d'implémentation.
/// </summary>
public sealed class GetCurrentEventQueryHandler(IEventRepository eventRepository, TimeProvider timeProvider)
{
    private readonly IEventRepository _eventRepository = eventRepository;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<GetCurrentEventResult> HandleAsync(
        GetCurrentEventQuery query,
        CancellationToken cancellationToken)
    {
        // La query est vide (ressource singleton) : rien d'autre dans ce corps ne la touche, et
        // un paramètre inutilisé est un signal que les analyseurs remontent à juste titre. La
        // garde est donc à la fois le contrat de la méthode et son seul usage légitime.
        ArgumentNullException.ThrowIfNull(query);

        var today = _timeProvider.TodayLocal();
        var candidates = await _eventRepository.FindUpToTwoWithOrderWindowOpenOnAsync(today, cancellationToken);

        if (candidates.Count == 0)
        {
            return new GetCurrentEventResult.None();
        }

        if (candidates.Count > 1)
        {
            return new GetCurrentEventResult.Ambiguous([.. candidates.Select(candidate => candidate.Id.Value)]);
        }

        return new GetCurrentEventResult.Found(candidates[0].ToCurrentEventDto());
    }
}
