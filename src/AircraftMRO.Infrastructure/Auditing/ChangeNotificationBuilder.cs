using System.Globalization;
using System.Text;
using System.Text.Json;
using AircraftMRO.Application.Features.Notifications.DTOs;
using AircraftMRO.Domain.Common.Entities;
using AircraftMRO.Domain.Entities;
using AircraftMRO.Domain.Enums.Notifications;
using AircraftMRO.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AircraftMRO.Infrastructure.Auditing;

/// <summary>Turns a pending change to an <see cref="AuditableEntity"/> into a <see cref="Notification"/>.</summary>
internal static class ChangeNotificationBuilder
{
    private const int MaxChangesInMessage = 3;
    private const int MaxValueLength = 200;

    public static readonly JsonSerializerOptions ChangesJsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Audit, soft-delete, and concurrency columns are bookkeeping, not business changes.</summary>
    private static readonly HashSet<string> ExcludedProperties =
    [
        nameof(BaseEntity.Id),
        nameof(AuditableEntity.CreatedAtUtc),
        nameof(AuditableEntity.CreatedBy),
        nameof(AuditableEntity.UpdatedAtUtc),
        nameof(AuditableEntity.UpdatedBy),
        nameof(AuditableEntity.IsDeleted),
        nameof(AuditableEntity.DeletedAtUtc),
        nameof(AuditableEntity.DeletedBy),
        AircraftMroDbContext.RowVersionPropertyName
    ];

    /// <summary>
    /// Returns <c>null</c> when the entry is not a create, update, or delete, or when an update
    /// changed no business property. Must run before a delete is converted to a soft delete.
    /// </summary>
    public static Notification? Build(EntityEntry<AuditableEntity> entry, string? actorUserId, DateTimeOffset occurredAtUtc)
    {
        NotificationAction? action = entry.State switch
        {
            EntityState.Added => NotificationAction.Created,
            EntityState.Modified => NotificationAction.Updated,
            EntityState.Deleted => NotificationAction.Deleted,
            _ => null
        };

        if (action is null)
        {
            return null;
        }

        var changes = action == NotificationAction.Updated ? CollectChanges(entry) : [];
        if (action == NotificationAction.Updated && changes.Count == 0)
        {
            return null;
        }

        var entity = entry.Entity;
        var entityType = entry.Metadata.ClrType.Name;
        var displayName = entity is IHasDisplayName { DisplayName: { Length: > 0 } name } ? name : entity.Id.ToString();

        return Notification.Record(
            action.Value,
            entityType,
            entity.Id,
            displayName,
            BuildMessage(action.Value, Humanize(entityType), displayName, changes),
            SerializeChanges(changes),
            actorUserId,
            occurredAtUtc);
    }

    public static IReadOnlyList<NotificationChangeDto> DeserializeChanges(string? json) =>
        string.IsNullOrEmpty(json)
            ? []
            : JsonSerializer.Deserialize<List<NotificationChangeDto>>(json, ChangesJsonOptions) ?? [];

    private static List<NotificationChangeDto> CollectChanges(EntityEntry entry) =>
        entry.Properties
            .Where(property => property.IsModified
                && !ExcludedProperties.Contains(property.Metadata.Name)
                && !Equals(property.OriginalValue, property.CurrentValue))
            .Select(property => new NotificationChangeDto(
                property.Metadata.Name,
                Format(property.OriginalValue),
                Format(property.CurrentValue)))
            .ToList();

    private static string BuildMessage(
        NotificationAction action,
        string entityLabel,
        string displayName,
        IReadOnlyList<NotificationChangeDto> changes)
    {
        var verb = action switch
        {
            NotificationAction.Created => "created",
            NotificationAction.Deleted => "deleted",
            _ => "updated"
        };

        var message = new StringBuilder($"{entityLabel} {displayName} was {verb}");
        if (changes.Count > 0)
        {
            message.Append(": ");
            message.AppendJoin(", ", changes
                .Take(MaxChangesInMessage)
                .Select(change => $"{Humanize(change.Property)} {change.OldValue ?? "none"} → {change.NewValue ?? "none"}"));
            if (changes.Count > MaxChangesInMessage)
            {
                message.Append(", …");
            }
        }

        return message.ToString();
    }

    /// <summary>Serializes changes, dropping the last ones if needed to stay within the column size.</summary>
    private static string? SerializeChanges(List<NotificationChangeDto> changes)
    {
        while (changes.Count > 0)
        {
            var json = JsonSerializer.Serialize(changes, ChangesJsonOptions);
            if (json.Length <= Notification.ChangesMaxLength)
            {
                return json;
            }

            changes.RemoveAt(changes.Count - 1);
        }

        return null;
    }

    private static string? Format(object? value)
    {
        var text = value switch
        {
            null => null,
            string s => s,
            Enum e => e.ToString(),
            DateTimeOffset d => d.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            DateTime d => d.ToString("O", CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };

        return text is { Length: > MaxValueLength } ? text[..(MaxValueLength - 1)] + "…" : text;
    }

    /// <summary>"YearOfManufacture" becomes "Year of manufacture".</summary>
    private static string Humanize(string name)
    {
        var builder = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1]))
            {
                builder.Append(' ').Append(char.ToLowerInvariant(c));
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
