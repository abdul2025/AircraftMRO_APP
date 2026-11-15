using AircraftMRO.Domain.Entities;

namespace AircraftMRO.Tests.Domain;

public sealed class CountryTests
{
    [Fact]
    public void Create_trims_name_and_upper_cases_code()
    {
        var result = Country.Create("  Saudi Arabia ", " sa ", isActive: true);

        Assert.True(result.IsSuccess);
        Assert.Equal("Saudi Arabia", result.Value!.Name);
        Assert.Equal("SA", result.Value.Code);
        Assert.True(result.Value.IsActive);
    }

    [Theory]
    [InlineData("S")]
    [InlineData("SAU")]
    [InlineData("S1")]
    [InlineData("")]
    public void Create_rejects_a_code_that_is_not_two_letters(string code) =>
        Assert.Equal(Country.ValidationErrorCode, Country.Create("Saudi Arabia", code, true).ErrorCode);

    [Fact]
    public void Create_rejects_blank_and_oversized_names()
    {
        Assert.Equal(Country.ValidationErrorCode, Country.Create(" ", "SA", true).ErrorCode);
        Assert.Equal(Country.ValidationErrorCode, Country.Create(new string('N', Country.NameMaxLength + 1), "SA", true).ErrorCode);
    }

    [Fact]
    public void Failed_update_leaves_country_unchanged()
    {
        var country = Country.Create("France", "FR", true).Value!;

        var result = country.Update("Frankreich", "FRA", false);

        Assert.True(result.IsFailure);
        Assert.Equal("France", country.Name);
        Assert.True(country.IsActive);
    }
}
