using AircraftMRO.Application.Features.Employees.Ports;
using AircraftMRO.Infrastructure.Identity;
using AircraftMRO.Infrastructure.Persistence;
using AircraftMRO.Infrastructure.Persistence.Features.Employees;
using Microsoft.AspNetCore.Identity;
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

        services.AddDataProtection();
        services.AddAuthentication();

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.SignIn.RequireConfirmedAccount = true;
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<AircraftMroDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders()
            .AddErrorDescriber<AircraftMroIdentityErrorDescriber>();

        services.Configure<SecurityStampValidatorOptions>(options =>
            options.ValidationInterval = TimeSpan.FromMinutes(5));

        services.AddScoped<IEmployeeRepository, EmployeeRepository>();

        return services;
    }
}
