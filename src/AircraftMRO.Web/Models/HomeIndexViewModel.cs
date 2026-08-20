namespace AircraftMRO.Web.Models;

public sealed record HomeIndexViewModel(IReadOnlyList<SystemFeatureViewModel> Features);

public sealed record SystemFeatureViewModel(
    string Title,
    string Description,
    SystemFeatureIcon Icon,
    string? Controller = null,
    string? Action = null,
    string Status = "Coming soon")
{
    public bool IsAvailable => Controller is not null && Action is not null;
}

public enum SystemFeatureIcon
{
    Generic,
    Aircraft,
    WorkOrder,
    Maintenance,
    Compliance,
}
