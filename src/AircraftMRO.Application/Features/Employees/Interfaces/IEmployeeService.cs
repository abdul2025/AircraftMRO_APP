using AircraftMRO.Application.Features.Employees.DTOs;
using AircraftMRO.Domain.Common.Results;

namespace AircraftMRO.Application.Features.Employees.Interfaces;

public interface IEmployeeService
{
    Task<Result> CreateEmployeeProfileAsync(
        CreateEmployeeProfileDto dto,
        CancellationToken cancellationToken);
}
