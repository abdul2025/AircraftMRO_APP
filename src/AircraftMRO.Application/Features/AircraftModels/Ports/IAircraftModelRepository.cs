using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.AircraftModels.DTOs;
using AircraftMRO.Domain.Common.Results;
using AircraftMRO.Domain.Entities;

namespace AircraftMRO.Application.Features.AircraftModels.Ports;

public interface IAircraftModelRepository
{
    Task<PagedResponse<AircraftModelListItemDto>> ListAsync(
        AircraftModelListFilter filter,
        PagedRequest request,
        CancellationToken cancellationToken);

    Task<AircraftModelDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Loads a tracked model for a write in the same operation.</summary>
    Task<AircraftModel?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task<ManufacturerOptionDto?> GetManufacturerAsync(Guid manufacturerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ManufacturerOptionDto>> ListManufacturerOptionsAsync(CancellationToken cancellationToken);

    Task<bool> ModelCodeExistsAsync(Guid manufacturerId, string modelCode, Guid? excludeId, CancellationToken cancellationToken);

    /// <summary>True while a live aircraft refers to the model.</summary>
    Task<bool> IsInUseAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Fails with a duplicate error when a unique constraint rejects the insert.</summary>
    Task<Result> AddAsync(AircraftModel model, CancellationToken cancellationToken);

    /// <summary>Fails with a concurrency or duplicate error when the save is rejected.</summary>
    Task<Result> UpdateAsync(AircraftModel model, byte[] expectedRowVersion, CancellationToken cancellationToken);

    /// <summary>Soft-deletes the model; fails with a concurrency error when it changed since it was read.</summary>
    Task<Result> DeleteAsync(AircraftModel model, byte[] expectedRowVersion, CancellationToken cancellationToken);
}
