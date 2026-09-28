using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.WorkOrders.DTOs;
using AircraftMRO.Domain.Common.Results;

namespace AircraftMRO.Application.Features.WorkOrders.Interfaces;

public interface IWorkOrderService
{
    Task<Result<PagedResponse<WorkOrderListItemDto>>> ListAsync(
        WorkOrderListFilter filter,
        PagedRequest request,
        CancellationToken cancellationToken);

    /// <summary>Statistics for every work order, or for one aircraft's when <paramref name="aircraftId"/> is set.</summary>
    Task<Result<WorkOrderStatisticsDto>> GetStatisticsAsync(Guid? aircraftId, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<AircraftOptionDto>>> ListAircraftOptionsAsync(CancellationToken cancellationToken);

    Task<Result<WorkOrderDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<Guid>> CreateAsync(CreateWorkOrderDto dto, CancellationToken cancellationToken);

    Task<Result> UpdateAsync(Guid id, UpdateWorkOrderDto dto, CancellationToken cancellationToken);

    Task<Result> DeleteAsync(Guid id, byte[] rowVersion, CancellationToken cancellationToken);
}
