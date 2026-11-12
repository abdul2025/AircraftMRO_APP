using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.MaintenanceOrders;
using AircraftMRO.Application.Features.MaintenanceOrders.DTOs;
using AircraftMRO.Application.Features.MaintenanceOrders.Ports;
using AircraftMRO.Domain.Common.Results;
using AircraftMRO.Domain.Entities;
using AircraftMRO.Domain.Enums.MaintenanceOrders;
using AircraftMRO.Domain.Enums.WorkOrders;
using AircraftMRO.Application.Features.Aircraft.DTOs;
using AircraftMRO.Infrastructure.Persistence.Features.WorkOrders;
using Microsoft.EntityFrameworkCore;
using AircraftEntity = AircraftMRO.Domain.Entities.Aircraft;

namespace AircraftMRO.Infrastructure.Persistence.Features.MaintenanceOrders;

internal sealed class MaintenanceOrderRepository(AircraftMroDbContext dbContext) : IMaintenanceOrderRepository
{
    /// <summary>Upper bound for the work order picker; open work orders come first so they are never cut off.</summary>
    public const int MaxWorkOrderOptions = 1000;

    public async Task<PagedResponse<MaintenanceOrderListItemDto>> ListAsync(
        MaintenanceOrderListFilter filter,
        PagedRequest request,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var maintenanceOrders = dbContext.MaintenanceOrders.AsNoTracking();

        if (filter.WorkOrderId is { } workOrderId)
        {
            maintenanceOrders = maintenanceOrders.Where(maintenanceOrder => maintenanceOrder.WorkOrderId == workOrderId);
        }

        if (filter.Status is { } status)
        {
            maintenanceOrders = maintenanceOrders.Where(maintenanceOrder => maintenanceOrder.Status == status);
        }

        if (filter.Type is { } type)
        {
            maintenanceOrders = maintenanceOrders.Where(maintenanceOrder => maintenanceOrder.Type == type);
        }

        if (filter.OpenOnly)
        {
            maintenanceOrders = maintenanceOrders.WhereOpen();
        }

        if (filter.OverdueOnly)
        {
            maintenanceOrders = maintenanceOrders.WhereOverdue(today);
        }

        // The joins also hide maintenance whose work order or aircraft was soft-deleted.
        var rows =
            from maintenanceOrder in maintenanceOrders
            join workOrder in dbContext.WorkOrders on maintenanceOrder.WorkOrderId equals workOrder.Id
            join aircraft in dbContext.Aircraft on workOrder.AircraftId equals aircraft.Id
            select new { MaintenanceOrder = maintenanceOrder, WorkOrderNumber = workOrder.Number, aircraft.RegistrationNumber };

        var totalCount = await rows.CountAsync(cancellationToken);
        var items = await rows
            // Open maintenance first, soonest scheduled first; completed sinks to the end.
            .OrderBy(row => row.MaintenanceOrder.Status == MaintenanceStatus.Completed ? 1 : 0)
            .ThenBy(row => row.MaintenanceOrder.ScheduledDate)
            .ThenBy(row => row.MaintenanceOrder.Id)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .Select(row => new MaintenanceOrderListItemDto(
                row.MaintenanceOrder.Id,
                row.MaintenanceOrder.WorkOrderId,
                row.WorkOrderNumber,
                row.RegistrationNumber,
                row.MaintenanceOrder.Type,
                row.MaintenanceOrder.Status,
                row.MaintenanceOrder.ScheduledDate,
                row.MaintenanceOrder.Status != MaintenanceStatus.Completed && row.MaintenanceOrder.ScheduledDate < today,
                row.MaintenanceOrder.CompletedAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResponse<MaintenanceOrderListItemDto>(items, request.Page, request.PageSize, totalCount);
    }

    public async Task<MaintenanceOrderStatisticsDto> GetStatisticsAsync(
        Guid? workOrderId,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        // Same visibility as the list: maintenance of a soft-deleted work order or aircraft is left out.
        var maintenanceOrders = dbContext.MaintenanceOrders
            .AsNoTracking()
            .Where(maintenanceOrder => dbContext.WorkOrders.Any(workOrder => workOrder.Id == maintenanceOrder.WorkOrderId
                && dbContext.Aircraft.Any(aircraft => aircraft.Id == workOrder.AircraftId)));
        if (workOrderId is { } id)
        {
            maintenanceOrders = maintenanceOrders.Where(maintenanceOrder => maintenanceOrder.WorkOrderId == id);
        }

        var groups = await maintenanceOrders
            .GroupBy(maintenanceOrder => maintenanceOrder.Status)
            .Select(group => new
            {
                Status = group.Key,
                Count = group.Count(),
                PastDue = group.Count(maintenanceOrder => maintenanceOrder.ScheduledDate < today)
            })
            .ToListAsync(cancellationToken);

        return new MaintenanceOrderStatisticsDto(
            groups.Sum(group => group.Count),
            groups.ToDictionary(group => group.Status, group => group.Count),
            groups.Where(group => MaintenanceOrder.IsOpenStatus(group.Status)).Sum(group => group.PastDue));
    }

    public async Task<IReadOnlyList<WorkOrderOptionDto>> ListWorkOrderOptionsAsync(CancellationToken cancellationToken) =>
        await ToOptions(dbContext.WorkOrders
                .AsNoTracking()
                .OrderBy(workOrder => workOrder.Status == WorkOrderStatus.Completed
                    || workOrder.Status == WorkOrderStatus.Cancelled ? 1 : 0)
                .ThenByDescending(workOrder => workOrder.Number))
            .Take(MaxWorkOrderOptions)
            .ToListAsync(cancellationToken);

    public Task<WorkOrderOptionDto?> GetWorkOrderOptionAsync(Guid workOrderId, CancellationToken cancellationToken) =>
        ToOptions(dbContext.WorkOrders.AsNoTracking().Where(workOrder => workOrder.Id == workOrderId))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<MaintenanceOrderDto?> GetByIdAsync(Guid id, DateOnly today, CancellationToken cancellationToken) =>
        (from maintenanceOrder in dbContext.MaintenanceOrders.AsNoTracking()
         join workOrder in dbContext.WorkOrders on maintenanceOrder.WorkOrderId equals workOrder.Id
         join aircraft in dbContext.Aircraft on workOrder.AircraftId equals aircraft.Id
         where maintenanceOrder.Id == id
         select new MaintenanceOrderDto(
             maintenanceOrder.Id,
             maintenanceOrder.WorkOrderId,
             workOrder.Number,
             aircraft.Id,
             aircraft.RegistrationNumber,
             maintenanceOrder.Type,
             maintenanceOrder.Status,
             maintenanceOrder.ScheduledDate,
             maintenanceOrder.Status != MaintenanceStatus.Completed && maintenanceOrder.ScheduledDate < today,
             maintenanceOrder.CompletedAtUtc,
             maintenanceOrder.Notes,
             maintenanceOrder.CreatedAtUtc,
             maintenanceOrder.CreatedBy,
             maintenanceOrder.UpdatedAtUtc,
             maintenanceOrder.UpdatedBy,
             EF.Property<byte[]>(maintenanceOrder, AircraftMroDbContext.RowVersionPropertyName)))
        .SingleOrDefaultAsync(cancellationToken);

    /// <summary>Hides maintenance whose work order was soft-deleted, matching what the reads return.</summary>
    public Task<MaintenanceOrder?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.MaintenanceOrders
            .Where(maintenanceOrder => dbContext.WorkOrders.Any(workOrder => workOrder.Id == maintenanceOrder.WorkOrderId))
            .SingleOrDefaultAsync(maintenanceOrder => maintenanceOrder.Id == id, cancellationToken);

    public Task<WorkOrder?> GetWorkOrderForUpdateAsync(Guid workOrderId, CancellationToken cancellationToken) =>
        dbContext.WorkOrders.SingleOrDefaultAsync(workOrder => workOrder.Id == workOrderId, cancellationToken);

    public Task<AircraftEntity?> GetAircraftForUpdateAsync(Guid aircraftId, CancellationToken cancellationToken) =>
        dbContext.Aircraft.SingleOrDefaultAsync(aircraft => aircraft.Id == aircraftId, cancellationToken);

    public Task<OpenWorkOrderCounts> GetOpenWorkOrderCountsAsync(
        Guid aircraftId,
        Guid excludeWorkOrderId,
        CancellationToken cancellationToken) =>
        dbContext.WorkOrders
            .AsNoTracking()
            .Where(workOrder => workOrder.AircraftId == aircraftId && workOrder.Id != excludeWorkOrderId)
            .CountOpenAsync(cancellationToken);

    public async Task<MaintenanceProgressDto> GetMaintenanceProgressAsync(
        Guid workOrderId,
        Guid excludeMaintenanceOrderId,
        CancellationToken cancellationToken) =>
        await dbContext.MaintenanceOrders
            .AsNoTracking()
            .Where(maintenanceOrder => maintenanceOrder.WorkOrderId == workOrderId
                && maintenanceOrder.Id != excludeMaintenanceOrderId)
            .GroupBy(_ => 1)
            .Select(group => new MaintenanceProgressDto(
                group.Count(),
                group.Count(maintenanceOrder => maintenanceOrder.Status == MaintenanceStatus.InProgress),
                group.Count(maintenanceOrder => maintenanceOrder.Status == MaintenanceStatus.Completed)))
            .SingleOrDefaultAsync(cancellationToken)
        ?? MaintenanceProgressDto.None;

    public Task<Result> AddAsync(MaintenanceOrder maintenanceOrder, WorkOrder workOrder, CancellationToken cancellationToken)
    {
        dbContext.MaintenanceOrders.Add(maintenanceOrder);
        WriteWorkOrder(workOrder);
        return SaveAsync(cancellationToken);
    }

    public Task<Result> UpdateAsync(
        MaintenanceOrder maintenanceOrder,
        byte[] expectedRowVersion,
        WorkOrder? workOrder,
        AircraftEntity? aircraft,
        CancellationToken cancellationToken)
    {
        SetExpectedRowVersion(maintenanceOrder, expectedRowVersion);
        WriteRelated(workOrder, aircraft);
        return SaveAsync(cancellationToken);
    }

    public Task<Result> DeleteAsync(
        MaintenanceOrder maintenanceOrder,
        byte[] expectedRowVersion,
        WorkOrder? workOrder,
        AircraftEntity? aircraft,
        CancellationToken cancellationToken)
    {
        SetExpectedRowVersion(maintenanceOrder, expectedRowVersion);
        dbContext.MaintenanceOrders.Remove(maintenanceOrder);
        WriteRelated(workOrder, aircraft);
        return SaveAsync(cancellationToken);
    }

    private void WriteRelated(WorkOrder? workOrder, AircraftEntity? aircraft)
    {
        if (workOrder is not null)
        {
            WriteWorkOrder(workOrder);
        }

        if (aircraft is not null)
        {
            // Same as a work order save: written even when unchanged, so its row version is checked.
            dbContext.Entry(aircraft).Property(entity => entity.Status).IsModified = true;
        }
    }

    /// <summary>
    /// Projects work orders to picker options, keeping their order. The join also drops work
    /// orders whose aircraft was soft-deleted. Filter and sort before calling: a constructor
    /// projection cannot be composed further.
    /// </summary>
    private IQueryable<WorkOrderOptionDto> ToOptions(IQueryable<WorkOrder> workOrders) =>
        from workOrder in workOrders
        join aircraft in dbContext.Aircraft on workOrder.AircraftId equals aircraft.Id
        select new WorkOrderOptionDto(
            workOrder.Id,
            workOrder.Number,
            aircraft.RegistrationNumber,
            workOrder.Status != WorkOrderStatus.Completed && workOrder.Status != WorkOrderStatus.Cancelled);

    /// <summary>
    /// Writes the work order even when it did not change, so the save carries its row version
    /// check: a work order changed since it was read rejects the whole save; see
    /// <see cref="MaintenanceOrderService"/>. Its change notification is skipped when no value
    /// actually changed. Two such saves under one work order at the same moment also collide
    /// here; the second gets a retryable <c>WorkOrderChanged</c>.
    /// </summary>
    private void WriteWorkOrder(WorkOrder workOrder) =>
        dbContext.Entry(workOrder).Property(entity => entity.Status).IsModified = true;

    private void SetExpectedRowVersion(MaintenanceOrder maintenanceOrder, byte[] expectedRowVersion) =>
        dbContext.Entry(maintenanceOrder).Property(AircraftMroDbContext.RowVersionPropertyName).OriginalValue =
            expectedRowVersion;

    private async Task<Result> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException exception)
        {
            // When only the work order or aircraft failed its version check, the caller's
            // maintenance order version is still valid, so report it as retryable.
            return exception.Entries.All(entry => entry.Entity is WorkOrder or AircraftEntity)
                ? Result.Failure(MaintenanceOrderErrors.WorkOrderChanged, MaintenanceOrderErrors.WorkOrderChangedMessage)
                : Result.Failure(MaintenanceOrderErrors.ConcurrencyConflict, MaintenanceOrderErrors.ConcurrencyConflictMessage);
        }
    }
}
