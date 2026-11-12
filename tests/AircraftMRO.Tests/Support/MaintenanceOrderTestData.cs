using AircraftMRO.Application.Features.MaintenanceOrders.DTOs;
using AircraftMRO.Domain.Entities;
using AircraftMRO.Domain.Enums.MaintenanceOrders;

namespace AircraftMRO.Tests.Support;

public static class MaintenanceOrderTestData
{
    public static readonly DateOnly ScheduledDate = new(2026, 10, 1);

    public static MaintenanceOrder NewMaintenanceOrder(
        Guid workOrderId,
        MaintenanceType type = MaintenanceType.ACheck,
        DateOnly? scheduledDate = null) =>
        MaintenanceOrder.Create(workOrderId, type, scheduledDate ?? ScheduledDate, null).Value!;

    public static CreateMaintenanceOrderDto CreateDto(Guid workOrderId, MaintenanceType type = MaintenanceType.ACheck) =>
        new(workOrderId, type, ScheduledDate, "Routine check.");

    public static UpdateMaintenanceOrderDto UpdateDto(
        MaintenanceStatus status = MaintenanceStatus.InProgress,
        MaintenanceType type = MaintenanceType.ACheck,
        byte[]? rowVersion = null) =>
        new(type, ScheduledDate, status, null, rowVersion ?? [1]);
}
