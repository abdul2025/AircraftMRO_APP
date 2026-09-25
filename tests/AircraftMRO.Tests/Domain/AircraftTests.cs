using AircraftMRO.Domain.Entities;
using AircraftMRO.Domain.Enums.Aircraft;
using AircraftMRO.Tests.Support;

namespace AircraftMRO.Tests.Domain;

public sealed class AircraftTests
{
    private const int CurrentYear = AircraftTestData.CurrentYear;

    [Fact]
    public void Create_normalizes_registration_and_trims_text()
    {
        var result = Aircraft.Create("  hz-abc ", " Airbus ", " A320 ", " 5123 ", 2012, 100.5m, AircraftStatus.Active, CurrentYear);

        Assert.True(result.IsSuccess);
        var aircraft = result.Value!;
        Assert.Equal("HZ-ABC", aircraft.RegistrationNumber);
        Assert.Equal("Airbus", aircraft.Manufacturer);
        Assert.Equal("A320", aircraft.Model);
        Assert.Equal("5123", aircraft.SerialNumber);
        Assert.Equal(Guid.Empty, aircraft.Id); // assigned by persistence on Add
    }

    [Theory]
    [InlineData("A")]
    [InlineData("HZ-ABCDEFGH")]
    [InlineData("HZ ABC")]
    [InlineData("HZ--ABC")]
    [InlineData("-HZABC")]
    [InlineData("")]
    public void Create_rejects_invalid_registration(string registration) =>
        AssertInvalid(Aircraft.Create(registration, "Airbus", "A320", "1", 2012, 0, AircraftStatus.Active, CurrentYear).ErrorCode);

    [Theory]
    [InlineData(1902)]
    [InlineData(CurrentYear + 2)]
    public void Create_rejects_year_out_of_range(int year) =>
        AssertInvalid(Aircraft.Create("HZ-ABC", "Airbus", "A320", "1", year, 0, AircraftStatus.Active, CurrentYear).ErrorCode);

    [Theory]
    [InlineData(1903)]
    [InlineData(CurrentYear + 1)]
    public void Create_accepts_year_boundaries(int year) =>
        Assert.True(Aircraft.Create("HZ-ABC", "Airbus", "A320", "1", year, 0, AircraftStatus.Active, CurrentYear).IsSuccess);

    [Theory]
    [InlineData("-0.1")]
    [InlineData("10.25")]
    [InlineData("100000000.0")]
    public void Create_rejects_invalid_flight_hours(string hours) =>
        AssertInvalid(Aircraft.Create("HZ-ABC", "Airbus", "A320", "1", 2012, decimal.Parse(hours), AircraftStatus.Active, CurrentYear).ErrorCode);

    [Fact]
    public void Create_rejects_blank_and_oversized_text()
    {
        AssertInvalid(Aircraft.Create("HZ-ABC", " ", "A320", "1", 2012, 0, AircraftStatus.Active, CurrentYear).ErrorCode);
        AssertInvalid(Aircraft.Create("HZ-ABC", "Airbus", new string('M', 101), "1", 2012, 0, AircraftStatus.Active, CurrentYear).ErrorCode);
        AssertInvalid(Aircraft.Create("HZ-ABC", "Airbus", "A320", new string('S', 51), 2012, 0, AircraftStatus.Active, CurrentYear).ErrorCode);
    }

    [Fact]
    public void Create_rejects_undefined_status() =>
        AssertInvalid(Aircraft.Create("HZ-ABC", "Airbus", "A320", "1", 2012, 0, (AircraftStatus)99, CurrentYear).ErrorCode);

    [Fact]
    public void Update_changes_values_and_keeps_id()
    {
        var aircraft = AircraftTestData.NewAircraft();
        var id = aircraft.Id;

        var result = aircraft.Update("hz-xyz", "Boeing", "737-800", "999", 2015, 42m, AircraftStatus.Grounded, CurrentYear);

        Assert.True(result.IsSuccess);
        Assert.Equal(id, aircraft.Id);
        Assert.Equal("HZ-XYZ", aircraft.RegistrationNumber);
        Assert.Equal(AircraftStatus.Grounded, aircraft.Status);
    }

    [Fact]
    public void Failed_update_leaves_aircraft_unchanged()
    {
        var aircraft = AircraftTestData.NewAircraft();

        var result = aircraft.Update("HZ-XYZ", "Boeing", "737", "999", 1800, 42m, AircraftStatus.Grounded, CurrentYear);

        Assert.True(result.IsFailure);
        Assert.Equal("HZ-ABC", aircraft.RegistrationNumber);
        Assert.Equal(AircraftStatus.Active, aircraft.Status);
    }

    private static void AssertInvalid(string? errorCode) =>
        Assert.Equal(Aircraft.ValidationErrorCode, errorCode);
}
