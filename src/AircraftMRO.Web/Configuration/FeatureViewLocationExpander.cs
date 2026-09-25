using Microsoft.AspNetCore.Mvc.Razor;

namespace AircraftMRO.Web.Configuration;

/// <summary>
/// Resolves views from <c>Features/{Controller}/Views/</c> before the default MVC locations.
/// </summary>
public sealed class FeatureViewLocationExpander : IViewLocationExpander
{
    private static readonly string[] FeatureLocations =
    [
        "/Features/{1}/Views/{0}.cshtml"
    ];

    public void PopulateValues(ViewLocationExpanderContext context)
    {
    }

    public IEnumerable<string> ExpandViewLocations(
        ViewLocationExpanderContext context,
        IEnumerable<string> viewLocations) =>
        FeatureLocations.Concat(viewLocations);
}
