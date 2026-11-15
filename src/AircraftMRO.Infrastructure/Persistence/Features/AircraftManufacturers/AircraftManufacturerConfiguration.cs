using AircraftMRO.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AircraftMRO.Infrastructure.Persistence.Features.AircraftManufacturers;

internal sealed class AircraftManufacturerConfiguration : IEntityTypeConfiguration<AircraftManufacturer>
{
    public const string CodeIndexName = "IX_AircraftManufacturers_Code";
    private const string NotDeletedFilter = "[IsDeleted] = 0";

    public void Configure(EntityTypeBuilder<AircraftManufacturer> builder)
    {
        builder.ToTable("AircraftManufacturers");

        builder.HasKey(manufacturer => manufacturer.Id);

        builder.Property(manufacturer => manufacturer.Name)
            .HasMaxLength(AircraftManufacturer.NameMaxLength)
            .IsRequired();
        builder.Property(manufacturer => manufacturer.Code)
            .HasMaxLength(AircraftManufacturer.CodeMaxLength)
            .IsRequired();

        // Optional only for manufacturers carried over from free text; the domain requires it on every write.
        // Countries are only soft-deleted, and not while in use, so the database never cascades.
        builder.HasOne<Country>()
            .WithMany()
            .HasForeignKey(manufacturer => manufacturer.CountryId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(manufacturer => manufacturer.Code)
            .HasDatabaseName(CodeIndexName)
            .IsUnique()
            .HasFilter(NotDeletedFilter);
        builder.HasIndex(manufacturer => manufacturer.Name);
    }
}
