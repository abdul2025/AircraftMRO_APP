namespace AircraftMRO.Application.Features.Aircraft.DTOs;

/// <summary>A manufacturer an aircraft can be recorded against.</summary>
public sealed record AircraftManufacturerOptionDto(Guid Id, string Name, bool IsActive);
