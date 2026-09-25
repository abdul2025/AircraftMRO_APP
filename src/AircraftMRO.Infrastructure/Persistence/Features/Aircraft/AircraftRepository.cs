using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.Aircraft;
using AircraftMRO.Application.Features.Aircraft.DTOs;
using AircraftMRO.Application.Features.Aircraft.Ports;
using AircraftMRO.Domain.Common.Results;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using AircraftEntity = AircraftMRO.Domain.Entities.Aircraft;

namespace AircraftMRO.Infrastructure.Persistence.Features.Aircraft;

internal sealed class AircraftRepository(AircraftMroDbContext dbContext) : IAircraftRepository
{
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    public async Task<PagedResponse<AircraftListItemDto>> ListAsync(
        AircraftListFilter filter,
        PagedRequest request,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Aircraft.AsNoTracking();

        if (filter.Status is { } status)
        {
            query = query.Where(aircraft => aircraft.Status == status);
        }

        if (filter.Search is { } search)
        {
            // EF Core escapes LIKE wildcards in the term; the column collation makes it case-insensitive.
            query = query.Where(aircraft =>
                aircraft.RegistrationNumber.Contains(search)
                || aircraft.Manufacturer.Contains(search)
                || aircraft.Model.Contains(search)
                || aircraft.SerialNumber.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(aircraft => aircraft.RegistrationNumber)
            .ThenBy(aircraft => aircraft.Id)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .Select(aircraft => new AircraftListItemDto(
                aircraft.Id,
                aircraft.RegistrationNumber,
                aircraft.Manufacturer,
                aircraft.Model,
                aircraft.YearOfManufacture,
                aircraft.TotalFlightHours,
                aircraft.Status))
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
        dbContext.Aircraft
            .AsNoTracking()
            .Where(aircraft => aircraft.Id == id)
            .Select(aircraft => new AircraftDto(
                aircraft.Id,
                aircraft.RegistrationNumber,
                aircraft.Manufacturer,
                aircraft.Model,
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
        string manufacturer,
        string serialNumber,
        Guid? excludeId,
        CancellationToken cancellationToken) =>
        dbContext.Aircraft
            .AsNoTracking()
            .AnyAsync(
                aircraft => aircraft.Manufacturer == manufacturer
                    && aircraft.SerialNumber == serialNumber
                    && (excludeId == null || aircraft.Id != excludeId),
                cancellationToken);

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
