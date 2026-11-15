using AircraftMRO.Application.Features.Countries.DTOs;

namespace AircraftMRO.Web.Features.Countries.Models;

public sealed record CountryDetailsViewModel(
    Guid Id,
    string Name,
    string Code,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    string? CreatedBy,
    DateTimeOffset? UpdatedAtUtc,
    string? UpdatedBy,
    string RowVersion)
{
    public string DisplayName => Name;

    public static CountryDetailsViewModel From(CountryDto dto) => new(
        dto.Id,
        dto.Name,
        dto.Code,
        dto.IsActive,
        dto.CreatedAtUtc,
        dto.CreatedBy,
        dto.UpdatedAtUtc,
        dto.UpdatedBy,
        Convert.ToBase64String(dto.RowVersion));
}
