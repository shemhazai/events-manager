namespace EventsManager.ApiTests;

/// <summary>
/// TimeProvider main (pas de lib de mock) : instant UTC fixe, donc « aujourd'hui » déterministe
/// quelle que soit la machine qui exécute les tests. Copie assumée de ceux d'UnitTests et
/// d'IntegrationTests — 15 lignes ne justifient pas un projet utilitaire partagé.
/// <para>
/// Aucune surcharge de <see cref="TimeProvider.LocalTimeZone"/> ici, et c'est volontaire :
/// <c>TimeProviderExtensions.TodayLocal()</c> convertit depuis UTC vers un fuseau fixé en dur,
/// le fuseau local du provider n'a donc plus aucun rôle. En surcharger un laisserait croire
/// le contraire.
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
