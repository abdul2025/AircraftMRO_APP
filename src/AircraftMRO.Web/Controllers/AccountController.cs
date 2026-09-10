using System.Security.Claims;
using AircraftMRO.Application.Features.Employees.DTOs;
using AircraftMRO.Application.Features.Employees.Interfaces;
using AircraftMRO.Infrastructure.Identity;
using AircraftMRO.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AircraftMRO.Web.Controllers;

public sealed class AccountController(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    IEmployeeService employeeService,
    ILogger<AccountController> logger) : Controller
{
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null) =>
        View(new LoginViewModel { ReturnUrl = returnUrl });

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await signInManager.PasswordSignInAsync(
            model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            return LocalRedirect(model.ReturnUrl ?? Url.Action("Index", "Home")!);
        }

        ModelState.AddModelError(string.Empty, "Invalid login attempt.");
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register(string? returnUrl = null) =>
        View(new RegisterViewModel { ReturnUrl = returnUrl });

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FirstName = model.FirstName,
            LastName = model.LastName
        };

        var result = await userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        var employeeResult = await employeeService.CreateEmployeeProfileAsync(
            new CreateEmployeeProfileDto(user.Id, model.EmployeeId),
            cancellationToken);

        if (!employeeResult.IsSuccess)
        {
            var deleteResult = await userManager.DeleteAsync(user);
            if (!deleteResult.Succeeded)
            {
                logger.LogError(
                    "Failed to roll back user {UserId} after employee profile creation failed with {ErrorCode}.",
                    user.Id,
                    employeeResult.ErrorCode);
            }

            ModelState.AddModelError(nameof(RegisterViewModel.EmployeeId), employeeResult.ErrorMessage!);
            return View(model);
        }

        await userManager.AddClaimAsync(
            user,
            new Claim(ApplicationClaimTypes.EmployeeId, model.EmployeeId.ToString()));

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var confirmationLink = Url.Action(
            nameof(ConfirmEmail),
            "Account",
            new { userId = user.Id, token },
            Request.Scheme)!;

        return View("RegisterConfirmation", new RegisterConfirmationViewModel
        {
            ConfirmationLink = confirmationLink
        });
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmEmail(string? userId, string? token)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
        {
            return View(new ConfirmEmailViewModel { Succeeded = false });
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return View(new ConfirmEmailViewModel { Succeeded = false });
        }

        var result = await userManager.ConfirmEmailAsync(user, token);
        return View(new ConfirmEmailViewModel { Succeeded = result.Succeeded });
    }
}
