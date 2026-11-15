namespace AircraftMRO.Application.Features.Aircraft.DTOs;

/// <summary>A model an aircraft can be recorded against; it belongs to <paramref name="ManufacturerId"/>.</summary>
public sealed record AircraftModelOptionDto(Guid Id, Guid ManufacturerId, string Name, string ModelCode, bool IsActive);
