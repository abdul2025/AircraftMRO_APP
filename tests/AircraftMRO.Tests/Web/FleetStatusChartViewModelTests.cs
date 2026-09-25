using AircraftMRO.Application.Features.Aircraft.DTOs;
using AircraftMRO.Domain.Enums.Aircraft;
using AircraftMRO.Web.Features.Aircraft.Models;

namespace AircraftMRO.Tests.Web;

public sealed class FleetStatusChartViewModelTests
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

    [Fact]
    public void Segments_follow_fixed_status_order_and_fill_the_ring_with_gaps()
    {
        var chart = FleetStatusChartViewModel.From(Stats(5, 3, 2, 0), selectedStatus: null);

        Assert.Equal(Enum.GetValues<AircraftStatus>(), chart.Segments.Select(s => s.Status));
        var drawn = chart.Segments.Where(s => s.Count > 0).ToList();
        Assert.Equal(3, drawn.Count);
        // Drawn arcs plus one surface gap per visible segment close the ring exactly.
        Assert.Equal(FleetStatusChartViewModel.Circumference,
            drawn.Sum(s => s.Length) + drawn.Count * FleetStatusChartViewModel.Gap, precision: 6);
        // Each segment starts where the previous one's share ended.
        Assert.Equal(0, drawn[0].StartOffset);
        Assert.Equal(0.5 * FleetStatusChartViewModel.Circumference, drawn[1].StartOffset, precision: 6);
        Assert.Equal(["50%", "30%", "20%", "0%"], chart.Segments.Select(s => s.Percent));
    }

    [Fact]
    public void A_single_status_is_a_full_ring_without_a_gap()
    {
        var chart = FleetStatusChartViewModel.From(Stats(0, 4, 0, 0), selectedStatus: AircraftStatus.InMaintenance);

        var segment = Assert.Single(chart.Segments, s => s.Count > 0);
        Assert.Equal(FleetStatusChartViewModel.Circumference, segment.Length, precision: 6);
        Assert.Equal(AircraftStatus.InMaintenance, chart.SelectedStatus);
    }

    [Fact]
    public void An_empty_fleet_draws_no_segments()
    {
        var chart = FleetStatusChartViewModel.From(AircraftStatisticsDto.Empty, selectedStatus: null);

        Assert.Equal(0, chart.Total);
        Assert.All(chart.Segments, s => Assert.Equal(0, s.Length));
    }

    [Fact]
    public void A_tiny_share_stays_visible()
    {
        var chart = FleetStatusChartViewModel.From(Stats(999, 0, 1, 0), selectedStatus: null);

        Assert.True(chart.Segments.Single(s => s.Status == AircraftStatus.Grounded).Length > 0);
    }
}
