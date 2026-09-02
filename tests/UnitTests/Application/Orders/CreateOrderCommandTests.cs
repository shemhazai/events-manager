using EventsManager.Application.Orders;

namespace EventsManager.UnitTests.Application.Orders;

/// <summary>
/// Forme du contrat d'entrée de la création, indépendamment de sa validation
/// (<see cref="CreateOrderCommandValidatorTests"/>) et de son exécution
/// (<see cref="CreateOrderCommandHandlerTests"/>).
/// </summary>
public class CreateOrderCommandTests
{
    [Fact]
    public void Command_DoesNotCarryATotal()
    {
        // OR-C5 : le total n'est pas saisi, il est calculé — le contrat d'entrée ne porte
        // ni total de commande, ni sous-total de ligne : une ligne n'a que son prix
        // unitaire et sa quantité.
        var members = typeof(CreateOrderCommand).GetProperties()
            .Concat(typeof(CreateOrderCommand.Line).GetProperties())
            .Select(property => property.Name)
            .ToArray();

        // Garde-fou : une sélection vide rendrait la règle vraie sans rien prouver.
        members.Should().NotBeEmpty();

        members.Where(name => name.Contains("total", StringComparison.OrdinalIgnoreCase))
               .Should()
               .BeEmpty("le total n'est ni saisi ni stocké, il est calculé à partir des lignes");
    }
}
