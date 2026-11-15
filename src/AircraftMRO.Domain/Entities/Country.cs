using AircraftMRO.Domain.Common.Entities;
using AircraftMRO.Domain.Common.Results;

namespace AircraftMRO.Domain.Entities;

/// <summary>A country, identified by its ISO 3166-1 alpha-2 code, that other records can refer to.</summary>
public sealed class Country : AuditableEntity, IHasDisplayName
{
    public const string ValidationErrorCode = "Country.Validation";
    public const int NameMaxLength = 100;
    public const int CodeLength = 2;

    private Country()
    {
    }

    public string Name { get; private set; } = null!;

    /// <summary>ISO 3166-1 alpha-2 code, stored upper-case.</summary>
    public string Code { get; private set; } = null!;

    public bool IsActive { get; private set; }

    public string DisplayName => Name;

    public static Result<Country> Create(string name, string code, bool isActive)
    {
        var country = new Country();
        var result = country.Apply(name, code, isActive);

        return result.IsSuccess
            ? Result<Country>.Success(country)
            : Result<Country>.Failure(result.ErrorCode!, result.ErrorMessage!);
    }

    public Result Update(string name, string code, bool isActive) => Apply(name, code, isActive);

    public static string NormalizeCode(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();

    private Result Apply(string name, string code, bool isActive)
    {
        var trimmedName = name?.Trim() ?? string.Empty;
        var normalizedCode = NormalizeCode(code);

        if (trimmedName.Length is 0 or > NameMaxLength)
        {
            return Invalid($"Name is required and must be at most {NameMaxLength} characters.");
        }

        if (normalizedCode.Length != CodeLength || !normalizedCode.All(char.IsAsciiLetterUpper))
        {
            return Invalid($"Code must be the {CodeLength}-letter ISO country code.");
        }

        Name = trimmedName;
        Code = normalizedCode;
        IsActive = isActive;

        return Result.Success();
    }

    private static Result Invalid(string message) => Result.Failure(ValidationErrorCode, message);
}
