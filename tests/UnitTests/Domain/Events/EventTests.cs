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
        var @event = Event.Create("  Fête nationale  ", ValidDate, Today);

        @event.Name.Should().Be("Fête nationale");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidName_Throws(string? name)
    {
        Action act = () => Event.Create(name!, ValidDate, Today);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithNameOfMaxLength_Succeeds()
    {
        var name = new string('a', Event.NameMaxLength);

        var @event = Event.Create(name, ValidDate, Today);

        @event.Name.Should().Be(name);
    }

    [Fact]
    public void Create_WithNameTrimmedToMaxLength_Succeeds()
    {
        var name = $"  {new string('a', Event.NameMaxLength)}  ";

        var @event = Event.Create(name, ValidDate, Today);

        @event.Name.Should().Be(name.Trim());
    }

    [Fact]
    public void Create_WithNameLongerThanMaxLength_Throws()
    {
        var name = new string('a', Event.NameMaxLength + 1);

        Action act = () => Event.Create(name, ValidDate, Today);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithTomorrow_Succeeds()
    {
        var @event = Event.Create("Fête nationale", Today.AddDays(1), Today);

        @event.Date.Should().Be(Today.AddDays(1));
    }

    [Fact]
    public void Create_WithToday_Throws()
    {
        Action act = () => Event.Create("Fête nationale", Today, Today);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_WithPastDate_Throws()
    {
        Action act = () => Event.Create("Fête nationale", Today.AddDays(-1), Today);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_WithDefaultDate_Throws()
    {
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
