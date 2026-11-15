namespace AircraftMRO.Application.Features.Countries.DTOs;

public sealed record CountryListItemDto(Guid Id, string Name, string Code, bool IsActive);
