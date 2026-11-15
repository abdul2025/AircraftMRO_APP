namespace AircraftMRO.Web.Models;

/// <summary>The audit values a Details page shows for any record.</summary>
public sealed record AuditTrailViewModel(
    Guid Id,
    DateTimeOffset CreatedAtUtc,
    string? CreatedBy,
    DateTimeOffset? UpdatedAtUtc,
    string? UpdatedBy);
