using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.MaintenanceOrders;
using AircraftMRO.Application.Features.MaintenanceOrders.DTOs;
using AircraftMRO.Application.Features.MaintenanceOrders.Interfaces;
using AircraftMRO.Domain.Common.Results;
using AircraftMRO.Domain.Enums.MaintenanceOrders;
using AircraftMRO.Web.Controllers;
using AircraftMRO.Web.Features.MaintenanceOrders.Models;
using Microsoft.AspNetCore.Mvc;

namespace AircraftMRO.Web.Features.MaintenanceOrders;

public sealed class MaintenanceOrdersController(IMaintenanceOrderService maintenanceOrderService) : ModalFormController
{
    [HttpGet]
    public async Task<IActionResult> Index(
        Guid? workOrderId = null,
        MaintenanceStatus? status = null,
        MaintenanceType? type = null,
        bool open = false,
        bool overdue = false,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        var filter = new MaintenanceOrderListFilter(workOrderId, status, type, open, overdue);
        var list = await maintenanceOrderService.ListAsync(filter, new PagedRequest(page), cancellationToken);
        var statistics = await maintenanceOrderService.GetStatisticsAsync(filter.WorkOrderId, cancellationToken);
        var workOrders = await maintenanceOrderService.ListWorkOrderOptionsAsync(cancellationToken);
        var selected = filter.WorkOrderId is { } selectedId
            ? (await maintenanceOrderService.GetWorkOrderOptionAsync(selectedId, cancellationToken)).Value
            : null;

        return View(new MaintenanceOrderListViewModel(list.Value!, statistics.Value!, filter, workOrders.Value!, selected));
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var result = await maintenanceOrderService.GetByIdAsync(id, cancellationToken);
        return result.IsSuccess ? View(MaintenanceOrderDetailsViewModel.From(result.Value!)) : NotFound();
    }

    [HttpGet]
    public async Task<IActionResult> Create(Guid? workOrderId, CancellationToken cancellationToken)
    {
        var form = new MaintenanceOrderFormViewModel { WorkOrderId = workOrderId };
        await LoadWorkOrderOptionsAsync(form, cancellationToken);
        return FormView(nameof(Create), form);
    }

    [HttpPost]
    public async Task<IActionResult> Create(MaintenanceOrderFormViewModel form, CancellationToken cancellationToken)
    {
        if (form.WorkOrderId is null || form.WorkOrderId == Guid.Empty)
        {
            ModelState.AddModelError(nameof(MaintenanceOrderFormViewModel.WorkOrderId), "Select a work order.");
        }

        if (!ModelState.IsValid)
        {
            await LoadWorkOrderOptionsAsync(form, cancellationToken);
            return FormView(nameof(Create), form, StatusCodes.Status422UnprocessableEntity);
        }

        var result = await maintenanceOrderService.CreateAsync(
            new CreateMaintenanceOrderDto(form.WorkOrderId!.Value, form.Type!.Value, form.ScheduledDate!.Value, form.Notes),
            cancellationToken);

        if (result.IsFailure)
        {
            AddError(result.ErrorCode!, result.ErrorMessage!);
            await LoadWorkOrderOptionsAsync(form, cancellationToken);
            return FormView(nameof(Create), form, StatusFor(result.ErrorCode!));
        }

        return Saved("Maintenance scheduled.", ModalReturnUrl ?? Url.Action(nameof(Details), new { id = result.Value })!);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var result = await maintenanceOrderService.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure)
        {
            return NotFound();
        }

