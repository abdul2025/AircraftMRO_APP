using AircraftMRO.Infrastructure;
using AircraftMRO.Infrastructure.Identity;
using AircraftMRO.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AircraftMRO.Tests;

public sealed class InfrastructureIdentityRegistrationTests
{
    [Fact]
    public void AddInfrastructure_RegistersUserManagerAsScoped()
    {
        using var provider = BuildProvider();
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();

        var first = firstScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var sameScope = firstScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var otherScope = secondScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        Assert.Same(first, sameScope);
        Assert.NotSame(first, otherScope);
    }

    [Fact]
    public void AddInfrastructure_RegistersRoleManagerAsScoped()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

        Assert.NotNull(roleManager);
    }

    [Fact]
    public void AddInfrastructure_RegistersSignInManagerAsScoped()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>();

        Assert.NotNull(signInManager);
    }

    [Fact]
    public void DbContextModel_ContainsExpectedIdentitySchema()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AircraftMroDbContext>();
        var model = context.GetService<IDesignTimeModel>().Model;

        var usersEntity = Assert.Single(
            model.GetEntityTypes(),
            candidate => candidate.GetTableName() == "AspNetUsers");
        var rolesEntity = Assert.Single(
            model.GetEntityTypes(),
            candidate => candidate.GetTableName() == "AspNetRoles");

        Assert.NotNull(usersEntity.FindProperty("Email"));
        Assert.NotNull(usersEntity.FindProperty("UserName"));
        Assert.NotNull(rolesEntity.FindProperty("Name"));
    }

    private static ServiceProvider BuildProvider()
    {
        const string connectionString =
            "Server=invalid.example;Database=AircraftMROTests;Integrated Security=True;TrustServerCertificate=True";

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AircraftMRO"] = connectionString
            })
            .Build();
        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);

        return services.BuildServiceProvider();
    }
}
