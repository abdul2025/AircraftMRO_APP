namespace AircraftMRO.Application.Features.AircraftModels.DTOs;

/// <summary>Optional list filters; the search term matches the model name, model code, or manufacturer name.</summary>
public sealed record AircraftModelListFilter
{
    public const int MaxSearchLength = 100;

    public static readonly AircraftModelListFilter None = new();

    public AircraftModelListFilter(string? search = null, Guid? manufacturerId = null, bool? isActive = null)
    {
        var trimmed = search?.Trim();
        Search = string.IsNullOrEmpty(trimmed)
            ? null
            : trimmed.Length > MaxSearchLength ? trimmed[..MaxSearchLength] : trimmed;
        ManufacturerId = manufacturerId == Guid.Empty ? null : manufacturerId;
        IsActive = isActive;
    }

    public string? Search { get; }
    public Guid? ManufacturerId { get; }
    public bool? IsActive { get; }
    public bool IsEmpty => Search is null && ManufacturerId is null && IsActive is null;
}
