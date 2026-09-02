using EventsManager.Domain.Events;
using EventsManager.Infrastructure.Events;
using Microsoft.EntityFrameworkCore;

namespace EventsManager.IntegrationTests;

/// <summary>
/// Aller-retours réels du repository contre SQL Server : mapping fluent
/// (conversion EventId, nvarchar(100), DateOnly → date) et index unique (Name, Year) —
/// un évènement n'a lieu qu'une fois par année civile.
/// Chaque test écrit puis relit via des DbContext distincts (pas d'illusion de cache),
/// et crée ses propres données avec un nom unique (l'index unique l'exige).
/// </summary>
public class EventRepositoryTests(SqlServerContainerFixture fixture)
{
    private static readonly DateOnly Today = new(2026, 7, 13);
    private static readonly DateOnly ValidDate = new(2026, 12, 31);

    private readonly SqlServerContainerFixture _fixture = fixture;

    [Fact]
    public async Task AddAsync_Then_GetByIdAsync_RoundtripsAggregate()
    {
        var @event = Event.Create(UniqueName("Fête nationale"), ValidDate, Today);
        await AddAsync(@event);

        await using var context = _fixture.CreateContext();
        var repository = new EventRepository(context);
        var reloaded = await repository.GetByIdAsync(@event.Id, TestContext.Current.CancellationToken);

        reloaded.Should().NotBeNull();
        reloaded!.Id.Should().Be(@event.Id);
        reloaded.Name.Should().Be(@event.Name);
        reloaded.Date.Should().Be(@event.Date);
    }

    [Fact]
    public async Task GetByIdAsync_WithSeveralPersistedEvents_ReturnsOnlyTheMatchingOne()
    {
        // Trois évènements, même année civile (2026) autorisée car noms distincts.
        var first = Event.Create(UniqueName("Carnaval"), new DateOnly(2026, 8, 15), Today);
        var target = Event.Create(UniqueName("Fête nationale"), ValidDate, Today);
        var third = Event.Create(UniqueName("Marché de Noël"), new DateOnly(2026, 12, 6), Today);
        await AddAsync(first);
        await AddAsync(target);
        await AddAsync(third);

        await using var context = _fixture.CreateContext();
        var repository = new EventRepository(context);
        var reloaded = await repository.GetByIdAsync(target.Id, TestContext.Current.CancellationToken);

        reloaded.Should().NotBeNull();
        reloaded!.Id.Should().Be(target.Id);
        reloaded.Name.Should().Be(target.Name);
        reloaded.Date.Should().Be(target.Date);
    }

    [Fact]
    public async Task GetByIdAsync_WithUnknownId_ReturnsNull()
    {
        await using var context = _fixture.CreateContext();
        var repository = new EventRepository(context);

        var found = await repository.GetByIdAsync(EventId.New(), TestContext.Current.CancellationToken);

        found.Should().BeNull();
    }

    [Fact]
    public async Task AddAsync_WithDuplicateNameAndYear_ThrowsDbUpdateException()
    {
        var name = UniqueName("Marché de Noël");
        await AddAsync(Event.Create(name, ValidDate, Today));

        // Même nom et même année civile (2026), mais autre date : l'index unique (Name, Year) refuse.
        var duplicate = Event.Create(name, new DateOnly(2026, 8, 15), Today);
        Func<Task> act = async () => await AddAsync(duplicate);

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task AddAsync_WithSameNameSurroundedByBlanks_ThrowsDbUpdateException()
    {
        // EV-U2 : la comparaison de noms d'EV-U1 s'applique après trim — « Souper » au
        // 12/09/2026 puis «   Souper   » au 08/11/2026, même nom et même année civile :
        // le second est rejeté.
        var name = UniqueName("Souper");
        await AddAsync(Event.Create(name, new DateOnly(2026, 9, 12), Today));

        var duplicate = Event.Create($"   {name}   ", new DateOnly(2026, 11, 8), Today);
        Func<Task> act = async () => await AddAsync(duplicate);

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task AddAsync_WithSameNameInDifferentYear_Succeeds()
    {
        // Le cas métier visé : l'édition annuelle (Saint-Nicolas 2026 puis 2027) garde le même nom.
        var name = UniqueName("Saint-Nicolas");
        await AddAsync(Event.Create(name, ValidDate, Today));

        var nextYearEdition = Event.Create(name, new DateOnly(2027, 12, 6), Today);
        await AddAsync(nextYearEdition);

        await using var context = _fixture.CreateContext();
        var repository = new EventRepository(context);
        var reloaded = await repository.GetByIdAsync(nextYearEdition.Id, TestContext.Current.CancellationToken);

        reloaded.Should().NotBeNull();
        reloaded!.Name.Should().Be(name);
    }

    [Fact]
    public async Task AddAsync_WithNameOfMaxLength_RoundtripsIntact()
    {
        // 67 « é » + espace + Guid N (32) = 100 caractères exactement : borne nvarchar(100)
        // et Unicode prouvés d'un coup, tout en restant unique.
        var name = $"{new string('é', Event.NameMaxLength - 33)} {Guid.CreateVersion7():N}";
        var @event = Event.Create(name, ValidDate, Today);
        await AddAsync(@event);

        await using var context = _fixture.CreateContext();
        var repository = new EventRepository(context);
        var reloaded = await repository.GetByIdAsync(@event.Id, TestContext.Current.CancellationToken);

        reloaded.Should().NotBeNull();
        reloaded!.Name.Should().Be(name);
    }

    private static string UniqueName(string prefix)
    {
        return $"{prefix} {Guid.CreateVersion7():N}";
    }

    private async Task AddAsync(Event @event)
    {
        await using var context = _fixture.CreateContext();
        var repository = new EventRepository(context);
        await repository.AddAsync(@event, TestContext.Current.CancellationToken);
    }
}
