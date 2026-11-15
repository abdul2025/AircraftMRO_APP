using AircraftMRO.Api.Features.AircraftManufacturers.Contracts;
using AircraftMRO.Api.OpenApi;
using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.AircraftManufacturers;
using AircraftMRO.Application.Features.AircraftManufacturers.DTOs;
using AircraftMRO.Application.Features.AircraftManufacturers.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AircraftMRO.Api.Features.AircraftManufacturers;

/// <summary>
/// Manufacturer reference data. Reads return an <c>ETag</c>; updates and deletes require it in <c>If-Match</c>.
/// </summary>
[ApiController]
[Route("api/aircraft-manufacturers")]
public sealed class AircraftManufacturersController(IAircraftManufacturerService manufacturerService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<AircraftManufacturerPageResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? search = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PagedRequest.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await manufacturerService.ListAsync(
            new AircraftManufacturerListFilter(search, isActive),
            new PagedRequest(page, pageSize),
            cancellationToken);
        var paged = result.Value!;

        return Ok(new AircraftManufacturerPageResponse(
            paged.Items.Select(AircraftManufacturerSummaryResponse.From).ToList(),
            paged.Page,
            paged.PageSize,
            paged.TotalCount,
            paged.TotalPages));
    }

    [HttpGet("{id:guid}")]
    [ReturnsETag(StatusCodes.Status200OK)]
    [ProducesResponseType<AircraftManufacturerResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await manufacturerService.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure)
        {
            return Problem(result.ErrorCode!, result.ErrorMessage!);
        }

        ConcurrencyHeaders.SetETag(Response, result.Value!.RowVersion);
        return Ok(AircraftManufacturerResponse.From(result.Value!));
    }

    [HttpPost]
    [ReturnsETag(StatusCodes.Status201Created)]
    [ProducesResponseType<AircraftManufacturerResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateAircraftManufacturerRequest request, CancellationToken cancellationToken)
    {
        var created = await manufacturerService.CreateAsync(
            new CreateAircraftManufacturerDto(request.Name, request.Code, request.CountryId!.Value, request.IsActive),
            cancellationToken);
        if (created.IsFailure)
        {
            return Problem(created.ErrorCode!, created.ErrorMessage!);
        }

        var result = await manufacturerService.GetByIdAsync(created.Value, cancellationToken);
        ConcurrencyHeaders.SetETag(Response, result.Value!.RowVersion);
        return CreatedAtAction(nameof(Get), new { id = created.Value }, AircraftManufacturerResponse.From(result.Value!));
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
        UpdateAircraftManufacturerRequest request,
        CancellationToken cancellationToken)
    {
        if (!ConcurrencyHeaders.TryReadIfMatch(Request, out var rowVersion))
        {
            return PreconditionRequired();
        }

        var result = await manufacturerService.UpdateAsync(
            id,
            new UpdateAircraftManufacturerDto(
                request.Name, request.Code, request.CountryId!.Value, request.IsActive!.Value, rowVersion),
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

        var result = await manufacturerService.DeleteAsync(id, rowVersion, cancellationToken);
        return result.IsSuccess ? NoContent() : Problem(result.ErrorCode!, result.ErrorMessage!);
    }

    private ObjectResult Problem(string errorCode, string errorMessage)
    {
        var statusCode = errorCode switch
        {
            AircraftManufacturerErrors.NotFound => StatusCodes.Status404NotFound,
            AircraftManufacturerErrors.Validation or AircraftManufacturerErrors.CountryNotFound
                or AircraftManufacturerErrors.CountryInactive => StatusCodes.Status400BadRequest,
            AircraftManufacturerErrors.DuplicateCode or AircraftManufacturerErrors.InUse => StatusCodes.Status409Conflict,
            AircraftManufacturerErrors.ConcurrencyConflict => StatusCodes.Status412PreconditionFailed,
            _ => StatusCodes.Status400BadRequest
        };

        var problem = Problem(detail: errorMessage, statusCode: statusCode);
        ((ProblemDetails)problem.Value!).Extensions["code"] = errorCode;
        return problem;
    }

    private ObjectResult PreconditionRequired()
    {
        var problem = Problem(
            detail: "Send the manufacturer's current ETag in the If-Match header.",
            statusCode: StatusCodes.Status428PreconditionRequired);
        ((ProblemDetails)problem.Value!).Extensions["code"] = "AircraftManufacturer.PreconditionRequired";
        return problem;
    }
}
