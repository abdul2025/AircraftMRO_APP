namespace AircraftMRO.Application.SystemFeatures;

public sealed record SystemFeatureListItem(
    string Code,
    string Title,
    string Description,
    string IconKey,
    string? ControllerName,
    string? ActionName,
    string StatusText,
    int DisplayOrder);
