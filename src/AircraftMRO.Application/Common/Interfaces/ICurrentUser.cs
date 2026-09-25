namespace AircraftMRO.Application.Common.Interfaces;

public interface ICurrentUser
{
    /// <summary>The authenticated user's id, or <c>null</c> when there is no authenticated user.</summary>
    string? UserId { get; }
}
