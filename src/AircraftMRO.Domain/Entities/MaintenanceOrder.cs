using System.Globalization;
using AircraftMRO.Domain.Common.Entities;
using AircraftMRO.Domain.Common.Results;
using AircraftMRO.Domain.Enums.MaintenanceOrders;

namespace AircraftMRO.Domain.Entities;

/// <summary>
/// A maintenance check scheduled under one work order. While any is open (scheduled or in
/// progress) its work order cannot be completed or cancelled; see <see cref="WorkOrder.Update"/>.
/// </summary>
public sealed class MaintenanceOrder : AuditableEntity, IHasDisplayName
{
    public const string ValidationErrorCode = "MaintenanceOrder.Validation";
    public const string ClosedErrorCode = "MaintenanceOrder.Closed";
    public const int NotesMaxLength = 2000;

    private MaintenanceOrder()
    {
    }

    public Guid WorkOrderId { get; private set; }
    public MaintenanceType Type { get; private set; }
    public DateOnly ScheduledDate { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public MaintenanceStatus Status { get; private set; }
    public string? Notes { get; private set; }

    public string DisplayName =>
        $"{Type} {ScheduledDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";

    public bool IsOpen => IsOpenStatus(Status);

    public static bool IsOpenStatus(MaintenanceStatus status) =>
        status is MaintenanceStatus.Scheduled or MaintenanceStatus.InProgress;

    /// <summary>Open and past its scheduled date. Scheduled for today is not yet overdue.</summary>
    public static bool IsOverdue(MaintenanceStatus status, DateOnly scheduledDate, DateOnly today) =>
        IsOpenStatus(status) && scheduledDate < today;

    /// <summary>Only scheduled maintenance can be deleted; started or completed work is kept as history.</summary>
    public static bool CanBeDeleted(MaintenanceStatus status) => status == MaintenanceStatus.Scheduled;

    /// <summary>A new maintenance order always starts <see cref="MaintenanceStatus.Scheduled"/>.</summary>
    public static Result<MaintenanceOrder> Create(
        Guid workOrderId,
        MaintenanceType type,
        DateOnly scheduledDate,
        string? notes)
    {
        if (workOrderId == Guid.Empty)
        {
            return Result<MaintenanceOrder>.Failure(ValidationErrorCode, "Work order is required.");
        }

        var maintenanceOrder = new MaintenanceOrder
        {
            WorkOrderId = workOrderId,
            Status = MaintenanceStatus.Scheduled
        };
        var result = maintenanceOrder.Apply(type, scheduledDate, MaintenanceStatus.Scheduled, notes, completedAtUtc: null);

        return result.IsSuccess
            ? Result<MaintenanceOrder>.Success(maintenanceOrder)
            : Result<MaintenanceOrder>.Failure(result.ErrorCode!, result.ErrorMessage!);
    }

    /// <summary>Changes the details and status. Completed maintenance is final and can no longer be changed.</summary>
    public Result Update(
        MaintenanceType type,
        DateOnly scheduledDate,
        MaintenanceStatus status,
        string? notes,
        DateTimeOffset now)
    {
        if (!IsOpen)
        {
            return Result.Failure(ClosedErrorCode, "This maintenance order is completed and can no longer be changed.");
        }

        var completedAtUtc = status == MaintenanceStatus.Completed ? now : (DateTimeOffset?)null;
        return Apply(type, scheduledDate, status, notes, completedAtUtc);
    }

    private Result Apply(
        MaintenanceType type,
        DateOnly scheduledDate,
        MaintenanceStatus status,
        string? notes,
        DateTimeOffset? completedAtUtc)
    {
        var trimmedNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

        if (!Enum.IsDefined(type))
        {
            return Invalid("Maintenance type is not valid.");
        }

        if (!Enum.IsDefined(status))
        {
            return Invalid("Status is not valid.");
        }

        if (scheduledDate == default)
        {
            return Invalid("Scheduled date is required.");
        }

        if (trimmedNotes is { Length: > NotesMaxLength })
        {
            return Invalid($"Notes must be at most {NotesMaxLength} characters.");
        }

        Type = type;
        ScheduledDate = scheduledDate;
        Status = status;
        Notes = trimmedNotes;
        CompletedAtUtc = completedAtUtc;

        return Result.Success();
    }

    private static Result Invalid(string message) => Result.Failure(ValidationErrorCode, message);
}
