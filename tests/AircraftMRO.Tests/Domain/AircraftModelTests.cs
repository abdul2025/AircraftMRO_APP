using AircraftMRO.Domain.Entities;
using AircraftMRO.Domain.Enums.AircraftModels;

namespace AircraftMRO.Tests.Domain;

public sealed class AircraftModelTests
{
    private static readonly Guid ManufacturerId = Guid.NewGuid();

    [Fact]
    public void Create_keeps_manufacturer_trims_name_and_upper_cases_code()
    {
        var result = AircraftModel.Create(ManufacturerId, " A320neo ", " a320-251n ", AircraftCategory.NarrowBody, true);

        Assert.True(result.IsSuccess);
        var model = result.Value!;
        Assert.Equal(ManufacturerId, model.ManufacturerId);
        Assert.Equal("A320neo", model.Name);
        Assert.Equal("A320-251N", model.ModelCode);
        Assert.Equal(AircraftCategory.NarrowBody, model.AircraftCategory);
    }

    [Fact]
    public void Create_requires_a_manufacturer() =>
        Assert.Equal(
            AircraftModel.ValidationErrorCode,
            AircraftModel.Create(Guid.Empty, "A320", "A320", AircraftCategory.NarrowBody, true).ErrorCode);

    [Theory]
    [InlineData("")]
    [InlineData("A320 NEO")]
    [InlineData("A320-251N-EXTRA-LONG-")]
    public void Create_rejects_invalid_model_codes(string code) =>
        Assert.Equal(
            AircraftModel.ValidationErrorCode,
            AircraftModel.Create(ManufacturerId, "A320", code, AircraftCategory.NarrowBody, true).ErrorCode);

    [Fact]
    public void Create_rejects_undefined_category() =>
        Assert.Equal(
            AircraftModel.ValidationErrorCode,
            AircraftModel.Create(ManufacturerId, "A320", "A320", (AircraftCategory)99, true).ErrorCode);

    [Fact]
    public void Update_never_changes_the_manufacturer()
    {
        var model = AircraftModel.Create(ManufacturerId, "A320", "A320", AircraftCategory.NarrowBody, true).Value!;

        Assert.True(model.Update("A321", "A321", AircraftCategory.NarrowBody, false).IsSuccess);
        Assert.Equal(ManufacturerId, model.ManufacturerId);
        Assert.Equal("A321", model.Name);
        Assert.False(model.IsActive);
    }
}
