using EventsManager.Application.Events;
using EventsManager.Domain.Events;

namespace EventsManager.UnitTests.Application.Events;

public class GetEventQueryHandlerTests
{
    private static readonly DateOnly Today = new(2026, 7, 13);
    private static readonly DateOnly ValidDate = new(2026, 7, 14);

    private readonly FakeEventRepository _repository = new();
    private readonly GetEventQueryHandler _handler;

    public GetEventQueryHandlerTests()
    {
        _handler = new GetEventQueryHandler(_repository);
    }

    [Fact]
    public async Task HandleAsync_WithExistingEvent_ReturnsItsDto()
    {
        // EV-R1 : lecture par id — l'évènement est retourné avec son nom et sa date.
        var @event = Event.Create("Fête nationale", ValidDate, Today);
        _repository.Events.Add(@event);

        var dto = await _handler.HandleAsync(new GetEventQuery(@event.Id.Value), TestContext.Current.CancellationToken);

        dto.Should().Be(new EventDto(@event.Id.Value, "Fête nationale", ValidDate));
    }

    [Fact]
    public async Task HandleAsync_WithSeveralEvents_ReturnsOnlyTheTargetedOne()
    {
        var first = Event.Create("Carnaval", new DateOnly(2026, 8, 15), Today);
        var second = Event.Create("Fête nationale", ValidDate, Today);
        var third = Event.Create("Marché de Noël", new DateOnly(2026, 12, 6), Today);
        _repository.Events.Add(first);
        _repository.Events.Add(second);
        _repository.Events.Add(third);

        var dto = await _handler.HandleAsync(new GetEventQuery(second.Id.Value), TestContext.Current.CancellationToken);

        dto.Should().Be(new EventDto(second.Id.Value, "Fête nationale", ValidDate));
    }

    [Fact]
    public async Task HandleAsync_WithUnknownId_ReturnsNull()
    {
        // EV-R1 : id inconnu — not found.
        _repository.Events.Add(Event.Create("Fête nationale", ValidDate, Today));

        var dto = await _handler.HandleAsync(new GetEventQuery(Guid.CreateVersion7()), TestContext.Current.CancellationToken);

        dto.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_WithEmptyId_ThrowsArgumentException()
    {
        Func<Task> act = () => _handler.HandleAsync(new GetEventQuery(Guid.Empty), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
