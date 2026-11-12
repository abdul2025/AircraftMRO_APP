using AircraftMRO.Domain.Enums.MaintenanceOrders;
using AircraftMRO.Web.Models;

namespace AircraftMRO.Web.Features.MaintenanceOrders.Models;

/// <summary>
/// Maintenance by status, in fixed order so each keeps its color. The same breakdown fills the
/// stat panel rows, so both always agree.
/// </summary>
public static class MaintenanceOrderStatusChart
{
    public static readonly DonutChartText Text = new(
        Id: "maintenance-status-chart",
        Title: "Maintenance status",
        Subtitle: "Share of maintenance orders by status",
        Category: "Status",
        Caption: "maintenance",
        UnitSingular: "maintenance order",
        UnitPlural: "maintenance orders");

    /// <param name="urlFor">The list URL that filters to a status.</param>
    public static DonutChartViewModel From(MaintenanceOrderListViewModel model, Func<MaintenanceStatus, string> urlFor) =>
        DonutChartViewModel.Create(
            Text,
            Enum.GetValues<MaintenanceStatus>()
                .Select(status => new DonutSlice(
                    MaintenanceOrderLabels.ToLabel(status),
                    model.Statistics.CountFor(status),
                    MaintenanceOrderLabels.ToCssModifier(status),
                    urlFor(status),
                    model.IsFacet(status: status)))
                .ToList());
}
