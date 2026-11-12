using AircraftMRO.Domain.Enums.WorkOrders;

namespace AircraftMRO.Application.Features.WorkOrders.DTOs;

public sealed record WorkOrderDto(
    Guid Id,
    Guid AircraftId,
    string AircraftRegistration,
    string Number,
    string Title,
    string? Description,
    WorkOrderPriority Priority,
    WorkOrderStatus Status,
    DateOnly? DueDate,
    bool IsOverdue,
    DateTimeOffset? CompletedAtUtc,
    int MaintenanceCount,
    int CompletedMaintenanceCount,
    DateTimeOffset CreatedAtUtc,
    string? CreatedBy,
    DateTimeOffset? UpdatedAtUtc,
    string? UpdatedBy,
    byte[] RowVersion);
