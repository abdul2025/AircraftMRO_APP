using AircraftMRO.Application.Common.Interfaces;

namespace AircraftMRO.Tests.Support;

public sealed class TestCurrentUser(string? userId = null) : ICurrentUser
{
    public string? UserId { get; set; } = userId;
}

public sealed class FakeTimeProvider(DateTimeOffset? now = null) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now ?? new DateTimeOffset(2026, 9, 25, 8, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => Now;
}
