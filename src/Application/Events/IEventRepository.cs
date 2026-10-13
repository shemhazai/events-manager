using EventsManager.Domain.Events;

namespace EventsManager.Application.Events;

/// <summary>
/// Port de persistance de l'agrégat <see cref="Event"/>.
/// </summary>
public interface IEventRepository
{
    Task AddAsync(Event @event, CancellationToken cancellationToken);

    Task<Event?> GetByIdAsync(EventId id, CancellationToken cancellationToken);

    /// <summary>Existence seule — sert aux autres agrégats qui référencent un évènement par identité.</summary>
    Task<bool> ExistsAsync(EventId id, CancellationToken cancellationToken);

    /// <summary>
    /// Évènements dont la fenêtre de commande couvre <paramref name="today"/>, <b>bornes
    /// incluses</b>. Un évènement sans fenêtre n'est jamais rendu.
    /// <para>
    /// Rend <b>au plus deux</b> éléments, et c'est contractuel : le chemin de lecture de
    /// l'évènement courant ne distingue que « aucun », « un seul » et « plus d'un » — au-delà
    /// de deux, il n'y a rien de plus à apprendre, et charger la suite serait du travail jeté.
    /// Aucun ordre n'est garanti : l'unicité de la candidate vient de l'invariant de
    /// non-chevauchement des fenêtres, jamais d'un départage à la lecture.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<Event>> FindUpToTwoWithOrderWindowOpenOnAsync(DateOnly today, CancellationToken cancellationToken);
}
