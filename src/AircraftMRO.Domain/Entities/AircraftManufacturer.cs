using System.Text.RegularExpressions;
using AircraftMRO.Domain.Common.Entities;
using AircraftMRO.Domain.Common.Results;

namespace AircraftMRO.Domain.Entities;

/// <summary>Master reference data: a company that builds aircraft.</summary>
public sealed partial class AircraftManufacturer : AuditableEntity, IHasDisplayName
{
    public const string ValidationErrorCode = "AircraftManufacturer.Validation";
    public const int NameMaxLength = 100;
    public const int CodeMaxLength = 10;

    private AircraftManufacturer()
    {
    }

    public string Name { get; private set; } = null!;

    /// <summary>Short upper-case code, unique among live manufacturers.</summary>
    public string Code { get; private set; } = null!;

    /// <summary>
    /// Required whenever a manufacturer is created or updated. Only manufacturers carried over from
    /// the free-text aircraft data can still have none until someone edits them.
    /// </summary>
    public Guid? CountryId { get; private set; }

    public bool IsActive { get; private set; }

    public string DisplayName => Name;

    public static Result<AircraftManufacturer> Create(string name, string code, Guid countryId, bool isActive)
    {
        var manufacturer = new AircraftManufacturer();
        var result = manufacturer.Apply(name, code, countryId, isActive);

        return result.IsSuccess
            ? Result<AircraftManufacturer>.Success(manufacturer)
            : Result<AircraftManufacturer>.Failure(result.ErrorCode!, result.ErrorMessage!);
    }

    public Result Update(string name, string code, Guid countryId, bool isActive) =>
        Apply(name, code, countryId, isActive);

    public static string NormalizeCode(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();

    private Result Apply(string name, string code, Guid countryId, bool isActive)
    {
        var trimmedName = name?.Trim() ?? string.Empty;
        var normalizedCode = NormalizeCode(code);

        if (trimmedName.Length is 0 or > NameMaxLength)
        {
            return Invalid($"Name is required and must be at most {NameMaxLength} characters.");
        }

        if (normalizedCode.Length is 0 or > CodeMaxLength || !CodePattern().IsMatch(normalizedCode))
        {
            return Invalid($"Code must be 1-{CodeMaxLength} letters, digits, or hyphens.");
        }

        if (countryId == Guid.Empty)
        {
            return Invalid("Country is required.");
        }

        Name = trimmedName;
        Code = normalizedCode;
        CountryId = countryId;
        IsActive = isActive;

        return Result.Success();
    }

    private static Result Invalid(string message) => Result.Failure(ValidationErrorCode, message);

    [GeneratedRegex("^[A-Z0-9]+(-[A-Z0-9]+)*$")]
    private static partial Regex CodePattern();
}
