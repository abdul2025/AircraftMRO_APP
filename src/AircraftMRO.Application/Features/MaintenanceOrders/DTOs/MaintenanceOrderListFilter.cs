using AircraftMRO.Domain.Enums.MaintenanceOrders;

namespace AircraftMRO.Application.Features.MaintenanceOrders.DTOs;

/// <summary>
/// Optional list filters. <see cref="OpenOnly"/> keeps scheduled and in-progress maintenance;
/// <see cref="OverdueOnly"/> keeps open maintenance past its scheduled date.
/// </summary>
public sealed record MaintenanceOrderListFilter
{
    public static readonly MaintenanceOrderListFilter None = new();

    public MaintenanceOrderListFilter(
        Guid? workOrderId = null,
        MaintenanceStatus? status = null,
        MaintenanceType? type = null,
        bool openOnly = false,
        bool overdueOnly = false)
    {
        WorkOrderId = workOrderId == Guid.Empty ? null : workOrderId;
        Status = status is { } statusValue && Enum.IsDefined(statusValue) ? statusValue : null;
        Type = type is { } typeValue && Enum.IsDefined(typeValue) ? typeValue : null;
        OpenOnly = openOnly;
        OverdueOnly = overdueOnly;
    }

    public Guid? WorkOrderId { get; }
    public MaintenanceStatus? Status { get; }
    public MaintenanceType? Type { get; }
    public bool OpenOnly { get; }
    public bool OverdueOnly { get; }

    public bool IsEmpty => WorkOrderId is null && Status is null && Type is null && !OpenOnly && !OverdueOnly;
}
