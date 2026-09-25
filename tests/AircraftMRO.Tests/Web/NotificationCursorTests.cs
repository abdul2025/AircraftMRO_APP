using AircraftMRO.Application.Features.Notifications.DTOs;
using AircraftMRO.Domain.Enums.Notifications;
using AircraftMRO.Web.Features.Notifications;

namespace AircraftMRO.Tests.Web;

public sealed class NotificationCursorTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 25, 8, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(10);

    private static NotificationDto Row(long id, DateTimeOffset occurredAt) =>
        new(id, NotificationAction.Created, "Aircraft", Guid.NewGuid(), "HZ", "m", [], null, occurredAt);

    [Fact]
    public void Starts_after_existing_history()
    {
        var cursor = new NotificationCursor(Window);
        cursor.Start(latestId: 40);

        Assert.True(cursor.IsStarted);
        Assert.Equal(40, cursor.SettledThroughId);
        Assert.Empty(cursor.TakeUnpushed([Row(39, Now), Row(40, Now)]));
    }

    [Fact]
    public void Pushes_each_row_once_across_re_reads()
    {
        var cursor = new NotificationCursor(Window);
        cursor.Start(0);

        Assert.Equal([1L, 2L], cursor.TakeUnpushed([Row(2, Now), Row(1, Now)]).Select(r => r.Id));
        Assert.Empty(cursor.TakeUnpushed([Row(1, Now), Row(2, Now)]));
    }

    [Fact]
    public void A_late_committing_lower_id_is_still_pushed()
    {
        var cursor = new NotificationCursor(Window);
        cursor.Start(0);

        // Id 5 commits and is pushed first; id 4 (allocated earlier) commits a moment later.
        cursor.TakeUnpushed([Row(5, Now)]);
        cursor.Settle(Now.AddSeconds(1));
        Assert.Equal(0, cursor.SettledThroughId); // still inside the window, so re-read from 0

        Assert.Equal([4L], cursor.TakeUnpushed([Row(4, Now), Row(5, Now)]).Select(r => r.Id));
    }

    [Fact]
    public void Settles_only_rows_older_than_the_window_in_id_order()
    {
        var cursor = new NotificationCursor(Window);
        cursor.Start(0);
        cursor.TakeUnpushed([Row(1, Now), Row(2, Now.AddSeconds(8)), Row(3, Now)]);

        cursor.Settle(Now.AddSeconds(11));

        // Row 1 is settled; row 2 is still recent, so row 3 must wait behind it.
        Assert.Equal(1, cursor.SettledThroughId);

        cursor.Settle(Now.AddSeconds(19));
        Assert.Equal(3, cursor.SettledThroughId);
    }
}
