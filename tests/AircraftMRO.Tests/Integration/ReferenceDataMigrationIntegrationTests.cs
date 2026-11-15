using AircraftMRO.Tests.Support;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AircraftMRO.Tests.Integration;

/// <summary>
/// Moving aircraft from free-text manufacturer and model columns to reference data must keep every
/// existing aircraft, including soft-deleted ones, and must be reversible.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class ReferenceDataMigrationIntegrationTests(SqlServerFixture fixture)
{
    private const string BeforeReferenceData = "20261110091941_AddMaintenanceOrders";

    [Fact]
    public async Task Existing_aircraft_are_carried_over_and_the_migration_reverts()
    {
        var connectionString = fixture.NewDatabaseConnectionString();
        await using var context = SqlServerFixture.CreateContext(connectionString, new TestCurrentUser(), new FakeTimeProvider());
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(BeforeReferenceData);

        await context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO [Aircraft] ([Id], [RegistrationNumber], [Manufacturer], [Model], [SerialNumber],
                [YearOfManufacture], [TotalFlightHours], [Status], [CreatedAtUtc], [IsDeleted])
            VALUES
                (NEWID(), N'MG-1', N'Airbus', N'A320-214', N'S1', 2012, 0, 1, SYSDATETIMEOFFSET(), 0),
                (NEWID(), N'MG-2', N'airbus', N'a320-214', N'S2', 2013, 0, 1, SYSDATETIMEOFFSET(), 0),
                (NEWID(), N'MG-3', N'Airbus', N'A330-300', N'S3', 2014, 0, 1, SYSDATETIMEOFFSET(), 0),
                (NEWID(), N'MG-4', N'Boeing', N'737-800', N'S4', 2015, 0, 4, SYSDATETIMEOFFSET(), 1),
                (NEWID(), N'MG-5', N'Embraer', N'E190', N'S5', 2016, 0, 1, SYSDATETIMEOFFSET(), 0),
                (NEWID(), N'MG-6', N'Embraer S.A.', N'E195', N'S6', 2017, 0, 1, SYSDATETIMEOFFSET(), 0),
                (NEWID(), N'MG-7', N'Ünal Aero', N'X/1', N'S7', 2018, 0, 1, SYSDATETIMEOFFSET(), 0);
            """);

        await migrator.MigrateAsync();

        // Every aircraft, soft-deleted included, points at a manufacturer and model with its old names.
        var linked = await QueryAsync(connectionString,
            """
            SELECT a.[RegistrationNumber], m.[Name], m.[Code], c.[Code], model.[Name], model.[ModelCode], model.[AircraftCategory]
            FROM [Aircraft] a
            JOIN [AircraftManufacturers] m ON m.[Id] = a.[ManufacturerId]
            JOIN [AircraftModels] model ON model.[Id] = a.[ModelId] AND model.[ManufacturerId] = m.[Id]
            LEFT JOIN [Countries] c ON c.[Id] = m.[CountryId]
            ORDER BY a.[RegistrationNumber];
            """);
        Assert.Equal(["MG-1", "MG-2", "MG-3", "MG-4", "MG-5", "MG-6", "MG-7"], linked.Select(row => row[0]));
        Assert.All(linked, row => Assert.Equal("7", row[6])); // AircraftCategory.Other

        var byRegistration = linked.ToDictionary(row => row[0]!);
        Assert.Equal(byRegistration["MG-1"][1], byRegistration["MG-2"][1], StringComparer.OrdinalIgnoreCase);
        Assert.Equal(byRegistration["MG-1"][4], byRegistration["MG-2"][4], StringComparer.OrdinalIgnoreCase);
        Assert.Equal(("AIRBUS", "FR", "A320214"), (byRegistration["MG-1"][2], byRegistration["MG-1"][3], byRegistration["MG-1"][5]));
        Assert.Equal(("BOEING", "US"), (byRegistration["MG-4"][2], byRegistration["MG-4"][3]));
        Assert.Equal("BR", byRegistration["MG-5"][3]);
        Assert.Equal("BR", byRegistration["MG-6"][3]);
        Assert.Equal(["EMBRAER", "EMBRAER2"], new[] { byRegistration["MG-5"][2], byRegistration["MG-6"][2] }.Order());
        Assert.Equal(("MFR", null, "X1"), (byRegistration["MG-7"][2], byRegistration["MG-7"][3], byRegistration["MG-7"][5]));

        var counts = await QueryAsync(connectionString,
            "SELECT (SELECT COUNT(*) FROM [AircraftManufacturers]), (SELECT COUNT(*) FROM [AircraftModels]);");
        // Airbus, Boeing, Embraer, Embraer S.A., Ünal Aero; one model per distinct pair.
        Assert.Equal("5,6", string.Join(",", counts.Single()));

        await migrator.MigrateAsync(BeforeReferenceData);

        var restored = await QueryAsync(connectionString,
            "SELECT [RegistrationNumber], [Manufacturer], [Model] FROM [Aircraft] ORDER BY [RegistrationNumber];");
        Assert.Equal(7, restored.Count);
        Assert.Equal("Embraer S.A.", restored.Single(row => row[0] == "MG-6")[1]);
        Assert.Equal("A330-300", restored.Single(row => row[0] == "MG-3")[2]);
        Assert.Equal("Ünal Aero", restored.Single(row => row[0] == "MG-7")[1]);
        Assert.Equal("X/1", restored.Single(row => row[0] == "MG-7")[2]);
    }

    private static async Task<List<string?[]>> QueryAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var rows = new List<string?[]>();
        while (await reader.ReadAsync())
        {
            var row = new string?[reader.FieldCount];
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[i] = reader.IsDBNull(i) ? null : Convert.ToString(reader.GetValue(i), System.Globalization.CultureInfo.InvariantCulture);
            }

            rows.Add(row);
        }

        return rows;
    }
}
