using AircraftMRO.Application.Features.Employees;
using AircraftMRO.Application.Features.Employees.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace AircraftMRO.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IEmployeeService, EmployeeService>();

        return services;
    }
}
