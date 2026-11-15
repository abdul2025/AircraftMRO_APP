using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.Countries.DTOs;
using AircraftMRO.Application.Features.Countries.Interfaces;
using AircraftMRO.Application.Features.Countries.Ports;
using AircraftMRO.Domain.Common.Results;
using AircraftMRO.Domain.Entities;

namespace AircraftMRO.Application.Features.Countries;

public sealed class CountryService(ICountryRepository repository) : ICountryService
{
    public async Task<Result<PagedResponse<CountryListItemDto>>> ListAsync(
        CountryListFilter filter,
        PagedRequest request,
        CancellationToken cancellationToken) =>
        Result<PagedResponse<CountryListItemDto>>.Success(
            await repository.ListAsync(filter, request, cancellationToken));

    public async Task<Result<CountryDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var country = await repository.GetByIdAsync(id, cancellationToken);

        return country is null
            ? Result<CountryDto>.Failure(CountryErrors.NotFound, CountryErrors.NotFoundMessage)
            : Result<CountryDto>.Success(country);
    }

    public async Task<Result<Guid>> CreateAsync(CreateCountryDto dto, CancellationToken cancellationToken)
    {
        var created = Country.Create(dto.Name, dto.Code, dto.IsActive);
        if (created.IsFailure)
        {
            return Result<Guid>.Failure(created.ErrorCode!, created.ErrorMessage!);
        }

        var country = created.Value!;
        if (await repository.CodeExistsAsync(country.Code, excludeId: null, cancellationToken))
        {
            return Result<Guid>.Failure(CountryErrors.DuplicateCode, CountryErrors.DuplicateCodeMessage);
        }

        var saved = await repository.AddAsync(country, cancellationToken);

        return saved.IsSuccess
            ? Result<Guid>.Success(country.Id)
            : Result<Guid>.Failure(saved.ErrorCode!, saved.ErrorMessage!);
    }

    public async Task<Result> UpdateAsync(Guid id, UpdateCountryDto dto, CancellationToken cancellationToken)
    {
        var country = await repository.GetForUpdateAsync(id, cancellationToken);
        if (country is null)
        {
            return Result.Failure(CountryErrors.NotFound, CountryErrors.NotFoundMessage);
        }

        var updated = country.Update(dto.Name, dto.Code, dto.IsActive);
        if (updated.IsFailure)
        {
            return updated;
        }

        if (await repository.CodeExistsAsync(country.Code, excludeId: id, cancellationToken))
        {
            return Result.Failure(CountryErrors.DuplicateCode, CountryErrors.DuplicateCodeMessage);
        }

        return await repository.UpdateAsync(country, dto.RowVersion, cancellationToken);
    }

    public async Task<Result> DeleteAsync(Guid id, byte[] rowVersion, CancellationToken cancellationToken)
    {
        var country = await repository.GetForUpdateAsync(id, cancellationToken);
        if (country is null)
        {
            return Result.Failure(CountryErrors.NotFound, CountryErrors.NotFoundMessage);
        }

        if (await repository.IsInUseAsync(id, cancellationToken))
        {
            return Result.Failure(CountryErrors.InUse, CountryErrors.InUseMessage);
        }

        return await repository.DeleteAsync(country, rowVersion, cancellationToken);
    }
}
