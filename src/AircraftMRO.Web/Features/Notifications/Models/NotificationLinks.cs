using AircraftMRO.Domain.Entities;

namespace AircraftMRO.Web.Features.Notifications.Models;

/// <summary>
/// Which controller shows each notified entity type. A notification's entity type is its class
/// name (<c>WorkOrder</c>), which is not always its controller's name (<c>WorkOrders</c>), and
/// routing builds a URL for any controller name without checking it exists. Types not listed
/// here get no link.
/// </summary>
public static class NotificationLinks
{
    private static readonly Dictionary<string, string> DetailsControllers = new(StringComparer.Ordinal)
    {
        [nameof(Aircraft)] = "Aircraft",
        [nameof(WorkOrder)] = "WorkOrders"
    };

    /// <summary>The path to the entity's Details page, or null when it was deleted or has none.</summary>
    public static string? DetailsPath(LinkGenerator links, string entityType, Guid entityId, bool deleted) =>
        !deleted && DetailsControllers.TryGetValue(entityType, out var controller)
            ? links.GetPathByAction("Details", controller, new { id = entityId })
            : null;
}
