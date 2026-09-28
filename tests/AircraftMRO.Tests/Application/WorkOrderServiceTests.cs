using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.Aircraft.DTOs;
using AircraftMRO.Application.Features.WorkOrders;
using AircraftMRO.Application.Features.WorkOrders.DTOs;
using AircraftMRO.Application.Features.WorkOrders.Ports;
using AircraftMRO.Domain.Common.Results;
using AircraftMRO.Domain.Entities;
using AircraftMRO.Domain.Enums.Aircraft;
using AircraftMRO.Domain.Enums.WorkOrders;
using AircraftMRO.Tests.Support;
using Moq;

namespace AircraftMRO.Tests.Application;

public sealed class WorkOrderServiceTests
{
    private static readonly Guid AircraftId = Guid.NewGuid();
    private readonly Mock<IWorkOrderRepository> _repository = new();
    private readonly FakeTimeProvider _clock = new();
    private readonly WorkOrderService _service;

    public WorkOrderServiceTests()
    {
        _repository.Setup(r => r.NextNumberAsync(It.IsAny<CancellationToken>())).ReturnsAsync("WO-000042");
        _repository.Setup(r => r.GetOpenWorkOrderCountsAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OpenWorkOrderCounts.None);
        _repository.Setup(r => r.AddAsync(It.IsAny<WorkOrder>(), It.IsAny<Aircraft>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _repository.Setup(r => r.UpdateAsync(It.IsAny<WorkOrder>(), It.IsAny<byte[]>(), It.IsAny<Aircraft>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _repository.Setup(r => r.DeleteAsync(It.IsAny<WorkOrder>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _service = new WorkOrderService(_repository.Object, _clock, new WorkOrderOptions());
    }

    [Fact]
    public async Task Create_critical_work_order_grounds_the_aircraft_in_the_same_save()
    {
        var aircraft = AircraftWithStatus(AircraftStatus.Active);
        WorkOrder? saved = null;
        Aircraft? savedAircraft = null;
        _repository.Setup(r => r.AddAsync(It.IsAny<WorkOrder>(), It.IsAny<Aircraft>(), It.IsAny<CancellationToken>()))
            .Callback<WorkOrder, Aircraft, CancellationToken>((w, a, _) => (saved, savedAircraft) = (w, a))
            .ReturnsAsync(Result.Success());

        var result = await _service.CreateAsync(WorkOrderTestData.CreateDto(AircraftId, WorkOrderPriority.Critical), CancellationToken.None);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal("WO-000042", saved!.Number);
        Assert.Equal(WorkOrderStatus.Open, saved.Status);
        Assert.Same(aircraft, savedAircraft);
        Assert.Equal(AircraftStatus.Grounded, aircraft.Status);
    }

    [Theory]
    [InlineData(AircraftStatus.Active, 0, 0, AircraftStatus.InMaintenance)]
    [InlineData(AircraftStatus.Grounded, 0, 0, AircraftStatus.InMaintenance)]   // manual grounding: work orders decide
    [InlineData(AircraftStatus.Grounded, 1, 1, AircraftStatus.Grounded)]        // another critical is still open
    public async Task Create_non_critical_work_order_sets_status_from_all_open_work(
        AircraftStatus before,
        int otherOpen,
        int otherCritical,
        AircraftStatus after)
    {
        var aircraft = AircraftWithStatus(before);
        OthersOpen(otherOpen, otherCritical);

        var result = await _service.CreateAsync(WorkOrderTestData.CreateDto(AircraftId, WorkOrderPriority.High), CancellationToken.None);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(after, aircraft.Status);
        _repository.Verify(r => r.GetOpenWorkOrderCountsAsync(AircraftId, null, It.IsAny<CancellationToken>()));
        _repository.Verify(r => r.AddAsync(It.IsAny<WorkOrder>(), aircraft, It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Create_rejects_retired_aircraft_without_reserving_a_number()
    {
        AircraftWithStatus(AircraftStatus.Retired);

        var result = await _service.CreateAsync(WorkOrderTestData.CreateDto(AircraftId), CancellationToken.None);

        Assert.Equal(WorkOrderErrors.AircraftRetired, result.ErrorCode);
        _repository.Verify(r => r.NextNumberAsync(It.IsAny<CancellationToken>()), Times.Never);
        _repository.Verify(r => r.AddAsync(It.IsAny<WorkOrder>(), It.IsAny<Aircraft>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_rejects_unknown_aircraft_and_invalid_input()
    {
        Assert.Equal(WorkOrderErrors.AircraftNotFound,
            (await _service.CreateAsync(WorkOrderTestData.CreateDto(AircraftId), CancellationToken.None)).ErrorCode);

        AircraftWithStatus(AircraftStatus.Active);
        var invalid = await _service.CreateAsync(WorkOrderTestData.CreateDto(AircraftId) with { Title = " " }, CancellationToken.None);
        Assert.Equal(WorkOrderErrors.Validation, invalid.ErrorCode);
        _repository.Verify(r => r.AddAsync(It.IsAny<WorkOrder>(), It.IsAny<Aircraft>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(WorkOrderPriority.Medium, WorkOrderPriority.Critical, AircraftStatus.InMaintenance, AircraftStatus.Grounded)]
    [InlineData(WorkOrderPriority.Critical, WorkOrderPriority.High, AircraftStatus.Grounded, AircraftStatus.InMaintenance)]
    [InlineData(WorkOrderPriority.Critical, WorkOrderPriority.Low, AircraftStatus.Grounded, AircraftStatus.InMaintenance)]
    public async Task Changing_priority_moves_the_aircraft_both_ways(
        WorkOrderPriority from,
        WorkOrderPriority to,
        AircraftStatus before,
        AircraftStatus after)
    {
        var aircraft = AircraftWithStatus(before);
        var workOrder = TrackedWorkOrder(from);
        byte[] rowVersion = [7, 7];

        var result = await _service.UpdateAsync(
            workOrder.Id, WorkOrderTestData.UpdateDto(WorkOrderStatus.InProgress, to, rowVersion), CancellationToken.None);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(after, aircraft.Status);
        _repository.Verify(r => r.GetOpenWorkOrderCountsAsync(AircraftId, workOrder.Id, It.IsAny<CancellationToken>()));
        _repository.Verify(r => r.UpdateAsync(workOrder, rowVersion, aircraft, It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Lowering_one_critical_keeps_the_aircraft_grounded_while_another_critical_is_open()
    {
        var aircraft = AircraftWithStatus(AircraftStatus.Grounded);
        var workOrder = TrackedWorkOrder(WorkOrderPriority.Critical);
        OthersOpen(total: 1, critical: 1);

        await _service.UpdateAsync(workOrder.Id, WorkOrderTestData.UpdateDto(priority: WorkOrderPriority.High), CancellationToken.None);

        Assert.Equal(AircraftStatus.Grounded, aircraft.Status);
    }

    [Theory]
    [InlineData(WorkOrderStatus.Completed, 0, 0, AircraftStatus.Active)]
    [InlineData(WorkOrderStatus.Cancelled, 0, 0, AircraftStatus.Active)]
    [InlineData(WorkOrderStatus.Completed, 2, 0, AircraftStatus.InMaintenance)]
    [InlineData(WorkOrderStatus.Cancelled, 1, 1, AircraftStatus.Grounded)]
    public async Task Closing_a_work_order_sets_status_from_the_work_still_open(
        WorkOrderStatus closedStatus,
        int otherOpen,
        int otherCritical,
        AircraftStatus after)
    {
        var aircraft = AircraftWithStatus(AircraftStatus.Grounded);
        var workOrder = TrackedWorkOrder(WorkOrderPriority.Critical);
        OthersOpen(otherOpen, otherCritical);

        var result = await _service.UpdateAsync(
            workOrder.Id, WorkOrderTestData.UpdateDto(closedStatus, WorkOrderPriority.Critical), CancellationToken.None);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(after, aircraft.Status);
        Assert.Equal(closedStatus == WorkOrderStatus.Completed ? _clock.Now : null, workOrder.CompletedAtUtc);
        _repository.Verify(r => r.UpdateAsync(workOrder, It.IsAny<byte[]>(), aircraft, It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Update_passes_through_closed_and_concurrency_failures()
    {
        AircraftWithStatus(AircraftStatus.InMaintenance);
        var workOrder = TrackedWorkOrder(WorkOrderPriority.Low);
        _repository.Setup(r => r.UpdateAsync(workOrder, It.IsAny<byte[]>(), It.IsAny<Aircraft>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(WorkOrderErrors.ConcurrencyConflict, WorkOrderErrors.ConcurrencyConflictMessage));

        Assert.Equal(WorkOrderErrors.ConcurrencyConflict,
            (await _service.UpdateAsync(workOrder.Id, WorkOrderTestData.UpdateDto(), CancellationToken.None)).ErrorCode);

        workOrder.Update("Done", null, WorkOrderPriority.Low, WorkOrderStatus.Cancelled, null, _clock.Now);
        Assert.Equal(WorkOrderErrors.Closed,
            (await _service.UpdateAsync(workOrder.Id, WorkOrderTestData.UpdateDto(), CancellationToken.None)).ErrorCode);
    }

    [Fact]
    public async Task Open_work_orders_cannot_be_deleted_but_closed_ones_can()
    {
        var workOrder = TrackedWorkOrder(WorkOrderPriority.Critical);

        var open = await _service.DeleteAsync(workOrder.Id, [1], CancellationToken.None);
        Assert.Equal(WorkOrderErrors.OpenCannotBeDeleted, open.ErrorCode);
        _repository.Verify(r => r.DeleteAsync(It.IsAny<WorkOrder>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);

        workOrder.Update("Done", null, WorkOrderPriority.Critical, WorkOrderStatus.Completed, null, _clock.Now);
        Assert.True((await _service.DeleteAsync(workOrder.Id, [1], CancellationToken.None)).IsSuccess);
    }

    [Fact]
    public async Task Get_update_and_delete_return_not_found_for_unknown_id()
    {
        var id = Guid.NewGuid();

        Assert.Equal(WorkOrderErrors.NotFound, (await _service.GetByIdAsync(id, CancellationToken.None)).ErrorCode);
        Assert.Equal(WorkOrderErrors.NotFound, (await _service.UpdateAsync(id, WorkOrderTestData.UpdateDto(), CancellationToken.None)).ErrorCode);
        Assert.Equal(WorkOrderErrors.NotFound, (await _service.DeleteAsync(id, [1], CancellationToken.None)).ErrorCode);
    }

    [Fact]
    public async Task Reads_use_today_from_the_time_provider()
    {
        _clock.Now = new DateTimeOffset(2026, 12, 31, 23, 30, 0, TimeSpan.Zero);
        var filter = new WorkOrderListFilter(overdueOnly: true);
        var request = new PagedRequest();
        var today = new DateOnly(2026, 12, 31);
        var page = new PagedResponse<WorkOrderListItemDto>([], 1, 20, 0);
        _repository.Setup(r => r.ListAsync(filter, request, today, It.IsAny<CancellationToken>())).ReturnsAsync(page);
        _repository.Setup(r => r.GetStatisticsAsync(AircraftId, today, It.IsAny<CancellationToken>())).ReturnsAsync(WorkOrderStatisticsDto.Empty);

        Assert.Same(page, (await _service.ListAsync(filter, request, CancellationToken.None)).Value);
        Assert.Same(WorkOrderStatisticsDto.Empty, (await _service.GetStatisticsAsync(AircraftId, CancellationToken.None)).Value);
    }

    [Theory]
    [InlineData("UTC", "2026-12-31")]
    [InlineData("Asia/Riyadh", "2027-01-01")]        // UTC+3: already past local midnight
    [InlineData("America/New_York", "2026-12-31")]
    public async Task Today_follows_the_configured_business_time_zone(string timeZone, string expectedToday)
    {
        _clock.Now = new DateTimeOffset(2026, 12, 31, 22, 30, 0, TimeSpan.Zero);
        var service = new WorkOrderService(_repository.Object, _clock, new WorkOrderOptions { TimeZone = timeZone });
        var request = new PagedRequest();

        await service.ListAsync(WorkOrderListFilter.None, request, CancellationToken.None);

        _repository.Verify(r => r.ListAsync(WorkOrderListFilter.None, request, DateOnly.Parse(expectedToday), It.IsAny<CancellationToken>()));
    }

    [Theory]
    [InlineData("UTC", true)]
    [InlineData("Asia/Riyadh", true)]
    [InlineData("Not/AZone", false)]
    [InlineData("", false)]
    public void Options_accept_only_known_time_zones(string timeZone, bool expected) =>
        Assert.Equal(expected, WorkOrderOptions.IsValid(new WorkOrderOptions { TimeZone = timeZone }));

    [Fact]
    public void ListFilter_normalizes_input()
    {
        var filter = new WorkOrderListFilter("  wo-1 ", (WorkOrderStatus)99, (WorkOrderPriority)99, Guid.Empty);

        Assert.Equal("wo-1", filter.Search);
        Assert.Null(filter.Status);
        Assert.Null(filter.Priority);
        Assert.Null(filter.AircraftId);
        Assert.False(filter.IsEmpty);
        Assert.True(new WorkOrderListFilter("   ").IsEmpty);
        Assert.False(new WorkOrderListFilter(overdueOnly: true).IsEmpty);
    }

    private Aircraft AircraftWithStatus(AircraftStatus status)
    {
        var aircraft = Aircraft.Create("HZ-ABC", "Airbus", "A320-214", "5123", 2012, 0m, status, AircraftTestData.CurrentYear).Value!;
        _repository.Setup(r => r.GetAircraftForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(aircraft);
        return aircraft;
    }

    private void OthersOpen(int total, int critical) =>
        _repository.Setup(r => r.GetOpenWorkOrderCountsAsync(AircraftId, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OpenWorkOrderCounts(total, critical));

    private WorkOrder TrackedWorkOrder(WorkOrderPriority priority)
    {
        var workOrder = WorkOrderTestData.NewWorkOrder(AircraftId, priority);
        _repository.Setup(r => r.GetForUpdateAsync(workOrder.Id, It.IsAny<CancellationToken>())).ReturnsAsync(workOrder);
        return workOrder;
    }
}
