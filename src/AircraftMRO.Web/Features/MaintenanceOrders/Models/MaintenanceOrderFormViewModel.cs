using System.ComponentModel.DataAnnotations;
using AircraftMRO.Application.Features.MaintenanceOrders.DTOs;
using AircraftMRO.Domain.Enums.MaintenanceOrders;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using MaintenanceOrderEntity = AircraftMRO.Domain.Entities.MaintenanceOrder;

namespace AircraftMRO.Web.Features.MaintenanceOrders.Models;

/// <summary>
/// Create sets the work order and always schedules the maintenance; edit keeps the work order and
/// can change the status. The display-only values are reloaded by the controller, never posted.
/// </summary>
public sealed class MaintenanceOrderFormViewModel
{
    [Display(Name = "Work order")]
    public Guid? WorkOrderId { get; set; }

    [Required]
    [Display(Name = "Maintenance type")]
    public MaintenanceType? Type { get; set; }

    [Required]
    [Display(Name = "Scheduled date")]
    public DateOnly? ScheduledDate { get; set; }

    [Required]
    public MaintenanceStatus? Status { get; set; } = MaintenanceStatus.Scheduled;

    [StringLength(MaintenanceOrderEntity.NotesMaxLength)]
    [DataType(DataType.MultilineText)]
    public string? Notes { get; set; }

    /// <summary>Base64 row version captured when the edit form was loaded.</summary>
    public string? RowVersion { get; set; }

    [BindNever]
    public bool IsEdit { get; set; }

    [BindNever]
    public string? WorkOrderNumber { get; set; }

    [BindNever]
    public IReadOnlyList<WorkOrderOptionDto> WorkOrderOptions { get; set; } = [];
}
