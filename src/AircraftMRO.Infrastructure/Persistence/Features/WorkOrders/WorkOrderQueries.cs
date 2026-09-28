using AircraftMRO.Application.Features.Aircraft.DTOs;
using AircraftMRO.Domain.Entities;
using AircraftMRO.Domain.Enums.WorkOrders;
using Microsoft.EntityFrameworkCore;

namespace AircraftMRO.Infrastructure.Persistence.Features.WorkOrders;

/// <summary>Translatable forms of <see cref="WorkOrder.IsOpen"/> and <see cref="WorkOrder.IsOverdue"/>.</summary>
internal static class WorkOrderQueries
{
    public static IQueryable<WorkOrder> WhereOpen(this IQueryable<WorkOrder> query) =>
        query.Where(workOrder =>
            workOrder.Status != WorkOrderStatus.Completed && workOrder.Status != WorkOrderStatus.Cancelled);

    public static IQueryable<WorkOrder> WhereOverdue(this IQueryable<WorkOrder> query, DateOnly today) =>
        query.WhereOpen().Where(workOrder => workOrder.DueDate != null && workOrder.DueDate < today);

    /// <summary>Counts the open work orders in <paramref name="query"/>, and how many of them are critical.</summary>
    public static async Task<OpenWorkOrderCounts> CountOpenAsync(
        this IQueryable<WorkOrder> query,
        CancellationToken cancellationToken) =>
        await query
            .WhereOpen()
            .GroupBy(_ => 1)
            .Select(group => new OpenWorkOrderCounts(
                group.Count(),
                group.Count(workOrder => workOrder.Priority == WorkOrderPriority.Critical)))
            .SingleOrDefaultAsync(cancellationToken)
        ?? OpenWorkOrderCounts.None;
}
