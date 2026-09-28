using AircraftMRO.Domain.Enums.WorkOrders;

namespace AircraftMRO.Application.Features.WorkOrders.DTOs;

public sealed record WorkOrderListItemDto(
    Guid Id,
    Guid AircraftId,
    string AircraftRegistration,
    string Number,
    string Title,
    WorkOrderPriority Priority,
    WorkOrderStatus Status,
    DateOnly? DueDate,
    bool IsOverdue);
