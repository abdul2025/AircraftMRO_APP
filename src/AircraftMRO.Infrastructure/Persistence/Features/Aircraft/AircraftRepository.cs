using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.Aircraft;
using AircraftMRO.Application.Features.Aircraft.DTOs;
using AircraftMRO.Application.Features.Aircraft.Ports;
using AircraftMRO.Domain.Common.Results;
using AircraftMRO.Infrastructure.Persistence.Features.WorkOrders;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using AircraftEntity = AircraftMRO.Domain.Entities.Aircraft;

namespace AircraftMRO.Infrastructure.Persistence.Features.Aircraft;

internal sealed class AircraftRepository(AircraftMroDbContext dbContext) : IAircraftRepository
{
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;
    public const int MaxFormOptions = 1000;

    public async Task<PagedResponse<AircraftListItemDto>> ListAsync(
        AircraftListFilter filter,
        PagedRequest request,
        CancellationToken cancellationToken)
    {
        var aircraftQuery = dbContext.Aircraft.AsNoTracking();

        if (filter.Status is { } status)
        {
            aircraftQuery = aircraftQuery.Where(aircraft => aircraft.Status == status);
        }

        var query =
            from aircraft in aircraftQuery
            join manufacturer in dbContext.AircraftManufacturers on aircraft.ManufacturerId equals manufacturer.Id
            join model in dbContext.AircraftModels on aircraft.ModelId equals model.Id
            select new { Aircraft = aircraft, ManufacturerName = manufacturer.Name, ModelName = model.Name };

        if (filter.Search is { } search)
        {
            // EF Core escapes LIKE wildcards in the term; the column collation makes it case-insensitive.
            query = query.Where(row =>
                row.Aircraft.RegistrationNumber.Contains(search)
                || row.ManufacturerName.Contains(search)
                || row.ModelName.Contains(search)
                || row.Aircraft.SerialNumber.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(row => row.Aircraft.RegistrationNumber)
            .ThenBy(row => row.Aircraft.Id)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .Select(row => new AircraftListItemDto(
                row.Aircraft.Id,
                row.Aircraft.RegistrationNumber,
                row.ManufacturerName,
                row.ModelName,
                row.Aircraft.YearOfManufacture,
                row.Aircraft.TotalFlightHours,
                row.Aircraft.Status))
            .ToListAsync(cancellationToken);

        return new PagedResponse<AircraftListItemDto>(items, request.Page, request.PageSize, totalCount);
    }

    public async Task<AircraftStatisticsDto> GetStatisticsAsync(CancellationToken cancellationToken)
    {
        var groups = await dbContext.Aircraft
            .AsNoTracking()
            .GroupBy(aircraft => aircraft.Status)
            .Select(group => new
            {
                Status = group.Key,
                Count = group.Count(),
                FlightHours = group.Sum(aircraft => aircraft.TotalFlightHours)
            })
            .ToListAsync(cancellationToken);

        return new AircraftStatisticsDto(
            groups.Sum(group => group.Count),
            groups.ToDictionary(group => group.Status, group => group.Count),
            groups.Sum(group => group.FlightHours));
    }

    public Task<AircraftDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        (from aircraft in dbContext.Aircraft.AsNoTracking()
         join manufacturer in dbContext.AircraftManufacturers on aircraft.ManufacturerId equals manufacturer.Id
         join model in dbContext.AircraftModels on aircraft.ModelId equals model.Id
         where aircraft.Id == id
         select new AircraftDto(
             aircraft.Id,
             aircraft.RegistrationNumber,
             aircraft.ManufacturerId,
             manufacturer.Name,
             aircraft.ModelId,
             model.Name,
             aircraft.SerialNumber,
             aircraft.YearOfManufacture,
             aircraft.TotalFlightHours,
             aircraft.Status,
             aircraft.CreatedAtUtc,
             aircraft.CreatedBy,
             aircraft.UpdatedAtUtc,
             aircraft.UpdatedBy,
             EF.Property<byte[]>(aircraft, AircraftMroDbContext.RowVersionPropertyName)))
        .SingleOrDefaultAsync(cancellationToken);

    public Task<AircraftEntity?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Aircraft.SingleOrDefaultAsync(aircraft => aircraft.Id == id, cancellationToken);

    public Task<bool> RegistrationNumberExistsAsync(
        string registrationNumber,
        Guid? excludeId,
        CancellationToken cancellationToken) =>
        dbContext.Aircraft
            .AsNoTracking()
            .AnyAsync(
                aircraft => aircraft.RegistrationNumber == registrationNumber
                    && (excludeId == null || aircraft.Id != excludeId),
                cancellationToken);

    public Task<bool> SerialNumberExistsAsync(
        Guid manufacturerId,
        string serialNumber,
        Guid? excludeId,
        CancellationToken cancellationToken) =>
        dbContext.Aircraft
            .AsNoTracking()
            .AnyAsync(
                aircraft => aircraft.ManufacturerId == manufacturerId
                    && aircraft.SerialNumber == serialNumber
                    && (excludeId == null || aircraft.Id != excludeId),
                cancellationToken);

    public Task<AircraftModelReference?> GetModelReferenceAsync(Guid modelId, CancellationToken cancellationToken) =>
        (from model in dbContext.AircraftModels.AsNoTracking()
         join manufacturer in dbContext.AircraftManufacturers on model.ManufacturerId equals manufacturer.Id
         where model.Id == modelId
         select new AircraftModelReference(model.Id, model.ManufacturerId, model.IsActive, manufacturer.IsActive))
        .SingleOrDefaultAsync(cancellationToken);

    public async Task<AircraftFormOptionsDto> ListFormOptionsAsync(
        Guid? currentManufacturerId,
        Guid? currentModelId,
        CancellationToken cancellationToken)
    {
        var manufacturers = await dbContext.AircraftManufacturers
            .AsNoTracking()
            .Where(manufacturer => manufacturer.IsActive || manufacturer.Id == currentManufacturerId)
            .OrderBy(manufacturer => manufacturer.Name)
            .Take(MaxFormOptions)
            .Select(manufacturer => new AircraftManufacturerOptionDto(manufacturer.Id, manufacturer.Name, manufacturer.IsActive))
            .ToListAsync(cancellationToken);

        // A model is offered when it and its manufacturer are active, or when it is the current one.
        var models = await (
                from model in dbContext.AircraftModels.AsNoTracking()
                join manufacturer in dbContext.AircraftManufacturers on model.ManufacturerId equals manufacturer.Id
                where (model.IsActive && manufacturer.IsActive) || model.Id == currentModelId
                orderby manufacturer.Name, model.Name
                select new AircraftModelOptionDto(model.Id, model.ManufacturerId, model.Name, model.ModelCode, model.IsActive))
            .Take(MaxFormOptions)
            .ToListAsync(cancellationToken);

        return new AircraftFormOptionsDto(manufacturers, models);
    }

    public Task<OpenWorkOrderCounts> GetOpenWorkOrderCountsAsync(Guid aircraftId, CancellationToken cancellationToken) =>
        dbContext.WorkOrders
            .AsNoTracking()
            .Where(workOrder => workOrder.AircraftId == aircraftId)
            .CountOpenAsync(cancellationToken);

    public Task<Result> AddAsync(AircraftEntity aircraft, CancellationToken cancellationToken)
    {
        dbContext.Aircraft.Add(aircraft);
        return SaveAsync(cancellationToken);
    }

    public Task<Result> UpdateAsync(
        AircraftEntity aircraft,
        byte[] expectedRowVersion,
        CancellationToken cancellationToken)
    {
        SetExpectedRowVersion(aircraft, expectedRowVersion);
        return SaveAsync(cancellationToken);
    }

    public Task<Result> DeleteAsync(
        AircraftEntity aircraft,
        byte[] expectedRowVersion,
        CancellationToken cancellationToken)
    {
        SetExpectedRowVersion(aircraft, expectedRowVersion);
        dbContext.Aircraft.Remove(aircraft);
        return SaveAsync(cancellationToken);
    }

    private void SetExpectedRowVersion(AircraftEntity aircraft, byte[] expectedRowVersion) =>
        dbContext.Entry(aircraft).Property(AircraftMroDbContext.RowVersionPropertyName).OriginalValue =
            expectedRowVersion;

    private async Task<Result> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(AircraftErrors.ConcurrencyConflict, AircraftErrors.ConcurrencyConflictMessage);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation } sql)
        {
            if (sql.Message.Contains(AircraftConfiguration.RegistrationNumberIndexName, StringComparison.Ordinal))
            {
                return Result.Failure(AircraftErrors.DuplicateRegistration, AircraftErrors.DuplicateRegistrationMessage);
            }

            if (sql.Message.Contains(AircraftConfiguration.SerialNumberIndexName, StringComparison.Ordinal))
            {
                return Result.Failure(AircraftErrors.DuplicateSerialNumber, AircraftErrors.DuplicateSerialNumberMessage);
            }

            throw;
        }
    }
}
