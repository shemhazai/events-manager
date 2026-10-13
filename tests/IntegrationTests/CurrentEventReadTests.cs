using System.Data.Common;
using EventsManager.Application.Events;
using EventsManager.Domain.Events;
using EventsManager.Infrastructure.Events;
using Microsoft.EntityFrameworkCore;

namespace EventsManager.IntegrationTests;

/// <summary>
/// Lecture de l'évènement courant de bout en bout contre un vrai SQL Server : handler →
/// TodayLocal() → prédicat SQL d'encadrement → projection. Couvre EV-001 à EV-003, EV-007,
/// EV-009, EV-014, EV-015, EV-018 et EV-P4 de docs/acceptance/event.md, ainsi que les deux
/// règles de forme de la fenêtre (EV-W2, EV-W3) sur le seul chemin d'écriture de cette
/// tranche : l'insertion SQL directe (db/seed/).
/// <para>
/// <b>Isolation : un îlot de dates par test.</b> La base du conteneur est partagée par toute
/// l'assembly et les classes de tests tournent en parallèle. Or cette lecture interroge la table
/// entière : un évènement posé par un autre test, dont la fenêtre couvrirait le même jour,
/// fausserait le résultat. Chaque test travaille donc sur une <b>année civile qui lui est
/// propre</b>, disjointe de celles des autres, tout en conservant la <b>géométrie exacte</b> du
/// critère couvert — position de la fenêtre par rapport à « aujourd'hui », bornes incluses,
/// fenêtre d'un seul jour. Les valeurs littérales du fichier de critères
/// (aujourd'hui = 2026-06-15) sont exercées là où aucune base n'est partagée : tests unitaires
/// et ApiTests. Chaque test écrit puis relit via des DbContext distincts, et nomme ses
/// évènements de façon unique (l'index unique (Name, Year) l'exige).
/// </para>
/// </summary>
public class CurrentEventReadTests(SqlServerContainerFixture fixture)
{
    private readonly SqlServerContainerFixture _fixture = fixture;

    [Fact]
    public async Task HandleAsync_WithAWindowStartingToday_ReturnsThatEvent()
    {
        // EV-002 : borne de début incluse — le jour même du début compte. Îlot 2031.
        var today = new DateOnly(2031, 3, 10);
        var @event = await PersistAsync(WithWindow(new DateOnly(2031, 4, 20), today, new DateOnly(2031, 3, 15)));

        var result = await ReadCurrentAsync(today);

        result.Should().BeOfType<GetCurrentEventResult.Found>()
              .Which.Event.Should().Be(
                  new CurrentEventDto(@event.Id.Value, @event.Name, new DateOnly(2031, 4, 20), new DateOnly(2031, 3, 15)));
    }

    [Fact]
    public async Task HandleAsync_WithAWindowEndingToday_ReturnsThatEvent()
    {
        // EV-002 : borne de fin incluse — le jour même de la fin compte. Îlot 2032.
        var today = new DateOnly(2032, 3, 10);
        var @event = await PersistAsync(WithWindow(new DateOnly(2032, 4, 20), new DateOnly(2032, 3, 5), today));

        var result = await ReadCurrentAsync(today);

        result.Should().BeOfType<GetCurrentEventResult.Found>()
              .Which.Event.Id.Should().Be(@event.Id.Value);
    }

    [Fact]
    public async Task HandleAsync_WithAWindowStartingTomorrow_ReturnsNone()
    {
        // EV-002 : première valeur invalide avant le début — une fenêtre qui ouvre demain n'est
        // pas ouverte aujourd'hui. EV-015, troisième situation. Îlot 2033.
        var today = new DateOnly(2033, 3, 10);
        await PersistAsync(WithWindow(new DateOnly(2033, 4, 20), today.AddDays(1), new DateOnly(2033, 3, 15)));

        var result = await ReadCurrentAsync(today);

        result.Should().BeOfType<GetCurrentEventResult.None>();
    }

    [Fact]
    public async Task HandleAsync_WithAWindowClosedYesterday_ReturnsNone()
    {
        // EV-002 : première valeur invalide après la fin — une fenêtre fermée hier n'est plus
        // ouverte. L'évènement est pourtant à venir, ce qui redit EV-001 : seule la fenêtre
        // décide. EV-015, quatrième situation. Îlot 2034.
        var today = new DateOnly(2034, 3, 10);
        await PersistAsync(WithWindow(new DateOnly(2034, 4, 20), new DateOnly(2034, 2, 25), today.AddDays(-1)));

        var result = await ReadCurrentAsync(today);

        result.Should().BeOfType<GetCurrentEventResult.None>();
    }

