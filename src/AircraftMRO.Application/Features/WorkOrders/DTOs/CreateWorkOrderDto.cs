using AircraftMRO.Domain.Enums.WorkOrders;

namespace AircraftMRO.Application.Features.WorkOrders.DTOs;

public sealed record CreateWorkOrderDto(
    Guid AircraftId,
    string Title,
    string? Description,
    WorkOrderPriority Priority,
    DateOnly? DueDate);
