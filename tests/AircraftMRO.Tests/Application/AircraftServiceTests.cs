using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.Aircraft;
using AircraftMRO.Application.Features.Aircraft.DTOs;
using AircraftMRO.Application.Features.Aircraft.Ports;
using AircraftMRO.Domain.Common.Results;
using AircraftMRO.Domain.Entities;
using AircraftMRO.Domain.Enums.Aircraft;
using AircraftMRO.Tests.Support;
using Moq;

namespace AircraftMRO.Tests.Application;

public sealed class AircraftServiceTests
{
    private readonly Mock<IAircraftRepository> _repository = new();
    private readonly AircraftService _service;

    public AircraftServiceTests()
    {
        _repository.Setup(r => r.AddAsync(It.IsAny<Aircraft>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());
        _repository.Setup(r => r.UpdateAsync(It.IsAny<Aircraft>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());
        _repository.Setup(r => r.DeleteAsync(It.IsAny<Aircraft>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());
        _service = new AircraftService(_repository.Object, new FakeTimeProvider());
    }

    [Fact]
    public async Task Create_saves_valid_aircraft_and_returns_its_id()
    {
        Aircraft? saved = null;
        _repository.Setup(r => r.AddAsync(It.IsAny<Aircraft>(), It.IsAny<CancellationToken>()))
            .Callback<Aircraft, CancellationToken>((a, _) => saved = a)
            .ReturnsAsync(Result.Success());

        var result = await _service.CreateAsync(AircraftTestData.CreateDto("hz-abc"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(saved);
        Assert.Equal(saved.Id, result.Value);
        Assert.Equal("HZ-ABC", saved.RegistrationNumber);
    }

    [Fact]
    public async Task Create_rejects_duplicate_registration_without_saving()
    {
        _repository.Setup(r => r.RegistrationNumberExistsAsync("HZ-ABC", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _service.CreateAsync(AircraftTestData.CreateDto("hz-abc"), CancellationToken.None);

        Assert.Equal(AircraftErrors.DuplicateRegistration, result.ErrorCode);
        _repository.Verify(r => r.AddAsync(It.IsAny<Aircraft>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_rejects_duplicate_serial_number()
    {
        _repository.Setup(r => r.SerialNumberExistsAsync("Airbus", "5123", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _service.CreateAsync(AircraftTestData.CreateDto(), CancellationToken.None);

        Assert.Equal(AircraftErrors.DuplicateSerialNumber, result.ErrorCode);
    }

    [Fact]
    public async Task Create_returns_validation_error_for_invalid_input()
    {
        var dto = AircraftTestData.CreateDto() with { YearOfManufacture = 1800 };

        var result = await _service.CreateAsync(dto, CancellationToken.None);

        Assert.Equal(AircraftErrors.Validation, result.ErrorCode);
        _repository.Verify(r => r.AddAsync(It.IsAny<Aircraft>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_uses_current_year_from_time_provider()
    {
        var dto = AircraftTestData.CreateDto() with { YearOfManufacture = 2027 };

        Assert.True((await _service.CreateAsync(dto, CancellationToken.None)).IsSuccess);
        Assert.Equal(AircraftErrors.Validation,
            (await _service.CreateAsync(dto with { YearOfManufacture = 2028 }, CancellationToken.None)).ErrorCode);
    }

    [Fact]
    public async Task Get_update_and_delete_return_not_found_for_unknown_id()
    {
        var id = Guid.NewGuid();

        Assert.Equal(AircraftErrors.NotFound, (await _service.GetByIdAsync(id, CancellationToken.None)).ErrorCode);
        Assert.Equal(AircraftErrors.NotFound, (await _service.UpdateAsync(id, UpdateDto(), CancellationToken.None)).ErrorCode);
        Assert.Equal(AircraftErrors.NotFound, (await _service.DeleteAsync(id, [1], CancellationToken.None)).ErrorCode);
    }

    [Fact]
    public async Task Update_excludes_own_id_from_uniqueness_check_and_passes_row_version()
    {
        var aircraft = AircraftTestData.NewAircraft();
        byte[] rowVersion = [1, 2, 3];
        _repository.Setup(r => r.GetForUpdateAsync(aircraft.Id, It.IsAny<CancellationToken>())).ReturnsAsync(aircraft);

        var result = await _service.UpdateAsync(aircraft.Id, UpdateDto(rowVersion) with { Status = AircraftStatus.Grounded }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AircraftStatus.Grounded, aircraft.Status);
        _repository.Verify(r => r.RegistrationNumberExistsAsync("HZ-ABC", aircraft.Id, It.IsAny<CancellationToken>()));
        _repository.Verify(r => r.UpdateAsync(aircraft, rowVersion, It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Update_passes_through_concurrency_conflict()
    {
        var aircraft = AircraftTestData.NewAircraft();
        _repository.Setup(r => r.GetForUpdateAsync(aircraft.Id, It.IsAny<CancellationToken>())).ReturnsAsync(aircraft);
        _repository.Setup(r => r.UpdateAsync(aircraft, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(AircraftErrors.ConcurrencyConflict, AircraftErrors.ConcurrencyConflictMessage));

        var result = await _service.UpdateAsync(aircraft.Id, UpdateDto(), CancellationToken.None);

        Assert.Equal(AircraftErrors.ConcurrencyConflict, result.ErrorCode);
    }

    [Fact]
    public async Task List_passes_request_and_cancellation_token_to_repository()
    {
        using var cts = new CancellationTokenSource();
        var request = new PagedRequest(3, 500);
        var filter = new AircraftListFilter("hz", AircraftStatus.Grounded);
        var page = new PagedResponse<AircraftListItemDto>([], 3, 100, 0);
        _repository.Setup(r => r.ListAsync(filter, request, cts.Token)).ReturnsAsync(page);

        var result = await _service.ListAsync(filter, request, cts.Token);

        Assert.Same(page, result.Value);
        Assert.Equal(100, request.PageSize);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData("  hz-abc ", "hz-abc")]
    public void ListFilter_trims_search_and_treats_blank_as_no_filter(string? search, string? expected)
    {
        var filter = new AircraftListFilter(search);

        Assert.Equal(expected, filter.Search);
        Assert.Equal(expected is null, filter.IsEmpty);
    }

    [Fact]
    public void ListFilter_caps_search_length_and_ignores_undefined_status()
    {
        var filter = new AircraftListFilter(new string('x', 500), (AircraftStatus)99);

        Assert.Equal(AircraftListFilter.MaxSearchLength, filter.Search!.Length);
        Assert.Null(filter.Status);
    }

    [Theory]
    [InlineData(0, 0, 1, 1)]
    [InlineData(-5, 1000, 1, 100)]
    [InlineData(4, 25, 4, 25)]
    [InlineData(int.MaxValue, 100, PagedRequest.MaxPage, 100)]
    public void PagedRequest_clamps_values(int page, int pageSize, int expectedPage, int expectedPageSize)
    {
        var request = new PagedRequest(page, pageSize);

        Assert.Equal(expectedPage, request.Page);
        Assert.Equal(expectedPageSize, request.PageSize);
        Assert.True(request.Skip >= 0);
    }

    private static UpdateAircraftDto UpdateDto(byte[]? rowVersion = null) =>
        new("HZ-ABC", "Airbus", "A320-214", "5123", 2012, 31300m, AircraftStatus.Active, rowVersion ?? [1]);
}
