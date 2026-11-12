using System.Globalization;
using AircraftMRO.Domain.Enums.MaintenanceOrders;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AircraftMRO.Web.Features.MaintenanceOrders.Models;

public static class MaintenanceOrderLabels
{
    public static string ToLabel(MaintenanceStatus status) => status switch
    {
        MaintenanceStatus.Scheduled => "Scheduled",
        MaintenanceStatus.InProgress => "In progress",
        MaintenanceStatus.Completed => "Completed",
        _ => status.ToString()
    };

    public static string ToLabel(MaintenanceType type) => type switch
    {
        MaintenanceType.ACheck => "A-check",
        MaintenanceType.BCheck => "B-check",
        MaintenanceType.CCheck => "C-check",
        MaintenanceType.DCheck => "D-check",
        MaintenanceType.Unscheduled => "Unscheduled",
        _ => type.ToString()
    };

    /// <summary>Badge modifier, shared with the work order badges' palette.</summary>
    public static string ToCssModifier(MaintenanceStatus status) => status switch
    {
        MaintenanceStatus.Scheduled => "info",
        MaintenanceStatus.InProgress => "active",
        MaintenanceStatus.Completed => "success",
        _ => "neutral"
    };

    public static IEnumerable<SelectListItem> StatusOptions() =>
        Enum.GetValues<MaintenanceStatus>().Select(status => new SelectListItem(ToLabel(status), status.ToString()));

    public static IEnumerable<SelectListItem> TypeOptions() =>
        Enum.GetValues<MaintenanceType>().Select(type => new SelectListItem(ToLabel(type), type.ToString()));

    public static string Date(DateOnly value) => value.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    public static string MachineDate(DateOnly value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
