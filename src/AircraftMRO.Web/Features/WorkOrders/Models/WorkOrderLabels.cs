using System.Globalization;
using AircraftMRO.Domain.Enums.WorkOrders;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AircraftMRO.Web.Features.WorkOrders.Models;

public static class WorkOrderLabels
{
    /// <summary>Most urgent first, the order used by the priority chart and select lists.</summary>
    public static readonly IReadOnlyList<WorkOrderPriority> PrioritiesByUrgency =
        [WorkOrderPriority.Critical, WorkOrderPriority.High, WorkOrderPriority.Medium, WorkOrderPriority.Low];

    public static string ToLabel(WorkOrderStatus status) => status switch
    {
        WorkOrderStatus.Open => "Open",
        WorkOrderStatus.InProgress => "In progress",
        WorkOrderStatus.OnHold => "On hold",
        WorkOrderStatus.Completed => "Completed",
        WorkOrderStatus.Cancelled => "Cancelled",
        _ => status.ToString()
    };

    public static string ToLabel(WorkOrderPriority priority) => priority switch
    {
        WorkOrderPriority.Low => "Low",
        WorkOrderPriority.Medium => "Medium",
        WorkOrderPriority.High => "High",
        WorkOrderPriority.Critical => "Critical",
        _ => priority.ToString()
    };

    /// <summary>Badge and status-dot modifier; "neutral" has no modifier rule and uses the base style.</summary>
    public static string ToCssModifier(WorkOrderStatus status) => status switch
    {
        WorkOrderStatus.Open => "info",
        WorkOrderStatus.InProgress => "warning",
        WorkOrderStatus.Completed => "success",
        _ => "neutral"
    };

    public static string ToCssModifier(WorkOrderPriority priority) => priority switch
    {
        WorkOrderPriority.Critical => "danger",
        WorkOrderPriority.High => "warning",
        WorkOrderPriority.Medium => "info",
        _ => "neutral"
    };

    public static string ToChartColorClass(WorkOrderPriority priority) => priority switch
    {
        WorkOrderPriority.Critical => "chart-priority--critical",
        WorkOrderPriority.High => "chart-priority--high",
        WorkOrderPriority.Medium => "chart-priority--medium",
        _ => "chart-priority--low"
    };

    public static IEnumerable<SelectListItem> StatusOptions() =>
        Enum.GetValues<WorkOrderStatus>().Select(status => new SelectListItem(ToLabel(status), status.ToString()));

    public static IEnumerable<SelectListItem> PriorityOptions() =>
        PrioritiesByUrgency.Select(priority => new SelectListItem(ToLabel(priority), priority.ToString()));

    public static string Date(DateOnly value) => value.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    public static string MachineDate(DateOnly value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
