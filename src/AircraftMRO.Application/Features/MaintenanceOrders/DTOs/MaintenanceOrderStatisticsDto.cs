using AircraftMRO.Domain.Enums.MaintenanceOrders;

namespace AircraftMRO.Application.Features.MaintenanceOrders.DTOs;

/// <summary>Counts for live (not deleted) maintenance orders, optionally for one work order.</summary>
public sealed record MaintenanceOrderStatisticsDto(
    int TotalCount,
    IReadOnlyDictionary<MaintenanceStatus, int> CountByStatus,
    int OverdueCount)
{
    public static readonly MaintenanceOrderStatisticsDto Empty = new(0, new Dictionary<MaintenanceStatus, int>(), 0);

    public int CountFor(MaintenanceStatus status) => CountByStatus.GetValueOrDefault(status);

    public int OpenCount => CountFor(MaintenanceStatus.Scheduled) + CountFor(MaintenanceStatus.InProgress);
}
