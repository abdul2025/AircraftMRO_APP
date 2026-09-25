using AircraftMRO.Application.Common.Interfaces;
using AircraftMRO.Domain.Common.Entities;
using AircraftMRO.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AircraftMRO.Infrastructure.Auditing;

/// <summary>
/// Stamps audit values on every <see cref="AuditableEntity"/> and turns deletes into soft deletes.
/// </summary>
internal sealed class AuditableEntityInterceptor(ICurrentUser currentUser, TimeProvider timeProvider)
    : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ApplyAuditValues(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditValues(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyAuditValues(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var userId = currentUser.UserId;

        foreach (var entry in context.ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    Set(entry, nameof(AuditableEntity.CreatedAtUtc), now);
                    Set(entry, nameof(AuditableEntity.CreatedBy), userId);
                    Set(entry, nameof(AuditableEntity.UpdatedAtUtc), null);
                    Set(entry, nameof(AuditableEntity.UpdatedBy), null);
                    Set(entry, nameof(AuditableEntity.IsDeleted), false);
                    Set(entry, nameof(AuditableEntity.DeletedAtUtc), null);
                    Set(entry, nameof(AuditableEntity.DeletedBy), null);
                    break;

                case EntityState.Modified:
                    ProtectCreationValues(entry);
                    Set(entry, nameof(AuditableEntity.UpdatedAtUtc), now);
                    Set(entry, nameof(AuditableEntity.UpdatedBy), userId);
                    break;

                case EntityState.Deleted:
                    SoftDelete(entry, now, userId);
                    break;
            }
        }
    }

    private static void SoftDelete(EntityEntry<AuditableEntity> entry, DateTimeOffset now, string? userId)
    {
        // Keep the concurrency token the caller expects so a stale delete is still rejected.
        var rowVersion = entry.Property(AircraftMroDbContext.RowVersionPropertyName);
        var expectedRowVersion = rowVersion.OriginalValue;

        entry.State = EntityState.Unchanged;
        rowVersion.OriginalValue = expectedRowVersion;

        Set(entry, nameof(AuditableEntity.IsDeleted), true);
        Set(entry, nameof(AuditableEntity.DeletedAtUtc), now);
        Set(entry, nameof(AuditableEntity.DeletedBy), userId);
    }

    private static void ProtectCreationValues(EntityEntry<AuditableEntity> entry)
    {
        foreach (var name in new[] { nameof(AuditableEntity.CreatedAtUtc), nameof(AuditableEntity.CreatedBy) })
        {
            var property = entry.Property(name);
            property.CurrentValue = property.OriginalValue;
            property.IsModified = false;
        }
    }

    private static void Set(EntityEntry entry, string propertyName, object? value) =>
        entry.Property(propertyName).CurrentValue = value;
}
