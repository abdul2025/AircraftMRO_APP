using AircraftMRO.Application.SystemFeatures;
using AircraftMRO.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

        services.AddDbContext<AircraftMroDbContext>(options =>
            options.UseSqlServer(connectionString));
        services.AddScoped<ISystemFeatureReader, SystemFeatureReader>();

        return services;
    }
}