    [Fact]
    public async Task HandleAsync_WithASingleDayWindowOnToday_ReturnsThatEvent()
    {
        // EV-003 (conséquence d'EV-002 et d'EV-W4, à vérifier explicitement) : une fenêtre d'un
        // seul jour est ouverte ce jour-là. Une fin exclusive rendrait ce cas vide. Îlot 2035.
        var today = new DateOnly(2035, 3, 10);
        var @event = await PersistAsync(WithWindow(new DateOnly(2035, 4, 20), today, today));

        var result = await ReadCurrentAsync(today);

        result.Should().BeOfType<GetCurrentEventResult.Found>()
              .Which.Event.Id.Should().Be(@event.Id.Value);
    }

    [Fact]
    public async Task HandleAsync_WithASingleDayWindowOnTheDayBefore_ReturnsNone()
    {
        // EV-003 : ce jour-là et lui seul — la veille, l'évènement n'est pas courant. Îlot 2036.
        var today = new DateOnly(2036, 3, 10);
        await PersistAsync(WithWindow(new DateOnly(2036, 4, 20), today.AddDays(-1), today.AddDays(-1)));

        var result = await ReadCurrentAsync(today);

        result.Should().BeOfType<GetCurrentEventResult.None>();
    }

    [Fact]
    public async Task HandleAsync_WithASingleDayWindowOnTheDayAfter_ReturnsNone()
    {
        // EV-003 : ce jour-là et lui seul — le lendemain non plus. Îlot 2037.
        var today = new DateOnly(2037, 3, 10);
        await PersistAsync(WithWindow(new DateOnly(2037, 4, 20), today.AddDays(1), today.AddDays(1)));

        var result = await ReadCurrentAsync(today);

        result.Should().BeOfType<GetCurrentEventResult.None>();
    }

    [Fact]
    public async Task HandleAsync_WithAnEventWithoutOrderWindow_ReturnsNone()
    {
        // EV-001 et EV-W5 : un évènement sans fenêtre n'est jamais courant, même à venir — en
        // SQL, l'encadrement par une colonne nulle vaut UNKNOWN, donc la ligne est écartée.
        // EV-015, deuxième situation. Îlot 2038.
        var today = new DateOnly(2038, 3, 10);
        await PersistAsync(Event.Create(UniqueName(), new DateOnly(2038, 4, 20), CreationDayOf(today)));

        var result = await ReadCurrentAsync(today);

        result.Should().BeOfType<GetCurrentEventResult.None>();
    }

    [Fact]
    public async Task HandleAsync_WithTwoOverlappingWindows_PicksNothing()
    {
        // EV-017 et EV-018 : deux candidates — l'invariant de non-chevauchement est violé en
        // base. La lecture échoue et ne choisit jamais silencieusement une candidate, ni la
        // première par date d'évènement, ni la dernière, ni la plus récemment insérée. Îlot 2039.
        var today = new DateOnly(2039, 3, 10);
        var early = await PersistAsync(
            WithWindow(new DateOnly(2039, 4, 4), new DateOnly(2039, 3, 1), new DateOnly(2039, 3, 20)));
        var late = await PersistAsync(
            WithWindow(new DateOnly(2039, 6, 12), new DateOnly(2039, 3, 5), new DateOnly(2039, 5, 31)));

        var result = await ReadCurrentAsync(today);

        result.Should().NotBeOfType<GetCurrentEventResult.Found>();
        result.Should().BeOfType<GetCurrentEventResult.Ambiguous>()
              .Which.CandidateIds.Should().BeEquivalentTo([early.Id.Value, late.Id.Value]);
    }

    [Fact]
    public async Task HandleAsync_WithThreeOverlappingWindows_StillPicksNothing()
    {
        // EV-007 et EV-018 : la lecture n'emploie aucun ordre de départage, et son plafond à deux
        // candidates ne la fait pas retomber sur un choix quand il y en a davantage. Îlot 2040.
        var today = new DateOnly(2040, 3, 10);
        var first = await PersistAsync(
            WithWindow(new DateOnly(2040, 4, 4), new DateOnly(2040, 3, 1), new DateOnly(2040, 3, 20)));
        var second = await PersistAsync(
            WithWindow(new DateOnly(2040, 5, 4), new DateOnly(2040, 3, 5), new DateOnly(2040, 4, 30)));
        var third = await PersistAsync(
            WithWindow(new DateOnly(2040, 6, 12), new DateOnly(2040, 3, 10), new DateOnly(2040, 5, 31)));

        var result = await ReadCurrentAsync(today);

        result.Should().NotBeOfType<GetCurrentEventResult.Found>();
        var ambiguous = result.Should().BeOfType<GetCurrentEventResult.Ambiguous>().Which;
        ambiguous.CandidateIds.Count.Should().BeGreaterThan(1);
        ambiguous.CandidateIds.Should().BeSubsetOf([first.Id.Value, second.Id.Value, third.Id.Value]);
    }

