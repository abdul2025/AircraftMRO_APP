using AircraftMRO.Application.Features.Aircraft.DTOs;
using AircraftMRO.Domain.Entities;
using AircraftMRO.Domain.Enums.Aircraft;

namespace AircraftMRO.Tests.Support;

public static class AircraftTestData
{
    public const int CurrentYear = 2026;

    public static Aircraft NewAircraft(string registration = "HZ-ABC", string serial = "5123") =>
        Aircraft.Create(registration, "Airbus", "A320-214", serial, 2012, 31250.5m, AircraftStatus.Active, CurrentYear).Value!;

    public static CreateAircraftDto CreateDto(string registration = "HZ-ABC", string serial = "5123") =>
        new(registration, "Airbus", "A320-214", serial, 2012, 31250.5m, AircraftStatus.Active);

    public static string UniqueRegistration() => $"T-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
}
