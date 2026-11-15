using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.Countries.DTOs;
using AircraftMRO.Domain.Common.Results;
using AircraftMRO.Domain.Entities;

namespace AircraftMRO.Application.Features.Countries.Ports;

public interface ICountryRepository
{
    Task<PagedResponse<CountryListItemDto>> ListAsync(
        CountryListFilter filter,
        PagedRequest request,
        CancellationToken cancellationToken);

    Task<CountryDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Loads a tracked country for a write in the same operation.</summary>
    Task<Country?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken cancellationToken);

    /// <summary>True while a live manufacturer refers to the country.</summary>
    Task<bool> IsInUseAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Fails with a duplicate error when a unique constraint rejects the insert.</summary>
    Task<Result> AddAsync(Country country, CancellationToken cancellationToken);

    /// <summary>Fails with a concurrency or duplicate error when the save is rejected.</summary>
    Task<Result> UpdateAsync(Country country, byte[] expectedRowVersion, CancellationToken cancellationToken);

    /// <summary>Soft-deletes the country; fails with a concurrency error when it changed since it was read.</summary>
    Task<Result> DeleteAsync(Country country, byte[] expectedRowVersion, CancellationToken cancellationToken);
}
