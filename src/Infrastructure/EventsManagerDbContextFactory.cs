using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EventsManager.Infrastructure;

/// <summary>
/// Factory design-time pour l'outillage dotnet-ef (migrations add / database update),
/// sans projet de démarrage. Chaîne de connexion de dev uniquement, et volontairement
/// <b>sans mot de passe</b> : ce fichier est versionné. PostgreSQL n'a pas d'équivalent de
/// l'authentification intégrée Windows, donc « migrations add » (qui n'ouvre aucune connexion)
/// fonctionne tel quel, tandis que « database update » depuis le poste réclame un mot de passe
/// — à passer par l'environnement. Le bundle de migrations, lui, remplace cette chaîne par son
/// option --connection, et le runtime passe par AddInfrastructure.
/// </summary>
public sealed class EventsManagerDbContextFactory : IDesignTimeDbContextFactory<EventsManagerDbContext>
{
    public EventsManagerDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<EventsManagerDbContext>();
        optionsBuilder.UseEventsManagerPostgreSql(
            "Host=localhost;Port=5432;Database=EventsManager;Username=postgres");

        return new EventsManagerDbContext(optionsBuilder.Options);
    }
}
