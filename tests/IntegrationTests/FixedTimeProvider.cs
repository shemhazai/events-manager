namespace EventsManager.IntegrationTests;

/// <summary>
/// TimeProvider main figé (pas de lib de mock) : instant UTC fixe, donc « aujourd'hui » est
/// déterministe quelle que soit la machine qui exécute les tests.
/// <para>
/// Aucune surcharge de <see cref="TimeProvider.LocalTimeZone"/>, et c'est volontaire :
/// <c>TimeProviderExtensions.TodayLocal()</c> convertit depuis UTC vers un fuseau fixé en dur
/// (Europe/Brussels), le fuseau local du provider n'a donc plus aucun rôle. En surcharger un
/// laisserait croire le contraire.
/// </para>
/// </summary>
internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    private readonly DateTimeOffset _utcNow = utcNow;

    public override DateTimeOffset GetUtcNow()
    {
        return _utcNow;
    }
}
