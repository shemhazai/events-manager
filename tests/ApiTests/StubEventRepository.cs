using EventsManager.Application.Events;
using EventsManager.Domain.Events;

namespace EventsManager.ApiTests;

/// <summary>
/// Double de test de <see cref="IEventRepository"/> pour les tests de pipeline HTTP : ils
/// vérifient le <b>contrat de la réponse</b> (statut, type de média, corps), pas la traduction
/// SQL du prédicat — celle-ci est prouvée sur un vrai PostgreSQL par les tests d'intégration.
/// C'est aussi ce qui garde ces tests sans Docker, comme le reste de cette assembly.
/// <para>
/// Alimenté par <see cref="Events"/> avant l'appel HTTP. Même prédicat et même plafond à deux
/// éléments que l'implémentation EF, à la lettre.
/// </para>
/// </summary>
internal sealed class StubEventRepository : IEventRepository
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

    public Task<IReadOnlyList<Event>> FindUpToTwoWithOrderWindowOpenOnAsync(
        DateOnly today,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Event> candidates =
            [.. Events.Where(e => e.OrderWindowStartDate <= today && today <= e.OrderWindowEndDate).Take(2)];

        return Task.FromResult(candidates);
    }
}
