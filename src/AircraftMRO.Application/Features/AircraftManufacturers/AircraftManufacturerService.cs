using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.AircraftManufacturers.DTOs;
using AircraftMRO.Application.Features.AircraftManufacturers.Interfaces;
using AircraftMRO.Application.Features.AircraftManufacturers.Ports;
using AircraftMRO.Domain.Common.Results;
using AircraftMRO.Domain.Entities;

namespace AircraftMRO.Application.Features.AircraftManufacturers;

public sealed class AircraftManufacturerService(IAircraftManufacturerRepository repository) : IAircraftManufacturerService
{
    public async Task<Result<PagedResponse<AircraftManufacturerListItemDto>>> ListAsync(
        AircraftManufacturerListFilter filter,
        PagedRequest request,
        CancellationToken cancellationToken) =>
        Result<PagedResponse<AircraftManufacturerListItemDto>>.Success(
            await repository.ListAsync(filter, request, cancellationToken));

    public async Task<Result<AircraftManufacturerDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var manufacturer = await repository.GetByIdAsync(id, cancellationToken);

        return manufacturer is null
            ? Result<AircraftManufacturerDto>.Failure(AircraftManufacturerErrors.NotFound, AircraftManufacturerErrors.NotFoundMessage)
            : Result<AircraftManufacturerDto>.Success(manufacturer);
    }

    public async Task<Result<IReadOnlyList<CountryOptionDto>>> ListCountryOptionsAsync(
        Guid? currentCountryId,
        CancellationToken cancellationToken) =>
        Result<IReadOnlyList<CountryOptionDto>>.Success(
            await repository.ListCountryOptionsAsync(currentCountryId, cancellationToken));

    public async Task<Result<Guid>> CreateAsync(CreateAircraftManufacturerDto dto, CancellationToken cancellationToken)
    {
        var created = AircraftManufacturer.Create(dto.Name, dto.Code, dto.CountryId, dto.IsActive);
        if (created.IsFailure)
        {
            return Result<Guid>.Failure(created.ErrorCode!, created.ErrorMessage!);
        }

        var manufacturer = created.Value!;
        var checks = await CheckAsync(manufacturer, currentCountryId: null, excludeId: null, cancellationToken);
        if (checks.IsFailure)
        {
            return Result<Guid>.Failure(checks.ErrorCode!, checks.ErrorMessage!);
        }

        var saved = await repository.AddAsync(manufacturer, cancellationToken);

        return saved.IsSuccess
            ? Result<Guid>.Success(manufacturer.Id)
            : Result<Guid>.Failure(saved.ErrorCode!, saved.ErrorMessage!);
    }

    public async Task<Result> UpdateAsync(Guid id, UpdateAircraftManufacturerDto dto, CancellationToken cancellationToken)
    {
        var manufacturer = await repository.GetForUpdateAsync(id, cancellationToken);
        if (manufacturer is null)
        {
            return Result.Failure(AircraftManufacturerErrors.NotFound, AircraftManufacturerErrors.NotFoundMessage);
        }

        var currentCountryId = manufacturer.CountryId;
        var updated = manufacturer.Update(dto.Name, dto.Code, dto.CountryId, dto.IsActive);
        if (updated.IsFailure)
        {
            return updated;
        }

        var checks = await CheckAsync(manufacturer, currentCountryId, excludeId: id, cancellationToken);
        if (checks.IsFailure)
        {
            return checks;
        }

        return await repository.UpdateAsync(manufacturer, dto.RowVersion, cancellationToken);
    }

    public async Task<Result> DeleteAsync(Guid id, byte[] rowVersion, CancellationToken cancellationToken)
    {
        var manufacturer = await repository.GetForUpdateAsync(id, cancellationToken);
        if (manufacturer is null)
        {
            return Result.Failure(AircraftManufacturerErrors.NotFound, AircraftManufacturerErrors.NotFoundMessage);
        }

        if (await repository.IsInUseAsync(id, cancellationToken))
        {
            return Result.Failure(AircraftManufacturerErrors.InUse, AircraftManufacturerErrors.InUseMessage);
        }

        return await repository.DeleteAsync(manufacturer, rowVersion, cancellationToken);
    }

    /// <summary>
    /// The country must exist, and must be active unless the manufacturer already had it, so that
    /// deactivating a country does not block unrelated edits to its manufacturers.
    /// </summary>
    private async Task<Result> CheckAsync(
        AircraftManufacturer manufacturer,
        Guid? currentCountryId,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        var country = await repository.GetCountryAsync(manufacturer.CountryId!.Value, cancellationToken);
        if (country is null)
        {
            return Result.Failure(AircraftManufacturerErrors.CountryNotFound, AircraftManufacturerErrors.CountryNotFoundMessage);
        }

        if (!country.IsActive && country.Id != currentCountryId)
        {
            return Result.Failure(AircraftManufacturerErrors.CountryInactive, AircraftManufacturerErrors.CountryInactiveMessage);
        }

        if (await repository.CodeExistsAsync(manufacturer.Code, excludeId, cancellationToken))
        {
            return Result.Failure(AircraftManufacturerErrors.DuplicateCode, AircraftManufacturerErrors.DuplicateCodeMessage);
        }

        return Result.Success();
    }
}
