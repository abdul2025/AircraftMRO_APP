using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.Aircraft;
using AircraftMRO.Application.Features.Aircraft.DTOs;
using AircraftMRO.Domain.Common.Entities;
using AircraftMRO.Domain.Entities;
using AircraftMRO.Domain.Enums.Aircraft;
using AircraftMRO.Infrastructure.Persistence;
using AircraftMRO.Infrastructure.Persistence.Features.Aircraft;
using AircraftMRO.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace AircraftMRO.Tests.Integration;

[Collection(SqlServerCollection.Name)]
public sealed class AircraftPersistenceIntegrationTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private readonly TestCurrentUser _user = new("user-1");
    private readonly FakeTimeProvider _clock = new();
    private string _connectionString = null!;

    public async Task InitializeAsync() => _connectionString = await fixture.CreateDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Insert_stamps_created_values_only()
    {
        var aircraft = await AddAsync(AircraftTestData.NewAircraft());

        await using var context = NewContext();
        var stored = await context.Aircraft.AsNoTracking().SingleAsync(a => a.Id == aircraft.Id);
        Assert.Equal(_clock.Now, stored.CreatedAtUtc);
        Assert.Equal("user-1", stored.CreatedBy);
        Assert.Null(stored.UpdatedAtUtc);
        Assert.Null(stored.UpdatedBy);
        Assert.False(stored.IsDeleted);
    }

    [Fact]
    public async Task Update_stamps_updated_values_and_cannot_change_created_values()
    {
        var aircraft = await AddAsync(AircraftTestData.NewAircraft());
        var createdAt = _clock.Now;
        _clock.Now = createdAt.AddHours(3);
        _user.UserId = "user-2";

        await using (var context = NewContext())
        {
            var tracked = await context.Aircraft.SingleAsync(a => a.Id == aircraft.Id);
            tracked.Update("HZ-ABC", "Airbus", "A320-214", "5123", 2012, 31300m, AircraftStatus.InMaintenance, 2026);
            var entry = context.Entry(tracked);
            entry.Property(a => a.CreatedAtUtc).CurrentValue = DateTimeOffset.UnixEpoch;
            entry.Property(a => a.CreatedBy).CurrentValue = "attacker";
            await context.SaveChangesAsync();
        }

        await using var verify = NewContext();
        var stored = await verify.Aircraft.AsNoTracking().SingleAsync(a => a.Id == aircraft.Id);
        Assert.Equal(AircraftStatus.InMaintenance, stored.Status);
        Assert.Equal(createdAt, stored.CreatedAtUtc);
        Assert.Equal("user-1", stored.CreatedBy);
        Assert.Equal(_clock.Now, stored.UpdatedAtUtc);
        Assert.Equal("user-2", stored.UpdatedBy);
    }

    [Fact]
    public async Task Delete_is_soft_and_hidden_from_queries()
    {
        var aircraft = await AddAsync(AircraftTestData.NewAircraft());
        var rowVersion = await GetRowVersionAsync(aircraft.Id);
        _user.UserId = "deleter";

        await using (var context = NewContext())
        {
            var repository = new AircraftRepository(context);
            var tracked = (await repository.GetForUpdateAsync(aircraft.Id, CancellationToken.None))!;
            Assert.True((await repository.DeleteAsync(tracked, rowVersion, CancellationToken.None)).IsSuccess);
        }

        await using var verify = NewContext();
        Assert.Null(await new AircraftRepository(verify).GetByIdAsync(aircraft.Id, CancellationToken.None));
        var stored = await verify.Aircraft.IgnoreQueryFilters().AsNoTracking().SingleAsync(a => a.Id == aircraft.Id);
        Assert.True(stored.IsDeleted);
        Assert.Equal(_clock.Now, stored.DeletedAtUtc);
        Assert.Equal("deleter", stored.DeletedBy);
        Assert.Equal("user-1", stored.CreatedBy);
    }

    [Fact]
    public async Task Registration_can_be_reused_after_soft_delete()
    {
        var registration = AircraftTestData.UniqueRegistration();
        var first = await AddAsync(AircraftTestData.NewAircraft(registration, "S1"));
        await DeleteAsync(first.Id);

        await using var context = NewContext();
        var result = await new AircraftRepository(context)
            .AddAsync(AircraftTestData.NewAircraft(registration, "S1"), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Stale_row_version_rejects_update_and_delete()
    {
        var aircraft = await AddAsync(AircraftTestData.NewAircraft());
        var staleRowVersion = await GetRowVersionAsync(aircraft.Id);

        await using (var first = NewContext())
        {
            var tracked = await first.Aircraft.SingleAsync(a => a.Id == aircraft.Id);
            tracked.Update("HZ-ABC", "Airbus", "A320-214", "5123", 2012, 1m, AircraftStatus.Active, 2026);
            await first.SaveChangesAsync();
        }

        await using (var second = NewContext())
        {
            var repository = new AircraftRepository(second);
            var tracked = (await repository.GetForUpdateAsync(aircraft.Id, CancellationToken.None))!;
            tracked.Update("HZ-ABC", "Airbus", "A320-214", "5123", 2012, 2m, AircraftStatus.Active, 2026);
            var result = await repository.UpdateAsync(tracked, staleRowVersion, CancellationToken.None);
            Assert.Equal(AircraftErrors.ConcurrencyConflict, result.ErrorCode);
        }

        await using (var third = NewContext())
        {
            var repository = new AircraftRepository(third);
            var tracked = (await repository.GetForUpdateAsync(aircraft.Id, CancellationToken.None))!;
            var result = await repository.DeleteAsync(tracked, staleRowVersion, CancellationToken.None);
            Assert.Equal(AircraftErrors.ConcurrencyConflict, result.ErrorCode);
        }

        await using var verify = NewContext();
        var stored = await verify.Aircraft.AsNoTracking().SingleAsync(a => a.Id == aircraft.Id);
        Assert.Equal(1m, stored.TotalFlightHours);
    }

    [Fact]
    public async Task Unique_indexes_reject_duplicates_that_bypass_the_pre_check()
    {
        var registration = AircraftTestData.UniqueRegistration();
        await AddAsync(AircraftTestData.NewAircraft(registration, "S-A"));

        await using var context = NewContext();
        var repository = new AircraftRepository(context);
        var duplicateRegistration = await repository.AddAsync(
            AircraftTestData.NewAircraft(registration, "S-B"), CancellationToken.None);
        Assert.Equal(AircraftErrors.DuplicateRegistration, duplicateRegistration.ErrorCode);

        await using var context2 = NewContext();
        var duplicateSerial = await new AircraftRepository(context2).AddAsync(
            AircraftTestData.NewAircraft(AircraftTestData.UniqueRegistration(), "S-A"), CancellationToken.None);
        Assert.Equal(AircraftErrors.DuplicateSerialNumber, duplicateSerial.ErrorCode);
    }

    [Fact]
    public async Task List_pages_by_registration_and_excludes_deleted()
    {
        foreach (var registration in new[] { "PG-C", "PG-A", "PG-B", "PG-D" })
        {
            await AddAsync(AircraftTestData.NewAircraft(registration, registration));
        }
        var deleted = await AddAsync(AircraftTestData.NewAircraft("PG-AA", "PG-AA"));
        await DeleteAsync(deleted.Id);

        await using var context = NewContext();
        var repository = new AircraftRepository(context);
        var page1 = await repository.ListAsync(AircraftListFilter.None, new PagedRequest(1, 3), CancellationToken.None);
        var page2 = await repository.ListAsync(AircraftListFilter.None, new PagedRequest(2, 3), CancellationToken.None);

        Assert.Equal(4, page1.TotalCount);
        Assert.Equal(["PG-A", "PG-B", "PG-C"], page1.Items.Select(a => a.RegistrationNumber));
        Assert.Equal(["PG-D"], page2.Items.Select(a => a.RegistrationNumber));
        Assert.Equal(2, page1.TotalPages);
    }

    [Fact]
    public async Task Ids_are_assigned_on_add_in_sql_server_sort_order()
    {
        var inserted = new List<Guid>();
        for (var i = 0; i < 20; i++)
        {
            var aircraft = await AddAsync(AircraftTestData.NewAircraft($"SQ-{i:D2}", $"SQ-{i:D2}"));
            Assert.NotEqual(Guid.Empty, aircraft.Id);
            inserted.Add(aircraft.Id);
        }

        await using var context = NewContext();
        var orderedByDatabase = await context.Aircraft.AsNoTracking()
            .Where(a => inserted.Contains(a.Id))
            .OrderBy(a => a.Id)
            .Select(a => a.Id)
            .ToListAsync();

        Assert.Equal(inserted, orderedByDatabase);
    }

    [Fact]
    public async Task List_filters_by_status_and_searches_every_text_column()
    {
        await AddAsync(Aircraft.Create("FL-ONE", "Airbus", "A320", "MSN-111", 2012, 10m, AircraftStatus.Active, 2026).Value!);
        await AddAsync(Aircraft.Create("FL-TWO", "Boeing", "737-800", "MSN-222", 2014, 20m, AircraftStatus.Grounded, 2026).Value!);
        await AddAsync(Aircraft.Create("FL-3", "Embraer", "E190", "SN-50%", 2016, 30m, AircraftStatus.Grounded, 2026).Value!);

        await using var context = NewContext();
        var repository = new AircraftRepository(context);
        async Task<string[]> Find(string? search, AircraftStatus? status = null) =>
            (await repository.ListAsync(new AircraftListFilter(search, status), new PagedRequest(1, 100), CancellationToken.None))
                .Items.Select(a => a.RegistrationNumber).Order().ToArray();

        Assert.Equal(["FL-3", "FL-TWO"], await Find(null, AircraftStatus.Grounded));
        Assert.Equal(["FL-TWO"], await Find("fl-t"));        // registration, case-insensitive
        Assert.Equal(["FL-ONE"], await Find("airbus"));      // manufacturer
        Assert.Equal(["FL-TWO"], await Find("737"));         // model
        Assert.Equal(["FL-ONE"], await Find("MSN-111"));     // serial number
        Assert.Equal(["FL-TWO"], await Find("boeing", AircraftStatus.Grounded));
        Assert.Empty(await Find("boeing", AircraftStatus.Active));
        Assert.Equal(["FL-3"], await Find("50%"));           // LIKE wildcard is matched literally
        Assert.Equal(["FL-3"], await Find("%"));            // not every row: "%" is not a wildcard
    }

    [Fact]
    public async Task Statistics_count_live_aircraft_by_status_and_sum_flight_hours()
    {
        await AddAsync(Aircraft.Create("ST-1", "Airbus", "A320", "ST-1", 2012, 100.5m, AircraftStatus.Active, 2026).Value!);
        await AddAsync(Aircraft.Create("ST-2", "Airbus", "A320", "ST-2", 2012, 200m, AircraftStatus.Active, 2026).Value!);
        await AddAsync(Aircraft.Create("ST-3", "Airbus", "A320", "ST-3", 2012, 50m, AircraftStatus.InMaintenance, 2026).Value!);
        var deleted = await AddAsync(Aircraft.Create("ST-4", "Airbus", "A320", "ST-4", 2012, 999m, AircraftStatus.Grounded, 2026).Value!);
        await DeleteAsync(deleted.Id);

        await using var context = NewContext();
        var statistics = await new AircraftRepository(context).GetStatisticsAsync(CancellationToken.None);

        Assert.Equal(3, statistics.TotalCount);
        Assert.Equal(2, statistics.CountFor(AircraftStatus.Active));
        Assert.Equal(1, statistics.CountFor(AircraftStatus.InMaintenance));
        Assert.Equal(0, statistics.CountFor(AircraftStatus.Grounded));
        Assert.Equal(0, statistics.CountFor(AircraftStatus.Retired));
        Assert.Equal(350.5m, statistics.TotalFlightHours);
    }

    [Fact]
    public void Every_auditable_entity_gets_audit_columns_row_version_and_soft_delete_filter()
    {
        using var context = NewContext();
        var auditableTypes = context.Model.GetEntityTypes()
            .Where(type => typeof(AuditableEntity).IsAssignableFrom(type.ClrType))
            .ToList();

        Assert.NotEmpty(auditableTypes);
        foreach (var type in auditableTypes)
        {
            Assert.NotNull(type.FindProperty(nameof(AuditableEntity.CreatedAtUtc)));
            Assert.NotNull(type.FindProperty(nameof(AuditableEntity.DeletedBy)));
            Assert.True(type.FindProperty(AircraftMroDbContext.RowVersionPropertyName)?.IsConcurrencyToken);
            Assert.NotEmpty(type.GetDeclaredQueryFilters());
        }
    }

    private AircraftMroDbContext NewContext() => SqlServerFixture.CreateContext(_connectionString, _user, _clock);

    private async Task<Aircraft> AddAsync(Aircraft aircraft)
    {
        await using var context = NewContext();
        var result = await new AircraftRepository(context).AddAsync(aircraft, CancellationToken.None);
        Assert.True(result.IsSuccess, result.ErrorMessage);
        return aircraft;
    }

    private async Task DeleteAsync(Guid id)
    {
        var rowVersion = await GetRowVersionAsync(id);
        await using var context = NewContext();
        var repository = new AircraftRepository(context);
        var tracked = (await repository.GetForUpdateAsync(id, CancellationToken.None))!;
        Assert.True((await repository.DeleteAsync(tracked, rowVersion, CancellationToken.None)).IsSuccess);
    }

    private async Task<byte[]> GetRowVersionAsync(Guid id)
    {
        await using var context = NewContext();
        return (await new AircraftRepository(context).GetByIdAsync(id, CancellationToken.None))!.RowVersion;
    }
}
