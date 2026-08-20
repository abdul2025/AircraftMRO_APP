using AircraftMRO.Application.SystemFeatures;
using AircraftMRO.Web.Controllers;
using AircraftMRO.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace AircraftMRO.Tests;

public sealed class HomeControllerTests
{
    [Fact]
    public async Task Index_MapsDatabaseFeaturesToLandingPageModel()
    {
        var reader = new StubSystemFeatureReader(
        [
            CreateFeature("Aircraft", "aircraft"),
            CreateFeature("Custom", "unknown-icon")
        ]);
        var controller = new HomeController(reader);

        var result = Assert.IsType<ViewResult>(
            await controller.Index(CancellationToken.None));
        var model = Assert.IsType<HomeIndexViewModel>(result.Model);

        Assert.Collection(
            model.Features,
            feature => Assert.Equal(SystemFeatureIcon.Aircraft, feature.Icon),
            feature => Assert.Equal(SystemFeatureIcon.Generic, feature.Icon));
    }

    [Fact]
    public async Task Index_WhenReaderReturnsNoFeatures_ReturnsEmptyModel()
    {
        var controller = new HomeController(new StubSystemFeatureReader([]));

        var result = Assert.IsType<ViewResult>(
            await controller.Index(CancellationToken.None));
        var model = Assert.IsType<HomeIndexViewModel>(result.Model);

        Assert.Empty(model.Features);
    }

    [Fact]
    public async Task Index_ForwardsRequestCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var reader = new StubSystemFeatureReader([]);
        var controller = new HomeController(reader);

        await controller.Index(cancellation.Token);

        Assert.Equal(cancellation.Token, reader.ReceivedCancellationToken);
    }

    [Theory]
    [InlineData(null, "Index", false)]
    [InlineData("Home", null, false)]
    [InlineData("Home", "Index", true)]
    public void SystemFeature_IsAvailableOnlyWithACompleteMvcDestination(
        string? controller,
        string? action,
        bool expectedAvailability)
    {
        var feature = new SystemFeatureViewModel(
            "Feature",
            "Description",
            SystemFeatureIcon.Generic,
            controller,
            action);

        Assert.Equal(expectedAvailability, feature.IsAvailable);
    }

    private static SystemFeatureListItem CreateFeature(string title, string iconKey) =>
        new(
            title.ToLowerInvariant(),
            title,
            $"{title} description",
            iconKey,
            null,
            null,
            "Coming soon",
            10);

    private sealed class StubSystemFeatureReader(
        IReadOnlyList<SystemFeatureListItem> features)
        : ISystemFeatureReader
    {
        public CancellationToken ReceivedCancellationToken { get; private set; }

        public Task<IReadOnlyList<SystemFeatureListItem>> ListVisibleAsync(
            CancellationToken cancellationToken)
        {
            ReceivedCancellationToken = cancellationToken;
            return Task.FromResult(features);
        }
    }
}
