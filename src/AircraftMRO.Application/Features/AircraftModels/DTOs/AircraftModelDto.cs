using AircraftMRO.Domain.Enums.AircraftModels;

namespace AircraftMRO.Application.Features.AircraftModels.DTOs;

public sealed record AircraftModelDto(
    Guid Id,
    Guid ManufacturerId,
    string ManufacturerName,
    string Name,
    string ModelCode,
    AircraftCategory AircraftCategory,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    string? CreatedBy,
    DateTimeOffset? UpdatedAtUtc,
    string? UpdatedBy,
    byte[] RowVersion);
