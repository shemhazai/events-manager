using EventsManager.Application.Events;
using EventsManager.Domain.Events;

namespace EventsManager.UnitTests.Application.Events;

/// <summary>
/// Contrat de sortie de la lecture de l'évènement courant : EV-010 (champs exposés), EV-011
/// (début de fenêtre non exposé), EV-012 (identifiant exposé) et EV-013 (contrat dédié au front
/// public) de docs/acceptance/event.md.
/// <para>
/// Les <b>noms de propriétés JSON</b> d'EV-010 sont vérifiés sur un corps de réponse réel, dans
/// les ApiTests : c'est la sérialisation qui les décide, pas le type .NET.
/// </para>
/// </summary>
public class CurrentEventContractTests
{
    private static readonly DateOnly EventDate = new(2026, 9, 12);
    private static readonly DateOnly CreationToday = new(2026, 1, 1);

    [Fact]
    public void ToCurrentEventDto_Maps_TheFourExposedFields()
    {
        // EV-010 et EV-012 : pour « Souper » au 2026-09-12 avec la fenêtre 2026-06-01 vers
        // 2026-06-20, le contrat porte l'identifiant, le nom, la date de l'évènement et la date
        // de fin de fenêtre.
        var @event = Event.Create(
            "Souper", EventDate, CreationToday, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 20));

        var dto = @event.ToCurrentEventDto();

        dto.Should().Be(new CurrentEventDto(@event.Id.Value, "Souper", EventDate, new DateOnly(2026, 6, 20)));
    }

    [Fact]
    public void CurrentEventDto_Exposes_ExactlyFourFields()
    {
        // EV-010 : exactement quatre champs, ni plus ni moins.
        var fields = typeof(CurrentEventDto).GetProperties().Select(property => property.Name);

        fields.Should().BeEquivalentTo(
            nameof(CurrentEventDto.Id),
            nameof(CurrentEventDto.Name),
            nameof(CurrentEventDto.Date),
            nameof(CurrentEventDto.OrderWindowEndDate));
    }

    [Fact]
    public void CurrentEventDto_DoesNotExpose_TheOrderWindowStart()
    {
        // EV-011 : le début de fenêtre n'est pas exposé. Par construction (EV-001, EV-002) un
        // évènement rendu a sa fenêtre ouverte, le début n'a donc plus aucune utilité pour le
        // front : un champ qui le porterait serait un défaut, pas un champ en trop sans
        // conséquence. Vérifié sur le nom plutôt que sur la seule présence
        // d'OrderWindowStartDate : un alias (WindowOpenedOn, StartDate…) serait la même fuite.
        var fields = typeof(CurrentEventDto).GetProperties().Select(property => property.Name);

        fields.Should().NotContain(nameof(Event.OrderWindowStartDate));
        fields.Should().NotContain(field => field.Contains("Start", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CurrentEventReadPath_DoesNotReuse_TheBackOfficeReadContract()
    {
        // EV-013 : le contrat de cette lecture est dédié au front public et distinct de celui du
        // back-office (EV-R1, EV-R2), aucune réutilisation du DTO de lecture par id.
        // Conséquence vérifiée ici : ajouter un champ à EventDto — catalogue de produits,
        // statistiques — ne peut pas changer le corps de GET /api/events/current, puisqu'aucun
        // type atteignable depuis ce chemin de lecture ne porte EventDto.
        var handleAsync = typeof(GetCurrentEventQueryHandler).GetMethod(
            nameof(GetCurrentEventQueryHandler.HandleAsync));

        // Les trois cas sont énumérés, pas seulement celui qui porte le DTO : Reachable ne
        // descend pas dans les types imbriqués, et une réutilisation glissée dans None ou
        // Ambiguous serait tout autant une réutilisation.
        var reachable = Reachable(typeof(CurrentEventDto))
            .Concat(Reachable(typeof(GetCurrentEventResult.Found)))
            .Concat(Reachable(typeof(GetCurrentEventResult.None)))
            .Concat(Reachable(typeof(GetCurrentEventResult.Ambiguous)))
            .Concat(Reachable(handleAsync!.ReturnType))
            .Distinct();

        reachable.Should().NotContain(typeof(EventDto));
        typeof(EventDto).IsAssignableFrom(typeof(CurrentEventDto)).Should().BeFalse();
        typeof(CurrentEventDto).IsAssignableFrom(typeof(EventDto)).Should().BeFalse();
    }

    /// <summary>
    /// Types atteignables depuis un type exposé : lui-même, ses arguments génériques
    /// (Task&lt;T&gt;, IReadOnlyList&lt;T&gt;) et les types de ses propriétés publiques, de
    /// proche en proche. Un DTO réutilisé sous un générique ou derrière une propriété reste une
    /// réutilisation.
    /// </summary>
    private static IEnumerable<Type> Reachable(Type type)
    {
        var visited = new HashSet<Type>();
        var pending = new Stack<Type>([type]);

        while (pending.Count > 0)
        {
            var current = pending.Pop();

            if (!visited.Add(current))
            {
                continue;
            }

            foreach (var argument in current.GetGenericArguments())
            {
                pending.Push(argument);
            }

            foreach (var property in current.GetProperties())
            {
                pending.Push(property.PropertyType);
            }
        }

        return visited;
    }
}
