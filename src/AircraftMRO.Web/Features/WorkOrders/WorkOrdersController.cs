using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.WorkOrders;
using AircraftMRO.Application.Features.WorkOrders.DTOs;
using AircraftMRO.Application.Features.WorkOrders.Interfaces;
using AircraftMRO.Domain.Common.Results;
using AircraftMRO.Domain.Enums.WorkOrders;
using AircraftMRO.Web.Controllers;
using AircraftMRO.Web.Features.WorkOrders.Models;
using Microsoft.AspNetCore.Mvc;

namespace AircraftMRO.Web.Features.WorkOrders;

public sealed class WorkOrdersController(IWorkOrderService workOrderService) : ModalFormController
{
    [HttpGet]
    public async Task<IActionResult> Index(
        string? search = null,
        WorkOrderStatus? status = null,
        WorkOrderPriority? priority = null,
        Guid? aircraftId = null,
        bool open = false,
        bool overdue = false,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        var filter = new WorkOrderListFilter(search, status, priority, aircraftId, open, overdue);
        var list = await workOrderService.ListAsync(filter, new PagedRequest(page), cancellationToken);
        var statistics = await workOrderService.GetStatisticsAsync(filter.AircraftId, cancellationToken);
        var aircraft = await workOrderService.ListAircraftOptionsAsync(cancellationToken);

        return View(new WorkOrderListViewModel(list.Value!, statistics.Value!, filter, aircraft.Value!));
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var result = await workOrderService.GetByIdAsync(id, cancellationToken);
        return result.IsSuccess ? View(WorkOrderDetailsViewModel.From(result.Value!)) : NotFound();
    }

    [HttpGet]
    public async Task<IActionResult> Create(Guid? aircraftId, CancellationToken cancellationToken)
    {
        var form = new WorkOrderFormViewModel { AircraftId = aircraftId };
        await LoadAircraftOptionsAsync(form, cancellationToken);
        return FormView(nameof(Create), form);
    }

    [HttpPost]
    public async Task<IActionResult> Create(WorkOrderFormViewModel form, CancellationToken cancellationToken)
    {
        if (form.AircraftId is null || form.AircraftId == Guid.Empty)
        {
            ModelState.AddModelError(nameof(WorkOrderFormViewModel.AircraftId), "Select an aircraft.");
        }

        if (!ModelState.IsValid)
        {
            await LoadAircraftOptionsAsync(form, cancellationToken);
            return FormView(nameof(Create), form, StatusCodes.Status422UnprocessableEntity);
        }

        var result = await workOrderService.CreateAsync(
            new CreateWorkOrderDto(form.AircraftId!.Value, form.Title, form.Description, form.Priority!.Value, form.DueDate),
            cancellationToken);

        if (result.IsFailure)
        {
            AddError(result.ErrorCode!, result.ErrorMessage!);
            await LoadAircraftOptionsAsync(form, cancellationToken);
            return FormView(nameof(Create), form, StatusFor(result.ErrorCode!));
        }

        return Saved("Work order created.", ModalReturnUrl ?? Url.Action(nameof(Details), new { id = result.Value })!);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var result = await workOrderService.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure)
        {
            return NotFound();
        }

        var workOrder = result.Value!;
        return FormView(nameof(Edit), new WorkOrderFormViewModel
        {
            AircraftId = workOrder.AircraftId,
            Title = workOrder.Title,
            Description = workOrder.Description,
            Priority = workOrder.Priority,
            Status = workOrder.Status,
            DueDate = workOrder.DueDate,
            RowVersion = Convert.ToBase64String(workOrder.RowVersion),
            Number = workOrder.Number,
            AircraftRegistration = workOrder.AircraftRegistration
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(Guid id, WorkOrderFormViewModel form, CancellationToken cancellationToken)
    {
        if (!TryDecodeRowVersion(form.RowVersion, out var rowVersion))
        {
            ModelState.AddModelError(string.Empty, WorkOrderErrors.ConcurrencyConflictMessage);
        }

        if (!ModelState.IsValid)
        {
            return await EditFormViewAsync(id, form, StatusCodes.Status422UnprocessableEntity, cancellationToken);
        }

        var result = await workOrderService.UpdateAsync(
            id,
            new UpdateWorkOrderDto(
                form.Title, form.Description, form.Priority!.Value, form.Status!.Value, form.DueDate, rowVersion),
            cancellationToken);

        if (result.ErrorCode == WorkOrderErrors.NotFound)
        {
            return NotFound();
        }

        if (result.IsFailure)
        {
            AddError(result.ErrorCode!, result.ErrorMessage!);
            return await EditFormViewAsync(id, form, StatusFor(result.ErrorCode!), cancellationToken);
        }

        return Saved("Work order updated.", ModalReturnUrl ?? Url.Action(nameof(Details), new { id })!);
    }

    [HttpGet]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await workOrderService.GetByIdAsync(id, cancellationToken);
        return result.IsSuccess ? FormView(nameof(Delete), WorkOrderDetailsViewModel.From(result.Value!)) : NotFound();
    }

    [HttpPost]
    [ActionName(nameof(Delete))]
    public async Task<IActionResult> DeleteConfirmed(Guid id, string? rowVersion, CancellationToken cancellationToken)
    {
        Result result = TryDecodeRowVersion(rowVersion, out var expectedRowVersion)
            ? await workOrderService.DeleteAsync(id, expectedRowVersion, cancellationToken)
            : Result.Failure(WorkOrderErrors.ConcurrencyConflict, WorkOrderErrors.ConcurrencyConflictMessage);

        if (result.ErrorCode == WorkOrderErrors.NotFound)
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
            var current = await workOrderService.GetByIdAsync(id, cancellationToken);
            return current.IsSuccess
                ? FormView(nameof(Delete), WorkOrderDetailsViewModel.From(current.Value!), StatusCodes.Status409Conflict)
                : NotFound();
        }

        // A deleted work order's details page no longer exists, so always return to the list.
        return Saved("Work order deleted.", Url.Action(nameof(Index))!);
    }

    private async Task LoadAircraftOptionsAsync(WorkOrderFormViewModel form, CancellationToken cancellationToken) =>
        form.AircraftOptions = (await workOrderService.ListAircraftOptionsAsync(cancellationToken)).Value!;

    /// <summary>Re-renders the edit form, reloading the values that are displayed but never posted.</summary>
    private async Task<IActionResult> EditFormViewAsync(
        Guid id,
        WorkOrderFormViewModel form,
        int statusCode,
        CancellationToken cancellationToken)
    {
        var current = await workOrderService.GetByIdAsync(id, cancellationToken);
        if (current.IsFailure)
        {
            return NotFound();
        }

        form.Number = current.Value!.Number;
        form.AircraftRegistration = current.Value.AircraftRegistration;
        return FormView(nameof(Edit), form, statusCode);
    }

    private static int StatusFor(string errorCode) => errorCode switch
    {
        WorkOrderErrors.ConcurrencyConflict or WorkOrderErrors.Closed
            or WorkOrderErrors.AircraftRetired or WorkOrderErrors.OpenCannotBeDeleted
            or WorkOrderErrors.AircraftChanged => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status422UnprocessableEntity
    };

    private void AddError(string errorCode, string errorMessage)
    {
        var key = errorCode switch
        {
            WorkOrderErrors.AircraftNotFound or WorkOrderErrors.AircraftRetired => nameof(WorkOrderFormViewModel.AircraftId),
            _ => string.Empty
        };

        ModelState.AddModelError(key, errorMessage);
    }
}
