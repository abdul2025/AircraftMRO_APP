using AircraftMRO.Domain.Entities;
using AircraftMRO.Domain.Enums.MaintenanceOrders;

namespace AircraftMRO.Infrastructure.Persistence.Features.MaintenanceOrders;

/// <summary>Translatable forms of <see cref="MaintenanceOrder.IsOpen"/> and <see cref="MaintenanceOrder.IsOverdue"/>.</summary>
internal static class MaintenanceOrderQueries
{
    public static IQueryable<MaintenanceOrder> WhereOpen(this IQueryable<MaintenanceOrder> query) =>
        query.Where(maintenanceOrder => maintenanceOrder.Status != MaintenanceStatus.Completed);

    public static IQueryable<MaintenanceOrder> WhereOverdue(this IQueryable<MaintenanceOrder> query, DateOnly today) =>
        query.WhereOpen().Where(maintenanceOrder => maintenanceOrder.ScheduledDate < today);
}
