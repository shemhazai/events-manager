using EventsManager.Application.Events;
using EventsManager.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace EventsManager.Infrastructure.Events;

/// <summary>
/// Implémentation EF Core du port <see cref="IEventRepository"/>.
/// AddAsync persiste immédiatement : le contrat du port implique la sauvegarde,
/// les handlers n'appellent rien d'autre.
/// </summary>
public sealed class EventRepository(EventsManagerDbContext context) : IEventRepository
{
    private readonly EventsManagerDbContext _context = context;

    public async Task AddAsync(Event @event, CancellationToken cancellationToken)
    {
        _context.Events.Add(@event);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Event?> GetByIdAsync(EventId id, CancellationToken cancellationToken)
    {
        return await _context.Events.SingleOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<bool> ExistsAsync(EventId id, CancellationToken cancellationToken)
    {
        return await _context.Events.AnyAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Event>> FindUpToTwoWithOrderWindowOpenOnAsync(
        DateOnly today,
        CancellationToken cancellationToken)
    {
        // Encadrement aux deux bornes incluses. Une fenêtre absente donne NULL <= @today, donc
        // UNKNOWN, donc une ligne écartée : « pas de fenêtre » signifie « commandes fermées »
        // sans qu'aucun test de nullité n'ait à l'écrire. Le même prédicat en LINQ-to-objects
        // écarte aussi la ligne ((DateOnly?)null <= today vaut false), ce qui garde les doubles
        // de test fidèles au comportement de la base.
        //
        // Aucun OrderBy : la lecture n'emploie aucun ordre de départage, et le TOP(2) qui en
        // résulte est donc volontairement non déterministe. Deux candidates sont une
        // incohérence à signaler, pas un ex æquo à trancher.
        return await _context.Events
                             .Where(e => e.OrderWindowStartDate <= today && today <= e.OrderWindowEndDate)
                             .Take(2)
                             .ToListAsync(cancellationToken);
    }
}
