using AircraftMRO.Api.Features.Aircraft.Contracts;
using AircraftMRO.Api.OpenApi;
using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.Aircraft;
using AircraftMRO.Application.Features.Aircraft.DTOs;
using AircraftMRO.Application.Features.Aircraft.Interfaces;
using AircraftMRO.Domain.Common.Results;
using AircraftMRO.Domain.Enums.Aircraft;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace AircraftMRO.Api.Features.Aircraft;

/// <summary>
/// Aircraft CRUD. Reads return an <c>ETag</c>; updates and deletes require it in <c>If-Match</c>.
/// </summary>
[ApiController]
[Route("api/aircraft")]
public sealed class AircraftController(IAircraftService aircraftService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<AircraftPageResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? search = null,
        [FromQuery] AircraftStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PagedRequest.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await aircraftService.ListAsync(
            new AircraftListFilter(search, status),
            new PagedRequest(page, pageSize),
            cancellationToken);
        var paged = result.Value!;

        return Ok(new AircraftPageResponse(
            paged.Items.Select(AircraftSummaryResponse.From).ToList(),
            paged.Page,
            paged.PageSize,
            paged.TotalCount,
            paged.TotalPages));
    }

    [HttpGet("{id:guid}")]
    [ReturnsETag(StatusCodes.Status200OK)]
    [ProducesResponseType<AircraftResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await aircraftService.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure)
        {
            return Problem(result.ErrorCode!, result.ErrorMessage!);
        }

        SetETag(result.Value!.RowVersion);
        return Ok(AircraftResponse.From(result.Value!));
    }

    [HttpPost]
    [ReturnsETag(StatusCodes.Status201Created)]
    [ProducesResponseType<AircraftResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateAircraftRequest request, CancellationToken cancellationToken)
    {
        var created = await aircraftService.CreateAsync(
            new CreateAircraftDto(
                request.RegistrationNumber,
                request.Manufacturer,
                request.Model,
                request.SerialNumber,
                request.YearOfManufacture!.Value,
                request.TotalFlightHours!.Value,
                request.Status!.Value),
            cancellationToken);
        if (created.IsFailure)
        {
            return Problem(created.ErrorCode!, created.ErrorMessage!);
        }

        var result = await aircraftService.GetByIdAsync(created.Value, cancellationToken);
        SetETag(result.Value!.RowVersion);
        return CreatedAtAction(nameof(Get), new { id = created.Value }, AircraftResponse.From(result.Value!));
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
        UpdateAircraftRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryReadIfMatch(out var rowVersion))
        {
            return PreconditionRequired();
        }

        var result = await aircraftService.UpdateAsync(
            id,
            new UpdateAircraftDto(
                request.RegistrationNumber,
                request.Manufacturer,
                request.Model,
                request.SerialNumber,
                request.YearOfManufacture!.Value,
                request.TotalFlightHours!.Value,
                request.Status!.Value,
                rowVersion),
            cancellationToken);

        return result.IsSuccess ? NoContent() : Problem(result.ErrorCode!, result.ErrorMessage!);
    }

    [HttpDelete("{id:guid}")]
    [RequiresIfMatch]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!TryReadIfMatch(out var rowVersion))
        {
            return PreconditionRequired();
        }

        var result = await aircraftService.DeleteAsync(id, rowVersion, cancellationToken);
        return result.IsSuccess ? NoContent() : Problem(result.ErrorCode!, result.ErrorMessage!);
    }

    private ObjectResult Problem(string errorCode, string errorMessage)
    {
        var statusCode = errorCode switch
        {
            AircraftErrors.NotFound => StatusCodes.Status404NotFound,
            AircraftErrors.Validation => StatusCodes.Status400BadRequest,
            AircraftErrors.DuplicateRegistration or AircraftErrors.DuplicateSerialNumber => StatusCodes.Status409Conflict,
            AircraftErrors.ConcurrencyConflict => StatusCodes.Status412PreconditionFailed,
            _ => StatusCodes.Status400BadRequest
        };

        var problem = Problem(detail: errorMessage, statusCode: statusCode);
        ((ProblemDetails)problem.Value!).Extensions["code"] = errorCode;
        return problem;
    }

    private ObjectResult PreconditionRequired()
    {
        var problem = Problem(
            detail: "Send the aircraft's current ETag in the If-Match header.",
            statusCode: StatusCodes.Status428PreconditionRequired);
        ((ProblemDetails)problem.Value!).Extensions["code"] = "Aircraft.PreconditionRequired";
        return problem;
    }

    private void SetETag(byte[] rowVersion) =>
        Response.Headers.ETag = new EntityTagHeaderValue($"\"{Convert.ToBase64String(rowVersion)}\"").ToString();

    private bool TryReadIfMatch(out byte[] rowVersion)
    {
        rowVersion = [];
        var header = Request.GetTypedHeaders().IfMatch;
        if (header is not [{ IsWeak: false } tag] || tag.Tag.Length < 3)
        {
            return false;
        }

        var value = tag.Tag.Value![1..^1];
        var buffer = new byte[value.Length];
        if (!Convert.TryFromBase64String(value, buffer, out var written) || written == 0)
        {
            return false;
        }

        rowVersion = buffer[..written];
        return true;
    }
}
