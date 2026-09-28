using AircraftMRO.Domain.Enums.WorkOrders;
using AircraftMRO.Web.Models;

namespace AircraftMRO.Web.Features.WorkOrders.Models;

/// <summary>
/// Work orders by status, in fixed order so each keeps its color. Slices mirror the status tiles:
/// same counts, same links, and the slice for the selected tile is highlighted.
/// </summary>
public static class WorkOrderStatusChart
{
    public static readonly DonutChartText Text = new(
        Id: "work-order-status-chart",
        Title: "Work order status",
        Subtitle: "Share of work orders by status",
        Category: "Status",
        Caption: "work orders",
        UnitSingular: "work order",
        UnitPlural: "work orders");

    /// <param name="urlFor">The list URL that filters to a status.</param>
    public static DonutChartViewModel From(WorkOrderListViewModel model, Func<WorkOrderStatus, string> urlFor) =>
        DonutChartViewModel.Create(
            Text,
            Enum.GetValues<WorkOrderStatus>()
                .Select(status => new DonutSlice(
                    WorkOrderLabels.ToLabel(status),
                    model.Statistics.CountFor(status),
                    WorkOrderLabels.ToCssModifier(status),
                    urlFor(status),
                    model.IsFacet(status: status)))
                .ToList());
}
