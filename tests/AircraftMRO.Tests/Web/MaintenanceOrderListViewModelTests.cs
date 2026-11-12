using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.MaintenanceOrders.DTOs;
using AircraftMRO.Domain.Enums.MaintenanceOrders;
using AircraftMRO.Web.Features.MaintenanceOrders.Models;

namespace AircraftMRO.Tests.Web;

public sealed class MaintenanceOrderListViewModelTests
{
    private static readonly Guid WorkOrderId = Guid.NewGuid();

    [Fact]
    public void Facet_label_names_status_type_and_overdue()
    {
        Assert.Null(Model(new MaintenanceOrderListFilter(WorkOrderId)).FacetLabel);
        Assert.Equal("In progress", Model(new(status: MaintenanceStatus.InProgress)).FacetLabel);
        Assert.Equal("C-check · Overdue", Model(new(type: MaintenanceType.CCheck, openOnly: true, overdueOnly: true)).FacetLabel);
        Assert.Equal("Open only", Model(new(openOnly: true)).FacetLabel);
    }

    [Fact]
    public void Tile_links_keep_the_work_order_scope_but_replace_the_facets()
    {
        var model = Model(new(WorkOrderId, MaintenanceStatus.Scheduled, MaintenanceType.ACheck, openOnly: true));

        var tile = model.RouteValues(overdueOnly: true, keepFacets: false);
        Assert.Equal(new Dictionary<string, string> { ["workOrderId"] = WorkOrderId.ToString(), ["overdue"] = "true" }, tile);

        var pager = model.RouteValues(page: 2);
        Assert.Equal("Scheduled", pager["status"]);
        Assert.Equal("ACheck", pager["type"]);
        Assert.Equal("true", pager["open"]);
        Assert.Equal("2", pager["page"]);
    }

    [Fact]
    public void Is_facet_matches_only_the_exact_facets()
    {
        var model = Model(new(status: MaintenanceStatus.Completed));

        Assert.True(model.IsFacet(status: MaintenanceStatus.Completed));
        Assert.False(model.IsFacet());
        Assert.False(model.IsFacet(status: MaintenanceStatus.Completed, openOnly: true));
    }

    [Fact]
    public void Selected_work_order_missing_from_the_capped_list_is_added_to_the_filter_options()
    {
        var listed = new WorkOrderOptionDto(Guid.NewGuid(), "WO-000002", "HZ-ABC", true);
        var old = new WorkOrderOptionDto(WorkOrderId, "WO-000001", "HZ-ABC", false);

        var model = Model(new(WorkOrderId), [listed], old);

        Assert.Equal([old, listed], model.FilterOptions);
        Assert.False(model.CanSchedule);
    }

    [Fact]
    public void Status_chart_slices_match_the_statistics_and_mark_the_selected_status()
    {
        var statistics = new MaintenanceOrderStatisticsDto(
            4,
            new Dictionary<MaintenanceStatus, int> { [MaintenanceStatus.Scheduled] = 3, [MaintenanceStatus.Completed] = 1 },
            1);
        var model = Model(new(status: MaintenanceStatus.Scheduled)) with { Statistics = statistics };

        var chart = MaintenanceOrderStatusChart.From(model, status => $"/MaintenanceOrders?status={status}");

        Assert.Equal(4, chart.Total);
        Assert.Equal(["Scheduled", "In progress", "Completed"], chart.Segments.Select(s => s.Label));
        Assert.Equal([3, 0, 1], chart.Segments.Select(s => s.Count));
        Assert.True(chart.Segments[0].IsSelected);
        Assert.Equal(3, statistics.OpenCount);
    }

    private static MaintenanceOrderListViewModel Model(
        MaintenanceOrderListFilter filter,
        IReadOnlyList<WorkOrderOptionDto>? workOrders = null,
        WorkOrderOptionDto? selected = null) =>
        new(new PagedResponse<MaintenanceOrderListItemDto>([], 1, 20, 0),
            MaintenanceOrderStatisticsDto.Empty,
            filter,
            workOrders ?? [],
            selected);
}
