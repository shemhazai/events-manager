using EventsManager.Application.Events;
using EventsManager.Application.Orders;
using EventsManager.Infrastructure.Events;
using EventsManager.Infrastructure.Orders;
using Microsoft.Extensions.DependencyInjection;

namespace EventsManager.Infrastructure;

/// <summary>
/// Câblage DI de la couche Infrastructure. La chaîne de connexion arrive en primitif :
/// la couche ne dépend pas d'IConfiguration, c'est l'Api qui lit sa configuration.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<EventsManagerDbContext>(options => options.UseEventsManagerPostgreSql(connectionString));
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();

        return services;
    }
}
