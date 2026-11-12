using AircraftMRO.Domain.Enums.MaintenanceOrders;

namespace AircraftMRO.Application.Features.MaintenanceOrders.DTOs;

public sealed record MaintenanceOrderDto(
    Guid Id,
    Guid WorkOrderId,
    string WorkOrderNumber,
    Guid AircraftId,
    string AircraftRegistration,
    MaintenanceType Type,
    MaintenanceStatus Status,
    DateOnly ScheduledDate,
    bool IsOverdue,
    DateTimeOffset? CompletedAtUtc,
    string? Notes,
    DateTimeOffset CreatedAtUtc,
    string? CreatedBy,
    DateTimeOffset? UpdatedAtUtc,
    string? UpdatedBy,
    byte[] RowVersion);
