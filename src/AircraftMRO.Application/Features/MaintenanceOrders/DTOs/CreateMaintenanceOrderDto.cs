using AircraftMRO.Domain.Enums.MaintenanceOrders;

namespace AircraftMRO.Application.Features.MaintenanceOrders.DTOs;

public sealed record CreateMaintenanceOrderDto(
    Guid WorkOrderId,
    MaintenanceType Type,
    DateOnly ScheduledDate,
    string? Notes);
