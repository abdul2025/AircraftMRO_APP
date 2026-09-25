extern alias ApiHost;

using AircraftMRO.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AircraftMRO.Tests.Support;

public sealed class WebHostFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting($"ConnectionStrings:{DependencyInjection.ConnectionStringName}", connectionString);
    }
}

public sealed class ApiHostFactory(string connectionString, string environment = "Testing")
    : WebApplicationFactory<ApiHost::Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.UseSetting($"ConnectionStrings:{DependencyInjection.ConnectionStringName}", connectionString);
    }
}
