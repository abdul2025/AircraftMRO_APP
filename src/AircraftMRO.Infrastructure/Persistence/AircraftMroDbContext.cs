using Microsoft.EntityFrameworkCore;

namespace AircraftMRO.Infrastructure.Persistence;

public sealed class AircraftMroDbContext(
    DbContextOptions<AircraftMroDbContext> options)
    : DbContext(options)
{
    internal DbSet<SystemFeatureRecord> SystemFeatures => Set<SystemFeatureRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AircraftMroDbContext).Assembly);
    }
}
