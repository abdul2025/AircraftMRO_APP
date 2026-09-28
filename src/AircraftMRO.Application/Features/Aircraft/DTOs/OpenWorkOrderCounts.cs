namespace AircraftMRO.Application.Features.Aircraft.DTOs;

/// <summary>Open (not completed or cancelled) work orders on one aircraft.</summary>
public sealed record OpenWorkOrderCounts(int Total, int Critical)
{
    public static readonly OpenWorkOrderCounts None = new(0, 0);
}
