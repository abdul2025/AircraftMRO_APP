using AircraftMRO.Domain.Entities;

namespace AircraftMRO.Application.Features.AircraftManufacturers;

public static class AircraftManufacturerErrors
{
    public const string NotFound = "AircraftManufacturer.NotFound";
    public const string Validation = AircraftManufacturer.ValidationErrorCode;
    public const string DuplicateCode = "AircraftManufacturer.DuplicateCode";
    public const string ConcurrencyConflict = "AircraftManufacturer.ConcurrencyConflict";
    public const string InUse = "AircraftManufacturer.InUse";
    public const string CountryNotFound = "AircraftManufacturer.CountryNotFound";
    public const string CountryInactive = "AircraftManufacturer.CountryInactive";

    public const string NotFoundMessage = "The manufacturer was not found.";
    public const string DuplicateCodeMessage = "A manufacturer with this code already exists.";
    public const string ConcurrencyConflictMessage =
        "This manufacturer was changed by someone else after you opened it. Reload and try again.";
    public const string InUseMessage =
        "This manufacturer still has aircraft models or aircraft. Remove them or deactivate the manufacturer instead.";
    public const string CountryNotFoundMessage = "The selected country was not found.";
    public const string CountryInactiveMessage = "The selected country is inactive. Choose an active country.";
}
