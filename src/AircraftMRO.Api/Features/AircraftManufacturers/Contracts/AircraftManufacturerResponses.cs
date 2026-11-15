using AircraftMRO.Application.Features.AircraftManufacturers.DTOs;

namespace AircraftMRO.Api.Features.AircraftManufacturers.Contracts;

/// <summary>The country values are null only for a manufacturer carried over without one.</summary>
public sealed record AircraftManufacturerResponse(
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
    string? UpdatedBy)
{
    public static AircraftManufacturerResponse From(AircraftManufacturerDto dto) => new(
        dto.Id, dto.Name, dto.Code, dto.CountryId, dto.CountryName, dto.CountryCode, dto.IsActive,
        dto.CreatedAtUtc, dto.CreatedBy, dto.UpdatedAtUtc, dto.UpdatedBy);
}

public sealed record AircraftManufacturerSummaryResponse(
    Guid Id,
    string Name,
    string Code,
    string? CountryName,
    string? CountryCode,
    bool IsActive)
{
    public static AircraftManufacturerSummaryResponse From(AircraftManufacturerListItemDto dto) => new(
        dto.Id, dto.Name, dto.Code, dto.CountryName, dto.CountryCode, dto.IsActive);
}

public sealed record AircraftManufacturerPageResponse(
    IReadOnlyList<AircraftManufacturerSummaryResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
