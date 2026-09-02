using EventsManager.Application.Events;
using FluentValidation;

namespace EventsManager.UnitTests.Application.Events;

public class CreateEventCommandHandlerTests
{
    private static readonly DateOnly Today = new(2026, 7, 13);
    private static readonly DateOnly ValidDate = new(2026, 7, 14);

    private readonly FakeEventRepository _repository = new();
    private readonly CreateEventCommandHandler _handler;

    public CreateEventCommandHandlerTests()
    {
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 7, 13, 12, 0, 0, TimeSpan.Zero));
        _handler = new CreateEventCommandHandler(
            _repository,
            new CreateEventCommandValidator(timeProvider),
            timeProvider);
    }

    [Fact]
    public async Task HandleAsync_WithValidCommand_PersistsEventAndReturnsItsId()
    {
        var command = new CreateEventCommand("Fête nationale", ValidDate);

        var id = await _handler.HandleAsync(command, TestContext.Current.CancellationToken);

        var @event = _repository.Events.Should().ContainSingle().Subject;
        @event.Id.Value.Should().Be(id);
        @event.Name.Should().Be("Fête nationale");
        @event.Date.Should().Be(ValidDate);
    }

    [Fact]
    public async Task HandleAsync_WithInvalidCommand_ThrowsAndDoesNotPersist()
    {
        // EV-N1 + EV-P3 (versant applicatif) : nom blanc — rien n'est persisté.
        var command = new CreateEventCommand("   ", ValidDate);

        Func<Task> act = () => _handler.HandleAsync(command, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ValidationException>();
        _repository.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_WithTodayDate_ThrowsAndDoesNotPersist()
    {
        // EV-D2 + EV-P3 (versant applicatif) : date du jour — rien n'est persisté.
        var command = new CreateEventCommand("Fête nationale", Today);

        Func<Task> act = () => _handler.HandleAsync(command, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ValidationException>();
        _repository.Events.Should().BeEmpty();
    }
}
