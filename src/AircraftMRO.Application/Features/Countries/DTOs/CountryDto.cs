namespace AircraftMRO.Application.Features.Countries.DTOs;

public sealed record CountryDto(
    Guid Id,
    string Name,
    string Code,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    string? CreatedBy,
    DateTimeOffset? UpdatedAtUtc,
    string? UpdatedBy,
    byte[] RowVersion);
