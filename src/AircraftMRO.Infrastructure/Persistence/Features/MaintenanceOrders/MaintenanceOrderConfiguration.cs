using AircraftMRO.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AircraftMRO.Infrastructure.Persistence.Features.MaintenanceOrders;

internal sealed class MaintenanceOrderConfiguration : IEntityTypeConfiguration<MaintenanceOrder>
{
    public void Configure(EntityTypeBuilder<MaintenanceOrder> builder)
    {
        builder.ToTable("MaintenanceOrders");

        builder.HasKey(maintenanceOrder => maintenanceOrder.Id);

        builder.Property(maintenanceOrder => maintenanceOrder.Type)
            .HasConversion<int>();
        builder.Property(maintenanceOrder => maintenanceOrder.Status)
            .HasConversion<int>();
        builder.Property(maintenanceOrder => maintenanceOrder.Notes)
            .HasMaxLength(MaintenanceOrder.NotesMaxLength);

        // One work order has many maintenance orders. Work orders are only soft-deleted, and only
        // once closed (which requires their maintenance to be finished), so the database never cascades.
        builder.HasOne<WorkOrder>()
            .WithMany()
            .HasForeignKey(maintenanceOrder => maintenanceOrder.WorkOrderId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(maintenanceOrder => new { maintenanceOrder.WorkOrderId, maintenanceOrder.Status });
        builder.HasIndex(maintenanceOrder => new { maintenanceOrder.Status, maintenanceOrder.ScheduledDate });
    }
}
