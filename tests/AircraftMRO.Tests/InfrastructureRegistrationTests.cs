using AircraftMRO.Application.SystemFeatures;
using AircraftMRO.Infrastructure;
using AircraftMRO.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AircraftMRO.Tests;

public sealed class InfrastructureRegistrationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddInfrastructure_WhenConnectionStringIsMissing_ThrowsSafeError(
        string? connectionString)
    {
        var configurationValues = new Dictionary<string, string?>();
        if (connectionString is not null)
        {
            configurationValues["ConnectionStrings:AircraftMRO"] = connectionString;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddInfrastructure(configuration));

        Assert.Equal(
            "Required configuration 'ConnectionStrings:AircraftMRO' is missing.",
            exception.Message);
    }

    [Fact]
    public void AddInfrastructure_ConfiguresSqlServerWithoutOpeningAConnection()
    {
        var services = CreateServices();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AircraftMroDbContext>();

        Assert.Equal("Microsoft.EntityFrameworkCore.SqlServer", context.Database.ProviderName);
    }

    [Fact]
    public void AddInfrastructure_RegistersDbContextWithScopedLifetime()
    {
        var services = CreateServices();

        using var provider = services.BuildServiceProvider();
        AircraftMroDbContext firstScopeContext;

        using (var firstScope = provider.CreateScope())
        {
            firstScopeContext = firstScope.ServiceProvider
                .GetRequiredService<AircraftMroDbContext>();
            var sameScopeContext = firstScope.ServiceProvider
                .GetRequiredService<AircraftMroDbContext>();

            Assert.Same(firstScopeContext, sameScopeContext);
        }

        using var secondScope = provider.CreateScope();
        var secondScopeContext = secondScope.ServiceProvider
            .GetRequiredService<AircraftMroDbContext>();

        Assert.NotSame(firstScopeContext, secondScopeContext);
    }

    [Fact]
    public void AddInfrastructure_RegistersSystemFeatureReaderAsScoped()
    {
        var services = CreateServices();

        using var provider = services.BuildServiceProvider();
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();

        var first = firstScope.ServiceProvider.GetRequiredService<ISystemFeatureReader>();
        var sameScope = firstScope.ServiceProvider.GetRequiredService<ISystemFeatureReader>();
        var otherScope = secondScope.ServiceProvider.GetRequiredService<ISystemFeatureReader>();

        Assert.Same(first, sameScope);
        Assert.NotSame(first, otherScope);
    }

    [Fact]
    public void DbContextModel_ContainsExpectedSystemFeaturesSchema()
    {
        var services = CreateServices();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AircraftMroDbContext>();
        var model = context.GetService<IDesignTimeModel>().Model;
        var entity = Assert.Single(
            model.GetEntityTypes(),
            candidate => candidate.GetTableName() == "SystemFeatures");

        Assert.NotNull(entity.FindProperty("Code"));
        Assert.NotNull(entity.FindProperty("DisplayOrder"));
        Assert.NotNull(entity.FindProperty("IsVisible"));
        Assert.Contains(
            entity.GetIndexes(),
            index => index.IsUnique &&
                index.Properties.Single().Name == "Code");
        Assert.Equal(4, entity.GetSeedData().Count());
    }

    private static ServiceCollection CreateServices()
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

        return services;
    }
}
