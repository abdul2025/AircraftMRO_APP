namespace AircraftMRO.Application.Features.MaintenanceOrders.DTOs;

/// <summary>How far a work order's maintenance has got: all of it, and how much is in progress or completed.</summary>
public sealed record MaintenanceProgressDto(int Total, int InProgress, int Completed)
{
    public static readonly MaintenanceProgressDto None = new(0, 0, 0);
}
