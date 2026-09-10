using AircraftMRO.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AircraftMRO.Infrastructure.Persistence.Features.Employees;

internal sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees");

        builder.HasKey(employee => employee.Id);

        builder.Property(employee => employee.UserId)
            .HasMaxLength(450)
            .IsRequired();
        builder.HasIndex(employee => employee.UserId)
            .IsUnique();

        builder.HasIndex(employee => employee.EmployeeId)
            .IsUnique();
    }
}
