using System.Security.Claims;
using AircraftMRO.Application.Features.Employees.DTOs;
using AircraftMRO.Application.Features.Employees.Interfaces;
using AircraftMRO.Domain.Common.Results;
using AircraftMRO.Infrastructure.Identity;
using AircraftMRO.Web.Controllers;
using AircraftMRO.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SignInResult = Microsoft.AspNetCore.Identity.SignInResult;

namespace AircraftMRO.Tests;

public sealed class AccountControllerTests
{
    private const string ConfirmationLink = "https://localhost/Account/ConfirmEmail?userId=user-1&token=test-token";

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsLocalRedirectToReturnUrl()
    {
        var signInManager = CreateSignInManagerMock(out var userManagerMock);
        signInManager
            .Setup(m => m.PasswordSignInAsync("user@example.com", "Password1!", false, true))
            .ReturnsAsync(SignInResult.Success);
        var controller = CreateController(signInManager, userManagerMock);
        var model = new LoginViewModel
        {
            Email = "user@example.com",
            Password = "Password1!",
            ReturnUrl = "/aircraft"
        };

        var result = await controller.Login(model);

        var redirect = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/aircraft", redirect.Url);
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ReturnsViewWithGenericError()
    {
        var signInManager = CreateSignInManagerMock(out var userManagerMock);
        signInManager
            .Setup(m => m.PasswordSignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), true))
            .ReturnsAsync(SignInResult.Failed);
        var controller = CreateController(signInManager, userManagerMock);
        var model = new LoginViewModel { Email = "user@example.com", Password = "wrong" };

        var result = await controller.Login(model);

