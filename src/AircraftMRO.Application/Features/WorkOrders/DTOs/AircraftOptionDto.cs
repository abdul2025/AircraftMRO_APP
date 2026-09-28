using AircraftMRO.Domain.Enums.Aircraft;

namespace AircraftMRO.Application.Features.WorkOrders.DTOs;

/// <summary>An aircraft a work order can be filtered by or raised against.</summary>
public sealed record AircraftOptionDto(Guid Id, string RegistrationNumber, AircraftStatus Status)
{
    public bool CanRaiseWorkOrders => Status != AircraftStatus.Retired;
}
