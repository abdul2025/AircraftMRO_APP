using AircraftMRO.Application.Features.Aircraft.DTOs;
using AircraftMRO.Domain.Enums.Aircraft;

namespace AircraftMRO.Web.Features.Aircraft.Models;

public sealed record AircraftDetailsViewModel(
    Guid Id,
    string RegistrationNumber,
    Guid ManufacturerId,
    string ManufacturerName,
    Guid ModelId,
    string ModelName,
    string SerialNumber,
    int YearOfManufacture,
    decimal TotalFlightHours,
    AircraftStatus Status,
    DateTimeOffset CreatedAtUtc,
    string? CreatedBy,
    DateTimeOffset? UpdatedAtUtc,
    string? UpdatedBy,
    string RowVersion)
{
    public static AircraftDetailsViewModel From(AircraftDto dto) => new(
        dto.Id,
        dto.RegistrationNumber,
        dto.ManufacturerId,
        dto.ManufacturerName,
        dto.ModelId,
        dto.ModelName,
        dto.SerialNumber,
        dto.YearOfManufacture,
        dto.TotalFlightHours,
        dto.Status,
        dto.CreatedAtUtc,
        dto.CreatedBy,
        dto.UpdatedAtUtc,
        dto.UpdatedBy,
        Convert.ToBase64String(dto.RowVersion));
}
