using AircraftMRO.Domain.Enums.Aircraft;

namespace AircraftMRO.Application.Features.Aircraft.DTOs;

/// <summary>Fleet-wide counts for live (not deleted) aircraft.</summary>
public sealed record AircraftStatisticsDto(
    int TotalCount,
    IReadOnlyDictionary<AircraftStatus, int> CountByStatus,
    decimal TotalFlightHours)
{
    public static readonly AircraftStatisticsDto Empty = new(0, new Dictionary<AircraftStatus, int>(), 0m);

    public int CountFor(AircraftStatus status) => CountByStatus.GetValueOrDefault(status);
}