    [Fact]
    public async Task AddAsync_Then_GetByIdAsync_RoundtripsBothOrderWindowDates()
    {
        // EV-P4 : les deux dates de fenêtre sont persistées et relues à l'identique. EV-W1
        // (amendé) : elles sont posées dès la création, en une seule opération. Îlot 2041.
        var start = new DateOnly(2041, 3, 1);
        var end = new DateOnly(2041, 3, 20);
        var @event = await PersistAsync(WithWindow(new DateOnly(2041, 4, 20), start, end));

        var reloaded = await GetByIdAsync(@event.Id);

        reloaded.Should().NotBeNull();
        reloaded!.OrderWindowStartDate.Should().Be(start);
        reloaded.OrderWindowEndDate.Should().Be(end);
        reloaded.Date.Should().Be(new DateOnly(2041, 4, 20));
    }

    [Fact]
    public async Task AddAsync_Then_GetByIdAsync_KeepsBothOrderWindowDatesAbsent()
    {
        // EV-P4 : absentes des deux côtés — un évènement créé sans fenêtre est relu sans
        // fenêtre, les deux dates nulles (EV-W5). Îlot 2042.
        var @event = await PersistAsync(
            Event.Create(UniqueName(), new DateOnly(2042, 4, 20), CreationDayOf(new DateOnly(2042, 3, 10))));

        var reloaded = await GetByIdAsync(@event.Id);

        reloaded.Should().NotBeNull();
        reloaded!.OrderWindowStartDate.Should().BeNull();
        reloaded.OrderWindowEndDate.Should().BeNull();
    }

    [Fact]
    public async Task SqlInsert_WithOnlyTheOrderWindowStart_IsRejectedByTheDatabase()
    {
        // EV-W2 : renseigner une seule des deux dates est rejeté. Le domaine rend ce cas
        // inexprimable (aucune surcharge de Create ne l'accepte), mais le chemin d'écriture de
        // cette tranche est l'insertion SQL directe : c'est donc à la base de le refuser.
        // Îlot 2044.
        var id = Guid.CreateVersion7();

        Func<Task> act = () => ExecuteSqlAsync(
            $"INSERT INTO app.Events (Id, Name, [Date], OrderWindowStartDate) VALUES ({id}, {UniqueName()}, {new DateOnly(2044, 4, 20)}, {new DateOnly(2044, 3, 1)})");

        await act.Should().ThrowAsync<DbException>();
        (await GetByIdAsync(EventId.From(id))).Should().BeNull();
    }

    [Fact]
    public async Task SqlInsert_WithOnlyTheOrderWindowEnd_IsRejectedByTheDatabase()
    {
        // EV-W2, l'autre moitié : la date de fin seule est rejetée elle aussi. Îlot 2045.
        var id = Guid.CreateVersion7();

        Func<Task> act = () => ExecuteSqlAsync(
            $"INSERT INTO app.Events (Id, Name, [Date], OrderWindowEndDate) VALUES ({id}, {UniqueName()}, {new DateOnly(2045, 4, 20)}, {new DateOnly(2045, 3, 20)})");

        await act.Should().ThrowAsync<DbException>();
        (await GetByIdAsync(EventId.From(id))).Should().BeNull();
    }

    [Fact]
    public async Task SqlInsert_WithTheWindowEndAfterTheEventDate_IsRejectedByTheDatabase()
    {
        // EV-W3 : fin <= date de l'évènement — première valeur invalide, le lendemain de
        // l'évènement, rejetée jusqu'en base. Îlot 2046.
        var id = Guid.CreateVersion7();

        Func<Task> act = () => ExecuteSqlAsync(
            $"INSERT INTO app.Events (Id, Name, [Date], OrderWindowStartDate, OrderWindowEndDate) VALUES ({id}, {UniqueName()}, {new DateOnly(2046, 4, 20)}, {new DateOnly(2046, 3, 1)}, {new DateOnly(2046, 4, 21)})");

        await act.Should().ThrowAsync<DbException>();
        (await GetByIdAsync(EventId.From(id))).Should().BeNull();
    }

