using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.Countries.DTOs;
using AircraftMRO.Domain.Common.Results;

namespace AircraftMRO.Application.Features.Countries.Interfaces;

public interface ICountryService
{
    Task<Result<PagedResponse<CountryListItemDto>>> ListAsync(
        CountryListFilter filter,
        PagedRequest request,
        CancellationToken cancellationToken);

    Task<Result<CountryDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<Guid>> CreateAsync(CreateCountryDto dto, CancellationToken cancellationToken);

    Task<Result> UpdateAsync(Guid id, UpdateCountryDto dto, CancellationToken cancellationToken);

    Task<Result> DeleteAsync(Guid id, byte[] rowVersion, CancellationToken cancellationToken);
}
