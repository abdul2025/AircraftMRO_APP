using System.ComponentModel.DataAnnotations;
using AircraftMRO.Application.Features.WorkOrders.DTOs;
using AircraftMRO.Domain.Enums.WorkOrders;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using WorkOrderEntity = AircraftMRO.Domain.Entities.WorkOrder;

namespace AircraftMRO.Web.Features.WorkOrders.Models;

/// <summary>
/// Create sets the aircraft and always opens the work order; edit keeps the aircraft and can
/// change the status. The display-only values are reloaded by the controller, never posted.
/// </summary>
public sealed class WorkOrderFormViewModel
{
    [Display(Name = "Aircraft")]
    public Guid? AircraftId { get; set; }

    [Required]
    [StringLength(WorkOrderEntity.TitleMaxLength)]
    public string Title { get; set; } = string.Empty;

    [StringLength(WorkOrderEntity.DescriptionMaxLength)]
    [DataType(DataType.MultilineText)]
    public string? Description { get; set; }

    [Required]
    public WorkOrderPriority? Priority { get; set; } = WorkOrderPriority.Medium;

    [Required]
    public WorkOrderStatus? Status { get; set; } = WorkOrderStatus.Open;

    [Display(Name = "Due date")]
    public DateOnly? DueDate { get; set; }

    /// <summary>Base64 row version captured when the edit form was loaded.</summary>
    public string? RowVersion { get; set; }

    [BindNever]
    public string? Number { get; set; }

    [BindNever]
    public string? AircraftRegistration { get; set; }

    [BindNever]
    public IReadOnlyList<AircraftOptionDto> AircraftOptions { get; set; } = [];
}
