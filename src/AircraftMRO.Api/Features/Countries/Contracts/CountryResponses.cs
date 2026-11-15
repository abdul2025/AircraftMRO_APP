using AircraftMRO.Application.Features.Countries.DTOs;

namespace AircraftMRO.Api.Features.Countries.Contracts;

public sealed record CountryResponse(
    Guid Id,
    string Name,
    string Code,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    string? CreatedBy,
    DateTimeOffset? UpdatedAtUtc,
    string? UpdatedBy)
{
    public static CountryResponse From(CountryDto dto) => new(
        dto.Id, dto.Name, dto.Code, dto.IsActive,
        dto.CreatedAtUtc, dto.CreatedBy, dto.UpdatedAtUtc, dto.UpdatedBy);
}

public sealed record CountrySummaryResponse(Guid Id, string Name, string Code, bool IsActive)
{
    public static CountrySummaryResponse From(CountryListItemDto dto) => new(dto.Id, dto.Name, dto.Code, dto.IsActive);
}

public sealed record CountryPageResponse(
    IReadOnlyList<CountrySummaryResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
