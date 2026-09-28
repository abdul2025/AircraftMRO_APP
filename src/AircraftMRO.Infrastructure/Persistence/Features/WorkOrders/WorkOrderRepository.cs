using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.Aircraft.DTOs;
using AircraftMRO.Application.Features.WorkOrders;
using AircraftMRO.Application.Features.WorkOrders.DTOs;
using AircraftMRO.Application.Features.WorkOrders.Ports;
using AircraftMRO.Domain.Common.Results;
using AircraftMRO.Domain.Entities;
using AircraftMRO.Domain.Enums.WorkOrders;
using Microsoft.EntityFrameworkCore;
using AircraftEntity = AircraftMRO.Domain.Entities.Aircraft;

namespace AircraftMRO.Infrastructure.Persistence.Features.WorkOrders;

internal sealed class WorkOrderRepository(AircraftMroDbContext dbContext) : IWorkOrderRepository
{
    /// <summary>Upper bound for the aircraft picker; far above any fleet this application serves.</summary>
    public const int MaxAircraftOptions = 1000;

    // Not composed with LINQ, so it runs as written: NEXT VALUE FOR is not allowed in a derived table.
    private const string NextNumberSql =
        "SELECT NEXT VALUE FOR [" + WorkOrderConfiguration.NumberSequenceName + "] AS [Value]";

    public async Task<PagedResponse<WorkOrderListItemDto>> ListAsync(
        WorkOrderListFilter filter,
        PagedRequest request,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var workOrders = dbContext.WorkOrders.AsNoTracking();

        if (filter.AircraftId is { } aircraftId)
        {
            workOrders = workOrders.Where(workOrder => workOrder.AircraftId == aircraftId);
        }

        if (filter.Status is { } status)
        {
            workOrders = workOrders.Where(workOrder => workOrder.Status == status);
        }

        if (filter.Priority is { } priority)
        {
            workOrders = workOrders.Where(workOrder => workOrder.Priority == priority);
        }

        if (filter.OpenOnly)
        {
            workOrders = workOrders.WhereOpen();
        }

        if (filter.OverdueOnly)
        {
            workOrders = workOrders.WhereOverdue(today);
        }

        // The join also hides work orders whose aircraft was soft-deleted.
        var rows =
            from workOrder in workOrders
            join aircraft in dbContext.Aircraft on workOrder.AircraftId equals aircraft.Id
            select new { WorkOrder = workOrder, aircraft.RegistrationNumber };

        if (filter.Search is { } search)
        {
            // EF Core escapes LIKE wildcards in the term; the column collation makes it case-insensitive.
            rows = rows.Where(row =>
                row.WorkOrder.Number.Contains(search)
                || row.WorkOrder.Title.Contains(search)
                || row.RegistrationNumber.Contains(search));
        }

        var totalCount = await rows.CountAsync(cancellationToken);
        var items = await rows
            // Open work first, most urgent first, then soonest due; newest number breaks ties.
            .OrderBy(row => row.WorkOrder.Status == WorkOrderStatus.Completed
                || row.WorkOrder.Status == WorkOrderStatus.Cancelled ? 1 : 0)
            .ThenByDescending(row => row.WorkOrder.Priority)
            .ThenBy(row => row.WorkOrder.DueDate == null ? 1 : 0)
            .ThenBy(row => row.WorkOrder.DueDate)
            .ThenByDescending(row => row.WorkOrder.Number)
            .ThenBy(row => row.WorkOrder.Id)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .Select(row => new WorkOrderListItemDto(
                row.WorkOrder.Id,
                row.WorkOrder.AircraftId,
                row.RegistrationNumber,
                row.WorkOrder.Number,
                row.WorkOrder.Title,
                row.WorkOrder.Priority,
                row.WorkOrder.Status,
                row.WorkOrder.DueDate,
                row.WorkOrder.Status != WorkOrderStatus.Completed
                    && row.WorkOrder.Status != WorkOrderStatus.Cancelled
                    && row.WorkOrder.DueDate != null
                    && row.WorkOrder.DueDate < today))
            .ToListAsync(cancellationToken);

        return new PagedResponse<WorkOrderListItemDto>(items, request.Page, request.PageSize, totalCount);
    }

    public async Task<WorkOrderStatisticsDto> GetStatisticsAsync(
        Guid? aircraftId,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var workOrders = LiveWorkOrders();
        if (aircraftId is { } id)
        {
            workOrders = workOrders.Where(workOrder => workOrder.AircraftId == id);
        }

        var groups = await workOrders
            .GroupBy(workOrder => new { workOrder.Status, workOrder.Priority })
            .Select(group => new
            {
                group.Key.Status,
                group.Key.Priority,
                Count = group.Count(),
                PastDue = group.Count(workOrder => workOrder.DueDate != null && workOrder.DueDate < today)
            })
            .ToListAsync(cancellationToken);

        var open = groups.Where(group => WorkOrder.IsOpenStatus(group.Status)).ToList();

        return new WorkOrderStatisticsDto(
            groups.Sum(group => group.Count),
            groups.GroupBy(group => group.Status).ToDictionary(g => g.Key, g => g.Sum(group => group.Count)),
            open.GroupBy(group => group.Priority).ToDictionary(g => g.Key, g => g.Sum(group => group.Count)),
            open.Sum(group => group.PastDue));
    }

