using AircraftMRO.Application.Features.WorkOrders.DTOs;
using AircraftMRO.Domain.Entities;
using AircraftMRO.Domain.Enums.WorkOrders;

namespace AircraftMRO.Tests.Support;

public static class WorkOrderTestData
{
    public static WorkOrder NewWorkOrder(
        Guid aircraftId,
        WorkOrderPriority priority = WorkOrderPriority.Medium,
        string number = "WO-000001",
        string title = "Replace brake assembly",
        DateOnly? dueDate = null) =>
        WorkOrder.Create(aircraftId, number, title, null, priority, dueDate).Value!;

    public static CreateWorkOrderDto CreateDto(Guid aircraftId, WorkOrderPriority priority = WorkOrderPriority.Medium) =>
        new(aircraftId, "Replace brake assembly", "Left main gear, position 2.", priority, new DateOnly(2026, 10, 1));

    public static UpdateWorkOrderDto UpdateDto(
        WorkOrderStatus status = WorkOrderStatus.InProgress,
        WorkOrderPriority priority = WorkOrderPriority.Medium,
        byte[]? rowVersion = null) =>
        new("Replace brake assembly", null, priority, status, new DateOnly(2026, 10, 1), rowVersion ?? [1]);
}
