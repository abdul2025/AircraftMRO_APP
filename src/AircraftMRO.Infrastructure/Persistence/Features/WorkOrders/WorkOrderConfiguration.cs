using AircraftMRO.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AircraftEntity = AircraftMRO.Domain.Entities.Aircraft;

namespace AircraftMRO.Infrastructure.Persistence.Features.WorkOrders;

internal sealed class WorkOrderConfiguration : IEntityTypeConfiguration<WorkOrder>
{
    public const string NumberIndexName = "IX_WorkOrders_Number";

    /// <summary>Database sequence behind work order numbers; see <see cref="WorkOrderRepository.NextNumberAsync"/>.</summary>
    public const string NumberSequenceName = "WorkOrderNumbers";

    private const string NotDeletedFilter = "[IsDeleted] = 0";

    public void Configure(EntityTypeBuilder<WorkOrder> builder)
    {
        builder.ToTable("WorkOrders");

        builder.HasKey(workOrder => workOrder.Id);

        builder.Property(workOrder => workOrder.Number)
            .HasMaxLength(WorkOrder.NumberMaxLength)
            .IsRequired();
        builder.Property(workOrder => workOrder.Title)
            .HasMaxLength(WorkOrder.TitleMaxLength)
            .IsRequired();
        builder.Property(workOrder => workOrder.Description)
            .HasMaxLength(WorkOrder.DescriptionMaxLength);
        builder.Property(workOrder => workOrder.Priority)
            .HasConversion<int>();
        builder.Property(workOrder => workOrder.Status)
            .HasConversion<int>();

        // One aircraft has many work orders. Aircraft are only soft-deleted, and not while they
        // have open work orders, so the database never cascades.
        builder.HasOne<AircraftEntity>()
            .WithMany()
            .HasForeignKey(workOrder => workOrder.AircraftId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(workOrder => workOrder.Number)
            .HasDatabaseName(NumberIndexName)
            .IsUnique()
            .HasFilter(NotDeletedFilter);
        builder.HasIndex(workOrder => new { workOrder.AircraftId, workOrder.Status });
        builder.HasIndex(workOrder => new { workOrder.Status, workOrder.Priority });
    }
}
