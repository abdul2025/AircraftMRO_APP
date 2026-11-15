using AircraftMRO.Domain.Enums.AircraftModels;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AircraftMRO.Web.Features.AircraftModels.Models;

public static class AircraftModelLabels
{
    public static string ToLabel(AircraftCategory category) => category switch
    {
        AircraftCategory.NarrowBody => "Narrow-body",
        AircraftCategory.WideBody => "Wide-body",
        AircraftCategory.RegionalJet => "Regional jet",
        AircraftCategory.Turboprop => "Turboprop",
        AircraftCategory.BusinessJet => "Business jet",
        AircraftCategory.Helicopter => "Helicopter",
        AircraftCategory.Other => "Other",
        _ => category.ToString()
    };

    public static IEnumerable<SelectListItem> CategoryOptions() =>
        Enum.GetValues<AircraftCategory>()
            .Select(category => new SelectListItem(ToLabel(category), category.ToString()));
}
