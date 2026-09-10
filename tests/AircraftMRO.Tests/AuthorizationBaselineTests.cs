extern alias ApiHost;

using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AircraftMRO.Tests;

public sealed class AuthorizationBaselineTests
{
    private const string FakeConnectionString =
        "Server=invalid.example;Database=AircraftMROTests;Integrated Security=True;TrustServerCertificate=True";

    [Fact]
    public async Task Web_AnonymousRequestToHome_RedirectsToLogin()
    {
        await using var factory = CreateWebFactory();
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Web_AnonymousRequestToLogin_Succeeds()
    {
        await using var factory = CreateWebFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/Account/Login");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Api_AnonymousRequest_Returns401NotARedirect()
    {
        await using var factory = CreateApiFactory();
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/weatherforecast");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    private static WebApplicationFactory<Program> CreateWebFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(ConfigureForTesting);

    private static WebApplicationFactory<ApiHost::Program> CreateApiFactory() =>
        new WebApplicationFactory<ApiHost::Program>().WithWebHostBuilder(ConfigureForTesting);

    private static void ConfigureForTesting(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:AircraftMRO", FakeConnectionString);
    }
}
