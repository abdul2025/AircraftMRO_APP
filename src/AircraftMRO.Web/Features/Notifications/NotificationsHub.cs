using AircraftMRO.Web.Features.Notifications.Models;
using Microsoft.AspNetCore.SignalR;

namespace AircraftMRO.Web.Features.Notifications;

/// <summary>Methods the server calls on connected browsers.</summary>
public interface INotificationsClient
{
    Task NotificationReceived(NotificationMessage notification);
}

/// <summary>
/// Server-to-browser only: clients receive notifications and never call the hub.
/// Anonymous until authentication exists; then add [Authorize] and per-role groups.
/// </summary>
public sealed class NotificationsHub : Hub<INotificationsClient>
{
    public const string Path = "/hubs/notifications";
}
