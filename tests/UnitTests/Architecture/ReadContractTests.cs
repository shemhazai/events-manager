using System.Reflection;
using NetArchTest.Rules;

namespace EventsManager.UnitTests.Architecture;

/// <summary>
/// EV-R3 : les contrats de lecture n'exposent que des primitifs et des types BCL, jamais
/// un type du Domain. Sont des contrats de lecture les *Query (entrée), les *Dto (sortie)
/// et les signatures publiques des *QueryHandler qui les relient.
/// Les DTO s'emboîtent (OrderDto porte des OrderLineDto) : la règle vérifiée ici est
/// « aucun type du Domain », pas « rien d'autre que la BCL ».
/// Complément d'<see cref="ArchitectureTests"/> : là on interdit à l'Api d'utiliser un type
/// du Domain, ici on vérifie que les contrats qu'elle consomme n'en portent aucun — c'est
/// la condition qui rend la frontière tenable.
/// </summary>
public class ReadContractTests
{
    private const string DomainNamespace = "EventsManager.Domain";

    private static readonly Assembly Application =
        typeof(EventsManager.Application.AssemblyMarker).Assembly;

    private static readonly Assembly Domain =
        typeof(EventsManager.Domain.AssemblyMarker).Assembly;

    [Fact]
    public void QueriesAndDtos_MustNot_DependOn_Domain()
    {
        // Garde-fou : une sélection vide (ou qui raterait les contrats visés par EV-R3)
        // rendrait la règle vraie sans rien prouver.
        ReadContracts().GetTypes().Select(type => type.Name).Should()
                       .Contain([nameof(EventsManager.Application.Events.EventDto),
                                 nameof(EventsManager.Application.Events.GetEventQuery)]);

        var result = ReadContracts().ShouldNot().HaveDependencyOnAny(DomainNamespace).GetResult();

        result.IsSuccessful.Should().BeTrue(
            "les contrats de lecture (*Query, *Dto) ne doivent porter aucun type du Domain. " +
            $"Types en faute : {string.Join(", ", result.FailingTypes?.Select(t => t.FullName) ?? [])}");
    }

    [Fact]
    public void QueryHandlers_MustNot_Expose_DomainTypes_InTheirPublicMethods()
    {
        var handlers = Application.GetExportedTypes()
                                  .Where(type => type.Name.EndsWith("QueryHandler", StringComparison.Ordinal))
                                  .ToArray();

        // Garde-fou : une sélection vide (ou qui raterait le handler de lecture visé par
        // EV-R3) rendrait la règle vraie sans rien prouver.
        handlers.Select(handler => handler.Name).Should()
                .Contain(nameof(EventsManager.Application.Events.GetEventQueryHandler));

        var exposed = handlers
            .SelectMany(handler => handler.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .SelectMany(method => Flatten(method.ReturnType)
                .Concat(method.GetParameters().SelectMany(parameter => Flatten(parameter.ParameterType))))
            .Where(type => type.Assembly == Domain)
            .Select(type => type.FullName)
            .Distinct()
            .ToArray();

        exposed.Should().BeEmpty(
            "les signatures publiques des handlers de lecture ne doivent exposer aucun type du Domain, " +
            $"or : {string.Join(", ", exposed)}");
    }

    private static PredicateList ReadContracts()
    {
        return Types.InAssembly(Application)
                    .That()
                    .HaveNameEndingWith("Query")
                    .Or()
                    .HaveNameEndingWith("Dto");
    }

    /// <summary>
    /// Déplie un type exposé jusqu'à ses composants : Task&lt;T&gt;, IReadOnlyList&lt;T&gt;,
    /// T?, T[] — un type du Domain caché sous un générique reste une exposition.
    /// </summary>
    private static IEnumerable<Type> Flatten(Type type)
    {
        yield return type;

        var element = type.GetElementType();

        if (element is not null)
        {
            foreach (var nested in Flatten(element))
            {
                yield return nested;
            }
        }

        foreach (var argument in type.GetGenericArguments())
        {
            foreach (var nested in Flatten(argument))
            {
                yield return nested;
            }
        }
    }
}
