using AircraftMRO.Domain.Entities;

namespace AircraftMRO.Application.Features.Countries;

public static class CountryErrors
{
    public const string NotFound = "Country.NotFound";
    public const string Validation = Country.ValidationErrorCode;
    public const string DuplicateCode = "Country.DuplicateCode";
    public const string ConcurrencyConflict = "Country.ConcurrencyConflict";
    public const string InUse = "Country.InUse";

    public const string NotFoundMessage = "The country was not found.";
    public const string DuplicateCodeMessage = "A country with this code already exists.";
    public const string ConcurrencyConflictMessage =
        "This country was changed by someone else after you opened it. Reload and try again.";
    public const string InUseMessage =
        "Manufacturers are recorded against this country. Change them or deactivate the country instead.";
}
