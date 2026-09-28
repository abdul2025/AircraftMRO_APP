using System.Globalization;
using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.WorkOrders.DTOs;
using AircraftMRO.Domain.Enums.WorkOrders;

namespace AircraftMRO.Web.Features.WorkOrders.Models;

/// <summary>
/// The list page. Search and aircraft are kept on every link; the "facets" (status, priority,
/// open only, overdue only) are what the stat tiles and chart segments switch between.
/// </summary>
public sealed record WorkOrderListViewModel(
    PagedResponse<WorkOrderListItemDto> Page,
    WorkOrderStatisticsDto Statistics,
    WorkOrderListFilter Filter,
    IReadOnlyList<AircraftOptionDto> Aircraft)
{
    public string? SelectedAircraftRegistration =>
        Filter.AircraftId is { } id ? Aircraft.FirstOrDefault(aircraft => aircraft.Id == id)?.RegistrationNumber : null;

    /// <summary>True when the current facets are exactly these, so a tile or segment shows as selected.</summary>
    public bool IsFacet(
        WorkOrderStatus? status = null,
        WorkOrderPriority? priority = null,
        bool openOnly = false,
        bool overdueOnly = false) =>
        Filter.Status == status && Filter.Priority == priority
        && Filter.OpenOnly == openOnly && Filter.OverdueOnly == overdueOnly;

    /// <summary>
    /// Route values for list links. With <paramref name="keepFacets"/> the current facets are kept
    /// (pager links); without it only the facets given here apply (tiles and chart segments).
    /// </summary>
    public Dictionary<string, string> RouteValues(
        int? page = null,
        WorkOrderStatus? status = null,
        WorkOrderPriority? priority = null,
        bool openOnly = false,
        bool overdueOnly = false,
        bool keepFacets = true)
    {
        var values = new Dictionary<string, string>();
        if (Filter.Search is { } search)
        {
            values["search"] = search;
        }

        if (Filter.AircraftId is { } aircraftId)
        {
            values["aircraftId"] = aircraftId.ToString();
        }

        var effectiveStatus = keepFacets ? status ?? Filter.Status : status;
        var effectivePriority = keepFacets ? priority ?? Filter.Priority : priority;
        if (effectiveStatus is { } statusValue)
        {
            values["status"] = statusValue.ToString();
        }

        if (effectivePriority is { } priorityValue)
        {
            values["priority"] = priorityValue.ToString();
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
