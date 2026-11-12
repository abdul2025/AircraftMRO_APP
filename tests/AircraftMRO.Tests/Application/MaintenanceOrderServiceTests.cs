using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.Aircraft.DTOs;
using AircraftMRO.Application.Features.MaintenanceOrders;
using AircraftMRO.Application.Features.MaintenanceOrders.DTOs;
using AircraftMRO.Application.Features.MaintenanceOrders.Ports;
using AircraftMRO.Application.Features.WorkOrders;
using AircraftMRO.Domain.Common.Results;
using AircraftMRO.Domain.Entities;
using AircraftMRO.Domain.Enums.Aircraft;
using AircraftMRO.Domain.Enums.MaintenanceOrders;
using AircraftMRO.Domain.Enums.WorkOrders;
using AircraftMRO.Tests.Support;
using Moq;

namespace AircraftMRO.Tests.Application;

public sealed class MaintenanceOrderServiceTests
{
    private static readonly Guid AircraftId = Guid.NewGuid();

    // Entities are never saved here, so their own ids stay empty; requests use this id instead.
    private static readonly Guid WorkOrderId = Guid.NewGuid();
    private readonly Mock<IMaintenanceOrderRepository> _repository = new();
    private readonly FakeTimeProvider _clock = new();
    private readonly MaintenanceOrderService _service;

