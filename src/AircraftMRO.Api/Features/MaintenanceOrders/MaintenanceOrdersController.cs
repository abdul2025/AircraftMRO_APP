using AircraftMRO.Api.Features.MaintenanceOrders.Contracts;
using AircraftMRO.Api.OpenApi;
using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.MaintenanceOrders;
using AircraftMRO.Application.Features.MaintenanceOrders.DTOs;
using AircraftMRO.Application.Features.MaintenanceOrders.Interfaces;
using AircraftMRO.Domain.Enums.MaintenanceOrders;
using Microsoft.AspNetCore.Mvc;

namespace AircraftMRO.Api.Features.MaintenanceOrders;

/// <summary>
/// Maintenance order CRUD. Maintenance can only be scheduled or changed while its work order is
/// open, and the work order cannot be closed until all of its maintenance is completed. Only
/// scheduled maintenance can be deleted. Reads return an <c>ETag</c>; updates and deletes require
/// it in <c>If-Match</c>.
/// </summary>
[ApiController]
[Route("api/maintenance-orders")]
public sealed class MaintenanceOrdersController(IMaintenanceOrderService maintenanceOrderService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<MaintenanceOrderPageResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? workOrderId = null,
        [FromQuery] MaintenanceStatus? status = null,
        [FromQuery] MaintenanceType? type = null,
        [FromQuery] bool open = false,
        [FromQuery] bool overdue = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PagedRequest.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await maintenanceOrderService.ListAsync(
            new MaintenanceOrderListFilter(workOrderId, status, type, open, overdue),
            new PagedRequest(page, pageSize),
            cancellationToken);
        var paged = result.Value!;

        return Ok(new MaintenanceOrderPageResponse(
            paged.Items.Select(MaintenanceOrderSummaryResponse.From).ToList(),
            paged.Page,
            paged.PageSize,
            paged.TotalCount,
            paged.TotalPages));
    }

    [HttpGet("statistics")]
    [ProducesResponseType<MaintenanceOrderStatisticsResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Statistics([FromQuery] Guid? workOrderId, CancellationToken cancellationToken)
    {
        var result = await maintenanceOrderService.GetStatisticsAsync(workOrderId, cancellationToken);
        return Ok(MaintenanceOrderStatisticsResponse.From(result.Value!));
    }

    [HttpGet("{id:guid}")]
    [ReturnsETag(StatusCodes.Status200OK)]
    [ProducesResponseType<MaintenanceOrderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await maintenanceOrderService.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure)
        {
            return Problem(result.ErrorCode!, result.ErrorMessage!);
        }

        ConcurrencyHeaders.SetETag(Response, result.Value!.RowVersion);
        return Ok(MaintenanceOrderResponse.From(result.Value!));
    }

    /// <summary>
    /// Schedules maintenance under an open work order. Simultaneous creates under the same work
    /// order collide: all but one get 409 <c>MaintenanceOrder.WorkOrderChanged</c>, which is safe to
    /// retry unchanged. Send bulk imports for one work order sequentially.
    /// </summary>
    [HttpPost]
    [ReturnsETag(StatusCodes.Status201Created)]
    [ProducesResponseType<MaintenanceOrderResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(CreateMaintenanceOrderRequest request, CancellationToken cancellationToken)
    {
        var created = await maintenanceOrderService.CreateAsync(
            new CreateMaintenanceOrderDto(
                request.WorkOrderId!.Value,
                request.Type!.Value,
                request.ScheduledDate!.Value,
                request.Notes),
            cancellationToken);
        if (created.IsFailure)
        {
            return Problem(created.ErrorCode!, created.ErrorMessage!);
        }

        var result = await maintenanceOrderService.GetByIdAsync(created.Value, cancellationToken);
        ConcurrencyHeaders.SetETag(Response, result.Value!.RowVersion);
        return CreatedAtAction(nameof(Get), new { id = created.Value }, MaintenanceOrderResponse.From(result.Value!));
    }

    [HttpPut("{id:guid}")]
    [RequiresIfMatch]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired)]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateMaintenanceOrderRequest request,
        CancellationToken cancellationToken)
    {
        if (!ConcurrencyHeaders.TryReadIfMatch(Request, out var rowVersion))
        {
            return PreconditionRequired();
        }

        var result = await maintenanceOrderService.UpdateAsync(
            id,
            new UpdateMaintenanceOrderDto(
                request.Type!.Value,
                request.ScheduledDate!.Value,
                request.Status!.Value,
                request.Notes,
                rowVersion),
            cancellationToken);

        return result.IsSuccess ? NoContent() : Problem(result.ErrorCode!, result.ErrorMessage!);
    }

    [HttpDelete("{id:guid}")]
    [RequiresIfMatch]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!ConcurrencyHeaders.TryReadIfMatch(Request, out var rowVersion))
        {
            return PreconditionRequired();
        }

        var result = await maintenanceOrderService.DeleteAsync(id, rowVersion, cancellationToken);
        return result.IsSuccess ? NoContent() : Problem(result.ErrorCode!, result.ErrorMessage!);
    }

    private ObjectResult Problem(string errorCode, string errorMessage)
    {
        var statusCode = errorCode switch
        {
            MaintenanceOrderErrors.NotFound => StatusCodes.Status404NotFound,
            MaintenanceOrderErrors.Validation => StatusCodes.Status400BadRequest,
            MaintenanceOrderErrors.WorkOrderNotFound => StatusCodes.Status422UnprocessableEntity,
            MaintenanceOrderErrors.Closed or MaintenanceOrderErrors.WorkOrderClosed
                or MaintenanceOrderErrors.OnlyScheduledCanBeDeleted
                or MaintenanceOrderErrors.WorkOrderChanged => StatusCodes.Status409Conflict,
            MaintenanceOrderErrors.ConcurrencyConflict => StatusCodes.Status412PreconditionFailed,
            _ => StatusCodes.Status400BadRequest
        };

        var problem = Problem(detail: errorMessage, statusCode: statusCode);
        ((ProblemDetails)problem.Value!).Extensions["code"] = errorCode;
        return problem;
    }

    private ObjectResult PreconditionRequired()
    {
        var problem = Problem(
            detail: "Send the maintenance order's current ETag in the If-Match header.",
            statusCode: StatusCodes.Status428PreconditionRequired);
        ((ProblemDetails)problem.Value!).Extensions["code"] = "MaintenanceOrder.PreconditionRequired";
        return problem;
    }
}
