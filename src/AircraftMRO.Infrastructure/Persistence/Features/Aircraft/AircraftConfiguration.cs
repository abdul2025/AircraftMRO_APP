using AircraftMRO.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AircraftEntity = AircraftMRO.Domain.Entities.Aircraft;

namespace AircraftMRO.Infrastructure.Persistence.Features.Aircraft;

internal sealed class AircraftConfiguration : IEntityTypeConfiguration<AircraftEntity>
{
    public const string RegistrationNumberIndexName = "IX_Aircraft_RegistrationNumber";
    public const string SerialNumberIndexName = "IX_Aircraft_ManufacturerId_SerialNumber";
    private const string NotDeletedFilter = "[IsDeleted] = 0";

    public void Configure(EntityTypeBuilder<AircraftEntity> builder)
    {
        builder.ToTable("Aircraft");

        builder.HasKey(aircraft => aircraft.Id);

        builder.Property(aircraft => aircraft.RegistrationNumber)
            .HasMaxLength(AircraftEntity.RegistrationNumberMaxLength)
            .IsRequired();
        builder.Property(aircraft => aircraft.SerialNumber)
            .HasMaxLength(AircraftEntity.SerialNumberMaxLength)
            .IsRequired();
        builder.Property(aircraft => aircraft.TotalFlightHours)
            .HasPrecision(9, 1);
        builder.Property(aircraft => aircraft.Status)
            .HasConversion<int>();

        // Manufacturers and models are only soft-deleted, and not while aircraft use them, so the
        // database never cascades.
        builder.HasOne<AircraftManufacturer>()
            .WithMany()
            .HasForeignKey(aircraft => aircraft.ManufacturerId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        // Referencing the model through (ManufacturerId, Id) makes the database reject a model that
        // belongs to another manufacturer.
        builder.HasOne<AircraftModel>()
            .WithMany()
            .HasForeignKey(aircraft => new { aircraft.ManufacturerId, aircraft.ModelId })
            .HasPrincipalKey(model => new { model.ManufacturerId, model.Id })
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        // Uniqueness applies to live aircraft only, so a soft-deleted registration can be reused.
        builder.HasIndex(aircraft => aircraft.RegistrationNumber)
            .HasDatabaseName(RegistrationNumberIndexName)
            .IsUnique()
            .HasFilter(NotDeletedFilter);
        builder.HasIndex(aircraft => new { aircraft.ManufacturerId, aircraft.SerialNumber })
            .HasDatabaseName(SerialNumberIndexName)
            .IsUnique()
            .HasFilter(NotDeletedFilter);
    }
}
