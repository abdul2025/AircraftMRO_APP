using AircraftMRO.Application.Features.AircraftModels.DTOs;
using AircraftMRO.Domain.Enums.AircraftModels;

namespace AircraftMRO.Web.Features.AircraftModels.Models;

public sealed record AircraftModelDetailsViewModel(
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
    string RowVersion)
{
    public string DisplayName => $"{ManufacturerName} {Name}";

    public static AircraftModelDetailsViewModel From(AircraftModelDto dto) => new(
        dto.Id,
        dto.ManufacturerId,
        dto.ManufacturerName,
        dto.Name,
        dto.ModelCode,
        dto.AircraftCategory,
        dto.IsActive,
        dto.CreatedAtUtc,
        dto.CreatedBy,
        dto.UpdatedAtUtc,
        dto.UpdatedBy,
        Convert.ToBase64String(dto.RowVersion));
}
