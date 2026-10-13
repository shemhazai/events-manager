using EventsManager.Application.Events;
using EventsManager.Application.Orders;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace EventsManager.Application;

/// <summary>
/// Câblage DI de la couche Application : chaque tranche enregistre ici ses
/// validators (singletons, sans état par requête) et ses handlers (scoped).
/// CQRS light : handlers résolus par injection DI directe dans les endpoints,
/// pas de bus. Le TimeProvider reste enregistré par le composition root (Api).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IValidator<CreateEventCommand>, CreateEventCommandValidator>();
        services.AddScoped<CreateEventCommandHandler>();
        services.AddScoped<GetEventQueryHandler>();
        services.AddScoped<GetCurrentEventQueryHandler>();

        services.AddSingleton<IValidator<CreateOrderCommand>, CreateOrderCommandValidator>();
        services.AddScoped<CreateOrderCommandHandler>();
        services.AddScoped<GetOrderQueryHandler>();
        services.AddScoped<GetOrdersByEventQueryHandler>();

        return services;
    }
}
