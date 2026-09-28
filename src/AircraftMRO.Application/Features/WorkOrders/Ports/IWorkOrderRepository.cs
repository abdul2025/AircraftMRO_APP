using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.Aircraft.DTOs;
using AircraftMRO.Application.Features.WorkOrders.DTOs;
using AircraftMRO.Domain.Common.Results;
using AircraftMRO.Domain.Entities;
using AircraftEntity = AircraftMRO.Domain.Entities.Aircraft;

namespace AircraftMRO.Application.Features.WorkOrders.Ports;

/// <summary>
/// Reads take <c>today</c> so "overdue" is decided by the caller's clock, not the database's.
/// Adds and updates always save the work order's aircraft in the same transaction and check its
/// row version, so a concurrent change to that aircraft, or to another of its work orders, rejects
/// the whole save.
/// </summary>
public interface IWorkOrderRepository
{
    Task<PagedResponse<WorkOrderListItemDto>> ListAsync(
        WorkOrderListFilter filter,
        PagedRequest request,
        DateOnly today,
        CancellationToken cancellationToken);

    Task<WorkOrderStatisticsDto> GetStatisticsAsync(Guid? aircraftId, DateOnly today, CancellationToken cancellationToken);

    Task<IReadOnlyList<AircraftOptionDto>> ListAircraftOptionsAsync(CancellationToken cancellationToken);

    Task<WorkOrderDto?> GetByIdAsync(Guid id, DateOnly today, CancellationToken cancellationToken);

    /// <summary>Loads a tracked work order for a write in the same operation.</summary>
    Task<WorkOrder?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Loads a tracked aircraft whose status the same operation may change.</summary>
    Task<AircraftEntity?> GetAircraftForUpdateAsync(Guid aircraftId, CancellationToken cancellationToken);

    /// <summary>Open work orders on an aircraft, leaving out <paramref name="excludeWorkOrderId"/> when set.</summary>
    Task<OpenWorkOrderCounts> GetOpenWorkOrderCountsAsync(
        Guid aircraftId,
        Guid? excludeWorkOrderId,
        CancellationToken cancellationToken);

    /// <summary>Reserves the next work order number, such as <c>WO-000042</c>.</summary>
    Task<string> NextNumberAsync(CancellationToken cancellationToken);

    /// <summary>Fails with a concurrency error when <paramref name="aircraft"/> changed since it was read.</summary>
    Task<Result> AddAsync(WorkOrder workOrder, AircraftEntity aircraft, CancellationToken cancellationToken);

    /// <summary>Fails with a concurrency error when the work order or <paramref name="aircraft"/> changed since it was read.</summary>
    Task<Result> UpdateAsync(
        WorkOrder workOrder,
        byte[] expectedRowVersion,
        AircraftEntity aircraft,
        CancellationToken cancellationToken);

    /// <summary>Soft-deletes the work order; fails with a concurrency error when it changed since it was read.</summary>
    Task<Result> DeleteAsync(WorkOrder workOrder, byte[] expectedRowVersion, CancellationToken cancellationToken);
}
