using AircraftMRO.Application.Features.Employees.DTOs;
using AircraftMRO.Application.Features.Employees.Interfaces;
using AircraftMRO.Application.Features.Employees.Ports;
using AircraftMRO.Domain.Common.Results;
using AircraftMRO.Domain.Entities;

namespace AircraftMRO.Application.Features.Employees;

public sealed class EmployeeService(IEmployeeRepository repository) : IEmployeeService
{
    public async Task<Result> CreateEmployeeProfileAsync(
        CreateEmployeeProfileDto dto,
        CancellationToken cancellationToken)
    {
        if (await repository.ExistsByEmployeeIdAsync(dto.EmployeeId, cancellationToken))
        {
            return Result.Failure(
                "Employee.DuplicateEmployeeId",
                "An employee with this Employee ID already exists.");
        }

        var employee = new Employee(dto.UserId, dto.EmployeeId);
        await repository.AddAsync(employee, cancellationToken);

        return Result.Success();
    }
}
