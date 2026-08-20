using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AircraftMRO.Infrastructure.Persistence;

internal sealed class SystemFeatureConfiguration
    : IEntityTypeConfiguration<SystemFeatureRecord>
{
    public void Configure(EntityTypeBuilder<SystemFeatureRecord> builder)
    {
        builder.ToTable("SystemFeatures", table =>
        {
            table.HasCheckConstraint(
                "CK_SystemFeatures_DisplayOrder_NonNegative",
                "[DisplayOrder] >= 0");
            table.HasCheckConstraint(
                "CK_SystemFeatures_Destination_Complete",
                "([ControllerName] IS NULL AND [ActionName] IS NULL) OR " +
                "([ControllerName] IS NOT NULL AND [ActionName] IS NOT NULL)");
        });

        builder.HasKey(feature => feature.Id);

        builder.Property(feature => feature.Code)
            .HasMaxLength(64)
            .IsRequired();
        builder.HasIndex(feature => feature.Code)
            .IsUnique();

        builder.Property(feature => feature.Title)
            .HasMaxLength(120)
            .IsRequired();
        builder.Property(feature => feature.Description)
            .HasMaxLength(600)
            .IsRequired();
        builder.Property(feature => feature.IconKey)
            .HasMaxLength(40)
            .IsRequired();
        builder.Property(feature => feature.ControllerName)
            .HasMaxLength(100);
        builder.Property(feature => feature.ActionName)
            .HasMaxLength(100);
        builder.Property(feature => feature.StatusText)
            .HasMaxLength(60)
            .IsRequired();
        builder.Property(feature => feature.IsVisible)
            .HasDefaultValue(true);

        builder.HasData(
            CreateFeature(
                1,
                "aircraft",
                "Aircraft",
                "Manage aircraft profiles, registration details, fleet status, and technical records.",
                "aircraft",
                10),
            CreateFeature(
                2,
                "work-orders",
                "Work Orders",
                "Plan, assign, and track maintenance work from discovery through release to service.",
                "work-order",
                20),
            CreateFeature(
                3,
                "maintenance-planning",
                "Maintenance Planning",
                "Coordinate scheduled tasks, due dates, labor, tooling, and material requirements.",
                "maintenance",
                30),
            CreateFeature(
                4,
                "compliance-records",
                "Compliance & Records",
                "Keep maintenance history, airworthiness evidence, and audit-ready records together.",
                "compliance",
                40));
    }

    private static SystemFeatureRecord CreateFeature(
        int id,
        string code,
        string title,
        string description,
        string iconKey,
        int displayOrder) =>
        new()
        {
            Id = id,
            Code = code,
            Title = title,
            Description = description,
            IconKey = iconKey,
            StatusText = "Coming soon",
            DisplayOrder = displayOrder,
            IsVisible = true
        };
}
