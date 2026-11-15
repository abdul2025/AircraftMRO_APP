using AircraftMRO.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AircraftMRO.Infrastructure.Persistence.Features.AircraftModels;

internal sealed class AircraftModelConfiguration : IEntityTypeConfiguration<AircraftModel>
{
    public const string ModelCodeIndexName = "IX_AircraftModels_ManufacturerId_ModelCode";
    private const string NotDeletedFilter = "[IsDeleted] = 0";

    public void Configure(EntityTypeBuilder<AircraftModel> builder)
    {
        builder.ToTable("AircraftModels");

        builder.HasKey(model => model.Id);

        builder.Property(model => model.Name)
            .HasMaxLength(AircraftModel.NameMaxLength)
            .IsRequired();
        builder.Property(model => model.ModelCode)
            .HasMaxLength(AircraftModel.ModelCodeMaxLength)
            .IsRequired();
        builder.Property(model => model.AircraftCategory)
            .HasConversion<int>();

        // One manufacturer has many models. Manufacturers are only soft-deleted, and not while they
        // have models, so the database never cascades.
        builder.HasOne<AircraftManufacturer>()
            .WithMany()
            .HasForeignKey(model => model.ManufacturerId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        // Lets Aircraft reference (ManufacturerId, ModelId) as one key, so the database rejects an
        // aircraft whose model belongs to a different manufacturer.
        builder.HasAlternateKey(model => new { model.ManufacturerId, model.Id });

        builder.HasIndex(model => new { model.ManufacturerId, model.ModelCode })
            .HasDatabaseName(ModelCodeIndexName)
            .IsUnique()
            .HasFilter(NotDeletedFilter);
    }
}
