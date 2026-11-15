using AircraftMRO.Domain.Enums.AircraftModels;

namespace AircraftMRO.Application.Features.AircraftModels.DTOs;

/// <summary>The manufacturer is fixed when the model is created, so an update does not carry it.</summary>
public sealed record UpdateAircraftModelDto(
    string Name,
    string ModelCode,
    AircraftCategory AircraftCategory,
    bool IsActive,
    byte[] RowVersion);
