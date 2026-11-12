using AircraftMRO.Domain.Enums.MaintenanceOrders;

namespace AircraftMRO.Application.Features.MaintenanceOrders.DTOs;

/// <summary>The work order is fixed when a maintenance order is created and cannot be changed.</summary>
public sealed record UpdateMaintenanceOrderDto(
    MaintenanceType Type,
    DateOnly ScheduledDate,
    MaintenanceStatus Status,
    string? Notes,
    byte[] RowVersion);
