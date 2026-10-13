namespace EventsManager.Application.Events;

/// <summary>
/// Issue de la lecture de l'évènement courant. Trois cas, et trois seulement : le nullable de
/// convention du projet (<c>Task&lt;EventDto?&gt;</c>) ne sait porter qu'un échec, or celle-ci
/// en a deux, qui ne se traduisent pas par le même statut HTTP.
/// <para>
/// Hiérarchie <b>fermée</b> — constructeur privé, donc seuls les types imbriqués ici en
/// héritent. Le DTO est porté par le seul cas qui en a un : un succès sans évènement ne
/// compile pas, et l'appelant n'a aucun nullable à revérifier après avoir reconnu le cas.
/// </para>
/// </summary>
public abstract record GetCurrentEventResult
{
    private GetCurrentEventResult() { }

    /// <summary>Exactement une candidate : l'évènement courant.</summary>
    public sealed record Found(CurrentEventDto Event) : GetCurrentEventResult;

    /// <summary>
    /// Aucune candidate. État <b>normal et attendu</b> — il pilote la page « aucun évènement à
    /// venir » du front public — et non une anomalie.
    /// </summary>
    public sealed record None : GetCurrentEventResult;

    /// <summary>
    /// Deux candidates ou plus : l'invariant de non-chevauchement des fenêtres est violé en
    /// base. La lecture refuse de choisir ; <paramref name="CandidateIds"/> porte de quoi
    /// identifier les données en faute dans les logs serveur (au plus deux, cf.
    /// <see cref="IEventRepository.FindUpToTwoWithOrderWindowOpenOnAsync"/>).
    /// </summary>
    public sealed record Ambiguous(IReadOnlyList<Guid> CandidateIds) : GetCurrentEventResult;
}
