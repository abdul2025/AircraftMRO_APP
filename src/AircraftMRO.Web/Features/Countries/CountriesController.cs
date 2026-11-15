using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.Countries;
using AircraftMRO.Application.Features.Countries.DTOs;
using AircraftMRO.Application.Features.Countries.Interfaces;
using AircraftMRO.Domain.Common.Results;
using AircraftMRO.Web.Controllers;
using AircraftMRO.Web.Features.Countries.Models;
using Microsoft.AspNetCore.Mvc;

namespace AircraftMRO.Web.Features.Countries;

public sealed class CountriesController(ICountryService countryService) : ModalFormController
{
    [HttpGet]
    public async Task<IActionResult> Index(
        string? search = null,
        bool? isActive = null,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        var filter = new CountryListFilter(search, isActive);
        var list = await countryService.ListAsync(filter, new PagedRequest(page), cancellationToken);

        return View(new CountryListViewModel(list.Value!, filter));
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var result = await countryService.GetByIdAsync(id, cancellationToken);
        return result.IsSuccess ? View(CountryDetailsViewModel.From(result.Value!)) : NotFound();
    }

    [HttpGet]
    public IActionResult Create() => FormView(nameof(Create), new CountryFormViewModel());

    [HttpPost]
    public async Task<IActionResult> Create(CountryFormViewModel form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return FormView(nameof(Create), form, StatusCodes.Status422UnprocessableEntity);
        }

        var result = await countryService.CreateAsync(
            new CreateCountryDto(form.Name, form.Code, form.IsActive),
            cancellationToken);

        if (result.IsFailure)
        {
            AddError(result.ErrorCode!, result.ErrorMessage!);
            return FormView(nameof(Create), form, StatusFor(result.ErrorCode!));
        }

        return Saved("Country created.", ModalReturnUrl ?? Url.Action(nameof(Details), new { id = result.Value })!);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var result = await countryService.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure)
        {
            return NotFound();
        }

        var country = result.Value!;
        return FormView(nameof(Edit), new CountryFormViewModel
        {
            Name = country.Name,
            Code = country.Code,
            IsActive = country.IsActive,
            RowVersion = Convert.ToBase64String(country.RowVersion)
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(Guid id, CountryFormViewModel form, CancellationToken cancellationToken)
    {
        if (!TryDecodeRowVersion(form.RowVersion, out var rowVersion))
        {
            ModelState.AddModelError(string.Empty, CountryErrors.ConcurrencyConflictMessage);
        }

        if (!ModelState.IsValid)
        {
            return FormView(nameof(Edit), form, StatusCodes.Status422UnprocessableEntity);
        }

        var result = await countryService.UpdateAsync(
            id,
            new UpdateCountryDto(form.Name, form.Code, form.IsActive, rowVersion),
            cancellationToken);

        if (result.ErrorCode == CountryErrors.NotFound)
        {
            return NotFound();
        }

        if (result.IsFailure)
        {
            AddError(result.ErrorCode!, result.ErrorMessage!);
            return FormView(nameof(Edit), form, StatusFor(result.ErrorCode!));
        }

        return Saved("Country updated.", ModalReturnUrl ?? Url.Action(nameof(Details), new { id })!);
    }

    [HttpGet]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await countryService.GetByIdAsync(id, cancellationToken);
        return result.IsSuccess ? FormView(nameof(Delete), CountryDetailsViewModel.From(result.Value!)) : NotFound();
    }

    [HttpPost]
    [ActionName(nameof(Delete))]
    public async Task<IActionResult> DeleteConfirmed(Guid id, string? rowVersion, CancellationToken cancellationToken)
    {
        Result result = TryDecodeRowVersion(rowVersion, out var expectedRowVersion)
            ? await countryService.DeleteAsync(id, expectedRowVersion, cancellationToken)
            : Result.Failure(CountryErrors.ConcurrencyConflict, CountryErrors.ConcurrencyConflictMessage);

        if (result.ErrorCode == CountryErrors.NotFound)
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
            var current = await countryService.GetByIdAsync(id, cancellationToken);
            return current.IsSuccess
                ? FormView(nameof(Delete), CountryDetailsViewModel.From(current.Value!), StatusCodes.Status409Conflict)
                : NotFound();
        }

        // A deleted country's details page no longer exists, so always return to the list.
        return Saved("Country deleted.", Url.Action(nameof(Index))!);
    }

    private static int StatusFor(string errorCode) => errorCode switch
    {
        CountryErrors.DuplicateCode or CountryErrors.ConcurrencyConflict => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status422UnprocessableEntity
    };

    private void AddError(string errorCode, string errorMessage)
    {
        var key = errorCode == CountryErrors.DuplicateCode ? nameof(CountryFormViewModel.Code) : string.Empty;
        ModelState.AddModelError(key, errorMessage);
    }
}
