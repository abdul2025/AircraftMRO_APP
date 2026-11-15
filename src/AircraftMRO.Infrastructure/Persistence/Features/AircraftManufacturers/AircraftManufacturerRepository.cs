using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.AircraftManufacturers;
using AircraftMRO.Application.Features.AircraftManufacturers.DTOs;
using AircraftMRO.Application.Features.AircraftManufacturers.Ports;
using AircraftMRO.Domain.Common.Results;
using AircraftMRO.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AircraftMRO.Infrastructure.Persistence.Features.AircraftManufacturers;

internal sealed class AircraftManufacturerRepository(AircraftMroDbContext dbContext) : IAircraftManufacturerRepository
{
    public const int MaxCountryOptions = 1000;

    private static readonly Dictionary<string, (string Code, string Message)> UniqueIndexErrors = new()
    {
        [AircraftManufacturerConfiguration.CodeIndexName] =
            (AircraftManufacturerErrors.DuplicateCode, AircraftManufacturerErrors.DuplicateCodeMessage)
    };

    public async Task<PagedResponse<AircraftManufacturerListItemDto>> ListAsync(
        AircraftManufacturerListFilter filter,
        PagedRequest request,
        CancellationToken cancellationToken)
    {
        var manufacturers = dbContext.AircraftManufacturers.AsNoTracking();
        if (filter.IsActive is { } isActive)
        {
            manufacturers = manufacturers.Where(manufacturer => manufacturer.IsActive == isActive);
        }

        // A left join, because a manufacturer carried over from free text may have no country yet.
        var rows =
            from manufacturer in manufacturers
            join country in dbContext.Countries on manufacturer.CountryId equals (Guid?)country.Id into countries
            from country in countries.DefaultIfEmpty()
            select new
            {
                manufacturer.Id,
                manufacturer.Name,
                manufacturer.Code,
                CountryName = country == null ? null : country.Name,
                CountryCode = country == null ? null : country.Code,
                manufacturer.IsActive
            };

        if (filter.Search is { } search)
        {
            // EF Core escapes LIKE wildcards in the term; the column collation makes it case-insensitive.
            rows = rows.Where(row =>
                row.Name.Contains(search)
                || row.Code.Contains(search)
                || (row.CountryName != null && row.CountryName.Contains(search)));
        }

        var totalCount = await rows.CountAsync(cancellationToken);
        var items = await rows
            .OrderBy(row => row.Name)
            .ThenBy(row => row.Id)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .Select(row => new AircraftManufacturerListItemDto(
                row.Id, row.Name, row.Code, row.CountryName, row.CountryCode, row.IsActive))
            .ToListAsync(cancellationToken);

        return new PagedResponse<AircraftManufacturerListItemDto>(items, request.Page, request.PageSize, totalCount);
    }

    public Task<AircraftManufacturerDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        (from manufacturer in dbContext.AircraftManufacturers.AsNoTracking()
         join country in dbContext.Countries on manufacturer.CountryId equals (Guid?)country.Id into countries
         from country in countries.DefaultIfEmpty()
         where manufacturer.Id == id
         select new AircraftManufacturerDto(
             manufacturer.Id,
             manufacturer.Name,
             manufacturer.Code,
             manufacturer.CountryId,
             country == null ? null : country.Name,
             country == null ? null : country.Code,
             manufacturer.IsActive,
             manufacturer.CreatedAtUtc,
             manufacturer.CreatedBy,
             manufacturer.UpdatedAtUtc,
             manufacturer.UpdatedBy,
             EF.Property<byte[]>(manufacturer, AircraftMroDbContext.RowVersionPropertyName)))
        .SingleOrDefaultAsync(cancellationToken);

    public Task<AircraftManufacturer?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.AircraftManufacturers.SingleOrDefaultAsync(manufacturer => manufacturer.Id == id, cancellationToken);

    public Task<CountryOptionDto?> GetCountryAsync(Guid countryId, CancellationToken cancellationToken) =>
        dbContext.Countries
            .AsNoTracking()
            .Where(country => country.Id == countryId)
            .Select(country => new CountryOptionDto(country.Id, country.Name, country.Code, country.IsActive))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<CountryOptionDto>> ListCountryOptionsAsync(
        Guid? includeId,
        CancellationToken cancellationToken) =>
        await dbContext.Countries
            .AsNoTracking()
            .Where(country => country.IsActive || country.Id == includeId)
            .OrderBy(country => country.Name)
            .Take(MaxCountryOptions)
            .Select(country => new CountryOptionDto(country.Id, country.Name, country.Code, country.IsActive))
            .ToListAsync(cancellationToken);

    public Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken cancellationToken) =>
        dbContext.AircraftManufacturers
            .AsNoTracking()
            .AnyAsync(
                manufacturer => manufacturer.Code == code && (excludeId == null || manufacturer.Id != excludeId),
                cancellationToken);

    public async Task<bool> IsInUseAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.AircraftModels.AsNoTracking().AnyAsync(model => model.ManufacturerId == id, cancellationToken)
        || await dbContext.Aircraft.AsNoTracking().AnyAsync(aircraft => aircraft.ManufacturerId == id, cancellationToken);

    public Task<Result> AddAsync(AircraftManufacturer manufacturer, CancellationToken cancellationToken)
    {
        dbContext.AircraftManufacturers.Add(manufacturer);
        return SaveAsync(cancellationToken);
    }

    public Task<Result> UpdateAsync(
        AircraftManufacturer manufacturer,
        byte[] expectedRowVersion,
        CancellationToken cancellationToken)
    {
        SetExpectedRowVersion(manufacturer, expectedRowVersion);
        return SaveAsync(cancellationToken);
    }

    public Task<Result> DeleteAsync(
        AircraftManufacturer manufacturer,
        byte[] expectedRowVersion,
        CancellationToken cancellationToken)
    {
        SetExpectedRowVersion(manufacturer, expectedRowVersion);
        dbContext.AircraftManufacturers.Remove(manufacturer);
        return SaveAsync(cancellationToken);
    }

    private void SetExpectedRowVersion(AircraftManufacturer manufacturer, byte[] expectedRowVersion) =>
        dbContext.Entry(manufacturer).Property(AircraftMroDbContext.RowVersionPropertyName).OriginalValue = expectedRowVersion;

    private Task<Result> SaveAsync(CancellationToken cancellationToken) =>
        SaveChangesResult.SaveAsync(
            dbContext,
            (AircraftManufacturerErrors.ConcurrencyConflict, AircraftManufacturerErrors.ConcurrencyConflictMessage),
            UniqueIndexErrors,
            cancellationToken);
}
