namespace AircraftMRO.Application.Features.Countries.DTOs;

public sealed record CreateCountryDto(string Name, string Code, bool IsActive);
