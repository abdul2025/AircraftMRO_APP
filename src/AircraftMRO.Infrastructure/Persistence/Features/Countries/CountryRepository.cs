using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.Countries;
using AircraftMRO.Application.Features.Countries.DTOs;
using AircraftMRO.Application.Features.Countries.Ports;
using AircraftMRO.Domain.Common.Results;
using AircraftMRO.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AircraftMRO.Infrastructure.Persistence.Features.Countries;

internal sealed class CountryRepository(AircraftMroDbContext dbContext) : ICountryRepository
{
    private static readonly Dictionary<string, (string Code, string Message)> UniqueIndexErrors = new()
    {
        [CountryConfiguration.CodeIndexName] = (CountryErrors.DuplicateCode, CountryErrors.DuplicateCodeMessage)
    };

    public async Task<PagedResponse<CountryListItemDto>> ListAsync(
        CountryListFilter filter,
        PagedRequest request,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Countries.AsNoTracking();

        if (filter.IsActive is { } isActive)
        {
            query = query.Where(country => country.IsActive == isActive);
        }

        if (filter.Search is { } search)
        {
            // EF Core escapes LIKE wildcards in the term; the column collation makes it case-insensitive.
            query = query.Where(country => country.Name.Contains(search) || country.Code.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(country => country.Name)
            .ThenBy(country => country.Id)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .Select(country => new CountryListItemDto(country.Id, country.Name, country.Code, country.IsActive))
            .ToListAsync(cancellationToken);

        return new PagedResponse<CountryListItemDto>(items, request.Page, request.PageSize, totalCount);
    }

    public Task<CountryDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Countries
            .AsNoTracking()
            .Where(country => country.Id == id)
            .Select(country => new CountryDto(
                country.Id,
                country.Name,
                country.Code,
                country.IsActive,
                country.CreatedAtUtc,
                country.CreatedBy,
                country.UpdatedAtUtc,
                country.UpdatedBy,
                EF.Property<byte[]>(country, AircraftMroDbContext.RowVersionPropertyName)))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<Country?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Countries.SingleOrDefaultAsync(country => country.Id == id, cancellationToken);

    public Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken cancellationToken) =>
        dbContext.Countries
            .AsNoTracking()
            .AnyAsync(
                country => country.Code == code && (excludeId == null || country.Id != excludeId),
                cancellationToken);

    public Task<bool> IsInUseAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.AircraftManufacturers.AsNoTracking().AnyAsync(manufacturer => manufacturer.CountryId == id, cancellationToken);

    public Task<Result> AddAsync(Country country, CancellationToken cancellationToken)
    {
        dbContext.Countries.Add(country);
        return SaveAsync(cancellationToken);
    }

    public Task<Result> UpdateAsync(Country country, byte[] expectedRowVersion, CancellationToken cancellationToken)
    {
        SetExpectedRowVersion(country, expectedRowVersion);
        return SaveAsync(cancellationToken);
    }

    public Task<Result> DeleteAsync(Country country, byte[] expectedRowVersion, CancellationToken cancellationToken)
    {
        SetExpectedRowVersion(country, expectedRowVersion);
        dbContext.Countries.Remove(country);
        return SaveAsync(cancellationToken);
    }

    private void SetExpectedRowVersion(Country country, byte[] expectedRowVersion) =>
        dbContext.Entry(country).Property(AircraftMroDbContext.RowVersionPropertyName).OriginalValue = expectedRowVersion;

    private Task<Result> SaveAsync(CancellationToken cancellationToken) =>
        SaveChangesResult.SaveAsync(
            dbContext,
            (CountryErrors.ConcurrencyConflict, CountryErrors.ConcurrencyConflictMessage),
            UniqueIndexErrors,
            cancellationToken);
}
