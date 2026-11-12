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

    [Theory]
    [InlineData(WorkOrderStatus.Open)]
    [InlineData(WorkOrderStatus.OnHold)]
    public void Maintenance_in_progress_puts_the_work_order_in_progress(WorkOrderStatus status)
    {
        var workOrder = WorkOrderTestData.NewWorkOrder(AircraftId);
        workOrder.Update(workOrder.Title, null, workOrder.Priority, status, null, 0, Now);

        Assert.True(workOrder.FollowMaintenance(maintenanceCount: 3, inProgressCount: 1, completedCount: 1, Now));
        Assert.Equal(WorkOrderStatus.InProgress, workOrder.Status);
        Assert.Null(workOrder.CompletedAtUtc);

        // Already in progress: nothing changes.
        Assert.False(workOrder.FollowMaintenance(3, 1, 1, Now));
    }

    [Fact]
    public void All_maintenance_completed_completes_the_work_order()
    {
        var workOrder = WorkOrderTestData.NewWorkOrder(AircraftId);

        Assert.True(workOrder.FollowMaintenance(maintenanceCount: 2, inProgressCount: 0, completedCount: 2, Now));
        Assert.Equal(WorkOrderStatus.Completed, workOrder.Status);
        Assert.Equal(Now, workOrder.CompletedAtUtc);
    }

    [Theory]
    [InlineData(0, 0, 0)]   // no maintenance at all
    [InlineData(2, 0, 1)]   // one completed, one still scheduled
    public void Work_order_stays_as_it_is_while_maintenance_is_only_scheduled_or_partly_done(int total, int inProgress, int completed)
    {
        var workOrder = WorkOrderTestData.NewWorkOrder(AircraftId);

        Assert.False(workOrder.FollowMaintenance(total, inProgress, completed, Now));
        Assert.Equal(WorkOrderStatus.Open, workOrder.Status);
    }

    [Fact]
    public void Closed_work_order_does_not_follow_maintenance()
    {
        var workOrder = WorkOrderTestData.NewWorkOrder(AircraftId);
        workOrder.Update(workOrder.Title, null, workOrder.Priority, WorkOrderStatus.Cancelled, null, 0, Now);

        Assert.False(workOrder.FollowMaintenance(1, 0, 1, Now));
        Assert.Equal(WorkOrderStatus.Cancelled, workOrder.Status);
    }

    [Fact]
    public void Completing_stamps_completion_time_and_closes_the_work_order()
    {
        var workOrder = WorkOrderTestData.NewWorkOrder(AircraftId);

        var result = workOrder.Update("Replace brake assembly", null, WorkOrderPriority.Medium, WorkOrderStatus.Completed, null, 0, Now);

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
        workOrder.Update("Replace brake assembly", null, WorkOrderPriority.Medium, closedStatus, null, 0, Now);

        var result = workOrder.Update("Reopened", null, WorkOrderPriority.Critical, WorkOrderStatus.Open, null, 0, Now);

        Assert.Equal(WorkOrder.ClosedErrorCode, result.ErrorCode);
        Assert.Equal(closedStatus, workOrder.Status);
        Assert.Equal("Replace brake assembly", workOrder.Title);
    }

    [Theory]
    [InlineData(WorkOrderStatus.Completed)]
    [InlineData(WorkOrderStatus.Cancelled)]
    public void Open_maintenance_blocks_closing_the_work_order(WorkOrderStatus closedStatus)
    {
        var workOrder = WorkOrderTestData.NewWorkOrder(AircraftId);

        var result = workOrder.Update("Replace brake assembly", null, WorkOrderPriority.Medium, closedStatus, null, 1, Now);

        Assert.Equal(WorkOrder.OpenMaintenanceErrorCode, result.ErrorCode);
        Assert.True(workOrder.IsOpen);
        Assert.Null(workOrder.CompletedAtUtc);
    }

    [Fact]
    public void Undefined_status_is_a_validation_error_even_with_open_maintenance()
    {
        var workOrder = WorkOrderTestData.NewWorkOrder(AircraftId);

        var result = workOrder.Update("Replace brake assembly", null, WorkOrderPriority.Medium, (WorkOrderStatus)99, null, 1, Now);

        Assert.Equal(WorkOrder.ValidationErrorCode, result.ErrorCode);
        Assert.Equal(WorkOrderStatus.Open, workOrder.Status);
    }

    [Theory]
    [InlineData(WorkOrderStatus.Open)]
    [InlineData(WorkOrderStatus.InProgress)]
    [InlineData(WorkOrderStatus.OnHold)]
    public void Open_maintenance_does_not_block_other_changes(WorkOrderStatus openStatus)
    {
        var workOrder = WorkOrderTestData.NewWorkOrder(AircraftId);

        var result = workOrder.Update("Renamed", null, WorkOrderPriority.High, openStatus, null, 3, Now);

        Assert.True(result.IsSuccess);
        Assert.Equal("Renamed", workOrder.Title);
    }

    [Fact]
    public void Failed_update_leaves_work_order_unchanged()
    {
        var workOrder = WorkOrderTestData.NewWorkOrder(AircraftId, WorkOrderPriority.Low);

        var result = workOrder.Update(" ", null, WorkOrderPriority.Critical, WorkOrderStatus.InProgress, null, 0, Now);

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
