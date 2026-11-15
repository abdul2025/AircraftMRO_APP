using AircraftMRO.Domain.Entities;

namespace AircraftMRO.Tests.Domain;

public sealed class AircraftManufacturerTests
{
    private static readonly Guid CountryId = Guid.NewGuid();

    [Fact]
    public void Create_trims_name_and_upper_cases_code()
    {
        var result = AircraftManufacturer.Create(" Airbus ", " aib ", CountryId, isActive: true);

        Assert.True(result.IsSuccess);
        Assert.Equal("Airbus", result.Value!.Name);
        Assert.Equal("AIB", result.Value.Code);
        Assert.Equal(CountryId, result.Value.CountryId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("AIRBUS-HELI")]  // 11 characters
    [InlineData("AI B")]
    [InlineData("-AIB")]
    [InlineData("AIB--X")]
    public void Create_rejects_invalid_codes(string code) =>
        Assert.Equal(
            AircraftManufacturer.ValidationErrorCode,
            AircraftManufacturer.Create("Airbus", code, CountryId, true).ErrorCode);

    [Fact]
    public void Create_and_update_require_a_country()
    {
        Assert.Equal(
            AircraftManufacturer.ValidationErrorCode,
            AircraftManufacturer.Create("Airbus", "AIB", Guid.Empty, true).ErrorCode);

        var manufacturer = AircraftManufacturer.Create("Airbus", "AIB", CountryId, true).Value!;
        Assert.True(manufacturer.Update("Airbus", "AIB", Guid.Empty, true).IsFailure);
        Assert.Equal(CountryId, manufacturer.CountryId);
    }

    [Fact]
    public void Update_can_deactivate()
    {
        var manufacturer = AircraftManufacturer.Create("Airbus", "AIB", CountryId, true).Value!;

        Assert.True(manufacturer.Update("Airbus", "AIB", CountryId, isActive: false).IsSuccess);
        Assert.False(manufacturer.IsActive);
    }
}