    [Fact]
    public async Task SqlInsert_WithTheWindowStartAfterItsEnd_IsRejectedByTheDatabase()
    {
        // EV-W3 : début <= fin — une fenêtre à l'envers est rejetée jusqu'en base. Îlot 2047.
        var id = Guid.CreateVersion7();

        Func<Task> act = () => ExecuteSqlAsync(
            $"INSERT INTO app.Events (Id, Name, [Date], OrderWindowStartDate, OrderWindowEndDate) VALUES ({id}, {UniqueName()}, {new DateOnly(2047, 4, 20)}, {new DateOnly(2047, 3, 20)}, {new DateOnly(2047, 3, 1)})");

        await act.Should().ThrowAsync<DbException>();
        (await GetByIdAsync(EventId.From(id))).Should().BeNull();
    }

    [Fact]
    public async Task SqlInsert_WithTheWindowEndOnTheEventDate_IsAcceptedByTheDatabase()
    {
        // EV-W3 : borne exacte dans l'autre sens — une fin posée le jour de l'évènement est la
        // dernière valeur valide, et la base l'accepte. Îlot 2048.
        var id = Guid.CreateVersion7();
        var eventDate = new DateOnly(2048, 4, 20);
        var start = new DateOnly(2048, 3, 1);

        await ExecuteSqlAsync(
            $"INSERT INTO app.Events (Id, Name, [Date], OrderWindowStartDate, OrderWindowEndDate) VALUES ({id}, {UniqueName()}, {eventDate}, {start}, {eventDate})");

        var reloaded = await GetByIdAsync(EventId.From(id));

        reloaded.Should().NotBeNull();
        reloaded!.OrderWindowStartDate.Should().Be(start);
        reloaded.OrderWindowEndDate.Should().Be(eventDate);
    }

    [Fact]
    public async Task HandleAsync_WithNoEventHavingAnOpenWindow_ReturnsNone()
    {
        // EV-014 et EV-015, première situation : aucune candidate — sur un îlot de dates qu'aucun
        // autre test ne couvre, la table ne porte rien d'ouvert ce jour-là. Îlot 2049.
        var result = await ReadCurrentAsync(new DateOnly(2049, 3, 10));

        result.Should().BeOfType<GetCurrentEventResult.None>();
    }

    /// <summary>
    /// « Aujourd'hui » au sens d'EV-D2 (date de l'évènement au moins à J+1) au moment de
    /// construire l'évènement : pris au premier jour de l'îlot, pour que seule la géométrie de la
    /// fenêtre soit en jeu et jamais la borne de création.
    /// </summary>
    private static DateOnly CreationDayOf(DateOnly islandDay)
    {
        return new DateOnly(islandDay.Year, 1, 1);
    }

    private static Event WithWindow(DateOnly eventDate, DateOnly orderWindowStartDate, DateOnly orderWindowEndDate)
    {
        return Event.Create(
            UniqueName(), eventDate, CreationDayOf(eventDate), orderWindowStartDate, orderWindowEndDate);
    }

    private static string UniqueName()
    {
        return $"Évènement courant {Guid.CreateVersion7():N}";
    }

    /// <summary>
    /// Horloge figée sur midi UTC du jour visé : Bruxelles est à UTC+1 ou UTC+2, la date locale
    /// que TodayLocal() en tire est donc bien celle-là, quelle que soit la machine de test.
    /// </summary>
    private static TimeProvider ClockAt(DateOnly today)
    {
        return new FixedTimeProvider(new DateTimeOffset(today.Year, today.Month, today.Day, 12, 0, 0, TimeSpan.Zero));
    }

    private async Task<GetCurrentEventResult> ReadCurrentAsync(DateOnly today)
    {
        await using var context = _fixture.CreateContext();
        var handler = new GetCurrentEventQueryHandler(new EventRepository(context), ClockAt(today));

        return await handler.HandleAsync(new GetCurrentEventQuery(), TestContext.Current.CancellationToken);
    }

    private async Task<Event> PersistAsync(Event @event)
    {
        await using var context = _fixture.CreateContext();
        await new EventRepository(context).AddAsync(@event, TestContext.Current.CancellationToken);

        return @event;
    }

    private async Task<Event?> GetByIdAsync(EventId id)
    {
        await using var context = _fixture.CreateContext();

        return await new EventRepository(context).GetByIdAsync(id, TestContext.Current.CancellationToken);
    }

    private async Task ExecuteSqlAsync(FormattableString sql)
    {
        await using var context = _fixture.CreateContext();
        await context.Database.ExecuteSqlAsync(sql, TestContext.Current.CancellationToken);
    }
}
