namespace AircraftMRO.Application.Features.AircraftManufacturers.DTOs;

/// <summary>The country values are null only for a manufacturer carried over without one.</summary>
public sealed record AircraftManufacturerDto(
    Guid Id,
    string Name,
    string Code,
    Guid? CountryId,
    string? CountryName,
    string? CountryCode,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    string? CreatedBy,
    DateTimeOffset? UpdatedAtUtc,
    string? UpdatedBy,
    byte[] RowVersion);
