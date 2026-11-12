using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.Aircraft.DTOs;
using AircraftMRO.Application.Features.MaintenanceOrders.DTOs;
using AircraftMRO.Domain.Common.Results;
using AircraftMRO.Domain.Entities;
using AircraftEntity = AircraftMRO.Domain.Entities.Aircraft;

namespace AircraftMRO.Application.Features.MaintenanceOrders.Ports;

/// <summary>
/// Reads take <c>today</c> so "overdue" is decided by the caller's clock, not the database's.
/// Adds also save the maintenance order's work order in the same transaction and check its row
/// version, so a work order closed in the meantime rejects the whole save. Status changes and
/// deletes do the same, and may also save the aircraft when the work order closes. Maintenance whose work
/// order was soft-deleted is hidden from every read, including tracked loads.
/// </summary>
public interface IMaintenanceOrderRepository
{
    Task<PagedResponse<MaintenanceOrderListItemDto>> ListAsync(
        MaintenanceOrderListFilter filter,
        PagedRequest request,
        DateOnly today,
        CancellationToken cancellationToken);

    Task<MaintenanceOrderStatisticsDto> GetStatisticsAsync(Guid? workOrderId, DateOnly today, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkOrderOptionDto>> ListWorkOrderOptionsAsync(CancellationToken cancellationToken);

    /// <summary>One work order option, whether or not it is in the capped option list.</summary>
    Task<WorkOrderOptionDto?> GetWorkOrderOptionAsync(Guid workOrderId, CancellationToken cancellationToken);

    Task<MaintenanceOrderDto?> GetByIdAsync(Guid id, DateOnly today, CancellationToken cancellationToken);

    /// <summary>Loads a tracked maintenance order for a write in the same operation.</summary>
    Task<MaintenanceOrder?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Loads the tracked work order the same operation saves alongside the maintenance order.</summary>
    Task<WorkOrder?> GetWorkOrderForUpdateAsync(Guid workOrderId, CancellationToken cancellationToken);

    /// <summary>Loads a tracked aircraft whose status the same operation may change.</summary>
    Task<AircraftEntity?> GetAircraftForUpdateAsync(Guid aircraftId, CancellationToken cancellationToken);

    /// <summary>Open work orders on an aircraft, leaving out <paramref name="excludeWorkOrderId"/>.</summary>
    Task<OpenWorkOrderCounts> GetOpenWorkOrderCountsAsync(
        Guid aircraftId,
        Guid excludeWorkOrderId,
        CancellationToken cancellationToken);

    /// <summary>The work order's maintenance, leaving out <paramref name="excludeMaintenanceOrderId"/>.</summary>
    Task<MaintenanceProgressDto> GetMaintenanceProgressAsync(
        Guid workOrderId,
        Guid excludeMaintenanceOrderId,
        CancellationToken cancellationToken);

    /// <summary>Fails with a concurrency error when <paramref name="workOrder"/> changed since it was read.</summary>
    Task<Result> AddAsync(MaintenanceOrder maintenanceOrder, WorkOrder workOrder, CancellationToken cancellationToken);

    /// <summary>
    /// Fails with a concurrency error when the maintenance order, or the <paramref name="workOrder"/>
    /// or <paramref name="aircraft"/> saved with it, changed since it was read.
    /// </summary>
    Task<Result> UpdateAsync(
        MaintenanceOrder maintenanceOrder,
        byte[] expectedRowVersion,
        WorkOrder? workOrder,
        AircraftEntity? aircraft,
        CancellationToken cancellationToken);

    /// <summary>
    /// Soft-deletes the maintenance order; fails with a concurrency error when it, or the
    /// <paramref name="workOrder"/> or <paramref name="aircraft"/> saved with it, changed since it was read.
    /// </summary>
    Task<Result> DeleteAsync(
        MaintenanceOrder maintenanceOrder,
        byte[] expectedRowVersion,
        WorkOrder? workOrder,
        AircraftEntity? aircraft,
        CancellationToken cancellationToken);
}
