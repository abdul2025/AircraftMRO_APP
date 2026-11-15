namespace AircraftMRO.Application.Features.Countries.DTOs;

public sealed record UpdateCountryDto(string Name, string Code, bool IsActive, byte[] RowVersion);
