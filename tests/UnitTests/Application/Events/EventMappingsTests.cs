using EventsManager.Application.Events;
using EventsManager.Domain.Events;

namespace EventsManager.UnitTests.Application.Events;

public class EventMappingsTests
{
    [Fact]
    public void ToDto_Maps_AllProperties()
    {
        // EV-R1 : le contrat de lecture porte l'identifiant, le nom et la date de l'évènement.
        var @event = Event.Create("Fête nationale", new DateOnly(2026, 7, 14), new DateOnly(2026, 7, 13));

        var dto = @event.ToDto();

        dto.Id.Should().Be(@event.Id.Value);
        dto.Name.Should().Be("Fête nationale");
        dto.Date.Should().Be(new DateOnly(2026, 7, 14));
    }
}
