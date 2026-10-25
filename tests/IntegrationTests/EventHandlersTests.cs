using EventsManager.Application.Events;
using EventsManager.Domain.Events;
using EventsManager.Infrastructure;
using EventsManager.Infrastructure.Events;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace EventsManager.IntegrationTests;

/// <summary>
/// La chaîne applicative complète contre un vrai PostgreSQL, graphe réel assemblé à la
/// main (pas de host DI) : création — ValidateAndThrowAsync → TodayLocal() → Event.Create
/// → AddAsync (EF) ; lecture — EventId.From → GetByIdAsync (EF) → ToDto().
/// Complète les tests unitaires des handlers (FakeEventRepository) et les tests
/// d'intégration du repository : c'est le filet qui casse si un refactor déplace une
/// couture (ex. SaveChanges sorti de AddAsync vers un unit of work).
/// </summary>
public class EventHandlersTests(PostgreSqlContainerFixture fixture)
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly ValidDate = new(2026, 12, 31);

    private readonly PostgreSqlContainerFixture _fixture = fixture;

    [Fact]
    public async Task HandleAsync_CreateThenGet_RoundtripsThroughRealDatabase()
    {
        // EV-P2 : round-trip create → get via les vrais handlers ; EV-R1 : nom et date retournés.
        var name = UniqueName("Fête de la musique");
        Guid id;

        await using (var writeContext = _fixture.CreateContext())
        {
            var createHandler = CreateCreateHandler(writeContext);
            id = await createHandler.HandleAsync(
                new CreateEventCommand(name, ValidDate),
                TestContext.Current.CancellationToken);
        }

        // Lecture via un DbContext distinct : pas d'illusion de cache de premier niveau.
        await using var readContext = _fixture.CreateContext();
        var getHandler = new GetEventQueryHandler(new EventRepository(readContext));
        var dto = await getHandler.HandleAsync(new GetEventQuery(id), TestContext.Current.CancellationToken);

        dto.Should().Be(new EventDto(id, name, ValidDate));
    }

    [Fact]
    public async Task HandleAsync_CreateSeveralThenGet_ReturnsOnlyTheTargetedEvent()
    {
        var firstName = UniqueName("Carnaval");
        var targetName = UniqueName("Fête nationale");
        var thirdName = UniqueName("Marché de Noël");
        var targetDate = ValidDate;
        Guid targetId;

        await using (var writeContext = _fixture.CreateContext())
        {
            var createHandler = CreateCreateHandler(writeContext);
            await createHandler.HandleAsync(
                new CreateEventCommand(firstName, new DateOnly(2026, 8, 15)),
                TestContext.Current.CancellationToken);
            targetId = await createHandler.HandleAsync(
                new CreateEventCommand(targetName, targetDate),
                TestContext.Current.CancellationToken);
            await createHandler.HandleAsync(
                new CreateEventCommand(thirdName, new DateOnly(2026, 12, 6)),
                TestContext.Current.CancellationToken);
        }

        await using var readContext = _fixture.CreateContext();
        var getHandler = new GetEventQueryHandler(new EventRepository(readContext));
        var dto = await getHandler.HandleAsync(new GetEventQuery(targetId), TestContext.Current.CancellationToken);

        dto.Should().Be(new EventDto(targetId, targetName, targetDate));
    }

    [Fact]
    public async Task HandleAsync_CreateWithUntrimmedName_StoresTheTrimmedName()
    {
        // EV-N2 : le trim est appliqué avant stockage — «   Fête   » est stocké « Fête ».
        // Relu par un DbContext distinct : c'est bien la base qui rend le nom trimé.
        var name = UniqueName("Fête");
        Guid id;

        await using (var writeContext = _fixture.CreateContext())
        {
            var createHandler = CreateCreateHandler(writeContext);
            id = await createHandler.HandleAsync(
                new CreateEventCommand($"   {name}   ", ValidDate),
                TestContext.Current.CancellationToken);
        }

        await using var readContext = _fixture.CreateContext();
        var getHandler = new GetEventQueryHandler(new EventRepository(readContext));
        var dto = await getHandler.HandleAsync(new GetEventQuery(id), TestContext.Current.CancellationToken);

        dto.Should().Be(new EventDto(id, name, ValidDate));
    }

    [Fact]
    public async Task GetHandleAsync_WithUnknownId_ReturnsNull()
    {
        // EV-R1 : id inconnu — not found.
        await using var context = _fixture.CreateContext();
        var getHandler = new GetEventQueryHandler(new EventRepository(context));

        var dto = await getHandler.HandleAsync(
            new GetEventQuery(Guid.CreateVersion7()),
            TestContext.Current.CancellationToken);

        dto.Should().BeNull();
    }

    [Fact]
    public async Task CreateHandleAsync_WithInvalidCommand_PersistsNothingInDatabase()
    {
        // EV-N3 + EV-P3 : nom de 101 caractères — rien n'est persisté en base.
        // 68 « a » + espace + Guid N (32) = 101 caractères : invalide (limite 100) et unique,
        // donc requêtable pour prouver que rien n'a été écrit.
        var name = $"{new string('a', Event.NameMaxLength - 32)} {Guid.CreateVersion7():N}";

        await using (var context = _fixture.CreateContext())
        {
            var createHandler = CreateCreateHandler(context);

            Func<Task> act = () => createHandler.HandleAsync(
                new CreateEventCommand(name, ValidDate),
                TestContext.Current.CancellationToken);

            await act.Should().ThrowAsync<ValidationException>();
        }

        await using var verifyContext = _fixture.CreateContext();
        var persisted = await verifyContext.Events.AnyAsync(e => e.Name == name, TestContext.Current.CancellationToken);

        persisted.Should().BeFalse();
    }

    private static string UniqueName(string prefix)
    {
        return $"{prefix} {Guid.CreateVersion7():N}";
    }

    private static CreateEventCommandHandler CreateCreateHandler(EventsManagerDbContext context)
    {
        var timeProvider = new FixedTimeProvider(FixedUtcNow);

        return new CreateEventCommandHandler(
            new EventRepository(context),
            new CreateEventCommandValidator(timeProvider),
            timeProvider);
    }
}
