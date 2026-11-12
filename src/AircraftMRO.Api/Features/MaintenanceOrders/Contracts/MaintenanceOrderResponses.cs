using AircraftMRO.Application.Features.MaintenanceOrders.DTOs;
using AircraftMRO.Domain.Enums.MaintenanceOrders;

namespace AircraftMRO.Api.Features.MaintenanceOrders.Contracts;

public sealed record MaintenanceOrderResponse(
    Guid Id,
    Guid WorkOrderId,
    string WorkOrderNumber,
    Guid AircraftId,
    string AircraftRegistration,
    MaintenanceType Type,
    MaintenanceStatus Status,
    DateOnly ScheduledDate,
    bool IsOverdue,
    DateTimeOffset? CompletedAtUtc,
    string? Notes,
    DateTimeOffset CreatedAtUtc,
    string? CreatedBy,
    DateTimeOffset? UpdatedAtUtc,
    string? UpdatedBy)
{
    public static MaintenanceOrderResponse From(MaintenanceOrderDto dto) => new(
        dto.Id, dto.WorkOrderId, dto.WorkOrderNumber, dto.AircraftId, dto.AircraftRegistration,
        dto.Type, dto.Status, dto.ScheduledDate, dto.IsOverdue, dto.CompletedAtUtc, dto.Notes,
        dto.CreatedAtUtc, dto.CreatedBy, dto.UpdatedAtUtc, dto.UpdatedBy);
}

public sealed record MaintenanceOrderSummaryResponse(
    Guid Id,
    Guid WorkOrderId,
    string WorkOrderNumber,
    string AircraftRegistration,
    MaintenanceType Type,
    MaintenanceStatus Status,
    DateOnly ScheduledDate,
    bool IsOverdue,
    DateTimeOffset? CompletedAtUtc)
{
    public static MaintenanceOrderSummaryResponse From(MaintenanceOrderListItemDto dto) => new(
        dto.Id, dto.WorkOrderId, dto.WorkOrderNumber, dto.AircraftRegistration,
        dto.Type, dto.Status, dto.ScheduledDate, dto.IsOverdue, dto.CompletedAtUtc);
}

public sealed record MaintenanceOrderStatisticsResponse(
    int TotalCount,
    IReadOnlyDictionary<MaintenanceStatus, int> CountByStatus,
    int OpenCount,
    int OverdueCount)
{
    /// <summary>Every status is present, with zero when there are none.</summary>
    public static MaintenanceOrderStatisticsResponse From(MaintenanceOrderStatisticsDto dto) => new(
        dto.TotalCount,
        Enum.GetValues<MaintenanceStatus>().ToDictionary(status => status, dto.CountFor),
        dto.OpenCount,
        dto.OverdueCount);
}

public sealed record MaintenanceOrderPageResponse(
    IReadOnlyList<MaintenanceOrderSummaryResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
