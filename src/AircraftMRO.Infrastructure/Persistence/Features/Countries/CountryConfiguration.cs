using AircraftMRO.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AircraftMRO.Infrastructure.Persistence.Features.Countries;

internal sealed class CountryConfiguration : IEntityTypeConfiguration<Country>
{
    public const string CodeIndexName = "IX_Countries_Code";
    private const string NotDeletedFilter = "[IsDeleted] = 0";

    public void Configure(EntityTypeBuilder<Country> builder)
    {
        builder.ToTable("Countries");

        builder.HasKey(country => country.Id);

        builder.Property(country => country.Name)
            .HasMaxLength(Country.NameMaxLength)
            .IsRequired();
        builder.Property(country => country.Code)
            .HasMaxLength(Country.CodeLength)
            .IsFixedLength()
            .IsRequired();

        // Uniqueness applies to live countries only, so a soft-deleted code can be reused.
        builder.HasIndex(country => country.Code)
            .HasDatabaseName(CodeIndexName)
            .IsUnique()
            .HasFilter(NotDeletedFilter);
        builder.HasIndex(country => country.Name);
    }
}
