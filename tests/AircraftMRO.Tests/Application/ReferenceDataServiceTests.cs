using AircraftMRO.Application.Features.AircraftManufacturers;
using AircraftMRO.Application.Features.AircraftManufacturers.DTOs;
using AircraftMRO.Application.Features.AircraftManufacturers.Ports;
using AircraftMRO.Application.Features.AircraftModels;
using AircraftMRO.Application.Features.AircraftModels.DTOs;
using AircraftMRO.Application.Features.AircraftModels.Ports;
using AircraftMRO.Application.Features.Countries;
using AircraftMRO.Application.Features.Countries.DTOs;
using AircraftMRO.Application.Features.Countries.Ports;
using AircraftMRO.Domain.Common.Results;
using AircraftMRO.Domain.Entities;
using AircraftMRO.Domain.Enums.AircraftModels;
using Moq;

namespace AircraftMRO.Tests.Application;

/// <summary>Use-case rules for countries, manufacturers, and aircraft models.</summary>
public sealed class ReferenceDataServiceTests
{
    private static readonly Guid CountryId = Guid.NewGuid();
    private static readonly Guid ManufacturerId = Guid.NewGuid();

    [Fact]
    public async Task Country_create_rejects_a_duplicate_code_without_saving()
    {
        var repository = new Mock<ICountryRepository>();
        repository.Setup(r => r.CodeExistsAsync("SA", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await new CountryService(repository.Object)
            .CreateAsync(new CreateCountryDto("Saudi Arabia", "sa", true), CancellationToken.None);

        Assert.Equal(CountryErrors.DuplicateCode, result.ErrorCode);
        repository.Verify(r => r.AddAsync(It.IsAny<Country>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Country_delete_is_blocked_while_a_manufacturer_uses_it()
    {
        var repository = new Mock<ICountryRepository>();
        var country = Country.Create("France", "FR", true).Value!;
        repository.Setup(r => r.GetForUpdateAsync(country.Id, It.IsAny<CancellationToken>())).ReturnsAsync(country);
        repository.Setup(r => r.IsInUseAsync(country.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await new CountryService(repository.Object).DeleteAsync(country.Id, [1], CancellationToken.None);

        Assert.Equal(CountryErrors.InUse, result.ErrorCode);
        repository.Verify(r => r.DeleteAsync(It.IsAny<Country>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(false, true, AircraftManufacturerErrors.CountryNotFound)]
    [InlineData(true, false, AircraftManufacturerErrors.CountryInactive)]
    [InlineData(true, true, null)]
    public async Task Manufacturer_create_needs_an_existing_active_country(bool exists, bool active, string? expected)
    {
        var repository = ManufacturerRepository(exists ? new CountryOptionDto(CountryId, "France", "FR", active) : null);

        var result = await new AircraftManufacturerService(repository.Object)
            .CreateAsync(new CreateAircraftManufacturerDto("Airbus", "AIB", CountryId, true), CancellationToken.None);

        Assert.Equal(expected, result.ErrorCode);
    }

    [Fact]
    public async Task Manufacturer_update_keeps_an_inactive_country_it_already_has()
    {
        var repository = ManufacturerRepository(new CountryOptionDto(CountryId, "France", "FR", IsActive: false));
        var manufacturer = AircraftManufacturer.Create("Airbus", "AIB", CountryId, true).Value!;
        repository.Setup(r => r.GetForUpdateAsync(manufacturer.Id, It.IsAny<CancellationToken>())).ReturnsAsync(manufacturer);

        var result = await new AircraftManufacturerService(repository.Object).UpdateAsync(
            manufacturer.Id,
            new UpdateAircraftManufacturerDto("Airbus SE", "AIB", CountryId, true, [1]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Airbus SE", manufacturer.Name);
    }

    [Fact]
    public async Task Manufacturer_delete_is_blocked_while_models_or_aircraft_use_it()
    {
        var repository = ManufacturerRepository(null);
        var manufacturer = AircraftManufacturer.Create("Airbus", "AIB", CountryId, true).Value!;
        repository.Setup(r => r.GetForUpdateAsync(manufacturer.Id, It.IsAny<CancellationToken>())).ReturnsAsync(manufacturer);
        repository.Setup(r => r.IsInUseAsync(manufacturer.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await new AircraftManufacturerService(repository.Object)
            .DeleteAsync(manufacturer.Id, [1], CancellationToken.None);

        Assert.Equal(AircraftManufacturerErrors.InUse, result.ErrorCode);
    }

    [Theory]
    [InlineData(false, true, AircraftModelErrors.ManufacturerNotFound)]
    [InlineData(true, false, AircraftModelErrors.ManufacturerInactive)]
    [InlineData(true, true, null)]
    public async Task Model_create_needs_an_existing_active_manufacturer(bool exists, bool active, string? expected)
    {
        var repository = ModelRepository(exists ? new ManufacturerOptionDto(ManufacturerId, "Airbus", active) : null);

        var result = await new AircraftModelService(repository.Object).CreateAsync(
            new CreateAircraftModelDto(ManufacturerId, "A320", "A320", AircraftCategory.NarrowBody, true),
            CancellationToken.None);

        Assert.Equal(expected, result.ErrorCode);
    }

    [Fact]
    public async Task Model_code_must_be_unique_within_its_manufacturer()
    {
        var repository = ModelRepository(new ManufacturerOptionDto(ManufacturerId, "Airbus", true));
        repository.Setup(r => r.ModelCodeExistsAsync(ManufacturerId, "A320", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await new AircraftModelService(repository.Object).CreateAsync(
            new CreateAircraftModelDto(ManufacturerId, "A320", "a320", AircraftCategory.NarrowBody, true),
            CancellationToken.None);

        Assert.Equal(AircraftModelErrors.DuplicateModelCode, result.ErrorCode);
    }

    [Fact]
    public async Task Model_delete_is_blocked_while_aircraft_use_it()
    {
        var repository = ModelRepository(null);
        var model = AircraftModel.Create(ManufacturerId, "A320", "A320", AircraftCategory.NarrowBody, true).Value!;
        repository.Setup(r => r.GetForUpdateAsync(model.Id, It.IsAny<CancellationToken>())).ReturnsAsync(model);
        repository.Setup(r => r.IsInUseAsync(model.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await new AircraftModelService(repository.Object).DeleteAsync(model.Id, [1], CancellationToken.None);

        Assert.Equal(AircraftModelErrors.InUse, result.ErrorCode);
    }

    private static Mock<IAircraftManufacturerRepository> ManufacturerRepository(CountryOptionDto? country)
    {
        var repository = new Mock<IAircraftManufacturerRepository>();
        repository.Setup(r => r.GetCountryAsync(CountryId, It.IsAny<CancellationToken>())).ReturnsAsync(country);
        repository.Setup(r => r.AddAsync(It.IsAny<AircraftManufacturer>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());
        repository.Setup(r => r.UpdateAsync(It.IsAny<AircraftManufacturer>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        return repository;
    }

    private static Mock<IAircraftModelRepository> ModelRepository(ManufacturerOptionDto? manufacturer)
    {
        var repository = new Mock<IAircraftModelRepository>();
        repository.Setup(r => r.GetManufacturerAsync(ManufacturerId, It.IsAny<CancellationToken>())).ReturnsAsync(manufacturer);
        repository.Setup(r => r.AddAsync(It.IsAny<AircraftModel>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());
        return repository;
    }
}