    public MaintenanceOrderServiceTests()
    {
        _repository.Setup(r => r.AddAsync(It.IsAny<MaintenanceOrder>(), It.IsAny<WorkOrder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _repository.Setup(r => r.UpdateAsync(It.IsAny<MaintenanceOrder>(), It.IsAny<byte[]>(),
                It.IsAny<WorkOrder?>(), It.IsAny<Aircraft?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _repository.Setup(r => r.DeleteAsync(It.IsAny<MaintenanceOrder>(), It.IsAny<byte[]>(),
                It.IsAny<WorkOrder?>(), It.IsAny<Aircraft?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _repository.Setup(r => r.GetMaintenanceProgressAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MaintenanceProgressDto.None);
        _repository.Setup(r => r.GetOpenWorkOrderCountsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OpenWorkOrderCounts.None);
        _service = new MaintenanceOrderService(_repository.Object, _clock, new WorkOrderOptions());
    }

    [Fact]
    public async Task Create_schedules_under_an_open_work_order_and_saves_the_work_order_with_it()
    {
        var workOrder = TrackedWorkOrder();
        MaintenanceOrder? saved = null;
        _repository.Setup(r => r.AddAsync(It.IsAny<MaintenanceOrder>(), workOrder, It.IsAny<CancellationToken>()))
            .Callback<MaintenanceOrder, WorkOrder, CancellationToken>((m, _, _) => saved = m)
            .ReturnsAsync(Result.Success());

        var result = await _service.CreateAsync(
            MaintenanceOrderTestData.CreateDto(WorkOrderId, MaintenanceType.DCheck), CancellationToken.None);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(WorkOrderId, saved!.WorkOrderId);
        Assert.Equal(MaintenanceType.DCheck, saved.Type);
        Assert.Equal(MaintenanceStatus.Scheduled, saved.Status);
    }

    [Fact]
    public async Task Create_rejects_unknown_or_closed_work_orders_and_invalid_input()
    {
        var unknown = await _service.CreateAsync(MaintenanceOrderTestData.CreateDto(Guid.NewGuid()), CancellationToken.None);
        Assert.Equal(MaintenanceOrderErrors.WorkOrderNotFound, unknown.ErrorCode);

        TrackedWorkOrder(WorkOrderStatus.Cancelled);
        var onClosed = await _service.CreateAsync(MaintenanceOrderTestData.CreateDto(WorkOrderId), CancellationToken.None);
        Assert.Equal(MaintenanceOrderErrors.WorkOrderClosed, onClosed.ErrorCode);

        TrackedWorkOrder();
        var invalid = await _service.CreateAsync(
            MaintenanceOrderTestData.CreateDto(WorkOrderId) with { Type = (MaintenanceType)99 }, CancellationToken.None);
        Assert.Equal(MaintenanceOrderErrors.Validation, invalid.ErrorCode);

        _repository.Verify(r => r.AddAsync(It.IsAny<MaintenanceOrder>(), It.IsAny<WorkOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Edit_without_a_status_change_saves_with_the_callers_row_version_without_touching_the_work_order()
    {
        var maintenanceOrder = TrackedMaintenanceOrder();
        byte[] rowVersion = [7, 7];

        var result = await _service.UpdateAsync(
            maintenanceOrder.Id,
            MaintenanceOrderTestData.UpdateDto(MaintenanceStatus.Scheduled, MaintenanceType.CCheck, rowVersion),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        _repository.Verify(r => r.UpdateAsync(maintenanceOrder, rowVersion, null, null, It.IsAny<CancellationToken>()));
        _repository.Verify(r => r.GetWorkOrderForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Starting_maintenance_puts_the_work_order_in_progress_and_saves_it_without_the_aircraft()
    {
        var workOrder = TrackedWorkOrder();
        var maintenanceOrder = TrackedMaintenanceOrder();
        _repository.Setup(r => r.GetMaintenanceProgressAsync(WorkOrderId, maintenanceOrder.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MaintenanceProgressDto(Total: 1, InProgress: 0, Completed: 0));

        var result = await _service.UpdateAsync(
            maintenanceOrder.Id, MaintenanceOrderTestData.UpdateDto(MaintenanceStatus.InProgress), CancellationToken.None);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(WorkOrderStatus.InProgress, workOrder.Status);
        _repository.Verify(r => r.UpdateAsync(maintenanceOrder, It.IsAny<byte[]>(), workOrder, null, It.IsAny<CancellationToken>()));
        _repository.Verify(r => r.GetAircraftForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Completing_the_last_open_maintenance_completes_the_work_order_and_frees_the_aircraft()
    {
        var workOrder = TrackedWorkOrder();
        var aircraft = TrackedAircraftInMaintenance();
        var maintenanceOrder = TrackedMaintenanceOrder();
        _repository.Setup(r => r.GetMaintenanceProgressAsync(WorkOrderId, maintenanceOrder.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MaintenanceProgressDto(Total: 2, InProgress: 0, Completed: 2));

        var result = await _service.UpdateAsync(
            maintenanceOrder.Id, MaintenanceOrderTestData.UpdateDto(MaintenanceStatus.Completed), CancellationToken.None);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(_clock.Now, maintenanceOrder.CompletedAtUtc);
        Assert.Equal(WorkOrderStatus.Completed, workOrder.Status);
        Assert.Equal(_clock.Now, workOrder.CompletedAtUtc);
        Assert.Equal(AircraftStatus.Active, aircraft.Status);
        _repository.Verify(r => r.GetOpenWorkOrderCountsAsync(workOrder.AircraftId, workOrder.Id, It.IsAny<CancellationToken>()));
        _repository.Verify(r => r.UpdateAsync(maintenanceOrder, It.IsAny<byte[]>(), workOrder, aircraft, It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Completing_while_other_maintenance_is_open_keeps_the_work_order_open_but_still_saves_it()
    {
        var workOrder = TrackedWorkOrder();
        var maintenanceOrder = TrackedMaintenanceOrder();
        _repository.Setup(r => r.GetMaintenanceProgressAsync(WorkOrderId, maintenanceOrder.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MaintenanceProgressDto(Total: 1, InProgress: 0, Completed: 0));

        var result = await _service.UpdateAsync(
            maintenanceOrder.Id, MaintenanceOrderTestData.UpdateDto(MaintenanceStatus.Completed), CancellationToken.None);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(WorkOrderStatus.Open, workOrder.Status);
        // Saved anyway, so its row version check catches another maintenance change made at the same moment.
        _repository.Verify(r => r.UpdateAsync(maintenanceOrder, It.IsAny<byte[]>(), workOrder, null, It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Deleting_the_last_open_maintenance_completes_the_work_order_when_the_rest_is_completed()
    {
        var workOrder = TrackedWorkOrder();
        var aircraft = TrackedAircraftInMaintenance();
        var maintenanceOrder = TrackedMaintenanceOrder();
        _repository.Setup(r => r.GetMaintenanceProgressAsync(WorkOrderId, maintenanceOrder.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MaintenanceProgressDto(Total: 1, InProgress: 0, Completed: 1));

        var result = await _service.DeleteAsync(maintenanceOrder.Id, [1], CancellationToken.None);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(WorkOrderStatus.Completed, workOrder.Status);
        Assert.Equal(AircraftStatus.Active, aircraft.Status);
        _repository.Verify(r => r.DeleteAsync(maintenanceOrder, It.IsAny<byte[]>(), workOrder, aircraft, It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Deleting_the_only_maintenance_leaves_the_work_order_open()
    {
        var workOrder = TrackedWorkOrder();
        var maintenanceOrder = TrackedMaintenanceOrder();

        var result = await _service.DeleteAsync(maintenanceOrder.Id, [1], CancellationToken.None);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(WorkOrderStatus.Open, workOrder.Status);
        _repository.Verify(r => r.DeleteAsync(maintenanceOrder, It.IsAny<byte[]>(), workOrder, null, It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Update_rejects_completed_maintenance()
    {
        var maintenanceOrder = TrackedMaintenanceOrder();
        maintenanceOrder.Update(MaintenanceType.ACheck, MaintenanceOrderTestData.ScheduledDate, MaintenanceStatus.Completed, null, _clock.Now);

        Assert.Equal(MaintenanceOrderErrors.Closed,
            (await _service.UpdateAsync(maintenanceOrder.Id, MaintenanceOrderTestData.UpdateDto(), CancellationToken.None)).ErrorCode);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<MaintenanceOrder>(), It.IsAny<byte[]>(),
            It.IsAny<WorkOrder?>(), It.IsAny<Aircraft?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Work_order_option_is_not_found_for_unknown_work_order()
    {
        var result = await _service.GetWorkOrderOptionAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(MaintenanceOrderErrors.WorkOrderNotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData(MaintenanceStatus.InProgress)]
    [InlineData(MaintenanceStatus.Completed)]
    public async Task Only_scheduled_maintenance_can_be_deleted(MaintenanceStatus status)
    {
        var maintenanceOrder = TrackedMaintenanceOrder();
        maintenanceOrder.Update(MaintenanceType.ACheck, MaintenanceOrderTestData.ScheduledDate, status, null, _clock.Now);

        var result = await _service.DeleteAsync(maintenanceOrder.Id, [1], CancellationToken.None);

        Assert.Equal(MaintenanceOrderErrors.OnlyScheduledCanBeDeleted, result.ErrorCode);
        _repository.Verify(r => r.DeleteAsync(It.IsAny<MaintenanceOrder>(), It.IsAny<byte[]>(),
            It.IsAny<WorkOrder?>(), It.IsAny<Aircraft?>(), It.IsAny<CancellationToken>()), Times.Never);

        TrackedWorkOrder();
        var scheduled = TrackedMaintenanceOrder();
        Assert.True((await _service.DeleteAsync(scheduled.Id, [1], CancellationToken.None)).IsSuccess);
    }

    [Fact]
    public async Task Get_update_and_delete_return_not_found_for_unknown_id()
    {
        var id = Guid.NewGuid();

        Assert.Equal(MaintenanceOrderErrors.NotFound, (await _service.GetByIdAsync(id, CancellationToken.None)).ErrorCode);
        Assert.Equal(MaintenanceOrderErrors.NotFound,
            (await _service.UpdateAsync(id, MaintenanceOrderTestData.UpdateDto(), CancellationToken.None)).ErrorCode);
        Assert.Equal(MaintenanceOrderErrors.NotFound, (await _service.DeleteAsync(id, [1], CancellationToken.None)).ErrorCode);
    }

    [Fact]
    public async Task List_uses_today_in_the_business_time_zone()
    {
        _clock.Now = new DateTimeOffset(2026, 12, 31, 22, 30, 0, TimeSpan.Zero);
        var service = new MaintenanceOrderService(_repository.Object, _clock, new WorkOrderOptions { TimeZone = "Asia/Riyadh" });
        var request = new PagedRequest();

        await service.ListAsync(MaintenanceOrderListFilter.None, request, CancellationToken.None);

        _repository.Verify(r => r.ListAsync(MaintenanceOrderListFilter.None, request, new DateOnly(2027, 1, 1), It.IsAny<CancellationToken>()));

        await service.GetStatisticsAsync(WorkOrderId, CancellationToken.None);
        _repository.Verify(r => r.GetStatisticsAsync(WorkOrderId, new DateOnly(2027, 1, 1), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public void ListFilter_normalizes_input()
    {
        var filter = new MaintenanceOrderListFilter(Guid.Empty, (MaintenanceStatus)99, (MaintenanceType)99);

        Assert.Null(filter.WorkOrderId);
        Assert.Null(filter.Status);
        Assert.Null(filter.Type);
        Assert.True(filter.IsEmpty);
        Assert.False(new MaintenanceOrderListFilter(overdueOnly: true).IsEmpty);
    }

    private WorkOrder TrackedWorkOrder(WorkOrderStatus status = WorkOrderStatus.Open)
    {
        var workOrder = WorkOrderTestData.NewWorkOrder(AircraftId);
        if (status != WorkOrderStatus.Open)
        {
            workOrder.Update(workOrder.Title, null, workOrder.Priority, status, null, 0, _clock.Now);
        }

        _repository.Setup(r => r.GetWorkOrderForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(workOrder);
        return workOrder;
    }

    /// <summary>An aircraft held in maintenance by the tracked work order, its only open one.</summary>
    private Aircraft TrackedAircraftInMaintenance()
    {
        var aircraft = AircraftTestData.NewAircraft();
        aircraft.ApplyOpenWorkOrders(1, anyCritical: false);
        Assert.Equal(AircraftStatus.InMaintenance, aircraft.Status);
        _repository.Setup(r => r.GetAircraftForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(aircraft);
        return aircraft;
    }

    private MaintenanceOrder TrackedMaintenanceOrder()
    {
        var maintenanceOrder = MaintenanceOrderTestData.NewMaintenanceOrder(WorkOrderId);
        _repository.Setup(r => r.GetForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(maintenanceOrder);
        return maintenanceOrder;
    }
}
