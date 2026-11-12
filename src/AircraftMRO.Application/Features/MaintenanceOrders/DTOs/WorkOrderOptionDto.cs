namespace AircraftMRO.Application.Features.MaintenanceOrders.DTOs;

/// <summary>A work order maintenance can be filtered by or scheduled under.</summary>
public sealed record WorkOrderOptionDto(Guid Id, string Number, string AircraftRegistration, bool IsOpen)
{
    public bool CanScheduleMaintenance => IsOpen;
}
