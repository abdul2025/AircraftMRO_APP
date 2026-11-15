using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.AircraftModels;
using AircraftMRO.Application.Features.AircraftModels.DTOs;
using AircraftMRO.Application.Features.AircraftModels.Ports;
using AircraftMRO.Domain.Common.Results;
using AircraftMRO.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AircraftMRO.Infrastructure.Persistence.Features.AircraftModels;

internal sealed class AircraftModelRepository(AircraftMroDbContext dbContext) : IAircraftModelRepository
{
    public const int MaxManufacturerOptions = 1000;

    private static readonly Dictionary<string, (string Code, string Message)> UniqueIndexErrors = new()
    {
        [AircraftModelConfiguration.ModelCodeIndexName] =
            (AircraftModelErrors.DuplicateModelCode, AircraftModelErrors.DuplicateModelCodeMessage)
    };

    public async Task<PagedResponse<AircraftModelListItemDto>> ListAsync(
        AircraftModelListFilter filter,
        PagedRequest request,
        CancellationToken cancellationToken)
    {
        var models = dbContext.AircraftModels.AsNoTracking();
        if (filter.ManufacturerId is { } manufacturerId)
        {
            models = models.Where(model => model.ManufacturerId == manufacturerId);
        }

        if (filter.IsActive is { } isActive)
        {
            models = models.Where(model => model.IsActive == isActive);
        }

        var rows =
            from model in models
            join manufacturer in dbContext.AircraftManufacturers on model.ManufacturerId equals manufacturer.Id
            select new { Model = model, ManufacturerName = manufacturer.Name };

        if (filter.Search is { } search)
        {
            // EF Core escapes LIKE wildcards in the term; the column collation makes it case-insensitive.
            rows = rows.Where(row =>
                row.Model.Name.Contains(search)
                || row.Model.ModelCode.Contains(search)
                || row.ManufacturerName.Contains(search));
        }

        var totalCount = await rows.CountAsync(cancellationToken);
        var items = await rows
            .OrderBy(row => row.ManufacturerName)
            .ThenBy(row => row.Model.Name)
            .ThenBy(row => row.Model.Id)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .Select(row => new AircraftModelListItemDto(
                row.Model.Id,
                row.Model.ManufacturerId,
                row.ManufacturerName,
                row.Model.Name,
                row.Model.ModelCode,
                row.Model.AircraftCategory,
                row.Model.IsActive))
            .ToListAsync(cancellationToken);

        return new PagedResponse<AircraftModelListItemDto>(items, request.Page, request.PageSize, totalCount);
    }

    public Task<AircraftModelDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        (from model in dbContext.AircraftModels.AsNoTracking()
         join manufacturer in dbContext.AircraftManufacturers on model.ManufacturerId equals manufacturer.Id
         where model.Id == id
         select new AircraftModelDto(
             model.Id,
             model.ManufacturerId,
             manufacturer.Name,
             model.Name,
             model.ModelCode,
             model.AircraftCategory,
             model.IsActive,
             model.CreatedAtUtc,
             model.CreatedBy,
             model.UpdatedAtUtc,
             model.UpdatedBy,
             EF.Property<byte[]>(model, AircraftMroDbContext.RowVersionPropertyName)))
        .SingleOrDefaultAsync(cancellationToken);

    public Task<AircraftModel?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.AircraftModels.SingleOrDefaultAsync(model => model.Id == id, cancellationToken);

    public Task<ManufacturerOptionDto?> GetManufacturerAsync(Guid manufacturerId, CancellationToken cancellationToken) =>
        dbContext.AircraftManufacturers
            .AsNoTracking()
            .Where(manufacturer => manufacturer.Id == manufacturerId)
            .Select(manufacturer => new ManufacturerOptionDto(manufacturer.Id, manufacturer.Name, manufacturer.IsActive))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ManufacturerOptionDto>> ListManufacturerOptionsAsync(CancellationToken cancellationToken) =>
        await dbContext.AircraftManufacturers
            .AsNoTracking()
            .OrderBy(manufacturer => manufacturer.Name)
            .Take(MaxManufacturerOptions)
            .Select(manufacturer => new ManufacturerOptionDto(manufacturer.Id, manufacturer.Name, manufacturer.IsActive))
            .ToListAsync(cancellationToken);

    public Task<bool> ModelCodeExistsAsync(
        Guid manufacturerId,
        string modelCode,
        Guid? excludeId,
        CancellationToken cancellationToken) =>
        dbContext.AircraftModels
            .AsNoTracking()
            .AnyAsync(
                model => model.ManufacturerId == manufacturerId
                    && model.ModelCode == modelCode
                    && (excludeId == null || model.Id != excludeId),
                cancellationToken);

    public Task<bool> IsInUseAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Aircraft.AsNoTracking().AnyAsync(aircraft => aircraft.ModelId == id, cancellationToken);

    public Task<Result> AddAsync(AircraftModel model, CancellationToken cancellationToken)
    {
        dbContext.AircraftModels.Add(model);
        return SaveAsync(cancellationToken);
    }

    public Task<Result> UpdateAsync(AircraftModel model, byte[] expectedRowVersion, CancellationToken cancellationToken)
    {
        SetExpectedRowVersion(model, expectedRowVersion);
        return SaveAsync(cancellationToken);
    }

    public Task<Result> DeleteAsync(AircraftModel model, byte[] expectedRowVersion, CancellationToken cancellationToken)
    {
        SetExpectedRowVersion(model, expectedRowVersion);
        dbContext.AircraftModels.Remove(model);
        return SaveAsync(cancellationToken);
    }

    private void SetExpectedRowVersion(AircraftModel model, byte[] expectedRowVersion) =>
        dbContext.Entry(model).Property(AircraftMroDbContext.RowVersionPropertyName).OriginalValue = expectedRowVersion;

    private Task<Result> SaveAsync(CancellationToken cancellationToken) =>
        SaveChangesResult.SaveAsync(
            dbContext,
            (AircraftModelErrors.ConcurrencyConflict, AircraftModelErrors.ConcurrencyConflictMessage),
            UniqueIndexErrors,
            cancellationToken);
}