        var view = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains(
            controller.ModelState[string.Empty]!.Errors,
            error => error.ErrorMessage == "Invalid login attempt.");
        Assert.Same(model, view.Model);
    }

    [Fact]
    public async Task Login_WhenLockedOut_ReturnsSameGenericErrorAsInvalidCredentials()
    {
        var signInManager = CreateSignInManagerMock(out var userManagerMock);
        signInManager
            .Setup(m => m.PasswordSignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), true))
            .ReturnsAsync(SignInResult.LockedOut);
        var controller = CreateController(signInManager, userManagerMock);
        var model = new LoginViewModel { Email = "user@example.com", Password = "wrong" };

        var result = await controller.Login(model);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Contains(
            controller.ModelState[string.Empty]!.Errors,
            error => error.ErrorMessage == "Invalid login attempt.");
    }

    [Fact]
    public async Task Login_WhenNotAllowed_ReturnsSameGenericErrorAsInvalidCredentials()
    {
        var signInManager = CreateSignInManagerMock(out var userManagerMock);
        signInManager
            .Setup(m => m.PasswordSignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), true))
            .ReturnsAsync(SignInResult.NotAllowed);
        var controller = CreateController(signInManager, userManagerMock);
        var model = new LoginViewModel { Email = "unconfirmed@example.com", Password = "Password1!" };

        var result = await controller.Login(model);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Contains(
            controller.ModelState[string.Empty]!.Errors,
            error => error.ErrorMessage == "Invalid login attempt.");
    }

    [Fact]
    public async Task Logout_SignsOutAndRedirectsToHome()
    {
        var signInManager = CreateSignInManagerMock(out var userManagerMock);
        signInManager.Setup(m => m.SignOutAsync()).Returns(Task.CompletedTask);
        var controller = CreateController(signInManager, userManagerMock);

        var result = await controller.Logout();

        signInManager.Verify(m => m.SignOutAsync(), Times.Once);
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Home", redirect.ControllerName);
    }

    [Fact]
    public async Task Register_WithValidData_AddsEmployeeIdClaimAndReturnsConfirmationView()
    {
        var signInManager = CreateSignInManagerMock(out var userManagerMock);
        userManagerMock
            .Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), "Password1!"))
            .ReturnsAsync(IdentityResult.Success);
        userManagerMock
            .Setup(m => m.AddClaimAsync(It.IsAny<ApplicationUser>(), It.IsAny<Claim>()))
            .ReturnsAsync(IdentityResult.Success);
        userManagerMock
            .Setup(m => m.GenerateEmailConfirmationTokenAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync("test-token");
        var employeeServiceMock = new Mock<IEmployeeService>();
        employeeServiceMock
            .Setup(m => m.CreateEmployeeProfileAsync(It.IsAny<CreateEmployeeProfileDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        var controller = CreateController(signInManager, userManagerMock, employeeServiceMock);
        var model = new RegisterViewModel
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            EmployeeId = 1001,
            Email = "ada@example.com",
            Password = "Password1!",
            ConfirmPassword = "Password1!",
            ReturnUrl = "/aircraft"
        };

        var result = await controller.Register(model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("RegisterConfirmation", view.ViewName);
        var confirmationModel = Assert.IsType<RegisterConfirmationViewModel>(view.Model);
        Assert.Equal(ConfirmationLink, confirmationModel.ConfirmationLink);
        employeeServiceMock.Verify(
            m => m.CreateEmployeeProfileAsync(
                It.Is<CreateEmployeeProfileDto>(dto =>
                    dto.EmployeeId == 1001 && !string.IsNullOrEmpty(dto.UserId)),
                It.IsAny<CancellationToken>()),
            Times.Once);
        userManagerMock.Verify(
            m => m.AddClaimAsync(
                It.IsAny<ApplicationUser>(),
                It.Is<Claim>(c => c.Type == ApplicationClaimTypes.EmployeeId && c.Value == "1001")),
            Times.Once);
        signInManager.Verify(
            m => m.SignInAsync(It.IsAny<ApplicationUser>(), It.IsAny<bool>(), It.IsAny<string>()),
            Times.Never);
        userManagerMock.Verify(m => m.DeleteAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task Register_WhenEmployeeCreationFails_DeletesUserAndReturnsViewWithError()
    {
        var signInManager = CreateSignInManagerMock(out var userManagerMock);
        userManagerMock
            .Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), "Password1!"))
            .ReturnsAsync(IdentityResult.Success);
        userManagerMock
            .Setup(m => m.DeleteAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);
        var employeeServiceMock = new Mock<IEmployeeService>();
        employeeServiceMock
            .Setup(m => m.CreateEmployeeProfileAsync(It.IsAny<CreateEmployeeProfileDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(
                "Employee.DuplicateEmployeeId",
                "An employee with this Employee ID already exists."));
        var controller = CreateController(signInManager, userManagerMock, employeeServiceMock);
        var model = new RegisterViewModel
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            EmployeeId = 1001,
            Email = "ada@example.com",
            Password = "Password1!",
            ConfirmPassword = "Password1!"
        };

        var result = await controller.Register(model, CancellationToken.None);

        userManagerMock.Verify(m => m.DeleteAsync(It.IsAny<ApplicationUser>()), Times.Once);
        userManagerMock.Verify(
            m => m.AddClaimAsync(It.IsAny<ApplicationUser>(), It.IsAny<Claim>()),
            Times.Never);
        signInManager.Verify(
            m => m.SignInAsync(It.IsAny<ApplicationUser>(), It.IsAny<bool>(), It.IsAny<string>()),
            Times.Never);
        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
        Assert.Contains(
            controller.ModelState[nameof(RegisterViewModel.EmployeeId)]!.Errors,
            error => error.ErrorMessage == "An employee with this Employee ID already exists.");
    }

    [Fact]
    public async Task ConfirmEmail_WithValidToken_ReturnsSucceededView()
    {
        var signInManager = CreateSignInManagerMock(out var userManagerMock);
        var user = new ApplicationUser { Id = "user-1", Email = "ada@example.com" };
        userManagerMock.Setup(m => m.FindByIdAsync("user-1")).ReturnsAsync(user);
        userManagerMock
            .Setup(m => m.ConfirmEmailAsync(user, "good-token"))
            .ReturnsAsync(IdentityResult.Success);
        var controller = CreateController(signInManager, userManagerMock);

        var result = await controller.ConfirmEmail("user-1", "good-token");

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ConfirmEmailViewModel>(view.Model);
        Assert.True(model.Succeeded);
    }

    [Fact]
    public async Task ConfirmEmail_WithUnknownUser_ReturnsFailedViewWithoutCallingConfirm()
    {
        var signInManager = CreateSignInManagerMock(out var userManagerMock);
        userManagerMock.Setup(m => m.FindByIdAsync("missing")).ReturnsAsync((ApplicationUser?)null);
        var controller = CreateController(signInManager, userManagerMock);

        var result = await controller.ConfirmEmail("missing", "some-token");

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ConfirmEmailViewModel>(view.Model);
        Assert.False(model.Succeeded);
        userManagerMock.Verify(
            m => m.ConfirmEmailAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()),
            Times.Never);
    }

    private static AccountController CreateController(
        Mock<SignInManager<ApplicationUser>> signInManager,
        Mock<UserManager<ApplicationUser>> userManager,
        Mock<IEmployeeService>? employeeService = null)
    {
        var resolvedEmployeeService = employeeService ?? new Mock<IEmployeeService>();
        if (employeeService is null)
        {
            resolvedEmployeeService
                .Setup(m => m.CreateEmployeeProfileAsync(It.IsAny<CreateEmployeeProfileDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result.Success());
        }

        var controller = new AccountController(
            signInManager.Object,
            userManager.Object,
            resolvedEmployeeService.Object,
            Mock.Of<ILogger<AccountController>>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var urlHelperMock = new Mock<IUrlHelper>();
        urlHelperMock
            .Setup(u => u.Action(It.IsAny<UrlActionContext>()))
            .Returns(ConfirmationLink);
        controller.Url = urlHelperMock.Object;

        return controller;
    }

    private static Mock<SignInManager<ApplicationUser>> CreateSignInManagerMock(
        out Mock<UserManager<ApplicationUser>> userManagerMock)
    {
        var storeMock = new Mock<IUserStore<ApplicationUser>>();
        userManagerMock = new Mock<UserManager<ApplicationUser>>(
            storeMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        var contextAccessorMock = new Mock<IHttpContextAccessor>();
        var claimsFactoryMock = new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>();

        return new Mock<SignInManager<ApplicationUser>>(
            userManagerMock.Object,
            contextAccessorMock.Object,
            claimsFactoryMock.Object,
            Options.Create(new IdentityOptions()),
            Mock.Of<ILogger<SignInManager<ApplicationUser>>>(),
            Mock.Of<Microsoft.AspNetCore.Authentication.IAuthenticationSchemeProvider>(),
            Mock.Of<IUserConfirmation<ApplicationUser>>());
    }
}
