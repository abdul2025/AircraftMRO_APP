using AircraftMRO.Domain.Enums.MaintenanceOrders;

namespace AircraftMRO.Application.Features.MaintenanceOrders.DTOs;

public sealed record MaintenanceOrderListItemDto(
    Guid Id,
    Guid WorkOrderId,
    string WorkOrderNumber,
    string AircraftRegistration,
    MaintenanceType Type,
    MaintenanceStatus Status,
    DateOnly ScheduledDate,
    bool IsOverdue,
    DateTimeOffset? CompletedAtUtc);
