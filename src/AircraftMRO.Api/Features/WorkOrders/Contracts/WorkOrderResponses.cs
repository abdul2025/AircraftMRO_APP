using AircraftMRO.Application.Features.WorkOrders.DTOs;
using AircraftMRO.Domain.Enums.WorkOrders;

namespace AircraftMRO.Api.Features.WorkOrders.Contracts;

public sealed record WorkOrderResponse(
    Guid Id,
    Guid AircraftId,
    string AircraftRegistration,
    string Number,
    string Title,
    string? Description,
    WorkOrderPriority Priority,
    WorkOrderStatus Status,
    DateOnly? DueDate,
    bool IsOverdue,
    DateTimeOffset? CompletedAtUtc,
    DateTimeOffset CreatedAtUtc,
    string? CreatedBy,
    DateTimeOffset? UpdatedAtUtc,
    string? UpdatedBy)
{
    public static WorkOrderResponse From(WorkOrderDto dto) => new(
        dto.Id, dto.AircraftId, dto.AircraftRegistration, dto.Number, dto.Title, dto.Description,
        dto.Priority, dto.Status, dto.DueDate, dto.IsOverdue, dto.CompletedAtUtc,
        dto.CreatedAtUtc, dto.CreatedBy, dto.UpdatedAtUtc, dto.UpdatedBy);
}

public sealed record WorkOrderSummaryResponse(
    Guid Id,
    Guid AircraftId,
    string AircraftRegistration,
    string Number,
    string Title,
    WorkOrderPriority Priority,
    WorkOrderStatus Status,
    DateOnly? DueDate,
    bool IsOverdue)
{
    public static WorkOrderSummaryResponse From(WorkOrderListItemDto dto) => new(
        dto.Id, dto.AircraftId, dto.AircraftRegistration, dto.Number, dto.Title,
        dto.Priority, dto.Status, dto.DueDate, dto.IsOverdue);
}

public sealed record WorkOrderPageResponse(
    IReadOnlyList<WorkOrderSummaryResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record WorkOrderStatisticsResponse(
    int TotalCount,
    IReadOnlyDictionary<WorkOrderStatus, int> CountByStatus,
    IReadOnlyDictionary<WorkOrderPriority, int> OpenCountByPriority,
    int OpenCount,
    int OverdueCount)
{
    /// <summary>Every status and priority is present, with zero when there are none.</summary>
    public static WorkOrderStatisticsResponse From(WorkOrderStatisticsDto dto) => new(
        dto.TotalCount,
        Enum.GetValues<WorkOrderStatus>().ToDictionary(status => status, dto.CountFor),
        Enum.GetValues<WorkOrderPriority>().ToDictionary(priority => priority, dto.OpenCountFor),
        dto.OpenCount,
        dto.OverdueCount);
}
