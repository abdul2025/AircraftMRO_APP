using AircraftMRO.Domain.Enums.Aircraft;

namespace AircraftMRO.Application.Features.Aircraft.DTOs;

public sealed record AircraftDto(
    Guid Id,
    string RegistrationNumber,
    Guid ManufacturerId,
    string ManufacturerName,
    Guid ModelId,
    string ModelName,
    string SerialNumber,
    int YearOfManufacture,
    decimal TotalFlightHours,
    AircraftStatus Status,
    DateTimeOffset CreatedAtUtc,
    string? CreatedBy,
    DateTimeOffset? UpdatedAtUtc,
    string? UpdatedBy,
    byte[] RowVersion);
