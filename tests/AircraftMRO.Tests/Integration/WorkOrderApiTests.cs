using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AircraftMRO.Tests.Support;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AircraftMRO.Tests.Integration;

[Collection(SqlServerCollection.Name)]
public sealed class WorkOrderApiTests(SqlServerFixture fixture) : IAsyncLifetime
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
    public async Task Lifecycle_with_etags_grounds_the_aircraft_until_the_work_is_completed()
    {
        var (aircraftUri, aircraftId, registration) = await CreateAircraftAsync("Active");

        var created = await _client.PostAsJsonAsync("/api/work-orders", new { aircraftId, title = "Engine bleed fault", priority = "Critical", dueDate = "2026-10-01" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var location = created.Headers.Location!;
        using (var json = await ReadJsonAsync(created))
        {
            Assert.Matches("^WO-\\d{6}$", json.RootElement.GetProperty("number").GetString());
            Assert.Equal("Open", json.RootElement.GetProperty("status").GetString());
            Assert.Equal("2026-10-01", json.RootElement.GetProperty("dueDate").GetString());
        }

        Assert.Equal("Grounded", await StatusOfAsync(aircraftUri));

        // The aircraft cannot be returned to service while the critical work order is open.
        var aircraft = await _client.GetAsync(aircraftUri);
        var release = await SendAsync(HttpMethod.Put, aircraftUri, aircraft.Headers.ETag!, AircraftBody(registration, "Active"));
        Assert.Equal(HttpStatusCode.Conflict, release.StatusCode);
        await AssertProblemAsync(release, "Aircraft.OpenCriticalWorkOrders");

        var etag = created.Headers.ETag!;
        var started = await SendAsync(HttpMethod.Put, location, etag,
            new { title = "Engine bleed fault", priority = "Critical", status = "InProgress" });
        Assert.Equal(HttpStatusCode.NoContent, started.StatusCode);

        var stale = await SendAsync(HttpMethod.Put, location, etag, new { title = "x", priority = "Low", status = "Open" });
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        await AssertProblemAsync(stale, "WorkOrder.ConcurrencyConflict");

        var openDelete = await SendAsync(HttpMethod.Delete, location, (await _client.GetAsync(location)).Headers.ETag!);
        Assert.Equal(HttpStatusCode.Conflict, openDelete.StatusCode);
        await AssertProblemAsync(openDelete, "WorkOrder.OpenCannotBeDeleted");

        var completed = await SendAsync(HttpMethod.Put, location, (await _client.GetAsync(location)).Headers.ETag!,
            new { title = "Engine bleed fault", priority = "Critical", status = "Completed" });
        Assert.Equal(HttpStatusCode.NoContent, completed.StatusCode);
        Assert.Equal("Active", await StatusOfAsync(aircraftUri));

        var current = await _client.GetAsync(location);
        var reopen = await SendAsync(HttpMethod.Put, location, current.Headers.ETag!, new { title = "x", priority = "Low", status = "Open" });
        Assert.Equal(HttpStatusCode.Conflict, reopen.StatusCode);
        await AssertProblemAsync(reopen, "WorkOrder.Closed");

        var deleted = await SendAsync(HttpMethod.Delete, location, current.Headers.ETag!);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(location)).StatusCode);
    }

    [Fact]
    public async Task Open_work_blocks_manual_status_changes_and_deletion_of_the_aircraft()
    {
        var (aircraftUri, aircraftId, registration) = await CreateAircraftAsync("Active");
        await _client.PostAsJsonAsync("/api/work-orders", new { aircraftId, title = "Cabin panel", priority = "Low" });
        Assert.Equal("InMaintenance", await StatusOfAsync(aircraftUri));

        var aircraft = await _client.GetAsync(aircraftUri);
        var activate = await SendAsync(HttpMethod.Put, aircraftUri, aircraft.Headers.ETag!, AircraftBody(registration, "Active"));
        Assert.Equal(HttpStatusCode.Conflict, activate.StatusCode);
        await AssertProblemAsync(activate, "Aircraft.OpenWorkOrders");

        var ground = await SendAsync(HttpMethod.Put, aircraftUri, aircraft.Headers.ETag!, AircraftBody(registration, "Grounded"));
        Assert.Equal(HttpStatusCode.Conflict, ground.StatusCode);
        await AssertProblemAsync(ground, "Aircraft.OpenWorkOrders");

        var delete = await SendAsync(HttpMethod.Delete, aircraftUri, aircraft.Headers.ETag!);
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
        await AssertProblemAsync(delete, "Aircraft.HasOpenWorkOrders");
    }

    [Fact]
    public async Task Rejects_retired_or_unknown_aircraft_and_invalid_bodies()
    {
        var (_, retiredId, _) = await CreateAircraftAsync("Retired");

        var retired = await _client.PostAsJsonAsync("/api/work-orders", new { aircraftId = retiredId, title = "x", priority = "Low" });
        Assert.Equal(HttpStatusCode.Conflict, retired.StatusCode);
        await AssertProblemAsync(retired, "Aircraft.Retired");

        var unknown = await _client.PostAsJsonAsync("/api/work-orders", new { aircraftId = Guid.NewGuid(), title = "x", priority = "Low" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, unknown.StatusCode);
        await AssertProblemAsync(unknown, "WorkOrder.AircraftNotFound");

        var missing = await _client.PostAsJsonAsync("/api/work-orders", new { title = "x" });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);

        var blankTitle = await _client.PostAsJsonAsync("/api/work-orders", new { aircraftId = retiredId, title = " ", priority = "Low" });
        Assert.Equal(HttpStatusCode.BadRequest, blankTitle.StatusCode);
    }

    [Fact]
    public async Task List_filters_and_statistics_cover_every_status_and_priority()
    {
        var (_, aircraftId, _) = await CreateAircraftAsync("Active");
        await _client.PostAsJsonAsync("/api/work-orders", new { aircraftId, title = "Overdue check", priority = "High", dueDate = "2026-01-01" });
        await _client.PostAsJsonAsync("/api/work-orders", new { aircraftId, title = "Future check", priority = "Low", dueDate = "2099-01-01" });

        using var overdue = await ReadJsonAsync(await _client.GetAsync($"/api/work-orders?aircraftId={aircraftId}&overdue=true"));
        Assert.Equal(1, overdue.RootElement.GetProperty("totalCount").GetInt32());
        Assert.True(overdue.RootElement.GetProperty("items")[0].GetProperty("isOverdue").GetBoolean());

        using var statistics = await ReadJsonAsync(await _client.GetAsync($"/api/work-orders/statistics?aircraftId={aircraftId}"));
        var root = statistics.RootElement;
        Assert.Equal(2, root.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, root.GetProperty("openCount").GetInt32());
        Assert.Equal(1, root.GetProperty("overdueCount").GetInt32());
        Assert.Equal(0, root.GetProperty("countByStatus").GetProperty("Cancelled").GetInt32());
        Assert.Equal(1, root.GetProperty("openCountByPriority").GetProperty("High").GetInt32());
        Assert.Equal(0, root.GetProperty("openCountByPriority").GetProperty("Critical").GetInt32());
    }

    [Theory]
    [InlineData("UTC", false)]
    [InlineData("Asia/Riyadh", true)]   // 22:30 UTC on 31 Dec is already 1 Jan in Riyadh
    public async Task Overdue_follows_the_configured_business_time_zone_end_to_end(string timeZone, bool expectedOverdue)
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 12, 31, 22, 30, 0, TimeSpan.Zero));
        await using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("WorkOrders:TimeZone", timeZone);
            builder.ConfigureTestServices(services => services.Replace(ServiceDescriptor.Singleton<TimeProvider>(clock)));
        });
        using var client = factory.CreateClient();
        var registration = AircraftTestData.UniqueRegistration();
        var aircraft = await client.PostAsJsonAsync("/api/aircraft", AircraftBody(registration, "Active"));
        using var aircraftJson = await ReadJsonAsync(aircraft);
        var aircraftId = aircraftJson.RootElement.GetProperty("id").GetGuid();

        var created = await client.PostAsJsonAsync("/api/work-orders", new { aircraftId, title = "Year-end check", priority = "Low", dueDate = "2026-12-31" });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var workOrder = await ReadJsonAsync(created);
        Assert.Equal(expectedOverdue, workOrder.RootElement.GetProperty("isOverdue").GetBoolean());
        using var overdue = await ReadJsonAsync(await client.GetAsync($"/api/work-orders?aircraftId={aircraftId}&overdue=true"));
        Assert.Equal(expectedOverdue ? 1 : 0, overdue.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Rejected_create_leaves_the_aircraft_unchanged()
    {
        var (aircraftUri, aircraftId, _) = await CreateAircraftAsync("Active");
        var etagBefore = (await _client.GetAsync(aircraftUri)).Headers.ETag!;

        // A numeric enum value the JSON converter accepts but the domain rejects.
        var invalid = await _client.PostAsJsonAsync("/api/work-orders", new { aircraftId, title = "x", priority = 99 });

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        await AssertProblemAsync(invalid, "WorkOrder.Validation");
        var after = await _client.GetAsync(aircraftUri);
        Assert.Equal(etagBefore, after.Headers.ETag);
        Assert.Equal("Active", await StatusOfAsync(aircraftUri));
        using var list = await ReadJsonAsync(await _client.GetAsync($"/api/work-orders?aircraftId={aircraftId}"));
        Assert.Equal(0, list.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Update_without_if_match_returns_428()
    {
        var (_, aircraftId, _) = await CreateAircraftAsync("Active");
        var created = await _client.PostAsJsonAsync("/api/work-orders", new { aircraftId, title = "x", priority = "Low" });

        var response = await _client.PutAsJsonAsync(created.Headers.Location, new { title = "y", priority = "Low", status = "Open" });

        Assert.Equal((HttpStatusCode)428, response.StatusCode);
        await AssertProblemAsync(response, "WorkOrder.PreconditionRequired");
    }

    private async Task<(Uri Location, Guid Id, string Registration)> CreateAircraftAsync(string status)
    {
        var registration = AircraftTestData.UniqueRegistration();
        var response = await _client.PostAsJsonAsync("/api/aircraft", AircraftBody(registration, status));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        return (response.Headers.Location!, json.RootElement.GetProperty("id").GetGuid(), registration);
    }

    private static object AircraftBody(string registration, string status) => new
    {
        registrationNumber = registration,
        manufacturer = "Airbus",
        model = "A320-214",
        serialNumber = registration,
        yearOfManufacture = 2012,
        totalFlightHours = 100m,
        status
    };

    private async Task<string> StatusOfAsync(Uri aircraftUri)
    {
        using var json = await ReadJsonAsync(await _client.GetAsync(aircraftUri));
        return json.RootElement.GetProperty("status").GetString()!;
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
