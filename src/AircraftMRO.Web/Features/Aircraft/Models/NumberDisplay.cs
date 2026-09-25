using System.Globalization;

namespace AircraftMRO.Web.Features.Aircraft.Models;

public static class NumberDisplay
{
    /// <summary>Full value below 10,000; compact (12.9K, 4.2M) above, for stat tiles.</summary>
    public static string Compact(decimal value) => Math.Abs(value) switch
    {
        >= 1_000_000m => (value / 1_000_000m).ToString("0.#", CultureInfo.InvariantCulture) + "M",
        >= 10_000m => (value / 1_000m).ToString("0.#", CultureInfo.InvariantCulture) + "K",
        _ => value.ToString("#,0.#", CultureInfo.InvariantCulture)
    };
}
