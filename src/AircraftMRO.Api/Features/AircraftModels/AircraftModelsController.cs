using AircraftMRO.Api.Features.AircraftModels.Contracts;
using AircraftMRO.Api.OpenApi;
using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.AircraftModels;
using AircraftMRO.Application.Features.AircraftModels.DTOs;
using AircraftMRO.Application.Features.AircraftModels.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AircraftMRO.Api.Features.AircraftModels;

/// <summary>
/// Aircraft model reference data. Reads return an <c>ETag</c>; updates and deletes require it in <c>If-Match</c>.
/// </summary>
[ApiController]
[Route("api/aircraft-models")]
public sealed class AircraftModelsController(IAircraftModelService modelService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<AircraftModelPageResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? search = null,
        [FromQuery] Guid? manufacturerId = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PagedRequest.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await modelService.ListAsync(
            new AircraftModelListFilter(search, manufacturerId, isActive),
            new PagedRequest(page, pageSize),
            cancellationToken);
        var paged = result.Value!;

        return Ok(new AircraftModelPageResponse(
            paged.Items.Select(AircraftModelSummaryResponse.From).ToList(),
            paged.Page,
            paged.PageSize,
            paged.TotalCount,
            paged.TotalPages));
    }

    [HttpGet("{id:guid}")]
    [ReturnsETag(StatusCodes.Status200OK)]
    [ProducesResponseType<AircraftModelResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await modelService.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure)
        {
            return Problem(result.ErrorCode!, result.ErrorMessage!);
        }

        ConcurrencyHeaders.SetETag(Response, result.Value!.RowVersion);
        return Ok(AircraftModelResponse.From(result.Value!));
    }

    [HttpPost]
    [ReturnsETag(StatusCodes.Status201Created)]
    [ProducesResponseType<AircraftModelResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateAircraftModelRequest request, CancellationToken cancellationToken)
    {
        var created = await modelService.CreateAsync(
            new CreateAircraftModelDto(
                request.ManufacturerId!.Value,
                request.Name,
                request.ModelCode,
                request.AircraftCategory!.Value,
                request.IsActive),
            cancellationToken);
        if (created.IsFailure)
        {
            return Problem(created.ErrorCode!, created.ErrorMessage!);
        }

        var result = await modelService.GetByIdAsync(created.Value, cancellationToken);
        ConcurrencyHeaders.SetETag(Response, result.Value!.RowVersion);
        return CreatedAtAction(nameof(Get), new { id = created.Value }, AircraftModelResponse.From(result.Value!));
    }

    [HttpPut("{id:guid}")]
    [RequiresIfMatch]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired)]
    public async Task<IActionResult> Update(Guid id, UpdateAircraftModelRequest request, CancellationToken cancellationToken)
    {
        if (!ConcurrencyHeaders.TryReadIfMatch(Request, out var rowVersion))
        {
            return PreconditionRequired();
        }

        var result = await modelService.UpdateAsync(
            id,
            new UpdateAircraftModelDto(
                request.Name, request.ModelCode, request.AircraftCategory!.Value, request.IsActive!.Value, rowVersion),
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

        var result = await modelService.DeleteAsync(id, rowVersion, cancellationToken);
        return result.IsSuccess ? NoContent() : Problem(result.ErrorCode!, result.ErrorMessage!);
    }

    private ObjectResult Problem(string errorCode, string errorMessage)
    {
        var statusCode = errorCode switch
        {
            AircraftModelErrors.NotFound => StatusCodes.Status404NotFound,
            AircraftModelErrors.Validation or AircraftModelErrors.ManufacturerNotFound
                or AircraftModelErrors.ManufacturerInactive => StatusCodes.Status400BadRequest,
            AircraftModelErrors.DuplicateModelCode or AircraftModelErrors.InUse => StatusCodes.Status409Conflict,
            AircraftModelErrors.ConcurrencyConflict => StatusCodes.Status412PreconditionFailed,
            _ => StatusCodes.Status400BadRequest
        };

        var problem = Problem(detail: errorMessage, statusCode: statusCode);
        ((ProblemDetails)problem.Value!).Extensions["code"] = errorCode;
        return problem;
    }

    private ObjectResult PreconditionRequired()
    {
        var problem = Problem(
            detail: "Send the aircraft model's current ETag in the If-Match header.",
            statusCode: StatusCodes.Status428PreconditionRequired);
        ((ProblemDetails)problem.Value!).Extensions["code"] = "AircraftModel.PreconditionRequired";
        return problem;
    }
}
