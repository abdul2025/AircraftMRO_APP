using System.ComponentModel.DataAnnotations;
using AircraftMRO.Domain.Enums.AircraftModels;

namespace AircraftMRO.Api.Features.AircraftModels.Contracts;

public sealed record CreateAircraftModelRequest(
    [Required] Guid? ManufacturerId,
    [Required] string Name,
    [Required] string ModelCode,
    [Required] AircraftCategory? AircraftCategory,
    bool IsActive = true);

/// <summary>The manufacturer is fixed when the model is created, so an update does not carry it.</summary>
public sealed record UpdateAircraftModelRequest(
    [Required] string Name,
    [Required] string ModelCode,
    [Required] AircraftCategory? AircraftCategory,
    [Required] bool? IsActive);
