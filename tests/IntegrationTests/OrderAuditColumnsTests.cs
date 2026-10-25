using EventsManager.Domain.Events;
using EventsManager.Domain.Orders;
using EventsManager.Infrastructure.Events;
using EventsManager.Infrastructure.Orders;
using Microsoft.EntityFrameworkCore;

namespace EventsManager.IntegrationTests;

/// <summary>
/// OR-P1 : colonnes d'audit DB-only (AddedBy, AddedDate, UpdatedBy, UpdatedDate) sur
/// app.Orders et app.OrderLines — hors modèle EF, donc lues en SQL brut. Added*
/// remplies par les contraintes DEFAULT à l'insertion ; Updated* par les triggers
/// BEFORE UPDATE (TR_Orders_Update_Audit, TR_OrderLines_Update_Audit).
/// </summary>
public class OrderAuditColumnsTests(PostgreSqlContainerFixture fixture)
{
    private static readonly DateOnly Today = new(2026, 7, 13);
    private static readonly DateOnly ValidDate = new(2026, 12, 31);
    private static readonly DateTime CreatedAtUtc = new(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc);

    private readonly PostgreSqlContainerFixture _fixture = fixture;

    [Fact]
    public async Task Insert_PopulatesAddedColumns_AndLeavesUpdatedColumnsNull_OnOrders()
    {
        var order = await AddOrderAsync();

        var audit = await ReadOrderAuditAsync(order.Id);

        audit.AddedBy.Should().NotBeNullOrWhiteSpace();
        audit.AddedDate.Should().NotBeNull();
        audit.UpdatedBy.Should().BeNull();
        audit.UpdatedDate.Should().BeNull();
    }

    [Fact]
    public async Task Insert_PopulatesAddedColumns_AndLeavesUpdatedColumnsNull_OnOrderLines()
    {
        var order = await AddOrderAsync();

        var audit = await ReadOrderLineAuditAsync(order.Id);

        audit.AddedBy.Should().NotBeNullOrWhiteSpace();
        audit.AddedDate.Should().NotBeNull();
        audit.UpdatedBy.Should().BeNull();
        audit.UpdatedDate.Should().BeNull();
    }

    [Fact]
    public async Task SqlUpdate_PopulatesUpdatedColumns_ViaTrigger_OnOrders()
    {
        var order = await AddOrderAsync();
        var newName = $"Renommé {Guid.CreateVersion7():N}";

        await using (var context = _fixture.CreateContext())
        {
            // L'agrégat est immuable et aucun use case de modification n'existe encore :
            // un UPDATE SQL direct suffit à prouver le trigger.
            await context.Database.ExecuteSqlAsync(
                $"UPDATE app.\"Orders\" SET \"CustomerName\" = {newName} WHERE \"Id\" = {order.Id.Value}",
                TestContext.Current.CancellationToken);
        }

        var audit = await ReadOrderAuditAsync(order.Id);

        audit.UpdatedBy.Should().NotBeNullOrWhiteSpace();
        audit.UpdatedDate.Should().NotBeNull();
    }

    [Fact]
    public async Task SqlUpdate_PopulatesUpdatedColumns_ViaTrigger_OnOrderLines()
    {
        var order = await AddOrderAsync();

        await using (var context = _fixture.CreateContext())
        {
            await context.Database.ExecuteSqlAsync(
                $"UPDATE app.\"OrderLines\" SET \"Quantity\" = 5 WHERE \"OrderId\" = {order.Id.Value}",
                TestContext.Current.CancellationToken);
        }

        var audit = await ReadOrderLineAuditAsync(order.Id);

        audit.UpdatedBy.Should().NotBeNullOrWhiteSpace();
        audit.UpdatedDate.Should().NotBeNull();
    }

    private async Task<Order> AddOrderAsync()
    {
        var @event = Event.Create($"Audit {Guid.CreateVersion7():N}", ValidDate, Today);
        var order = Order.Create(
            @event.Id,
            $"Client {Guid.CreateVersion7():N}",
            [OrderLine.Create("Frites", Money.From(4.50m), 2)],
            CreatedAtUtc);

        await using var context = _fixture.CreateContext();
        await new EventRepository(context).AddAsync(@event, TestContext.Current.CancellationToken);
        await new OrderRepository(context).AddAsync(order, TestContext.Current.CancellationToken);

        return order;
    }

    private async Task<AuditRow> ReadOrderAuditAsync(OrderId id)
    {
        await using var context = _fixture.CreateContext();

        return await context.Database
            .SqlQuery<AuditRow>(
                $"SELECT \"AddedBy\", \"AddedDate\", \"UpdatedBy\", \"UpdatedDate\" FROM app.\"Orders\" WHERE \"Id\" = {id.Value}")
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    private async Task<AuditRow> ReadOrderLineAuditAsync(OrderId orderId)
    {
        await using var context = _fixture.CreateContext();

        // Le setup ne crée qu'une seule ligne par commande : SingleAsync la vise sans ambiguïté.
        return await context.Database
            .SqlQuery<AuditRow>(
                $"SELECT \"AddedBy\", \"AddedDate\", \"UpdatedBy\", \"UpdatedDate\" FROM app.\"OrderLines\" WHERE \"OrderId\" = {orderId.Value}")
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Projection SQL brut (record positionnel : EF matérialise par le constructeur paramétré) ;
    /// propriétés nullable pour laisser la base dire ce qui est NULL.
    /// </summary>
    private sealed record AuditRow(string? AddedBy, DateTime? AddedDate, string? UpdatedBy, DateTime? UpdatedDate);
}
