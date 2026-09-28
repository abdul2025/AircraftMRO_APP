using AircraftMRO.Domain.Enums.WorkOrders;

namespace AircraftMRO.Application.Features.WorkOrders.DTOs;

/// <summary>
/// Optional list filters. The search term matches the work order number, title, or aircraft
/// registration. <see cref="OpenOnly"/> keeps open work orders; <see cref="OverdueOnly"/> keeps
/// open work orders past their due date.
/// </summary>
public sealed record WorkOrderListFilter
{
    public const int MaxSearchLength = 100;

    public static readonly WorkOrderListFilter None = new();

    public WorkOrderListFilter(
        string? search = null,
        WorkOrderStatus? status = null,
        WorkOrderPriority? priority = null,
        Guid? aircraftId = null,
        bool openOnly = false,
        bool overdueOnly = false)
    {
        var trimmed = search?.Trim();
        Search = string.IsNullOrEmpty(trimmed)
            ? null
            : trimmed.Length > MaxSearchLength ? trimmed[..MaxSearchLength] : trimmed;
        Status = status is { } statusValue && Enum.IsDefined(statusValue) ? statusValue : null;
        Priority = priority is { } priorityValue && Enum.IsDefined(priorityValue) ? priorityValue : null;
        AircraftId = aircraftId == Guid.Empty ? null : aircraftId;
        OpenOnly = openOnly;
        OverdueOnly = overdueOnly;
    }

    public string? Search { get; }
    public WorkOrderStatus? Status { get; }
    public WorkOrderPriority? Priority { get; }
    public Guid? AircraftId { get; }
    public bool OpenOnly { get; }
    public bool OverdueOnly { get; }

    public bool IsEmpty =>
        Search is null && Status is null && Priority is null && AircraftId is null && !OpenOnly && !OverdueOnly;
}
