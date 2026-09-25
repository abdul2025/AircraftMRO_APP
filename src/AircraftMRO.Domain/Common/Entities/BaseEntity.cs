namespace AircraftMRO.Domain.Common.Entities;

public abstract class BaseEntity
{
    /// <summary>Assigned by persistence when the entity is added; empty until then.</summary>
    public Guid Id { get; protected set; }
}
