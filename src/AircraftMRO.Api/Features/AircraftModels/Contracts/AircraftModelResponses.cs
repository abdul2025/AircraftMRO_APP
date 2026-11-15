using AircraftMRO.Application.Features.AircraftModels.DTOs;
using AircraftMRO.Domain.Enums.AircraftModels;

namespace AircraftMRO.Api.Features.AircraftModels.Contracts;

public sealed record AircraftModelResponse(
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
    string? UpdatedBy)
{
    public static AircraftModelResponse From(AircraftModelDto dto) => new(
        dto.Id, dto.ManufacturerId, dto.ManufacturerName, dto.Name, dto.ModelCode, dto.AircraftCategory, dto.IsActive,
        dto.CreatedAtUtc, dto.CreatedBy, dto.UpdatedAtUtc, dto.UpdatedBy);
}

public sealed record AircraftModelSummaryResponse(
    Guid Id,
    Guid ManufacturerId,
    string ManufacturerName,
    string Name,
    string ModelCode,
    AircraftCategory AircraftCategory,
    bool IsActive)
{
    public static AircraftModelSummaryResponse From(AircraftModelListItemDto dto) => new(
        dto.Id, dto.ManufacturerId, dto.ManufacturerName, dto.Name, dto.ModelCode, dto.AircraftCategory, dto.IsActive);
}

public sealed record AircraftModelPageResponse(
    IReadOnlyList<AircraftModelSummaryResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
