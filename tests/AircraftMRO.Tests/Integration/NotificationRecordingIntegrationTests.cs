using AircraftMRO.Application.Features.Aircraft;
using AircraftMRO.Domain.Common.Entities;
using AircraftMRO.Domain.Entities;
using AircraftMRO.Domain.Enums.Aircraft;
using AircraftMRO.Domain.Enums.Notifications;
using AircraftMRO.Infrastructure.Persistence;
using AircraftMRO.Infrastructure.Persistence.Features.Aircraft;
using AircraftMRO.Infrastructure.Persistence.Features.Notifications;
using AircraftMRO.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace AircraftMRO.Tests.Integration;

[Collection(SqlServerCollection.Name)]
public sealed class NotificationRecordingIntegrationTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private readonly TestCurrentUser _user = new("user-1");
    private readonly FakeTimeProvider _clock = new();
    private string _connectionString = null!;

    public async Task InitializeAsync() => _connectionString = await fixture.CreateDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Create_update_and_delete_each_record_one_notification()
    {
        var aircraft = AircraftTestData.NewAircraft("NT-1", "NT-1");
        await AddAsync(aircraft);

        await using (var context = NewContext())
        {
            var tracked = await context.Aircraft.SingleAsync(a => a.Id == aircraft.Id);
            tracked.Update("NT-1", "Airbus", "A320-214", "NT-1", 2012, 31250.5m, AircraftStatus.Grounded, 2026);
            await context.SaveChangesAsync();
        }

        await using (var context = NewContext())
        {
            var repository = new AircraftRepository(context);
            var rowVersion = (await repository.GetByIdAsync(aircraft.Id, CancellationToken.None))!.RowVersion;
            var tracked = (await repository.GetForUpdateAsync(aircraft.Id, CancellationToken.None))!;
            Assert.True((await repository.DeleteAsync(tracked, rowVersion, CancellationToken.None)).IsSuccess);
        }

        var notifications = await AllNotificationsAsync();
        Assert.Equal([NotificationAction.Created, NotificationAction.Updated, NotificationAction.Deleted], notifications.Select(n => n.Action));
        Assert.All(notifications, n =>
        {
            Assert.Equal("Aircraft", n.EntityType);
            Assert.Equal(aircraft.Id, n.EntityId);
            Assert.Equal("NT-1", n.EntityDisplayName);
            Assert.Equal("user-1", n.ActorUserId);
            Assert.Equal(_clock.Now, n.OccurredAtUtc);
        });

        Assert.Equal("Aircraft NT-1 was created", notifications[0].Message);
        Assert.Equal("Aircraft NT-1 was updated: Status Active → Grounded", notifications[1].Message);
        Assert.Equal("Aircraft NT-1 was deleted", notifications[2].Message);

        await using var read = NewContext();
        var updated = (await new NotificationRepository(read).ListAfterAsync(notifications[0].Id, 10, CancellationToken.None))[0];
        var change = Assert.Single(updated.Changes);
        Assert.Equal(("Status", "Active", "Grounded"), (change.Property, change.OldValue, change.NewValue));
    }

    [Fact]
    public async Task Update_that_changes_nothing_records_nothing()
    {
        var aircraft = AircraftTestData.NewAircraft("NT-2", "NT-2");
        await AddAsync(aircraft);

        await using (var context = NewContext())
        {
            var tracked = await context.Aircraft.SingleAsync(a => a.Id == aircraft.Id);
            tracked.Update("nt-2", "Airbus", "A320-214", "NT-2", 2012, 31250.5m, AircraftStatus.Active, 2026);
            await context.SaveChangesAsync();
        }

        Assert.Single(await AllNotificationsAsync());
    }

    [Fact]
    public async Task Failed_saves_record_nothing()
    {
        var registration = AircraftTestData.UniqueRegistration();
        var aircraft = AircraftTestData.NewAircraft(registration, "NT-3");
        await AddAsync(aircraft);

        // Duplicate registration: rejected by the unique index.
        await using (var context = NewContext())
        {
            var duplicate = await new AircraftRepository(context)
                .AddAsync(AircraftTestData.NewAircraft(registration, "NT-4"), CancellationToken.None);
            Assert.Equal(AircraftErrors.DuplicateRegistration, duplicate.ErrorCode);
        }

        // Stale row version: rejected as a concurrency conflict.
        await using (var context = NewContext())
        {
            var repository = new AircraftRepository(context);
            var tracked = (await repository.GetForUpdateAsync(aircraft.Id, CancellationToken.None))!;
            tracked.Update(registration, "Airbus", "A320-214", "NT-3", 2012, 1m, AircraftStatus.Retired, 2026);
            var stale = await repository.UpdateAsync(tracked, [0, 0, 0, 0, 0, 0, 0, 1], CancellationToken.None);
            Assert.Equal(AircraftErrors.ConcurrencyConflict, stale.ErrorCode);
        }

        var only = Assert.Single(await AllNotificationsAsync());
        Assert.Equal(NotificationAction.Created, only.Action);
    }

    [Fact]
    public void Every_auditable_entity_can_be_described_and_notifications_are_not_auditable()
    {
        using var context = NewContext();
        var auditableTypes = context.Model.GetEntityTypes()
            .Where(type => typeof(AuditableEntity).IsAssignableFrom(type.ClrType))
            .ToList();

        Assert.NotEmpty(auditableTypes);
        Assert.All(auditableTypes, type => Assert.True(
            typeof(IHasDisplayName).IsAssignableFrom(type.ClrType),
            $"{type.ClrType.Name} should implement IHasDisplayName so its notifications have a readable name."));
        Assert.False(typeof(AuditableEntity).IsAssignableFrom(typeof(Notification)));
    }

    private AircraftMroDbContext NewContext() => SqlServerFixture.CreateContext(_connectionString, _user, _clock);

    private async Task AddAsync(Aircraft aircraft)
    {
        await using var context = NewContext();
        var result = await new AircraftRepository(context).AddAsync(aircraft, CancellationToken.None);
        Assert.True(result.IsSuccess, result.ErrorMessage);
    }

    private async Task<List<Notification>> AllNotificationsAsync()
    {
        await using var context = NewContext();
        return await context.Notifications.AsNoTracking().OrderBy(n => n.Id).ToListAsync();
    }
}
