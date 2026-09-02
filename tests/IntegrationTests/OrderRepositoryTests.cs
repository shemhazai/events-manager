using EventsManager.Domain.Events;
using EventsManager.Domain.Orders;
using EventsManager.Infrastructure.Events;
using EventsManager.Infrastructure.Orders;
using Microsoft.EntityFrameworkCore;

namespace EventsManager.IntegrationTests;

/// <summary>
/// Aller-retours réels du repository Order contre SQL Server : mapping fluent
/// (conversions OrderId/EventId/Money, lignes owned rechargées avec la racine),
/// FK physique vers app.Events (OR-P1) et unicité physique des libellés d'une
/// commande portée par la PK composite (OrderId, Label), collation CI comprise.
/// Chaque test écrit puis relit via des DbContext distincts (pas d'illusion de cache),
/// et crée ses propres données uniques (index unique (Name, Year) sur Events).
/// </summary>
public class OrderRepositoryTests(SqlServerContainerFixture fixture)
{
    private static readonly DateOnly Today = new(2026, 7, 13);
    private static readonly DateOnly ValidDate = new(2026, 12, 31);
    private static readonly DateTime CreatedAtUtc = new(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc);

    private readonly SqlServerContainerFixture _fixture = fixture;

    [Fact]
    public async Task AddAsync_Then_GetByIdAsync_RoundtripsAggregateWithItsLines()
    {
        // OR-C6 (versant persistance) : la racine et toutes ses lignes, persistées et
        // relues ensemble ; OR-C5 : le total recalculé après relecture reste 12.50.
        var eventId = await AddEventAsync();
        var order = Order.Create(
            eventId,
            UniqueName("Alice"),
            [
                OrderLine.Create("Frites", Money.From(4.00m), 2),
                OrderLine.Create("Bière", Money.From(1.50m), 3),
            ],
            CreatedAtUtc);
        await AddOrderAsync(order);

        await using var context = _fixture.CreateContext();
        var reloaded = await new OrderRepository(context)
            .GetByIdAsync(order.Id, TestContext.Current.CancellationToken);

        reloaded.Should().NotBeNull();
        reloaded!.Id.Should().Be(order.Id);
        reloaded.EventId.Should().Be(eventId);
        reloaded.CustomerName.Should().Be(order.CustomerName);
        reloaded.CreatedAt.Should().Be(CreatedAtUtc);
        reloaded.Lines.Should().BeEquivalentTo(order.Lines);
        reloaded.Total.Amount.Should().Be(12.50m);
    }

    [Fact]
    public async Task GetByIdAsync_WithUnknownId_ReturnsNull()
    {
        await using var context = _fixture.CreateContext();

        var found = await new OrderRepository(context)
            .GetByIdAsync(OrderId.New(), TestContext.Current.CancellationToken);

        found.Should().BeNull();
    }

    [Fact]
    public async Task AddAsync_WithUnknownEvent_ThrowsDbUpdateException()
    {
        // OR-P1 : la FK physique vers app.Events refuse une commande orpheline,
        // même si la couche Application était contournée.
        var order = Order.Create(
            EventId.New(),
            UniqueName("Fantôme"),
            [OrderLine.Create("Frites", Money.From(4.50m), 2)],
            CreatedAtUtc);

        Func<Task> act = () => AddOrderAsync(order);

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task OrdersTable_DoesNotStoreTheTotal()
    {
        // OR-C5 : le total n'est ni saisi ni stocké, il est calculé — aucune colonne de
        // total dans app.Orders. Introspection du schéma réel, pas du modèle EF.
        await using var context = _fixture.CreateContext();

        var columns = await context.Database
            .SqlQuery<string>($"SELECT name AS Value FROM sys.columns WHERE object_id = OBJECT_ID('app.Orders')")
            .ToListAsync(TestContext.Current.CancellationToken);

        // Garde-fou : table introuvable ou mal nommée = liste vide, l'assertion suivante
        // passerait à vide.
        columns.Should().NotBeEmpty();

        columns.Where(name => name.Contains("total", StringComparison.OrdinalIgnoreCase))
               .Should()
               .BeEmpty();
    }

    [Fact]
    public async Task InsertLineViaSql_WithDuplicateLabelIgnoringCase_ThrowsPrimaryKeyViolation()
    {
        // Filet physique du « pas de doublon de libellé » (OR-C4) : la PK composite
        // (OrderId, Label) refuse un doublon même en contournant le domaine, et la
        // collation CI par défaut de SQL Server couvre la différence de casse.
        var eventId = await AddEventAsync();
        var order = Order.Create(
            eventId,
            UniqueName("Benoît"),
            [OrderLine.Create("Frites", Money.From(4.00m), 2)],
            CreatedAtUtc);
        await AddOrderAsync(order);

        await using var context = _fixture.CreateContext();
        Func<Task> act = () => context.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO app.OrderLines (OrderId, Label, UnitPrice, Quantity)
             VALUES ({order.Id.Value}, {"FRITES"}, {2.00m}, {1});
             """,
            TestContext.Current.CancellationToken);

        // 2627 : « Violation of PRIMARY KEY constraint » — l'erreur SQL Server dédiée.
        (await act.Should().ThrowAsync<Microsoft.Data.SqlClient.SqlException>())
            .Which.Number.Should().Be(2627);
    }

    private static string UniqueName(string prefix)
    {
        return $"{prefix} {Guid.CreateVersion7():N}";
    }

    private async Task<EventId> AddEventAsync()
    {
        var @event = Event.Create(UniqueName("Évènement"), ValidDate, Today);

        await using var context = _fixture.CreateContext();
        await new EventRepository(context).AddAsync(@event, TestContext.Current.CancellationToken);

        return @event.Id;
    }

    private async Task AddOrderAsync(Order order)
    {
        await using var context = _fixture.CreateContext();
        await new OrderRepository(context).AddAsync(order, TestContext.Current.CancellationToken);
    }
}
