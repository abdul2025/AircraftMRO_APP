using AircraftMRO.Application.Features.Aircraft.DTOs;
using AircraftMRO.Domain.Enums.Aircraft;

namespace AircraftMRO.Api.Features.Aircraft.Contracts;

public sealed record AircraftResponse(
    Guid Id,
    string RegistrationNumber,
    string Manufacturer,
    string Model,
    string SerialNumber,
    int YearOfManufacture,
    decimal TotalFlightHours,
    AircraftStatus Status,
    DateTimeOffset CreatedAtUtc,
    string? CreatedBy,
    DateTimeOffset? UpdatedAtUtc,
    string? UpdatedBy)
{
    public static AircraftResponse From(AircraftDto dto) => new(
        dto.Id, dto.RegistrationNumber, dto.Manufacturer, dto.Model, dto.SerialNumber,
        dto.YearOfManufacture, dto.TotalFlightHours, dto.Status,
        dto.CreatedAtUtc, dto.CreatedBy, dto.UpdatedAtUtc, dto.UpdatedBy);
}

public sealed record AircraftSummaryResponse(
    Guid Id,
    string RegistrationNumber,
    string Manufacturer,
    string Model,
    int YearOfManufacture,
    decimal TotalFlightHours,
    AircraftStatus Status)
{
    public static AircraftSummaryResponse From(AircraftListItemDto dto) => new(
        dto.Id, dto.RegistrationNumber, dto.Manufacturer, dto.Model,
        dto.YearOfManufacture, dto.TotalFlightHours, dto.Status);
}

public sealed record AircraftPageResponse(
    IReadOnlyList<AircraftSummaryResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
