using EventsManager.Domain.Events;

namespace EventsManager.UnitTests.Domain.Events;

public class EventTests
{
    private static readonly DateOnly Today = new(2026, 7, 13);
    private static readonly DateOnly ValidDate = new(2026, 7, 14);

    [Fact]
    public void Create_WithValidData_SetsProperties()
    {
        var @event = Event.Create("Fête nationale", ValidDate, Today);

        @event.Id.Should().NotBe(default(EventId));
        @event.Name.Should().Be("Fête nationale");
        @event.Date.Should().Be(ValidDate);
    }

    [Fact]
    public void Event_ExposesASingleDate()
    {
        // EV-D1 : une seule date d'évènement, jamais une plage — l'agrégat n'expose
        // qu'une seule propriété DateOnly, et c'est Date.
        var dates = typeof(Event).GetProperties()
                                 .Where(property => property.PropertyType == typeof(DateOnly))
                                 .ToArray();

        dates.Should().ContainSingle().Which.Name.Should().Be(nameof(Event.Date));
    }

    [Fact]
    public void Create_Trims_Name()
    {
        // EV-N2 : trim appliqué au nom avant toute validation et avant stockage.
        var @event = Event.Create("  Fête nationale  ", ValidDate, Today);

        @event.Name.Should().Be("Fête nationale");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidName_Throws(string? name)
    {
        // EV-N1 : nom obligatoire — null, chaîne vide ou blancs seulement sont rejetés.
        Action act = () => Event.Create(name!, ValidDate, Today);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithNameOfMaxLength_Succeeds()
    {
        // EV-N3 : borne exacte — 100 caractères est la dernière longueur valide.
        var name = new string('a', Event.NameMaxLength);

        var @event = Event.Create(name, ValidDate, Today);

        @event.Name.Should().Be(name);
    }

    [Fact]
    public void Create_WithNameTrimmedToMaxLength_Succeeds()
    {
        // EV-N3 : la limite de 100 caractères se mesure après trim.
        var name = $"  {new string('a', Event.NameMaxLength)}  ";

        var @event = Event.Create(name, ValidDate, Today);

        @event.Name.Should().Be(name.Trim());
    }

    [Fact]
    public void Create_WithNameLongerThanMaxLength_Throws()
    {
        // EV-N3 : borne exacte — 101 caractères est la première longueur invalide.
        var name = new string('a', Event.NameMaxLength + 1);

        Action act = () => Event.Create(name, ValidDate, Today);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithTomorrow_Succeeds()
    {
        // EV-D2 : borne exacte — J+1 est la première date valide à la création.
        var @event = Event.Create("Fête nationale", Today.AddDays(1), Today);

        @event.Date.Should().Be(Today.AddDays(1));
    }

    [Fact]
    public void Create_WithToday_Throws()
    {
        // EV-D2 : borne exacte — aujourd'hui est rejeté, la date doit lui être
        // strictement postérieure.
        Action act = () => Event.Create("Fête nationale", Today, Today);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_WithPastDate_Throws()
    {
        // EV-D2 : une date antérieure à aujourd'hui est rejetée.
        Action act = () => Event.Create("Fête nationale", Today.AddDays(-1), Today);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_WithDefaultDate_Throws()
    {
        // EV-D2 : une date non construite (default, 01/01/0001) n'est pas strictement
        // postérieure à aujourd'hui : rejetée.
        Action act = () => Event.Create("Fête nationale", default, Today);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_Twice_GeneratesDistinctIds()
    {
        var first = Event.Create("Fête nationale", ValidDate, Today);
        var second = Event.Create("Marché de Noël", ValidDate, Today);

        first.Id.Should().NotBe(second.Id);
    }
}
