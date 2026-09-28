using AircraftMRO.Application.Features.WorkOrders.DTOs;
using AircraftMRO.Domain.Enums.WorkOrders;
using WorkOrderEntity = AircraftMRO.Domain.Entities.WorkOrder;

namespace AircraftMRO.Web.Features.WorkOrders.Models;

public sealed record WorkOrderDetailsViewModel(
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
    string RowVersion)
{
    public bool IsOpen => WorkOrderEntity.IsOpenStatus(Status);

    public WorkOrderActionsViewModel Actions => new(Id, Number, IsOpen);

    public static WorkOrderDetailsViewModel From(WorkOrderDto dto) => new(
        dto.Id,
        dto.AircraftId,
        dto.AircraftRegistration,
        dto.Number,
        dto.Title,
        dto.Description,
        dto.Priority,
        dto.Status,
        dto.DueDate,
        dto.IsOverdue,
        dto.CompletedAtUtc,
        dto.CreatedAtUtc,
        dto.CreatedBy,
        dto.UpdatedAtUtc,
        dto.UpdatedBy,
        Convert.ToBase64String(dto.RowVersion));
}

/// <summary>
/// Row and page actions: an open work order can be edited but not deleted (it must be completed
/// or cancelled first); a closed one can be deleted but no longer edited.
/// </summary>
public sealed record WorkOrderActionsViewModel(Guid Id, string Number, bool IsOpen);
