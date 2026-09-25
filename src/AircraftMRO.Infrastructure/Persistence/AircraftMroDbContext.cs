using System.Linq.Expressions;
using AircraftMRO.Domain.Common.Entities;
using AircraftMRO.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AircraftMRO.Infrastructure.Persistence;

public sealed class AircraftMroDbContext(DbContextOptions<AircraftMroDbContext> options) : DbContext(options)
{
    public const string RowVersionPropertyName = "RowVersion";
    public const int UserIdMaxLength = 450;

    public DbSet<Aircraft> Aircraft => Set<Aircraft>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AircraftMroDbContext).Assembly);
        ConfigureAuditableEntities(modelBuilder);
    }

    /// <summary>
    /// Every entity deriving from <see cref="AuditableEntity"/> gets the audit columns,
    /// a row-version concurrency token, and a filter that hides soft-deleted rows.
    /// </summary>
    private static void ConfigureAuditableEntities(ModelBuilder modelBuilder)
    {
        var auditableTypes = modelBuilder.Model.GetEntityTypes()
            .Where(type => typeof(AuditableEntity).IsAssignableFrom(type.ClrType) && type.BaseType is null)
            .Select(type => type.ClrType)
            .ToList();

        foreach (var clrType in auditableTypes)
        {
            var entity = modelBuilder.Entity(clrType);

            // Generated on Add as SQL Server-ordered sequential GUIDs, which keeps clustered-index inserts at the end.
            entity.Property(nameof(BaseEntity.Id)).ValueGeneratedOnAdd();
            entity.Property(nameof(AuditableEntity.CreatedAtUtc)).IsRequired();
            entity.Property(nameof(AuditableEntity.CreatedBy)).HasMaxLength(UserIdMaxLength);
            entity.Property(nameof(AuditableEntity.UpdatedBy)).HasMaxLength(UserIdMaxLength);
            entity.Property(nameof(AuditableEntity.DeletedBy)).HasMaxLength(UserIdMaxLength);
            entity.Property(nameof(AuditableEntity.IsDeleted)).HasDefaultValue(false);
            entity.Property<byte[]>(RowVersionPropertyName).IsRowVersion();

            var parameter = Expression.Parameter(clrType, "entity");
            var notDeleted = Expression.Lambda(
                Expression.Not(Expression.Property(parameter, nameof(AuditableEntity.IsDeleted))),
                parameter);
            entity.HasQueryFilter(notDeleted);
        }
    }
}
