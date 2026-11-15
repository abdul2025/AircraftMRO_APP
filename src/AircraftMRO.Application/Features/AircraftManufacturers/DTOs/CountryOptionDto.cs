namespace AircraftMRO.Application.Features.AircraftManufacturers.DTOs;

/// <summary>A country a manufacturer can be recorded against.</summary>
public sealed record CountryOptionDto(Guid Id, string Name, string Code, bool IsActive);
