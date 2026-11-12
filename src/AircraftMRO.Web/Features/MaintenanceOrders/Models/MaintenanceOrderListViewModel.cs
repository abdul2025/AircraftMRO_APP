using System.Globalization;
using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.MaintenanceOrders.DTOs;
using AircraftMRO.Domain.Enums.MaintenanceOrders;

namespace AircraftMRO.Web.Features.MaintenanceOrders.Models;

/// <summary>
/// The list page. The work order is the scope and is kept on every link; the "facets" (status,
/// type, open only, overdue only) are what the stat rows, highlights, and chart segments switch
/// between. <see cref="SelectedWorkOrder"/> is loaded on its own because the picker list is
/// capped and may not contain it.
/// </summary>
public sealed record MaintenanceOrderListViewModel(
    PagedResponse<MaintenanceOrderListItemDto> Page,
    MaintenanceOrderStatisticsDto Statistics,
    MaintenanceOrderListFilter Filter,
    IReadOnlyList<WorkOrderOptionDto> WorkOrders,
    WorkOrderOptionDto? SelectedWorkOrder)
{
    /// <summary>The picker options, with the selected work order added when the cap left it out.</summary>
    public IReadOnlyList<WorkOrderOptionDto> FilterOptions =>
        SelectedWorkOrder is { } selected && WorkOrders.All(workOrder => workOrder.Id != selected.Id)
            ? [selected, .. WorkOrders]
            : WorkOrders;

    /// <summary>A new order can be scheduled straight from the list unless the selected work order is closed.</summary>
    public bool CanSchedule => SelectedWorkOrder is not { IsOpen: false };

    /// <summary>True when the current facets are exactly these, so a row, highlight, or segment shows as selected.</summary>
    public bool IsFacet(
        MaintenanceStatus? status = null,
        MaintenanceType? type = null,
        bool openOnly = false,
        bool overdueOnly = false) =>
        Filter.Status == status && Filter.Type == type
        && Filter.OpenOnly == openOnly && Filter.OverdueOnly == overdueOnly;

    /// <summary>
    /// The current facets in words for the summary footer ("In progress", "C-check · Overdue"),
    /// or null when none is set. Overdue already implies open.
    /// </summary>
    public string? FacetLabel
    {
        get
        {
            var parts = new List<string>();
            if (Filter.Status is { } status)
            {
                parts.Add(MaintenanceOrderLabels.ToLabel(status));
            }

            if (Filter.Type is { } type)
            {
                parts.Add(MaintenanceOrderLabels.ToLabel(type));
            }

            if (Filter.OverdueOnly)
            {
                parts.Add("Overdue");
            }
            else if (Filter.OpenOnly)
            {
                parts.Add("Open only");
            }

            return parts.Count == 0 ? null : string.Join(" · ", parts);
        }
    }

    /// <summary>
    /// Route values for list links. With <paramref name="keepFacets"/> the current facets are kept
    /// (pager links); without it only the facets given here apply (stat rows, highlights, segments).
    /// </summary>
    public Dictionary<string, string> RouteValues(
        int? page = null,
        MaintenanceStatus? status = null,
        MaintenanceType? type = null,
        bool openOnly = false,
        bool overdueOnly = false,
        bool keepFacets = true)
    {
        var values = new Dictionary<string, string>();
        if (Filter.WorkOrderId is { } workOrderId)
        {
            values["workOrderId"] = workOrderId.ToString();
        }

        if ((keepFacets ? status ?? Filter.Status : status) is { } statusValue)
        {
            values["status"] = statusValue.ToString();
        }

        if ((keepFacets ? type ?? Filter.Type : type) is { } typeValue)
        {
            values["type"] = typeValue.ToString();
        }

        if (openOnly || (keepFacets && Filter.OpenOnly))
        {
            values["open"] = "true";
        }

        if (overdueOnly || (keepFacets && Filter.OverdueOnly))
        {
            values["overdue"] = "true";
        }

        if (page is > 1)
        {
            values["page"] = page.Value.ToString(CultureInfo.InvariantCulture);
        }

        return values;
    }
}
