using AircraftMRO.Infrastructure.Identity;

namespace AircraftMRO.Tests;

public sealed class AircraftMroIdentityErrorDescriberTests
{
    [Fact]
    public void DuplicateUserName_DescribesTheConflictInTermsOfEmail()
    {
        var describer = new AircraftMroIdentityErrorDescriber();

        var error = describer.DuplicateUserName("ada@example.com");

        Assert.Equal("An account with email 'ada@example.com' already exists.", error.Description);
    }
}
