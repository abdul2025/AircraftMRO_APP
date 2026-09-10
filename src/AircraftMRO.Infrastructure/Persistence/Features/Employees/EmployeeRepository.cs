using AircraftMRO.Application.Features.Employees.Ports;
using AircraftMRO.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AircraftMRO.Infrastructure.Persistence.Features.Employees;

internal sealed class EmployeeRepository(AircraftMroDbContext dbContext) : IEmployeeRepository
{
    public Task<bool> ExistsByEmployeeIdAsync(int employeeId, CancellationToken cancellationToken) =>
        dbContext.Employees
            .AsNoTracking()
            .AnyAsync(employee => employee.EmployeeId == employeeId, cancellationToken);

    public async Task AddAsync(Employee employee, CancellationToken cancellationToken)
    {
        dbContext.Employees.Add(employee);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
