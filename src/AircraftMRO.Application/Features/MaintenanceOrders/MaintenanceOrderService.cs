using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.MaintenanceOrders.DTOs;
using AircraftMRO.Application.Features.MaintenanceOrders.Interfaces;
using AircraftMRO.Application.Features.MaintenanceOrders.Ports;
using AircraftMRO.Application.Features.WorkOrders;
using AircraftMRO.Domain.Common.Results;
using AircraftMRO.Domain.Enums.MaintenanceOrders;
using AircraftEntity = AircraftMRO.Domain.Entities.Aircraft;
using MaintenanceOrderEntity = AircraftMRO.Domain.Entities.MaintenanceOrder;
using WorkOrderEntity = AircraftMRO.Domain.Entities.WorkOrder;

namespace AircraftMRO.Application.Features.MaintenanceOrders;

/// <summary>
/// Maintenance can only be scheduled while its work order is open. Creating maintenance also
/// writes that work order, so closing it at the same moment rejects one of the two saves instead
/// of leaving open maintenance under a closed work order. Status changes and deletes move the
/// work order along with its maintenance: in progress when any is started, completed (freeing its
/// aircraft) when all of it is completed.
/// </summary>
public sealed class MaintenanceOrderService(
    IMaintenanceOrderRepository repository,
    TimeProvider timeProvider,
    WorkOrderOptions options) : IMaintenanceOrderService
{
    private readonly TimeZoneInfo _businessTimeZone = options.ResolveTimeZone();

    public async Task<Result<PagedResponse<MaintenanceOrderListItemDto>>> ListAsync(
        MaintenanceOrderListFilter filter,
        PagedRequest request,
        CancellationToken cancellationToken) =>
        Result<PagedResponse<MaintenanceOrderListItemDto>>.Success(
            await repository.ListAsync(filter, request, Today, cancellationToken));

    public async Task<Result<MaintenanceOrderStatisticsDto>> GetStatisticsAsync(Guid? workOrderId, CancellationToken cancellationToken) =>
        Result<MaintenanceOrderStatisticsDto>.Success(await repository.GetStatisticsAsync(workOrderId, Today, cancellationToken));

    public async Task<Result<IReadOnlyList<WorkOrderOptionDto>>> ListWorkOrderOptionsAsync(CancellationToken cancellationToken) =>
        Result<IReadOnlyList<WorkOrderOptionDto>>.Success(await repository.ListWorkOrderOptionsAsync(cancellationToken));

    public async Task<Result<WorkOrderOptionDto>> GetWorkOrderOptionAsync(Guid workOrderId, CancellationToken cancellationToken)
    {
        var option = await repository.GetWorkOrderOptionAsync(workOrderId, cancellationToken);

        return option is null
            ? Result<WorkOrderOptionDto>.Failure(MaintenanceOrderErrors.WorkOrderNotFound, MaintenanceOrderErrors.WorkOrderNotFoundMessage)
            : Result<WorkOrderOptionDto>.Success(option);
    }

    public async Task<Result<MaintenanceOrderDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var maintenanceOrder = await repository.GetByIdAsync(id, Today, cancellationToken);

        return maintenanceOrder is null
            ? Result<MaintenanceOrderDto>.Failure(MaintenanceOrderErrors.NotFound, MaintenanceOrderErrors.NotFoundMessage)
            : Result<MaintenanceOrderDto>.Success(maintenanceOrder);
    }

    public async Task<Result<Guid>> CreateAsync(CreateMaintenanceOrderDto dto, CancellationToken cancellationToken)
    {
        var workOrder = await repository.GetWorkOrderForUpdateAsync(dto.WorkOrderId, cancellationToken);
        if (workOrder is null)
        {
            return Result<Guid>.Failure(MaintenanceOrderErrors.WorkOrderNotFound, MaintenanceOrderErrors.WorkOrderNotFoundMessage);
        }

        if (!workOrder.IsOpen)
        {
            return Result<Guid>.Failure(MaintenanceOrderErrors.WorkOrderClosed, MaintenanceOrderErrors.WorkOrderClosedMessage);
        }

        var created = MaintenanceOrderEntity.Create(dto.WorkOrderId, dto.Type, dto.ScheduledDate, dto.Notes);
        if (created.IsFailure)
        {
            return Result<Guid>.Failure(created.ErrorCode!, created.ErrorMessage!);
        }

        var maintenanceOrder = created.Value!;
        var saved = await repository.AddAsync(maintenanceOrder, workOrder, cancellationToken);

        return saved.IsSuccess
            ? Result<Guid>.Success(maintenanceOrder.Id)
            : Result<Guid>.Failure(saved.ErrorCode!, saved.ErrorMessage!);
    }

    public async Task<Result> UpdateAsync(Guid id, UpdateMaintenanceOrderDto dto, CancellationToken cancellationToken)
    {
        var maintenanceOrder = await repository.GetForUpdateAsync(id, cancellationToken);
        if (maintenanceOrder is null)
        {
            return Result.Failure(MaintenanceOrderErrors.NotFound, MaintenanceOrderErrors.NotFoundMessage);
        }

        var previousStatus = maintenanceOrder.Status;
        var now = timeProvider.GetUtcNow();
        var updated = maintenanceOrder.Update(dto.Type, dto.ScheduledDate, dto.Status, dto.Notes, now);
        if (updated.IsFailure)
        {
            return updated;
        }

        // Only a status change can move the work order. Other edits leave it alone: open
        // maintenance means its work order is open, and completed maintenance is final.
        if (maintenanceOrder.Status == previousStatus)
        {
            return await repository.UpdateAsync(maintenanceOrder, dto.RowVersion, workOrder: null, aircraft: null, cancellationToken);
        }

        var followed = await FollowWorkOrderAsync(maintenanceOrder, maintenanceOrder.Status, now, cancellationToken);
        return followed.IsFailure
            ? Result.Failure(followed.ErrorCode!, followed.ErrorMessage!)
            : await repository.UpdateAsync(
                maintenanceOrder, dto.RowVersion, followed.Value!.WorkOrder, followed.Value.Aircraft, cancellationToken);
    }

    public async Task<Result> DeleteAsync(Guid id, byte[] rowVersion, CancellationToken cancellationToken)
    {
        var maintenanceOrder = await repository.GetForUpdateAsync(id, cancellationToken);
        if (maintenanceOrder is null)
        {
            return Result.Failure(MaintenanceOrderErrors.NotFound, MaintenanceOrderErrors.NotFoundMessage);
        }

        if (!MaintenanceOrderEntity.CanBeDeleted(maintenanceOrder.Status))
        {
            return Result.Failure(
                MaintenanceOrderErrors.OnlyScheduledCanBeDeleted,
                MaintenanceOrderErrors.OnlyScheduledCanBeDeletedMessage);
        }

        // Deleting the last open maintenance can leave all that remains completed.
        var followed = await FollowWorkOrderAsync(maintenanceOrder, status: null, timeProvider.GetUtcNow(), cancellationToken);
        return followed.IsFailure
            ? Result.Failure(followed.ErrorCode!, followed.ErrorMessage!)
            : await repository.DeleteAsync(
                maintenanceOrder, rowVersion, followed.Value!.WorkOrder, followed.Value.Aircraft, cancellationToken);
    }

    /// <summary>
    /// Moves the work order along with its maintenance (see <see cref="WorkOrderEntity.FollowMaintenance"/>),
    /// counting <paramref name="maintenanceOrder"/> as <paramref name="status"/>, or leaving it out when
    /// null because it is being deleted. When that closes the work order, its aircraft's status is set
    /// from the aircraft's remaining open work orders, as a work order save does.
    /// </summary>
    /// <remarks>
    /// The work order is always saved with the change, so its row version check rejects one of two
    /// maintenance changes made at once under it: otherwise each could count the other as still open
    /// and neither would complete the work order.
    /// </remarks>
    private async Task<Result<WorkOrderWrite>> FollowWorkOrderAsync(
        MaintenanceOrderEntity maintenanceOrder,
        MaintenanceStatus? status,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var workOrder = await repository.GetWorkOrderForUpdateAsync(maintenanceOrder.WorkOrderId, cancellationToken);
        if (workOrder is null)
        {
            return Result<WorkOrderWrite>.Failure(MaintenanceOrderErrors.NotFound, MaintenanceOrderErrors.NotFoundMessage);
        }

        var others = await repository.GetMaintenanceProgressAsync(maintenanceOrder.WorkOrderId, maintenanceOrder.Id, cancellationToken);
        var changed = workOrder.FollowMaintenance(
            others.Total + (status is null ? 0 : 1),
            others.InProgress + (status == MaintenanceStatus.InProgress ? 1 : 0),
            others.Completed + (status == MaintenanceStatus.Completed ? 1 : 0),
            now);
        if (!changed || workOrder.IsOpen)
        {
            return Result<WorkOrderWrite>.Success(new WorkOrderWrite(workOrder, Aircraft: null));
        }

        var aircraft = await repository.GetAircraftForUpdateAsync(workOrder.AircraftId, cancellationToken);
        if (aircraft is null)
        {
            return Result<WorkOrderWrite>.Failure(MaintenanceOrderErrors.NotFound, MaintenanceOrderErrors.NotFoundMessage);
        }

        // Counted after the aircraft is loaded: a work order saved in between also writes the
        // aircraft, so its row version rejects this save instead of it using stale counts.
        var openWorkOrders = await repository.GetOpenWorkOrderCountsAsync(workOrder.AircraftId, workOrder.Id, cancellationToken);
        var applied = aircraft.ApplyOpenWorkOrders(openWorkOrders.Total, openWorkOrders.Critical > 0);

        return applied.IsSuccess
            ? Result<WorkOrderWrite>.Success(new WorkOrderWrite(workOrder, aircraft))
            : Result<WorkOrderWrite>.Failure(applied.ErrorCode!, applied.ErrorMessage!);
    }

    private sealed record WorkOrderWrite(WorkOrderEntity WorkOrder, AircraftEntity? Aircraft);

    /// <summary>Today's date in the business time zone, so "overdue" follows the operator's calendar.</summary>
    private DateOnly Today =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), _businessTimeZone).DateTime);
}
