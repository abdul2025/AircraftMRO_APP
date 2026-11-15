using AircraftMRO.Domain.Enums.AircraftModels;

namespace AircraftMRO.Application.Features.AircraftModels.DTOs;

public sealed record CreateAircraftModelDto(
    Guid ManufacturerId,
    string Name,
    string ModelCode,
    AircraftCategory AircraftCategory,
    bool IsActive);
