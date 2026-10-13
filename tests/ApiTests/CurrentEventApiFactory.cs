using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using EventsManager.Application.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace EventsManager.ApiTests;

/// <summary>
/// Hôte de test du pipeline HTTP réel pour l'endpoint de l'évènement courant : c'est bien
/// Program.cs qui tourne, options JSON, Problem JSON, formateurs et ordre des middlewares
/// compris. Trois substitutions, et trois seulement :
/// <list type="bullet">
/// <item>le port de persistance, remplacé par <see cref="Repository"/> — ces tests vérifient le
/// contrat de la réponse, pas la traduction SQL du prédicat, et restent donc sans Docker ;</item>
/// <item>l'horloge, figée pour que « aujourd'hui » vaille le <b>2026-06-15</b>, la date de
/// référence de tous les exemples de docs/acceptance/event.md ;</item>
/// <item>un fournisseur de journal qui capte, pour vérifier ce que la réponse ne dit pas.</item>
/// </list>
/// L'environnement est forcé à Production pour la même raison que <see cref="ApiFactory"/> :
/// en Development, WebApplication insère sa page d'exception de développement en tête de
/// pipeline et masque le contrat d'erreur.
/// </summary>
public sealed class CurrentEventApiFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// 2026-06-15 à 12 h à Bruxelles, soit 10 h UTC en heure d'été : la conversion de
    /// TodayLocal() rend donc bien le 2026-06-15, et le test ne dépend ni du fuseau ni des
    /// réglages de la machine qui l'exécute.
    /// <para>
    /// Aucune propriété publique n'expose cette date : les tests écrivent leurs dates attendues
    /// en littéral, parce qu'elles viennent du fichier de critères et non de cet hôte. Les faire
    /// dériver d'ici affaiblirait l'oracle — l'attendu se calculerait depuis la configuration du
    /// sujet testé. Déplacer cet instant fait donc tomber les tests, et c'est le signal voulu.
    /// </para>
    /// </summary>
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 6, 15, 10, 0, 0, TimeSpan.Zero);

    internal StubEventRepository Repository { get; } = new();

    internal CapturingLoggerProvider Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting("ConnectionStrings:EventsManager", "Server=(unused);Database=EventsManagerApiTests");

        builder.ConfigureTestServices(services =>
        {
            // Retrait explicite plutôt qu'un simple réenregistrement : la dernière inscription
            // gagnerait à la résolution, mais rien ne le dirait à la lecture — et le
            // EventRepository d'origine resterait résoluble par erreur.
            services.RemoveAll<IEventRepository>();
            services.AddScoped<IEventRepository>(_ => Repository);

            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(FixedUtcNow));

            services.AddSingleton<ILoggerProvider>(Logs);
        });
    }
}
