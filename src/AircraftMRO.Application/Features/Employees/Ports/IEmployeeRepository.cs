using AircraftMRO.Domain.Entities;

namespace AircraftMRO.Application.Features.Employees.Ports;

public interface IEmployeeRepository
{
    Task<bool> ExistsByEmployeeIdAsync(int employeeId, CancellationToken cancellationToken);

    Task AddAsync(Employee employee, CancellationToken cancellationToken);
}
