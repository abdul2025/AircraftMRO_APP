using System.Text.RegularExpressions;
using AircraftMRO.Domain.Common.Entities;
using AircraftMRO.Domain.Common.Results;
using AircraftMRO.Domain.Enums.Aircraft;

namespace AircraftMRO.Domain.Entities;

public sealed partial class Aircraft : AuditableEntity, IHasDisplayName
{
    public const string ValidationErrorCode = "Aircraft.Validation";
    public const string RetiredErrorCode = "Aircraft.Retired";
    public const int RegistrationNumberMinLength = 2;
    public const int RegistrationNumberMaxLength = 10;
    public const int SerialNumberMaxLength = 50;
    public const int FirstYearOfManufacture = 1903;
    public const decimal MaxTotalFlightHours = 99_999_999.9m;

    private Aircraft()
    {
    }

    public string RegistrationNumber { get; private set; } = null!;
    public Guid ManufacturerId { get; private set; }

    /// <summary>Always one of <see cref="ManufacturerId"/>'s models; the database enforces the pair.</summary>
    public Guid ModelId { get; private set; }
    public string SerialNumber { get; private set; } = null!;
    public int YearOfManufacture { get; private set; }
    public decimal TotalFlightHours { get; private set; }
    public AircraftStatus Status { get; private set; }

    public string DisplayName => RegistrationNumber;

    public static Result<Aircraft> Create(
        string registrationNumber,
        Guid manufacturerId,
        Guid modelId,
        string serialNumber,
        int yearOfManufacture,
        decimal totalFlightHours,
        AircraftStatus status,
        int currentYear)
    {
        var aircraft = new Aircraft();
        var result = aircraft.Apply(
            registrationNumber, manufacturerId, modelId, serialNumber,
            yearOfManufacture, totalFlightHours, status, currentYear);

        return result.IsSuccess
            ? Result<Aircraft>.Success(aircraft)
            : Result<Aircraft>.Failure(result.ErrorCode!, result.ErrorMessage!);
    }

    public Result Update(
        string registrationNumber,
        Guid manufacturerId,
        Guid modelId,
        string serialNumber,
        int yearOfManufacture,
        decimal totalFlightHours,
        AircraftStatus status,
        int currentYear) =>
        Apply(
            registrationNumber, manufacturerId, modelId, serialNumber,
            yearOfManufacture, totalFlightHours, status, currentYear);

    public bool IsRetired => Status == AircraftStatus.Retired;

    /// <summary>The status open work orders require: grounded when any is critical, required maintenance otherwise.</summary>
    public static AircraftStatus StatusForOpenWorkOrders(bool anyCritical) =>
        anyCritical ? AircraftStatus.Grounded : AircraftStatus.RequiredMaintenance;

    /// <summary>
    /// Sets the status from the aircraft's open work orders after a work order changed: grounded
    /// while any is critical, required maintenance while any other is open, and back to active once the
    /// last one is completed or cancelled. A retired aircraft cannot have open work orders.
    /// </summary>
    public Result ApplyOpenWorkOrders(int openCount, bool anyCritical)
    {
        if (openCount == 0)
        {
            if (Status is AircraftStatus.RequiredMaintenance or AircraftStatus.Grounded)
            {
                Status = AircraftStatus.Active;
            }

            return Result.Success();
        }

        if (IsRetired)
        {
            return Result.Failure(RetiredErrorCode, "Work orders cannot be raised or changed for a retired aircraft.");
        }

        Status = StatusForOpenWorkOrders(anyCritical);
        return Result.Success();
    }

    public static string NormalizeRegistrationNumber(string? registrationNumber) =>
        (registrationNumber ?? string.Empty).Trim().ToUpperInvariant();

    private Result Apply(
        string registrationNumber,
        Guid manufacturerId,
        Guid modelId,
        string serialNumber,
        int yearOfManufacture,
        decimal totalFlightHours,
        AircraftStatus status,
        int currentYear)
    {
        var normalizedRegistration = NormalizeRegistrationNumber(registrationNumber);
        var trimmedSerial = serialNumber?.Trim() ?? string.Empty;

        if (normalizedRegistration.Length is < RegistrationNumberMinLength or > RegistrationNumberMaxLength
            || !RegistrationPattern().IsMatch(normalizedRegistration))
        {
            return Invalid(
                $"Registration number must be {RegistrationNumberMinLength}-{RegistrationNumberMaxLength} letters, digits, or hyphens.");
        }

        if (manufacturerId == Guid.Empty)
        {
            return Invalid("Manufacturer is required.");
        }

        if (modelId == Guid.Empty)
        {
            return Invalid("Model is required.");
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
        ManufacturerId = manufacturerId;
        ModelId = modelId;
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
