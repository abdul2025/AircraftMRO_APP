namespace AircraftMRO.Application.Features.Countries.DTOs;

/// <summary>Optional list filters; the search term matches the name or code.</summary>
public sealed record CountryListFilter
{
    public const int MaxSearchLength = 100;

    public static readonly CountryListFilter None = new();

    public CountryListFilter(string? search = null, bool? isActive = null)
    {
        var trimmed = search?.Trim();
        Search = string.IsNullOrEmpty(trimmed)
            ? null
            : trimmed.Length > MaxSearchLength ? trimmed[..MaxSearchLength] : trimmed;
        IsActive = isActive;
    }

    public string? Search { get; }
    public bool? IsActive { get; }
    public bool IsEmpty => Search is null && IsActive is null;
}
