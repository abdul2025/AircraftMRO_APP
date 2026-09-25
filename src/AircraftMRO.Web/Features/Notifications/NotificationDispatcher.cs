using AircraftMRO.Application.Features.Notifications.Interfaces;
using AircraftMRO.Web.Features.Notifications.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace AircraftMRO.Web.Features.Notifications;

/// <summary>
/// Polls the Notifications table and pushes new rows to every connected browser. Rows are
/// written in the same transaction as the change, by this host or the API, so polling is
/// what lets API changes reach Web clients. Each Web instance runs its own dispatcher.
/// </summary>
public sealed partial class NotificationDispatcher(
    IServiceScopeFactory scopeFactory,
    IHubContext<NotificationsHub, INotificationsClient> hub,
    LinkGenerator links,
    IOptions<NotificationOptions> options,
    TimeProvider timeProvider,
    ILogger<NotificationDispatcher> logger) : BackgroundService
{
    private readonly NotificationCursor _cursor = new(options.Value.ReplayWindow);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.PollInterval, timeProvider);
        do
        {
            try
            {
                await DispatchOnceAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Keep polling; the rows stay in the table and are pushed on a later tick.
                LogDispatchFailed(logger, exception);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    internal async Task DispatchOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();

        if (!_cursor.IsStarted)
        {
            var latest = await notifications.GetLatestIdAsync(cancellationToken);
            _cursor.Start(latest.Value);
            return;
        }

        var rows = await notifications.ListAfterAsync(_cursor.SettledThroughId, options.Value.BatchSize, cancellationToken);
        foreach (var row in _cursor.TakeUnpushed(rows.Value!))
        {
            await hub.Clients.All.NotificationReceived(NotificationMessage.From(row, links));
        }

        _cursor.Settle(timeProvider.GetUtcNow());
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Dispatching notifications failed; retrying on the next poll.")]
    private static partial void LogDispatchFailed(ILogger logger, Exception exception);
}
