using AircraftMRO.Application.Features.Employees.Ports;
using AircraftMRO.Domain.Entities;
using AircraftMRO.Infrastructure;
using AircraftMRO.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

namespace AircraftMRO.Tests;

public sealed class EmployeeRepositoryFixture : IAsyncLifetime
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

public sealed class EmployeeRepositoryIntegrationTests(EmployeeRepositoryFixture fixture)
    : IClassFixture<EmployeeRepositoryFixture>
{
    [Fact]
    public async Task AddAsync_ThenExistsByEmployeeIdAsync_RoundTripsTheEmployee()
    {
        var repository = BuildServices().GetRequiredService<IEmployeeRepository>();
        var employeeId = Random.Shared.Next(1, int.MaxValue);

        await repository.AddAsync(new Employee(Guid.NewGuid().ToString(), employeeId), CancellationToken.None);

        Assert.True(await repository.ExistsByEmployeeIdAsync(employeeId, CancellationToken.None));
    }

    [Fact]
    public async Task AddAsync_WhenEmployeeIdAlreadyExists_ThrowsDueToUniqueConstraint()
    {
        var repository = BuildServices().GetRequiredService<IEmployeeRepository>();
        var employeeId = Random.Shared.Next(1, int.MaxValue);
        await repository.AddAsync(new Employee(Guid.NewGuid().ToString(), employeeId), CancellationToken.None);

        var otherRepository = BuildServices().GetRequiredService<IEmployeeRepository>();

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            otherRepository.AddAsync(new Employee(Guid.NewGuid().ToString(), employeeId), CancellationToken.None));
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
