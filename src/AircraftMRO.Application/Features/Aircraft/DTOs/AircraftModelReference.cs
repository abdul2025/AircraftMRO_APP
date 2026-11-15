namespace AircraftMRO.Application.Features.Aircraft.DTOs;

/// <summary>What the aircraft rules need to know about a selected model and its manufacturer.</summary>
public sealed record AircraftModelReference(Guid Id, Guid ManufacturerId, bool IsActive, bool ManufacturerIsActive);
