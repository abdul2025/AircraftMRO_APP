namespace AircraftMRO.Application.Features.Aircraft.DTOs;

/// <summary>The choices an aircraft form offers: active entries plus the ones the aircraft already has.</summary>
public sealed record AircraftFormOptionsDto(
    IReadOnlyList<AircraftManufacturerOptionDto> Manufacturers,
    IReadOnlyList<AircraftModelOptionDto> Models)
{
    public static readonly AircraftFormOptionsDto Empty = new([], []);
}
