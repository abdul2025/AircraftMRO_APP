using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.Aircraft.DTOs;
using AircraftMRO.Application.Features.WorkOrders.DTOs;
using AircraftMRO.Domain.Enums.Aircraft;
using AircraftMRO.Domain.Enums.WorkOrders;
using AircraftMRO.Web.Features.Aircraft.Models;
using AircraftMRO.Web.Features.WorkOrders.Models;
using AircraftMRO.Web.Models;

namespace AircraftMRO.Tests.Web;

public sealed class DonutChartViewModelTests
{
    private static AircraftStatisticsDto Stats(int active, int maintenance, int grounded, int retired) =>
        new(active + maintenance + grounded + retired,
            new Dictionary<AircraftStatus, int>
            {
                [AircraftStatus.Active] = active,
                [AircraftStatus.InMaintenance] = maintenance,
                [AircraftStatus.Grounded] = grounded,
                [AircraftStatus.Retired] = retired
            },
            0m);

    private static DonutChartViewModel Fleet(AircraftStatisticsDto statistics, AircraftStatus? selected = null) =>
        FleetStatusChart.From(statistics, selected, status => $"/Aircraft?status={status}");

    [Fact]
    public void Segments_follow_slice_order_and_fill_the_ring_with_gaps()
    {
        var chart = Fleet(Stats(5, 3, 2, 0));

        Assert.Equal(["Active", "In maintenance", "Grounded", "Retired"], chart.Segments.Select(s => s.Label));
        var drawn = chart.Segments.Where(s => s.Count > 0).ToList();
        Assert.Equal(3, drawn.Count);
        // Drawn arcs plus one surface gap per visible segment close the ring exactly.
        Assert.Equal(DonutChartViewModel.Circumference,
            drawn.Sum(s => s.Length) + drawn.Count * DonutChartViewModel.Gap, precision: 6);
        // Each segment starts where the previous one's share ended.
        Assert.Equal(0, drawn[0].StartOffset);
        Assert.Equal(0.5 * DonutChartViewModel.Circumference, drawn[1].StartOffset, precision: 6);
        Assert.Equal(["50%", "30%", "20%", "0%"], chart.Segments.Select(s => s.Percent));
    }

    [Fact]
    public void A_single_category_is_a_full_ring_without_a_gap()
    {
        var chart = Fleet(Stats(0, 4, 0, 0), selected: AircraftStatus.InMaintenance);

        var segment = Assert.Single(chart.Segments, s => s.Count > 0);
        Assert.Equal(DonutChartViewModel.Circumference, segment.Length, precision: 6);
        Assert.True(segment.IsSelected);
        Assert.True(chart.HasSelection);
    }

    [Fact]
    public void An_empty_chart_draws_no_segments()
    {
        var chart = Fleet(AircraftStatisticsDto.Empty);

        Assert.Equal(0, chart.Total);
        Assert.False(chart.HasSelection);
        Assert.All(chart.Segments, s => Assert.Equal(0, s.Length));
    }

    [Fact]
    public void A_tiny_share_stays_visible()
    {
        var chart = Fleet(Stats(999, 0, 1, 0));

        Assert.True(chart.Segments.Single(s => s.Label == "Grounded").Length > 0);
    }

    [Fact]
    public void Segments_carry_their_filter_url_and_color()
    {
        var chart = Fleet(Stats(1, 1, 1, 1));

        var grounded = chart.Segments.Single(s => s.Label == "Grounded");
        Assert.Equal("/Aircraft?status=Grounded", grounded.Url);
        Assert.Equal("danger", grounded.Role);
        Assert.Equal("chart-role--danger", grounded.ColorClass);
        Assert.Equal("1 aircraft", chart.CountText(1));
    }

    [Fact]
    public void Status_chart_counts_work_by_status_and_selects_the_status_tile_facet()
    {
        var statistics = new WorkOrderStatisticsDto(
            TotalCount: 9,
            CountByStatus: new Dictionary<WorkOrderStatus, int>
            {
                [WorkOrderStatus.Open] = 4,
                [WorkOrderStatus.InProgress] = 2,
                [WorkOrderStatus.Completed] = 3
            },
            OpenCountByPriority: new Dictionary<WorkOrderPriority, int>(),
            OverdueCount: 0);
        var model = new WorkOrderListViewModel(
            new PagedResponse<WorkOrderListItemDto>([], 1, 20, 0),
            statistics,
            new WorkOrderListFilter(status: WorkOrderStatus.InProgress),
            []);

        var chart = WorkOrderStatusChart.From(model, status => $"/WorkOrders?status={status}");

        Assert.Equal(["Open", "In progress", "On hold", "Completed", "Cancelled"], chart.Segments.Select(s => s.Label));
        Assert.Equal([4, 2, 0, 3, 0], chart.Segments.Select(s => s.Count));
        Assert.Equal(9, chart.Total);
        Assert.Equal("In progress", Assert.Single(chart.Segments, s => s.IsSelected).Label);
        Assert.Equal("1 work order", chart.CountText(1));
        Assert.Equal("2 work orders", chart.CountText(2));
    }

    [Fact]
    public void Status_chart_has_no_selection_when_a_tile_other_than_a_status_is_active()
    {
        var statistics = new WorkOrderStatisticsDto(
            TotalCount: 1,
            CountByStatus: new Dictionary<WorkOrderStatus, int> { [WorkOrderStatus.Open] = 1 },
            OpenCountByPriority: new Dictionary<WorkOrderPriority, int> { [WorkOrderPriority.Critical] = 1 },
            OverdueCount: 0);
        var model = new WorkOrderListViewModel(
            new PagedResponse<WorkOrderListItemDto>([], 1, 20, 0),
            statistics,
            new WorkOrderListFilter(priority: WorkOrderPriority.Critical, openOnly: true),
            []);

        var chart = WorkOrderStatusChart.From(model, status => $"/WorkOrders?status={status}");

        Assert.False(chart.HasSelection);
    }
}
