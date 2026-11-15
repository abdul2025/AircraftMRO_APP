using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AircraftMRO.Tests.Support;

namespace AircraftMRO.Tests.Integration;

[Collection(SqlServerCollection.Name)]
public sealed class ReferenceDataApiTests(SqlServerFixture fixture) : IAsyncLifetime
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
    public async Task Country_crud_with_etags_and_conflicts()
    {
        var created = await _client.PostAsJsonAsync("/api/countries", new { name = "Atlantis", code = "xa" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(created.Headers.ETag);
        using (var json = await ReadJsonAsync(created))
        {
            Assert.Equal("XA", json.RootElement.GetProperty("code").GetString());
            Assert.True(json.RootElement.GetProperty("isActive").GetBoolean());
        }

        var location = created.Headers.Location!;
        var body = new { name = "Atlantis", code = "XA", isActive = false };
        Assert.Equal((HttpStatusCode)428, (await _client.PutAsJsonAsync(location, body)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(HttpMethod.Put, location, created.Headers.ETag!, body)).StatusCode);

        var stale = await SendAsync(HttpMethod.Put, location, created.Headers.ETag!, body);
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        await AssertProblemAsync(stale, "Country.ConcurrencyConflict");

        var duplicate = await _client.PostAsJsonAsync("/api/countries", new { name = "Again", code = "XA" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        await AssertProblemAsync(duplicate, "Country.DuplicateCode");

        using var inactive = await ReadJsonAsync(await _client.GetAsync("/api/countries?isActive=false"));
        Assert.Equal("XA", inactive.RootElement.GetProperty("items")[0].GetProperty("code").GetString());

        var current = await _client.GetAsync(location);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(HttpMethod.Delete, location, current.Headers.ETag!)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(location)).StatusCode);
    }

    [Fact]
    public async Task Country_in_use_cannot_be_deleted()
    {
        var france = await FindIdAsync("/api/countries?search=France");
        var current = await _client.GetAsync($"/api/countries/{france}");

        var response = await SendAsync(HttpMethod.Delete, new Uri($"/api/countries/{france}", UriKind.Relative), current.Headers.ETag!);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertProblemAsync(response, "Country.InUse");
    }

    [Fact]
    public async Task Manufacturer_and_model_crud_and_aircraft_reference_rules()
    {
        var japan = await FindIdAsync("/api/countries?search=Japan");
        var manufacturer = await _client.PostAsJsonAsync("/api/aircraft-manufacturers",
            new { name = "Mitsubishi Aircraft", code = "MITAC", countryId = japan });
        Assert.Equal(HttpStatusCode.Created, manufacturer.StatusCode);
        Guid manufacturerId;
        using (var json = await ReadJsonAsync(manufacturer))
        {
            manufacturerId = json.RootElement.GetProperty("id").GetGuid();
            Assert.Equal("JP", json.RootElement.GetProperty("countryCode").GetString());
        }

        var model = await _client.PostAsJsonAsync("/api/aircraft-models",
            new { manufacturerId, name = "SpaceJet M90", modelCode = "MRJ90", aircraftCategory = "RegionalJet" });
        Assert.Equal(HttpStatusCode.Created, model.StatusCode);
        Guid modelId;
        using (var json = await ReadJsonAsync(model))
        {
            modelId = json.RootElement.GetProperty("id").GetGuid();
            Assert.Equal("Mitsubishi Aircraft", json.RootElement.GetProperty("manufacturerName").GetString());
            Assert.Equal("RegionalJet", json.RootElement.GetProperty("aircraftCategory").GetString());
        }

        using (var filtered = await ReadJsonAsync(await _client.GetAsync($"/api/aircraft-models?manufacturerId={manufacturerId}")))
        {
            Assert.Equal(1, filtered.RootElement.GetProperty("totalCount").GetInt32());
        }

        var mismatch = await _client.PostAsJsonAsync("/api/aircraft", AircraftBody(manufacturerId, AircraftTestData.ModelId));
        Assert.Equal(HttpStatusCode.BadRequest, mismatch.StatusCode);
        await AssertProblemAsync(mismatch, "Aircraft.ModelManufacturerMismatch");

        var aircraft = await _client.PostAsJsonAsync("/api/aircraft", AircraftBody(manufacturerId, modelId));
        Assert.Equal(HttpStatusCode.Created, aircraft.StatusCode);
        using (var json = await ReadJsonAsync(aircraft))
        {
            Assert.Equal("Mitsubishi Aircraft", json.RootElement.GetProperty("manufacturerName").GetString());
            Assert.Equal("SpaceJet M90", json.RootElement.GetProperty("modelName").GetString());
        }

        var modelEtag = (await _client.GetAsync($"/api/aircraft-models/{modelId}")).Headers.ETag!;
        var inUse = await SendAsync(HttpMethod.Delete, new Uri($"/api/aircraft-models/{modelId}", UriKind.Relative), modelEtag);
        Assert.Equal(HttpStatusCode.Conflict, inUse.StatusCode);
        await AssertProblemAsync(inUse, "AircraftModel.InUse");

        var manufacturerEtag = (await _client.GetAsync($"/api/aircraft-manufacturers/{manufacturerId}")).Headers.ETag!;
        var deactivated = await SendAsync(
            HttpMethod.Put,
            new Uri($"/api/aircraft-manufacturers/{manufacturerId}", UriKind.Relative),
            manufacturerEtag,
            new { name = "Mitsubishi Aircraft", code = "MITAC", countryId = japan, isActive = false });
        Assert.Equal(HttpStatusCode.NoContent, deactivated.StatusCode);

        var underInactive = await _client.PostAsJsonAsync("/api/aircraft-models",
            new { manufacturerId, name = "SpaceJet M100", modelCode = "M100", aircraftCategory = "RegionalJet" });
        Assert.Equal(HttpStatusCode.BadRequest, underInactive.StatusCode);
        await AssertProblemAsync(underInactive, "AircraftModel.ManufacturerInactive");
    }

    [Fact]
    public async Task Missing_or_unknown_references_are_rejected()
    {
        var noCountry = await _client.PostAsJsonAsync("/api/aircraft-manufacturers", new { name = "Nowhere", code = "NOW" });
        Assert.Equal(HttpStatusCode.BadRequest, noCountry.StatusCode);

        var unknownCountry = await _client.PostAsJsonAsync("/api/aircraft-manufacturers",
            new { name = "Nowhere", code = "NOW", countryId = Guid.NewGuid() });
        await AssertProblemAsync(unknownCountry, "AircraftManufacturer.CountryNotFound");

        var unknownModel = await _client.PostAsJsonAsync("/api/aircraft", AircraftBody(AircraftTestData.ManufacturerId, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.BadRequest, unknownModel.StatusCode);
        await AssertProblemAsync(unknownModel, "Aircraft.ModelNotFound");

        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/aircraft-models/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/aircraft-manufacturers/{Guid.NewGuid()}")).StatusCode);
    }

    private static object AircraftBody(Guid manufacturerId, Guid modelId) => new
    {
        registrationNumber = AircraftTestData.UniqueRegistration(),
        manufacturerId,
        modelId,
        serialNumber = Guid.NewGuid().ToString("N")[..8],
        yearOfManufacture = 2015,
        totalFlightHours = 100m,
        status = "Active"
    };

    private async Task<Guid> FindIdAsync(string url)
    {
        using var json = await ReadJsonAsync(await _client.GetAsync(url));
        return json.RootElement.GetProperty("items")[0].GetProperty("id").GetGuid();
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
}