    public async Task<IReadOnlyList<AircraftOptionDto>> ListAircraftOptionsAsync(CancellationToken cancellationToken) =>
        await dbContext.Aircraft
            .AsNoTracking()
            .OrderBy(aircraft => aircraft.RegistrationNumber)
            .Take(MaxAircraftOptions)
            .Select(aircraft => new AircraftOptionDto(aircraft.Id, aircraft.RegistrationNumber, aircraft.Status))
            .ToListAsync(cancellationToken);

    public Task<WorkOrderDto?> GetByIdAsync(Guid id, DateOnly today, CancellationToken cancellationToken) =>
        (from workOrder in dbContext.WorkOrders.AsNoTracking()
         join aircraft in dbContext.Aircraft on workOrder.AircraftId equals aircraft.Id
         where workOrder.Id == id
         select new WorkOrderDto(
             workOrder.Id,
             workOrder.AircraftId,
             aircraft.RegistrationNumber,
             workOrder.Number,
             workOrder.Title,
             workOrder.Description,
             workOrder.Priority,
             workOrder.Status,
             workOrder.DueDate,
             workOrder.Status != WorkOrderStatus.Completed
                 && workOrder.Status != WorkOrderStatus.Cancelled
                 && workOrder.DueDate != null
                 && workOrder.DueDate < today,
             workOrder.CompletedAtUtc,
             workOrder.CreatedAtUtc,
             workOrder.CreatedBy,
             workOrder.UpdatedAtUtc,
             workOrder.UpdatedBy,
             EF.Property<byte[]>(workOrder, AircraftMroDbContext.RowVersionPropertyName)))
        .SingleOrDefaultAsync(cancellationToken);

    public Task<WorkOrder?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.WorkOrders.SingleOrDefaultAsync(workOrder => workOrder.Id == id, cancellationToken);

    public Task<AircraftEntity?> GetAircraftForUpdateAsync(Guid aircraftId, CancellationToken cancellationToken) =>
        dbContext.Aircraft.SingleOrDefaultAsync(aircraft => aircraft.Id == aircraftId, cancellationToken);

    public Task<OpenWorkOrderCounts> GetOpenWorkOrderCountsAsync(
        Guid aircraftId,
        Guid? excludeWorkOrderId,
        CancellationToken cancellationToken) =>
        dbContext.WorkOrders
            .AsNoTracking()
            .Where(workOrder => workOrder.AircraftId == aircraftId
                && (excludeWorkOrderId == null || workOrder.Id != excludeWorkOrderId))
            .CountOpenAsync(cancellationToken);

    public async Task<string> NextNumberAsync(CancellationToken cancellationToken)
    {
        var values = await dbContext.Database.SqlQueryRaw<long>(NextNumberSql).ToListAsync(cancellationToken);
        return $"WO-{values.Single():D6}";
    }

    public Task<Result> AddAsync(WorkOrder workOrder, AircraftEntity aircraft, CancellationToken cancellationToken)
    {
        dbContext.WorkOrders.Add(workOrder);
        WriteAircraft(aircraft);
        return SaveAsync(cancellationToken);
    }

    public Task<Result> UpdateAsync(
        WorkOrder workOrder,
        byte[] expectedRowVersion,
        AircraftEntity aircraft,
        CancellationToken cancellationToken)
    {
        SetExpectedRowVersion(workOrder, expectedRowVersion);
        WriteAircraft(aircraft);
        return SaveAsync(cancellationToken);
    }

    public Task<Result> DeleteAsync(
        WorkOrder workOrder,
        byte[] expectedRowVersion,
        CancellationToken cancellationToken)
    {
        SetExpectedRowVersion(workOrder, expectedRowVersion);
        dbContext.WorkOrders.Remove(workOrder);
        return SaveAsync(cancellationToken);
    }

    /// <summary>Work orders whose aircraft still exists (is not soft-deleted).</summary>
    private IQueryable<WorkOrder> LiveWorkOrders() =>
        dbContext.WorkOrders
            .AsNoTracking()
            .Where(workOrder => dbContext.Aircraft.Any(aircraft => aircraft.Id == workOrder.AircraftId));

    /// <summary>
    /// Writes the aircraft even when its status did not change, so the save carries its row
    /// version check: an aircraft edited since it was read rejects the whole save. The change
    /// notification skips it when no value actually changed.
    /// </summary>
    private void WriteAircraft(AircraftEntity aircraft) =>
        dbContext.Entry(aircraft).Property(entity => entity.Status).IsModified = true;

    private void SetExpectedRowVersion(WorkOrder workOrder, byte[] expectedRowVersion) =>
        dbContext.Entry(workOrder).Property(AircraftMroDbContext.RowVersionPropertyName).OriginalValue =
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
            // The entries are the rows whose version check failed. When only the aircraft failed,
            // the caller's work order version is still valid, so report it as retryable.
            return exception.Entries.All(entry => entry.Entity is AircraftEntity)
                ? Result.Failure(WorkOrderErrors.AircraftChanged, WorkOrderErrors.AircraftChangedMessage)
                : Result.Failure(WorkOrderErrors.ConcurrencyConflict, WorkOrderErrors.ConcurrencyConflictMessage);
        }
    }
}
