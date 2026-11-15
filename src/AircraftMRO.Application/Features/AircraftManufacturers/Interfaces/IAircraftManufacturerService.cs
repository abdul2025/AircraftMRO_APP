using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.AircraftManufacturers.DTOs;
using AircraftMRO.Domain.Common.Results;

namespace AircraftMRO.Application.Features.AircraftManufacturers.Interfaces;

public interface IAircraftManufacturerService
{
    Task<Result<PagedResponse<AircraftManufacturerListItemDto>>> ListAsync(
        AircraftManufacturerListFilter filter,
        PagedRequest request,
        CancellationToken cancellationToken);

    Task<Result<AircraftManufacturerDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Active countries, plus <paramref name="currentCountryId"/> so an edit form can still show it.</summary>
    Task<Result<IReadOnlyList<CountryOptionDto>>> ListCountryOptionsAsync(
        Guid? currentCountryId,
        CancellationToken cancellationToken);

    Task<Result<Guid>> CreateAsync(CreateAircraftManufacturerDto dto, CancellationToken cancellationToken);

    Task<Result> UpdateAsync(Guid id, UpdateAircraftManufacturerDto dto, CancellationToken cancellationToken);

    Task<Result> DeleteAsync(Guid id, byte[] rowVersion, CancellationToken cancellationToken);
}
