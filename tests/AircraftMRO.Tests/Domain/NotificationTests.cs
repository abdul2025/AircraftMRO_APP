using AircraftMRO.Domain.Entities;
using AircraftMRO.Domain.Enums.Notifications;

namespace AircraftMRO.Tests.Domain;

public sealed class NotificationTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 25, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Record_keeps_values_and_truncates_long_display_text()
    {
        var notification = Notification.Record(
            NotificationAction.Updated, "Aircraft", Guid.NewGuid(), new string('R', 300), new string('M', 900), "[]", "user-1", Now);

        Assert.Equal(NotificationAction.Updated, notification.Action);
        Assert.Equal(Notification.EntityDisplayNameMaxLength, notification.EntityDisplayName.Length);
        Assert.EndsWith("…", notification.Message);
        Assert.Equal(Notification.MessageMaxLength, notification.Message.Length);
        Assert.Equal("user-1", notification.ActorUserId);
        Assert.Equal(Now, notification.OccurredAtUtc);
    }

    [Fact]
    public void Record_rejects_invalid_input()
    {
        var id = Guid.NewGuid();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Notification.Record((NotificationAction)9, "Aircraft", id, "HZ-ABC", "m", null, null, Now));
        Assert.Throws<ArgumentException>(() =>
            Notification.Record(NotificationAction.Created, " ", id, "HZ-ABC", "m", null, null, Now));
        Assert.Throws<ArgumentException>(() =>
            Notification.Record(NotificationAction.Created, "Aircraft", id, "HZ-ABC", "m", new string('x', Notification.ChangesMaxLength + 1), null, Now));
    }
}
