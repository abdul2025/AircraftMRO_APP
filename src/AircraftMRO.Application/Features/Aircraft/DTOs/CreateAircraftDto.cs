using AircraftMRO.Domain.Enums.Aircraft;

namespace AircraftMRO.Application.Features.Aircraft.DTOs;

public sealed record CreateAircraftDto(
    string RegistrationNumber,
    Guid ManufacturerId,
    Guid ModelId,
    string SerialNumber,
    int YearOfManufacture,
    decimal TotalFlightHours,
    AircraftStatus Status);
