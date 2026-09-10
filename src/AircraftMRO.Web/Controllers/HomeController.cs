using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using AircraftMRO.Web.Models;

namespace AircraftMRO.Web.Controllers;

public class HomeController() : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return View();
    }

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
