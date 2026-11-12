using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.MaintenanceOrders.DTOs;
using AircraftMRO.Domain.Common.Results;

namespace AircraftMRO.Application.Features.MaintenanceOrders.Interfaces;

public interface IMaintenanceOrderService
{
    Task<Result<PagedResponse<MaintenanceOrderListItemDto>>> ListAsync(
        MaintenanceOrderListFilter filter,
        PagedRequest request,
        CancellationToken cancellationToken);

    /// <summary>Statistics for all maintenance, or for one work order's when <paramref name="workOrderId"/> is set.</summary>
    Task<Result<MaintenanceOrderStatisticsDto>> GetStatisticsAsync(Guid? workOrderId, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<WorkOrderOptionDto>>> ListWorkOrderOptionsAsync(CancellationToken cancellationToken);

    /// <summary>Fails with <see cref="MaintenanceOrderErrors.WorkOrderNotFound"/> for an unknown or deleted work order.</summary>
    Task<Result<WorkOrderOptionDto>> GetWorkOrderOptionAsync(Guid workOrderId, CancellationToken cancellationToken);

    Task<Result<MaintenanceOrderDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<Guid>> CreateAsync(CreateMaintenanceOrderDto dto, CancellationToken cancellationToken);

    Task<Result> UpdateAsync(Guid id, UpdateMaintenanceOrderDto dto, CancellationToken cancellationToken);

    Task<Result> DeleteAsync(Guid id, byte[] rowVersion, CancellationToken cancellationToken);
}
