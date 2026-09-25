using AircraftMRO.Domain.Enums.Aircraft;

namespace AircraftMRO.Application.Features.Aircraft.DTOs;

public sealed record AircraftDto(
    Guid Id,
    string RegistrationNumber,
    string Manufacturer,
    string Model,
    string SerialNumber,
    int YearOfManufacture,
    decimal TotalFlightHours,
    AircraftStatus Status,
    DateTimeOffset CreatedAtUtc,
    string? CreatedBy,
    DateTimeOffset? UpdatedAtUtc,
    string? UpdatedBy,
    byte[] RowVersion);
