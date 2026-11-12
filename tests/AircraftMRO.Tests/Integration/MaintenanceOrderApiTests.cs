using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AircraftMRO.Tests.Support;

namespace AircraftMRO.Tests.Integration;

[Collection(SqlServerCollection.Name)]
public sealed class MaintenanceOrderApiTests(SqlServerFixture fixture) : IAsyncLifetime
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
    public async Task Lifecycle_with_etags_blocks_closing_the_work_order_until_maintenance_is_completed()
    {
        var (workOrderUri, workOrderId) = await CreateWorkOrderAsync();

        var created = await _client.PostAsJsonAsync("/api/maintenance-orders",
            new { workOrderId, type = "CCheck", scheduledDate = "2026-10-01", notes = "Hangar 2" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(created.Headers.ETag);
        var location = created.Headers.Location!;
        using (var json = await ReadJsonAsync(created))
        {
            Assert.Equal("Scheduled", json.RootElement.GetProperty("status").GetString());
            Assert.Equal("CCheck", json.RootElement.GetProperty("type").GetString());
            Assert.Equal("2026-10-01", json.RootElement.GetProperty("scheduledDate").GetString());
        }

        // The work order cannot be completed or cancelled while this maintenance is open.
        foreach (var status in new[] { "Completed", "Cancelled" })
        {
            var close = await SendAsync(HttpMethod.Put, workOrderUri, (await _client.GetAsync(workOrderUri)).Headers.ETag!,
                new { title = "Heavy check", priority = "Low", status });
            Assert.Equal(HttpStatusCode.Conflict, close.StatusCode);
            await AssertProblemAsync(close, "WorkOrder.OpenMaintenance");
        }

        var started = await SendAsync(HttpMethod.Put, location, created.Headers.ETag!,
            new { type = "CCheck", scheduledDate = "2026-10-01", status = "InProgress" });
        Assert.Equal(HttpStatusCode.NoContent, started.StatusCode);
        using (var json = await ReadJsonAsync(await _client.GetAsync(workOrderUri)))
        {
            Assert.Equal("InProgress", json.RootElement.GetProperty("status").GetString());
            Assert.Equal(1, json.RootElement.GetProperty("maintenanceCount").GetInt32());
        }

        var stale = await SendAsync(HttpMethod.Put, location, created.Headers.ETag!,
            new { type = "ACheck", scheduledDate = "2026-10-01", status = "Scheduled" });
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        await AssertProblemAsync(stale, "MaintenanceOrder.ConcurrencyConflict");

        var deleteStarted = await SendAsync(HttpMethod.Delete, location, (await _client.GetAsync(location)).Headers.ETag!);
        Assert.Equal(HttpStatusCode.Conflict, deleteStarted.StatusCode);
        await AssertProblemAsync(deleteStarted, "MaintenanceOrder.OnlyScheduledCanBeDeleted");

        var completed = await SendAsync(HttpMethod.Put, location, (await _client.GetAsync(location)).Headers.ETag!,
            new { type = "CCheck", scheduledDate = "2026-10-01", status = "Completed" });
        Assert.Equal(HttpStatusCode.NoContent, completed.StatusCode);
        using (var json = await ReadJsonAsync(await _client.GetAsync(location)))
        {
            Assert.Equal(JsonValueKind.String, json.RootElement.GetProperty("completedAtUtc").ValueKind);
        }

        var reopen = await SendAsync(HttpMethod.Put, location, (await _client.GetAsync(location)).Headers.ETag!,
            new { type = "CCheck", scheduledDate = "2026-10-01", status = "Scheduled" });
        Assert.Equal(HttpStatusCode.Conflict, reopen.StatusCode);
        await AssertProblemAsync(reopen, "MaintenanceOrder.Closed");

        // All maintenance is completed, so the work order completed with it and freed its aircraft.
        Guid aircraftId;
        using (var json = await ReadJsonAsync(await _client.GetAsync(workOrderUri)))
        {
            Assert.Equal("Completed", json.RootElement.GetProperty("status").GetString());
            Assert.Equal(1, json.RootElement.GetProperty("completedMaintenanceCount").GetInt32());
            aircraftId = json.RootElement.GetProperty("aircraftId").GetGuid();
        }

        using (var json = await ReadJsonAsync(await _client.GetAsync($"/api/aircraft/{aircraftId}")))
        {
            Assert.Equal("Active", json.RootElement.GetProperty("status").GetString());
        }

        using (var list = await ReadJsonAsync(await _client.GetAsync($"/api/work-orders?aircraftId={aircraftId}")))
        {
            Assert.Equal(1, list.RootElement.GetProperty("items")[0].GetProperty("maintenanceCount").GetInt32());
        }

        var underClosed = await _client.PostAsJsonAsync("/api/maintenance-orders",
            new { workOrderId, type = "ACheck", scheduledDate = "2026-10-02" });
        Assert.Equal(HttpStatusCode.Conflict, underClosed.StatusCode);
        await AssertProblemAsync(underClosed, "MaintenanceOrder.WorkOrderClosed");
    }

    [Fact]
    public async Task Scheduled_maintenance_can_be_deleted_and_is_gone()
    {
        var (_, workOrderId) = await CreateWorkOrderAsync();
        var created = await _client.PostAsJsonAsync("/api/maintenance-orders",
            new { workOrderId, type = "Unscheduled", scheduledDate = "2026-10-01" });

        using (var statistics = await ReadJsonAsync(await _client.GetAsync($"/api/maintenance-orders/statistics?workOrderId={workOrderId}")))
        {
            var root = statistics.RootElement;
            Assert.Equal(1, root.GetProperty("totalCount").GetInt32());
            Assert.Equal(1, root.GetProperty("openCount").GetInt32());
            Assert.Equal(1, root.GetProperty("countByStatus").GetProperty("Scheduled").GetInt32());
            Assert.Equal(0, root.GetProperty("countByStatus").GetProperty("Completed").GetInt32());
        }

        var deleted = await SendAsync(HttpMethod.Delete, created.Headers.Location!, created.Headers.ETag!);

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(created.Headers.Location)).StatusCode);
        using var list = await ReadJsonAsync(await _client.GetAsync($"/api/maintenance-orders?workOrderId={workOrderId}"));
        Assert.Equal(0, list.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Rejects_unknown_work_order_invalid_bodies_and_missing_if_match()
    {
        var unknown = await _client.PostAsJsonAsync("/api/maintenance-orders",
            new { workOrderId = Guid.NewGuid(), type = "ACheck", scheduledDate = "2026-10-01" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, unknown.StatusCode);
        await AssertProblemAsync(unknown, "MaintenanceOrder.WorkOrderNotFound");

        var (_, workOrderId) = await CreateWorkOrderAsync();
        var missing = await _client.PostAsJsonAsync("/api/maintenance-orders", new { workOrderId, type = "ACheck" });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);

        var badType = await _client.PostAsJsonAsync("/api/maintenance-orders",
            new { workOrderId, type = 99, scheduledDate = "2026-10-01" });
        Assert.Equal(HttpStatusCode.BadRequest, badType.StatusCode);
        await AssertProblemAsync(badType, "MaintenanceOrder.Validation");

        var created = await _client.PostAsJsonAsync("/api/maintenance-orders",
            new { workOrderId, type = "ACheck", scheduledDate = "2026-10-01" });
        var noIfMatch = await _client.PutAsJsonAsync(created.Headers.Location,
            new { type = "ACheck", scheduledDate = "2026-10-01", status = "InProgress" });
        Assert.Equal((HttpStatusCode)428, noIfMatch.StatusCode);
        await AssertProblemAsync(noIfMatch, "MaintenanceOrder.PreconditionRequired");
    }

    private async Task<(Uri Location, Guid Id)> CreateWorkOrderAsync()
    {
        var registration = AircraftTestData.UniqueRegistration();
        var aircraft = await _client.PostAsJsonAsync("/api/aircraft", new
        {
            registrationNumber = registration,
            manufacturer = "Airbus",
            model = "A320-214",
            serialNumber = registration,
            yearOfManufacture = 2012,
            totalFlightHours = 100m,
            status = "Active"
        });
        Assert.Equal(HttpStatusCode.Created, aircraft.StatusCode);
        using var aircraftJson = await ReadJsonAsync(aircraft);
        var aircraftId = aircraftJson.RootElement.GetProperty("id").GetGuid();

        var workOrder = await _client.PostAsJsonAsync("/api/work-orders", new { aircraftId, title = "Heavy check", priority = "Low" });
        Assert.Equal(HttpStatusCode.Created, workOrder.StatusCode);
        using var workOrderJson = await ReadJsonAsync(workOrder);
        return (workOrder.Headers.Location!, workOrderJson.RootElement.GetProperty("id").GetGuid());
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
