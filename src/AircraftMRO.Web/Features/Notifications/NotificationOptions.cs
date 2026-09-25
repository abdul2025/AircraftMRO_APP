namespace AircraftMRO.Web.Features.Notifications;

/// <summary>Settings for the notification dispatcher, bound from the "Notifications" section.</summary>
public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";

    /// <summary>How often new notification rows are read and pushed.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Maximum rows read per poll.</summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// How long recently pushed rows stay in the re-read window. Covers transactions that
    /// commit a lower id after a higher one was already read; must exceed the longest save.
    /// </summary>
    public TimeSpan ReplayWindow { get; set; } = TimeSpan.FromSeconds(10);

    public static bool IsValid(NotificationOptions options) =>
        options.PollInterval >= TimeSpan.FromMilliseconds(50)
        && options.PollInterval <= TimeSpan.FromMinutes(1)
        && options.BatchSize is >= 1 and <= 200
        && options.ReplayWindow >= TimeSpan.FromSeconds(1)
        && options.ReplayWindow <= TimeSpan.FromMinutes(5);
}
