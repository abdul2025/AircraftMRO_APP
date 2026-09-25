using AircraftEntity = AircraftMRO.Domain.Entities.Aircraft;

namespace AircraftMRO.Application.Features.Aircraft;

public static class AircraftErrors
{
    public const string NotFound = "Aircraft.NotFound";
    public const string Validation = AircraftEntity.ValidationErrorCode;
    public const string DuplicateRegistration = "Aircraft.DuplicateRegistration";
    public const string DuplicateSerialNumber = "Aircraft.DuplicateSerialNumber";
    public const string ConcurrencyConflict = "Aircraft.ConcurrencyConflict";

    public const string NotFoundMessage = "The aircraft was not found.";
    public const string DuplicateRegistrationMessage = "An aircraft with this registration number already exists.";
    public const string DuplicateSerialNumberMessage = "An aircraft with this manufacturer and serial number already exists.";
    public const string ConcurrencyConflictMessage =
        "This aircraft was changed by someone else after you opened it. Reload and try again.";
}
