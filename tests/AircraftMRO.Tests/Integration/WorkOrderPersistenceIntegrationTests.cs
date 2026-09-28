using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.WorkOrders;
using AircraftMRO.Application.Features.WorkOrders.DTOs;
using AircraftMRO.Domain.Entities;
using AircraftMRO.Domain.Enums.Aircraft;
using AircraftMRO.Domain.Enums.Notifications;
using AircraftMRO.Domain.Enums.WorkOrders;
using AircraftMRO.Infrastructure.Persistence;
using AircraftMRO.Infrastructure.Persistence.Features.Aircraft;
using AircraftMRO.Infrastructure.Persistence.Features.WorkOrders;
using AircraftMRO.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace AircraftMRO.Tests.Integration;

[Collection(SqlServerCollection.Name)]
public sealed class WorkOrderPersistenceIntegrationTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private readonly TestCurrentUser _user = new("planner-1");
    private readonly FakeTimeProvider _clock = new();
    private string _connectionString = null!;

    private DateOnly Today => DateOnly.FromDateTime(_clock.Now.UtcDateTime);

    public async Task InitializeAsync() => _connectionString = await fixture.CreateDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Critical_work_order_and_grounding_are_saved_together_with_a_notification_each()
    {
        var aircraft = await AddAircraftAsync("GR-001", AircraftStatus.Active);

        var created = await CreateAsync(aircraft.Id, WorkOrderPriority.Critical);

        await using var context = NewContext();
        var storedAircraft = await context.Aircraft.AsNoTracking().SingleAsync(a => a.Id == aircraft.Id);
        Assert.Equal(AircraftStatus.Grounded, storedAircraft.Status);
        Assert.Equal("planner-1", storedAircraft.UpdatedBy);

        var workOrder = await context.WorkOrders.AsNoTracking().SingleAsync(w => w.Id == created);
        Assert.Equal(aircraft.Id, workOrder.AircraftId);
        Assert.Matches("^WO-\\d{6}$", workOrder.Number);
        Assert.Equal(WorkOrderStatus.Open, workOrder.Status);

        var notifications = await context.Notifications.AsNoTracking()
            .Where(n => n.EntityId == created || n.EntityId == aircraft.Id)
            .OrderBy(n => n.Id)
            .Select(n => new { n.EntityType, n.Action, n.Message })
            .ToListAsync();
        Assert.Contains(notifications, n => n.EntityType == "WorkOrder" && n.Action == NotificationAction.Created
            && n.Message.StartsWith($"Work order {workOrder.Number} was created", StringComparison.Ordinal));
        Assert.Contains(notifications, n => n.EntityType == "Aircraft" && n.Action == NotificationAction.Updated
            && n.Message.Contains("Status Active → Grounded", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Non_critical_work_order_moves_an_active_aircraft_into_maintenance()
    {
        var aircraft = await AddAircraftAsync("MX-001", AircraftStatus.Active);

        await CreateAsync(aircraft.Id, WorkOrderPriority.Low);

        Assert.Equal(AircraftStatus.InMaintenance, await AircraftStatusAsync(aircraft.Id));
    }

    [Fact]
    public async Task Aircraft_changed_after_it_was_read_rejects_the_whole_save()
    {
        var aircraft = await AddAircraftAsync("RC-001", AircraftStatus.Grounded);

        await using var planner = NewContext();
        var repository = new WorkOrderRepository(planner);
        var tracked = (await repository.GetAircraftForUpdateAsync(aircraft.Id, CancellationToken.None))!;
        Assert.True(tracked.ApplyOpenWorkOrders(1, anyCritical: true).IsSuccess); // already grounded: no value changes

        // Someone else edits the aircraft between the read and the save.
        await using (var other = NewContext())
        {
            var competing = await other.Aircraft.SingleAsync(a => a.Id == aircraft.Id);
            competing.Update("RC-001", "Airbus", "A320-214", "RC-001", 2012, 1m, AircraftStatus.Active, 2026);
            await other.SaveChangesAsync();
        }

        var workOrder = WorkOrderTestData.NewWorkOrder(aircraft.Id, WorkOrderPriority.Critical,
            await repository.NextNumberAsync(CancellationToken.None));
        var result = await repository.AddAsync(workOrder, tracked, CancellationToken.None);

        // Only the aircraft was stale, so the conflict is reported as retryable.
        Assert.Equal(WorkOrderErrors.AircraftChanged, result.ErrorCode);
        await using var verify = NewContext();
        Assert.False(await verify.WorkOrders.AnyAsync(w => w.AircraftId == aircraft.Id));
        Assert.Equal(AircraftStatus.Active, await AircraftStatusAsync(aircraft.Id));
    }

    [Fact]
    public async Task Saving_without_a_status_change_writes_the_aircraft_but_records_no_aircraft_notification()
    {
        var aircraft = await AddAircraftAsync("NN-001", AircraftStatus.InMaintenance);
        var rowVersionBefore = await AircraftRowVersionAsync(aircraft.Id);

        await CreateAsync(aircraft.Id, WorkOrderPriority.Medium);

        Assert.NotEqual(rowVersionBefore, await AircraftRowVersionAsync(aircraft.Id));
        await using var context = NewContext();
        var aircraftUpdates = await context.Notifications.AsNoTracking()
            .CountAsync(n => n.EntityId == aircraft.Id && n.Action == NotificationAction.Updated);
        Assert.Equal(0, aircraftUpdates);
        Assert.Equal(AircraftStatus.InMaintenance, await AircraftStatusAsync(aircraft.Id));
    }

    [Fact]
    public async Task Numbers_come_from_the_sequence_unique_and_increasing()
    {
        await using var context = NewContext();
        var repository = new WorkOrderRepository(context);

        var first = await repository.NextNumberAsync(CancellationToken.None);
        var second = await repository.NextNumberAsync(CancellationToken.None);

        Assert.Matches("^WO-\\d{6}$", first);
        Assert.True(string.CompareOrdinal(second, first) > 0, $"{second} should follow {first}");
    }

    [Fact]
    public async Task Foreign_key_rejects_a_work_order_for_an_unknown_aircraft()
    {
        var aircraft = await AddAircraftAsync("FK-001", AircraftStatus.Active);
        await using var context = NewContext();
        var repository = new WorkOrderRepository(context);
        var tracked = (await repository.GetAircraftForUpdateAsync(aircraft.Id, CancellationToken.None))!;

        await Assert.ThrowsAsync<DbUpdateException>(() => repository.AddAsync(
            WorkOrderTestData.NewWorkOrder(Guid.NewGuid(), number: "WO-FK-1"), tracked, CancellationToken.None));
    }

    [Fact]
    public async Task Priority_changes_move_the_aircraft_both_ways_and_closing_the_last_returns_it_to_active()
    {
        var aircraft = await AddAircraftAsync("PR-001", AircraftStatus.Active);
        var critical = await CreateAsync(aircraft.Id, WorkOrderPriority.Critical);
        var low = await CreateAsync(aircraft.Id, WorkOrderPriority.Low);
        Assert.Equal(AircraftStatus.Grounded, await AircraftStatusAsync(aircraft.Id));

        await UpdateAsync(critical, WorkOrderStatus.InProgress, WorkOrderPriority.High);
        Assert.Equal(AircraftStatus.InMaintenance, await AircraftStatusAsync(aircraft.Id));

        await UpdateAsync(critical, WorkOrderStatus.InProgress, WorkOrderPriority.Critical);
        Assert.Equal(AircraftStatus.Grounded, await AircraftStatusAsync(aircraft.Id));

        await UpdateAsync(critical, WorkOrderStatus.Completed, WorkOrderPriority.Critical);
        Assert.Equal(AircraftStatus.InMaintenance, await AircraftStatusAsync(aircraft.Id));   // the low one is still open

        await UpdateAsync(low, WorkOrderStatus.Cancelled, WorkOrderPriority.Low);
        Assert.Equal(AircraftStatus.Active, await AircraftStatusAsync(aircraft.Id));
    }

    [Fact]
    public async Task Closing_two_work_orders_at_once_cannot_leave_the_aircraft_in_maintenance()
    {
        var aircraft = await AddAircraftAsync("CC-001", AircraftStatus.Active);
        var first = await CreateAsync(aircraft.Id, WorkOrderPriority.Low);
        var second = await CreateAsync(aircraft.Id, WorkOrderPriority.Low);
        var firstRowVersion = await WorkOrderRowVersionAsync(first);

        // The first close is part-way through, as the service does it: aircraft loaded, then the
        // other open work (the second one) counted, so it would set In maintenance.
        await using var slow = NewContext();
        var repository = new WorkOrderRepository(slow);
        var workOrder = (await repository.GetForUpdateAsync(first, CancellationToken.None))!;
        workOrder.Update(workOrder.Title, null, WorkOrderPriority.Low, WorkOrderStatus.Completed, null, _clock.Now);
        var tracked = (await repository.GetAircraftForUpdateAsync(aircraft.Id, CancellationToken.None))!;
        var others = await repository.GetOpenWorkOrderCountsAsync(aircraft.Id, first, CancellationToken.None);
        Assert.Equal(1, others.Total);
        tracked.ApplyOpenWorkOrders(others.Total, others.Critical > 0);

        // Meanwhile the second one is closed; it still sees the first as open.
        await UpdateAsync(second, WorkOrderStatus.Completed, WorkOrderPriority.Low);

        var result = await repository.UpdateAsync(workOrder, firstRowVersion, tracked, CancellationToken.None);

        // The first work order's own version is still current: only the aircraft moved on.
        Assert.Equal(WorkOrderErrors.AircraftChanged, result.ErrorCode);
        // Retrying with fresh data closes the last one and returns the aircraft to service.
        await UpdateAsync(first, WorkOrderStatus.Completed, WorkOrderPriority.Low);
        Assert.Equal(AircraftStatus.Active, await AircraftStatusAsync(aircraft.Id));
    }

    [Fact]
    public async Task List_filters_sorts_and_flags_overdue_and_statistics_match()
    {
        var alpha = await AddAircraftAsync("LS-ALPHA", AircraftStatus.Active);
        var bravo = await AddAircraftAsync("LS-BRAVO", AircraftStatus.Active);
        var overdue = await CreateAsync(alpha.Id, WorkOrderPriority.Critical, "Hydraulic leak", Today.AddDays(-1));
        var completed = await CreateAsync(alpha.Id, WorkOrderPriority.Low, "Cabin light", Today.AddDays(-5));
        await UpdateAsync(completed, WorkOrderStatus.Completed, WorkOrderPriority.Low);
        var noDueDate = await CreateAsync(bravo.Id, WorkOrderPriority.High, "Tyre change", null);
        var onHold = await CreateAsync(bravo.Id, WorkOrderPriority.Medium, "Seat repair", Today.AddDays(1));
        await UpdateAsync(onHold, WorkOrderStatus.OnHold, WorkOrderPriority.Medium);

        await using var context = NewContext();
        var repository = new WorkOrderRepository(context);
        async Task<Guid[]> Find(WorkOrderListFilter filter) =>
            (await repository.ListAsync(filter, new PagedRequest(1, 100), Today, CancellationToken.None))
                .Items.Select(w => w.Id).ToArray();

        // Open first, then most urgent; closed work sinks to the end.
        Assert.Equal([overdue, noDueDate, onHold, completed], await Find(WorkOrderListFilter.None));
        Assert.Equal([overdue], await Find(new WorkOrderListFilter(overdueOnly: true)));
        Assert.Equal([overdue, noDueDate, onHold], await Find(new WorkOrderListFilter(openOnly: true)));
        Assert.Equal([overdue, completed], await Find(new WorkOrderListFilter(aircraftId: alpha.Id)));
        Assert.Equal([noDueDate, onHold], await Find(new WorkOrderListFilter("ls-bravo")));   // registration
        Assert.Equal([noDueDate], await Find(new WorkOrderListFilter("tyre")));                // title
        Assert.Equal([onHold], await Find(new WorkOrderListFilter(status: WorkOrderStatus.OnHold)));
        Assert.Equal([overdue], await Find(new WorkOrderListFilter(priority: WorkOrderPriority.Critical, openOnly: true)));

        var page = await repository.ListAsync(WorkOrderListFilter.None, new PagedRequest(1, 100), Today, CancellationToken.None);
        Assert.True(page.Items.Single(w => w.Id == overdue).IsOverdue);
        Assert.False(page.Items.Single(w => w.Id == completed).IsOverdue);   // past due but closed
        Assert.Equal("LS-ALPHA", page.Items.Single(w => w.Id == overdue).AircraftRegistration);

        var statistics = await repository.GetStatisticsAsync(null, Today, CancellationToken.None);
        Assert.Equal(4, statistics.TotalCount);
        Assert.Equal(1, statistics.CountFor(WorkOrderStatus.Completed));
        Assert.Equal(1, statistics.CountFor(WorkOrderStatus.OnHold));
        Assert.Equal(3, statistics.OpenCount);
        Assert.Equal(1, statistics.OpenCountFor(WorkOrderPriority.Critical));
        Assert.Equal(0, statistics.OpenCountFor(WorkOrderPriority.Low));
        Assert.Equal(1, statistics.OverdueCount);

        var bravoStatistics = await repository.GetStatisticsAsync(bravo.Id, Today, CancellationToken.None);
        Assert.Equal(2, bravoStatistics.TotalCount);
        Assert.Equal(0, bravoStatistics.OverdueCount);

        var counts = await new AircraftRepository(context).GetOpenWorkOrderCountsAsync(alpha.Id, CancellationToken.None);
        Assert.Equal(new(1, 1), (counts.Total, counts.Critical));
    }

    [Fact]
    public async Task Work_orders_of_a_soft_deleted_aircraft_are_hidden()
    {
        var aircraft = await AddAircraftAsync("SD-001", AircraftStatus.Active);
        var workOrder = await CreateAsync(aircraft.Id, WorkOrderPriority.Low);
        await UpdateAsync(workOrder, WorkOrderStatus.Completed, WorkOrderPriority.Low);
        await using (var context = NewContext())
        {
            var repository = new AircraftRepository(context);
            var tracked = (await repository.GetForUpdateAsync(aircraft.Id, CancellationToken.None))!;
            var rowVersion = (await repository.GetByIdAsync(aircraft.Id, CancellationToken.None))!.RowVersion;
            Assert.True((await repository.DeleteAsync(tracked, rowVersion, CancellationToken.None)).IsSuccess);
        }

        await using var verify = NewContext();
        var repositoryAfter = new WorkOrderRepository(verify);
        Assert.Null(await repositoryAfter.GetByIdAsync(workOrder, Today, CancellationToken.None));
        Assert.Equal(0, (await repositoryAfter.ListAsync(
            new WorkOrderListFilter(aircraftId: aircraft.Id), new PagedRequest(), Today, CancellationToken.None)).TotalCount);
        Assert.Equal(0, (await repositoryAfter.GetStatisticsAsync(aircraft.Id, Today, CancellationToken.None)).TotalCount);
    }

    [Fact]
    public async Task Stale_row_version_rejects_work_order_update_and_delete()
    {
        var aircraft = await AddAircraftAsync("ST-001", AircraftStatus.Active);
        var id = await CreateAsync(aircraft.Id, WorkOrderPriority.Low);
        var stale = await WorkOrderRowVersionAsync(id);
        await UpdateAsync(id, WorkOrderStatus.InProgress, WorkOrderPriority.Low);

        await using var context = NewContext();
        var service = NewService(context);
        var update = await service.UpdateAsync(id, WorkOrderTestData.UpdateDto(WorkOrderStatus.OnHold, rowVersion: stale), CancellationToken.None);
        Assert.Equal(WorkOrderErrors.ConcurrencyConflict, update.ErrorCode);

        // Only closed work orders can be deleted; a stale version is still rejected.
        await UpdateAsync(id, WorkOrderStatus.Completed, WorkOrderPriority.Low);
        await using var context2 = NewContext();
        var delete = await NewService(context2).DeleteAsync(id, stale, CancellationToken.None);
        Assert.Equal(WorkOrderErrors.ConcurrencyConflict, delete.ErrorCode);
    }

    [Fact]
    public async Task Open_work_order_cannot_be_deleted()
    {
        var aircraft = await AddAircraftAsync("OD-001", AircraftStatus.Active);
        var id = await CreateAsync(aircraft.Id, WorkOrderPriority.Critical);

        await using var context = NewContext();
        var result = await NewService(context)
            .DeleteAsync(id, await WorkOrderRowVersionAsync(id), CancellationToken.None);

        Assert.Equal(WorkOrderErrors.OpenCannotBeDeleted, result.ErrorCode);
        Assert.NotNull(await GetAsync(id));
        Assert.Equal(AircraftStatus.Grounded, await AircraftStatusAsync(aircraft.Id));
    }

    private AircraftMroDbContext NewContext() => SqlServerFixture.CreateContext(_connectionString, _user, _clock);

    private WorkOrderService NewService(AircraftMroDbContext context) =>
        new(new WorkOrderRepository(context), _clock, new WorkOrderOptions());

    private async Task<Aircraft> AddAircraftAsync(string registration, AircraftStatus status)
    {
        var aircraft = Aircraft.Create(registration, "Airbus", "A320-214", registration, 2012, 0m, status, AircraftTestData.CurrentYear).Value!;
        await using var context = NewContext();
        Assert.True((await new AircraftRepository(context).AddAsync(aircraft, CancellationToken.None)).IsSuccess);
        return aircraft;
    }

    private async Task<Guid> CreateAsync(
        Guid aircraftId,
        WorkOrderPriority priority,
        string title = "Replace brake assembly",
        DateOnly? dueDate = null)
    {
        await using var context = NewContext();
        var service = NewService(context);
        var result = await service.CreateAsync(new CreateWorkOrderDto(aircraftId, title, null, priority, dueDate), CancellationToken.None);
        Assert.True(result.IsSuccess, result.ErrorMessage);
        return result.Value;
    }

    private async Task UpdateAsync(Guid id, WorkOrderStatus status, WorkOrderPriority priority)
    {
        var current = await GetAsync(id);
        await using var context = NewContext();
        var service = NewService(context);
        var result = await service.UpdateAsync(
            id,
            new UpdateWorkOrderDto(current.Title, current.Description, priority, status, current.DueDate, current.RowVersion),
            CancellationToken.None);
        Assert.True(result.IsSuccess, result.ErrorMessage);
    }

    private async Task<WorkOrderDto> GetAsync(Guid id)
    {
        await using var context = NewContext();
        return (await new WorkOrderRepository(context).GetByIdAsync(id, Today, CancellationToken.None))!;
    }

    private async Task<byte[]> WorkOrderRowVersionAsync(Guid id) => (await GetAsync(id)).RowVersion;

    private async Task<AircraftStatus> AircraftStatusAsync(Guid id)
    {
        await using var context = NewContext();
        return await context.Aircraft.AsNoTracking().Where(a => a.Id == id).Select(a => a.Status).SingleAsync();
    }

    private async Task<byte[]> AircraftRowVersionAsync(Guid id)
    {
        await using var context = NewContext();
        return (await new AircraftRepository(context).GetByIdAsync(id, CancellationToken.None))!.RowVersion;
    }
}
