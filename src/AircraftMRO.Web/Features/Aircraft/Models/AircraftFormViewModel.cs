using System.ComponentModel.DataAnnotations;
using AircraftMRO.Domain.Enums.Aircraft;
using AircraftEntity = AircraftMRO.Domain.Entities.Aircraft;

namespace AircraftMRO.Web.Features.Aircraft.Models;

public sealed class AircraftFormViewModel
{
    [Required]
    [Display(Name = "Registration number")]
    [StringLength(AircraftEntity.RegistrationNumberMaxLength, MinimumLength = AircraftEntity.RegistrationNumberMinLength)]
    [RegularExpression("^[A-Za-z0-9]+(-[A-Za-z0-9]+)*$", ErrorMessage = "Use letters, digits, and single hyphens only.")]
    public string RegistrationNumber { get; set; } = string.Empty;

    [Required]
    [StringLength(AircraftEntity.ManufacturerMaxLength)]
    public string Manufacturer { get; set; } = string.Empty;

    [Required]
    [StringLength(AircraftEntity.ModelMaxLength)]
    public string Model { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Serial number (MSN)")]
    [StringLength(AircraftEntity.SerialNumberMaxLength)]
    public string SerialNumber { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Year of manufacture")]
    [Range(AircraftEntity.FirstYearOfManufacture, 9999)]
    public int? YearOfManufacture { get; set; }

    [Required]
    [Display(Name = "Total flight hours")]
    [Range(typeof(decimal), "0", "99999999.9", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal? TotalFlightHours { get; set; }

    [Required]
    public AircraftStatus? Status { get; set; } = AircraftStatus.Active;

    /// <summary>Base64 row version captured when the edit form was loaded.</summary>
    public string? RowVersion { get; set; }
}
