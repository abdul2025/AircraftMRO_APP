using System.Diagnostics;
using AircraftMRO.Application.SystemFeatures;
using Microsoft.AspNetCore.Mvc;
using AircraftMRO.Web.Models;

namespace AircraftMRO.Web.Controllers;

public class HomeController(ISystemFeatureReader systemFeatureReader) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var features = await systemFeatureReader.ListVisibleAsync(cancellationToken);
        var model = new HomeIndexViewModel(
            features
                .Select(feature => new SystemFeatureViewModel(
                    feature.Title,
                    feature.Description,
                    MapIcon(feature.IconKey),
                    feature.ControllerName,
                    feature.ActionName,
                    feature.StatusText))
                .ToArray());

        return View(model);
    }

    private static SystemFeatureIcon MapIcon(string iconKey) =>
        iconKey switch
        {
            "aircraft" => SystemFeatureIcon.Aircraft,
            "work-order" => SystemFeatureIcon.WorkOrder,
            "maintenance" => SystemFeatureIcon.Maintenance,
            "compliance" => SystemFeatureIcon.Compliance,
            _ => SystemFeatureIcon.Generic
        };

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
