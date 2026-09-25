using AircraftMRO.Domain.Enums.Aircraft;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AircraftMRO.Web.Features.Aircraft.Models;

public static class AircraftStatusLabels
{
    public static string ToLabel(AircraftStatus status) => status switch
    {
        AircraftStatus.Active => "Active",
        AircraftStatus.InMaintenance => "In maintenance",
        AircraftStatus.Grounded => "Grounded",
        AircraftStatus.Retired => "Retired",
        _ => status.ToString()
    };

    public static string ToCssModifier(AircraftStatus status) => status switch
    {
        AircraftStatus.Active => "success",
        AircraftStatus.InMaintenance => "warning",
        AircraftStatus.Grounded => "danger",
        _ => "neutral"
    };

    public static IEnumerable<SelectListItem> Options() =>
        Enum.GetValues<AircraftStatus>()
            .Select(status => new SelectListItem(ToLabel(status), status.ToString()));
}
