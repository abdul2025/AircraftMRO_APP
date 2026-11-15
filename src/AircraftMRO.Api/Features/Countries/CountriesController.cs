using AircraftMRO.Api.Features.Countries.Contracts;
using AircraftMRO.Api.OpenApi;
using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.Countries;
using AircraftMRO.Application.Features.Countries.DTOs;
using AircraftMRO.Application.Features.Countries.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AircraftMRO.Api.Features.Countries;

/// <summary>
/// Country reference data. Reads return an <c>ETag</c>; updates and deletes require it in <c>If-Match</c>.
/// </summary>
[ApiController]
[Route("api/countries")]
public sealed class CountriesController(ICountryService countryService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<CountryPageResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? search = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PagedRequest.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await countryService.ListAsync(
            new CountryListFilter(search, isActive),
            new PagedRequest(page, pageSize),
            cancellationToken);
        var paged = result.Value!;

        return Ok(new CountryPageResponse(
            paged.Items.Select(CountrySummaryResponse.From).ToList(),
            paged.Page,
            paged.PageSize,
            paged.TotalCount,
            paged.TotalPages));
    }

    [HttpGet("{id:guid}")]
    [ReturnsETag(StatusCodes.Status200OK)]
    [ProducesResponseType<CountryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await countryService.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure)
        {
            return Problem(result.ErrorCode!, result.ErrorMessage!);
        }

        ConcurrencyHeaders.SetETag(Response, result.Value!.RowVersion);
        return Ok(CountryResponse.From(result.Value!));
    }

    [HttpPost]
    [ReturnsETag(StatusCodes.Status201Created)]
    [ProducesResponseType<CountryResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateCountryRequest request, CancellationToken cancellationToken)
    {
        var created = await countryService.CreateAsync(
            new CreateCountryDto(request.Name, request.Code, request.IsActive),
            cancellationToken);
        if (created.IsFailure)
        {
            return Problem(created.ErrorCode!, created.ErrorMessage!);
        }

        var result = await countryService.GetByIdAsync(created.Value, cancellationToken);
        ConcurrencyHeaders.SetETag(Response, result.Value!.RowVersion);
        return CreatedAtAction(nameof(Get), new { id = created.Value }, CountryResponse.From(result.Value!));
    }

    [HttpPut("{id:guid}")]
    [RequiresIfMatch]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired)]
    public async Task<IActionResult> Update(Guid id, UpdateCountryRequest request, CancellationToken cancellationToken)
    {
        if (!ConcurrencyHeaders.TryReadIfMatch(Request, out var rowVersion))
        {
            return PreconditionRequired();
        }

        var result = await countryService.UpdateAsync(
            id,
            new UpdateCountryDto(request.Name, request.Code, request.IsActive!.Value, rowVersion),
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

        var result = await countryService.DeleteAsync(id, rowVersion, cancellationToken);
        return result.IsSuccess ? NoContent() : Problem(result.ErrorCode!, result.ErrorMessage!);
    }

    private ObjectResult Problem(string errorCode, string errorMessage)
    {
        var statusCode = errorCode switch
        {
            CountryErrors.NotFound => StatusCodes.Status404NotFound,
            CountryErrors.Validation => StatusCodes.Status400BadRequest,
            CountryErrors.DuplicateCode or CountryErrors.InUse => StatusCodes.Status409Conflict,
            CountryErrors.ConcurrencyConflict => StatusCodes.Status412PreconditionFailed,
            _ => StatusCodes.Status400BadRequest
        };

        var problem = Problem(detail: errorMessage, statusCode: statusCode);
        ((ProblemDetails)problem.Value!).Extensions["code"] = errorCode;
        return problem;
    }

    private ObjectResult PreconditionRequired()
    {
        var problem = Problem(
            detail: "Send the country's current ETag in the If-Match header.",
            statusCode: StatusCodes.Status428PreconditionRequired);
        ((ProblemDetails)problem.Value!).Extensions["code"] = "Country.PreconditionRequired";
        return problem;
    }
}
