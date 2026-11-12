using AircraftMRO.Application.Features.MaintenanceOrders.DTOs;
using AircraftMRO.Domain.Enums.MaintenanceOrders;
using MaintenanceOrderEntity = AircraftMRO.Domain.Entities.MaintenanceOrder;

namespace AircraftMRO.Web.Features.MaintenanceOrders.Models;

public sealed record MaintenanceOrderDetailsViewModel(
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
    string? UpdatedBy,
    string RowVersion)
{
    public string Title => $"{MaintenanceOrderLabels.ToLabel(Type)} · {MaintenanceOrderLabels.Date(ScheduledDate)}";

    public MaintenanceOrderActionsViewModel Actions => MaintenanceOrderActionsViewModel.For(Id, Title, Status);

    public bool CanBeDeleted => MaintenanceOrderEntity.CanBeDeleted(Status);

    public static MaintenanceOrderDetailsViewModel From(MaintenanceOrderDto dto) => new(
        dto.Id,
        dto.WorkOrderId,
        dto.WorkOrderNumber,
        dto.AircraftId,
        dto.AircraftRegistration,
        dto.Type,
        dto.Status,
        dto.ScheduledDate,
        dto.IsOverdue,
        dto.CompletedAtUtc,
        dto.Notes,
        dto.CreatedAtUtc,
        dto.CreatedBy,
        dto.UpdatedAtUtc,
        dto.UpdatedBy,
        Convert.ToBase64String(dto.RowVersion));
}

/// <summary>
/// Row and page actions: open maintenance can be edited; only scheduled maintenance can be deleted;
/// completed maintenance is final.
/// </summary>
public sealed record MaintenanceOrderActionsViewModel(Guid Id, string Label, bool CanEdit, bool CanDelete)
{
    public static MaintenanceOrderActionsViewModel For(Guid id, string label, MaintenanceStatus status) =>
        new(id, label, MaintenanceOrderEntity.IsOpenStatus(status), MaintenanceOrderEntity.CanBeDeleted(status));
}
