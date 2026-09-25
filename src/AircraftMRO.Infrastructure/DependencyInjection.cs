using AircraftMRO.Application.Common.Interfaces;
using AircraftMRO.Application.Features.Aircraft.Ports;
using AircraftMRO.Application.Features.Notifications.Ports;
using AircraftMRO.Infrastructure.Auditing;
using AircraftMRO.Infrastructure.Persistence;
using AircraftMRO.Infrastructure.Persistence.Features.Aircraft;
using AircraftMRO.Infrastructure.Persistence.Features.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AircraftMRO.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "AircraftMRO";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Required configuration 'ConnectionStrings:{ConnectionStringName}' is missing.");
        }

        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<AuditableEntityInterceptor>();

        services.AddDbContext<AircraftMroDbContext>((serviceProvider, options) =>
            options
                .UseSqlServer(connectionString)
                .AddInterceptors(serviceProvider.GetRequiredService<AuditableEntityInterceptor>()));

        services.AddScoped<IAircraftRepository, AircraftRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();

        return services;
    }
}
