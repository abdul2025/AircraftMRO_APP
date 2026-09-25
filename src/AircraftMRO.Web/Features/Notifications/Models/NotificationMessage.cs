using AircraftMRO.Application.Features.Notifications.DTOs;
using AircraftMRO.Domain.Enums.Notifications;

namespace AircraftMRO.Web.Features.Notifications.Models;

/// <summary>What browsers receive over SignalR and from the catch-up endpoint.</summary>
public sealed record NotificationMessage(
    long Id,
    string Action,
    string EntityType,
    Guid EntityId,
    string DisplayName,
    string Message,
    DateTimeOffset OccurredAtUtc,
    string? Url)
{
    /// <summary>
    /// Links to the entity's Details page when one exists for its type (resolved by routing,
    /// so new features get links automatically) and the entity was not deleted.
    /// </summary>
    public static NotificationMessage From(NotificationDto dto, LinkGenerator links) => new(
        dto.Id,
        dto.Action.ToString(),
        dto.EntityType,
        dto.EntityId,
        dto.EntityDisplayName,
        dto.Message,
        dto.OccurredAtUtc,
        dto.Action == NotificationAction.Deleted
            ? null
            : links.GetPathByAction("Details", dto.EntityType, new { id = dto.EntityId }));
}
