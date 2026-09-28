using AircraftMRO.Domain.Enums.WorkOrders;

namespace AircraftMRO.Application.Features.WorkOrders.DTOs;

/// <summary>Counts for live (not deleted) work orders, optionally for one aircraft.</summary>
public sealed record WorkOrderStatisticsDto(
    int TotalCount,
    IReadOnlyDictionary<WorkOrderStatus, int> CountByStatus,
    IReadOnlyDictionary<WorkOrderPriority, int> OpenCountByPriority,
    int OverdueCount)
{
    public static readonly WorkOrderStatisticsDto Empty = new(
        0, new Dictionary<WorkOrderStatus, int>(), new Dictionary<WorkOrderPriority, int>(), 0);

    public int CountFor(WorkOrderStatus status) => CountByStatus.GetValueOrDefault(status);

    public int OpenCountFor(WorkOrderPriority priority) => OpenCountByPriority.GetValueOrDefault(priority);

    public int OpenCount => OpenCountByPriority.Values.Sum();
}
