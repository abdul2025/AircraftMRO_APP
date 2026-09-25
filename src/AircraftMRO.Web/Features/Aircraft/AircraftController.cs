using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.Aircraft;
using AircraftMRO.Application.Features.Aircraft.DTOs;
using AircraftMRO.Application.Features.Aircraft.Interfaces;
using AircraftMRO.Domain.Common.Results;
using AircraftMRO.Domain.Enums.Aircraft;
using AircraftMRO.Web.Features.Aircraft.Models;
using Microsoft.AspNetCore.Mvc;

namespace AircraftMRO.Web.Features.Aircraft;

/// <summary>
/// Create, edit, and delete render as modal content when requested with the modal header
/// (see <c>wwwroot/js/modal-forms.js</c>) and as full pages otherwise.
/// </summary>
public sealed class AircraftController(IAircraftService aircraftService) : Controller
{
    public const string ModalRequestHeader = "X-Modal-Request";
    public const string ModalReturnUrlHeader = "X-Modal-Return-Url";
    private const string StatusMessageKey = "StatusMessage";
    private const string ErrorMessageKey = "ErrorMessage";

    private bool IsModalRequest => Request.Headers[ModalRequestHeader] == "true";

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search = null,
        AircraftStatus? status = null,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        var filter = new AircraftListFilter(search, status);
        var list = await aircraftService.ListAsync(filter, new PagedRequest(page), cancellationToken);
        var statistics = await aircraftService.GetStatisticsAsync(cancellationToken);

