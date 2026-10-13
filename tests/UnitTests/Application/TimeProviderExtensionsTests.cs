using System.Globalization;
using EventsManager.Application;

namespace EventsManager.UnitTests.Application;

/// <summary>
/// Point de vérité unique de la définition métier d'« aujourd'hui » (convention d'horloge de
/// docs/acceptance/event.md, décision du 2026-10-12) : la date du jour dans un fuseau
/// <b>fixé en dur</b>, Europe/Brussels, et jamais celui de la machine hôte.
/// <para>
/// Ces attendus décrivent la décision, pas le comportement antérieur : avant le 2026-10-12,
/// <c>TodayLocal()</c> lisait le fuseau de la machine et le premier cas ci-dessous rendait
/// <c>2026-07-13</c> sous un hébergement en UTC — soit une fenêtre de commande ouverte et
/// fermée deux heures en retard, en silence.
/// </para>
/// </summary>
public class TimeProviderExtensionsTests
{
    [Theory]
    // Heure d'été (UTC+2) : 23 h 30 UTC est déjà le lendemain à Bruxelles.
    [InlineData("2026-07-13T23:30:00Z", "2026-07-14")]
    // Même instant de la journée en heure d'hiver (UTC+1) : 23 h 30 UTC bascule aussi,
    // mais 22 h 30 UTC non — ce qui ne serait pas le cas avec un décalage fixe de deux heures.
    [InlineData("2026-01-15T23:30:00Z", "2026-01-16")]
    [InlineData("2026-01-15T22:30:00Z", "2026-01-15")]
    // Le même 22 h 30 UTC en été, lui, bascule : le décalage appliqué suit bien la saison.
    [InlineData("2026-07-15T22:30:00Z", "2026-07-16")]
    // Milieu de journée : aucune bascule, quel que soit le mois.
    [InlineData("2026-07-13T12:00:00Z", "2026-07-13")]
    [InlineData("2026-01-15T12:00:00Z", "2026-01-15")]
    // Minuit UTC est déjà le jour suivant à Bruxelles, hiver comme été.
    [InlineData("2026-12-05T23:00:00Z", "2026-12-06")]
    public void TodayLocal_Returns_TheDate_InTheEventTimeZone(string utcNow, string expected)
    {
        var timeProvider = new FixedTimeProvider(ParseInstant(utcNow));

        timeProvider.TodayLocal().Should().Be(DateOnly.Parse(expected, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void TodayLocal_DoesNot_DependOn_TheHostTimeZone()
    {
        // Le double n'impose aucun fuseau local : si TodayLocal() en lisait un, le résultat
        // varierait d'une machine à l'autre et ce test serait vert ici, rouge en intégration
        // continue. L'assertion porte donc sur l'instant, pas sur la machine.
        var atTheEdge = new FixedTimeProvider(ParseInstant("2026-07-13T23:30:00Z"));

        atTheEdge.TodayLocal().Should().Be(new DateOnly(2026, 7, 14));
        atTheEdge.TodayLocal().Should().NotBe(DateOnly.FromDateTime(atTheEdge.GetUtcNow().UtcDateTime));
    }

    private static DateTimeOffset ParseInstant(string utcNow)
    {
        // ParseExact plutôt que Parse : l'instant doit être lu comme UTC sans dépendre du
        // fuseau de la machine qui exécute le test.
        return DateTimeOffset.ParseExact(
            utcNow,
            "yyyy-MM-ddTHH:mm:ssZ",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
    }
}
