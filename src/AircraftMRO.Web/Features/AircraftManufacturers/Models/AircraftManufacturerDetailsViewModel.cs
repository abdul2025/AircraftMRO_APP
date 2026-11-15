using AircraftMRO.Application.Features.AircraftManufacturers.DTOs;

namespace AircraftMRO.Web.Features.AircraftManufacturers.Models;

public sealed record AircraftManufacturerDetailsViewModel(
    Guid Id,
    string Name,
    string Code,
    Guid? CountryId,
    string? CountryName,
    string? CountryCode,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    string? CreatedBy,
    DateTimeOffset? UpdatedAtUtc,
    string? UpdatedBy,
    string RowVersion)
{
    public string DisplayName => Name;

    public static AircraftManufacturerDetailsViewModel From(AircraftManufacturerDto dto) => new(
        dto.Id,
        dto.Name,
        dto.Code,
        dto.CountryId,
        dto.CountryName,
        dto.CountryCode,
        dto.IsActive,
        dto.CreatedAtUtc,
        dto.CreatedBy,
        dto.UpdatedAtUtc,
        dto.UpdatedBy,
        Convert.ToBase64String(dto.RowVersion));
}
