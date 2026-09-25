using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.Notifications.Interfaces;
using AircraftMRO.Web.Features.Notifications.Models;
using Microsoft.AspNetCore.Mvc;

namespace AircraftMRO.Web.Features.Notifications;

/// <summary>The topbar bell: the latest notifications, rendered on the server and kept live by notifications.js.</summary>
public sealed partial class NotificationBellViewComponent(
    INotificationService notificationService,
    LinkGenerator links,
    ILogger<NotificationBellViewComponent> logger) : ViewComponent
{
    public const int RecentCount = 10;

    public async Task<IViewComponentResult> InvokeAsync()
    {
        IReadOnlyList<NotificationMessage> recent = [];
        try
        {
            var result = await notificationService.ListAsync(new PagedRequest(1, RecentCount), HttpContext.RequestAborted);
            recent = result.Value!.Items.Select(dto => NotificationMessage.From(dto, links)).ToList();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The bell must never take down the page it sits on; it fills in once the database is reachable.
            LogBellLoadFailed(logger, exception);
        }

        return View(new NotificationBellViewModel(recent, recent.Count == 0 ? 0 : recent.Max(item => item.Id)));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Loading recent notifications for the bell failed.")]
    private static partial void LogBellLoadFailed(ILogger logger, Exception exception);
}

public sealed record NotificationBellViewModel(IReadOnlyList<NotificationMessage> Recent, long LatestId);
