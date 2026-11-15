using System.ComponentModel.DataAnnotations;

namespace AircraftMRO.Api.Features.Countries.Contracts;

public sealed record CreateCountryRequest(
    [Required] string Name,
    [Required] string Code,
    bool IsActive = true);

public sealed record UpdateCountryRequest(
    [Required] string Name,
    [Required] string Code,
    [Required] bool? IsActive);
