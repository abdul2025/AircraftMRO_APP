using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.AircraftModels.DTOs;
using AircraftMRO.Application.Features.AircraftModels.Interfaces;
using AircraftMRO.Application.Features.AircraftModels.Ports;
using AircraftMRO.Domain.Common.Results;
using AircraftMRO.Domain.Entities;

namespace AircraftMRO.Application.Features.AircraftModels;

public sealed class AircraftModelService(IAircraftModelRepository repository) : IAircraftModelService
{
    public async Task<Result<PagedResponse<AircraftModelListItemDto>>> ListAsync(
        AircraftModelListFilter filter,
        PagedRequest request,
        CancellationToken cancellationToken) =>
        Result<PagedResponse<AircraftModelListItemDto>>.Success(
            await repository.ListAsync(filter, request, cancellationToken));

    public async Task<Result<AircraftModelDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var model = await repository.GetByIdAsync(id, cancellationToken);

        return model is null
            ? Result<AircraftModelDto>.Failure(AircraftModelErrors.NotFound, AircraftModelErrors.NotFoundMessage)
            : Result<AircraftModelDto>.Success(model);
    }

    public async Task<Result<IReadOnlyList<ManufacturerOptionDto>>> ListManufacturerOptionsAsync(
        CancellationToken cancellationToken) =>
        Result<IReadOnlyList<ManufacturerOptionDto>>.Success(
            await repository.ListManufacturerOptionsAsync(cancellationToken));

    public async Task<Result<Guid>> CreateAsync(CreateAircraftModelDto dto, CancellationToken cancellationToken)
    {
        var created = AircraftModel.Create(dto.ManufacturerId, dto.Name, dto.ModelCode, dto.AircraftCategory, dto.IsActive);
        if (created.IsFailure)
        {
            return Result<Guid>.Failure(created.ErrorCode!, created.ErrorMessage!);
        }

        var model = created.Value!;
        var manufacturer = await repository.GetManufacturerAsync(model.ManufacturerId, cancellationToken);
        if (manufacturer is null)
        {
            return Result<Guid>.Failure(AircraftModelErrors.ManufacturerNotFound, AircraftModelErrors.ManufacturerNotFoundMessage);
        }

        if (!manufacturer.IsActive)
        {
            return Result<Guid>.Failure(AircraftModelErrors.ManufacturerInactive, AircraftModelErrors.ManufacturerInactiveMessage);
        }

        if (await repository.ModelCodeExistsAsync(model.ManufacturerId, model.ModelCode, excludeId: null, cancellationToken))
        {
            return Result<Guid>.Failure(AircraftModelErrors.DuplicateModelCode, AircraftModelErrors.DuplicateModelCodeMessage);
        }

        var saved = await repository.AddAsync(model, cancellationToken);

        return saved.IsSuccess
            ? Result<Guid>.Success(model.Id)
            : Result<Guid>.Failure(saved.ErrorCode!, saved.ErrorMessage!);
    }

    public async Task<Result> UpdateAsync(Guid id, UpdateAircraftModelDto dto, CancellationToken cancellationToken)
    {
        var model = await repository.GetForUpdateAsync(id, cancellationToken);
        if (model is null)
        {
            return Result.Failure(AircraftModelErrors.NotFound, AircraftModelErrors.NotFoundMessage);
        }

        var updated = model.Update(dto.Name, dto.ModelCode, dto.AircraftCategory, dto.IsActive);
        if (updated.IsFailure)
        {
            return updated;
        }

        if (await repository.ModelCodeExistsAsync(model.ManufacturerId, model.ModelCode, excludeId: id, cancellationToken))
        {
            return Result.Failure(AircraftModelErrors.DuplicateModelCode, AircraftModelErrors.DuplicateModelCodeMessage);
        }

        return await repository.UpdateAsync(model, dto.RowVersion, cancellationToken);
    }

    public async Task<Result> DeleteAsync(Guid id, byte[] rowVersion, CancellationToken cancellationToken)
    {
        var model = await repository.GetForUpdateAsync(id, cancellationToken);
        if (model is null)
        {
            return Result.Failure(AircraftModelErrors.NotFound, AircraftModelErrors.NotFoundMessage);
        }

        if (await repository.IsInUseAsync(id, cancellationToken))
        {
            return Result.Failure(AircraftModelErrors.InUse, AircraftModelErrors.InUseMessage);
        }

        return await repository.DeleteAsync(model, rowVersion, cancellationToken);
    }
}
