using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.MaintenanceOrders;
using AircraftMRO.Application.Features.MaintenanceOrders.DTOs;
using AircraftMRO.Application.Features.WorkOrders;
using AircraftMRO.Application.Features.WorkOrders.DTOs;
using AircraftMRO.Domain.Entities;
using AircraftMRO.Domain.Enums.Aircraft;
using AircraftMRO.Domain.Enums.MaintenanceOrders;
using AircraftMRO.Domain.Enums.Notifications;
using AircraftMRO.Domain.Enums.WorkOrders;
using AircraftMRO.Infrastructure.Persistence;
using AircraftMRO.Infrastructure.Persistence.Features.Aircraft;
using AircraftMRO.Infrastructure.Persistence.Features.MaintenanceOrders;
using AircraftMRO.Infrastructure.Persistence.Features.WorkOrders;
using AircraftMRO.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace AircraftMRO.Tests.Integration;

[Collection(SqlServerCollection.Name)]
public sealed class MaintenanceOrderPersistenceIntegrationTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private readonly TestCurrentUser _user = new("planner-1");
    private readonly FakeTimeProvider _clock = new();
    private string _connectionString = null!;

    private DateOnly Today => DateOnly.FromDateTime(_clock.Now.UtcDateTime);

    public async Task InitializeAsync() => _connectionString = await fixture.CreateDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Create_saves_maintenance_with_a_notification_and_writes_the_work_order_without_one()
    {
        var workOrderId = await CreateWorkOrderAsync("MN-001");
        var workOrderVersionBefore = await WorkOrderRowVersionAsync(workOrderId);

        var id = await CreateAsync(workOrderId, MaintenanceType.CCheck, Today.AddDays(3));

        var stored = await GetAsync(id);
        Assert.Equal(workOrderId, stored.WorkOrderId);
        Assert.Equal(MaintenanceType.CCheck, stored.Type);
        Assert.Equal(MaintenanceStatus.Scheduled, stored.Status);
        Assert.Equal("MN-001", stored.AircraftRegistration);
        Assert.Equal("planner-1", stored.CreatedBy);
        Assert.NotEqual(workOrderVersionBefore, await WorkOrderRowVersionAsync(workOrderId));

        await using var context = NewContext();
        var notifications = await context.Notifications.AsNoTracking()
            .Where(n => n.EntityId == id || n.EntityId == workOrderId)
            .Select(n => new { n.EntityType, n.Action })
            .ToListAsync();
        Assert.Contains(notifications, n => n.EntityType == "MaintenanceOrder" && n.Action == NotificationAction.Created);
        Assert.DoesNotContain(notifications, n => n.EntityType == "WorkOrder" && n.Action == NotificationAction.Updated);
    }

    [Fact]
    public async Task Editing_maintenance_without_changing_its_status_leaves_the_work_order_untouched()
    {
        var workOrderId = await CreateWorkOrderAsync("MN-005");
        var id = await CreateAsync(workOrderId, MaintenanceType.ACheck, Today);
        var before = await GetWorkOrderAsync(workOrderId);

        await UpdateAsync(id, MaintenanceStatus.Scheduled, MaintenanceType.BCheck);

        var after = await GetWorkOrderAsync(workOrderId);
        Assert.Equal(before.RowVersion, after.RowVersion);
        Assert.Equal(before.UpdatedAtUtc, after.UpdatedAtUtc);
        Assert.Equal(MaintenanceType.BCheck, (await GetAsync(id)).Type);
    }

    [Fact]
    public async Task Work_order_follows_its_maintenance_and_completing_it_frees_the_aircraft()
    {
        var workOrderId = await CreateWorkOrderAsync("MN-006");
        var aircraftId = (await GetWorkOrderAsync(workOrderId)).AircraftId;
        Assert.Equal(AircraftStatus.RequiredMaintenance, await AircraftStatusAsync(aircraftId));
        var first = await CreateAsync(workOrderId, MaintenanceType.ACheck, Today);
        var second = await CreateAsync(workOrderId, MaintenanceType.BCheck, Today);

        await UpdateAsync(first, MaintenanceStatus.InProgress);
        Assert.Equal(WorkOrderStatus.InProgress, (await GetWorkOrderAsync(workOrderId)).Status);

        await UpdateAsync(first, MaintenanceStatus.Completed);
        var partlyDone = await GetWorkOrderAsync(workOrderId);
        Assert.Equal(WorkOrderStatus.InProgress, partlyDone.Status);   // the second is still scheduled
        Assert.Equal((2, 1), (partlyDone.MaintenanceCount, partlyDone.CompletedMaintenanceCount));

        await UpdateAsync(second, MaintenanceStatus.Completed);
        var done = await GetWorkOrderAsync(workOrderId);
        Assert.Equal(WorkOrderStatus.Completed, done.Status);
        Assert.Equal(_clock.Now, done.CompletedAtUtc);
        Assert.Equal(AircraftStatus.Active, await AircraftStatusAsync(aircraftId));

        await using var context = NewContext();
        var workOrderNotifications = await context.Notifications.AsNoTracking()
            .CountAsync(n => n.EntityId == workOrderId && n.Action == NotificationAction.Updated);
        Assert.Equal(2, workOrderNotifications);   // in progress, then completed
    }

    [Fact]
    public async Task Two_maintenance_changes_at_once_under_one_work_order_reject_the_second()
    {
        var workOrderId = await CreateWorkOrderAsync("MN-007");
        var first = await CreateAsync(workOrderId, MaintenanceType.ACheck, Today);
        var second = await CreateAsync(workOrderId, MaintenanceType.BCheck, Today);
        var firstVersion = (await GetAsync(first)).RowVersion;

        // Completing the first is part-way through, as the service does it: the second still
        // counts as open, so the work order is left open...
        await using var slow = NewContext();
        var repository = new MaintenanceOrderRepository(slow);
        var maintenanceOrder = (await repository.GetForUpdateAsync(first, CancellationToken.None))!;
        var workOrder = (await repository.GetWorkOrderForUpdateAsync(workOrderId, CancellationToken.None))!;
        Assert.True(maintenanceOrder.Update(maintenanceOrder.Type, maintenanceOrder.ScheduledDate,
            MaintenanceStatus.Completed, null, _clock.Now).IsSuccess);
        Assert.False(workOrder.FollowMaintenance(2, 0, 1, _clock.Now));

        // ...when the second is completed, which also saves the work order.
        await UpdateAsync(second, MaintenanceStatus.Completed);

        var result = await repository.UpdateAsync(maintenanceOrder, firstVersion, workOrder, null, CancellationToken.None);

        Assert.Equal(MaintenanceOrderErrors.WorkOrderChanged, result.ErrorCode);
        Assert.Equal(MaintenanceStatus.Scheduled, (await GetAsync(first)).Status);

        // Retrying sees both completed and completes the work order.
        await UpdateAsync(first, MaintenanceStatus.Completed);
        Assert.Equal(WorkOrderStatus.Completed, (await GetWorkOrderAsync(workOrderId)).Status);
    }

    [Fact]
    public async Task Work_order_cannot_close_while_maintenance_is_open_and_completes_itself_once_it_all_is()
    {
        var workOrderId = await CreateWorkOrderAsync("MN-002");
        var scheduled = await CreateAsync(workOrderId, MaintenanceType.ACheck, Today);
        var inProgress = await CreateAsync(workOrderId, MaintenanceType.BCheck, Today);
        await UpdateAsync(inProgress, MaintenanceStatus.InProgress);

        await using (var context = NewContext())
        {
            Assert.Equal(2, await new WorkOrderRepository(context).CountOpenMaintenanceOrdersAsync(workOrderId, CancellationToken.None));
        }

        Assert.Equal(WorkOrderErrors.OpenMaintenance, await CloseWorkOrderAsync(workOrderId, WorkOrderStatus.Completed));
        Assert.Equal(WorkOrderErrors.OpenMaintenance, await CloseWorkOrderAsync(workOrderId, WorkOrderStatus.Cancelled));

        // Deleting the scheduled one and completing the other completes the work order.
        await DeleteAsync(scheduled);
        Assert.Equal(WorkOrderStatus.InProgress, (await GetWorkOrderAsync(workOrderId)).Status);
        await UpdateAsync(inProgress, MaintenanceStatus.Completed);
        Assert.Equal(WorkOrderStatus.Completed, (await GetWorkOrderAsync(workOrderId)).Status);

        // Closed work order: no new maintenance can be scheduled under it.
        await using var after = NewContext();
        var rejected = await NewService(after).CreateAsync(
            new CreateMaintenanceOrderDto(workOrderId, MaintenanceType.ACheck, Today, null), CancellationToken.None);
        Assert.Equal(MaintenanceOrderErrors.WorkOrderClosed, rejected.ErrorCode);
    }

    [Fact]
    public async Task Maintenance_added_while_the_work_order_is_being_closed_rejects_the_close()
    {
        var workOrderId = await CreateWorkOrderAsync("MN-003");
        var rowVersion = await WorkOrderRowVersionAsync(workOrderId);

        // The close is part-way through, as the service does it: work order loaded, open
        // maintenance counted (none), domain change applied...
        await using var slow = NewContext();
        var repository = new WorkOrderRepository(slow);
        var workOrder = (await repository.GetForUpdateAsync(workOrderId, CancellationToken.None))!;
        var openMaintenance = await repository.CountOpenMaintenanceOrdersAsync(workOrderId, CancellationToken.None);
        Assert.Equal(0, openMaintenance);
        Assert.True(workOrder.Update(workOrder.Title, null, workOrder.Priority, WorkOrderStatus.Completed,
            null, openMaintenance, _clock.Now).IsSuccess);
        var aircraft = (await repository.GetAircraftForUpdateAsync(workOrder.AircraftId, CancellationToken.None))!;
        aircraft.ApplyOpenWorkOrders(0, anyCritical: false);

        // ...when maintenance is scheduled under it, which also writes the work order.
        await CreateAsync(workOrderId, MaintenanceType.Unscheduled, Today);

        var result = await repository.UpdateAsync(workOrder, rowVersion, aircraft, CancellationToken.None);

        Assert.Equal(WorkOrderErrors.ConcurrencyConflict, result.ErrorCode);
        Assert.Equal(WorkOrderStatus.Open, (await GetWorkOrderAsync(workOrderId)).Status);
    }

    [Fact]
    public async Task Work_order_closed_after_it_was_read_rejects_the_maintenance_save()
    {
        var workOrderId = await CreateWorkOrderAsync("MN-004");

        await using var slow = NewContext();
        var repository = new MaintenanceOrderRepository(slow);
        var tracked = (await repository.GetWorkOrderForUpdateAsync(workOrderId, CancellationToken.None))!;
        Assert.True(tracked.IsOpen);

        // Meanwhile the work order is closed (it has no maintenance yet, so this is allowed).
        Assert.Null(await CloseWorkOrderAsync(workOrderId, WorkOrderStatus.Completed));

        var maintenanceOrder = MaintenanceOrderTestData.NewMaintenanceOrder(workOrderId);
        var result = await repository.AddAsync(maintenanceOrder, tracked, CancellationToken.None);

        Assert.Equal(MaintenanceOrderErrors.WorkOrderChanged, result.ErrorCode);
        await using var verify = NewContext();
        Assert.False(await verify.MaintenanceOrders.AnyAsync(m => m.WorkOrderId == workOrderId));
    }

    [Fact]
    public async Task List_filters_sorts_and_flags_overdue()
    {
        var first = await CreateWorkOrderAsync("LS-M1");
        var second = await CreateWorkOrderAsync("LS-M2");
        var overdue = await CreateAsync(first, MaintenanceType.ACheck, Today.AddDays(-1));
        var later = await CreateAsync(first, MaintenanceType.DCheck, Today.AddDays(10));
        var completed = await CreateAsync(second, MaintenanceType.BCheck, Today.AddDays(-5));
        var started = await CreateAsync(second, MaintenanceType.CCheck, Today.AddDays(2));
        await UpdateAsync(completed, MaintenanceStatus.Completed);   // "started" is still open, so the work order stays open
        await UpdateAsync(started, MaintenanceStatus.InProgress);

        await using var context = NewContext();
        var repository = new MaintenanceOrderRepository(context);
        async Task<Guid[]> Find(MaintenanceOrderListFilter filter) =>
            (await repository.ListAsync(filter, new PagedRequest(1, 100), Today, CancellationToken.None))
                .Items.Select(m => m.Id).ToArray();

        Assert.Equal([overdue, started, later, completed], await Find(MaintenanceOrderListFilter.None));
        Assert.Equal([overdue, later], await Find(new MaintenanceOrderListFilter(workOrderId: first)));
        Assert.Equal([overdue], await Find(new MaintenanceOrderListFilter(overdueOnly: true)));
        Assert.Equal([overdue, started, later], await Find(new MaintenanceOrderListFilter(openOnly: true)));
        Assert.Equal([started], await Find(new MaintenanceOrderListFilter(status: MaintenanceStatus.InProgress)));
        Assert.Equal([later], await Find(new MaintenanceOrderListFilter(type: MaintenanceType.DCheck)));

        var page = await repository.ListAsync(MaintenanceOrderListFilter.None, new PagedRequest(1, 100), Today, CancellationToken.None);
        Assert.True(page.Items.Single(m => m.Id == overdue).IsOverdue);
        Assert.False(page.Items.Single(m => m.Id == completed).IsOverdue);   // past date but completed
        Assert.Equal("LS-M2", page.Items.Single(m => m.Id == started).AircraftRegistration);

        var statistics = await repository.GetStatisticsAsync(null, Today, CancellationToken.None);
        Assert.Equal(4, statistics.TotalCount);
        Assert.Equal(2, statistics.CountFor(MaintenanceStatus.Scheduled));
        Assert.Equal(1, statistics.CountFor(MaintenanceStatus.InProgress));
        Assert.Equal(1, statistics.CountFor(MaintenanceStatus.Completed));
        Assert.Equal(3, statistics.OpenCount);
        Assert.Equal(1, statistics.OverdueCount);   // the completed one is past its date but not overdue

        var firstStatistics = await repository.GetStatisticsAsync(first, Today, CancellationToken.None);
        Assert.Equal((2, 1), (firstStatistics.TotalCount, firstStatistics.OverdueCount));

        var options = await repository.ListWorkOrderOptionsAsync(CancellationToken.None);
        Assert.Contains(options, option => option.Id == first && option.IsOpen);
        var single = await repository.GetWorkOrderOptionAsync(second, CancellationToken.None);
        Assert.Equal(("LS-M2", true), (single!.AircraftRegistration, single.IsOpen));
    }

    [Fact]
    public async Task Maintenance_of_a_soft_deleted_work_order_is_hidden()
    {
        var workOrderId = await CreateWorkOrderAsync("SD-M1");
        var id = await CreateAsync(workOrderId, MaintenanceType.ACheck, Today);
        await UpdateAsync(id, MaintenanceStatus.Completed);   // completes the work order, so it can be deleted
        await using (var context = NewContext())
        {
            var workOrder = await GetWorkOrderAsync(workOrderId);
            var result = await new WorkOrderService(new WorkOrderRepository(context), _clock, new WorkOrderOptions())
                .DeleteAsync(workOrderId, workOrder.RowVersion, CancellationToken.None);
            Assert.True(result.IsSuccess, result.ErrorMessage);
        }

        await using var verify = NewContext();
        var repository = new MaintenanceOrderRepository(verify);
        Assert.Null(await repository.GetByIdAsync(id, Today, CancellationToken.None));
        Assert.Null(await repository.GetForUpdateAsync(id, CancellationToken.None));
        Assert.Null(await repository.GetWorkOrderOptionAsync(workOrderId, CancellationToken.None));
        Assert.Equal(0, (await repository.ListAsync(
            new MaintenanceOrderListFilter(workOrderId), new PagedRequest(), Today, CancellationToken.None)).TotalCount);
        Assert.Equal(0, (await repository.GetStatisticsAsync(workOrderId, Today, CancellationToken.None)).TotalCount);

        // Reads, updates and deletes all agree that it is gone.
        var service = NewService(verify);
        Assert.Equal(MaintenanceOrderErrors.NotFound,
            (await service.UpdateAsync(id, MaintenanceOrderTestData.UpdateDto(), CancellationToken.None)).ErrorCode);
        Assert.Equal(MaintenanceOrderErrors.NotFound, (await service.DeleteAsync(id, [1], CancellationToken.None)).ErrorCode);
    }

    [Fact]
    public async Task Stale_row_version_rejects_update_and_delete_and_delete_is_soft()
    {
        var workOrderId = await CreateWorkOrderAsync("ST-M1");
        var id = await CreateAsync(workOrderId, MaintenanceType.ACheck, Today);
        var stale = (await GetAsync(id)).RowVersion;
        await UpdateAsync(id, MaintenanceStatus.Scheduled, MaintenanceType.BCheck);

        await using (var context = NewContext())
        {
            var update = await NewService(context).UpdateAsync(
                id, MaintenanceOrderTestData.UpdateDto(rowVersion: stale), CancellationToken.None);
            Assert.Equal(MaintenanceOrderErrors.ConcurrencyConflict, update.ErrorCode);
        }

        await using (var context = NewContext())
        {
            var delete = await NewService(context).DeleteAsync(id, stale, CancellationToken.None);
            Assert.Equal(MaintenanceOrderErrors.ConcurrencyConflict, delete.ErrorCode);
        }

        await DeleteAsync(id);
        await using var verify = NewContext();
        Assert.Null(await new MaintenanceOrderRepository(verify).GetByIdAsync(id, Today, CancellationToken.None));
        Assert.True(await verify.MaintenanceOrders.IgnoreQueryFilters().AnyAsync(m => m.Id == id && m.IsDeleted));
    }

    private AircraftMroDbContext NewContext() => SqlServerFixture.CreateContext(_connectionString, _user, _clock);

    private MaintenanceOrderService NewService(AircraftMroDbContext context) =>
        new(new MaintenanceOrderRepository(context), _clock, new WorkOrderOptions());

    private async Task<Guid> CreateWorkOrderAsync(string registration)
    {
        var aircraft = Aircraft.Create(registration, "Airbus", "A320-214", registration, 2012, 0m,
            AircraftStatus.Active, AircraftTestData.CurrentYear).Value!;
        await using (var context = NewContext())
        {
            Assert.True((await new AircraftRepository(context).AddAsync(aircraft, CancellationToken.None)).IsSuccess);
        }

        await using var workContext = NewContext();
        var result = await new WorkOrderService(new WorkOrderRepository(workContext), _clock, new WorkOrderOptions())
            .CreateAsync(new CreateWorkOrderDto(aircraft.Id, "Heavy check", null, WorkOrderPriority.Low, null), CancellationToken.None);
        Assert.True(result.IsSuccess, result.ErrorMessage);
        return result.Value;
    }

    /// <summary>Returns the error code, or null when the work order was closed.</summary>
    private async Task<string?> CloseWorkOrderAsync(Guid workOrderId, WorkOrderStatus status)
    {
        var current = await GetWorkOrderAsync(workOrderId);
        await using var context = NewContext();
        var result = await new WorkOrderService(new WorkOrderRepository(context), _clock, new WorkOrderOptions()).UpdateAsync(
            workOrderId,
            new UpdateWorkOrderDto(current.Title, current.Description, current.Priority, status, current.DueDate, current.RowVersion),
            CancellationToken.None);
        return result.ErrorCode;
    }

    private async Task<Guid> CreateAsync(Guid workOrderId, MaintenanceType type, DateOnly scheduledDate)
    {
        await using var context = NewContext();
        var result = await NewService(context).CreateAsync(
            new CreateMaintenanceOrderDto(workOrderId, type, scheduledDate, null), CancellationToken.None);
        Assert.True(result.IsSuccess, result.ErrorMessage);
        return result.Value;
    }

    private async Task UpdateAsync(Guid id, MaintenanceStatus status, MaintenanceType? type = null)
    {
        var current = await GetAsync(id);
        await using var context = NewContext();
        var result = await NewService(context).UpdateAsync(
            id,
            new UpdateMaintenanceOrderDto(type ?? current.Type, current.ScheduledDate, status, current.Notes, current.RowVersion),
            CancellationToken.None);
        Assert.True(result.IsSuccess, result.ErrorMessage);
    }

    private async Task DeleteAsync(Guid id)
    {
        var current = await GetAsync(id);
        await using var context = NewContext();
        var result = await NewService(context).DeleteAsync(id, current.RowVersion, CancellationToken.None);
        Assert.True(result.IsSuccess, result.ErrorMessage);
    }

    private async Task<MaintenanceOrderDto> GetAsync(Guid id)
    {
        await using var context = NewContext();
        return (await new MaintenanceOrderRepository(context).GetByIdAsync(id, Today, CancellationToken.None))!;
    }

    private async Task<WorkOrderDto> GetWorkOrderAsync(Guid id)
    {
        await using var context = NewContext();
        return (await new WorkOrderRepository(context).GetByIdAsync(id, Today, CancellationToken.None))!;
    }

    private async Task<byte[]> WorkOrderRowVersionAsync(Guid id) => (await GetWorkOrderAsync(id)).RowVersion;

    private async Task<AircraftStatus> AircraftStatusAsync(Guid id)
    {
        await using var context = NewContext();
        return await context.Aircraft.AsNoTracking().Where(a => a.Id == id).Select(a => a.Status).SingleAsync();
    }
}
