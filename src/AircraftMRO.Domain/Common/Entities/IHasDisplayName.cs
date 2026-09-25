namespace AircraftMRO.Domain.Common.Entities;

/// <summary>A human-readable name used when an entity appears in notifications.</summary>
public interface IHasDisplayName
{
    string DisplayName { get; }
}
