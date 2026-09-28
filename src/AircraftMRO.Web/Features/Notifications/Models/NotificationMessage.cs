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
    /// <summary>Links to the entity's Details page when its type has one and it was not deleted.</summary>
    public static NotificationMessage From(NotificationDto dto, LinkGenerator links) => new(
        dto.Id,
        dto.Action.ToString(),
        dto.EntityType,
        dto.EntityId,
        dto.EntityDisplayName,
        dto.Message,
        dto.OccurredAtUtc,
        NotificationLinks.DetailsPath(links, dto.EntityType, dto.EntityId, dto.Action == NotificationAction.Deleted));
}
