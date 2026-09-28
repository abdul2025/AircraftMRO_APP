using AircraftMRO.Domain.Enums.WorkOrders;

namespace AircraftMRO.Application.Features.WorkOrders.DTOs;

public sealed record WorkOrderDto(
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
    string? UpdatedBy,
    byte[] RowVersion);
