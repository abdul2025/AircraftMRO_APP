namespace AircraftMRO.Application.Features.WorkOrders;

/// <summary>Work order settings, bound from the "WorkOrders" configuration section.</summary>
public sealed class WorkOrderOptions
{
    public const string SectionName = "WorkOrders";

    /// <summary>
    /// The time zone whose calendar decides when a work order becomes overdue: one due today is
    /// overdue from local midnight tomorrow. An IANA id such as <c>Asia/Riyadh</c>; defaults to UTC.
    /// </summary>
    public string TimeZone { get; set; } = "UTC";

    public static bool IsValid(WorkOrderOptions options) =>
        TimeZoneInfo.TryFindSystemTimeZoneById(options.TimeZone, out _);

    public TimeZoneInfo ResolveTimeZone() => TimeZoneInfo.FindSystemTimeZoneById(TimeZone);
}
