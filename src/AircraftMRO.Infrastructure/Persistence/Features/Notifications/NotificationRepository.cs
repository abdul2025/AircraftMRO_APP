using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.Notifications.DTOs;
using AircraftMRO.Application.Features.Notifications.Ports;
using AircraftMRO.Domain.Entities;
using AircraftMRO.Infrastructure.Auditing;
using Microsoft.EntityFrameworkCore;

namespace AircraftMRO.Infrastructure.Persistence.Features.Notifications;

internal sealed class NotificationRepository(AircraftMroDbContext dbContext) : INotificationRepository
{
    public async Task<PagedResponse<NotificationDto>> ListAsync(PagedRequest request, CancellationToken cancellationToken)
    {
        var query = dbContext.Notifications.AsNoTracking();

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(notification => notification.Id)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResponse<NotificationDto>(rows.Select(ToDto).ToList(), request.Page, request.PageSize, totalCount);
    }

    public async Task<IReadOnlyList<NotificationDto>> ListAfterAsync(long afterId, int max, CancellationToken cancellationToken)
    {
        var rows = await dbContext.Notifications
            .AsNoTracking()
            .Where(notification => notification.Id > afterId)
            .OrderBy(notification => notification.Id)
            .Take(max)
            .ToListAsync(cancellationToken);

        return rows.Select(ToDto).ToList();
    }

    public async Task<long> GetLatestIdAsync(CancellationToken cancellationToken) =>
        await dbContext.Notifications.MaxAsync(notification => (long?)notification.Id, cancellationToken) ?? 0;

    // Changes are stored as JSON, so they are expanded after materializing the row.
    private static NotificationDto ToDto(Notification notification) => new(
        notification.Id,
        notification.Action,
        notification.EntityType,
        notification.EntityId,
        notification.EntityDisplayName,
        notification.Message,
        ChangeNotificationBuilder.DeserializeChanges(notification.ChangesJson),
        notification.ActorUserId,
        notification.OccurredAtUtc);
}
