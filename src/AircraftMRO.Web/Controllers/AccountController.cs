using Microsoft.AspNetCore.Mvc;

namespace AircraftMRO.Web.Controllers;

public sealed class AccountController : Controller
{
    [HttpGet]
    public IActionResult Login() => View();
}
