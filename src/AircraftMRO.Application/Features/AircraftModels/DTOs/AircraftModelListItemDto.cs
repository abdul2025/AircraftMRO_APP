using AircraftMRO.Domain.Enums.AircraftModels;

namespace AircraftMRO.Application.Features.AircraftModels.DTOs;

public sealed record AircraftModelListItemDto(
    Guid Id,
    Guid ManufacturerId,
    string ManufacturerName,
    string Name,
    string ModelCode,
    AircraftCategory AircraftCategory,
    bool IsActive);
