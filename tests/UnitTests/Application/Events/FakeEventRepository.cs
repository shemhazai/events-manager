using EventsManager.Application.Events;
using EventsManager.Domain.Events;

namespace EventsManager.UnitTests.Application.Events;

/// <summary>
/// Double de test main de <see cref="IEventRepository"/> (pas de lib de mock dans le repo).
/// </summary>
internal sealed class FakeEventRepository : IEventRepository
{
    public List<Event> Events { get; } = [];

    public Task AddAsync(Event @event, CancellationToken cancellationToken)
    {
        Events.Add(@event);
        return Task.CompletedTask;
    }

    public Task<Event?> GetByIdAsync(EventId id, CancellationToken cancellationToken)
    {
        return Task.FromResult(Events.Find(e => e.Id == id));
    }

    public Task<bool> ExistsAsync(EventId id, CancellationToken cancellationToken)
    {
        return Task.FromResult(Events.Exists(e => e.Id == id));
    }

    /// <summary>
    /// Même prédicat que l'implémentation EF, à la lettre : l'encadrement aux deux bornes
    /// incluses écarte de lui-même un évènement sans fenêtre, puisqu'en C# comme en SQL une
    /// comparaison impliquant un nul n'est jamais vraie. Le plafond à deux éléments fait partie
    /// du contrat du port, pas de l'implémentation : le reproduire est ce qui rend ce double
    /// capable de prouver que l'appelant ne dépend d'aucun ordre ni d'aucun décompte exact.
    /// </summary>
    public Task<IReadOnlyList<Event>> FindUpToTwoWithOrderWindowOpenOnAsync(
        DateOnly today,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Event> candidates =
            [.. Events.Where(e => e.OrderWindowStartDate <= today && today <= e.OrderWindowEndDate).Take(2)];

        return Task.FromResult(candidates);
    }
}
