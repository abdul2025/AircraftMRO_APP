using AircraftMRO.Infrastructure;
using AircraftMRO.Infrastructure.Identity;
using AircraftMRO.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

namespace AircraftMRO.Tests;

public sealed class IdentitySchemaFixture : IAsyncLifetime
{
    public MsSqlContainer Container { get; } = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public async Task InitializeAsync()
    {
        await Container.StartAsync();

        var options = new DbContextOptionsBuilder<AircraftMroDbContext>()
            .UseSqlServer(Container.GetConnectionString())
            .Options;
        await using var context = new AircraftMroDbContext(options);
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Container.DisposeAsync().AsTask();
}

public sealed class IdentitySchemaIntegrationTests(IdentitySchemaFixture fixture)
    : IClassFixture<IdentitySchemaFixture>
{
    [Fact]
    public async Task CreateAsync_ThenFindByEmailAsync_RoundTripsTheUserAgainstTheRealProvider()
    {
        var userManager = BuildServices().GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser
        {
            UserName = "integration@example.com",
            Email = "integration@example.com"
        };
        var createResult = await userManager.CreateAsync(user, "Password1!");
        Assert.True(createResult.Succeeded, string.Join(';', createResult.Errors.Select(e => e.Description)));

        var found = await userManager.FindByEmailAsync("integration@example.com");

        Assert.NotNull(found);
        Assert.Equal(user.Id, found!.Id);
    }

    [Fact]
    public async Task RoleManager_CreateAsync_PersistsTheRoleAgainstTheRealProvider()
    {
        var roleManager = BuildServices().GetRequiredService<RoleManager<ApplicationRole>>();
        var roleName = $"{ApplicationRoles.Administrator}-{Guid.NewGuid():N}";

        var createResult = await roleManager.CreateAsync(new ApplicationRole { Name = roleName });
        Assert.True(createResult.Succeeded);

        Assert.True(await roleManager.RoleExistsAsync(roleName));
    }

    private IServiceProvider BuildServices()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AircraftMRO"] = fixture.Container.GetConnectionString()
            })
            .Build();
        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);

        return services.BuildServiceProvider().CreateScope().ServiceProvider;
    }
}
