namespace AircraftMRO.Application.Features.AircraftManufacturers.DTOs;

public sealed record AircraftManufacturerListItemDto(
    Guid Id,
    string Name,
    string Code,
    string? CountryName,
    string? CountryCode,
    bool IsActive);
