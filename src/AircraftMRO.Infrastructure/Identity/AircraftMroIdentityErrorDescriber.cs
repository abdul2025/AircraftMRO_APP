using Microsoft.AspNetCore.Identity;

namespace AircraftMRO.Infrastructure.Identity;

public sealed class AircraftMroIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DuplicateUserName(string userName) => new()
    {
        Code = nameof(DuplicateUserName),
        Description = $"An account with email '{userName}' already exists."
    };
}
