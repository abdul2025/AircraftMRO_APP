using MaintenanceOrderEntity = AircraftMRO.Domain.Entities.MaintenanceOrder;

namespace AircraftMRO.Application.Features.MaintenanceOrders;

public static class MaintenanceOrderErrors
{
    public const string NotFound = "MaintenanceOrder.NotFound";
    public const string Validation = MaintenanceOrderEntity.ValidationErrorCode;
    public const string Closed = MaintenanceOrderEntity.ClosedErrorCode;
    public const string WorkOrderNotFound = "MaintenanceOrder.WorkOrderNotFound";
    public const string WorkOrderClosed = "MaintenanceOrder.WorkOrderClosed";
    public const string OnlyScheduledCanBeDeleted = "MaintenanceOrder.OnlyScheduledCanBeDeleted";
    public const string ConcurrencyConflict = "MaintenanceOrder.ConcurrencyConflict";

    /// <summary>
    /// The maintenance order was current but its work order changed during the save. Unlike
    /// <see cref="ConcurrencyConflict"/>, the caller's version is still valid, so retrying is safe.
    /// </summary>
    public const string WorkOrderChanged = "MaintenanceOrder.WorkOrderChanged";

    public const string NotFoundMessage = "The maintenance order was not found.";
    public const string WorkOrderNotFoundMessage = "The selected work order was not found.";
    public const string WorkOrderClosedMessage =
        "The work order is closed, so its maintenance can no longer be scheduled or changed.";
    public const string OnlyScheduledCanBeDeletedMessage =
        "Only scheduled maintenance can be deleted. Started or completed maintenance is kept as history.";
    public const string ConcurrencyConflictMessage =
        "This maintenance order was changed by someone else after you opened it. Reload and try again.";
    public const string WorkOrderChangedMessage =
        "The work order changed while this was being saved. Try saving again.";
}
