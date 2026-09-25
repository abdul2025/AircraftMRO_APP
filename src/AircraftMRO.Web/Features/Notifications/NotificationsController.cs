using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.Notifications.Interfaces;
using AircraftMRO.Web.Features.Notifications.Models;
using Microsoft.AspNetCore.Mvc;

namespace AircraftMRO.Web.Features.Notifications;

public sealed class NotificationsController(INotificationService notificationService, LinkGenerator links) : Controller
{
    public const int CatchUpLimit = 100;

    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default)
    {
        var result = await notificationService.ListAsync(new PagedRequest(page), cancellationToken);
        return View(result.Value!);
    }

    /// <summary>Notifications after <paramref name="afterId"/>; browsers call this after (re)connecting to fill gaps.</summary>
    [HttpGet]
    public async Task<IActionResult> Since(long afterId, CancellationToken cancellationToken)
    {
        var result = await notificationService.ListAfterAsync(afterId, CatchUpLimit, cancellationToken);
        return Json(result.Value!.Select(dto => NotificationMessage.From(dto, links)));
    }
}
