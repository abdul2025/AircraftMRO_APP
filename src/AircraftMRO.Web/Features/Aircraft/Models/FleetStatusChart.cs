using AircraftMRO.Application.Features.Aircraft.DTOs;
using AircraftMRO.Domain.Enums.Aircraft;
using AircraftMRO.Web.Models;

namespace AircraftMRO.Web.Features.Aircraft.Models;

/// <summary>The fleet status doughnut: one slice per status, in fixed order so each keeps its color.</summary>
public static class FleetStatusChart
{
    public static readonly DonutChartText Text = new(
        Id: "fleet-status-chart",
        Title: "Fleet status",
        Subtitle: "Share of aircraft by status",
        Category: "Status",
        Caption: "aircraft",
        UnitSingular: "aircraft",
        UnitPlural: "aircraft");

    /// <param name="urlFor">The list URL that filters to a status.</param>
    public static DonutChartViewModel From(
        AircraftStatisticsDto statistics,
        AircraftStatus? selectedStatus,
        Func<AircraftStatus, string> urlFor) =>
        DonutChartViewModel.Create(
            Text,
            Enum.GetValues<AircraftStatus>()
                .Select(status => new DonutSlice(
                    AircraftStatusLabels.ToLabel(status),
                    statistics.CountFor(status),
                    ColorClass(status),
                    urlFor(status),
                    selectedStatus == status))
                .ToList());

    public static string ColorClass(AircraftStatus status) => status switch
    {
        AircraftStatus.Active => "chart-status--active",
        AircraftStatus.InMaintenance => "chart-status--maintenance",
        AircraftStatus.Grounded => "chart-status--grounded",
        _ => "chart-status--retired"
    };
}
