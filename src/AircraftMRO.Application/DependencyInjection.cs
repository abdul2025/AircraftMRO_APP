using AircraftMRO.Application.Features.Aircraft;
using AircraftMRO.Application.Features.Aircraft.Interfaces;
using AircraftMRO.Application.Features.Notifications;
using AircraftMRO.Application.Features.Notifications.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AircraftMRO.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IAircraftService, AircraftService>();
        services.AddScoped<INotificationService, NotificationService>();

        return services;
    }
}
