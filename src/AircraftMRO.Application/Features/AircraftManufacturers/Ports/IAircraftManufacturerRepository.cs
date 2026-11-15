using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.AircraftManufacturers.DTOs;
using AircraftMRO.Domain.Common.Results;
using AircraftMRO.Domain.Entities;

namespace AircraftMRO.Application.Features.AircraftManufacturers.Ports;

public interface IAircraftManufacturerRepository
{
    Task<PagedResponse<AircraftManufacturerListItemDto>> ListAsync(
        AircraftManufacturerListFilter filter,
        PagedRequest request,
        CancellationToken cancellationToken);

    Task<AircraftManufacturerDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Loads a tracked manufacturer for a write in the same operation.</summary>
    Task<AircraftManufacturer?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task<CountryOptionDto?> GetCountryAsync(Guid countryId, CancellationToken cancellationToken);

    Task<IReadOnlyList<CountryOptionDto>> ListCountryOptionsAsync(Guid? includeId, CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken cancellationToken);

    /// <summary>True while a live aircraft model or aircraft refers to the manufacturer.</summary>
    Task<bool> IsInUseAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Fails with a duplicate error when a unique constraint rejects the insert.</summary>
    Task<Result> AddAsync(AircraftManufacturer manufacturer, CancellationToken cancellationToken);

    /// <summary>Fails with a concurrency or duplicate error when the save is rejected.</summary>
    Task<Result> UpdateAsync(AircraftManufacturer manufacturer, byte[] expectedRowVersion, CancellationToken cancellationToken);

    /// <summary>Soft-deletes the manufacturer; fails with a concurrency error when it changed since it was read.</summary>
    Task<Result> DeleteAsync(AircraftManufacturer manufacturer, byte[] expectedRowVersion, CancellationToken cancellationToken);
}
