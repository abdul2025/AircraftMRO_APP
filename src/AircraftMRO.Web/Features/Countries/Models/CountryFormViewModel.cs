using System.ComponentModel.DataAnnotations;
using AircraftMRO.Domain.Entities;

namespace AircraftMRO.Web.Features.Countries.Models;

public sealed class CountryFormViewModel
{
    [Required]
    [StringLength(Country.NameMaxLength)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Display(Name = "ISO code")]
    [StringLength(Country.CodeLength, MinimumLength = Country.CodeLength)]
    [RegularExpression("^[A-Za-z]{2}$", ErrorMessage = "Use the two-letter ISO country code.")]
    public string Code { get; set; } = string.Empty;

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    /// <summary>Base64 row version captured when the edit form was loaded.</summary>
    public string? RowVersion { get; set; }
}
