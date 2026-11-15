using System.Text.RegularExpressions;
using AircraftMRO.Domain.Common.Entities;
using AircraftMRO.Domain.Common.Results;
using AircraftMRO.Domain.Enums.AircraftModels;

namespace AircraftMRO.Domain.Entities;

/// <summary>Master reference data: an aircraft type built by one manufacturer.</summary>
public sealed partial class AircraftModel : AuditableEntity, IHasDisplayName
{
    public const string ValidationErrorCode = "AircraftModel.Validation";
    public const int NameMaxLength = 100;
    public const int ModelCodeMaxLength = 20;

    private AircraftModel()
    {
    }

    /// <summary>Set when the model is created and never changed, because aircraft rely on the pair.</summary>
    public Guid ManufacturerId { get; private set; }

    public string Name { get; private set; } = null!;

    /// <summary>Upper-case code, unique among the manufacturer's live models.</summary>
    public string ModelCode { get; private set; } = null!;

    public AircraftCategory AircraftCategory { get; private set; }
    public bool IsActive { get; private set; }

    public string DisplayName => Name;

    public static Result<AircraftModel> Create(
        Guid manufacturerId,
        string name,
        string modelCode,
        AircraftCategory category,
        bool isActive)
    {
        if (manufacturerId == Guid.Empty)
        {
            return Result<AircraftModel>.Failure(ValidationErrorCode, "Manufacturer is required.");
        }

        var model = new AircraftModel { ManufacturerId = manufacturerId };
        var result = model.Apply(name, modelCode, category, isActive);

        return result.IsSuccess
            ? Result<AircraftModel>.Success(model)
            : Result<AircraftModel>.Failure(result.ErrorCode!, result.ErrorMessage!);
    }

    public Result Update(string name, string modelCode, AircraftCategory category, bool isActive) =>
        Apply(name, modelCode, category, isActive);

    public static string NormalizeModelCode(string? modelCode) => (modelCode ?? string.Empty).Trim().ToUpperInvariant();

    private Result Apply(string name, string modelCode, AircraftCategory category, bool isActive)
    {
        var trimmedName = name?.Trim() ?? string.Empty;
        var normalizedCode = NormalizeModelCode(modelCode);

        if (trimmedName.Length is 0 or > NameMaxLength)
        {
            return Invalid($"Name is required and must be at most {NameMaxLength} characters.");
        }

        if (normalizedCode.Length is 0 or > ModelCodeMaxLength || !CodePattern().IsMatch(normalizedCode))
        {
            return Invalid($"Model code must be 1-{ModelCodeMaxLength} letters, digits, or hyphens.");
        }

        if (!Enum.IsDefined(category))
        {
            return Invalid("Aircraft category is not valid.");
        }

        Name = trimmedName;
        ModelCode = normalizedCode;
        AircraftCategory = category;
        IsActive = isActive;

        return Result.Success();
    }

    private static Result Invalid(string message) => Result.Failure(ValidationErrorCode, message);

    [GeneratedRegex("^[A-Z0-9]+(-[A-Z0-9]+)*$")]
    private static partial Regex CodePattern();
}
