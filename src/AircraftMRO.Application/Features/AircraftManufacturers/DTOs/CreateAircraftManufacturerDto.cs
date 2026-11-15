namespace AircraftMRO.Application.Features.AircraftManufacturers.DTOs;

public sealed record CreateAircraftManufacturerDto(string Name, string Code, Guid CountryId, bool IsActive);
