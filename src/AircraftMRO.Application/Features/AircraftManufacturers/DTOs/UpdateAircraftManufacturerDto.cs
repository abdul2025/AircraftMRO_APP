namespace AircraftMRO.Application.Features.AircraftManufacturers.DTOs;

public sealed record UpdateAircraftManufacturerDto(
    string Name,
    string Code,
    Guid CountryId,
    bool IsActive,
    byte[] RowVersion);
