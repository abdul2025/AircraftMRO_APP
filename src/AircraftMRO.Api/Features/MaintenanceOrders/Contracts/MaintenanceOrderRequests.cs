using System.ComponentModel.DataAnnotations;
using AircraftMRO.Domain.Enums.MaintenanceOrders;

namespace AircraftMRO.Api.Features.MaintenanceOrders.Contracts;

/// <summary>New maintenance is always <c>Scheduled</c>, and only under an open work order.</summary>
public sealed record CreateMaintenanceOrderRequest(
    [Required] Guid? WorkOrderId,
    [Required] MaintenanceType? Type,
    [Required] DateOnly? ScheduledDate,
    string? Notes);

/// <summary>The work order cannot be changed after the maintenance order is created.</summary>
public sealed record UpdateMaintenanceOrderRequest(
    [Required] MaintenanceType? Type,
    [Required] DateOnly? ScheduledDate,
    [Required] MaintenanceStatus? Status,
    string? Notes);
