using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.Notifications.DTOs;
using AircraftMRO.Domain.Common.Results;

namespace AircraftMRO.Application.Features.Notifications.Interfaces;

public interface INotificationService
{
    /// <summary>Newest first.</summary>
    Task<Result<PagedResponse<NotificationDto>>> ListAsync(PagedRequest request, CancellationToken cancellationToken);

    /// <summary>Notifications with an id greater than <paramref name="afterId"/>, oldest first, at most <paramref name="max"/>.</summary>
    Task<Result<IReadOnlyList<NotificationDto>>> ListAfterAsync(long afterId, int max, CancellationToken cancellationToken);

    /// <summary>The highest notification id, or 0 when there are none.</summary>
    Task<Result<long>> GetLatestIdAsync(CancellationToken cancellationToken);
}
