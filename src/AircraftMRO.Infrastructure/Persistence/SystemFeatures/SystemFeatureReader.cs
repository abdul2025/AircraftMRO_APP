using AircraftMRO.Application.SystemFeatures;
using Microsoft.EntityFrameworkCore;

namespace AircraftMRO.Infrastructure.Persistence;

internal sealed class SystemFeatureReader(AircraftMroDbContext dbContext)
    : ISystemFeatureReader
{
    public async Task<IReadOnlyList<SystemFeatureListItem>> ListVisibleAsync(
        CancellationToken cancellationToken) =>
        await dbContext.SystemFeatures
            .AsNoTracking()
            .Where(feature => feature.IsVisible)
            .OrderBy(feature => feature.DisplayOrder)
            .ThenBy(feature => feature.Id)
            .Select(feature => new SystemFeatureListItem(
                feature.Code,
                feature.Title,
                feature.Description,
                feature.IconKey,
                feature.ControllerName,
                feature.ActionName,
                feature.StatusText,
                feature.DisplayOrder))
            .Take(100)
            .ToListAsync(cancellationToken);
}
