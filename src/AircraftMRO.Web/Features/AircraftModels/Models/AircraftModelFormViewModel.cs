using System.ComponentModel.DataAnnotations;
using AircraftMRO.Application.Features.AircraftModels.DTOs;
using AircraftMRO.Domain.Entities;
using AircraftMRO.Domain.Enums.AircraftModels;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AircraftMRO.Web.Features.AircraftModels.Models;

/// <summary>
/// Create sets the manufacturer; edit keeps it, because aircraft rely on the pair. The
/// display-only values are reloaded by the controller, never posted.
/// </summary>
public sealed class AircraftModelFormViewModel
{
    [Display(Name = "Manufacturer")]
    public Guid? ManufacturerId { get; set; }

    [Required]
    [StringLength(AircraftModel.NameMaxLength)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Model code")]
    [StringLength(AircraftModel.ModelCodeMaxLength)]
    [RegularExpression("^[A-Za-z0-9]+(-[A-Za-z0-9]+)*$", ErrorMessage = "Use letters, digits, and single hyphens only.")]
    public string ModelCode { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Aircraft category")]
    public AircraftCategory? AircraftCategory { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    /// <summary>Base64 row version captured when the edit form was loaded.</summary>
    public string? RowVersion { get; set; }

    /// <summary>Set on edit, where the manufacturer is shown but cannot change.</summary>
    [BindNever]
    public string? ManufacturerName { get; set; }

    [BindNever]
    public IReadOnlyList<ManufacturerOptionDto> ManufacturerOptions { get; set; } = [];
}
