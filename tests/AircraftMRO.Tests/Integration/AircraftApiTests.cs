using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AircraftMRO.Tests.Support;

namespace AircraftMRO.Tests.Integration;

[Collection(SqlServerCollection.Name)]
public sealed class AircraftApiTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private ApiHostFactory _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _factory = new ApiHostFactory(await fixture.CreateDatabaseAsync());
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Full_crud_lifecycle_with_etags()
    {
        var registration = AircraftTestData.UniqueRegistration();
        var created = await _client.PostAsJsonAsync("/api/aircraft", Body(registration));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var location = created.Headers.Location!;
        var etag = created.Headers.ETag!;
        using (var json = await ReadJsonAsync(created))
        {
            Assert.Equal(registration, json.RootElement.GetProperty("registrationNumber").GetString());
            Assert.Equal("Active", json.RootElement.GetProperty("status").GetString());
        }

        var updated = await SendAsync(HttpMethod.Put, location, etag, Body(registration, status: "Grounded"));
        Assert.Equal(HttpStatusCode.NoContent, updated.StatusCode);

        var stale = await SendAsync(HttpMethod.Put, location, etag, Body(registration));
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        await AssertProblemAsync(stale, "Aircraft.ConcurrencyConflict");

        var current = await _client.GetAsync(location);
        using (var json = await ReadJsonAsync(current))
        {
            Assert.Equal("Grounded", json.RootElement.GetProperty("status").GetString());
            Assert.True(json.RootElement.TryGetProperty("updatedAtUtc", out var updatedAt) && updatedAt.ValueKind != JsonValueKind.Null);
        }

        var deleted = await SendAsync(HttpMethod.Delete, location, current.Headers.ETag!);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(location)).StatusCode);
    }

    [Fact]
    public async Task List_filters_by_status_and_search()
    {
        var registration = AircraftTestData.UniqueRegistration();
        await _client.PostAsJsonAsync("/api/aircraft", Body(registration, serial: "API-F1", status: "Retired"));

        using var retired = await ReadJsonAsync(await _client.GetAsync($"/api/aircraft?status=Retired&search={registration}"));
        using var active = await ReadJsonAsync(await _client.GetAsync($"/api/aircraft?status=Active&search={registration}"));

        Assert.Equal(1, retired.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(0, active.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Update_without_if_match_returns_428()
    {
        var created = await _client.PostAsJsonAsync("/api/aircraft", Body(AircraftTestData.UniqueRegistration()));

        var response = await _client.PutAsJsonAsync(created.Headers.Location, Body("HZ-NEW"));

        Assert.Equal((HttpStatusCode)428, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_registration_returns_409_problem()
    {
        var registration = AircraftTestData.UniqueRegistration();
        await _client.PostAsJsonAsync("/api/aircraft", Body(registration, serial: "A-1"));

        var response = await _client.PostAsJsonAsync("/api/aircraft", Body(registration, serial: "A-2"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertProblemAsync(response, "Aircraft.DuplicateRegistration");
    }

    [Fact]
    public async Task Invalid_body_returns_400_and_unknown_id_returns_404()
    {
        var invalid = await _client.PostAsJsonAsync("/api/aircraft", Body(AircraftTestData.UniqueRegistration()) with { YearOfManufacture = 1800 });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        await AssertProblemAsync(invalid, "Aircraft.Validation");

        var missing = await _client.PostAsJsonAsync("/api/aircraft", new { registrationNumber = "HZ-ONLY" });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);

        var notFound = await _client.GetAsync($"/api/aircraft/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
        await AssertProblemAsync(notFound, "Aircraft.NotFound");
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, Uri uri, EntityTagHeaderValue etag, object? body = null)
    {
        using var request = new HttpRequestMessage(method, uri);
        request.Headers.IfMatch.Add(etag);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await _client.SendAsync(request);
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, string code)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = await ReadJsonAsync(response);
        Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    private static RequestBody Body(string registration, string serial = "5123", string status = "Active") =>
        new(registration, "Airbus", "A320-214", serial, 2012, 31250.5m, status);

    private sealed record RequestBody(
        string RegistrationNumber,
        string Manufacturer,
        string Model,
        string SerialNumber,
        int YearOfManufacture,
        decimal TotalFlightHours,
        string Status);
}
