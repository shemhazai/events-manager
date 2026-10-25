using EventsManager.Domain.Events;
using EventsManager.Infrastructure.Events;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EventsManager.IntegrationTests;

/// <summary>
/// Aller-retours réels du repository contre PostgreSQL : mapping fluent
/// (conversion EventId, varchar(100), DateOnly → date) et index unique (NameLower, Year) —
/// un évènement n'a lieu qu'une fois par année civile, et ce à la casse près.
/// Chaque test écrit puis relit via des DbContext distincts (pas d'illusion de cache),
/// et crée ses propres données avec un nom unique (l'index unique l'exige).
/// </summary>
public class EventRepositoryTests(PostgreSqlContainerFixture fixture)
{
    private static readonly DateOnly Today = new(2026, 7, 13);
    private static readonly DateOnly ValidDate = new(2026, 12, 31);

    private readonly PostgreSqlContainerFixture _fixture = fixture;

    [Fact]
    public async Task AddAsync_Then_GetByIdAsync_RoundtripsAggregate()
    {
        // EV-P1 : conventions de persistance existantes — identifiant typé, nom et date
        // relus intacts depuis PostgreSQL.
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
        // EV-U1 : deux évènements ne peuvent pas porter le même nom dans la même année
        // civile ; EV-U4 : l'unicité est garantie jusqu'en base, sur PostgreSQL.
        var name = UniqueName("Marché de Noël");
        await AddAsync(Event.Create(name, ValidDate, Today));

        // Même nom et même année civile (2026), mais autre date : l'index unique
        // (NameLower, Year) refuse. Le nom replié étant unique, le nom exact l'est aussi.
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
    public async Task AddAsync_WithSameNameDifferingOnlyByCase_ThrowsDbUpdateException()
    {
        // EV-U2 (amendé le 2026-10-12) : la comparaison de noms ignore la casse. Cette règle
        // n'avait AUCUN test, parce qu'elle reposait entièrement sur la collation CI par défaut
        // de SQL Server — PostgreSQL étant sensible à la casse, elle serait partie en silence.
        // Rien en amont ne la garantit : Event.Create ne connaît pas l'unicité et
        // CreateEventCommandHandler ne la vérifie pas. Seul l'index unique (NameLower, Year)
        // mord, et l'assertion sur son nom est ce qui empêche ce test de devenir vert pour une
        // autre raison que la sienne.
        var name = UniqueName("Souper");
        await AddAsync(Event.Create(name, new DateOnly(2026, 9, 12), Today));

        var duplicate = Event.Create(name.ToUpperInvariant(), new DateOnly(2026, 11, 8), Today);
        Func<Task> act = async () => await AddAsync(duplicate);

        var exception = await act.Should().ThrowAsync<DbUpdateException>();
        var inner = exception.Which.InnerException.Should().BeOfType<PostgresException>().Which;
        inner.SqlState.Should().Be("23505");
        inner.ConstraintName.Should().Be("IX_Events_NameLower_Year");
    }

    [Fact]
    public async Task AddAsync_WithSameNameInDifferentYear_Succeeds()
    {
        // EV-U1 : cas positif — le même nom dans une autre année civile est accepté.
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
        // EV-N3 : borne exacte — 100 caractères est la dernière longueur valide, jusqu'en base.
        // 67 « é » + espace + Guid N (32) = 100 caractères exactement : borne varchar(100)
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
