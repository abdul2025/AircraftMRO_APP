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
    public const string ModelNotFound = "Aircraft.ModelNotFound";
    public const string ModelManufacturerMismatch = "Aircraft.ModelManufacturerMismatch";
    public const string ManufacturerInactive = "Aircraft.ManufacturerInactive";
    public const string ModelInactive = "Aircraft.ModelInactive";

    public const string NotFoundMessage = "The aircraft was not found.";
    public const string OpenCriticalWorkOrdersMessage =
        "This aircraft has an open critical work order, so it must stay grounded. Complete or cancel that work order first.";
    public const string OpenWorkOrdersMessage =
        "This aircraft has open work orders, so it must stay in required maintenance. Complete or cancel them first.";
    public const string HasOpenWorkOrdersMessage =
        "This aircraft has open work orders. Complete or cancel them before deleting it.";
    public const string DuplicateRegistrationMessage = "An aircraft with this registration number already exists.";
    public const string DuplicateSerialNumberMessage = "An aircraft with this manufacturer and serial number already exists.";
    public const string ModelNotFoundMessage = "The selected model was not found.";
    public const string ModelManufacturerMismatchMessage = "The selected model is not made by the selected manufacturer.";
    public const string ManufacturerInactiveMessage =
        "The selected manufacturer is inactive. Choose an active manufacturer.";
    public const string ModelInactiveMessage = "The selected model is inactive. Choose an active model.";
    public const string ConcurrencyConflictMessage =
        "This aircraft was changed by someone else after you opened it. Reload and try again.";
}
