using AircraftMRO.Domain.Entities;
using AircraftMRO.Domain.Enums.MaintenanceOrders;
using AircraftMRO.Tests.Support;

namespace AircraftMRO.Tests.Domain;

public sealed class MaintenanceOrderTests
{
    private static readonly Guid WorkOrderId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_schedules_the_maintenance_and_trims_notes()
    {
        var result = MaintenanceOrder.Create(WorkOrderId, MaintenanceType.CCheck, new DateOnly(2026, 11, 1), "  Hangar 2  ");

        Assert.True(result.IsSuccess);
        var maintenanceOrder = result.Value!;
        Assert.Equal(MaintenanceStatus.Scheduled, maintenanceOrder.Status);
        Assert.True(maintenanceOrder.IsOpen);
        Assert.Equal("Hangar 2", maintenanceOrder.Notes);
        Assert.Null(maintenanceOrder.CompletedAtUtc);
        Assert.Equal("CCheck 2026-11-01", maintenanceOrder.DisplayName);
        Assert.Null(MaintenanceOrder.Create(WorkOrderId, MaintenanceType.ACheck, new DateOnly(2026, 11, 1), "   ").Value!.Notes);
    }

    [Fact]
    public void Create_rejects_missing_work_order_date_and_bad_values()
    {
        var date = new DateOnly(2026, 11, 1);
        AssertInvalid(MaintenanceOrder.Create(Guid.Empty, MaintenanceType.ACheck, date, null).ErrorCode);
        AssertInvalid(MaintenanceOrder.Create(WorkOrderId, (MaintenanceType)99, date, null).ErrorCode);
        AssertInvalid(MaintenanceOrder.Create(WorkOrderId, MaintenanceType.ACheck, default, null).ErrorCode);
        AssertInvalid(MaintenanceOrder.Create(WorkOrderId, MaintenanceType.ACheck, date, new string('N', 2001)).ErrorCode);
    }

    [Fact]
    public void Completing_stamps_completion_time_and_closes_it_for_good()
    {
        var maintenanceOrder = MaintenanceOrderTestData.NewMaintenanceOrder(WorkOrderId);

        Assert.True(maintenanceOrder.Update(MaintenanceType.ACheck, MaintenanceOrderTestData.ScheduledDate,
            MaintenanceStatus.Completed, "Done", Now).IsSuccess);
        Assert.False(maintenanceOrder.IsOpen);
        Assert.Equal(Now, maintenanceOrder.CompletedAtUtc);

        var reopen = maintenanceOrder.Update(MaintenanceType.BCheck, MaintenanceOrderTestData.ScheduledDate,
            MaintenanceStatus.InProgress, null, Now);

        Assert.Equal(MaintenanceOrder.ClosedErrorCode, reopen.ErrorCode);
        Assert.Equal(MaintenanceStatus.Completed, maintenanceOrder.Status);
        Assert.Equal(MaintenanceType.ACheck, maintenanceOrder.Type);
    }

    [Fact]
    public void Failed_update_leaves_maintenance_unchanged()
    {
        var maintenanceOrder = MaintenanceOrderTestData.NewMaintenanceOrder(WorkOrderId);

        var result = maintenanceOrder.Update(MaintenanceType.DCheck, MaintenanceOrderTestData.ScheduledDate,
            (MaintenanceStatus)99, null, Now);

        Assert.Equal(MaintenanceOrder.ValidationErrorCode, result.ErrorCode);
        Assert.Equal(MaintenanceType.ACheck, maintenanceOrder.Type);
        Assert.Equal(MaintenanceStatus.Scheduled, maintenanceOrder.Status);
    }

    [Theory]
    [InlineData(MaintenanceStatus.Scheduled, true)]
    [InlineData(MaintenanceStatus.InProgress, false)]
    [InlineData(MaintenanceStatus.Completed, false)]
    public void Only_scheduled_maintenance_can_be_deleted(MaintenanceStatus status, bool expected) =>
        Assert.Equal(expected, MaintenanceOrder.CanBeDeleted(status));

    [Theory]
    [InlineData(MaintenanceStatus.Scheduled, "2026-09-27", true)]
    [InlineData(MaintenanceStatus.InProgress, "2026-09-27", true)]
    [InlineData(MaintenanceStatus.Scheduled, "2026-09-28", false)] // scheduled today is not yet overdue
    [InlineData(MaintenanceStatus.Completed, "2026-09-01", false)]
    public void Overdue_means_open_and_past_its_scheduled_date(MaintenanceStatus status, string scheduledDate, bool expected) =>
        Assert.Equal(expected, MaintenanceOrder.IsOverdue(status, DateOnly.Parse(scheduledDate), new DateOnly(2026, 9, 28)));

    private static void AssertInvalid(string? errorCode) =>
        Assert.Equal(MaintenanceOrder.ValidationErrorCode, errorCode);
}
