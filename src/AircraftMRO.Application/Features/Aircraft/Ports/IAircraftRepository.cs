using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.Aircraft.DTOs;
using AircraftMRO.Domain.Common.Results;
using AircraftEntity = AircraftMRO.Domain.Entities.Aircraft;

namespace AircraftMRO.Application.Features.Aircraft.Ports;

public interface IAircraftRepository
{
    Task<PagedResponse<AircraftListItemDto>> ListAsync(
        AircraftListFilter filter,
        PagedRequest request,
        CancellationToken cancellationToken);

    Task<AircraftStatisticsDto> GetStatisticsAsync(CancellationToken cancellationToken);

    Task<AircraftDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Loads a tracked aircraft for a write in the same operation.</summary>
    Task<AircraftEntity?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> RegistrationNumberExistsAsync(string registrationNumber, Guid? excludeId, CancellationToken cancellationToken);

    Task<bool> SerialNumberExistsAsync(Guid manufacturerId, string serialNumber, Guid? excludeId, CancellationToken cancellationToken);

    /// <summary>The live model with its manufacturer's state, or null when either was deleted or never existed.</summary>
    Task<AircraftModelReference?> GetModelReferenceAsync(Guid modelId, CancellationToken cancellationToken);

    /// <summary>Active manufacturers and models, plus the given current ones even when inactive.</summary>
    Task<AircraftFormOptionsDto> ListFormOptionsAsync(
        Guid? currentManufacturerId,
        Guid? currentModelId,
        CancellationToken cancellationToken);

    Task<OpenWorkOrderCounts> GetOpenWorkOrderCountsAsync(Guid aircraftId, CancellationToken cancellationToken);

    /// <summary>Fails with a duplicate error when a unique constraint rejects the insert.</summary>
    Task<Result> AddAsync(AircraftEntity aircraft, CancellationToken cancellationToken);

    /// <summary>Fails with a concurrency or duplicate error when the save is rejected.</summary>
    Task<Result> UpdateAsync(AircraftEntity aircraft, byte[] expectedRowVersion, CancellationToken cancellationToken);

    /// <summary>Soft-deletes the aircraft; fails with a concurrency error when it changed since it was read.</summary>
    Task<Result> DeleteAsync(AircraftEntity aircraft, byte[] expectedRowVersion, CancellationToken cancellationToken);
}
