using AircraftMRO.Domain.Entities;
using AircraftMRO.Domain.Enums.WorkOrders;
using AircraftMRO.Tests.Support;

namespace AircraftMRO.Tests.Domain;

public sealed class WorkOrderTests
{
    private static readonly Guid AircraftId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_opens_the_work_order_and_trims_text()
    {
        var result = WorkOrder.Create(AircraftId, " WO-000007 ", "  Replace actuator ", "  ", WorkOrderPriority.High, null);

        Assert.True(result.IsSuccess);
        var workOrder = result.Value!;
        Assert.Equal(WorkOrderStatus.Open, workOrder.Status);
        Assert.True(workOrder.IsOpen);
        Assert.Equal("WO-000007", workOrder.Number);
        Assert.Equal("WO-000007", workOrder.DisplayName);
        Assert.Equal("Replace actuator", workOrder.Title);
        Assert.Null(workOrder.Description);
        Assert.Null(workOrder.CompletedAtUtc);
    }

    [Fact]
    public void Create_rejects_missing_aircraft_number_title_and_bad_values()
    {
        AssertInvalid(WorkOrder.Create(Guid.Empty, "WO-1", "Title", null, WorkOrderPriority.Low, null).ErrorCode);
        AssertInvalid(WorkOrder.Create(AircraftId, " ", "Title", null, WorkOrderPriority.Low, null).ErrorCode);
        AssertInvalid(WorkOrder.Create(AircraftId, "WO-1", " ", null, WorkOrderPriority.Low, null).ErrorCode);
        AssertInvalid(WorkOrder.Create(AircraftId, "WO-1", new string('T', 151), null, WorkOrderPriority.Low, null).ErrorCode);
        AssertInvalid(WorkOrder.Create(AircraftId, "WO-1", "Title", new string('D', 2001), WorkOrderPriority.Low, null).ErrorCode);
        AssertInvalid(WorkOrder.Create(AircraftId, "WO-1", "Title", null, (WorkOrderPriority)99, null).ErrorCode);
    }

    [Fact]
    public void Completing_stamps_completion_time_and_closes_the_work_order()
    {
        var workOrder = WorkOrderTestData.NewWorkOrder(AircraftId);

        var result = workOrder.Update("Replace brake assembly", null, WorkOrderPriority.Medium, WorkOrderStatus.Completed, null, Now);

        Assert.True(result.IsSuccess);
        Assert.False(workOrder.IsOpen);
        Assert.Equal(Now, workOrder.CompletedAtUtc);
    }

    [Theory]
    [InlineData(WorkOrderStatus.Completed)]
    [InlineData(WorkOrderStatus.Cancelled)]
    public void Closed_work_orders_can_no_longer_change(WorkOrderStatus closedStatus)
    {
        var workOrder = WorkOrderTestData.NewWorkOrder(AircraftId);
        workOrder.Update("Replace brake assembly", null, WorkOrderPriority.Medium, closedStatus, null, Now);

        var result = workOrder.Update("Reopened", null, WorkOrderPriority.Critical, WorkOrderStatus.Open, null, Now);

        Assert.Equal(WorkOrder.ClosedErrorCode, result.ErrorCode);
        Assert.Equal(closedStatus, workOrder.Status);
        Assert.Equal("Replace brake assembly", workOrder.Title);
    }

    [Fact]
    public void Failed_update_leaves_work_order_unchanged()
    {
        var workOrder = WorkOrderTestData.NewWorkOrder(AircraftId, WorkOrderPriority.Low);

        var result = workOrder.Update(" ", null, WorkOrderPriority.Critical, WorkOrderStatus.InProgress, null, Now);

        Assert.True(result.IsFailure);
        Assert.Equal(WorkOrderPriority.Low, workOrder.Priority);
        Assert.Equal(WorkOrderStatus.Open, workOrder.Status);
    }

    [Theory]
    [InlineData(WorkOrderStatus.Open, "2026-09-27", true)]
    [InlineData(WorkOrderStatus.OnHold, "2026-09-27", true)]
    [InlineData(WorkOrderStatus.InProgress, "2026-09-28", false)] // due today is not yet overdue
    [InlineData(WorkOrderStatus.Completed, "2026-09-01", false)]
    [InlineData(WorkOrderStatus.Open, null, false)]
    public void Overdue_means_open_and_past_its_due_date(WorkOrderStatus status, string? dueDate, bool expected) =>
        Assert.Equal(expected, WorkOrder.IsOverdue(status, dueDate is null ? null : DateOnly.Parse(dueDate), new DateOnly(2026, 9, 28)));

    private static void AssertInvalid(string? errorCode) =>
        Assert.Equal(WorkOrder.ValidationErrorCode, errorCode);
}
