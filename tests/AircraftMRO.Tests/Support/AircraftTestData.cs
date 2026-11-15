using AircraftMRO.Application.Features.Aircraft.DTOs;
using AircraftMRO.Domain.Entities;
using AircraftMRO.Domain.Enums.Aircraft;
using Microsoft.EntityFrameworkCore;

namespace AircraftMRO.Tests.Support;

public static class AircraftTestData
{
    public const int CurrentYear = 2026;

    /// <summary>Airbus, seeded into every test database by <see cref="SeedReferenceDataAsync"/>.</summary>
    public static readonly Guid ManufacturerId = Guid.Parse("0f0c3a1e-0000-4000-8000-000000000001");

    /// <summary>The A320-214, one of <see cref="ManufacturerId"/>'s models.</summary>
    public static readonly Guid ModelId = Guid.Parse("0f0c3a1e-0000-4000-8000-000000000002");

    /// <summary>Boeing, a second seeded manufacturer.</summary>
    public static readonly Guid OtherManufacturerId = Guid.Parse("0f0c3a1e-0000-4000-8000-000000000003");

    /// <summary>The 737-800, one of <see cref="OtherManufacturerId"/>'s models.</summary>
    public static readonly Guid OtherModelId = Guid.Parse("0f0c3a1e-0000-4000-8000-000000000004");

    public static Aircraft NewAircraft(string registration = "HZ-ABC", string serial = "5123") =>
        Aircraft.Create(registration, ManufacturerId, ModelId, serial, 2012, 31250.5m, AircraftStatus.Active, CurrentYear).Value!;

    public static CreateAircraftDto CreateDto(string registration = "HZ-ABC", string serial = "5123") =>
        new(registration, ManufacturerId, ModelId, serial, 2012, 31250.5m, AircraftStatus.Active);

    public static AircraftModelReference ModelReference(
        Guid? modelId = null,
        Guid? manufacturerId = null,
        bool isActive = true,
        bool manufacturerIsActive = true) =>
        new(modelId ?? ModelId, manufacturerId ?? ManufacturerId, isActive, manufacturerIsActive);

    public static string UniqueRegistration() => $"T-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

    /// <summary>
    /// Inserts the fixed manufacturers and models above, with their countries from the migration's
    /// seed, so aircraft built by this class satisfy the foreign keys.
    /// </summary>
    public static Task SeedReferenceDataAsync(DbContext context) =>
        context.Database.ExecuteSqlRawAsync(
            """
            DECLARE @now datetimeoffset = SYSDATETIMEOFFSET();
            INSERT INTO [AircraftManufacturers] ([Id], [Name], [Code], [CountryId], [IsActive], [CreatedAtUtc], [IsDeleted])
            VALUES
                ({0}, N'Airbus', N'AIB', (SELECT [Id] FROM [Countries] WHERE [Code] = N'FR'), 1, @now, 0),
                ({2}, N'Boeing', N'BOE', (SELECT [Id] FROM [Countries] WHERE [Code] = N'US'), 1, @now, 0);
            INSERT INTO [AircraftModels] ([Id], [ManufacturerId], [Name], [ModelCode], [AircraftCategory], [IsActive], [CreatedAtUtc], [IsDeleted])
            VALUES
                ({1}, {0}, N'A320-214', N'A320-214', 1, 1, @now, 0),
                ({3}, {2}, N'737-800', N'737-800', 1, 1, @now, 0);
            """,
            ManufacturerId, ModelId, OtherManufacturerId, OtherModelId);
}
