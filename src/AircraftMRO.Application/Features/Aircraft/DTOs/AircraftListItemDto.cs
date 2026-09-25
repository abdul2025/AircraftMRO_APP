using AircraftMRO.Domain.Enums.Aircraft;

namespace AircraftMRO.Application.Features.Aircraft.DTOs;

public sealed record AircraftListItemDto(
    Guid Id,
    string RegistrationNumber,
    string Manufacturer,
    string Model,
    int YearOfManufacture,
    decimal TotalFlightHours,
    AircraftStatus Status);
