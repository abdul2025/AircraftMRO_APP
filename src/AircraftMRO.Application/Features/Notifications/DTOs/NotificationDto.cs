using AircraftMRO.Domain.Enums.Notifications;

namespace AircraftMRO.Application.Features.Notifications.DTOs;

public sealed record NotificationDto(
    long Id,
    NotificationAction Action,
    string EntityType,
    Guid EntityId,
    string EntityDisplayName,
    string Message,
    IReadOnlyList<NotificationChangeDto> Changes,
    string? ActorUserId,
    DateTimeOffset OccurredAtUtc);

/// <summary>One changed property: its values before and after, formatted for display.</summary>
public sealed record NotificationChangeDto(string Property, string? OldValue, string? NewValue);
