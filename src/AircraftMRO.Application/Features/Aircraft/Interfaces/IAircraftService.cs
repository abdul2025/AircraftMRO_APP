using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.Aircraft.DTOs;
using AircraftMRO.Domain.Common.Results;

namespace AircraftMRO.Application.Features.Aircraft.Interfaces;

public interface IAircraftService
{
    Task<Result<PagedResponse<AircraftListItemDto>>> ListAsync(
        AircraftListFilter filter,
        PagedRequest request,
        CancellationToken cancellationToken);

    Task<Result<AircraftStatisticsDto>> GetStatisticsAsync(CancellationToken cancellationToken);

    Task<Result<AircraftDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<Guid>> CreateAsync(CreateAircraftDto dto, CancellationToken cancellationToken);

    Task<Result> UpdateAsync(Guid id, UpdateAircraftDto dto, CancellationToken cancellationToken);

    Task<Result> DeleteAsync(Guid id, byte[] rowVersion, CancellationToken cancellationToken);
}
