using EventsManager.Domain.Events;

namespace EventsManager.Application.Events;

public static class EventMappings
{
    public static EventDto ToDto(this Event @event)
    {
        return new EventDto(@event.Id.Value, @event.Name, @event.Date);
    }

    /// <summary>
    /// Projette un évènement <b>dont la fenêtre de commande est ouverte</b> vers le contrat du
    /// front public. Le début de fenêtre est délibérément laissé de côté.
    /// </summary>
    public static CurrentEventDto ToCurrentEventDto(this Event @event)
    {
        // Précondition de l'appelant, pas une règle métier : seul un évènement sélectionné pour
        // avoir sa fenêtre ouverte aujourd'hui arrive ici, sa date de fin est donc posée. Le
        // throw dit cette précondition au lieu de la masquer derrière un « ! ».
        var orderWindowEndDate = @event.OrderWindowEndDate
            ?? throw new InvalidOperationException(
                $"L'évènement {@event.Id} n'a pas de fenêtre de commande : il ne peut pas être projeté en {nameof(CurrentEventDto)}.");

        return new CurrentEventDto(@event.Id.Value, @event.Name, @event.Date, orderWindowEndDate);
    }
}
