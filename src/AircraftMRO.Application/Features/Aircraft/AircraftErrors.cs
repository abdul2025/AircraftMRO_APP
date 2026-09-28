using AircraftEntity = AircraftMRO.Domain.Entities.Aircraft;

namespace AircraftMRO.Application.Features.Aircraft;

public static class AircraftErrors
{
    public const string NotFound = "Aircraft.NotFound";
    public const string Validation = AircraftEntity.ValidationErrorCode;
    public const string DuplicateRegistration = "Aircraft.DuplicateRegistration";
    public const string DuplicateSerialNumber = "Aircraft.DuplicateSerialNumber";
    public const string ConcurrencyConflict = "Aircraft.ConcurrencyConflict";
    public const string OpenCriticalWorkOrders = "Aircraft.OpenCriticalWorkOrders";
    public const string OpenWorkOrders = "Aircraft.OpenWorkOrders";
    public const string HasOpenWorkOrders = "Aircraft.HasOpenWorkOrders";

    public const string NotFoundMessage = "The aircraft was not found.";
    public const string OpenCriticalWorkOrdersMessage =
        "This aircraft has an open critical work order, so it must stay grounded. Complete or cancel that work order first.";
    public const string OpenWorkOrdersMessage =
        "This aircraft has open work orders, so it must stay in maintenance. Complete or cancel them first.";
    public const string HasOpenWorkOrdersMessage =
        "This aircraft has open work orders. Complete or cancel them before deleting it.";
    public const string DuplicateRegistrationMessage = "An aircraft with this registration number already exists.";
    public const string DuplicateSerialNumberMessage = "An aircraft with this manufacturer and serial number already exists.";
    public const string ConcurrencyConflictMessage =
        "This aircraft was changed by someone else after you opened it. Reload and try again.";
}
