using AircraftMRO.Api.Features.WorkOrders.Contracts;
using AircraftMRO.Api.OpenApi;
using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.WorkOrders;
using AircraftMRO.Application.Features.WorkOrders.DTOs;
using AircraftMRO.Application.Features.WorkOrders.Interfaces;
using AircraftMRO.Domain.Enums.WorkOrders;
using Microsoft.AspNetCore.Mvc;

namespace AircraftMRO.Api.Features.WorkOrders;

/// <summary>
/// Work order CRUD. Every save also sets the aircraft's status from its open work orders:
/// grounded if any is critical, in maintenance otherwise, active once none are open. Only closed
/// work orders can be deleted. Reads return an <c>ETag</c>; updates and deletes require it in <c>If-Match</c>.
/// </summary>
[ApiController]
[Route("api/work-orders")]
public sealed class WorkOrdersController(IWorkOrderService workOrderService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<WorkOrderPageResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? search = null,
        [FromQuery] WorkOrderStatus? status = null,
        [FromQuery] WorkOrderPriority? priority = null,
        [FromQuery] Guid? aircraftId = null,
        [FromQuery] bool open = false,
        [FromQuery] bool overdue = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PagedRequest.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await workOrderService.ListAsync(
            new WorkOrderListFilter(search, status, priority, aircraftId, open, overdue),
            new PagedRequest(page, pageSize),
            cancellationToken);
        var paged = result.Value!;

        return Ok(new WorkOrderPageResponse(
            paged.Items.Select(WorkOrderSummaryResponse.From).ToList(),
            paged.Page,
            paged.PageSize,
            paged.TotalCount,
            paged.TotalPages));
    }

    [HttpGet("statistics")]
    [ProducesResponseType<WorkOrderStatisticsResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Statistics([FromQuery] Guid? aircraftId, CancellationToken cancellationToken)
    {
        var result = await workOrderService.GetStatisticsAsync(aircraftId, cancellationToken);
        return Ok(WorkOrderStatisticsResponse.From(result.Value!));
    }

    [HttpGet("{id:guid}")]
    [ReturnsETag(StatusCodes.Status200OK)]
    [ProducesResponseType<WorkOrderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await workOrderService.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure)
        {
            return Problem(result.ErrorCode!, result.ErrorMessage!);
        }

        ConcurrencyHeaders.SetETag(Response, result.Value!.RowVersion);
        return Ok(WorkOrderResponse.From(result.Value!));
    }

    [HttpPost]
    [ReturnsETag(StatusCodes.Status201Created)]
    [ProducesResponseType<WorkOrderResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(CreateWorkOrderRequest request, CancellationToken cancellationToken)
    {
        var created = await workOrderService.CreateAsync(
            new CreateWorkOrderDto(
                request.AircraftId!.Value,
                request.Title,
                request.Description,
                request.Priority!.Value,
                request.DueDate),
            cancellationToken);
        if (created.IsFailure)
        {
            return Problem(created.ErrorCode!, created.ErrorMessage!);
        }

        var result = await workOrderService.GetByIdAsync(created.Value, cancellationToken);
        ConcurrencyHeaders.SetETag(Response, result.Value!.RowVersion);
        return CreatedAtAction(nameof(Get), new { id = created.Value }, WorkOrderResponse.From(result.Value!));
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
        UpdateWorkOrderRequest request,
        CancellationToken cancellationToken)
    {
        if (!ConcurrencyHeaders.TryReadIfMatch(Request, out var rowVersion))
        {
            return PreconditionRequired();
        }

        var result = await workOrderService.UpdateAsync(
            id,
            new UpdateWorkOrderDto(
                request.Title,
                request.Description,
                request.Priority!.Value,
                request.Status!.Value,
                request.DueDate,
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

        var result = await workOrderService.DeleteAsync(id, rowVersion, cancellationToken);
        return result.IsSuccess ? NoContent() : Problem(result.ErrorCode!, result.ErrorMessage!);
    }

    private ObjectResult Problem(string errorCode, string errorMessage)
    {
        var statusCode = errorCode switch
        {
            WorkOrderErrors.NotFound => StatusCodes.Status404NotFound,
            WorkOrderErrors.Validation => StatusCodes.Status400BadRequest,
            WorkOrderErrors.AircraftNotFound => StatusCodes.Status422UnprocessableEntity,
            WorkOrderErrors.AircraftRetired or WorkOrderErrors.Closed or WorkOrderErrors.OpenCannotBeDeleted
                or WorkOrderErrors.AircraftChanged => StatusCodes.Status409Conflict,
            WorkOrderErrors.ConcurrencyConflict => StatusCodes.Status412PreconditionFailed,
            _ => StatusCodes.Status400BadRequest
        };

        var problem = Problem(detail: errorMessage, statusCode: statusCode);
        ((ProblemDetails)problem.Value!).Extensions["code"] = errorCode;
        return problem;
    }

    private ObjectResult PreconditionRequired()
    {
        var problem = Problem(
            detail: "Send the work order's current ETag in the If-Match header.",
            statusCode: StatusCodes.Status428PreconditionRequired);
        ((ProblemDetails)problem.Value!).Extensions["code"] = "WorkOrder.PreconditionRequired";
        return problem;
    }
}
