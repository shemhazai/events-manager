namespace EventsManager.Application;

/// <summary>
/// Point de vérité unique de la définition métier d'« aujourd'hui » : la date du jour dans le
/// fuseau des évènements, <b>fixé en dur</b> et jamais celui de la machine hôte.
/// <para>
/// Motif de ce choix (décision du 2026-10-12) : la fenêtre de commande devient visible sur une
/// page publique. Sous un hébergement en UTC, un « aujourd'hui » pris sur la machine ouvrirait
/// et fermerait la fenêtre deux heures en retard l'été, en silence. La journée métier ne doit
/// pas dépendre de la configuration de l'hôte.
/// </para>
/// </summary>
public static class TimeProviderExtensions
{
    /// <summary>
    /// Fuseau des évènements, en identifiant IANA. Accepté sur Windows comme sur Linux depuis
    /// .NET 6 (résolution ICU), et mis en cache par le runtime depuis .NET 8 — d'où le champ
    /// statique plutôt qu'une résolution à chaque appel.
    /// Contrainte d'hébergement, mesurée le 2026-10-18 : sous Linux, la résolution lit la base
    /// tzdata du système, dans /usr/share/zoneinfo. Une image d'exécution qui ne l'embarque pas
    /// (variantes aspnet chiseled, alpine, distroless) fait échouer ce champ statique au premier
    /// appel, et toute route qui lit la date du jour rend un 500 — pendant que le conteneur, lui,
    /// reste « Up ». D'où l'image complète mcr.microsoft.com/dotnet/aspnet:10.0 pour le service
    /// « api » du docker-compose. Autre limite, distincte : la conversion croisée d'identifiants
    /// (IANA sous Windows, identifiants Windows sous Unix) est indisponible en mode globalization
    /// invariant comme en mode NLS, que ce dépôt n'active ni l'un ni l'autre.
    /// </summary>
    private static readonly TimeZoneInfo EventTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Brussels");

    public static DateOnly TodayLocal(this TimeProvider timeProvider)
    {
        // GetUtcNow() et non GetLocalNow() : le fuseau local du TimeProvider n'a aucun rôle
        // dans la définition métier de la journée, et le laisser intervenir rendrait le
        // résultat dépendant de l'hôte — exactement ce que cette conversion élimine.
        var eventZoneNow = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), EventTimeZone);

        return DateOnly.FromDateTime(eventZoneNow.Date);
    }
}
