using System.ComponentModel.DataAnnotations;
using AircraftMRO.Domain.Enums.WorkOrders;

namespace AircraftMRO.Api.Features.WorkOrders.Contracts;

/// <summary>A new work order is always <c>Open</c>; its number is assigned by the server.</summary>
public sealed record CreateWorkOrderRequest(
    [Required] Guid? AircraftId,
    [Required] string Title,
    string? Description,
    [Required] WorkOrderPriority? Priority,
    DateOnly? DueDate);

/// <summary>The aircraft cannot be changed after the work order is created.</summary>
public sealed record UpdateWorkOrderRequest(
    [Required] string Title,
    string? Description,
    [Required] WorkOrderPriority? Priority,
    [Required] WorkOrderStatus? Status,
    DateOnly? DueDate);
