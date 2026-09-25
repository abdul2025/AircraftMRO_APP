using System.Text.RegularExpressions;
using AircraftMRO.Domain.Common.Entities;
using AircraftMRO.Domain.Common.Results;
using AircraftMRO.Domain.Enums.Aircraft;

namespace AircraftMRO.Domain.Entities;

public sealed partial class Aircraft : AuditableEntity
{
    public const string ValidationErrorCode = "Aircraft.Validation";
    public const int RegistrationNumberMinLength = 2;
    public const int RegistrationNumberMaxLength = 10;
    public const int ManufacturerMaxLength = 100;
    public const int ModelMaxLength = 100;
    public const int SerialNumberMaxLength = 50;
    public const int FirstYearOfManufacture = 1903;
    public const decimal MaxTotalFlightHours = 99_999_999.9m;

    private Aircraft()
    {
    }

    public string RegistrationNumber { get; private set; } = null!;
    public string Manufacturer { get; private set; } = null!;
    public string Model { get; private set; } = null!;
    public string SerialNumber { get; private set; } = null!;
    public int YearOfManufacture { get; private set; }
    public decimal TotalFlightHours { get; private set; }
    public AircraftStatus Status { get; private set; }

    public static Result<Aircraft> Create(
        string registrationNumber,
        string manufacturer,
        string model,
        string serialNumber,
        int yearOfManufacture,
        decimal totalFlightHours,
        AircraftStatus status,
        int currentYear)
    {
        var aircraft = new Aircraft();
        var result = aircraft.Apply(
            registrationNumber, manufacturer, model, serialNumber,
            yearOfManufacture, totalFlightHours, status, currentYear);

        return result.IsSuccess
            ? Result<Aircraft>.Success(aircraft)
            : Result<Aircraft>.Failure(result.ErrorCode!, result.ErrorMessage!);
    }

    public Result Update(
        string registrationNumber,
        string manufacturer,
        string model,
        string serialNumber,
        int yearOfManufacture,
        decimal totalFlightHours,
        AircraftStatus status,
        int currentYear) =>
        Apply(
            registrationNumber, manufacturer, model, serialNumber,
            yearOfManufacture, totalFlightHours, status, currentYear);

    public static string NormalizeRegistrationNumber(string? registrationNumber) =>
        (registrationNumber ?? string.Empty).Trim().ToUpperInvariant();

    private Result Apply(
        string registrationNumber,
        string manufacturer,
        string model,
        string serialNumber,
        int yearOfManufacture,
        decimal totalFlightHours,
        AircraftStatus status,
        int currentYear)
    {
        var normalizedRegistration = NormalizeRegistrationNumber(registrationNumber);
        var trimmedManufacturer = manufacturer?.Trim() ?? string.Empty;
        var trimmedModel = model?.Trim() ?? string.Empty;
        var trimmedSerial = serialNumber?.Trim() ?? string.Empty;

        if (normalizedRegistration.Length is < RegistrationNumberMinLength or > RegistrationNumberMaxLength
            || !RegistrationPattern().IsMatch(normalizedRegistration))
        {
            return Invalid(
                $"Registration number must be {RegistrationNumberMinLength}-{RegistrationNumberMaxLength} letters, digits, or hyphens.");
        }

        if (trimmedManufacturer.Length is 0 or > ManufacturerMaxLength)
        {
            return Invalid($"Manufacturer is required and must be at most {ManufacturerMaxLength} characters.");
        }

        if (trimmedModel.Length is 0 or > ModelMaxLength)
        {
            return Invalid($"Model is required and must be at most {ModelMaxLength} characters.");
        }

        if (trimmedSerial.Length is 0 or > SerialNumberMaxLength)
        {
            return Invalid($"Serial number is required and must be at most {SerialNumberMaxLength} characters.");
        }

        if (yearOfManufacture < FirstYearOfManufacture || yearOfManufacture > currentYear + 1)
        {
            return Invalid($"Year of manufacture must be between {FirstYearOfManufacture} and {currentYear + 1}.");
        }

        if (totalFlightHours < 0 || totalFlightHours > MaxTotalFlightHours
            || decimal.Round(totalFlightHours, 1) != totalFlightHours)
        {
            return Invalid("Total flight hours must be zero or greater with at most one decimal place.");
        }

        if (!Enum.IsDefined(status))
        {
            return Invalid("Status is not valid.");
        }

        RegistrationNumber = normalizedRegistration;
        Manufacturer = trimmedManufacturer;
        Model = trimmedModel;
        SerialNumber = trimmedSerial;
        YearOfManufacture = yearOfManufacture;
        TotalFlightHours = totalFlightHours;
        Status = status;

        return Result.Success();
    }

    private static Result Invalid(string message) => Result.Failure(ValidationErrorCode, message);

    [GeneratedRegex("^[A-Z0-9]+(-[A-Z0-9]+)*$")]
    private static partial Regex RegistrationPattern();
}
