using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.WorkOrders.DTOs;
using AircraftMRO.Application.Features.WorkOrders.Interfaces;
using AircraftMRO.Application.Features.WorkOrders.Ports;
using AircraftMRO.Domain.Common.Results;
using AircraftMRO.Domain.Enums.WorkOrders;
using WorkOrderEntity = AircraftMRO.Domain.Entities.WorkOrder;

namespace AircraftMRO.Application.Features.WorkOrders;

/// <summary>
/// Every work order save also sets its aircraft's status from all of that aircraft's open work
/// orders (grounded, in maintenance, or back to active) in the same save, so the two never disagree.
/// </summary>
public sealed class WorkOrderService(
    IWorkOrderRepository repository,
    TimeProvider timeProvider,
    WorkOrderOptions options) : IWorkOrderService
{
    private readonly TimeZoneInfo _businessTimeZone = options.ResolveTimeZone();

    public async Task<Result<PagedResponse<WorkOrderListItemDto>>> ListAsync(
        WorkOrderListFilter filter,
        PagedRequest request,
        CancellationToken cancellationToken) =>
        Result<PagedResponse<WorkOrderListItemDto>>.Success(
            await repository.ListAsync(filter, request, Today, cancellationToken));

    public async Task<Result<WorkOrderStatisticsDto>> GetStatisticsAsync(Guid? aircraftId, CancellationToken cancellationToken) =>
        Result<WorkOrderStatisticsDto>.Success(await repository.GetStatisticsAsync(aircraftId, Today, cancellationToken));

    public async Task<Result<IReadOnlyList<AircraftOptionDto>>> ListAircraftOptionsAsync(CancellationToken cancellationToken) =>
        Result<IReadOnlyList<AircraftOptionDto>>.Success(await repository.ListAircraftOptionsAsync(cancellationToken));

    public async Task<Result<WorkOrderDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var workOrder = await repository.GetByIdAsync(id, Today, cancellationToken);

        return workOrder is null
            ? Result<WorkOrderDto>.Failure(WorkOrderErrors.NotFound, WorkOrderErrors.NotFoundMessage)
            : Result<WorkOrderDto>.Success(workOrder);
    }

    public async Task<Result<Guid>> CreateAsync(CreateWorkOrderDto dto, CancellationToken cancellationToken)
    {
        var aircraft = await repository.GetAircraftForUpdateAsync(dto.AircraftId, cancellationToken);
        if (aircraft is null)
        {
            return Result<Guid>.Failure(WorkOrderErrors.AircraftNotFound, WorkOrderErrors.AircraftNotFoundMessage);
        }

        // A new work order is always open. Applied before a number is reserved, so a rejected
        // aircraft does not use one up; nothing is saved if a later step fails.
        var others = await repository.GetOpenWorkOrderCountsAsync(dto.AircraftId, excludeWorkOrderId: null, cancellationToken);
        var applied = aircraft.ApplyOpenWorkOrders(
            others.Total + 1,
            others.Critical > 0 || dto.Priority == WorkOrderPriority.Critical);
        if (applied.IsFailure)
        {
            return Result<Guid>.Failure(applied.ErrorCode!, applied.ErrorMessage!);
        }

        var number = await repository.NextNumberAsync(cancellationToken);
        var created = WorkOrderEntity.Create(dto.AircraftId, number, dto.Title, dto.Description, dto.Priority, dto.DueDate);
        if (created.IsFailure)
        {
            return Result<Guid>.Failure(created.ErrorCode!, created.ErrorMessage!);
        }

        var workOrder = created.Value!;
        var saved = await repository.AddAsync(workOrder, aircraft, cancellationToken);

        return saved.IsSuccess
            ? Result<Guid>.Success(workOrder.Id)
            : Result<Guid>.Failure(saved.ErrorCode!, saved.ErrorMessage!);
    }

    public async Task<Result> UpdateAsync(Guid id, UpdateWorkOrderDto dto, CancellationToken cancellationToken)
    {
        var workOrder = await repository.GetForUpdateAsync(id, cancellationToken);
        if (workOrder is null)
        {
            return Result.Failure(WorkOrderErrors.NotFound, WorkOrderErrors.NotFoundMessage);
        }

        var updated = workOrder.Update(
            dto.Title, dto.Description, dto.Priority, dto.Status, dto.DueDate, timeProvider.GetUtcNow());
        if (updated.IsFailure)
        {
            return updated;
        }

        var aircraft = await repository.GetAircraftForUpdateAsync(workOrder.AircraftId, cancellationToken);
        if (aircraft is null)
        {
            return Result.Failure(WorkOrderErrors.AircraftNotFound, WorkOrderErrors.AircraftNotFoundMessage);
        }

        // Counted after the aircraft is loaded: a work order saved in between also writes the
        // aircraft, so its row version rejects this save instead of it using stale counts.
        var others = await repository.GetOpenWorkOrderCountsAsync(workOrder.AircraftId, workOrder.Id, cancellationToken);
        var applied = aircraft.ApplyOpenWorkOrders(
            others.Total + (workOrder.IsOpen ? 1 : 0),
            others.Critical > 0 || (workOrder.IsOpen && workOrder.Priority == WorkOrderPriority.Critical));
        if (applied.IsFailure)
        {
            return applied;
        }

        return await repository.UpdateAsync(workOrder, dto.RowVersion, aircraft, cancellationToken);
    }

    /// <summary>
    /// Only closed work orders can be deleted, so the reason an aircraft's status changed is
    /// always recorded as a completion or cancellation.
    /// </summary>
    public async Task<Result> DeleteAsync(Guid id, byte[] rowVersion, CancellationToken cancellationToken)
    {
        var workOrder = await repository.GetForUpdateAsync(id, cancellationToken);
        if (workOrder is null)
        {
            return Result.Failure(WorkOrderErrors.NotFound, WorkOrderErrors.NotFoundMessage);
        }

        if (workOrder.IsOpen)
        {
            return Result.Failure(WorkOrderErrors.OpenCannotBeDeleted, WorkOrderErrors.OpenCannotBeDeletedMessage);
        }

        return await repository.DeleteAsync(workOrder, rowVersion, cancellationToken);
    }

    /// <summary>Today's date in the business time zone, so "overdue" follows the operator's calendar.</summary>
    private DateOnly Today =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), _businessTimeZone).DateTime);
}
