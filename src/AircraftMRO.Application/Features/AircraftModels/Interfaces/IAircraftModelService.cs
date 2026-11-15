using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.AircraftModels.DTOs;
using AircraftMRO.Domain.Common.Results;

namespace AircraftMRO.Application.Features.AircraftModels.Interfaces;

public interface IAircraftModelService
{
    Task<Result<PagedResponse<AircraftModelListItemDto>>> ListAsync(
        AircraftModelListFilter filter,
        PagedRequest request,
        CancellationToken cancellationToken);

    Task<Result<AircraftModelDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>All live manufacturers, active or not, for filtering and for the create form.</summary>
    Task<Result<IReadOnlyList<ManufacturerOptionDto>>> ListManufacturerOptionsAsync(CancellationToken cancellationToken);

    Task<Result<Guid>> CreateAsync(CreateAircraftModelDto dto, CancellationToken cancellationToken);

    Task<Result> UpdateAsync(Guid id, UpdateAircraftModelDto dto, CancellationToken cancellationToken);

    Task<Result> DeleteAsync(Guid id, byte[] rowVersion, CancellationToken cancellationToken);
}
