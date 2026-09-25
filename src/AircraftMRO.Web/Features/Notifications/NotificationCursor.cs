using AircraftMRO.Application.Features.Notifications.DTOs;

namespace AircraftMRO.Web.Features.Notifications;

/// <summary>
/// Tracks which notification rows have been pushed. SQL Server assigns identity ids at insert,
/// so a slower transaction can commit a lower id after a higher one was already read. The cursor
/// therefore keeps re-reading from the last <em>settled</em> id - one whose row is older than the
/// replay window - and skips rows it has already pushed.
/// </summary>
public sealed class NotificationCursor(TimeSpan replayWindow)
{
    private readonly SortedDictionary<long, DateTimeOffset> _pushed = new();

    /// <summary>Every id at or below this has been handled; read rows after it.</summary>
    public long SettledThroughId { get; private set; }

    public bool IsStarted { get; private set; }

    /// <summary>Starts after the current newest id so history is not replayed on startup.</summary>
    public void Start(long latestId)
    {
        SettledThroughId = latestId;
        IsStarted = true;
    }

    /// <summary>Returns the rows not pushed before, in id order, and remembers them.</summary>
    public IReadOnlyList<NotificationDto> TakeUnpushed(IEnumerable<NotificationDto> rows) =>
        rows
            .Where(row => row.Id > SettledThroughId && _pushed.TryAdd(row.Id, row.OccurredAtUtc))
            .OrderBy(row => row.Id)
            .ToList();

    /// <summary>Advances the settled id over pushed rows older than the replay window, in id order.</summary>
    public void Settle(DateTimeOffset now)
    {
        var cutoff = now - replayWindow;
        foreach (var (id, occurredAtUtc) in _pushed.ToList())
        {
            if (occurredAtUtc > cutoff)
            {
                break;
            }

            SettledThroughId = id;
            _pushed.Remove(id);
        }
    }
}
