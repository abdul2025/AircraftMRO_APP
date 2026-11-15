using System.Globalization;
using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.AircraftModels.DTOs;

namespace AircraftMRO.Web.Features.AircraftModels.Models;

public sealed record AircraftModelListViewModel(
    PagedResponse<AircraftModelListItemDto> Page,
    AircraftModelListFilter Filter,
    IReadOnlyList<ManufacturerOptionDto> Manufacturers)
{
    /// <summary>Route values that keep the current filter on pager links.</summary>
    public Dictionary<string, string> RouteValues(int? page = null)
    {
        var values = new Dictionary<string, string>();
        if (Filter.Search is { } search)
        {
            values["search"] = search;
        }

        if (Filter.ManufacturerId is { } manufacturerId)
        {
            values["manufacturerId"] = manufacturerId.ToString();
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
