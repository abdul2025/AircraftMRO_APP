using AircraftMRO.Domain.Enums.Aircraft;

namespace AircraftMRO.Application.Features.Aircraft.DTOs;

public sealed record AircraftListItemDto(
    Guid Id,
    string RegistrationNumber,
    string ManufacturerName,
    string ModelName,
    int YearOfManufacture,
    decimal TotalFlightHours,
    AircraftStatus Status);
