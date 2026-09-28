using AircraftMRO.Domain.Enums.WorkOrders;
using AircraftMRO.Web.Models;

namespace AircraftMRO.Web.Features.WorkOrders.Models;

/// <summary>Open work orders by priority, most urgent first; each segment filters the list to it.</summary>
public static class WorkOrderPriorityChart
{
    public static readonly DonutChartText Text = new(
        Id: "work-order-priority-chart",
        Title: "Open work by priority",
        Subtitle: "Share of open work orders by priority",
        Category: "Priority",
        Caption: "open",
        UnitSingular: "open work order",
        UnitPlural: "open work orders");

    /// <param name="urlFor">The list URL that filters to open work orders of a priority.</param>
    public static DonutChartViewModel From(WorkOrderListViewModel model, Func<WorkOrderPriority, string> urlFor) =>
        DonutChartViewModel.Create(
            Text,
            WorkOrderLabels.PrioritiesByUrgency
                .Select(priority => new DonutSlice(
                    WorkOrderLabels.ToLabel(priority),
                    model.Statistics.OpenCountFor(priority),
                    WorkOrderLabels.ToChartColorClass(priority),
                    urlFor(priority),
                    model.IsFacet(priority: priority, openOnly: true)))
                .ToList());
}
