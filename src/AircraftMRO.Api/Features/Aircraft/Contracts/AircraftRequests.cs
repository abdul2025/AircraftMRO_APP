using System.ComponentModel.DataAnnotations;
using AircraftMRO.Domain.Enums.Aircraft;

namespace AircraftMRO.Api.Features.Aircraft.Contracts;

public sealed record CreateAircraftRequest(
    [Required] string RegistrationNumber,
    [Required] string Manufacturer,
    [Required] string Model,
    [Required] string SerialNumber,
    [Required] int? YearOfManufacture,
    [Required] decimal? TotalFlightHours,
    [Required] AircraftStatus? Status);

public sealed record UpdateAircraftRequest(
    [Required] string RegistrationNumber,
    [Required] string Manufacturer,
    [Required] string Model,
    [Required] string SerialNumber,
    [Required] int? YearOfManufacture,
    [Required] decimal? TotalFlightHours,
    [Required] AircraftStatus? Status);
