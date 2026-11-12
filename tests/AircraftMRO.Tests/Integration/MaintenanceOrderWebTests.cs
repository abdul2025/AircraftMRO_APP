using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using AircraftMRO.Tests.Support;
using AircraftMRO.Web.Features.MaintenanceOrders;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AircraftMRO.Tests.Integration;

[Collection(SqlServerCollection.Name)]
public sealed class MaintenanceOrderWebTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private WebHostFactory _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _factory = new WebHostFactory(await fixture.CreateDatabaseAsync());
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Index_renders_from_feature_folder_with_navigation_and_live_refresh()
    {
        var html = await _client.GetStringAsync("/MaintenanceOrders");

        Assert.Contains("No maintenance has been scheduled yet.", html);
        Assert.Contains("href=\"/MaintenanceOrders\"", html);
        Assert.Contains("data-live-refresh=\"MaintenanceOrder\"", html);
        Assert.Contains("class=\"stat-overview\"", html);
        Assert.Contains("id=\"maintenance-status-chart-title\"", html);
        Assert.Contains("No maintenance orders yet.", html);
    }

    [Fact]
    public async Task Overview_counts_link_to_facets_and_scope_to_the_selected_work_order()
    {
        var workOrderId = await CreateWorkOrderAsync();
        foreach (var type in new[] { "ACheck", "BCheck", "CCheck" })
        {
            var created = await PostFormAsync("/MaintenanceOrders/Create", "/MaintenanceOrders/Create", MaintenanceForm(workOrderId, type));
            Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        }

        var otherWorkOrder = await CreateWorkOrderAsync();
        await PostFormAsync("/MaintenanceOrders/Create", "/MaintenanceOrders/Create", MaintenanceForm(otherWorkOrder, "DCheck"));

        var all = await _client.GetStringAsync("/MaintenanceOrders");
        Assert.Matches(@"Total maintenance orders</span>\s*<span class=""stat-panel__total-value"">4<", all);
        Assert.Matches(@"Scheduled</span>\s*<span class=""stat-share__value"">4</span>\s*<span class=""stat-share__percent"">100%<", all);
        Assert.Matches(@"Open</span>\s*<span class=""stat-highlight__value"">4<", all);
        Assert.Contains("href=\"/MaintenanceOrders?overdue=true\"", all);
        Assert.Contains("Scheduled 4 (100%), In progress 0 (0%), Completed 0 (0%)", all);

        // Scoped to one work order: counts follow it and every overview link keeps it.
        var scoped = await _client.GetStringAsync($"/MaintenanceOrders?workOrderId={workOrderId}");
        Assert.Matches(@"Total maintenance orders</span>\s*<span class=""stat-panel__total-value"">3<", scoped);
        Assert.Contains($"href=\"/MaintenanceOrders?workOrderId={workOrderId}&amp;status=InProgress\"", scoped);

        var selected = await _client.GetStringAsync("/MaintenanceOrders?status=Scheduled");
        Assert.Contains("donut--has-selection", selected);
        Assert.Contains("aria-label=\"Clear filter: Scheduled\"", selected);
    }

    [Fact]
    public async Task Scheduled_date_field_shows_the_date_format()
    {
        var form = await _client.GetStringAsync("/MaintenanceOrders/Create");

        var input = Regex.Match(form, "<input[^>]*name=\"ScheduledDate\"[^>]*>").Value;
        Assert.Contains("type=\"date\"", input);
        Assert.Contains("placeholder=\"YYYY-MM-DD\"", input);
        Assert.Contains("aria-describedby=\"scheduled-date-hint\"", input);
        Assert.Contains("id=\"scheduled-date-hint\" class=\"field-hint\">Date format: YYYY-MM-DD, e.g. 2026-11-25<", form);
    }

    [Fact]
    public async Task Edit_form_fills_the_date_picker_with_the_saved_date()
    {
        var workOrderId = await CreateWorkOrderAsync();
        var created = await PostFormAsync("/MaintenanceOrders/Create", "/MaintenanceOrders/Create", MaintenanceForm(workOrderId, "ACheck"));
        var id = created.Headers.Location!.OriginalString.Split('/').Last();

        var edit = await _client.GetStringAsync($"/MaintenanceOrders/Edit/{id}");

        // A date input only accepts yyyy-MM-dd as its value; any other format shows as empty.
        var input = Regex.Match(edit, "<input[^>]*name=\"ScheduledDate\"[^>]*>").Value;
        Assert.Contains("type=\"date\"", input);
        Assert.Contains("value=\"2026-10-15\"", input);
    }

    [Fact]
    public async Task Schedule_from_work_order_then_work_order_cannot_close_until_maintenance_completes()
    {
        var workOrderId = await CreateWorkOrderAsync();
        var workOrderPage = await _client.GetStringAsync($"/WorkOrders/Details/{workOrderId}");
        Assert.Contains($"/MaintenanceOrders/Create?workOrderId={workOrderId}", WebUtility.HtmlDecode(workOrderPage));
        Assert.Contains($"/MaintenanceOrders?workOrderId={workOrderId}", WebUtility.HtmlDecode(workOrderPage));

        var created = await PostFormAsync($"/MaintenanceOrders/Create?workOrderId={workOrderId}", "/MaintenanceOrders/Create",
            MaintenanceForm(workOrderId, "BCheck"));
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        var detailsUrl = created.Headers.Location!.OriginalString;
        var id = detailsUrl.Split('/').Last();
        var details = await _client.GetStringAsync(detailsUrl);
        Assert.Contains("Maintenance scheduled.", details);
        Assert.Contains("B-check", details);
        Assert.Contains(">Scheduled<", details);

        // Closing the work order is refused with a 409 that names the reason.
        var close = await EditWorkOrderAsync(workOrderId, "Completed");
        Assert.Equal(HttpStatusCode.Conflict, close.StatusCode);
        Assert.Contains("scheduled or in-progress maintenance", WebUtility.HtmlDecode(await close.Content.ReadAsStringAsync()));

        // Start it: it can no longer be deleted.
        Assert.Equal(HttpStatusCode.Redirect, (await EditMaintenanceAsync(id, workOrderId, "InProgress")).StatusCode);
        var deletePage = await _client.GetStringAsync($"/MaintenanceOrders/Delete/{id}");
        Assert.Contains("Only scheduled maintenance can be deleted", deletePage);
        Assert.DoesNotContain("Delete maintenance</button>", deletePage);

        // Complete it: it becomes final, and as the work order's only maintenance it completes the work order.
        Assert.Equal(HttpStatusCode.Redirect, (await EditMaintenanceAsync(id, workOrderId, "Completed")).StatusCode);
        Assert.DoesNotContain($"/MaintenanceOrders/Edit/{id}", await _client.GetStringAsync(detailsUrl));
        var completedWorkOrder = WebUtility.HtmlDecode(await _client.GetStringAsync($"/WorkOrders/Details/{workOrderId}"));
        Assert.Contains(">Completed<", completedWorkOrder);
        Assert.Contains(">1 order</a>", completedWorkOrder);
        Assert.Contains("· 1 completed", completedWorkOrder);
        var workOrderList = WebUtility.HtmlDecode(await _client.GetStringAsync("/WorkOrders"));
        Assert.Contains(">Maintenance</th>", workOrderList);
        Assert.Contains($"href=\"/MaintenanceOrders?workOrderId={workOrderId}\"", workOrderList);
        Assert.Contains("aria-label=\"1 maintenance order for WO-", workOrderList);

        // A closed work order offers no scheduling link and rejects a forced create.
        Assert.DoesNotContain($"/MaintenanceOrders/Create?workOrderId={workOrderId}",
            WebUtility.HtmlDecode(await _client.GetStringAsync($"/WorkOrders/Details/{workOrderId}")));
        var rejected = await PostFormAsync("/MaintenanceOrders/Create", "/MaintenanceOrders/Create", MaintenanceForm(workOrderId, "ACheck"));
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
    }

    [Fact]
    public async Task Scheduled_maintenance_can_be_deleted_from_the_modal()
    {
        var workOrderId = await CreateWorkOrderAsync();
        var created = await PostFormAsync("/MaintenanceOrders/Create", "/MaintenanceOrders/Create", MaintenanceForm(workOrderId, "ACheck"));
        var id = created.Headers.Location!.OriginalString.Split('/').Last();

        var deleteHtml = await (await ModalGetAsync($"/MaintenanceOrders/Delete/{id}")).Content.ReadAsStringAsync();
        Assert.Contains("Delete maintenance</button>", deleteHtml);
        var form = new Dictionary<string, string> { ["rowVersion"] = HiddenValue(deleteHtml, "rowVersion") };
        var deleted = await ModalPostAsync($"/MaintenanceOrders/Delete/{id}", deleteHtml, form);

        Assert.Equal("/MaintenanceOrders", await ReadRedirectUrlAsync(deleted));
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/MaintenanceOrders/Details/{id}")).StatusCode);
    }

    [Fact]
    public async Task Create_validation_returns_422_and_keeps_the_form()
    {
        var workOrderId = await CreateWorkOrderAsync();
        var form = MaintenanceForm(workOrderId, "ACheck");
        form.Remove("ScheduledDate");
        form.Remove("WorkOrderId");

        var invalid = await PostFormAsync("/MaintenanceOrders/Create", "/MaintenanceOrders/Create", form);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
        var html = await invalid.Content.ReadAsStringAsync();
        Assert.Contains("Select a work order.", html);
        Assert.Contains("input-validation-error", html);
    }

    [Fact]
    public async Task Maintenance_notifications_link_to_the_maintenance_details_page()
    {
        var workOrderId = await CreateWorkOrderAsync();
        var created = await PostFormAsync("/MaintenanceOrders/Create", "/MaintenanceOrders/Create", MaintenanceForm(workOrderId, "DCheck"));
        var id = created.Headers.Location!.OriginalString.Split('/').Last();

        using var catchUp = JsonDocument.Parse(await _client.GetStringAsync("/Notifications/Since?afterId=0"));
        var message = catchUp.RootElement.EnumerateArray()
            .Single(m => m.GetProperty("entityType").GetString() == "MaintenanceOrder");
        Assert.Equal($"/MaintenanceOrders/Details/{id}", message.GetProperty("url").GetString());
    }

    private async Task<Guid> CreateWorkOrderAsync()
    {
        var registration = AircraftTestData.UniqueRegistration();
        var aircraft = await PostFormAsync("/Aircraft/Create", "/Aircraft/Create", new Dictionary<string, string>
        {
            ["RegistrationNumber"] = registration,
            ["Manufacturer"] = "Airbus",
            ["Model"] = "A320-214",
            ["SerialNumber"] = registration,
            ["YearOfManufacture"] = "2012",
            ["TotalFlightHours"] = "31250.5",
            ["Status"] = "Active"
        });
        Assert.Equal(HttpStatusCode.Redirect, aircraft.StatusCode);
        var aircraftId = aircraft.Headers.Location!.OriginalString.Split('/').Last();

        var workOrder = await PostFormAsync("/WorkOrders/Create", "/WorkOrders/Create", WorkOrderForm(aircraftId, "Open"));
        Assert.Equal(HttpStatusCode.Redirect, workOrder.StatusCode);
        return Guid.Parse(workOrder.Headers.Location!.OriginalString.Split('/').Last());
    }

    private static Dictionary<string, string> WorkOrderForm(string aircraftId, string status) => new()
    {
        ["AircraftId"] = aircraftId,
        ["Title"] = "Heavy check",
        ["Priority"] = "Low",
        ["Status"] = status
    };

    private static Dictionary<string, string> MaintenanceForm(Guid workOrderId, string type) => new()
    {
        ["WorkOrderId"] = workOrderId.ToString(),
        ["Type"] = type,
        ["ScheduledDate"] = "2026-10-15",
        ["Status"] = "Scheduled",
        ["Notes"] = "Hangar 2"
    };

    private async Task<HttpResponseMessage> EditWorkOrderAsync(Guid workOrderId, string status)
    {
        var editPage = await _client.GetStringAsync($"/WorkOrders/Edit/{workOrderId}");
        var form = WorkOrderForm(string.Empty, status);
        form.Remove("AircraftId");
        form["RowVersion"] = HiddenValue(editPage, "RowVersion");
        return await PostFormAsync($"/WorkOrders/Edit/{workOrderId}", $"/WorkOrders/Edit/{workOrderId}", form);
    }

    private async Task<HttpResponseMessage> EditMaintenanceAsync(string id, Guid workOrderId, string status)
    {
        var form = MaintenanceForm(workOrderId, "BCheck");
        form["Status"] = status;
        form["RowVersion"] = HiddenValue(await _client.GetStringAsync($"/MaintenanceOrders/Edit/{id}"), "RowVersion");
        return await PostFormAsync($"/MaintenanceOrders/Edit/{id}", $"/MaintenanceOrders/Edit/{id}", form);
    }

    private Task<HttpResponseMessage> ModalGetAsync(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add(MaintenanceOrdersController.ModalRequestHeader, "true");
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> ModalPostAsync(string url, string formHtml, Dictionary<string, string> form)
    {
        form["__RequestVerificationToken"] = HiddenValue(formHtml, "__RequestVerificationToken");
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(form) };
        request.Headers.Add(MaintenanceOrdersController.ModalRequestHeader, "true");
        return _client.SendAsync(request);
    }

    private static async Task<string> ReadRedirectUrlAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("redirectUrl").GetString()!;
    }

    private async Task<HttpResponseMessage> PostFormAsync(string formPageUrl, string postUrl, Dictionary<string, string> form)
    {
        var page = await _client.GetStringAsync(formPageUrl);
        form["__RequestVerificationToken"] = HiddenValue(page, "__RequestVerificationToken");
        return await _client.PostAsync(postUrl, new FormUrlEncodedContent(form));
    }

    private static string HiddenValue(string html, string name)
    {
        var match = Regex.Match(html, $"name=\"{Regex.Escape(name)}\"[^>]*value=\"([^\"]*)\"|value=\"([^\"]*)\"[^>]*name=\"{Regex.Escape(name)}\"");
        Assert.True(match.Success, $"Hidden input '{name}' not found.");
        return WebUtility.HtmlDecode(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value);
    }
}