        return View(new AircraftListViewModel(list.Value!, statistics.Value!, filter));
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var result = await aircraftService.GetByIdAsync(id, cancellationToken);
        return result.IsSuccess ? View(AircraftDetailsViewModel.From(result.Value!)) : NotFound();
    }

    [HttpGet]
    public IActionResult Create() => FormView(nameof(Create), new AircraftFormViewModel());

    [HttpPost]
    public async Task<IActionResult> Create(AircraftFormViewModel form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return FormView(nameof(Create), form, StatusCodes.Status422UnprocessableEntity);
        }

        var result = await aircraftService.CreateAsync(
            new CreateAircraftDto(
                form.RegistrationNumber,
                form.Manufacturer,
                form.Model,
                form.SerialNumber,
                form.YearOfManufacture!.Value,
                form.TotalFlightHours!.Value,
                form.Status!.Value),
            cancellationToken);

        if (result.IsFailure)
        {
            AddError(result.ErrorCode!, result.ErrorMessage!);
            return FormView(nameof(Create), form, StatusFor(result.ErrorCode!));
        }

        return Saved("Aircraft created.", ModalReturnUrl ?? Url.Action(nameof(Details), new { id = result.Value })!);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var result = await aircraftService.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure)
        {
            return NotFound();
        }

        var aircraft = result.Value!;
        return FormView(nameof(Edit), new AircraftFormViewModel
        {
            RegistrationNumber = aircraft.RegistrationNumber,
            Manufacturer = aircraft.Manufacturer,
            Model = aircraft.Model,
            SerialNumber = aircraft.SerialNumber,
            YearOfManufacture = aircraft.YearOfManufacture,
            TotalFlightHours = aircraft.TotalFlightHours,
            Status = aircraft.Status,
            RowVersion = Convert.ToBase64String(aircraft.RowVersion)
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(Guid id, AircraftFormViewModel form, CancellationToken cancellationToken)
    {
        if (!TryDecodeRowVersion(form.RowVersion, out var rowVersion))
        {
            ModelState.AddModelError(string.Empty, AircraftErrors.ConcurrencyConflictMessage);
        }

        if (!ModelState.IsValid)
        {
            return FormView(nameof(Edit), form, StatusCodes.Status422UnprocessableEntity);
        }

        var result = await aircraftService.UpdateAsync(
            id,
            new UpdateAircraftDto(
                form.RegistrationNumber,
                form.Manufacturer,
                form.Model,
                form.SerialNumber,
                form.YearOfManufacture!.Value,
                form.TotalFlightHours!.Value,
                form.Status!.Value,
                rowVersion),
            cancellationToken);

        if (result.ErrorCode == AircraftErrors.NotFound)
        {
            return NotFound();
        }

        if (result.IsFailure)
        {
            AddError(result.ErrorCode!, result.ErrorMessage!);
            return FormView(nameof(Edit), form, StatusFor(result.ErrorCode!));
        }

        return Saved("Aircraft updated.", ModalReturnUrl ?? Url.Action(nameof(Details), new { id })!);
    }

    [HttpGet]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await aircraftService.GetByIdAsync(id, cancellationToken);
        return result.IsSuccess ? FormView(nameof(Delete), AircraftDetailsViewModel.From(result.Value!)) : NotFound();
    }

    [HttpPost]
    [ActionName(nameof(Delete))]
    public async Task<IActionResult> DeleteConfirmed(Guid id, string? rowVersion, CancellationToken cancellationToken)
    {
        Result result = TryDecodeRowVersion(rowVersion, out var expectedRowVersion)
            ? await aircraftService.DeleteAsync(id, expectedRowVersion, cancellationToken)
            : Result.Failure(AircraftErrors.ConcurrencyConflict, AircraftErrors.ConcurrencyConflictMessage);

        if (result.ErrorCode == AircraftErrors.NotFound)
        {
            return NotFound();
        }

        if (result.IsFailure)
        {
            TempData[ErrorMessageKey] = result.ErrorMessage;
            if (!IsModalRequest)
            {
                return RedirectToAction(nameof(Delete), new { id });
            }

            // Re-render the confirmation with the current values and row version.
            var current = await aircraftService.GetByIdAsync(id, cancellationToken);
            return current.IsSuccess
                ? FormView(nameof(Delete), AircraftDetailsViewModel.From(current.Value!), StatusCodes.Status409Conflict)
                : NotFound();
        }

        // A deleted aircraft's details page no longer exists, so always return to the list.
        return Saved("Aircraft deleted.", Url.Action(nameof(Index))!);
    }

    /// <summary>Renders the form as modal content (partial) or as a full page.</summary>
    private IActionResult FormView(string action, object model, int statusCode = StatusCodes.Status200OK)
    {
        Response.StatusCode = statusCode;
        return IsModalRequest ? PartialView($"_{action}Form", model) : View(action, model);
    }

    /// <summary>After a successful save: a redirect for full pages, or its URL as JSON for the modal script.</summary>
    private IActionResult Saved(string message, string redirectUrl)
    {
        TempData[StatusMessageKey] = message;
        return IsModalRequest ? Json(new { redirectUrl }) : Redirect(redirectUrl);
    }

    /// <summary>The page the modal was opened from, accepted only when it is local to this site.</summary>
    private string? ModalReturnUrl
    {
        get
        {
            if (!IsModalRequest)
            {
                return null;
            }

            var returnUrl = Request.Headers[ModalReturnUrlHeader].ToString();
            return Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        }
    }

    private static int StatusFor(string errorCode) => errorCode switch
    {
        AircraftErrors.DuplicateRegistration or AircraftErrors.DuplicateSerialNumber
            or AircraftErrors.ConcurrencyConflict => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status422UnprocessableEntity
    };

    private void AddError(string errorCode, string errorMessage)
    {
        var key = errorCode switch
        {
            AircraftErrors.DuplicateRegistration => nameof(AircraftFormViewModel.RegistrationNumber),
            AircraftErrors.DuplicateSerialNumber => nameof(AircraftFormViewModel.SerialNumber),
            _ => string.Empty
        };

        ModelState.AddModelError(key, errorMessage);
    }

    private static bool TryDecodeRowVersion(string? value, out byte[] rowVersion)
    {
        rowVersion = [];
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var buffer = new byte[value.Length];
        if (!Convert.TryFromBase64String(value, buffer, out var written) || written == 0)
        {
            return false;
        }

        rowVersion = buffer[..written];
        return true;
    }
}
