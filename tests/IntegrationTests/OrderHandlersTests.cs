using EventsManager.Application.Orders;
using EventsManager.Domain.Events;
using EventsManager.Infrastructure;
using EventsManager.Infrastructure.Events;
using EventsManager.Infrastructure.Orders;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace EventsManager.IntegrationTests;

/// <summary>
/// La chaîne applicative Order complète contre un vrai PostgreSQL, graphe réel assemblé
/// à la main (pas de host DI) : création — ValidateAndThrowAsync → ExistsAsync →
/// Order.Create → AddAsync (EF) ; lectures — GetByIdAsync → ToDto() et
/// ListByEventAsync → ToSummaryDto(). Couvre OR-P2 (round-trip create → get → list),
/// OR-P3 (rien persisté si invalide, ni racine ni lignes), OR-R1/OR-R2/OR-R3.
/// Le tri par CreatedAt exige des horloges croissantes : un handler et un
/// FixedTimeProvider neufs par création.
/// </summary>
public class OrderHandlersTests(PostgreSqlContainerFixture fixture)
{
    private static readonly DateOnly Today = new(2026, 7, 13);
    private static readonly DateOnly ValidDate = new(2026, 12, 31);
    private static readonly DateTimeOffset BaseUtcNow = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);

    private readonly PostgreSqlContainerFixture _fixture = fixture;

    [Fact]
    public async Task HandleAsync_CreateThenGetThenList_RoundtripsThroughRealDatabase()
    {
        // OR-P2 : round-trip create → get → list via les vrais handlers.
        var eventId = await CreateEventAsync();
        var customerName = UniqueName("Alice");
        Guid orderId;

        await using (var writeContext = _fixture.CreateContext())
        {
            orderId = await CreateCreateHandler(writeContext, BaseUtcNow).HandleAsync(
                new CreateOrderCommand(
                    eventId,
                    customerName,
                    [new("Frites", 4.00m, 2), new("Bière", 1.50m, 3)]),
                TestContext.Current.CancellationToken);
        }

        // Lectures via des DbContext distincts : pas d'illusion de cache de premier niveau.
        await using (var readContext = _fixture.CreateContext())
        {
            var getHandler = new GetOrderQueryHandler(new OrderRepository(readContext));
            var dto = await getHandler.HandleAsync(new GetOrderQuery(orderId), TestContext.Current.CancellationToken);

            // OR-R1 : la commande est retournée avec ses lignes et son total
            // (2 × 4.00 + 3 × 1.50 = 12.50, exemple d'OR-C5).
            dto.Should().NotBeNull();
            dto!.Id.Should().Be(orderId);
            dto.EventId.Should().Be(eventId);
            dto.CustomerName.Should().Be(customerName);
            dto.Lines.Should().BeEquivalentTo(new[]
            {
                new OrderLineDto("Frites", 4.00m, 2, 8.00m),
                new OrderLineDto("Bière", 1.50m, 3, 4.50m),
            });
            dto.Total.Should().Be(12.50m);
        }

        await using var listContext = _fixture.CreateContext();
        var listHandler = CreateListHandler(listContext);
        var summaries = await listHandler.HandleAsync(
            new GetOrdersByEventQuery(eventId),
            TestContext.Current.CancellationToken);

        summaries.Should().NotBeNull();
        summaries!.Should().Equal(new OrderSummaryDto(orderId, customerName, 12.50m));
    }

    [Fact]
    public async Task ListHandleAsync_WithSeveralEvents_ReturnsOnlyItsOrdersOrderedByCreation()
    {
        // OR-R2 : avec plusieurs évènements persistés simultanément, la liste ne rend
        // que les commandes de l'évènement demandé, ordonnées par ordre de création.
        var eventA = await CreateEventAsync();
        var eventB = await CreateEventAsync();
        var firstCustomer = UniqueName("Zoé");
        var secondCustomer = UniqueName("Anna");
        Guid firstId;
        Guid secondId;

        await using (var writeContext = _fixture.CreateContext())
        {
            // Insertion volontairement dans le désordre des horloges : l'ordre du
            // résultat doit suivre l'ordre de création (CreatedAt), pas l'ordre d'insertion.
            secondId = await CreateCreateHandler(writeContext, BaseUtcNow.AddMinutes(2)).HandleAsync(
                new CreateOrderCommand(eventA, secondCustomer, [new("Gaufre", 3.00m, 2)]),
                TestContext.Current.CancellationToken);
            await CreateCreateHandler(writeContext, BaseUtcNow.AddMinutes(1)).HandleAsync(
                new CreateOrderCommand(eventB, UniqueName("Bruno"), [new("Crêpe", 2.00m, 1)]),
                TestContext.Current.CancellationToken);
            firstId = await CreateCreateHandler(writeContext, BaseUtcNow).HandleAsync(
                new CreateOrderCommand(eventA, firstCustomer, [new("Frites", 2.50m, 1)]),
                TestContext.Current.CancellationToken);
        }

        await using var readContext = _fixture.CreateContext();
        var listHandler = CreateListHandler(readContext);
        var summaries = await listHandler.HandleAsync(
            new GetOrdersByEventQuery(eventA),
            TestContext.Current.CancellationToken);

        summaries.Should().NotBeNull();
        summaries!.Should().Equal(
            new OrderSummaryDto(firstId, firstCustomer, 2.50m),
            new OrderSummaryDto(secondId, secondCustomer, 6.00m));
    }

    [Fact]
    public async Task GetHandleAsync_WithUnknownId_ReturnsNull()
    {
        // OR-R1 : id inconnu — not found (même comportement que GetEvent).
        await using var context = _fixture.CreateContext();
        var getHandler = new GetOrderQueryHandler(new OrderRepository(context));

        var dto = await getHandler.HandleAsync(
            new GetOrderQuery(Guid.CreateVersion7()),
            TestContext.Current.CancellationToken);

        dto.Should().BeNull();
    }

    [Fact]
    public async Task ListHandleAsync_WithEventWithoutOrders_ReturnsEmptyList()
    {
        // OR-R3 : évènement existant sans commandes — liste vide, pas not found.
        var eventId = await CreateEventAsync();

        await using var context = _fixture.CreateContext();
        var listHandler = CreateListHandler(context);
        var summaries = await listHandler.HandleAsync(
            new GetOrdersByEventQuery(eventId),
            TestContext.Current.CancellationToken);

        summaries.Should().NotBeNull();
        summaries!.Should().BeEmpty();
    }

    [Fact]
    public async Task ListHandleAsync_WithUnknownEvent_ReturnsNull()
    {
        // OR-R3 : évènement inconnu — not found.
        await using var context = _fixture.CreateContext();
        var listHandler = CreateListHandler(context);

        var summaries = await listHandler.HandleAsync(
            new GetOrdersByEventQuery(Guid.CreateVersion7()),
            TestContext.Current.CancellationToken);

        summaries.Should().BeNull();
    }

    [Fact]
    public async Task CreateHandleAsync_WithUnknownEvent_PersistsNothingInDatabase()
    {
        // OR-C1 : création vers un évènement inconnu — rejetée, rien n'est persisté.
        var customerName = UniqueName("Orphelin");
        var label = UniqueLabel();

        await using (var context = _fixture.CreateContext())
        {
            Func<Task> act = () => CreateCreateHandler(context, BaseUtcNow).HandleAsync(
                new CreateOrderCommand(Guid.CreateVersion7(), customerName, [new(label, 4.50m, 2)]),
                TestContext.Current.CancellationToken);

            await act.Should().ThrowAsync<ValidationException>();
        }

        await AssertNothingPersistedAsync(customerName, label);
    }

    [Fact]
    public async Task CreateHandleAsync_WithOneInvalidLine_PersistsNothingNotEvenValidLines()
    {
        // OR-P3 + OR-C6 : une ligne valide et une ligne invalide (quantité 21) —
        // rien n'est persisté, ni la racine ni aucune ligne, pas même la valide.
        var eventId = await CreateEventAsync();
        var customerName = UniqueName("Invalide");
        var validLabel = UniqueLabel();
        var invalidLabel = UniqueLabel();

        await using (var context = _fixture.CreateContext())
        {
            Func<Task> act = () => CreateCreateHandler(context, BaseUtcNow).HandleAsync(
                new CreateOrderCommand(
                    eventId,
                    customerName,
                    [new(validLabel, 4.00m, 2), new(invalidLabel, 1.50m, 21)]),
                TestContext.Current.CancellationToken);

            await act.Should().ThrowAsync<ValidationException>();
        }

        await AssertNothingPersistedAsync(customerName, validLabel, invalidLabel);
    }

    private static string UniqueName(string prefix)
    {
        return $"{prefix} {Guid.CreateVersion7():N}";
    }

    private static string UniqueLabel()
    {
        return $"Produit {Guid.CreateVersion7():N}";
    }

    private static CreateOrderCommandHandler CreateCreateHandler(EventsManagerDbContext context, DateTimeOffset utcNow)
    {
        return new CreateOrderCommandHandler(
            new OrderRepository(context),
            new EventRepository(context),
            new CreateOrderCommandValidator(),
            new FixedTimeProvider(utcNow));
    }

    private static GetOrdersByEventQueryHandler CreateListHandler(EventsManagerDbContext context)
    {
        return new GetOrdersByEventQueryHandler(new OrderRepository(context), new EventRepository(context));
    }

    private async Task<Guid> CreateEventAsync()
    {
        var @event = Event.Create(UniqueName("Évènement"), ValidDate, Today);

        await using var context = _fixture.CreateContext();
        await new EventRepository(context).AddAsync(@event, TestContext.Current.CancellationToken);

        return @event.Id.Value;
    }

    private async Task AssertNothingPersistedAsync(string customerName, params string[] labels)
    {
        await using var verifyContext = _fixture.CreateContext();

        var rootPersisted = await verifyContext.Orders
            .AnyAsync(o => o.CustomerName == customerName, TestContext.Current.CancellationToken);
        rootPersisted.Should().BeFalse();

        // Les lignes sont owned, hors DbSet : SQL brut pour prouver qu'aucune ligne
        // n'a survécu, même orpheline.
        foreach (var label in labels)
        {
            var lineCount = await verifyContext.Database
                .SqlQuery<int>($"SELECT COUNT(*) AS \"Value\" FROM app.\"OrderLines\" WHERE \"Label\" = {label}")
                .SingleAsync(TestContext.Current.CancellationToken);
            lineCount.Should().Be(0);
        }
    }
}
