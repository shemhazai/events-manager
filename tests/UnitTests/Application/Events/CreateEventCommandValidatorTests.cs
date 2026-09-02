using EventsManager.Application.Events;
using EventsManager.Domain.Events;
using FluentValidation.TestHelper;

namespace EventsManager.UnitTests.Application.Events;

public class CreateEventCommandValidatorTests
{
    private static readonly DateOnly Today = new(2026, 7, 13);
    private static readonly DateOnly ValidDate = new(2026, 7, 14);

    private readonly CreateEventCommandValidator _validator = new(
        new FixedTimeProvider(new DateTimeOffset(2026, 7, 13, 12, 0, 0, TimeSpan.Zero)));

    [Fact]
    public void Validate_WithValidCommand_Passes()
    {
        var command = new CreateEventCommand("Fête nationale", ValidDate);

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithInvalidName_Fails(string? name)
    {
        // EV-N1 : nom obligatoire, non vide (ni blanc).
        var command = new CreateEventCommand(name!, ValidDate);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Name);
    }

    [Fact]
    public void Validate_WithNameOfMaxLength_Passes()
    {
        // EV-N3 : borne exacte — 100 caractères est la dernière longueur valide.
        var command = new CreateEventCommand(new string('a', Event.NameMaxLength), ValidDate);

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithNameLongerThanMaxLength_FailsWithMaxLengthMessage()
    {
        // EV-N3 : borne exacte — 101 caractères est la première longueur invalide.
        var command = new CreateEventCommand(new string('a', Event.NameMaxLength + 1), ValidDate);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Name)
              .WithErrorMessage("Le nom de l'évènement ne peut pas dépasser 100 caractères.");
    }

    [Fact]
    public void Validate_WithDefaultDate_FailsWithEmptyDateMessage()
    {
        // EV-D2 : une date non renseignée (default) n'est pas au moins à J+1 : rejetée.
        var command = new CreateEventCommand("Fête nationale", default);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Date)
              .WithErrorMessage("La date de l'évènement ne peut pas être vide.");
    }

    [Fact]
    public void Validate_WithTodayDate_FailsWithMinimumDateMessage()
    {
        // EV-D2 : borne exacte — aujourd'hui est rejeté, la date doit être au moins à J+1.
        var command = new CreateEventCommand("Fête nationale", Today);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Date)
              .WithErrorMessage("La date de l'évènement doit être au moins à J+1.");
    }

    [Fact]
    public void Validate_WithPastDate_FailsWithMinimumDateMessage()
    {
        // EV-D2 : une date antérieure à aujourd'hui est rejetée.
        var command = new CreateEventCommand("Fête nationale", Today.AddDays(-1));

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Date)
              .WithErrorMessage("La date de l'évènement doit être au moins à J+1.");
    }
}
