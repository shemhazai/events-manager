using EventsManager.Domain.Events;
using EventsManager.Infrastructure.Events;
using Microsoft.EntityFrameworkCore;

namespace EventsManager.IntegrationTests;

/// <summary>
/// Colonnes d'audit DB-only (AddedBy, AddedDate, UpdatedBy, UpdatedDate) : hors modèle EF,
/// donc lues en SQL brut. Added* remplies par les contraintes DEFAULT à l'insertion ;
/// Updated* par le trigger BEFORE UPDATE (TR_Events_Update_Audit).
/// </summary>
public class EventAuditColumnsTests(PostgreSqlContainerFixture fixture)
{
    private static readonly DateOnly Today = new(2026, 7, 13);
    private static readonly DateOnly ValidDate = new(2026, 12, 31);

    private readonly PostgreSqlContainerFixture _fixture = fixture;

    [Fact]
    public async Task Insert_PopulatesAddedColumns_AndLeavesUpdatedColumnsNull()
    {
        // EV-P1 : colonnes d'audit DB-only — Added* remplies à l'insertion, Updated* nulles.
        var @event = await AddEventAsync();

        var audit = await ReadAuditAsync(@event.Id);

        audit.AddedBy.Should().NotBeNullOrWhiteSpace();
        audit.AddedDate.Should().NotBeNull();
        audit.UpdatedBy.Should().BeNull();
        audit.UpdatedDate.Should().BeNull();
    }

    [Fact]
    public async Task SqlUpdate_PopulatesUpdatedColumns_ViaTrigger()
    {
        // EV-P1 : colonnes d'audit DB-only — Updated* remplies par le trigger BEFORE UPDATE.
        var @event = await AddEventAsync();
        var newName = $"Renommé {Guid.CreateVersion7():N}";

        await using (var context = _fixture.CreateContext())
        {
            // L'agrégat est immuable et aucun use case de modification n'existe encore :
            // un UPDATE SQL direct suffit à prouver le trigger.
            await context.Database.ExecuteSqlAsync(
                $"UPDATE app.\"Events\" SET \"Name\" = {newName} WHERE \"Id\" = {@event.Id.Value}",
                TestContext.Current.CancellationToken);
        }

        var audit = await ReadAuditAsync(@event.Id);

        audit.UpdatedBy.Should().NotBeNullOrWhiteSpace();
        audit.UpdatedDate.Should().NotBeNull();
    }

    private async Task<Event> AddEventAsync()
    {
        var @event = Event.Create($"Audit {Guid.CreateVersion7():N}", ValidDate, Today);

        await using var context = _fixture.CreateContext();
        var repository = new EventRepository(context);
        await repository.AddAsync(@event, TestContext.Current.CancellationToken);

        return @event;
    }

    private async Task<EventAuditRow> ReadAuditAsync(EventId id)
    {
        await using var context = _fixture.CreateContext();

        return await context.Database
            .SqlQuery<EventAuditRow>(
                $"SELECT \"AddedBy\", \"AddedDate\", \"UpdatedBy\", \"UpdatedDate\" FROM app.\"Events\" WHERE \"Id\" = {id.Value}")
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Projection SQL brut (record positionnel : EF matérialise par le constructeur paramétré) ;
    /// propriétés nullable pour laisser la base dire ce qui est NULL.
    /// </summary>
    private sealed record EventAuditRow(string? AddedBy, DateTime? AddedDate, string? UpdatedBy, DateTime? UpdatedDate);
}
