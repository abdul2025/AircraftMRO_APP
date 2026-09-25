using AircraftMRO.Domain.Enums.Aircraft;

namespace AircraftMRO.Application.Features.Aircraft.DTOs;

/// <summary>Optional list filters; the search term matches registration, manufacturer, model, or serial number.</summary>
public sealed record AircraftListFilter
{
    public const int MaxSearchLength = 100;

    public static readonly AircraftListFilter None = new();

    public AircraftListFilter(string? search = null, AircraftStatus? status = null)
    {
        var trimmed = search?.Trim();
        Search = string.IsNullOrEmpty(trimmed)
            ? null
            : trimmed.Length > MaxSearchLength ? trimmed[..MaxSearchLength] : trimmed;
        Status = status is { } value && Enum.IsDefined(value) ? value : null;
    }

    public string? Search { get; }
    public AircraftStatus? Status { get; }
    public bool IsEmpty => Search is null && Status is null;
}
