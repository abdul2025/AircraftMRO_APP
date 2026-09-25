namespace AircraftMRO.Domain.Common.Entities;

/// <summary>
/// Base for every persisted entity. Audit and soft-delete values are stamped by
/// Infrastructure when changes are saved; domain and presentation code never set them.
/// </summary>
public abstract class AuditableEntity : BaseEntity
{
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }
    public string? UpdatedBy { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAtUtc { get; private set; }
    public string? DeletedBy { get; private set; }
}
