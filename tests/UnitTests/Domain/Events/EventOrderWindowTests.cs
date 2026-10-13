using System.Reflection;
using EventsManager.Domain.Events;

namespace EventsManager.UnitTests.Domain.Events;

/// <summary>
/// Fenêtre de commande de l'agrégat : EV-W1 (amendé le 2026-10-12) à EV-W5 de
/// docs/acceptance/event.md. Les valeurs sont celles du fichier de critères, « aujourd'hui »
/// valant 2026-06-15 et l'évènement étant daté du 2026-09-12.
/// <para>
/// Les critères ne disent que « accepté » ou « rejeté » : les tests négatifs attendent donc
/// <see cref="ArgumentException"/>, la famille d'exceptions d'un argument refusé, et non un type
/// plus précis qui serait un choix d'implémentation et non un attendu métier.
/// </para>
/// </summary>
public class EventOrderWindowTests
{
    private const string Name = "Souper";

    private static readonly DateOnly Today = new(2026, 6, 15);
    private static readonly DateOnly EventDate = new(2026, 9, 12);

    [Fact]
    public void Create_WithOrderWindow_PostsBothDatesInASingleOperation()
    {
        // EV-W1 (amendé) : les deux dates doivent pouvoir être posées dès la création. Un
        // évènement au 2026-09-12 créé aujourd'hui (2026-06-15) avec la fenêtre
        // 2026-06-15 → 2026-06-20 est accepté en une seule opération.
        var @event = Event.Create(Name, EventDate, Today, new DateOnly(2026, 6, 15), new DateOnly(2026, 6, 20));

        @event.Date.Should().Be(EventDate);
        @event.OrderWindowStartDate.Should().Be(new DateOnly(2026, 6, 15));
        @event.OrderWindowEndDate.Should().Be(new DateOnly(2026, 6, 20));
    }

    [Fact]
    public void Create_ExposesNoWayToPostASingleOrderWindowDate()
    {
        // EV-W2 : les deux dates sont soit toutes deux présentes, soit toutes deux absentes,
        // renseigner une seule des deux est rejeté. Côté domaine le rejet n'est pas un
        // contrôle mais une impossibilité : aucune surcharge publique n'accepte une seule des
        // deux dates, aucune ne les accepte nullables (ce qui laisserait passer un null pour
        // l'une), et rien d'autre que ces surcharges ne construit un évènement.
        var overloads = typeof(Event).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                     .Where(method => method.Name == nameof(Event.Create))
                                     .ToArray();

        overloads.Should().NotBeEmpty();

        foreach (var overload in overloads)
        {
            var windowParameters = overload.GetParameters()
                                           .Where(parameter => parameter.Name!.StartsWith("orderWindow", StringComparison.Ordinal))
                                           .ToArray();

            windowParameters.Length.Should().BeOneOf(0, 2);
            windowParameters.Should().OnlyContain(parameter => parameter.ParameterType == typeof(DateOnly));
        }

        typeof(Event).GetConstructors(BindingFlags.Public | BindingFlags.Instance).Should().BeEmpty();
        typeof(Event).GetProperty(nameof(Event.OrderWindowStartDate))!.SetMethod.Should().BeNull();
        typeof(Event).GetProperty(nameof(Event.OrderWindowEndDate))!.SetMethod.Should().BeNull();
    }

    [Fact]
    public void Create_AlwaysYieldsEitherBothOrderWindowDatesOrNeither()
    {
        // EV-W2, vérifié sur l'état produit et non plus sur les signatures : quelle que soit la
        // surcharge empruntée, les deux dates sont présentes ensemble ou absentes ensemble.
        var withWindow = Event.Create(Name, EventDate, Today, new DateOnly(2026, 6, 15), new DateOnly(2026, 6, 20));
        var withoutWindow = Event.Create("Barbecue", EventDate, Today);

        (withWindow.OrderWindowStartDate is null).Should().Be(withWindow.OrderWindowEndDate is null);
        (withoutWindow.OrderWindowStartDate is null).Should().Be(withoutWindow.OrderWindowEndDate is null);
    }

    [Fact]
    public void Create_WithOrderWindowEndingOnTheEventDate_Succeeds()
    {
        // EV-W3 : borne exacte — pour un évènement au 2026-09-12, la fenêtre
        // 2026-08-01 → 2026-09-12 est valide : fin <= date de l'évènement, dernière valeur valide.
        var @event = Event.Create(Name, EventDate, Today, new DateOnly(2026, 8, 1), new DateOnly(2026, 9, 12));

        @event.OrderWindowStartDate.Should().Be(new DateOnly(2026, 8, 1));
        @event.OrderWindowEndDate.Should().Be(new DateOnly(2026, 9, 12));
    }

    [Fact]
    public void Create_WithOrderWindowEndingAfterTheEventDate_Throws()
    {
        // EV-W3 : borne exacte — 2026-08-01 → 2026-09-13 est rejetée, première valeur invalide
        // au-delà de la date de l'évènement.
        Action act = () => Event.Create(Name, EventDate, Today, new DateOnly(2026, 8, 1), new DateOnly(2026, 9, 13));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithOrderWindowStartingAfterItsEnd_Throws()
    {
        // EV-W3 : 2026-09-01 → 2026-08-01 est rejetée — début <= fin n'est pas tenu.
        Action act = () => Event.Create(Name, EventDate, Today, new DateOnly(2026, 9, 1), new DateOnly(2026, 8, 1));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithASingleDayOrderWindow_Succeeds()
    {
        // EV-W4 : début et fin peuvent être égaux, une fenêtre d'un seul jour est valide.
        var singleDay = new DateOnly(2026, 6, 15);

        var @event = Event.Create(Name, EventDate, Today, singleDay, singleDay);

        @event.OrderWindowStartDate.Should().Be(singleDay);
        @event.OrderWindowEndDate.Should().Be(singleDay);
    }

    [Fact]
    public void Create_WithoutOrderWindow_LeavesBothDatesAbsent()
    {
        // EV-W5 : l'absence de fenêtre est un état normal — celui d'un évènement dont
        // l'ouverture des commandes n'est pas encore décidée — et non un défaut de
        // configuration : la création sans fenêtre est acceptée et ne pose aucune des deux dates.
        var @event = Event.Create(Name, EventDate, Today);

        @event.OrderWindowStartDate.Should().BeNull();
        @event.OrderWindowEndDate.Should().BeNull();
    }
}
