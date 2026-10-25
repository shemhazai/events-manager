using EventsManager.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace EventsManager.IntegrationTests;

/// <summary>
/// Un conteneur PostgreSQL (Testcontainers) partagé par tous les tests d'intégration,
/// migré une seule fois par les migrations EF réelles — la fixture prouve donc aussi
/// InitialCreate en entier (schéma app, colonnes générées STORED, index uniques qui portent
/// l'insensibilité à la casse, colonnes d'audit, trigger) sur un vrai PostgreSQL.
/// Prérequis : Docker actif.
/// </summary>
public sealed class PostgreSqlContainerFixture : IAsyncLifetime
{
    // Image épinglée explicitement, et c'est la MÊME que le service db du docker-compose :
    // un seul PostgreSQL à connaître dans le dépôt, donc un seul comportement.
    private readonly PostgreSqlContainer _container =
        new PostgreSqlBuilder("postgres:18.6").Build();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    public EventsManagerDbContext CreateContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<EventsManagerDbContext>();
        optionsBuilder.UseEventsManagerPostgreSql(_container.GetConnectionString());

        return new EventsManagerDbContext(optionsBuilder.Options);
    }
}
