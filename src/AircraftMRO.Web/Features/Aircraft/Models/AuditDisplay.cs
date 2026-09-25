using System.Globalization;

namespace AircraftMRO.Web.Features.Aircraft.Models;

public static class AuditDisplay
{
    /// <summary>Shown when a change was made without a signed-in user.</summary>
    public const string UnknownUser = "Not recorded";

    public static string Timestamp(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("d MMM yyyy, HH:mm 'UTC'", CultureInfo.InvariantCulture);

    public static string MachineTimestamp(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    public static string User(string? userId) => string.IsNullOrWhiteSpace(userId) ? UnknownUser : userId;
}
