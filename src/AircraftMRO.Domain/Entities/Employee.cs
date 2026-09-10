namespace AircraftMRO.Domain.Entities;

public sealed class Employee
{
    private Employee()
    {
    }

    public Employee(string userId, int employeeId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        if (employeeId <= 0)
        {
            throw new ArgumentException("Employee id must be a positive number.", nameof(employeeId));
        }

        Id = Guid.NewGuid();
        UserId = userId;
        EmployeeId = employeeId;
    }

    public Guid Id { get; private set; }
    public string UserId { get; private set; } = null!;
    public int EmployeeId { get; private set; }
}
