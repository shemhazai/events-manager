using EventsManager.Application.Events;
using EventsManager.Domain.Events;

namespace EventsManager.UnitTests.Application.Events;

/// <summary>
/// Lecture de l'évènement courant : EV-001 à EV-003, EV-007, EV-009, EV-014, EV-015, EV-017 et
/// EV-018 de docs/acceptance/event.md. « Aujourd'hui » vaut 2026-06-15, la date de référence de
/// tous les exemples du fichier de critères, et les fenêtres sont celles qu'il donne
/// littéralement.
/// </summary>
public class GetCurrentEventQueryHandlerTests
{
    private const string Name = "Souper";
    private const string OtherName = "Barbecue";

    /// <summary>
    /// 2026-06-15 à 10 h UTC, soit 12 h à Bruxelles en heure d'été : TodayLocal() rend donc bien
    /// le 2026-06-15, quels que soient le fuseau et les réglages de la machine de test.
    /// </summary>
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 6, 15, 10, 0, 0, TimeSpan.Zero);

    private static readonly DateOnly EventDate = new(2026, 9, 12);

    /// <summary>
    /// « Aujourd'hui » au sens d'EV-D2 (date de l'évènement au moins à J+1) au moment de
    /// construire les évènements du test, pris avant toutes les fenêtres des critères : seule la
    /// géométrie de la fenêtre est en jeu ici, jamais la borne de création.
    /// </summary>
    private static readonly DateOnly CreationToday = new(2026, 1, 1);

    private readonly FakeEventRepository _repository = new();
    private readonly GetCurrentEventQueryHandler _handler;

    public GetCurrentEventQueryHandlerTests()
    {
        _handler = new GetCurrentEventQueryHandler(_repository, new FixedTimeProvider(FixedUtcNow));
    }

    [Fact]
    public async Task HandleAsync_WithAnOpenOrderWindow_ReturnsThatEvent()
    {
        // EV-001 : un évènement est courant quand sa fenêtre est ouverte aujourd'hui, et la date
        // de l'évènement ne décide rien — ici elle est à trois mois.
        // EV-009 et EV-010 : exactement une candidate, décrite par ses quatre champs.
        var @event = WithWindow(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 20));
        _repository.Events.Add(@event);

        var result = await _handler.HandleAsync(new GetCurrentEventQuery(), TestContext.Current.CancellationToken);

        result.Should().BeOfType<GetCurrentEventResult.Found>()
              .Which.Event.Should().Be(new CurrentEventDto(@event.Id.Value, Name, EventDate, new DateOnly(2026, 6, 20)));
    }

    [Fact]
    public async Task HandleAsync_WithoutAnyOrderWindow_ReturnsNone()
    {
        // EV-001 et EV-W5 : un évènement sans fenêtre n'est jamais courant, même daté du
        // 2026-09-12, donc à venir. EV-015, deuxième situation.
        _repository.Events.Add(Event.Create(Name, EventDate, CreationToday));

        var result = await _handler.HandleAsync(new GetCurrentEventQuery(), TestContext.Current.CancellationToken);

        result.Should().BeOfType<GetCurrentEventResult.None>();
    }

    [Fact]
    public async Task HandleAsync_WithAWindowStartingToday_ReturnsThatEvent()
    {
        // EV-002 : borne de début incluse — 2026-06-15 vers 2026-06-20 est ouverte, le jour même
        // du début compte.
        var @event = WithWindow(new DateOnly(2026, 6, 15), new DateOnly(2026, 6, 20));
        _repository.Events.Add(@event);

        var result = await _handler.HandleAsync(new GetCurrentEventQuery(), TestContext.Current.CancellationToken);

        result.Should().BeOfType<GetCurrentEventResult.Found>()
              .Which.Event.Id.Should().Be(@event.Id.Value);
    }

    [Fact]
    public async Task HandleAsync_WithAWindowEndingToday_ReturnsThatEvent()
    {
        // EV-002 : borne de fin incluse — 2026-06-10 vers 2026-06-15 est ouverte, le jour même
        // de la fin compte.
        var @event = WithWindow(new DateOnly(2026, 6, 10), new DateOnly(2026, 6, 15));
        _repository.Events.Add(@event);

        var result = await _handler.HandleAsync(new GetCurrentEventQuery(), TestContext.Current.CancellationToken);

        result.Should().BeOfType<GetCurrentEventResult.Found>()
              .Which.Event.Id.Should().Be(@event.Id.Value);
    }

    [Fact]
    public async Task HandleAsync_WithAWindowStartingTomorrow_ReturnsNone()
    {
        // EV-002 : première valeur invalide avant le début — 2026-06-16 vers 2026-06-20 n'est
        // pas ouverte le 2026-06-15. EV-015, troisième situation.
        _repository.Events.Add(WithWindow(new DateOnly(2026, 6, 16), new DateOnly(2026, 6, 20)));

        var result = await _handler.HandleAsync(new GetCurrentEventQuery(), TestContext.Current.CancellationToken);

        result.Should().BeOfType<GetCurrentEventResult.None>();
    }

    [Fact]
    public async Task HandleAsync_WithAWindowClosedYesterday_ReturnsNone()
    {
        // EV-002 : première valeur invalide après la fin — 2026-06-01 vers 2026-06-14 n'est pas
        // ouverte le 2026-06-15. L'évènement est pourtant à venir (2026-09-12), ce qui redit
        // EV-001 : seule la fenêtre décide. EV-015, quatrième situation.
        _repository.Events.Add(WithWindow(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 14)));

        var result = await _handler.HandleAsync(new GetCurrentEventQuery(), TestContext.Current.CancellationToken);

        result.Should().BeOfType<GetCurrentEventResult.None>();
    }

    [Fact]
    public async Task HandleAsync_WithASingleDayWindowOnToday_ReturnsThatEvent()
    {
        // EV-003 (conséquence d'EV-002 et d'EV-W4, à vérifier explicitement) : la fenêtre
        // 2026-06-15 vers 2026-06-15 rend l'évènement courant. Une fin exclusive rendrait ce cas
        // vide et contredirait EV-W4.
        var @event = WithWindow(new DateOnly(2026, 6, 15), new DateOnly(2026, 6, 15));
        _repository.Events.Add(@event);

        var result = await _handler.HandleAsync(new GetCurrentEventQuery(), TestContext.Current.CancellationToken);

        result.Should().BeOfType<GetCurrentEventResult.Found>()
              .Which.Event.Id.Should().Be(@event.Id.Value);
    }

    [Fact]
    public async Task HandleAsync_WithASingleDayWindowOnTheDayBefore_ReturnsNone()
    {
        // EV-003 : une fenêtre d'un seul jour est ouverte ce jour-là et lui seul —
        // 2026-06-14 vers 2026-06-14 ne rend pas l'évènement courant le 2026-06-15.
        _repository.Events.Add(WithWindow(new DateOnly(2026, 6, 14), new DateOnly(2026, 6, 14)));

        var result = await _handler.HandleAsync(new GetCurrentEventQuery(), TestContext.Current.CancellationToken);

        result.Should().BeOfType<GetCurrentEventResult.None>();
    }

    [Fact]
    public async Task HandleAsync_WithASingleDayWindowOnTheDayAfter_ReturnsNone()
    {
        // EV-003 : 2026-06-16 vers 2026-06-16 ne rend pas l'évènement courant le 2026-06-15.
        _repository.Events.Add(WithWindow(new DateOnly(2026, 6, 16), new DateOnly(2026, 6, 16)));

        var result = await _handler.HandleAsync(new GetCurrentEventQuery(), TestContext.Current.CancellationToken);

        result.Should().BeOfType<GetCurrentEventResult.None>();
    }

    [Fact]
    public async Task HandleAsync_WithoutAnyEvent_ReturnsNone()
    {
        // EV-014 et EV-015, première situation : aucun évènement du tout.
        var result = await _handler.HandleAsync(new GetCurrentEventQuery(), TestContext.Current.CancellationToken);

        result.Should().BeOfType<GetCurrentEventResult.None>();
    }

    [Fact]
    public async Task HandleAsync_WithTwoOpenWindows_ReportsBothCandidates()
    {
        // EV-017 : deux candidates — « Barbecue » au 2026-07-04 (fenêtre 2026-06-01 vers
        // 2026-06-20) et « Souper » au 2026-09-12 (fenêtre 2026-06-10 vers 2026-08-31), tous
        // deux ouverts le 2026-06-15. L'invariant de non-chevauchement est violé, la lecture le
        // signale au lieu de rendre un succès.
        _repository.Events.Add(Barbecue());
        _repository.Events.Add(Souper());

        var result = await _handler.HandleAsync(new GetCurrentEventQuery(), TestContext.Current.CancellationToken);

        var candidateIds = _repository.Events.Select(candidate => candidate.Id.Value).ToArray();
        result.Should().BeOfType<GetCurrentEventResult.Ambiguous>()
              .Which.CandidateIds.Should().BeEquivalentTo(candidateIds);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandleAsync_WithTwoOpenWindows_NeverPicksACandidate(bool reversed)
    {
        // EV-007 : la lecture n'emploie aucun ordre de départage — ni date d'évènement, ni ordre
        // d'insertion. EV-018 : elle échoue et ne choisit jamais silencieusement une candidate,
        // ni la première par date d'évènement, ni la dernière, ni la plus récemment insérée.
        // D'où les deux ordres d'insertion : l'issue doit être la même.
        var barbecue = Barbecue();
        var souper = Souper();
        _repository.Events.AddRange(reversed ? [souper, barbecue] : [barbecue, souper]);

        var result = await _handler.HandleAsync(new GetCurrentEventQuery(), TestContext.Current.CancellationToken);

        result.Should().NotBeOfType<GetCurrentEventResult.Found>();
        result.Should().BeOfType<GetCurrentEventResult.Ambiguous>();
    }

    private static Event WithWindow(DateOnly orderWindowStartDate, DateOnly orderWindowEndDate)
    {
        return Event.Create(Name, EventDate, CreationToday, orderWindowStartDate, orderWindowEndDate);
    }

    private static Event Barbecue()
    {
        return Event.Create(
            OtherName,
            new DateOnly(2026, 7, 4),
            CreationToday,
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 20));
    }

    private static Event Souper()
    {
        return Event.Create(
            Name,
            EventDate,
            CreationToday,
            new DateOnly(2026, 6, 10),
            new DateOnly(2026, 8, 31));
    }
}
