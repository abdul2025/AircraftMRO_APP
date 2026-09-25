using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.Notifications.DTOs;
using AircraftMRO.Application.Features.Notifications.Interfaces;
using AircraftMRO.Application.Features.Notifications.Ports;
using AircraftMRO.Domain.Common.Results;

namespace AircraftMRO.Application.Features.Notifications;

public sealed class NotificationService(INotificationRepository repository) : INotificationService
{
    public const int MaxBatchSize = 200;

    public async Task<Result<PagedResponse<NotificationDto>>> ListAsync(
        PagedRequest request,
        CancellationToken cancellationToken) =>
        Result<PagedResponse<NotificationDto>>.Success(await repository.ListAsync(request, cancellationToken));

    public async Task<Result<IReadOnlyList<NotificationDto>>> ListAfterAsync(
        long afterId,
        int max,
        CancellationToken cancellationToken) =>
        Result<IReadOnlyList<NotificationDto>>.Success(
            await repository.ListAfterAsync(Math.Max(0, afterId), Math.Clamp(max, 1, MaxBatchSize), cancellationToken));

    public async Task<Result<long>> GetLatestIdAsync(CancellationToken cancellationToken) =>
        Result<long>.Success(await repository.GetLatestIdAsync(cancellationToken));
}
