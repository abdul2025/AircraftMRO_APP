using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.Aircraft.DTOs;
using AircraftMRO.Application.Features.Aircraft.Interfaces;
using AircraftMRO.Application.Features.Aircraft.Ports;
using AircraftMRO.Domain.Common.Results;
using AircraftMRO.Domain.Enums.Aircraft;
using AircraftEntity = AircraftMRO.Domain.Entities.Aircraft;

namespace AircraftMRO.Application.Features.Aircraft;

public sealed class AircraftService(IAircraftRepository repository, TimeProvider timeProvider) : IAircraftService
{
    public async Task<Result<PagedResponse<AircraftListItemDto>>> ListAsync(
        AircraftListFilter filter,
        PagedRequest request,
        CancellationToken cancellationToken) =>
        Result<PagedResponse<AircraftListItemDto>>.Success(
            await repository.ListAsync(filter, request, cancellationToken));

    public async Task<Result<AircraftStatisticsDto>> GetStatisticsAsync(CancellationToken cancellationToken) =>
        Result<AircraftStatisticsDto>.Success(await repository.GetStatisticsAsync(cancellationToken));

    public async Task<Result<AircraftDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var aircraft = await repository.GetByIdAsync(id, cancellationToken);

        return aircraft is null
            ? Result<AircraftDto>.Failure(AircraftErrors.NotFound, AircraftErrors.NotFoundMessage)
            : Result<AircraftDto>.Success(aircraft);
    }

    public async Task<Result<Guid>> CreateAsync(CreateAircraftDto dto, CancellationToken cancellationToken)
    {
        var created = AircraftEntity.Create(
            dto.RegistrationNumber, dto.Manufacturer, dto.Model, dto.SerialNumber,
            dto.YearOfManufacture, dto.TotalFlightHours, dto.Status, CurrentYear);
        if (created.IsFailure)
        {
            return Result<Guid>.Failure(created.ErrorCode!, created.ErrorMessage!);
        }

        var aircraft = created.Value!;
        var uniqueness = await CheckUniquenessAsync(aircraft, excludeId: null, cancellationToken);
        if (uniqueness.IsFailure)
        {
            return Result<Guid>.Failure(uniqueness.ErrorCode!, uniqueness.ErrorMessage!);
        }

        var saved = await repository.AddAsync(aircraft, cancellationToken);

        return saved.IsSuccess
            ? Result<Guid>.Success(aircraft.Id)
            : Result<Guid>.Failure(saved.ErrorCode!, saved.ErrorMessage!);
    }

    public async Task<Result> UpdateAsync(Guid id, UpdateAircraftDto dto, CancellationToken cancellationToken)
    {
        var aircraft = await repository.GetForUpdateAsync(id, cancellationToken);
        if (aircraft is null)
        {
            return Result.Failure(AircraftErrors.NotFound, AircraftErrors.NotFoundMessage);
        }

        var updated = aircraft.Update(
            dto.RegistrationNumber, dto.Manufacturer, dto.Model, dto.SerialNumber,
            dto.YearOfManufacture, dto.TotalFlightHours, dto.Status, CurrentYear);
        if (updated.IsFailure)
        {
            return updated;
        }

        var uniqueness = await CheckUniquenessAsync(aircraft, excludeId: id, cancellationToken);
        if (uniqueness.IsFailure)
        {
            return uniqueness;
        }

        var status = await CheckStatusAgainstOpenWorkOrdersAsync(id, aircraft.Status, cancellationToken);
        if (status.IsFailure)
        {
            return status;
        }

        return await repository.UpdateAsync(aircraft, dto.RowVersion, cancellationToken);
    }

    public async Task<Result> DeleteAsync(Guid id, byte[] rowVersion, CancellationToken cancellationToken)
    {
        var aircraft = await repository.GetForUpdateAsync(id, cancellationToken);
        if (aircraft is null)
        {
            return Result.Failure(AircraftErrors.NotFound, AircraftErrors.NotFoundMessage);
        }

        var openWorkOrders = await repository.GetOpenWorkOrderCountsAsync(id, cancellationToken);
        if (openWorkOrders.Total > 0)
        {
            return Result.Failure(AircraftErrors.HasOpenWorkOrders, AircraftErrors.HasOpenWorkOrdersMessage);
        }

        return await repository.DeleteAsync(aircraft, rowVersion, cancellationToken);
    }

    /// <summary>
    /// While work orders are open they decide the status: grounded if any is critical, in
    /// maintenance otherwise, and nothing else can be set by hand. A work order saved at the same
    /// moment is caught by the row version, because saving a work order always writes its aircraft.
    /// </summary>
    private async Task<Result> CheckStatusAgainstOpenWorkOrdersAsync(
        Guid aircraftId,
        AircraftStatus newStatus,
        CancellationToken cancellationToken)
    {
        var openWorkOrders = await repository.GetOpenWorkOrderCountsAsync(aircraftId, cancellationToken);
        if (openWorkOrders.Total == 0)
        {
            return Result.Success();
        }

        var anyCritical = openWorkOrders.Critical > 0;
        if (newStatus == AircraftEntity.StatusForOpenWorkOrders(anyCritical))
        {
            return Result.Success();
        }

        return anyCritical
            ? Result.Failure(AircraftErrors.OpenCriticalWorkOrders, AircraftErrors.OpenCriticalWorkOrdersMessage)
            : Result.Failure(AircraftErrors.OpenWorkOrders, AircraftErrors.OpenWorkOrdersMessage);
    }

    private int CurrentYear => timeProvider.GetUtcNow().Year;

    private async Task<Result> CheckUniquenessAsync(
        AircraftEntity aircraft,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        if (await repository.RegistrationNumberExistsAsync(aircraft.RegistrationNumber, excludeId, cancellationToken))
        {
            return Result.Failure(AircraftErrors.DuplicateRegistration, AircraftErrors.DuplicateRegistrationMessage);
        }

        if (await repository.SerialNumberExistsAsync(aircraft.Manufacturer, aircraft.SerialNumber, excludeId, cancellationToken))
        {
            return Result.Failure(AircraftErrors.DuplicateSerialNumber, AircraftErrors.DuplicateSerialNumberMessage);
        }

        return Result.Success();
    }
}
