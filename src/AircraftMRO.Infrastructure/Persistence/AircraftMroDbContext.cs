using AircraftMRO.Domain.Entities;
using AircraftMRO.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AircraftMRO.Infrastructure.Persistence;

public sealed class AircraftMroDbContext(
    DbContextOptions<AircraftMroDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, string>(options)
{
    public DbSet<Employee> Employees => Set<Employee>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AircraftMroDbContext).Assembly);
    }
}
