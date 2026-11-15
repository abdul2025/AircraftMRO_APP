using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.AircraftManufacturers;
using AircraftMRO.Application.Features.AircraftManufacturers.DTOs;
using AircraftMRO.Application.Features.AircraftManufacturers.Interfaces;
using AircraftMRO.Domain.Common.Results;
using AircraftMRO.Web.Controllers;
using AircraftMRO.Web.Features.AircraftManufacturers.Models;
using Microsoft.AspNetCore.Mvc;

namespace AircraftMRO.Web.Features.AircraftManufacturers;

public sealed class AircraftManufacturersController(IAircraftManufacturerService manufacturerService) : ModalFormController
{
    [HttpGet]
    public async Task<IActionResult> Index(
        string? search = null,
        bool? isActive = null,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        var filter = new AircraftManufacturerListFilter(search, isActive);
        var list = await manufacturerService.ListAsync(filter, new PagedRequest(page), cancellationToken);

        return View(new AircraftManufacturerListViewModel(list.Value!, filter));
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var result = await manufacturerService.GetByIdAsync(id, cancellationToken);
        return result.IsSuccess ? View(AircraftManufacturerDetailsViewModel.From(result.Value!)) : NotFound();
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var form = new AircraftManufacturerFormViewModel();
        await LoadCountryOptionsAsync(form, cancellationToken);
        return FormView(nameof(Create), form);
    }

    [HttpPost]
    public async Task<IActionResult> Create(AircraftManufacturerFormViewModel form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadCountryOptionsAsync(form, cancellationToken);
            return FormView(nameof(Create), form, StatusCodes.Status422UnprocessableEntity);
        }

        var result = await manufacturerService.CreateAsync(
            new CreateAircraftManufacturerDto(form.Name, form.Code, form.CountryId!.Value, form.IsActive),
            cancellationToken);

        if (result.IsFailure)
        {
            AddError(result.ErrorCode!, result.ErrorMessage!);
            await LoadCountryOptionsAsync(form, cancellationToken);
            return FormView(nameof(Create), form, StatusFor(result.ErrorCode!));
        }

        return Saved("Manufacturer created.", ModalReturnUrl ?? Url.Action(nameof(Details), new { id = result.Value })!);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var result = await manufacturerService.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure)
        {
            return NotFound();
        }

        var manufacturer = result.Value!;
        var form = new AircraftManufacturerFormViewModel
        {
            Name = manufacturer.Name,
            Code = manufacturer.Code,
            CountryId = manufacturer.CountryId,
            IsActive = manufacturer.IsActive,
            RowVersion = Convert.ToBase64String(manufacturer.RowVersion)
        };
        await LoadCountryOptionsAsync(form, cancellationToken);
        return FormView(nameof(Edit), form);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(Guid id, AircraftManufacturerFormViewModel form, CancellationToken cancellationToken)
    {
        if (!TryDecodeRowVersion(form.RowVersion, out var rowVersion))
        {
            ModelState.AddModelError(string.Empty, AircraftManufacturerErrors.ConcurrencyConflictMessage);
        }

        if (!ModelState.IsValid)
        {
            await LoadCountryOptionsAsync(form, cancellationToken);
            return FormView(nameof(Edit), form, StatusCodes.Status422UnprocessableEntity);
        }

        var result = await manufacturerService.UpdateAsync(
            id,
            new UpdateAircraftManufacturerDto(form.Name, form.Code, form.CountryId!.Value, form.IsActive, rowVersion),
            cancellationToken);

        if (result.ErrorCode == AircraftManufacturerErrors.NotFound)
        {
            return NotFound();
        }

        if (result.IsFailure)
        {
            AddError(result.ErrorCode!, result.ErrorMessage!);
            await LoadCountryOptionsAsync(form, cancellationToken);
            return FormView(nameof(Edit), form, StatusFor(result.ErrorCode!));
        }

        return Saved("Manufacturer updated.", ModalReturnUrl ?? Url.Action(nameof(Details), new { id })!);
    }

    [HttpGet]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await manufacturerService.GetByIdAsync(id, cancellationToken);
        return result.IsSuccess
            ? FormView(nameof(Delete), AircraftManufacturerDetailsViewModel.From(result.Value!))
            : NotFound();
    }

    [HttpPost]
    [ActionName(nameof(Delete))]
    public async Task<IActionResult> DeleteConfirmed(Guid id, string? rowVersion, CancellationToken cancellationToken)
    {
        Result result = TryDecodeRowVersion(rowVersion, out var expectedRowVersion)
            ? await manufacturerService.DeleteAsync(id, expectedRowVersion, cancellationToken)
            : Result.Failure(AircraftManufacturerErrors.ConcurrencyConflict, AircraftManufacturerErrors.ConcurrencyConflictMessage);

        if (result.ErrorCode == AircraftManufacturerErrors.NotFound)
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
            var current = await manufacturerService.GetByIdAsync(id, cancellationToken);
            return current.IsSuccess
                ? FormView(nameof(Delete), AircraftManufacturerDetailsViewModel.From(current.Value!), StatusCodes.Status409Conflict)
                : NotFound();
        }

        // A deleted manufacturer's details page no longer exists, so always return to the list.
        return Saved("Manufacturer deleted.", Url.Action(nameof(Index))!);
    }

    private static int StatusFor(string errorCode) => errorCode switch
    {
        AircraftManufacturerErrors.DuplicateCode or AircraftManufacturerErrors.ConcurrencyConflict
            => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status422UnprocessableEntity
    };

    private void AddError(string errorCode, string errorMessage)
    {
        var key = errorCode switch
        {
            AircraftManufacturerErrors.DuplicateCode => nameof(AircraftManufacturerFormViewModel.Code),
            AircraftManufacturerErrors.CountryNotFound or AircraftManufacturerErrors.CountryInactive
                => nameof(AircraftManufacturerFormViewModel.CountryId),
            _ => string.Empty
        };

        ModelState.AddModelError(key, errorMessage);
    }

    /// <summary>The selected country stays listed even when inactive, so an edit form can still show it.</summary>
    private async Task LoadCountryOptionsAsync(AircraftManufacturerFormViewModel form, CancellationToken cancellationToken) =>
        form.CountryOptions = (await manufacturerService.ListCountryOptionsAsync(form.CountryId, cancellationToken)).Value!;
}
