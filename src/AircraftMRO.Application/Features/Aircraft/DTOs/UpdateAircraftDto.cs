using AircraftMRO.Domain.Enums.Aircraft;

namespace AircraftMRO.Application.Features.Aircraft.DTOs;

public sealed record UpdateAircraftDto(
    string RegistrationNumber,
    string Manufacturer,
    string Model,
    string SerialNumber,
    int YearOfManufacture,
    decimal TotalFlightHours,
    AircraftStatus Status,
    byte[] RowVersion);
