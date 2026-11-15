using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.AircraftModels;
using AircraftMRO.Application.Features.AircraftModels.DTOs;
using AircraftMRO.Application.Features.AircraftModels.Interfaces;
using AircraftMRO.Domain.Common.Results;
using AircraftMRO.Web.Controllers;
using AircraftMRO.Web.Features.AircraftModels.Models;
using Microsoft.AspNetCore.Mvc;

namespace AircraftMRO.Web.Features.AircraftModels;

public sealed class AircraftModelsController(IAircraftModelService modelService) : ModalFormController
{
    [HttpGet]
    public async Task<IActionResult> Index(
        string? search = null,
        Guid? manufacturerId = null,
        bool? isActive = null,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        var filter = new AircraftModelListFilter(search, manufacturerId, isActive);
        var list = await modelService.ListAsync(filter, new PagedRequest(page), cancellationToken);
        var manufacturers = await modelService.ListManufacturerOptionsAsync(cancellationToken);

        return View(new AircraftModelListViewModel(list.Value!, filter, manufacturers.Value!));
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var result = await modelService.GetByIdAsync(id, cancellationToken);
        return result.IsSuccess ? View(AircraftModelDetailsViewModel.From(result.Value!)) : NotFound();
    }

    [HttpGet]
    public async Task<IActionResult> Create(Guid? manufacturerId, CancellationToken cancellationToken)
    {
        var form = new AircraftModelFormViewModel { ManufacturerId = manufacturerId };
        await LoadManufacturerOptionsAsync(form, cancellationToken);
        return FormView(nameof(Create), form);
    }

    [HttpPost]
    public async Task<IActionResult> Create(AircraftModelFormViewModel form, CancellationToken cancellationToken)
    {
        if (form.ManufacturerId is null)
        {
            ModelState.AddModelError(nameof(AircraftModelFormViewModel.ManufacturerId), "Manufacturer is required.");
        }

        if (!ModelState.IsValid)
        {
            await LoadManufacturerOptionsAsync(form, cancellationToken);
            return FormView(nameof(Create), form, StatusCodes.Status422UnprocessableEntity);
        }

        var result = await modelService.CreateAsync(
            new CreateAircraftModelDto(
                form.ManufacturerId!.Value,
                form.Name,
                form.ModelCode,
                form.AircraftCategory!.Value,
                form.IsActive),
            cancellationToken);

        if (result.IsFailure)
        {
            AddError(result.ErrorCode!, result.ErrorMessage!);
            await LoadManufacturerOptionsAsync(form, cancellationToken);
            return FormView(nameof(Create), form, StatusFor(result.ErrorCode!));
        }

        return Saved("Aircraft model created.", ModalReturnUrl ?? Url.Action(nameof(Details), new { id = result.Value })!);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var result = await modelService.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure)
        {
            return NotFound();
        }

        var model = result.Value!;
        return FormView(nameof(Edit), new AircraftModelFormViewModel
        {
            ManufacturerId = model.ManufacturerId,
            ManufacturerName = model.ManufacturerName,
            Name = model.Name,
            ModelCode = model.ModelCode,
            AircraftCategory = model.AircraftCategory,
            IsActive = model.IsActive,
            RowVersion = Convert.ToBase64String(model.RowVersion)
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(Guid id, AircraftModelFormViewModel form, CancellationToken cancellationToken)
    {
        // The manufacturer cannot change on edit; whatever was posted for it is ignored.
        var current = await modelService.GetByIdAsync(id, cancellationToken);
        if (current.IsFailure)
        {
            return NotFound();
        }

        form.ManufacturerId = current.Value!.ManufacturerId;
        form.ManufacturerName = current.Value.ManufacturerName;

        if (!TryDecodeRowVersion(form.RowVersion, out var rowVersion))
        {
            ModelState.AddModelError(string.Empty, AircraftModelErrors.ConcurrencyConflictMessage);
        }

        if (!ModelState.IsValid)
        {
            return FormView(nameof(Edit), form, StatusCodes.Status422UnprocessableEntity);
        }

        var result = await modelService.UpdateAsync(
            id,
            new UpdateAircraftModelDto(form.Name, form.ModelCode, form.AircraftCategory!.Value, form.IsActive, rowVersion),
            cancellationToken);

        if (result.ErrorCode == AircraftModelErrors.NotFound)
        {
            return NotFound();
        }

        if (result.IsFailure)
        {
            AddError(result.ErrorCode!, result.ErrorMessage!);
            return FormView(nameof(Edit), form, StatusFor(result.ErrorCode!));
        }

        return Saved("Aircraft model updated.", ModalReturnUrl ?? Url.Action(nameof(Details), new { id })!);
    }

    [HttpGet]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await modelService.GetByIdAsync(id, cancellationToken);
        return result.IsSuccess ? FormView(nameof(Delete), AircraftModelDetailsViewModel.From(result.Value!)) : NotFound();
    }

    [HttpPost]
    [ActionName(nameof(Delete))]
    public async Task<IActionResult> DeleteConfirmed(Guid id, string? rowVersion, CancellationToken cancellationToken)
    {
        Result result = TryDecodeRowVersion(rowVersion, out var expectedRowVersion)
            ? await modelService.DeleteAsync(id, expectedRowVersion, cancellationToken)
            : Result.Failure(AircraftModelErrors.ConcurrencyConflict, AircraftModelErrors.ConcurrencyConflictMessage);

        if (result.ErrorCode == AircraftModelErrors.NotFound)
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
            var current = await modelService.GetByIdAsync(id, cancellationToken);
            return current.IsSuccess
                ? FormView(nameof(Delete), AircraftModelDetailsViewModel.From(current.Value!), StatusCodes.Status409Conflict)
                : NotFound();
        }

        // A deleted model's details page no longer exists, so always return to the list.
        return Saved("Aircraft model deleted.", Url.Action(nameof(Index))!);
    }

    private static int StatusFor(string errorCode) => errorCode switch
    {
        AircraftModelErrors.DuplicateModelCode or AircraftModelErrors.ConcurrencyConflict => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status422UnprocessableEntity
    };

    private void AddError(string errorCode, string errorMessage)
    {
        var key = errorCode switch
        {
            AircraftModelErrors.DuplicateModelCode => nameof(AircraftModelFormViewModel.ModelCode),
            AircraftModelErrors.ManufacturerNotFound or AircraftModelErrors.ManufacturerInactive
                => nameof(AircraftModelFormViewModel.ManufacturerId),
            _ => string.Empty
        };

        ModelState.AddModelError(key, errorMessage);
    }

    private async Task LoadManufacturerOptionsAsync(AircraftModelFormViewModel form, CancellationToken cancellationToken) =>
        form.ManufacturerOptions = (await modelService.ListManufacturerOptionsAsync(cancellationToken)).Value!;
}
