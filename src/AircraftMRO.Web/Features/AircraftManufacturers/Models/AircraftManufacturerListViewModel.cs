using System.Globalization;
using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.AircraftManufacturers.DTOs;

namespace AircraftMRO.Web.Features.AircraftManufacturers.Models;

public sealed record AircraftManufacturerListViewModel(
    PagedResponse<AircraftManufacturerListItemDto> Page,
    AircraftManufacturerListFilter Filter)
{
    /// <summary>Shown where a manufacturer carried over from free text has no country yet.</summary>
    public const string NoCountry = "Not set";

    /// <summary>Route values that keep the current filter on pager links.</summary>
    public Dictionary<string, string> RouteValues(int? page = null)
    {
        var values = new Dictionary<string, string>();
        if (Filter.Search is { } search)
        {
            values["search"] = search;
        }

        if (Filter.IsActive is { } isActive)
        {
            values["isActive"] = isActive ? "true" : "false";
        }

        if (page is > 1)
        {
            values["page"] = page.Value.ToString(CultureInfo.InvariantCulture);
        }

        return values;
    }
}
