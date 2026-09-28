using AircraftMRO.Domain.Enums.WorkOrders;

namespace AircraftMRO.Application.Features.WorkOrders.DTOs;

/// <summary>The aircraft is fixed when a work order is created and cannot be changed.</summary>
public sealed record UpdateWorkOrderDto(
    string Title,
    string? Description,
    WorkOrderPriority Priority,
    WorkOrderStatus Status,
    DateOnly? DueDate,
    byte[] RowVersion);
