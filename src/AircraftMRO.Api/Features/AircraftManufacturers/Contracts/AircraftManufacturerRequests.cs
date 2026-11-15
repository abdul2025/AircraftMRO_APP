using System.ComponentModel.DataAnnotations;

namespace AircraftMRO.Api.Features.AircraftManufacturers.Contracts;

public sealed record CreateAircraftManufacturerRequest(
    [Required] string Name,
    [Required] string Code,
    [Required] Guid? CountryId,
    bool IsActive = true);

public sealed record UpdateAircraftManufacturerRequest(
    [Required] string Name,
    [Required] string Code,
    [Required] Guid? CountryId,
    [Required] bool? IsActive);
