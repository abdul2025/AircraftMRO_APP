using AircraftMRO.Domain.Enums.Notifications;

namespace AircraftMRO.Domain.Entities;

/// <summary>
/// An immutable record of an action on an entity. Deliberately not an <c>AuditableEntity</c>:
/// recording a notification must never produce another notification.
/// </summary>
public sealed class Notification
{
    public const int EntityTypeMaxLength = 100;
    public const int EntityDisplayNameMaxLength = 200;
    public const int MessageMaxLength = 500;
    public const int ChangesMaxLength = 4000;
    public const int ActorUserIdMaxLength = 450;

    private Notification()
    {
    }

    /// <summary>Database-assigned and increasing, so it also orders notifications.</summary>
    public long Id { get; private set; }
    public NotificationAction Action { get; private set; }
    public string EntityType { get; private set; } = null!;
    public Guid EntityId { get; private set; }
    public string EntityDisplayName { get; private set; } = null!;
    public string Message { get; private set; } = null!;
    public string? ChangesJson { get; private set; }
    public string? ActorUserId { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }

    public static Notification Record(
        NotificationAction action,
        string entityType,
        Guid entityId,
        string entityDisplayName,
        string message,
        string? changesJson,
        string? actorUserId,
        DateTimeOffset occurredAtUtc)
    {
        if (!Enum.IsDefined(action))
        {
            throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown notification action.");
        }

        if (string.IsNullOrWhiteSpace(entityType) || entityType.Length > EntityTypeMaxLength)
        {
            throw new ArgumentException($"Entity type is required and must be at most {EntityTypeMaxLength} characters.", nameof(entityType));
        }

        if (string.IsNullOrWhiteSpace(entityDisplayName))
        {
            throw new ArgumentException("Entity display name is required.", nameof(entityDisplayName));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Message is required.", nameof(message));
        }

        if (changesJson is { Length: > ChangesMaxLength })
        {
            throw new ArgumentException($"Changes must be at most {ChangesMaxLength} characters.", nameof(changesJson));
        }

        if (actorUserId is { Length: > ActorUserIdMaxLength })
        {
            throw new ArgumentException("Actor user id is too long.", nameof(actorUserId));
        }

        return new Notification
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            EntityDisplayName = Truncate(entityDisplayName, EntityDisplayNameMaxLength),
            Message = Truncate(message, MessageMaxLength),
            ChangesJson = changesJson,
            ActorUserId = actorUserId,
            OccurredAtUtc = occurredAtUtc
        };
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..(maxLength - 1)] + "…";
}
