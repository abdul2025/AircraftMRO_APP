using System.Globalization;
using AircraftMRO.Application.Features.Aircraft.DTOs;
using AircraftMRO.Domain.Enums.Aircraft;

namespace AircraftMRO.Web.Features.Aircraft.Models;

/// <summary>
/// Doughnut geometry for the fleet status chart. Segments are circles drawn with
/// stroke-dasharray, in fixed status order so each status keeps its color.
/// </summary>
public sealed record FleetStatusChartViewModel(
    int Total,
    IReadOnlyList<FleetStatusSegment> Segments,
    AircraftStatus? SelectedStatus)
{
    public const double Size = 160;
    public const double Radius = 62;
    public const double Thickness = 22;
    public const double Center = Size / 2;

    /// <summary>Surface-colored gap between touching segments.</summary>
    public const double Gap = 2;

    public static readonly double Circumference = 2 * Math.PI * Radius;

    public static FleetStatusChartViewModel From(AircraftStatisticsDto statistics, AircraftStatus? selectedStatus)
    {
        var total = statistics.TotalCount;
        var statuses = Enum.GetValues<AircraftStatus>();
        var nonEmpty = statuses.Count(status => statistics.CountFor(status) > 0);
        var gap = nonEmpty > 1 ? Gap : 0;

        var segments = new List<FleetStatusSegment>();
        var offset = 0d;
        foreach (var status in statuses)
        {
            var count = statistics.CountFor(status);
            var share = total == 0 ? 0 : (double)count / total;
            var arc = share * Circumference;
            var drawn = count == 0 ? 0 : Math.Max(arc - gap, 0.5);

            segments.Add(new FleetStatusSegment(status, count, share, drawn, offset));
            offset += arc;
        }

        return new FleetStatusChartViewModel(total, segments, selectedStatus);
    }

    public static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}

public sealed record FleetStatusSegment(
    AircraftStatus Status,
    int Count,
    double Share,
    double Length,
    double StartOffset)
{
    public string Label => AircraftStatusLabels.ToLabel(Status);

    public string Percent => Share.ToString("0%", CultureInfo.InvariantCulture);

    public string ColorClass => Status switch
    {
        AircraftStatus.Active => "chart-status--active",
        AircraftStatus.InMaintenance => "chart-status--maintenance",
        AircraftStatus.Grounded => "chart-status--grounded",
        _ => "chart-status--retired"
    };

    public string DashArray =>
        $"{FleetStatusChartViewModel.Number(Length)} {FleetStatusChartViewModel.Number(FleetStatusChartViewModel.Circumference - Length)}";

    /// <summary>Negative offset moves the dash start clockwise to where the previous segment ended.</summary>
    public string DashOffset => FleetStatusChartViewModel.Number(-StartOffset);
}
