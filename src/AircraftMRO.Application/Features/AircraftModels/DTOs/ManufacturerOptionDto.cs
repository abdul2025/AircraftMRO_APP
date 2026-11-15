namespace AircraftMRO.Application.Features.AircraftModels.DTOs;

/// <summary>A manufacturer an aircraft model can be filtered by or created under.</summary>
public sealed record ManufacturerOptionDto(Guid Id, string Name, bool IsActive);
