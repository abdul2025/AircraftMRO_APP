using AircraftMRO.Application.Common.Interfaces;
using AircraftMRO.Infrastructure.Auditing;
using AircraftMRO.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace AircraftMRO.Tests.Support;

/// <summary>One SQL Server container per test run; each test class migrates its own database.</summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container =
        new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public async Task<string> CreateDatabaseAsync()
    {
        var connectionString = new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = $"AircraftMRO_Test_{Guid.NewGuid():N}"
        }.ConnectionString;

        await using var context = CreateContext(connectionString, new TestCurrentUser(), new FakeTimeProvider());
        await context.Database.MigrateAsync();

        return connectionString;
    }

    public static AircraftMroDbContext CreateContext(
        string connectionString,
        ICurrentUser currentUser,
        TimeProvider timeProvider) =>
        new(new DbContextOptionsBuilder<AircraftMroDbContext>()
            .UseSqlServer(connectionString)
            .AddInterceptors(new AuditableEntityInterceptor(currentUser, timeProvider))
            .Options);
}

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SqlServer";
}
