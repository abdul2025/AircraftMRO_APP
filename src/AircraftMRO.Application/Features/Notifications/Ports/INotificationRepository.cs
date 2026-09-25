using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.Notifications.DTOs;

namespace AircraftMRO.Application.Features.Notifications.Ports;

public interface INotificationRepository
{
    Task<PagedResponse<NotificationDto>> ListAsync(PagedRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<NotificationDto>> ListAfterAsync(long afterId, int max, CancellationToken cancellationToken);

    Task<long> GetLatestIdAsync(CancellationToken cancellationToken);
}
