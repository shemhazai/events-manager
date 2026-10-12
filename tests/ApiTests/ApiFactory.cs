using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace EventsManager.ApiTests;

/// <summary>
/// Hôte de test du pipeline HTTP réel : c'est bien Program.cs qui est exécuté, options JSON,
/// Problem JSON et ordre des middlewares compris. Deux écarts au démarrage de production, et
/// deux seulement :
/// <list type="bullet">
/// <item>une chaîne de connexion factice — EF Core n'ouvre aucune connexion au démarrage, donc
/// aucun SQL Server ni Docker n'est requis pour ces tests ;</item>
/// <item>l'environnement forcé à Production — en Development, WebApplication insère sa page
/// d'exception de développement en tête de pipeline, ce qui masque le contrat d'erreur qu'on
/// vérifie ici (docs/api-conventions.md, section 4.5).</item>
/// </list>
/// L'assembly de test est enregistrée comme ApplicationPart pour que le contrôleur de sonde
/// soit routé par le vrai MapControllers, sans qu'aucun contrôleur n'existe dans src/.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting("ConnectionStrings:EventsManager", "Server=(unused);Database=EventsManagerApiTests");

        builder.ConfigureTestServices(services =>
        {
            services.AddControllers().AddApplicationPart(typeof(ProbeController).Assembly);
        });
    }
}
