using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.Aircraft.DTOs;
using AircraftMRO.Domain.Enums.Aircraft;

namespace AircraftMRO.Web.Features.Aircraft.Models;

public sealed record AircraftListViewModel(
    PagedResponse<AircraftListItemDto> Page,
    AircraftStatisticsDto Statistics,
    AircraftListFilter Filter)
{
    /// <summary>Route values that keep the current filter on pager and stat-tile links.</summary>
    public Dictionary<string, string> RouteValues(int? page = null, AircraftStatus? status = null, bool keepStatus = true)
    {
        var values = new Dictionary<string, string>();
        if (Filter.Search is { } search)
        {
            values["search"] = search;
        }

        var effectiveStatus = keepStatus ? status ?? Filter.Status : status;
        if (effectiveStatus is { } value)
        {
            values["status"] = value.ToString();
        }

        if (page is > 1)
        {
            values["page"] = page.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return values;
    }
}
