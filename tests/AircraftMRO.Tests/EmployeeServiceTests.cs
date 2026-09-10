using AircraftMRO.Application.Features.Employees;
using AircraftMRO.Application.Features.Employees.DTOs;
using AircraftMRO.Application.Features.Employees.Ports;
using AircraftMRO.Domain.Entities;
using Moq;

namespace AircraftMRO.Tests;

public sealed class EmployeeServiceTests
{
    [Fact]
    public async Task CreateEmployeeProfileAsync_WhenEmployeeIdIsNew_CallsAddAndReturnsSuccess()
    {
        var repository = new Mock<IEmployeeRepository>();
        repository
            .Setup(r => r.ExistsByEmployeeIdAsync(1001, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var service = new EmployeeService(repository.Object);
        var dto = new CreateEmployeeProfileDto("user-1", 1001);

        var result = await service.CreateEmployeeProfileAsync(dto, CancellationToken.None);

        Assert.True(result.IsSuccess);
        repository.Verify(
            r => r.AddAsync(
                It.Is<Employee>(e => e.UserId == "user-1" && e.EmployeeId == 1001),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateEmployeeProfileAsync_WhenEmployeeIdAlreadyExists_ReturnsFailureWithoutCallingAdd()
    {
        var repository = new Mock<IEmployeeRepository>();
        repository
            .Setup(r => r.ExistsByEmployeeIdAsync(1001, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var service = new EmployeeService(repository.Object);
        var dto = new CreateEmployeeProfileDto("user-1", 1001);

        var result = await service.CreateEmployeeProfileAsync(dto, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Employee.DuplicateEmployeeId", result.ErrorCode);
        repository.Verify(
            r => r.AddAsync(It.IsAny<Employee>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
