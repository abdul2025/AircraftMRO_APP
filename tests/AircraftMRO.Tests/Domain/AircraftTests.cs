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
        var result = Aircraft.Create("  hz-abc ", AircraftTestData.ManufacturerId, AircraftTestData.ModelId, " 5123 ", 2012, 100.5m, AircraftStatus.Active, CurrentYear);

        Assert.True(result.IsSuccess);
        var aircraft = result.Value!;
        Assert.Equal("HZ-ABC", aircraft.RegistrationNumber);
        Assert.Equal(AircraftTestData.ManufacturerId, aircraft.ManufacturerId);
        Assert.Equal(AircraftTestData.ModelId, aircraft.ModelId);
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
        AssertInvalid(Aircraft.Create(registration, AircraftTestData.ManufacturerId, AircraftTestData.ModelId, "1", 2012, 0, AircraftStatus.Active, CurrentYear).ErrorCode);

    [Theory]
    [InlineData(1902)]
    [InlineData(CurrentYear + 2)]
    public void Create_rejects_year_out_of_range(int year) =>
        AssertInvalid(Aircraft.Create("HZ-ABC", AircraftTestData.ManufacturerId, AircraftTestData.ModelId, "1", year, 0, AircraftStatus.Active, CurrentYear).ErrorCode);

    [Theory]
    [InlineData(1903)]
    [InlineData(CurrentYear + 1)]
    public void Create_accepts_year_boundaries(int year) =>
        Assert.True(Aircraft.Create("HZ-ABC", AircraftTestData.ManufacturerId, AircraftTestData.ModelId, "1", year, 0, AircraftStatus.Active, CurrentYear).IsSuccess);

    [Theory]
    [InlineData("-0.1")]
    [InlineData("10.25")]
    [InlineData("100000000.0")]
    public void Create_rejects_invalid_flight_hours(string hours) =>
        AssertInvalid(Aircraft.Create("HZ-ABC", AircraftTestData.ManufacturerId, AircraftTestData.ModelId, "1", 2012, decimal.Parse(hours), AircraftStatus.Active, CurrentYear).ErrorCode);

    [Fact]
    public void Create_rejects_missing_references_and_oversized_text()
    {
        AssertInvalid(Aircraft.Create("HZ-ABC", Guid.Empty, AircraftTestData.ModelId, "1", 2012, 0, AircraftStatus.Active, CurrentYear).ErrorCode);
        AssertInvalid(Aircraft.Create("HZ-ABC", AircraftTestData.ManufacturerId, Guid.Empty, "1", 2012, 0, AircraftStatus.Active, CurrentYear).ErrorCode);
        AssertInvalid(Aircraft.Create("HZ-ABC", AircraftTestData.ManufacturerId, AircraftTestData.ModelId, new string('S', 51), 2012, 0, AircraftStatus.Active, CurrentYear).ErrorCode);
    }

    [Fact]
    public void Create_rejects_undefined_status() =>
        AssertInvalid(Aircraft.Create("HZ-ABC", AircraftTestData.ManufacturerId, AircraftTestData.ModelId, "1", 2012, 0, (AircraftStatus)99, CurrentYear).ErrorCode);

    [Fact]
    public void Update_changes_values_and_keeps_id()
    {
        var aircraft = AircraftTestData.NewAircraft();
        var id = aircraft.Id;

        var result = aircraft.Update("hz-xyz", AircraftTestData.OtherManufacturerId, AircraftTestData.OtherModelId, "999", 2015, 42m, AircraftStatus.Grounded, CurrentYear);

        Assert.True(result.IsSuccess);
        Assert.Equal(id, aircraft.Id);
        Assert.Equal("HZ-XYZ", aircraft.RegistrationNumber);
        Assert.Equal(AircraftStatus.Grounded, aircraft.Status);
    }

    [Fact]
    public void Failed_update_leaves_aircraft_unchanged()
    {
        var aircraft = AircraftTestData.NewAircraft();

        var result = aircraft.Update("HZ-XYZ", AircraftTestData.OtherManufacturerId, AircraftTestData.OtherModelId, "999", 1800, 42m, AircraftStatus.Grounded, CurrentYear);

        Assert.True(result.IsFailure);
        Assert.Equal("HZ-ABC", aircraft.RegistrationNumber);
        Assert.Equal(AircraftStatus.Active, aircraft.Status);
    }

    [Theory]
    [InlineData(AircraftStatus.Active, 1, true, AircraftStatus.Grounded)]
    [InlineData(AircraftStatus.RequiredMaintenance, 2, true, AircraftStatus.Grounded)]
    [InlineData(AircraftStatus.Active, 1, false, AircraftStatus.RequiredMaintenance)]
    [InlineData(AircraftStatus.Grounded, 1, false, AircraftStatus.RequiredMaintenance)]   // critical lowered to high
    [InlineData(AircraftStatus.Grounded, 0, false, AircraftStatus.Active)]          // last one closed
    [InlineData(AircraftStatus.RequiredMaintenance, 0, false, AircraftStatus.Active)]
    [InlineData(AircraftStatus.Active, 0, false, AircraftStatus.Active)]
    [InlineData(AircraftStatus.Retired, 0, false, AircraftStatus.Retired)]
    public void Open_work_orders_decide_the_status(
        AircraftStatus before,
        int openCount,
        bool anyCritical,
        AircraftStatus after)
    {
        var aircraft = Aircraft.Create("HZ-ABC", AircraftTestData.ManufacturerId, AircraftTestData.ModelId, "1", 2012, 0, before, CurrentYear).Value!;

        var result = aircraft.ApplyOpenWorkOrders(openCount, anyCritical);

        Assert.True(result.IsSuccess);
        Assert.Equal(after, aircraft.Status);
    }

    [Fact]
    public void Open_work_orders_are_rejected_for_a_retired_aircraft()
    {
        var aircraft = Aircraft.Create("HZ-ABC", AircraftTestData.ManufacturerId, AircraftTestData.ModelId, "1", 2012, 0, AircraftStatus.Retired, CurrentYear).Value!;

        var result = aircraft.ApplyOpenWorkOrders(1, anyCritical: true);

        Assert.Equal(Aircraft.RetiredErrorCode, result.ErrorCode);
        Assert.Equal(AircraftStatus.Retired, aircraft.Status);
    }

    private static void AssertInvalid(string? errorCode) =>
        Assert.Equal(Aircraft.ValidationErrorCode, errorCode);
}
