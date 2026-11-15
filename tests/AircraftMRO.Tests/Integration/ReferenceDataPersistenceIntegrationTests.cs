using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.AircraftManufacturers;
using AircraftMRO.Application.Features.AircraftManufacturers.DTOs;
using AircraftMRO.Application.Features.AircraftModels;
using AircraftMRO.Application.Features.Countries;
using AircraftMRO.Domain.Entities;
using AircraftMRO.Domain.Enums.Aircraft;
using AircraftMRO.Domain.Enums.AircraftModels;
using AircraftMRO.Infrastructure.Persistence;
using AircraftMRO.Infrastructure.Persistence.Features.Aircraft;
using AircraftMRO.Infrastructure.Persistence.Features.AircraftManufacturers;
using AircraftMRO.Infrastructure.Persistence.Features.AircraftModels;
using AircraftMRO.Infrastructure.Persistence.Features.Countries;
using AircraftMRO.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace AircraftMRO.Tests.Integration;

[Collection(SqlServerCollection.Name)]
public sealed class ReferenceDataPersistenceIntegrationTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private string _connectionString = null!;

    public async Task InitializeAsync() => _connectionString = await fixture.CreateDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Migration_seeds_the_iso_country_list()
    {
        await using var context = NewContext();
        var codes = await context.Countries.Select(country => country.Code).ToListAsync();

        Assert.Equal(249, codes.Count);
        Assert.Equal(codes.Count, codes.Distinct().Count());
        Assert.Contains("SA", codes);
        Assert.True(await context.Countries.AllAsync(country => country.IsActive));
    }

    [Fact]
    public async Task Database_rejects_an_aircraft_whose_model_belongs_to_another_manufacturer()
    {
        await using var context = NewContext();
        context.Aircraft.Add(Aircraft.Create(
            "FK-1", AircraftTestData.ManufacturerId, AircraftTestData.OtherModelId, "FK-1", 2012, 0m,
            AircraftStatus.Active, AircraftTestData.CurrentYear).Value!);

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Contains("FK_Aircraft_AircraftModels_ManufacturerId_ModelId", exception.InnerException!.Message);
    }

    [Fact]
    public async Task Unique_indexes_become_duplicate_errors()
    {
        await using var context = NewContext();

        var country = await new CountryRepository(context)
            .AddAsync(Country.Create("Fakeland", "FR", true).Value!, CancellationToken.None);
        Assert.Equal(CountryErrors.DuplicateCode, country.ErrorCode);
        context.ChangeTracker.Clear();

        var countryId = await context.Countries.Where(c => c.Code == "FR").Select(c => c.Id).SingleAsync();
        var manufacturer = await new AircraftManufacturerRepository(context)
            .AddAsync(AircraftManufacturer.Create("Another Airbus", "AIB", countryId, true).Value!, CancellationToken.None);
        Assert.Equal(AircraftManufacturerErrors.DuplicateCode, manufacturer.ErrorCode);
        context.ChangeTracker.Clear();

        var models = new AircraftModelRepository(context);
        var duplicate = await models.AddAsync(
            AircraftModel.Create(AircraftTestData.ManufacturerId, "A320 again", "A320-214", AircraftCategory.NarrowBody, true).Value!,
            CancellationToken.None);
        Assert.Equal(AircraftModelErrors.DuplicateModelCode, duplicate.ErrorCode);
        context.ChangeTracker.Clear();

        // The model code is unique per manufacturer only.
        var otherManufacturer = await models.AddAsync(
            AircraftModel.Create(AircraftTestData.OtherManufacturerId, "A320-214", "A320-214", AircraftCategory.NarrowBody, true).Value!,
            CancellationToken.None);
        Assert.True(otherManufacturer.IsSuccess);
    }

    [Fact]
    public async Task Deactivating_is_saved()
    {
        await using (var context = NewContext())
        {
            var model = await context.AircraftModels.SingleAsync(m => m.Id == AircraftTestData.ModelId);
            var rowVersion = context.Entry(model).Property<byte[]>(AircraftMroDbContext.RowVersionPropertyName).CurrentValue!;
            model.Update(model.Name, model.ModelCode, model.AircraftCategory, isActive: false);
            Assert.True((await new AircraftModelRepository(context).UpdateAsync(model, rowVersion, CancellationToken.None)).IsSuccess);
        }

        await using var verify = NewContext();
        Assert.False(await verify.AircraftModels.Where(m => m.Id == AircraftTestData.ModelId).Select(m => m.IsActive).SingleAsync());
    }

    [Fact]
    public async Task Manufacturer_list_includes_one_without_a_country()
    {
        await using var context = NewContext();
        await context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO [AircraftManufacturers] ([Id], [Name], [Code], [CountryId], [IsActive], [CreatedAtUtc], [IsDeleted])
            VALUES (NEWID(), N'Legacy Works', N'LEGACY', NULL, 1, SYSDATETIMEOFFSET(), 0);
            """);

        var page = await new AircraftManufacturerRepository(context)
            .ListAsync(new AircraftManufacturerListFilter("legacy"), new PagedRequest(), CancellationToken.None);

        var legacy = Assert.Single(page.Items);
        Assert.Null(legacy.CountryName);
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task In_use_checks_follow_live_models_and_aircraft()
    {
        await using var context = NewContext();
        var manufacturers = new AircraftManufacturerRepository(context);
        var models = new AircraftModelRepository(context);
        var countries = new CountryRepository(context);
        var franceId = await context.Countries.Where(c => c.Code == "FR").Select(c => c.Id).SingleAsync();

        Assert.True(await countries.IsInUseAsync(franceId, CancellationToken.None));
        Assert.True(await manufacturers.IsInUseAsync(AircraftTestData.ManufacturerId, CancellationToken.None)); // has models
        Assert.False(await models.IsInUseAsync(AircraftTestData.ModelId, CancellationToken.None));

        context.Aircraft.Add(AircraftTestData.NewAircraft("IU-1", "IU-1"));
        await context.SaveChangesAsync();

        Assert.True(await models.IsInUseAsync(AircraftTestData.ModelId, CancellationToken.None));
    }

    [Fact]
    public async Task Form_options_hide_inactive_entries_unless_they_are_current()
    {
        await using (var setup = NewContext())
        {
            await setup.Database.ExecuteSqlRawAsync(
                "UPDATE [AircraftModels] SET [IsActive] = 0 WHERE [Id] = {0}", AircraftTestData.OtherModelId);
        }

        await using var context = NewContext();
        var repository = new AircraftRepository(context);

        var forNew = await repository.ListFormOptionsAsync(null, null, CancellationToken.None);
        Assert.DoesNotContain(forNew.Models, model => model.Id == AircraftTestData.OtherModelId);
        Assert.Contains(forNew.Models, model => model.Id == AircraftTestData.ModelId);

        var forCurrent = await repository.ListFormOptionsAsync(
            AircraftTestData.OtherManufacturerId, AircraftTestData.OtherModelId, CancellationToken.None);
        Assert.Contains(forCurrent.Models, model => model.Id == AircraftTestData.OtherModelId && !model.IsActive);

        var reference = await repository.GetModelReferenceAsync(AircraftTestData.OtherModelId, CancellationToken.None);
        Assert.Equal(AircraftTestData.OtherManufacturerId, reference!.ManufacturerId);
        Assert.False(reference.IsActive);
        Assert.True(reference.ManufacturerIsActive);
    }

    private AircraftMroDbContext NewContext() =>
        SqlServerFixture.CreateContext(_connectionString, new TestCurrentUser("user-1"), new FakeTimeProvider());
}
