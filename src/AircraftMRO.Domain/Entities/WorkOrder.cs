using AircraftMRO.Domain.Common.Entities;
using AircraftMRO.Domain.Common.Results;
using AircraftMRO.Domain.Enums.WorkOrders;

namespace AircraftMRO.Domain.Entities;

/// <summary>
/// A unit of maintenance work raised against one aircraft. While open it holds the aircraft
/// out of service; see <see cref="Aircraft.ApplyOpenWorkOrder"/>.
/// </summary>
public sealed class WorkOrder : AuditableEntity, IHasDisplayName
{
    public const string ValidationErrorCode = "WorkOrder.Validation";
    public const string ClosedErrorCode = "WorkOrder.Closed";
    public const int NumberMaxLength = 20;
    public const int TitleMaxLength = 150;
    public const int DescriptionMaxLength = 2000;

    private WorkOrder()
    {
    }

    public Guid AircraftId { get; private set; }
    public string Number { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public string? Description { get; private set; }
    public WorkOrderPriority Priority { get; private set; }
    public WorkOrderStatus Status { get; private set; }
    public DateOnly? DueDate { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public string DisplayName => Number;

    public bool IsOpen => IsOpenStatus(Status);

    public static bool IsOpenStatus(WorkOrderStatus status) =>
        status is WorkOrderStatus.Open or WorkOrderStatus.InProgress or WorkOrderStatus.OnHold;

    /// <summary>Open and past its due date. A due date of today is not yet overdue.</summary>
    public static bool IsOverdue(WorkOrderStatus status, DateOnly? dueDate, DateOnly today) =>
        IsOpenStatus(status) && dueDate < today;

    /// <summary>A new work order always starts <see cref="WorkOrderStatus.Open"/>.</summary>
    public static Result<WorkOrder> Create(
        Guid aircraftId,
        string number,
        string title,
        string? description,
        WorkOrderPriority priority,
        DateOnly? dueDate)
    {
        var trimmedNumber = number?.Trim() ?? string.Empty;
        if (aircraftId == Guid.Empty)
        {
            return Result<WorkOrder>.Failure(ValidationErrorCode, "Aircraft is required.");
        }

        if (trimmedNumber.Length is 0 or > NumberMaxLength)
        {
            return Result<WorkOrder>.Failure(ValidationErrorCode, "Work order number is missing.");
        }

        var workOrder = new WorkOrder
        {
            AircraftId = aircraftId,
            Number = trimmedNumber,
            Status = WorkOrderStatus.Open
        };
        var result = workOrder.Apply(title, description, priority, WorkOrderStatus.Open, dueDate, completedAtUtc: null);

        return result.IsSuccess
            ? Result<WorkOrder>.Success(workOrder)
            : Result<WorkOrder>.Failure(result.ErrorCode!, result.ErrorMessage!);
    }

    /// <summary>
    /// Changes the details and status. Completed and cancelled work orders are final and
    /// can no longer be changed.
    /// </summary>
    public Result Update(
        string title,
        string? description,
        WorkOrderPriority priority,
        WorkOrderStatus status,
        DateOnly? dueDate,
        DateTimeOffset now)
    {
        if (!IsOpen)
        {
            return Result.Failure(ClosedErrorCode, "This work order is closed and can no longer be changed.");
        }

        var completedAtUtc = status == WorkOrderStatus.Completed ? now : (DateTimeOffset?)null;
        return Apply(title, description, priority, status, dueDate, completedAtUtc);
    }

    private Result Apply(
        string title,
        string? description,
        WorkOrderPriority priority,
        WorkOrderStatus status,
        DateOnly? dueDate,
        DateTimeOffset? completedAtUtc)
    {
        var trimmedTitle = title?.Trim() ?? string.Empty;
        var trimmedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

        if (trimmedTitle.Length is 0 or > TitleMaxLength)
        {
            return Invalid($"Title is required and must be at most {TitleMaxLength} characters.");
        }

        if (trimmedDescription is { Length: > DescriptionMaxLength })
        {
            return Invalid($"Description must be at most {DescriptionMaxLength} characters.");
        }

        if (!Enum.IsDefined(priority))
        {
            return Invalid("Priority is not valid.");
        }

        if (!Enum.IsDefined(status))
        {
            return Invalid("Status is not valid.");
        }

        Title = trimmedTitle;
        Description = trimmedDescription;
        Priority = priority;
        Status = status;
        DueDate = dueDate;
        CompletedAtUtc = completedAtUtc;

        return Result.Success();
    }

    private static Result Invalid(string message) => Result.Failure(ValidationErrorCode, message);
}