        var maintenanceOrder = result.Value!;
        return FormView(nameof(Edit), new MaintenanceOrderFormViewModel
        {
            WorkOrderId = maintenanceOrder.WorkOrderId,
            Type = maintenanceOrder.Type,
            ScheduledDate = maintenanceOrder.ScheduledDate,
            Status = maintenanceOrder.Status,
            Notes = maintenanceOrder.Notes,
            RowVersion = Convert.ToBase64String(maintenanceOrder.RowVersion),
            IsEdit = true,
            WorkOrderNumber = maintenanceOrder.WorkOrderNumber
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(Guid id, MaintenanceOrderFormViewModel form, CancellationToken cancellationToken)
    {
        if (!TryDecodeRowVersion(form.RowVersion, out var rowVersion))
        {
            ModelState.AddModelError(string.Empty, MaintenanceOrderErrors.ConcurrencyConflictMessage);
        }

        if (!ModelState.IsValid)
        {
            return await EditFormViewAsync(id, form, StatusCodes.Status422UnprocessableEntity, cancellationToken);
        }

        var result = await maintenanceOrderService.UpdateAsync(
            id,
            new UpdateMaintenanceOrderDto(form.Type!.Value, form.ScheduledDate!.Value, form.Status!.Value, form.Notes, rowVersion),
            cancellationToken);

        if (result.ErrorCode == MaintenanceOrderErrors.NotFound)
        {
            return NotFound();
        }

        if (result.IsFailure)
        {
            AddError(result.ErrorCode!, result.ErrorMessage!);
            return await EditFormViewAsync(id, form, StatusFor(result.ErrorCode!), cancellationToken);
        }

        return Saved("Maintenance updated.", ModalReturnUrl ?? Url.Action(nameof(Details), new { id })!);
    }

    [HttpGet]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await maintenanceOrderService.GetByIdAsync(id, cancellationToken);
        return result.IsSuccess ? FormView(nameof(Delete), MaintenanceOrderDetailsViewModel.From(result.Value!)) : NotFound();
    }

    [HttpPost]
    [ActionName(nameof(Delete))]
    public async Task<IActionResult> DeleteConfirmed(Guid id, string? rowVersion, CancellationToken cancellationToken)
    {
        Result result = TryDecodeRowVersion(rowVersion, out var expectedRowVersion)
            ? await maintenanceOrderService.DeleteAsync(id, expectedRowVersion, cancellationToken)
            : Result.Failure(MaintenanceOrderErrors.ConcurrencyConflict, MaintenanceOrderErrors.ConcurrencyConflictMessage);

        if (result.ErrorCode == MaintenanceOrderErrors.NotFound)
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
            var current = await maintenanceOrderService.GetByIdAsync(id, cancellationToken);
            return current.IsSuccess
                ? FormView(nameof(Delete), MaintenanceOrderDetailsViewModel.From(current.Value!), StatusCodes.Status409Conflict)
                : NotFound();
        }

        // A deleted maintenance order's details page no longer exists, so always return to the list.
        return Saved("Maintenance deleted.", Url.Action(nameof(Index))!);
    }

    private async Task LoadWorkOrderOptionsAsync(MaintenanceOrderFormViewModel form, CancellationToken cancellationToken) =>
        form.WorkOrderOptions = (await maintenanceOrderService.ListWorkOrderOptionsAsync(cancellationToken)).Value!;

    /// <summary>Re-renders the edit form, reloading the values that are displayed but never posted.</summary>
    private async Task<IActionResult> EditFormViewAsync(
        Guid id,
        MaintenanceOrderFormViewModel form,
        int statusCode,
        CancellationToken cancellationToken)
    {
        var current = await maintenanceOrderService.GetByIdAsync(id, cancellationToken);
        if (current.IsFailure)
        {
            return NotFound();
        }

        form.IsEdit = true;
        form.WorkOrderNumber = current.Value!.WorkOrderNumber;
        return FormView(nameof(Edit), form, statusCode);
    }

    private static int StatusFor(string errorCode) => errorCode switch
    {
        MaintenanceOrderErrors.ConcurrencyConflict or MaintenanceOrderErrors.Closed
            or MaintenanceOrderErrors.WorkOrderClosed or MaintenanceOrderErrors.OnlyScheduledCanBeDeleted
            or MaintenanceOrderErrors.WorkOrderChanged => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status422UnprocessableEntity
    };

    private void AddError(string errorCode, string errorMessage)
    {
        var key = errorCode switch
        {
            MaintenanceOrderErrors.WorkOrderNotFound or MaintenanceOrderErrors.WorkOrderClosed =>
                nameof(MaintenanceOrderFormViewModel.WorkOrderId),
            _ => string.Empty
        };

        ModelState.AddModelError(key, errorMessage);
    }
}
