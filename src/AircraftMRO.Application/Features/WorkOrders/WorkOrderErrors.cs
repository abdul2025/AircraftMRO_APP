using AircraftEntity = AircraftMRO.Domain.Entities.Aircraft;
using WorkOrderEntity = AircraftMRO.Domain.Entities.WorkOrder;

namespace AircraftMRO.Application.Features.WorkOrders;

public static class WorkOrderErrors
{
    public const string NotFound = "WorkOrder.NotFound";
    public const string Validation = WorkOrderEntity.ValidationErrorCode;
    public const string Closed = WorkOrderEntity.ClosedErrorCode;
    public const string AircraftNotFound = "WorkOrder.AircraftNotFound";
    public const string AircraftRetired = AircraftEntity.RetiredErrorCode;
    public const string ConcurrencyConflict = "WorkOrder.ConcurrencyConflict";
    public const string OpenCannotBeDeleted = "WorkOrder.OpenCannotBeDeleted";

    /// <summary>
    /// The work order was current but its aircraft, or another of its work orders, changed during
    /// the save. Unlike <see cref="ConcurrencyConflict"/>, the caller's version is still valid, so
    /// retrying is safe.
    /// </summary>
    public const string AircraftChanged = "WorkOrder.AircraftChanged";

    public const string NotFoundMessage = "The work order was not found.";
    public const string AircraftNotFoundMessage = "The selected aircraft was not found.";
    public const string OpenCannotBeDeletedMessage =
        "Open work orders can't be deleted. Complete or cancel it first so the aircraft's status change is recorded.";
    public const string ConcurrencyConflictMessage =
        "This work order was changed by someone else after you opened it. Reload and try again.";
    public const string AircraftChangedMessage =
        "The aircraft or another of its work orders changed while this was being saved. Try saving again.";
}
