namespace AircraftMRO.Application.SystemFeatures;

public interface ISystemFeatureReader
{
    Task<IReadOnlyList<SystemFeatureListItem>> ListVisibleAsync(
        CancellationToken cancellationToken);
}
