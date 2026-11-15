using System.ComponentModel.DataAnnotations;
using AircraftMRO.Application.Features.AircraftManufacturers.DTOs;
using AircraftMRO.Domain.Entities;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AircraftMRO.Web.Features.AircraftManufacturers.Models;

public sealed class AircraftManufacturerFormViewModel
{
    [Required]
    [StringLength(AircraftManufacturer.NameMaxLength)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(AircraftManufacturer.CodeMaxLength)]
    [RegularExpression("^[A-Za-z0-9]+(-[A-Za-z0-9]+)*$", ErrorMessage = "Use letters, digits, and single hyphens only.")]
    public string Code { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Country")]
    public Guid? CountryId { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    /// <summary>Base64 row version captured when the edit form was loaded.</summary>
    public string? RowVersion { get; set; }

    /// <summary>Reloaded by the controller on every render, never posted.</summary>
    [BindNever]
    public IReadOnlyList<CountryOptionDto> CountryOptions { get; set; } = [];
}
